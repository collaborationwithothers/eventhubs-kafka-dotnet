using System.Collections.Immutable;
using System.Text.Json;

namespace Parchwell.Kafka.Statistics;

/// <summary>Copies the supported librdkafka statistics fields into immutable typed values.</summary>
/// <remarks>
/// The native statistics document is defined by librdkafka v2.16.0:
/// <see href="https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md">STATISTICS.md</see>.
/// Unknown fields are allowed for forward compatibility, while duplicate property names are
/// rejected because selecting one duplicate would make the evidence ambiguous. The session bounds
/// input size before calling this parser; this parser independently caps JSON nesting at 64 levels.
/// </remarks>
internal static class KafkaStatisticsParser
{
    private const int MaximumJsonDepth = 64;

    /// <summary>Parses one raw statistics document and enforces the configured broker-series limit.</summary>
    /// <param name="json">The original JSON text received from librdkafka.</param>
    /// <param name="maxBrokerSeries">The maximum number of broker objects allowed in the report.</param>
    /// <returns>A snapshot only when all required fields and broker series are valid.</returns>
    internal static (KafkaStatisticsSnapshot? Snapshot, ImmutableArray<KafkaStatisticsDiagnostic> Diagnostics)
        Parse(string json, int maxBrokerSeries)
    {
        if (json is null)
        {
            return Failure("invalid_json");
        }

        if (maxBrokerSeries < 1)
        {
            return Failure("invalid_broker_series_limit", field: "brokers");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaximumJsonDepth });
        }
        catch (JsonException)
        {
            return Failure("invalid_json");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (FindDuplicateProperty(root, out string? duplicate))
            {
                return Failure("duplicate_property", field: duplicate);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Failure("invalid_root");
            }

            if (!TryRequiredString(root, "name", out string? name)
                || !TryRequiredString(root, "client_id", out string? clientId)
                || !TryRequiredString(root, "type", out string? type)
                || !TryRequiredNonNegativeInt64(root, "ts", out long timestampMicroseconds)
                || !TryRequiredNonNegativeInt64(root, "time", out long timeSeconds)
                || !TryRequiredNonNegativeInt64(root, "age", out long ageMicroseconds))
            {
                return Failure("invalid_or_missing_field", field: FindInvalidRootField(root));
            }

            if (!TryGetObject(root, "brokers", out JsonElement brokers))
            {
                return Failure("invalid_or_missing_field", field: "brokers");
            }

            int brokerCount = 0;
            foreach (JsonProperty _ in brokers.EnumerateObject())
            {
                brokerCount++;
                if (brokerCount > maxBrokerSeries)
                {
                    return Failure("broker_series_limit_exceeded", field: "brokers");
                }
            }

            ImmutableArray<KafkaBrokerStatistics>.Builder parsedBrokers =
                ImmutableArray.CreateBuilder<KafkaBrokerStatistics>(brokerCount);
            foreach (JsonProperty brokerEntry in brokers.EnumerateObject())
            {
                if (!TryParseBroker(brokerEntry.Name, brokerEntry.Value, out KafkaBrokerStatistics? broker,
                        out string? field, out string? code))
                {
                    return Failure(code!, brokerEntry.Name, field);
                }

                parsedBrokers.Add(broker!);
            }

            KafkaStatisticsSnapshot snapshot = new(
                name!, clientId!, type!, timestampMicroseconds, timeSeconds, ageMicroseconds,
                parsedBrokers.MoveToImmutable());
            return (snapshot, ImmutableArray<KafkaStatisticsDiagnostic>.Empty);
        }
    }

    private static bool TryParseBroker(
        string dictionaryName,
        JsonElement element,
        out KafkaBrokerStatistics? broker,
        out string? field,
        out string? code)
    {
        broker = null;
        field = null;
        code = "invalid_or_missing_field";
        if (element.ValueKind != JsonValueKind.Object)
        {
            field = "brokers";
            return false;
        }

        if (!TryRequiredString(element, "name", out string? name))
        {
            field = "name";
            return false;
        }

        if (!StringComparer.Ordinal.Equals(dictionaryName, name))
        {
            field = "name";
            code = "broker_name_mismatch";
            return false;
        }

        if (!TryRequiredInt32NodeId(element, "nodeid", out int nodeId))
        {
            field = "nodeid";
            return false;
        }

        if (!TryRequiredString(element, "state", out string? state))
        {
            field = "state";
            return false;
        }

        if (!TryRequiredNonNegativeInt64(element, "stateage", out long stateAgeMicroseconds)
            || !TryRequiredNonNegativeInt64(element, "req_timeouts", out long requestTimeouts)
            || !TryRequiredNonNegativeInt64(element, "disconnects", out long disconnects)
            || !TryRequiredNonNegativeInt64(element, "txretries", out long transmissionRetries)
            || !TryRequiredNonNegativeInt64(element, "txerrs", out long transmissionErrors)
            || !TryRequiredNonNegativeInt64(element, "rxerrs", out long receiveErrors))
        {
            field = FindInvalidBrokerCounter(element);
            return false;
        }

        if (!TryParseWindow(element, "throttle", "max", out KafkaWindowStatistics? throttle))
        {
            field = "throttle";
            return false;
        }

        if (!TryParseWindow(element, "rtt", "p99", out KafkaWindowStatistics? roundTripTime))
        {
            field = "rtt";
            return false;
        }

        broker = new KafkaBrokerStatistics(
            name!, nodeId, state!, stateAgeMicroseconds, requestTimeouts, disconnects,
            transmissionRetries, transmissionErrors, receiveErrors, throttle!, roundTripTime!);
        return true;
    }

    private static bool TryParseWindow(
        JsonElement broker,
        string windowField,
        string valueField,
        out KafkaWindowStatistics? window)
    {
        window = null;
        if (!TryGetObject(broker, windowField, out JsonElement windowElement)
            || !TryRequiredNonNegativeInt64(windowElement, "cnt", out long count)
            || !TryRequiredNonNegativeInt64(windowElement, valueField, out long value))
        {
            return false;
        }

        window = new KafkaWindowStatistics(count, count == 0 ? null : value);
        return true;
    }

    private static bool TryGetObject(JsonElement parent, string field, out JsonElement value)
    {
        value = default;
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(field, out value)
            && value.ValueKind == JsonValueKind.Object;
    }

    private static bool TryRequiredString(JsonElement parent, string field, out string? value)
    {
        value = null;
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(field, out JsonElement element)
            || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return value is not null;
    }

    private static bool TryRequiredNonNegativeInt64(JsonElement parent, string field, out long value)
    {
        value = 0;
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(field, out JsonElement element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt64(out value)
            && value >= 0;
    }

    private static bool TryRequiredInt32NodeId(JsonElement parent, string field, out int value)
    {
        value = 0;
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(field, out JsonElement element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value)
            && value >= -1;
    }

    private static bool FindDuplicateProperty(JsonElement element, out string? duplicate)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    duplicate = property.Name;
                    return true;
                }

                if (FindDuplicateProperty(property.Value, out duplicate))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (FindDuplicateProperty(item, out duplicate))
                {
                    return true;
                }
            }
        }

        duplicate = null;
        return false;
    }

    private static string? FindInvalidRootField(JsonElement root)
    {
        string[] strings = ["name", "client_id", "type"];
        foreach (string field in strings)
        {
            if (!TryRequiredString(root, field, out _))
            {
                return field;
            }
        }

        string[] numbers = ["ts", "time", "age"];
        foreach (string field in numbers)
        {
            if (!TryRequiredNonNegativeInt64(root, field, out _))
            {
                return field;
            }
        }

        return "brokers";
    }

    private static string? FindInvalidBrokerCounter(JsonElement broker)
    {
        string[] fields = ["stateage", "req_timeouts", "disconnects", "txretries", "txerrs", "rxerrs"];
        foreach (string field in fields)
        {
            if (!TryRequiredNonNegativeInt64(broker, field, out _))
            {
                return field;
            }
        }

        return null;
    }

    private static (KafkaStatisticsSnapshot? Snapshot, ImmutableArray<KafkaStatisticsDiagnostic> Diagnostics)
        Failure(string code, string? broker = null, string? field = null) =>
        (null, [new KafkaStatisticsDiagnostic(code, broker, field)]);
}
