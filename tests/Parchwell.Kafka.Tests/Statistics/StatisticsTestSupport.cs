using System.Collections.Concurrent;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;

namespace Parchwell.Kafka.Tests.Statistics;

internal static class StatisticsTestSupport
{
    internal static KafkaStatisticsOptions Options(
        int queueCapacity = 8,
        int maxBrokerSeries = 4,
        TimeSpan? completionTimeout = null) => new()
    {
        MaxSnapshotCharacters = 16_384,
        QueueCapacity = queueCapacity,
        MaxBrokerSeries = maxBrokerSeries,
        CompletionTimeout = completionTimeout ?? TimeSpan.FromSeconds(5)
    };

    internal static KafkaStatisticsIdentity ProducerIdentity(string clientId = "test-client") => new(
        "A01", "run-test", KafkaClientRole.LoadGenProducer, clientId);

    // Builds explicitly synthetic reports for session tests; these values are controlled inputs, not captured evidence.
    internal static string Report(
        string clientId,
        long timestampMicroseconds,
        long ageMicroseconds,
        long requestTimeouts = 0,
        long disconnects = 0,
        string broker = "broker/1",
        string type = "producer",
        string additionalBrokers = "") => $$"""
        {
          "name": "{{clientId}}-native",
          "client_id": "{{clientId}}",
          "type": "{{type}}",
          "ts": {{timestampMicroseconds}},
          "time": 1791999600,
          "age": {{ageMicroseconds}},
          "brokers": {
            "{{broker}}": {
              "name": "{{broker}}", "nodeid": 1, "state": "UP", "stateage": 100,
              "req_timeouts": {{requestTimeouts}}, "disconnects": {{disconnects}},
              "txretries": 0, "txerrs": 0, "rxerrs": 0,
              "throttle": { "cnt": 0, "max": 0 }, "rtt": { "cnt": 0, "p99": 0 }
            }{{additionalBrokers}}
          }
        }
        """;
}

internal sealed class RecordingStatisticsSink : IKafkaStatisticsEvidenceSink
{
    private readonly object _gate = new();
    private readonly ConcurrentQueue<KafkaStatisticsReport> _reports = new();
    private readonly List<(int Count, TaskCompletionSource Signal)> _waiters = [];

    internal IReadOnlyCollection<KafkaStatisticsReport> Reports => _reports.ToArray();

    public ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken)
    {
        _reports.Enqueue(report);
        lock (_gate)
        {
            foreach ((int count, TaskCompletionSource signal) in _waiters.Where(waiter => _reports.Count >= waiter.Count))
            {
                signal.TrySetResult();
            }

            _waiters.RemoveAll(waiter => _reports.Count >= waiter.Count);
        }

        return ValueTask.CompletedTask;
    }

    internal async Task WaitForReportsAsync(int count)
    {
        Task wait;
        lock (_gate)
        {
            if (_reports.Count >= count)
            {
                return;
            }

            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((count, signal));
            wait = signal.Task;
        }

        await wait.WaitAsync(TimeSpan.FromSeconds(5));
    }
}

internal sealed class BlockingStatisticsSink : IKafkaStatisticsEvidenceSink
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task Entered => _entered.Task;
    internal void Release() => _released.TrySetResult();

    public async ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken)
    {
        _entered.TrySetResult();
        await _released.Task.ConfigureAwait(false);
    }
}

internal sealed class FailingStatisticsSink : IKafkaStatisticsEvidenceSink
{
    public ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken) =>
        ValueTask.FromException(new InvalidOperationException("synthetic evidence sink failure"));
}

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly HashSet<ManualTimer> _timers = [];
    private readonly long _timestampFrequency;
    private long _timestamp;
    private DateTimeOffset _utcNow;

    internal ManualTimeProvider(long timestampFrequency = 1_000_000, long initialTimestamp = 10_000_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timestampFrequency);
        _timestampFrequency = timestampFrequency;
        _timestamp = initialTimestamp;
        _utcNow = DateTimeOffset.UnixEpoch.AddTicks(checked(initialTimestamp * TimeSpan.TicksPerSecond / timestampFrequency));
    }

    public override long TimestampFrequency => _timestampFrequency;

    public override long GetTimestamp()
    {
        lock (_gate)
        {
            return _timestamp;
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _utcNow;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    internal void Advance(TimeSpan amount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, TimeSpan.Zero);

        List<(TimerCallback Callback, object? State)> callbacks = [];
        lock (_gate)
        {
            long target = checked(_timestamp + ToTimestampUnits(amount, _timestampFrequency));
            _utcNow += amount;
            while (true)
            {
                ManualTimer? due = _timers
                    .Where(timer => !timer.IsDisposed && timer.DueTimestamp <= target)
                    .OrderBy(timer => timer.DueTimestamp)
                    .FirstOrDefault();
                if (due is null)
                {
                    break;
                }

                _timestamp = due.DueTimestamp;
                callbacks.Add((due.Callback, due.State));
                due.Reschedule();
                if (due.PeriodMicroseconds == 0)
                {
                    due.Disable();
                }
            }

            _timestamp = target;
        }

        foreach ((TimerCallback callback, object? state) in callbacks)
        {
            callback(state);
        }
    }

    private static long ToTimestampUnits(TimeSpan value, long frequency) =>
        checked(value.Ticks * frequency / TimeSpan.TicksPerSecond);

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;
        private bool _disposed;
        private long _dueTimestamp = long.MaxValue;
        private long _periodMicroseconds;

        internal ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            Callback = callback;
            State = state;
        }

        internal TimerCallback Callback { get; }
        internal object? State { get; }
        internal bool IsDisposed => _disposed;
        internal long DueTimestamp => _dueTimestamp;
        internal long PeriodMicroseconds => _periodMicroseconds;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime < TimeSpan.Zero && dueTime != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(dueTime));
            }

            if (period < TimeSpan.Zero && period != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(period));
            }

            if (_disposed)
            {
                return false;
            }

            long now = _owner.GetTimestamp();
            _dueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                ? long.MaxValue
                : checked(now + ToTimestampUnits(dueTime, _owner.TimestampFrequency));
            _periodMicroseconds = period == Timeout.InfiniteTimeSpan ? 0 : ToTimestampUnits(period, _owner.TimestampFrequency);
            return true;
        }

        internal void Reschedule() =>
            _dueTimestamp = _periodMicroseconds > 0
                ? checked(_dueTimestamp + _periodMicroseconds)
                : long.MaxValue;

        internal void Disable() => _dueTimestamp = long.MaxValue;

        public void Dispose()
        {
            _disposed = true;
            _owner.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
