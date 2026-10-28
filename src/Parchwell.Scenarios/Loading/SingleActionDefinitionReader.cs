using System.Collections.Immutable;
using System.Globalization;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios;

/// <summary>Checks the fixed experiments under scenario-contract.md, Single-action declarations.</summary>
internal static class SingleActionDefinitionReader
{
    internal static SingleActionDefinition? Read(NodeValidation validation, InputNode? node, ScenarioDefinition definition)
    {
        var expected = Rows(definition.Id);
        if (node is null)
        {
            if (expected.Count != 0) validation.Error(definition.Source, "single_action", "single_action_required", "This item requires its single-action declaration.");
            return null;
        }
        if (definition.Id is "A01" or "B04")
            validation.Error(node, "single_action", "single_action_unexpected", "Legacy items cannot declare single_action.");
        else if (expected.Count == 0)
            validation.Error(node, "single_action", "single_action_item", "The item is not an approved single-action experiment.");
        var map = validation.Map(node, "single_action");
        if (map is null) return null;
        validation.CheckFields(map, "single_action", "configurations");
        var configurations = validation.Map(map.Fields.GetValueOrDefault("configurations"), "single_action.configurations");
        if (configurations is null) return null;
        if (!expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(configurations.Fields.Keys) ||
            !expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(definition.Configurations.Keys))
            validation.Error(configurations, "single_action.configurations", "single_action_configurations", "The configuration set must equal the fixed experiment.");
        var result = ImmutableDictionary.CreateBuilder<string, SingleActionConfiguration>(StringComparer.Ordinal);
        foreach (var (id, input) in configurations.Fields)
        {
            var path = "single_action.configurations." + id;
            var row = validation.Map(input, path);
            if (row is null) continue;
            validation.TryFields(row, path, ["action", "role", "topic", "partition", "payload_bytes", "window_ms", "token_scope", "match", "terminal_rule"],
                ["action", "role", "topic", "partition", "payload_bytes", "window_ms", "token_scope", "match"]);
            var action = String("action") ?? "";
            var roleName = String("role") ?? "";
            var roleValid = Enum.TryParse<KafkaClientRole>(roleName, out var role) && Enum.IsDefined(role) && role.ToString() == roleName;
            var topic = String("topic") ?? "";
            var partition = Integer("partition");
            var payload = Integer("payload_bytes");
            var window = Integer("window_ms");
            var scope = String("token_scope") ?? "";
            var match = String("match") ?? "";
            var hasTerminalRule = row.Fields.TryGetValue("terminal_rule", out var terminalRuleNode);
            var terminalRule = terminalRuleNode is ScalarNode terminalScalar ? terminalScalar.Value : "standard";
            if (expected.TryGetValue(id, out var literal))
            {
                Check("action", action == literal.Action);
                Check("role", roleValid && role == literal.Role && definition.Roles.Contains(role));
                Check("topic", topic == literal.Topic);
                Check("partition", partition == literal.Partition);
                Check("payload", payload == literal.PayloadBytes);
                Check("window", window == literal.WindowMs);
                Check("token_scope", scope == literal.TokenScope);
                Check("match", match == literal.Match);
                Check("terminal_rule", literal.TerminalRule == "standard"
                    ? !hasTerminalRule
                    : hasTerminalRule && terminalRuleNode is ScalarNode && terminalRule == literal.TerminalRule);
            }
            result[id] = new(action, role, topic, partition, payload, window, scope, match, terminalRule);
            continue;

            string? String(string key)
            {
                if (row.Fields.GetValueOrDefault(key) is ScalarNode scalar &&
                    !(scalar.IsPlain && (scalar.Value.Length == 0 || scalar.Value is "~" or "null" or "Null" or "NULL"))) return scalar.Value;
                validation.Error(row.Fields.GetValueOrDefault(key), path + "." + key, "scalar_required", "Expected a non-null scalar.");
                return null;
            }
            int Integer(string key)
            {
                if (row.Fields.GetValueOrDefault(key) is ScalarNode { IsPlain: true } scalar &&
                    scalar.Value.Length != 0 && scalar.Value.All(char.IsAsciiDigit) &&
                    int.TryParse(scalar.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) return value;
                validation.Error(row.Fields.GetValueOrDefault(key), path + "." + key, "int32_required", "Expected a plain decimal Int32.");
                return 0;
            }
            void Check(string field, bool valid)
            {
                if (!valid) validation.Error(input, path + "." + field, "single_action_" + field, "The field must equal the fixed experiment input.");
            }
        }
        if (definition.Repeats != 5) Error("repeats");
        if (definition.Fault.Live.Kind != LiveFaultKind.None || definition.Fault.Component.Kind != ComponentFaultKind.None) Error("fault");
        if (definition.Gate is not NoGate) Error("gate");
        var roles = expected.Values.Select(row => row.Role).Distinct().ToHashSet();
        if (!roles.SetEquals(definition.Roles) || definition.Measures.Length != roles.Count * 2 ||
            roles.Any(role => definition.Measures.Count(measure => measure.Role == role && measure.Id == "single_action.outcome") != 1 ||
                definition.Measures.Count(measure => measure.Role == role && measure.Id == "single_action.client_errors") != 1)) Error("measures");
        if (definition.Configurations.Any(configuration => !Deltas(definition.Id, configuration.Key).OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(configuration.Value.Overrides.SelectMany(role => role.Value.Select(setting => $"{role.Key}:{setting.Key}={setting.Value.Value}")).OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))) Error("deltas");
        return new(result.ToImmutable());

        void Error(string check) => validation.Error(node, "single_action", "single_action_" + check, "The declaration must equal the fixed experiment contract.");
    }

    private static ImmutableDictionary<string, SingleActionConfiguration> Rows(string id)
    {
        var rows = ImmutableDictionary.CreateBuilder<string, SingleActionConfiguration>(StringComparer.Ordinal);
        switch (id)
        {
            case "B01": Add("C0", "produce_one", KafkaClientRole.LoadGenProducer, 1000); Add("C1", "produce_one", KafkaClientRole.LoadGenProducer, 1100000); break;
            case "B03": foreach (var c in new[] { "C1", "C2", "C3", "C4" }) Add(c, "produce_one", KafkaClientRole.LoadGenProducer, 1000); break;
            case "B05": Add("C1", "init_transactions", KafkaClientRole.LoadGenProducer, 0); break;
            case "C08": Add("C0", "subscribe", KafkaClientRole.EnrichmentConsumer, 0); Add("C1", "subscribe", KafkaClientRole.EnrichmentConsumer, 0, terminalRule: "window_end_no_outcome"); break;
            case "C12": Add("C1", "subscribe", KafkaClientRole.EnrichmentConsumer, 0); Add("C2", "subscribe", KafkaClientRole.EnrichmentConsumer, 0); break;
            case "F04":
                Add("C0", "produce_one", KafkaClientRole.LoadGenProducer, 1000, match: "f04_seed");
                Add("C1", "produce_one", KafkaClientRole.LoadGenProducer, 1000, topic: "document-state");
                Add("C2", "assign_consume", KafkaClientRole.VerifierConsumer, 1000, topic: "document-state", match: "f04_seed", terminalRule: "window_end_no_outcome");
                Add("C3", "produce_one", KafkaClientRole.LoadGenProducer, 1000, scope: "storage", terminalRule: "first_error_callback_or_delivery");
                Add("C4", "assign_consume", KafkaClientRole.VerifierConsumer, 1000, match: "f04_seed"); break;
        }
        return rows.ToImmutable();
        void Add(string configuration, string action, KafkaClientRole role, int payload, string topic = "document-events", string scope = "namespace", string match = "none", string terminalRule = "standard") =>
            rows.Add(configuration, new(action, role, topic, 0, payload, action == "produce_one" ? 330000 : action == "init_transactions" ? 150000 : 120000, scope, match, terminalRule));
    }

    private static string[] Deltas(string item, string configuration) => (item, configuration) switch
    {
        ("B01", _) => ["LoadGenProducer:message.max.bytes=2000000"],
        ("B03", "C1") => ["LoadGenProducer:compression.type=gzip"],
        ("B03", "C2") => ["LoadGenProducer:compression.type=snappy"],
        ("B03", "C3") => ["LoadGenProducer:compression.type=lz4"],
        ("B03", "C4") => ["LoadGenProducer:compression.type=zstd"],
        ("B05", "C1") => ["LoadGenProducer:transactional.id=b05-probe", "LoadGenProducer:enable.idempotence=true", "LoadGenProducer:max.in.flight=5", "LoadGenProducer:message.timeout.ms=60000"],
        ("C08", "C1") => ["EnrichmentConsumer:group.protocol=consumer", "EnrichmentConsumer:session.timeout.ms=omit", "EnrichmentConsumer:heartbeat.interval.ms=omit", "EnrichmentConsumer:partition.assignment.strategy=omit", "EnrichmentConsumer:debug=cgrp"],
        ("F04", "C2") => ["VerifierConsumer:debug=topic,fetch,metadata"],
        ("C12", "C1") => ["EnrichmentConsumer:group.id=c12-" + new string('g', 252)],
        ("C12", "C2") => ["EnrichmentConsumer:group.id=c12-" + new string('g', 253)],
        _ => []
    };
}
