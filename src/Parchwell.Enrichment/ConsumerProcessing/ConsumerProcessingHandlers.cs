using Confluent.Kafka;

namespace Parchwell.Enrichment.ConsumerProcessing;

internal delegate IConsumer<byte[], byte[]> ConsumerProcessingConsumerFactory(
    ConsumerConfig configuration, ConsumerProcessingHandlers handlers);

internal sealed record ConsumerProcessingHandlers(
    Action<IConsumer<byte[], byte[]>, List<TopicPartition>> Assigned,
    Action<IConsumer<byte[], byte[]>, List<TopicPartition>> Revoked,
    Action<IConsumer<byte[], byte[]>, List<TopicPartition>> Lost,
    Action<IConsumer<byte[], byte[]>, CommittedOffsets> OffsetsCommitted);
