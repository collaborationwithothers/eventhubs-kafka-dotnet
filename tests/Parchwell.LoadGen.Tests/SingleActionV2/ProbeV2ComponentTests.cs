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
    private static readonly DateTimeOffset MetadataE0 = new(2030, 1, 1, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_MetadataOptInProducer_When_BackgroundPollRuns_Then_ObservedSessionRecordsInstall()
    {
        ProbeV2Options options = ProbeV2OptionsTests.Options("B08", "C0");
        options.CaptureAuthenticationMetadata = true;
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaTokenProvider, FixedE0TokenProvider>();
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, value =>
        { value.NamespaceName = options.NamespaceName; value.ClientId = options.PodName; });
        services.AddParchwellKafkaAuthentication(value =>
        {
            value.ClientId = options.WorkloadIdentityClientId;
            value.TenantId = "00000000-0000-0000-0000-000000000004";
            value.TokenFilePath = "/unopened/synthetic-token";
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        KafkaAuthenticationFactory authentication = provider.GetRequiredService<KafkaAuthenticationFactory>();
        var settings = provider.GetRequiredService<KafkaConfigurationFactory>().CreateLoadGenProducer().Configuration
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        settings["bootstrap.servers"] = "127.0.0.1:1";
        settings["client.id"] = "synthetic-metadata-producer";
        var factory = new ProbeV2SessionFactory(authentication, options);
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), TimeProvider.System, TimeProvider.System.GetTimestamp(), 5000,
            captureAuthenticationMetadata: true);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        using IProducer<byte[], byte[]> producer = factory.Producer(settings, capture, deadline.Token);
        KafkaOAuthBearerSession session = Assert.IsType<KafkaOAuthBearerSession>(capture.AuthenticationSession);
        while (session.GetLifecycleSnapshot().Events.All(value => value.Stage != "installation_succeeded"))
            await Task.Delay(20, deadline.Token);
        KafkaAuthenticationLifecycleSnapshot snapshot = session.GetLifecycleSnapshot();
        Assert.Equal("synthetic-metadata-producer", snapshot.Identity.ClientId);
        Assert.Equal(MetadataE0, snapshot.Events.First(value => value.Stage == "installation_succeeded").InstalledExpiresOn);
        Assert.Equal(MetadataE0, session.InstalledTokenExpiresOn);
    }

    [Fact]
    public void Given_MetadataOptInConsumer_When_ConsumeRuns_Then_ObservedSessionRecordsInstall()
    {
        var options = ProbeV2OptionsTests.Options("B08", "C0", "read");
        options.CaptureAuthenticationMetadata = true;
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaTokenProvider, FixedE0TokenProvider>();
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
        settings["client.id"] = "synthetic-metadata-consumer";
        var factory = new ProbeV2SessionFactory(authentication, options);
        var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), TimeProvider.System, TimeProvider.System.GetTimestamp(), 5000,
            captureAuthenticationMetadata: true);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var result = factory.Consumer(settings, capture, deadline.Token);
        using IConsumer<byte[], byte[]> consumer = result.Client;
        Assert.True(ProbeV2Worker.Bootstrap(consumer, () => result.Authentication.InstalledTokenExpiresOn is not null,
            capture, new("document-events", "explicit", 30, 4, "aw==", "dg=="), 100, 5000, deadline.Token));
        KafkaAuthenticationLifecycleSnapshot snapshot = result.Authentication.GetLifecycleSnapshot();
        Assert.Same(result.Authentication, capture.AuthenticationSession);
        Assert.Equal("synthetic-metadata-consumer", snapshot.Identity.ClientId);
        Assert.Equal(MetadataE0, snapshot.Events.First(value => value.Stage == "installation_succeeded").InstalledExpiresOn);
        Assert.Equal(MetadataE0, result.Authentication.InstalledTokenExpiresOn);
    }

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

    [Fact]
    public async Task Given_LocalKafka_When_SameClientsProduceAndSeekReread_ThenRawHandlerDiagnosticRetainsServiceOperations()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        await using LocalKafkaFixture broker = await LocalKafkaFixture.StartAsync(deadline.Token);
        foreach (byte[]? value in new byte[]?[] { [], null })
        {
            string topic = "probe-v2-refresh-" + Guid.NewGuid().ToString("N");
            using (IAdminClient admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = broker.BootstrapServers }).Build())
                await admin.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }],
                    new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(20), OperationTimeout = TimeSpan.FromSeconds(20) }).WaitAsync(deadline.Token);
            byte[] key = Encoding.UTF8.GetBytes("synthetic-original-key");
            var producerPath = new ProbeV2HandlerRefreshPath(TimeProvider.System, 1, "synthetic-producer");
            using IProducer<byte[], byte[]> producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig
            { BootstrapServers = broker.BootstrapServers, Acks = Acks.All, MessageTimeoutMs = 10000 }).Build();
            var firstDelivery = new TaskCompletionSource<DeliveryReport<byte[], byte[]>>(TaskCreationOptions.RunContinuationsAsynchronously);
            producer.Produce(topic, new Message<byte[], byte[]> { Key = key, Value = value! }, report =>
            { producerPath.ObserveBeforeDelivery(report); firstDelivery.TrySetResult(report); });
            DeliveryReport<byte[], byte[]> original = await firstDelivery.Task.WaitAsync(TimeSpan.FromSeconds(15), deadline.Token);
            Assert.False(original.Error.IsError);
            Assert.Equal(PersistenceStatus.Persisted, original.Status);

            var consumerPath = new ProbeV2HandlerRefreshPath(TimeProvider.System, 1, "synthetic-consumer");
            using IConsumer<byte[], byte[]> consumer = new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
            { BootstrapServers = broker.BootstrapServers, GroupId = "probe-v2-refresh-" + Guid.NewGuid().ToString("N"), EnableAutoCommit = false }).Build();
            consumer.Assign(new TopicPartitionOffset(original.TopicPartition, original.Offset));
            var requested = new ProbeV2Requested(topic, "explicit", original.Partition.Value, original.Offset.Value,
                Convert.ToBase64String(key), value is null ? null : Convert.ToBase64String(value));
            ConsumeResult<byte[], byte[]>? initialRead = consumer.Consume(TimeSpan.FromSeconds(15));
            Assert.NotNull(initialRead);
            var scientificCapture = new ProbeV2Capture(ProbeV2Limits.Fixed(), TimeProvider.System,
                TimeProvider.System.GetTimestamp(), 120000);
            scientificCapture.Read(initialRead, requested);
            consumerPath.ObserveBeforeRead(initialRead, requested);

            producerPath.StartWindow(TimeProvider.System.GetTimestamp());
            consumerPath.StartWindow(TimeProvider.System.GetTimestamp());
            using var observationDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            observationDeadline.CancelAfter(TimeSpan.FromSeconds(20));
            Assert.True(producerPath.TryAdmit(3, observationDeadline.Token));
            Assert.True(consumerPath.TryAdmit(3, observationDeadline.Token));
            var secondDelivery = new TaskCompletionSource<DeliveryReport<byte[], byte[]>>(TaskCreationOptions.RunContinuationsAsynchronously);
            byte[] diagnosticKey = Encoding.UTF8.GetBytes("handler-refresh:run-1:C6:1");
            byte[] diagnosticValue = Encoding.UTF8.GetBytes("synthetic-handler-refresh-path");
            producer.Produce(new TopicPartition(topic, original.Partition),
                new Message<byte[], byte[]> { Key = diagnosticKey, Value = diagnosticValue }, report =>
                { producerPath.ObserveAfterDelivery(report); secondDelivery.TrySetResult(report); });
            DeliveryReport<byte[], byte[]> diagnostic = await secondDelivery.Task.WaitAsync(observationDeadline.Token);
            Assert.False(diagnostic.Error.IsError);
            Assert.Equal(PersistenceStatus.Persisted, diagnostic.Status);
            Assert.Equal(topic, diagnostic.Topic);
            Assert.Equal(original.Partition, diagnostic.Partition);

            consumer.Seek(new TopicPartitionOffset(original.TopicPartition, original.Offset));
            consumerPath.SeekReturned();
            while (consumerPath.CanContinue(observationDeadline.Token))
            {
                TimeSpan slice = consumerPath.Slice(100, observationDeadline.Token);
                if (slice <= TimeSpan.Zero) break;
                ConsumeResult<byte[], byte[]>? reread = consumer.Consume(slice);
                if (consumerPath.ObserveDiagnosticRead(reread, requested)) break;
            }
            ProbeV2HandlerRefreshMetadata producerMetadata = producerPath.Finish(true, false);
            ProbeV2HandlerRefreshMetadata consumerMetadata = consumerPath.Finish(true, false);
            Assert.Equal("delivery", producerMetadata.BeforeService!.Action);
            Assert.Equal("Persisted", producerMetadata.AfterService!.PersistenceStatus);
            Assert.Equal(Convert.ToBase64String(diagnosticKey), Convert.ToBase64String(diagnostic.Message.Key));
            Assert.Equal(Convert.ToBase64String(diagnosticValue), Convert.ToBase64String(diagnostic.Message.Value));
            Assert.Equal("read", consumerMetadata.BeforeService!.Action);
            Assert.Equal("read", consumerMetadata.AfterService!.Action);
            Assert.Equal(original.Offset.Value, consumerMetadata.AfterService.Offset);
            Assert.Equal(requested.KeyBase64, consumerMetadata.AfterService.KeyBase64);
            Assert.Equal(requested.ValueBase64, consumerMetadata.AfterService.ValueBase64);
            ProbeV2Repeat originalCapture = scientificCapture.Snapshot(1, "synthetic-consumer", ProbeV2Stage.For("E01", "C1", "read"),
                new Dictionary<string, string>(), [], null, requested);
            Assert.Equal(requested.ValueBase64, originalCapture.ReadObservation.Record!.ValueBase64);
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

    private sealed class FixedE0TokenProvider : IKafkaTokenProvider
    {
        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new("SYNTHETIC_TOKEN_MUST_NOT_APPEAR", MetadataE0);
        }
    }
}
