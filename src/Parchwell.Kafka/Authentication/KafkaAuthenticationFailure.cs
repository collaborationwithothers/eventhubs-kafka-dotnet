namespace Parchwell.Kafka.Authentication;

/// <summary>Describes a sanitized token refresh failure for one Kafka client.</summary>
/// <param name="Identity">The scenario run and Kafka client associated with the failure.</param>
/// <param name="Phase">Whether this occurred before or after the first successful token installation.</param>
/// <param name="Reason">The fixed failure category.</param>
public sealed record KafkaAuthenticationFailure(
    KafkaAuthenticationIdentity Identity,
    string Phase,
    string Reason);
