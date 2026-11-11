using System.Globalization;
using System.Text.Json;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

public sealed class B10AbruptSignalProcessTests
{
    [Theory]
    [InlineData("b10_abrupt_untrusted_secret", "b10_abrupt_capture_gap")]
    [InlineData("b10_abrupt_signal_failed:private-token", "b10_abrupt_signal_failed")]
    public void Given_SecretBearingSignalFailure_When_HostReportsFailure_Then_EmitsClosedLabelAndActualObservation(string message, string expectedFailure)
    {
        var exception = Assert.Throws<B10AbruptProcess.SignalException>((Action)(() => { throw new B10AbruptProcess.SignalException(message, new(19, -1, 3)); }));
        Assert.NotNull(exception.StackTrace);
        using var diagnostic = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(exception, diagnostic));
        string report = diagnostic.ToString();
        using var emitted = JsonDocument.Parse(report);
        B10AbruptWire.ExactMembers(emitted.RootElement, "failure", "exception_type", "diagnostic", "signal_observation");
        Assert.Equal(expectedFailure, emitted.RootElement.GetProperty("failure").GetString());
        Assert.Equal("Parchwell.LoadGen.Run.Abrupt.B10AbruptProcess+SignalException", emitted.RootElement.GetProperty("exception_type").GetString());
        var observation = emitted.RootElement.GetProperty("signal_observation");
        B10AbruptWire.ExactMembers(observation, "requested_signal", "return_value", "errno", "native_exception");
        Assert.Equal(19, observation.GetProperty("requested_signal").GetInt32());
        Assert.Equal(-1, observation.GetProperty("return_value").GetInt32());
        Assert.Equal(3, observation.GetProperty("errno").GetInt32());
        Assert.Equal(JsonValueKind.Null, observation.GetProperty("native_exception").ValueKind);
        Assert.DoesNotContain(message, report, StringComparison.Ordinal);
        Assert.DoesNotContain("private-token", report, StringComparison.Ordinal);
        Assert.DoesNotContain(exception.StackTrace, report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Given_NativeLoadFailure_When_Preflight_Then_RetainsTypeWithoutFabricatedErrno(int kind)
    {
        Exception failure = kind switch { 0 => new DllNotFoundException(), 1 => new EntryPointNotFoundException(), _ => new BadImageFormatException() };
        var exception = Assert.Throws<B10AbruptProcess.SignalException>(() => B10AbruptProcess.PreflightSignalEntryPoint((_, _) => throw failure));
        Assert.Equal("b10_abrupt_signal_preflight_failed", exception.Message);
        Assert.Null(exception.Observation.Errno);
        Assert.Null(exception.Observation.ReturnValue);
        Assert.Equal(failure.GetType().Name, exception.Observation.NativeException);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Given_NativeReturnFailure_When_HostReportsFailure_Then_EmitsActualReturnAndErrno(int signal)
    {
        var exception = CaptureSignalFailure(signal, (_, requested) => new(requested, -1, 13));
        using var diagnostic = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(exception, diagnostic));
        string line = Assert.Single(diagnostic.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var emitted = JsonDocument.Parse(line);
        B10AbruptWire.ExactMembers(emitted.RootElement, "failure", "exception_type", "diagnostic", "signal_observation");
        Assert.Equal(signal == 0 ? "b10_abrupt_signal_preflight_failed" : "b10_abrupt_signal_failed", emitted.RootElement.GetProperty("failure").GetString());
        Assert.Equal("Parchwell.LoadGen.Run.Abrupt.B10AbruptProcess+SignalException", emitted.RootElement.GetProperty("exception_type").GetString());
        var observation = emitted.RootElement.GetProperty("signal_observation");
        B10AbruptWire.ExactMembers(observation, "requested_signal", "return_value", "errno", "native_exception");
        Assert.Equal(signal, observation.GetProperty("requested_signal").GetInt32());
        Assert.Equal(-1, observation.GetProperty("return_value").GetInt32());
        Assert.Equal(13, observation.GetProperty("errno").GetInt32());
        Assert.Equal(JsonValueKind.Null, observation.GetProperty("native_exception").ValueKind);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(9, 0)]
    [InlineData(9, 1)]
    [InlineData(9, 2)]
    public void Given_NativeImportFailure_When_HostReportsFailure_Then_EmitsTypeWithUnavailableReturnAndErrno(int signal, int kind)
    {
        Exception failure = kind switch { 0 => new DllNotFoundException(), 1 => new EntryPointNotFoundException(), _ => new BadImageFormatException() };
        var exception = CaptureSignalFailure(signal, (_, _) => throw failure);
        using var diagnostic = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(exception, diagnostic));
        string line = Assert.Single(diagnostic.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var emitted = JsonDocument.Parse(line);
        B10AbruptWire.ExactMembers(emitted.RootElement, "failure", "exception_type", "diagnostic", "signal_observation");
        Assert.Equal(signal == 0 ? "b10_abrupt_signal_preflight_failed" : "b10_abrupt_signal_failed", emitted.RootElement.GetProperty("failure").GetString());
        Assert.Equal("Parchwell.LoadGen.Run.Abrupt.B10AbruptProcess+SignalException", emitted.RootElement.GetProperty("exception_type").GetString());
        var observation = emitted.RootElement.GetProperty("signal_observation");
        B10AbruptWire.ExactMembers(observation, "requested_signal", "return_value", "errno", "native_exception");
        Assert.Equal(signal, observation.GetProperty("requested_signal").GetInt32());
        Assert.Equal(JsonValueKind.Null, observation.GetProperty("return_value").ValueKind);
        Assert.Equal(JsonValueKind.Null, observation.GetProperty("errno").ValueKind);
        Assert.Equal(failure.GetType().Name, observation.GetProperty("native_exception").GetString());
    }

    [Fact]
    public void Given_PreflightErrno_When_ChildPreflightRuns_Then_ActualOwnPidAndNullSignalAreRecorded()
    {
        var calls = new List<(int Pid, int Signal)>();
        var exception = Assert.Throws<B10AbruptProcess.SignalException>(() => B10AbruptProcess.PreflightSignalEntryPoint((pid, signal) =>
        { calls.Add((pid, signal)); return new(signal, -1, 13); }));
        Assert.Equal("b10_abrupt_signal_preflight_failed", exception.Message);
        Assert.Equal(-1, exception.Observation.ReturnValue);
        Assert.Equal(13, exception.Observation.Errno);
        Assert.Equal([(Environment.ProcessId, 0)], calls);
    }

    private static B10AbruptProcess.SignalException CaptureSignalFailure(int signal,
        Func<int, int, B10AbruptProcess.SignalObservation> invoke)
    {
        if (signal == 0) return Assert.Throws<B10AbruptProcess.SignalException>(() => B10AbruptProcess.PreflightSignalEntryPoint(invoke));
        B10AbruptProcess.SignalObservation observation;
        try { observation = invoke(Environment.ProcessId, signal); }
        catch (Exception failure) when (failure is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { observation = new(signal, null, null, failure.GetType().Name); }
        return new B10AbruptProcess.SignalException("b10_abrupt_signal_failed", observation);
    }
}
