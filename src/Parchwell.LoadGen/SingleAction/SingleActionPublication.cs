using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Parchwell.LoadGen.Common.SingleAction;

namespace Parchwell.LoadGen.SingleAction;

internal interface ISingleActionPublication
{
    Task WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);
}

internal sealed class SingleActionPublication(BlobServiceClient client, SingleActionHostOptions options) : ISingleActionPublication
{
    public async Task WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        BlobClient blob = client.GetBlobContainerClient(options.BlobContainer).GetBlobClient(path);
        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        await blob.UploadAsync(stream, new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } }, cancellationToken).ConfigureAwait(false);
    }
    internal static string Prefix(SingleActionHostOptions options) => SingleActionWire.Prefix(options.RunId, options.ScenarioId, options.ConfigurationId);
    internal static SingleActionIdentity Identity(SingleActionHostOptions options) => new(options.ScenarioId, options.ConfigurationId, options.RunId, options.PodName);
    internal static SingleActionRegistration Registration(SingleActionHostOptions options) => new(1, "single-action", Identity(options), options.JobName, options.PodName, options.PodUid, options.SourceCommit);
    internal static SingleActionCompletion Completion(SingleActionHostOptions options) => new(1, "single-action", Identity(options), options.JobName, options.PodName, options.PodUid, options.SourceCommit, 5, 0, true);
}
