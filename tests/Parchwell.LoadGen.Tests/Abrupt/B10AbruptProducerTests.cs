using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Workload;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptProducerTests
{
    private static readonly string[] EventNames = ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];

    [Fact]
    public async Task Given_OutputDirectoryCollision_When_Initializing_Then_ReportsCaptureGapBeforeConstructingProducer()
    {
        await using var fixture = new Fixture();
        string root = Path.Combine(fixture.Root, "collision");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "abrupt");
        await File.WriteAllBytesAsync(path, [1], TestContext.Current.CancellationToken);
        var exception = Assert.Throws<IOException>(() => new B10AbruptJournal(Path.Combine(path, "journal.jsonl"), HostOptions(root).Identity(), TimeProvider.System));
        Assert.Equal(0, fixture.Constructions);
        AssertCaptureGapReport(exception, "b10_abrupt_capture_gap exception_type=System.IO.IOException", path);

        using var childFixture = new B10AbruptProcessFixture("self-kill");
        string childPath = Path.Combine(childFixture.Directory, "abrupt");
        await File.WriteAllBytesAsync(childPath, [1], TestContext.Current.CancellationToken);
        var start = new ProcessStartInfo(B10AbruptProcessFixture.Apphost())
        { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("--b10-abrupt-child");
        foreach (var pair in childFixture.EnvironmentValues) start.Environment[pair.Key] = pair.Value;
        foreach (var pair in childFixture.Command.ExtraEnvironment) start.Environment[pair.Key] = pair.Value;
        start.Environment[B10AbruptOptions.Prefix + "Role"] = "child";
        B10AbruptOptions.Read(start.Environment.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value ?? ""))).Validate("child");
        using Process child = Process.Start(start)!;
        Task<string> stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        try
        {
            await child.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            Assert.Equal(1, child.ExitCode);
            string errors = await stderr;
            B10AbruptDiagnosticsTests.AssertPlainReport(errors, "b10_abrupt_capture_gap exception_type=System.IO.IOException");
            Assert.DoesNotContain(childPath, errors, StringComparison.Ordinal);
            Assert.DoesNotContain(childFixture.Directory, errors, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(childFixture.Directory, "producer-calls.txt")));
            Assert.False(Directory.Exists(Path.Combine(childFixture.Directory, "capture")));
            Assert.DoesNotContain(childPath, await stdout, StringComparison.Ordinal);
        }
        finally
        {
            if (!child.HasExited) { child.Kill(entireProcessTree: true); Assert.True(child.WaitForExit(10000)); }
        }
    }

    [Fact]
    public async Task Given_CallbackBeforeProduceReturns_When_Invoking_Then_GenuineTerminalPrecedesAcceptance()
    {
        await using var fixture = new Fixture();
        fixture.Initialize();
        fixture.Native.OnProduce = (_, callback) =>
        {
            callback(Report());
            fixture.Journal.DrainAvailable().GetAwaiter().GetResult();
        };
        var attempt = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        Assert.NotNull(attempt.Terminal);
        Assert.Equal(["child_started", "warmup_prepared", "warmup_terminal", "prepared", "pre_acceptance_report", "terminal", "accepted"], fixture.Journal.Frames.Select(frame => frame.Kind));
        Assert.Equal(2, attempt.Terminal.Value.GetProperty("persistence_status").GetInt32());
    }

    [Fact]
    public async Task Given_NoCallback_When_Invoking_Then_NoTerminalOrPersistenceIsInvented()
    {
        await using var fixture = new Fixture(); fixture.Initialize();
        var attempt = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        Assert.Null(attempt.Terminal);
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "terminal");
    }

    [Fact]
    public async Task Given_OriginalCount_When_InvokingProbe_Then_OnlyNextMemberIsAdmitted()
    {
        await using var fixture = new Fixture(); fixture.Initialize();
        async Task Rejected(long ordinal, int count)
        {
            int records = fixture.Journal.Frames.Count;
            Assert.Equal("b10_abrupt_guard_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.InvokeProbe("same_instance", [1], null, ordinal, TestContext.Current.CancellationToken))).Message);
            Assert.Equal(count, fixture.Native.Calls.Count);
            Assert.Equal(records, fixture.Journal.Frames.Count);
        }
        await Rejected(1, 0);
        for (int ordinal = 0; ordinal <= 4; ordinal++) await fixture.Runner.InvokeProbe("same_instance", [1], null, ordinal, TestContext.Current.CancellationToken);
        await Rejected(4, 5);
    }

    [Fact]
    public async Task Given_ExistingResend_When_InvokingAnother_Then_GuardRejectsBeforeDurableOrNativeWork()
    {
        await using var fixture = new Fixture(); fixture.Initialize();
        B10AbruptProducer.ProbeAttempt? original = null;
        for (int ordinal = 0; ordinal <= 4; ordinal++) original = await fixture.Runner.InvokeProbe("same_instance", [1], null, ordinal, TestContext.Current.CancellationToken);
        await fixture.Runner.InvokeProbe("same_instance", [1], original!.AttemptId, 4, TestContext.Current.CancellationToken);
        int records = fixture.Journal.Frames.Count;
        Assert.Equal("b10_abrupt_guard_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.InvokeProbe("same_instance", [1], original.AttemptId, 4, TestContext.Current.CancellationToken))).Message);
        Assert.Equal(6, fixture.Native.Calls.Count); Assert.Equal(records, fixture.Journal.Frames.Count);
    }

    [Fact]
    public async Task Given_PredecessorSequenceMismatch_When_InvokingResend_Then_GuardRejectsBeforeDurableOrNativeWork()
    {
        await using var fixture = new Fixture(); fixture.Initialize();
        B10AbruptProducer.ProbeAttempt? original = null;
        for (int ordinal = 0; ordinal <= 4; ordinal++) original = await fixture.Runner.InvokeProbe("same_instance", [1], null, ordinal, TestContext.Current.CancellationToken);
        int records = fixture.Journal.Frames.Count;
        Assert.Equal("b10_abrupt_guard_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.InvokeProbe("same_instance", [1], original!.AttemptId, 3, TestContext.Current.CancellationToken))).Message);
        Assert.Equal(5, fixture.Native.Calls.Count); Assert.Equal(records, fixture.Journal.Frames.Count);
    }

    [Fact]
    public async Task Given_ProbeCallback_When_Recorded_Then_WritesNeitherConsoleStream()
    {
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        await using var fixture = new Fixture(); fixture.Initialize();
        Action<DeliveryReport<byte[], byte[]>>? recorded = null;
        fixture.Native.OnProduce = (_, callback) => recorded = callback;
        var attempt = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        TextWriter previousOutput = Console.Out, previousErrors = Console.Error;
        try { Console.SetOut(stdout); Console.SetError(stderr); recorded!(Report()); }
        finally { Console.SetOut(previousOutput); Console.SetError(previousErrors); }
        Assert.NotNull(attempt.Terminal); Assert.Empty(stdout.ToString()); Assert.Empty(stderr.ToString());
    }

    [Fact]
    public async Task Given_CallbackAfterAcceptance_When_Recording_Then_PreservesTerminalForFinalReconciliation()
    {
        await using var fixture = new Fixture(); fixture.Initialize();
        Action<DeliveryReport<byte[], byte[]>>? recorded = null;
        fixture.Native.OnProduce = (_, callback) => recorded = callback;
        var attempt = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        Assert.Null(attempt.Terminal); Assert.NotNull(recorded);
        recorded(Report()); await fixture.Journal.DrainAvailable(TestContext.Current.CancellationToken);
        Assert.NotNull(attempt.Terminal);
        Assert.Equal(["child_started", "warmup_prepared", "warmup_terminal", "prepared", "pre_acceptance_report", "accepted", "terminal"], fixture.Journal.Frames.Select(frame => frame.Kind));
    }

    [Fact]
    public async Task Given_PreAcceptanceReport_When_Invoking_Then_RawFileAndExactAttributionPrecedeAccepted()
    {
        await using var fixture = new Fixture(); fixture.Initialize();
        KafkaStatisticsReport report = fixture.LastReport!;
        await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        JsonElement handoff = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "pre_acceptance_report").Data;
        Assert.Equal("B10", handoff.GetProperty("report_identity").GetProperty("scenario_id").GetString());
        Assert.Equal("run-1", handoff.GetProperty("report_identity").GetProperty("run_id").GetString());
        Assert.Equal("LoadGenProducer", handoff.GetProperty("report_identity").GetProperty("role").GetString());
        Assert.Equal("pod-1", handoff.GetProperty("report_identity").GetProperty("client_id").GetString());
        Assert.Equal(2, handoff.GetProperty("report_sequence").GetInt64());
        Assert.Equal(report.ReceivedAt.ToString("O", CultureInfo.InvariantCulture), handoff.GetProperty("report_capture_utc").GetString());
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(fixture.Root, "abrupt", "same_instance-pre-acceptance-report.json"), TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(report.RawJson), bytes);
        Assert.Equal(bytes.Length, handoff.GetProperty("length").GetInt32());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), handoff.GetProperty("sha256").GetString());
        Assert.True(fixture.Journal.Frames.Single(frame => frame.Kind == "pre_acceptance_report").Sequence < fixture.Journal.Frames.Single(frame => frame.Kind == "accepted").Sequence);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_CompletedControlOriginal_When_ReleaseArrives_Then_ResendsOnceAndAdmitsKillWithoutCleanup(bool terminalAfterCut)
    {
        await using var fixture = new Fixture();
        fixture.ConfigureControl(terminalAfterCut: terminalAfterCut);
        fixture.StartReports();
        await Assert.ThrowsAsync<SimulatedDeathException>(() => fixture.Runner.RunFirstAsync(Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        var cut = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "cut");
        string predecessor = Assert.Single(cut.Data.GetProperty("unconfirmed_attempt_ids").EnumerateArray()).GetString()!;
        Assert.EndsWith("same_instance-4", predecessor, StringComparison.Ordinal);
        var resend = Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "prepared" && frame.Data.GetProperty("role").GetString() == "resend");
        Assert.Equal(predecessor, resend.Data.GetProperty("predecessor_attempt_id").GetString());
        Assert.True(DateTimeOffset.Parse(resend.ObservedUtc, CultureInfo.InvariantCulture) >= DateTimeOffset.Parse(cut.Data.GetProperty("release_utc").GetString()!, CultureInfo.InvariantCulture));
        Assert.Equal(11, fixture.Native.Calls.Count);
        Assert.Equal(fixture.Native.Calls[4].Key, fixture.Native.Calls[5].Key);
        Assert.Equal(fixture.Native.Calls[4].Value, fixture.Native.Calls[5].Value);
        Assert.Equal(0, fixture.Native.FlushAfterKill); Assert.Equal(0, fixture.Native.Disposals);
        Assert.Equal("kill_intent", fixture.Journal.Frames[^1].Kind);
        Assert.Empty(B10AbruptJournal.Read(await File.ReadAllBytesAsync(fixture.Journal.Path, TestContext.Current.CancellationToken)).Gaps);
    }

    [Fact]
    public async Task Given_ControlArm_When_Run_Then_FiveSequentialOriginals()
    {
        await using var fixture = new Fixture(); fixture.ConfigureControl(); fixture.StartReports();
        await Assert.ThrowsAsync<SimulatedDeathException>(() => fixture.Runner.RunFirstAsync(Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        var originals = fixture.Journal.Frames.Where(frame => frame.Kind == "prepared" && frame.Data.GetProperty("arm").GetString() == "same_instance" && frame.Data.GetProperty("role").GetString() == "original").ToArray();
        Assert.Equal([0, 1, 2, 3, 4], originals.Select(frame => frame.Data.GetProperty("ordinal").GetInt32()));
        for (int ordinal = 1; ordinal <= 4; ordinal++) Assert.True(originals[ordinal].MonotonicTicks - originals[0].MonotonicTicks >= TimeSpan.FromMilliseconds(ordinal * 10).Ticks);
        Assert.Equal(originals[0].Data.GetProperty("attempt_id").GetString(), fixture.Journal.Frames.First(frame => frame.Kind == "pre_acceptance_report").Data.GetProperty("attempt_id").GetString());
    }

    [Fact]
    public async Task Given_AllMembersConfirmed_When_ControlCheckpoints_Then_ExposureInconclusive()
    {
        await using var fixture = new Fixture();
        fixture.Native.OnProduce = (_, callback) => { callback(Report()); fixture.Journal.DrainAvailable().GetAwaiter().GetResult(); };
        fixture.StartReports();
        Assert.Equal(3, await fixture.Runner.RunFirstAsync(Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_exposure_inconclusive", fixture.Journal.Frames[^1].Data.GetProperty("reason").GetString());
        Assert.Equal("no_kill", fixture.Journal.Frames[^1].Kind);
        Assert.Empty(Assert.Single(fixture.Journal.Frames, frame => frame.Kind == "cut").Data.GetProperty("unconfirmed_attempt_ids").EnumerateArray());
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "prepared" && frame.Data.GetProperty("arm").GetString() == "cross_restart");
        Assert.Equal(1, fixture.Native.Disposals);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_MissingControlTerminal_When_Draining_Then_KillAdmissionIsBlocked(bool flushHasOutstanding)
    {
        await using var fixture = new Fixture(); fixture.ConfigureControl(missingResendTerminal: !flushHasOutstanding);
        fixture.Native.FlushResult = flushHasOutstanding ? 1 : 0;
        fixture.StartReports();
        Assert.Equal(3, await fixture.Runner.RunFirstAsync(Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_control_incomplete", fixture.Journal.Frames[^1].Data.GetProperty("reason").GetString());
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "prepared" && frame.Data.GetProperty("arm").GetString() == "cross_restart");
    }

    [Theory]
    [InlineData("same_instance", false, "b10_abrupt_cut_rejected")]
    [InlineData("cross_restart", false, "b10_abrupt_cut_rejected")]
    [InlineData("cross_restart", true, "kill_too_early")]
    public async Task Given_CutOriginalRejected_When_Running_Then_NoKillHasExactCause(string rejectedArm, bool rejectAllCross, string reason)
    {
        await using var fixture = new Fixture(); fixture.ConfigureControl();
        var original = fixture.Native.OnProduce;
        fixture.Native.OnProduce = (message, callback) =>
        {
            string attempt = Encoding.UTF8.GetString(message.Headers.GetLastBytes("attempt-id"));
            if (Encoding.UTF8.GetString(message.Key).EndsWith(rejectedArm, StringComparison.Ordinal) && (Encoding.UTF8.GetString(message.Headers.GetLastBytes("sequence")) == "4" || rejectAllCross))
                throw new ProduceException<byte[], byte[]>(new Error(ErrorCode.Local_QueueFull), new DeliveryResult<byte[], byte[]>());
            original(message, callback);
        };
        fixture.StartReports();
        Assert.Equal(3, await fixture.Runner.RunFirstAsync(Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        Assert.Equal("no_kill", fixture.Journal.Frames[^1].Kind);
        Assert.Equal(reason, fixture.Journal.Frames[^1].Data.GetProperty("reason").GetString());
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "kill_intent");
    }
    [Trait("Tier", "Slow")]
    [Fact]
    public async Task Given_LastControlTerminalRacesWithCut_When_RunRepeatedly_Then_JournalOrderAndStoredSetAgree()
    {
        for (int iteration = 0; iteration < 1000; iteration++)
        {
            await using var fixture = Fixture.ForCutRace();
            Task? racing = null;
            int originals = 0;
            fixture.Native.OnProduce = (message, callback) =>
            {
                if (!Encoding.UTF8.GetString(message.Key).EndsWith("same_instance", StringComparison.Ordinal)) return;
                int ordinal = originals++;
                if (ordinal < 4) { callback(Report()); fixture.Journal.DrainAvailable().GetAwaiter().GetResult(); }
                else if (ordinal == 4)
                {
                    racing = Task.Run(async () =>
                    {
                        while (!fixture.Journal.Frames.Any(frame => frame.Kind == "accepted" && frame.Data.GetProperty("attempt_id").GetString()!.EndsWith("same_instance-4", StringComparison.Ordinal))) await Task.Yield();
                        callback(Report()); await fixture.Journal.DrainAvailable(TestContext.Current.CancellationToken);
                    }, TestContext.Current.CancellationToken);
                }
                else { callback(Report()); fixture.Journal.DrainAvailable().GetAwaiter().GetResult(); }
            };
            fixture.StartReports(advanceClock: false);
            int result;
            try { result = await fixture.Runner.RunFirstAsync(Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken); }
            catch (SimulatedDeathException) { result = 137; }
            Assert.NotNull(racing); await racing;
            var snapshot = B10AbruptJournal.Read(await File.ReadAllBytesAsync(fixture.Journal.Path, TestContext.Current.CancellationToken));
            Assert.Empty(snapshot.Gaps);
            var cut = Assert.Single(snapshot.Frames, frame => frame.Kind == "cut");
            Assert.Equal(snapshot.ControlUnconfirmedAttemptIds, cut.Data.GetProperty("unconfirmed_attempt_ids").EnumerateArray().Select(item => item.GetString()));
            var terminal = Assert.Single(snapshot.Frames, frame => frame.Kind == "terminal" && frame.Data.GetProperty("attempt_id").GetString()!.EndsWith("same_instance-4", StringComparison.Ordinal));
            if (terminal.Sequence < cut.Sequence)
            {
                Assert.Empty(snapshot.ControlUnconfirmedAttemptIds);
                Assert.Equal(3, result);
                Assert.Equal("b10_abrupt_exposure_inconclusive", Assert.Single(snapshot.Frames, frame => frame.Kind == "no_kill").Data.GetProperty("reason").GetString());
            }
        }
    }

    [Fact]
    public async Task Given_JournalWriterBlocked_When_AnotherCallbackArrives_Then_CallbackEnqueuesWithoutWaiting()
    {
        await using var fixture = new Fixture(); fixture.Initialize();
        var callbacks = new List<Action<DeliveryReport<byte[], byte[]>>>();
        fixture.Native.OnProduce = (_, callback) => callbacks.Add(callback);
        await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        await fixture.Runner.InvokeProbe("same_instance", [1], null, 1, TestContext.Current.CancellationToken);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        fixture.FlushHook = () => { entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)); return Task.CompletedTask; };
        try
        {
            callbacks[0](Report());
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            await Task.Run(() => callbacks[1](Report()), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally { release.Set(); }
        await fixture.Journal.DrainAvailable(TestContext.Current.CancellationToken);
        Assert.Equal(2, fixture.Journal.Frames.Count(frame => frame.Kind == "terminal"));
    }

    [Fact]
    public async Task Given_SynchronousRejection_When_Invoking_Then_NoAcceptedRecordIsWritten()
    {
        await using var fixture = new Fixture(); fixture.Initialize();
        fixture.Native.OnProduce = (_, _) => throw new ProduceException<byte[], byte[]>(new Error(ErrorCode.Local_QueueFull), new DeliveryResult<byte[], byte[]>());
        var attempt = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        Assert.False(attempt.Accepted);
        Assert.Equal(["child_started", "warmup_prepared", "warmup_terminal", "prepared", "pre_acceptance_report", "synchronous_rejection"], fixture.Journal.Frames.Select(frame => frame.Kind));
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "accepted");
    }

    [Fact]
    public async Task Given_OverflowedCounter_When_Invoking_Then_EarlierReportCannotRepairMissingSample()
    {
        await using var fixture = new Fixture(long.MaxValue - 1); fixture.Initialize(warm: false); fixture.Observe();
        Assert.Equal("b10_abrupt_sample_missing", (await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken))).Message);
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind is "pre_acceptance_report" or "accepted");
        Assert.Empty(fixture.Native.Calls);
    }

    [Theory]
    [InlineData(ErrorCode.NoError, PersistenceStatus.Persisted, true)]
    [InlineData(ErrorCode.NoError, PersistenceStatus.NotPersisted, true)]
    [InlineData(ErrorCode.NoError, PersistenceStatus.PossiblyPersisted, false)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.NotPersisted, true)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.PossiblyPersisted, true)]
    [InlineData(ErrorCode.Local_MsgTimedOut, PersistenceStatus.Persisted, false)]
    [InlineData(ErrorCode.Local_Transport, PersistenceStatus.PossiblyPersisted, false)]
    public async Task Given_ProbeCallback_When_AdmittingResend_Then_PossiblyPersistedRequiresPermittedTimeout(ErrorCode error, PersistenceStatus status, bool permitted)
    {
        await using var fixture = new Fixture(); fixture.Initialize();
        fixture.Native.OnProduce = (_, callback) => callback(Report(error, status));
        var original = await fixture.Runner.InvokeProbe("same_instance", [1], null, 0, TestContext.Current.CancellationToken);
        Assert.NotNull(original.Terminal);
        Assert.Equal((int)status, original.Terminal.Value.GetProperty("persistence_status").GetInt32());
        Assert.Equal((int)error, original.Terminal.Value.GetProperty("error_code").GetInt32());
        Task resend = fixture.Runner.InvokeProbe("same_instance", [1], original.AttemptId, original.Sequence, TestContext.Current.CancellationToken);
        if (permitted) { await resend; Assert.Equal(2, fixture.Native.Calls.Count); }
        else { Assert.Equal("b10_abrupt_guard_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => resend)).Message); Assert.Single(fixture.Native.Calls); }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Given_SuccessorEntrySequence_When_Bound_Then_InBurstOnly(long sequence)
    {
        await using var fixture = new Fixture();
        await fixture.StartJournalAsync(); fixture.StartReports();
        fixture.Native.OnProduce = (_, callback) => callback(Report());
        byte[] payload = GeneratedPayload();
        JsonElement selection = JsonSerializer.SerializeToElement(new[] { new
        {
            attempt_id = "predecessor-cross_restart-4", key = "B10/run-1/C1/1/cross_restart", sequence,
            payload_length = payload.Length, payload_sha256 = Convert.ToHexStringLower(SHA256.HashData(payload))
        } });
        Task<B10AbruptProducer.SuccessorArtifacts> operation = fixture.Runner.RunSuccessorAsync(Profile(), fixture.Identity, fixture.Options, selection, fixture.Clock.GetUtcNow().AddSeconds(1), TestContext.Current.CancellationToken);
        if (sequence == 5)
        {
            Assert.Equal("b10_abrupt_successor_binding_invalid", (await Assert.ThrowsAsync<InvalidDataException>(() => operation)).Message);
            Assert.Empty(fixture.Native.Calls); return;
        }
        var artifacts = await operation;
        Assert.Equal("4", Encoding.UTF8.GetString(Assert.Single(fixture.Native.Calls).Headers.GetLastBytes("sequence")));
        Assert.Equal(sequence, artifacts.Invocations[0].GetProperty("sequence").GetInt64());
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(artifacts.Paths["manifest.json"], TestContext.Current.CancellationToken));
        Assert.Equal(sequence, manifest.RootElement.GetProperty("data").GetProperty("inventory")[0].GetProperty("sequence").GetInt64());
    }

    [Fact]
    public async Task Given_OutstandingMessages_When_DrainBudgetExpires_Then_DiagnosticNamesOutstandingCountAndBudget()
    {
        var clock = new ManualClock(); await using var fixture = new Fixture(clock: clock);
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Native.FlushResult = 3; fixture.Native.OnFlush = () => clock.Advance(TimeSpan.FromSeconds(1));
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => InvokePrivateAsync(fixture.Runner, "DrainAsync", TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_control_incomplete", exception.Message);
        JsonElement detail = Detail(exception, "DrainAsync");
        Assert.Equal(3, detail.GetProperty("facts").GetProperty("outstanding_count").GetInt32());
        Assert.Equal(1000, detail.GetProperty("facts").GetProperty("budget_milliseconds").GetInt32());
    }

    [Fact]
    public async Task Given_NoReportAfterDrainBoundary_When_WaitExpires_Then_DiagnosticNamesEachSelectionPredicate()
    {
        var clock = new ManualClock(); await using var fixture = new Fixture(clock: clock);
        fixture.Runner.Initialize(Profile(), fixture.Identity, fixture.Options);
        fixture.Statistics.Observe(StatisticsReport(new("B10", "run-1", KafkaClientRole.LoadGenProducer, "pod-1"), clock));
        clock.Advance(TimeSpan.FromSeconds(1)); DateTimeOffset drained = clock.GetUtcNow();
        Task operation = InvokePrivateAsync(fixture.Runner, "WaitForPostDrainAsync", 0, drained, TestContext.Current.CancellationToken);
        await clock.WaitForTimerAsync(TimeSpan.FromSeconds(1)); clock.Advance(TimeSpan.FromSeconds(1));
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => operation);
        Assert.Equal("b10_abrupt_control_incomplete", exception.Message);
        JsonElement facts = Detail(exception, "WaitForPostDrainAsync").GetProperty("facts");
        Assert.Equal(1, facts.GetProperty("report_count").GetInt32()); Assert.Equal(1, facts.GetProperty("snapshot_count").GetInt32());
        Assert.Equal(0, facts.GetProperty("after_drain_utc_count").GetInt32()); Assert.Equal(0, facts.GetProperty("after_native_boundary_count").GetInt32());
        Assert.Equal(1, facts.GetProperty("expected_brokers_count").GetInt32()); Assert.Equal(1, facts.GetProperty("qualifying_brokers_count").GetInt32());
        Assert.Equal(0, facts.GetProperty("overflow").GetInt32()); Assert.Equal(1, facts.GetProperty("drain_boundary_matches").GetInt32());
    }

    [Theory]
    [InlineData(ErrorCode.NoError)]
    [InlineData(ErrorCode.Local_MsgTimedOut)]
    public async Task Given_SealedKillSelection_When_Replaying_Then_InvokesOnceWithoutRetryAndDrains(ErrorCode outcome)
    {
        await using var fixture = new Fixture(); await fixture.StartJournalAsync(); fixture.StartReports();
        fixture.Native.OnProduce = (_, callback) => callback(Report(outcome, outcome == ErrorCode.NoError ? PersistenceStatus.Persisted : PersistenceStatus.PossiblyPersisted));
        byte[] payload = GeneratedPayload();
        JsonElement selection = JsonSerializer.SerializeToElement(new[] { new
        {
            attempt_id = "predecessor-cross_restart-0", key = "B10/run-1/C1/1/cross_restart", sequence = 0,
            payload_length = payload.Length, payload_sha256 = Convert.ToHexStringLower(SHA256.HashData(payload))
        } });
        var artifacts = await fixture.Runner.RunSuccessorAsync(Profile(), fixture.Identity, fixture.Options, selection, fixture.Clock.GetUtcNow().AddSeconds(1), TestContext.Current.CancellationToken);
        Assert.Single(fixture.Native.Calls); Assert.Equal(payload, fixture.Native.Calls[0].Value); Assert.Equal(1, fixture.Native.Disposals);
        Assert.Empty(fixture.Native.WarmupCalls);
        Assert.DoesNotContain(fixture.Journal.Frames, frame => frame.Kind == "warmup_prepared");
        Assert.Equal(5, artifacts.Paths.Count); Assert.All(artifacts.Paths.Values, path => Assert.True(File.Exists(path)));
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(artifacts.Paths["manifest.json"], TestContext.Current.CancellationToken));
        Assert.Equal(3, manifest.RootElement.GetProperty("schema_version").GetInt32()); Assert.Equal("b10_abrupt", manifest.RootElement.GetProperty("dialect").GetString());
        Assert.Equal(1, manifest.RootElement.GetProperty("data").GetProperty("inventory_count").GetInt32());
        Assert.Equal("predecessor-cross_restart-0", artifacts.Invocations[0].GetProperty("predecessor_attempt_id").GetString());
        Assert.NotEqual("predecessor-cross_restart-0", artifacts.Invocations[0].GetProperty("attempt_id").GetString());
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
        byte[] payload = GeneratedPayload();
        JsonElement selection = JsonSerializer.SerializeToElement(new[] { new
        {
            attempt_id = "predecessor-cross_restart-0", key = "B10/run-1/C1/1/cross_restart", sequence = 0,
            payload_length = payload.Length, payload_sha256 = Convert.ToHexStringLower(SHA256.HashData(payload))
        } });
        Task operation = Task.Run(() => fixture.Runner.RunSuccessorAsync(Profile(), fixture.Identity, fixture.Options, selection, release, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        if (ticksFromRelease < 0)
        {
            await clock.WaitObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Empty(fixture.Native.Calls); Assert.False(operation.IsCompleted); clock.Advance(TimeSpan.FromTicks(1));
        }
        if (ticksFromRelease > 0)
        {
            Assert.Equal("b10_abrupt_successor_late", (await Assert.ThrowsAsync<InvalidDataException>(() => operation)).Message); Assert.Empty(fixture.Native.Calls);
        }
        else
        {
            Assert.Equal("synthetic_invocation_observed", (await Assert.ThrowsAsync<InvalidOperationException>(() => operation)).Message);
            Assert.Single(fixture.Native.Calls); Assert.Equal(release, clock.GetUtcNow());
        }
    }

    [Fact]
    public async Task Given_MissingPersistenceOnGenuineCallback_When_Running_Then_GuardBlocksControl()
    {
        await using var fixture = new Fixture(); fixture.StartReports();
        fixture.Native.OnProduce = (_, callback) => callback(Report(status: null));
        Assert.Equal(3, await fixture.Runner.RunFirstAsync(Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_guard_failed", fixture.Journal.Frames.Last(frame => frame.Kind == "no_kill").Data.GetProperty("reason").GetString());
        Assert.Single(fixture.Native.Calls); Assert.Empty(fixture.CrossCallbacks);
    }

    private static byte[] GeneratedPayload() => WorkloadGenerator.GeneratePayload(WorkloadGenerator.Generate(Profile(), 8, 256, TestContext.Current.CancellationToken).First(), Profile().Seed);
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
        var reportIdentity = new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, fixture.Identity.ClientId);
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
        Assert.True(stats.SelectPostDrain(drained)!.Sequence > 0);
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

    internal static T RuntimeMember<T>(object runtime, string name) => (T)runtime.GetType().GetProperty(name)!.GetValue(runtime)!;

    internal static B10AbruptOptions HostOptions(string root)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Enabled"] = "true", ["Purpose"] = "diagnostic_trial", ["Role"] = "child", ["RunId"] = "run-1",
            ["ScenarioId"] = "B10", ["ConfigurationId"] = "C1", ["Phase"] = "C1", ["Repeat"] = "1",
            ["SourceCommit"] = new('a', 40), ["ImageReference"] = "image@sha256:" + new string('b', 64),
            ["JobName"] = "run-1-c1-r1-loadgen-restart", ["PodName"] = "pod-1", ["PodUid"] = "uid-1", ["NamespaceName"] = "synthetic-ns",
            ["BlobEndpoint"] = "https://storage.invalid/", ["ScenarioSourceRoot"] = AppContext.BaseDirectory,
            ["OutputRoot"] = root, ["StartLineBindingPath"] = Path.Combine(root, "start-line.json")
        };
        foreach (var policy in B10AbruptOptions.Policy) values[policy.Key] = policy.Value.ToString(CultureInfo.InvariantCulture);
        return B10AbruptOptions.Read(values.ToDictionary(pair => B10AbruptOptions.Prefix + pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    internal static ServiceProvider ProducerHostServices(B10AbruptOptions options, UnusedTokenProvider tokenProvider)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        typeof(B10AbruptHost).GetMethod("AddB10KafkaRoles", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [services, options]);
        services.AddParchwellScenarios(value =>
        { value.MaxDocumentCharacters = 1048576; value.MaxNestingDepth = 32; value.MaxScalarCharacters = 65536; value.MaxParserEvents = 100000; });
        services.AddSingleton<IScenarioSource>(new FileSystemScenarioSource(options.ScenarioSourceRoot));
        services.AddSingleton<RunEvidenceSinkRouter>();
        services.AddSingleton<IKafkaStatisticsEvidenceSink>(provider => provider.GetRequiredService<RunEvidenceSinkRouter>());
        services.AddSingleton<IKafkaTokenProvider>(tokenProvider);
        services.AddParchwellKafkaAuthentication(value =>
        { value.TenantId = "synthetic-tenant"; value.ClientId = "synthetic-client"; value.TokenFilePath = Path.Combine(options.OutputRoot, "missing-token"); });
        services.AddParchwellKafkaStatistics(value =>
        { value.MaxSnapshotCharacters = 1048576; value.QueueCapacity = 2048; value.MaxBrokerSeries = 64; value.CompletionTimeout = TimeSpan.FromSeconds(1); });
        return services.BuildServiceProvider();
    }

    private static JsonElement Detail(Exception exception, string member)
    {
        JsonElement detail = JsonSerializer.SerializeToElement(B10AbruptDiagnostics.Describe(exception)).GetProperty("detail");
        Assert.Equal("B10AbruptProducer.cs", detail.GetProperty("file").GetString());
        Assert.Equal(member, detail.GetProperty("member").GetString());
        return detail;
    }

    private static Task InvokePrivateAsync(B10AbruptProducer runner, string member, params object[] arguments) =>
        (Task)typeof(B10AbruptProducer).GetMethod(member, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(runner, arguments)!;

    private static void AssertCaptureGapReport(Exception exception, string expectedLine, string privatePath)
    {
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(exception, errors));
        B10AbruptDiagnosticsTests.AssertPlainReport(errors.ToString(), expectedLine);
        Assert.DoesNotContain(privatePath, errors.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(exception.Message, errors.ToString(), StringComparison.Ordinal);
    }

    internal static DeliveryReport<byte[], byte[]> Report(ErrorCode code = ErrorCode.NoError, PersistenceStatus? status = PersistenceStatus.Persisted) => new()
    { Error = new Error(code), Status = status ?? (PersistenceStatus)(-1), Partition = 0, Offset = 1 };

    internal static KafkaStatisticsReport StatisticsReport(KafkaStatisticsIdentity identity, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        long timestamp = checked((long)((Int128)clock.GetTimestamp() * 1_000_000 / clock.TimestampFrequency));
        return new(identity, "session", clock.GetUtcNow(), JsonSerializer.Serialize(new
        {
            name = "native", client_id = identity.ClientId, type = "producer", ts = timestamp, time = 100, age = timestamp,
            topics = new Dictionary<string, object> { ["document-events"] = new { partitions = new Dictionary<string, object> { ["0"] = new { leader = 0 } } } },
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


    internal sealed class SimulatedDeathException : Exception;

    internal sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "abrupt-producer-" + Guid.NewGuid().ToString("N"));
        internal RunIdentity Identity { get; }
        internal RunExecutionOptions Options { get; }
        internal B10AbruptProducer Runner { get; }
        internal ReleaseWindowClock Clock { get; }
        internal B10AbruptWireProducerTests.HarnessClock Advancing { get; } = new();
        internal B10AbruptStatistics Statistics { get; }
        internal B10AbruptJournal Journal { get; }
        internal NativeProducer Native { get; }
        internal KafkaStatisticsReport? LastReport { get; private set; }
        internal Func<KafkaStatisticsReport, KafkaStatisticsReport> TransformReport { get; set; } = report => report;
        internal bool SuppressReports { get; set; }
        internal bool AdvanceReportClock { get; set; } = true;
        internal int Constructions { get; private set; }
        internal Action AtKill { get; set; } = () => { };
        internal List<Action<DeliveryReport<byte[], byte[]>>> CrossCallbacks { get; } = [];
        internal List<int> Exits { get; } = [];
        internal Func<Task> FlushHook { get; set; } = () => Task.CompletedTask;
        private readonly bool advancesReports;
        private readonly bool warmupOnSuppliedClock;
        private readonly CancellationTokenSource reportStop = new();
        private Task? reportTask;
        private readonly KafkaStatisticsIdentity statisticsIdentity;
        internal Fixture(long sequence = 0, TimeProvider? clock = null, B10AbruptTimeline? timeline = null, B10AbruptWire.Identity? diagnosticIdentity = null,
            Action<TimeSpan>? advanceWarmupClock = null, string purpose = "diagnostic_trial")
        {
            Advancing.EnterManual();
            Clock = new ReleaseWindowClock(clock ?? Advancing);
            advancesReports = clock is null;
            warmupOnSuppliedClock = clock is not null && advanceWarmupClock is not null;
            Directory.CreateDirectory(Root);
            Options = new RunExecutionOptions(8, 256, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1),
                16384, 1048576, 1048576, Root, "document-events", ["broker"], false, ApplicationRetry: ApplicationRetryPolicy.ForScenario("B10"));
            Identity = diagnosticIdentity is null ? new("B10", "C1", "run-1", "pod-1", "pod-1", "pod-1") : new("B10", "C1", diagnosticIdentity.RunId, diagnosticIdentity.InstanceId, diagnosticIdentity.InstanceId, diagnosticIdentity.InstanceId);
            statisticsIdentity = new("B10", Identity.RunId, KafkaClientRole.LoadGenProducer, Identity.ClientId);
            Statistics = new(statisticsIdentity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>(), initialSequence: sequence);
            var wireIdentity = diagnosticIdentity ?? new B10AbruptWire.Identity("run-1", "B10", "C1", "C1", 1, new string('a', 40),
                "image@sha256:" + new string('b', 64), "run-1-c1-r1-loadgen-restart", "pod-1", "uid-1", "pod-1", null);
            Journal = new(Path.Combine(Root, "abrupt", "journal.jsonl"), wireIdentity, Clock, _ => FlushHook(), purpose);
            var native = DispatchProxy.Create<IProducer<byte[], byte[]>, NativeProducer>();
            Native = (NativeProducer)(object)native;
            if (warmupOnSuppliedClock)
                Native.OnWarmup = (message, callback) =>
                {
                    callback(Report());
                    string attemptId = Encoding.UTF8.GetString(message.Headers.GetLastBytes("attempt-id"));
                    Assert.True(SpinWait.SpinUntil(() => Journal.Frames.Any(frame => frame.Kind == "warmup_terminal" &&
                        frame.Data.GetProperty("attempt_id").GetString() == attemptId), TimeSpan.FromSeconds(5)));
                    advanceWarmupClock!(TimeSpan.FromMilliseconds(1));
                    Observe();
                };
            Native.OnFlush = () => Observe();
            Runner = new(() => { Constructions++; return native; }, Clock, Statistics, Journal, diagnosticIdentity: wireIdentity,
                timeline: timeline, selfKill: () => { AtKill(); throw new SimulatedDeathException(); }, exit: code => Exits.Add(code), purpose: purpose);
        }
        internal static Fixture ForCutRace()
        {
            var clock = new CutRaceClock();
            var fixture = new Fixture(clock: clock);
            clock.ReleaseUtc = () => fixture.Journal.Frames.LastOrDefault(frame => frame.Kind == "cut") is { } cut
                ? DateTimeOffset.Parse(cut.Data.GetProperty("release_utc").GetString()!, CultureInfo.InvariantCulture) : null;
            return fixture;
        }
        internal void Initialize(bool warm = true, string arm = "same_instance")
        {
            Journal.AppendAsync("child_started", B10AbruptWire.Element(new { pid = Environment.ProcessId, start_time_ticks = B10AbruptProc.ReadStartTimeTicks(B10AbruptProc.ReadBounded("/proc/self/stat")), boot_id = B10AbruptProc.ReadBootId() })).GetAwaiter().GetResult();
            Runner.Initialize(Profile(), Identity, Options);
            Advancing.Advance(TimeSpan.FromMilliseconds(1)); Observe(); Advancing.Advance(TimeSpan.FromMilliseconds(1));
            if (warm)
            {
                Task preparation = Runner.PrepareArmAsync(arm, TestContext.Current.CancellationToken);
                if (!warmupOnSuppliedClock)
                {
                    Journal.DrainAvailable().GetAwaiter().GetResult();
                    Observe();
                    Advancing.Advance(TimeSpan.FromMilliseconds(10));
                }
                preparation.GetAwaiter().GetResult();
            }
        }
        internal Task StartJournalAsync() => Journal.AppendAsync("child_started", B10AbruptWire.Element(new { pid = Environment.ProcessId, start_time_ticks = B10AbruptProc.ReadStartTimeTicks(B10AbruptProc.ReadBounded("/proc/self/stat")), boot_id = B10AbruptProc.ReadBootId() }), TestContext.Current.CancellationToken);
        internal KafkaStatisticsReport Observe()
        {
            if (SuppressReports && LastReport is not null) return LastReport;
            if (advancesReports) Advancing.Advance(TimeSpan.FromMicroseconds(1));
            LastReport = TransformReport(StatisticsReport(statisticsIdentity, Clock));
            if (!SuppressReports) Statistics.Observe(LastReport);
            return LastReport;
        }
        internal void ConfigureControl(bool terminalAfterCut = false, bool missingResendTerminal = false)
        {
            Action<DeliveryReport<byte[], byte[]>>? pending = null;
            int originals = 0;
            bool originalCompleted = false;
            Native.OnProduce = (message, callback) =>
            {
                if (!Encoding.UTF8.GetString(message.Key).EndsWith("same_instance", StringComparison.Ordinal)) { CrossCallbacks.Add(callback); return; }
                if (originals++ < 4) { callback(Report()); Journal.DrainAvailable().GetAwaiter().GetResult(); }
                else if (originals == 5) pending = callback;
                else
                {
                    if (!originalCompleted) { pending!(Report()); originalCompleted = true; }
                    if (!missingResendTerminal) callback(Report());
                    Journal.DrainAvailable().GetAwaiter().GetResult();
                }
            };
            if (terminalAfterCut)
                BeforeAdvanceRelease = () => { pending!(Report()); originalCompleted = true; Journal.DrainAvailable().GetAwaiter().GetResult(); };
        }
        private Action? BeforeAdvanceRelease { get; set; }
        internal Func<DateTimeOffset, DateTimeOffset> ReleaseTarget { get; set; } = release => release;
        internal void StartReports(bool advanceClock = true) => reportTask = Task.Run(async () =>
        {
            try
            {
                bool released = false;
                while (!reportStop.IsCancellationRequested)
                {
                    if (advanceClock && AdvanceReportClock && !Journal.Frames.Any(frame => frame.Kind == "no_kill"))
                        Advancing.Advance(TimeSpan.FromMilliseconds(10));
                    Observe();
                    B10AbruptWire.Frame? cut = Journal.Frames.LastOrDefault(frame => frame.Kind == "cut");
                    if (advanceClock && !released && cut is not null)
                    {
                        released = true; BeforeAdvanceRelease?.Invoke();
                        Advancing.AdvanceTo(ReleaseTarget(DateTimeOffset.Parse(cut.Data.GetProperty("release_utc").GetString()!, CultureInfo.InvariantCulture)));
                        Advancing.Advance(TimeSpan.Zero);
                    }
                    await Task.Delay(1, reportStop.Token);
                }
            }
            catch (OperationCanceledException) when (reportStop.IsCancellationRequested) { }
        }, TestContext.Current.CancellationToken);
        internal async Task StopReportsAsync() { await reportStop.CancelAsync(); if (reportTask is not null) await reportTask; }
        public async ValueTask DisposeAsync()
        {
            await StopReportsAsync();
            await Journal.DisposeAsync(); reportStop.Dispose();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    private sealed class CutRaceClock : TimeProvider
    {
        private readonly DateTimeOffset initialUtc = TimeProvider.System.GetUtcNow();
        private readonly long initialTimestamp = TimeProvider.System.GetTimestamp();
        private long releaseOffset;
        internal Func<DateTimeOffset?> ReleaseUtc { get; set; } = () => null;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => TimeProvider.System.GetElapsedTime(initialTimestamp).Ticks + Interlocked.Read(ref releaseOffset);
        public override DateTimeOffset GetUtcNow() => initialUtc.AddTicks(GetTimestamp());
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            // Advance only the registered control-release wait. Ordinary pacing and the one-second
            // deadlines use live system timers, so report scheduling cannot expire queued cleanup.
            if (dueTime > TimeSpan.FromSeconds(1) && dueTime <= TimeSpan.FromSeconds(120) && ReleaseUtc() is { } release)
            {
                DateTimeOffset now = GetUtcNow();
                // Task.Delay rounds its timer request down to whole milliseconds.
                if (now < release && now + dueTime + TimeSpan.FromMilliseconds(1) >= release)
                {
                    Interlocked.Add(ref releaseOffset, (release - now).Ticks);
                    dueTime = TimeSpan.Zero;
                }
            }
            return TimeProvider.System.CreateTimer(callback, state, dueTime, period);
        }
    }

    internal sealed class UnusedTokenProvider : IKafkaTokenProvider
    {
        internal int Calls { get; private set; }
        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("synthetic_token_provider_must_not_run");
        }
    }

    internal sealed class ReleaseWindowClock(TimeProvider inner) : TimeProvider
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

    private sealed class ManualClock : TimeProvider
    {
        private long ticks;
        private readonly List<ManualTimer> timers = [];
        private readonly object gate = new();
        private readonly Channel<TimeSpan> installedTimers = Channel.CreateUnbounded<TimeSpan>();
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            lock (gate) timers.Add(timer);
            timer.Change(dueTime, period);
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

    public class NativeProducer : DispatchProxy
    {
        // Original and resend counts keep their probe-only meaning; warm-up invocations are checked separately.
        internal List<Message<byte[], byte[]>> Calls { get; } = [];
        internal List<Message<byte[], byte[]>> WarmupCalls { get; } = [];
        internal Action<Message<byte[], byte[]>, Action<DeliveryReport<byte[], byte[]>>> OnWarmup { get; set; } = (_, callback) => callback(Report());
        internal List<TimeSpan> FlushBudgets { get; } = [];
        internal Action<Message<byte[], byte[]>, Action<DeliveryReport<byte[], byte[]>>> OnProduce { get; set; } = (_, _) => { };
        internal Action OnFlush { get; set; } = () => { };
        internal Action OnDispose { get; set; } = () => { };
        internal int Disposals { get; private set; }
        internal int FlushAfterKill { get; private set; }
        internal int FlushResult { get; set; }
        private bool killed;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "Produce":
                    var message = (Message<byte[], byte[]>)args![1]!;
                    if (Encoding.UTF8.GetString(message.Headers.GetLastBytes("sequence")) == "-1")
                    {
                        WarmupCalls.Add(message);
                        OnWarmup(message, (Action<DeliveryReport<byte[], byte[]>>)args[2]!);
                        return null;
                    }
                    Calls.Add(message);
                    killed |= Encoding.UTF8.GetString(message.Key).EndsWith("cross_restart", StringComparison.Ordinal);
                    OnProduce(message, (Action<DeliveryReport<byte[], byte[]>>)args[2]!);
                    return null;
                case "Flush":
                    FlushBudgets.Add((TimeSpan)args![0]!);
                    if (killed) FlushAfterKill++;
                    OnFlush();
                    return FlushResult;
                case "Dispose": Disposals++; OnDispose(); return null;
                default: throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
