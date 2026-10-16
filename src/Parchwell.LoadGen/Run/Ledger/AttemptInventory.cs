using System.Collections.Immutable;
using Parchwell.LoadGen.Run.Contracts;

namespace Parchwell.LoadGen.Run.Ledger;

/// <summary>Independent bounded record of every actual application invocation.</summary>
public sealed class AttemptInventory
{
    private readonly RunIdentity identity;
    private readonly int maximumAttempts;
    private readonly object gate = new();
    private readonly List<AttemptEntry> entries = [];
    private readonly HashSet<string> attemptIds = new(StringComparer.Ordinal);
    private readonly HashSet<long> ordinals = [];
    private bool sealedInventory;

    /// <summary>Creates an inventory with a finite memory bound.</summary>
    public AttemptInventory(RunIdentity identity, int maximumAttempts)
    {
        this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
        identity.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumAttempts);
        this.maximumAttempts = maximumAttempts;
    }

    /// <summary>Registers an attempt before the corresponding native Produce call.</summary>
    public void Register(AttemptEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (gate)
        {
            if (sealedInventory)
                throw new InvalidOperationException("inventory_already_sealed");
            if (entries.Count >= maximumAttempts)
                throw new InvalidOperationException("inventory_capacity_exceeded");
            if (string.IsNullOrWhiteSpace(entry.AttemptId) || string.IsNullOrWhiteSpace(entry.InstanceId) ||
                !string.Equals(entry.InstanceId, identity.InstanceId, StringComparison.Ordinal) ||
                entry.Ordinal < 0 || entry.Sequence < 0 || entry.PlannedMicroseconds < 0 || entry.InvokedMicroseconds < entry.PlannedMicroseconds ||
                string.IsNullOrWhiteSpace(entry.TenantId) || string.IsNullOrWhiteSpace(entry.DocumentId) ||
                string.IsNullOrWhiteSpace(entry.EventType) || entry.Key is null)
                throw new ArgumentException("invalid_inventory_entry", nameof(entry));
            if (entries.Count > 0 && entry.Ordinal <= entries[^1].Ordinal)
                throw new InvalidOperationException("inventory_ordinals_must_increase");
            if (attemptIds.Contains(entry.AttemptId) || ordinals.Contains(entry.Ordinal))
                throw new InvalidOperationException("duplicate_inventory_attempt_or_ordinal");
            attemptIds.Add(entry.AttemptId);
            ordinals.Add(entry.Ordinal);
            entries.Add(entry);
        }
    }

    /// <summary>Seals and returns an immutable snapshot. Repeated calls return the same snapshot.</summary>
    public ImmutableArray<AttemptEntry> Seal()
    {
        lock (gate)
        {
            sealedInventory = true;
            return entries.ToImmutableArray();
        }
    }
}
