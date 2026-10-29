using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class ProbeV2DefinitionTests
{
    [Theory]
    [InlineData("B08", 2)]
    [InlineData("E01", 2)]
    [InlineData("F06", 12)]
    public async Task Given_ProbeFile_When_LoadingAndResolving_Then_PreservesExactRowsAndBaselineDeltas(string item, int count)
    {
        var loaded = await Load(item);
        var definition = loaded.Definition;
        Assert.Null(definition.SingleAction);
        var probe = Assert.IsType<ProbeV2Definition>(definition.ProbeV2);
        Assert.Equal(count, probe.Configurations.Count);
        Assert.Equal(5, definition.Repeats);
        Assert.Equal([KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer], definition.Roles);
        Assert.IsType<NoGate>(definition.Gate);
        Assert.Equal(LiveFaultKind.None, definition.Fault.Live.Kind);
        Assert.Equal(ComponentFaultKind.None, definition.Fault.Component.Kind);
        Assert.Equal(4, definition.Measures.Length);
        foreach (var role in definition.Roles)
            Assert.Equal(["single_action.client_errors", "single_action.outcome"], definition.Measures.Where(m => m.Role == role).Select(m => m.Id).Order(StringComparer.Ordinal));
        using var services = ScenarioTestData.Provider([.. definition.Roles]);
        var factory = services.GetRequiredService<KafkaConfigurationFactory>();
        var resolved = new ScenarioResolver(factory).Resolve(loaded);
        for (int index = 0; index < count; index++)
        {
            var id = "C" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(Expected(item, index), probe.Configurations[id]);
            foreach (var role in definition.Roles)
            {
                var baseline = role == KafkaClientRole.LoadGenProducer
                    ? factory.CreateLoadGenProducer().Configuration.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
                    : factory.CreateVerifierConsumer().Configuration.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
                if (item == "B08" && id == "C1" && role == KafkaClientRole.LoadGenProducer) baseline["partitioner"] = "murmur2_random";
                Assert.Equal(baseline.OrderBy(x => x.Key, StringComparer.Ordinal),
                    resolved.Configurations[id].Roles[role].Settings.Select(x => KeyValuePair.Create(x.Key, x.Value.Value)).OrderBy(x => x.Key, StringComparer.Ordinal));
            }
            var changes = definition.Configurations[id].Overrides.SelectMany(role => role.Value.Select(setting => $"{role.Key}:{setting.Key}={setting.Value.Value}"));
            Assert.Equal(item == "B08" && id == "C1" ? ["LoadGenProducer:partitioner=murmur2_random"] : Array.Empty<string>(), changes);
        }
    }

    [Theory]
    [InlineData("action: produce_one", "action: subscribe")]
    [InlineData("role: LoadGenProducer", "role: VerifierConsumer")]
    [InlineData("service_account: loadgen", "service_account: verifier")]
    [InlineData("topic: document-events", "topic: document-state")]
    [InlineData("partition_mode: automatic", "partition_mode: explicit")]
    [InlineData("partition: null", "partition: 0")]
    [InlineData("partition: null", "partition: 'null'")]
    [InlineData("value_kind: marker", "value_kind: empty")]
    [InlineData("window_ms: 330000", "window_ms: 329999")]
    [InlineData("window_ms: 330000", "window_ms: '330000'")]
    [InlineData("window_ms: 330000", "window_ms: 2147483648")]
    [InlineData("key_rule: b08_fixed", "key_rule: e01_unique")]
    [InlineData("read_rule: delivery_location", "read_rule: seeded_location")]
    [InlineData("      read_rule: delivery_location\n", "")]
    [InlineData("      action: produce_one", "      unknown: 1\n      action: produce_one")]
    public async Task Given_ChangedRowField_When_Loading_Then_RejectsLiteral(string before, string after)
    {
        var yaml = Text("B08");
        var position = yaml.IndexOf("probe_v2:", StringComparison.Ordinal);
        var mutated = yaml[..position] + yaml[position..].Replace(before, after, StringComparison.Ordinal);
        Assert.NotEqual(yaml, mutated);
        await Reject("B08", mutated, "probe_v2_literal");
    }

    [Theory]
    [InlineData("B08")]
    [InlineData("E01")]
    [InlineData("F06")]
    public async Task Given_MissingDeclaration_When_Loading_Then_RequiresProbeV2(string item) =>
        await Reject(item, Text(item).Split("probe_v2:", StringSplitOptions.None)[0], "probe_v2_required");

    [Theory]
    [InlineData("probe_v2:\n  configurations:", "probe_v2: []\nunused:")]
    [InlineData("    C1:\n      action:", "    C2:\n      action:")]
    [InlineData("  C0: {}", "  extra: {}\n  C0: {}")]
    [InlineData("  C0: {}\n", "")]
    public async Task Given_ChangedConfigurationSet_When_Loading_Then_RejectsMatrix(string before, string after)
    {
        var original = Text("B08");
        var mutated = original.Replace(before, after, StringComparison.Ordinal);
        Assert.NotEqual(original, mutated);
        await Reject("B08", mutated, "probe_v2_configurations");
    }

    [Theory]
    [InlineData("repeats: 5", "repeats: 6")]
    [InlineData("single_action.outcome", "ledger.delivery_error_code")]
    [InlineData("  live:\n    kind: none", "  live:\n    kind: throttling\n    configurations: [C0]")]
    [InlineData("  component:\n    kind: none", "  component:\n    kind: toxiproxy_latency\n    configurations: [C0]\n    direction: downstream\n    latency_ms: 1")]
    public async Task Given_ChangedExperimentConstraint_When_Loading_Then_RejectsLiteral(string before, string after) =>
        await Reject("B08", Text("B08").Replace(before, after, StringComparison.Ordinal), "probe_v2_literal");

    [Theory]
    [InlineData("value_kind: 'null'", "value_kind: empty")]
    [InlineData("value_kind: 'null'", "value_kind: null")]
    [InlineData("value_kind: empty", "value_kind: 'null'")]
    [InlineData("partition: 0", "partition: 1")]
    public async Task Given_NullEmptyOrPartitionMutation_When_Loading_Then_RejectsConflation(string before, string after) =>
        await Reject("E01", Text("E01").Replace(before, after, StringComparison.Ordinal), "probe_v2_literal");

    [Fact]
    public async Task Given_ProbeOnLegacyItem_When_Loading_Then_RejectsUnexpectedGeneration() =>
        await Reject("A01", ScenarioTestData.ValidScenario + "\nprobe_v2: {configurations: {}}\n", "probe_v2_unexpected");

    [Fact]
    public async Task Given_MixedProbeGenerations_When_Loading_Then_RejectsUnexpectedGeneration() =>
        await Reject("B08", Text("B08") + "\nsingle_action: {configurations: {}}\n", "probe_v2_unexpected");

    [Theory]
    [InlineData("partitioner: murmur2_random", "partitioner: consistent_random")]
    [InlineData("  C0: {}", "  C0:\n    LoadGenProducer:\n      partitioner: consistent_random")]
    [InlineData("partitioner: murmur2_random", "partitioner: murmur2_random\n      message.max.bytes: 2000000")]
    public async Task Given_ExtraOrMissingDelta_When_Loading_Then_RejectsDelta(string before, string after) =>
        await Reject("B08", Text("B08").Replace(before, after, StringComparison.Ordinal), "probe_v2_deltas");

    [Fact]
    public async Task Given_ProbeGate_When_Loading_Then_RejectsGate()
    {
        var yaml = Text("B08").Replace("gate:\n  kind: none", "gate:\n  kind: probe\n  configuration: C0\n  repeats: 5\n  configurations: [C1]\n  predicate: {id: stats.broker.throttle.max, role: LoadGenProducer, operator: greater_than, threshold_ms: 1}", StringComparison.Ordinal);
        await Reject("B08", yaml, "probe_v2_literal");
    }

    private static ProbeV2Configuration Expected(string item, int index)
    {
        if (item == "B08") return new("produce_one", KafkaClientRole.LoadGenProducer, "loadgen", "document-events", "automatic", null, "marker", 330000, "b08_fixed", "delivery_location");
        if (item == "E01") return new("produce_one", KafkaClientRole.LoadGenProducer, "probe", "document-state", "explicit", 0, index == 0 ? "empty" : "null", 330000, "e01_unique", "delivery_location");
        (string Account, string Topic, bool Produce)[] matrix =
        [
            ("loadgen", "document-events", true), ("probe", "document-events", true),
            ("loadgen", "document-state", true), ("probe", "document-state", true),
            ("loadgen", "document-events-dlq", true), ("probe", "document-events-dlq", true),
            ("probe", "document-events", false), ("probe", "document-state", false), ("probe", "document-events-dlq", false),
            ("enrichment-consumer", "document-events", false), ("enrichment-consumer", "document-state", false), ("enrichment-consumer", "document-events-dlq", false)
        ];
        var row = matrix[index];
        return new(row.Produce ? "produce_one" : "assign_consume", row.Produce ? KafkaClientRole.LoadGenProducer : KafkaClientRole.VerifierConsumer,
            row.Account, row.Topic, "explicit", 0, "marker", row.Produce ? 330000 : 120000,
            row.Produce ? "f06_unique" : "f06_seed", row.Produce ? "delivery_location" : "seeded_location");
    }

    internal static string Text(string item) => Resource("Parchwell.Scenarios.Tests.ProbeV2." + item + ".scenario.yaml");

    private static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new InvalidOperationException(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static async Task<LoadedScenario> Load(string item, string? yaml = null)
    {
        var source = item == "A01" ? ScenarioTestData.Source(yaml!) :
            new InMemoryScenarioSource(($"scenarios/{item}/scenario.yaml", yaml ?? Text(item)),
                ("scenarios/profiles/saturation-ramp.yaml", Resource("Parchwell.Scenarios.Tests.A01.saturation-ramp.yaml")));
        return await ScenarioTestData.Loader(source).LoadAsync(item, TestContext.Current.CancellationToken);
    }

    private static async Task Reject(string item, string yaml, string code)
    {
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load(item, yaml));
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == code);
    }
}
