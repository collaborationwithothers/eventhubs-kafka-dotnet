namespace Parchwell.Scenarios;

/// <summary>A logical source of bounded scenario text, with no deployment or expected-outcome access required.</summary>
/// <remarks>
/// Paths are logical, such as scenarios/A01/scenario.yaml or
/// scenarios/profiles/synthetic.yaml. The loader asks only for a definition and its profile, never for
/// expected.yaml (ADR-014). A filesystem implementation is future runner work.
/// </remarks>
public interface IScenarioSource
{
    /// <summary>Returns a fresh reader whose ownership transfers to the loader.</summary>
    ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default);
}
