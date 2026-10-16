using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Parchwell.LoadGen.Run.Evidence;

/// <summary>One schema-one request capability claim and its pinned source trail.</summary>
internal sealed record RequestCapabilityClaim(
    string Requirement,
    string Status,
    string PinnedVersion,
    ImmutableArray<string> SourceUrls,
    string SourceFacts,
    string Inference,
    string LocalTestEvidence);

/// <summary>Canonical schema-one request capability claims emitted and enforced by evidence read-back.</summary>
internal static class RequestCapabilityCatalogue
{
    public const string PinnedVersion = "2.16.0";

    public static ImmutableArray<RequestCapabilityClaim> Claims { get; } =
    [
        new("request_identity", "unsupported", PinnedVersion,
            ["https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md",
             "https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L968-L1058",
             "https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/LogMessage.cs#L45-L71"],
            "Statistics expose aggregate client and broker counters and windows. The timeout logger is capped by log_first_n and its format has no correlation identifier. LogMessage exposes name, level, facility, and message.",
            "These sources provide no complete unique identity for every request observed by this collector.",
            "tests/Parchwell.LoadGen.Tests/Evidence/RunEvidenceSessionTests.cs"),
        new("matched_response_error_throttle_attribution", "unsupported", PinnedVersion,
            ["https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md"],
            "The statistics document defines broker throttle windows and cumulative counters.",
            "Those aggregate fields cannot match one request with a response, error, or throttle event.",
            "tests/Parchwell.LoadGen.Tests/Evidence/RunEvidenceSessionTests.cs"),
        new("applicable_deadline_provenance", "unproven", PinnedVersion,
            ["https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/LogMessage.cs#L45-L71",
             "https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_request.c#L5109-L5127"],
            "Managed LogMessage exposes name, level, facility, and message; request source defines a message-lifetime deadline.",
            "No managed callback field ties an observed event to the applicable request deadline.",
            "tests/Parchwell.LoadGen.Tests/Evidence/RunEvidenceSessionTests.cs"),
        new("native_retransmission", "unsupported", PinnedVersion,
            ["https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_request.c#L5030-L5037"],
            "The retry path constructs a new request.",
            "This adapter records no complete request sequence to establish which request is a retransmission.",
            "tests/Parchwell.LoadGen.Tests/Evidence/RunEvidenceSessionTests.cs"),
        new("request_ordering", "unproven", PinnedVersion,
            ["https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ProducerBuilder.cs#L261-L278"],
            "The managed producer log callback is called spontaneously from internal librdkafka threads.",
            "A local receipt sequence cannot establish native source-event order.",
            "tests/Parchwell.LoadGen.Tests/Evidence/RunEvidenceSessionTests.cs"),
        new("request_lifecycle_completeness", "unsupported", PinnedVersion,
            ["https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ProducerBuilder.cs#L261-L278",
             "https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/LogMessage.cs#L45-L71",
             "https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L968-L1058"],
            "The managed callback is a spontaneous log callback from internal librdkafka threads. Timeout log emission is limited by log_first_n, and LogMessage provides only name, level, facility, and message.",
            "This capture cannot establish a complete request lifecycle.",
            "tests/Parchwell.LoadGen.Tests/Evidence/RunEvidenceSessionTests.cs"),
        new("broker_instance_lifecycle_inventory", "unproven", PinnedVersion,
            ["https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md"],
            "Statistics reports broker objects in periodic samples.",
            "Samples cannot establish broker instances absent between reports.",
            "tests/Parchwell.LoadGen.Tests/Evidence/RunEvidenceSessionTests.cs")
    ];

    private static readonly ImmutableDictionary<string, RequestCapabilityClaim> ClaimsByRequirement =
        Claims.ToImmutableDictionary(claim => claim.Requirement, StringComparer.Ordinal);

    public static bool TryGet(string requirement, [NotNullWhen(true)] out RequestCapabilityClaim? claim) =>
        ClaimsByRequirement.TryGetValue(requirement, out claim!);

    /// <summary>Returns whether a persisted claim exactly matches the canonical source and inference record.</summary>
    public static bool Matches(JsonElement candidate)
    {
        if (candidate.ValueKind != JsonValueKind.Object ||
            !TryGetString(candidate, "requirement", out string requirement) ||
            !TryGet(requirement, out RequestCapabilityClaim? claim) ||
            !TryGetString(candidate, "status", out string status) ||
            !StringComparer.Ordinal.Equals(status, claim.Status) ||
            !TryGetString(candidate, "pinned_version", out string version) ||
            !StringComparer.Ordinal.Equals(version, claim.PinnedVersion) ||
            !TryGetStringArray(candidate, "source_urls", out ImmutableArray<string> sources) ||
            !sources.SequenceEqual(claim.SourceUrls, StringComparer.Ordinal) ||
            !TryGetString(candidate, "source_facts", out string facts) ||
            !StringComparer.Ordinal.Equals(facts, claim.SourceFacts) ||
            !TryGetString(candidate, "inference", out string inference) ||
            !StringComparer.Ordinal.Equals(inference, claim.Inference) ||
            !TryGetString(candidate, "local_test_evidence", out string testEvidence) ||
            !StringComparer.Ordinal.Equals(testEvidence, claim.LocalTestEvidence))
            return false;

        string[] expectedProperties = ["requirement", "status", "pinned_version", "source_urls", "source_facts", "inference", "local_test_evidence"];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in candidate.EnumerateObject())
        {
            if (!expectedProperties.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name)) return false;
        }
        return seen.Count == expectedProperties.Length;
    }

    private static bool TryGetString(JsonElement parent, string propertyName, out string value)
    {
        if (parent.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.String && property.GetString() is { } text)
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetStringArray(JsonElement parent, string propertyName, out ImmutableArray<string> values)
    {
        if (parent.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.Array)
        {
            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>(property.GetArrayLength());
            foreach (JsonElement value in property.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } text)
                {
                    values = [];
                    return false;
                }
                builder.Add(text);
            }
            values = builder.MoveToImmutable();
            return true;
        }

        values = [];
        return false;
    }
}
