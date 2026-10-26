namespace Parchwell.Enrichment.SeedF04;

public sealed class SeedF04Options
{
    public const string SectionName = "SeedF04";
    public string RunId { get; set; } = string.Empty;
    public string SourceCommit { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string PodName { get; set; } = string.Empty;
    public string PodUid { get; set; } = string.Empty;
    public string NamespaceName { get; set; } = string.Empty;
    public int ActionWindowMs { get; set; }
    public int ProcessLifetimeSeconds { get; set; }
    public int CleanupSeconds { get; set; }
    public int HostCleanupSeconds { get; set; }
    public int MaxReceiptBytes { get; set; }

    public void Validate()
    {
        if (!DnsLabel(RunId, 29) || !StringComparer.Ordinal.Equals(JobName, RunId + "-f04-seed") ||
            !DnsLabel(PodName, 63) || !PodName.StartsWith(JobName + "-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(PodUid, "D", out _))
            throw new InvalidOperationException("seed_identity_invalid");
        if (SourceCommit.Length != 40 || SourceCommit.Any(character => !char.IsAsciiHexDigitLower(character)))
            throw new InvalidOperationException("seed_source_commit_invalid");
        if (!DnsLabel(NamespaceName, 63) || NamespaceName.Contains("azure", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("seed_namespace_invalid");
        if (ActionWindowMs != 330000 || ProcessLifetimeSeconds != 390 || CleanupSeconds != 10 ||
            HostCleanupSeconds != 10 || MaxReceiptBytes != 65536)
            throw new InvalidOperationException("seed_bounds_invalid");
    }

    private static bool DnsLabel(string value, int maximum) => value.Length > 0 && value.Length <= maximum &&
        char.IsAsciiLetterOrDigit(value[0]) && char.IsAsciiLetterOrDigit(value[^1]) &&
        value.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'z' or '-');
}
