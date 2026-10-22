using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;

namespace Parchwell.Runner.Run.Execution;

public sealed class SweepExecutor(RunLedger ledger, ITerraformEnvironment environment,
    GenerationGuard guard, EvidenceExporter exporter, TimeProvider clock, RecoveryEvidenceCollector recovery,
    ExecutionBudgets? budgets = null)
{
    public async Task<bool> ExecuteAsync(string outputDirectory, CancellationToken cancellationToken)
    {
        EnvironmentGeneration? aged = await environment.ReadGenerationAsync(cancellationToken).ConfigureAwait(false);
        if (aged is null || clock.GetUtcNow() <= aged.ExpiresAt) return false;
        LedgerDocument document = await ledger.ReadAsync(cancellationToken).ConfigureAwait(false);
        KeyValuePair<string, PhaseRecord>[] matches = document.Phases.Where(item => item.Value.EnvironmentRunId == aged.RunId &&
            item.Value.ExpiresAt == aged.ExpiresAt && item.Value.ApplyStartedUtc is not null).ToArray();
        if (matches.Length != 1 || matches[0].Value.DestroyReceipt?.Complete == true)
            throw new InvalidDataException("sweep_generation_not_registered");
        string phase = matches[0].Key;
        bool exportFailed = false;
        PrivateExportReceipt? recovered = null;
        using (var deadline = new CancellationTokenSource(budgets?.Seconds("export_seconds") ?? TimeSpan.FromSeconds(300), clock))
        using (var export = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token))
        {
            try
            {
                string capture = Path.Combine(outputDirectory, "recovery-" + Guid.NewGuid().ToString("N"));
                RecoveryCapture collection = await recovery.CollectAsync(aged, phase, matches[0].Value, capture, export.Token).ConfigureAwait(false);
                recovered = await exporter.ExportRecoveryAsync(aged.RunId, collection.CaptureSha256, collection.Files, export.Token).ConfigureAwait(false);
                // A recovery snapshot never replaces the immutable original phase manifest or supplies missing watch proof.
                await ledger.MarkInterruptedAsync(phase, aged.RunId, "recovery_history_unavailable", export.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException) { exportFailed = true; }
        }
        using var cleanup = new CancellationTokenSource(budgets?.Seconds("destroy_seconds") ?? TimeSpan.FromSeconds(900), clock);
        bool destroyed;
        try { destroyed = await guard.DestroyAsync(aged, true, cleanup.Token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException) { destroyed = false; }
        var receipt = new DestroyReceipt(aged.RunId, clock.GetUtcNow(), destroyed,
            exportFailed ? "recovery_export_incomplete" : destroyed ? "destroyed" : "destroy_incomplete", RecoveryExport: recovered);
        EvidenceObjectReceipt stored = await exporter.UploadCleanupReceiptAsync(aged.RunId, receipt, cleanup.Token).ConfigureAwait(false);
        await ledger.MarkDestroyedAsync(phase, aged.RunId, receipt with { PrivateReceiptPath = stored.Path, PrivateReceiptSha256 = stored.Sha256 }, cleanup.Token).ConfigureAwait(false);
        if (exportFailed || !destroyed) throw new IOException(exportFailed ? "sweep_export_incomplete" : "sweep_destroy_incomplete");
        return true;
    }
}
