namespace Parchwell.LoadGen.Run.Contracts;

/// <summary>An actual invocation registered before Produce, independently of its delivery callback.</summary>
public sealed record AttemptEntry(string InstanceId, string AttemptId, long Ordinal, string TenantId,
    string DocumentId, string Key, long Sequence, string EventType, long PlannedMicroseconds,
    long InvokedMicroseconds);
