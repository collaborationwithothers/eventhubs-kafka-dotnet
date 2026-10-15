namespace Parchwell.Kafka.Tests.PhantomSpike;

/// <summary>
/// The Toxiproxy toxic applied downstream between the producer and the broker, or None for a
/// healthy control. These are TCP faults, not Kafka acknowledgement selectors
/// (docs/operations/phantom-spike/README.md, Frozen experiment).
/// </summary>
internal enum SpikeFault { None, Latency, Timeout, ResetPeer }

/// <summary>
/// The frozen timings every trial must use. Approved estimates dated 2026-10-14, not measurements
/// (docs/operations/phantom-spike/README.md, Frozen experiment).
/// </summary>
internal sealed record TimingSettings(int MessageTimeoutMs = 5000, int SocketTimeoutMs = 2000, int LatencyMs = 10000);

/// <summary>One cell of the approved matrix: a retries value, a fault and a repeat number.</summary>
internal sealed record TrialConfiguration(int Retries, SpikeFault Fault, int Repeat);

/// <summary>
/// The message one Produce call submits. Instance and attempt IDs travel as headers so a callback
/// or a consumed record can be attributed to this exact attempt (ADR-012 identity join).
/// </summary>
internal sealed record AttemptMessage(string Topic, string InstanceId, string AttemptId, byte[] Key, byte[] Payload);

internal sealed record HeaderObservation(string Key, byte[]? Value);

/// <summary>A delivery callback as the producer reported it, with its persistence status and the returned bytes.</summary>
internal sealed record DeliveryObservation(DateTimeOffset Timestamp, string Topic, int Partition, long Offset,
    bool IsError, string ErrorCode, string ErrorReason, string PersistenceStatus, string InstanceId, string AttemptId,
    byte[]? Key = null, byte[]? Payload = null, IReadOnlyList<HeaderObservation>? Headers = null);

/// <summary>A record read by the independent observer, which reads the broker directly, not through the proxy.</summary>
internal sealed record ConsumedObservation(DateTimeOffset Timestamp, string Topic, int Partition, long Offset,
    string? InstanceId, string? AttemptId, byte[] Key, byte[] Payload, IReadOnlyList<HeaderObservation>? Headers = null);

/// <summary>
/// A bounded read of one partition from Start up to the End high watermark captured when the scan
/// began. Complete is true only when Position reached End; an empty poll never counts.
/// </summary>
internal sealed record ObservationScan(long Start, long End, long Position, bool Complete,
    IReadOnlyList<ConsumedObservation> Records, string? Failure);

internal sealed record ProducerDiagnostics(string ManagedVersion, string NativeVersion,
    IReadOnlyList<string> Logs, IReadOnlyList<string> Statistics, IReadOnlyList<string> Errors);

internal sealed record InfrastructureDiagnostics(string ProxyState, string Metrics, string KafkaLogs, string ProxyLogs,
    string? MetricsBeforeTarget = null, IReadOnlyDictionary<string, string>? ContainerIds = null);

/// <summary>
/// Everything retained for one trial. Executed is false for placeholder entries written when the
/// matrix stopped early; those are not observations and carry no reproduction rate.
/// </summary>
internal sealed record TrialEvidence(string TrialId, TrialConfiguration Configuration, TimingSettings Timings,
    AttemptMessage Target, bool Accepted, string? SynchronousError, IReadOnlyList<DeliveryObservation> Callbacks,
    ObservationScan? Scan, IReadOnlyList<string> AdvertisedBrokers, string? FaultState,
    ProducerDiagnostics? Producer, InfrastructureDiagnostics? Infrastructure,
    IReadOnlyList<string> Failures, bool CleanupSucceeded, DateTimeOffset Started, DateTimeOffset Sealed,
    string? ExpectedProducerEndpoint = null, bool RouteVerified = false, bool FaultVerified = false,
    bool HealthyControlVerified = false, bool IndependentObserver = false,
    DateTimeOffset? FaultActivated = null, DateTimeOffset? Submitted = null,
    AttemptMessage? WarmupTarget = null, IReadOnlyList<DeliveryObservation>? WarmupCallbacks = null,
    ObservationScan? WarmupScan = null, bool Executed = true);

/// <summary>
/// The full record of one spike run, written as experiment.json. ImagePins are the configured image
/// references, not observed image identities (docs/operations/phantom-spike/README.md).
/// </summary>
internal sealed record ExperimentEvidence(string SourceCommit, bool Dirty, DateTimeOffset Started,
    DateTimeOffset Finished, TimingSettings Timings, IReadOnlyList<TrialEvidence> Trials,
    IReadOnlyList<TrialEvidence> CalibrationTrials, IReadOnlyList<string> Failures, bool CleanupSucceeded,
    IReadOnlyDictionary<string, string>? ImagePins = null);
