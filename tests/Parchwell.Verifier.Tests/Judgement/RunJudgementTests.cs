using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Parchwell.Verifier.VerifyRun.Window;

using Xunit;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707 // Test names follow the repository Given_When_Then convention.

public sealed class RunJudgementTests
{
    [Fact]
    public async Task Given_CommittedProducerBytesAndSyntheticWindow_When_Judging_Then_GoldenCountsAndBrokerMeasurementAgree()
    {
        // Consumed records and window passes here are synthetic; captured bytes prove format compatibility only.
        InputLimits limits = RecordedEvidence.SmallLimits with
        { MaxDepth = 32, MaxArtifactBytes = 262_144, MaxTotalInputBytes = 1_048_576,
          MaxRowBytes = 131_072, MaxEntries = 50_000 };
        var budget = new InputBudget(limits);
        byte[] Read(string name) => File.ReadAllBytes(RecordedEvidence.FixturePath("captured-run/" + name));
        var ledger = LedgerReader.ReadRows(Read("ledger.jsonl"), limits, budget);
        var manifest = ManifestReader.Read(Read("manifest.json"), limits, budget, 16);
        var index = ArtifactIndexReader.Read(Read("index.json"), limits, budget);
        var statistics = CapturedEvidenceReader.ReadStatistics(Read("statistics.jsonl"), "statistics.jsonl", limits, budget);
        var lifecycle = CapturedEvidenceReader.ReadLifecycle(Read("request-lifecycle.jsonl"), "request-lifecycle.jsonl", limits, budget);
        var capabilities = CapturedEvidenceReader.ReadCapabilities(Read("capabilities.json"), "capabilities.json", limits, budget);
        Assert.True(ledger.IsSuccess);
        Assert.True(manifest.IsSuccess);
        Assert.True(index.IsSuccess);
        Assert.True(statistics.IsSuccess);
        Assert.True(lifecycle.IsSuccess);
        Assert.True(capabilities.IsSuccess);
        ReportAssociation[] reports = statistics.Value!.Select(e => new ReportAssociation(e,
            NativeStatisticsReader.Read(System.Text.Encoding.UTF8.GetBytes(e.RawJson), "statistics.jsonl", limits, budget, 131_072, 16))).ToArray();
        Assert.All(reports, r => Assert.True(r.Native.IsSuccess));
        AttemptEntry seen = ledger.Value![0].Attempt;
        var request = new RunJudgementRequest(manifest.Value!.Identity, index, ledger, manifest, statistics, lifecycle,
            capabilities, reports, ["broker-a/1"], await ConfigurationFidelityTests.Resolve("C0"),
            [new("synthetic-topic", 0, 0, seen.Key, seen.InstanceId, seen.AttemptId, seen.Sequence, [])],
            Enumerable.Range(7, 5).Select(RecordedEvidence.WindowPass).ToArray(), new(262_144, 16, 256));
        RunJudgementResult result = RunJudgement.Evaluate(request);
        Assert.Empty(result.FailedChecks);
        Assert.Equal(VerifierResultStatus.CompleteValid, result.Status);
        Assert.Equal(new OutcomeCounts(1, 0, 0, 1, 0, 0), result.Counts);
        Assert.Equal(new BrokerConnectionCount("LoadGenProducer", "golden-loadgen-client", 1, 1, true), result.BrokerCount);
    }

    [Fact]
    public void Given_CompleteValidSyntheticEvidence_When_Judging_Then_ExactCountsAndMeasurementsAvailable()
    {
        RunJudgementResult result = RunJudgement.Evaluate(RecordedEvidence.SyntheticRequest());
        Assert.Equal(VerifierResultStatus.CompleteValid, result.Status);
        Assert.Equal(new OutcomeCounts(1, 0, 0, 0, 0, 0), result.Counts);
        Assert.Empty(result.FailedChecks);
        Assert.Equal(new BrokerConnectionCount("LoadGenProducer", "producer-1", 1, 1, true), result.BrokerCount);
        Assert.Equal(27, result.Assessments.Count);
        Assert.Equal(Enumerable.Range(1, 27), result.Assessments.Select(a => a.CheckNumber));
    }

    [Theory]
    [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    public void Given_MissingWindowAssessment_When_Judging_Then_StatusAndCountsCannotBePublished(int check)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        RunJudgementResult result = RunJudgement.Evaluate(request with
        { WindowAssessments = request.WindowAssessments.Where(a => a.CheckNumber != check).ToArray() });
        Assert.Null(result.Status);
        Assert.Null(result.Counts);
        Assert.Equal(AssessmentEvaluation.NotEvaluated, result.Assessments.Single(a => a.CheckNumber == check).Evaluation);
    }

