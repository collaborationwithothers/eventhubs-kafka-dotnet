using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.LoadGen.Run.Execution;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
public sealed class B10AbruptStatisticsContentionTests
{
    private static readonly KafkaStatisticsIdentity Identity = new("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");

    [Fact]
    public void Given_GateHeldByAnotherThread_When_NativeReportCaptured_Then_CaptureReturnsAndReportStillMatches()
    {
        B10AbruptStatistics stats = Tracked();
        KafkaStatisticsReport report = Report(1000);
        bool returned = CaptureWhileHeld(stats, [report]);

        Assert.True(returned);
        stats.AssertAvailableGuards();
        stats.Observe(report);
        Assert.Equal(1, stats.SelectPreAcceptance(DateTimeOffset.MaxValue)!.Sequence);
    }

    [Fact]
    public void Given_ReportsCapturedWhileGateHeld_When_ObservedInCaptureOrder_Then_AllMatchInOrder()
    {
        B10AbruptStatistics stats = Tracked();
        KafkaStatisticsReport[] reports = [Report(1000), Report(2000), Report(3000)];
        bool returned = CaptureWhileHeld(stats, reports);

        Assert.True(returned);
        foreach (KafkaStatisticsReport report in reports) stats.Observe(report);
        stats.AssertAvailableGuards();
        Assert.Equal(3, stats.SelectPreAcceptance(DateTimeOffset.MaxValue)!.Sequence);
        string[] lines = System.Text.Encoding.UTF8.GetString(stats.CapturedBytes()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        for (int index = 0; index < lines.Length; index++)
            Assert.Equal(index + 1, JsonDocument.Parse(lines[index]).RootElement.GetProperty("report_sequence").GetInt64());
    }

    [Fact]
    public void Given_ReportsCapturedWhileGateHeld_When_ObservedOutOfCaptureOrder_Then_GuardReasonSix()
    {
        B10AbruptStatistics stats = Tracked();
        KafkaStatisticsReport second = Report(2000);
        bool returned = CaptureWhileHeld(stats, [Report(1000), second]);

        Assert.True(returned);
        stats.Observe(second);
        Assert.Equal(6, GuardReasonOf(() => stats.AssertAvailableGuards()));
    }

    [Theory]
    [InlineData(2048, false)]
    [InlineData(2049, true)]
    public void Given_PendingCapturesWhileGateHeld_When_BoundReached_Then_OnlyAboveBoundFails(int captures, bool fails)
    {
        B10AbruptStatistics stats = Tracked();
        KafkaStatisticsReport report = Report(1000);
        bool returned = CaptureWhileHeld(stats, Enumerable.Repeat(report, captures).ToArray());

        Assert.True(returned);
        if (fails) Assert.Equal(4, GuardReasonOf(() => stats.AssertAvailableGuards()));
        else stats.AssertAvailableGuards();
    }

    [Fact]
    public void Given_TrackingNotActivated_When_NativeReportCaptured_Then_GuardReasonFour()
    {
        var stats = new B10AbruptStatistics(Identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.CaptureNative(Report(1000).RawJson);
        Assert.Equal(4, GuardReasonOf(() => stats.AssertAvailableGuards()));
    }

    [Fact]
    public void Given_MoreReportsThanBoundCapturedAndObservedInTurn_When_Checked_Then_NoGuardFailure()
    {
        B10AbruptStatistics stats = Tracked();
        for (int index = 1; index <= 2049; index++)
        {
            KafkaStatisticsReport report = Report(index * 1000L);
            stats.CaptureNative(report.RawJson);
            stats.Observe(report);
        }
        stats.AssertAvailableGuards();
        Assert.Equal(2049, stats.ReportCount);
    }

    [Fact]
    public void Given_TrackedAdapterWithNoPendingReport_When_Observed_Then_GuardReasonSix()
    {
        B10AbruptStatistics stats = Tracked();
        stats.Observe(Report(1000));
        Assert.Equal(6, GuardReasonOf(() => stats.AssertAvailableGuards()));
    }

    [Fact]
    public async Task Given_RealNativeReportsAndContinuousPostDrainScan_When_ReportsArrive_Then_NoGuardFailureAndEveryReportMatched()
    {
        const string bootstrap = "127.0.0.1:1/bootstrap";
        CancellationToken token = TestContext.Current.CancellationToken;
        var stats = new B10AbruptStatistics(Identity, [bootstrap], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.ActivateNativeCaptureTracking();
        stats.MarkConstructed(DateTimeOffset.MinValue);
        var router = new RunEvidenceSinkRouter();
        router.Attach(stats);
        using var metrics = new KafkaStatisticsMetrics();
        var options = new KafkaStatisticsOptions
        {
            MaxSnapshotCharacters = 1048576, QueueCapacity = 2048, MaxBrokerSeries = 64, CompletionTimeout = TimeSpan.FromSeconds(1)
        };
        KafkaStatisticsSession session = new KafkaStatisticsBridgeFactory(options, TimeProvider.System, router, metrics,
            nativeClockCompatible: true).Create(Identity);
        await using var sessionOwner = session;
        var config = new ProducerConfig { BootstrapServers = "127.0.0.1:1", ClientId = "client", StatisticsIntervalMs = 20 };
        IProducer<byte[], byte[]> producer = new ProducerBuilder<byte[], byte[]>(config)
            .SetStatisticsHandler(B10AbruptHost.StatisticsHandler(stats, session))
            .SetLogHandler((_, _) => { }).SetErrorHandler((_, _) => { }).Build();
        stats.MarkDrainBoundary(DateTimeOffset.MaxValue, System.Diagnostics.Stopwatch.GetTimestamp(), System.Diagnostics.Stopwatch.Frequency);

        long scans = 0;
        Exception? scannerFailure = null;
        using var stop = new CancellationTokenSource();
        var scanner = new Thread(() =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    Assert.Null(stats.SelectPostDrain(DateTimeOffset.MaxValue));
                    _ = stats.PostDrainFacts(DateTimeOffset.MaxValue);
                    Interlocked.Increment(ref scans);
                }
            }
            catch (Exception exception) { scannerFailure = exception; }
        }) { IsBackground = true };
        scanner.Start();
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && stats.ReportCount < 50 && Volatile.Read(ref scannerFailure) is null)
            await Task.Delay(10, token);
        producer.Dispose();
        stop.Cancel();
        scanner.Join();
        KafkaStatisticsCompletion completion = await session.CompleteAsync(token);

