using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.VerifyRun.Input;

/// <summary>Reads ledger rows as independent structural evidence for check 1.</summary>
internal static class LedgerReader
{
    private const string Artifact = "ledger.jsonl";
    private static readonly IReadOnlySet<string> RowFields = Fields(
        "schema_version", "identity", "attempt", "completion_source", "delivered", "error_code",
        "error_name", "persistence_status", "persistence_provenance", "completed_microseconds",
        "delivery_latency_ms", "synchronous_call_duration_ms");
    private static readonly IReadOnlySet<string> AttemptFields = Fields(
        "instance_id", "attempt_id", "ordinal", "tenant_id", "document_id", "key", "sequence",
        "event_type", "planned_microseconds", "invoked_microseconds");
    private static readonly IReadOnlySet<string> CompletionSources = Fields("callback", "synchronous_rejection");
    private static readonly IReadOnlySet<string> PersistenceProvenances = Fields(
        "native_callback", "missing", "pre_enqueue");

    /// <summary>Reads LF-framed rows and preserves their original order and duplicate attempts.</summary>
    public static ParseResult<IReadOnlyList<LedgerRow>> ReadRows(ReadOnlyMemory<byte>? source, InputLimits limits, InputBudget budget)
    {
        if (source is null)
            return ParseResult<IReadOnlyList<LedgerRow>>.Failure(new StructuralProblem(1, "missing_input", Artifact, null, null, null));
        ReadOnlyMemory<byte> utf8 = source.Value;
        budget.ConsumeBytes(utf8.Length);
        ParseResult<IReadOnlyList<ReadOnlyMemory<byte>>> lines = StrictJson.ReadJsonLines(utf8, Artifact, limits, 1);
        if (!lines.IsSuccess)
        {
            return ParseResult<IReadOnlyList<LedgerRow>>.Failure(lines.Problems);
        }

        budget.ConsumeEntries(lines.Value!.Count);

        var rows = new List<LedgerRow>(lines.Value!.Count);
        var problems = new List<StructuralProblem>();
        for (int index = 0; index < lines.Value.Count; index++)
        {
            ParseResult<JsonDocument> parsed = StrictJson.ParseDocument(
                lines.Value[index], Artifact, limits with { MaxArtifactBytes = limits.MaxRowBytes }, 1, index + 1L);
            if (!parsed.IsSuccess)
            {
                problems.AddRange(parsed.Problems);
                continue;
            }

            using JsonDocument document = parsed.Value!;
            var reader = new RowParser(Artifact, index + 1L);
            LedgerRow? row = reader.Read(document.RootElement);
            if (reader.Problems.Count > 0)
            {
                problems.AddRange(reader.Problems);
            }
            else if (row is not null)
            {
                rows.Add(row);
            }
        }

        return problems.Count == 0
            ? ParseResult<IReadOnlyList<LedgerRow>>.Success(rows)
            : ParseResult<IReadOnlyList<LedgerRow>>.Failure(problems);
    }

    private static HashSet<string> Fields(params string[] names) => new(names, StringComparer.Ordinal);

    private sealed class RowParser(string artifact, long rowNumber)
    {
        public List<StructuralProblem> Problems { get; } = [];

        public LedgerRow? Read(JsonElement root)
        {
            Validate(root, "$", RowFields);
            int? schemaVersion = Int32(root, "schema_version", "schema_version");
            if (schemaVersion is not null && schemaVersion != 1)
            {
                Add("unsupported_schema_version", "schema_version");
            }

            JsonElement identityElement = Object(root, "identity", "identity");
            var identityReader = new NestedParser(artifact, rowNumber, Problems);
            RunIdentity? identity = identityElement.ValueKind == JsonValueKind.Object
                ? identityReader.ReadIdentity(identityElement, "identity")
                : null;
            JsonElement attemptElement = Object(root, "attempt", "attempt");
            AttemptEntry? attempt = attemptElement.ValueKind == JsonValueKind.Object ? ReadAttempt(attemptElement) : null;
            string? completionSource = EnumString(root, "completion_source", "completion_source", CompletionSources);
            bool? delivered = Boolean(root, "delivered", "delivered");
            int? errorCode = Int32(root, "error_code", "error_code");
            ParsedField<string> errorName = OptionalString(root, "error_name", "error_name");
            ParsedField<int> persistenceStatus = OptionalInt32(root, "persistence_status", "persistence_status");
            string? persistenceProvenance = EnumString(root, "persistence_provenance", "persistence_provenance", PersistenceProvenances);
            long? completed = Int64(root, "completed_microseconds", "completed_microseconds");
            ParsedField<double> deliveryLatency = OptionalDouble(root, "delivery_latency_ms", "delivery_latency_ms");
            ParsedField<double> synchronousDuration = OptionalDouble(root, "synchronous_call_duration_ms", "synchronous_call_duration_ms");

            if (Problems.Count != 0 || schemaVersion is null || identity is null || attempt is null || completionSource is null ||
                delivered is null || errorCode is null || persistenceProvenance is null || completed is null)
            {
                return null;
            }

            return new LedgerRow(schemaVersion.Value, identity, attempt, completionSource, delivered.Value, errorCode.Value,
                errorName, persistenceStatus, persistenceProvenance, completed.Value, deliveryLatency, synchronousDuration);
        }

