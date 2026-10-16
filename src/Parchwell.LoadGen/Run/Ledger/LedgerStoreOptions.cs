namespace Parchwell.LoadGen.Run.Ledger;

/// <summary>Explicit local file and bounded-buffer limits for one run.</summary>
public sealed record LedgerStoreOptions(string LedgerPath, string ManifestPath, int QueueCapacity,
    int MaximumRowBytes, long MaximumLedgerBytes, int MaximumManifestBytes, TimeSpan DrainTimeout)
{
    /// <summary>Validates the storage paths and finite positive limits before creating the store.</summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(LedgerPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ManifestPath);
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(Path.GetFullPath(LedgerPath), Path.GetFullPath(ManifestPath), pathComparison))
            throw new ArgumentException("ledger_and_manifest_paths_must_differ");
        if (QueueCapacity <= 0 || MaximumRowBytes <= 0 || MaximumLedgerBytes <= 0 || MaximumManifestBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity), "ledger_limits_must_be_positive_and_finite");
        if (MaximumLedgerBytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(MaximumLedgerBytes), "ledger_readback_limit_exceeds_supported_memory_bound");
        if (DrainTimeout <= TimeSpan.Zero || DrainTimeout.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(DrainTimeout), "drain_timeout_must_be_positive_and_finite");
    }
}
