#pragma warning disable CA1707
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Enrichment.ConsumerProcessing;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

// Protects the production consumer plan source for C10 and C01 (the packaged scenario files, the subject and witness plans and the failure path).
public sealed class ScenarioKillPlanSourceTests
{
    public static TheoryData<string, string, int, string?, string?> SubjectCases => new()
    {
        { "C10", "C0", 110, null, null },
        { "C10", "C1", 110, "enable.auto.offset.store", "true" },
        { "C01", "C0", 1, null, null },
        { "C01", "C1", 1, "session.timeout.ms", "45000" }
    };

    private static ScenarioConsumerProcessingPlanSource Source(ConsumerProcessingOptions options, string? root = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options), root ?? AppContext.BaseDirectory);

    [Theory]
    [MemberData(nameof(SubjectCases))]
    public void Given_C10_or_C01_subject_When_asked_for_the_plan_Then_it_carries_the_kill_count_and_only_the_replaced_setting(
        string scenario, string phase, int killCount, string? key, string? value)
    {
        ConsumerProcessingPlan plan = Source(ScenarioPlanSourceTests.Options(scenario, phase, "subject")).GetPlan(scenario, phase)!;
        Assert.Equal(phase, plan.ConfigurationId);
        Assert.Equal(200, plan.ProcessingDelayMs);
        Assert.Equal(360000, plan.WindowMs);
        Assert.Null(plan.SlowMessage);
        Assert.Equal(killCount, plan.KillCount);
        Assert.False(plan.StopConfigured);
        Assert.Null(plan.ShutdownMode);
        Assert.False(plan.RecordsConsumerErrors);
        Assert.Equal(key is null ? [] : [KeyValuePair.Create(key, value!)], plan.ConsumerSettings);
    }

    [Theory]
    [MemberData(nameof(SubjectCases))]
    public void Given_C10_or_C01_witness_When_asked_for_the_plan_Then_it_has_no_kill_count_and_no_replaced_setting(
        string scenario, string phase, int killCount, string? key, string? value)
    {
        Assert.True(killCount > 0);
        Assert.Equal(key is null, value is null);
        ConsumerProcessingPlan plan = Source(ScenarioPlanSourceTests.Options(scenario, phase, "witness")).GetPlan(scenario, phase)!;
        Assert.Equal(phase, plan.ConfigurationId);
        Assert.Equal(200, plan.ProcessingDelayMs);
        Assert.Equal(360000, plan.WindowMs);
        Assert.Null(plan.SlowMessage);
        Assert.Null(plan.KillCount);
        Assert.False(plan.StopConfigured);
        Assert.Null(plan.ShutdownMode);
        Assert.False(plan.RecordsConsumerErrors);
        Assert.Empty(plan.ConsumerSettings);
    }

    [Theory]
    [InlineData("C10", "C2")]
    [InlineData("C01", "C2")]
    [InlineData("C08", "C0")]
    public void Given_a_pair_the_source_does_not_serve_When_asked_for_the_plan_Then_it_returns_null(string scenario, string phase) =>
        Assert.Null(Source(ScenarioPlanSourceTests.Options("C10", "C0", "subject")).GetPlan(scenario, phase));

    [Fact]
    public void Given_an_empty_root_When_asked_for_the_C10_plan_Then_it_returns_null()
    {
        string root = Path.Combine(Path.GetTempPath(), "empty-scenario-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { Assert.Null(Source(ScenarioPlanSourceTests.Options("C10", "C0", "subject"), root).GetPlan("C10", "C0")); }
        finally { Directory.Delete(root); }
    }

    [Theory]
    [InlineData("C10", "C0", 110)]
    [InlineData("C01", "C1", 1)]
    public void Given_a_host_configured_with_only_the_chart_environment_names_When_asked_for_the_plan_Then_it_resolves_without_any_parsing_budget(
        string scenario, string phase, int killCount)
    {
        ConsumerProcessingOptions options = ScenarioPlanSourceTests.Options(scenario, phase, "subject");
        Dictionary<string, string?> values = ConsumerProcessingHostTests.HostConfiguration(options);
        // The 31 names charts/parchwell/templates/_consumer.tpl sets; none is a ScenarioLoadOptions key.
        Assert.Equal(31, values.Count);
        Assert.DoesNotContain(values.Keys, key => key.Contains("ScenarioLoadOptions", StringComparison.Ordinal));
        using var host = Program.BuildConsumerProcessingHost(builder =>
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(values);
        });
        ConsumerProcessingPlan plan = host.Services.GetRequiredService<IConsumerProcessingPlanSource>().GetPlan(scenario, phase)!;
        Assert.Equal(phase, plan.ConfigurationId);
        Assert.Null(plan.SlowMessage);
        Assert.Equal(killCount, plan.KillCount);
        Assert.False(plan.StopConfigured);
        Assert.Null(plan.ShutdownMode);
        Assert.False(plan.RecordsConsumerErrors);
        Assert.Equal(200, plan.ProcessingDelayMs);
        Assert.Equal(360000, plan.WindowMs);
        // The E1 plan of the subject: C10 C0 and C01 C0 replace nothing; C01 C1 replaces session.timeout.ms.
        Assert.Equal(scenario == "C01" && phase == "C1" ? [KeyValuePair.Create("session.timeout.ms", "45000")] : [], plan.ConsumerSettings);
    }

    [Theory]
    [MemberData(nameof(SubjectCases))]
    public void Given_each_C10_and_C01_plan_When_applied_to_the_baseline_configuration_Then_the_worker_validation_passes(
        string scenario, string phase, int killCount, string? key, string? value)
    {
        Assert.True(killCount > 0);
        foreach (string role in new[] { "subject", "witness" })
        {
            ConsumerProcessingOptions options = ScenarioPlanSourceTests.Options(scenario, phase, role);
            var services = new ServiceCollection();
            services.AddParchwellKafka(KafkaClientRole.EnrichmentConsumer, baseline =>
            {
                baseline.NamespaceName = options.NamespaceName;
                baseline.ClientId = options.PodName;
                baseline.EnrichmentGroupId = options.GroupId;
            });
            using ServiceProvider provider = services.BuildServiceProvider();
            ConsumerConfig configuration = provider.GetRequiredService<KafkaConfigurationFactory>().CreateEnrichmentConsumer().Configuration;
            Source(options).GetPlan(scenario, phase)!.ApplyAndValidate(options, configuration);
            bool replaced = role == "subject";
            if (key == "session.timeout.ms") Assert.Equal(replaced ? 45000 : 30000, configuration.SessionTimeoutMs);
            if (key == "enable.auto.offset.store") Assert.Equal(replaced, configuration.EnableAutoOffsetStore);
            if (key is null) Assert.False(configuration.EnableAutoOffsetStore);
            Assert.Equal(value is null, key is null);
        }
    }
}
