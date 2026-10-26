using Confluent.Kafka;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;

namespace Parchwell.LoadGen.SingleAction;

internal sealed class SingleActionSessionFactory
{
    private readonly Func<IReadOnlyDictionary<string, string>, SingleActionCapture, CancellationToken, IProducer<byte[], byte[]>> _producer;
    private readonly Func<IReadOnlyDictionary<string, string>, SingleActionCapture, CancellationToken, IConsumer<byte[], byte[]>> _consumer;
    internal SingleActionSessionFactory(KafkaAuthenticationFactory authentication, SingleActionHostOptions options, KafkaClientRole role)
    {
        _producer = (settings, capture, cancellation) =>
        {
            KafkaOAuthBearerSession session = authentication.CreateSession(new(options.ScenarioId, options.RunId, role, settings["client.id"]), cancellation);
            return authentication.CreateProducerBuilder<byte[], byte[]>(new ProducerConfig(settings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)), session)
                .SetErrorHandler((_, error) => capture.Error(error))
                .SetStatisticsHandler((_, report) => capture.Statistics(report)).Build();
        };
        _consumer = (settings, capture, cancellation) =>
        {
            KafkaOAuthBearerSession session = authentication.CreateSession(new(options.ScenarioId, options.RunId, role, settings["client.id"]), cancellation);
            return authentication.CreateConsumerBuilder<byte[], byte[]>(new ConsumerConfig(settings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)), session)
                .SetErrorHandler((_, error) => capture.Error(error))
                .SetStatisticsHandler((_, report) => capture.Statistics(report))
                .SetPartitionsAssignedHandler((_, assigned) => capture.AssignedPartitions(assigned.Count)).Build();
        };
    }
    internal SingleActionSessionFactory(
        Func<IReadOnlyDictionary<string, string>, SingleActionCapture, CancellationToken, IProducer<byte[], byte[]>> producer,
        Func<IReadOnlyDictionary<string, string>, SingleActionCapture, CancellationToken, IConsumer<byte[], byte[]>> consumer)
    { _producer = producer; _consumer = consumer; }
    internal IProducer<byte[], byte[]> Producer(IReadOnlyDictionary<string, string> settings, SingleActionCapture capture, CancellationToken token) => _producer(settings, capture, token);
    internal IConsumer<byte[], byte[]> Consumer(IReadOnlyDictionary<string, string> settings, SingleActionCapture capture, CancellationToken token) => _consumer(settings, capture, token);
}
