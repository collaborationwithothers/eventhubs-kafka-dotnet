using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class RunExecutorTests
{
    [Fact]
    public async Task Given_SynchronousNativeCallbacks_When_ExecutingFullSchedule_Then_PersistsMatchingLedgerAndCompleteManifest()
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-executor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = Identity();
            var ledgerPath = Path.Combine(directory, "ledger.jsonl");
            var manifestPath = Path.Combine(directory, "manifest.json");
            await using var ledger = new LocalLedgerStore(new LedgerStoreOptions(ledgerPath, manifestPath,
                8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            var native = DispatchProxy.Create<IProducer<byte[], byte[]>, SuccessfulProducer>();
            var executor = new RunExecutor(() => native, ledger, TimeProvider.System);

            var result = await executor.ExecuteAsync(Profile(), identity, Options(directory), TestContext.Current.CancellationToken);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("complete", result.Manifest.WorkerStatus);
            Assert.True(result.Manifest.BookkeepingComplete);
            Assert.Equal(1, result.Manifest.AttemptCount);
            Assert.Null(result.Manifest.FirstUnexecutedOrdinal);
            Assembly kafkaAssembly = typeof(IProducer<byte[], byte[]>).Assembly;
            string expectedKafkaVersion = kafkaAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? kafkaAssembly.GetName().Version?.ToString() ?? "unknown";
            Assert.Equal(expectedKafkaVersion, result.Manifest.RuntimeVersions["confluent_kafka"]);
            var rows = await LedgerValidator.ReadValidatedAsync(ledgerPath, manifestPath, identity, 8, 4096,
                64 * 1024, 64 * 1024, TestContext.Current.CancellationToken);
            var row = Assert.Single(rows);
            Assert.Equal("callback", row.CompletionSource);
            Assert.True(row.Delivered);
            Assert.Equal((int)PersistenceStatus.Persisted, row.PersistenceStatus);
            Assert.Equal("native_callback", row.PersistenceProvenance);
            Assert.Equal(0, row.Attempt.Ordinal);
            Assert.Equal("instance-1-0", row.Attempt.AttemptId);
            var producer = (SuccessfulProducer)(object)native;
            Assert.Equal("tenant-d0", Encoding.UTF8.GetString(producer.LastKey!));
            Assert.Equal(["tenant-id", "document-id", "sequence", "event-type", "instance-id", "attempt-id"],
                producer.LastHeaders!.Select(header => header.Key));
            Assert.Equal("tenant", HeaderValue(producer.LastHeaders!, "tenant-id"));
            Assert.Equal("d0", HeaderValue(producer.LastHeaders!, "document-id"));
            Assert.Equal("instance-1", HeaderValue(producer.LastHeaders!, "instance-id"));
            Assert.Equal("instance-1-0", HeaderValue(producer.LastHeaders!, "attempt-id"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_ProducerConstructionConsumesTime_When_Executing_Then_StartsOfferedScheduleAfterConstruction()
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-executor-producer-start-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = Identity();
            await using var ledger = new LocalLedgerStore(new LedgerStoreOptions(Path.Combine(directory, "ledger.jsonl"),
                Path.Combine(directory, "manifest.json"), 8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            var clock = new AdvancingTimeProvider();
            var native = DispatchProxy.Create<IProducer<byte[], byte[]>, SuccessfulProducer>();
            var executor = new RunExecutor(() =>
            {
                clock.Advance(TimeSpan.FromSeconds(2));
                return native;
            }, ledger, clock);

            var result = await executor.ExecuteAsync(Profile(), identity, Options(directory), TestContext.Current.CancellationToken);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(TimeSpan.FromSeconds(2).Ticks, result.Manifest.StartTimestamp);
            Assert.Equal(1, ((SuccessfulProducer)(object)native).ProduceCount);
            Assert.Equal(0, result.Manifest.Inventory[0].PlannedMicroseconds);
            Assert.Equal(0, result.Manifest.Inventory[0].InvokedMicroseconds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_RequiredRequestCapabilityIsUnsupported_When_Executing_Then_BlocksBeforeProducerConstructionAndSavesFailure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-executor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = Identity();
            var ledgerPath = Path.Combine(directory, "ledger.jsonl");
            var manifestPath = Path.Combine(directory, "manifest.json");
            await using var ledger = new LocalLedgerStore(new LedgerStoreOptions(ledgerPath, manifestPath,
                8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            var factoryCalled = false;
            var executor = new RunExecutor(() =>
            {
                factoryCalled = true;
                return DispatchProxy.Create<IProducer<byte[], byte[]>, SuccessfulProducer>();
            }, ledger, TimeProvider.System);

            var result = await executor.ExecuteAsync(Profile(), identity,
                Options(directory) with { RequireRequestEvidence = true }, TestContext.Current.CancellationToken);

            Assert.False(factoryCalled);
            Assert.Equal(1, result.ExitCode);
            Assert.Equal("failed", result.Manifest.WorkerStatus);
            Assert.Equal(1, result.Manifest.PlannedAttemptCount);
            Assert.Equal(0, result.Manifest.AttemptCount);
            Assert.Equal(0, result.Manifest.FirstUnexecutedOrdinal);
            Assert.Contains("execution_failed:InvalidOperationException", result.Manifest.Diagnostics);
            Assert.True(File.Exists(manifestPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_AsyncDeliveryFailure_When_Executing_Then_OffersRemainingScheduleAndRecordsGuardEvidence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-executor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = Identity();
            var ledgerPath = Path.Combine(directory, "ledger.jsonl");
            var manifestPath = Path.Combine(directory, "manifest.json");
            await using var ledger = new LocalLedgerStore(new LedgerStoreOptions(ledgerPath, manifestPath,
                8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            var native = DispatchProxy.Create<IProducer<byte[], byte[]>, FirstFailureProducer>();
            var executor = new RunExecutor(() => native, ledger, TimeProvider.System);

            var result = await executor.ExecuteAsync(Profile() with { Stages = [new LoadStage("two", 2, 1)] },
                identity, Options(directory), TestContext.Current.CancellationToken);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("complete", result.Manifest.WorkerStatus);
            Assert.True(result.Manifest.BookkeepingComplete);
            Assert.Equal(2, result.Manifest.AttemptCount);
            Assert.Equal(2, ((FirstFailureProducer)(object)native).ProduceCount);
            Assert.Contains("async_delivery_failure_observed", result.Manifest.GuardObservations);
            Assert.Contains("possibly_persisted_observed", result.Manifest.GuardObservations);
            var rows = await LedgerValidator.ReadValidatedAsync(ledgerPath, manifestPath, identity, 8, 4096,
                64 * 1024, 64 * 1024, TestContext.Current.CancellationToken);
            Assert.Equal(2, rows.Length);
            Assert.False(rows[0].Delivered);
            Assert.True(rows[1].Delivered);
            Assert.Equal((int)ErrorCode.Local_MsgTimedOut, rows[0].ErrorCode);
            Assert.Equal(nameof(ErrorCode.Local_MsgTimedOut), rows[0].ErrorName);
            Assert.Equal((int)PersistenceStatus.PossiblyPersisted, rows[0].PersistenceStatus);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_CancelledRun_When_Executing_Then_SavesIncompletePrefixAndReturnsFailure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-executor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = Identity();
            var ledgerPath = Path.Combine(directory, "ledger.jsonl");
            var manifestPath = Path.Combine(directory, "manifest.json");
            await using var ledger = new LocalLedgerStore(new LedgerStoreOptions(ledgerPath, manifestPath,
                8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            var native = DispatchProxy.Create<IProducer<byte[], byte[]>, SuccessfulProducer>();
            var executor = new RunExecutor(() => native, ledger, TimeProvider.System);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancellation.Cancel();

            var result = await executor.ExecuteAsync(Profile(), identity, Options(directory), cancellation.Token);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal("failed", result.Manifest.WorkerStatus);
            Assert.Equal(1, result.Manifest.PlannedAttemptCount);
            Assert.Equal(0, result.Manifest.AttemptCount);
            Assert.Equal(0, result.Manifest.FirstUnexecutedOrdinal);
            Assert.Contains("execution_cancelled", result.Manifest.Diagnostics);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_FinalProduceCancelsRun_When_Executing_Then_DoesNotCompleteRun()
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-executor-cancel-final-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = Identity();
            await using var ledger = new LocalLedgerStore(new LedgerStoreOptions(Path.Combine(directory, "ledger.jsonl"),
                Path.Combine(directory, "manifest.json"), 8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var native = DispatchProxy.Create<IProducer<byte[], byte[]>, CancellingProducer>();
            ((CancellingProducer)(object)native).Cancel = cancellation;
            var executor = new RunExecutor(() => native, ledger, TimeProvider.System);

            var result = await executor.ExecuteAsync(Profile(), identity, Options(directory), cancellation.Token);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal("failed", result.Manifest.WorkerStatus);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Contains("execution_cancelled", result.Manifest.Diagnostics);
            Assert.Equal(1, ((CancellingProducer)(object)native).ProduceCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_FlushReturnsZeroAfterDrainDeadline_When_Draining_Then_FailsWithinSingleBudget()
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-executor-drain-deadline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = Identity();
            await using var ledger = new LocalLedgerStore(new LedgerStoreOptions(Path.Combine(directory, "ledger.jsonl"),
                Path.Combine(directory, "manifest.json"), 8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            var clock = new AdvancingTimeProvider();
            var native = DispatchProxy.Create<IProducer<byte[], byte[]>, DeadlineFlushProducer>();
            ((DeadlineFlushProducer)(object)native).Clock = clock;
            var executor = new RunExecutor(() => native, ledger, clock);

            var result = await executor.ExecuteAsync(Profile(), identity,
                Options(directory) with { ProducerDrainTimeout = TimeSpan.FromMilliseconds(250) }, TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("producer_drain_timeout", result.Manifest.Diagnostics);
            DeadlineFlushProducer producer = (DeadlineFlushProducer)(object)native;
            Assert.Equal(3, producer.FlushTimeouts.Count);
            Assert.Equal(TimeSpan.FromMilliseconds(100), producer.FlushTimeouts[0]);
            Assert.Equal(TimeSpan.FromMilliseconds(100), producer.FlushTimeouts[1]);
            Assert.Equal(TimeSpan.FromMilliseconds(50), producer.FlushTimeouts[2]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_CancellationDuringFinalFlush_When_Draining_Then_ReturnsFailure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-executor-cancel-flush-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = Identity();
            await using var ledger = new LocalLedgerStore(new LedgerStoreOptions(Path.Combine(directory, "ledger.jsonl"),
                Path.Combine(directory, "manifest.json"), 8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var clock = new AdvancingTimeProvider();
            var native = DispatchProxy.Create<IProducer<byte[], byte[]>, DeadlineFlushProducer>();
            DeadlineFlushProducer producer = (DeadlineFlushProducer)(object)native;
            producer.Clock = clock;
            producer.CancelOnFlush = cancellation;
            var executor = new RunExecutor(() => native, ledger, clock);

            var result = await executor.ExecuteAsync(Profile(), identity, Options(directory), cancellation.Token);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.DoesNotContain("producer_drain_timeout", result.Manifest.Diagnostics);
            Assert.Contains("execution_cancelled", result.Manifest.Diagnostics);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Given_InventorySetupConsumesTime_When_ReservationIsFinalizedOrCancelled_Then_UsesFinalTimeWithoutPhantomEntry()
    {
        var identity = Identity();
        var clock = new AdvancingTimeProvider();
        var cancelledInventory = new AttemptInventory(identity, 8);
        AttemptEntry draft = DraftAttempt(identity, 0);
        cancelledInventory.Reserve(draft);
        clock.Advance(TimeSpan.FromSeconds(2));
        long sampledMicroseconds = clock.GetElapsedTime(0, clock.GetTimestamp()).Ticks / 10;
        cancelledInventory.CancelReservation();
        Assert.Empty(cancelledInventory.Seal());

        var committedInventory = new AttemptInventory(identity, 8);
        committedInventory.Reserve(draft);
        clock.Advance(TimeSpan.FromMilliseconds(17));
        long finalMicroseconds = clock.GetElapsedTime(0, clock.GetTimestamp()).Ticks / 10;
        AttemptEntry committed = committedInventory.CommitReservation(finalMicroseconds);

        Assert.Equal(finalMicroseconds, committed.InvokedMicroseconds);
        Assert.True(committed.InvokedMicroseconds > sampledMicroseconds);
        Assert.Equal(committed, Assert.Single(committedInventory.Seal()));
    }

    [Fact]
    public async Task Given_DeliveryCallbackThrows_When_Executing_Then_LatchesCaptureFaultAndStopsAdmission()
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-executor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = Identity();
            var ledgerPath = Path.Combine(directory, "ledger.jsonl");
            var manifestPath = Path.Combine(directory, "manifest.json");
            await using var ledger = new LocalLedgerStore(new LedgerStoreOptions(ledgerPath, manifestPath,
                8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            var native = DispatchProxy.Create<IProducer<byte[], byte[]>, ThrowingCallbackProducer>();
            var executor = new RunExecutor(() => native, ledger, TimeProvider.System);

            var result = await executor.ExecuteAsync(Profile() with { Stages = [new LoadStage("two", 2, 1)] },
                identity, Options(directory), TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal("failed", result.Manifest.WorkerStatus);
            Assert.Equal(1, result.Manifest.AttemptCount);
            Assert.Contains("delivery_callback_failure", result.Manifest.GuardObservations);
            Assert.Contains("callback_or_ledger_capture_faulted", result.Manifest.Diagnostics);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_LedgerCaptureFaultsDuringScheduledWait_When_Executing_Then_DiscardsReservationBeforeProduce()
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-executor-fault-during-wait-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = Identity();
            var ledger = new FaultingLedgerStore(new LedgerStoreOptions(Path.Combine(directory, "ledger.jsonl"),
                Path.Combine(directory, "manifest.json"), 8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            await using (ledger)
            {
                var clock = new FaultingDuringWaitTimeProvider(TimeSpan.FromMilliseconds(500), ledger.MarkFaulted);
                var native = DispatchProxy.Create<IProducer<byte[], byte[]>, SuccessfulProducer>();
                var executor = new RunExecutor(() => native, ledger, clock);

                var result = await executor.ExecuteAsync(Profile() with { Stages = [new LoadStage("two", 2, 1)] },
                    identity, Options(directory), TestContext.Current.CancellationToken);

                Assert.True(clock.WaitTriggered);
                Assert.True(ledger.IsFaulted);
                Assert.Equal(1, ((SuccessfulProducer)(object)native).ProduceCount);
                Assert.Equal(1, result.Manifest.AttemptCount);
                Assert.Equal(1, result.Manifest.FirstUnexecutedOrdinal);
                Assert.Contains("callback_or_ledger_capture_faulted", result.Manifest.Diagnostics);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_UnnamedNativeErrorCode_When_Recorded_Then_PreservesCodeWithAbsentSymbolicName()
    {
        string directory = Path.Combine(Path.GetTempPath(), "loadgen-unnamed-error-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = Identity();
            var ledgerPath = Path.Combine(directory, "ledger.jsonl");
            var manifestPath = Path.Combine(directory, "manifest.json");
            await using var ledger = new LocalLedgerStore(new LedgerStoreOptions(ledgerPath, manifestPath,
                8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            var native = DispatchProxy.Create<IProducer<byte[], byte[]>, UnnamedErrorProducer>();
            var executor = new RunExecutor(() => native, ledger, TimeProvider.System);

            var result = await executor.ExecuteAsync(Profile(), identity, Options(directory), TestContext.Current.CancellationToken);

            Assert.Equal(0, result.ExitCode);
            var rows = await LedgerValidator.ReadValidatedAsync(ledgerPath, manifestPath, identity,
                8, 4096, 64 * 1024, 64 * 1024, TestContext.Current.CancellationToken);
            LedgerRow row = Assert.Single(rows);
            Assert.Equal(123456, row.ErrorCode);
            Assert.Null(row.ErrorName);
            Assert.False(row.Delivered);
            Assert.Contains("async_delivery_failure_observed", result.Manifest.GuardObservations);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    public class UnnamedErrorProducer : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new DeliveryReport<byte[], byte[]>
                {
                    Error = new Error((ErrorCode)123456, "synthetic-unnamed-error"),
                    Status = PersistenceStatus.NotPersisted
                });
                return null;
            }
            if (targetMethod?.Name == "Flush") return 0;
            if (targetMethod?.Name == "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private static RunIdentity Identity() => new("A01", "baseline", "run-1", "instance-1", "instance-1", "loadgen");

    private static AttemptEntry DraftAttempt(RunIdentity identity, long ordinal) => new(identity.InstanceId,
        string.Concat(identity.InstanceId, "-", ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)), ordinal,
        "tenant", "d" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "tenant-d" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), 0, "uploaded", 0, 0);

    private static string HeaderValue(Headers headers, string name)
        => Encoding.UTF8.GetString(headers.Single(header => header.Key == name).GetValueBytes());

    private static RunExecutionOptions Options(string directory) => new(8, 256,
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), 4096, 64 * 1024,
        64 * 1024, directory, "document-events", [], false);

    private static WorkloadProfile Profile()
    {
        var names = new[] { "uploaded", "ocr-started", "ocr-finished", "classified", "indexed" };
        var payloads = names.ToImmutableDictionary(name => name,
            _ => ImmutableArray.Create(new PayloadBucket(4, 1)), StringComparer.Ordinal);
        return new WorkloadProfile(1, "executor-fixture", 7, [new TenantWeight("tenant", 1)], 1,
            payloads, [new LoadStage("one", 1, 1)], new SourceLocation("synthetic-test-fixture"));
    }

    public class SuccessfulProducer : DispatchProxy
    {
        public Headers? LastHeaders { get; private set; }
        public byte[]? LastKey { get; private set; }
        public int ProduceCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                ProduceCount++;
                var message = (Message<byte[], byte[]>)args[1]!;
                LastHeaders = message.Headers;
                LastKey = message.Key;
                var callback = (Action<DeliveryReport<byte[], byte[]>>)args[2]!;
                callback(new DeliveryReport<byte[], byte[]>
                {
                    Error = new Error(ErrorCode.NoError),
                    Status = PersistenceStatus.Persisted
                });
                return null;
            }
            if (targetMethod?.Name == "Flush") return 0;
            if (targetMethod?.Name == "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private sealed class FaultingLedgerStore : ILedgerStore
    {
        private readonly LocalLedgerStore inner;
        private Exception? failure;

        public FaultingLedgerStore(LedgerStoreOptions options) => inner = new LocalLedgerStore(options);

        public bool IsFaulted => Volatile.Read(ref failure) is not null || inner.IsFaulted;
        public Exception? Failure => Volatile.Read(ref failure) ?? inner.Failure;
        public bool TryAppend(LedgerRow row) => IsFaulted ? false : inner.TryAppend(row);
        public Task DrainAsync(CancellationToken cancellationToken = default) => inner.DrainAsync(cancellationToken);
        public Task SaveManifestAsync(RunManifest manifest, CancellationToken cancellationToken = default)
            => inner.SaveManifestAsync(manifest, cancellationToken);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
        public void MarkFaulted() => Interlocked.CompareExchange(ref failure,
            new InvalidOperationException("test_capture_fault"), null);
    }

    private sealed class FaultingDuringWaitTimeProvider : TimeProvider
    {
        private readonly TimeSpan waitDuration;
        private readonly Action markFaulted;
        private long timestamp;
        private int waitTriggered;

        public FaultingDuringWaitTimeProvider(TimeSpan waitDuration, Action markFaulted)
        {
            this.waitDuration = waitDuration;
            this.markFaulted = markFaulted;
        }

        public bool WaitTriggered => Volatile.Read(ref waitTriggered) != 0;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref timestamp);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == waitDuration && Interlocked.Exchange(ref waitTriggered, 1) == 0)
            {
                Interlocked.Add(ref timestamp, dueTime.Ticks);
                markFaulted();
                callback(state);
            }
            return new NoopTimer();
        }

        private sealed class NoopTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    public class FirstFailureProducer : DispatchProxy
    {
        public int ProduceCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                ProduceCount++;
                var callback = (Action<DeliveryReport<byte[], byte[]>>)args[2]!;
                callback(new DeliveryReport<byte[], byte[]>
                {
                    Error = new Error(ProduceCount == 1 ? ErrorCode.Local_MsgTimedOut : ErrorCode.NoError),
                    Status = ProduceCount == 1 ? PersistenceStatus.PossiblyPersisted : PersistenceStatus.Persisted
                });
                return null;
            }
            if (targetMethod?.Name == "Flush") return 0;
            if (targetMethod?.Name == "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    public class ThrowingCallbackProducer : DispatchProxy
    {
        public int ProduceCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                ProduceCount++;
                var callback = (Action<DeliveryReport<byte[], byte[]>>)args[2]!;
                callback(null!);
                return null;
            }
            if (targetMethod?.Name == "Flush") return 0;
            if (targetMethod?.Name == "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    public class CancellingProducer : DispatchProxy
    {
        public CancellationTokenSource? Cancel { get; set; }
        public int ProduceCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                ProduceCount++;
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new DeliveryReport<byte[], byte[]>
                {
                    Error = new Error(ErrorCode.NoError), Status = PersistenceStatus.Persisted
                });
                Cancel!.Cancel();
                return null;
            }
            if (targetMethod?.Name == "Flush") return 0;
            if (targetMethod?.Name == "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    public class DeadlineFlushProducer : DispatchProxy
    {
        public AdvancingTimeProvider? Clock { get; set; }
        public CancellationTokenSource? CancelOnFlush { get; set; }
        public List<TimeSpan> FlushTimeouts { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new DeliveryReport<byte[], byte[]>
                {
                    Error = new Error(ErrorCode.NoError), Status = PersistenceStatus.Persisted
                });
                return null;
            }
            if (targetMethod?.Name == "Flush" && args is { Length: 1 })
            {
                FlushTimeouts.Add((TimeSpan)args[0]!);
                Clock!.Advance(TimeSpan.FromMilliseconds(100));
                CancelOnFlush?.Cancel();
                return FlushTimeouts.Count == 3 ? 0 : 1;
            }
            if (targetMethod?.Name == "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    public sealed class AdvancingTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref timestamp);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => new NoopTimer();

        public void Advance(TimeSpan value) => Interlocked.Add(ref timestamp, value.Ticks);

        private sealed class NoopTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
#pragma warning restore CA1707
