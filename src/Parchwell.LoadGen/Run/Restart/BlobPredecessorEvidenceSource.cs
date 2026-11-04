using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Parchwell.LoadGen.Run.Restart;

/// <summary>Reads the predecessor's objects from the ledger container with a bounded listing and bounded downloads.</summary>
public sealed class BlobPredecessorEvidenceSource : IPredecessorEvidenceSource
{
    // A valid repeat holds eight objects for its first instance, so a longer listing is rejected rather than truncated.
    internal const int MaximumEntries = 64;
    private readonly BlobContainerClient _container;

    /// <summary>Creates a source for the ledger container of the configured Blob service.</summary>
    public BlobPredecessorEvidenceSource(BlobServiceClient client, string container)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (!StringComparer.Ordinal.Equals(container, "ledger"))
            throw new ArgumentException("predecessor_source_container_invalid", nameof(container));
        _container = client.GetBlobContainerClient(container);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        var names = new List<string>();
        await foreach (Page<BlobItem> page in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken)
            .AsPages(pageSizeHint: MaximumEntries).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            foreach (BlobItem item in page.Values)
            {
                if (names.Count == MaximumEntries) throw new InvalidDataException("predecessor_listing_incomplete");
                names.Add(item.Name);
            }
        }
        return names;
    }

    /// <inheritdoc />
    public async Task<byte[]> ReadAsync(string name, long maximumBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumBytes, (long)int.MaxValue);
        Response<BlobDownloadStreamingResult> response = await _container.GetBlobClient(name)
            .DownloadStreamingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        using BlobDownloadStreamingResult result = response.Value;
        using var output = new MemoryStream();
        byte[] buffer = new byte[81920];
        while (true)
        {
            int read = await result.Content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) return output.ToArray();
            if (output.Length + read > maximumBytes) throw new InvalidDataException("predecessor_object_exceeds_byte_limit");
            output.Write(buffer, 0, read);
        }
    }
}
