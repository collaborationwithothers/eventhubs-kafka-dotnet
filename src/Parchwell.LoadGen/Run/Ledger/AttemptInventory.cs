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
    private readonly Dictionary<long, AttemptEntry> logicalAttempts = [];
    private long scheduleProgress;
    private bool sealedInventory;
    private AttemptEntry? reservedAttempt;
    private long reservedLogicalSlot;

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
            if (reservedAttempt is not null) throw new InvalidOperationException("inventory_reservation_active");
            long logicalSlot = ValidateNext(entry);
            Commit(entry, logicalSlot);
        }
    }

    /// <summary>Reserves and validates an attempt before its final invocation timestamp is sampled.</summary>
    internal void Reserve(AttemptEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (gate)
        {
            if (reservedAttempt is not null) throw new InvalidOperationException("inventory_reservation_already_active");
            reservedLogicalSlot = ValidateNext(entry);
            entries.EnsureCapacity(entries.Count + 1);
            attemptIds.EnsureCapacity(attemptIds.Count + 1);
            ordinals.EnsureCapacity(ordinals.Count + 1);
            if (reservedLogicalSlot == scheduleProgress)
                logicalAttempts.EnsureCapacity(logicalAttempts.Count + 1);
            reservedAttempt = entry;
        }
    }

    /// <summary>Commits the active reservation with the timestamp sampled at the Produce boundary.</summary>
    internal AttemptEntry CommitReservation(long invokedMicroseconds)
    {
        lock (gate)
        {
            AttemptEntry entry = reservedAttempt ?? throw new InvalidOperationException("inventory_reservation_missing");
            if (invokedMicroseconds < entry.PlannedMicroseconds)
                throw new InvalidOperationException("inventory_invocation_precedes_schedule");
            entry.SetInvokedMicroseconds(invokedMicroseconds);
            Commit(entry, reservedLogicalSlot);
            reservedAttempt = null;
            reservedLogicalSlot = 0;
            return entry;
        }
    }

    /// <summary>Discards a reserved attempt that did not reach the native invocation.</summary>
    internal void CancelReservation()
    {
        lock (gate)
        {
            reservedAttempt = null;
            reservedLogicalSlot = 0;
        }
    }

    /// <summary>Seals and returns an immutable snapshot. Repeated calls return the same snapshot.</summary>
    public ImmutableArray<AttemptEntry> Seal()
    {
        lock (gate)
        {
            sealedInventory = true;
            reservedAttempt = null;
            reservedLogicalSlot = 0;
            return entries.ToImmutableArray();
        }
    }

    private long ValidateNext(AttemptEntry entry)
    {
        if (sealedInventory)
            throw new InvalidOperationException("inventory_already_sealed");
        if (entries.Count >= maximumAttempts)
            throw new InvalidOperationException("inventory_capacity_exceeded");
        LedgerValidator.ValidateAttempt(entry, identity);
        if (entries.Count > 0 && entry.Ordinal <= entries[^1].Ordinal)
            throw new InvalidOperationException("inventory_ordinals_must_increase");
        if (entry.Ordinal != entries.Count)
            throw new InvalidOperationException("inventory_ordinal_must_be_contiguous");
        if (attemptIds.Contains(entry.AttemptId) || ordinals.Contains(entry.Ordinal))
            throw new InvalidOperationException("duplicate_inventory_attempt_or_ordinal");
        long logicalSlot = LedgerValidator.GetLogicalScheduleOrdinal(entry);
        if (logicalSlot != scheduleProgress &&
            (logicalSlot > scheduleProgress || !logicalAttempts.TryGetValue(logicalSlot, out AttemptEntry? firstAttempt) ||
             !LedgerValidator.SameLogicalAttempt(firstAttempt, entry)))
            throw new InvalidDataException("invalid_retry_or_skipped_schedule_slot");
        return logicalSlot;
    }

    private void Commit(AttemptEntry entry, long logicalSlot)
    {
        if (logicalSlot == scheduleProgress) logicalAttempts.Add(logicalSlot, entry);
        attemptIds.Add(entry.AttemptId);
        ordinals.Add(entry.Ordinal);
        entries.Add(entry);
        if (logicalSlot == scheduleProgress) scheduleProgress = checked(scheduleProgress + 1);
    }
}
