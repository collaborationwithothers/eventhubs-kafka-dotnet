using System.Globalization;
using System.Text;
using System.Runtime.CompilerServices;

namespace Parchwell.LoadGen.Run.Abrupt;

internal static class B10AbruptProc
{
    internal const int MaximumReadBytes = 65536;

    internal static long ReadStartTimeTicks(ReadOnlySpan<byte> stat)
    {
        var delimiter = stat.LastIndexOf((byte)')');
        if (delimiter < 0) throw InvalidIdentity();
        var fields = Encoding.ASCII.GetString(stat[(delimiter + 1)..]).Trim(' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 20 || fields[19].Length == 0 || fields[19].Any(c => c is < '0' or > '9') ||
            !long.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)) throw InvalidIdentity();
        return ticks;
    }

    internal static byte[] ReadBounded(string path, int maximum = MaximumReadBytes)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var bytes = new MemoryStream();
        var buffer = new byte[Math.Min(4096, maximum + 1)];
        while (bytes.Length <= maximum)
        {
            var read = file.Read(buffer, 0, Math.Min(buffer.Length, maximum + 1 - (int)bytes.Length));
            if (read == 0) return bytes.ToArray();
            bytes.Write(buffer, 0, read);
        }
        throw new IOException("bounded_read_exceeded");
    }

    internal static string ReadBootId()
    {
        string value = Encoding.ASCII.GetString(ReadBounded("/proc/sys/kernel/random/boot_id"));
        return value.EndsWith('\n') ? value[..^1] : value;
    }
    internal static InvalidDataException InvalidIdentity([CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0) => B10AbruptDiagnostics.Attach(new InvalidDataException("b10_abrupt_child_identity_mismatch"), null, file, member, line);
}
