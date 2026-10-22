using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.A01;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.A01;

#pragma warning disable CA1707 // Repository test naming convention.

/// <summary>Checks the explicit diagnostic-only C3 projection with synthetic inputs.</summary>
public sealed class A01C3EvidenceRecorderTests
{
    private static readonly string[] States = ["fail", "not_evaluated", "missing"];

    public static TheoryData<int, string> Prerequisites => new(
        from check in Enumerable.Range(17, 10)
        from state in States
        select (check, state));

    [Theory]
    [MemberData(nameof(Prerequisites))]
    public void Given_UnpassedStatisticsPrerequisite_When_C3Recorded_Then_OmitsWindowsAndProbe(int check, string state)
    {
        CheckAssessment[] assessments = Assessments().Where(a => a.CheckNumber != check).ToArray();
        if (state != "missing") assessments = [.. assessments, state == "fail" ? RecordedEvidence.WindowFail(check) :
            new(check, AssessmentEvaluation.NotEvaluated, null, null, null, null, null, null, null)];
        A01EvidenceRecording recording = A01EvidenceRecorder.RecordC3(Request(), Judgement(assessments));
        Assert.Null(recording.ProbeObservation);
        Assert.Null(recording.RecordedEvidence!.Windows);
        Assert.Empty(recording.RecordedEvidence.DeliveryErrorCodes!);
    }

    [Fact]
    public void Given_LedgerCodesOrMissingLedger_When_C3Recorded_Then_CodesRequireSuccessfulParse()
    {
        RunJudgementRequest request = Request() with
        {
            Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success(new[] { 0, -184, 5, -184 }
                .Select(code => RecordedEvidence.LedgerRow() with { ErrorCode = code }).ToArray())
        };
        A01EvidenceRecording recorded = A01EvidenceRecorder.RecordC3(request, Judgement());
        Assert.Equal([-184, 5], recorded.RecordedEvidence!.DeliveryErrorCodes);
        Assert.Null(recorded.ProbeObservation);
        foreach (bool retained in new[] { false, true })
        {
            request = request with { Ledger = new(retained ? request.Ledger.Value : null,
                [new StructuralProblem(3, "synthetic_missing", "ledger.jsonl", null, null, null)]) };
            Assert.Null(A01EvidenceRecorder.RecordC3(request, Judgement()).RecordedEvidence!.DeliveryErrorCodes);
        }
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Given_WindowCount_When_C3Recorded_Then_PreservesSummariesWithoutProbe(long count, bool present)
    {
        RunJudgementRequest request = Request(A01EvidenceRecorderTests.Report(90,
            A01EvidenceRecorderTests.Broker(count: count, maximum: 20_001, rttCount: count, p99: 37)));
        RunJudgementResult judgement = Judgement();
        A01EvidenceRecording recording = A01EvidenceRecorder.RecordC3(request, judgement);
        var window = Assert.Single(recording.RecordedEvidence!.Windows!);
        Assert.Equal(present ? 20_001 : (long?)null, window.ThrottleMaximumMilliseconds);
        Assert.Equal(present ? 37 : (long?)null, window.RoundTripP99Microseconds);
        Assert.Null(recording.ProbeObservation);
        Assert.Equal(VerifierResultStatus.CompleteValid, judgement.Status);
        Assert.Equal(new OutcomeCounts(1, 0, 0, 0, 0, 0), judgement.Counts);
        Assert.Empty(judgement.FailedChecks);
    }

    [Fact]
    public void Given_EmptyCapture_When_C3Recorded_Then_RetainsEmptyWindowsAndNeverProbe()
    {
        A01EvidenceRecording recording = A01EvidenceRecorder.RecordC3(Request(), Judgement());
        Assert.Empty(recording.RecordedEvidence!.Windows!);
        Assert.Null(recording.ProbeObservation);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Given_OverflowAndInvalidResult_When_C3Recorded_Then_RetainsBoundedDiagnosticsWithoutAuthorizingCounts(int limit)
    {
        RunJudgementRequest request = Request(
            A01EvidenceRecorderTests.Report(90, A01EvidenceRecorderTests.Broker("broker-z", long.MinValue, long.MinValue),
                A01EvidenceRecorderTests.Broker("broker-a", long.MinValue, long.MinValue)),
            A01EvidenceRecorderTests.Report(100, A01EvidenceRecorderTests.Broker("broker-z", long.MaxValue, long.MaxValue),
                A01EvidenceRecorderTests.Broker("broker-a", long.MaxValue, long.MaxValue)));
        request = request with { Limits = request.Limits with { MaxDiagnosticsPerCheck = limit } };
        RunJudgementResult judgement = Judgement() with { Status = VerifierResultStatus.Invalid, Counts = null, FailedChecks = [12] };
        A01EvidenceRecording recording = A01EvidenceRecorder.RecordC3(request, judgement);
        string[] fields = ["brokers.broker-a.req_timeouts", "brokers.broker-a.disconnects", "brokers.broker-z.req_timeouts", "brokers.broker-z.disconnects"];
        Assert.Equal(fields.Take(limit), recording.Diagnostics.Select(d => d.Field));
        Assert.All(recording.Diagnostics, diagnostic => Assert.Equal("increment_overflow", diagnostic.Code));
        Assert.Equal(4, recording.RecordedEvidence!.Windows!.Count);
        Assert.All(recording.RecordedEvidence.Windows.Skip(2), window =>
        {
            Assert.Null(window.RequestTimeoutsIncrement);
            Assert.Null(window.DisconnectsIncrement);
        });
        Assert.Null(recording.ProbeObservation);
        Assert.Equal(VerifierResultStatus.Invalid, judgement.Status);
        Assert.Null(judgement.Counts);
        Assert.Equal([12], judgement.FailedChecks);
    }

    [Theory]
    [InlineData("A01", "C0")]
    [InlineData("A01", "C2")]
    [InlineData("B01", "C3")]
    public void Given_OtherSelection_When_C3Recorded_Then_BothProjectionsAreAbsent(string scenario, string configuration)
    {
        RunJudgementRequest request = Request();
        A01EvidenceRecording recording = A01EvidenceRecorder.RecordC3(request with
            { Identity = request.Identity with { ScenarioId = scenario, ConfigurationId = configuration } }, Judgement());
        Assert.Null(recording.ProbeObservation);
        Assert.Null(recording.RecordedEvidence);
        Assert.Empty(recording.Diagnostics);
    }

    private static RunJudgementRequest Request(params ReportAssociation[] reports)
    {
        RunJudgementRequest request = A01EvidenceRecorderTests.Request(reports);
        return request with { Identity = request.Identity with { ConfigurationId = "C3" } };
    }
    private static CheckAssessment[] Assessments() => Enumerable.Range(1, 27).Select(RecordedEvidence.WindowPass).ToArray();
    private static RunJudgementResult Judgement(IReadOnlyList<CheckAssessment>? assessments = null) =>
        new(VerifierResultStatus.CompleteValid, [], assessments ?? Assessments(), new(1, 0, 0, 0, 0, 0), null,
            new(RecordedEvidence.WindowPass(27), []), []);
}
#pragma warning restore CA1707
