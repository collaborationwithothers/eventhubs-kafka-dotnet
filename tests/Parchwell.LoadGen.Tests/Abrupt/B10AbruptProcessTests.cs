using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

public sealed class B10AbruptProcessTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Given_OwnedChild_When_StopResumeKill_Then_ObservesStoppedProgressExitAndEof()
    {
        var command = Command();
        if (!OperatingSystem.IsLinux())
        {
            var exception = Assert.Throws<InvalidOperationException>(() => OwnedProducerProcess.Start(command, Options()));
            Assert.Equal("b10_abrupt_platform_unsupported", exception.Message);
            return;
        }
        Assert.Equal(0, OwnedProducerProcess.PreflightSignalEntryPoint().ReturnValue);
        string bindingPath = Path.Combine(Path.GetTempPath(), "b10-start-" + Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(bindingPath, "synthetic-start-line-binding", TestContext.Current.CancellationToken);
        using var bindingCleanup = new FileCleanup(bindingPath);
        using var process = OwnedProducerProcess.Start(command, Options(bindingPath));
        var messages = new ConcurrentQueue<B10AbruptWire.Message>();
        long acknowledgement = 0;
        using var reader = new B10AbruptWire.AvailableMessageReader(process.Events, process.EventsReady, message =>
        {
            messages.Enqueue(message);
            B10AbruptWire.WriteMessageAsync(process.Acknowledgements,
                new("ack", ++acknowledgement, B10AbruptWire.Element(new { acknowledged_sequence = message.Sequence })), TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        });
        var pump = Task.Run(async () =>
        {
            while (!await reader.DrainAvailableAsync(TestContext.Current.CancellationToken))
                await Task.Delay(1, TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);
        await UntilAsync(() => messages.Count >= 3, TimeSpan.FromSeconds(10));
        output.WriteLine($"runtime={Environment.Version};os={RuntimeInformation.OSDescription};kernel={File.ReadAllText("/proc/sys/kernel/osrelease").Trim()};start={TimeProvider.System.GetUtcNow():O}");
        var notExited = process.ObserveExit(0);
        Assert.False(notExited.WaitForExitObserved);
        Assert.Null(notExited.ExitCodeLocalObservation);
        Assert.Equal(0, process.RequestStop().ReturnValue);
        Assert.True(process.ConfirmStopped());
        output.WriteLine($"stopped={TimeProvider.System.GetUtcNow():O}");
        Assert.False(await reader.DrainAvailableAsync(TestContext.Current.CancellationToken));
        Assert.False(process.EventsReady());
        int count = messages.Count;
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal(count, messages.Count);
        Assert.Equal(0, process.Resume().ReturnValue);
        await UntilAsync(() => messages.Count > count, TimeSpan.FromSeconds(5));
        output.WriteLine($"resumed_progress={TimeProvider.System.GetUtcNow():O}");
        Assert.Equal(0, process.RequestKill().ReturnValue);
        output.WriteLine($"kill_requested={TimeProvider.System.GetUtcNow():O}");
        var exit = process.ObserveExit();
        Assert.True(exit.WaitForExitObserved);
        Assert.NotNull(exit.ExitCodeLocalObservation);
        await pump.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        output.WriteLine($"exit={TimeProvider.System.GetUtcNow():O};exit_code_local_observation={exit.ExitCodeLocalObservation};eof=true;heartbeats={messages.Count}");
    }

    [Theory]
    [InlineData("worker", "123")]
    [InlineData("worker (nested) space", "9223372036854775807")]
    [InlineData("a ) ) ( b", "0")]
    public void Given_CommContainsParentheses_When_ParseStartTime_Then_UsesLastDelimiter(string comm, string ticks)
    {
        var fields = Enumerable.Repeat("0", 23).ToArray();
        fields[0] = "S"; fields[19] = ticks;
        Assert.Equal(long.Parse(ticks, CultureInfo.InvariantCulture), B10AbruptProc.ReadStartTimeTicks(Encoding.ASCII.GetBytes("7 (" + comm + ")   " + string.Join("  ", fields) + "  ")));
    }

    [Theory]
    [InlineData("no delimiter")]
    [InlineData("1 (x) S 1")]
    [InlineData("1 (x) S 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 -1")]
    [InlineData("1 (x) S 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 9223372036854775808")]
    [InlineData("1 (x) S 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1.5")]
    public void Given_MalformedStat_When_Parse_Then_Rejects(string stat) =>
        Assert.Equal("b10_abrupt_child_identity_mismatch", Assert.Throws<InvalidDataException>(() => B10AbruptProc.ReadStartTimeTicks(Encoding.ASCII.GetBytes(stat))).Message);

    [Fact]
    public void Given_PreflightErrno_When_Start_Then_FailsBeforeExecutableLaunch()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            var unsupported = Assert.Throws<InvalidOperationException>(() => OwnedProducerProcess.Start(
                new("/does/not/exist", [], new Dictionary<string, string>()), Options(),
                (_, _) => throw new InvalidOperationException("preflight_must_not_run")));
            Assert.Equal("b10_abrupt_platform_unsupported", unsupported.Message);
            return;
        }
        var calls = new List<(int Pid, int Signal)>();
        var exception = Assert.Throws<OwnedProducerProcess.SignalException>(() => OwnedProducerProcess.Start(
            new("/does/not/exist", [], new Dictionary<string, string>()), Options(), (pid, signal) =>
            { calls.Add((pid, signal)); return new(signal, -1, 13); }));
        Assert.Equal("b10_abrupt_signal_preflight_failed", exception.Message);
        Assert.Equal(-1, exception.Observation.ReturnValue);
        Assert.Equal(13, exception.Observation.Errno);
        Assert.Equal([(Environment.ProcessId, 0)], calls);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("target")]
    [InlineData("errno")]
    [InlineData("import")]
    public void Given_HandshakeFailure_When_StartupCleanupRuns_Then_PreservesCauseAndActualCleanupObservation(string cleanupKind)
    {
        var cause = new InvalidDataException("b10_abrupt_child_identity_mismatch");
        Exception? cleanupFailure = cleanupKind switch
        {
            "target" => new InvalidOperationException("b10_abrupt_target_invalid"),
            "errno" => CaptureSignalFailure(9, (_, signal) => new(signal, -1, 13)),
            "import" => CaptureSignalFailure(9, (_, _) => throw new EntryPointNotFoundException()),
            _ => null
        };
        int cleanups = 0;
        var failure = OwnedProducerProcess.CleanupStartupFailure(cause, () =>
        {
            cleanups++;
            if (cleanupFailure is not null) throw cleanupFailure;
        });
        Assert.Equal(1, cleanups);
        Assert.Equal("b10_abrupt_child_identity_mismatch", failure.Message);
        Assert.Same(cause, failure.InnerException);
        Assert.Same(cleanupFailure, failure.CleanupFailure);
        using var diagnostic = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(failure, diagnostic));
        using var emitted = JsonDocument.Parse(diagnostic.ToString());
        B10AbruptWire.ExactMembers(emitted.RootElement, "failure", "cleanup_failure", "cleanup_signal_observation");
        Assert.Equal("b10_abrupt_child_identity_mismatch", emitted.RootElement.GetProperty("failure").GetString());
        Assert.Equal(cleanupFailure?.Message, emitted.RootElement.GetProperty("cleanup_failure").GetString());
        var observation = emitted.RootElement.GetProperty("cleanup_signal_observation");
        if (cleanupFailure is OwnedProducerProcess.SignalException signalFailure)
        {
            Assert.Equal(signalFailure.Observation, observation.Deserialize<OwnedProducerProcess.SignalObservation>(B10AbruptWire.JsonOptions));
            Assert.Equal(9, observation.GetProperty("requested_signal").GetInt32());
            if (cleanupKind == "errno")
            {
                Assert.Equal(-1, observation.GetProperty("return_value").GetInt32());
                Assert.Equal(13, observation.GetProperty("errno").GetInt32());
                Assert.Equal(JsonValueKind.Null, observation.GetProperty("native_exception").ValueKind);
            }
            else
            {
                Assert.Equal(JsonValueKind.Null, observation.GetProperty("return_value").ValueKind);
                Assert.Equal(JsonValueKind.Null, observation.GetProperty("errno").ValueKind);
                Assert.Equal(nameof(EntryPointNotFoundException), observation.GetProperty("native_exception").GetString());
            }
        }
        else Assert.Equal(JsonValueKind.Null, observation.ValueKind);
    }

    [Fact]
    public void Given_ExecutableLaunchFailure_When_StartupCleanupFails_Then_PreservesStartFailureName()
    {
        var cause = new InvalidOperationException("b10_abrupt_child_start_failed");
        var cleanup = new IOException("synthetic_cleanup_failure");
        var failure = OwnedProducerProcess.CleanupStartupFailure(cause, () => throw cleanup);
        Assert.Equal("b10_abrupt_child_start_failed", failure.Message);
        Assert.Same(cause, failure.InnerException);
        Assert.Same(cleanup, failure.CleanupFailure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Given_NativeLoadFailure_When_Preflight_Then_RetainsTypeWithoutFabricatedErrno(int kind)
    {
        Exception failure = kind switch { 0 => new DllNotFoundException(), 1 => new EntryPointNotFoundException(), _ => new BadImageFormatException() };
        var exception = Assert.Throws<OwnedProducerProcess.SignalException>(() => OwnedProducerProcess.PreflightSignalEntryPoint((_, _) => throw failure));
        Assert.Equal("b10_abrupt_signal_preflight_failed", exception.Message);
        Assert.Null(exception.Observation.Errno);
        Assert.Null(exception.Observation.ReturnValue);
        Assert.Equal(failure.GetType().Name, exception.Observation.NativeException);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(18)]
    [InlineData(19)]
    public void Given_NativeReturnFailure_When_HostReportsFailure_Then_EmitsActualReturnAndErrno(int signal)
    {
        var exception = CaptureSignalFailure(signal, (_, requested) => new(requested, -1, 13));
        using var diagnostic = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(exception, diagnostic));
        string line = Assert.Single(diagnostic.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var emitted = JsonDocument.Parse(line);
        B10AbruptWire.ExactMembers(emitted.RootElement, "failure", "signal_observation");
        Assert.Equal(signal == 0 ? "b10_abrupt_signal_preflight_failed" : "b10_abrupt_signal_failed", emitted.RootElement.GetProperty("failure").GetString());
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
    [InlineData(18, 0)]
    [InlineData(18, 1)]
    [InlineData(18, 2)]
    [InlineData(19, 0)]
    [InlineData(19, 1)]
    [InlineData(19, 2)]
    public void Given_NativeImportFailure_When_HostReportsFailure_Then_EmitsTypeWithUnavailableReturnAndErrno(int signal, int kind)
    {
        Exception failure = kind switch { 0 => new DllNotFoundException(), 1 => new EntryPointNotFoundException(), _ => new BadImageFormatException() };
        var exception = CaptureSignalFailure(signal, (_, _) => throw failure);
        using var diagnostic = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(exception, diagnostic));
        string line = Assert.Single(diagnostic.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var emitted = JsonDocument.Parse(line);
        B10AbruptWire.ExactMembers(emitted.RootElement, "failure", "signal_observation");
        Assert.Equal(signal == 0 ? "b10_abrupt_signal_preflight_failed" : "b10_abrupt_signal_failed", emitted.RootElement.GetProperty("failure").GetString());
        var observation = emitted.RootElement.GetProperty("signal_observation");
        B10AbruptWire.ExactMembers(observation, "requested_signal", "return_value", "errno", "native_exception");
        Assert.Equal(signal, observation.GetProperty("requested_signal").GetInt32());
        Assert.Equal(JsonValueKind.Null, observation.GetProperty("return_value").ValueKind);
        Assert.Equal(JsonValueKind.Null, observation.GetProperty("errno").ValueKind);
        Assert.Equal(failure.GetType().Name, observation.GetProperty("native_exception").GetString());
    }

    private static OwnedProducerProcess.SignalException CaptureSignalFailure(int signal,
        Func<int, int, OwnedProducerProcess.SignalObservation> invoke)
    {
        if (signal == 0) return Assert.Throws<OwnedProducerProcess.SignalException>(() => OwnedProducerProcess.PreflightSignalEntryPoint(invoke));
        var target = new OwnedProducerProcess.ProcessIdentity(5, 10, "boot", 1);
        return Assert.Throws<OwnedProducerProcess.SignalException>(() => OwnedProducerProcess.RequestSignal(target, target, false, 10, signal, invoke));
    }

    [Theory]
    [InlineData(0, 10, 1, false)]
    [InlineData(5, 11, 1, false)]
    [InlineData(5, 10, 2, false)]
    [InlineData(5, 10, 1, true)]
    public void Given_UnownedOrChangedTarget_When_Validate_Then_RejectsBeforeSignal(int pid, int parent, long ticks, bool exited)
    {
        var original = new OwnedProducerProcess.ProcessIdentity(5, 10, "boot", 1);
        var current = new OwnedProducerProcess.ProcessIdentity(pid, parent, "boot", ticks);
        Assert.Equal("b10_abrupt_target_invalid", Assert.Throws<InvalidOperationException>(() => OwnedProducerProcess.ValidateTarget(original, current, exited, 10)).Message);
    }

    [Fact]
    public void Given_SignalFailure_When_Request_Then_RetainsActualErrno()
    {
        var target = new OwnedProducerProcess.ProcessIdentity(5, 10, "boot", 1);
        var exception = Assert.Throws<OwnedProducerProcess.SignalException>(() => OwnedProducerProcess.RequestSignal(target, target, false, 10, 19,
            (pid, signal) => { Assert.Equal(5, pid); Assert.Equal(19, signal); return new(signal, -1, 3); }));
        Assert.Equal("b10_abrupt_signal_failed", exception.Message);
        Assert.Equal(-1, exception.Observation.ReturnValue);
        Assert.Equal(3, exception.Observation.Errno);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1)]
    public void Given_NonArmSignal_When_Request_Then_NativeBoundaryIsNotCalled(int signal)
    {
        var target = new OwnedProducerProcess.ProcessIdentity(5, 10, "boot", 1);
        int calls = 0;
        Assert.Equal("b10_abrupt_target_invalid", Assert.Throws<InvalidOperationException>(() => OwnedProducerProcess.RequestSignal(target, target, false, 10, signal,
            (_, _) => { calls++; return new(signal, 0, null); })).Message);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("T", 1, 10)]
    [InlineData("t", 0, 10)]
    [InlineData("Z", 0, 10)]
    [InlineData("?", 0, 10)]
    [InlineData("S", 0, 11)]
    public void Given_TracedAmbiguousOrForeignProc_When_ReadIdentity_Then_Rejects(string state, int tracer, int parent)
    {
        var fields = Enumerable.Repeat("0", 23).ToArray();
        fields[0] = state; fields[1] = "10"; fields[19] = "1";
        byte[] stat = Encoding.ASCII.GetBytes("5 (worker) " + string.Join(' ', fields));
        Assert.Equal("b10_abrupt_target_invalid", Assert.Throws<InvalidOperationException>(() => OwnedProducerProcess.ParseIdentity(5, stat,
            $"PPid:\t{parent}\nTracerPid:\t{tracer}\n", "boot")).Message);
    }

    [Theory]
    [InlineData(65535, true)]
    [InlineData(65536, true)]
    [InlineData(65537, false)]
    public void Given_ProcReadByteBoundary_When_Read_Then_RejectsExcess(int count, bool valid)
    {
        string path = Path.Combine(Path.GetTempPath(), "b10-proc-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(path, new byte[count]);
            if (valid) Assert.Equal(count, B10AbruptProc.ReadBounded(path).Length);
            else Assert.Throws<IOException>(() => B10AbruptProc.ReadBounded(path));
        }
        finally { File.Delete(path); }
        Assert.Throws<FileNotFoundException>(() => B10AbruptProc.ReadBounded(path));
    }

    [Fact]
    public void Given_SdkDotnetParent_When_ApphostCommandIsValidated_Then_RejectsWithNamedDetail()
    {
        var productionFailure = Assert.Throws<InvalidOperationException>(() => ChildCommand.ForRunningApphost("/app/", "/usr/share/dotnet/dotnet"));
        Assert.Equal("ApphostProcessPathMismatch", B10AbruptOptions.ReadValidationDetail(productionFailure));
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        string expected = Path.Combine(directory, "Parchwell.LoadGen");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(expected, "synthetic-apphost");
            var failure = Assert.Throws<InvalidOperationException>(() => ChildCommand.ForRunningApphost(directory, "/usr/share/dotnet/dotnet"));
            using var errors = new StringWriter(CultureInfo.InvariantCulture);
            Assert.Equal(1, B10AbruptHost.ReportFailure(failure, errors));
            Assert.Equal("b10_abrupt_configuration_invalid validation_detail=ApphostProcessPathMismatch" + Environment.NewLine, errors.ToString());
            ChildCommand command = ChildCommand.ForRunningApphost(directory, expected);
            Assert.Equal(expected, command.FileName);
            Assert.Equal(["--b10-abrupt-child"], command.Arguments);
            Assert.Empty(command.ExtraEnvironment);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Given_MissingPublishedApphost_When_ApphostCommandIsValidated_Then_RejectsWithDistinctDetail()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var failure = Assert.Throws<InvalidOperationException>(() => ChildCommand.ForRunningApphost(directory, Path.Combine(directory, "Parchwell.LoadGen")));
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(failure, errors));
        Assert.Equal("b10_abrupt_configuration_invalid validation_detail=ApphostMissing" + Environment.NewLine, errors.ToString());
    }

    internal static B10AbruptOptions Options(string bindingPath = "/work/start-line.json") => B10AbruptOptions.Read(new Dictionary<string, string>
    {
        ["B10Abrupt__Enabled"] = "true", ["B10Abrupt__Purpose"] = "diagnostic_trial", ["B10Abrupt__Role"] = "supervisor",
        ["B10Abrupt__RunId"] = "test", ["B10Abrupt__ScenarioId"] = "B10", ["B10Abrupt__ConfigurationId"] = "C1", ["B10Abrupt__Phase"] = "C1",
        ["B10Abrupt__Repeat"] = "1", ["B10Abrupt__SourceCommit"] = new('a', 40), ["B10Abrupt__ImageReference"] = "example/image@sha256:" + new string('b', 64),
        ["B10Abrupt__JobName"] = "test-c1-r1-loadgen", ["B10Abrupt__PodName"] = "first", ["B10Abrupt__PodUid"] = "uid",
        ["B10Abrupt__NamespaceName"] = "parchwell", ["B10Abrupt__BlobEndpoint"] = "https://example.blob.core.windows.net",
        ["B10Abrupt__ScenarioSourceRoot"] = "/scenarios", ["B10Abrupt__OutputRoot"] = "/work/loadgen", ["B10Abrupt__StartLineBindingPath"] = bindingPath,
    });

    private sealed class FileCleanup(string path) : IDisposable
    {
        public void Dispose() => File.Delete(path);
    }

    private static ChildCommand Command()
    {
        string executable = Environment.ProcessPath!;
        List<string> arguments = ["-class", "Parchwell.LoadGen.Tests.Abrupt.B10AbruptProcessChildWorker"];
        if (Path.GetFileNameWithoutExtension(executable) == "dotnet") arguments.Insert(0, typeof(B10AbruptProcessChildWorker).Assembly.Location);
        return new(executable, arguments, new Dictionary<string, string> { ["PARCHWELL_B10_ABRUPT_PROCESS_CHILD"] = "1" });
    }

    private static async Task UntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!condition()) await Task.Delay(10, cancellation.Token);
    }
}

