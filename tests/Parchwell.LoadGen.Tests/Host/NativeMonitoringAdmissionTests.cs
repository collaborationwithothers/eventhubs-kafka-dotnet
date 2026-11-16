using Parchwell.LoadGen.IdleObservation;
using Parchwell.LoadGen.SingleAction;
using Parchwell.LoadGen.SingleActionV2;
using Parchwell.LoadGen.Tests.IdleObservation;
using Parchwell.LoadGen.Tests.SingleAction;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707
public sealed class NativeMonitoringAdmissionTests
{
    [Fact]
    public void SingleAction_ReturnsOnlyRetainedSequences()
    {
        var capture = new SingleActionCapture(SingleActionCaptureTests.Options(), TimeProvider.System,
            TimeProvider.System.GetTimestamp(), new(100));
        Assert.True(capture.TryStatistics("{}", out long first));
        capture.Drain();
        Assert.Equal(1, first);
        Assert.False(capture.TryStatistics("\ud800", out long rejected));
        Assert.Equal(0, rejected);
        Assert.Equal(first, Assert.Single(capture.Snapshot().Statistics).Sequence);
    }

    [Fact]
    public void IdleObservation_ReturnsOnlyRetainedSequences()
    {
        var capture = new IdleObservationCapture(IdleObservationOptionsTests.Options(), TimeProvider.System,
            TimeProvider.System.GetTimestamp(), 1, new IdleObservationClientLogBudget(1));
        Assert.True(capture.TryStatistics("{}", out long first));
        capture.Drain();
        Assert.False(capture.TryStatistics("{}", out long rejected));
        Assert.Equal(0, rejected);
        Assert.Equal(first, Assert.Single(capture.Snapshot().Statistics).Sequence);
    }

    [Fact]
    public void ProbeV2_ReturnsTheSourceSequenceAfterRejectedCallbacks()
    {
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), TimeProvider.System,
            TimeProvider.System.GetTimestamp(), 0);
        Assert.False(capture.TryStatistics("before", out long rejected));
        Assert.Equal(0, rejected);
        capture.NoOutcome();
        Assert.True(capture.TryStatistics("{}", out long accepted));
        Assert.True(accepted > 1);
        Assert.False(capture.TryStatistics("later", out _));
    }
}
