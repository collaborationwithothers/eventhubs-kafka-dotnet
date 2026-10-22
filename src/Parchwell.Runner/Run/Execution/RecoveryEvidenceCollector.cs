using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;

namespace Parchwell.Runner.Run.Execution;

public sealed record RecoveryCapture(string CaptureSha256, IReadOnlyList<EvidenceFile> Files);

/// <summary>Recovers available original bytes. A fresh process cannot reconstruct the previous producer watch.</summary>
public sealed partial class RecoveryEvidenceCollector(IProcessRunner process, TerraformEnvironment environment,
    TimeProvider clock, ExecutionBudgets budgets, string privateKubeconfigPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static readonly string[] PublicationNames = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "index.json"];
    private static readonly string[] VerifierNames = ["start-line.json", "broker-inventory.json", "result.json"];

    public async Task<RecoveryCapture> CollectAsync(EnvironmentGeneration expected, string phase, PhaseRecord record,
        string directory, CancellationToken cancellationToken)
    {
        if (phase is not ("probe" or "C0" or "C3") || record.EnvironmentRunId != expected.RunId || record.ExpiresAt != expected.ExpiresAt)
            throw new InvalidDataException("recovery_generation_not_registered");
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()) throw new InvalidDataException("recovery_capture_must_be_fresh");
        Directory.CreateDirectory(directory);
        var reasons = new HashSet<string>(StringComparer.Ordinal) { "historical_watch_continuity_unavailable" };
        try
        {
            JsonElement outputs = await environment.CaptureRecoverySnapshotAsync(expected, Path.Combine(directory, "terraform"), cancellationToken).ConfigureAwait(false);
            await EnsureGenerationAsync(expected, cancellationToken).ConfigureAwait(false);
            await CollectPodsAsync(outputs, expected, record, directory, reasons, cancellationToken).ConfigureAwait(false);
            await EnsureGenerationAsync(expected, cancellationToken).ConfigureAwait(false);
            await CollectBlobsAsync(outputs, phase, record, directory, reasons, cancellationToken).ConfigureAwait(false);
            await EnsureGenerationAsync(expected, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException && !cancellationToken.IsCancellationRequested)
        { reasons.Add("recovery_collection_incomplete"); }
        await File.WriteAllBytesAsync(Path.Combine(directory, "collection-receipt.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 1, environment_run_id = expected.RunId, phase, observed_at = clock.GetUtcNow(),
            historical_watch_continuity = "unavailable", accepted_counts = false, complete = false,
            reasons = reasons.Order(StringComparer.Ordinal).ToArray()
        }, JsonOptions), cancellationToken).ConfigureAwait(false);
        EvidenceFile[] files = Files(directory);
        var entries = new List<EvidenceManifestEntry>();
        foreach (EvidenceFile file in files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
        {
            await using var stream = File.OpenRead(file.LocalPath);
            entries.Add(new(file.RelativePath, stream.Length, Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false))));
        }
        string hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(entries, JsonOptions)));
        return new(hash, files);
    }

    private async Task CollectPodsAsync(JsonElement outputs, EnvironmentGeneration expected, PhaseRecord record,
        string directory, HashSet<string> reasons, CancellationToken token)
    {
        IReadOnlyDictionary<string, string> overlay;
        try
        {
            overlay = await AksKubeconfigBinding.BindReadOnlyAsync(process, outputs, privateKubeconfigPath,
                budgets.Seconds("api_seconds"), token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException && !token.IsCancellationRequested)
        { reasons.Add("recovery_cluster_unavailable"); return; }
        await EnsureGenerationAsync(expected, token).ConfigureAwait(false);
        string eventDirectory = Path.Combine(directory, "kubernetes"); Directory.CreateDirectory(eventDirectory);
        await TryCommandAsync("kubectl", ["get", "--raw", "/api/v1/namespaces/parchwell/events?fieldSelector=involvedObject.kind%3DPod&limit=10000"],
            Path.Combine(eventDirectory, "pod-events.json"), overlay, reasons, token).ConfigureAwait(false);
        foreach (RepeatRecord repeat in record.Repeats)
        {
            string run = repeat.RunId ?? throw new InvalidDataException("recovery_run_id_missing");
            string target = Path.Combine(directory, run, "kubernetes"); Directory.CreateDirectory(target);
            string selector = "app.kubernetes.io/instance=" + run;
            foreach (string resource in new[] { "pods", "jobs" })
            {
                string api = resource == "pods" ? "/api/v1" : "/apis/batch/v1";
                string uri = api + "/namespaces/parchwell/" + resource + "?labelSelector=" + Uri.EscapeDataString(selector) + "&limit=10000";
                await TryCommandAsync("kubectl", ["get", "--raw", uri], Path.Combine(target, resource + ".json"), overlay, reasons, token).ConfigureAwait(false);
            }
            // https://github.com/kubernetes/kubectl/blob/v0.35.0/pkg/cmd/logs/logs.go#L194-L201
            // https://github.com/kubernetes/kubectl/blob/v0.35.0/pkg/polymorphichelpers/logsforobject.go#L71-L87
            await TryCommandAsync("kubectl", ["logs", "-n", "parchwell", "-l", selector, "--all-containers=true", "--prefix=true", "--tail=-1", "--ignore-errors=true", "--limit-bytes=16777217"],
                Path.Combine(target, "all-worker-stdout.log"), overlay, reasons, token).ConfigureAwait(false);
            _ = Files(directory);
        }
    }

    private async Task CollectBlobsAsync(JsonElement outputs, string phase, PhaseRecord record, string directory,
        HashSet<string> reasons, CancellationToken token)
    {
        string account;
        try
        {
            JsonElement blob = Output(outputs, "blob");
            if (!Uri.TryCreate(WorkerIdentity.Text(blob, "account_url"), UriKind.Absolute, out Uri? uri) || uri.Scheme != "https" ||
                !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/" || !AccountHost().IsMatch(uri.Host))
                throw new InvalidDataException("recovery_worker_account_invalid");
            JsonElement containers = blob.GetProperty("containers");
            if (WorkerIdentity.Text(containers, "ledger") != "ledger" || WorkerIdentity.Text(containers, "verifier") != "verifier")
                throw new InvalidDataException("recovery_worker_containers_invalid");
            account = uri.Host.Split('.')[0];
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or InvalidDataException)
        { reasons.Add("recovery_worker_storage_unavailable"); return; }
        foreach (RepeatRecord repeat in record.Repeats)
        {
            string run = repeat.RunId ?? throw new InvalidDataException("recovery_run_id_missing");
            string target = Path.Combine(directory, run, "blobs");
            foreach (string name in VerifierNames)
                await DownloadAsync("verifier", phase + "/" + run + "/" + name, "verifier/" + name).ConfigureAwait(false);
            if (repeat.AdmittedIdentity is { } producer)
                foreach (string name in PublicationNames)
                    await DownloadAsync("ledger", "A01/" + (phase == "probe" ? "C0" : phase) + "/" + run + "/" + producer + "/" + name, "ledger/" + name).ConfigureAwait(false);
            else reasons.Add("recovery_admission_unavailable");

            async Task DownloadAsync(string container, string name, string relative)
            {
                try
                {
                    string path = Path.Combine(target, relative);
                    long retained = Directory.Exists(target) ? Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length) : 0;
                    long remaining = budgets.Number("repeat_input_bytes") - retained;
                    if (remaining <= 0) throw new InvalidDataException("recovery_repeat_input_limit");
                    await WorkerBlobDownloader.DownloadAsync(process, account, container, name, path,
                        Math.Min(budgets.Number("producer_artifact_bytes"), remaining), budgets.Seconds("api_seconds"), token).ConfigureAwait(false);
                    _ = Files(directory);
                }
                catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException && !token.IsCancellationRequested)
                { reasons.Add("recovery_worker_artifact_unavailable"); }
            }
        }
    }

    private async Task TryCommandAsync(string tool, IReadOnlyList<string> arguments, string path,
        IReadOnlyDictionary<string, string> overlay, HashSet<string> reasons, CancellationToken token)
    {
        try
        {
            ProcessResult result = await process.RunAsync(new(tool, arguments, budgets.Seconds("api_seconds"), Environment: overlay), token).ConfigureAwait(false);
            await File.WriteAllTextAsync(path, result.StandardOutput, token).ConfigureAwait(false);
            if (result.StandardError.Length > 0)
            {
                await File.WriteAllTextAsync(path + ".stderr.log", result.StandardError, token).ConfigureAwait(false);
                reasons.Add("recovery_cluster_diagnostics_present");
            }
            if (result.ExitCode != 0 || result.TimedOut) reasons.Add("recovery_cluster_artifact_unavailable");
            if (path.EndsWith(".json", StringComparison.Ordinal))
            {
                using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("metadata", out JsonElement metadata) && metadata.TryGetProperty("continue", out JsonElement continuation) && !string.IsNullOrEmpty(continuation.GetString()))
                    reasons.Add("recovery_cluster_snapshot_incomplete");
                if (root.TryGetProperty("items", out JsonElement items) && items.GetArrayLength() > budgets.Number("watch_events"))
                    throw new InvalidDataException("recovery_event_limit");
            }
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException && !token.IsCancellationRequested)
        { reasons.Add("recovery_cluster_artifact_unavailable"); }
    }

    private async Task EnsureGenerationAsync(EnvironmentGeneration expected, CancellationToken token) =>
        GenerationGuard.EnsureSame(expected, await environment.ReadGenerationAsync(token).ConfigureAwait(false) ?? throw new InvalidDataException("recovery_generation_disappeared"));

    private static EvidenceFile[] Files(string directory)
    {
        EvidenceFile[] files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Take(EvidenceExporter.MaxEntries + 1)
            .Select(path => new EvidenceFile(Path.GetRelativePath(directory, path).Replace('\\', '/'), path)).ToArray();
        if (files.Length > EvidenceExporter.MaxEntries || files.Sum(file => new FileInfo(file.LocalPath).Length) > EvidenceExporter.MaxPhaseBytes ||
            files.Any(file => new FileInfo(file.LocalPath).Length > EvidenceExporter.MaxFileBytes)) throw new InvalidDataException("recovery_evidence_limit");
        return files;
    }
    private static JsonElement Output(JsonElement values, string key) { JsonElement value = values.GetProperty(key); return value.TryGetProperty("value", out JsonElement nested) ? nested : value; }
    [GeneratedRegex("^[a-z0-9]{3,24}\\.blob\\.core\\.windows\\.net$", RegexOptions.CultureInvariant)]
    private static partial Regex AccountHost();
}
