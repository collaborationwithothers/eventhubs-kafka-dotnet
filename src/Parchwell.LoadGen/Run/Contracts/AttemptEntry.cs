namespace Parchwell.LoadGen.Run.Contracts;

/// <summary>An actual invocation registered before Produce, independently of its delivery callback.</summary>
public sealed record AttemptEntry(string InstanceId, string AttemptId, long Ordinal, string TenantId,
    string DocumentId, string Key, long Sequence, string EventType, long PlannedMicroseconds,
    long InvokedMicroseconds)
{
    /// <summary>Run-relative pre-call sample assigned when the reserved invocation is admitted.</summary>
    public long InvokedMicroseconds { get; private set; } = InvokedMicroseconds;

    internal void SetInvokedMicroseconds(long value) => InvokedMicroseconds = value;
}
