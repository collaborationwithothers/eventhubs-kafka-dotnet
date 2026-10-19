using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Judgement;

/// <summary>Checks structural bookkeeping, run identity and joins against the sealed attempt inventory.</summary>
internal static class BookkeepingChecks
{
    internal static IReadOnlyList<CheckResult> Evaluate(RunJudgementRequest request)
    {
        // Detect repeated bare IDs before reconciliation constructs any join lookup.
        CheckResult uniqueAttempts = UniqueAttempts(request);
        return
        [
            Structural(1, request.Ledger),
            Structural(2, request.Manifest),
            Identity(request),
            Reconciliation(request),
            uniqueAttempts,
            ConsumedRecords(request)
        ];
    }

    private static CheckResult Structural<T>(int check, ParseResult<T> parsed) =>
        parsed.Problems.Count > 0 ? CheckResult.FromProblems(check, parsed.Problems)
            : parsed.Value is not null ? CheckResult.Pass(check) : CheckResult.NotEvaluated(check);

    private static CheckResult Identity(RunJudgementRequest request)
    {
        var failures = new List<ResultDiagnostic>();
        bool available = request.Index.Value is not null && request.Manifest.Value is not null &&
            request.Ledger.Value is not null && request.Statistics.Value is not null &&
            request.Lifecycle.Value is not null && request.Capabilities.Value is not null;
        if (request.Index.Value is { } index) Compare(index.Identity, "index.json", null);
        if (request.Manifest.Value is { } manifest)
        {
            Compare(manifest.Identity, "manifest.json", null);
            for (int i = 0; i < manifest.Inventory.Count; i++)
                CompareInstance(manifest.Inventory[i].InstanceId, "manifest.json", null, $"inventory[{i}].instance_id");
        }
        if (request.Ledger.Value is { } ledger)
        {
            for (int i = 0; i < ledger.Count; i++)
            {
                Compare(ledger[i].Identity, "ledger.jsonl", i + 1L);
                CompareInstance(ledger[i].Attempt.InstanceId, "ledger.jsonl", i + 1L, "attempt.instance_id");
            }
        }
        if (request.Statistics.Value is { } statistics)
            for (int i = 0; i < statistics.Count; i++) Compare(statistics[i].Identity, "statistics.jsonl", i + 1L);
        if (request.Lifecycle.Value is { } lifecycle)
            for (int i = 0; i < lifecycle.Count; i++) Compare(lifecycle[i].Identity, "request-lifecycle.jsonl", i + 1L);
        if (request.Capabilities.Value is { } capabilities) Compare(capabilities.Identity, "capabilities.json", null);
        for (int i = 0; i < request.Records.Count; i++)
        {
            if (request.Records[i].InstanceId is { } instance)
                CompareInstance(instance, "consumed", i + 1L, "instance_id");
            else available = false;
        }
        return CheckResult.Create(3, failures, available);

        void Compare(RunIdentity actual, string artifact, long? row)
        {
            CompareField(actual.ScenarioId, request.Identity.ScenarioId, "identity.scenario_id");
            CompareField(actual.ConfigurationId, request.Identity.ConfigurationId, "identity.configuration_id");
            CompareField(actual.RunId, request.Identity.RunId, "identity.run_id");
            CompareField(actual.InstanceId, request.Identity.InstanceId, "identity.instance_id");
            CompareField(actual.ExpectedInstanceId, request.Identity.ExpectedInstanceId, "identity.expected_instance_id");
            CompareField(actual.ClientId, request.Identity.ClientId, "identity.client_id");

            void CompareField(string value, string expected, string field)
            {
                if (!StringComparer.Ordinal.Equals(value, expected)) failures.Add(Failure(3, artifact, row, field));
            }
        }

        void CompareInstance(string actual, string artifact, long? row, string field)
        {
            if (!StringComparer.Ordinal.Equals(actual, request.Identity.ExpectedInstanceId))
                failures.Add(Failure(3, artifact, row, field));
        }
    }

