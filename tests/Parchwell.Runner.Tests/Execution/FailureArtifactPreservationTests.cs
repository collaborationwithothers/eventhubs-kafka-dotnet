using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Deployment;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Run.Rehearsal;
using Parchwell.Runner.Tests.Cost;
using Parchwell.Runner.Tests.Deployment;
using Parchwell.Verifier.StartLine;
using Xunit;
using VerifierIdentity = Parchwell.Verifier.Common.Contracts.RunIdentity;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Repository-required Given_When_Then names.

public sealed class FailureArtifactPreservationTests
{
    [Theory]
    [InlineData("loadgen_exit")]
    [InlineData("loadgen_receipt")]
    [InlineData("partial_publication")]
    [InlineData("start_line_exit")]
    [InlineData("start_line_receipt")]
    [InlineData("pre_admission")]
    [InlineData("producer_wait")]
    [InlineData("release_uncertain")]
    [InlineData("cancelled_repeat")]
    [InlineData("verify_exit")]
    [InlineData("verify_transport")]
    [InlineData("oversize_artifact")]
    [InlineData("partial_transfer")]
    [InlineData("collection_cancelled")]
    public async Task Given_PublishedOriginalsAndFailedRepeat_When_PhaseCleansUp_Then_AvailableBytesArePrivatelyVerifiedBeforeDestroyWithoutCountsOrReplacement(string failure)
    {
        using var deployment = new DeploymentFixture();
        string output = Path.Combine(deployment.Directory, "phase");
        var trace = new List<string>();
        var clock = new CaptureClock();
        var process = new ArtifactProcess(failure, trace) { CancelCapture = clock.CancelCapture };
        using var production = new KubernetesRunCluster(process, deployment.Generator(), new ChartRenderer(process),
            JsonSerializer.SerializeToElement(new { }), "unused-chart", TimeProvider.System,
            privateKubeconfigPath: Path.Combine(deployment.Directory, "credentials", "config"));
        await production.BindClusterAsync(JsonSerializer.SerializeToElement(new
        {
            aks = new { name = "synthetic", id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/synthetic/providers/Microsoft.ContainerService/managedClusters/synthetic" },
            blob = new { account_url = "https://storage.example.invalid" }
        }), TestContext.Current.CancellationToken);
        using var repeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cluster = new FailingCluster(production, process, failure, repeatCancellation);
        var store = new RecordingStore(trace);
        var ledger = new RunLedger(store, TimeProvider.System);
        var environment = new RehearsalEnvironment(trace: trace);
        var executor = new PhaseExecutor(ledger, environment, new RepeatExecutor(cluster, TimeProvider.System),
            new EvidenceExporter(store, TimeProvider.System), new GenerationGuard(environment, TimeProvider.System), clock,
            collectCorroboration: (directory, token) =>
            {
                if (failure == "collection_cancelled")
                {
                    trace.Add("corroboration_started");
                    clock.CancelCapture();
                    token.ThrowIfCancellationRequested();
                }
                return Task.CompletedTask;
            });

        PhaseExecution result = await executor.ExecuteAsync(new("probe", "failure-phase", new string('a', 40), output,
            PhaseReservationTests.Input(), true), repeatCancellation.Token);

        Assert.Equal("failed", result.Result);
        Assert.Empty(result.Repeats);
        Assert.True(result.Destroyed);
        Assert.True(result.Exported);
        Assert.Equal(1, cluster.Created);
        Assert.Equal(failure.StartsWith("verify_", StringComparison.Ordinal) ? 1 : 0, cluster.Verifications);
        Assert.Equal(1, environment.Applies);
        Assert.Equal(1, environment.Destroys);
        PhaseRecord phase = (await ledger.ReadAsync(TestContext.Current.CancellationToken)).Phases["probe"];
        Assert.Equal(failure == "cancelled_repeat" ? "started" : "interrupted", phase.Repeats[0].State);
        Assert.All(phase.Repeats.Skip(1), repeat => Assert.Equal("not_started", repeat.State));
        Assert.True(trace.IndexOf("artifact_read") < trace.IndexOf("private_readback"));
        Assert.True(trace.IndexOf("private_readback") < trace.IndexOf("destroy_plan"));
        string prefix = "A01/failure-phase/failure-phase-r1/";
        var exported = store.Inner.Objects.Where(item => item.Key.Container == "evidence").ToDictionary(item => item.Key.Path, item => item.Value.Bytes, StringComparer.Ordinal);
        Assert.Contains(prefix + "pod-history.json", exported.Keys);
        string receiptPath = prefix + "failure-artifacts/collection-receipt.json";
        using JsonDocument receipt = JsonDocument.Parse(exported[failure == "collection_cancelled" ? prefix + "failure-artifacts-unavailable.json" : receiptPath]);
        Assert.False(receipt.RootElement.GetProperty("accepted_counts").GetBoolean());
        if (failure == "collection_cancelled")
            Assert.Equal("failure_artifact_collection_incomplete", receipt.RootElement.GetProperty("reason").GetString());
        else
        {
            Assert.False(receipt.RootElement.GetProperty("execution_completed").GetBoolean());
            Assert.Equal(failure != "pre_admission", receipt.RootElement.GetProperty("producer_identity_available").GetBoolean());
        }
        foreach (var blob in process.Blobs)
        {
            if (failure == "pre_admission" && blob.Key.Container == "ledger") continue;
            if (failure == "collection_cancelled" && blob.Key.Name.EndsWith("/result.json", StringComparison.Ordinal)) continue;
            if (failure == "oversize_artifact" && blob.Key.Name.EndsWith("/ledger.jsonl", StringComparison.Ordinal)) continue;
            byte[] expected = failure == "partial_transfer" && blob.Key.Name.EndsWith("/ledger.jsonl", StringComparison.Ordinal) ? blob.Value[..2] : blob.Value;
            string filename = blob.Key.Name.Split('/')[^1];
            Assert.Contains(exported, item => item.Key.StartsWith(prefix, StringComparison.Ordinal) &&
                item.Key.EndsWith("/" + filename, StringComparison.Ordinal) && item.Value.AsSpan().SequenceEqual(expected));
        }
        if (failure.StartsWith("verify_", StringComparison.Ordinal))
            foreach (string name in new[] { "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "index.json" })
            {
                Assert.Contains(prefix + name, exported.Keys);
                Assert.DoesNotContain(prefix + "downloads/" + name, exported.Keys);
                Assert.DoesNotContain(prefix + "failure-artifacts/ledger/" + name, exported.Keys);
            }
        if (failure == "collection_cancelled")
        {
            Assert.Contains("A01/failure-phase/kql-unavailable.json", exported.Keys);
            Assert.True(trace.IndexOf("corroboration_started") < trace.IndexOf("private_readback"));
        }
        if (failure is "partial_publication" or "oversize_artifact" or "partial_transfer")
            Assert.Contains(receipt.RootElement.GetProperty("artifacts").EnumerateArray(), item => item.GetProperty("status").GetString() == "unavailable");
        Assert.DoesNotContain(process.Requests, request => request.FileName == "az" && request.Arguments[2] is "upload" or "delete");
        Assert.All(process.Requests.Where(request => request.FileName == "az" && request.Arguments[0] == "storage"),
            request => Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout));
    }

    private sealed class FailingCluster(KubernetesRunCluster production, ArtifactProcess process, string failure,
        CancellationTokenSource repeatCancellation) : IRunCluster
    {
        internal int Created { get; private set; }
        internal int Verifications { get; private set; }
        public Task PreflightAsync(JsonElement outputs, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<PodAdmission> EstablishProducerWatchAsync(RepeatContext context, CancellationToken cancellationToken) => production.EstablishProducerWatchAsync(context, cancellationToken);
        public Task CreateProducerAsync(RepeatContext context, CancellationToken cancellationToken) { Created++; return Task.CompletedTask; }
        public Task<ProducerPod> ObserveProducerAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken) =>
            failure == "pre_admission" ? throw new IOException("synthetic_admission_failed") : production.ObserveProducerAsync(context, admission, cancellationToken);
        public Task<string> GenerateComparisonAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken) => Task.FromResult(new string('b', 64));
        public async Task<StartLineExecution> StartLineAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken)
        {
            process.Initialize(context);
            VerifierIdentity worker = Identity(context);
            byte[] inventory = BrokerInventoryCodec.Write(worker, [new BrokerMetadata(1, "broker.example.invalid", 9093)], context.SourceCommit, 16777216);
            process.Blobs[("verifier", context.Phase + "/" + context.RunId + "/broker-inventory.json")] = inventory;
            string receipt = Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("start-line", worker, context.SourceCommit));
            return new(new(new(failure == "start_line_exit" ? 1 : 0, failure == "start_line_receipt" ? "original stdout without receipt" : receipt, "")), inventory);
        }
        public Task ReleaseAsync(RepeatContext context, byte[] permit, CancellationToken cancellationToken) => production.ReleaseAsync(context, permit, cancellationToken);
        public async Task<WorkerExecution> WaitProducerAsync(RepeatContext context, CancellationToken cancellationToken)
        {
            if (failure == "producer_wait") throw new IOException("synthetic_worker_read_failed");
            if (failure == "cancelled_repeat") { repeatCancellation.Cancel(); cancellationToken.ThrowIfCancellationRequested(); }
            string receipt = Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("loadgen", Identity(context), context.SourceCommit));
            return new(new(failure == "loadgen_exit" || failure is "oversize_artifact" or "partial_transfer" or "collection_cancelled" ? 1 : 0,
                failure == "loadgen_receipt" ? "original stdout without receipt" : receipt, ""));
        }
        public async Task<ProducerPublication> DownloadPublicationAsync(RepeatContext context, CancellationToken cancellationToken)
        {
            if (failure.StartsWith("verify_", StringComparison.Ordinal))
            {
                var identity = Identity(context);
                var golden = await WorkerPublicationFixture.CreateAsync(Path.Combine(context.OutputDirectory, "fixture-producer"),
                    new(identity.ScenarioId, identity.ConfigurationId, identity.RunId, identity.InstanceId, identity.ExpectedInstanceId, identity.ClientId), cancellationToken);
                foreach (var item in golden.Payloads) process.Blobs[("ledger", "A01/C0/" + context.RunId + "/" + identity.InstanceId + "/" + item.Key)] = item.Value;
            }
            return await production.DownloadPublicationAsync(context, cancellationToken);
        }
        public Task<WorkerExecution> VerifyAsync(RepeatContext context, WorkerIdentity identity, IReadOnlyList<string> brokers, CancellationToken cancellationToken)
        {
            Verifications++;
            if (failure == "verify_transport") throw new IOException("synthetic_verifier_transport_failed");
            return Task.FromResult(new WorkerExecution(new(1, "original failed verifier stdout", ""), process.Blobs[("verifier", "probe/" + context.RunId + "/result.json")]));
        }
        public Task PreserveAvailableArtifactsAsync(RepeatContext context, CancellationToken cancellationToken)
        {
            Assert.False(cancellationToken.IsCancellationRequested);
            process.Initialize(context);
            return production.PreserveAvailableArtifactsAsync(context, cancellationToken);
        }
        public Task CompleteProducerWatchAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken) => production.CompleteProducerWatchAsync(context, admission, cancellationToken);
        private static VerifierIdentity Identity(RepeatContext context) => new("A01", "C0", context.RunId, context.RunId + "-pod", context.RunId + "-pod", context.RunId + "-pod");
    }

    private sealed class CaptureClock : TimeProvider
    {
        private TimerCallback? callback;
        private object? callbackState;
        internal void CancelCapture() => callback!(callbackState);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime != TimeSpan.FromSeconds(60)) return System.CreateTimer(callback, state, dueTime, period);
            this.callback = callback;
            callbackState = state;
            return new CaptureTimer();
        }
        private sealed class CaptureTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingStore(List<string> trace) : IArtifactStore
    {
        internal PersistentTestStore Inner { get; } = new();
        public async Task<StoredArtifact?> ReadAsync(string container, string path, int maxBytes, CancellationToken cancellationToken)
        {
            StoredArtifact? result = await Inner.ReadAsync(container, path, maxBytes, cancellationToken);
            if (container == "evidence") trace.Add("private_readback");
            return result;
        }
        public Task<ArtifactWriteOutcome> WriteAsync(string container, string path, ReadOnlyMemory<byte> bytes, string? expectedETag, bool createOnly, CancellationToken cancellationToken) =>
            Inner.WriteAsync(container, path, bytes, expectedETag, createOnly, cancellationToken);
    }

    private sealed class ArtifactProcess(string failure, List<string> trace) : IProcessRunner
    {
        internal Dictionary<(string Container, string Name), byte[]> Blobs { get; } = [];
        internal List<ProcessRequest> Requests { get; } = [];
        internal Action? CancelCapture { get; init; }
        private int storageReads;
        private bool initialized;
        private int watches;
        internal void Initialize(RepeatContext context)
        {
            if (initialized) return;
            initialized = true;
            foreach (string name in new[] { "start-line.json", "broker-inventory.json", "result.json" })
                Blobs[("verifier", context.Phase + "/" + context.RunId + "/" + name)] = Encoding.UTF8.GetBytes("synthetic original " + name + "\n");
            foreach (string name in new[] { "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "index.json" })
                if (failure != "partial_publication" || name is "ledger.jsonl" or "manifest.json")
                    Blobs[("ledger", "A01/C0/" + context.RunId + "/" + context.RunId + "-pod/" + name)] = Encoding.UTF8.GetBytes("synthetic original " + name + "\n");
        }
        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            var args = request.Arguments;
            if (request.FileName == "az" && args[0] == "aks")
            { await File.WriteAllTextAsync(Value(args, "--file"), "synthetic kubeconfig", cancellationToken); return new(0, "", ""); }
            if (request.FileName == "kubelogin") return new(0, args[0] == "--version" ? "synthetic version" : "--login azurecli", "");
            if (request.FileName == "kubectl")
            {
                if (failure == "release_uncertain" && args[0] == "patch") return new(1, "", "synthetic uncertain response after permit delivery");
                if (args[1] == "--raw") return new(0, ++watches == 1
                    ? "{\"type\":\"ADDED\",\"object\":{\"metadata\":{\"resourceVersion\":\"2\",\"name\":\"failure-phase-r1-pod\",\"uid\":\"original-uid\"}}}"
                    : "{\"type\":\"BOOKMARK\",\"object\":{\"metadata\":{\"resourceVersion\":\"3\"}}}", "");
                return new(0, "{\"metadata\":{\"resourceVersion\":\"1\"},\"items\":[]}", "");
            }
            Assert.Equal("az", request.FileName);
            Assert.Equal("storage", args[0]);
            trace.Add("artifact_read");
            if (failure == "collection_cancelled" && ++storageReads == 17) CancelCapture!();
            if (args[2] == "list")
                return new(0, JsonSerializer.Serialize(Blobs.Keys.Where(key => key.Container == "ledger").Select(key => new { name = key.Name })), "");
            string name = Value(args, "--name");
            if (!Blobs.TryGetValue((Value(args, "--container-name"), name), out byte[]? bytes)) return new(1, "", "BlobNotFound");
            if (args[2] == "show") return new(0, JsonSerializer.Serialize(new
            { etag = "\"original-etag\"", contentLength = failure == "oversize_artifact" && name.EndsWith("/ledger.jsonl", StringComparison.Ordinal) ? 268435457L : bytes.Length }), "");
            Assert.Equal("download", args[2]);
            Assert.Equal("\"original-etag\"", Value(args, "--if-match"));
            Assert.Equal("false", Value(args, "--overwrite"));
            bool partial = failure == "partial_transfer" && name.EndsWith("/ledger.jsonl", StringComparison.Ordinal);
            await File.WriteAllBytesAsync(Value(args, "--file"), partial ? bytes[..2] : bytes, cancellationToken);
            return new(partial ? 1 : 0, "", partial ? "original transfer interrupted" : "");
        }
        private static string Value(IReadOnlyList<string> args, string name) => args[args.ToList().IndexOf(name) + 1];
    }
}
#pragma warning restore CA1707