public sealed class B10AbruptProcessChildWorker
{
    [Fact]
    public async Task Given_ChildWorkerFlag_When_Run_Then_SendsHeartbeats()
    {
        if (Environment.GetEnvironmentVariable("PARCHWELL_B10_ABRUPT_PROCESS_CHILD") != "1") return;
        Assert.Equal("synthetic-start-line-binding", await File.ReadAllTextAsync(Environment.GetEnvironmentVariable("B10Abrupt__StartLineBindingPath")!, TestContext.Current.CancellationToken));
        Assert.Null(Environment.GetEnvironmentVariable("B10Abrupt__PredecessorBindingPath"));
        await using var channel = B10AbruptChildChannel.Connect(Environment.GetEnvironmentVariable("B10Abrupt__EventsPipeHandle")!,
            Environment.GetEnvironmentVariable("B10Abrupt__AckPipeHandle")!, Environment.GetEnvironmentVariable("B10Abrupt__IdentityChallenge")!);
        for (int index = 0; index < 600; index++)
        {
            await channel.SendAsync("prepared", B10AbruptWire.Element(new { attempt_id = "heartbeat-" + index.ToString(CultureInfo.InvariantCulture),
                invocation_ordinal = index, cohort = "same_instance", probe_id = "heartbeat", key = "heartbeat", sequence = 0,
                payload_length = 0, payload_sha256 = new string('a', 64), predecessor_attempt_id = (string?)null }), TestContext.Current.CancellationToken);
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }
}

public sealed class B10AbruptPublishedApphostTests
{
    [Fact]
    public void Given_ChartSelectedPublishedApphost_When_PathGuardRuns_Then_SelectsOnlyThatChildExecutable()
    {
        string? apphost = Environment.GetEnvironmentVariable("PARCHWELL_B10_ABRUPT_PUBLISHED_APPHOST");
        if (apphost is null) return;
        Assert.True(File.Exists(apphost));
        Assert.Equal("Parchwell.LoadGen", Path.GetFileName(apphost));
        ChildCommand command = ChildCommand.ForRunningApphost(Path.GetDirectoryName(apphost)!, apphost);
        Assert.Equal(apphost, command.FileName);
        Assert.Equal(["--b10-abrupt-child"], command.Arguments);
        Assert.Empty(command.ExtraEnvironment);
        var failure = Assert.Throws<InvalidOperationException>(() => ChildCommand.ForRunningApphost(Path.GetDirectoryName(apphost)!, "/usr/share/dotnet/dotnet"));
        Assert.Equal("ApphostProcessPathMismatch", B10AbruptOptions.ReadValidationDetail(failure));
    }
}
