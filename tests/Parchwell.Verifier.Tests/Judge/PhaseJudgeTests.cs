using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Judge;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

#pragma warning disable CA1707
public sealed class PhaseJudgeTests
{
    [Theory]
    [InlineData("all", true)] [InlineData("slot", true)] [InlineData("invalid", true)]
    [InlineData("incomplete", true)] [InlineData("sweep", true)]
    [InlineData("start", false)] [InlineData("publication", false)] [InlineData("receipt", false)]
    [InlineData("job-false", false)] [InlineData("job-null", false)]
    [InlineData("registration-false", false)] [InlineData("registration-null", false)]
    [InlineData("inventory-false", false)] [InlineData("inventory-null", false)]
    [InlineData("non-b10", false)]
    public void Given_B10ObservationTrustPredicates_When_Judged_Then_UsesOnlyIntakeAuthorization(string change, bool retained)
    {
        B10ProducerObservation[] observations = [new("first", "generator-a", null, new("LoadGenProducer", "first", 3, 1, true)),
            new("second", "generator-a", null, new("LoadGenProducer", "second", 7, 0, true))];
        PhaseRepeatEvidence entry = Complete(1, null) with { B10ProducerObservations = observations };
        entry = change switch
        {
            "slot" => entry with { SlotPresent = false },
            "invalid" => entry with { KnownInvalid = true },
            "incomplete" => entry with { TrustedIncomplete = true },
            "sweep" => entry with { B10Finality = new(true) },
            "start" => entry with { StartLineAccepted = false },
            "publication" => entry with { PublicationAccepted = false },
            "receipt" => entry with { VerifierReceiptValid = false },
            "job-false" => entry with { JobBindingPass = false },
            "job-null" => entry with { JobBindingPass = null },
            "registration-false" => entry with { RegistrationCountPass = false },
            "registration-null" => entry with { RegistrationCountPass = null },
            "inventory-false" => entry with { InventoryCoveragePass = false },
            "inventory-null" => entry with { InventoryCoveragePass = null },
            _ => entry
        };
        PhaseJudgement result = PhaseJudge.Evaluate("C0", [entry, Complete(2,null), Complete(3,null), Complete(4,null), Complete(5,null)],
            change == "non-b10" ? "A01" : "B10");
        if (retained) Assert.Same(observations, result.Repeats[0].B10ProducerObservations);
        else Assert.Null(result.Repeats[0].B10ProducerObservations);
        if (change != "non-b10") { Assert.Null(result.Counts); Assert.Null(result.Repeats[0].Counts); }
    }

