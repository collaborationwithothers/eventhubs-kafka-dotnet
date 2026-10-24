using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Publication;
using Xunit;

namespace Parchwell.LoadGen.Tests.Publication;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class ProducerRunLifecycleTests
{
    private const string Endpoint = "https://lifecycle.blob.test/";
    private static readonly string[] RegistrationFieldNames = ["schema_version", "identity", "phase", "repeat_number", "job_name", "pod_name", "pod_uid", "source_commit"];
    private static readonly string[] IdentityFieldNames = ["scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id"];
    private static readonly string[] CompletionFieldNames = ["schema_version", "identity", "phase", "repeat_number", "job_name", "pod_name", "pod_uid", "source_commit", "mode", "handler_exit_code", "cleanup_completed"];

    [Fact]
    public async Task Given_ProducerLifecycle_When_RegisteringAndCompleting_Then_SdkUploadsExactCreateOnlyRecords()
    {
        using var handler = new RecordingHandler();
        var lifecycle = new BlobProducerRunLifecycle(CreateClient(handler), TimeProvider.System);
        ProducerRunContext context = Context();
        ProducerLifecycleOptions options = Options(NewDirectory());

        await lifecycle.RegisterAsync(context, options, TestContext.Current.CancellationToken);
        await lifecycle.WriteCompletionAsync(context, options, TestContext.Current.CancellationToken);

        CapturedRequest[] requests = handler.Requests;
        Assert.Equal(["registration.json", "completion.json"], requests.Select(request => request.Name));
        Assert.Equal("/ledger/run-1/C0/r3/producer/pod-one/registration.json", requests[0].Uri.AbsolutePath);
        Assert.Equal("/ledger/run-1/C0/r3/producer/pod-one/completion.json", requests[1].Uri.AbsolutePath);
        Assert.All(requests, request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("*", request.IfNoneMatch);
            Assert.Contains("/ledger/run-1/C0/r3/producer/pod-one/", request.Uri.AbsolutePath, StringComparison.Ordinal);
        });
        using JsonDocument registration = JsonDocument.Parse(requests[0].Body);
        Assert.Equal(RegistrationFieldNames,
            registration.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(IdentityFieldNames,
            registration.RootElement.GetProperty("identity").EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, registration.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal("C0", registration.RootElement.GetProperty("phase").GetString());
        Assert.Equal(3, registration.RootElement.GetProperty("repeat_number").GetInt32());
        Assert.Equal("run-1-c0-r3-loadgen", registration.RootElement.GetProperty("job_name").GetString());
        Assert.Equal("pod-one", registration.RootElement.GetProperty("pod_name").GetString());
        Assert.Equal("pod-uid-1", registration.RootElement.GetProperty("pod_uid").GetString());
        Assert.Equal(new string('a', 40), registration.RootElement.GetProperty("source_commit").GetString());
        Assert.Equal("run-1", registration.RootElement.GetProperty("identity").GetProperty("run_id").GetString());
        Assert.False(registration.RootElement.TryGetProperty("mode", out _));

        using JsonDocument completion = JsonDocument.Parse(requests[1].Body);
        Assert.Equal(CompletionFieldNames,
            completion.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("loadgen", completion.RootElement.GetProperty("mode").GetString());
        Assert.Equal(0, completion.RootElement.GetProperty("handler_exit_code").GetInt32());
        Assert.True(completion.RootElement.GetProperty("cleanup_completed").GetBoolean());
        Assert.Equal("run-1", completion.RootElement.GetProperty("identity").GetProperty("run_id").GetString());
        Assert.True(requests.All(request => request.Body.Length <= 65_536));
    }

    [Fact]
    public async Task Given_FailedRunArtifacts_When_PublishingDiagnostics_Then_UploadsOnlyKnownPayloadsUnderDiagnosticPrefix()
    {
        string directory = NewDirectory();
        foreach (string name in new[] { "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json" })
            await File.WriteAllTextAsync(Path.Combine(directory, name), name, TestContext.Current.CancellationToken);
        using var handler = new RecordingHandler();
        var lifecycle = new BlobProducerRunLifecycle(CreateClient(handler), TimeProvider.System);

        await lifecycle.PublishDiagnosticsAsync(Context(), result: null, Options(directory), TestContext.Current.CancellationToken);

        CapturedRequest[] requests = handler.Requests;
        Assert.Equal(["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"], requests.Select(request => request.Name));
        Assert.All(requests, request => Assert.Contains("/diagnostics/", request.Uri.AbsolutePath, StringComparison.Ordinal));
        Assert.DoesNotContain(requests, request => request.Uri.AbsolutePath.EndsWith("/index.json", StringComparison.Ordinal));
        Assert.DoesNotContain(requests, request => request.Uri.AbsolutePath.EndsWith("/completion.json", StringComparison.Ordinal));
        Assert.Equal("ledger.jsonl", Encoding.UTF8.GetString(requests[0].Body));
        Assert.Equal("manifest.json", Encoding.UTF8.GetString(requests[1].Body));
    }

    [Fact]
    public async Task Given_FirstDiagnosticUploadFails_When_PublishingDiagnostics_Then_AttemptsLaterBoundedSnapshots()
    {
        string directory = NewDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory, "ledger.jsonl"), "ledger", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), "manifest", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "statistics.jsonl"), "statistics", TestContext.Current.CancellationToken);
        using var handler = new RecordingHandler(failName: "ledger.jsonl", failureBody: "<Message>sig=secret-sas-token</Message>");
        var lifecycle = new BlobProducerRunLifecycle(CreateClient(handler), TimeProvider.System);

        BlobProducerRunLifecycle.DiagnosticPublicationException failure = await Assert.ThrowsAsync<BlobProducerRunLifecycle.DiagnosticPublicationException>(
            () => lifecycle.PublishDiagnosticsAsync(Context(), result: null, Options(directory), TestContext.Current.CancellationToken));

        Assert.Equal(["ledger.jsonl", "manifest.json", "statistics.jsonl"], handler.Requests.Select(request => request.Name));
        Assert.All(handler.Requests, request => Assert.Contains("/diagnostics/", request.Uri.AbsolutePath, StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Requests, request => request.Uri.AbsolutePath.Contains("publication/index.json", StringComparison.Ordinal));
        Assert.Contains("ledger.jsonl:upload_failed", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-sas-token", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_OversizedDiagnosticArtifact_When_PublishingDiagnostics_Then_SkipsItAndUploadsOtherKnownFiles()
    {
        string directory = NewDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory, "ledger.jsonl"), new string('x', 33), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), "small", TestContext.Current.CancellationToken);
        using var handler = new RecordingHandler();
        var lifecycle = new BlobProducerRunLifecycle(CreateClient(handler), TimeProvider.System);
        ProducerLifecycleOptions options = Options(directory) with { MaxArtifactBytes = 32, MaxAggregateArtifactBytes = 32 };

        BlobProducerRunLifecycle.DiagnosticPublicationException failure = await Assert.ThrowsAsync<BlobProducerRunLifecycle.DiagnosticPublicationException>(
            () => lifecycle.PublishDiagnosticsAsync(Context(), result: null, options, TestContext.Current.CancellationToken));

        Assert.Equal(["manifest.json"], handler.Requests.Select(request => request.Name));
        Assert.Equal("small", Encoding.UTF8.GetString(Assert.Single(handler.Requests).Body));
        Assert.Contains("ledger.jsonl:oversized", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_DiagnosticArtifactExceedsLifecycleRecordCeiling_When_Publishing_Then_UploadsWithinArtifactBudgets()
    {
        string directory = NewDirectory();
        string payload = new string('x', 65_537);
        await File.WriteAllTextAsync(Path.Combine(directory, "statistics.jsonl"), payload, TestContext.Current.CancellationToken);
        using var handler = new RecordingHandler();
        var lifecycle = new BlobProducerRunLifecycle(CreateClient(handler), TimeProvider.System);

        await Assert.ThrowsAsync<BlobProducerRunLifecycle.DiagnosticPublicationException>(() => lifecycle.PublishDiagnosticsAsync(Context(), result: null,
            Options(directory) with { MaxArtifactBytes = 70_000, MaxAggregateArtifactBytes = 70_000 },
            TestContext.Current.CancellationToken));

        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal("statistics.jsonl", request.Name);
        Assert.Equal(65_537, request.Body.Length);
    }

    [Fact]
    public async Task Given_CombinedDiagnosticSnapshotsExceedAggregateCap_When_Publishing_Then_UploadsOnlyTheBoundedPrefix()
    {
        string directory = NewDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory, "ledger.jsonl"), "ledger-1", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), "manifest", TestContext.Current.CancellationToken);
        using var handler = new RecordingHandler();
        var lifecycle = new BlobProducerRunLifecycle(CreateClient(handler), TimeProvider.System);
        ProducerLifecycleOptions options = Options(directory) with { MaxArtifactBytes = 32, MaxAggregateArtifactBytes = 8 };

        BlobProducerRunLifecycle.DiagnosticPublicationException failure = await Assert.ThrowsAsync<BlobProducerRunLifecycle.DiagnosticPublicationException>(
            () => lifecycle.PublishDiagnosticsAsync(Context(), result: null, options, TestContext.Current.CancellationToken));

        Assert.Equal(["ledger.jsonl"], handler.Requests.Select(request => request.Name));
        Assert.Equal(8, Assert.Single(handler.Requests).Body.Length);
        Assert.Contains("manifest.json:aggregate_limit", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_DiagnosticUploadDeadlineExpires_When_Publishing_Then_ReturnsWithoutUploadingPastDeadline()
    {
        string directory = NewDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory, "ledger.jsonl"), "ledger", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), "manifest", TestContext.Current.CancellationToken);
        var timeProvider = new ManualTimeProvider();
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHandler(blockName: "ledger.jsonl", blockStarted: requestStarted);
        var lifecycle = new BlobProducerRunLifecycle(CreateClient(handler), timeProvider);
        ProducerLifecycleOptions options = Options(directory) with { UploadTimeout = TimeSpan.FromMilliseconds(50) };

        Task publishing = lifecycle.PublishDiagnosticsAsync(Context(), result: null, options, TestContext.Current.CancellationToken);
        await requestStarted.Task;
        timeProvider.AdvanceBy(options.UploadTimeout);
        BlobProducerRunLifecycle.DiagnosticPublicationException failure = await Assert.ThrowsAsync<BlobProducerRunLifecycle.DiagnosticPublicationException>(() => publishing);

        Assert.Equal(["ledger.jsonl"], handler.Requests.Select(request => request.Name));
        Assert.All(handler.Requests, request => Assert.Contains("/diagnostics/", request.Uri.AbsolutePath, StringComparison.Ordinal));
        Assert.Contains("ledger.jsonl:upload_deadline", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_MissingDiagnosticCandidates_When_PublishingDiagnostics_Then_RecordsGapsWithoutUploadingAnIncompleteSetAsSuccess()
    {
        string directory = NewDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), "manifest", TestContext.Current.CancellationToken);
        using var handler = new RecordingHandler();
        var lifecycle = new BlobProducerRunLifecycle(CreateClient(handler), TimeProvider.System);

        BlobProducerRunLifecycle.DiagnosticPublicationException failure = await Assert.ThrowsAsync<BlobProducerRunLifecycle.DiagnosticPublicationException>(
            () => lifecycle.PublishDiagnosticsAsync(Context(), result: null, Options(directory), TestContext.Current.CancellationToken));

        Assert.Equal(["manifest.json"], handler.Requests.Select(request => request.Name));
        Assert.Contains("ledger.jsonl:missing", failure.Message, StringComparison.Ordinal);
        Assert.Contains("statistics.jsonl:missing", failure.Message, StringComparison.Ordinal);
        Assert.Contains("request-lifecycle.jsonl:missing", failure.Message, StringComparison.Ordinal);
        Assert.Contains("capabilities.json:missing", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("wrong-job")]
    [InlineData("run-2-C0-r3-loadgen")]
    public async Task Given_InvalidProducerContext_When_Registering_Then_DoesNotSendARequest(string jobName)
    {
        using var handler = new RecordingHandler();
        var lifecycle = new BlobProducerRunLifecycle(CreateClient(handler), TimeProvider.System);
        ProducerRunContext context = Context() with { JobName = jobName };

        await Assert.ThrowsAsync<InvalidDataException>(() => lifecycle.RegisterAsync(context, Options(NewDirectory()),
            TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Given_LifecycleRecordExceedsByteCeiling_When_Registering_Then_DoesNotUploadAPartialRecord()
    {
        using var handler = new RecordingHandler();
        var lifecycle = new BlobProducerRunLifecycle(CreateClient(handler), TimeProvider.System);
        ProducerRunContext context = Context() with { PodUid = new string('x', 65_537) };

        InvalidDataException failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
            lifecycle.RegisterAsync(context, Options(NewDirectory()), TestContext.Current.CancellationToken));

        Assert.Equal("producer_lifecycle_record_too_large", failure.Message);
        Assert.Empty(handler.Requests);
    }

    private static BlobServiceClient CreateClient(HttpMessageHandler handler)
    {
        var clientOptions = new BlobClientOptions
        {
            Transport = new HttpClientTransport(new HttpClient(handler, disposeHandler: false)),
            Retry = { MaxRetries = 0 }
        };
        return new BlobServiceClient(new Uri(Endpoint), new FixedTokenCredential(), clientOptions);
    }

    private static ProducerRunContext Context() => new(
        new RunIdentity("A01", "C0", "run-1", "pod-one", "pod-one", "pod-one"),
        "C0", 3, "run-1-c0-r3-loadgen", "pod-one", "pod-uid-1", new string('a', 40));

    private static ProducerLifecycleOptions Options(string outputRoot) => new(
        Endpoint, "ledger", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), 65_536, 131_072, outputRoot);

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "loadgen-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FixedTokenCredential : TokenCredential
    {
        private static readonly AccessToken Token = new("lifecycle-test-token", DateTimeOffset.Parse("2037-01-01T00:00:00Z", CultureInfo.InvariantCulture));
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Token;
        }
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Token);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;
        private readonly List<ManualTimer> _timers = [];
        private readonly object _gate = new();

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, GetTimestamp);
            if (dueTime != Timeout.InfiniteTimeSpan) timer.Schedule(GetTimestamp() + dueTime.Ticks, period);
            lock (_gate) _timers.Add(timer);
            return timer;
        }

        public void AdvanceBy(TimeSpan elapsed)
        {
            long timestamp = Interlocked.Add(ref _timestamp, elapsed.Ticks);
            ManualTimer[] timers;
            lock (_gate) timers = [.. _timers];
            foreach (ManualTimer timer in timers) timer.FireIfDue(timestamp);
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, Func<long> getTimestamp) : ITimer
    {
        private int _disposed;
        private int _fired;
        private long _dueTimestamp = long.MaxValue;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (Volatile.Read(ref _disposed) != 0) return false;
            Schedule(dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : checked(getTimestamp() + dueTime.Ticks), period);
            return true;
        }
        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        public void Schedule(long dueTimestamp, TimeSpan period) => _dueTimestamp = dueTimestamp;
        public void FireIfDue(long timestamp)
        {
            if (timestamp >= Interlocked.Read(ref _dueTimestamp) && Volatile.Read(ref _disposed) == 0 &&
                Interlocked.Exchange(ref _fired, 1) == 0)
            {
                Interlocked.Exchange(ref _dueTimestamp, long.MaxValue);
                callback(state);
            }
        }
    }

    private sealed class RecordingHandler(string? failName = null, string? blockName = null,
        TaskCompletionSource? blockStarted = null, string? failureBody = null) : HttpMessageHandler
    {
        private readonly List<CapturedRequest> requests = [];
        public CapturedRequest[] Requests => requests.ToArray();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[] body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            string? condition = request.Headers.TryGetValues("If-None-Match", out IEnumerable<string>? values) ? values.Single() : null;
            requests.Add(new CapturedRequest(request.Method, request.RequestUri!, Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.Split('/').Last()), body, condition));
            if (blockName == requests[^1].Name)
            {
                blockStarted?.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            if (failName == requests[^1].Name)
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(failureBody ?? "<Error><Code>InternalError</Code></Error>", Encoding.UTF8, "application/xml")
                };
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"lifecycle-etag\"");
            response.Headers.TryAddWithoutValidation("Last-Modified", "Tue, 01 Jan 2030 00:00:00 GMT");
            response.Headers.TryAddWithoutValidation("x-ms-request-id", "lifecycle-test");
            return response;
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Name, byte[] Body, string? IfNoneMatch);
}
#pragma warning restore CA1707
