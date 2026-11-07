using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Judge;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Publication;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707
/// <summary>Exercises scientific readers against fresh diagnostic publisher output, with no stored evidence fallback.</summary>
public sealed class B10AbruptRejectionTests
{
    private static readonly InputLimits Limits = new(32, 268435456, 1073741824, 16384, 186000, 186000, 256);
    private static readonly string[] ExportNames =
    [
        "first/registration.json", "first/journal.jsonl", "first/timeout-seal.json", "first/control-seal.json",
        "first/kill-selection.json", "first/abrupt-manifest.json", "first/termination.json",
        "successor/registration.json", "successor/ledger.jsonl", "successor/manifest.json",
        "successor/statistics.jsonl", "successor/request-lifecycle.jsonl", "successor/capabilities.json", "successor/completion.json"
    ];

    [Fact]
    public async Task Given_ProductionDiagnosticBytes_When_CopiedOrRelabelled_Then_AllScientificAdmissionRejects()
    {
        string directory = Path.Combine(Path.GetTempPath(), "parchwell-b10-abrupt-rejection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await ProduceExportAsync(directory);
            Dictionary<string, byte[]> files = ReadExport(directory);
            foreach (string name in new[] { "first/registration.json", "first/abrupt-manifest.json", "first/termination.json",
                "first/kill-selection.json", "successor/registration.json", "successor/manifest.json", "successor/completion.json" })
            {
                byte[] original = files[name];
                using JsonDocument document = JsonDocument.Parse(original);
                Assert.Equal(2, document.RootElement.GetProperty("schema_version").GetInt32());
                Assert.Equal("b10_abrupt", document.RootElement.GetProperty("dialect").GetString());
                Assert.False(document.RootElement.GetProperty("scientific_admissible").GetBoolean());
                foreach (byte[] bytes in Variants(original))
                {
                    Assert.ThrowsAny<FormatException>(() => PhaseWireCodec.ReadRegistration(bytes));
                    Assert.ThrowsAny<FormatException>(() => PhaseWireCodec.ReadProducerCompletion(bytes));
                    Assert.False(ArtifactIndexReader.Read(bytes, Limits, new InputBudget(Limits)).IsSuccess);
                    Assert.False(ManifestReader.Read(bytes, Limits, new InputBudget(Limits), 186000).IsSuccess);
                    Assert.ThrowsAny<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(bytes, Limits));
                    Assert.ThrowsAny<FormatException>(() => PhaseCandidateCodec.Read(bytes));
                    AssertPublicationRejected(files, bytes);
                }
            }
            foreach (string variant in new[] { "unchanged", "remove-purpose", "schema-one", "mixed-source", "mixed-image", "mixed-dialect" })
                await AssertIntakeRejectedAsync(files, variant);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Given_SchemaOneProductionWriters_When_RoundTripped_Then_LegacyReadersRemainUsable()
    {
        const string run = "synthetic-abrupt";
        string job = PhaseIdentity.JobName(run, "C1", 1);
        var identity = new RunIdentity("B10", "C1", run, "synthetic-first", "synthetic-first", "synthetic-client");
        var registration = new ProducerRegistration(1, identity, "C1", 1, job, "synthetic-first", "synthetic-first-uid", new string('a', 40));
        Assert.Equal(registration, PhaseWireCodec.ReadRegistration(PhaseWireCodec.WriteRegistration(registration)));
        var completion = new ProducerCompletionReceipt(1, identity, "C1", 1, job, registration.PodName,
            registration.PodUid, registration.SourceCommit, "loadgen", 0, true);
        Assert.Equal(completion, PhaseWireCodec.ReadProducerCompletion(PhaseWireCodec.WriteProducerCompletion(completion)));
        ArtifactReference Ref(string path) => new("verifier", path, 1, new string('a', 64));
        var finalization = new B10Finalization(1, "B10", "C1", run, "C1", registration.SourceCommit,
            Ref(PhasePaths.SweepLine(run, "C1")), true, B10SweepEvidence.ResidualRisk,
            Enumerable.Range(1, 5).Select(i => new B10RepeatFinalization(i, Ref(PhasePaths.Result(run, "C1", i)),
                Ref(PhasePaths.VerifyCompletion(run, "C1", i)), true, VerifierResultStatus.CompleteValid, new(1, 0, 0, 0, 0, 0), [])).ToArray());
        byte[] bytes = B10SweepEvidenceCodec.WriteFinalization(finalization);
        Assert.Equal(bytes, B10SweepEvidenceCodec.WriteFinalization(B10SweepEvidenceCodec.ReadFinalization(bytes)));
    }

    private static async Task ProduceExportAsync(string directory)
    {
        string b = AppContext.BaseDirectory;
        string configuration = new DirectoryInfo(b).Parent!.Name;
        string assembly = Path.GetFullPath(Path.Combine(b, "..", "..", "..", "..", "Parchwell.LoadGen.Tests", "bin", configuration,
            "net10.0", "Parchwell.LoadGen.Tests.dll"));
        Assert.True(File.Exists(assembly), "loadgen_test_assembly_missing:" + assembly);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(assembly);
        start.ArgumentList.Add("-class");
        start.ArgumentList.Add("Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests");
        start.Environment["PARCHWELL_B10_ABRUPT_WIRE_OUTPUT"] = directory;
        using Process child = Process.Start(start) ?? throw new InvalidOperationException("loadgen_test_worker_start_failed");
        Task<string> stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try { await child.WaitForExitAsync(timeout.Token); }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                Assert.True(child.WaitForExit(10000), "loadgen_test_worker_reap_failed");
            }
        }
        Assert.True(child.ExitCode == 0, (await stdout) + (await stderr));
    }

    private static Dictionary<string, byte[]> ReadExport(string directory)
    {
        string exportPath = Path.Combine(directory, "export.json");
        Assert.True(File.Exists(exportPath), "b10_abrupt_wire_export_missing:" + exportPath);
        using JsonDocument export = JsonDocument.Parse(File.ReadAllBytes(exportPath));
        JsonElement[] entries = export.RootElement.GetProperty("files").EnumerateArray().ToArray();
        string[] names = entries.Select(entry => entry.GetProperty("name").GetString()!).ToArray();
        Assert.Equal(ExportNames.Order(StringComparer.Ordinal), names);
        Assert.Equal(names.Append("export.json").Order(StringComparer.Ordinal),
            Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/')).Order(StringComparer.Ordinal));
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (JsonElement entry in entries)
        {
            string name = entry.GetProperty("name").GetString()!;
            byte[] bytes = File.ReadAllBytes(Path.Combine(directory, name));
            Assert.Equal(entry.GetProperty("length").GetInt64(), bytes.LongLength);
            Assert.Equal(entry.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
            files.Add(name, bytes);
        }
        return files;
    }

    private static IEnumerable<byte[]> Variants(byte[] bytes)
    {
        foreach (string variant in new[] { "unchanged", "remove-purpose", "schema-one", "mixed-source", "mixed-image", "mixed-dialect" })
            yield return Change(bytes, variant);
    }

    private static byte[] Change(byte[] bytes, string variant)
    {
        if (variant == "unchanged") return bytes;
        JsonObject root = JsonNode.Parse(bytes)!.AsObject();
        switch (variant)
        {
            case "remove-purpose": root.Remove("purpose"); break;
            case "schema-one": root["schema_version"] = 1; break;
            case "mixed-source": root["identity"]!["source_commit"] = new string('b', 40); break;
            case "mixed-image": root["identity"]!["image_reference"] = "synthetic.invalid/worker@sha256:" + new string('b', 64); break;
            case "mixed-dialect": root["dialect"] = "legacy"; break;
            default: throw new ArgumentOutOfRangeException(nameof(variant));
        }
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static void AssertPublicationRejected(Dictionary<string, byte[]> files, byte[] diagnostic)
    {
        // Even copying diagnostic bytes to the complete ordinary path set cannot make its closed parsers accept them.
        const string run = "synthetic-abrupt";
        const string phase = "C1";
        string job = PhaseIdentity.JobName(run, phase, 1);
        var identity = new RunIdentity("B10", phase, run, "synthetic-first", "synthetic-first", "synthetic-client");
        var registration = new ProducerRegistration(1, identity, phase, 1, job, identity.InstanceId, "synthetic-uid", new string('a', 40));
        string prefix = PhasePaths.ProducerPrefix(run, phase, 1, identity.InstanceId);
        var payloads = PublicationAcceptance.PublicationNames.ToDictionary(name => name,
            name => (byte[]?)(files.TryGetValue("successor/" + name, out byte[]? payload) ? payload : diagnostic), StringComparer.Ordinal);
        payloads["index.json"] = diagnostic;
        payloads["manifest.json"] = diagnostic;
        ArtifactIndex? index = ArtifactIndexReader.Read(diagnostic, Limits, new InputBudget(Limits)).Value;
        SealedRunManifest? manifest = ManifestReader.Read(diagnostic, Limits, new InputBudget(Limits), 186000).Value;
        var expectedStart = new RunIdentity("B10", phase, run, job, job, job);
        var start = new StartLineNote(1, expectedStart, "document-events", [], "2026-11-07T00:00:00.0000000+00:00",
            registration.SourceCommit, "synthetic", "synthetic") { ExpectedJobNames = [job, PhaseIdentity.RestartJobName(run, phase, 1)] };
        var inventory = new BrokerInventory(1, expectedStart, [], "kafka_metadata", registration.SourceCommit);
        var completion = new ProducerCompletionReceipt(1, identity, phase, 1, job, registration.PodName, registration.PodUid,
            registration.SourceCommit, "loadgen", 0, true);
        PublicationReadOutcome result = PublicationAcceptance.Evaluate(
            new BlobListing(PublicationAcceptance.PublicationNames.Select(name => prefix + "publication/" + name).ToArray(), 1, true),
            prefix, payloads, registration, completion, index, manifest, start, inventory, registration.SourceCommit,
            new("verifier", PhasePaths.StartLine(run, phase, 1), 1, new string('a', 64)),
            new("verifier", PhasePaths.BrokerInventory(run, phase, 1), 1, new string('a', 64)),
            new("ledger", PhasePaths.ProducerCompletion(run, phase, 1, identity.InstanceId), 1, new string('a', 64)),
            Limits.MaxArtifactBytes, Limits.MaxTotalInputBytes);
        Assert.False(result.Accepted);
        Assert.Null(result.AcceptedSnapshot);
        Assert.Contains("publication_index_unavailable", result.RejectionCodes);
        Assert.Contains("publication_manifest_unavailable", result.RejectionCodes);
    }

    private static async Task AssertIntakeRejectedAsync(Dictionary<string, byte[]> files, string variant)
    {
        const string run = "synthetic-abrupt";
        string prefix = PhasePaths.ProducersPrefix(run, "C1", 1);
        var store = new DiagnosticStore(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [prefix + "synthetic-first/registration.json"] = Change(files["first/registration.json"], variant),
            [prefix + "synthetic-successor/registration.json"] = files["successor/registration.json"]
        });
        var options = new VerifierHostOptions { ScenarioId = "B10", ConfigurationId = "C1", RunId = run, Phase = "C1",
            SourceCommit = new string('a', 40), BlobIoTimeout = TimeSpan.FromSeconds(30), MaxArtifactBytes = Limits.MaxArtifactBytes,
            MaxInputDepth = Limits.MaxDepth, MaxTotalInputBytes = Limits.MaxTotalInputBytes, MaxRowBytes = Limits.MaxRowBytes,
            MaxRowsPerArtifact = Limits.MaxRowsPerArtifact, MaxInputEntries = Limits.MaxEntries, MaxDiagnosticBytes = Limits.MaxDiagnosticBytes };
        await Assert.ThrowsAnyAsync<FormatException>(() => B10PhaseRegistrationReader.ReadAsync(options, 1, store,
            new InputBudget(Limits), (name, _) => Task.FromResult(store.Files[name]), TimeProvider.System, TestContext.Current.CancellationToken));
    }

    private sealed class DiagnosticStore(Dictionary<string, byte[]> files) : IProducerArtifactStore
    {
        internal Dictionary<string, byte[]> Files => files;
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) =>
            Task.FromResult(new BlobListing(files.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray(), 1, true));
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => Task.FromResult(files.GetValueOrDefault(name));
    }
}
#pragma warning restore CA1707
