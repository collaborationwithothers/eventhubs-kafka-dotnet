using System.Text.Json;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.Common.Monitoring;
using Parchwell.LoadGen.SingleAction;
using Parchwell.LoadGen.SingleActionV2;
using Parchwell.LoadGen.Tests.SingleAction;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707
public sealed class NativeMonitoringCallbackTests
{
    private static readonly string[] ExpectedSelectiveSourceKinds = ["raw_chunk", "existing_capture", "raw_chunk"];
    [Fact]
    public async Task RejectedSourceReport_LeavesAStickyGapAndIsNotObserved()
    {
        var services = new ServiceCollection();
        NativeMonitoringFixture store = NativeMonitoringFixture.Register(services, "run-1", "F04", "C2",
            "job-1", "pod-uid", new string('a', 40), "single-action/run/capture.json");
        using ServiceProvider provider = services.BuildServiceProvider();
        NativeMonitoringHost monitoring = provider.GetRequiredService<NativeMonitoringHost>();
        var lifetime = monitoring.Begin(new("F04", "run-1", KafkaClientRole.LoadGenProducer, "client-1"))!;
        lifetime.MarkBuilt();
        SingleActionHostOptions options = SingleActionCaptureTests.Options();
        options.MaxStatisticsReports = 1;
        var capture = new SingleActionCapture(options, TimeProvider.System, TimeProvider.System.GetTimestamp(), new(100));
        Action<string> callback = NativeMonitoringCallback.Create(capture.TryStatistics, monitoring, lifetime);
        callback("{}");
        capture.Drain();
        callback("{\"rejected\":true}");
        Assert.Single(capture.Snapshot().Statistics);
        await monitoring.CompleteAsync(true, TestContext.Current.CancellationToken);
        using JsonDocument index = JsonDocument.Parse(Assert.Single(store.Objects,
            pair => pair.Key.EndsWith("/index.json", StringComparison.Ordinal)).Value);
        JsonElement client = index.RootElement.GetProperty("clients")[0];
        Assert.Equal(1, client.GetProperty("report_count").GetInt32());
        Assert.Single(client.GetProperty("raw_sources").EnumerateArray());
        Assert.Contains(client.GetProperty("diagnostics").EnumerateArray(),
            value => value.GetString() == "raw_capture_rejected");
        Assert.False(client.GetProperty("capture_complete").GetBoolean());
    }
    [Fact]
    public async Task SelectiveV2_RetainsEveryCallbackAndSharesCounterState()
    {
        var values = new ConcurrentQueue<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meter) =>
        {
            if (instrument.Name == "kafka.librdkafka.client.txmsgs") meter.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "run" && Equals(tag.Value, "selective-run")) values.Enqueue(value);
        });
        listener.Start();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedClock());
        NativeMonitoringFixture store = NativeMonitoringFixture.Register(services, "selective-run", "F04", "C2",
            "job-1", "pod-uid", new string('a', 40), "single-action/run/capture.json");
        using ServiceProvider provider = services.BuildServiceProvider();
        NativeMonitoringHost monitoring = provider.GetRequiredService<NativeMonitoringHost>();
        var lifetime = monitoring.Begin(new("F04", "selective-run", KafkaClientRole.LoadGenProducer, "client-1"))!;
        lifetime.MarkBuilt();
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), provider.GetRequiredService<TimeProvider>(), 10, 0);
        Action<string> callback = NativeMonitoringCallback.CreateSelective(capture.TryStatistics, monitoring, lifetime, capture.MaxStatisticsReportBytes);
        callback(Raw(11, 2));
        callback(Raw(12, 5));
        capture.NoOutcome();
        callback(Raw(13, 9));
        callback(Raw(14, 15));
        await monitoring.CompleteAsync(true, TestContext.Current.CancellationToken);
        using JsonDocument index = JsonDocument.Parse(Assert.Single(store.Objects,
            pair => pair.Key.EndsWith("/index.json", StringComparison.Ordinal)).Value);
        JsonElement client = index.RootElement.GetProperty("clients")[0];
        Assert.True(client.GetProperty("capture_complete").GetBoolean());
        Assert.Equal(4, client.GetProperty("report_count").GetInt32());
        JsonElement[] sources = client.GetProperty("raw_sources").EnumerateArray().ToArray();
        Assert.Equal(ExpectedSelectiveSourceKinds, sources.Select(x => x.GetProperty("kind").GetString()));
        Assert.Equal(new long[] { 1, 3, 4 }, sources.Select(x => x.GetProperty("capture_ordinal").GetInt64()));
        Assert.Equal(new long[] { 2, 1, 1 }, sources.Select(x => x.GetProperty("row_count").GetInt64()));
        Assert.Equal(3, sources[1].GetProperty("report_ordinal").GetInt64());
        Assert.Equal(new long[] { 2, 3, 4, 6 }, values.ToArray());
        Assert.Equal(3, store.Objects.Where(pair => pair.Key.Contains("/raw-", StringComparison.Ordinal))
            .Sum(pair => System.Text.Encoding.UTF8.GetString(pair.Value).Count(c => c == '\n')));
    }

    [Theory]
    [InlineData(false, "invalid_utf8")]
    [InlineData(true, "report_limit")]
    public async Task SelectiveV2_InvalidUnselectedReportMarksMonitoringGap(bool oversized, string diagnostic)
    {
        var services = new ServiceCollection();
        NativeMonitoringFixture store = NativeMonitoringFixture.Register(services, "run-1", "F04", "C2",
            "job-1", "pod-uid", new string('a', 40), "single-action/run/capture.json");
        using ServiceProvider provider = services.BuildServiceProvider();
        NativeMonitoringHost monitoring = provider.GetRequiredService<NativeMonitoringHost>();
        var lifetime = monitoring.Begin(new("F04", "run-1", KafkaClientRole.LoadGenProducer, "client-1"))!;
        lifetime.MarkBuilt();
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), TimeProvider.System, TimeProvider.System.GetTimestamp(), 0);
        Action<string> callback = NativeMonitoringCallback.CreateSelective(capture.TryStatistics, monitoring, lifetime, capture.MaxStatisticsReportBytes);
        callback(oversized ? new string('x', 262145) : "\ud800");
        Assert.False(capture.Limit);
        await monitoring.CompleteAsync(true, TestContext.Current.CancellationToken);
        using JsonDocument index = JsonDocument.Parse(Assert.Single(store.Objects,
            pair => pair.Key.EndsWith("/index.json", StringComparison.Ordinal)).Value);
        JsonElement client = index.RootElement.GetProperty("clients")[0];
        Assert.False(client.GetProperty("capture_complete").GetBoolean());
        Assert.Equal(0, client.GetProperty("report_count").GetInt64());
        Assert.Contains(client.GetProperty("diagnostics").EnumerateArray(), item => item.GetString() == diagnostic);
    }

    private static string Raw(int timestamp, int count) =>
        $$"""{"name":"native","client_id":"client-1","type":"producer","ts":{{timestamp}},"time":1,"age":{{timestamp - 10}},"txmsgs":{{count}}}""";

    private sealed class FixedClock : TimeProvider
    {
        public override long GetTimestamp() => 10;
        public override long TimestampFrequency => 1_000_000;
    }

}
