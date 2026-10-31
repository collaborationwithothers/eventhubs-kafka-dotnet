using System.Text.Json.Serialization;

namespace Parchwell.Kafka.Authentication;

/// <summary>Describes one bounded observation made while a native OAuth callback runs.</summary>
/// <param name="Sequence">The one-based sequence within the session snapshot.</param>
/// <param name="Attempt">The one-based callback attempt number.</param>
/// <param name="Phase">The existing session phase, either initial or refresh.</param>
/// <param name="Stage">The callback stage represented by this event.</param>
/// <param name="ObservedAt">The UTC wall-clock observation time, when available.</param>
/// <param name="Timestamp">The monotonic timestamp, when available.</param>
/// <param name="AcquiredExpiresOn">The expiry returned by acquisition for this attempt, when available.</param>
/// <param name="InstalledExpiresOn">The expiry currently installed in the native client session.</param>
/// <param name="FailureReason">An existing sanitized handler failure reason, only for failed events.</param>
/// <param name="EffectiveExpiresOn">The effective absolute expiry advertised to Kafka in capped diagnostic mode.</param>
/// <param name="CapComputedAt">The post-acquisition UTC time used to compute the advertised expiry.</param>
/// <param name="TestExpiryCapMilliseconds">The explicit test-only expiry cap, when enabled.</param>
public sealed record KafkaAuthenticationLifecycleEvent(
    int Sequence,
    int Attempt,
    string Phase,
    string Stage,
    DateTimeOffset? ObservedAt,
    long? Timestamp,
    DateTimeOffset? AcquiredExpiresOn,
    DateTimeOffset? InstalledExpiresOn,
    string? FailureReason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? EffectiveExpiresOn = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? CapComputedAt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TestExpiryCapMilliseconds = null);

/// <summary>Provides immutable callback progress without waiting for token acquisition to finish.</summary>
/// <param name="SuccessfulObservedInstallations">Successful capped installs with observed SDK invocation and return.</param>
/// <param name="LastAttempt">The callback attempt that produced the latest progress.</param>
/// <param name="ThirdSuccessfulAttempt">The attempt that reached the third qualifying installation, once reached.</param>
public sealed record KafkaHandlerRefreshProgress(
    int SuccessfulObservedInstallations,
    int LastAttempt,
    int? ThirdSuccessfulAttempt);

/// <summary>Returns a defensive snapshot of one session's explicitly enabled lifecycle observations.</summary>
/// <param name="Enabled">Whether lifecycle capture was explicitly enabled for the session.</param>
/// <param name="Complete">Whether the bounded snapshot has no known capture gap.</param>
/// <param name="IncompleteReason">The first applicable reason that the snapshot is incomplete.</param>
/// <param name="Identity">The Kafka client identity associated with the session.</param>
/// <param name="TimestampFrequency">The injected time provider's monotonic timestamp frequency.</param>
/// <param name="Events">A read-only copy of the captured events.</param>
public sealed record KafkaAuthenticationLifecycleSnapshot(
    bool Enabled,
    bool Complete,
    string? IncompleteReason,
    KafkaAuthenticationIdentity Identity,
    long TimestampFrequency,
    IReadOnlyList<KafkaAuthenticationLifecycleEvent> Events);
