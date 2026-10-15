using System.Collections.Immutable;

namespace Parchwell.Kafka.Statistics;

/// <summary>Reports whether a statistics session proved post-drain broker coverage and evidence drain.</summary>
/// <remarks>
/// Completeness describes only this client's bounded statistics capture. The harness still owns
/// broker lifecycle inventory and the full ADR-013 late-write eligibility decision.
/// </remarks>
/// <param name="IsComplete">Whether this session met its clock, coverage, diagnostic and drain requirements.</param>
/// <param name="Diagnostics">The distinct bounded reasons the session is incomplete.</param>
/// <param name="MissingBrokers">Expected broker names without a qualifying post-drain sample.</param>
public sealed record KafkaStatisticsCompletion(
    bool IsComplete,
    ImmutableArray<KafkaStatisticsDiagnostic> Diagnostics,
    ImmutableArray<string> MissingBrokers);
