using System.Collections.Immutable;

namespace Parchwell.Kafka.Statistics;

internal sealed record KafkaNativeMetricIdentity(string Name, string ClientId, string Type, long Ts, long Time, long Age);

internal sealed record KafkaNativeMetricPoint(
    KafkaNativeMetricDefinition Definition, long Value, string? Broker, string? Topic,
    int? Partition, string? Request, string? State, string? BrokerAddress);

internal sealed record KafkaNativeMetricDiagnostic(string Code, string? Field);

internal sealed record KafkaNativeMetricProjection(
    bool IsValid, KafkaNativeMetricIdentity? Identity, ImmutableArray<KafkaNativeMetricPoint> Points,
    ImmutableArray<KafkaNativeMetricDiagnostic> Diagnostics);
