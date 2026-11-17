using Confluent.Kafka;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Common.Monitoring;

namespace Parchwell.LoadGen.IdleObservation;

internal sealed class IdleObservationSessionFactory
{
    private readonly Func<IReadOnlyDictionary<string, string>, IdleObservationCapture, CancellationToken, IProducer<byte[], byte[]>> _producer;
    internal IdleObservationSessionFactory(KafkaAuthenticationFactory authentication, IdleObservationOptions options, NativeMonitoringHost? monitoring = null,
        Func<ProducerBuilder<byte[], byte[]>, IProducer<byte[], byte[]>>? buildProducer = null)
    {
        buildProducer ??= builder => builder.Build();
        _producer = (settings, capture, cancellation) =>
        {
            KafkaNativeMonitoringLifetime? lifetime = monitoring?.Begin(new(options.ScenarioId, options.RunId, KafkaClientRole.LoadGenProducer, settings["client.id"]));
            Action<string> statistics = NativeMonitoringCallback.Create(capture.TryStatistics, monitoring, lifetime);
            try
            {
            KafkaOAuthBearerSession session = authentication.CreateSession(new(options.ScenarioId, options.RunId, KafkaClientRole.LoadGenProducer, settings["client.id"]), cancellation);
            IProducer<byte[], byte[]> client = buildProducer(authentication.CreateProducerBuilder<byte[], byte[]>(new ProducerConfig(settings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)), session)
                .SetLogHandler((_, message) => capture.Log(message))
                .SetErrorHandler((_, error) => capture.Error(error))
                .SetStatisticsHandler((_, report) => statistics(report)));
            lifetime?.MarkBuilt();
            return client;
            }
            catch { lifetime?.MarkBuildFailed(); throw; }
        };
    }
    internal IdleObservationSessionFactory(
        Func<IReadOnlyDictionary<string, string>, IdleObservationCapture, CancellationToken, IProducer<byte[], byte[]>> producer)
    { _producer = producer; }
    internal IProducer<byte[], byte[]> Producer(IReadOnlyDictionary<string, string> settings, IdleObservationCapture capture, CancellationToken token) => _producer(settings, capture, token);
}
