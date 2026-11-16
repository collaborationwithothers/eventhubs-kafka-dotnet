using System.Runtime.InteropServices;

namespace Parchwell.Enrichment.ConsumerProcessing;

/// <summary>
/// Sends one signal number to the current process. Returns the call's return value and errno.
/// The worker passes only 0 (an existence and permission check) and 9 (SIGKILL).
/// </summary>
internal delegate (int ReturnValue, int Errno) ConsumerSignalSender(int signal);

/// <summary>Production implementation of <see cref="ConsumerSignalSender"/>: libc kill on this process's own ID.</summary>
internal static class ConsumerSelfSignal
{
    internal static (int ReturnValue, int Errno) Send(int signal)
    {
        int result = Kill(Environment.ProcessId, signal);
        return (result, Marshal.GetLastPInvokeError());
    }

#pragma warning disable SYSLIB1054
    [DllImport("libc", EntryPoint = "kill", ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Kill(int pid, int signal);
#pragma warning restore SYSLIB1054
}
