using Parchwell.Kafka.Configuration;

namespace Parchwell.Kafka.Authentication;

/// <summary>Identifies the scenario run and Kafka client served by an authentication session.</summary>
/// <param name="ScenarioId">The frozen scenario identifier.</param>
/// <param name="RunId">The identifier of this scenario execution.</param>
/// <param name="Role">The baseline role of the Kafka client.</param>
/// <param name="ClientId">The configured Kafka client identifier.</param>
public sealed record KafkaAuthenticationIdentity(
    string ScenarioId,
    string RunId,
    KafkaClientRole Role,
    string ClientId);
