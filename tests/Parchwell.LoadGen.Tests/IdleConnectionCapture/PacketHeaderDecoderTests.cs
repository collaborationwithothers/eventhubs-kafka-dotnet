using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Parchwell.LoadGen.IdleConnectionCapture;
using Xunit;

namespace Parchwell.LoadGen.Tests.IdleConnectionCapture;

#pragma warning disable CA1707
public sealed class PacketHeaderDecoderTests
{
    [Theory]
    [InlineData(5, 5, 40)]
    [InlineData(15, 15, 120)]
    [InlineData(5, 15, 80)]
    [InlineData(15, 5, 80)]
    public void Given_SyntheticHeaderLengths_When_DecodingBothDirections_Then_NormalizesOnlyScalarFields(int ip, int tcp, int length)
    {
        foreach (PacketDirection direction in new[] { PacketDirection.Outbound, PacketDirection.Inbound })
        {
            byte[] bytes = SyntheticPacketHeaders.Create(ip, tcp, direction, 0xff);
            Assert.Equal(length, bytes.Length);
            PrivatePacketHeader packet = Decode(bytes, direction);
            Assert.Equal(SyntheticPacketHeaders.LocalIpv4, packet.LocalIpv4);
            Assert.Equal(SyntheticPacketHeaders.RemoteIpv4, packet.RemoteIpv4);
            Assert.Equal(SyntheticPacketHeaders.LocalPort, packet.LocalPort);
            Assert.Equal(SyntheticPacketHeaders.RemotePort, packet.RemotePort);
            Assert.Equal(SyntheticPacketHeaders.Sequence, packet.Sequence);
            Assert.Equal(SyntheticPacketHeaders.Acknowledgment, packet.Acknowledgment);
            Assert.Equal(ip * 4, packet.Ipv4HeaderBytes);
            Assert.Equal(tcp * 4, packet.TcpHeaderBytes);
            Array.Clear(bytes);
            Assert.Equal(SyntheticPacketHeaders.Sequence, packet.Sequence);
            Assert.Equal(SyntheticPacketHeaders.RemoteIpv4, packet.RemoteIpv4);
            Assert.Equal((TcpControlFlags)255, packet.Flags);
            Assert.Equal(typeof(PrivatePacketHeader).FullName, packet.ToString());
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(8)]
    [InlineData(16)] [InlineData(32)] [InlineData(64)] [InlineData(128)] [InlineData(255)]
    public void Given_SyntheticFlags_When_Decoding_Then_PreservesEveryWireBit(byte flags) =>
        Assert.Equal((TcpControlFlags)flags, Decode(SyntheticPacketHeaders.Create(flags: flags)).Flags);

    [Theory]
    [InlineData(5, 0, "PAYLOAD_SENTINEL_SHORT_334")]
    [InlineData(5, 1, "PAYLOAD_SENTINEL_SHORT_334")]
    [InlineData(15, 0, "PAYLOAD_SENTINEL_LONG_334")]
    [InlineData(15, 1, "PAYLOAD_SENTINEL_LONG_334")]
    public void Given_SyntheticPayloadAndOptions_When_ProjectingHeaders_Then_ManagedSummaryContainsOnlyAllowedFields(int words, int directionValue, string sentinel)
    {
        var direction = (PacketDirection)directionValue;
        byte[] header = SyntheticPacketHeaders.Create(words, words, direction, 255);
        if (words == 15) Encoding.ASCII.GetBytes("OPTION_SENTINEL").CopyTo(header, 20);
        byte[] full = [.. header, .. Encoding.ASCII.GetBytes(sentinel)];
        BinaryPrimitives.WriteUInt16BigEndian(full.AsSpan(2), (ushort)full.Length);
        Reject(full, PacketHeaderDiagnostic.HeaderLengthInvalid, direction);
        PrivatePacketHeader packet = Decode(full.AsSpan(0, header.Length), direction);
        using var stream = new MemoryStream();
        var writer = new PacketSummaryStreamWriter(stream, SyntheticPacketHeaders.Identity());
        Assert.True(writer.TryAppend(new PacketSummaryEvent(1, direction, 0, packet.Flags)));
        Assert.True(writer.TryComplete(SyntheticPacketHeaders.StartedUtc));
        string encoded = Encoding.UTF8.GetString(stream.ToArray());
        foreach (string forbidden in new[]
        {
            sentinel, "OPTION_SENTINEL", "192.0.2.10", "192.0.2.20", "9093", "40000", "sequence", "acknowledgment",
            "raw", "header", "options", Convert.ToHexString(header),
            SyntheticPacketHeaders.RemoteIpv4.ToString(CultureInfo.InvariantCulture),
            SyntheticPacketHeaders.LocalIpv4.ToString(CultureInfo.InvariantCulture),
            SyntheticPacketHeaders.Sequence.ToString(CultureInfo.InvariantCulture),
            SyntheticPacketHeaders.Acknowledgment.ToString(CultureInfo.InvariantCulture)
        }) Assert.DoesNotContain(forbidden, encoded, StringComparison.OrdinalIgnoreCase);
        using JsonDocument document = JsonDocument.Parse(encoded);
        Assert.Equal(new[] { "flow", "direction", "elapsed_ns", "flags" },
            document.RootElement.GetProperty("events")[0].EnumerateObject().Select(p => p.Name));
    }

    [Theory]
    [InlineData(0, false)] [InlineData(19, false)] [InlineData(20, false)] [InlineData(39, false)]
    [InlineData(40, true)] [InlineData(119, false)] [InlineData(120, true)] [InlineData(121, false)]
    public void Given_SyntheticSpanBoundary_When_Decoding_Then_RequiresExactDeclaredHeaders(int length, bool valid)
    {
        byte[] source = SyntheticPacketHeaders.Create(length >= 119 ? 15 : 5, length >= 119 ? 15 : 5);
        byte[] supplied = new byte[length];
        source.AsSpan(0, Math.Min(source.Length, supplied.Length)).CopyTo(supplied);
        if (valid) Decode(supplied);
        else Reject(supplied, PacketHeaderDiagnostic.HeaderLengthInvalid);
    }

    [Theory]
    [InlineData(4, 5, false)] [InlineData(5, 4, false)] [InlineData(5, 5, true)]
    [InlineData(15, 15, true)] [InlineData(15, 5, false)] [InlineData(5, 15, false)]
    public void Given_SyntheticDeclaredOffsets_When_Decoding_Then_RejectsShortAndTruncatedOptions(int ipWords, int tcpWords, bool valid)
    {
        byte[] bytes = SyntheticPacketHeaders.Create(valid && ipWords == 15 ? 15 : 5, valid && tcpWords == 15 ? 15 : 5);
        bytes[0] = (byte)(0x40 | ipWords);
        if (ipWords * 4 + 12 < bytes.Length) bytes[ipWords * 4 + 12] = (byte)(tcpWords << 4);
        if (valid) Decode(bytes);
        else Reject(bytes, PacketHeaderDiagnostic.HeaderLengthInvalid);
    }

    [Theory]
    [InlineData(5, 19, false)] [InlineData(5, 20, false)] [InlineData(5, 39, false)]
    [InlineData(5, 40, true)] [InlineData(5, 41, true)]
    [InlineData(15, 119, false)] [InlineData(15, 120, true)] [InlineData(15, 121, true)]
    public void Given_SyntheticTotalLength_When_Decoding_Then_AcceptsOriginalPayloadLengthOnlyWithCompleteHeaders(int words, ushort total, bool valid)
    {
        byte[] bytes = SyntheticPacketHeaders.Create(words, words);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2), total);
        if (valid) Decode(bytes);
        else Reject(bytes, PacketHeaderDiagnostic.HeaderLengthInvalid);
    }

    [Theory]
    [InlineData(0x2000)] [InlineData(1)] [InlineData(0x2001)]
    public void Given_SyntheticFragment_When_Decoding_Then_OnlyHeaderSpanReturnsUnsupportedAndNeverReadsTcp(ushort fragment)
    {
        byte[] bytes = SyntheticPacketHeaders.Create();
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), fragment);
        bytes[9] = 17;
        Reject(bytes.AsSpan(0, 20), PacketHeaderDiagnostic.FragmentUnsupported);
        Reject(bytes.AsSpan(0, 21), PacketHeaderDiagnostic.HeaderLengthInvalid);
        Reject(bytes, PacketHeaderDiagnostic.HeaderLengthInvalid);
    }

    [Fact]
    public void Given_SyntheticDontFragmentFlag_When_Decoding_Then_AcceptsHeader()
    {
        byte[] bytes = SyntheticPacketHeaders.Create();
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), 0x4000);
        Decode(bytes);
    }

    [Theory]
    [InlineData(0, 9092, false)] [InlineData(0, 9093, true)] [InlineData(0, 9094, false)]
    [InlineData(1, 9092, false)] [InlineData(1, 9093, true)] [InlineData(1, 9094, false)]
    public void Given_SyntheticRemotePort_When_Decoding_Then_RequiresSelectedPort(int directionValue, ushort port, bool valid)
    {
        var direction = (PacketDirection)directionValue;
        byte[] bytes = SyntheticPacketHeaders.Create(direction: direction);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(direction == PacketDirection.Outbound ? 22 : 20), port);
        if (valid) Decode(bytes, direction);
        else Reject(bytes, PacketHeaderDiagnostic.UnexpectedPeer, direction);
    }

    [Fact]
    public void Given_MultipleSyntheticMalformedFields_When_Decoding_Then_UsesSpecifiedDiagnosticPrecedence()
    {
        byte[] bytes = SyntheticPacketHeaders.Create();
        bytes[0] = 0x64;
        Reject(bytes, PacketHeaderDiagnostic.HeaderLengthInvalid, (PacketDirection)2);
        Reject(bytes, PacketHeaderDiagnostic.UnsupportedNetworkProtocol);
        bytes[0] = 0x44;
        bytes[16] = 1;
        Reject(bytes, PacketHeaderDiagnostic.HeaderLengthInvalid);
        bytes[0] = 0x45;
        bytes[9] = 17;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), 0x2000);
        Reject(bytes.AsSpan(0, 20), PacketHeaderDiagnostic.UnexpectedPeer);
        bytes[16] = 192;
        Reject(bytes.AsSpan(0, 20), PacketHeaderDiagnostic.FragmentUnsupported);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), 0);
        Reject(bytes.AsSpan(0, 20), PacketHeaderDiagnostic.UnsupportedNetworkProtocol);
        bytes[9] = 6;
        bytes[32] = 0x40;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(22), 9092);
        Reject(bytes, PacketHeaderDiagnostic.HeaderLengthInvalid);
        bytes[32] = 0x50;
        Reject(bytes, PacketHeaderDiagnostic.UnexpectedPeer);
        byte[] inbound = SyntheticPacketHeaders.Create(direction: PacketDirection.Inbound);
        inbound[12] = 1;
        Reject(inbound, PacketHeaderDiagnostic.UnexpectedPeer, PacketDirection.Inbound);
    }

    private static PrivatePacketHeader Decode(ReadOnlySpan<byte> bytes, PacketDirection direction = PacketDirection.Outbound)
    {
        Assert.True(PacketHeaderDecoder.TryDecode(bytes, SyntheticPacketHeaders.RemoteIpv4, direction, out var packet, out var diagnostic));
        Assert.Equal(PacketHeaderDiagnostic.None, diagnostic);
        return Assert.IsType<PrivatePacketHeader>(packet);
    }

    private static void Reject(ReadOnlySpan<byte> bytes, PacketHeaderDiagnostic expected, PacketDirection direction = PacketDirection.Outbound)
    {
        Assert.False(PacketHeaderDecoder.TryDecode(bytes, SyntheticPacketHeaders.RemoteIpv4, direction, out var packet, out var diagnostic));
        Assert.Null(packet);
        Assert.Equal(expected, diagnostic);
    }
}
