#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Kafka.Configuration;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Tests.Cost;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.ProbeGate;
using Xunit;
using VerifierIdentity = Parchwell.Verifier.Common.Contracts.RunIdentity;

namespace Parchwell.Runner.Tests.Evidence;

/// <summary>Exercises dispatch-independent reads through actual immutable export receipts and worker codecs.</summary>
public sealed class ImmutablePhaseReplayTests
{
    private const string Commit = "1111111111111111111111111111111111111111";

    [Theory]
    [InlineData(16_777_215, true)]
    [InlineData(16_777_216, true)]
    [InlineData(16_777_217, false)]
    public async Task Given_ManifestResultSizeAroundWorkerLimit_When_ReadForReplay_Then_RejectsOversizeBeforeReadingContent(int size, bool mayRead)
    {
        var manifest = new EvidenceManifest(1, "env", [new("env-r1/result.json", size, new string('a', 64))]);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, LedgerJson.Options);
        var store = new ManifestOnlyStore(bytes);
        var exporter = new EvidenceExporter(store, TimeProvider.System);
        var receipt = new PrivateExportReceipt("A01/env/manifest.json", Convert.ToHexStringLower(SHA256.HashData(bytes)), 1, size);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => exporter.ReadVerifiedFileAsync("env", "env-r1/result.json", receipt,
            PrivateProvenanceCollector.MaxReceiptBytes, TestContext.Current.CancellationToken));
        Assert.Equal(mayRead, store.ContentRead);
        Assert.Equal(mayRead ? "private_evidence_file_missing" : "private_manifest_file_size_invalid", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_DestroyedExportedProbe_When_GateDispatchHasFreshOrTamperedWorkspace_Then_RehydratesOriginalsAndAcceptsLegacyGateReceipt(bool tamperedLocal)
    {
        await using Fixture fixture = await Fixture.CreateAsync("probe");
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Output));
        if (tamperedLocal) await fixture.WriteLocalResultsAsync([0]);
        IReadOnlyList<RepeatExecution> restored = await PhaseResultReplay.RestoreGateInputsAsync(fixture.Exporter, TimeProvider.System,
            fixture.Document, Commit, fixture.Output, TestContext.Current.CancellationToken);
        Assert.Equal(5, restored.Count);
        foreach (RepeatExecution repeat in restored)
            Assert.Equal(fixture.Originals[repeat.Identity.RunId], await File.ReadAllBytesAsync(Path.Combine(fixture.Output, repeat.Identity.RunId, "result.json"), TestContext.Current.CancellationToken));

        string limitsPath = Path.Combine(fixture.Output, "limits.json");
        await File.WriteAllTextAsync(limitsPath, "{\"verifier_shared\":{},\"verifier_gate\":{},\"scenario_parsing\":{}}", TestContext.Current.CancellationToken);
        using JsonDocument runtime = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(new
        { limits_file = limitsPath, scenario_root = "unused", verifier_executable = "synthetic-in-process-verifier" }));
        var process = new CodecGateProcess();
        var gate = new LocalVerifierGate(new ProbeGateRunner(process), runtime.RootElement);
        GateExecution executed = await gate.ExecuteAsync(restored, Commit, fixture.Output, TestContext.Current.CancellationToken);
        Assert.Equal(1, process.Calls);
        Assert.True(WorkerOutcome.HasReceipt(executed.Process, "probe-gate", null, Commit));
        Assert.Equal("pass", ProbeGateRunner.Inspect(executed, Commit, restored.Select(repeat => repeat.Identity).ToArray()).Outcome);
        // No completion receipt means no accepted gate even when the gate bytes themselves pass.
        Assert.Throws<InvalidDataException>(() => ProbeGateRunner.Inspect(executed with { Process = new(0, "", "") }, Commit,
            restored.Select(repeat => repeat.Identity).ToArray()));
    }

    [Fact]
    public async Task Given_CompletedPhaseAndLateActualCost_When_CandidateRebuiltInFreshWorkspace_Then_PreservesOriginalCountsAndVersions()
    {
        await using Fixture fixture = await Fixture.CreateAsync("C3");
        using JsonDocument before = await fixture.CandidateAsync(fixture.Executions);
        PhaseRecord phase = fixture.Document.Phases["C3"];
        fixture.Document.Phases["C3"] = phase with { ActualCostGbp = 0.65m };
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Output));
        using JsonDocument after = await fixture.CandidateAsync([]);
        Assert.Equal(before.RootElement.GetProperty("counts").GetRawText(), after.RootElement.GetProperty("counts").GetRawText());
        Assert.Equal(55, after.RootElement.GetProperty("counts").GetProperty("ok").GetInt64());
        Assert.Equal("2.16.0", after.RootElement.GetProperty("versions").GetProperty("confluent_kafka").GetString());
        Assert.Equal(0.65m, after.RootElement.GetProperty("costs").GetProperty("actual_gbp").GetDecimal());
        Assert.DoesNotContain("counts_unavailable", after.RootElement.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()));
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("missing")]
    [InlineData("partial")]
    [InlineData("foreign-export")]
    [InlineData("manifest-hash")]
    [InlineData("ledger-hash")]
    [InlineData("identity")]
    [InlineData("source")]
    [InlineData("duplicate-ordinal")]
    [InlineData("missing-ordinal")]
    public async Task Given_UnverifiedImmutableProbe_When_Replayed_Then_GateRejectsBeforeMaterializingInputs(string fault)
    {
        await using Fixture fixture = await Fixture.CreateAsync("probe", fault);
        await Assert.ThrowsAsync<InvalidDataException>(() => PhaseResultReplay.RestoreGateInputsAsync(fixture.Exporter, TimeProvider.System,
            fixture.Document, Commit, fixture.Output, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Output));
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("missing")]
    [InlineData("partial")]
    [InlineData("foreign-export")]
    [InlineData("manifest-hash")]
    [InlineData("ledger-hash")]
    [InlineData("identity")]
    [InlineData("source")]
    [InlineData("duplicate-ordinal")]
    [InlineData("missing-ordinal")]
    public async Task Given_UnverifiedImmutablePhaseAndLocalOriginals_When_CandidateReplayed_Then_EveryCountStaysUnavailable(string fault)
    {
        await using Fixture fixture = await Fixture.CreateAsync("C3", fault);
        await fixture.WriteLocalResultsAsync();
        using JsonDocument candidate = await fixture.CandidateAsync([]);
        Assert.All(candidate.RootElement.GetProperty("counts").EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind));
        Assert.Contains("counts_unavailable", candidate.RootElement.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()));
    }

    private sealed class CodecGateProcess : IProcessRunner
    {
        internal int Calls { get; private set; }
        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal(TimeSpan.FromSeconds(90), request.Timeout);
            IReadOnlyDictionary<string, string> environment = request.Environment!;
            var results = new List<VerifierResult>();
            for (int index = 0; index < 5; index++)
            {
                string path = environment["Verifier__RepeatResultPaths__" + index.ToString(CultureInfo.InvariantCulture)];
                byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                var parsed = VerifierResultCodec.Read(bytes, path, new InputLimits(32, 16777216, 16777216, 65536, 100, 1000, 2048), 100);
                Assert.True(parsed.IsSuccess);
                results.Add(parsed.Value!);
            }
            var rule = new Parchwell.Scenarios.ProbeGate("C0", new("stats.broker.throttle.max", KafkaClientRole.LoadGenProducer),
                Parchwell.Scenarios.ProbeOperator.GreaterThan, 20000, 5, ["C0", "C3"]);
            ProbeGateResult gate = ProbeGateEvaluator.Evaluate(new(rule, results.Select(result => result.Identity).ToArray(),
                results.Select((result, index) => new ProbeGateInput("replayed-" + index, ProbeGateInputStatus.Readable, result)).ToArray()), Commit);
            await File.WriteAllBytesAsync(environment["Verifier__GateOutputPath"], ProbeGateResultCodec.Write(gate, 16777216), cancellationToken);
            return new(0, Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("probe-gate", null, Commit)), "");
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "immutable-replay-" + Guid.NewGuid().ToString("N"));
        private readonly PersistentTestStore store = new();
        private Fixture(string phaseName) { PhaseName = phaseName; Exporter = new(store, TimeProvider.System); }
        private string PhaseName { get; }
        internal string Output => Path.Combine(root, "fresh-dispatch");
        internal EvidenceExporter Exporter { get; }
        internal LedgerDocument Document { get; private set; } = null!;
        internal RepeatExecution[] Executions { get; private set; } = [];
        internal Dictionary<string, byte[]> Originals { get; } = new(StringComparer.Ordinal);

        internal static async Task<Fixture> CreateAsync(string phaseName, string? fault = null)
        {
            var fixture = new Fixture(phaseName);
            Directory.CreateDirectory(fixture.Output);
            string source = Path.Combine(fixture.root, "original-dispatch");
            Directory.CreateDirectory(source);
            var executions = new List<RepeatExecution>();
            var records = new List<RepeatRecord>();
            var files = new List<EvidenceFile>();
            for (int ordinal = 1; ordinal <= 5; ordinal++)
            {
                string run = "env-r" + ordinal.ToString(CultureInfo.InvariantCulture);
                var identity = new VerifierIdentity("A01", phaseName == "probe" ? "C0" : phaseName, run, "pod", "pod", "pod");
                var worker = new WorkerIdentity(identity.ScenarioId, identity.ConfigurationId, run, "pod", "pod", "pod");
                var result = new VerifierResult(1, identity, "1", new Dictionary<string, string>(StringComparer.Ordinal)
                    { ["workload_generator"] = "1", ["confluent_kafka"] = "2.16.0", ["librdkafka"] = "2.16.0", ["dotnet"] = "10.0.13" },
                    Commit, "2.16.0", "2.16.0", VerifierResultStatus.CompleteValid, [], null, null, null, null,
                    new(11, 2, 3, 4, 5, 6), null, null, phaseName == "probe" ? new(true, ordinal == 1, "broker", 1, 1, ordinal == 1 ? 20001 : 20000) : null,
                    null, [], []);
                byte[] bytes = VerifierResultCodec.Write(result, new(16777216, 100, 2048));
                AcceptedWorkerOutcome outcome = WorkerOutcome.Reconcile(new(0, Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("verify", identity, Commit, 0)), ""), bytes, worker, Commit);
                Assert.True(outcome.ExecutionCompleted);
                Assert.True(outcome.CountsAccepted);
                executions.Add(new(worker, outcome, new(new Dictionary<string, byte[]>()), bytes, []));
                if (ordinal == 1 && fault is ("identity" or "source"))
                {
                    JsonNode changed = JsonNode.Parse(bytes)!;
                    if (fault == "identity") changed["identity"]!["run_id"] = "foreign-r1";
                    else changed["verifier_commit"] = new string('2', 40);
                    bytes = JsonSerializer.SerializeToUtf8Bytes(changed);
                }
                fixture.Originals.Add(run, bytes);
                records.Add(new(ordinal, run, "completed", "pod", ResultHash: Convert.ToHexStringLower(SHA256.HashData(bytes)), Outcome: "complete_valid", Reason: "complete_valid"));
                string path = Path.Combine(source, run + ".json");
                await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
                files.Add(new(run + "/result.json", path));
            }
            PrivateExportReceipt receipt = await fixture.Exporter.ExportAsync("env", files, TestContext.Current.CancellationToken);
            Directory.Delete(source, true);
            if (fault == "tampered") fixture.store.Objects[("evidence", "A01/env/env-r1/result.json")] = new([0], "tampered");
            if (fault == "missing") fixture.store.Objects.Remove(("evidence", "A01/env/env-r1/result.json"));
            if (fault == "partial") receipt = receipt with { Complete = false };
            if (fault == "foreign-export") receipt = receipt with { ManifestPath = "A01/foreign/manifest.json" };
            if (fault == "manifest-hash") receipt = receipt with { ManifestSha256 = new string('f', 64) };
            if (fault == "ledger-hash") records[0] = records[0] with { ResultHash = new string('f', 64) };
            if (fault == "duplicate-ordinal") records[4] = records[0];
            if (fault == "missing-ordinal") records.RemoveAt(4);
            var started = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
            var phase = new PhaseRecord("destroyed", "reservation", "env", 1, 1, 2, null, null, started, started.AddHours(4),
                records.ToArray(), receipt, new("env", started.AddMinutes(1), true, "destroyed"), null);
            fixture.Document = new(1, "A01", "GBP", 1, Commit, 0, [], new(StringComparer.Ordinal)
            { [phaseName] = phase, ["probe"] = phase with { GateReceipt = new("pass", new string('a', 64), Commit) } });
            fixture.Executions = executions.ToArray();
            return fixture;
        }

        internal async Task WriteLocalResultsAsync(byte[]? replace = null)
        {
            foreach ((string run, byte[] original) in Originals)
            {
                string directory = Path.Combine(Output, run);
                Directory.CreateDirectory(directory);
                await File.WriteAllBytesAsync(Path.Combine(directory, "result.json"), replace ?? original, TestContext.Current.CancellationToken);
            }
        }

        internal async Task<JsonDocument> CandidateAsync(RepeatExecution[] executions)
        {
            using JsonDocument runtime = JsonDocument.Parse("{\"subscription_guid\":\"01234567-89ab-cdef-0123-456789abcdef\"}");
            var candidate = new PhaseCandidate(Exporter, TimeProvider.System);
            return JsonDocument.Parse(await candidate.CreateAsync(runtime.RootElement, Document, PhaseName, "completed", executions, Output,
                TestContext.Current.CancellationToken));
        }

        public ValueTask DisposeAsync() { Directory.Delete(root, true); return ValueTask.CompletedTask; }
    }

    private sealed class ManifestOnlyStore(byte[] manifest) : IArtifactStore
    {
        internal bool ContentRead { get; private set; }
        public Task<StoredArtifact?> ReadAsync(string container, string path, int maxBytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (path == "A01/env/manifest.json") return Task.FromResult<StoredArtifact?>(new(manifest, "manifest"));
            ContentRead = true;
            return Task.FromResult<StoredArtifact?>(null);
        }

        public Task<ArtifactWriteOutcome> WriteAsync(string container, string path, ReadOnlyMemory<byte> bytes, string? expectedETag,
            bool createOnly, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
