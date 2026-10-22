using System.Text.Json;
using System.Text.RegularExpressions;
using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Cost;

/// <summary>Each returned transition has been conditionally persisted and read back byte for byte.</summary>
public sealed partial class RunLedger(IArtifactStore store, TimeProvider clock)
{
    public const int MaxBytes = 1_048_576;
    public const string BlobPath = "A01/state.json";
    public static IReadOnlyList<string> PhaseNames { get; } = ["probe", "C0", "C3"];

    public async Task<LedgerDocument> ReadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await store.ReadAsync("ledger", BlobPath, MaxBytes, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("ledger_missing");
        return Decode(stored.Bytes);
    }

    public Task<LedgerDocument> InitializeAsync(CostInput input, string sourceCommit, bool initialize,
        bool ephemeralStateEmpty, CancellationToken cancellationToken = default) =>
        InitializeAsync(input, sourceCommit, initialize, ephemeralStateEmpty, "probe", cancellationToken);

    public async Task<LedgerDocument> InitializeAsync(CostInput input, string sourceCommit, bool initialize,
        bool ephemeralStateEmpty, string phase, CancellationToken cancellationToken = default)
    {
        using var operationTimer = new CancellationTokenSource(TimeSpan.FromSeconds(180), clock);
        using var operationDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operationTimer.Token);
        cancellationToken = operationDeadline.Token;
        PhaseReservation.Validate(input);
        if (!CommitPattern().IsMatch(sourceCommit)) throw new InvalidDataException("source_commit_invalid");
        var existing = await store.ReadAsync("ledger", BlobPath, MaxBytes, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            var ledger = Decode(existing.Bytes);
            CheckBaseline(ledger, input, sourceCommit);
            return ledger;
        }
        if (!initialize || !ephemeralStateEmpty || phase != "probe") throw new InvalidOperationException("ledger_bootstrap_forbidden");
        var phases = PhaseNames.ToDictionary(name => name, _ => new PhaseRecord("not_started", null, null,
            0, 0, 0, null, null, null, null, Enumerable.Range(1, 5).Select(i => new RepeatRecord(i, null)).ToArray(), null, null, null), StringComparer.Ordinal);
        var created = new LedgerDocument(1, "A01", "GBP", 0, sourceCommit, input.BaselineSpendGbp,
            input.BaselineSources, phases);
        await PersistAsync(created, null, true, cancellationToken).ConfigureAwait(false);
        return created;
    }

    public Task<LedgerDocument> ReserveAsync(string phase, string environmentRunId, CostInput input, CancellationToken cancellationToken = default) =>
        ChangeAsync(phase, environmentRunId, (ledger, current) =>
        {
            PhaseReservation.Validate(input);
            CheckBaseline(ledger, input, ledger.SourceCommit);
            if (current.ReservationId is not null)
            {
                if (current.EnvironmentRunId != environmentRunId) throw new InvalidOperationException("reservation_generation_conflict");
                return current;
            }
            if (ledger.Phases.Any(pair => pair.Key != phase && pair.Value.ReservationId is not null && pair.Value.State != "destroyed"))
                throw new InvalidOperationException("outstanding_phase_reservation");
            var index = PhaseNames.ToList().IndexOf(phase);
            foreach (var previous in PhaseNames.Take(index).Select(key => ledger.Phases[key]))
                if (previous.State != "destroyed" || previous.PrivateExport?.Complete != true || previous.DestroyReceipt?.Complete != true ||
                    previous.Repeats.Any(repeat => repeat.State != "completed"))
                    throw new InvalidOperationException("prior_phase_incomplete");
            if (phase != "probe" && ledger.Phases["probe"].GateReceipt?.Outcome != "pass")
                throw new InvalidOperationException("passing_gate_required");
            var estimate = input.PhaseEstimates[phase];
            var total = checked(estimate.PhaseEstimateGbp + estimate.TeardownReserveGbp);
            if (checked(PhaseReservation.CommittedSpend(ledger) + total) > PhaseReservation.LimitGbp)
                throw new InvalidOperationException("cost_limit_exceeded");
            return current with { State = "reserved", ReservationId = "A01/" + phase, EnvironmentRunId = environmentRunId,
                PhaseEstimateGbp = estimate.PhaseEstimateGbp, TeardownReserveGbp = estimate.TeardownReserveGbp,
                ReservedTotalGbp = total, Repeats = current.Repeats.Select(r => r with { RunId = environmentRunId + "-r" + r.Ordinal }).ToArray() };
        }, cancellationToken);

    public Task<LedgerDocument> MarkApplyStartedAsync(string phase, string environmentRunId, CancellationToken cancellationToken = default) =>
        ChangeAsync(phase, environmentRunId, (_, current) =>
        {
            if (current.State != "reserved" || current.ApplyStartedUtc is not null) throw new InvalidOperationException("phase_restart_forbidden");
            var observed = clock.GetUtcNow().ToUniversalTime();
            var now = observed.AddTicks(-(observed.Ticks % TimeSpan.TicksPerSecond));
            return current with { State = "apply_started", ApplyStartedUtc = now, ExpiresAt = now.AddHours(4) };
        }, cancellationToken);

    public Task<LedgerDocument> MarkRepeatStartedAsync(string phase, string environmentRunId, int ordinal, string? admittedIdentity = null,
        CancellationToken cancellationToken = default) => ChangeAsync(phase, environmentRunId, (_, current) =>
        {
            if (current.State is not ("apply_started" or "running")) throw new InvalidOperationException("phase_not_running");
            var repeat = Repeat(current, ordinal);
            if (repeat.State != "not_started" || current.Repeats.Take(ordinal - 1).Any(r => r.State != "completed"))
                throw new InvalidOperationException("repeat_replacement_forbidden");
            return Replace(current with { State = "running" }, repeat with { State = "started", AdmittedIdentity = admittedIdentity });
        }, cancellationToken);

    public Task<LedgerDocument> RecordAdmissionAsync(string phase, string environmentRunId, int ordinal, string admittedIdentity,
        CancellationToken cancellationToken = default) => ChangeAsync(phase, environmentRunId, (_, current) =>
        {
            var repeat = Repeat(current, ordinal);
            if (repeat.State != "started" || repeat.AdmittedIdentity is not null || string.IsNullOrWhiteSpace(admittedIdentity))
                throw new InvalidOperationException("admission_not_allowed");
            return Replace(current, repeat with { AdmittedIdentity = admittedIdentity });
        }, cancellationToken);

    public Task<LedgerDocument> MarkRepeatCompletedAsync(string phase, string environmentRunId, int ordinal, RepeatCompletion completion,
        CancellationToken cancellationToken = default) => ChangeAsync(phase, environmentRunId, (_, current) =>
        {
            var repeat = Repeat(current, ordinal);
            if (repeat.State != "started" || repeat.AdmittedIdentity is null || !SafeCode(completion.Outcome) || !SafeCode(completion.Reason))
                throw new InvalidOperationException("repeat_completion_invalid");
            return Replace(current, repeat with { State = "completed", Outcome = completion.Outcome, Reason = completion.Reason,
                WorkerHash = completion.WorkerHash, ResultHash = completion.ResultHash, PublicationHash = completion.PublicationHash });
        }, cancellationToken);

    public Task<LedgerDocument> MarkInterruptedAsync(string phase, string environmentRunId, string reason, CancellationToken cancellationToken = default) =>
        ChangeAsync(phase, environmentRunId, (_, current) =>
        {
            if (current.ReservationId is null || current.State == "destroyed" || !SafeCode(reason)) throw new InvalidOperationException("interruption_invalid");
            return current with { State = "interrupted", Repeats = current.Repeats.Select(r => r.State == "started"
                ? r with { State = "interrupted", Outcome = "incomplete", Reason = reason } : r).ToArray() };
        }, cancellationToken);

    public Task<LedgerDocument> MarkExportedAsync(string phase, string environmentRunId, PrivateExportReceipt receipt, CancellationToken cancellationToken = default) =>
        ChangeAsync(phase, environmentRunId, (_, current) =>
        {
            if (current.PrivateExport == receipt) return current;
            if (current.ApplyStartedUtc is null || current.DestroyReceipt is not null || current.PrivateExport is not null)
                throw new InvalidOperationException("export_transition_invalid");
            return current with { State = "exported", PrivateExport = receipt };
        }, cancellationToken);

    public Task<LedgerDocument> MarkDestroyedAsync(string phase, string environmentRunId, DestroyReceipt receipt, CancellationToken cancellationToken = default) =>
        ChangeAsync(phase, environmentRunId, (_, current) =>
        {
            if (current.DestroyReceipt == receipt) return current;
            if (current.ApplyStartedUtc is null || receipt.EnvironmentRunId != environmentRunId || receipt.CompletedAt < current.ApplyStartedUtc ||
                receipt.PrivateReceiptPath is null || receipt.PrivateReceiptSha256 is null || current.DestroyReceipt?.Complete == true)
                throw new InvalidOperationException("destroy_receipt_invalid");
            return current with { State = receipt.Complete ? "destroyed" : "interrupted", DestroyReceipt = receipt };
        }, cancellationToken);

    public Task<LedgerDocument> RecordGateAsync(GateReceipt receipt, CancellationToken cancellationToken = default) =>
        ChangeAsync("probe", null, (ledger, current) =>
        {
            if (current.GateReceipt == receipt) return current;
            if (current.State != "destroyed" || current.PrivateExport?.Complete != true || current.DestroyReceipt?.Complete != true ||
                current.Repeats.Any(r => r.State != "completed") || current.GateReceipt is not null || receipt.SourceCommit != ledger.SourceCommit ||
                receipt.Outcome is not ("pass" or "untestable_at_build_scale" or "inconclusive") || !ShaPattern().IsMatch(receipt.SourceSha256))
                throw new InvalidOperationException("gate_receipt_invalid");
            return current with { GateReceipt = receipt };
        }, cancellationToken);

    public Task<LedgerDocument> ReconcileAsync(string phase, ActualCostRecord actual, string expectedResourceScope, string expectedNodeResourceScope,
        CancellationToken cancellationToken = default) => ChangeAsync(phase, actual.EnvironmentRunId, (_, current) =>
        {
            if (actual.ResourceScope != expectedResourceScope || string.IsNullOrWhiteSpace(expectedResourceScope) ||
                actual.NodeResourceScope != expectedNodeResourceScope || string.IsNullOrWhiteSpace(expectedNodeResourceScope) ||
                expectedResourceScope.Equals(expectedNodeResourceScope, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("actual_cost_scope_incomplete");
            if (current.ActualCostSource == actual) return current;
            if (current.DestroyReceipt?.Complete != true || !actual.Settled || actual.Currency != "GBP" || actual.CostGbp < 0 ||
                string.IsNullOrWhiteSpace(actual.RecordId) || string.IsNullOrWhiteSpace(actual.Source) || actual.ReceivedAt == default ||
                actual.CoverageThrough < current.DestroyReceipt.CompletedAt || actual.ReceivedAt < actual.CoverageThrough || current.ActualCostSource is not null)
                throw new InvalidOperationException("actual_cost_not_settled");
            return current with { ActualCostGbp = actual.CostGbp, ActualCostSource = actual };
        }, cancellationToken);

    private async Task<LedgerDocument> ChangeAsync(string phase, string? environmentRunId,
        Func<LedgerDocument, PhaseRecord, PhaseRecord> change, CancellationToken cancellationToken)
    {
        using var operationTimer = new CancellationTokenSource(TimeSpan.FromSeconds(180), clock);
        using var operationDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operationTimer.Token);
        cancellationToken = operationDeadline.Token;
        if (!PhaseNames.Contains(phase, StringComparer.Ordinal)) throw new InvalidDataException("phase_unknown");
        if (environmentRunId is not null && !RunIdPattern().IsMatch(environmentRunId)) throw new InvalidDataException("environment_run_id_invalid");
        var stored = await store.ReadAsync("ledger", BlobPath, MaxBytes, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("ledger_missing");
        var ledger = Decode(stored.Bytes);
        var current = ledger.Phases[phase];
        if (current.EnvironmentRunId is not null && environmentRunId is not null && current.EnvironmentRunId != environmentRunId)
            throw new InvalidOperationException("environment_generation_conflict");
        var changed = change(ledger, current);
        if (changed == current) return ledger;
        ledger.Phases[phase] = changed;
        ledger = ledger with { Revision = checked(ledger.Revision + 1) };
        await PersistAsync(ledger, stored.ETag, false, cancellationToken).ConfigureAwait(false);
        return ledger;
    }

    private async Task PersistAsync(LedgerDocument ledger, string? etag, bool createOnly, CancellationToken cancellationToken)
    {
        Validate(ledger);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(ledger, LedgerJson.Options);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("ledger_size_exceeded");
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(180), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        var outcome = await store.WriteAsync("ledger", BlobPath, bytes, etag, createOnly, deadline.Token).ConfigureAwait(false);
        if (outcome == ArtifactWriteOutcome.Conflict) throw new InvalidOperationException("ledger_etag_conflict");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0) await Task.Delay(TimeSpan.FromSeconds(attempt), clock, deadline.Token).ConfigureAwait(false);
            try
            {
                var readback = await store.ReadAsync("ledger", BlobPath, MaxBytes, deadline.Token).ConfigureAwait(false);
                if (readback is not null && readback.Bytes.AsSpan().SequenceEqual(bytes)) return;
                if (outcome == ArtifactWriteOutcome.Stored) break;
            }
            catch (IOException) when (outcome == ArtifactWriteOutcome.Uncertain && attempt < 2) { }
        }
        throw new InvalidOperationException("ledger_readback_unconfirmed");
    }

    public static LedgerDocument Decode(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) throw new InvalidDataException("ledger_size_exceeded");
        using var document = JsonDocument.Parse(bytes);
        RejectDuplicateProperties(document.RootElement);
        var value = JsonSerializer.Deserialize<LedgerDocument>(bytes, LedgerJson.Options) ?? throw new InvalidDataException("ledger_invalid");
        Validate(value);
        using var canonical = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(value, LedgerJson.Options));
        RequireFixedShape(document.RootElement, canonical.RootElement);
        return value;
    }

    private static void RequireFixedShape(JsonElement supplied, JsonElement expected)
    {
        if (expected.ValueKind == JsonValueKind.Object)
        {
            if (supplied.ValueKind != JsonValueKind.Object) throw new InvalidDataException("ledger_required_fields_missing");
            // Schema 1 predates the optional recovery pointer on destroy receipts.
            bool absentRecovery = expected.TryGetProperty("recovery_export", out var recovery) && recovery.ValueKind == JsonValueKind.Null &&
                !supplied.TryGetProperty("recovery_export", out _);
            if (supplied.EnumerateObject().Count() != expected.EnumerateObject().Count() - (absentRecovery ? 1 : 0))
                throw new InvalidDataException("ledger_required_fields_missing");
            foreach (var property in expected.EnumerateObject())
            {
                if (!supplied.TryGetProperty(property.Name, out var child))
                {
                    if (absentRecovery && property.Name == "recovery_export") continue;
                    throw new InvalidDataException("ledger_required_fields_missing");
                }
                RequireFixedShape(child, property.Value);
            }
        }
        else if (expected.ValueKind == JsonValueKind.Array)
        {
            if (supplied.ValueKind != JsonValueKind.Array || supplied.GetArrayLength() != expected.GetArrayLength())
                throw new InvalidDataException("ledger_array_shape_invalid");
            for (var i = 0; i < expected.GetArrayLength(); i++) RequireFixedShape(supplied[i], expected[i]);
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("ledger_duplicate_property");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
    }

    private static void CheckBaseline(LedgerDocument ledger, CostInput input, string sourceCommit)
    {
        if (ledger.SourceCommit != sourceCommit || ledger.BaselineSpendGbp != input.BaselineSpendGbp ||
            !ledger.BaselineSources.SequenceEqual(input.BaselineSources, StringComparer.Ordinal))
            throw new InvalidOperationException("ledger_initialization_mismatch");
    }

    public static void Validate(LedgerDocument ledger)
    {
        if (ledger.SchemaVersion != 1 || ledger.Item != "A01" || ledger.Currency != "GBP" || ledger.Revision < 0 ||
            !CommitPattern().IsMatch(ledger.SourceCommit) || ledger.BaselineSpendGbp < 0 || ledger.BaselineSources is not { Length: > 0 } ||
            ledger.BaselineSources.Any(string.IsNullOrWhiteSpace) || ledger.Phases is null ||
            !ledger.Phases.Keys.Order(StringComparer.Ordinal).SequenceEqual(PhaseNames.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("ledger_schema_invalid");
        foreach (var (name, phase) in ledger.Phases)
        {
            if (phase is null || phase.State is not ("not_started" or "reserved" or "apply_started" or "running" or "exported" or "destroyed" or "interrupted") ||
                phase.Repeats is not { Length: 5 } || phase.ActualCostGbp < 0 ||
                (phase.ActualCostGbp is null) != (phase.ActualCostSource is null) || (name != "probe" && phase.GateReceipt is not null))
                throw new InvalidDataException("ledger_phase_invalid");
            if (phase.ReservationId is null)
            {
                if (phase.State != "not_started" || phase.EnvironmentRunId is not null || phase.ReservedTotalGbp != 0 || phase.PhaseEstimateGbp != 0 || phase.TeardownReserveGbp != 0 ||
                    phase.ActualCostGbp is not null || phase.ApplyStartedUtc is not null || phase.ExpiresAt is not null ||
                    phase.PrivateExport is not null || phase.DestroyReceipt is not null || phase.GateReceipt is not null)
                    throw new InvalidDataException("ledger_unreserved_invalid");
            }
            else if (phase.ReservationId != "A01/" + name || phase.EnvironmentRunId is null || !RunIdPattern().IsMatch(phase.EnvironmentRunId) ||
                phase.PhaseEstimateGbp <= 0 || phase.TeardownReserveGbp <= 0 || phase.ReservedTotalGbp != checked(phase.PhaseEstimateGbp + phase.TeardownReserveGbp) ||
                phase.State == "not_started" || (phase.ApplyStartedUtc is null) != (phase.ExpiresAt is null) ||
                (phase.ApplyStartedUtc is not null && phase.ExpiresAt != phase.ApplyStartedUtc.Value.AddHours(4)))
                throw new InvalidDataException("ledger_reservation_invalid");
            if (phase.State == "reserved" && phase.ApplyStartedUtc is not null ||
                phase.State is "apply_started" or "running" or "exported" or "destroyed" && phase.ApplyStartedUtc is null ||
                phase.ApplyStartedUtc is not null && (phase.ApplyStartedUtc.Value.Offset != TimeSpan.Zero || phase.ApplyStartedUtc.Value.Ticks % TimeSpan.TicksPerSecond != 0))
                throw new InvalidDataException("ledger_apply_marker_invalid");
            if (phase.PrivateExport is { } export && (phase.EnvironmentRunId is null ||
                export.ManifestPath != "A01/" + phase.EnvironmentRunId + "/manifest.json" || !ShaPattern().IsMatch(export.ManifestSha256) ||
                export.VerifiedFileCount is < 0 or > 256 || export.VerifiedBytes is < 0 or > 8L * 1024 * 1024 * 1024))
                throw new InvalidDataException("ledger_export_receipt_invalid");
            if (phase.DestroyReceipt is { } destroyed && (destroyed.EnvironmentRunId != phase.EnvironmentRunId || phase.ApplyStartedUtc is null ||
                destroyed.CompletedAt < phase.ApplyStartedUtc || !SafeCode(destroyed.Reason) ||
                destroyed.PrivateReceiptSha256 is null || !ShaPattern().IsMatch(destroyed.PrivateReceiptSha256) ||
                destroyed.PrivateReceiptPath != "A01/" + phase.EnvironmentRunId + "/cleanup/" + destroyed.PrivateReceiptSha256 + ".json"))
                throw new InvalidDataException("ledger_destroy_receipt_invalid");
            if (phase.DestroyReceipt?.RecoveryExport is { } snapshot)
            {
                string prefix = "A01/" + phase.EnvironmentRunId + "/recovery/";
                string suffix = "/_private-export-manifest.json";
                if (snapshot.Complete || !snapshot.ManifestPath.StartsWith(prefix, StringComparison.Ordinal) ||
                    !snapshot.ManifestPath.EndsWith(suffix, StringComparison.Ordinal) ||
                    snapshot.ManifestPath.Length != prefix.Length + 64 + suffix.Length ||
                    !ShaPattern().IsMatch(snapshot.ManifestPath.AsSpan(prefix.Length, 64)) ||
                    !ShaPattern().IsMatch(snapshot.ManifestSha256) || snapshot.VerifiedFileCount is < 0 or > 256 ||
                    snapshot.VerifiedBytes is < 0 or > 8L * 1024 * 1024 * 1024)
                    throw new InvalidDataException("ledger_recovery_export_invalid");
            }
            if (phase.State == "destroyed" && phase.DestroyReceipt?.Complete != true)
                throw new InvalidDataException("ledger_destroy_marker_invalid");
            if (phase.GateReceipt is { } gate && (gate.SourceCommit != ledger.SourceCommit || !ShaPattern().IsMatch(gate.SourceSha256) ||
                gate.Outcome is not ("pass" or "untestable_at_build_scale" or "inconclusive") || phase.DestroyReceipt?.Complete != true || phase.PrivateExport?.Complete != true ||
                phase.Repeats.Any(r => r.State != "completed")))
                throw new InvalidDataException("ledger_gate_receipt_invalid");
            if (phase.ActualCostSource is { } actual && (!actual.Settled || actual.Currency != "GBP" || actual.CostGbp != phase.ActualCostGbp ||
                actual.EnvironmentRunId != phase.EnvironmentRunId || string.IsNullOrWhiteSpace(actual.RecordId) || string.IsNullOrWhiteSpace(actual.Source) ||
                string.IsNullOrWhiteSpace(actual.ResourceScope) || string.IsNullOrWhiteSpace(actual.NodeResourceScope) ||
                actual.ResourceScope.Equals(actual.NodeResourceScope, StringComparison.OrdinalIgnoreCase) || phase.DestroyReceipt?.Complete != true ||
                actual.CoverageThrough < phase.DestroyReceipt.CompletedAt || actual.ReceivedAt < actual.CoverageThrough))
                throw new InvalidDataException("ledger_actual_cost_invalid");
            for (var i = 0; i < 5; i++)
            {
                var repeat = phase.Repeats[i];
                if (repeat is null || repeat.Ordinal != i + 1 || repeat.RunId != (phase.EnvironmentRunId is null ? null : phase.EnvironmentRunId + "-r" + (i + 1)) ||
                    repeat.State is not ("not_started" or "started" or "completed" or "interrupted") ||
                    (repeat.Outcome is not null && !SafeCode(repeat.Outcome)) || (repeat.Reason is not null && !SafeCode(repeat.Reason)) ||
                    new[] { repeat.WorkerHash, repeat.ResultHash, repeat.PublicationHash }.Any(hash => hash is not null && !ShaPattern().IsMatch(hash)))
                    throw new InvalidDataException("ledger_repeat_invalid");
                if (repeat.State == "not_started" && (repeat.AdmittedIdentity is not null || repeat.WorkerHash is not null || repeat.ResultHash is not null ||
                    repeat.PublicationHash is not null || repeat.Outcome is not null || repeat.Reason is not null) ||
                    repeat.State != "not_started" && (phase.ApplyStartedUtc is null || phase.Repeats.Take(i).Any(prior => prior.State != "completed")) ||
                    repeat.State == "completed" && (string.IsNullOrWhiteSpace(repeat.AdmittedIdentity) || repeat.Outcome is null || repeat.Reason is null))
                    throw new InvalidDataException("ledger_repeat_marker_invalid");
            }
        }
    }

    private static RepeatRecord Repeat(PhaseRecord phase, int ordinal) => ordinal is >= 1 and <= 5
        ? phase.Repeats[ordinal - 1] : throw new InvalidOperationException("repeat_ordinal_invalid");
    private static PhaseRecord Replace(PhaseRecord phase, RepeatRecord repeat) => phase with
    { Repeats = phase.Repeats.Select(r => r.Ordinal == repeat.Ordinal ? repeat : r).ToArray() };
    private static bool SafeCode(string value) => value.Length is > 0 and <= 128 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');
    [GeneratedRegex("^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ShaPattern();
    [GeneratedRegex("^[a-fA-F0-9]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitPattern();
    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9_-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex RunIdPattern();
}
