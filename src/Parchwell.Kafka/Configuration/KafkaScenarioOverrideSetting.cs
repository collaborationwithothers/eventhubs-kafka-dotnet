using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Parchwell.Kafka.Configuration;

/// <summary>Describes an override key admitted by the repository's scenario policy.</summary>
/// <remarks>
/// A scenario override is one baseline setting that a scenario definition changes for its run.
/// Overrides are how a scenario expresses its configurations as deltas from the baseline
/// (ADR-014, ADR-015). Only keys listed in <see cref="KafkaScenarioOverridePolicy"/> may be overridden.
/// The range is the native librdkafka range. <see cref="SupportedRoles"/> is a separate
/// repository policy and does not claim that librdkafka itself restricts the key by role.
/// </remarks>
public sealed class KafkaScenarioOverrideSetting
{
    internal KafkaScenarioOverrideSetting(string key, int minimum, int maximum,
        ImmutableArray<KafkaClientRole> supportedRoles, ImmutableArray<string> inventoryIds,
        ImmutableArray<KafkaSettingSource> sources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimum, maximum);
        if (supportedRoles.IsDefaultOrEmpty || supportedRoles.Any(role => !Enum.IsDefined(role)))
            throw new ArgumentException("At least one defined client role is required.", nameof(supportedRoles));
        if (inventoryIds.IsDefaultOrEmpty || inventoryIds.Any(id => !FrozenInventory.Contains(id)))
            throw new ArgumentException("Every override needs IDs from the frozen inventory.", nameof(inventoryIds));
        if (sources.IsDefaultOrEmpty || sources.Any(source => source is null))
            throw new ArgumentException("Every override needs cited sources.", nameof(sources));

        Key = key;
        Minimum = minimum;
        Maximum = maximum;
        SupportedRoles = supportedRoles;
        InventoryIds = inventoryIds;
        Sources = sources;
    }

    /// <summary>Gets the exact, case-sensitive Kafka configuration key.</summary>
    public string Key { get; }

    /// <summary>Gets the native librdkafka minimum accepted value for this setting.</summary>
    public int Minimum { get; }

    /// <summary>Gets the native librdkafka maximum accepted value for this setting.</summary>
    public int Maximum { get; }

    /// <summary>
    /// Gets the workload roles for which this repository permits scenario overrides.
    /// This is repository policy, independent of the native setting's bounds.
    /// </summary>
    public ImmutableArray<KafkaClientRole> SupportedRoles { get; }

    /// <summary>Gets the frozen-inventory IDs supporting this override.</summary>
    public ImmutableArray<string> InventoryIds { get; }

    /// <summary>Gets the cited sources for this setting's native bounds.</summary>
    public ImmutableArray<KafkaSettingSource> Sources { get; }

    /// <summary>Checks whether repository policy permits this override for a workload role.</summary>
    /// <param name="role">The workload role to check.</param>
    /// <returns><see langword="true"/> when the role is listed in <see cref="SupportedRoles"/>.</returns>
    public bool SupportsRole(KafkaClientRole role) => SupportedRoles.Contains(role);
}

/// <summary>Exposes the keys, native bounds and role policy supported for scenario overrides.</summary>
/// <remarks>
/// These bounds are verified against librdkafka v2.16.0. At present, this repository separately
/// permits overrides only for <see cref="KafkaClientRole.LoadGenProducer"/>.
/// Each entry must name the frozen inventory items it serves.
/// </remarks>
public static class KafkaScenarioOverridePolicy
{
    private static readonly KafkaSettingSource NativeConfiguration = new(
        new Uri("https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md"), "v2.16.0");

    private static readonly ImmutableArray<KafkaScenarioOverrideSetting> Entries =
    [
        new("request.timeout.ms", 1, 900_000, [KafkaClientRole.LoadGenProducer], ["A01"], [NativeConfiguration]),
        new("socket.timeout.ms", 10, 300_000, [KafkaClientRole.LoadGenProducer], ["A01"], [NativeConfiguration])
    ];

    /// <summary>Gets the immutable list of exact keys accepted by the override policy.</summary>
    public static ImmutableArray<KafkaScenarioOverrideSetting> Settings => Entries;

    /// <summary>Checks whether repository policy permits scenario overrides for a workload role.</summary>
    /// <param name="role">The workload role to check.</param>
    /// <returns><see langword="true"/> when at least one override key is permitted for the role.</returns>
    public static bool SupportsRole(KafkaClientRole role) => Entries.Any(entry => entry.SupportsRole(role));

    /// <summary>Finds the policy entry for an exact configuration key.</summary>
    /// <param name="key">The case-sensitive configuration key to find.</param>
    /// <param name="setting">The matching entry, or <see langword="null"/> when the key is unsupported.</param>
    /// <returns><see langword="true"/> when the key is recognized by the override policy.</returns>
    public static bool TryGetSetting(string? key,
        [NotNullWhen(true)] out KafkaScenarioOverrideSetting? setting)
    {
        setting = key is null
            ? null
            : Entries.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
        return setting is not null;
    }
}
