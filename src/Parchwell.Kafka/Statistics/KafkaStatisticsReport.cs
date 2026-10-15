using System.Collections.Immutable;

namespace Parchwell.Kafka.Statistics;

/// <summary>Retains a received native report, its attribution, parsed snapshot, and diagnostics.</summary>
/// <remarks>The raw JSON is evidence and remains available when parsing did not produce a snapshot.</remarks>
/// <param name="Identity">The scenario, run, role, and configured client identity.</param>
/// <param name="SessionId">The statistics capture session identifier.</param>
/// <param name="ReceivedAt">The time the report was received by the managed callback.</param>
/// <param name="RawJson">The original unmodified native JSON report.</param>
/// <param name="Snapshot">The parsed fields, or null when JSON or selected-field validation failed; runtime attribution errors remain in diagnostics.</param>
/// <param name="Diagnostics">The parse and evidence-quality diagnostics for this report.</param>
public sealed record KafkaStatisticsReport(
    KafkaStatisticsIdentity Identity,
    string SessionId,
    DateTimeOffset ReceivedAt,
    string RawJson,
    KafkaStatisticsSnapshot? Snapshot,
    ImmutableArray<KafkaStatisticsDiagnostic> Diagnostics);
