using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Repository-required Given_When_Then names.
public sealed partial class KubernetesWatchTests
{
    [Theory]
    [InlineData(119, false, true)]
    [InlineData(120, false, true)]
    [InlineData(121, false, true)]
    [InlineData(179, false, true)]
    [InlineData(180, false, false)]
    [InlineData(181, false, false)]
    [InlineData(119, true, true)]
    [InlineData(120, true, true)]
    [InlineData(121, true, true)]
    [InlineData(179, true, true)]
    [InlineData(180, true, false)]
    [InlineData(181, true, false)]
    public async Task Given_ProducerSchedulingAroundAdmissionBoundaries_When_Observed_Then_Full180SecondsRemainSubjectToTheEarlierParentDeadline(
        int scheduledSecond, bool sharedDeadline, bool accepted)
    {
        var clock = new ProducerObservationClock();
        var response = new TaskCompletionSource<ProcessResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduled = new Step(token => response.Task.WaitAsync(token));
        using var fixture = new WatchFixture(clock, Step.Return(Event("BOOKMARK", "initial")), scheduled);
        await fixture.BindAsync();
        PodAdmission admission = await fixture.Cluster.EstablishProducerWatchAsync(fixture.Context, TestContext.Current.CancellationToken);
        await scheduled.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using var parentDeadline = sharedDeadline ? new CancellationTokenSource(TimeSpan.FromSeconds(180), clock) : null;
        using var parent = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, parentDeadline?.Token ?? CancellationToken.None);
        // The shared deadline starts with producer creation; observing later cannot restart it.
        if (sharedDeadline) clock.SetTime(60);
        Task<ProducerPod> observation = fixture.Cluster.ObserveProducerAsync(fixture.Context, admission, parent.Token);
        Assert.False(observation.IsCompleted);
        Assert.Contains(TimeSpan.FromSeconds(sharedDeadline ? 240 : 180), clock.ActiveDueTimes);
        Assert.Contains(TimeSpan.FromSeconds(1500), clock.ActiveDueTimes);

        clock.SetTime(scheduledSecond);
        Task watchPolled = clock.ExpectNextTimer();
        response.SetResult(new(0, Event("ADDED", "scheduled", "producer-pod", "uid-1"), string.Empty));
        // WatchLoop schedules its next poll only after the real parser records the event.
        await watchPolled.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(new ProducerPod("producer-pod", "uid-1"), admission.Producer);
        clock.FireDueTimers();

        if (accepted)
            Assert.Equal(admission.Producer, await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        else
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal(sharedDeadline && !accepted, parent.IsCancellationRequested);
        Assert.Equal(1, fixture.Process.InitialLists);
        Assert.Equal(2, fixture.Process.WatchRequests.Count);
        Assert.All(fixture.Process.WatchRequests, request => AssertRawRequest(request, fixture));
        Assert.Contains(TimeSpan.FromSeconds(1500), clock.ActiveDueTimes);
    }

    [Fact]
    public async Task Given_NoProducerAndNoParentDeadline_When_LocalAdmissionDeadlineExpires_Then_ObservationIsIndependentlyBounded()
    {
        var clock = new ProducerObservationClock();
        var waiting = new Step(async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("watch_fixture_must_be_cancelled");
        });
        using var fixture = new WatchFixture(clock, Step.Return(Event("BOOKMARK", "initial")), waiting);
        await fixture.BindAsync();
        PodAdmission admission = await fixture.Cluster.EstablishProducerWatchAsync(fixture.Context, TestContext.Current.CancellationToken);
        Task<ProducerPod> observation = fixture.Cluster.ObserveProducerAsync(fixture.Context, admission, TestContext.Current.CancellationToken);

        clock.SetTime(180);
        clock.FireDueTimers();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.All(admission.Events, item => Assert.Null(item.Pod));
        Assert.Equal(2, fixture.Process.WatchRequests.Count);
        Assert.Contains(TimeSpan.FromSeconds(1500), clock.ActiveDueTimes);
    }

    [Fact]
    public async Task Given_RecordedProducerAndCancelledParent_When_Observed_Then_IdentityCannotBypassCancellation()
    {
        var clock = new ProducerObservationClock();
        var waiting = new Step(async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("watch_fixture_must_be_cancelled");
        });
        using var fixture = new WatchFixture(clock, Step.Return(Event("ADDED", "initial", "producer-pod", "uid-1")), waiting);
        await fixture.BindAsync();
        PodAdmission admission = await fixture.Cluster.EstablishProducerWatchAsync(fixture.Context, TestContext.Current.CancellationToken);
        using var parent = new CancellationTokenSource();
        parent.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Cluster.ObserveProducerAsync(fixture.Context, admission, parent.Token));

        Assert.Equal(2, fixture.Process.WatchRequests.Count);
        Assert.Equal(new ProducerPod("producer-pod", "uid-1"), admission.Producer);
    }

    private sealed class ProducerObservationClock : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<ObservationTimer> timers = [];
        private long timestamp;
        private TaskCompletionSource? nextTimer;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() { lock (gate) return timestamp; }
        internal TimeSpan[] ActiveDueTimes
        {
            get { lock (gate) return timers.Where(timer => timer.Due != long.MaxValue).Select(timer => TimeSpan.FromTicks(timer.Due)).ToArray(); }
        }
        internal void SetTime(int seconds) { lock (gate) timestamp = TimeSpan.FromSeconds(seconds).Ticks; }
        internal Task ExpectNextTimer()
        {
            lock (gate)
            {
                Assert.Null(nextTimer);
                nextTimer = new(TaskCreationOptions.RunContinuationsAsynchronously);
                return nextTimer.Task;
            }
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.Equal(Timeout.InfiniteTimeSpan, period);
            lock (gate)
            {
                var timer = new ObservationTimer(this, callback, state);
                timer.Change(dueTime, period);
                timers.Add(timer);
                nextTimer?.TrySetResult();
                nextTimer = null;
                return timer;
            }
        }
        internal void FireDueTimers()
        {
            ObservationTimer[] due;
            // Deliver expired deadlines before delayed polling callbacks, making equality deterministic.
            lock (gate) due = timers.Where(timer => timer.Due <= timestamp).OrderByDescending(timer => timer.Due).ToArray();
            foreach (ObservationTimer timer in due) timer.Fire();
        }
        private sealed class ObservationTimer(ProducerObservationClock clock, TimerCallback callback, object? state) : ITimer
        {
            internal long Due { get; private set; } = long.MaxValue;
            private bool disposed;
            internal void Fire()
            {
                lock (clock.gate)
                {
                    if (disposed || Due == long.MaxValue) return;
                    Due = long.MaxValue;
                }
                callback(state);
            }
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (clock.gate)
                {
                    if (disposed) return false;
                    Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : checked(clock.timestamp + dueTime.Ticks);
                    return true;
                }
            }
            public void Dispose() { lock (clock.gate) { disposed = true; Due = long.MaxValue; } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
#pragma warning restore CA1707
