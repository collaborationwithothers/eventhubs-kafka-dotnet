using System.Reflection;
using Parchwell.Kafka.Statistics;

namespace Parchwell.Kafka.Tests.Statistics;

// Test doubles for the late-capture tests. Sessions come from the public Create of the factory, built
// through its internal constructor so a test can supply a clock. The session is driven through its public
// surface; SessionProbe additionally reads two private fields and takes the session's gate once.
internal static class LateCaptureTestSupport
{
    internal static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(5);

    internal static string Report(long timestampMicroseconds) =>
        StatisticsTestSupport.Report("test-client", timestampMicroseconds, timestampMicroseconds - 9_000_000);

    // Same factory class production uses, with a controlled clock whose native compatibility is asserted.
    internal static KafkaStatisticsSession NewSession(
        TimeProvider clock,
        IKafkaStatisticsEvidenceSink sink,
        KafkaStatisticsMetrics metrics) =>
        new KafkaStatisticsBridgeFactory(StatisticsTestSupport.Options(), clock, sink, metrics, nativeClockCompatible: true)
            .Create(StatisticsTestSupport.ProducerIdentity());

    internal static async Task WaitUntilAsync(Func<bool> condition, string what, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Watchdog);
        while (!condition())
        {
            try
            {
                await Task.Delay(1, timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Did not observe within 5 seconds: {what}.");
            }
        }
    }
}

// Reads the session's private capture state so a test can tell that completion closed capture, and
// briefly takes the session's gate to learn that no other thread holds it. Only WaitUntilGateFree
// holds the gate: use it only where no admitted callback can arrive meanwhile, because a callback that
// finds the gate held records capture contention. The field read does not touch the gate.
// A probe that cannot find what it reads fails the test; it never passes vacuously.
internal static class SessionProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FieldInfo CaptureOpen = typeof(KafkaStatisticsSession).GetField("_captureOpen", Private)
        ?? throw new InvalidOperationException("KafkaStatisticsSession has no _captureOpen field to observe.");

    private static readonly FieldInfo Gate = typeof(KafkaStatisticsSession).GetField("_gate", Private)
        ?? throw new InvalidOperationException("KafkaStatisticsSession has no _gate field to observe.");

    internal static Task WaitUntilCaptureClosedAsync(KafkaStatisticsSession session, CancellationToken cancellationToken) =>
        LateCaptureTestSupport.WaitUntilAsync(
            () => !(bool)CaptureOpen.GetValue(session)!, "capture closed by completion", cancellationToken);

    // Waits until no other thread holds the session's state gate, so a resumed callback cannot
    // meet the close itself and take the contention path.
    internal static void WaitUntilGateFree(KafkaStatisticsSession session)
    {
        object gate = Gate.GetValue(session) ?? throw new InvalidOperationException("The session gate is null.");
        if (!Monitor.TryEnter(gate, LateCaptureTestSupport.Watchdog))
        {
            throw new TimeoutException("The session gate was not free within 5 seconds.");
        }

        Monitor.Exit(gate);
    }
}

// Blocks the first two writes that qualify until the test releases each, then records the report.
// A held write waits at most 5 seconds and then throws, so a failed test's disposal completes.
internal sealed class HoldingStatisticsSink : IKafkaStatisticsEvidenceSink
{
    private readonly object _gate = new();
    private readonly Func<KafkaStatisticsReport, bool> _qualifies;
    private readonly List<long> _written = [];
    private readonly TaskCompletionSource[] _entered =
        [new(TaskCreationOptions.RunContinuationsAsynchronously), new(TaskCreationOptions.RunContinuationsAsynchronously)];
    private readonly TaskCompletionSource[] _released =
        [new(TaskCreationOptions.RunContinuationsAsynchronously), new(TaskCreationOptions.RunContinuationsAsynchronously)];
    private int _held;

    internal HoldingStatisticsSink(Func<KafkaStatisticsReport, bool> qualifies) => _qualifies = qualifies;

    internal long[] WrittenTimestamps
    {
        get
        {
            lock (_gate)
            {
                return [.. _written];
            }
        }
    }

    internal Task EnteredAsync(int writeNumber, CancellationToken cancellationToken) =>
        _entered[writeNumber - 1].Task.WaitAsync(LateCaptureTestSupport.Watchdog, cancellationToken);

    internal void Release(int writeNumber) => _released[writeNumber - 1].TrySetResult();

    internal void ReleaseAll()
    {
        Release(1);
        Release(2);
    }

    public async ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken)
    {
        int index = -1;
        if (_qualifies(report))
        {
            lock (_gate)
            {
                if (_held < 2)
                {
                    index = _held++;
                }
            }
        }

        if (index >= 0)
        {
            _entered[index].TrySetResult();
            await _released[index].Task.WaitAsync(LateCaptureTestSupport.Watchdog, cancellationToken).ConfigureAwait(false);
        }

        lock (_gate)
        {
            _written.Add(report.Snapshot?.TimestampMicroseconds ?? -1);
        }
    }
}

// A clock whose next timestamp read can be paused, which holds a Capture call after admission and
// before it reaches the session gate. Timers use the system clock so completion deadlines still fire.
internal sealed class PausingClock : TimeProvider
{
    private readonly object _gate = new();
    private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _timestampMicroseconds = 9_000_000;
    private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch.AddSeconds(9);
    private bool _pauseNext;

    public override long TimestampFrequency => 1_000_000;

    internal Task PausedAsync(CancellationToken cancellationToken) =>
        _paused.Task.WaitAsync(LateCaptureTestSupport.Watchdog, cancellationToken);

    public override long GetTimestamp()
    {
        bool pause;
        lock (_gate)
        {
            pause = _pauseNext;
            _pauseNext = false;
        }

        if (pause)
        {
            _paused.TrySetResult();
            if (!_resume.Task.Wait(LateCaptureTestSupport.Watchdog))
            {
                throw new TimeoutException("The paused clock read was not resumed within 5 seconds.");
            }
        }

        lock (_gate)
        {
            return _timestampMicroseconds;
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
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
            _pauseNext = true;
        }
    }

    internal void Resume() => _resume.TrySetResult();

    internal void Advance(TimeSpan amount)
    {
        lock (_gate)
        {
            _timestampMicroseconds = checked(_timestampMicroseconds + amount.Ticks / 10);
            _utcNow += amount;
        }
    }
}
