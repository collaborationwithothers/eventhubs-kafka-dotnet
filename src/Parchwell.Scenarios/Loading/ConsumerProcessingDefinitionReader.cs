using System.Collections.Immutable;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios;

/// <summary>Checks the closed consumer-processing declaration owned by scenario-contract.md, "Consumer processing declarations".</summary>
internal static class ConsumerProcessingDefinitionReader
{
    private const string Root = "consumer_processing";
    private const int WindowMs = 360000;
    private const int CompletionDeadlineMs = 30000;
    private const int SlowOrdinal = 100;
    private const int SlowDurationMs = 90000;
    private const string PollIntervalOverride = "EnrichmentConsumer:max.poll.interval.ms=60000";

    internal static ConsumerProcessingDefinition? Read(NodeValidation validation, InputNode? node, ScenarioDefinition definition, bool combined)
    {
        // The fixed rows of the items that declare a consumer experiment; #510 adds C10 and C01.
        var modes = Modes(definition.Id);
        if (node is null)
        {
            if (modes.Count != 0)
                validation.Error(definition.Source, Root, "consumer_processing_required", "This item requires its consumer_processing declaration.");
            return null;
        }
        if (modes.Count == 0)
        {
            validation.Error(node, Root, "consumer_processing_unexpected", "This item cannot declare consumer_processing.");
            return null;
        }
        if (combined)
        {
            validation.Error(node, Root, "consumer_processing_combined", "Consumer processing cannot be combined with another probe generation.");
            return null;
        }
        if (node is not MapNode map || map.Fields.Count != 3 ||
            !map.Fields.TryGetValue("window_ms", out var windowNode) || !map.Fields.TryGetValue("completion_deadline_ms", out var deadlineNode) ||
            !map.Fields.TryGetValue("configurations", out var configurationsNode))
        {
            validation.Error(node, Root, "consumer_processing_shape", "Expected exactly the window_ms, completion_deadline_ms and configurations fields.");
            return null;
        }
        if (windowNode is not ScalarNode { IsPlain: true, Value: "360000" })
            validation.Error(windowNode, Root + ".window_ms", "consumer_processing_literal", "The window must be 360000 milliseconds.");
        if (deadlineNode is not ScalarNode { IsPlain: true, Value: "30000" })
            validation.Error(deadlineNode, Root + ".completion_deadline_ms", "consumer_processing_literal", "The completion deadline must be 30000 milliseconds.");
        if (configurationsNode is not MapNode configurationsMap ||
            !configurationsMap.Fields.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(definition.Configurations.Keys))
        {
            validation.Error(configurationsNode, Root + ".configurations", "consumer_processing_configurations", "The configurations must equal the scenario's configurations.");
            return null;
        }
        var slowMessages = ImmutableDictionary.CreateBuilder<string, ConsumerSlowMessageDeclaration>(StringComparer.Ordinal);
        foreach (var (id, entry) in configurationsMap.Fields)
        {
            var path = Root + ".configurations." + id;
            if (entry is not MapNode entryMap || entryMap.Fields.Count != 1 || !entryMap.Fields.TryGetValue("slow_message", out var slowNode) ||
                slowNode is not MapNode slowMap || slowMap.Fields.Count != 3 ||
                !slowMap.Fields.TryGetValue("ordinal", out var ordinalNode) || !slowMap.Fields.TryGetValue("duration_ms", out var durationNode) ||
                !slowMap.Fields.TryGetValue("mode", out var modeNode))
            {
                validation.Error(entry, path, "consumer_processing_shape", "Expected exactly one slow_message with the ordinal, duration_ms and mode fields.");
                continue;
            }
            var expectedMode = modes.GetValueOrDefault(id);
            if (ordinalNode is not ScalarNode { IsPlain: true, Value: "100" })
                validation.Error(ordinalNode, path + ".slow_message.ordinal", "consumer_processing_literal", "The slow message ordinal must be 100.");
            if (durationNode is not ScalarNode { IsPlain: true, Value: "90000" })
                validation.Error(durationNode, path + ".slow_message.duration_ms", "consumer_processing_literal", "The slow message duration must be 90000 milliseconds.");
            if (modeNode is not ScalarNode { Value: var mode } || !StringComparer.Ordinal.Equals(mode, expectedMode))
                validation.Error(modeNode, path + ".slow_message.mode", "consumer_processing_literal", $"The slow message mode must be {expectedMode}.");
            slowMessages[id] = new ConsumerSlowMessageDeclaration(SlowOrdinal, SlowDurationMs, expectedMode ?? "");
        }
        if (definition.Repeats != 5 || definition.Fault.Live.Kind != LiveFaultKind.None ||
            definition.Fault.Component.Kind != ComponentFaultKind.None || definition.Gate is not NoGate ||
            !definition.Roles.SequenceEqual([KafkaClientRole.LoadGenProducer, KafkaClientRole.EnrichmentConsumer, KafkaClientRole.VerifierConsumer]) ||
            !MeasuresAreFixed(definition) || !OverridesAreFixed(definition))
            validation.Error(node, Root, "consumer_processing_experiment", "Repeats, faults, gate, roles, measures and overrides must equal the fixed experiment.");
        return new ConsumerProcessingDefinition(WindowMs, CompletionDeadlineMs, slowMessages.ToImmutable());
    }

    private static bool MeasuresAreFixed(ScenarioDefinition definition)
    {
        ImmutableArray<MeasurementRequest> expected =
        [
            new("ledger.delivery_error_code", KafkaClientRole.LoadGenProducer),
            new("client.fatal_error", KafkaClientRole.LoadGenProducer),
            new("verifier.ok", KafkaClientRole.VerifierConsumer),
            new("verifier.loss", KafkaClientRole.VerifierConsumer),
            new("verifier.phantom", KafkaClientRole.VerifierConsumer),
            new("verifier.reported_failure", KafkaClientRole.VerifierConsumer),
            new("verifier.duplicate", KafkaClientRole.VerifierConsumer),
            new("verifier.reorder", KafkaClientRole.VerifierConsumer)
        ];
        return definition.Measures.SequenceEqual(expected);
    }

    private static bool OverridesAreFixed(ScenarioDefinition definition) =>
        definition.Configurations.Values.All(configuration =>
            configuration.Overrides.SelectMany(role => role.Value.Select(setting => $"{role.Key}:{setting.Key}={setting.Value.Value}"))
                .SequenceEqual([PollIntervalOverride], StringComparer.Ordinal));

    // The slow-message mode each configuration of an item that declares consumer_processing must use.
    private static ImmutableDictionary<string, string> Modes(string item)
    {
        var rows = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        if (item == "C03")
        {
            rows.Add("C0", "inline");
            rows.Add("C1", "pause");
        }
        return rows.ToImmutable();
    }
}
