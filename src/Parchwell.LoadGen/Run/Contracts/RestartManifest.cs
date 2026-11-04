using System.Collections.Immutable;

namespace Parchwell.LoadGen.Run.Contracts;

/// <summary>The resend list of a restarted B10 instance, and the instance whose timed-out sends it resends (ADR-021).</summary>
public sealed record RestartManifest(string PredecessorInstanceId, ImmutableArray<RestartPlannedEntry> Planned);

/// <summary>One resend: the logical event of a predecessor attempt whose latest send timed out, with its original planned time.</summary>
public sealed record RestartPlannedEntry(long Ordinal, string PredecessorAttemptId, string TenantId, string DocumentId,
    string Key, long Sequence, string EventType, long PlannedMicroseconds);
