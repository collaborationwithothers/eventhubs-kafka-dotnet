using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Repository test naming convention.
// Builds the abrupt host's real service registration and resolves the packaged B10 scenario.
// No host is started, so no telemetry export, token acquisition, Blob call or Kafka client runs.
[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptHostRegistrationTests
{
    private const string TelemetryVariable = "Telemetry__ConnectionString";
    private const string SyntheticTelemetry = "InstrumentationKey=synthetic;IngestionEndpoint=https://example.invalid";

    [Theory]
    [InlineData("parent")]
    [InlineData("child")]
    [InlineData("successor")]
    public async Task Given_AbruptHostRegistration_When_B10IsResolved_Then_EveryDeclaredRoleResolves(string role)
    {
        ResolvedScenario resolved = await ResolveAsync(role);

        KafkaClientRole[] declared = [KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer];
        Assert.Equal(declared, resolved.Definition.Roles.Order().ToArray());
        Assert.Equal(["C0", "C1"], resolved.Configurations.Keys.Order(StringComparer.Ordinal).ToArray());
        foreach (ResolvedConfiguration configuration in resolved.Configurations.Values)
            Assert.Equal(declared, configuration.Roles.Keys.Order().ToArray());

        ResolvedRoleConfiguration producer = resolved.Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer];
        Assert.Equal("synthetic-pod", producer.Settings["client.id"].Value);
        Assert.Equal("synthetic-ns.servicebus.windows.net:9093", producer.Settings["bootstrap.servers"].Value);
        Assert.Equal("true", producer.Settings["enable.idempotence"].Value);
        Assert.Equal("100", producer.Settings["message.timeout.ms"].Value);
        Assert.Equal("5", producer.Settings["max.in.flight"].Value);
        ResolvedRoleConfiguration verifier = resolved.Configurations["C1"].Roles[KafkaClientRole.VerifierConsumer];
        Assert.Equal("parchwell-verifier", verifier.Settings["group.id"].Value);
        Assert.Equal("b10-abrupt-unused-verifier", verifier.Settings["client.id"].Value);
    }

    [Fact]
    public async Task Given_ParentAndChildRegistrations_When_B10IsResolved_Then_ProducerSettingsAreEqual()
    {
        Dictionary<string, string> parent = ProducerSettings(await ResolveAsync("parent"));
        Dictionary<string, string> child = ProducerSettings(await ResolveAsync("child"));
        Assert.Equal(34, child.Count);
        Assert.Equal(child.OrderBy(pair => pair.Key, StringComparer.Ordinal), parent.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("child", "0")]
    [InlineData("successor", "5")]
    public async Task Given_ProbeRole_When_SettingsAreApplied_Then_UsesBaselineTimeoutWithScenarioIdempotence(string role, string linger)
    {
        var scenario = (await ResolveAsync(role, commonInputs: true)).Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer];
        var probe = B10AbruptHost.ProbeSettings(scenario, role == "child");
        var config = new Confluent.Kafka.ProducerConfig();
        foreach (var setting in probe.Settings) config.Set(setting.Key, setting.Value.Value);
        Assert.Equal("300000", config.Get("message.timeout.ms"));
        Assert.Equal(linger, config.Get("linger.ms"));
        Assert.Equal("true", config.Get("enable.idempotence"));
        Assert.Equal("5", config.Get("max.in.flight"));
        Assert.Null(probe.Settings["message.timeout.ms"].Override);
        Assert.Equal(scenario.Settings["message.timeout.ms"].Baseline, probe.Settings["message.timeout.ms"].Baseline);
        Assert.Equal("B10", probe.Settings["max.in.flight"].Override!.ScenarioId);
        Assert.Equal("C1", probe.Settings["max.in.flight"].Override!.ConfigurationId);
        Assert.Equal(scenario.Settings["enable.idempotence"], probe.Settings["enable.idempotence"]);
    }

    [Fact]
    public async Task Given_SuccessorSettings_When_CapabilitiesRoundTrip_Then_BaselineTimeoutHasNoOverride()
    {
        var scenario = (await ResolveAsync("successor", commonInputs: true)).Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer];
        var selected = B10AbruptHost.ProbeSettings(scenario, false);
        var expectations = Parchwell.LoadGen.Run.Evidence.RunEvidenceExpectations.CaptureOnly(selected, false);
        string root = Path.Combine(Path.GetTempPath(), "abrupt-settings-" + Guid.NewGuid().ToString("N"));
        var identity = new Parchwell.LoadGen.Run.Contracts.RunIdentity("B10", "C1", "b10d-1-1", "first", "first", "first");
        try
        {
            await using (var session = new Parchwell.LoadGen.Run.Evidence.RunEvidenceSession(identity,
                new(root, 64, 1048576, 134217728, 100, TimeSpan.FromSeconds(5)), TimeProvider.System, expectations))
                await session.CompleteAsync(null, TestContext.Current.CancellationToken);
            var result = await Parchwell.LoadGen.Run.Evidence.RunEvidenceReader.ReadAsync(root, identity, expectations, 134217728, 1048576, 100, TestContext.Current.CancellationToken);
            Assert.False(result.IsFaulted);
            string path = Path.Combine(root, "capabilities.json");
            var bytes = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!;
            var timeout = bytes["resolved_configuration"]!["settings"]!["message.timeout.ms"]!;
            Assert.Equal("300000", timeout["value"]!.GetValue<string>());
            Assert.Null(timeout["override"]);
            var inFlight = bytes["resolved_configuration"]!["settings"]!["max.in.flight"]!;
            Assert.Equal("5", inFlight["value"]!.GetValue<string>());
            Assert.Equal("B10", inFlight["override"]!["scenario_id"]!.GetValue<string>());
            Assert.Equal("C1", inFlight["override"]!["configuration_id"]!.GetValue<string>());
            timeout["override"] = System.Text.Json.JsonSerializer.SerializeToNode(scenario.Settings["message.timeout.ms"].Override,
                B10AbruptWire.JsonOptions);
            await File.WriteAllTextAsync(path, bytes.ToJsonString(), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => Parchwell.LoadGen.Run.Evidence.RunEvidenceReader.ReadAsync(root, identity, expectations,
                134217728, 1048576, 100, TestContext.Current.CancellationToken));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static Dictionary<string, string> ProducerSettings(ResolvedScenario resolved) =>
        resolved.Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer].Settings
            .ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);

    private static async Task<ResolvedScenario> ResolveAsync(string role, bool commonInputs = false)
    {
        string? previous = Environment.GetEnvironmentVariable(TelemetryVariable);
        Environment.SetEnvironmentVariable(TelemetryVariable, SyntheticTelemetry);
        try
        {
            var values = Valid(role);
            if (commonInputs)
            {
                values[B10AbruptOptions.Prefix + "RunId"] = "b10d-1-1";
                values[B10AbruptOptions.Prefix + "PodName"] = "first";
                values[B10AbruptOptions.Prefix + "JobName"] = "b10d-1-1-c1-r1-loadgen-" + (role == "successor" ? "successor" : "restart");
            }
            B10AbruptOptions options = B10AbruptOptions.Read(values);
            using IHost host = B10AbruptHost.BuildServices(options);
            LoadedScenario loaded = await host.Services.GetRequiredService<ScenarioLoader>()
                .LoadAsync("B10", TestContext.Current.CancellationToken);
            return host.Services.GetRequiredService<ScenarioResolver>().Resolve(loaded);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TelemetryVariable, previous);
        }
    }

    private static Dictionary<string, string> Valid(string role)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Enabled"] = "true", ["Purpose"] = "diagnostic_trial", ["Role"] = role,
            ["RunId"] = "synthetic", ["ScenarioId"] = "B10", ["ConfigurationId"] = "C1", ["Phase"] = "C1", ["Repeat"] = "1",
            ["SourceCommit"] = new('a', 40), ["ImageReference"] = "registry.invalid/parchwell/loadgen@sha256:" + new string('b', 64),
            ["JobName"] = "synthetic-c1-r1-loadgen-" + (role == "successor" ? "successor" : "restart"),
            ["PodName"] = "synthetic-pod", ["PodUid"] = "synthetic-uid", ["NamespaceName"] = "synthetic-ns",
            ["BlobEndpoint"] = "https://storage.invalid/", ["ScenarioSourceRoot"] = AppContext.BaseDirectory, ["OutputRoot"] = "/work/loadgen",
            [role == "successor" ? "PredecessorBindingPath" : "StartLineBindingPath"] = "/bindings/not-mounted.json",
        };
        foreach (var policy in B10AbruptOptions.Policy) values[policy.Key] = policy.Value.ToString(CultureInfo.InvariantCulture);
        return values.ToDictionary(pair => B10AbruptOptions.Prefix + pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}
#pragma warning restore CA1707
