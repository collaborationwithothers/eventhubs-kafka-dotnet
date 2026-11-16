using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Monitoring;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707
public sealed class NativeMonitoringHostTests
{
    [Fact]
    public async Task EmptyPod_ProducesAnExplicitZeroClientIndex()
    {
        var services = new ServiceCollection();
        NativeMonitoringFixture store = Register(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        await provider.GetRequiredService<NativeMonitoringHost>().CompleteAsync(true, TestContext.Current.CancellationToken);
        using JsonDocument index = Index(store);
        Assert.Empty(index.RootElement.GetProperty("clients").EnumerateArray());
    }

    [Fact]
    public async Task FailedHostCapture_ProducesIncompleteIndexEvenWithoutClients()
    {
        var services = new ServiceCollection();
        NativeMonitoringFixture store = Register(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        await provider.GetRequiredService<NativeMonitoringHost>().CompleteAsync(false, TestContext.Current.CancellationToken);
        using JsonDocument index = Index(store);
        Assert.False(index.RootElement.GetProperty("capture_complete").GetBoolean());
        Assert.Empty(index.RootElement.GetProperty("clients").EnumerateArray());
    }

    [Fact]
    public async Task BeforeBuildReturn_AdmittedReportAndFailedBuildRemainDistinct()
    {
        var services = new ServiceCollection();
        NativeMonitoringFixture store = Register(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        NativeMonitoringHost host = provider.GetRequiredService<NativeMonitoringHost>();
        var first = host.Begin(Identity("client-1"))!;
        host.Capture(first, "{}", 7);
        first.MarkBuilt();
        var failed = host.Begin(Identity("client-2"))!;
        failed.MarkBuildFailed();
        await host.CompleteAsync(true, TestContext.Current.CancellationToken);
        using JsonDocument index = Index(store);
        JsonElement[] clients = index.RootElement.GetProperty("clients").EnumerateArray().ToArray();
        Assert.Equal(2, clients.Length);
        Assert.Equal("built", clients[0].GetProperty("build_status").GetString());
        Assert.Equal(1, clients[0].GetProperty("report_count").GetInt32());
        Assert.Equal(7, clients[0].GetProperty("raw_sources")[0].GetProperty("report_ordinal").GetInt32());
        Assert.Equal("failed", clients[1].GetProperty("build_status").GetString());
        Assert.Equal(0, clients[1].GetProperty("report_count").GetInt32());
        Assert.False(index.RootElement.GetProperty("capture_complete").GetBoolean());
    }

    [Fact]
    public async Task ExpiredCleanupBudget_DoesNotPublishAnIndex()
    {
        var services = new ServiceCollection();
        NativeMonitoringFixture store = Register(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        using var deadline = new CancellationTokenSource();
        await deadline.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetRequiredService<NativeMonitoringHost>().CompleteAsync(true, deadline.Token));
        Assert.Empty(store.Objects);
    }

    private static NativeMonitoringFixture Register(IServiceCollection services) => NativeMonitoringFixture.Register(services,
        "run-1", "B01", "C1", "job-1", "pod-uid", new string('a', 40), "ledger/run/capture.json");

    private static KafkaStatisticsIdentity Identity(string client) => new("B01", "run-1", KafkaClientRole.LoadGenProducer, client);

    private static JsonDocument Index(NativeMonitoringFixture store) => JsonDocument.Parse(
        Assert.Single(store.Objects, pair => pair.Key.EndsWith("/index.json", StringComparison.Ordinal)).Value);
}
