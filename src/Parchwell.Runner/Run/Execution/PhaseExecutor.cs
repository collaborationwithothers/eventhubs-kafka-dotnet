using System.Security.Cryptography;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;

namespace Parchwell.Runner.Run.Execution;

public sealed record PhaseRequest(string Phase, string EnvironmentRunId, string SourceCommit,
    string OutputDirectory, CostInput Costs, bool InitializeLedger = false);
public sealed record PhaseExecution(string Phase, string Result, IReadOnlyList<RepeatExecution> Repeats, bool Destroyed, bool Exported);

public sealed class PhaseExecutor(RunLedger ledger, ITerraformEnvironment environment,
    RepeatExecutor repeats, EvidenceExporter exporter, GenerationGuard guard, TimeProvider clock, ExecutionBudgets? budgets = null,
    Func<string, CancellationToken, Task>? collectCorroboration = null)
{
    public async Task<PhaseExecution> ExecuteAsync(PhaseRequest request, CancellationToken cancellationToken)
    {
        using IDisposable diagnosticScope = FailureDiagnostics.UseStage("apply");
        ArgumentNullException.ThrowIfNull(request);
        if (request.Phase is not ("probe" or "C0" or "C3")) throw new InvalidDataException("phase_selection");
        using var phaseDeadline = new CancellationTokenSource(budgets is null ? TimeSpan.FromSeconds(8400) : budgets.Seconds("phase_seconds") - budgets.Seconds("export_seconds") - budgets.Seconds("destroy_seconds"), clock); // Total phase budget less independent export/destroy reserves.
        using var work = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, phaseDeadline.Token);
        Directory.CreateDirectory(request.OutputDirectory);
        EnvironmentGeneration? existing = await environment.ReadGenerationAsync(work.Token).ConfigureAwait(false);
        LedgerDocument current = await ledger.InitializeAsync(request.Costs, request.SourceCommit, request.InitializeLedger,
            existing is null, request.Phase, work.Token).ConfigureAwait(false);
        PhaseRecord selected = current.Phases[request.Phase];
        if (selected.State == "destroyed")
        {
            bool complete = selected.PrivateExport?.Complete == true && selected.DestroyReceipt?.Complete == true && selected.Repeats.All(repeat => repeat.State == "completed");
            return new(request.Phase, complete ? "already_completed" : "previous_incomplete", [], true, selected.PrivateExport?.Complete == true);
        }
        if (selected.State == "reserved")
        {
            await ledger.MarkInterruptedAsync(request.Phase, request.EnvironmentRunId, "crash_before_apply", work.Token).ConfigureAwait(false);
            throw new InvalidDataException("reserved_phase_interrupted");
        }
        if (selected.State != "not_started") throw new InvalidDataException("phase_already_started_recovery_only");
        if (existing is not null) throw new InvalidDataException("environment_already_exists");
        await ledger.ReserveAsync(request.Phase, request.EnvironmentRunId, request.Costs, work.Token).ConfigureAwait(false);
        await JournalAsync(request.OutputDirectory, "reserved", work.Token).ConfigureAwait(false);
        current = await ledger.MarkApplyStartedAsync(request.Phase, request.EnvironmentRunId, work.Token).ConfigureAwait(false);
        selected = current.Phases[request.Phase];
        DateTimeOffset start = selected.ApplyStartedUtc!.Value;
        DateTimeOffset expiry = selected.ExpiresAt!.Value;
        var completed = new List<RepeatExecution>();
        bool exported = false;
        bool destroyed = false;
        bool failed = false;
        RepeatContext? activeRepeat = null;
        string failureStage = "apply";
        try
        {
            await JournalAsync(request.OutputDirectory, "apply_started", work.Token).ConfigureAwait(false);
            await environment.ApplyAsync(request.EnvironmentRunId, start, expiry, work.Token).ConfigureAwait(false);
            JsonElement outputs = await environment.ReadOutputsAsync(work.Token).ConfigureAwait(false);
            await File.WriteAllBytesAsync(Path.Combine(request.OutputDirectory, "terraform-outputs.json"), JsonSerializer.SerializeToUtf8Bytes(outputs), work.Token).ConfigureAwait(false);
            await JournalAsync(request.OutputDirectory, "outputs_read", work.Token).ConfigureAwait(false);
            await environment.PreflightAsync(outputs, work.Token).ConfigureAwait(false);
            await repeats.PreflightAsync(outputs, work.Token).ConfigureAwait(false);
            await JournalAsync(request.OutputDirectory, "preflight", work.Token).ConfigureAwait(false);
            failureStage = "repeat";
            for (int ordinal = 1; ordinal <= 5; ordinal++)
            {
                using IDisposable repeatScope = FailureDiagnostics.UseStage("repeat");
                using var repeatDeadline = new CancellationTokenSource(budgets?.Seconds("repeat_seconds") ?? TimeSpan.FromSeconds(1500), clock);
                using var repeatWork = CancellationTokenSource.CreateLinkedTokenSource(work.Token, repeatDeadline.Token);
                await ledger.MarkRepeatStartedAsync(request.Phase, request.EnvironmentRunId, ordinal, cancellationToken: repeatWork.Token).ConfigureAwait(false);
                string runId = request.EnvironmentRunId + "-r" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string directory = Path.Combine(request.OutputDirectory, runId);
                Directory.CreateDirectory(directory);
                var context = new RepeatContext(request.Phase, ordinal, runId, request.SourceCommit, directory);
                activeRepeat = context;
                RepeatExecution result = await repeats.ExecuteAsync(context, async (identity, token) =>
                {
                    await ledger.RecordAdmissionAsync(request.Phase, request.EnvironmentRunId, ordinal, identity.InstanceId, token).ConfigureAwait(false);
                }, repeatWork.Token).ConfigureAwait(false);
                foreach ((string name, byte[] bytes) in result.Publication.Objects)
                    await File.WriteAllBytesAsync(Path.Combine(directory, name), bytes, repeatWork.Token).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(directory, "result.json"), result.Result, repeatWork.Token).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(directory, "pod-history-before-export.json"), JsonSerializer.SerializeToUtf8Bytes(result.PodHistory), repeatWork.Token).ConfigureAwait(false);
                EnsurePhaseEvidenceBudget(request.OutputDirectory);
                EvidenceFile[] repeatFiles = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Take(257)
                    .Select(path => new EvidenceFile(Path.GetRelativePath(directory, path).Replace('\\', '/'), path)).ToArray();
                failureStage = "export";
                PrivateExportReceipt repeatExport;
                using (FailureDiagnostics.UseStage("export"))
                    repeatExport = await exporter.ExportRepeatAsync(request.EnvironmentRunId, runId, repeatFiles, repeatWork.Token).ConfigureAwait(false);
                failureStage = "repeat";
                await File.WriteAllBytesAsync(Path.Combine(directory, "repeat-private-export.json"), JsonSerializer.SerializeToUtf8Bytes(repeatExport), repeatWork.Token).ConfigureAwait(false);
                await repeats.ObserveThroughExportAsync(context, result, repeatWork.Token).ConfigureAwait(false);
                await ledger.MarkRepeatCompletedAsync(request.Phase, request.EnvironmentRunId, ordinal,
                    new(result.Outcome.Status, result.Outcome.Reason, ResultHash: Hash(result.Result),
                        PublicationHash: Hash(result.Publication.Objects["index.json"])), repeatWork.Token).ConfigureAwait(false);
                completed.Add(result);
                activeRepeat = null;
                await JournalAsync(request.OutputDirectory, "repeat_" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), repeatWork.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            FailureDiagnostics.Write(exception, Console.Error, failureStage);
            failed = true;
            using IDisposable failureScope = FailureDiagnostics.UseStage(failureStage);
            await BestEffortInterruptedAsync(request, cancellationToken: work.Token).ConfigureAwait(false);
        }
        finally
        {
            using (FailureDiagnostics.UseStage("export"))
            {
                using var exportDeadline = new CancellationTokenSource(budgets?.Seconds("export_seconds") ?? TimeSpan.FromSeconds(300), clock);
                try
                {
                    if (failed && activeRepeat is not null)
                    {
                        // Spend at most one API-call slot collecting failed-worker originals; leave the rest for private export.
                        using var captureDeadline = new CancellationTokenSource(budgets?.Seconds("api_seconds") ?? TimeSpan.FromSeconds(60), clock);
                        using var capture = CancellationTokenSource.CreateLinkedTokenSource(exportDeadline.Token, captureDeadline.Token);
                        try { await repeats.PreserveFailureAsync(activeRepeat, capture.Token, exportDeadline.Token).ConfigureAwait(false); }
                        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
                        {
                            FailureDiagnostics.Write(exception, Console.Error);
                            try
                            {
                                await File.WriteAllTextAsync(Path.Combine(activeRepeat.OutputDirectory, "failure-artifacts-unavailable.json"),
                                    "{\"accepted_counts\":false,\"reason\":\"failure_artifact_collection_incomplete\"}", exportDeadline.Token).ConfigureAwait(false);
                            }
                            catch (Exception writeException) when (writeException is not StackOverflowException and not OutOfMemoryException)
                            {
                                FailureDiagnostics.Write(writeException, Console.Error);
                            }
                        }
                    }
                    if (collectCorroboration is not null)
                    {
                        using var corroborationDeadline = failed
                            ? new CancellationTokenSource(budgets?.Seconds("api_seconds") ?? TimeSpan.FromSeconds(60), clock) : null;
                        using var corroboration = corroborationDeadline is null ? null
                            : CancellationTokenSource.CreateLinkedTokenSource(exportDeadline.Token, corroborationDeadline.Token);
                        try { await collectCorroboration(request.OutputDirectory, corroboration?.Token ?? exportDeadline.Token).ConfigureAwait(false); }
                        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
                        {
                            FailureDiagnostics.Write(exception, Console.Error);
                            // Collection failure preserves available bounded evidence and cannot skip teardown.
                            try
                            {
                                await File.WriteAllTextAsync(Path.Combine(request.OutputDirectory, "kql-unavailable.json"),
                                    "{\"status\":\"unavailable\",\"reason\":\"corroboration_collection_failed\"}", exportDeadline.Token).ConfigureAwait(false);
                            }
                            catch (Exception writeException) when (writeException is not StackOverflowException and not OutOfMemoryException)
                            {
                                FailureDiagnostics.Write(writeException, Console.Error);
                                failed = true;
                            }
                        }
                    }
                    EvidenceFile[] files = Directory.EnumerateFiles(request.OutputDirectory, "*", SearchOption.AllDirectories)
                        .Take(257).Select(path => new EvidenceFile(Path.GetRelativePath(request.OutputDirectory, path).Replace('\\', '/'), path)).ToArray();
                    PrivateExportReceipt receipt = await exporter.ExportAsync(request.EnvironmentRunId, files, exportDeadline.Token).ConfigureAwait(false);
                    await ledger.MarkExportedAsync(request.Phase, request.EnvironmentRunId, receipt, exportDeadline.Token).ConfigureAwait(false);
                    exported = receipt.Complete;
                }
                catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
                {
                    FailureDiagnostics.Write(exception, Console.Error);
                    failed = true;
                    await BestEffortInterruptedAsync(request, "export_incomplete", work.Token).ConfigureAwait(false);
                }
            }
            using IDisposable destroyScope = FailureDiagnostics.UseStage("destroy");
            using var destroyDeadline = new CancellationTokenSource(budgets?.Seconds("destroy_seconds") ?? TimeSpan.FromSeconds(900), clock);
            try
            {
                EnvironmentGeneration? generation = await environment.ReadGenerationAsync(destroyDeadline.Token).ConfigureAwait(false);
                ValidateCleanupGeneration(generation, request.EnvironmentRunId, expiry);
                destroyed = generation is null || await guard.DestroyAsync(generation, false, destroyDeadline.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
            {
                FailureDiagnostics.Write(exception, Console.Error);
                failed = true; destroyed = false;
            }
            try
            {
                var receipt = new DestroyReceipt(request.EnvironmentRunId, clock.GetUtcNow(), destroyed, destroyed ? "destroyed" : "destroy_incomplete");
                EvidenceObjectReceipt stored = await exporter.UploadCleanupReceiptAsync(request.EnvironmentRunId, receipt, destroyDeadline.Token).ConfigureAwait(false);
                await ledger.MarkDestroyedAsync(request.Phase, request.EnvironmentRunId,
                    receipt with { PrivateReceiptPath = stored.Path, PrivateReceiptSha256 = stored.Sha256 }, destroyDeadline.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
            {
                FailureDiagnostics.Write(exception, Console.Error);
                failed = true; destroyed = false;
            }
        }
        return new(request.Phase, failed || !exported || !destroyed ? "failed" : "completed", completed, destroyed, exported);
    }

    private async Task BestEffortInterruptedAsync(PhaseRequest request, string reason = "execution_failure", CancellationToken cancellationToken = default)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180), clock);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try { await ledger.MarkInterruptedAsync(request.Phase, request.EnvironmentRunId, reason, bounded.Token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            FailureDiagnostics.Write(exception, Console.Error);
        }
    }

    private static void ValidateCleanupGeneration(EnvironmentGeneration? generation, string runId, DateTimeOffset expiry)
    {
        if (generation is not null && (generation.RunId != runId || generation.ExpiresAt != expiry))
            throw new InvalidDataException("cleanup_generation_mismatch");
    }

    private static void EnsurePhaseEvidenceBudget(string directory)
    {
        long total = 0;
        int count = 0;
        foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (++count > EvidenceExporter.MaxEntries) throw new InvalidDataException("phase_evidence_entry_limit");
            total = checked(total + new FileInfo(path).Length);
            if (total > EvidenceExporter.MaxPhaseBytes) throw new InvalidDataException("phase_evidence_byte_limit");
        }
    }

    private Task JournalAsync(string directory, string transition, CancellationToken token) =>
        File.AppendAllTextAsync(Path.Combine(directory, "transitions.jsonl"),
            JsonSerializer.Serialize(new { transition, utc = clock.GetUtcNow() }) + "\n", token);
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
