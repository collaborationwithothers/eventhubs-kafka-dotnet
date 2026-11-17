using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Parchwell.Kafka.Statistics;

namespace Parchwell.Enrichment.Common.NativeMonitoring;

internal sealed class BlobNativeMonitoringStore(BlobContainerClient container) : IKafkaNativeMonitoringStore
{
    public async Task CreateAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        await container.GetBlobClient(path).UploadAsync(stream, new BlobUploadOptions
        {
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        }, cancellationToken).ConfigureAwait(false);
    }
}
