using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.VerifyRun.Input;

/// <summary>Reads captured envelopes while keeping their later evidence checks out of the parser.</summary>
internal static class CapturedEvidenceReader
{
    private const int EnvelopeCheck = 17;

    /// <summary>Reads a statistics envelope, leaving raw native JSON for NativeStatisticsReader.</summary>
    public static ParseResult<IReadOnlyList<StatisticsEnvelope>> ReadStatistics(ReadOnlyMemory<byte>? source, string artifact, InputLimits limits, InputBudget budget)
    {
        if (source is null)
            return ParseResult<IReadOnlyList<StatisticsEnvelope>>.Failure(new StructuralProblem(EnvelopeCheck, "missing_input", artifact, null, null, null));
        ReadOnlyMemory<byte> utf8 = source.Value;
        budget.ConsumeBytes(utf8.Length);
        ParseResult<IReadOnlyList<ReadOnlyMemory<byte>>> lines = StrictJson.ReadJsonLines(utf8, artifact, limits, EnvelopeCheck);
        if (!lines.IsSuccess) return ParseResult<IReadOnlyList<StatisticsEnvelope>>.Failure(lines.Problems);
        budget.ConsumeEntries(lines.Value!.Count);
        var reports = new List<StatisticsEnvelope>(lines.Value.Count);
        var problems = new List<StructuralProblem>();
        for (int index = 0; index < lines.Value.Count; index++)
        {
            ParseResult<JsonDocument> document = StrictJson.ParseDocument(
                lines.Value[index], artifact, limits with { MaxArtifactBytes = limits.MaxRowBytes }, EnvelopeCheck, index + 1L);
            if (!document.IsSuccess) { problems.AddRange(document.Problems); continue; }
            using JsonDocument parsed = document.Value!;
            var parser = new EvidenceParser(artifact, EnvelopeCheck, index + 1L, budget);
            StatisticsEnvelope? item = parser.Statistics(parsed.RootElement);
            if (parser.Problems.Count != 0) problems.AddRange(parser.Problems);
            else if (item is not null) reports.Add(item);
        }
        return problems.Count == 0
            ? ParseResult<IReadOnlyList<StatisticsEnvelope>>.Success(reports)
            : ParseResult<IReadOnlyList<StatisticsEnvelope>>.Failure(problems);
    }

    /// <summary>Reads request-lifecycle JSONL rows and retains their event order and duplicates.</summary>
    public static ParseResult<IReadOnlyList<LifecycleEvent>> ReadLifecycle(ReadOnlyMemory<byte>? source, string artifact, InputLimits limits, InputBudget budget)
    {
        if (source is null)
            return ParseResult<IReadOnlyList<LifecycleEvent>>.Failure(new StructuralProblem(EnvelopeCheck, "missing_input", artifact, null, null, null));
        ReadOnlyMemory<byte> utf8 = source.Value;
        budget.ConsumeBytes(utf8.Length);
        ParseResult<IReadOnlyList<ReadOnlyMemory<byte>>> lines = StrictJson.ReadJsonLines(utf8, artifact, limits, EnvelopeCheck);
        if (!lines.IsSuccess) return ParseResult<IReadOnlyList<LifecycleEvent>>.Failure(lines.Problems);
        budget.ConsumeEntries(lines.Value!.Count);
        var events = new List<LifecycleEvent>(lines.Value!.Count);
        var problems = new List<StructuralProblem>();
        for (int index = 0; index < lines.Value.Count; index++)
        {
            ParseResult<JsonDocument> document = StrictJson.ParseDocument(
                lines.Value[index], artifact, limits with { MaxArtifactBytes = limits.MaxRowBytes }, EnvelopeCheck, index + 1L);
            if (!document.IsSuccess) { problems.AddRange(document.Problems); continue; }
            using JsonDocument parsed = document.Value!;
            var parser = new EvidenceParser(artifact, EnvelopeCheck, index + 1L, budget);
            LifecycleEvent? item = parser.Lifecycle(parsed.RootElement);
            if (parser.Problems.Count != 0) problems.AddRange(parser.Problems);
            else if (item is not null) events.Add(item);
        }

        return problems.Count == 0
            ? ParseResult<IReadOnlyList<LifecycleEvent>>.Success(events)
            : ParseResult<IReadOnlyList<LifecycleEvent>>.Failure(problems);
    }

