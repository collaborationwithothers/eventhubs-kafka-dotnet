using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
/// <summary>
/// Drives the B10 successor's completion order on the real evidence, adapter and statistics objects.
/// Only the native producer is a fake, except in the tests that build a real librdkafka client.
/// </summary>
[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptSuccessorCompletionTests(ITestOutputHelper output)
{
    private static readonly string[] Codes =
    [
        "clock_unverified", "late_capture", "capture_contention", "oversized_snapshot", "capture_queue_overflow",
        "disposed_before_completion", "completion_cancelled", "sink_drain_timeout", "completion_timeout",
        "disposed_during_completion", "abandoned_without_completion", "processing_failure", "metrics_recording_failure",
        "evidence_delivery_cancelled", "evidence_sink_failure", "client_id_mismatch", "client_type_mismatch",
        "client_name_changed", "non_increasing_timestamp", "client_instance_changed", "late_attachment",
        "future_native_timestamp", "broker_series_limit_exceeded", "counter_reset", "capture_loss",
        "worker_cancellation_failure", "invalid_json", "duplicate_property", "invalid_root", "invalid_or_missing_field",
        "broker_name_mismatch"
    ];

    private static readonly string[] FactNames =
        ["completion_complete", "evidence_faulted", "missing_broker_count", "diagnostic_count", .. Codes.Select(code => "diagnostic_" + code)];

    private static readonly string[] HostBrokers = ["broker-a", "broker-b"];
    private static readonly string[] AcceptedCodes = ["duplicate_property", "invalid_or_missing_field"];

    private const string CompleteCompletion =
        """^\{"is_complete":true,"drain_boundary_microseconds":[0-9]+,"diagnostics":\[\],"missing_brokers":\[\]\}$""";

    [Fact]
    public async Task Given_LiveProducerAfterSeal_When_SuccessorCompletes_Then_StatisticsCompleteBeforeDisposalAndEvidenceAfter()
    {
        await using var chain = new Chain();
        chain.DeliverAtPoll = true;
        chain.DeliverAtBoundary = true;
        chain.DeliverAtDispose = true;

        B10AbruptProducer.SuccessorArtifacts artifacts = await chain.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, chain.Native.Disposals);
        Assert.Equal(1, chain.BoundaryRuns);
        Assert.Equal(0, chain.DisposalsAtBoundary);
        Assert.Equal(1, chain.DisposalsAtEvidenceCompletion);
        Assert.False(chain.Evidence.IsFaulted);
        Assert.Equal(5, artifacts.Paths.Count);
        Assert.All(artifacts.Paths.Values, path => Assert.True(File.Exists(path)));
        Assert.Equal(2, File.ReadAllLines(artifacts.Paths["statistics.jsonl"]).Count(line => line.Length > 0));
        Assert.Equal(0, new FileInfo(artifacts.Paths["request-lifecycle.jsonl"]).Length);
        JsonElement capabilities = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(artifacts.Paths["capabilities.json"], TestContext.Current.CancellationToken));
        Assert.Matches(CompleteCompletion, capabilities.GetProperty("statistics_completion").GetRawText());
        Assert.Equal(JsonValueKind.Null, capabilities.GetProperty("resolved_configuration").ValueKind);
    }

    [Fact]
    public async Task Given_NoPostBoundaryReport_When_SuccessorCompletes_Then_GuardFailsWithTimeoutFacts()
    {
        await using var chain = new Chain();
        chain.DeliverAtPoll = true;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => chain.RunAsync(TestContext.Current.CancellationToken));

        Assert.True(watch.ElapsedMilliseconds >= 2000, watch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("b10_abrupt_guard_failed", failure.Message);
        Assert.Equal(Expected(0, 0, 1, 1, "completion_timeout"), SiteFacts(failure, "B10AbruptHost.cs"));
        Assert.Equal(1, chain.Native.Disposals);
        Assert.False(File.Exists(Path.Combine(chain.Root, "successor", "statistics.jsonl")));
        string capabilities = await File.ReadAllTextAsync(Path.Combine(chain.Root, "capture", "capabilities.json"), TestContext.Current.CancellationToken);
        JsonElement document = JsonSerializer.Deserialize<JsonElement>(capabilities);
        Assert.Matches(
            """^\{"is_complete":false,"drain_boundary_microseconds":[0-9]+,"diagnostics":\[\{"code":"completion_timeout","broker":null,"field":null\}\],"missing_brokers":\["broker"\]\}$""",
            document.GetProperty("statistics_completion").GetRawText());
        Assert.False(document.GetProperty("is_faulted").GetBoolean());
    }

    [Theory]
    [InlineData(ErrorCode.NoError)]
    [InlineData(ErrorCode.Local_Fail)]
    public async Task Given_DisposalErrorCallback_When_CompletingSuccessor_Then_CapturesBeforeFinalizingAndAppliesGuard(ErrorCode code)
    {
        await using var chain = new Chain();
        chain.DeliverAtPoll = true;
        chain.DeliverAtBoundary = true;
        chain.Native.OnDispose = () => chain.Evidence.CaptureError(new Error(code, "synthetic disposal callback"));
        Task<B10AbruptProducer.SuccessorArtifacts> operation = chain.RunAsync(TestContext.Current.CancellationToken);

        if (code == ErrorCode.NoError)
        {
            B10AbruptProducer.SuccessorArtifacts artifacts = await operation;
            Assert.False(chain.Evidence.IsFaulted);
            Assert.All(artifacts.Paths.Values, path => Assert.True(File.Exists(path)));
            string captured = await File.ReadAllTextAsync(artifacts.Paths["request-lifecycle.jsonl"], TestContext.Current.CancellationToken);
            Assert.Contains("synthetic disposal callback", captured, StringComparison.Ordinal);
        }
        else
        {
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => operation);
            Assert.Equal("b10_abrupt_guard_failed", failure.Message);
            Assert.Contains("native_error_callback_observed", chain.Evidence.GuardObservations);
            JsonElement detail = JsonSerializer.SerializeToElement(B10AbruptDiagnostics.Describe(failure)).GetProperty("detail");
            Assert.Equal("B10AbruptProducer.cs", detail.GetProperty("file").GetString());
            Assert.Equal("CheckGuards", detail.GetProperty("member").GetString());
            Assert.Equal(1, detail.GetProperty("facts").GetProperty("native_error_observed").GetInt64());
            Assert.Equal(0, detail.GetProperty("facts").GetProperty("evidence_faulted").GetInt64());
            Assert.False(File.Exists(Path.Combine(chain.Root, "successor", "capabilities.json")));
            Assert.False(File.Exists(Path.Combine(chain.Root, "successor", "request-lifecycle.jsonl")));
        }

        Assert.Equal(1, chain.Native.Disposals);
        Assert.Equal(1, chain.DisposalsAtEvidenceCompletion);
        Assert.False(chain.Evidence.IsFaulted);
        Assert.DoesNotContain("error_capture_unavailable", chain.Evidence.Diagnostics);
        string retained = await File.ReadAllTextAsync(Path.Combine(chain.Root, "capture", "request-lifecycle.jsonl"), TestContext.Current.CancellationToken);
        Assert.Contains("synthetic disposal callback", retained, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_EvidenceFaultDuringDisposal_When_SuccessorCompletes_Then_GuardFailsWithEvidenceFaulted()
    {
        await using var chain = new Chain();
        chain.DeliverAtPoll = true;
        chain.DeliverAtBoundary = true;
        chain.Native.OnDispose = () => chain.Evidence.RecordDiagnostic(" ");

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => chain.RunAsync(TestContext.Current.CancellationToken));

        Assert.Equal("b10_abrupt_guard_failed", failure.Message);
        Assert.Equal(Expected(1, 1, 0, 0), SiteFacts(failure, "B10AbruptHost.cs"));
        JsonElement capabilities = JsonSerializer.Deserialize<JsonElement>(
            await File.ReadAllTextAsync(Path.Combine(chain.Root, "capture", "capabilities.json"), TestContext.Current.CancellationToken));
        Assert.True(capabilities.GetProperty("is_faulted").GetBoolean());
        Assert.True(capabilities.GetProperty("statistics_completion").GetProperty("is_complete").GetBoolean());
    }

    public static TheoryData<int> GuardFactCases => new(Enumerable.Range(0, 34));

    [Theory]
    [MemberData(nameof(GuardFactCases))]
    public void Given_StatisticsCompletion_When_GuardFactsAreBuilt_Then_EachFlagMapsOneCode(int index)
    {
        ImmutableArray<string> missing = ["a", "b"];
        KafkaStatisticsCompletion completion;
        bool faulted = false;
        long[] summary = [0, 0, 2, 1];
        string[] flagged = [];
        if (index == 0) { completion = new KafkaStatisticsCompletion(false, [], []); faulted = true; summary = [0, 1, 0, 0]; }
        else if (index == 1) completion = new KafkaStatisticsCompletion(false, [new("synthetic_unknown_code")], missing);
        else if (index == 2)
        {
            completion = new KafkaStatisticsCompletion(false, [.. Codes.Select(code => new KafkaStatisticsDiagnostic(code)), new("synthetic_unknown_code")], missing);
            summary = [0, 0, 2, 32];
            flagged = Codes;
        }
        else { completion = new KafkaStatisticsCompletion(false, [new(Codes[index - 3])], missing); flagged = [Codes[index - 3]]; }

        B10AbruptFactValue[] facts = B10AbruptHost.CompletionFacts(completion, faulted);

        Assert.Equal(35, facts.Length);
        Assert.Equal(FactNames, facts.Select(fact => SnakeCase(fact.Key.ToString())));
        Assert.Equal(summary, facts.Take(4).Select(fact => fact.Value));
        Assert.Equal(Codes.Select(code => Array.IndexOf(flagged, code) >= 0 ? 1L : 0L), facts.Skip(4).Select(fact => fact.Value));
    }

    [Fact]
    public async Task Given_StatisticsCompletionThrows_When_SuccessorCompletes_Then_ExceptionPropagatesAndProducerDisposedOnce()
    {
        await using var chain = new Chain();
        chain.DeliverAtPoll = true;
        chain.BoundaryFailure = new InvalidOperationException("synthetic_boundary_failure");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => chain.RunAsync(TestContext.Current.CancellationToken));

        Assert.Equal("synthetic_boundary_failure", failure.Message);
        Assert.Equal(1, chain.Native.Disposals);
        Assert.False(File.Exists(Path.Combine(chain.Root, "capture", "capabilities.json")));
        Assert.False(File.Exists(Path.Combine(chain.Root, "successor", "statistics.jsonl")));
    }

    [Fact]
    public async Task Given_RealNativeProducerWithoutBroker_When_CompletionMembersRun_Then_StatisticsCompleteWhileLiveAndEvidenceAfterDisposal()
    {
        await using var chain = new Chain(TimeProvider.System, TimeSpan.FromSeconds(20), ["127.0.0.1:1/bootstrap"], real: true);
        var config = new ProducerConfig
        {
            BootstrapServers = "127.0.0.1:1", ClientId = "client", StatisticsIntervalMs = 200, MessageTimeoutMs = 1000
        };
        IProducer<byte[], byte[]> native = new ProducerBuilder<byte[], byte[]>(config)
            .SetStatisticsHandler(B10AbruptHost.StatisticsHandler(chain.Adapter, chain.Session))
            .SetLogHandler((_, log) => chain.Evidence.CaptureLog(log))
            .SetErrorHandler((_, error) => chain.Evidence.CaptureError(error)).Build();
        try
        {
            await Task.Delay(500, TestContext.Current.CancellationToken);
            KafkaStatisticsCompletion completion = await chain.Completion.CompleteStatistics(TestContext.Current.CancellationToken);
            native.Dispose();
            native = null!;
            try
            {
                await chain.Completion.CompleteEvidence(completion, TestContext.Current.CancellationToken);
            }
            catch (InvalidDataException failure)
            {
                // Decision 6754970055: a duplicate broker key or an unexplained field failure from this fixture is accepted, not a pass.
                Assert.Equal("b10_abrupt_guard_failed", failure.Message);
                Assert.False(completion.IsComplete);
                string[] codes = completion.Diagnostics.Select(diagnostic => diagnostic.Code).Distinct(StringComparer.Ordinal).ToArray();
                Assert.NotEmpty(codes.Intersect(AcceptedCodes));
                Assert.Empty(codes.Except([.. AcceptedCodes, "completion_timeout"]));
                Assert.Equal(0, SiteFacts(failure, "B10AbruptHost.cs").Single(fact => fact.Name == "evidence_faulted").Value);
                output.WriteLine("accepted outcome, not a pass: " + string.Join(",", codes));
                return;
            }

            Assert.True(completion.IsComplete);
            Assert.False(chain.Evidence.IsFaulted);
            JsonElement capabilities = JsonSerializer.Deserialize<JsonElement>(
                await File.ReadAllTextAsync(Path.Combine(chain.Root, "capture", "capabilities.json"), TestContext.Current.CancellationToken));
            Assert.Matches(CompleteCompletion, capabilities.GetProperty("statistics_completion").GetRawText());
        }
        finally { native?.Dispose(); }
    }

    [Fact]
    public void Given_AllStatisticsCodes_When_GuardFactsAreAttached_Then_CapKeepsAllThirtyFive()
    {
        var completion = new KafkaStatisticsCompletion(false, [.. Codes.Select(code => new KafkaStatisticsDiagnostic(code))], ["a"]);
        B10AbruptFactValue[] facts = [.. B10AbruptHost.CompletionFacts(completion, false), B10AbruptDiagnostics.Fact(B10AbruptFact.Arm, 1)];

        var exception = B10AbruptDiagnostics.Attach(new InvalidDataException("b10_abrupt_guard_failed"), facts);

        Assert.Equal(FactNames, SiteFacts(exception, "B10AbruptSuccessorCompletionTests.cs").Select(fact => fact.Name));
    }

    [Fact]
    public async Task Given_HostBuiltSuccessor_When_StatisticsCompletionRunsWithoutReports_Then_MissingBrokersEqualHostInventory()
    {
        await using var host = await HostPath.CreateAsync(["broker-b", "broker-a"], TimeProvider.System);

        KafkaStatisticsCompletion completion = await host.Completion.CompleteStatistics(TestContext.Current.CancellationToken);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => host.Completion.CompleteEvidence(completion, TestContext.Current.CancellationToken));

        Assert.Equal("b10_abrupt_guard_failed", failure.Message);
        var facts = SiteFacts(failure, "B10AbruptHost.cs");
        Assert.Equal(2, facts.Single(fact => fact.Name == "missing_broker_count").Value);
        Assert.Equal(1, facts.Single(fact => fact.Name == "diagnostic_completion_timeout").Value);
        JsonElement capabilities = JsonSerializer.Deserialize<JsonElement>(
            await File.ReadAllTextAsync(Path.Combine(host.Root, "capture", "capabilities.json"), TestContext.Current.CancellationToken));
        JsonElement statistics = capabilities.GetProperty("statistics_completion");
        Assert.Equal(["broker-a", "broker-b"], statistics.GetProperty("missing_brokers").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(JsonValueKind.Null, statistics.GetProperty("first_post_drain_timestamp_microseconds").ValueKind);
    }

    [Fact]
    public async Task Given_HostBuiltSuccessorClient_When_SessionCaptureFails_Then_NativeCaptureWasTrackedFirst()
    {
        var clock = new FailingClock();
        await using var host = await HostPath.CreateAsync(["broker-a", "broker-b"], clock);
        IProducer<byte[], byte[]> native = host.CreateNativeClient();
        try
        {
            Action<string> handler = HostPath.StatisticsHandlerOf(native);
            clock.Armed = true;
            var failure = Assert.Throws<InvalidOperationException>(() => handler(Json(10_000_000, 9_000_000)));
            clock.Armed = false;
            Assert.Equal("synthetic_clock_failure", failure.Message);

            handler(Json(20_000_000, 9_000_000));

            B10AbruptStatistics adapter = host.Adapter;
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (adapter.ReportCount < 1 && DateTime.UtcNow < deadline) await Task.Delay(10, TestContext.Current.CancellationToken);
            Assert.Equal(1, adapter.ReportCount);
            var guard = Assert.Throws<InvalidDataException>(() => adapter.SelectPostDrain(DateTimeOffset.UtcNow));
            JsonElement detail = JsonSerializer.SerializeToElement(B10AbruptDiagnostics.Describe(guard)).GetProperty("detail");
            Assert.Equal(6, detail.GetProperty("facts").GetProperty("guard_reason").GetInt64());
        }
        finally { native.Dispose(); }

        static string Json(long timestamp, long creation) => JsonSerializer.Serialize(new
        {
            name = "native", client_id = "pod-2", type = "producer", ts = timestamp, time = 100, age = timestamp - creation,
            brokers = HostBrokers.ToDictionary(name => name, name => (object)new
            {
                name, nodeid = 0, source = "learned", state = "UP", stateage = 0, req_timeouts = 0, disconnects = 0,
                txretries = 0, txerrs = 0, rxerrs = 0, throttle = new { cnt = 1, max = 0 }, rtt = new { cnt = 1, p99 = 0 }
            })
        });
    }

    private static string SnakeCase(string name) => JsonNamingPolicy.SnakeCaseLower.ConvertName(name);

    private static (string Name, long Value)[] Expected(long complete, long faulted, long missing, long count, params string[] flagged) =>
        [("completion_complete", complete), ("evidence_faulted", faulted), ("missing_broker_count", missing), ("diagnostic_count", count),
            .. Codes.Select(code => ("diagnostic_" + code, flagged.Contains(code) ? 1L : 0L))];

    private static (string Name, long Value)[] SiteFacts(Exception exception, string file)
    {
        JsonElement detail = JsonSerializer.SerializeToElement(B10AbruptDiagnostics.Describe(exception)).GetProperty("detail");
        Assert.Equal(file, detail.GetProperty("file").GetString());
        return [.. detail.GetProperty("facts").EnumerateObject().Select(property => (property.Name, property.Value.GetInt64()))];
    }

    /// <summary>A clock that passes everything through, delivers one statistics report when the post-drain poll first creates its 10 ms timer.</summary>
    internal sealed class HookClock(TimeProvider inner) : TimeProvider
    {
        private int fired;
        internal Action? OnFirstPollTimer { get; set; }
        public override long TimestampFrequency => inner.TimestampFrequency;
        public override long GetTimestamp() => inner.GetTimestamp();
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == TimeSpan.FromMilliseconds(10) && Interlocked.Exchange(ref fired, 1) == 0) OnFirstPollTimer?.Invoke();
            return inner.CreateTimer(callback, state, dueTime, period);
        }
    }

    private sealed class FailingClock : TimeProvider
    {
        internal volatile bool Armed;
        public override long GetTimestamp() => Armed ? throw new InvalidOperationException("synthetic_clock_failure") : TimeProvider.System.GetTimestamp();
    }

    /// <summary>
    /// The real evidence session, B10 statistics adapter, sink router and statistics session, with the production completion behaviour.
    /// Delivery points: P1 at the first 10 ms timer, P2 from the completion-boundary observer while the fake client is live, P3 at disposal.
    /// </summary>
    internal sealed class Chain : IAsyncDisposable
    {
        private readonly bool real;
        private readonly bool ownsRoot;
        private readonly long creation;
        private int next;
        internal string Root { get; }
        internal HookClock Clock { get; }
        internal RunEvidenceSession Evidence { get; }
        internal B10AbruptStatistics Adapter { get; }
        internal KafkaStatisticsSession Session { get; }
        internal B10AbruptProducerTests.NativeProducer Native { get; }
        internal B10AbruptProducer.SuccessorCompletion Completion { get; }
        internal B10AbruptProducer Producer { get; }
        internal RunExecutionOptions Execution { get; }
        internal RunIdentity Identity { get; }
        internal bool DeliverAtPoll { get; set; }
        internal bool DeliverAtBoundary { get; set; }
        internal bool DeliverAtDispose { get; set; }
        internal Exception? BoundaryFailure { get; set; }
        internal int BoundaryRuns { get; private set; }
        internal int DisposalsAtBoundary { get; private set; } = -1;
        internal int DisposalsAtEvidenceCompletion { get; private set; } = -1;

        internal Chain(TimeProvider? inner = null, TimeSpan? completionTimeout = null, string[]? brokers = null, bool real = false,
            RunIdentity? identity = null, string? root = null, B10AbruptWire.Identity? wireIdentity = null)
        {
            this.real = real;
            Identity = identity ?? new("B10", "C1", "run-1", "pod-1", "pod-1", "client");
            ownsRoot = root is null;
            Root = root ?? Path.Combine(Path.GetTempPath(), "abrupt-successor-" + Guid.NewGuid().ToString("N"));
            brokers ??= ["broker"];
            Clock = new HookClock(inner ?? TimeProvider.System);
            Directory.CreateDirectory(Root);
            string capture = Path.Combine(Root, "capture");
            Directory.CreateDirectory(capture);
            Evidence = new RunEvidenceSession(Identity, new RunEvidenceOptions(capture, 2048, 1048576, 134217728, 100, TimeSpan.FromSeconds(20)), Clock);
            var statisticsIdentity = new KafkaStatisticsIdentity("B10", Identity.RunId, KafkaClientRole.LoadGenProducer, Identity.ClientId);
            Adapter = new B10AbruptStatistics(statisticsIdentity, brokers, new Dictionary<string, string>(), new Dictionary<string, string>(), Evidence);
            Adapter.ActivateNativeCaptureTracking();
            var router = new RunEvidenceSinkRouter();
            router.Attach(Adapter);
            var options = new KafkaStatisticsOptions
            {
                MaxSnapshotCharacters = 1048576, QueueCapacity = 2048, MaxBrokerSeries = 64, CompletionTimeout = completionTimeout ?? TimeSpan.FromSeconds(2)
            };
            Session = new KafkaStatisticsBridgeFactory(options, Clock, router, new KafkaStatisticsMetrics(), nativeClockCompatible: true, completionBoundaryObserver: OnBoundary)
                .Create(statisticsIdentity);
            Completion = B10AbruptHost.SuccessorCompletion(Session, Evidence, brokers, capture);
            B10AbruptProducer.SuccessorCompletion recording = Completion with
            {
                CompleteEvidence = (completion, token) =>
                {
                    DisposalsAtEvidenceCompletion = Native?.Disposals ?? -1;
                    return Completion.CompleteEvidence(completion, token);
                }
            };
            Native = (B10AbruptProducerTests.NativeProducer)(object)DispatchProxy.Create<IProducer<byte[], byte[]>, B10AbruptProducerTests.NativeProducer>();
            Native.OnProduce = (_, callback) => callback(B10AbruptProducerTests.Report());
            Native.OnDispose = () => { if (DeliverAtDispose) Deliver(); };
            Thread.Sleep(2);
            creation = Micro(Clock.GetTimestamp());
            Execution = new RunExecutionOptions(8, 256, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 16384,
                1048576, 1048576, Root, "document-events", brokers, false, ApplicationRetry: ApplicationRetryPolicy.ForScenario("B10"));
            wireIdentity ??= new B10AbruptWire.Identity("run-1", "B10", "C1", "C1", 1, new string('a', 40),
                "image@sha256:" + new string('b', 64), "run-1-loadgen", "pod-1", "uid-1", "pod-1", null);
            Producer = new B10AbruptProducer(() => (IProducer<byte[], byte[]>)(object)Native, Clock, Adapter,
                cohort =>
                {
                    Directory.CreateDirectory(Path.Combine(Root, cohort));
                    return new LocalLedgerStore(new LedgerStoreOptions(Path.Combine(Root, cohort, "ledger.jsonl"),
                        Path.Combine(Root, cohort, "manifest.json"), 16, 16384, 1048576, 1048576, TimeSpan.FromSeconds(1)));
                },
                (_, _, _) => Task.FromResult((long)Interlocked.Increment(ref next)), _ => true,
                (_, _) => Task.FromResult(new B10AbruptProducer.ReleaseInfo(DateTimeOffset.MinValue, null)),
                (_, _, _) => Task.CompletedTask, Evidence, wireIdentity, recording);
            Clock.OnFirstPollTimer = () => { if (DeliverAtPoll) Deliver(); };
        }

        internal Task<B10AbruptProducer.SuccessorArtifacts> RunAsync(CancellationToken cancellationToken) =>
            RunAsync(Selection(), Clock.GetUtcNow().AddMilliseconds(200), Execution, cancellationToken);

        internal Task<B10AbruptProducer.SuccessorArtifacts> RunAsync(JsonElement selection, DateTimeOffset release,
            RunExecutionOptions execution, CancellationToken cancellationToken) =>
            Producer.RunSuccessorAsync(B10AbruptProducerTests.Profile(), Identity, execution, selection, release, cancellationToken);

        private static JsonElement Selection()
        {
            byte[] payload = [1, 2, 3];
            return JsonSerializer.SerializeToElement(new[] { new
            {
                attempt_id = "predecessor-cross_restart-0", key = "B10/run-1/C1/1/cross_restart", sequence = 0,
                payload_length = payload.Length, payload_sha256 = Convert.ToHexStringLower(SHA256.HashData(payload)),
                payload_base64 = Convert.ToBase64String(payload)
            } });
        }

        internal void Deliver()
        {
            Thread.Sleep(1); // Keep the native timestamp strictly above a boundary taken a moment earlier, at microsecond resolution.
            long timestamp = Micro(Clock.GetTimestamp());
            string json = ReportJson(timestamp, creation, Identity.ClientId);
            B10AbruptHost.StatisticsHandler(Adapter, Session)(null!, json);
        }

        internal static string ReportJson(long timestamp, long creation, string clientId = "client") => JsonSerializer.Serialize(new
        {
            name = "native", client_id = clientId, type = "producer", ts = timestamp, time = 100, age = timestamp - creation,
            brokers = new
            {
                broker = new
                {
                    name = "broker", nodeid = 0, source = "learned", state = "UP", stateage = 0, req_timeouts = 0, disconnects = 0,
                    txretries = 0, txerrs = 0, rxerrs = 0, throttle = new { cnt = 1, max = 0 }, rtt = new { cnt = 1, p99 = 0 }
                }
            }
        });

        private long Micro(long timestamp) => (long)((Int128)timestamp * 1_000_000 / Clock.TimestampFrequency);

        private void OnBoundary()
        {
            BoundaryRuns++;
            DisposalsAtBoundary = real ? 0 : Native.Disposals;
            if (BoundaryFailure is not null) throw BoundaryFailure;
            if (DeliverAtBoundary && Native.Disposals == 0) Deliver();
        }

        public async ValueTask DisposeAsync()
        {
            await Session.DisposeAsync();
            await Evidence.DisposeAsync();
            if (ownsRoot && Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    /// <summary>Builds the producer through the host's own successor creation path, with a binding that lists the given brokers.</summary>
    private sealed class HostPath : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly object runtime;
        internal string Root { get; }
        internal B10AbruptProducer Producer { get; }
        internal B10AbruptProducer.SuccessorCompletion Completion { get; }
        internal B10AbruptStatistics Adapter { get; }

        private HostPath(string root, ServiceProvider services, object runtime)
        {
            Root = root;
            this.services = services;
            this.runtime = runtime;
            Producer = (B10AbruptProducer)runtime.GetType().GetProperty("Producer")!.GetValue(runtime)!;
            Completion = (B10AbruptProducer.SuccessorCompletion)typeof(B10AbruptProducer)
                .GetField("completion", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Producer)!;
            Adapter = (B10AbruptStatistics)typeof(B10AbruptProducer).GetField("statistics", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Producer)!;
        }

        internal static async Task<HostPath> CreateAsync(string[] brokers, TimeProvider clock)
        {
            string root = Path.Combine(Path.GetTempPath(), "abrupt-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string binding = Path.Combine(root, "predecessor.json");
            B10AbruptOptions options = B10AbruptWireProducerTests.TestOptions(root, "successor", binding, values =>
            {
                values["ScenarioSourceRoot"] = AppContext.BaseDirectory;
                values["NamespaceName"] = "synthetic-ns";
                values["PodName"] = "pod-2";
            });
            await File.WriteAllTextAsync(binding, JsonSerializer.Serialize(new
            {
                start_line_binding = new
                {
                    start_line = "synthetic", completion = "synthetic", broker_inventory = JsonSerializer.Serialize(new
                    {
                        schema_version = 1, identity = new { run_id = options.RunId, scenario_id = "B10", configuration_id = "C1" },
                        brokers = brokers.Select(name => new { name }).ToArray(), source = "kafka_metadata", source_commit = options.SourceCommit
                    })
                }
            }), TestContext.Current.CancellationToken);
            ServiceProvider provider = Services(options, clock);
            try
            {
                var creation = (Task)typeof(B10AbruptHost).GetMethod("CreateProducerAsync", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [provider, options, null, null, TestContext.Current.CancellationToken])!;
                await creation.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                return new HostPath(root, provider, creation.GetType().GetProperty("Result")!.GetValue(creation)!);
            }
            catch
            {
                await provider.DisposeAsync();
                throw;
            }
        }

        internal IProducer<byte[], byte[]> CreateNativeClient()
        {
            var factory = (Func<IProducer<byte[], byte[]>>)typeof(B10AbruptProducer).GetField("producerFactory", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Producer)!;
            return factory();
        }

        // The client stores a closure over the handler installed by the host's builder, and invokes it with the raw statistics string.
        internal static Action<string> StatisticsHandlerOf(IProducer<byte[], byte[]> native) =>
            (Action<string>)(native.GetType().GetField("statisticsHandler", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(native)
                ?? throw new InvalidOperationException("synthetic_statistics_handler_not_found"));

        public async ValueTask DisposeAsync()
        {
            await ((KafkaStatisticsSession)runtime.GetType().GetProperty("Statistics")!.GetValue(runtime)!).DisposeAsync();
            await ((RunEvidenceSession)runtime.GetType().GetProperty("Evidence")!.GetValue(runtime)!).DisposeAsync();
            await services.DisposeAsync();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }

        private static ServiceProvider Services(B10AbruptOptions options, TimeProvider clock)
        {
            var services = new ServiceCollection();
            services.AddSingleton(clock);
            typeof(B10AbruptHost).GetMethod("AddB10KafkaRoles", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [services, options]);
            services.AddParchwellScenarios(value =>
            { value.MaxDocumentCharacters = 1048576; value.MaxNestingDepth = 32; value.MaxScalarCharacters = 65536; value.MaxParserEvents = 100000; });
            services.AddSingleton<IScenarioSource>(new FileSystemScenarioSource(options.ScenarioSourceRoot));
            services.AddSingleton<RunEvidenceSinkRouter>();
            services.AddSingleton<IKafkaStatisticsEvidenceSink>(provider => provider.GetRequiredService<RunEvidenceSinkRouter>());
            services.AddSingleton<IKafkaTokenProvider>(new UnusedTokenProvider());
            services.AddParchwellKafkaAuthentication(value =>
            { value.TenantId = "synthetic-tenant"; value.ClientId = "synthetic-client"; value.TokenFilePath = Path.Combine(options.OutputRoot, "missing-token"); });
            services.AddParchwellKafkaStatistics(value =>
            { value.MaxSnapshotCharacters = 1048576; value.QueueCapacity = 2048; value.MaxBrokerSeries = 64; value.CompletionTimeout = TimeSpan.FromSeconds(1); });
            return services.BuildServiceProvider();
        }
    }

    private sealed class UnusedTokenProvider : IKafkaTokenProvider
    {
        public AccessToken GetToken(string scope, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("synthetic_token_provider_must_not_run");
    }
}
