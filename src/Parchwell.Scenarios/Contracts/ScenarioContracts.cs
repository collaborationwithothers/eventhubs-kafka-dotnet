using System.Collections.Immutable;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios;

/// <summary>
/// Where a value came from: the logical path of its YAML document, such as
/// scenarios/A01/scenario.yaml, and its one-based line and column when known.
/// </summary>
public sealed record SourceLocation(string Path, int? Line = null, int? Column = null);

/// <summary>One validation problem found in a scenario definition or profile.</summary>
/// <remarks>
/// <see cref="ScenarioDiagnostic.Code"/> is a stable identifier that callers may compare. It changes only through a
/// documented migration (docs/operations/scenario-contract.md, "Diagnostic code migration").
/// <see cref="ScenarioDiagnostic.FieldPath"/> names the YAML field, such as configurations.c1.LoadGenProducer.
/// </remarks>
public sealed record ScenarioDiagnostic(string Source, string FieldPath, string Code, string Message,
    int? Line = null, int? Column = null);

/// <summary>
/// An override: one value in a scenario definition that replaces one baseline Kafka setting
/// for one client role, with the place in the file where it was written.
/// </summary>
/// <remarks>
/// The loader stores policy-admitted strings, plain decimal Int32 values, boolean literals, or plain omit. Which keys, roles and ranges are
/// allowed is checked later by <see cref="ScenarioResolver"/>. Overrides are how a scenario expresses
/// its configurations as deltas from the baseline (ADR-014, ADR-015).
/// </remarks>
public sealed record SettingOverride(string Value, SourceLocation Location);

/// <summary>
/// A configuration: one named variant of the experiment, such as a baseline run or a run with a
/// changed timeout. It holds the overrides to apply, grouped by Kafka client role.
/// </summary>
/// <remarks>
/// A configuration with no overrides runs the baseline unchanged, and is valid. The scenario runs
/// each configuration the number of times given by <see cref="ScenarioDefinition.Repeats"/>.
/// The identifier is an ASCII slug that starts with a letter.
/// </remarks>
public sealed record ScenarioConfiguration(string Id,
    ImmutableDictionary<KafkaClientRole, ImmutableDictionary<string, SettingOverride>> Overrides);

/// <summary>
/// A measure: a request that runs of this scenario record one named measurement for one client role,
/// such as verifier.loss for the verifier consumer.
/// </summary>
/// <remarks>
/// The id must be in the declaration catalogue (<see cref="MeasurementCatalog"/>). A valid request does
/// not mean a collector for it exists yet (docs/operations/scenario-contract.md, "Measurements and review boundaries").
/// </remarks>
public sealed record MeasurementRequest(string Id, KafkaClientRole Role);

/// <summary>The fault kinds a scenario may declare for live runs against Event Hubs.</summary>
public enum LiveFaultKind
{
    None,
    Throttling
}

/// <summary>The fault kinds a scenario may declare for component tests against a local broker (ADR-016).</summary>
public enum ComponentFaultKind
{
    None,
    ToxiproxyLatency
}

/// <summary>
/// The live fault: whether, and for which configurations, a live run is declared to run under
/// throttling. This library records the intent only; it does not cause throttling.
/// </summary>
public sealed record LiveFault(LiveFaultKind Kind, ImmutableArray<string> Configurations);

/// <summary>
/// The component fault: whether, and for which configurations, component tests add Toxiproxy latency
/// (ADR-016). <see cref="ComponentFault.Direction"/> and <see cref="ComponentFault.LatencyMs"/> are set only for
/// <see cref="ComponentFaultKind.ToxiproxyLatency"/>.
/// </summary>
public sealed record ComponentFault(ComponentFaultKind Kind, ImmutableArray<string> Configurations,
    string? Direction = null, int? LatencyMs = null);

/// <summary>
/// The fault of a scenario: the condition deliberately applied to some configurations. It has a live
/// part and a component part, each of which may be none; the two run independently.
/// </summary>
public sealed record FaultDefinition(LiveFault Live, ComponentFault Component);

