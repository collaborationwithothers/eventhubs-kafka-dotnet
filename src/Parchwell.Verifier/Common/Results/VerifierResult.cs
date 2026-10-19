using Parchwell.Verifier.Common.Contracts;

namespace Parchwell.Verifier.Common.Results;

/// <summary>Retains the output status supplied by verifier judgement.</summary>
internal enum VerifierResultStatus
{
    CompleteValid,
    Invalid,
    Incomplete
}

/// <summary>Contains the explicit result projection for a verifier run.</summary>
/// <remarks>Status, failed checks, measurements, and counts are supplied values; this record computes none of them.</remarks>
internal sealed record VerifierResult(
    int SchemaVersion,
    RunIdentity Identity,
    string? GeneratorVersion,
    IReadOnlyDictionary<string, string>? RuntimeVersions,
    string VerifierCommit,
    string VerifierConfluentKafkaVersion,
    string VerifierLibrdkafkaVersion,
    VerifierResultStatus Status,
    IReadOnlyList<int> FailedChecks,
    IReadOnlyList<WatermarkOffset>? StartLine,
    IReadOnlyList<WatermarkOffset>? EndLine,
    decimal? DrainTimeMilliseconds,
    decimal? DrainElapsedAtStopMilliseconds,
    OutcomeCounts? Counts,
    BrokerConnectionCount? BrokerCount,
    ConfigurationFidelityResult? ConfigurationFidelity,
    ProbeRepeatObservation? ProbeObservation,
    A01RecordedEvidence? A01RecordedEvidence,
    IReadOnlyList<JudgedFileHash> JudgedFileHashes,
    IReadOnlyList<ResultDiagnostic> Diagnostics);

/// <summary>Records an offset at a partition boundary.</summary>
internal sealed record WatermarkOffset(int Partition, long Offset);

/// <summary>Contains the six verifier outcome totals.</summary>
internal sealed record OutcomeCounts(
    long Ok,
    long Loss,
    long Phantom,
    long ReportedFailure,
    long Duplicate,
    long Reorder);

/// <summary>Contains the supplied broker connection measurements for one client.</summary>
internal sealed record BrokerConnectionCount(
    string Role,
    string ClientId,
    long? Maximum,
    long? FirstPostDrain,
    bool LowerBound);

/// <summary>Contains the supplied configuration comparison and bounded differences.</summary>
internal sealed record ConfigurationFidelityResult(
    CheckAssessment Assessment,
    IReadOnlyList<SettingDifference> Differences);

/// <summary>Records one supplied setting difference.</summary>
internal sealed record SettingDifference(string Setting, string? Expected, string? Actual);

/// <summary>Contains the supplied result of one probe repeat, independent of run status.</summary>
internal sealed record ProbeRepeatObservation(
    bool Usable,
    bool Qualifying,
    string? Broker,
    long? ReportIndex,
    long? WindowCount,
    long? WindowMaximum);

/// <summary>Contains bounded recorded evidence for the A01 scenario.</summary>
internal sealed record A01RecordedEvidence(
    IReadOnlyList<int>? DeliveryErrorCodes,
    IReadOnlyList<A01WindowEvidence>? Windows);

/// <summary>Records supplied increments and selected native values for one A01 window.</summary>
internal sealed record A01WindowEvidence(
    string Broker,
    long ReportIndex,
    long NativeTimestamp,
    long? RequestTimeoutsIncrement,
    long? DisconnectsIncrement,
    long? ThrottleMaximumMilliseconds,
    long? RoundTripP99Microseconds);

/// <summary>Copies one judged artifact's declared name and digest.</summary>
internal sealed record JudgedFileHash(string Name, string Sha256);

/// <summary>Contains one bounded diagnostic supplied for a numbered check.</summary>
internal sealed record ResultDiagnostic(
    int Check,
    string Code,
    string? Artifact,
    long? Row,
    string? Field,
    string? Snippet);
