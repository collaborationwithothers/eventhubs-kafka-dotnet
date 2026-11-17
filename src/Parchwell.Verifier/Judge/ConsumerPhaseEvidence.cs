using System.Buffers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Parchwell.Verifier.Judge;

/// <summary>The four consumer outcome counts of one repeat or, summed, of one phase.</summary>
internal sealed record ConsumerCounts(long ProcessedOnce, long ProcessedAgain, long ProcessedAgainExtraRows, long CommittedNeverProcessed);

/// <summary>
/// One repeat's judged consumer evidence: the typed fields the phase result needs and the
/// candidate JSON of docs/operations/verifier-contract.md "C5 phase, blob and receipt contract".
/// </summary>
internal sealed record ConsumerProjection(string Status, IReadOnlyList<string> Reasons, ConsumerCounts? Counts, JsonElement Json);

/// <summary>The phase-level consumer object and the root status and reasons it feeds.</summary>
internal sealed record ConsumerPhaseResult(string Status, IReadOnlyList<string> Reasons, ConsumerCounts? Counts,
    string RootStatus, IReadOnlyList<string> RootReasons);

/// <summary>
/// Reads the <c>consumer</c> block that <c>consumer-verify</c> writes into a result, and the
/// projection of it that a candidate carries. The result codec keeps the block as unchecked JSON.
/// </summary>
internal static partial class ConsumerEvidenceRules
{
    internal const string CountsUnavailableReason = "j10_consumer_counts_unavailable";
    internal const string ProducerSideReason = "producer_side_not_complete_valid";

    private static readonly string[] ReasonNames =
    [
        ProducerSideReason, "consumer_registration_invalid", "consumer_record_invalid", "consumer_start_assignment_invalid",
        "consumer_processed_outside_window", "consumer_processed_unjoined", "consumer_stop_invalid",
        "consumer_configuration_mismatch", "consumer_commit_record_disagreement", "consumer_record_unavailable",
        "consumer_commit_evidence_unavailable", "consumer_completion_deadline", "consumer_commit_frontier_short"
    ];

    private static readonly string[] ProjectedNames = ["status", "reasons", "group_id", "kill_count", "offset_read", "counts", "reassignment"];

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$")]
    private static partial Regex TimestampRegex();

    /// <summary>The group ID of one consumer pair repeat.</summary>
    internal static string GroupId(string runId, string phase, int repeat) => $"{runId}-{phase.ToLowerInvariant()}-r{repeat}-consumers";

    /// <summary>Checks a full result block and builds its candidate projection.</summary>
    internal static ConsumerProjection? ReadBlock(JsonElement block, string groupId) => Read(block, groupId, fullBlock: true);

    /// <summary>Checks a candidate's per-repeat projection.</summary>
    internal static ConsumerProjection? ReadProjection(JsonElement projection, string groupId) => Read(projection, groupId, fullBlock: false);

