#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Text.Json;
using System.Text.Json.Nodes;
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
            var state = Path.Combine(directory, "state.private.json");
            await File.WriteAllBytesAsync(state, State(scope), token);
            var receipt = await exporter.ExportAsync("env-a", [new("terraform-outputs.json", outputs), new("terraform/state.private.json", state)], token);
            await ledger.MarkExportedAsync("probe", "env-a", receipt, token);
            var now = TimeProvider.System.GetUtcNow();
            var destroy = new DestroyReceipt("env-a", now, true, "destroyed");
            var cleanup = await exporter.UploadCleanupReceiptAsync("env-a", destroy, token);
            await ledger.MarkDestroyedAsync("probe", "env-a", destroy with { PrivateReceiptPath = cleanup.Path, PrivateReceiptSha256 = cleanup.Sha256 }, token);
            var record = new ActualCostRecord("received-1", "GBP", 0.3m, "independent-cost-source", now.AddMinutes(2), "env-a", scope + "-other", now.AddMinutes(1), true, NodeScope);
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

    private const string PrimaryScope = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/synthetic-phase";
    private const string NodeScope = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/synthetic-nodes";

    [Theory]
    [InlineData("primary-only")]
    [InlineData("node-only")]
    [InlineData("wrong-node")]
    [InlineData("same-group")]
    [InlineData("wrong-subscription")]
    [InlineData("missing-state")]
    [InlineData("missing-node")]
    [InlineData("empty-node")]
    [InlineData("different-cluster")]
    [InlineData("different-state-output")]
    [InlineData("different-generation")]
    [InlineData("duplicate-cluster")]
    [InlineData("duplicate-group")]
    [InlineData("unsupported-state")]
    [InlineData("corrupt-state-readback")]
    public async Task Given_IncompleteOrUnverifiedPhaseCoverage_When_Reconciled_Then_FullReservationRemains(string fault)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            CancellationToken token = TestContext.Current.CancellationToken;
            var store = new PersistentTestStore();
            var ledger = new RunLedger(store, TimeProvider.System);
            var exporter = new EvidenceExporter(store, TimeProvider.System);
            await ledger.InitializeAsync(PhaseReservationTests.Input(), new string('1', 40), true, true, token);
            await ledger.ReserveAsync("probe", "env-a", PhaseReservationTests.Input(), token);
            await ledger.MarkApplyStartedAsync("probe", "env-a", token);
            string outputs = Path.Combine(directory, "terraform-outputs.json"), statePath = Path.Combine(directory, "state.private.json");
            await File.WriteAllBytesAsync(outputs, JsonSerializer.SerializeToUtf8Bytes(new { aks = new { value = new { id = PrimaryScope + "/providers/Microsoft.ContainerService/managedClusters/synthetic" } } }), token);
            JsonNode state = JsonNode.Parse(State(PrimaryScope))!;
            JsonNode cluster = state["values"]!["root_module"]!["child_modules"]![0]!["resources"]![0]!["values"]!;
            switch (fault)
            {
                case "missing-node": cluster["output"]!["properties"]!.AsObject().Remove("nodeResourceGroup"); break;
                case "empty-node": cluster["output"]!["properties"]!["nodeResourceGroup"] = ""; break;
                case "different-cluster": cluster["id"] = PrimaryScope + "/providers/Microsoft.ContainerService/managedClusters/other"; break;
                case "different-state-output": state["values"]!["outputs"]!["aks"]!["value"]!["id"] = "other"; break;
                case "different-generation": state["values"]!["root_module"]!["resources"]![0]!["values"]!["tags"]!["run-id"] = "env-b"; break;
                case "duplicate-cluster": state["values"]!["root_module"]!["child_modules"]![0]!["resources"]!.AsArray().Add(cluster.Parent!.DeepClone()); break;
                case "duplicate-group": state["values"]!["root_module"]!["resources"]!.AsArray().Add(state["values"]!["root_module"]!["resources"]![0]!.DeepClone()); break;
                case "unsupported-state": state["format_version"] = "2.0"; break;
            }
            await File.WriteAllTextAsync(statePath, state.ToJsonString(), token);
            EvidenceFile[] files = fault == "missing-state" ? [new("terraform-outputs.json", outputs)] : [new("terraform-outputs.json", outputs), new("terraform/state.private.json", statePath)];
            PrivateExportReceipt exported = await exporter.ExportAsync("env-a", files, token);
            await ledger.MarkExportedAsync("probe", "env-a", exported, token);
            DateTimeOffset now = TimeProvider.System.GetUtcNow();
            var destroy = new DestroyReceipt("env-a", now, true, "destroyed");
            var cleanup = await exporter.UploadCleanupReceiptAsync("env-a", destroy, token);
            await ledger.MarkDestroyedAsync("probe", "env-a", destroy with { PrivateReceiptPath = cleanup.Path, PrivateReceiptSha256 = cleanup.Sha256 }, token);
            if (fault == "corrupt-state-readback") store.Objects[("evidence", "A01/env-a/terraform/state.private.json")] = new([0], "changed");
            var actual = new ActualCostRecord("received-1", "GBP", 0.01m, "independent-cost-source", now.AddMinutes(2), "env-a", fault == "node-only" ? NodeScope : PrimaryScope, now.AddMinutes(1), true,
                fault switch { "primary-only" => null, "wrong-node" => NodeScope + "-other", "same-group" => PrimaryScope, "wrong-subscription" => NodeScope.Replace("000000000001", "000000000002", StringComparison.Ordinal), _ => NodeScope });
            string recordPath = Path.Combine(directory, "received-cost.json");
            await File.WriteAllBytesAsync(recordPath, JsonSerializer.SerializeToUtf8Bytes(actual, LedgerJson.Options), token);
            long revision = (await ledger.ReadAsync(token)).Revision;
            var reconciler = new ActualCostReconciler(ledger, exporter);

            await Assert.ThrowsAnyAsync<Exception>(() => reconciler.ReconcileAsync("probe", recordPath, token));

            LedgerDocument retained = await ledger.ReadAsync(token);
            Assert.Equal(revision, retained.Revision);
            Assert.Null(retained.Phases["probe"].ActualCostGbp);
            Assert.Equal(0.5m, PhaseReservation.CommittedSpend(retained));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static byte[] State(string scope)
    {
        string cluster = scope + "/providers/Microsoft.ContainerService/managedClusters/synthetic";
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            format_version = "1.0", terraform_version = "1.16.6",
            values = new
            {
                outputs = new { aks = new { value = new { id = cluster } } },
                root_module = new
                {
                    resources = new[] { new { mode = "managed", type = "azurerm_resource_group", values = new { id = scope, tags = new Dictionary<string, string> { ["run-id"] = "env-a", ["repo"] = "eventhubs-kafka-dotnet", ["layer"] = "ephemeral", ["scenario"] = "A01" } } } },
                    child_modules = new[] { new { address = "module.aks", resources = new[] { new { mode = "managed", type = "azapi_resource", values = new { id = cluster, type = "Microsoft.ContainerService/managedClusters@2025-04-01", output = new { properties = new { nodeResourceGroup = "synthetic-nodes" } } } } } } }
                }
            }
        });
    }
}
