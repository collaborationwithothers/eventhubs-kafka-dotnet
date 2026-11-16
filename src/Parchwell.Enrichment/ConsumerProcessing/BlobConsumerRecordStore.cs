using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace Parchwell.Enrichment.ConsumerProcessing;

// Each instance belongs to one member. The worker serializes all calls, including
// callbacks served by Consume and Close, so positions follow durable row order.
internal sealed class BlobConsumerRecordStore(BlobServiceClient client, string prefix) : IConsumerRecordStore
{
    private readonly BlobContainerClient container = client.GetBlobContainerClient("consumer-record");
    private AppendBlobClient? current;
    private int blobIndex;
    private int rowsInBlob;
    private long position;

    public Task RegisterAsync(byte[] json, CancellationToken cancellationToken) =>
        UploadAsync("registration.json", json, cancellationToken);

    public async Task AppendAsync(byte[] jsonLine, CancellationToken cancellationToken)
    {
        if (jsonLine.Length is 0 or > 65536 || jsonLine[^1] != (byte)'\n')
            throw new InvalidDataException("consumer_record_append_failed");

        if (current is null || rowsInBlob == 40000)
        {
            string name = "record/" + blobIndex.ToString("D6", CultureInfo.InvariantCulture) + ".jsonl";
            AppendBlobClient next = container.GetAppendBlobClient(prefix + name);
            await next.CreateAsync(new AppendBlobCreateOptions
            {
                Conditions = new AppendBlobRequestConditions { IfNoneMatch = ETag.All }
            }, cancellationToken).ConfigureAwait(false);
            current = next;
            blobIndex++;
            rowsInBlob = 0;
            position = 0;
        }

        using var stream = new MemoryStream(jsonLine, writable: false);
        // Append Block's position condition makes an ambiguous/duplicate append a
        // failed run instead of silently introducing a second row (plan S2).
        await current.AppendBlockAsync(stream, new AppendBlobAppendBlockOptions
        {
            Conditions = new AppendBlobRequestConditions { IfAppendPositionEqual = position }
        }, cancellationToken).ConfigureAwait(false);
        position += jsonLine.Length;
        rowsInBlob++;
    }

    public Task CompleteAsync(byte[] json, CancellationToken cancellationToken) =>
        UploadAsync("completion.json", json, cancellationToken);

    private async Task UploadAsync(string name, byte[] json, CancellationToken cancellationToken)
    {
        if (json.Length > 65536) throw new InvalidDataException("consumer_record_too_large");
        using var stream = new MemoryStream(json, writable: false);
        await container.GetBlobClient(prefix + name).UploadAsync(stream, new BlobUploadOptions
        {
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        }, cancellationToken).ConfigureAwait(false);
    }
}
