using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Protects the committed B06 and B07 ledger-run declarations, their two statistics measures and the signed baseline check (issue #357).
[Trait("Category", "Unit")]
public sealed class B06B07DeclarationTests
{
    private static readonly string[] Measures =
    [
        "client.fatal_error/LoadGenProducer", "ledger.delivery_error_code/LoadGenProducer", "ledger.delivery_latency_ms/LoadGenProducer",
        "stats.broker.req.produce/LoadGenProducer", "stats.topic.batchcnt/LoadGenProducer", "verifier.duplicate/VerifierConsumer",
        "verifier.loss/VerifierConsumer", "verifier.ok/VerifierConsumer", "verifier.phantom/VerifierConsumer",
        "verifier.reorder/VerifierConsumer", "verifier.reported_failure/VerifierConsumer",
    ];

    [Theory]
    [InlineData("B06", "acks", "1")]
    [InlineData("B07", "linger.ms", "0")]
    public async Task Given_CommittedLedgerFile_When_LoadingAndResolving_Then_C0IsBaselineAndC1ChangesOneKey(string item, string key, string value)
    {
        var loaded = await Load(item);
        var definition = loaded.Definition;
        Assert.Equal([KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer], definition.Roles);
        Assert.Equal(5, definition.Repeats);
        Assert.IsType<NoGate>(definition.Gate);
        Assert.Equal(LiveFaultKind.None, definition.Fault.Live.Kind);
        Assert.Equal(ComponentFaultKind.None, definition.Fault.Component.Kind);
        Assert.Null(definition.SingleAction);
        Assert.Null(definition.ProbeV2);
        Assert.Null(definition.IdleObservation);
        Assert.Equal(Measures, definition.Measures.Select(m => m.Id + "/" + m.Role).Order(StringComparer.Ordinal));
        Assert.Equal(["C0", "C1"], definition.Configurations.Keys.Order(StringComparer.Ordinal));
        using var services = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer);
        var factory = services.GetRequiredService<KafkaConfigurationFactory>();
        var resolved = new ScenarioResolver(factory).Resolve(loaded);
        var expected = factory.CreateLoadGenProducer().Configuration.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        Assert.Equal(expected.OrderBy(x => x.Key, StringComparer.Ordinal), Settings(resolved, "C0"));
        expected[key] = value;
        Assert.Equal(expected.OrderBy(x => x.Key, StringComparer.Ordinal), Settings(resolved, "C1"));
    }

    [Theory]
    [InlineData("stats.broker.req.produce", "count", "broker_cumulative")]
    [InlineData("stats.topic.batchcnt", "messages", "topic_window")]
    public void Given_ThroughputMeasure_When_ReadingCatalogue_Then_ExposesProducerOnlyStatisticsEntry(string id, string unit, string scope)
    {
        Assert.True(MeasurementCatalog.TryGet(id, out var descriptor));
        Assert.Equal("statistics", descriptor.Source);
        Assert.Equal("count", descriptor.ValueKind);
        Assert.Equal(unit, descriptor.Unit);
        Assert.Equal(scope, descriptor.Scope);
        Assert.Equal([KafkaClientRole.LoadGenProducer], descriptor.Roles);
        Assert.Equal("https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md", descriptor.SourceUrl);
        Assert.Equal("v2.16.0", descriptor.VersionOrDate);
    }

    [Theory]
    [InlineData("B06")]
    [InlineData("B07")]
    public async Task Given_ThroughputMeasureOnVerifierRole_When_Loading_Then_RejectsRoleMismatch(string item)
    {
        var yaml = Text(item).Replace("{id: stats.topic.batchcnt, role: LoadGenProducer}", "{id: stats.topic.batchcnt, role: VerifierConsumer}", StringComparison.Ordinal);
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load(item, yaml));
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == "measurement_role_mismatch");
    }

    [Theory]
    [InlineData("B06")]
    [InlineData("B07")]
    public async Task Given_SingleActionOnLedgerItem_When_Loading_Then_RejectsUnexpectedDeclaration(string item)
    {
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load(item, Text(item) + "\nsingle_action: {configurations: {}}\n"));
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == "single_action_unexpected");
    }

    [Theory]
    [InlineData("-1", true)]
    [InlineData("5", true)]
    [InlineData("+5", true)]
    [InlineData("2147483647", true)]
    [InlineData("-2147483648", true)]
    [InlineData("2147483648", false)]
    [InlineData("-2147483649", false)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    [InlineData("true", false)]
    [InlineData("1.5", false)]
    [InlineData(" 5", false)]
    [InlineData("--1", false)]
    public void Given_BaselineText_When_CheckingIntegerBaseline_Then_AcceptsOnlySignedInt32(string value, bool expected) =>
        Assert.Equal(expected, ScenarioResolver.IsIntegerBaseline(value));

    private static IEnumerable<KeyValuePair<string, string>> Settings(ResolvedScenario resolved, string configuration) =>
        resolved.Configurations[configuration].Roles[KafkaClientRole.LoadGenProducer].Settings
            .Select(x => KeyValuePair.Create(x.Key, x.Value.Value)).OrderBy(x => x.Key, StringComparer.Ordinal);

    private static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Parchwell.slnx"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("repository_root");
        return root;
    }

    private static string Text(string item) => File.ReadAllText(Path.Combine(Root(), "scenarios", item, "scenario.yaml"));

    private static async Task<LoadedScenario> Load(string item, string? yaml = null)
    {
        var source = new InMemoryScenarioSource(($"scenarios/{item}/scenario.yaml", yaml ?? Text(item)),
            ("scenarios/profiles/saturation-ramp.yaml", File.ReadAllText(Path.Combine(Root(), "scenarios", "profiles", "saturation-ramp.yaml"))));
        return await ScenarioTestData.Loader(source).LoadAsync(item, TestContext.Current.CancellationToken);
    }
}
