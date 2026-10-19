namespace Parchwell.Verifier;

/// <summary>Names the executable verifier mode selected by the runner.</summary>
/// <remarks>Mode selection is case-sensitive because configuration is an execution precondition.</remarks>
internal enum VerifierMode
{
    StartLine,
    Verify,
    ProbeGate
}

/// <summary>Holds runner-supplied verifier settings until startup validation completes.</summary>
/// <remarks>Options bind from the <c>Verifier</c> configuration section; no value has a production default.</remarks>
internal sealed class VerifierHostOptions
{
    public string? Mode { get; set; }
    public string? ScenarioId { get; set; }
    public string? VerifierCommit { get; set; }
    public TimeSpan? ProcessLifetime { get; set; }
    public TimeSpan? CleanupTimeout { get; set; }
    public TimeSpan? IoTimeout { get; set; }
    public long? MaxOutputBytes { get; set; }
    public int? MaxDiagnosticsPerCheck { get; set; }
    public int? MaxDiagnosticBytes { get; set; }

    public string? ConfigurationId { get; set; }
    public string? RunId { get; set; }
    public string? InstanceId { get; set; }
    public string? ExpectedInstanceId { get; set; }
    public string? ClientId { get; set; }
    public string? VerifierClientId { get; set; }
    public string? NamespaceName { get; set; }
    public string? BlobServiceUri { get; set; }
    public string? VerifierContainer { get; set; }
    public TimeSpan? TokenTimeout { get; set; }
    public TimeSpan? MetadataTimeout { get; set; }
    public TimeSpan? WatermarkTimeout { get; set; }
    public TimeSpan? PollSlice { get; set; }
    public TimeSpan? BlobIoTimeout { get; set; }
    public string? TenantId { get; set; }
    public string? WorkloadIdentityClientId { get; set; }
    public string? FederatedTokenFilePath { get; set; }

    public string? StartLineOutputName { get; set; }
    public int? MaxPartitions { get; set; }
    public int? MaxInputDepth { get; set; }

    public string? LedgerContainer { get; set; }
    public string? ArtifactPrefix { get; set; }
    public string? StartLineInputName { get; set; }
    public string? ResultOutputName { get; set; }
    public string? ScenarioRoot { get; set; }
    public List<string>? ExpectedBrokerNames { get; set; }
    public TimeSpan? SafetyStop { get; set; }
    public long? MaxConsumedRecords { get; set; }
    public long? MaxConsumedBytes { get; set; }
    public long? MaxArtifactBytes { get; set; }
    public long? MaxTotalInputBytes { get; set; }
    public int? MaxRowBytes { get; set; }
    public long? MaxRowsPerArtifact { get; set; }
    public long? MaxInventoryEntries { get; set; }
    public int? MaxNativeReportBytes { get; set; }
    public int? MaxBrokerEntries { get; set; }
    public VerifierScenarioLoadOptions? ScenarioLoadOptions { get; set; }

    public string? GateOutputPath { get; set; }
    public List<ExpectedProbeRepeat>? ExpectedRepeats { get; set; }
    public List<string>? RepeatResultPaths { get; set; }
}

/// <summary>Provides the explicit parser limits used while loading a scenario.</summary>
internal sealed class VerifierScenarioLoadOptions
{
    public int? MaxDocumentCharacters { get; set; }
    public int? MaxNestingDepth { get; set; }
    public int? MaxScalarCharacters { get; set; }
    public int? MaxParserEvents { get; set; }
}

/// <summary>Identifies one runner-expected A01/C0 probe repeat.</summary>
internal sealed record ExpectedProbeRepeat
{
    public string? ScenarioId { get; init; }
    public string? ConfigurationId { get; init; }
    public string? RunId { get; init; }
    public string? InstanceId { get; init; }
    public string? ExpectedInstanceId { get; init; }
    public string? ClientId { get; init; }
}

/// <summary>Parses the runner's exact, case-sensitive mode name.</summary>
internal static class VerifierModeParser
{
    public static bool TryParse(string? value, out VerifierMode mode)
    {
        mode = value switch
        {
            "start-line" => VerifierMode.StartLine,
            "verify" => VerifierMode.Verify,
            "probe-gate" => VerifierMode.ProbeGate,
            _ => default
        };

        return value is "start-line" or "verify" or "probe-gate";
    }
}