    /// <summary>Reads the capabilities envelope, preserving completion and boundary presence states.</summary>
    public static ParseResult<CapturedCapabilities> ReadCapabilities(ReadOnlyMemory<byte>? source, string artifact, InputLimits limits, InputBudget budget) =>
        source is null
            ? ParseResult<CapturedCapabilities>.Failure(new StructuralProblem(EnvelopeCheck, "missing_input", artifact, null, null, null))
            : ReadEnvelope(source.Value, artifact, limits, budget, (root, parser) => parser.Capabilities(root));

    private static ParseResult<T> ReadEnvelope<T>(ReadOnlyMemory<byte> utf8, string artifact, InputLimits limits, InputBudget budget,
        Func<JsonElement, EvidenceParser, T?> read)
    {
        budget.ConsumeBytes(utf8.Length);
        ParseResult<JsonDocument> parsed = StrictJson.ParseDocument(utf8, artifact, limits, EnvelopeCheck);
        if (!parsed.IsSuccess) return ParseResult<T>.Failure(parsed.Problems);
        using JsonDocument document = parsed.Value!;
        var parser = new EvidenceParser(artifact, EnvelopeCheck, null, budget);
        T? value = read(document.RootElement, parser);
        return parser.Problems.Count == 0 && value is not null
            ? ParseResult<T>.Success(value)
            : ParseResult<T>.Failure(parser.Problems);
    }

    private static HashSet<string> Fields(params string[] names) => new(names, StringComparer.Ordinal);

    private sealed class EvidenceParser(string artifact, int checkNumber, long? row, InputBudget budget)
    {
        public List<StructuralProblem> Problems { get; } = [];

        public StatisticsEnvelope? Statistics(JsonElement root)
        {
        Validate(root, "$", "schema_version", "identity", "statistics_identity", "session_id", "received_at",
                "receipt_time", "raw_json", "typed_snapshot", "diagnostics");
            int? schema = Int32(root, "schema_version", "schema_version");
            Schema(schema);
            RunIdentity? identity = Identity(Object(root, "identity", "identity"), "identity");
            StatisticsIdentity? statisticsIdentity = ReadStatisticsIdentity(Object(root, "statistics_identity", "statistics_identity"));
            string? session = String(root, "session_id", "session_id");
            string? received = String(root, "received_at", "received_at");
            string? receipt = String(root, "receipt_time", "receipt_time");
            string? raw = String(root, "raw_json", "raw_json");
            ParsedField<TypedSnapshot> snapshot = OptionalObject(root, "typed_snapshot", "typed_snapshot", ReadSnapshot);
            List<CapturedDiagnostic>? diagnostics = ReadArray(root, "diagnostics", "diagnostics", ReadDiagnostic);
            return Problems.Count != 0 || schema is null || identity is null || statisticsIdentity is null || session is null ||
                received is null || receipt is null || raw is null || diagnostics is null
                ? null
                : new StatisticsEnvelope(schema.Value, identity, statisticsIdentity, session, received, receipt, raw, snapshot, diagnostics);
        }

        public LifecycleEvent? Lifecycle(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object) { Add("expected_object", "$" ); return null; }
            string? eventType = String(root, "event_type", "event_type");
            if (eventType is null) return null;
            if (eventType == "native_log") return NativeLogValue(root);
            if (eventType == "native_error") return NativeErrorValue(root);
            Add("unknown_discriminator", "event_type");
            return null;
        }

