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
    [Required] public string NamespaceName { get; set; } = string.Empty;
    [Required] public string EnrichmentGroupId { get; set; } = string.Empty;
    [Required] public string ScenarioSourceRoot { get; set; } = string.Empty;
    [Required] public string OutputRoot { get; set; } = string.Empty;
    [Required] public string BlobEndpoint { get; set; } = string.Empty;
    [Required] public string BlobContainer { get; set; } = string.Empty;
    [Required] public string ExpectedBrokerNamesCsv { get; set; } = string.Empty;

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
            BlobEndpoint, BlobContainer, ExpectedBrokerNamesCsv
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
        string[] brokerNames = GetExpectedBrokerNames();
        if (brokerNames.Length == 0 || brokerNames.Length > MaxBrokerSeries)
            throw new InvalidOperationException("independent_expected_broker_inventory_required");
        if (NamespaceName.Length is < 1 or > 63 || !char.IsAsciiLetterOrDigit(NamespaceName[0]) ||
            !char.IsAsciiLetterOrDigit(NamespaceName[^1]) ||
            NamespaceName.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            throw new InvalidOperationException("namespace_name_must_be_one_dns_label");
        _ = Path.GetFullPath(ScenarioSourceRoot);
        _ = Path.GetFullPath(OutputRoot);
        if (StringComparer.Ordinal.Equals(Path.GetFullPath(ScenarioSourceRoot), Path.GetFullPath(OutputRoot)))
            throw new InvalidOperationException("scenario_source_and_output_roots_must_differ");
    }

    public string[] GetExpectedBrokerNames()
    {
        string[] names = ExpectedBrokerNamesCsv.Split(',', StringSplitOptions.TrimEntries);
        if (names.Any(string.IsNullOrWhiteSpace) || names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new InvalidOperationException("expected_broker_inventory_must_be_unique_and_nonempty");
        return names;
    }

    private static void ValidatePositiveFinite(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan || value.TotalMilliseconds > uint.MaxValue - 1d)
            throw new InvalidOperationException(name + "_must_be_positive_finite");
    }
}
