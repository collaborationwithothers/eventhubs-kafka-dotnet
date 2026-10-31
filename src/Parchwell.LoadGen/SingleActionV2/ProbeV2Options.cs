using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.SingleActionV2;

internal sealed class ProbeV2Limits
{
    public int PostTerminalStatisticsSeconds { get; set; }
    public int CleanupSeconds { get; set; }
    public int HostCleanupSeconds { get; set; }
    public int IoSeconds { get; set; }
    public int PollSliceMs { get; set; }
    public int MetadataTimeoutMs { get; set; }
    public int MaxCaptureBytes { get; set; }
    public int MaxRegistrationBytes { get; set; }
    public int MaxPodBytes { get; set; }
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
    public int MaxKeyBytes { get; set; }
    public int MaxValueBytes { get; set; }
    public int MaxClientLogBytes { get; set; }

    internal static ProbeV2Limits Fixed() => new()
    {
        PostTerminalStatisticsSeconds = 20, CleanupSeconds = 10, HostCleanupSeconds = 10, IoSeconds = 10,
        PollSliceMs = 100, MetadataTimeoutMs = 60000, MaxCaptureBytes = 2097152, MaxRegistrationBytes = 65536,
        MaxPodBytes = 65536, MaxStatisticsReportBytes = 262144, MaxStatisticsReports = 1, MaxClientErrors = 8,
        MaxErrorBytes = 1024, MaxDiagnostics = 100, MaxDiagnosticBytes = 2048, CallbackQueueCapacity = 128,
        MaxSettingsKeys = 128, MaxSettingsBytes = 16384, MaxNonmatchingRecords = 4096, MaxInputDepth = 32,
        MaxKeyBytes = 4096, MaxValueBytes = 4096, MaxClientLogBytes = 16384
    };
    internal void Validate()
    {
        ProbeV2Limits expected = Fixed();
        if (GetType().GetProperties().Any(property => !Equals(property.GetValue(this), property.GetValue(expected))))
            throw new InvalidOperationException("v2_fixed_bounds");
    }
}

internal sealed partial class ProbeV2Options
{
    private static readonly string[] OtherPrefixes = ["ProbeV2Judge__", "SingleAction__", "SeedF04__", "Verifier__"];
    public string ScenarioId { get; set; } = "";
    public string ConfigurationId { get; set; } = "";
    public string RunId { get; set; } = "";
    public string SourceCommit { get; set; } = "";
    public string JobName { get; set; } = "";
    public string PodName { get; set; } = "";
    public string PodUid { get; set; } = "";
    public string NamespaceName { get; set; } = "";
    public string ServiceAccount { get; set; } = "";
    public string WorkloadIdentityClientId { get; set; } = "";
    public string ScenarioRoot { get; set; } = "";
    public string InputRoot { get; set; } = "";
    public string Stage { get; set; } = "";
    public bool CaptureAuthenticationMetadata { get; set; }
    public bool TestHandlerRefreshPath { get; set; }
    public ProbeV2Limits Limits { get; set; } = new();
    public ScenarioLoadOptions ScenarioLoadOptions { get; set; } = new();
    public int ProcessLifetimeSeconds { get; set; }

