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

// C03, C10 and C01 admission in the load generator: the pair rule is local to it (ScenarioPhases is unchanged), the committed files resolve, and the workloads spread their partitions as the plans assume.
public sealed class C03AdmissionTests
{
    private static LoadGenHostOptions HostOptions(string scenario, string phase, string configuration, string? jobName = null)
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
    [InlineData("C03", "C0")]
    [InlineData("C03", "C1")]
    [InlineData("C10", "C0")]
    [InlineData("C10", "C1")]
    [InlineData("C01", "C0")]
    [InlineData("C01", "C1")]
    public void Given_a_consumer_scenario_with_matching_phase_and_configuration_When_validating_Then_options_and_run_context_accept_it(string scenario, string phase)
    {
        Assert.Null(Record.Exception(HostOptions(scenario, phase, phase).Validate));
        Assert.Null(Record.Exception(Context(scenario, phase, phase).Validate));
    }

    [Theory]
    [InlineData("C03", "C0", "C1")]
    [InlineData("C03", "C2", "C2")]
    [InlineData("C03", "probe", "C0")]
    [InlineData("C10", "C0", "C1")]
    [InlineData("C10", "C2", "C2")]
    [InlineData("C01", "probe", "C0")]
    public void Given_an_unaccepted_pair_When_validating_Then_each_check_keeps_its_failure_name(string scenario, string phase, string configuration)
    {
        Assert.Equal("phase_configuration_pair_invalid", Assert.Throws<InvalidOperationException>(HostOptions(scenario, phase, configuration).Validate).Message);
        Assert.Equal("producer_run_context_invalid", Assert.Throws<InvalidDataException>(Context(scenario, phase, configuration).Validate).Message);
    }

    [Theory]
    [InlineData("C03")]
    [InlineData("C10")]
    [InlineData("C01")]
    public void Given_a_consumer_scenario_with_the_B10_restart_job_name_When_validating_Then_it_is_rejected_outside_B10(string scenario)
    {
        Assert.Equal("restart_instance_requires_b10",
            Assert.Throws<InvalidOperationException>(HostOptions(scenario, "C0", "C0", "child-run-c0-r1-loadgen2").Validate).Message);
        Assert.Equal("restart_instance_requires_b10",
            Assert.Throws<InvalidDataException>(Context(scenario, "C0", "C0", "run-1-c0-r1-loadgen2").Validate).Message);
    }

    [Theory]
    [InlineData("C03", "C0")]
    [InlineData("C03", "C1")]
    [InlineData("C10", "C0")]
    [InlineData("C10", "C1")]
    [InlineData("C01", "C0")]
    [InlineData("C01", "C1")]
    public async Task Given_the_LoadGen_host_registrations_When_loading_and_resolving_a_consumer_scenario_as_the_worker_does_Then_the_producer_settings_equal_the_baseline(string scenario, string configuration)
    {
        // The host is the real one: its scenario source, loader, resolver and role registrations are what a live C03 load generator uses.
        string[] args = LoadGenHostProcessTests.CreateInputs(Path.Combine(Path.GetTempPath(), "c03-" + Guid.NewGuid().ToString("N")))
            .Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
        using IHost host = Program.BuildHostCore(args, _ => { }, (_, configure) => configure(new ParchwellTelemetryOptions()));
        LoadedScenario loaded = await host.Services.GetRequiredService<ScenarioLoader>().LoadAsync(scenario, TestContext.Current.CancellationToken);
        ResolvedScenario resolved = host.Services.GetRequiredService<ScenarioResolver>().Resolve(loaded);
        // The two checks LoadGenWorker makes after resolving: the configuration is declared and has a LoadGenProducer role.
        ResolvedRoleConfiguration producer = resolved.Configurations[configuration].Roles[KafkaClientRole.LoadGenProducer];
        KafkaConfigurationFactory factory = host.Services.GetRequiredService<KafkaConfigurationFactory>();
        Assert.Equal(factory.CreateLoadGenProducer().Configuration.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            producer.Settings.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Value)).OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.DoesNotContain(producer.Settings, pair => pair.Value.Override is not null);
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

    [Fact]
    public async Task Given_the_committed_C01_profile_When_generating_Then_every_partition_receives_a_message_after_101_seconds()
    {
        var limits = Options.Create(new ScenarioLoadOptions { MaxDocumentCharacters = 100000, MaxNestingDepth = 32, MaxScalarCharacters = 10000, MaxParserEvents = 10000 });
        LoadedScenario loaded = await new ScenarioLoader(new FileSystemScenarioSource(Root()), limits).LoadAsync("C01", TestContext.Current.CancellationToken);
        var events = WorkloadGenerator.Generate(loaded.Profile, 600, 1_000_000, TestContext.Current.CancellationToken).ToList();
        Assert.Equal(600, events.Count);
        Assert.All(events, planned => Assert.StartsWith("tenant01-d", planned.Key, StringComparison.Ordinal));
        var withPartition = events.Select(planned => (Event: planned, Partition: (int)(Crc32(Encoding.UTF8.GetBytes(planned.Key)) % 32))).ToList();
        foreach (int start in new[] { 0, 16 })
        {
            var half = withPartition.Where(item => item.Partition >= start && item.Partition < start + 16).ToList();
            Assert.Equal(300, half.Count);
            Assert.Equal(16, half.Select(item => item.Partition).Distinct().Count());
        }
        (PlannedEvent first0, int partition0) = withPartition.First(item => item.Partition < 16);
        Assert.Equal((0L, 0L, 11), (first0.Ordinal, first0.DueMicroseconds, partition0));
        (PlannedEvent first1, int partition1) = withPartition.First(item => item.Partition >= 16);
        Assert.Equal((5L, 1_250_000L, 29), (first1.Ordinal, first1.DueMicroseconds, partition1));
        // The earliest last message of any partition of each half: the latest time by which that partition's last message is due.
        var lastDue = withPartition.GroupBy(item => item.Partition).ToDictionary(group => group.Key, group => group.Max(item => item.Event.DueMicroseconds));
        var low = lastDue.Where(pair => pair.Key < 16).MinBy(pair => pair.Value);
        var high = lastDue.Where(pair => pair.Key >= 16).MinBy(pair => pair.Value);
        Assert.Equal((14, 102_250_000L), (low.Key, low.Value));
        Assert.Equal((24, 101_000_000L), (high.Key, high.Value));
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
