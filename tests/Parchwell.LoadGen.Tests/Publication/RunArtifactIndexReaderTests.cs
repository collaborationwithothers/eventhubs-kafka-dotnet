using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Immutable;
using Confluent.Kafka;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Publication;
using Xunit;

namespace Parchwell.LoadGen.Tests.Publication;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class RunArtifactIndexReaderTests
{
    private static readonly string[] ArtifactNames =
    ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
    private const int MaximumIndexBytes = 64 * 1024;
    private const int MaximumArtifactBytes = 256 * 1024;
    private const int MaximumAggregateBytes = 1024 * 1024;

    [Fact]
    public async Task Given_CompleteIndexAndExactPayloads_When_ReadingAndValidating_Then_AcceptsTheIndexedSet()
    {
        // The index is the reader acceptance marker; all five payloads must match the bytes it names.
        RunIdentity identity = Identity();
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity);
        byte[] bytes = CreateIndex(identity, payloads);
        RunArtifactIndex index = await ReadAsync(bytes, identity, MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes);

        Assert.Equal(1, index.SchemaVersion);
        Assert.Equal(bytes.Length, index.IndexLength);
        Assert.Equal(ArtifactNames, index.Artifacts.Select(item => item.Name));
        await RunArtifactIndexReader.ValidateArtifactSetAsync(index,
            ObservedNames(),
            (name, _) => ValueTask.FromResult<Stream>(new MemoryStream(payloads[name], writable: false)),
            MaximumArtifactBytes, MaximumAggregateBytes, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_IdentityMismatch_When_ReadingIndex_Then_RejectsItBeforeAcceptingAnyFiles()
    {
        // A valid artifact set from another run must never be attributed to the expected run.
        RunIdentity identity = Identity();
        byte[] bytes = CreateIndex(identity, await PayloadsAsync(identity));
        RunIdentity otherIdentity = identity with { RunId = "other-run" };

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(bytes, otherIdentity, MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes));
    }

