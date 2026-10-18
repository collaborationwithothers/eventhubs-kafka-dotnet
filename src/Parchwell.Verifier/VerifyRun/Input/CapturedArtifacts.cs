using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.VerifyRun.Input;

/// <summary>Describes one independently registered application produce attempt.</summary>
internal sealed record AttemptEntry(
    string InstanceId,
    string AttemptId,
    long Ordinal,
    string TenantId,
    string DocumentId,
    string Key,
    long Sequence,
    string EventType,
    long PlannedMicroseconds,
    long InvokedMicroseconds);

/// <summary>Retains one parsed ledger row without joining it to other evidence.</summary>
internal sealed record LedgerRow(
    int SchemaVersion,
    RunIdentity Identity,
    AttemptEntry Attempt,
    string CompletionSource,
    bool Delivered,
    int ErrorCode,
    ParsedField<string> ErrorName,
    ParsedField<int> PersistenceStatus,
    string PersistenceProvenance,
    long CompletedMicroseconds,
    ParsedField<double> DeliveryLatencyMs,
    ParsedField<double> SynchronousCallDurationMs);

/// <summary>Retains the independently sealed run inventory and producer declarations.</summary>
internal sealed record SealedRunManifest(
    int SchemaVersion,
    RunIdentity Identity,
    string GeneratorVersion,
    long PlannedAttemptCount,
    long AttemptCount,
    IReadOnlyList<AttemptEntry> Inventory,
    long TimestampFrequency,
    long StartTimestamp,
    string WorkerStatus,
    bool BookkeepingComplete,
    ParsedField<long> FirstUnexecutedOrdinal,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyDictionary<string, string> RuntimeVersions,
    IReadOnlyDictionary<string, string> EvidenceFiles,
    IReadOnlyList<string> GuardObservations);

/// <summary>Retains a producer-declared payload name, byte length, and digest string.</summary>
internal sealed record ArtifactDescriptor(string Name, long Length, string Sha256);

/// <summary>Retains the index declarations without comparing them to payloads.</summary>
internal sealed record ArtifactIndex(
    int SchemaVersion,
    RunIdentity Identity,
    string WorkerStatus,
    bool BookkeepingComplete,
    bool WorkerComplete,
    bool PublicationComplete,
    IReadOnlyList<ArtifactDescriptor> Artifacts);

/// <summary>Identifies a client in the captured statistics envelope.</summary>
internal sealed record StatisticsIdentity(string ScenarioId, string RunId, int Role, string ClientId);

/// <summary>Retains one producer-supplied diagnostics entry.</summary>
internal sealed record CapturedDiagnostic(string Code, ParsedField<string> Broker, ParsedField<string> Field);

/// <summary>Retains a statistics envelope and its raw native JSON string.</summary>
internal sealed record StatisticsEnvelope(
    int SchemaVersion,
    RunIdentity Identity,
    StatisticsIdentity StatisticsIdentity,
    string SessionId,
    string ReceivedAt,
    string ReceiptTime,
    string RawJson,
    ParsedField<TypedSnapshot> TypedSnapshot,
    IReadOnlyList<CapturedDiagnostic> Diagnostics);

/// <summary>Retains the typed statistics projection, which is not used to judge raw reports.</summary>
internal sealed record TypedSnapshot(
    string Name,
    string ClientId,
    string Type,
    long TimestampMicroseconds,
    long TimeSeconds,
    long AgeMicroseconds,
    IReadOnlyList<TypedBroker> Brokers);

/// <summary>Retains one broker's typed bridge projection.</summary>
internal sealed record TypedBroker(
    string Name,
    int NodeId,
    string State,
    long StateAgeMicroseconds,
    long RequestTimeouts,
    long Disconnects,
    long TransmissionRetries,
    long TransmissionErrors,
    long ReceiveErrors,
    CountValue Throttle,
    CountValue RoundTripTime);

/// <summary>Retains a sampled count and an optional sampled value.</summary>
internal sealed record CountValue(long Count, ParsedField<long> Value);

/// <summary>Common data for one captured request-lifecycle event.</summary>
internal abstract record LifecycleEvent(
    int SchemaVersion,
    RunIdentity Identity,
    string SessionId,
    long ReceiptSequence,
    string ReceivedAt,
    string EventType,
    string Provenance);

/// <summary>Retains a managed native-log callback record.</summary>
internal sealed record NativeLog(
    int SchemaVersion,
    RunIdentity Identity,
    string SessionId,
    long ReceiptSequence,
    string ReceivedAt,
    ParsedField<string> SourceEventTimestamp,
    ParsedField<string> CorrelationId,
    ParsedField<long> SourceOrder,
    IReadOnlyList<string> KnownMissingFields,
    ParsedField<string> Inference,
    string NativeName,
    string Level,
    string Facility,
    string Message,
    string Provenance)
    : LifecycleEvent(SchemaVersion, Identity, SessionId, ReceiptSequence, ReceivedAt, "native_log", Provenance);

