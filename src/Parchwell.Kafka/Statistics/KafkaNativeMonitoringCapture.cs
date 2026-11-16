using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Kafka.Statistics;

/// <summary>Deployment identity bound to one private per-pod monitoring index.</summary>
/// <param name="RunId">The run identifier.</param>
/// <param name="Phase">The phase identifier.</param>
/// <param name="ScenarioId">The scenario identifier.</param>
/// <param name="ConfigurationId">The configuration identifier.</param>
/// <param name="JobName">The job name.</param>
/// <param name="PodUid">The pod UID.</param>
/// <param name="SourceCommit">The source commit.</param>
public sealed record KafkaNativeMonitoringIdentity(string RunId, string Phase, string ScenarioId,
    string ConfigurationId, string JobName, string PodUid, string SourceCommit);

/// <summary>An authoritative reference to an admitted report in an existing capture.</summary>
/// <param name="Source">Container and full blob name, separated by a slash.</param>
/// <param name="ReportOrdinal">The report's ordinal in that source.</param>
/// <param name="ByteLength">The UTF-8 byte length of the original report.</param>
/// <param name="Sha256">The lowercase SHA-256 digest of the original report bytes.</param>
public sealed record KafkaNativeRawReference(string Source, long ReportOrdinal, long ByteLength, string Sha256);

/// <summary>Registers the shared native-monitoring factory and metric provider.</summary>
public static class KafkaNativeMonitoringServiceCollectionExtensions
{
    /// <summary>Registers monitoring services; the host supplies the private object store.</summary>
    /// <param name="services">The host service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddParchwellNativeMonitoring(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton(_ => new KafkaStatisticsMetrics());
        services.TryAddSingleton(provider => new KafkaNativeMonitoringFactory(
            provider.GetRequiredService<IKafkaNativeMonitoringStore>(), provider.GetRequiredService<KafkaStatisticsMetrics>(),
            provider.GetRequiredService<TimeProvider>()));
        return services;
    }
}

/// <summary>Creates a pod-scoped monitoring capture using host-owned storage.</summary>
public sealed class KafkaNativeMonitoringFactory
{
    private readonly IKafkaNativeMonitoringStore _store;
    private readonly KafkaStatisticsMetrics _metrics;
    private readonly TimeProvider _time;

    internal KafkaNativeMonitoringFactory(IKafkaNativeMonitoringStore store, KafkaStatisticsMetrics metrics, TimeProvider time)
    {
        _store = store;
        _metrics = metrics;
        _time = time;
    }

    /// <summary>Creates an independent pod coordinator.</summary>
    /// <param name="identity">The deployment identity.</param>
    /// <returns>A new coordinator.</returns>
    public KafkaNativeMonitoringCoordinator Create(KafkaNativeMonitoringIdentity identity) => new(_store, _metrics, _time, identity);
}

