namespace Parchwell.LoadGen.IdleConnectionCapture;

/// <summary>Assigns instance-local ordinal labels to exact caller-supplied private identities.</summary>
/// <remarks>
/// One sequential owner supplies the tuple and opening sequence; this table does not validate
/// connection lifecycles or resolve packets to generations. The storage bound comes from
/// docs/operations/idle-connection-capture-design.md.
/// </remarks>
internal sealed class PacketFlowLabelTable
{
    public const int MaximumFlows = 64;

    private readonly Entry[] _entries = new Entry[MaximumFlows];

    public int Count { get; private set; }
    public string? FailureCode { get; private set; }

    public bool TryGetOrAdd(uint localIpv4, ushort localPort, uint remoteIpv4,
        ushort remotePort, uint initialSequence, out int flow)
    {
        flow = 0;
        if (FailureCode is not null) return false;

        for (int index = 0; index < Count; index++)
        {
            if (_entries[index].Matches(localIpv4, localPort, remoteIpv4, remotePort, initialSequence))
            {
                flow = index + 1;
                return true;
            }
        }

        // A healthy full table still permits lookup; only a new identity makes failure terminal.
        if (Count == MaximumFlows)
        {
            FailureCode = "packet_flow_limit_exceeded";
            return false;
        }

        _entries[Count] = new Entry(localIpv4, localPort, remoteIpv4, remotePort, initialSequence);
        Count++;
        flow = Count;
        return true;
    }

    private readonly struct Entry
    {
        private readonly uint _localIpv4;
        private readonly ushort _localPort;
        private readonly uint _remoteIpv4;
        private readonly ushort _remotePort;
        private readonly uint _initialSequence;

        public Entry(uint localIpv4, ushort localPort, uint remoteIpv4, ushort remotePort, uint initialSequence)
        {
            _localIpv4 = localIpv4;
            _localPort = localPort;
            _remoteIpv4 = remoteIpv4;
            _remotePort = remotePort;
            _initialSequence = initialSequence;
        }

        public bool Matches(uint localIpv4, ushort localPort, uint remoteIpv4, ushort remotePort, uint initialSequence) =>
            _localIpv4 == localIpv4 && _localPort == localPort && _remoteIpv4 == remoteIpv4 &&
            _remotePort == remotePort && _initialSequence == initialSequence;
    }
}
