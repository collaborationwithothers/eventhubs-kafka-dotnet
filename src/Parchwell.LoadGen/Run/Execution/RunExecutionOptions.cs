namespace Parchwell.LoadGen.Run.Execution;

/// <summary>Finite execution, scheduling, drain, output and read-back limits for one worker run.</summary>
public sealed record RunExecutionOptions(
    int MaxAttempts,
    int MaxPayloadBytes,
    TimeSpan MaxScheduleLateness,
    TimeSpan ProducerDrainTimeout,
    TimeSpan CleanupTimeout,
    int MaxRowBytes,
    int MaxManifestBytes,
    long MaxLedgerReadbackBytes,
    string OutputRoot,
    string Topic,
    IReadOnlyList<string> ExpectedBrokerNames,
    bool RequireRequestEvidence,
    bool RequireStatisticsEvidence = false,
    TimeSpan? LedgerDrainTimeout = null,
    TimeSpan? StatisticsCompletionTimeout = null,
    TimeSpan? EvidenceCompletionTimeout = null)
{
    /// <summary>Validates every finite budget and the output destination before a producer is constructed.</summary>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxAttempts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxPayloadBytes);
        ValidateFiniteNonNegative(MaxScheduleLateness, nameof(MaxScheduleLateness));
        ValidateFinitePositive(ProducerDrainTimeout, nameof(ProducerDrainTimeout));
        ValidateFinitePositive(CleanupTimeout, nameof(CleanupTimeout));
        ValidateFinitePositive(LedgerDrainTimeout ?? CleanupTimeout, nameof(LedgerDrainTimeout));
        ValidateFinitePositive(StatisticsCompletionTimeout ?? CleanupTimeout, nameof(StatisticsCompletionTimeout));
        ValidateFinitePositive(EvidenceCompletionTimeout ?? CleanupTimeout, nameof(EvidenceCompletionTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxRowBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxManifestBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxLedgerReadbackBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(OutputRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(Topic);
        ArgumentNullException.ThrowIfNull(ExpectedBrokerNames);
        if (ExpectedBrokerNames.Any(string.IsNullOrWhiteSpace) ||
            ExpectedBrokerNames.Distinct(StringComparer.Ordinal).Count() != ExpectedBrokerNames.Count)
            throw new ArgumentException("expected_broker_names_must_be_nonempty_and_unique", nameof(ExpectedBrokerNames));
    }

    private static void ValidateFinitePositive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan || value.TotalMilliseconds > uint.MaxValue - 1d)
            throw new ArgumentOutOfRangeException(name, "budget_must_be_positive_and_finite");
    }

    private static void ValidateFiniteNonNegative(TimeSpan value, string name)
    {
        if (value < TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(name, "budget_must_be_nonnegative_and_finite");
    }
}
