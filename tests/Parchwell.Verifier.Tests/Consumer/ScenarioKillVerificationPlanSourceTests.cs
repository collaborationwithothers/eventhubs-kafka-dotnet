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

// Protects the production consumer verification plan source for C10 and C01: the kill counts, the settings each member must have
// registered, the chart budgets and the resolver's consumer registration.
[Collection("VerifierEnvironment")]
public sealed class ScenarioKillVerificationPlanSourceTests
{
    private static readonly string[] Reserved = ["bootstrap.servers", "client.id", "group.id"];

    public static TheoryData<string, string, int, string?, string?> Pairs => new()
    {
        { "C10", "C0", 110, null, null },
        { "C10", "C1", 110, "enable.auto.offset.store", "true" },
        { "C01", "C0", 1, null, null },
        { "C01", "C1", 1, "session.timeout.ms", "45000" }
    };

    // The budgets the chart gives consumer-verify (docs/operations/a01-runtime-limits.json: verifier_shared, verifier_kafka, verifier_verify).
    private static VerifierHostOptions Options(string scenario, string phase)
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
    [MemberData(nameof(Pairs))]
    public void Given_C10_or_C01_When_the_production_source_is_asked_Then_it_returns_the_kill_plan_and_the_chart_budgets_accept_it(
        string scenario, string phase, int killCount, string? key, string? value)
    {
        VerifierHostOptions options = Options(scenario, phase);
        Assert.True(Source(options).TryGetPlan(scenario, phase, out ConsumerVerificationPlan? plan));
        Assert.Equal(killCount, plan!.KillCount);
        Assert.Equal(30000, plan.CompletionDeadlineMs);
        Assert.Null(plan.WindowMs);
        Assert.Null(plan.SlowMessageMode);
        Assert.Equal("false", plan.WitnessSettings["enable.auto.offset.store"]);
        Assert.Equal("30000", plan.WitnessSettings["session.timeout.ms"]);
        Assert.Equal(key is null ? plan.WitnessSettings.OrderBy(pair => pair.Key, StringComparer.Ordinal) :
            plan.WitnessSettings.Where(pair => pair.Key != key).Append(KeyValuePair.Create(key, value!)).OrderBy(pair => pair.Key, StringComparer.Ordinal),
            plan.SubjectSettings.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.All(Reserved, reserved => Assert.DoesNotContain(reserved, plan.SubjectSettings.Keys));
        Assert.All(Reserved, reserved => Assert.DoesNotContain(reserved, plan.WitnessSettings.Keys));
        Assert.True(ConsumerPlanValidation.IsValid(plan, options));
    }

    [Theory]
    [InlineData("C10", "C2")]
    [InlineData("C01", "C2")]
    [InlineData("C03", "C2")]
    [InlineData("B04", "C0")]
    public void Given_a_pair_the_source_does_not_serve_When_asked_Then_it_returns_false(string scenario, string phase)
    {
        Assert.False(Source(Options("C10", "C0")).TryGetPlan(scenario, phase, out ConsumerVerificationPlan? plan));
        Assert.Null(plan);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Given_each_member_role_When_comparing_with_the_worker_settings_Then_the_plan_plus_the_reserved_keys_equals_them_key_for_key(
        string scenario, string phase, int killCount, string? key, string? value)
    {
        Assert.True(killCount > 0);
        VerifierHostOptions options = Options(scenario, phase);
        Assert.True(Source(options).TryGetPlan(scenario, phase, out ConsumerVerificationPlan? plan));
        foreach (bool subject in new[] { true, false })
        {
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
            if (subject && key is not null) worker[key] = value!;
            var expected = new Dictionary<string, string>(subject ? plan!.SubjectSettings : plan!.WitnessSettings, StringComparer.Ordinal)
            {
                ["bootstrap.servers"] = worker["bootstrap.servers"], ["client.id"] = "run-1-pod", ["group.id"] = ConsumerPlanValidation.GroupId(options)
            };
            Assert.Equal(worker.OrderBy(pair => pair.Key, StringComparer.Ordinal), expected.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        }
    }

    [Theory]
    [InlineData("C10")]
    [InlineData("C01")]
    public async Task Given_a_C10_or_C01_scenario_and_a_producer_registration_When_resolving_Then_EnrichmentConsumer_is_registered_with_the_group(string scenario)
    {
        VerifierHostOptions options = Options(scenario, "C0");
        var resolver = new RegisteredProducerConfigurationResolver(Microsoft.Extensions.Options.Options.Create(options));
        var source = new FileSystemScenarioSource(AppContext.BaseDirectory);
        LoadedScenario loaded = await new ScenarioLoader(source, Microsoft.Extensions.Options.Options.Create(Limits())).LoadAsync(scenario, TestContext.Current.CancellationToken);
        var identity = new RunIdentity(scenario, "C0", "run-1", "run-1-c0-r1-loadgen", "run-1-c0-r1-loadgen", "run-1-c0-r1-loadgen");
        var registration = new ProducerRegistration(1, identity, "C0", 1, "run-1-c0-r1-loadgen", "pod", "uid", new string('a', 40));
        var roles = resolver.Resolve(loaded, registration).Configurations["C0"].Roles;
        Assert.Equal("run-1-c0-r1-consumers", roles[KafkaClientRole.EnrichmentConsumer].Settings["group.id"].Value);
        Assert.Equal("run-1-c0-r1-loadgen", roles[KafkaClientRole.LoadGenProducer].Settings["client.id"].Value);
    }
}
