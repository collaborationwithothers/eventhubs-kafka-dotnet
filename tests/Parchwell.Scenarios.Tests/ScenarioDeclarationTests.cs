using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

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
