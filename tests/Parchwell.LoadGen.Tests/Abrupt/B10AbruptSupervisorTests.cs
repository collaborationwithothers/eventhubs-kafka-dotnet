using System.Globalization;
using System.Reflection;
using System.Threading.Channels;
using Confluent.Kafka;
using System.Text;
using System.Text.Json;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
public sealed class B10AbruptSupervisorTests
{
    [Fact]
    public async Task Given_NoDurableAcceptance_When_Finalizing_Then_ReportsKillTooEarly()
    {
        await using var fixture = new Fixture();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Supervisor.FinalizeSelectionAsync(TestContext.Current.CancellationToken));
        Assert.Equal("kill_too_early", error.Message);
        Assert.Empty(fixture.Journal.Frames);
    }

    [Fact]
    public async Task Given_AcceptedWithoutReport_When_LateReportArrives_Then_OnlySampleMissingPersists()
    {
        await using var fixture = new Fixture();
        await fixture.PrepareAsync();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AcceptAsync());
        Assert.Equal("b10_abrupt_sample_missing", error.Message);
        await fixture.Supervisor.AcceptReportAsync(fixture.Report(), TestContext.Current.CancellationToken);
        Assert.Equal("b10_abrupt_sample_missing", Assert.Single(fixture.Supervisor.ArmFailures).Value);
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind is "signal" or "checkpoint");
    }

    [Fact]
    public async Task Given_DuplicateReport_When_AcceptedArrives_Then_OnlyBindingInvalidPersists()
    {
        await using var fixture = new Fixture();
        await fixture.PrepareAsync();
        await fixture.Supervisor.AcceptReportAsync(fixture.Report(), TestContext.Current.CancellationToken);
        await fixture.Supervisor.AcceptReportAsync(fixture.Report(), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AcceptAsync());
        Assert.Equal("b10_abrupt_report_binding_invalid", error.Message);
        Assert.Equal("b10_abrupt_report_binding_invalid", Assert.Single(fixture.Supervisor.ArmFailures).Value);
    }

    [Theory]
    [InlineData("length_zero")]
    [InlineData("length_negative")]
    [InlineData("length_oversized")]
    [InlineData("length_wrong")]
    [InlineData("hash_changed")]
    [InlineData("file_changed")]
    [InlineData("file_missing")]
    [InlineData("file_unreadable")]
    [InlineData("identity_missing")]
    [InlineData("identity_extra")]
    [InlineData("wrong_scenario")]
    [InlineData("wrong_run")]
    [InlineData("wrong_role")]
    [InlineData("numeric_role")]
    [InlineData("empty_client")]
    [InlineData("sequence_zero")]
    [InlineData("sequence_negative")]
    [InlineData("sequence_fraction")]
    [InlineData("sequence_string")]
    [InlineData("utc_short")]
    [InlineData("utc_offset")]
    [InlineData("utc_bad_date")]
    [InlineData("attempt_unknown")]
    public async Task Given_InvalidReportBinding_When_AcceptedArrives_Then_FirstBindingFailureWins(string fault)
    {
        await using var fixture = new Fixture();
        await fixture.PrepareAsync();
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(fixture.Report().GetRawText())!;
        var identity = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(data["report_identity"].GetRawText())!;
        void Set(string key, object value) => data[key] = B10AbruptWire.Element(value);
        void Id(string key, object value) => identity[key] = B10AbruptWire.Element(value);
        switch (fault)
        {
            case "length_zero": Set("report_length", 0); break;
            case "length_negative": Set("report_length", -1); break;
            case "length_oversized": Set("report_length", 1048577); break;
            case "length_wrong": Set("report_length", 1); break;
            case "hash_changed": Set("report_sha256", new string('f', 64)); break;
            case "file_changed": await File.WriteAllTextAsync(fixture.ReportPath, "changed", TestContext.Current.CancellationToken); break;
            case "file_missing": File.Delete(fixture.ReportPath); break;
            case "file_unreadable": File.Delete(fixture.ReportPath); Directory.CreateDirectory(fixture.ReportPath); break;
            case "identity_missing": identity.Remove("client_id"); break;
            case "identity_extra": Id("extra", true); break;
            case "wrong_scenario": Id("scenario_id", "B11"); break;
            case "wrong_run": Id("run_id", "wrong"); break;
            case "wrong_role": Id("role", "Verifier"); break;
            case "numeric_role": Id("role", 0); break;
            case "empty_client": Id("client_id", ""); break;
            case "sequence_zero": Set("report_sequence", 0); break;
            case "sequence_negative": Set("report_sequence", -1); break;
            case "sequence_fraction": Set("report_sequence", 1.5); break;
            case "sequence_string": Set("report_sequence", "1"); break;
            case "utc_short": Set("report_capture_utc", "2026-01-01T00:00:00Z"); break;
            case "utc_offset": Set("report_capture_utc", "2026-01-01T00:00:00.0000000+01:00"); break;
            case "utc_bad_date": Set("report_capture_utc", "2026-13-01T00:00:00.0000000+00:00"); break;
            case "attempt_unknown": Set("attempt_id", "unknown"); break;
        }
        data["report_identity"] = B10AbruptWire.Element(identity);
        await fixture.Supervisor.AcceptReportAsync(B10AbruptWire.Element(data), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AcceptAsync());
        Assert.Equal("b10_abrupt_report_binding_invalid", Assert.Single(fixture.Supervisor.ArmFailures).Value);
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind is "signal" or "checkpoint");
    }

    [Theory]
    [InlineData(1048575, true)]
    [InlineData(1048576, true)]
    [InlineData(1048577, false)]
    public async Task Given_ReportFileAtByteBoundary_When_Bound_Then_ExactLimitIsEnforced(int length, bool valid)
    {
        await using var fixture = new Fixture(new byte[length]);
        await fixture.PrepareAsync();
        await fixture.Supervisor.AcceptReportAsync(fixture.Report(), TestContext.Current.CancellationToken);
        if (valid) { await fixture.AcceptAsync(); Assert.Empty(fixture.Supervisor.ArmFailures); }
        else Assert.Equal("b10_abrupt_report_binding_invalid", Assert.Single(fixture.Supervisor.ArmFailures).Value);
    }

    [Fact]
    public async Task Given_UnknownArmAlongsideTwoValidArms_When_Bound_Then_RecognizedArmsHaveNoSampleFailure()
    {
        await using var fixture = new Fixture();
        await fixture.PrepareAsync();
        await fixture.PrepareAsync("cross_restart");
        await fixture.Supervisor.AcceptReportAsync(fixture.Report(), TestContext.Current.CancellationToken);
        await fixture.Supervisor.AcceptReportAsync(fixture.Report("cross_restart"), TestContext.Current.CancellationToken);
        await fixture.Supervisor.AcceptReportAsync(fixture.Report("unknown"), TestContext.Current.CancellationToken);
        await fixture.AcceptAsync();
        await fixture.AcceptAsync("cross_restart");
        Assert.Equal("unknown", Assert.Single(fixture.Supervisor.ArmFailures).Key);
        Assert.Equal("b10_abrupt_report_binding_invalid", fixture.Supervisor.ArmFailures["unknown"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_GenuineTerminal_When_CapturedAroundAcceptance_Then_RecordIsRetained(bool beforeAcceptance)
    {
        await using var fixture = new Fixture();
        await fixture.PrepareAsync();
        await fixture.Supervisor.AcceptReportAsync(fixture.Report(), TestContext.Current.CancellationToken);
        if (!beforeAcceptance) await fixture.AcceptAsync();
        await fixture.Supervisor.HandleAsync(new("terminal", 5, B10AbruptWire.Element(new
        {
            attempt_id = "same_instance-original", completion_source = "callback", delivered = true, error_code = 0, persistence_status = 2
        })), TestContext.Current.CancellationToken, true);
        if (beforeAcceptance) await fixture.AcceptAsync();
        Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "terminal");
        Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "accepted");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_CheckpointFlushAtArmDeadline_When_ArmingKill_Then_OnlyOnTimeCheckpointAuthorizesKill(int relativeTicks)
    {
        await using var fixture = new DeadlineFixture();
        await fixture.PrepareAcceptedAsync();
        fixture.Child.OnConfirmStopped = () => fixture.Clock.Advance(TimeSpan.FromSeconds(29));
        fixture.BeforeFlush = () => fixture.Clock.Advance(TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(relativeTicks));
        fixture.Child.ArmCheckpointDelay = () => fixture.DelayNextFlush = true;
        if (relativeTicks > 0)
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Supervisor.CheckpointKillAsync("original", TestContext.Current.CancellationToken));
            Assert.Equal("b10_abrupt_deadline_exhausted", error.Message);
            Assert.Equal(0, fixture.Child.Kills);
            Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "signal" && frame.Data.GetProperty("requested_signal").GetInt32() == 9);
        }
        else
        {
            await fixture.Supervisor.CheckpointKillAsync("original", TestContext.Current.CancellationToken);
            Assert.Equal(1, fixture.Child.Kills);
            Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "checkpoint");
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_QueuedTerminalDrainAtArmDeadline_When_Checkpointing_Then_LateDrainCannotAuthorizeKill(int relativeTicks)
    {
        await using var fixture = new DeadlineFixture();
        await fixture.PrepareAcceptedAsync();
        await fixture.AddPreparedAsync("other");
        var incoming = Channel.CreateUnbounded<B10AbruptWire.Message>();
        incoming.Writer.TryWrite(Terminal("other", 0, 2, true));
        SetPrivate(fixture.Supervisor, "_incoming", incoming);
        fixture.Child.OnConfirmStopped = () => fixture.Clock.Advance(TimeSpan.FromSeconds(29));
        fixture.BeforeFlush = () => fixture.Clock.Advance(TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(relativeTicks));
        fixture.Child.ArmCheckpointDelay = () => fixture.DelayNextFlush = true;
        if (relativeTicks > 0)
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Supervisor.CheckpointKillAsync("original", TestContext.Current.CancellationToken));
            Assert.Equal("b10_abrupt_deadline_exhausted", error.Message);
            Assert.Equal(0, fixture.Child.Kills);
            Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "checkpoint");
        }
        else
        {
            await fixture.Supervisor.CheckpointKillAsync("original", TestContext.Current.CancellationToken);
            Assert.Equal(1, fixture.Child.Kills);
            Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "terminal");
        }
    }

    [Theory]
    [InlineData("kill", -1)] [InlineData("kill", 0)] [InlineData("kill", 1)]
    [InlineData("reap", -1)] [InlineData("reap", 0)] [InlineData("reap", 1)]
    [InlineData("eof", -1)] [InlineData("eof", 0)] [InlineData("eof", 1)]
    public async Task Given_MechanicalCompletionAtTenSecondBoundary_When_Observed_Then_OnlyOnTimeCompletionPasses(string step, int relativeTicks)
    {
        await using var fixture = new DeadlineFixture();
        TimeSpan elapsed = TimeSpan.FromSeconds(10) + TimeSpan.FromTicks(relativeTicks);
        Func<Task> operation;
        if (step == "kill")
        {
            fixture.Child.OnKill = () => fixture.Clock.Advance(elapsed);
            operation = () => fixture.Supervisor.RequestKillAsync(fixture.Child, 1, TestContext.Current.CancellationToken);
        }
        else if (step == "reap")
        {
            fixture.Child.OnObserveExit = () => fixture.Clock.Advance(elapsed);
            operation = async () => { _ = await fixture.Supervisor.ObserveExitAsync(fixture.Child, TestContext.Current.CancellationToken); };
        }
        else
        {
            await fixture.AddPreparedAsync("other");
            var incoming = Channel.CreateUnbounded<B10AbruptWire.Message>();
            incoming.Writer.TryWrite(Terminal("other", 0, 2, true));
            incoming.Writer.Complete();
            fixture.DelayNextFlush = true;
            fixture.BeforeFlush = () => fixture.Clock.Advance(elapsed);
            operation = () => fixture.Supervisor.DrainToEofAsync(incoming.Reader, Task.CompletedTask, TestContext.Current.CancellationToken);
        }
        if (relativeTicks <= 0) await operation();
        else
        {
            Exception error = await Assert.ThrowsAnyAsync<Exception>(operation);
            Assert.Equal(step == "kill" ? "b10_abrupt_deadline_exhausted" : step == "reap" ? "b10_abrupt_exit_unobserved" : "b10_abrupt_eof_incomplete", error.Message);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_KillSignalRecordFlushAtDeadline_When_Observed_Then_LateCompletionRetainsActualSignalAndFails(int relativeTicks)
    {
        await using var fixture = new DeadlineFixture();
        fixture.DelayNextFlush = true;
        fixture.BeforeFlush = () => fixture.Clock.Advance(TimeSpan.FromSeconds(10) + TimeSpan.FromTicks(relativeTicks));
        Task operation = fixture.Supervisor.RequestKillAsync(fixture.Child, 1, TestContext.Current.CancellationToken);
        if (relativeTicks <= 0) await operation;
        else
        {
            var failure = await Assert.ThrowsAsync<OwnedProducerProcess.SignalException>(() => operation);
            Assert.Equal("b10_abrupt_deadline_exhausted", failure.Message);
            Assert.Equal(9, failure.Observation.RequestedSignal);
            Assert.Equal(0, failure.Observation.ReturnValue);
            Assert.Null(failure.Observation.Errno);
        }
        Assert.Equal(1, fixture.Child.Kills);
    }

    [Fact]
    public async Task Given_ReapReportsNoExit_When_SupervisorObserves_Then_ExitRemainsUnobserved()
    {
        await using var fixture = new DeadlineFixture();
        fixture.Child.ExitObserved = false;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Supervisor.ObserveExitAsync(fixture.Child, TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_exit_unobserved", error.Message);
    }

    [Fact]
    public async Task Given_EventsRemainOpen_When_EofBudgetExpires_Then_EofIsIncomplete()
    {
        await using var fixture = new DeadlineFixture();
        var incoming = Channel.CreateUnbounded<B10AbruptWire.Message>();
        Task operation = fixture.Supervisor.DrainToEofAsync(incoming.Reader, Task.CompletedTask, TestContext.Current.CancellationToken);
        Assert.False(operation.IsCompleted);
        fixture.Clock.Advance(TimeSpan.FromSeconds(10) + TimeSpan.FromTicks(1));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_eof_incomplete", error.Message);
        Assert.Empty(fixture.Journal.Frames);
    }

    [Theory]
    [InlineData(0, 1, true, false)]
    [InlineData(0, 2, true, true)]
    [InlineData((int)ErrorCode.Local_MsgTimedOut, 1, false, true)]
    [InlineData((int)ErrorCode.Local_MsgTimedOut, 0, false, true)]
    [InlineData((int)ErrorCode.Local_MsgTimedOut, 1, true, false)]
    public async Task Given_TerminalPersistenceTuple_When_SupervisorCaptures_Then_OnlyPermittedTuplePasses(int errorCode, int status, bool delivered, bool permitted)
    {
        await using var fixture = new Fixture();
        await fixture.PrepareAsync();
        Task operation = fixture.Supervisor.HandleAsync(Terminal("same_instance-original", errorCode, status, delivered), TestContext.Current.CancellationToken, true);
        if (permitted) await operation;
        else Assert.Equal("b10_abrupt_guard_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => operation)).Message);
        Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "terminal");
    }

    private static B10AbruptWire.Message Terminal(string attempt, int code, int status, bool delivered) => new("terminal", 9,
        B10AbruptWire.Element(new { attempt_id = attempt, completion_source = "callback", delivered, error_code = code, persistence_status = status }));

    private static void SetPrivate(object owner, string name, object value) =>
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);

    private sealed class DeadlineFixture : IAsyncDisposable
    {
        private string Root { get; } = Path.Combine(Path.GetTempPath(), "b10-deadline-" + Guid.NewGuid().ToString("N"));
        internal ManualClock Clock { get; } = new();
        internal DeadlineChild Child { get; }
        internal B10AbruptJournal Journal { get; }
        internal B10AbruptSupervisor Supervisor { get; }
        internal Action BeforeFlush { get; set; } = () => { };
        internal bool DelayNextFlush { get; set; }
        internal DeadlineFixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, "abrupt"));
            Directory.CreateDirectory(Path.Combine(Root, "abrupt-child"));
            var options = B10AbruptWireProducerTests.TestOptions(Root);
            Child = new DeadlineChild();
            Journal = new(Path.Combine(Root, "abrupt", "journal.jsonl"), options.Identity(), Clock,
                _ => { if (DelayNextFlush) { DelayNextFlush = false; BeforeFlush(); } return Task.CompletedTask; });
            Supervisor = new(options, Clock, Journal, new(new B10AbruptWireProducerTests.FileStorage(Path.Combine(Root, "publication")), Clock), () => Child);
            SetPrivate(Supervisor, "_child", Child);
        }
        internal async Task AddPreparedAsync(string attempt)
        {
            await Supervisor.HandleAsync(new("prepared", 1, B10AbruptWire.Element(new
            {
                attempt_id = attempt, invocation_ordinal = 0, cohort = "cross_restart", probe_id = "B10/synthetic/C1/1/cross_restart", key = "B10/synthetic/C1/1/cross_restart",
                sequence = 0, payload_length = 1, payload_sha256 = B10AbruptPublisher.Hash([1]), predecessor_attempt_id = (string?)null
            })), TestContext.Current.CancellationToken, true);
        }
        internal async Task PrepareAcceptedAsync()
        {
            await AddPreparedAsync("original");
            File.WriteAllBytes(Path.Combine(Root, "abrupt-child", "cross_restart-pre-acceptance-report.json"), [1]);
            await Supervisor.AcceptReportAsync(B10AbruptWire.Element(new
            {
                arm = "cross_restart", attempt_id = "original", report_length = 1, report_sha256 = B10AbruptPublisher.Hash([1]),
                report_identity = new { scenario_id = "B10", run_id = "synthetic", role = "LoadGenProducer", client_id = "synthetic-first" },
                report_sequence = 1, report_capture_utc = B10AbruptPublisher.Utc(Clock.GetUtcNow())
            }), TestContext.Current.CancellationToken);
            await Supervisor.HandleAsync(new("accepted", 2, B10AbruptWire.Element(new { attempt_id = "original", prepared_sequence = Journal.Frames[0].Sequence })),
                TestContext.Current.CancellationToken, true);
        }
        public async ValueTask DisposeAsync() { await Journal.DisposeAsync(); Directory.Delete(Root, true); }
    }

    private sealed class DeadlineChild : IB10AbruptProcess
    {
        public OwnedProducerProcess.ProcessIdentity Identity { get; } = new(123, 122, "synthetic", 1);
        public Stream Events => Stream.Null;
        public Stream Acknowledgements => Stream.Null;
        public bool HasExited => false;
        internal int Kills { get; private set; }
        internal bool ExitObserved { get; set; } = true;
        internal Action OnConfirmStopped { get; set; } = () => { };
        internal Action ArmCheckpointDelay { get; set; } = () => { };
        internal Action OnKill { get; set; } = () => { };
        internal Action OnObserveExit { get; set; } = () => { };
        public OwnedProducerProcess.SignalObservation Stop() => new(19, 0, null);
        public bool ConfirmStopped(TimeSpan timeout, TimeProvider clock) { OnConfirmStopped(); ArmCheckpointDelay(); return true; }
        public OwnedProducerProcess.SignalObservation Resume() => new(18, 0, null);
        public OwnedProducerProcess.SignalObservation Kill() { Kills++; OnKill(); return new(9, 0, null); }
        public OwnedProducerProcess.ExitObservation ObserveExit() { OnObserveExit(); return new(ExitObserved, ExitObserved ? 137 : null, "synthetic"); }
        public B10AbruptCgroup.Observation CaptureBefore() => new("synthetic", "/group", "1:2", 0);
        public B10AbruptCgroup.Observation CaptureAfter(B10AbruptCgroup.Observation before) => before;
        public void Dispose() { }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long ticks;
        private readonly List<ManualTimer> timers = [];
        private readonly object gate = new();
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            lock (gate) timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }
        internal void Advance(TimeSpan elapsed)
        {
            Interlocked.Add(ref ticks, elapsed.Ticks);
            ManualTimer[] snapshot; lock (gate) snapshot = timers.ToArray();
            foreach (var timer in snapshot) timer.Fire(GetTimestamp());
        }
        private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long due = long.MaxValue;
            public bool Change(TimeSpan dueTime, TimeSpan period) { Interlocked.Exchange(ref due, dueTime < TimeSpan.Zero ? long.MaxValue : checked(clock.GetTimestamp() + dueTime.Ticks)); return true; }
            internal void Fire(long now) { long target = Interlocked.Read(ref due); if (now >= target && Interlocked.CompareExchange(ref due, long.MaxValue, target) == target) callback(state); }
            public void Dispose() => Interlocked.Exchange(ref due, long.MaxValue);
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "b10-supervisor-" + Guid.NewGuid().ToString("N"));
        internal string ReportPath => Path.Combine(Root, "abrupt-child", "same_instance-pre-acceptance-report.json");
        internal B10AbruptSupervisor Supervisor { get; }
        internal B10AbruptJournal Journal { get; }
        private readonly byte[] raw;
        private readonly Dictionary<string, long> preparedSequences = new(StringComparer.Ordinal);
        internal Fixture(byte[]? bytes = null)
        {
            raw = bytes ?? Encoding.UTF8.GetBytes("{\"synthetic\":true}");
            Directory.CreateDirectory(Path.Combine(Root, "abrupt-child"));
            foreach (string arm in new[] { "same_instance", "cross_restart" }) File.WriteAllBytes(Path.Combine(Root, "abrupt-child", arm + "-pre-acceptance-report.json"), raw);
            B10AbruptOptions options = B10AbruptWireProducerTests.TestOptions(Root);
            Journal = new(Path.Combine(Root, "journal.jsonl"), options.Identity(), TimeProvider.System);
            Supervisor = new(options, TimeProvider.System, Journal, new(new B10AbruptWireProducerTests.FileStorage(Path.Combine(Root, "publication")), TimeProvider.System),
                () => throw new InvalidOperationException("Process must not start in message validation test"));
        }
        internal async Task PrepareAsync(string arm = "same_instance")
        {
            await Supervisor.HandleAsync(new("prepared", 1, B10AbruptWire.Element(new
            {
                attempt_id = arm + "-original", invocation_ordinal = 0, cohort = arm, probe_id = arm, key = "B10/synthetic/C1/1/" + arm,
                sequence = 0, payload_length = 1, payload_sha256 = B10AbruptPublisher.Hash([1]), predecessor_attempt_id = (string?)null
            })), TestContext.Current.CancellationToken, true);
            preparedSequences[arm] = Journal.Frames[^1].Sequence;
        }
        internal Task AcceptAsync(string arm = "same_instance") => Supervisor.HandleAsync(new("accepted", 3, B10AbruptWire.Element(new
        { attempt_id = arm + "-original", prepared_sequence = preparedSequences[arm] })), TestContext.Current.CancellationToken, true);
        internal JsonElement Report(string arm = "same_instance") => B10AbruptWire.Element(new
        {
            arm, attempt_id = arm + "-original", report_length = raw.Length, report_sha256 = B10AbruptPublisher.Hash(raw),
            report_identity = new { scenario_id = "B10", run_id = "synthetic", role = "LoadGenProducer", client_id = "synthetic-first" },
            report_sequence = 1, report_capture_utc = TimeProvider.System.GetUtcNow().ToString("O", CultureInfo.InvariantCulture)
        });
        public async ValueTask DisposeAsync() { await Journal.DisposeAsync(); Directory.Delete(Root, true); }
    }
}
