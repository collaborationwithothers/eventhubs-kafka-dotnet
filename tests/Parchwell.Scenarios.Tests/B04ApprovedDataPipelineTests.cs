using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class B04ApprovedDataPipelineTests
{
    private static readonly string[] ExpectedMeasureIds =
    [
        "ledger.delivery_error_code", "client.fatal_error", "verifier.ok", "verifier.loss",
        "verifier.phantom", "verifier.reported_failure", "verifier.duplicate", "verifier.reorder"
    ];

    [Fact]
    public async Task Given_ApprovedB04Data_When_LoadingAndResolving_Then_UsesDeclaredConfigurationsAndBaselineDeltas()
    {
        var scenarioText = ReadResource("Parchwell.Scenarios.Tests.B04.scenario.yaml");
        var profileText = ReadResource("Parchwell.Scenarios.Tests.B04.saturation-ramp.yaml");
        var source = new InMemoryScenarioSource(("scenarios/B04/scenario.yaml", scenarioText),
            ("scenarios/profiles/saturation-ramp.yaml", profileText));

        var loaded = await ScenarioTestData.Loader(source).LoadAsync("B04", TestContext.Current.CancellationToken);

        Assert.Equal(["scenarios/B04/scenario.yaml", "scenarios/profiles/saturation-ramp.yaml"], source.RequestedPaths);
        Assert.Equal("B04", loaded.Definition.Id);
        Assert.Equal([KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer], loaded.Definition.Roles);
        Assert.Equal(["C0", "C1"], loaded.Definition.Configurations.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(LiveFaultKind.None, loaded.Definition.Fault.Live.Kind);
        Assert.Equal(ComponentFaultKind.None, loaded.Definition.Fault.Component.Kind);
        Assert.Equal(5, loaded.Definition.Repeats);
        Assert.IsType<NoGate>(loaded.Definition.Gate);
        Assert.Equal(ExpectedMeasureIds, loaded.Definition.Measures.Select(measure => measure.Id));

        using var provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer);
        var resolved = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
        var baselineProducer = ApprovedConfigurationContract.Settings(KafkaClientRole.LoadGenProducer);
        var baselineVerifier = ApprovedConfigurationContract.Settings(KafkaClientRole.VerifierConsumer);
        var c0 = resolved.Configurations["C0"];
        var c1 = resolved.Configurations["C1"];
        Assert.Equal(baselineProducer.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal),
            c0.Roles[KafkaClientRole.LoadGenProducer].Settings.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal));
        Assert.Equal(baselineVerifier.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal),
            c0.Roles[KafkaClientRole.VerifierConsumer].Settings.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal));

        var expectedC1Producer = baselineProducer.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);
        expectedC1Producer["enable.idempotence"] = "true";
        expectedC1Producer["max.in.flight"] = "5";
        Assert.Equal(expectedC1Producer,
            c1.Roles[KafkaClientRole.LoadGenProducer].Settings.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal));
        Assert.Equal(baselineVerifier.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal),
            c1.Roles[KafkaClientRole.VerifierConsumer].Settings.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Given_A01AndB04Declarations_When_ComparingDeclaredPhases_Then_MatchesExecutablePhaseTable()
    {
        var b04 = await ScenarioTestData.Loader(new InMemoryScenarioSource(
            ("scenarios/B04/scenario.yaml", ReadResource("Parchwell.Scenarios.Tests.B04.scenario.yaml")),
            ("scenarios/profiles/saturation-ramp.yaml", ReadResource("Parchwell.Scenarios.Tests.B04.saturation-ramp.yaml"))))
            .LoadAsync("B04", TestContext.Current.CancellationToken);
        foreach (var phase in b04.Definition.Configurations.Keys)
        {
            Assert.True(ScenarioPhases.TryGetConfiguration("B04", phase, out var configuration));
            Assert.Equal(phase, configuration);
        }

        var a01 = await ScenarioTestData.Loader(new InMemoryScenarioSource(
            ("scenarios/A01/scenario.yaml", ReadResource("Parchwell.Scenarios.Tests.A01.scenario.yaml")),
            ("scenarios/profiles/saturation-ramp.yaml", ReadResource("Parchwell.Scenarios.Tests.A01.saturation-ramp.yaml"))))
            .LoadAsync("A01", TestContext.Current.CancellationToken);
        Assert.Equal(["C0", "C3", "probe"], a01.Definition.Configurations.Keys.Append("probe").Order(StringComparer.Ordinal));
        foreach (var phase in a01.Definition.Configurations.Keys.Append("probe"))
            Assert.True(ScenarioPhases.TryGetConfiguration("A01", phase, out _));
    }

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded scenario test resource '{name}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
