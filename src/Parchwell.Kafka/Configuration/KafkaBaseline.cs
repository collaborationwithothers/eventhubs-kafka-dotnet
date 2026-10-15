using System.Collections.Immutable;
using Confluent.Kafka;

namespace Parchwell.Kafka.Configuration;

/// <summary>A fresh native configuration with immutable metadata describing its initial emission.</summary>
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
