using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Parchwell.Enrichment.ConsumerProcessing;

internal sealed class ConsumerProcessingOptions
{
    public const string SectionName = "ConsumerProcessing";
    public string RunId { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public int RepeatNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string PodName { get; set; } = string.Empty;
    public string PodUid { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string SourceCommit { get; set; } = string.Empty;
    public string NamespaceName { get; set; } = string.Empty;
    public string BlobEndpoint { get; set; } = string.Empty;
    public string GroupId => $"{RunId}-{Phase.ToLowerInvariant()}-r{RepeatNumber.ToString(CultureInfo.InvariantCulture)}-consumers";
    public string Prefix => $"{RunId}/{Phase}/r{RepeatNumber.ToString(CultureInfo.InvariantCulture)}/consumer/{Uri.EscapeDataString(PodName)}/";

    public void Validate()
    {
        string expectedJob = $"{RunId}-{Phase.ToLowerInvariant()}-r{RepeatNumber.ToString(CultureInfo.InvariantCulture)}-consumer-{Role}";
        if (!DnsLabel(RunId, 29) || ScenarioId is not ("C03" or "C10" or "C01" or "C18") ||
            Phase is not ("C0" or "C1") || RepeatNumber is < 1 or > 5 || Role is not ("subject" or "witness") ||
            !StringComparer.Ordinal.Equals(JobName, expectedJob) || !DnsLabel(PodName, 63) ||
            !PodName.StartsWith(JobName + "-", StringComparison.Ordinal) || !Guid.TryParseExact(PodUid, "D", out _) ||
            string.IsNullOrEmpty(NodeName) || NodeName.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) ||
            SourceCommit.Length != 40 || SourceCommit.Any(character => !char.IsAsciiHexDigitLower(character)) ||
            !DnsLabel(NamespaceName, 63) || NamespaceName.Contains("azure", StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(BlobEndpoint, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("consumer_identity_invalid");
    }

    public void ValidateDeploymentIdentity(IConfiguration configuration)
    {
        Validate();
        if (!StringComparer.Ordinal.Equals(SourceCommit, configuration["PARCHWELL_SOURCE_COMMIT"]) ||
            !StringComparer.Ordinal.Equals(JobName, configuration["PARCHWELL_JOB_NAME"]) ||
            !StringComparer.Ordinal.Equals(PodName, configuration["PARCHWELL_POD_NAME"]) ||
            !StringComparer.Ordinal.Equals(PodUid, configuration["PARCHWELL_POD_UID"]) ||
            !StringComparer.Ordinal.Equals(NodeName, configuration["PARCHWELL_NODE_NAME"]))
            throw new InvalidOperationException("consumer_identity_invalid");
    }

    private static bool DnsLabel(string value, int maximum) => value.Length > 0 && value.Length <= maximum &&
        char.IsAsciiLetterOrDigit(value[0]) && char.IsAsciiLetterOrDigit(value[^1]) &&
        value.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'z' or '-');
}
