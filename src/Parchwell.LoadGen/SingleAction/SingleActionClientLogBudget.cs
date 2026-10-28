using System.Globalization;
using System.Text;
using Confluent.Kafka;

namespace Parchwell.LoadGen.SingleAction;

// Native callbacks only serialize into finite memory; the worker owns output after cleanup.
internal sealed class SingleActionClientLogBudget(int repeat)
{
    internal const int MaximumBytes = 163840;
    internal const int MaximumMessageBytes = 1024;
    internal const int MaximumMarkerBytes = 128;
    private readonly object _gate = new();
    private readonly List<string> _serializedLines = [];
    private int _acceptedBytes;
    private long _droppedLines;
    private bool _sealed;

    internal void Append(LogMessage message)
    {
        lock (_gate)
        {
            if (_sealed) return;
            int remaining = MaximumBytes - _acceptedBytes;
            if (remaining == 0) { Drop(); return; }
            var line = new StringBuilder();
            int headerBytes = 0;
            int headerLimit = remaining - 1;
            if (!AppendField(line, "single_action_client_log r" + repeat.ToString(CultureInfo.InvariantCulture) + " " +
                    ((int)message.Level).ToString(CultureInfo.InvariantCulture) + "|", headerLimit, ref headerBytes) ||
                !AppendField(line, message.Facility, headerLimit, ref headerBytes) ||
                !AppendField(line, "|", headerLimit, ref headerBytes) ||
                !AppendField(line, message.Name, headerLimit, ref headerBytes) ||
                !AppendField(line, "| ", headerLimit, ref headerBytes)) { Drop(); return; }
            int messageBytes = 0;
            _ = AppendField(line, message.Message, MaximumMessageBytes, ref messageBytes);
            int bytes = headerBytes + messageBytes + 1;
            if (bytes > remaining) { Drop(); return; }
            line.Append('\n');
            _serializedLines.Add(line.ToString());
            _acceptedBytes += bytes;
        }
    }

    internal IReadOnlyList<string> Seal()
    {
        lock (_gate)
        {
            if (_sealed) return [];
            _sealed = true;
            var lines = new List<string>(_serializedLines);
            _serializedLines.Clear();
            if (_droppedLines > 0)
            {
                string marker = "single_action_client_log_capped r" + repeat.ToString(CultureInfo.InvariantCulture) +
                    " dropped_lines " + _droppedLines.ToString(CultureInfo.InvariantCulture) + "\n";
                if (Encoding.UTF8.GetByteCount(marker) > MaximumMarkerBytes) throw new InvalidOperationException("single_action_client_log_marker_limit");
                lines.Add(marker);
            }
            return lines;
        }
    }

    private void Drop() { if (_droppedLines < long.MaxValue) _droppedLines++; }

    private static bool AppendField(StringBuilder target, string value, int maximum, ref int bytes)
    {
        foreach (Rune scalar in value.EnumerateRunes())
        {
            int length = scalar.Value is '\r' or '\n' ? 2 : scalar.Utf8SequenceLength;
            if (length > maximum - bytes) return false;
            bytes += length;
            if (scalar.Value == '\r') target.Append("\\r");
            else if (scalar.Value == '\n') target.Append("\\n");
            else target.Append(scalar.ToString());
        }
        return true;
    }
}
