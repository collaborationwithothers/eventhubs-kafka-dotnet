using System.Collections.Immutable;
using System.Globalization;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios;

/// <summary>
/// Validates the input tree of one scenario.yaml and builds a <see cref="ScenarioDefinition"/>.
/// </summary>
/// <remarks>
/// It checks structure and cross-references only: field shapes, declared roles, configuration ids and the
/// measurement catalogue. Whether an override key and value are allowed is checked by
/// <see cref="ScenarioResolver"/>. The rules are in docs/operations/scenario-contract.md, "Scenario schema".
/// </remarks>
internal static class DefinitionReader
{
    internal static ScenarioDefinition Read(InputNode root, string source, string requestedId)
    {
        var validation = new NodeValidation(source);
        var rootMap = validation.Map(root, "");
        if (rootMap is null)
        {
            validation.ThrowIfErrors();
            throw new InvalidOperationException("Unreachable");
        }
        validation.TryFields(rootMap, "",
            ["schema_version", "id", "baseline", "roles", "profile", "configurations", "fault", "repeats", "gate", "measures", "single_action", "probe_v2", "idle_observation"],
            ["schema_version", "id", "baseline", "roles", "profile", "configurations", "fault", "repeats", "gate", "measures"]);

        var version = ReadLiteralVersion(validation, rootMap, "schema_version", "");
        var id = validation.String(rootMap, "id", "") ?? "";
        // One definition file exists per inventory item (ADR-014). A file whose id differs from the
        // requested item would run the wrong experiment, so it is rejected.
        if (!StringComparer.Ordinal.Equals(id, requestedId)) validation.Error(rootMap.Fields.GetValueOrDefault("id"), "id", "scenario_id_mismatch", "The definition id must match the requested inventory id.");
        var baseline = validation.String(rootMap, "baseline", "") ?? "";
        // Every scenario is a delta from the one baseline in Parchwell.Kafka (ADR-015).
        if (!StringComparer.Ordinal.Equals(baseline, "Parchwell.Kafka")) validation.Error(rootMap.Fields.GetValueOrDefault("baseline"), "baseline", "unsupported_baseline", "The baseline must be Parchwell.Kafka.");

        var roles = ReadRoles(validation, rootMap.Fields.GetValueOrDefault("roles"));
        var profile = validation.String(rootMap, "profile", "") ?? "";
        // The loader opens this path next, so it is limited to profiles/<slug>.yaml. A definition then
        // cannot make the loader read a file outside scenarios/profiles/.
        if (!IsProfileReference(profile)) validation.Error(rootMap.Fields.GetValueOrDefault("profile"), "profile", "invalid_profile_reference", "Profile must be profiles/<slug>.yaml.");
        var configurations = ReadConfigurations(validation, rootMap.Fields.GetValueOrDefault("configurations"), roles);
        var fault = ReadFault(validation, rootMap.Fields.GetValueOrDefault("fault"), configurations.Keys.ToImmutableHashSet(StringComparer.Ordinal));
        // The minimum repeat count is a repository rule (docs/agents/factory.md, "Plan template"), not a measurement.
        var repeats = ReadPositiveInt32(validation, rootMap.Fields.GetValueOrDefault("repeats"), "repeats", ScenarioContract.MinimumRepeats);
        var measures = ReadMeasures(validation, rootMap.Fields.GetValueOrDefault("measures"), roles);
        var gate = ReadGate(validation, rootMap.Fields.GetValueOrDefault("gate"), configurations.Keys.ToImmutableHashSet(StringComparer.Ordinal), roles, measures);
        var definition = new ScenarioDefinition(version, id, baseline, roles, profile, configurations, fault, repeats, gate, measures, root.Location);
        var singleAction = SingleActionDefinitionReader.Read(validation, rootMap.Fields.GetValueOrDefault("single_action"), definition);
        if (rootMap.Fields.ContainsKey("single_action") && rootMap.Fields.TryGetValue("probe_v2", out var probeNode))
            validation.Error(probeNode, "probe_v2", "probe_v2_unexpected", "Probe generations cannot be combined.");
        var probeV2 = ProbeV2DefinitionReader.Read(validation, rootMap.Fields.GetValueOrDefault("probe_v2"), definition);
        var idleObservation = IdleObservationDefinitionReader.Read(validation, rootMap.Fields.GetValueOrDefault("idle_observation"), definition);
        if (rootMap.Fields.TryGetValue("idle_observation", out var idleNode) &&
            (rootMap.Fields.ContainsKey("single_action") || rootMap.Fields.ContainsKey("probe_v2")))
            validation.Error(idleNode, "idle_observation", "idle_observation_combined", "Idle observation cannot be combined with another probe generation.");
        validation.ThrowIfErrors();
        return definition with { SingleAction = singleAction, ProbeV2 = probeV2, IdleObservation = idleObservation };
    }

