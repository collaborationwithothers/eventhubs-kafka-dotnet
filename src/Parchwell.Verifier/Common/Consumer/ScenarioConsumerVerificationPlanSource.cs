using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Scenario;

namespace Parchwell.Verifier.Common.Consumer;

/// <summary>
/// Builds the verifier's plan for C03, C10 or C01 from the packaged scenario files: the effective settings each member must have
/// registered, and the completion deadline. The subject carries the configuration's replaced setting; the witness runs the baseline. C10 and C01 add the subject's kill count.
/// </summary>
internal sealed class ScenarioConsumerVerificationPlanSource(VerifierHostOptions options, ScenarioLoadOptions loadOptions, string root) : IConsumerVerificationPlanSource
{
    private static readonly string[] Reserved = ["bootstrap.servers", "client.id", "group.id"];

    public bool TryGetPlan(string scenarioId, string configurationId, out ConsumerVerificationPlan? plan)
    {
        plan = null;
        if (scenarioId is not ("C03" or "C10" or "C01") || configurationId is not ("C0" or "C1")) return false;
        try
        {
            var loader = new ScenarioLoader(new FileSystemScenarioSource(root), Options.Create(loadOptions));
            LoadedScenario scenario = loader.LoadAsync(scenarioId).AsTask().GetAwaiter().GetResult();
            ServiceCollection services = new();
            ConsumerPlanRoles.Register(services, options, options.VerifierClientId!);
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            KafkaConfigurationFactory factory = provider.GetRequiredService<KafkaConfigurationFactory>();
            ResolvedScenario resolved = new ScenarioResolver(factory).Resolve(scenario);
            Dictionary<string, string> subject = resolved.Configurations[configurationId].Roles[KafkaClientRole.EnrichmentConsumer].Settings
                .Where(pair => !Reserved.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);
            Dictionary<string, string> witness = factory.CreateEnrichmentConsumer().Configuration
                .Where(pair => !Reserved.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            ConsumerProcessingDefinition processing = scenario.Definition.ConsumerProcessing!;
            plan = processing.KillCounts.TryGetValue(configurationId, out int killCount)
                ? new ConsumerVerificationPlan(killCount, processing.CompletionDeadlineMs, subject, witness, null, null)
                : new ConsumerVerificationPlan(null, processing.CompletionDeadlineMs, subject, witness,
                    processing.WindowMs, processing.Configurations[configurationId].Mode);
            return true;
        }
        catch (Exception)
        {
            // A missing file or an invalid declaration surfaces as consumer_plan_invalid at startup.
            plan = null;
            return false;
        }
    }
}

/// <summary>Registers the three Kafka roles a consumer scenario declares, so its scenario resolves in a verifier process.</summary>
internal static class ConsumerPlanRoles
{
    internal static void Register(IServiceCollection services, VerifierHostOptions options, string producerClientId)
    {
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, baseline =>
        {
            baseline.NamespaceName = options.NamespaceName!;
            baseline.ClientId = producerClientId;
        });
        services.AddParchwellKafka(KafkaClientRole.VerifierConsumer, baseline =>
        {
            baseline.NamespaceName = options.NamespaceName!;
            baseline.ClientId = options.VerifierClientId!;
        });
        services.AddParchwellKafka(KafkaClientRole.EnrichmentConsumer, baseline =>
        {
            baseline.NamespaceName = options.NamespaceName!;
            baseline.ClientId = options.VerifierClientId!;
            baseline.EnrichmentGroupId = ConsumerPlanValidation.GroupId(options);
        });
    }
}
