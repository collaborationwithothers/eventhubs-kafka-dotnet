using Azure.Storage.Blobs;

namespace Parchwell.Verifier.Common.Storage;

/// <summary>Provides bounded reads and complete listings for repeat-scoped producer evidence.</summary>
internal sealed class BlobProducerArtifactStore(BlobContainerClient container, long maximumBytes) : IProducerArtifactStore
{
    public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) =>
        BoundedBlobListing.ListAsync(container, prefix, cancellationToken);

    public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => ReadBoundedAsync(name,maximumBytes,cancellationToken);

    public Task<byte[]?> ReadBoundedAsync(string name,long maximumArtifactBytes,CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.StartsWith('/') || name.Contains("..", StringComparison.Ordinal) || name.Contains('\\'))
            throw new ArgumentException("producer_blob_name_not_fixed", nameof(name));
        return BoundedBlobReader.ReadAsync(container.GetBlobClient(name), Math.Min(maximumBytes,maximumArtifactBytes), cancellationToken);
    }
}
