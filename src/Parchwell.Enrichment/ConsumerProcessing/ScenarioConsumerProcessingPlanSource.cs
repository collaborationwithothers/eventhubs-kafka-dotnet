using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;

namespace Parchwell.Enrichment.ConsumerProcessing;

/// <summary>
/// Builds the worker's plan for C03, C10 or C01 from the packaged scenario files. The subject gets C03's slow message or the
/// kill count of C10 and C01, and the configuration's one replaced setting; the witness gets the same window and delay and runs the baseline.
/// </summary>
internal sealed class ScenarioConsumerProcessingPlanSource(IOptions<ConsumerProcessingOptions> options, string root) : IConsumerProcessingPlanSource
{
    // The scenario_parsing values of docs/operations/a01-runtime-limits.json. The member container is given no
    // parsing budget by the chart, so the source holds them as constants.
    private static readonly ScenarioLoadOptions LoadOptions = new()
    {
        MaxDocumentCharacters = 1048576, MaxNestingDepth = 32, MaxScalarCharacters = 65536, MaxParserEvents = 100000
    };

    public ConsumerProcessingPlan? GetPlan(string scenarioId, string configurationId)
    {
        if (scenarioId is not ("C03" or "C10" or "C01") || configurationId is not ("C0" or "C1")) return null;
        try
        {
            ConsumerProcessingOptions identity = options.Value;
            var loader = new ScenarioLoader(new ScenarioFileSource(root), Options.Create(LoadOptions));
            LoadedScenario scenario = loader.LoadAsync(scenarioId).AsTask().GetAwaiter().GetResult();
            ResolvedScenario resolved = Resolve(scenario, identity);
            ConsumerProcessingDefinition processing = scenario.Definition.ConsumerProcessing!;
            ConsumerProfile consumer = scenario.Profile.Consumer!;
            bool subject = identity.Role == "subject";
            ConsumerSlowMessageDeclaration? slow = processing.Configurations.GetValueOrDefault(configurationId);
            int? killCount = processing.KillCounts.TryGetValue(configurationId, out int declaredKillCount) ? declaredKillCount : null;
            ImmutableDictionary<string, ResolvedSetting> settings = resolved.Configurations[configurationId].Roles[KafkaClientRole.EnrichmentConsumer].Settings;
            return new ConsumerProcessingPlan
            {
                ConfigurationId = configurationId,
                ProcessingDelayMs = consumer.ProcessingDelayMs,
                WindowMs = processing.WindowMs,
                SlowMessage = subject && slow is not null ? new ConsumerSlowMessage(slow.Ordinal, slow.DurationMs, slow.Mode) : null,
                KillCount = subject ? killCount : null,
                ConsumerSettings = subject
                    ? settings.Where(pair => pair.Value.Override is not null).ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal)
            };
        }
        catch (Exception)
        {
            // A missing file or an invalid declaration is reported by the worker as consumer_plan_invalid.
            return null;
        }
    }

    private static ResolvedScenario Resolve(LoadedScenario scenario, ConsumerProcessingOptions identity)
    {
        var services = new ServiceCollection();
        foreach (KafkaClientRole role in new[] { KafkaClientRole.LoadGenProducer, KafkaClientRole.EnrichmentConsumer, KafkaClientRole.VerifierConsumer })
        {
            services.AddParchwellKafka(role, baseline =>
            {
                baseline.NamespaceName = identity.NamespaceName;
                baseline.ClientId = identity.PodName;
                if (role == KafkaClientRole.EnrichmentConsumer) baseline.EnrichmentGroupId = identity.GroupId;
            });
        }
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        return new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(scenario);
    }
}
