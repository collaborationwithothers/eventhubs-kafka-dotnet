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
public sealed class RunExecutorRetryTests
{
    private static readonly ApplicationRetryPolicy B10 = ApplicationRetryPolicy.ForScenario("B10");

    [Theory]
    [InlineData(PersistenceStatus.PossiblyPersisted)]
    [InlineData(PersistenceStatus.NotPersisted)]
    public async Task Given_B10PolicyAndTimedOutSend_When_Executing_Then_RetriesOnceInDrainWithNewAttemptId(PersistenceStatus status)
    {
        await RunAsync(Profile(), B10, [(ErrorCode.Local_MsgTimedOut, status)], async (result, produced, rows) =>
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("complete", result.Manifest.WorkerStatus);
            Assert.True(result.Manifest.BookkeepingComplete);
            Assert.Equal(1, result.Manifest.PlannedAttemptCount);
            Assert.Equal(2, result.Manifest.AttemptCount);
            Assert.Null(result.Manifest.FirstUnexecutedOrdinal);
            Assert.Equal(2, produced.Count);
            Assert.Equal(["instance-1-0", "instance-1-1"], result.Manifest.Inventory.Select(entry => entry.AttemptId));
            Assert.Equal([0L, 1L], result.Manifest.Inventory.Select(entry => entry.Ordinal));
            AttemptEntry retry = result.Manifest.Inventory[1];
            Assert.Equal("tenant-d0", retry.Key);
            Assert.Equal("d0", retry.DocumentId);
            Assert.Equal(0, retry.Sequence);
            Assert.Equal(result.Manifest.Inventory[0].PlannedMicroseconds, retry.PlannedMicroseconds);
            Assert.Equal("instance-1-1", HeaderValue(produced[1].Headers, "attempt-id"));
            Assert.True(produced[1].Value.SequenceEqual(produced[0].Value));
            Assert.Equal(2, rows.Length);
            Assert.False(rows[0].Delivered);
            Assert.Equal(-192, rows[0].ErrorCode);
            Assert.Equal((int)status, rows[0].PersistenceStatus);
            Assert.True(rows[1].Delivered);
            Assert.Contains("async_delivery_failure_observed", result.Manifest.GuardObservations);
            Assert.DoesNotContain("application_retry_limit_reached", result.Manifest.Diagnostics);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Given_B10PolicyAndRetryAlsoTimesOut_When_Executing_Then_StopsAfterOneRetry()
    {
        await RunAsync(Profile(), B10,
            [(ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted),
             (ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted)],
            async (result, produced, rows) =>
            {
                Assert.Equal(0, result.ExitCode);
                Assert.Equal("complete", result.Manifest.WorkerStatus);
                Assert.Equal(2, result.Manifest.AttemptCount);
                Assert.Equal(2, produced.Count);
                Assert.Contains("application_retry_limit_reached", result.Manifest.Diagnostics);
                Assert.All(rows, row => Assert.False(row.Delivered));
                await Task.CompletedTask;
            });
    }

    [Fact]
    public async Task Given_B10PolicyAndTimeoutDuringSchedule_When_Executing_Then_RetryPrecedesNextScheduledEvent()
    {
        await RunAsync(Profile() with { Stages = [new LoadStage("two", 2, 1)] }, B10,
            [(ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted)],
            async (result, produced, _) =>
            {
                Assert.Equal(0, result.ExitCode);
                Assert.Equal("complete", result.Manifest.WorkerStatus);
                Assert.Equal(2, result.Manifest.PlannedAttemptCount);
                Assert.Equal(3, result.Manifest.AttemptCount);
                Assert.Equal(["instance-1-0", "instance-1-1", "instance-1-2"], result.Manifest.Inventory.Select(entry => entry.AttemptId));
                Assert.Equal(["d0", "d0", "d0"], result.Manifest.Inventory.Select(entry => entry.DocumentId));
                Assert.Equal([0L, 0L, 1L], result.Manifest.Inventory.Select(entry => (long)entry.Sequence));
                Assert.Equal(3, produced.Count);
                await Task.CompletedTask;
            });
    }

    [Fact]
    public async Task Given_B10PolicyAndNonTimeoutAsyncFailure_When_Executing_Then_DoesNotRetry()
    {
        await RunAsync(Profile(), B10, [(ErrorCode.Local_Transport, PersistenceStatus.NotPersisted)],
            async (result, produced, _) =>
            {
                Assert.Equal(0, result.ExitCode);
                Assert.Equal(1, result.Manifest.AttemptCount);
                Assert.Single(produced);
                await Task.CompletedTask;
            });
    }

    [Fact]
    public async Task Given_NoRetryPolicyAndTimedOutSend_When_Executing_Then_DoesNotRetry()
    {
        await RunAsync(Profile(), null, [(ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted)],
            async (result, produced, _) =>
            {
                Assert.Equal(0, result.ExitCode);
                Assert.Equal(1, result.Manifest.AttemptCount);
                Assert.Single(produced);
                await Task.CompletedTask;
            });
    }

    [Fact]
    public async Task Given_B10PolicyAndAttemptLimitBelowRetryCapacity_When_Executing_Then_FailsBeforeProducerConstruction()
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-retry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await using var ledger = NewLedger(directory, out _, out _);
            var constructed = 0;
            var executor = new RunExecutor(() =>
            {
                constructed++;
                return DispatchProxy.Create<IProducer<byte[], byte[]>, ScriptedProducer>();
            }, ledger, TimeProvider.System);

            var result = await executor.ExecuteAsync(Profile(), Identity(), Options(directory, B10, maxAttempts: 1),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(0, constructed);
            Assert.Equal("failed", result.Manifest.WorkerStatus);
            Assert.Contains("execution_failed:InvalidOperationException", result.Manifest.Diagnostics);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Given_RetriedInventory_When_CreatingManifest_Then_CompletesOnScheduleProgress()
    {
        ImmutableArray<AttemptEntry> entries =
        [new AttemptEntry("instance-1", "instance-1-0", 0, "tenant", "d0", "tenant-d0", 0, "uploaded", 0, 0),
         new AttemptEntry("instance-1", "instance-1-1", 1, "tenant", "d0", "tenant-d0", 0, "uploaded", 0, 5)];

        RunManifest manifest = RunExecutor.CreateManifest(Identity(), entries, admittedAttemptCount: 2, plannedCount: 1,
            startTimestamp: 0, diagnostics: [], evidenceFiles: ImmutableDictionary<string, string>.Empty,
            guardObservations: [], failed: false, bookkeepingComplete: true, frequency: 1);

        Assert.Equal("complete", manifest.WorkerStatus);
        Assert.Null(manifest.FirstUnexecutedOrdinal);
        Assert.Equal(2, manifest.AttemptCount);
        LedgerValidator.ValidateManifest(manifest);
    }

    [Fact]
    public void Given_RetriedInventoryShortOfSchedule_When_CreatingManifest_Then_ReportsFirstUnexecutedSlot()
    {
        ImmutableArray<AttemptEntry> entries =
        [new AttemptEntry("instance-1", "instance-1-0", 0, "tenant", "d0", "tenant-d0", 0, "uploaded", 0, 0),
         new AttemptEntry("instance-1", "instance-1-1", 1, "tenant", "d0", "tenant-d0", 0, "uploaded", 0, 5)];

        RunManifest manifest = RunExecutor.CreateManifest(Identity(), entries, admittedAttemptCount: 2, plannedCount: 2,
            startTimestamp: 0, diagnostics: [], evidenceFiles: ImmutableDictionary<string, string>.Empty,
            guardObservations: [], failed: false, bookkeepingComplete: false, frequency: 1);

        Assert.Equal("incomplete", manifest.WorkerStatus);
        Assert.Equal(1, manifest.FirstUnexecutedOrdinal);
        LedgerValidator.ValidateManifest(manifest);
    }

    [Fact]
    public void Given_NegativeRetryLimit_When_ValidatingOptions_Then_Rejects()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Options("unused", new ApplicationRetryPolicy(-1)).Validate());
    }

    private static async Task RunAsync(WorkloadProfile profile, ApplicationRetryPolicy? retry,
        (ErrorCode Code, PersistenceStatus Status)[] outcomes,
        Func<RunExecutionResult, IReadOnlyList<Message<byte[], byte[]>>, ImmutableArray<LedgerRow>, Task> assert)
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-retry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = Identity();
            await using var ledger = NewLedger(directory, out string ledgerPath, out string manifestPath);
            var native = DispatchProxy.Create<IProducer<byte[], byte[]>, ScriptedProducer>();
            var scripted = (ScriptedProducer)(object)native;
            foreach (var (code, status) in outcomes) scripted.Enqueue(code, status);
            var executor = new RunExecutor(() => native, ledger, TimeProvider.System);

            var result = await executor.ExecuteAsync(profile, identity, Options(directory, retry),
                TestContext.Current.CancellationToken);

            var rows = await LedgerValidator.ReadValidatedAsync(ledgerPath, manifestPath, identity, 8, 4096,
                64 * 1024, 64 * 1024, TestContext.Current.CancellationToken);
            await assert(result, scripted.Produced, rows);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static LocalLedgerStore NewLedger(string directory, out string ledgerPath, out string manifestPath)
    {
        ledgerPath = Path.Combine(directory, "ledger.jsonl");
        manifestPath = Path.Combine(directory, "manifest.json");
        return new LocalLedgerStore(new LedgerStoreOptions(ledgerPath, manifestPath,
            8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
    }

    private static RunIdentity Identity() => new("A01", "baseline", "run-1", "instance-1", "instance-1", "loadgen");

    private static string HeaderValue(Headers headers, string name)
        => Encoding.UTF8.GetString(headers.Single(header => header.Key == name).GetValueBytes());

    private static RunExecutionOptions Options(string directory, ApplicationRetryPolicy? retry, int maxAttempts = 8) => new(
        maxAttempts, 256, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), 4096, 64 * 1024,
        64 * 1024, directory, "document-events", [], false, ApplicationRetry: retry);

    private static WorkloadProfile Profile()
    {
        var names = new[] { "uploaded", "ocr-started", "ocr-finished", "classified", "indexed" };
        var payloads = names.ToImmutableDictionary(name => name,
            _ => ImmutableArray.Create(new PayloadBucket(4, 1)), StringComparer.Ordinal);
        return new WorkloadProfile(1, "executor-fixture", 7, [new TenantWeight("tenant", 1)], 1,
            payloads, [new LoadStage("one", 1, 1)], new SourceLocation("synthetic-test-fixture"));
    }

    public class ScriptedProducer : DispatchProxy
    {
        private readonly Queue<(ErrorCode Code, PersistenceStatus Status)> outcomes = new();
        private readonly List<Message<byte[], byte[]>> produced = [];

        public IReadOnlyList<Message<byte[], byte[]>> Produced => produced;

        public void Enqueue(ErrorCode code, PersistenceStatus status) => outcomes.Enqueue((code, status));

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "Produce" when args is { Length: 3 }:
                    produced.Add((Message<byte[], byte[]>)args[1]!);
                    var (code, status) = outcomes.Count > 0 ? outcomes.Dequeue() : (ErrorCode.NoError, PersistenceStatus.Persisted);
                    ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(
                        new DeliveryReport<byte[], byte[]> { Error = new Error(code), Status = status });
                    return null;
                case "Flush":
                    return 0;
                case "Dispose":
                    return null;
                default:
                    throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