/// <summary>Coordinates bounded native monitoring capture for one pod.</summary>
public sealed class KafkaNativeMonitoringCoordinator
{
    private const int MaxReferences = 4096;
    private const int MaxDiagnostics = 128;
    private const int MaxReportBytes = 1024 * 1024;
    private const int MaxIndexBytes = 1024 * 1024;
    private const long MaxRawBytes = 128L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private readonly object _gate = new();
    private readonly IKafkaNativeMonitoringStore _store;
    private readonly KafkaStatisticsMetrics _metrics;
    private readonly TimeProvider _time;
    private readonly KafkaNativeMonitoringIdentity _identity;
    private readonly List<KafkaNativeMonitoringLifetime> _clients = [];
    private readonly Channel<Work> _queue = Channel.CreateBounded<Work>(new BoundedChannelOptions(128) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly Task _worker;
    private long _rawBytes;
    private int _references;
    private int _diagnostics;
    private bool _truncated;
    private bool _closed;
    private bool _publicationStarted;
    private bool _gap;
    private KafkaNativeMonitoringLifetime? _refusedLifetime;

    internal KafkaNativeMonitoringCoordinator(IKafkaNativeMonitoringStore store, KafkaStatisticsMetrics metrics, TimeProvider time, KafkaNativeMonitoringIdentity identity)
    {
        _store = store;
        _metrics = metrics;
        _time = time;
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        if (new[] { identity.RunId, identity.Phase, identity.ScenarioId, identity.ConfigurationId, identity.JobName, identity.PodUid, identity.SourceCommit }
            .Any(value => string.IsNullOrWhiteSpace(value) || value.Contains('/') || value.Contains('\\') || value is "." or ".." || value.Any(char.IsControl)))
            throw new ArgumentException("Monitoring identity fields must be nonempty path-safe identifiers.", nameof(identity));
        _worker = Task.Run(ProcessAsync);
    }

    /// <summary>Records a new native client build in build order.</summary>
    /// <param name="identity">The configured client identity.</param>
    /// <returns>The new lifetime.</returns>
    public KafkaNativeMonitoringLifetime CreateLifetime(KafkaStatisticsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        lock (_gate)
        {
            if (_closed || _clients.Count >= 32)
            {
                _gap = true;
                return _refusedLifetime ??= new KafkaNativeMonitoringLifetime(this, identity, 0, _metrics.CreateNativeState(identity, false), _time.GetTimestamp()) { Closed = true, Gap = true };
            }
            var client = new KafkaNativeMonitoringLifetime(this, identity, _clients.Count + 1, _metrics.CreateNativeState(identity, false), _time.GetTimestamp());
            _clients.Add(client);
            return client;
        }
    }

    /// <summary>Records an incomplete pod capture, including when no client was built.</summary>
    /// <param name="code">A static diagnostic identifier for the host failure.</param>
    public void MarkIncomplete(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length > 80 || code.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
            throw new ArgumentException("Diagnostic must be a static identifier.", nameof(code));
        lock (_gate) { _gap = true; }
    }

    internal void Submit(KafkaNativeMonitoringLifetime client, string raw, KafkaNativeRawReference? reference, bool bridge)
    {
        lock (_gate)
        {
            if (_closed || client.Closed) { Gap(client, "late_capture"); return; }
            if (raw is null || Encoding.UTF8.GetByteCount(raw) > MaxReportBytes) { Gap(client, "report_limit"); return; }
            if (_references >= MaxReferences) { Gap(client, "reference_limit"); return; }
            long ordinal = client.Count + 1;
            if (!_queue.Writer.TryWrite(new Work(client, raw, reference, ordinal, bridge, _time.GetTimestamp()))) { Gap(client, "queue_overflow"); return; }
            client.Count = ordinal;
            _references++;
        }
    }

    internal void Finish(KafkaNativeMonitoringLifetime client, bool captureComplete, string exportTailStatus)
    {
        lock (_gate)
        {
            if (client.Closed) { Gap(client, "duplicate_completion"); return; }
            client.Closed = true;
            client.CompleteFlag = captureComplete && client.BuildStatus == "built";
            client.ExportTailStatus = exportTailStatus;
            if (!client.CompleteFlag) Gap(client, "incomplete_lifetime");
        }
    }

    internal void Gap(KafkaNativeMonitoringLifetime client, string code)
    {
        lock (_gate)
        {
            _gap = true;
            client.Gap = true;
            if (code == "diagnostics_truncated") _truncated = true;
            if (!client.Diagnostics.Contains(code))
            {
                if (_diagnostics >= MaxDiagnostics) _truncated = true;
                else { client.Diagnostics.Add(code); _diagnostics++; }
            }
        }
    }

    private async Task ProcessAsync()
    {
        await foreach (Work work in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try { Process(work); }
            catch (Exception) { Gap(work.Client, "processing_failure"); }
        }
    }

    private void Process(Work work)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(work.Raw);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var client = work.Client;
        if (work.Reference is { } reference)
        {
            if (reference.ReportOrdinal <= 0 || reference.ByteLength != bytes.Length || reference.Sha256 != hash
                || string.IsNullOrWhiteSpace(reference.Source) || !reference.Source.Contains('/') || reference.Source.Contains("://", StringComparison.Ordinal))
            { Gap(client, "invalid_reference"); return; }
            client.Sources.Add(new("existing_capture", reference.Source, reference.ByteLength, reference.Sha256, reference.ReportOrdinal, 1));
        }
        else
        {
            byte[] row = JsonSerializer.SerializeToUtf8Bytes(new RawRow(client.Ordinal, work.Ordinal, work.Raw), JsonOptions);
            if (_rawBytes + row.Length + 1 > MaxRawBytes) { Gap(client, "raw_limit"); return; }
            if (client.NewRows == 0) client.FirstNewOrdinal = work.Ordinal;
            client.Raw.Write(row);
            client.Raw.WriteByte((byte)'\n');
            _rawBytes += row.Length + 1;
            client.NewRows++;
        }
        var projection = KafkaNativeMetricParser.Parse(work.Raw);
        if (!work.Bridge)
        {
            var native = KafkaStatisticsSession.ReadMonitoringIdentity(work.Raw);
            string type = client.Identity.Role is KafkaClientRole.EnrichmentConsumer or KafkaClientRole.VerifierConsumer ? "consumer" : "producer";
            long receipt = (long)((Int128)work.Receipt * 1_000_000 / _time.TimestampFrequency);
            Int128 startNumerator = (Int128)client.Start * 1_000_000;
            long start = (long)((startNumerator + _time.TimestampFrequency - 1) / _time.TimestampFrequency);
            bool valid = native is not null && native.ClientId == client.Identity.ClientId && native.Type == type
                && (client.Name is null || client.Name == native.Name)
                && (client.LastTimestamp is null || native.Ts > client.LastTimestamp)
                && (client.Creation is null || native.Ts - native.Age == client.Creation)
                && (!ReferenceEquals(_time, TimeProvider.System) || !OperatingSystem.IsLinux() || (native.Ts <= receipt && native.Ts - native.Age >= start));
            if (!valid) Gap(client, "invalid_native_identity");
            else
            {
                client.Name = native!.Name; client.LastTimestamp = native.Ts; client.Creation = native.Ts - native.Age;
                client.State.Record(projection);
                foreach (var diagnostic in client.State.Diagnostics) Gap(client, diagnostic.Code);
            }
        }
        else if (projection.Identity is { } identity) client.Name ??= identity.Name;
        foreach (var diagnostic in projection.Diagnostics) Gap(client, diagnostic.Code);
    }

