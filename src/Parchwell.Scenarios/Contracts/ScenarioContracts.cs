using System.Collections.Immutable;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios;

/// <summary>A logical YAML source location.</summary>
public sealed record SourceLocation(string Path, int? Line = null, int? Column = null);

/// <summary>A source-aware validation diagnostic.</summary>
public sealed record ScenarioDiagnostic(string Source, string FieldPath, string Code, string Message,
    int? Line = null, int? Column = null);

/// <summary>A validated integer replacement and its definition location.</summary>
public sealed record SettingOverride(int Value, SourceLocation Location);

/// <summary>One named set of role-specific baseline replacements.</summary>
public sealed record ScenarioConfiguration(string Id,
    ImmutableDictionary<KafkaClientRole, ImmutableDictionary<string, SettingOverride>> Overrides);

/// <summary>A logical measurement request, independent of collection support.</summary>
public sealed record MeasurementRequest(string Id, KafkaClientRole Role);

/// <summary>A live fault intent; kind is none or throttling.</summary>
public sealed record LiveFault(string Kind, ImmutableArray<string> Configurations);

/// <summary>A component fault declaration; kind is none or toxiproxy_latency.</summary>
public sealed record ComponentFault(string Kind, ImmutableArray<string> Configurations,
    string? Direction = null, int? LatencyMs = null);

/// <summary>Separate live and component fault declarations.</summary>
public sealed record FaultDefinition(LiveFault Live, ComponentFault Component);

/// <summary>A probe declaration, or an explicit none gate. Outcomes are fixed by the contract.</summary>
public sealed record ProbeGate(string Kind, string? Configuration, MeasurementRequest? Measurement,
    string? Operator, long? ThresholdMs, int? Repeats, ImmutableArray<string> Configurations);

/// <summary>A fully validated scenario declaration.</summary>
public sealed record ScenarioDefinition(int SchemaVersion, string Id, string Baseline,
    ImmutableArray<KafkaClientRole> Roles, string Profile,
    ImmutableDictionary<string, ScenarioConfiguration> Configurations, FaultDefinition Fault,
    int Repeats, ProbeGate Gate, ImmutableArray<MeasurementRequest> Measures, SourceLocation Source);

/// <summary>A synthetic tenant and its positive relative traffic weight.</summary>
public sealed record TenantWeight(string Id, int Weight);

/// <summary>An explicit payload size and its positive relative weight.</summary>
public sealed record PayloadBucket(int SizeBytes, int Weight);

/// <summary>A finite stage in a predetermined offered-load schedule.</summary>
public sealed record LoadStage(string Id, int OfferedAttemptsPerSecond, int DurationSeconds);

/// <summary>A validated, seeded synthetic workload profile.</summary>
public sealed record WorkloadProfile(int SchemaVersion, string Id, int Seed,
    ImmutableArray<TenantWeight> Tenants, int DocumentsPerBurst,
    ImmutableDictionary<string, ImmutableArray<PayloadBucket>> Payloads,
    ImmutableArray<LoadStage> Stages, SourceLocation Source);

/// <summary>Immutable inputs produced only by successful scenario loading.</summary>
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

/// <summary>The inventory and definition that replaced an initial baseline value.</summary>
public sealed record OverrideOrigin(string ScenarioId, string ConfigurationId, SourceLocation Location);

/// <summary>An effective string value with unchanged initial-emission metadata.</summary>
public sealed record ResolvedSetting(string Value, KafkaSettingMetadata Baseline, OverrideOrigin? Override);

/// <summary>Immutable effective settings and deliberate omissions for one role.</summary>
public sealed record ResolvedRoleConfiguration(KafkaClientRole Role,
    ImmutableDictionary<string, ResolvedSetting> Settings, ImmutableArray<KafkaSettingMetadata> Omissions);

/// <summary>A resolved named configuration.</summary>
public sealed record ResolvedConfiguration(string Id,
    ImmutableDictionary<KafkaClientRole, ResolvedRoleConfiguration> Roles);

/// <summary>An immutable run declaration; it establishes no execution capability or verdict.</summary>
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

/// <summary>The supported format versions and repository repeat policy.</summary>
public static class ScenarioContract
{
    public const int SchemaVersion = 1;
    public const int ValuesVersion = 1;
    // docs/agents/factory.md, Plan template: at least five repeats per configuration.
    public const int MinimumRepeats = 5;
}
