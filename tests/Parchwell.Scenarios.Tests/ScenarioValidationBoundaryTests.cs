using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class ScenarioValidationBoundaryTests
{
    // The loader must reject an ID outside the frozen inventory before it reads any file,
    // so a typo or a path such as "../A01" can never open an unrelated source.
    [Theory]
    [InlineData("Z99")]
    [InlineData("A00")]
    [InlineData("a01")]
    [InlineData("../A01")]
    [InlineData("")]
    public async Task Given_UnknownInventoryId_When_Loading_Then_RejectsBeforeOpeningAnySource(string id)
    {
        var source = ScenarioTestData.Source();

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () =>
            await ScenarioTestData.Loader(source).LoadAsync(id, TestContext.Current.CancellationToken));

        var diagnostic = Assert.Single(exception.Diagnostics);
        Assert.Equal("unknown_inventory_id", diagnostic.Code);
        Assert.Equal("id", diagnostic.FieldPath);
        Assert.Empty(source.RequestedPaths);
    }

    // The last ID of every inventory group, K10 from amendment 1 included (docs/blueprint.md section 12),
    // proves the loader uses the Kafka library's FrozenInventory rather than a shorter copy.
    [Theory]
    [InlineData("A06")]
    [InlineData("B11")]
    [InlineData("C19")]
    [InlineData("D05")]
    [InlineData("E06")]
    [InlineData("F06")]
    [InlineData("G05")]
    [InlineData("H08")]
    [InlineData("K10")]
    public async Task Given_FrozenInventoryId_When_Loading_Then_AcceptsTheSharedInventory(string id)
    {
        var path = $"scenarios/{id}/scenario.yaml";
        var yaml = ScenarioTestData.ValidScenario.Replace("id: A01", "id: " + id, StringComparison.Ordinal);
        var source = new InMemoryScenarioSource((path, yaml), (ScenarioTestData.ProfilePath, ScenarioTestData.ValidProfile));

        var loaded = await ScenarioTestData.Loader(source).LoadAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal(id, loaded.Definition.Id);
        Assert.Equal([path, ScenarioTestData.ProfilePath], source.RequestedPaths);
    }

    // roles must be nonempty (scenario-contract.md): with no roles the resolver would emit no Kafka settings.
    [Fact]
    public async Task Given_EmptyRoles_When_Loading_Then_RejectsAtTheDeclarationBoundary()
    {
        var yaml = ScenarioTestData.ValidScenario
            .Replace("roles: [LoadGenProducer, VerifierConsumer]", "roles: []", StringComparison.Ordinal)
            .Replace("measures:\n  - {id: verifier.ok, role: VerifierConsumer}", "measures: []", StringComparison.Ordinal);

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () =>
            await ScenarioTestData.Loader(ScenarioTestData.Source(yaml)).LoadAsync("A01", TestContext.Current.CancellationToken));

        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.FieldPath == "roles" && diagnostic.Code == "empty_sequence");
    }

    // Resolver codes follow the issue 61 snake_case migration (scenario-contract.md), and the diagnostic
    // points at the override's line in scenario.yaml, the file the author edits.
    [Theory]
    [InlineData("request.timeout.ms", 0, "setting_out_of_range")]
    [InlineData("socket.timeout.ms", 300_001, "setting_out_of_range")]
    [InlineData("request.timeout.ms", 60_000, "no_op_override")]
    [InlineData("transactional.id", 1, "unsupported_setting")]
    public async Task Given_InvalidOverride_When_Resolving_Then_UsesSnakeCaseDiagnosticWithOriginalSource(
        string key, int value, string code)
    {
        var yaml = ScenarioTestData.ValidScenario.Replace("configurations:\n  baseline: {}",
            $"configurations:\n  baseline: {{}}\n  changed:\n    LoadGenProducer:\n      {key}: {value}", StringComparison.Ordinal);
        var loaded = await ScenarioTestData.Loader(ScenarioTestData.Source(yaml))
            .LoadAsync("A01", TestContext.Current.CancellationToken);
        using var provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer);

        var exception = Assert.Throws<ScenarioValidationException>(() =>
            new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded));

        var diagnostic = Assert.Single(exception.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal($"configurations.changed.LoadGenProducer.{key}", diagnostic.FieldPath);
        Assert.Equal(ScenarioTestData.ScenarioPath, diagnostic.Source);
        Assert.Equal(10, diagnostic.Line);
        Assert.NotNull(diagnostic.Column);
    }
}
