using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// fault and gate are closed declarations (docs/operations/scenario-contract.md "Scenario schema"):
// only the exact snake_case kinds load, so a near-miss spelling is reported, never guessed.
[Trait("Category", "Unit")]
public sealed class ScenarioDeclarationTests
{
    [Fact]
    public async Task Given_NoGateAndNoFaults_When_Loading_Then_UsesClosedNoneDeclarations()
    {
        var scenario = await Load(ScenarioTestData.ValidScenario);

        Assert.IsType<NoGate>(scenario.Definition.Gate);
        Assert.Equal(LiveFaultKind.None, scenario.Definition.Fault.Live.Kind);
        Assert.Equal(ComponentFaultKind.None, scenario.Definition.Fault.Component.Kind);
    }

    // A probe gate's predicate measurement must also be requested in measures, which is why the
    // throttle statistic is added here alongside the gate.
    [Fact]
    public async Task Given_ProbeGateAndBothFaultKinds_When_Loading_Then_UsesTypedDeclarations()
    {
        var yaml = ScenarioTestData.ValidScenario
            .Replace("fault:\n  live: {kind: none}\n  component: {kind: none}",
                "fault:\n  live: {kind: throttling, configurations: [baseline]}\n  component: {kind: toxiproxy_latency, configurations: [baseline], direction: downstream, latency_ms: 25}",
                StringComparison.Ordinal)
            .Replace("gate: {kind: none}",
                "gate:\n  kind: probe\n  configuration: baseline\n  repeats: 5\n  predicate: {id: stats.broker.throttle.max, role: LoadGenProducer, operator: greater_than, threshold_ms: 10}\n  configurations: [baseline]",
                StringComparison.Ordinal)
            .Replace("measures:\n  - {id: verifier.ok, role: VerifierConsumer}",
                "measures:\n  - {id: verifier.ok, role: VerifierConsumer}\n  - {id: stats.broker.throttle.max, role: LoadGenProducer}",
                StringComparison.Ordinal);

        var scenario = await Load(yaml);

        var gate = Assert.IsType<ProbeGate>(scenario.Definition.Gate);
        Assert.Equal("baseline", gate.Configuration);
        Assert.Equal(new MeasurementRequest("stats.broker.throttle.max", KafkaClientRole.LoadGenProducer), gate.Measurement);
        Assert.Equal(ProbeOperator.GreaterThan, gate.Operator);
        Assert.Equal(10, gate.ThresholdMs);
        Assert.Equal(5, gate.Repeats);
        Assert.Equal(["baseline"], gate.Configurations);
        Assert.Equal(LiveFaultKind.Throttling, scenario.Definition.Fault.Live.Kind);
        Assert.Equal(["baseline"], scenario.Definition.Fault.Live.Configurations);
        Assert.Equal(ComponentFaultKind.ToxiproxyLatency, scenario.Definition.Fault.Component.Kind);
        Assert.Equal("downstream", scenario.Definition.Fault.Component.Direction);
        Assert.Equal(25, scenario.Definition.Fault.Component.LatencyMs);
    }

    // The diagnostic carries field path and YAML position so the author can find the typo.
    [Theory]
    [InlineData("fault:\n  live: {kind: Throttling, configurations: [baseline]}\n  component: {kind: none}", "unknown_fault_kind", "fault.live.kind")]
    [InlineData("fault:\n  live: {kind: none}\n  component: {kind: toxiproxy-latency, configurations: [baseline], direction: downstream, latency_ms: 25}", "unknown_fault_kind", "fault.component.kind")]
    [InlineData("gate: {kind: Probe}", "unknown_gate_kind", "gate.kind")]
    [InlineData("gate:\n  kind: probe\n  configuration: baseline\n  repeats: 5\n  predicate: {id: stats.broker.throttle.max, role: LoadGenProducer, operator: greater-than, threshold_ms: 10}\n  configurations: [baseline]", "invalid_predicate_operator", "gate.predicate.operator")]
    public async Task Given_UnknownDeclarationSpelling_When_Loading_Then_ReportsFieldDiagnostic(string replacement, string code, string fieldPath)
    {
        var original = fieldPath.StartsWith("gate.", StringComparison.Ordinal)
            ? "gate: {kind: none}"
            : "fault:\n  live: {kind: none}\n  component: {kind: none}";
        var yaml = ScenarioTestData.ValidScenario.Replace(original, replacement, StringComparison.Ordinal);

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load(yaml));

        var diagnostic = Assert.Single(exception.Diagnostics, item => item.Code == code && item.FieldPath == fieldPath);
        Assert.Equal(ScenarioTestData.ScenarioPath, diagnostic.Source);
        Assert.NotNull(diagnostic.Line);
        Assert.NotNull(diagnostic.Column);
    }

    private static ValueTask<LoadedScenario> Load(string yaml) =>
        ScenarioTestData.Loader(ScenarioTestData.Source(yaml))
            .LoadAsync("A01", TestContext.Current.CancellationToken);
}
