using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.LedgerBatch;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.LedgerBatch;

#pragma warning disable CA1707
// Recorded measures preserve missing evidence independently of run validity and outcome counts.
public sealed class BatchingEvidenceRecorderTests
{
    private static readonly BatchingRecordedEvidence NoLatency = new(null, null, null, null, null, 7, 5, 4);

    [Theory]
    [InlineData("A01")]
    [InlineData("B04")]
    public void Given_OtherScenario_When_Recording_Then_ReturnsNoEvidence(string scenario)
    {
        BatchingEvidenceRecording result = BatchingEvidenceRecorder.Record(Request(scenario, [Row(9.5)], Raw(Stats(7))), Judgement());
        Assert.Null(result.Evidence); Assert.Empty(result.Diagnostics); Assert.Null(result.WorkloadObservation);
    }

    [Theory]
    [InlineData("B06")]
    [InlineData("B07")]
    public void Given_DeliveredRowsAndPassingStatistics_When_Recording_Then_RecordsEveryMeasure(string scenario)
    {
        BatchingEvidenceRecording result = BatchingEvidenceRecorder.Record(Request(scenario,
            [Row(15.584), Row(2372.592), Row(4), Row(9.5), RecordedEvidence.LedgerRow(delivered: false)],
            Raw("{\"brokers\":{\"b0\":{\"req\":{\"Produce\":7}},\"b1\":{\"req\":{\"Produce\":4,\"Metadata\":1}}},\"topics\":{\"t\":{\"batchcnt\":{\"cnt\":9,\"max\":6}},\"u\":{\"batchcnt\":{\"cnt\":2,\"max\":8}}}}")), Judgement());
        Assert.Equal(new BatchingRecordedEvidence(4, 4000, 9500, 2372592, 2372592, 11, 11, 8), result.Evidence);
        Assert.True(result.WorkloadObservation!.Synthetic);
    }

    [Fact]
    public void Given_SeveralReports_When_Recording_Then_UsesFirstPostDrainReportAndSumsWindows()
    {
        Assert.Equal(new BatchingRecordedEvidence(1, 4000, 4000, 4000, 4000, 7, 15, 4), Record(Raw(Stats(3), 90), Raw(Stats(7), 100), Raw(Stats(9), 110)));
        Assert.Equal(new BatchingRecordedEvidence(1, 4000, 4000, 4000, 4000, 9, 10, 4), Record(Raw(Stats(3), 99), Raw(Stats(9), 101)));
        Assert.Equal(new BatchingRecordedEvidence(1, 4000, 4000, 4000, 4000, null, 10, 4), Record(Raw(Stats(3), 90), Raw(Stats(9), 99)));
    }

    [Theory]
    [InlineData(0.0005, 1)]
    [InlineData(0.0004, 0)]
    [InlineData(0d, 0)]
    [InlineData(15.584, 15584)]
    public void Given_LatencyRounding_When_Recording_Then_RoundsHalfAwayFromZeroToMicroseconds(double milliseconds, long microseconds)
    {
        Assert.Equal(new BatchingRecordedEvidence(1, microseconds, microseconds, microseconds, microseconds, 7, 5, 4), RecordRows(Row(milliseconds)));
    }

    [Theory]
    [InlineData(20, 10000, 19000)]
    [InlineData(21, 11000, 20000)]
    public void Given_NearestRankBoundary_When_Recording_Then_SelectsCeilingRank(int count, long p50, long p95)
    {
        Assert.Equal(new BatchingRecordedEvidence(count, 1000, p50, p95, count * 1000L, 7, 5, 4),
            RecordRows(Enumerable.Range(1, count).Reverse().Select(i => Row(i)).ToArray()));
    }

    [Fact]
    public void Given_LatencyLimits_When_Recording_Then_RecordsOnlyBoundedValues()
    {
        Assert.Equal(new BatchingRecordedEvidence(1, 9000000000000000, 9000000000000000, 9000000000000000, 9000000000000000, 7, 5, 4), RecordRows(Row(9_000_000_000_000d)));
        foreach (double value in new[] { 9_000_000_000_001d, -0.001, double.NaN, double.PositiveInfinity })
            Assert.Equal(NoLatency, RecordRows(Row(value)));
    }

