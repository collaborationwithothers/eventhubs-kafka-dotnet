using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707

[Trait("Category", "Unit")]
public sealed class KafkaNativeMonitoringCaptureTests
{
    private static readonly JsonSerializerOptions NativeRowOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_RejectedMiddleRawReport_When_LaterReportIsRetained_Then_CounterBaselineIgnoresRejectedReport(bool rawLimit)
    {
        string run = rawLimit ? "raw-admission-boundary" : "raw-admission-reference";
        var values = new ConcurrentQueue<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == "Parchwell.Kafka.Statistics" && instrument.Name == "kafka.librdkafka.client.tx")
                current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            if (tags.ToArray().Any(tag => tag.Key == "run" && Equals(tag.Value, run))) values.Enqueue(value);
        });
        listener.Start();
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new FixedTimeProvider(),
            new(run, "phase", "scenario", "configuration", "job", "pod", "commit"));
        var lifetime = coordinator.CreateLifetime(new("scenario", run, KafkaClientRole.LoadGenProducer, "client"));
        lifetime.MarkBuilt();
        string first = CounterReport(1, 2);
        string middle = CounterReport(2, 5);
        string last = CounterReport(3, 7);
        if (rawLimit)
        {
            int rowBytes = JsonSerializer.SerializeToUtf8Bytes(new { ClientOrdinal = 1, ReportOrdinal = 1L, Raw = first }, NativeRowOptions).Length + 1;
            coordinator.SetRetainedCountsForTest(0, KafkaNativeMonitoringCoordinator.MaxRawBytes - 2L * rowBytes);
            lifetime.CaptureNew(first);
            lifetime.CaptureNew(middle + new string(' ', 1024));
            lifetime.CaptureNew(last);
        }
        else
        {
            lifetime.CaptureExisting(first, ValidReference(first, 1));
            lifetime.CaptureExisting(middle, ValidReference(middle, 2) with { Sha256 = new string('0', 64) });
            lifetime.CaptureExisting(last, ValidReference(last, 3));
        }
        lifetime.Complete(true, "unknown");
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new long[] { 2, 5 }, values.ToArray());
        using var index = JsonDocument.Parse(store.Objects[$"{run}/phase/job/pod/native-monitoring/index.json"]);
        JsonElement client = index.RootElement.GetProperty("clients")[0];
        Assert.False(client.GetProperty("capture_complete").GetBoolean());
        Assert.Equal(3, client.GetProperty("report_count").GetInt64());
        Assert.Equal(new long[] { 1, 3 }, client.GetProperty("raw_sources").EnumerateArray().Select(source => source.GetProperty("capture_ordinal").GetInt64()));
        Assert.Contains(client.GetProperty("diagnostics").EnumerateArray(), item => item.GetString() == (rawLimit ? "raw_limit" : "invalid_reference"));
    }

    private static string CounterReport(int timestamp, int value) =>
        $"{{\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":{timestamp},\"time\":1,\"age\":{timestamp},\"tx\":{value}}}";

    private static KafkaNativeRawReference ValidReference(string raw, long ordinal)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(raw);
        return new("ledger/run/statistics.jsonl", ordinal, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Fact]
    public async Task Given_PendingHostDrain_When_ItFinishesWithinCleanupBudget_Then_IndexIsPublishedComplete()
    {
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var processing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new FixedTimeProvider(),
            new("run", "phase", "scenario", "configuration", "job", "pod", "commit"), () =>
            {
                processing.TrySetResult();
                return release.Task;
            });
        var lifetime = coordinator.CreateLifetime(new("scenario", "run", KafkaClientRole.LoadGenProducer, "client"));
        lifetime.MarkBuilt();
        string raw = CounterReport(1, 2);
        lifetime.CaptureNew(raw);
        lifetime.Complete(true, "unknown");
        await processing.Task.WaitAsync(TestContext.Current.CancellationToken);
        Task drain = coordinator.DrainAsync(TestContext.Current.CancellationToken);
        Task<bool> wait = coordinator.WaitForDrainAsync(drain, TestContext.Current.CancellationToken);
        Assert.False(wait.IsCompleted);
        Assert.Empty(store.Objects);
        release.SetResult();
        Assert.True(await wait);
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(store.Objects["run/phase/job/pod/native-monitoring/index.json"]);
        JsonElement client = document.RootElement.GetProperty("clients")[0];
        Assert.True(document.RootElement.GetProperty("capture_complete").GetBoolean());
        Assert.Equal("unknown", client.GetProperty("export_tail_status").GetString());
        Assert.Equal(1, client.GetProperty("report_count").GetInt64());
        string rawPath = client.GetProperty("raw_sources")[0].GetProperty("source").GetString()!;
        using var row = JsonDocument.Parse(store.Objects[rawPath]);
        Assert.Equal(raw, row.RootElement.GetProperty("raw").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_HostDrainCannotFinish_When_CleanupBudgetExpiresOrDrainFails_Then_GapIsSticky(bool expired)
    {
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, TimeProvider.System,
            new("run", "phase", "scenario", "configuration", "job", "pod", "commit"));
        var lifetime = coordinator.CreateLifetime(new("scenario", "run", KafkaClientRole.LoadGenProducer, "client"));
        lifetime.MarkBuilt();
        lifetime.Complete(true, "unknown");
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cleanup = new CancellationTokenSource();
        Task<bool> wait = coordinator.WaitForDrainAsync(pending.Task, cleanup.Token);
        Assert.False(wait.IsCompleted);
        if (expired) cleanup.Cancel();
        else pending.SetException(new InvalidOperationException("drain_failed"));
        Assert.False(await wait);
        Assert.Empty(store.Objects);
        if (expired) pending.SetResult();
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(store.Objects["run/phase/job/pod/native-monitoring/index.json"]);
        Assert.False(document.RootElement.GetProperty("capture_complete").GetBoolean());
        Assert.Equal("unknown", document.RootElement.GetProperty("clients")[0].GetProperty("export_tail_status").GetString());
    }

    [Fact]
    public async Task Given_MissingHostDrain_When_PublicationIsConsidered_Then_GapIsSticky()
    {
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, TimeProvider.System,
            new("run", "phase", "scenario", "configuration", "job", "pod", "commit"));
        var lifetime = coordinator.CreateLifetime(new("scenario", "run", KafkaClientRole.LoadGenProducer, "client"));
        lifetime.MarkBuilt();
        lifetime.Complete(true, "unknown");
        Assert.False(await coordinator.WaitForDrainAsync(null, TestContext.Current.CancellationToken));
        Assert.Empty(store.Objects);
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(store.Objects["run/phase/job/pod/native-monitoring/index.json"]);
        Assert.False(document.RootElement.GetProperty("capture_complete").GetBoolean());
    }

    [Fact]
    public async Task Given_CompletedHostDrain_When_CleanupBudgetAlreadyExpired_Then_GapIsSticky()
    {
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, TimeProvider.System,
            new("run", "phase", "scenario", "configuration", "job", "pod", "commit"));
        var lifetime = coordinator.CreateLifetime(new("scenario", "run", KafkaClientRole.LoadGenProducer, "client"));
        lifetime.MarkBuilt();
        lifetime.Complete(true, "unknown");
        using var cleanup = new CancellationTokenSource();
        cleanup.Cancel();
        Assert.False(await coordinator.WaitForDrainAsync(Task.CompletedTask, cleanup.Token));
        Assert.Empty(store.Objects);
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(store.Objects["run/phase/job/pod/native-monitoring/index.json"]);
        Assert.False(document.RootElement.GetProperty("capture_complete").GetBoolean());
    }

    [Fact]
    public async Task Given_ZeroReports_When_Completed_Then_IndexRecordsBuiltLifetime()
    {
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, TimeProvider.System,
            new("run", "phase", "scenario", "configuration", "job", "pod", "commit"));
        var lifetime = coordinator.CreateLifetime(new("scenario", "run", KafkaClientRole.LoadGenProducer, "client"));
        lifetime.MarkBuilt();
        lifetime.Complete(true, "unknown");
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(store.Objects["run/phase/job/pod/native-monitoring/index.json"]);
        var client = document.RootElement.GetProperty("clients")[0];
        Assert.Equal(0, client.GetProperty("report_count").GetInt64());
        Assert.Equal(JsonValueKind.Null, client.GetProperty("native_identity").ValueKind);
        Assert.Equal("built", client.GetProperty("build_status").GetString());
        Assert.Equal("unknown", client.GetProperty("export_tail_status").GetString());
        Assert.True(document.RootElement.GetProperty("capture_complete").GetBoolean());
    }

    [Fact]
    public async Task Given_InvalidExistingDigest_When_Completed_Then_GapIsStickyAndNoCopyIsWritten()
    {
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new FixedTimeProvider(),
            new("run", "phase", "scenario", "configuration", "job", "pod", "commit"));
        var lifetime = coordinator.CreateLifetime(new("scenario", "run", KafkaClientRole.LoadGenProducer, "client"));
        lifetime.MarkBuilt();
        lifetime.CaptureExisting("{\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":11,\"time\":1,\"age\":1}", new("evidence/blob", 1, 2, "wrong"));
        lifetime.Complete(true, "unknown");
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        Assert.Single(store.Objects);
        using var document = JsonDocument.Parse(store.Objects["run/phase/job/pod/native-monitoring/index.json"]);
        Assert.False(document.RootElement.GetProperty("capture_complete").GetBoolean());
        Assert.Equal("invalid_reference", document.RootElement.GetProperty("clients")[0].GetProperty("diagnostics")[0].GetString());
    }

    [Fact]
    public async Task Given_LifetimeLimit_When_AnotherBuildStarts_Then_AdmissionDoesNotThrowAndIndexIsIncomplete()
    {
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, TimeProvider.System,
            new("run", "phase", "scenario", "configuration", "job", "pod", "commit"));
        var identity = new KafkaStatisticsIdentity("scenario", "run", KafkaClientRole.LoadGenProducer, "client");
        for (int i = 0; i < 32; i++)
        {
            var lifetime = coordinator.CreateLifetime(identity);
            lifetime.MarkBuilt();
            lifetime.Complete(true, "unknown");
        }
        var refused = coordinator.CreateLifetime(identity);
        refused.MarkBuilt();
        refused.CaptureNew("{}");
        Assert.Same(refused, coordinator.CreateLifetime(identity));
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(store.Objects["run/phase/job/pod/native-monitoring/index.json"]);
        Assert.Equal(32, document.RootElement.GetProperty("clients").GetArrayLength());
        Assert.False(document.RootElement.GetProperty("capture_complete").GetBoolean());
    }

    [Fact]
    public async Task Given_InvalidFieldsWithValidHeader_When_TimestampRegresses_Then_IdentityGapIsRecorded()
    {
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new FixedTimeProvider(),
            new("run", "phase", "scenario", "configuration", "job", "pod", "commit"));
        var lifetime = coordinator.CreateLifetime(new("scenario", "run", KafkaClientRole.LoadGenProducer, "client"));
        lifetime.MarkBuilt();
        lifetime.CaptureNew("{\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":5,\"time\":1,\"age\":5,\"topics\":[]}");
        lifetime.CaptureNew("{\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":4,\"time\":1,\"age\":4}");
        lifetime.Complete(true, "unknown");
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(store.Objects["run/phase/job/pod/native-monitoring/index.json"]);
        var diagnostics = document.RootElement.GetProperty("clients")[0].GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("invalid_native_identity", diagnostics);
    }

    [Fact]
    public async Task Given_HostGapWithoutClients_When_Published_Then_IndexIsIncomplete()
    {
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, TimeProvider.System,
            new("run", "phase", "scenario", "configuration", "job", "pod", "commit"));
        coordinator.MarkIncomplete("host_capture_failed");
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(store.Objects["run/phase/job/pod/native-monitoring/index.json"]);
        Assert.Empty(document.RootElement.GetProperty("clients").EnumerateArray());
        Assert.False(document.RootElement.GetProperty("capture_complete").GetBoolean());
    }

    [Fact]
    public void Given_SourceCommit_When_Validating_Then_OnlyLowercaseFortyCharacterHexIsAccepted()
    {
        var options = new KafkaNativeMonitoringOptions
        {
            BlobEndpoint = "https://example.blob.core.windows.net", BlobContainer = "native-monitoring",
            RunId = "run", Phase = "phase", ScenarioId = "scenario", ConfigurationId = "configuration",
            JobName = "job", PodUid = "pod", SourceCommit = new string('a', 40)
        };
        Assert.True(options.IsValid(out _));
        options.SourceCommit = new string('A', 40);
        Assert.False(options.IsValid(out _));
        options.SourceCommit = "abc";
        Assert.False(options.IsValid(out _));
    }

    [Fact]
    public async Task Given_DrainedExistingCapture_When_PublishedAsDiagnostics_Then_ReferencesBindActualObject()
    {
        const string raw = "{\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":11,\"time\":1,\"age\":1}";
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new FixedTimeProvider(),
            new("run", "phase", "scenario", "configuration", "job", "pod", "commit"));
        var lifetime = coordinator.CreateLifetime(new("scenario", "run", KafkaClientRole.LoadGenProducer, "client"));
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        lifetime.MarkBuilt();
        lifetime.CaptureExisting(raw, new("ledger/run/publication/statistics.jsonl", 1, bytes.LongLength, hash));
        await coordinator.DrainAsync(TestContext.Current.CancellationToken);
        lifetime.RebindExistingSource("ledger/run/publication/statistics.jsonl", "ledger/run/diagnostics/statistics.jsonl");
        lifetime.Complete(true, "unknown");
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(Assert.Single(store.Objects).Value);
        var source = document.RootElement.GetProperty("clients")[0].GetProperty("raw_sources")[0];
        Assert.True(document.RootElement.GetProperty("capture_complete").GetBoolean());
        Assert.Equal("ledger/run/diagnostics/statistics.jsonl", source.GetProperty("source").GetString());
        Assert.Equal(hash, source.GetProperty("sha256").GetString());
        Assert.Equal(bytes.LongLength, source.GetProperty("byte_length").GetInt64());
        Assert.Equal(1, source.GetProperty("report_ordinal").GetInt64());
    }


    [Fact]
    public async Task Given_MixedCapture_When_SourceSequenceDiffers_Then_ChunksFollowNativeOrdinals()
    {
        static string Raw(int ts) => $$"""{"name":"native","client_id":"client","type":"producer","ts":{{ts}},"time":1,"age":{{ts - 10}}}""";
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new FixedTimeProvider(),
            new("run", "phase", "scenario", "configuration", "job", "pod", "commit"));
        var lifetime = coordinator.CreateLifetime(new("scenario", "run", KafkaClientRole.LoadGenProducer, "client"));
        lifetime.MarkBuilt();
        lifetime.CaptureNew(Raw(11));
        lifetime.CaptureNew(Raw(12));
        string selected = Raw(13);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(selected);
        lifetime.CaptureExisting(selected, new("ledger/run/capture.json", 7, bytes.Length,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))));
        lifetime.CaptureNew(Raw(14));
        lifetime.Complete(true, "unknown");
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using var index = JsonDocument.Parse(store.Objects["run/phase/job/pod/native-monitoring/index.json"]);
        var sources = index.RootElement.GetProperty("clients")[0].GetProperty("raw_sources").EnumerateArray().ToArray();
        Assert.True(index.RootElement.GetProperty("capture_complete").GetBoolean());
        Assert.Equal(new long[] { 1, 3, 4 }, sources.Select(source => source.GetProperty("capture_ordinal").GetInt64()));
        Assert.Equal(new long[] { 1, 7, 4 }, sources.Select(source => source.GetProperty("report_ordinal").GetInt64()));
        Assert.Equal(new long[] { 2, 1, 1 }, sources.Select(source => source.GetProperty("row_count").GetInt64()));
        Assert.Equal(2, store.Objects.Keys.Count(key => key.Contains("/raw-", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Given_InvalidReferenceBetweenPrivateRows_When_Published_Then_IncompleteIndexRetainsBothChunks()
    {
        const string raw = "{\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":11,\"time\":1,\"age\":1}";
        var store = new MemoryStore();
        using var metrics = new KafkaStatisticsMetrics();
        var coordinator = new KafkaNativeMonitoringCoordinator(store, metrics, new FixedTimeProvider(),
            new("run", "phase", "scenario", "configuration", "job", "pod", "commit"));
        var lifetime = coordinator.CreateLifetime(new("scenario", "run", KafkaClientRole.LoadGenProducer, "client"));
        lifetime.MarkBuilt();
        lifetime.CaptureNew(raw);
        lifetime.CaptureExisting(raw, new("ledger/run/capture.json", 7, 1, "invalid"));
        lifetime.CaptureNew(raw);
        lifetime.Complete(true, "unknown");
        await coordinator.CompleteAsync(TestContext.Current.CancellationToken);
        using var index = JsonDocument.Parse(store.Objects["run/phase/job/pod/native-monitoring/index.json"]);
        var client = index.RootElement.GetProperty("clients")[0];
        Assert.False(client.GetProperty("capture_complete").GetBoolean());
        Assert.Equal(3, client.GetProperty("report_count").GetInt64());
        Assert.Equal(new long[] { 1, 3 }, client.GetProperty("raw_sources").EnumerateArray().Select(source => source.GetProperty("capture_ordinal").GetInt64()));
        Assert.Contains(client.GetProperty("diagnostics").EnumerateArray(), item => item.GetString() == "invalid_reference");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override long GetTimestamp() => 10;
        public override long TimestampFrequency => 1_000_000;
    }

    private sealed class MemoryStore : IKafkaNativeMonitoringStore
    {
        internal Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);
        public Task CreateAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Objects.Add(path, content.ToArray());
            return Task.CompletedTask;
        }
    }
}
