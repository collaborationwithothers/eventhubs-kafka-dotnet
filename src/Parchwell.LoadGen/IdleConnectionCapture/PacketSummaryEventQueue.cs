namespace Parchwell.LoadGen.IdleConnectionCapture;

internal enum PacketSummaryQueueState { Active, Draining, Completed, Faulted }

internal enum PacketSummaryDequeueResult { Item, Empty, Completed, Faulted }

/// <summary>Holds a bounded FIFO handoff of already-derived packet-summary events.</summary>
/// <remarks>
/// Completion means sealed input has been removed, not that a downstream file or capture succeeded.
/// Queue policy is defined in docs/operations/idle-connection-capture-design.md.
/// </remarks>
internal sealed class PacketSummaryEventQueue
{
    public const int MaximumEntries = 256;

    private readonly object _gate = new();
    private readonly PacketSummaryEvent[] _entries;
    private int _head;
    private int _count;
    private PacketSummaryQueueState _state = PacketSummaryQueueState.Active;
    private string? _failureCode;

    public PacketSummaryEventQueue() : this(MaximumEntries) { }

    // Only tests select smaller capacities. There is no production override.
    internal PacketSummaryEventQueue(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capacity, MaximumEntries);
        _entries = new PacketSummaryEvent[capacity];
    }

    public int Capacity => _entries.Length;
    public int Count { get { lock (_gate) return _count; } }
    public PacketSummaryQueueState State { get { lock (_gate) return _state; } }
    public string? FailureCode { get { lock (_gate) return _failureCode; } }

    public bool TryEnqueue(PacketSummaryEvent value)
    {
        lock (_gate)
        {
            if (_state == PacketSummaryQueueState.Faulted) return false;
            if (_state != PacketSummaryQueueState.Active) return Fault("packet_queue_state_invalid");

            // The real encoder owns event syntax and the serialized-byte ceiling; keep no encoded copy.
            if (!PacketSummaryEncoding.TryEncodeEvent(value, out _, out string? failure))
                return Fault(failure!);
            if (_count == Capacity) return Fault("packet_queue_capacity_exceeded");

            _entries[(_head + _count) % Capacity] = value;
            _count++;
            return true;
        }
    }

    public PacketSummaryDequeueResult TryDequeue(out PacketSummaryEvent value)
    {
        lock (_gate)
        {
            value = default;
            if (_state == PacketSummaryQueueState.Faulted) return PacketSummaryDequeueResult.Faulted;
            if (_state == PacketSummaryQueueState.Completed) return PacketSummaryDequeueResult.Completed;
            if (_count == 0) return PacketSummaryDequeueResult.Empty;

            value = _entries[_head];
            _entries[_head] = default;
            _head = (_head + 1) % Capacity;
            _count--;
            if (_state == PacketSummaryQueueState.Draining && _count == 0)
                _state = PacketSummaryQueueState.Completed;

            // Even when this removes the last sealed event, the caller still has an Item to process.
            return PacketSummaryDequeueResult.Item;
        }
    }

    public bool TryCompleteAdding()
    {
        lock (_gate)
        {
            if (_state == PacketSummaryQueueState.Faulted) return false;
            if (_state == PacketSummaryQueueState.Active)
                _state = _count == 0 ? PacketSummaryQueueState.Completed : PacketSummaryQueueState.Draining;
            return true;
        }
    }

    public void Abort()
    {
        lock (_gate)
        {
            if (_state is PacketSummaryQueueState.Active or PacketSummaryQueueState.Draining)
                Fault("packet_queue_abandoned");
        }
    }

    // Called only under _gate. Failed handoffs discard their remainder and never emit another Item.
    private bool Fault(string failure)
    {
        _failureCode ??= failure;
        _state = PacketSummaryQueueState.Faulted;
        Array.Clear(_entries);
        _head = 0;
        _count = 0;
        return false;
    }
}
