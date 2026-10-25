using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.VerifyRun.Publication;

internal sealed record DiagnosticPublicationReadOutcome(
    bool HasPayload,
    IReadOnlyDictionary<string, byte[]?> Payloads);

/// <summary>Reads only the fixed, optional failed-publication payload set.</summary>
internal static class DiagnosticPublicationReader
{
    internal static readonly string[] Names =
        ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];

    internal static async Task<DiagnosticPublicationReadOutcome> ReadAsync(
        BlobListing listing,
        string diagnosticPrefix,
        Func<string, CancellationToken, Task<byte[]?>> read,
        long maximumObjectBytes,
        long maximumAggregateBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticPrefix);
        ArgumentNullException.ThrowIfNull(read);
        if (!listing.IsComplete)
            throw new InvalidOperationException(listing.FailureCode ?? "listing_incomplete");

        var present = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in listing.Names)
        {
            if (!name.StartsWith(diagnosticPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException("diagnostic_prefix_escape");
            string relative = name[diagnosticPrefix.Length..];
            if (!Names.Contains(relative, StringComparer.Ordinal))
                throw new InvalidOperationException("diagnostic_publication_membership_invalid");
            present.Add(relative);
        }

        var payloads = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        long total = 0;
        foreach (string name in Names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!present.Contains(name))
            {
                payloads.Add(name, null);
                continue;
            }

            byte[]? bytes = await read(diagnosticPrefix + name, cancellationToken).ConfigureAwait(false);
            if (bytes is null)
                throw new InvalidOperationException("diagnostic_publication_listing_read_mismatch");
            if (bytes.LongLength > maximumObjectBytes)
                throw new InvalidOperationException("diagnostic_publication_object_oversized");
            total = checked(total + bytes.LongLength);
            if (total > maximumAggregateBytes)
                throw new InvalidOperationException("diagnostic_publication_aggregate_oversized");
            payloads.Add(name, bytes);
        }

        return new(present.Count != 0, payloads);
    }
}
