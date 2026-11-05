using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Judge;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

#pragma warning disable CA1707
public sealed class B10JudgeChecksTests
{
    [Theory]
    [InlineData("empty", null, null)]
    [InlineData("first", null, null)]
    [InlineData("restart", null, null)]
    [InlineData("pair", true, true)]
    [InlineData("reverse", true, true)]
    [InlineData("incomplete", true, null)]
    [InlineData("excess", true, false)]
    [InlineData("excess-incomplete", true, false)]
    [InlineData("duplicate-instance", false, false)]
    [InlineData("duplicate-job", false, true)]
    [InlineData("predecessor", false, true)]
    [InlineData("first-predecessor", false, true)]
    [InlineData("source", false, true)]
    [InlineData("run", false, true)]
    [InlineData("configuration", false, true)]
    [InlineData("scenario", false, true)]
    [InlineData("phase", false, true)]
    [InlineData("repeat", false, true)]
    [InlineData("path", false, true)]
    [InlineData("expected", false, true)]
    [InlineData("pod", false, true)]
    [InlineData("job", false, true)]
    [InlineData("malformed", null, false)]
    [InlineData("source-missing", false, null)]
    public void Given_RegistrationEvidence_When_Evaluated_Then_PreservesIndependentBindingAndCount(string change, bool? binding, bool? count)
    {
        ProducerRegistration first=Registration(false), second=Registration(true);
        first=change switch
        {
            "source" or "source-missing" => first with {SourceCommit=new string('b',40)},
            "run" => first with {Identity=first.Identity with {RunId="foreign"}},
            "configuration" => first with {Identity=first.Identity with {ConfigurationId="C1"}},
            "scenario" => first with {Identity=first.Identity with {ScenarioId="A01"}},
            "phase" => first with {Phase="C1"},
            "repeat" => first with {RepeatNumber=2},
            "expected" => first with {Identity=first.Identity with {ExpectedInstanceId="other"}},
            "pod" => first with {PodName="other"},
            "job" => first with {JobName="foreign"},
            "first-predecessor" => first with {PredecessorInstanceId="other"},
            _ => first
        };
        second=change switch
        {
            "duplicate-instance" => second with {Identity=first.Identity},
            "duplicate-job" => second with {JobName=first.JobName},
            "predecessor" => second with {PredecessorInstanceId="other"},
            _=>second
        };
        var registrations=new List<(string PathInstance,ProducerRegistration Registration)>();
        if(change is not ("empty" or "restart")) registrations.Add((change=="path"?"other":"pod-first",first));
        if(change is not ("empty" or "first" or "malformed" or "source-missing")) registrations.Add(("pod-second",second));
        if(change=="reverse")registrations.Reverse();
        B10RegistrationJudgement result=B10JudgeChecks.EvaluateRegistrations(Options(),1,
            change is not ("incomplete" or "excess-incomplete"),registrations,
            change.StartsWith("excess",StringComparison.Ordinal)?3:change=="malformed"?2:registrations.Count,change=="malformed");
        Assert.Equal(binding,result.JobBinding);
        Assert.Equal(count,result.RegisteredProducerCount);
        if(change is "pair" or "reverse") { Assert.Equal(first,result.First);Assert.Equal(second,result.Restart); }
        if(change=="duplicate-job") { Assert.Null(result.First);Assert.Null(result.Restart); }
        if(change=="empty") { Assert.Null(result.First);Assert.Null(result.Restart); }
    }

