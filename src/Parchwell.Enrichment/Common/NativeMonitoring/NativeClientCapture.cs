using Confluent.Kafka;
using Parchwell.Kafka.Statistics;

namespace Parchwell.Enrichment.Common.NativeMonitoring;

public sealed class NativeClientCapture(KafkaNativeMonitoringCoordinator coordinator)
{
    private KafkaNativeMonitoringLifetime? lifetime;

    public KafkaNativeMonitoringLifetime Begin(KafkaStatisticsIdentity identity) =>
        lifetime = coordinator.CreateLifetime(identity);

    internal static Action<IProducer<byte[], byte[]>, string> ProducerHandler(KafkaNativeMonitoringLifetime lifetime) =>
        (_, raw) => lifetime.CaptureNew(raw);

    internal static Action<IConsumer<byte[], byte[]>, string> ConsumerHandler(KafkaNativeMonitoringLifetime lifetime) =>
        (_, raw) => lifetime.CaptureNew(raw);

    public void Complete(bool success) => lifetime?.Complete(success, "unknown");
}
