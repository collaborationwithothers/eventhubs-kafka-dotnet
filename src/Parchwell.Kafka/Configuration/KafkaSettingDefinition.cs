using System.Collections.Immutable;

namespace Parchwell.Kafka.Configuration;

internal sealed class KafkaSettingDefinition
{
    private KafkaSettingDefinition(string key, string? value, KafkaRequiredInput? requiredInput,
        KafkaSettingDisposition disposition, ImmutableArray<string> inventoryIds,
        ImmutableArray<KafkaSettingSource> sources, string selectionReason)
    {
        KafkaSettingMetadata.ValidateAuditFields(key, inventoryIds, sources, selectionReason);
        Key = key;
        Value = value;
        RequiredInput = requiredInput;
        Disposition = disposition;
        InventoryIds = inventoryIds;
        Sources = sources;
        SelectionReason = selectionReason;
    }

    internal string Key { get; }
    internal string? Value { get; }
    internal KafkaRequiredInput? RequiredInput { get; }
    internal KafkaSettingDisposition Disposition { get; }
    internal ImmutableArray<string> InventoryIds { get; }
    internal ImmutableArray<KafkaSettingSource> Sources { get; }
    internal string SelectionReason { get; }

    internal static KafkaSettingDefinition Explicit(string key, string value, ImmutableArray<string> inventoryIds,
        ImmutableArray<KafkaSettingSource> sources, string selectionReason)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(key, value, null, KafkaSettingDisposition.Explicit, inventoryIds, sources, selectionReason);
    }

    internal static KafkaSettingDefinition FromInput(string key, KafkaRequiredInput input,
        ImmutableArray<string> inventoryIds, ImmutableArray<KafkaSettingSource> sources, string selectionReason)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new(key, null, input, KafkaSettingDisposition.Explicit, inventoryIds, sources, selectionReason);
    }

    internal static KafkaSettingDefinition Omitted(string key, KafkaSettingDisposition disposition,
        ImmutableArray<string> inventoryIds, ImmutableArray<KafkaSettingSource> sources, string selectionReason)
    {
        if (disposition is not (KafkaSettingDisposition.Excluded or KafkaSettingDisposition.Unspecified))
            throw new ArgumentOutOfRangeException(nameof(disposition), $"Setting '{key}' needs an omission disposition.");
        return new(key, null, null, disposition, inventoryIds, sources, selectionReason);
    }

    internal string ResolveValue(KafkaBaselineOptions options)
    {
        if (Disposition != KafkaSettingDisposition.Explicit)
            throw new InvalidOperationException($"Setting '{Key}' is omitted and has no emitted value.");
        return RequiredInput is { } input ? input.ResolveValue(options, Key) : Value!;
    }
}
