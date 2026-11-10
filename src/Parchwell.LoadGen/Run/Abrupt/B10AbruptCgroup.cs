using System.Globalization;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;

namespace Parchwell.LoadGen.Run.Abrupt;

internal static class B10AbruptCgroup
{
    internal sealed record Observation(string BootId, string CgroupPath, string DirectoryIdentity, long OomKill);

    internal static Observation CaptureBefore() => CaptureBefore(B10AbruptProcess.ReadIdentity(Environment.ProcessId));

    internal static Observation CaptureBefore(B10AbruptProcess.ProcessIdentity process)
    {
        try
        {
            if (B10AbruptProcess.ReadIdentity(process.Pid) != process) throw Unavailable();
            var root = "/proc/" + process.Pid.ToString(CultureInfo.InvariantCulture);
            var path = Resolve(Encoding.UTF8.GetString(B10AbruptProc.ReadBounded(root + "/cgroup")),
                Encoding.UTF8.GetString(B10AbruptProc.ReadBounded(root + "/mountinfo")));
            return Capture(path, process.BootId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or OverflowException)
        { throw Unavailable(exception); }
    }

    internal static Observation CaptureAfter(Observation before)
    {
        try { return Capture(before.CgroupPath, before.BootId); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or OverflowException)
        { throw Unavailable(exception); }
    }

    private static Observation Capture(string directory, string boot)
    {
        if (B10AbruptProc.ReadBootId() != boot) throw Unavailable();
        // Linux v6.11 include/uapi/asm-generic/fcntl.h: O_DIRECTORY | O_CLOEXEC, read-only.
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64) throw Unavailable();
        int descriptor;
        try { descriptor = Open(directory, 0x10000 | 0x80000); }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { throw Unavailable(exception); }
        if (descriptor < 0) throw Unavailable();
        using var handle = new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
        var fdInfo = Encoding.ASCII.GetString(B10AbruptProc.ReadBounded("/proc/self/fdinfo/" + handle.DangerousGetHandle().ToInt64().ToString(CultureInfo.InvariantCulture)));
        var identity = ReadDirectoryIdentity(fdInfo);
        var counter = ParseLocalCounter(Encoding.ASCII.GetString(B10AbruptProc.ReadBounded("/proc/self/fd/" + descriptor.ToString(CultureInfo.InvariantCulture) + "/memory.events.local")));
        return new(boot, directory, identity, counter);
    }

    internal static string ReadDirectoryIdentity(string fdInfo)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in fdInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2 || !fields.TryAdd(parts[0], parts[1].Trim())) throw Unavailable();
        }
        if (!fields.TryGetValue("mnt_id", out var mount) || !fields.TryGetValue("ino", out var inode) ||
            !ulong.TryParse(mount, NumberStyles.None, CultureInfo.InvariantCulture, out _) || !ulong.TryParse(inode, NumberStyles.None, CultureInfo.InvariantCulture, out _)) throw Unavailable();
        return mount + ":" + inode;
    }

    internal static string Resolve(string membership, string mountInfo)
    {
        var groups = membership.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(line => line.StartsWith("0::", StringComparison.Ordinal)).ToArray();
        if (groups.Length != 1) throw Unavailable();
        var path = groups[0][3..];
        if (!path.StartsWith('/') || path.Split('/').Any(part => part is ".." or ".")) throw Unavailable();
        var mounts = mountInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => Array.IndexOf(parts, "-") is var separator && separator >= 6 && separator + 3 < parts.Length && parts[separator + 1] == "cgroup2")
            .ToArray();
        if (mounts.Length != 1) throw Unavailable();
        var root = Unescape(mounts[0][3]);
        var target = Unescape(mounts[0][4]);
        if (root != "/" && path != root && !path.StartsWith(root + "/", StringComparison.Ordinal)) throw Unavailable();
        var relative = root == "/" ? path.TrimStart('/') : path[root.Length..].TrimStart('/');
        var resolved = System.IO.Path.GetFullPath(System.IO.Path.Combine(target, relative));
        if (resolved != target && !resolved.StartsWith(target.TrimEnd('/') + "/", StringComparison.Ordinal)) throw Unavailable();
        return resolved;
    }

    private static string Unescape(string path) => path.Replace("\\040", " ", StringComparison.Ordinal).Replace("\\011", "\t", StringComparison.Ordinal)
        .Replace("\\012", "\n", StringComparison.Ordinal).Replace("\\134", "\\", StringComparison.Ordinal);

    internal static long ParseLocalCounter(string text)
    {
        long? count = null;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 2 || !names.Add(fields[0]) || !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var value)) throw Unavailable();
            if (fields[0] == "oom_kill") count = value;
        }
        return count ?? throw Unavailable();
    }

    internal static bool Compare(Observation before, Observation after)
    {
        if (before.BootId != after.BootId || before.CgroupPath != after.CgroupPath || before.DirectoryIdentity != after.DirectoryIdentity || before.OomKill < 0 || after.OomKill < 0) throw Unavailable();
        if (before.OomKill != after.OomKill) throw B10AbruptDiagnostics.Attach(new InvalidOperationException("b10_abrupt_oom_conflict"));
        return true;
    }

#pragma warning disable SYSLIB1054 // Read-only directory handles bind the local cgroup inode without unsafe struct layouts.
    [DllImport("libc", EntryPoint = "open", ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.Cdecl, BestFitMapping = false)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
#pragma warning restore SYSLIB1054

    private static InvalidOperationException Unavailable(Exception? inner = null, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0) => B10AbruptDiagnostics.Attach(new InvalidOperationException("b10_abrupt_cgroup_unavailable", inner), null, file, member, line);
}
