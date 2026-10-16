namespace Parchwell.LoadGen.Run.Evidence;

/// <summary>Finite limits for one run's local evidence files and capture queue.</summary>
public sealed record RunEvidenceOptions(
    string DirectoryPath,
    int QueueCapacity,
    int MaximumReportBytes,
    long MaximumFileBytes,
    int MaximumDiagnostics,
    TimeSpan CompletionTimeout)
{
    /// <summary>Validates limits before the evidence directory is opened.</summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DirectoryPath);
        if (QueueCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(QueueCapacity));
        if (MaximumReportBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumReportBytes));
        if (MaximumFileBytes <= 0 || MaximumFileBytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(MaximumFileBytes));
        if (MaximumDiagnostics <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumDiagnostics));
        if (CompletionTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(CompletionTimeout));
        if (CompletionTimeout.TotalMilliseconds > uint.MaxValue - 1d)
            throw new ArgumentOutOfRangeException(nameof(CompletionTimeout), "CompletionTimeout exceeds the supported cancellation timer range.");
    }
}