    private static int ReadLiteralVersion(NodeValidation validation, MapNode map, string key, string path)
    {
        if (!validation.TryInt32(map, key, path, true, out var version)) return 0;
        if (version != ScenarioContract.SchemaVersion)
            validation.Error(map.Fields.GetValueOrDefault(key), NodeValidation.Join(path, key), "unsupported_schema_version", $"Only schema_version {ScenarioContract.SchemaVersion} is supported.");
        return version;
    }

    private static int ReadPositiveInt32(NodeValidation validation, InputNode? node, string path, int minimum = 1)
    {
        if (!validation.TryInt32(node, path, true, out var value)) return 0;
        if (value < minimum)
            validation.Error(node, path, "below_minimum", $"The value must be at least {minimum.ToString(CultureInfo.InvariantCulture)}.");
        return value;
    }

    private static ImmutableArray<KafkaClientRole> ReadRoles(NodeValidation validation, InputNode? node)
    {
        var sequence = validation.Sequence(node, "roles");
        if (sequence is null) return [];
        var roles = ImmutableArray.CreateBuilder<KafkaClientRole>();
        var seen = new HashSet<KafkaClientRole>();
        for (var index = 0; index < sequence.Items.Length; index++)
        {
            var item = sequence.Items[index];
            var path = $"roles[{index}]";
            if (!ReadRole(validation, item, path, out var role)) continue;
            if (!seen.Add(role)) validation.Error(item, path, "duplicate_role", "Roles must be unique.");
            else roles.Add(role);
        }
        return roles.ToImmutable();
    }

    private static bool ReadRole(NodeValidation validation, InputNode? node, string path, out KafkaClientRole role)
    {
        role = default;
        if (node is not ScalarNode scalar)
        {
            validation.Error(node, path, "role_required", "Expected an exact KafkaClientRole name.");
            return false;
        }
        foreach (var name in Enum.GetNames<KafkaClientRole>())
        {
            if (!StringComparer.Ordinal.Equals(name, scalar.Value)) continue;
            role = Enum.Parse<KafkaClientRole>(name, ignoreCase: false);
            return true;
        }
        validation.Error(node, path, "unknown_role", "Expected an exact KafkaClientRole name.");
        return false;
    }

