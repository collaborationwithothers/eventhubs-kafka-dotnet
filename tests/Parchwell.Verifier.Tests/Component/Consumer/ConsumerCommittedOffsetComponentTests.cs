using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Xunit;

namespace Parchwell.Verifier.Tests.Component.Consumer;

#pragma warning disable CA1707 // Repository test naming convention.
[Trait("Tier", "Docker")]
public sealed class ConsumerCommittedOffsetComponentTests
{
    private const string Topic = "document-events";
    private const string Group = "run1-c1-r1-consumers";
    private static readonly TopicPartition[] Partitions = [new(Topic, 0), new(Topic, 1)];

    [Fact]
    public async Task Given_TwoEmptyMembers_CT1_When_ClosedThenOffsetsRead_Then_BothOffsetsAreUnset()
    {
        await using LocalKafkaFixture fixture = await StartAsync();
        using IConsumer<byte[], byte[]> subject = Member(fixture.ObserverBootstrapServers, "subject");
        using IConsumer<byte[], byte[]> witness = Member(fixture.ObserverBootstrapServers, "witness");
        subject.Subscribe(Topic);
        witness.Subscribe(Topic);
        PollMembers(subject, witness, expectedMessages: 0);
        subject.Close();
        witness.Close();

        var observation = new OffsetObservation(fixture.ObserverBootstrapServers, TimeProvider.System);
        ConsumerOffsetRead read = await observation.ReadAsync(TimeSpan.FromSeconds(30));

        Assert.Equal("success", read.Outcome);
        Assert.Equal([0, 1], read.RequestedPartitions);
        Assert.Equal([new ConsumerObservedOffset(0, -1001, 0), new ConsumerObservedOffset(1, -1001, 0)], read.Offsets);
        observation.AssertReadOnlySingleCall();
    }

