using System.Buffers.Binary;

namespace Parchwell.LoadGen.IdleConnectionCapture;

internal static class PacketHeaderDecoder
{
    // Layout: RFC 791 section 3.1 and RFC 9293 section 3.1. The supplied span
    // must already be truncated; this managed check does not establish kernel privacy.
    public static bool TryDecode(ReadOnlySpan<byte> header, uint endpointIpv4, PacketDirection direction,
        out PrivatePacketHeader? packet, out PacketHeaderDiagnostic diagnostic)
    {
        packet = null;
        diagnostic = PacketHeaderDiagnostic.HeaderLengthInvalid;
        if (direction is not (PacketDirection.Outbound or PacketDirection.Inbound) || header.Length is < 20 or > 120)
            return false;
        if (header[0] >> 4 != 4)
        {
            diagnostic = PacketHeaderDiagnostic.UnsupportedNetworkProtocol;
            return false;
        }
        int ipBytes = (header[0] & 15) * 4;
        int totalBytes = BinaryPrimitives.ReadUInt16BigEndian(header[2..]);
        if (ipBytes < 20 || header.Length < ipBytes || totalBytes < ipBytes) return false;
        uint source = BinaryPrimitives.ReadUInt32BigEndian(header[12..]);
        uint destination = BinaryPrimitives.ReadUInt32BigEndian(header[16..]);
        bool outbound = direction == PacketDirection.Outbound;
        uint remote = outbound ? destination : source;
        if (remote != endpointIpv4)
        {
            diagnostic = PacketHeaderDiagnostic.UnexpectedPeer;
            return false;
        }
        if ((BinaryPrimitives.ReadUInt16BigEndian(header[6..]) & 0x3fff) != 0)
        {
            diagnostic = header.Length == ipBytes
                ? PacketHeaderDiagnostic.FragmentUnsupported : PacketHeaderDiagnostic.HeaderLengthInvalid;
            return false;
        }
        if (header[9] != 6)
        {
            diagnostic = PacketHeaderDiagnostic.UnsupportedNetworkProtocol;
            return false;
        }
        if (header.Length - ipBytes < 20) return false;
        ReadOnlySpan<byte> tcp = header[ipBytes..];
        int tcpBytes = (tcp[12] >> 4) * 4;
        if (tcpBytes < 20 || header.Length != ipBytes + tcpBytes || totalBytes < ipBytes + tcpBytes) return false;
        ushort sourcePort = BinaryPrimitives.ReadUInt16BigEndian(tcp);
        ushort destinationPort = BinaryPrimitives.ReadUInt16BigEndian(tcp[2..]);
        ushort remotePort = outbound ? destinationPort : sourcePort;
        if (remotePort != 9093)
        {
            diagnostic = PacketHeaderDiagnostic.UnexpectedPeer;
            return false;
        }
        packet = new PrivatePacketHeader(outbound ? source : destination, remote,
            outbound ? sourcePort : destinationPort, remotePort,
            BinaryPrimitives.ReadUInt32BigEndian(tcp[4..]), BinaryPrimitives.ReadUInt32BigEndian(tcp[8..]),
            (TcpControlFlags)tcp[13], ipBytes, tcpBytes);
        diagnostic = PacketHeaderDiagnostic.None;
        return true;
    }
}
