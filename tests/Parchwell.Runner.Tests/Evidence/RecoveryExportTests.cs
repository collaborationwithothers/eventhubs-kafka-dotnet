#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Text;
using System.Text.Json;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Tests.Cost;
using Xunit;

namespace Parchwell.Runner.Tests.Evidence;

public sealed class RecoveryExportTests
{
    [Fact]
    public async Task Given_ExistingPhaseManifest_When_RecoveryExported_Then_OriginalPreservedAndIncompleteSideSnapshotDiscoverable()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var token = TestContext.Current.CancellationToken;
            var store = new PersistentTestStore();
            var ledger = new RunLedger(store, TimeProvider.System);
            var exporter = new EvidenceExporter(store, TimeProvider.System);
            await ledger.InitializeAsync(PhaseReservationTests.Input(), new string('1', 40), true, true, token);
            await ledger.ReserveAsync("probe", "env-a", PhaseReservationTests.Input(), token);
            await ledger.MarkApplyStartedAsync("probe", "env-a", token);
            var file = Path.Combine(directory, "state.json");
            byte[] original = [0, 1, 255];
            await File.WriteAllBytesAsync(file, original, token);
            var phase = await exporter.ExportAsync("env-a", [new("state.json", file)], token);
            await ledger.MarkExportedAsync("probe", "env-a", phase, token);
            var phaseManifest = store.Objects[("evidence", phase.ManifestPath)].Bytes.ToArray();
            byte[] recovered = [9, 8, 7];
            await File.WriteAllBytesAsync(file, recovered, token);
            var captureHash = new string('a', 64);
            var recovery = await exporter.ExportRecoveryAsync("env-a", captureHash, [new("state.json", file)], token);
            Assert.False(recovery.Complete);
            Assert.Equal("A01/env-a/recovery/" + captureHash + "/_private-export-manifest.json", recovery.ManifestPath);
            Assert.Equal(recovered, store.Objects[("evidence", "A01/env-a/recovery/" + captureHash + "/state.json")].Bytes);
            Assert.Equal(recovery.ManifestPath, store.Writes[^1]);
            Assert.Equal(recovery, await exporter.ExportRecoveryAsync("env-a", captureHash, [new("state.json", file)], token));
            Assert.Equal(phaseManifest, store.Objects[("evidence", phase.ManifestPath)].Bytes);
            Assert.Equal(original, store.Objects[("evidence", "A01/env-a/state.json")].Bytes);
            var cleanup = new DestroyReceipt("env-a", TimeProvider.System.GetUtcNow(), true, "destroyed", RecoveryExport: recovery);
            var stored = await exporter.UploadCleanupReceiptAsync("env-a", cleanup, token);
            await ledger.MarkDestroyedAsync("probe", "env-a", cleanup with { PrivateReceiptPath = stored.Path, PrivateReceiptSha256 = stored.Sha256 }, token);
            var fresh = await new RunLedger(store, TimeProvider.System).ReadAsync(token);
            Assert.Equal(phase, fresh.Phases["probe"].PrivateExport);
            Assert.Equal(recovery, fresh.Phases["probe"].DestroyReceipt!.RecoveryExport);
            Assert.All(fresh.Phases["probe"].Repeats, repeat => Assert.Equal("not_started", repeat.State));
            await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.ReserveAsync("C0", "env-b", PhaseReservationTests.Input(), token));
            await File.WriteAllBytesAsync(file, new byte[] { 6 }, token);
            await Assert.ThrowsAsync<InvalidDataException>(() => exporter.ExportRecoveryAsync("env-a", captureHash, [new("state.json", file)], token));
            Assert.Equal(phaseManifest, store.Objects[("evidence", phase.ManifestPath)].Bytes);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Given_LegacyDestroyReceipt_When_Read_Then_OptionalRecoveryPointerMayBeAbsentButRequiredFieldsStayStrict()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = new RunLedger(new PersistentTestStore(), TimeProvider.System);
        await ledger.InitializeAsync(PhaseReservationTests.Input(), new string('1', 40), true, true, token);
        await ledger.ReserveAsync("probe", "env-a", PhaseReservationTests.Input(), token);
        await ledger.MarkApplyStartedAsync("probe", "env-a", token);
        var hash = new string('b', 64);
        var current = await ledger.MarkDestroyedAsync("probe", "env-a", new("env-a", TimeProvider.System.GetUtcNow(), true, "destroyed",
            "A01/env-a/cleanup/" + hash + ".json", hash), token);
        var json = Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(current, LedgerJson.Options));
        var legacy = json.Replace(",\"recovery_export\":null", "", StringComparison.Ordinal);
        Assert.NotEqual(json, legacy);
        Assert.Null(RunLedger.Decode(Encoding.UTF8.GetBytes(legacy)).Phases["probe"].DestroyReceipt!.RecoveryExport);
        var missingRequired = legacy.Replace(",\"reason\":\"destroyed\"", "", StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => RunLedger.Decode(Encoding.UTF8.GetBytes(missingRequired)));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Given_RecoveryReceiptWithCompletionOrForeignGeneration_When_Committed_Then_Rejects(bool complete, bool foreign)
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = new RunLedger(new PersistentTestStore(), TimeProvider.System);
        await ledger.InitializeAsync(PhaseReservationTests.Input(), new string('1', 40), true, true, token);
        await ledger.ReserveAsync("probe", "env-a", PhaseReservationTests.Input(), token);
        await ledger.MarkApplyStartedAsync("probe", "env-a", token);
        var hash = new string('b', 64);
        var recovery = new PrivateExportReceipt("A01/" + (foreign ? "env-b" : "env-a") + "/recovery/" + hash + "/_private-export-manifest.json", hash, 1, 1, complete);
        await Assert.ThrowsAsync<InvalidDataException>(() => ledger.MarkDestroyedAsync("probe", "env-a",
            new("env-a", TimeProvider.System.GetUtcNow(), true, "destroyed", "A01/env-a/cleanup/" + hash + ".json", hash, recovery), token));
        Assert.Null((await ledger.ReadAsync(token)).Phases["probe"].DestroyReceipt);
    }
}
