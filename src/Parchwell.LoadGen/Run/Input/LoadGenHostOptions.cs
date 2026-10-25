using System.ComponentModel.DataAnnotations;

namespace Parchwell.LoadGen.Run.Input;

/// <summary>Required deployment inputs and finite budgets for one load-generator worker.</summary>
public sealed class LoadGenHostOptions
{
    public const string SectionName = "LoadGen";

    [Required] public string ScenarioId { get; set; } = string.Empty;
    [Required] public string ConfigurationId { get; set; } = string.Empty;
    [Required] public string RunId { get; set; } = string.Empty;
    [Required] public string ExpectedInstanceId { get; set; } = string.Empty;
    [Required] public string ActualInstanceId { get; set; } = string.Empty;
    [Required] public string PodName { get; set; } = string.Empty;
    [Required] public string ClientId { get; set; } = string.Empty;
    [Required] public string Phase { get; set; } = string.Empty;
    [Required] public string JobName { get; set; } = string.Empty;
    [Required] public string PodUid { get; set; } = string.Empty;
    [Required] public string ApplicationInsightsConnectionString { get; set; } = string.Empty;
    [Range(1, 5)] public int RepeatNumber { get; set; }
    [Required] public string NamespaceName { get; set; } = string.Empty;
    [Required] public string EnrichmentGroupId { get; set; } = string.Empty;
    [Required] public string ScenarioSourceRoot { get; set; } = string.Empty;
    [Required] public string OutputRoot { get; set; } = string.Empty;
    [Required] public string BlobEndpoint { get; set; } = string.Empty;
    [Required] public string BlobContainer { get; set; } = string.Empty;

    [Range(1, int.MaxValue)] public int MaxDocumentCharacters { get; set; }
    [Range(1, int.MaxValue)] public int MaxNestingDepth { get; set; }
    [Range(1, int.MaxValue)] public int MaxScalarCharacters { get; set; }
    [Range(1, int.MaxValue)] public int MaxParserEvents { get; set; }
    [Range(1, int.MaxValue)] public int MaxAttempts { get; set; }
    [Range(1, int.MaxValue)] public int MaxPayloadBytes { get; set; }
    [Range(1, int.MaxValue)] public int MaxRowBytes { get; set; }
    [Range(1, int.MaxValue)] public int MaxManifestBytes { get; set; }
    [Range(1, long.MaxValue)] public long MaxLedgerBytes { get; set; }
    [Range(1, long.MaxValue)] public long MaxLedgerReadbackBytes { get; set; }
    [Range(1, int.MaxValue)] public int LedgerQueueCapacity { get; set; }
    [Range(1, int.MaxValue)] public int EvidenceQueueCapacity { get; set; }
    [Range(1, int.MaxValue)] public int MaxReportBytes { get; set; }
    [Range(1, long.MaxValue)] public long MaxEvidenceFileBytes { get; set; }
    [Range(1, int.MaxValue)] public int MaxDiagnostics { get; set; }
    [Range(1, int.MaxValue)] public int MaxBrokerSeries { get; set; }
    [Range(1, int.MaxValue)] public int MaxSnapshotCharacters { get; set; }
    [Range(1, long.MaxValue)] public long MaxArtifactBytes { get; set; }
    [Range(1, long.MaxValue)] public long MaxAggregateArtifactBytes { get; set; }
    public TimeSpan MaxScheduleLateness { get; set; }
    public TimeSpan ProducerDrainTimeout { get; set; }
    public TimeSpan LedgerDrainTimeout { get; set; }
    public TimeSpan EvidenceCompletionTimeout { get; set; }
    public TimeSpan StatisticsCompletionTimeout { get; set; }
    public TimeSpan UploadTimeout { get; set; }
    public TimeSpan CleanupTimeout { get; set; }
    public TimeSpan ProcessLifetimeTimeout { get; set; }
    public bool? RequireStatisticsEvidence { get; set; }
    public bool? RequireRequestEvidence { get; set; }

