using System.Globalization;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Judgement;

/// <summary>Retains ADR-021's nonfinal repeat judgement and unpublished in-window classifications.</summary>
internal sealed record B10RepeatJudgement(
    RunJudgementResult Judgement, OutcomeCounts? InWindowCounts);

/// <summary>Applies ADR-021's recorded per-repeat timeout, retry and restart rules.</summary>
internal static class B10RepeatChecks
{
    private static readonly string[] LifecycleEvents = ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];

    internal static bool IsPermittedTimeout(LedgerRow row) =>
        StringComparer.Ordinal.Equals(row.CompletionSource, "callback") && !row.Delivered &&
        row.ErrorCode == -192 && row.PersistenceStatus.Presence == FieldPresence.Value &&
        row.PersistenceStatus.Value is 0 or 1;

    internal static bool IsPermittedGuardMarker(string marker, RunJudgementRequest request)
    {
        if (marker == "async_delivery_failure_observed")
        {
            LedgerRow[]? rows = request.Ledger.Value?.Where(row =>
                row.CompletionSource == "callback" && !row.Delivered).ToArray();
            return rows is { Length: > 0 } && rows.All(IsPermittedTimeout);
        }
        if (marker == "possibly_persisted_observed")
        {
            LedgerRow[]? rows = request.Ledger.Value?.Where(row => row.CompletionSource == "callback" &&
                row.PersistenceStatus.Presence == FieldPresence.Value && row.PersistenceStatus.Value == 1).ToArray();
            return rows is { Length: > 0 } && rows.All(IsPermittedTimeout);
        }
        foreach (ReportAssociation report in request.Reports)
        {
            if (report.Native.Value is not { } native || native.Brokers.Presence != FieldPresence.Value ||
                native.Brokers.Value is not { } brokers) continue;
            foreach (NativeBrokerReport broker in brokers.Values)
            {
                if (broker.Name.Presence != FieldPresence.Value) continue;
                if (MatchesCounter(broker.RequestTimeouts, "req_timeouts") ||
                    MatchesCounter(broker.TransmissionRetries, "txretries")) return true;

                bool MatchesCounter(ParsedField<long> counter, string name) =>
                    counter.Presence == FieldPresence.Value && counter.Value > 0 &&
                    marker == FormattableString.Invariant($"broker={broker.Name.Value};counter={name};observed={counter.Value}");
            }
        }
        return false;
    }

    internal static B10RepeatJudgement Evaluate(
        RunJudgementRequest firstRequest, RunJudgementRequest secondRequest,
        RunJudgementResult first, RunJudgementResult second,
        IReadOnlyList<string> orderedInstanceIds, OutputLimits limits)
    {
        bool available = BookkeepingPassed(first) && BookkeepingPassed(second) &&
            firstRequest.Manifest.Value is not null && firstRequest.Ledger.Value is not null &&
            secondRequest.Manifest.Value is not null && secondRequest.Ledger.Value is not null;
        var firstFailures = new List<ResultDiagnostic>();
        var secondFailures = new List<ResultDiagnostic>();
        if (available)
        {
            // Only reconciled inventories can supply a unique composite-ID join.
            var firstRows = RowsByAttempt(firstRequest.Ledger.Value!);
            var secondRows = RowsByAttempt(secondRequest.Ledger.Value!);
            var latest = new Dictionary<(string Key, long Sequence), LedgerRow>();
            CheckRetries(firstRequest.Manifest.Value!, firstRows, firstFailures);
            CheckRetries(secondRequest.Manifest.Value!, secondRows, secondFailures);
            CheckRestart(firstRequest, secondRequest, firstRows, secondFailures);

            void CheckRetries(SealedRunManifest manifest,
                Dictionary<(string InstanceId, string AttemptId), (LedgerRow Row, long Number)> rows,
                List<ResultDiagnostic> failures)
            {
                foreach (AttemptEntry attempt in manifest.Inventory)
                {
                    (LedgerRow row, long number) = rows[(attempt.InstanceId, attempt.AttemptId)];
                    var key = (attempt.Key, attempt.Sequence);
                    if (latest.TryGetValue(key, out LedgerRow? previous) && !IsPermittedTimeout(previous))
                        failures.Add(new(4, "b10_retry_requires_permitted_timeout", "ledger.jsonl", number, "attempt", null));
                    latest[key] = row;
                }
            }
        }

        RunJudgementResult amendedFirst = Amend(first, CheckResult.Create(4, firstFailures, available), limits);
        RunJudgementResult amendedSecond = Amend(second, CheckResult.Create(4, secondFailures, available), limits);
        OutcomeCounts? counts = amendedFirst.Status == VerifierResultStatus.CompleteValid &&
            amendedSecond.Status == VerifierResultStatus.CompleteValid && available &&
            firstFailures.Count == 0 && secondFailures.Count == 0
            ? OutcomeClassifier.Classify(firstRequest.Ledger.Value!.Concat(secondRequest.Ledger.Value!).ToArray(),
                firstRequest.Records.Concat(secondRequest.Records).ToArray()) : null;
        // Aggregate retains b10_intake_only as the historical schema-one nonfinal-stage marker.
        return new(B10IntakeChecks.Aggregate(amendedFirst, amendedSecond, orderedInstanceIds, limits), counts);
    }

    private static bool BookkeepingPassed(RunJudgementResult judgement) =>
        Enumerable.Range(1, 6).All(check => judgement.Assessments.Any(assessment =>
            assessment.CheckNumber == check && assessment.Evaluation == AssessmentEvaluation.Evaluated &&
            assessment.Disposition == AssessmentDisposition.Pass));

    private static Dictionary<(string InstanceId, string AttemptId), (LedgerRow Row, long Number)> RowsByAttempt(
        IReadOnlyList<LedgerRow> ledger) => ledger.Select((row, index) => (Row: row, Number: index + 1L))
        .ToDictionary(item => (item.Row.Attempt.InstanceId, item.Row.Attempt.AttemptId));

    private static void CheckRestart(RunJudgementRequest firstRequest, RunJudgementRequest secondRequest,
        Dictionary<(string InstanceId, string AttemptId), (LedgerRow Row, long Number)> firstRows,
        List<ResultDiagnostic> failures)
    {
        SealedRunManifest first = firstRequest.Manifest.Value!;
        SealedRunManifest second = secondRequest.Manifest.Value!;
        RunIdentity identity = first.Identity;
        if (identity.ScenarioId != "B10" || second.Identity.ScenarioId != identity.ScenarioId ||
            second.Identity.ConfigurationId != identity.ConfigurationId || second.Identity.RunId != identity.RunId ||
            first.WorkerStatus != "complete" || first.Restart is not null || second.Restart is null ||
            second.Restart.PredecessorInstanceId != firstRequest.Identity.InstanceId)
        {
            failures.Add(new(4, "restart_identity_mismatch", "manifest.json", null, "restart.predecessor_instance_id", null));
            return;
        }

        var latestBySlot = new SortedDictionary<long, AttemptEntry>();
        foreach (AttemptEntry attempt in first.Inventory)
        {
            if (!TryGetLogicalSlot(attempt, out long slot))
            {
                failures.Add(new(4, "restart_plan_mismatch", "manifest.json", null, "restart.planned", null));
                return;
            }
            latestBySlot[slot] = attempt;
        }
        var expected = new List<RestartPlannedEntry>();
        foreach (AttemptEntry attempt in latestBySlot.Values)
        {
            if (!IsPermittedTimeout(firstRows[(attempt.InstanceId, attempt.AttemptId)].Row)) continue;
            expected.Add(new(expected.Count, attempt.AttemptId, attempt.TenantId, attempt.DocumentId,
                attempt.Key, attempt.Sequence, attempt.EventType, attempt.PlannedMicroseconds));
        }
        if (!expected.SequenceEqual(second.Restart.Planned))
            failures.Add(new(4, "restart_plan_mismatch", "manifest.json", null, "restart.planned", null));
    }

    private static bool TryGetLogicalSlot(AttemptEntry entry, out long slot)
    {
        slot = 0;
        if (entry.Sequence < 0 || entry.Sequence >= LifecycleEvents.Length ||
            entry.EventType != LifecycleEvents[(int)entry.Sequence] || entry.DocumentId.Length < 2 || entry.DocumentId[0] != 'd' ||
            !long.TryParse(entry.DocumentId.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out long ordinal) || ordinal < 0 ||
            entry.DocumentId != string.Concat("d", ordinal.ToString(CultureInfo.InvariantCulture)) ||
            entry.Key != string.Concat(entry.TenantId, "-", entry.DocumentId)) return false;
        try
        {
            slot = checked(ordinal * 5 + entry.Sequence);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static RunJudgementResult Amend(RunJudgementResult previous, CheckResult additional, OutputLimits limits)
    {
        CheckAssessment[] candidates = [previous.Assessments.FirstOrDefault(a => a.CheckNumber == 4)
            ?? CheckResult.NotEvaluated(4).Assessment, additional.Assessment];
        CheckAssessment merged = candidates.FirstOrDefault(a => a.Disposition == AssessmentDisposition.Fail &&
                a.Classification == AssessmentClassification.Invalid)
            ?? candidates.FirstOrDefault(a => a.Disposition == AssessmentDisposition.Fail &&
                a.Classification == AssessmentClassification.Incomplete)
            ?? candidates.FirstOrDefault(a => a.Evaluation == AssessmentEvaluation.NotEvaluated)
            ?? candidates.FirstOrDefault(a => a.Disposition == AssessmentDisposition.Pass)
            ?? CheckResult.NotEvaluated(4).Assessment;
        CheckAssessment[] assessments = previous.Assessments.Where(a => a.CheckNumber != 4).Append(merged)
            .OrderBy(a => a.CheckNumber).ToArray();
        int[] failedChecks = previous.FailedChecks.Concat(merged.Disposition == AssessmentDisposition.Fail ? [4] : Array.Empty<int>())
            .Distinct().Order().ToArray();
        var diagnostics = new List<ResultDiagnostic>();
        var countsByCheck = new Dictionary<int, int> { [27] = 1 };
        foreach (ResultDiagnostic diagnostic in previous.Diagnostics.Concat(additional.Diagnostics))
        {
            int count = countsByCheck.GetValueOrDefault(diagnostic.Check);
            if (count >= limits.MaxDiagnosticsPerCheck) continue;
            diagnostics.Add(diagnostic with { Snippet = RunJudgement.TruncateSnippet(diagnostic.Snippet, limits.MaxDiagnosticBytes) });
            countsByCheck[diagnostic.Check] = count + 1;
        }
        VerifierResultStatus? status = previous.Status == VerifierResultStatus.Invalid ||
            assessments.Any(a => a.Disposition == AssessmentDisposition.Fail &&
                a.Classification == AssessmentClassification.Invalid)
            ? VerifierResultStatus.Invalid
            : previous.Status == VerifierResultStatus.Incomplete ||
                assessments.Any(a => a.Disposition == AssessmentDisposition.Fail &&
                    a.Classification == AssessmentClassification.Incomplete)
                ? VerifierResultStatus.Incomplete
                : Enumerable.Range(1, 27).All(check => assessments.Any(a =>
                    a.CheckNumber == check && a.Evaluation == AssessmentEvaluation.Evaluated &&
                    a.Disposition == AssessmentDisposition.Pass))
                    ? VerifierResultStatus.CompleteValid : null;
        RunJudgementResult amended = previous with
        {
            Status = status,
            Assessments = assessments,
            FailedChecks = failedChecks,
            Diagnostics = diagnostics,
            Counts = status == VerifierResultStatus.CompleteValid ? previous.Counts : null
        };
        return amended;
    }
}