    /// <summary>Checks the counts object of 3.3 rule 5.</summary>
    internal static ConsumerCounts? ReadCounts(JsonElement counts)
    {
        if (!HasExactly(counts, ["processed_once", "processed_again", "processed_again_extra_rows", "committed_never_processed"])) return null;
        long[] values = new long[4];
        string[] names = ["processed_once", "processed_again", "processed_again_extra_rows", "committed_never_processed"];
        for (int index = 0; index < names.Length; index++)
        {
            JsonElement value = counts.GetProperty(names[index]);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out values[index]) || values[index] < 0) return null;
        }
        return values[2] >= values[1] ? new(values[0], values[1], values[2], values[3]) : null;
    }

    private static ConsumerProjection? Read(JsonElement e, string groupId, bool fullBlock)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("status", out JsonElement statusElement) ||
            statusElement.ValueKind != JsonValueKind.String) return null;
        string status = statusElement.GetString()!;
        if (status is not ("complete_valid" or "invalid" or "incomplete")) return null;
        if (!e.TryGetProperty("kill_count", out JsonElement kill)) return null;
        bool hasKill = kill.ValueKind != JsonValueKind.Null;
        if (hasKill && (kill.ValueKind != JsonValueKind.Number || !kill.TryGetInt64(out long killCount) || killCount is < 1 or > 1_000_000)) return null;
        bool complete = status == "complete_valid";
        var expected = new List<string> { "status", "reasons", "group_id", "kill_count", "offset_read" };
        if (fullBlock) expected.AddRange(["schema_version", "members", "partitions", "diagnostics"]);
        if (complete) expected.Add("counts");
        if (complete && hasKill) expected.Add("reassignment");
        if (!HasExactly(e, expected.ToArray())) return null;
        if (fullBlock)
        {
            JsonElement schema = e.GetProperty("schema_version");
            if (schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int version) || version != 1) return null;
            foreach (string array in new[] { "members", "partitions", "diagnostics" })
                if (e.GetProperty(array).ValueKind != JsonValueKind.Array) return null;
        }
        JsonElement reasonsElement = e.GetProperty("reasons");
        if (!TryReadReasons(reasonsElement, out string[]? reasons) || (reasons!.Length == 0) != complete) return null;
        if (reasons.Any(reason => !ReasonNames.Contains(reason, StringComparer.Ordinal))) return null;
        JsonElement group = e.GetProperty("group_id");
        if (group.ValueKind != JsonValueKind.String || group.GetString() != groupId) return null;
        ConsumerCounts? counts = null;
        if (complete)
        {
            counts = ReadCounts(e.GetProperty("counts"));
            if (counts is null) return null;
            if (hasKill && !ValidReassignment(e.GetProperty("reassignment"))) return null;
        }
        if (!ValidOffsetRead(e.GetProperty("offset_read"))) return null;
        return new(status, reasons, counts, Project(e));
    }

    private static bool TryReadReasons(JsonElement element, out string[]? reasons)
    {
        reasons = null;
        if (element.ValueKind != JsonValueKind.Array) return false;
        var values = new List<string>();
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) return false;
            values.Add(item.GetString()!);
        }
        for (int index = 1; index < values.Count; index++)
            if (string.CompareOrdinal(values[index - 1], values[index]) >= 0) return false;
        reasons = values.ToArray();
        return true;
    }

    private static bool ValidReassignment(JsonElement e)
    {
        if (!HasExactly(e, ["microseconds", "unavailable_reason"])) return false;
        JsonElement microseconds = e.GetProperty("microseconds"), reason = e.GetProperty("unavailable_reason");
        bool hasMicroseconds = microseconds.ValueKind != JsonValueKind.Null, hasReason = reason.ValueKind != JsonValueKind.Null;
        if (hasMicroseconds == hasReason) return false;
        if (hasMicroseconds) return microseconds.ValueKind == JsonValueKind.Number && microseconds.TryGetInt64(out long value) && value >= 0;
        return reason.ValueKind == JsonValueKind.String && reason.GetString() is "different_nodes" or "no_reassignment";
    }

    private static bool ValidOffsetRead(JsonElement e)
    {
        if (!HasExactly(e, ["outcome", "requested_partitions", "started_utc", "ended_utc", "error_code", "offsets"])) return false;
        JsonElement outcome = e.GetProperty("outcome");
        if (outcome.ValueKind != JsonValueKind.String ||
            outcome.GetString() is not ("success" or "kafka_exception" or "partition_exception" or "invalid_response" or "not_attempted")) return false;
        JsonElement partitions = e.GetProperty("requested_partitions");
        if (partitions.ValueKind != JsonValueKind.Array) return false;
        long previous = -1;
        foreach (JsonElement partition in partitions.EnumerateArray())
        {
            if (partition.ValueKind != JsonValueKind.Number || !partition.TryGetInt64(out long number) || number <= previous) return false;
            previous = number;
        }
        JsonElement started = e.GetProperty("started_utc"), ended = e.GetProperty("ended_utc"), error = e.GetProperty("error_code");
        if (!NullOrTimestamp(started) || !NullOrTimestamp(ended) ||
            error.ValueKind != JsonValueKind.Null && (error.ValueKind != JsonValueKind.Number || !error.TryGetInt64(out _))) return false;
        if (outcome.GetString() == "not_attempted" &&
            (started.ValueKind != JsonValueKind.Null || ended.ValueKind != JsonValueKind.Null || error.ValueKind != JsonValueKind.Null)) return false;
        JsonElement offsets = e.GetProperty("offsets");
        if (offsets.ValueKind != JsonValueKind.Array) return false;
        foreach (JsonElement offset in offsets.EnumerateArray())
        {
            if (!HasExactly(offset, ["partition", "offset", "error_code"])) return false;
            foreach (string name in new[] { "partition", "offset", "error_code" })
            {
                JsonElement value = offset.GetProperty(name);
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out _)) return false;
            }
        }
        return true;
    }

    private static bool NullOrTimestamp(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null || e.ValueKind == JsonValueKind.String && TimestampRegex().IsMatch(e.GetString()!);

    /// <summary>True when the object has exactly the named properties, once each.</summary>
    internal static bool HasExactly(JsonElement e, string[] names)
    {
        if (e.ValueKind != JsonValueKind.Object) return false;
        int count = 0;
        foreach (JsonProperty property in e.EnumerateObject())
        {
            if (!names.Contains(property.Name, StringComparer.Ordinal)) return false;
            count++;
        }
        return count == names.Length;
    }

    private static JsonElement Project(JsonElement source)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (string name in ProjectedNames)
                if (source.TryGetProperty(name, out JsonElement value))
                {
                    writer.WritePropertyName(name);
                    value.WriteTo(writer);
                }
            writer.WriteEndObject();
        }
        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}

