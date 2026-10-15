using System.Collections.Immutable;
using Confluent.Kafka;

namespace Parchwell.Kafka.Configuration;

/// <summary>A fresh native configuration with immutable metadata describing its initial emission.</summary>
/// <remarks>
/// <para>
/// The baseline is the one starting configuration for each Kafka client the harness runs.
/// Every scenario is described as a delta from it, so a scenario file shows only what it changes.
/// The baseline follows Microsoft's guidance for librdkafka clients. Every setting the harness
/// depends on is set explicitly, even when the value equals the library default, and is tagged
/// with the inventory items it serves. Rule and conflict policy: ADR-015 in docs/blueprint.md.
/// </para>
/// <para>
/// "Native" means the string key and value form, rather than Confluent.Kafka's typed properties.
/// Most keys are librdkafka keys from its CONFIGURATION.md at v2.16.0. Keys that start with
/// "dotnet." are Confluent.Kafka's own settings (see KafkaSettingKeys). "Emission" means that write.
/// </para>
/// <para>
/// Each call to the factory returns a new instance. The caller may change the
/// Configuration property, for example to apply a scenario delta. <see cref="Settings"/> and
/// <see cref="Omissions"/> do not change with it. They keep recording what the baseline itself emitted.
/// </para>
/// </remarks>
/// <typeparam name="TConfig">The native producer or consumer configuration type.</typeparam>
public sealed class KafkaBaseline<TConfig> where TConfig : ClientConfig
{
    internal KafkaBaseline(TConfig configuration, ImmutableArray<KafkaSettingMetadata> settings,
        ImmutableArray<KafkaSettingMetadata> omissions)
    {
        Configuration = configuration;
        Settings = settings;
        Omissions = omissions;
    }

    /// <summary>Gets the caller-owned mutable native client configuration.</summary>
    public TConfig Configuration { get; }
    /// <summary>Gets immutable metadata for the initial emitted values.</summary>
    public ImmutableArray<KafkaSettingMetadata> Settings { get; }
    /// <summary>Gets immutable metadata for settings excluded or left unspecified.</summary>
    public ImmutableArray<KafkaSettingMetadata> Omissions { get; }
}
