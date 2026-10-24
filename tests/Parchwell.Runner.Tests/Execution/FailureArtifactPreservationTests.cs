using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Deployment;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Run.Rehearsal;
using Parchwell.Runner.Tests.Cost;
using Parchwell.Runner.Tests.Deployment;
using Parchwell.Verifier.StartLine;
using Xunit;
using VerifierIdentity = Parchwell.Verifier.Common.Contracts.RunIdentity;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Repository-required Given_When_Then names.

[Collection("Failure diagnostics environment")]
public sealed class FailureArtifactPreservationTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 137)]
    public async Task Given_VerifierTerminatesWithoutResult_When_DownloadFails_Then_DebugRetainsWorkerExitAndLogsAlongsideBlobError(bool debug, int exitCode)
    {
        string? previous = Environment.GetEnvironmentVariable("PARCHWELL_DEBUG_FAILURES");
        Environment.SetEnvironmentVariable("PARCHWELL_DEBUG_FAILURES", debug ? "true" : null);
        try
        {
            using var deployment = new DeploymentFixture();
            var context = new RepeatContext("probe", 1, "failure-phase-r1", new string('a', 40), deployment.Directory);
            var process = new ArtifactProcess("verify_missing_blob", []) { WorkerExitCode = exitCode };
            process.Initialize(context);
            using var cluster = new KubernetesRunCluster(process, deployment.Generator(), new ChartRenderer(process),
                JsonSerializer.SerializeToElement(deployment.Input("C0")), "unused-chart", TimeProvider.System,
                privateKubeconfigPath: Path.Combine(deployment.Directory, "credentials", "config"));
            JsonObject outputs = JsonSerializer.SerializeToNode(deployment.Input("C0"))!["ephemeral_outputs"]!.AsObject();
            outputs["aks"] = JsonSerializer.SerializeToNode(new
            {
                name = "synthetic", id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/synthetic/providers/Microsoft.ContainerService/managedClusters/synthetic"
            });
            await cluster.BindClusterAsync(JsonSerializer.SerializeToElement(outputs), TestContext.Current.CancellationToken);
            var identity = new WorkerIdentity("A01", "C0", context.RunId, context.RunId + "-pod", context.RunId + "-pod", context.RunId + "-pod");

            IOException exception = await Assert.ThrowsAsync<IOException>(() => cluster.VerifyAsync(context, identity,
                ["broker.example.invalid"], TestContext.Current.CancellationToken));

            Assert.Equal("worker_blob_read_failed", exception.Message);
            using var error = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            FailureDiagnostics.Write(exception, error, "repeat");
            string output = error.ToString();
            if (debug)
            {
                Assert.Contains($"runner_debug_worker verify exit_code={exitCode} timed_out=False", output, StringComparison.Ordinal);
                Assert.Contains("runner_debug_worker verify stdout", output, StringComparison.Ordinal);
                Assert.Contains($"runner_debug_worker verify exit_code={exitCode} timed_out=False scenario_id=A01 run_id={context.RunId}", output, StringComparison.Ordinal);
                Assert.Contains($"runner_debug_worker verify stdout scenario_id=A01 run_id={context.RunId}", output, StringComparison.Ordinal);
                Assert.Contains(process.WorkerStdout!, output, StringComparison.Ordinal);
                Assert.Contains("runner_debug_command repeat az stderr", output, StringComparison.Ordinal);
                Assert.Contains("BlobNotFound", output, StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal("runner_execution_failed repeat IOException worker_blob_read_failed" + Environment.NewLine, output);
                Assert.Empty(exception.Data);
            }
            Assert.Single(process.Requests, request => request.FileName == "kubectl" && request.Arguments[0] == "logs");
            Assert.False(File.Exists(Path.Combine(context.OutputDirectory, "result.json")));
        }
        finally { Environment.SetEnvironmentVariable("PARCHWELL_DEBUG_FAILURES", previous); }
    }

    [Theory]
    [InlineData("loadgen_exit")]
    [InlineData("loadgen_receipt")]
    [InlineData("partial_publication")]
    [InlineData("start_line_exit")]
    [InlineData("start_line_receipt")]
    [InlineData("pre_admission")]
    [InlineData("producer_wait")]
    [InlineData("release_uncertain")]
    [InlineData("cancelled_repeat")]
    [InlineData("verify_exit")]
    [InlineData("verify_transport")]
    [InlineData("oversize_artifact")]
    [InlineData("partial_transfer")]
    [InlineData("collection_cancelled")]
    [InlineData("start_line_missing_blob")]
    [InlineData("start_line_download_cancelled")]
    [InlineData("start_line_logs_failed")]
    [InlineData("start_line_logs_timed_out")]
    [InlineData("loadgen_logs_cancelled")]
    [InlineData("verify_missing_blob")]
    [InlineData("verify_download_cancelled")]
    [InlineData("verify_logs_threw")]
    [InlineData("verify_observation_write_failed")]
    [InlineData("start_line_oom")]
    [InlineData("verify_signal")]
    [InlineData("start_line_malformed_pods")]
    [InlineData("start_line_duplicate_pods")]
    [InlineData("start_line_pods_failed")]
    public async Task Given_PublishedOriginalsAndFailedRepeat_When_PhaseCleansUp_Then_AvailableBytesArePrivatelyVerifiedBeforeDestroyWithoutCountsOrReplacement(string failure)
    {
        using var deployment = new DeploymentFixture();
        string output = Path.Combine(deployment.Directory, "phase");
        var trace = new List<string>();
        var clock = new CaptureClock();
        var process = new ArtifactProcess(failure, trace) { CancelCapture = clock.CancelCapture };
        using var production = new KubernetesRunCluster(process, deployment.Generator(), new ChartRenderer(process),
            JsonSerializer.SerializeToElement(deployment.Input("C0")), "unused-chart", TimeProvider.System,
            privateKubeconfigPath: Path.Combine(deployment.Directory, "credentials", "config"));
        JsonObject outputs = JsonSerializer.SerializeToNode(deployment.Input("C0"))!["ephemeral_outputs"]!.AsObject();
        outputs["aks"] = JsonSerializer.SerializeToNode(new
        {
            name = "synthetic", id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/synthetic/providers/Microsoft.ContainerService/managedClusters/synthetic"
        });
        await production.BindClusterAsync(JsonSerializer.SerializeToElement(outputs), TestContext.Current.CancellationToken);
        using var repeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        process.CancelRepeat = repeatCancellation.Cancel;
        var cluster = new FailingCluster(production, process, failure, repeatCancellation);
        var store = new RecordingStore(trace);
        var ledger = new RunLedger(store, TimeProvider.System);
        var environment = new RehearsalEnvironment(trace: trace);
        var executor = new PhaseExecutor(ledger, environment, new RepeatExecutor(cluster, TimeProvider.System),
            new EvidenceExporter(store, TimeProvider.System), new GenerationGuard(environment, TimeProvider.System), clock,
            collectCorroboration: (directory, token) =>
            {
                if (failure == "collection_cancelled")
                {
                    trace.Add("corroboration_started");
                    clock.CancelCapture();
                    token.ThrowIfCancellationRequested();
                }
                return Task.CompletedTask;
            });

        PhaseExecution result = await executor.ExecuteAsync(new("probe", "failure-phase", new string('a', 40), output,
            PhaseReservationTests.Input(), true), repeatCancellation.Token);

        Assert.Equal("failed", result.Result);
        Assert.Empty(result.Repeats);
        Assert.True(result.Destroyed);
        Assert.True(result.Exported);
        Assert.Equal(1, cluster.Created);
        Assert.Equal(failure.StartsWith("verify_", StringComparison.Ordinal) ? 1 : 0, cluster.Verifications);
        Assert.Equal(1, environment.Applies);
        Assert.Equal(1, environment.Destroys);
        if (failure is "start_line_missing_blob" or "start_line_download_cancelled" or "start_line_logs_failed" or "start_line_logs_timed_out" or "loadgen_logs_cancelled" or
            "verify_missing_blob" or "verify_download_cancelled" or "verify_logs_threw" or "verify_observation_write_failed" or "start_line_oom" or "verify_signal")
            Assert.NotNull(process.ObservedMode);
        if (failure is "start_line_malformed_pods" or "start_line_duplicate_pods" or "start_line_pods_failed") Assert.NotNull(process.ObservedMode);
        PhaseRecord phase = (await ledger.ReadAsync(TestContext.Current.CancellationToken)).Phases["probe"];
        Assert.Equal(failure is "cancelled_repeat" or "start_line_download_cancelled" or "verify_download_cancelled" or "loadgen_logs_cancelled" ? "started" : "interrupted", phase.Repeats[0].State);
        Assert.All(phase.Repeats.Skip(1), repeat => Assert.Equal("not_started", repeat.State));
        Assert.True(trace.IndexOf("artifact_read") < trace.IndexOf("private_readback"));
        Assert.True(trace.IndexOf("private_readback") < trace.IndexOf("destroy_plan"));
        string prefix = "A01/failure-phase/failure-phase-r1/";
        var exported = store.Inner.Objects.Where(item => item.Key.Container == "evidence").ToDictionary(item => item.Key.Path, item => item.Value.Bytes, StringComparer.Ordinal);
        Assert.Contains(prefix + "pod-history.json", exported.Keys);
        string receiptPath = prefix + "failure-artifacts/collection-receipt.json";
        using JsonDocument receipt = JsonDocument.Parse(exported[failure == "collection_cancelled" ? prefix + "failure-artifacts-unavailable.json" : receiptPath]);
        Assert.False(receipt.RootElement.GetProperty("accepted_counts").GetBoolean());
        if (failure == "collection_cancelled")
            Assert.Equal("failure_artifact_collection_incomplete", receipt.RootElement.GetProperty("reason").GetString());
        else
        {
            Assert.False(receipt.RootElement.GetProperty("execution_completed").GetBoolean());
            Assert.Equal(failure != "pre_admission", receipt.RootElement.GetProperty("producer_identity_available").GetBoolean());
        }
        if (process.ObservedMode is { } observedMode)
        {
            if (failure != "verify_observation_write_failed")
                Assert.Equal(process.PodObservation, Encoding.UTF8.GetString(exported[prefix + observedMode + "-pod.json"]));
            Assert.Equal("original pod API warning\n", Encoding.UTF8.GetString(exported[prefix + observedMode + "-pod-stderr.log"]));
            bool terminationAvailable = failure is not ("start_line_malformed_pods" or "start_line_duplicate_pods" or "start_line_pods_failed");
            if (terminationAvailable)
            {
                using JsonDocument termination = JsonDocument.Parse(exported[prefix + observedMode + "-termination.json"]);
                Assert.Equal(failure == "start_line_oom" ? 137 : failure == "verify_signal" ? 143 : 0, termination.RootElement.GetProperty("exit_code").GetInt32());
                Assert.False(termination.RootElement.GetProperty("timed_out").GetBoolean());
            }
            else Assert.DoesNotContain(prefix + observedMode + "-termination.json", exported.Keys);
            using JsonDocument logCommand = JsonDocument.Parse(exported[prefix + observedMode + "-logs-command.json"]);
            Assert.Equal(failure == "start_line_pods_failed" ? 1 : 0, logCommand.RootElement.GetProperty("pod_command").GetProperty("exit_code").GetInt32());
            Assert.False(logCommand.RootElement.GetProperty("pod_command").GetProperty("timed_out").GetBoolean());
            bool logsAvailable = terminationAvailable && failure is not ("verify_logs_threw" or "loadgen_logs_cancelled");
            Assert.Equal(logsAvailable, logCommand.RootElement.GetProperty("available").GetBoolean());
            if (logsAvailable)
            {
                Assert.Equal(process.WorkerStdout, Encoding.UTF8.GetString(exported[prefix + observedMode + "-stdout.log"]));
                Assert.Equal("original log API warning\n", Encoding.UTF8.GetString(exported[prefix + observedMode + "-logs-stderr.log"]));
                Assert.Equal(failure == "start_line_logs_failed" ? 1 : 0, logCommand.RootElement.GetProperty("exit_code").GetInt32());
                Assert.Equal(failure == "start_line_logs_timed_out", logCommand.RootElement.GetProperty("timed_out").GetBoolean());
            }
            else
            {
                Assert.DoesNotContain(prefix + observedMode + "-stdout.log", exported.Keys);
                Assert.DoesNotContain(prefix + observedMode + "-logs-stderr.log", exported.Keys);
                Assert.Equal(!terminationAvailable ? "logs_not_observed" : failure == "loadgen_logs_cancelled" ? "logs_call_cancelled" : "logs_call_failed", logCommand.RootElement.GetProperty("reason").GetString());
            }
            if (failure is "start_line_missing_blob" or "start_line_download_cancelled" or "verify_missing_blob" or "verify_download_cancelled")
                Assert.Contains(receipt.RootElement.GetProperty("artifacts").EnumerateArray(), item => item.GetProperty("status").GetString() == "unavailable");
            if (terminationAvailable)
                Assert.Single(exported.Keys, path => path.EndsWith("/" + observedMode + "-termination.json", StringComparison.Ordinal));
            if (logsAvailable)
                Assert.Single(exported.Keys, path => path.EndsWith("/" + observedMode + "-stdout.log", StringComparison.Ordinal));
        }
        if (failure == "verify_observation_write_failed")
        {
            Assert.Equal(process.WorkerStdout, Encoding.UTF8.GetString(exported[prefix + "verify-stdout.log"]));
            Assert.Contains(prefix + "verify-termination.json", exported.Keys);
            Assert.Contains(prefix + "verify-logs-command.json", exported.Keys);
            Assert.DoesNotContain(prefix + "verify-pod.json", exported.Keys);
            Assert.DoesNotContain(prefix + "failure-artifacts-unavailable.json", exported.Keys);
        }
        foreach (var blob in process.Blobs)
        {
            if (failure == "pre_admission" && blob.Key.Container == "ledger") continue;
            if (failure == "collection_cancelled" && blob.Key.Name.EndsWith("/result.json", StringComparison.Ordinal)) continue;
            if (failure == "oversize_artifact" && blob.Key.Name.EndsWith("/ledger.jsonl", StringComparison.Ordinal)) continue;
            byte[] expected = failure == "partial_transfer" && blob.Key.Name.EndsWith("/ledger.jsonl", StringComparison.Ordinal) ? blob.Value[..2] : blob.Value;
            string filename = blob.Key.Name.Split('/')[^1];
            Assert.Contains(exported, item => item.Key.StartsWith(prefix, StringComparison.Ordinal) &&
                item.Key.EndsWith("/" + filename, StringComparison.Ordinal) && item.Value.AsSpan().SequenceEqual(expected));
        }
        if (failure.StartsWith("verify_", StringComparison.Ordinal))
            foreach (string name in new[] { "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "index.json" })
            {
                Assert.Contains(prefix + name, exported.Keys);
                Assert.DoesNotContain(prefix + "downloads/" + name, exported.Keys);
                Assert.DoesNotContain(prefix + "failure-artifacts/ledger/" + name, exported.Keys);
            }
        if (failure == "collection_cancelled")
        {
            Assert.Contains("A01/failure-phase/kql-unavailable.json", exported.Keys);
            Assert.True(trace.IndexOf("corroboration_started") < trace.IndexOf("private_readback"));
        }
        if (failure is "partial_publication" or "oversize_artifact" or "partial_transfer")
            Assert.Contains(receipt.RootElement.GetProperty("artifacts").EnumerateArray(), item => item.GetProperty("status").GetString() == "unavailable");
        Assert.DoesNotContain(process.Requests, request => request.FileName == "az" && request.Arguments[2] is "upload" or "delete");
        Assert.All(process.Requests.Where(request => request.FileName == "az" && request.Arguments[0] == "storage"),
            request => Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout));
        Assert.All(process.Requests.Where(request => request.FileName == "kubectl" && request.Arguments[0] == "logs"), request =>
        {
            Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout);
            Assert.Equal(16777216, request.MaxOutputBytes);
        });
    }

    private sealed class FailingCluster(KubernetesRunCluster production, ArtifactProcess process, string failure,
        CancellationTokenSource repeatCancellation) : IRunCluster
    {
        internal int Created { get; private set; }
        internal int Verifications { get; private set; }
        public Task PreflightAsync(JsonElement outputs, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<PodAdmission> EstablishProducerWatchAsync(RepeatContext context, CancellationToken cancellationToken) => production.EstablishProducerWatchAsync(context, cancellationToken);
        public Task CreateProducerAsync(RepeatContext context, CancellationToken cancellationToken) { Created++; return Task.CompletedTask; }
        public Task<ProducerPod> ObserveProducerAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken) =>
            failure == "pre_admission" ? throw new IOException("synthetic_admission_failed") : production.ObserveProducerAsync(context, admission, cancellationToken);
        public Task<string> GenerateComparisonAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken) => production.GenerateComparisonAsync(context, identity, cancellationToken);
        public async Task<StartLineExecution> StartLineAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken)
        {
            process.Initialize(context);
            VerifierIdentity worker = Identity(context);
            byte[] inventory = BrokerInventoryCodec.Write(worker, [new BrokerMetadata(1, "broker.example.invalid", 9093)], context.SourceCommit, 16777216);
            process.Blobs[("verifier", context.Phase + "/" + context.RunId + "/broker-inventory.json")] = inventory;
            if (failure is "start_line_missing_blob" or "start_line_download_cancelled" or "start_line_logs_failed" or "start_line_logs_timed_out" or "start_line_oom" or
                "start_line_malformed_pods" or "start_line_duplicate_pods" or "start_line_pods_failed")
                return await production.StartLineAsync(context, identity, cancellationToken);
            string receipt = Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("start-line", worker, context.SourceCommit));
            return new(new(new(failure == "start_line_exit" ? 1 : 0, failure == "start_line_receipt" ? "original stdout without receipt" : receipt, "")), inventory);
        }
        public Task ReleaseAsync(RepeatContext context, byte[] permit, CancellationToken cancellationToken) => production.ReleaseAsync(context, permit, cancellationToken);
        public async Task<WorkerExecution> WaitProducerAsync(RepeatContext context, CancellationToken cancellationToken)
        {
            if (failure == "producer_wait") throw new IOException("synthetic_worker_read_failed");
            if (failure == "loadgen_logs_cancelled") return await production.WaitProducerAsync(context, cancellationToken);
            if (failure == "cancelled_repeat") { repeatCancellation.Cancel(); cancellationToken.ThrowIfCancellationRequested(); }
            string receipt = Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("loadgen", Identity(context), context.SourceCommit));
            return new(new(failure == "loadgen_exit" || failure is "oversize_artifact" or "partial_transfer" or "collection_cancelled" ? 1 : 0,
                failure == "loadgen_receipt" ? "original stdout without receipt" : receipt, ""));
        }
        public async Task<ProducerPublication> DownloadPublicationAsync(RepeatContext context, CancellationToken cancellationToken)
        {
            if (failure.StartsWith("verify_", StringComparison.Ordinal))
            {
                var identity = Identity(context);
                var golden = await WorkerPublicationFixture.CreateAsync(Path.Combine(context.OutputDirectory, "fixture-producer"),
                    new(identity.ScenarioId, identity.ConfigurationId, identity.RunId, identity.InstanceId, identity.ExpectedInstanceId, identity.ClientId), cancellationToken);
                foreach (var item in golden.Payloads) process.Blobs[("ledger", "A01/C0/" + context.RunId + "/" + identity.InstanceId + "/" + item.Key)] = item.Value;
            }
            return await production.DownloadPublicationAsync(context, cancellationToken);
        }
        public Task<WorkerExecution> VerifyAsync(RepeatContext context, WorkerIdentity identity, IReadOnlyList<string> brokers, CancellationToken cancellationToken)
        {
            Verifications++;
            if (failure == "verify_transport") throw new IOException("synthetic_verifier_transport_failed");
            if (failure is "verify_missing_blob" or "verify_download_cancelled" or "verify_logs_threw" or "verify_observation_write_failed" or "verify_signal")
                return production.VerifyAsync(context, identity, brokers, cancellationToken);
            return Task.FromResult(new WorkerExecution(new(1, "original failed verifier stdout", ""), process.Blobs[("verifier", "probe/" + context.RunId + "/result.json")]));
        }
        public Task PreserveObservedWorkersAsync(RepeatContext context, CancellationToken cancellationToken) => production.PreserveObservedWorkersAsync(context, cancellationToken);
        public Task PreserveAvailableArtifactsAsync(RepeatContext context, CancellationToken cancellationToken)
        {
            Assert.False(cancellationToken.IsCancellationRequested);
            process.Initialize(context);
            return production.PreserveAvailableArtifactsAsync(context, cancellationToken);
        }
        public Task CompleteProducerWatchAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken) => production.CompleteProducerWatchAsync(context, admission, cancellationToken);
        private static VerifierIdentity Identity(RepeatContext context) => new("A01", "C0", context.RunId, context.RunId + "-pod", context.RunId + "-pod", context.RunId + "-pod");
    }

    private sealed class CaptureClock : TimeProvider
    {
        private TimerCallback? callback;
        private object? callbackState;
        internal void CancelCapture() => callback!(callbackState);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime != TimeSpan.FromSeconds(60)) return System.CreateTimer(callback, state, dueTime, period);
            this.callback = callback;
            callbackState = state;
            return new CaptureTimer();
        }
        private sealed class CaptureTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingStore(List<string> trace) : IArtifactStore
    {
        internal PersistentTestStore Inner { get; } = new();
        public async Task<StoredArtifact?> ReadAsync(string container, string path, int maxBytes, CancellationToken cancellationToken)
        {
            StoredArtifact? result = await Inner.ReadAsync(container, path, maxBytes, cancellationToken);
            if (container == "evidence") trace.Add("private_readback");
            return result;
        }
        public Task<ArtifactWriteOutcome> WriteAsync(string container, string path, ReadOnlyMemory<byte> bytes, string? expectedETag, bool createOnly, CancellationToken cancellationToken) =>
            Inner.WriteAsync(container, path, bytes, expectedETag, createOnly, cancellationToken);
    }

    private sealed class ArtifactProcess(string failure, List<string> trace) : IProcessRunner
    {
        internal Dictionary<(string Container, string Name), byte[]> Blobs { get; } = [];
        internal List<ProcessRequest> Requests { get; } = [];
        internal Action? CancelCapture { get; init; }
        internal Action? CancelRepeat { get; set; }
        internal int? WorkerExitCode { get; init; }
        internal string? ObservedMode { get; private set; }
        internal string? PodObservation { get; private set; }
        internal string? WorkerStdout { get; private set; }
        private RepeatContext? context;
        private int storageReads;
        private bool initialized;
        private int watches;
        internal void Initialize(RepeatContext context)
        {
            if (initialized) return;
            initialized = true;
            this.context = context;
            foreach (string name in new[] { "start-line.json", "broker-inventory.json", "result.json" })
                Blobs[("verifier", context.Phase + "/" + context.RunId + "/" + name)] = Encoding.UTF8.GetBytes("synthetic original " + name + "\n");
            if (failure is "start_line_missing_blob" or "start_line_download_cancelled")
                Blobs.Remove(("verifier", context.Phase + "/" + context.RunId + "/start-line.json"));
            if (failure is "verify_missing_blob" or "verify_download_cancelled" or "verify_observation_write_failed")
                Blobs.Remove(("verifier", context.Phase + "/" + context.RunId + "/result.json"));
            foreach (string name in new[] { "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "index.json" })
                if (failure != "partial_publication" || name is "ledger.jsonl" or "manifest.json")
                    Blobs[("ledger", "A01/C0/" + context.RunId + "/" + context.RunId + "-pod/" + name)] = Encoding.UTF8.GetBytes("synthetic original " + name + "\n");
        }
        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            var args = request.Arguments;
            if (request.FileName == "az" && args[0] == "aks")
            { await File.WriteAllTextAsync(Value(args, "--file"), "synthetic kubeconfig", cancellationToken); return new(0, "", ""); }
            if (request.FileName == "kubelogin") return new(0, args[0] == "--version" ? "synthetic version" : "--login azurecli", "");
            if (request.FileName == "helm") return new(0, args[0] == "version" ? "v4.3.1+synthetic" : "synthetic rendered output", "");
            if (request.FileName == "kubectl")
            {
                if (failure == "release_uncertain" && args[0] == "patch") return new(1, "", "synthetic uncertain response after permit delivery");
                if (args[1] == "--raw" && args[2].StartsWith("/api/v1/namespaces/parchwell/pods?labelSelector=", StringComparison.Ordinal))
                    return new(0, "{\"apiVersion\":\"v1\",\"kind\":\"PodList\",\"metadata\":{\"resourceVersion\":\"1\"},\"items\":[]}", "");
                if (args[1] == "--raw" && args[2].StartsWith("/api/v1/namespaces/parchwell/pods?watch=true&", StringComparison.Ordinal)) return new(0, ++watches == 1
                    ? "{\"type\":\"ADDED\",\"object\":{\"metadata\":{\"resourceVersion\":\"2\",\"name\":\"failure-phase-r1-pod\",\"uid\":\"original-uid\"}}}"
                    : "{\"type\":\"BOOKMARK\",\"object\":{\"metadata\":{\"resourceVersion\":\"3\"}}}", "");
                if (args[0] == "get" && args.Contains("-l", StringComparer.Ordinal) && args.Single(value => value.StartsWith("job-name=", StringComparison.Ordinal) || value.StartsWith("app.kubernetes.io/instance=", StringComparison.Ordinal)).StartsWith("job-name=", StringComparison.Ordinal))
                {
                    string job = args.Single(value => value.StartsWith("job-name=", StringComparison.Ordinal));
                    ObservedMode = job.EndsWith("-start-line", StringComparison.Ordinal) ? "start-line" : job.EndsWith("-loadgen", StringComparison.Ordinal) ? "loadgen" : "verify";
                    PodObservation = JsonSerializer.Serialize(new { items = new[] { new
                    {
                        metadata = new { name = context!.RunId + "-" + ObservedMode + "-pod", uid = "original-worker-uid" },
                        status = new { containerStatuses = new[] { new { name = ObservedMode == "loadgen" ? "loadgen" : "verifier", restartCount = 0,
                            state = new { terminated = new { exitCode = WorkerExitCode ?? (failure == "start_line_oom" ? 137 : failure == "verify_signal" ? 1 : 0),
                                reason = failure == "start_line_oom" ? "OOMKilled" : "Completed", signal = failure == "verify_signal" ? 15 : 0 } } } } }
                    } } });
                    if (failure == "start_line_malformed_pods") PodObservation = "original malformed pod API response {\n";
                    if (failure == "start_line_duplicate_pods")
                    {
                        JsonObject snapshot = JsonNode.Parse(PodObservation)!.AsObject();
                        snapshot["items"]!.AsArray().Add(snapshot["items"]![0]!.DeepClone());
                        PodObservation = snapshot.ToJsonString();
                    }
                    return new(failure == "start_line_pods_failed" ? 1 : 0, PodObservation, "original pod API warning\n");
                }
                if (args[0] == "logs")
                {
                    if (failure == "loadgen_logs_cancelled") { CancelRepeat!(); cancellationToken.ThrowIfCancellationRequested(); }
                    if (failure == "verify_logs_threw") throw new IOException("synthetic log retrieval failed");
                    WorkerStdout = "original worker stdout before unavailable dependent bytes\n";
                    if (failure == "verify_observation_write_failed")
                    {
                        Directory.CreateDirectory(Path.Combine(context!.OutputDirectory, "verify-pod.json"));
                        Directory.CreateDirectory(Path.Combine(context.OutputDirectory, "failure-artifacts-unavailable.json"));
                    }
                    return new(failure == "start_line_logs_failed" ? 1 : 0, WorkerStdout, "original log API warning\n", failure == "start_line_logs_timed_out");
                }
                return new(0, "{\"metadata\":{\"resourceVersion\":\"1\"},\"items\":[]}", "");
            }
            Assert.Equal("az", request.FileName);
            Assert.Equal("storage", args[0]);
            trace.Add("artifact_read");
            if (failure == "collection_cancelled" && ++storageReads == 17) CancelCapture!();
            if (args[2] == "list")
                return new(0, JsonSerializer.Serialize(Blobs.Keys.Where(key => key.Container == "ledger").Select(key => new { name = key.Name })), "");
            string name = Value(args, "--name");
            if (failure is "start_line_download_cancelled" or "verify_download_cancelled" &&
                name.EndsWith(failure == "start_line_download_cancelled" ? "/start-line.json" : "/result.json", StringComparison.Ordinal) &&
                !Value(args, "--container-name").Equals("ledger", StringComparison.Ordinal) && !cancellationToken.IsCancellationRequested)
            {
                CancelRepeat!();
                // Only the work token is cancelled; independent failure collection remains usable.
                if (Requests.Count(request => request.FileName == "az" && request.Arguments.Contains(name, StringComparer.Ordinal)) == 1)
                    cancellationToken.ThrowIfCancellationRequested();
            }
            if (!Blobs.TryGetValue((Value(args, "--container-name"), name), out byte[]? bytes)) return new(1, "", "BlobNotFound");
            if (args[2] == "show") return new(0, JsonSerializer.Serialize(new
            { etag = "\"original-etag\"", contentLength = failure == "oversize_artifact" && name.EndsWith("/ledger.jsonl", StringComparison.Ordinal) ? 268435457L : bytes.Length }), "");
            Assert.Equal("download", args[2]);
            Assert.Equal("\"original-etag\"", Value(args, "--if-match"));
            Assert.Equal("false", Value(args, "--overwrite"));
            bool partial = failure == "partial_transfer" && name.EndsWith("/ledger.jsonl", StringComparison.Ordinal);
            await File.WriteAllBytesAsync(Value(args, "--file"), partial ? bytes[..2] : bytes, cancellationToken);
            return new(partial ? 1 : 0, "", partial ? "original transfer interrupted" : "");
        }
        private static string Value(IReadOnlyList<string> args, string name) => args[args.ToList().IndexOf(name) + 1];
    }
}
#pragma warning restore CA1707