    [Fact]
    public async Task Given_ExplicitOffsetSeven_CT2_When_MembersCloseAndOffsetsRead_Then_GroupAndVerifierOffsetsStayIndependent()
    {
        await using LocalKafkaFixture fixture = await StartAsync();
        using IConsumer<byte[], byte[]> ordinary = Member(fixture.ObserverBootstrapServers, "ordinary", "parchwell-verifier");
        ordinary.Assign(new TopicPartitionOffset(Topic, 0, 0));
        ordinary.Commit([new TopicPartitionOffset(Topic, 0, 3)]);
        List<TopicPartitionOffset> before = ordinary.Committed(Partitions, TimeSpan.FromSeconds(30));
        using IConsumer<byte[], byte[]> subject = Member(fixture.ObserverBootstrapServers, "subject");
        using IConsumer<byte[], byte[]> witness = Member(fixture.ObserverBootstrapServers, "witness");
        subject.Subscribe(Topic);
        witness.Subscribe(Topic);
        PollMembers(subject, witness, expectedMessages: 0);
        await ProduceTenAsync(fixture.ObserverBootstrapServers);
        IReadOnlyList<ConsumeResult<byte[], byte[]>> consumed = PollMembers(subject, witness, expectedMessages: 10);
        Assert.Equal(Enumerable.Range(0, 10).Select(value => (long)value), consumed.Select(record => record.Offset.Value).Order());
        IConsumer<byte[], byte[]> partitionOwner = subject.Assignment.Contains(Partitions[0]) ? subject : witness;
        partitionOwner.Commit([new TopicPartitionOffset(Topic, 0, 7)]);
        subject.Close();
        witness.Close();

        var observation = new OffsetObservation(fixture.ObserverBootstrapServers, TimeProvider.System);
        ConsumerOffsetRead read = await observation.ReadAsync(TimeSpan.FromSeconds(30));

        Assert.Equal("success", read.Outcome);
        Assert.Equal([new ConsumerObservedOffset(0, 7, 0), new ConsumerObservedOffset(1, -1001, 0)], read.Offsets);
        observation.AssertReadOnlySingleCall();
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = fixture.ObserverBootstrapServers }).Build();
        DescribeConsumerGroupsResult groups = await admin.DescribeConsumerGroupsAsync([Group],
            new DescribeConsumerGroupsOptions { RequestTimeout = TimeSpan.FromSeconds(30) }).WaitAsync(TestContext.Current.CancellationToken);
        ConsumerGroupDescription group = Assert.Single(groups.ConsumerGroupDescriptions);
        Assert.False(group.Error.IsError, group.Error.Reason);
        Assert.Empty(group.Members);
        Assert.Equal(before, ordinary.Committed(Partitions, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task Given_ProxyTimeout_CT3_When_OffsetsReadWithOneMillisecond_Then_OneCallReportsKafkaException()
    {
        await using LocalKafkaFixture fixture = await StartAsync();
        await fixture.ResetProxyAsync("consumer-offset-timeout", TestContext.Current.CancellationToken);
        await fixture.ApplyFaultAsync(SpikeFault.Timeout, new TimingSettings(), TestContext.Current.CancellationToken);
        // Hold the test clock fixed so native construction cannot consume the one millisecond call budget.
        var observation = new OffsetObservation(fixture.ProducerBootstrapServers, new ManualKafkaTimeProvider());

        ConsumerOffsetRead read = await observation.ReadAsync(TimeSpan.FromMilliseconds(1));

        Assert.Equal("kafka_exception", read.Outcome);
        Assert.Equal((int)ErrorCode.Local_TimedOut, read.ErrorCode);
        Assert.Empty(read.Offsets);
        observation.AssertReadOnlySingleCall();
        Assert.Equal(TimeSpan.FromMilliseconds(1), observation.CallTimeout);
    }

    private static async Task<LocalKafkaFixture> StartAsync()
    {
        var fixture = new LocalKafkaFixture();
        try
        {
            await fixture.StartAsync(TestContext.Current.CancellationToken);
            using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = fixture.ObserverBootstrapServers }).Build();
            await admin.CreateTopicsAsync([new TopicSpecification { Name = Topic, NumPartitions = 2, ReplicationFactor = 1 }],
                new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(30), OperationTimeout = TimeSpan.FromSeconds(30) })
                .WaitAsync(TestContext.Current.CancellationToken);
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    private static IConsumer<byte[], byte[]> Member(string bootstrap, string client, string group = Group) =>
        new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
        {
            BootstrapServers = bootstrap, ClientId = "synthetic-" + client, GroupId = group,
            EnableAutoCommit = false, EnableAutoOffsetStore = false, AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = false, IsolationLevel = IsolationLevel.ReadCommitted,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.Range,
            SessionTimeoutMs = 6000, SocketTimeoutMs = 5000
        }).Build();

    private static List<ConsumeResult<byte[], byte[]>> PollMembers(IConsumer<byte[], byte[]> subject,
        IConsumer<byte[], byte[]> witness, int expectedMessages)
    {
        var records = new List<ConsumeResult<byte[], byte[]>>();
        // Fixed local harness bound: each iteration polls both members for at most 50 ms each.
        for (int attempt = 0; attempt < 600; attempt++)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            foreach (IConsumer<byte[], byte[]> member in new[] { subject, witness })
            {
                ConsumeResult<byte[], byte[]>? record = member.Consume(TimeSpan.FromMilliseconds(50));
                if (record is not null) records.Add(record);
            }
            if (subject.Assignment.Count == 1 && witness.Assignment.Count == 1 &&
                subject.Assignment.Concat(witness.Assignment).Distinct().Count() == 2 && records.Count == expectedMessages)
                return records;
        }
        throw new TimeoutException($"Synthetic members did not hold both assignments and consume the planned messages: subject={string.Join(',', subject.Assignment)};witness={string.Join(',', witness.Assignment)};messages={records.Count};expected={expectedMessages}.");
    }

    private static async Task ProduceTenAsync(string bootstrap)
    {
        using var producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig
        { BootstrapServers = bootstrap, Acks = Acks.All, EnableIdempotence = false }).Build();
        for (int index = 0; index < 10; index++)
        {
            DeliveryResult<byte[], byte[]> delivered = await producer.ProduceAsync(Partitions[0], new Message<byte[], byte[]>
            { Key = Encoding.UTF8.GetBytes($"synthetic-{index}"), Value = "synthetic-consumer-offset"u8.ToArray() }, TestContext.Current.CancellationToken);
            Assert.Equal(PersistenceStatus.Persisted, delivered.Status);
        }
    }

    private sealed class OffsetObservation(string bootstrap, TimeProvider clock)
    {
        private static readonly string[] ForbiddenOperations=["Subscribe", "Assign", "Consume", "Commit", "StoreOffset", "StoreOffsets", "Close"];
        private readonly List<string> calls = [];
        internal TimeSpan? CallTimeout { get; private set; }

        internal Task<ConsumerOffsetRead> ReadAsync(TimeSpan timeout)
        {
            var reader = new ConsumerCommittedOffsetReader(_ =>
            {
                IConsumer<byte[], byte[]> native = Member(bootstrap, "offset-reader");
                return NativeProxy.Create<IConsumer<byte[], byte[]>>((method, arguments) => Invoke(native, method, arguments));
            }, clock);
            return reader.ReadAsync([0, 1], clock.GetUtcNow() + timeout, TestContext.Current.CancellationToken);
        }

        private object? Invoke(IConsumer<byte[], byte[]> native, MethodInfo method, object?[]? arguments)
        {
            calls.Add(method.Name);
            Assert.DoesNotContain(method.Name, ForbiddenOperations);
            if (method.Name == "Committed")
            {
                Assert.Equal(Partitions, Assert.IsAssignableFrom<IEnumerable<TopicPartition>>(arguments![0]));
                CallTimeout = Assert.IsType<TimeSpan>(arguments[1]);
            }
            try { return method.Invoke(native, arguments); }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        }

        internal void AssertReadOnlySingleCall() => Assert.Equal(["Committed", "Dispose"], calls);
    }
}
#pragma warning restore CA1707
