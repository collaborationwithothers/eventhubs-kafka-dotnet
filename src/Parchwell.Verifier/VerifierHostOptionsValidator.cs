using Microsoft.Extensions.Options;

namespace Parchwell.Verifier;

/// <summary>Validates runner configuration before a verifier mode can perform work.</summary>
/// <remarks>These are startup preconditions, not numbered evidence checks (ADR-019).</remarks>
internal sealed class VerifierHostOptionsValidator : IValidateOptions<VerifierHostOptions>
{
    private const long MaximumTimeoutTicks = (long)int.MaxValue * TimeSpan.TicksPerMillisecond;

    public ValidateOptionsResult Validate(string? name, VerifierHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        if (!VerifierModeParser.TryParse(options.Mode, out var mode))
        {
            errors.Add("Verifier:Mode must be exactly start-line, verify, judge, or probe-gate.");
            return ValidateOptionsResult.Fail(errors);
        }

        RequireText(options.ScenarioId, "Verifier:ScenarioId", errors);
        RequireText(options.VerifierCommit, "Verifier:VerifierCommit", errors);
        RequireText(options.SourceCommit, "PARCHWELL_SOURCE_COMMIT", errors);
        if (options.SourceCommit is not null && options.VerifierCommit != options.SourceCommit) errors.Add("Verifier:VerifierCommit must equal PARCHWELL_SOURCE_COMMIT.");
        RequirePositive(options.MaxOutputBytes, "Verifier:MaxOutputBytes", errors);
        RequirePositive(options.MaxDiagnosticsPerCheck, "Verifier:MaxDiagnosticsPerCheck", errors);
        RequirePositive(options.MaxDiagnosticBytes, "Verifier:MaxDiagnosticBytes", errors);

        var requiredBudgets = new List<TimeSpan>();
        AddPositive(options.CleanupTimeout, "Verifier:CleanupTimeout", errors, requiredBudgets);
        AddPositive(options.ProcessLifetime, "Verifier:ProcessLifetime", errors, null);

        if (mode is VerifierMode.StartLine or VerifierMode.Verify or VerifierMode.Judge)
        {
            if (options.SourceCommit is not null && (options.SourceCommit.Length != 40 ||
                options.SourceCommit.Any(character => !(character is >= '0' and <= '9' or >= 'a' and <= 'f'))))
                errors.Add("PARCHWELL_SOURCE_COMMIT must be 40 lowercase hexadecimal characters.");
            ValidateC5Identity(options, mode, errors);
            if(mode is VerifierMode.Verify or VerifierMode.Judge)
                AddPositive(options.IoTimeout, "Verifier:IoTimeout", errors, requiredBudgets);
            ValidateContainerName(options.LedgerContainer, "Verifier:LedgerContainer", errors);
            if(mode is VerifierMode.StartLine or VerifierMode.Verify)
            {
                RequireText(options.PodName, "PARCHWELL_POD_NAME", errors);
                RequireText(options.VerifierClientId, "Verifier:VerifierClientId", errors);
                RequireText(options.NamespaceName, "Verifier:NamespaceName", errors);
            }
            if (StringComparer.Ordinal.Equals(options.VerifierContainer, options.LedgerContainer))
                errors.Add("Verifier:VerifierContainer must be separate from Verifier:LedgerContainer.");
            if (mode is VerifierMode.StartLine or VerifierMode.Verify)
            {
                AddPositive(options.TokenTimeout, "Verifier:TokenTimeout", errors, requiredBudgets);
                AddPositive(options.MetadataTimeout, "Verifier:MetadataTimeout", errors, requiredBudgets);
                AddPositive(options.WatermarkTimeout, "Verifier:WatermarkTimeout", errors, requiredBudgets);
                AddPositive(options.PollSlice, "Verifier:PollSlice", errors, null);
                AddPositive(options.BlobIoTimeout, "Verifier:BlobIoTimeout", errors, requiredBudgets);
            }
            if (mode == VerifierMode.Judge)
            {
                RequireText(options.CandidateOutputName, "Verifier:CandidateOutputName", errors);
                if(options.RunId is not null&&(options.Phase is "probe" or "C0" or "C3")&&Common.Phase.PhaseIdentity.IsRunId(options.RunId)&&
                    options.CandidateOutputName!=Common.Phase.PhasePaths.Candidate(options.RunId,options.Phase))
                    errors.Add("Verifier:CandidateOutputName must be the derived phase path.");
                ValidateImageReference(options.LoadGenImage, "Verifier:LoadGenImage", errors);
                ValidateImageReference(options.VerifierImage, "Verifier:VerifierImage", errors);
                RequirePositive(options.MaxArtifactBytes, "Verifier:MaxArtifactBytes", errors);
                RequirePositive(options.MaxTotalInputBytes, "Verifier:MaxTotalInputBytes", errors);
                ValidateScenarioLoadOptions(options.ScenarioLoadOptions, errors);
                if (options.Repeat.HasValue || options.RepeatPrefix is not null || options.ExpectedInstanceId is not null)
                    errors.Add("Judge mode cannot bind a single repeat or expected producer.");
            }
            else if (mode == VerifierMode.StartLine)
            {
                RequireText(options.StartLineOutputName, "Verifier:StartLineOutputName", errors);
                RequireText(options.BrokerInventoryOutputName, "Verifier:BrokerInventoryOutputName", errors);
                RequirePositive(options.MaxPartitions, "Verifier:MaxPartitions", errors);
                RequirePositive(options.MaxInputDepth, "Verifier:MaxInputDepth", errors);
                if ((options.Phase is "probe" or "C0" or "C3") && Common.Phase.PhaseIdentity.IsRunId(options.RunId) && options.Repeat is >= 1 and <= 5)
                {
                    if (options.StartLineOutputName != Common.Phase.PhasePaths.StartLine(options.RunId!, options.Phase!, options.Repeat.Value)) errors.Add("Start-line output path must be the derived repeat path.");
                    if (options.BrokerInventoryOutputName != Common.Phase.PhasePaths.BrokerInventory(options.RunId!, options.Phase!, options.Repeat.Value)) errors.Add("Broker inventory output path must be the derived repeat path.");
                }
            }
            else
            {
                RequireText(options.ResultOutputName, "Verifier:ResultOutputName", errors);
                RequireText(options.ScenarioRoot, "Verifier:ScenarioRoot", errors);
                ValidateOptionalPath(options.GeneratedValuesFile, "Verifier:GeneratedValuesFile", errors);
                RequirePositive(options.MaxConsumedRecords, "Verifier:MaxConsumedRecords", errors);
                RequirePositive(options.MaxConsumedBytes, "Verifier:MaxConsumedBytes", errors);
                RequirePositive(options.MaxArtifactBytes, "Verifier:MaxArtifactBytes", errors);
                RequirePositive(options.MaxTotalInputBytes, "Verifier:MaxTotalInputBytes", errors);
                RequirePositive(options.MaxRowBytes, "Verifier:MaxRowBytes", errors);
                RequirePositive(options.MaxRowsPerArtifact, "Verifier:MaxRowsPerArtifact", errors);
                RequirePositive(options.MaxInventoryEntries, "Verifier:MaxInventoryEntries", errors);
                RequirePositive(options.MaxInputEntries, "Verifier:MaxInputEntries", errors);
                RequirePositive(options.MaxPartitions, "Verifier:MaxPartitions", errors);
                RequirePositive(options.MaxInputDepth, "Verifier:MaxInputDepth", errors);
                RequirePositive(options.MaxNativeReportBytes, "Verifier:MaxNativeReportBytes", errors);
                RequirePositive(options.MaxBrokerEntries, "Verifier:MaxBrokerEntries", errors);
                AddPositive(options.SafetyStop, "Verifier:SafetyStop", errors, requiredBudgets);
                ValidateScenarioLoadOptions(options.ScenarioLoadOptions, errors);
                if (options.ExpectedBrokerNames is not null)
                    errors.Add("Verifier:ExpectedBrokerNames is not accepted; broker inventory is discovered independently.");
                if ((options.Phase is "probe" or "C0" or "C3") && Common.Phase.PhaseIdentity.IsRunId(options.RunId) && options.Repeat is >= 1 and <= 5)
                {
                    if (options.StartLineInputName != Common.Phase.PhasePaths.StartLine(options.RunId!, options.Phase!, options.Repeat.Value)) errors.Add("Start-line input path must be the derived repeat path.");
                    if (options.ResultOutputName != Common.Phase.PhasePaths.Result(options.RunId!, options.Phase!, options.Repeat.Value)) errors.Add("Result output path must be the derived repeat path.");
                    if (options.ArtifactPrefix != Common.Phase.PhasePaths.ProducersPrefix(options.RunId!, options.Phase!, options.Repeat.Value)) errors.Add("Producer prefix must be the derived path.");
                }
            }
            ValidateBudgetSum(requiredBudgets, options.ProcessLifetime, errors);
            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }

        switch (mode)
        {
            case VerifierMode.StartLine:
                AddKafkaInputs(options, errors, requiredBudgets);
                RequireText(options.StartLineOutputName, "Verifier:StartLineOutputName", errors);
                if (options.BrokerInventoryOutputName is not null)
                {
                    RequireText(options.BrokerInventoryOutputName, "Verifier:BrokerInventoryOutputName", errors);
                    if (options.BrokerInventoryOutputName == options.StartLineOutputName)
                        errors.Add("Broker inventory and start-line output names must differ.");
                }
                RequirePositive(options.MaxPartitions, "Verifier:MaxPartitions", errors);
                RequirePositive(options.MaxInputDepth, "Verifier:MaxInputDepth", errors);
                break;

            case VerifierMode.Verify:
                AddKafkaInputs(options, errors, requiredBudgets);
                AddPositive(options.IoTimeout, "Verifier:IoTimeout", errors, requiredBudgets);
                ValidateContainerName(options.LedgerContainer, "Verifier:LedgerContainer", errors);
                RequireText(options.ArtifactPrefix, "Verifier:ArtifactPrefix", errors);
                RequireText(options.StartLineInputName, "Verifier:StartLineInputName", errors);
                RequireText(options.ResultOutputName, "Verifier:ResultOutputName", errors);
                RequireText(options.ScenarioRoot, "Verifier:ScenarioRoot", errors);
                ValidateOptionalPath(options.GeneratedValuesFile, "Verifier:GeneratedValuesFile", errors);
                RequireCollection(options.ExpectedBrokerNames, "Verifier:ExpectedBrokerNames", errors);
                AddPositive(options.SafetyStop, "Verifier:SafetyStop", errors, requiredBudgets);
                RequirePositive(options.MaxConsumedRecords, "Verifier:MaxConsumedRecords", errors);
                RequirePositive(options.MaxConsumedBytes, "Verifier:MaxConsumedBytes", errors);
                RequirePositive(options.MaxArtifactBytes, "Verifier:MaxArtifactBytes", errors);
                RequirePositive(options.MaxTotalInputBytes, "Verifier:MaxTotalInputBytes", errors);
                RequirePositive(options.MaxRowBytes, "Verifier:MaxRowBytes", errors);
                RequirePositive(options.MaxRowsPerArtifact, "Verifier:MaxRowsPerArtifact", errors);
                RequirePositive(options.MaxInventoryEntries, "Verifier:MaxInventoryEntries", errors);
                RequirePositive(options.MaxInputEntries, "Verifier:MaxInputEntries", errors);
                RequirePositive(options.MaxPartitions, "Verifier:MaxPartitions", errors);
                RequirePositive(options.MaxInputDepth, "Verifier:MaxInputDepth", errors);
                RequirePositive(options.MaxNativeReportBytes, "Verifier:MaxNativeReportBytes", errors);
                RequirePositive(options.MaxBrokerEntries, "Verifier:MaxBrokerEntries", errors);
                ValidateScenarioLoadOptions(options.ScenarioLoadOptions, errors);
                if (StringComparer.Ordinal.Equals(options.VerifierContainer, options.LedgerContainer))
                {
                    errors.Add("Verifier:VerifierContainer must be separate from Verifier:LedgerContainer.");
                }

                break;

            case VerifierMode.ProbeGate:
                AddPositive(options.IoTimeout, "Verifier:IoTimeout", errors, requiredBudgets);
                RequireText(options.GateOutputPath, "Verifier:GateOutputPath", errors);
                RequireText(options.ScenarioRoot, "Verifier:ScenarioRoot", errors);
                RequirePositive(options.MaxArtifactBytes, "Verifier:MaxArtifactBytes", errors);
                RequirePositive(options.MaxTotalInputBytes, "Verifier:MaxTotalInputBytes", errors);
                RequirePositive(options.MaxInputDepth, "Verifier:MaxInputDepth", errors);
                ValidateScenarioLoadOptions(options.ScenarioLoadOptions, errors);
                ValidateExpectedRepeats(options.ExpectedRepeats, errors);
                RequireCollection(options.RepeatResultPaths, "Verifier:RepeatResultPaths", errors);
                break;
        }

        ValidateBudgetSum(requiredBudgets, options.ProcessLifetime, errors);
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static void AddKafkaInputs(
        VerifierHostOptions options,
        List<string> errors,
        ICollection<TimeSpan> requiredBudgets)
    {
        RequireText(options.ConfigurationId, "Verifier:ConfigurationId", errors);
        RequireText(options.RunId, "Verifier:RunId", errors);
        RequireText(options.VerifierClientId, "Verifier:VerifierClientId", errors);
        RequireText(options.NamespaceName, "Verifier:NamespaceName", errors);
        RequireAbsoluteUri(options.BlobServiceUri, "Verifier:BlobServiceUri", errors);
        ValidateContainerName(options.VerifierContainer, "Verifier:VerifierContainer", errors);
        RequireText(options.PodName, "PARCHWELL_POD_NAME", errors);
        if(options.PodName is { } podName && (podName.Length>253||podName.Contains('/')||podName.Any(char.IsControl)))
            errors.Add("PARCHWELL_POD_NAME must be bounded text without slash or control characters.");
        RequireText(options.TenantId, "Verifier:TenantId", errors);
        RequireText(options.WorkloadIdentityClientId, "Verifier:WorkloadIdentityClientId", errors);
        RequireText(options.FederatedTokenFilePath, "Verifier:FederatedTokenFilePath", errors);
        AddPositive(options.TokenTimeout, "Verifier:TokenTimeout", errors, requiredBudgets);
        AddPositive(options.MetadataTimeout, "Verifier:MetadataTimeout", errors, requiredBudgets);
        AddPositive(options.WatermarkTimeout, "Verifier:WatermarkTimeout", errors, requiredBudgets);
        AddPositive(options.PollSlice, "Verifier:PollSlice", errors, null);
        AddPositive(options.BlobIoTimeout, "Verifier:BlobIoTimeout", errors, requiredBudgets);
    }

    private static void ValidateC5Identity(VerifierHostOptions options, VerifierMode mode, List<string> errors)
    {
        RequireText(options.RunId, "Verifier:RunId", errors);
        if (!Common.Phase.PhaseIdentity.IsRunId(options.RunId)) errors.Add("Verifier:RunId must be a DNS-safe run identifier.");
        RequireText(options.Phase, "Verifier:Phase", errors);
        if (options.Phase is not ("probe" or "C0" or "C3")) errors.Add("Verifier:Phase must be exactly probe, C0, or C3.");
        RequireText(options.ConfigurationId, "Verifier:ConfigurationId", errors);
        if ((options.Phase is "probe" or "C0" or "C3") && options.ConfigurationId != Common.Phase.PhaseIdentity.ConfigurationFor(options.Phase))
            errors.Add("Verifier:ConfigurationId does not match Verifier:Phase.");
        RequireText(options.VerifierCommit, "Verifier:VerifierCommit", errors);
        RequireText(options.TenantId ?? Environment.GetEnvironmentVariable("AZURE_TENANT_ID"), "Verifier:TenantId/AZURE_TENANT_ID", errors);
        RequireText(options.WorkloadIdentityClientId ?? Environment.GetEnvironmentVariable("AZURE_CLIENT_ID"), "Verifier:WorkloadIdentityClientId/AZURE_CLIENT_ID", errors);
        RequireText(options.FederatedTokenFilePath ?? Environment.GetEnvironmentVariable("AZURE_FEDERATED_TOKEN_FILE"), "Verifier:FederatedTokenFilePath/AZURE_FEDERATED_TOKEN_FILE", errors);
        RequireAbsoluteUri(options.BlobServiceUri, "Verifier:BlobServiceUri", errors);
        ValidateContainerName(options.VerifierContainer, "Verifier:VerifierContainer", errors);
        if (mode == VerifierMode.Judge)
        {
            if (options.Repeat.HasValue) errors.Add("Judge mode forbids Verifier:Repeat.");
            if (Common.Phase.PhaseIdentity.IsRunId(options.RunId) && (options.Phase is "probe" or "C0" or "C3"))
            {
                if (options.JobName != Common.Phase.PhaseIdentity.JudgeJobName(options.RunId!, options.Phase!)) errors.Add("Verifier:JobName does not match the derived judge Job.");
                if (options.PhasePrefix != Common.Phase.PhaseIdentity.PhasePrefix(options.RunId!, options.Phase!)) errors.Add("Verifier:PhasePrefix does not match the derived phase path.");
            }
            if (options.RepeatPrefix is not null || options.ExpectedInstanceId is not null) errors.Add("Judge mode forbids repeat and producer binding fields.");
        }
        else
        {
            if (options.Repeat is not (>= 1 and <= 5)) errors.Add("Verifier:Repeat must be between 1 and 5.");
            string expectedJob = (options.Phase is "probe" or "C0" or "C3") && Common.Phase.PhaseIdentity.IsRunId(options.RunId) && options.Repeat is >= 1 and <= 5
                ? mode == VerifierMode.StartLine
                    ? Common.Phase.PhaseIdentity.StartLineJobName(options.RunId!, options.Phase!, options.Repeat.Value)
                    : Common.Phase.PhaseIdentity.VerifyJobName(options.RunId!, options.Phase!, options.Repeat.Value)
                : string.Empty;
            if (options.JobName != expectedJob) errors.Add("Verifier:JobName does not match the derived LoadGen Job.");
            if (expectedJob.Length > 0)
            {
                if (options.RepeatPrefix != Common.Phase.PhaseIdentity.RepeatPrefix(options.RunId!, options.Phase!, options.Repeat is >= 1 and <= 5 ? options.Repeat.Value : 1)) errors.Add("Verifier:RepeatPrefix does not match the derived repeat path.");
                if (options.PhasePrefix != Common.Phase.PhaseIdentity.PhasePrefix(options.RunId!, options.Phase!)) errors.Add("Verifier:PhasePrefix does not match the derived phase path.");
            }
            string loadgen = (options.Phase is "probe" or "C0" or "C3") && Common.Phase.PhaseIdentity.IsRunId(options.RunId) && options.Repeat is >= 1 and <= 5
                ? Common.Phase.PhaseIdentity.JobName(options.RunId!, options.Phase!, options.Repeat.Value) : string.Empty;
            if (options.ExpectedInstanceId != loadgen) errors.Add("Verifier:ExpectedInstanceId must equal the derived LoadGen Job name.");
        }
        if (options.LedgerContainer is null) ValidateContainerName(options.LedgerContainer, "Verifier:LedgerContainer", errors);
        if (options.LedgerContainer is not null && options.VerifierContainer is not null && options.LedgerContainer == options.VerifierContainer)
            errors.Add("Verifier containers must be separate.");
    }

    private static void ValidateImageReference(string? value, string name, List<string> errors)
    {
        RequireText(value, name, errors);
        if (value is null) return;
        int at=value.LastIndexOf("@sha256:",StringComparison.Ordinal);
        if(at<=0 || value.Length-at!="@sha256:".Length+64 || value[(at+8)..].Any(c=>!(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            errors.Add($"{name} must be an immutable image reference ending in @sha256 and 64 lowercase hex characters.");
    }

    private static void ValidateScenarioLoadOptions(VerifierScenarioLoadOptions? limits, List<string> errors)
    {
        if (limits is null)
        {
            errors.Add("Verifier:ScenarioLoadOptions is required.");
            return;
        }

        RequirePositive(limits.MaxDocumentCharacters, "Verifier:ScenarioLoadOptions:MaxDocumentCharacters", errors);
        RequirePositive(limits.MaxNestingDepth, "Verifier:ScenarioLoadOptions:MaxNestingDepth", errors);
        RequirePositive(limits.MaxScalarCharacters, "Verifier:ScenarioLoadOptions:MaxScalarCharacters", errors);
        RequirePositive(limits.MaxParserEvents, "Verifier:ScenarioLoadOptions:MaxParserEvents", errors);
    }

    private static void ValidateOptionalPath(string? value, string name, List<string> errors)
    {
        if (value is null) return;
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
        {
            errors.Add($"{name} must be a nonblank path without control characters.");
            return;
        }

        try
        {
            _ = Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            errors.Add($"{name} must be a valid path.");
        }
    }

    /// <summary>
    /// Validates Azure Blob container name syntax without checking whether the container exists or can be written.
    /// </summary>
    /// <remarks>
    /// Ordinary names follow Microsoft's 3-63 character lowercase alphanumeric and hyphen rules, with
    /// alphanumeric ends and no consecutive hyphens. The pinned Azure Storage SDK also defines the special
    /// container names <c>$root</c>, <c>$logs</c> and <c>$web</c>.
    /// Naming rules: MicrosoftDocs/azure-docs at commit 75162ac564772aaf7e37a740dbc56540e08b377e,
    /// includes/storage-dev-guides/storage-dev-guide-about-container-naming.md; edge and dash rules:
    /// MicrosoftDocs/azure-docs at commit 8ef1ae8a3759ddb2ed2e437bf38083aa2268c39f,
    /// articles/databox/data-box-troubleshoot.md (ERROR_CONTAINER_OR_SHARE_NAME_LENGTH,
    /// ALPHA_NUMERIC_DASH and IMPROPER_DASH); special names: Azure/azure-sdk-for-net at commit
    /// 999c8a67bd73f03457eb7e1406059764c4a28c32, sdk/storage/Azure.Storage.Blobs/src/BlobContainerClient.cs
    /// and sdk/storage/Azure.Storage.Common/src/Shared/Constants.cs.
    /// </remarks>
    private static void ValidateContainerName(string? value, string optionName, List<string> errors)
    {
        RequireText(value, optionName, errors);
        if (string.IsNullOrWhiteSpace(value)
            || StringComparer.Ordinal.Equals(value, "$root")
            || StringComparer.Ordinal.Equals(value, "$logs")
            || StringComparer.Ordinal.Equals(value, "$web"))
        {
            return;
        }

        if (value.Length is < 3 or > 63
            || value[0] == '-'
            || value[^1] == '-'
            || value.Contains("--", StringComparison.Ordinal))
        {
            errors.Add($"{optionName} is not a valid Azure Blob container name.");
            return;
        }

        foreach (char character in value)
        {
            if (!IsLowercaseAsciiLetterOrDigit(character) && character != '-')
            {
                errors.Add($"{optionName} is not a valid Azure Blob container name.");
                return;
            }
        }

    }

    private static bool IsLowercaseAsciiLetterOrDigit(char value) =>
        value is >= 'a' and <= 'z' or >= '0' and <= '9';

    private static void ValidateExpectedRepeats(List<ExpectedProbeRepeat>? repeats, List<string> errors)
    {
        if (repeats is null)
        {
            errors.Add("Verifier:ExpectedRepeats is required; supply an explicit empty list when there are no expected identities.");
            return;
        }

        for (var index = 0; index < repeats.Count; index++)
        {
            var repeat = repeats[index];
            if (repeat is null)
            {
                errors.Add($"Verifier:ExpectedRepeats:{index} is required.");
                continue;
            }

            RequireText(repeat.ScenarioId, $"Verifier:ExpectedRepeats:{index}:ScenarioId", errors);
            RequireText(repeat.ConfigurationId, $"Verifier:ExpectedRepeats:{index}:ConfigurationId", errors);
            RequireText(repeat.RunId, $"Verifier:ExpectedRepeats:{index}:RunId", errors);
            RequireText(repeat.InstanceId, $"Verifier:ExpectedRepeats:{index}:InstanceId", errors);
            RequireText(repeat.ExpectedInstanceId, $"Verifier:ExpectedRepeats:{index}:ExpectedInstanceId", errors);
            RequireText(repeat.ClientId, $"Verifier:ExpectedRepeats:{index}:ClientId", errors);
        }
    }

    private static void ValidateBudgetSum(
        IReadOnlyCollection<TimeSpan> budgets,
        TimeSpan? processLifetime,
        List<string> errors)
    {
        if (!processLifetime.HasValue || !IsValidDuration(processLifetime.Value))
        {
            return;
        }

        if (!TrySumBudgetTicks(budgets, out long totalTicks))
        {
            errors.Add("Verifier's required budget sum exceeds the supported duration range.");
            return;
        }

        if (processLifetime.Value.Ticks <= totalTicks)
        {
            errors.Add("Verifier:ProcessLifetime must be strictly greater than the sum of this mode's required budgets.");
        }
    }

    internal static bool TrySumBudgetTicks(IEnumerable<TimeSpan> budgets, out long totalTicks)
    {
        totalTicks = 0;
        try
        {
            foreach (TimeSpan budget in budgets)
            {
                totalTicks = checked(totalTicks + budget.Ticks);
            }

            return true;
        }
        catch (OverflowException)
        {
            totalTicks = 0;
            return false;
        }
    }

    private static void AddPositive(
        TimeSpan? value,
        string name,
        List<string> errors,
        ICollection<TimeSpan>? budgets)
    {
        if (!value.HasValue || !IsValidDuration(value.Value))
        {
            errors.Add($"{name} must be a positive duration that fits the timeout representation.");
            return;
        }

        budgets?.Add(value.Value);
    }

    internal static bool IsValidDuration(TimeSpan value) =>
        value > TimeSpan.Zero && value.Ticks <= MaximumTimeoutTicks;

    private static void RequirePositive(long? value, string name, List<string> errors)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            errors.Add($"{name} must be a positive integer.");
        }
    }

    private static void RequirePositive(int? value, string name, List<string> errors) =>
        RequirePositive(value.HasValue ? (long?)value.Value : null, name, errors);

    private static void RequireText(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} is required and cannot be blank.");
        }
    }

    private static void RequireCollection<T>(IReadOnlyCollection<T>? values, string name, List<string> errors)
    {
        if (values is null)
        {
            errors.Add($"{name} is required.");
        }
    }

    private static void RequireAbsoluteUri(string? value, string name, List<string> errors)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            errors.Add($"{name} must be an absolute URI.");
            return;
        }
        if(uri.Scheme!="https"||string.IsNullOrEmpty(uri.Host)||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||
            !string.IsNullOrEmpty(uri.Fragment)||uri.AbsolutePath!="/")errors.Add($"{name} must be an HTTPS account-root URI without userinfo, query or fragment.");
    }
}