/// <summary>Retains a managed native-error callback record.</summary>
internal sealed record NativeError(
    int SchemaVersion,
    RunIdentity Identity,
    string SessionId,
    long ReceiptSequence,
    string ReceivedAt,
    int ErrorCode,
    string ErrorName,
    ParsedField<string> Reason,
    bool IsError,
    bool IsFatal,
    string Provenance)
    : LifecycleEvent(SchemaVersion, Identity, SessionId, ReceiptSequence, ReceivedAt, "native_error", Provenance);

/// <summary>Retains a source reference attached to a captured setting.</summary>
internal sealed record Source(string Url, string VersionOrDate);

/// <summary>Retains a setting's fixed catalogue metadata as producer data.</summary>
internal sealed record SettingMetadata(
    string Key,
    ParsedField<string> Value,
    int Role,
    int Disposition,
    IReadOnlyList<string> InventoryIds,
    IReadOnlyList<Source> Sources,
    string SelectionReason);

/// <summary>Retains a location in a scenario or captured configuration source.</summary>
internal sealed record Location(string Path, ParsedField<int> Line, ParsedField<int> Column);

/// <summary>Retains a scenario-specific setting override declaration.</summary>
internal sealed record Override(string ScenarioId, string ConfigurationId, Location Location);

/// <summary>Retains a resolved value and its baseline and optional override metadata.</summary>
internal sealed record ResolvedSetting(string Value, SettingMetadata Baseline, ParsedField<Override> Override);

/// <summary>Retains a producer's resolved setting map and omissions.</summary>
internal sealed record ResolvedConfiguration(
    int Role,
    IReadOnlyDictionary<string, ResolvedSetting> Settings,
    IReadOnlyList<SettingMetadata> Omissions);

/// <summary>Retains one declared request-lifecycle capability requirement.</summary>
internal sealed record RequestLifecycleRequirement(
    string Requirement,
    string Status,
    string PinnedVersion,
    IReadOnlyList<string> SourceUrls,
    string SourceFacts,
    string Inference,
    string LocalTestEvidence);

/// <summary>Retains the producer's statistics completion declaration.</summary>
internal sealed record Completion(
    bool IsComplete,
    ParsedField<long> DrainBoundaryMicroseconds,
    IReadOnlyList<CapturedDiagnostic> Diagnostics,
    IReadOnlyList<string> MissingBrokers);

/// <summary>Retains the captured capability envelope as supplied.</summary>
internal sealed record CapturedCapabilities(
    int SchemaVersion,
    RunIdentity Identity,
    IReadOnlyList<string> Artifacts,
    ParsedField<ResolvedConfiguration> ResolvedConfiguration,
    IReadOnlyList<RequestLifecycleRequirement> RequestLifecycleRequirements,
    bool RequestCapabilitySupported,
    IReadOnlyList<string> ObservedBrokerNames,
    IReadOnlyList<string> GuardObservations,
    bool GuardObservationsComplete,
    ParsedField<Completion> StatisticsCompletion,
    IReadOnlyList<string> SessionDiagnostics,
    bool IsFaulted);

/// <summary>Retains one native broker statistics window.</summary>
internal sealed record NativeWindow(ParsedField<long> Count, ParsedField<long> Maximum, ParsedField<long> P99);

/// <summary>Retains selected native broker fields and the independent report dictionary key.</summary>
internal sealed record NativeBrokerReport(
    string DictionaryKey,
    ParsedField<string> Name,
    ParsedField<long> NodeId,
    ParsedField<string> Source,
    ParsedField<string> State,
    ParsedField<long> RequestTimeouts,
    ParsedField<long> TransmissionRetries,
    ParsedField<long> TransmissionErrors,
    ParsedField<long> ReceiveErrors,
    ParsedField<long> Disconnects,
    ParsedField<NativeWindow> Throttle,
    ParsedField<NativeWindow> RoundTripTime);

/// <summary>Retains the selected native report projection with original field presence.</summary>
internal sealed record NativeClientReport(
    ParsedField<string> Name,
    ParsedField<string> ClientId,
    ParsedField<string> Type,
    ParsedField<long> TimestampMicroseconds,
    ParsedField<long> TimeSeconds,
    ParsedField<long> AgeMicroseconds,
    ParsedField<IReadOnlyDictionary<string, NativeBrokerReport>> Brokers);
