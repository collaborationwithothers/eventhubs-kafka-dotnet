namespace Parchwell.Kafka.Authentication;

/// <summary>Configuration for the projected workload identity used to acquire Kafka tokens.</summary>
/// <remarks>
/// These values identify the credential application and projected token file. The Kafka client ID
/// remains role-specific and comes from <see cref="Configuration.KafkaBaselineOptions.ClientId"/>.
/// </remarks>
public sealed record KafkaWorkloadIdentityOptions
{
    /// <summary>Gets or sets the tenant ID for the workload identity credential.</summary>
    public string? TenantId { get; set; }

    /// <summary>Gets or sets the credential application's client ID, distinct from Kafka client ID.</summary>
    public string? ClientId { get; set; }

    /// <summary>Gets or sets the path to the projected workload identity token file.</summary>
    public string? TokenFilePath { get; set; }
}
