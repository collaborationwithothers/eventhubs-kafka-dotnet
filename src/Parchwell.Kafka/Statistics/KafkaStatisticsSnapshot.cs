using System.Collections.Immutable;

namespace Parchwell.Kafka.Statistics;

/// <summary>Contains the validated, typed fields copied from one native statistics report.</summary>
/// <remarks>
/// Native timestamp meanings and broker field units follow librdkafka v2.16.0:
/// <see href="https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md">STATISTICS.md</see>.
/// </remarks>
/// <param name="Name">The native client handle name.</param>
/// <param name="ClientId">The configured or default native client identifier.</param>
/// <param name="Type">The native client type.</param>
/// <param name="TimestampMicroseconds">The native monotonic timestamp, in microseconds.</param>
/// <param name="TimeSeconds">The native wall-clock timestamp, in seconds since the epoch.</param>
/// <param name="AgeMicroseconds">Elapsed time since client creation, in microseconds.</param>
/// <param name="Brokers">The immutable broker series copied from the report.</param>
public sealed record KafkaStatisticsSnapshot(
    string Name,
    string ClientId,
    string Type,
    long TimestampMicroseconds,
    long TimeSeconds,
    long AgeMicroseconds,
    ImmutableArray<KafkaBrokerStatistics> Brokers);
