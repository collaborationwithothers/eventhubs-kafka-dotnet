namespace Parchwell.Kafka.Telemetry;

/// <summary>Configures the worker's Azure Monitor destination and telemetry identity.</summary>
public sealed class ParchwellTelemetryOptions
{
    /// <summary>Gets or sets the runtime-supplied Azure Monitor connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets the service name attached to all signals.</summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Gets or sets the service instance identity attached to all signals.</summary>
    public string ServiceInstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the scenario identity attached to all signals.</summary>
    public string ScenarioId { get; set; } = string.Empty;

    /// <summary>Gets or sets the run identity attached to all signals.</summary>
    public string RunId { get; set; } = string.Empty;
}
