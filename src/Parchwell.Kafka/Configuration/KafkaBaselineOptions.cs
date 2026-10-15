namespace Parchwell.Kafka.Configuration;

/// <summary>Required caller inputs for one named workload configuration.</summary>
public sealed record KafkaBaselineOptions
{
    /// <summary>Gets or sets the Event Hubs namespace name without a suffix, URL or port.</summary>
    public string NamespaceName { get; set; } = string.Empty;

    /// <summary>Gets or sets the workload or pod identity emitted as client.id.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Gets or sets the stable group identity required only for enrichment consumption.</summary>
    public string? EnrichmentGroupId { get; set; }
}
