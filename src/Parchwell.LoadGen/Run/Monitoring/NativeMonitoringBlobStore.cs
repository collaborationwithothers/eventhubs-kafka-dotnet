using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Parchwell.Kafka.Statistics;

namespace Parchwell.LoadGen.Run.Monitoring;

internal sealed class NativeMonitoringBlobStore(BlobServiceClient client) : IKafkaNativeMonitoringStore
{
    public async Task CreateAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        BlobClient blob = client.GetBlobContainerClient("native-monitoring").GetBlobClient(path);
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        }, cancellationToken).ConfigureAwait(false);
    }
}
