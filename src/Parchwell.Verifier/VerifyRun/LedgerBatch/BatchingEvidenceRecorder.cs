using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.A01;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;

namespace Parchwell.Verifier.VerifyRun.LedgerBatch;

internal sealed record BatchingEvidenceRecording(BatchingRecordedEvidence? Evidence, IReadOnlyList<ResultDiagnostic> Diagnostics, WorkloadObservation? WorkloadObservation);

/// <summary>Records the approved B06 and B07 latency, Produce-request and batch measurements without adding run-validity checks.</summary>
internal static class BatchingEvidenceRecorder
{
    /// <summary>The largest delivery latency, in milliseconds, recorded; its microsecond value fits an Int64 with margin.</summary>
    internal const double MaximumMilliseconds = 9_000_000_000_000d;

    internal static BatchingEvidenceRecording Record(RunJudgementRequest request, RunJudgementResult judgement)
    {
        if (request.Identity.ScenarioId is not ("B06" or "B07")) return new(null, [], null);
        long[]? latencies = Latencies(request.Ledger);
        (long? produce, long? batches, long? largest) = Statistics(request, judgement.Assessments);
        WorkloadRecording workload = WorkloadRecorder.Record(request.Manifest, request.Ledger);
        BatchingRecordedEvidence evidence = latencies is null
            ? new(null, null, null, null, null, produce, batches, largest)
            : new(latencies.LongLength, latencies[0], NearestRank(latencies, 50), NearestRank(latencies, 95), latencies[^1], produce, batches, largest);
        return new(evidence, workload.Diagnostics, workload.Observation);
    }

    // One delivered row without a usable latency leaves the whole summary absent.
    private static long[]? Latencies(ParseResult<IReadOnlyList<LedgerRow>> ledger)
    {
        if (!ledger.IsSuccess || ledger.Value is null) return null;
        var values = new List<long>(ledger.Value.Count);
        foreach (LedgerRow row in ledger.Value)
        {
            if (!row.Delivered) continue;
            if (row.DeliveryLatencyMs.Presence != FieldPresence.Value) return null;
            double milliseconds = row.DeliveryLatencyMs.Value;
            if (!double.IsFinite(milliseconds) || milliseconds < 0 || milliseconds > MaximumMilliseconds) return null;
            values.Add(decimal.ToInt64(decimal.Round((decimal)milliseconds * 1000m, 0, MidpointRounding.AwayFromZero)));
        }
        if (values.Count == 0) return null;
        values.Sort();
        return [.. values];
    }

    // Nearest rank: the value at one-based position ceil(percent * count / 100).
    private static long NearestRank(long[] ordered, int percent) => ordered[(int)((ordered.LongLength * percent + 99) / 100) - 1];

    // Produce comes from check 25's report, which holds every broker key; batchcnt windows reset on emit, so all are summed.
    private static (long? Produce, long? Batches, long? Largest) Statistics(RunJudgementRequest request, IReadOnlyList<CheckAssessment> assessments)
    {
        for (int number = 18; number <= 26; number++)
        {
            CheckAssessment? assessment = assessments.FirstOrDefault(item => item.CheckNumber == number);
            if (assessment?.Evaluation != AssessmentEvaluation.Evaluated || assessment.Disposition != AssessmentDisposition.Pass) return (null, null, null);
        }
        if (request.Reports.Count == 0) return (null, null, null);
        long boundary = request.Capabilities.Value!.StatisticsCompletion.Value!.DrainBoundaryMicroseconds.Value;
        long? produce = null;
        long? batches = 0;
        long? largest = 0;
        bool selected = false;
        foreach (ReportAssociation association in request.Reports)
        {
            bool first = !selected && association.Native.Value!.TimestampMicroseconds.Value >= boundary;
            selected |= first;
            JsonElement? root = Parse(association.Envelope.RawJson);
            if (first) produce = root is JsonElement report ? ProduceRequests(report) : null;
            (batches, largest) = batches is long sum && largest is long max && root is JsonElement value ? AddBatches(value, sum, max) : (null, null);
        }
        return (produce, batches, largest);
    }

    private static JsonElement? Parse(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // A missing req, Produce or broker is an unexpected shape, not a measured zero.
    private static long? ProduceRequests(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("brokers", out JsonElement brokers) || brokers.ValueKind != JsonValueKind.Object) return null;
        long total = 0;
        bool any = false;
        foreach (JsonProperty broker in brokers.EnumerateObject())
        {
            if (broker.Value.ValueKind != JsonValueKind.Object || !broker.Value.TryGetProperty("req", out JsonElement requests) ||
                requests.ValueKind != JsonValueKind.Object || !requests.TryGetProperty("Produce", out JsonElement produce) ||
                !TryCount(produce, out long count) || !TryAdd(ref total, count)) return null;
            any = true;
        }
        return any ? total : null;
    }

    // The largest message count comes only from windows that sampled a batch.
    private static (long? Batches, long? Largest) AddBatches(JsonElement root, long sum, long largest)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("topics", out JsonElement topics) || topics.ValueKind != JsonValueKind.Object) return (null, null);
        foreach (JsonProperty topic in topics.EnumerateObject())
        {
            if (topic.Value.ValueKind != JsonValueKind.Object || !topic.Value.TryGetProperty("batchcnt", out JsonElement window) ||
                window.ValueKind != JsonValueKind.Object || !window.TryGetProperty("cnt", out JsonElement cnt) || !window.TryGetProperty("max", out JsonElement max) ||
                !TryCount(cnt, out long count) || !TryCount(max, out long messages) || (count > 0 && messages == 0) || !TryAdd(ref sum, count)) return (null, null);
            if (count > 0) largest = Math.Max(largest, messages);
        }
        return (sum, largest);
    }

    private static bool TryCount(JsonElement value, out long count)
    {
        count = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out count) && count >= 0;
    }

    private static bool TryAdd(ref long total, long value)
    {
        if (value > long.MaxValue - total) return false;
        total += value;
        return true;
    }
}
