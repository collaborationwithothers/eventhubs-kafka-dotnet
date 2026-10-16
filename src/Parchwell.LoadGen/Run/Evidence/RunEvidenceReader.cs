using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Evidence;

/// <summary>Bounded summary of persisted evidence. Record counts describe artifact rows, not Kafka outcomes.</summary>
public sealed record RunEvidenceReadResult(
    int StatisticsRecordCount,
    int LifecycleRecordCount,
    bool IsFaulted,
    bool RequestCapabilitySupported,
    bool? StatisticsComplete,
    ImmutableArray<string> CapabilityStatuses,
    ImmutableArray<string> GuardObservations,
    bool GuardObservationsComplete,
    ImmutableArray<string> SessionDiagnostics);

/// <summary>Strict, bounded reader for the version 1 evidence artifacts.</summary>
public static class RunEvidenceReader
{
    private const int SchemaVersion = 1;
    private const int MaximumJsonDepth = 32;
    private const int MaximumLifecycleFieldNames = 32;
    private static readonly byte[] Utf8Bom = [0xef, 0xbb, 0xbf];
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonDocumentOptions JsonOptions = new() { MaxDepth = MaximumJsonDepth, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false };
    private static readonly string[] ArtifactNames = ["statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
    private static readonly string[] KnownMissingLifecycleFields = ["source_event_timestamp", "correlation_id", "source_order"];
    private static readonly string[] IdentityFields = ["scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id"];
    /// <summary>Reads and validates all three artifacts without parsing the native raw JSON string.</summary>
    public static async Task<RunEvidenceReadResult> ReadAsync(
        string directoryPath,
        RunIdentity expectedIdentity,
        RunEvidenceExpectations expectations,
        long maximumFileBytes,
        int maximumRowBytes,
        int maximumDiagnostics,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        ArgumentNullException.ThrowIfNull(expectations);
        expectedIdentity.Validate();
        if (maximumFileBytes <= 0 || maximumFileBytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maximumFileBytes), "Evidence file limit must fit in a positive Int32-sized buffer.");
        if (maximumRowBytes <= 0 || maximumRowBytes > maximumFileBytes)
            throw new ArgumentOutOfRangeException(nameof(maximumRowBytes));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDiagnostics);

        string root = Path.GetFullPath(directoryPath);
        byte[] statisticsBytes = await ReadBoundedFileAsync(Path.Combine(root, ArtifactNames[0]), maximumFileBytes, cancellationToken).ConfigureAwait(false);
        byte[] lifecycleBytes = await ReadBoundedFileAsync(Path.Combine(root, ArtifactNames[1]), maximumFileBytes, cancellationToken).ConfigureAwait(false);
        byte[] capabilityBytes = await ReadBoundedFileAsync(Path.Combine(root, ArtifactNames[2]), maximumFileBytes, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return ReadBytes(statisticsBytes, lifecycleBytes, capabilityBytes, expectedIdentity,
            expectations, maximumFileBytes, maximumRowBytes, maximumDiagnostics);
    }

