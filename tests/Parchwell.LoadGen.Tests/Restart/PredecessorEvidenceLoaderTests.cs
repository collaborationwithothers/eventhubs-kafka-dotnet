using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.LoadGen.Run.Restart;
using Parchwell.LoadGen.Tests.Publication;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Restart;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class PredecessorEvidenceLoaderTests
{
    private const string Instance = "run-1/C0/r1/producer/pod-a/";
    private static readonly JsonSerializerOptions SnakeCase = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static readonly string[] PayloadNames = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
    private static readonly ProducerRunContext Successor = new(RestartFixture.Second, "C0", 1, "run-1-c0-r1-loadgen2", "pod-b", "uid-b", new string('a', 40));
    private static readonly ProducerRunContext FirstJob = new(RestartFixture.First, "C0", 1, "run-1-c0-r1-loadgen", "pod-a", "uid-a", new string('a', 40));

    [Fact]
    public async Task Given_OneCompletePredecessor_When_Loading_Then_ReturnsItsManifestRowsAndResendList()
    {
        Dictionary<string, byte[]> objects = await PublishedAsync();

        RunArtifactSet set = await LoadAsync(new Source(objects), Successor);

        Assert.Equal([.. objects[Instance + "publication/manifest.json"].SkipLast(1)], LedgerValidator.SerializeManifest(set.Manifest));
        Assert.Equal(RestartFixture.Predecessor().Rows, set.Rows);
        Assert.Equal(RestartFixture.Plan().Planned, RestartInstance.Create(RestartFixture.Second, set.Manifest, set.Rows).Plan.Planned);
    }

    [Theory]
    [InlineData("none", "predecessor_instance_count_invalid")]
    [InlineData("run-1/C0/r1/producer/pod-c/registration.json", "predecessor_instance_count_invalid")]
    [InlineData("run-1/C0/r1/producer/pod-b/registration.json", "predecessor_successor_already_registered")]
    [InlineData("run-1/C0/r1/producer/stray.json", "predecessor_listing_invalid")]
    [InlineData(Instance + "diagnostics/ledger.jsonl", "predecessor_member_set_invalid")]
    [InlineData("no-completion", "predecessor_member_set_invalid")]
    [InlineData("first-job", "predecessor_requires_restart_instance")]
    public async Task Given_ListingThatIsNotOneCompletePredecessor_When_Loading_Then_RejectsBeforeReadingAnObject(string change, string error)
    {
        Dictionary<string, byte[]> objects = await PublishedAsync();
        if (change == "none") objects.Clear();
        else if (change == "no-completion") objects.Remove(Instance + "completion.json");
        else if (change != "first-job") objects[change] = [1];
        var source = new Source(objects);

        Assert.Equal(error, (await Assert.ThrowsAsync<InvalidDataException>(() =>
            LoadAsync(source, change == "first-job" ? FirstJob : Successor))).Message);
        Assert.Empty(source.Reads);
    }

    [Fact]
    public async Task Given_PredecessorDirectoryThatIsNotAnEscapedPodName_When_Loading_Then_RejectsBeforeReadingAnObject()
    {
        Dictionary<string, byte[]> objects = (await PublishedAsync()).ToDictionary(
            entry => "run-1/C0/r1/producer/pod a/" + entry.Key[Instance.Length..], entry => entry.Value);
        var source = new Source(objects);

        Assert.Equal("predecessor_listing_invalid", (await Assert.ThrowsAsync<InvalidDataException>(() => LoadAsync(source, Successor))).Message);
        Assert.Empty(source.Reads);
    }

    [Theory]
    [InlineData("registration.json", "job_name", "run-1-c0-r1-loadgen2", "predecessor_registration_invalid")]
    [InlineData("registration.json", "source_commit", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "predecessor_registration_invalid")]
    [InlineData("registration.json", "pod_name", "pod-z", "predecessor_registration_invalid")]
    [InlineData("registration.json", "predecessor_instance_id", "pod-z", "predecessor_registration_invalid")]
    [InlineData("completion.json", "pod_uid", "uid-other", "predecessor_completion_invalid")]
    [InlineData("completion.json", "mode", "single-action", "predecessor_completion_invalid")]
    [InlineData("publication/manifest.json", "", "", "publication_artifact_content_mismatch:manifest.json")]
    public async Task Given_PredecessorObjectThatFailsItsCheck_When_Loading_Then_Rejects(string name, string field, string value, string error)
    {
        Dictionary<string, byte[]> objects = await PublishedAsync();
        if (field.Length == 0) objects[Instance + name][^2] = (byte)' ';
        else
        {
            JsonNode record = JsonNode.Parse(objects[Instance + name])!;
            record[field] = value;
            objects[Instance + name] = JsonSerializer.SerializeToUtf8Bytes(record);
        }

        Assert.Equal(error, (await Assert.ThrowsAsync<InvalidDataException>(() => LoadAsync(new Source(objects), Successor))).Message);
    }

    private static Task<RunArtifactSet> LoadAsync(Source source, ProducerRunContext successor) => PredecessorEvidenceLoader.LoadAsync(
        source, successor, RunArtifactIndexReaderTests.Expectations(RestartFixture.First), 256 * 1024, 1024 * 1024, TestContext.Current.CancellationToken);

    // The first instance's eight objects, written by the production serializers and evidence session.
    private static async Task<Dictionary<string, byte[]>> PublishedAsync()
    {
        RunIdentity identity = RestartFixture.First;
        (RunManifest predecessor, ImmutableArray<LedgerRow> rows) = RestartFixture.Predecessor();
        RunManifest manifest = predecessor with
        {
            EvidenceFiles = PayloadNames.Skip(2).ToImmutableDictionary(name => name, name => name, StringComparer.Ordinal),
            GuardObservations = ["async_delivery_failure_observed", "possibly_persisted_observed", "request_lifecycle_capture_unsupported", "broker_lifecycle_inventory_unproven"]
        };
        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["ledger.jsonl"] = [.. rows.SelectMany(row => (byte[])[.. LedgerValidator.SerializeRow(row), (byte)'\n'])],
            ["manifest.json"] = [.. LedgerValidator.SerializeManifest(manifest), (byte)'\n']
        };
        string directory = Path.Combine(Path.GetTempPath(), "predecessor-loader-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var session = new RunEvidenceSession(identity, new RunEvidenceOptions(directory, 8, 64 * 1024, 256 * 1024, 16,
                TimeSpan.FromSeconds(2)), TimeProvider.System, RunArtifactIndexReaderTests.Expectations(RestartFixture.First)))
            {
                var snapshot = new KafkaStatisticsSnapshot("native-producer", identity.ClientId, "producer", 1, 1, 1, [new KafkaBrokerStatistics(
                    "broker-1", 1, "UP", 1, 0, 0, 0, 0, 0, new KafkaWindowStatistics(0, null), new KafkaWindowStatistics(0, null))]);
                await session.WriteAsync(new KafkaStatisticsReport(new KafkaStatisticsIdentity(identity.ScenarioId, identity.RunId,
                    KafkaClientRole.LoadGenProducer, identity.ClientId), "loader-session", DateTimeOffset.UnixEpoch, "{}", snapshot, []),
                    TestContext.Current.CancellationToken);
                await session.CompleteAsync(new KafkaStatisticsCompletion(true, [], []), TestContext.Current.CancellationToken);
            }
            foreach (string name in PayloadNames.Skip(2))
                payloads[name] = await File.ReadAllBytesAsync(Path.Combine(directory, name), TestContext.Current.CancellationToken);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        var objects = payloads.ToDictionary(pair => Instance + "publication/" + pair.Key, pair => pair.Value, StringComparer.Ordinal);
        objects[Instance + "registration.json"] = BlobProducerRunLifecycle.SerializeRegistration(FirstJob);
        objects[Instance + "completion.json"] = BlobProducerRunLifecycle.SerializeCompletion(FirstJob);
        objects[Instance + "publication/index.json"] = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 1, identity, worker_status = "complete", bookkeeping_complete = true, worker_complete = true, publication_complete = true,
            artifacts = PayloadNames.Select(name => new
            {
                name, length = payloads[name].LongLength, sha256 = Convert.ToHexString(SHA256.HashData(payloads[name])).ToLowerInvariant()
            }).ToArray()
        }, SnakeCase);
        return objects;
    }

    private sealed class Source(Dictionary<string, byte[]> objects) : IPredecessorEvidenceSource
    {
        public List<string> Reads { get; } = [];

        public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([.. objects.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal)]);

        public Task<byte[]> ReadAsync(string name, long maximumBytes, CancellationToken cancellationToken)
        {
            Reads.Add(name);
            return Task.FromResult(objects[name].LongLength <= maximumBytes ? objects[name].ToArray() : throw new InvalidDataException("too_large"));
        }
    }
}
