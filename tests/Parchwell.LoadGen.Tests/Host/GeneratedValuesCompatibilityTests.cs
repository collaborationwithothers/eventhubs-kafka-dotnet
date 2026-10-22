using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class GeneratedValuesCompatibilityTests
{
    [Theory]
    [InlineData("C0")]
    [InlineData("C3")]
    public async Task Given_ActualLoadGenRegistrations_When_ResolvingComparison_Then_EachDeclaredRoleUsesAdmittedProducerIdentity(string configuration)
    {
        string output = Path.Combine(Path.GetTempPath(), "compatibility-" + Guid.NewGuid().ToString("N"));
        string path = Path.GetTempFileName();
        Dictionary<string, string> inputs = LoadGenHostProcessTests.CreateInputs(output);
        inputs["LoadGen__ConfigurationId"] = configuration;
        string[] args = inputs.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
        using IHost host = Program.BuildHost(args);
        HelmValuesGenerator generator = host.Services.GetRequiredService<HelmValuesGenerator>();
        LoadedScenario loaded = await host.Services.GetRequiredService<ScenarioLoader>().LoadAsync("A01", TestContext.Current.CancellationToken);
        ResolvedScenario resolved = host.Services.GetRequiredService<ScenarioResolver>().Resolve(loaded);
        try
        {
            foreach (ResolvedRoleConfiguration role in resolved.Configurations[configuration].Roles.Values)
                Assert.Equal("worker-pod", role.Settings["client.id"].Value);
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(generator.Generate(resolved, configuration)));
            GeneratedValuesCompatibility.Validate(generator, resolved, configuration, path);
            Assert.False(Directory.Exists(output));

            var services = new ServiceCollection();
            foreach (KafkaClientRole role in Enum.GetValues<KafkaClientRole>())
                services.AddParchwellKafka(role, options =>
                {
                    options.NamespaceName = inputs["LoadGen__NamespaceName"];
                    options.ClientId = role == KafkaClientRole.VerifierConsumer ? "run-1-verifier" : "worker-pod";
                    options.EnrichmentGroupId = inputs["LoadGen__EnrichmentGroupId"];
                });
            using ServiceProvider verifierContext = services.BuildServiceProvider();
            ResolvedScenario verifierResolved = new ScenarioResolver(verifierContext.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
            Assert.Equal("run-1-verifier", verifierResolved.Configurations[configuration].Roles[KafkaClientRole.VerifierConsumer].Settings["client.id"].Value);
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(generator.Generate(verifierResolved, configuration)));
            Assert.Throws<InvalidOperationException>(() => GeneratedValuesCompatibility.Validate(generator, resolved, configuration, path));
        }
        finally { File.Delete(path); }
    }
}
#pragma warning restore CA1707
