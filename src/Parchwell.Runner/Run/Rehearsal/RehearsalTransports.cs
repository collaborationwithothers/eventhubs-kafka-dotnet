using System.Text;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;

namespace Parchwell.Runner.Run.Rehearsal;

public sealed class RehearsalArtifactStore(string? failure = null, List<string>? trace = null) : IArtifactStore
{
    private readonly Dictionary<string, StoredArtifact> objects = new(StringComparer.Ordinal);
    private long revision;
    public Task<StoredArtifact?> ReadAsync(string container, string path, int maxBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        objects.TryGetValue(container + "/" + path, out StoredArtifact? value);
        if (value is not null && value.Bytes.Length > maxBytes) throw new InvalidDataException("store_limit");
        return Task.FromResult(value is null ? null : new StoredArtifact(value.Bytes.ToArray(), value.ETag));
    }
    public Task<ArtifactWriteOutcome> WriteAsync(string container, string path, ReadOnlyMemory<byte> bytes, string? expectedETag,
        bool createOnly, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (container == "evidence" && trace is not null)
        {
            if (!trace.Contains("export_started", StringComparer.Ordinal)) trace.Add("export_started");
            if (path.EndsWith("/manifest.json", StringComparison.Ordinal)) trace.Add("private_manifest");
            if (path.Contains("/cleanup/", StringComparison.Ordinal)) trace.Add("cleanup_receipt");
        }
        if (container == "evidence" && (failure == "export" ||
            failure == "phase_export" && path == "A01/rehearsal-probe/manifest.json" ||
            failure == "cleanup_receipt" && path.Contains("/cleanup/", StringComparison.Ordinal)))
            throw new IOException("scripted_export_failure");
        string key = container + "/" + path;
        bool exists = objects.TryGetValue(key, out StoredArtifact? previous);
        if ((createOnly && exists) || (!createOnly && previous?.ETag != expectedETag)) return Task.FromResult(ArtifactWriteOutcome.Conflict);
        objects[key] = new(bytes.ToArray(), (++revision).ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Task.FromResult(ArtifactWriteOutcome.Stored);
    }
}

public sealed class RehearsalEnvironment(string? failure = null, List<string>? trace = null) : ITerraformEnvironment
{
    public int Applies { get; private set; }
    public int Destroys { get; private set; }
    public List<string> Trace { get; } = trace ?? [];
    private EnvironmentGeneration? generation;
    public Task<EnvironmentGeneration?> ReadGenerationAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(generation); }
    public Task ApplyAsync(string environmentRunId, DateTimeOffset startedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); Trace.Add("apply"); Applies++;
        generation = new(environmentRunId, expiresAt, "scripted-lineage", 1);
        Fail("apply"); return Task.CompletedTask;
    }
    public Task<JsonElement> ReadOutputsAsync(CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); Trace.Add("outputs"); Fail("outputs"); return Task.FromResult(JsonSerializer.SerializeToElement(new { synthetic = true })); }
    public Task PreflightAsync(JsonElement outputs, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); Trace.Add("preflight"); Fail("preflight"); return Task.CompletedTask; }
    public Task<DestroyPlan> PlanDestroyAsync(EnvironmentGeneration expected, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); Trace.Add("destroy_plan"); Fail("destroy_plan"); return Task.FromResult(new DestroyPlan("scripted-saved-plan", expected)); }
    public Task ApplyDestroyPlanAsync(DestroyPlan plan, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); Trace.Add("destroy"); Destroys++; Fail("destroy"); generation = null; return Task.CompletedTask; }
    private void Fail(string point) { if (failure == point) throw new IOException("scripted_" + point); }
}

public sealed class RehearsalCluster(WireFixtures fixtures, string? failure = null, string probeVariant = "pass", List<string>? trace = null) : IRunCluster
{
    public List<string> Trace { get; } = trace ?? [];
    public Task PreflightAsync(JsonElement outputs, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    public Task<PodAdmission> EstablishProducerWatchAsync(RepeatContext context, CancellationToken cancellationToken)
    { Step("watch", cancellationToken); return Task.FromResult(new PodAdmission(context.RunId, "1")); }
    public Task CreateProducerAsync(RepeatContext context, CancellationToken cancellationToken) { Step("producer_create", cancellationToken); return Task.CompletedTask; }
    public Task<ProducerPod> ObserveProducerAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken)
    {
        Step("admission", cancellationToken);
        var pod = new ProducerPod(context.RunId + "-pod", "scripted-uid-" + context.Ordinal);
        admission.Observe(new("2", "ADDED", pod));
        if (failure == "extra_producer") admission.Observe(new("3", "ADDED", new(context.RunId + "-silent", "silent-uid")));
        return Task.FromResult(pod);
    }
    public Task<string> GenerateComparisonAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken)
    { Step("comparison", cancellationToken); return Task.FromResult(new string('a', 64)); }
    public Task<StartLineExecution> StartLineAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken)
    { Step("start_line", cancellationToken); return Task.FromResult(new StartLineExecution(new(Receipt(context, "start-line", 0)), fixtures.Get(Prefix(context) + "broker-inventory.json"))); }
    public Task ReleaseAsync(RepeatContext context, byte[] permit, CancellationToken cancellationToken)
    {
        Step("release", cancellationToken);
        using JsonDocument document = JsonDocument.Parse(permit);
        if (document.RootElement.GetProperty("pod_name").GetString() != context.RunId + "-pod") throw new InvalidDataException("scripted_permit_mismatch");
        return Task.CompletedTask;
    }
    public Task<WorkerExecution> WaitProducerAsync(RepeatContext context, CancellationToken cancellationToken)
    { Step("loadgen", cancellationToken); return Task.FromResult(new WorkerExecution(Receipt(context, "loadgen", 0))); }
    public Task<ProducerPublication> DownloadPublicationAsync(RepeatContext context, CancellationToken cancellationToken)
    {
        Step("publication", cancellationToken);
        string[] names = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "index.json"];
        return Task.FromResult(new ProducerPublication(names.ToDictionary(name => name, name => fixtures.Get(Prefix(context) + name), StringComparer.Ordinal)));
    }
    public Task<WorkerExecution> VerifyAsync(RepeatContext context, WorkerIdentity identity, IReadOnlyList<string> brokers, CancellationToken cancellationToken)
    { Step("verify", cancellationToken); return Task.FromResult(new WorkerExecution(Receipt(context, "verify", 1), fixtures.Get((context.Phase == "probe" && probeVariant != "pass" ? "probe-" + probeVariant + "/r" + context.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" : Prefix(context)) + "result.json"))); }
    public Task CompleteProducerWatchAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken)
    { Step("watch_complete", cancellationToken); _ = admission.Producer; return Task.CompletedTask; }
    private ProcessResult Receipt(RepeatContext context, string mode, int exit) => new(exit, Encoding.UTF8.GetString(fixtures.Get(Prefix(context) + mode + "-receipt.txt")), "");
    private static string Prefix(RepeatContext context) => context.Phase + "/r" + context.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/";
    private void Step(string point, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); Trace.Add(point); if (failure == point) throw new IOException("scripted_" + point); }
}
