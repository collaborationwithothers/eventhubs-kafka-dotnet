using System.Collections.Immutable;
using System.Globalization;
using Confluent.Kafka;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios;

/// <summary>Composes scenario replacements over fresh, source-cited Kafka baselines.</summary>
public sealed class ScenarioResolver
{
    private const string SupportedBaseline = "Parchwell.Kafka";

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
        if (definition is null)
            throw new ArgumentException("The loaded scenario has no definition.", nameof(scenario));

        ValidateResolutionInputs(definition);

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

                if (configuration.Value.Overrides.TryGetValue(role, out var overrides))
                {
                    if (overrides is null)
                        throw Invalid(definition, $"configurations.{configuration.Key}.{role}", "null-overrides",
                            "The role override map cannot be null.");

                    foreach (var item in overrides.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    {
                        var fieldPath = $"configurations.{configuration.Key}.{role}.{item.Key}";
                        if (item.Value is null)
                            throw Invalid(definition, fieldPath, "null-override", "A setting override cannot be null.");

                        var bounds = GetSupportedBounds(role, item.Key, definition, fieldPath, item.Value.Location);
                        var value = item.Value.Value;
                        if (value < bounds.Minimum || value > bounds.Maximum)
                        {
                            throw Invalid(definition, fieldPath, "setting-out-of-range",
                                $"{item.Key} must be between {bounds.Minimum.ToString(CultureInfo.InvariantCulture)} and {bounds.Maximum.ToString(CultureInfo.InvariantCulture)} milliseconds.",
                                item.Value.Location);
                        }

                        if (!effective.TryGetValue(item.Key, out var inherited))
                        {
                            var isOmission = omissions.Any(omission => string.Equals(omission.Key, item.Key, StringComparison.Ordinal));
                            throw Invalid(definition, fieldPath,
                                isOmission ? "cannot-activate-omitted-setting" : "setting-not-in-baseline",
                                isOmission
                                    ? "A baseline omission cannot be activated by a scenario override."
                                    : "The setting is not an emitted baseline key and cannot be introduced.",
                                item.Value.Location);
                        }

                        if (!int.TryParse(inherited.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var baselineValue))
                        {
                            throw Invalid(definition, fieldPath, "invalid-baseline-value",
                                "The baseline value for this numeric setting is not a plain decimal integer.",
                                item.Value.Location);
                        }

                        if (value == baselineValue)
                        {
                            throw Invalid(definition, fieldPath, "no-op-override",
                                "An override must change the numeric baseline value.", item.Value.Location);
                        }

                        effective = effective.SetItem(item.Key, inherited with
                        {
                            Value = value.ToString(CultureInfo.InvariantCulture),
                            Override = new OverrideOrigin(definition.Id, configuration.Key,
                                item.Value.Location)
                        });
                    }
                }

                roleConfigurations.Add(role, new ResolvedRoleConfiguration(role, effective, omissions));
            }

            foreach (var overrideRole in configuration.Value.Overrides.Keys)
            {
                if (!definition.Roles.Contains(overrideRole))
                {
                    throw Invalid(definition, $"configurations.{configuration.Key}.{overrideRole}",
                        "role-not-declared", "Overrides may target only roles declared by the scenario.");
                }
            }

