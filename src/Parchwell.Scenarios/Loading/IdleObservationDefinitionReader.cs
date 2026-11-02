using System.Collections.Immutable;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios;

/// <summary>Checks the closed idle-observation declarations owned by scenario-contract.md, "Idle observation declarations".</summary>
internal static class IdleObservationDefinitionReader
{
    private const string Action = "produce";
    private const int IdleSeconds = 420;
    private const int StatisticsIntervalMs = 15000;
    private const int MaxStatisticsReports = 80;

    internal static IdleObservationDefinition? Read(NodeValidation validation, InputNode? node, ScenarioDefinition definition)
    {
        var configurations = Configurations(definition.Id);
        if (node is null)
        {
            if (configurations.Count != 0)
                validation.Error(definition.Source, "idle_observation", "idle_observation_required", "This item requires its idle-observation declaration.");
            return null;
        }
        if (configurations.Count == 0)
        {
            validation.Error(node, "idle_observation", "idle_observation_unexpected", "This item cannot declare idle_observation.");
            return null;
        }
        if (node is not MapNode map || map.Fields.Count != 5 ||
            !map.Fields.TryGetValue("action", out var actionNode) || !map.Fields.TryGetValue("role", out var roleNode) ||
            !map.Fields.TryGetValue("idle_seconds", out var idleNode) || !map.Fields.TryGetValue("statistics_interval_ms", out var intervalNode) ||
            !map.Fields.TryGetValue("max_statistics_reports", out var reportsNode))
        {
            validation.Error(node, "idle_observation", "idle_observation_shape", "Expected exactly the action, role, idle_seconds, statistics_interval_ms and max_statistics_reports fields.");
            return null;
        }
        if (actionNode is not ScalarNode { Value: Action })
            validation.Error(actionNode, "idle_observation.action", "idle_observation_literal", "The action must be produce.");
        if (roleNode is not ScalarNode { Value: nameof(KafkaClientRole.LoadGenProducer) })
            validation.Error(roleNode, "idle_observation.role", "idle_observation_literal", "The role must be LoadGenProducer.");
        if (idleNode is not ScalarNode { IsPlain: true, Value: "420" })
            validation.Error(idleNode, "idle_observation.idle_seconds", "idle_observation_literal", "The idle period must be 420 seconds.");
        if (intervalNode is not ScalarNode { IsPlain: true, Value: "15000" })
            validation.Error(intervalNode, "idle_observation.statistics_interval_ms", "idle_observation_literal", "The statistics interval must be 15000 milliseconds.");
        if (reportsNode is not ScalarNode { IsPlain: true, Value: "80" })
            validation.Error(reportsNode, "idle_observation.max_statistics_reports", "idle_observation_literal", "The statistics report cap must be 80.");
        if (definition.Repeats != 5 || definition.Fault.Live.Kind != LiveFaultKind.None ||
            definition.Fault.Component.Kind != ComponentFaultKind.None || definition.Gate is not NoGate ||
            definition.Roles.Length != 1 || definition.Roles[0] != KafkaClientRole.LoadGenProducer || definition.Measures.Length != 2 ||
            !definition.Measures.Contains(new MeasurementRequest("single_action.outcome", KafkaClientRole.LoadGenProducer)) ||
            !definition.Measures.Contains(new MeasurementRequest("single_action.client_errors", KafkaClientRole.LoadGenProducer)))
            validation.Error(node, "idle_observation", "idle_observation_experiment", "Repeats, faults, gate, roles and measures must equal the fixed experiment.");
        if (!configurations.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(definition.Configurations.Keys))
            validation.Error(node, "configurations", "idle_observation_configurations", "Configurations must equal the fixed experiment.");
        foreach (var (id, configuration) in definition.Configurations)
        {
            ImmutableArray<string> expected = configurations.TryGetValue(id, out var row) ? row : [];
            var actual = configuration.Overrides.SelectMany(role => role.Value.Select(setting => $"{role.Key}:{setting.Key}={setting.Value.Value}"));
            if (!expected.Order(StringComparer.Ordinal).SequenceEqual(actual.Order(StringComparer.Ordinal), StringComparer.Ordinal))
                validation.Error(node, "configurations." + id, "idle_observation_deltas", "Only the fixed idle-observation deltas are permitted.");
        }
        return new(Action, KafkaClientRole.LoadGenProducer, IdleSeconds, StatisticsIntervalMs, MaxStatisticsReports);
    }

    // The configurations fixed by the #301 decisions; C0 is the librdkafka default in each item.
    private static ImmutableDictionary<string, ImmutableArray<string>> Configurations(string item)
    {
        var rows = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(StringComparer.Ordinal);
        switch (item)
        {
            case "A02":
                rows.Add("C0", ["LoadGenProducer:socket.keepalive.enable=false"]);
                rows.Add("C1", []);
                break;
            case "A03":
                rows.Add("C0", ["LoadGenProducer:metadata.max.age.ms=900000"]);
                rows.Add("C1", []);
                rows.Add("C2", ["LoadGenProducer:topic.metadata.refresh.interval.ms=180000"]);
                break;
            case "A04":
                rows.Add("C0", ["LoadGenProducer:connections.max.idle.ms=omit"]);
                rows.Add("C1", ["LoadGenProducer:connections.max.idle.ms=180000"]);
                break;
        }
        return rows.ToImmutable();
    }
}
