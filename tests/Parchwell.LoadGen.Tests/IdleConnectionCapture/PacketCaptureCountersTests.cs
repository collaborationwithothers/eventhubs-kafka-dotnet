using Parchwell.LoadGen.IdleConnectionCapture;
using Xunit;

namespace Parchwell.LoadGen.Tests.IdleConnectionCapture;

#pragma warning disable CA1707
// Synthetic samples protect arithmetic and terminal-state accounting without claiming native-read success.
public sealed class PacketCaptureCountersTests
{
    [Fact]
    public void Given_DefaultCounters_When_Constructed_Then_StartsActiveWithoutSubmittedSamples()
    {
        var counters = new PacketCaptureCounters();
        AssertActive(counters, 0, 0, 0);
    }

    [Theory]
    [InlineData(1UL, 1UL, 1UL)]
    [InlineData(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue)]
    public void Given_PositiveCeilings_When_Constructing_Then_StartsActiveWithoutSubmittedSamples(
        ulong maximumPackets, ulong maximumDrops, ulong maximumSamples)
    {
        var counters = new PacketCaptureCounters(maximumPackets, maximumDrops, maximumSamples);
        AssertActive(counters, 0, 0, 0);
    }

    [Theory]
    [InlineData(0UL, 1UL, 1UL, "maximumPackets")]
    [InlineData(1UL, 0UL, 1UL, "maximumDrops")]
    [InlineData(1UL, 1UL, 0UL, "maximumSamples")]
    public void Given_ZeroCeiling_When_Constructing_Then_ThrowsBeforeUse(
        ulong maximumPackets, ulong maximumDrops, ulong maximumSamples, string parameterName) =>
        Assert.Throws<ArgumentOutOfRangeException>(parameterName,
            () => new PacketCaptureCounters(maximumPackets, maximumDrops, maximumSamples));

    [Fact]
    public void Given_IndependentSamples_When_AddingAndCompleting_Then_SumsEachFieldOnce()
    {
        var counters = new PacketCaptureCounters();
        Assert.True(counters.TryAddSample(10, 2));
        AssertActive(counters, 10, 2, 1);
        Assert.True(counters.TryAddSample(3, 1));
        AssertActive(counters, 13, 3, 2);
        Assert.True(counters.TryAddSample(0, 0));
        AssertActive(counters, 13, 3, 3);
        Assert.True(counters.TryComplete(2, 0));
        AssertCompleted(counters, 15, 3, 4);
    }

    [Fact]
    public void Given_IdenticalSamples_When_AddingTwice_Then_DoesNotDeduplicate()
    {
        var counters = new PacketCaptureCounters();
        Assert.True(counters.TryAddSample(7, 1));
        AssertActive(counters, 7, 1, 1);
        Assert.True(counters.TryAddSample(7, 1));
        AssertActive(counters, 14, 2, 2);
    }

    [Fact]
    public void Given_MoreDropsThanPackets_When_AddingSample_Then_RecordsIndependentFields()
    {
        var counters = new PacketCaptureCounters();
        Assert.True(counters.TryAddSample(0, 1));
        AssertActive(counters, 0, 1, 1);
    }

    [Fact]
    public void Given_MaximumUnsignedSamples_When_AddingTwiceAndCompleting_Then_PreservesTotalsBeyond32Bits()
    {
        var counters = new PacketCaptureCounters();
        Assert.True(counters.TryAddSample(uint.MaxValue, uint.MaxValue));
        AssertActive(counters, 4294967295, 4294967295, 1);
        Assert.True(counters.TryAddSample(uint.MaxValue, uint.MaxValue));
        AssertActive(counters, 8589934590, 8589934590, 2);
        Assert.True(counters.TryComplete(0, 0));
        AssertCompleted(counters, 8589934590, 8589934590, 3);
    }

    [Fact]
    public void Given_ZeroSamples_When_AddingAndCompleting_Then_CountsBothSubmissions()
    {
        var counters = new PacketCaptureCounters();
        Assert.True(counters.TryAddSample(0, 0));
        AssertActive(counters, 0, 0, 1);
        Assert.True(counters.TryComplete(0, 0));
        AssertCompleted(counters, 0, 0, 2);
    }

    [Fact]
    public void Given_PacketCeiling_When_AddingBelowAtAndAbove_Then_RejectsWholeOverflowingSample()
    {
        var counters = new PacketCaptureCounters(3, 10, 10);
        Assert.True(counters.TryAddSample(2, 1));
        AssertActive(counters, 2, 1, 1);
        Assert.True(counters.TryAddSample(1, 1));
        AssertActive(counters, 3, 2, 2);
        Assert.False(counters.TryAddSample(1, 1));
        AssertTerminal(counters, "packet_counter_overflow", 3, 2, 2);
    }

