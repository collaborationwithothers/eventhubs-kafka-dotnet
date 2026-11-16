using System.Collections.Immutable;

namespace Parchwell.Kafka.Statistics;

/// <summary>Monitoring coverage separate from scientific capture completeness.</summary>
/// <param name="IsComplete">Whether monitoring has recorded no coverage gap in this lifetime.</param>
/// <param name="Diagnostics">Bounded monitoring diagnostics accumulated through this report.</param>
public sealed record KafkaStatisticsMonitoringStatus(
    bool IsComplete,
    ImmutableArray<KafkaStatisticsMonitoringDiagnostic> Diagnostics);

/// <summary>A bounded monitoring diagnostic with an optional static field path.</summary>
/// <param name="Code">The diagnostic code.</param>
/// <param name="Field">The static field path, if applicable.</param>
public sealed record KafkaStatisticsMonitoringDiagnostic(string Code, string? Field);