    internal void Validate()
    {
        ProbeV2Stage stage = ProbeV2Stage.For(ScenarioId, ConfigurationId, Stage);
        if (!RunPattern().IsMatch(RunId) || !CommitPattern().IsMatch(SourceCommit) || !DnsPattern().IsMatch(JobName) ||
            !DnsPattern().IsMatch(PodName) || !DnsPattern().IsMatch(NamespaceName) || NamespaceName.Contains("azure", StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(PodUid, out _) || !Guid.TryParse(WorkloadIdentityClientId, out _) ||
            ServiceAccount != stage.Account || JobName != Job(RunId, ScenarioId, ConfigurationId, Stage) ||
            !PodName.StartsWith(JobName + "-", StringComparison.Ordinal) || ScenarioRoot != "/app" || InputRoot != "/run/probe-inputs" ||
            ProcessLifetimeSeconds != (stage.Action == "produce_one" ? 1920 : 840))
            throw new InvalidOperationException("v2_identity");
        if (TestHandlerRefreshPath && (!CaptureAuthenticationMetadata || ScenarioId != "F06" ||
            ConfigurationId == "C6" && !(Stage == "seed" && stage.Account == "loadgen") ||
            ConfigurationId == "C9" && !(Stage == "action" && stage.Account == "enrichment-consumer") ||
            ConfigurationId is not ("C6" or "C9")))
            throw new InvalidOperationException("v2_handler_refresh_selection");
        Limits.Validate();
        if (ScenarioLoadOptions.MaxDocumentCharacters != 1048576 || ScenarioLoadOptions.MaxNestingDepth != 32 ||
            ScenarioLoadOptions.MaxScalarCharacters != 65536 || ScenarioLoadOptions.MaxParserEvents != 100000)
            throw new InvalidOperationException("v2_parsing_bounds");
    }
    internal static ProbeV2Options Read(IReadOnlyDictionary<string, string> environment)
    {
        var selected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in environment)
        {
            if (OtherPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("v2_mixed_mode");
            if (key.StartsWith("ProbeV2__", StringComparison.OrdinalIgnoreCase))
            {
                if (!key.StartsWith("ProbeV2__", StringComparison.Ordinal) || !selected.TryAdd(key[9..], value))
                    throw new InvalidOperationException("v2_option_name");
            }
        }
        var options = new ProbeV2Options();
        Bind(options, "", selected);
        if (selected.Count != 0) throw new InvalidOperationException("v2_unknown_option");
        options.Validate();
        if (!environment.TryGetValue("AZURE_CLIENT_ID", out string? client) || client != options.WorkloadIdentityClientId ||
            !environment.TryGetValue("AZURE_TENANT_ID", out string? tenant) || !Guid.TryParse(tenant, out _) ||
            !environment.TryGetValue("AZURE_FEDERATED_TOKEN_FILE", out string? tokenFile) || !Path.IsPathFullyQualified(tokenFile) || tokenFile.Contains('\r') || tokenFile.Contains('\n'))
            throw new InvalidOperationException("v2_auth_identity");
        foreach ((string name, string expected) in new[] { ("SOURCE_COMMIT", options.SourceCommit), ("JOB_NAME", options.JobName), ("POD_NAME", options.PodName), ("POD_UID", options.PodUid) })
            if (!environment.TryGetValue("PARCHWELL_" + name, out string? actual) || actual != expected)
                throw new InvalidOperationException("v2_deployment_identity");
        return options;
    }
    private static void Bind(object target, string prefix, Dictionary<string, string> values)
    {
        foreach (PropertyInfo property in target.GetType().GetProperties())
        {
            string key = prefix + property.Name;
            if (target is ProbeV2Options && prefix.Length == 0 && property.Name is nameof(CaptureAuthenticationMetadata) or nameof(TestHandlerRefreshPath))
            {
                if (values.Remove(key, out string? raw))
                {
                    if (raw == "true") property.SetValue(target, true);
                    else if (raw == "false") property.SetValue(target, false);
                    else throw new InvalidOperationException("v2_authentication_capture_option");
                }
                continue;
            }
            if (property.PropertyType == typeof(string) || property.PropertyType == typeof(int))
            {
                if (!values.Remove(key, out string? raw) || string.IsNullOrWhiteSpace(raw) || raw.Contains('\r') || raw.Contains('\n'))
                    throw new InvalidOperationException("v2_required_option");
                property.SetValue(target, property.PropertyType == typeof(string) ? raw : int.Parse(raw, NumberStyles.None, CultureInfo.InvariantCulture));
            }
            else Bind(property.GetValue(target)!, key + "__", values);
        }
    }
    internal static string Job(string run, string item, string configuration, string stage) => $"{run}-{item.ToLowerInvariant()}-{configuration.ToLowerInvariant()}-{stage}";
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,27}[a-z0-9])?$")]
    private static partial Regex RunPattern();
    [GeneratedRegex("^[a-f0-9]{40}$")]
    private static partial Regex CommitPattern();
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex DnsPattern();
}
