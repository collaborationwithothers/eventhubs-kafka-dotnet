using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.A01;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;

using Xunit;

namespace Parchwell.Verifier.Tests.A01;

#pragma warning disable CA1707 // Test names follow the repository convention.

// Every input here is synthetic, including all-pass assessments for overflow-only DTOs.
public sealed class A01EvidenceRecorderTests
{
    private static readonly string[] PrerequisiteStates = ["fail", "not_evaluated", "missing"];
    private static readonly string[] OverflowFields = ["brokers.broker-a.req_timeouts", "brokers.broker-a.disconnects", "brokers.broker-z.req_timeouts", "brokers.broker-z.disconnects"];

    public static TheoryData<int, string> Prerequisites => new(
        from check in Enumerable.Range(17, 10).Concat([3, 27])
        from state in PrerequisiteStates
        select (check, state));

    [Theory]
    [MemberData(nameof(Prerequisites))]
    public void Given_UnpassedPrerequisite_When_Recorded_Then_OnlyCompleteStatisticsAreRetained(int check, string state)
    {
        RunJudgementRequest request = Request([Report(90, Broker(maximum: 20_001))]);
        CheckAssessment[] assessments = Assessments().Where(a => a.CheckNumber != check).ToArray();
        if (state != "missing") assessments = [.. assessments, state == "fail" ? RecordedEvidence.WindowFail(check) :
            new(check, AssessmentEvaluation.NotEvaluated, null, null, null, null, null, null, null)];
        A01EvidenceRecording recording = A01EvidenceRecorder.Record(request, Judgement(assessments));
        AssertUnusable(recording.ProbeObservation!);
        Assert.Equal(check is 3 or 27, recording.RecordedEvidence!.Windows is not null);
        Assert.Empty(recording.RecordedEvidence.DeliveryErrorCodes!);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public void Given_IsolatedLateWriteFailure_When_Recorded_Then_InvalidRunRetainsUsableQualifyingEvidence(int check)
    {
        RunJudgementResult judgement = Judgement(Assessments().Select(a => a.CheckNumber == check ?
            RecordedEvidence.WindowFail(check, AssessmentClassification.Invalid) : a).ToArray()) with
        { Status = VerifierResultStatus.Invalid, FailedChecks = [check], Counts = null };
        A01EvidenceRecording recording = A01EvidenceRecorder.Record(Request([Report(90, Broker(maximum: 20_001))]), judgement);
        Assert.True(recording.ProbeObservation!.Usable);
        Assert.True(recording.ProbeObservation.Qualifying);
        Assert.Equal(VerifierResultStatus.Invalid, judgement.Status);
        Assert.Equal([check], judgement.FailedChecks);
        Assert.Null(judgement.Counts);
        Assert.Single(recording.RecordedEvidence!.Windows!);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Given_WindowCounts_When_Recorded_Then_SummariesPreservePresenceIndependently(long count, bool present)
    {
        foreach (long summary in new long[] { 0, 17, 99_327 })
        {
            A01WindowEvidence throttle = Record([Report(90, Broker(count: count, maximum: summary, rttCount: 1, p99: 17))]).RecordedEvidence!.Windows!.Single();
            Assert.Equal(present ? summary : (long?)null, throttle.ThrottleMaximumMilliseconds);
            Assert.Equal(17, throttle.RoundTripP99Microseconds);
            A01WindowEvidence rtt = Record([Report(90, Broker(count: 1, maximum: 17, rttCount: count, p99: summary))]).RecordedEvidence!.Windows!.Single();
            Assert.Equal(17, rtt.ThrottleMaximumMilliseconds);
            Assert.Equal(present ? summary : (long?)null, rtt.RoundTripP99Microseconds);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(19_999, false)]
    [InlineData(20_000, false)]
    [InlineData(20_001, true)]
    public void Given_NonemptyThrottle_When_Recorded_Then_QualificationUsesStrictThreshold(long maximum, bool qualifying)
    {
        ProbeRepeatObservation observation = Record([Report(90, Broker(maximum: maximum))]).ProbeObservation!;
        Assert.True(observation.Usable);
        Assert.Equal(qualifying, observation.Qualifying);
        Assert.Equal("broker-a", observation.Broker);
        Assert.Equal(0, observation.ReportIndex);
        Assert.Equal(1, observation.WindowCount);
        Assert.Equal(maximum, observation.WindowMaximum);
    }

    [Fact]
    public void Given_EmptyStreamOrWindows_When_Recorded_Then_ObservationIsUnusable()
    {
        AssertUnusable(Record([]).ProbeObservation!);
        AssertUnusable(Record([Report(90), Report(100, Broker(count: 0)), Report(110, Broker(count: -1))]).ProbeObservation!);
        Assert.Empty(Record([]).RecordedEvidence!.Windows!);
    }

    [Theory]
    [InlineData("B01", "C0")]
    [InlineData("A01", "C2")]
    [InlineData("A01", "C3")]
    public void Given_OtherSelection_When_Recorded_Then_BothProjectionsAreAbsent(string scenario, string configuration)
    {
        RunJudgementRequest request = Request([]);
        A01EvidenceRecording recording = A01EvidenceRecorder.Record(request with
        { Identity = request.Identity with { ScenarioId = scenario, ConfigurationId = configuration } }, Judgement());
        Assert.Null(recording.ProbeObservation);
        Assert.Null(recording.RecordedEvidence);
        Assert.Empty(recording.Diagnostics);
    }

    [Fact]
    public void Given_LedgerAvailability_When_Recorded_Then_CodesAreIndependentOfStatistics()
    {
        RunJudgementRequest request = Request([Report(90, Broker())]);
        foreach (int[] input in new[] { Array.Empty<int>(), new[] { 0, 0 }, new[] { 0, -184, 5, -184, 0 } })
        {
            A01EvidenceRecording recording = A01EvidenceRecorder.Record(request with
            { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success(input.Select(code => RecordedEvidence.LedgerRow() with { ErrorCode = code }).ToArray()) }, Judgement());
            Assert.Equal(input.Where(code => code != 0).Distinct().Order(), recording.RecordedEvidence!.DeliveryErrorCodes);
        }
        A01EvidenceRecording unavailable = A01EvidenceRecorder.Record(request with
        { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Failure(new StructuralProblem(3, "synthetic_unreadable", "ledger.jsonl", null, null, null)) }, Judgement());
        Assert.Null(unavailable.RecordedEvidence!.DeliveryErrorCodes);
        Assert.Single(unavailable.RecordedEvidence.Windows!);
        Assert.True(unavailable.ProbeObservation!.Usable);
        RunJudgementRequest incompleteRequest = request with
        { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([RecordedEvidence.LedgerRow() with { ErrorCode = -184 }]) };
        A01EvidenceRecording incomplete = A01EvidenceRecorder.Record(incompleteRequest,
            Judgement(Assessments().Where(a => a.CheckNumber != 17).ToArray()));
        Assert.Equal([-184], incomplete.RecordedEvidence!.DeliveryErrorCodes);
        Assert.Null(incomplete.RecordedEvidence.Windows);
    }

    [Fact]
    public void Given_BrokerKeysAndChangingLabels_When_Recorded_Then_CountersUseWholeCaptureOrderAndDictionaryKeys()
    {
        ReportAssociation first = Report(90, Broker("broker-z", 2, 5), Broker("broker-a", 7, 11));
        NativeBrokerReport renamed = Broker("broker-z", 5, 9) with
        { Name = ParsedField<string>.FromValue("renamed"), Source = ParsedField<string>.FromValue("internal"), State = ParsedField<string>.FromValue("DOWN") };
        ReportAssociation last = Report(110, renamed);
        last = last with { Envelope = last.Envelope with { SessionId = "other-session", StatisticsIdentity = last.Envelope.StatisticsIdentity with { Role = 7, ClientId = "other-envelope-client" } },
            Native = ParseResult<NativeClientReport>.Success(last.Native.Value! with
            { Name = ParsedField<string>.FromValue("other-name"), ClientId = ParsedField<string>.FromValue("other-client"),
                Type = ParsedField<string>.FromValue("consumer"), TimeSeconds = ParsedField<long>.FromValue(9) }) };
        IReadOnlyList<A01WindowEvidence> windows = Record([first, Report(100), last]).RecordedEvidence!.Windows!;
        Assert.Equal(["broker-a", "broker-z", "broker-z"], windows.Select(w => w.Broker));
        Assert.Equal([7L, 2L, 3L], windows.Select(w => w.RequestTimeoutsIncrement!.Value));
        Assert.Equal([11L, 5L, 4L], windows.Select(w => w.DisconnectsIncrement!.Value));
        Assert.Equal([0L, 0L, 2L], windows.Select(w => w.ReportIndex));
        Assert.Equal([90L, 90L, 110L], windows.Select(w => w.NativeTimestamp));
    }

    [Fact]
    public void Given_RegressionJudgement_When_Recorded_Then_AllWindowsAreOmitted()
    {
        RunJudgementRequest request = Request([Report(90, Broker(timeouts: 2)), Report(100, Broker(timeouts: 1))]);
        RunJudgementResult judgement = RunJudgement.Evaluate(request);
        Assert.Contains(23, judgement.FailedChecks);
        Assert.Null(A01EvidenceRecorder.Record(request, judgement).RecordedEvidence!.Windows);
    }

    [Fact]
    public void Given_FailedLedgerWithRetainedValue_When_Recorded_Then_DeliveryCodesRemainUnavailable()
    {
        RunJudgementRequest request = Request([]);
        LedgerRow row = RecordedEvidence.LedgerRow() with { ErrorCode = -184 };
        request = request with
        {
            Ledger = new ParseResult<IReadOnlyList<LedgerRow>>([row],
                [new StructuralProblem(3, "synthetic_unreadable", "ledger.jsonl", null, null, null)])
        };
        Assert.False(request.Ledger.IsSuccess);
        Assert.NotNull(request.Ledger.Value);
        A01EvidenceRecording recording = A01EvidenceRecorder.Record(request, Judgement());
        Assert.NotNull(recording.RecordedEvidence);
        Assert.Null(recording.RecordedEvidence.DeliveryErrorCodes);
        Assert.Empty(recording.RecordedEvidence.Windows!);
        AssertUnusable(recording.ProbeObservation!);
        Assert.Empty(recording.Diagnostics);
    }

    [Theory]
    [InlineData(long.MaxValue, null)]
    [InlineData(-1, long.MaxValue)]
    public void Given_SyntheticExtremeCounters_When_Recorded_Then_OverflowOmitsOnlyIncrementAndUpdatesPrevious(long current, long? expected)
    {
        A01EvidenceRecording recording = Record([Report(90, Broker(timeouts: long.MinValue, disconnects: 1)),
            Report(100, Broker(timeouts: current, disconnects: 3)), Report(110, Broker(timeouts: current, disconnects: 4))]);
        IReadOnlyList<A01WindowEvidence> windows = recording.RecordedEvidence!.Windows!;
        Assert.Equal(long.MinValue, windows[0].RequestTimeoutsIncrement);
        Assert.Equal(expected, windows[1].RequestTimeoutsIncrement);
        Assert.Equal(2, windows[1].DisconnectsIncrement);
        Assert.Equal(0, windows[2].RequestTimeoutsIncrement);
        Assert.Equal(1, windows[2].DisconnectsIncrement);
        Assert.Equal(expected is null ? 1 : 0, recording.Diagnostics.Count);
        if (expected is null) Assert.Equal(new ResultDiagnostic(29, "increment_overflow", "statistics.jsonl", 2, "brokers.broker-a.req_timeouts", null), recording.Diagnostics.Single());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Given_MultipleOverflowDiagnostics_When_Recorded_Then_RetentionUsesTraversalOrder(int limit)
    {
        RunJudgementRequest request = Request([Report(90, Broker("broker-z", long.MinValue, long.MinValue), Broker("broker-a", long.MinValue, long.MinValue)),
            Report(100, Broker("broker-z", long.MaxValue, long.MaxValue), Broker("broker-a", long.MaxValue, long.MaxValue))]);
        request = request with { Limits = request.Limits with { MaxDiagnosticsPerCheck = limit } };
        A01EvidenceRecording recording = A01EvidenceRecorder.Record(request, Judgement());
        Assert.Equal(OverflowFields.Take(limit), recording.Diagnostics.Select(d => d.Field));
        Assert.All(recording.Diagnostics, d => { Assert.Equal(29, d.Check); Assert.Equal("increment_overflow", d.Code); Assert.Equal("statistics.jsonl", d.Artifact); Assert.Equal(2, d.Row); Assert.Null(d.Snippet); });
        Assert.Equal(4, recording.RecordedEvidence!.Windows!.Count);
    }

    [Fact]
    public void Given_MaximumTies_When_Recorded_Then_SelectionUsesMaximumThenReportIndexThenOrdinalKey()
    {
        ProbeRepeatObservation higher = Record([Report(90, Broker("broker-z", maximum: 20_001)), Report(100, Broker("broker-a", maximum: 20_002))]).ProbeObservation!;
        Assert.Equal("broker-a", higher.Broker); Assert.Equal(1, higher.ReportIndex);
        ProbeRepeatObservation first = Record([Report(90, Broker("broker-z", maximum: 20_002)), Report(100, Broker("broker-a", maximum: 20_002))]).ProbeObservation!;
        Assert.Equal("broker-z", first.Broker); Assert.Equal(0, first.ReportIndex);
        ProbeRepeatObservation ordinal = Record([Report(90, Broker("broker-z", maximum: 17), Broker("broker-a", maximum: 17))]).ProbeObservation!;
        Assert.Equal("broker-a", ordinal.Broker); Assert.Equal(0, ordinal.ReportIndex); Assert.False(ordinal.Qualifying);
        Assert.Equal([90L, 100L, 110L], Record([Report(90, Broker()), Report(100, Broker()), Report(110, Broker())]).RecordedEvidence!.Windows!.Select(w => w.NativeTimestamp));
        Assert.Equal([0L, 1L, 2L], Record([Report(90, Broker()), Report(100, Broker()), Report(110, Broker())]).RecordedEvidence!.Windows!.Select(w => w.ReportIndex));
    }

    private static void AssertUnusable(ProbeRepeatObservation observation)
    {
        Assert.False(observation.Usable); Assert.False(observation.Qualifying);
        Assert.Null(observation.Broker); Assert.Null(observation.ReportIndex);
        Assert.Null(observation.WindowCount); Assert.Null(observation.WindowMaximum);
    }

    private static CheckAssessment[] Assessments() => Enumerable.Range(1, 27).Select(RecordedEvidence.WindowPass).ToArray();
    private static RunJudgementResult Judgement(IReadOnlyList<CheckAssessment>? assessments = null) =>
        new(VerifierResultStatus.CompleteValid, [], assessments ?? Assessments(), new(1, 0, 0, 0, 0, 0), null, new(RecordedEvidence.WindowPass(27), []), []);
    private static A01EvidenceRecording Record(IReadOnlyList<ReportAssociation> reports) => A01EvidenceRecorder.Record(Request(reports), Judgement());
    internal static RunJudgementRequest Request(IReadOnlyList<ReportAssociation> reports) => RecordedEvidence.SyntheticRequest() with
    { Reports = reports, Statistics = ParseResult<IReadOnlyList<StatisticsEnvelope>>.Success(reports.Select(r => r.Envelope).ToArray()) };
    internal static NativeBrokerReport Broker(string key = "broker-a", long timeouts = 0, long disconnects = 0, long count = 1, long maximum = 0, long rttCount = 1, long p99 = 0) =>
        RecordedEvidence.Broker(key) with { RequestTimeouts = ParsedField<long>.FromValue(timeouts), Disconnects = ParsedField<long>.FromValue(disconnects),
            Throttle = ParsedField<NativeWindow>.FromValue(new(ParsedField<long>.FromValue(count), ParsedField<long>.FromValue(maximum), ParsedField<long>.Missing())),
            RoundTripTime = ParsedField<NativeWindow>.FromValue(new(ParsedField<long>.FromValue(rttCount), ParsedField<long>.Missing(), ParsedField<long>.FromValue(p99))) };
    internal static ReportAssociation Report(long timestamp, params NativeBrokerReport[] brokers) => RecordedEvidence.Report(timestamp, timestamp - 50, brokers);
}
