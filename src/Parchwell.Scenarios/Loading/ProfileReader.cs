using System.Collections.Immutable;
using System.Globalization;

namespace Parchwell.Scenarios;

internal static class ProfileReader
{
    private static readonly string[] PayloadNames = ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];

    internal static WorkloadProfile Read(InputNode root, string source, string expectedSlug)
    {
        var validation = new NodeValidation(source);
        var map = validation.Map(root, "");
        if (map is null)
        {
            validation.ThrowIfErrors();
            throw new InvalidOperationException("Unreachable");
        }
        validation.CheckFields(map, "", "schema_version", "id", "seed", "tenants", "documents_per_burst", "payloads", "stages");
        var version = ReadVersion(validation, map);
        var id = validation.String(map, "id", "") ?? "";
        validation.IsSlug(id, map.Fields.GetValueOrDefault("id"), "id");
        if (!StringComparer.Ordinal.Equals(id, expectedSlug))
            validation.Error(map.Fields.GetValueOrDefault("id"), "id", "profile_id_mismatch", "Profile id must match the referenced filename slug.");
        var seed = ReadSeed(validation, map.Fields.GetValueOrDefault("seed"));
        var tenants = ReadTenants(validation, map.Fields.GetValueOrDefault("tenants"));
        CheckWeightSum(validation, tenants.Select(static tenant => tenant.Weight), "tenants");
        var documents = ReadPositive(validation, map.Fields.GetValueOrDefault("documents_per_burst"), "documents_per_burst");
        var payloads = ReadPayloads(validation, map.Fields.GetValueOrDefault("payloads"));
        foreach (var (name, buckets) in payloads) CheckWeightSum(validation, buckets.Select(static bucket => bucket.Weight), "payloads." + name);
        var stages = ReadStages(validation, map.Fields.GetValueOrDefault("stages"));
        CheckStageTotals(validation, stages);
        validation.ThrowIfErrors();
        return new WorkloadProfile(version, id, seed, tenants, documents, payloads, stages, root.Location);
    }

    private static int ReadVersion(NodeValidation validation, MapNode map)
    {
        if (!validation.TryInt32(map, "schema_version", "", true, out var version)) return 0;
        if (version != ScenarioContract.SchemaVersion)
            validation.Error(map.Fields.GetValueOrDefault("schema_version"), "schema_version", "unsupported_schema_version", $"Only schema_version {ScenarioContract.SchemaVersion} is supported.");
        return version;
    }

    private static int ReadSeed(NodeValidation validation, InputNode? node)
    {
        if (node is not ScalarNode { IsPlain: true } scalar)
        {
            validation.Error(node, "seed", "plain_integer_required", "Seed must be a plain signed decimal Int32.");
            return 0;
        }
        if (!IsSignedInteger(scalar.Value) || !int.TryParse(scalar.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var seed))
        {
            validation.Error(node, "seed", "int32_required", "Seed must be a plain signed decimal Int32.");
            return 0;
        }
        return seed;
    }

    private static ImmutableArray<TenantWeight> ReadTenants(NodeValidation validation, InputNode? node)
    {
        var sequence = validation.Sequence(node, "tenants");
        if (sequence is null) return [];
        var result = ImmutableArray.CreateBuilder<TenantWeight>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < sequence.Items.Length; i++)
        {
            var path = $"tenants[{i}]";
            var map = validation.Map(sequence.Items[i], path);
            if (map is null) continue;
            validation.CheckFields(map, path, "id", "weight");
            var id = validation.String(map, "id", path) ?? "";
            validation.IsSlug(id, map.Fields.GetValueOrDefault("id"), path + ".id");
            if (!ids.Add(id)) validation.Error(map.Fields.GetValueOrDefault("id"), path + ".id", "duplicate_identifier", "Tenant identifiers must be unique.");
            var weight = ReadPositive(validation, map.Fields.GetValueOrDefault("weight"), path + ".weight");
            result.Add(new TenantWeight(id, weight));
        }
        return result.ToImmutable();
    }

    private static int ReadPositive(NodeValidation validation, InputNode? node, string path)
    {
        return validation.TryInt32(node, path, true, out var value) ? value : 0;
    }

    private static ImmutableDictionary<string, ImmutableArray<PayloadBucket>> ReadPayloads(NodeValidation validation, InputNode? node)
    {
        var map = validation.Map(node, "payloads");
        if (map is null) return ImmutableDictionary<string, ImmutableArray<PayloadBucket>>.Empty.WithComparers(StringComparer.Ordinal);
        validation.CheckFields(map, "payloads", PayloadNames);
        var result = ImmutableDictionary.CreateBuilder<string, ImmutableArray<PayloadBucket>>(StringComparer.Ordinal);
        foreach (var name in PayloadNames)
        {
            var sequence = validation.Sequence(map.Fields.GetValueOrDefault(name), "payloads." + name);
            if (sequence is null) continue;
            var buckets = ImmutableArray.CreateBuilder<PayloadBucket>();
            var sizes = new HashSet<int>();
            for (var i = 0; i < sequence.Items.Length; i++)
            {
                var path = $"payloads.{name}[{i}]";
                var bucket = validation.Map(sequence.Items[i], path);
                if (bucket is null) continue;
                validation.CheckFields(bucket, path, "size_bytes", "weight");
                var size = ReadPositive(validation, bucket.Fields.GetValueOrDefault("size_bytes"), path + ".size_bytes");
                if (!sizes.Add(size)) validation.Error(bucket.Fields.GetValueOrDefault("size_bytes"), path + ".size_bytes", "duplicate_payload_size", "Payload sizes must be unique within a lifecycle type.");
                var weight = ReadPositive(validation, bucket.Fields.GetValueOrDefault("weight"), path + ".weight");
                buckets.Add(new PayloadBucket(size, weight));
            }
            result[name] = buckets.ToImmutable();
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<LoadStage> ReadStages(NodeValidation validation, InputNode? node)
    {
        var sequence = validation.Sequence(node, "stages");
        if (sequence is null) return [];
        var result = ImmutableArray.CreateBuilder<LoadStage>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < sequence.Items.Length; i++)
        {
            var path = $"stages[{i}]";
            var map = validation.Map(sequence.Items[i], path);
            if (map is null) continue;
            validation.CheckFields(map, path, "id", "offered_attempts_per_second", "duration_seconds");
            var id = validation.String(map, "id", path) ?? "";
            validation.IsSlug(id, map.Fields.GetValueOrDefault("id"), path + ".id");
            if (!ids.Add(id)) validation.Error(map.Fields.GetValueOrDefault("id"), path + ".id", "duplicate_identifier", "Stage identifiers must be unique.");
            var rate = ReadPositive(validation, map.Fields.GetValueOrDefault("offered_attempts_per_second"), path + ".offered_attempts_per_second");
            var duration = ReadPositive(validation, map.Fields.GetValueOrDefault("duration_seconds"), path + ".duration_seconds");
            result.Add(new LoadStage(id, rate, duration));
        }
        return result.ToImmutable();
    }

    private static void CheckWeightSum(NodeValidation validation, IEnumerable<int> values, string path)
    {
        try
        {
            long sum = 0;
            foreach (var value in values) sum = checked(sum + value);
        }
        catch (OverflowException)
        {
            validation.Error(new SourceLocation(validation.Source), path, "int64_overflow", "Weight sum exceeds Int64.");
        }
    }

    private static void CheckStageTotals(NodeValidation validation, ImmutableArray<LoadStage> stages)
    {
        try
        {
            long total = 0;
            foreach (var stage in stages)
            {
                var stageAttempts = checked((long)stage.OfferedAttemptsPerSecond * stage.DurationSeconds);
                total = checked(total + stageAttempts);
            }
        }
        catch (OverflowException)
        {
            validation.Error(new SourceLocation(validation.Source), "stages", "int64_overflow", "Aggregate planned attempts exceed Int64.");
        }
    }

    private static bool IsSignedInteger(string value) => value.Length > 0 &&
        (value[0] == '-' ? value.Length > 1 && value[1..].All(char.IsAsciiDigit) : value.All(char.IsAsciiDigit));
}
