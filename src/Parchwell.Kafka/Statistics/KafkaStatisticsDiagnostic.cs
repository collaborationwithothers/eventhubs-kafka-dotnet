namespace Parchwell.Kafka.Statistics;

/// <summary>Describes why a statistics report could not supply a valid snapshot or evidence.</summary>
/// <param name="Code">A stable machine-readable diagnostic code.</param>
/// <param name="Broker">The native broker name when the issue is broker-specific.</param>
/// <param name="Field">The native JSON field when the issue is field-specific.</param>
public sealed record KafkaStatisticsDiagnostic(string Code, string? Broker = null, string? Field = null);
