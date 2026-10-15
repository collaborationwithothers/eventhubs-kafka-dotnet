using System.Collections.Immutable;

namespace Parchwell.Scenarios;

/// <summary>Rejects invalid definitions without returning a partial run plan.</summary>
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
