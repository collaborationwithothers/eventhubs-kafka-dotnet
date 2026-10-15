using System.Reflection;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class A01ApprovedDataPipelineTests
{
    private static readonly string[] ExpectedMeasureIds =
    [
        "ledger.delivery_error_code", "ledger.delivery_latency_ms", "stats.broker.throttle.max",
        "stats.broker.req_timeouts", "stats.broker.disconnects", "stats.broker.rtt.p99",
        "producer.request_timeout", "verifier.ok", "verifier.loss", "verifier.phantom",
        "verifier.reported_failure", "verifier.duplicate", "verifier.reorder"
    ];

    // ADR-014 keeps expected outcomes out of deployment inputs, so only the two approved run-plan files enter this complete pipeline.
    [Fact]
    public async Task Given_ApprovedA01Data_When_LoadingResolvingAndGenerating_Then_UsesOnlyDefinitionAndProfileAndEmitsEveryConfiguration()
    {
        var scenarioText = ReadResource("Parchwell.Scenarios.Tests.A01.scenario.yaml");
        var profileText = ReadResource("Parchwell.Scenarios.Tests.A01.saturation-ramp.yaml");
        var source = new InMemoryScenarioSource(
            ("scenarios/A01/scenario.yaml", scenarioText),
            ("scenarios/profiles/saturation-ramp.yaml", profileText));

        var loaded = await ScenarioTestData.Loader(source).LoadAsync("A01", TestContext.Current.CancellationToken);

        Assert.Equal(["scenarios/A01/scenario.yaml", "scenarios/profiles/saturation-ramp.yaml"], source.RequestedPaths);
        Assert.Equal("A01", loaded.Definition.Id);
        Assert.Equal("Parchwell.Kafka", loaded.Definition.Baseline);
        Assert.Equal([KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer], loaded.Definition.Roles);
        Assert.Equal("profiles/saturation-ramp.yaml", loaded.Definition.Profile);
        Assert.Equal(["C0", "C2", "C3"], loaded.Definition.Configurations.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(LiveFaultKind.Throttling, loaded.Definition.Fault.Live.Kind);
        Assert.Equal(["C0", "C2", "C3"], loaded.Definition.Fault.Live.Configurations);
        Assert.Equal(ComponentFaultKind.ToxiproxyLatency, loaded.Definition.Fault.Component.Kind);
        Assert.Equal(["C3"], loaded.Definition.Fault.Component.Configurations);
        Assert.Equal("downstream", loaded.Definition.Fault.Component.Direction);
        Assert.Equal(25_000, loaded.Definition.Fault.Component.LatencyMs);
        Assert.Equal(5, loaded.Definition.Repeats);
        var gate = Assert.IsType<ProbeGate>(loaded.Definition.Gate);
        Assert.Equal("C0", gate.Configuration);
        Assert.Equal(5, gate.Repeats);
        Assert.Equal("stats.broker.throttle.max", gate.Measurement.Id);
        Assert.Equal(ProbeOperator.GreaterThan, gate.Operator);
        Assert.Equal(20_000, gate.ThresholdMs);
        Assert.Equal(ExpectedMeasureIds, loaded.Definition.Measures.Select(measure => measure.Id));

        Assert.Equal("saturation-ramp", loaded.Profile.Id);
        Assert.Equal(1, loaded.Profile.SchemaVersion);
        Assert.Equal(1401, loaded.Profile.Seed);
        Assert.Equal(2_000, loaded.Profile.DocumentsPerBurst);
        Assert.Equal(new[]
        {
            new TenantWeight("tenant01", 40), new TenantWeight("tenant02", 20),
            new TenantWeight("tenant03", 10), new TenantWeight("tenant04", 8),
            new TenantWeight("tenant05", 6), new TenantWeight("tenant06", 5),
            new TenantWeight("tenant07", 4), new TenantWeight("tenant08", 3),
            new TenantWeight("tenant09", 2), new TenantWeight("tenant10", 2)
        }, loaded.Profile.Tenants);
        Assert.Equal(new[]
        {
            new LoadStage("ramp100", 100, 60), new LoadStage("ramp300", 300, 60),
            new LoadStage("ramp900", 900, 60), new LoadStage("ramp1800", 1_800, 60)
        }.Select(stage => (stage.Id, stage.OfferedAttemptsPerSecond, stage.DurationSeconds)),
            loaded.Profile.Stages.Select(stage => (stage.Id, stage.OfferedAttemptsPerSecond, stage.DurationSeconds)));
        var expectedPayloads = new Dictionary<string, (int Size, int Weight)[]>(StringComparer.Ordinal)
        {
            ["uploaded"] = [(1_024, 1)],
            ["ocr-started"] = [(1_024, 1)],
            ["ocr-finished"] = [(150_000, 99), (900_000, 1)],
            ["classified"] = [(1_024, 1)],
            ["indexed"] = [(1_024, 1)]
        };
        Assert.Equal(expectedPayloads.Keys.Order(StringComparer.Ordinal), loaded.Profile.Payloads.Keys.Order(StringComparer.Ordinal));
        foreach (var (eventId, expectedBuckets) in expectedPayloads)
            Assert.Equal(expectedBuckets, loaded.Profile.Payloads[eventId].Select(bucket => (bucket.SizeBytes, bucket.Weight)));

        using var provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer);
        var resolved = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
        var expectedProducer = ApprovedConfigurationContract.Settings(KafkaClientRole.LoadGenProducer);
        var expectedVerifier = ApprovedConfigurationContract.Settings(KafkaClientRole.VerifierConsumer);
        foreach (var configurationId in new[] { "C0", "C2", "C3" })
        {
            var configuration = resolved.Configurations[configurationId];
            var producer = configuration.Roles[KafkaClientRole.LoadGenProducer].Settings;
            var verifier = configuration.Roles[KafkaClientRole.VerifierConsumer].Settings;
            Assert.Equal(ApprovedValues(expectedProducer, configurationId, applyA01Overrides: true),
                producer.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal));
            Assert.Equal(ApprovedValues(expectedVerifier, configurationId, applyA01Overrides: false),
                verifier.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal));

            var generated = new HelmValuesGenerator().Generate(resolved, configurationId);
            AssertGeneratedValues(generated, configurationId,
                ApprovedValues(expectedProducer, configurationId, applyA01Overrides: true),
                ApprovedValues(expectedVerifier, configurationId, applyA01Overrides: false), loaded.Profile);
        }

        Assert.Equal(["scenarios/A01/scenario.yaml", "scenarios/profiles/saturation-ramp.yaml"], source.RequestedPaths);
        Assert.Equal("5000", resolved.Configurations["C2"].Roles[KafkaClientRole.LoadGenProducer].Settings["request.timeout.ms"].Value);
        Assert.Equal("20000", resolved.Configurations["C3"].Roles[KafkaClientRole.LoadGenProducer].Settings["socket.timeout.ms"].Value);
        Assert.Equal("60000", resolved.Configurations["C2"].Roles[KafkaClientRole.LoadGenProducer].Settings["socket.timeout.ms"].Value);
        Assert.Equal("60000", resolved.Configurations["C3"].Roles[KafkaClientRole.LoadGenProducer].Settings["request.timeout.ms"].Value);
    }

    private static Dictionary<string, string> ApprovedValues(
        IReadOnlyDictionary<string, ApprovedConfigurationContract.Setting> baseline, string configurationId,
        bool applyA01Overrides)
    {
        var expected = baseline.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);
        if (applyA01Overrides && configurationId == "C2") expected["request.timeout.ms"] = "5000";
        if (applyA01Overrides && configurationId == "C3") expected["socket.timeout.ms"] = "20000";
        return expected;
    }

    private static void AssertGeneratedValues(string yaml, string configurationId,
        IReadOnlyDictionary<string, string> expectedProducer, IReadOnlyDictionary<string, string> expectedVerifier,
        WorkloadProfile expectedProfile)
    {
        using var reader = new StringReader(yaml);
        var stream = new YamlStream();
        stream.Load(reader);
        var root = Mapping(stream.Documents[0].RootNode);
        Assert.Equal("1", Scalar(root, "contract_version"));
        Assert.Equal("A01", Scalar(root, "scenario_id"));
        Assert.Equal(configurationId, Scalar(root, "configuration_id"));
        Assert.DoesNotContain("expected.yaml", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("assessment_rules", yaml, StringComparison.Ordinal);

        var profile = Mapping(root.Children[new YamlScalarNode("profile")]);
        Assert.Equal("1", Scalar(profile, "schema_version"));
        Assert.Equal("saturation-ramp", Scalar(profile, "id"));
        Assert.Equal(expectedProfile.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), Scalar(profile, "schema_version"));
        Assert.Equal(expectedProfile.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture), Scalar(profile, "seed"));
        Assert.Equal(expectedProfile.DocumentsPerBurst.ToString(System.Globalization.CultureInfo.InvariantCulture), Scalar(profile, "documents_per_burst"));
        var tenants = Sequence(profile, "tenants");
        Assert.Equal(expectedProfile.Tenants.Select(tenant => (tenant.Id, tenant.Weight.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            tenants.Children.Select(tenant => (Scalar(Mapping(tenant), "id"), Scalar(Mapping(tenant), "weight"))));
        var stages = Sequence(profile, "stages");
        Assert.Equal(expectedProfile.Stages.Select(stage => (stage.Id,
                stage.DurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                stage.OfferedAttemptsPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            stages.Children.Select(stage => (Scalar(Mapping(stage), "id"), Scalar(Mapping(stage), "duration_seconds"),
                Scalar(Mapping(stage), "offered_attempts_per_second"))));
        var payloads = Mapping(profile.Children[new YamlScalarNode("payloads")]);
        Assert.Equal(expectedProfile.Payloads.Keys.Order(StringComparer.Ordinal),
            payloads.Children.Keys.Cast<YamlScalarNode>().Select(key => key.Value));
        foreach (var (eventId, expectedBuckets) in expectedProfile.Payloads)
        {
            var actualBuckets = Sequence(payloads, eventId).Children;
            Assert.Equal(expectedBuckets.Select(bucket => (bucket.SizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    bucket.Weight.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                actualBuckets.Select(bucket => (Scalar(Mapping(bucket), "size_bytes"), Scalar(Mapping(bucket), "weight"))));
        }

        var kafka = Mapping(root.Children[new YamlScalarNode("kafka")]);
        var producer = ReadSettings(Mapping(kafka.Children[new YamlScalarNode(KafkaClientRole.LoadGenProducer.ToString())]));
        var verifier = ReadSettings(Mapping(kafka.Children[new YamlScalarNode(KafkaClientRole.VerifierConsumer.ToString())]));
        Assert.Equal(expectedProducer, producer);
        Assert.Equal(expectedVerifier, verifier);
    }

    private static Dictionary<string, string> ReadSettings(YamlMappingNode mapping) =>
        mapping.Children.ToDictionary(pair => ((YamlScalarNode)pair.Key).Value!, pair => ((YamlScalarNode)pair.Value).Value!, StringComparer.Ordinal);

    private static YamlMappingNode Mapping(YamlNode node) => Assert.IsType<YamlMappingNode>(node);

    private static YamlSequenceNode Sequence(YamlMappingNode mapping, string key) =>
        Assert.IsType<YamlSequenceNode>(mapping.Children[new YamlScalarNode(key)]);

    private static string Scalar(YamlMappingNode mapping, string key) =>
        Assert.IsType<YamlScalarNode>(mapping.Children[new YamlScalarNode(key)]).Value!;

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded scenario test resource '{name}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
