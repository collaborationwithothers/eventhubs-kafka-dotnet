using System.Collections.Immutable;
using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Workload;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Component;

#pragma warning disable CA1707 // Repository test naming uses Given_When_Then.
public sealed class RunExecutorComponentTests
{
    private static readonly string[] ExpectedHeaderNames = ["tenant-id", "document-id", "sequence", "event-type", "instance-id", "attempt-id"];
    [Fact]
    public async Task Given_LocalKafka_When_RunExecutorProduces_Then_IndependentConsumerAndLedgerJoinOnExactIdentityAndHeaders()
    {
        using var startup = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using var broker = await LocalKafkaFixture.StartAsync(startup.Token);
        var topic = "loadgen-component-" + Guid.NewGuid().ToString("N");
        using (var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = broker.BootstrapServers }).Build())
        {
            await admin.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }],
                new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(20), OperationTimeout = TimeSpan.FromSeconds(20) })
                .WaitAsync(startup.Token);
        }

        var directory = NewDirectory();
        try
        {
            var identity = Identity();
            var profile = Profile();
            var ledgerPath = Path.Combine(directory, "ledger.jsonl");
            var manifestPath = Path.Combine(directory, "manifest.json");
            await using var ledger = NewLedger(ledgerPath, manifestPath);
            var producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig
            {
                BootstrapServers = broker.BootstrapServers,
                ClientId = "loadgen-component-producer",
                Acks = Acks.All,
                EnableIdempotence = false,
                MessageTimeoutMs = 10_000,
                SocketTimeoutMs = 5_000
            }).Build();
            var executor = new RunExecutor(() => producer, ledger, TimeProvider.System);

            var result = await executor.ExecuteAsync(profile, identity, Options(directory, topic), startup.Token);

            Assert.Equal(0, result.ExitCode);
            Assert.True(result.Manifest.BookkeepingComplete);
            Assert.Equal(1, result.Manifest.PlannedAttemptCount);
            Assert.Equal(1, result.Manifest.AttemptCount);
            var inventory = Assert.Single(result.Manifest.Inventory);
            var rows = await LedgerValidator.ReadValidatedAsync(ledgerPath, manifestPath, identity, 8, 4096,
                64 * 1024, 64 * 1024, startup.Token);
            var row = Assert.Single(rows);
            Assert.Equal(inventory.AttemptId, row.Attempt.AttemptId);
            Assert.Equal(inventory.InstanceId, row.Attempt.InstanceId);
            Assert.Equal(inventory.Key, row.Attempt.Key);
            Assert.True(row.Delivered);
            Assert.Equal((int)PersistenceStatus.Persisted, row.PersistenceStatus);

            using var consumer = new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
            {
                BootstrapServers = broker.BootstrapServers,
                GroupId = "loadgen-component-independent-observer",
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                AllowAutoCreateTopics = false
            }).Build();
            consumer.Assign(new TopicPartitionOffset(new TopicPartition(topic, new Partition(0)), Offset.Beginning));
            var consumed = consumer.Consume(TimeSpan.FromSeconds(15));
            Assert.NotNull(consumed);
            Assert.Equal(inventory.Key, Encoding.UTF8.GetString(consumed.Message.Key));
            var planned = Assert.Single(WorkloadGenerator.Generate(profile, 8, 256, startup.Token));
            Assert.Equal(WorkloadGenerator.GeneratePayload(planned, profile.Seed), consumed.Message.Value);
            Assert.Equal(inventory.InstanceId, Header(consumed.Message.Headers, "instance-id"));
            Assert.Equal(inventory.AttemptId, Header(consumed.Message.Headers, "attempt-id"));
            Assert.Equal(inventory.TenantId, Header(consumed.Message.Headers, "tenant-id"));
            Assert.Equal(inventory.DocumentId, Header(consumed.Message.Headers, "document-id"));
            Assert.Equal(inventory.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), Header(consumed.Message.Headers, "sequence"));
            Assert.Equal(inventory.EventType, Header(consumed.Message.Headers, "event-type"));
            Assert.Equal(ExpectedHeaderNames,
                consumed.Message.Headers.Select(header => header.Key));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Given_ScheduleInvocationExceedsLatenessBudget_When_Executing_Then_PersistsOnlyAdmittedPrefixAndFails()
    {
        var directory = NewDirectory();
        try
        {
            await using var ledger = NewLedger(Path.Combine(directory, "ledger.jsonl"), Path.Combine(directory, "manifest.json"));
            var clock = new ControlledClock();
            var native = CreateProducer<SlowFirstProduceProxy>();
            ((SlowFirstProduceProxy)(object)native).OnFirstProduce = () => clock.Advance(TimeSpan.FromMilliseconds(800));
            var result = await new RunExecutor(() => native, ledger, clock).ExecuteAsync(
                Profile() with { Stages = [new LoadStage("late-second", 2, 1)] }, Identity(),
                Options(directory, "unused") with { MaxScheduleLateness = TimeSpan.FromMilliseconds(25) },
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Equal(2, result.Manifest.PlannedAttemptCount);
            Assert.Equal(1, result.Manifest.AttemptCount);
            Assert.Equal(1, result.Manifest.FirstUnexecutedOrdinal);
            Assert.Contains("schedule_lateness_limit_exceeded", result.Manifest.Diagnostics);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Given_LedgerWriterFails_When_CallbackAppends_Then_ReturnsNonzeroWithoutCompleteCounts()
    {
        var directory = NewDirectory();
        try
        {
            var ledger = new LocalLedgerStore(new LedgerStoreOptions(Path.Combine(directory, "ledger.jsonl"),
                Path.Combine(directory, "manifest.json"), 8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)),
                () => new FailingWriteStream());
            var native = CreateProducer<SuccessfulCallbackProxy>();
            var result = await new RunExecutor(() => native, ledger, TimeProvider.System).ExecuteAsync(
                Profile(), Identity(), Options(directory, "unused"), TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Equal(1, result.Manifest.AttemptCount);
            Assert.Contains(result.Manifest.Diagnostics, diagnostic => diagnostic.StartsWith("ledger_drain_failed:", StringComparison.Ordinal));
            Assert.Contains(result.Manifest.Diagnostics, diagnostic => diagnostic.StartsWith("ledger_readback_failed:", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Given_NativeFlushNeverDrains_When_Executing_Then_ReturnsNonzeroAndLeavesInventoryUnreconciled()
    {
        var directory = NewDirectory();
        try
        {
            await using var ledger = NewLedger(Path.Combine(directory, "ledger.jsonl"), Path.Combine(directory, "manifest.json"));
            var native = CreateProducer<NeverDrainedProxy>();
            var result = await new RunExecutor(() => native, ledger, TimeProvider.System).ExecuteAsync(
                Profile(), Identity(), Options(directory, "unused") with { ProducerDrainTimeout = TimeSpan.FromMilliseconds(80) },
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Equal(1, result.Manifest.PlannedAttemptCount);
            Assert.Equal(1, result.Manifest.AttemptCount);
            Assert.Contains("producer_drain_timeout", result.Manifest.Diagnostics);
            Assert.Contains(result.Manifest.Diagnostics, diagnostic => diagnostic.StartsWith("ledger_readback_failed:", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Given_RequiredStatisticsEvidenceHasNoSession_When_Executing_Then_BlocksProducerAndSavesFailure()
    {
        var directory = NewDirectory();
        try
        {
            await using var ledger = NewLedger(Path.Combine(directory, "ledger.jsonl"), Path.Combine(directory, "manifest.json"));
            var constructed = false;
            var executor = new RunExecutor(() =>
            {
                constructed = true;
                return CreateProducer<SuccessfulCallbackProxy>();
            }, ledger, TimeProvider.System);

            var result = await executor.ExecuteAsync(Profile(), Identity(), Options(directory, "unused") with
            {
                ExpectedBrokerNames = ["broker-1"],
                RequireStatisticsEvidence = true
            }, TestContext.Current.CancellationToken);

            Assert.False(constructed);
            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Equal(1, result.Manifest.PlannedAttemptCount);
            Assert.Equal(0, result.Manifest.AttemptCount);
            Assert.Equal(0, result.Manifest.FirstUnexecutedOrdinal);
            Assert.Contains("execution_failed:InvalidOperationException", result.Manifest.Diagnostics);
            Assert.Contains("statistics_guard_unproven", result.Manifest.GuardObservations);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Given_NativeQueueCapacityIsOne_When_SecondProduceIsRejected_Then_RecordsMeasuredPreEnqueueFailure()
    {
        using var blackhole = new TcpListener(IPAddress.Loopback, 0);
        blackhole.Start();
        using var acceptCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptedClients = new List<TcpClient>();
        var acceptTask = Task.Run(async () =>
        {
            while (!acceptCancellation.IsCancellationRequested)
            {
                try { acceptedClients.Add(await blackhole.AcceptTcpClientAsync(acceptCancellation.Token)); }
                catch (OperationCanceledException) when (acceptCancellation.IsCancellationRequested) { break; }
                catch (ObjectDisposedException) { break; }
            }
        }, CancellationToken.None);

        var directory = NewDirectory();
        try
        {
            var identity = Identity();
            var ledgerPath = Path.Combine(directory, "ledger.jsonl");
            var manifestPath = Path.Combine(directory, "manifest.json");
            await using var ledger = NewLedger(ledgerPath, manifestPath);
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = $"127.0.0.1:{((IPEndPoint)blackhole.LocalEndpoint).Port}",
                ClientId = "loadgen-component-queue-rejection",
                Acks = Acks.All,
                EnableIdempotence = false,
                MessageTimeoutMs = 1_800,
                SocketTimeoutMs = 15_000,
                ReconnectBackoffMs = 100,
                ReconnectBackoffMaxMs = 200
            };
            producerConfig.Set("queue.buffering.max.messages", "1");
            var producer = new ProducerBuilder<byte[], byte[]>(producerConfig).Build();
            var result = await new RunExecutor(() => producer, ledger, TimeProvider.System).ExecuteAsync(
                Profile() with { Stages = [new LoadStage("two", 2, 1)] }, identity,
                Options(directory, "unreachable-component-topic") with { ProducerDrainTimeout = TimeSpan.FromSeconds(5) },
                TestContext.Current.CancellationToken);

            var rows = await LedgerValidator.ReadValidatedAsync(ledgerPath, manifestPath, identity, 8, 4096,
                64 * 1024, 64 * 1024, TestContext.Current.CancellationToken);
            Assert.Equal(2, rows.Length);
            var rejection = Assert.Single(rows, row => row.CompletionSource == "synchronous_rejection");
            Assert.False(rejection.Delivered);
            Assert.Null(rejection.PersistenceStatus);
            Assert.Equal("pre_enqueue", rejection.PersistenceProvenance);
            Assert.Equal((int)ErrorCode.Local_QueueFull, rejection.ErrorCode);
            Assert.Equal(nameof(ErrorCode.Local_QueueFull), rejection.ErrorName);
            Assert.True(rejection.SynchronousCallDurationMs > 0);
            Assert.Equal(2, result.Manifest.AttemptCount);
            Assert.True(result.Manifest.BookkeepingComplete);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("async_delivery_failure_observed", result.Manifest.GuardObservations);
            Assert.Contains(rows, row => row.CompletionSource == "callback" && !row.Delivered);
        }
        finally
        {
            acceptCancellation.Cancel();
            blackhole.Stop();
            try { await acceptTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken); }
            catch (OperationCanceledException) { }
            foreach (var client in acceptedClients) client.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Header(Headers headers, string name) => Encoding.UTF8.GetString(headers.Single(header => header.Key == name).GetValueBytes());

    private static IProducer<byte[], byte[]> CreateProducer<T>() where T : DispatchProxy =>
        DispatchProxy.Create<IProducer<byte[], byte[]>, T>();

    private static RunIdentity Identity() => new("A01", "component", "run-component", "instance-component", "instance-component", "loadgen");

    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-component-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static LocalLedgerStore NewLedger(string ledgerPath, string manifestPath) => new(new LedgerStoreOptions(
        ledgerPath, manifestPath, 8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));

    private static RunExecutionOptions Options(string directory, string topic) => new(8, 256,
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), 4096, 64 * 1024,
        64 * 1024, directory, topic, [], false);

    private static WorkloadProfile Profile()
    {
        var eventTypes = new[] { "uploaded", "ocr-started", "ocr-finished", "classified", "indexed" };
        var payloads = eventTypes.ToImmutableDictionary(name => name,
            _ => ImmutableArray.Create(new PayloadBucket(32, 1)), StringComparer.Ordinal);
        return new WorkloadProfile(1, "component-profile", 19, [new TenantWeight("tenant", 1)], 1,
            payloads, [new LoadStage("one", 1, 1)], new SourceLocation("synthetic-component-fixture"));
    }

    public class SuccessfulCallbackProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                var callback = (Action<DeliveryReport<byte[], byte[]>>)args[2]!;
                callback(new DeliveryReport<byte[], byte[]> { Error = new Error(ErrorCode.NoError), Status = PersistenceStatus.Persisted });
                return null;
            }
            if (targetMethod?.Name is "Flush") return 0;
            if (targetMethod?.Name is "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    public class SlowFirstProduceProxy : DispatchProxy
    {
        private int count;
        public Action? OnFirstProduce { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                if (Interlocked.Increment(ref count) == 1) OnFirstProduce?.Invoke();
                var callback = (Action<DeliveryReport<byte[], byte[]>>)args[2]!;
                callback(new DeliveryReport<byte[], byte[]> { Error = new Error(ErrorCode.NoError), Status = PersistenceStatus.Persisted });
                return null;
            }
            if (targetMethod?.Name is "Flush") return 0;
            if (targetMethod?.Name is "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private sealed class ControlledClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref timestamp);
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
        public void Advance(TimeSpan duration) => Interlocked.Add(ref timestamp, duration.Ticks);
    }

    public class NeverDrainedProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce") return null;
            if (targetMethod?.Name is "Flush") return 1;
            if (targetMethod?.Name is "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private sealed class FailingWriteStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new IOException("forced_writer_failure");
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.FromException(new IOException("forced_writer_failure"));
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("forced_writer_failure");
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("forced_writer_failure"));
    }
}
#pragma warning restore CA1707
