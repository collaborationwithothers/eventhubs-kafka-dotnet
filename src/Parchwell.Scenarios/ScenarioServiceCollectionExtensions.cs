using Microsoft.Extensions.DependencyInjection;

namespace Parchwell.Scenarios;

/// <summary>Registers scenario declaration loading and pure resolution.</summary>
public static class ScenarioServiceCollectionExtensions
{
    /// <summary>Registers required parsing limits with startup validation. The caller supplies IScenarioSource and Kafka roles.</summary>
    public static IServiceCollection AddParchwellScenarios(this IServiceCollection services,
        Action<ScenarioLoadOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        // Budgets have no defaults, so a missing or zero budget fails at startup instead of at the first load.
        services.AddOptions<ScenarioLoadOptions>().Configure(configure)
            .Validate(options => options.MaxDocumentCharacters > 0, "MaxDocumentCharacters must be positive.")
            .Validate(options => options.MaxNestingDepth > 0, "MaxNestingDepth must be positive.")
            .Validate(options => options.MaxScalarCharacters > 0, "MaxScalarCharacters must be positive.")
            .Validate(options => options.MaxParserEvents > 0, "MaxParserEvents must be positive.")
            .ValidateOnStart();
        services.AddSingleton<ScenarioLoader>();
        services.AddSingleton<ScenarioResolver>();
        return services;
    }
}
