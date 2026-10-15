namespace Parchwell.Kafka.Configuration;

/// <summary>A cited source at its pinned version or recorded date.</summary>
/// <remarks>
/// Every baseline value and every override bound must cite where it comes from (AGENTS.md,
/// "Truth and verification"). A version or date is kept with the link because the cited page
/// can change after it was read.
/// </remarks>
public sealed class KafkaSettingSource
{
    internal KafkaSettingSource(Uri url, string versionOrDate)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionOrDate);
        if (!url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("A source must be an absolute HTTPS URL.", nameof(url));
        Url = url;
        VersionOrDate = versionOrDate;
    }

    /// <summary>Gets the source URL.</summary>
    public Uri Url { get; }

    /// <summary>Gets the pinned version or source date.</summary>
    public string VersionOrDate { get; }
}
