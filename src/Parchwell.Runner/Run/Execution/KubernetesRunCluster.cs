using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Deployment;

namespace Parchwell.Runner.Run.Execution;

public sealed class KubernetesRunCluster(IProcessRunner process, DeploymentGenerator generator,
    ChartRenderer renderer, JsonElement runtime, string chartDirectory, TimeProvider clock, ExecutionBudgets? budgets = null, string? privateKubeconfigPath = null, string? toolReceiptDirectory = null) : IRunCluster, IDisposable
{
    private static readonly string[] PublicationNames = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "index.json"];
    private static readonly string[] VerifierNames = ["start-line.json", "broker-inventory.json", "result.json"];
    private readonly Dictionary<(string RunId, string Container, string Name), string> downloaded = [];
    private readonly HashSet<string> releaseAttempted = new(StringComparer.Ordinal);
    private JsonElement currentRuntime = runtime;
    private IReadOnlyDictionary<string, string>? clusterEnvironment;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> versions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WatchSession> watches = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DeploymentFiles> files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProducerPod> producers = new(StringComparer.Ordinal);
    // Only the active repeat's three fixed worker modes are cached. Prior repeats have persisted their canonical files.
    // Each original stream retains the existing bounded process output ceiling.
    private readonly Dictionary<string, WorkerObservation> observations = new(StringComparer.Ordinal);
    private string? observedRunId;

    public async Task PreflightAsync(JsonElement outputs, CancellationToken cancellationToken)
    {
        await BindClusterAsync(outputs, cancellationToken).ConfigureAwait(false);
        ProcessResult dns = await KubectlAsync(["get", "pods", "-n", "kube-system", "-l", "k8s-app=kube-dns", "-o", "json"], cancellationToken).ConfigureAwait(false);
        using JsonDocument dnsPods = JsonDocument.Parse(dns.StandardOutput);
        if (dnsPods.RootElement.GetProperty("items").GetArrayLength() == 0) throw new InvalidDataException("dns_pods_missing");
        ProcessResult ns = await KubectlAsync(["get", "namespace", "kube-system", "-o", "json"], cancellationToken).ConfigureAwait(false);
        using JsonDocument nsDocument = JsonDocument.Parse(ns.StandardOutput);
        if (nsDocument.RootElement.GetProperty("metadata").GetProperty("labels").GetProperty("kubernetes.io/metadata.name").GetString() != "kube-system")
            throw new InvalidDataException("dns_namespace_selector_mismatch");
        JsonElement ips = Output(outputs, "private_endpoint_ips");
        string ehHost = WorkerIdentity.Text(Output(outputs, "eventhubs"), "namespace_name") + ".servicebus.windows.net";
        string blobHost = new Uri(WorkerIdentity.Text(Output(outputs, "blob"), "account_url")).Host;
        foreach ((string host, string key) in new[] { (ehHost, "eventhubs"), (blobHost, "blob") })
        {
            using var dnsDeadline = new CancellationTokenSource(budgets?.Seconds("api_seconds") ?? TimeSpan.FromSeconds(60), clock);
            using var dnsWork = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, dnsDeadline.Token);
            System.Net.IPAddress[] addresses = await System.Net.Dns.GetHostAddressesAsync(host, dnsWork.Token).ConfigureAwait(false);
            if (addresses.Length != 1 || addresses[0].ToString() != WorkerIdentity.Text(ips, key)) throw new InvalidDataException("private_endpoint_dns_mismatch");
        }
    }
    internal async Task BindClusterAsync(JsonElement outputs, CancellationToken cancellationToken)
    {
        clusterEnvironment = null;
        clusterEnvironment = await AksKubeconfigBinding.BindAsync(process, outputs,
            privateKubeconfigPath ?? throw new InvalidDataException("explicit_private_kubeconfig_required"),
            budgets?.Seconds("api_seconds") ?? TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);
        if (toolReceiptDirectory is not null)
        {
            Directory.CreateDirectory(toolReceiptDirectory);
            foreach (string name in new[] { "kubelogin-version.txt", "kubelogin-convert-help.txt" })
            {
                byte[] receipt = await File.ReadAllBytesAsync(Path.Combine(Path.GetDirectoryName(privateKubeconfigPath!)!, name), cancellationToken).ConfigureAwait(false);
                await File.WriteAllBytesAsync(Path.Combine(toolReceiptDirectory, name), receipt, cancellationToken).ConfigureAwait(false);
            }
        }
        JsonObject updated = JsonNode.Parse(currentRuntime.GetRawText())!.AsObject();
        updated["ephemeral_outputs"] = JsonNode.Parse(outputs.GetRawText());
        currentRuntime = JsonSerializer.SerializeToElement(updated);
    }

    private static JsonElement Output(JsonElement outputs, string name)
    { JsonElement value = outputs.GetProperty(name); return value.TryGetProperty("value", out JsonElement nested) ? nested : value; }

    public async Task<PodAdmission> EstablishProducerWatchAsync(RepeatContext context, CancellationToken cancellationToken)
    {
        ProcessResult list = await KubectlAsync(["get", "pods", "-n", "parchwell", "-l", Selector(context), "-o", "json"], cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(list.StandardOutput);
        string version = WorkerIdentity.Text(document.RootElement.GetProperty("metadata"), "resourceVersion");
        var admission = new PodAdmission(context.RunId, version);
        foreach (JsonElement item in document.RootElement.GetProperty("items").EnumerateArray())
            admission.Observe(new(version, "ADDED", Pod(item)));
        if (!versions.TryAdd(context.RunId, version)) throw new InvalidDataException("producer_watch_already_started");
        await ObserveWatchAsync(context, admission, cancellationToken).ConfigureAwait(false);
        var watchDeadline = new CancellationTokenSource(budgets?.Seconds("repeat_seconds") ?? TimeSpan.FromSeconds(1500), clock);
        var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, watchDeadline.Token);
        watches.Add(context.RunId, new(stop, WatchLoopAsync(context, admission, stop.Token), watchDeadline));
        return admission;
    }

    public async Task CreateProducerAsync(RepeatContext context, CancellationToken cancellationToken)
    {
        // Placeholder identity is superseded from the independent watch before release.
        DeploymentFiles deployment = await GenerateAsync(context, "loadgen", context.RunId + "-pending", [], cancellationToken).ConfigureAwait(false);
        files[context.RunId] = deployment;
        await ApplyChartAsync(context, deployment, "loadgen", cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProducerPod> ObserveProducerAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken)
    {
        long start = clock.GetTimestamp();
        while (clock.GetElapsedTime(start) < (budgets?.Seconds("admission_seconds") ?? TimeSpan.FromSeconds(180)) - TimeSpan.FromSeconds(60))
        {
            if (watches[context.RunId].Work.IsFaulted) await watches[context.RunId].Work.ConfigureAwait(false);
            if (admission.Events.Any(item => item.Pod is not null))
            {
                ProducerPod producer = admission.Producer;
                producers[context.RunId] = producer;
                return producer;
            }
            await Task.Delay(budgets?.Seconds("kubernetes_poll_seconds") ?? TimeSpan.FromSeconds(1), clock, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("producer_admission_deadline");
    }

    public async Task<string> GenerateComparisonAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken)
    {
        DeploymentFiles deployment = await GenerateAsync(context, "loadgen", identity.InstanceId, [], cancellationToken).ConfigureAwait(false);
        files[context.RunId] = deployment;
        foreach ((string role, string path) in new[] { ("loadgen", deployment.LoadGenValuesPath), ("verifier", deployment.VerifierValuesPath) })
        {
            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            await PatchAsync(context.RunId + "-" + role + "-values", new { binaryData = new Dictionary<string, string> { ["generated-values.yaml"] = Convert.ToBase64String(bytes) } }, cancellationToken).ConfigureAwait(false);
        }
        return Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(deployment.LoadGenValuesPath, cancellationToken).ConfigureAwait(false)));
    }

    public async Task<StartLineExecution> StartLineAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken)
    {
        DeploymentFiles deployment = await GenerateAsync(context, "start-line", identity.InstanceId, [], cancellationToken).ConfigureAwait(false);
        await ApplyChartAsync(context, deployment, "start-line", cancellationToken).ConfigureAwait(false);
        WorkerExecution worker = await WaitJobAsync(context, "start-line", budgets?.Number("start_line_job_seconds") ?? 150, cancellationToken).ConfigureAwait(false);
        byte[] inventory = worker.Process.ExitCode == 0
            ? await DownloadAsync(context, "verifier", context.Phase + "/" + context.RunId + "/broker-inventory.json", "broker-inventory.json", cancellationToken).ConfigureAwait(false) : [];
        byte[] boundary = worker.Process.ExitCode == 0
            ? await DownloadAsync(context, "verifier", context.Phase + "/" + context.RunId + "/start-line.json", "start-line.json", cancellationToken).ConfigureAwait(false) : [];
        return new(worker with { Result = boundary }, inventory);
    }

    public async Task ReleaseAsync(RepeatContext context, byte[] permit, CancellationToken cancellationToken)
    {
        releaseAttempted.Add(context.RunId);
        await PatchAsync(context.RunId + "-admission", new { data = new Dictionary<string, string> { ["admission.json"] = System.Text.Encoding.UTF8.GetString(permit) } }, cancellationToken).ConfigureAwait(false);
    }

    public Task<WorkerExecution> WaitProducerAsync(RepeatContext context, CancellationToken cancellationToken) => WaitJobAsync(context, "loadgen", budgets?.Number("loadgen_job_seconds") ?? 840, cancellationToken);

    public async Task<ProducerPublication> DownloadPublicationAsync(RepeatContext context, CancellationToken cancellationToken)
    {
        string producer = producers[context.RunId].Name;
        string prefix = "A01/" + (context.Phase == "probe" ? "C0" : context.Phase) + "/" + context.RunId + "/" + producer + "/";
        ProcessResult listing = await AzAsync(["storage", "blob", "list", "--account-name", AccountName(), "--container-name", "ledger", "--prefix", prefix,
            "--auth-mode", "login", "--num-results", "7", "-o", "json"], cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(listing.StandardOutput);
        JsonElement members = document.RootElement;
        if (members.GetArrayLength() != 6) throw new InvalidDataException("publication_prefix_member_set");
        var objects = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long total = 0;
        foreach (JsonElement member in members.EnumerateArray())
        {
            string name = WorkerIdentity.Text(member, "name");
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || name[prefix.Length..].Contains('/', StringComparison.Ordinal)) throw new InvalidDataException("publication_prefix_member_name");
            string relative = name[prefix.Length..];
            byte[] bytes = await DownloadAsync(context, "ledger", name, relative, cancellationToken).ConfigureAwait(false);
            total = checked(total + bytes.LongLength);
            if (total > 1073741824) throw new InvalidDataException("publication_total_limit");
            objects.Add(relative, bytes);
        }
        return new(objects);
    }

    public async Task<WorkerExecution> VerifyAsync(RepeatContext context, WorkerIdentity identity, IReadOnlyList<string> brokers, CancellationToken cancellationToken)
    {
        DeploymentFiles deployment = await GenerateAsync(context, "verify", identity.InstanceId, brokers, cancellationToken).ConfigureAwait(false);
        await ApplyChartAsync(context, deployment, "verify", cancellationToken).ConfigureAwait(false);
        WorkerExecution worker = await WaitJobAsync(context, "verify", budgets?.Number("verify_job_seconds") ?? 750, cancellationToken).ConfigureAwait(false);
        byte[] result = await DownloadAsync(context, "verifier", context.Phase + "/" + context.RunId + "/result.json", "result.json", cancellationToken).ConfigureAwait(false);
        return worker with { Result = result };
    }

    public async Task PreserveObservedWorkersAsync(RepeatContext context, CancellationToken cancellationToken)
    {
        if (observedRunId != context.RunId) return;
        bool incomplete = false;
        foreach ((string mode, WorkerObservation observation) in observations)
        {
            var originals = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [mode + "-pod.json"] = observation.Pod.StandardOutput,
                [mode + "-pod-stderr.log"] = observation.Pod.StandardError,
                [mode + "-logs-command.json"] = JsonSerializer.Serialize(new
                {
                    pod_command = new { exit_code = observation.Pod.ExitCode, timed_out = observation.Pod.TimedOut },
                    available = observation.Logs is not null,
                    exit_code = observation.Logs?.ExitCode, timed_out = observation.Logs?.TimedOut,
                    reason = observation.LogFailure
                })
            };
            if (observation.ExitCode is { } code)
                originals[mode + "-termination.json"] = JsonSerializer.Serialize(new { exit_code = code, timed_out = false });
            if (observation.Logs is { } logs)
            {
                originals[mode + "-stdout.log"] = logs.StandardOutput;
                originals[mode + "-logs-stderr.log"] = logs.StandardError;
            }
            foreach ((string name, string content) in originals)
                try { await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, name), content, cancellationToken).ConfigureAwait(false); }
                catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
                { incomplete = true; }
        }
        if (incomplete) throw new IOException("worker_observation_retention_incomplete");
    }

    public async Task PreserveAvailableArtifactsAsync(RepeatContext context, CancellationToken cancellationToken)
    {
        string directory = Path.Combine(context.OutputDirectory, "failure-artifacts");
        Directory.CreateDirectory(directory);
        var entries = new List<object>();
        var artifacts = VerifierNames.Select(name => (Container: "verifier", Name: context.Phase + "/" + context.RunId + "/" + name, Filename: name)).ToList();
        bool producerKnown = producers.TryGetValue(context.RunId, out ProducerPod? producer);
        if (producerKnown)
        {
            string prefix = "A01/" + (context.Phase == "probe" ? "C0" : context.Phase) + "/" + context.RunId + "/" + producer!.Name + "/";
            var publication = PublicationNames.Select(name => ("ledger", prefix + name, name));
            if (releaseAttempted.Contains(context.RunId)) artifacts.InsertRange(0, publication);
            else artifacts.AddRange(publication);
        }
        try
        {
            foreach ((string container, string name, string filename) in artifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = Path.Combine(directory, container, filename);
                string status = "unavailable";
                try
                {
                    if (downloaded.TryGetValue((context.RunId, container, name), out string? retained))
                        path = retained;
                    else
                    {
                        long retainedBytes = RetainedInputBytes(context.OutputDirectory);
                        long remaining = (budgets?.Number("repeat_input_bytes") ?? 1073741824) - retainedBytes;
                        if (remaining <= 0) throw new InvalidDataException("failure_repeat_input_limit");
                        await WorkerBlobDownloader.DownloadAsync(process, AccountName(), container, name, path,
                            Math.Min(budgets?.Number("producer_artifact_bytes") ?? 268435456, remaining),
                            budgets?.Seconds("api_seconds") ?? TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);
                        downloaded.Add((context.RunId, container, name), path);
                    }
                    status = "retained";
                }
                catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException && !cancellationToken.IsCancellationRequested) { }
                entries.Add(new { container, name = filename, status,
                    path = File.Exists(path) ? Path.GetRelativePath(context.OutputDirectory, path).Replace('\\', '/') : null });
            }
        }
        finally
        {
            await File.WriteAllBytesAsync(Path.Combine(directory, "collection-receipt.json"), JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_version = 1, accepted_counts = false, execution_completed = false,
                producer_identity_available = producerKnown, artifacts = entries,
                reason = "failed_repeat_diagnostic_artifacts"
            }), cancellationToken).ConfigureAwait(false);
        }
    }

    private static long RetainedInputBytes(string directory)
    {
        IEnumerable<string> originals = PublicationNames.Concat(VerifierNames).Select(name => Path.Combine(directory, name)).Where(File.Exists);
        string failed = Path.Combine(directory, "failure-artifacts");
        if (Directory.Exists(failed)) originals = originals.Concat(Directory.EnumerateFiles(failed, "*", SearchOption.AllDirectories));
        return originals.Sum(path => new FileInfo(path).Length);
    }

    public async Task CompleteProducerWatchAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken)
    {
        WatchSession session = watches[context.RunId];
        await session.Stop.CancelAsync().ConfigureAwait(false);
        try { await session.Work.ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && session.Stop.IsCancellationRequested) { }
        // Re-read the event stream from its last acknowledged version after export; cancellation cannot discard the final events.
        await ObserveWatchAsync(context, admission, cancellationToken).ConfigureAwait(false);
    }

    private async Task WatchLoopAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ObserveWatchAsync(context, admission, cancellationToken).ConfigureAwait(false);
            await Task.Delay(budgets?.Seconds("kubernetes_poll_seconds") ?? TimeSpan.FromSeconds(1), clock, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        foreach (WatchSession session in watches.Values)
        {
            session.Stop.Cancel();
            try { session.Work.GetAwaiter().GetResult(); }
            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException) { }
            session.Stop.Dispose();
            session.Deadline.Dispose();
        }
    }

    private sealed record WatchSession(CancellationTokenSource Stop, Task Work, CancellationTokenSource Deadline);

    private async Task ObserveWatchAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken)
    {
        // kubectl v0.35.0 pkg/rawhttp/raw.go passes RequestURI directly and copies the complete response stream.
        // A raw API watch preserves our independently captured RV; ordinary `get --watch` first obtains a new list RV.
        string uri = "/api/v1/namespaces/parchwell/pods?watch=true&resourceVersion=" + Uri.EscapeDataString(versions[context.RunId]) +
            "&labelSelector=" + Uri.EscapeDataString(Selector(context)) + "&timeoutSeconds=1&allowWatchBookmarks=true";
        ProcessResult result = await process.RunAsync(new("kubectl", ["get", "--raw", uri, "--request-timeout=60s"],
            budgets?.Seconds("api_seconds") ?? TimeSpan.FromSeconds(60), Environment: RequireClusterEnvironment()), cancellationToken).ConfigureAwait(false);
        if (result.TimedOut || result.ExitCode != 0) throw new InvalidDataException("pod_watch_lost");
        foreach (JsonElement item in ReadDocuments(result.StandardOutput))
        {
            string type = WorkerIdentity.Text(item, "type");
            JsonElement value = item.GetProperty("object");
            if (type == "ERROR") { admission.Observe(new("", "ERROR", null)); continue; }
            string version = WorkerIdentity.Text(value.GetProperty("metadata"), "resourceVersion");
            admission.Observe(new(version, type, type == "BOOKMARK" ? null : Pod(value)));
            versions[context.RunId] = version;
        }
    }

    private async Task<WorkerExecution> WaitJobAsync(RepeatContext context, string mode, int seconds, CancellationToken cancellationToken)
    {
        long origin = clock.GetTimestamp();
        while (clock.GetElapsedTime(origin) < TimeSpan.FromSeconds(seconds))
        {
            ProcessResult result = await process.RunAsync(new("kubectl",
                ["get", "pods", "-n", "parchwell", "-l", "job-name=" + context.RunId + "-" + mode, "-o", "json"],
                budgets?.Seconds("api_seconds") ?? TimeSpan.FromSeconds(60), Environment: RequireClusterEnvironment()), cancellationToken).ConfigureAwait(false);
            if (observedRunId != context.RunId)
            {
                observations.Clear();
                observedRunId = context.RunId;
            }
            var observation = new WorkerObservation(result);
            observations[mode] = observation;
            if (result.ExitCode != 0 || result.TimedOut) throw new IOException("external_command_failed");
            using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
            JsonElement items = document.RootElement.GetProperty("items");
            if (items.GetArrayLength() > 1) throw new InvalidDataException("worker_pod_replacement");
            if (items.GetArrayLength() == 1)
            {
                JsonElement pod = items[0];
                if (pod.TryGetProperty("status", out JsonElement status) && status.TryGetProperty("containerStatuses", out JsonElement containers))
                    foreach (JsonElement container in containers.EnumerateArray())
                        if (container.GetProperty("state").TryGetProperty("terminated", out JsonElement termination))
                        {
                            if (containers.GetArrayLength() != 1 || container.GetProperty("restartCount").GetInt32() != 0 ||
                                WorkerIdentity.Text(container, "name") != (mode == "loadgen" ? "loadgen" : "verifier"))
                                throw new InvalidDataException("worker_container_restart_or_shape");
                            int code = termination.GetProperty("exitCode").GetInt32();
                            if (termination.TryGetProperty("reason", out JsonElement reason) && reason.GetString() == "OOMKilled") code = 137;
                            if (termination.TryGetProperty("signal", out JsonElement signal) && signal.GetInt32() != 0) code = 128 + signal.GetInt32();
                            observation.ExitCode = code;
                            try
                            {
                                ProcessResult logs = await process.RunAsync(new("kubectl",
                                    ["logs", "-n", "parchwell", WorkerIdentity.Text(pod.GetProperty("metadata"), "name"), "--limit-bytes=16777217"],
                                    budgets?.Seconds("api_seconds") ?? TimeSpan.FromSeconds(60), Environment: RequireClusterEnvironment()), cancellationToken).ConfigureAwait(false);
                                observation.Logs = logs;
                                observation.LogFailure = null;
                                if (logs.ExitCode != 0 || logs.TimedOut) throw new IOException("external_command_failed");
                                return new(new(code, logs.StandardOutput, ""));
                            }
                            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
                            {
                                observation.LogFailure = exception is OperationCanceledException ? "logs_call_cancelled" : "logs_call_failed";
                                throw;
                            }
                        }
            }
            await Task.Delay(budgets?.Seconds("kubernetes_poll_seconds") ?? TimeSpan.FromSeconds(1), clock, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("worker_job_deadline");
    }

    private sealed class WorkerObservation(ProcessResult pod)
    {
        internal ProcessResult Pod { get; } = pod;
        internal int? ExitCode { get; set; }
        internal ProcessResult? Logs { get; set; }
        internal string? LogFailure { get; set; } = "logs_not_observed";
    }

    private async Task<DeploymentFiles> GenerateAsync(RepeatContext context, string mode, string producer, IReadOnlyList<string> brokers, CancellationToken token)
    {
        JsonObject input = JsonNode.Parse(currentRuntime.GetRawText())!.AsObject();
        input["run_id"] = context.RunId; input["phase"] = context.Phase; input["mode"] = mode; input["producer_pod_name"] = producer;
        input["expected_broker_names"] = JsonSerializer.SerializeToNode(brokers);
        return await generator.GenerateAsync(JsonSerializer.SerializeToElement(input), Path.Combine(context.OutputDirectory, mode + "-deployment"), token).ConfigureAwait(false);
    }

    private async Task ApplyChartAsync(RepeatContext context, DeploymentFiles deployment, string mode, CancellationToken token)
    {
        string rendered = await renderer.RenderAsync(deployment, chartDirectory, Path.Combine(context.OutputDirectory, mode + "-render"), token).ConfigureAwait(false);
        await KubectlAsync(["apply", "-f", rendered], token).ConfigureAwait(false);
    }
    private async Task PatchAsync(string name, object patch, CancellationToken token)
    { await KubectlAsync(["patch", "configmap", name, "-n", "parchwell", "--type=merge", "-p", JsonSerializer.Serialize(patch)], token).ConfigureAwait(false); }
    private async Task<byte[]> DownloadAsync(RepeatContext context, string container, string name, string filename, CancellationToken token)
    {
        Directory.CreateDirectory(context.OutputDirectory);
        string destination = Path.Combine(context.OutputDirectory, filename);
        await WorkerBlobDownloader.DownloadAsync(process, AccountName(), container, name, destination,
            budgets?.Number("producer_artifact_bytes") ?? 268435456, budgets?.Seconds("api_seconds") ?? TimeSpan.FromSeconds(60), token).ConfigureAwait(false);
        downloaded.Add((context.RunId, container, name), destination);
        return await File.ReadAllBytesAsync(destination, token).ConfigureAwait(false);
    }
    private string AccountName()
    {
        JsonElement blob = currentRuntime.GetProperty("ephemeral_outputs").GetProperty("blob");
        if (blob.TryGetProperty("value", out JsonElement wrapped)) blob = wrapped;
        return new Uri(WorkerIdentity.Text(blob, "account_url")).Host.Split('.')[0];
    }
    private async Task<ProcessResult> KubectlAsync(IReadOnlyList<string> args, CancellationToken token) => await CheckedAsync("kubectl", args, token).ConfigureAwait(false);
    private async Task<ProcessResult> AzAsync(IReadOnlyList<string> args, CancellationToken token) => await CheckedAsync("az", args, token).ConfigureAwait(false);
    private async Task<ProcessResult> CheckedAsync(string tool, IReadOnlyList<string> args, CancellationToken token)
    {
        ProcessResult result = await process.RunAsync(new(tool, args, budgets?.Seconds("api_seconds") ?? TimeSpan.FromSeconds(60), Environment: tool == "kubectl" ? RequireClusterEnvironment() : null), token).ConfigureAwait(false);
        if (result.ExitCode != 0 || result.TimedOut) throw new IOException("external_command_failed");
        return result;
    }
    private IReadOnlyDictionary<string, string> RequireClusterEnvironment() =>
        clusterEnvironment ?? throw new InvalidDataException("aks_context_not_bound");
    private static ProducerPod Pod(JsonElement value) => new(WorkerIdentity.Text(value.GetProperty("metadata"), "name"), WorkerIdentity.Text(value.GetProperty("metadata"), "uid"));
    private static string Selector(RepeatContext context) => "app.kubernetes.io/instance=" + context.RunId + ",parchwell.io/role=loadgen";
    private static List<JsonElement> ReadDocuments(string text)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { AllowMultipleValues = true });
        var result = new List<JsonElement>();
        while (reader.Read()) { using JsonDocument document = JsonDocument.ParseValue(ref reader); result.Add(document.RootElement.Clone()); }
        return result;
    }
}
