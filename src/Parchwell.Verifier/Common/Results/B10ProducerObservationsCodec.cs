using System.Text.Json;
using Parchwell.Verifier.Common.Phase;

namespace Parchwell.Verifier.Common.Results;

/// <summary>Shares the passive, ordered B10 producer-observation schema across result and candidate projections.</summary>
internal static class B10ProducerObservationsCodec
{
    internal const int MaximumStringLength = 256;

    private static readonly string[] RuntimeKeys = ["confluent_kafka", "librdkafka", "dotnet"];
    private static readonly string[] EntryFields = ["instance_id", "generator_version", "runtime_versions", "broker_count"];
    private static readonly string[] BrokerFields = ["role", "client_id", "maximum", "first_post_drain", "lower_bound"];

    internal static IReadOnlyList<B10ProducerObservation> Read(JsonElement value, B10IntakeEvidence intake)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != intake.Producers.Count)
            throw new FormatException("B10 producer observations require an ordered pair.");
        var observations = new List<B10ProducerObservation>(2);
        foreach (JsonElement entry in value.EnumerateArray())
        {
            Object(entry, EntryFields, ["instance_id"]);
            string instance = String(entry.GetProperty("instance_id"));
            string? generator = entry.TryGetProperty("generator_version", out JsonElement version) ? String(version) : null;
            Dictionary<string, string>? runtimes = null;
            if (entry.TryGetProperty("runtime_versions", out JsonElement runtime))
            {
                Object(runtime, RuntimeKeys, []);
                runtimes = new(StringComparer.Ordinal);
                foreach (JsonProperty property in runtime.EnumerateObject()) runtimes.Add(property.Name, String(property.Value));
            }
            BrokerConnectionCount? count = null;
            if (entry.TryGetProperty("broker_count", out JsonElement broker))
            {
                Object(broker, BrokerFields, ["role", "client_id", "lower_bound"]);
                if (broker.GetProperty("lower_bound").ValueKind != JsonValueKind.True)
                    throw new FormatException("B10 broker observations require lower_bound=true.");
                count = new(String(broker.GetProperty("role")), String(broker.GetProperty("client_id")),
                    Measurement(broker, "maximum"), Measurement(broker, "first_post_drain"), true);
            }
            observations.Add(new(instance, generator, runtimes, count));
        }
        Validate(observations, intake);
        return observations;
    }

    internal static void Validate(IReadOnlyList<B10ProducerObservation> observations, B10IntakeEvidence intake)
    {
        bool oneProducer = intake.Producers.Count == 1 && intake.Producers[0].Identity.ConfigurationId == "C0" &&
            VerifierHostOptions.IsB10ScientificRunId(intake.Producers[0].Identity.RunId);
        if (observations.Count != intake.Producers.Count || !oneProducer && intake.Producers.Count != 2 ||
            intake.Producers.Any(p => p.Identity.ScenarioId != "B10"))
            throw new FormatException("B10 producer observations require a B10 intake pair.");
        if (oneProducer && (intake.Phase != "C0" || intake.RepeatNumber is < 1 or > 5 ||
            intake.Producers[0].Identity.InstanceId != intake.Producers[0].Identity.ExpectedInstanceId ||
            intake.Producers[0].PredecessorInstanceId is not null ||
            intake.Producers[0].JobName != PhaseIdentity.JobName(intake.Producers[0].Identity.RunId, intake.Phase, intake.RepeatNumber)))
            throw new FormatException("B10 C0 producer observation has an invalid producer role.");
        for (int index = 0; index < observations.Count; index++)
        {
            B10ProducerObservation observation = observations[index];
            if (observation is null || !StringComparer.Ordinal.Equals(observation.InstanceId, intake.Producers[index].Identity.InstanceId))
                throw new FormatException("B10 producer observations do not match intake order.");
            Text(observation.InstanceId);
            if (observation.GeneratorVersion is not null) Text(observation.GeneratorVersion);
            if (observation.RuntimeVersions is not null)
                foreach (var runtime in observation.RuntimeVersions)
                {
                    if (!RuntimeKeys.Contains(runtime.Key, StringComparer.Ordinal))
                        throw new FormatException("B10 producer runtime key is unknown.");
                    Text(runtime.Value);
                }
            if (observation.BrokerCount is { } count)
            {
                Text(count.Role); Text(count.ClientId);
                if (!count.LowerBound || count.Maximum is < 0 || count.FirstPostDrain is < 0)
                    throw new FormatException("B10 broker observations have invalid measurements.");
            }
        }
    }

    internal static void Write(Utf8JsonWriter writer, IReadOnlyList<B10ProducerObservation> observations, B10IntakeEvidence intake)
    {
        Validate(observations, intake);
        writer.WritePropertyName("b10_producer_observations");
        writer.WriteStartArray();
        foreach (B10ProducerObservation observation in observations)
        {
            writer.WriteStartObject();
            writer.WriteString("instance_id", observation.InstanceId);
            if (observation.GeneratorVersion is not null) writer.WriteString("generator_version", observation.GeneratorVersion);
            if (observation.RuntimeVersions is not null)
            {
                writer.WritePropertyName("runtime_versions"); writer.WriteStartObject();
                foreach (string key in RuntimeKeys)
                    if (observation.RuntimeVersions.TryGetValue(key, out string? version)) writer.WriteString(key, version);
                writer.WriteEndObject();
            }
            if (observation.BrokerCount is { } count)
            {
                writer.WritePropertyName("broker_count"); writer.WriteStartObject();
                writer.WriteString("role", count.Role); writer.WriteString("client_id", count.ClientId);
                if (count.Maximum is long maximum) writer.WriteNumber("maximum", maximum);
                if (count.FirstPostDrain is long first) writer.WriteNumber("first_post_drain", first);
                writer.WriteBoolean("lower_bound", true); writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void Object(JsonElement value, string[] allowed, string[] required)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new FormatException("B10 observation object has the wrong kind.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
            if (!names.Add(property.Name) || !allowed.Contains(property.Name, StringComparer.Ordinal))
                throw new FormatException("B10 observation object has duplicate or unknown fields.");
        if (required.Any(name => !names.Contains(name))) throw new FormatException("B10 observation object is missing a required field.");
    }

    private static string String(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String) throw new FormatException("B10 observation text has the wrong kind.");
        string text = value.GetString()!; Text(text); return text;
    }

    private static void Text(string? value)
    {
        if (value is null || value.Length > MaximumStringLength) throw new FormatException("B10 observation text exceeds its bound or is null.");
    }

    private static long? Measurement(JsonElement broker, string name)
    {
        if (!broker.TryGetProperty(name, out JsonElement value)) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long count) || count < 0)
            throw new FormatException("B10 broker measurement must be a nonnegative Int64.");
        return count;
    }
}
