using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.LoadGen.Run.Input;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Tests.Evidence;

internal static class GoldenArtifactFixture
{
    internal const string Endpoint = "https://golden-artifacts.blob.test/";
    internal const string Container = "ledger";
    internal const long DrainBoundaryMicroseconds = 12_000_000;
    internal static readonly DateTimeOffset CapturedAt = DateTimeOffset.Parse("2026-10-17T12:34:56.789Z",
        System.Globalization.CultureInfo.InvariantCulture);

    internal static readonly string[] ArtifactNames =
    ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];

    internal static async Task<GoldenRun> CreateAsync(string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        RunIdentity identity = Identity();
        ImmutableArray<AttemptEntry> inventory = RegisterInventory(identity);
        var expectations = new RunEvidenceExpectations(await ResolveBaselineAsync(identity.ClientId, cancellationToken),
            true, ["broker-a/1"]);

        string ledgerPath = Path.Combine(directory, "ledger.jsonl");
        string manifestPath = Path.Combine(directory, "manifest.json");
        var ledgerOptions = new LedgerStoreOptions(ledgerPath, manifestPath, 8, 16 * 1024, 64 * 1024,
            16 * 1024, TimeSpan.FromSeconds(5));
        await using var ledger = new LocalLedgerStore(ledgerOptions);
        if (!ledger.TryAppend(CallbackRow(identity, inventory[0])) ||
            !ledger.TryAppend(SynchronousRejectionRow(identity, inventory[1])))
            throw new InvalidOperationException("synthetic_ledger_append_rejected");

        string evidenceDirectory = directory;
        var evidenceOptions = new RunEvidenceOptions(evidenceDirectory, 16, 16 * 1024, 64 * 1024,
            32, TimeSpan.FromSeconds(5));
        await using var evidence = new RunEvidenceSession(identity, evidenceOptions,
            new FixedUtcTimeProvider(CapturedAt), expectations);
        await evidence.WriteAsync(StatisticsReport(identity, "golden-statistics-session", 10_000_000, 1_000_000,
            throttleCount: 0, throttleMaximum: null, rttCount: 0, rttP99: null), cancellationToken);
        await evidence.WriteAsync(StatisticsReport(identity, "golden-statistics-session", 15_000_000, 6_000_000,
            throttleCount: 2, throttleMaximum: 17, rttCount: 4, rttP99: 99327), cancellationToken);
        evidence.CaptureLog(new LogMessage("golden-client#producer", SyslogLevel.Info, "BROKER",
            "synthetic broker state notification"));
        evidence.CaptureError(new Error(ErrorCode.NoError));

        RunManifest manifest = Manifest(identity, inventory);
        await ledger.CompleteAsync(manifest, cancellationToken);
        var completion = new KafkaStatisticsCompletion(true, [], [])
        {
            DrainBoundaryMicroseconds = GoldenArtifactFixture.DrainBoundaryMicroseconds
        };
        await evidence.CompleteAsync(completion, cancellationToken);

        ImmutableDictionary<string, string> paths = ImmutableDictionary<string, string>.Empty
            .Add("ledger", ledgerPath)
            .Add("manifest", manifestPath)
            .Add("statistics.jsonl", Path.Combine(directory, "statistics.jsonl"))
            .Add("request-lifecycle.jsonl", Path.Combine(directory, "request-lifecycle.jsonl"))
            .Add("capabilities.json", Path.Combine(directory, "capabilities.json"));
        var result = new RunExecutionResult(0, manifest, paths, expectations);

        using var handler = new RecordingBlobHandler();
        using var http = new HttpClient(handler);
        var clientOptions = new BlobClientOptions
        {
            Transport = new HttpClientTransport(http),
            Retry = { MaxRetries = 0 }
        };
        var client = new BlobServiceClient(new Uri(Endpoint), new FixedTokenCredential(), clientOptions);
        var publisher = new BlobRunArtifactPublisher(client, TimeProvider.System);
        await publisher.PublishAsync(result,
            new RunPublicationOptions(Endpoint, Container, TimeSpan.FromSeconds(5), 2_000_000, 8_000_000),
            cancellationToken);

        Dictionary<string, byte[]> payloads = new(StringComparer.Ordinal);
        foreach (string name in ArtifactNames)
        {
            string key = name switch
            {
                "ledger.jsonl" => "ledger",
                "manifest.json" => "manifest",
                _ => name
            };
            payloads.Add(name, await File.ReadAllBytesAsync(paths[key], cancellationToken));
        }
        byte[] indexBytes = handler.StoredByName["index.json"];
        payloads.Add("index.json", indexBytes);

        await LedgerValidator.ReadValidatedAsync(ledgerPath, manifestPath, identity, inventory.Length,
            16 * 1024, 64 * 1024, 16 * 1024, cancellationToken);
        RunEvidenceReadResult readEvidence = await RunEvidenceReader.ReadAsync(directory, identity, expectations,
            64 * 1024, 16 * 1024, 32, cancellationToken);
        if (readEvidence.IsFaulted) throw new InvalidDataException("synthetic_evidence_readback_faulted");
        _ = LedgerValidator.DeserializeManifest(payloads["manifest.json"].AsSpan(0, payloads["manifest.json"].Length - 1));

        return new GoldenRun(identity, expectations, result, payloads, handler.Requests);
    }

    internal static RunIdentity Identity() => new("A01", "C0", "golden-run-001", "golden-instance-001",
        "golden-instance-001", "golden-loadgen-client");

    private static async Task<ResolvedRoleConfiguration> ResolveBaselineAsync(string clientId, CancellationToken cancellationToken)
    {
        string root = FindRepositoryRoot();
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "golden-synthetic-namespace";
            options.ClientId = clientId;
        });
        services.AddParchwellKafka(KafkaClientRole.VerifierConsumer, options =>
        {
            options.NamespaceName = "golden-synthetic-namespace";
            options.ClientId = clientId;
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        var loader = new ScenarioLoader(new FileSystemScenarioSource(root), Options.Create(new ScenarioLoadOptions
        {
            MaxDocumentCharacters = 256 * 1024,
            MaxNestingDepth = 32,
            MaxScalarCharacters = 8 * 1024,
            MaxParserEvents = 20_000
        }));
        LoadedScenario loaded = await loader.LoadAsync("A01", cancellationToken);
        ResolvedScenario resolved = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
        return resolved.Configurations["C0"].Roles[KafkaClientRole.LoadGenProducer];
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "scenarios", "A01", "scenario.yaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("synthetic_scenario_source_not_found");
    }

    private static ImmutableArray<AttemptEntry> RegisterInventory(RunIdentity identity)
    {
        var attemptInventory = new AttemptInventory(identity, maximumAttempts: 2);
        attemptInventory.Register(new AttemptEntry(identity.InstanceId, "golden-attempt-000", 0,
            "tenant-golden", "d0", "tenant-golden-d0", 0, "uploaded", 100, 110));
        attemptInventory.Register(new AttemptEntry(identity.InstanceId, "golden-attempt-001", 1,
            "tenant-golden", "d0", "tenant-golden-d0", 1, "ocr-started", 200, 210));
        return attemptInventory.Seal();
    }

    private static LedgerRow CallbackRow(RunIdentity identity, AttemptEntry attempt) => new(1, identity, attempt,
        "callback", true, 0, null, (int)PersistenceStatus.Persisted, "native_callback", 120, 0.25, null);

    private static LedgerRow SynchronousRejectionRow(RunIdentity identity, AttemptEntry attempt) => new(1, identity,
        attempt, "synchronous_rejection", false, (int)ErrorCode.Local_QueueFull, "Local_QueueFull", null,
        "pre_enqueue", 215, null, 0.125);

    private static RunManifest Manifest(RunIdentity identity, ImmutableArray<AttemptEntry> inventory) => new(1, identity,
        "1", inventory.Length, inventory.Length, inventory, 1_000_000, 100_000, "complete", true, null, [],
        ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal)
            .Add("confluent_kafka", "2.16.0")
            .Add("librdkafka", "2.16.0")
            .Add("dotnet", "10.0.13")
            .Add("workload_generator", "1"),
        ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal)
            .Add("statistics.jsonl", "statistics.jsonl")
            .Add("request-lifecycle.jsonl", "request-lifecycle.jsonl")
            .Add("capabilities.json", "capabilities.json"),
        ["broker_lifecycle_inventory_unproven", "request_lifecycle_capture_unsupported"]);

    private static KafkaStatisticsReport StatisticsReport(RunIdentity identity, string sessionId, long timestamp,
        long age, long throttleCount, long? throttleMaximum, long rttCount, long? rttP99)
    {
        string raw = $$"""
            {
              "name":"golden-client#producer",
              "client_id":"{{identity.ClientId}}",
              "type":"producer",
              "ts":{{timestamp}},
              "time":1792240496,
              "age":{{age}},
              "brokers":{
                "broker-a/1":{
                  "name":"broker-a/1","nodeid":1,"source":"learned","state":"UP","stateage":845055,
                  "req_timeouts":0,"disconnects":0,"txretries":0,"txerrs":0,"rxerrs":0,
                  "throttle":{"cnt":{{throttleCount}},"max":{{throttleMaximum ?? 0}}},
                  "rtt":{"cnt":{{rttCount}},"p99":{{rttP99 ?? 0}}}
                }
              }
            }
            """;
        var broker = new KafkaBrokerStatistics("broker-a/1", 1, "UP", 845055, 0, 0, 0, 0, 0,
            new KafkaWindowStatistics(throttleCount, throttleMaximum), new KafkaWindowStatistics(rttCount, rttP99));
        var snapshot = new KafkaStatisticsSnapshot("golden-client#producer", identity.ClientId, "producer", timestamp,
            1792240496, age, [broker]);
        return new KafkaStatisticsReport(new KafkaStatisticsIdentity(identity.ScenarioId, identity.RunId,
            KafkaClientRole.LoadGenProducer, identity.ClientId), sessionId, CapturedAt, raw, snapshot, []);
    }

    internal sealed record GoldenRun(RunIdentity Identity, RunEvidenceExpectations Expectations,
        RunExecutionResult Result, Dictionary<string, byte[]> Payloads, CapturedBlobRequest[] Requests);

    private sealed class FixedUtcTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FixedTokenCredential : TokenCredential
    {
        private static readonly AccessToken Token = new("synthetic-golden-token",
            DateTimeOffset.Parse("2037-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Token;
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Token);
        }
    }

    private sealed class RecordingBlobHandler : HttpMessageHandler
    {
        private readonly object gate = new();
        private readonly List<CapturedBlobRequest> requests = [];
        private readonly Dictionary<string, byte[]> stored = new(StringComparer.Ordinal);

        internal CapturedBlobRequest[] Requests { get { lock (gate) return requests.ToArray(); } }
        internal Dictionary<string, byte[]> StoredByName
        {
            get { lock (gate) return new Dictionary<string, byte[]>(stored, StringComparer.Ordinal); }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            byte[] body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Uri uri = request.RequestUri ?? throw new InvalidOperationException("recorded_request_uri_missing");
            string name = Uri.UnescapeDataString(uri.AbsolutePath.Split('/').Last());
            string? condition = request.Headers.TryGetValues("If-None-Match", out IEnumerable<string>? values)
                ? values.Single()
                : null;
            lock (gate)
            {
                requests.Add(new CapturedBlobRequest(request.Method, uri, name, body, condition));
                if (request.Method == HttpMethod.Put) stored[name] = body;
            }

            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.ETag = new EntityTagHeaderValue("\"synthetic-etag\"");
            response.Headers.TryAddWithoutValidation("x-ms-request-id", "synthetic-recording-transport");
            response.Headers.TryAddWithoutValidation("Last-Modified", "Sat, 17 Oct 2026 12:34:56 GMT");
            return response;
        }
    }

    internal sealed record CapturedBlobRequest(HttpMethod Method, Uri Uri, string Name, byte[] Body,
        string? IfNoneMatch);
}