    [Fact]
    public async Task Given_DuplicateOrIncompleteIndexFields_When_Reading_Then_RejectsTheIndex()
    {
        // Duplicate JSON properties and incomplete status flags can make readers disagree about publication state.
        RunIdentity identity = Identity();
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity);
        string valid = Encoding.UTF8.GetString(CreateIndex(identity, payloads));
        string duplicate = valid.Replace("{\"schema_version\":1,", "{\"schema_version\":1,\"schema_version\":1,", StringComparison.Ordinal);
        string incomplete = valid.Replace("\"publication_complete\":true", "\"publication_complete\":false", StringComparison.Ordinal);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(Encoding.UTF8.GetBytes(duplicate), Identity(), MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(Encoding.UTF8.GetBytes(incomplete), Identity(), MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes));
    }

    [Fact]
    public async Task Given_MissingDuplicateOrReorderedArtifactNames_When_Reading_Then_RejectsTheSet()
    {
        // Exact file membership prevents a partial upload from being treated as a complete run.
        RunIdentity identity = Identity();
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity);
        byte[] valid = CreateIndex(identity, payloads);
        using JsonDocument parsed = JsonDocument.Parse(valid);
        JsonElement root = parsed.RootElement;
        var descriptors = root.GetProperty("artifacts").EnumerateArray().Select(element => JsonSerializer.Deserialize<Descriptor>(element.GetRawText())!).ToArray();
        byte[] missing = SerializeIndex(identity, descriptors[..^1]);
        descriptors[1] = descriptors[0];
        byte[] duplicate = SerializeIndex(identity, descriptors);
        descriptors = root.GetProperty("artifacts").EnumerateArray().Select(element => JsonSerializer.Deserialize<Descriptor>(element.GetRawText())!).ToArray();
        (descriptors[0], descriptors[1]) = (descriptors[1], descriptors[0]);
        byte[] reordered = SerializeIndex(identity, descriptors);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(missing, Identity(), MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(duplicate, Identity(), MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(reordered, Identity(), MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes));
    }

    [Fact]
    public async Task Given_CorruptPayloadOrAggregateBudgetExceeded_When_Validating_Then_RejectsThePublication()
    {
        // The index hash and budgets protect the exact bytes readers later download.
        RunIdentity identity = Identity();
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity);
        byte[] indexBytes = CreateIndex(identity, payloads);
        RunArtifactIndex index = await ReadAsync(indexBytes, Identity(), MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes);
        payloads["ledger.jsonl"] = Encoding.UTF8.GetBytes("different-ledger");

        await Assert.ThrowsAsync<InvalidDataException>(() => RunArtifactIndexReader.ValidateArtifactSetAsync(index,
            ObservedNames(),
            (name, _) => ValueTask.FromResult<Stream>(new MemoryStream(payloads[name], writable: false)),
            MaximumArtifactBytes, MaximumAggregateBytes, TestContext.Current.CancellationToken));
        InvalidDataException aggregateFailure = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ReadAsync(indexBytes, Identity(), MaximumIndexBytes, MaximumArtifactBytes, index.IndexLength));
        Assert.Equal("publication_aggregate_byte_limit_exceeded", aggregateFailure.Message);
    }

    [Fact]
    public async Task Given_ExtraStaleDuplicateMissingOrWrongPrefixMembers_When_Validating_Then_RejectsBeforeOpeningPayloads()
    {
        // A valid index cannot hide stale objects or an incomplete Blob listing beneath the run prefix.
        RunIdentity identity = Identity();
        byte[] indexBytes = CreateIndex(identity, await PayloadsAsync(identity));
        RunArtifactIndex index = await ReadAsync(indexBytes, identity, MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes);
        string[] exact = ObservedNames();
        string[][] invalidListings =
        [
            [.. exact, "stale.json"],
            [.. exact, "index.json"],
            exact[..^1],
            [.. exact[..^1], "unexpected.json"]
        ];

        foreach (string[] listing in invalidListings)
        {
            int opened = 0;
            await Assert.ThrowsAsync<InvalidDataException>(() => RunArtifactIndexReader.ValidateArtifactSetAsync(index,
                listing,
                (name, _) =>
                {
                    Interlocked.Increment(ref opened);
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                }, MaximumArtifactBytes, MaximumAggregateBytes, TestContext.Current.CancellationToken));
            Assert.Equal(0, opened);
        }
    }

    [Fact]
    public async Task Given_RehashedMalformedLedgerOrEvidenceArtifacts_When_Validating_Then_RejectsTheArtifactSet()
    {
        RunIdentity identity = Identity();
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity);
        LedgerRow row = LedgerValidator.DeserializeRow(payloads["ledger.jsonl"].AsSpan(0, payloads["ledger.jsonl"].Length - 1));
        byte[] inventoryMismatch = [.. LedgerValidator.SerializeRow(row with
        {
            Attempt = row.Attempt with { AttemptId = "attempt-other" }
        }), (byte)'\n'];
        string statistics = Encoding.UTF8.GetString(payloads["statistics.jsonl"]);
        Assert.Contains("run-index", statistics, StringComparison.Ordinal);
        byte[] wrongStatisticsIdentity = Encoding.UTF8.GetBytes(statistics.Replace("run-index", "other-run", StringComparison.Ordinal));

        await AssertRehashedArtifactRejectedAsync(identity, payloads, "ledger.jsonl", Encoding.UTF8.GetBytes("ledger.jsonl\n"));
        await AssertRehashedArtifactRejectedAsync(identity, payloads, "ledger.jsonl", []);
        await AssertRehashedArtifactRejectedAsync(identity, payloads, "ledger.jsonl", [.. payloads["ledger.jsonl"], .. payloads["ledger.jsonl"]]);
        await AssertRehashedArtifactRejectedAsync(identity, payloads, "ledger.jsonl", inventoryMismatch);
        await AssertRehashedArtifactRejectedAsync(identity, payloads, "statistics.jsonl", wrongStatisticsIdentity);
        await AssertRehashedArtifactRejectedAsync(identity, payloads, "request-lifecycle.jsonl", Encoding.UTF8.GetBytes("{}\n"));
        await AssertRehashedArtifactRejectedAsync(identity, payloads, "capabilities.json", Encoding.UTF8.GetBytes("{}"));
    }

    private static async Task<RunArtifactIndex> ReadAsync(byte[] bytes, RunIdentity identity, long maxIndex, long maxArtifact, long maxAggregate)
        => await RunArtifactIndexReader.ReadAsync(new MemoryStream(bytes, writable: false), identity, maxIndex,
            maxArtifact, maxAggregate, TestContext.Current.CancellationToken);

    private static async Task<Dictionary<string, byte[]>> PayloadsAsync(RunIdentity identity)
    {
        var entry = new AttemptEntry(identity.InstanceId, "attempt-0", 0, "tenant", "d0", "tenant-d0", 0,
            "uploaded", 0, 0);
        var manifest = new RunManifest(1, identity, "1", 1, 1, ImmutableArray.Create(entry), 1, 0,
            "complete", true, null, ImmutableArray<string>.Empty,
            ImmutableDictionary<string, string>.Empty,
            ArtifactNames.Skip(2).ToImmutableDictionary(name => name, name => name, StringComparer.Ordinal),
            ImmutableArray<string>.Empty);
        byte[] json = LedgerValidator.SerializeManifest(manifest);
        byte[] persisted = new byte[json.Length + 1];
        Buffer.BlockCopy(json, 0, persisted, 0, json.Length);
        persisted[^1] = (byte)'\n';
        byte[] ledgerJson = LedgerValidator.SerializeRow(new LedgerRow(1, identity, entry, "callback", true,
            0, null, 2, "native_callback", 0, 0, null));
        byte[] ledger = new byte[ledgerJson.Length + 1];
        Buffer.BlockCopy(ledgerJson, 0, ledger, 0, ledgerJson.Length);
        ledger[^1] = (byte)'\n';

        string directory = Path.Combine(Path.GetTempPath(), "publication-reader-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var session = new RunEvidenceSession(identity,
                new RunEvidenceOptions(directory, 8, 64 * 1024, 256 * 1024, 16, TimeSpan.FromSeconds(2)), TimeProvider.System);
            var report = new KafkaStatisticsReport(
                new KafkaStatisticsIdentity(identity.ScenarioId, identity.RunId, KafkaClientRole.LoadGenProducer, identity.ClientId),
                "reader-session", DateTimeOffset.UnixEpoch, "{}", null, ImmutableArray<KafkaStatisticsDiagnostic>.Empty);
            await session.WriteAsync(report, TestContext.Current.CancellationToken);
            await session.CompleteAsync(new KafkaStatisticsCompletion(true, ImmutableArray<KafkaStatisticsDiagnostic>.Empty,
                ImmutableArray<string>.Empty), TestContext.Current.CancellationToken);
            if (session.IsFaulted) throw new InvalidOperationException("Evidence fixture capture faulted.");
            return new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["ledger.jsonl"] = ledger,
                ["manifest.json"] = persisted,
                ["statistics.jsonl"] = await File.ReadAllBytesAsync(Path.Combine(directory, "statistics.jsonl"), TestContext.Current.CancellationToken),
                ["request-lifecycle.jsonl"] = await File.ReadAllBytesAsync(Path.Combine(directory, "request-lifecycle.jsonl"), TestContext.Current.CancellationToken),
                ["capabilities.json"] = await File.ReadAllBytesAsync(Path.Combine(directory, "capabilities.json"), TestContext.Current.CancellationToken)
            };
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static Task ValidateAsync(RunArtifactIndex index, Dictionary<string, byte[]> payloads) =>
        RunArtifactIndexReader.ValidateArtifactSetAsync(index, ObservedNames(),
            (name, _) => ValueTask.FromResult<Stream>(new MemoryStream(payloads[name], writable: false)),
            MaximumArtifactBytes, MaximumAggregateBytes, TestContext.Current.CancellationToken);

    private static async Task AssertRehashedArtifactRejectedAsync(RunIdentity identity,
        Dictionary<string, byte[]> validPayloads, string name, byte[] invalidBytes)
    {
        Dictionary<string, byte[]> payloads = validPayloads.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        payloads[name] = invalidBytes;
        RunArtifactIndex index = await ReadAsync(CreateIndex(identity, payloads), identity,
            MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => ValidateAsync(index, payloads));
    }

    private static byte[] CreateIndex(RunIdentity identity, Dictionary<string, byte[]> payloads)
        => SerializeIndex(identity, ArtifactNames.Select(name => new Descriptor(name, payloads[name].LongLength,
            Convert.ToHexString(SHA256.HashData(payloads[name])).ToLowerInvariant())).ToArray());

    private static byte[] SerializeIndex(RunIdentity identity, IReadOnlyList<Descriptor> descriptors)
    {
        var value = new
        {
            schema_version = 1,
            identity = new
            {
                scenario_id = identity.ScenarioId,
                configuration_id = identity.ConfigurationId,
                run_id = identity.RunId,
                instance_id = identity.InstanceId,
                expected_instance_id = identity.ExpectedInstanceId,
                client_id = identity.ClientId
            },
            worker_status = "complete",
            bookkeeping_complete = true,
            worker_complete = true,
            publication_complete = true,
            artifacts = descriptors
        };
        return JsonSerializer.SerializeToUtf8Bytes(value);
    }

    private static RunIdentity Identity() => new("A01", "C0", "run-index", "instance-index", "instance-index", "loadgen-index");
    private static string[] ObservedNames() => [.. ArtifactNames, "index.json"];
    private sealed record Descriptor([property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("length")] long Length, [property: JsonPropertyName("sha256")] string Sha256);
}
