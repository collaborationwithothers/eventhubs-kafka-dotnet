using System.Diagnostics.Metrics;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Protects the per-client evidence boundary because dropped, stale, or cross-client samples invalidate results.
[Trait("Category", "Unit")]
public sealed class KafkaStatisticsSessionTests
{
    private static readonly string[] ExpectedBroker = ["broker/1"];

    [Fact]
    public async Task Given_ValidPreAndPostDrainReports_When_Completing_Then_RetainsImmutableEvidenceAndFreshCoverage()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);

        clock.Advance(TimeSpan.FromSeconds(2));
        string firstJson = StatisticsTestSupport.Report("test-client", 11_000_000, 1_000_000, 4, 2);
        session.Capture(firstJson);
        await sink.WaitForReportsAsync(1);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        string secondJson = StatisticsTestSupport.Report("test-client", 11_500_000, 1_500_000, 7, 3);
        session.Capture(secondJson);
        await sink.WaitForReportsAsync(2);

        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        string postDrainJson = StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000, 7, 3);
        session.Capture(postDrainJson);
        KafkaStatisticsCompletion completion = await completionTask;

        Assert.True(completion.IsComplete);
        Assert.Empty(completion.Diagnostics);
        Assert.Empty(completion.MissingBrokers);
        KafkaStatisticsReport[] reports = sink.Reports.ToArray();
        Assert.Equal(3, reports.Length);
        Assert.All(reports, report => Assert.Equal("test-client", report.Identity.ClientId));
        Assert.Equal(firstJson, reports[0].RawJson);
        Assert.Equal(secondJson, reports[1].RawJson);
        Assert.Equal(postDrainJson, reports[2].RawJson);
        Assert.Equal(4, Assert.Single(reports[0].Snapshot!.Brokers).RequestTimeouts);
        Assert.Equal(7, Assert.Single(reports[1].Snapshot!.Brokers).RequestTimeouts);
        Assert.False(reports[0].Snapshot!.Brokers.IsDefault);
        Assert.False(reports[0].Diagnostics.IsDefault);
        Assert.NotEqual(reports[0].SessionId, string.Empty);
        Assert.All(reports, report => Assert.Equal(reports[0].SessionId, report.SessionId));
    }

    [Fact]
    public async Task Given_CumulativeCounterDecrease_When_LaterReportsRecover_Then_ResetRemainsPersistent()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);

        clock.Advance(TimeSpan.FromSeconds(2));
        session.Capture(StatisticsTestSupport.Report("test-client", 11_000_000, 1_000_000, 4));
        await sink.WaitForReportsAsync(1);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        session.Capture(StatisticsTestSupport.Report("test-client", 11_500_000, 1_500_000, 2));
        await sink.WaitForReportsAsync(2);
        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000, 6));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "counter_reset");
        KafkaStatisticsReport[] reports = sink.Reports.ToArray();
        Assert.Contains(reports[1].Diagnostics, item => item.Code == "counter_reset");
        Assert.Contains(reports[2].Diagnostics, item => item.Code == "counter_reset");
        Assert.Equal(3, reports.Length);
    }

    [Fact]
    public async Task Given_InvalidReportThenFreshCoverage_When_Processing_Then_ParseFailureDoesNotHeal()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);

        session.Capture("{ malformed");
        await sink.WaitForReportsAsync(1);
        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        clock.Advance(TimeSpan.FromSeconds(3));
        session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "invalid_json");
        Assert.Null(sink.Reports.First().Snapshot);
        Assert.Equal("{ malformed", sink.Reports.First().RawJson);
        Assert.Equal(2, sink.Reports.Count);
    }

    [Fact]
    public async Task Given_ReportFromAnotherClient_When_ValidCoverageFollows_Then_IdentityFailurePersists()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);

        clock.Advance(TimeSpan.FromSeconds(2));
        session.Capture(StatisticsTestSupport.Report("other-client", 11_000_000, 1_000_000));
        await sink.WaitForReportsAsync(1);
        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        clock.Advance(TimeSpan.FromSeconds(1));
        session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "client_id_mismatch");
        Assert.Contains(sink.Reports.First().Diagnostics, item => item.Code == "client_id_mismatch");
    }

    [Fact]
    public async Task Given_NativeTimestampRegresses_When_FreshCoverageFollows_Then_RegressionRemainsDiagnostic()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);

        clock.Advance(TimeSpan.FromSeconds(2));
        session.Capture(StatisticsTestSupport.Report("test-client", 11_000_000, 1_000_000));
        await sink.WaitForReportsAsync(1);
        session.Capture(StatisticsTestSupport.Report("test-client", 10_900_000, 900_000));
        await sink.WaitForReportsAsync(2);
        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        clock.Advance(TimeSpan.FromSeconds(1));
        session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "non_increasing_timestamp");
        Assert.Contains(sink.Reports.ElementAt(1).Diagnostics, item => item.Code == "non_increasing_timestamp");
    }

    [Fact]
    public async Task Given_BrokerSeriesLimitReachedAcrossReports_When_NewBrokerAppears_Then_SessionStaysIncomplete()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics, maxBrokerSeries: 1);

        clock.Advance(TimeSpan.FromSeconds(2));
        session.Capture(StatisticsTestSupport.Report("test-client", 11_000_000, 1_000_000, broker: "broker/1"));
        await sink.WaitForReportsAsync(1);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        session.Capture(StatisticsTestSupport.Report("test-client", 11_500_000, 1_500_000, broker: "broker/2"));
        await sink.WaitForReportsAsync(2);
        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000, broker: "broker/1"));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "broker_series_limit_exceeded");
        Assert.Contains(sink.Reports.ElementAt(1).Diagnostics, item => item.Code == "broker_series_limit_exceeded");
    }

    [Fact]
    public async Task Given_CallbackArrivesAfterDrainWithOldNativeTimestamp_When_ClockAdvances_Then_ReceiptDoesNotProveFreshness()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);

        clock.Advance(TimeSpan.FromSeconds(2));
        session.Capture(StatisticsTestSupport.Report("test-client", 11_000_000, 1_000_000));
        await sink.WaitForReportsAsync(1);
        using var cancellation = new CancellationTokenSource();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation.Token, TestContext.Current.CancellationToken);
        Task<KafkaStatisticsCompletion> completionTask = session.CompleteAsync(ExpectedBroker, linkedCancellation.Token);
        clock.Advance(TimeSpan.FromSeconds(1));
        session.Capture(StatisticsTestSupport.Report("test-client", 11_500_000, 1_500_000));
        await sink.WaitForReportsAsync(2);
        cancellation.Cancel();

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "completion_cancelled");
        Assert.Equal(ExpectedBroker, completion.MissingBrokers);
        Assert.True(sink.Reports.Last().ReceivedAt > sink.Reports.First().ReceivedAt);
    }

    [Fact]
    public async Task Given_ClockCompatibilityIsUnverified_When_ReportsAreCaptured_Then_CompletionCannotClaimCoverage()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics, nativeClockCompatible: false);

        session.Capture(StatisticsTestSupport.Report("test-client", 11_000_000, 1_000_000));
        KafkaStatisticsCompletion completion = await CompleteAsync(session);

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "clock_unverified");
        Assert.Contains("broker/1", completion.MissingBrokers);
        await sink.WaitForReportsAsync(1);
    }

    [Fact]
    public async Task Given_UnexpectedBrokerArrivesAfterDrain_When_ExpectedBrokerIsAbsent_Then_DoesNotClaimCoverage()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);
        using var cancellation = new CancellationTokenSource();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation.Token, TestContext.Current.CancellationToken);

        Task<KafkaStatisticsCompletion> completionTask = session.CompleteAsync(ExpectedBroker, linkedCancellation.Token);
        clock.Advance(TimeSpan.FromSeconds(3));
        session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000, broker: "unexpected/9"));
        await sink.WaitForReportsAsync(1);
        cancellation.Cancel();

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Equal(ExpectedBroker, completion.MissingBrokers);
    }

    [Fact]
    public async Task Given_MetricListenerBlocksWorker_When_CapturingAnotherReport_Then_CallbackCaptureReturnsWithoutWaiting()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        using var listener = new MeterListener();
        var metricEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMetric = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (instrument.Meter.Name == "Parchwell.Kafka.Statistics")
            {
                currentListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name == "kafka.broker.throttle.max_ms" && IsCurrentSession(tags))
            {
                metricEntered.TrySetResult();
                releaseMetric.Task.GetAwaiter().GetResult();
            }
        });
        listener.Start();
        await using var session = NewSession(clock, sink, metrics);

        clock.Advance(TimeSpan.FromSeconds(2));
        string withWindow = StatisticsTestSupport.Report("test-client", 11_000_000, 1_000_000)
            .Replace("\"cnt\": 0, \"max\": 0", "\"cnt\": 1, \"max\": 17", StringComparison.Ordinal);
        session.Capture(withWindow);
        await metricEntered.Task;
        var captureStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task captureTask = Task.Run(() =>
        {
            captureStarted.TrySetResult();
            session.Capture(StatisticsTestSupport.Report("test-client", 11_500_000, 1_500_000));
        }, TestContext.Current.CancellationToken);
        try
        {
            await captureStarted.Task;
            await captureTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseMetric.TrySetResult();
        }

        await sink.WaitForReportsAsync(2);
    }

    [Fact]
    public async Task Given_EvidenceSinkThrows_When_FreshCoverageIsReceived_Then_FailurePersists()
    {
        var clock = new ManualTimeProvider();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, new FailingStatisticsSink(), metrics);

        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        clock.Advance(TimeSpan.FromSeconds(3));
        session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "evidence_sink_failure");
    }

    [Fact]
    public async Task Given_UncooperativeSink_When_CompletionDeadlineExpires_Then_ReturnsBoundedIncompleteResult()
    {
        var clock = new ManualTimeProvider();
        var sink = new BlockingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics, completionTimeout: TimeSpan.FromSeconds(5));

        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        clock.Advance(TimeSpan.FromSeconds(3));
        session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000));
        await sink.Entered;
        clock.Advance(TimeSpan.FromSeconds(2));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "sink_drain_timeout");
        sink.Release();
    }

    [Fact]
    public async Task Given_MetricWorkPausesBoundedQueue_When_CaptureExceedsCapacity_Then_OverflowStaysDiagnostic()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        using var listener = new MeterListener();
        var metricEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMetric = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (instrument.Meter.Name == "Parchwell.Kafka.Statistics")
            {
                currentListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name == "kafka.broker.throttle.max_ms" && IsCurrentSession(tags))
            {
                metricEntered.TrySetResult();
                releaseMetric.Task.GetAwaiter().GetResult();
            }
        });
        listener.Start();
        await using var session = NewSession(clock, sink, metrics, queueCapacity: 1);

        clock.Advance(TimeSpan.FromSeconds(2));
        string withWindow = StatisticsTestSupport.Report("test-client", 11_000_000, 1_000_000)
            .Replace("\"cnt\": 0, \"max\": 0", "\"cnt\": 1, \"max\": 17", StringComparison.Ordinal);
        session.Capture(withWindow);
        await metricEntered.Task;
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        Task captures = Task.Run(() =>
        {
            session.Capture(StatisticsTestSupport.Report("test-client", 11_500_000, 1_500_000));
            session.Capture(StatisticsTestSupport.Report("test-client", 11_600_000, 1_600_000));
        }, TestContext.Current.CancellationToken);
        try
        {
            await captures.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseMetric.TrySetResult();
        }

        await sink.WaitForReportsAsync(2);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "capture_queue_overflow");
    }

    [Fact]
    public async Task Given_LateAttachment_When_FirstSamplePredatesSessionCreation_Then_EvidenceIsIncomplete()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);

        clock.Advance(TimeSpan.FromSeconds(2));
        session.Capture(StatisticsTestSupport.Report("test-client", 11_000_000, 3_000_000));
        await sink.WaitForReportsAsync(1);

        Assert.Contains(sink.Reports.Single().Diagnostics, item => item.Code == "late_attachment");
    }

    [Fact]
    public async Task Given_CompletionTimesOutWithoutExpectedBroker_When_DeadlineExpires_Then_ReportsMissingCoverage()
    {
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics, completionTimeout: TimeSpan.FromSeconds(1));

        Task<KafkaStatisticsCompletion> completionTask = CompleteAsync(session);
        clock.Advance(TimeSpan.FromSeconds(1));

        KafkaStatisticsCompletion completion = await completionTask;

        Assert.False(completion.IsComplete);
        Assert.Contains(completion.Diagnostics, item => item.Code == "completion_timeout");
        Assert.Equal(ExpectedBroker, completion.MissingBrokers);
    }

    private static Task<KafkaStatisticsCompletion> CompleteAsync(KafkaStatisticsSession session) =>
        session.CompleteAsync(ExpectedBroker, TestContext.Current.CancellationToken);

    private static bool IsCurrentSession(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (KeyValuePair<string, object?> tag in tags)
        {
            if (tag.Key == "run" && Equals(tag.Value, "run-test"))
            {
                return true;
            }
        }

        return false;
    }

    private static KafkaStatisticsSession NewSession(
        ManualTimeProvider clock,
        IKafkaStatisticsEvidenceSink sink,
        KafkaStatisticsMetrics metrics,
        int queueCapacity = 8,
        int maxBrokerSeries = 4,
        TimeSpan? completionTimeout = null,
        bool nativeClockCompatible = true) => new(
            StatisticsTestSupport.ProducerIdentity(),
            StatisticsTestSupport.Options(queueCapacity, maxBrokerSeries, completionTimeout),
            clock,
            sink,
            metrics,
            nativeClockCompatible);
}
