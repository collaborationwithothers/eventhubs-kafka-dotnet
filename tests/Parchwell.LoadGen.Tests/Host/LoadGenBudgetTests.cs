using System.Text.Json;
using System.Collections.Immutable;
using Confluent.Kafka;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Restart;
using Parchwell.LoadGen.Run.Workload;
using Parchwell.Scenarios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Telemetry;
using Parchwell.LoadGen;
using Parchwell.LoadGen.Run.Input;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class LoadGenBudgetTests
{
    [Fact]
    public void Given_RuntimeLimitsAndBoundWorkerOptions_When_ComputingProcessEnvelope_Then_PreservesApprovedWorkerAndJobBounds()
    {
        string root = FindRepositoryRoot();
        using JsonDocument limits = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "docs/operations/a01-runtime-limits.json")));
        JsonElement loadgen = limits.RootElement.GetProperty("loadgen");
        JsonElement execution = limits.RootElement.GetProperty("execution");
        string output = Path.Combine(Path.GetTempPath(), "loadgen-budget-" + Guid.NewGuid().ToString("N"));
        Dictionary<string, string> values = LoadGenHostProcessTests.CreateInputs(output);
        values["LoadGen__ProducerDrainTimeout"] = loadgen.GetProperty("ProducerDrainTimeout").GetString()!;
        values["LoadGen__LedgerDrainTimeout"] = loadgen.GetProperty("LedgerDrainTimeout").GetString()!;
        values["LoadGen__StatisticsCompletionTimeout"] = loadgen.GetProperty("StatisticsCompletionTimeout").GetString()!;
        values["LoadGen__EvidenceCompletionTimeout"] = loadgen.GetProperty("EvidenceCompletionTimeout").GetString()!;
        values["LoadGen__CleanupTimeout"] = loadgen.GetProperty("CleanupTimeout").GetString()!;
        values["LoadGen__UploadTimeout"] = loadgen.GetProperty("UploadTimeout").GetString()!;
        values["LoadGen__ProcessLifetimeTimeout"] = loadgen.GetProperty("ProcessLifetimeTimeout").GetString()!;
        string[] args = values.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();

        using IHost host = Program.BuildHostCore(args, _ => { }, (_, configure) => configure(new ParchwellTelemetryOptions()));
        LoadGenHostOptions options = host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value;

        int finalisation =
            300 + // Producer drain
            10 + // Ledger drain
            20 + // Statistics completion
            10 + // Native producer disposal
            20 + // Evidence writer drain
            20 + // Capabilities save
            10 + // Evidence readback
            10 + // Ledger readback
            10 + // Manifest save
            30 + // Normal upload
            5 * 10 + // Five bounded diagnostic snapshots
            30 + // Diagnostic upload
            10 + // Session cleanup
            10 + // Host stop/disposal
            10 + // Watchdog cleanup reserve
            10;  // Completion upload
        int completePath = 10 + 40 + 240 + finalisation; // Registration, setup/resolution, offered schedule, finalisation.

        Assert.Equal(TimeSpan.FromSeconds(300), options.ProducerDrainTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), options.LedgerDrainTimeout);
        Assert.Equal(TimeSpan.FromSeconds(20), options.StatisticsCompletionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(20), options.EvidenceCompletionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), options.CleanupTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.UploadTimeout);
        Assert.Equal(TimeSpan.FromSeconds(900), options.ProcessLifetimeTimeout);
        Assert.Equal(560, finalisation);
        Assert.Equal(850, completePath);
        Assert.True(completePath <= execution.GetProperty("loadgen_seconds").GetInt32());
        Assert.Equal(900, execution.GetProperty("loadgen_seconds").GetInt32());
        Assert.Equal(1140, execution.GetProperty("loadgen_job_seconds").GetInt32());
        Assert.Equal(1140, execution.GetProperty("loadgen_seconds").GetInt32() +
            execution.GetProperty("termination_grace_seconds").GetInt32() + 210);
        Assert.Equal(1500, execution.GetProperty("repeat_seconds").GetInt32());
        Assert.Equal(9750, execution.GetProperty("phase_seconds").GetInt32());
        Assert.Equal(240, execution.GetProperty("phase_job_minutes").GetInt32());
    }

    [Fact]
    public void Given_B10AndPredecessorDiscovery_When_ComputingConfiguredEnvelope_Then_BothFitProcessLifetime()
    {
        using JsonDocument limits = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FindRepositoryRoot(), "docs/operations/a01-runtime-limits.json")));
        JsonElement loadgen = limits.RootElement.GetProperty("loadgen");
        int discovery = (int)TimeSpan.Parse(loadgen.GetProperty("UploadTimeout").GetString()!, System.Globalization.CultureInfo.InvariantCulture).TotalSeconds;
        int process = limits.RootElement.GetProperty("execution").GetProperty("loadgen_seconds").GetInt32();
        Assert.Equal(30, discovery);
        Assert.Equal(880, 850 + discovery);
        Assert.True(850 + discovery <= process);
        int b10 = 10 + 40 + 40 + 560 + discovery;
        Assert.Equal(680, b10);
        Assert.True(b10 <= process);
        Assert.Equal(900, process);
        Assert.Equal(186000, loadgen.GetProperty("MaxAttempts").GetInt32());
        Assert.True(loadgen.GetProperty("MaxAttempts").GetInt32() >= 62000);
    }

    [Fact]
    public async Task Given_FullB10RetryAndRestartInventory_When_UsingProductionSerializers_Then_AllConfiguredByteAndReaderBudgetsFit()
    {
        string root = FindRepositoryRoot();
        using JsonDocument limits = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "docs/operations/a01-runtime-limits.json")));
        JsonElement budgets = limits.RootElement.GetProperty("loadgen");
        JsonElement verifier = limits.RootElement.GetProperty("verifier_verify");
        var loader = new ScenarioLoader(new FileSystemScenarioSource(root), Options.Create(new ScenarioLoadOptions
        {
            MaxDocumentCharacters = 1048576, MaxNestingDepth = 32, MaxScalarCharacters = 65536, MaxParserEvents = 100000
        }));
        LoadedScenario loaded = await loader.LoadAsync("B10", TestContext.Current.CancellationToken);
        var first = new RunIdentity("B10", "C1", new string('r', 29), new string('p', 63), new string('p', 63), new string('p', 63));
        var second = first with { InstanceId = new string('q', 63), ExpectedInstanceId = new string('q', 63), ClientId = new string('q', 63) };
        var attempts = ImmutableArray.CreateBuilder<AttemptEntry>(62000);
        var rows = ImmutableArray.CreateBuilder<LedgerRow>(62000);
        foreach (PlannedEvent planned in WorkloadGenerator.Generate(loaded.Profile, 31000, 900000, TestContext.Current.CancellationToken))
        {
            for (int retry = 0; retry < 2; retry++)
            {
                long ordinal = attempts.Count;
                var attempt = new AttemptEntry(first.InstanceId, first.InstanceId + "-" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), ordinal,
                    planned.TenantId, planned.DocumentId, planned.Key, planned.Sequence, planned.EventType, planned.DueMicroseconds, planned.DueMicroseconds + 10);
                attempts.Add(attempt);
                rows.Add(new LedgerRow(1, first, attempt, "callback", false, (int)ErrorCode.Local_MsgTimedOut, ErrorCode.Local_MsgTimedOut.ToString(),
                    (int)PersistenceStatus.PossiblyPersisted, "native_callback", attempt.InvokedMicroseconds + 5, 0.005, null));
            }
        }
        var manifest = new RunManifest(1, first, "1", 31000, attempts.Count, attempts.ToImmutable(), 1000000, 0, "complete", true, null, [],
            ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, string>.Empty, []);
        RestartInstance restart = RestartInstance.Create(second, manifest, rows.ToImmutable());
        Assert.Equal(62000, attempts.Count);
        Assert.Equal(31000, restart.Plan.Planned.Length);
        var secondAttempts = restart.Plan.Planned.Select(planned => new AttemptEntry(second.InstanceId,
            second.InstanceId + "-" + planned.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), planned.Ordinal, planned.TenantId, planned.DocumentId,
            planned.Key, planned.Sequence, planned.EventType, planned.PlannedMicroseconds, planned.PlannedMicroseconds + 10)).ToImmutableArray();
        RunManifest secondManifest = manifest with { Identity = second, PlannedAttemptCount = 31000, AttemptCount = secondAttempts.Length, Inventory = secondAttempts, Restart = restart.Plan };
        ImmutableArray<LedgerRow> secondRows = [.. secondAttempts.Select(attempt => rows[0] with { Identity = second, Attempt = attempt, CompletedMicroseconds = attempt.InvokedMicroseconds + 5 })];
        long acceptedBytes = 0;
        // B10PhaseRegistrationReader charges all eight listed producer objects per instance.
        long acceptedEntries = 2 * 8;
        foreach ((RunManifest current, ImmutableArray<LedgerRow> currentRows) in new[] { (manifest, rows.ToImmutable()), (secondManifest, secondRows) })
        {
            LedgerValidator.Validate(currentRows, current, current.Identity);
            long ledgerBytes = 0;
            foreach (LedgerRow row in currentRows)
            {
                byte[] serialized = LedgerValidator.SerializeRow(row, budgets.GetProperty("MaxRowBytes").GetInt32());
                Assert.True(serialized.Length <= 16384);
                AssertDocumentBounds(serialized, verifier);
                ledgerBytes += serialized.LongLength + 1; // The persisted ledger appends LF to every row.
            }
            byte[] serializedManifest = LedgerValidator.SerializeManifest(current, budgets.GetProperty("MaxManifestBytes").GetInt32());
            AssertDocumentBounds(serializedManifest, verifier);
            using JsonDocument manifestDocument = JsonDocument.Parse(serializedManifest);
            acceptedEntries += currentRows.Length + ManifestEntries(manifestDocument.RootElement);
            long manifestBytes = serializedManifest.LongLength + 1;
            Assert.True(ledgerBytes <= 268435456);
            Assert.True(manifestBytes <= 67108864);
            (long aggregate, byte[] indexBytes) = B10SecondWorkerTests.PublicationCapacity(current, ledgerBytes, manifestBytes,
                budgets.GetProperty("MaxEvidenceFileBytes").GetInt64());
            AssertDocumentBounds(indexBytes, verifier);
            Assert.True(indexBytes.LongLength <= verifier.GetProperty("MaxArtifactBytes").GetInt64());
            Assert.True(aggregate <= 1073741824);
            Assert.True(aggregate <= budgets.GetProperty("MaxAggregateArtifactBytes").GetInt64());
            Dictionary<string, byte[]> supporting = await SupportingEvidenceAsync(current.Identity, loaded);
            foreach ((string name, byte[] bytes) in supporting)
            {
                Assert.True(bytes.LongLength <= budgets.GetProperty("MaxEvidenceFileBytes").GetInt64());
                if (name.EndsWith(".jsonl", StringComparison.Ordinal))
                {
                    int start = 0;
                    long lines = 0;
                    for (int offset = 0; offset < bytes.Length; offset++)
                    {
                        if (bytes[offset] != (byte)'\n') continue;
                        byte[] line = bytes[start..offset];
                        Assert.True(line.Length <= verifier.GetProperty("MaxRowBytes").GetInt32());
                        AssertDocumentBounds(line, verifier);
                        using JsonDocument envelope = JsonDocument.Parse(line);
                        acceptedEntries += EvidenceEntries(envelope.RootElement);
                        if (name == "statistics.jsonl")
                        {
                            byte[] nativeBytes = System.Text.Encoding.UTF8.GetBytes(envelope.RootElement.GetProperty("raw_json").GetString()!);
                            Assert.True(nativeBytes.Length <= verifier.GetProperty("MaxNativeReportBytes").GetInt64());
                            AssertDocumentBounds(nativeBytes, verifier);
                            using JsonDocument native = JsonDocument.Parse(nativeBytes);
                            int brokers = native.RootElement.GetProperty("brokers").EnumerateObject().Count();
                            Assert.True(brokers <= verifier.GetProperty("MaxBrokerEntries").GetInt64());
                            acceptedEntries += brokers;
                        }
                        lines++;
                        start = offset + 1;
                    }
                    Assert.Equal(bytes.Length, start);
                    Assert.True(lines <= verifier.GetProperty("MaxRowsPerArtifact").GetInt64());
                    acceptedEntries += lines;
                }
                else
                {
                    AssertDocumentBounds(bytes, verifier);
                    using JsonDocument capabilities = JsonDocument.Parse(bytes);
                    acceptedEntries += EvidenceEntries(capabilities.RootElement);
                }
            }
            // Each production publication index charges five descriptors to the shared InputBudget.
            acceptedEntries += 5;
            acceptedBytes += aggregate;
            Assert.True(current.Inventory.Length <= verifier.GetProperty("MaxInventoryEntries").GetInt64());
            Assert.True(currentRows.Length <= verifier.GetProperty("MaxRowsPerArtifact").GetInt64());
            Assert.True(Math.Max(ledgerBytes, manifestBytes) <= verifier.GetProperty("MaxArtifactBytes").GetInt64());
            TestContext.Current.TestOutputHelper?.WriteLine($"Synthetic {current.Identity.InstanceId[0]}: rows={currentRows.Length}, ledger_with_lf={ledgerBytes}, manifest_with_lf={manifestBytes}, publication_with_index_and_full_evidence_allowances={aggregate}");
        }
        // The exact synthetic complete publications charge rows, manifest arrays/maps, index descriptors,
        // evidence arrays/settings maps and native broker entries, rather than every JSON property.
        Assert.True(acceptedEntries <= verifier.GetProperty("MaxInputEntries").GetInt64());
        TestContext.Current.TestOutputHelper?.WriteLine($"Synthetic per-repeat shared input entries={acceptedEntries}; publication allowance bytes={acceptedBytes}");
        Assert.True(acceptedBytes <= verifier.GetProperty("MaxTotalInputBytes").GetInt64());
        Assert.True(93000 <= verifier.GetProperty("MaxRowsPerArtifact").GetInt64());
    }

    private static void AssertDocumentBounds(byte[] bytes, JsonElement verifier)
    {
        // Mirrors StrictJson.Preflight: properties and immediate array elements count once each;
        // this ceiling applies independently to each document, and JSONL rows are separate documents.
        var reader = new Utf8JsonReader(bytes);
        var containers = new Stack<JsonTokenType>();
        long entries = 0;
        int deepest = 0;
        while (reader.Read())
        {
            bool starts = reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray;
            deepest = Math.Max(deepest, reader.CurrentDepth + (starts ? 1 : 0));
            if (reader.TokenType == JsonTokenType.PropertyName || containers.Count > 0 && containers.Peek() == JsonTokenType.StartArray &&
                reader.TokenType is not JsonTokenType.EndArray and not JsonTokenType.EndObject) entries++;
            if (starts) containers.Push(reader.TokenType);
            else if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray) containers.Pop();
        }
        Assert.True(entries <= verifier.GetProperty("MaxInputEntries").GetInt64());
        Assert.True(deepest <= 32);
    }

    private static long ManifestEntries(JsonElement manifest)
    {
        long entries = manifest.GetProperty("inventory").GetArrayLength();
        foreach (string name in new[] { "diagnostics", "guard_observations" }) entries += manifest.GetProperty(name).GetArrayLength();
        foreach (string name in new[] { "runtime_versions", "evidence_files" }) entries += manifest.GetProperty(name).EnumerateObject().Count();
        if (manifest.TryGetProperty("restart", out JsonElement restart)) entries += restart.GetProperty("planned").GetArrayLength();
        return entries;
    }

    private static long EvidenceEntries(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.GetArrayLength() + element.EnumerateArray().Sum(EvidenceEntries);
        if (element.ValueKind != JsonValueKind.Object) return 0;
        long entries = 0;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            // CapturedEvidenceReader.ReadMap only charges resolved_configuration.settings maps.
            if (property.Name == "settings") entries += property.Value.EnumerateObject().Count();
            entries += EvidenceEntries(property.Value);
        }
        return entries;
    }

    private static async Task<Dictionary<string, byte[]>> SupportingEvidenceAsync(RunIdentity identity, LoadedScenario loaded)
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = identity.ClientId;
            options.EnrichmentGroupId = "synthetic-unused-group";
        });
        services.AddParchwellKafka(KafkaClientRole.VerifierConsumer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-verifier";
            options.EnrichmentGroupId = "synthetic-unused-group";
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        ResolvedScenario resolved = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
        var expectations = RunEvidenceExpectations.CaptureOnly(resolved.Configurations[identity.ConfigurationId].Roles[KafkaClientRole.LoadGenProducer], true);
        string directory = Path.Combine(Path.GetTempPath(), "b10-capacity-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var session = new RunEvidenceSession(identity, new RunEvidenceOptions(directory, 8, 1048576, 134217728, 100, TimeSpan.FromSeconds(2)), TimeProvider.System, expectations))
            {
                string raw = "{\"name\":\"synthetic-producer\",\"client_id\":\"" + identity.ClientId + "\",\"type\":\"producer\",\"ts\":2,\"time\":1,\"age\":0,\"brokers\":{\"broker-1\":{\"name\":\"broker-1\",\"nodeid\":1,\"state\":\"UP\",\"stateage\":1,\"req_timeouts\":0,\"disconnects\":0,\"txretries\":0,\"txerrs\":0,\"rxerrs\":0,\"throttle\":{\"cnt\":1,\"max\":0},\"rtt\":{\"cnt\":0,\"p99\":0}}}}";
                var snapshot = new KafkaStatisticsSnapshot("synthetic-producer", identity.ClientId, "producer", 2, 1, 0,
                    [new KafkaBrokerStatistics("broker-1", 1, "UP", 1, 0, 0, 0, 0, 0, new KafkaWindowStatistics(1, 0), new KafkaWindowStatistics(0, 0))]);
                await session.WriteAsync(new KafkaStatisticsReport(new KafkaStatisticsIdentity(identity.ScenarioId, identity.RunId, KafkaClientRole.LoadGenProducer,
                    identity.ClientId), "synthetic-capacity", DateTimeOffset.UnixEpoch, raw, snapshot, []), TestContext.Current.CancellationToken);
                await session.CompleteAsync(new KafkaStatisticsCompletion(true, [], []) { DrainBoundaryMicroseconds = 1, FirstPostDrainTimestampMicroseconds = 2 }, TestContext.Current.CancellationToken);
            }
            var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string name in new[] { "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json" })
                bytes[name] = await File.ReadAllBytesAsync(Path.Combine(directory, name), TestContext.Current.CancellationToken);
            return bytes;
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs/operations/a01-runtime-limits.json"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository_root_not_found");
    }
}
#pragma warning restore CA1707
