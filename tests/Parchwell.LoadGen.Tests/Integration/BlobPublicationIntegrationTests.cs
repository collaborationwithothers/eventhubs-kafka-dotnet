using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.LoadGen.Run.Workload;
using Parchwell.LoadGen.Tests.Component;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Integration;

#pragma warning disable CA1707 // Repository test naming uses Given_When_Then.
public sealed class BlobPublicationIntegrationTests
{
    private const string Endpoint = "https://recording.blob.test/";
    private const string Container = "ledger";
    private static readonly string[] ArtifactNames =
    ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
    private static readonly string[] EventTypes = ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];

    [Fact]
    public async Task Given_RealLocalKafkaRun_When_PublishingThroughBlobSdk_Then_IndependentReaderAcceptsExactIdentityAndArtifacts()
    {
        using var startup = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using var broker = await LocalKafkaFixture.StartAsync(startup.Token);
        string topic = "loadgen-publication-" + Guid.NewGuid().ToString("N");
        using (var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = broker.BootstrapServers }).Build())
        {
            await admin.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }],
                new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(20), OperationTimeout = TimeSpan.FromSeconds(20) })
                .WaitAsync(startup.Token);
        }

        string directory = NewDirectory();
        try
        {
            RunIdentity identity = Identity();
            WorkloadProfile profile = Profile();
            await using var evidence = new RunEvidenceSession(identity, EvidenceOptions(directory), TimeProvider.System);
            await using var ledger = NewLedger(directory);
            IProducer<byte[], byte[]> producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig
            {
                BootstrapServers = broker.BootstrapServers,
                ClientId = identity.ClientId,
                Acks = Acks.All,
                EnableIdempotence = false,
                MessageTimeoutMs = 10_000,
                SocketTimeoutMs = 5_000
            }).Build();

            RunExecutionResult result = await new RunExecutor(() => producer, ledger, TimeProvider.System, evidence: evidence)
                .ExecuteAsync(profile, identity, ExecutionOptions(directory, topic), startup.Token);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("complete", result.Manifest.WorkerStatus);
            Assert.True(result.Manifest.BookkeepingComplete);
            Assert.Contains("request_lifecycle_capture_unsupported", result.Manifest.GuardObservations);
            Assert.Contains("broker_lifecycle_inventory_unproven", result.Manifest.GuardObservations);
            ReadOneIndependentMessage(topic, broker.BootstrapServers, result.Manifest.Inventory[0], startup.Token);

            using var transport = new RecordingHttpHandler();
            using var http = new HttpClient(transport);
            var publisher = NewPublisher(http);
            await publisher.PublishAsync(result, PublicationOptions(), startup.Token);

            Assert.True(IndependentAccept(transport.StoredByName, identity, workerExitCode: 0));
            Assert.False(IndependentAccept(transport.StoredByName, identity, workerExitCode: 1));
            Assert.Equal(6, transport.Requests.Length);
            Assert.Equal(ArtifactNames.Append("index.json"), transport.Requests.Select(request => request.Name));

            foreach (string name in ArtifactNames.Append("index.json"))
            {
                CapturedRequest request = Assert.Single(transport.Requests, item => item.Name == name);
                Assert.Equal(HttpMethod.Put, request.Method);
                Assert.Equal("*", request.IfNoneMatch);
                Assert.EndsWith("/scenario%2FA01/config%2FC0/run-publication/instance-one/" + name,
                    Uri.UnescapeDataString(request.Uri.AbsolutePath), StringComparison.OrdinalIgnoreCase);
                if (name != "index.json")
                    Assert.Equal(await File.ReadAllBytesAsync(result.ArtifactPaths[name == "ledger.jsonl" ? "ledger" :
                        name == "manifest.json" ? "manifest" : name], TestContext.Current.CancellationToken), request.Body);
            }

            VerifyIndexAndArtifactsIndependently(transport.StoredByName, identity);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("ledger.jsonl")]
    [InlineData("manifest.json")]
    [InlineData("statistics.jsonl")]
    [InlineData("request-lifecycle.jsonl")]
    [InlineData("capabilities.json")]
    public async Task Given_RequiredArtifactUploadFails_When_Publishing_Then_NoIndexCanAcceptACompleteRun(string failedName)
    {
        string directory = NewDirectory();
        try
        {
            RunExecutionResult result = await CreateSyntheticCompletedRunAsync(directory);
            using var handler = new RecordingHttpHandler(failName: failedName);
            using var http = new HttpClient(handler);

            await Assert.ThrowsAsync<RequestFailedException>(() => NewPublisher(http).PublishAsync(result, PublicationOptions(),
                TestContext.Current.CancellationToken));

            Assert.DoesNotContain(handler.Requests, request => request.Name == "index.json");
            Assert.False(IndependentAccept(handler.StoredByName, Identity(), workerExitCode: 1));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_IndexUploadHasAmbiguousSdkFailureAfterTransportRecordsBytes_When_Publishing_Then_WorkerSuccessGateRemainsClosed()
    {
        string directory = NewDirectory();
        try
        {
            RunExecutionResult result = await CreateSyntheticCompletedRunAsync(directory);
            using var handler = new RecordingHttpHandler(ambiguousName: "index.json");
            using var http = new HttpClient(handler);

            await Assert.ThrowsAnyAsync<Exception>(() => NewPublisher(http).PublishAsync(result, PublicationOptions(),
                TestContext.Current.CancellationToken));

            Assert.Contains(handler.Requests, request => request.Name == "index.json");
            Assert.True(handler.StoredByName.ContainsKey("index.json"));
            Assert.True(IndependentAccept(handler.StoredByName, result.Manifest.Identity, workerExitCode: 0));
            Assert.False(IndependentAccept(handler.StoredByName, result.Manifest.Identity, workerExitCode: 1));
            Assert.Equal(6, handler.Requests.Length);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_CallerCancelsAnActiveBlobRequest_When_Publishing_Then_PropagatesCancellationWithoutAcceptingRun()
    {
        string directory = NewDirectory();
        try
        {
            RunExecutionResult result = await CreateSyntheticCompletedRunAsync(directory);
            using var handler = new RecordingHttpHandler(blockName: "ledger.jsonl");
            using var http = new HttpClient(handler);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token, TestContext.Current.CancellationToken);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                NewPublisher(http).PublishAsync(result, PublicationOptions(TimeSpan.FromSeconds(5)), linked.Token));

            Assert.DoesNotContain(handler.Requests, request => request.Name == "index.json");
            Assert.False(IndependentAccept(handler.StoredByName, result.Manifest.Identity, workerExitCode: 1));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_UploadDeadlineExpires_When_Publishing_Then_ThrowsTimeoutAndDoesNotPublishIndex()
    {
        string directory = NewDirectory();
        try
        {
            RunExecutionResult result = await CreateSyntheticCompletedRunAsync(directory);
            using var handler = new RecordingHttpHandler(blockName: "ledger.jsonl");
            using var http = new HttpClient(handler);

            TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(() =>
                NewPublisher(http).PublishAsync(result, PublicationOptions(TimeSpan.FromMilliseconds(100)),
                    TestContext.Current.CancellationToken));

            Assert.Equal("artifact_publication_timeout", exception.Message);
            Assert.DoesNotContain(handler.Requests, request => request.Name == "index.json");
            Assert.False(IndependentAccept(handler.StoredByName, result.Manifest.Identity, workerExitCode: 1));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_IndexConflictsAfterAllPayloads_When_Publishing_Then_ConflictDoesNotBecomeWorkerSuccess()
    {
        string directory = NewDirectory();
        try
        {
            RunExecutionResult result = await CreateSyntheticCompletedRunAsync(directory);
            using var handler = new RecordingHttpHandler(failName: "index.json");
            using var http = new HttpClient(handler);

            await Assert.ThrowsAsync<RequestFailedException>(() => NewPublisher(http).PublishAsync(result, PublicationOptions(),
                TestContext.Current.CancellationToken));

            Assert.Equal(6, handler.Requests.Length);
            Assert.All(handler.Requests, request => Assert.Equal("*", request.IfNoneMatch));
            Assert.False(IndependentAccept(handler.StoredByName, result.Manifest.Identity, workerExitCode: 1));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_CompleteIndex_When_RequiredSetOrPayloadIsDamaged_Then_IndependentReaderRejectsIt()
    {
        string directory = NewDirectory();
        try
        {
            RunExecutionResult result = await CreateSyntheticCompletedRunAsync(directory);
            using var handler = new RecordingHttpHandler();
            using var http = new HttpClient(handler);
            await NewPublisher(http).PublishAsync(result, PublicationOptions(), TestContext.Current.CancellationToken);
            Assert.True(IndependentAccept(handler.StoredByName, result.Manifest.Identity, workerExitCode: 0));

            var missing = new Dictionary<string, byte[]>(handler.StoredByName, StringComparer.Ordinal);
            missing.Remove("statistics.jsonl");
            Assert.False(IndependentAccept(missing, result.Manifest.Identity, workerExitCode: 0));

            var corrupt = new Dictionary<string, byte[]>(handler.StoredByName, StringComparer.Ordinal);
            corrupt["manifest.json"] = [.. corrupt["manifest.json"], (byte)'x'];
            Assert.False(IndependentAccept(corrupt, result.Manifest.Identity, workerExitCode: 0));

            var truncated = new Dictionary<string, byte[]>(handler.StoredByName, StringComparer.Ordinal);
            truncated["index.json"] = truncated["index.json"][..^1];
            Assert.False(IndependentAccept(truncated, result.Manifest.Identity, workerExitCode: 0));

            var wrongIdentity = new Dictionary<string, byte[]>(handler.StoredByName, StringComparer.Ordinal);
            wrongIdentity["index.json"] = ReplaceJsonString(wrongIdentity["index.json"], "run_id", "other-run");
            Assert.False(IndependentAccept(wrongIdentity, result.Manifest.Identity, workerExitCode: 0));

            var duplicateIndex = new Dictionary<string, byte[]>(handler.StoredByName, StringComparer.Ordinal);
            duplicateIndex["index.json"] = DuplicateFirstArtifactProperty(duplicateIndex["index.json"]);
            Assert.False(IndependentAccept(duplicateIndex, result.Manifest.Identity, workerExitCode: 0));

            var duplicateArtifact = new Dictionary<string, byte[]>(handler.StoredByName, StringComparer.Ordinal);
            duplicateArtifact["index.json"] = DuplicateArtifactEntry(duplicateArtifact["index.json"]);
            Assert.False(IndependentAccept(duplicateArtifact, result.Manifest.Identity, workerExitCode: 0));

            var partial = new Dictionary<string, byte[]>(handler.StoredByName, StringComparer.Ordinal);
            partial.Remove("capabilities.json");
            Assert.False(IndependentAccept(partial, result.Manifest.Identity, workerExitCode: 0));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void ReadOneIndependentMessage(string topic, string bootstrapServers, AttemptEntry attempt,
        CancellationToken cancellationToken)
    {
        using var consumer = new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "loadgen-publication-observer-" + Guid.NewGuid().ToString("N"),
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = false
        }).Build();
        consumer.Assign(new TopicPartitionOffset(new TopicPartition(topic, new Partition(0)), Offset.Beginning));
        ConsumeResult<byte[], byte[]>? consumed = consumer.Consume(TimeSpan.FromSeconds(15));
        Assert.NotNull(consumed);
        Assert.Equal(attempt.Key, Encoding.UTF8.GetString(consumed.Message.Key));
        Assert.Equal(attempt.InstanceId, ReadHeader(consumed.Message.Headers, "instance-id"));
        Assert.Equal(attempt.AttemptId, ReadHeader(consumed.Message.Headers, "attempt-id"));
        Assert.Equal(attempt.TenantId, ReadHeader(consumed.Message.Headers, "tenant-id"));
        Assert.Equal(attempt.DocumentId, ReadHeader(consumed.Message.Headers, "document-id"));
        Assert.Equal(attempt.Sequence.ToString(CultureInfo.InvariantCulture), ReadHeader(consumed.Message.Headers, "sequence"));
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string ReadHeader(Headers headers, string name)
        => Encoding.UTF8.GetString(headers.Single(header => header.Key == name).GetValueBytes());

    private static async Task<RunExecutionResult> CreateSyntheticCompletedRunAsync(string directory)
    {
        RunIdentity identity = Identity();
        await using var ledger = NewLedger(directory);
        await using var evidence = new RunEvidenceSession(identity, EvidenceOptions(directory), TimeProvider.System);
        var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, SuccessfulProducer>();
        return await new RunExecutor(() => producer, ledger, TimeProvider.System, evidence: evidence)
            .ExecuteAsync(Profile(), identity, ExecutionOptions(directory, "synthetic-publication-topic"),
                TestContext.Current.CancellationToken);
    }

    private static BlobRunArtifactPublisher NewPublisher(HttpClient http)
    {
        var clientOptions = new BlobClientOptions
        {
            Transport = new HttpClientTransport(http),
            Retry = { MaxRetries = 0 }
        };
        var client = new BlobServiceClient(new Uri(Endpoint), new FixedTokenCredential(), clientOptions);
        return new BlobRunArtifactPublisher(client, TimeProvider.System);
    }

    private static RunPublicationOptions PublicationOptions(TimeSpan? timeout = null)
        => new(Endpoint, Container, timeout ?? TimeSpan.FromSeconds(5), 2_000_000, 8_000_000);

    private static bool IndependentAccept(Dictionary<string, byte[]> blobs, RunIdentity expectedIdentity,
        int workerExitCode)
    {
        if (workerExitCode != 0 || !blobs.TryGetValue("index.json", out byte[]? indexBytes)) return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(indexBytes);
            JsonElement root = document.RootElement;
            if (!HasExactNames(root, "schema_version", "identity", "worker_status", "bookkeeping_complete",
                    "worker_complete", "publication_complete", "artifacts")) return false;
            if (root.GetProperty("schema_version").GetInt32() != 1 ||
                root.GetProperty("worker_status").GetString() != "complete" ||
                !root.GetProperty("bookkeeping_complete").GetBoolean() ||
                !root.GetProperty("worker_complete").GetBoolean() ||
                !root.GetProperty("publication_complete").GetBoolean()) return false;

            JsonElement identity = root.GetProperty("identity");
            if (!HasExactNames(identity, "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id") ||
                identity.GetProperty("scenario_id").GetString() != expectedIdentity.ScenarioId ||
                identity.GetProperty("configuration_id").GetString() != expectedIdentity.ConfigurationId ||
                identity.GetProperty("run_id").GetString() != expectedIdentity.RunId ||
                identity.GetProperty("instance_id").GetString() != expectedIdentity.InstanceId ||
                identity.GetProperty("expected_instance_id").GetString() != expectedIdentity.ExpectedInstanceId ||
                identity.GetProperty("client_id").GetString() != expectedIdentity.ClientId) return false;

            JsonElement artifacts = root.GetProperty("artifacts");
            if (artifacts.ValueKind != JsonValueKind.Array || artifacts.GetArrayLength() != ArtifactNames.Length) return false;
            for (int i = 0; i < ArtifactNames.Length; i++)
            {
                JsonElement item = artifacts[i];
                if (!HasExactNames(item, "name", "length", "sha256")) return false;
                string name = item.GetProperty("name").GetString()!;
                if (!StringComparer.Ordinal.Equals(name, ArtifactNames[i]) || !blobs.TryGetValue(name, out byte[]? bytes)) return false;
                if (item.GetProperty("length").GetInt64() != bytes.LongLength) return false;
                string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!StringComparer.Ordinal.Equals(item.GetProperty("sha256").GetString(), hash)) return false;
            }
            return ArtifactNames.All(blobs.ContainsKey) && blobs.Keys.All(name => ArtifactNames.Contains(name, StringComparer.Ordinal) || name == "index.json");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return false;
        }
    }

    private static bool HasExactNames(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        var expected = names.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
            if (!expected.Contains(property.Name) || !seen.Add(property.Name)) return false;
        return seen.SetEquals(expected);
    }

    private static void VerifyIndexAndArtifactsIndependently(Dictionary<string, byte[]> blobs, RunIdentity identity)
    {
        Assert.True(IndependentAccept(blobs, identity, workerExitCode: 0));
        using JsonDocument document = JsonDocument.Parse(blobs["index.json"]);
        JsonElement root = document.RootElement;
        Assert.Equal("complete", root.GetProperty("worker_status").GetString());
        Assert.True(root.GetProperty("bookkeeping_complete").GetBoolean());
        Assert.True(root.GetProperty("worker_complete").GetBoolean());
        Assert.True(root.GetProperty("publication_complete").GetBoolean());
        Assert.Equal(ArtifactNames, root.GetProperty("artifacts").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()));
    }

    private static byte[] ReplaceJsonString(byte[] source, string property, string value)
    {
        using JsonDocument document = JsonDocument.Parse(source);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            foreach (JsonProperty item in document.RootElement.EnumerateObject())
            {
                if (item.NameEquals("identity"))
                {
                    writer.WritePropertyName("identity");
                    writer.WriteStartObject();
                    foreach (JsonProperty identityProperty in item.Value.EnumerateObject())
                    {
                        writer.WritePropertyName(identityProperty.Name);
                        if (identityProperty.NameEquals(property)) writer.WriteStringValue(value);
                        else identityProperty.Value.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WritePropertyName(item.Name);
                    item.Value.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    private static byte[] DuplicateFirstArtifactProperty(byte[] source)
    {
        string text = Encoding.UTF8.GetString(source);
        const string marker = "\"schema_version\":";
        return Encoding.UTF8.GetBytes(text.Replace(marker, "\"schema_version\":1,\"schema_version\":", StringComparison.Ordinal));
    }

    private static byte[] DuplicateArtifactEntry(byte[] source)
    {
        using JsonDocument document = JsonDocument.Parse(source);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (property.NameEquals("artifacts"))
                {
                    writer.WriteStartArray();
                    property.Value[0].WriteTo(writer);
                    property.Value[0].WriteTo(writer);
                    for (int index = 2; index < property.Value.GetArrayLength(); index++)
                        property.Value[index].WriteTo(writer);
                    writer.WriteEndArray();
                }
                else property.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    private static RunIdentity Identity() => new("scenario/A01", "config/C0", "run-publication", "instance-one", "instance-one", "client-publication");

    private static string NewDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "loadgen-publication-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static LocalLedgerStore NewLedger(string directory) => new(new LedgerStoreOptions(
        Path.Combine(directory, "ledger.jsonl"), Path.Combine(directory, "manifest.json"), 16, 8192,
        2_000_000, 2_000_000, TimeSpan.FromSeconds(3)));

    private static RunEvidenceOptions EvidenceOptions(string directory) => new(directory, 16, 8192,
        2_000_000, 64, TimeSpan.FromSeconds(3));

    private static RunExecutionOptions ExecutionOptions(string directory, string topic) => new(8, 1024,
        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), 8192,
        2_000_000, 2_000_000, directory, topic, [], false);

    private static WorkloadProfile Profile()
    {
        var payloads = EventTypes
            .ToImmutableDictionary(name => name, _ => ImmutableArray.Create(new PayloadBucket(32, 1)), StringComparer.Ordinal);
        return new WorkloadProfile(1, "publication-profile", 23, [new TenantWeight("tenant-publication", 1)], 1,
            payloads, [new LoadStage("one", 1, 1)], new SourceLocation("synthetic-publication-profile"));
    }

    private sealed class FixedTokenCredential : TokenCredential
    {
        private static readonly AccessToken Token = new("integration-fake-token", DateTimeOffset.Parse("2037-01-01T00:00:00Z", CultureInfo.InvariantCulture));
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Token;
        }
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Token);
        }
    }

    private sealed class RecordingHttpHandler(string? failName = null, string? ambiguousName = null, string? blockName = null)
        : HttpMessageHandler
    {
        private readonly object gate = new();
        private readonly List<CapturedRequest> requests = [];
        private readonly Dictionary<string, byte[]> stored = new(StringComparer.Ordinal);
        public CapturedRequest[] Requests { get { lock (gate) return requests.ToArray(); } }
        public Dictionary<string, byte[]> StoredByName { get { lock (gate) return new Dictionary<string, byte[]>(stored, StringComparer.Ordinal); } }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[] body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            Uri uri = request.RequestUri ?? throw new InvalidOperationException("recorded_request_uri_missing");
            string name = Uri.UnescapeDataString(uri.AbsolutePath.Split('/').Last());
            string? condition = request.Headers.TryGetValues("If-None-Match", out IEnumerable<string>? values) ? values.Single() : null;
            lock (gate)
            {
                requests.Add(new CapturedRequest(request.Method, uri, name, body, condition));
                if (request.Method == HttpMethod.Put && (failName == name || ambiguousName == name || ambiguousName == "index.json" && name == "index.json"))
                {
                    if (ambiguousName == name) stored[name] = body;
                    else if (failName != name) stored[name] = body;
                }
                else if (request.Method == HttpMethod.Put)
                {
                    stored[name] = body;
                }
            }

            if (blockName == name)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            if (ambiguousName == name) throw new IOException("synthetic_ambiguous_transport_failure_after_request_body");
            if (failName == name)
            {
                var failure = new HttpResponseMessage(name == "index.json" ? HttpStatusCode.Conflict : HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("<Error><Code>Conflict</Code><Message>synthetic fault</Message></Error>", Encoding.UTF8, "application/xml")
                };
                failure.Headers.TryAddWithoutValidation("x-ms-request-id", "integration-fault");
                return failure;
            }

            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.ETag = new EntityTagHeaderValue("\"integration-etag\"");
            response.Headers.TryAddWithoutValidation("x-ms-request-id", "integration-success");
            response.Headers.TryAddWithoutValidation("Last-Modified", "Tue, 01 Jan 2030 00:00:00 GMT");
            return response;
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Name, byte[] Body, string? IfNoneMatch);

    public class SuccessfulProducer : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                var callback = (Action<DeliveryReport<byte[], byte[]>>)args[2]!;
                callback(new DeliveryReport<byte[], byte[]> { Error = new Error(ErrorCode.NoError), Status = PersistenceStatus.Persisted });
                return null;
            }
            if (targetMethod?.Name == "Flush") return 0;
            if (targetMethod?.Name == "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
#pragma warning restore CA1707
