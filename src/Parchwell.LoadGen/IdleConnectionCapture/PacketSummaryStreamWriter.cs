namespace Parchwell.LoadGen.IdleConnectionCapture;

internal enum PacketSummaryWriterState { Active, Completed, Faulted }

internal sealed class PacketSummaryStreamWriter
{
    private readonly Stream _stream;
    private readonly PacketSummaryIdentity _identity;
    private readonly PacketSummaryBudget _budget;
    private long _envelopeBytes;

    public PacketSummaryStreamWriter(Stream stream, PacketSummaryIdentity identity)
        : this(stream, identity, new PacketSummaryBudget()) { }

    // Only tests select smaller ceilings. There is no configuration or runtime route.
    internal PacketSummaryStreamWriter(Stream stream, PacketSummaryIdentity identity, PacketSummaryBudget budget)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(budget);
        _stream = stream;
        _identity = identity;
        _budget = budget;
        if (!PacketSummaryEncoding.IsValidIdentity(identity))
        {
            Fault("packet_identity_invalid");
            return;
        }
        if (!stream.CanWrite)
        {
            Fault("packet_summary_write_failed");
            return;
        }
        ReadOnlyMemory<byte> prefix = PacketSummaryEncoding.EncodePrefix(identity);
        string? failure = budget.CheckEnvelope(0, prefix.Length);
        failure ??= budget.CheckTotal(0, prefix.Length, budget.EnvelopeBytes - prefix.Length);
        if (failure is not null)
        {
            Fault(failure);
            return;
        }
        if (TryWrite(prefix.Span)) _envelopeBytes = prefix.Length;
    }

    public PacketSummaryWriterState State { get; private set; }
    public string? FailureCode { get; private set; }
    public int EventCount { get; private set; }
    // Successful Write calls only: a throwing caller stream may have consumed a prefix.
    public long BytesWritten { get; private set; }

    public bool TryAppend(PacketSummaryEvent value)
    {
        if (!RequireActive()) return false;
        if (!PacketSummaryEncoding.TryEncodeEvent(value, out ReadOnlyMemory<byte> encoded, out string? failure))
            return Fault(failure!);
        failure = _budget.CheckEvent(EventCount, encoded.Length, BytesWritten, _envelopeBytes);
        if (failure is not null) return Fault(failure);
        if (EventCount != 0 && !TryWrite(","u8)) return false;
        if (!TryWrite(encoded.Span)) return false;
        EventCount++;
        return true;
    }

    public bool TryComplete(DateTimeOffset endedUtc)
    {
        if (!RequireActive()) return false;
        if (endedUtc.Offset != TimeSpan.Zero || endedUtc < _identity.CaptureStartedUtc)
            return Fault("packet_identity_invalid");
        ReadOnlyMemory<byte> tail = PacketSummaryEncoding.EncodeTail(endedUtc);
        string? failure = _budget.CheckEnvelope(_envelopeBytes, tail.Length);
        failure ??= _budget.CheckTotal(BytesWritten, tail.Length, 0);
        if (failure is not null) return Fault(failure);
        if (!TryWrite(tail.Span)) return false;
        _envelopeBytes += tail.Length;
        State = PacketSummaryWriterState.Completed;
        return true;
    }

    private bool RequireActive() => State == PacketSummaryWriterState.Active ||
        (State != PacketSummaryWriterState.Faulted && Fault("packet_summary_state_invalid"));

    private bool Fault(string failure)
    {
        State = PacketSummaryWriterState.Faulted;
        FailureCode ??= failure;
        return false;
    }

    private bool TryWrite(ReadOnlySpan<byte> bytes)
    {
        try
        {
            _stream.Write(bytes);
            BytesWritten += bytes.Length;
            return true;
        }
#pragma warning disable CA1031 // Arbitrary caller stream faults are terminal and expose only the fixed code.
        catch (Exception)
#pragma warning restore CA1031
        {
            return Fault("packet_summary_write_failed");
        }
    }
}

internal sealed class PacketSummaryBudget
{
    public const int MaximumEvents = 65536;
    public const int MaximumEventBytes = 256;
    public const int MaximumEnvelopeBytes = 65536;
    public const long MaximumSummaryBytes = 20971520;

    public PacketSummaryBudget() : this(MaximumEvents, MaximumEventBytes, MaximumSummaryBytes, MaximumEnvelopeBytes) { }

    internal PacketSummaryBudget(int events, int eventBytes, long summaryBytes, int envelopeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(events, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(events, MaximumEvents);
        ArgumentOutOfRangeException.ThrowIfLessThan(eventBytes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(eventBytes, MaximumEventBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(summaryBytes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(summaryBytes, MaximumSummaryBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(envelopeBytes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(envelopeBytes, MaximumEnvelopeBytes);
        Events = events;
        EventBytes = eventBytes;
        SummaryBytes = summaryBytes;
        EnvelopeBytes = envelopeBytes;
    }

    public int Events { get; }
    public int EventBytes { get; }
    public long SummaryBytes { get; }
    public int EnvelopeBytes { get; }

    public string? CheckEvent(long count, long encodedBytes, long writtenBytes, long envelopeWritten)
    {
        if (count < 0 || count >= Events) return "packet_event_count_exceeded";
        if (encodedBytes < 0 || encodedBytes > EventBytes) return "packet_event_bytes_exceeded";
        string? failure = CheckEnvelope(envelopeWritten, 0);
        if (failure is not null) return failure;
        long separator = count == 0 ? 0 : 1;
        return CheckTotal(writtenBytes, encodedBytes + separator, EnvelopeBytes - envelopeWritten);
    }

    public string? CheckEnvelope(long written, long addition) =>
        written < 0 || addition < 0 || written > EnvelopeBytes || addition > EnvelopeBytes - written
            ? "packet_summary_envelope_exceeded" : null;

    public string? CheckTotal(long written, long addition, long reservation) =>
        written < 0 || addition < 0 || reservation < 0 || written > SummaryBytes ||
        addition > SummaryBytes - written || reservation > SummaryBytes - written - addition
            ? "packet_summary_bytes_exceeded" : null;
}
