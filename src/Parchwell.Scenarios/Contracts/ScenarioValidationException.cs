using System.Collections.Immutable;

namespace Parchwell.Scenarios;

/// <summary>Rejects invalid definitions without returning a partial run plan.</summary>
/// <remarks>
/// When the readers reject a document, <see cref="ScenarioValidationException.Diagnostics"/> holds every
/// problem they found, so an author can fix a file in one pass. The YAML parser stops at its first
/// problem, so its rejections carry one diagnostic.
/// </remarks>
public sealed class ScenarioValidationException : Exception
{
    public ScenarioValidationException() { }
    public ScenarioValidationException(string message) : base(message) { }
    public ScenarioValidationException(string message, Exception innerException) : base(message, innerException) { }
    public ScenarioValidationException(IEnumerable<ScenarioDiagnostic> diagnostics)
        : this(diagnostics.ToImmutableArray()) { }

    private ScenarioValidationException(ImmutableArray<ScenarioDiagnostic> diagnostics)
        : base(string.Join(Environment.NewLine, diagnostics.Select(item =>
            $"{item.Source}:{item.Line}:{item.Column} {item.FieldPath}: {item.Message}")))
    {
        Diagnostics = diagnostics;
    }

    public ImmutableArray<ScenarioDiagnostic> Diagnostics { get; } = [];
}
