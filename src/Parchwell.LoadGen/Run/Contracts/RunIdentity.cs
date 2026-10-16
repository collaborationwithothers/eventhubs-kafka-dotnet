namespace Parchwell.LoadGen.Run.Contracts;

/// <summary>Identifies one execution independently of the seeded workload.</summary>
public sealed record RunIdentity(string ScenarioId, string ConfigurationId, string RunId,
    string InstanceId, string ExpectedInstanceId, string ClientId)
{
    /// <summary>Rejects missing identity or a worker different from the runner's recorded worker.</summary>
    public void Validate()
    {
        foreach (var value in new[] { ScenarioId, ConfigurationId, RunId, InstanceId, ExpectedInstanceId, ClientId })
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
                throw new ArgumentException("identity_contains_whitespace_or_control_character");
        }
        if (!string.Equals(InstanceId, ExpectedInstanceId, StringComparison.Ordinal))
            throw new InvalidOperationException("worker_identity_mismatch");
    }
}
