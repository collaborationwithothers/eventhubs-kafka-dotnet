using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptJournalTests
{
    internal static B10AbruptWire.Identity Identity => new("test", "B10", "C1", "C1", 1, new('a', 40), "example/image@sha256:" + new string('b', 64), "test-c1-r1-loadgen-restart", "first", "uid", "first", null);
    internal static JsonElement Terminal(string attempt = "a") => B10AbruptWire.Element(new { attempt_id = attempt, completion_source = "callback", delivered = true, error_code = 0, persistence_status = 2 });
    internal static JsonElement Started => B10AbruptWire.Element(new { pid = 1, start_time_ticks = 1L, boot_id = "boot" });
    internal static JsonElement NoKill => B10AbruptWire.Element(new { reason = "b10_abrupt_exposure_inconclusive", cross_restart_admitted = false });

    [Fact]
    public async Task Given_TwoWritingThreads_When_RecordsInterleave_Then_All1020LinesAreCompleteAndSequential()
    {
        using var temporary = new Temporary();
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, TimeProvider.System);
        await journal.AppendAsync("child_started", Started, TestContext.Current.CancellationToken);
        await Task.WhenAll(Task.Run(async () =>
        {
            for (int index = 0; index < 1000; index++) await journal.AppendAsync("terminal", Terminal(), TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken), Task.Run(async () =>
        {
            for (int index = 0; index < 19; index++) await journal.AppendAsync("accepted", B10AbruptWire.Element(new { attempt_id = "a", prepared_sequence = 1 }), TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken));
        byte[] bytes = await File.ReadAllBytesAsync(temporary.Path, TestContext.Current.CancellationToken);
        Assert.Equal((byte)10, bytes[^1]);
        string[] lines = Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(1020, lines.Length);
        var frames = lines.Select(line => B10AbruptWire.ReadFrame(Encoding.UTF8.GetBytes(line))).ToArray();
        Assert.Equal(Enumerable.Range(1, 1020).Select(index => (long)index), frames.Select(frame => frame.Sequence));
        Assert.Equal("child_started", frames[0].Kind);
        Assert.Equal(1000, frames.Count(frame => frame.Kind == "terminal"));
    }

    [Theory]
    [InlineData("empty", "malformed_frame")]
    [InlineData("torn", "malformed_frame")]
    [InlineData("malformed", "malformed_frame")]
    [InlineData("skipped", "sequence_gap")]
    public void Given_DamagedJournal_When_Read_Then_CaptureGapIsLatched(string mutation, string reason)
    {
        byte[] first = Line(1, "child_started", Started);
        byte[] second = Line(2, "no_kill", NoKill);
        byte[] bytes = mutation switch
        {
            "empty" => [],
            "torn" => [.. first, .. second[..^1]],
            "malformed" => [.. first, .. Encoding.UTF8.GetBytes("{\n")],
            _ => [.. first, .. second, .. Line(4, "no_kill", NoKill)]
        };
        var snapshot = B10AbruptJournal.Read(bytes, Identity);
        Assert.Contains(reason, snapshot.Gaps);
    }

    [Theory]
    [InlineData("extra")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("unknown-kind")]
    [InlineData("trailing")]
    [InlineData("fractional-sequence")]
    [InlineData("negative-sequence")]
    [InlineData("overflow-sequence")]
    [InlineData("schema-two")]
    public void Given_NonClosedJson_When_Read_Then_Rejects(string mutation)
    {
        string original = Encoding.UTF8.GetString(Line(1, "child_started", Started)).TrimEnd('\n');
        JsonObject json = JsonNode.Parse(original)!.AsObject();
        switch (mutation)
        {
            case "extra": json["extra"] = 1; break;
            case "missing": json["data"]!.AsObject().Remove("pid"); break;
            case "unknown-kind": json["kind"] = "unknown"; break;
            case "fractional-sequence": json["sequence"] = 1.5; break;
            case "negative-sequence": json["sequence"] = -1; break;
            case "overflow-sequence": json["sequence"] = JsonNode.Parse("9223372036854775808"); break;
            case "schema-two": json["schema_version"] = 2; break;
        }
        string text = mutation == "duplicate" ? original.Replace("\"pid\":1", "\"pid\":1,\"pid\":1", StringComparison.Ordinal)
            : json.ToJsonString() + (mutation == "trailing" ? "x" : "");
        Assert.Equal("b10_abrupt_capture_gap:malformed_frame", Assert.Throws<InvalidDataException>(() => B10AbruptWire.ReadFrame(Encoding.UTF8.GetBytes(text))).Message);
        Assert.Contains("malformed_frame", B10AbruptJournal.Read(Encoding.UTF8.GetBytes(text + "\n"), Identity).Gaps);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Given_DuplicateOrSkippedSequence_When_Read_Then_LatchesGap(long sequence) =>
        Assert.Contains("sequence_gap", B10AbruptJournal.Read([.. Line(1, "child_started", Started), .. Line(sequence + 1, "no_kill", NoKill)], Identity).Gaps);

    [Theory]
    [InlineData(65535, true)]
    [InlineData(65536, true)]
    [InlineData(65537, false)]
    public async Task Given_RecordByteBoundary_When_Appended_Then_OnlyAboveBoundLatchesGap(int size, bool valid)
    {
        using var temporary = new Temporary();
        var clock = new FixedClock();
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, clock);
        var empty = Started;
        var frame = new B10AbruptWire.Frame(Identity, 1, clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture), 0, clock.TimestampFrequency, "child_started", empty);
        int padding = size - B10AbruptWire.WriteFrame(frame).Length;
        var data = B10AbruptWire.Element(new { pid = 1, start_time_ticks = 1L, boot_id = "boot" + new string('x', padding) });
        Assert.Equal(size, B10AbruptWire.WriteFrame(frame with { Data = data }).Length);
        if (valid)
        {
            await journal.AppendAsync("child_started", data, TestContext.Current.CancellationToken);
            Assert.Equal(size + 1, new FileInfo(temporary.Path).Length);
        }
        else
        {
            Assert.Equal("b10_abrupt_capture_gap:frame_oversize", (await Assert.ThrowsAsync<InvalidDataException>(() => journal.AppendAsync("child_started", data, TestContext.Current.CancellationToken))).Message);
            Assert.Contains("frame_oversize", journal.Gaps);
            Assert.Equal(0, new FileInfo(temporary.Path).Length);
        }
    }

    [Theory]
    [InlineData(16777215, true)]
    [InlineData(16777216, true)]
    [InlineData(16777217, false)]
    public void Given_JournalByteBoundary_When_Admit_Then_ChargesBeforeWrite(long total, bool valid) => Assert.Equal(valid, B10AbruptJournal.FitsByteBudget(total - 1, 1));

    [Fact]
    public async Task Given_JournalBytesAtLimit_When_AnotherRecordArrives_Then_OnlyAboveLimitLatchesGap()
    {
        using var temporary = new Temporary();
        var clock = new FixedClock();
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, clock);
        await journal.AppendAsync("child_started", Started, TestContext.Current.CancellationToken);
        while (true)
        {
            long remaining = B10AbruptJournal.MaximumBytes - new FileInfo(temporary.Path).Length;
            if (remaining == 0) break;
            int length = (int)Math.Min(65536, remaining - 1);
            long sequence = journal.Frames.Count + 1L;
            var data = B10AbruptWire.Element(new { attempt_id = "a", prepared_sequence = 1, error_code = -1, error_reason = "x" });
            var frame = new B10AbruptWire.Frame(Identity, sequence, clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture), 0, clock.TimestampFrequency, "synchronous_rejection", data);
            int padding = length - B10AbruptWire.WriteFrame(frame).Length;
            Assert.True(padding >= 0);
            data = B10AbruptWire.Element(new { attempt_id = "a", prepared_sequence = 1, error_code = -1, error_reason = "x" + new string('x', padding) });
            await journal.AppendAsync("synchronous_rejection", data, TestContext.Current.CancellationToken);
            Assert.Empty(journal.Gaps);
        }
        Assert.Equal(16777216, new FileInfo(temporary.Path).Length);
        Assert.Equal("b10_abrupt_capture_gap:journal_bytes_exceeded", (await Assert.ThrowsAsync<InvalidDataException>(() => journal.AppendAsync("no_kill", NoKill, TestContext.Current.CancellationToken))).Message);
        Assert.Equal(["journal_bytes_exceeded"], journal.Gaps);
        Assert.Equal(16777216, new FileInfo(temporary.Path).Length);
    }

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public async Task Given_TerminalQueueBoundary_When_Capture_Then_OverflowIsSticky(int count, bool valid)
    {
        using var temporary = new Temporary();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, TimeProvider.System,
            async token => { reached.TrySetResult(); await release.Task.WaitAsync(token); });
        Task<B10AbruptWire.Frame> start = Task.Run(() => journal.AppendAsync("child_started", Started, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        await reached.Task;
        long length = new FileInfo(temporary.Path).Length;
        bool accepted = true;
        for (int index = 0; index < count; index++) accepted = journal.TryCaptureTerminal(Terminal());
        Assert.Equal(valid, accepted);
        Assert.Equal(length, new FileInfo(temporary.Path).Length);
        release.TrySetResult(); await start;
        await journal.DrainAvailable(TestContext.Current.CancellationToken);
        if (!valid)
        {
            Assert.Equal(["queue_full"], journal.Gaps);
            Assert.False(journal.TryCaptureTerminal(Terminal()));
            await Assert.ThrowsAsync<InvalidDataException>(() => journal.SealAsync(TestContext.Current.CancellationToken));
        }
        else
        {
            Assert.Empty(journal.Gaps);
            Assert.Equal(count, journal.Frames.Count(frame => frame.Kind == "terminal"));
        }
    }

    [Fact]
    public async Task Given_WarmupCallbackWhileWriterBlocked_When_Captured_Then_OnlyQueueIsTouched()
    {
        using var temporary = new Temporary();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, TimeProvider.System,
            async token => { reached.TrySetResult(); await release.Task.WaitAsync(token); });
        var start = Task.Run(() => journal.AppendAsync("child_started", Started, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        await reached.Task;
        long length = new FileInfo(temporary.Path).Length;
        bool captured = journal.TryCaptureWarmupTerminal(WarmupTerminal("same_instance"));
        Assert.True(captured);
        Assert.Equal(length, new FileInfo(temporary.Path).Length);
        release.TrySetResult(); await start;
        await journal.DrainAvailable(TestContext.Current.CancellationToken);
        Assert.Single(journal.Frames, frame => frame.Kind == "warmup_terminal");
        Assert.Empty(journal.Gaps);
    }

    [Fact]
    public async Task Given_JournalEntryBoundary_When_Append_Then_4097thEntryCannotBecomeClean()
    {
        using var temporary = new Temporary();
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, TimeProvider.System);
        await journal.AppendAsync("child_started", Started, TestContext.Current.CancellationToken);
        for (int index = 0; index < 4094; index++) await journal.AppendAsync("terminal", Terminal(), TestContext.Current.CancellationToken);
        Assert.Equal(4095, journal.Frames.Count);
        await journal.AppendAsync("terminal", Terminal(), TestContext.Current.CancellationToken);
        Assert.Equal(4096, journal.Frames.Count);
        Assert.Equal("b10_abrupt_capture_gap:journal_entries_exceeded", (await Assert.ThrowsAsync<InvalidDataException>(() => journal.AppendAsync("terminal", Terminal(), TestContext.Current.CancellationToken))).Message);
        Assert.Equal(4096, journal.Frames.Count);
        await Assert.ThrowsAsync<InvalidDataException>(() => journal.SealAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_DurableAppend_When_Returns_Then_ReadbackContainsExactClosedFrame()
    {
        using var temporary = new Temporary();
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, TimeProvider.System);
        var frame = await journal.AppendAsync("child_started", Started, TestContext.Current.CancellationToken);
        Assert.Equal(1, frame.Sequence);
        byte[] bytes = await File.ReadAllBytesAsync(temporary.Path, TestContext.Current.CancellationToken);
        Assert.Equal(10, bytes[^1]);
        var read = B10AbruptWire.ReadFrame(bytes.AsMemory(0, bytes.Length - 1));
        Assert.Equal(frame.Identity, read.Identity);
        Assert.Equal(frame.Data.GetRawText(), read.Data.GetRawText());
        var descriptor = await journal.SealAsync(TestContext.Current.CancellationToken);
        B10AbruptWire.Reconcile(descriptor, bytes);
        bytes[0] ^= 1;
        Assert.Equal("b10_abrupt_publication_failed", Assert.Throws<InvalidDataException>(() => B10AbruptWire.Reconcile(descriptor, bytes)).Message);
    }

    [Fact]
    public async Task Given_FlushHeld_When_Append_Then_NoDurableAcknowledgementBeforeFlush()
    {
        using var temporary = new Temporary();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, TimeProvider.System, async token =>
        { reached.SetResult(); await release.Task.WaitAsync(token); });
        var append = Task.Run(() => journal.AppendAsync("child_started", Started, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        await reached.Task;
        Assert.False(append.IsCompleted);
        var pendingFrames = (List<B10AbruptWire.Frame>)typeof(B10AbruptJournal).GetField("frames", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(journal)!;
        Assert.Empty(pendingFrames);
        release.SetResult();
        Assert.Equal(1, (await append).Sequence);
    }

    [Fact]
    public async Task Given_WriterFailure_When_Append_Then_GapCannotBeClearedByLaterRecord()
    {
        using var temporary = new Temporary();
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, TimeProvider.System, _ => Task.FromException(new IOException("synthetic flush failure")));
        Assert.Equal("b10_abrupt_capture_gap:writer_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => journal.AppendAsync("child_started", Started, TestContext.Current.CancellationToken))).Message);
        Assert.Equal(["writer_failed"], journal.Gaps);
        Assert.Empty(journal.Frames);
        await Assert.ThrowsAsync<InvalidDataException>(() => journal.AppendAsync("child_started", Started, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => journal.SealAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_ProductionJournalWithPreparedOnlyCrossOriginal_When_Read_Then_CaptureBlocksSuccessor()
    {
        await using var fixture = new B10AbruptProducerTests.Fixture(diagnosticIdentity: Identity);
        fixture.ConfigureControl(); fixture.StartReports();
        await Assert.ThrowsAsync<B10AbruptProducerTests.SimulatedDeathException>(() => fixture.Runner.RunFirstAsync(B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options, TestContext.Current.CancellationToken));
        var valid = fixture.Journal.Frames;
        AssertUsable(valid);
        string attempt = valid.Single(frame => frame.Kind == "prepared" && frame.Data.GetProperty("arm").GetString() == "cross_restart" && frame.Data.GetProperty("ordinal").GetInt32() == 1).Data.GetProperty("attempt_id").GetString()!;
        var snapshot = ReadReindexed(valid.Where(frame => frame.Kind != "accepted" || frame.Data.GetProperty("attempt_id").GetString() != attempt));
        Assert.Contains("malformed_frame", snapshot.Gaps);
        Assert.Empty(snapshot.Failures);
        Assert.False(Classify(snapshot).SuccessorPermitted);
    }

    [Theory]
    [InlineData("same_instance-original-0")]
    [InlineData("same_instance-original-1")]
    [InlineData("same_instance-original-2")]
    [InlineData("same_instance-original-3")]
    [InlineData("same_instance-original-4")]
    [InlineData("same_instance-resend-4")]
    [InlineData("cross_restart-original-0")]
    [InlineData("cross_restart-original-1")]
    [InlineData("cross_restart-original-2")]
    [InlineData("cross_restart-original-3")]
    [InlineData("cross_restart-original-4")]
    public void Given_CompleteJournalWithOneAcceptanceRemoved_When_Read_Then_AmbiguousAttemptBlocksSuccessor(string attempt)
    {
        var valid = CompleteJournal();
        AssertUsable(valid);
        var snapshot = ReadReindexed(valid.Where(frame => frame.Kind != "accepted" || frame.Data.GetProperty("attempt_id").GetString() != attempt));
        Assert.Contains("malformed_frame", snapshot.Gaps);
        Assert.False(Classify(snapshot).SuccessorPermitted);
    }

    [Theory]
    [InlineData("all-control")]
    [InlineData("cut")]
    [InlineData("empty-cut")]
    [InlineData("resend")]
    [InlineData("early-resend")]
    [InlineData("original-terminal")]
    [InlineData("resend-terminal")]
    [InlineData("control_drained")]
    [InlineData("duplicate-drain")]
    [InlineData("flush-false")]
    [InlineData("outstanding")]
    [InlineData("post_drain_report")]
    [InlineData("late-control-terminal")]
    [InlineData("late-control-report")]
    public void Given_IncompleteControlWithOtherwiseValidKill_When_Read_Then_CaptureBlocksSuccessor(string fault)
    {
        var valid = CompleteJournal();
        AssertUsable(valid);
        var changed = valid.Where(frame => fault switch
        {
            "all-control" => !IsControl(frame),
            "cut" or "control_drained" or "post_drain_report" => frame.Kind != fault,
            "resend" => !frame.Data.TryGetProperty("attempt_id", out var id) || id.GetString() != "same_instance-resend-4",
            "original-terminal" => frame.Kind != "terminal" || frame.Data.GetProperty("attempt_id").GetString() != "same_instance-original-4",
            "resend-terminal" => frame.Kind != "terminal" || frame.Data.GetProperty("attempt_id").GetString() != "same_instance-resend-4",
            _ => true
        }).ToList();
        if (fault == "empty-cut")
        {
            var terminal = changed.Single(frame => frame.Kind == "terminal" && frame.Data.GetProperty("attempt_id").GetString() == "same_instance-original-4");
            changed.Remove(terminal); changed.Insert(changed.FindIndex(frame => frame.Kind == "cut"), terminal);
            int cut = changed.FindIndex(frame => frame.Kind == "cut");
            JsonObject data = JsonNode.Parse(changed[cut].Data.GetRawText())!.AsObject(); data["unconfirmed_attempt_ids"] = new JsonArray();
            changed[cut] = changed[cut] with { Data = B10AbruptWire.Element(data) };
            changed.RemoveAll(frame => frame.Data.TryGetProperty("attempt_id", out var id) && id.GetString() == "same_instance-resend-4");
        }
        if (fault == "early-resend")
        {
            int resend = changed.FindIndex(frame => frame.Kind == "prepared" && frame.Data.GetProperty("role").GetString() == "resend");
            changed[resend] = changed[resend] with { ObservedUtc = "2026-11-07T00:00:02.0000000+00:00" };
        }
        if (fault == "duplicate-drain") changed.Insert(changed.FindIndex(frame => frame.Kind == "control_drained"), changed.Single(frame => frame.Kind == "control_drained"));
        if (fault is "flush-false" or "outstanding")
        {
            int drain = changed.FindIndex(frame => frame.Kind == "control_drained");
            changed[drain] = changed[drain] with { Data = B10AbruptWire.Element(new { flush_returned = fault != "flush-false", out_queue_length = fault == "outstanding" ? 1 : 0 }) };
        }
        if (fault is "late-control-terminal" or "late-control-report")
        {
            var late = changed.Single(frame => fault == "late-control-report" ? frame.Kind == "post_drain_report" : frame.Kind == "terminal" && frame.Data.GetProperty("attempt_id").GetString() == "same_instance-original-4");
            changed.Remove(late);
            int position = fault == "late-control-report"
                ? changed.FindIndex(frame => frame.Kind == "prepared" && frame.Data.GetProperty("arm").GetString() == "cross_restart") + 1
                : changed.FindIndex(frame => frame.Kind == "kill_intent");
            changed.Insert(position, late);
        }
        var snapshot = ReadReindexed(changed);
        Assert.Contains("malformed_frame", snapshot.Gaps);
        Assert.False(Classify(snapshot).SuccessorPermitted);
    }

    [Theory]
    [InlineData("same_instance-original-1")]
    [InlineData("same_instance-resend-4")]
    public void Given_CompleteControlWithSynchronousRejection_When_Read_Then_RejectionCountsAsTerminal(string attempt)
    {
        var frames = CompleteJournal().Where(frame => frame.Kind != "terminal" || frame.Data.GetProperty("attempt_id").GetString() != attempt).Select(frame =>
            frame.Kind == "accepted" && frame.Data.GetProperty("attempt_id").GetString() == attempt
                ? frame with { Kind = "synchronous_rejection", Data = B10AbruptWire.Element(new { attempt_id = attempt, prepared_sequence = frame.Data.GetProperty("prepared_sequence").GetInt64(), error_code = -184, error_reason = "synthetic rejection" }) }
                : frame);
        AssertUsable(frames);
    }

    [Fact]
    public void Given_AcceptedControlPrefixWithoutBoundary_When_Read_Then_NoCompletionRequirementIsInvented()
    {
        var prefix = CompleteJournal().TakeWhile(frame => frame.Kind != "cut");
        var snapshot = ReadReindexed(prefix);
        Assert.Empty(snapshot.Gaps); Assert.Empty(snapshot.Failures);
        Assert.False(Classify(snapshot).SuccessorPermitted);
    }

    [Fact]
    public void Given_CompleteWarmups_When_Read_Then_WarmupsNeverEnterUnconfirmedSets()
    {
        var frames = CompleteJournal();
        var snapshot = ReadReindexed(frames);
        AssertUsable(frames);
        var originals = frames.Where(frame => frame.Kind is not ("warmup_prepared" or "warmup_terminal")).ToArray();
        Assert.Equal(B10AbruptJournal.Unconfirmed(originals.TakeWhile(frame => frame.Kind != "cut"), "same_instance"), snapshot.ControlUnconfirmedAttemptIds);
        Assert.Equal(B10AbruptJournal.Unconfirmed(originals, "cross_restart"), snapshot.KillUnconfirmedAttemptIds);
    }

    [Theory]
    [InlineData("missing-cross-prepared")]
    [InlineData("terminal-after-original")]
    [InlineData("duplicate-prepared")]
    [InlineData("duplicate-terminal")]
    [InlineData("terminal-before-prepared")]
    [InlineData("wrong-attempt")]
    [InlineData("wrong-key")]
    [InlineData("early-cross-warmup")]
    [InlineData("failed-terminal")]
    [InlineData("nonzero-terminal")]
    [InlineData("invalid-status")]
    [InlineData("null-status")]
    public void Given_InvalidWarmupOrderOrDelivery_When_OriginalExists_Then_MalformedFrame(string fault)
    {
        var frames = CompleteJournal();
        int prepared = frames.FindIndex(frame => frame.Kind == "warmup_prepared");
        int terminal = frames.FindIndex(frame => frame.Kind == "warmup_terminal");
        switch (fault)
        {
            case "missing-cross-prepared": frames.RemoveAll(frame => frame.Kind == "warmup_prepared" && frame.Data.GetProperty("arm").GetString() == "cross_restart"); break;
            case "terminal-after-original":
                var late = frames[terminal]; frames.RemoveAt(terminal); frames.Insert(frames.FindIndex(frame => frame.Kind == "prepared") + 1, late); break;
            case "duplicate-prepared": frames.Insert(prepared, frames[prepared]); break;
            case "duplicate-terminal": frames.Insert(terminal, frames[terminal]); break;
            case "terminal-before-prepared": (frames[prepared], frames[terminal]) = (frames[terminal], frames[prepared]); break;
            case "early-cross-warmup":
                var cross = frames.Single(frame => frame.Kind == "warmup_prepared" && frame.Data.GetProperty("arm").GetString() == "cross_restart");
                frames.Remove(cross); frames.Insert(1, cross); break;
            default:
                int position = fault is "wrong-attempt" or "wrong-key" ? prepared : terminal;
                JsonObject data = JsonNode.Parse(frames[position].Data.GetRawText())!.AsObject();
                switch (fault)
                {
                    case "wrong-attempt": data["attempt_id"] = "another-warmup"; break;
                    case "wrong-key": data["key"] = "another-key"; break;
                    case "failed-terminal": data["delivered"] = false; data["error_code"] = -192; break;
                    case "nonzero-terminal": data["error_code"] = -192; break;
                    case "invalid-status": data["persistence_status"] = 1; break;
                    case "null-status": data["persistence_status"] = null; break;
                }
                frames[position] = frames[position] with { Data = B10AbruptWire.Element(data) }; break;
        }
        Assert.Contains("malformed_frame", ReadReindexed(frames).Gaps);
    }

    [Theory]
    [InlineData("attempt", false)]
    [InlineData("partition", false)]
    [InlineData("before", false)]
    [InlineData("equal", false)]
    [InlineData("one-tick-after", true)]
    public void Given_WarmupReportBinding_When_Read_Then_RequiresMatchingFieldsAndStrictUtcOrder(string fault, bool valid)
    {
        var frames = CompleteJournal();
        int report = frames.FindIndex(frame => frame.Kind == "pre_acceptance_report");
        var terminal = frames.First(frame => frame.Kind == "warmup_terminal");
        JsonObject data = JsonNode.Parse(frames[report].Data.GetRawText())!.AsObject();
        if (fault == "attempt") data["warmup_attempt_id"] = "another-warmup";
        else if (fault == "partition") data["partition"] = 1;
        else data["report_capture_utc"] = DateTimeOffset.ParseExact(terminal.ObservedUtc, "O", CultureInfo.InvariantCulture)
            .AddTicks(fault == "before" ? -1 : fault == "equal" ? 0 : 1).ToString("O", CultureInfo.InvariantCulture);
        frames[report] = frames[report] with { Data = B10AbruptWire.Element(data) };
        var snapshot = ReadReindexed(frames);
        Assert.Empty(snapshot.Gaps);
        if (valid) Assert.Empty(snapshot.Failures);
        else Assert.Equal("b10_abrupt_report_binding_invalid", Assert.Single(snapshot.Failures).Value);
    }

    [Theory]
    [InlineData("failed-control")]
    [InlineData("missing-terminal")]
    [InlineData("late-terminal")]
    [InlineData("failed-cross")]
    [InlineData("no-cross")]
    [InlineData("leader-not-up")]
    [InlineData("sample-missing")]
    public void Given_NoKillBeforeArmOriginal_When_Read_Then_WarmupNeedsNoSuccessfulCompletion(string ending)
    {
        var complete = CompleteJournal();
        bool cross = ending is "failed-cross" or "no-cross";
        string arm = cross ? "cross_restart" : "same_instance";
        var frames = cross
            ? complete.TakeWhile(frame => frame.Kind != "warmup_prepared" || frame.Data.GetProperty("arm").GetString() != "cross_restart").ToList()
            : complete.Take(1).ToList();
        var template = complete.First(frame => frame.Kind == "warmup_prepared" && frame.Data.GetProperty("arm").GetString() == arm);
        if (ending != "no-cross") frames.Add(template);
        if (ending is "failed-control" or "failed-cross" or "leader-not-up" or "sample-missing")
            frames.Add(template with { Kind = "warmup_terminal", Data = WarmupTerminal(arm, delivered: ending is "leader-not-up" or "sample-missing") });
        string reason = ending == "leader-not-up" ? "b10_abrupt_leader_not_up" : ending == "sample-missing" ? "b10_abrupt_sample_missing" : "b10_abrupt_warmup_failed";
        frames.Add(template with { Kind = "no_kill", Data = B10AbruptWire.Element(new { reason, cross_restart_admitted = false }) });
        if (ending == "late-terminal") frames.Add(template with { Kind = "warmup_terminal", Data = WarmupTerminal(arm) });
        var snapshot = ReadReindexed(frames);
        Assert.Empty(snapshot.Gaps); Assert.Empty(snapshot.Failures);
    }

    [Fact]
    public void Given_DeliveredWarmupWith64BitOffset_When_Read_Then_AcceptsWithoutNarrowing()
    {
        var frames = CompleteJournal();
        int index = frames.FindIndex(frame => frame.Kind == "warmup_terminal");
        frames[index] = frames[index] with { Data = WarmupTerminal("same_instance", offset: 4294967296) };
        AssertUsable(frames);
    }

    [Theory]
    [InlineData("partition", "-2147483649", false)]
    [InlineData("partition", "-2147483648", true)]
    [InlineData("partition", "2147483647", true)]
    [InlineData("partition", "2147483648", false)]
    [InlineData("offset", "-9223372036854775809", false)]
    [InlineData("offset", "-9223372036854775808", true)]
    [InlineData("offset", "9223372036854775807", true)]
    [InlineData("offset", "9223372036854775808", false)]
    [InlineData("offset", "1.5", false)]
    public void Given_WarmupNumericBoundary_When_Read_Then_EnforcesSignedFieldWidth(string field, string value, bool valid)
    {
        JsonObject data = JsonNode.Parse(WarmupTerminal("same_instance", delivered: false).GetRawText())!.AsObject();
        data[field] = JsonNode.Parse(value);
        var frame = Line(1, "warmup_terminal", B10AbruptWire.Element(data));
        if (valid) Assert.Equal("warmup_terminal", B10AbruptWire.ReadFrame(frame.AsMemory(0, frame.Length - 1)).Kind);
        else Assert.Equal("b10_abrupt_capture_gap:malformed_frame", Assert.Throws<InvalidDataException>(() => B10AbruptWire.ReadFrame(frame.AsMemory(0, frame.Length - 1))).Message);
    }

    [Theory]
    [InlineData("partition", -1, false)]
    [InlineData("partition", 0, true)]
    [InlineData("partition", 1, true)]
    [InlineData("offset", -1, false)]
    [InlineData("offset", 0, true)]
    [InlineData("offset", 1, true)]
    public void Given_DeliveredWarmupCoordinate_When_Read_Then_RequiresNonnegativeValue(string field, int value, bool valid)
    {
        JsonObject data = JsonNode.Parse(WarmupTerminal("same_instance").GetRawText())!.AsObject();
        data[field] = value;
        var frame = Line(1, "warmup_terminal", B10AbruptWire.Element(data));
        if (valid) Assert.Equal("warmup_terminal", B10AbruptWire.ReadFrame(frame.AsMemory(0, frame.Length - 1)).Kind);
        else Assert.Equal("b10_abrupt_capture_gap:malformed_frame", Assert.Throws<InvalidDataException>(() => B10AbruptWire.ReadFrame(frame.AsMemory(0, frame.Length - 1))).Message);
    }

    private static bool IsControl(B10AbruptWire.Frame frame) => frame.Kind is "cut" or "control_drained" or "post_drain_report"
        || frame.Data.TryGetProperty("arm", out var arm) && arm.GetString() == "same_instance"
        || frame.Data.TryGetProperty("attempt_id", out var attempt) && attempt.GetString()!.StartsWith("same_instance", StringComparison.Ordinal);
    private static void AssertUsable(IEnumerable<B10AbruptWire.Frame> frames)
    {
        var snapshot = ReadReindexed(frames);
        Assert.Empty(snapshot.Gaps); Assert.Empty(snapshot.Failures); Assert.True(Classify(snapshot).SuccessorPermitted);
    }
    private static B10AbruptParent.Classification Classify(B10AbruptJournal.Snapshot snapshot)
    {
        var group = new B10AbruptCgroup.Observation("boot", "/synthetic", "inode", 0);
        var started = snapshot.Frames[0].Data;
        return B10AbruptParent.Classify(snapshot, true, 137, new(started.GetProperty("pid").GetInt32(), started.GetProperty("boot_id").GetString()!, started.GetProperty("start_time_ticks").GetInt64()), group, group);
    }
    private static B10AbruptJournal.Snapshot ReadReindexed(IEnumerable<B10AbruptWire.Frame> source)
    {
        var frames = source.ToArray();
        var prepared = frames.Select((frame, index) => (frame, Sequence: index + 1L)).Where(item => item.frame.Kind == "prepared")
            .ToDictionary(item => item.frame.Data.GetProperty("attempt_id").GetString()!, item => item.Sequence, StringComparer.Ordinal);
        byte[] bytes = frames.SelectMany((frame, index) =>
        {
            JsonObject data = JsonNode.Parse(frame.Data.GetRawText())!.AsObject();
            if (data.ContainsKey("prepared_sequence")) data["prepared_sequence"] = prepared[data["attempt_id"]!.GetValue<string>()];
            return B10AbruptWire.WriteFrame(frame with { Sequence = index + 1L, Data = B10AbruptWire.Element(data) }).Append((byte)10);
        }).ToArray();
        return B10AbruptJournal.Read(bytes, Identity);
    }
    private static List<B10AbruptWire.Frame> CompleteJournal()
    {
        List<B10AbruptWire.Frame> frames = [];
        string observed = "2026-11-07T00:00:02.0000000+00:00";
        void Add(string kind, object data) => frames.Add(new(Identity, frames.Count + 1L, observed, frames.Count, TimeSpan.TicksPerSecond, kind, B10AbruptWire.Element(data)));
        void Prepare(string arm, int ordinal, bool resend = false)
        {
            string attempt = $"{arm}-{(resend ? "resend" : "original")}-{ordinal}";
            Add("prepared", new { attempt_id = attempt, arm, role = resend ? "resend" : "original", ordinal, probe_id = $"B10/test/C1/1/{arm}", key = $"B10/test/C1/1/{arm}", sequence = ordinal, payload_length = 0, payload_sha256 = new string('a', 64), predecessor_attempt_id = resend ? $"{arm}-original-{ordinal}" : null });
            long sequence = frames.Count;
            if (ordinal == 0 && !resend) Report(arm, attempt, false);
            Add("accepted", new { attempt_id = attempt, prepared_sequence = sequence });
        }
        void Report(string arm, string? attempt, bool post)
        {
            var data = B10AbruptWire.Element(new
            {
            arm, attempt_id = attempt, name = post ? "control-post-drain-report.json" : $"{arm}-pre-acceptance-report.json", length = 1, sha256 = new string('a', 64),
            report_identity = new { scenario_id = "B10", run_id = "test", role = "LoadGenProducer", client_id = "first" }, report_sequence = 1,
            report_capture_utc = post ? observed : DateTimeOffset.ParseExact(observed, "O", CultureInfo.InvariantCulture).AddTicks(-1).ToString("O", CultureInfo.InvariantCulture)
            });
            if (!post)
            {
                JsonObject fields = JsonNode.Parse(data.GetRawText())!.AsObject();
                fields["warmup_attempt_id"] = $"first-{arm}-warmup"; fields["partition"] = 0;
                data = B10AbruptWire.Element(fields);
            }
            Add(post ? "post_drain_report" : "pre_acceptance_report", data);
        }
        void Warmup(string arm)
        {
            string originalObserved = observed;
            observed = DateTimeOffset.ParseExact(observed, "O", CultureInfo.InvariantCulture).AddTicks(-2).ToString("O", CultureInfo.InvariantCulture);
            Add("warmup_prepared", WarmupPrepared(arm)); Add("warmup_terminal", WarmupTerminal(arm));
            observed = originalObserved;
        }
        Add("child_started", new { pid = 1, start_time_ticks = 1L, boot_id = "boot" });
        Warmup("same_instance");
        for (int ordinal = 0; ordinal < 5; ordinal++) { Prepare("same_instance", ordinal); if (ordinal < 4) Add("terminal", Terminal($"same_instance-original-{ordinal}")); }
        Add("cut", new { arm = "same_instance", cut_attempt_id = "same_instance-original-4", release_utc = "2026-11-07T00:02:02.0000000+00:00", unconfirmed_attempt_ids = (string[])["same_instance-original-4"] });
        observed = "2026-11-07T00:02:02.0000000+00:00";
        Prepare("same_instance", 4, true);
        Add("terminal", Terminal("same_instance-original-4")); Add("terminal", Terminal("same_instance-resend-4"));
        Add("control_drained", new { flush_returned = true, out_queue_length = 0 }); Report("same_instance", null, true);
        Warmup("cross_restart");
        for (int ordinal = 0; ordinal < 5; ordinal++) Prepare("cross_restart", ordinal);
        Add("kill_intent", new { arm = "cross_restart", cut_attempt_id = "cross_restart-original-4", release_utc = "2026-11-07T00:04:02.0000000+00:00" });
        return frames;
    }

    private static JsonElement WarmupPrepared(string arm) => B10AbruptWire.Element(new
    { attempt_id = $"first-{arm}-warmup", arm, key = $"B10/test/C1/1/{arm}", payload_length = 0, payload_sha256 = new string('a', 64) });
    private static JsonElement WarmupTerminal(string arm, bool delivered = true, long offset = 1) => B10AbruptWire.Element(new
    { attempt_id = $"first-{arm}-warmup", completion_source = "callback", delivered, error_code = delivered ? 0 : -192, persistence_status = 2, partition = 0, offset });

    internal static byte[] Line(long sequence, string kind, JsonElement data) => [.. B10AbruptWire.WriteFrame(new(Identity, sequence, "2026-11-07T00:00:00.0000000+00:00", 0, TimeSpan.TicksPerSecond, kind, data)), 10];
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        public override long GetTimestamp() => 0;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    }
    internal sealed class Temporary : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        internal Temporary() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "journal.jsonl"); }
        internal string Path { get; }
        public void Dispose() => Directory.Delete(directory, true);
    }
}
