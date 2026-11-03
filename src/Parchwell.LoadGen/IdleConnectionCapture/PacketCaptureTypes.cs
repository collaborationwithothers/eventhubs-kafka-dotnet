namespace Parchwell.LoadGen.IdleConnectionCapture;

internal enum PacketDirection { Outbound, Inbound }

[Flags]
internal enum TcpControlFlags : byte
{
    Fin = 1, Syn = 2, Rst = 4, Psh = 8, Ack = 16, Urg = 32, Ece = 64, Cwr = 128
}

internal enum PacketHeaderDiagnostic
{
    None, HeaderLengthInvalid, UnsupportedNetworkProtocol, UnexpectedPeer, FragmentUnsupported
}

// Correlation inputs only. Never pass this type to a serializer or logger.
internal sealed class PrivatePacketHeader(
    uint localIpv4, uint remoteIpv4, ushort localPort, ushort remotePort,
    uint sequence, uint acknowledgment, TcpControlFlags flags, int ipv4HeaderBytes, int tcpHeaderBytes)
{
    public uint LocalIpv4 { get; } = localIpv4;
    public uint RemoteIpv4 { get; } = remoteIpv4;
    public ushort LocalPort { get; } = localPort;
    public ushort RemotePort { get; } = remotePort;
    public uint Sequence { get; } = sequence;
    public uint Acknowledgment { get; } = acknowledgment;
    public TcpControlFlags Flags { get; } = flags;
    public int Ipv4HeaderBytes { get; } = ipv4HeaderBytes;
    public int TcpHeaderBytes { get; } = tcpHeaderBytes;
}

internal readonly record struct PacketSummaryEvent(
    int Flow, PacketDirection Direction, long ElapsedNanoseconds, TcpControlFlags Flags);

internal sealed class PacketSummaryIdentity(
    string runId, string sourceCommit, string configurationId, int repeat, DateTimeOffset captureStartedUtc)
{
    public string RunId { get; } = runId;
    public string SourceCommit { get; } = sourceCommit;
    public string ConfigurationId { get; } = configurationId;
    public int Repeat { get; } = repeat;
    public DateTimeOffset CaptureStartedUtc { get; } = captureStartedUtc;
}