            resolvedConfigurations.Add(configuration.Key,
                new ResolvedConfiguration(configuration.Key, roleConfigurations.ToImmutable()));
        }

        return new ResolvedScenario(scenario, resolvedConfigurations.ToImmutable());
    }

    private static void ValidateResolutionInputs(ScenarioDefinition definition)
    {
        if (!string.Equals(definition.Baseline, SupportedBaseline, StringComparison.Ordinal))
            throw Invalid(definition, "baseline", "unsupported-baseline", $"Only {SupportedBaseline} is supported.");

        if (definition.Roles.IsDefaultOrEmpty)
            throw Invalid(definition, "roles", "roles-required", "At least one workload role is required.");

        if (definition.Roles.Distinct().Count() != definition.Roles.Length ||
            definition.Roles.Any(role => !Enum.IsDefined(role)))
        {
            throw Invalid(definition, "roles", "invalid-roles", "Roles must be unique declared KafkaClientRole values.");
        }

        if (definition.Configurations is null || definition.Configurations.Count == 0)
            throw Invalid(definition, "configurations", "configurations-required", "At least one configuration is required.");

        foreach (var configuration in definition.Configurations)
        {
            if (string.IsNullOrWhiteSpace(configuration.Key) || configuration.Value is null)
                throw Invalid(definition, "configurations", "invalid-configuration", "Configuration IDs and values must be present.");

            if (configuration.Value.Id != configuration.Key)
                throw Invalid(definition, $"configurations.{configuration.Key}", "configuration-id-mismatch",
                    "The configuration ID must match its mapping key.");

            if (configuration.Value.Overrides is null)
                throw Invalid(definition, $"configurations.{configuration.Key}", "null-overrides",
                    "The configuration override map cannot be null.");
        }
    }

    private static (ImmutableDictionary<string, ResolvedSetting> Settings,
        ImmutableArray<KafkaSettingMetadata> Omissions) SnapshotBaseline<TConfig>(KafkaBaseline<TConfig> baseline,
        KafkaClientRole role, ScenarioDefinition definition) where TConfig : ClientConfig
    {
        if (baseline.Configuration is null || baseline.Settings.IsDefault || baseline.Omissions.IsDefault)
            throw Invalid(definition, "baseline", "invalid-baseline-snapshot",
                $"The Kafka factory returned an incomplete baseline for {role}.");

        var nativeSettings = baseline.Configuration.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var metadataSettings = ImmutableDictionary.CreateBuilder<string, KafkaSettingMetadata>(StringComparer.Ordinal);
        foreach (var metadata in baseline.Settings)
        {
            if (metadata is null || metadata.Role != role || metadata.Disposition != KafkaSettingDisposition.Explicit ||
                metadata.Value is null || !metadataSettings.TryAdd(metadata.Key, metadata))
            {
                throw Invalid(definition, "baseline", "invalid-baseline-metadata",
                    $"The Kafka factory returned invalid setting metadata for {role}.");
            }
        }

        if (nativeSettings.Count != metadataSettings.Count ||
            nativeSettings.Keys.Any(key => !metadataSettings.ContainsKey(key)))
        {
            throw Invalid(definition, "baseline", "baseline-metadata-coverage",
                $"The native baseline and setting metadata keys do not match for {role}.");
        }

        var effective = ImmutableDictionary.CreateBuilder<string, ResolvedSetting>(StringComparer.Ordinal);
        foreach (var pair in nativeSettings)
        {
            var metadata = metadataSettings[pair.Key];
            if (!string.Equals(pair.Value, metadata.Value, StringComparison.Ordinal))
            {
                throw Invalid(definition, "baseline", "baseline-metadata-value-mismatch",
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
                throw Invalid(definition, "baseline", "invalid-baseline-omission",
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

    private static (int Minimum, int Maximum) GetSupportedBounds(KafkaClientRole role, string key,
        ScenarioDefinition definition, string fieldPath, SourceLocation location)
    {
        if (role != KafkaClientRole.LoadGenProducer)
        {
            throw Invalid(definition, fieldPath, "wrong-role-setting",
                "Scenario overrides are currently supported only for LoadGenProducer.", location);
        }

        return key switch
        {
            "request.timeout.ms" => (1, 900_000),
            "socket.timeout.ms" => (10, 300_000),
            _ => throw Invalid(definition, fieldPath, "unsupported-setting",
                "Only request.timeout.ms and socket.timeout.ms are currently supported overrides.", location)
        };
    }

    private static ScenarioValidationException Invalid(ScenarioDefinition definition, string fieldPath,
        string code, string message, SourceLocation? location = null)
    {
        var source = location?.Path ?? definition.Source.Path;
        return new ScenarioValidationException([new ScenarioDiagnostic(source, fieldPath, code, message,
            location?.Line ?? definition.Source.Line, location?.Column ?? definition.Source.Column)]);
    }
}
