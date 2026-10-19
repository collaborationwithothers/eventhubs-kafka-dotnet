using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707 // Repository Given_When_Then naming convention.
public sealed class BrokerCountTests
{
    [Fact]
    public void Given_StableKeysWithChangingStates_When_Recorded_Then_MaximumUsesAllReportsAndPostUsesFirstBoundaryReport()
    {
        NativeBrokerReport a = RecordedEvidence.Broker("A");
        NativeBrokerReport b = RecordedEvidence.Broker("B");
        NativeBrokerReport c = RecordedEvidence.Broker("C");
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [
            RecordedEvidence.Report(90, 40, [a, b, Down(c)]),
            RecordedEvidence.Report(100, 50, [a, Down(b), Down(c)]),
            RecordedEvidence.Report(110, 60, [a, b, c])]) with { ExpectedBrokerNames = ["A", "B", "C"] };
        AssertCounts(request, 3, 1);
    }

    [Theory]
    [InlineData("learned", "UP", 1)]
    [InlineData("configured", "UP", 1)]
    [InlineData("logical", "UP", 1)]
    [InlineData("internal", "UP", 0)]
    [InlineData("unknown", "UP", 0)]
    [InlineData("learned", "DOWN", 0)]
    [InlineData("configured", "DOWN", 0)]
    [InlineData("logical", "DOWN", 0)]
    [InlineData("Learned", "UP", 0)]
    [InlineData("learned", "up", 0)]
    public void Given_SourceAndState_When_Recorded_Then_OnlyKnownSourcesInUppercaseUpCount(string source, string state, long expected)
    {
        NativeBrokerReport broker = RecordedEvidence.Broker("broker-a", source, state);
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [RecordedEvidence.Report(100, 50, [broker])]);
        AssertCounts(request, expected, expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_TwoDictionaryKeysWithSameNodeId_When_Recorded_Then_EachKeyCounts(bool sameName)
    {
        NativeBrokerReport a = RecordedEvidence.Broker("A");
        NativeBrokerReport b = RecordedEvidence.Broker("B") with { NodeId = a.NodeId };
        if (sameName) b = b with { Name = a.Name };
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [RecordedEvidence.Report(100, 50, [a, b])])
            with { ExpectedBrokerNames = ["A", "B"] };
        AssertCounts(request, 2, 2);
    }

    [Fact]
    public void Given_KeyAndNativeNameDiffer_When_Recorded_Then_KeyStillCounts()
    {
        NativeBrokerReport broker = RecordedEvidence.Broker() with { Name = ParsedField<string>.FromValue("broker-z") };
        AssertCounts(WithReports(RecordedEvidence.SyntheticRequest(), [RecordedEvidence.Report(100, 50, [broker])]), 1, 1);
    }

    [Fact]
    public void Given_EmptyPresentBrokerDictionary_When_Recorded_Then_ZeroIsMeasured()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), [RecordedEvidence.Report(100, 50, [])])
            with { ExpectedBrokerNames = [] };
        AssertCounts(request, 0, 0);
    }

    [Fact]
    public void Given_NoReports_When_Recorded_Then_CountsAreAbsent()
    {
        RunJudgementRequest request = WithReports(RecordedEvidence.SyntheticRequest(), []);
        Assert.Null(BrokerCountRecorder.Record(request, Assessments(request)));
    }

    [Theory]
    [InlineData(18, false)] [InlineData(18, true)]
    [InlineData(19, false)] [InlineData(19, true)]
    [InlineData(20, false)] [InlineData(20, true)]
    [InlineData(21, false)] [InlineData(21, true)]
    [InlineData(22, false)] [InlineData(22, true)]
    [InlineData(23, false)] [InlineData(23, true)]
    [InlineData(24, false)] [InlineData(24, true)]
    [InlineData(25, false)] [InlineData(25, true)]
    [InlineData(26, false)] [InlineData(26, true)]
    public void Given_EachStatisticsPrerequisiteFailsOrIsUnevaluated_When_Recorded_Then_NoMeasurementIsSubstituted(int check, bool unevaluated)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        CheckAssessment[] assessments = Assessments(request);
        assessments = assessments.Select(assessment => assessment.CheckNumber != check ? assessment : assessment with
        {
            Evaluation = unevaluated ? AssessmentEvaluation.NotEvaluated : AssessmentEvaluation.Evaluated,
            Disposition = unevaluated ? null : AssessmentDisposition.Fail,
            Classification = unevaluated ? null : AssessmentClassification.Invalid
        }).ToArray();
        Assert.Null(BrokerCountRecorder.Record(request, assessments));
        Assert.Null(BrokerCountRecorder.Record(request, assessments.Where(assessment => assessment.CheckNumber != check).ToArray()));
    }

    [Fact]
    public void Given_Check17FailureAlone_When_Recorded_Then_ItDoesNotAddACountPrerequisite()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with { Lifecycle = ParseResult<IReadOnlyList<LifecycleEvent>>.Failure(
            new StructuralProblem(17, "missing_input", "request-lifecycle.jsonl", null, null, null)) };
        Assert.Equal(AssessmentDisposition.Fail, Assessments(request).Single(assessment => assessment.CheckNumber == 17).Disposition);
        AssertCounts(request, 1, 1);
    }

    [Fact]
    public void Given_ForeignLabels_When_Recorded_Then_RunnerClientAndProducerRoleOwnMeasurement()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = WithReports(request, request.Reports.Select(report => report with { Envelope = report.Envelope with
        {
            StatisticsIdentity = report.Envelope.StatisticsIdentity with { Role = 999, ClientId = "foreign" }
        } }).ToArray());
        BrokerConnectionCount? measurement = BrokerCountRecorder.Record(request, Assessments(request));
        Assert.Equal(new BrokerConnectionCount("LoadGenProducer", "producer-1", 1, 1, true), measurement);
    }

    private static NativeBrokerReport Down(NativeBrokerReport broker) => broker with { State = ParsedField<string>.FromValue("DOWN") };

    private static RunJudgementRequest WithReports(RunJudgementRequest request, IReadOnlyList<ReportAssociation> reports) =>
        request with { Reports = reports, Statistics = ParseResult<IReadOnlyList<StatisticsEnvelope>>.Success(reports.Select(report => report.Envelope).ToArray()) };

    private static CheckAssessment[] Assessments(RunJudgementRequest request) =>
        StatisticsChecks.Evaluate(request).Select(result => result.Assessment).ToArray();

    private static void AssertCounts(RunJudgementRequest request, long maximum, long postDrain)
    {
        foreach (CheckAssessment assessment in Assessments(request).Where(assessment => assessment.CheckNumber is >= 18 and <= 26))
        {
            Assert.Equal(AssessmentEvaluation.Evaluated, assessment.Evaluation);
            Assert.Equal(AssessmentDisposition.Pass, assessment.Disposition);
            Assert.Null(assessment.Classification);
        }
        BrokerConnectionCount? measurement = BrokerCountRecorder.Record(request, Assessments(request));
        Assert.NotNull(measurement);
        Assert.Equal(maximum, measurement.Maximum);
        Assert.Equal(postDrain, measurement.FirstPostDrain);
        Assert.Equal("LoadGenProducer", measurement.Role);
        Assert.Equal("producer-1", measurement.ClientId);
        Assert.True(measurement.LowerBound);
    }
}
