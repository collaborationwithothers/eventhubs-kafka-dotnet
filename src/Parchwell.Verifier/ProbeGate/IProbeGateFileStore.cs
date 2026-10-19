namespace Parchwell.Verifier.ProbeGate;

/// <summary>Opens local gate inputs and creates one immutable output object.</summary>
internal interface IProbeGateFileStore
{
    ValueTask<Stream> OpenReadAsync(string path, CancellationToken cancellationToken);
    ValueTask<Stream> CreateNewAsync(string path, CancellationToken cancellationToken);
}
