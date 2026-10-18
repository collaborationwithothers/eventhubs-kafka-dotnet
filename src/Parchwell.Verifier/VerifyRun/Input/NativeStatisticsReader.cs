using System.Text.Json;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.VerifyRun.Input;

/// <summary>Projects selected fields from open librdkafka reports without judging their values.</summary>
internal static class NativeStatisticsReader
{
    /// <summary>Parses raw native JSON under its own byte and broker-count ceilings.</summary>
    public static ParseResult<NativeClientReport> Read(
        ReadOnlyMemory<byte> utf8,
        string artifact,
        InputLimits limits,
        InputBudget budget,
        long maxNativeReportBytes,
        long maxBrokerEntries)
    {
        if (utf8.Length > maxNativeReportBytes)
            throw new InputLimitExceededException($"{artifact} exceeds the configured native report byte limit.");

        ParseResult<JsonDocument> parsed = StrictJson.ParseDocument(
            utf8, artifact, limits with { MaxArtifactBytes = Math.Min(limits.MaxArtifactBytes, maxNativeReportBytes) }, 20);
        if (!parsed.IsSuccess) return ParseResult<NativeClientReport>.Failure(parsed.Problems);
        using JsonDocument document = parsed.Value!;
        var parser = new NativeParser(artifact, budget, maxBrokerEntries);
        NativeClientReport? report = parser.Read(document.RootElement);
        return parser.Problems.Count != 0 || report is null
            ? ParseResult<NativeClientReport>.Failure(parser.Problems)
            : new ParseResult<NativeClientReport>(report, parser.MissingFields);
    }

    private sealed class NativeParser(string artifact, InputBudget budget, long maxBrokerEntries)
    {
        public List<StructuralProblem> Problems { get; } = [];
        public List<StructuralProblem> MissingFields { get; } = [];

        public NativeClientReport? Read(JsonElement root)
        {
            // Native objects are deliberately open: ordinary additional fields are accepted at every depth.
            if (root.ValueKind != JsonValueKind.Object)
            {
                Add("root_not_object", "$", null);
                return null;
            }

            ParsedField<string> name = OptionalString(root, "name", "name");
            ParsedField<string> clientId = OptionalString(root, "client_id", "client_id");
            ParsedField<string> type = OptionalString(root, "type", "type");
            ParsedField<long> timestamp = OptionalInt64(root, "ts", "ts", required: true, missingCheck: 20);
            ParsedField<long> time = OptionalInt64(root, "time", "time");
            ParsedField<long> age = OptionalInt64(root, "age", "age", required: true, missingCheck: 20);
            ParsedField<IReadOnlyDictionary<string, NativeBrokerReport>> brokers = ReadBrokers(root);
            if (Problems.Count != 0 || timestamp.Presence != FieldPresence.Value || age.Presence != FieldPresence.Value ||
                brokers.Presence != FieldPresence.Value)
            {
                return null;
            }

            return new NativeClientReport(name, clientId, type, timestamp, time, age, brokers);
        }

        private ParsedField<IReadOnlyDictionary<string, NativeBrokerReport>> ReadBrokers(JsonElement root)
        {
            if (!root.TryGetProperty("brokers", out JsonElement value))
            {
                Add("missing_property", "brokers", null);
                return ParsedField<IReadOnlyDictionary<string, NativeBrokerReport>>.Missing();
            }
            if (value.ValueKind != JsonValueKind.Object)
            {
                Add("wrong_json_kind", "brokers", null);
                return ParsedField<IReadOnlyDictionary<string, NativeBrokerReport>>.Missing();
            }

            long count = 0;
            foreach (JsonProperty _ in value.EnumerateObject()) count++;
            if (count > maxBrokerEntries)
                throw new InputLimitExceededException($"{artifact} exceeds the configured native broker entry limit.");
            budget.ConsumeEntries(count);

            var result = new Dictionary<string, NativeBrokerReport>(StringComparer.Ordinal);
            foreach (JsonProperty broker in value.EnumerateObject())
            {
                if (broker.Value.ValueKind != JsonValueKind.Object)
                {
                    Add("expected_object", "brokers." + broker.Name, null);
                    continue;
                }
                NativeBrokerReport? parsed = ReadBroker(broker.Name, broker.Value);
                if (parsed is not null) result.Add(broker.Name, parsed);
            }

            return ParsedField<IReadOnlyDictionary<string, NativeBrokerReport>>.FromValue(result);
        }

