using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json.Nodes;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.Kafka.Tests.Telemetry;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707

[Collection("Parchwell telemetry")]
[Trait("Category", "Unit")]
public sealed class KafkaNativeMetricSdkTests
{
    [Fact]
    public void Given_NativeValues_When_ExportedThroughSdk_Then_GaugeIsLastValueAndCounterIsCumulative()
    {
        using var pipeline = new TelemetryTestPipeline();
        using var metrics = new KafkaStatisticsMetrics();
        var state = metrics.CreateNativeState(new KafkaStatisticsIdentity("A01", "sdk-native", KafkaClientRole.LoadGenProducer, "client"), false);
        state.Record(Parse(2, "\"replyq\":2,\"tx\":2,\"topics\":{\"t\":{\"partitions\":{\"-1\":{\"query_offset\":-1001}}}}"));
        state.Record(Parse(3, "\"replyq\":5,\"tx\":5,\"topics\":{\"t\":{\"partitions\":{\"-1\":{\"query_offset\":-1001}}}}"));
        pipeline.Flush();
        var records = pipeline.Metrics.Records.Where(r => r.Tags.TryGetValue("run", out var run) && Equals(run, "sdk-native")).ToArray();
        var gauge = Assert.Single(records, r => r.Name == "kafka.librdkafka.client.replyq");
        Assert.Equal(5, gauge.Gauge);
        Assert.Equal("{operation}", gauge.Unit);
        var counter = Assert.Single(records, r => r.Name == "kafka.librdkafka.client.tx");
        Assert.Equal(5, counter.Sum);
        var partition = Assert.Single(records, r => r.Name == "kafka.librdkafka.topic.partition.query_offset");
        Assert.Equal(-1001, partition.Gauge);
        Assert.Equal(-1, partition.Tags["partition"]);
        Assert.All(records, record =>
        {
            Assert.DoesNotContain("field", record.Tags.Keys);
            Assert.DoesNotContain("attempt", record.Tags.Keys);
            Assert.DoesNotContain("key", record.Tags.Keys);
            Assert.DoesNotContain("reason", record.Tags.Keys);
        });
    }

    [Fact]
    public void Given_MalformedMiddleAndReset_When_ExportedThroughSdk_Then_CounterStateIsTransactionalAndSticky()
    {
        using var pipeline = new TelemetryTestPipeline();
        using var metrics = new KafkaStatisticsMetrics();
        var state = metrics.CreateNativeState(new KafkaStatisticsIdentity("A01", "sdk-state", KafkaClientRole.LoadGenProducer, "client"), false);
        state.Record(Parse(1, "\"tx\":2"));
        state.Record(Parse(2, "\"tx\":5,\"topics\":{\"t\":{\"age\":\"bad\"}}"));
        state.Record(Parse(3, "\"tx\":7"));
        pipeline.Flush();
        var first = Assert.Single(pipeline.Metrics.Records, r => r.Name == "kafka.librdkafka.client.tx" && Equals(r.Tags["run"], "sdk-state"));
        Assert.Equal(7, first.Sum);
        state.Record(Parse(4, "\"tx\":2"));
        state.Record(Parse(5, "\"tx\":9"));
        pipeline.Flush();
        Assert.Contains(state.Diagnostics, d => d.Code == "counter_reset");
        Assert.All(pipeline.Metrics.Records.Where(r => r.Name == "kafka.librdkafka.client.tx" && Equals(r.Tags["run"], "sdk-state")), r => Assert.Equal(7, r.Sum));
    }

