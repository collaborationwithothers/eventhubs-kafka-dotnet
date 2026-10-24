using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Workload;
using Parchwell.LoadGen.Tests.Evidence;
using Parchwell.Scenarios;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Execution;
using Xunit;

namespace Parchwell.LoadGen.Tests.Execution;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class FinalisationDeadlineTests
{
    [Theory]
    [InlineData("producer-drain", 1)]
    [InlineData("ledger-drain", 2)]
    [InlineData("statistics-completion", 3)]
    [InlineData("evidence-drain", 4)]
    [InlineData("capabilities-save", 5)]
    [InlineData("evidence-readback", 6)]
    [InlineData("ledger-readback", 7)]
    [InlineData("manifest-save", 8)]
    public async Task Given_OneFinalisationDeadlineExpires_When_ExecutorContinues_Then_LaterPreservationUsesFreshTokens(
        string operation, int deadlineNumber)
    {
        string directory = NewDirectory();
        var clock = new SelectedDeadlineTimeProvider(deadlineNumber);
        RunIdentity identity = Identity();
        string ledgerPath = Path.Combine(directory, "ledger.jsonl");
        string manifestPath = Path.Combine(directory, "manifest.json");
        var ledgerOptions = new LedgerStoreOptions(ledgerPath, manifestPath, 8, 4096, 64 * 1024, 64 * 1024,
            TimeSpan.FromSeconds(2));
        var ledger = new DeadlineTrackingLedger(ledgerOptions, operation);
        RunEvidenceExpectations baseline = await RunEvidenceReaderTests.ExpectationsAsync(identity,
            requireStatisticsEvidence: true);
        bool requireStatistics = operation == "statistics-completion";
        RunEvidenceExpectations captureOnly = RunEvidenceExpectations.CaptureOnly(baseline.ResolvedConfiguration,
            requireStatisticsEvidence: requireStatistics);
        bool holdEvidenceWriter = operation == "evidence-drain";
        var evidenceWriterStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEvidenceWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var evidence = new RunEvidenceSession(identity,
            new RunEvidenceOptions(directory, 8, 4096, 64 * 1024, 16, TimeSpan.FromSeconds(2)), clock, captureOnly,
            holdEvidenceWriter
                ? async () =>
                {
                    evidenceWriterStarted.TrySetResult();
                    await releaseEvidenceWriter.Task.ConfigureAwait(false);
                }
                : null);
        KafkaStatisticsMetrics? statisticsMetrics = requireStatistics ? new KafkaStatisticsMetrics() : null;
        KafkaStatisticsSession? statistics = requireStatistics
            ? new KafkaStatisticsSession(new KafkaStatisticsIdentity(identity.ScenarioId, identity.RunId,
                KafkaClientRole.LoadGenProducer, identity.ClientId),
                new KafkaStatisticsOptions
            {
                MaxSnapshotCharacters = 4096, QueueCapacity = 8, MaxBrokerSeries = 8,
                CompletionTimeout = TimeSpan.FromSeconds(2)
            }, clock, new EmptyStatisticsSink(), statisticsMetrics!, nativeClockCompatible: true)
            : null;
        IProducer<byte[], byte[]> native = DispatchProxy.Create<IProducer<byte[], byte[]>, DrainTrackingProducer>();
        ((DrainTrackingProducer)(object)native).KeepQueueNonempty = operation == "producer-drain";
        var executor = new RunExecutor(() => native, ledger, clock, statistics, evidence);

        try
        {
            if (holdEvidenceWriter)
                await evidenceWriterStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
            RunExecutionResult result = await executor.ExecuteAsync(Profile(), identity, Options(directory, requireStatistics),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Manifest.BookkeepingComplete);
            Assert.Contains(deadlineNumber, clock.ExpiredDeadlineNumbers);
            Assert.True(ledger.ManifestSaveAttempts > 0);
            Assert.All(ledger.DrainTokens, token => Assert.Equal(operation == "ledger-drain", token.IsCancellationRequested));
            Assert.All(ledger.LaterTokens, token => Assert.Equal(operation == "manifest-save", token.IsCancellationRequested));
            if (operation == "manifest-save")
            {
                Assert.False(File.Exists(manifestPath));
                Assert.Contains(result.Manifest.Diagnostics, diagnostic => diagnostic.StartsWith("manifest_save_failed", StringComparison.Ordinal));
            }
            else
            {
                Assert.True(File.Exists(manifestPath));
                using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath,
                    TestContext.Current.CancellationToken));
                Assert.Equal("failed", manifest.RootElement.GetProperty("worker_status").GetString());
                Assert.False(manifest.RootElement.GetProperty("bookkeeping_complete").GetBoolean());
            }
            string capabilitiesPath = Path.Combine(directory, "capabilities.json");
            Assert.True(File.Exists(capabilitiesPath));
            if (operation != "capabilities-save") Assert.True(new FileInfo(capabilitiesPath).Length > 0);
            Assert.True(File.Exists(ledgerPath));
            Assert.True(ledger.DrainAttempts > 0);
        }
        finally
        {
            releaseEvidenceWriter.TrySetResult();
            if (statistics is not null) await statistics.DisposeAsync();
            statisticsMetrics?.Dispose();
            await evidence.DisposeAsync();
            await evidence.WriterTask.WaitAsync(TestContext.Current.CancellationToken);
            await ledger.DisposeAsync();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_EvidenceWriterAlreadyDrained_When_Finalising_Then_CapabilitiesSaveGetsFreshDeadline()
    {
        string path = NewDirectory();
        var clock = new FirstDeadlineExpiresTimeProvider();
        await using var session = new RunEvidenceSession(Identity(), EvidenceOptions(path), clock);

        await session.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(2, clock.DeadlineCount);
        Assert.Contains("evidence_writer_completion_timeout", session.Diagnostics);
        Assert.Contains("evidence_writer_failed", session.Diagnostics);
        Assert.True(File.Exists(Path.Combine(path, "capabilities.json")));
        Assert.DoesNotContain("capabilities_write_timeout", session.Diagnostics);
        Assert.True(new FileInfo(Path.Combine(path, "capabilities.json")).Length > 0);
    }

    [Fact]
    public async Task Given_NativeProducerDisposeStalls_When_Finalising_Then_UsesOneAttemptAndKeepsFailureSticky()
    {
        string directory = NewDirectory();
        RunIdentity identity = Identity();
        var ledger = new LocalLedgerStore(new LedgerStoreOptions(Path.Combine(directory, "ledger.jsonl"),
            Path.Combine(directory, "manifest.json"), 8, 4096, 64 * 1024, 64 * 1024, TimeSpan.FromSeconds(1)));
        var evidence = new RunEvidenceSession(identity, EvidenceOptions(directory), TimeProvider.System);
        var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, StalledDisposeProducer>();
        StalledDisposeProducer tracker = (StalledDisposeProducer)(object)producer;
        var executor = new RunExecutor(() => producer, ledger, TimeProvider.System, statistics: null, evidence);

        try
        {
            RunExecutionResult result = await executor.ExecuteAsync(Profile(), identity,
                Options(directory, requireStatistics: false) with { CleanupTimeout = TimeSpan.FromMilliseconds(50) },
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("producer_dispose_timeout", result.Manifest.Diagnostics);
            Assert.Equal(1, tracker.DisposeAttempts);
            tracker.Release.TrySetResult();
            await tracker.DisposeFinished.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, tracker.DisposeAttempts);
            Assert.Contains("producer_dispose_timeout", result.Manifest.Diagnostics);
        }
        finally
        {
            tracker.Release.TrySetResult();
            await evidence.DisposeAsync();
            await ledger.DisposeAsync();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Given_ExecutionTokenWasCancelledBeforeFinalisation_When_CapabilitiesAreSaved_Then_SaveUsesIndependentToken()
    {
        string path = NewDirectory();
        var clock = new FirstDeadlineExpiresTimeProvider();
        await using var session = new RunEvidenceSession(Identity(), EvidenceOptions(path), clock);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await session.CompleteAsync(null, cancellation.Token);

        Assert.Equal(2, clock.DeadlineCount);
        Assert.True(File.Exists(Path.Combine(path, "capabilities.json")));
        Assert.DoesNotContain("capabilities_write_cancelled", session.Diagnostics);
        Assert.DoesNotContain("capabilities_write_timeout", session.Diagnostics);
    }

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "loadgen-finalisation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static RunIdentity Identity() => new("A01", "C0", "finalisation-run", "pod-one", "pod-one", "pod-one");

    private static RunEvidenceOptions EvidenceOptions(string path) =>
        new(path, 8, 4096, 64 * 1024, 16, TimeSpan.FromSeconds(1));

    private static RunExecutionOptions Options(string directory, bool requireStatistics) => new(8, 256,
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), 4096, 64 * 1024,
        64 * 1024, directory, "deadline-topic", [], RequireRequestEvidence: false,
        RequireStatisticsEvidence: requireStatistics, LedgerDrainTimeout: TimeSpan.FromSeconds(2),
        StatisticsCompletionTimeout: TimeSpan.FromSeconds(2), EvidenceCompletionTimeout: TimeSpan.FromSeconds(2));

    private static WorkloadProfile Profile()
    {
        string[] eventTypes = ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];
        var payloads = eventTypes.ToImmutableDictionary(name => name,
            _ => ImmutableArray.Create(new PayloadBucket(4, 1)), StringComparer.Ordinal);
        return new WorkloadProfile(1, "finalisation", 7, [new TenantWeight("tenant", 1)], 1, payloads,
            [new LoadStage("one", 1, 1)], new SourceLocation("synthetic-finalisation-profile"));
    }

    private sealed class FirstDeadlineExpiresTimeProvider : TimeProvider
    {
        private int _deadlineCount;
        public int DeadlineCount => Volatile.Read(ref _deadlineCount);

        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow();
        public override long GetTimestamp() => TimeProvider.System.GetTimestamp();
        public override long TimestampFrequency => TimeProvider.System.TimestampFrequency;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            bool expiresImmediately = dueTime != Timeout.InfiniteTimeSpan && Interlocked.Increment(ref _deadlineCount) == 1;
            var timer = new ManualTimer();
            if (expiresImmediately) callback(state);
            return timer;
        }
    }

    private sealed class SelectedDeadlineTimeProvider(int selectedDeadline) : TimeProvider
    {
        private int _deadlineCount;
        private readonly List<int> _expired = [];
        public IReadOnlyList<int> ExpiredDeadlineNumbers => _expired;
        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow();
        public override long GetTimestamp() => TimeProvider.System.GetTimestamp();
        public override long TimestampFrequency => TimeProvider.System.TimestampFrequency;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            int number = dueTime == Timeout.InfiniteTimeSpan ? 0 : Interlocked.Increment(ref _deadlineCount);
            if (number == selectedDeadline)
            {
                lock (_expired) _expired.Add(number);
                callback(state);
            }
            return new ManualTimer();
        }
    }

    private sealed class DeadlineTrackingLedger : ILedgerStore
    {
        private readonly LocalLedgerStore _inner;
        private readonly string _operation;
        private readonly List<CancellationToken> _laterTokens = [];
        private readonly List<CancellationToken> _drainTokens = [];
        public int DrainAttempts { get; private set; }
        public int ManifestSaveAttempts { get; private set; }
        public IReadOnlyList<CancellationToken> LaterTokens => _laterTokens;
        public IReadOnlyList<CancellationToken> DrainTokens => _drainTokens;
        public bool IsFaulted => _inner.IsFaulted;
        public Exception? Failure => _inner.Failure;

        public DeadlineTrackingLedger(LedgerStoreOptions options, string operation)
        {
            _inner = new LocalLedgerStore(options);
            _operation = operation;
        }

        public bool TryAppend(LedgerRow row) => _inner.TryAppend(row);

        public async Task DrainAsync(CancellationToken cancellationToken = default)
        {
            DrainAttempts++;
            _drainTokens.Add(cancellationToken);
            if (_operation == "ledger-drain" && cancellationToken.IsCancellationRequested)
            {
                await _inner.DrainAsync(CancellationToken.None).ConfigureAwait(false);
                throw new OperationCanceledException(cancellationToken);
            }
            await _inner.DrainAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task SaveManifestAsync(RunManifest manifest, CancellationToken cancellationToken = default)
        {
            ManifestSaveAttempts++;
            _laterTokens.Add(cancellationToken);
            if (_operation == "manifest-save" && cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);
            await _inner.SaveManifestAsync(manifest, cancellationToken).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private sealed class EmptyStatisticsSink : IKafkaStatisticsEvidenceSink
    {
        public ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    public class DrainTrackingProducer : DispatchProxy
    {
        public bool KeepQueueNonempty { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new DeliveryReport<byte[], byte[]>
                { Error = new Error(ErrorCode.NoError), Status = PersistenceStatus.Persisted });
                return null;
            }
            if (targetMethod?.Name == "Flush") return KeepQueueNonempty ? 1 : 0;
            if (targetMethod?.Name == "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    public class StalledDisposeProducer : DispatchProxy
    {
        private int _disposeAttempts;
        public int DisposeAttempts => Volatile.Read(ref _disposeAttempts);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DisposeFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new DeliveryReport<byte[], byte[]>
                { Error = new Error(ErrorCode.NoError), Status = PersistenceStatus.Persisted });
                return null;
            }
            if (targetMethod?.Name == "Flush") return 0;
            if (targetMethod?.Name == "Dispose")
            {
                Interlocked.Increment(ref _disposeAttempts);
                try { Release.Task.GetAwaiter().GetResult(); }
                finally { DisposeFinished.TrySetResult(); }
                return null;
            }
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private int _disposed;

        public bool Change(TimeSpan dueTime, TimeSpan period) => Volatile.Read(ref _disposed) == 0;

        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
#pragma warning restore CA1707
