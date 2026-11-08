using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
public sealed class B10AbruptProducerTests
{
    private static readonly string[] EventNames = ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];

    [Fact]
    public async Task Given_OutputDirectoryCollision_When_Initializing_Then_ReportsCaptureGapBeforeConstructingProducer()
    {
        await using var fixture = new Fixture();
        string path = Path.Combine(fixture.Root, "abrupt-child");
        await File.WriteAllBytesAsync(path, [1], TestContext.Current.CancellationToken);

        var exception = Assert.Throws<IOException>(() => fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options));

        Assert.Equal(0, fixture.Constructions);
        AssertCaptureGapReport(exception, "b10_abrupt_capture_gap exception_type=System.IO.IOException", path);
    }

    [Fact]
    public async Task Given_ExistingDurableDestination_When_Writing_Then_ReportsCaptureGapAndPreservesExistingBytes()
    {
        await using var fixture = new Fixture();
        string path = Path.Combine(fixture.Root, "private-existing-output.bin");
        byte[] retained = [1, 2, 3];
        await File.WriteAllBytesAsync(path, retained, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<IOException>(() => B10AbruptProducer.WriteDurableAsync(
            path, [4, 5], TestContext.Current.CancellationToken));

        Assert.Equal(retained, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        AssertCaptureGapReport(exception, "b10_abrupt_capture_gap exception_type=System.IO.IOException", path);
    }

    [Fact]
    public async Task Given_NewDurableDestination_When_Writing_Then_RetainsExactBytes()
    {
        await using var fixture = new Fixture();
        string path = Path.Combine(fixture.Root, "new-output", "payload.bin");
        byte[] bytes = [1, 2, 3];

        await B10AbruptProducer.WriteDurableAsync(path, bytes, TestContext.Current.CancellationToken);

        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Given_UnauthorizedLocalAccess_When_Reported_Then_EmitsTypeWithoutPrivateMessageOrPath()
    {
        const string path = "/private-output/synthetic-token";
        var exception = new UnauthorizedAccessException("private-access-message " + path);

        AssertCaptureGapReport(exception, "b10_abrupt_capture_gap exception_type=System.UnauthorizedAccessException", path);
    }

    [Fact]
    public void Given_FrozenReleaseUtc_When_InnerClockAdvancesOneDay_Then_ResumingExposesAdvancedUtc()
    {
        var release = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        var inner = new ReleaseBoundaryClock(release);
        var clock = new ReleaseWindowClock(inner);

        Assert.Equal(release, clock.FreezeUtc());
        inner.Advance(TimeSpan.FromDays(1));
        Assert.Equal(release, clock.GetUtcNow());

        clock.ResumeUtc();
        Assert.Equal(release.AddDays(1), clock.GetUtcNow());
    }

    [Fact]
    public async Task Given_SuppliedClock_When_ObservingStatistics_Then_ReceivedAtUsesItsUtc()
    {
        var received = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        var clock = new ReleaseBoundaryClock(received);
        await using var fixture = new Fixture(clock: clock);

        Assert.Equal(received, fixture.Observe().ReceivedAt);
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(received.AddDays(1), fixture.Observe().ReceivedAt);
    }

    [Fact]
    public async Task Given_PreparedAcknowledgementHeld_When_Invoking_Then_NoNativeCallOccurs()
    {
        await using var fixture = new Fixture();
        var release = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Send = (kind, _) => kind == "prepared" ? release.Task : Task.FromResult(2L);
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        Task<B10AbruptProducer.ProbeAttempt> operation = fixture.Runner.InvokeProbe("same_instance", [1, 2], null, TestContext.Current.CancellationToken);
        Assert.Empty(fixture.Native.Calls);
        Assert.False(operation.IsCompleted);
        release.SetResult(1);
        await operation;
        Assert.Single(fixture.Native.Calls);
    }

    [Fact]
    public async Task Given_AcceptanceAcknowledgementHeld_When_Invoking_Then_InvocationDoesNotComplete()
    {
        await using var fixture = new Fixture();
        var release = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Send = (kind, _) => kind == "accepted" ? release.Task : Task.FromResult(1L);
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        Task<B10AbruptProducer.ProbeAttempt> operation = fixture.Runner.InvokeProbe("same_instance", [1], null, TestContext.Current.CancellationToken);
        await fixture.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(operation.IsCompleted);
        Assert.Equal(0, fixture.Releases);
        release.SetResult(2);
        await operation;
    }

    [Fact]
    public async Task Given_CallbackBeforeProduceReturns_When_Invoking_Then_GenuineTerminalPrecedesAcceptance()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Native.OnProduce = (_, callback) => callback(Report());
        var attempt = await fixture.Runner.InvokeProbe("same_instance", [1], null, TestContext.Current.CancellationToken);
        Assert.NotNull(attempt.Terminal);
        Assert.Equal(["prepared", "terminal", "accepted"], fixture.Events.Select(item => item.Kind));
        Assert.Equal(2, attempt.Terminal.Value.GetProperty("persistence_status").GetInt32());
    }

    [Fact]
    public async Task Given_NoCallback_When_Invoking_Then_NoTerminalOrPersistenceIsInvented()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        var attempt = await fixture.Runner.InvokeProbe("same_instance", [1], null, TestContext.Current.CancellationToken);
        Assert.Null(attempt.Terminal);
        Assert.DoesNotContain(fixture.Events, item => item.Kind == "terminal");
    }

    [Fact]
    public async Task Given_SynchronousRejection_When_Invoking_Then_NoAcceptedRecordIsWritten()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Native.OnProduce = (_, _) => throw new ProduceException<byte[], byte[]>(new Error(ErrorCode.Local_QueueFull), new DeliveryResult<byte[], byte[]>());
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.InvokeProbe("same_instance", [1], null, TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_guard_failed", exception.Message);
        Assert.Equal(["prepared", "synchronous_rejection"], fixture.Events.Select(item => item.Kind));
    }

    [Fact]
    public async Task Given_PreAcceptanceReport_When_Invoking_Then_RawFileAndExactAttributionPrecedeAccepted()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        KafkaStatisticsReport report = fixture.Observe();
        await Task.Delay(2, TestContext.Current.CancellationToken);
        await fixture.Runner.InvokeProbe("same_instance", [1], null, TestContext.Current.CancellationToken);
        JsonElement handoff = Assert.Single(fixture.Events, item => item.Kind == "pre_acceptance_report").Data;
        Assert.Equal("B10", handoff.GetProperty("report_identity").GetProperty("scenario_id").GetString());
        Assert.Equal("run-1", handoff.GetProperty("report_identity").GetProperty("run_id").GetString());
        Assert.Equal("LoadGenProducer", handoff.GetProperty("report_identity").GetProperty("role").GetString());
        Assert.Equal("client", handoff.GetProperty("report_identity").GetProperty("client_id").GetString());
        Assert.Equal(1, handoff.GetProperty("report_sequence").GetInt64());
        Assert.Equal(report.ReceivedAt.ToString("O", CultureInfo.InvariantCulture), handoff.GetProperty("report_capture_utc").GetString());
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(fixture.Root, "abrupt-child", "same_instance-pre-acceptance-report.json"), TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(report.RawJson), bytes);
        Assert.Equal(bytes.Length, handoff.GetProperty("report_length").GetInt32());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), handoff.GetProperty("report_sha256").GetString());
        Assert.Equal(["prepared", "pre_acceptance_report", "accepted"], fixture.Events.Select(item => item.Kind));
    }

    [Fact]
    public async Task Given_OverflowedCounter_When_Invoking_Then_EarlierReportCannotRepairMissingSample()
    {
        await using var fixture = new Fixture(long.MaxValue - 1);
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Observe();
        fixture.Observe();
        await fixture.Runner.InvokeProbe("same_instance", [1], null, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(fixture.Events, item => item.Kind == "pre_acceptance_report");
        Assert.Contains(fixture.Events, item => item.Kind == "accepted");
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("broker")]
    [InlineData("txerrs")]
    [InlineData("rxerrs")]
    [InlineData("reset")]
    [InlineData("settings")]
    public void Given_InvalidStatistics_When_Selecting_Then_GuardFails(string invalid)
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string> { ["a"] = "b" },
            new Dictionary<string, string> { ["a"] = invalid == "settings" ? "changed" : "b" });
        stats.MarkConstructed(DateTimeOffset.MinValue);
        KafkaStatisticsReport report = StatisticsReport(identity);
        if (invalid == "identity") report = report with { Identity = identity with { RunId = "other" } };
        var broker = report.Snapshot!.Brokers[0];
        if (invalid == "broker") broker = broker with { Name = "foreign" };
        if (invalid == "txerrs") broker = broker with { TransmissionErrors = 1 };
        if (invalid == "rxerrs") broker = broker with { ReceiveErrors = 1 };
        if (invalid == "reset")
        {
            stats.Observe(report with { Snapshot = report.Snapshot with { Brokers = [broker with { RequestTimeouts = 1 }] } });
        }
        stats.Observe(report with { Snapshot = report.Snapshot with { Brokers = [broker],
            TimestampMicroseconds = report.Snapshot.TimestampMicroseconds + 1, AgeMicroseconds = report.Snapshot.AgeMicroseconds + 1 } });
        Assert.Equal("b10_abrupt_guard_failed", Assert.Throws<InvalidDataException>(() => stats.SelectPreAcceptance(DateTimeOffset.MaxValue)).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AvailableCapturedStatistics_When_ReadBack_Then_RejectsObservedTransmissionError(bool transmissionError)
    {
        await using var fixture = new Fixture();
        var reportIdentity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var report = StatisticsReport(reportIdentity);
        if (transmissionError) report = report with { Snapshot = report.Snapshot! with
            { Brokers = [report.Snapshot.Brokers[0] with { TransmissionErrors = 1 }] } };
        byte[] row = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 1, identity = fixture.Identity, statistics_identity = report.Identity,
            session_id = report.SessionId, received_at = report.ReceivedAt, receipt_time = report.ReceivedAt,
            raw_json = report.RawJson, typed_snapshot = report.Snapshot, diagnostics = report.Diagnostics
        }, B10AbruptWire.JsonOptions);
        string path = Path.Combine(fixture.Root, "statistics.jsonl");
        await File.WriteAllBytesAsync(path, [.. row, (byte)'\n'], TestContext.Current.CancellationToken);
        Task check = B10AbruptStatistics.ValidateCapturedAsync(path, reportIdentity, ["broker"],
            new Dictionary<string, string>(), new Dictionary<string, string>(), TestContext.Current.CancellationToken);
        if (transmissionError)
            Assert.Equal("b10_abrupt_guard_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => check)).Message);
        else await check;
    }

    [Fact]
    public async Task Given_EmptyAvailableStatistics_When_ReadBack_Then_DoesNotInventASampleCompletenessGate()
    {
        await using var fixture = new Fixture();
        string path = Path.Combine(fixture.Root, "statistics.jsonl");
        await File.WriteAllBytesAsync(path, [], TestContext.Current.CancellationToken);
        await B10AbruptStatistics.ValidateCapturedAsync(path, new("B10", "run-1", KafkaClientRole.LoadGenProducer, "client"),
            ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Given_DroppedNativePrefix_When_LaterReportReachesSink_Then_GapRemainsSticky()
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.ActivateNativeCaptureTracking();
        stats.MarkConstructed(DateTimeOffset.MinValue);
        var dropped = StatisticsReport(identity);
        var later = StatisticsReport(identity);
        stats.CaptureNative(dropped.RawJson);
        stats.CaptureNative(later.RawJson);
        stats.Observe(later);
        Assert.Equal("b10_abrupt_guard_failed", Assert.Throws<InvalidDataException>(() => stats.SelectPreAcceptance(DateTimeOffset.MaxValue)).Message);
        var valid = StatisticsReport(identity);
        stats.CaptureNative(valid.RawJson);
        stats.Observe(valid);
        Assert.Equal("b10_abrupt_guard_failed", Assert.Throws<InvalidDataException>(() => stats.SelectPreAcceptance(DateTimeOffset.MaxValue)).Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_NativeOversizeOrQueueOverflow_When_Selecting_Then_NoLaterReportRepairsCapture(bool oversize)
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.ActivateNativeCaptureTracking();
        var report = StatisticsReport(identity);
        if (oversize) stats.CaptureNative(new string('x', 1048577));
        else for (int index = 0; index < 2049; index++) stats.CaptureNative(report.RawJson);
        stats.CaptureNative(report.RawJson);
        stats.Observe(report);
        Assert.Equal("b10_abrupt_guard_failed", Assert.Throws<InvalidDataException>(() => stats.SelectPreAcceptance(DateTimeOffset.MaxValue)).Message);
    }

    [Fact]
    public void Given_JoinedPostDrainPrefixAndPendingLaterCallback_When_Sealing_Then_EligiblePrefixRemainsComplete()
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.ActivateNativeCaptureTracking();
        var report = StatisticsReport(identity);
        DateTimeOffset drained = report.ReceivedAt.AddTicks(-1);
        stats.MarkDrainBoundary(drained, report.Snapshot!.TimestampMicroseconds - 1, 1000000);
        stats.CaptureNative(report.RawJson);
        stats.Observe(report);
        stats.CaptureNative(StatisticsReport(identity).RawJson);
        Assert.NotNull(stats.SelectPostDrain(drained));
        Assert.Equal("not_implemented_pr3", stats.SealCohort("timeout", drained).GetProperty("fatal_capture_status").GetString());
    }

    [Fact]
    public void Given_AdditionalStableQualifyingBroker_When_Selecting_Then_ExpectedSubsetPasses()
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.MarkConstructed(DateTimeOffset.MinValue);
        for (int index = 0; index < 2; index++)
        {
            var report = StatisticsReport(identity);
            JsonNode raw = JsonNode.Parse(report.RawJson)!;
            JsonNode additional = raw["brokers"]!["broker"]!.DeepClone();
            additional["name"] = "additional";
            additional["nodeid"] = -1;
            additional["source"] = "configured";
            raw["brokers"]!["additional"] = additional;
            var extra = report.Snapshot!.Brokers[0] with { Name = "additional", NodeId = -1 };
            stats.Observe(report with { RawJson = raw.ToJsonString(), Snapshot = report.Snapshot with { Brokers = report.Snapshot.Brokers.Add(extra) } });
        }
        Assert.NotNull(stats.SelectPreAcceptance(DateTimeOffset.MaxValue));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_MissingSourceOrChangedQualifyingSet_When_Selecting_Then_GuardFails(bool missing)
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        stats.MarkConstructed(DateTimeOffset.MinValue);
        stats.Observe(StatisticsReport(identity));
        var next = StatisticsReport(identity);
        JsonNode raw = JsonNode.Parse(next.RawJson)!;
        if (missing) raw["brokers"]!["broker"]!.AsObject().Remove("source");
        else raw["brokers"]!["broker"]!["source"] = "internal";
        stats.Observe(next with { RawJson = raw.ToJsonString() });
        Assert.Equal("b10_abrupt_guard_failed", Assert.Throws<InvalidDataException>(() => stats.SelectPreAcceptance(DateTimeOffset.MaxValue)).Message);
    }

    [Fact]
    public void Given_ReportAtAcceptance_When_Selecting_Then_NoPreAcceptanceSampleExists()
    {
        var identity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");
        var stats = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        var report = StatisticsReport(identity);
        stats.MarkConstructed(report.ReceivedAt.AddSeconds(-1));
        stats.Observe(report);
        Assert.Null(stats.SelectPreAcceptance(report.ReceivedAt));
        Assert.NotNull(stats.SelectPreAcceptance(report.ReceivedAt.AddTicks(1)));
    }

    [Theory]
    [InlineData(PersistenceStatus.NotPersisted)]
    [InlineData(PersistenceStatus.PossiblyPersisted)]
    public async Task Given_TimeoutInDrain_When_RunningCohort_Then_RetriesOnceWithSeparateInventory(PersistenceStatus status)
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.StartReports();
        var callbacks = new Queue<Action<DeliveryReport<byte[], byte[]>>>();
        fixture.Native.OnProduce = (_, callback) => callbacks.Enqueue(callback);
        int flush = 0;
        fixture.Native.OnFlush = () =>
        {
            while (callbacks.TryDequeue(out var callback)) callback(flush == 0 ? Report(ErrorCode.Local_MsgTimedOut, status) : Report());
            flush++;
        };
        await fixture.Runner.RunTimeoutCohortAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, fixture.Native.Calls.Count);
        Assert.Equal(fixture.Native.Calls[0].Key, fixture.Native.Calls[1].Key);
        Assert.Equal(fixture.Native.Calls[0].Value, fixture.Native.Calls[1].Value);
        var rows = await LedgerValidator.ReadValidatedAsync(Path.Combine(fixture.Root, "timeout", "ledger.jsonl"),
            Path.Combine(fixture.Root, "timeout", "manifest.json"), fixture.Identity, 8, 16384, 1048576, 1048576, TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Length);
        Assert.NotEqual(rows[0].Attempt.AttemptId, rows[1].Attempt.AttemptId);
        Assert.Single(fixture.Seals);
        Assert.Equal("not_implemented_pr3", fixture.Seals[0].GetProperty("fatal_capture_status").GetString());
    }

    [Theory]
    [InlineData(ErrorCode.NoError, PersistenceStatus.Persisted, true)]
    [InlineData(ErrorCode.NoError, PersistenceStatus.NotPersisted, true)]
    [InlineData(ErrorCode.NoError, PersistenceStatus.PossiblyPersisted, false)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.NotPersisted, true)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted, true)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.Persisted, false)]
    [InlineData(ErrorCode.Local_Transport, PersistenceStatus.PossiblyPersisted, false)]
    public async Task Given_TimeoutCohortCallback_When_CheckingGuards_Then_PossiblyPersistedRequiresPermittedTimeout(
        ErrorCode error, PersistenceStatus status, bool permitted)
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.StartReports();
        fixture.Native.OnProduce = (_, callback) => callback(Report(error, status));
        Task cohort = fixture.Runner.RunTimeoutCohortAsync(TestContext.Current.CancellationToken);
        if (permitted)
        {
            await cohort;
            Assert.Single(fixture.Seals);
            Assert.Equal(error == ErrorCode.Local_MsgTimedOut ? 2 : 1, fixture.Native.Calls.Count);
        }
        else
        {
            Assert.Equal("b10_abrupt_guard_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => cohort)).Message);
            Assert.Empty(fixture.Seals);
            Assert.Single(fixture.Native.Calls);
        }
    }

    [Theory]
    [InlineData(ErrorCode.NoError, PersistenceStatus.Persisted, true)]
    [InlineData(ErrorCode.NoError, PersistenceStatus.NotPersisted, true)]
    [InlineData(ErrorCode.NoError, PersistenceStatus.PossiblyPersisted, false)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.NotPersisted, true)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted, true)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.Persisted, false)]
    [InlineData(ErrorCode.Local_Transport, PersistenceStatus.PossiblyPersisted, false)]
    public async Task Given_ProbeCallback_When_AdmittingResend_Then_PossiblyPersistedRequiresPermittedTimeout(
        ErrorCode error, PersistenceStatus status, bool permitted)
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Native.OnProduce = (_, callback) => callback(Report(error, status));
        var original = await fixture.Runner.InvokeProbe("same_instance", [1], null, TestContext.Current.CancellationToken);
        Assert.NotNull(original.Terminal);
        Assert.Equal((int)status, original.Terminal.Value.GetProperty("persistence_status").GetInt32());
        Assert.Equal((int)error, original.Terminal.Value.GetProperty("error_code").GetInt32());
        Task resend = fixture.Runner.InvokeProbe("same_instance", [1], original.AttemptId, TestContext.Current.CancellationToken);
        if (permitted)
        {
            await resend;
            Assert.Equal(2, fixture.Native.Calls.Count);
        }
        else
        {
            Assert.Equal("b10_abrupt_guard_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => resend)).Message);
            Assert.Single(fixture.Native.Calls);
        }
    }

    [Fact]
    public async Task Given_CompletedControlOriginal_When_ReleaseArrives_Then_ResendsOnceAndAdmitsKillWithoutCleanup()
    {
        await using var fixture = new Fixture();
        fixture.StartReports();
        using var finished = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Action<DeliveryReport<byte[], byte[]>>? originalCallback = null;
        fixture.Native.OnProduce = (message, callback) =>
        {
            string key = Encoding.UTF8.GetString(message.Key);
            if (key.EndsWith("/cross_restart", StringComparison.Ordinal)) return;
            if (key.EndsWith("/same_instance", StringComparison.Ordinal) && originalCallback is null) originalCallback = callback;
            else callback(Report());
        };
        fixture.Release = _ =>
        {
            Assert.NotNull(originalCallback);
            originalCallback(Report());
            Assert.Equal(2, fixture.Native.Calls.Count);
            return Task.FromResult(fixture.Clock.FreezeUtc());
        };
        fixture.Send = (kind, data) =>
        {
            if (kind == "accepted" && data.GetProperty("attempt_id").GetString()!.Contains("cross_restart", StringComparison.Ordinal)) finished.Cancel();
            return Task.FromResult(1L);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Runner.RunFirstAsync(Profile(), fixture.Identity, fixture.Options, finished.Token));
        Assert.Equal(1, fixture.Constructions);
        Assert.Equal(4, fixture.Native.Calls.Count);
        Assert.Equal(fixture.Native.Calls[1].Key, fixture.Native.Calls[2].Key);
        Assert.Equal(fixture.Native.Calls[1].Value, fixture.Native.Calls[2].Value);
        Assert.Equal(2, fixture.Seals.Count);
        Assert.Equal(0, fixture.Native.FlushAfterKill);
        Assert.Equal(0, fixture.Native.Disposals);
    }

    [Fact]
    public async Task Given_MissingControlTerminal_When_Draining_Then_KillAdmissionIsBlocked()
    {
        await using var fixture = new Fixture();
        fixture.StartReports();
        fixture.Native.OnProduce = (message, callback) =>
        {
            if (!Encoding.UTF8.GetString(message.Key).EndsWith("same_instance", StringComparison.Ordinal)) callback(Report());
        };
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.RunFirstAsync(Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_control_incomplete", exception.Message);
        Assert.DoesNotContain(fixture.Native.Calls, call => Encoding.UTF8.GetString(call.Key).EndsWith("cross_restart", StringComparison.Ordinal));
        Assert.Single(fixture.Seals);
    }

    [Theory]
    [InlineData(ErrorCode.NoError)]
    [InlineData(ErrorCode.Local_MsgTimedOut)]
    public async Task Given_SealedKillSelection_When_Replaying_Then_InvokesOnceWithoutRetryAndDrains(ErrorCode outcome)
    {
        var release = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        var clock = new ReleaseBoundaryClock(release);
        await using var fixture = new Fixture(clock: clock);
        fixture.StartReports();
        fixture.Native.OnProduce = (_, callback) => callback(Report(outcome, outcome == ErrorCode.NoError ? PersistenceStatus.Persisted : PersistenceStatus.PossiblyPersisted));
        byte[] payload = [1, 2, 3];
        JsonElement selection = JsonSerializer.SerializeToElement(new[] { new
        {
            attempt_id = "predecessor-cross_restart-0", key = "B10/run-1/C1/1/cross_restart", sequence = 0,
            payload_length = payload.Length, payload_sha256 = Convert.ToHexStringLower(SHA256.HashData(payload)),
            payload_base64 = Convert.ToBase64String(payload)
        } });
        var artifacts = await fixture.Runner.RunSuccessorAsync(Profile(), fixture.Identity, fixture.Options,
            selection, release, TestContext.Current.CancellationToken);
        Assert.Single(fixture.Native.Calls);
        Assert.Equal(payload, fixture.Native.Calls[0].Value);
        Assert.Equal(1, fixture.Native.Disposals);
        Assert.Equal(5, artifacts.Paths.Count);
        Assert.All(artifacts.Paths.Values, path => Assert.True(File.Exists(path)));
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(artifacts.Paths["manifest.json"], TestContext.Current.CancellationToken));
        Assert.Equal(2, manifest.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal("b10_abrupt", manifest.RootElement.GetProperty("dialect").GetString());
        Assert.Equal(1, manifest.RootElement.GetProperty("data").GetProperty("inventory_count").GetInt32());
        Assert.Equal("predecessor-cross_restart-0", artifacts.Invocations[0].GetProperty("predecessor_attempt_id").GetString());
        Assert.NotEqual("predecessor-cross_restart-0", artifacts.Invocations[0].GetProperty("attempt_id").GetString());
    }

    [Theory]
    [InlineData(ErrorCode.NoError)]
    [InlineData(ErrorCode.Local_Fail)]
    public async Task Given_DisposalErrorCallback_When_CompletingSuccessor_Then_CapturesBeforeFinalizingAndAppliesGuard(ErrorCode code)
    {
        var release = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        await using var fixture = new Fixture(clock: new ReleaseBoundaryClock(release), captureEvidence: true);
        fixture.StartReports();
        fixture.Native.OnProduce = (_, callback) => callback(Report());
        fixture.Native.OnDispose = () => fixture.Evidence!.CaptureError(new Error(code, "synthetic disposal callback"));
        byte[] payload = [1, 2, 3];
        JsonElement selection = JsonSerializer.SerializeToElement(new[] { new
        {
            attempt_id = "predecessor-cross_restart-0", key = "B10/run-1/C1/1/cross_restart", sequence = 0,
            payload_length = payload.Length, payload_sha256 = Convert.ToHexStringLower(SHA256.HashData(payload)),
            payload_base64 = Convert.ToBase64String(payload)
        } });
        Task<B10AbruptProducer.SuccessorArtifacts> operation = fixture.Runner.RunSuccessorAsync(Profile(), fixture.Identity,
            fixture.Options, selection, release, TestContext.Current.CancellationToken);
        if (code == ErrorCode.NoError)
        {
            var artifacts = await operation;
            string captured = await File.ReadAllTextAsync(artifacts.Paths["request-lifecycle.jsonl"], TestContext.Current.CancellationToken);
            Assert.Contains("synthetic disposal callback", captured, StringComparison.Ordinal);
            Assert.All(artifacts.Paths.Values, path => Assert.True(File.Exists(path)));
        }
        else
        {
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => operation);
            Assert.Equal("b10_abrupt_guard_failed", failure.Message);
            Assert.Contains("native_error_callback_observed", fixture.Evidence!.GuardObservations);
            Assert.False(File.Exists(Path.Combine(fixture.Root, "successor", "capabilities.json")));
            Assert.False(File.Exists(Path.Combine(fixture.Root, "successor", "request-lifecycle.jsonl")));
        }
        Assert.Equal(1, fixture.Native.Disposals);
        Assert.Equal(1, fixture.DisposalsAtEvidenceCompletion);
        Assert.False(fixture.Evidence!.IsFaulted);
        Assert.DoesNotContain("error_capture_unavailable", fixture.Evidence.Diagnostics);
        string retained = await File.ReadAllTextAsync(Path.Combine(fixture.Root, "capture", "request-lifecycle.jsonl"), TestContext.Current.CancellationToken);
        Assert.Contains("synthetic disposal callback", retained, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_ReadinessAroundRelease_When_Replaying_Then_NeverInvokesEarlyOrAfterLateReadiness(int ticksFromRelease)
    {
        var release = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        var clock = new ReleaseBoundaryClock(release.AddTicks(ticksFromRelease));
        await using var fixture = new Fixture(clock: clock);
        fixture.Native.OnProduce = (_, _) => throw new InvalidOperationException("synthetic_invocation_observed");
        byte[] payload = [1];
        JsonElement selected = JsonSerializer.SerializeToElement(new[] { new
        {
            attempt_id = "predecessor-cross_restart-0", key = "B10/run-1/C1/1/cross_restart", sequence = 0,
            payload_length = 1, payload_sha256 = Convert.ToHexStringLower(SHA256.HashData(payload)),
            payload_base64 = Convert.ToBase64String(payload)
        } });
        Task operation = Task.Run(() => fixture.Runner.RunSuccessorAsync(Profile(), fixture.Identity,
            fixture.Options, selected, release, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        if (ticksFromRelease < 0)
        {
            await clock.WaitObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Empty(fixture.Native.Calls);
            Assert.False(operation.IsCompleted);
            clock.Advance(TimeSpan.FromTicks(1));
        }
        if (ticksFromRelease > 0)
        {
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => operation);
            Assert.Equal("b10_abrupt_successor_late", failure.Message);
            Assert.Empty(fixture.Native.Calls);
        }
        else
        {
            var observation = await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
            Assert.Equal("synthetic_invocation_observed", observation.Message);
            Assert.Single(fixture.Native.Calls);
            Assert.Equal(release, clock.GetUtcNow());
        }
    }

    [Fact]
    public async Task Given_MissingPersistenceOnGenuineCallback_When_Running_Then_GuardBlocksControl()
    {
        await using var fixture = new Fixture();
        fixture.Native.OnProduce = (_, callback) => callback(Report(status: null));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.RunFirstAsync(
            Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_guard_failed", error.Message);
        Assert.Empty(fixture.Seals);
        Assert.Single(fixture.Native.Calls);
    }

    [Fact]
    public async Task Given_CallbackAfterAcceptance_When_Recording_Then_PreservesTerminalForFinalReconciliation()
    {
        await using var fixture = new Fixture();
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        Action<DeliveryReport<byte[], byte[]>>? callback = null;
        fixture.Native.OnProduce = (_, captured) => callback = captured;
        var attempt = await fixture.Runner.InvokeProbe("cross_restart", [1], null, TestContext.Current.CancellationToken);
        Assert.Null(attempt.Terminal);
        Assert.NotNull(callback);
        callback(Report());
        Assert.NotNull(attempt.Terminal);
        Assert.Equal(["prepared", "accepted", "terminal"], fixture.Events.Select(item => item.Kind));
    }

    private static void AssertCaptureGapReport(Exception exception, string expectedLine, string privatePath)
    {
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(exception, errors));
        Assert.Equal(expectedLine + Environment.NewLine, errors.ToString());
        Assert.DoesNotContain(privatePath, errors.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(exception.Message, errors.ToString(), StringComparison.Ordinal);
    }

    internal static DeliveryReport<byte[], byte[]> Report(ErrorCode code = ErrorCode.NoError, PersistenceStatus? status = PersistenceStatus.Persisted) => new()
    { Error = new Error(code), Status = status ?? (PersistenceStatus)(-1) };

    internal static KafkaStatisticsReport StatisticsReport(KafkaStatisticsIdentity identity, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        long timestamp = checked((long)((Int128)clock.GetTimestamp() * 1_000_000 / clock.TimestampFrequency));
        return new(identity, "session", clock.GetUtcNow(), JsonSerializer.Serialize(new
        {
            name = "native", client_id = identity.ClientId, type = "producer", ts = timestamp, time = 100, age = timestamp,
            brokers = new Dictionary<string, object>
            {
                ["broker"] = new { name = "broker", nodeid = 0, source = "learned", state = "UP", stateage = 0,
                    req_timeouts = 0, disconnects = 0, txretries = 0, txerrs = 0, rxerrs = 0,
                    throttle = new { cnt = 1, max = 0 }, rtt = new { cnt = 1, p99 = 0 } }
            }
        }), new KafkaStatisticsSnapshot("native", identity.ClientId,
            "producer", timestamp, 100, timestamp, [new KafkaBrokerStatistics("broker", 0, "UP", 0, 0, 0, 0, 0, 0,
                new KafkaWindowStatistics(1, 0), new KafkaWindowStatistics(1, 0))]), []);
    }

    internal static WorkloadProfile Profile() => new(1, "test", 7, [new TenantWeight("tenant", 1)], 1,
        EventNames.ToImmutableDictionary(
            name => name, _ => ImmutableArray.Create(new PayloadBucket(4, 1)), StringComparer.Ordinal),
        [new LoadStage("one", 1, 1)], new SourceLocation("synthetic"));

    private sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "abrupt-producer-" + Guid.NewGuid().ToString("N"));
        internal RunIdentity Identity { get; } = new("B10", "C1", "run-1", "pod-1", "pod-1", "client");
        internal RunExecutionOptions Options { get; }
        internal B10AbruptProducer Runner { get; }
        internal ReleaseWindowClock Clock { get; }
        internal B10AbruptStatistics Statistics { get; }
        internal NativeProducer Native { get; }
        internal RunEvidenceSession? Evidence { get; }
        internal int DisposalsAtEvidenceCompletion { get; private set; }
        internal List<(string Kind, JsonElement Data)> Events { get; } = [];
        internal List<JsonElement> Seals { get; } = [];
        internal Func<string, JsonElement, Task<long>> Send { get; set; } = (_, _) => Task.FromResult(1L);
        internal Func<string, Task<DateTimeOffset>> Release { get; set; }
        internal TaskCompletionSource Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Constructions { get; private set; }
        internal int Releases { get; private set; }
        private readonly CancellationTokenSource reportStop = new();
        private Task? reportTask;
        private readonly KafkaStatisticsIdentity statisticsIdentity = new("B10", "run-1", KafkaClientRole.LoadGenProducer, "client");

        internal Fixture(long sequence = 0, TimeProvider? clock = null, bool captureEvidence = false)
        {
            Clock = new ReleaseWindowClock(clock ?? TimeProvider.System);
            Release = _ => Task.FromResult(Clock.FreezeUtc());
            Directory.CreateDirectory(Root);
            Options = new RunExecutionOptions(8, 256, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1), 16384, 1048576, 1048576, Root, "document-events", ["broker"], false,
                ApplicationRetry: ApplicationRetryPolicy.ForScenario("B10"));
            Statistics = new B10AbruptStatistics(statisticsIdentity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>(), initialSequence: sequence);
            IProducer<byte[], byte[]> native = DispatchProxy.Create<IProducer<byte[], byte[]>, NativeProducer>();
            Native = (NativeProducer)(object)native;
            if (captureEvidence)
                Evidence = new RunEvidenceSession(Identity,
                    new RunEvidenceOptions(Path.Combine(Root, "capture"), 16, 16384, 1048576, 16, TimeSpan.FromSeconds(1)),
                    Clock);
            var wireIdentity = new B10AbruptWire.Identity("run-1", "B10", "C1", "C1", 1, new string('a', 40),
                "image@sha256:" + new string('b', 64), "run-1-loadgen", "pod-1", "uid-1", "pod-1", null);
            Runner = new B10AbruptProducer(() => { Constructions++; return native; }, Clock, Statistics,
                cohort => new LocalLedgerStore(new LedgerStoreOptions(Path.Combine(Root, cohort, "ledger.jsonl"),
                    Path.Combine(Root, cohort, "manifest.json"), 16, 16384, 1048576, 1048576, TimeSpan.FromSeconds(1))),
                async (kind, data, _) =>
                {
                    if (kind == "prepared" && data.GetProperty("predecessor_attempt_id").ValueKind != JsonValueKind.Null)
                        Clock.ResumeUtc();
                    Events.Add((kind, data));
                    if (kind == "accepted") Accepted.TrySetResult();
                    return await Send(kind, data);
                }, data => { Events.Add(("terminal", data)); return true; },
                (arm, _) => { Releases++; return Release(arm); },
                (_, seal, _) => { Seals.Add(seal); return Task.CompletedTask; }, evidence: Evidence, diagnosticIdentity: wireIdentity,
                completeEvidence: Evidence is null ? null : async token =>
                {
                    DisposalsAtEvidenceCompletion = Native.Disposals;
                    await Evidence.CompleteAsync(null, token);
                    return Evidence.ArtifactNames.ToDictionary(name => name, name => Path.Combine(Root, "capture", name), StringComparer.Ordinal);
                });
        }

        internal KafkaStatisticsReport Observe()
        {
            var report = StatisticsReport(statisticsIdentity) with { ReceivedAt = Clock.GetUtcNow() };
            Statistics.Observe(report);
            return report;
        }

        internal void StartReports() => reportTask = Task.Run(async () =>
        {
            try
            {
                while (!reportStop.IsCancellationRequested)
                {
                    Observe();
                    await Task.Delay(2, reportStop.Token);
                }
            }
            catch (OperationCanceledException) when (reportStop.IsCancellationRequested) { }
        }, TestContext.Current.CancellationToken);

        public async ValueTask DisposeAsync()
        {
            await reportStop.CancelAsync();
            if (reportTask is not null) await reportTask;
            reportStop.Dispose();
            if (Evidence is not null) await Evidence.DisposeAsync();
            Directory.Delete(Root, true);
        }
    }

    private sealed class ReleaseWindowClock(TimeProvider inner) : TimeProvider
    {
        private readonly object gate = new();
        private DateTimeOffset? frozenUtc;
        public override long TimestampFrequency => inner.TimestampFrequency;
        public override long GetTimestamp() => inner.GetTimestamp();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            inner.CreateTimer(callback, state, dueTime, period);
        public override DateTimeOffset GetUtcNow()
        {
            lock (gate) return frozenUtc ?? inner.GetUtcNow();
        }
        internal DateTimeOffset FreezeUtc()
        {
            lock (gate)
            {
                frozenUtc = inner.GetUtcNow();
                return frozenUtc.Value;
            }
        }
        internal void ResumeUtc()
        {
            lock (gate) frozenUtc = null;
        }
    }

    private sealed class ReleaseBoundaryClock(DateTimeOffset initialUtc) : TimeProvider
    {
        private long ticks;
        private int timestampReads;
        internal TaskCompletionSource WaitObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() => initialUtc.AddTicks(Interlocked.Read(ref ticks));
        public override long GetTimestamp()
        {
            if (Interlocked.Increment(ref timestampReads) >= 3) WaitObserved.TrySetResult();
            return Interlocked.Read(ref ticks);
        }
        internal void Advance(TimeSpan duration) => Interlocked.Add(ref ticks, duration.Ticks);
    }

    public class NativeProducer : DispatchProxy
    {
        internal List<Message<byte[], byte[]>> Calls { get; } = [];
        internal Action<Message<byte[], byte[]>, Action<DeliveryReport<byte[], byte[]>>> OnProduce { get; set; } = (_, _) => { };
        internal Action OnFlush { get; set; } = () => { };
        internal Action OnDispose { get; set; } = () => { };
        internal int Disposals { get; private set; }
        internal int FlushAfterKill { get; private set; }
        private bool killed;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "Produce":
                    var message = (Message<byte[], byte[]>)args![1]!;
                    Calls.Add(message);
                    killed |= Encoding.UTF8.GetString(message.Key).EndsWith("cross_restart", StringComparison.Ordinal);
                    OnProduce(message, (Action<DeliveryReport<byte[], byte[]>>)args[2]!);
                    return null;
                case "Flush":
                    if (killed) FlushAfterKill++;
                    OnFlush();
                    return 0;
                case "Dispose": Disposals++; OnDispose(); return null;
                default: throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
