using Azure.Storage.Blobs;
using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.VerifyRun.Storage;

/// <summary>Reads a fixed payload set beneath the configured ledger prefix.</summary>
internal sealed class BlobLedgerArtifactSource : ILedgerArtifactSource
{
    private readonly BlobContainerClient _container;
    private readonly string _prefix;
    private readonly long _maxArtifactBytes;

    public BlobLedgerArtifactSource(BlobContainerClient container, string artifactPrefix, long maxArtifactBytes)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPrefix);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxArtifactBytes);
        _container = container;
        _prefix = artifactPrefix.EndsWith('/') ? artifactPrefix : artifactPrefix + "/";
        _maxArtifactBytes = maxArtifactBytes;
    }

    public Task<byte[]?> ReadAsync(string logicalFixedName, CancellationToken cancellationToken)
    {
        if (logicalFixedName is not ("index.json" or "ledger.jsonl" or "manifest.json" or "statistics.jsonl" or
            "request-lifecycle.jsonl" or "capabilities.json"))
            throw new ArgumentException("verifier_artifact_name_not_fixed", nameof(logicalFixedName));
        return BoundedBlobReader.ReadAsync(_container.GetBlobClient(_prefix + logicalFixedName),
            _maxArtifactBytes, cancellationToken);
    }
}