    /// <summary>Closes admission and drains callbacks within the caller's budget.</summary>
    /// <param name="cancellationToken">The host cleanup deadline.</param>
    /// <returns>A task completing when the callback worker has drained.</returns>
    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_closed) { _closed = true; _queue.Writer.TryComplete(); }
        }
        await _worker.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Publishes the index after capture and existing evidence have drained.</summary>
    /// <param name="cancellationToken">The host cleanup deadline.</param>
    /// <returns>A task completing when the index has been created.</returns>
    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_publicationStarted) throw new InvalidOperationException("Publication is already started.");
            _publicationStarted = true;
        }
        await DrainAsync(cancellationToken).ConfigureAwait(false);
        foreach (var client in _clients.Where(c => c.BuildStatus == "pending"))
        {
            client.BuildStatus = "failed";
            Gap(client, "build_unresolved");
        }
        string prefix = $"{_identity.RunId}/{_identity.Phase}/{_identity.JobName}/{_identity.PodUid}/native-monitoring";
        foreach (var client in _clients.Where(c => c.NewRows > 0))
        {
            byte[] raw = client.Raw.ToArray();
            string path = $"{prefix}/raw-{client.Ordinal:D6}.jsonl";
            await _store.CreateAsync(path, raw, cancellationToken).ConfigureAwait(false);
            string hash = Convert.ToHexStringLower(SHA256.HashData(raw));
            client.Sources.Add(new("raw_chunk", path, raw.Length, hash, client.FirstNewOrdinal, client.NewRows));
        }
        var clients = _clients.Select(c => new ClientIndex(c.Ordinal, c.Identity.Role.ToString(), c.Identity.ClientId, c.Name,
            c.BuildStatus, c.Count, c.Sources, c.Diagnostics.Order(StringComparer.Ordinal).ToArray(), !c.Gap && c.Closed && c.CompleteFlag, c.ExportTailStatus)).ToArray();
        byte[] index = JsonSerializer.SerializeToUtf8Bytes(new Index(1, _identity, clients, !_gap && !_truncated && clients.All(c => c.CaptureComplete), _truncated), JsonOptions);
        if (index.Length > MaxIndexBytes) throw new InvalidOperationException("Native monitoring index exceeds its limit.");
        await _store.CreateAsync($"{prefix}/index.json", index, cancellationToken).ConfigureAwait(false);
    }

    internal sealed record Source(string Kind, string SourceName, long ByteLength, string Sha256, long ReportOrdinal, long RowCount)
    {
        [System.Text.Json.Serialization.JsonPropertyName("source")]
        public string SourceName { get; init; } = SourceName;
    }
    private sealed record Work(KafkaNativeMonitoringLifetime Client, string Raw, KafkaNativeRawReference? Reference, long Ordinal, bool Bridge, long Receipt);
    private sealed record RawRow(int ClientOrdinal, long ReportOrdinal, string Raw);
    private sealed record ClientIndex(int Ordinal, string Role, string ClientId, string? NativeIdentity, string BuildStatus, long ReportCount, List<Source> RawSources, string[] Diagnostics, bool CaptureComplete, string ExportTailStatus);
    private sealed record Index(int SchemaVersion, KafkaNativeMonitoringIdentity Identity, ClientIndex[] Clients, bool CaptureComplete, bool DiagnosticsTruncated);
}