    [Theory]
    [InlineData("equal",true)]
    [InlineData("both-null",true)]
    [InlineData("phase",false)]
    [InlineData("repeat",false)]
    [InlineData("count",false)]
    [InlineData("first-identity",false)]
    [InlineData("second-identity",false)]
    [InlineData("first-job",false)]
    [InlineData("second-job",false)]
    [InlineData("first-predecessor",false)]
    [InlineData("second-predecessor",false)]
    [InlineData("prefix",false)]
    [InlineData("snapshot-identity",false)]
    [InlineData("source",false)]
    [InlineData("start",false)]
    [InlineData("inventory",false)]
    [InlineData("receipt",false)]
    [InlineData("object-order",false)]
    [InlineData("object-count",false)]
    [InlineData("object-path",false)]
    [InlineData("object-hash",false)]
    [InlineData("object-length",false)]
    [InlineData("one-null",false)]
    public void Given_IntakeCopies_When_Compared_Then_UsesOrderedStructuralEquality(string change,bool equal)
    {
        B10IntakeEvidence left=Intake(),right=Intake();
        B10ProducerEvidence[] entries=right.Producers.ToArray();
        int ordinal=change.StartsWith("first",StringComparison.Ordinal)?0:1;
        B10ProducerEvidence entry=entries[ordinal];
        PublicationAcceptanceSnapshot snapshot=entry.PublicationAcceptance!;
        ArtifactReference[] objects=snapshot.Objects.ToArray();
        snapshot=change switch
        {
            "prefix"=>snapshot with {ProducerPrefix="other"},
            "snapshot-identity"=>snapshot with {ProducerIdentity=snapshot.ProducerIdentity with {ClientId="other"}},
            "source"=>snapshot with {SourceCommit="other"},
            "start"=>snapshot with {StartLine=snapshot.StartLine with {Length=2}},
            "inventory"=>snapshot with {BrokerInventory=snapshot.BrokerInventory with {Length=2}},
            "receipt"=>snapshot with {ProducerReceipt=snapshot.ProducerReceipt with {Length=2}},
            "object-order"=>snapshot with {Objects=objects.Reverse().ToArray()},
            "object-count"=>snapshot with {Objects=objects.Take(1).ToArray()},
            "object-path"=>snapshot with {Objects=[objects[0] with {Name="other"},..objects.Skip(1)]},
            "object-hash"=>snapshot with {Objects=[objects[0] with {Sha256=new string('b',64)},..objects.Skip(1)]},
            "object-length"=>snapshot with {Objects=[objects[0] with {Length=2},..objects.Skip(1)]},
            _=>snapshot
        };
        entry=entry with {PublicationAcceptance=change is "one-null" or "both-null"?null:snapshot};
        if(change.EndsWith("-identity",StringComparison.Ordinal)&&change!="snapshot-identity")entry=entry with {Identity=entry.Identity with {ClientId="other"}};
        if(change.EndsWith("-job",StringComparison.Ordinal))entry=entry with {JobName="other"};
        if(change.EndsWith("-predecessor",StringComparison.Ordinal))entry=entry with {PredecessorInstanceId="other"};
        entries[ordinal]=entry;
        right=right with {Producers=entries};
        if(change=="phase")right=right with {Phase="C1"};
        if(change=="repeat")right=right with {RepeatNumber=2};
        if(change=="count")right=right with {Producers=entries.Take(1).ToArray()};
        if(change=="both-null")left=left with {Producers=[left.Producers[0],left.Producers[1] with {PublicationAcceptance=null}]};
        Assert.Equal(equal,B10JudgeChecks.SameIntake(left,right));
        Assert.Equal(equal,B10JudgeChecks.SameIntake(right,left));
    }

    [Theory]
    [InlineData("equal",true)]
    [InlineData("phase",false)]
    [InlineData("repeat",false)]
    [InlineData("empty",false)]
    [InlineData("one",false)]
    [InlineData("three",false)]
    [InlineData("reverse",false)]
    [InlineData("client",false)]
    [InlineData("instance",false)]
    [InlineData("job",false)]
    [InlineData("predecessor",false)]
    public void Given_Intake_When_BoundToRegistrations_Then_RequiresBothOrderedRoles(string change,bool binds)
    {
        B10IntakeEvidence intake=Intake();
        B10ProducerEvidence first=intake.Producers[0],second=intake.Producers[1];
        second=change switch
        {
            "client"=>second with {Identity=second.Identity with {ClientId="other"}},
            "instance"=>second with {Identity=second.Identity with {InstanceId="other"}},
            "job"=>second with {JobName="other"},
            "predecessor"=>second with {PredecessorInstanceId="other"},
            _=>second
        };
        intake=intake with {Producers=change switch {"empty"=>[],"one"=>[first],"three"=>[first,second,second],"reverse"=>[second,first],_=>[first,second]}};
        if(change=="phase")intake=intake with {Phase="C1"};
        if(change=="repeat")intake=intake with {RepeatNumber=2};
        Assert.Equal(binds,B10JudgeChecks.BindsIntake(intake,Registration(false),Registration(true)));
    }

