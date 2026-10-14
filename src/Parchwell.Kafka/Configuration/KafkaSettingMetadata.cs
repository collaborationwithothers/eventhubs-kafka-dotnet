using System.Collections.Immutable;

namespace Parchwell.Kafka.Configuration;

/// <summary>An immutable audit record for one emitted setting or deliberate omission.</summary>
public sealed class KafkaSettingMetadata
{
    internal KafkaSettingMetadata(string key, string? value, KafkaClientRole role,
        KafkaSettingDisposition disposition, ImmutableArray<string> inventoryIds,
        ImmutableArray<KafkaSettingSource> sources, string selectionReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectionReason);
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        if (!Enum.IsDefined(disposition))
            throw new ArgumentOutOfRangeException(nameof(disposition));
        if ((disposition == KafkaSettingDisposition.Explicit) != (value is not null))
            throw new ArgumentException("Only an explicit setting must have a value.", nameof(value));
        if (inventoryIds.IsDefaultOrEmpty || inventoryIds.Any(id => !FrozenInventory.Contains(id)))
            throw new ArgumentException("Every setting needs IDs from the frozen inventory.", nameof(inventoryIds));
        if (sources.IsDefaultOrEmpty || sources.Any(source => source is null))
            throw new ArgumentException("Every setting needs cited sources.", nameof(sources));
        Key = key;
        Value = value;
        Role = role;
        Disposition = disposition;
        InventoryIds = inventoryIds;
        Sources = sources;
        SelectionReason = selectionReason;
    }

    /// <summary>Gets the exact configuration key.</summary>
    public string Key { get; }
    /// <summary>Gets the initially emitted string, or null for an omission.</summary>
    public string? Value { get; }
    /// <summary>Gets the workload profile.</summary>
    public KafkaClientRole Role { get; }
    /// <summary>Gets whether this setting is emitted, excluded or unspecified.</summary>
    public KafkaSettingDisposition Disposition { get; }
    /// <summary>Gets validated frozen-inventory IDs.</summary>
    public ImmutableArray<string> InventoryIds { get; }
    /// <summary>Gets the cited source versions or dates.</summary>
    public ImmutableArray<KafkaSettingSource> Sources { get; }
    /// <summary>Gets the reason this value or omission was selected.</summary>
    public string SelectionReason { get; }
}
