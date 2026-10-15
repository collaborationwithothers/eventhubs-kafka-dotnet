using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios;

/// <summary>Loads and validates one frozen-inventory scenario and its referenced workload profile.</summary>
/// <remarks>
/// It reads scenarios/&lt;ID&gt;/scenario.yaml, then the one profile that file references, and nothing
/// else. In particular it never opens expected.yaml (ADR-014). Every document is parsed within the
/// <see cref="ScenarioLoadOptions"/> budgets.
/// </remarks>
public sealed class ScenarioLoader
{
    private readonly IScenarioSource _source;
    private readonly IOptions<ScenarioLoadOptions> _options;

    /// <summary>Creates a loader over a logical scenario source and required parsing budgets.</summary>
    public ScenarioLoader(IScenarioSource source, IOptions<ScenarioLoadOptions> options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        _source = source;
        _options = options;
    }

    /// <summary>Loads only the requested scenario definition and its validated profile.</summary>
    public async ValueTask<LoadedScenario> LoadAsync(string inventoryId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventoryId);
        var scenarioPath = $"scenarios/{inventoryId}/scenario.yaml";
        var options = SnapshotAndValidateOptions(scenarioPath);
        // Only ids in the frozen inventory (docs/blueprint.md section 12) are in scope. Checking before any
        // read also means a typo or a path such as "../A01" never opens a source.
        if (!FrozenInventory.Contains(inventoryId))
        {
            throw new ScenarioValidationException([new ScenarioDiagnostic(scenarioPath, "id", "unknown_inventory_id",
                "The requested id must be an exact id from the frozen scenario inventory.")]);
        }

        var definitionNode = await ReadNodeAsync(scenarioPath, options, cancellationToken).ConfigureAwait(false);
        var definition = DefinitionReader.Read(definitionNode, scenarioPath, inventoryId);
        var profilePath = "scenarios/" + definition.Profile;
        // The definition reader has already checked the profiles/<slug>.yaml shape, so this slice is safe.
        var profileSlug = definition.Profile["profiles/".Length..^".yaml".Length];
        var profileNode = await ReadNodeAsync(profilePath, options, cancellationToken).ConfigureAwait(false);
        var profile = ProfileReader.Read(profileNode, profilePath, profileSlug);
        return new LoadedScenario(definition, profile);
    }

    private async ValueTask<InputNode> ReadNodeAsync(string logicalPath, ScenarioLoadOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var reader = await _source.OpenReadAsync(logicalPath, cancellationToken).ConfigureAwait(false);
        return await YamlInput.ReadAsync(reader, logicalPath, options, cancellationToken).ConfigureAwait(false);
    }

    private ScenarioLoadOptions SnapshotAndValidateOptions(string source)
    {
        var configured = _options.Value ?? throw new InvalidOperationException("ScenarioLoadOptions must be registered.");
        // Copy the budgets so both documents of one load use the same values, even if the registered
        // options object is changed meanwhile.
        var snapshot = new ScenarioLoadOptions
        {
            MaxDocumentCharacters = configured.MaxDocumentCharacters,
            MaxNestingDepth = configured.MaxNestingDepth,
            MaxScalarCharacters = configured.MaxScalarCharacters,
            MaxParserEvents = configured.MaxParserEvents
        };
        var invalid = new List<ScenarioDiagnostic>();
        AddBudgetError(nameof(snapshot.MaxDocumentCharacters), snapshot.MaxDocumentCharacters);
        AddBudgetError(nameof(snapshot.MaxNestingDepth), snapshot.MaxNestingDepth);
        AddBudgetError(nameof(snapshot.MaxScalarCharacters), snapshot.MaxScalarCharacters);
        AddBudgetError(nameof(snapshot.MaxParserEvents), snapshot.MaxParserEvents);
        if (invalid.Count != 0) throw new ScenarioValidationException(invalid);
        return snapshot;

        void AddBudgetError(string name, int value)
        {
            if (value > 0) return;
            invalid.Add(new ScenarioDiagnostic(source, "options." + name, "invalid_budget", "Parsing budgets must be positive."));
        }
    }
}
