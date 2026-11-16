using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.Run.Monitoring;
using Parchwell.LoadGen.SingleAction;
using Parchwell.LoadGen.Tests.SingleAction;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707
public sealed class NativeMonitoringCallbackTests
{
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
}
