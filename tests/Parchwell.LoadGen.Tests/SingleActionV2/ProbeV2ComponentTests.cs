using System.Text;
using Azure.Core;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.SingleActionV2;
using Parchwell.LoadGen.Tests.Component;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleActionV2;

#pragma warning disable CA1707
public sealed class ProbeV2ComponentTests
{
    [Fact]
    public void Given_RealNativeUnassignedConsumer_When_Bootstrapping_Then_ConsumeInstallsAttachedOAuthToken()
    {
        var options = ProbeV2OptionsTests.Options("B08", "C0", "read");
        var tokens = new SyntheticTokenProvider();
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaTokenProvider>(tokens);
        services.AddParchwellKafka(KafkaClientRole.VerifierConsumer, value =>
        { value.NamespaceName = options.NamespaceName; value.ClientId = options.PodName; });
        services.AddParchwellKafkaAuthentication(value =>
        {
            value.ClientId = options.WorkloadIdentityClientId;
            value.TenantId = "00000000-0000-0000-0000-000000000004";
            value.TokenFilePath = "/unopened/synthetic-token";
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        KafkaAuthenticationFactory authentication = provider.GetRequiredService<KafkaAuthenticationFactory>();
        var settings = provider.GetRequiredService<KafkaConfigurationFactory>().CreateVerifierConsumer().Configuration
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        settings["bootstrap.servers"] = "127.0.0.1:1";
        var factory = new ProbeV2SessionFactory(authentication, options);
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), TimeProvider.System, TimeProvider.System.GetTimestamp(), 5000);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));
        var session = factory.Consumer(settings, capture, cancellation.Token);
        using IConsumer<byte[], byte[]> consumer = session.Client;
        Assert.Empty(consumer.Assignment);
        bool ready = ProbeV2Worker.Bootstrap(consumer, () => session.Authentication.InstalledTokenExpiresOn is not null,
            capture, new("document-events", "explicit", 30, 4, "aw==", "dg=="), 100, 5000, cancellation.Token);
        Assert.True(ready);
        Assert.NotNull(session.Authentication.InstalledTokenExpiresOn);
        Assert.Equal(1, tokens.Calls);
        Assert.Empty(consumer.Assignment);
    }

    [Fact]
    public async Task Given_LocalKafka_When_ProducingEmptyAndNull_Then_IndependentAssignedReadsPreserveWireValuesAndLocation()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        await using LocalKafkaFixture broker = await LocalKafkaFixture.StartAsync(deadline.Token);
        string topic = "probe-v2-component-" + Guid.NewGuid().ToString("N");
        using (IAdminClient admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = broker.BootstrapServers }).Build())
            await admin.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = 2, ReplicationFactor = 1 }],
                new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(20), OperationTimeout = TimeSpan.FromSeconds(20) }).WaitAsync(deadline.Token);
        foreach (byte[]? value in new byte[]?[] { [], null })
        {
            var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), TimeProvider.System, TimeProvider.System.GetTimestamp(), 330000);
            using IProducer<byte[], byte[]> producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig
            { BootstrapServers = broker.BootstrapServers, Acks = Acks.All, MessageTimeoutMs = 10000, Partitioner = Partitioner.ConsistentRandom }).Build();
            var completion = new TaskCompletionSource<DeliveryReport<byte[], byte[]>>(TaskCreationOptions.RunContinuationsAsynchronously);
            byte[] key = Encoding.UTF8.GetBytes("synthetic-null-control");
            producer.Produce(topic, new Message<byte[], byte[]> { Key = key, Value = value! }, report => { capture.Delivery(report); completion.SetResult(report); });
            DeliveryReport<byte[], byte[]> delivered = await completion.Task.WaitAsync(TimeSpan.FromSeconds(15), deadline.Token);
            Assert.False(delivered.Error.IsError);
            Assert.Equal(PersistenceStatus.Persisted, delivered.Status);
            using IConsumer<byte[], byte[]> consumer = new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
            { BootstrapServers = broker.BootstrapServers, GroupId = "probe-v2-observer-" + Guid.NewGuid().ToString("N"), EnableAutoCommit = false, EnablePartitionEof = true }).Build();
            consumer.Assign(new TopicPartitionOffset(delivered.TopicPartition, delivered.Offset));
            ConsumeResult<byte[], byte[]>? observed = consumer.Consume(TimeSpan.FromSeconds(15));
            Assert.NotNull(observed);
            var readerCapture = new ProbeV2Capture(ProbeV2Limits.Fixed(), TimeProvider.System, TimeProvider.System.GetTimestamp(), 120000);
            var requested = new ProbeV2Requested(topic, "explicit", delivered.Partition.Value, delivered.Offset.Value,
                Convert.ToBase64String(key), value is null ? null : Convert.ToBase64String(value));
            readerCapture.Read(observed, requested);
            ProbeV2Repeat record = readerCapture.Snapshot(1, "synthetic-reader", ProbeV2Stage.For("E01", "C1", "read"),
                new Dictionary<string, string>(), [], null, requested);
            ProbeV2Repeat decoded = ProbeV2Wire.Read<ProbeV2Repeat>(ProbeV2Wire.Encode(record, 524288), 524288);
            Assert.Equal(delivered.Offset.Value, decoded.ReadObservation.Record!.Offset);
            Assert.Equal(delivered.Partition.Value, decoded.ReadObservation.Record.Partition);
            Assert.Equal(requested.KeyBase64, decoded.ReadObservation.Record.KeyBase64);
            Assert.Equal(value is null ? null : "", decoded.ReadObservation.Record.ValueBase64);
        }
    }
    private sealed class SyntheticTokenProvider : IKafkaTokenProvider
    {
        internal int Calls;
        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref Calls);
            return new("synthetic-token", TimeProvider.System.GetUtcNow().AddHours(1));
        }
    }
}
