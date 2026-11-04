namespace Parchwell.LoadGen.IdleConnectionCapture;

internal enum PacketCaptureCounterState { Active, Completed, Faulted }

/// <summary>Accumulates caller-supplied packet and drop samples under one sequential owner.</summary>
/// <remarks>
/// Completion records an accepted final sample, not complete packet evidence. Faulted totals
/// retain only earlier accepted samples for diagnostics. Sampling policy belongs to
/// docs/operations/idle-connection-capture-design.md.
/// </remarks>
internal sealed class PacketCaptureCounters
{
    private readonly ulong _maximumPackets;
    private readonly ulong _maximumDrops;
    private readonly ulong _maximumSamples;

    public PacketCaptureCounters() : this(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue) { }

    // Only tests select reduced arithmetic ceilings. There is no production override.
    internal PacketCaptureCounters(ulong maximumPackets, ulong maximumDrops, ulong maximumSamples)
    {
        ArgumentOutOfRangeException.ThrowIfZero(maximumPackets);
        ArgumentOutOfRangeException.ThrowIfZero(maximumDrops);
        ArgumentOutOfRangeException.ThrowIfZero(maximumSamples);
        _maximumPackets = maximumPackets;
        _maximumDrops = maximumDrops;
        _maximumSamples = maximumSamples;
    }

    public ulong PacketTotal { get; private set; }
    public ulong DropTotal { get; private set; }
    public ulong SampleCount { get; private set; }
    public PacketCaptureCounterState State { get; private set; } = PacketCaptureCounterState.Active;
    public string? FailureCode { get; private set; }

    public bool TryAddSample(uint packets, uint drops) => TryAdd(packets, drops, finalSample: false);

    public bool TryComplete(uint packets, uint drops) => TryAdd(packets, drops, finalSample: true);

    public void FailRead()
    {
        if (State == PacketCaptureCounterState.Faulted) return;
        Fault(State == PacketCaptureCounterState.Completed
            ? "packet_counter_state_invalid"
            : "packet_counter_read_failed");
    }

    public void Abort()
    {
        if (State == PacketCaptureCounterState.Active) Fault("packet_counter_abandoned");
    }

    private bool TryAdd(uint packets, uint drops, bool finalSample)
    {
        if (State == PacketCaptureCounterState.Faulted) return false;
        if (State == PacketCaptureCounterState.Completed) return Fault("packet_counter_state_invalid");

        // Check every addition before changing any total, retaining the whole accepted prefix on failure.
        if (packets > _maximumPackets - PacketTotal
            || drops > _maximumDrops - DropTotal
            || SampleCount == _maximumSamples)
            return Fault("packet_counter_overflow");

        PacketTotal += packets;
        DropTotal += drops;
        SampleCount++;
        if (finalSample) State = PacketCaptureCounterState.Completed;
        return true;
    }

    private bool Fault(string failure)
    {
        FailureCode ??= failure;
        State = PacketCaptureCounterState.Faulted;
        return false;
    }
}