    [Fact]
    public void Given_InvalidAndIncompleteFailures_When_Judging_Then_InvalidWinsAndBothNumbersRemain()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        RunJudgementResult result = RunJudgement.Evaluate(request with
        {
            Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([RecordedEvidence.LedgerRow(delivered: false)]),
            WindowAssessments = request.WindowAssessments.Select(a => a.CheckNumber == 10 ? RecordedEvidence.WindowFail(10) : a).ToArray()
        });
        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        Assert.Equal([10, 12], result.FailedChecks);
        Assert.Null(result.Counts);
    }

    [Fact]
    public void Given_IncompleteWindow_When_Judging_Then_AllSixCountsAbsent()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        RunJudgementResult result = RunJudgement.Evaluate(request with { Records = [],
            WindowAssessments = request.WindowAssessments.Select(a => a.CheckNumber == 10 ? RecordedEvidence.WindowFail(10) : a).ToArray() });
        Assert.Equal(VerifierResultStatus.Incomplete, result.Status);
        Assert.Null(result.Counts);
    }

    [Fact]
    public void Given_BadLedgerAndIndependentNativeError_When_Judging_Then_BothFailuresRetained()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        RunJudgementResult result = RunJudgement.Evaluate(request with
        {
            Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Failure(new StructuralProblem(1, "malformed_json", "ledger.jsonl", 1, null, null)),
            Lifecycle = ParseResult<IReadOnlyList<LifecycleEvent>>.Success([new NativeError(1, request.Identity,
                "synthetic", 1, "synthetic", 0, "synthetic", ParsedField<string>.Null(), true, false, "synthetic")])
        });
        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        Assert.Equal([1, 16], result.FailedChecks);
        Assert.Null(result.Counts);
        foreach (int check in new[] { 4, 5, 6, 12, 13 })
            Assert.Equal(AssessmentEvaluation.NotEvaluated, result.Assessments.Single(a => a.CheckNumber == check).Evaluation);
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(2, 2)] [InlineData(3, 2)]
    public void Given_DiagnosticsAroundCap_When_Judging_Then_FirstRecordsRetainedWithoutLosingFailureNumbers(int count, int retained)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        StructuralProblem[] problems = Enumerable.Range(0, count).Select(n => new StructuralProblem(1,
            $"problem_{n}", "ledger.jsonl", n + 1, "field", "abcde")).ToArray();
        RunJudgementResult result = RunJudgement.Evaluate(request with
        {
            Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Failure(problems),
            Limits = request.Limits with { MaxDiagnosticsPerCheck = 2, MaxDiagnosticBytes = 4 }
        });
        Assert.Equal([1], result.FailedChecks);
        Assert.Equal(retained, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, d => Assert.Equal("abcd", d.Snippet));
        Assert.Equal(Enumerable.Range(0, retained).Select(n => $"problem_{n}"), result.Diagnostics.Select(d => d.Code));
    }

    [Theory]
    [InlineData(7, 1)] [InlineData(7, 2)] [InlineData(7, 3)]
    [InlineData(8, 1)] [InlineData(8, 2)] [InlineData(8, 3)]
    [InlineData(9, 1)] [InlineData(9, 2)] [InlineData(9, 3)]
    public void Given_MultipleObservedWindowFailures_When_Judging_Then_RetainsDiagnosticsUpToPerCheckCap(int check, int cap)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        var note = new StartLineNote(1, request.Identity, RunWindow.Topic,
            check == 7 ? [new(0, 1), new(0, -1)] : [new(0, 2), new(1, 2)],
            "synthetic", "synthetic", "synthetic", "synthetic");
        RunWindow window = RunWindow.Create(ParseResult<StartLineNote>.Success(note), request.Identity,
            check == 8 ? new Dictionary<int, long> { [2] = 2 } : new Dictionary<int, long> { [0] = 1, [1] = 1 });
        string[] expectedCodes = check switch
        {
            7 => ["check_7_failed", "check_7_failed"],
            8 => ["check_8_failed", "check_8_failed"],
            _ => ["check_9_failed", "check_9_failed"]
        };
        RunJudgementResult result = RunJudgement.Evaluate(request with
        {
            WindowAssessments = window.Assessments,
            Limits = request.Limits with { MaxDiagnosticsPerCheck = cap }
        });

        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        Assert.Equal([check], result.FailedChecks);
        Assert.Null(result.Counts);
        Assert.Equal(expectedCodes.Take(cap), result.Diagnostics.Where(d => d.Check == check).Select(d => d.Code));
        Assert.Equal(AssessmentDisposition.Fail, result.Assessments.Single(a => a.CheckNumber == check).Disposition);
    }

    [Fact]
    public void Given_WindowPassBeforeObservedFailures_When_Judging_Then_FailureAndAllDiagnosticFieldsSurvive()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        CheckAssessment firstFailure = RecordedEvidence.WindowFail(7, AssessmentClassification.Invalid) with
        { Artifact = "first-note", Row = 1, Field = "first-field", Code = "first_failure", Snippet = "abcde" };
        CheckAssessment secondFailure = firstFailure with
        { Artifact = "second-note", Row = 2, Field = "second-field", Code = null, Snippet = "\u00e9\u00e9a" };
        RunJudgementResult result = RunJudgement.Evaluate(request with
        {
            WindowAssessments = [RecordedEvidence.WindowPass(7), firstFailure, secondFailure],
            Limits = request.Limits with { MaxDiagnosticsPerCheck = 2, MaxDiagnosticBytes = 4 }
        });

        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        Assert.Equal([7], result.FailedChecks);
        Assert.Null(result.Counts);
        Assert.Equal(firstFailure, result.Assessments.Single(a => a.CheckNumber == 7));
        Assert.Equal([
            new ResultDiagnostic(7, "first_failure", "first-note", 1, "first-field", "abcd"),
            new ResultDiagnostic(7, "check_7_failed", "second-note", 2, "second-field", "\u00e9\u00e9")
        ], result.Diagnostics);
    }

    [Theory]
    [InlineData(null, null)] [InlineData("", "")] [InlineData("abc", "abc")]
    [InlineData("abcd", "abcd")] [InlineData("abcde", "abcd")]
    [InlineData("\u00e9\u00e9", "\u00e9\u00e9")] [InlineData("\u00e9\u00e9a", "\u00e9\u00e9")]
    [InlineData("\ud83d\ude00a", "\ud83d\ude00")]
    public void Given_SnippetAroundByteBoundary_When_Retaining_Then_CompleteUnicodeScalarsFit(string? snippet, string? expected)
    {
        Assert.Equal(expected, RunJudgement.TruncateSnippet(snippet, 4));
    }

    [Fact]
    public void Given_MultipleFailedChecksWithCapOne_When_Judging_Then_AllNumbersSurvive()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        RunJudgementResult result = RunJudgement.Evaluate(request with
        {
            Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Failure([
                new(1, "a", "ledger.jsonl", 1, null, null), new(1, "b", "ledger.jsonl", 2, null, null),
                new(1, "c", "ledger.jsonl", 3, null, null)]),
            Manifest = ParseResult<SealedRunManifest>.Failure(new StructuralProblem(2, "d", "manifest.json", null, null, null)),
            Capabilities = ParseResult<CapturedCapabilities>.Success(request.Capabilities.Value! with
                { ResolvedConfiguration = ParsedField<Parchwell.Verifier.VerifyRun.Input.ResolvedConfiguration>.Null() }),
            Limits = request.Limits with { MaxDiagnosticsPerCheck = 1 }
        });
        Assert.Equal([1, 2, 27], result.FailedChecks);
        Assert.Equal([1, 2], result.Diagnostics.Select(d => d.Check));
        Assert.Equal(AssessmentDisposition.Fail, result.ConfigurationFidelity.Assessment.Disposition);
    }

    [Fact]
    public void Given_IndexExecutionFailure_When_Judging_Then_NoNumberedCheckInvented()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => RunJudgement.Evaluate(request with
        { Index = ParseResult<ArtifactIndex>.ExecutionFailure(new StructuralProblem(null, "missing", "index.json", null, null, null)) }));
        Assert.Equal("Recorded evidence has an execution failure.", exception.Message);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("first")]
    [InlineData("second")]
    [InlineData("both")]
    [InlineData("empty")]
    public void Given_NativeExecutionFailureCombination_When_Judging_Then_AnyFailureStopsJudgement(string variation)
    {
        // Synthetic parse failures exercise both native report positions and an empty collection.
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        ReportAssociation[] reports = variation == "empty" ? [] : request.Reports.Take(2).ToArray();
        if (variation is "first" or "both") reports[0] = reports[0] with
        {
            Native = ParseResult<NativeClientReport>.ExecutionFailure(
                new StructuralProblem(null, "synthetic_io", "statistics.jsonl", null, null, null))
        };
        if (variation is "second" or "both") reports[1] = reports[1] with
        {
            Native = ParseResult<NativeClientReport>.ExecutionFailure(
                new StructuralProblem(null, "synthetic_io", "statistics.jsonl", null, null, null))
        };
        request = request with
        {
            Reports = reports,
            Statistics = ParseResult<IReadOnlyList<StatisticsEnvelope>>.Success(reports.Select(r => r.Envelope).ToArray())
        };
        if (variation is "first" or "second" or "both")
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => RunJudgement.Evaluate(request));
            Assert.Equal("Recorded evidence has an execution failure.", exception.Message);
        }
        else
        {
            RunJudgementResult result = RunJudgement.Evaluate(request);
            Assert.Equal(variation == "none" ? VerifierResultStatus.CompleteValid : VerifierResultStatus.Invalid, result.Status);
        }
    }
}
