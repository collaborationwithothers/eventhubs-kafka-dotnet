using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class SingleActionJudgementBoundaryAuditTests
{
    [Theory]
    [InlineData(0,false)][InlineData(1,true)][InlineData(2,true)]
    public void Given_StatisticsSequenceBoundary_When_Judged_Then_OnlyPositiveSequencesAccepted(int sequence,bool accepted)
    {
        var repeat=SingleActionFixture.Repeat();repeat=repeat with{Statistics=[repeat.Statistics[0] with{Sequence=sequence}]};
        AssertStatistics(SingleActionFixture.Judge(repeat),accepted);
    }
    [Theory]
    [InlineData(-1,false)][InlineData(0,true)][InlineData(1,true)]
    public void Given_StatisticsObservedElapsedBoundary_When_Judged_Then_NonnegativeValuesAccepted(long elapsed,bool accepted)
    {
        var repeat=SingleActionFixture.Repeat();repeat=repeat with{Statistics=[repeat.Statistics[0] with{ObservedElapsedMs=elapsed}]};
        AssertStatistics(SingleActionFixture.Judge(repeat),accepted);
    }
    [Theory]
    [InlineData(0,true)][InlineData(4095,true)][InlineData(4096,true)][InlineData(4097,false)]
    public void Given_TerminalErrorReasonUtf8ByteBoundary_When_Judged_Then_ExactLimitAndEmptyReasonHonoured(int bytes,bool accepted)
    {
        string reason=Utf8Text(bytes);Assert.Equal(bytes,Encoding.UTF8.GetByteCount(reason));
        var repeat=SingleActionFixture.Repeat() with{Result=SingleActionFixture.Json("\"error\""),Error=JsonSerializer.SerializeToElement(new{code=29,is_fatal=false,origin="broker",reason})};
        var result=SingleActionFixture.Judge(repeat);Assert.Equal(accepted?"complete_valid":"invalid",result.Status);
        Assert.Equal(!accepted,result.Reasons.Contains("p7_outcome_record",StringComparer.Ordinal));
        if(accepted)Assert.Equal(new SingleActionOutcome("error",29,false,"broker"),result.OutcomeClass);else Assert.Null(result.OutcomeClass);
    }
    [Theory]
    [InlineData("null")][InlineData("false")][InlineData("0")][InlineData("[]")][InlineData("{}")]
    public void Given_TerminalReasonWrongKind_When_Judged_Then_P7Invalid(string raw)
    {
        var repeat=SingleActionFixture.Repeat() with{Result=SingleActionFixture.Json("\"error\""),Error=SingleActionFixture.Json("{\"code\":29,\"is_fatal\":false,\"origin\":\"broker\",\"reason\":"+raw+"}")};
        var result=SingleActionFixture.Judge(repeat);Assert.Equal("invalid",result.Status);Assert.Contains("p7_outcome_record",result.Reasons);
    }
    [Theory]
    [InlineData("-2147483649",false)][InlineData("-2147483648",true)][InlineData("-2147483647",true)][InlineData("2147483646",true)][InlineData("2147483647",true)][InlineData("2147483648",false)][InlineData("0.5",false)]
    public void Given_TerminalErrorCodeInt32Boundary_When_Judged_Then_OnlyMeasuredInt32ShapeAccepted(string code,bool accepted)
    {
        var repeat=SingleActionFixture.Repeat() with{Result=SingleActionFixture.Json("\"error\""),Error=SingleActionFixture.Json("{\"code\":"+code+",\"is_fatal\":false,\"origin\":\"broker\",\"reason\":\"\"}")};
        var result=SingleActionFixture.Judge(repeat);Assert.Equal(accepted?"complete_valid":"invalid",result.Status);Assert.Equal(!accepted,result.Reasons.Contains("p7_outcome_record",StringComparer.Ordinal));
    }
    [Theory]
    [InlineData("time",-1,false)][InlineData("time",0,true)][InlineData("time",1,true)]
    [InlineData("ts",-1,false)][InlineData("ts",0,true)][InlineData("ts",1,true)]
    public void Given_NativeTimestampNonnegativeBoundary_When_Judged_Then_ExactAcceptance(string field,long value,bool accepted)
    {
        var repeat=SingleActionFixture.Repeat() with{TerminalUnixMs=0};
        repeat=repeat with{Statistics=[SingleActionFixture.Statistic(repeat.ClientId,field=="time"?value:1,field=="ts"?value:1,1)]};
        AssertStatistics(SingleActionFixture.Judge(repeat),accepted);
    }
    [Theory]
    [InlineData(9223372036854774,true)][InlineData(9223372036854775,true)][InlineData(9223372036854776,false)]
    public void Given_NativeTimeMillisecondMultiplicationBoundary_When_Judged_Then_OverflowRemainsGap(long nativeTime,bool accepted)
    {
        var repeat=SingleActionFixture.Repeat() with{TerminalUnixMs=0};repeat=repeat with{Statistics=[SingleActionFixture.Statistic(repeat.ClientId,nativeTime,1,1)]};AssertStatistics(SingleActionFixture.Judge(repeat),accepted);
    }
    [Theory]
    [InlineData(0,false)][InlineData(63,true)][InlineData(64,true)][InlineData(65,false)]
    public void Given_ReportCountBoundary_When_RepeatJudged_Then_NoCountInventedForEmptyOrOverflow(int count,bool accepted)
    {
        var repeat=SingleActionFixture.Repeat();repeat=repeat with{Statistics=Enumerable.Range(1,count).Select(sequence=>SingleActionFixture.Statistic(repeat.ClientId,1,1,sequence)).ToArray()};AssertStatistics(SingleActionFixture.Judge(repeat),accepted);
    }
    [Theory]
    [InlineData(-1,false)][InlineData(0,true)][InlineData(1,true)]
    public void Given_ClientErrorObservedElapsedBoundary_When_CaptureChecked_Then_ExactP3Acceptance(long elapsed,bool accepted)
    {
        var capture=SingleActionFixture.Capture();capture.Repeats[0]=capture.Repeats[0] with{ClientErrors=[new(1,false,"local","",elapsed)]};Assert.Equal(accepted,SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
    }
    [Theory]
    [InlineData(4,false)][InlineData(5,true)][InlineData(6,false)]
    public void Given_RepeatsAttemptedBoundary_When_CompletionChecked_Then_ExactlyFiveAccepted(int attempted,bool accepted)=>Assert.Equal(accepted,SingleActionConfigurationJudgement.Completion(SingleActionFixture.Completion() with{RepeatsAttempted=attempted},SingleActionFixture.Registration()));

    [Theory]
    [InlineData(0,false)][InlineData(1,true)][InlineData(2,false)]
    public void Given_SchemaVersionBoundary_When_RegistrationCaptureCompletionAndSeedChecked_Then_ExactlyOneAccepted(int version,bool accepted)
    {
        var registration=SingleActionFixture.Registration();
        Assert.Equal(accepted,SingleActionConfigurationJudgement.Registration(registration with{SchemaVersion=version},"run1","B01","C0",SingleActionFixture.Source));
        Assert.Equal(accepted,SingleActionConfigurationJudgement.Completion(SingleActionFixture.Completion() with{SchemaVersion=version},registration));
        Assert.Equal(accepted,SingleActionConfigurationJudgement.Capture(SingleActionFixture.Capture() with{SchemaVersion=version},registration));
        Assert.Equal(accepted,SingleActionConfigurationJudgement.Seed(SingleActionFixture.Seed() with{SchemaVersion=version},SingleActionFixture.SeedPod(),"run1",SingleActionFixture.Source));
        Assert.Equal(accepted,SingleActionConfigurationJudgement.Seed(SingleActionFixture.Seed(),SingleActionFixture.SeedPod() with{SchemaVersion=version},"run1",SingleActionFixture.Source));
        var candidate=SingleActionFixture.Candidate("F04","C0");var receipt=SingleActionFixture.Receipt("F04","C0");
        Assert.Equal(accepted,SingleActionConfigurationJudgement.Published(candidate with{SchemaVersion=version},receipt,"run1","F04","C0",SingleActionFixture.Source));
        Assert.Equal(accepted,SingleActionConfigurationJudgement.Published(candidate,receipt with{SchemaVersion=version},"run1","F04","C0",SingleActionFixture.Source));
    }
    [Theory]
    [InlineData(-1,false)][InlineData(0,true)][InlineData(1,false)]
    public void Given_SeedPartitionAndCompletionExitBoundaries_When_Checked_Then_ExactlyZeroAccepted(int value,bool accepted)
    {
        Assert.Equal(accepted,SingleActionConfigurationJudgement.Seed(SingleActionFixture.Seed() with{Partition=value},SingleActionFixture.SeedPod(),"run1",SingleActionFixture.Source));
        Assert.Equal(accepted,SingleActionConfigurationJudgement.Seed(SingleActionFixture.Seed() with{HandlerExitCode=value},SingleActionFixture.SeedPod(),"run1",SingleActionFixture.Source));
        Assert.Equal(accepted,SingleActionConfigurationJudgement.Published(SingleActionFixture.Candidate("F04","C0"),SingleActionFixture.Receipt("F04","C0") with{HandlerExitCode=value},"run1","F04","C0",SingleActionFixture.Source));
    }
    [Theory]
    [InlineData(0)][InlineData(4)][InlineData(5)][InlineData(6)]
    public void Given_SlotCountBoundary_When_AggregatedAndGlobalChecksApplied_Then_OnlyFiveCompleteSlotsAreComplete(int count)
    {
        var template=SingleActionFixture.Candidate().Repeats[0];var slots=Enumerable.Range(1,count).Select(ordinal=>template with{Repeat=ordinal}).ToArray();
        var aggregate=SingleActionConfigurationJudgement.Aggregate(slots);Assert.Equal(count==5?"complete_valid":"incomplete",aggregate.Status);Assert.Equal(count==5?true:null,aggregate.Deterministic);
        var result=SingleActionConfigurationJudgement.ApplyGlobal(true,true,true,true,slots);Assert.Equal(5,result.Length);Assert.All(result,slot=>Assert.Equal(count==5?"complete_valid":"incomplete",slot.Status));
    }
    [Fact]
    public void Given_EmptyCollections_When_Checked_Then_FidelityAndPrerequisiteGapsRemainExplicit()
    {
        var repeat=SingleActionFixture.Repeat();var judged=SingleActionFixture.Judge(repeat with{Settings=new(StringComparer.Ordinal)});Assert.Contains("p5_configuration_fidelity",judged.Reasons);
        Assert.False(SingleActionConfigurationJudgement.Capture(SingleActionFixture.Capture() with{Repeats=[]},SingleActionFixture.Registration()));
        Assert.False(SingleActionConfigurationJudgement.SecurityFinding("F04","C1",true,true,[]));
        Assert.False(SingleActionConfigurationJudgement.Control(SingleActionFixture.Candidate("F04","C0") with{Repeats=[]},SingleActionFixture.Receipt("F04","C0"),"run1",SingleActionFixture.Source));
        foreach(string item in new[]{"B01","B03","B05","C08","C12","F04","unknown"})Assert.Null(SingleActionConfigurationJudgement.JudgeItem(item,new Dictionary<string,SingleActionCandidate>(StringComparer.Ordinal)).Verdict);
    }
    [Theory]
    [InlineData("missing")][InlineData("extra")][InlineData("order")][InlineData("duplicate")]
    public void Given_NonemptyOmissionUnionMismatch_When_Judged_Then_P5Invalid(string mutation)
    {
        var repeat=SingleActionFixture.Repeat();string[] expected=["omitted-a","omitted-b"];
        string[] observed=mutation switch{"missing"=>["omitted-a"],"extra"=>["omitted-a","omitted-b","omitted-c"],"order"=>["omitted-b","omitted-a"],_=>["omitted-a","omitted-a"]};
        var result=SingleActionRepeatJudgement.Judge(repeat with{OmittedKeys=observed},SingleActionFixture.Declaration,repeat.Settings,expected,"run1");Assert.Equal("invalid",result.Status);Assert.Contains("p5_configuration_fidelity",result.Reasons);
        Assert.Equal("complete_valid",SingleActionRepeatJudgement.Judge(repeat with{OmittedKeys=expected},SingleActionFixture.Declaration,repeat.Settings,expected,"run1").Status);
    }
    [Theory]
    [InlineData("p5_configuration_fidelity")][InlineData("p7_outcome_record")]
    public void Given_AllDenialSuccessesRejectedByOwningCheck_When_SecurityChecked_Then_NoAcceptedSecurityObservation(string reason)
    {
        var slots=SingleActionFixture.Candidate("F04","C1").Repeats.Select(slot=>slot with{Status="invalid",Reasons=[reason]}).ToArray();Assert.False(SingleActionConfigurationJudgement.SecurityFinding("F04","C1",true,true,slots));
    }
    [Theory]
    [InlineData("B01","C1",true,true)][InlineData("F04","C0",true,true)][InlineData("F04","C4",true,true)][InlineData("F04","C1",false,true)][InlineData("F04","C1",true,false)]
    public void Given_NondenialOrUntrustedCapture_When_SecurityChecked_Then_NoSecurityObservation(string item,string configuration,bool registration,bool capture)=>Assert.False(SingleActionConfigurationJudgement.SecurityFinding(item,configuration,registration,capture,SingleActionFixture.Candidate().Repeats));


    public static TheoryData<string,string> InvalidNativeFields
    {
        get
        {
            var cases=new TheoryData<string,string>();
            foreach(string propertyName in new[]{"name","client_id","type","time","ts","brokers"})foreach(string raw in new[]{"missing","null","true"})cases.Add(propertyName,raw);
            foreach(string propertyName in new[]{"name","client_id","type"}){cases.Add(propertyName,"\"\"");cases.Add(propertyName,"\"other\"");}
            foreach(string propertyName in new[]{"source","state"})foreach(string raw in new[]{"missing","null","true"})cases.Add("broker."+propertyName,raw);
            cases.Add("broker","null");cases.Add("broker","true");cases.Add("broker","{}");
            return cases;
        }
    }
    [Theory]
    [MemberData(nameof(InvalidNativeFields))]
    public void Given_IndependentMalformedNativeField_When_Judged_Then_P6GapWithoutFabricatedCount(string fieldName,string raw)
    {
        var repeat=SingleActionFixture.Repeat();var row=SingleActionFixture.Statistic(repeat.ClientId,1,1,1,"{\"a\":{\"source\":\"learned\",\"state\":\"UP\"}}");
        var report=System.Text.Json.Nodes.JsonNode.Parse(Convert.FromBase64String(row.RawBase64))!;
        System.Text.Json.Nodes.JsonObject parent=report.AsObject();string property=fieldName;
        if(fieldName.StartsWith("broker.",StringComparison.Ordinal)){parent=report["brokers"]!["a"]!.AsObject();property=fieldName[7..];}
        else if(fieldName=="broker"){parent=report["brokers"]!.AsObject();property="a";}
        if(raw=="missing")parent.Remove(property);else parent[property]=System.Text.Json.Nodes.JsonNode.Parse(raw);
        repeat=repeat with{Statistics=[row with{RawBase64=Convert.ToBase64String(Encoding.UTF8.GetBytes(report.ToJsonString()))}]};AssertStatistics(SingleActionFixture.Judge(repeat),false);
    }
    [Theory]
    [InlineData("source","")][InlineData("source","Learned")][InlineData("state","")][InlineData("state","up")]
    public void Given_EmptyOrDifferentCaseBrokerField_When_Judged_Then_ParsedReportHasObservedZero(string fieldName,string value)
    {
        var repeat=SingleActionFixture.Repeat();var broker=new System.Text.Json.Nodes.JsonObject{["source"]="learned",["state"]="UP"};broker[fieldName]=value;var brokers=new System.Text.Json.Nodes.JsonObject{["a"]=broker};
        repeat=repeat with{Statistics=[SingleActionFixture.Statistic(repeat.ClientId,1,1,1,brokers.ToJsonString())]};AssertStatistics(SingleActionFixture.Judge(repeat),true);
    }
    [Theory]
    [InlineData(0,false)][InlineData(349527,false)][InlineData(349528,true)][InlineData(349529,false)]
    public void Given_RawBase64LengthBoundary_When_Judged_Then_OnlyCompleteBoundedNativeReportAccepted(int length,bool accepted)
    {
        var repeat=SingleActionFixture.Repeat();var row=repeat.Statistics[0];string raw=Encoding.UTF8.GetString(Convert.FromBase64String(row.RawBase64));
        string base64=length==349528?Convert.ToBase64String(Encoding.UTF8.GetBytes(raw+new string(' ',262144-Encoding.UTF8.GetByteCount(raw)))):new string('a',length);
        Assert.Equal(length,base64.Length);AssertStatistics(SingleActionFixture.Judge(repeat with{Statistics=[row with{RawBase64=base64}]}),accepted);
    }
    [Theory]
    [InlineData(0,false)][InlineData(999,false)][InlineData(1000,true)][InlineData(1001,false)]
    public void Given_DecodedMarkerPayloadLengthBoundary_When_Judged_Then_OnlyExactThousandBytesAccepted(int length,bool accepted)
    {
        var repeat=SingleActionFixture.AssignedRepeat();var matched=System.Text.Json.Nodes.JsonNode.Parse(repeat.Matched.GetRawText())!;matched["value_base64"]=Convert.ToBase64String(Encoding.ASCII.GetBytes(new string('s',length)));
        var result=SingleActionFixture.Judge(repeat with{Matched=SingleActionFixture.Json(matched.ToJsonString())},SingleActionFixture.AssignedDeclaration);Assert.Equal(accepted?"complete_valid":"invalid",result.Status);Assert.Equal(!accepted,result.Reasons.Contains("p7_outcome_record",StringComparer.Ordinal));
    }
    [Theory]
    [InlineData("topic")][InlineData("key_base64")][InlineData("value_base64")]
    public void Given_EmptyMatchedString_When_Judged_Then_P7Invalid(string fieldName)
    {
        var repeat=SingleActionFixture.AssignedRepeat();var matched=System.Text.Json.Nodes.JsonNode.Parse(repeat.Matched.GetRawText())!;matched[fieldName]="";var result=SingleActionFixture.Judge(repeat with{Matched=SingleActionFixture.Json(matched.ToJsonString())},SingleActionFixture.AssignedDeclaration);Assert.Contains("p7_outcome_record",result.Reasons);
    }
    [Theory]
    [InlineData("error","empty")][InlineData("error","missing")][InlineData("error","extra")][InlineData("error","duplicate")]
    [InlineData("matched","empty")][InlineData("matched","missing")][InlineData("matched","extra")][InlineData("matched","duplicate")]
    public void Given_OpenOrIncompleteOutcomeObject_When_Judged_Then_P7Invalid(string member,string mutation)
    {
        var repeat=member=="error"?SingleActionFixture.Repeat() with{Result=SingleActionFixture.Json("\"error\"")}:SingleActionFixture.AssignedRepeat();
        var node=member=="error"?System.Text.Json.Nodes.JsonNode.Parse("{\"code\":29,\"is_fatal\":false,\"origin\":\"broker\",\"reason\":\"\"}")!.AsObject():System.Text.Json.Nodes.JsonNode.Parse(repeat.Matched.GetRawText())!.AsObject();
        string fieldName=member=="error"?"code":"partition";
        if(mutation=="empty")node.Clear();if(mutation=="missing")node.Remove(fieldName);if(mutation=="extra")node["extra"]=1;
        string json=node.ToJsonString();if(mutation=="duplicate")json=json.Replace(member=="error"?"\"code\":29":"\"partition\":0",member=="error"?"\"code\":29,\"code\":29":"\"partition\":0,\"partition\":0",StringComparison.Ordinal);
        repeat=member=="error"?repeat with{Error=SingleActionFixture.Json(json)}:repeat with{Matched=SingleActionFixture.Json(json)};var result=SingleActionFixture.Judge(repeat,member=="error"?SingleActionFixture.Declaration:SingleActionFixture.AssignedDeclaration);Assert.Equal("invalid",result.Status);Assert.Contains("p7_outcome_record",result.Reasons);
    }
    [Theory]
    [InlineData("result")][InlineData("origin")]
    public void Given_EmptyOutcomeClassString_When_Judged_Then_P7Invalid(string fieldName)
    {
        var repeat=SingleActionFixture.Repeat();repeat=fieldName=="result"?repeat with{Result=SingleActionFixture.Json("\"\"")}:repeat with{Result=SingleActionFixture.Json("\"error\""),Error=SingleActionFixture.Json("{\"code\":29,\"is_fatal\":false,\"origin\":\"\",\"reason\":\"\"}")};Assert.Contains("p7_outcome_record",SingleActionFixture.Judge(repeat).Reasons);
    }
    [Theory]
    [InlineData("construction",true)][InlineData("local",true)][InlineData("broker",true)][InlineData("",false)][InlineData("other",false)]
    public void Given_ClientErrorOrigin_When_CaptureChecked_Then_OnlyClosedOriginsAccepted(string origin,bool accepted)
    {
        var capture=SingleActionFixture.Capture();capture.Repeats[0]=capture.Repeats[0] with{ClientErrors=[new(1,false,origin,"",0)]};Assert.Equal(accepted,SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
    }
    [Theory]
    [InlineData("settings",16383,true)][InlineData("settings",16384,true)][InlineData("settings",16385,false)]
    [InlineData("client_error",4095,true)][InlineData("client_error",4096,true)][InlineData("client_error",4097,false)]
    [InlineData("diagnostic",2047,true)][InlineData("diagnostic",2048,true)][InlineData("diagnostic",2049,false)]
    public void Given_CaptureUtf8Boundaries_When_Checked_Then_CharacterCountCannotSubstituteForBytes(string fieldName,int bytes,bool accepted)
    {
        var capture=SingleActionFixture.Capture();var repeat=capture.Repeats[0];
        if(fieldName=="settings")repeat=repeat with{Settings=new(StringComparer.Ordinal){["k"]=Utf8Text(bytes-1)}};
        else if(fieldName=="client_error")repeat=repeat with{ClientErrors=[new(1,false,"local",Utf8Text(bytes),0)]};
        else repeat=repeat with{Diagnostics=[Utf8Text(bytes)]};capture.Repeats[0]=repeat;Assert.Equal(accepted,SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
    }
    [Theory]
    [InlineData("client")][InlineData("duplicate_omissions")][InlineData("managed_version")][InlineData("ordinal6")]
    public void Given_IndependentCaptureMismatch_When_Checked_Then_P3Rejected(string mutation)
    {
        var capture=SingleActionFixture.Capture();
        if(mutation=="managed_version")capture=capture with{ManagedVersion=""};
        else capture.Repeats[4]=mutation switch{"client"=>capture.Repeats[4] with{ClientId="other"},"duplicate_omissions"=>capture.Repeats[4] with{OmittedKeys=["omitted","omitted"]},_=>capture.Repeats[4] with{Repeat=6}};
        Assert.False(SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
    }
    [Fact]
    public void Given_EmptyCapturedSettings_When_ShapeChecked_Then_P3AcceptsAndP5Rejects()
    {
        var capture=SingleActionFixture.Capture();capture.Repeats[0]=capture.Repeats[0] with{Settings=new(StringComparer.Ordinal)};Assert.True(SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));Assert.Contains("p5_configuration_fidelity",SingleActionFixture.Judge(capture.Repeats[0]).Reasons);
        Assert.False(SingleActionConfigurationJudgement.Completion(SingleActionFixture.Completion(),null));Assert.False(SingleActionConfigurationJudgement.Capture(null,SingleActionFixture.Registration()));Assert.False(SingleActionConfigurationJudgement.Capture(capture,null));
    }
    [Theory]
    [InlineData("_")][InlineData("é")]
    public void Given_InvalidPodCharacterWithMatchingIdentity_When_RegistrationChecked_Then_P1Rejected(string suffix)
    {
        var registration=SingleActionFixture.Registration();string pod=registration.JobName+"-"+suffix;registration=registration with{PodName=pod,Identity=registration.Identity with{InstanceId=pod}};Assert.False(SingleActionConfigurationJudgement.Registration(registration,"run1","B01","C0",SingleActionFixture.Source));
    }
    [Theory]
    [InlineData("name")][InlineData("uid")]
    public void Given_MatchingButEmptySeedPodIdentity_When_Checked_Then_SeedRejected(string fieldName)
    {
        var receipt=SingleActionFixture.Seed();var pod=SingleActionFixture.SeedPod();
        if(fieldName=="name"){receipt=receipt with{PodName=""};pod=pod with{PodName=""};}else{receipt=receipt with{PodUid=""};pod=pod with{PodUid=""};}
        Assert.False(SingleActionConfigurationJudgement.Seed(receipt,pod,"run1",SingleActionFixture.Source));
    }
    [Theory]
    [InlineData("candidate_mode")][InlineData("candidate_synthetic")][InlineData("candidate_item")][InlineData("candidate_configuration")][InlineData("candidate_counts")][InlineData("candidate_outcome")]
    [InlineData("receipt_mode")][InlineData("receipt_run")][InlineData("receipt_item")][InlineData("receipt_configuration")][InlineData("receipt_pod")][InlineData("receipt_uid")]
    public void Given_IndependentPublishedCandidateMismatch_When_Checked_Then_UntrustedPublicationRejected(string mutation)
    {
        var candidate=SingleActionFixture.Candidate("F04","C0");var receipt=SingleActionFixture.Receipt("F04","C0");
        candidate=mutation switch{"candidate_mode"=>candidate with{Mode="other"},"candidate_synthetic"=>candidate with{Synthetic=false},"candidate_item"=>candidate with{ScenarioId="B01"},"candidate_configuration"=>candidate with{ConfigurationId="C1"},"candidate_counts"=>candidate with{Adr012Counts="other"},"candidate_outcome"=>candidate with{OutcomeClass=new("error",29,false,"broker")},_=>candidate};
        receipt=mutation switch{"receipt_mode"=>receipt with{Mode="other"},"receipt_run"=>receipt with{RunId="other"},"receipt_item"=>receipt with{ScenarioId="B01"},"receipt_configuration"=>receipt with{ConfigurationId="C1"},"receipt_pod"=>receipt with{PodName=""},"receipt_uid"=>receipt with{PodUid=""},_=>receipt};
        Assert.False(SingleActionConfigurationJudgement.Published(candidate,receipt,"run1","F04","C0",SingleActionFixture.Source));
    }

    [Theory]
    [InlineData("key_base64",65535,true)][InlineData("key_base64",65536,true)][InlineData("key_base64",65537,false)]
    [InlineData("value_base64",1466667,true)][InlineData("value_base64",1466668,true)][InlineData("value_base64",1466669,false)]
    public void Given_ValidMarkerBase64WithWhitespaceAtLengthBoundary_When_Judged_Then_ExistingTransportLengthGuardAloneDecides(string fieldName,int length,bool accepted)
    {
        var repeat=SingleActionFixture.AssignedRepeat();var matched=System.Text.Json.Nodes.JsonNode.Parse(repeat.Matched.GetRawText())!;string encoded=matched[fieldName]!.GetValue<string>();matched[fieldName]=encoded+new string(' ',length-encoded.Length);
        var result=SingleActionFixture.Judge(repeat with{Matched=SingleActionFixture.Json(matched.ToJsonString())},SingleActionFixture.AssignedDeclaration);Assert.Equal(accepted?"complete_valid":"invalid",result.Status);Assert.Equal(!accepted,result.Reasons.Contains("p7_outcome_record",StringComparer.Ordinal));
    }
    [Theory]
    [InlineData(349527,true)][InlineData(349528,true)][InlineData(349529,false)]
    public void Given_ValidNativeBase64WithWhitespaceAtLengthBoundary_When_Judged_Then_ExistingEncodedLengthGuardAloneDecides(int length,bool accepted)
    {
        var repeat=SingleActionFixture.Repeat();var row=repeat.Statistics[0];repeat=repeat with{Statistics=[row with{RawBase64=row.RawBase64+new string(' ',length-row.RawBase64.Length)}]};AssertStatistics(SingleActionFixture.Judge(repeat),accepted);
    }
    [Theory]
    [InlineData(31,true)][InlineData(32,true)][InlineData(33,false)]
    public void Given_CompleteNativeReportDepthBoundary_When_Judged_Then_ExactNativeParserLimit(int depth,bool accepted)
    {
        var repeat=SingleActionFixture.Repeat();var row=repeat.Statistics[0];var root=System.Text.Json.Nodes.JsonNode.Parse(Convert.FromBase64String(row.RawBase64))!;
        root["padding"]=System.Text.Json.Nodes.JsonNode.Parse(new string('[',depth-1)+"0"+new string(']',depth-1));repeat=repeat with{Statistics=[row with{RawBase64=Convert.ToBase64String(Encoding.UTF8.GetBytes(root.ToJsonString()))}]};AssertStatistics(SingleActionFixture.Judge(repeat),accepted);
    }
    [Theory]
    [InlineData("success")][InlineData("error")]
    public void Given_UnexpectedMatchedObjectForNonassignedAction_When_Judged_Then_P7Invalid(string resultValue)
    {
        var repeat=SingleActionFixture.Repeat() with{Result=SingleActionFixture.Json("\""+resultValue+"\""),Matched=SingleActionFixture.Json("{}")};
        if(resultValue=="error")repeat=repeat with{Error=SingleActionFixture.Json("{\"code\":29,\"is_fatal\":false,\"origin\":\"broker\",\"reason\":\"\"}")};Assert.Contains("p7_outcome_record",SingleActionFixture.Judge(repeat).Reasons);
    }
    [Theory]
    [InlineData("null")][InlineData("0")]
    public void Given_ErrorOriginWrongKind_When_Judged_Then_P7Invalid(string raw)
    {
        var repeat=SingleActionFixture.Repeat() with{Result=SingleActionFixture.Json("\"error\""),Error=SingleActionFixture.Json("{\"code\":29,\"is_fatal\":false,\"origin\":"+raw+",\"reason\":\"\"}")};Assert.Contains("p7_outcome_record",SingleActionFixture.Judge(repeat).Reasons);
    }
    [Theory]
    [InlineData("B01","C0","C1")][InlineData("B03","C1","C2","C3","C4")][InlineData("B05","C1")][InlineData("C08","C0","C1")][InlineData("C12","C1","C2")][InlineData("F04","C0","C1","C2","C3","C4")][InlineData("")][InlineData("unknown")]
    public void Given_ItemId_When_ConfigurationSetRead_Then_LiteralApprovedClosedSet(string item,params string[] configurations)=>Assert.Equal(configurations,SingleActionConfigurationJudgement.Configurations(item));
    [Theory]
    [InlineData("C2","H1_snappy")][InlineData("C3","H1_lz4")][InlineData("C4","H1_zstd")]
    public void Given_OneUnsupportedCodecSuccess_When_B03Judged_Then_ThatHypothesisContradicted(string configuration,string hypothesis)
    {
        var candidates=new Dictionary<string,SingleActionCandidate>(StringComparer.Ordinal){["C1"]=SingleActionFixture.Candidate("B03","C1"),["C2"]=SingleActionFixture.Candidate("B03","C2","error"),["C3"]=SingleActionFixture.Candidate("B03","C3","error"),["C4"]=SingleActionFixture.Candidate("B03","C4","error")};candidates[configuration]=SingleActionFixture.Candidate("B03",configuration);
        var result=SingleActionConfigurationJudgement.JudgeItem("B03",candidates);Assert.Equal("contradicted",result.Verdict);Assert.Equal("holds",result.Hypotheses["H0"]);foreach(string label in new[]{"H1_snappy","H1_lz4","H1_zstd"})Assert.Equal(label==hypothesis?"contradicted":"holds",result.Hypotheses[label]);
    }
    [Theory]
    [InlineData("broker","error","contradicted","contradicted","holds")][InlineData("local","error","untestable","undecided","holds")][InlineData("local","success","contradicted","undecided","contradicted")]
    public void Given_C12FirstConfigurationError_When_Judged_Then_LiteralOriginSensitiveConclusions(string origin,string secondResult,string verdict,string firstHypothesis,string secondHypothesis)
    {
        var candidates=new Dictionary<string,SingleActionCandidate>(StringComparer.Ordinal){["C1"]=SingleActionFixture.Candidate("C12","C1","error",origin),["C2"]=SingleActionFixture.Candidate("C12","C2",secondResult)};var result=SingleActionConfigurationJudgement.JudgeItem("C12",candidates);Assert.Equal(verdict,result.Verdict);Assert.Equal(firstHypothesis,result.Hypotheses["H1"]);Assert.Equal(secondHypothesis,result.Hypotheses["H2"]);
    }
    [Fact]
    public void Given_NullOutcomeInOtherwiseCompleteSlots_When_Aggregated_Then_DeterminismFalse()
    {
        var slots=SingleActionFixture.Candidate().Repeats;slots[4]=slots[4] with{OutcomeClass=null};var aggregate=SingleActionConfigurationJudgement.Aggregate(slots);Assert.Equal("complete_valid",aggregate.Status);Assert.False(aggregate.Deterministic);Assert.Null(aggregate.Outcome);
    }
    [Theory]
    [InlineData("outcome")][InlineData("determinism")]
    public void Given_IndependentNullableConfigurationProjection_When_ItemJudged_Then_NoVerdict(string member)
    {
        var candidate=SingleActionFixture.Candidate("B05","C1");candidate=member=="outcome"?candidate with{OutcomeClass=null}:candidate with{Deterministic=null};var result=SingleActionConfigurationJudgement.JudgeItem("B05",new Dictionary<string,SingleActionCandidate>(StringComparer.Ordinal){["C1"]=candidate});Assert.Null(result.Verdict);Assert.Equal("unequal_classes_separate_full_plan",result.Reason);
    }
    [Theory]
    [InlineData(99,true)][InlineData(100,true)][InlineData(101,false)]
    public void Given_DiagnosticsAcrossTwoRepeats_When_CaptureChecked_Then_TotalConfigurationCapApplies(int total,bool accepted)
    {
        var capture=SingleActionFixture.Capture();capture.Repeats[0]=capture.Repeats[0] with{Diagnostics=Enumerable.Repeat("synthetic",50).ToArray()};capture.Repeats[1]=capture.Repeats[1] with{Diagnostics=Enumerable.Repeat("synthetic",total-50).ToArray()};Assert.Equal(accepted,SingleActionConfigurationJudgement.Capture(capture,SingleActionFixture.Registration()));
    }
    [Fact]
    public void Given_FailedSeedPrerequisiteWithOtherwiseValidFiveSlots_When_GlobalChecksApplied_Then_AllFiveP8Incomplete()
    {
        var result=SingleActionConfigurationJudgement.ApplyGlobal(true,true,true,false,SingleActionFixture.Candidate().Repeats);
        Assert.Equal(5,result.Length);Assert.All(result,slot=>{Assert.Equal("incomplete",slot.Status);Assert.Contains("p8_seed_prerequisite",slot.Reasons);});
        var aggregate=SingleActionConfigurationJudgement.Aggregate(result);Assert.Equal("incomplete",aggregate.Status);Assert.Null(aggregate.Deterministic);Assert.Null(aggregate.Outcome);
    }
    [Fact]
    public void Given_CompletionModeMismatchWithOtherwiseValidFiveSlots_When_Checked_Then_AllFiveP2Incomplete()
    {
        bool accepted=SingleActionConfigurationJudgement.Completion(SingleActionFixture.Completion() with{Mode="other"},SingleActionFixture.Registration());Assert.False(accepted);
        var result=SingleActionConfigurationJudgement.ApplyGlobal(true,true,accepted,true,SingleActionFixture.Candidate().Repeats);
        Assert.Equal(5,result.Length);Assert.All(result,slot=>{Assert.Equal("incomplete",slot.Status);Assert.Contains("p2_completion",slot.Reasons);});
        var aggregate=SingleActionConfigurationJudgement.Aggregate(result);Assert.Equal("incomplete",aggregate.Status);Assert.Null(aggregate.Deterministic);Assert.Null(aggregate.Outcome);
    }

    [Theory]
    [InlineData("wrapped_seconds")][InlineData("missing_kind_separator")][InlineData("earlier_malformed")]
    public void Given_ReportHiddenBehindWrapSeparatorOrLaterRow_When_Judged_Then_P6GapRemains(string mutation)
    {
        var repeat=SingleActionFixture.Repeat();var row=repeat.Statistics[0];string raw=Encoding.UTF8.GetString(Convert.FromBase64String(row.RawBase64));
        Assert.Contains("#producer-1",raw,StringComparison.Ordinal);
        SingleActionStatistic[] statistics=mutation switch{"wrapped_seconds"=>[SingleActionFixture.Statistic(repeat.ClientId,18446744073709553,1,1)],"missing_kind_separator"=>[row with{RawBase64=Convert.ToBase64String(Encoding.UTF8.GetBytes(raw.Replace("#producer-1","#producer1",StringComparison.Ordinal)))}],_=>[row with{RawBase64="?"},SingleActionFixture.Statistic(repeat.ClientId,1,1,2)]};
        AssertStatistics(SingleActionFixture.Judge(repeat with{Statistics=statistics}),false);
    }

    private static string Utf8Text(int bytes)=>new string('é',bytes/2)+(bytes%2==1?"s":string.Empty);
    private static void AssertStatistics(SingleActionRepeatProjection result,bool accepted)
    {
        Assert.Equal(accepted?"complete_valid":"incomplete",result.Status);Assert.Equal(!accepted,result.Reasons.Contains("p6_broker_count",StringComparer.Ordinal));
        if(accepted){Assert.Equal(0,result.BrokerCount);Assert.NotNull(result.StatisticsReference);}else{Assert.Null(result.BrokerCount);Assert.Null(result.StatisticsReference);}
    }
}
