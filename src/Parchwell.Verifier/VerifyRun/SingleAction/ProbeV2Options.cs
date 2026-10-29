using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Parchwell.Scenarios;

namespace Parchwell.Verifier.VerifyRun.SingleAction;

internal sealed partial class ProbeV2Options
{
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
    public string BlobServiceUri { get; set; } = "";
    public string VerifierContainer { get; set; } = "";
    public string LoadGenImage { get; set; } = "";
    public string VerifierImage { get; set; } = "";
    public ProbeV2CaptureLimits Limits { get; set; } = new();
    public ProbeV2JudgeLimits JudgeLimits { get; set; } = new();
    public ScenarioLoadOptions ScenarioLoadOptions { get; set; } = new();

    internal void Validate()
    {
        _ = ProbeV2Matrix.For(ScenarioId, ConfigurationId, "action");
        if (!RunPattern().IsMatch(RunId) || !CommitPattern().IsMatch(SourceCommit) ||
            !DnsPattern().IsMatch(JobName) || !DnsPattern().IsMatch(PodName) ||
            !PodName.StartsWith(JobName + "-", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(PodUid) ||
            !DnsPattern().IsMatch(NamespaceName) || NamespaceName.Contains("azure", StringComparison.OrdinalIgnoreCase) ||
            ServiceAccount != "verifier" || Stage != "judge" || !Guid.TryParse(WorkloadIdentityClientId, out _) ||
            JobName != ProbeV2Matrix.Job(RunId, ScenarioId, ConfigurationId, Stage) ||
            InputRoot != "/run/probe-inputs" || !Path.IsPathFullyQualified(ScenarioRoot) ||
            VerifierContainer != "verifier" || !Uri.TryCreate(BlobServiceUri, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 || !ImagePattern().IsMatch(LoadGenImage) || !ImagePattern().IsMatch(VerifierImage) ||
            GetType().GetProperties().Where(x => x.PropertyType == typeof(string)).Select(x => (string)x.GetValue(this)!).Any(x => x.Length == 0 || x.Contains('\r') || x.Contains('\n')))
            throw new InvalidOperationException("v2_identity");
        Limits.Validate(); JudgeLimits.Validate();
        if (ScenarioLoadOptions.MaxDocumentCharacters != 1048576 || ScenarioLoadOptions.MaxNestingDepth != 32 ||
            ScenarioLoadOptions.MaxScalarCharacters != 65536 || ScenarioLoadOptions.MaxParserEvents != 100000)
            throw new InvalidOperationException("v2_parser_bounds");
    }

    internal static ProbeV2Options Read(IConfiguration configuration, IDictionary? environment = null)
    {
        if (environment is not null)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in environment)
            {
                string key = (string)entry.Key;
                if (key.StartsWith("ProbeV2__", StringComparison.OrdinalIgnoreCase) || key.StartsWith("SingleAction__", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Verifier__", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("v2_mixed_options");
                if (!key.StartsWith("ProbeV2Judge__", StringComparison.OrdinalIgnoreCase)) continue;
                if (!key.StartsWith("ProbeV2Judge__", StringComparison.Ordinal) || !names.Add(key)) throw new InvalidOperationException("v2_options_case");
            }
        }
        IConfigurationSection section = configuration.GetSection("ProbeV2Judge");
        RequireLeaves(section, typeof(ProbeV2Options));
        var options = new ProbeV2Options();
        section.Bind(options, value => value.ErrorOnUnknownConfiguration = true);
        options.Validate();
        if (configuration["AZURE_CLIENT_ID"] != options.WorkloadIdentityClientId ||
            !Guid.TryParse(configuration["AZURE_TENANT_ID"], out _) ||
            !Path.IsPathFullyQualified(configuration["AZURE_FEDERATED_TOKEN_FILE"] ?? ""))
            throw new InvalidOperationException("v2_authentication_inputs");
        foreach (var (key, expected) in new[] { ("SOURCE_COMMIT", options.SourceCommit), ("JOB_NAME", options.JobName), ("POD_NAME", options.PodName), ("POD_UID", options.PodUid) })
            if (configuration["PARCHWELL_" + key] is string actual && actual != expected) throw new InvalidOperationException("v2_deployment_identity");
        return options;
    }
    private static void RequireLeaves(IConfigurationSection section, Type type)
    {
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public).ToDictionary(x => x.Name, StringComparer.Ordinal);
        var children = section.GetChildren().ToArray();
        if (children.Length != properties.Count || children.Any(x => !properties.ContainsKey(x.Key))) throw new InvalidOperationException("v2_options_shape");
        foreach (var child in children)
        {
            Type valueType = properties[child.Key].PropertyType;
            if (valueType == typeof(string) || valueType == typeof(int))
            {
                if (child.Value is null || child.GetChildren().Any()) throw new InvalidOperationException("v2_options_leaf");
            }
            else { if (child.Value is not null) throw new InvalidOperationException("v2_options_group"); RequireLeaves(child, valueType); }
        }
    }
    internal Dictionary<string,string> ImageDigests() => new(StringComparer.Ordinal) { ["loadgen"] = LoadGenImage[(LoadGenImage.LastIndexOf('@') + 1)..], ["verifier"] = VerifierImage[(VerifierImage.LastIndexOf('@') + 1)..] };
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,27}[a-z0-9])?$")]
    private static partial Regex RunPattern();
    [GeneratedRegex("^[a-f0-9]{40}$")]
    private static partial Regex CommitPattern();
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex DnsPattern();
    [GeneratedRegex("^[a-z0-9.-]+(?::[0-9]+)?/[a-z0-9._/-]+@sha256:[0-9a-f]{64}$")]
    private static partial Regex ImagePattern();
}

