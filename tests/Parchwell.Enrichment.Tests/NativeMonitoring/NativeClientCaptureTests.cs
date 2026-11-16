#pragma warning disable CA1707
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Enrichment.NativeMonitoring;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Enrichment.Tests.NativeMonitoring;

public sealed class NativeClientCaptureTests
{
    [Fact]
    public async Task Given_two_client_lifetimes_When_completed_Then_index_keeps_attribution_and_zero_reports()
    {
        var store = new MemoryStore();
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaNativeMonitoringStore>(store);
        services.AddParchwellNativeMonitoring();
        using ServiceProvider provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<KafkaNativeMonitoringFactory>().Create(
            new KafkaNativeMonitoringIdentity("run", "C0", "F04", "C0", "job", "pod", "commit"));
        var capture = new NativeClientCapture(coordinator);
        var first = capture.Begin(new KafkaStatisticsIdentity("F04", "run", KafkaClientRole.EnrichmentProducer, "seed"));
        first.MarkBuilt();
        capture.Complete(true);
        var second = capture.Begin(new KafkaStatisticsIdentity("F04", "run", KafkaClientRole.EnrichmentConsumer, "consumer"));
        second.MarkBuildFailed();
        capture.Complete(false);
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(store.Objects.Single().Value);
        JsonElement clients = document.RootElement.GetProperty("clients");
        Assert.Equal(2, clients.GetArrayLength());
        Assert.Equal("EnrichmentProducer", clients[0].GetProperty("role").GetString());
        Assert.Equal("seed", clients[0].GetProperty("client_id").GetString());
        Assert.Equal(0, clients[0].GetProperty("report_count").GetInt32());
        Assert.Equal("built", clients[0].GetProperty("build_status").GetString());
        Assert.Equal("EnrichmentConsumer", clients[1].GetProperty("role").GetString());
        Assert.Equal("consumer", clients[1].GetProperty("client_id").GetString());
        Assert.Equal("failed", clients[1].GetProperty("build_status").GetString());
        Assert.False(document.RootElement.GetProperty("capture_complete").GetBoolean());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_native_callback_When_host_handler_runs_Then_exact_raw_is_retained_once(bool producer)
    {
        var store = new MemoryStore();
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaNativeMonitoringStore>(store);
        services.AddParchwellNativeMonitoring();
        using ServiceProvider provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<KafkaNativeMonitoringFactory>().Create(
            new KafkaNativeMonitoringIdentity("run", "C0", "F04", "C0", "job", "pod", "commit"));
        var capture = new NativeClientCapture(coordinator);
        KafkaClientRole role = producer ? KafkaClientRole.EnrichmentProducer : KafkaClientRole.EnrichmentConsumer;
        var lifetime = capture.Begin(new KafkaStatisticsIdentity("F04", "run", role, "client"));
        lifetime.MarkBuilt();
        const string raw = "{\"synthetic\": \"raw bytes preserved\"}";
        if (producer) NativeClientCapture.ProducerHandler(lifetime)(null!, raw);
        else NativeClientCapture.ConsumerHandler(lifetime)(null!, raw);
        capture.Complete(true);
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using JsonDocument row = JsonDocument.Parse(store.Objects.Single(pair => pair.Key.EndsWith(".jsonl", StringComparison.Ordinal)).Value);
        Assert.Equal(raw, row.RootElement.GetProperty("raw").GetString());
        using JsonDocument index = JsonDocument.Parse(store.Objects.Single(pair => pair.Key.EndsWith("index.json", StringComparison.Ordinal)).Value);
        Assert.Equal(1, index.RootElement.GetProperty("clients")[0].GetProperty("report_count").GetInt32());
    }

    private sealed class MemoryStore : IKafkaNativeMonitoringStore
    {
        internal Dictionary<string, byte[]> Objects { get; } = [];
        public Task CreateAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Objects.Add(path, content.ToArray());
            return Task.CompletedTask;
        }
    }
}