        private NativeLog? NativeLogValue(JsonElement root)
        {
            Validate(root, "$", "schema_version", "identity", "session_id", "receipt_sequence", "received_at",
                "source_event_timestamp", "correlation_id", "source_order", "known_missing_fields", "inference",
                "event_type", "native_name", "level", "facility", "message", "provenance");
            int? schema = Int32(root, "schema_version", "schema_version"); Schema(schema);
            RunIdentity? identity = Identity(Object(root, "identity", "identity"), "identity");
            string? session = String(root, "session_id", "session_id");
            long? sequence = Int64(root, "receipt_sequence", "receipt_sequence");
            string? received = String(root, "received_at", "received_at");
            ParsedField<string> timestamp = NullableString(root, "source_event_timestamp", "source_event_timestamp");
            ParsedField<string> correlation = NullableString(root, "correlation_id", "correlation_id");
            ParsedField<long> sourceOrder = NullableInt64(root, "source_order", "source_order");
            List<string>? missing = StringArray(root, "known_missing_fields", "known_missing_fields");
            ParsedField<string> inference = NullableString(root, "inference", "inference");
            string? name = String(root, "native_name", "native_name");
            string? level = String(root, "level", "level");
            string? facility = String(root, "facility", "facility");
            string? message = String(root, "message", "message");
            string? provenance = String(root, "provenance", "provenance");
            if (Problems.Count != 0 || schema is null || identity is null || session is null || sequence is null || received is null ||
                missing is null || name is null || level is null || facility is null || message is null || provenance is null) return null;
            return new NativeLog(schema.Value, identity, session, sequence.Value, received, timestamp, correlation, sourceOrder,
                missing, inference, name, level, facility, message, provenance);
        }

        private NativeError? NativeErrorValue(JsonElement root)
        {
            Validate(root, "$", "schema_version", "identity", "session_id", "receipt_sequence", "received_at", "event_type",
                "error_code", "error_name", "reason", "is_error", "is_fatal", "provenance");
            int? schema = Int32(root, "schema_version", "schema_version"); Schema(schema);
            RunIdentity? identity = Identity(Object(root, "identity", "identity"), "identity");
            string? session = String(root, "session_id", "session_id");
            long? sequence = Int64(root, "receipt_sequence", "receipt_sequence");
            string? received = String(root, "received_at", "received_at");
            int? errorCode = Int32(root, "error_code", "error_code");
            string? errorName = String(root, "error_name", "error_name");
            ParsedField<string> reason = NullableString(root, "reason", "reason");
            bool? isError = Boolean(root, "is_error", "is_error");
            bool? isFatal = Boolean(root, "is_fatal", "is_fatal");
            string? provenance = String(root, "provenance", "provenance");
            if (Problems.Count != 0 || schema is null || identity is null || session is null || sequence is null || received is null ||
                errorCode is null || errorName is null || isError is null || isFatal is null || provenance is null) return null;
            return new NativeError(schema.Value, identity, session, sequence.Value, received, errorCode.Value, errorName,
                reason, isError.Value, isFatal.Value, provenance);
        }

        public CapturedCapabilities? Capabilities(JsonElement root)
        {
            ValidateRequiredAndAllowed(root, "$",
                ["schema_version", "identity", "artifacts", "resolved_configuration", "request_lifecycle_requirements",
                    "request_capability_supported", "observed_broker_names", "guard_observations", "guard_observations_complete",
                    "session_diagnostics", "is_faulted"],
                ["schema_version", "identity", "artifacts", "resolved_configuration", "request_lifecycle_requirements",
                    "request_capability_supported", "observed_broker_names", "guard_observations", "guard_observations_complete",
                    "statistics_completion", "session_diagnostics", "is_faulted"]);
            int? schema = Int32(root, "schema_version", "schema_version"); Schema(schema);
            RunIdentity? identity = Identity(Object(root, "identity", "identity"), "identity");
            List<string>? artifacts = StringArray(root, "artifacts", "artifacts");
            ParsedField<ResolvedConfiguration> resolved = OptionalObject(root, "resolved_configuration", "resolved_configuration", ReadResolvedConfiguration);
            List<RequestLifecycleRequirement>? requirements = ReadArray(root, "request_lifecycle_requirements", "request_lifecycle_requirements", ReadRequirement);
            bool? supported = Boolean(root, "request_capability_supported", "request_capability_supported");
            List<string>? brokers = StringArray(root, "observed_broker_names", "observed_broker_names");
            List<string>? guards = StringArray(root, "guard_observations", "guard_observations");
            bool? guardsComplete = Boolean(root, "guard_observations_complete", "guard_observations_complete");
            ParsedField<Completion> completion = OptionalObject(root, "statistics_completion", "statistics_completion", ReadCompletion, allowMissing: true);
            List<string>? sessionDiagnostics = StringArray(root, "session_diagnostics", "session_diagnostics");
            bool? faulted = Boolean(root, "is_faulted", "is_faulted");
            return Problems.Count != 0 || schema is null || identity is null || artifacts is null || requirements is null ||
                supported is null || brokers is null || guards is null || guardsComplete is null || sessionDiagnostics is null || faulted is null
                ? null
                : new CapturedCapabilities(schema.Value, identity, artifacts, resolved, requirements, supported.Value, brokers,
                    guards, guardsComplete.Value, completion, sessionDiagnostics, faulted.Value);
        }

