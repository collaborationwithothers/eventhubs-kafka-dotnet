using System.Globalization;
using Parchwell.Kafka.Configuration;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Parchwell.Scenarios;

/// <summary>Emits deterministic Helm values for one resolved scenario configuration.</summary>
/// <remarks>
/// Helm values are generated, never written by hand, so a deployment always matches its reviewed
/// definition (ADR-014). The output is deterministic: the same input gives the same bytes, with no
/// timestamps or random ids. The format is fixed by docs/operations/scenario-contract.md,
/// "Composition and output"; no chart consumes it yet.
/// </remarks>
public sealed class HelmValuesGenerator
{
    /// <summary>Generates the version 1 values document for the selected configuration.</summary>
    /// <param name="scenario">The immutable resolved scenario.</param>
    /// <param name="configurationId">The configuration to emit.</param>
    /// <returns>A deterministic YAML document ending in one LF.</returns>
    /// <exception cref="ArgumentNullException">The scenario or configuration ID is null.</exception>
    /// <exception cref="ArgumentException">The selected configuration does not exist.</exception>
#pragma warning disable CA1822 // This generator is registered as an instance service; Generate is its DI-facing contract.
    public string Generate(ResolvedScenario scenario, string configurationId)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(configurationId);

        if (!scenario.Configurations.TryGetValue(configurationId, out var configuration))
        {
            throw new ArgumentException(
                $"Scenario '{scenario.Definition.Id}' has no configuration named '{configurationId}'.",
                nameof(configurationId));
        }

        using var output = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        var emitter = new Emitter(output, 2, 80, false);

        emitter.Emit(new StreamStart());
        emitter.Emit(new DocumentStart());
        StartMapping(emitter);

        EmitKey(emitter, "contract_version");
        EmitInteger(emitter, ScenarioContract.ValuesVersion);

        EmitKey(emitter, "scenario_id");
        EmitString(emitter, scenario.Definition.Id);

        EmitKey(emitter, "configuration_id");
        EmitString(emitter, configurationId);

        EmitKey(emitter, "profile");
        EmitProfile(emitter, scenario.Profile);

        EmitKey(emitter, "kafka");
        EmitKafka(emitter, configuration);

        EndMapping(emitter);
        emitter.Emit(new DocumentEnd(isImplicit: true));
        emitter.Emit(new StreamEnd());

        var yaml = output.ToString();
        return yaml.EndsWith('\n') ? yaml : string.Concat(yaml, '\n');
    }
#pragma warning restore CA1822

    // Map keys are written in ordinal order by hand, as the contract requires. Keep that order when adding
    // a field. List items keep their profile order, because stage order is meaningful.
    private static void EmitProfile(Emitter emitter, WorkloadProfile profile)
    {
        StartMapping(emitter);

        EmitKey(emitter, "documents_per_burst");
        EmitInteger(emitter, profile.DocumentsPerBurst);

        EmitKey(emitter, "id");
        EmitString(emitter, profile.Id);

        EmitKey(emitter, "payloads");
        StartMapping(emitter);
        foreach (var payload in profile.Payloads.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            EmitKey(emitter, payload.Key);
            StartSequence(emitter);
            foreach (var bucket in payload.Value)
            {
                StartMapping(emitter);
                EmitKey(emitter, "size_bytes");
                EmitInteger(emitter, bucket.SizeBytes);
                EmitKey(emitter, "weight");
                EmitInteger(emitter, bucket.Weight);
                EndMapping(emitter);
            }

            EndSequence(emitter);
        }

        EndMapping(emitter);

        EmitKey(emitter, "schema_version");
        EmitInteger(emitter, profile.SchemaVersion);

        EmitKey(emitter, "seed");
        EmitInteger(emitter, profile.Seed);

        EmitKey(emitter, "stages");
        StartSequence(emitter);
        foreach (var stage in profile.Stages)
        {
            StartMapping(emitter);
            EmitKey(emitter, "duration_seconds");
            EmitInteger(emitter, stage.DurationSeconds);
            EmitKey(emitter, "id");
            EmitString(emitter, stage.Id);
            EmitKey(emitter, "offered_attempts_per_second");
            EmitInteger(emitter, stage.OfferedAttemptsPerSecond);
            EndMapping(emitter);
        }

        EndSequence(emitter);

        EmitKey(emitter, "tenants");
        StartSequence(emitter);
        foreach (var tenant in profile.Tenants)
        {
            StartMapping(emitter);
            EmitKey(emitter, "id");
            EmitString(emitter, tenant.Id);
            EmitKey(emitter, "weight");
            EmitInteger(emitter, tenant.Weight);
            EndMapping(emitter);
        }

        EndSequence(emitter);
        EndMapping(emitter);
    }

    private static void EmitKafka(Emitter emitter, ResolvedConfiguration configuration)
    {
        StartMapping(emitter);
        foreach (var role in configuration.Roles.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            EmitKey(emitter, role.Key.ToString());
            StartMapping(emitter);
            foreach (var setting in role.Value.Settings.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                EmitKey(emitter, setting.Key);
                EmitString(emitter, setting.Value.Value);
            }

            EndMapping(emitter);
        }

        EndMapping(emitter);
    }

    private static void StartMapping(Emitter emitter) =>
        emitter.Emit(new MappingStart(null, null, true, MappingStyle.Block));

    private static void EndMapping(Emitter emitter) => emitter.Emit(new MappingEnd());

    private static void StartSequence(Emitter emitter) =>
        emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Block));

    private static void EndSequence(Emitter emitter) => emitter.Emit(new SequenceEnd());

    private static void EmitKey(Emitter emitter, string value) =>
        emitter.Emit(new Scalar(null, null, value, ScalarStyle.DoubleQuoted, false, true));

    private static void EmitString(Emitter emitter, string value) =>
        emitter.Emit(new Scalar(null, null, value, ScalarStyle.DoubleQuoted, false, true));

    private static void EmitInteger(Emitter emitter, int value) =>
        emitter.Emit(new Scalar(null, null, value.ToString(CultureInfo.InvariantCulture), ScalarStyle.Plain, true, false));
}
