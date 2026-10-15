using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios;

internal sealed record MeasurementDescriptor(string Source, string ValueKind, string Unit, string Scope,
    ImmutableArray<KafkaClientRole> Roles, string SourceUrl, string VersionOrDate);

internal static class MeasurementCatalog
{
    // These are declaration identities, not implemented collectors or OpenTelemetry names.
    private const string Statistics = "https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md";
    private const string Blueprint = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/9ec3d69779a36aa0cc258fc449e37e769c825336/docs/blueprint.md";
    private const string A01 = "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/14#issuecomment-6151738621";
    private static readonly ImmutableArray<KafkaClientRole> AllRoles = [.. Enum.GetValues<KafkaClientRole>()];
    private static readonly ImmutableDictionary<string, MeasurementDescriptor> Entries = Create();

    internal static bool TryGet(string id, [NotNullWhen(true)] out MeasurementDescriptor? descriptor) => Entries.TryGetValue(id, out descriptor);

    private static ImmutableDictionary<string, MeasurementDescriptor> Create()
    {
        var entries = ImmutableDictionary.CreateBuilder<string, MeasurementDescriptor>(StringComparer.Ordinal);
        entries.Add("ledger.delivery_error_code", new("ledger", "error_code", "none", "attempt",
            [KafkaClientRole.LoadGenProducer], A01, "revision 2; 2026-10-13; collector pending"));
        entries.Add("ledger.delivery_latency_ms", new("ledger", "duration", "ms", "attempt",
            [KafkaClientRole.LoadGenProducer], A01, "revision 2; 2026-10-13; collector pending"));
        foreach (var count in new[] { "ok", "loss", "phantom", "reported_failure", "duplicate", "reorder" })
            entries.Add("verifier." + count, new("verifier", "count", "count", "complete_valid_run",
                [KafkaClientRole.VerifierConsumer], Blueprint, "ADR-012/013; inspected 9ec3d697"));
        entries.Add("stats.broker.throttle.max", new("statistics", "duration", "ms", "broker_window", AllRoles, Statistics, "v2.16.0"));
        entries.Add("stats.broker.req_timeouts", new("statistics", "count", "count", "broker_cumulative", AllRoles, Statistics, "v2.16.0"));
        entries.Add("stats.broker.disconnects", new("statistics", "count", "count", "broker_cumulative", AllRoles, Statistics, "v2.16.0"));
        entries.Add("stats.broker.rtt.p99", new("statistics", "duration", "us", "broker_window", AllRoles, Statistics, "v2.16.0"));
        entries.Add("producer.request_timeout", new("request_evidence", "event", "none", "request",
            [KafkaClientRole.LoadGenProducer, KafkaClientRole.EnrichmentProducer],
            "https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L1043-L1049", "v2.16.0; collector pending"));
        return entries.ToImmutable();
    }
}
