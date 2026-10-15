using System.Collections.Immutable;

namespace Parchwell.Kafka.Configuration;

/// <summary>An immutable audit record for one emitted setting or deliberate omission.</summary>
/// <remarks>
/// The record answers, for one role and one key: what value the baseline chose or why it chose
/// none, which frozen inventory items depend on it, and which sources support it.
/// ADR-015 requires every setting the harness depends on to be explicit and tagged; this record
/// is that tag, available at run time.
/// </remarks>
public sealed class KafkaSettingMetadata
{
    internal KafkaSettingMetadata(string key, string? value, KafkaClientRole role,
        KafkaSettingDisposition disposition, ImmutableArray<string> inventoryIds,
        ImmutableArray<KafkaSettingSource> sources, string selectionReason)
    {
        ValidateAuditFields(key, inventoryIds, sources, selectionReason);
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        if (!Enum.IsDefined(disposition))
            throw new ArgumentOutOfRangeException(nameof(disposition));
        // An omission has no value by definition. A value on an omission, or a missing value on an
        // explicit setting, would make the audit record disagree with the emitted configuration.
        if ((disposition == KafkaSettingDisposition.Explicit) != (value is not null))
            throw new ArgumentException("Only an explicit setting must have a value.", nameof(value));
        Key = key;
        Value = value;
        Role = role;
        Disposition = disposition;
        InventoryIds = inventoryIds;
        Sources = sources;
        SelectionReason = selectionReason;
    }

    // Shared with KafkaSettingDefinition, so a bad catalogue entry fails when the catalogue loads,
    // before any configuration is built.
    // An untagged or unsourced setting breaks ADR-015's rule that every setting is traceable.
    internal static void ValidateAuditFields(string key, ImmutableArray<string> inventoryIds,
        ImmutableArray<KafkaSettingSource> sources, string selectionReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectionReason);
        if (inventoryIds.IsDefaultOrEmpty || inventoryIds.Any(id => !FrozenInventory.Contains(id)))
            throw new ArgumentException("Every setting needs IDs from the frozen inventory.", nameof(inventoryIds));
        if (sources.IsDefaultOrEmpty || sources.Any(source => source is null))
            throw new ArgumentException("Every setting needs cited sources.", nameof(sources));
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
