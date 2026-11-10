using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Parchwell.LoadGen.Run.Abrupt;

internal sealed class B10AbruptProcess
{
    internal sealed record ProcessIdentity(int Pid, string BootId, long StartIdentity);
    internal static ProcessIdentity ReadIdentity(int pid) => new(pid, B10AbruptProc.ReadBootId(), B10AbruptProc.ReadStartTimeTicks(B10AbruptProc.ReadBounded("/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/stat")));
    internal static Process Start(ChildCommand command, B10AbruptOptions options)
    {
        if (!OperatingSystem.IsLinux()) throw B10AbruptDiagnostics.Attach(new InvalidOperationException("b10_abrupt_platform_unsupported"));
        var info = new ProcessStartInfo(command.FileName) { UseShellExecute = false };
        foreach (string argument in command.Arguments) info.ArgumentList.Add(argument);
        foreach (var pair in command.ExtraEnvironment)
        {
            if (pair.Key.StartsWith(B10AbruptOptions.Prefix, StringComparison.OrdinalIgnoreCase)) throw B10AbruptDiagnostics.Attach(new InvalidDataException("b10_abrupt_configuration_invalid"));
            info.Environment[pair.Key] = pair.Value;
        }
        foreach (var pair in options.ToChildEnvironment()) info.Environment[pair.Key] = pair.Value;
        info.Environment[B10AbruptOptions.Prefix + "Role"] = "child";
        try { return Process.Start(info) ?? throw new InvalidOperationException(); }
        catch (Exception exception) { throw B10AbruptDiagnostics.Attach(new InvalidOperationException("b10_abrupt_child_start_failed", exception)); }
    }
    internal sealed record SignalObservation(int RequestedSignal, int? ReturnValue, int? Errno, string? NativeException = null);
    internal sealed class SignalException(string failure, SignalObservation observation) : Exception(failure)
    {
        internal SignalObservation Observation { get; } = observation;
    }
    internal static Func<int, int, (int ReturnValue, int Errno)>? SignalOverride { get; set; }
    internal static void Preflight() => PreflightSignalEntryPoint((pid, signal) =>
    {
        var result = SignalOverride is { } invoke ? invoke(pid, signal) : (Kill(pid, signal), Marshal.GetLastPInvokeError());
        return new(signal, result.Item1, result.Item1 == 0 ? null : result.Item2);
    });
    internal static void PreflightSignalEntryPoint(Func<int, int, SignalObservation> invoke)
    {
        if (!OperatingSystem.IsLinux()) throw B10AbruptDiagnostics.Attach(new InvalidOperationException("b10_abrupt_platform_unsupported"));
        SignalObservation observation;
        try { observation = invoke(Environment.ProcessId, 0); }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { observation = new(0, null, null, exception.GetType().Name); }
        if (observation.RequestedSignal != 0 || observation.ReturnValue != 0)
            throw B10AbruptDiagnostics.Attach(new SignalException("b10_abrupt_signal_preflight_failed", observation));
    }
    internal static (int ReturnValue, int Errno) SelfKill()
    {
        if (SignalOverride is { } invoke) return invoke(Environment.ProcessId, 9);
        int result = Kill(Environment.ProcessId, 9);
        return (result, Marshal.GetLastPInvokeError());
    }
#pragma warning disable SYSLIB1054
    [DllImport("libc", EntryPoint = "kill", ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Kill(int pid, int signal);
#pragma warning restore SYSLIB1054
}