internal sealed class ProbeV2CaptureLimits
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
    internal void Validate()
    {
        if (PostTerminalStatisticsSeconds != 20 ||
            CleanupSeconds != 10 ||
            HostCleanupSeconds != 10 ||
            IoSeconds != 10 ||
            PollSliceMs != 100 ||
            MetadataTimeoutMs != 60000 ||
            MaxCaptureBytes != 524288 ||
            MaxRegistrationBytes != 65536 ||
            MaxPodBytes != 65536 ||
            MaxStatisticsReportBytes != 32768 ||
            MaxStatisticsReports != 1 ||
            MaxClientErrors != 8 ||
            MaxErrorBytes != 1024 ||
            MaxDiagnostics != 100 ||
            MaxDiagnosticBytes != 2048 ||
            CallbackQueueCapacity != 128 ||
            MaxSettingsKeys != 128 ||
            MaxSettingsBytes != 16384 ||
            MaxNonmatchingRecords != 4096 ||
            MaxInputDepth != 32 ||
            MaxKeyBytes != 4096 ||
            MaxValueBytes != 4096 ||
            MaxClientLogBytes != 16384)
            throw new InvalidOperationException("v2_fixed_bounds");
    }
}

internal sealed class ProbeV2JudgeLimits
{
    public int ProcessLifetimeSeconds { get; set; }
    public int IoSeconds { get; set; }
    public int CleanupSeconds { get; set; }
    public int MaxCandidateBytes { get; set; }
    public int MaxJudgeReceiptBytes { get; set; }
    public int MaxTotalInputBytes { get; set; }
    public int MaxInputFiles { get; set; }
    public int MaxBrokerEntries { get; set; }
    public int MaxRoleScopeBytes { get; set; }
    public int MaxEffectiveScopeBytes { get; set; }
    internal void Validate()
    {
        if (ProcessLifetimeSeconds != 90 ||
            IoSeconds != 10 ||
            CleanupSeconds != 10 ||
            MaxCandidateBytes != 16777216 ||
            MaxJudgeReceiptBytes != 65536 ||
            MaxTotalInputBytes != 268435456 ||
            MaxInputFiles != 256 ||
            MaxBrokerEntries != 64 ||
            MaxRoleScopeBytes != 131072 ||
            MaxEffectiveScopeBytes != 32768)
            throw new InvalidOperationException("v2_fixed_bounds");
    }
}
