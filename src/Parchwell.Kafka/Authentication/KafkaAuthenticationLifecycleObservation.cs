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
public sealed record KafkaAuthenticationLifecycleEvent(
    int Sequence,
    int Attempt,
    string Phase,
    string Stage,
    DateTimeOffset? ObservedAt,
    long? Timestamp,
    DateTimeOffset? AcquiredExpiresOn,
    DateTimeOffset? InstalledExpiresOn,
    string? FailureReason);

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
