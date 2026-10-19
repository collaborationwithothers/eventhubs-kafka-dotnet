namespace Parchwell.Verifier.VerifyRun.Storage;

/// <summary>Reads only fixed producer artifacts; absence is distinct from a storage failure.</summary>
internal interface ILedgerArtifactSource
{
    Task<byte[]?> ReadAsync(string logicalFixedName, CancellationToken cancellationToken);
}
