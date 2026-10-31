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
    public void Given_SixthRepeatSlot_When_Judged_Then_RejectsExtraSlot()
    {
        PhaseRepeatEvidence[] evidence=Enumerable.Range(1,6).Select(i=>Complete(i,null)).ToArray();
        PhaseJudgement result=PhaseJudge.Evaluate("C0",evidence);
        Assert.Equal("incomplete",result.Status);
        Assert.All(result.Repeats,repeat=>Assert.False(repeat.Checks.J1));
        Assert.Contains("j1_repeat_slots",result.Reasons);
    }

    [Fact]
    public void Given_ForeignAndDuplicateRepeatSlots_When_Judged_Then_RejectsAffectedSlots()
    {
        PhaseRepeatEvidence[] valid=Enumerable.Range(1,5).Select(i=>Complete(i,null)).ToArray();
        PhaseJudgement foreign=PhaseJudge.Evaluate("C0",valid.Append(Complete(0,null)).ToArray());
        Assert.All(foreign.Repeats,repeat=>Assert.False(repeat.Checks.J1));
        PhaseJudgement duplicate=PhaseJudge.Evaluate("C0",valid.Append(Complete(3,null)).ToArray());
        Assert.Equal("incomplete",duplicate.Repeats[2].Status);
        Assert.False(duplicate.Repeats[2].Checks.J1);
        Assert.Contains("j1_repeat_slots",duplicate.Reasons);
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

    [Fact]
    public void Given_FiveAuthorizedBrokerCounts_When_Judged_Then_PreservesEachClientMeasurementWithoutChangingTotals()
    {
        PhaseRepeatEvidence[] evidence = MeasuredRepeats();

        PhaseJudgement result = PhaseJudge.Evaluate("C0", evidence);

        Assert.Equal("complete_valid", result.Status);
        Assert.Equal(new OutcomeCounts(5, 10, 15, 20, 25, 30), result.Counts);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(new BrokerConnectionCount("LoadGenProducer", $"client-{i}", i + 1, i, true), result.Repeats[i - 1].BrokerCount);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("publication")]
    [InlineData("receipt")]
    [InlineData("job")]
    [InlineData("registration")]
    [InlineData("inventory")]
    public void Given_B04EvidenceAndFailedTrustPredicate_When_Judged_Then_OmitsOnlyUntrustedProjection(string predicate)
    {
        PhaseRepeatEvidence[] evidence=Enumerable.Range(1,5).Select(i=>Complete(i,null) with
        { B04Evidence=new([i],[new B04FatalError(i,"fatal")]) }).ToArray();
        evidence[2]=predicate switch
        {
            "start"=>evidence[2] with{StartLineAccepted=false},
            "publication"=>evidence[2] with{PublicationAccepted=false},
            "receipt"=>evidence[2] with{VerifierReceiptValid=false},
            "job"=>evidence[2] with{JobBindingPass=false},
            "registration"=>evidence[2] with{RegistrationCountPass=false},
            "inventory"=>evidence[2] with{InventoryCoveragePass=false},
            _=>throw new ArgumentOutOfRangeException(nameof(predicate))
        };
        PhaseJudgement result=PhaseJudge.Evaluate("C1",evidence);
        Assert.Null(result.Repeats[2].B04Evidence);
        Assert.Equal(evidence[0].B04Evidence,result.Repeats[0].B04Evidence);
    }

    [Theory]
    [InlineData("job")]
    [InlineData("registration")]
    [InlineData("inventory")]
    public void Given_NullableB04TrustPredicate_When_Judged_Then_OmitsProjection(string predicate)
    {
        PhaseRepeatEvidence[] evidence=Enumerable.Range(1,5).Select(i=>Complete(i,null) with { B04Evidence=new([i],[]) }).ToArray();
        evidence[0]=predicate switch
        {
            "job"=>evidence[0] with{JobBindingPass=null},
            "registration"=>evidence[0] with{RegistrationCountPass=null},
            "inventory"=>evidence[0] with{InventoryCoveragePass=null},
            _=>throw new ArgumentOutOfRangeException(nameof(predicate))
        };
        Assert.Null(PhaseJudge.Evaluate("C1",evidence).Repeats[0].B04Evidence);
    }

    [Fact]
    public void Given_KnownInvalidOrSlotOnlyIssue_When_Judged_Then_PreservesTrustedB04Evidence()
    {
        PhaseRepeatEvidence[] evidence=Enumerable.Range(1,5).Select(i=>Complete(i,null) with { B04Evidence=new([i],[]) }).ToArray();
        evidence[1]=evidence[1] with{KnownInvalid=true};
        evidence[2]=evidence[2] with{SlotPresent=false};
        PhaseJudgement result=PhaseJudge.Evaluate("C1",evidence);
        Assert.Equal(evidence[1].B04Evidence,result.Repeats[1].B04Evidence);
        Assert.Null(result.Repeats[1].Counts);
        Assert.Equal(evidence[2].B04Evidence,result.Repeats[2].B04Evidence);
        PhaseJudgement extra=PhaseJudge.Evaluate("C1",evidence.Append(Complete(6,null)).ToArray());
        Assert.Equal(evidence[0].B04Evidence,extra.Repeats[0].B04Evidence);
        PhaseJudgement duplicate=PhaseJudge.Evaluate("C1",evidence.Append(evidence[0]).ToArray());
        Assert.Null(duplicate.Repeats[0].B04Evidence);
    }

    [Fact]
    public void Given_MessageInvalidityWithAuthorizedBrokerCount_When_Judged_Then_RetainsMeasurementAndPassingProbe()
    {
        PhaseRepeatEvidence[] evidence = MeasuredRepeats();
        evidence[2] = evidence[2] with { KnownInvalid = true };

        PhaseJudgement result = PhaseJudge.Evaluate("probe", evidence);

        Assert.Equal("invalid", result.Status);
        Assert.Equal("invalid", result.Repeats[2].Status);
        Assert.Equal(evidence[2].BrokerCount, result.Repeats[2].BrokerCount);
        Assert.Null(result.Repeats[2].Counts);
        Assert.Null(result.Counts);
        Assert.Equal("pass", result.Gate);
    }

    [Theory]
    [InlineData("trusted-incomplete")]
    [InlineData("missing-slot")]
    public void Given_IncompleteRepeatWithAuthorizedBrokerCount_When_Judged_Then_RetainsIndependentMeasurement(string condition)
    {
        PhaseRepeatEvidence[] evidence = MeasuredRepeats();
        evidence[2] = condition == "trusted-incomplete"
            ? evidence[2] with { TrustedIncomplete = true }
            : evidence[2] with { SlotPresent = false };

        PhaseJudgement result = PhaseJudge.Evaluate("C0", evidence);

        Assert.Equal("incomplete", result.Status);
        Assert.Equal("incomplete", result.Repeats[2].Status);
        Assert.Equal(evidence[2].BrokerCount, result.Repeats[2].BrokerCount);
        Assert.Null(result.Repeats[2].Counts);
        Assert.Null(result.Counts);
    }

    [Theory]
    [InlineData("start-line", false, "incomplete")]
    [InlineData("publication", false, "incomplete")]
    [InlineData("receipt", false, "incomplete")]
    [InlineData("job", false, "invalid")]
    [InlineData("job", null, "incomplete")]
    [InlineData("registration", false, "invalid")]
    [InlineData("registration", null, "incomplete")]
    [InlineData("inventory", false, "invalid")]
    [InlineData("inventory", null, "incomplete")]
    public void Given_BrokerCountTrustPredicateNotPassing_When_Judged_Then_SuppressesMeasurement(string predicate, bool? pass, string status)
    {
        PhaseRepeatEvidence[] evidence = MeasuredRepeats();
        evidence[2] = predicate switch
        {
            "start-line" => evidence[2] with { StartLineAccepted = pass == true },
            "publication" => evidence[2] with { PublicationAccepted = pass == true },
            "receipt" => evidence[2] with { VerifierReceiptValid = pass == true },
            "job" => evidence[2] with { JobBindingPass = pass },
            "registration" => evidence[2] with { RegistrationCountPass = pass },
            "inventory" => evidence[2] with { InventoryCoveragePass = pass },
            _ => throw new ArgumentOutOfRangeException(nameof(predicate))
        };

        PhaseJudgement result = PhaseJudge.Evaluate("probe", evidence);

        Assert.Null(result.Repeats[2].BrokerCount);
        Assert.Equal(status, result.Status);
        Assert.Equal(status, result.Repeats[2].Status);
        Assert.Null(result.Counts);
        Assert.Null(result.Repeats[2].Counts);
        Assert.Equal("inconclusive", result.Gate);
    }

    [Fact]
    public void Given_ExtraOrDuplicateSlotsWithBrokerCounts_When_Judged_Then_OnlyUniqueIndependentMeasurementsRemain()
    {
        PhaseRepeatEvidence[] evidence = MeasuredRepeats();

        PhaseJudgement extra = PhaseJudge.Evaluate("C0", evidence.Append(Complete(6, null)).ToArray());
        Assert.Equal("incomplete", extra.Status);
        Assert.All(extra.Repeats, repeat => Assert.False(repeat.Checks.J1));
        for (int i = 0; i < 5; i++) Assert.Equal(evidence[i].BrokerCount, extra.Repeats[i].BrokerCount);
        Assert.Null(extra.Counts);

        PhaseJudgement duplicate = PhaseJudge.Evaluate("C0", evidence.Append(evidence[2]).ToArray());
        Assert.Equal("incomplete", duplicate.Status);
        Assert.False(duplicate.Repeats[2].Checks.J1);
        Assert.Null(duplicate.Repeats[2].BrokerCount);
        Assert.Equal(evidence[1].BrokerCount, duplicate.Repeats[1].BrokerCount);
        Assert.Null(duplicate.Counts);
    }

    [Fact]
    public void Given_AbsentAndZeroBrokerMeasurements_When_Judged_Then_DistinguishesMissingFromMeasuredZero()
    {
        PhaseRepeatEvidence[] evidence = MeasuredRepeats();
        evidence[0] = evidence[0] with { BrokerCount = null };
        evidence[1] = evidence[1] with { BrokerCount = new("LoadGenProducer", "client-zero", 0, 0, true) };

        PhaseJudgement result = PhaseJudge.Evaluate("C0", evidence);

        Assert.Null(result.Repeats[0].BrokerCount);
        Assert.Equal(new BrokerConnectionCount("LoadGenProducer", "client-zero", 0, 0, true), result.Repeats[1].BrokerCount);
        Assert.Equal("complete_valid", result.Status);
        Assert.Equal(new OutcomeCounts(5, 10, 15, 20, 25, 30), result.Counts);
    }

    [Theory]
    [InlineData("C0", "none", "complete_valid", null, "")]
    [InlineData("probe", "none", "complete_valid", "pass", "")]
    [InlineData("C0", "duplicate-3", "incomplete", null, "j1_repeat_slots,j2_publication_unaccepted,j3_verifier_receipt,j4_producer_job_binding,j5_registered_producer_count,j6_broker_inventory_coverage,j7_counts_unavailable")]
    [InlineData("probe", "inventory-false-4", "invalid", "inconclusive", "j6_broker_inventory_coverage,j7_counts_unavailable,j8_probe_inconclusive")]
    [InlineData("C0", "known-invalid-2", "invalid", null, "j7_counts_unavailable,trusted_result_invalid")]
    [InlineData("C0", "counts-null-5", "complete_valid", null, "j7_counts_unavailable")]
    [InlineData("probe", "qualifying-1-only", "complete_valid", "pass", "")]
    public void Given_PhaseEvidenceCase_When_Judged_Then_ReturnsExactStatusGateAndReasons(string phase, string change, string status, string? gate, string reasons)
    {
        PhaseRepeatEvidence[] evidence=Enumerable.Range(1,5).Select(i=>Complete(i,new(true,true,"broker",0,1,20_001))).ToArray();
        evidence=change switch
        {
            "none"=>evidence,
            "duplicate-3"=>evidence.Append(evidence[2]).ToArray(),
            "inventory-false-4"=>evidence.Select(e=>e.Repeat==4?e with{InventoryCoveragePass=false}:e).ToArray(),
            "known-invalid-2"=>evidence.Select(e=>e.Repeat==2?e with{KnownInvalid=true}:e).ToArray(),
            "counts-null-5"=>evidence.Select(e=>e.Repeat==5?e with{Counts=null}:e).ToArray(),
            "qualifying-1-only"=>evidence.Select(e=>e.Repeat==1?e:e with{ProbeObservation=new(true,false,null,null,null,null)}).ToArray(),
            _=>throw new ArgumentOutOfRangeException(nameof(change))
        };
        PhaseJudgement result=PhaseJudge.Evaluate(phase,evidence);
        Assert.Equal(status,result.Status);
        Assert.Equal(gate,result.Gate);
        Assert.Equal(reasons.Split(',',StringSplitOptions.RemoveEmptyEntries),result.Reasons);
        if(change=="counts-null-5") Assert.Null(result.Counts);
    }

    [Fact]
    public void Given_TrustedWorkloadObservation_When_Judged_Then_PreservesEachRepeatObservation()
    {
        PhaseRepeatEvidence[] evidence=Enumerable.Range(1,5).Select(i=>Complete(i,null) with
        { WorkloadObservation=new(true,i,i,i,true,null,null) }).ToArray();
        PhaseJudgement result=PhaseJudge.Evaluate("C0",evidence);
        for(int i=0;i<5;i++) Assert.Equal(evidence[i].WorkloadObservation,result.Repeats[i].WorkloadObservation);
    }

    [Fact]
    public void Given_NullEvidence_When_Judged_Then_ThrowsForEvidenceParameter()
    {
        ArgumentNullException error=Assert.Throws<ArgumentNullException>(()=>PhaseJudge.Evaluate("C0",null!));
        Assert.Equal("evidence",error.ParamName);
    }

    [Fact]
    public void Given_UnsupportedPhase_When_Judged_Then_ThrowsWithPhaseMessage()
    {
        PhaseRepeatEvidence[] evidence=Enumerable.Range(1,5).Select(i=>Complete(i,null)).ToArray();
        ArgumentException error=Assert.Throws<ArgumentException>(()=>PhaseJudge.Evaluate("C2",evidence));
        Assert.Equal("phase",error.ParamName);
        Assert.Equal("Unsupported phase. (Parameter 'phase')",error.Message);
    }

    private static PhaseRepeatEvidence[] MeasuredRepeats() => Enumerable.Range(1, 5).Select(i =>
        Complete(i, new(true, true, "broker", 0, 1, 20_001)) with
        {
            BrokerCount = new("LoadGenProducer", $"client-{i}", i + 1, i, true)
        }).ToArray();

    private static PhaseRepeatEvidence Complete(int repeat,ProbeRepeatObservation? probe)=>new(repeat,true,true,true,true,true,true,true,false,false,new(1,2,3,4,5,6),probe);
}
#pragma warning restore CA1707
