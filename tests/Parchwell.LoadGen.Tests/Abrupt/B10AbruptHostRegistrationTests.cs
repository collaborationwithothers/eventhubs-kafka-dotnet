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
    [InlineData("supervisor")]
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
    public async Task Given_SupervisorAndChildRegistrations_When_B10IsResolved_Then_ProducerSettingsAreEqual()
    {
        Dictionary<string, string> supervisor = ProducerSettings(await ResolveAsync("supervisor"));
        Dictionary<string, string> child = ProducerSettings(await ResolveAsync("child"));
        Assert.Equal(34, child.Count);
        Assert.Equal(child.OrderBy(pair => pair.Key, StringComparer.Ordinal), supervisor.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    private static Dictionary<string, string> ProducerSettings(ResolvedScenario resolved) =>
        resolved.Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer].Settings
            .ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);

    private static async Task<ResolvedScenario> ResolveAsync(string role)
    {
        string? previous = Environment.GetEnvironmentVariable(TelemetryVariable);
        Environment.SetEnvironmentVariable(TelemetryVariable, SyntheticTelemetry);
        try
        {
            B10AbruptOptions options = B10AbruptOptions.Read(Valid(role));
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
            ["JobName"] = "synthetic-c1-r1-loadgen" + (role == "successor" ? "2" : ""),
            ["PodName"] = "synthetic-pod", ["PodUid"] = "synthetic-uid", ["NamespaceName"] = "synthetic-ns",
            ["BlobEndpoint"] = "https://storage.invalid/", ["ScenarioSourceRoot"] = AppContext.BaseDirectory, ["OutputRoot"] = "/work/loadgen",
            [role == "successor" ? "PredecessorBindingPath" : "StartLineBindingPath"] = "/bindings/not-mounted.json",
        };
        if (role == "child")
        {
            values["EventsPipeHandle"] = "4";
            values["AckPipeHandle"] = "5";
            values["IdentityChallenge"] = new string('c', 64);
        }
        foreach (var policy in B10AbruptOptions.Policy) values[policy.Key] = policy.Value.ToString(CultureInfo.InvariantCulture);
        return values.ToDictionary(pair => B10AbruptOptions.Prefix + pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}
#pragma warning restore CA1707
