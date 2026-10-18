using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task Given_CompletionBoundary_When_CapabilitiesAreWritten_Then_PersistsTheExactMicroseconds()
    {
        RunEvidenceExpectations expectations = await RunEvidenceReaderTests.ExpectationsAsync(Identity());
        string boundedPath = NewDirectory();
        await using (var session = new RunEvidenceSession(Identity(), Options(boundedPath), TimeProvider.System, expectations))
        {
            await session.WriteAsync(Report("{}", Snapshot(0, 0)), TestContext.Current.CancellationToken);
            await session.CompleteAsync(new KafkaStatisticsCompletion(true,
                ImmutableArray<KafkaStatisticsDiagnostic>.Empty, ImmutableArray<string>.Empty)
            {
                DrainBoundaryMicroseconds = 10_000_001
            }, TestContext.Current.CancellationToken);
        }

        using (JsonDocument capabilities = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(boundedPath, "capabilities.json"), TestContext.Current.CancellationToken)))
        {
            JsonElement completion = capabilities.RootElement.GetProperty("statistics_completion");
            Assert.Equal(JsonValueKind.Number, completion.GetProperty("drain_boundary_microseconds").ValueKind);
            Assert.Equal(10_000_001L, completion.GetProperty("drain_boundary_microseconds").GetInt64());
            string[] settingNames = capabilities.RootElement.GetProperty("resolved_configuration")
                .GetProperty("settings").EnumerateObject().Select(property => property.Name).ToArray();
            Assert.Equal(settingNames.OrderBy(name => name, StringComparer.Ordinal), settingNames);
        }

        string unverifiedPath = NewDirectory();
        await using (var session = new RunEvidenceSession(Identity(), Options(unverifiedPath), TimeProvider.System, expectations))
        {
            await session.WriteAsync(Report("{}", Snapshot(0, 0)), TestContext.Current.CancellationToken);
            await session.CompleteAsync(new KafkaStatisticsCompletion(false,
                [new KafkaStatisticsDiagnostic("clock_unverified")], ["broker-1"])
            {
                DrainBoundaryMicroseconds = null
            }, TestContext.Current.CancellationToken);
        }

        using (JsonDocument capabilities = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(unverifiedPath, "capabilities.json"), TestContext.Current.CancellationToken)))
        {
            JsonElement completion = capabilities.RootElement.GetProperty("statistics_completion");
            Assert.Equal(JsonValueKind.Null, completion.GetProperty("drain_boundary_microseconds").ValueKind);
        }

        string absentCompletionPath = NewDirectory();
        await using (var session = new RunEvidenceSession(Identity(), Options(absentCompletionPath), TimeProvider.System, expectations))
        {
            await session.WriteAsync(Report("{}", Snapshot(0, 0)), TestContext.Current.CancellationToken);
            await session.CompleteAsync(null, TestContext.Current.CancellationToken);
        }

        using JsonDocument absentCompletionCapabilities = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(absentCompletionPath, "capabilities.json"), TestContext.Current.CancellationToken));
        Assert.Equal(JsonValueKind.Null,
            absentCompletionCapabilities.RootElement.GetProperty("statistics_completion").ValueKind);
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
    public async Task Given_AtLimitQuoteHeavyRawReport_When_Writing_Then_PersistsWithinExplicitEnvelopeBudget()
    {
        string path = NewDirectory();
        string escapedQuotes = string.Concat(Enumerable.Repeat("\\\"", 2048));
        string raw = "{\"name\":\"producer-1\",\"payload\":\"" + escapedQuotes + "\"}";
        RunEvidenceOptions options = Options(path, maximumReportBytes: Encoding.UTF8.GetByteCount(raw), maximumFileBytes: 32 * 1024);
        await using var session = new RunEvidenceSession(Identity(), options, TimeProvider.System);

        await session.WriteAsync(Report(raw, Snapshot(0, 0)), TestContext.Current.CancellationToken);
        await session.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.False(session.IsFaulted);
        byte[] row = await File.ReadAllBytesAsync(Path.Combine(path, "statistics.jsonl"), TestContext.Current.CancellationToken);
        Assert.True(row.Length > options.MaximumReportBytes + 4096);
        Assert.True(row.Length <= options.MaximumStatisticsRecordBytes);
        using JsonDocument document = JsonDocument.Parse(row);
        Assert.Equal(raw, document.RootElement.GetProperty("raw_json").GetString());
    }

    [Fact]
    public async Task Given_EnvelopeExceedsIndependentRecordBudget_When_Writing_Then_RejectsBeforeQueueing()
    {
        string path = NewDirectory();
        RunEvidenceOptions options = Options(path, maximumReportBytes: 64, maximumFileBytes: 16 * 1024);
        await using var session = new RunEvidenceSession(Identity(), options, TimeProvider.System);
        KafkaStatisticsSnapshot oversizedSnapshot = Snapshot(0, 0) with { Name = new string('x', 20_000) };

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.WriteAsync(Report("{}", oversizedSnapshot), TestContext.Current.CancellationToken));

        Assert.True(session.IsFaulted);
        Assert.Contains("statistics_report_envelope_exceeds_byte_limit", session.Diagnostics);
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
    public async Task Given_PersistedFaultFlagContradictsLiveState_When_Validating_Then_FailsAndLatchesSession()
    {
        string path = NewDirectory();
        RunIdentity identity = Identity();
        RunEvidenceExpectations expectations = await RunEvidenceReaderTests.ExpectationsAsync(identity,
            expectedBrokerNames: ImmutableArray.Create("broker-1"));
        await using var session = new RunEvidenceSession(identity, Options(path), TimeProvider.System, expectations);
        await session.WriteAsync(Report("{}", Snapshot(0, 0)), TestContext.Current.CancellationToken);
        await session.CompleteAsync(null, TestContext.Current.CancellationToken);
        Assert.False(session.IsFaulted);
        await session.ValidatePersistedAsync(TestContext.Current.CancellationToken);

        string capabilitiesPath = Path.Combine(path, "capabilities.json");
        JsonNode capabilities = JsonNode.Parse(await File.ReadAllTextAsync(capabilitiesPath, TestContext.Current.CancellationToken))!;
        capabilities["is_faulted"] = true;
        await File.WriteAllTextAsync(capabilitiesPath, capabilities.ToJsonString(), TestContext.Current.CancellationToken);

        InvalidDataException faultStateFailure = await Assert.ThrowsAsync<InvalidDataException>(
            () => session.ValidatePersistedAsync(TestContext.Current.CancellationToken));
        Assert.Equal("persisted_evidence_fault_state_mismatch", faultStateFailure.Message);
        Assert.True(session.IsFaulted);
        Assert.Contains("persisted_evidence_validation_failed", session.Diagnostics);
    }

    [Fact]
    public async Task Given_PersistedCompletionMarkerWasNeverProduced_When_Validating_Then_FailsAndLatchesSession()
    {
        string path = NewDirectory();
        RunIdentity identity = Identity();
        RunEvidenceExpectations expectations = await RunEvidenceReaderTests.ExpectationsAsync(identity,
            expectedBrokerNames: ImmutableArray.Create("broker-1"));
        await using var session = new RunEvidenceSession(identity, Options(path), TimeProvider.System, expectations);
        await session.WriteAsync(Report("{}", Snapshot(0, 0)), TestContext.Current.CancellationToken);
        await session.CompleteAsync(null, TestContext.Current.CancellationToken);
        await session.ValidatePersistedAsync(TestContext.Current.CancellationToken);

        string capabilitiesPath = Path.Combine(path, "capabilities.json");
        JsonNode capabilities = JsonNode.Parse(await File.ReadAllTextAsync(capabilitiesPath, TestContext.Current.CancellationToken))!;
        Assert.Null(capabilities["statistics_completion"]);
        capabilities["statistics_completion"] = JsonNode.Parse("{\"is_complete\":true,\"diagnostics\":[],\"missing_brokers\":[],\"drain_boundary_microseconds\":null}");
        await File.WriteAllTextAsync(capabilitiesPath, capabilities.ToJsonString(), TestContext.Current.CancellationToken);

        InvalidDataException completionFailure = await Assert.ThrowsAsync<InvalidDataException>(
            () => session.ValidatePersistedAsync(TestContext.Current.CancellationToken));
        Assert.Equal("persisted_evidence_completion_state_mismatch", completionFailure.Message);
        Assert.True(session.IsFaulted);
        Assert.Contains("persisted_evidence_validation_failed", session.Diagnostics);
    }

    [Fact]
    public async Task Given_PersistedGuardMetadataDiffersFromLiveCapture_When_Validating_Then_FailsAndLatchesSession()
    {
        string path = NewDirectory();
        RunIdentity identity = Identity();
        RunEvidenceExpectations expectations = await RunEvidenceReaderTests.ExpectationsAsync(identity,
            expectedBrokerNames: ImmutableArray.Create("broker-1"));
        await using var session = new RunEvidenceSession(identity, Options(path), TimeProvider.System, expectations);
        KafkaStatisticsSnapshot firstSnapshot = Snapshot(0, 0, requestTimeouts: 2);
        KafkaStatisticsSnapshot laterSnapshot = firstSnapshot with
        {
            TimestampMicroseconds = 2,
            AgeMicroseconds = 2,
            Brokers = [new KafkaBrokerStatistics("broker-1", 1, "UP", 2, 3, 0, 0, 0, 0,
                new KafkaWindowStatistics(0, null), new KafkaWindowStatistics(0, null))]
        };
        await session.WriteAsync(Report("{}", firstSnapshot), TestContext.Current.CancellationToken);
        await session.WriteAsync(Report("{}", laterSnapshot), TestContext.Current.CancellationToken);
        await session.CompleteAsync(null, TestContext.Current.CancellationToken);
        Assert.False(session.IsFaulted);
        Assert.True(session.GuardObservationsComplete);
        Assert.Equal(["broker=broker-1;counter=req_timeouts;observed=2"], session.GuardObservations);
        await session.ValidatePersistedAsync(TestContext.Current.CancellationToken);

        string capabilitiesPath = Path.Combine(path, "capabilities.json");
        JsonNode capabilities = JsonNode.Parse(await File.ReadAllTextAsync(capabilitiesPath, TestContext.Current.CancellationToken))!;
        capabilities["guard_observations"]![0] = "broker=broker-1;counter=req_timeouts;observed=3";
        await File.WriteAllTextAsync(capabilitiesPath, capabilities.ToJsonString(), TestContext.Current.CancellationToken);

        InvalidDataException guardStateFailure = await Assert.ThrowsAsync<InvalidDataException>(
            () => session.ValidatePersistedAsync(TestContext.Current.CancellationToken));
        Assert.Equal("persisted_evidence_guard_observation_state_mismatch", guardStateFailure.Message);
        Assert.True(session.IsFaulted);
        Assert.Contains("persisted_evidence_validation_failed", session.Diagnostics);
    }

    [Fact]
    public async Task Given_PersistedGuardCompletenessDiffersFromLiveCapture_When_Validating_Then_FailsAndLatchesSession()
    {
        string path = NewDirectory();
        RunIdentity identity = Identity();
        RunEvidenceExpectations expectations = await RunEvidenceReaderTests.ExpectationsAsync(identity,
            expectedBrokerNames: ImmutableArray.Create("broker-1"));
        await using var session = new RunEvidenceSession(identity,
            Options(path, maximumReportBytes: 32), TimeProvider.System, expectations);
        await session.WriteAsync(Report("{}", Snapshot(0, 0, requestTimeouts: 2)), TestContext.Current.CancellationToken);
        session.CaptureLog(new LogMessage("producer-1", SyslogLevel.Warning, "BROKER", "oversized capture"));
        await session.CompleteAsync(null, TestContext.Current.CancellationToken);
        Assert.True(session.IsFaulted);
        Assert.True(session.GuardObservationsComplete);
        await session.ValidatePersistedAsync(TestContext.Current.CancellationToken);

        string capabilitiesPath = Path.Combine(path, "capabilities.json");
        JsonNode capabilities = JsonNode.Parse(await File.ReadAllTextAsync(capabilitiesPath, TestContext.Current.CancellationToken))!;
        capabilities["guard_observations_complete"] = false;
        await File.WriteAllTextAsync(capabilitiesPath, capabilities.ToJsonString(), TestContext.Current.CancellationToken);

        InvalidDataException guardStateFailure = await Assert.ThrowsAsync<InvalidDataException>(
            () => session.ValidatePersistedAsync(TestContext.Current.CancellationToken));
        Assert.Equal("persisted_evidence_guard_observation_state_mismatch", guardStateFailure.Message);
        Assert.True(session.IsFaulted);
        Assert.Contains("persisted_evidence_validation_failed", session.Diagnostics);
    }

    [Fact]
    public async Task Given_StatisticsExpectationHasInvalidBrokerInventory_When_Constructing_Then_RejectsIt()
    {
        RunEvidenceExpectations baseline = await RunEvidenceReaderTests.ExpectationsAsync(Identity(),
            expectedBrokerNames: ImmutableArray.Create("broker-1"));

        Assert.Throws<ArgumentException>(() => new RunEvidenceExpectations(baseline.ResolvedConfiguration, true, []));
        Assert.Throws<ArgumentException>(() => new RunEvidenceExpectations(baseline.ResolvedConfiguration, false,
            ["broker-1", "broker-1"]));
        Assert.Throws<ArgumentException>(() => new RunEvidenceExpectations(baseline.ResolvedConfiguration, false,
            ["broker-1", " "]));
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_NativeErrorAndCounterGuardsExceedSharedCapacity_When_Capturing_Then_SticksToTheBound(bool errorFirst)
    {
        string path = NewDirectory();
        await using var session = new RunEvidenceSession(Identity(), Options(path, maximumDiagnostics: 1), TimeProvider.System);

        if (errorFirst) session.CaptureError(new Error(ErrorCode.Local_TimedOut));
        await session.WriteAsync(Report("{}", Snapshot(0, 0, requestTimeouts: 2)), TestContext.Current.CancellationToken);
        if (!errorFirst) session.CaptureError(new Error(ErrorCode.Local_TimedOut));

        Assert.Single(session.GuardObservations);
        Assert.False(session.GuardObservationsComplete);
        Assert.True(session.IsFaulted);
        Assert.Contains("statistics_guard_observations_truncated", session.Diagnostics);
        await session.CompleteAsync(null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_ConcurrentNativeErrorAndCounterGuards_When_CapacityIsOne_Then_RetainsAtMostOneObservation()
    {
        string path = NewDirectory();
        await using var session = new RunEvidenceSession(Identity(), Options(path, maximumDiagnostics: 1), TimeProvider.System);
        using var barrier = new Barrier(3);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Task statisticsCapture = Task.Run(async () =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(5), cancellationToken)) throw new TimeoutException("statistics_capture_barrier_timeout");
            await session.WriteAsync(Report("{}", Snapshot(0, 0, requestTimeouts: 2)), cancellationToken);
        }, cancellationToken);
        Task errorCapture = Task.Run(() =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(5), cancellationToken)) throw new TimeoutException("error_capture_barrier_timeout");
            session.CaptureError(new Error(ErrorCode.Local_TimedOut));
        }, cancellationToken);

        Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(5), cancellationToken));
        await Task.WhenAll(statisticsCapture, errorCapture);

        Assert.Single(session.GuardObservations);
        Assert.False(session.GuardObservationsComplete);
        Assert.True(session.IsFaulted);
        Assert.Contains("statistics_guard_observations_truncated", session.Diagnostics);
        await session.CompleteAsync(null, TestContext.Current.CancellationToken);
    }

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "loadgen-evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static RunEvidenceOptions Options(string path, int maximumReportBytes = 4096, long maximumFileBytes = 64 * 1024,
        int maximumDiagnostics = 16) =>
        new(path, 8, maximumReportBytes, maximumFileBytes, maximumDiagnostics, TimeSpan.FromSeconds(2));

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
