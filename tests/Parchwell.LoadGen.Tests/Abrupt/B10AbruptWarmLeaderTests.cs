using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptWarmLeaderTests
{
    // A successful warm-up and its later UP report must exist before the first original is prepared.
    [Fact]
    public async Task Given_BothArms_When_Warmed_Then_DurableDeliveryAndLeaderReportPrecedeOriginals()
    {
        await using var fixture = new B10AbruptProducerTests.Fixture();
        fixture.ConfigureControl(); fixture.StartReports();
        await Assert.ThrowsAsync<B10AbruptProducerTests.SimulatedDeathException>(() => fixture.Runner.RunFirstAsync(
            B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        Assert.Equal(2, fixture.Native.WarmupCalls.Count);
        foreach (string arm in new[] { "same_instance", "cross_restart" })
        {
            string id = fixture.Identity.InstanceId + "-" + arm + "-warmup";
            var prepared = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "warmup_prepared" && frame.Data.GetProperty("arm").GetString() == arm);
            var terminal = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "warmup_terminal" && frame.Data.GetProperty("attempt_id").GetString() == id);
            var original = fixture.Journal.Frames.First(frame => frame.Kind == "prepared" && frame.Data.GetProperty("arm").GetString() == arm);
            var report = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "pre_acceptance_report" && frame.Data.GetProperty("arm").GetString() == arm);
            var accepted = fixture.Journal.Frames.First(frame => frame.Kind == "accepted" && frame.Data.GetProperty("attempt_id").GetString() == original.Data.GetProperty("attempt_id").GetString());
            Assert.True(prepared.Sequence < terminal.Sequence && terminal.Sequence < original.Sequence && original.Sequence < report.Sequence && report.Sequence < accepted.Sequence);
            Assert.True(terminal.Data.GetProperty("delivered").GetBoolean());
            Assert.Equal(0, terminal.Data.GetProperty("error_code").GetInt32());
            Assert.Equal(id, report.Data.GetProperty("warmup_attempt_id").GetString());
            Assert.Equal(terminal.Data.GetProperty("partition").GetInt32(), report.Data.GetProperty("partition").GetInt32());
            Assert.True(DateTimeOffset.Parse(report.Data.GetProperty("report_capture_utc").GetString()!, CultureInfo.InvariantCulture) > DateTimeOffset.Parse(terminal.ObservedUtc, CultureInfo.InvariantCulture));
            var message = Assert.Single(fixture.Native.WarmupCalls, call => Encoding.UTF8.GetString(call.Headers.GetLastBytes("attempt-id")) == id);
            Assert.Equal(fixture.Identity.InstanceId, Encoding.UTF8.GetString(message.Headers.GetLastBytes("instance-id")));
            Assert.Equal("-1", Encoding.UTF8.GetString(message.Headers.GetLastBytes("sequence")));
            Assert.Equal(prepared.Data.GetProperty("key").GetString(), Encoding.UTF8.GetString(message.Key));
        }
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("timeout_error")]
    [InlineData("ninety_seconds")]
    [InlineData("deadline")]
    [InlineData("late_success")]
    public async Task Given_WarmupFailure_When_FirstArmRuns_Then_NoKillWithoutOriginalOrAdmission(string failure)
    {
        await using var fixture = new B10AbruptProducerTests.Fixture();
        fixture.Native.OnWarmup = (_, callback) =>
        {
            switch (failure)
            {
                case "throw": throw new InvalidOperationException("synthetic_produce_failure");
                case "timeout_error": callback(B10AbruptProducerTests.Report(ErrorCode.Local_MsgTimedOut, PersistenceStatus.NotPersisted)); break;
                case "ninety_seconds": fixture.Advancing.Advance(TimeSpan.FromSeconds(90)); break;
                case "deadline": fixture.Advancing.Advance(TimeSpan.FromSeconds(690)); break;
                case "late_success": fixture.Advancing.Advance(TimeSpan.FromSeconds(91)); callback(B10AbruptProducerTests.Report()); fixture.Journal.DrainAvailable().GetAwaiter().GetResult(); break;
            }
        };
        fixture.StartReports();
        Assert.Equal(3, await fixture.Runner.RunFirstAsync(B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        AssertNoKill(fixture, "b10_abrupt_warmup_failed");
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "prepared");
        Assert.Equal(1, fixture.Native.Disposals);
        if (failure == "deadline") Assert.Equal(TimeSpan.Zero, fixture.Native.FlushBudgets[^1]);
    }

    [Fact]
    public async Task Given_CrossRestartWarmupFailure_When_ControlIsComplete_Then_NoKillWithoutAdmission()
    {
        await using var fixture = new B10AbruptProducerTests.Fixture(); fixture.ConfigureControl();
        fixture.Native.OnWarmup = (message, callback) => callback(B10AbruptProducerTests.Report(
            Encoding.UTF8.GetString(message.Key).EndsWith("cross_restart", StringComparison.Ordinal) ? ErrorCode.Local_MsgTimedOut : ErrorCode.NoError,
            PersistenceStatus.NotPersisted));
        fixture.StartReports();
        Assert.Equal(3, await fixture.Runner.RunFirstAsync(B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        AssertNoKill(fixture, "b10_abrupt_warmup_failed");
        Assert.Contains(fixture.Journal.Frames, frame => frame.Kind == "post_drain_report");
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "prepared" && frame.Data.GetProperty("arm").GetString() == "cross_restart");
        Assert.Equal(0, fixture.Native.FlushAfterKill); Assert.Equal(1, fixture.Native.Disposals);
    }

    [Theory]
    [InlineData(false, "b10_abrupt_leader_not_up")]
    [InlineData(true, "b10_abrupt_sample_missing")]
    public async Task Given_NoLeaderReportAfterWarmup_When_WaitExpires_Then_NoOriginalIsPrepared(bool missing, string reason)
    {
        await using var fixture = new B10AbruptProducerTests.Fixture();
        fixture.TransformReport = report => WithState(report, "INIT");
        fixture.Native.OnWarmup = (_, callback) => { callback(B10AbruptProducerTests.Report()); fixture.SuppressReports = missing; };
        fixture.StartReports();
        Assert.Equal(3, await fixture.Runner.RunFirstAsync(B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        AssertNoKill(fixture, reason);
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "prepared");
    }

    [Fact]
    public async Task Given_InitThenUpReport_When_SelectingLeader_Then_FirstUpReportIsBound()
    {
        await using var fixture = new B10AbruptProducerTests.Fixture(); fixture.ConfigureControl();
        int afterWarmup = 0;
        fixture.Native.OnWarmup = (_, callback) => { afterWarmup = 1; callback(B10AbruptProducerTests.Report()); };
        fixture.TransformReport = report => WithState(report, afterWarmup != 0 && Interlocked.Increment(ref afterWarmup) < 5 ? "INIT" : "UP");
        fixture.StartReports();
        await Assert.ThrowsAsync<B10AbruptProducerTests.SimulatedDeathException>(() => fixture.Runner.RunFirstAsync(
            B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        foreach (var report in fixture.Journal.Frames.Where(frame => frame.Kind == "pre_acceptance_report"))
            Assert.True(B10AbruptStatistics.LeaderUp(await File.ReadAllTextAsync(Path.Combine(fixture.Root, "abrupt", report.Data.GetProperty("name").GetString()!), TestContext.Current.CancellationToken), "document-events", 0));
    }

    [Fact]
    public async Task Given_ControlDrainFailsWith140SecondsLeft_When_CleaningUp_Then_FlushUsesRemainingDeadline()
    {
        await using var fixture = new B10AbruptProducerTests.Fixture(); fixture.ConfigureControl();
        bool drained = false;
        fixture.Native.FlushResult = 1;
        fixture.Native.OnFlush = () =>
        {
            if (drained) return;
            drained = true;
            fixture.AdvanceReportClock = false;
            fixture.SuppressReports = true;
            long start = fixture.Journal.Frames[0].MonotonicTicks;
            fixture.Advancing.Advance(TimeSpan.FromSeconds(550) - fixture.Clock.GetElapsedTime(start));
        };
        fixture.StartReports();
        Assert.Equal(3, await fixture.Runner.RunFirstAsync(B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        AssertNoKill(fixture, "b10_abrupt_control_incomplete");
        Assert.Equal(TimeSpan.FromSeconds(140), fixture.Native.FlushBudgets[^1]);
        Assert.Equal(1, fixture.Native.Disposals);
    }

    [Fact]
    public async Task Given_DeadlineReachedDuringRelease_When_Running_Then_NoKillFlushGetsZeroTime()
    {
        await using var fixture = new B10AbruptProducerTests.Fixture(); fixture.ConfigureControl();
        fixture.ReleaseTarget = _ => DateTimeOffset.Parse(fixture.Journal.Frames[0].ObservedUtc, CultureInfo.InvariantCulture).AddSeconds(690);
        fixture.StartReports();
        Assert.Equal(3, await fixture.Runner.RunFirstAsync(B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        AssertNoKill(fixture, "b10_abrupt_deadline_exhausted");
        Assert.Equal(TimeSpan.Zero, fixture.Native.FlushBudgets[^1]);
        Assert.Equal(1, fixture.Native.Disposals);
    }

    [Fact]
    public async Task Given_FailureAfterCrossRestartAdmission_When_Running_Then_ExitsWithoutFlushOrDispose()
    {
        await using var fixture = new B10AbruptProducerTests.Fixture(); fixture.ConfigureControl();
        var invoke = fixture.Native.OnProduce;
        fixture.Native.OnProduce = (message, callback) =>
        {
            if (Encoding.UTF8.GetString(message.Key).EndsWith("cross_restart", StringComparison.Ordinal)) throw new InvalidOperationException("synthetic_failure_after_admission");
            invoke(message, callback);
        };
        fixture.StartReports();
        Assert.Equal(3, await fixture.Runner.RunFirstAsync(B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        Assert.True(Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "no_kill").Data.GetProperty("cross_restart_admitted").GetBoolean());
        Assert.Equal([3], fixture.Exits); Assert.Equal(0, fixture.Native.FlushAfterKill); Assert.Equal(0, fixture.Native.Disposals);
    }

    [Fact]
    public async Task Given_BlockedJournalWriter_When_WarmupCallbackRuns_Then_CallbackReturnsBeforeDurableWrite()
    {
        await using var fixture = new B10AbruptProducerTests.Fixture(); fixture.Initialize(warm: false);
        Action<DeliveryReport<byte[], byte[]>>? callback = null;
        fixture.Native.OnWarmup = (_, value) => callback = value;
        Task preparation = fixture.Runner.PrepareArmAsync("same_instance", TestContext.Current.CancellationToken);
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        fixture.FlushHook = () => { entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)); return Task.CompletedTask; };
        try
        {
            Assert.True(fixture.Journal.TryCaptureNativeError(0, "NoError", false, false, fixture.Clock.GetTimestamp()));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            await Task.Run(() => callback!(B10AbruptProducerTests.Report()), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally { release.Set(); }
        await fixture.Journal.DrainAvailable(TestContext.Current.CancellationToken);
        fixture.Observe(); fixture.Advancing.Advance(TimeSpan.FromMilliseconds(10));
        await preparation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "warmup_terminal");
    }

    [Theory]
    [InlineData("UP", 1, 1, true)]
    [InlineData("INIT", 1, 1, false)]
    [InlineData("CONNECT", 1, 1, false)]
    [InlineData("DOWN", 1, 1, false)]
    [InlineData("UP", -1, 1, false)]
    [InlineData("UP", 1, 2, false)]
    public void Given_LeaderAndBroker_When_CheckingLeaderUp_Then_RequiresOneMatchingUpBroker(string state, int leader, int node, bool expected)
    {
        string raw = JsonSerializer.Serialize(new { topics = new { topic = new { partitions = new Dictionary<string, object> { ["0"] = new { leader } } } }, brokers = new { broker = new { nodeid = node, state } } });
        Assert.Equal(expected, B10AbruptStatistics.LeaderUp(raw, "topic", 0));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"topics\":{\"topic\":{\"partitions\":{}}},\"brokers\":{}}")]
    [InlineData("{\"topics\":{\"topic\":{\"partitions\":{\"0\":{\"leader\":\"1\"}}}},\"brokers\":{\"one\":{\"nodeid\":1,\"state\":\"UP\"}}}")]
    [InlineData("{\"topics\":{\"topic\":{\"partitions\":{\"0\":{\"leader\":1}}}},\"brokers\":{\"one\":{\"nodeid\":1,\"state\":\"UP\"},\"two\":{\"nodeid\":1,\"state\":\"UP\"}}}")]
    public void Given_MissingOrAmbiguousLeader_When_CheckingLeaderUp_Then_Rejects(string raw) => Assert.False(B10AbruptStatistics.LeaderUp(raw, "topic", 0));

    [Fact]
    public void Given_PreWarmupAndLaterReports_When_SelectingLeader_Then_OnlyFirstUpAfterTerminalQualifies()
    {
        var identity = new KafkaStatisticsIdentity("B10", "b10d-1-1", Parchwell.Kafka.Configuration.KafkaClientRole.LoadGenProducer, "first");
        var clock = new B10AbruptWireProducerTests.HarnessClock(); clock.EnterManual();
        var statistics = new B10AbruptStatistics(identity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
        statistics.MarkConstructed(clock.GetUtcNow().AddTicks(-1));
        statistics.Observe(B10AbruptProducerTests.StatisticsReport(identity, clock));
        DateTimeOffset terminal = clock.GetUtcNow();
        Assert.Null(statistics.SelectLeaderReport(terminal, "document-events", 0, out bool beforeSeen));
        Assert.False(beforeSeen);
        clock.Advance(TimeSpan.FromMicroseconds(1));
        statistics.Observe(WithState(B10AbruptProducerTests.StatisticsReport(identity, clock), "INIT"));
        Assert.Null(statistics.SelectLeaderReport(terminal, "document-events", 0, out bool initSeen)); Assert.True(initSeen);
        clock.Advance(TimeSpan.FromMicroseconds(1));
        var up = B10AbruptProducerTests.StatisticsReport(identity, clock); statistics.Observe(up);
        clock.Advance(TimeSpan.FromMicroseconds(1)); statistics.Observe(B10AbruptProducerTests.StatisticsReport(identity, clock));
        Assert.Equal(up, statistics.SelectLeaderReport(terminal, "document-events", 0, out bool upSeen)!.Report); Assert.True(upSeen);
    }

    private static KafkaStatisticsReport WithState(KafkaStatisticsReport report, string state)
    {
        var raw = JsonNode.Parse(report.RawJson)!; raw["brokers"]!["broker"]!["state"] = state;
        return report with { RawJson = raw.ToJsonString(), Snapshot = report.Snapshot! with { Brokers = [report.Snapshot.Brokers[0] with { State = state }] } };
    }
    private static void AssertNoKill(B10AbruptProducerTests.Fixture fixture, string reason)
    {
        var noKill = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "no_kill");
        Assert.Equal(reason, noKill.Data.GetProperty("reason").GetString());
        Assert.False(noKill.Data.GetProperty("cross_restart_admitted").GetBoolean());
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "kill_intent");
    }
}
#pragma warning restore CA1707
