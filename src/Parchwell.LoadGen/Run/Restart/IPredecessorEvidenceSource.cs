namespace Parchwell.LoadGen.Run.Restart;

/// <summary>Read-only access to the ledger objects that a B10 second instance reads from its predecessor (ADR-021).</summary>
public interface IPredecessorEvidenceSource
{
    /// <summary>Returns every object name beneath the prefix, or throws when the listing cannot be completed.</summary>
    Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken);

    /// <summary>Returns one object's bytes, or throws when it is missing or larger than the limit.</summary>
    Task<byte[]> ReadAsync(string name, long maximumBytes, CancellationToken cancellationToken);
}
