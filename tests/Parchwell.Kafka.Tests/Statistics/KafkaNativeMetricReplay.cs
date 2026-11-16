using System.Diagnostics;
using System.Globalization;
using System.Text;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.Kafka.Tests.Telemetry;

namespace Parchwell.Kafka.Tests.Statistics;

// Invoke explicitly for the approved single offline measurement, not as a CI test.
internal static class KafkaNativeMetricReplay
{
    private static int _started;

    internal static ReplayComparison MeasureOnce()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("The approved replay permits one baseline and one expanded pass.");
        string[] reports = Enumerable.Range(1, 256).Select(index =>
            "{\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":" + index.ToString(CultureInfo.InvariantCulture)
            + ",\"time\":1,\"age\":" + index.ToString(CultureInfo.InvariantCulture)
            + ",\"tx\":" + index.ToString(CultureInfo.InvariantCulture)
            + ",\"replyq\":2,\"brokers\":{\"broker\":{\"name\":\"broker\",\"state\":\"UP\",\"nodeid\":1,\"stateage\":1,\"disconnects\":0,\"txretries\":0,\"txerrs\":0,\"rxerrs\":0,\"throttle\":{\"cnt\":2,\"max\":2},\"req_timeouts\":"
            + index.ToString(CultureInfo.InvariantCulture)
            + ",\"rtt\":{\"cnt\":2,\"min\":1,\"max\":3,\"avg\":2,\"sum\":4,\"p99\":3}}}}")
            .ToArray();
        return new ReplayComparison(Measure(reports, false), Measure(reports, true));
    }

    private static ReplayMeasurement Measure(string[] reports, bool expanded)
    {
        using var pipeline = new TelemetryTestPipeline();
        using var metrics = new KafkaStatisticsMetrics();
        var identity = new KafkaStatisticsIdentity("A01", expanded ? "expanded" : "baseline", KafkaClientRole.LoadGenProducer, "client");
        var state = metrics.CreateNativeState(identity, true);
        long allocated = GC.GetTotalAllocatedBytes(true);
        var stopwatch = Stopwatch.StartNew();
        foreach (string report in reports)
        {
            var (snapshot, _) = KafkaStatisticsParser.Parse(report, 1);
            if (snapshot is null) throw new InvalidOperationException("Invalid legacy replay fixture.");
            metrics.Record(identity, snapshot.Brokers[0], 1, 0);
            if (expanded)
            {
                var projection = KafkaNativeMetricParser.Parse(report);
                if (!projection.IsValid || projection.Identity is null) throw new InvalidOperationException("Invalid replay fixture.");
                state.Record(projection);
                state.RecordCanonical(projection.Identity, "broker", null, 1, 0);
            }
        }
        pipeline.Flush();
        stopwatch.Stop();
        long bytes = pipeline.Handler.Requests.Sum(request => (long)Encoding.UTF8.GetByteCount(request.Body));
        return new ReplayMeasurement(reports.Length, stopwatch.Elapsed, GC.GetTotalAllocatedBytes(true) - allocated,
            bytes, pipeline.Handler.Requests.Count, pipeline.Metrics.Records.Count);
    }
}

internal sealed record ReplayMeasurement(int Reports, TimeSpan Elapsed, long AllocatedBytes,
    long SerializedExporterBytes, int ExportRequests, int ExportedPoints);

internal sealed record ReplayComparison(ReplayMeasurement Baseline, ReplayMeasurement Expanded);
