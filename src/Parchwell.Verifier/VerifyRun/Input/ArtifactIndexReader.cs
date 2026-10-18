using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.VerifyRun.Input;

/// <summary>Reads the index's declarations without comparing them to payloads or process status.</summary>
internal static class ArtifactIndexReader
{
    private const string Artifact = "index.json";
    private static readonly IReadOnlySet<string> IndexFields = Fields(
        "schema_version", "identity", "worker_status", "bookkeeping_complete", "worker_complete",
        "publication_complete", "artifacts");
    private static readonly IReadOnlySet<string> IdentityFields = Fields(
        "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id");
    private static readonly IReadOnlySet<string> DescriptorFields = Fields("name", "length", "sha256");
    private static readonly IReadOnlySet<string> WorkerStatuses = Fields("complete");

    /// <summary>Returns structural index data; a caller treats any parse problem as an execution failure.</summary>
    public static ParseResult<ArtifactIndex> Read(ReadOnlyMemory<byte>? source, InputLimits limits, InputBudget budget)
    {
        if (source is null)
            return ParseResult<ArtifactIndex>.Failure(new StructuralProblem(null, "missing_input", Artifact, null, null, null));
        ReadOnlyMemory<byte> utf8 = source.Value;
        budget.ConsumeBytes(utf8.Length);
        ParseResult<JsonDocument> parsed = StrictJson.ParseDocument(utf8, Artifact, limits, null);
        if (!parsed.IsSuccess) return ParseResult<ArtifactIndex>.Failure(parsed.Problems);
        using JsonDocument document = parsed.Value!;
        var parser = new IndexParser(budget);
        ArtifactIndex? value = parser.Read(document.RootElement);
        return parser.Problems.Count == 0 && value is not null
            ? ParseResult<ArtifactIndex>.Success(value)
            : ParseResult<ArtifactIndex>.Failure(parser.Problems);
    }

    private static HashSet<string> Fields(params string[] names) => new(names, StringComparer.Ordinal);

    private sealed class IndexParser(InputBudget budget)
    {
        public List<StructuralProblem> Problems { get; } = [];

        public ArtifactIndex? Read(JsonElement root)
        {
            Validate(root, "$", IndexFields);
            int? schema = Int32(root, "schema_version", "schema_version");
            if (schema is not null && schema != 1) Add("unsupported_schema_version", "schema_version");
            RunIdentity? identity = ReadIdentity(Object(root, "identity", "identity"));
            string? status = String(root, "worker_status", "worker_status", WorkerStatuses);
            bool? bookkeeping = Boolean(root, "bookkeeping_complete", "bookkeeping_complete");
            bool? workerComplete = Boolean(root, "worker_complete", "worker_complete");
            bool? publicationComplete = Boolean(root, "publication_complete", "publication_complete");
            List<ArtifactDescriptor>? artifacts = ReadArtifacts(root);

            if (Problems.Count != 0 || schema is null || identity is null || status is null || bookkeeping is null ||
                workerComplete is null || publicationComplete is null || artifacts is null)
            {
                return null;
            }

            return new ArtifactIndex(schema.Value, identity, status, bookkeeping.Value, workerComplete.Value,
                publicationComplete.Value, artifacts);
        }

        private RunIdentity? ReadIdentity(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object) return null;
            Validate(value, "identity", IdentityFields);
            string? scenario = String(value, "scenario_id", "identity.scenario_id");
            string? configuration = String(value, "configuration_id", "identity.configuration_id");
            string? run = String(value, "run_id", "identity.run_id");
            string? instance = String(value, "instance_id", "identity.instance_id");
            string? expected = String(value, "expected_instance_id", "identity.expected_instance_id");
            string? client = String(value, "client_id", "identity.client_id");
            return scenario is null || configuration is null || run is null || instance is null || expected is null || client is null
                ? null : new RunIdentity(scenario, configuration, run, instance, expected, client);
        }

        private List<ArtifactDescriptor>? ReadArtifacts(JsonElement root)
        {
            if (!root.TryGetProperty("artifacts", out JsonElement array)) { Add("missing_property", "artifacts"); return null; }
            if (array.ValueKind != JsonValueKind.Array) { Add("wrong_json_kind", "artifacts"); return null; }
            budget.ConsumeEntries(array.GetArrayLength());
            var result = new List<ArtifactDescriptor>();
            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                string path = $"artifacts[{index++}]";
                if (item.ValueKind != JsonValueKind.Object) { Add("expected_object", path); continue; }
                Validate(item, path, DescriptorFields);
                string? name = String(item, "name", path + ".name");
                long? length = Int64(item, "length", path + ".length");
                string? digest = String(item, "sha256", path + ".sha256");
                if (name is not null && length is not null && digest is not null)
                    result.Add(new ArtifactDescriptor(name, length.Value, digest));
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
            Problems.AddRange(StrictJson.ValidateProperties(value, path, fields, fields, Artifact, null));

        private string? String(JsonElement parent, string property, string path, IReadOnlySet<string>? allowed = null)
        {
            if (!parent.TryGetProperty(property, out JsonElement value)) { Add("missing_property", path); return null; }
            if (!StrictJson.TryGetString(value, out string result)) { Add("wrong_json_kind", path); return null; }
            if (allowed is not null && !allowed.Contains(result)) { Add("unsupported_harness_value", path); return null; }
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

        private void Add(string code, string path) => Problems.Add(new StructuralProblem(null, code, Artifact, null, path, null));
    }
}
