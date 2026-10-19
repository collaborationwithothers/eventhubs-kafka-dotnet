using System.Text;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Judgement;

/// <summary>Supplies recorded inputs and independently selected expectations to pure judgement.</summary>
internal sealed record RunJudgementRequest(
    RunIdentity Identity,
    ParseResult<ArtifactIndex> Index,
    ParseResult<IReadOnlyList<LedgerRow>> Ledger,
    ParseResult<SealedRunManifest> Manifest,
    ParseResult<IReadOnlyList<StatisticsEnvelope>> Statistics,
    ParseResult<IReadOnlyList<LifecycleEvent>> Lifecycle,
    ParseResult<CapturedCapabilities> Capabilities,
    IReadOnlyList<ReportAssociation> Reports,
    IReadOnlyList<string> ExpectedBrokerNames,
    ResolvedRoleConfiguration ResolvedProducerConfiguration,
    IReadOnlyList<ConsumedRecord> Records,
    IReadOnlyList<CheckAssessment> WindowAssessments,
    OutputLimits Limits);

/// <summary>Retains one check's complete observations before diagnostic retention.</summary>
internal sealed record CheckResult(CheckAssessment Assessment, IReadOnlyList<ResultDiagnostic> Diagnostics)
{
    internal static CheckResult Pass(int check) => new(new(check, AssessmentEvaluation.Evaluated,
        AssessmentDisposition.Pass, null, null, null, null, null, null), []);

    internal static CheckResult NotEvaluated(int check) => new(new(check, AssessmentEvaluation.NotEvaluated,
        null, null, null, null, null, null, null), []);

    internal static CheckResult Fail(int check, IReadOnlyList<ResultDiagnostic> diagnostics)
    {
        ResultDiagnostic first = diagnostics[0];
        return new(new(check, AssessmentEvaluation.Evaluated, AssessmentDisposition.Fail,
            AssessmentClassification.Invalid, first.Artifact, first.Row, first.Field, first.Code, first.Snippet), diagnostics);
    }

    internal static CheckResult FromProblems(int check, IReadOnlyList<StructuralProblem> problems) =>
        Fail(check, problems.Select(p => new ResultDiagnostic(check, p.Code, p.Artifact, p.Row, p.Field, p.Snippet)).ToArray());

    internal static CheckResult Create(int check, IReadOnlyList<ResultDiagnostic> diagnostics, bool prerequisitesAvailable) =>
        diagnostics.Count > 0 ? Fail(check, diagnostics) : prerequisitesAvailable ? Pass(check) : NotEvaluated(check);
}

/// <summary>Retains the internal aggregate, including an unpublishable absent status.</summary>
internal sealed record RunJudgementResult(
    VerifierResultStatus? Status,
    IReadOnlyList<int> FailedChecks,
    IReadOnlyList<CheckAssessment> Assessments,
    OutcomeCounts? Counts,
    BrokerConnectionCount? BrokerCount,
    ConfigurationFidelityResult ConfigurationFidelity,
    IReadOnlyList<ResultDiagnostic> Diagnostics);

/// <summary>Combines the approved recorded-input checks without performing window operations.</summary>
internal static class RunJudgement
{
    internal static RunJudgementResult Evaluate(RunJudgementRequest request)
    {
        ThrowIfExecutionFailure(request.Index.IsExecutionFailure || request.Ledger.IsExecutionFailure ||
            request.Manifest.IsExecutionFailure || request.Statistics.IsExecutionFailure ||
            request.Lifecycle.IsExecutionFailure || request.Capabilities.IsExecutionFailure ||
            request.Reports.Any(r => r.Native.IsExecutionFailure));
        ConfigurationFidelityResult fidelity = ConfigurationFidelityCheck.Evaluate(request);
        var results = new List<CheckResult>();
        results.AddRange(BookkeepingChecks.Evaluate(request));
        for (int check = 7; check <= 11; check++)
        {
            CheckAssessment? supplied = request.WindowAssessments.FirstOrDefault(a => a.CheckNumber == check);
            results.Add(supplied is null ? CheckResult.NotEvaluated(check) : new(supplied,
                supplied.Disposition == AssessmentDisposition.Fail
                    ? [new(check, supplied.Code ?? $"check_{check}_failed", supplied.Artifact,
                        supplied.Row, supplied.Field, supplied.Snippet)] : []));
        }
        results.AddRange(LateWriteChecks.Evaluate(request));
        results.AddRange(StatisticsChecks.Evaluate(request));
        results.Add(new(fidelity.Assessment, []));
        CheckAssessment[] assessments = results.Select(r => r.Assessment).OrderBy(a => a.CheckNumber).ToArray();
        int[] failed = assessments.Where(a => a.Disposition == AssessmentDisposition.Fail)
            .Select(a => a.CheckNumber).Distinct().Order().ToArray();
        VerifierResultStatus? status = assessments.Any(a => a.Disposition == AssessmentDisposition.Fail &&
                a.Classification == AssessmentClassification.Invalid) ? VerifierResultStatus.Invalid
            : assessments.Any(a => a.Disposition == AssessmentDisposition.Fail &&
                a.Classification == AssessmentClassification.Incomplete) ? VerifierResultStatus.Incomplete
            : Enumerable.Range(1, 27).All(check => assessments.Any(a => a.CheckNumber == check &&
                a.Evaluation == AssessmentEvaluation.Evaluated && a.Disposition == AssessmentDisposition.Pass))
                ? VerifierResultStatus.CompleteValid : null;
        var retained = new List<ResultDiagnostic>();
        var countsByCheck = new Dictionary<int, int>();
        // The embedded fidelity assessment consumes the codec's existing check-27 slot.
        countsByCheck[27] = 1;
        foreach (ResultDiagnostic diagnostic in results.SelectMany(r => r.Diagnostics))
        {
            int count = countsByCheck.GetValueOrDefault(diagnostic.Check);
            if (count >= request.Limits.MaxDiagnosticsPerCheck) continue;
            retained.Add(diagnostic with { Snippet = TruncateSnippet(diagnostic.Snippet, request.Limits.MaxDiagnosticBytes) });
            countsByCheck[diagnostic.Check] = count + 1;
        }
        fidelity = fidelity with
        {
            Assessment = fidelity.Assessment with { Snippet = TruncateSnippet(fidelity.Assessment.Snippet, request.Limits.MaxDiagnosticBytes) },
            Differences = fidelity.Differences.OrderBy(d => d.Setting, StringComparer.Ordinal)
                .Take(request.Limits.MaxDiagnosticsPerCheck).ToArray()
        };
        return new(status, failed, assessments,
            status == VerifierResultStatus.CompleteValid ? OutcomeClassifier.Classify(request.Ledger.Value!, request.Records) : null,
            BrokerCountRecorder.Record(request, assessments), fidelity, retained);
    }

    private static void ThrowIfExecutionFailure(bool executionFailure)
    {
        if (executionFailure) throw new InvalidOperationException("Recorded evidence has an execution failure.");
    }

    internal static string? TruncateSnippet(string? snippet, int maximumBytes)
    {
        if (snippet is null) return null;
        var result = new StringBuilder();
        int bytes = 0;
        foreach (Rune rune in snippet.EnumerateRunes())
        {
            if (rune.Utf8SequenceLength > maximumBytes - bytes) break;
            result.Append(rune.ToString());
            bytes += rune.Utf8SequenceLength;
        }
        return result.ToString();
    }
}