    /// <summary>Validates all required inputs, identities, storage destinations, and finite time limits.</summary>
    public void Validate()
    {
        Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);
        if (MaxEvidenceFileBytes > int.MaxValue)
            throw new InvalidOperationException("evidence_file_budget_exceeds_reader_limit");
        if (MaxLedgerBytes > int.MaxValue)
            throw new InvalidOperationException("ledger_file_budget_exceeds_reader_limit");
        foreach (string value in new[]
        {
            ScenarioId, ConfigurationId, RunId, ExpectedInstanceId, ActualInstanceId, PodName,
            ClientId, NamespaceName, EnrichmentGroupId, ScenarioSourceRoot, OutputRoot,
            BlobEndpoint, BlobContainer, Phase, JobName, PodUid, ApplicationInsightsConnectionString
        })
        {
            if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
                throw new InvalidOperationException("required_loadgen_input_missing_or_invalid");
        }
        foreach (string value in new[]
        {
            ScenarioId, ConfigurationId, RunId, ExpectedInstanceId, ActualInstanceId,
            PodName, ClientId, EnrichmentGroupId
        })
        {
            if (value.Any(char.IsWhiteSpace))
                throw new InvalidOperationException("identity_must_not_contain_whitespace");
        }
        if (!StringComparer.Ordinal.Equals(ExpectedInstanceId, ActualInstanceId) ||
            !StringComparer.Ordinal.Equals(ActualInstanceId, PodName) ||
            !StringComparer.Ordinal.Equals(ClientId, PodName))
            throw new InvalidOperationException("worker_and_client_identity_must_match_pod_name");
        if (!System.Text.RegularExpressions.Regex.IsMatch(RunId, "^[a-z0-9]([a-z0-9-]*[a-z0-9])?$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant) || RunId.Length > 29)
            throw new InvalidOperationException("run_id_must_be_dns_label");
        bool validPhaseConfiguration = Phase switch
        {
            "probe" => ConfigurationId == "C0",
            "C0" => ConfigurationId == "C0",
            "C3" => ConfigurationId == "C3",
            _ => false
        };
        if (!validPhaseConfiguration)
            throw new InvalidOperationException("phase_configuration_pair_invalid");
        if (RepeatNumber is < 1 or > 5) throw new InvalidOperationException("repeat_number_out_of_range");
        string expectedJobName = $"{RunId}-{Phase.ToLowerInvariant()}-r{RepeatNumber}-loadgen";
        if (!StringComparer.Ordinal.Equals(JobName, expectedJobName))
            throw new InvalidOperationException("job_name_must_match_run_phase_repeat");
        if (PodUid.Any(char.IsWhiteSpace) || PodUid.Any(char.IsControl))
            throw new InvalidOperationException("pod_uid_must_not_contain_whitespace_or_controls");
        if (StringComparer.Ordinal.Equals(ActualInstanceId, "seed"))
            throw new InvalidOperationException("worker_identity_must_not_be_seed");
        if (!Uri.TryCreate(BlobEndpoint, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrEmpty(endpoint.Host) || endpoint.AbsolutePath != "/" || endpoint.UserInfo.Length != 0 ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
            throw new InvalidOperationException("blob_endpoint_must_be_https_uri");
        if (!StringComparer.Ordinal.Equals(BlobContainer, "ledger"))
            throw new InvalidOperationException("blob_container_must_be_ledger");
        if (MaxScheduleLateness < TimeSpan.Zero || MaxScheduleLateness == Timeout.InfiniteTimeSpan)
            throw new InvalidOperationException("schedule_lateness_budget_must_be_finite_and_nonnegative");
        foreach (long budget in new long[]
        {
            MaxDocumentCharacters, MaxNestingDepth, MaxScalarCharacters, MaxParserEvents, MaxAttempts,
            MaxPayloadBytes, MaxRowBytes, MaxManifestBytes, MaxLedgerBytes, MaxLedgerReadbackBytes,
            LedgerQueueCapacity, EvidenceQueueCapacity, MaxReportBytes, MaxEvidenceFileBytes,
            MaxDiagnostics, MaxBrokerSeries, MaxSnapshotCharacters, MaxArtifactBytes, MaxAggregateArtifactBytes
        })
        {
            if (budget <= 0) throw new InvalidOperationException("resource_budget_must_be_positive");
        }
        ValidatePositiveFinite(ProducerDrainTimeout, nameof(ProducerDrainTimeout));
        ValidatePositiveFinite(LedgerDrainTimeout, nameof(LedgerDrainTimeout));
        ValidatePositiveFinite(EvidenceCompletionTimeout, nameof(EvidenceCompletionTimeout));
        ValidatePositiveFinite(StatisticsCompletionTimeout, nameof(StatisticsCompletionTimeout));
        ValidatePositiveFinite(UploadTimeout, nameof(UploadTimeout));
        ValidatePositiveFinite(CleanupTimeout, nameof(CleanupTimeout));
        ValidatePositiveFinite(ProcessLifetimeTimeout, nameof(ProcessLifetimeTimeout));
        if (!RequireStatisticsEvidence.HasValue || !RequireRequestEvidence.HasValue)
            throw new InvalidOperationException("evidence_requirements_must_be_explicit");
        if (NamespaceName.Length is < 1 or > 63 || !char.IsAsciiLetterOrDigit(NamespaceName[0]) ||
            !char.IsAsciiLetterOrDigit(NamespaceName[^1]) ||
            NamespaceName.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            throw new InvalidOperationException("namespace_name_must_be_one_dns_label");
        if (NamespaceName.Contains("azure", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("namespace_name_must_exclude_reserved_substring");
        _ = Path.GetFullPath(ScenarioSourceRoot);
        _ = Path.GetFullPath(OutputRoot);
        if (StringComparer.Ordinal.Equals(Path.GetFullPath(ScenarioSourceRoot), Path.GetFullPath(OutputRoot)))
            throw new InvalidOperationException("scenario_source_and_output_roots_must_differ");
    }

    private static void ValidatePositiveFinite(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan || value.TotalMilliseconds > uint.MaxValue - 1d)
            throw new InvalidOperationException(name + "_must_be_positive_finite");
    }
}
