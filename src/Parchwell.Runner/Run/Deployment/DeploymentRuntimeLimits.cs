using System.Globalization;
using System.Text.Json;

namespace Parchwell.Runner.Run.Deployment;

/// <summary>Validates the explicit deployment limits before worker configuration is emitted or used.</summary>
internal static class DeploymentRuntimeLimits
{
    private const long MaximumDurationTicks = (long)int.MaxValue * TimeSpan.TicksPerMillisecond;
    private static readonly Section[] Sections =
    [
        new("scenario_parsing", Integers: "MaxDocumentCharacters MaxNestingDepth MaxScalarCharacters MaxParserEvents"),
        // LoadGen's ledger/evidence readers cap those long-valued byte limits at Int32.MaxValue.
        new("loadgen",
            Integers: "MaxAttempts MaxPayloadBytes MaxRowBytes MaxManifestBytes LedgerQueueCapacity EvidenceQueueCapacity MaxReportBytes MaxSnapshotCharacters MaxDiagnostics MaxBrokerSeries MaxLedgerBytes MaxEvidenceFileBytes",
            LongIntegers: "MaxLedgerReadbackBytes MaxArtifactBytes MaxAggregateArtifactBytes",
            Durations: "ProducerDrainTimeout LedgerDrainTimeout EvidenceCompletionTimeout StatisticsCompletionTimeout UploadTimeout CleanupTimeout ProcessLifetimeTimeout",
            Text: "ScenarioSourceRoot OutputRoot EnrichmentGroupId",
            Booleans: "RequireStatisticsEvidence RequireRequestEvidence"),
        new("verifier_shared", Integers: "MaxDiagnosticsPerCheck MaxDiagnosticBytes MaxInputDepth",
            LongIntegers: "MaxOutputBytes", Durations: "CleanupTimeout IoTimeout"),
        new("verifier_kafka", Integers: "MaxPartitions",
            Durations: "TokenTimeout MetadataTimeout WatermarkTimeout PollSlice BlobIoTimeout", Text: "ScenarioRoot"),
        new("verifier_start_line", Durations: "ProcessLifetime"),
        new("verifier_verify", Integers: "MaxRowBytes MaxNativeReportBytes MaxBrokerEntries",
            LongIntegers: "MaxConsumedRecords MaxConsumedBytes MaxArtifactBytes MaxTotalInputBytes MaxRowsPerArtifact MaxInventoryEntries MaxInputEntries",
            Durations: "ProcessLifetime SafetyStop"),
        new("verifier_gate", LongIntegers: "MaxArtifactBytes MaxTotalInputBytes", Durations: "ProcessLifetime", Text: "ScenarioRoot"),
        new("execution",
            Integers: "phase_count repeats_per_phase admission_seconds admission_poll_seconds admission_bytes loadgen_job_seconds verify_job_seconds start_line_job_seconds gate_job_seconds loadgen_seconds verify_seconds start_line_seconds gate_seconds repeat_seconds phase_seconds apply_seconds export_seconds destroy_seconds api_seconds watch_events json_document_bytes kubernetes_poll_seconds read_attempts store_operation_seconds producer_artifact_bytes repeat_input_bytes container_log_bytes kql_rows kql_bytes kql_reads kql_spacing_seconds ledger_bytes manifest_bytes manifest_entries termination_grace_seconds expiry_hours phase_job_minutes sweep_job_minutes offline_command_seconds",
            LongIntegers: "phase_evidence_bytes")
    ];

    public static void Validate(JsonElement limits)
    {
        JsonElement schema = Required(limits, "schema_version");
        if (schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int version) || version != 1)
            throw new InvalidDataException("limits_schema_invalid");
        foreach (Section contract in Sections)
        {
            JsonElement section = Required(limits, contract.Name);
            if (section.ValueKind != JsonValueKind.Object) throw new InvalidDataException("limits_section_required");
            foreach (string name in Names(contract.Integers)) ValidateInteger(Required(section, name), int.MaxValue);
            foreach (string name in Names(contract.LongIntegers)) ValidateInteger(Required(section, name), long.MaxValue);
            foreach (string name in Names(contract.Durations)) ValidateDuration(Required(section, name), allowZero: false);
            foreach (string name in Names(contract.Text))
            {
                JsonElement value = Required(section, name);
                if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()) || value.GetString()!.Any(char.IsControl))
                    throw new InvalidDataException("limits_text_invalid");
            }
            foreach (string name in Names(contract.Booleans))
                if (Required(section, name).ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new InvalidDataException("limits_boolean_invalid");
        }
        // LoadGen permits an on-time-only schedule; its lateness allowance can be zero.
        ValidateDuration(Required(Required(limits, "loadgen"), "MaxScheduleLateness"), allowZero: true);
        JsonElement execution = Required(limits, "execution");
        ValidateInteger(Required(execution, "replacement_repeats"), int.MaxValue, allowZero: true);
        JsonElement price = Required(execution, "item_limit_gbp");
        if (price.ValueKind != JsonValueKind.Number || !price.TryGetDecimal(out decimal amount) || amount <= 0)
            throw new InvalidDataException("execution_limit_invalid");
        JsonElement delays = Required(execution, "read_delay_seconds");
        if (delays.ValueKind != JsonValueKind.Array || delays.GetArrayLength() == 0)
            throw new InvalidDataException("execution_limit_invalid");
        foreach (JsonElement delay in delays.EnumerateArray()) ValidateInteger(delay, int.MaxValue);
    }

    private static JsonElement Required(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out JsonElement value)
            ? value : throw new InvalidDataException("limits_required_input_missing");

    private static void ValidateInteger(JsonElement value, long maximum, bool allowZero = false)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long number) || number > maximum || number < (allowZero ? 0 : 1))
            throw new InvalidDataException("execution_limit_invalid");
    }

    private static void ValidateDuration(JsonElement value, bool allowZero)
    {
        if (value.ValueKind != JsonValueKind.String || !TimeSpan.TryParse(value.GetString(), CultureInfo.InvariantCulture, out TimeSpan duration) ||
            duration.Ticks < (allowZero ? 0 : 1) || duration.Ticks > MaximumDurationTicks)
            throw new InvalidDataException("execution_duration_invalid");
    }

    private static string[] Names(string names) => names.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    private sealed record Section(string Name, string Integers = "", string LongIntegers = "", string Durations = "", string Text = "", string Booleans = "");
}