    /// <summary>Validates the three evidence artifacts directly from bounded byte buffers without filesystem access.</summary>
    internal static RunEvidenceReadResult ReadBytes(byte[] statisticsBytes, byte[] lifecycleBytes, byte[] capabilityBytes,
        RunIdentity expectedIdentity, RunEvidenceExpectations expectations, long maximumFileBytes, int maximumRowBytes,
        int maximumDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(statisticsBytes);
        ArgumentNullException.ThrowIfNull(lifecycleBytes);
        ArgumentNullException.ThrowIfNull(capabilityBytes);
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        ArgumentNullException.ThrowIfNull(expectations);
        expectedIdentity.Validate();
        if (maximumFileBytes <= 0 || maximumFileBytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maximumFileBytes));
        if (maximumRowBytes <= 0 || maximumRowBytes > maximumFileBytes)
            throw new ArgumentOutOfRangeException(nameof(maximumRowBytes));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDiagnostics);
        if (statisticsBytes.LongLength > maximumFileBytes || lifecycleBytes.LongLength > maximumFileBytes ||
            capabilityBytes.LongLength > maximumFileBytes)
            throw new InvalidDataException("evidence_file_exceeds_byte_limit");

        int maximumRows = (int)Math.Min(int.MaxValue, maximumFileBytes);
        var statisticsAttribution = new StatisticsAttribution();
        int statisticsCount = ValidateJsonLines(statisticsBytes, maximumRowBytes, maximumRows,
            row => ValidateStatisticsRecord(row, expectedIdentity, maximumRows, maximumDiagnostics, statisticsAttribution));
        var lifecycleAttribution = new LifecycleAttribution();
        int lifecycleCount = ValidateJsonLines(lifecycleBytes, maximumRowBytes, maximumRows,
            row => ValidateLifecycleRecord(row, expectedIdentity, maximumDiagnostics, lifecycleAttribution));

        using JsonDocument capabilities = ParseDocument(capabilityBytes);
        JsonElement rootElement = capabilities.RootElement;
        RejectDuplicatePropertiesRecursively(rootElement);
        ValidateCapabilityReport(rootElement, expectedIdentity, expectations, statisticsCount, maximumRows, maximumDiagnostics,
            statisticsAttribution.ObservedBrokerNames, statisticsAttribution.HasReportDiagnostics,
            statisticsAttribution.HasUnparsedReports, statisticsAttribution.PositiveGuardValues,
            statisticsAttribution.HasNonIncreasingTimestamp, statisticsAttribution.HasNativeCreationChanged,
            statisticsAttribution.HasDecreasingCounter, statisticsAttribution.HasDuplicateBrokerName,
            lifecycleAttribution.HasErrorCallback,
            out bool isFaulted, out bool requestCapabilitySupported, out bool? statisticsComplete,
            out ImmutableArray<string> statuses, out ImmutableArray<string> guardObservations,
            out bool guardObservationsComplete, out ImmutableArray<string> diagnostics);
        return new RunEvidenceReadResult(statisticsCount, lifecycleCount, isFaulted, requestCapabilitySupported,
            statisticsComplete, statuses, guardObservations, guardObservationsComplete, diagnostics);
    }

    private static async Task<byte[]> ReadBoundedFileAsync(string path, long maximumBytes, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        long length = stream.Length;
        if (length < 0 || length > maximumBytes || length > int.MaxValue)
            throw new InvalidDataException("evidence_file_exceeds_byte_limit");
        byte[] bytes = new byte[(int)length];
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = await stream.ReadAsync(bytes.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new InvalidDataException("evidence_file_truncated_during_read");
            offset += read;
        }
        byte[] extra = new byte[1];
        if (await stream.ReadAsync(extra, cancellationToken).ConfigureAwait(false) != 0)
            throw new InvalidDataException("evidence_file_grew_beyond_byte_limit");
        return bytes;
    }

    private static int ValidateJsonLines(byte[] bytes, int maximumRowBytes, int maximumRows, Action<JsonElement> validateRow)
    {
        ValidateUtf8(bytes);
        if (bytes.AsSpan().StartsWith(Utf8Bom))
            throw new InvalidDataException("evidence_jsonl_bom_not_allowed");
        if (bytes.Length == 0) return 0;
        if (bytes[^1] != (byte)'\n') throw new InvalidDataException("evidence_jsonl_missing_final_lf");
        int count = 0;
        int start = 0;
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != (byte)'\n') continue;
            int rowLength = index - start;
            if (rowLength == 0) throw new InvalidDataException("evidence_jsonl_empty_row");
            if (rowLength > maximumRowBytes) throw new InvalidDataException("evidence_jsonl_row_exceeds_byte_limit");
            if (count >= maximumRows) throw new InvalidDataException("evidence_jsonl_row_count_exceeded");
            if (bytes[index - 1] == (byte)'\r') throw new InvalidDataException("evidence_jsonl_crlf_not_canonical");
            using JsonDocument row = JsonDocument.Parse(bytes.AsMemory(start, rowLength), JsonOptions);
            RejectDuplicatePropertiesRecursively(row.RootElement);
            validateRow(row.RootElement);
            count++;
            start = index + 1;
        }
        return count;
    }

    private static JsonDocument ParseDocument(byte[] bytes)
    {
        ValidateUtf8(bytes);
        if (bytes.AsSpan().StartsWith(Utf8Bom))
            throw new InvalidDataException("evidence_json_bom_not_allowed");
        try
        {
            return JsonDocument.Parse(bytes, JsonOptions);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("evidence_json_invalid_utf8", exception);
        }
    }

    private static void ValidateUtf8(byte[] bytes)
    {
        try
        {
            _ = StrictUtf8.GetCharCount(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("evidence_json_invalid_utf8", exception);
        }
    }

    private static void ValidateStatisticsRecord(JsonElement row, RunIdentity expected, int maximumRows, int maximumDiagnostics,
        StatisticsAttribution attribution)
    {
        RequireObject(row, "schema_version", "identity", "statistics_identity", "session_id", "received_at", "receipt_time", "raw_json", "typed_snapshot", "diagnostics");
        RequireSchemaVersion(row);
        ValidateIdentity(Get(row, "identity"), expected);

        JsonElement statisticsIdentity = Get(row, "statistics_identity");
        RequireObject(statisticsIdentity, "scenario_id", "run_id", "role", "client_id");
        RequireEqualString(statisticsIdentity, "scenario_id", expected.ScenarioId);
        RequireEqualString(statisticsIdentity, "run_id", expected.RunId);
        RequireEqualString(statisticsIdentity, "client_id", expected.ClientId);
        RequireEnumNumber(Get(statisticsIdentity, "role"), (int)KafkaClientRole.LoadGenProducer, "statistics_identity.role");
        string sessionId = RequireNonemptyString(Get(row, "session_id"), "session_id");
        if (attribution.SessionId is not null && !StringComparer.Ordinal.Equals(sessionId, attribution.SessionId))
            throw Invalid("statistics_session_id_changed");
        attribution.SessionId ??= sessionId;
        RequireTimestamp(Get(row, "received_at"), "received_at");
        RequireTimestamp(Get(row, "receipt_time"), "receipt_time");
        RequireString(Get(row, "raw_json"), "raw_json"); // Deliberately opaque: the bridge owns native JSON parsing policy.

        JsonElement snapshot = Get(row, "typed_snapshot");
        if (snapshot.ValueKind != JsonValueKind.Null)
        {
            string nativeName = ValidateSnapshot(snapshot, expected.ClientId, maximumRows, attribution);
            if (attribution.NativeName is not null && !StringComparer.Ordinal.Equals(nativeName, attribution.NativeName))
                throw Invalid("statistics_native_name_changed");
            attribution.NativeName ??= nativeName;
        }
        else
        {
            attribution.HasUnparsedReports = true;
        }
        if (ValidateStatisticsDiagnostics(Get(row, "diagnostics"), maximumDiagnostics))
            attribution.HasReportDiagnostics = true;
    }

    private static string ValidateSnapshot(JsonElement snapshot, string expectedClientId, int maximumRows,
        StatisticsAttribution attribution)
    {
        RequireObject(snapshot, "name", "client_id", "type", "timestamp_microseconds", "time_seconds", "age_microseconds", "brokers");
        string nativeName = RequireNonemptyString(Get(snapshot, "name"), "typed_snapshot.name");
        RequireEqualString(snapshot, "client_id", expectedClientId);
        RequireEqualString(snapshot, "type", "producer");
        long timestamp = RequireNonnegativeInt64(Get(snapshot, "timestamp_microseconds"), "typed_snapshot.timestamp_microseconds");
        RequireNonnegativeInt64(Get(snapshot, "time_seconds"), "typed_snapshot.time_seconds");
        long age = RequireNonnegativeInt64(Get(snapshot, "age_microseconds"), "typed_snapshot.age_microseconds");
        bool generationValid = true;
        if (attribution.LastNativeTimestamp is long previousTimestamp && timestamp <= previousTimestamp)
        {
            attribution.HasNonIncreasingTimestamp = true;
            generationValid = false;
        }
        long creationTimestamp = timestamp - age;
        if (attribution.NativeCreationTimestamp is long knownCreationTimestamp && creationTimestamp != knownCreationTimestamp)
        {
            attribution.HasNativeCreationChanged = true;
            generationValid = false;
        }
        if (generationValid)
        {
            attribution.LastNativeTimestamp = timestamp;
            attribution.NativeCreationTimestamp ??= creationTimestamp;
        }
        JsonElement brokers = Get(snapshot, "brokers");
        RequireArray(brokers, "typed_snapshot.brokers", maximumRows);
        var brokerNamesInSnapshot = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement broker in brokers.EnumerateArray())
        {
            string brokerName = ValidateBroker(broker, attribution, generationValid);
            if (!brokerNamesInSnapshot.Add(brokerName)) attribution.HasDuplicateBrokerName = true;
            attribution.ObservedBrokerNames.Add(brokerName);
        }
        return nativeName;
    }

    private static string ValidateBroker(JsonElement broker, StatisticsAttribution attribution, bool generationValid)
    {
        RequireObject(broker, "name", "node_id", "state", "state_age_microseconds", "request_timeouts", "disconnects", "transmission_retries", "transmission_errors", "receive_errors", "throttle", "round_trip_time");
        string brokerName = RequireNonemptyString(Get(broker, "name"), "broker.name");
        int nodeId = RequireInt32(Get(broker, "node_id"), "broker.node_id");
        if (nodeId < -1) throw Invalid("broker.node_id_out_of_range");
        RequireString(Get(broker, "state"), "broker.state");
        RequireNonnegativeInt64(Get(broker, "state_age_microseconds"), "broker.state_age_microseconds");
        RecordCounter(brokerName, "req_timeouts", Get(broker, "request_timeouts"), "broker.request_timeouts", attribution, generationValid, isGuard: true);
        RecordCounter(brokerName, "disconnects", Get(broker, "disconnects"), "broker.disconnects", attribution, generationValid, isGuard: false);
        RecordCounter(brokerName, "txretries", Get(broker, "transmission_retries"), "broker.transmission_retries", attribution, generationValid, isGuard: true);
        RecordCounter(brokerName, "txerrs", Get(broker, "transmission_errors"), "broker.transmission_errors", attribution, generationValid, isGuard: true);
        RecordCounter(brokerName, "rxerrs", Get(broker, "receive_errors"), "broker.receive_errors", attribution, generationValid, isGuard: true);
        ValidateWindow(Get(broker, "throttle"), "broker.throttle");
        ValidateWindow(Get(broker, "round_trip_time"), "broker.round_trip_time");
        return brokerName;
    }

    private static void ValidateWindow(JsonElement window, string field)
    {
        RequireObject(window, "count", "value");
        long count = RequireNonnegativeInt64(Get(window, "count"), field + ".count");
        JsonElement value = Get(window, "value");
        if (count == 0)
        {
            if (value.ValueKind != JsonValueKind.Null) throw Invalid(field + ".value_must_be_absent_for_empty_window");
        }
        else if (value.ValueKind == JsonValueKind.Null)
        {
            throw Invalid(field + ".value_required_for_populated_window");
        }
        else
        {
            RequireNonnegativeInt64(value, field + ".value");
        }
    }

    private static bool ValidateStatisticsDiagnostics(JsonElement diagnostics, int maximumDiagnostics)
    {
        RequireArray(diagnostics, "diagnostics", maximumDiagnostics);
        foreach (JsonElement diagnostic in diagnostics.EnumerateArray())
        {
            RequireObject(diagnostic, "code", "broker", "field");
            RequireNonemptyString(Get(diagnostic, "code"), "diagnostics.code");
            RequireNullableString(Get(diagnostic, "broker"), "diagnostics.broker");
            RequireNullableString(Get(diagnostic, "field"), "diagnostics.field");
        }
        return diagnostics.GetArrayLength() != 0;
    }

    private static void ValidateLifecycleRecord(JsonElement row, RunIdentity expected, int maximumDiagnostics,
        LifecycleAttribution attribution)
    {
        RequireObjectBase(row, "schema_version", "identity", "session_id", "receipt_sequence", "received_at", "event_type");
        RequireSchemaVersion(row);
        ValidateIdentity(Get(row, "identity"), expected);
        RequireNonemptyString(Get(row, "session_id"), "session_id");
        long receiptSequence = RequireNonnegativeInt64(Get(row, "receipt_sequence"), "receipt_sequence");
        if (receiptSequence <= 0) throw Invalid("receipt_sequence_must_be_positive");
        if (!attribution.ReceiptSequences.Add(receiptSequence)) throw Invalid("lifecycle_receipt_sequence_duplicate");
        RequireTimestamp(Get(row, "received_at"), "received_at");
        string eventType = RequireString(Get(row, "event_type"), "event_type");
        if (eventType == "native_log")
        {
            RequireExactProperties(row, "schema_version", "identity", "session_id", "receipt_sequence", "received_at", "source_event_timestamp", "correlation_id", "source_order", "known_missing_fields", "inference", "event_type", "native_name", "level", "facility", "message", "provenance");
            RequireEqualString(row, "session_id", "managed_log_callback");
            if (Get(row, "source_event_timestamp").ValueKind != JsonValueKind.Null ||
                Get(row, "correlation_id").ValueKind != JsonValueKind.Null ||
                Get(row, "source_order").ValueKind != JsonValueKind.Null)
                throw Invalid("native_log_known_missing_fields_have_values");
            ImmutableArray<string> missing = RequireStringArray(Get(row, "known_missing_fields"),
                "known_missing_fields", MaximumLifecycleFieldNames, allowEmpty: false);
            if (!missing.SequenceEqual(KnownMissingLifecycleFields, StringComparer.Ordinal))
                throw Invalid("native_log_known_missing_fields_mismatch");
            if (Get(row, "inference").ValueKind != JsonValueKind.Null)
                throw Invalid("native_log_inference_must_be_null");
            RequireString(Get(row, "native_name"), "native_name");
            string level = RequireNonemptyString(Get(row, "level"), "level");
            if (!Enum.TryParse<SyslogLevel>(level, ignoreCase: false, out SyslogLevel parsedLevel) || !Enum.IsDefined(parsedLevel))
                throw Invalid("log_level_invalid");
            RequireString(Get(row, "facility"), "facility");
            RequireString(Get(row, "message"), "message");
            RequireEqualString(row, "provenance", "managed_log_callback");
            return;
        }
        if (eventType == "native_error")
        {
            RequireExactProperties(row, "schema_version", "identity", "session_id", "receipt_sequence", "received_at", "event_type", "error_code", "error_name", "reason", "is_error", "is_fatal", "provenance");
            RequireEqualString(row, "session_id", "managed_error_callback");
            int errorCode = RequireInt32(Get(row, "error_code"), "error_code");
            if (!StringComparer.Ordinal.Equals(RequireNonemptyString(Get(row, "error_name"), "error_name"),
                    ((ErrorCode)errorCode).ToString()))
                throw Invalid("native_error_name_code_mismatch");
            RequireNullableString(Get(row, "reason"), "reason");
            bool isError = RequireBoolean(Get(row, "is_error"), "is_error");
            if (isError != (errorCode != (int)ErrorCode.NoError))
                throw Invalid("native_error_is_error_code_mismatch");
            if (isError) attribution.HasErrorCallback = true;
            RequireBoolean(Get(row, "is_fatal"), "is_fatal");
            RequireEqualString(row, "provenance", "managed_error_callback");
            return;
        }
        throw Invalid("lifecycle_event_type_unknown");
    }

    private static void ValidateCapabilityReport(JsonElement root, RunIdentity expected,
        RunEvidenceExpectations expectations, int statisticsRecordCount,
        int maximumRows, int maximumDiagnostics,
        HashSet<string> statisticsBrokerNames, bool hasStatisticsReportDiagnostics, bool hasUnparsedReports,
        HashSet<(string Broker, string Counter, long Value)> positiveGuardValues,
        bool hasNonIncreasingTimestamp, bool hasNativeCreationChanged, bool hasDecreasingCounter,
        bool hasDuplicateBrokerName,
        bool hasErrorCallback,
        out bool isFaulted, out bool requestCapabilitySupported, out bool? statisticsComplete,
        out ImmutableArray<string> statuses, out ImmutableArray<string> guardObservations,
        out bool guardObservationsComplete, out ImmutableArray<string> diagnostics)
    {
        RequireObject(root, "schema_version", "identity", "artifacts", "resolved_configuration", "request_lifecycle_requirements", "request_capability_supported", "observed_broker_names", "guard_observations", "guard_observations_complete", "statistics_completion", "session_diagnostics", "is_faulted");
        RequireSchemaVersion(root);
        ValidateIdentity(Get(root, "identity"), expected);
        JsonElement artifacts = Get(root, "artifacts");
        ImmutableArray<string> artifactNames = RequireStringArray(artifacts, "artifacts", 3, allowEmpty: false);
        if (artifactNames.Length != 3 || !artifactNames.ToHashSet(StringComparer.Ordinal).SetEquals(ArtifactNames))
            throw Invalid("artifact_name_set_mismatch");

        JsonElement resolved = Get(root, "resolved_configuration");
        if (resolved.ValueKind == JsonValueKind.Null) throw Invalid("resolved_configuration_required");
        ValidateResolvedConfiguration(resolved, expected, expectations, maximumRows);

        JsonElement requirements = Get(root, "request_lifecycle_requirements");
        RequireArray(requirements, "request_lifecycle_requirements", maximumRows);
        if (requirements.GetArrayLength() != RequestCapabilityCatalogue.Claims.Length)
            throw Invalid("capability_requirement_count_mismatch");
        var statusBuilder = ImmutableArray.CreateBuilder<string>(requirements.GetArrayLength());
        var requirementNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement requirement in requirements.EnumerateArray())
        {
            RequireObject(requirement, "requirement", "status", "pinned_version", "source_urls", "source_facts", "inference", "local_test_evidence");
            string name = RequireNonemptyString(Get(requirement, "requirement"), "capability.requirement");
            if (!RequestCapabilityCatalogue.TryGet(name, out RequestCapabilityClaim? claim) || !requirementNames.Add(name))
                throw Invalid("capability_requirement_unknown_or_duplicate");
            if (!RequestCapabilityCatalogue.Matches(requirement))
                throw Invalid("capability_claim_catalogue_mismatch");
            statusBuilder.Add(claim.Status);
        }
        statuses = statusBuilder.ToImmutable();
        requestCapabilitySupported = RequireBoolean(Get(root, "request_capability_supported"), "request_capability_supported");
        if (requestCapabilitySupported) throw Invalid("request_capability_supported_for_schema_v1");
        ImmutableArray<string> observedBrokerNames = RequireStringArray(Get(root, "observed_broker_names"),
            "observed_broker_names", maximumRows, allowEmpty: true);
        var observedBrokerNameSet = observedBrokerNames.ToHashSet(StringComparer.Ordinal);
        if (!statisticsBrokerNames.IsSubsetOf(observedBrokerNameSet))
            throw Invalid("observed_broker_names_missing_statistics_broker");
        guardObservations = RequireStringArray(Get(root, "guard_observations"), "guard_observations", maximumDiagnostics, allowEmpty: true);
        guardObservationsComplete = RequireBoolean(Get(root, "guard_observations_complete"), "guard_observations_complete");
        isFaulted = RequireBoolean(Get(root, "is_faulted"), "is_faulted");
        if (guardObservationsComplete)
        {
            ValidatePositiveGuardObservations(guardObservations, positiveGuardValues, hasErrorCallback, isFaulted);
        }
        JsonElement completion = Get(root, "statistics_completion");
        statisticsComplete = null;
        if (completion.ValueKind != JsonValueKind.Null)
        {
            RequireObject(completion, "is_complete", "diagnostics", "missing_brokers");
            statisticsComplete = RequireBoolean(Get(completion, "is_complete"), "statistics_completion.is_complete");
            if (statisticsComplete.Value && statisticsRecordCount == 0)
                throw Invalid("statistics_completion_without_records");
            JsonElement completionDiagnostics = Get(completion, "diagnostics");
            ValidateStatisticsDiagnostics(completionDiagnostics, maximumDiagnostics);
            ImmutableArray<string> missingBrokers = RequireStringArray(Get(completion, "missing_brokers"), "statistics_completion.missing_brokers", maximumRows, allowEmpty: true);
            if (statisticsComplete.Value &&
                (completionDiagnostics.GetArrayLength() != 0 || !missingBrokers.IsEmpty || hasStatisticsReportDiagnostics || hasUnparsedReports ||
                 hasNonIncreasingTimestamp || hasNativeCreationChanged || hasDecreasingCounter || hasDuplicateBrokerName))
                throw Invalid("statistics_completion_contradicts_diagnostics_or_missing_brokers");
            HashSet<string> expectedBrokerNames = expectations.ExpectedBrokerNames.ToHashSet(StringComparer.Ordinal);
            HashSet<string> missingBrokerSet = missingBrokers.ToHashSet(StringComparer.Ordinal);
            if (missingBrokerSet.Count != missingBrokers.Length || missingBrokerSet.Any(name => !expectedBrokerNames.Contains(name)))
                throw Invalid("statistics_missing_brokers_outside_expected_inventory");
            if (expectedBrokerNames.Except(statisticsBrokerNames, StringComparer.Ordinal).Any(name => !missingBrokerSet.Contains(name)))
                throw Invalid("statistics_expected_unobserved_broker_missing_from_completion");
            if (statisticsComplete.Value &&
                (expectedBrokerNames.Count == 0 || !expectedBrokerNames.IsSubsetOf(statisticsBrokerNames)))
                throw Invalid("statistics_complete_expected_broker_inventory_not_covered");
        }
        if (expectations.RequireStatisticsEvidence && statisticsComplete != true)
            throw Invalid("required_statistics_completion_missing_or_incomplete");
        diagnostics = RequireStringArray(Get(root, "session_diagnostics"), "session_diagnostics", maximumDiagnostics, allowEmpty: true);
        if (!isFaulted && !observedBrokerNameSet.SetEquals(statisticsBrokerNames))
            throw Invalid("observed_broker_names_statistics_mismatch");
        if (!guardObservationsComplete && !isFaulted) throw Invalid("guard_observation_truncation_not_faulted");
    }

    private static void RecordCounter(string broker, string counter, JsonElement value, string field,
        StatisticsAttribution attribution, bool generationValid, bool isGuard)
    {
        long observed = RequireNonnegativeInt64(value, field);
        if (isGuard && observed > 0) attribution.PositiveGuardValues.Add((broker, counter, observed));
        if (!generationValid) return;
        var key = (broker, counter);
        if (attribution.LastCounterValues.TryGetValue(key, out long previous) && observed < previous)
            attribution.HasDecreasingCounter = true;
        attribution.LastCounterValues[key] = observed;
    }

    private static void ValidatePositiveGuardObservations(ImmutableArray<string> observations,
        HashSet<(string Broker, string Counter, long Value)> positiveGuardValues,
        bool hasErrorCallback, bool isFaulted)
    {
        var valuesByPair = new Dictionary<(string Broker, string Counter), HashSet<string>>();
        foreach ((string broker, string counter, long value) in positiveGuardValues)
        {
            var pair = (broker, counter);
            if (!valuesByPair.TryGetValue(pair, out HashSet<string>? candidates))
                valuesByPair.Add(pair, candidates = new HashSet<string>(StringComparer.Ordinal));
            candidates.Add(FormattableString.Invariant($"broker={broker};counter={counter};observed={value}"));
        }

        // A faulted writer may have recorded a guard from a report that was rejected before
        // persistence, including an earlier value for a pair that later appears in persisted rows.
        // Match its emitted prefix against candidates built from persisted broker/counter fields;
        // do not parse broker names out of the free-form guard string.
        if (isFaulted)
        {
            if (hasErrorCallback && !observations.Contains("native_error_callback_observed", StringComparer.Ordinal))
                throw Invalid("native_error_guard_observation_missing");
            foreach ((string broker, string counter) in valuesByPair.Keys)
            {
                string prefix = FormattableString.Invariant($"broker={broker};counter={counter};observed=");
                bool found = observations.Any(observation => observation.StartsWith(prefix, StringComparison.Ordinal) &&
                    long.TryParse(observation.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out long value) && value > 0);
                if (!found) throw Invalid("statistics_guard_observation_missing");
            }
            return;
        }

        foreach (KeyValuePair<(string Broker, string Counter), HashSet<string>> pair in valuesByPair)
        {
            bool found = observations.Any(pair.Value.Contains);
            if (!found) throw Invalid("statistics_guard_observation_missing");
        }

        var candidatePairs = new Dictionary<string, HashSet<(string Broker, string Counter)>>(StringComparer.Ordinal);
        foreach (KeyValuePair<(string Broker, string Counter), HashSet<string>> pair in valuesByPair)
        {
            foreach (string candidate in pair.Value)
            {
                if (!candidatePairs.TryGetValue(candidate, out HashSet<(string Broker, string Counter)>? matches))
                    candidatePairs.Add(candidate, matches = []);
                matches.Add(pair.Key);
            }
        }

        var matchedPairs = new HashSet<(string Broker, string Counter)>();
        bool errorGuardObserved = false;
        foreach (string observation in observations)
        {
            if (StringComparer.Ordinal.Equals(observation, "native_error_callback_observed"))
            {
                if (!hasErrorCallback || errorGuardObserved)
                    throw Invalid("native_error_guard_observation_orphan_or_duplicate");
                errorGuardObserved = true;
                continue;
            }

            if (!candidatePairs.TryGetValue(observation, out HashSet<(string Broker, string Counter)>? matches) || matches.Count != 1)
                throw Invalid("statistics_guard_observation_orphan_or_ambiguous");
            (string Broker, string Counter) matchedPair = matches.Single();
            if (!matchedPairs.Add(matchedPair)) throw Invalid("statistics_guard_observation_pair_duplicate");
        }
        if (hasErrorCallback != errorGuardObserved)
            throw Invalid("native_error_guard_observation_missing_or_orphan");
    }

    private static void ValidateResolvedConfiguration(JsonElement resolved, RunIdentity expected,
        RunEvidenceExpectations expectations, int maximumRows)
    {
        RequireObject(resolved, "role", "settings", "omissions");
        ResolvedRoleConfiguration selected = expectations.ResolvedConfiguration;
        RequireEnumNumber(Get(resolved, "role"), (int)selected.Role, "resolved_configuration.role");
        KafkaBaseline<ProducerConfig> expectedBaseline = CreateExpectedProducerBaseline(expected.ClientId);
        JsonElement settings = Get(resolved, "settings");
        RequireKind(settings, JsonValueKind.Object, "resolved_configuration.settings");
        if (settings.EnumerateObject().Count() > maximumRows) throw Invalid("resolved_settings_limit_exceeded");
        var baselineSettings = expectedBaseline.Settings.ToDictionary(metadata => metadata.Key, StringComparer.Ordinal);
        var seenSettings = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty setting in settings.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(setting.Name) || !seenSettings.Add(setting.Name))
                throw Invalid("resolved_setting_name_empty_or_duplicate");
            if (!baselineSettings.TryGetValue(setting.Name, out KafkaSettingMetadata? metadata) ||
                !selected.Settings.TryGetValue(setting.Name, out ResolvedSetting? selectedSetting))
                throw Invalid("resolved_setting_unknown");
            ValidateResolvedSetting(setting.Value, setting.Name, expected, metadata, selectedSetting);
        }
        if (!seenSettings.SetEquals(baselineSettings.Keys) || !seenSettings.SetEquals(selected.Settings.Keys))
            throw Invalid("resolved_setting_catalogue_coverage_mismatch");
        JsonElement omissions = Get(resolved, "omissions");
        RequireArray(omissions, "resolved_configuration.omissions", maximumRows);
        var baselineOmissions = expectedBaseline.Omissions.ToDictionary(metadata => metadata.Key, StringComparer.Ordinal);
        var selectedOmissions = selected.Omissions.ToDictionary(metadata => metadata.Key, StringComparer.Ordinal);
        var seenOmissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement omission in omissions.EnumerateArray())
        {
            RequireKind(omission, JsonValueKind.Object, "resolved_configuration.omissions");
            string key = RequireNonemptyString(Get(omission, "key"), "setting_metadata.key");
            if (!seenOmissions.Add(key) || !baselineOmissions.TryGetValue(key, out KafkaSettingMetadata? metadata) ||
                !selectedOmissions.TryGetValue(key, out KafkaSettingMetadata? selectedMetadata))
                throw Invalid("resolved_omission_unknown_or_duplicate");
            ValidateSettingMetadata(omission, metadata, expected.ClientId);
            if (!MetadataMatchesSelectedSetting(metadata, selectedMetadata))
                throw Invalid("resolved_omission_selected_context_mismatch");
            ValidatePersistedMetadataValue(omission, selectedMetadata);
        }
        if (!seenOmissions.SetEquals(baselineOmissions.Keys) || !seenOmissions.SetEquals(selectedOmissions.Keys))
            throw Invalid("resolved_omission_catalogue_coverage_mismatch");
    }

    private static void ValidateResolvedSetting(JsonElement setting, string dictionaryKey, RunIdentity expected,
        KafkaSettingMetadata expectedMetadata, ResolvedSetting selectedSetting)
    {
        RequireObject(setting, "value", "baseline", "override");
        RequireNonemptyString(Get(setting, "value"), "resolved_setting.value");
        JsonElement baseline = Get(setting, "baseline");
        ValidateSettingMetadata(baseline, expectedMetadata, expected.ClientId);
        if (!StringComparer.Ordinal.Equals(RequireNonemptyString(Get(baseline, "key"), "baseline.key"), dictionaryKey))
            throw Invalid("resolved_setting_key_mismatch");
        if (!MetadataMatchesSelectedSetting(expectedMetadata, selectedSetting.Baseline))
            throw Invalid("resolved_setting_selected_baseline_mismatch");
        ValidatePersistedMetadataValue(baseline, selectedSetting.Baseline);
        string effectiveValue = RequireNonemptyString(Get(setting, "value"), "resolved_setting.value");
        if (!StringComparer.Ordinal.Equals(effectiveValue, selectedSetting.Value))
            throw Invalid("resolved_setting_selected_value_mismatch");
        JsonElement origin = Get(setting, "override");
        if (selectedSetting.Override is null)
        {
            if (origin.ValueKind != JsonValueKind.Null)
                throw Invalid("resolved_setting_unselected_override");
        }
        else
        {
            if (origin.ValueKind == JsonValueKind.Null)
                throw Invalid("resolved_setting_selected_override_missing");
            RequireObject(origin, "scenario_id", "configuration_id", "location");
            RequireEqualString(origin, "scenario_id", selectedSetting.Override.ScenarioId);
            RequireEqualString(origin, "configuration_id", selectedSetting.Override.ConfigurationId);
            ValidateSourceLocationMatches(Get(origin, "location"), selectedSetting.Override.Location);
            RequireEqualString(origin, "scenario_id", expected.ScenarioId);
            RequireEqualString(origin, "configuration_id", expected.ConfigurationId);
        }
        if (selectedSetting.Override is null && !StringComparer.Ordinal.Equals(effectiveValue,
                RequireNonemptyString(Get(baseline, "value"), "baseline.value")))
        {
            throw Invalid("resolved_setting_value_differs_from_unoverridden_baseline");
        }
    }

    private static bool MetadataMatchesSelectedSetting(KafkaSettingMetadata expected, KafkaSettingMetadata actual) =>
        StringComparer.Ordinal.Equals(expected.Key, actual.Key) &&
        (StringComparer.Ordinal.Equals(expected.Key, "bootstrap.servers") ||
            StringComparer.Ordinal.Equals(expected.Value, actual.Value)) &&
        expected.Role == actual.Role && expected.Disposition == actual.Disposition &&
        expected.InventoryIds.SequenceEqual(actual.InventoryIds, StringComparer.Ordinal) &&
        expected.Sources.Length == actual.Sources.Length && expected.Sources.Zip(actual.Sources)
            .All(pair => StringComparer.Ordinal.Equals(pair.First.Url.AbsoluteUri, pair.Second.Url.AbsoluteUri) &&
                StringComparer.Ordinal.Equals(pair.First.VersionOrDate, pair.Second.VersionOrDate)) &&
        StringComparer.Ordinal.Equals(expected.SelectionReason, actual.SelectionReason);

    private static void ValidatePersistedMetadataValue(JsonElement metadata, KafkaSettingMetadata selected)
    {
        JsonElement value = Get(metadata, "value");
        string? actual = value.ValueKind == JsonValueKind.Null ? null : RequireString(value, "setting_metadata.value");
        if (!StringComparer.Ordinal.Equals(actual, selected.Value))
            throw Invalid("setting_metadata_selected_value_mismatch");
    }

    private static void ValidateSourceLocationMatches(JsonElement location, SourceLocation expected)
    {
        ValidateSourceLocation(location);
        if (!StringComparer.Ordinal.Equals(RequireNonemptyString(Get(location, "path"), "source_location.path"), expected.Path) ||
            !EqualsNullablePositiveInt32(Get(location, "line"), expected.Line, "source_location.line") ||
            !EqualsNullablePositiveInt32(Get(location, "column"), expected.Column, "source_location.column"))
            throw Invalid("resolved_setting_source_location_mismatch");
    }

    private static bool EqualsNullablePositiveInt32(JsonElement element, int? expected, string field)
    {
        RequireNullablePositiveInt32(element, field);
        int? actual = element.ValueKind == JsonValueKind.Null ? null : element.GetInt32();
        return actual == expected;
    }

    private static void ValidateSettingMetadata(JsonElement metadata, KafkaSettingMetadata expected,
        string expectedClientId)
    {
        RequireObject(metadata, "key", "value", "role", "disposition", "inventory_ids", "sources", "selection_reason");
        if (!StringComparer.Ordinal.Equals(RequireNonemptyString(Get(metadata, "key"), "setting_metadata.key"), expected.Key))
            throw Invalid("setting_metadata_catalogue_key_mismatch");
        JsonElement valueElement = Get(metadata, "value");
        string? value = valueElement.ValueKind == JsonValueKind.Null ? null : RequireString(valueElement, "setting_metadata.value");
        if (StringComparer.Ordinal.Equals(expected.Key, "client.id"))
        {
            if (!StringComparer.Ordinal.Equals(value, expectedClientId))
                throw Invalid("setting_metadata_client_id_mismatch");
        }
        else if (StringComparer.Ordinal.Equals(expected.Key, "bootstrap.servers"))
        {
            const string suffix = ".servicebus.windows.net:9093";
            if (value is null || !value.EndsWith(suffix, StringComparison.Ordinal) ||
                !IsNamespaceLabel(value[..^suffix.Length]))
                throw Invalid("setting_metadata_bootstrap_value_invalid");
        }
        else if (!StringComparer.Ordinal.Equals(value, expected.Value))
        {
            throw Invalid("setting_metadata_catalogue_value_mismatch");
        }
        RequireEnumNumber(Get(metadata, "role"), (int)expected.Role, "setting_metadata.role");
        int disposition = RequireInt32(Get(metadata, "disposition"), "setting_metadata.disposition");
        if (disposition != (int)expected.Disposition) throw Invalid("setting_metadata_catalogue_disposition_mismatch");
        ImmutableArray<string> ids = RequireStringArray(Get(metadata, "inventory_ids"), "setting_metadata.inventory_ids", 100, allowEmpty: false);
        if (ids.Any(string.IsNullOrWhiteSpace)) throw Invalid("setting_metadata.inventory_id_empty");
        if (!ids.SequenceEqual(expected.InventoryIds, StringComparer.Ordinal))
            throw Invalid("setting_metadata_catalogue_inventory_mismatch");
        JsonElement sources = Get(metadata, "sources");
        RequireArray(sources, "setting_metadata.sources", 100);
        foreach (JsonElement source in sources.EnumerateArray())
        {
            RequireObject(source, "url", "version_or_date");
            string urlText = RequireNonemptyString(Get(source, "url"), "setting_source.url");
            if (!Uri.TryCreate(urlText, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw Invalid("setting_source_url_invalid");
            RequireNonemptyString(Get(source, "version_or_date"), "setting_source.version_or_date");
        }
        var actualSources = sources.EnumerateArray().Select(source => (
            Url: RequireNonemptyString(Get(source, "url"), "setting_source.url"),
            Version: RequireNonemptyString(Get(source, "version_or_date"), "setting_source.version_or_date"))).ToImmutableArray();
        if (actualSources.Length != expected.Sources.Length || actualSources.Where((source, index) =>
                !StringComparer.Ordinal.Equals(source.Url, expected.Sources[index].Url.AbsoluteUri) ||
                !StringComparer.Ordinal.Equals(source.Version, expected.Sources[index].VersionOrDate)).Any())
            throw Invalid("setting_metadata_catalogue_sources_mismatch");
        if (!StringComparer.Ordinal.Equals(RequireNonemptyString(Get(metadata, "selection_reason"), "setting_metadata.selection_reason"),
                expected.SelectionReason))
            throw Invalid("setting_metadata_catalogue_selection_reason_mismatch");
    }

    private static KafkaBaseline<ProducerConfig> CreateExpectedProducerBaseline(string clientId)
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "reader-validation";
            options.ClientId = clientId;
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<KafkaConfigurationFactory>().CreateLoadGenProducer();
    }

    private static bool IsNamespaceLabel(string value) => !string.IsNullOrEmpty(value) && value.Length <= 63 &&
        char.IsAsciiLetterOrDigit(value[0]) && char.IsAsciiLetterOrDigit(value[^1]) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    private static void ValidateSourceLocation(JsonElement location)
    {
        RequireObject(location, "path", "line", "column");
        RequireNonemptyString(Get(location, "path"), "source_location.path");
        RequireNullablePositiveInt32(Get(location, "line"), "source_location.line");
        RequireNullablePositiveInt32(Get(location, "column"), "source_location.column");
    }

    private static void ValidateIdentity(JsonElement identity, RunIdentity expected)
    {
        RequireObject(identity, IdentityFields);
        RequireEqualString(identity, "scenario_id", expected.ScenarioId);
        RequireEqualString(identity, "configuration_id", expected.ConfigurationId);
        RequireEqualString(identity, "run_id", expected.RunId);
        RequireEqualString(identity, "instance_id", expected.InstanceId);
        RequireEqualString(identity, "expected_instance_id", expected.ExpectedInstanceId);
        RequireEqualString(identity, "client_id", expected.ClientId);
    }

    private static void RequireSchemaVersion(JsonElement element)
    {
        if (RequireInt32(Get(element, "schema_version"), "schema_version") != SchemaVersion)
            throw Invalid("evidence_schema_version_unsupported");
    }

    private static void RejectDuplicatePropertiesRecursively(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Invalid("evidence_duplicate_property");
                RejectDuplicatePropertiesRecursively(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement value in element.EnumerateArray()) RejectDuplicatePropertiesRecursively(value);
        }
    }

    private static void RequireObject(JsonElement element, params string[] requiredProperties)
        => RequireExactProperties(element, requiredProperties);

    private static void RequireObjectBase(JsonElement element, params string[] requiredProperties)
    {
        RequireKind(element, JsonValueKind.Object, "object");
        var required = requiredProperties.ToHashSet(StringComparer.Ordinal);
        foreach (string name in requiredProperties)
            if (!element.TryGetProperty(name, out _)) throw Invalid("evidence_required_property_missing:" + name);
        foreach (JsonProperty property in element.EnumerateObject())
            if (!required.Contains(property.Name) && !IsLifecycleBranchField(property.Name))
                throw Invalid("evidence_property_unknown:" + property.Name);
    }

    private static bool IsLifecycleBranchField(string name) => name is "source_event_timestamp" or "correlation_id" or "source_order" or "known_missing_fields" or "inference" or "native_name" or "level" or "facility" or "message" or "provenance" or "error_code" or "error_name" or "reason" or "is_error" or "is_fatal";

    private static void RequireExactProperties(JsonElement element, params string[] requiredProperties)
    {
        RequireKind(element, JsonValueKind.Object, "object");
        var required = requiredProperties.ToHashSet(StringComparer.Ordinal);
        foreach (string name in requiredProperties)
            if (!element.TryGetProperty(name, out _)) throw Invalid("evidence_required_property_missing:" + name);
        foreach (JsonProperty property in element.EnumerateObject())
            if (!required.Contains(property.Name)) throw Invalid("evidence_property_unknown:" + property.Name);
    }

    private static JsonElement Get(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value)) throw Invalid("evidence_required_property_missing:" + propertyName);
        return value;
    }

    private static void RequireKind(JsonElement element, JsonValueKind kind, string field)
    {
        if (element.ValueKind != kind) throw Invalid("evidence_type_invalid:" + field);
    }

    private static void RequireArray(JsonElement element, string field, int maximumItems)
    {
        RequireKind(element, JsonValueKind.Array, field);
        if (element.GetArrayLength() > maximumItems) throw Invalid("evidence_array_limit_exceeded:" + field);
    }

    private static ImmutableArray<string> RequireStringArray(JsonElement element, string field, int maximumItems, bool allowEmpty)
    {
        RequireArray(element, field, maximumItems);
        var values = ImmutableArray.CreateBuilder<string>(element.GetArrayLength());
        foreach (JsonElement value in element.EnumerateArray())
        {
            string text = RequireString(value, field);
            if (!allowEmpty && string.IsNullOrWhiteSpace(text)) throw Invalid("evidence_string_array_empty_value:" + field);
            values.Add(text);
        }
        return values.ToImmutable();
    }

    private static string RequireString(JsonElement element, string field)
    {
        RequireKind(element, JsonValueKind.String, field);
        return element.GetString() ?? throw Invalid("evidence_string_null:" + field);
    }

    private static string RequireNonemptyString(JsonElement element, string field)
    {
        string value = RequireString(element, field);
        if (string.IsNullOrWhiteSpace(value)) throw Invalid("evidence_string_empty:" + field);
        return value;
    }

    private static void RequireNullableString(JsonElement element, string field)
    {
        if (element.ValueKind != JsonValueKind.Null) _ = RequireString(element, field);
    }

    private static bool RequireBoolean(JsonElement element, string field)
    {
        RequireKind(element, JsonValueKind.True, JsonValueKind.False, field);
        return element.GetBoolean();
    }

    private static int RequireInt32(JsonElement element, string field)
    {
        RequireKind(element, JsonValueKind.Number, field);
        if (!element.TryGetInt32(out int value)) throw Invalid("evidence_int32_invalid:" + field);
        return value;
    }

    private static long RequireNonnegativeInt64(JsonElement element, string field)
    {
        RequireKind(element, JsonValueKind.Number, field);
        if (!element.TryGetInt64(out long value) || value < 0) throw Invalid("evidence_nonnegative_int64_invalid:" + field);
        return value;
    }

    private static void RequireNullablePositiveInt32(JsonElement element, string field)
    {
        if (element.ValueKind == JsonValueKind.Null) return;
        if (RequireInt32(element, field) <= 0) throw Invalid("evidence_positive_int32_invalid:" + field);
    }

    private static void RequireEnumNumber(JsonElement element, int expected, string field)
    {
        if (RequireInt32(element, field) != expected) throw Invalid("evidence_enum_mismatch:" + field);
    }

    private static void RequireTimestamp(JsonElement element, string field)
    {
        string value = RequireString(element, field);
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
            throw Invalid("evidence_timestamp_invalid:" + field);
    }

    private static void RequireEqualString(JsonElement element, string propertyName, string expected)
    {
        string actual = RequireNonemptyString(Get(element, propertyName), propertyName);
        if (!StringComparer.Ordinal.Equals(actual, expected)) throw Invalid("evidence_identity_mismatch:" + propertyName);
    }

    private static void RequireKind(JsonElement element, JsonValueKind first, JsonValueKind second, string field)
    {
        if (element.ValueKind != first && element.ValueKind != second) throw Invalid("evidence_type_invalid:" + field);
    }

    private sealed class StatisticsAttribution
    {
        public string? SessionId { get; set; }

        public string? NativeName { get; set; }

        public HashSet<(string Broker, string Counter, long Value)> PositiveGuardValues { get; } = [];

        public HashSet<string> ObservedBrokerNames { get; } = new(StringComparer.Ordinal);

        public bool HasReportDiagnostics { get; set; }

        public bool HasUnparsedReports { get; set; }

        public long? LastNativeTimestamp { get; set; }

        public long? NativeCreationTimestamp { get; set; }

        public Dictionary<(string Broker, string Counter), long> LastCounterValues { get; } = [];

        public bool HasNonIncreasingTimestamp { get; set; }

        public bool HasNativeCreationChanged { get; set; }

        public bool HasDecreasingCounter { get; set; }

        public bool HasDuplicateBrokerName { get; set; }
    }

    private sealed class LifecycleAttribution
    {
        public HashSet<long> ReceiptSequences { get; } = [];

        public bool HasErrorCallback { get; set; }
    }

    private static InvalidDataException Invalid(string message) => new(message);
}
