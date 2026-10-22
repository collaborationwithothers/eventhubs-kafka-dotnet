namespace Parchwell.Runner.Run.Contracts;

public sealed record StoredArtifact(byte[] Bytes, string ETag);
public enum ArtifactWriteOutcome { Stored, Conflict, Uncertain }

/// <summary>
/// Private durable objects. Writes are conditional, with no Runner-initiated write retries.
/// The required Azure CLI may retain its pinned SDK's internal conditional-request retries
/// within the existing 60-second CLI and 180-second store-operation deadlines.
/// </summary>
public interface IArtifactStore
{
    Task<StoredArtifact?> ReadAsync(string container, string path, int maxBytes, CancellationToken cancellationToken);
    Task<ArtifactWriteOutcome> WriteAsync(string container, string path, ReadOnlyMemory<byte> bytes,
        string? expectedETag, bool createOnly, CancellationToken cancellationToken);
}
