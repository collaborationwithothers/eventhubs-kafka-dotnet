namespace Parchwell.Runner.Run.Contracts;

public sealed record StoredArtifact(byte[] Bytes, string ETag);
public enum ArtifactWriteOutcome { Stored, Conflict, Uncertain }

/// <summary>Private durable objects. Writes are conditional and never retried.</summary>
public interface IArtifactStore
{
    Task<StoredArtifact?> ReadAsync(string container, string path, int maxBytes, CancellationToken cancellationToken);
    Task<ArtifactWriteOutcome> WriteAsync(string container, string path, ReadOnlyMemory<byte> bytes,
        string? expectedETag, bool createOnly, CancellationToken cancellationToken);
}
