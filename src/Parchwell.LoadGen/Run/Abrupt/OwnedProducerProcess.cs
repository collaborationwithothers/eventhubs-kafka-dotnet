using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Parchwell.LoadGen.Run.Abrupt;

internal sealed record ChildCommand(string FileName, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string> ExtraEnvironment)
{
    internal static ChildCommand ForRunningApphost()
    {
        var expected = Path.Combine(AppContext.BaseDirectory, "Parchwell.LoadGen");
        if (Environment.ProcessPath != expected || !File.Exists(expected)) throw new InvalidOperationException("b10_abrupt_configuration_invalid");
        return new(expected, ["--b10-abrupt-child"], new Dictionary<string, string>());
    }
}

internal sealed class OwnedProducerProcess : IDisposable
{
    internal sealed record ProcessIdentity(int Pid, int ParentPid, string BootId, long StartIdentity);
    internal sealed record SignalObservation(int RequestedSignal, int? ReturnValue, int? Errno, string? NativeException = null);
    internal sealed record ExitObservation(bool WaitForExitObserved, int? ExitCodeLocalObservation, string RuntimeVersion);
    internal sealed class SignalException(string failure, SignalObservation observation) : Exception(failure)
    {
        internal SignalObservation Observation { get; } = observation;
    }

    private readonly Process process;
    private bool killRequested;
    private bool disposed;
    private OwnedProducerProcess(Process process, Stream events, Stream acknowledgements, ProcessIdentity identity)
    { this.process = process; Events = events; Acknowledgements = acknowledgements; Identity = identity; }

    internal Stream Events { get; }
    internal Stream Acknowledgements { get; }
    internal ProcessIdentity Identity { get; }
    internal int Id => process.Id;
    internal bool HasExited => process.HasExited;

    internal static OwnedProducerProcess Start(ChildCommand command, B10AbruptOptions options) => Start(command, options, InvokeSignal);

