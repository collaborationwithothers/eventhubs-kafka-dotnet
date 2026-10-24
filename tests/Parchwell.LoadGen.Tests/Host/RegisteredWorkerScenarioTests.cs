using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen;
using Parchwell.LoadGen.Run.Input;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class RegisteredWorkerScenarioTests
{
    [Theory]
    [InlineData("probe", "C0")]
    [InlineData("C0", "C0")]
    [InlineData("C3", "C3")]
    public async Task Given_PackagedScenario_When_ResolvingRegisteredWorker_Then_SelectedConfigurationAndProducerIdentityComeFromItsOwnInputs(
        string phase, string configuration)
    {
        string output = Path.Combine(Path.GetTempPath(), "registered-worker-" + Guid.NewGuid().ToString("N"));
        Dictionary<string, string> inputs = LoadGenHostProcessTests.CreateInputs(output);
        inputs["LoadGen__ConfigurationId"] = configuration;
        inputs["LoadGen__Phase"] = phase;
        inputs["LoadGen__RunId"] = "run-1";
        inputs["LoadGen__RepeatNumber"] = "1";
        inputs["LoadGen__JobName"] = $"run-1-{phase.ToLowerInvariant()}-r1-loadgen";
        string[] args = inputs.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
        var telemetry = new RecordingTelemetryRegistration();
        using IHost host = Program.BuildHostCore(args, null, telemetry.Register);

        LoadGenHostOptions options = host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value;
        Assert.Equal(configuration, options.ConfigurationId);
        Assert.Equal(phase, options.Phase);
        Assert.Equal("run-1", options.RunId);
        Assert.Equal(1, options.RepeatNumber);
        Assert.Equal($"run-1-{phase.ToLowerInvariant()}-r1-loadgen", options.JobName);
        Assert.Equal(1, telemetry.RegistrationCount);

        LoadedScenario loaded = await host.Services.GetRequiredService<ScenarioLoader>()
            .LoadAsync("A01", TestContext.Current.CancellationToken);
        ResolvedScenario resolved = host.Services.GetRequiredService<ScenarioResolver>().Resolve(loaded);
        foreach (ResolvedRoleConfiguration role in resolved.Configurations[configuration].Roles.Values)
            Assert.Equal("worker-pod", role.Settings["client.id"].Value);
        Assert.Equal("worker-pod", resolved.Configurations[configuration].Roles[
            KafkaClientRole.LoadGenProducer].Settings["client.id"].Value);
        Assert.False(Directory.Exists(output));

        var services = new ServiceCollection();
        foreach (KafkaClientRole role in Enum.GetValues<KafkaClientRole>())
            services.AddParchwellKafka(role, kafkaOptions =>
            {
                kafkaOptions.NamespaceName = options.NamespaceName;
                kafkaOptions.ClientId = role == KafkaClientRole.VerifierConsumer ? "run-1-verifier" : "worker-pod";
                kafkaOptions.EnrichmentGroupId = options.EnrichmentGroupId;
            });
        using ServiceProvider verifierContext = services.BuildServiceProvider();
        ResolvedScenario verifierResolved = new ScenarioResolver(
            verifierContext.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
        Assert.Equal("run-1-verifier", verifierResolved.Configurations[configuration]
            .Roles[KafkaClientRole.VerifierConsumer].Settings["client.id"].Value);
    }

}
#pragma warning restore CA1707
