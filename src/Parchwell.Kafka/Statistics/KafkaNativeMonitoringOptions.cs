namespace Parchwell.Kafka.Statistics;

/// <summary>Host configuration for private native-monitoring capture.</summary>
public sealed class KafkaNativeMonitoringOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "NativeMonitoring";
    /// <summary>Blob service endpoint.</summary>
    public string BlobEndpoint { get; set; } = string.Empty;
    /// <summary>Private container name.</summary>
    public string BlobContainer { get; set; } = string.Empty;
    /// <summary>Run identifier.</summary>
    public string RunId { get; set; } = string.Empty;
    /// <summary>Phase identifier.</summary>
    public string Phase { get; set; } = string.Empty;
    /// <summary>Scenario identifier.</summary>
    public string ScenarioId { get; set; } = string.Empty;
    /// <summary>Configuration identifier.</summary>
    public string ConfigurationId { get; set; } = string.Empty;
    /// <summary>Job name.</summary>
    public string JobName { get; set; } = string.Empty;
    /// <summary>Pod UID.</summary>
    public string PodUid { get; set; } = string.Empty;
    /// <summary>Source commit.</summary>
    public string SourceCommit { get; set; } = string.Empty;

    /// <summary>Validates destination and deployment identity without contacting storage.</summary>
    /// <param name="failure">An operator-facing validation failure, if any.</param>
    /// <returns>True when all fields are valid.</returns>
    public bool IsValid(out string? failure)
    {
        if (!Uri.TryCreate(BlobEndpoint, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment)
            || string.IsNullOrWhiteSpace(BlobContainer) || BlobContainer.Any(c => !(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
            || new[] { RunId, Phase, ScenarioId, ConfigurationId, JobName, PodUid, SourceCommit }
                .Any(value => string.IsNullOrWhiteSpace(value) || value is "." or ".." || value.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is '/' or '\\')))
        {
            failure = "Native monitoring destination and deployment identity must be valid.";
            return false;
        }
        failure = null;
        return true;
    }

    /// <summary>Creates the index identity from validated configuration.</summary>
    /// <returns>The deployment identity.</returns>
    public KafkaNativeMonitoringIdentity CreateIdentity()
    {
        if (!IsValid(out string? failure)) throw new InvalidOperationException(failure);
        return new(RunId, Phase, ScenarioId, ConfigurationId, JobName, PodUid, SourceCommit);
    }
}
