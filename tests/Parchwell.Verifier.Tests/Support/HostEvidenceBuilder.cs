using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.StartLine;
using Parchwell.Verifier.VerifyRun;
using Parchwell.Verifier.VerifyRun.Storage;

namespace Parchwell.Verifier.Tests.Support;

/// <summary>Builds coherent synthetic raw inputs independently of verifier readers and codecs.</summary>
internal sealed class HostEvidenceBuilder : IDisposable, ILedgerArtifactSource, IVerifierArtifactStore, IScenarioSource
{
    private readonly ServiceProvider services;
    private readonly Dictionary<string, string> scenarioDocuments;
    private readonly JsonObject identity;
    private static readonly JsonSerializerOptions WireOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public HostEvidenceBuilder()
    {
        var registrations = new ServiceCollection();
        registrations.AddParchwellKafka(KafkaClientRole.LoadGenProducer, baseline =>
        {
            baseline.NamespaceName = Options.NamespaceName;
            baseline.ClientId = Options.ClientId;
        });
        registrations.AddParchwellKafka(KafkaClientRole.VerifierConsumer, baseline =>
        {
            baseline.NamespaceName = Options.NamespaceName;
            baseline.ClientId = Options.VerifierClientId;
        });
        services = registrations.BuildServiceProvider();
        Resolver = new ScenarioResolver(services.GetRequiredService<KafkaConfigurationFactory>());
        string root = RepositoryRoot();
        scenarioDocuments = new(StringComparer.Ordinal)
        {
            ["scenarios/A01/scenario.yaml"] = File.ReadAllText(Path.Combine(root, "scenarios/A01/scenario.yaml")),
            ["scenarios/profiles/lifecycle-1k.yaml"] = ""
        };
        // The profile reference is read from the repository scenario, independently of captured evidence.
        string profile = scenarioDocuments["scenarios/A01/scenario.yaml"].Split('\n')
            .Single(line => line.StartsWith("profile:", StringComparison.Ordinal))["profile:".Length..].Trim();
        scenarioDocuments.Remove("scenarios/profiles/lifecycle-1k.yaml");
        scenarioDocuments["scenarios/" + profile] = File.ReadAllText(Path.Combine(root, "scenarios", profile));
        Loader = new ScenarioLoader(this, Microsoft.Extensions.Options.Options.Create(new ScenarioLoadOptions
        {
            MaxDocumentCharacters = 131072, MaxNestingDepth = 32, MaxScalarCharacters = 32768, MaxParserEvents = 16384
        }));
        identity = JsonNode.Parse(ReadGolden("index.json"))!["identity"]!.DeepClone().AsObject();
        foreach (string name in new[] { "index.json", "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json" })
            Inputs[name] = ReadGolden(name);
        JsonObject manifest = JsonNode.Parse(Inputs["manifest.json"]!)!.AsObject();
        manifest["generator_version"] = "synthetic-producer";
        manifest["runtime_versions"] = new JsonObject { ["Confluent.Kafka"] = "synthetic-native" };
        manifest["inventory"] = new JsonArray(manifest["inventory"]![0]!.DeepClone());
        manifest["planned_attempt_count"] = 1;
        manifest["attempt_count"] = 1;
        Set("manifest.json", manifest);
        Inputs["ledger.jsonl"] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Inputs["ledger.jsonl"]!).Split('\n')[0] + "\n");
        Inputs["request-lifecycle.jsonl"] = [];
        var statistics = new StringBuilder();
        foreach (long timestamp in new long[] { 90, 100, 110 })
        {
            var native = new JsonObject
            {
                ["name"] = "synthetic-client#producer", ["client_id"] = Options.ClientId, ["type"] = "producer",
                ["ts"] = timestamp, ["age"] = timestamp - 50,
                ["brokers"] = new JsonObject
                {
                    ["broker-a/1"] = new JsonObject
                    {
                        ["name"] = "broker-a/1", ["nodeid"] = 1, ["source"] = "learned", ["state"] = "UP",
                        ["req_timeouts"] = 0, ["disconnects"] = 0, ["txretries"] = 0, ["txerrs"] = 0, ["rxerrs"] = 0,
                        ["throttle"] = new JsonObject { ["cnt"] = 0, ["max"] = 0 },
                        ["rtt"] = new JsonObject { ["cnt"] = 0, ["p99"] = 0 }
                    }
                }
            };
            var envelope = new JsonObject
            {
                ["schema_version"] = 1, ["identity"] = identity.DeepClone(),
                ["statistics_identity"] = new JsonObject
                {
                    ["scenario_id"] = Options.ScenarioId, ["run_id"] = Options.RunId, ["role"] = 0, ["client_id"] = Options.ClientId
                },
                ["session_id"] = "synthetic-session", ["received_at"] = "synthetic", ["receipt_time"] = "synthetic",
                ["raw_json"] = native.ToJsonString(), ["typed_snapshot"] = null, ["diagnostics"] = new JsonArray()
            };
            statistics.Append(envelope.ToJsonString()).Append('\n');
        }
        Inputs["statistics.jsonl"] = Encoding.UTF8.GetBytes(statistics.ToString());
        JsonObject capabilities = JsonNode.Parse(Inputs["capabilities.json"]!)!.AsObject();
        capabilities["statistics_completion"] = new JsonObject
        {
            ["is_complete"] = true, ["drain_boundary_microseconds"] = 100,
            ["missing_brokers"] = new JsonArray(), ["diagnostics"] = new JsonArray()
        };
        // The handler resolves the same source itself; no parsed verifier projection is injected.
        LoadedScenario loaded = Loader.LoadAsync("A01").AsTask().GetAwaiter().GetResult();
        capabilities["resolved_configuration"] = JsonSerializer.SerializeToNode(
            Resolver.Resolve(loaded).Configurations["C0"].Roles[KafkaClientRole.LoadGenProducer], WireOptions);
        Set("capabilities.json", capabilities);
        Events.Clear();
        Note = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 1, identity, topic = "document-events",
            partition_highs = new[] { new { partition = 0, high = 0 } }, captured_at = "synthetic",
            verifier_commit = "synthetic-start", confluent_kafka_version = "synthetic-managed", librdkafka_version = "synthetic-native"
        });
        Kafka.Poll = Kafka.Refresh;
        Kafka.Query = _ => new WatermarkOffsets(0, 1);
        long position = 0;
        Kafka.Position = _ => new Offset(position);
        bool assigned = false;
        Kafka.Assign = _ => assigned = true;
        Kafka.ReadRecord = () => assigned ? ConsumeRecord?.Invoke() : null;
        ConsumeRecord = () =>
        {
            position = 1;
            return Record();
        };
        Kafka.AdminDispose = () => Events.Add("admin-dispose");
        Kafka.ConsumerDispose = () => Events.Add("consumer-dispose");
    }

    public KafkaSessionTestSupport Kafka { get; } = new();
    public VerifierHostOptions Options { get; } = new()
    {
        Mode = "verify", ScenarioId = "A01", ConfigurationId = "C0", RunId = "golden-run-001",
        InstanceId = "golden-instance-001", ExpectedInstanceId = "golden-instance-001", ClientId = "golden-loadgen-client",
        VerifierClientId = "synthetic-verifier", NamespaceName = "synthetic-namespace", VerifierCommit = "synthetic-verifier-commit",
        MaxInputDepth = 64, MaxArtifactBytes = 131072, MaxTotalInputBytes = 1048576, MaxRowBytes = 32768,
        MaxRowsPerArtifact = 100, MaxInventoryEntries = 8192, MaxInputEntries = 8192, MaxDiagnosticBytes = 256, MaxDiagnosticsPerCheck = 16,
        MaxNativeReportBytes = 32768, MaxBrokerEntries = 16, MaxPartitions = 2, MaxOutputBytes = 65536,
        MaxConsumedRecords = 16, MaxConsumedBytes = 4096, CleanupTimeout = TimeSpan.FromMilliseconds(100),
        IoTimeout = TimeSpan.FromMilliseconds(100), BlobIoTimeout = TimeSpan.FromMilliseconds(100),
        PollSlice = TimeSpan.FromMilliseconds(100), SafetyStop = TimeSpan.FromMilliseconds(1000),
        ExpectedBrokerNames = ["broker-a/1"], StartLineInputName = "notes/custom.json", ResultOutputName = "results/custom.json",
        StartLineOutputName = "notes/new.json"
    };
    public ScenarioLoader Loader { get; }
    public ScenarioResolver Resolver { get; }
    public Dictionary<string, byte[]?> Inputs { get; } = new(StringComparer.Ordinal);
    public List<string> Reads { get; } = [];
    public List<string> Events { get; } = [];
    public byte[]? Note { get; set; }
    public byte[]? Output { get; private set; }
    public string? OutputName { get; private set; }
    public int Creates { get; private set; }
    public Action<string, CancellationToken>? BeforeRead { get; set; }
    public Action<CancellationToken>? BeforeScenarioRead { get; set; }
    public Action? InputDispose { get; set; }
    public Action<CancellationToken>? AfterCreate { get; set; }
    public Action<CancellationToken>? SessionCreated { get; set; }
    public Func<ConsumeResult<byte[], byte[]>?>? ConsumeRecord { get; set; }
    public TimeProvider? HandlerClock { get; set; }

    public StartLineHandler StartLine() => new(Microsoft.Extensions.Options.Options.Create(Options), HandlerClock ?? Kafka.Clock,
        new VerifierKafkaSessionFactory(token => { SessionCreated?.Invoke(token); return Kafka.Session; }), this);
    public VerifyRunHandler Verify() => new(Microsoft.Extensions.Options.Options.Create(Options), HandlerClock ?? Kafka.Clock,
        new VerifierKafkaSessionFactory(token => { SessionCreated?.Invoke(token); return Kafka.Session; }), this, this, Loader, Resolver, new HelmValuesGenerator());

    public Task<byte[]?> ReadAsync(string logicalFixedName, CancellationToken cancellationToken)
    {
        Reads.Add(logicalFixedName);
        BeforeRead?.Invoke(logicalFixedName, cancellationToken);
        return Task.FromResult(Inputs.GetValueOrDefault(logicalFixedName));
    }
    public Task<byte[]?> ReadStartLineAsync(string name, CancellationToken cancellationToken)
    {
        Reads.Add(name);
        BeforeRead?.Invoke(name, cancellationToken);
        return Task.FromResult(Note);
    }
    public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken)
    {
        Creates++;
        OutputName = name;
        Output = content;
        Events.Add("create");
        AfterCreate?.Invoke(cancellationToken);
        return Task.CompletedTask;
    }
    public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
    {
        BeforeScenarioRead?.Invoke(cancellationToken);
        return ValueTask.FromResult<TextReader>(new TrackedReader(scenarioDocuments[logicalPath], () =>
        {
            Events.Add("input-dispose");
            InputDispose?.Invoke();
        }));
    }
    public void Set(string name, JsonNode value) => Inputs[name] = Encoding.UTF8.GetBytes(value.ToJsonString());
    public JsonObject Json(string name) => JsonNode.Parse(Inputs[name]!)!.AsObject();
    public static ConsumeResult<byte[], byte[]> Record() => new()
    {
        Topic = "document-events", Partition = 0, Offset = 0,
        Message = new Message<byte[], byte[]>
        {
            Key = Encoding.UTF8.GetBytes("tenant-golden-d0"), Value = [],
            Headers = new Headers
            {
                { "instance-id", Encoding.UTF8.GetBytes("golden-instance-001") },
                { "attempt-id", Encoding.UTF8.GetBytes("golden-attempt-000") },
                { "sequence", Encoding.UTF8.GetBytes("0") }
            }
        }
    };
    public void Dispose()
    {
        try { Kafka.Dispose(); }
        finally { services.Dispose(); }
    }
    public void DelayHandlerTimerCallbacks() => HandlerClock = new DelayedTimerProvider(Kafka.Clock);

    private sealed class DelayedTimerProvider(TimeProvider monotonic) : TimeProvider
    {
        public override long TimestampFrequency => monotonic.TimestampFrequency;
        public override long GetTimestamp() => monotonic.GetTimestamp();
        public override DateTimeOffset GetUtcNow() => monotonic.GetUtcNow();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new DelayedTimer();

        private sealed class DelayedTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private static byte[] ReadGolden(string name) => File.ReadAllBytes(RecordedEvidence.FixturePath("captured-run/" + name));
    private static string RepositoryRoot()
    {
        string? configured = Environment.GetEnvironmentVariable("PARCHWELL_REPOSITORY_ROOT");
        if (!string.IsNullOrEmpty(configured)) return configured;
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Parchwell.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root is unavailable.");
    }
    private sealed class TrackedReader(string content, Action disposed) : StringReader(content)
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) disposed();
        }
    }
}
