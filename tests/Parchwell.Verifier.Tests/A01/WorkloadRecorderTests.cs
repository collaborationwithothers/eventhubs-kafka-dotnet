using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.A01;
using Parchwell.Verifier.VerifyRun.Input;
using Xunit;

namespace Parchwell.Verifier.Tests.A01;

#pragma warning disable CA1707
public sealed class WorkloadRecorderTests
{
    [Fact]
    public void Given_AttemptsAtStageBoundary_When_Recorded_Then_UsesPlannedHalfOpenIntervals()
    {
        var request=RecordedEvidence.SyntheticRequest();
        AttemptEntry first=RecordedEvidence.Attempt() with{PlannedMicroseconds=59_999_999};
        AttemptEntry second=RecordedEvidence.Attempt("a2") with{PlannedMicroseconds=60_000_000};
        request=request with
        {
            Manifest=ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with{Inventory=[first,second],PlannedAttemptCount=2,AttemptCount=2}),
            Ledger=ParseResult<IReadOnlyList<LedgerRow>>.Success([RecordedEvidence.LedgerRow(first,true),RecordedEvidence.LedgerRow(second,false)])
        };
        WorkloadRecording result=WorkloadRecorder.Record(request.Manifest,request.Ledger);
        Assert.True(result.Observation!.Synthetic);
        Assert.Equal(2,result.Observation.PlannedAttempts); Assert.Equal(2,result.Observation.InvokedAttempts);
        Assert.Equal(2,result.Observation.TerminalObservations); Assert.True(result.Observation.FixedProfileComplete);
        Assert.Equal(1,result.Observation.Stages![0].InvokedAttempts);
        Assert.Equal(1,result.Observation.Stages[1].InvokedAttempts);
        Assert.Equal(1,result.Observation.Stages[1].FailedObservations);
    }

    [Fact]
    public void Given_MissingLedger_When_Recorded_Then_OmitsTerminalTotalsInsteadOfZero()
    {
        var request=RecordedEvidence.SyntheticRequest();
        var missing=ParseResult<IReadOnlyList<LedgerRow>>.Failure(new StructuralProblem(3,"missing_input","ledger.jsonl",null,null,null));
        WorkloadRecording result=WorkloadRecorder.Record(request.Manifest,missing);
        Assert.Null(result.Observation!.TerminalObservations);
        Assert.Null(result.Observation.Stages![0].DeliveredObservations);
        Assert.Null(result.Observation.Stages[0].FailedObservations);
    }

    [Fact]
    public void Given_ManifestInventoryAndLedgerWithDifferentCounts_When_Recorded_Then_PreservesEachMeasuredTotal()
    {
        var request=RecordedEvidence.SyntheticRequest();
        AttemptEntry first=RecordedEvidence.Attempt("a1");
        AttemptEntry second=RecordedEvidence.Attempt("a2");
        AttemptEntry third=RecordedEvidence.Attempt("a3");
        request=request with
        {
            Manifest=ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with
                {Inventory=[first,second,third],PlannedAttemptCount=3,AttemptCount=3}),
            Ledger=ParseResult<IReadOnlyList<LedgerRow>>.Success([RecordedEvidence.LedgerRow(first,true),RecordedEvidence.LedgerRow(second,false)])
        };
        var result=WorkloadRecorder.Record(request.Manifest,request.Ledger).Observation!;
        Assert.Equal(3,result.PlannedAttempts);
        Assert.Equal(3,result.InvokedAttempts);
        Assert.Equal(2,result.TerminalObservations);
    }

    [Fact]
    public void GivenIncompleteManifest_When_Recorded_Then_RetainsCountsAndMarksFixedProfileIncomplete()
    {
        var request=RecordedEvidence.SyntheticRequest();
        AttemptEntry attempt=RecordedEvidence.Attempt();
        request=request with
        {
            Manifest=ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with
                {Inventory=[attempt],PlannedAttemptCount=2,AttemptCount=1,WorkerStatus="incomplete",BookkeepingComplete=false}),
            Ledger=ParseResult<IReadOnlyList<LedgerRow>>.Success([RecordedEvidence.LedgerRow(attempt,true)])
        };
        var result=WorkloadRecorder.Record(request.Manifest,request.Ledger).Observation!;
        Assert.Equal(2,result.PlannedAttempts);
        Assert.Equal(1,result.InvokedAttempts);
        Assert.False(result.FixedProfileComplete);
    }
}
#pragma warning restore CA1707
