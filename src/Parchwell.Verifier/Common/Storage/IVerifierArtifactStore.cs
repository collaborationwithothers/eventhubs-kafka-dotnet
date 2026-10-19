namespace Parchwell.Verifier.Common.Storage;

/// <summary>Reads start-line notes and publishes verifier artifacts once.</summary>
internal interface IVerifierArtifactStore
{
    Task<byte[]?> ReadStartLineAsync(string name, CancellationToken cancellationToken);
    Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken);
}
