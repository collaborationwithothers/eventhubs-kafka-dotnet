using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;

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
    IReadOnlyList<ResultDiagnostic> Diagnostics,
    WorkloadObservation? WorkloadObservation = null,
    B04RecordedEvidence? B04RecordedEvidence = null,
    BatchingRecordedEvidence? BatchingRecordedEvidence = null,
    B10IntakeEvidence? B10Intake = null,
    bool? ClassificationFinal = null,
    bool? B10WindowChecksComplete = null);

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

/// <summary>Contains bounded recorded evidence for the B04 scenario.</summary>
internal sealed record B04RecordedEvidence(IReadOnlyList<int>? DeliveryErrorCodes, IReadOnlyList<B04FatalError>? FatalErrors);

/// <summary>Records one distinct fatal client error supplied by captured lifecycle evidence.</summary>
internal sealed record B04FatalError(int Code, string Name);

/// <summary>Contains bounded recorded latency, Produce-request and batch evidence for the B06 and B07 scenarios.</summary>
/// <remarks>Latencies are microseconds. The five latency fields are all present or all absent, and so are the two batch fields.</remarks>
internal sealed record BatchingRecordedEvidence(long? DeliveredLatencyCount, long? LatencyMinimumMicroseconds, long? LatencyP50Microseconds,
    long? LatencyP95Microseconds, long? LatencyMaximumMicroseconds, long? ProduceRequests, long? BatchCount, long? BatchMaximumMessages)
{
    /// <summary>The wire names, in record order.</summary>
    internal static readonly string[] FieldNames = ["delivered_latency_count", "latency_min_us", "latency_p50_us", "latency_p95_us",
        "latency_max_us", "produce_requests", "batch_count", "batch_max_messages"];

    /// <summary>Builds the record from values in <see cref="FieldNames"/> order.</summary>
    internal static BatchingRecordedEvidence FromValues(IReadOnlyList<long?> v) => new(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7]);

    /// <summary>Writes the present fields into the open JSON object.</summary>
    internal void WriteFields(Utf8JsonWriter writer)
    {
        long?[] values = [DeliveredLatencyCount, LatencyMinimumMicroseconds, LatencyP50Microseconds, LatencyP95Microseconds,
            LatencyMaximumMicroseconds, ProduceRequests, BatchCount, BatchMaximumMessages];
        for (int index = 0; index < values.Length; index++)
            if (values[index] is long present) writer.WriteNumber(FieldNames[index], present);
    }

    /// <summary>Returns whether the values satisfy the passive codec bounds.</summary>
    internal bool HasValidShape()
    {
        if (ProduceRequests is < 0 || BatchCount is null != BatchMaximumMessages is null) return false;
        if (BatchCount is long batches && (batches < 0 || BatchMaximumMessages < 0 || (batches == 0) != (BatchMaximumMessages == 0))) return false;
        long?[] latency = [DeliveredLatencyCount, LatencyMinimumMicroseconds, LatencyP50Microseconds, LatencyP95Microseconds, LatencyMaximumMicroseconds];
        if (latency.All(value => value is null)) return true;
        return latency.All(value => value is not null) && DeliveredLatencyCount >= 1 && LatencyMinimumMicroseconds >= 0 &&
            LatencyMinimumMicroseconds <= LatencyP50Microseconds && LatencyP50Microseconds <= LatencyP95Microseconds &&
            LatencyP95Microseconds <= LatencyMaximumMicroseconds;
    }
}

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

/// <summary>Record-only synthetic workload totals; never an input to validity or gate decisions.</summary>
internal sealed record WorkloadObservation(bool Synthetic, long? PlannedAttempts, long? InvokedAttempts,
    long? TerminalObservations, bool? FixedProfileComplete, long? FirstUnexecutedOrdinal,
    IReadOnlyList<WorkloadStageObservation>? Stages);

internal sealed record WorkloadStageObservation(string Id, long OfferedAttemptsPerSecond, long DurationSeconds,
    long? InvokedAttempts, long? DeliveredObservations, long? FailedObservations);
