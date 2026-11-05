using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707

public sealed class B10LateWriteChecksTests
{
    private static readonly RunIdentity First=new("B10","C0","synthetic-run","first","first","first");
    private static readonly RunIdentity Second=First with {InstanceId="second",ExpectedInstanceId="second",ClientId="second"};

    [Theory]
    [InlineData(false,3,0,0,2,0,0)]
    [InlineData(true,3,0,2,0,2,2)]
    public void Given_LiteralTwoProducerFixture_When_Classified_Then_OneUnionDeterminesAllSixCounts(
        bool includeLate,long ok,long loss,long phantom,long failure,long duplicate,long reorder)
    {
        LedgerRow[][] ledgers=[[Row(First,"a",1),Row(First,"b",2,false)],
            [Row(Second,"a",2),Row(Second,"c",3),Row(Second,"d",4,false)]];
        ConsumedRecord[] original=[Record(First,"a",1,0),Record(Second,"a",2,1),Record(Second,"c",3,2)];
        ConsumedRecord[] late=includeLate?[Record(First,"b",2,3),Record(Second,"d",4,4),Record(Second,"a",2,5)]:[];
        B10LateWriteJudgement result=Classify(ledgers,original,late);
        Assert.True(result.Valid);Assert.Empty(result.Reasons);
        Assert.Equal(new OutcomeCounts(ok,loss,phantom,failure,duplicate,reorder),result.Counts);
    }

    [Theory]
    [InlineData(true,false,0,1,0,0)] [InlineData(true,true,1,0,0,0)]
    [InlineData(false,false,0,0,0,1)] [InlineData(false,true,0,0,1,0)]
    public void Given_LateOwnRecord_When_Classified_Then_LossAndReportedFailureAreReclassified(
        bool delivered,bool observed,long ok,long loss,long phantom,long failure)
    {
        B10LateWriteJudgement result=Classify([[Row(First,"a",1,delivered)],[]],[],observed?[Record(First,"a",1,2)]:[]);
        Assert.True(result.Valid);Assert.Equal(new OutcomeCounts(ok,loss,phantom,failure,0,0),result.Counts);
    }

    [Theory]
    [InlineData("own-first",true)] [InlineData("own-second",true)]
    [InlineData("earlier",true)] [InlineData("later",false)] [InlineData("other-phase",false)]
    [InlineData("unknown",false)] [InlineData("missing",false)] [InlineData("duplicate-header",false)]
    public void Given_OriginalRecordOwnership_When_Classified_Then_OnlyOwnOrStrictlyEarlierInstancesArePermitted(string kind,bool valid)
    {
        ConsumedRecord record=kind switch
        {
            "own-first"=>Record(First,"a",1,0),"own-second"=>Record(Second,"a",2,0),
            "earlier"=>Record(First,"unused",1,0) with {InstanceId="earlier"},
            "missing"=>Record(First,"a",1,0) with {InstanceId=null},
            "duplicate-header"=>Record(First,"a",1,0) with {InstanceId=null,Problems=[Problem("instance-id")]},
            _=>Record(First,"a",1,0) with {InstanceId=kind}
        };
        B10LateWriteJudgement result=B10LateWriteChecks.Classify([First,Second],[[Row(First,"a",1)],[Row(Second,"a",2)]],
            [record],[],new HashSet<string>(StringComparer.Ordinal){"earlier"});
        Assert.Equal(valid,result.Valid);Assert.Equal(valid,result.Counts is not null);
        if(kind=="earlier")Assert.Equal(new OutcomeCounts(0,2,0,0,0,0),result.Counts);
    }

    [Theory]
    [InlineData("unknown")] [InlineData("missing")] [InlineData("duplicate-header")]
    public void Given_UnownedTail_When_Classified_Then_ItCannotBecomeOwnEvidence(string kind)
    {
        ConsumedRecord record=Record(First,"a",1,2) with {InstanceId=kind=="unknown"?"unknown":null,
            Problems=kind=="duplicate-header"?[Problem("instance-id")]:[]};
        B10LateWriteJudgement result=Classify([[Row(First,"a",1)],[]],[],[record]);
        Assert.True(result.Valid);Assert.Equal(new OutcomeCounts(0,1,0,0,0,0),result.Counts);
    }

    [Theory]
    [InlineData("missing-join")] [InlineData("key")] [InlineData("sequence")]
    [InlineData("missing-key")] [InlineData("missing-attempt")] [InlineData("missing-sequence")]
    [InlineData("problems")] [InlineData("duplicate-coordinate")]
    public void Given_OwnRecordReconciliationFailure_When_Classified_Then_CountsAreAbsent(string failure)
    {
        ConsumedRecord record=Record(First,"a",1,0);
        record=failure switch
        {
            "missing-join"=>record with {AttemptId="unknown"},"key"=>record with {Key="wrong"},
            "sequence"=>record with {Sequence=2},"missing-key"=>record with {Key=null},
            "missing-attempt"=>record with {AttemptId=null},"missing-sequence"=>record with {Sequence=null},
            "problems"=>record with {Problems=[Problem("attempt-id")]},_=>record
        };
        B10LateWriteJudgement result=Classify([[Row(First,"a",1)],[]],[record],failure=="duplicate-coordinate"?[record]:[]);
        Assert.False(result.Valid);Assert.Null(result.Counts);Assert.Equal(["b10_record_reconciliation_invalid"],result.Reasons);
    }

