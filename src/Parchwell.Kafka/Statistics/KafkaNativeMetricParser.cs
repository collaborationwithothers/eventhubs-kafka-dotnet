using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Parchwell.Kafka.Statistics;

// The parser never retains JsonElements or mutates counter state. The caller retains the original text.
internal static class KafkaNativeMetricParser
{
    private static readonly IReadOnlyDictionary<string, KafkaNativeMetricDefinition> ByPath =
        KafkaNativeMetricCatalogue.Definitions.ToDictionary(x => x.Path, StringComparer.Ordinal);

    internal static KafkaNativeMetricProjection Parse(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Failure("invalid_root", null);
            }

            if (HasDuplicates(root))
            {
                return Failure("duplicate_property", null);
            }

            if (!RequiredString(root, "name", out string name)
                || !RequiredString(root, "client_id", out string clientId)
                || !RequiredString(root, "type", out string type)
                || !RequiredNumber(root, "ts", out long ts)
                || !RequiredNumber(root, "time", out long time)
                || !RequiredNumber(root, "age", out long age))
            {
                return Failure("invalid_or_missing_field", "identity");
            }

            KafkaNativeMetricIdentity identity = new(name, clientId, type, ts, time, age);
            Parser parser = new();
            parser.Visit(root, "", null, null, null, null);
            return parser.Error is null
                ? new(true, identity, parser.Points.ToImmutable(), parser.Diagnostics.ToImmutable())
                : new(false, identity, [], parser.Diagnostics.ToImmutable());
        }
        catch (JsonException)
        {
            return Failure("invalid_json", null);
        }
    }

    private static KafkaNativeMetricProjection Failure(string code, string? field) =>
        new(false, null, [], [new(code, field)]);

    private static bool RequiredString(JsonElement root, string field, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(field, out JsonElement element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString()!;
        return value.Length > 0;
    }

    private static bool RequiredNumber(JsonElement root, string field, out long value)
    {
        value = 0;
        return root.TryGetProperty(field, out JsonElement element) && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt64(out value) && value >= 0;
    }

    private static bool HasDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name) || HasDuplicates(property.Value))
                {
                    return true;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (HasDuplicates(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed class Parser
    {
        internal ImmutableArray<KafkaNativeMetricPoint>.Builder Points { get; } = ImmutableArray.CreateBuilder<KafkaNativeMetricPoint>();
        internal ImmutableArray<KafkaNativeMetricDiagnostic>.Builder Diagnostics { get; } = ImmutableArray.CreateBuilder<KafkaNativeMetricDiagnostic>();
        internal string? Error { get; private set; }

        internal void Visit(JsonElement obj, string path, string? broker, string? topic, int? partition, string? brokerAddress)
        {
            if (Error is not null)
            {
                return;
            }

            if (obj.ValueKind != JsonValueKind.Object)
            {
                Reject("invalid_object", StaticField(path));
                return;
            }

            if (path == "topics.*.partitions.*" && !ValidateAlias(obj))
            {
                return;
            }

            foreach (JsonProperty property in obj.EnumerateObject())
            {
                string next = path.Length == 0 ? property.Name : path + "." + property.Name;
                if (IsMap(next))
                {
                    VisitMap(property.Value, next, broker, topic, brokerAddress);
                }
                else if (IsObject(next))
                {
                    Visit(property.Value, next, broker, topic, partition, brokerAddress);
                }
                else if (ByPath.TryGetValue(next, out KafkaNativeMetricDefinition? definition))
                {
                    ReadField(obj, property.Value, definition, broker, topic, partition, brokerAddress);
                }

                if (Error is not null)
                {
                    return;
                }
            }
        }

        private void VisitMap(JsonElement map, string path, string? broker, string? topic, string? brokerAddress)
        {
            if (map.ValueKind != JsonValueKind.Object)
            {
                Reject("invalid_object", path);
                return;
            }

            int count = 0;
            foreach (JsonProperty entry in map.EnumerateObject())
            {
                count++;
                if ((path == "brokers" && count > 64) || (path == "topics" && count > 3)
                    || (path == "topics.*.partitions" && count > 33)
                    || (path == "brokers.*.toppars" && count > 99))
                {
                    Reject("entity_limit_exceeded", path);
                    return;
                }

                if (path == "brokers.*.req")
                {
                    if (!KafkaNativeMetricCatalogue.Requests.Contains(entry.Name, StringComparer.Ordinal))
                    {
                        AddDiagnostic("unknown_request", "brokers.*.req");
                        continue;
                    }

                    ReadNumeric(entry.Value, ByPath["brokers.*.req.*"], broker, null, null, entry.Name, brokerAddress);
                    continue;
                }

                string? nextBroker = path == "brokers" ? entry.Name : broker;
                string? nextTopic = path == "topics" ? entry.Name : topic;
                int? nextPartition = null;
                if (path == "topics.*.partitions" && (!int.TryParse(entry.Name, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number)
                    || number < -1 || number.ToString(CultureInfo.InvariantCulture) != entry.Name))
                {
                    Reject("identity_mismatch", "topics.*.partitions");
                    return;
                }

                if (path == "topics.*.partitions")
                {
                    nextPartition = int.Parse(entry.Name, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                }

                if (entry.Value.ValueKind != JsonValueKind.Object || !IdentityMatches(entry.Value, path, entry.Name)
                    || (path == "brokers.*.toppars" && !TopparMatches(entry.Value, entry.Name)))
                {
                    Reject("identity_mismatch", path);
                    return;
                }

                string? nextAddress = brokerAddress;
                if (path == "brokers" && entry.Value.TryGetProperty("nodename", out JsonElement address))
                {
                    if (address.ValueKind != JsonValueKind.String)
                    {
                        Reject("invalid_identity", "brokers.*.nodename");
                        return;
                    }

                    nextAddress = address.GetString();
                }

                Visit(entry.Value, path + ".*", nextBroker, nextTopic, nextPartition, nextAddress);
                if (Error is not null)
                {
                    return;
                }
            }
        }

        private static bool IdentityMatches(JsonElement obj, string path, string key)
        {
            string? field = path switch { "brokers" => "name", "topics" => "topic", "topics.*.partitions" => "partition", _ => null };
            if (field is null || !obj.TryGetProperty(field, out JsonElement identity))
            {
                return true;
            }

            if (path == "topics.*.partitions")
            {
                return identity.ValueKind == JsonValueKind.Number && identity.TryGetInt64(out long id)
                    && id.ToString(CultureInfo.InvariantCulture) == key;
            }

            return identity.ValueKind == JsonValueKind.String && StringComparer.Ordinal.Equals(identity.GetString(), key);
        }

        private static bool TopparMatches(JsonElement obj, string key)
        {
            bool hasTopic = obj.TryGetProperty("topic", out JsonElement topic);
            bool hasPartition = obj.TryGetProperty("partition", out JsonElement partition);
            if ((hasTopic && topic.ValueKind != JsonValueKind.String)
                || (hasPartition && (partition.ValueKind != JsonValueKind.Number
                    || !partition.TryGetInt64(out _) || partition.GetInt64() < -1)))
            {
                return false;
            }

            return !hasTopic || !hasPartition || StringComparer.Ordinal.Equals(key,
                string.Concat(topic.GetString(), "-", partition.GetInt64().ToString(CultureInfo.InvariantCulture)));
        }

        private bool ValidateAlias(JsonElement obj)
        {
            if (!obj.TryGetProperty("commited_offset", out JsonElement alias))
            {
                return true;
            }

            if (alias.ValueKind != JsonValueKind.Number || !alias.TryGetInt64(out long a))
            {
                Reject("invalid_numeric", "topics.*.partitions.*.commited_offset");
                return false;
            }

            if (obj.TryGetProperty("committed_offset", out JsonElement canonical)
                && (canonical.ValueKind != JsonValueKind.Number || !canonical.TryGetInt64(out long c) || a != c))
            {
                Reject("alias_conflict", "topics.*.partitions.*.committed_offset");
                return false;
            }

            return true;
        }

        private void ReadField(JsonElement parent, JsonElement value, KafkaNativeMetricDefinition definition,
            string? broker, string? topic, int? partition, string? brokerAddress)
        {
            if (definition.Policy == "R")
            {
                return;
            }

            if (definition.Policy == "I")
            {
                if ((definition.Path.EndsWith(".partition", StringComparison.Ordinal) &&
                        (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out _))) ||
                    (!definition.Path.EndsWith(".partition", StringComparison.Ordinal) && value.ValueKind != JsonValueKind.String))
                {
                    Reject("invalid_identity", definition.Path);
                }

                return;
            }

            if (definition.Policy == "A")
            {
                if (parent.TryGetProperty("committed_offset", out _))
                {
                    return;
                }

                definition = ByPath["topics.*.partitions.*.committed_offset"];
            }

            if (definition.Policy == "S")
            {
                if (value.ValueKind != JsonValueKind.String)
                {
                    Reject("invalid_state", definition.Path);
                    return;
                }

                string? state = value.GetString();
                ImmutableArray<string> allowed = States(definition.Path);
                if (!allowed.Contains(state, StringComparer.Ordinal))
                {
                    AddDiagnostic("unknown_state", definition.Path);
                }

                foreach (string candidate in allowed)
                {
                    Points.Add(new(definition, StringComparer.Ordinal.Equals(candidate, state) ? 1 : 0,
                        broker, topic, partition, null, candidate, brokerAddress));
                }

                return;
            }

            if (definition.Policy == "B")
            {
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    Reject("invalid_boolean", definition.Path);
                    return;
                }

                Points.Add(new(definition, value.GetBoolean() ? 1 : 0, broker, topic, partition, null, null, brokerAddress));
                return;
            }

            if (definition.Policy == "W+")
            {
                // Validate even suppressed values; absence of count cannot authorize a summary.
                if (value.ValueKind != JsonValueKind.Number)
                {
                    Reject("invalid_numeric", definition.Path);
                    return;
                }

                if (!value.TryGetInt64(out long summary))
                {
                    string raw = value.GetRawText();
                    Reject(raw.IndexOfAny(['.', 'e', 'E']) >= 0 ? "invalid_numeric" : "numeric_overflow", definition.Path);
                    return;
                }

                if (summary < 0)
                {
                    Reject("invalid_numeric", definition.Path);
                    return;
                }

                if (!parent.TryGetProperty("cnt", out JsonElement count) || count.ValueKind != JsonValueKind.Number
                    || !count.TryGetInt64(out long samples) || samples <= 0)
                {
                    return;
                }
            }

            ReadNumeric(value, definition, broker, topic, partition, null, brokerAddress);
        }

        private void ReadNumeric(JsonElement value, KafkaNativeMetricDefinition definition,
            string? broker, string? topic, int? partition, string? request, string? brokerAddress)
        {
            if (value.ValueKind != JsonValueKind.Number)
            {
                Reject("invalid_numeric", definition.Path);
                return;
            }

            if (!value.TryGetInt64(out long number))
            {
                string raw = value.GetRawText();
                Reject(raw.IndexOfAny(['.', 'e', 'E']) >= 0 ? "invalid_numeric" : "numeric_overflow", definition.Path);
                return;
            }

            if (definition.Policy != "V" && number < 0)
            {
                Reject("invalid_numeric", definition.Path);
                return;
            }

            Points.Add(new(definition, number, broker, topic, partition, request, null, brokerAddress));
        }

        private void Reject(string code, string? field)
        {
            Error = code;
            AddDiagnostic(code, field);
        }

        private void AddDiagnostic(string code, string? field)
        {
            // One diagnostic per static code/field pair bounds unknown values and does not expose them as tags.
            if (!Diagnostics.Any(d => d.Code == code && d.Field == field))
            {
                Diagnostics.Add(new(code, field));
            }
        }
    }

    private static bool IsMap(string path) => path is "brokers" or "topics" or "brokers.*.req"
        or "brokers.*.toppars" or "topics.*.partitions";

    private static bool IsObject(string path) => path is "cgrp" or "eos" or "fatal"
        or "brokers.*.int_latency" or "brokers.*.outbuf_latency" or "brokers.*.rtt" or "brokers.*.throttle"
        or "topics.*.batchsize" or "topics.*.batchcnt";

    private static string StaticField(string path) => path;

    private static ImmutableArray<string> States(string path) => path switch
    {
        "brokers.*.source" => KafkaNativeMetricCatalogue.BrokerSource,
        "brokers.*.state" => KafkaNativeMetricCatalogue.BrokerState,
        "topics.*.partitions.*.fetch_state" => KafkaNativeMetricCatalogue.FetchState,
        "cgrp.state" => KafkaNativeMetricCatalogue.GroupState,
        "cgrp.join_state" => KafkaNativeMetricCatalogue.JoinState,
        "eos.idemp_state" => KafkaNativeMetricCatalogue.IdempotenceState,
        "eos.txn_state" => KafkaNativeMetricCatalogue.TransactionState,
        _ => [],
    };
}
