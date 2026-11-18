using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Scenario;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Configuration;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707 // Repository test naming convention.

// Protects the production consumer verification plan source for C03 and the consumer-role registration of the producer configuration resolver.
[Collection("VerifierEnvironment")]
public sealed class ScenarioVerificationPlanSourceTests
{
    private static readonly string[] Reserved = ["bootstrap.servers", "client.id", "group.id"];

    // The budgets the chart gives consumer-verify (docs/operations/a01-runtime-limits.json: verifier_shared, verifier_kafka, verifier_verify).
    private static VerifierHostOptions Options(string scenario = "C03", string phase = "C0")
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("verify");
        options.Mode = "consumer-verify";
        options.ScenarioId = scenario;
        options.Phase = phase;
        options.ConfigurationId = phase;
        options.ConsumerRecordContainer = "consumer-record";
        options.CleanupTimeout = TimeSpan.FromSeconds(10);
        options.IoTimeout = TimeSpan.FromSeconds(10);
        options.TokenTimeout = TimeSpan.FromSeconds(10);
        options.MetadataTimeout = TimeSpan.FromSeconds(10);
        options.WatermarkTimeout = TimeSpan.FromSeconds(10);
        options.BlobIoTimeout = TimeSpan.FromSeconds(30);
        options.SafetyStop = TimeSpan.FromMinutes(10);
        options.ProcessLifetime = TimeSpan.FromMinutes(12);
        return options;
    }

    private static ScenarioLoadOptions Limits() => new() { MaxDocumentCharacters = 1048576, MaxNestingDepth = 32, MaxScalarCharacters = 65536, MaxParserEvents = 100000 };

    private static ScenarioConsumerVerificationPlanSource Source(VerifierHostOptions options) => new(options, Limits(), AppContext.BaseDirectory);

    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public void Given_C03_When_the_production_source_is_asked_Then_it_returns_the_plan_and_the_chart_budgets_accept_it(string phase)
    {
        VerifierHostOptions options = Options("C03", phase);
        Assert.True(Source(options).TryGetPlan("C03", phase, out ConsumerVerificationPlan? plan));
        Assert.Null(plan!.KillCount);
        Assert.Equal(30000, plan.CompletionDeadlineMs);
        Assert.Equal("60000", plan.SubjectSettings["max.poll.interval.ms"]);
        Assert.Equal("300000", plan.WitnessSettings["max.poll.interval.ms"]);
        Assert.All(Reserved, key => Assert.DoesNotContain(key, plan.SubjectSettings.Keys));
        Assert.All(Reserved, key => Assert.DoesNotContain(key, plan.WitnessSettings.Keys));
        Assert.True(ConsumerPlanValidation.IsValid(plan, options));
    }

    [Theory]
    [InlineData("C10", "C0")]
    [InlineData("C01", "C1")]
    [InlineData("C03", "C2")]
    [InlineData("B04", "C0")]
    public void Given_any_other_pair_When_the_production_source_is_asked_Then_it_returns_false(string scenario, string phase)
    {
        Assert.False(Source(Options()).TryGetPlan(scenario, phase, out ConsumerVerificationPlan? plan));
        Assert.Null(plan);
    }

    [Theory]
    [InlineData("C0", true)]
    [InlineData("C1", true)]
    [InlineData("C0", false)]
    [InlineData("C1", false)]
    public void Given_a_member_role_When_comparing_with_the_worker_settings_Then_the_plan_plus_the_reserved_keys_equals_them_key_for_key(string phase, bool subject)
    {
        VerifierHostOptions options = Options("C03", phase);
        Assert.True(Source(options).TryGetPlan("C03", phase, out ConsumerVerificationPlan? plan));
        // What the worker registers: the baseline factory configuration, with the subject's one replaced setting, and its own pod identity and group.
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.EnrichmentConsumer, baseline =>
        {
            baseline.NamespaceName = options.NamespaceName!;
            baseline.ClientId = "run-1-pod";
            baseline.EnrichmentGroupId = ConsumerPlanValidation.GroupId(options);
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        Dictionary<string, string> worker = provider.GetRequiredService<KafkaConfigurationFactory>().CreateEnrichmentConsumer().Configuration
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (subject) worker["max.poll.interval.ms"] = "60000";
        var expected = new Dictionary<string, string>(subject ? plan!.SubjectSettings : plan!.WitnessSettings, StringComparer.Ordinal)
        {
            ["bootstrap.servers"] = worker["bootstrap.servers"], ["client.id"] = "run-1-pod", ["group.id"] = ConsumerPlanValidation.GroupId(options)
        };
        Assert.Equal(worker.OrderBy(pair => pair.Key, StringComparer.Ordinal), expected.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("C03", "run-1-c0-r1-consumers")]
    [InlineData("B04", null)]
    public async Task Given_a_scenario_and_a_producer_registration_When_resolving_Then_only_a_consumer_scenario_also_registers_EnrichmentConsumer(string scenario, string? group)
    {
        VerifierHostOptions options = Options(scenario);
        var resolver = new RegisteredProducerConfigurationResolver(Microsoft.Extensions.Options.Options.Create(options));
        var source = new FileSystemScenarioSource(AppContext.BaseDirectory);
        LoadedScenario loaded = await new ScenarioLoader(source, Microsoft.Extensions.Options.Options.Create(Limits())).LoadAsync(scenario, TestContext.Current.CancellationToken);
        var identity = new RunIdentity(scenario, "C0", "run-1", "run-1-c0-r1-loadgen", "run-1-c0-r1-loadgen", "run-1-c0-r1-loadgen");
        var registration = new ProducerRegistration(1, identity, "C0", 1, "run-1-c0-r1-loadgen", "pod", "uid", new string('a', 40));
        ResolvedScenario resolved = resolver.Resolve(loaded, registration);
        var roles = resolved.Configurations["C0"].Roles;
        if (group is null) Assert.DoesNotContain(KafkaClientRole.EnrichmentConsumer, roles.Keys);
        else
        {
            Assert.Equal(group, ConsumerPlanValidation.GroupId(options));
            Assert.Equal("60000", roles[KafkaClientRole.EnrichmentConsumer].Settings["max.poll.interval.ms"].Value);
            Assert.Equal(group, roles[KafkaClientRole.EnrichmentConsumer].Settings["group.id"].Value);
        }
        Assert.Equal("run-1-c0-r1-loadgen", roles[KafkaClientRole.LoadGenProducer].Settings["client.id"].Value);
    }
}
