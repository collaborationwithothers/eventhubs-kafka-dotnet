using System.Reflection;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.LoadGen.SingleActionV2;
using Parchwell.Testing;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleActionV2;

#pragma warning disable CA1707
public sealed class ProbeV2NativeStatisticsTests
{
    [Theory]
    [InlineData(4, 4)]
    [InlineData(32, 32)]
    [InlineData(32, 64)]
    public async Task Given_PinnedNativeCluster_When_TopicOnlyProduceCompletes_Then_FullFirstStatisticsSurviveCaptureAndFiveSlotWire(int partitions, int brokers)
    {
        string managed = typeof(ProducerBuilder<,>).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Assert.Equal("2.16.0", managed.Split('+')[0]);
        Assert.Equal("2.16.0", Library.VersionString);
        using var cluster = new ProbeV2MockCluster(brokers);
        string topic = partitions == 4 ? "document-events-dlq" : "document-events";
        cluster.CreateTopic(topic, partitions);
        ProbeV2Limits limits = ProbeV2Limits.Fixed();
        long started = TimeProvider.System.GetTimestamp();
        var capture = new ProbeV2Capture(limits, TimeProvider.System, started, 15000);
        var formerLimits = ProbeV2Limits.Fixed();
        formerLimits.MaxStatisticsReportBytes = 32768;
        var formerCapture = new ProbeV2Capture(formerLimits, TimeProvider.System, started, 15000);
        var statistics = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivery = new TaskCompletionSource<DeliveryReport<byte[], byte[]>>(TaskCreationOptions.RunContinuationsAsynchronously);
        string clientId = $"probe-v2-native-synthetic-{partitions}-{brokers}";
        var configuration = new ProducerConfig
        {
            BootstrapServers = cluster.BootstrapServers, ClientId = clientId,
            StatisticsIntervalMs = 1000, Acks = Acks.All, EnableIdempotence = true,
            MessageTimeoutMs = 5000
        };
        using IProducer<byte[], byte[]> producer = new ProducerBuilder<byte[], byte[]>(configuration)
            .SetStatisticsHandler((_, raw) =>
            {
                bool afterDelivery = capture.Terminal;
                capture.Statistics(raw);
                formerCapture.Statistics(raw);
                if (afterDelivery) statistics.TrySetResult(raw);
            }).Build();
        int[] metadata;
        using (IAdminClient admin = new DependentAdminClientBuilder(producer.Handle).Build())
        {
            Metadata response = admin.GetMetadata(topic, TimeSpan.FromSeconds(5));
            Assert.Equal(brokers, response.Brokers.Count);
            TopicMetadata described = Assert.Single(response.Topics);
            Assert.False(described.Error.IsError);
            metadata = described.Partitions.Select(partition => partition.PartitionId).Order().ToArray();
        }
        Assert.Equal(Enumerable.Range(0, partitions), metadata);
        byte[] key = Encoding.UTF8.GetBytes("synthetic-native-key");
        byte[] value = Encoding.UTF8.GetBytes("synthetic-native-value");
        // Deliberately use the topic-only overload: metadata must expose all partitions
        // even though this single synthetic message lands in only one of them.
        producer.Produce(topic, new Message<byte[], byte[]> { Key = key, Value = value }, report =>
        {
            capture.Delivery(report);
            formerCapture.Delivery(report);
            delivery.TrySetResult(report);
        });
        DeliveryReport<byte[], byte[]> delivered = await delivery.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(delivered.Error.IsError);
        Assert.Equal(PersistenceStatus.Persisted, delivered.Status);
        string raw = await statistics.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        byte[] nativeBytes = Encoding.UTF8.GetBytes(raw);
        using JsonDocument document = JsonDocument.Parse(nativeBytes);
        JsonElement root = document.RootElement;
        Assert.Equal("producer", root.GetProperty("type").GetString());
        Assert.Equal(1, root.GetProperty("txmsgs").GetInt64());
        Assert.Equal(brokers, root.GetProperty("brokers").EnumerateObject().Count());
        JsonProperty nativeTopic = Assert.Single(root.GetProperty("topics").EnumerateObject());
        Assert.Equal(topic, nativeTopic.Name);
        int[] reportedPartitions = nativeTopic.Value.GetProperty("partitions").EnumerateObject()
            .Select(partition => int.Parse(partition.Name, System.Globalization.CultureInfo.InvariantCulture)).Order().ToArray();
        Assert.Equal(Enumerable.Range(-1, partitions + 1), reportedPartitions);
        Assert.True(nativeBytes.Length < 262144);
        if (partitions == 32) Assert.True(nativeBytes.Length > 32768);
        else Assert.True(nativeBytes.Length < 32768);
        var requested = new ProbeV2Requested(topic, "automatic", null, null, Convert.ToBase64String(key), Convert.ToBase64String(value));
        Dictionary<string, string> settings = configuration.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        ProbeV2Repeat retained = Snapshot(capture, clientId, settings, metadata, requested);
        Assert.False(capture.Limit);
        Assert.Equal("success", retained.Result);
        Assert.DoesNotContain("capture_limit", retained.Diagnostics);
        ProbeV2Statistics retainedStatistics = Assert.Single(retained.Statistics);
        Assert.True(retainedStatistics.Sequence > retained.Delivery!.Sequence);
        Assert.InRange(retainedStatistics.ObservedElapsedMs - retained.Delivery.ObservedElapsedMs, 0, 5000);
        Assert.Equal(nativeBytes, Convert.FromBase64String(retainedStatistics.RawBase64));
        ProbeV2Repeat formerlyRetained = Snapshot(formerCapture, clientId, settings, metadata, requested);
        Assert.Equal(partitions == 32, formerCapture.Limit);
        if (partitions == 32)
        {
            Assert.Contains("capture_limit", formerlyRetained.Diagnostics);
            Assert.Empty(formerlyRetained.Statistics);
        }
        else Assert.Equal(nativeBytes, Convert.FromBase64String(Assert.Single(formerlyRetained.Statistics).RawBase64));

        // Exercise the full five-slot envelope with unmodified native report bytes in
        // every slot. This is serialization capacity, not five independent operations.
        ProbeV2Repeat[] repeats = Enumerable.Range(1, 5).Select(repeat => retained with { Repeat = repeat }).ToArray();
        var file = new ProbeV2CaptureFile(2, "single-action-v2", true, "native-synthetic", "B08", "C0", "action",
            new string('a', 40), "native-synthetic-job", "native-synthetic-pod", "00000000-0000-0000-0000-000000000001",
            "probe-v2", managed, Library.VersionString, repeats, 0, true, true);
        byte[] encoded = ProbeV2Wire.Encode(file, limits.MaxCaptureBytes);
        if (brokers == 64)
        {
            Assert.True(encoded.Length > 524288);
            Assert.Throws<InvalidDataException>(() => ProbeV2Wire.Encode(file, 524288));
        }
        ProbeV2CaptureFile decoded = ProbeV2Wire.Read<ProbeV2CaptureFile>(encoded, limits.MaxCaptureBytes);
        Assert.Equal(5, decoded.Repeats.Count);
        Assert.All(decoded.Repeats, repeat => Assert.Equal(nativeBytes, Convert.FromBase64String(Assert.Single(repeat.Statistics).RawBase64)));
        TestContext.Current.TestOutputHelper!.WriteLine($"native synthetic producer: bytes={nativeBytes.Length}, brokers={brokers}, metadata_partitions={metadata.Length}, native_partition_entries={reportedPartitions.Length}, five_slot_bytes={encoded.Length}");
        string? exportDirectory = Environment.GetEnvironmentVariable("PARCHWELL_PROBE_V2_NATIVE_EXPORT_DIRECTORY");
        if (!string.IsNullOrEmpty(exportDirectory))
        {
            Directory.CreateDirectory(exportDirectory);
            await File.WriteAllBytesAsync(Path.Combine(exportDirectory, $"native-producer-{partitions}-{brokers}.json"), nativeBytes, TestContext.Current.CancellationToken);
        }
        await AssertConsumerStatisticsAsync(cluster.BootstrapServers, delivered, partitions, brokers, requested, exportDirectory);
    }

