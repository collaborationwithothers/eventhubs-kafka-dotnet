using System.Collections.Immutable;
using System.Globalization;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios;

/// <summary>Checks the closed probe-v2 declarations owned by scenario-contract.md.</summary>
internal static class ProbeV2DefinitionReader
{
    private static readonly string[] RowFields =
        ["action", "role", "service_account", "topic", "partition_mode", "partition", "value_kind", "window_ms", "key_rule", "read_rule"];
    private static readonly KafkaClientRole[] Roles = [KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer];

    internal static ProbeV2Definition? Read(NodeValidation validation, InputNode? node, ScenarioDefinition definition)
    {
        var expected = Rows(definition.Id);
        if (node is null)
        {
            if (expected.Count != 0)
                validation.Error(definition.Source, "probe_v2", "probe_v2_required", "This item requires its probe-v2 declaration.");
            return null;
        }
        if (expected.Count == 0)
        {
            validation.Error(node, "probe_v2", "probe_v2_unexpected", "This item cannot declare probe_v2.");
            return null;
        }
        if (node is not MapNode map || !ExactKeys(map, ["configurations"]) ||
            map.Fields.GetValueOrDefault("configurations") is not MapNode configurations)
        {
            validation.Error(node, "probe_v2", "probe_v2_configurations", "Expected exactly the configurations mapping.");
            return null;
        }
        if (!expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(configurations.Fields.Keys) ||
            !expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(definition.Configurations.Keys))
            validation.Error(configurations, "probe_v2.configurations", "probe_v2_configurations", "Configurations must equal the fixed matrix.");

        foreach (var (id, input) in configurations.Fields)
        {
            if (!expected.TryGetValue(id, out var literal)) continue;
            var path = "probe_v2.configurations." + id;
            if (input is not MapNode row || !ExactKeys(row, RowFields))
            {
                validation.Error(input, path, "probe_v2_literal", "The row must contain exactly the fixed fields.");
                continue;
            }
            Check("action", Text(row, "action") == literal.Action);
            Check("role", Text(row, "role") == literal.Role.ToString());
            Check("service_account", Text(row, "service_account") == literal.ServiceAccount);
            Check("topic", Text(row, "topic") == literal.Topic);
            Check("partition_mode", Text(row, "partition_mode") == literal.PartitionMode);
            Check("partition", literal.Partition is null
                ? row.Fields["partition"] is ScalarNode { IsPlain: true, Value: "null" }
                : Integer(row, "partition") == literal.Partition);
            Check("value_kind", Text(row, "value_kind") == literal.ValueKind);
            Check("window_ms", Integer(row, "window_ms") == literal.WindowMs);
            Check("key_rule", Text(row, "key_rule") == literal.KeyRule);
            Check("read_rule", Text(row, "read_rule") == literal.ReadRule);
            continue;

            void Check(string field, bool valid)
            {
                if (!valid) validation.Error(row.Fields[field], path + "." + field, "probe_v2_literal", "The field must equal the fixed matrix input.");
            }
        }
        if (definition.Repeats != 5 || definition.Fault.Live.Kind != LiveFaultKind.None ||
            definition.Fault.Component.Kind != ComponentFaultKind.None || definition.Gate is not NoGate ||
            !Roles.ToHashSet().SetEquals(definition.Roles) || definition.Measures.Length != 4 ||
            Roles.Any(role => definition.Measures.Count(m => m.Role == role && m.Id == "single_action.outcome") != 1 ||
                definition.Measures.Count(m => m.Role == role && m.Id == "single_action.client_errors") != 1))
            validation.Error(node, "probe_v2", "probe_v2_literal", "Repeats, faults, gate, roles and measures must equal the fixed experiment.");

        foreach (var (id, configuration) in definition.Configurations)
        {
            string[] deltas = definition.Id == "B08" && id == "C1" ? ["LoadGenProducer:partitioner=murmur2_random"] : [];
            var actual = configuration.Overrides.SelectMany(role => role.Value.Select(setting => $"{role.Key}:{setting.Key}={setting.Value.Value}"));
            if (!deltas.Order(StringComparer.Ordinal).SequenceEqual(actual.Order(StringComparer.Ordinal), StringComparer.Ordinal))
                validation.Error(node, "configurations." + id, "probe_v2_deltas", "Only the fixed B08 C1 partitioner delta is permitted.");
        }
        return new(expected);
    }

    private static bool ExactKeys(MapNode map, string[] fields) =>
        map.Fields.Count == fields.Length && fields.All(map.Fields.ContainsKey);

    private static string? Text(MapNode map, string key) => map.Fields[key] is ScalarNode scalar &&
        !(scalar.IsPlain && (scalar.Value.Length == 0 || scalar.Value is "null" or "Null" or "NULL" or "~")) ? scalar.Value : null;

    private static int? Integer(MapNode map, string key) => map.Fields[key] is ScalarNode { IsPlain: true } scalar &&
        scalar.Value.Length != 0 && scalar.Value.All(char.IsAsciiDigit) &&
        int.TryParse(scalar.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static ImmutableDictionary<string, ProbeV2Configuration> Rows(string item)
    {
        var rows = ImmutableDictionary.CreateBuilder<string, ProbeV2Configuration>(StringComparer.Ordinal);
        if (item is "B08" or "E01")
        {
            for (int i = 0; i < 2; i++)
                rows["C" + i.ToString(CultureInfo.InvariantCulture)] = new("produce_one", KafkaClientRole.LoadGenProducer,
                    item == "B08" ? "loadgen" : "probe", item == "B08" ? "document-events" : "document-state",
                    item == "B08" ? "automatic" : "explicit", item == "B08" ? null : 0,
                    item == "B08" ? "marker" : i == 0 ? "empty" : "null", 330000,
                    item == "B08" ? "b08_fixed" : "e01_unique", "delivery_location");
        }
        else if (item == "F06")
        {
            string[] topics = ["document-events", "document-state", "document-events-dlq"];
            for (int i = 0; i < 12; i++)
            {
                bool producer = i < 6;
                rows["C" + i.ToString(CultureInfo.InvariantCulture)] = new(producer ? "produce_one" : "assign_consume",
                    producer ? KafkaClientRole.LoadGenProducer : KafkaClientRole.VerifierConsumer,
                    producer ? i % 2 == 0 ? "loadgen" : "probe" : i < 9 ? "probe" : "enrichment-consumer",
                    topics[producer ? i / 2 : (i - 6) % 3], "explicit", 0, "marker", producer ? 330000 : 120000,
                    producer ? "f06_unique" : "f06_seed", producer ? "delivery_location" : "seeded_location");
            }
        }
        return rows.ToImmutable();
    }
}
