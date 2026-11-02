using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Parchwell.LoadGen.Common.SingleAction;

namespace Parchwell.LoadGen.IdleObservation;

internal interface IIdleObservationPublication
{
    Task WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);
}

internal sealed class IdleObservationPublication(BlobServiceClient client, IdleObservationOptions options) : IIdleObservationPublication
{
    public async Task WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        BlobClient blob = client.GetBlobContainerClient(options.BlobContainer).GetBlobClient(path);
        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        await blob.UploadAsync(stream, new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } }, cancellationToken).ConfigureAwait(false);
    }
    internal static string Prefix(IdleObservationOptions options) => $"{options.RunId}/idle-observation/{options.ScenarioId}/{options.ConfigurationId}/";
    internal static SingleActionIdentity Identity(IdleObservationOptions options) => new(options.ScenarioId, options.ConfigurationId, options.RunId, options.PodName);
    internal static SingleActionRegistration Registration(IdleObservationOptions options) => new(1, "idle-observation", Identity(options), options.JobName, options.PodName, options.PodUid, options.SourceCommit);
    internal static SingleActionCompletion Completion(IdleObservationOptions options) => new(1, "idle-observation", Identity(options), options.JobName, options.PodName, options.PodUid, options.SourceCommit, 5, 0, true);
}