    [Theory]
    [InlineData("row-identity")] [InlineData("attempt-instance")] [InlineData("duplicate-composite")]
    public void Given_InvalidOwnLedger_When_Classified_Then_CountsAreAbsent(string failure)
    {
        LedgerRow row=Row(First,"a",1);
        row=failure switch {"row-identity"=>row with {Identity=Second},
            "attempt-instance"=>row with {Attempt=row.Attempt with {InstanceId="second"}},_=>row};
        B10LateWriteJudgement result=Classify([failure=="duplicate-composite"?[row,row]:[row],[]],[],[]);
        Assert.False(result.Valid);Assert.Null(result.Counts);Assert.Equal(["b10_ledger_identity_invalid"],result.Reasons);
    }

    [Fact]
    public void Given_DistinctOffsetsAndSameBareAttemptAcrossInstances_When_Classified_Then_CopiesRemainDuplicateEvidence()
    {
        B10LateWriteJudgement result=Classify([[Row(First,"same",1)],[Row(Second,"same",1)]],
            [Record(First,"same",1,0)],[Record(Second,"same",1,1),Record(First,"same",1,2)]);
        Assert.True(result.Valid);Assert.Equal(new OutcomeCounts(2,0,0,0,2,0),result.Counts);
    }

    [Theory]
    [InlineData("no-producers")] [InlineData("one-producer")] [InlineData("duplicate-producer")]
    [InlineData("no-ledgers")] [InlineData("own-earlier")]
    public void Given_AmbiguousOwnershipShape_When_Classified_Then_ItIsRejected(string failure)
    {
        RunIdentity[] producers=failure switch {"no-producers"=>[],"one-producer"=>[First],"duplicate-producer"=>[First,First],_=>[First,Second]};
        B10LateWriteJudgement result=B10LateWriteChecks.Classify(producers,failure=="no-ledgers"?[]:[[],[]],[],[],
            new HashSet<string>(StringComparer.Ordinal){failure=="own-earlier"?First.InstanceId:"earlier"});
        Assert.False(result.Valid);Assert.Null(result.Counts);
    }

    [Fact]
    public void Given_EmptyLedgersAndRecords_When_Classified_Then_EmptyUnionHasZeroCounts()
    {
        Assert.Equal(new OutcomeCounts(0,0,0,0,0,0),Classify([[],[]],[],[]).Counts);
    }

    [Theory]
    [InlineData("start",-1,false)] [InlineData("start",0,true)] [InlineData("start",1,true)]
    [InlineData("end",-1,false)] [InlineData("end",0,false)] [InlineData("end",1,true)] [InlineData("end",2,true)]
    [InlineData("sweep",-1,false)] [InlineData("sweep",1,false)] [InlineData("sweep",2,true)] [InlineData("sweep",3,true)]
    [InlineData("current",2,false)] [InlineData("current",3,true)] [InlineData("current",4,true)]
    [InlineData("partition",-1,false)] [InlineData("partition",0,true)] [InlineData("partition",1,true)]
    [InlineData("limit",0,false)] [InlineData("limit",1,true)] [InlineData("limit",2,true)]
    public void Given_OffsetAndPartitionBoundaries_When_WindowsBuilt_Then_BoundsAreMonotoneAndNonnegative(string field,int value,bool valid)
    {
        int partition=field=="partition"?value:0;
        long start=field=="start"?value:1,end=field=="end"?value:2,sweep=field=="sweep"?value:3,current=field=="current"?value:4;
        if(field=="end"){sweep=3;current=4;}
        if(field=="sweep"){end=2;current=4;}
        if(field=="current"){sweep=3;}
        void Build()=>B10LateWriteChecks.BuildWindows([new(partition,start)],[new(partition,end)],[new(partition,start)],
            [new(partition,sweep)],new Dictionary<int,long>{{partition,current}},field=="limit"?value:2);
        if(valid)Build();else Assert.Throws<InvalidOperationException>(Build);
    }

