using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2ScientificAssessmentTests
{
    private static readonly string[] DependentStages = ["seedread","action","read"];
    [Fact]
    public void Given_ExactIndependentPartitionReadback_When_FinalBatchJudged_Then_PartitionHypothesisHolds()
    {
        var first = Candidate("B08","C0"); var final = Candidate("B08","C1");
        Assert.Null(first.ItemAssessment); AssertAssessment(final,"confirmed","H_partition","holds");
        Assert.All(final.Repeats,row => Assert.True(row.SemanticOutcome.ValueMatch));
        Assert.Equal("complete_valid",final.Status); Assert.True(final.Deterministic);
        Assert.DoesNotContain("smoke_only",final.Diagnostics); Assert.Contains("full_experiment",final.Diagnostics);
    }

    [Theory]
    [InlineData(1)][InlineData(3)]
    public void Given_ContraryButCorrelatedDeliveredPartition_When_FinalBatchJudged_Then_PartitionHypothesisIsContradicted(int partition)
    {
        var candidate = Candidate("B08","C1",e => Partition(e,"C1",partition));
        Assert.Equal("complete_valid",candidate.Status); Assert.True(candidate.Deterministic);
        AssertAssessment(candidate,"contradicted","H_partition","contradicted");
    }

    [Theory]
    [InlineData("C0",30,1)][InlineData("C0",30,5)][InlineData("C0",29,1)][InlineData("C0",29,5)]
    [InlineData("C1",2,1)][InlineData("C1",2,5)][InlineData("C1",1,1)][InlineData("C1",1,5)][InlineData("C1",3,1)][InlineData("C1",3,5)]
    public void Given_WrongValueAtExpectedOrContraryPartition_When_BatchJudged_Then_IncompleteReadbackPrecedesAnyPartitionVerdict(string configuration,int partition,int wrongCount)
    {
        var affected = Candidate("B08",configuration,e =>
        {
            Partition(e,configuration,partition);
            Change(e,(configuration,"read"),row => row.Repeat <= wrongCount ? row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { ValueBase64 = "d3Jvbmc=" } } } : row);
        });
        Assert.Equal("incomplete",affected.Status); Assert.Null(affected.Deterministic); Assert.Null(affected.OutcomeClass);
        Assert.All(affected.Repeats.Take(wrongCount),row => { Assert.Contains("v2_location",row.Reasons); Assert.Equal("incomplete",row.Status); Assert.False(row.SemanticOutcome.ValueMatch); });
        Assert.All(affected.Repeats.Skip(wrongCount),row => Assert.Equal("complete_valid",row.Status));
        Assert.DoesNotContain("smoke_controls_failed",affected.Diagnostics);
        var final = configuration == "C1" ? affected : Candidate("B08","C1",previous:new(StringComparer.Ordinal) { ["C0"] = affected });
        AssertAssessment(final,null,"H_partition","undecided","incomplete_configuration");
        if (configuration == "C0") Assert.Null(affected.ItemAssessment);
    }

    [Theory]
    [InlineData(31)][InlineData(33)]
    public void Given_WrongPartitionInventory_When_BatchJudged_Then_InvalidInputHasNoScientificVerdict(int count)
    {
        var candidate = Candidate("B08","C1",e => Change(e,("C1","action"),row => row with { MetadataPartitions = Enumerable.Range(0,count).ToArray() }));
        Assert.Equal("invalid",candidate.Status); AssertAssessment(candidate,null,"H_partition","undecided","incomplete_configuration");
    }

    [Theory]
    [InlineData(null,"confirmed","holds")][InlineData("","contradicted","contradicted")][InlineData("Ynl0ZXM=","contradicted","contradicted")]
    public void Given_ObservedNullEmptyOrBytes_When_SeedAndEmptyControlComplete_Then_TransportVerdictUsesObservedBytes(string? observed,string verdict,string hypothesis)
    {
        var candidate = Candidate("E01","C1",e => Value(e,"C1",observed));
        Assert.Equal("complete_valid",candidate.Status); Assert.True(candidate.Deterministic);
        AssertAssessment(candidate,verdict,"H_transport",hypothesis);
    }

    [Theory]
    [InlineData(null)][InlineData("Ynl0ZXM=")]
    public void Given_CompleteContraryEmptyControl_When_TombstoneJudged_Then_ControlFailureIsUntestable(string? observed)
    {
        var first = Candidate("E01","C0",e => Value(e,"C0",observed));
        var candidate = Candidate("E01","C1",previous:new(StringComparer.Ordinal) { ["C0"] = first });
        Assert.Equal("complete_valid",first.Status); Assert.True(first.Deterministic);
        AssertAssessment(candidate,"untestable","H_transport","undecided","failed_control");
    }

    [Theory]
    [InlineData("B08")][InlineData("E01")]
    public void Given_MissingTargetRecord_When_FinalJudged_Then_NoRecordCannotBecomeContradiction(string item)
    {
        var candidate = Candidate(item,"C1",e => Change(e,("C1","read"),row => row with { ReadObservation = new(0,0,null) }));
        Assert.Equal("incomplete",candidate.Status); Assert.Null(candidate.ItemAssessment!.Verdict); Assert.Equal("incomplete_configuration",candidate.ItemAssessment.Reason);
    }

    [Fact]
    public void Given_AbsentValueProperty_When_Decoded_Then_MissingWireIsDistinctFromPresentNull()
    {
        var options = ProbeV2Fixture.Options("E01","C1"); var capture = ProbeV2Fixture.Evidence(options,"C1","read").Capture!;
        var node = JsonNode.Parse(ProbeV2Wire.Write(capture,2097152))!.AsObject();
        node["repeats"]![0]!["read_observation"]!["record"]!.AsObject().Remove("value_base64");
        Assert.Throws<FormatException>(() => ProbeV2Wire.Read<ProbeV2CaptureFile>(Encoding.UTF8.GetBytes(node.ToJsonString()),2097152));
    }

    [Theory]
    [InlineData("B08","C0")][InlineData("B08","C1")][InlineData("E01","C1")]
    public void Given_CompleteDeterministicActionError_When_OtherControlsComplete_Then_ActionIsUntestable(string item,string configuration)
    {
        var failed = Candidate(item,configuration,e => Error(e,configuration,"broker"));
        Assert.Equal("complete_valid",failed.Status); Assert.True(failed.Deterministic);
        var final = configuration == "C1" ? failed : Candidate(item,"C1",previous:new(StringComparer.Ordinal) { ["C0"] = failed });
        AssertAssessment(final,"untestable",item == "B08" ? "H_partition" : "H_transport","undecided","action_error");
    }

    [Fact]
    public void Given_ConstructionError_When_EmptyControlAlsoFails_Then_ConstructionPrecedesItemRules()
    {
        var first = Candidate("E01","C0",e => Error(e,"C0","construction"));
        var final = Candidate("E01","C1",previous:new(StringComparer.Ordinal) { ["C0"] = first });
        AssertAssessment(final,"untestable","H_transport","undecided","construction_origin");
    }

    [Fact]
    public void Given_FailedSeedAndSkippedTarget_When_FinalJudged_Then_DependencyGapIsNotCompleteFailedControl()
    {
        var candidate = Candidate("E01","C1",e =>
        {
            Change(e,("C1","seed"),row => row with { Result = "error",Error = new(29,false,"broker","synthetic",10,1),Delivery = null });
            foreach (string stage in DependentStages) Change(e,("C1",stage),row => Skip(row,"not_attempted_dependency"));
        });
        Assert.NotEqual("complete_valid",candidate.Status); AssertAssessment(candidate,null,"H_transport","undecided","incomplete_configuration");
    }

    [Theory]
    [InlineData(4)][InlineData(6)]
    public void Given_NotExactlyFiveActionOrdinals_When_Judged_Then_NoVerdictIsAvailable(int count)
    {
        var candidate = Candidate("B08","C1",e =>
        {
            var capture = e[("C1","action")].Capture!; var rows = capture.Repeats.Take(count).ToList();
            if (count == 6) rows.Add(rows[0] with { Repeat = 6 });
            e[("C1","action")] = e[("C1","action")] with { Capture = capture with { Repeats = rows.ToArray() } };
        });
        Assert.Null(candidate.Deterministic); Assert.Null(candidate.ItemAssessment!.Verdict);
    }

    [Fact]
    public void Given_EmptyOrMissingPriorConfigurations_When_ItemJudged_Then_MissingConfigurationIsExplicit()
    {
        var empty = SingleActionConfigurationJudgement.V2JudgeItem("B08",new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Null(empty.Verdict); Assert.Equal("missing_configuration",empty.Reason);
        var final = Candidate("B08","C1",previous:new(StringComparer.Ordinal));
        Assert.Equal("complete_valid",final.Status); Assert.True(final.Deterministic); Assert.Contains("v2_controls",final.Diagnostics);
        AssertAssessment(final,null,"H_partition","undecided","missing_configuration");
    }

    [Fact]
    public void Given_DistinctBoundRepeatMarkersAndOffsets_When_Compared_Then_TheyRemainOneSemanticClass()
    {
        var candidate = Candidate("B08","C1");
        Assert.Equal(5,candidate.Repeats.Select(x => x.ReadObservation!.Record!.Offset).Distinct().Count());
        Assert.Equal(5,candidate.Repeats.Select(x => x.ReadObservation!.Record!.ValueBase64).Distinct().Count());
        Assert.True(candidate.Deterministic); AssertAssessment(candidate,"confirmed","H_partition","holds");
    }

    [Fact]
    public void Given_DifferentComparedPartition_When_Complete_Then_UnequalClassesRequireSeparatePlan()
    {
        var candidate = Candidate("B08","C1",e => Partition(e,"C1",1,1));
        Assert.Equal("complete_valid",candidate.Status); Assert.False(candidate.Deterministic); Assert.Null(candidate.OutcomeClass);
        AssertAssessment(candidate,null,"H_partition","undecided","unequal_classes_separate_full_plan");
    }

    [Theory]
    [InlineData("missing")][InlineData("hash")][InlineData("exit")][InlineData("source")][InlineData("mode")]
    public void Given_UnusableFinalJudgeReceiptOrMode_When_TransportChecked_Then_AssessmentCannotBeConsumed(string mutation)
    {
        var options = ProbeV2Fixture.Options("B08","C1"); options.RunKind = "batch"; var candidate = Candidate("B08","C1");
        byte[] raw = ProbeV2Wire.Write(candidate,16777216); var receipt = Receipt(options,raw);
        if (mutation == "hash") receipt = receipt with { CandidateSha256 = new string('0',64) };
        if (mutation == "exit") receipt = receipt with { HandlerExitCode = 1 };
        if (mutation == "source") receipt = receipt with { SourceCommit = new string('2',40) };
        if (mutation == "mode") options.RunKind = "smoke";
        Assert.False(SingleActionConfigurationJudgement.V2Published(options,"C1",candidate,mutation == "missing" ? null : receipt,raw));
    }

    [Fact]
    public void Given_ExactFinalReceipt_When_TransportChecked_Then_TypedFinalAssessmentIsEligible()
    {
        var options = ProbeV2Fixture.Options("B08","C1"); options.RunKind = "batch"; var candidate = Candidate("B08","C1");
        byte[] raw = ProbeV2Wire.Write(candidate,16777216);
        Assert.True(SingleActionConfigurationJudgement.V2Published(options,"C1",candidate,Receipt(options,raw),raw));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Given_PermissionControlsAndNegativeErrorsOrSilence_When_AllConfigurationsComplete_Then_NegativesAreRecordedOnly(bool errors)
    {
        var previous = new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal);
        foreach (string configuration in ProbeV2Matrix.Configurations("F06"))
        {
            var candidate = Candidate("F06",configuration,errors && configuration is "C10" or "C11" ? e => Change(e,(configuration,"action"),row => row with
            { Result = "error",Error = new(29,false,"broker","synthetic",10,1),ElapsedMs = 10,TerminalUnixMs = 1000,Statistics = [ProbeV2Fixture.Statistic(row.ClientId!,row.Action,1)] }) : null,previous);
            previous.Add(configuration,candidate);
        }
        var final = previous["C11"];
        Assert.Equal("complete_valid",final.Status); Assert.True(final.Deterministic);
        AssertAssessment(final,"undocumented","H_positive_scope","holds"); Assert.Equal("recorded_only",final.ItemAssessment!.Hypotheses["H_negative_surface"]);
        Assert.All(previous.Values,candidate => Assert.Single(candidate.Diagnostics,x => x.StartsWith("permission_audit_sha256:",StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("C2",false)][InlineData("C4",true)][InlineData("C10",false)][InlineData("C11",true)]
    public void Given_ExactNarrowSuccess_When_StatisticsAlsoMissing_Then_SecurityFindingPrecedesOtherGaps(string configuration,bool missingStatistics)
    {
        var finding = Candidate("F06",configuration,e =>
        {
            if (configuration is "C2" or "C4")
            {
                Change(e,(configuration,"action"),row => row with { Result = "success",Error = null,Delivery = new(row.Requested!.Topic,0,100 * row.Repeat,"Persisted",null,10,1),Statistics = missingStatistics ? [] : row.Statistics });
            }
            else
            {
                Change(e,(configuration,"action"),row => row with { Result = "success",Error = null,ElapsedMs = 10,TerminalUnixMs = 1000,
                    ReadObservation = new(1,0,new(row.Requested!.Topic,0,row.Requested.Offset!.Value,row.Requested.KeyBase64,row.Requested.ValueBase64,10,1)),
                    Statistics = missingStatistics ? [] : [ProbeV2Fixture.Statistic(row.ClientId!,row.Action,1)] });
            }
        });
        Assert.True(finding.SecurityFinding);
        var previous = ProbeV2Matrix.Configurations("F06").Take(11).ToDictionary(c => c,c => Candidate("F06",c),StringComparer.Ordinal);
        if (configuration != "C11") previous[configuration] = finding;
        var final = configuration == "C11" ? finding : Candidate("F06","C11",previous:previous);
        AssertAssessment(final,null,"H_positive_scope","undecided","security_finding_requires_hari");
    }

    [Fact]
    public void Given_PermissionPositiveSendFailsCompletely_When_FinalJudged_Then_ValidFailureIsUntestable()
    {
        var first = Candidate("F06","C0",e => Error(e,"C0","broker"));
        Assert.Equal("complete_valid",first.Status); Assert.True(first.Deterministic);
        var previous = ProbeV2Matrix.Configurations("F06").Take(11).ToDictionary(c => c,c => Candidate("F06",c),StringComparer.Ordinal); previous["C0"] = first;
        var final = Candidate("F06","C11",previous:previous);
        AssertAssessment(final,"untestable","H_positive_scope","undecided","failed_control");
    }

    [Theory]
    [InlineData("C0")][InlineData("C1")][InlineData("C3")][InlineData("C5")]
    [InlineData("C6")][InlineData("C7")][InlineData("C8")][InlineData("C9")]
    public void Given_CompleteDeterministicWrongPositiveControlValue_When_FinalScopeJudged_Then_FailedControlIsUntestable(string configuration)
    {
        var failed = Candidate("F06",configuration,e => WrongPositiveValue(e,configuration,5));
        Assert.Equal("complete_valid",failed.Status); Assert.True(failed.Deterministic);
        Assert.All(failed.Repeats,row => Assert.False(row.SemanticOutcome.ValueMatch));
        Assert.DoesNotContain("smoke_controls_failed",failed.Diagnostics);
        var previous = ProbeV2Matrix.Configurations("F06").Take(11).ToDictionary(c => c,c => Candidate("F06",c),StringComparer.Ordinal);
        previous[configuration] = failed;
        var final = Candidate("F06","C11",previous:previous);
        AssertAssessment(final,"untestable","H_positive_scope","undecided","failed_control");
        Assert.Equal("undecided",final.ItemAssessment!.Hypotheses["H_negative_surface"]);
    }

    [Theory]
    [InlineData("C0")][InlineData("C1")][InlineData("C3")][InlineData("C5")]
    [InlineData("C6")][InlineData("C7")][InlineData("C8")][InlineData("C9")]
    public void Given_MixedWrongAndExactPositiveControlValues_When_FinalScopeJudged_Then_UnequalClassesPrecedeFailedControl(string configuration)
    {
        var mixed = Candidate("F06",configuration,e => WrongPositiveValue(e,configuration,1));
        Assert.Equal("complete_valid",mixed.Status); Assert.False(mixed.Deterministic); Assert.Null(mixed.OutcomeClass);
        Assert.False(mixed.Repeats[0].SemanticOutcome.ValueMatch);
        Assert.All(mixed.Repeats.Skip(1),row => Assert.True(row.SemanticOutcome.ValueMatch));
        var previous = ProbeV2Matrix.Configurations("F06").Take(11).ToDictionary(c => c,c => Candidate("F06",c),StringComparer.Ordinal);
        previous[configuration] = mixed;
        var final = Candidate("F06","C11",previous:previous);
        AssertAssessment(final,null,"H_positive_scope","undecided","unequal_classes_separate_full_plan");
        Assert.Equal("undecided",final.ItemAssessment!.Hypotheses["H_negative_surface"]);
    }

    [Theory]
    [InlineData("C0")][InlineData("C1")][InlineData("C3")][InlineData("C5")]
    [InlineData("C6")][InlineData("C7")][InlineData("C8")][InlineData("C9")]
    public void Given_MissingPositiveControlRecord_When_FinalScopeJudged_Then_IncompleteEvidencePrecedesFailedControl(string configuration)
    {
        string stage = configuration is "C0" or "C1" or "C3" or "C5" ? "read" : "action";
        var missing = Candidate("F06",configuration,e => Change(e,(configuration,stage),row => row with { ReadObservation = new(0,0,null) }));
        Assert.Equal("incomplete",missing.Status); Assert.Null(missing.Deterministic);
        Assert.All(missing.Repeats,row => Assert.Null(row.SemanticOutcome.ValueMatch));
        var previous = ProbeV2Matrix.Configurations("F06").Take(11).ToDictionary(c => c,c => Candidate("F06",c),StringComparer.Ordinal);
        previous[configuration] = missing;
        var final = Candidate("F06","C11",previous:previous);
        AssertAssessment(final,null,"H_positive_scope","undecided","incomplete_configuration");
    }

    private static void WrongPositiveValue(Dictionary<(string Configuration,string Stage),ProbeV2Evidence> evidence,string configuration,int count)
    {
        string stage = configuration is "C0" or "C1" or "C3" or "C5" ? "read" : "action";
        Change(evidence,(configuration,stage),row => row.Repeat <= count ? row with
        {
            ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { ValueBase64 = "d3Jvbmc=" } }
        } : row);
    }

    [Fact]
    public void Given_InvalidAndIncompleteRepeatEvidence_When_FinalJudged_Then_InvalidStatusPrecedesIncomplete()
    {
        var candidate = Candidate("B08","C1",e =>
        {
            Change(e,("C1","action"),row => row.Repeat == 1 ? row with { MetadataPartitions = Enumerable.Range(0,31).ToArray() } : row);
            Change(e,("C1","read"),row => row.Repeat == 2 ? row with { ReadObservation = new(0,0,null) } : row);
        });
        Assert.Equal("invalid",candidate.Status); Assert.Contains(candidate.Repeats,row => row.Status == "invalid"); Assert.Contains(candidate.Repeats,row => row.Status == "incomplete");
        AssertAssessment(candidate,null,"H_partition","undecided","incomplete_configuration");
    }

    private static ProbeV2JudgeReceipt Receipt(ProbeV2Options options,byte[] raw) => new(2,"single-action-v2-judge",options.RunId,options.ScenarioId,options.ConfigurationId,options.SourceCommit,options.JobName,options.PodName,options.PodUid,
        ProbeV2Matrix.Prefix(options.RunId,options.ScenarioId,options.ConfigurationId) + "candidate.json",Convert.ToHexStringLower(SHA256.HashData(raw)),0,true);

    private static void AssertAssessment(ProbeV2Candidate candidate,string? verdict,string label,string hypothesis,string? reason = null)
    {
        Assert.NotNull(candidate.ItemAssessment); Assert.Equal(verdict,candidate.ItemAssessment.Verdict); Assert.Equal(hypothesis,candidate.ItemAssessment.Hypotheses[label]); Assert.Equal(reason,candidate.ItemAssessment.Reason);
    }
    private static ProbeV2Candidate Candidate(string item,string configuration,Action<Dictionary<(string Configuration,string Stage),ProbeV2Evidence>>? mutate = null,Dictionary<string,ProbeV2Candidate>? previous = null)
    {
        var options = ProbeV2Fixture.Options(item,configuration); options.RunKind = "batch"; var evidence = ProbeV2Fixture.Evidence(options); mutate?.Invoke(evidence);
        previous ??= ProbeV2Matrix.Configurations(item).Last() == configuration ? ProbeV2Matrix.Configurations(item).TakeWhile(c => c != configuration).ToDictionary(c => c,c => Candidate(item,c),StringComparer.Ordinal) : new(StringComparer.Ordinal);
        return SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),previous,item == "F06" ? ProbeV2AccessEvidenceTests.Access(options) : null);
    }
    private static void Change(Dictionary<(string Configuration,string Stage),ProbeV2Evidence> evidence,(string Configuration,string Stage) key,Func<ProbeV2Repeat,ProbeV2Repeat> change)
    {
        var value = evidence[key]; evidence[key] = value with { Capture = value.Capture! with { Repeats = value.Capture.Repeats.Select(change).ToArray() } };
    }
    private static void Value(Dictionary<(string Configuration,string Stage),ProbeV2Evidence> evidence,string configuration,string? value) =>
        Change(evidence,(configuration,"read"),row => row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { ValueBase64 = value } } });
    private static void Partition(Dictionary<(string Configuration,string Stage),ProbeV2Evidence> evidence,string configuration,int partition,int? ordinal = null)
    {
        Change(evidence,(configuration,"action"),row => ordinal is null || row.Repeat == ordinal ? row with { Delivery = row.Delivery! with { Partition = partition } } : row);
        Change(evidence,(configuration,"read"),row => ordinal is null || row.Repeat == ordinal ? row with { Requested = row.Requested! with { Partition = partition },ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { Partition = partition } } } : row);
    }
    private static ProbeV2Repeat Skip(ProbeV2Repeat row,string admission) => row with { Admission = admission,ClientId = null,Terminal = false,ElapsedMs = 0,TerminalUnixMs = null,Settings = new Dictionary<string,string>(StringComparer.Ordinal),OmittedKeys = [],MetadataPartitions = null,Requested = null,Delivery = null,ReadObservation = new(0,0,null),Result = null,Error = null,ClientErrors = [],Statistics = [],Diagnostics = admission == "not_applicable_no_delivery" ? [] : [admission] };
    private static void Error(Dictionary<(string Configuration,string Stage),ProbeV2Evidence> evidence,string configuration,string origin)
    {
        Change(evidence,(configuration,"action"),row => row with { Result = "error",Error = new(29,false,origin,"synthetic",10,1),Delivery = null });
        Change(evidence,(configuration,"read"),row => Skip(row,"not_applicable_no_delivery"));
    }
}