    [Fact]
    public void Given_FixedNativeRegistry_When_Exported_Then_NoInstrumentOrPointIsSilentlyLost()
    {
        using var pipeline = new TelemetryTestPipeline();
        using var metrics = new KafkaStatisticsMetrics();
        using var authMeter = new Meter("Parchwell.Kafka.Authentication");
        using var workerMeter = new Meter("Parchwell.Workers");
        authMeter.CreateCounter<long>("parchwell.kafka.oauthbearer.refresh_handler.errors", "count").Add(1);
        workerMeter.CreateCounter<long>("parchwell.worker.operations").Add(1);
        workerMeter.CreateHistogram<double>("parchwell.worker.operation.duration_ms", "ms").Record(1);
        workerMeter.CreateCounter<long>("parchwell.loadgen.application_retry.events", "count").Add(1);
        workerMeter.CreateHistogram<double>("parchwell.worker.duration_ms", "ms").Record(1);
        workerMeter.CreateCounter<long>("parchwell.worker.completed").Add(1);
        var identity = new KafkaStatisticsIdentity("A01", "sdk-registry", KafkaClientRole.LoadGenProducer, "client");
        var state = metrics.CreateNativeState(identity, false);
        foreach (var row in NativeMetricContract.Rows)
        {
            var root = JsonNode.Parse("{\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":1,\"time\":1,\"age\":1}")!.AsObject();
            string path = row.Path.Replace("brokers.*", "brokers.b", StringComparison.Ordinal)
                .Replace("topics.*", "topics.t", StringComparison.Ordinal)
                .Replace("partitions.*", "partitions.-1", StringComparison.Ordinal)
                .Replace("req.*", "req.Produce", StringComparison.Ordinal);
            string[] segments = path.Split('.');
            JsonObject parent = root;
            foreach (string segment in segments[..^1])
            {
                parent[segment] ??= new JsonObject();
                parent = parent[segment]!.AsObject();
            }
            if (row.Policy == "W+") parent["cnt"] = 1;
            parent[segments[^1]] = row.Policy switch { "B" => JsonValue.Create(true), "S" => JsonValue.Create("unknown"), _ => JsonValue.Create(2L) };
            var projection = KafkaNativeMetricParser.Parse(root.ToJsonString());
            Assert.True(projection.IsValid, row.Path);
            state.Record(projection);
        }
        metrics.Record(identity, new KafkaBrokerStatistics("b", 1, "UP", 1, 1, 1, 0, 0, 0,
            new KafkaWindowStatistics(1, 2), new KafkaWindowStatistics(1, 2)), 2, 2);
        pipeline.Flush();
        var records = pipeline.Metrics.Records.Where(r => r.Tags.TryGetValue("run", out var run) && Equals(run, "sdk-registry")).ToArray();
        var native = records.Where(r => r.Name.StartsWith("kafka.librdkafka.", StringComparison.Ordinal)).ToArray();
        Assert.Equal(NativeMetricContract.Rows.Select(row => row.Name).Order(), native.Select(r => r.Name).Distinct().Order());
        foreach (var row in NativeMetricContract.Rows)
        {
            var points = native.Where(r => r.Name == row.Name).ToArray();
            Assert.NotEmpty(points);
            Assert.All(points, point =>
            {
                Assert.Equal(row.Unit, point.Unit);
                Assert.Equal(row.Kind == "Gauge`1", point.Gauge.HasValue);
                Assert.DoesNotContain("otel.metric.overflow", point.Tags.Keys);
                Assert.Equal("client", point.Tags["client_id"]);
                Assert.Equal("native", point.Tags["native_client_name"]);
            });
        }
        Assert.Equal(4, records.Select(r => r.Name).Distinct().Count(name => name.StartsWith("kafka.broker.", StringComparison.Ordinal)));
        foreach (string meter in new[] { "parchwell.kafka.oauthbearer.refresh_handler.errors", "parchwell.worker.operations", "parchwell.worker.operation.duration_ms", "parchwell.loadgen.application_retry.events", "parchwell.worker.duration_ms", "parchwell.worker.completed" })
            Assert.Contains(pipeline.Metrics.Records, r => r.Name == meter);
    }

    [Fact]
    public void Given_PointCapacity_When_AnExcessTupleArrives_Then_ExistingSdkPointsRemainAndGapIsSticky()
    {
        using var pipeline = new TelemetryTestPipeline();
        using var metrics = new KafkaStatisticsMetrics();
        KafkaNativeMetricState? last = null;
        for (int lifetime = 0; lifetime < 32; lifetime++)
        {
            var identity = new KafkaStatisticsIdentity("A01", "sdk-capacity-" + lifetime.ToString(CultureInfo.InvariantCulture), KafkaClientRole.LoadGenProducer, "client");
            last = metrics.CreateNativeState(identity, false);
            int count = lifetime == 31 ? 16 : 64;
            last.Record(BrokerProjection(count, 1));
            Assert.True(last.IsComplete);
        }
        Assert.NotNull(last);
        last.Record(BrokerProjection(17, 2));
        Assert.Contains(last.Diagnostics, d => d.Code == "admission_limit");
        last.Record(BrokerProjection(16, 3));
        pipeline.Flush();
        var points = pipeline.Metrics.Records.Where(r => r.Name == "kafka.librdkafka.broker.nodeid" && r.Tags.TryGetValue("run", out var run) && ((string?)run)?.StartsWith("sdk-capacity-", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(2000, points.Length);
        Assert.DoesNotContain(points, p => p.Tags.ContainsKey("otel.metric.overflow"));
        Assert.Equal(16, points.Count(p => Equals(p.Tags["run"], "sdk-capacity-31") && p.Gauge == 3));
    }

    private static KafkaNativeMetricProjection BrokerProjection(int count, int value)
    {
        var brokers = new JsonObject();
        for (int index = 0; index < count; index++)
            brokers[index.ToString(CultureInfo.InvariantCulture)] = new JsonObject { ["nodeid"] = value };
        var root = JsonNode.Parse("{\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":1,\"time\":1,\"age\":1}")!.AsObject();
        root["brokers"] = brokers;
        return KafkaNativeMetricParser.Parse(root.ToJsonString());
    }

    private static KafkaNativeMetricProjection Parse(long ts, string fields) => KafkaNativeMetricParser.Parse(
        "{\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":" + ts + ",\"time\":1,\"age\":1," + fields + "}");
}