    private static CheckResult Reconciliation(RunJudgementRequest request)
    {
        var failures = new List<ResultDiagnostic>();
        SealedRunManifest? manifest = request.Manifest.Value;
        IReadOnlyList<LedgerRow>? ledger = request.Ledger.Value;
        if (manifest is not null)
        {
            if (manifest.AttemptCount != manifest.Inventory.Count)
                failures.Add(Failure(4, "manifest.json", null, "attempt_count"));
            var seen = new HashSet<(string InstanceId, string AttemptId)>();
            for (int i = 0; i < manifest.Inventory.Count; i++)
            {
                AttemptEntry attempt = manifest.Inventory[i];
                if (!seen.Add((attempt.InstanceId, attempt.AttemptId)))
                    failures.Add(Failure(4, "manifest.json", null, $"inventory[{i}].attempt_id"));
            }
        }
        if (ledger is not null)
        {
            var seen = new HashSet<(string InstanceId, string AttemptId)>();
            for (int i = 0; i < ledger.Count; i++)
            {
                AttemptEntry attempt = ledger[i].Attempt;
                if (!seen.Add((attempt.InstanceId, attempt.AttemptId)))
                    failures.Add(Failure(4, "ledger.jsonl", i + 1L, "attempt.attempt_id"));
            }
        }
        if (manifest is not null && ledger is not null)
        {
            if (manifest.AttemptCount != ledger.Count)
                failures.Add(Failure(4, "ledger.jsonl", null, "attempt_count"));
            var inventory = manifest.Inventory.ToLookup(a => (a.InstanceId, a.AttemptId));
            var rows = ledger.ToLookup(r => (r.Attempt.InstanceId, r.Attempt.AttemptId));
            for (int i = 0; i < manifest.Inventory.Count; i++)
            {
                AttemptEntry attempt = manifest.Inventory[i];
                LedgerRow[] matching = rows[(attempt.InstanceId, attempt.AttemptId)].ToArray();
                if (matching.Length != 1)
                    failures.Add(Failure(4, "manifest.json", null, $"inventory[{i}].attempt_id"));
            }
            for (int i = 0; i < ledger.Count; i++)
            {
                AttemptEntry actual = ledger[i].Attempt;
                AttemptEntry[] expected = inventory[(actual.InstanceId, actual.AttemptId)].ToArray();
                if (expected.Length != 1)
                    failures.Add(Failure(4, "ledger.jsonl", i + 1L, "attempt.attempt_id"));
                else
                {
                    if (!StringComparer.Ordinal.Equals(actual.Key, expected[0].Key))
                        failures.Add(Failure(4, "ledger.jsonl", i + 1L, "attempt.key"));
                    if (actual.Sequence != expected[0].Sequence)
                        failures.Add(Failure(4, "ledger.jsonl", i + 1L, "attempt.sequence"));
                }
            }
        }
        return CheckResult.Create(4, failures, manifest is not null && ledger is not null);
    }

    private static CheckResult UniqueAttempts(RunJudgementRequest request)
    {
        var failures = new List<ResultDiagnostic>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (request.Ledger.Value is { } ledger)
            for (int i = 0; i < ledger.Count; i++)
                if (!seen.Add(ledger[i].Attempt.AttemptId))
                    failures.Add(Failure(5, "ledger.jsonl", i + 1L, "attempt.attempt_id"));
        return CheckResult.Create(5, failures, request.Ledger.Value is not null);
    }

    private static CheckResult ConsumedRecords(RunJudgementRequest request)
    {
        var failures = new List<ResultDiagnostic>();
        var rows = request.Ledger.Value?.ToLookup(r => (r.Attempt.InstanceId, r.Attempt.AttemptId));
        for (int i = 0; i < request.Records.Count; i++)
        {
            ConsumedRecord record = request.Records[i];
            failures.AddRange(record.Problems.Select(p => new ResultDiagnostic(6, p.Code, p.Artifact, p.Row, p.Field, p.Snippet)));
            if (record.InstanceId is null) failures.Add(Failure(6, "consumed", i + 1L, "instance_id"));
            if (record.AttemptId is null) failures.Add(Failure(6, "consumed", i + 1L, "attempt_id"));
            if (record.Key is null) failures.Add(Failure(6, "consumed", i + 1L, "key"));
            if (record.Sequence is null) failures.Add(Failure(6, "consumed", i + 1L, "sequence"));
            if (rows is null || record.InstanceId is null || record.AttemptId is null) continue;
            LedgerRow[] matching = rows[(record.InstanceId, record.AttemptId)].ToArray();
            if (matching.Length != 1) failures.Add(Failure(6, "consumed", i + 1L, "attempt_id"));
            else
            {
                if (record.Key is not null && !StringComparer.Ordinal.Equals(record.Key, matching[0].Attempt.Key))
                    failures.Add(Failure(6, "consumed", i + 1L, "key"));
                if (record.Sequence is not null && record.Sequence != matching[0].Attempt.Sequence)
                    failures.Add(Failure(6, "consumed", i + 1L, "sequence"));
            }
        }
        return CheckResult.Create(6, failures, rows is not null);
    }

    private static ResultDiagnostic Failure(int check, string artifact, long? row, string field) =>
        new(check, $"check_{check}_failed", artifact, row, field, null);
}