    private static ImmutableDictionary<string, ScenarioConfiguration> ReadConfigurations(NodeValidation validation, InputNode? node,
        ImmutableArray<KafkaClientRole> declaredRoles)
    {
        var map = validation.Map(node, "configurations");
        if (map is null) return ImmutableDictionary<string, ScenarioConfiguration>.Empty.WithComparers(StringComparer.Ordinal);
        if (map.Fields.Count == 0) validation.Error(map, "configurations", "empty_mapping", "At least one configuration is required.");
        var result = ImmutableDictionary.CreateBuilder<string, ScenarioConfiguration>(StringComparer.Ordinal);
        foreach (var (id, value) in map.Fields)
        {
            var path = NodeValidation.Join("configurations", id);
            if (!validation.IsSlug(id, value, path)) continue;
            var roleMap = validation.Map(value, path);
            if (roleMap is null) continue;
            var overrides = ImmutableDictionary.CreateBuilder<KafkaClientRole, ImmutableDictionary<string, SettingOverride>>();
            foreach (var (roleName, settingsNode) in roleMap.Fields)
            {
                var rolePath = NodeValidation.Join(path, roleName);
                if (!ReadRole(validation, new ScalarNode(roleName, true, settingsNode.Location), rolePath, out var role)) continue;
                // The resolver builds a baseline only for roles listed in roles, so an override for any other
                // role would have nothing to replace.
                if (!declaredRoles.Contains(role)) validation.Error(settingsNode, rolePath, "role_not_declared", "Configuration roles must be declared in roles.");
                var settings = validation.Map(settingsNode, rolePath);
                if (settings is null) continue;
                var values = ImmutableDictionary.CreateBuilder<string, SettingOverride>(StringComparer.Ordinal);
                foreach (var (key, settingNode) in settings.Fields)
                {
                    if (KafkaScenarioOverridePolicy.TryGetSetting(key, out var policy))
                    {
                        if (policy.ValueKind == KafkaScenarioOverrideValueKind.String && settingNode is ScalarNode text &&
                            !(text.IsPlain && (text.Value.Length == 0 || text.Value is "~" or "null" or "Null" or "NULL")))
                        {
                            values[key] = new SettingOverride(text.Value, settingNode.Location);
                            continue;
                        }
                        if ((policy.ValueKind == KafkaScenarioOverrideValueKind.Omission || !policy.OmissionInventoryIds.IsEmpty) &&
                            settingNode is ScalarNode { IsPlain: true, Value: "omit" })
                        {
                            values[key] = new SettingOverride("omit", settingNode.Location);
                            continue;
                        }
                    }
                    // Only the form of the value is checked here. The resolver decides whether the key, role and range
                    // are allowed.
                    if (settingNode is ScalarNode { IsPlain: true, Value: "true" or "false" } literal)
                    {
                        values[key] = new SettingOverride(literal.Value, settingNode.Location);
                        continue;
                    }
                    if (settingNode is not ScalarNode { IsPlain: true } scalar || !int.TryParse(scalar.Value,
                            NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ||
                        !IsSignedInteger(scalar.Value))
                    {
                        validation.Error(settingNode, NodeValidation.Join(rolePath, key), "int32_required", "Expected a plain decimal Int32 or a plain true or false replacement.");
                        continue;
                    }
                    values[key] = new SettingOverride(number.ToString(CultureInfo.InvariantCulture), settingNode.Location);
                }
                overrides[role] = values.ToImmutable();
            }
            result[id] = new ScenarioConfiguration(id, overrides.ToImmutable());
        }
        return result.ToImmutable();
    }

    // Each fault part and the gate is either exactly {kind: none} or a full declaration of its one
    // supported kind, so a stray field next to none is an error rather than silently ignored.
    private static FaultDefinition ReadFault(NodeValidation validation, InputNode? node, ImmutableHashSet<string> configIds)
    {
        var map = validation.Map(node, "fault");
        if (map is null) return new FaultDefinition(new LiveFault(LiveFaultKind.None, []), new ComponentFault(ComponentFaultKind.None, []));
        validation.CheckFields(map, "fault", "live", "component");
        var live = ReadLiveFault(validation, map.Fields.GetValueOrDefault("live"), configIds);
        var component = ReadComponentFault(validation, map.Fields.GetValueOrDefault("component"), configIds);
        return new FaultDefinition(live, component);
    }

    private static LiveFault ReadLiveFault(NodeValidation validation, InputNode? node, ImmutableHashSet<string> configIds)
    {
        var map = validation.Map(node, "fault.live");
        if (map is null) return new LiveFault(LiveFaultKind.None, []);
        var kindName = validation.String(map, "kind", "fault.live") ?? "";
        if (kindName == "none")
        {
            validation.CheckFields(map, "fault.live", "kind");
            return new LiveFault(LiveFaultKind.None, []);
        }
        validation.CheckFields(map, "fault.live", "kind", "configurations");
        if (kindName != "throttling") validation.Error(map.Fields.GetValueOrDefault("kind"), "fault.live.kind", "unknown_fault_kind", "Live fault kind must be none or throttling.");
        var configs = ReadReferences(validation, map.Fields.GetValueOrDefault("configurations"), "fault.live.configurations", configIds);
        return new LiveFault(LiveFaultKind.Throttling, configs);
    }

    private static ComponentFault ReadComponentFault(NodeValidation validation, InputNode? node, ImmutableHashSet<string> configIds)
    {
        var map = validation.Map(node, "fault.component");
        if (map is null) return new ComponentFault(ComponentFaultKind.None, []);
        var kindName = validation.String(map, "kind", "fault.component") ?? "";
        if (kindName == "none")
        {
            validation.CheckFields(map, "fault.component", "kind");
            return new ComponentFault(ComponentFaultKind.None, []);
        }
        validation.CheckFields(map, "fault.component", "kind", "configurations", "direction", "latency_ms");
        if (kindName != "toxiproxy_latency") validation.Error(map.Fields.GetValueOrDefault("kind"), "fault.component.kind", "unknown_fault_kind", "Component fault kind must be none or toxiproxy_latency.");
        var configs = ReadReferences(validation, map.Fields.GetValueOrDefault("configurations"), "fault.component.configurations", configIds);
        var direction = validation.String(map, "direction", "fault.component") ?? "";
        if (direction != "downstream") validation.Error(map.Fields.GetValueOrDefault("direction"), "fault.component.direction", "invalid_direction", "Component fault direction must be downstream.");
        var latency = ReadPositiveInt32(validation, map.Fields.GetValueOrDefault("latency_ms"), "fault.component.latency_ms");
        return new ComponentFault(ComponentFaultKind.ToxiproxyLatency, configs, direction, latency);
    }

    private static ScenarioGate ReadGate(NodeValidation validation, InputNode? node, ImmutableHashSet<string> configIds,
        ImmutableArray<KafkaClientRole> declaredRoles, ImmutableArray<MeasurementRequest> measures)
    {
        var map = validation.Map(node, "gate");
        if (map is null) return new NoGate();
        var kindName = validation.String(map, "kind", "gate") ?? "";
        if (kindName == "none")
        {
            validation.CheckFields(map, "gate", "kind");
            return new NoGate();
        }
        validation.CheckFields(map, "gate", "kind", "configuration", "repeats", "predicate", "configurations");
        if (kindName != "probe") validation.Error(map.Fields.GetValueOrDefault("kind"), "gate.kind", "unknown_gate_kind", "Gate kind must be none or probe.");
        var configuration = validation.String(map, "configuration", "gate") ?? "";
        CheckReference(validation, map.Fields.GetValueOrDefault("configuration"), "gate.configuration", configuration, configIds);
        // The probe's repeat count obeys the same minimum as full runs, independently of them.
        var repeats = ReadPositiveInt32(validation, map.Fields.GetValueOrDefault("repeats"), "gate.repeats", ScenarioContract.MinimumRepeats);
        var configs = ReadReferences(validation, map.Fields.GetValueOrDefault("configurations"), "gate.configurations", configIds);
        var predicate = ReadPredicate(validation, map.Fields.GetValueOrDefault("predicate"), declaredRoles, out var probeOperator);
        // The probe is judged on this measurement, so the scenario must also request it in measures.
        if (predicate is not null && !measures.Contains(predicate))
            validation.Error(map.Fields.GetValueOrDefault("predicate"), "gate.predicate", "predicate_measure_missing", "The probe predicate must also be requested in measures.");
        var threshold = ReadPositiveInt32(validation, map.Fields.GetValueOrDefault("predicate") is MapNode pred ? pred.Fields.GetValueOrDefault("threshold_ms") : null,
            "gate.predicate.threshold_ms");
        // A null predicate means an error was already recorded; ThrowIfErrors in Read stops this placeholder.
        return predicate is null ? new NoGate() :
            new ProbeGate(configuration, predicate, probeOperator, threshold, repeats, configs);
    }

    private static MeasurementRequest? ReadPredicate(NodeValidation validation, InputNode? node, ImmutableArray<KafkaClientRole> declaredRoles,
        out ProbeOperator probeOperator)
    {
        probeOperator = ProbeOperator.GreaterThan;
        var map = validation.Map(node, "gate.predicate");
        if (map is null) return null;
        validation.CheckFields(map, "gate.predicate", "id", "role", "operator", "threshold_ms");
        var id = validation.String(map, "id", "gate.predicate") ?? "";
        // The contract fixes the only probe predicate: stats.broker.throttle.max, greater_than a positive
        // threshold (docs/operations/scenario-contract.md, "Scenario schema").
        if (id != "stats.broker.throttle.max") validation.Error(map.Fields.GetValueOrDefault("id"), "gate.predicate.id", "invalid_predicate_measure", "Probe predicate id must be stats.broker.throttle.max.");
        var role = ReadRoleField(validation, map, "role", "gate.predicate", out var validRole);
        if (validRole && !declaredRoles.Contains(role)) validation.Error(map.Fields.GetValueOrDefault("role"), "gate.predicate.role", "role_not_declared", "The predicate role must be declared in roles.");
        if (!MeasurementCatalog.TryGet(id, out var descriptor) || (validRole && descriptor is not null && !descriptor.Roles.Contains(role)))
            validation.Error(map.Fields.GetValueOrDefault("role"), "gate.predicate.role", "measurement_role_mismatch", "The predicate measurement is not applicable to this role.");
        var op = validation.String(map, "operator", "gate.predicate") ?? "";
        if (op != "greater_than") validation.Error(map.Fields.GetValueOrDefault("operator"), "gate.predicate.operator", "invalid_predicate_operator", "Probe predicate operator must be greater_than.");
        return validRole ? new MeasurementRequest(id, role) : null;
    }

    private static ImmutableArray<MeasurementRequest> ReadMeasures(NodeValidation validation, InputNode? node,
        ImmutableArray<KafkaClientRole> declaredRoles)
    {
        var sequence = validation.Sequence(node, "measures");
        if (sequence is null) return [];
        var result = ImmutableArray.CreateBuilder<MeasurementRequest>();
        var seen = new HashSet<(string, KafkaClientRole)>();
        for (var i = 0; i < sequence.Items.Length; i++)
        {
            var path = $"measures[{i}]";
            var map = validation.Map(sequence.Items[i], path);
            if (map is null) continue;
            validation.CheckFields(map, path, "id", "role");
            var id = validation.String(map, "id", path) ?? "";
            var role = ReadRoleField(validation, map, "role", path, out var validRole);
            // A measure must name a catalogued measurement, for a role the entry applies to and the scenario declares.
            if (!MeasurementCatalog.TryGet(id, out var descriptor))
                validation.Error(map.Fields.GetValueOrDefault("id"), NodeValidation.Join(path, "id"), "unknown_measurement", "Measurement id is not in the declaration catalogue.");
            else if (validRole && descriptor is not null && !descriptor.Roles.Contains(role))
                validation.Error(map.Fields.GetValueOrDefault("role"), NodeValidation.Join(path, "role"), "measurement_role_mismatch", "Measurement is not applicable to this role.");
            if (validRole && !declaredRoles.Contains(role)) validation.Error(map.Fields.GetValueOrDefault("role"), NodeValidation.Join(path, "role"), "role_not_declared", "Measurement role must be declared in roles.");
            if (validRole)
            {
                if (!seen.Add((id, role))) validation.Error(sequence.Items[i], path, "duplicate_measurement", "Measurement requests must be unique by id and role.");
                else result.Add(new MeasurementRequest(id, role));
            }
        }
        return result.ToImmutable();
    }

    private static KafkaClientRole ReadRoleField(NodeValidation validation, MapNode map, string key, string path, out bool valid)
    {
        valid = ReadRole(validation, map.Fields.GetValueOrDefault(key), NodeValidation.Join(path, key), out var role);
        return role;
    }

    private static ImmutableArray<string> ReadReferences(NodeValidation validation, InputNode? node, string path, ImmutableHashSet<string> configIds)
    {
        var sequence = validation.Sequence(node, path);
        if (sequence is null) return [];
        var values = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < sequence.Items.Length; i++)
        {
            var item = sequence.Items[i];
            if (item is not ScalarNode scalar)
            {
                validation.Error(item, $"{path}[{i}]", "plain_scalar_required", "Expected a plain configuration identifier.");
                continue;
            }
            if (!seen.Add(scalar.Value)) validation.Error(item, $"{path}[{i}]", "duplicate_reference", "Configuration references must be unique.");
            CheckReference(validation, item, $"{path}[{i}]", scalar.Value, configIds);
            values.Add(scalar.Value);
        }
        return values.ToImmutable();
    }