    [Fact]
    public void Given_DropCeiling_When_AddingBelowAtAndAbove_Then_RejectsWholeOverflowingSample()
    {
        var counters = new PacketCaptureCounters(10, 3, 10);
        Assert.True(counters.TryAddSample(1, 2));
        AssertActive(counters, 1, 2, 1);
        Assert.True(counters.TryAddSample(1, 1));
        AssertActive(counters, 2, 3, 2);
        Assert.False(counters.TryAddSample(1, 1));
        AssertTerminal(counters, "packet_counter_overflow", 2, 3, 2);
    }

    [Fact]
    public void Given_SampleCeiling_When_AddingBelowAtAndAbove_Then_CountsZeroSamplesUntilFull()
    {
        var counters = new PacketCaptureCounters(10, 10, 3);
        Assert.True(counters.TryAddSample(0, 0));
        AssertActive(counters, 0, 0, 1);
        Assert.True(counters.TryAddSample(0, 0));
        AssertActive(counters, 0, 0, 2);
        Assert.True(counters.TryAddSample(0, 0));
        AssertActive(counters, 0, 0, 3);
        Assert.False(counters.TryAddSample(0, 0));
        AssertTerminal(counters, "packet_counter_overflow", 0, 0, 3);
    }

    [Theory]
    [InlineData(3UL, 10UL, 10UL, 2U, 1U, 2U, 1U)]
    [InlineData(10UL, 3UL, 10UL, 1U, 2U, 1U, 2U)]
    [InlineData(10UL, 10UL, 1UL, 1U, 1U, 0U, 0U)]
    public void Given_AcceptedSample_When_FinalSampleExceedsOneCeiling_Then_PreservesAllPriorTotals(
        ulong maximumPackets, ulong maximumDrops, ulong maximumSamples,
        uint packets, uint drops, uint finalPackets, uint finalDrops)
    {
        var counters = new PacketCaptureCounters(maximumPackets, maximumDrops, maximumSamples);
        Assert.True(counters.TryAddSample(packets, drops));
        AssertActive(counters, packets, drops, 1);
        Assert.False(counters.TryComplete(finalPackets, finalDrops));
        AssertTerminal(counters, "packet_counter_overflow", packets, drops, 1);
    }

    [Fact]
    public void Given_MinimumCeilings_When_FinalSampleReachesAllCeilings_Then_CompletesWithDrops()
    {
        var counters = new PacketCaptureCounters(1, 1, 1);
        Assert.True(counters.TryComplete(1, 1));
        AssertCompleted(counters, 1, 1, 1);
    }

    [Theory]
    [InlineData(2U, 0U)] [InlineData(0U, 2U)]
    public void Given_MinimumCeilings_When_FirstFinalSampleExceedsCeiling_Then_RetainsNoSamples(uint packets, uint drops)
    {
        var counters = new PacketCaptureCounters(1, 1, 1);
        Assert.False(counters.TryComplete(packets, drops));
        AssertTerminal(counters, "packet_counter_overflow", 0, 0, 0);
    }

    [Fact]
    public void Given_NoPriorSample_When_CompletingWithZero_Then_CountsExplicitFinalSample()
    {
        var counters = new PacketCaptureCounters();
        Assert.True(counters.TryComplete(0, 0));
        AssertCompleted(counters, 0, 0, 1);
    }

