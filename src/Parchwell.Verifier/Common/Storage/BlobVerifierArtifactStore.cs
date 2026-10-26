using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Parchwell.Verifier.Common.Storage;

/// <summary>Reads notes and creates immutable artifacts in the verifier container.</summary>
internal sealed class BlobVerifierArtifactStore : IVerifierArtifactStore
{
    private readonly BlobContainerClient _container;
    private readonly long _maxInputBytes;
    private readonly long _maxOutputBytes;

    public BlobVerifierArtifactStore(BlobContainerClient container, long maxInputBytes, long maxOutputBytes)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutputBytes);
        _container = container;
        _maxInputBytes = maxInputBytes;
        _maxOutputBytes = maxOutputBytes;
    }

    public Task<byte[]?> ReadStartLineAsync(string name, CancellationToken cancellationToken)
        => ReadAsync(name, cancellationToken);

    public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => ReadBoundedAsync(name,_maxInputBytes,cancellationToken);

    public Task<byte[]?> ReadBoundedAsync(string name,long maximumBytes,CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return BoundedBlobReader.ReadAsync(_container.GetBlobClient(name), Math.Min(_maxInputBytes,maximumBytes), cancellationToken);
    }

    public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) =>
        BoundedBlobListing.ListAsync(_container, prefix, cancellationToken);

    public async Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        if (content.LongLength > _maxOutputBytes)
            throw new InvalidDataException("verifier_output_byte_limit_exceeded");
        using var stream = new MemoryStream(content, writable: false);
        // Azure.Storage.Blobs 12.30.1 BlobClient.UploadAsync(Stream, bool, CancellationToken)
        // sends If-None-Match:* when overwrite is false. Native retry is disabled at composition.
        await _container.GetBlobClient(name).UploadAsync(stream, overwrite: false, cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>A bounded, complete or explicitly incomplete blob-name listing.</summary>
internal sealed record BlobListing(IReadOnlyList<string> Names, int Pages, bool IsComplete, string? FailureCode = null);

/// <summary>Retains a bounded blob listing and never treats a truncated listing as complete.</summary>
internal static class BoundedBlobListing
{
    internal const int MaximumPages = 16;
    internal const int MaximumEntries = 1024;
    internal const int MaximumNameBytes = 1_048_576;

    internal static async Task<BlobListing> ListAsync(BlobContainerClient container, string prefix, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        var names = new List<string>();
        long nameBytes = 0;
        int pages = 0;
        await foreach (var page in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken)
            .AsPages(pageSizeHint: 64).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            pages++;
            foreach (var item in page.Values)
            {
                if (!item.Name.StartsWith(prefix, StringComparison.Ordinal))
                    return new(names, pages, false, "listing_prefix_escape");
                int bytes = System.Text.Encoding.UTF8.GetByteCount(item.Name);
                nameBytes = checked(nameBytes + bytes);
                if (names.Count == MaximumEntries || nameBytes > MaximumNameBytes || pages > MaximumPages)
                    return new(names, pages, false, "listing_incomplete");
                names.Add(item.Name);
            }
            if (pages > MaximumPages) return new(names, pages, false, "listing_incomplete");
        }
        return new(names, pages, true);
    }
}

/// <summary>Enforces the artifact ceiling before retaining each streamed chunk.</summary>
internal static class BoundedBlobReader
{
    public static async Task<byte[]?> ReadAsync(BlobClient client, long maxBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            Response<BlobDownloadStreamingResult> response = await client.DownloadStreamingAsync(
                cancellationToken: cancellationToken).ConfigureAwait(false);
            using Stream source = response.Value.Content;
            if (response.Value.Details.ContentLength > maxBytes)
                throw new InvalidDataException("verifier_input_byte_limit_exceeded");

            using var content = new MemoryStream();
            byte[] buffer = new byte[(int)Math.Min(81920L, maxBytes)];
            while (true)
            {
                // Read one excess byte to detect overflow without appending it to the retained buffer.
                long remaining = maxBytes - content.Length;
                int requested = remaining >= buffer.Length ? buffer.Length : (int)remaining + 1;
                int read = await source.ReadAsync(buffer.AsMemory(0, requested), cancellationToken).ConfigureAwait(false);
                if (read == 0) return content.ToArray();
                if (read > maxBytes - content.Length)
                    throw new InvalidDataException("verifier_input_byte_limit_exceeded");
                content.Write(buffer, 0, read);
            }
        }
        catch (RequestFailedException exception) when (exception.Status == 404 &&
            exception.ErrorCode == BlobErrorCode.BlobNotFound.ToString())
        {
            return null;
        }
    }
}
