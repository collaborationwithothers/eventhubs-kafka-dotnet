#pragma warning disable CA1707
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Enrichment.ConsumerProcessing;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

// Protects the production consumer plan source for C03 (the packaged scenario files, the subject and witness plans and the failure path).
public sealed class ScenarioPlanSourceTests
{
    private static readonly string[] PollSetting = ["max.poll.interval.ms"];

    internal static ConsumerProcessingOptions Options(string scenario, string phase, string role)
    {
        string job = $"run1-{phase.ToLowerInvariant()}-r2-consumer-{role}";
        return new ConsumerProcessingOptions
        {
            RunId = "run1", ScenarioId = scenario, Phase = phase, RepeatNumber = 2, Role = role,
            JobName = job, PodName = job + "-abcde", PodUid = "11111111-2222-3333-4444-555555555555",
            NodeName = "synthetic-node", SourceCommit = new string('a', 40), NamespaceName = "synthetic-eh",
            BlobEndpoint = "https://synthetic.blob.core.windows.net"
        };
    }

    private static ScenarioConsumerProcessingPlanSource Source(ConsumerProcessingOptions options, string? root = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options), root ?? AppContext.BaseDirectory);

    [Fact]
    public void Given_C03_C0_subject_When_asked_for_the_plan_Then_it_carries_the_inline_slow_message_and_the_scaled_poll_limit()
    {
        ConsumerProcessingPlan plan = Source(Options("C03", "C0", "subject")).GetPlan("C03", "C0")!;
        Assert.Equal("C0", plan.ConfigurationId);
        Assert.Equal(200, plan.ProcessingDelayMs);
        Assert.Equal(360000, plan.WindowMs);
        Assert.Equal(new ConsumerSlowMessage(100, 90000, "inline"), plan.SlowMessage);
        Assert.Null(plan.KillCount);
        Assert.Equal([KeyValuePair.Create("max.poll.interval.ms", "60000")], plan.ConsumerSettings);
    }

    [Theory]
    [InlineData("C1", "subject", "pause")]
    [InlineData("C0", "witness", null)]
    [InlineData("C1", "witness", null)]
    public void Given_other_members_and_phases_When_asked_for_the_plan_Then_the_witness_has_no_slow_message_and_no_replaced_setting(string phase, string role, string? mode)
    {
        ConsumerProcessingPlan plan = Source(Options("C03", phase, role)).GetPlan("C03", phase)!;
        Assert.Equal(phase, plan.ConfigurationId);
        Assert.Equal(200, plan.ProcessingDelayMs);
        Assert.Equal(360000, plan.WindowMs);
        Assert.Equal(mode is null ? null : new ConsumerSlowMessage(100, 90000, mode), plan.SlowMessage);
        Assert.Null(plan.KillCount);
        if (role == "subject") Assert.Equal([KeyValuePair.Create(PollSetting[0], "60000")], plan.ConsumerSettings);
        else Assert.Empty(plan.ConsumerSettings);
    }

    [Theory]
    [InlineData("C10", "C0")]
    [InlineData("C01", "C0")]
    [InlineData("C03", "C2")]
    public void Given_a_pair_that_is_not_C03_C0_or_C1_When_asked_for_the_plan_Then_it_returns_null(string scenario, string phase) =>
        Assert.Null(Source(Options("C03", "C0", "subject")).GetPlan(scenario, phase));

    [Fact]
    public void Given_a_root_without_the_C03_files_When_asked_for_the_plan_Then_it_returns_null()
    {
        string root = Path.Combine(Path.GetTempPath(), "empty-scenario-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { Assert.Null(Source(Options("C03", "C0", "subject"), root).GetPlan("C03", "C0")); }
        finally { Directory.Delete(root); }
    }

    [Theory]
    [InlineData("C10")]
    [InlineData("C01")]
    public async Task Given_C10_or_C01_with_phase_C0_When_the_worker_runs_with_the_production_source_Then_it_exits_1_with_consumer_plan_invalid(string scenario)
    {
        using var harness = new ConsumerTestHarness();
        ConsumerProcessingOptions options = Options(scenario, "C0", "subject");
        var worker = new ConsumerProcessingWorker(Microsoft.Extensions.Options.Options.Create(options), Source(options),
            () => throw new InvalidOperationException("baseline_not_expected"), () => throw new InvalidOperationException("store_not_expected"),
            (_, _) => throw new InvalidOperationException("consumer_not_expected"), TimeProvider.System, _ => { }, _ => (0, 0), harness.Error);
        Assert.Equal(1, await worker.ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.StartsWith("consumer_processing_failed reason=consumer_plan_invalid ", harness.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_a_host_without_a_configure_registration_When_resolving_the_plan_source_Then_it_is_the_production_source_and_a_configured_one_wins()
    {
        ConsumerProcessingOptions options = Options("C03", "C0", "subject");
        using var production = Program.BuildConsumerProcessingHost(builder => Configure(builder, options));
        Assert.IsType<ScenarioConsumerProcessingPlanSource>(production.Services.GetRequiredService<IConsumerProcessingPlanSource>());
        var configured = new ConsumerTestHarness.PlanSource(new ConsumerProcessingPlan { ConfigurationId = "C0", WindowMs = 1000 });
        using var test = Program.BuildConsumerProcessingHost(builder =>
        {
            Configure(builder, options);
            builder.Services.AddSingleton<IConsumerProcessingPlanSource>(configured);
        });
        Assert.Same(configured, test.Services.GetRequiredService<IConsumerProcessingPlanSource>());
    }

    [Fact]
    public void Given_a_host_configured_with_only_the_chart_environment_names_When_asked_for_the_C03_plan_Then_it_resolves_without_any_parsing_budget()
    {
        ConsumerProcessingOptions options = Options("C03", "C0", "subject");
        Dictionary<string, string?> values = ConsumerProcessingHostTests.HostConfiguration(options);
        // The 31 names charts/parchwell/templates/_consumer.tpl lines 74 to 120 set: nine NativeMonitoring, five Telemetry, five PARCHWELL_ and twelve ConsumerProcessing.
        Assert.Equal(31, values.Count);
        Assert.Equal([9, 5, 5, 12], new[] { "NativeMonitoring:", "Telemetry:", "PARCHWELL_", "ConsumerProcessing:" }
            .Select(prefix => values.Keys.Count(key => key.StartsWith(prefix, StringComparison.Ordinal))));
        Assert.DoesNotContain(values.Keys, key => key.Contains("ScenarioLoadOptions", StringComparison.Ordinal));
        using var host = Program.BuildConsumerProcessingHost(builder =>
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(values);
        });
        ConsumerProcessingPlan plan = host.Services.GetRequiredService<IConsumerProcessingPlanSource>().GetPlan("C03", "C0")!;
        Assert.Equal(new ConsumerSlowMessage(100, 90000, "inline"), plan.SlowMessage);
        Assert.Equal(200, plan.ProcessingDelayMs);
        Assert.Equal(360000, plan.WindowMs);
    }

    [Theory]
    [InlineData("C0", "subject")]
    [InlineData("C1", "subject")]
    [InlineData("C0", "witness")]
    [InlineData("C1", "witness")]
    public void Given_each_C03_plan_When_applied_to_the_baseline_configuration_Then_the_worker_validation_passes(string phase, string role)
    {
        ConsumerProcessingOptions options = Options("C03", phase, role);
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.EnrichmentConsumer, baseline =>
        {
            baseline.NamespaceName = options.NamespaceName;
            baseline.ClientId = options.PodName;
            baseline.EnrichmentGroupId = options.GroupId;
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        ConsumerConfig configuration = provider.GetRequiredService<KafkaConfigurationFactory>().CreateEnrichmentConsumer().Configuration;
        ConsumerProcessingPlan plan = Source(options).GetPlan("C03", phase)!;
        plan.ApplyAndValidate(options, configuration);
        Assert.Equal(role == "subject" ? 60000 : 300000, configuration.MaxPollIntervalMs);
    }

    private static void Configure(HostApplicationBuilder builder, ConsumerProcessingOptions options)
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(ConsumerProcessingHostTests.HostConfiguration(options));
    }
}
