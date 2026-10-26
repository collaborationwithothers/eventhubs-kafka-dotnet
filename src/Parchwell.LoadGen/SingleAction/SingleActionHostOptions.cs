using System.Text.RegularExpressions;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.SingleAction;

internal sealed partial class SingleActionHostOptions
{
    public string ScenarioId { get; set; } = "";
    public string ConfigurationId { get; set; } = "";
    public string RunId { get; set; } = "";
    public string SourceCommit { get; set; } = "";
    public string JobName { get; set; } = "";
    public string PodName { get; set; } = "";
    public string PodUid { get; set; } = "";
    public string NamespaceName { get; set; } = "";
    public string BlobEndpoint { get; set; } = "";
    public string BlobContainer { get; set; } = "";
    public string ScenarioRoot { get; set; } = "";
    public int ProcessLifetimeSeconds { get; set; }
    public int PostTerminalStatisticsSeconds { get; set; }
    public int CleanupSeconds { get; set; }
    public int RegistrationUploadSeconds { get; set; }
    public int CaptureUploadSeconds { get; set; }
    public int HostCleanupSeconds { get; set; }
    public int ReceiptUploadSeconds { get; set; }
    public int PollSliceMs { get; set; }
    public int MaxCaptureBytes { get; set; }
    public int MaxRegistrationBytes { get; set; }
    public int MaxCompletionBytes { get; set; }
    public int MaxStatisticsReportBytes { get; set; }
    public int MaxStatisticsReports { get; set; }
    public int MaxClientErrors { get; set; }
    public int MaxErrorBytes { get; set; }
    public int MaxDiagnostics { get; set; }
    public int MaxDiagnosticBytes { get; set; }
    public int CallbackQueueCapacity { get; set; }
    public int MaxSettingsKeys { get; set; }
    public int MaxSettingsBytes { get; set; }
    public int MaxNonmatchingRecords { get; set; }
    public int MaxInputDepth { get; set; }
    public ScenarioLoadOptions ScenarioLoadOptions { get; set; } = new();

    internal void Validate()
    {
        string[] configurations = ScenarioId switch
        {
            "B01" or "C08" => ["C0", "C1"], "B03" => ["C1", "C2", "C3", "C4"],
            "B05" => ["C1"], "C12" => ["C1", "C2"], "F04" => ["C0", "C1", "C2", "C3", "C4"], _ => []
        };
        if (!configurations.Contains(ConfigurationId, StringComparer.Ordinal) || !RunPattern().IsMatch(RunId) ||
            !CommitPattern().IsMatch(SourceCommit) || !DnsPattern().IsMatch(JobName) || !DnsPattern().IsMatch(PodName) ||
            string.IsNullOrWhiteSpace(PodUid) || !DnsPattern().IsMatch(NamespaceName) || NamespaceName.Contains("azure", StringComparison.OrdinalIgnoreCase) ||
            !StringComparer.Ordinal.Equals(JobName, $"{RunId}-{ScenarioId.ToLowerInvariant()}-{ConfigurationId.ToLowerInvariant()}-single") ||
            !StringComparer.Ordinal.Equals(BlobContainer, ScenarioId is "C08" or "C12" || ScenarioId == "F04" && ConfigurationId is "C2" or "C4" ? "single-action" : "ledger") ||
            !Uri.TryCreate(BlobEndpoint, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != "https" || endpoint.AbsolutePath != "/" ||
            endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || string.IsNullOrWhiteSpace(ScenarioRoot))
            throw new InvalidOperationException("single_action_identity_invalid");
        if (ProcessLifetimeSeconds != 1920 || PostTerminalStatisticsSeconds != 20 || CleanupSeconds != 10 ||
            RegistrationUploadSeconds != 30 || CaptureUploadSeconds != 30 || HostCleanupSeconds != 10 || ReceiptUploadSeconds != 30 ||
            PollSliceMs != 100 || MaxCaptureBytes != 134217728 || MaxRegistrationBytes != 65536 || MaxCompletionBytes != 65536 ||
            MaxStatisticsReportBytes != 262144 || MaxStatisticsReports != 64 || MaxClientErrors != 64 || MaxErrorBytes != 4096 ||
            MaxDiagnostics != 100 || MaxDiagnosticBytes != 2048 || CallbackQueueCapacity != 128 || MaxSettingsKeys != 128 ||
            MaxSettingsBytes != 16384 || MaxNonmatchingRecords != 4096 || MaxInputDepth != 32 ||
            ScenarioLoadOptions.MaxDocumentCharacters <= 0 || ScenarioLoadOptions.MaxNestingDepth <= 0 ||
            ScenarioLoadOptions.MaxScalarCharacters <= 0 || ScenarioLoadOptions.MaxParserEvents <= 0)
            throw new InvalidOperationException("single_action_fixed_bounds_invalid");
        _ = Path.GetFullPath(ScenarioRoot);
    }
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,27}[a-z0-9])?$")]
    private static partial Regex RunPattern();
    [GeneratedRegex("^[a-f0-9]{40}$")]
    private static partial Regex CommitPattern();
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex DnsPattern();
}