        private NativeBrokerReport? ReadBroker(string dictionaryKey, JsonElement value)
        {
            ParsedField<string> name = OptionalString(value, "name", $"brokers.{dictionaryKey}.name", required: true);
            ParsedField<long> nodeId = OptionalInt64(value, "nodeid", $"brokers.{dictionaryKey}.nodeid", required: true);
            ParsedField<string> source = OptionalString(value, "source", $"brokers.{dictionaryKey}.source", required: true);
            ParsedField<string> state = OptionalString(value, "state", $"brokers.{dictionaryKey}.state", required: true);
            ParsedField<long> timeouts = OptionalInt64(value, "req_timeouts", $"brokers.{dictionaryKey}.req_timeouts", required: true);
            ParsedField<long> retries = OptionalInt64(value, "txretries", $"brokers.{dictionaryKey}.txretries", required: true);
            ParsedField<long> errors = OptionalInt64(value, "txerrs", $"brokers.{dictionaryKey}.txerrs", required: true);
            ParsedField<long> receive = OptionalInt64(value, "rxerrs", $"brokers.{dictionaryKey}.rxerrs", required: true);
            ParsedField<long> disconnects = OptionalInt64(value, "disconnects", $"brokers.{dictionaryKey}.disconnects", required: true);
            ParsedField<NativeWindow> throttle = Window(value, "throttle", $"brokers.{dictionaryKey}.throttle", "max");
            ParsedField<NativeWindow> roundTripTime = Window(value, "rtt", $"brokers.{dictionaryKey}.rtt", "p99");
            return Problems.Count != 0 ? null : new NativeBrokerReport(dictionaryKey, name, nodeId, source, state,
                timeouts, retries, errors, receive, disconnects, throttle, roundTripTime);
        }

        private ParsedField<NativeWindow> Window(JsonElement parent, string property, string path, string valueField)
        {
            if (!parent.TryGetProperty(property, out JsonElement value))
            {
                Missing(path + ".cnt");
                Missing(path + "." + valueField);
                return ParsedField<NativeWindow>.Missing();
            }
            if (value.ValueKind != JsonValueKind.Object)
            {
                Add("wrong_json_kind", path, null);
                return ParsedField<NativeWindow>.Missing();
            }
            ParsedField<long> count = OptionalInt64(value, "cnt", path + ".cnt", required: true);
            ParsedField<long> maximum = valueField == "max"
                ? OptionalInt64(value, "max", path + ".max", required: true)
                : ParsedField<long>.Missing();
            ParsedField<long> p99 = valueField == "p99"
                ? OptionalInt64(value, "p99", path + ".p99", required: true)
                : ParsedField<long>.Missing();
            return ParsedField<NativeWindow>.FromValue(new NativeWindow(count, maximum, p99));
        }

        private ParsedField<string> OptionalString(JsonElement parent, string property, string path, bool required = false)
        {
            if (!parent.TryGetProperty(property, out JsonElement value))
            {
                if (required) Missing(path);
                return ParsedField<string>.Missing();
            }
            if (StrictJson.TryGetString(value, out string result)) return ParsedField<string>.FromValue(result);
            Add("wrong_json_kind", path, null);
            return ParsedField<string>.Missing();
        }

        private ParsedField<long> OptionalInt64(JsonElement parent, string property, string path, bool required = false, int? missingCheck = null)
        {
            if (!parent.TryGetProperty(property, out JsonElement value))
            {
                if (required && missingCheck is int check)
                    Problems.Add(new StructuralProblem(check, "missing_property", artifact, null, path, null));
                else if (required)
                    Missing(path);
                return ParsedField<long>.Missing();
            }
            if (StrictJson.TryGetInt64(value, out long result)) return ParsedField<long>.FromValue(result);
            Add("wrong_json_kind", path, null);
            return ParsedField<long>.Missing();
        }

        private void Add(string code, string path, long? row) => Problems.Add(new StructuralProblem(20, code, artifact, row, path, null));
        private void Missing(string path) => MissingFields.Add(new StructuralProblem(21, "missing_required_field", artifact, null, path, null));
    }
}
