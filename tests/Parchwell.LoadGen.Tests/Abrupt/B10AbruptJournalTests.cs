using System.Buffers.Binary;
using System.Text;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

public sealed class B10AbruptJournalTests
{
    internal static B10AbruptWire.Identity Identity => new("test", "B10", "C1", "C1", 1, new('a', 40), "example/image@sha256:" + new string('b', 64), "test-c1-r1-loadgen", "first", "uid", "first", null);

    [Theory]
    [InlineData(65535)]
    [InlineData(65536)]
    public async Task Given_BoundedBody_When_Read_Then_AcceptsExactlyPrefixLength(int size)
    {
        var empty = "{\"kind\":\"hello\",\"sequence\":1,\"data\":{\"challenge\":\"\",\"pid\":1,\"start_time_ticks\":1,\"boot_id\":\"\"}}";
        var body = Encoding.UTF8.GetBytes(empty.Replace("\"boot_id\":\"\"", "\"boot_id\":\"" + new string('x', size - Encoding.UTF8.GetByteCount(empty)) + "\"", StringComparison.Ordinal));
        Assert.Equal(size, body.Length);
        using var stream = Framed(body);
        Assert.Equal("hello", (await B10AbruptWire.ReadMessageAsync(stream, 1, TestContext.Current.CancellationToken))!.Kind);
        Assert.Null(await B10AbruptWire.ReadMessageAsync(stream, 2, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(65537, "frame_oversize")]
    [InlineData(0, "malformed_frame")]
    [InlineData(uint.MaxValue, "frame_oversize")]
    public async Task Given_InvalidLength_When_Read_Then_RejectsBeforeBodyAllocation(uint size, string reason)
    {
        var header = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(header, size);
        using var stream = new MemoryStream(header);
        Assert.Equal("b10_abrupt_capture_gap:" + reason, (await Assert.ThrowsAsync<InvalidDataException>(() => B10AbruptWire.ReadMessageAsync(stream, 1, TestContext.Current.CancellationToken))).Message);
        Assert.Equal(4, stream.Position);
    }

    [Theory]
    [InlineData(new byte[] { 0 })]
    [InlineData(new byte[] { 0, 0, 0 })]
    [InlineData(new byte[] { 0, 0, 0, 2, 123 })]
    public async Task Given_PartialTransport_When_Eof_Then_Fails(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        Assert.Equal("b10_abrupt_capture_gap:malformed_frame", (await Assert.ThrowsAsync<InvalidDataException>(() => B10AbruptWire.ReadMessageAsync(stream, 1, TestContext.Current.CancellationToken))).Message);
    }

    [Fact]
    public async Task Given_EmptyTransport_When_Read_Then_ObservesEof()
    {
        using var stream = new MemoryStream();
        Assert.Null(await B10AbruptWire.ReadMessageAsync(stream, 1, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("{\"kind\":\"ack\",\"sequence\":1,\"data\":{\"acknowledged_sequence\":1},\"extra\":1}")]
    [InlineData("{\"kind\":\"ack\",\"kind\":\"ack\",\"sequence\":1,\"data\":{\"acknowledged_sequence\":1}}")]
    [InlineData("{\"kind\":\"ack\",\"sequence\":1,\"data\":{}}")]
    [InlineData("{\"kind\":\"unknown\",\"sequence\":1,\"data\":{}}")]
    [InlineData("{\"kind\":\"ack\",\"sequence\":1,\"data\":{\"acknowledged_sequence\":1}}x")]
    [InlineData("{\"kind\":\"ack\",\"sequence\":1.5,\"data\":{\"acknowledged_sequence\":1}}")]
    [InlineData("{\"kind\":\"ack\",\"sequence\":-1,\"data\":{\"acknowledged_sequence\":1}}")]
    [InlineData("{\"kind\":\"ack\",\"sequence\":9223372036854775808,\"data\":{\"acknowledged_sequence\":1}}")]
    public async Task Given_NonClosedJson_When_Read_Then_Rejects(string json)
    {
        using var stream = Framed(Encoding.UTF8.GetBytes(json));
        Assert.Equal("b10_abrupt_capture_gap:malformed_frame", (await Assert.ThrowsAsync<InvalidDataException>(() => B10AbruptWire.ReadMessageAsync(stream, 1, TestContext.Current.CancellationToken))).Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Given_DuplicateOrSkippedSequence_When_Read_Then_LatchesGap(long sequence)
    {
        using var stream = Framed(Encoding.UTF8.GetBytes($"{{\"kind\":\"ack\",\"sequence\":{sequence},\"data\":{{\"acknowledged_sequence\":1}}}}"));
        Assert.Equal("b10_abrupt_capture_gap:sequence_gap", (await Assert.ThrowsAsync<InvalidDataException>(() => B10AbruptWire.ReadMessageAsync(stream, 1, TestContext.Current.CancellationToken))).Message);
    }

    [Theory]
    [InlineData(16777215, true)]
    [InlineData(16777216, true)]
    [InlineData(16777217, false)]
    public void Given_JournalByteBoundary_When_Admit_Then_ChargesBeforeWrite(long total, bool valid) => Assert.Equal(valid, B10AbruptJournal.FitsByteBudget(total - 1, 1));

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public async Task Given_TerminalQueueBoundary_When_Capture_Then_OverflowIsSticky(int count, bool valid)
    {
        using var temporary = new Temporary();
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, TimeProvider.System);
        var terminal = B10AbruptWire.Element(new { attempt_id = "a", completion_source = "delivery_report", delivered = true, error_code = 0, persistence_status = "Persisted" });
        bool accepted = true;
        for (int index = 0; index < count; index++) accepted = journal.TryCaptureTerminal(terminal);
        Assert.Equal(valid, accepted);
        if (!valid)
        {
            Assert.Equal(["queue_full"], journal.Gaps);
            Assert.False(journal.TryCaptureTerminal(terminal));
            await Assert.ThrowsAsync<InvalidDataException>(() => journal.SealAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Given_JournalEntryBoundary_When_Append_Then_4097thEntryCannotBecomeClean()
    {
        using var temporary = new Temporary();
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, TimeProvider.System);
        var data = B10AbruptWire.Element(new { attempt_id = "a", prepared_sequence = 1 });
        for (int index = 0; index < 4095; index++) await journal.AppendAcceptedAndFlushAsync(data, TestContext.Current.CancellationToken);
        Assert.Equal(4095, journal.Frames.Count);
        await journal.AppendAcceptedAndFlushAsync(data, TestContext.Current.CancellationToken);
        Assert.Equal(4096, journal.Frames.Count);
        Assert.Equal("b10_abrupt_capture_gap:journal_entries_exceeded", (await Assert.ThrowsAsync<InvalidDataException>(() => journal.AppendAcceptedAndFlushAsync(data, TestContext.Current.CancellationToken))).Message);
        Assert.Equal(4096, journal.Frames.Count);
        await Assert.ThrowsAsync<InvalidDataException>(() => journal.SealAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_DurableAppend_When_Returns_Then_ReadbackContainsExactClosedFrame()
    {
        using var temporary = new Temporary();
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, TimeProvider.System);
        var data = B10AbruptWire.Element(new { attempt_id = "a", invocation_ordinal = 0, cohort = "cross_restart", probe_id = "p", key = "k", sequence = 0,
            payload_length = 1, payload_sha256 = new string('a', 64), predecessor_attempt_id = (string?)null });
        var frame = await journal.AppendPreparedAndFlushAsync(data, TestContext.Current.CancellationToken);
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
        var append = journal.AppendAcceptedAndFlushAsync(B10AbruptWire.Element(new { attempt_id = "a", prepared_sequence = 1 }), TestContext.Current.CancellationToken);
        await reached.Task;
        Assert.False(append.IsCompleted);
        Assert.Empty(journal.Frames);
        release.SetResult();
        Assert.Equal(1, (await append).Sequence);
    }

    [Fact]
    public async Task Given_WriterFailure_When_Append_Then_GapCannotBeClearedByLaterRecord()
    {
        using var temporary = new Temporary();
        await using var journal = new B10AbruptJournal(temporary.Path, Identity, TimeProvider.System,
            _ => Task.FromException(new IOException("synthetic flush failure")));
        var data = B10AbruptWire.Element(new { attempt_id = "a", prepared_sequence = 1 });
        Assert.Equal("b10_abrupt_capture_gap:writer_failed", (await Assert.ThrowsAsync<InvalidDataException>(() => journal.AppendAcceptedAndFlushAsync(data, TestContext.Current.CancellationToken))).Message);
        Assert.Equal(["writer_failed"], journal.Gaps);
        Assert.Empty(journal.Frames);
        await Assert.ThrowsAsync<InvalidDataException>(() => journal.AppendAcceptedAndFlushAsync(data, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => journal.SealAsync(TestContext.Current.CancellationToken));
    }

    private static MemoryStream Framed(byte[] body)
    {
        var bytes = new byte[body.Length + 4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)body.Length);
        body.CopyTo(bytes, 4);
        return new(bytes);
    }

    private sealed class Temporary : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        internal Temporary() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "journal.jsonl"); }
        internal string Path { get; }
        public void Dispose() => Directory.Delete(directory, true);
    }
}
