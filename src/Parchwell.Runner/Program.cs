using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Deployment;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Run.Rehearsal;
using Parchwell.Scenarios;

namespace Parchwell.Runner;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length < 1) throw new InvalidDataException("command_required");
            Dictionary<string, string> options = Parse(args.Skip(1).ToArray());
            string output = Required(options, "--output");
            Directory.CreateDirectory(output);
            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            if (args[0] == "rehearse")
            {
                // This branch constructs only finite in-process transports; it never reads runtime credentials.
                var rehearsalServices = new ServiceCollection();
                rehearsalServices.AddSingleton(TimeProvider.System);
                rehearsalServices.AddSingleton<RehearsalScenarioFactory>();
                rehearsalServices.AddSingleton<RehearsalRunner>();
                using ServiceProvider rehearsal = rehearsalServices.BuildServiceProvider();
                return await rehearsal.GetRequiredService<RehearsalRunner>().ExecuteAsync(Required(options, "--fixtures"), output, cancellation.Token).ConfigureAwait(false);
            }
            using JsonDocument runtimeDocument = await ReadDocumentAsync(Required(options, "--input"), cancellation.Token).ConfigureAwait(false);
            JsonElement runtime = runtimeDocument.RootElement;
            string commit = WorkerIdentity.Text(runtime, "source_commit");
            if (commit != WireFixtures.BuiltSourceCommit()) throw new InvalidDataException("approved_source_commit_mismatch");
            var services = new ServiceCollection();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IProcessRunner, BoundedProcessRunner>();
            string scenarioRoot = WorkerIdentity.Text(runtime, "scenario_root");
            using JsonDocument limits = await ReadDocumentAsync(WorkerIdentity.Text(runtime, "limits_file"), cancellation.Token).ConfigureAwait(false);
            ExecutionBudgets budgets = ExecutionBudgets.Read(limits.RootElement);
            services.AddSingleton(budgets);
            ScenarioLoadOptions parser = JsonSerializer.Deserialize<ScenarioLoadOptions>(limits.RootElement.GetProperty("scenario_parsing"))
                ?? throw new InvalidDataException("scenario_limits_required");
            services.AddSingleton<IScenarioSource>(new DeploymentScenarioSource(scenarioRoot));
            services.AddSingleton(Options.Create(parser));
            services.AddSingleton<ScenarioLoader>(); services.AddSingleton<HelmValuesGenerator>();
            services.AddSingleton<DeploymentGenerator>(); services.AddSingleton<ChartRenderer>();
            if (args[0] == "generate")
            {
                using ServiceProvider generation = services.BuildServiceProvider();
                await generation.GetRequiredService<DeploymentGenerator>().GenerateAsync(runtime, output, cancellation.Token).ConfigureAwait(false);
                return 0;
            }
            if (args[0] is not ("phase" or "gate" or "export" or "destroy" or "sweep")) throw new InvalidDataException("unknown_command");
            if (runtime.TryGetProperty("synthetic", out JsonElement synthetic) && synthetic.GetBoolean()) throw new InvalidDataException("synthetic_input_cannot_execute");
            if (args[0] == "phase") await DeploymentPreflight.ValidateAsync(runtime, cancellation.Token).ConfigureAwait(false);
            string phase = WorkerIdentity.Text(runtime, "phase");
            string env = WorkerIdentity.Text(runtime, "environment_run_id");
            if (env.Length > 29 || env.Length == 0 || !env.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-') || !char.IsAsciiLetterOrDigit(env[0]) || !char.IsAsciiLetterOrDigit(env[^1]))
                throw new InvalidDataException("environment_run_id_invalid");
            JsonElement runStorage = Output(runtime.GetProperty("persistent_outputs"), "run_storage");
            string accountUrl = WorkerIdentity.Text(runStorage, "account_url");
            await ValidateReadinessAsync(WorkerIdentity.Text(runtime, "readiness_file"), accountUrl, cancellation.Token).ConfigureAwait(false);
            services.AddSingleton<IArtifactStore>(provider => new AzureCliRunStore(provider.GetRequiredService<IProcessRunner>(), accountUrl, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "private-store")));
            services.AddSingleton<RunLedger>(); services.AddSingleton<EvidenceExporter>(); services.AddSingleton<ActualCostReconciler>();
            services.AddSingleton<ITerraformEnvironment>(provider => new TerraformEnvironment(provider.GetRequiredService<IProcessRunner>(),
                WorkerIdentity.Text(runtime, "ephemeral_root"), WorkerIdentity.Text(runtime, "backend_config"), WorkerIdentity.Text(runtime, "tfvars_file"), Path.Combine(output, "terraform"), provider.GetRequiredService<TimeProvider>()));
            services.AddSingleton(provider => (TerraformEnvironment)provider.GetRequiredService<ITerraformEnvironment>());
            services.AddSingleton(provider => new RecoveryEvidenceCollector(provider.GetRequiredService<IProcessRunner>(),
                provider.GetRequiredService<TerraformEnvironment>(), provider.GetRequiredService<TimeProvider>(), budgets,
                Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, env + "-private", "recovery-kubeconfig")));
            services.AddSingleton<IRunCluster>(provider => new KubernetesRunCluster(provider.GetRequiredService<IProcessRunner>(), provider.GetRequiredService<DeploymentGenerator>(),
                provider.GetRequiredService<ChartRenderer>(), runtime, WorkerIdentity.Text(runtime, "chart_directory"), provider.GetRequiredService<TimeProvider>(), budgets,
                privateKubeconfigPath: Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, env + "-private", "kubeconfig"),
                toolReceiptDirectory: Path.Combine(output, "tools", "kubelogin")));
            services.AddSingleton<RepeatExecutor>(); services.AddSingleton<GenerationGuard>(); services.AddSingleton<SweepExecutor>();
            services.AddSingleton<KqlCollector>();
            services.AddSingleton(provider => new PrivateEvidenceCollector(provider.GetRequiredService<KqlCollector>(),
                runtime.TryGetProperty("kql_parameters_file", out JsonElement parameters) ? parameters.GetString()! : Path.Combine(output, "missing-kql-parameters.private.json"),
                runtime.TryGetProperty("kql_source_file", out JsonElement query) ? query.GetString()! : Path.GetFullPath(Path.Combine(WorkerIdentity.Text(runtime, "ephemeral_root"), "..", "..", "docs", "operations", "kql", "a01-runtime.kql"))));
            services.AddSingleton(provider => new PhaseExecutor(provider.GetRequiredService<RunLedger>(), provider.GetRequiredService<ITerraformEnvironment>(),
                provider.GetRequiredService<RepeatExecutor>(), provider.GetRequiredService<EvidenceExporter>(), provider.GetRequiredService<GenerationGuard>(),
                provider.GetRequiredService<TimeProvider>(), budgets, provider.GetRequiredService<PrivateEvidenceCollector>().CollectAsync));
            services.AddSingleton<ProbeGateRunner>();
            services.AddSingleton<IRunGate>(provider => new LocalVerifierGate(provider.GetRequiredService<ProbeGateRunner>(), runtime));
            using ServiceProvider provider = services.BuildServiceProvider();
            var store = (AzureCliRunStore)provider.GetRequiredService<IArtifactStore>();
            await store.VerifyCliAsync(Path.Combine(output, "tools", "azure-cli"), cancellation.Token).ConfigureAwait(false);
            var ledger = provider.GetRequiredService<RunLedger>();
            var terraform = (TerraformEnvironment)provider.GetRequiredService<ITerraformEnvironment>();
            await terraform.InitializeAsync(cancellation.Token).ConfigureAwait(false);
            int exit;
            switch (args[0])
            {
                case "phase":
                    CostInput cost = PhaseReservation.Read(await File.ReadAllBytesAsync(WorkerIdentity.Text(runtime, "cost_file"), cancellation.Token).ConfigureAwait(false));
                    PhaseExecution executed = await provider.GetRequiredService<PhaseExecutor>().ExecuteAsync(new(phase, env, commit, output, cost,
                        runtime.GetProperty("initialize_ledger").GetBoolean()), cancellation.Token).ConfigureAwait(false);
                    await ReconcileAvailableCostAsync(runtime, phase, provider.GetRequiredService<ActualCostReconciler>(), cancellation.Token).ConfigureAwait(false);
                    await WriteCandidateAsync(runtime, output, executed.Result, ledger, cancellation.Token).ConfigureAwait(false);
                    exit = executed.Result is "completed" or "already_completed" ? 0 : 1;
                    break;
                case "gate":
                    LedgerDocument document = await ledger.ReadAsync(cancellation.Token).ConfigureAwait(false);
                    PhaseRecord probe = document.Phases["probe"];
                    if (probe.State != "destroyed" || probe.PrivateExport?.Complete != true || probe.DestroyReceipt?.Complete != true || probe.Repeats.Any(repeat => repeat.State != "completed"))
                        throw new InvalidDataException("gate_probe_not_completed");
                    if (probe.GateReceipt is not null)
                    {
                        byte[] retainedGate = await provider.GetRequiredService<EvidenceExporter>().ReadGateAsync(probe.EnvironmentRunId!, probe.GateReceipt.SourceSha256, cancellation.Token).ConfigureAwait(false);
                        await File.WriteAllBytesAsync(Path.Combine(output, "gate.json"), retainedGate, cancellation.Token).ConfigureAwait(false);
                        return 0;
                    }
                    var captured = new List<RepeatExecution>();
                    foreach (RepeatRecord repeat in probe.Repeats)
                    {
                        string name = repeat.AdmittedIdentity ?? throw new InvalidDataException("gate_admission_missing");
                        var identity = new WorkerIdentity("A01", "C0", repeat.RunId!, name, name, name);
                        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(output, repeat.RunId!, "result.json"), cancellation.Token).ConfigureAwait(false);
                        if (repeat.ResultHash != Convert.ToHexStringLower(SHA256.HashData(bytes))) throw new InvalidDataException("gate_result_hash");
                        captured.Add(new(identity, new(true, false, "diagnostic", null, "diagnostic"), new(new Dictionary<string, byte[]>()), bytes, []));
                    }
                    GateExecution gate = await provider.GetRequiredService<IRunGate>().ExecuteAsync(captured, commit, output, cancellation.Token).ConfigureAwait(false);
                    ProbeGateDecision decision = ProbeGateRunner.Inspect(gate, commit, captured.Select(item => item.Identity).ToArray());
                    await provider.GetRequiredService<EvidenceExporter>().UploadGateAsync(probe.EnvironmentRunId!, gate.Bytes, cancellation.Token).ConfigureAwait(false);
                    await ledger.RecordGateAsync(new(decision.Outcome, Convert.ToHexStringLower(SHA256.HashData(gate.Bytes)), commit), cancellation.Token).ConfigureAwait(false);
                    exit = 0;
                    break;
                case "export":
                    await ExportAsync(ledger, provider.GetRequiredService<EvidenceExporter>(), phase, env, output, cancellation.Token).ConfigureAwait(false);
                    await ReconcileAvailableCostAsync(runtime, phase, provider.GetRequiredService<ActualCostReconciler>(), cancellation.Token).ConfigureAwait(false);
                    exit = 0;
                    break;
                case "destroy":
                    await DestroyAsync(ledger, terraform, provider.GetRequiredService<GenerationGuard>(), provider.GetRequiredService<EvidenceExporter>(),
                        provider.GetRequiredService<TimeProvider>(), phase, env, output, cancellation.Token).ConfigureAwait(false);
                    await ReconcileAvailableCostAsync(runtime, phase, provider.GetRequiredService<ActualCostReconciler>(), cancellation.Token).ConfigureAwait(false);
                    exit = 0;
                    break;
                default:
                    await provider.GetRequiredService<SweepExecutor>().ExecuteAsync(output, cancellation.Token).ConfigureAwait(false);
                    exit = 0;
                    break;
            }
            return exit;
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            // Exception messages may contain resource identities, private file names or CLI output.
            Console.Error.WriteLine("runner_execution_failed");
            return 1;
        }
    }

    private static async Task ExportAsync(RunLedger ledger, EvidenceExporter exporter, string phase, string env, string output, CancellationToken token)
    {
        PhaseRecord record = (await ledger.ReadAsync(token).ConfigureAwait(false)).Phases[phase];
        if (record.EnvironmentRunId != env) throw new InvalidDataException("export_generation_mismatch");
        if (record.PrivateExport?.Complete == true) return;
        EvidenceFile[] files = Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).Where(path => !Path.GetRelativePath(output, path).StartsWith("private-store/", StringComparison.Ordinal))
            .Take(257).Select(path => new EvidenceFile(Path.GetRelativePath(output, path).Replace('\\', '/'), path)).ToArray();
        PrivateExportReceipt receipt = await exporter.ExportAsync(env, files, token).ConfigureAwait(false);
        await ledger.MarkExportedAsync(phase, env, receipt, token).ConfigureAwait(false);
    }

    private static async Task ReconcileAvailableCostAsync(JsonElement runtime, string phase, ActualCostReconciler reconciler, CancellationToken token)
    {
        if (runtime.TryGetProperty("actual_cost_file", out JsonElement file) && file.GetString() is string path && File.Exists(path))
            await reconciler.ReconcileAsync(phase, path, token).ConfigureAwait(false);
    }

    private static async Task DestroyAsync(RunLedger ledger, TerraformEnvironment environment, GenerationGuard guard,
        EvidenceExporter exporter, TimeProvider clock, string phase, string env, string output, CancellationToken token)
    {
        PhaseRecord record = (await ledger.ReadAsync(token).ConfigureAwait(false)).Phases[phase];
        if (record.EnvironmentRunId != env) throw new InvalidDataException("destroy_generation_mismatch");
        if (record.DestroyReceipt?.Complete == true)
        {
            await File.WriteAllBytesAsync(Path.Combine(output, "cleanup-receipt.json"), JsonSerializer.SerializeToUtf8Bytes(new { complete = true }), token).ConfigureAwait(false);
            return;
        }
        EnvironmentGeneration? generation = await environment.ReadGenerationAsync(token).ConfigureAwait(false);
        if (generation is not null && (generation.RunId != env || generation.ExpiresAt != record.ExpiresAt)) throw new InvalidDataException("destroy_generation_mismatch");
        bool complete = generation is null || await guard.DestroyAsync(generation, false, token).ConfigureAwait(false);
        var receipt = new DestroyReceipt(env, clock.GetUtcNow(), complete, complete ? "destroyed" : "destroy_incomplete");
        EvidenceObjectReceipt stored = await exporter.UploadCleanupReceiptAsync(env, receipt, token).ConfigureAwait(false);
        await ledger.MarkDestroyedAsync(phase, env, receipt with { PrivateReceiptPath = stored.Path, PrivateReceiptSha256 = stored.Sha256 }, token).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(output, "cleanup-receipt.json"), JsonSerializer.SerializeToUtf8Bytes(new { complete }), token).ConfigureAwait(false);
    }

    private static async Task WriteCandidateAsync(JsonElement runtime, string output, string status, RunLedger ledger, CancellationToken token)
    {
        LedgerDocument document = await ledger.ReadAsync(token).ConfigureAwait(false);
        PhaseRecord phase = document.Phases[WorkerIdentity.Text(runtime, "phase")];
        byte[] candidate = PublicCandidate.Create(JsonSerializer.SerializeToUtf8Bytes(new {
            scenario_id = "A01", configuration_id = WorkerIdentity.Text(runtime, "phase") == "probe" ? "C0" : WorkerIdentity.Text(runtime, "phase"),
            run_id = phase.EnvironmentRunId, source_commit = document.SourceCommit, status,
            costs = new { estimate_gbp = phase.ReservedTotalGbp, actual_gbp = phase.ActualCostGbp, actual_available = phase.ActualCostGbp is not null },
            reasons = new[] { phase.ActualCostGbp is null ? "actual_cost_unavailable" : "actual_cost_received" }
        }), WorkerIdentity.Text(runtime, "subscription_guid"));
        await File.WriteAllBytesAsync(Path.Combine(output, "candidate.json"), candidate, token).ConfigureAwait(false);
    }

    private static async Task ValidateReadinessAsync(string path, string account, CancellationToken token)
    {
        using JsonDocument document = await ReadDocumentAsync(path, token).ConfigureAwait(false);
        JsonElement root = document.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1 || WorkerIdentity.Text(root, "account_url") != account) throw new InvalidDataException("readiness_identity");
        foreach (string key in new[] { "shared_keys_disabled", "public_access_disabled", "private_endpoint_ready", "private_dns_ready", "ledger_read_create_conditional_update", "evidence_create_read" })
            if (!root.GetProperty(key).GetBoolean()) throw new InvalidDataException("private_storage_not_ready");
        string?[] environments = root.GetProperty("environments").EnumerateArray().Select(item => item.GetString()).ToArray();
        if (!environments.Contains("parchwell-scenarios", StringComparer.Ordinal) || !environments.Contains("parchwell-sweeper", StringComparer.Ordinal)) throw new InvalidDataException("storage_environments_not_ready");
    }
    private static async Task<JsonDocument> ReadDocumentAsync(string path, CancellationToken token)
    {
        if (new FileInfo(path).Length > 1048576) throw new InvalidDataException("runtime_document_limit");
        return JsonDocument.Parse(await File.ReadAllBytesAsync(path, token).ConfigureAwait(false), new JsonDocumentOptions { MaxDepth = 32 });
    }
    private static JsonElement Output(JsonElement root, string name) { JsonElement value = root.GetProperty(name); return value.TryGetProperty("value", out JsonElement nested) ? nested : value; }
    private static Dictionary<string, string> Parse(string[] args)
    {
        if (args.Length % 2 != 0) throw new InvalidDataException("command_option_pairs_required");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (args[i] is not ("--input" or "--output" or "--fixtures") || !result.TryAdd(args[i], args[i + 1])) throw new InvalidDataException("unknown_or_duplicate_option");
        }
        return result;
    }
    private static string Required(Dictionary<string, string> options, string name) => options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidDataException("missing_option");
}
