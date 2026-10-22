namespace Parchwell.Runner.Run.Cost;

public sealed record RepeatCompletion(string Outcome, string Reason, string? WorkerHash = null, string? ResultHash = null, string? PublicationHash = null);
public sealed record PrivateExportReceipt(string ManifestPath, string ManifestSha256, int VerifiedFileCount, long VerifiedBytes, bool Complete = true);
public sealed record DestroyReceipt(string EnvironmentRunId, DateTimeOffset CompletedAt, bool Complete, string Reason, string? PrivateReceiptPath = null, string? PrivateReceiptSha256 = null, PrivateExportReceipt? RecoveryExport = null);
public sealed record GateReceipt(string Outcome, string SourceSha256, string SourceCommit);
public sealed record ActualCostRecord(string RecordId, string Currency, decimal CostGbp, string Source, DateTimeOffset ReceivedAt,
    string EnvironmentRunId, string ResourceScope, DateTimeOffset CoverageThrough, bool Settled);
public sealed record RepeatRecord(int Ordinal, string? RunId, string State = "not_started", string? AdmittedIdentity = null,
    string? WorkerHash = null, string? ResultHash = null, string? PublicationHash = null, string? Outcome = null, string? Reason = null);
public sealed record PhaseRecord(string State, string? ReservationId, string? EnvironmentRunId,
    decimal PhaseEstimateGbp, decimal TeardownReserveGbp, decimal ReservedTotalGbp, decimal? ActualCostGbp,
    ActualCostRecord? ActualCostSource, DateTimeOffset? ApplyStartedUtc, DateTimeOffset? ExpiresAt,
    RepeatRecord[] Repeats, PrivateExportReceipt? PrivateExport, DestroyReceipt? DestroyReceipt, GateReceipt? GateReceipt);
public sealed record LedgerDocument(int SchemaVersion, string Item, string Currency, long Revision, string SourceCommit,
    decimal BaselineSpendGbp, string[] BaselineSources, Dictionary<string, PhaseRecord> Phases);
