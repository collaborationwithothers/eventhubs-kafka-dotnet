using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.VerifyRun.Input;

/// <summary>Reads the independently sealed manifest as structural evidence for check 2.</summary>
internal static class ManifestReader
{
    private const string Artifact = "manifest.json";
    private static readonly IReadOnlySet<string> ManifestFields = Fields(
        "schema_version", "identity", "generator_version", "planned_attempt_count", "attempt_count", "inventory",
        "timestamp_frequency", "start_timestamp", "worker_status", "bookkeeping_complete", "first_unexecuted_ordinal",
        "diagnostics", "runtime_versions", "evidence_files", "guard_observations");
    private static readonly IReadOnlySet<string> IdentityFields = Fields(
        "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id");
    private static readonly IReadOnlySet<string> AttemptFields = Fields(
        "instance_id", "attempt_id", "ordinal", "tenant_id", "document_id", "key", "sequence", "event_type",
        "planned_microseconds", "invoked_microseconds");
    private static readonly IReadOnlySet<string> WorkerStatuses = Fields("complete", "incomplete", "failed");

    /// <summary>Parses the manifest without reconciling its declarations to any other artifact.</summary>
    public static ParseResult<SealedRunManifest> Read(ReadOnlyMemory<byte>? source, InputLimits limits, InputBudget budget, long maxInventoryEntries)
    {
        if (source is null)
            return ParseResult<SealedRunManifest>.Failure(new StructuralProblem(2, "missing_input", Artifact, null, null, null));
        ReadOnlyMemory<byte> utf8 = source.Value;
        budget.ConsumeBytes(utf8.Length);
        ParseResult<JsonDocument> parsed = StrictJson.ParseDocument(utf8, Artifact, limits, 2);
        if (!parsed.IsSuccess) return ParseResult<SealedRunManifest>.Failure(parsed.Problems);

        using JsonDocument document = parsed.Value!;
        var parser = new ManifestParser(Artifact, budget, maxInventoryEntries);
        SealedRunManifest? manifest = parser.Read(document.RootElement);
        return parser.Problems.Count == 0 && manifest is not null
            ? ParseResult<SealedRunManifest>.Success(manifest)
            : ParseResult<SealedRunManifest>.Failure(parser.Problems);
    }

    private static HashSet<string> Fields(params string[] names) => new(names, StringComparer.Ordinal);

    private sealed class ManifestParser(string artifact, InputBudget budget, long maxInventoryEntries)
    {
        public List<StructuralProblem> Problems { get; } = [];

        public SealedRunManifest? Read(JsonElement root)
        {
            Validate(root, "$", ManifestFields);
            int? schema = Int32(root, "schema_version", "schema_version");
            if (schema is not null && schema != 1) Add("unsupported_schema_version", "schema_version");
            RunIdentity? identity = ReadIdentity(Object(root, "identity", "identity"), "identity");
            string? generatorVersion = String(root, "generator_version", "generator_version");
            long? plannedCount = Int64(root, "planned_attempt_count", "planned_attempt_count");
            long? attemptCount = Int64(root, "attempt_count", "attempt_count");
            List<AttemptEntry>? inventory = ReadAttemptArray(root, "inventory", "inventory");
            long? frequency = Int64(root, "timestamp_frequency", "timestamp_frequency");
            long? start = Int64(root, "start_timestamp", "start_timestamp");
            string? workerStatus = EnumString(root, "worker_status", "worker_status", WorkerStatuses);
            bool? bookkeeping = Boolean(root, "bookkeeping_complete", "bookkeeping_complete");
            ParsedField<long> firstUnexecuted = OptionalInt64(root, "first_unexecuted_ordinal", "first_unexecuted_ordinal");
            List<string>? diagnostics = ReadStringArray(root, "diagnostics", "diagnostics");
            Dictionary<string, string>? runtimeVersions = ReadStringMap(root, "runtime_versions", "runtime_versions");
            Dictionary<string, string>? evidenceFiles = ReadStringMap(root, "evidence_files", "evidence_files");
            List<string>? guards = ReadStringArray(root, "guard_observations", "guard_observations");

            if (Problems.Count != 0 || schema is null || identity is null || generatorVersion is null || plannedCount is null ||
                attemptCount is null || inventory is null || frequency is null || start is null || workerStatus is null ||
                bookkeeping is null || diagnostics is null || runtimeVersions is null || evidenceFiles is null || guards is null)
            {
                return null;
            }

            return new SealedRunManifest(schema.Value, identity, generatorVersion, plannedCount.Value, attemptCount.Value,
                inventory, frequency.Value, start.Value, workerStatus, bookkeeping.Value, firstUnexecuted, diagnostics,
                runtimeVersions, evidenceFiles, guards);
        }

