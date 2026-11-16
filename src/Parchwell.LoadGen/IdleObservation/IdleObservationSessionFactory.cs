using Confluent.Kafka;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Monitoring;

namespace Parchwell.LoadGen.IdleObservation;

internal sealed class IdleObservationSessionFactory
{
    private readonly Func<IReadOnlyDictionary<string, string>, IdleObservationCapture, CancellationToken, IProducer<byte[], byte[]>> _producer;
    internal IdleObservationSessionFactory(KafkaAuthenticationFactory authentication, IdleObservationOptions options, NativeMonitoringHost? monitoring = null)
    {
        _producer = (settings, capture, cancellation) =>
        {
            KafkaNativeMonitoringLifetime? lifetime = monitoring?.Begin(new(options.ScenarioId, options.RunId, KafkaClientRole.LoadGenProducer, settings["client.id"]));
            try
            {
            KafkaOAuthBearerSession session = authentication.CreateSession(new(options.ScenarioId, options.RunId, KafkaClientRole.LoadGenProducer, settings["client.id"]), cancellation);
            IProducer<byte[], byte[]> client = authentication.CreateProducerBuilder<byte[], byte[]>(new ProducerConfig(settings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)), session)
                .SetLogHandler((_, message) => capture.Log(message))
                .SetErrorHandler((_, error) => capture.Error(error))
                .SetStatisticsHandler((_, report) => { if (capture.TryStatistics(report, out long sequence) && lifetime is not null) monitoring!.Capture(lifetime, report, sequence); }).Build();
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
