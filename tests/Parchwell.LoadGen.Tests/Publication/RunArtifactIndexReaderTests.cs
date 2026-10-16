using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Immutable;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Publication;
using Xunit;

namespace Parchwell.LoadGen.Tests.Publication;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class RunArtifactIndexReaderTests
{
    private static readonly string[] ArtifactNames =
    ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];

    [Fact]
    public async Task Given_CompleteIndexAndExactPayloads_When_ReadingAndValidating_Then_AcceptsTheIndexedSet()
    {
        // The index is the reader acceptance marker; all five payloads must match the bytes it names.
        RunIdentity identity = Identity();
        Dictionary<string, byte[]> payloads = Payloads(identity);
        byte[] bytes = CreateIndex(identity, payloads);
        RunArtifactIndex index = await ReadAsync(bytes, identity, 4096, 4096, 8192);

        Assert.Equal(1, index.SchemaVersion);
        Assert.Equal(bytes.Length, index.IndexLength);
        Assert.Equal(ArtifactNames, index.Artifacts.Select(item => item.Name));
        await RunArtifactIndexReader.ValidateArtifactSetAsync(index,
            ObservedNames(),
            (name, _) => ValueTask.FromResult<Stream>(new MemoryStream(payloads[name], writable: false)),
            4096, 8192, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_IdentityMismatch_When_ReadingIndex_Then_RejectsItBeforeAcceptingAnyFiles()
    {
        // A valid artifact set from another run must never be attributed to the expected run.
        RunIdentity identity = Identity();
        byte[] bytes = CreateIndex(identity, Payloads(identity));
        RunIdentity otherIdentity = identity with { RunId = "other-run" };

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(bytes, otherIdentity, 4096, 4096, 8192));
    }

    [Fact]
    public async Task Given_DuplicateOrIncompleteIndexFields_When_Reading_Then_RejectsTheIndex()
    {
        // Duplicate JSON properties and incomplete status flags can make readers disagree about publication state.
        RunIdentity identity = Identity();
        Dictionary<string, byte[]> payloads = Payloads(identity);
        string valid = Encoding.UTF8.GetString(CreateIndex(identity, payloads));
        string duplicate = valid.Replace("{\"schema_version\":1,", "{\"schema_version\":1,\"schema_version\":1,", StringComparison.Ordinal);
        string incomplete = valid.Replace("\"publication_complete\":true", "\"publication_complete\":false", StringComparison.Ordinal);

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(Encoding.UTF8.GetBytes(duplicate), Identity(), 4096, 4096, 8192));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(Encoding.UTF8.GetBytes(incomplete), Identity(), 4096, 4096, 8192));
    }

    [Fact]
    public async Task Given_MissingDuplicateOrReorderedArtifactNames_When_Reading_Then_RejectsTheSet()
    {
        // Exact file membership prevents a partial upload from being treated as a complete run.
        RunIdentity identity = Identity();
        Dictionary<string, byte[]> payloads = Payloads(identity);
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

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(missing, Identity(), 4096, 4096, 8192));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(duplicate, Identity(), 4096, 4096, 8192));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(reordered, Identity(), 4096, 4096, 8192));
    }

    [Fact]
    public async Task Given_CorruptPayloadOrAggregateBudgetExceeded_When_Validating_Then_RejectsThePublication()
    {
        // The index hash and budgets protect the exact bytes readers later download.
        RunIdentity identity = Identity();
        Dictionary<string, byte[]> payloads = Payloads(identity);
        byte[] indexBytes = CreateIndex(identity, payloads);
        RunArtifactIndex index = await ReadAsync(indexBytes, Identity(), 4096, 4096, 8192);
        payloads["ledger.jsonl"] = Encoding.UTF8.GetBytes("different-ledger");

        await Assert.ThrowsAsync<InvalidDataException>(() => RunArtifactIndexReader.ValidateArtifactSetAsync(index,
            ObservedNames(),
            (name, _) => ValueTask.FromResult<Stream>(new MemoryStream(payloads[name], writable: false)),
            4096, 8192, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(indexBytes, Identity(), 4096, 4096, index.IndexLength));
    }

    [Fact]
    public async Task Given_ExtraStaleDuplicateMissingOrWrongPrefixMembers_When_Validating_Then_RejectsBeforeOpeningPayloads()
    {
        // A valid index cannot hide stale objects or an incomplete Blob listing beneath the run prefix.
        RunIdentity identity = Identity();
        byte[] indexBytes = CreateIndex(identity, Payloads(identity));
        RunArtifactIndex index = await ReadAsync(indexBytes, identity, 4096, 4096, 8192);
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
                }, 4096, 8192, TestContext.Current.CancellationToken));
            Assert.Equal(0, opened);
        }
    }

    private static async Task<RunArtifactIndex> ReadAsync(byte[] bytes, RunIdentity identity, long maxIndex, long maxArtifact, long maxAggregate)
        => await RunArtifactIndexReader.ReadAsync(new MemoryStream(bytes, writable: false), identity, maxIndex,
            maxArtifact, maxAggregate, TestContext.Current.CancellationToken);

    private static Dictionary<string, byte[]> Payloads(RunIdentity identity)
    {
        var payloads = ArtifactNames.ToDictionary(name => name, name => Encoding.UTF8.GetBytes(name + "\n"), StringComparer.Ordinal);
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
        payloads["manifest.json"] = persisted;
        return payloads;
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
