using System.Text;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class SingleActionSeedAndItemTests
{
    [Fact]
    public void Given_LiteralLiveDeliveryAndCollectedPod_When_Checked_Then_SeedAccepted()=>Assert.True(Seed(SingleActionFixture.Seed(),SingleActionFixture.SeedPod()));
    private static bool Seed(SingleActionSeedReceipt? receipt,SingleActionSeedPod? pod)=>SingleActionConfigurationJudgement.Seed(receipt,pod,SingleActionFixture.Run,SingleActionFixture.Source);
    [Theory]
    [InlineData("missing")][InlineData("smoke")][InlineData("schema")][InlineData("run")][InlineData("source")][InlineData("job")][InlineData("pod")][InlineData("uid")][InlineData("account")][InlineData("topic")][InlineData("partition")][InlineData("key")][InlineData("base64")][InlineData("hash")][InlineData("delivery")][InlineData("error")][InlineData("exit")][InlineData("cleanup")]
    public void Given_SeedReceiptMismatch_When_Checked_Then_P8Rejected(string mutation)
    {
        var r=SingleActionFixture.Seed();r=mutation switch{"missing"=>null,"smoke"=>r with{Mode="single-action-seed-smoke"},"schema"=>r with{SchemaVersion=2},"run"=>r with{RunId="other"},"source"=>r with{SourceCommit="other"},"job"=>r with{JobName="other"},"pod"=>r with{PodName="other"},"uid"=>r with{PodUid="other"},"account"=>r with{ServiceAccount="probe"},"topic"=>r with{Topic="document-events"},"partition"=>r with{Partition=1},"key"=>r with{KeyBase64=Convert.ToBase64String(Encoding.UTF8.GetBytes("other"))},"base64"=>r with{KeyBase64="?"},"hash"=>r with{ValueSha256=new string('a',64)},"delivery"=>r with{Delivered=false},"error"=>r with{Error=SingleActionFixture.Json("{}")},"exit"=>r with{HandlerExitCode=1},_=>r with{CleanupCompleted=false}};
        Assert.False(Seed(r,SingleActionFixture.SeedPod()));
    }
    [Theory]
    [InlineData("missing")][InlineData("schema")][InlineData("job")][InlineData("pod")][InlineData("uid")][InlineData("account")][InlineData("kind")][InlineData("owner")]
    public void Given_SeedPodMismatch_When_Checked_Then_P8Rejected(string mutation)
    {
        var p=SingleActionFixture.SeedPod();p=mutation switch{"missing"=>null,"schema"=>p with{SchemaVersion=2},"job"=>p with{JobName="other"},"pod"=>p with{PodName="other"},"uid"=>p with{PodUid="other"},"account"=>p with{ServiceAccount="probe"},"kind"=>p with{OwnerKind="ReplicaSet"},_=>p with{OwnerName="other"}};Assert.False(Seed(SingleActionFixture.Seed(),p));
    }
    [Theory]
    [InlineData("missing_candidate")][InlineData("missing_receipt")][InlineData("source")][InlineData("run")][InlineData("status")][InlineData("determinism")][InlineData("error")][InlineData("slots")][InlineData("receipt_exit")][InlineData("receipt_cleanup")][InlineData("receipt_path")][InlineData("receipt_job")][InlineData("receipt_source")]
    public void Given_C0ControlMismatch_When_Checked_Then_P8Rejected(string mutation)
    {
        var c=SingleActionFixture.Candidate("F04","C0");var r=SingleActionFixture.Receipt("F04","C0");
        c=mutation switch{"missing_candidate"=>null,"source"=>c with{SourceCommit="other"},"run"=>c with{RunId="other"},"status"=>c with{Status="incomplete"},"determinism"=>c with{Deterministic=false},"error"=>SingleActionFixture.Candidate("F04","C0","error"),"slots"=>c with{Repeats=c.Repeats[..4]},_=>c};
        r=mutation switch{"missing_receipt"=>null,"receipt_exit"=>r with{HandlerExitCode=1},"receipt_cleanup"=>r with{CleanupCompleted=false},"receipt_path"=>r with{CandidateName="other"},"receipt_job"=>r with{JobName="other"},"receipt_source"=>r with{SourceCommit="other"},_=>r};
        Assert.False(SingleActionConfigurationJudgement.Control(c,r,SingleActionFixture.Run,SingleActionFixture.Source));
    }
    [Fact]
    public void Given_CompletedSuccessfulC0AndFinalReceipt_When_Checked_Then_ControlAccepted()=>Assert.True(SingleActionConfigurationJudgement.Control(SingleActionFixture.Candidate("F04","C0"),SingleActionFixture.Receipt("F04","C0"),SingleActionFixture.Run,SingleActionFixture.Source));

    [Theory]
    [InlineData("B01","C1","error","broker","confirmed")][InlineData("B01","C1","success",null,"contradicted")]
    [InlineData("B03","C4","error","broker","confirmed")][InlineData("B03","C4","success",null,"contradicted")]
    [InlineData("B05","C1","success",null,"confirmed")][InlineData("B05","C1","error","broker","confirmed")]
    [InlineData("C08","C1","success",null,"undocumented")][InlineData("C08","C1","error","broker","undocumented")]
    [InlineData("C12","C2","error","broker","confirmed")][InlineData("C12","C2","success",null,"contradicted")][InlineData("C12","C2","error","local","untestable")]
    [InlineData("F04","C4","success",null,"undocumented")][InlineData("F04","C4","error","broker","untestable")]
    public void Given_AllDeterministicConfigurations_When_ItemJudged_Then_LiteralMatrix(string item,string changed,string result,string? origin,string verdict)
    {
        var candidates=Candidates(item);candidates[changed]=SingleActionFixture.Candidate(item,changed,result,origin);Assert.Equal(verdict,SingleActionConfigurationJudgement.JudgeItem(item,candidates).Verdict);
    }
    [Theory]
    [InlineData("B01","C0")][InlineData("B03","C1")][InlineData("C08","C0")][InlineData("F04","C0")][InlineData("F04","C4")]
    public void Given_FailedControl_When_ItemJudged_Then_Untestable(string item,string control)
    {
        var candidates=Candidates(item);candidates[control]=SingleActionFixture.Candidate(item,control,"error","broker");Assert.Equal("untestable",SingleActionConfigurationJudgement.JudgeItem(item,candidates).Verdict);
    }
    [Theory]
    [InlineData("B01")][InlineData("B03")][InlineData("B05")][InlineData("C08")][InlineData("C12")][InlineData("F04")]
    public void Given_MissingIncompleteUnequalOrConstruction_When_ItemJudged_Then_ExactDisposition(string item)
    {
        string configuration=SingleActionConfigurationJudgement.Configurations(item)[^1];
        foreach(string mutation in new[]{"missing","incomplete","unequal","construction"})
        {
            var candidates=Candidates(item);var c=candidates[configuration];
            if(mutation=="missing")candidates.Remove(configuration);
            else candidates[configuration]=mutation switch{"incomplete"=>c with{Status="incomplete",Deterministic=null,OutcomeClass=null},"unequal"=>c with{Deterministic=false,OutcomeClass=null},_=>SingleActionFixture.Candidate(item,configuration,"error","construction")};
            Assert.Equal(mutation=="construction"?"untestable":null,SingleActionConfigurationJudgement.JudgeItem(item,candidates).Verdict);
        }
    }
    [Fact]
    public void Given_B05OpposingOutcomes_When_ItemJudged_Then_SourceConclusionsReverse()
    {
        var success=SingleActionConfigurationJudgement.JudgeItem("B05",Candidates("B05"));
        var error=SingleActionConfigurationJudgement.JudgeItem("B05",new Dictionary<string,SingleActionCandidate>(StringComparer.Ordinal){["C1"]=SingleActionFixture.Candidate("B05","C1","error")});
        Assert.Equal("holds",success.Hypotheses["H1"]);Assert.Equal("contradicted",success.Hypotheses["H2"]);Assert.Equal("contradicted",error.Hypotheses["H1"]);Assert.Equal("holds",error.Hypotheses["H2"]);
    }
    [Fact]
    public void Given_SecurityFindingAndMissingOtherConfigurations_When_ItemJudged_Then_NoVerdictRequiresHari()
    {
        var result=SingleActionConfigurationJudgement.JudgeItem("F04",new Dictionary<string,SingleActionCandidate>(StringComparer.Ordinal){["C2"]=SingleActionFixture.Candidate("F04","C2") with{SecurityFinding=true}});Assert.Null(result.Verdict);Assert.Equal("security_finding_requires_hari",result.Reason);
    }
    [Fact]
    public void Given_ExactC2MarkerWithoutStatistics_When_Checked_Then_SecurityFindingRetainedButWrongBytesRejected()
    {
        var declaration=SingleActionFixture.Declaration with{Action="assign_consume",Topic="document-state",WindowMs=120000};
        string key=Convert.ToBase64String(Encoding.UTF8.GetBytes("f04-seed:run1"));string value=Convert.ToBase64String(Encoding.ASCII.GetBytes(new string('s',1000)));
        var repeat=SingleActionFixture.Repeat() with{Action="assign_consume",Statistics=[],Matched=SingleActionFixture.Json($$"""{"topic":"document-state","partition":0,"key_base64":"{{key}}","value_base64":"{{value}}"}""")};
        var result=SingleActionFixture.Judge(repeat,declaration);Assert.Equal("incomplete",result.Status);Assert.DoesNotContain("p7_outcome_record",result.Reasons);
        Assert.True(SingleActionConfigurationJudgement.SecurityFinding("F04","C2",true,true,[result]));
        result=SingleActionFixture.Judge(repeat with{Matched=SingleActionFixture.Json("null")},declaration);Assert.False(SingleActionConfigurationJudgement.SecurityFinding("F04","C2",true,true,[result]));
    }

    private static Dictionary<string,SingleActionCandidate> Candidates(string item)=>SingleActionConfigurationJudgement.Configurations(item).ToDictionary(c=>c,c=>SingleActionFixture.Candidate(item,c,item=="B03"&&c!="C1"||item=="C12"&&c=="C2"||item=="F04"&&c is "C1" or "C2" or "C3"?"error":"success"),StringComparer.Ordinal);
}
