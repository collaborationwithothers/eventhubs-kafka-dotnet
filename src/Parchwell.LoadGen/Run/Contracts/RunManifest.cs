using System.Collections.Immutable;

namespace Parchwell.LoadGen.Run.Contracts;

/// <summary>Sealed independent inventory and diagnostic provenance, never reconstructed from ledger rows.</summary>
/// <remarks><see cref="Restart"/> is set only for the restarted instance of a B10 repeat (ADR-021); every other manifest has none.</remarks>
public sealed record RunManifest(int SchemaVersion, RunIdentity Identity, string GeneratorVersion,
    long PlannedAttemptCount, long AttemptCount, ImmutableArray<AttemptEntry> Inventory,
    long TimestampFrequency, long StartTimestamp, string WorkerStatus, bool BookkeepingComplete,
    long? FirstUnexecutedOrdinal, ImmutableArray<string> Diagnostics,
    ImmutableDictionary<string, string> RuntimeVersions,
    ImmutableDictionary<string, string> EvidenceFiles,
    ImmutableArray<string> GuardObservations,
    RestartManifest? Restart = null);
