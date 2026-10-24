using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun;
using Parchwell.Verifier.VerifyRun.Storage;
using Parchwell.Verifier.VerifyRun.Configuration;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class GeneratedValuesCompatibilityTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("generated-values.yaml", null, null)]
    [InlineData(null, "/deployment/generated-values.yaml", "/deployment/generated-values.yaml")]
    [InlineData("alias.yaml", "section.yaml", "section.yaml")]
    public void Given_OptionalComparisonSettings_When_BindingHostOptions_Then_ValidPathsAndOmissionRemainAccepted(
        string? aliasPath, string? sectionPath, string? expectedPath)
    {
        IEnumerable<string> arguments = VerifierHostTests.ArgumentsFor("verify", "unused");
        if (aliasPath is not null) arguments = arguments.Append("--PARCHWELL_GENERATED_VALUES_FILE=" + aliasPath);
        if (sectionPath is not null) arguments = arguments.Append("--Verifier:GeneratedValuesFile=" + sectionPath);
        using IHost host = Program.BuildHost(arguments.ToArray());

        Assert.Equal(expectedPath, host.Services.GetRequiredService<IOptions<VerifierHostOptions>>().Value.GeneratedValuesFile);
    }

    [Fact]
    public void Given_NullOptionalComparisonPath_When_ValidatingVerifyOptions_Then_RemainsOptional()
    {
        VerifierHostOptions options = VerifierHostTests.ValidOptions("verify");
        options.GeneratedValuesFile = null;

        Assert.True(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("generated\nvalues.yaml")]
    [InlineData("generated\0values.yaml")]
    public void Given_InvalidConfiguredComparisonPath_When_Validated_Then_RejectsThePath(string path)
    {
        VerifierHostOptions options = VerifierHostTests.ValidOptions("verify");
        options.GeneratedValuesFile = path;

        var result = new VerifierHostOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures ?? [], failure => failure.Contains("GeneratedValuesFile", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("start-line")]
    [InlineData("probe-gate")]
    public void Given_InvalidInactiveComparisonPath_When_ValidatingOtherModes_Then_RemainsIgnored(string mode)
    {
        VerifierHostOptions options = VerifierHostTests.ValidOptions(mode);
        options.GeneratedValuesFile = "generated\0values.yaml";

        Assert.True(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("C0")]
    [InlineData("C3")]
    public async Task Given_RealVerifierRegistrations_When_Resolved_Then_PreservesSeparateClientIdsAndAcceptsMatchingBytes(string configuration)
    {
        using IHost host = Program.BuildHost(VerifierHostTests.ArgumentsFor("verify", "unused")
            .Append("--Verifier:ScenarioRoot=" + AppContext.BaseDirectory).ToArray());
        LoadedScenario loaded = await host.Services.GetRequiredService<ScenarioLoader>()
            .LoadAsync("A01", TestContext.Current.CancellationToken);
        VerifierHostOptions options = host.Services.GetRequiredService<IOptions<VerifierHostOptions>>().Value;
        string instance = "synthetic-loadgen-instance";
        string job = PhaseIdentity.JobName(options.RunId!, "C0", 1);
        var registration = new ProducerRegistration(1,
            new RunIdentity("A01", "C0", options.RunId!, instance, instance, "loadgen-producer"),
            "C0", 1, job, "loadgen-pod", "synthetic-loadgen-uid", options.SourceCommit!);
        ResolvedScenario resolved = new RegisteredProducerConfigurationResolver(
            Microsoft.Extensions.Options.Options.Create(options)).Resolve(loaded, registration);
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
    public async Task Given_LegacyGeneratedValuesSidecar_When_Verifying_Then_UsesRegisteredProducerConfiguration(string failure)
    {
        using var fixture = new HostEvidenceBuilder();
        string path = Path.Combine(Path.GetTempPath(), "verifier-values-" + Guid.NewGuid().ToString("N"));
        fixture.Options.GeneratedValuesFile = path;
        int nativeSessions = 0;
        fixture.SessionCreated = _ => nativeSessions++;
        try
        {
            if (failure == "different") await File.WriteAllTextAsync(path, "different", TestContext.Current.CancellationToken);
            Assert.Equal(0, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, nativeSessions);
            Assert.Equal(1, fixture.Creates);
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
        Assert.Null(options.GeneratedValuesFile);
        Assert.Equal("synthetic-tenant", options.TenantId);
        Assert.Equal("synthetic-identity", options.WorkloadIdentityClientId);
        Assert.Equal("/synthetic/token", options.FederatedTokenFilePath);
        Assert.Null(options.ClientId);
        Assert.Null(options.VerifierClientId);
    }

    private sealed class CountingVerifyHandler(VerifyRunHandler inner, Action onExecute) : IVerifierModeHandler
    {
        public VerifierMode Mode => VerifierMode.Verify;

        public Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            onExecute();
            return inner.ExecuteAsync(cancellationToken);
        }
    }
}
#pragma warning restore CA1707