    [Theory]
    [InlineData("add")] [InlineData("complete")] [InlineData("read-failed")]
    public void Given_CompletedCountersAtCeilings_When_SubmittingAgain_Then_StateFailurePrecedesOverflow(string operation)
    {
        var counters = new PacketCaptureCounters(1, 1, 1);
        Assert.True(counters.TryComplete(1, 1));
        AssertCompleted(counters, 1, 1, 1);
        switch (operation)
        {
            case "add": Assert.False(counters.TryAddSample(1, 1)); break;
            case "complete": Assert.False(counters.TryComplete(0, 0)); break;
            case "read-failed": counters.FailRead(); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
        AssertTerminal(counters, "packet_counter_state_invalid", 1, 1, 1);
    }

    [Fact]
    public void Given_CompletedCounters_When_AbortingRepeatedly_Then_PreservesCompletionAndTotals()
    {
        var counters = new PacketCaptureCounters();
        Assert.True(counters.TryComplete(1, 1));
        AssertCompleted(counters, 1, 1, 1);
        counters.Abort();
        AssertCompleted(counters, 1, 1, 1);
        counters.Abort();
        AssertCompleted(counters, 1, 1, 1);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_ActiveCounters_When_ReadFails_Then_PreservesAcceptedSamplesWithoutInventingZero(bool populated)
    {
        var counters = new PacketCaptureCounters();
        if (populated) Assert.True(counters.TryAddSample(5, 2));
        counters.FailRead();
        AssertTerminal(counters, "packet_counter_read_failed", populated ? 5UL : 0UL,
            populated ? 2UL : 0UL, populated ? 1UL : 0UL);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_ActiveCounters_When_Aborting_Then_PreservesAcceptedSamplesAndFirstFailure(bool populated)
    {
        var counters = new PacketCaptureCounters();
        if (populated) Assert.True(counters.TryAddSample(5, 2));
        counters.Abort();
        AssertTerminal(counters, "packet_counter_abandoned", populated ? 5UL : 0UL,
            populated ? 2UL : 0UL, populated ? 1UL : 0UL);
    }

    [Theory]
    [InlineData("packet_counter_state_invalid")]
    [InlineData("packet_counter_overflow")]
    [InlineData("packet_counter_read_failed")]
    [InlineData("packet_counter_abandoned")]
    public void Given_FirstFailureAtCeilings_When_OperatingAgain_Then_FailurePrecedesCapacityAndNeverChanges(string failure)
    {
        var counters = new PacketCaptureCounters(1, 1, 1);
        if (failure == "packet_counter_state_invalid")
        {
            Assert.True(counters.TryComplete(1, 1));
            Assert.False(counters.TryAddSample(0, 0));
        }
        else
        {
            Assert.True(counters.TryAddSample(1, 1));
            switch (failure)
            {
                case "packet_counter_overflow": Assert.False(counters.TryAddSample(0, 0)); break;
                case "packet_counter_read_failed": counters.FailRead(); break;
                case "packet_counter_abandoned": counters.Abort(); break;
                default: throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }
        AssertTerminal(counters, failure, 1, 1, 1);
    }

    [Fact]
    public void Given_257Samples_When_Completing_Then_AccumulatesBeyondQueueCapacity()
    {
        var counters = new PacketCaptureCounters();
        for (int n = 0; n < 257; n++) Assert.True(counters.TryAddSample(1, 0));
        AssertActive(counters, 257, 0, 257);
        Assert.True(counters.TryComplete(1, 0));
        AssertCompleted(counters, 258, 0, 258);
    }

    private static void AssertActive(PacketCaptureCounters counters, ulong packets, ulong drops, ulong samples)
    {
        Assert.Equal(PacketCaptureCounterState.Active, counters.State);
        Assert.Null(counters.FailureCode);
        AssertTotals(counters, packets, drops, samples);
    }

    private static void AssertCompleted(PacketCaptureCounters counters, ulong packets, ulong drops, ulong samples)
    {
        Assert.Equal(PacketCaptureCounterState.Completed, counters.State);
        Assert.Null(counters.FailureCode);
        AssertTotals(counters, packets, drops, samples);
    }

    private static void AssertFaulted(PacketCaptureCounters counters, string failure, ulong packets, ulong drops, ulong samples)
    {
        Assert.Equal(PacketCaptureCounterState.Faulted, counters.State);
        Assert.Equal(failure, counters.FailureCode);
        AssertTotals(counters, packets, drops, samples);
    }

    private static void AssertTerminal(PacketCaptureCounters counters, string failure, ulong packets, ulong drops, ulong samples)
    {
        AssertFaulted(counters, failure, packets, drops, samples);
        Assert.False(counters.TryAddSample(0, 0));
        AssertFaulted(counters, failure, packets, drops, samples);
        Assert.False(counters.TryComplete(0, 0));
        AssertFaulted(counters, failure, packets, drops, samples);
        Assert.False(counters.TryAddSample(uint.MaxValue, uint.MaxValue));
        AssertFaulted(counters, failure, packets, drops, samples);
        Assert.False(counters.TryComplete(uint.MaxValue, uint.MaxValue));
        AssertFaulted(counters, failure, packets, drops, samples);
        counters.FailRead();
        AssertFaulted(counters, failure, packets, drops, samples);
        counters.Abort();
        AssertFaulted(counters, failure, packets, drops, samples);
        counters.Abort();
        AssertFaulted(counters, failure, packets, drops, samples);
    }

    private static void AssertTotals(PacketCaptureCounters counters, ulong packets, ulong drops, ulong samples)
    {
        Assert.Equal(packets, counters.PacketTotal);
        Assert.Equal(drops, counters.DropTotal);
        Assert.Equal(samples, counters.SampleCount);
    }
}
#pragma warning restore CA1707
