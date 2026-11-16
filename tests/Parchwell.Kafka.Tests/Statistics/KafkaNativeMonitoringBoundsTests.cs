using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707

[Trait("Category", "Unit")]
public sealed class KafkaNativeMonitoringBoundsTests
{
    private const string Raw = "{\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":11,\"time\":1,\"age\":1}";
    private static readonly KafkaNativeMonitoringIdentity Identity = new("run", "phase", "scenario", "configuration", "job", "pod", "commit");
    private static readonly KafkaStatisticsIdentity ClientIdentity = new("scenario", "run", KafkaClientRole.LoadGenProducer, "client");

    [Fact]
    public async Task EmptyCoordinatorIsIncomplete()
    {
        var store = new Store();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new Clock(), Identity);
        await coordinator.CompleteAsync(CancellationToken.None);
        using var index = store.Index();
        Assert.Empty(index.RootElement.GetProperty("clients").EnumerateArray());
        Assert.False(index.RootElement.GetProperty("capture_complete").GetBoolean());
    }

    [Fact]
    public void ProductionBoundsAreFixed()
    {
        Assert.Equal(4096, KafkaNativeMonitoringCoordinator.MaxReferences);
        Assert.Equal(128, KafkaNativeMonitoringCoordinator.MaxDiagnostics);
        Assert.Equal(1048576, KafkaNativeMonitoringCoordinator.MaxReportBytes);
        Assert.Equal(1048576, KafkaNativeMonitoringCoordinator.MaxIndexBytes);
        Assert.Equal(134217728, KafkaNativeMonitoringCoordinator.MaxRawBytes);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task ReportByteBoundary(int offset, bool accepted)
    {
        string raw = Raw + new string(' ', KafkaNativeMonitoringCoordinator.MaxReportBytes + offset - Encoding.UTF8.GetByteCount(Raw));
        var store = new Store();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new Clock(), Identity);
        var client = coordinator.CreateLifetime(ClientIdentity);
        client.MarkBuilt();
        client.CaptureExisting(raw, Reference(raw));
        client.Complete(true, "unknown");
        await coordinator.CompleteAsync(CancellationToken.None);
        using var index = store.Index();
        Assert.Equal(accepted ? 1 : 0, index.RootElement.GetProperty("clients")[0].GetProperty("report_count").GetInt64());
        Assert.Equal(accepted, index.RootElement.GetProperty("capture_complete").GetBoolean());
    }

    [Theory]
    [InlineData(4094, 2)]
    [InlineData(4095, 1)]
    [InlineData(4096, 0)]
    public async Task ReferenceBoundary(int retained, int accepted)
    {
        var store = new Store();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new Clock(), Identity);
        var client = coordinator.CreateLifetime(ClientIdentity);
        client.MarkBuilt();
        coordinator.SetRetainedCountsForTest(retained, 0);
        client.CaptureExisting(Raw, Reference(Raw));
        string second = Report(12);
        client.CaptureExisting(second, Reference(second));
        client.Complete(true, "unknown");
        await coordinator.CompleteAsync(CancellationToken.None);
        using var index = store.Index();
        Assert.Equal(accepted, index.RootElement.GetProperty("clients")[0].GetProperty("report_count").GetInt64());
        if (accepted < 2) Assert.Contains("reference_limit", Diagnostics(index));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task RawAggregateBoundary(int offset, bool accepted)
    {
        byte[] row = JsonSerializer.SerializeToUtf8Bytes(new { ClientOrdinal = 1, ReportOrdinal = 1L, Raw }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        var store = new Store();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new Clock(), Identity);
        var client = coordinator.CreateLifetime(ClientIdentity);
        client.MarkBuilt();
        coordinator.SetRetainedCountsForTest(0, KafkaNativeMonitoringCoordinator.MaxRawBytes - row.Length - 1 + offset);
        client.CaptureNew(Raw);
        client.Complete(true, "unknown");
        await coordinator.CompleteAsync(CancellationToken.None);
        using var index = store.Index();
        Assert.Equal(accepted, index.RootElement.GetProperty("capture_complete").GetBoolean());
        Assert.Equal(accepted ? 1 : 0, index.RootElement.GetProperty("clients")[0].GetProperty("raw_sources").GetArrayLength());
        if (!accepted) Assert.Contains("raw_limit", Diagnostics(index));
    }

    [Fact]
    public async Task QueueHoldsExactly128WaitingCallbacksAndOverflowIsSticky()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Hold() { entered.TrySetResult(); await release.Task; }
        var store = new Store();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new Clock(), Identity, Hold);
        var client = coordinator.CreateLifetime(ClientIdentity);
        client.MarkBuilt();
        client.CaptureExisting(Raw, Reference(Raw));
        await entered.Task;
        for (int i = 0; i < 128; i++)
        {
            string raw = Report(12 + i);
            client.CaptureExisting(raw, Reference(raw));
        }
        string overflow = Report(140);
        client.CaptureExisting(overflow, Reference(overflow));
        release.SetResult();
        client.Complete(true, "unknown");
        await coordinator.CompleteAsync(CancellationToken.None);
        using var index = store.Index();
        Assert.Equal(129, index.RootElement.GetProperty("clients")[0].GetProperty("report_count").GetInt64());
        Assert.Contains("queue_overflow", Diagnostics(index));
        Assert.False(index.RootElement.GetProperty("capture_complete").GetBoolean());
    }

    [Theory]
    [InlineData(127, false)]
    [InlineData(128, false)]
    [InlineData(129, true)]
    public async Task DiagnosticLimitIsStickyAndRetains128DistinctRecords(int count, bool truncated)
    {
        var store = new Store();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new Clock(), Identity);
        var client = coordinator.CreateLifetime(ClientIdentity);
        client.MarkBuilt();
        for (int i = 0; i < count; i++) client.MarkIncomplete($"diagnostic_{i}");
        client.MarkIncomplete("diagnostic_0");
        client.Complete(true, "unknown");
        await coordinator.CompleteAsync(CancellationToken.None);
        using var index = store.Index();
        Assert.Equal(Math.Min(count, 128), Diagnostics(index).Length);
        Assert.Equal(truncated, index.RootElement.GetProperty("diagnostics_truncated").GetBoolean());
        Assert.False(index.RootElement.GetProperty("capture_complete").GetBoolean());
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task IndexByteBoundary(int offset, bool accepted)
    {
        var baseline = new Store();
        using var metrics = new KafkaStatisticsMetrics();
        var small = new KafkaNativeMonitoringCoordinator(baseline, metrics, new Clock(), Identity with { RunId = "r" });
        var smallClient = small.CreateLifetime(ClientIdentity);
        smallClient.MarkBuilt();
        smallClient.Complete(true, "unknown");
        await small.CompleteAsync(CancellationToken.None);
        int baselineLength = baseline.IndexBytes().Length;
        var store = new Store();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new Clock(), Identity with { RunId = new string('r', 1 + KafkaNativeMonitoringCoordinator.MaxIndexBytes + offset - baselineLength) });
        var client = coordinator.CreateLifetime(ClientIdentity);
        client.MarkBuilt();
        client.Complete(true, "unknown");
        if (accepted)
        {
            await coordinator.CompleteAsync(CancellationToken.None);
            Assert.Equal(KafkaNativeMonitoringCoordinator.MaxIndexBytes + offset, store.IndexBytes().Length);
        }
        else await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.CompleteAsync(CancellationToken.None));
    }

    private static string Report(int ts) => Raw.Replace("\"ts\":11", $"\"ts\":{ts}", StringComparison.Ordinal).Replace("\"age\":1", $"\"age\":{ts - 10}", StringComparison.Ordinal);
    private static string[] Diagnostics(JsonDocument index) => index.RootElement.GetProperty("clients")[0].GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()!).ToArray();
    private static KafkaNativeRawReference Reference(string raw)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(raw);
        return new("evidence/blob", 1, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
    private sealed class Clock : TimeProvider
    {
        public override long GetTimestamp() => 10;
        public override long TimestampFrequency => 1_000_000;
    }
    private sealed class Store : IKafkaNativeMonitoringStore
    {
        private readonly Dictionary<string, byte[]> _objects = new(StringComparer.Ordinal);
        public Task CreateAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _objects.Add(path, content.ToArray());
            return Task.CompletedTask;
        }
        public byte[] IndexBytes() => _objects.Single(x => x.Key.EndsWith("/index.json", StringComparison.Ordinal)).Value;
        public JsonDocument Index() => JsonDocument.Parse(IndexBytes());
    }
}
