using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Restart;
using Parchwell.Scenarios;
using Xunit;
using static Parchwell.LoadGen.Tests.Restart.RestartFixture;
using ScriptedProducer = Parchwell.LoadGen.Tests.Execution.RunExecutorRetryTests.ScriptedProducer;

namespace Parchwell.LoadGen.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class RunExecutorRestartTests
{
    [Fact]
    public async Task Given_PredecessorWhoseRetryTimedOut_When_ExecutingRestart_Then_ResendsOnceWithOriginalPayload()
    {
        var first = await RunAsync(First, Profile(2), null,
            [(ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted),
             (ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted)]);
        RestartInstance restart = RestartInstance.Create(Second, first.Result.Manifest, first.Rows);

        var second = await RunAsync(Second, Profile(2), restart,
            [(ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted)]);

        Assert.Equal([new RestartPlannedEntry(0, "pod-a-1", "tenant", "d0", "tenant-d0", 0, "uploaded", 0)], restart.Plan.Planned);
        RunManifest manifest = second.Result.Manifest;
        Assert.Equal(0, second.Result.ExitCode);
        Assert.Equal("complete", manifest.WorkerStatus);
        Assert.Equal(1, manifest.PlannedAttemptCount);
        Assert.Equal(1, manifest.AttemptCount);
        Assert.Equal("pod-a", manifest.Restart!.PredecessorInstanceId);
        Assert.Equal(["pod-b-0"], manifest.Inventory.Select(entry => entry.AttemptId));
        Message<byte[], byte[]> resent = Assert.Single(second.Produced);
        Assert.Equal("pod-b-0", HeaderValue(resent.Headers, "attempt-id"));
        Assert.Equal("pod-b", HeaderValue(resent.Headers, "instance-id"));
        Assert.Equal("tenant-d0", Encoding.UTF8.GetString(resent.Key));
        Assert.Equal(first.Produced[0].Value, resent.Value);
        Assert.DoesNotContain("application_retry_limit_reached", manifest.Diagnostics);
        Assert.Single(second.Rows);
        RestartPlan.ValidateAgainstPredecessor(manifest, first.Result.Manifest, first.Rows);
    }

    [Fact]
    public async Task Given_PredecessorWithoutPermittedTimeout_When_ExecutingRestart_Then_SendsNothingAndCompletes()
    {
        var first = await RunAsync(First, Profile(1), null, [(ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted)]);
        RestartInstance restart = RestartInstance.Create(Second, first.Result.Manifest, first.Rows);

        var second = await RunAsync(Second, Profile(1), restart, []);

        Assert.Equal(0, second.Result.ExitCode);
        Assert.Equal("complete", second.Result.Manifest.WorkerStatus);
        Assert.Equal(0, second.Result.Manifest.PlannedAttemptCount);
        Assert.Equal(0, second.Result.Manifest.AttemptCount);
        Assert.Null(second.Result.Manifest.FirstUnexecutedOrdinal);
        Assert.Empty(second.Result.Manifest.Restart!.Planned);
        Assert.Empty(second.Produced);
        Assert.Empty(second.Rows);
    }

    [Theory]
    [InlineData("pod-c", 2, "execution_failed:InvalidOperationException")]
    [InlineData("pod-b", 2, "execution_failed:InvalidDataException")]
    public async Task Given_RestartThatDoesNotFitRun_When_Executing_Then_FailsBeforeProducerConstruction(string instanceId,
        int attemptsPerSecond, string diagnostic)
    {
        var (manifest, rows) = Predecessor();
        RestartInstance restart = RestartInstance.Create(Second, manifest, rows);

        var run = await RunAsync(new RunIdentity("B10", "baseline", "run-1", instanceId, instanceId, instanceId),
            Profile(attemptsPerSecond), restart, []);

        Assert.Equal(1, run.Result.ExitCode);
        Assert.Equal(0, run.Constructed);
        Assert.Equal("failed", run.Result.Manifest.WorkerStatus);
        Assert.Contains(diagnostic, run.Result.Manifest.Diagnostics);
    }

    // Every run gets the B10 retry policy: a first instance uses it, and a restarted instance must ignore it.
    private static async Task<(RunExecutionResult Result, IReadOnlyList<Message<byte[], byte[]>> Produced,
        ImmutableArray<LedgerRow> Rows, int Constructed)> RunAsync(RunIdentity identity, WorkloadProfile profile,
        RestartInstance? restart, (ErrorCode Code, PersistenceStatus Status)[] outcomes)
    {
        var directory = Path.Combine(Path.GetTempPath(), "loadgen-restart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string ledgerPath = Path.Combine(directory, "ledger.jsonl");
            string manifestPath = Path.Combine(directory, "manifest.json");
            await using var ledger = new LocalLedgerStore(new LedgerStoreOptions(ledgerPath, manifestPath,
                8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));
            var native = DispatchProxy.Create<IProducer<byte[], byte[]>, ScriptedProducer>();
            var scripted = (ScriptedProducer)(object)native;
            foreach (var (code, status) in outcomes) scripted.Enqueue(code, status);
            var constructed = 0;
            var executor = new RunExecutor(() => { constructed++; return native; }, ledger, TimeProvider.System);
            var options = new RunExecutionOptions(8, 256, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5), 4096, 64 * 1024, 64 * 1024, directory, "document-events", [], false,
                ApplicationRetry: ApplicationRetryPolicy.ForScenario("B10"));

            var result = await executor.ExecuteAsync(profile, identity, options, restart, TestContext.Current.CancellationToken);

            var rows = await LedgerValidator.ReadValidatedAsync(ledgerPath, manifestPath, identity, 8, 4096,
                64 * 1024, 64 * 1024, TestContext.Current.CancellationToken);
            return (result, scripted.Produced, rows, constructed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string HeaderValue(Headers headers, string name)
        => Encoding.UTF8.GetString(headers.Single(header => header.Key == name).GetValueBytes());
}
