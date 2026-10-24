namespace Parchwell.Kafka.Telemetry;

/// <summary>Defines the activity and meter names that worker composition roots can emit.</summary>
public static class ParchwellTelemetrySources
{
    /// <summary>The activity source registered for worker operations.</summary>
    public const string WorkerActivitySourceName = "Parchwell.Workers";

    /// <summary>The meter registered for worker measurements.</summary>
    public const string WorkerMeterName = "Parchwell.Workers";
}
