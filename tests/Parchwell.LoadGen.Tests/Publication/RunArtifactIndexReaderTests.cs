using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Collections.Immutable;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Publication;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class RunArtifactIndexReaderTests
{
    private static readonly string[] ArtifactNames =
    ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private const int MaximumIndexBytes = 64 * 1024;
    private const int MaximumArtifactBytes = 256 * 1024;
    private const int MaximumAggregateBytes = 1024 * 1024;

    [Fact]
    public async Task Given_CompleteIndexAndExactPayloads_When_ReadingAndValidating_Then_AcceptsTheIndexedSet()
    {
        // The index is the reader acceptance marker; all five payloads must match the bytes it names.
        RunIdentity identity = Identity();
        RunEvidenceExpectations expectations = Expectations(identity);
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity, expectations);
        byte[] bytes = CreateIndex(identity, payloads);
        RunArtifactIndex index = await ReadAsync(bytes, identity, MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes);

        Assert.Equal(1, index.SchemaVersion);
        Assert.Equal(bytes.Length, index.IndexLength);
        Assert.Equal(ArtifactNames, index.Artifacts.Select(item => item.Name));
        await RunArtifactIndexReader.ValidateArtifactSetAsync(index,
            0,
            expectations,
            ObservedNames(),
            (name, _) => ValueTask.FromResult<Stream>(new MemoryStream(payloads[name], writable: false)),
            MaximumArtifactBytes, MaximumAggregateBytes, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_RehashedStatisticsAndCapabilityBrokerNamesAgreeOnWrongBroker_When_Validating_Then_RejectsAgainstTrustedInventory()
    {
        RunIdentity identity = Identity();
        RunEvidenceExpectations expectations = Expectations(identity);
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity, expectations);
        RunArtifactIndex validIndex = await ReadAsync(CreateIndex(identity, payloads), identity,
            MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes);
        await ValidateAsync(validIndex, expectations, payloads);

        Dictionary<string, byte[]> renamed = payloads.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        byte[] statistics = renamed["statistics.jsonl"];
        JsonNode report = JsonNode.Parse(statistics.AsSpan(0, statistics.Length - 1))!;
        report["typed_snapshot"]!["brokers"]!.AsArray()[0]!["name"] = "invented-broker";
        renamed["statistics.jsonl"] = [.. JsonSerializer.SerializeToUtf8Bytes(report), (byte)'\n'];

        JsonNode capabilities = JsonNode.Parse(renamed["capabilities.json"])!;
        var observedNames = new JsonArray();
        observedNames.Add("invented-broker");
        capabilities["observed_broker_names"] = observedNames;
        renamed["capabilities.json"] = JsonSerializer.SerializeToUtf8Bytes(capabilities);

        // Rebuilds index hashes for both matching persisted names. Only the independently supplied inventory remains broker-1.
        await AssertPayloadSetRejectedAsync(identity, expectations, renamed);
    }

    [Fact]
    public async Task Given_IdentityMismatch_When_ReadingIndex_Then_RejectsItBeforeAcceptingAnyFiles()
    {
        // A valid artifact set from another run must never be attributed to the expected run.
        RunIdentity identity = Identity();
        byte[] bytes = CreateIndex(identity, await PayloadsAsync(identity, Expectations(identity)));
        RunIdentity otherIdentity = identity with { RunId = "other-run" };

        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(bytes, otherIdentity, MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes));
    }

    [Fact]
    public async Task Given_DuplicateOrIncompleteIndexFields_When_Reading_Then_RejectsTheIndex()
    {
        // Duplicate JSON properties and incomplete status flags can make readers disagree about publication state.
        RunIdentity identity = Identity();
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity, Expectations(identity));
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
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity, Expectations(identity));
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
        RunEvidenceExpectations expectations = Expectations(identity);
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity, expectations);
        byte[] indexBytes = CreateIndex(identity, payloads);
        RunArtifactIndex index = await ReadAsync(indexBytes, Identity(), MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes);
        payloads["ledger.jsonl"] = Encoding.UTF8.GetBytes("different-ledger");

        await Assert.ThrowsAsync<InvalidDataException>(() => RunArtifactIndexReader.ValidateArtifactSetAsync(index,
            0,
            expectations,
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
        RunEvidenceExpectations expectations = Expectations(identity);
        byte[] indexBytes = CreateIndex(identity, await PayloadsAsync(identity, expectations));
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
                0,
                expectations,
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
    public async Task Given_MissingOrNonzeroObservedWorkerExitCode_When_Validating_Then_RejectsBeforeOpeningPayloads()
    {
        var index = new RunArtifactIndex(1, Identity(), "complete", true, true, true,
            ImmutableArray<RunArtifactDescriptor>.Empty) { IndexLength = 1 };
        foreach (int? exitCode in new int?[] { null, 1, -1 })
        {
            int opened = 0;
            InvalidDataException failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
                RunArtifactIndexReader.ValidateArtifactSetAsync(index, exitCode, Expectations(Identity()), ObservedNames(), (name, _) =>
                {
                    Interlocked.Increment(ref opened);
                    return ValueTask.FromResult<Stream>(new MemoryStream());
                }, MaximumArtifactBytes, MaximumAggregateBytes, TestContext.Current.CancellationToken));
            Assert.Equal("publication_worker_exit_code_invalid", failure.Message);
            Assert.Equal(0, opened);
        }
    }

    [Fact]
    public async Task Given_ArtifactStreamExceedsItsIndexedLength_When_Validating_Then_ReadsOnlyOneOverflowByte()
    {
        byte[] emptyHash = SHA256.HashData([]);
        var descriptors = ArtifactNames.Select(name => new RunArtifactDescriptor(name, 0,
            Convert.ToHexString(emptyHash).ToLowerInvariant())).ToArray();
        descriptors[0] = new RunArtifactDescriptor("ledger.jsonl", 1,
            Convert.ToHexString(SHA256.HashData([(byte)'x'])).ToLowerInvariant());
        var index = new RunArtifactIndex(1, Identity(), "complete", true, true, true,
            descriptors.ToImmutableArray()) { IndexLength = 1 };
        var oversizedPayload = new CountingReadStream(new byte[100_000]);
        int opened = 0;

        await Assert.ThrowsAsync<InvalidDataException>(() => RunArtifactIndexReader.ValidateArtifactSetAsync(index, 0,
            Expectations(Identity()), ObservedNames(), (name, _) =>
            {
                opened++;
                return ValueTask.FromResult<Stream>(oversizedPayload);
            }, MaximumArtifactBytes, MaximumAggregateBytes, TestContext.Current.CancellationToken));

        Assert.Equal(1, opened);
        Assert.Equal(2, oversizedPayload.BytesRead);
    }

    [Fact]
    public async Task Given_FileBudgetExceedsMaterializationLimit_When_ReadingOrValidating_Then_RejectsBeforeIo()
    {
        const long unsupportedBudget = (long)int.MaxValue + 1;
        var oversizedIndexStream = new TrackingReadStream();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => RunArtifactIndexReader.ReadAsync(oversizedIndexStream,
            Identity(), unsupportedBudget, MaximumArtifactBytes, long.MaxValue, TestContext.Current.CancellationToken));
        Assert.Equal(0, oversizedIndexStream.ReadCount);

        var oversizedArtifactStream = new TrackingReadStream();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => RunArtifactIndexReader.ReadAsync(oversizedArtifactStream,
            Identity(), MaximumIndexBytes, unsupportedBudget, long.MaxValue, TestContext.Current.CancellationToken));
        Assert.Equal(0, oversizedArtifactStream.ReadCount);

        var index = new RunArtifactIndex(1, Identity(), "complete", true, true, true,
            ImmutableArray<RunArtifactDescriptor>.Empty) { IndexLength = 1 };
        int opened = 0;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => RunArtifactIndexReader.ValidateArtifactSetAsync(index,
            0,
            Expectations(Identity()),
            ObservedNames(), (name, _) =>
            {
                Interlocked.Increment(ref opened);
                return ValueTask.FromResult<Stream>(new MemoryStream());
            }, unsupportedBudget, long.MaxValue, TestContext.Current.CancellationToken));
        Assert.Equal(0, opened);
    }

    [Fact]
    public async Task Given_RehashedMalformedLedgerOrEvidenceArtifacts_When_Validating_Then_RejectsTheArtifactSet()
    {
        RunIdentity identity = Identity();
        RunEvidenceExpectations expectations = Expectations(identity);
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity, expectations);
        LedgerRow row = LedgerValidator.DeserializeRow(payloads["ledger.jsonl"].AsSpan(0, payloads["ledger.jsonl"].Length - 1));
        byte[] inventoryMismatch = [.. LedgerValidator.SerializeRow(row with
        {
            Attempt = row.Attempt with { AttemptId = "attempt-other" }
        }), (byte)'\n'];
        string statistics = Encoding.UTF8.GetString(payloads["statistics.jsonl"]);
        Assert.Contains("run-index", statistics, StringComparison.Ordinal);
        byte[] wrongStatisticsIdentity = Encoding.UTF8.GetBytes(statistics.Replace("run-index", "other-run", StringComparison.Ordinal));
        byte[] manifestWithoutFinalLf = payloads["manifest.json"][..^1];

        await AssertRehashedArtifactRejectedAsync(identity, expectations, payloads, "ledger.jsonl", Encoding.UTF8.GetBytes("ledger.jsonl\n"));
        await AssertRehashedArtifactRejectedAsync(identity, expectations, payloads, "ledger.jsonl", []);
        await AssertRehashedArtifactRejectedAsync(identity, expectations, payloads, "ledger.jsonl", [.. payloads["ledger.jsonl"], .. payloads["ledger.jsonl"]]);
        await AssertRehashedArtifactRejectedAsync(identity, expectations, payloads, "ledger.jsonl", inventoryMismatch);
        await AssertRehashedArtifactRejectedAsync(identity, expectations, payloads, "manifest.json", manifestWithoutFinalLf);
        await AssertRehashedArtifactRejectedAsync(identity, expectations, payloads, "statistics.jsonl", wrongStatisticsIdentity);
        await AssertRehashedArtifactRejectedAsync(identity, expectations, payloads, "request-lifecycle.jsonl", Encoding.UTF8.GetBytes("{}\n"));
        await AssertRehashedArtifactRejectedAsync(identity, expectations, payloads, "capabilities.json", Encoding.UTF8.GetBytes("{}"));

        byte[] inventedOverride = AddInventedOverride(payloads["capabilities.json"], identity);
        await AssertRehashedArtifactRejectedAsync(identity, expectations, payloads, "capabilities.json", inventedOverride);

        Dictionary<string, byte[]> missingStatisticsCompletion = payloads.ToDictionary(pair => pair.Key,
            pair => pair.Value, StringComparer.Ordinal);
        JsonNode capability = JsonNode.Parse(missingStatisticsCompletion["capabilities.json"])!;
        capability["statistics_completion"] = null;
        missingStatisticsCompletion["capabilities.json"] = JsonSerializer.SerializeToUtf8Bytes(capability);
        await AssertPayloadSetRejectedAsync(identity, expectations, missingStatisticsCompletion);
    }

    [Theory]
    [InlineData("possibly_persisted_observed")]
    [InlineData("persistence_status_missing")]
    [InlineData("async_delivery_failure_observed")]
    [InlineData("broker=broker-1;counter=req_timeouts;observed=1")]
    [InlineData("broker=broker-1;counter=txretries;observed=1")]
    [InlineData("broker=broker-1;counter=txerrs;observed=1")]
    [InlineData("broker=broker-1;counter=rxerrs;observed=1")]
    [InlineData("native_error_callback_observed")]
    [InlineData("statistics_guard_unproven")]
    [InlineData("statistics_guard_incomplete")]
    [InlineData("request_lifecycle_capture_unsupported")]
    [InlineData("broker_lifecycle_inventory_unproven")]
    public async Task Given_OracleGuardIsSupportedByPayloads_When_RehashedManifestOmitsIt_Then_RejectsMissingGuard(string guard)
    {
        RunIdentity identity = Identity();
        RunEvidenceExpectations expectations = Expectations(identity);
        if (guard.StartsWith("statistics_guard_", StringComparison.Ordinal))
            expectations = new RunEvidenceExpectations(expectations.ResolvedConfiguration, false, expectations.ExpectedBrokerNames);
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity, expectations);
        LedgerRow row = LedgerValidator.DeserializeRow(payloads["ledger.jsonl"].AsSpan(0, payloads["ledger.jsonl"].Length - 1));
        row = guard switch
        {
            "possibly_persisted_observed" => row with { PersistenceStatus = (int)PersistenceStatus.PossiblyPersisted },
            "persistence_status_missing" => row with { PersistenceStatus = null, PersistenceProvenance = "missing" },
            "async_delivery_failure_observed" => row with { Delivered = false, ErrorCode = (int)ErrorCode.Local_MsgTimedOut },
            _ => row
        };
        payloads["ledger.jsonl"] = [.. LedgerValidator.SerializeRow(row), (byte)'\n'];
        JsonNode capabilities = JsonNode.Parse(payloads["capabilities.json"])!;
        if (guard.StartsWith("broker=", StringComparison.Ordinal))
        {
            string counter = guard.Split(';')[1].Split('=')[1];
            string field = counter switch
            {
                "req_timeouts" => "request_timeouts", "txretries" => "transmission_retries",
                "txerrs" => "transmission_errors", "rxerrs" => "receive_errors",
                _ => throw new InvalidOperationException()
            };
            JsonNode statistics = JsonNode.Parse(payloads["statistics.jsonl"])!;
            statistics["typed_snapshot"]!["brokers"]![0]![field] = 1;
            payloads["statistics.jsonl"] = [.. JsonSerializer.SerializeToUtf8Bytes(statistics), (byte)'\n'];
            capabilities["guard_observations"] = new JsonArray(guard);
        }
        else if (guard == "native_error_callback_observed")
        {
            var lifecycle = new
            {
                schema_version = 1, identity, session_id = "managed_error_callback", receipt_sequence = 1,
                received_at = DateTimeOffset.UnixEpoch, event_type = "native_error",
                error_code = (int)ErrorCode.Local_Transport, error_name = ErrorCode.Local_Transport.ToString(),
                reason = "synthetic error", is_error = true, is_fatal = false, provenance = "managed_error_callback"
            };
            payloads["request-lifecycle.jsonl"] = [.. JsonSerializer.SerializeToUtf8Bytes(lifecycle,
                EvidenceJsonOptions), (byte)'\n'];
            capabilities["guard_observations"] = new JsonArray(guard);
        }
        else if (guard == "statistics_guard_unproven") capabilities["statistics_completion"] = null;
        else if (guard == "statistics_guard_incomplete") capabilities["statistics_completion"]!["is_complete"] = false;
        payloads["capabilities.json"] = JsonSerializer.SerializeToUtf8Bytes(capabilities);

        RunManifest manifest = LedgerValidator.DeserializeManifest(payloads["manifest.json"].AsSpan(0, payloads["manifest.json"].Length - 1), 1);
        manifest = manifest with { GuardObservations = manifest.GuardObservations.Add(guard).Distinct(StringComparer.Ordinal).ToImmutableArray() };
        payloads["manifest.json"] = [.. LedgerValidator.SerializeManifest(manifest), (byte)'\n'];
        RunArtifactIndex valid = await ReadAsync(CreateIndex(identity, payloads), identity,
            MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes);
        // Complete bookkeeping with retained ambiguity remains publishable; this is not oracle classification.
        await ValidateAsync(valid, expectations, payloads);

        manifest = manifest with { GuardObservations = manifest.GuardObservations.Remove(guard) };
        payloads["manifest.json"] = [.. LedgerValidator.SerializeManifest(manifest), (byte)'\n'];
        RunArtifactIndex corrupt = await ReadAsync(CreateIndex(identity, payloads), identity,
            MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes);
        InvalidDataException failure = await Assert.ThrowsAsync<InvalidDataException>(() => ValidateAsync(corrupt, expectations, payloads));
        Assert.Equal("publication_manifest_guard_observation_missing:" + guard, failure.Message);
    }

    [Fact]
    public async Task Given_SynchronousPreEnqueueRejection_When_Validating_Then_DoesNotRequireCallbackGuards()
    {
        RunIdentity identity = Identity();
        RunEvidenceExpectations expectations = Expectations(identity);
        Dictionary<string, byte[]> payloads = await PayloadsAsync(identity, expectations);
        LedgerRow row = LedgerValidator.DeserializeRow(payloads["ledger.jsonl"].AsSpan(0, payloads["ledger.jsonl"].Length - 1));
        row = row with { CompletionSource = "synchronous_rejection", Delivered = false,
            ErrorCode = (int)ErrorCode.Local_QueueFull, PersistenceStatus = null, PersistenceProvenance = "pre_enqueue",
            DeliveryLatencyMs = null, SynchronousCallDurationMs = 0 };
        payloads["ledger.jsonl"] = [.. LedgerValidator.SerializeRow(row), (byte)'\n'];
        RunArtifactIndex index = await ReadAsync(CreateIndex(identity, payloads), identity,
            MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes);
        await ValidateAsync(index, expectations, payloads);
    }

    private static async Task<RunArtifactIndex> ReadAsync(byte[] bytes, RunIdentity identity, long maxIndex, long maxArtifact, long maxAggregate)
        => await RunArtifactIndexReader.ReadAsync(new MemoryStream(bytes, writable: false), identity, maxIndex,
            maxArtifact, maxAggregate, TestContext.Current.CancellationToken);

    private static async Task<Dictionary<string, byte[]>> PayloadsAsync(RunIdentity identity,
        RunEvidenceExpectations expectations)
    {
        var entry = new AttemptEntry(identity.InstanceId, "attempt-0", 0, "tenant", "d0", "tenant-d0", 0,
            "uploaded", 0, 0);
        var manifest = new RunManifest(1, identity, "1", 1, 1, ImmutableArray.Create(entry), 1, 0,
            "complete", true, null, ImmutableArray<string>.Empty,
            ImmutableDictionary<string, string>.Empty,
            ArtifactNames.Skip(2).ToImmutableDictionary(name => name, name => name, StringComparer.Ordinal),
            ImmutableArray.Create("request_lifecycle_capture_unsupported", "broker_lifecycle_inventory_unproven"));
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
                new RunEvidenceOptions(directory, 8, 64 * 1024, 256 * 1024, 16, TimeSpan.FromSeconds(2)),
                TimeProvider.System, expectations);
            var snapshot = new KafkaStatisticsSnapshot("native-producer", identity.ClientId, "producer", 1, 1, 1,
                [new KafkaBrokerStatistics("broker-1", 1, "UP", 1, 0, 0, 0, 0, 0,
                    new KafkaWindowStatistics(0, null), new KafkaWindowStatistics(0, null))]);
            var report = new KafkaStatisticsReport(
                new KafkaStatisticsIdentity(identity.ScenarioId, identity.RunId, KafkaClientRole.LoadGenProducer, identity.ClientId),
                "reader-session", DateTimeOffset.UnixEpoch, "{}", snapshot, ImmutableArray<KafkaStatisticsDiagnostic>.Empty);
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

    private static ResolvedRoleConfiguration CreateResolvedConfiguration(RunIdentity identity)
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = identity.ClientId;
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        KafkaBaseline<ProducerConfig> baseline = provider.GetRequiredService<KafkaConfigurationFactory>().CreateLoadGenProducer();
        ImmutableDictionary<string, ResolvedSetting> settings = baseline.Settings.ToImmutableDictionary(
            setting => setting.Key, setting => new ResolvedSetting(setting.Value!, setting, null), StringComparer.Ordinal);
        return new ResolvedRoleConfiguration(KafkaClientRole.LoadGenProducer, settings, baseline.Omissions);
    }

    private static Task ValidateAsync(RunArtifactIndex index, RunEvidenceExpectations expectations,
        Dictionary<string, byte[]> payloads) =>
        RunArtifactIndexReader.ValidateArtifactSetAsync(index, 0, expectations, ObservedNames(),
            (name, _) => ValueTask.FromResult<Stream>(new MemoryStream(payloads[name], writable: false)),
            MaximumArtifactBytes, MaximumAggregateBytes, TestContext.Current.CancellationToken);

    private static async Task AssertPayloadSetRejectedAsync(RunIdentity identity,
        RunEvidenceExpectations expectations, Dictionary<string, byte[]> payloads)
    {
        RunArtifactIndex index = await ReadAsync(CreateIndex(identity, payloads), identity,
            MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => ValidateAsync(index, expectations, payloads));
    }

    private static async Task AssertRehashedArtifactRejectedAsync(RunIdentity identity,
        RunEvidenceExpectations expectations,
        Dictionary<string, byte[]> validPayloads, string name, byte[] invalidBytes)
    {
        Dictionary<string, byte[]> payloads = validPayloads.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        payloads[name] = invalidBytes;
        RunArtifactIndex index = await ReadAsync(CreateIndex(identity, payloads), identity,
            MaximumIndexBytes, MaximumArtifactBytes, MaximumAggregateBytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => ValidateAsync(index, expectations, payloads));
    }

    private static RunEvidenceExpectations Expectations(RunIdentity identity) =>
        new(CreateResolvedConfiguration(identity), requireStatisticsEvidence: true,
            expectedBrokerNames: ImmutableArray.Create("broker-1"));

    private static byte[] AddInventedOverride(byte[] capabilityBytes, RunIdentity identity)
    {
        JsonNode capability = JsonNode.Parse(capabilityBytes)!;
        JsonObject setting = capability["resolved_configuration"]!["settings"]!.AsObject()["client.id"]!.AsObject();
        setting["value"] = "invented-client-id";
        setting["override"] = new JsonObject
        {
            ["scenario_id"] = identity.ScenarioId,
            ["configuration_id"] = identity.ConfigurationId,
            ["location"] = new JsonObject
            {
                ["path"] = "scenarios/A01.yaml",
                ["line"] = 1,
                ["column"] = 1
            }
        };
        return JsonSerializer.SerializeToUtf8Bytes(capability);
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
    private sealed class TrackingReadStream : MemoryStream
    {
        public int ReadCount { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class CountingReadStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public int BytesRead { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await base.ReadAsync(buffer, cancellationToken);
            BytesRead += read;
            return read;
        }
    }

    private sealed record Descriptor([property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("length")] long Length, [property: JsonPropertyName("sha256")] string Sha256);
}
