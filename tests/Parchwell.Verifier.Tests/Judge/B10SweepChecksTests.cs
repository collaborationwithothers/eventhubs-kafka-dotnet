using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Judge;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

#pragma warning disable CA1707
public sealed class B10SweepChecksTests
{
    [Theory]
    [InlineData("valid",true)]
    [InlineData("partial",true)]
    [InlineData("schema",false)]
    [InlineData("scenario",false)]
    [InlineData("configuration",false)]
    [InlineData("run",false)]
    [InlineData("phase",false)]
    [InlineData("source",false)]
    [InlineData("risk",false)]
    [InlineData("failed-scan",false)]
    [InlineData("receipt-mode",false)]
    [InlineData("receipt-source",false)]
    [InlineData("receipt-job",false)]
    [InlineData("receipt-exit",false)]
    [InlineData("cleanup",false)]
    [InlineData("finalization-hash",false)]
    [InlineData("finalization-length",false)]
    [InlineData("finalization-path",false)]
    [InlineData("note-hash",false)]
    [InlineData("note-length",false)]
    [InlineData("note-path",false)]
    [InlineData("missing-note",false)]
    [InlineData("settle",false)]
    [InlineData("future-capture",false)]
    [InlineData("newer-verifier",false)]
    [InlineData("result",false)]
    [InlineData("verify-receipt",false)]
    [InlineData("eligibility",false)]
    [InlineData("missing-original",false)]
    [InlineData("empty-original",false)]
    [InlineData("extra-original",false)]
    [InlineData("empty-repeat",false)]
    [InlineData("extra-repeat",false)]
    [InlineData("missing-repeat",false)]
    [InlineData("swapped-repeat",false)]
    [InlineData("missing-counts",false)]
    [InlineData("false-finality",false)]
    public void Given_SweepEvidence_When_Accepted_Then_RequiresEveryBindingAndKeepsPartialScanDistinct(string change,bool expected)
    {
        VerifierHostOptions options=Options();
        DateTimeOffset at=DateTimeOffset.Parse("2026-11-05T00:00:02Z",System.Globalization.CultureInfo.InvariantCulture);
        B10OriginalEvidence[] originals=Enumerable.Range(1,5).Select(i=>new B10OriginalEvidence(i,
            Reference(PhasePaths.Result("run","C0",i)),Reference(PhasePaths.VerifyCompletion("run","C0",i)),true,at)).ToArray();
        B10RepeatFinalization[] repeats=originals.Select(x=>new B10RepeatFinalization(x.Repeat,x.Result,x.VerifyReceipt,true,
            VerifierResultStatus.CompleteValid,new(3,0,2,0,2,2),[])).ToArray();
        ArtifactReference result=Reference(PhasePaths.B10Finalization("run","C0"));
        ArtifactReference? noteReference=Reference(PhasePaths.SweepLine("run","C0"));
        B10SweepLine? note=new(1,"B10","C0","run","C0",new('a',40),"document-events",[new(0,10)],at,at,at.AddMilliseconds(-1000),1000);
        B10Finalization finalization=new(1,"B10","C0","run","C0",new('a',40),noteReference,true,B10SweepEvidence.ResidualRisk,repeats);
        WorkerReceipt receipt=new(1,"b10-sweep","B10","C0","run","C0",null,"run-c0-b10-sweep","pod",null,new('a',40),0,true,
            at.ToString("O"),at.AddSeconds(1).ToString("O"),[result],null,null);
        switch(change)
        {
            case "partial": repeats[1]=repeats[1] with {Status=VerifierResultStatus.Invalid,ClassificationFinal=false,Counts=null};receipt=receipt with{HandlerExitCode=1};break;
            case "schema":finalization=finalization with{SchemaVersion=2};break;
            case "scenario":options.ScenarioId="A01";break;
            case "configuration":finalization=finalization with{ConfigurationId="C1"};break;
            case "run":finalization=finalization with{RunId="other"};break;
            case "phase":finalization=finalization with{Phase="C1",ConfigurationId="C1"};break;
            case "source":finalization=finalization with{SourceCommit=new('b',40)};break;
            case "risk":finalization=finalization with{ResidualRisk="other"};break;
            case "failed-scan":finalization=finalization with{ScanComplete=false};break;
            case "receipt-mode":receipt=receipt with{Mode="judge"};break;
            case "receipt-source":receipt=receipt with{SourceCommit=new('b',40)};break;
            case "receipt-job":receipt=receipt with{JobName="other"};break;
            case "receipt-exit":receipt=receipt with{HandlerExitCode=1};break;
            case "cleanup":receipt=receipt with{CleanupCompleted=false};break;
            case "finalization-hash":result=result with{Sha256=new('c',64)};break;
            case "finalization-length":result=result with{Length=2};break;
            case "finalization-path":result=result with{Name="other"};break;
            case "note-hash":noteReference=noteReference with{Sha256=new('c',64)};break;
            case "note-length":noteReference=noteReference with{Length=2};break;
            case "note-path":noteReference=noteReference with{Name="other"};break;
            case "missing-note":note=null;break;
            case "settle":options.B10SettleIntervalMilliseconds=1001;break;
            case "future-capture":receipt=receipt with{StartedUtc=at.AddTicks(-1).ToString("O")};break;
            case "newer-verifier":originals[2]=originals[2] with{CompletedUtc=at.AddTicks(1)};break;
            case "result":originals[2]=originals[2] with{Result=originals[2].Result! with{Sha256=new('c',64)}};break;
            case "verify-receipt":originals[2]=originals[2] with{VerifyReceipt=originals[2].VerifyReceipt! with{Length=2}};break;
            case "eligibility":originals[2]=originals[2] with{Eligible=false};break;
            case "missing-original":originals=originals[..4];break;
            case "empty-original":originals=[];break;
            case "extra-original":originals=[.. originals,originals[0]];break;
            case "empty-repeat":finalization=finalization with{Repeats=[]};break;
            case "extra-repeat":finalization=finalization with{Repeats=[.. repeats,repeats[0]]};break;
            case "missing-repeat":finalization=finalization with{Repeats=repeats[..4]};break;
            case "swapped-repeat":(repeats[0],repeats[1])=(repeats[1],repeats[0]);break;
            case "missing-counts":repeats[2]=repeats[2] with{Counts=null};break;
            case "false-finality":repeats[2]=repeats[2] with{ClassificationFinal=false};break;
        }
        Assert.Equal(expected,B10SweepChecks.Accept(options,finalization,receipt,result,note,noteReference,originals));
    }

    private static ArtifactReference Reference(string name)=>new("verifier",name,1,new('b',64));
    private static VerifierHostOptions Options()=>new(){ScenarioId="B10",ConfigurationId="C0",RunId="run",Phase="C0",SourceCommit=new('a',40),B10SettleIntervalMilliseconds=1000,MaxPartitions=16};
}
