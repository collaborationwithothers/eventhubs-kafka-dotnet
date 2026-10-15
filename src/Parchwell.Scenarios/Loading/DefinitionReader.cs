using System.Collections.Immutable;
using System.Globalization;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Scenarios;

internal static class DefinitionReader
{
    // Private copy of Parchwell.Kafka.Configuration.FrozenInventory, whose source is docs/blueprint.md section 12.
    private static readonly ImmutableHashSet<string> InventoryIds = new[]
    {
        "A01", "A02", "A03", "A04", "A05", "A06", "B01", "B02", "B03", "B04",
        "B05", "B06", "B07", "B08", "B09", "B10", "B11", "C01", "C02", "C03",
        "C04", "C05", "C06", "C07", "C08", "C09", "C10", "C11", "C12", "C13",
        "C14", "C15", "C16", "C17", "C18", "C19", "D01", "D02", "D03", "D04",
        "D05", "E01", "E02", "E03", "E04", "E05", "E06", "F01", "F02", "F03",
        "F04", "F05", "F06", "G01", "G02", "G03", "G04", "G05", "H01", "H02",
        "H03", "H04", "H05", "H06", "H07", "H08", "K01", "K02", "K03", "K04",
        "K05", "K06", "K07", "K08", "K09", "K10"
    }.ToImmutableHashSet(StringComparer.Ordinal);

    internal static bool IsFrozenInventoryId(string id) => InventoryIds.Contains(id);