/// <summary>One native client's monitoring state and capture admission.</summary>
public sealed class KafkaNativeMonitoringLifetime
{
    private readonly KafkaNativeMonitoringCoordinator _owner;
    internal KafkaNativeMonitoringLifetime(KafkaNativeMonitoringCoordinator owner, KafkaStatisticsIdentity identity, int ordinal, KafkaNativeMetricState state, long start)
    { _owner = owner; Identity = identity; Ordinal = ordinal; State = state; Start = start; }
    internal KafkaStatisticsIdentity Identity { get; }
    internal int Ordinal { get; }
    internal KafkaNativeMetricState State { get; }
    internal long Start { get; }
    internal long Count { get; set; }
    internal long NewRows { get; set; }
    internal long FirstNewOrdinal { get; set; }
    internal MemoryStream Raw { get; } = new();
    internal string? Name { get; set; }
    internal long? LastTimestamp { get; set; }
    internal long? Creation { get; set; }
    internal bool Closed { get; set; }
    internal bool Gap { get; set; }
    internal bool CompleteFlag { get; set; }
    internal string BuildStatus { get; set; } = "pending";
    internal string ExportTailStatus { get; set; } = "unknown";
    internal List<KafkaNativeMonitoringCoordinator.Source> Sources { get; } = [];
    internal HashSet<string> Diagnostics { get; } = new(StringComparer.Ordinal);
    /// <summary>Records a bounded monitoring gap.</summary>
    /// <param name="code">A static diagnostic code.</param>
    public void MarkIncomplete(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length > 80 || code.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
            throw new ArgumentException("Diagnostic must be a static identifier.", nameof(code));
        _owner.Gap(this, code);
    }
    /// <summary>Marks the native build successful.</summary>
    public void MarkBuilt() => BuildStatus = "built";
    /// <summary>Marks the native build failed.</summary>
    public void MarkBuildFailed() { BuildStatus = "failed"; _owner.Gap(this, "build_failed"); }
    /// <summary>Admits a report to new private raw capture.</summary>
    /// <param name="raw">The unmodified native JSON.</param>
    public void CaptureNew(string raw) => _owner.Submit(this, raw, null, false);
    /// <summary>Admits a report already retained by an existing raw writer.</summary>
    /// <param name="raw">The unmodified native JSON.</param>
    /// <param name="reference">The writer's authoritative admission reference.</param>
    public void CaptureExisting(string raw, KafkaNativeRawReference reference) => _owner.Submit(this, raw, reference ?? throw new ArgumentNullException(nameof(reference)), false);
    /// <summary>Observes a bridge report already retained by its existing raw writer.</summary>
    /// <param name="report">The processed bridge report.</param>
    /// <param name="reference">The writer's authoritative admission reference.</param>
    public void ObserveExisting(KafkaStatisticsReport report, KafkaNativeRawReference reference)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Identity != Identity || report.Monitoring is null || !report.Monitoring.IsComplete) _owner.Gap(this, "bridge_monitoring_incomplete");
        if (report.Monitoring is { } monitoring)
            foreach (var diagnostic in monitoring.Diagnostics)
            {
                string code = diagnostic.Code;
                _owner.Gap(this, !string.IsNullOrEmpty(code) && code.Length <= 80 && code.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
                    ? code : "invalid_diagnostic_code");
            }
        _owner.Submit(this, report.RawJson, reference ?? throw new ArgumentNullException(nameof(reference)), true);
    }
    /// <summary>Closes this lifetime after native disposal and existing capture flush.</summary>
    /// <param name="captureComplete">Whether the host observed complete capture and durable flush.</param>
    /// <param name="exportTailStatus">One of unknown, complete, or incomplete.</param>
    public void Complete(bool captureComplete, string exportTailStatus)
    {
        if (exportTailStatus is not ("unknown" or "complete" or "incomplete")) throw new ArgumentException("Invalid export tail status.", nameof(exportTailStatus));
        _owner.Finish(this, captureComplete, exportTailStatus);
    }
}
