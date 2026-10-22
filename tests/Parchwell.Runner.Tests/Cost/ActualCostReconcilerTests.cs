#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Text.Json;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Xunit;

namespace Parchwell.Runner.Tests.Cost;

public sealed class ActualCostReconcilerTests
{
    [Fact]
    public async Task Given_ActualCostAboveInputLimit_When_Reconciled_Then_NoReadOrPrivateWriteAdvances()
    {
        var path = Path.GetTempFileName();
        try
        {
            await using (var file = new FileStream(path, FileMode.Open, FileAccess.Write)) file.SetLength(1_048_577);
            var store = new PersistentTestStore();
            var reconciler = new ActualCostReconciler(new RunLedger(store, TimeProvider.System), new EvidenceExporter(store, TimeProvider.System));
            await Assert.ThrowsAsync<InvalidDataException>(() => reconciler.ReconcileAsync("probe", path, TestContext.Current.CancellationToken));
            Assert.Empty(store.Writes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Given_SettledRecord_When_Reconciled_Then_ExactVerifiedTerraformScopeRequiredAndReplayIdempotent()
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
            const string scope = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/synthetic-phase";
            var outputs = Path.Combine(directory, "terraform-outputs.json");
            await File.WriteAllBytesAsync(outputs, JsonSerializer.SerializeToUtf8Bytes(new { aks = new { value = new { id = scope + "/providers/Microsoft.ContainerService/managedClusters/synthetic" } } }), token);
            var receipt = await exporter.ExportAsync("env-a", [new("terraform-outputs.json", outputs)], token);
            await ledger.MarkExportedAsync("probe", "env-a", receipt, token);
            var now = TimeProvider.System.GetUtcNow();
            var destroy = new DestroyReceipt("env-a", now, true, "destroyed");
            var cleanup = await exporter.UploadCleanupReceiptAsync("env-a", destroy, token);
            await ledger.MarkDestroyedAsync("probe", "env-a", destroy with { PrivateReceiptPath = cleanup.Path, PrivateReceiptSha256 = cleanup.Sha256 }, token);
            var record = new ActualCostRecord("received-1", "GBP", 0.3m, "independent-cost-source", now.AddMinutes(2), "env-a", scope + "-other", now.AddMinutes(1), true);
            var recordFile = Path.Combine(directory, "received-cost.json");
            await File.WriteAllBytesAsync(recordFile, JsonSerializer.SerializeToUtf8Bytes(record, LedgerJson.Options), token);
            var reconciler = new ActualCostReconciler(ledger, exporter);
            await Assert.ThrowsAsync<InvalidOperationException>(() => reconciler.ReconcileAsync("probe", recordFile, token));
            Assert.Equal(0.5m, PhaseReservation.CommittedSpend(await ledger.ReadAsync(token)));
            record = record with { ResourceScope = scope };
            await File.WriteAllBytesAsync(recordFile, JsonSerializer.SerializeToUtf8Bytes(record, LedgerJson.Options), token);
            var receivedBytes = await File.ReadAllBytesAsync(recordFile, token);
            var accepted = await reconciler.ReconcileAsync("probe", recordFile, token);
            var receivedHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(receivedBytes));
            Assert.Equal(receivedBytes, store.Objects[("evidence", "A01/env-a/cost/" + receivedHash + ".json")].Bytes);
            var replay = await reconciler.ReconcileAsync("probe", recordFile, token);
            Assert.Equal(accepted.Revision, replay.Revision);
            Assert.Equal(0.3m, PhaseReservation.CommittedSpend(replay));
            store.Objects[("evidence", "A01/env-a/terraform-outputs.json")] = new([0], "changed");
            await Assert.ThrowsAsync<InvalidDataException>(() => reconciler.ReconcileAsync("probe", recordFile, token));
        }
        finally { Directory.Delete(directory, true); }
    }
}
