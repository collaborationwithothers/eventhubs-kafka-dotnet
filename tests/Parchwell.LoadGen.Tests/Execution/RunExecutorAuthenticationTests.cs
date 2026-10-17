using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Azure.Core;
using Confluent.Kafka;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Tests.Evidence;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class RunExecutorAuthenticationTests
{
    [Fact]
    public async Task Given_InitialFailureDuringProducerConstruction_When_Executing_Then_SavesFailedIncompleteRunWithoutAdmission()
    {
        string directory = CreateDirectory();
        try
        {
            await using var ledger = CreateLedger(directory);
            KafkaOAuthBearerSession session = CreateSession();
            var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, ControlledProducer>();
            var executor = new RunExecutor(() =>
            {
                FailInitial(session);
                return producer;
            }, ledger, TimeProvider.System, authentication: session);

            RunExecutionResult result = await executor.ExecuteAsync(Profile(), Identity(), Options(directory),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal("failed", result.Manifest.WorkerStatus);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Equal(0, result.Manifest.AttemptCount);
            Assert.Equal(0, ((ControlledProducer)(object)producer).ProduceCount);
            Assert.Contains("oauthbearer_initial_failure:acquisition", result.Manifest.Diagnostics);
            Assert.True(File.Exists(Path.Combine(directory, "manifest.json")));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("acquisition")]
    [InlineData("token_validation")]
    [InlineData("installation")]
    public async Task Given_EachInitialFailureCategory_When_Executing_Then_FailsWithSanitizedReason(string reason)
    {
        string directory = CreateDirectory();
        try
        {
            await using var ledger = CreateLedger(directory);
            KafkaOAuthBearerSession session = CreateSession(new CategoryTokenProvider(reason));
            var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, ControlledProducer>();
            var executor = new RunExecutor(() =>
            {
                session.Refresh((_, _, _) =>
                {
                    if (reason == "installation") throw new InvalidOperationException("private-token-value");
                }, _ => { });
                return producer;
            }, ledger, TimeProvider.System, authentication: session);

            RunExecutionResult result = await executor.ExecuteAsync(Profile(), Identity(), Options(directory),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Equal("failed", result.Manifest.WorkerStatus);
            Assert.Contains("oauthbearer_initial_failure:" + reason, result.Manifest.Diagnostics);
            Assert.DoesNotContain(result.Manifest.Diagnostics, item => item.Contains("private-token-value", StringComparison.Ordinal));
            Assert.Equal(reason, (await session.InitialFailureTask).Reason);
            Assert.Equal(0, result.Manifest.AttemptCount);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Given_InitialFailureFollowedBySuccessfulInstall_When_Executing_Then_RemainsIncomplete()
    {
        string directory = CreateDirectory();
        try
        {
            await using var ledger = CreateLedger(directory);
            var provider = new FailOnceTokenProvider();
            KafkaOAuthBearerSession session = CreateSession(provider);
            session.Refresh((_, _, _) => { }, _ => { });
            KafkaAuthenticationFailure firstFailure = await session.InitialFailureTask;
            session.Refresh((_, _, _) => { }, _ => { });
            Assert.Same(firstFailure, await session.InitialFailureTask);

            var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, ControlledProducer>();
            var executor = new RunExecutor(() => producer, ledger, TimeProvider.System, authentication: session);
            RunExecutionResult result = await executor.ExecuteAsync(Profile(), Identity(), Options(directory),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Same(firstFailure, session.LatestFailure);
            Assert.Contains("oauthbearer_initial_failure:acquisition", result.Manifest.Diagnostics);
            Assert.Equal(0, result.Manifest.AttemptCount);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Given_InitialFailureDuringLongScheduleGap_When_Executing_Then_WakesAndCancelsUncommittedReservation()
    {
        string directory = CreateDirectory();
        try
        {
            await using var ledger = CreateLedger(directory);
            KafkaOAuthBearerSession session = CreateSession();
            var clock = new DelaySignalingTimeProvider();
            var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, ControlledProducer>();
            var executor = new RunExecutor(() => producer, ledger, clock, authentication: session);

            Task<RunExecutionResult> execution = executor.ExecuteAsync(Profile() with
                { Stages = [new LoadStage("gap", 2, 1)] }, Identity(), Options(directory),
                TestContext.Current.CancellationToken);
            await clock.DelayScheduled.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            FailInitial(session);
            RunExecutionResult result = await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(clock.DelayCount >= 1);
            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Equal(1, result.Manifest.AttemptCount);
            Assert.Equal(1, result.Manifest.FirstUnexecutedOrdinal);
            Assert.Equal(1, ((ControlledProducer)(object)producer).ProduceCount);
            Assert.Contains("oauthbearer_initial_failure:acquisition", result.Manifest.Diagnostics);
            ImmutableArray<LedgerRow> rows = await LedgerValidator.ReadValidatedAsync(
                Path.Combine(directory, "ledger.jsonl"), Path.Combine(directory, "manifest.json"), Identity(), 8,
                4096, 64 * 1024, 64 * 1024, TestContext.Current.CancellationToken);
            Assert.Single(rows);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("flush")]
    [InlineData("dispose")]
    public async Task Given_InitialFailureDuringFinalCleanup_When_Executing_Then_ObservesItBeforeManifestBookkeeping(string point)
    {
        string directory = CreateDirectory();
        try
        {
            await using var ledger = CreateLedger(directory);
            KafkaOAuthBearerSession session = CreateSession();
            var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, ControlledProducer>();
            if (point == "flush") ((ControlledProducer)(object)producer).OnFlush = () => FailInitial(session);
            else ((ControlledProducer)(object)producer).OnDispose = () => FailInitial(session);
            var executor = new RunExecutor(() => producer, ledger, TimeProvider.System, authentication: session);

            RunExecutionResult result = await executor.ExecuteAsync(Profile(), Identity(), Options(directory),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Contains("oauthbearer_initial_failure:acquisition", result.Manifest.Diagnostics);
            Assert.Equal(1, result.Manifest.AttemptCount);
            Assert.Single(await LedgerValidator.ReadValidatedAsync(Path.Combine(directory, "ledger.jsonl"),
                Path.Combine(directory, "manifest.json"), Identity(), 8, 4096, 64 * 1024, 64 * 1024,
                TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("acquisition")]
    [InlineData("token_validation")]
    [InlineData("installation")]
    public async Task Given_RefreshFailureAndLaterReplacement_When_Executing_Then_CompletesAndPreservesHistoricFailure(string reason)
    {
        string directory = CreateDirectory();
        try
        {
            await using var ledger = CreateLedger(directory);
            var provider = new SequencedTokenProvider(reason);
            KafkaOAuthBearerSession session = CreateSession(provider);
            int installCalls = 0;
            void Install(string token, long expiry, string principal)
            {
                _ = token;
                _ = expiry;
                _ = principal;
                if (Interlocked.Increment(ref installCalls) == 2 && reason == "installation")
                    throw new InvalidOperationException("private-token-value");
            }
            session.Refresh(Install, _ => { });
            DateTimeOffset firstExpiry = Assert.IsType<DateTimeOffset>(session.InstalledTokenExpiresOn);
            var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, ControlledProducer>();
            DateTimeOffset? expiryAfterFailure = null;
            int produced = 0;
            ((ControlledProducer)(object)producer).AfterProduce = () =>
            {
                if (Interlocked.Increment(ref produced) == 1)
                {
                    session.Refresh(Install, _ => { });
                    expiryAfterFailure = session.InstalledTokenExpiresOn;
                }
                else session.Refresh(Install, _ => { });
            };
            var executor = new RunExecutor(() => producer, ledger, TimeProvider.System, authentication: session);

            RunExecutionResult result = await executor.ExecuteAsync(Profile() with
                { Stages = [new LoadStage("refresh-recovery", 2, 1)] }, Identity(), Options(directory),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("complete", result.Manifest.WorkerStatus);
            Assert.True(result.Manifest.BookkeepingComplete);
            Assert.True(session.InitialFailureTask.IsCompleted is false);
            Assert.Equal("refresh", session.LatestFailure?.Phase);
            Assert.Equal(reason, session.LatestFailure?.Reason);
            Assert.Equal(firstExpiry, expiryAfterFailure);
            Assert.NotEqual(firstExpiry, session.InstalledTokenExpiresOn);
            Assert.Equal(2, result.Manifest.AttemptCount);
            Assert.Equal(2, ((ControlledProducer)(object)producer).ProduceCount);
            Assert.Equal(2, (await LedgerValidator.ReadValidatedAsync(Path.Combine(directory, "ledger.jsonl"),
                Path.Combine(directory, "manifest.json"), Identity(), 8, 4096, 64 * 1024, 64 * 1024,
                TestContext.Current.CancellationToken)).Length);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Given_RefreshFailureAndIndependentDeliveryFailure_When_Executing_Then_PreservesDeliveryGuardEvidence()
    {
        string directory = CreateDirectory();
        try
        {
            await using var ledger = CreateLedger(directory);
            var provider = new SequencedTokenProvider("acquisition");
            KafkaOAuthBearerSession session = CreateSession(provider);
            session.Refresh((_, _, _) => { }, _ => { });
            var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, ControlledProducer>();
            var proxy = (ControlledProducer)(object)producer;
            proxy.DeliveryError = true;
            proxy.AfterProduce = () => session.Refresh((_, _, _) => { }, _ => { });
            var executor = new RunExecutor(() => producer, ledger, TimeProvider.System, authentication: session);

            RunExecutionResult result = await executor.ExecuteAsync(Profile(), Identity(), Options(directory),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, result.ExitCode);
            Assert.True(result.Manifest.BookkeepingComplete);
            Assert.Contains("async_delivery_failure_observed", result.Manifest.GuardObservations);
            Assert.Equal("refresh", session.LatestFailure?.Phase);
            Assert.False(session.InitialFailureTask.IsCompleted);
            Assert.Single(await LedgerValidator.ReadValidatedAsync(Path.Combine(directory, "ledger.jsonl"),
                Path.Combine(directory, "manifest.json"), Identity(), 8, 4096, 64 * 1024, 64 * 1024,
                TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Given_RunCancellationAndNonfatalRefreshFailure_When_Executing_Then_CancellationStillStopsRun()
    {
        string directory = CreateDirectory();
        using var cancellation = new CancellationTokenSource();
        try
        {
            await using var ledger = CreateLedger(directory);
            var provider = new SequencedTokenProvider("acquisition");
            KafkaOAuthBearerSession session = CreateSession(provider);
            session.Refresh((_, _, _) => { }, _ => { });
            var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, ControlledProducer>();
            ((ControlledProducer)(object)producer).AfterProduce = () =>
            {
                session.Refresh((_, _, _) => { }, _ => { });
                cancellation.Cancel();
            };
            var executor = new RunExecutor(() => producer, ledger, TimeProvider.System, authentication: session);

            RunExecutionResult result = await executor.ExecuteAsync(Profile(), Identity(), Options(directory), cancellation.Token);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Contains("execution_cancelled", result.Manifest.Diagnostics);
            Assert.Equal("refresh", session.LatestFailure?.Phase);
            Assert.False(session.InitialFailureTask.IsCompleted);
            Assert.Equal(1, result.Manifest.AttemptCount);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Given_RefreshFailureAndIndependentEvidenceFault_When_Executing_Then_EvidenceFaultStillBlocksCompletion()
    {
        string directory = CreateDirectory();
        try
        {
            RunIdentity identity = new("A01", "C0", "run-auth", "instance-auth", "instance-auth", "loadgen");
            RunEvidenceExpectations expectations = await RunEvidenceReaderTests.ExpectationsAsync(identity);
            await using var evidence = new RunEvidenceSession(identity,
                new RunEvidenceOptions(directory, 8, 4096, 128 * 1024, 16, TimeSpan.FromSeconds(2)),
                TimeProvider.System, expectations);
            await using var ledger = CreateLedger(directory);
            var provider = new SequencedTokenProvider("acquisition");
            KafkaOAuthBearerSession session = CreateSession(provider);
            session.Refresh((_, _, _) => { }, _ => { });
            var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, ControlledProducer>();
            var proxy = (ControlledProducer)(object)producer;
            proxy.Evidence = evidence;
            proxy.AfterProduce = () =>
            {
                session.Refresh((_, _, _) => { }, _ => { });
                proxy.Evidence!.CaptureLog(new LogMessage("producer", SyslogLevel.Warning, "synthetic", new string('x', 4096)));
            };
            var executor = new RunExecutor(() => producer, ledger, TimeProvider.System, evidence: evidence,
                authentication: session);

            RunExecutionResult result = await executor.ExecuteAsync(Profile(), identity, Options(directory),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Contains("evidence_capture_faulted", result.Manifest.Diagnostics);
            Assert.Equal("refresh", session.LatestFailure?.Phase);
            Assert.False(session.InitialFailureTask.IsCompleted);
            Assert.Equal(1, result.Manifest.AttemptCount);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void FailInitial(KafkaOAuthBearerSession session)
        => session.Refresh((_, _, _) => { }, _ => { });

    private static KafkaOAuthBearerSession CreateSession(IKafkaTokenProvider? provider = null)
        => new(new KafkaAuthenticationIdentity("A01", "run-auth", KafkaClientRole.LoadGenProducer,
            "loadgen"), "scope", "principal", provider ?? new ThrowingTokenProvider(), TimeProvider.System,
            new KafkaAuthenticationMetrics(), CancellationToken.None);

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "loadgen-auth-executor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static LocalLedgerStore CreateLedger(string directory) => new(new LedgerStoreOptions(
        Path.Combine(directory, "ledger.jsonl"), Path.Combine(directory, "manifest.json"), 8, 4096,
        64 * 1024, 64 * 1024, TimeSpan.FromSeconds(2)));

    private static RunIdentity Identity() => new("A01", "baseline", "run-auth", "instance-auth", "instance-auth", "loadgen");

    private static RunExecutionOptions Options(string directory) => new(8, 256, TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), 4096, 64 * 1024, 64 * 1024, directory,
        "document-events", [], false);

    private static WorkloadProfile Profile()
    {
        string[] names = ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];
        var payloads = names.ToImmutableDictionary(name => name,
            _ => ImmutableArray.Create(new PayloadBucket(4, 1)), StringComparer.Ordinal);
        return new WorkloadProfile(1, "auth-executor-fixture", 7, [new TenantWeight("tenant", 1)], 1,
            payloads, [new LoadStage("one", 1, 1)], new SourceLocation("synthetic-test-fixture"));
    }

    private sealed class ThrowingTokenProvider : IKafkaTokenProvider
    {
        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
            => throw new InvalidOperationException("credential details must not escape");
    }

    private sealed class FailOnceTokenProvider : IKafkaTokenProvider
    {
        private int calls;
        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == 1)
                throw new InvalidOperationException("synthetic_initial_failure");
            return new AccessToken("synthetic-token", TimeProvider.System.GetUtcNow().AddHours(1));
        }
    }

    private sealed class CategoryTokenProvider(string category) : IKafkaTokenProvider
    {
        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            if (category == "acquisition") throw new InvalidOperationException("private-token-value");
            return category == "token_validation"
                ? new AccessToken(string.Empty, DateTimeOffset.UnixEpoch)
                : new AccessToken("synthetic-token", TimeProvider.System.GetUtcNow().AddHours(1));
        }
    }

    private sealed class SequencedTokenProvider(string failingCategory) : IKafkaTokenProvider
    {
        private int calls;
        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            int call = Interlocked.Increment(ref calls);
            if (call == 2 && failingCategory == "acquisition")
                throw new InvalidOperationException("synthetic_refresh_failure");
            if (call == 2 && failingCategory == "token_validation")
                return new AccessToken(string.Empty, DateTimeOffset.UnixEpoch);
            return new AccessToken("synthetic-token", TimeProvider.System.GetUtcNow().AddHours(call));
        }
    }

    private sealed class DelaySignalingTimeProvider : TimeProvider
    {
        private readonly TaskCompletionSource delayScheduled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int delayCount;
        public Task DelayScheduled => delayScheduled.Task;
        public int DelayCount => Volatile.Read(ref delayCount);
        public override long TimestampFrequency => Stopwatch.Frequency;
        public override long GetTimestamp() => Stopwatch.GetTimestamp();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Interlocked.Increment(ref delayCount);
            delayScheduled.TrySetResult();
            return new NoopTimer();
        }

        private sealed class NoopTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    public class ControlledProducer : DispatchProxy
    {
        public int ProduceCount { get; private set; }
        public Action? AfterProduce { get; set; }
        public Action? OnDispose { get; set; }
        public Action? OnFlush { get; set; }
        public bool DeliveryError { get; set; }
        public RunEvidenceSession? Evidence { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                ProduceCount++;
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new DeliveryReport<byte[], byte[]>
                {
                    Error = new Error(DeliveryError ? ErrorCode.Local_MsgTimedOut : ErrorCode.NoError),
                    Status = DeliveryError ? PersistenceStatus.PossiblyPersisted : PersistenceStatus.Persisted
                });
                AfterProduce?.Invoke();
                return null;
            }
            if (targetMethod?.Name == "Flush") { OnFlush?.Invoke(); return 0; }
            if (targetMethod?.Name == "Dispose") { OnDispose?.Invoke(); return null; }
            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
#pragma warning restore CA1707
