namespace Parchwell.Verifier.Common.Storage;

/// <summary>Lists and reads evidence beneath one validated repeat prefix in the ledger container.</summary>
internal interface IProducerArtifactStore
{
    Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken);
    Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken);
}
