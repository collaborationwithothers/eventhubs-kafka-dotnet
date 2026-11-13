using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Judgement;

/// <summary>Applies the ordinary C0 producer's application retry rule after independent bookkeeping.</summary>
internal static class B10OneProducerChecks
{
    internal static RunJudgementResult Evaluate(RunJudgementRequest request, RunJudgementResult judgement)
    {
        if (!Enumerable.Range(1, 6).All(check => judgement.Assessments.Any(assessment =>
            assessment.CheckNumber == check && assessment.Evaluation == AssessmentEvaluation.Evaluated &&
            assessment.Disposition == AssessmentDisposition.Pass)) || request.Manifest.Value is null || request.Ledger.Value is null)
            return judgement;
        var rows = request.Ledger.Value.Select((row, index) => (Row: row, Number: index + 1L))
            .ToDictionary(item => (item.Row.Attempt.InstanceId, item.Row.Attempt.AttemptId));
        var latest = new Dictionary<(string Key, long Sequence), LedgerRow>();
        var failures = new List<ResultDiagnostic>();
        foreach (AttemptEntry attempt in request.Manifest.Value.Inventory)
        {
            var key = (attempt.Key, attempt.Sequence);
            (LedgerRow row, long number) = rows[(attempt.InstanceId, attempt.AttemptId)];
            if (latest.TryGetValue(key, out LedgerRow? previous) && !B10RepeatChecks.IsPermittedTimeout(previous))
                failures.Add(new(4, "b10_retry_requires_permitted_timeout", "ledger.jsonl", number, "attempt", null));
            latest[key] = row;
        }
        if (failures.Count == 0) return judgement;
        CheckResult failure = CheckResult.Fail(4, failures);
        ResultDiagnostic[] diagnostics = judgement.Diagnostics.Concat(failures)
            .GroupBy(diagnostic => diagnostic.Check).SelectMany(group => group.Take(request.Limits.MaxDiagnosticsPerCheck)).ToArray();
        return judgement with
        {
            Status = VerifierResultStatus.Invalid,
            Counts = null,
            FailedChecks = judgement.FailedChecks.Append(4).Distinct().Order().ToArray(),
            Assessments = judgement.Assessments.Where(assessment => assessment.CheckNumber != 4)
                .Append(failure.Assessment).OrderBy(assessment => assessment.CheckNumber).ToArray(),
            Diagnostics = diagnostics
        };
    }
}
