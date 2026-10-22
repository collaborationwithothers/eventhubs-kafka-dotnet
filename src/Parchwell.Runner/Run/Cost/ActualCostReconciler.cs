using System.Text.Json;
using Parchwell.Runner.Run.Evidence;

namespace Parchwell.Runner.Run.Cost;

/// <summary>Settled cost scope comes independently from the verified original Terraform output.</summary>
public sealed class ActualCostReconciler(RunLedger ledger, EvidenceExporter exporter)
{
    public async Task<LedgerDocument> ReconcileAsync(string phase, string recordFile, CancellationToken cancellationToken = default)
    {
        if (new FileInfo(recordFile).Length > 1_048_576) throw new InvalidDataException("actual_cost_record_limit");
        byte[] recordBytes = await ReadBoundedRecordAsync(recordFile, cancellationToken).ConfigureAwait(false);
        var actual = JsonSerializer.Deserialize<ActualCostRecord>(recordBytes, LedgerJson.Options)
            ?? throw new InvalidDataException("actual_cost_record_missing");
        var document = await ledger.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (!document.Phases.TryGetValue(phase, out var selected) || selected.EnvironmentRunId != actual.EnvironmentRunId || selected.PrivateExport?.Complete != true)
            throw new InvalidDataException("actual_cost_phase_scope_unavailable");
        var original = await exporter.ReadVerifiedFileAsync(actual.EnvironmentRunId, "terraform-outputs.json", selected.PrivateExport, cancellationToken).ConfigureAwait(false);
        using var outputs = JsonDocument.Parse(original);
        var aks = outputs.RootElement.GetProperty("aks");
        if (aks.TryGetProperty("value", out var wrapped)) aks = wrapped;
        var clusterId = aks.GetProperty("id").GetString() ?? throw new InvalidDataException("actual_cost_phase_scope_unavailable");
        var provider = clusterId.IndexOf("/providers/", StringComparison.OrdinalIgnoreCase);
        if (provider < 1) throw new InvalidDataException("actual_cost_phase_scope_unavailable");
        var scope = clusterId[..provider];
        var segments = scope.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 4 || !segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(segments[1], out _) ||
            !segments[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(segments[3]))
            throw new InvalidDataException("actual_cost_phase_scope_unavailable");
        // Retain the received formatting and bytes before committing normalized cost fields.
        await exporter.UploadActualCostAsync(actual.EnvironmentRunId, recordBytes, cancellationToken).ConfigureAwait(false);
        return await ledger.ReconcileAsync(phase, actual, scope, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadBoundedRecordAsync(string path, CancellationToken cancellationToken)
    {
        const int limit = 1_048_576;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length > limit) throw new InvalidDataException("actual_cost_record_limit");
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > limit) throw new InvalidDataException("actual_cost_record_limit");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}
