namespace Parchwell.Kafka.Tests.PhantomSpike;

internal enum SpikeFault { None, Latency, Timeout, ResetPeer }

internal sealed record TimingSettings(int MessageTimeoutMs = 5000, int SocketTimeoutMs = 2000, int LatencyMs = 10000);

internal sealed record TrialConfiguration(int Retries, SpikeFault Fault, int Repeat);

internal sealed record AttemptMessage(string Topic, string InstanceId, string AttemptId, byte[] Key, byte[] Payload);

internal sealed record HeaderObservation(string Key, byte[]? Value);

internal sealed record DeliveryObservation(DateTimeOffset Timestamp, string Topic, int Partition, long Offset,
    bool IsError, string ErrorCode, string ErrorReason, string PersistenceStatus, string InstanceId, string AttemptId,
    byte[]? Key = null, byte[]? Payload = null, IReadOnlyList<HeaderObservation>? Headers = null);

internal sealed record ConsumedObservation(DateTimeOffset Timestamp, string Topic, int Partition, long Offset,
    string? InstanceId, string? AttemptId, byte[] Key, byte[] Payload, IReadOnlyList<HeaderObservation>? Headers = null);

internal sealed record ObservationScan(long Start, long End, long Position, bool Complete,
    IReadOnlyList<ConsumedObservation> Records, string? Failure);

internal sealed record ProducerDiagnostics(string ManagedVersion, string NativeVersion,
    IReadOnlyList<string> Logs, IReadOnlyList<string> Statistics, IReadOnlyList<string> Errors);

internal sealed record InfrastructureDiagnostics(string ProxyState, string Metrics, string KafkaLogs, string ProxyLogs,
    string? MetricsBeforeTarget = null);

internal sealed record TrialEvidence(string TrialId, TrialConfiguration Configuration, TimingSettings Timings,
    AttemptMessage Target, bool Accepted, string? SynchronousError, IReadOnlyList<DeliveryObservation> Callbacks,
    ObservationScan? Scan, IReadOnlyList<string> AdvertisedBrokers, string? FaultState,
    ProducerDiagnostics? Producer, InfrastructureDiagnostics? Infrastructure,
    IReadOnlyList<string> Failures, bool CleanupSucceeded, DateTimeOffset Started, DateTimeOffset Sealed,
    string? ExpectedProducerEndpoint = null, bool RouteVerified = false, bool FaultVerified = false,
    bool HealthyControlVerified = false, bool IndependentObserver = false,
    DateTimeOffset? FaultActivated = null, DateTimeOffset? Submitted = null,
    AttemptMessage? WarmupTarget = null, IReadOnlyList<DeliveryObservation>? WarmupCallbacks = null,
    ObservationScan? WarmupScan = null);

internal sealed record ExperimentEvidence(string SourceCommit, bool Dirty, DateTimeOffset Started,
    DateTimeOffset Finished, TimingSettings Timings, IReadOnlyList<TrialEvidence> Trials,
    IReadOnlyList<TrialEvidence> CalibrationTrials, IReadOnlyList<string> Failures, bool CleanupSucceeded);
