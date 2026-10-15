namespace Parchwell.Kafka.Configuration;

/// <summary>Required caller inputs for one named workload configuration.</summary>
/// <remarks>
/// These are the deployment-specific values the baseline cannot choose. Each role has its own
/// named instance, named after the <see cref="KafkaClientRole"/> value.
/// Every other setting comes from the baseline catalogue (ADR-015).
/// </remarks>
public sealed record KafkaBaselineOptions
{
    /// <summary>Gets or sets the Event Hubs namespace name without a suffix, URL or port.</summary>
    public string NamespaceName { get; set; } = string.Empty;

    /// <summary>Gets or sets the workload or pod identity emitted as client.id.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Gets or sets the stable group identity required only for enrichment consumption.</summary>
    public string? EnrichmentGroupId { get; set; }
}
