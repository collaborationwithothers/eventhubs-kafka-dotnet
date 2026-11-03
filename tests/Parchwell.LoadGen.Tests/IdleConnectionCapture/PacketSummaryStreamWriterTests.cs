using System.Globalization;
using System.Text;
using System.Text.Json;
using Parchwell.LoadGen.IdleConnectionCapture;
using Xunit;

namespace Parchwell.LoadGen.Tests.IdleConnectionCapture;

#pragma warning disable CA1707
public sealed class PacketSummaryStreamWriterTests
{
    private static PacketSummaryEvent Event => new(1, PacketDirection.Outbound, 0, TcpControlFlags.Syn);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_EmptyOrPopulatedEvents_When_Completing_Then_EmitsExactCompactEnvelope(bool populated)
    {
        using var stream = new MemoryStream();
        var writer = new PacketSummaryStreamWriter(stream, SyntheticPacketHeaders.Identity());
        if (populated)
        {
            Assert.True(writer.TryAppend(Event));
            Assert.True(writer.TryAppend(new PacketSummaryEvent(64, PacketDirection.Inbound, long.MaxValue, TcpControlFlags.Ack)));
        }
        Assert.True(writer.TryComplete(SyntheticPacketHeaders.StartedUtc.AddSeconds(1)));
        Assert.Equal(PacketSummaryWriterState.Completed, writer.State);
        Assert.Null(writer.FailureCode);
        Assert.Equal(stream.Length, writer.BytesWritten);
        Assert.Equal(populated ? 2 : 0, writer.EventCount);
        byte[] bytes = stream.ToArray();
        Assert.Equal((byte)'{', bytes[0]);
        string text = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("\n", text, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Assert.Equal(new[] { "schema_version", "run_id", "source_commit", "configuration_id", "repeat", "capture_started_utc", "events", "capture_ended_utc" },
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal("synthetic-334", root.GetProperty("run_id").GetString());
        Assert.Equal(new string('a', 40), root.GetProperty("source_commit").GetString());
        Assert.Equal("C0", root.GetProperty("configuration_id").GetString());
        Assert.Equal(1, root.GetProperty("repeat").GetInt32());
        Assert.Equal(SyntheticPacketHeaders.StartedUtc.ToString("O", CultureInfo.InvariantCulture), root.GetProperty("capture_started_utc").GetString());
        Assert.Equal(SyntheticPacketHeaders.StartedUtc.AddSeconds(1).ToString("O", CultureInfo.InvariantCulture), root.GetProperty("capture_ended_utc").GetString());
        Assert.Equal(populated ? 2 : 0, root.GetProperty("events").GetArrayLength());
        if (populated)
        {
            Assert.Equal("SYN", root.GetProperty("events")[0].GetProperty("flags")[0].GetString());
            Assert.Equal("inbound", root.GetProperty("events")[1].GetProperty("direction").GetString());
        }
        stream.WriteByte(0); // The writer never owns or disposes its caller's stream.
    }

    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(1, true)]
    public void Given_EndTimeBoundary_When_Completing_Then_RequiresEndAtOrAfterStart(int ticks, bool valid)
    {
        using var stream = new MemoryStream();
        var writer = new PacketSummaryStreamWriter(stream, SyntheticPacketHeaders.Identity());
        long before = stream.Length;
        Assert.Equal(valid, writer.TryComplete(SyntheticPacketHeaders.StartedUtc.AddTicks(ticks)));
        if (!valid) AssertTerminal(writer, stream, before, "packet_identity_invalid");
    }

    [Fact]
    public void Given_NonzeroEndOffset_When_Completing_Then_FaultsBeforeWriting()
    {
        using var stream = new MemoryStream();
        var writer = new PacketSummaryStreamWriter(stream, SyntheticPacketHeaders.Identity());
        long before = stream.Length;
        Assert.False(writer.TryComplete(SyntheticPacketHeaders.StartedUtc.ToOffset(TimeSpan.FromHours(1))));
        AssertTerminal(writer, stream, before, "packet_identity_invalid");
    }

    [Fact]
    public void Given_ProductionCountBoundary_When_StreamingOnce_Then_CompletesAtBothAllowedCountsAndRejectsNextEvent()
    {
        using var belowStream = new CountingStream();
        using var atStream = new CountingStream();
        using var overStream = new CountingStream();
        var below = new PacketSummaryStreamWriter(belowStream, SyntheticPacketHeaders.Identity());
        var at = new PacketSummaryStreamWriter(atStream, SyntheticPacketHeaders.Identity());
        var over = new PacketSummaryStreamWriter(overStream, SyntheticPacketHeaders.Identity());
        for (int count = 1; count <= 65536; count++)
        {
            if (count <= 65535) Assert.True(below.TryAppend(Event));
            Assert.True(at.TryAppend(Event));
            Assert.True(over.TryAppend(Event));
        }
        Assert.Equal(65535, below.EventCount);
        Assert.True(below.TryComplete(SyntheticPacketHeaders.StartedUtc));
        Assert.Equal(65536, at.EventCount);
        Assert.True(at.TryComplete(SyntheticPacketHeaders.StartedUtc));
        long before = overStream.Length;
        Assert.False(over.TryAppend(Event));
        Assert.Equal(65536, over.EventCount);
        Assert.Equal("packet_event_count_exceeded", over.FailureCode);
        Assert.False(over.TryComplete(SyntheticPacketHeaders.StartedUtc));
        Assert.False(over.TryAppend(Event));
        Assert.Equal(before, overStream.Length);
        Assert.Equal(before, over.BytesWritten);
        Assert.Equal(PacketSummaryWriterState.Faulted, over.State);
    }

    [Theory]
    [InlineData(255, true)] [InlineData(256, true)] [InlineData(257, false)]
    public void Given_ProductionEventByteBoundary_When_CheckingBudget_Then_UsesInclusiveCeiling(long bytes, bool valid) =>
        Assert.Equal(valid ? null : "packet_event_bytes_exceeded", new PacketSummaryBudget().CheckEvent(0, bytes, 0, 0));

    [Theory]
    [InlineData(20971519, true)] [InlineData(20971520, true)] [InlineData(20971521, false)]
    public void Given_ProductionTotalByteBoundary_When_CheckingBudget_Then_UsesInclusiveCeiling(long total, bool valid)
    {
        var budget = new PacketSummaryBudget();
        Assert.Equal(valid ? null : "packet_summary_bytes_exceeded", budget.CheckTotal(total - 1, 1, 0));
        Assert.Equal(valid ? null : "packet_summary_bytes_exceeded", budget.CheckTotal(total - 2, 1, 1));
    }

    [Theory]
    [InlineData(65535, true)] [InlineData(65536, true)] [InlineData(65537, false)]
    public void Given_ProductionEnvelopeBoundary_When_CheckingBudget_Then_UsesInclusiveCeiling(long total, bool valid) =>
        Assert.Equal(valid ? null : "packet_summary_envelope_exceeded", new PacketSummaryBudget().CheckEnvelope(total - 1, 1));

    [Fact]
    public void Given_ArithmeticExtremes_When_CheckingBudget_Then_RejectsWithoutOverflow()
    {
        var budget = new PacketSummaryBudget();
        Assert.Equal("packet_summary_bytes_exceeded", budget.CheckTotal(long.MaxValue, 1, 0));
        Assert.Equal("packet_summary_bytes_exceeded", budget.CheckTotal(1, long.MaxValue, 1));
        Assert.Equal("packet_summary_bytes_exceeded", budget.CheckTotal(1, 1, long.MaxValue));
        Assert.Equal("packet_summary_envelope_exceeded", budget.CheckEnvelope(long.MaxValue, 1));
        Assert.Equal("packet_summary_envelope_exceeded", budget.CheckEnvelope(1, long.MaxValue));
        Assert.Equal("packet_event_bytes_exceeded", budget.CheckEvent(0, long.MaxValue, 0, 0));
        Assert.Equal("packet_event_count_exceeded", budget.CheckEvent(long.MaxValue, 1, 0, 0));
        Assert.Equal("packet_summary_bytes_exceeded", budget.CheckEvent(1, 256, long.MaxValue, 1));
    }

    [Fact]
    public void Given_SmallerEventCeiling_When_Appending_Then_RejectsBeforeSeparatorOrEventWrite()
    {
        using var stream = new MemoryStream();
        Assert.True(PacketSummaryEncoding.TryEncodeEvent(Event, out var encoded, out _));
        var budget = new PacketSummaryBudget(10, encoded.Length, 1000, 400);
        var writer = new PacketSummaryStreamWriter(stream, SyntheticPacketHeaders.Identity(), budget);
        Assert.True(writer.TryAppend(Event));
        long before = stream.Length;
        Assert.False(writer.TryAppend(new PacketSummaryEvent(64, PacketDirection.Outbound, long.MaxValue, (TcpControlFlags)255)));
        AssertTerminal(writer, stream, before, "packet_event_bytes_exceeded");
    }

    [Fact]
    public void Given_SmallerTotalCeiling_When_Appending_Then_ReservesEnvelopeAndCountsSeparatorBeforeWrite()
    {
        using var stream = new MemoryStream();
        Assert.True(PacketSummaryEncoding.TryEncodeEvent(Event, out var encoded, out _));
        var budget = new PacketSummaryBudget(10, 256, 400 + encoded.Length * 2, 400);
        var writer = new PacketSummaryStreamWriter(stream, SyntheticPacketHeaders.Identity(), budget);
        Assert.True(writer.TryAppend(Event));
        long before = stream.Length;
        Assert.False(writer.TryAppend(Event)); // The second event's separator exceeds the remaining reservation.
        AssertTerminal(writer, stream, before, "packet_summary_bytes_exceeded");
    }

    [Fact]
    public void Given_EnvelopeReservationExceedsTotal_When_Starting_Then_RejectsBeforeFirstWrite()
    {
        using var stream = new MemoryStream();
        var writer = new PacketSummaryStreamWriter(stream, SyntheticPacketHeaders.Identity(), new PacketSummaryBudget(1, 256, 399, 400));
        AssertTerminal(writer, stream, 0, "packet_summary_bytes_exceeded");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_SmallerEnvelopeCeiling_When_StartingOrCompleting_Then_RejectsBeforeWrite(bool tail)
    {
        using var stream = new MemoryStream();
        PacketSummaryIdentity identity = SyntheticPacketHeaders.Identity();
        int prefixLength = PacketSummaryEncoding.EncodePrefix(identity).Length;
        int ceiling = tail ? prefixLength : prefixLength - 1;
        var writer = new PacketSummaryStreamWriter(stream, identity, new PacketSummaryBudget(1, 256, 1000, ceiling));
        long before = stream.Length;
        if (tail) Assert.False(writer.TryComplete(SyntheticPacketHeaders.StartedUtc));
        AssertTerminal(writer, stream, before, "packet_summary_envelope_exceeded");
        if (!tail) Assert.Equal(0, before);
    }

    [Fact]
    public void Given_SmallerCountCeiling_When_AppendingAgain_Then_RejectsBeforeWriting()
    {
        using var stream = new MemoryStream();
        var writer = new PacketSummaryStreamWriter(stream, SyntheticPacketHeaders.Identity(), new PacketSummaryBudget(1, 256, 1000, 400));
        Assert.True(writer.TryAppend(Event));
        long before = stream.Length;
        Assert.False(writer.TryAppend(Event));
        AssertTerminal(writer, stream, before, "packet_event_count_exceeded");
    }

    [Theory]
    [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)]
    public void Given_StreamFaultOrCancellation_When_WritingEachStage_Then_FaultIsTerminal(int failCall, bool cancel)
    {
        using var stream = new ThrowingStream(failCall, cancel);
        var writer = new PacketSummaryStreamWriter(stream, SyntheticPacketHeaders.Identity());
        if (failCall >= 2) Assert.Equal(failCall > 2, writer.TryAppend(Event));
        if (failCall == 3) Assert.False(writer.TryComplete(SyntheticPacketHeaders.StartedUtc));
        AssertTerminal(writer, stream, stream.Length, "packet_summary_write_failed");
        Assert.Equal(failCall, stream.Calls);
        Assert.True(stream.CanWrite);
        Assert.Equal(stream.Length, writer.BytesWritten);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_CompletedWriter_When_AppendingOrCompletingAgain_Then_FaultsWithoutFurtherWrites(bool append)
    {
        using var stream = new MemoryStream();
        var writer = new PacketSummaryStreamWriter(stream, SyntheticPacketHeaders.Identity());
        Assert.True(writer.TryComplete(SyntheticPacketHeaders.StartedUtc));
        long before = stream.Length;
        Assert.False(append ? writer.TryAppend(Event) : writer.TryComplete(SyntheticPacketHeaders.StartedUtc));
        AssertTerminal(writer, stream, before, "packet_summary_state_invalid");
    }

    [Fact]
    public void Given_InvalidEvent_When_Appending_Then_FaultsBeforeWriting()
    {
        using var stream = new MemoryStream();
        var writer = new PacketSummaryStreamWriter(stream, SyntheticPacketHeaders.Identity());
        long before = stream.Length;
        Assert.False(writer.TryAppend(new PacketSummaryEvent(0, PacketDirection.Outbound, 0, 0)));
        AssertTerminal(writer, stream, before, "packet_event_invalid");
    }

    private static void AssertTerminal(PacketSummaryStreamWriter writer, Stream stream, long expectedBytes, string failure)
    {
        Assert.Equal(PacketSummaryWriterState.Faulted, writer.State);
        Assert.Equal(failure, writer.FailureCode);
        Assert.False(writer.TryAppend(Event));
        Assert.False(writer.TryComplete(SyntheticPacketHeaders.StartedUtc));
        Assert.Equal(expectedBytes, stream.Length);
        Assert.Equal(failure, writer.FailureCode);
    }

    private sealed class ThrowingStream(int failCall, bool cancel) : MemoryStream
    {
        public int Calls { get; private set; }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Calls++;
            if (Calls == failCall)
            {
                if (cancel) throw new OperationCanceledException("synthetic cancellation");
                throw new IOException("synthetic failure");
            }
            base.Write(buffer);
        }
    }

    private sealed class CountingStream : Stream
    {
        private long _length;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;
        public override long Position { get => _length; set => throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer) => _length += buffer.Length;
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
