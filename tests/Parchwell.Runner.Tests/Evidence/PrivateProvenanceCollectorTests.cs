#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Text;
using System.Text.Json;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Tests.Cost;
using Xunit;

namespace Parchwell.Runner.Tests.Evidence;

public sealed class PrivateProvenanceCollectorTests
{
    private static readonly string[] Tools = ["azure-cli.json", "kubectl.json", "kubelogin-version.txt",
        "kubelogin-convert-help.txt", "aks-get-credentials-help.txt", "terraform.json", "blob-upload-help.txt",
        "blob-download-help.txt", "helm.json"];

    [Theory]
    [InlineData("probe")]
    [InlineData("C0")]
    [InlineData("C3")]
    public async Task Given_AllPhaseProvenance_When_StagedAndExported_Then_ExactInputsReadBackBeforeManifest(string phase)
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var token = TestContext.Current.CancellationToken;
            var (runtime, original) = await PrepareAsync(root, phase);
            using (runtime)
            {
                string output = Path.Combine(root, "phase");
                var capture = await PrivateProvenanceCollector.CollectAsync(runtime.RootElement, original, output, token);
                Assert.True(capture.Complete);
                byte[] cost = await File.ReadAllBytesAsync(Path.Combine(root, "cost.private.json"), token);
                Assert.Equal(cost, capture.CostBytes);
                Assert.Contains("persistent_estimate_gbp", Encoding.UTF8.GetString(capture.CostBytes!), StringComparison.Ordinal);
                Assert.Contains("sources", Encoding.UTF8.GetString(capture.CostBytes!), StringComparison.Ordinal);
                Assert.Equal(original, await File.ReadAllBytesAsync(Path.Combine(output, "private-provenance", "runtime.json"), token));
                Assert.True((await PrivateProvenanceCollector.CollectAsync(runtime.RootElement, original, output, token)).Complete);
                var store = new PersistentTestStore();
                var exporter = new EvidenceExporter(store, TimeProvider.System);
                var files = Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
                    .Select(path => new EvidenceFile(Path.GetRelativePath(output, path).Replace('\\', '/'), path)).ToArray();
                var receipt = await exporter.ExportAsync("env-" + phase.ToLowerInvariant(), files, token);
                Assert.True(receipt.Complete);
                Assert.Equal(receipt.ManifestPath, store.Writes[^1]);
                foreach (var file in files)
                    Assert.Equal(await File.ReadAllBytesAsync(file.LocalPath, token), await exporter.ReadVerifiedFileAsync("env-" + phase.ToLowerInvariant(), file.RelativePath, receipt, token));
                Assert.DoesNotContain(files, file => file.RelativePath.Contains("unrelated-secret", StringComparison.Ordinal));
                Assert.DoesNotContain(files, file => file.RelativePath.Contains("helm.tar.gz", StringComparison.Ordinal));
                byte[] candidate = PublicCandidate.Create(JsonSerializer.SerializeToUtf8Bytes(new { scenario_id = "A01", raw = original, cost, status = "completed" }), "01234567-89ab-cdef-0123-456789abcdef");
                Assert.DoesNotContain("PRIVATE-ORIGINAL", Encoding.UTF8.GetString(candidate), StringComparison.Ordinal);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Given_MissingReceiptAndOversizedTool_When_FallbackCaptured_Then_RetainsAvailableOriginalsWithIncompleteStatus()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var token = TestContext.Current.CancellationToken;
            var (runtime, original) = await PrepareAsync(root, "probe");
            using (runtime)
            {
                File.Delete(Path.Combine(root, "image-receipts.json"));
                await using (var file = new FileStream(Path.Combine(root, "tools", "kubectl.json"), FileMode.Open, FileAccess.Write))
                    file.SetLength(PrivateProvenanceCollector.MaxReceiptBytes + 1L);
                string output = Path.Combine(root, "phase");
                var capture = await PrivateProvenanceCollector.CollectAsync(runtime.RootElement, original, output, token);
                Assert.False(capture.Complete);
                using var status = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "private-provenance", "status.json"), token));
                Assert.False(status.RootElement.GetProperty("complete").GetBoolean());
                Assert.Equal("image-receipts.json", Assert.Single(status.RootElement.GetProperty("missing").EnumerateArray()).GetString());
                Assert.Equal("tools/kubectl.json", Assert.Single(status.RootElement.GetProperty("invalid").EnumerateArray()).GetString());
                Assert.Equal(original, await File.ReadAllBytesAsync(Path.Combine(output, "private-provenance", "runtime.json"), token));
                Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(root, "cost.private.json"), token), capture.CostBytes);
                Assert.False(File.Exists(Path.Combine(output, "private-provenance", "image-receipts.json")));
                Assert.False(File.Exists(Path.Combine(output, "private-provenance", "tools", "kubectl.json")));
                var store = new PersistentTestStore();
                var exporter = new EvidenceExporter(store, TimeProvider.System);
                var receipt = await PrivateProvenanceCollector.ExportAvailableAsync(runtime.RootElement, original, output, "env-a", exporter, TimeProvider.System, token);
                Assert.False(receipt.Complete);
                Assert.Equal(receipt.ManifestPath, store.Writes[^1]);
                Assert.Equal(original, store.Objects[("evidence", "A01/env-a/private-provenance/runtime.json")].Bytes);
                Assert.Equal(capture.CostBytes, store.Objects[("evidence", "A01/env-a/private-provenance/cost-input.json")].Bytes);
                await Assert.ThrowsAsync<InvalidDataException>(() => exporter.ReadVerifiedFileAsync("env-a", "private-provenance/runtime.json", receipt, token));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Given_StatusPathIsDirectory_When_FallbackExported_Then_RetainsExactAvailableOriginalsWithIncompleteReceipt()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var token = TestContext.Current.CancellationToken;
            var (runtime, original) = await PrepareAsync(root, "probe");
            using (runtime)
            {
                string output = Path.Combine(root, "phase");
                Directory.CreateDirectory(Path.Combine(output, "private-provenance", "status.json"));
                var store = new PersistentTestStore();
                var exporter = new EvidenceExporter(store, TimeProvider.System);
                var receipt = await PrivateProvenanceCollector.ExportAvailableAsync(runtime.RootElement, original, output, "env-a", exporter, TimeProvider.System, token);
                Assert.False(receipt.Complete);
                Assert.Equal(receipt.ManifestPath, store.Writes[^1]);
                Assert.Equal(Tools.Length + 4, receipt.VerifiedFileCount);
                Assert.Equal(original, store.Objects[("evidence", "A01/env-a/private-provenance/runtime.json")].Bytes);
                Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(root, "cost.private.json"), token),
                    store.Objects[("evidence", "A01/env-a/private-provenance/cost-input.json")].Bytes);
                foreach (string path in Tools.Select(tool => "tools/" + tool).Concat(["image-receipts.json", "image-publication.log"]))
                    Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(root, path), token),
                        store.Objects[("evidence", "A01/env-a/private-provenance/" + path)].Bytes);
                Assert.False(store.Objects.ContainsKey(("evidence", "A01/env-a/private-provenance/status.json")));
                await Assert.ThrowsAsync<InvalidDataException>(() => exporter.ReadVerifiedFileAsync("env-a", "private-provenance/runtime.json", receipt, token));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Given_ChangedSourceAfterSnapshot_When_CollectedAgain_Then_PreservesFirstOriginalAndMarksIncomplete()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var token = TestContext.Current.CancellationToken;
            var (runtime, original) = await PrepareAsync(root, "C0");
            using (runtime)
            {
                string output = Path.Combine(root, "phase");
                byte[] first = (await PrivateProvenanceCollector.CollectAsync(runtime.RootElement, original, output, token)).CostBytes!;
                await File.WriteAllTextAsync(Path.Combine(root, "cost.private.json"), "changed", token);
                Assert.False((await PrivateProvenanceCollector.CollectAsync(runtime.RootElement, original, output, token)).Complete);
                Assert.Equal(first, await File.ReadAllBytesAsync(Path.Combine(output, "private-provenance", "cost-input.json"), token));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(1_048_575, true)]
    [InlineData(1_048_576, true)]
    [InlineData(1_048_577, false)]
    [InlineData(16_777_215, true)]
    [InlineData(16_777_216, true)]
    [InlineData(16_777_217, false)]
    public async Task Given_InputByteBoundary_When_Read_Then_EnforcesDocumentAndLogLimits(int size, bool allowed)
    {
        string path = Path.GetTempFileName();
        try
        {
            var token = TestContext.Current.CancellationToken;
            await using (var file = new FileStream(path, FileMode.Open, FileAccess.Write)) file.SetLength(size);
            int limit = size < 2_000_000 ? PrivateProvenanceCollector.MaxDocumentBytes : PrivateProvenanceCollector.MaxReceiptBytes;
            if (allowed) Assert.Equal(size, (await PrivateProvenanceCollector.ReadBoundedAsync(path, limit, token)).Length);
            else await Assert.ThrowsAsync<InvalidDataException>(() => PrivateProvenanceCollector.ReadBoundedAsync(path, limit, token));
        }
        finally { File.Delete(path); }
    }

    private static async Task<(JsonDocument Runtime, byte[] Original)> PrepareAsync(string root, string phase)
    {
        var token = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Path.Combine(root, "tools"));
        foreach (string tool in Tools)
            await File.WriteAllBytesAsync(Path.Combine(root, "tools", tool), Encoding.UTF8.GetBytes("PRIVATE-ORIGINAL\r\n" + tool), token);
        foreach (string name in new[] { "image-receipts.json", phase == "probe" ? "image-publication.log" : "image-reference-readback.log", "unrelated-secret", "tools/helm.tar.gz" })
            await File.WriteAllBytesAsync(Path.Combine(root, name), Encoding.UTF8.GetBytes("PRIVATE-ORIGINAL\r\n" + name), token);
        string cost = Path.Combine(root, "cost.private.json");
        await File.WriteAllBytesAsync(cost, JsonSerializer.SerializeToUtf8Bytes(PhaseReservationTests.Input(), LedgerJson.Options), token);
        byte[] runtime = Encoding.UTF8.GetBytes(" \n" + JsonSerializer.Serialize(new { phase, private_provenance_directory = root, cost_file = cost, raw_private = "PRIVATE-ORIGINAL" }) + "\r\n");
        return (JsonDocument.Parse(runtime), runtime);
    }
}
