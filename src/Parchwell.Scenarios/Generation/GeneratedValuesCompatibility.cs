using System.Text;

namespace Parchwell.Scenarios;

/// <summary>Checks a deployment comparison file against the worker's complete resolved settings.</summary>
public static class GeneratedValuesCompatibility
{
    /// <summary>Requires exact UTF-8 generator bytes, including its existing LF newline policy.</summary>
    /// <param name="generator">The generator registered by the application composition root.</param>
    /// <param name="resolved">The scenario resolved using this worker's own Kafka registrations.</param>
    /// <param name="configurationId">The selected configuration.</param>
    /// <param name="comparisonFilePath">The deployment-provided comparison file.</param>
    public static void Validate(HelmValuesGenerator generator, ResolvedScenario resolved, string configurationId, string comparisonFilePath)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentException.ThrowIfNullOrWhiteSpace(comparisonFilePath);
        byte[] expected = Encoding.UTF8.GetBytes(generator.Generate(resolved, configurationId));
        using var input = File.OpenRead(comparisonFilePath);
        if (input.Length != expected.Length)
            throw new InvalidOperationException("generated_values_mismatch");
        byte[] actual = new byte[expected.Length];
        input.ReadExactly(actual);
        if (!expected.AsSpan().SequenceEqual(actual) || input.ReadByte() != -1)
            throw new InvalidOperationException("generated_values_mismatch");
    }
}