    [Theory]
    [InlineData("valid", "complete_valid")]
    [InlineData("slot", "incomplete")]
    [InlineData("start", "incomplete")]
    [InlineData("publication", "incomplete")]
    [InlineData("receipt", "incomplete")]
    [InlineData("job", "invalid")]
    [InlineData("registration", "invalid")]
    [InlineData("inventory", "invalid")]
    [InlineData("original-invalid", "invalid")]
    [InlineData("incomplete", "incomplete")]
    [InlineData("sweep-failed", "invalid")]
    [InlineData("repeat-invalid", "invalid")]
    [InlineData("repeat-incomplete", "incomplete")]
    public void Given_B10Finality_When_Judged_Then_PreservesEveryIndependentGateAndOriginalInvalidity(string change,string expected)
    {
        PhaseRepeatEvidence[] evidence=Enumerable.Range(1,5).Select(i=>Complete(i,null) with
        {
            B10Finality=new(false,new(i,null,null,true,VerifierResultStatus.CompleteValid,new(3,0,2,0,2,2),[]))
        }).ToArray();
        evidence[2]=change switch
        {
            "slot"=>evidence[2] with{SlotPresent=false},
            "start"=>evidence[2] with{StartLineAccepted=false},
            "publication"=>evidence[2] with{PublicationAccepted=false},
            "receipt"=>evidence[2] with{VerifierReceiptValid=false},
            "job"=>evidence[2] with{JobBindingPass=false},
            "registration"=>evidence[2] with{RegistrationCountPass=false},
            "inventory"=>evidence[2] with{InventoryCoveragePass=false},
            "original-invalid"=>evidence[2] with{KnownInvalid=true},
            "incomplete"=>evidence[2] with{TrustedIncomplete=true},
            "sweep-failed"=>evidence[2] with{B10Finality=new(true)},
            "repeat-invalid"=>evidence[2] with{B10Finality=new(false,new(3,null,null,false,VerifierResultStatus.Invalid,null,["join_failed"]))},
            "repeat-incomplete"=>evidence[2] with{B10Finality=new(false,new(3,null,null,false,VerifierResultStatus.Incomplete,null,[]))},
            _=>evidence[2]
        };
        PhaseJudgement judgement=PhaseJudge.Evaluate("C0",evidence,"B10");
        Assert.Equal(expected,judgement.Status);
        Assert.Equal(expected,judgement.Repeats[2].Status);
        Assert.Equal(expected=="complete_valid",judgement.Repeats[2].ClassificationFinal);
        if(change=="valid")Assert.Equal(new OutcomeCounts(15,0,10,0,10,10),judgement.Counts);
        else { Assert.Null(judgement.Counts);Assert.Null(judgement.Repeats[2].Counts); }
        Assert.Equal(change=="slot",judgement.Repeats[2].Reasons.Contains("j1_repeat_slots",StringComparer.Ordinal));
        Assert.Equal(change=="original-invalid",judgement.Repeats[2].Reasons.Contains("trusted_result_invalid",StringComparer.Ordinal));
        Assert.Equal(change=="sweep-failed",judgement.Repeats[2].Reasons.Contains("b10_sweep_failed",StringComparer.Ordinal));
        Assert.DoesNotContain("b10_intake_only",judgement.Repeats[2].Reasons);
        Assert.All(judgement.Repeats,r=>{Assert.Null(r.ProbeObservation);Assert.Null(r.WorkloadObservation);Assert.Null(r.BrokerCount);Assert.Null(r.BatchingEvidence);});
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Given_AcceptedSweepWithInvalidOriginal_When_Judged_Then_OnlyJudgeTrustControlsProvenance(bool knownInvalid, bool receiptValid)
    {
        PhaseRepeatEvidence[] evidence = Enumerable.Range(1, 5).Select(i => Complete(i, null) with
        {
            B10Finality = new(false, new(i, null, null, true, VerifierResultStatus.CompleteValid, new(3, 0, 2, 0, 2, 2), []))
        }).ToArray();
        evidence[2] = evidence[2] with
        {
            KnownInvalid = knownInvalid,
            VerifierReceiptValid = receiptValid,
            B10Finality = new(false, new(3, null, null, false, VerifierResultStatus.Invalid, null,
                ["trusted_result_invalid", "record_join_failed", "record_join_failed"]))
        };

        PhaseJudgement judgement = PhaseJudge.Evaluate("C0", evidence, "B10");
        PhaseRepeatResult repeat = judgement.Repeats[2];
        Assert.Equal("invalid", judgement.Status);
        Assert.Equal("invalid", repeat.Status);
        Assert.False(repeat.ClassificationFinal);
        Assert.Null(repeat.Counts);
        Assert.Null(judgement.Counts);
        Assert.Equal(knownInvalid ? 1 : 0, repeat.Reasons.Count(reason => reason == "trusted_result_invalid"));
        Assert.Equal(1, repeat.Reasons.Count(reason => reason == "record_join_failed"));
        Assert.Equal(!receiptValid, repeat.Reasons.Contains("j3_verifier_receipt", StringComparer.Ordinal));
        Assert.Equal(repeat.Reasons.Count, repeat.Reasons.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain("b10_sweep_failed", repeat.Reasons);
    }

    [Fact]
    public void Given_B10FinalCountsOverflow_When_Judged_Then_PreservesRepeatFinalityWithoutPhaseTotals()
    {
        PhaseRepeatEvidence[] evidence=Enumerable.Range(1,5).Select(i=>Complete(i,null) with
        { B10Finality=new(false,new(i,null,null,true,VerifierResultStatus.CompleteValid,new(long.MaxValue,0,0,0,0,0),[])) }).ToArray();
        PhaseJudgement judgement=PhaseJudge.Evaluate("C0",evidence,"B10");
        Assert.Equal("incomplete",judgement.Status);Assert.Null(judgement.Counts);
        Assert.Contains("j7_counts_unavailable",judgement.Reasons);
        Assert.All(judgement.Repeats,r=>Assert.True(r.ClassificationFinal));
    }

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

    [Theory]
    [InlineData("start")]
    [InlineData("publication")]
    [InlineData("receipt")]
    [InlineData("job")]
    [InlineData("registration")]
    [InlineData("inventory")]
    public void Given_BatchingEvidenceAndFailedTrustPredicate_When_Judged_Then_OmitsOnlyUntrustedProjection(string predicate)
    {
        PhaseRepeatEvidence[] evidence=Enumerable.Range(1,5).Select(i=>Complete(i,null) with
        { BatchingEvidence=new BatchingRecordedEvidence(1,i,i,i,i,i,i,i) }).ToArray();
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
        Assert.Null(result.Repeats[2].BatchingEvidence);
        Assert.Equal(evidence[0].BatchingEvidence,result.Repeats[0].BatchingEvidence);
    }

    [Theory]
    [InlineData("job")]
    [InlineData("registration")]
    [InlineData("inventory")]
    public void Given_NullableBatchingTrustPredicate_When_Judged_Then_OmitsProjection(string predicate)
    {
        PhaseRepeatEvidence[] evidence=Enumerable.Range(1,5).Select(i=>Complete(i,null) with { BatchingEvidence=new BatchingRecordedEvidence(1,i,i,i,i,i,i,i) }).ToArray();
        evidence[0]=predicate switch
        {
            "job"=>evidence[0] with{JobBindingPass=null},
            "registration"=>evidence[0] with{RegistrationCountPass=null},
            "inventory"=>evidence[0] with{InventoryCoveragePass=null},
            _=>throw new ArgumentOutOfRangeException(nameof(predicate))
        };
        Assert.Null(PhaseJudge.Evaluate("C1",evidence).Repeats[0].BatchingEvidence);
    }

    [Fact]
    public void Given_KnownInvalidOrSlotOnlyIssue_When_Judged_Then_PreservesTrustedBatchingEvidence()
    {
        PhaseRepeatEvidence[] evidence=Enumerable.Range(1,5).Select(i=>Complete(i,null) with { BatchingEvidence=new BatchingRecordedEvidence(1,i,i,i,i,i,i,i) }).ToArray();
        evidence[1]=evidence[1] with{KnownInvalid=true};
        evidence[2]=evidence[2] with{SlotPresent=false};
        PhaseJudgement result=PhaseJudge.Evaluate("C1",evidence);
        Assert.Equal(evidence[1].BatchingEvidence,result.Repeats[1].BatchingEvidence);
        Assert.Null(result.Repeats[1].Counts);
        Assert.Equal(evidence[2].BatchingEvidence,result.Repeats[2].BatchingEvidence);
        PhaseJudgement extra=PhaseJudge.Evaluate("C1",evidence.Append(Complete(6,null)).ToArray());
        Assert.Equal(evidence[0].BatchingEvidence,extra.Repeats[0].BatchingEvidence);
        PhaseJudgement duplicate=PhaseJudge.Evaluate("C1",evidence.Append(evidence[0]).ToArray());
        Assert.Null(duplicate.Repeats[0].BatchingEvidence);
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

    [Theory]
    [InlineData("C0", false)]
    [InlineData("C0", true)]
    [InlineData("C1", false)]
    [InlineData("C1", true)]
    public void Given_B10WithStaleCounts_When_Judged_Then_RemainsNonfinalWithOrWithoutIntake(string phase, bool hasIntake)
    {
        PhaseRepeatEvidence[] evidence = B10Repeats(phase);
        if (!hasIntake) evidence = evidence.Select(e => e with { B10Intake = null }).ToArray();
        PhaseJudgement result = PhaseJudge.Evaluate(phase, evidence, "B10");
        Assert.Equal("incomplete", result.Status);
        Assert.Null(result.Counts);
        Assert.Null(result.Gate);
        Assert.All(result.Repeats, repeat =>
        {
            Assert.Equal("incomplete", repeat.Status);
            Assert.Null(repeat.Counts);
            Assert.Contains("b10_intake_only", repeat.Reasons);
            Assert.Equal(new PhaseRepeatChecks(true, true, true, true, true, true, false), repeat.Checks);
            Assert.Equal(hasIntake, repeat.B10Intake is not null);
        });
        evidence[2] = evidence[2] with { KnownInvalid = true };
        result = PhaseJudge.Evaluate(phase, evidence, "B10");
        Assert.Equal("invalid", result.Status);
        Assert.Equal("invalid", result.Repeats[2].Status);
        Assert.Contains("trusted_result_invalid", result.Repeats[2].Reasons);
        Assert.Equal(evidence[2].B10Intake, result.Repeats[2].B10Intake);
        Assert.Null(result.Counts);
        Assert.All(result.Repeats, repeat => Assert.Null(repeat.Counts));
    }

    [Theory]
    [InlineData("start", false, "incomplete")]
    [InlineData("publication", false, "incomplete")]
    [InlineData("receipt", false, "incomplete")]
    [InlineData("job", false, "invalid")]
    [InlineData("job", null, "incomplete")]
    [InlineData("registration", false, "invalid")]
    [InlineData("registration", null, "incomplete")]
    [InlineData("inventory", false, "invalid")]
    [InlineData("inventory", null, "incomplete")]
    public void Given_B10UntrustedPredicate_When_Judged_Then_OnlyAffectedSlotLosesIntake(string predicate, bool? pass, string status)
    {
        foreach (string phase in new[] { "C0", "C1" })
        {
            PhaseRepeatEvidence[] evidence = B10Repeats(phase);
            evidence[2] = predicate switch
            {
                "start" => evidence[2] with { StartLineAccepted = pass == true },
                "publication" => evidence[2] with { PublicationAccepted = pass == true },
                "receipt" => evidence[2] with { VerifierReceiptValid = pass == true },
                "job" => evidence[2] with { JobBindingPass = pass },
                "registration" => evidence[2] with { RegistrationCountPass = pass },
                "inventory" => evidence[2] with { InventoryCoveragePass = pass },
                _ => throw new ArgumentOutOfRangeException(nameof(predicate))
            };
            PhaseJudgement result = PhaseJudge.Evaluate(phase, evidence, "B10");
            Assert.Equal(status, result.Status);
            Assert.Equal(status, result.Repeats[2].Status);
            Assert.Null(result.Repeats[2].B10Intake);
            for (int i = 0; i < 5; i++)
                if (i != 2) Assert.Same(evidence[i].B10Intake, result.Repeats[i].B10Intake);
            Assert.Null(result.Counts);
            Assert.All(result.Repeats, repeat => Assert.Null(repeat.Counts));
        }
    }

    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public void Given_B10SlotOnlyFailureOrDuplicate_When_Judged_Then_RetainsOnlyUniquelySelectedIntake(string phase)
    {
        PhaseRepeatEvidence[] evidence = B10Repeats(phase);
        evidence[2] = evidence[2] with { SlotPresent = false };
        PhaseJudgement missing = PhaseJudge.Evaluate(phase, evidence, "B10");
        Assert.False(missing.Repeats[2].Checks.J1);
        Assert.Same(evidence[2].B10Intake, missing.Repeats[2].B10Intake);
        PhaseJudgement duplicate = PhaseJudge.Evaluate(phase, evidence.Append(evidence[2]).ToArray(), "B10");
        Assert.False(duplicate.Repeats[2].Checks.J1);
        Assert.Null(duplicate.Repeats[2].B10Intake);
        Assert.Same(evidence[1].B10Intake, duplicate.Repeats[1].B10Intake);
        Assert.Null(missing.Counts);
        Assert.Null(duplicate.Counts);
    }

    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public void Given_B10FirstOnlyObservations_When_Judged_Then_SuppressesEveryMeasurement(string phase)
    {
        PhaseRepeatEvidence[] evidence = B10Repeats(phase).Select(e => e with
        {
            ProbeObservation = new(true, true, "broker", 0, 1, 20_001),
            WorkloadObservation = new(true, 1, 1, 1, true, null, null),
            BrokerCount = new("LoadGenProducer", "client-first", 1, 1, true),
            B04Evidence = new([1], []),
            BatchingEvidence = new(1, 1, 1, 1, 1, 1, 1, 1)
        }).ToArray();
        Assert.All(PhaseJudge.Evaluate(phase, evidence, "B10").Repeats, repeat =>
        {
            Assert.Null(repeat.ProbeObservation);
            Assert.Null(repeat.WorkloadObservation);
            Assert.Null(repeat.BrokerCount);
            Assert.Null(repeat.B04Evidence);
            Assert.Null(repeat.BatchingEvidence);
            Assert.NotNull(repeat.B10Intake);
        });
        Assert.All(PhaseJudge.Evaluate(phase, evidence).Repeats, repeat => Assert.Null(repeat.B10Intake));
    }

    private static PhaseRepeatEvidence[] B10Repeats(string phase) => Enumerable.Range(1, 5).Select(i =>
        Complete(i, null) with
        {
            B10Intake = new(phase, i,
            [
                new(new("B10", phase, "synthetic-c5", "pod-first", "pod-first", "client-first"),
                    PhaseIdentity.JobName("synthetic-c5", phase, i), null, null),
                new(new("B10", phase, "synthetic-c5", "pod-second", "pod-second", "client-second"),
                    PhaseIdentity.RestartJobName("synthetic-c5", phase, i), "pod-first", null)
            ])
        }).ToArray();

    private static PhaseRepeatEvidence[] MeasuredRepeats() => Enumerable.Range(1, 5).Select(i =>
        Complete(i, new(true, true, "broker", 0, 1, 20_001)) with
        {
            BrokerCount = new("LoadGenProducer", $"client-{i}", i + 1, i, true)
        }).ToArray();

    private static PhaseRepeatEvidence Complete(int repeat,ProbeRepeatObservation? probe)=>new(repeat,true,true,true,true,true,true,true,false,false,new(1,2,3,4,5,6),probe);
}
#pragma warning restore CA1707
