using System.Text.Json;
using Parchwell.LoadGen.IdleConnectionCapture;
using Xunit;

namespace Parchwell.LoadGen.Tests.IdleConnectionCapture;

#pragma warning disable CA1707
public sealed class PacketSummaryEncodingTests
{
    private static readonly string[] ExpectedEventKeys = ["flow", "direction", "elapsed_ns", "flags"];

    [Theory]
    [InlineData(0, 0, 0, false)] [InlineData(1, 0, 0, true)]
    [InlineData(64, 1, long.MaxValue, true)] [InlineData(65, 0, 0, false)]
    [InlineData(1, 0, -1, false)] [InlineData(1, 2, 0, false)]
    public void Given_EventSyntaxBoundary_When_Encoding_Then_ValidatesBeforeProducingBytes(int flow, int direction, long elapsed, bool valid)
    {
        Assert.Equal(valid, PacketSummaryEncoding.TryEncodeEvent(new PacketSummaryEvent(flow, (PacketDirection)direction, elapsed, 0), out var encoded, out var failure));
        if (!valid)
        {
            Assert.Equal("packet_event_invalid", failure);
            Assert.True(encoded.IsEmpty);
            return;
        }
        Assert.Null(failure);
        using JsonDocument document = JsonDocument.Parse(encoded);
        JsonElement root = document.RootElement;
        Assert.Equal(ExpectedEventKeys, root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(flow, root.GetProperty("flow").GetInt32());
        Assert.Equal(direction == 0 ? "outbound" : "inbound", root.GetProperty("direction").GetString());
        Assert.Equal(elapsed, root.GetProperty("elapsed_ns").GetInt64());
        Assert.Empty(root.GetProperty("flags").EnumerateArray());
    }

    [Fact]
    public void Given_AllByteFlagCombinations_When_EncodingOnceEach_Then_UsesExactOrderedFlagStrings()
    {
        (byte Mask, string Label)[] flags = [(2, "SYN"), (16, "ACK"), (1, "FIN"), (4, "RST"), (8, "PSH"), (32, "URG"), (64, "ECE"), (128, "CWR")];
        for (int bits = 0; bits <= byte.MaxValue; bits++)
        {
            Assert.True(PacketSummaryEncoding.TryEncodeEvent(new PacketSummaryEvent(64, PacketDirection.Outbound, long.MaxValue, (TcpControlFlags)bits), out var encoded, out var failure));
            Assert.Null(failure);
            using JsonDocument document = JsonDocument.Parse(encoded);
            Assert.Equal(flags.Where(f => (bits & f.Mask) != 0).Select(f => f.Label),
                document.RootElement.GetProperty("flags").EnumerateArray().Select(f => f.GetString()));
            if (bits == byte.MaxValue) Assert.Equal(125, encoded.Length);
        }
    }

    [Theory]
    [InlineData("", false)] [InlineData("a", true)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData("a-0", true)] [InlineData("0", true)] [InlineData("-a", false)] [InlineData("a-", false)]
    [InlineData("a_b", false)] [InlineData("a.b", false)] [InlineData("A", false)] [InlineData("a\n", false)]
    public void Given_RunGrammarBoundary_When_Starting_Then_InvalidIdentityWritesNothing(string run, bool valid) =>
        CheckIdentity(SyntheticPacketHeaders.Identity(run: run), valid);

    [Theory]
    [InlineData(39, 'a', false)] [InlineData(40, 'a', true)] [InlineData(41, 'a', false)]
    [InlineData(40, 'A', false)] [InlineData(40, 'g', false)] [InlineData(40, '0', true)]
    public void Given_CommitGrammarBoundary_When_Starting_Then_EnforcesLowercaseHex(int length, char character, bool valid) =>
        CheckIdentity(SyntheticPacketHeaders.Identity(commit: new string(character, length)), valid);

    [Theory]
    [InlineData("C0", 1, true)] [InlineData("C1", 5, true)] [InlineData("C2", 1, false)]
    [InlineData("C0", 0, false)] [InlineData("C0", 6, false)]
    public void Given_ConfigurationAndRepeatBoundary_When_Starting_Then_EnforcesIdentity(string configuration, int repeat, bool valid) =>
        CheckIdentity(SyntheticPacketHeaders.Identity(configuration: configuration, repeat: repeat), valid);

    [Fact]
    public void Given_NonzeroStartOffset_When_Starting_Then_WritesNothing() =>
        CheckIdentity(SyntheticPacketHeaders.Identity(start: SyntheticPacketHeaders.StartedUtc.ToOffset(TimeSpan.FromHours(1))), false);

    [Fact]
    public void Given_FixedScratchCapacity_When_RequestExceedsRemainingSpace_Then_RefusesWithoutResizing()
    {
        var buffer = new FixedPacketBuffer(256);
        Assert.Equal(256, buffer.GetMemory(256).Length);
        buffer.Advance(255);
        Assert.Single(buffer.GetMemory(1).ToArray());
        Assert.Throws<PacketBufferFullException>(() => buffer.GetMemory(2));
        Assert.Throws<PacketBufferFullException>(() => buffer.Advance(2));
        buffer.Advance(1);
        Assert.Throws<PacketBufferFullException>(() => buffer.GetMemory());
        Assert.Equal(256, buffer.WrittenMemory.Length);
    }

    private static void CheckIdentity(PacketSummaryIdentity identity, bool valid)
    {
        using var stream = new MemoryStream();
        var writer = new PacketSummaryStreamWriter(stream, identity);
        Assert.Equal(valid, writer.State == PacketSummaryWriterState.Active);
        if (valid)
        {
            Assert.Null(writer.FailureCode);
            Assert.True(writer.TryComplete(identity.CaptureStartedUtc));
        }
        else
        {
            Assert.Equal("packet_identity_invalid", writer.FailureCode);
            Assert.Equal(0, stream.Length);
            Assert.Equal(0, writer.BytesWritten);
            Assert.False(writer.TryAppend(new PacketSummaryEvent(1, PacketDirection.Outbound, 0, 0)));
            Assert.False(writer.TryComplete(SyntheticPacketHeaders.StartedUtc));
            Assert.Equal(0, stream.Length);
        }
    }
}
