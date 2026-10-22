#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using Xunit;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;

namespace Parchwell.Runner.Tests.Cost;

public sealed class PhaseReservationTests
{
    private const string Source = "1111111111111111111111111111111111111111";

    [Theory]
    [InlineData("14.99", true)]
    [InlineData("15.00", true)]
    [InlineData("15.01", false)]
    public async Task Given_BudgetBoundary_When_Reserved_Then_ExactDecimalLimitApplied(string total, bool permitted)
    {
        var store = new PersistentTestStore();
        var ledger = new RunLedger(store, TimeProvider.System);
        var cost = Input(decimal.Parse(total, System.Globalization.CultureInfo.InvariantCulture) - 0.5m);
        await ledger.InitializeAsync(cost, Source, true, true, cancellationToken: TestContext.Current.CancellationToken);
        if (permitted) Assert.Equal("reserved", (await ledger.ReserveAsync("probe", "env-a", cost, cancellationToken: TestContext.Current.CancellationToken)).Phases["probe"].State);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.ReserveAsync("probe", "env-a", cost, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_ReservedLedger_When_FreshRunnerBootstrapAndReserve_Then_NoHeadroomOrRevisionReset()
    {
        var store = new PersistentTestStore();
        var cost = Input(2);
        var first = new RunLedger(store, TimeProvider.System);
        await first.InitializeAsync(cost, Source, true, true, cancellationToken: TestContext.Current.CancellationToken);
        var reserved = await first.ReserveAsync("probe", "env-a", cost, cancellationToken: TestContext.Current.CancellationToken);
        var fresh = new RunLedger(store, TimeProvider.System);
        var duplicate = await fresh.InitializeAsync(cost, Source, true, true, cancellationToken: TestContext.Current.CancellationToken);
        var replay = await fresh.ReserveAsync("probe", "env-a", cost, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(reserved.Revision, duplicate.Revision);
        Assert.Equal(reserved.Revision, replay.Revision);
        Assert.Equal(2.5m, PhaseReservation.CommittedSpend(replay));
        await fresh.MarkInterruptedAsync("probe", "env-a", "crash_before_apply", cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fresh.MarkApplyStartedAsync("probe", "env-a", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2.5m, PhaseReservation.CommittedSpend(await fresh.ReadAsync(cancellationToken: TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Given_FractionalClock_When_ApplyStarted_Then_PersistedExpiryMatchesTerraformSecondPrecision()
    {
        var clock = new FractionalClock();
        var ledger = new RunLedger(new PersistentTestStore(), clock);
        await ledger.InitializeAsync(Input(), Source, true, true, cancellationToken: TestContext.Current.CancellationToken);
        await ledger.ReserveAsync("probe", "env-a", Input(), cancellationToken: TestContext.Current.CancellationToken);
        var phase = (await ledger.MarkApplyStartedAsync("probe", "env-a", cancellationToken: TestContext.Current.CancellationToken)).Phases["probe"];
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 1, 2, 3, TimeSpan.Zero), phase.ApplyStartedUtc);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 5, 2, 3, TimeSpan.Zero), phase.ExpiresAt);
    }

    private sealed class FractionalClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 22, 1, 2, 3, TimeSpan.Zero).AddTicks(1234567);
    }

    [Fact]
    public async Task Given_BootstrapMismatch_When_Initialize_Then_BlocksMissingAndChangedBaseline()
    {
        var ledger = new RunLedger(new PersistentTestStore(), TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.InitializeAsync(Input(), Source, false, true, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.InitializeAsync(Input(), Source, true, false, cancellationToken: TestContext.Current.CancellationToken));
        await ledger.InitializeAsync(Input(), Source, true, true, cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.InitializeAsync(Input(1), Source, true, true, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(ArtifactWriteOutcome.Conflict, false)]
    [InlineData(ArtifactWriteOutcome.Uncertain, true)]
    [InlineData(ArtifactWriteOutcome.Uncertain, false)]
    public async Task Given_ConditionalWrite_When_Readback_Then_OnlyExactBytesAuthorizeAdvance(ArtifactWriteOutcome outcome, bool persist)
    {
        var store = new PersistentTestStore();
        var ledger = new RunLedger(store, TimeProvider.System);
        await ledger.InitializeAsync(Input(), Source, true, true, cancellationToken: TestContext.Current.CancellationToken);
        store.NextOutcome = outcome;
        store.PersistNextWrite = persist;
        if (outcome == ArtifactWriteOutcome.Uncertain && persist)
            Assert.Equal("reserved", (await ledger.ReserveAsync("probe", "env-a", Input(), cancellationToken: TestContext.Current.CancellationToken)).Phases["probe"].State);
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.ReserveAsync("probe", "env-a", Input(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, store.WriteCount);
    }

    [Fact]
    public async Task Given_StartedRepeat_When_ReplayedOrFreshRunner_Then_NeverReplaced()
    {
        var store = new PersistentTestStore();
        var ledger = new RunLedger(store, TimeProvider.System);
        await ledger.InitializeAsync(Input(), Source, true, true, cancellationToken: TestContext.Current.CancellationToken);
        await ledger.ReserveAsync("probe", "env-a", Input(), cancellationToken: TestContext.Current.CancellationToken);
        await ledger.MarkApplyStartedAsync("probe", "env-a", cancellationToken: TestContext.Current.CancellationToken);
        await ledger.MarkRepeatStartedAsync("probe", "env-a", 1, cancellationToken: TestContext.Current.CancellationToken);
        var fresh = new RunLedger(store, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fresh.MarkApplyStartedAsync("probe", "env-a", cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fresh.MarkRepeatStartedAsync("probe", "env-a", 1, cancellationToken: TestContext.Current.CancellationToken));
        await fresh.MarkInterruptedAsync("probe", "env-a", "process_lost", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("interrupted", (await fresh.ReadAsync(cancellationToken: TestContext.Current.CancellationToken)).Phases["probe"].Repeats[0].State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fresh.ReserveAsync("C0", "env-b", Input(), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_DestroyedPhase_When_ActualsDelayedThenSettledReplay_Then_ReservationRetainedUntilReceipt()
    {
        var ledger = new RunLedger(new PersistentTestStore(), TimeProvider.System);
        await ledger.InitializeAsync(Input(), Source, true, true, cancellationToken: TestContext.Current.CancellationToken);
        await ledger.ReserveAsync("probe", "env-a", Input(), cancellationToken: TestContext.Current.CancellationToken);
        await ledger.MarkApplyStartedAsync("probe", "env-a", cancellationToken: TestContext.Current.CancellationToken);
        var now = DateTimeOffset.UtcNow.AddMinutes(1);
        await ledger.MarkDestroyedAsync("probe", "env-a", new DestroyReceipt("env-a", now, true, "destroyed", "A01/env-a/cleanup/" + new string('a', 64) + ".json", new string('a', 64)), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0.5m, PhaseReservation.CommittedSpend(await ledger.ReadAsync(cancellationToken: TestContext.Current.CancellationToken)));
        var actual = new ActualCostRecord("cost-1", "GBP", 0.3m, "private-cost-record", now.AddMinutes(2), "env-a", "exact-phase-scope", now.AddMinutes(1), false, "exact-node-scope");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.ReconcileAsync("probe", actual, "exact-phase-scope", "exact-node-scope", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0.5m, PhaseReservation.CommittedSpend(await ledger.ReadAsync(cancellationToken: TestContext.Current.CancellationToken)));
        actual = actual with { Settled = true };
        var reconciled = await ledger.ReconcileAsync("probe", actual, "exact-phase-scope", "exact-node-scope", cancellationToken: TestContext.Current.CancellationToken);
        var replay = await ledger.ReconcileAsync("probe", actual, "exact-phase-scope", "exact-node-scope", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(reconciled.Revision, replay.Revision);
        Assert.Equal(0.3m, PhaseReservation.CommittedSpend(replay));
    }

    [Fact]
    public void Given_InvalidCostInputs_When_Validated_Then_FailClosed()
    {
        Assert.Throws<InvalidDataException>(() => PhaseReservation.Validate(Input(-1)));
        Assert.Throws<InvalidDataException>(() => PhaseReservation.Validate(Input() with { Currency = "USD" }));
        Assert.Throws<InvalidDataException>(() => PhaseReservation.Validate(Input() with { Sources = [] }));
        Assert.Throws<InvalidDataException>(() => PhaseReservation.Validate(Input() with { Sources = [new("https://example.invalid", DateTimeOffset.UtcNow, "hour", 1, -1)] }));
        Assert.Throws<InvalidDataException>(() => PhaseReservation.Validate(Input() with { PhaseEstimates = new() }));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("primary")]
    public async Task Given_PersistedActualCostWithoutDistinctNodeScope_When_FreshProcessReads_Then_LegacySettlementCannotAuthorizeSpend(string fault)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var store = new PersistentTestStore();
        var ledger = new RunLedger(store, TimeProvider.System);
        await ledger.InitializeAsync(Input(), Source, true, true, token);
        await ledger.ReserveAsync("probe", "env-a", Input(), token);
        await ledger.MarkApplyStartedAsync("probe", "env-a", token);
        DateTimeOffset now = TimeProvider.System.GetUtcNow();
        await ledger.MarkDestroyedAsync("probe", "env-a", new DestroyReceipt("env-a", now, true, "destroyed", "A01/env-a/cleanup/" + new string('a', 64) + ".json", new string('a', 64)), token);
        var actual = new ActualCostRecord("cost-1", "GBP", 0.3m, "private-cost-record", now.AddMinutes(2), "env-a", "exact-phase-scope", now.AddMinutes(1), true, "exact-node-scope");
        await ledger.ReconcileAsync("probe", actual, "exact-phase-scope", "exact-node-scope", token);
        StoredArtifact original = store.Objects[("ledger", RunLedger.BlobPath)];
        var json = System.Text.Json.Nodes.JsonNode.Parse(original.Bytes)!;
        var recordedActual = json["phases"]!["probe"]!["actual_cost_source"]!.AsObject();
        if (fault == "missing") recordedActual.Remove("node_resource_scope");
        else recordedActual["node_resource_scope"] = fault switch { "null" => null, "empty" => "", _ => "exact-phase-scope" };
        store.Objects[("ledger", RunLedger.BlobPath)] = original with { Bytes = JsonSerializer.SerializeToUtf8Bytes(json) };
        int writes = store.WriteCount;
        var fresh = new RunLedger(store, TimeProvider.System);

        await Assert.ThrowsAsync<InvalidDataException>(() => fresh.ReadAsync(token));
        await Assert.ThrowsAsync<InvalidDataException>(() => fresh.ReserveAsync("C0", "env-b", Input(), token));
        Assert.Equal(writes, store.WriteCount);
    }

    [Fact]
    public async Task Given_ExportedAndDestroyedLedger_When_FreshRunnerReads_Then_ReceiptsAndEligibilitySurvive()
    {
        var store = new PersistentTestStore();
        var ledger = new RunLedger(store, TimeProvider.System);
        await ledger.InitializeAsync(Input(), Source, true, true, cancellationToken: TestContext.Current.CancellationToken);
        foreach (var phaseName in RunLedger.PhaseNames)
        {
            var env = "env-" + phaseName;
            await ledger.ReserveAsync(phaseName, env, Input(), cancellationToken: TestContext.Current.CancellationToken);
            await ledger.MarkApplyStartedAsync(phaseName, env, cancellationToken: TestContext.Current.CancellationToken);
            for (var ordinal = 1; ordinal <= 5; ordinal++)
            {
                await ledger.MarkRepeatStartedAsync(phaseName, env, ordinal, "admitted-pod", cancellationToken: TestContext.Current.CancellationToken);
                await ledger.MarkRepeatCompletedAsync(phaseName, env, ordinal, new("valid", "completed"), cancellationToken: TestContext.Current.CancellationToken);
            }
            var export = new PrivateExportReceipt("A01/" + env + "/manifest.json", new string('a', 64), 1, 1);
            await ledger.MarkExportedAsync(phaseName, env, export, cancellationToken: TestContext.Current.CancellationToken);
            ledger = new RunLedger(store, TimeProvider.System);
            Assert.Equal(export, (await ledger.ReadAsync(cancellationToken: TestContext.Current.CancellationToken)).Phases[phaseName].PrivateExport);
            await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.MarkApplyStartedAsync(phaseName, env, cancellationToken: TestContext.Current.CancellationToken));
            var cleanup = new DestroyReceipt(env, TimeProvider.System.GetUtcNow(), true, "destroyed", "A01/" + env + "/cleanup/" + new string('b', 64) + ".json", new string('b', 64));
            await ledger.MarkDestroyedAsync(phaseName, env, cleanup, cancellationToken: TestContext.Current.CancellationToken);
            ledger = new RunLedger(store, TimeProvider.System);
            Assert.Equal(cleanup, (await ledger.ReadAsync(cancellationToken: TestContext.Current.CancellationToken)).Phases[phaseName].DestroyReceipt);
            if (phaseName == "probe") await ledger.RecordGateAsync(new("pass", new string('c', 64), Source), cancellationToken: TestContext.Current.CancellationToken);
        }
        var before = (await ledger.ReadAsync(cancellationToken: TestContext.Current.CancellationToken)).Revision;
        foreach (var phaseName in RunLedger.PhaseNames) await ledger.ReserveAsync(phaseName, "env-" + phaseName, Input(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(before, (await ledger.ReadAsync(cancellationToken: TestContext.Current.CancellationToken)).Revision);
        Assert.Equal(1.5m, PhaseReservation.CommittedSpend(await ledger.ReadAsync(cancellationToken: TestContext.Current.CancellationToken)));
        await Assert.ThrowsAsync<InvalidDataException>(() => ledger.ReserveAsync("C4", "env-new", Input(), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_DestroyedPhaseWithInterruptedRepeats_When_NextPhaseReserved_Then_BlocksDespiteExportAndCleanup()
    {
        var ledger = new RunLedger(new PersistentTestStore(), TimeProvider.System);
        await ledger.InitializeAsync(Input(), Source, true, true, cancellationToken: TestContext.Current.CancellationToken);
        await ledger.ReserveAsync("probe", "env-a", Input(), cancellationToken: TestContext.Current.CancellationToken);
        await ledger.MarkApplyStartedAsync("probe", "env-a", cancellationToken: TestContext.Current.CancellationToken);
        await ledger.MarkRepeatStartedAsync("probe", "env-a", 1, cancellationToken: TestContext.Current.CancellationToken);
        await ledger.MarkInterruptedAsync("probe", "env-a", "execution_failure", cancellationToken: TestContext.Current.CancellationToken);
        await ledger.MarkExportedAsync("probe", "env-a", new("A01/env-a/manifest.json", new string('a', 64), 1, 1), cancellationToken: TestContext.Current.CancellationToken);
        await ledger.MarkDestroyedAsync("probe", "env-a", new("env-a", TimeProvider.System.GetUtcNow(), true, "destroyed",
            "A01/env-a/cleanup/" + new string('b', 64) + ".json", new string('b', 64)), cancellationToken: TestContext.Current.CancellationToken);
        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.ReserveAsync("C0", "env-b", Input(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("prior_phase_incomplete", blocked.Message);
        Assert.Equal(0.5m, PhaseReservation.CommittedSpend(await ledger.ReadAsync(cancellationToken: TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Given_UnknownLedgerField_When_Read_Then_RejectsSchema()
    {
        var store = new PersistentTestStore();
        var ledger = new RunLedger(store, TimeProvider.System);
        await ledger.InitializeAsync(Input(), Source, true, true, cancellationToken: TestContext.Current.CancellationToken);
        var current = store.Objects[("ledger", RunLedger.BlobPath)];
        var json = System.Text.Encoding.UTF8.GetString(current.Bytes).Replace("\"schema_version\":1", "\"unknown\":1,\"schema_version\":1", StringComparison.Ordinal);
        store.Objects[("ledger", RunLedger.BlobPath)] = current with { Bytes = System.Text.Encoding.UTF8.GetBytes(json) };
        await Assert.ThrowsAsync<JsonException>(() => ledger.ReadAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    internal static CostInput Input(decimal baseline = 0) => new("GBP", "A01", baseline, ["received-baseline"],
        new() { ["probe"] = new(0.4m, 0.1m), ["C0"] = new(0.4m, 0.1m), ["C3"] = new(0.4m, 0.1m) }, 1,
        [new("https://example.invalid/price", new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero), "hour", 1, 0.4m)]);
}

internal sealed class PersistentTestStore : IArtifactStore
{
    public Dictionary<(string Container, string Path), StoredArtifact> Objects { get; } = [];
    public List<string> Writes { get; } = [];
    public int WriteCount { get; private set; }
    public ArtifactWriteOutcome NextOutcome { get; set; } = ArtifactWriteOutcome.Stored;
    public bool PersistNextWrite { get; set; } = true;
    public bool CorruptReads { get; set; }

    public Task<StoredArtifact?> ReadAsync(string container, string path, int maxBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Objects.TryGetValue((container, path), out var value)) return Task.FromResult<StoredArtifact?>(null);
        if (value.Bytes.Length > maxBytes) throw new InvalidDataException("limit");
        return Task.FromResult<StoredArtifact?>(CorruptReads ? value with { Bytes = [0] } : value);
    }

    public Task<ArtifactWriteOutcome> WriteAsync(string container, string path, ReadOnlyMemory<byte> bytes, string? expectedETag, bool createOnly, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteCount++;
        Writes.Add(path);
        var exists = Objects.TryGetValue((container, path), out var prior);
        if (createOnly && exists || !createOnly && prior?.ETag != expectedETag) return Task.FromResult(ArtifactWriteOutcome.Conflict);
        var outcome = NextOutcome;
        NextOutcome = ArtifactWriteOutcome.Stored;
        if (PersistNextWrite && outcome != ArtifactWriteOutcome.Conflict)
            Objects[(container, path)] = new(bytes.ToArray(), WriteCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        PersistNextWrite = true;
        return Task.FromResult(outcome);
    }
}
