using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Confluent.Kafka;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios;

/// <summary>Composes scenario replacements over fresh, source-cited Kafka baselines.</summary>
/// <remarks>
/// Resolution applies each configuration's overrides to a fresh baseline for every declared role and
/// records where each replacement came from. Which keys may be overridden, for which roles and within
/// which ranges, is set by <see cref="KafkaScenarioOverridePolicy"/>, which cites its sources.
/// </remarks>
public sealed class ScenarioResolver
{
    private readonly KafkaConfigurationFactory factory;

    /// <summary>Creates a resolver using the application's registered Kafka baseline factory.</summary>
    /// <param name="factory">The factory registered for the scenario's declared roles.</param>
    public ScenarioResolver(KafkaConfigurationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        this.factory = factory;
    }

    /// <summary>Resolves all named configurations into immutable effective settings and provenance.</summary>
    /// <param name="scenario">A successfully loaded scenario and its profile.</param>
    /// <returns>The immutable resolved scenario.</returns>
    /// <exception cref="ScenarioValidationException">The loaded declaration contains an unsupported replacement.</exception>
    /// <exception cref="InvalidOperationException">A declared Kafka role was not registered with the factory.</exception>
    /// <exception cref="Microsoft.Extensions.Options.OptionsValidationException">Registered Kafka inputs are invalid.</exception>
    public ResolvedScenario Resolve(LoadedScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var definition = scenario.Definition;
        // Capture one fresh baseline snapshot per role for this resolution. OptionsMonitor may
        // change later; one run plan must not mix values from different option snapshots.
        var baselineSnapshots = new Dictionary<KafkaClientRole,
            (ImmutableDictionary<string, ResolvedSetting> Settings, ImmutableArray<KafkaSettingMetadata> Omissions)>();
        foreach (var role in definition.Roles)
            baselineSnapshots.Add(role, CreateBaselineSnapshot(role, definition));

        var resolvedConfigurations = ImmutableDictionary.CreateBuilder<string, ResolvedConfiguration>(StringComparer.Ordinal);
        foreach (var configuration in definition.Configurations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var roleConfigurations = ImmutableDictionary.CreateBuilder<KafkaClientRole, ResolvedRoleConfiguration>();
            foreach (var role in definition.Roles)
            {
                var (settings, omissions) = baselineSnapshots[role];
                var effective = settings;
                var scenarioOmissions = ImmutableArray.CreateBuilder<ResolvedOmission>();

                if (configuration.Value.Overrides.TryGetValue(role, out var overrides))
                {
                    foreach (var item in overrides.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    {
                        var fieldPath = $"configurations.{configuration.Key}.{role}.{item.Key}";
                        var setting = GetSupportedSetting(role, item.Key, definition, fieldPath, item.Value.Location);
                        var value = item.Value.Value;
                        var origin = new OverrideOrigin(definition.Id, configuration.Key, item.Value.Location);
                        var normalized = value;
                        // An integer entry may also let named items omit its key (connections.max.idle.ms for A04).
                        var omits = setting.ValueKind == KafkaScenarioOverrideValueKind.Omission ||
                            (value == "omit" && setting.OmissionInventoryIds.Contains(definition.Id, StringComparer.Ordinal));
                        switch (omits ? KafkaScenarioOverrideValueKind.Omission : setting.ValueKind)
                        {
                            case KafkaScenarioOverrideValueKind.Boolean:
                                if (value is not ("true" or "false")) OutOfRange();
                                break;
                            case KafkaScenarioOverrideValueKind.Integer:
                                if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) ||
                                    parsed < setting.Minimum || parsed > setting.Maximum) OutOfRange();
                                normalized = parsed.ToString(CultureInfo.InvariantCulture);
                                break;
                            case KafkaScenarioOverrideValueKind.String:
                                if (value.Length == 0 || value.IndexOfAny(['\0', '\r', '\n']) >= 0 || Encoding.UTF8.GetByteCount(value) > 65536 ||
                                    (!setting.AllowAnyNonemptyValue && !setting.AllowedValues.Contains(value, StringComparer.Ordinal))) OutOfRange();
                                break;
                            case KafkaScenarioOverrideValueKind.Omission:
                                if (value != "omit") OutOfRange();
                                break;
                            default:
                                throw new InvalidOperationException("Unknown override value kind.");
                        }

                        if (!effective.TryGetValue(item.Key, out var inherited))
                        {
                            var omission = omissions.FirstOrDefault(omission => string.Equals(omission.Key, item.Key, StringComparison.Ordinal));
                            if (omission is not null && setting.AllowActivation)
                            {
                                effective = effective.Add(item.Key, new ResolvedSetting(normalized, omission, origin));
                                omissions = [.. omissions.Where(entry => !string.Equals(entry.Key, item.Key, StringComparison.Ordinal))];
                                continue;
                            }
                            throw Invalid(definition, fieldPath, omission is not null ? "cannot_activate_omitted_setting" : "setting_not_in_baseline",
                                "The setting is not an emitted baseline key and cannot be introduced.", item.Value.Location);
                        }
                        if (omits)
                        {
                            effective = effective.Remove(item.Key);
                            scenarioOmissions.Add(new(item.Key, inherited.Baseline, origin));
                            continue;
                        }
                        if (setting.ValueKind == KafkaScenarioOverrideValueKind.Boolean && inherited.Value is not ("true" or "false"))
                            throw Invalid(definition, fieldPath, "invalid_baseline_value", "The boolean baseline is invalid.", item.Value.Location);
                        if (setting.ValueKind == KafkaScenarioOverrideValueKind.Integer &&
                            !int.TryParse(inherited.Value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                            throw Invalid(definition, fieldPath, "invalid_baseline_value", "The numeric baseline is invalid.", item.Value.Location);
                        if (string.Equals(normalized, inherited.Value, StringComparison.Ordinal))
                            throw Invalid(definition, fieldPath, "no_op_override", "An override must change the baseline value.", item.Value.Location);
                        effective = effective.SetItem(item.Key, inherited with { Value = normalized, Override = origin });

                        void OutOfRange() => throw Invalid(definition, fieldPath, "setting_out_of_range",
                            $"{item.Key} is outside its supported value policy.", item.Value.Location);
                    }
                }

                roleConfigurations.Add(role, new ResolvedRoleConfiguration(role, effective, omissions, scenarioOmissions.ToImmutable()));
            }

            resolvedConfigurations.Add(configuration.Key,
                new ResolvedConfiguration(configuration.Key, roleConfigurations.ToImmutable()));
        }

        return new ResolvedScenario(scenario, resolvedConfigurations.ToImmutable());
    }

    private static (ImmutableDictionary<string, ResolvedSetting> Settings,
        ImmutableArray<KafkaSettingMetadata> Omissions) SnapshotBaseline<TConfig>(KafkaBaseline<TConfig> baseline,
        KafkaClientRole role, ScenarioDefinition definition) where TConfig : ClientConfig
    {
        // Check that the factory's native configuration and its metadata agree, so the provenance in the run
        // plan describes exactly the settings that are emitted.
        if (baseline.Configuration is null || baseline.Settings.IsDefault || baseline.Omissions.IsDefault)
            throw Invalid(definition, "baseline", "invalid_baseline_snapshot",
                $"The Kafka factory returned an incomplete baseline for {role}.");

        var nativeSettings = baseline.Configuration.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var metadataSettings = ImmutableDictionary.CreateBuilder<string, KafkaSettingMetadata>(StringComparer.Ordinal);
        foreach (var metadata in baseline.Settings)
        {
            if (metadata is null || metadata.Role != role || metadata.Disposition != KafkaSettingDisposition.Explicit ||
                metadata.Value is null || !metadataSettings.TryAdd(metadata.Key, metadata))
            {
                throw Invalid(definition, "baseline", "invalid_baseline_metadata",
                    $"The Kafka factory returned invalid setting metadata for {role}.");
            }
        }

        if (nativeSettings.Count != metadataSettings.Count ||
            nativeSettings.Keys.Any(key => !metadataSettings.ContainsKey(key)))
        {
            throw Invalid(definition, "baseline", "baseline_metadata_coverage",
                $"The native baseline and setting metadata keys do not match for {role}.");
        }

        var effective = ImmutableDictionary.CreateBuilder<string, ResolvedSetting>(StringComparer.Ordinal);
        foreach (var pair in nativeSettings)
        {
            var metadata = metadataSettings[pair.Key];
            if (!string.Equals(pair.Value, metadata.Value, StringComparison.Ordinal))
            {
                throw Invalid(definition, "baseline", "baseline_metadata_value_mismatch",
                    $"The native baseline and metadata values do not match for {role}.{pair.Key}.");
            }
            effective.Add(pair.Key, new ResolvedSetting(pair.Value, metadata, null));
        }

        var omissionKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var omission in baseline.Omissions)
        {
            if (omission is null || omission.Role != role || omission.Disposition == KafkaSettingDisposition.Explicit ||
                omission.Value is not null || nativeSettings.ContainsKey(omission.Key) || !omissionKeys.Add(omission.Key))
            {
                throw Invalid(definition, "baseline", "invalid_baseline_omission",
                    $"The Kafka factory returned invalid omission metadata for {role}.");
            }
        }

        return (effective.ToImmutable(), baseline.Omissions);
    }

