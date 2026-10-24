using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.StartLine;
using Parchwell.Verifier.VerifyRun;
using Parchwell.Verifier.VerifyRun.Storage;

namespace Parchwell.Verifier.Tests.Support;

/// <summary>Builds coherent synthetic raw inputs independently of verifier readers and codecs.</summary>
internal sealed class HostEvidenceBuilder : IDisposable, ILedgerArtifactSource, IVerifierArtifactStore, IProducerArtifactStore, IScenarioSource
{
    private readonly ServiceProvider services;
    private readonly Dictionary<string, string> scenarioDocuments;
    private readonly JsonObject identity;
    private readonly Dictionary<string, byte[]> ledgerObjects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> verifierObjects = new(StringComparer.Ordinal);
    private bool fixtureIdentityInitialized;
    private static readonly JsonSerializerOptions WireOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public HostEvidenceBuilder()
    {
        Kafka.ReadMetadata = () =>
        {
            Metadata source = KafkaSessionTestSupport.Metadata(0);
            return new Metadata([new BrokerMetadata(1, "broker-a", 9093)], source.Topics, 0, "synthetic-broker");
        };
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
        identity = IdentityJson(new RunIdentity("A01", "C0", Options.RunId!, Options.InstanceId!,
            Options.InstanceId!, Options.ClientId!));
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
                    ["sasl_ssl://broker-a:9093/1"] = new JsonObject
                    {
                        ["name"] = "sasl_ssl://broker-a:9093/1", ["nodeid"] = 1, ["source"] = "learned", ["state"] = "UP",
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
        capabilities["identity"] = IdentityJson(new RunIdentity("A01", "C0", Options.RunId!, Options.InstanceId!,
            Options.InstanceId!, Options.ClientId!));
        capabilities["observed_broker_names"] = new JsonArray("sasl_ssl://broker-a:9093/1");
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
        RebuildPublicationReferences();
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
        Mode = "verify", ScenarioId = "A01", ConfigurationId = "C0", RunId = "synthetic-c5", Phase = "C0", Repeat = 1,
        InstanceId = "pod-c5", ExpectedInstanceId = "synthetic-c5-c0-r1-loadgen", ClientId = "pod-c5", PodName = "pod-c5", PodUid = "pod-c5",
        JobName = "synthetic-c5-c0-r1-verify", RepeatPrefix = "synthetic-c5/C0/r1/", PhasePrefix = "synthetic-c5/C0/",
        SourceCommit = new string('a', 40),
        VerifierClientId = "synthetic-verifier", NamespaceName = "synthetic-namespace", VerifierCommit = new string('a', 40),
        MaxInputDepth = 64, MaxArtifactBytes = 131072, MaxTotalInputBytes = 1048576, MaxRowBytes = 32768,
        MaxRowsPerArtifact = 100, MaxInventoryEntries = 8192, MaxInputEntries = 8192, MaxDiagnosticBytes = 256, MaxDiagnosticsPerCheck = 16,
        MaxNativeReportBytes = 32768, MaxBrokerEntries = 16, MaxPartitions = 2, MaxOutputBytes = 65536,
        MaxConsumedRecords = 16, MaxConsumedBytes = 4096, CleanupTimeout = TimeSpan.FromMilliseconds(100),
        IoTimeout = TimeSpan.FromMilliseconds(100), BlobIoTimeout = TimeSpan.FromMilliseconds(100),
        PollSlice = TimeSpan.FromMilliseconds(100), SafetyStop = TimeSpan.FromMilliseconds(1000),
        ExpectedBrokerNames = null, StartLineInputName = "synthetic-c5/C0/r1/start-line.json",
        ResultOutputName = "synthetic-c5/C0/r1/result.json", ArtifactPrefix = "synthetic-c5/C0/r1/producer/",
        StartLineOutputName = "synthetic-c5/C0/r1/start-line.json",
        BrokerInventoryOutputName = "synthetic-c5/C0/r1/broker-inventory.json"
    };
    public ScenarioLoader Loader { get; }
    public ScenarioResolver Resolver { get; }
    public Dictionary<string, byte[]?> Inputs { get; } = new(StringComparer.Ordinal);
    public List<string> Reads { get; } = [];
    public List<string> Events { get; } = [];
    public byte[]? Note { get; set; }
    public bool OmitStartLineNote { get; set; }
    public byte[]? Output { get; private set; }
    public string? OutputName { get; private set; }
    public int Creates { get; private set; }
    public List<string> CreateNames { get; } = [];
    public Dictionary<string, byte[]> CreatedObjects { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, byte[]> DiagnosticInputs { get; } = new(StringComparer.Ordinal);
    public Action<string, CancellationToken>? BeforeRead { get; set; }
    public Action<CancellationToken>? BeforeScenarioRead { get; set; }
    public Action? InputDispose { get; set; }
    public Action<CancellationToken>? AfterCreate { get; set; }
    public Action<CancellationToken>? SessionCreated { get; set; }
    public Func<ConsumeResult<byte[], byte[]>?>? ConsumeRecord { get; set; }
    public TimeProvider? HandlerClock { get; set; }

    public StartLineHandler StartLine() => new(Microsoft.Extensions.Options.Options.Create(Options), HandlerClock ?? Kafka.Clock,
        new VerifierKafkaSessionFactory(token => { SessionCreated?.Invoke(token); return Kafka.Session; }), this);
    public VerifyRunHandler Verify()
    {
        if (!PreservePublicationReferences) RebuildPublicationReferences();
        return new VerifyRunHandler(Microsoft.Extensions.Options.Options.Create(Options), HandlerClock ?? Kafka.Clock,
            new VerifierKafkaSessionFactory(token => { SessionCreated?.Invoke(token); return Kafka.Session; }), this, this, Loader,
            new Parchwell.Verifier.VerifyRun.Configuration.RegisteredProducerConfigurationResolver(Microsoft.Extensions.Options.Options.Create(Options)));
    }
    public bool PreservePublicationReferences { get; set; }
    public long ChargedInputBytes => ledgerObjects.Values.Sum(bytes => (long)bytes.Length)
        + verifierObjects.Values.Sum(bytes => (long)bytes.Length);

    public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        Reads.Add(name);
        BeforeRead?.Invoke(name, cancellationToken);
        if (verifierObjects.TryGetValue(name, out byte[]? verifierBytes)) return Task.FromResult<byte[]?>(verifierBytes);
        string publicationPrefix = PhasePaths.ProducerPrefix(Options.RunId!, Options.Phase!, Options.Repeat!.Value, Options.InstanceId!) + "publication/";
        if (name.StartsWith(publicationPrefix, StringComparison.Ordinal))
        {
            string logical = name[(name.LastIndexOf('/') + 1)..];
            return Task.FromResult(Inputs.GetValueOrDefault(logical));
        }
        string diagnosticPrefix = PhasePaths.ProducerPrefix(Options.RunId!, Options.Phase!, Options.Repeat!.Value, Options.InstanceId!) + "diagnostics/";
        if (name.StartsWith(diagnosticPrefix, StringComparison.Ordinal))
        {
            string logical = name[diagnosticPrefix.Length..];
            return Task.FromResult(DiagnosticInputs.TryGetValue(logical, out byte[]? bytes) ? bytes : null);
        }
        if (ledgerObjects.TryGetValue(name, out byte[]? ledgerBytes)) return Task.FromResult<byte[]?>(ledgerBytes);
        return Task.FromResult<byte[]?>(null);
    }
    public Task<byte[]?> ReadStartLineAsync(string name, CancellationToken cancellationToken)
    {
        Reads.Add(name);
        BeforeRead?.Invoke(name, cancellationToken);
        return Task.FromResult<byte[]?>(verifierObjects.GetValueOrDefault(name));
    }
    public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken)
    {
        string[] names = ledgerObjects.Keys.Concat(DiagnosticInputs.Keys.Select(name =>
                PhasePaths.ProducerPrefix(Options.RunId!, Options.Phase!, Options.Repeat!.Value, Options.InstanceId!) + "diagnostics/" + name))
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        return Task.FromResult(new BlobListing(names, 1, true));
    }
    public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken)
    {
        Creates++;
        CreateNames.Add(name);
        CreatedObjects[name] = content;
        OutputName = name;
        Output = content;
        verifierObjects[name] = content;
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
    public void RebuildPublicationReferences()
    {
        linesBuilder.Clear();
        RunIdentity producerIdentity = new(Options.ScenarioId!, Options.ConfigurationId!, Options.RunId!, Options.InstanceId!,
            Options.InstanceId!, Options.ClientId!);
        string[] descriptors = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
        if (!fixtureIdentityInitialized)
        {
            UpdateObject("index.json", value => value["identity"] = IdentityJson(producerIdentity));
            UpdateObject("manifest.json", value =>
            {
                value["identity"] = IdentityJson(producerIdentity);
                if (value["inventory"] is JsonArray rows)
                    foreach (JsonNode? row in rows)
                        if (row is JsonObject attempt) attempt["instance_id"] = producerIdentity.InstanceId;
            });
            UpdateObject("capabilities.json", value =>
            {
                value["identity"] = IdentityJson(producerIdentity);
                value["observed_broker_names"] = new JsonArray("sasl_ssl://broker-a:9093/1");
            });
            foreach (string line in (Inputs["statistics.jsonl"] is byte[] statisticsBytes
                ? Encoding.UTF8.GetString(statisticsBytes).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>()))
            {
                JsonObject envelope;
                try { envelope = JsonNode.Parse(line)!.AsObject(); }
                catch (System.Text.Json.JsonException) { linesBuilder.Append(line).Append('\n'); continue; }
                envelope["identity"] = IdentityJson(producerIdentity);
                JsonObject statisticsIdentity = envelope["statistics_identity"]!.AsObject();
                statisticsIdentity["scenario_id"] = producerIdentity.ScenarioId;
                statisticsIdentity["run_id"] = producerIdentity.RunId;
                statisticsIdentity["client_id"] = producerIdentity.ClientId;
                JsonObject raw = JsonNode.Parse(envelope["raw_json"]!.GetValue<string>())!.AsObject();
                raw["client_id"] = producerIdentity.ClientId;
                raw["name"] = "synthetic-client#" + producerIdentity.ClientId;
                envelope["raw_json"] = raw.ToJsonString();
                linesBuilder.Append(envelope.ToJsonString()).Append('\n');
            }
            if (Inputs["statistics.jsonl"] is byte[]) Inputs["statistics.jsonl"] = Encoding.UTF8.GetBytes(linesBuilder.ToString());
            linesBuilder.Clear();
            foreach (string line in (Inputs["ledger.jsonl"] is byte[] ledgerBytes
                ? Encoding.UTF8.GetString(ledgerBytes).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>()))
            {
                JsonObject row;
                try { row = JsonNode.Parse(line)!.AsObject(); }
                catch (System.Text.Json.JsonException) { linesBuilder.Append(line).Append('\n'); continue; }
                row["identity"] = IdentityJson(producerIdentity);
                if (row["attempt"] is JsonObject attempt) attempt["instance_id"] = producerIdentity.InstanceId;
                linesBuilder.Append(row.ToJsonString()).Append('\n');
            }
            if (Inputs["ledger.jsonl"] is byte[]) Inputs["ledger.jsonl"] = Encoding.UTF8.GetBytes(linesBuilder.ToString());
            fixtureIdentityInitialized = true;
        }
        UpdateObject("index.json", index =>
        {
            var items = new JsonArray();
            foreach (string name in descriptors)
            {
                if (Inputs[name] is byte[] bytes)
                {
                    items.Add(new JsonObject
                    {
                        ["name"] = name, ["length"] = bytes.LongLength,
                        ["sha256"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))
                    });
                }
                else if (index["artifacts"] is JsonArray old)
                {
                    JsonNode? retained = old.FirstOrDefault(item => item?["name"]?.GetValue<string>() == name);
                    if (retained is not null) items.Add(retained.DeepClone());
                }
            }
            index["artifacts"] = items;
        });
        var startIdentity = new RunIdentity(Options.ScenarioId!, Options.ConfigurationId!, Options.RunId!,
            PhaseIdentity.JobName(Options.RunId!, Options.Phase!, Options.Repeat!.Value),
            PhaseIdentity.JobName(Options.RunId!, Options.Phase!, Options.Repeat!.Value),
            PhaseIdentity.JobName(Options.RunId!, Options.Phase!, Options.Repeat!.Value));
        var limits = new OutputLimits(Options.MaxOutputBytes!.Value, Options.MaxDiagnosticsPerCheck!.Value, Options.MaxDiagnosticBytes!.Value);
        if (!OmitStartLineNote)
            Note ??= StartLineNoteCodec.Write(new StartLineNote(1, startIdentity, VerifierKafkaSession.Topic,
                [new PartitionHigh(0, 0)], "2026-10-24T00:00:00.0000000+00:00", Options.SourceCommit!,
                "synthetic-managed", "synthetic-native"), limits);
        if (!verifierObjects.TryGetValue(Options.BrokerInventoryOutputName!, out byte[]? inventory))
        {
            inventory = BrokerInventoryCodec.Write(startIdentity, [new BrokerMetadata(1, "broker-a", 9093)],
                Options.SourceCommit!, Options.MaxOutputBytes.Value);
            verifierObjects[Options.BrokerInventoryOutputName!] = inventory;
        }
        if (Note is null) verifierObjects.Remove(Options.StartLineInputName!);
        else verifierObjects[Options.StartLineInputName!] = Note;
        ArtifactReference Ref(string container, string name, byte[] bytes) =>
            new(container, name, bytes.LongLength, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        string startJob = PhaseIdentity.StartLineJobName(Options.RunId!, Options.Phase!, Options.Repeat.Value);
        var startReceipt = new WorkerReceipt(1, "start-line", Options.ScenarioId!, Options.ConfigurationId!, Options.RunId!,
            Options.Phase!, Options.Repeat, startJob, "start-line-pod", startIdentity, Options.SourceCommit!, 0, true,
            "2026-10-24T00:00:00.0000000+00:00", "2026-10-24T00:00:01.0000000+00:00",
            [Ref("verifier", Options.StartLineInputName!, Note ?? []), Ref("verifier", Options.BrokerInventoryOutputName!, inventory)], null, null);
        verifierObjects[PhasePaths.StartLineCompletion(Options.RunId!, Options.Phase!, Options.Repeat.Value)] = PhaseWireCodec.WriteReceipt(startReceipt);
        ledgerObjects.Clear();
        string producerPrefix = PhasePaths.ProducerPrefix(Options.RunId!, Options.Phase!, Options.Repeat.Value, Options.InstanceId!);
        string jobName = PhaseIdentity.JobName(Options.RunId!, Options.Phase!, Options.Repeat.Value);
        var registration = new ProducerRegistration(1, producerIdentity, Options.Phase!, Options.Repeat.Value, jobName,
            Options.PodName!, Options.PodUid!, Options.SourceCommit!);
        ledgerObjects[PhasePaths.Registration(Options.RunId!, Options.Phase!, Options.Repeat.Value, Options.InstanceId!)] =
            PhaseWireCodec.WriteRegistration(registration);
        ledgerObjects[PhasePaths.ProducerCompletion(Options.RunId!, Options.Phase!, Options.Repeat.Value, Options.InstanceId!)] =
            PhaseWireCodec.WriteProducerCompletion(new ProducerCompletionReceipt(1, producerIdentity, Options.Phase!,
                Options.Repeat.Value, jobName, Options.PodName!, Options.PodUid!, Options.SourceCommit!, "loadgen", 0, true));
        foreach (string name in PublicationNames)
            if (Inputs[name] is byte[] bytes) ledgerObjects[PhasePaths.Publication(Options.RunId!, Options.Phase!, Options.Repeat.Value, Options.InstanceId!, name)] = bytes;
    }
    private static readonly string[] PublicationNames = ["index.json", "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
    private readonly StringBuilder linesBuilder = new();
    private static JsonObject IdentityJson(RunIdentity value) => new()
    {
        ["scenario_id"] = value.ScenarioId, ["configuration_id"] = value.ConfigurationId, ["run_id"] = value.RunId,
        ["instance_id"] = value.InstanceId, ["expected_instance_id"] = value.ExpectedInstanceId, ["client_id"] = value.ClientId
    };
    private void UpdateObject(string name, Action<JsonObject> update)
    {
        if (Inputs[name] is not byte[] bytes) return;
        try
        {
            JsonObject value = JsonNode.Parse(bytes)!.AsObject();
            update(value);
            Set(name, value);
        }
        catch (System.Text.Json.JsonException) { }
    }
    public static ConsumeResult<byte[], byte[]> Record() => new()
    {
        Topic = "document-events", Partition = 0, Offset = 0,
        Message = new Message<byte[], byte[]>
        {
            Key = Encoding.UTF8.GetBytes("tenant-golden-d0"), Value = [],
            Headers = new Headers
            {
                { "instance-id", Encoding.UTF8.GetBytes("pod-c5") },
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
