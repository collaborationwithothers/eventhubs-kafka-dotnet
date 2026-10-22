#pragma warning disable CA1707 // Repository-required Given/When/Then test names.
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Runner.Run.Deployment;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.Runner.Tests.Deployment;

public sealed class DeploymentPreflightTests
{
    [Fact]
    public async Task Given_MissingCommittedServiceTags_When_Preflight_Then_ItRejectsBeforeAnyProductionTransportCanBeConstructed()
    {
        using var fixture = new DeploymentFixture();
        Dictionary<string, object?> runtime = fixture.Input("C0");
        runtime["synthetic"] = false;
        runtime["service_tags_file"] = Path.Combine(fixture.Directory, "missing-committed-service-tags.json");
        using ServiceProvider provider = PreflightProvider();
        await Assert.ThrowsAsync<FileNotFoundException>(() => provider.GetRequiredService<DeploymentPreflight>().ValidateAsync(JsonSerializer.SerializeToElement(runtime), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_SyntheticRuntime_When_ProductionPreflight_Then_ItRejects()
    {
        using var fixture = new DeploymentFixture();
        using ServiceProvider provider = PreflightProvider();
        await Assert.ThrowsAsync<InvalidDataException>(() => provider.GetRequiredService<DeploymentPreflight>().ValidateAsync(JsonSerializer.SerializeToElement(fixture.Input("C0")), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_RegisteredScenarioSourceAndBudgets_When_Preflight_Then_UsesThatLoaderBeforeToolVerification()
    {
        using var fixture = new DeploymentFixture();
        var token = TestContext.Current.CancellationToken;
        Dictionary<string, object?> runtime = fixture.Input("C0");
        runtime["synthetic"] = false;
        runtime["scenario_root"] = Path.Combine(fixture.Directory, "unregistered-source");
        runtime["helm_archive_file"] = Path.Combine(fixture.Directory, "missing-helm.tar.gz");
        string tagsPath = (string)runtime["service_tags_file"]!;
        JsonNode tags = JsonNode.Parse(await File.ReadAllTextAsync(tagsPath, token))!;
        tags["synthetic"] = false;
        await File.WriteAllTextAsync(tagsPath, tags.ToJsonString(), token);
        var source = new RecordingScenarioSource();
        using ServiceProvider provider = PreflightProvider(source, new ScenarioLoadOptions
        { MaxDocumentCharacters = 1, MaxNestingDepth = 32, MaxScalarCharacters = 65536, MaxParserEvents = 100000 });

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(() => provider.GetRequiredService<DeploymentPreflight>()
            .ValidateAsync(JsonSerializer.SerializeToElement(runtime), token));

        Assert.Equal("scenarios/A01/scenario.yaml", Assert.Single(source.RequestedPaths));
        Assert.Equal("DocumentTooLarge", Assert.Single(exception.Diagnostics).Code);
    }

    private static ServiceProvider PreflightProvider(IScenarioSource? source = null, ScenarioLoadOptions? parsing = null)
    {
        using JsonDocument limits = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(DeploymentFixture.RepositoryRoot, "docs/operations/a01-runtime-limits.json")));
        var services = new ServiceCollection();
        services.AddSingleton(source ?? new DeploymentScenarioSource(DeploymentFixture.RepositoryRoot));
        services.AddSingleton(Options.Create(parsing ?? JsonSerializer.Deserialize<ScenarioLoadOptions>(limits.RootElement.GetProperty("scenario_parsing"))!));
        services.AddSingleton<ScenarioLoader>();
        services.AddSingleton<DeploymentPreflight>();
        return services.BuildServiceProvider();
    }

    private sealed class RecordingScenarioSource : IScenarioSource
    {
        public List<string> RequestedPaths { get; } = [];
        public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedPaths.Add(logicalPath);
            return ValueTask.FromResult<TextReader>(new StringReader("schema_version: 1"));
        }
    }
}
