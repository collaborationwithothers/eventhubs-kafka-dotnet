using System.Globalization;
using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptProducerPolicyTests
{
    [Theory]
    [InlineData("same_instance")]
    [InlineData("cross_restart")]
    public async Task Given_FirstOriginalSetupTakes50Milliseconds_When_InvokingOriginals_Then_OffsetsStartAtItsNativeInvocation(string arm)
    {
        var clock = new PacingClock();
        await using var fixture = new B10AbruptProducerTests.Fixture(clock: clock);
        fixture.Initialize();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        fixture.Observe();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        fixture.FlushHook = () =>
        {
            if (fixture.Journal.Frames.Count == 2) clock.Advance(TimeSpan.FromMilliseconds(50));
            return Task.CompletedTask;
        };
        var invocations = new List<long>();
        fixture.Native.OnProduce = (_, _) => invocations.Add(clock.GetTimestamp());

        await InvokeOriginalsAsync(fixture.Runner, arm);

        Assert.Equal(TimeSpan.FromMilliseconds(52).Ticks, invocations[0]);
        AssertInvocationOffsets(invocations);
        Assert.Equal([0, 1, 2, 3, 4], fixture.Journal.Frames.Where(frame => frame.Kind == "prepared")
            .Select(frame => frame.Data.GetProperty("ordinal").GetInt32()));
    }

    [Theory]
    [InlineData("same_instance", true)]
    [InlineData("cross_restart", true)]
    [InlineData("same_instance", false)]
    [InlineData("cross_restart", false)]
    public async Task Given_EarlyOrQuantizedTimerWake_When_InvokingOriginals_Then_EachNativeInvocationHonorsItsExactLowerBound(string arm, bool earlyWake)
    {
        var clock = new PacingClock(earlyWake);
        await using var fixture = new B10AbruptProducerTests.Fixture(clock: clock);
        fixture.Initialize();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        fixture.Observe();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var invocations = new List<long>();
        fixture.Native.OnProduce = (_, _) => invocations.Add(clock.GetTimestamp());
        fixture.FlushHook = () =>
        {
            if (fixture.Journal.Frames.Count == 4) clock.Advance(TimeSpan.FromMicroseconds(250));
            return Task.CompletedTask;
        };

        await InvokeOriginalsAsync(fixture.Runner, arm);

        AssertInvocationOffsets(invocations);
        Assert.True(clock.TimerCalls >= 4);
    }

    [Theory]
    [InlineData("original", 0)]
    [InlineData("original", 1)]
    [InlineData("original", 2)]
    [InlineData("original", 3)]
    [InlineData("resend", 4)]
    public async Task Given_DurableSynchronousRejection_When_OtherControlAttemptsFinish_Then_ControlCompletesAndKillIsAdmitted(string rejectedRole, int rejectedOrdinal)
    {
        await using var fixture = new B10AbruptProducerTests.Fixture();
        Action<DeliveryReport<byte[], byte[]>>? pendingOriginal = null;
        string? rejectedId = null;
        fixture.Native.OnProduce = (message, callback) =>
        {
            if (!Encoding.UTF8.GetString(message.Key).EndsWith("same_instance", StringComparison.Ordinal)) return;
            var prepared = fixture.Journal.Frames.Last(frame => frame.Kind == "prepared").Data;
            string role = prepared.GetProperty("role").GetString()!;
            int ordinal = int.Parse(Encoding.UTF8.GetString(message.Headers.GetLastBytes("sequence")), CultureInfo.InvariantCulture);
            if (role == "resend")
            {
                Assert.NotNull(pendingOriginal);
                pendingOriginal(B10AbruptProducerTests.Report());
                fixture.Journal.DrainAvailable().GetAwaiter().GetResult();
            }
            if (role == rejectedRole && ordinal == rejectedOrdinal)
            {
                rejectedId = prepared.GetProperty("attempt_id").GetString();
                throw new ProduceException<byte[], byte[]>(new Error(ErrorCode.Local_QueueFull), new DeliveryResult<byte[], byte[]>());
            }
            if (role == "original" && ordinal == 4) { pendingOriginal = callback; return; }
            callback(B10AbruptProducerTests.Report());
            fixture.Journal.DrainAvailable().GetAwaiter().GetResult();
        };
        fixture.StartReports();

        await Assert.ThrowsAsync<B10AbruptProducerTests.SimulatedDeathException>(() => fixture.Runner.RunFirstAsync(
            B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));

        Assert.NotNull(rejectedId);
        var rejection = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "synchronous_rejection");
        Assert.Equal(rejectedId, rejection.Data.GetProperty("attempt_id").GetString());
        Assert.Equal((int)ErrorCode.Local_QueueFull, rejection.Data.GetProperty("error_code").GetInt32());
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind is "accepted" or "terminal"
            && frame.Data.GetProperty("attempt_id").GetString() == rejectedId);
        var cut = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "cut");
        string selectedId = Assert.Single(cut.Data.GetProperty("unconfirmed_attempt_ids").EnumerateArray()).GetString()!;
        var resend = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "prepared" && frame.Data.GetProperty("role").GetString() == "resend");
        Assert.Equal(selectedId, resend.Data.GetProperty("predecessor_attempt_id").GetString());
        Assert.Equal(4, resend.Data.GetProperty("ordinal").GetInt32());
        Assert.Contains(fixture.Journal.Frames, frame => frame.Kind == "terminal" && frame.Data.GetProperty("attempt_id").GetString() == selectedId);
        Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "post_drain_report");
        Assert.Equal("kill_intent", fixture.Journal.Frames[^1].Kind);
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "no_kill");
        Assert.Equal(11, fixture.Native.Calls.Count);
        Assert.Equal(0, fixture.Native.Disposals);
        Assert.Equal(0, fixture.Native.FlushAfterKill);
    }

    private static Task InvokeOriginalsAsync(B10AbruptProducer runner, string arm) =>
        (Task)typeof(B10AbruptProducer).GetMethod("OriginalsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(runner, [arm, TestContext.Current.CancellationToken])!;

    private static void AssertInvocationOffsets(List<long> invocations)
    {
        Assert.Equal(5, invocations.Count);
        Assert.True(invocations[1] - invocations[0] >= TimeSpan.FromMilliseconds(10).Ticks);
        Assert.True(invocations[2] - invocations[0] >= TimeSpan.FromMilliseconds(20).Ticks);
        Assert.True(invocations[3] - invocations[0] >= TimeSpan.FromMilliseconds(30).Ticks);
        Assert.True(invocations[4] - invocations[0] >= TimeSpan.FromMilliseconds(40).Ticks);
    }

    private sealed class PacingClock(bool earlyWake = false) : TimeProvider
    {
        private long ticks;
        internal int TimerCalls { get; private set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero).AddTicks(ticks);
        internal void Advance(TimeSpan elapsed) => ticks += elapsed.Ticks;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            TimerCalls++;
            Advance(earlyWake ? dueTime - TimeSpan.FromMicroseconds(100) : dueTime);
            callback(state);
            return new CompletedTimer();
        }
        private sealed class CompletedTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => false;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
