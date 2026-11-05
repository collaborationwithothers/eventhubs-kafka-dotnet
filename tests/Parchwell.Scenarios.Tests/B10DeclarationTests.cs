using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class B10DeclarationTests
{
    private static readonly string[] Measures = new[] { "client.fatal_error/LoadGenProducer", "ledger.delivery_error_code/LoadGenProducer", "verifier.duplicate/VerifierConsumer", "verifier.loss/VerifierConsumer", "verifier.ok/VerifierConsumer", "verifier.phantom/VerifierConsumer", "verifier.reorder/VerifierConsumer", "verifier.reported_failure/VerifierConsumer" };
    [Fact]
    public async Task Given_CommittedB10_When_LoadingAndResolving_Then_PreservesDeclarationAndExactProducerOverrides()
    {
        LoadedScenario loaded = await LoadAsync();
        ScenarioDefinition definition = loaded.Definition;
        Assert.Equal(1, definition.SchemaVersion);
        Assert.Equal("B10", definition.Id);
        Assert.Equal("Parchwell.Kafka", definition.Baseline);
        Assert.Equal("profiles/b10-timeout.yaml", definition.Profile);
        Assert.Equal([KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer], definition.Roles);
        Assert.Equal(5, definition.Repeats);
        Assert.IsType<NoGate>(definition.Gate);
        Assert.Equal(LiveFaultKind.None, definition.Fault.Live.Kind);
        Assert.Equal(ComponentFaultKind.None, definition.Fault.Component.Kind);
        Assert.Null(definition.SingleAction);
        Assert.Null(definition.ProbeV2);
        Assert.Null(definition.IdleObservation);
        Assert.Equal(["C0", "C1"], definition.Configurations.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["max.in.flight", "message.timeout.ms"], definition.Configurations["C0"].Overrides[KafkaClientRole.LoadGenProducer].Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["enable.idempotence", "max.in.flight", "message.timeout.ms"], definition.Configurations["C1"].Overrides[KafkaClientRole.LoadGenProducer].Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Measures,
            definition.Measures.Select(measure => measure.Id + "/" + measure.Role).Order(StringComparer.Ordinal));
        using var services = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer);
        KafkaConfigurationFactory factory = services.GetRequiredService<KafkaConfigurationFactory>();
        ResolvedScenario resolved = new ScenarioResolver(factory).Resolve(loaded);
        foreach (string configuration in new[] { "C0", "C1" })
        {
            Assert.True(ScenarioPhases.TryGetConfiguration("B10", configuration, out string? phaseConfiguration));
            Assert.Equal(configuration, phaseConfiguration);
            var producer = resolved.Configurations[configuration].Roles[KafkaClientRole.LoadGenProducer].Settings;
            Assert.Equal("100", producer["message.timeout.ms"].Value);
            Assert.Equal("5", producer["max.in.flight"].Value);
            Assert.NotNull(producer["message.timeout.ms"].Override);
            Assert.NotNull(producer["max.in.flight"].Override);
            Assert.Equal(configuration == "C0" ? "false" : "true", producer["enable.idempotence"].Value);
            Assert.Equal(configuration == "C1", producer["enable.idempotence"].Override is not null);
            var expected = factory.CreateLoadGenProducer().Configuration.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            expected["message.timeout.ms"] = "100";
            expected["max.in.flight"] = "5";
            if (configuration == "C1") expected["enable.idempotence"] = "true";
            Assert.Equal(expected.OrderBy(entry => entry.Key, StringComparer.Ordinal), producer.Select(entry => KeyValuePair.Create(entry.Key, entry.Value.Value)).OrderBy(entry => entry.Key, StringComparer.Ordinal));
            Assert.Equal(factory.CreateVerifierConsumer().Configuration.OrderBy(entry => entry.Key, StringComparer.Ordinal),
                resolved.Configurations[configuration].Roles[KafkaClientRole.VerifierConsumer].Settings.Select(entry => KeyValuePair.Create(entry.Key, entry.Value.Value)).OrderBy(entry => entry.Key, StringComparer.Ordinal));
        }
        Assert.Equal("b10-timeout", loaded.Profile.Id);
        Assert.Equal(1401, loaded.Profile.Seed);
        Assert.Equal(40, loaded.Profile.Stages.Sum(stage => stage.DurationSeconds));
        Assert.Equal(31000, loaded.Profile.Stages.Sum(stage => (long)stage.DurationSeconds * stage.OfferedAttemptsPerSecond));
        string original = File.ReadAllText(Path.Combine(Root(), "scenarios/profiles/saturation-ramp.yaml"));
        Assert.Equal(original.Replace("id: saturation-ramp", "id: b10-timeout", StringComparison.Ordinal).Replace("duration_seconds: 60", "duration_seconds: 10", StringComparison.Ordinal), Profile());
    }

    [Theory]
    [InlineData("enable.idempotence")]
    [InlineData("message.timeout.ms")]
    [InlineData("max.in.flight")]
    public async Task Given_B10OverrideOnVerifier_When_Loading_Then_RejectsRole(string key)
    {
        string yaml = Scenario().Replace("  C0:\n    LoadGenProducer:\n      message.timeout.ms: 100\n      max.in.flight: 5", $"  C0:\n    VerifierConsumer:\n      {key}: {(key == "enable.idempotence" ? "true" : "100")}", StringComparison.Ordinal);
        LoadedScenario loaded = await LoadAsync(yaml);
        using var services = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer);
        var error = Assert.Throws<ScenarioValidationException>(() => new ScenarioResolver(services.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded));
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == "unsupported_setting");
    }

    private static Task<LoadedScenario> LoadAsync(string? yaml = null) => ScenarioTestData.Loader(new InMemoryScenarioSource(
        ("scenarios/B10/scenario.yaml", yaml ?? Scenario()), ("scenarios/profiles/b10-timeout.yaml", Profile())))
        .LoadAsync("B10", TestContext.Current.CancellationToken).AsTask();

    private static string Scenario() => File.ReadAllText(Path.Combine(Root(), "scenarios/B10/scenario.yaml"));
    private static string Profile() => File.ReadAllText(Path.Combine(Root(), "scenarios/profiles/b10-timeout.yaml"));
    private static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Parchwell.slnx"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("repository_root");
        return root;
    }
}
#pragma warning restore CA1707
