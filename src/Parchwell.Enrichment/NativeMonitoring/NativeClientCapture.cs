using Parchwell.Kafka.Statistics;

namespace Parchwell.Enrichment.NativeMonitoring;

public sealed class NativeClientCapture(KafkaNativeMonitoringCoordinator coordinator)
{
    private KafkaNativeMonitoringLifetime? lifetime;

    public KafkaNativeMonitoringLifetime Begin(KafkaStatisticsIdentity identity) =>
        lifetime = coordinator.CreateLifetime(identity);

    public void Complete(bool success) => lifetime?.Complete(success, "unknown");
}