        private AttemptEntry? ReadAttempt(JsonElement value)
        {
            Validate(value, "attempt", AttemptFields);
            string? instanceId = String(value, "instance_id", "attempt.instance_id");
            string? attemptId = String(value, "attempt_id", "attempt.attempt_id");
            long? ordinal = Int64(value, "ordinal", "attempt.ordinal");
            string? tenantId = String(value, "tenant_id", "attempt.tenant_id");
            string? documentId = String(value, "document_id", "attempt.document_id");
            string? key = String(value, "key", "attempt.key");
            long? sequence = Int64(value, "sequence", "attempt.sequence");
            string? eventType = String(value, "event_type", "attempt.event_type");
            long? planned = Int64(value, "planned_microseconds", "attempt.planned_microseconds");
            long? invoked = Int64(value, "invoked_microseconds", "attempt.invoked_microseconds");
            return Problems.Count != 0 || instanceId is null || attemptId is null || ordinal is null || tenantId is null ||
                documentId is null || key is null || sequence is null || eventType is null || planned is null || invoked is null
                ? null
                : new AttemptEntry(instanceId, attemptId, ordinal.Value, tenantId, documentId, key, sequence.Value,
                    eventType, planned.Value, invoked.Value);
        }

        private void Validate(JsonElement value, string path, IReadOnlySet<string> fields)
        {
            Problems.AddRange(StrictJson.ValidateProperties(value, path, fields, fields, artifact, 1, rowNumber));
        }

        private JsonElement Object(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
            {
                Add(parent.TryGetProperty(property, out _) ? "wrong_json_kind" : "missing_property", path);
                return default;
            }

            return value;
        }

        private string? String(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) { Add("missing_property", path); return null; }
            if (!StrictJson.TryGetString(value, out string result)) { Add("wrong_json_kind", path); return null; }
            return result;
        }

        private string? EnumString(JsonElement parent, string property, string path, IReadOnlySet<string> accepted)
        {
            string? result = String(parent, property, path);
            if (result is not null && !accepted.Contains(result)) { Add("unsupported_harness_value", path); return null; }
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

        private ParsedField<string> OptionalString(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) return ParsedField<string>.Missing();
            if (value.ValueKind == JsonValueKind.Null) return ParsedField<string>.Null();
            if (StrictJson.TryGetString(value, out string result)) return ParsedField<string>.FromValue(result);
            Add("wrong_json_kind", path);
            return ParsedField<string>.Missing();
        }

        private ParsedField<int> OptionalInt32(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) return ParsedField<int>.Missing();
            if (value.ValueKind == JsonValueKind.Null) return ParsedField<int>.Null();
            if (value.ValueKind == JsonValueKind.Number && StrictJson.TryGetInt32(value, out int result)) return ParsedField<int>.FromValue(result);
            Add("wrong_json_kind", path);
            return ParsedField<int>.Missing();
        }

        private ParsedField<double> OptionalDouble(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) return ParsedField<double>.Missing();
            if (value.ValueKind == JsonValueKind.Null) return ParsedField<double>.Null();
            if (StrictJson.TryGetFiniteDouble(value, out double result)) return ParsedField<double>.FromValue(result);
            Add("wrong_json_kind", path);
            return ParsedField<double>.Missing();
        }

        private void Add(string code, string path) => Problems.Add(new StructuralProblem(1, code, artifact, rowNumber, path, null));
    }

    private sealed class NestedParser(string artifact, long rowNumber, List<StructuralProblem> problems)
    {
        public Common.Contracts.RunIdentity? ReadIdentity(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            IReadOnlySet<string> fields = Fields("scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id");
            problems.AddRange(StrictJson.ValidateProperties(value, path, fields, fields, artifact, 1, rowNumber));
            string? scenario = Get(value, "scenario_id", path);
            string? configuration = Get(value, "configuration_id", path);
            string? run = Get(value, "run_id", path);
            string? instance = Get(value, "instance_id", path);
            string? expected = Get(value, "expected_instance_id", path);
            string? client = Get(value, "client_id", path);
            return scenario is null || configuration is null || run is null || instance is null || expected is null || client is null
                ? null
                : new RunIdentity(scenario, configuration, run, instance, expected, client);
        }

        private string? Get(JsonElement parent, string field, string path)
        {
            if (!parent.TryGetProperty(field, out JsonElement value) || !StrictJson.TryGetString(value, out string result))
            {
                problems.Add(new StructuralProblem(1, parent.TryGetProperty(field, out _) ? "wrong_json_kind" : "missing_property", artifact, rowNumber, $"{path}.{field}", null));
                return null;
            }
            return result;
        }
    }
}
