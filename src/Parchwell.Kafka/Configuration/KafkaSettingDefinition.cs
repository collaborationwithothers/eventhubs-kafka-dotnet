using System.Collections.Immutable;

namespace Parchwell.Kafka.Configuration;

internal enum KafkaSettingValueKind
{
    Literal,
    NamespaceEndpoint,
    ClientId,
    EnrichmentGroupId
}

internal sealed record KafkaSettingDefinition(string Key, string? Value, KafkaSettingValueKind ValueKind,
    KafkaSettingDisposition Disposition, ImmutableArray<string> InventoryIds,
    ImmutableArray<KafkaSettingSource> Sources, string SelectionReason);
