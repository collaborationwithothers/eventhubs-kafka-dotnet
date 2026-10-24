namespace Parchwell.Verifier.Common.Storage;

/// <summary>Reads start-line notes and publishes verifier artifacts once.</summary>
internal interface IVerifierArtifactStore
{
    Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken);
    Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken);
    Task<byte[]?> ReadStartLineAsync(string name, CancellationToken cancellationToken) => ReadAsync(name, cancellationToken);
    Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken);
}
