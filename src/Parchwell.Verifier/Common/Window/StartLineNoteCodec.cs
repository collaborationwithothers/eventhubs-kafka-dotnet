using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.Common.Window;

/// <summary>Reads and writes the bounded schema-one start-line note projection.</summary>
internal static class StartLineNoteCodec
{
    private static readonly IReadOnlySet<string> RootFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "schema_version", "identity", "topic", "partition_highs", "captured_at",
        "verifier_commit", "confluent_kafka_version", "librdkafka_version"
    };

    private static readonly IReadOnlySet<string> IdentityFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id"
    };

    private static readonly IReadOnlySet<string> PartitionFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "partition", "high"
    };

    /// <summary>Writes an explicit snake-case projection without a checksum.</summary>
    internal static byte[] Write(StartLineNote note, OutputLimits limits)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(note.Identity);
        ArgumentNullException.ThrowIfNull(note.PartitionHighs);
        if (note.SchemaVersion != 1)
        {
            throw new ArgumentException("Start-line notes must use schema version 1.", nameof(note));
        }

        using var stream = new BoundedMemoryStream(limits.MaxOutputBytes);
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema_version", note.SchemaVersion);
            WriteIdentity(writer, note.Identity);
            writer.WriteString("topic", note.Topic);
            writer.WritePropertyName("partition_highs");
            writer.WriteStartArray();
            foreach (PartitionHigh high in note.PartitionHighs)
            {
                ArgumentNullException.ThrowIfNull(high);
                writer.WriteStartObject();
                writer.WriteNumber("partition", high.Partition);
                writer.WriteNumber("high", high.High);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteString("captured_at", note.CapturedAt);
            writer.WriteString("verifier_commit", note.VerifierCommit);
            writer.WriteString("confluent_kafka_version", note.ConfluentKafkaVersion);
            writer.WriteString("librdkafka_version", note.LibrdkafkaVersion);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    /// <summary>Reads a strict schema-one start-line note.</summary>
    internal static ParseResult<StartLineNote> Read(
        ReadOnlyMemory<byte> utf8,
        string artifact,
        InputLimits limits,
        int maxPartitions)
    {
        ParseResult<JsonDocument> document = StrictJson.ParseDocument(utf8, artifact, limits, checkNumber: 7);
        if (!document.IsSuccess)
        {
            return ParseResult<StartLineNote>.Failure(document.Problems);
        }

        using JsonDocument parsed = document.Value!;
        JsonElement root = parsed.RootElement;
        IReadOnlyList<StructuralProblem> rootProblems = StrictJson.ValidateProperties(
            root, "$", RootFields, RootFields, artifact, checkNumber: 7);
        if (rootProblems.Count > 0)
        {
            return ParseResult<StartLineNote>.Failure(rootProblems);
        }

        if (!TryInt32(root, "schema_version", out int schemaVersion))
        {
            return Failure(artifact, "schema_version", "wrong_json_kind");
        }

        if (schemaVersion != 1)
        {
            return Failure(artifact, "schema_version", "unsupported_schema_version");
        }

        if (!TryIdentity(root, artifact, out RunIdentity? identity, out StructuralProblem? identityProblem))
        {
            return ParseResult<StartLineNote>.Failure(identityProblem!);
        }

        if (!TryString(root, "topic", out string? topic)) return Failure(artifact, "topic", "wrong_json_kind");
        if (!TryString(root, "captured_at", out string? capturedAt)) return Failure(artifact, "captured_at", "wrong_json_kind");
        if (!TryString(root, "verifier_commit", out string? verifierCommit)) return Failure(artifact, "verifier_commit", "wrong_json_kind");
        if (!TryString(root, "confluent_kafka_version", out string? kafkaVersion)) return Failure(artifact, "confluent_kafka_version", "wrong_json_kind");
        if (!TryString(root, "librdkafka_version", out string? librdkafkaVersion)) return Failure(artifact, "librdkafka_version", "wrong_json_kind");

        JsonElement highsElement = root.GetProperty("partition_highs");
        if (highsElement.ValueKind != JsonValueKind.Array)
        {
            return Failure(artifact, "partition_highs", "wrong_json_kind");
        }

        if (highsElement.GetArrayLength() > maxPartitions)
        {
            throw new InputLimitExceededException($"{artifact} exceeds the configured maximum partition count.");
        }

        var highs = new List<PartitionHigh>(highsElement.GetArrayLength());
        int index = 0;
        foreach (JsonElement item in highsElement.EnumerateArray())
        {
            string path = $"partition_highs[{index}]";
            IReadOnlyList<StructuralProblem> problems = StrictJson.ValidateProperties(
                item, path, PartitionFields, PartitionFields, artifact, checkNumber: 7);
            if (problems.Count > 0) return ParseResult<StartLineNote>.Failure(problems);
            if (!TryInt32(item, "partition", out int partition)) return Failure(artifact, path + ".partition", "wrong_json_kind");
            if (!TryInt64(item, "high", out long high)) return Failure(artifact, path + ".high", "wrong_json_kind");
            highs.Add(new PartitionHigh(partition, high));
            index++;
        }

        return ParseResult<StartLineNote>.Success(new StartLineNote(
            schemaVersion, identity!, topic!, highs, capturedAt!, verifierCommit!, kafkaVersion!, librdkafkaVersion!));
    }

    private static void WriteIdentity(Utf8JsonWriter writer, RunIdentity identity)
    {
        writer.WritePropertyName("identity");
        writer.WriteStartObject();
        writer.WriteString("scenario_id", identity.ScenarioId);
        writer.WriteString("configuration_id", identity.ConfigurationId);
        writer.WriteString("run_id", identity.RunId);
        writer.WriteString("instance_id", identity.InstanceId);
        writer.WriteString("expected_instance_id", identity.ExpectedInstanceId);
        writer.WriteString("client_id", identity.ClientId);
        writer.WriteEndObject();
    }

    private static bool TryIdentity(
        JsonElement root,
        string artifact,
        out RunIdentity? identity,
        out StructuralProblem? problem)
    {
        identity = null;
        problem = null;
        JsonElement value = root.GetProperty("identity");
        IReadOnlyList<StructuralProblem> problems = StrictJson.ValidateProperties(
            value, "identity", IdentityFields, IdentityFields, artifact, checkNumber: 7);
        if (problems.Count > 0)
        {
            problem = problems[0];
            return false;
        }

        if (!TryString(value, "scenario_id", out string? scenarioId)) { problem = Problem(artifact, "identity.scenario_id", "wrong_json_kind"); return false; }
        if (!TryString(value, "configuration_id", out string? configurationId)) { problem = Problem(artifact, "identity.configuration_id", "wrong_json_kind"); return false; }
        if (!TryString(value, "run_id", out string? runId)) { problem = Problem(artifact, "identity.run_id", "wrong_json_kind"); return false; }
        if (!TryString(value, "instance_id", out string? instanceId)) { problem = Problem(artifact, "identity.instance_id", "wrong_json_kind"); return false; }
        if (!TryString(value, "expected_instance_id", out string? expectedInstanceId)) { problem = Problem(artifact, "identity.expected_instance_id", "wrong_json_kind"); return false; }
        if (!TryString(value, "client_id", out string? clientId)) { problem = Problem(artifact, "identity.client_id", "wrong_json_kind"); return false; }
        identity = new RunIdentity(scenarioId!, configurationId!, runId!, instanceId!, expectedInstanceId!, clientId!);
        return true;

    }

    private static bool TryString(JsonElement parent, string name, out string? value)
    {
        if (parent.TryGetProperty(name, out JsonElement element) && StrictJson.TryGetString(element, out string result))
        {
            value = result;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryInt32(JsonElement parent, string name, out int value) =>
        parent.TryGetProperty(name, out JsonElement element) && StrictJson.TryGetInt32(element, out value)
            ? true
            : Fail(out value);

    private static bool TryInt64(JsonElement parent, string name, out long value) =>
        parent.TryGetProperty(name, out JsonElement element) && StrictJson.TryGetInt64(element, out value)
            ? true
            : Fail(out value);

    private static bool Fail(out int value) { value = 0; return false; }
    private static bool Fail(out long value) { value = 0; return false; }

    private static ParseResult<StartLineNote> Failure(string artifact, string field, string code) =>
        ParseResult<StartLineNote>.Failure(Problem(artifact, field, code));

    private static StructuralProblem Problem(string artifact, string field, string code) =>
        new(7, code, artifact, null, field, null);

    private sealed class BoundedMemoryStream(long maximumBytes) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacityFor(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacityFor(buffer.Length);
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            EnsureCapacityFor(1);
            base.WriteByte(value);
        }

        private void EnsureCapacityFor(int count)
        {
            if (count < 0 || Length > maximumBytes - count)
                throw new InputLimitExceededException("The start-line note exceeds the configured output byte limit.");
        }
    }
}
