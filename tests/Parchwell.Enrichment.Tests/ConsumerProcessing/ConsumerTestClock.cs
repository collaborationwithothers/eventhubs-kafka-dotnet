using System.Globalization;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

internal sealed class ConsumerTestClock : TimeProvider
{
    private readonly object gate = new();
    private readonly List<ClockTimer> timers = [];
    private long milliseconds;
    public override long TimestampFrequency => 1000;
    public override long GetTimestamp() { lock (gate) return milliseconds; }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-11-15T00:00:00Z", CultureInfo.InvariantCulture).AddMilliseconds(GetTimestamp());
    internal long? NextTimer
    {
        get { lock (gate) return timers.Where(timer => !timer.Disposed && timer.Due >= 0).Select(timer => (long?)timer.Due).Min(); }
    }
    internal void Advance(long amount)
    {
        List<ClockTimer> due;
        lock (gate)
        {
            milliseconds += amount;
            due = timers.Where(timer => !timer.Disposed && timer.Due >= 0 && timer.Due <= milliseconds).ToList();
            foreach (ClockTimer timer in due) timer.Due = timer.Period < 0 ? -1 : milliseconds + timer.Period;
        }
        foreach (ClockTimer timer in due) timer.Fire();
    }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ClockTimer(this, callback, state);
        lock (gate) timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }
    private sealed class ClockTimer(ConsumerTestClock clock, TimerCallback callback, object? state) : ITimer
    {
        internal long Due = -1;
        internal long Period = -1;
        internal bool Disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock.gate)
            {
                if (Disposed) return false;
                Due = dueTime == Timeout.InfiniteTimeSpan ? -1 : clock.milliseconds + (long)dueTime.TotalMilliseconds;
                Period = period == Timeout.InfiniteTimeSpan ? -1 : (long)period.TotalMilliseconds;
                return true;
            }
        }
        internal void Fire() => callback(state);
        public void Dispose() { lock (clock.gate) Disposed = true; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