    internal static ScenarioDefinition Read(InputNode root, string source, string requestedId)
    {
        var validation = new NodeValidation(source);
        var rootMap = validation.Map(root, "");
        if (rootMap is null)
        {
            validation.ThrowIfErrors();
            throw new InvalidOperationException("Unreachable");
        }
        validation.CheckFields(rootMap, "", "schema_version", "id", "baseline", "roles", "profile", "configurations", "fault", "repeats", "gate", "measures");

        var version = ReadLiteralVersion(validation, rootMap, "schema_version", "");
        var id = validation.String(rootMap, "id", "") ?? "";
        if (!StringComparer.Ordinal.Equals(id, requestedId)) validation.Error(rootMap.Fields.GetValueOrDefault("id"), "id", "scenario_id_mismatch", "The definition id must match the requested inventory id.");
        if (!InventoryIds.Contains(requestedId)) validation.Error(rootMap, "id", "unknown_inventory_id", "The requested id is not in the frozen scenario inventory.");
        var baseline = validation.String(rootMap, "baseline", "") ?? "";
        if (!StringComparer.Ordinal.Equals(baseline, "Parchwell.Kafka")) validation.Error(rootMap.Fields.GetValueOrDefault("baseline"), "baseline", "unsupported_baseline", "The baseline must be Parchwell.Kafka.");

        var roles = ReadRoles(validation, rootMap.Fields.GetValueOrDefault("roles"));
        var profile = validation.String(rootMap, "profile", "") ?? "";
        if (!IsProfileReference(profile)) validation.Error(rootMap.Fields.GetValueOrDefault("profile"), "profile", "invalid_profile_reference", "Profile must be profiles/<slug>.yaml.");
        var configurations = ReadConfigurations(validation, rootMap.Fields.GetValueOrDefault("configurations"), roles);
        var fault = ReadFault(validation, rootMap.Fields.GetValueOrDefault("fault"), configurations.Keys.ToImmutableHashSet(StringComparer.Ordinal));
        var repeats = ReadPositiveInt32(validation, rootMap.Fields.GetValueOrDefault("repeats"), "repeats", ScenarioContract.MinimumRepeats);
        var measures = ReadMeasures(validation, rootMap.Fields.GetValueOrDefault("measures"), roles);
        var gate = ReadGate(validation, rootMap.Fields.GetValueOrDefault("gate"), configurations.Keys.ToImmutableHashSet(StringComparer.Ordinal), roles, measures);
        validation.ThrowIfErrors();
        return new ScenarioDefinition(version, id, baseline, roles, profile, configurations, fault, repeats, gate, measures, root.Location);
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
                if (!declaredRoles.Contains(role)) validation.Error(settingsNode, rolePath, "role_not_declared", "Configuration roles must be declared in roles.");
                var settings = validation.Map(settingsNode, rolePath);
                if (settings is null) continue;
                var values = ImmutableDictionary.CreateBuilder<string, SettingOverride>(StringComparer.Ordinal);
                foreach (var (key, settingNode) in settings.Fields)
                {
                    if (settingNode is not ScalarNode { IsPlain: true } scalar || !int.TryParse(scalar.Value,
                            NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ||
                        !IsSignedInteger(scalar.Value))
                    {
                        validation.Error(settingNode, NodeValidation.Join(rolePath, key), "int32_required", "Expected a plain decimal Int32 replacement.");
                        continue;
                    }
                    values[key] = new SettingOverride(number, settingNode.Location);
                }
                overrides[role] = values.ToImmutable();
            }
            result[id] = new ScenarioConfiguration(id, overrides.ToImmutable());
        }
        return result.ToImmutable();
    }

    private static FaultDefinition ReadFault(NodeValidation validation, InputNode? node, ImmutableHashSet<string> configIds)
    {
        var map = validation.Map(node, "fault");
        if (map is null) return new FaultDefinition(new LiveFault("none", []), new ComponentFault("none", []));
        validation.CheckFields(map, "fault", "live", "component");
        var live = ReadLiveFault(validation, map.Fields.GetValueOrDefault("live"), configIds);
        var component = ReadComponentFault(validation, map.Fields.GetValueOrDefault("component"), configIds);
        return new FaultDefinition(live, component);
    }

    private static LiveFault ReadLiveFault(NodeValidation validation, InputNode? node, ImmutableHashSet<string> configIds)
    {
        var map = validation.Map(node, "fault.live");
        if (map is null) return new LiveFault("none", []);
        var kind = validation.String(map, "kind", "fault.live") ?? "";
        if (kind == "none")
        {
            validation.CheckFields(map, "fault.live", "kind");
            return new LiveFault(kind, []);
        }
        validation.CheckFields(map, "fault.live", "kind", "configurations");
        if (kind != "throttling") validation.Error(map.Fields.GetValueOrDefault("kind"), "fault.live.kind", "unknown_fault_kind", "Live fault kind must be none or throttling.");
        var configs = ReadReferences(validation, map.Fields.GetValueOrDefault("configurations"), "fault.live.configurations", configIds);
        return new LiveFault(kind, configs);
    }

    private static ComponentFault ReadComponentFault(NodeValidation validation, InputNode? node, ImmutableHashSet<string> configIds)
    {
        var map = validation.Map(node, "fault.component");
        if (map is null) return new ComponentFault("none", []);
        var kind = validation.String(map, "kind", "fault.component") ?? "";
        if (kind == "none")
        {
            validation.CheckFields(map, "fault.component", "kind");
            return new ComponentFault(kind, []);
        }
        validation.CheckFields(map, "fault.component", "kind", "configurations", "direction", "latency_ms");
        if (kind != "toxiproxy_latency") validation.Error(map.Fields.GetValueOrDefault("kind"), "fault.component.kind", "unknown_fault_kind", "Component fault kind must be none or toxiproxy_latency.");
        var configs = ReadReferences(validation, map.Fields.GetValueOrDefault("configurations"), "fault.component.configurations", configIds);
        var direction = validation.String(map, "direction", "fault.component") ?? "";
        if (direction != "downstream") validation.Error(map.Fields.GetValueOrDefault("direction"), "fault.component.direction", "invalid_direction", "Component fault direction must be downstream.");
        var latency = ReadPositiveInt32(validation, map.Fields.GetValueOrDefault("latency_ms"), "fault.component.latency_ms");
        return new ComponentFault(kind, configs, direction, latency);
    }

    private static ProbeGate ReadGate(NodeValidation validation, InputNode? node, ImmutableHashSet<string> configIds,
        ImmutableArray<KafkaClientRole> declaredRoles, ImmutableArray<MeasurementRequest> measures)
    {
        var map = validation.Map(node, "gate");
        if (map is null) return new ProbeGate("none", null, null, null, null, null, []);
        var kind = validation.String(map, "kind", "gate") ?? "";
        if (kind == "none")
        {
            validation.CheckFields(map, "gate", "kind");
            return new ProbeGate(kind, null, null, null, null, null, []);
        }
        validation.CheckFields(map, "gate", "kind", "configuration", "repeats", "predicate", "configurations");
        if (kind != "probe") validation.Error(map.Fields.GetValueOrDefault("kind"), "gate.kind", "unknown_gate_kind", "Gate kind must be none or probe.");
        var configuration = validation.String(map, "configuration", "gate") ?? "";
        CheckReference(validation, map.Fields.GetValueOrDefault("configuration"), "gate.configuration", configuration, configIds);
        var repeats = ReadPositiveInt32(validation, map.Fields.GetValueOrDefault("repeats"), "gate.repeats", ScenarioContract.MinimumRepeats);
        var configs = ReadReferences(validation, map.Fields.GetValueOrDefault("configurations"), "gate.configurations", configIds);
        var predicate = ReadPredicate(validation, map.Fields.GetValueOrDefault("predicate"), declaredRoles);
        if (predicate is not null && !measures.Contains(predicate))
            validation.Error(map.Fields.GetValueOrDefault("predicate"), "gate.predicate", "predicate_measure_missing", "The probe predicate must also be requested in measures.");
        var threshold = ReadPositiveInt32(validation, map.Fields.GetValueOrDefault("predicate") is MapNode pred ? pred.Fields.GetValueOrDefault("threshold_ms") : null,
            "gate.predicate.threshold_ms");
        return new ProbeGate(kind, configuration, predicate, "greater_than", threshold, repeats, configs);
    }

    private static MeasurementRequest? ReadPredicate(NodeValidation validation, InputNode? node, ImmutableArray<KafkaClientRole> declaredRoles)
    {
        var map = validation.Map(node, "gate.predicate");
        if (map is null) return null;
        validation.CheckFields(map, "gate.predicate", "id", "role", "operator", "threshold_ms");
        var id = validation.String(map, "id", "gate.predicate") ?? "";
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

    private static bool IsProfileReference(string profile)
    {
        const string prefix = "profiles/";
        const string suffix = ".yaml";
        if (!profile.StartsWith(prefix, StringComparison.Ordinal) || !profile.EndsWith(suffix, StringComparison.Ordinal)) return false;
        var slug = profile[prefix.Length..^suffix.Length];
        return slug.Length > 0 && slug.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-') &&
            char.IsAsciiLetter(slug[0]) && !profile.Contains('\\') && !profile.Contains(':');
    }

    private static bool IsSignedInteger(string value) => value.Length > 0 &&
        (value[0] == '-' ? value.Length > 1 && value[1..].All(char.IsAsciiDigit) : value.All(char.IsAsciiDigit));
}
