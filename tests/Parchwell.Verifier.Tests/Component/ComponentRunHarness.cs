using System.Reflection;
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
using Parchwell.Verifier.Common.Scenario;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.StartLine;
using Parchwell.Verifier.VerifyRun;
using Parchwell.Verifier.VerifyRun.Storage;

namespace Parchwell.Verifier.Tests.Component;

/// <summary>Runs production handlers against local Kafka and explicitly synthetic serialized run artifacts.</summary>
internal sealed class ComponentRunHarness : IDisposable, ILedgerArtifactSource, IVerifierArtifactStore, IProducerArtifactStore
{
    private readonly ServiceProvider services;
    private readonly KafkaSessionTestSupport authentication = new();
    private readonly string bootstrap;
    private readonly ScenarioLoader loader;
    private readonly ScenarioResolver resolver;
    private static readonly JsonSerializerOptions WireOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    internal Dictionary<string, byte[]> Inputs { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, byte[]> Outputs { get; } = new(StringComparer.Ordinal);
    private Dictionary<string, byte[]> ProducerObjects { get; } = new(StringComparer.Ordinal);
    internal Action? AfterEndQuery { get; set; }
    internal Action? AfterAssign { get; set; }
    internal List<ErrorCode> NativeErrors { get; } = [];
    internal VerifierHostOptions Options { get; } = new()
    {
        ScenarioId = "A01", ConfigurationId = "C0", RunId = "synthetic-component-run", Phase = "C0", Repeat = 1,
        JobName = "synthetic-component-run-c0-r1-verify", RepeatPrefix = "synthetic-component-run/C0/r1/",
        PhasePrefix = "synthetic-component-run/C0/", SourceCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        PodName = "synthetic-component-pod", PodUid = "synthetic-component-pod-uid", NamespaceName = "syntheticns",
        VerifierContainer = "verifier-data", LedgerContainer = "ledger-data",
        InstanceId = "synthetic-component-instance", ExpectedInstanceId = "synthetic-component-run-c0-r1-loadgen",
        ClientId = "golden-loadgen-client", VerifierClientId = "synthetic-verifier", VerifierCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        TokenTimeout = TimeSpan.FromSeconds(10), MetadataTimeout = TimeSpan.FromSeconds(10),
        WatermarkTimeout = TimeSpan.FromSeconds(10), PollSlice = TimeSpan.FromMilliseconds(100),
        SafetyStop = TimeSpan.FromSeconds(10), IoTimeout = TimeSpan.FromSeconds(10),
        BlobIoTimeout = TimeSpan.FromSeconds(10), CleanupTimeout = TimeSpan.FromSeconds(30),
        MaxPartitions = 8, MaxInputDepth = 64, MaxArtifactBytes = 1_000_000, MaxTotalInputBytes = 10_000_000,
        MaxRowBytes = 1_000_000, MaxRowsPerArtifact = 100, MaxInventoryEntries = 10000, MaxInputEntries = 10000,
        MaxNativeReportBytes = 1_000_000, MaxBrokerEntries = 100, MaxConsumedRecords = 100, MaxConsumedBytes = 1_000_000,
        MaxOutputBytes = 1_000_000, MaxDiagnosticsPerCheck = 32, MaxDiagnosticBytes = 4096,
        ExpectedBrokerNames = null, StartLineOutputName = "synthetic-component-run/C0/r1/start-line.json",
        BrokerInventoryOutputName = "synthetic-component-run/C0/r1/broker-inventory.json",
        StartLineInputName = "synthetic-component-run/C0/r1/start-line.json",
        ResultOutputName = "synthetic-component-run/C0/r1/result.json",
        ArtifactPrefix = "synthetic-component-run/C0/r1/producer/"
    };

    internal ComponentRunHarness(string bootstrap, string? instance = null)
    {
        this.bootstrap = bootstrap;
        if (instance is not null) Options.InstanceId = instance;
        authentication.Refresh();
        var registrations = new ServiceCollection();
        foreach (KafkaClientRole role in new[] { KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer })
            registrations.AddParchwellKafka(role, options =>
            {
                options.NamespaceName = "syntheticns";
                options.ClientId = role == KafkaClientRole.LoadGenProducer ? "golden-loadgen-client" : "synthetic-verifier";
            });
        services = registrations.BuildServiceProvider();
        resolver = new ScenarioResolver(services.GetRequiredService<KafkaConfigurationFactory>());
        string root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "scenarios")))
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException("Repository scenarios unavailable.");
        loader = new ScenarioLoader(new FileSystemScenarioSource(root), Microsoft.Extensions.Options.Options.Create(new ScenarioLoadOptions
        { MaxDocumentCharacters = 100000, MaxNestingDepth = 32, MaxScalarCharacters = 10000, MaxParserEvents = 100000 }));
        foreach (string name in new[] { "index.json", "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json" })
        {
            string raw = File.ReadAllText(RecordedEvidence.FixturePath("captured-run/" + name));
            raw = raw.Replace("golden-run-001", Options.RunId, StringComparison.Ordinal)
                .Replace("golden-instance-001", Options.InstanceId, StringComparison.Ordinal);
            Inputs[name] = Encoding.UTF8.GetBytes(raw);
        }
        // These are serialized synthetic controls. The toxic tests later copy measured guard
        // counters from their retained native reports through ComponentEvidenceBuilder.
        var manifest = JsonNode.Parse(Inputs["manifest.json"])!;
        manifest["generator_version"] = "synthetic-component-producer";
        manifest["runtime_versions"] = new JsonObject { ["component_fixture"] = "synthetic" };
        Inputs["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest);
        Inputs["request-lifecycle.jsonl"] = [];
        string[] reports = Encoding.UTF8.GetString(Inputs["statistics.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < reports.Length; index++)
        {
            var envelope = JsonNode.Parse(reports[index])!;
            var native = JsonNode.Parse(envelope["raw_json"]!.GetValue<string>())!;
            native["name"] = "synthetic-component#producer";
            native["ts"] = 90 + 20 * index;
            native["age"] = 40 + 20 * index;
            foreach (var broker in native["brokers"]!.AsObject())
            {
                foreach (string counter in new[] { "req_timeouts", "disconnects", "txretries", "txerrs", "rxerrs" })
                    broker.Value![counter] = 0;
                broker.Value!["throttle"] = new JsonObject { ["cnt"] = 0, ["max"] = 0 };
                broker.Value["rtt"] = new JsonObject { ["cnt"] = 0, ["p99"] = 0 };
            }
            envelope["session_id"] = "synthetic-component-session";
            envelope["raw_json"] = native.ToJsonString();
            envelope["typed_snapshot"] = null;
            reports[index] = envelope.ToJsonString();
        }
        Inputs["statistics.jsonl"] = Encoding.UTF8.GetBytes(string.Join('\n', reports) + "\n");
        var capabilities = JsonNode.Parse(Inputs["capabilities.json"])!;
        capabilities["statistics_completion"]!["drain_boundary_microseconds"] = 100;
        capabilities["statistics_completion"]!["first_post_drain_timestamp_microseconds"] = 101;
        Inputs["capabilities.json"] = JsonSerializer.SerializeToUtf8Bytes(capabilities);
    }

    internal async Task SetAttemptsAsync(params JsonObject[] rows)
    {
        Inputs["ledger.jsonl"] = rows.Length == 0 ? []
            : Encoding.UTF8.GetBytes(string.Join('\n', rows.Select(row => row.ToJsonString())) + "\n");
        var manifest = JsonNode.Parse(Inputs["manifest.json"])!;
        manifest["planned_attempt_count"] = rows.Length;
        manifest["attempt_count"] = rows.Length;
        manifest["inventory"] = new JsonArray(rows.Select(row => row["attempt"]!.DeepClone()).ToArray());
        Inputs["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var capabilities = JsonNode.Parse(Inputs["capabilities.json"])!;
        var selected = resolver.Resolve(await loader.LoadAsync("A01")).Configurations["C0"].Roles[KafkaClientRole.LoadGenProducer];
        capabilities["resolved_configuration"] = JsonSerializer.SerializeToNode(new { selected.Role, selected.Settings, selected.Omissions }, WireOptions);
        Inputs["capabilities.json"] = JsonSerializer.SerializeToUtf8Bytes(capabilities);
    }

    internal JsonObject Row(string attempt, string key, bool delivered = true, int persistence = 2)
    {
        var row = JsonNode.Parse(Encoding.UTF8.GetString(Inputs["ledger.jsonl"]).Split('\n')[0])!.AsObject();
        row["attempt"]!["attempt_id"] = attempt;
        row["attempt"]!["key"] = key;
        row["attempt"]!["sequence"] = 1;
        row["delivered"] = delivered;
        row["error_code"] = delivered ? 0 : -192;
        row["persistence_status"] = persistence;
        return row;
    }

    internal async Task<int> StartAsync(CancellationToken token)
    {
        Options.JobName = PhaseIdentity.StartLineJobName(Options.RunId!, Options.Phase!, Options.Repeat!.Value);
        int result = await new StartLineHandler(
            Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System, Factory(false), this).ExecuteAsync(token);
        string noteName = Options.StartLineOutputName!;
        string inventoryName = Options.BrokerInventoryOutputName!;
        byte[] note = Outputs[noteName];
        byte[] inventory = Outputs[inventoryName];
        JsonArray discovered = JsonNode.Parse(inventory)!["brokers"]!.AsArray();
        string[] brokerNames = discovered.Select(item => item!["name"]!.GetValue<string>()).ToArray();
        string[] reports = Encoding.UTF8.GetString(Inputs["statistics.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < reports.Length; i++)
        {
            JsonObject envelope = JsonNode.Parse(reports[i])!.AsObject();
            JsonObject native = JsonNode.Parse(envelope["raw_json"]!.GetValue<string>())!.AsObject();
            JsonObject oldBrokers = native["brokers"]!.AsObject();
            JsonObject remapped = new();
            foreach ((string brokerName, JsonNode? broker) in oldBrokers)
                remapped[brokerNames[0]] = broker!.DeepClone();
            native["brokers"] = remapped;
            envelope["raw_json"] = native.ToJsonString();
            reports[i] = envelope.ToJsonString();
        }
        Inputs["statistics.jsonl"] = Encoding.UTF8.GetBytes(string.Join('\n', reports) + (reports.Length == 0 ? "" : "\n"));
        JsonObject capabilities = JsonNode.Parse(Inputs["capabilities.json"])!.AsObject();
        capabilities["observed_broker_names"] = new JsonArray(brokerNames.Select(name => JsonValue.Create(name)).ToArray());
        Inputs["capabilities.json"] = JsonSerializer.SerializeToUtf8Bytes(capabilities);
        string loadgenJob = PhaseIdentity.JobName(Options.RunId!, Options.Phase!, Options.Repeat.Value);
        var identity = new RunIdentity(Options.ScenarioId!, Options.ConfigurationId!, Options.RunId!, loadgenJob, loadgenJob, loadgenJob);
        ArtifactReference Ref(string name, byte[] bytes) =>
            new("verifier", name, bytes.LongLength, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        string timestamp = DateTimeOffset.UtcNow.ToString("O");
        var receipt = new WorkerReceipt(1, "start-line", Options.ScenarioId!, Options.ConfigurationId!, Options.RunId!,
            Options.Phase!, Options.Repeat, Options.JobName!, Options.PodName!, identity, Options.SourceCommit!, 0, true,
            timestamp, timestamp, [Ref(noteName, note), Ref(inventoryName, inventory)], null, null);
        Outputs[PhasePaths.StartLineCompletion(Options.RunId!, Options.Phase!, Options.Repeat.Value)] = PhaseWireCodec.WriteReceipt(receipt);
        Options.JobName = PhaseIdentity.VerifyJobName(Options.RunId!, Options.Phase!, Options.Repeat.Value);
        return result;
    }

    internal Task<int> VerifyAsync(CancellationToken token)
    {
        PrepareProducerArtifacts();
        return new VerifyRunHandler(
            Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System, Factory(true), this, this, loader,
            new Parchwell.Verifier.VerifyRun.Configuration.RegisteredProducerConfigurationResolver(Microsoft.Extensions.Options.Options.Create(Options))).ExecuteAsync(token);
    }

    private void PrepareProducerArtifacts()
    {
        ProducerObjects.Clear();
        string producerPrefix = PhasePaths.ProducerPrefix(Options.RunId!, Options.Phase!, Options.Repeat!.Value, Options.InstanceId!);
        string loadgenJob = PhaseIdentity.JobName(Options.RunId!, Options.Phase!, Options.Repeat.Value);
        var identity = new RunIdentity(Options.ScenarioId!, Options.ConfigurationId!, Options.RunId!,
            Options.InstanceId!, Options.InstanceId!, Options.ClientId!);
        var registration = new ProducerRegistration(1, identity, Options.Phase!, Options.Repeat.Value,
            loadgenJob, Options.PodName!, Options.PodUid!, Options.SourceCommit!);
        ProducerObjects[PhasePaths.Registration(Options.RunId!, Options.Phase!, Options.Repeat.Value, Options.InstanceId!)] =
            PhaseWireCodec.WriteRegistration(registration);
        ProducerObjects[PhasePaths.ProducerCompletion(Options.RunId!, Options.Phase!, Options.Repeat.Value, Options.InstanceId!)] =
            PhaseWireCodec.WriteProducerCompletion(new ProducerCompletionReceipt(1, identity, Options.Phase!, Options.Repeat.Value,
                loadgenJob, Options.PodName!, Options.PodUid!, Options.SourceCommit!, "loadgen", 0, true));
        string[] descriptors = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
        JsonObject index = JsonNode.Parse(Inputs["index.json"])!.AsObject();
        index["artifacts"] = new JsonArray(descriptors.Select(name =>
        {
            byte[] content = Inputs[name];
            return (JsonNode)new JsonObject
            {
                ["name"] = name,
                ["length"] = content.LongLength,
                ["sha256"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content))
            };
        }).ToArray());
        Inputs["index.json"] = JsonSerializer.SerializeToUtf8Bytes(index);
        foreach ((string name, byte[] content) in Inputs)
            ProducerObjects[producerPrefix + "publication/" + name] = content;
    }

    private VerifierKafkaSessionFactory Factory(bool end) => new(_ =>
    {
        var real = Consumer(bootstrap);
        var consumer = NativeProxy.Create<IConsumer<byte[], byte[]>>((method, arguments) =>
        {
            object? result;
            try { result = method.Invoke(real, arguments); }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                if (exception.InnerException is KafkaException kafka) NativeErrors.Add(kafka.Error.Code);
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            if (end && method.Name == "QueryWatermarkOffsets")
            {
                var action = AfterEndQuery;
                AfterEndQuery = null;
                action?.Invoke();
            }
            if (end && method.Name == "Assign")
            {
                var action = AfterAssign;
                AfterAssign = null;
                action?.Invoke();
            }
            return result;
        });
        return new VerifierKafkaSession(consumer, new DependentAdminClientBuilder(real.Handle).Build(),
            authentication.Authentication, TimeProvider.System, Options, new CancellationTokenSource());
    });

    internal static IConsumer<byte[], byte[]> Consumer(string bootstrap) => new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
    {
        BootstrapServers = bootstrap, GroupId = "synthetic-component-" + Guid.NewGuid().ToString("N"),
        EnableAutoCommit = false, EnableAutoOffsetStore = false, AutoOffsetReset = AutoOffsetReset.Error,
        IsolationLevel = IsolationLevel.ReadCommitted, AllowAutoCreateTopics = false
    }).Build();

    public Task<byte[]?> ReadAsync(string logicalFixedName, CancellationToken cancellationToken)
    {
        if (Outputs.TryGetValue(logicalFixedName, out byte[]? verifierBytes))
            return Task.FromResult<byte[]?>(verifierBytes);
        return Task.FromResult(ProducerObjects.GetValueOrDefault(logicalFixedName));
    }
    public Task<byte[]?> ReadStartLineAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(Outputs.GetValueOrDefault(name));
    public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) =>
        Task.FromResult(new BlobListing(ProducerObjects.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray(), 1, true));
    public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Outputs.Add(name, content);
        return Task.CompletedTask;
    }
    public void Dispose() { authentication.Dispose(); services.Dispose(); }
}
