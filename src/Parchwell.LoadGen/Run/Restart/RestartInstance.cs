using System.Collections.Immutable;
using Parchwell.LoadGen.Run.Contracts;

namespace Parchwell.LoadGen.Run.Restart;

/// <summary>A restarted B10 instance: its own identity, its predecessor's sealed evidence and the resend list selected from it.</summary>
public sealed class RestartInstance
{
    private RestartInstance(RunIdentity identity, RunManifest predecessorManifest,
        ImmutableArray<LedgerRow> predecessorRows, RestartManifest plan)
    {
        Identity = identity;
        PredecessorManifest = predecessorManifest;
        PredecessorRows = predecessorRows;
        Plan = plan;
    }

    /// <summary>The restarted instance's own run identity.</summary>
    public RunIdentity Identity { get; }

    /// <summary>The predecessor's sealed manifest.</summary>
    public RunManifest PredecessorManifest { get; }

    /// <summary>The predecessor's ledger rows, validated against its manifest.</summary>
    public ImmutableArray<LedgerRow> PredecessorRows { get; }

    /// <summary>The resend list and the predecessor's instance ID.</summary>
    public RestartManifest Plan { get; }

    /// <summary>Validates the predecessor's evidence and the restarted identity, then selects the resend list.</summary>
    public static RestartInstance Create(RunIdentity identity, RunManifest predecessorManifest,
        IEnumerable<LedgerRow> predecessorRows)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(predecessorManifest);
        ArgumentNullException.ThrowIfNull(predecessorRows);
        identity.Validate();
        if (!string.Equals(identity.ScenarioId, "B10", StringComparison.Ordinal))
            throw new InvalidDataException("restart_manifest_requires_b10");
        ImmutableArray<LedgerRow> rows = predecessorRows.ToImmutableArray();
        RestartManifest plan = RestartPlan.Select(predecessorManifest, rows);
        if (!RestartPlan.SameRepeat(identity, predecessorManifest.Identity))
            throw new InvalidDataException("restart_identity_mismatch");
        if (string.Equals(identity.InstanceId, plan.PredecessorInstanceId, StringComparison.Ordinal))
            throw new InvalidDataException("restart_predecessor_invalid");
        return new RestartInstance(identity, predecessorManifest, rows, plan);
    }
}