    private static void CheckReference(NodeValidation validation, InputNode? node, string path, string reference, ImmutableHashSet<string> configIds)
    {
        if (!configIds.Contains(reference)) validation.Error(node, path, "unknown_configuration", $"Configuration '{reference}' is not declared.");
    }

    // The contract forbids absolute paths, URLs, backslashes and parent traversal in profile references.
    // The slug allows no dot, slash, backslash or colon, so none of those can pass.
    private static bool IsProfileReference(string profile)
    {
        const string prefix = "profiles/";
        const string suffix = ".yaml";
        if (!profile.StartsWith(prefix, StringComparison.Ordinal) || !profile.EndsWith(suffix, StringComparison.Ordinal)) return false;
        var slug = profile[prefix.Length..^suffix.Length];
        return slug.Length > 0 && slug.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-') &&
            char.IsAsciiLetter(slug[0]) && !profile.Contains('\\') && !profile.Contains(':');
    }

    // int.TryParse with AllowLeadingSign would also accept a leading plus sign; this keeps the form to an
    // optional minus and ASCII digits.
    private static bool IsSignedInteger(string value) => value.Length > 0 &&
        (value[0] == '-' ? value.Length > 1 && value[1..].All(char.IsAsciiDigit) : value.All(char.IsAsciiDigit));
}