    private static async Task AssertConsumerStatisticsAsync(string bootstrapServers, DeliveryReport<byte[], byte[]> delivered,
        int partitions, int brokers, ProbeV2Requested produced, string? exportDirectory)
    {
        string clientId = $"probe-v2-native-reader-synthetic-{partitions}-{brokers}";
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), TimeProvider.System, TimeProvider.System.GetTimestamp(), 15000);
        var statistics = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using IConsumer<byte[], byte[]> consumer = new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers, ClientId = clientId, GroupId = clientId,
            StatisticsIntervalMs = 1000, EnableAutoCommit = false, EnableAutoOffsetStore = false
        }).SetStatisticsHandler((_, raw) =>
        {
            bool afterRead = capture.Terminal;
            capture.Statistics(raw);
            if (afterRead) statistics.TrySetResult(raw);
        }).Build();
        int[] metadata;
        using (IAdminClient admin = new DependentAdminClientBuilder(consumer.Handle).Build())
        {
            Metadata response = admin.GetMetadata(delivered.Topic, TimeSpan.FromSeconds(5));
            Assert.Equal(brokers, response.Brokers.Count);
            metadata = Assert.Single(response.Topics).Partitions.Select(partition => partition.PartitionId).Order().ToArray();
        }
        Assert.Equal(Enumerable.Range(0, partitions), metadata);
        consumer.Assign(new TopicPartitionOffset(delivered.TopicPartition, delivered.Offset));
        ConsumeResult<byte[], byte[]>? observed = consumer.Consume(TimeSpan.FromSeconds(5));
        Assert.NotNull(observed);
        var requested = produced with { PartitionMode = "explicit", Partition = delivered.Partition.Value, Offset = delivered.Offset.Value };
        capture.Read(observed, requested);
        Assert.True(capture.Terminal);
        long started = TimeProvider.System.GetTimestamp();
        while (!statistics.Task.IsCompleted && TimeProvider.System.GetElapsedTime(started) < TimeSpan.FromSeconds(5))
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            consumer.Consume(TimeSpan.FromMilliseconds(100));
        }
        Assert.True(statistics.Task.IsCompletedSuccessfully, "The first post-read native statistics callback must arrive within five seconds.");
        byte[] raw = Encoding.UTF8.GetBytes(await statistics.Task);
        using JsonDocument document = JsonDocument.Parse(raw);
        JsonElement root = document.RootElement;
        Assert.Equal("consumer", root.GetProperty("type").GetString());
        Assert.Equal(1, root.GetProperty("rxmsgs").GetInt64());
        JsonProperty nativeTopic = Assert.Single(root.GetProperty("topics").EnumerateObject());
        Assert.Equal(delivered.Topic, nativeTopic.Name);
        int[] nativePartitions = nativeTopic.Value.GetProperty("partitions").EnumerateObject()
            .Select(partition => int.Parse(partition.Name, System.Globalization.CultureInfo.InvariantCulture)).Order().ToArray();
        Assert.Equal(Enumerable.Range(-1, partitions + 1), nativePartitions);
        Assert.True(raw.Length < 262144);
        if (partitions == 32) Assert.True(raw.Length > 32768);
        ProbeV2Repeat retained = capture.Snapshot(1, clientId, ProbeV2Stage.For("B08", "C0", "read"),
            new Dictionary<string, string>(), [], metadata, requested);
        Assert.False(capture.Limit);
        Assert.Equal("success", retained.Result);
        Assert.Equal(raw, Convert.FromBase64String(Assert.Single(retained.Statistics).RawBase64));
        TestContext.Current.TestOutputHelper!.WriteLine($"native synthetic consumer: bytes={raw.Length}, brokers={root.GetProperty("brokers").EnumerateObject().Count()}, metadata_partitions={metadata.Length}, native_partition_entries={nativePartitions.Length}");
        if (!string.IsNullOrEmpty(exportDirectory))
            await File.WriteAllBytesAsync(Path.Combine(exportDirectory, $"native-consumer-{partitions}-{brokers}.json"), raw, TestContext.Current.CancellationToken);
    }

    private static ProbeV2Repeat Snapshot(ProbeV2Capture capture, string clientId, Dictionary<string, string> settings,
        int[] metadata, ProbeV2Requested requested) => capture.Snapshot(1, clientId, ProbeV2Stage.For("B08", "C0", "action"),
            settings, [], metadata, requested);
}
