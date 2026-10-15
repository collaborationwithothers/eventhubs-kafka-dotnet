using Parchwell.Kafka.Configuration;

namespace Parchwell.Kafka.Statistics;

/// <summary>Identifies the scenario run and Kafka client that produced statistics evidence.</summary>
/// <param name="ScenarioId">The frozen inventory scenario identifier.</param>
/// <param name="RunId">The identifier of this execution of the scenario.</param>
/// <param name="Role">The baseline role of the Kafka client.</param>
/// <param name="ClientId">The configured Kafka client identifier.</param>
public sealed record KafkaStatisticsIdentity(
    string ScenarioId,
    string RunId,
    KafkaClientRole Role,
    string ClientId);
