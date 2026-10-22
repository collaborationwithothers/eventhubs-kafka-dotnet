#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using Xunit;
using System.Text;
using System.Text.Json;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Tests.Cost;

namespace Parchwell.Runner.Tests.Evidence;

public sealed class EvidenceExporterTests
{
    [Fact]
    public async Task Given_OriginalBytes_When_ExportedTwice_Then_ReadbackManifestLastAndCleanupSeparate()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "worker.bin");
            byte[] original = [0, 1, 255, 13, 10];
            await File.WriteAllBytesAsync(path, original, cancellationToken: TestContext.Current.CancellationToken);
            var store = new PersistentTestStore();
            var exporter = new EvidenceExporter(store, TimeProvider.System);
            var receipt = await exporter.ExportAsync("env-a", [new("repeat-1/worker.bin", path)], cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("A01/env-a/manifest.json", store.Writes[^1]);
            Assert.Equal(original, store.Objects[("evidence", "A01/env-a/repeat-1/worker.bin")].Bytes);
            Assert.Equal(original.Length, receipt.VerifiedBytes);
            Assert.Equal(receipt, await exporter.ExportAsync("env-a", [new("repeat-1/worker.bin", path)], cancellationToken: TestContext.Current.CancellationToken));
            var manifestBefore = store.Objects[("evidence", receipt.ManifestPath)].Bytes.ToArray();
            await exporter.UploadCleanupReceiptAsync("env-a", new("env-a", TimeProvider.System.GetUtcNow(), true, "destroyed"), cancellationToken: TestContext.Current.CancellationToken);
            Assert.StartsWith("A01/env-a/cleanup/", store.Writes[^1], StringComparison.Ordinal);
            Assert.Equal(manifestBefore, store.Objects[("evidence", receipt.ManifestPath)].Bytes);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Given_GateOriginalBytes_When_UploadedAndReplayed_Then_ReadsExactPrivateBytes()
    {
        var store = new PersistentTestStore();
        var exporter = new EvidenceExporter(store, TimeProvider.System);
        var rule = new Parchwell.Scenarios.ProbeGate("C0",
            new("stats.broker.throttle.max", Parchwell.Kafka.Configuration.KafkaClientRole.LoadGenProducer),
            Parchwell.Scenarios.ProbeOperator.GreaterThan, 20000, 5, ["C0", "C3"]);
        var evaluated = Parchwell.Verifier.ProbeGate.ProbeGateEvaluator.Evaluate(new(rule, [], []), new string('1', 40));
        Assert.Equal("inconclusive", evaluated.Outcome);
        byte[] bytes = Parchwell.Verifier.ProbeGate.ProbeGateResultCodec.Write(evaluated, 16 * 1024 * 1024);
        var receipt = await exporter.UploadGateAsync("env-a", bytes, TestContext.Current.CancellationToken);
        Assert.Equal("A01/env-a/gate/" + receipt.Sha256 + ".json", receipt.Path);
        Assert.Equal(receipt, await exporter.UploadGateAsync("env-a", bytes, TestContext.Current.CancellationToken));
        Assert.Equal(bytes, await exporter.ReadGateAsync("env-a", receipt.Sha256, TestContext.Current.CancellationToken));
        store.Objects[("evidence", receipt.Path)] = new([0], "different");
        await Assert.ThrowsAsync<InvalidDataException>(() => exporter.UploadGateAsync("env-a", bytes, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => exporter.ReadGateAsync("env-a", receipt.Sha256, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_GateAboveByteLimit_When_Uploaded_Then_NoRemoteWrite()
    {
        var store = new PersistentTestStore();
        await Assert.ThrowsAsync<InvalidDataException>(() => new EvidenceExporter(store, TimeProvider.System)
            .UploadGateAsync("env-a", new byte[16 * 1024 * 1024 + 1], TestContext.Current.CancellationToken));
        Assert.Empty(store.Writes);
    }

    [Fact]
    public async Task Given_RealWorkerManifestFilename_When_RepeatThenPhaseExported_Then_OriginalObjectsSharePathsAndReadBack()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "manifest.json");
            await File.WriteAllBytesAsync(path, new byte[] { 1, 2 }, TestContext.Current.CancellationToken);
            var store = new PersistentTestStore();
            var exporter = new EvidenceExporter(store, TimeProvider.System);
            var repeat = await exporter.ExportRepeatAsync("env-a", "env-a-r1", [new("manifest.json", path)], TestContext.Current.CancellationToken);
            Assert.Equal("A01/env-a/env-a-r1/_private-export-manifest.json", repeat.ManifestPath);
            var phase = await exporter.ExportAsync("env-a", [new("env-a-r1/manifest.json", path)], TestContext.Current.CancellationToken);
            Assert.Equal(new byte[] { 1, 2 }, await exporter.ReadVerifiedFileAsync("env-a", "env-a-r1/manifest.json", phase, TestContext.Current.CancellationToken));
            Assert.Equal(3, store.Objects.Count);
            await Assert.ThrowsAsync<InvalidDataException>(() => exporter.ExportRepeatAsync("env-a", "env-a-r6", [], TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Given_EmptyOriginalArtifact_When_Exported_Then_PreservesZeroLength()
    {
        var path = Path.GetTempFileName();
        try
        {
            var receipt = await new EvidenceExporter(new PersistentTestStore(), TimeProvider.System).ExportAsync("env-a", [new("empty.log", path)], cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(1, receipt.VerifiedFileCount);
            Assert.Equal(0, receipt.VerifiedBytes);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ExistingDifferentBytesOrCorruption_When_Exported_Then_NoManifest(bool corrupt)
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "original", cancellationToken: TestContext.Current.CancellationToken);
            var store = new PersistentTestStore { CorruptReads = corrupt };
            if (!corrupt) store.Objects[("evidence", "A01/env-a/worker")] = new([9], "existing");
            var exporter = new EvidenceExporter(store, TimeProvider.System);
            await Assert.ThrowsAsync<InvalidDataException>(() => exporter.ExportAsync("env-a", [new("worker", path)], cancellationToken: TestContext.Current.CancellationToken));
            Assert.DoesNotContain("A01/env-a/manifest.json", store.Writes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Given_EntryLimitOrTraversal_When_Exported_Then_NoRemoteWrites()
    {
        var store = new PersistentTestStore();
        var exporter = new EvidenceExporter(store, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidDataException>(() => exporter.ExportAsync("env-a", Enumerable.Range(0, 257).Select(i => new EvidenceFile(i.ToString(System.Globalization.CultureInfo.InvariantCulture), "unused")).ToArray(), cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => exporter.ExportAsync("env-a", [new("../secret", "unused")], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(store.Writes);
    }

    [Fact]
    public void Given_NestedUnknownAndCaseVariantSubscription_When_CandidateCreated_Then_OnlyAllowlistedRedactedFieldsRemain()
    {
        const string guid = "01234567-89ab-cdef-0123-456789abcdef";
        var input = JsonSerializer.SerializeToUtf8Bytes(new
        {
            run_id = "run-" + guid.ToUpperInvariant(),
            connection_string = "private",
            raw = new { secret = "private" },
            versions = new { runner = guid.ToUpperInvariant(), client_id = "private" },
            diagnostics = new { error_codes = new[] { -184, 5 }, logs = "private" },
            costs = new { actual_gbp = (decimal?)null, actual_available = false }
        });
        var output = Encoding.UTF8.GetString(PublicCandidate.Create(input, guid));
        Assert.DoesNotContain("private", output, StringComparison.Ordinal);
        Assert.DoesNotContain(guid, output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED_SUBSCRIPTION]", output, StringComparison.Ordinal);
        Assert.Contains("\"actual_gbp\":null", output, StringComparison.Ordinal);
        Assert.Contains("\"synthetic\":true", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"run_id\":\"/subscriptions/private/resourceGroups/private\"}")]
    [InlineData("{\"reasons\":[\"arbitrary exception text with secrets\"]}")]
    [InlineData("{\"diagnostics\":{\"max_throttle_ms\":{\"secret\":\"private\"}}}")]
    public void Given_PrivateOrUnboundedStrings_When_CandidateCreated_Then_Rejects(string input) =>
        Assert.Throws<InvalidDataException>(() => PublicCandidate.Create(Encoding.UTF8.GetBytes(input), "01234567-89ab-cdef-0123-456789abcdef"));

    [Fact]
    public async Task Given_OfficialAzureCliRecording_When_Parsed_Then_ActualFlatRowShapeAccepted()
    {
        using var resource = typeof(EvidenceExporterTests).Assembly.GetManifestResourceStream("Parchwell.Runner.Tests.Evidence.azure-cli-query-recording.json");
        Assert.NotNull(resource);
        using var bytes = new MemoryStream();
        await resource.CopyToAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        var receipt = KqlCollector.Parse(bytes.ToArray(), "getschema source from official CLI recording");
        Assert.Equal("available", receipt.Status);
        Assert.Equal(31, receipt.Rows);
        Assert.Equal(bytes.ToArray(), receipt.OriginalOutput);
    }

    [Theory]
    [InlineData("[]", "incomplete", "logs_absent_or_delayed")]
    [InlineData("{\"tables\":[]}", "incomplete", "query_shape_invalid")]
    [InlineData("[{\"TableName\":\"PrimaryResult\",\"ErrorCount\":\"2\",\"Detail\":\"bounded\"}]", "available", "corroborating_only")]
    [InlineData("[{\"TableName\":\"PrimaryResult\",\"ErrorCount\":2}]", "incomplete", "query_shape_invalid")]
    public void Given_ActualCliFlatStringRows_When_Parsed_Then_RecordsCorroborationOnly(string json, string status, string reason)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var receipt = KqlCollector.Parse(bytes, "retained query");
        Assert.Equal(status, receipt.Status);
        Assert.Equal(reason, receipt.Reason);
        Assert.Equal(bytes, receipt.OriginalOutput);
    }
}
