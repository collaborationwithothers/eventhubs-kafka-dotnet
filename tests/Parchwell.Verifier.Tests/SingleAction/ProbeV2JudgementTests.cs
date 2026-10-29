using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2JudgementTests
{
    private static readonly string[] ImageNames = ["loadgen", "verifier"];
    public static TheoryData<string,string> Matrix
    {
        get
        {
            var rows = new TheoryData<string,string>();
            foreach (string item in new[] { "B08", "E01", "F06" })
                foreach (string configuration in ProbeV2Matrix.Configurations(item)) rows.Add(item,configuration);
            return rows;
        }
    }
    [Theory,MemberData(nameof(Matrix))]
    public void Given_CompleteSyntheticMatrix_When_Judged_Then_NoVerdictAndAllRequiredControlsPass(string item,string configuration)
    {
        var candidate = ProbeV2Fixture.Candidate(item,configuration);
        Assert.Equal("complete_valid",candidate.Status); Assert.True(candidate.Deterministic);
        Assert.True(SingleActionConfigurationJudgement.V2SmokeAccepted(candidate)); Assert.Null(candidate.ItemAssessment);
        Assert.All(candidate.Repeats,x => { Assert.Empty(x.Reasons); Assert.Equal("complete_valid",x.Status); });
        Assert.Equal(ImageNames,candidate.ImageDigests.Keys.Order(StringComparer.Ordinal));
        byte[] bytes = ProbeV2Wire.Write(candidate,16777216);
        var parsed = ProbeV2Wire.Read<ProbeV2Candidate>(bytes,16777216);
        Assert.Equal(candidate.Status,parsed.Status); Assert.Null(parsed.ItemAssessment);
        string json = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("principal_id",json,StringComparison.Ordinal); Assert.DoesNotContain("/subscriptions/",json,StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => SingleActionCandidateCodec.Read(bytes));
    }
    [Theory]
    [InlineData("B08",true)][InlineData("E01",true)][InlineData("F06",false)]
    public void Given_MissingEffectiveScopeConfirmation_When_ScopeChecked_Then_OnlyPermissionItemBlocks(string item,bool valid)
    {
        var options = ProbeV2Fixture.Options(item); var scope = ProbeV2Fixture.Scope(options) with { EffectiveScope = null,EffectiveScopeSha256 = null };
        Assert.Equal(valid,SingleActionConfigurationJudgement.V2Scope(options,scope));
    }
    [Theory]
    [InlineData("extra")][InlineData("role")][InlineData("scope")][InlineData("principal")][InlineData("client")][InlineData("source")][InlineData("false_confirmation")][InlineData("hash")]
    public void Given_ChangedRoleEvidence_When_Checked_Then_ControlsCannotPass(string mutation)
    {
        var options = ProbeV2Fixture.Options("F06"); var scope = ProbeV2Fixture.Scope(options);
        if (mutation == "extra") scope.Deployed.Assignments.Add("extra",scope.Deployed.Assignments["loadgen_events_sender"]);
        if (mutation is "role" or "scope" or "principal")
        {
            var role = scope.Deployed.Assignments["loadgen_events_sender"];
            scope.Deployed.Assignments["loadgen_events_sender"] = mutation switch { "role" => role with { RoleDefinitionName = "Azure Event Hubs Data Owner" }, "scope" => role with { Scope = scope.Deployed.NamespaceId }, _ => role with { PrincipalId = scope.Deployed.Accounts["probe"].PrincipalId } };
        }
        if (mutation == "client") scope.Deployed.Accounts["verifier"] = scope.Deployed.Accounts["verifier"] with { ClientId = scope.Deployed.Accounts["probe"].ClientId };
        if (mutation == "source") scope = scope with { SourceCommit = new string('2',40) };
        if (mutation == "false_confirmation") scope.EffectiveScope!.Accounts["loadgen"] = scope.EffectiveScope.Accounts["loadgen"] with { NoAdditionalDataAccess = false };
        if (mutation == "hash") scope = scope with { EffectiveScopeSha256 = new string('z',64) };
        Assert.False(SingleActionConfigurationJudgement.V2Scope(options,scope));
    }
    [Theory]
    [InlineData("missing")][InlineData("extra")][InlineData("uppercase")][InlineData("short")][InlineData("swapped")]
    public void Given_ChangedImageMap_When_Checked_Then_ItCannotBindToRuntime(string mutation)
    {
        var expected = ProbeV2Fixture.Options().ImageDigests(); var map = new Dictionary<string,string>(expected,StringComparer.Ordinal);
        if (mutation == "missing") map.Remove("verifier");
        if (mutation == "extra") map.Add("enrichment",map["loadgen"]);
        if (mutation == "uppercase") map["loadgen"] = "sha256:" + new string('A',64);
        if (mutation == "short") map["loadgen"] = "sha256:" + new string('a',63);
        if (mutation == "swapped") (map["loadgen"],map["verifier"]) = (map["verifier"],map["loadgen"]);
        Assert.False(SingleActionConfigurationJudgement.V2Images(map,expected));
    }
    [Theory]
    [InlineData(31,"invalid")][InlineData(32,"complete_valid")][InlineData(33,"invalid")]
    public void Given_MetadataPartitionCount_When_Judged_Then_ExactInventoryRequired(int count,string status)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action");
        var row = evidence.Capture!.Repeats[0] with { MetadataPartitions = Enumerable.Range(0,count).ToArray() };
        Assert.Equal(status,SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]).Status);
    }
    [Theory]
    [InlineData(63,true)][InlineData(64,true)][InlineData(65,false)]
    public void Given_BrokerEntries_When_StatisticsChecked_Then_BoundaryIsExplicit(int count,bool valid)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action"); var row = evidence.Capture!.Repeats[0];
        var brokers = new JsonObject();
        for (int i = 0;i < count;i++) brokers["b" + i] = new JsonObject { ["source"] = "learned",["state"] = "UP" };
        row = row with { Statistics = [ProbeV2Fixture.Statistic(row.ClientId!,row.Action,1,11,brokers.ToJsonString())] };
        var result = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
        Assert.Equal(valid ? "complete_valid" : "incomplete",result.Status); Assert.Equal(valid ? count : null,result.BrokerCount);
        Assert.Equal(!valid,result.Reasons.Contains("v2_capture_limit",StringComparer.Ordinal));
    }
    [Theory]
    [InlineData(32767,true)][InlineData(32768,true)][InlineData(32769,false)]
    public void Given_RawStatisticsBytes_When_Checked_Then_ExactCeilingApplies(int size,bool valid)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action"); var row = evidence.Capture!.Repeats[0];
        string raw = Encoding.UTF8.GetString(Convert.FromBase64String(row.Statistics[0].RawBase64));
        raw += new string(' ',size - Encoding.UTF8.GetByteCount(raw));
        row = row with { Statistics = [row.Statistics[0] with { RawBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)) }] };
        var result = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
        Assert.Equal(valid ? "complete_valid" : "incomplete",result.Status);
        Assert.Equal(!valid,result.Reasons.Contains("v2_capture_limit",StringComparer.Ordinal));
    }
    [Theory]
    [InlineData(999,true)][InlineData(1000,true)][InlineData(1001,false)]
    public void Given_NativeSecondsRounding_When_ComparedToTerminal_Then_NoEarlierReportAccepted(long terminal,bool valid)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action"); var row = evidence.Capture!.Repeats[0] with { TerminalUnixMs = terminal };
        var result = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
        Assert.Equal(valid ? "complete_valid" : "incomplete",result.Status);
    }
    [Theory]
    [InlineData(7,true)][InlineData(8,true)][InlineData(9,false)]
    public void Given_ErrorCollectionSize_When_Judged_Then_CaptureBoundApplies(int count,bool valid)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action"); var row = evidence.Capture!.Repeats[0];
        row = row with { ClientErrors = Enumerable.Range(1,count).Select(i => new ProbeV2Error(-1,false,"local","synthetic",1,i)).ToArray() };
        var result = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
        Assert.Equal(valid ? "complete_valid" : "incomplete",result.Status);
        Assert.Equal(valid ? Array.Empty<string>() : ["v2_capture_limit"],result.Reasons);
    }
    [Theory]
    [InlineData(119999,false)][InlineData(120000,true)][InlineData(120001,false)]
    public void Given_SilentReadWindow_When_Judged_Then_FullWindowIsRequired(long elapsed,bool valid)
    {
        var options = ProbeV2Fixture.Options("F06","C10"); var evidence = ProbeV2Fixture.Evidence(options,"C10","action"); var row = evidence.Capture!.Repeats[0] with { ElapsedMs = elapsed };
        var result = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
        Assert.Equal(valid,result.Status == "complete_valid");
    }
    [Fact]
    public void Given_FiveFailedSends_When_ReadersHaveNoClientReceipts_Then_OutcomeRemainsCompleteError()
    {
        var candidate = ProbeV2Fixture.Candidate("F06","C2");
        Assert.Equal("complete_valid",candidate.Status); Assert.True(candidate.Deterministic);
        Assert.All(candidate.Repeats,row => { Assert.Equal("error",row.OutcomeClass!.Result); Assert.Single(row.Clients); Assert.Null(row.Delivery); Assert.NotEmpty(row.ControlReferences); });
    }
    [Fact]
    public void Given_MissingAllSkippedReaderReceipt_When_Judged_Then_ErrorEvidenceIsIncomplete()
    {
        var options = ProbeV2Fixture.Options("F06","C2"); var evidence = ProbeV2Fixture.Evidence(options);
        var read = evidence[("C2","read")]; evidence[("C2","read")] = read with { Capture = read.Capture! with { CleanupCompleted = false } };
        var candidate = SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Equal("incomplete",candidate.Status); Assert.All(candidate.Repeats,row => Assert.Contains("v2_completion",row.Reasons));
    }
    [Fact]
    public void Given_ContraryButCorrelatedPartition_When_Judged_Then_EvidenceIsCompleteWithoutScientificVerdict()
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options);
        foreach (string stage in new[] { "action", "read" })
        {
            var value = evidence[("C0",stage)];
            evidence[("C0",stage)] = value with { Capture = value.Capture! with { Repeats = value.Capture.Repeats.Select(row => stage == "action" ? row with { Delivery = row.Delivery! with { Partition = 31 } } : row with { Requested = row.Requested! with { Partition = 31 },ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { Partition = 31 } } }).ToArray() } };
        }
        var candidate = SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Equal("complete_valid",candidate.Status); Assert.True(candidate.Deterministic); Assert.Null(candidate.ItemAssessment);
        Assert.All(candidate.Repeats,row => Assert.Equal(31,row.SemanticOutcome.DeliveredPartition));
    }
    [Fact]
    public void Given_WrongRequestedReadOffset_When_Judged_Then_DeliveryCannotBeCorrelated()
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options); var value = evidence[("C0","read")];
        evidence[("C0","read")] = value with { Capture = value.Capture! with { Repeats = value.Capture.Repeats.Select(row => row with { Requested = row.Requested! with { Offset = row.Requested.Offset + 1 } }).ToArray() } };
        var candidate = SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Equal("invalid",candidate.Status); Assert.All(candidate.Repeats,row => Assert.Contains("v2_location",row.Reasons));
    }
    [Fact]
    public void Given_UnexpectedNarrowReadAndMissingStatistics_When_Judged_Then_SecurityFindingSurvives()
    {
        var options = ProbeV2Fixture.Options("F06","C10"); var evidence = ProbeV2Fixture.Evidence(options); var value = evidence[("C10","action")];
        evidence[("C10","action")] = value with { Capture = value.Capture! with { Repeats = value.Capture.Repeats.Select(row => row with { Result = "success",ElapsedMs = 10,TerminalUnixMs = 1000,Statistics = [],ReadObservation = new(1,0,new(row.Requested!.Topic,0,row.Requested.Offset!.Value,row.Requested.KeyBase64,row.Requested.ValueBase64,10,1)) }).ToArray() } };
        var candidate = SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Equal("incomplete",candidate.Status); Assert.True(candidate.SecurityFinding); Assert.Null(candidate.ItemAssessment);
    }
    [Fact]
    public void Given_RepeatSpecificMarkers_When_Compared_Then_F06EqualityUsesOnlySemanticMask()
    {
        var candidate = ProbeV2Fixture.Candidate("F06","C1");
        Assert.NotEqual(candidate.Repeats[0].SemanticOutcome.KeyBase64,candidate.Repeats[1].SemanticOutcome.KeyBase64);
        Assert.True(SingleActionConfigurationJudgement.V2Equal("F06","C1",candidate.Repeats[0],candidate.Repeats[1]));
        Assert.False(SingleActionConfigurationJudgement.V2Equal("F06","C1",candidate.Repeats[0],candidate.Repeats[1] with { SemanticOutcome = candidate.Repeats[1].SemanticOutcome with { ValueMatch = false } }));
    }
    [Theory]
    [InlineData("missing")][InlineData("extra")][InlineData("duplicate")][InlineData("null_collection")]
    public void Given_MalformedCaptureWire_When_Decoded_Then_ItIsNotEquivalentToMissingEvidence(string mutation)
    {
        byte[] bytes = ProbeV2Wire.Write(ProbeV2Fixture.Evidence(ProbeV2Fixture.Options(),"C0","action").Capture!,524288);
        var node = JsonNode.Parse(bytes)!.AsObject();
        if (mutation == "missing") node.Remove("repeats");
        if (mutation == "extra") node["extra"] = 1;
        if (mutation == "null_collection") node["repeats"] = null;
        string json = node.ToJsonString();
        if (mutation == "duplicate") json = json.Replace("\"schema_version\":2","\"schema_version\":2,\"schema_version\":2",StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => ProbeV2Wire.Read<ProbeV2CaptureFile>(Encoding.UTF8.GetBytes(json),524288));
    }
    [Theory]
    [InlineData("earlier_fatal")][InlineData("terminal_time")][InlineData("nonfatal_promotion")][InlineData("after_window")]
    public void Given_InconsistentTerminal_When_Judged_Then_ResultIsInvalid(string mutation)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action"); var row = evidence.Capture!.Repeats[0];
        if (mutation == "earlier_fatal") row = row with { ClientErrors = [new(-150,true,"local","synthetic",5,0)] };
        if (mutation == "terminal_time") row = row with { ElapsedMs = 9 };
        if (mutation == "nonfatal_promotion")
        {
            var error = new ProbeV2Error(-1,false,"local","synthetic",5,0);
            row = row with { Result = "error",Error = error,ElapsedMs = 5,Delivery = null,ClientErrors = [error] };
        }
        if (mutation == "after_window") row = row with { Delivery = row.Delivery! with { ObservedElapsedMs = 330001 } };
        var result = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
        Assert.Equal("invalid",result.Status); Assert.Contains("v2_terminal",result.Reasons);
    }
    [Fact]
    public void Given_EarlierFatalAndLaterDelivery_When_Judged_Then_EarlierErrorIsPreserved()
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action");
        var error = new ProbeV2Error(-150,true,"local","synthetic",5,0);
        var row = evidence.Capture!.Repeats[0] with { Result = "error",Error = error,ElapsedMs = 5,ClientErrors = [error] };
        var result = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
        Assert.Equal("complete_valid",result.Status); Assert.Equal("error",result.OutcomeClass!.Result); Assert.NotNull(result.Delivery);
    }
    [Fact]
    public void Given_RecordsDuringPostterminalPolling_When_Judged_Then_SilenceClassRemainsButSecurityFindingIsRetained()
    {
        var options = ProbeV2Fixture.Options("F06","C10"); var evidence = ProbeV2Fixture.Evidence(options); var value = evidence[("C10","action")];
        evidence[("C10","action")] = value with { Capture = value.Capture! with { Repeats = value.Capture.Repeats.Select(row => row with
        { ReadObservation = new(1,0,new(row.Requested!.Topic,0,row.Requested.Offset!.Value,row.Requested.KeyBase64,row.Requested.ValueBase64,120001,2)) }).ToArray() } };
        var candidate = SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Equal("complete_valid",candidate.Status); Assert.True(candidate.SecurityFinding); Assert.Equal("no_outcome",candidate.OutcomeClass!.Result);
    }
    [Theory]
    [InlineData(false,119999)][InlineData(false,120000)][InlineData(false,120001)]
    [InlineData(true,119999)][InlineData(true,120000)][InlineData(true,120001)]
    public void Given_CallbackAtSilenceBoundary_When_Judged_Then_LateErrorsRemainEvidenceWithoutChangingTheWindow(bool fatal,long observed)
    {
        var options = ProbeV2Fixture.Options("F06","C10");
        var evidence = ProbeV2Fixture.Evidence(options,"C10","action");
        var error = new ProbeV2Error(-1,fatal,"local","synthetic",observed,4);
        var row = evidence.Capture!.Repeats[0] with { ClientErrors = [error] };
        var result = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
        Assert.Equal(observed > 120000 ? "complete_valid" : "invalid",result.Status);
        Assert.Equal(observed <= 120000,result.Reasons.Contains("v2_terminal",StringComparer.Ordinal));
        if (observed > 120000) Assert.Equal("no_outcome",result.OutcomeClass!.Result);
        Assert.Equal(error,Assert.Single(row.ClientErrors));
    }
    [Fact]
    public void Given_CleanupFailurePrefixThenResumedClient_When_CompletionChecked_Then_FailureCannotBeHidden()
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0","action");
        var rows = evidence.Capture!.Repeats.ToArray(); rows[0] = rows[0] with { Diagnostics = ["client_cleanup_failed:TimeoutException"] };
        evidence = evidence with { Capture = evidence.Capture with { Repeats = rows } };
        Assert.Contains("v2_completion",SingleActionConfigurationJudgement.V2EvidenceProblems(options,evidence));
        var result = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,rows[0],rows[0].Settings,[]);
        Assert.Contains("v2_completion",result.Reasons);
    }
    [Fact]
    public void Given_AliasedControlIdentities_When_RoleEvidenceChecked_Then_RolesCannotPretendToBeIndependent()
    {
        var options = ProbeV2Fixture.Options("F06"); var scope = ProbeV2Fixture.Scope(options);
        var same = scope.Deployed.Accounts["probe"]; scope.Deployed.Accounts["loadgen"] = same;
        foreach (string key in scope.Deployed.Assignments.Keys.ToArray())
            if (scope.Deployed.Assignments[key].ServiceAccount == "loadgen") scope.Deployed.Assignments[key] = scope.Deployed.Assignments[key] with { PrincipalId = same.PrincipalId };
        scope.EffectiveScope!.Accounts["loadgen"] = new(same.PrincipalId,true);
        Assert.False(SingleActionConfigurationJudgement.V2Scope(options,scope));
    }
    [Fact]
    public void Given_PriorSecurityFinding_When_FinalConfigurationJudged_Then_FindingIsNotLost()
    {
        var options = ProbeV2Fixture.Options("F06","C11"); var evidence = ProbeV2Fixture.Evidence(options);
        var previous = ProbeV2Matrix.Configurations("F06").Take(11).ToDictionary(x => x,x => ProbeV2Fixture.Candidate("F06",x),StringComparer.Ordinal);
        previous["C2"] = previous["C2"] with { SecurityFinding = true };
        var candidate = SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),previous);
        Assert.True(candidate.SecurityFinding); Assert.False(SingleActionConfigurationJudgement.V2SmokeAccepted(candidate)); Assert.Null(candidate.ItemAssessment);
    }
}
