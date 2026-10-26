using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.B04;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.B04;

#pragma warning disable CA1707
public sealed class B04EvidenceRecorderTests
{
    [Fact]
    public void Given_NonB04Request_When_Recording_Then_ReturnsNoEvidence()
    {
        B04EvidenceRecording result = B04EvidenceRecorder.Record(RecordedEvidence.SyntheticRequest());
        Assert.Null(result.Evidence); Assert.Empty(result.Diagnostics); Assert.Null(result.WorkloadObservation);
    }

    [Fact]
    public void Given_B04LedgerAndLifecycle_When_Recording_Then_RecordsSortedDistinctEvidence()
    {
        RunJudgementRequest request = B04Request() with
        {
            Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([Row(0), Row(7), Row(7), Row(-185)]),
            Lifecycle = ParseResult<IReadOnlyList<LifecycleEvent>>.Success([Fatal(7,"z"), Fatal(7,"a"), Fatal(7,"a"), Fatal(1,"ignored",false)])
        };
        B04RecordedEvidence evidence = B04EvidenceRecorder.Record(request).Evidence!;
        Assert.Equal(new[]{-185,7}, evidence.DeliveryErrorCodes);
        Assert.Equal([new B04FatalError(7,"a"),new B04FatalError(7,"z")], evidence.FatalErrors);
    }

    [Fact]
    public void Given_NoFatalErrors_When_Recording_Then_RecordsPresentEmptyArray()
    {
        B04EvidenceRecording result = B04EvidenceRecorder.Record(B04Request());
        Assert.Empty(result.Evidence!.FatalErrors!);
    }

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Given_FatalArrayBoundary_When_Recording_Then_RespectsLimit(int count, bool retained)
    {
        NativeError[] errors = Enumerable.Range(0,count).Select(i=>Fatal(i,"n"+i)).ToArray();
        var request = B04Request() with
        {
            Lifecycle=ParseResult<IReadOnlyList<LifecycleEvent>>.Success(errors),
            Ledger=ParseResult<IReadOnlyList<LedgerRow>>.Success([Row(3)])
        };
        IReadOnlyList<B04FatalError>? actual=B04EvidenceRecorder.Record(request).Evidence!.FatalErrors;
        if(retained)Assert.Equal(count,actual!.Count);else Assert.Null(actual);
        Assert.Equal(new[]{3},B04EvidenceRecorder.Record(request).Evidence!.DeliveryErrorCodes);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(255, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Given_FatalNameBoundary_When_Recording_Then_RespectsLimit(int length, bool retained)
    {
        var request=B04Request() with
        {
            Lifecycle=ParseResult<IReadOnlyList<LifecycleEvent>>.Success([Fatal(1,new string('x',length))]),
            Ledger=ParseResult<IReadOnlyList<LedgerRow>>.Success([Row(9)])
        };
        IReadOnlyList<B04FatalError>? actual=B04EvidenceRecorder.Record(request).Evidence!.FatalErrors;
        if(retained)Assert.Single(actual!);else Assert.Null(actual);
        Assert.Equal(new[]{9},B04EvidenceRecorder.Record(request).Evidence!.DeliveryErrorCodes);
    }

    [Fact]
    public void Given_UnreadableInputs_When_Recording_Then_LeavesArraysUnavailable()
    {
        var request=B04Request() with
        {
            Ledger=ParseResult<IReadOnlyList<LedgerRow>>.Failure(new(1,"bad","ledger.jsonl",null,null,null)),
            Lifecycle=ParseResult<IReadOnlyList<LifecycleEvent>>.Failure(new(1,"bad","request-lifecycle.jsonl",null,null,null))
        };
        B04RecordedEvidence evidence=B04EvidenceRecorder.Record(request).Evidence!;
        Assert.Null(evidence.DeliveryErrorCodes);Assert.Null(evidence.FatalErrors);
    }

    [Fact]
    public void Given_FailedParseResultsWithRetainedValues_When_Recording_Then_DoesNotUseThoseValuesAndKeepsIndependentEvidence()
    {
        StructuralProblem problem=new(1,"bad","captured.json",null,null,null);
        RunJudgementRequest ledgerFailure=B04Request() with
        {
            Ledger=new ParseResult<IReadOnlyList<LedgerRow>>([Row(8)],[problem],ParseFailureKind.Structural),
            Lifecycle=ParseResult<IReadOnlyList<LifecycleEvent>>.Success([Fatal(2,"fatal")])
        };
        B04RecordedEvidence first=B04EvidenceRecorder.Record(ledgerFailure).Evidence!;
        Assert.Null(first.DeliveryErrorCodes);
        Assert.Equal(new[]{new B04FatalError(2,"fatal")},first.FatalErrors);

        RunJudgementRequest lifecycleFailure=B04Request() with
        {
            Ledger=ParseResult<IReadOnlyList<LedgerRow>>.Success([Row(8)]),
            Lifecycle=new ParseResult<IReadOnlyList<LifecycleEvent>>([Fatal(2,"fatal")],[problem],ParseFailureKind.Structural)
        };
        B04RecordedEvidence second=B04EvidenceRecorder.Record(lifecycleFailure).Evidence!;
        Assert.Equal(new[]{8},second.DeliveryErrorCodes);
        Assert.Null(second.FatalErrors);
    }

    [Theory]
    [InlineData(255,true)]
    [InlineData(256,true)]
    [InlineData(257,false)]
    public void Given_LedgerCodeArrayBoundary_When_Recording_Then_RespectsIndependentLimit(int count,bool retained)
    {
        var rows=Enumerable.Range(1,count).Select(Row).ToArray();
        var request=B04Request() with
        {
            Ledger=ParseResult<IReadOnlyList<LedgerRow>>.Success(rows),
            Lifecycle=ParseResult<IReadOnlyList<LifecycleEvent>>.Success([Fatal(5,"fatal")])
        };
        B04RecordedEvidence evidence=B04EvidenceRecorder.Record(request).Evidence!;
        if(retained)Assert.Equal(count,evidence.DeliveryErrorCodes!.Count);else Assert.Null(evidence.DeliveryErrorCodes);
        Assert.Equal(new[]{new B04FatalError(5,"fatal")},evidence.FatalErrors);
    }

    private static RunJudgementRequest B04Request()
    {
        RunJudgementRequest source=RecordedEvidence.SyntheticRequest();
        return source with { Identity=source.Identity with { ScenarioId="B04" } };
    }

    private static LedgerRow Row(int code) => RecordedEvidence.LedgerRow() with { ErrorCode=code };

    private static NativeError Fatal(int code,string name,bool fatal=true) => new(1,
        new RunIdentity("B04","C1","run-1","instance-1","instance-1","producer-1"),"session",code,"received",code,name,
        ParsedField<string>.Null(),true,fatal,"native_callback");
}
#pragma warning restore CA1707