    [Fact]
    public void Given_UnusableLatencyInput_When_Recording_Then_LeavesLatencyAbsent()
    {
        Assert.Equal(NoLatency, RecordRows(Row(4), RecordedEvidence.LedgerRow()));
        Assert.Equal(NoLatency, RecordRows(Row(4), RecordedEvidence.LedgerRow() with { DeliveryLatencyMs = ParsedField<double>.Missing() }));
        Assert.Equal(NoLatency, RecordRows());
        Assert.Equal(NoLatency, RecordRows(RecordedEvidence.LedgerRow(delivered: false)));
        RunJudgementRequest request = Request("B06", [Row(4)], Raw(Stats(7))) with
        {
            Ledger = new ParseResult<IReadOnlyList<LedgerRow>>([Row(4)], [new StructuralProblem(1, "bad", "ledger.jsonl", null, null, null)], ParseFailureKind.Structural)
        };
        Assert.Equal(NoLatency, BatchingEvidenceRecorder.Record(request, Judgement()).Evidence);
    }

    [Theory]
    [InlineData(18)] [InlineData(19)] [InlineData(20)] [InlineData(21)] [InlineData(22)]
    [InlineData(23)] [InlineData(24)] [InlineData(25)] [InlineData(26)]
    public void Given_StatisticsCheckNotPassed_When_Recording_Then_LeavesStatisticsAbsent(int check)
    {
        BatchingRecordedEvidence expected = new(1, 4000, 4000, 4000, 4000, null, null, null);
        Assert.Equal(expected, RecordWith(Assessments().Select(a => a.CheckNumber == check ? a with { Disposition = AssessmentDisposition.Fail } : a).ToArray()));
        Assert.Equal(expected, RecordWith(Assessments().Where(a => a.CheckNumber != check).ToArray()));
        Assert.Equal(expected, RecordWith(Assessments().Select(a => a.CheckNumber == check ? a with { Evaluation = AssessmentEvaluation.NotEvaluated, Disposition = null } : a).ToArray()));
    }

    [Theory]
    [InlineData(17)] [InlineData(27)]
    public void Given_AdjacentCheckNotPassed_When_Recording_Then_KeepsStatistics(int check)
    {
        Assert.Equal(new BatchingRecordedEvidence(1, 4000, 4000, 4000, 4000, 7, 5, 4),
            RecordWith(Assessments().Select(a => a.CheckNumber == check ? a with { Disposition = AssessmentDisposition.Fail } : a).ToArray()));
    }

