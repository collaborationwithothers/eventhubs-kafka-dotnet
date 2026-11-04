using System.Collections.Immutable;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Workload;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Restart;

/// <summary>Selects, regenerates and checks the resends of a restarted B10 instance (ADR-021).</summary>
public static class RestartPlan
{
    /// <summary>Returns the resend list: for each logical slot, the latest predecessor attempt when its row is a permitted timeout, in ascending slot order.</summary>
    public static RestartManifest Select(RunManifest predecessor, IEnumerable<LedgerRow> predecessorRows)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        ArgumentNullException.ThrowIfNull(predecessorRows);
        if (predecessor.Restart is not null) throw new InvalidDataException("restart_predecessor_is_restart");
        if (predecessor.Identity is null || !string.Equals(predecessor.Identity.ScenarioId, "B10", StringComparison.Ordinal))
            throw new InvalidDataException("restart_manifest_requires_b10");
        if (!string.Equals(predecessor.WorkerStatus, "complete", StringComparison.Ordinal))
            throw new InvalidDataException("restart_predecessor_incomplete");
        ImmutableArray<LedgerRow> rows = LedgerValidator.Validate(predecessorRows, predecessor, predecessor.Identity);
        var rowsByAttempt = rows.ToDictionary(row => row.Attempt.AttemptId, StringComparer.Ordinal);
        var latestBySlot = new SortedDictionary<long, AttemptEntry>();
        foreach (AttemptEntry entry in predecessor.Inventory)
            latestBySlot[LedgerValidator.GetLogicalScheduleOrdinal(entry)] = entry;
        var planned = ImmutableArray.CreateBuilder<RestartPlannedEntry>();
        foreach (AttemptEntry latest in latestBySlot.Values)
        {
            if (!ApplicationRetryPolicy.IsPermittedTimeout(rowsByAttempt[latest.AttemptId])) continue;
            planned.Add(new RestartPlannedEntry(planned.Count, latest.AttemptId, latest.TenantId, latest.DocumentId,
                latest.Key, latest.Sequence, latest.EventType, latest.PlannedMicroseconds));
        }
        return new RestartManifest(predecessor.Identity.InstanceId, planned.ToImmutable());
    }

    /// <summary>Streams the original workload event of each resend, in resend order, after checking it against its planned entry.</summary>
    public static IEnumerable<PlannedEvent> Events(WorkloadProfile profile, RestartManifest restart, long maxAttempts,
        int maxPayloadBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(restart);
        IEnumerable<PlannedEvent> workload = WorkloadGenerator.Generate(profile, maxAttempts, maxPayloadBytes, cancellationToken);
        return Match(workload, restart.Planned);
    }

    /// <summary>Requires a restart manifest's resend list to equal the selection recomputed from its predecessor's manifest and rows.</summary>
    public static void ValidateAgainstPredecessor(RunManifest manifest, RunManifest predecessor,
        IEnumerable<LedgerRow> predecessorRows)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        LedgerValidator.ValidateManifest(manifest);
        RestartManifest expected = Select(predecessor, predecessorRows);
        if (manifest.Restart is null ||
            !string.Equals(manifest.Restart.PredecessorInstanceId, expected.PredecessorInstanceId, StringComparison.Ordinal) ||
            !manifest.Restart.Planned.SequenceEqual(expected.Planned))
            throw new InvalidDataException("restart_plan_mismatch");
        if (!SameRepeat(manifest.Identity, predecessor.Identity))
            throw new InvalidDataException("restart_identity_mismatch");
    }

    internal static bool SameRepeat(RunIdentity restarted, RunIdentity predecessor) =>
        string.Equals(restarted.ScenarioId, predecessor.ScenarioId, StringComparison.Ordinal) &&
        string.Equals(restarted.ConfigurationId, predecessor.ConfigurationId, StringComparison.Ordinal) &&
        string.Equals(restarted.RunId, predecessor.RunId, StringComparison.Ordinal);

    private static IEnumerable<PlannedEvent> Match(IEnumerable<PlannedEvent> workload,
        ImmutableArray<RestartPlannedEntry> planned)
    {
        if (planned.Length == 0) yield break;
        var index = 0;
        foreach (PlannedEvent candidate in workload)
        {
            RestartPlannedEntry entry = planned[index];
            long slot = LedgerValidator.GetLogicalScheduleOrdinal(entry.DocumentId, entry.Sequence);
            if (candidate.Ordinal < slot) continue;
            if (candidate.Ordinal != slot ||
                !string.Equals(candidate.TenantId, entry.TenantId, StringComparison.Ordinal) ||
                !string.Equals(candidate.DocumentId, entry.DocumentId, StringComparison.Ordinal) ||
                !string.Equals(candidate.Key, entry.Key, StringComparison.Ordinal) || candidate.Sequence != entry.Sequence ||
                !string.Equals(candidate.EventType, entry.EventType, StringComparison.Ordinal) ||
                candidate.DueMicroseconds != entry.PlannedMicroseconds)
                throw new InvalidDataException("restart_workload_mismatch");
            yield return candidate;
            index++;
            if (index == planned.Length) yield break;
        }
        throw new InvalidDataException("restart_workload_mismatch");
    }
}
