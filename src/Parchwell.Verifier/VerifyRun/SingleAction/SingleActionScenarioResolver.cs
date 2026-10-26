using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;

namespace Parchwell.Verifier.VerifyRun.SingleAction;

/// <summary>Registered factory for independently resolving the native dictionary for a captured client identity.</summary>
internal sealed class SingleActionScenarioResolver(IOptions<VerifierHostOptions> configured)
{
    internal ResolvedRoleConfiguration Resolve(LoadedScenario scenario,string client,string configuration,KafkaClientRole selectedRole)
    {
        var options=configured.Value;
        var services=new ServiceCollection();
        foreach(var role in scenario.Definition.Roles)
            services.AddParchwellKafka(role,baseline=>
            {
                baseline.NamespaceName=options.NamespaceName!;baseline.ClientId=client;
                baseline.EnrichmentGroupId=$"single-action-{options.RunId}-{scenario.Definition.Id.ToLowerInvariant()}-{configuration.ToLowerInvariant()}";
            });
        using var provider=services.BuildServiceProvider();
        var resolver=new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>());
        return resolver.Resolve(scenario).Configurations[configuration].Roles[selectedRole];
    }
}
