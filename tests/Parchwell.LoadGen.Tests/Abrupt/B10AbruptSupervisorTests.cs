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
[Collection("B10AbruptEnvironment")]
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

    [Fact]
    public async Task Given_FullIncomingQueue_When_IncrementalReaderCaptures_Then_RetainsOnlyQueueFullGap()
    {
        await using var fixture = new DeadlineFixture();
        var incoming = Channel.CreateBounded<B10AbruptWire.Message>(256);
        B10AbruptWire.Message message = Terminal("original", 0, 2, true);
        for (int index = 0; index < 256; index++) Assert.True(incoming.Writer.TryWrite(message));
        SetPrivate(fixture.Supervisor, "_incoming", incoming);
        var capture = typeof(B10AbruptSupervisor).GetMethod("CaptureIncoming", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .CreateDelegate<Action<B10AbruptWire.Message>>(fixture.Supervisor);
        using var input = new MemoryStream();
        await B10AbruptWire.WriteMessageAsync(input, message with { Sequence = 2 }, TestContext.Current.CancellationToken);
        input.Position = 0;
        using var reader = new B10AbruptWire.AvailableMessageReader(input, () => input.Position < input.Length, capture);
        var drain = typeof(B10AbruptSupervisor).GetMethod("DrainAvailableAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Task<bool> operation = (Task<bool>)drain.Invoke(fixture.Supervisor, [reader, TestContext.Current.CancellationToken, false])!;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => operation);
        Assert.Equal("b10_abrupt_capture_gap:queue_full", error.Message);
        Assert.Equal("queue_full", Assert.Single(fixture.Journal.Gaps));
        Assert.Equal(256, incoming.Reader.Count);
    }

    [Theory]
    [InlineData("same_instance", false)]
    [InlineData("same_instance", true)]
    [InlineData("cross_restart", false)]
    [InlineData("cross_restart", true)]
    public async Task Given_TerminalAvailableBeforeStop_When_ReaderHasNotEnqueued_Then_CheckpointWaitsAndExcludesOriginal(string arm, bool inFlight)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, TestContext.Current.CancellationToken);
        CancellationToken token = cancellation.Token;
        await using var fixture = new DeadlineFixture();
        await fixture.PrepareAcceptedAsync(arm);
        using var input = new MemoryStream();
        await B10AbruptWire.WriteMessageAsync(input, Terminal("original", 0, 2, true), token);
        input.Position = 0;
        var incoming = Channel.CreateUnbounded<B10AbruptWire.Message>();
        var captureEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCapture = new ManualResetEventSlim(!inFlight);
        using var reader = new B10AbruptWire.AvailableMessageReader(input, () => input.Position < input.Length, message =>
        {
            captureEntered.TrySetResult();
            Assert.True(releaseCapture.Wait(TimeSpan.FromSeconds(10), token));
            Assert.True(incoming.Writer.TryWrite(message));
        }, 9);
        SetPrivate(fixture.Supervisor, "_incoming", incoming);
        SetPrivate(fixture.Supervisor, "_eventReader", reader);
        fixture.Child.OnConfirmStopped = () => stopped.TrySetResult();
        Task<bool>? pump = inFlight ? Task.Run(() => reader.DrainAvailableAsync(token), token) : null;
        try
        {
            if (pump is not null) await captureEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            Task checkpoint = arm == "same_instance" ? fixture.Supervisor.CheckpointControlAsync("original", token) :
                fixture.Supervisor.CheckpointKillAsync("original", token);
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            if (inFlight)
            {
                Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "checkpoint");
                Assert.Equal(0, fixture.Child.Kills);
                releaseCapture.Set();
            }
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => checkpoint.WaitAsync(TimeSpan.FromSeconds(10), token));
            Assert.Equal(arm == "same_instance" ? "b10_abrupt_control_incomplete" : "kill_too_late:no_unconfirmed_attempt", error.Message);
            var terminal = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "terminal");
            var sealedCheckpoint = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "checkpoint");
            Assert.True(terminal.Sequence < sealedCheckpoint.Sequence);
            Assert.Empty(sealedCheckpoint.Data.GetProperty("selected_attempt_ids").EnumerateArray());
            Assert.Equal(0, fixture.Child.Kills);
            Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "signal" && frame.Data.GetProperty("requested_signal").GetInt32() == 18);
        }
        finally
        {
            releaseCapture.Set();
            if (pump is not null) await pump.WaitAsync(TimeSpan.FromSeconds(10), token);
        }
    }

    [Theory]
    [InlineData("same_instance", 1)]
    [InlineData("same_instance", 4)]
    [InlineData("same_instance", 5)]
    [InlineData("same_instance", -1)]
    [InlineData("cross_restart", 1)]
    [InlineData("cross_restart", 4)]
    [InlineData("cross_restart", 5)]
    [InlineData("cross_restart", -1)]
    public async Task Given_PartialFrameBufferedAtStop_When_Checkpointing_Then_RejectsWithoutSealingOrResuming(string arm, int length)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, TestContext.Current.CancellationToken);
        CancellationToken token = cancellation.Token;
        await using var fixture = new DeadlineFixture();
        await fixture.PrepareAcceptedAsync(arm);
        using var input = new MemoryStream();
        await B10AbruptWire.WriteMessageAsync(input, Terminal("original", 0, 2, true), token);
        input.SetLength(length == -1 ? input.Length - 1 : length);
        input.Position = 0;
        var incoming = Channel.CreateUnbounded<B10AbruptWire.Message>();
        using var reader = new B10AbruptWire.AvailableMessageReader(input, () => input.Position < input.Length,
            message => Assert.True(incoming.Writer.TryWrite(message)), 9);
        Assert.False(await reader.DrainAvailableAsync(token));
        Assert.False(incoming.Reader.TryPeek(out _));
        SetPrivate(fixture.Supervisor, "_incoming", incoming);
        SetPrivate(fixture.Supervisor, "_eventReader", reader);
        Task checkpoint = arm == "same_instance" ? fixture.Supervisor.CheckpointControlAsync("original", token) :
            fixture.Supervisor.CheckpointKillAsync("original", token);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => checkpoint.WaitAsync(TimeSpan.FromSeconds(10), token));
        Assert.Equal("b10_abrupt_capture_gap:malformed_frame", error.Message);
        Assert.Contains("malformed_frame", fixture.Journal.Gaps);
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "checkpoint");
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "signal" && frame.Data.GetProperty("requested_signal").GetInt32() is 18 or 9);
        Assert.Equal(0, fixture.Child.Kills);
        Assert.Equal("b10_abrupt_capture_gap:malformed_frame", (await Assert.ThrowsAsync<InvalidDataException>(() => reader.DrainAvailableAsync(token))).Message);
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

    [Theory]
    [InlineData(null, "healthy")]
    [InlineData(null, "before")]
    [InlineData(null, "cleanup")]
    [InlineData("DllNotFoundException", "healthy")]
    [InlineData("DllNotFoundException", "before")]
    [InlineData("DllNotFoundException", "cleanup")]
    [InlineData("EntryPointNotFoundException", "healthy")]
    [InlineData("EntryPointNotFoundException", "before")]
    [InlineData("EntryPointNotFoundException", "cleanup")]
    [InlineData("BadImageFormatException", "healthy")]
    [InlineData("BadImageFormatException", "before")]
    [InlineData("BadImageFormatException", "cleanup")]
    public async Task Given_CleanupSignalFailure_When_SupervisorAborts_Then_OriginalCauseAndActualCleanupEvidenceSurvive(string? nativeException, string writerState)
    {
        await using var fixture = new DeadlineFixture();
        await fixture.AddPreparedAsync("original");
        using var events = await CleanupFailureEventsAsync();
        fixture.Child.EventInput = events;
        fixture.BeforeFlush = () => throw new IOException("synthetic_writer_failure");
        fixture.DelayNextFlush = writerState == "before";
        var observation = new OwnedProducerProcess.SignalObservation(9, nativeException is null ? -1 : null,
            nativeException is null ? 13 : null, nativeException);
        fixture.Child.OnKill = () =>
        {
            if (writerState == "cleanup") fixture.DelayNextFlush = true;
            throw new OwnedProducerProcess.SignalException("b10_abrupt_signal_failed", observation);
        };
        var original = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Supervisor.RunAsync(TestContext.Current.CancellationToken));
        string originalName = writerState == "before" ? "b10_abrupt_capture_gap" : "b10_abrupt_guard_failed";
        Assert.Equal(originalName, B10AbruptSupervisor.FailureName(original));
        using var diagnostic = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(fixture.Root, "abrupt", "failure.json"), TestContext.Current.CancellationToken));
        JsonElement data = diagnostic.RootElement.GetProperty("data");
        Assert.Equal("incomplete", data.GetProperty("mechanical_status").GetString());
        Assert.Equal(originalName, data.GetProperty("failure").GetString());
        Assert.Equal(typeof(InvalidDataException).FullName, data.GetProperty("exception_type").GetString());
        Assert.Equal(fixture.Child.Identity, data.GetProperty("process").Deserialize<OwnedProducerProcess.ProcessIdentity>(B10AbruptWire.JsonOptions));
        Assert.False(data.GetProperty("has_exited").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("startup_cleanup").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("signal_observation").ValueKind);
        JsonElement cleanup = data.GetProperty("cleanup");
        Assert.Equal("cleanup", cleanup.GetProperty("arm").GetString());
        Assert.Equal("b10_abrupt_signal_failed", cleanup.GetProperty("failure").GetString());
        Assert.Equal(observation, cleanup.GetProperty("signal_observation").Deserialize<OwnedProducerProcess.SignalObservation>(B10AbruptWire.JsonOptions));
        Assert.Equal(writerState == "healthy" ? null : "b10_abrupt_capture_gap", cleanup.GetProperty("capture_failure").GetString());
        if (writerState == "healthy")
        {
            Assert.Empty(fixture.Journal.Gaps);
            var frame = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "signal");
            Assert.Equal("cleanup", frame.Data.GetProperty("arm").GetString());
            Assert.Equal(observation.ReturnValue, frame.Data.GetProperty("return_value").Deserialize<int?>());
            Assert.Equal(observation.Errno, frame.Data.GetProperty("errno").Deserialize<int?>());
        }
        else
        {
            Assert.Equal("writer_failed", Assert.Single(fixture.Journal.Gaps));
            Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "signal");
        }
        await fixture.Supervisor.AbortAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, fixture.Child.Kills);
        Assert.Equal(0, fixture.Child.ExitObservations);
        Assert.Equal(1, fixture.Child.Disposals);
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "termination.json", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_CleanupReapFailure_When_SupervisorAborts_Then_ReapIsNotMislabelledAsWriterFailure(bool throws)
    {
        await using var fixture = new DeadlineFixture();
        await fixture.AddPreparedAsync("original");
        using var events = await CleanupFailureEventsAsync();
        fixture.Child.EventInput = events;
        fixture.Child.ExitObserved = false;
        if (throws) fixture.Child.OnObserveExit = () => throw new InvalidDataException("b10_abrupt_exit_unobserved");
        var original = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Supervisor.RunAsync(TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_guard_failed", original.Message);
        using var diagnostic = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(fixture.Root, "abrupt", "failure.json"), TestContext.Current.CancellationToken));
        JsonElement data = diagnostic.RootElement.GetProperty("data");
        Assert.Equal("b10_abrupt_guard_failed", data.GetProperty("failure").GetString());
        Assert.Equal(fixture.Child.Identity, data.GetProperty("process").Deserialize<OwnedProducerProcess.ProcessIdentity>(B10AbruptWire.JsonOptions));
        Assert.False(data.GetProperty("has_exited").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("startup_cleanup").ValueKind);
        JsonElement cleanup = data.GetProperty("cleanup");
        Assert.Equal("cleanup", cleanup.GetProperty("arm").GetString());
        Assert.Equal("b10_abrupt_exit_unobserved", cleanup.GetProperty("failure").GetString());
        Assert.Equal(0, cleanup.GetProperty("signal_observation").GetProperty("return_value").GetInt32());
        Assert.Equal(JsonValueKind.Null, cleanup.GetProperty("capture_failure").ValueKind);
        Assert.Empty(fixture.Journal.Gaps);
        Assert.Equal(1, fixture.Child.Kills);
        Assert.Equal(1, fixture.Child.ExitObservations);
        Assert.Equal(1, fixture.Child.Disposals);
    }

    [Fact]
    public async Task Given_ExitedChildAtCleanEof_When_SupervisorFails_Then_SkipsGraceAndCleanup()
    {
        await using var fixture = new DeadlineFixture();
        fixture.Child.Exited = true;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Supervisor.RunAsync(TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_unexpected_exit", error.Message);
        Assert.Equal(0, fixture.Clock.GetTimestamp());
        Assert.Equal(0, fixture.Clock.FailureReportPolls);
        Assert.Equal(0, fixture.Child.Kills);
        Assert.Equal(0, fixture.Child.ExitObservations);
        await AssertFailedRunAsync(fixture, "b10_abrupt_unexpected_exit");
    }

    [Fact]
    public async Task Given_ChildExitsBeforeGraceBoundary_When_CleanEofWaits_Then_RetainsFailureWithoutCleanup()
    {
        await using var fixture = new DeadlineFixture();
        Task run = fixture.Supervisor.RunAsync(TestContext.Current.CancellationToken);
        await fixture.Clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(10));
        fixture.Clock.Advance(TimeSpan.FromSeconds(10) - TimeSpan.FromMilliseconds(10) - TimeSpan.FromTicks(1));
        await fixture.Clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(10));
        Assert.False(run.IsCompleted);
        Assert.Equal(0, fixture.Child.Kills);
        fixture.Child.Exited = true;
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(10));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_unexpected_exit", error.Message);
        Assert.Equal(TimeSpan.FromSeconds(10).Ticks - 1, fixture.Clock.GetTimestamp());
        Assert.Equal(0, fixture.Child.Kills);
        Assert.Equal(0, fixture.Child.ExitObservations);
        await AssertFailedRunAsync(fixture, "b10_abrupt_unexpected_exit");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_LiveChildAtGraceBoundary_When_CleanEofExpires_Then_CleanupRechecksExitAndAttemptsKillOnce(bool exitsBeforeAbort)
    {
        await using var fixture = new DeadlineFixture();
        Assert.Equal(TimeSpan.FromSeconds(10), B10AbruptSupervisor.ChildFailureReportGrace);
        Assert.Equal(TimeSpan.FromMilliseconds(10), B10AbruptSupervisor.ChildFailureReportPoll);
        int boundaryObservations = 0;
        fixture.Child.ReadHasExited = () => exitsBeforeAbort && fixture.Clock.GetTimestamp() >= TimeSpan.FromSeconds(10).Ticks &&
            Interlocked.Increment(ref boundaryObservations) > 1;
        Task run = fixture.Supervisor.RunAsync(TestContext.Current.CancellationToken);
        await fixture.Clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(10));
        fixture.Clock.Advance(TimeSpan.FromSeconds(10) - TimeSpan.FromTicks(1));
        await fixture.Clock.WaitForTimerAsync(TimeSpan.FromTicks(1));
        Assert.False(run.IsCompleted);
        Assert.Equal(0, fixture.Child.Kills);
        Assert.Equal(0, fixture.Child.ExitObservations);
        fixture.Clock.Advance(TimeSpan.FromTicks(1));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_unexpected_exit", error.Message);
        Assert.Equal(exitsBeforeAbort ? 0 : 1, fixture.Child.Kills);
        Assert.Equal(exitsBeforeAbort ? 0 : 1, fixture.Child.ExitObservations);
        fixture.Clock.Advance(TimeSpan.FromTicks(1));
        await fixture.Supervisor.AbortAsync(TestContext.Current.CancellationToken);
        Assert.Equal(exitsBeforeAbort ? 0 : 1, fixture.Child.Kills);
        Assert.Equal(exitsBeforeAbort ? 0 : 1, fixture.Child.ExitObservations);
        await AssertFailedRunAsync(fixture, "b10_abrupt_unexpected_exit");
    }

    [Fact]
    public async Task Given_EnclosingCancellationDuringGrace_When_CleanEofWaits_Then_DeadlineFailureStillCleansUp()
    {
        await using var fixture = new DeadlineFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task run = fixture.Supervisor.RunAsync(cancellation.Token);
        await fixture.Clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(10));
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal(0, fixture.Clock.GetTimestamp());
        Assert.Equal(1, fixture.Child.Kills);
        Assert.Equal(1, fixture.Child.ExitObservations);
        await AssertFailedRunAsync(fixture, "b10_abrupt_deadline_exhausted");
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("guard")]
    public async Task Given_FaultBeforePlannedKill_When_SupervisorFails_Then_CleansUpWithoutFailureReportGrace(string fault)
    {
        await using var fixture = new DeadlineFixture();
        using var events = fault == "partial" ? new MemoryStream([0]) : await CleanupFailureEventsAsync();
        fixture.Child.EventInput = events;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Supervisor.RunAsync(TestContext.Current.CancellationToken));
        string name = fault == "partial" ? "b10_abrupt_capture_gap" : "b10_abrupt_guard_failed";
        Assert.Equal(name, B10AbruptSupervisor.FailureName(error));
        if (fault == "partial") Assert.Equal("b10_abrupt_capture_gap:malformed_frame", error.Message);
        Assert.Equal(0, fixture.Clock.GetTimestamp());
        Assert.Equal(0, fixture.Clock.FailureReportPolls);
        Assert.Equal(1, fixture.Child.Kills);
        Assert.Equal(1, fixture.Child.ExitObservations);
        await AssertFailedRunAsync(fixture, name);
    }

    [Fact]
    public async Task Given_PlannedKillBeforeCleanEof_When_SupervisorContinues_Then_UsesPostKillPathWithoutFailureReportGrace()
    {
        await using var fixture = new DeadlineFixture();
        SetPrivate(fixture.Supervisor, "_killRequested", true);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Supervisor.RunAsync(TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_cgroup_unavailable", error.Message);
        Assert.Equal(0, fixture.Clock.GetTimestamp());
        Assert.Equal(0, fixture.Clock.FailureReportPolls);
        Assert.Equal(0, fixture.Child.Kills);
        Assert.Equal(1, fixture.Child.ExitObservations);
        Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "exit");
        await AssertFailedRunAsync(fixture, "b10_abrupt_cgroup_unavailable");
    }

    [Theory]
    [InlineData("start", false)] [InlineData("start", true)]
    [InlineData("preflight", false)] [InlineData("preflight", true)]
    [InlineData("cleanup_signal", false)] [InlineData("cleanup_signal", true)]
    [InlineData("cleanup_native", false)] [InlineData("cleanup_native", true)]
    [InlineData("cleanup_io", false)] [InlineData("cleanup_io", true)]
    [InlineData("cleanup_none", false)] [InlineData("cleanup_none", true)]
    public async Task Given_StartupThrows_When_SupervisorReports_Then_PreservesOriginalAndDoesNotRepeatOwnedCleanup(string fault, bool diagnosticCollision)
    {
        int startupCleanups = 0;
        var observation = new OwnedProducerProcess.SignalObservation(fault == "preflight" ? 0 : 9,
            fault == "cleanup_native" ? null : -1, fault == "cleanup_native" ? null : 13,
            fault == "cleanup_native" ? nameof(EntryPointNotFoundException) : null);
        Exception? cleanupFailure = fault switch
        {
            "cleanup_signal" or "cleanup_native" => new OwnedProducerProcess.SignalException("b10_abrupt_signal_failed:private-cleanup-message", observation),
            "cleanup_io" => new IOException("private-cleanup-message /private/startup-path token=private-token"),
            _ => null
        };
        Exception failure = fault switch
        {
            "start" => new InvalidOperationException("b10_abrupt_child_start_failed:private-startup-message"),
            "preflight" => new OwnedProducerProcess.SignalException("b10_abrupt_signal_preflight_failed:private-startup-message", observation),
            _ => OwnedProducerProcess.CleanupStartupFailure(new InvalidOperationException("private-startup-message"), () =>
            {
                startupCleanups++;
                if (cleanupFailure is not null) throw cleanupFailure;
            })
        };
        await using var fixture = new DeadlineFixture(() => throw failure);
        string path = Path.Combine(fixture.Root, "abrupt", "failure.json");
        if (diagnosticCollision) await File.WriteAllTextAsync(path, "existing-diagnostic", TestContext.Current.CancellationToken);
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        TextWriter original = Console.Error;
        Console.SetError(errors);
        try
        {
            Exception actual = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Supervisor.RunAsync(TestContext.Current.CancellationToken));
            Assert.Same(failure, actual);
        }
        finally { Console.SetError(original); }
        string stderr = errors.ToString();
        using var document = JsonDocument.Parse(stderr);
        JsonElement envelope = document.RootElement;
        Assert.Equal("failed_diagnostic", envelope.GetProperty("kind").GetString());
        JsonElement data = envelope.GetProperty("data");
        B10AbruptWire.ExactMembers(data, "mechanical_status", "failure", "exception_type", "process", "kill_requested", "has_exited",
            "capture_gaps", "arm_failures", "fatal_capture_status", "signal_observation", "cleanup", "startup_cleanup");
        Assert.Equal("incomplete", data.GetProperty("mechanical_status").GetString());
        string expectedFailure = fault == "start" ? "b10_abrupt_child_start_failed" :
            fault == "preflight" ? "b10_abrupt_signal_preflight_failed" : "b10_abrupt_child_identity_mismatch";
        Assert.Equal(expectedFailure, data.GetProperty("failure").GetString());
        Assert.Equal(failure.GetType().FullName, data.GetProperty("exception_type").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("process").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("has_exited").ValueKind);
        Assert.False(data.GetProperty("kill_requested").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("cleanup").ValueKind);
        if (failure is OwnedProducerProcess.SignalException)
            Assert.Equal(observation, data.GetProperty("signal_observation").Deserialize<OwnedProducerProcess.SignalObservation>(B10AbruptWire.JsonOptions));
        else Assert.Equal(JsonValueKind.Null, data.GetProperty("signal_observation").ValueKind);
        JsonElement startup = data.GetProperty("startup_cleanup");
        if (failure is OwnedProducerProcess.StartupException)
        {
            B10AbruptWire.ExactMembers(startup, "failure", "exception_type", "signal_observation");
            Assert.Equal(cleanupFailure is null ? null : fault == "cleanup_io" ? "b10_abrupt_capture_gap" : "b10_abrupt_signal_failed",
                startup.GetProperty("failure").GetString());
            Assert.Equal(cleanupFailure?.GetType().FullName, startup.GetProperty("exception_type").GetString());
            Assert.Equal((cleanupFailure as OwnedProducerProcess.SignalException)?.Observation,
                startup.GetProperty("signal_observation").Deserialize<OwnedProducerProcess.SignalObservation>(B10AbruptWire.JsonOptions));
        }
        else Assert.Equal(JsonValueKind.Null, startup.ValueKind);
        foreach (string secret in new[] { "private-startup-message", "private-cleanup-message", "/private/startup-path", "private-token" })
            Assert.DoesNotContain(secret, stderr, StringComparison.Ordinal);
        if (diagnosticCollision)
        {
            Assert.Equal("existing-diagnostic", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            Assert.Equal("writer_failed", Assert.Single(fixture.Journal.Gaps));
        }
        else
        {
            Assert.Equal(stderr.TrimEnd('\r', '\n'), await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            Assert.Empty(fixture.Journal.Gaps);
        }
        await fixture.Supervisor.AbortAsync(TestContext.Current.CancellationToken);
        Assert.Equal(failure is OwnedProducerProcess.StartupException ? 1 : 0, startupCleanups);
        Assert.Equal(1, fixture.Starts);
        Assert.Equal(0, fixture.Child.Kills);
        Assert.Equal(0, fixture.Child.ExitObservations);
        Assert.Equal(0, fixture.Child.Disposals);
        Assert.Empty(fixture.Journal.Frames);
        AssertNoSuccessfulArtifacts(fixture);
    }

    private static async Task AssertFailedRunAsync(DeadlineFixture fixture, string failure)
    {
        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(fixture.Root, "abrupt", "failure.json"), TestContext.Current.CancellationToken));
        Assert.Equal("failed_diagnostic", document.RootElement.GetProperty("kind").GetString());
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.Equal("incomplete", data.GetProperty("mechanical_status").GetString());
        Assert.Equal(failure, data.GetProperty("failure").GetString());
        Assert.Equal(fixture.Child.Identity, data.GetProperty("process").Deserialize<OwnedProducerProcess.ProcessIdentity>(B10AbruptWire.JsonOptions));
        Assert.Equal(fixture.Child.HasExited, data.GetProperty("has_exited").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("startup_cleanup").ValueKind);
        Assert.Equal(1, fixture.Starts);
        Assert.Equal(1, fixture.Child.Disposals);
        Assert.Equal(0, fixture.Child.Stops);
        Assert.Equal(0, fixture.Child.Resumes);
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind is "checkpoint" or "accepted" or "prepared");
        AssertNoSuccessfulArtifacts(fixture);
    }

    private static void AssertNoSuccessfulArtifacts(DeadlineFixture fixture)
    {
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "signal" && frame.Data.GetProperty("arm").GetString() != "cleanup");
        foreach (string name in new[] { "kill-selection.json", "termination.json", "first-completion.json", "successor-receipt.json", "completion.json" })
            Assert.Empty(Directory.EnumerateFiles(fixture.Root, name, SearchOption.AllDirectories));
        Assert.DoesNotContain(Directory.EnumerateFiles(Path.Combine(fixture.Root, "publication")), path => !Path.GetFileName(path).Equals("registration.json", StringComparison.Ordinal));
    }

    private static async Task<MemoryStream> CleanupFailureEventsAsync()
    {
        var events = new MemoryStream();
        await B10AbruptWire.WriteMessageAsync(events, new("synchronous_rejection", 2, B10AbruptWire.Element(new
        {
            attempt_id = "original", prepared_sequence = 1, error_code = -184, error_reason = "synthetic_rejection"
        })), TestContext.Current.CancellationToken);
        events.Position = 0;
        return events;
    }

    private sealed class DeadlineFixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "b10-deadline-" + Guid.NewGuid().ToString("N"));
        internal ManualClock Clock { get; } = new();
        internal DeadlineChild Child { get; }
        internal B10AbruptJournal Journal { get; }
        internal B10AbruptSupervisor Supervisor { get; }
        internal Action BeforeFlush { get; set; } = () => { };
        internal bool DelayNextFlush { get; set; }
        internal int Starts { get; private set; }
        internal DeadlineFixture(Func<IB10AbruptProcess>? start = null)
        {
            Directory.CreateDirectory(Path.Combine(Root, "abrupt"));
            Directory.CreateDirectory(Path.Combine(Root, "abrupt-child"));
            var options = B10AbruptWireProducerTests.TestOptions(Root);
            Child = new DeadlineChild();
            Journal = new(Path.Combine(Root, "abrupt", "journal.jsonl"), options.Identity(), Clock,
                _ => { if (DelayNextFlush) { DelayNextFlush = false; BeforeFlush(); } return Task.CompletedTask; });
            Supervisor = new(options, Clock, Journal, new(new B10AbruptWireProducerTests.FileStorage(Path.Combine(Root, "publication")), Clock),
                () => { Starts++; return start is null ? Child : start(); });
            if (start is null) SetPrivate(Supervisor, "_child", Child);
        }
        internal async Task AddPreparedAsync(string attempt, string arm = "cross_restart")
        {
            await Supervisor.HandleAsync(new("prepared", 1, B10AbruptWire.Element(new
            {
                attempt_id = attempt, invocation_ordinal = 0, cohort = arm, probe_id = "B10/synthetic/C1/1/" + arm, key = "B10/synthetic/C1/1/" + arm,
                sequence = 0, payload_length = 1, payload_sha256 = B10AbruptPublisher.Hash([1]), predecessor_attempt_id = (string?)null
            })), TestContext.Current.CancellationToken, true);
        }
        internal async Task PrepareAcceptedAsync(string arm = "cross_restart")
        {
            await AddPreparedAsync("original", arm);
            File.WriteAllBytes(Path.Combine(Root, "abrupt-child", arm + "-pre-acceptance-report.json"), [1]);
            await Supervisor.AcceptReportAsync(B10AbruptWire.Element(new
            {
                arm, attempt_id = "original", report_length = 1, report_sha256 = B10AbruptPublisher.Hash([1]),
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
        internal Stream EventInput { get; set; } = Stream.Null;
        public Stream Events => EventInput;
        public bool EventsReady() => true;
        public Stream Acknowledgements => Stream.Null;
        internal bool Exited { get; set; }
        internal Func<bool>? ReadHasExited { get; set; }
        public bool HasExited => ReadHasExited?.Invoke() ?? Exited;
        internal int Kills { get; private set; }
        internal int ExitObservations { get; private set; }
        internal int Stops { get; private set; }
        internal int Resumes { get; private set; }
        internal int Disposals { get; private set; }
        internal bool ExitObserved { get; set; } = true;
        internal Action OnConfirmStopped { get; set; } = () => { };
        internal Action ArmCheckpointDelay { get; set; } = () => { };
        internal Action OnKill { get; set; } = () => { };
        internal Action OnObserveExit { get; set; } = () => { };
        public OwnedProducerProcess.SignalObservation Stop() { Stops++; return new(19, 0, null); }
        public bool ConfirmStopped(TimeSpan timeout, TimeProvider clock) { OnConfirmStopped(); ArmCheckpointDelay(); return true; }
        public OwnedProducerProcess.SignalObservation Resume() { Resumes++; return new(18, 0, null); }
        public OwnedProducerProcess.SignalObservation Kill() { Kills++; OnKill(); return new(9, 0, null); }
        public OwnedProducerProcess.ExitObservation ObserveExit() { ExitObservations++; OnObserveExit(); return new(ExitObserved, ExitObserved ? 137 : null, "synthetic"); }
        public B10AbruptCgroup.Observation CaptureBefore() => new("synthetic", "/group", "1:2", 0);
        public B10AbruptCgroup.Observation CaptureAfter(B10AbruptCgroup.Observation before) => before;
        public void Dispose() { Disposals++; }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long ticks;
        private readonly List<ManualTimer> timers = [];
        private readonly object gate = new();
        private readonly Channel<TimeSpan> installedTimers = Channel.CreateUnbounded<TimeSpan>();
        private int failureReportPolls;
        internal int FailureReportPolls => Volatile.Read(ref failureReportPolls);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            lock (gate) timers.Add(timer);
            timer.Change(dueTime, period);
            if (dueTime > TimeSpan.Zero && dueTime <= TimeSpan.FromMilliseconds(10)) Interlocked.Increment(ref failureReportPolls);
            installedTimers.Writer.TryWrite(dueTime);
            return timer;
        }
        internal async Task WaitForTimerAsync(TimeSpan dueTime)
        {
            while (await installedTimers.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken) != dueTime) { }
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
