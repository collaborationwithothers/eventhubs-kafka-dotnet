using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class ScenarioDependencyInjectionTests
{
    // Parsing budgets have no defaults and must be positive. Validation on start (dotnet.md "Seams and
    // dependencies") makes a bad budget fail when the host starts, not at the first load.
    [Fact]
    public void Given_NonpositiveParsingBudget_When_StartingScenarioModule_Then_ThrowsOptionsValidationException()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IScenarioSource>(ScenarioTestData.Source());
        services.AddParchwellScenarios(options =>
        {
            options.MaxDocumentCharacters = 1_000;
            options.MaxNestingDepth = 10;
            options.MaxScalarCharacters = 0;
            options.MaxParserEvents = 1_000;
        });
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    // The scenario declares VerifierConsumer but only LoadGenProducer is registered. Resolution must fail
    // and name the role, rather than return a run plan missing one client.
    [Fact]
    public async Task Given_ScenarioDeclaresRoleMissingFromKafkaFactory_When_Resolving_Then_NamesTheMissingRole()
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-client";
        });
        services.AddSingleton<IScenarioSource>(ScenarioTestData.Source());
        services.AddParchwellScenarios(options =>
        {
            options.MaxDocumentCharacters = 16_384;
            options.MaxNestingDepth = 20;
            options.MaxScalarCharacters = 1_024;
            options.MaxParserEvents = 2_000;
        });
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStartupValidator>().Validate();
        var loaded = await provider.GetRequiredService<ScenarioLoader>()
            .LoadAsync("A01", TestContext.Current.CancellationToken);

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ScenarioResolver>().Resolve(loaded));

        Assert.Contains(nameof(KafkaClientRole.VerifierConsumer), exception.Message, StringComparison.Ordinal);
    }

    // AddParchwellScenarios registers the concrete loader, resolver and generator; only the scenario
    // source is an interface, because it is a boundary (dotnet.md "Seams and dependencies").
    [Fact]
    public void Given_ValidScenarioModuleRegistrations_When_ResolvingServices_Then_CreatesConcreteServices()
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-client";
        });
        services.AddSingleton<IScenarioSource>(ScenarioTestData.Source());
        services.AddParchwellScenarios(options =>
        {
            options.MaxDocumentCharacters = 16_384;
            options.MaxNestingDepth = 20;
            options.MaxScalarCharacters = 1_024;
            options.MaxParserEvents = 2_000;
        });
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ScenarioLoader>());
        Assert.NotNull(provider.GetRequiredService<ScenarioResolver>());
        Assert.NotNull(provider.GetRequiredService<HelmValuesGenerator>());
    }
}
