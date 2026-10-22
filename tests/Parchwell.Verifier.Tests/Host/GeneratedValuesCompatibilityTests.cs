using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class GeneratedValuesCompatibilityTests
{
    [Theory]
    [InlineData("C0")]
    [InlineData("C3")]
    public async Task Given_RealVerifierRegistrations_When_Resolved_Then_PreservesSeparateClientIdsAndAcceptsMatchingBytes(string configuration)
    {
        using IHost host = Program.BuildHost(VerifierHostTests.ArgumentsFor("verify", "unused")
            .Append("--Verifier:ScenarioRoot=" + AppContext.BaseDirectory).ToArray());
        LoadedScenario loaded = await host.Services.GetRequiredService<ScenarioLoader>()
            .LoadAsync("A01", TestContext.Current.CancellationToken);
        ResolvedScenario resolved = host.Services.GetRequiredService<ScenarioResolver>().Resolve(loaded);
        var roles = resolved.Configurations[configuration].Roles;
        Assert.Equal("loadgen-producer", roles[KafkaClientRole.LoadGenProducer].Settings["client.id"].Value);
        Assert.Equal("verifier-client", roles[KafkaClientRole.VerifierConsumer].Settings["client.id"].Value);
        HelmValuesGenerator generator = host.Services.GetRequiredService<HelmValuesGenerator>();
        string generated = generator.Generate(resolved, configuration);
        string path = Path.Combine(Path.GetTempPath(), "verifier-values-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes(generated), TestContext.Current.CancellationToken);
            GeneratedValuesCompatibility.Validate(generator, resolved, configuration, path);
            await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes(generated.Replace("verifier-client", "loadgen-producer", StringComparison.Ordinal)),
                TestContext.Current.CancellationToken);
            Assert.Throws<InvalidOperationException>(() => GeneratedValuesCompatibility.Validate(generator, resolved, configuration, path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("different")]
    public async Task Given_PresentComparisonPathWithBadBytes_When_Verifying_Then_FailsBeforeNativeSession(string failure)
    {
        using var fixture = new HostEvidenceBuilder();
        string path = Path.Combine(Path.GetTempPath(), "verifier-values-" + Guid.NewGuid().ToString("N"));
        fixture.Options.GeneratedValuesFile = path;
        int nativeSessions = 0;
        fixture.SessionCreated = _ => nativeSessions++;
        try
        {
            if (failure == "different") await File.WriteAllTextAsync(path, "different", TestContext.Current.CancellationToken);
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, nativeSessions);
            Assert.Equal(0, fixture.Creates);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Given_DeploymentEnvironmentKeys_When_Bound_Then_MapsComparisonAndIdentityWithoutChangingClientIds()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Verifier:Mode"] = "verify", ["PARCHWELL_GENERATED_VALUES_FILE"] = "/deployment/generated-values.yaml",
            ["AZURE_TENANT_ID"] = "synthetic-tenant", ["AZURE_CLIENT_ID"] = "synthetic-identity",
            ["AZURE_FEDERATED_TOKEN_FILE"] = "/synthetic/token"
        }).Build();
        var options = new VerifierHostOptions();
        new VerifierHostOptionsSetup(config).Configure(options);
        Assert.Equal("/deployment/generated-values.yaml", options.GeneratedValuesFile);
        Assert.Equal("synthetic-tenant", options.TenantId);
        Assert.Equal("synthetic-identity", options.WorkloadIdentityClientId);
        Assert.Equal("/synthetic/token", options.FederatedTokenFilePath);
        Assert.Null(options.ClientId);
        Assert.Null(options.VerifierClientId);
    }
}
#pragma warning restore CA1707