    internal static OwnedProducerProcess Start(ChildCommand command, B10AbruptOptions options, Func<int, int, SignalObservation> signal)
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new InvalidOperationException("b10_abrupt_platform_unsupported");
        PreflightSignalEntryPoint(signal);
        foreach (var key in command.ExtraEnvironment.Keys)
            if (key.StartsWith("B10Abrupt__", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("b10_abrupt_configuration_invalid");
        var events = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        AnonymousPipeServerStream acknowledgements;
        try { acknowledgements = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable); }
        catch { events.Dispose(); throw; }
        Process? child = null;
        OwnedProducerProcess? owned = null;
        try
        {
            var challenge = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            var start = new ProcessStartInfo(command.FileName)
            { UseShellExecute = false, RedirectStandardInput = false, RedirectStandardOutput = false, RedirectStandardError = false };
            foreach (var argument in command.Arguments) start.ArgumentList.Add(argument);
            foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("B10Abrupt__", StringComparison.OrdinalIgnoreCase)).ToArray()) start.Environment.Remove(key);
            foreach (var pair in options.ToChildEnvironment()) start.Environment[pair.Key] = pair.Value;
            start.Environment.Remove("B10Abrupt__PredecessorBindingPath");
            start.Environment["B10Abrupt__Role"] = "child";
            start.Environment["B10Abrupt__EventsPipeHandle"] = events.GetClientHandleAsString();
            start.Environment["B10Abrupt__AckPipeHandle"] = acknowledgements.GetClientHandleAsString();
            start.Environment["B10Abrupt__IdentityChallenge"] = challenge;
            foreach (var pair in command.ExtraEnvironment) start.Environment[pair.Key] = pair.Value;
            try { child = Process.Start(start) ?? throw new InvalidOperationException("b10_abrupt_child_start_failed"); }
            catch (Exception exception) { throw new InvalidOperationException("b10_abrupt_child_start_failed", exception); }
            events.DisposeLocalCopyOfClientHandle();
            acknowledgements.DisposeLocalCopyOfClientHandle();
            var identity = ReadIdentity(child.Id);
            owned = new(child, events, acknowledgements, identity);
            var hello = B10AbruptWire.ReadMessageAsync(events, 1).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            if (hello?.Kind != "hello" || hello.Data.GetProperty("challenge").GetString() != challenge ||
                hello.Data.GetProperty("pid").GetInt32() != child.Id || hello.Data.GetProperty("start_time_ticks").GetInt64() != identity.StartIdentity ||
                hello.Data.GetProperty("boot_id").GetString() != identity.BootId || identity.ParentPid != Environment.ProcessId)
                throw B10AbruptProc.InvalidIdentity();
            return owned;
        }
        catch (Exception exception)
        {
            if (owned is not null) owned.Dispose();
            else { events.Dispose(); acknowledgements.Dispose(); child?.Dispose(); }
            if (exception.Message == "b10_abrupt_child_start_failed") throw;
            throw new InvalidOperationException("b10_abrupt_child_identity_mismatch", exception);
        }
    }

    internal static SignalObservation PreflightSignalEntryPoint() => PreflightSignalEntryPoint(InvokeSignal);
    internal static SignalObservation PreflightSignalEntryPoint(Func<int, int, SignalObservation> signal)
    {
        SignalObservation observation;
        try { observation = signal(Environment.ProcessId, 0); }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { observation = new(0, null, null, exception.GetType().Name); }
        if (observation.ReturnValue != 0) throw new SignalException("b10_abrupt_signal_preflight_failed", observation);
        return observation;
    }

    internal static ProcessIdentity ReadIdentity(int pid)
    {
        if (pid <= 0) throw new InvalidOperationException("b10_abrupt_target_invalid");
        var stat = B10AbruptProc.ReadBounded($"/proc/{pid.ToString(CultureInfo.InvariantCulture)}/stat");
        var status = Encoding.ASCII.GetString(B10AbruptProc.ReadBounded($"/proc/{pid.ToString(CultureInfo.InvariantCulture)}/status"));
        return ParseIdentity(pid, stat, status, B10AbruptProc.ReadBootId());
    }

    internal static ProcessIdentity ParseIdentity(int pid, byte[] stat, string status, string bootId)
    {
        var parent = StatusNumber(status, "PPid:");
        if (StatusNumber(status, "TracerPid:") != 0) throw new InvalidOperationException("b10_abrupt_target_invalid");
        var fields = Encoding.ASCII.GetString(stat.AsSpan(stat.AsSpan().LastIndexOf((byte)')') + 1)).Trim(' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (pid <= 0 || fields.Length < 20 || fields[0] is not ("R" or "S" or "D" or "T" or "I") ||
            !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var statParent) || parent != statParent)
            throw new InvalidOperationException("b10_abrupt_target_invalid");
        return new(pid, parent, bootId, B10AbruptProc.ReadStartTimeTicks(stat));
    }

    private static int StatusNumber(string status, string key)
    {
        var rows = status.Split('\n').Where(line => line.StartsWith(key, StringComparison.Ordinal)).ToArray();
        if (rows.Length != 1 || !int.TryParse(rows[0][key.Length..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            throw new InvalidOperationException("b10_abrupt_target_invalid");
        return value;
    }

    internal static void ValidateTarget(ProcessIdentity original, ProcessIdentity current, bool exited, int parentPid)
    {
        if (exited || original.Pid <= 0 || original.ParentPid != parentPid || original != current)
            throw new InvalidOperationException("b10_abrupt_target_invalid");
    }

    private void ValidateTarget()
    {
        try { ValidateTarget(Identity, ReadIdentity(process.Id), process.HasExited, Environment.ProcessId); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw new InvalidOperationException("b10_abrupt_target_invalid", exception); }
    }

    // Linux v6.11 arch/x86/include/uapi/asm/signal.h fixes KILL=9, CONT=18 and STOP=19.
    internal SignalObservation RequestStop() => Signal(19);
    internal SignalObservation Resume() => Signal(18);
    internal SignalObservation RequestKill()
    {
        if (killRequested) throw new InvalidOperationException("b10_abrupt_target_invalid");
        killRequested = true;
        return Signal(9);
    }

    private SignalObservation Signal(int number)
    {
        try { return RequestSignal(Identity, ReadIdentity(process.Id), process.HasExited, Environment.ProcessId, number, InvokeSignal); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw new InvalidOperationException("b10_abrupt_target_invalid", exception); }
    }

    internal static SignalObservation RequestSignal(ProcessIdentity original, ProcessIdentity current, bool exited, int parentPid,
        int number, Func<int, int, SignalObservation> invoke)
    {
        ValidateTarget(original, current, exited, parentPid);
        if (number is not (9 or 18 or 19)) throw new InvalidOperationException("b10_abrupt_target_invalid");
        SignalObservation observation;
        try { observation = invoke(original.Pid, number); }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { observation = new(number, null, null, exception.GetType().Name); }
        if (observation.ReturnValue != 0) throw new SignalException("b10_abrupt_signal_failed", observation);
        return observation;
    }

    internal bool ConfirmStopped(TimeSpan? timeout = null, TimeProvider? timeProvider = null)
    {
        var clock = timeProvider ?? TimeProvider.System;
        var start = clock.GetTimestamp();
        var budget = timeout ?? TimeSpan.FromSeconds(30);
        while (clock.GetElapsedTime(start) <= budget)
        {
            ValidateTarget();
            var stat = B10AbruptProc.ReadBounded($"/proc/{process.Id.ToString(CultureInfo.InvariantCulture)}/stat");
            var suffix = Encoding.ASCII.GetString(stat.AsSpan(stat.AsSpan().LastIndexOf((byte)')') + 1)).Trim(' ');
            if (suffix.StartsWith("T ", StringComparison.Ordinal) && clock.GetElapsedTime(start) <= budget) return true;
            Thread.Sleep(10);
        }
        return false;
    }

    internal ExitObservation ObserveExit(int milliseconds = 10000)
    {
        if (milliseconds is < 0 or > 10000) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        var exited = process.WaitForExit(milliseconds);
        return new(exited, exited ? process.ExitCode : null, Environment.Version.ToString());
    }

    private static SignalObservation InvokeSignal(int pid, int signal)
    {
        try
        {
            var result = Kill(pid, signal);
            var error = result == 0 ? (int?)null : Marshal.GetLastPInvokeError();
            return new(signal, result, error);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { return new(signal, null, null, exception.GetType().Name); }
    }

#pragma warning disable SYSLIB1054 // The approved native boundary specifies DllImport without generated unsafe code.
    [DllImport("libc", EntryPoint = "kill", ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Kill(int pid, int signal);
#pragma warning restore SYSLIB1054

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { if (!process.HasExited && !killRequested) { RequestKill(); _ = ObserveExit(); } }
        finally { Events.Dispose(); Acknowledgements.Dispose(); process.Dispose(); }
    }
}
