using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Lifetime checks protect the evidence boundary where native generations and completion meet.
[Trait("Category", "Unit")]
public sealed class KafkaStatisticsLifetimeTests
{
    private static readonly string[] ExpectedBroker = ["broker/1"];

    [Fact]
    public async Task Given_NativeCreationTimeChanges_When_FreshSampleFollows_Then_SessionStaysIncomplete()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics, TimeSpan.FromSeconds(1));

        clock.Advance(TimeSpan.FromSeconds(2));
        session.Capture(StatisticsTestSupport.Report("test-client", 11_000_000, 1_000_000));
        await sink.WaitForReportsAsync(1);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        session.Capture(StatisticsTestSupport.Report("test-client", 11_500_000, 1_400_000));
        await sink.WaitForReportsAsync(2);
        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_800_000));
        await sink.WaitForReportsAsync(3);
        clock.Advance(TimeSpan.FromSeconds(1));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "client_instance_changed");
        Assert.Contains(sink.Reports.ElementAt(1).Diagnostics, item => item.Code == "client_instance_changed");
        Assert.Contains(sink.Reports.ElementAt(2).Diagnostics, item => item.Code == "client_instance_changed");
    }

    [Fact]
    public async Task Given_ConsumerIdentityWithProducerReport_When_Processing_Then_TypeMismatchIsRetained()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        var identity = new KafkaStatisticsIdentity("A01", "consumer-type-run", KafkaClientRole.EnrichmentConsumer, "consumer-client");
        await using var session = NewSession(clock, sink, metrics, identity, TimeSpan.FromSeconds(1));

        clock.Advance(TimeSpan.FromSeconds(2));
        session.Capture(StatisticsTestSupport.Report("consumer-client", 11_000_000, 1_000_000, type: "producer"));
        await sink.WaitForReportsAsync(1);
        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        clock.Advance(TimeSpan.FromSeconds(1));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "client_type_mismatch");
        Assert.Contains(sink.Reports.Single().Diagnostics, item => item.Code == "client_type_mismatch");
    }

    [Fact]
    public async Task Given_NativeTimestampExceedsReceiptMicrosecond_When_ReportIsCaptured_Then_FutureSampleIsRejected()
    {
        var clock = new NanosecondTimeProvider(10_000_000_000);
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);

        clock.SetTimestamp(12_000_001_999);
        session.Capture(StatisticsTestSupport.Report("test-client", 12_000_002, 2_000_002));
        await sink.WaitForReportsAsync(1);

        KafkaStatisticsReport report = Assert.Single(sink.Reports);
        Assert.Contains(report.Diagnostics, item => item.Code == "future_native_timestamp");
        Assert.NotNull(report.Snapshot);
    }

    [Fact]
    public async Task Given_PostDrainSampleHasSameNativeMicrosecond_When_DeadlineExpires_Then_FreshnessIsNotInferred()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics, TimeSpan.FromSeconds(1));

        clock.Advance(TimeSpan.FromSeconds(2));
        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        session.Capture(StatisticsTestSupport.Report("test-client", 12_000_000, 2_000_000));
        await sink.WaitForReportsAsync(1);
        clock.Advance(TimeSpan.FromSeconds(1));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "completion_timeout");
        Assert.Equal(ExpectedBroker, completion.MissingBrokers);
    }

    [Fact]
    public async Task Given_CompletionFallsBetweenNativeTicks_When_NextTickIsCaptured_Then_CeilingBoundaryRequiresLaterTick()
    {
        var clock = new NanosecondTimeProvider(10_000_000_000);
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);
        using var cancellation = new CancellationTokenSource();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation.Token, TestContext.Current.CancellationToken);

        clock.SetTimestamp(12_000_000_100);
        Task<KafkaStatisticsCompletion> completionTask = session.CompleteAsync(ExpectedBroker, linkedCancellation.Token);
        clock.SetTimestamp(12_000_001_000);
        session.Capture(StatisticsTestSupport.Report("test-client", 12_000_001, 2_000_001));
        await sink.WaitForReportsAsync(1);
        cancellation.Cancel();

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "completion_cancelled");
        Assert.Equal(ExpectedBroker, completion.MissingBrokers);
        Assert.DoesNotContain(sink.Reports.Single().Diagnostics, item => item.Code == "future_native_timestamp");
    }

    [Fact]
    public async Task Given_CompletionAndDisposeRace_When_DisposeRunsDuringCoverageWait_Then_ReturnsBoundedIncompleteResult()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        var session = NewSession(clock, sink, metrics, TimeSpan.FromSeconds(5));

        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        await session.DisposeAsync();
        KafkaStatisticsCompletion completion = await completionTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "disposed_during_completion");
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Given_SessionWasDisposedBeforeCompletion_When_Completing_Then_ReturnsIncompleteWithoutWaitingForCoverage()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        var session = NewSession(clock, sink, metrics, TimeSpan.FromSeconds(5));

        await session.DisposeAsync();
        KafkaStatisticsCompletion completion = await CompleteAsync(session).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "disposed_before_completion");
        Assert.Equal(ExpectedBroker, completion.MissingBrokers);
    }

    [Fact]
    public async Task Given_OversizedInputIsCaptured_When_ValidCoverageWouldOtherwiseBePossible_Then_InputLossStaysLatched()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics, TimeSpan.FromSeconds(5));

        session.Capture(new string('x', 16_385));
        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        clock.Advance(TimeSpan.FromSeconds(3));
        session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "oversized_snapshot");
        KafkaStatisticsReport report = Assert.Single(sink.Reports);
        Assert.Equal(12_900_000, report.Snapshot!.TimestampMicroseconds);
    }

    private static Task<KafkaStatisticsCompletion> CompleteAsync(KafkaStatisticsSession session) =>
        session.CompleteAsync(ExpectedBroker, TestContext.Current.CancellationToken);

    private static KafkaStatisticsSession NewSession(
        TimeProvider clock,
        RecordingStatisticsSink sink,
        KafkaStatisticsMetrics metrics,
        TimeSpan? timeout = null) => NewSession(
            clock,
            sink,
            metrics,
            StatisticsTestSupport.ProducerIdentity(),
            timeout);

    private static KafkaStatisticsSession NewSession(
        TimeProvider clock,
        RecordingStatisticsSink sink,
        KafkaStatisticsMetrics metrics,
        KafkaStatisticsIdentity identity,
        TimeSpan? timeout = null) => new(
            identity,
            StatisticsTestSupport.Options(completionTimeout: timeout),
            clock,
            sink,
            metrics,
            nativeClockCompatible: true);

    private sealed class NanosecondTimeProvider(long initialTimestamp) : TimeProvider
    {
        private long _timestamp = initialTimestamp;

        public override long TimestampFrequency => 1_000_000_000;

        public override long GetTimestamp() => _timestamp;

        internal void SetTimestamp(long timestamp) => _timestamp = timestamp;
    }
}
