using Confluent.Kafka;

namespace Parchwell.Enrichment.ConsumerProcessing;

internal delegate IConsumer<byte[], byte[]> ConsumerProcessingConsumerFactory(
    ConsumerConfig configuration, ConsumerProcessingHandlers handlers);

internal sealed record ConsumerProcessingHandlers(
    Action<IConsumer<byte[], byte[]>, List<TopicPartition>> Assigned,
    Action<IConsumer<byte[], byte[]>, List<TopicPartition>> Revoked,
    Action<IConsumer<byte[], byte[]>, List<TopicPartition>> Lost,
    Action<IConsumer<byte[], byte[]>, CommittedOffsets> OffsetsCommitted,
    Action<IConsumer<byte[], byte[]>, Error>? ErrorReported = null);

internal static class ConsumerProcessingBuilderConfiguration
{
    // Sets the handlers the worker needs. The error handler is set only when the worker
    // supplies a callback: Confluent.Kafka invokes none when none is set.
    internal static ConsumerBuilder<byte[], byte[]> SetHandlers(ConsumerBuilder<byte[], byte[]> builder,
        ConsumerProcessingHandlers handlers, Action<IConsumer<byte[], byte[]>, string> statisticsHandler)
    {
        builder.SetStatisticsHandler(statisticsHandler)
            .SetPartitionsAssignedHandler(handlers.Assigned)
            .SetPartitionsRevokedHandler((consumer, partitions) => handlers.Revoked(consumer, partitions.Select(partition => partition.TopicPartition).ToList()))
            .SetPartitionsLostHandler((consumer, partitions) => handlers.Lost(consumer, partitions.Select(partition => partition.TopicPartition).ToList()))
            .SetOffsetsCommittedHandler(handlers.OffsetsCommitted);
        if (handlers.ErrorReported is { } errorReported) builder.SetErrorHandler(errorReported);
        return builder;
    }
}
