using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707

[Trait("Category", "Unit")]
public sealed class KafkaStatisticsNativeBridgeTests
{
    [Fact]
    public async Task Given_MalformedOrdinaryProjection_When_LegacyCountersAdvance_Then_CanonicalDeltasFollowLegacyGuard()
    {
        const string run = "native-bridge-interaction";
        var values = new ConcurrentQueue<(string Name, long Value)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == "Parchwell.Kafka.Statistics") current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (tags.ToArray().Any(tag => tag.Key == "run" && Equals(tag.Value, run)))
                values.Enqueue((instrument.Name, value));
        });
        listener.Start();
        using var metrics = new KafkaStatisticsMetrics();
        var clock = new ManualTimeProvider();
        var sink = new RecordingStatisticsSink();
        await using var session = new KafkaStatisticsSession(
            new KafkaStatisticsIdentity("A01", run, KafkaClientRole.LoadGenProducer, "bridge-client"),
            StatisticsTestSupport.Options(), clock, sink, metrics, nativeClockCompatible: true);

        string[] reports = [MakeReport(11_000_000, 1_000_000, 2, false),
            MakeReport(11_500_000, 1_500_000, 5, true), MakeReport(12_000_000, 2_000_000, 7, false)];
        clock.Advance(TimeSpan.FromSeconds(3));
        for (int i = 0; i < reports.Length; i++)
        {
            session.Capture(reports[i]);
            await sink.WaitForReportsAsync(i + 1);
        }

        long[] For(string name) => values.Where(item => item.Name == name).Select(item => item.Value).ToArray();
        Assert.Equal([2L, 3L, 2L], For("kafka.broker.req_timeouts"));
        Assert.Equal([2L, 3L, 2L], For("kafka.librdkafka.broker.req_timeouts"));
        Assert.Equal([2L, 3L, 2L], For("kafka.broker.disconnects"));
        Assert.Equal([2L, 3L, 2L], For("kafka.librdkafka.broker.disconnects"));
        Assert.Equal([2L, 5L], For("kafka.librdkafka.client.tx"));
        KafkaStatisticsReport[] captured = sink.Reports.ToArray();
        Assert.Equal(reports, captured.Select(report => report.RawJson));
        Assert.Empty(captured[1].Diagnostics);
        Assert.False(captured[1].Monitoring!.IsComplete);
        Assert.Contains(captured[1].Monitoring!.Diagnostics, diagnostic => diagnostic.Code == "invalid_numeric");
        Assert.False(captured[2].Monitoring!.IsComplete);
    }

    private static string MakeReport(long ts, long age, long value, bool malformed) =>
        StatisticsTestSupport.Report("bridge-client", ts, age, value, value)
            .Replace("\"brokers\": {", $"\"tx\": {value}, \"topics\": {{ \"topic\": {{ \"age\": {(malformed ? "\"bad\"" : "1")} }} }}, \"brokers\": {{", StringComparison.Ordinal);
}
