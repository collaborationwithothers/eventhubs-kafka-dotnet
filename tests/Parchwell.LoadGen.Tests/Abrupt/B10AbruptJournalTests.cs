using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
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
