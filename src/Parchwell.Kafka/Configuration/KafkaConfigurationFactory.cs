using System.Collections.Immutable;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Parchwell.Kafka.Configuration;

/// <summary>Creates baseline native configurations without constructing clients or performing I/O.</summary>
public sealed class KafkaConfigurationFactory
{
    private readonly IOptionsMonitor<KafkaBaselineOptions> options;
    private readonly HashSet<KafkaClientRole> registeredRoles;

    internal KafkaConfigurationFactory(IOptionsMonitor<KafkaBaselineOptions> options,
        IEnumerable<KafkaRoleRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(registrations);
        this.options = options;
        registeredRoles = registrations.Select(registration => registration.Role).ToHashSet();
    }

    /// <summary>Creates a fresh load-generator producer configuration and its initial metadata.</summary>
    /// <returns>The native producer configuration with immutable setting and omission metadata.</returns>
    /// <exception cref="InvalidOperationException">The workload role was not registered.</exception>
    /// <exception cref="OptionsValidationException">The required caller inputs are invalid.</exception>
    public KafkaBaseline<ProducerConfig> CreateLoadGenProducer() =>
        Create<ProducerConfig>(KafkaClientRole.LoadGenProducer);

    /// <summary>Creates a fresh enrichment producer configuration and its initial metadata.</summary>
    /// <returns>The native producer configuration with immutable setting and omission metadata.</returns>
    /// <exception cref="InvalidOperationException">The workload role was not registered.</exception>
    /// <exception cref="OptionsValidationException">The required caller inputs are invalid.</exception>
    public KafkaBaseline<ProducerConfig> CreateEnrichmentProducer() =>
        Create<ProducerConfig>(KafkaClientRole.EnrichmentProducer);

    /// <summary>Creates a fresh enrichment consumer configuration and its initial metadata.</summary>
    /// <returns>The native consumer configuration with immutable setting and omission metadata.</returns>
    /// <exception cref="InvalidOperationException">The workload role was not registered.</exception>
    /// <exception cref="OptionsValidationException">The required caller inputs are invalid.</exception>
    public KafkaBaseline<ConsumerConfig> CreateEnrichmentConsumer() =>
        Create<ConsumerConfig>(KafkaClientRole.EnrichmentConsumer);

    /// <summary>Creates a fresh assigned-verifier consumer configuration and its initial metadata.</summary>
    /// <returns>The native consumer configuration with immutable setting and omission metadata.</returns>
    /// <exception cref="InvalidOperationException">The workload role was not registered.</exception>
    /// <exception cref="OptionsValidationException">The required caller inputs are invalid.</exception>
    public KafkaBaseline<ConsumerConfig> CreateVerifierConsumer() =>
        Create<ConsumerConfig>(KafkaClientRole.VerifierConsumer);

    private KafkaBaseline<TConfig> Create<TConfig>(KafkaClientRole role) where TConfig : ClientConfig, new()
    {
        if (!registeredRoles.Contains(role))
            throw new InvalidOperationException($"Kafka role {role} has not been registered.");

        var configured = options.Get(role.ToString());
        var snapshot = configured with { };
        var validation = new KafkaBaselineOptionsValidator(role).Validate(role.ToString(), snapshot);
        if (validation.Failed)
            throw new OptionsValidationException(role.ToString(), typeof(KafkaBaselineOptions), validation.Failures);

        var configuration = new TConfig();
        var settings = ImmutableArray.CreateBuilder<KafkaSettingMetadata>();
        var omissions = ImmutableArray.CreateBuilder<KafkaSettingMetadata>();
        foreach (var entry in BaselineSettingsCatalog.GetEntries(role))
        {
            var value = entry.Disposition == KafkaSettingDisposition.Explicit ? entry.ResolveValue(snapshot) : null;
            var metadata = new KafkaSettingMetadata(entry.Key, value, role, entry.Disposition,
                entry.InventoryIds, entry.Sources, entry.SelectionReason);
            if (entry.Disposition == KafkaSettingDisposition.Explicit)
            {
                configuration.Set(entry.Key, value!);
                settings.Add(metadata);
            }
            else
            {
                omissions.Add(metadata);
            }
        }
        return new KafkaBaseline<TConfig>(configuration, settings.ToImmutable(), omissions.ToImmutable());
    }
}
