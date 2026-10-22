using System.Collections.Concurrent;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Deployment;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Tests.Deployment;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed partial class KubernetesWatchTests
{
    private const string InitialVersion = "list/rv&1";
    private static readonly byte[] ArtifactBytes = [1, 2, 3];
    private static readonly string[] ArtifactNames = [.. WorkerPublicationFixture.ArtifactNames, "index.json"];

    [Fact]
    public async Task Given_ContinuousWatchWithBookmarks_When_RenewedAndDrained_Then_EveryRequestResumesItsLastAcknowledgedVersion()
    {
        var initial = Step.Return(Event("BOOKMARK", "initial/rv&2"));
        var producer = Step.Return(Event("ADDED", "producer/rv&3", "producer-pod", "uid-1"));
        var renewal = Step.Return(Event("BOOKMARK", "bookmark/rv&4"));
        var drain = Step.Return(Event("MODIFIED", "final/rv&5", "producer-pod", "uid-1"));
        using var fixture = new WatchFixture(initial, producer, renewal, drain);
        await fixture.BindAsync();
        PodAdmission admission = await fixture.Cluster.EstablishProducerWatchAsync(fixture.Context, TestContext.Current.CancellationToken);
        await renewal.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(new ProducerPod("producer-pod", "uid-1"), admission.Producer);

        await fixture.Cluster.CompleteProducerWatchAsync(fixture.Context, admission, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { InitialVersion, "initial/rv&2", "producer/rv&3", "bookmark/rv&4" },
            fixture.Process.WatchRequests.Select(ResourceVersion));
        Assert.Equal("final/rv&5", admission.Events[^1].ResourceVersion);
        Assert.Equal(2, admission.Events.Count(item => item.Type == "BOOKMARK"));
        Assert.All(admission.Events.Where(item => item.Type == "BOOKMARK"), item => Assert.Null(item.Pod));
        Assert.Equal(1, fixture.Process.InitialLists);
        Assert.All(fixture.Process.WatchRequests, request => AssertRawRequest(request, fixture));
    }

    [Theory]
    [InlineData("worker")]
    [InlineData("export")]
    public async Task Given_ReplacementAddedAndDeletedWhileOtherWorkWaits_When_ExportEnds_Then_HistoricalSecondUidPreventsAcceptance(string stage)
    {
        var replacement = new TaskCompletionSource<ProcessResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var initial = Step.Return(Event("ADDED", "rv-2", "producer-pod", "uid-1"));
        var ongoing = new Step(token => replacement.Task.WaitAsync(token));
        using var fixture = new WatchFixture(initial, ongoing);
        await fixture.BindAsync();
        PodAdmission admission = await fixture.Cluster.EstablishProducerWatchAsync(fixture.Context, TestContext.Current.CancellationToken);
        await ongoing.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("uid-1", (await fixture.Cluster.ObserveProducerAsync(fixture.Context, admission, TestContext.Current.CancellationToken)).Uid);
        Task otherWork = stage == "worker"
            ? fixture.Cluster.WaitProducerAsync(fixture.Context, TestContext.Current.CancellationToken)
            : fixture.Cluster.DownloadPublicationAsync(fixture.Context, TestContext.Current.CancellationToken);
        await fixture.Process.OtherWorkStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        replacement.SetResult(new(0,
            Event("ADDED", "rv-3", "replacement-pod", "uid-2") + "\n" +
            Event("DELETED", "rv-4", "replacement-pod", "uid-2"), string.Empty));
        await ongoing.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        fixture.Process.OtherWorkRelease.TrySetResult();
        await otherWork.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Cluster.CompleteProducerWatchAsync(
            fixture.Context, admission, TestContext.Current.CancellationToken));
        Assert.Throws<InvalidDataException>(() => admission.Producer);
        Assert.Contains(admission.Events, item => item.Pod?.Uid == "uid-2");
        Assert.Equal(2, fixture.Process.WatchRequests.Count);
        Assert.Equal(1, fixture.Process.InitialLists);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("failed-process")]
    [InlineData("timeout")]
    public async Task Given_WatchExpiresOrTransportLosesHistory_When_BackgroundWatchCompletes_Then_LaterSnapshotCannotAuthorizeAdmission(string failure)
    {
        var initial = Step.Return(Event("ADDED", "rv-2", "producer-pod", "uid-1"));
        var lost = new Step(_ => Task.FromResult(failure switch
        {
            "expired" => new ProcessResult(0, "{\"type\":\"ERROR\",\"object\":{\"kind\":\"Status\",\"code\":410,\"reason\":\"Expired\"}}", string.Empty),
            "timeout" => new ProcessResult(0, string.Empty, string.Empty, TimedOut: true),
            _ => new ProcessResult(1, string.Empty, "synthetic disconnected watch")
        }));
        using var fixture = new WatchFixture(initial, lost);
        await fixture.BindAsync();
        PodAdmission admission = await fixture.Cluster.EstablishProducerWatchAsync(fixture.Context, TestContext.Current.CancellationToken);
        await lost.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Cluster.CompleteProducerWatchAsync(
            fixture.Context, admission, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Cluster.ObserveProducerAsync(
            fixture.Context, admission, TestContext.Current.CancellationToken));
        Assert.Equal(1, fixture.Process.InitialLists);
        Assert.Equal(2, fixture.Process.WatchRequests.Count);
    }

    [Fact]
    public async Task Given_InFlightWatch_When_NormalCompletionCancelsIt_Then_JoinsItsCleanupBeforeFinalDrain()
    {
        var cancellationSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var initial = Step.Return(Event("ADDED", "rv-2", "producer-pod", "uid-1"));
        var running = new Step(async token =>
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                cancellationSeen.TrySetResult();
                await cleanupRelease.Task.WaitAsync(TestContext.Current.CancellationToken);
                throw;
            }
            throw new InvalidOperationException("watch_fixture_must_be_cancelled");
        });
        var drain = new Step(_ =>
        {
            Assert.True(running.Completed.Task.IsCompleted);
            return Task.FromResult(new ProcessResult(0, Event("BOOKMARK", "rv-final"), string.Empty));
        });
        using var fixture = new WatchFixture(initial, running, drain);
        await fixture.BindAsync();
        PodAdmission admission = await fixture.Cluster.EstablishProducerWatchAsync(fixture.Context, TestContext.Current.CancellationToken);
        await running.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Task complete = fixture.Cluster.CompleteProducerWatchAsync(fixture.Context, admission, TestContext.Current.CancellationToken);
        try
        {
            await cancellationSeen.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(complete.IsCompleted);
            Assert.False(drain.Started.Task.IsCompleted);
        }
        finally { cleanupRelease.TrySetResult(); }
        await complete.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("rv-final", admission.Events[^1].ResourceVersion);
        Assert.Equal("rv-2", ResourceVersion(fixture.Process.WatchRequests.Last()));
    }

    [Fact]
    public async Task Given_ExternalCancellation_When_WatchIsInFlight_Then_JoinsBackgroundFailureAndDoesNotStartFinalRead()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var initial = Step.Return(Event("ADDED", "rv-2", "producer-pod", "uid-1"));
        var running = new Step(async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("watch_fixture_must_be_cancelled");
        });
        using var fixture = new WatchFixture(initial, running);
        await fixture.BindAsync();
        PodAdmission admission = await fixture.Cluster.EstablishProducerWatchAsync(fixture.Context, cancellation.Token);
        await running.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Cluster.CompleteProducerWatchAsync(
            fixture.Context, admission, cancellation.Token));
        Assert.True(running.Completed.Task.IsCompleted);
        Assert.Equal(2, fixture.Process.WatchRequests.Count);
    }

    private static string Event(string type, string version, string? name = null, string? uid = null) =>
        name is null ? JsonSerializer.Serialize(new { type, @object = new { metadata = new { resourceVersion = version } } }) :
        JsonSerializer.Serialize(new { type, @object = new { metadata = new { resourceVersion = version, name, uid } } });

    private static string ResourceVersion(ProcessRequest request)
    {
        string query = request.Arguments[2];
        string value = query.Split('&').Single(part => part.StartsWith("resourceVersion=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(value["resourceVersion=".Length..]);
    }

    private static void AssertRawRequest(ProcessRequest request, WatchFixture fixture)
    {
        string expected = "/api/v1/namespaces/parchwell/pods?watch=true&resourceVersion=" +
            Uri.EscapeDataString(ResourceVersion(request)) + "&labelSelector=" +
            Uri.EscapeDataString("app.kubernetes.io/instance=" + fixture.Context.RunId + ",parchwell.io/role=loadgen") +
            "&timeoutSeconds=1&allowWatchBookmarks=true";
        Assert.Equal("kubectl", request.FileName);
        Assert.Equal(new[] { "get", "--raw", expected, "--request-timeout=60s" }, request.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout);
        Assert.Equal(16777216, request.MaxOutputBytes);
        Assert.Equal(fixture.KubeconfigPath, request.Environment!["KUBECONFIG"]);
    }

    private sealed class WatchFixture : IDisposable
    {
        private readonly DeploymentFixture _deployment = new();
        internal WatchProcess Process { get; }
        internal KubernetesRunCluster Cluster { get; }
        internal string KubeconfigPath => Path.Combine(_deployment.Directory, "private-context", "config");
        internal RepeatContext Context { get; }
        internal WatchFixture(params Step[] steps) : this(TimeProvider.System, steps) { }
        internal WatchFixture(TimeProvider clock, params Step[] steps)
        {
            Context = new("probe", 1, "synthetic-a01-r1", new string('a', 40), _deployment.Directory);
            Process = new WatchProcess(KubeconfigPath, Context, steps);
            Cluster = new(Process, _deployment.Generator(), new ChartRenderer(Process), JsonSerializer.SerializeToElement(new { }),
                "unused-chart", clock, privateKubeconfigPath: KubeconfigPath);
        }
        internal Task BindAsync() => Cluster.BindClusterAsync(JsonSerializer.SerializeToElement(new
        {
            aks = new { name = "aks-synthetic", id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic/providers/Microsoft.ContainerService/managedClusters/aks-synthetic" },
            blob = new { account_url = "https://storage.example.invalid" }
        }), TestContext.Current.CancellationToken);
        public void Dispose()
        {
            Process.OtherWorkRelease.TrySetResult();
            Cluster.Dispose();
            _deployment.Dispose();
        }
    }

    private sealed class Step(Func<CancellationToken, Task<ProcessResult>> response)
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal async Task<ProcessResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try { return await response(cancellationToken); }
            finally { Completed.TrySetResult(); }
        }
        internal static Step Return(string stdout) => new(_ => Task.FromResult(new ProcessResult(0, stdout, string.Empty)));
    }

    private sealed class WatchProcess(string kubeconfigPath, RepeatContext context, IEnumerable<Step> steps) : IProcessRunner
    {
        private readonly ConcurrentQueue<Step> _steps = new(steps);
        internal ConcurrentQueue<ProcessRequest> WatchRequests { get; } = new();
        internal int InitialLists { get; private set; }
        internal TaskCompletionSource OtherWorkStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource OtherWorkRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.FileName != "az" || request.Arguments[0] == "aks")
                Assert.Equal(kubeconfigPath, request.Environment!["KUBECONFIG"]);
            if (request.FileName == "az" && request.Arguments[0] == "aks")
            {
                await File.WriteAllTextAsync(kubeconfigPath, "synthetic private kubeconfig", cancellationToken);
                return new(0, string.Empty, string.Empty);
            }
            if (request.FileName == "kubelogin")
                return new(0, request.Arguments[0] == "--version" ? "kubelogin synthetic-version" : "--login supports azurecli", string.Empty);
            if (request.FileName == "kubectl" && request.Arguments[0] == "apply") return new(0, string.Empty, string.Empty);
            if (request.FileName == "kubectl" && request.Arguments[1] == "--raw")
            {
                WatchRequests.Enqueue(request);
                Assert.True(_steps.TryDequeue(out Step? step), "No unplanned watch request is allowed.");
                return await step.ExecuteAsync(cancellationToken);
            }
            if (request.FileName == "kubectl" && request.Arguments[0] == "get")
            {
                if (request.Arguments.Contains("job-name=" + context.RunId + "-loadgen", StringComparer.Ordinal))
                {
                    await WaitOtherWorkAsync(cancellationToken);
                    return new(0, JsonSerializer.Serialize(new { items = new[] { new
                    {
                        metadata = new { name = "producer-pod", uid = "uid-1" },
                        status = new { containerStatuses = new[] { new { name = "loadgen", restartCount = 0, state = new { terminated = new { exitCode = 0 } } } } }
                    } } }), string.Empty);
                }
                InitialLists++;
                return new(0, JsonSerializer.Serialize(new { metadata = new { resourceVersion = InitialVersion }, items = Array.Empty<object>() }), string.Empty);
            }
            if (request.FileName == "kubectl" && request.Arguments[0] == "logs") return new(0, "synthetic worker log", string.Empty);
            if (request.FileName == "az" && request.Arguments[2] == "list")
            {
                await WaitOtherWorkAsync(cancellationToken);
                string prefix = "A01/C0/" + context.RunId + "/producer-pod/";
                return new(0, JsonSerializer.Serialize(ArtifactNames.Select(name => new { name = prefix + name })), string.Empty);
            }
            if (request.FileName == "az" && request.Arguments[2] == "show")
                return new(0, JsonSerializer.Serialize(new { etag = "synthetic-etag", contentLength = ArtifactBytes.Length }), string.Empty);
            if (request.FileName == "az" && request.Arguments[2] == "download")
            {
                int fileIndex = request.Arguments.ToList().IndexOf("--file");
                await File.WriteAllBytesAsync(request.Arguments[fileIndex + 1], ArtifactBytes, cancellationToken);
                return new(0, string.Empty, string.Empty);
            }
            throw new InvalidOperationException("unexpected_watch_fixture_command");
        }
        private async Task WaitOtherWorkAsync(CancellationToken cancellationToken)
        {
            OtherWorkStarted.TrySetResult();
            await OtherWorkRelease.Task.WaitAsync(cancellationToken);
        }
    }
}
#pragma warning restore CA1707
