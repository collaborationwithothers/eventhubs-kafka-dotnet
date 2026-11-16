using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.Kafka.Tests.Telemetry;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707

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

    private static KafkaNativeMetricProjection Parse(long ts, string fields) => KafkaNativeMetricParser.Parse(
        "{\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":" + ts + ",\"time\":1,\"age\":1," + fields + "}");
}