        private RunIdentity? ReadIdentity(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, path, IdentityFields);
            string? scenario = String(value, "scenario_id", path + ".scenario_id");
            string? configuration = String(value, "configuration_id", path + ".configuration_id");
            string? run = String(value, "run_id", path + ".run_id");
            string? instance = String(value, "instance_id", path + ".instance_id");
            string? expected = String(value, "expected_instance_id", path + ".expected_instance_id");
            string? client = String(value, "client_id", path + ".client_id");
            return scenario is null || configuration is null || run is null || instance is null || expected is null || client is null
                ? null : new RunIdentity(scenario, configuration, run, instance, expected, client);
        }

        private List<AttemptEntry>? ReadAttemptArray(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement array)) { Add("missing_property", path); return null; }
            if (array.ValueKind != JsonValueKind.Array) { Add("wrong_json_kind", path); return null; }
            if (array.GetArrayLength() > maxInventoryEntries)
                throw new InputLimitExceededException($"{artifact} inventory exceeds the configured entry limit.");
            budget.ConsumeEntries(array.GetArrayLength());
            var result = new List<AttemptEntry>();
            int index = 0;
            foreach (JsonElement value in array.EnumerateArray())
            {
                string itemPath = $"{path}[{index++}]";
                if (value.ValueKind != JsonValueKind.Object) { Add("expected_object", itemPath); continue; }
                Validate(value, itemPath, AttemptFields);
                string? instance = String(value, "instance_id", itemPath + ".instance_id");
                string? id = String(value, "attempt_id", itemPath + ".attempt_id");
                long? ordinal = Int64(value, "ordinal", itemPath + ".ordinal");
                string? tenant = String(value, "tenant_id", itemPath + ".tenant_id");
                string? document = String(value, "document_id", itemPath + ".document_id");
                string? key = String(value, "key", itemPath + ".key");
                long? sequence = Int64(value, "sequence", itemPath + ".sequence");
                string? eventType = String(value, "event_type", itemPath + ".event_type");
                long? planned = Int64(value, "planned_microseconds", itemPath + ".planned_microseconds");
                long? invoked = Int64(value, "invoked_microseconds", itemPath + ".invoked_microseconds");
                if (instance is not null && id is not null && ordinal is not null && tenant is not null && document is not null &&
                    key is not null && sequence is not null && eventType is not null && planned is not null && invoked is not null)
                {
                    result.Add(new AttemptEntry(instance, id, ordinal.Value, tenant, document, key, sequence.Value,
                        eventType, planned.Value, invoked.Value));
                }
            }
            return result;
        }

        private List<string>? ReadStringArray(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement array)) { Add("missing_property", path); return null; }
            if (array.ValueKind != JsonValueKind.Array) { Add("wrong_json_kind", path); return null; }
            budget.ConsumeEntries(array.GetArrayLength());
            var result = new List<string>();
            int index = 0;
            foreach (JsonElement value in array.EnumerateArray())
            {
                if (!StrictJson.TryGetString(value, out string text)) Add("wrong_json_kind", $"{path}[{index}]");
                else result.Add(text);
                index++;
            }
            return result;
        }

        private Dictionary<string, string>? ReadStringMap(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement map)) { Add("missing_property", path); return null; }
            if (map.ValueKind != JsonValueKind.Object) { Add("wrong_json_kind", path); return null; }
            long count = 0;
            foreach (JsonProperty _ in map.EnumerateObject()) count++;
            budget.ConsumeEntries(count);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonProperty item in map.EnumerateObject())
            {
                if (!StrictJson.TryGetString(item.Value, out string text)) Add("wrong_json_kind", $"{path}.{item.Name}");
                else result.Add(item.Name, text);
            }
            return result;
        }

        private JsonElement Object(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) { Add("missing_property", path); return default; }
            if (value.ValueKind != JsonValueKind.Object) { Add("wrong_json_kind", path); return default; }
            return value;
        }

        private void Validate(JsonElement value, string path, IReadOnlySet<string> fields) =>
            Problems.AddRange(StrictJson.ValidateProperties(value, path, fields, fields, artifact, 2));

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

        private ParsedField<long> OptionalInt64(JsonElement parent, string property, string path)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) return ParsedField<long>.Missing();
            if (value.ValueKind == JsonValueKind.Null) return ParsedField<long>.Null();
            if (value.ValueKind == JsonValueKind.Number && StrictJson.TryGetInt64(value, out long result)) return ParsedField<long>.FromValue(result);
            Add("wrong_json_kind", path);
            return ParsedField<long>.Missing();
        }

        private void Add(string code, string path) => Problems.Add(new StructuralProblem(2, code, artifact, null, path, null));
    }
}
