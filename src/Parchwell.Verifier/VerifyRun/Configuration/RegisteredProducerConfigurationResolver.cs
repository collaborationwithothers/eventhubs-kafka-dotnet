using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Phase;

namespace Parchwell.Verifier.VerifyRun.Configuration;

/// <summary>Resolves the packaged scenario from the producer client identity independently discovered at runtime.</summary>
internal sealed class RegisteredProducerConfigurationResolver(IOptions<VerifierHostOptions> configured)
{
    internal ResolvedScenario Resolve(LoadedScenario scenario, ProducerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(registration);
        var options = configured.Value;
        string namespaceName = options.NamespaceName!;
        string producerClientId = registration.Identity.ClientId;
        string verifierClientId = options.VerifierClientId!;
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, baseline =>
        {
            baseline.NamespaceName = namespaceName;
            baseline.ClientId = producerClientId;
        });
        services.AddParchwellKafka(KafkaClientRole.VerifierConsumer, baseline =>
        {
            baseline.NamespaceName = namespaceName;
            baseline.ClientId = verifierClientId;
        });
        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var resolver = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>());
        return resolver.Resolve(scenario);
    }
}
