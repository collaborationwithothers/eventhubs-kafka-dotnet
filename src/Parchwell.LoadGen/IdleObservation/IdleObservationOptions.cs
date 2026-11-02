using System.Text.RegularExpressions;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.IdleObservation;

internal sealed partial class IdleObservationOptions
{
    public string ScenarioId { get; set; } = "";
    public string ConfigurationId { get; set; } = "";
    public string RunId { get; set; } = "";
    public string RunKind { get; set; } = "";
    public string SourceCommit { get; set; } = "";
    public string JobName { get; set; } = "";
    public string PodName { get; set; } = "";
    public string PodUid { get; set; } = "";
    public string NamespaceName { get; set; } = "";
    public string BlobEndpoint { get; set; } = "";
    public string BlobContainer { get; set; } = "";
    public string ScenarioRoot { get; set; } = "";
    public int ProcessLifetimeSeconds { get; set; }
    public int ActionWindowMs { get; set; }
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
    public int MaxClientErrors { get; set; }
    public int MaxErrorBytes { get; set; }
    public int MaxDiagnosticsPerClient { get; set; }
    public int MaxDiagnosticBytes { get; set; }
    public int CallbackQueueCapacity { get; set; }
    public int MaxSettingsKeys { get; set; }
    public int MaxSettingsBytes { get; set; }
    public ScenarioLoadOptions ScenarioLoadOptions { get; set; } = new();

    internal void Validate()
    {
        string[] configurations = ScenarioId switch
        {
            "A02" or "A04" => ["C0", "C1"], "A03" => ["C0", "C1", "C2"], _ => []
        };
        if (!configurations.Contains(ConfigurationId, StringComparer.Ordinal) || !RunPattern().IsMatch(RunId) ||
            !CommitPattern().IsMatch(SourceCommit) || !DnsPattern().IsMatch(JobName) || !DnsPattern().IsMatch(PodName) ||
            string.IsNullOrWhiteSpace(PodUid) || !DnsPattern().IsMatch(NamespaceName) || NamespaceName.Contains("azure", StringComparison.OrdinalIgnoreCase) ||
            RunKind is not ("smoke" or "batch") ||
            !StringComparer.Ordinal.Equals(JobName, $"{RunId}-{ScenarioId.ToLowerInvariant()}-{ConfigurationId.ToLowerInvariant()}-idle") ||
            !StringComparer.Ordinal.Equals(BlobContainer, "ledger") ||
            !Uri.TryCreate(BlobEndpoint, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != "https" || endpoint.AbsolutePath != "/" ||
            endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || string.IsNullOrWhiteSpace(ScenarioRoot))
            throw new InvalidOperationException("idle_observation_identity_invalid");
        if (ProcessLifetimeSeconds != 1260 || ActionWindowMs != 330000 || PostTerminalStatisticsSeconds != 20 || CleanupSeconds != 10 ||
            RegistrationUploadSeconds != 30 || CaptureUploadSeconds != 60 || HostCleanupSeconds != 10 || ReceiptUploadSeconds != 30 ||
            PollSliceMs != 100 || MaxCaptureBytes != 167772160 || MaxRegistrationBytes != 65536 || MaxCompletionBytes != 65536 ||
            MaxStatisticsReportBytes != 262144 || MaxClientErrors != 64 || MaxErrorBytes != 4096 ||
            MaxDiagnosticsPerClient != 20 || MaxDiagnosticBytes != 2048 || CallbackQueueCapacity != 128 || MaxSettingsKeys != 128 ||
            MaxSettingsBytes != 16384 ||
            ScenarioLoadOptions.MaxDocumentCharacters <= 0 || ScenarioLoadOptions.MaxNestingDepth <= 0 ||
            ScenarioLoadOptions.MaxScalarCharacters <= 0 || ScenarioLoadOptions.MaxParserEvents <= 0)
            throw new InvalidOperationException("idle_observation_fixed_bounds_invalid");
        _ = Path.GetFullPath(ScenarioRoot);
    }
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,27}[a-z0-9])?$")]
    private static partial Regex RunPattern();
    [GeneratedRegex("^[a-f0-9]{40}$")]
    private static partial Regex CommitPattern();
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex DnsPattern();
}
