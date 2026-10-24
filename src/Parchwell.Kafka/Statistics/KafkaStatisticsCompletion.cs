using System.Collections.Immutable;

namespace Parchwell.Kafka.Statistics;

/// <summary>Reports whether a statistics session met its selected post-drain capture contract and evidence drain.</summary>
/// <remarks>
/// Completeness describes only this client's bounded statistics capture. The harness still owns
/// broker lifecycle inventory and the full ADR-013 late-write eligibility decision.
/// Capture-only completion proves a successfully written nonempty fresh report, not independent
/// broker coverage. The compatibility overload additionally checks the caller's expected broker set.
/// </remarks>
/// <param name="IsComplete">Whether this session met its clock, coverage, diagnostic and drain requirements.</param>
/// <param name="Diagnostics">The distinct bounded reasons the session is incomplete.</param>
/// <param name="MissingBrokers">Expected broker names without a qualifying post-drain sample; empty in capture-only mode, which accepts no inventory.</param>
public sealed record KafkaStatisticsCompletion(
    bool IsComplete,
    ImmutableArray<KafkaStatisticsDiagnostic> Diagnostics,
    ImmutableArray<string> MissingBrokers)
{
    /// <summary>Gets the native-clock boundary captured when statistics completion began, in microseconds.</summary>
    public long? DrainBoundaryMicroseconds { get; init; }

    /// <summary>Gets the first successfully written qualifying snapshot's native timestamp in capture-only mode, or null otherwise.</summary>
    /// <remarks>This observation alone does not establish complete independent broker coverage or durable storage beyond the sink contract.</remarks>
    public long? FirstPostDrainTimestampMicroseconds { get; init; }
}
