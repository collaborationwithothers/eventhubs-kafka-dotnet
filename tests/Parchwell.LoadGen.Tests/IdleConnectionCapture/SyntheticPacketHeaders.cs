using System.Buffers.Binary;
using Parchwell.LoadGen.IdleConnectionCapture;

namespace Parchwell.LoadGen.Tests.IdleConnectionCapture;

internal static class SyntheticPacketHeaders
{
    // Synthetic documentation endpoints; no fixture is a captured packet.
    public const uint RemoteIpv4 = 0xc000020a;
    public const uint LocalIpv4 = 0xc0000214;
    public const ushort RemotePort = 9093;
    public const ushort LocalPort = 40000;
    public const uint Sequence = 0x01020304;
    public const uint Acknowledgment = 0x05060708;
    public static DateTimeOffset StartedUtc => new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    public static PacketSummaryIdentity Identity(string run = "synthetic-334", string? commit = null,
        string configuration = "C0", int repeat = 1, DateTimeOffset? start = null) =>
        new(run, commit ?? new string('a', 40), configuration, repeat, start ?? StartedUtc);

    public static byte[] Create(int ipWords = 5, int tcpWords = 5,
        PacketDirection direction = PacketDirection.Outbound, byte flags = 0)
    {
        byte[] bytes = new byte[(ipWords + tcpWords) * 4];
        bytes[0] = (byte)(0x40 | ipWords);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2), (ushort)bytes.Length);
        bytes[9] = 6;
        bool outbound = direction == PacketDirection.Outbound;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12), outbound ? LocalIpv4 : RemoteIpv4);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), outbound ? RemoteIpv4 : LocalIpv4);
        Span<byte> tcp = bytes.AsSpan(ipWords * 4);
        BinaryPrimitives.WriteUInt16BigEndian(tcp, outbound ? LocalPort : RemotePort);
        BinaryPrimitives.WriteUInt16BigEndian(tcp[2..], outbound ? RemotePort : LocalPort);
        BinaryPrimitives.WriteUInt32BigEndian(tcp[4..], Sequence);
        BinaryPrimitives.WriteUInt32BigEndian(tcp[8..], Acknowledgment);
        tcp[12] = (byte)(tcpWords << 4);
        tcp[13] = flags;
        return bytes;
    }
}
