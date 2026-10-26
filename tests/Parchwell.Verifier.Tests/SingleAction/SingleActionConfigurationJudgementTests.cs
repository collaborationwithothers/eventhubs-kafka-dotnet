using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class SingleActionConfigurationJudgementTests
{
    [Fact]
    public void Given_LiteralRegistrationCaptureCompletion_When_Checked_Then_Accepted()
    {
        Assert.True(Registration(SingleActionFixture.Registration()));Assert.True(SingleActionConfigurationJudgement.Capture(SingleActionFixture.Capture(),SingleActionFixture.Registration()));Assert.True(SingleActionConfigurationJudgement.Completion(SingleActionFixture.Completion(),SingleActionFixture.Registration()));
    }
    private static bool Registration(SingleActionRegistration? value)=>SingleActionConfigurationJudgement.Registration(value,SingleActionFixture.Run,"B01","C0",SingleActionFixture.Source);

    [Theory]
    [InlineData("missing")][InlineData("schema")][InlineData("mode")][InlineData("job")][InlineData("pod")][InlineData("uid")][InlineData("source")][InlineData("run")][InlineData("item")][InlineData("configuration")][InlineData("instance")]
    public void Given_RegistrationMismatch_When_Checked_Then_P1Rejected(string mutation)
    {
        var r=SingleActionFixture.Registration();
        r=mutation switch{"missing"=>null,"schema"=>r with{SchemaVersion=2},"mode"=>r with{Mode="other"},"job"=>r with{JobName="other"},"pod"=>r with{PodName="other"},"uid"=>r with{PodUid=""},"source"=>r with{SourceCommit=new string('2',40)},"run"=>r with{Identity=r.Identity with{RunId="other"}},"item"=>r with{Identity=r.Identity with{ScenarioId="B03"}},"configuration"=>r with{Identity=r.Identity with{ConfigurationId="C1"}},_=>r with{Identity=r.Identity with{InstanceId="other"}}};
        Assert.False(Registration(r));
    }
    [Theory]
    [InlineData("missing")][InlineData("cleanup")][InlineData("exit1")][InlineData("exit-1")][InlineData("exit2")][InlineData("attempts")][InlineData("job")][InlineData("pod")][InlineData("uid")][InlineData("source")][InlineData("identity")]
    public void Given_CompletionFailure_When_Judged_Then_AllFiveIncompleteAndNoVerdict(string mutation)
    {
        var c=SingleActionFixture.Completion();
        c=mutation switch{"missing"=>null,"cleanup"=>c with{CleanupCompleted=false},"exit1"=>c with{HandlerExitCode=1},"exit-1"=>c with{HandlerExitCode=-1},"exit2"=>c with{HandlerExitCode=2},"attempts"=>c with{RepeatsAttempted=4},"job"=>c with{JobName="other"},"pod"=>c with{PodName="other"},"uid"=>c with{PodUid="other"},"source"=>c with{SourceCommit="other"},_=>c with{Identity=c.Identity with{RunId="other"}}};
        bool accepted=SingleActionConfigurationJudgement.Completion(c,SingleActionFixture.Registration());Assert.False(accepted);
        var repeats=SingleActionConfigurationJudgement.ApplyGlobal(true,true,accepted,true,SingleActionFixture.Candidate().Repeats);
        Assert.Equal(5,repeats.Length);Assert.All(repeats,r=>{Assert.Equal("incomplete",r.Status);Assert.Contains("p2_completion",r.Reasons);});
        var aggregate=SingleActionConfigurationJudgement.Aggregate(repeats);Assert.Equal("incomplete",aggregate.Status);Assert.Null(aggregate.Deterministic);Assert.Null(aggregate.Outcome);
        var candidate=SingleActionFixture.Candidate("B05","C1") with{Repeats=repeats,Status=aggregate.Status,Deterministic=null,OutcomeClass=null};
        Assert.Null(SingleActionConfigurationJudgement.JudgeItem("B05",new Dictionary<string,SingleActionCandidate>(StringComparer.Ordinal){["C1"]=candidate}).Verdict);
    }
    [Theory]
    [InlineData("missing")][InlineData("extra")][InlineData("duplicate")][InlineData("ordinal")][InlineData("schema")][InlineData("mode")][InlineData("synthetic")][InlineData("identity")][InlineData("source")][InlineData("pod")][InlineData("uid")][InlineData("job")][InlineData("version")]
    public void Given_CaptureMismatch_When_Checked_Then_P3Rejected(string mutation)
    {
        var c=SingleActionFixture.Capture();
        c=mutation switch{"missing"=>c with{Repeats=c.Repeats[..4]},"extra"=>c with{Repeats=[..c.Repeats,SingleActionFixture.Repeat(6)]},"duplicate"=>c with{Repeats=[c.Repeats[0],c.Repeats[0],..c.Repeats[2..]]},"ordinal"=>c with{Repeats=[SingleActionFixture.Repeat(0),..c.Repeats[1..]]},"schema"=>c with{SchemaVersion=2},"mode"=>c with{Mode="other"},"synthetic"=>c with{Synthetic=false},"identity"=>c with{Identity=c.Identity with{InstanceId="other"}},"source"=>c with{SourceCommit="other"},"pod"=>c with{PodName="other"},"uid"=>c with{PodUid="other"},"job"=>c with{JobName="other"},_=>c with{NativeVersion=""}};
        Assert.False(SingleActionConfigurationJudgement.Capture(c,SingleActionFixture.Registration()));
    }
    [Theory]
    [InlineData(false,true,false,"invalid")][InlineData(true,false,false,"invalid")][InlineData(true,true,false,"incomplete")][InlineData(true,true,true,"complete_valid")]
    public void Given_GlobalChecks_When_Applied_Then_InvalidPrecedesCompletion(bool registration,bool capture,bool completion,string expected)
    {
        var repeats=SingleActionConfigurationJudgement.ApplyGlobal(registration,capture,completion,true,SingleActionFixture.Candidate().Repeats);Assert.All(repeats,r=>Assert.Equal(expected,r.Status));
    }
    [Fact]
    public void Given_InvalidFidelityAndMissingCompletion_When_Applied_Then_InvalidAndBothReasonsRetained()
    {
        var repeats=SingleActionFixture.Candidate().Repeats;
        repeats[0]=repeats[0] with{Status="invalid",Reasons=["p5_configuration_fidelity"]};
        var result=SingleActionConfigurationJudgement.ApplyGlobal(true,true,false,true,repeats);
        Assert.Equal("invalid",result[0].Status);Assert.Contains("p5_configuration_fidelity",result[0].Reasons);Assert.Contains("p2_completion",result[0].Reasons);
        Assert.Equal("invalid",SingleActionConfigurationJudgement.Aggregate(result).Status);
    }

    [Theory]
    [InlineData("result")][InlineData("code")][InlineData("fatal")][InlineData("origin")]
    public void Given_UnequalTupleMember_When_Aggregated_Then_DeterminismFalseAndNoClass(string member)
    {
        var c=SingleActionFixture.Candidate(result:"error");var outcome=c.OutcomeClass!;
        outcome=member switch{"result"=>new("success",null,null,null),"code"=>outcome with{ErrorCode=30},"fatal"=>outcome with{IsFatal=true},_=>outcome with{Origin="local"}};
        var repeats=c.Repeats.ToArray();repeats[4]=repeats[4] with{OutcomeClass=outcome};var result=SingleActionConfigurationJudgement.Aggregate(repeats);Assert.Equal("complete_valid",result.Status);Assert.False(result.Deterministic);Assert.Null(result.Outcome);
    }
    [Fact]
    public void Given_EmptySlots_When_Aggregated_Then_IncompleteWithNullDeterminism()
    {
        var result=SingleActionConfigurationJudgement.Aggregate([]);Assert.Equal("incomplete",result.Status);Assert.Null(result.Deterministic);Assert.Null(result.Outcome);
    }
    [Theory]
    [InlineData("C1")][InlineData("C2")][InlineData("C3")]
    public void Given_DenialSuccessAndOtherIncompleteRepeat_When_Checked_Then_SecurityFindingRetained(string configuration)
    {
        var repeats=SingleActionFixture.Candidate("F04",configuration).Repeats;repeats[4]=repeats[4] with{Status="incomplete",Reasons=["p6_broker_count"],BrokerCount=null,StatisticsReference=null};
        Assert.True(SingleActionConfigurationJudgement.SecurityFinding("F04",configuration,true,true,repeats));Assert.False(SingleActionConfigurationJudgement.SecurityFinding("F04",configuration,false,true,repeats));
        repeats[0]=repeats[0] with{Reasons=["p5_configuration_fidelity"]};
        Assert.True(SingleActionConfigurationJudgement.SecurityFinding("F04",configuration,true,true,repeats));
    }
}
