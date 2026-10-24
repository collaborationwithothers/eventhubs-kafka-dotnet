using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Judge;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

#pragma warning disable CA1707
public sealed class PhaseJudgeTests
{
    [Fact]
    public void Given_FiveCompleteNoSampleRepeats_When_Judged_Then_GateIsUsableAndUntestable()
    {
        PhaseJudgement result=PhaseJudge.Evaluate("probe",Enumerable.Range(1,5).Select(i=>Complete(i,new(true,false,null,null,null,null))).ToArray());
        Assert.Equal("complete_valid",result.Status);
        Assert.Equal("untestable_at_build_scale",result.Gate);
        Assert.Equal(new OutcomeCounts(5,10,15,20,25,30),result.Counts);
    }

    [Fact]
    public void Given_FiveQualifiedProbeRepeats_When_Judged_Then_GatePasses()
    {
        PhaseJudgement result=PhaseJudge.Evaluate("probe",Enumerable.Range(1,5).Select(i=>Complete(i,new(true,true,"b",0,1,20_001))).ToArray());
        Assert.Equal("pass",result.Gate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void Given_FreshInventoryCoverageIsNotPassing_When_Judged_Then_ProbeIsInconclusive(bool? coverage)
    {
        PhaseRepeatEvidence[] repeats=Enumerable.Range(1,5).Select(i=>Complete(i,new(true,true,"b",0,1,20_001))).ToArray();
        repeats[3]=repeats[3] with{InventoryCoveragePass=coverage};
        PhaseJudgement result=PhaseJudge.Evaluate("probe",repeats);
        Assert.Equal("inconclusive",result.Gate);
        Assert.Null(result.Counts);
    }

    [Fact]
    public void Given_OnlyFourExpectedRepeats_When_Judged_Then_RequiresAllFive()
    {
        PhaseJudgement result=PhaseJudge.Evaluate("C0",Enumerable.Range(1,4).Select(i=>Complete(i,null)).ToArray());
        Assert.Equal("incomplete",result.Status); Assert.Null(result.Counts);
        Assert.Contains("j1_repeat_slots",result.Reasons);
    }

    [Fact]
    public void GivenRepeatBecomesInvalidAfterCountsWereRead_WhenJudged_ThenOmitsCountsButKeepsIndependentProbeEvidence()
    {
        PhaseRepeatEvidence[] repeats=Enumerable.Range(1,5).Select(i=>Complete(i,new(true,true,"broker",0,1,20_001))).ToArray();
        repeats[2]=repeats[2] with{KnownInvalid=true};
        PhaseJudgement result=PhaseJudge.Evaluate("probe",repeats);
        Assert.Equal("invalid",result.Repeats[2].Status);
        Assert.Null(result.Repeats[2].Counts);
        Assert.Equal("broker",result.Repeats[2].ProbeObservation?.Broker);
        Assert.Null(result.Counts);
    }

    [Fact]
    public void GivenPhaseCountSumOverflowsInt64_WhenJudged_ThenOmitsAllTotals()
    {
        PhaseRepeatEvidence[] repeats=Enumerable.Range(1,5).Select(i=>Complete(i,null) with
        {
            Counts=new(long.MaxValue,2,3,4,5,6)
        }).ToArray();

        PhaseJudgement result=PhaseJudge.Evaluate("C0",repeats);

        Assert.Null(result.Counts);
        Assert.Equal("incomplete",result.Status);
        Assert.Contains("j7_counts_unavailable",result.Reasons);
    }

    private static PhaseRepeatEvidence Complete(int repeat,ProbeRepeatObservation? probe)=>new(repeat,true,true,true,true,true,true,true,false,false,new(1,2,3,4,5,6),probe);
}
#pragma warning restore CA1707
