using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTelemetry;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.Kafka.Tests.Telemetry;

namespace Parchwell.Kafka.Tests.Statistics;

// Explicit offline measurement only. This measures parsing/emission and one SDK collection per
// synthetic provider, not native callbacks, capture-worker overhead, whole-process CPU or billing.
internal static class KafkaNativeMetricReplay
{
    private static readonly string[] Requests = ["Produce", "Fetch", "ListOffsets", "Metadata", "LeaderAndIsr", "StopReplica", "UpdateMetadata", "ControlledShutdown", "OffsetCommit", "OffsetFetch", "FindCoordinator", "JoinGroup", "Heartbeat", "LeaveGroup", "SyncGroup", "DescribeGroups", "ListGroups", "SaslHandshake", "ApiVersion", "CreateTopics", "DeleteTopics", "DeleteRecords", "InitProducerId", "OffsetForLeaderEpoch", "AddPartitionsToTxn", "AddOffsetsToTxn", "EndTxn"];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static int _started;

    internal static ReplayComparison MeasureOnce()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("The approved replay permits one baseline and one expanded pass.");
        using var environment = new TelemetryEnvironmentFixture();
        string[][] reports = BuildReports();
        var archive = ArchiveSize(reports);
        return new ReplayComparison(Measure(reports, false), Measure(reports, true), archive);
    }

    private static string[][] BuildReports()
    {
        var reports = new string[32][];
        for (int lifetime = 0; lifetime < 32; lifetime++)
        {
            reports[lifetime] = new string[8];
            for (int index = 0; index < 8; index++)
            {
                var root = new JsonObject { ["name"] = "native-" + lifetime, ["client_id"] = "client-" + lifetime, ["type"] = lifetime < 20 ? "producer" : "consumer" };
                foreach (var row in NativeMetricContract.Rows)
                {
                    if (row.Path.StartsWith("fatal.", StringComparison.Ordinal)
                        || (lifetime < 20 && row.Path.StartsWith("cgrp.", StringComparison.Ordinal))
                        || (lifetime >= 20 && row.Path.StartsWith("eos.", StringComparison.Ordinal))) continue;
                    if (row.Path.StartsWith("brokers.*.", StringComparison.Ordinal))
                    {
                        for (int broker = 0; broker < 16; broker++)
                        {
                            string path = row.Path.Replace("brokers.*", "brokers.broker-" + broker, StringComparison.Ordinal);
                            if (path.EndsWith(".req.*", StringComparison.Ordinal))
                                foreach (string request in Requests) Set(root, path.Replace("req.*", "req." + request, StringComparison.Ordinal), row.Policy, index + 1);
                            else Set(root, path, row.Policy, index + 1);
                        }
                    }
                    else if (row.Path.StartsWith("topics.*.partitions.*.", StringComparison.Ordinal))
                    {
                        for (int partition = -1; partition < 32; partition++)
                            Set(root, row.Path.Replace("topics.*.partitions.*", "topics.topic.partitions." + partition.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal), row.Policy, index + 1);
                    }
                    else Set(root, row.Path.Replace("topics.*", "topics.topic", StringComparison.Ordinal), row.Policy, index + 1);
                }
                root["ts"] = index + 1;
                root["time"] = index + 1;
                root["age"] = index + 1;
                for (int broker = 0; broker < 16; broker++)
                {
                    var item = root["brokers"]!["broker-" + broker]!.AsObject();
                    item["name"] = "broker-" + broker;
                    item["nodeid"] = broker;
                }
                string report = root.ToJsonString();
                if (Encoding.UTF8.GetByteCount(report) > 1024 * 1024) throw new InvalidOperationException("Synthetic report exceeds the capture limit.");
                reports[lifetime][index] = report;
            }
        }
        return reports;
    }

    private static void Set(JsonObject root, string path, string policy, int ordinal)
    {
        string[] segments = path.Split('.');
        JsonObject parent = root;
        foreach (string segment in segments[..^1])
        {
            parent[segment] ??= new JsonObject();
            parent = parent[segment]!.AsObject();
        }
        string field = segments[^1];
        if (policy == "W+") parent["cnt"] = 2;
        parent[field] = policy switch
        {
            "B" => JsonValue.Create(true),
            "S" => JsonValue.Create(field switch { "source" => "configured", "state" when path.StartsWith("brokers.", StringComparison.Ordinal) => "UP", "state" => "up", "join_state" => "steady", "fetch_state" => "active", "idemp_state" => "Assigned", "txn_state" => "Ready", _ => throw new InvalidOperationException(path) }),
            "W0" when field == "cnt" => JsonValue.Create(2),
            "W+" when field == "sum" => JsonValue.Create(4),
            "C" or "C/Q" => JsonValue.Create(ordinal),
            _ => JsonValue.Create(2),
        };
    }

    private static ReplayArchive ArchiveSize(string[][] reports)
    {
        long original = 0, jsonl = 0, compressed = 0;
        for (int lifetime = 0; lifetime < reports.Length; lifetime++)
        {
            using var buffer = new MemoryStream();
            using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, true))
            {
                for (int index = 0; index < reports[lifetime].Length; index++)
                {
                    string raw = reports[lifetime][index];
                    original += Encoding.UTF8.GetByteCount(raw);
                    byte[] row = JsonSerializer.SerializeToUtf8Bytes(new RawRow(1, index + 1, raw), JsonOptions);
                    jsonl += row.Length + 1;
                    gzip.Write(row);
                    gzip.WriteByte((byte)'\n');
                }
            }
            compressed += buffer.Length;
        }
        return new ReplayArchive(original, jsonl, compressed);
    }

    private static ReplayMeasurement Measure(string[][] reports, bool expanded)
    {
        long allocated = GC.GetTotalAllocatedBytes(true), bytes = 0;
        int requests = 0, points = 0;
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var stopwatch = Stopwatch.StartNew();
        for (int lifetime = 0; lifetime < reports.Length; lifetime++)
        {
            string instance = "pod-" + lifetime.ToString(CultureInfo.InvariantCulture);
            using var pipeline = new TelemetryTestPipeline(options =>
            {
                TelemetryTestPipeline.Configure(options);
                options.ServiceInstanceId = instance;
                options.ServiceName = lifetime < 20 ? "Parchwell.LoadGen" : "Parchwell.Enrichment";
                options.RunId = "synthetic-replay";
            });
            var resource = pipeline.Meter.GetResource().Attributes.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (!resource.TryGetValue("service.instance.id", out object? actualInstance) || !Equals(instance, actualInstance) || !identities.Add(instance))
                throw new InvalidOperationException("Missing or duplicate synthetic provider resource identity.");
            using var metrics = new KafkaStatisticsMetrics();
            var identity = new KafkaStatisticsIdentity("A01", "synthetic-replay", lifetime < 20 ? KafkaClientRole.LoadGenProducer : KafkaClientRole.EnrichmentConsumer, "client-" + lifetime);
            var state = metrics.CreateNativeState(identity, true);
            for (int index = 0; index < reports[lifetime].Length; index++)
            {
                string report = reports[lifetime][index];
                var (snapshot, _) = KafkaStatisticsParser.Parse(report, 16);
                if (snapshot is null || snapshot.Brokers.Length != 16) throw new InvalidOperationException("Invalid legacy replay fixture.");
                KafkaNativeMetricProjection? projection = expanded ? KafkaNativeMetricParser.Parse(report) : null;
                if (expanded && (projection is null || !projection.IsValid || projection.Identity is null || projection.Diagnostics.Length != 0)) throw new InvalidOperationException("Invalid native replay fixture.");
                if (projection is not null) state.Record(projection);
                foreach (var broker in snapshot.Brokers)
                {
                    metrics.Record(identity, broker, 1, 1);
                    if (projection?.Identity is { } nativeIdentity) state.RecordCanonical(nativeIdentity, broker.Name, null, 1, 1);
                }
                if (expanded && !state.IsComplete) throw new InvalidOperationException("Native replay admission or counter failure.");
            }
            pipeline.Flush();
            var records = pipeline.Metrics.Records.ToArray();
            if (records.Any(record => record.Tags.ContainsKey("otel.metric.overflow"))) throw new InvalidOperationException("SDK metric overflow.");
            int expected = expanded ? (lifetime < 20 ? 3430 : 3421) : 64;
            if (records.Length != expected) throw new InvalidOperationException($"Expected {expected} points, received {records.Length} for provider {lifetime}.");
            foreach (string name in records.Select(record => record.Name).Distinct(StringComparer.Ordinal))
            {
                string encodedName = JsonSerializer.Serialize(name);
                if (!pipeline.Handler.Requests.Any(request => request.Body.Contains(encodedName, StringComparison.Ordinal)))
                    throw new InvalidOperationException("The Azure exporter did not serialize an SDK metric: " + name);
            }
            points += records.Length;
            bytes += pipeline.Handler.Requests.Sum(request => (long)Encoding.UTF8.GetByteCount(request.Body));
            requests += pipeline.Handler.Requests.Count;
        }
        stopwatch.Stop();
        if (identities.Count != 32 || requests == 0 || bytes == 0) throw new InvalidOperationException("Missing provider identities or exporter requests.");
        return new ReplayMeasurement(256, stopwatch.Elapsed, GC.GetTotalAllocatedBytes(true) - allocated, bytes, requests, points, identities.Count);
    }

    private sealed record RawRow(int ClientOrdinal, long ReportOrdinal, string Raw);
}

internal sealed record ReplayMeasurement(int Reports, TimeSpan Elapsed, long AllocatedBytes,
    long SerializedExporterBytes, int ExportRequests, int ExportedPoints, int ResourceIdentities);
internal sealed record ReplayArchive(long OriginalUtf8Bytes, long RawJsonlBytes, long GzipRawJsonlBytes);
internal sealed record ReplayComparison(ReplayMeasurement Baseline, ReplayMeasurement Expanded, ReplayArchive Archive);
