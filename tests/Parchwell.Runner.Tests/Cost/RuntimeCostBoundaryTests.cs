using System.Security.Cryptography;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Xunit;

namespace Parchwell.Runner.Tests.Cost;

#pragma warning disable CA1707 // Repository-required Given_When_Then names.

/// <summary>Protects exact ledger and received-cost byte ceilings through durable production paths.</summary>
public sealed class RuntimeCostBoundaryTests
{
    private const string Commit = "1111111111111111111111111111111111111111";
    private const string Scope = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/synthetic-phase";
    private const string NodeScope = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/synthetic-nodes";

    [Theory]
    [InlineData(1_048_575, true)]
    [InlineData(1_048_576, true)]
    [InlineData(1_048_577, false)]
    public async Task Given_LedgerAtReadByteBoundary_When_Read_Then_OnlyOversizeIsRejectedByDecoder(int length, bool accepted)
    {
        var seed = new PersistentTestStore();
        await new RunLedger(seed, TimeProvider.System).InitializeAsync(PhaseReservationTests.Input(), Commit, true, true, TestContext.Current.CancellationToken);
        byte[] bytes = Pad(seed.Objects[("ledger", RunLedger.BlobPath)].Bytes, length);
        var store = new ReadBoundaryStore(bytes);
        var ledger = new RunLedger(store, TimeProvider.System);

        if (accepted)
        {
            LedgerDocument read = await ledger.ReadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(Commit, read.SourceCommit);
            Assert.Equal(3, read.Phases.Count);
            Assert.All(read.Phases.Values, phase => Assert.Equal("not_started", phase.State));
        }
        else Assert.Equal("ledger_size_exceeded", (await Assert.ThrowsAsync<InvalidDataException>(() => ledger.ReadAsync(TestContext.Current.CancellationToken))).Message);
        Assert.Equal(length, bytes.Length);
        Assert.Equal(1, store.Reads);
    }

    [Theory]
    [InlineData(1_048_575, true)]
    [InlineData(1_048_576, true)]
    [InlineData(1_048_577, false)]
    public async Task Given_LedgerAtPersistByteBoundary_When_Initialized_Then_OnlyOversizeCannotCommit(int length, bool accepted)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var clock = new FixedClock();
        CostInput input = PhaseReservationTests.Input() with { BaselineSources = ["a"] };
        var seed = new PersistentTestStore();
        await new RunLedger(seed, clock).InitializeAsync(input, Commit, true, true, token);
        int baseLength = seed.Objects[("ledger", RunLedger.BlobPath)].Bytes.Length;
        input = input with { BaselineSources = [new string('a', checked(length - baseLength + 1))] };
        var store = new PersistentTestStore();
        var ledger = new RunLedger(store, clock);
        Task<LedgerDocument> initialize = ledger.InitializeAsync(input, Commit, true, true, token);