    private (ImmutableDictionary<string, ResolvedSetting> Settings,
        ImmutableArray<KafkaSettingMetadata> Omissions) CreateBaselineSnapshot(KafkaClientRole role,
        ScenarioDefinition definition) => role switch
    {
        KafkaClientRole.LoadGenProducer => SnapshotBaseline(factory.CreateLoadGenProducer(), role, definition),
        KafkaClientRole.EnrichmentProducer => SnapshotBaseline(factory.CreateEnrichmentProducer(), role, definition),
        KafkaClientRole.EnrichmentConsumer => SnapshotBaseline(factory.CreateEnrichmentConsumer(), role, definition),
        KafkaClientRole.VerifierConsumer => SnapshotBaseline(factory.CreateVerifierConsumer(), role, definition),
        _ => throw new InvalidOperationException($"Kafka role {role} is not supported.")
    };

    private static KafkaScenarioOverrideSetting GetSupportedSetting(KafkaClientRole role, string key,
        ScenarioDefinition definition, string fieldPath, SourceLocation location)
    {
        if (!KafkaScenarioOverridePolicy.SupportsRole(role))
        {
            throw Invalid(definition, fieldPath, "wrong_role_setting",
                $"Scenario overrides are not supported for {role}.{key}.", location);
        }

        if (!KafkaScenarioOverridePolicy.TryGetSetting(key, out var setting) || !setting.SupportsRole(role))
        {
            throw Invalid(definition, fieldPath, "unsupported_setting",
                $"{role}.{key} is not supported by the scenario override policy.", location);
        }

        if ((setting.ValueKind is KafkaScenarioOverrideValueKind.String or KafkaScenarioOverrideValueKind.Omission || setting.AllowActivation) &&
            !setting.InventoryIds.Contains(definition.Id, StringComparer.Ordinal))
            throw Invalid(definition, fieldPath, "unsupported_setting", "The setting is not supported for this inventory item.", location);
        return setting;
    }

    private static ScenarioValidationException Invalid(ScenarioDefinition definition, string fieldPath,
        string code, string message, SourceLocation? location = null)
    {
        var source = location?.Path ?? definition.Source.Path;
        return new ScenarioValidationException([new ScenarioDiagnostic(source, fieldPath, code, message,
            location?.Line ?? definition.Source.Line, location?.Column ?? definition.Source.Column)]);
    }
}