/// <summary>The supported probe predicate operators.</summary>
public enum ProbeOperator
{
    GreaterThan
}

/// <summary>
/// A gate: a check that must pass before some full runs of a scenario may go ahead. The only kinds are
/// <see cref="NoGate"/> and <see cref="ProbeGate"/>; the private protected constructor keeps the set closed.
/// </summary>
public abstract record ScenarioGate
{
    private protected ScenarioGate() { }
}

/// <summary>An explicit declaration that no gate is required.</summary>
public sealed record NoGate : ScenarioGate;

/// <summary>
/// A probe gate: a separate phase, run before the full runs, that tests a predicate on one
/// measurement. <see cref="ProbeGate.Configuration"/> is the configuration the probe runs, and
/// <see cref="ProbeGate.Repeats"/> is the probe's own repeat count. <see cref="ProbeGate.Configurations"/> lists the full
/// configurations that may run only if the probe passes.
/// </summary>
/// <remarks>
/// What pass, fail and missing evidence mean is fixed by docs/operations/scenario-contract.md,
/// "Scenario schema". This library declares the gate only; it does not run or evaluate a probe.
/// </remarks>
public sealed record ProbeGate(string Configuration, MeasurementRequest Measurement,
    ProbeOperator Operator, int ThresholdMs, int Repeats, ImmutableArray<string> Configurations) : ScenarioGate;

/// <summary>
/// A scenario definition: the validated content of scenarios/&lt;ID&gt;/scenario.yaml, which says
/// what runs for one frozen-inventory item (ADR-014). A scenario is that item's experiment.
/// </summary>
/// <remarks>
/// <para>
/// It names the baseline (always Parchwell.Kafka, see ADR-015), the Kafka client roles taking part,
/// the workload profile, the configurations, the fault, the repeat count, the gate and the measures.
/// </para>
/// <para>
/// <see cref="ScenarioDefinition.Repeats"/> is how many times each configuration's full run is repeated. Each repeat is
/// one run window (ADR-013). It is at least <see cref="ScenarioContract.MinimumRepeats"/>.
/// </para>
/// <para>
/// The predicted outcome lives in a separate protected file, expected.yaml, which this library never
/// reads, so a prediction cannot shape what runs (ADR-014).
/// </para>
/// </remarks>
public sealed record ScenarioDefinition(int SchemaVersion, string Id, string Baseline,
    ImmutableArray<KafkaClientRole> Roles, string Profile,
    ImmutableDictionary<string, ScenarioConfiguration> Configurations, FaultDefinition Fault,
    int Repeats, ScenarioGate Gate, ImmutableArray<MeasurementRequest> Measures, SourceLocation Source,
    SingleActionDefinition? SingleAction = null);

/// <summary>The validated fixed single-action rows; owning checks are in scenario-contract.md.</summary>
public sealed record SingleActionDefinition(ImmutableDictionary<string, SingleActionConfiguration> Configurations);

/// <summary>One fresh-client action with its literal declared inputs.</summary>
public sealed record SingleActionConfiguration(string Action, KafkaClientRole Role, string Topic, int Partition,
    int PayloadBytes, int WindowMs, string TokenScope, string Match, string TerminalRule = "standard");

/// <summary>
/// A synthetic tenant, standing for one firm in the workload, and its share of traffic relative to the
/// other tenants' weights.
/// </summary>
public sealed record TenantWeight(string Id, int Weight);

/// <summary>One payload size in bytes for one lifecycle event type, and how often it is chosen relative to the other sizes.</summary>
public sealed record PayloadBucket(int SizeBytes, int Weight);

/// <summary>
/// A stage: one step of the load schedule, offering a fixed number of produce attempts per second for
/// a fixed time. Stages run in order and never change in response to what is observed.
/// </summary>
public sealed record LoadStage(string Id, int OfferedAttemptsPerSecond, int DurationSeconds);

