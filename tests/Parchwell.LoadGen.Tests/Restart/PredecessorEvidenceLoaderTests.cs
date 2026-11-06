using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.LoadGen.Run.Restart;
using Parchwell.LoadGen.Tests.Host;
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

    [Theory]
    [InlineData("C0", false, false)]
    [InlineData("C0", false, true)]
    [InlineData("C0", true, false)]
    [InlineData("C0", true, true)]
    [InlineData("C1", false, false)]
    [InlineData("C1", false, true)]
    [InlineData("C1", true, false)]
    [InlineData("C1", true, true)]
    public async Task Given_SuccessorResolvedIdentityAndCompletePredecessor_When_Loading_Then_ReturnsExactEvidenceWithoutChangingCaller(
        string phase, bool independentBrokers, bool requireStatistics)
    {
        BoundPublication fixture = await CreateBoundPublicationAsync(phase, independentBrokers, requireStatistics);
        RunEvidenceExpectations caller = fixture.SuccessorExpectations;
        ResolvedRoleConfiguration selected = caller.ResolvedConfiguration;
        byte[] original = JsonSerializer.SerializeToUtf8Bytes(caller);
        KeyValuePair<string, ResolvedSetting>[] settings = [.. selected.Settings];
        ImmutableArray<KafkaSettingMetadata> omissions = selected.Omissions;
        ImmutableArray<ResolvedOmission> scenarioOmissions = selected.ScenarioOmissions;
        ImmutableArray<string> expectedBrokers = caller.ExpectedBrokerNames;
        Assert.Equal("pod-b", selected.Settings["client.id"].Value);
        Assert.Equal("pod-b", selected.Settings["client.id"].Baseline.Value);
        Assert.Null(selected.Settings["client.id"].Override);
        Assert.Equal("100", selected.Settings["message.timeout.ms"].Value);
        Assert.NotNull(selected.Settings["message.timeout.ms"].Override);

        RunArtifactSet set = await fixture.LoadAsync(caller);

        Assert.Equal(LedgerValidator.SerializeManifest(fixture.Manifest), LedgerValidator.SerializeManifest(set.Manifest));
        Assert.Equal(fixture.Rows, set.Rows);
        Assert.Equal(RestartFixture.Plan().Planned, RestartInstance.Create(fixture.Successor.Identity, set.Manifest, set.Rows).Plan.Planned);
        Assert.Same(selected, caller.ResolvedConfiguration);
        Assert.Same(selected.Settings, caller.ResolvedConfiguration.Settings);
        foreach ((string key, ResolvedSetting setting) in settings)
        {
            Assert.Same(setting, caller.ResolvedConfiguration.Settings[key]);
            Assert.Same(setting.Baseline, caller.ResolvedConfiguration.Settings[key].Baseline);
            Assert.Same(setting.Override, caller.ResolvedConfiguration.Settings[key].Override);
        }
        Assert.Equal(omissions, caller.ResolvedConfiguration.Omissions);
        Assert.Equal(scenarioOmissions, caller.ResolvedConfiguration.ScenarioOmissions);
        Assert.Equal(independentBrokers, caller.RequiresIndependentBrokerCoverage);
        Assert.Equal(requireStatistics, caller.RequireStatisticsEvidence);
        Assert.Equal(expectedBrokers, caller.ExpectedBrokerNames);
        Assert.Equal(original, JsonSerializer.SerializeToUtf8Bytes(caller));
    }

    [Theory]
    [InlineData("client-effective", "publication_evidence_validation_failed")]
    [InlineData("client-baseline", "publication_evidence_validation_failed")]
    [InlineData("client-source", "publication_evidence_validation_failed")]
    [InlineData("client-override", "publication_evidence_validation_failed")]
    [InlineData("timeout-effective", "publication_evidence_validation_failed")]
    [InlineData("timeout-baseline", "publication_evidence_validation_failed")]
    [InlineData("timeout-source", "publication_evidence_validation_failed")]
    [InlineData("timeout-origin", "publication_evidence_validation_failed")]
    [InlineData("statistics-envelope-client", "publication_evidence_validation_failed")]
    [InlineData("statistics-snapshot-client", "publication_evidence_validation_failed")]
    [InlineData("registration-client", "predecessor_registration_invalid")]
    [InlineData("completion-client", "predecessor_completion_invalid")]
    [InlineData("registration-source", "predecessor_registration_invalid")]
    [InlineData("capture-boundary", "publication_evidence_validation_failed")]
    public async Task Given_IdentityBoundPredecessorHasOneSemanticMismatch_When_LoadingWithSuccessorRole_Then_RejectsIt(
        string change, string error)
    {
        BoundPublication fixture = await CreateBoundPublicationAsync("C1", independentBrokers: change != "capture-boundary", requireStatistics: true);
        Dictionary<string, byte[]> publicationBefore = PayloadNames.ToDictionary(name => name,
            name => fixture.Objects[fixture.First.PublicationPrefix + name].ToArray(), StringComparer.Ordinal);
        string name = change switch
        {
            "registration-client" or "registration-source" => "registration.json",
            "completion-client" => "completion.json",
            "statistics-envelope-client" or "statistics-snapshot-client" => "publication/statistics.jsonl",
            _ => "publication/capabilities.json"
        };
        JsonNode document = JsonNode.Parse(fixture.Objects[fixture.First.InstancePrefix + name])!;
        JsonNode? settings = document["resolved_configuration"]?["settings"];
        switch (change)
        {
            case "client-effective": settings!["client.id"]!["value"] = "pod-b"; break;
            case "client-baseline": settings!["client.id"]!["baseline"]!["value"] = "pod-b"; break;
            case "client-source": settings!["client.id"]!["baseline"]!["sources"]![0]!["url"] = "https://example.invalid/client"; break;
            case "client-override":
                settings!["client.id"]!["override"] = settings["message.timeout.ms"]!["override"]!.DeepClone();
                break;
            case "timeout-effective": settings!["message.timeout.ms"]!["value"] = "101"; break;
            case "timeout-baseline": settings!["message.timeout.ms"]!["baseline"]!["value"] = "101"; break;
            case "timeout-source": settings!["message.timeout.ms"]!["baseline"]!["sources"]![0]!["url"] = "https://example.invalid/timeout"; break;
            case "timeout-origin":
                JsonNode location = settings!["message.timeout.ms"]!["override"]!["location"]!;
                location["line"] = location["line"]!.GetValue<int>() + 1;
                break;
            case "statistics-envelope-client": document["statistics_identity"]!["client_id"] = "pod-b"; break;
            case "statistics-snapshot-client": document["typed_snapshot"]!["client_id"] = "pod-b"; break;
            case "registration-client" or "completion-client": document["identity"]!["client_id"] = "pod-b"; break;
            case "registration-source": document["source_commit"] = new string('b', 40); break;
            case "capture-boundary":
                long boundary = document["statistics_completion"]!["drain_boundary_microseconds"]!.GetValue<long>();
                document["statistics_completion"]!["first_post_drain_timestamp_microseconds"] = boundary;
                JsonNode statistics = JsonNode.Parse(fixture.Objects[fixture.First.PublicationPrefix + "statistics.jsonl"])!;
                statistics["typed_snapshot"]!["timestamp_microseconds"] = boundary;
                fixture.ReplaceJson("publication/statistics.jsonl", statistics);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        fixture.ReplaceJson(name, document);
        if (name.StartsWith("publication/", StringComparison.Ordinal)) fixture.Rehash();
        else
            foreach ((string payload, byte[] bytes) in publicationBefore)
                Assert.Equal(bytes, fixture.Objects[fixture.First.PublicationPrefix + payload]);

        Assert.Equal(error, (await Assert.ThrowsAsync<InvalidDataException>(() => fixture.LoadAsync(fixture.SuccessorExpectations))).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_IndependentExpectedBrokerAbsentFromCompletePredecessor_When_Loading_Then_PreservesInventoryAndRejectsIt(bool requireStatistics)
    {
        BoundPublication fixture = await CreateBoundPublicationAsync("C1", independentBrokers: true, requireStatistics);
        var expectations = new RunEvidenceExpectations(fixture.SuccessorExpectations.ResolvedConfiguration, requireStatistics,
            ["broker-1", "broker-absent"]);

        Assert.Equal("publication_evidence_validation_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => fixture.LoadAsync(expectations))).Message);
    }

    [Theory]
    [InlineData("broker-absent")]
    public async Task Given_CaptureOnlyPredecessorDeclaresMissingBroker_When_Loading_Then_RejectsIt(string missingBroker)
    {
        BoundPublication fixture = await CreateBoundPublicationAsync("C1", independentBrokers: false, requireStatistics: true);
        JsonNode capabilities = JsonNode.Parse(fixture.Objects[fixture.First.PublicationPrefix + "capabilities.json"])!;
        capabilities["statistics_completion"]!["missing_brokers"] = new JsonArray(missingBroker);
        fixture.ReplaceJson("publication/capabilities.json", capabilities);
        fixture.Rehash();

        Assert.Equal("publication_evidence_validation_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => fixture.LoadAsync(fixture.SuccessorExpectations))).Message);
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

    private static async Task<BoundPublication> CreateBoundPublicationAsync(string phase, bool independentBrokers, bool requireStatistics)
    {
        RunIdentity firstIdentity = RestartFixture.First with { ConfigurationId = phase };
        RunIdentity secondIdentity = RestartFixture.Second with { ConfigurationId = phase };
        var first = new ProducerRunContext(firstIdentity, phase, 1, $"run-1-{phase.ToLowerInvariant()}-r1-loadgen", "pod-a", "uid-a", new string('a', 40));
        var successor = new ProducerRunContext(secondIdentity, phase, 1, first.FirstJobName + "2", "pod-b", "uid-b", first.SourceCommit);
        first.Validate();
        successor.Validate();
        RunEvidenceExpectations firstExpectations = await BoundExpectationsAsync(firstIdentity, independentBrokers, requireStatistics);
        RunEvidenceExpectations successorExpectations = await BoundExpectationsAsync(secondIdentity, independentBrokers, requireStatistics);
        (RunManifest predecessor, ImmutableArray<LedgerRow> predecessorRows) = RestartFixture.Predecessor();
        RunManifest manifest = predecessor with
        {
            Identity = firstIdentity,
            EvidenceFiles = PayloadNames.Skip(2).ToImmutableDictionary(name => name, name => name, StringComparer.Ordinal),
            GuardObservations = ["async_delivery_failure_observed", "possibly_persisted_observed", "request_lifecycle_capture_unsupported", "broker_lifecycle_inventory_unproven"]
        };
        ImmutableArray<LedgerRow> rows = [.. predecessorRows.Select(row => row with { Identity = firstIdentity })];
        var objects = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [first.PublicationPrefix + "ledger.jsonl"] = [.. rows.SelectMany(row => (byte[])[.. LedgerValidator.SerializeRow(row), (byte)'\n'])],
            [first.PublicationPrefix + "manifest.json"] = [.. LedgerValidator.SerializeManifest(manifest), (byte)'\n'],
            [first.RegistrationPath] = [.. BlobProducerRunLifecycle.SerializeRegistration(first), (byte)'\n'],
            [first.CompletionPath] = [.. BlobProducerRunLifecycle.SerializeCompletion(first), (byte)'\n']
        };
        string directory = Path.Combine(Path.GetTempPath(), "predecessor-bound-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var session = new RunEvidenceSession(firstIdentity,
                new RunEvidenceOptions(directory, 8, 64 * 1024, 256 * 1024, 16, TimeSpan.FromSeconds(2)), TimeProvider.System, firstExpectations))
            {
                var snapshot = new KafkaStatisticsSnapshot("native-producer", firstIdentity.ClientId, "producer", 11, 1, 1,
                    [new KafkaBrokerStatistics("broker-1", 1, "UP", 1, 0, 0, 0, 0, 0,
                        new KafkaWindowStatistics(0, null), new KafkaWindowStatistics(0, null))]);
                await session.WriteAsync(new KafkaStatisticsReport(
                    new KafkaStatisticsIdentity(firstIdentity.ScenarioId, firstIdentity.RunId, KafkaClientRole.LoadGenProducer, firstIdentity.ClientId),
                    "bound-loader-session", DateTimeOffset.UnixEpoch, "{}", snapshot, []), TestContext.Current.CancellationToken);
                session.CaptureLog(new LogMessage("native-producer", SyslogLevel.Info, "BROKER", "synthetic fixture"));
                await session.CompleteAsync(new KafkaStatisticsCompletion(true, [], [])
                {
                    DrainBoundaryMicroseconds = 10,
                    FirstPostDrainTimestampMicroseconds = independentBrokers ? null : 11
                }, TestContext.Current.CancellationToken);
                Assert.False(session.IsFaulted);
                await session.ValidatePersistedAsync(TestContext.Current.CancellationToken);
            }
            foreach (string name in PayloadNames.Skip(2))
                objects[first.PublicationPrefix + name] = await File.ReadAllBytesAsync(Path.Combine(directory, name), TestContext.Current.CancellationToken);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        var fixture = new BoundPublication(first, successor, successorExpectations, manifest, rows, objects);
        fixture.Rehash();
        Dictionary<string, byte[]> payloads = PayloadNames.ToDictionary(name => name,
            name => objects[first.PublicationPrefix + name], StringComparer.Ordinal);
        Assert.Equal(LedgerValidator.SerializeManifest(manifest), LedgerValidator.SerializeManifest(
            RunArtifactIndexReader.ValidatePayloadSemantics(firstIdentity, firstExpectations, payloads, 256 * 1024, TestContext.Current.CancellationToken)));
        await using var indexStream = new MemoryStream(objects[first.PublicationPrefix + "index.json"], writable: false);
        RunArtifactIndex index = await RunArtifactIndexReader.ReadAsync(indexStream, firstIdentity,
            256 * 1024, 256 * 1024, 1024 * 1024, TestContext.Current.CancellationToken);
        await RunArtifactIndexReader.ValidateArtifactSetAsync(index, 0, firstExpectations, [.. PayloadNames, "index.json"],
            (name, _) => ValueTask.FromResult<Stream>(new MemoryStream(payloads[name], writable: false)),
            256 * 1024, 1024 * 1024, TestContext.Current.CancellationToken);
        return fixture;
    }

    private static async Task<RunEvidenceExpectations> BoundExpectationsAsync(RunIdentity identity, bool independentBrokers, bool requireStatistics)
    {
        var services = new ServiceCollection();
        foreach (KafkaClientRole role in new[] { KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer })
            services.AddParchwellKafka(role, options =>
            {
                options.NamespaceName = "synthetic-namespace";
                options.ClientId = identity.ClientId;
            });
        using ServiceProvider provider = services.BuildServiceProvider();
        string? root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "Parchwell.slnx")))
            root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        var loader = new ScenarioLoader(new FileSystemScenarioSource(root), Options.Create(new ScenarioLoadOptions
        {
            MaxDocumentCharacters = 32 * 1024,
            MaxNestingDepth = 32,
            MaxScalarCharacters = 8 * 1024,
            MaxParserEvents = 20_000
        }));
        LoadedScenario loaded = await loader.LoadAsync("B10", TestContext.Current.CancellationToken);
        ResolvedRoleConfiguration selected = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>())
            .Resolve(loaded).Configurations[identity.ConfigurationId].Roles[KafkaClientRole.LoadGenProducer];
        return independentBrokers
            ? new RunEvidenceExpectations(selected, requireStatistics, ["broker-1"])
            : RunEvidenceExpectations.CaptureOnly(selected, requireStatistics);
    }

    private sealed record BoundPublication(ProducerRunContext First, ProducerRunContext Successor,
        RunEvidenceExpectations SuccessorExpectations, RunManifest Manifest, ImmutableArray<LedgerRow> Rows,
        Dictionary<string, byte[]> Objects)
    {
        public Task<RunArtifactSet> LoadAsync(RunEvidenceExpectations expectations) => PredecessorEvidenceLoader.LoadAsync(
            new Source(Objects), Successor, expectations, 256 * 1024, 1024 * 1024, TestContext.Current.CancellationToken);

        public void ReplaceJson(string name, JsonNode document)
        {
            string path = First.InstancePrefix + name;
            bool finalLf = Objects[path].Length > 0 && Objects[path][^1] == (byte)'\n';
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document);
            Objects[path] = finalLf ? [.. bytes, (byte)'\n'] : bytes;
        }

        public void Rehash()
        {
            ImmutableArray<BlobRunArtifactPublisher.ArtifactSource> sources = [.. PayloadNames.Select(name =>
                new BlobRunArtifactPublisher.ArtifactSource(name, name, null!, Objects[First.PublicationPrefix + name].LongLength,
                    Convert.ToHexString(SHA256.HashData(Objects[First.PublicationPrefix + name])).ToLowerInvariant()))];
            Objects[First.PublicationPrefix + "index.json"] = B10SecondWorkerTests.SerializePublicationIndex(
                new RunExecutionResult(0, Manifest, ImmutableDictionary<string, string>.Empty), sources);
        }
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
