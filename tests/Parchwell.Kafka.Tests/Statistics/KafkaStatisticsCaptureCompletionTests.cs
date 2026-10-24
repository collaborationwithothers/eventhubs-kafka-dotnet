using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class KafkaStatisticsCaptureCompletionTests
{
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(5);
    private static readonly string[] ExpectedBrokers = ["broker/1"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_CaptureOnlyCompletion_When_FreshReportIsRetained_Then_ReportsCaptureWithoutInventory(bool earlierReport)
    {
        var clock = new ManualTimeProvider(initialTimestamp: 9_000_000);
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);
        clock.Advance(TimeSpan.FromSeconds(1));
        if (earlierReport)
        {
            session.Capture(Report(9_999_999));
            await sink.WaitForReportsAsync(1);
        }

        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromTicks(10));
        string json = Report(10_000_001);
        session.Capture(json);
        KafkaStatisticsCompletion result = await WaitAsync(pending);

        Assert.True(result.IsComplete);
        Assert.Empty(result.MissingBrokers);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(10_000_000, result.DrainBoundaryMicroseconds);
        Assert.Equal(10_000_001, result.FirstPostDrainTimestampMicroseconds);
        Assert.Equal(json, sink.Reports.Last().RawJson);
    }

    [Fact]
    public async Task Given_ReportsAroundBoundary_When_ReceivedLater_Then_OnlyStrictlyLaterGenerationCompletes()
    {
        var clock = new ManualTimeProvider(initialTimestamp: 9_000_000);
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);
        clock.Advance(TimeSpan.FromSeconds(1));
        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        long[] timestamps = [9_999_999, 10_000_000, 10_000_001];
        for (int index = 0; index < timestamps.Length; index++)
        {
            string json = Report(timestamps[index]);
            session.Capture(json);
            await sink.WaitForReportsAsync(index + 1);
            KafkaStatisticsReport report = sink.Reports.ElementAt(index);
            Assert.Equal(json, report.RawJson);
            Assert.Equal(timestamps[index], report.Snapshot!.TimestampMicroseconds);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddMilliseconds(10_001), report.ReceivedAt);
            if (index < 2)
            {
                Assert.False(pending.IsCompleted);
            }
        }

        KafkaStatisticsCompletion result = await WaitAsync(pending);
        Assert.True(result.IsComplete);
        Assert.Equal(10_000_000, result.DrainBoundaryMicroseconds);
        Assert.Equal(10_000_001, result.FirstPostDrainTimestampMicroseconds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NoQualifyingBrokers_When_DeadlineExpires_Then_CaptureRemainsIncomplete(bool emptyReport)
    {
        var clock = new ManualTimeProvider(initialTimestamp: 9_000_000);
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);
        clock.Advance(TimeSpan.FromSeconds(1));
        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(TestContext.Current.CancellationToken);
        if (emptyReport)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1));
            const string json = """
                {"name":"test-client-native","client_id":"test-client","type":"producer",
                 "ts":10000001,"time":1791999600,"age":1000001,"brokers":{}}
                """;
            session.Capture(json);
            await sink.WaitForReportsAsync(1);
            Assert.Equal(json, Assert.Single(sink.Reports).RawJson);
        }

        clock.Advance(TimeSpan.FromSeconds(5));
        KafkaStatisticsCompletion result = await WaitAsync(pending);
        Assert.False(result.IsComplete);
        Assert.Empty(result.MissingBrokers);
        Assert.Null(result.FirstPostDrainTimestampMicroseconds);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "completion_timeout");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(1, 20001)]
    public async Task Given_EmptyOrObservedThrottleWindow_When_CaptureCompletes_Then_PreservesAbsentOrActualValue(long count, long maximum)
    {
        var clock = new ManualTimeProvider(initialTimestamp: 9_000_000);
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        using var listener = new MeterListener();
        var values = new ConcurrentQueue<long>();
        string run = Guid.NewGuid().ToString("N");
        listener.InstrumentPublished = (instrument, activeListener) =>
        {
            if (instrument.Meter.Name == "Parchwell.Kafka.Statistics" && instrument.Name == "kafka.broker.throttle.max_ms")
            {
                activeListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == "run" && Equals(tag.Value, run))
                {
                    values.Enqueue(value);
                }
            }
        });
        listener.Start();
        await using var session = new KafkaStatisticsSession(
            StatisticsTestSupport.ProducerIdentity() with { RunId = run }, StatisticsTestSupport.Options(),
            clock, sink, metrics, nativeClockCompatible: true);
        clock.Advance(TimeSpan.FromSeconds(1));
        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        string json = Report(10_000_001).Replace("\"cnt\": 0, \"max\": 0", $"\"cnt\": {count}, \"max\": {maximum}", StringComparison.Ordinal);
        session.Capture(json);
        KafkaStatisticsCompletion result = await WaitAsync(pending);

        Assert.True(result.IsComplete);
        KafkaStatisticsReport report = Assert.Single(sink.Reports);
        Assert.Equal(json, report.RawJson);
        KafkaWindowStatistics throttle = Assert.Single(report.Snapshot!.Brokers).Throttle;
        Assert.Equal(count, throttle.Count);
        Assert.Equal(count == 0 ? null : (long?)maximum, throttle.Value);
        if (count == 0)
        {
            Assert.Empty(values);
        }
        else
        {
            Assert.Equal(maximum, Assert.Single(values));
        }
    }

    [Theory]
    [InlineData("malformed", "invalid_json")]
    [InlineData("misattributed", "client_id_mismatch")]
    public async Task Given_InvalidReport_When_FreshValidReportFollows_Then_DiagnosticAndRawEvidencePersist(string kind, string diagnostic)
    {
        var clock = new ManualTimeProvider(initialTimestamp: 9_000_000);
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);
        clock.Advance(TimeSpan.FromSeconds(1));
        string bad = kind == "malformed" ? "{" : Report(9_999_999).Replace("test-client", "wrong-client", StringComparison.Ordinal);
        session.Capture(bad);
        await sink.WaitForReportsAsync(1);
        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        session.Capture(Report(10_000_001));
        KafkaStatisticsCompletion result = await WaitAsync(pending);

        Assert.False(result.IsComplete);
        Assert.Equal(bad, sink.Reports.First().RawJson);
        Assert.Contains(result.Diagnostics, item => item.Code == diagnostic);
        Assert.Equal(10_000_001, result.FirstPostDrainTimestampMicroseconds);
    }

    [Fact]
    public async Task Given_FirstSinkWriteFails_When_FreshReportIsLaterRetained_Then_FailureStaysLatched()
    {
        var clock = new ManualTimeProvider(initialTimestamp: 9_000_000);
        var sink = new RecoveringSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);
        clock.Advance(TimeSpan.FromSeconds(1));
        session.Capture(Report(9_999_999));
        await sink.Failed.WaitAsync(Watchdog, TestContext.Current.CancellationToken);
        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        session.Capture(Report(10_000_001));
        KafkaStatisticsCompletion result = await WaitAsync(pending);

        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, item => item.Code == "evidence_sink_failure");
        Assert.Equal(10_000_001, Assert.Single(sink.Retained.Reports).Snapshot!.TimestampMicroseconds);
    }

    [Theory]
    [InlineData("release")]
    [InlineData("cancel")]
    [InlineData("deadline")]
    public async Task Given_FreshReportBlockedAtSink_When_WriteFinishesOrWaitEnds_Then_OnlySuccessfulWriteCanEstablishCapture(string ending)
    {
        var clock = new ManualTimeProvider(initialTimestamp: 9_000_000);
        var sink = new BlockingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(1));
        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(cancellation.Token);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        session.Capture(Report(10_000_001));
        await sink.Entered.WaitAsync(Watchdog, TestContext.Current.CancellationToken);
        Assert.False(pending.IsCompleted);
        KafkaStatisticsCompletion result;
        try
        {
            if (ending == "release")
            {
                sink.Release();
            }
            else if (ending == "cancel")
            {
                await cancellation.CancelAsync();
            }
            else
            {
                clock.Advance(TimeSpan.FromSeconds(5));
            }

            result = await WaitAsync(pending);
        }
        finally
        {
            sink.Release();
        }

        Assert.Equal(ending == "release", result.IsComplete);
        Assert.Equal(ending == "release" ? 10_000_001 : (long?)null, result.FirstPostDrainTimestampMicroseconds);
        if (ending != "release")
        {
            Assert.Contains(result.Diagnostics, item => item.Code == (ending == "cancel" ? "completion_cancelled" : "completion_timeout"));
            await session.DisposeAsync();
            Assert.Null(result.FirstPostDrainTimestampMicroseconds);
        }
    }

    [Fact]
    public async Task Given_CancellationBeforeCompletion_When_Completing_Then_ReturnsIncomplete()
    {
        var clock = new ManualTimeProvider();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, new RecordingStatisticsSink(), metrics);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        KafkaStatisticsCompletion result = await WaitAsync(session.CompleteAsync(cancellation.Token));
        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, item => item.Code == "completion_cancelled");
        Assert.Null(result.FirstPostDrainTimestampMicroseconds);
    }

    [Fact]
    public async Task Given_DisposalDuringCompletion_When_NoReportArrives_Then_ReturnsIncomplete()
    {
        var clock = new ManualTimeProvider();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, new RecordingStatisticsSink(), metrics);
        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(TestContext.Current.CancellationToken);
        await session.DisposeAsync();
        KafkaStatisticsCompletion result = await WaitAsync(pending);
        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, item => item.Code == "disposed_during_completion");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_CompletionStarted_When_EitherOverloadIsCalledAgain_Then_RejectsSecondCompletion(bool firstLegacy, bool secondLegacy)
    {
        var clock = new ManualTimeProvider();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, new RecordingStatisticsSink(), metrics);
        using var cancellation = new CancellationTokenSource();
        Task<KafkaStatisticsCompletion> pending = firstLegacy
            ? session.CompleteAsync(ExpectedBrokers, cancellation.Token)
            : session.CompleteAsync(cancellation.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => secondLegacy
            ? session.CompleteAsync(ExpectedBrokers, TestContext.Current.CancellationToken)
            : session.CompleteAsync(TestContext.Current.CancellationToken));
        await cancellation.CancelAsync();
        KafkaStatisticsCompletion result = await WaitAsync(pending);
        Assert.Null(result.FirstPostDrainTimestampMicroseconds);
    }

    [Fact]
    public async Task Given_LegacyCompletion_When_FreshExpectedBrokerArrives_Then_NewTimestampRemainsNull()
    {
        var clock = new ManualTimeProvider(initialTimestamp: 9_000_000);
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);
        clock.Advance(TimeSpan.FromSeconds(1));
        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(ExpectedBrokers, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        session.Capture(Report(10_000_001));
        KafkaStatisticsCompletion result = await WaitAsync(pending);
        Assert.True(result.IsComplete);
        Assert.Empty(result.MissingBrokers);
        Assert.Null(result.FirstPostDrainTimestampMicroseconds);
    }

    [Fact]
    public async Task Given_BlockedWorkerAndFullQueue_When_FreshReportFollows_Then_OverflowRemainsIncomplete()
    {
        var clock = new ManualTimeProvider(initialTimestamp: 9_000_000);
        var sink = new FirstWriteBlockingSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics, queueCapacity: 1);
        clock.Advance(TimeSpan.FromSeconds(1));
        session.Capture(Report(9_999_998));
        await sink.Entered.WaitAsync(Watchdog, TestContext.Current.CancellationToken);
        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(TestContext.Current.CancellationToken);
        try
        {
            session.Capture(Report(9_999_999));
            session.Capture(Report(10_000_000));
        }
        finally
        {
            sink.Release();
        }

        await sink.Retained.WaitForReportsAsync(2);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        session.Capture(Report(10_000_001));
        KafkaStatisticsCompletion result = await WaitAsync(pending);
        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, item => item.Code == "capture_queue_overflow");
        Assert.Equal(3, sink.Retained.Reports.Count);
        Assert.Equal(10_000_001, result.FirstPostDrainTimestampMicroseconds);
    }

    [Fact]
    public async Task Given_CaptureContendsWithCompletionGate_When_FreshReportFollows_Then_LossStaysLatched()
    {
        using var clock = new PausingTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);
        clock.Advance(TimeSpan.FromSeconds(1));
        // The first read is completion start; the second is the boundary read inside the gate.
        clock.PauseTimestampCall(2);
        Task<KafkaStatisticsCompletion>? pending = null;
        Task initializeCompletion = Task.Run(() =>
        {
            pending = session.CompleteAsync(TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);
        try
        {
            await clock.Paused.WaitAsync(Watchdog, TestContext.Current.CancellationToken);
            session.Capture(Report(10_000_000));
        }
        finally
        {
            clock.Resume();
        }

        await initializeCompletion.WaitAsync(Watchdog, TestContext.Current.CancellationToken);
        Assert.NotNull(pending);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        session.Capture(Report(10_000_001));
        KafkaStatisticsCompletion result = await WaitAsync(pending);
        Assert.False(result.IsComplete);
        Assert.Contains(result.Diagnostics, item => item.Code == "capture_contention");
        Assert.Contains(result.Diagnostics, item => item.Code == "capture_loss");
        Assert.Single(sink.Reports);
    }

    [Fact]
    public async Task Given_CallbackPausedBeforeGate_When_CompletionSeals_Then_ReturnedLossCannotBeRewritten()
    {
        using var clock = new PausingTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);
        clock.Advance(TimeSpan.FromSeconds(1));
        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        clock.PauseTimestampCall(1);
        Task callback = Task.Run(() => session.Capture(Report(10_000_001)), TestContext.Current.CancellationToken);
        KafkaStatisticsCompletion result;
        try
        {
            await clock.Paused.WaitAsync(Watchdog, TestContext.Current.CancellationToken);
            session.Capture(Report(10_000_002));
            result = await WaitAsync(pending);
        }
        finally
        {
            clock.Resume();
            await callback.WaitAsync(Watchdog, TestContext.Current.CancellationToken);
        }

        Assert.False(result.IsComplete);
        Assert.Collection(result.Diagnostics, item => Assert.Equal("capture_loss", item.Code));
        Assert.Equal(10_000_002, result.FirstPostDrainTimestampMicroseconds);
        session.Capture(Report(10_000_003));
        Assert.Single(sink.Reports);
        Assert.Collection(result.Diagnostics, item => Assert.Equal("capture_loss", item.Code));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_QueuedReportsAfterFreshObservation_When_DrainCompletesOrCancels_Then_AllAcceptedWorkControlsCompletion(bool cancel)
    {
        var clock = new ManualTimeProvider(initialTimestamp: 9_000_000);
        var sink = new TwoWriteBlockingSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(1));
        Task<KafkaStatisticsCompletion> pending = session.CompleteAsync(cancellation.Token);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        session.Capture(Report(10_000_001));
        await sink.FirstEntered.WaitAsync(Watchdog, TestContext.Current.CancellationToken);
        KafkaStatisticsCompletion result;
        try
        {
            session.Capture(Report(10_000_002));
            sink.ReleaseFirst();
            await sink.SecondEntered.WaitAsync(Watchdog, TestContext.Current.CancellationToken);
            Assert.False(pending.IsCompleted);
            if (cancel)
            {
                await cancellation.CancelAsync();
            }
            else
            {
                sink.ReleaseSecond();
            }

            result = await WaitAsync(pending);
        }
        finally
        {
            sink.ReleaseFirst();
            sink.ReleaseSecond();
        }

        Assert.Equal(!cancel, result.IsComplete);
        Assert.Equal(10_000_001, result.FirstPostDrainTimestampMicroseconds);
        if (cancel)
        {
            Assert.Contains(result.Diagnostics, item => item.Code == "completion_cancelled");
        }
        else
        {
            Assert.Equal(2, sink.Retained.Reports.Count);
        }
    }

    private static string Report(long timestamp) => StatisticsTestSupport.Report("test-client", timestamp, timestamp - 9_000_000);

    private static Task<KafkaStatisticsCompletion> WaitAsync(Task<KafkaStatisticsCompletion> task) =>
        task.WaitAsync(Watchdog, TestContext.Current.CancellationToken);

    private static KafkaStatisticsSession NewSession(TimeProvider clock, IKafkaStatisticsEvidenceSink sink, KafkaStatisticsMetrics metrics, int queueCapacity = 8) => new(
        StatisticsTestSupport.ProducerIdentity(), StatisticsTestSupport.Options(queueCapacity: queueCapacity),
        clock, sink, metrics, nativeClockCompatible: true);

    private sealed class RecoveringSink : IKafkaStatisticsEvidenceSink
    {
        private readonly FailingStatisticsSink _failure = new();
        private readonly TaskCompletionSource _failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _writes;
        internal Task Failed => _failed.Task;
        internal RecordingStatisticsSink Retained { get; } = new();

        public ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _writes) == 1)
            {
                _failed.TrySetResult();
                return _failure.WriteAsync(report, cancellationToken);
            }

            return Retained.WriteAsync(report, cancellationToken);
        }
    }

    private sealed class FirstWriteBlockingSink : IKafkaStatisticsEvidenceSink
    {
        private readonly BlockingStatisticsSink _first = new();
        private int _writes;
        internal Task Entered => _first.Entered;
        internal RecordingStatisticsSink Retained { get; } = new();
        internal void Release() => _first.Release();

        public async ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _writes) == 1)
            {
                await _first.WriteAsync(report, cancellationToken).ConfigureAwait(false);
            }

            await Retained.WriteAsync(report, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class TwoWriteBlockingSink : IKafkaStatisticsEvidenceSink
    {
        private readonly BlockingStatisticsSink _first = new();
        private readonly BlockingStatisticsSink _second = new();
        private int _writes;
        internal Task FirstEntered => _first.Entered;
        internal Task SecondEntered => _second.Entered;
        internal RecordingStatisticsSink Retained { get; } = new();
        internal void ReleaseFirst() => _first.Release();
        internal void ReleaseSecond() => _second.Release();

        public async ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken)
        {
            BlockingStatisticsSink blocker = Interlocked.Increment(ref _writes) == 1 ? _first : _second;
            await blocker.WriteAsync(report, cancellationToken).ConfigureAwait(false);
            await Retained.WriteAsync(report, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class PausingTimeProvider : TimeProvider, IDisposable
    {
        private readonly ManualTimeProvider _clock = new(initialTimestamp: 9_000_000);
        private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _resume = new();
        private int _callsUntilPause;

        internal Task Paused => _paused.Task;
        public override long TimestampFrequency => _clock.TimestampFrequency;
        public override DateTimeOffset GetUtcNow() => _clock.GetUtcNow();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            _clock.CreateTimer(callback, state, dueTime, period);

        public override long GetTimestamp()
        {
            if (Volatile.Read(ref _callsUntilPause) > 0 && Interlocked.Decrement(ref _callsUntilPause) == 0)
            {
                _paused.TrySetResult();
                if (!_resume.Wait(Watchdog, TestContext.Current.CancellationToken))
                {
                    throw new TimeoutException("Synthetic clock pause exceeded the test watchdog.");
                }

            }

            return _clock.GetTimestamp();
        }

        internal void PauseTimestampCall(int call) => Volatile.Write(ref _callsUntilPause, call);
        internal void Advance(TimeSpan amount) => _clock.Advance(amount);
        internal void Resume() => _resume.Set();
        public void Dispose() => _resume.Dispose();
    }
}
