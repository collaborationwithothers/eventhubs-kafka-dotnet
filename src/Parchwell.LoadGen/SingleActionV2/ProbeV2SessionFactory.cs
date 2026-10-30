using Confluent.Kafka;
using Parchwell.Kafka.Authentication;

namespace Parchwell.LoadGen.SingleActionV2;

internal sealed class ProbeV2SessionFactory
{
    private readonly Func<IReadOnlyDictionary<string, string>, ProbeV2Capture, CancellationToken, IProducer<byte[], byte[]>> producer;
    private readonly Func<IReadOnlyDictionary<string, string>, ProbeV2Capture, CancellationToken, (IConsumer<byte[], byte[]> Client, KafkaOAuthBearerSession Authentication)> consumer;
    private readonly Func<IClient, string, TimeSpan, int[]> metadata;
    private readonly Func<Func<ProjectedTokenFileObservation>> createProjectedFileSampler;
    internal ProbeV2SessionFactory(KafkaAuthenticationFactory authentication, ProbeV2Options options)
    {
        ProbeV2Stage stage = ProbeV2Stage.For(options.ScenarioId, options.ConfigurationId, options.Stage);
        producer = (settings, capture, cancellation) =>
        {
            KafkaOAuthBearerSession session = capture.CaptureAuthenticationMetadata
                ? authentication.CreateObservedSession(new(options.ScenarioId, options.RunId, stage.Role, settings["client.id"]), cancellation)
                : authentication.CreateSession(new(options.ScenarioId, options.RunId, stage.Role, settings["client.id"]), cancellation);
            capture.AttachAuthentication(session);
            return authentication.CreateProducerBuilder<byte[], byte[]>(new ProducerConfig(settings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)), session)
                .SetLogHandler((_, value) => capture.Log(value)).SetErrorHandler((_, value) => capture.Error(value))
                .SetStatisticsHandler((_, value) => capture.Statistics(value)).Build();
        };
        consumer = (settings, capture, cancellation) =>
        {
            KafkaOAuthBearerSession session = capture.CaptureAuthenticationMetadata
                ? authentication.CreateObservedSession(new(options.ScenarioId, options.RunId, stage.Role, settings["client.id"]), cancellation)
                : authentication.CreateSession(new(options.ScenarioId, options.RunId, stage.Role, settings["client.id"]), cancellation);
            capture.AttachAuthentication(session);
            IConsumer<byte[], byte[]> client = authentication.CreateConsumerBuilder<byte[], byte[]>(new ConsumerConfig(settings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)), session)
                .SetLogHandler((_, value) => capture.Log(value)).SetErrorHandler((_, value) => capture.Error(value))
                .SetStatisticsHandler((_, value) => capture.Statistics(value)).Build();
            return (client, session);
        };
        metadata = (client, topic, timeout) =>
        {
            using IAdminClient admin = new DependentAdminClientBuilder(client.Handle).Build();
            Metadata result = admin.GetMetadata(topic, timeout);
            TopicMetadata selected = result.Topics.Single(value => value.Topic == topic);
            if (selected.Error.IsError) throw new KafkaException(selected.Error);
            if (selected.Partitions.Any(value => value.Error.IsError)) throw new KafkaException(selected.Partitions.First(value => value.Error.IsError).Error);
            return selected.Partitions.Select(value => value.PartitionId).Order().ToArray();
        };
        createProjectedFileSampler = () => authentication.CreateProjectedTokenFileObserver().Observe;
    }
    internal ProbeV2SessionFactory(
        Func<IReadOnlyDictionary<string, string>, ProbeV2Capture, CancellationToken, IProducer<byte[], byte[]>> producer,
        Func<IReadOnlyDictionary<string, string>, ProbeV2Capture, CancellationToken, (IConsumer<byte[], byte[]> Client, KafkaOAuthBearerSession Authentication)> consumer,
        Func<IClient, string, TimeSpan, int[]> metadata,
        Func<Func<ProjectedTokenFileObservation>>? projectedFileSampler = null)
    {
        this.producer = producer; this.consumer = consumer; this.metadata = metadata;
        createProjectedFileSampler = projectedFileSampler ?? (() => throw new InvalidOperationException("v2_projected_sampler_unavailable"));
    }
    internal IProducer<byte[], byte[]> Producer(IReadOnlyDictionary<string, string> settings, ProbeV2Capture capture, CancellationToken token) => producer(settings, capture, token);
    internal (IConsumer<byte[], byte[]> Client, KafkaOAuthBearerSession Authentication) Consumer(IReadOnlyDictionary<string, string> settings, ProbeV2Capture capture, CancellationToken token) => consumer(settings, capture, token);
    internal int[] Metadata(IClient client, string topic, TimeSpan timeout) => metadata(client, topic, timeout);
    internal Func<ProjectedTokenFileObservation> CreateProjectedFileSampler() => createProjectedFileSampler();
}
