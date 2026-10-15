namespace Parchwell.Kafka.Statistics;

/// <summary>Receives immutable native statistics reports for durable harness evidence.</summary>
public interface IKafkaStatisticsEvidenceSink
{
    /// <summary>Writes one received report and its original raw JSON.</summary>
    /// <param name="report">The immutable report to retain.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A value task that completes when the report write completes.</returns>
    ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken);
}
