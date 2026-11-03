using System.Buffers;
using System.Globalization;
using System.Text.Json;

namespace Parchwell.LoadGen.IdleConnectionCapture;

internal static class PacketSummaryEncoding
{
    public static bool TryEncodeEvent(PacketSummaryEvent value, out ReadOnlyMemory<byte> encoded, out string? failureCode)
    {
        encoded = default;
        failureCode = "packet_event_invalid";
        if (value.Flow is < 1 or > 64 || value.ElapsedNanoseconds < 0 ||
            value.Direction is not (PacketDirection.Outbound or PacketDirection.Inbound)) return false;
        var buffer = new FixedPacketBuffer(PacketSummaryBudget.MaximumEventBytes);
        try
        {
            using var json = new Utf8JsonWriter(buffer);
            json.WriteStartObject();
            json.WriteNumber("flow", value.Flow);
            json.WriteString("direction", value.Direction == PacketDirection.Outbound ? "outbound" : "inbound");
            json.WriteNumber("elapsed_ns", value.ElapsedNanoseconds);
            json.WriteStartArray("flags");
            WriteFlag(json, value.Flags, TcpControlFlags.Syn, "SYN");
            WriteFlag(json, value.Flags, TcpControlFlags.Ack, "ACK");
            WriteFlag(json, value.Flags, TcpControlFlags.Fin, "FIN");
            WriteFlag(json, value.Flags, TcpControlFlags.Rst, "RST");
            WriteFlag(json, value.Flags, TcpControlFlags.Psh, "PSH");
            WriteFlag(json, value.Flags, TcpControlFlags.Urg, "URG");
            WriteFlag(json, value.Flags, TcpControlFlags.Ece, "ECE");
            WriteFlag(json, value.Flags, TcpControlFlags.Cwr, "CWR");
            json.WriteEndArray();
            json.WriteEndObject();
            json.Flush();
        }
        catch (PacketBufferFullException)
        {
            failureCode = "packet_event_bytes_exceeded";
            return false;
        }
        encoded = buffer.WrittenMemory;
        failureCode = null;
        return true;
    }

    public static bool IsValidIdentity(PacketSummaryIdentity identity)
    {
        string run = identity.RunId;
        string commit = identity.SourceCommit;
        return run is { Length: >= 1 and <= 29 } && IsRunCharacter(run[0]) && IsRunCharacter(run[^1]) &&
            run.All(c => IsRunCharacter(c) || c == '-') && commit is { Length: 40 } &&
            commit.All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9') &&
            identity.ConfigurationId is "C0" or "C1" && identity.Repeat is >= 1 and <= 5 &&
            identity.CaptureStartedUtc.Offset == TimeSpan.Zero;
    }

    public static ReadOnlyMemory<byte> EncodePrefix(PacketSummaryIdentity identity)
    {
        var buffer = new FixedPacketBuffer(PacketSummaryBudget.MaximumEnvelopeBytes);
        using var json = new Utf8JsonWriter(buffer);
        json.WriteStartObject();
        json.WriteNumber("schema_version", 1);
        json.WriteString("run_id", identity.RunId);
        json.WriteString("source_commit", identity.SourceCommit);
        json.WriteString("configuration_id", identity.ConfigurationId);
        json.WriteNumber("repeat", identity.Repeat);
        json.WriteString("capture_started_utc", identity.CaptureStartedUtc.ToString("O", CultureInfo.InvariantCulture));
        json.WriteStartArray("events");
        json.Flush();
        return buffer.WrittenMemory;
    }

    public static ReadOnlyMemory<byte> EncodeTail(DateTimeOffset endedUtc)
    {
        var buffer = new FixedPacketBuffer(512);
        buffer.Advance(1);
        using var json = new Utf8JsonWriter(buffer);
        json.WriteStartObject();
        json.WriteString("capture_ended_utc", endedUtc.ToString("O", CultureInfo.InvariantCulture));
        json.WriteEndObject();
        json.Flush();
        buffer.WrittenSpan[0] = (byte)']';
        buffer.WrittenSpan[1] = (byte)',';
        return buffer.WrittenMemory;
    }

    private static bool IsRunCharacter(char value) => value is >= 'a' and <= 'z' or >= '0' and <= '9';

    private static void WriteFlag(Utf8JsonWriter json, TcpControlFlags value, TcpControlFlags flag, string label)
    {
        if ((value & flag) != 0) json.WriteStringValue(label);
    }
}

internal sealed class PacketBufferFullException : Exception;

internal sealed class FixedPacketBuffer(int capacity) : IBufferWriter<byte>
{
    private readonly byte[] _bytes = new byte[capacity];
    private int _written;
    public ReadOnlyMemory<byte> WrittenMemory => _bytes.AsMemory(0, _written);
    public Span<byte> WrittenSpan => _bytes.AsSpan(0, _written);

    public void Advance(int count)
    {
        if (count < 0 || count > _bytes.Length - _written) throw new PacketBufferFullException();
        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (sizeHint < 0 || Math.Max(1, sizeHint) > _bytes.Length - _written) throw new PacketBufferFullException();
        return _bytes.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
}