/// <summary>Builds the phase consumer result of ADR-022 from the producer judgement and the five projections.</summary>
internal static class ConsumerPhaseJudge
{
    /// <summary>
    /// <paramref name="projections"/> holds one entry per repeat, in order, already set to
    /// <c>null</c> where the slot failed J2 to J6.
    /// </summary>
    internal static ConsumerPhaseResult Evaluate(PhaseJudgement judgement, IReadOnlyList<ConsumerProjection?> projections)
    {
        ArgumentNullException.ThrowIfNull(judgement);
        ArgumentNullException.ThrowIfNull(projections);
        bool invalid = projections.Any(p => p is { Status: "invalid" } && !(p.Reasons.Count == 1 && p.Reasons[0] == ConsumerEvidenceRules.ProducerSideReason));
        bool allComplete = judgement.Repeats.Count == 5 && projections.Count == 5 &&
            judgement.Repeats.All(r => r.Checks.J1 && r.Checks.J2 && r.Checks.J3 && r.Checks.J4 && r.Checks.J5 && r.Checks.J6) &&
            projections.All(p => p is { Status: "complete_valid", Counts: not null });
        ConsumerCounts? counts = null;
        if (!invalid && allComplete)
        {
            try
            {
                ConsumerCounts[] each = projections.Select(p => p!.Counts!).ToArray();
                counts = new(checked(each.Sum(c => c.ProcessedOnce)), checked(each.Sum(c => c.ProcessedAgain)),
                    checked(each.Sum(c => c.ProcessedAgainExtraRows)), checked(each.Sum(c => c.CommittedNeverProcessed)));
            }
            catch (OverflowException) { counts = null; }
        }
        string status = invalid ? "invalid" : counts is not null ? "complete_valid" : "incomplete";
        var reasons = projections.Where(p => p is not null).SelectMany(p => p!.Reasons).ToList();
        if (counts is null) reasons.Add(ConsumerEvidenceRules.CountsUnavailableReason);
        string[] sorted = reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(100).ToArray();
        string root = judgement.Status == "invalid" || status == "invalid" ? "invalid"
            : judgement.Status == "complete_valid" && status == "complete_valid" ? "complete_valid" : "incomplete";
        string[] rootReasons = judgement.Reasons.Concat(sorted).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(100).ToArray();
        return new(status, sorted, counts, root, rootReasons);
    }
}
