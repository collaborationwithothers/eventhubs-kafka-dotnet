using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Parchwell.Scenarios;

/// <summary>The fixed workflow phases of each executable scenario and the configuration each phase runs.</summary>
/// <remarks>A probe phase runs its gate configuration; every other phase runs the configuration of the same name.</remarks>
public static class ScenarioPhases
{
    private static readonly ImmutableDictionary<string, ImmutableDictionary<string, string>> Table =
        ImmutableDictionary.CreateRange(StringComparer.Ordinal, new KeyValuePair<string, ImmutableDictionary<string, string>>[]
        {
            new("A01", ImmutableDictionary.CreateRange(StringComparer.Ordinal, new KeyValuePair<string, string>[] { new("probe", "C0"), new("C0", "C0"), new("C3", "C3") })),
            new("B04", ImmutableDictionary.CreateRange(StringComparer.Ordinal, new KeyValuePair<string, string>[] { new("C0", "C0"), new("C1", "C1") }))
        });

    /// <summary>Checks whether a scenario has workflow phases.</summary>
    /// <param name="scenarioId">The exact scenario identifier.</param>
    /// <returns><see langword="true"/> for A01 and B04.</returns>
    public static bool IsExecutableScenario(string? scenarioId) => scenarioId is not null && Table.ContainsKey(scenarioId);

    /// <summary>Finds the configuration a scenario phase runs.</summary>
    /// <param name="scenarioId">The exact scenario identifier.</param>
    /// <param name="phase">The exact phase name.</param>
    /// <param name="configurationId">The configuration, or <see langword="null"/> when the pair is unknown.</param>
    /// <returns><see langword="true"/> when the scenario has the phase.</returns>
    public static bool TryGetConfiguration(string? scenarioId, string? phase, [NotNullWhen(true)] out string? configurationId)
    {
        configurationId = null;
        return scenarioId is not null && phase is not null && Table.TryGetValue(scenarioId, out var phases) &&
            phases.TryGetValue(phase, out configurationId);
    }
}
