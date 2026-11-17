using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Telemetry;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.LoadGen.Run.Workload;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.

// C03 admission in the load generator: the pair rule is local to it (ScenarioPhases is unchanged), the committed files resolve, and the workload spreads its partitions as DA8 assumes.
public sealed class C03AdmissionTests
{
    private static LoadGenHostOptions Options(string scenario, string phase, string configuration, string? jobName = null)
    {
        string[] args = LoadGenHostProcessTests.CreateInputs(Path.Combine(Path.GetTempPath(), "c03-" + Guid.NewGuid().ToString("N")))
            .Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
        using IHost host = Program.BuildHostCore(args, _ => { }, (_, configure) => configure(new ParchwellTelemetryOptions()));
        LoadGenHostOptions options = host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value;
        options.ScenarioId = scenario;
        options.Phase = phase;
        options.ConfigurationId = configuration;
        options.JobName = jobName ?? $"child-run-{phase.ToLowerInvariant()}-r1-loadgen";
        return options;
    }

    private static ProducerRunContext Context(string scenario, string phase, string configuration, string? jobName = null) =>
        new(new RunIdentity(scenario, configuration, "run-1", "pod-one", "pod-one", "pod-one"), phase, 1,
            jobName ?? $"run-1-{phase.ToLowerInvariant()}-r1-loadgen", "pod-one", "pod-uid-1", new string('a', 40));

    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public void Given_C03_with_matching_phase_and_configuration_When_validating_Then_options_and_run_context_accept_it(string phase)
    {
        Assert.Null(Record.Exception(Options("C03", phase, phase).Validate));
        Assert.Null(Record.Exception(Context("C03", phase, phase).Validate));
    }

    [Theory]
    [InlineData("C03", "C0", "C1")]
    [InlineData("C03", "C2", "C2")]
    [InlineData("C03", "probe", "C0")]
    [InlineData("C10", "C0", "C0")]
    [InlineData("C01", "C0", "C0")]
    public void Given_an_unaccepted_pair_When_validating_Then_each_check_keeps_its_failure_name(string scenario, string phase, string configuration)
    {
        Assert.Equal("phase_configuration_pair_invalid", Assert.Throws<InvalidOperationException>(Options(scenario, phase, configuration).Validate).Message);
        Assert.Equal("producer_run_context_invalid", Assert.Throws<InvalidDataException>(Context(scenario, phase, configuration).Validate).Message);
    }

    [Fact]
    public void Given_C03_with_the_B10_restart_job_name_When_validating_Then_it_is_rejected_outside_B10()
    {
        Assert.Equal("restart_instance_requires_b10",
            Assert.Throws<InvalidOperationException>(Options("C03", "C0", "C0", "child-run-c0-r1-loadgen2").Validate).Message);
        Assert.Equal("restart_instance_requires_b10",
            Assert.Throws<InvalidDataException>(Context("C03", "C0", "C0", "run-1-c0-r1-loadgen2").Validate).Message);
    }

    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public async Task Given_the_committed_C03_files_When_loading_and_resolving_as_the_worker_does_Then_the_producer_settings_equal_the_baseline(string configuration)
    {
        var source = new FileSystemScenarioSource(Root());
        var limits = Options.Create(new ScenarioLoadOptions { MaxDocumentCharacters = 100000, MaxNestingDepth = 32, MaxScalarCharacters = 10000, MaxParserEvents = 10000 });
        LoadedScenario loaded = await new ScenarioLoader(source, limits).LoadAsync("C03", TestContext.Current.CancellationToken);
        var services = new ServiceCollection();
        foreach (KafkaClientRole role in Enum.GetValues<KafkaClientRole>())
            services.AddParchwellKafka(role, value => { value.NamespaceName = "unit-test"; value.ClientId = "worker-pod"; value.EnrichmentGroupId = "worker-group"; });
        using ServiceProvider provider = services.BuildServiceProvider();
        KafkaConfigurationFactory factory = provider.GetRequiredService<KafkaConfigurationFactory>();
        ResolvedScenario resolved = new ScenarioResolver(factory).Resolve(loaded);
        // The two checks LoadGenWorker makes after resolving: the configuration is declared and has a LoadGenProducer role.
        ResolvedRoleConfiguration role = resolved.Configurations[configuration].Roles[KafkaClientRole.LoadGenProducer];
        Assert.Equal(factory.CreateLoadGenProducer().Configuration.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            role.Settings.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Value)).OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.DoesNotContain(role.Settings, pair => pair.Value.Override is not null);
    }

    // CRC-32 as librdkafka's consistent partitioner uses it (reflected polynomial 0xEDB88320). Written here so the test adds no package.
    private static uint Crc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }

    [Fact]
    public void Given_the_CRC_32_helper_When_hashing_the_standard_check_string_Then_it_returns_the_check_value() =>
        Assert.Equal(0xCBF43926u, Crc32(Encoding.ASCII.GetBytes("123456789")));

    [Fact]
    public async Task Given_the_committed_C03_profile_When_generating_Then_each_half_of_the_partitions_gets_its_first_messages_by_the_86th()
    {
        var limits = Options.Create(new ScenarioLoadOptions { MaxDocumentCharacters = 100000, MaxNestingDepth = 32, MaxScalarCharacters = 10000, MaxParserEvents = 10000 });
        LoadedScenario loaded = await new ScenarioLoader(new FileSystemScenarioSource(Root()), limits).LoadAsync("C03", TestContext.Current.CancellationToken);
        var events = WorkloadGenerator.Generate(loaded.Profile, 240, 1_000_000, TestContext.Current.CancellationToken).ToList();
        Assert.Equal(240, events.Count);
        Assert.All(events, planned => Assert.Equal("tenant06", planned.TenantId));
        var counts = new int[2];
        var firsts = new List<(int Partition, int Ordinal)>[] { [], [] };
        foreach (PlannedEvent planned in events)
        {
            int partition = (int)(Crc32(Encoding.UTF8.GetBytes(planned.Key)) % 32);
            int half = partition < 16 ? 0 : 1;
            counts[half]++;
            if (!firsts[half].Any(first => first.Partition == partition)) firsts[half].Add((partition, counts[half]));
        }
        Assert.Equal([120, 120], counts);
        int[] ordinals = [1, 6, 11, 16, 21, 26, 31, 36, 41, 46, 71, 76, 81, 86];
        Assert.Equal([4, 8, 11, 7, 0, 1, 13, 14, 2, 5, 6, 3, 15, 12], firsts[0].Select(first => first.Partition));
        Assert.Equal(ordinals, firsts[0].Select(first => first.Ordinal));
        Assert.Equal([18, 30, 29, 17, 22, 23, 27, 24, 20, 19, 16, 21, 25, 26], firsts[1].Select(first => first.Partition));
        Assert.Equal(ordinals, firsts[1].Select(first => first.Ordinal));
        Assert.Equal([9, 10], Enumerable.Range(0, 16).Where(partition => firsts[0].All(first => first.Partition != partition)));
        Assert.Equal([28, 31], Enumerable.Range(16, 16).Where(partition => firsts[1].All(first => first.Partition != partition)));
    }

    private static string Root()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "scenarios", "C03", "scenario.yaml"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository_root_not_found");
    }
}