    [Fact]
    public void Given_NoReports_When_Recording_Then_LeavesStatisticsAbsent()
    {
        Assert.Equal(new BatchingRecordedEvidence(1, 4000, 4000, 4000, 4000, null, null, null),
            BatchingEvidenceRecorder.Record(Request("B06", [Row(4)]), Judgement()).Evidence);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"b0\":1}")]
    [InlineData("{\"b0\":{}}")]
    [InlineData("{\"b0\":{\"req\":[]}}")]
    [InlineData("{\"b0\":{\"req\":{}}}")]
    [InlineData("{\"b0\":{\"req\":{\"Produce\":7}},\"b1\":{\"req\":{\"Metadata\":1}}}")]
    [InlineData("{\"b0\":{\"req\":{\"Produce\":-1}}}")]
    [InlineData("{\"b0\":{\"req\":{\"Produce\":1.5}}}")]
    [InlineData("{\"b0\":{\"req\":{\"Produce\":\"1\"}}}")]
    [InlineData("{\"b0\":{\"req\":{\"Produce\":9223372036854775807}},\"b1\":{\"req\":{\"Produce\":1}}}")]
    public void Given_MalformedPostDrainBrokers_When_Recording_Then_LeavesOnlyProduceAbsent(string brokers)
    {
        Assert.Equal(new BatchingRecordedEvidence(1, 4000, 4000, 4000, 4000, null, 5, 4),
            Record(Raw("{\"brokers\":" + brokers + ",\"topics\":{\"t\":{\"batchcnt\":{\"cnt\":5,\"max\":4}}}}")));
    }

    [Theory]
    [InlineData("{")] [InlineData("[]")] [InlineData("{}")]
    public void Given_UnreadableReport_When_Recording_Then_LeavesStatisticsAbsent(string json)
    {
        Assert.Equal(new BatchingRecordedEvidence(1, 4000, 4000, 4000, 4000, null, null, null), Record(Raw(json)));
    }

    [Fact]
    public void Given_ZeroOrLargestProduceTotal_When_Recording_Then_RecordsIt()
    {
        Assert.Equal(0, Record(Raw(Stats(0))).ProduceRequests);
        Assert.Equal(long.MaxValue, Record(Raw("{\"brokers\":{\"b0\":{\"req\":{\"Produce\":9223372036854775806}},\"b1\":{\"req\":{\"Produce\":1}}},\"topics\":{}}")).ProduceRequests);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"topics\":[]")]
    [InlineData(",\"topics\":{\"t\":1}")]
    [InlineData(",\"topics\":{\"t\":{}}")]
    [InlineData(",\"topics\":{\"t\":{\"batchcnt\":[]}}")]
    [InlineData(",\"topics\":{\"t\":{\"batchcnt\":{\"max\":4}}}")]
    [InlineData(",\"topics\":{\"t\":{\"batchcnt\":{\"cnt\":5}}}")]
    [InlineData(",\"topics\":{\"t\":{\"batchcnt\":{\"cnt\":-1,\"max\":4}}}")]
    [InlineData(",\"topics\":{\"t\":{\"batchcnt\":{\"cnt\":5,\"max\":1.5}}}")]
    [InlineData(",\"topics\":{\"t\":{\"batchcnt\":{\"cnt\":\"5\",\"max\":4}}}")]
    [InlineData(",\"topics\":{\"t\":{\"batchcnt\":{\"cnt\":5,\"max\":0}}}")]
    [InlineData(",\"topics\":{\"t\":{\"batchcnt\":{\"cnt\":9223372036854775807,\"max\":4}},\"u\":{\"batchcnt\":{\"cnt\":1,\"max\":4}}}")]
    public void Given_MalformedBatchWindow_When_Recording_Then_LeavesOnlyBatchesAbsent(string suffix)
    {
        string json = "{\"brokers\":{\"b0\":{\"req\":{\"Produce\":7}}}" + suffix + "}";
        BatchingRecordedEvidence expected = new(1, 4000, 4000, 4000, 4000, 7, null, null);
        Assert.Equal(expected, Record(Raw(json)));
        Assert.Equal(expected, Record(Raw(json, 90), Raw(Stats(7), 110)));
    }

    [Fact]
    public void Given_IdleWindows_When_Recording_Then_CountsOnlySampledBatches()
    {
        Assert.Equal(new BatchingRecordedEvidence(1, 4000, 4000, 4000, 4000, 0, 0, 0), Record(Raw("{\"brokers\":{\"b0\":{\"req\":{\"Produce\":0}}},\"topics\":{}}")));
        Assert.Equal(new BatchingRecordedEvidence(1, 4000, 4000, 4000, 4000, 7, 7, 9), Record(Raw(Stats(3, 0, 3), 90), Raw(Stats(7, 5, 4), 100), Raw(Stats(7, 2, 9), 110)));
    }

    private static RunJudgementRequest Request(string scenario, IReadOnlyList<LedgerRow> rows, params ReportAssociation[] reports)
    {
        RunJudgementRequest source = RecordedEvidence.SyntheticRequest();
        return source with { Identity = source.Identity with { ScenarioId = scenario }, Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success(rows), Reports = reports };
    }
    private static LedgerRow Row(double milliseconds) => RecordedEvidence.LedgerRow() with { DeliveryLatencyMs = ParsedField<double>.FromValue(milliseconds) };
    private static ReportAssociation Raw(string json, long timestamp = 110)
    {
        ReportAssociation source = RecordedEvidence.Report(timestamp, 60);
        return source with { Envelope = source.Envelope with { RawJson = json } };
    }
    private static string Stats(long produce, long batches = 5, long largest = 4) =>
        FormattableString.Invariant($"{{\"brokers\":{{\"b0\":{{\"req\":{{\"Produce\":{produce}}}}}}},\"topics\":{{\"t\":{{\"batchcnt\":{{\"cnt\":{batches},\"max\":{largest}}}}}}}}}");
    private static CheckAssessment[] Assessments() => Enumerable.Range(1, 27).Select(RecordedEvidence.WindowPass).ToArray();
    private static RunJudgementResult Judgement(IReadOnlyList<CheckAssessment>? assessments = null) =>
        new(VerifierResultStatus.CompleteValid, [], assessments ?? Assessments(), new(1, 0, 0, 0, 0, 0), null, new(RecordedEvidence.WindowPass(27), []), []);
    private static BatchingRecordedEvidence Record(params ReportAssociation[] reports) =>
        BatchingEvidenceRecorder.Record(Request("B06", [Row(4)], reports), Judgement()).Evidence!;
    private static BatchingRecordedEvidence RecordWith(IReadOnlyList<CheckAssessment> assessments) =>
        BatchingEvidenceRecorder.Record(Request("B06", [Row(4)], Raw(Stats(7))), Judgement(assessments)).Evidence!;
    private static BatchingRecordedEvidence RecordRows(params LedgerRow[] rows) =>
        BatchingEvidenceRecorder.Record(Request("B06", rows, Raw(Stats(7))), Judgement()).Evidence!;
}
#pragma warning restore CA1707
