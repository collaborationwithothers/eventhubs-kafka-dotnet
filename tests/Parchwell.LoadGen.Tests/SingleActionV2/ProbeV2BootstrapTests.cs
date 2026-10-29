using System.Reflection;
using Confluent.Kafka;
using Parchwell.LoadGen.SingleActionV2;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleActionV2;

#pragma warning disable CA1707
public sealed class ProbeV2BootstrapTests
{
    [Theory]
    [InlineData("installed", true, 2)]
    [InlineData("fatal", false, 1)]
    [InlineData("deadline", false, 2)]
    [InlineData("at_deadline", false, 2)]
    public void Given_UnassignedConsumer_When_Bootstrapping_Then_PollsWithinOriginalBudgetBeforeMetadata(string mode, bool ready, int polls)
    {
        var clock = new TestClock();
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), clock, 0, 150);
        IConsumer<byte[], byte[]> consumer = DispatchProxy.Create<IConsumer<byte[], byte[]>, NativeConsumer>();
        var proxy = (NativeConsumer)(object)consumer;
        proxy.Clock = clock;
        proxy.Capture = capture;
        proxy.Mode = mode;
        var request = new ProbeV2Requested("document-events", "explicit", 30, 4, "aw==", "dg==");
        bool result = ProbeV2Worker.Bootstrap(consumer, () => proxy.Installed, capture, request, 100, 150, TestContext.Current.CancellationToken);
        Assert.Equal(ready, result);
        Assert.Equal(polls, proxy.Polls.Count);
        Assert.All(proxy.Polls, value => Assert.InRange(value.TotalMilliseconds, 1, 100));
        if (mode != "fatal") Assert.Equal(TimeSpan.FromMilliseconds(50), proxy.Polls[1]);
        Assert.Equal(0, proxy.AssignCalls);
        Assert.Equal(0, proxy.SubscribeCalls);
    }
    public class NativeConsumer : DispatchProxy
    {
        internal TestClock Clock = null!;
        internal ProbeV2Capture Capture = null!;
        internal string Mode = "";
        internal bool Installed;
        internal List<TimeSpan> Polls { get; } = [];
        internal int AssignCalls, SubscribeCalls;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "Consume":
                    var timeout = (TimeSpan)args![0]!;
                    Polls.Add(timeout);
                    Clock.Milliseconds += Mode == "installed" && Polls.Count == 2 ? 1 : (long)timeout.TotalMilliseconds;
                    if (Mode == "fatal") Capture.Error(new Error(ErrorCode.Local_Fail, "synthetic", true));
                    if (Mode is "installed" or "at_deadline" && Polls.Count == 2) Installed = true;
                    return null;
                case "Assign": AssignCalls++; return null;
                case "Subscribe": SubscribeCalls++; return null;
                default: throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
