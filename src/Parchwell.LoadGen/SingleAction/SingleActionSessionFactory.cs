using Confluent.Kafka;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Common.Monitoring;

namespace Parchwell.LoadGen.SingleAction;

internal sealed class SingleActionSessionFactory
{
    private readonly Func<IReadOnlyDictionary<string, string>, SingleActionCapture, CancellationToken, IProducer<byte[], byte[]>> _producer;
    private readonly Func<IReadOnlyDictionary<string, string>, SingleActionCapture, CancellationToken, IConsumer<byte[], byte[]>> _consumer;
    internal SingleActionSessionFactory(KafkaAuthenticationFactory authentication, SingleActionHostOptions options, KafkaClientRole role, NativeMonitoringHost? monitoring = null,
        Func<ProducerBuilder<byte[], byte[]>, IProducer<byte[], byte[]>>? buildProducer = null,
        Func<ConsumerBuilder<byte[], byte[]>, IConsumer<byte[], byte[]>>? buildConsumer = null)
    {
        buildProducer ??= builder => builder.Build();
        buildConsumer ??= builder => builder.Build();
        _producer = (settings, capture, cancellation) =>
        {
            KafkaNativeMonitoringLifetime? lifetime = monitoring?.Begin(new(options.ScenarioId, options.RunId, role, settings["client.id"]));
            Action<string> statistics = NativeMonitoringCallback.Create(capture.TryStatistics, monitoring, lifetime);
            try
            {
            KafkaOAuthBearerSession session = authentication.CreateSession(new(options.ScenarioId, options.RunId, role, settings["client.id"]), cancellation);
            IProducer<byte[], byte[]> client = buildProducer(authentication.CreateProducerBuilder<byte[], byte[]>(new ProducerConfig(settings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)), session)
                .SetLogHandler((_, message) => capture.Log(message))
                .SetErrorHandler((_, error) => capture.Error(error))
                .SetStatisticsHandler((_, report) => statistics(report)));
            lifetime?.MarkBuilt();
            return client;
            }
            catch { lifetime?.MarkBuildFailed(); throw; }
        };
        _consumer = (settings, capture, cancellation) =>
        {
            KafkaNativeMonitoringLifetime? lifetime = monitoring?.Begin(new(options.ScenarioId, options.RunId, role, settings["client.id"]));
            Action<string> statistics = NativeMonitoringCallback.Create(capture.TryStatistics, monitoring, lifetime);
            try
            {
            KafkaOAuthBearerSession session = authentication.CreateSession(new(options.ScenarioId, options.RunId, role, settings["client.id"]), cancellation);
            IConsumer<byte[], byte[]> client = buildConsumer(authentication.CreateConsumerBuilder<byte[], byte[]>(new ConsumerConfig(settings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)), session)
                .SetLogHandler((_, message) => capture.Log(message))
                .SetErrorHandler((_, error) => capture.Error(error))
                .SetStatisticsHandler((_, report) => statistics(report))
                .SetPartitionsAssignedHandler((_, assigned) => capture.AssignedPartitions(assigned.Count)));
            lifetime?.MarkBuilt();
            return client;
            }
            catch { lifetime?.MarkBuildFailed(); throw; }
        };
    }
    internal SingleActionSessionFactory(
        Func<IReadOnlyDictionary<string, string>, SingleActionCapture, CancellationToken, IProducer<byte[], byte[]>> producer,
        Func<IReadOnlyDictionary<string, string>, SingleActionCapture, CancellationToken, IConsumer<byte[], byte[]>> consumer)
    { _producer = producer; _consumer = consumer; }
    internal IProducer<byte[], byte[]> Producer(IReadOnlyDictionary<string, string> settings, SingleActionCapture capture, CancellationToken token) => _producer(settings, capture, token);
    internal IConsumer<byte[], byte[]> Consumer(IReadOnlyDictionary<string, string> settings, SingleActionCapture capture, CancellationToken token) => _consumer(settings, capture, token);
}
