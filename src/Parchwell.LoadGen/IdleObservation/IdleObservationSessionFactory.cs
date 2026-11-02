using Confluent.Kafka;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;

namespace Parchwell.LoadGen.IdleObservation;

internal sealed class IdleObservationSessionFactory
{
    private readonly Func<IReadOnlyDictionary<string, string>, IdleObservationCapture, CancellationToken, IProducer<byte[], byte[]>> _producer;
    internal IdleObservationSessionFactory(KafkaAuthenticationFactory authentication, IdleObservationOptions options)
    {
        _producer = (settings, capture, cancellation) =>
        {
            KafkaOAuthBearerSession session = authentication.CreateSession(new(options.ScenarioId, options.RunId, KafkaClientRole.LoadGenProducer, settings["client.id"]), cancellation);
            return authentication.CreateProducerBuilder<byte[], byte[]>(new ProducerConfig(settings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)), session)
                .SetLogHandler((_, message) => capture.Log(message))
                .SetErrorHandler((_, error) => capture.Error(error))
                .SetStatisticsHandler((_, report) => capture.Statistics(report)).Build();
        };
    }
    internal IdleObservationSessionFactory(
        Func<IReadOnlyDictionary<string, string>, IdleObservationCapture, CancellationToken, IProducer<byte[], byte[]>> producer)
    { _producer = producer; }
    internal IProducer<byte[], byte[]> Producer(IReadOnlyDictionary<string, string> settings, IdleObservationCapture capture, CancellationToken token) => _producer(settings, capture, token);
}
