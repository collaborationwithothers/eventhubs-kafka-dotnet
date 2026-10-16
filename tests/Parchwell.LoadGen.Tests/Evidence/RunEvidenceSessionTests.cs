using System.Collections.Immutable;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Xunit;

namespace Parchwell.LoadGen.Tests.Evidence;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class RunEvidenceSessionTests
{
    [Fact]
    public void Given_InvalidBudget_When_Constructing_Then_DoesNotTouchDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var options = new RunEvidenceOptions(path, 0, 1024, 4096, 8, TimeSpan.FromSeconds(1));

        Assert.Throws<ArgumentOutOfRangeException>(() => new RunEvidenceSession(Identity(), options, TimeProvider.System));
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task Given_ReportAndManagedLog_When_Completed_Then_PersistsRawReportAndHonestCapabilityEvidence()
    {
        string path = NewDirectory();
        string raw = "{\"name\":\"producer-1\",\"native_extra\":{\"kept\":true}}";
        await using (var session = new RunEvidenceSession(Identity(), Options(path), TimeProvider.System))
        {
            await session.WriteAsync(Report(raw, Snapshot(21_000, 1)), TestContext.Current.CancellationToken);
            session.CaptureLog(new LogMessage("producer-1", SyslogLevel.Warning, "BROKER", "timed out"));
            Assert.False(session.RequestCapabilitySupported);
            Assert.Equal(["statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"], session.ArtifactNames);
            await session.CompleteAsync(new KafkaStatisticsCompletion(false,
                [new KafkaStatisticsDiagnostic("final_sample_missing", "broker-1")], ["broker-1"]), TestContext.Current.CancellationToken);
            Assert.False(session.IsFaulted);
        }

        string stats = await File.ReadAllTextAsync(Path.Combine(path, "statistics.jsonl"), TestContext.Current.CancellationToken);
        using JsonDocument statsDoc = JsonDocument.Parse(stats);
        Assert.Equal(1, statsDoc.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal(raw, statsDoc.RootElement.GetProperty("raw_json").GetString());
        string lifecycle = await File.ReadAllTextAsync(Path.Combine(path, "request-lifecycle.jsonl"), TestContext.Current.CancellationToken);
        using JsonDocument lifecycleDoc = JsonDocument.Parse(lifecycle);
        Assert.Equal(JsonValueKind.Null, lifecycleDoc.RootElement.GetProperty("source_event_timestamp").ValueKind);
        Assert.Equal(JsonValueKind.Null, lifecycleDoc.RootElement.GetProperty("correlation_id").ValueKind);
        Assert.Equal(JsonValueKind.Null, lifecycleDoc.RootElement.GetProperty("source_order").ValueKind);
        string capabilities = await File.ReadAllTextAsync(Path.Combine(path, "capabilities.json"), TestContext.Current.CancellationToken);
        Assert.Contains("request_identity", capabilities, StringComparison.Ordinal);
        Assert.Contains("unsupported", capabilities, StringComparison.Ordinal);
        Assert.Contains("missing_brokers", capabilities, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_OverLimitRawReport_When_Writing_Then_FaultsAndDoesNotQueueIt()
    {
        string path = NewDirectory();
        await using var session = new RunEvidenceSession(Identity(), Options(path, maximumReportBytes: 32), TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.WriteAsync(Report(new string('x', 33), Snapshot(0, 0)), TestContext.Current.CancellationToken));
        Assert.True(session.IsFaulted);
        Assert.Contains("statistics_report_exceeds_byte_limit", session.Diagnostics);
        await session.CompleteAsync(null, TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, await File.ReadAllTextAsync(Path.Combine(path, "statistics.jsonl"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_ReusedArtifactDirectory_When_Constructing_Then_RejectsCreateOnlyConflict()
    {
        string path = NewDirectory();
        await File.WriteAllTextAsync(Path.Combine(path, "statistics.jsonl"), "existing", TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => new RunEvidenceSession(Identity(), Options(path), TimeProvider.System));
    }

    [Fact]
    public async Task Given_StatisticsFileLimitAfterLastReport_When_Completed_Then_PreservesCompletionFailureSeparately()
    {
        string path = NewDirectory();
        await using var session = new RunEvidenceSession(Identity(), Options(path, maximumReportBytes: 7000, maximumFileBytes: 8000), TimeProvider.System);
        string raw = "{\"payload\":\"" + new string('x', 5000) + "\"}";
        await session.WriteAsync(Report(raw, Snapshot(0, 0)), TestContext.Current.CancellationToken);
        await session.WriteAsync(Report(raw, Snapshot(0, 0)), TestContext.Current.CancellationToken);
        var completion = new KafkaStatisticsCompletion(false,
            [new KafkaStatisticsDiagnostic("final_snapshot_missing")], ["broker-1"]);

        await session.CompleteAsync(completion, TestContext.Current.CancellationToken);

        Assert.True(session.IsFaulted);
        string[] persisted = await File.ReadAllLinesAsync(Path.Combine(path, "statistics.jsonl"), TestContext.Current.CancellationToken);
        Assert.Single(persisted);
        string capabilities = await File.ReadAllTextAsync(Path.Combine(path, "capabilities.json"), TestContext.Current.CancellationToken);
        Assert.Contains("final_snapshot_missing", capabilities, StringComparison.Ordinal);
        Assert.Contains("broker-1", capabilities, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_TimeProviderAdvancesToCompletionDeadline_When_Completed_Then_RecordsTimeoutWithoutWallClockDelay()
    {
        string path = NewDirectory();
        RunEvidenceOptions options = Options(path);
        var timeProvider = new DeadlineAdvancingTimeProvider();
        await using var session = new RunEvidenceSession(Identity(), options, timeProvider);

        await session.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(options.CompletionTimeout, timeProvider.ScheduledDueTime);
        Assert.Equal(options.CompletionTimeout.Ticks, timeProvider.GetTimestamp());
        Assert.Contains("evidence_writer_completion_timeout", session.Diagnostics);
    }

    [Fact]
    public async Task Given_CallerCancellationDuringEvidenceCompletion_When_Completed_Then_DoesNotReportDeadlineTimeout()
    {
        string path = NewDirectory();
        await using var session = new RunEvidenceSession(Identity(), Options(path), TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await session.CompleteAsync(null, cancellation.Token);

        Assert.Contains("capabilities_write_cancelled", session.Diagnostics);
        Assert.DoesNotContain("evidence_writer_completion_timeout", session.Diagnostics);
    }

    [Fact]
    public async Task Given_PositiveStatisticsGuardCounterThenZero_When_ReceivingReports_Then_RetainsPositiveObservation()
    {
        string path = NewDirectory();
        await using var session = new RunEvidenceSession(Identity(), Options(path), TimeProvider.System);
        await session.WriteAsync(Report("{}", Snapshot(0, 0, requestTimeouts: 2)), TestContext.Current.CancellationToken);
        await session.WriteAsync(Report("{}", Snapshot(0, 0)), TestContext.Current.CancellationToken);

        Assert.Equal(["broker=broker-1;counter=req_timeouts;observed=2"], session.GuardObservations);
        Assert.False(session.IsFaulted);
        await session.CompleteAsync(null, TestContext.Current.CancellationToken);
    }

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "loadgen-evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static RunEvidenceOptions Options(string path, int maximumReportBytes = 4096, long maximumFileBytes = 64 * 1024) =>
        new(path, 8, maximumReportBytes, maximumFileBytes, 16, TimeSpan.FromSeconds(2));

    private static RunIdentity Identity() => new("A01", "C0", "run-1", "instance-1", "instance-1", "loadgen-pod-1");

    private static KafkaStatisticsReport Report(string raw, KafkaStatisticsSnapshot snapshot) => new(
        new KafkaStatisticsIdentity("A01", "run-1", KafkaClientRole.LoadGenProducer, "loadgen-pod-1"),
        "session-1", DateTimeOffset.UnixEpoch, raw, snapshot, ImmutableArray<KafkaStatisticsDiagnostic>.Empty);

    private static KafkaStatisticsSnapshot Snapshot(long throttle, long count, long requestTimeouts = 0) => new("producer-1", "loadgen-pod-1", "producer", 1, 1, 1,
        [new KafkaBrokerStatistics("broker-1", 1, "UP", 1, requestTimeouts, 0, 0, 0, 0,
            new KafkaWindowStatistics(count, count == 0 ? null : throttle), new KafkaWindowStatistics(0, null))]);

    private sealed class DeadlineAdvancingTimeProvider : TimeProvider
    {
        private long _timestamp;

        public TimeSpan? ScheduledDueTime { get; private set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state);
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                ScheduledDueTime = dueTime;
                timer.Schedule(checked(GetTimestamp() + dueTime.Ticks));
                AdvanceBy(dueTime);
                timer.FireIfDue(GetTimestamp());
            }
            return timer;
        }

        private void AdvanceBy(TimeSpan elapsed) => Interlocked.Add(ref _timestamp, elapsed.Ticks);
    }

    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        private int _disposed;
        private int _fired;
        private long _dueTimestamp;

        public bool Change(TimeSpan dueTime, TimeSpan period) => Volatile.Read(ref _disposed) == 0;

        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Schedule(long dueTimestamp) => _dueTimestamp = dueTimestamp;

        public void FireIfDue(long timestamp)
        {
            if (timestamp >= Interlocked.Read(ref _dueTimestamp) &&
                Volatile.Read(ref _disposed) == 0 && Interlocked.Exchange(ref _fired, 1) == 0)
                callback(state);
        }
    }
}