        private StatisticsIdentity? ReadStatisticsIdentity(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, "statistics_identity", "scenario_id", "run_id", "role", "client_id");
            string? scenario = String(value, "scenario_id", "statistics_identity.scenario_id");
            string? run = String(value, "run_id", "statistics_identity.run_id");
            int? role = Int32(value, "role", "statistics_identity.role");
            string? client = String(value, "client_id", "statistics_identity.client_id");
            return scenario is null || run is null || role is null || client is null ? null : new StatisticsIdentity(scenario, run, role.Value, client);
        }

        private TypedSnapshot? ReadSnapshot(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, "name", "client_id", "type", "timestamp_microseconds", "time_seconds", "age_microseconds", "brokers");
            string? name = String(value, "name", path + ".name");
            string? client = String(value, "client_id", path + ".client_id");
            string? type = String(value, "type", path + ".type");
            long? timestamp = Int64(value, "timestamp_microseconds", path + ".timestamp_microseconds");
            long? time = Int64(value, "time_seconds", path + ".time_seconds");
            long? age = Int64(value, "age_microseconds", path + ".age_microseconds");
            List<TypedBroker>? brokers = ReadArray(value, "brokers", path + ".brokers", ReadTypedBroker);
            return name is null || client is null || type is null || timestamp is null || time is null || age is null || brokers is null
                ? null : new TypedSnapshot(name, client, type, timestamp.Value, time.Value, age.Value, brokers);
        }

        private TypedBroker? ReadTypedBroker(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, "name", "node_id", "state", "state_age_microseconds", "request_timeouts", "disconnects",
                "transmission_retries", "transmission_errors", "receive_errors", "throttle", "round_trip_time");
            string? name = String(value, "name", path + ".name");
            int? node = Int32(value, "node_id", path + ".node_id");
            string? state = String(value, "state", path + ".state");
            long? age = Int64(value, "state_age_microseconds", path + ".state_age_microseconds");
            long? timeouts = Int64(value, "request_timeouts", path + ".request_timeouts");
            long? disconnects = Int64(value, "disconnects", path + ".disconnects");
            long? retries = Int64(value, "transmission_retries", path + ".transmission_retries");
            long? errors = Int64(value, "transmission_errors", path + ".transmission_errors");
            long? receive = Int64(value, "receive_errors", path + ".receive_errors");
            CountValue? throttle = ReadCountValue(Object(value, "throttle", path + ".throttle"), path + ".throttle");
            CountValue? rtt = ReadCountValue(Object(value, "round_trip_time", path + ".round_trip_time"), path + ".round_trip_time");
            return name is null || node is null || state is null || age is null || timeouts is null || disconnects is null ||
                retries is null || errors is null || receive is null || throttle is null || rtt is null
                ? null : new TypedBroker(name, node.Value, state, age.Value, timeouts.Value, disconnects.Value, retries.Value,
                    errors.Value, receive.Value, throttle, rtt);
        }

        private CountValue? ReadCountValue(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, "count", "value");
            long? count = Int64(value, "count", path + ".count");
            ParsedField<long> sample = NullableInt64(value, "value", path + ".value");
            return count is null ? null : new CountValue(count.Value, sample);
        }

        private CapturedDiagnostic? ReadDiagnostic(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, "code", "broker", "field");
            string? code = String(value, "code", path + ".code");
            ParsedField<string> broker = NullableString(value, "broker", path + ".broker");
            ParsedField<string> field = NullableString(value, "field", path + ".field");
            return code is null ? null : new CapturedDiagnostic(code, broker, field);
        }

        private ResolvedConfiguration? ReadResolvedConfiguration(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, "role", "settings", "omissions");
            int? role = Int32(value, "role", path + ".role");
            Dictionary<string, ResolvedSetting>? settings = ReadMap(value, "settings", path + ".settings", ReadResolvedSetting);
            List<SettingMetadata>? omissions = ReadArray(value, "omissions", path + ".omissions", ReadSettingMetadata);
            return role is null || settings is null || omissions is null ? null : new ResolvedConfiguration(role.Value, settings, omissions);
        }

        private ResolvedSetting? ReadResolvedSetting(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, "value", "baseline", "override");
            string? resolved = String(value, "value", path + ".value");
            SettingMetadata? baseline = ReadSettingMetadata(Object(value, "baseline", path + ".baseline"), path + ".baseline");
            ParsedField<Override> settingOverride = OptionalObject(value, "override", path + ".override", ReadOverride);
            return resolved is null || baseline is null ? null : new ResolvedSetting(resolved, baseline, settingOverride);
        }

        private SettingMetadata? ReadSettingMetadata(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, "key", "value", "role", "disposition", "inventory_ids", "sources", "selection_reason");
            string? key = String(value, "key", path + ".key");
            ParsedField<string> metadataValue = NullableString(value, "value", path + ".value");
            int? role = Int32(value, "role", path + ".role");
            int? disposition = Int32(value, "disposition", path + ".disposition");
            List<string>? ids = StringArray(value, "inventory_ids", path + ".inventory_ids");
            List<Source>? sources = ReadArray(value, "sources", path + ".sources", ReadSource);
            string? reason = String(value, "selection_reason", path + ".selection_reason");
            return key is null || role is null || disposition is null || ids is null || sources is null || reason is null
                ? null : new SettingMetadata(key, metadataValue, role.Value, disposition.Value, ids, sources, reason);
        }

        private Source? ReadSource(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, "url", "version_or_date");
            string? url = String(value, "url", path + ".url");
            string? version = String(value, "version_or_date", path + ".version_or_date");
            return url is null || version is null ? null : new Source(url, version);
        }

        private Override? ReadOverride(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, "scenario_id", "configuration_id", "location");
            string? scenario = String(value, "scenario_id", path + ".scenario_id");
            string? configuration = String(value, "configuration_id", path + ".configuration_id");
            Location? location = ReadLocation(Object(value, "location", path + ".location"), path + ".location");
            return scenario is null || configuration is null || location is null ? null : new Override(scenario, configuration, location);
        }

        private Location? ReadLocation(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, "path", "line", "column");
            string? locationPath = String(value, "path", path + ".path");
            ParsedField<int> line = NullableInt32(value, "line", path + ".line");
            ParsedField<int> column = NullableInt32(value, "column", path + ".column");
            return locationPath is null ? null : new Location(locationPath, line, column);
        }

        private RequestLifecycleRequirement? ReadRequirement(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, "requirement", "status", "pinned_version", "source_urls", "source_facts", "inference", "local_test_evidence");
            string? requirement = String(value, "requirement", path + ".requirement");
            string? status = String(value, "status", path + ".status");
            string? version = String(value, "pinned_version", path + ".pinned_version");
            List<string>? sources = StringArray(value, "source_urls", path + ".source_urls");
            string? facts = String(value, "source_facts", path + ".source_facts");
            string? inference = String(value, "inference", path + ".inference");
            string? evidence = String(value, "local_test_evidence", path + ".local_test_evidence");
            return requirement is null || status is null || version is null || sources is null || facts is null || inference is null || evidence is null
                ? null : new RequestLifecycleRequirement(requirement, status, version, sources, facts, inference, evidence);
        }

        private Completion? ReadCompletion(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            ValidateRequiredAndAllowed(value, path, ["is_complete", "diagnostics", "missing_brokers"],
                ["is_complete", "drain_boundary_microseconds", "first_post_drain_timestamp_microseconds", "diagnostics", "missing_brokers"]);
            bool? complete = Boolean(value, "is_complete", path + ".is_complete");
            ParsedField<long> boundary = NullableInt64(value, "drain_boundary_microseconds", path + ".drain_boundary_microseconds", allowMissing: true);
            ParsedField<long> firstPostDrain = NullableInt64(value, "first_post_drain_timestamp_microseconds", path + ".first_post_drain_timestamp_microseconds", allowMissing: true);
            List<CapturedDiagnostic>? diagnostics = ReadArray(value, "diagnostics", path + ".diagnostics", ReadDiagnostic);
            List<string>? brokers = StringArray(value, "missing_brokers", path + ".missing_brokers");
            return complete is null || diagnostics is null || brokers is null ? null : new Completion(complete.Value, boundary, firstPostDrain, diagnostics, brokers);
        }

        private List<T>? ReadArray<T>(JsonElement parent, string property, string path, Func<JsonElement, string, T?> read) where T : class
        {
            if (!parent.TryGetProperty(property, out JsonElement array)) { Add("missing_property", path); return null; }
            if (array.ValueKind != JsonValueKind.Array) { Add("wrong_json_kind", path); return null; }
            budget.ConsumeEntries(array.GetArrayLength());
            var result = new List<T>();
            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                string itemPath = $"{path}[{index++}]";
                if (item.ValueKind != JsonValueKind.Object) { Add("expected_object", itemPath); continue; }
                T? parsed = read(item, itemPath);
                if (parsed is not null) result.Add(parsed);
            }
            return result;
        }

        private Dictionary<string, T>? ReadMap<T>(JsonElement parent, string property, string path, Func<JsonElement, string, T?> read) where T : class
        {
            if (!parent.TryGetProperty(property, out JsonElement map)) { Add("missing_property", path); return null; }
            if (map.ValueKind != JsonValueKind.Object) { Add("wrong_json_kind", path); return null; }
            long mapEntries = 0;
            foreach (JsonProperty _ in map.EnumerateObject()) mapEntries++;
            budget.ConsumeEntries(mapEntries);
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (JsonProperty item in map.EnumerateObject())
            {
                if (item.Value.ValueKind != JsonValueKind.Object) { Add("expected_object", path + "." + item.Name); continue; }
                T? parsed = read(item.Value, path + "." + item.Name);
                if (parsed is not null) result.Add(item.Name, parsed);
            }
            return result;
        }

        private List<string>? StringArray(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement array)) { Add("missing_property", path); return null; }
            if (array.ValueKind != JsonValueKind.Array) { Add("wrong_json_kind", path); return null; }
            budget.ConsumeEntries(array.GetArrayLength());
            var result = new List<string>();
            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (!StrictJson.TryGetString(item, out string text)) Add("wrong_json_kind", $"{path}[{index}]");
                else result.Add(text);
                index++;
            }
            return result;
        }

        private ParsedField<T> OptionalObject<T>(JsonElement parent, string property, string path,
            Func<JsonElement, string, T?> read, bool allowMissing = false) where T : class
        {
            if (!parent.TryGetProperty(property, out JsonElement value))
            {
                if (allowMissing) return ParsedField<T>.Missing();
                Add("missing_property", path);
                return ParsedField<T>.Missing();
            }
            if (value.ValueKind == JsonValueKind.Null) return ParsedField<T>.Null();
            if (value.ValueKind != JsonValueKind.Object) { Add("wrong_json_kind", path); return ParsedField<T>.Missing(); }
            T? parsed = read(value, path);
            return parsed is null ? ParsedField<T>.Missing() : ParsedField<T>.FromValue(parsed);
        }

        private RunIdentity? Identity(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id");
            string? scenario = String(value, "scenario_id", path + ".scenario_id");
            string? configuration = String(value, "configuration_id", path + ".configuration_id");
            string? run = String(value, "run_id", path + ".run_id");
            string? instance = String(value, "instance_id", path + ".instance_id");
            string? expected = String(value, "expected_instance_id", path + ".expected_instance_id");
            string? client = String(value, "client_id", path + ".client_id");
            return scenario is null || configuration is null || run is null || instance is null || expected is null || client is null
                ? null : new RunIdentity(scenario, configuration, run, instance, expected, client);
        }

        private JsonElement Object(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) { Add("missing_property", path); return default; }
            if (value.ValueKind != JsonValueKind.Object) { Add("wrong_json_kind", path); return default; }
            return value;
        }

        private void ValidateRequiredAndAllowed(JsonElement value, string path, string[] requiredFields, string[] allowedFields) =>
            Problems.AddRange(StrictJson.ValidateProperties(value, path, Fields(requiredFields), Fields(allowedFields), artifact, checkNumber, row));

        private void Validate(JsonElement value, string path, params string[] fields)
        {
            IReadOnlySet<string> set = Fields(fields);
            Problems.AddRange(StrictJson.ValidateProperties(value, path, set, set, artifact, checkNumber, row));
        }

        private string? String(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) { Add("missing_property", path); return null; }
            if (!StrictJson.TryGetString(value, out string result)) { Add("wrong_json_kind", path); return null; }
            return result;
        }

        private int? Int32(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) { Add("missing_property", path); return null; }
            if (value.ValueKind != JsonValueKind.Number || !StrictJson.TryGetInt32(value, out int result)) { Add("wrong_json_kind", path); return null; }
            return result;
        }

        private long? Int64(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) { Add("missing_property", path); return null; }
            if (value.ValueKind != JsonValueKind.Number || !StrictJson.TryGetInt64(value, out long result)) { Add("wrong_json_kind", path); return null; }
            return result;
        }

        private bool? Boolean(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) { Add("missing_property", path); return null; }
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) { Add("wrong_json_kind", path); return null; }
            return value.GetBoolean();
        }

        private ParsedField<string> NullableString(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) { Add("missing_property", path); return ParsedField<string>.Missing(); }
            if (value.ValueKind == JsonValueKind.Null) return ParsedField<string>.Null();
            if (StrictJson.TryGetString(value, out string result)) return ParsedField<string>.FromValue(result);
            Add("wrong_json_kind", path); return ParsedField<string>.Missing();
        }

        private ParsedField<int> NullableInt32(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) { Add("missing_property", path); return ParsedField<int>.Missing(); }
            if (value.ValueKind == JsonValueKind.Null) return ParsedField<int>.Null();
            if (value.ValueKind == JsonValueKind.Number && StrictJson.TryGetInt32(value, out int result)) return ParsedField<int>.FromValue(result);
            Add("wrong_json_kind", path); return ParsedField<int>.Missing();
        }

        private ParsedField<long> NullableInt64(JsonElement parent, string property, string path, bool allowMissing = false)
        {
            if (!parent.TryGetProperty(property, out JsonElement value))
            {
                if (!allowMissing) Add("missing_property", path);
                return ParsedField<long>.Missing();
            }
            if (value.ValueKind == JsonValueKind.Null) return ParsedField<long>.Null();
            if (value.ValueKind == JsonValueKind.Number && StrictJson.TryGetInt64(value, out long result)) return ParsedField<long>.FromValue(result);
            Add("wrong_json_kind", path); return ParsedField<long>.Missing();
        }

        private void Schema(int? schema) { if (schema is not null && schema != 1) Add("unsupported_schema_version", "schema_version"); }
        private void Add(string code, string path) => Problems.Add(new StructuralProblem(checkNumber, code, artifact, row, path, null));
    }
}
