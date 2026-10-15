using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// These public-session tests force callback/completion overlap at the clock calls which used to
// happen before callback admission was registered.
[Trait("Category", "Unit")]
public sealed class KafkaStatisticsBoundaryTests
{
    private static readonly string[] ExpectedBroker = ["broker/1"];

    [Theory]
    [InlineData("timestamp")]
    [InlineData("utc")]
    public async Task Given_CallbackPausesBeforeAdmissionGate_When_CompletionSeals_Then_ResultIncludesCaptureLoss(string clockCall)
    {
        var clock = new BoundaryTimeProvider();
        var sink = new RecordingStatisticsSink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics, TimeSpan.FromSeconds(2));

        clock.Advance(TimeSpan.FromSeconds(2));
        session.Capture(StatisticsTestSupport.Report("test-client", 11_000_000, 1_000_000));
        await sink.WaitForReportsAsync(1);
        Task<KafkaStatisticsCompletion> completionTask = session.CompleteAsync(
            ExpectedBroker,
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(1));

        if (clockCall == "timestamp")
        {
            clock.PauseNextTimestamp();
        }
        else
        {
            clock.PauseNextUtcNow();
        }

        Task pausedCapture = Task.Run(() => session.Capture(StatisticsTestSupport.Report("test-client", 12_500_000, 2_500_000)), TestContext.Current.CancellationToken);
        KafkaStatisticsCompletion completion;
        try
        {
            await clock.Paused.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000));
            completion = await completionTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            clock.Resume();
            await pausedCapture.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.False(completion.IsComplete);
        Assert.Empty(completion.MissingBrokers);
        Assert.Equal(2, sink.Reports.Count);
        Assert.Collection(completion.Diagnostics, item => Assert.Equal("capture_loss", item.Code));
    }

    [Theory]
    [InlineData("caller")]
    [InlineData("deadline")]
    public async Task Given_CancelOrDeadlineAtFinalSealBoundary_When_WorkerHasDrained_Then_CompletionFailsClosed(string cause)
    {
        var clock = new BoundaryTimeProvider();
        var sink = new FinalBoundarySink();
        using var metrics = new KafkaStatisticsMetrics();
        await using var session = NewSession(clock, sink, metrics, TimeSpan.FromSeconds(5));

        clock.Advance(TimeSpan.FromSeconds(2));
        session.Capture(StatisticsTestSupport.Report("test-client", 11_000_000, 1_000_000));
        await sink.FirstWrite.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<KafkaStatisticsCompletion> completionTask = session.CompleteAsync(ExpectedBroker, cancellation.Token);
        clock.Advance(TimeSpan.FromSeconds(1));
        session.Capture(StatisticsTestSupport.Report("test-client", 12_900_000, 2_900_000));
        await sink.SecondWriteEntered.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.PauseNextTimestamp();
        sink.ReleaseSecondWrite();
        KafkaStatisticsCompletion completion;
        try
        {
            await clock.Paused.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            if (cause == "caller")
            {
                cancellation.Cancel();
            }
            else
            {
                clock.Advance(TimeSpan.FromSeconds(5));
            }

            clock.Resume();
            completion = await completionTask.WaitAsync(
                TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            clock.Resume();
            sink.ReleaseSecondWrite();
        }

        Assert.False(completion.IsComplete);
        Assert.Empty(completion.MissingBrokers);
        Assert.Collection(completion.Diagnostics, item => Assert.Equal(cause == "caller"
            ? "completion_cancelled"
            : "completion_timeout", item.Code));
    }

    private static KafkaStatisticsSession NewSession(
        BoundaryTimeProvider clock,
        IKafkaStatisticsEvidenceSink sink,
        KafkaStatisticsMetrics metrics,
        TimeSpan completionTimeout) => new(
            StatisticsTestSupport.ProducerIdentity() with { RunId = "boundary-test-run" },
            StatisticsTestSupport.Options(completionTimeout: completionTimeout),
            clock,
            sink,
            metrics,
            nativeClockCompatible: true);

    private sealed class FinalBoundarySink : IKafkaStatisticsEvidenceSink
    {
        private readonly TaskCompletionSource _firstWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondWriteEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseSecondWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _writeCount;

        internal Task FirstWrite => _firstWrite.Task;
        internal Task SecondWriteEntered => _secondWriteEntered.Task;
        internal void ReleaseSecondWrite() => _releaseSecondWrite.TrySetResult();

        public async ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _writeCount) == 1)
            {
                _firstWrite.TrySetResult();
                return;
            }

            _secondWriteEntered.TrySetResult();
            await _releaseSecondWrite.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class BoundaryTimeProvider : TimeProvider
    {
        private readonly object _gate = new();
        private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private long _timestampMicroseconds = 10_000_000;
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch.AddSeconds(10);
        private bool _pauseTimestamp;
        private bool _pauseUtcNow;

        internal Task Paused => _paused.Task;

        public override long TimestampFrequency => 1_000_000;

        public override long GetTimestamp()
        {
            bool pause;
            lock (_gate)
            {
                pause = _pauseTimestamp;
                _pauseTimestamp = false;
            }

            if (pause)
            {
                Pause();
            }

            lock (_gate)
            {
                return _timestampMicroseconds;
            }
        }

        public override DateTimeOffset GetUtcNow()
        {
            bool pause;
            lock (_gate)
            {
                pause = _pauseUtcNow;
                _pauseUtcNow = false;
            }

            if (pause)
            {
                Pause();
            }

            lock (_gate)
            {
                return _utcNow;
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            TimeProvider.System.CreateTimer(callback, state, dueTime, period);

        internal void PauseNextTimestamp()
        {
            lock (_gate)
            {
                _pauseTimestamp = true;
            }
        }

        internal void PauseNextUtcNow()
        {
            lock (_gate)
            {
                _pauseUtcNow = true;
            }
        }

        internal void Advance(TimeSpan amount)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(amount, TimeSpan.Zero);
            lock (_gate)
            {
                _timestampMicroseconds = checked(_timestampMicroseconds + amount.Ticks / 10);
                _utcNow += amount;
            }
        }

        internal void Resume() => _resume.TrySetResult();

        private void Pause()
        {
            _paused.TrySetResult();
            _resume.Task.GetAwaiter().GetResult();
        }
    }
}
