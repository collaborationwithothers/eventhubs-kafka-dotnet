namespace Parchwell.Scenarios;

/// <summary>A logical source of bounded scenario text, with no deployment or expected-outcome access required.</summary>
public interface IScenarioSource
{
    /// <summary>Returns a fresh reader whose ownership transfers to the loader.</summary>
    ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default);
}