    [Theory]
    [InlineData("C0",1)]
    [InlineData("C0",5)]
    [InlineData("C1",1)]
    [InlineData("C1",5)]
    public void Given_B10BoundaryCoordinates_When_RegistrationsAndIntakeAreBound_Then_AcceptsBothRoles(string phase,int repeat)
    {
        ProducerRegistration first=Registration(false,phase,repeat),second=Registration(true,phase,repeat);
        B10RegistrationJudgement result=B10JudgeChecks.EvaluateRegistrations(Options(phase),repeat,true,
            [("pod-second",second),("pod-first",first)],2,false);
        Assert.True(result.JobBinding);
        Assert.True(result.RegisteredProducerCount);
        Assert.Equal(first,result.First);
        Assert.Equal(second,result.Restart);
        Assert.Equal(first,PhaseWireCodec.ReadRegistration(PhaseWireCodec.WriteRegistration(first)));
        Assert.Equal(second,PhaseWireCodec.ReadRegistration(PhaseWireCodec.WriteRegistration(second)));
        B10IntakeEvidence intake=Intake(phase,repeat);
        PhaseWireCodec.ValidateB10Intake(intake,first.Identity,first.SourceCommit);
        Assert.True(B10JudgeChecks.BindsIntake(intake,first,second));
        Assert.True(B10JudgeChecks.SameIntake(intake,Intake(phase,repeat)));
    }

    private static readonly string[] PublicationNames=["index.json","ledger.jsonl","manifest.json","statistics.jsonl","request-lifecycle.jsonl","capabilities.json"];
    private static VerifierHostOptions Options(string phase="C0")=>new(){ScenarioId="B10",ConfigurationId=phase,RunId="synthetic-c5",Phase=phase,SourceCommit=new string('a',40)};
    private static ProducerRegistration Registration(bool restart,string phase="C0",int repeat=1)
    {
        string instance=restart?"pod-second":"pod-first";
        return new(1,new("B10",phase,"synthetic-c5",instance,instance,restart?"client-second":"client-first"),phase,repeat,
            restart?PhaseIdentity.RestartJobName("synthetic-c5",phase,repeat):PhaseIdentity.JobName("synthetic-c5",phase,repeat),instance,instance,new string('a',40),restart?"pod-first":null);
    }
    private static B10IntakeEvidence Intake(string phase="C0",int repeat=1)=>new(phase,repeat,
        new[]{Registration(false,phase,repeat),Registration(true,phase,repeat)}.Select(registration=>
        {
            string instance=registration.Identity.InstanceId;
            ArtifactReference Reference(string container,string name)=>new(container,name,1,new string('a',64));
            return new B10ProducerEvidence(registration.Identity,registration.JobName,registration.PredecessorInstanceId,
                new(PhasePaths.ProducerPrefix("synthetic-c5",phase,repeat,instance),registration.Identity,registration.SourceCommit,
                    Reference("verifier",PhasePaths.StartLine("synthetic-c5",phase,repeat)),
                    Reference("verifier",PhasePaths.BrokerInventory("synthetic-c5",phase,repeat)),
                    Reference("ledger",PhasePaths.ProducerCompletion("synthetic-c5",phase,repeat,instance)),
                    PublicationNames.Select(name=>Reference("ledger",PhasePaths.Publication("synthetic-c5",phase,repeat,instance,name))).ToArray()));
        }).ToArray());
}
#pragma warning restore CA1707