        Exception? failure = scannerFailure;
        if (failure is null)
        {
            try { stats.AssertAvailableGuards(); }
            catch (InvalidDataException exception) { failure = exception; }
        }

        Assert.True(Interlocked.Read(ref scans) > 0);
        string[] codes = completion.Diagnostics.Select(item => item.Code).ToArray();
        if (failure is null)
        {
            Assert.True(stats.ReportCount >= 50, $"report_count={stats.ReportCount}");
            return;
        }
        Assert.IsType<InvalidDataException>(failure);
        Assert.Equal("b10_abrupt_guard_failed", failure.Message);
        int reason = GuardReasonOf(failure);
        string detail = $"guard_reason={reason} diagnostics={string.Join(',', codes)}";
        // Accepted outcomes caused outside this change: the session dropped a pending report (A), librdkafka repeated a
        // broker key (B), or an unidentified invalid field (C). Guard reason 3 and every other outcome fail the test.
        Assert.True(
            reason == 6 && codes.Contains("capture_contention") ||
            reason == 7 && (codes.Contains("duplicate_property") || codes.Contains("invalid_or_missing_field")),
            detail);
    }

    private static B10AbruptStatistics Tracked()
    {
        var stats = new B10AbruptStatistics(Identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.ActivateNativeCaptureTracking();
        stats.MarkConstructed(DateTimeOffset.MinValue);
        return stats;
    }

    private static KafkaStatisticsReport Report(long ts)
    {
        string raw = JsonSerializer.Serialize(new
        {
            name = "native", client_id = Identity.ClientId, type = "producer", ts, time = 100, age = ts,
            brokers = new Dictionary<string, object>
            {
                ["broker"] = new { name = "broker", nodeid = 0, source = "learned", state = "UP", stateage = 0,
                    req_timeouts = 0, disconnects = 0, txretries = 0, txerrs = 0, rxerrs = 0,
                    throttle = new { cnt = 1, max = 0 }, rtt = new { cnt = 1, p99 = 0 } }
            }
        });
        return new(Identity, "session", DateTimeOffset.UtcNow, raw, new KafkaStatisticsSnapshot("native", Identity.ClientId,
            "producer", ts, 100, ts, [new KafkaBrokerStatistics("broker", 0, "UP", 0, 0, 0, 0, 0, 0,
                new KafkaWindowStatistics(1, 0), new KafkaWindowStatistics(1, 0))]), ImmutableArray<KafkaStatisticsDiagnostic>.Empty);
    }

    // Holds the adapter lock on one thread while a second thread captures every report; true when the capture thread
    // finished within 5 s although the lock was still held. The lock is released before returning.
    private static bool CaptureWhileHeld(B10AbruptStatistics stats, KafkaStatisticsReport[] reports)
    {
        object gate = typeof(B10AbruptStatistics).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(stats)!;
        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = new Thread(() =>
        {
            lock (gate)
            {
                held.Set();
                release.Wait();
            }
        }) { IsBackground = true };
        holder.Start();
        Assert.True(held.Wait(TimeSpan.FromSeconds(5)));
        var capture = new Thread(() =>
        {
            foreach (KafkaStatisticsReport report in reports) stats.CaptureNative(report.RawJson);
        }) { IsBackground = true };
        capture.Start();
        bool returned = capture.Join(TimeSpan.FromSeconds(5));
        release.Set();
        holder.Join();
        capture.Join();
        return returned;
    }

    private static int GuardReasonOf(Action action)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(action);
        Assert.Equal("b10_abrupt_guard_failed", exception.Message);
        return GuardReasonOf(exception);
    }

    private static int GuardReasonOf(Exception exception)
    {
        B10AbruptDiagnostics.Site site = B10AbruptDiagnostics.FindSites(exception).Outer!;
        return (int)site.Facts.Single(fact => fact.Key == B10AbruptFact.GuardReason).Value;
    }
}