/// <summary>
/// A workload profile: the synthetic input a scenario generates, read from
/// scenarios/profiles/&lt;slug&gt;.yaml. Several scenarios may share one profile (ADR-014).
/// </summary>
/// <remarks>
/// The seed, tenant weights, burst size and payload sizes make the input reproducible, and the stages
/// fix the offered load in advance (ADR-008). Payload keys are the five document lifecycle events in
/// docs/blueprint.md section 2, "Workload". The values are synthetic planning inputs, not measurements.
/// </remarks>
public sealed record WorkloadProfile(int SchemaVersion, string Id, int Seed,
    ImmutableArray<TenantWeight> Tenants, int DocumentsPerBurst,
    ImmutableDictionary<string, ImmutableArray<PayloadBucket>> Payloads,
    ImmutableArray<LoadStage> Stages, SourceLocation Source);

/// <summary>
/// A scenario definition and its profile, both fully validated. The constructor is internal and only
/// <see cref="ScenarioLoader"/> calls it, so holding one means loading succeeded.
/// </summary>
public sealed class LoadedScenario
{
    internal LoadedScenario(ScenarioDefinition definition, WorkloadProfile profile)
    {
        Definition = definition;
        Profile = profile;
    }

    public ScenarioDefinition Definition { get; }
    public WorkloadProfile Profile { get; }
}

/// <summary>
/// Provenance of an override: the scenario (inventory id), configuration and file position that
/// replaced a baseline value.
/// </summary>
public sealed record OverrideOrigin(string ScenarioId, string ConfigurationId, SourceLocation Location);

/// <summary>
/// One effective Kafka setting after resolution. <see cref="ResolvedSetting.Baseline"/> still describes the value the
/// baseline first emitted, even when <see cref="ResolvedSetting.Override"/> shows that a configuration replaced it.
/// </summary>
public sealed record ResolvedSetting(string Value, KafkaSettingMetadata Baseline, OverrideOrigin? Override);

/// <summary>
/// The effective settings for one client role in one configuration, the remaining baseline omissions,
/// and scenario removals retaining their emitted baseline provenance.
/// </summary>
public sealed record ResolvedRoleConfiguration(KafkaClientRole Role,
    ImmutableDictionary<string, ResolvedSetting> Settings, ImmutableArray<KafkaSettingMetadata> Omissions,
    ImmutableArray<ResolvedOmission> ScenarioOmissions = default)
{
    /// <summary>Gets scenario removals, empty when the configuration emits every baseline key.</summary>
    public ImmutableArray<ResolvedOmission> ScenarioOmissions { get; init; } = ScenarioOmissions.IsDefault ? [] : ScenarioOmissions;
}

/// <summary>A scenario removal retaining the original emitted metadata and YAML provenance.</summary>
public sealed record ResolvedOmission(string Key, KafkaSettingMetadata Baseline, OverrideOrigin Origin);

/// <summary>One configuration after its overrides were applied to a fresh baseline for every declared role.</summary>
public sealed record ResolvedConfiguration(string Id,
    ImmutableDictionary<KafkaClientRole, ResolvedRoleConfiguration> Roles);

/// <summary>
/// A resolved scenario: the definition, profile and effective settings of every configuration.
/// It is a run plan only; it says nothing about whether a worker can run it or what the result is.
/// </summary>
public sealed class ResolvedScenario
{
    internal ResolvedScenario(LoadedScenario loaded, ImmutableDictionary<string, ResolvedConfiguration> configurations)
    {
        Definition = loaded.Definition;
        Profile = loaded.Profile;
        Configurations = configurations;
    }

    public ScenarioDefinition Definition { get; }
    public WorkloadProfile Profile { get; }
    public ImmutableDictionary<string, ResolvedConfiguration> Configurations { get; }
}

/// <summary>The contract version literals and the repository's minimum repeat count.</summary>
/// <remarks>
/// <see cref="ScenarioContract.SchemaVersion"/> is the schema_version that scenario and profile files must declare.
/// Changing it is a contract change (docs/operations/scenario-contract.md).
/// </remarks>
public static class ScenarioContract
{
    public const int SchemaVersion = 1;
    // docs/agents/factory.md, Plan template: at least five repeats per configuration.
    public const int MinimumRepeats = 5;
}