    [Theory]
    [InlineData("start-null")] [InlineData("end-null")] [InlineData("start-empty")] [InlineData("end-empty")]
    [InlineData("accepted-empty")] [InlineData("sweep-empty")] [InlineData("start-duplicate")] [InlineData("end-duplicate")]
    [InlineData("accepted-duplicate")] [InlineData("sweep-duplicate")] [InlineData("accepted-mismatch")]
    [InlineData("accepted-missing")] [InlineData("end-missing")] [InlineData("sweep-missing")] [InlineData("current-missing")]
    public void Given_InvalidVectorShape_When_WindowsBuilt_Then_WholeScanCannotComplete(string failure)
    {
        WatermarkOffset[]? start=failure=="start-null"?null:failure=="start-empty"?[]:[new(0,1)];
        WatermarkOffset[]? end=failure=="end-null"?null:failure=="end-empty"?[]:[new(0,2)];
        PartitionHigh[] accepted=failure=="accepted-empty"?[]:[new(0,1)];
        PartitionHigh[] sweep=failure=="sweep-empty"?[]:[new(0,3)];
        Dictionary<int,long> current=new(){{0,3}};
        if(failure=="start-duplicate")start=[new(0,1),new(0,1)];
        if(failure=="end-duplicate")end=[new(0,2),new(0,2)];
        if(failure=="accepted-duplicate")accepted=[new(0,1),new(0,1)];
        if(failure=="sweep-duplicate")sweep=[new(0,3),new(0,3)];
        if(failure=="accepted-mismatch")accepted=[new(0,2)];
        if(failure=="accepted-missing")accepted=[new(1,1)];
        if(failure=="end-missing")end=[new(1,2)];
        if(failure=="sweep-missing")sweep=[new(1,3)];
        if(failure=="current-missing")current=new(){{1,3}};
        Assert.Throws<InvalidOperationException>(()=>B10LateWriteChecks.BuildWindows(start,end,accepted,sweep,current,2));
    }

    [Fact]
    public void Given_PartitionsAddedAtEndAndSweep_When_WindowsBuilt_Then_NewPartitionsBeginAtZeroAndCurrentExtrasStayExcluded()
    {
        B10SweepWindows windows=B10LateWriteChecks.BuildWindows([new(0,1)],[new(0,2),new(1,3)],[new(0,1)],
            [new(0,4),new(1,5),new(2,6)],new Dictionary<int,long>{{0,4},{1,5},{2,6},{3,7}},3);
        Assert.Equal([new(0,1,2),new(1,0,3)],windows.Original.Partitions);
        Assert.Equal([new(0,2,4),new(1,3,5),new(2,0,6)],windows.Late.Partitions);
        Assert.True(windows.Original.CanAssign);Assert.True(windows.Late.CanAssign);
    }

    [Theory]
    [InlineData("end",1,false)] [InlineData("end",2,true)] [InlineData("end",3,true)]
    [InlineData("sweep",1,false)] [InlineData("sweep",2,true)] [InlineData("sweep",3,true)]
    public void Given_NewPartitionAroundCapacity_When_WindowsBuilt_Then_EveryVectorObservesPartitionLimit(string addedAt,int limit,bool valid)
    {
        WatermarkOffset[] ends=addedAt=="end"?[new(0,2),new(1,2)]:[new(0,2)];
        void Build()=>B10LateWriteChecks.BuildWindows([new(0,1)],ends,[new(0,1)],
            [new(0,3),new(1,3)],new Dictionary<int,long>{{0,3},{1,3}},limit);
        if(valid)Build();else Assert.Throws<InvalidOperationException>(Build);
    }

    [Theory]
    [InlineData(0,false,false)] [InlineData(1,true,false)] [InlineData(2,true,false)]
    [InlineData(3,true,false)] [InlineData(4,false,true)] [InlineData(5,false,true)]
    [InlineData(6,false,true)] [InlineData(7,false,false)] [InlineData(8,false,false)]
    public void Given_OffsetsAroundFixedLines_When_Selected_Then_WindowsAreDisjointAndHalfOpen(long offset,bool original,bool late)
    {
        B10SweepWindows windows=B10LateWriteChecks.BuildWindows([new(0,1)],[new(0,4)],[new(0,1)],
            [new(0,7)],new Dictionary<int,long>{{0,8}},1);
        Assert.Equal(original,windows.Original.Partitions.Any(p=>offset>=p.Start&&offset<p.End));
        Assert.Equal(late,windows.Late.Partitions.Any(p=>offset>=p.Start&&offset<p.End));
    }

    private static B10LateWriteJudgement Classify(IReadOnlyList<IReadOnlyList<LedgerRow>> ledger,
        IReadOnlyList<ConsumedRecord> original,IReadOnlyList<ConsumedRecord> late)=>
        B10LateWriteChecks.Classify([First,Second],ledger,original,late,new HashSet<string>(StringComparer.Ordinal));
    private static LedgerRow Row(RunIdentity identity,string attempt,long sequence,bool delivered=true)=>
        new(1,identity,new(identity.InstanceId,attempt,sequence,"synthetic-tenant","synthetic-document","k",sequence,"synthetic",0,0),
            "callback",delivered,delivered?0:-192,ParsedField<string>.Null(),ParsedField<int>.FromValue(delivered?2:1),
            "native_callback",1,ParsedField<double>.FromValue(1),ParsedField<double>.Missing());
    private static ConsumedRecord Record(RunIdentity identity,string attempt,long sequence,long offset)=>
        new("document-events",0,offset,"k",identity.InstanceId,attempt,sequence,[]);
    private static StructuralProblem Problem(string field)=>new(6,"synthetic_invalid","consumed",null,field,null);
}
