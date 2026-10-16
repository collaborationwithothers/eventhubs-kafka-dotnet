namespace Parchwell.LoadGen.Run.Contracts;

/// <summary>One terminal observation; application timing is distinct from broker request timing.</summary>
public sealed record LedgerRow(int SchemaVersion, RunIdentity Identity, AttemptEntry Attempt,
    string CompletionSource, bool Delivered, int ErrorCode, string? ErrorName,
    int? PersistenceStatus, string PersistenceProvenance, long CompletedMicroseconds,
    double? DeliveryLatencyMs, double? SynchronousCallDurationMs);
