using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Parchwell.Scenarios;

namespace Parchwell.Verifier.VerifyRun.IdleObservation;

internal sealed partial class IdleObservationJudgeOptions
{
    private static readonly string[] PodKeys = ["A02C0", "A02C1", "A03C0", "A03C1", "A03C2", "A04C0", "A04C1"];

    public string RunKind { get; set; } = "";
    public string RunId { get; set; } = "";
    public string SourceCommit { get; set; } = "";
    public string JobName { get; set; } = "";
    public string PodName { get; set; } = "";
    public string PodUid { get; set; } = "";
    public string NamespaceName { get; set; } = "";
    public string ServiceAccount { get; set; } = "";
    public string WorkloadIdentityClientId { get; set; } = "";
    public string ScenarioRoot { get; set; } = "";
    public string BlobServiceUri { get; set; } = "";
    public string LedgerContainer { get; set; } = "";
    public string VerifierContainer { get; set; } = "";
    public string LoadGenImage { get; set; } = "";
    public string VerifierImage { get; set; } = "";
    public string CapturePods { get; set; } = "";
    public string Items { get; set; } = "";
    public IdleObservationJudgeLimits Limits { get; set; } = new();
    public ScenarioLoadOptions ScenarioLoadOptions { get; set; } = new();

    internal void Validate()
    {
        if (RunKind is not ("smoke" or "batch")) throw new InvalidOperationException("idle_judge_run_kind");
        if (!RunPattern().IsMatch(RunId) || !CommitPattern().IsMatch(SourceCommit) ||
            !DnsPattern().IsMatch(JobName) || !DnsPattern().IsMatch(PodName) ||
            !PodName.StartsWith(JobName + "-", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(PodUid) ||
            !DnsPattern().IsMatch(NamespaceName) || NamespaceName.Contains("azure", StringComparison.OrdinalIgnoreCase) ||
            ServiceAccount != "verifier" || !Guid.TryParse(WorkloadIdentityClientId, out _) ||
            JobName != IdleObservationPaths.Job(RunId) || LedgerContainer != "ledger" || VerifierContainer != "verifier" ||
            !Path.IsPathFullyQualified(ScenarioRoot) || !Uri.TryCreate(BlobServiceUri, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 || !ImagePattern().IsMatch(LoadGenImage) || !ImagePattern().IsMatch(VerifierImage) ||
            GetType().GetProperties().Where(x => x.PropertyType == typeof(string)).Select(x => (string)x.GetValue(this)!).Any(x => x.Length == 0 || x.Contains('\r') || x.Contains('\n')) ||
            !ItemsAreValid() || !PodsAreValid())
            throw new InvalidOperationException("idle_judge_identity");
        Limits.Validate();
        if (ScenarioLoadOptions.MaxDocumentCharacters != 1048576 || ScenarioLoadOptions.MaxNestingDepth != 32 ||
            ScenarioLoadOptions.MaxScalarCharacters != 65536 || ScenarioLoadOptions.MaxParserEvents != 100000)
            throw new InvalidOperationException("idle_judge_parser_bounds");
    }

    /// <summary>The capture Pods named by <see cref="CapturePods"/>, keyed by item and configuration, such as A02C0.</summary>
    internal IReadOnlyDictionary<string, (string Pod, string Uid)> Pods()
    {
        var pods = new Dictionary<string, (string Pod, string Uid)>(StringComparer.Ordinal);
        if (CapturePods == "none") return pods;
        foreach (string entry in CapturePods.Split(','))
        {
            string[] parts = entry.Split(':');
            if (parts.Length != 3) throw new InvalidOperationException("idle_judge_identity");
            pods.Add(parts[0], (parts[1], parts[2]));
        }
        return pods;
    }

    /// <summary>The items named by <see cref="Items"/>, such as A02 and A04, in the order given.</summary>
    internal string[] SelectedItems() => Items.Split(',');

    private bool ItemsAreValid()
    {
        string[] items = SelectedItems();
        return items.SequenceEqual(IdleObservationJudgement.Items.Where(item => Array.IndexOf(items, item) >= 0));
    }

    private bool PodsAreValid()
    {
        try
        {
            foreach (var (key, (pod, uid)) in Pods())
            {
                if (Array.IndexOf(PodKeys, key) < 0 || Array.IndexOf(SelectedItems(), key[..3]) < 0 || !DnsPattern().IsMatch(pod) ||
                    !pod.StartsWith($"{RunId}-{key[..3].ToLowerInvariant()}-{key[3..].ToLowerInvariant()}-idle-", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(uid) || uid.Contains(':') || uid.Contains(','))
                    return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException) { return false; }
    }

    internal static IdleObservationJudgeOptions Read(IConfiguration configuration, IDictionary? environment = null)
    {
        if (environment is not null)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in environment)
            {
                string key = (string)entry.Key;
                if (key.StartsWith("ProbeV2__", StringComparison.OrdinalIgnoreCase) || key.StartsWith("ProbeV2Judge__", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("SingleAction__", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Verifier__", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("IdleObservation__", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("idle_judge_mixed_options");
                if (!key.StartsWith("IdleObservationJudge__", StringComparison.OrdinalIgnoreCase)) continue;
                if (!key.StartsWith("IdleObservationJudge__", StringComparison.Ordinal) || !names.Add(key)) throw new InvalidOperationException("idle_judge_options_case");
            }
        }
        IConfigurationSection section = configuration.GetSection("IdleObservationJudge");
        if (section["RunKind"] is not ("smoke" or "batch")) throw new InvalidOperationException("idle_judge_run_kind");
        RequireLeaves(section, typeof(IdleObservationJudgeOptions));
        var options = new IdleObservationJudgeOptions();
        section.Bind(options, value => value.ErrorOnUnknownConfiguration = true);
        options.Validate();
        if (configuration["AZURE_CLIENT_ID"] != options.WorkloadIdentityClientId ||
            !Guid.TryParse(configuration["AZURE_TENANT_ID"], out _) ||
            !Path.IsPathFullyQualified(configuration["AZURE_FEDERATED_TOKEN_FILE"] ?? ""))
            throw new InvalidOperationException("idle_judge_authentication_inputs");
        foreach (var (key, expected) in new[] { ("SOURCE_COMMIT", options.SourceCommit), ("JOB_NAME", options.JobName), ("POD_NAME", options.PodName), ("POD_UID", options.PodUid) })
            if (configuration["PARCHWELL_" + key] is string actual && actual != expected) throw new InvalidOperationException("idle_judge_deployment_identity");
        return options;
    }

    private static void RequireLeaves(IConfigurationSection section, Type type)
    {
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public).ToDictionary(x => x.Name, StringComparer.Ordinal);
        var children = section.GetChildren().ToArray();
        if (children.Length != properties.Count || children.Any(x => !properties.ContainsKey(x.Key))) throw new InvalidOperationException("idle_judge_options_shape");
        foreach (var child in children)
        {
            Type valueType = properties[child.Key].PropertyType;
            if (valueType == typeof(string) || valueType == typeof(int))
            {
                if (child.Value is null || child.GetChildren().Any()) throw new InvalidOperationException("idle_judge_options_leaf");
            }
            else { if (child.Value is not null) throw new InvalidOperationException("idle_judge_options_group"); RequireLeaves(child, valueType); }
        }
    }

    internal Dictionary<string, string> ImageDigests() => new(StringComparer.Ordinal)
    {
        ["loadgen"] = LoadGenImage[(LoadGenImage.LastIndexOf('@') + 1)..],
        ["verifier"] = VerifierImage[(VerifierImage.LastIndexOf('@') + 1)..]
    };

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,27}[a-z0-9])?$")]
    private static partial Regex RunPattern();
    [GeneratedRegex("^[a-f0-9]{40}$")]
    private static partial Regex CommitPattern();
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex DnsPattern();
    [GeneratedRegex("^[a-z0-9.-]+(?::[0-9]+)?/[a-z0-9._/-]+@sha256:[0-9a-f]{64}$")]
    private static partial Regex ImagePattern();
}

internal sealed class IdleObservationJudgeLimits
{
    public int ProcessLifetimeSeconds { get; set; }
    public int CaptureIoSeconds { get; set; }
    public int IoSeconds { get; set; }
    public int CleanupSeconds { get; set; }
    public int MaxCaptureBytes { get; set; }
    public int MaxRegistrationBytes { get; set; }
    public int MaxCompletionBytes { get; set; }
    public int MaxStatisticsReportBytes { get; set; }
    public int MaxBrokerEntries { get; set; }
    public int MaxCandidateBytes { get; set; }
    public int MaxJudgeReceiptBytes { get; set; }

    internal void Validate()
    {
        if (ProcessLifetimeSeconds != 660 ||
            CaptureIoSeconds != 60 ||
            IoSeconds != 10 ||
            CleanupSeconds != 10 ||
            MaxCaptureBytes != 167772160 ||
            MaxRegistrationBytes != 65536 ||
            MaxCompletionBytes != 65536 ||
            MaxStatisticsReportBytes != 262144 ||
            MaxBrokerEntries != 64 ||
            MaxCandidateBytes != 16777216 ||
            MaxJudgeReceiptBytes != 65536)
            throw new InvalidOperationException("idle_judge_fixed_bounds");
    }
}
