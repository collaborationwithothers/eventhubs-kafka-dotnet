using System.Runtime.InteropServices;

namespace Parchwell.LoadGen.IdleConnectionCapture;

/// <summary>Finalizes a local packet summary through Linux's no-replace rename operation.</summary>
internal static class LinuxPacketSummaryRename
{
    private const int CurrentWorkingDirectory = -100;
    private const uint NoReplace = 1;

    public static void MoveWithoutReplace(string partialPath, string finalPath)
    {
        if (!OperatingSystem.IsLinux()) throw new PacketSummaryRenameException();
        int result;
        try
        {
            result = RenameAt2(CurrentWorkingDirectory, partialPath, CurrentWorkingDirectory, finalPath, NoReplace);
        }
        catch (DllNotFoundException)
        {
            throw new PacketSummaryRenameException();
        }
        catch (EntryPointNotFoundException)
        {
            throw new PacketSummaryRenameException();
        }
        if (result != 0) throw new PacketSummaryRenameException();
    }

#pragma warning disable SYSLIB1054 // The approved native boundary specifies DllImport without generated unsafe code.
    [DllImport("libc", EntryPoint = "renameat2", ExactSpelling = true, SetLastError = true,
        CallingConvention = CallingConvention.Cdecl, BestFitMapping = false)]
    private static extern int RenameAt2(int oldDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string oldPath,
        int newDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath, uint flags);
#pragma warning restore SYSLIB1054

    private sealed class PacketSummaryRenameException : Exception;
}