        if (accepted)
        {
            LedgerDocument created = await initialize;
            Assert.Equal(length, store.Objects[("ledger", RunLedger.BlobPath)].Bytes.Length);
            Assert.Equal(input.BaselineSources, created.BaselineSources);
            Assert.Equal(input.BaselineSources, (await ledger.ReadAsync(token)).BaselineSources);
            Assert.Single(store.Writes);
        }
        else
        {
            Assert.Equal("ledger_size_exceeded", (await Assert.ThrowsAsync<InvalidDataException>(() => initialize)).Message);
            Assert.Empty(store.Writes);
            Assert.Empty(store.Objects);
        }
    }

    [Theory]
    [InlineData(1_048_575, true)]
    [InlineData(1_048_576, true)]
    [InlineData(1_048_577, false)]
    public async Task Given_SettledCostAtInputByteBoundary_When_Reconciled_Then_OnlyOversizeCannotReplaceReservation(int length, bool accepted)
    {
        string directory = Path.Combine(Path.GetTempPath(), "actual-cost-boundary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            CancellationToken token = TestContext.Current.CancellationToken;
            var store = new PersistentTestStore();
            var ledger = new RunLedger(store, TimeProvider.System);
            var exporter = new EvidenceExporter(store, TimeProvider.System);
            await ledger.InitializeAsync(PhaseReservationTests.Input(), Commit, true, true, token);
            await ledger.ReserveAsync("probe", "env-a", PhaseReservationTests.Input(), token);
            await ledger.MarkApplyStartedAsync("probe", "env-a", token);
            string outputsPath = Path.Combine(directory, "terraform-outputs.json");
            string statePath = Path.Combine(directory, "state.private.json");
            string clusterId = Scope + "/providers/Microsoft.ContainerService/managedClusters/synthetic";
            await File.WriteAllBytesAsync(outputsPath, JsonSerializer.SerializeToUtf8Bytes(new { aks = new { value = new { id = clusterId } } }), token);
            await File.WriteAllBytesAsync(statePath, State(clusterId), token);
            PrivateExportReceipt exported = await exporter.ExportAsync("env-a", [new("terraform-outputs.json", outputsPath), new("terraform/state.private.json", statePath)], token);
            await ledger.MarkExportedAsync("probe", "env-a", exported, token);
            DateTimeOffset now = TimeProvider.System.GetUtcNow();
            var destroy = new DestroyReceipt("env-a", now, true, "destroyed");
            EvidenceObjectReceipt cleanup = await exporter.UploadCleanupReceiptAsync("env-a", destroy, token);
            await ledger.MarkDestroyedAsync("probe", "env-a", destroy with { PrivateReceiptPath = cleanup.Path, PrivateReceiptSha256 = cleanup.Sha256 }, token);
            var record = new ActualCostRecord("synthetic-received-cost", "GBP", 0.3m, "synthetic-independent-source", now.AddMinutes(2), "env-a", Scope, now.AddMinutes(1), true, NodeScope);
            byte[] bytes = Pad(JsonSerializer.SerializeToUtf8Bytes(record, LedgerJson.Options), length);
            string path = Path.Combine(directory, "received-cost.json");
            await File.WriteAllBytesAsync(path, bytes, token);
            int previousWrites = store.Writes.Count;
            long revision = (await ledger.ReadAsync(token)).Revision;
            var reconciler = new ActualCostReconciler(ledger, exporter);

            if (accepted)
            {
                LedgerDocument reconciled = await reconciler.ReconcileAsync("probe", path, token);
                Assert.Equal(0.3m, PhaseReservation.CommittedSpend(reconciled));
                Assert.Equal(revision + 1, reconciled.Revision);
                string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                Assert.Equal(bytes, store.Objects[("evidence", "A01/env-a/cost/" + hash + ".json")].Bytes);
            }
            else
            {
                Assert.Equal("actual_cost_record_limit", (await Assert.ThrowsAsync<InvalidDataException>(() => reconciler.ReconcileAsync("probe", path, token))).Message);
                LedgerDocument retained = await ledger.ReadAsync(token);
                Assert.Equal(revision, retained.Revision);
                Assert.Equal(0.5m, PhaseReservation.CommittedSpend(retained));
                Assert.Equal(previousWrites, store.Writes.Count);
                Assert.Null(retained.Phases["probe"].ActualCostGbp);
            }
            Assert.Equal(length, new FileInfo(path).Length);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static byte[] State(string clusterId) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        format_version = "1.0",
        values = new
        {
            outputs = new { aks = new { value = new { id = clusterId } } },
            root_module = new
            {
                resources = new object[]
                {
                    new { mode = "managed", type = "azurerm_resource_group", values = new { id = Scope, tags = new Dictionary<string, string> { ["run-id"] = "env-a", ["repo"] = "eventhubs-kafka-dotnet", ["layer"] = "ephemeral", ["scenario"] = "A01" } } },
                    new { mode = "managed", type = "azapi_resource", values = new { id = clusterId, type = "Microsoft.ContainerService/managedClusters@2025-04-01", output = new { properties = new { nodeResourceGroup = "synthetic-nodes" } } } }
                }
            }
        }
    });

    private static byte[] Pad(byte[] json, int length)
    {
        Assert.True(json.Length < length);
        byte[] padded = new byte[length];
        json.CopyTo(padded, 0);
        padded.AsSpan(json.Length).Fill((byte)' ');
        return padded;
    }

    private sealed class ReadBoundaryStore(byte[] bytes) : IArtifactStore
    {
        internal int Reads { get; private set; }
        public Task<StoredArtifact?> ReadAsync(string container, string path, int maxBytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("ledger", container);
            Assert.Equal(RunLedger.BlobPath, path);
            Assert.Equal(1_048_576, maxBytes);
            Reads++;
            // Return the oversized transport value too, exercising the ledger's independent bound.
            return Task.FromResult<StoredArtifact?>(new(bytes, "synthetic-etag"));
        }
        public Task<ArtifactWriteOutcome> WriteAsync(string container, string path, ReadOnlyMemory<byte> content, string? expectedETag, bool createOnly, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("unexpected_write");
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    }
}
#pragma warning restore CA1707
