namespace Parchwell.LoadGen.Run.Workload;

/// <summary>A deterministic event offered by workload generator version 1.</summary>
public sealed record PlannedEvent(long Ordinal, string StageId, string TenantId, string DocumentId,
    string Key, int Sequence, string EventType, long DueMicroseconds, int PayloadSizeBytes);
