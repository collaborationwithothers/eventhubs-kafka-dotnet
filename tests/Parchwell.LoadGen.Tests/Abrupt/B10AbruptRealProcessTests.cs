using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

public sealed class B10AbruptRealProcessTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Given_RealChild_When_SelfKillFollowsDurableIntent_Then_ParentObserves137AndCompleteJournal()
    {
        using var fixture = new B10AbruptProcessFixture("kill");
        var parentSignals = new List<(int Pid, int Signal)>();
        B10AbruptProcess.SignalOverride = (pid, signal) => { parentSignals.Add((pid, signal)); return (0, 0); };
        JsonElement termination;
        try { termination = await fixture.RunParentAsync(); }
        finally { B10AbruptProcess.SignalOverride = null; }
        Assert.Empty(parentSignals);
        JsonElement data = termination.GetProperty("data");
        Assert.Equal("intended_self_kill", data.GetProperty("termination").GetString());
        Assert.Equal(137, data.GetProperty("exit_code_local_observation").GetInt32());
        Assert.True(data.GetProperty("successor_permitted").GetBoolean());
        int pid = data.GetProperty("child").GetProperty("pid").GetInt32();
        Assert.False(Directory.Exists("/proc/" + pid.ToString(CultureInfo.InvariantCulture)));
        Assert.Equal(pid.ToString(CultureInfo.InvariantCulture), Assert.Single(File.ReadAllLines(Path.Combine(fixture.Directory, "child-pids.txt"))));
        byte[] bytes = File.ReadAllBytes(fixture.JournalPath);
        Assert.Equal((byte)'\n', bytes[^1]);
        JsonElement[] records = fixture.Records();
        Assert.Equal("child_started", records[0].GetProperty("kind").GetString());
        Assert.Equal(pid, records[0].GetProperty("data").GetProperty("pid").GetInt32());
        Assert.Equal("kill_intent", records[^1].GetProperty("kind").GetString());
        Assert.Equal(Enumerable.Range(1, records.Length).Select(value => (long)value), records.Select(record => record.GetProperty("sequence").GetInt64()));
        output.WriteLine($"local_observation exit_code_local_observation=137 runtime={Environment.Version} kernel={File.ReadAllText("/proc/sys/kernel/osrelease").Trim()}");
    }

    [Fact]
    public async Task Given_RealSleepingChild_When_ParentWaitBoundExpires_Then_UnknownChildRemainsAliveUntilTestCleanup()
    {
        using var fixture = new B10AbruptProcessFixture("sleep");
        JsonElement termination = await fixture.RunParentAsync(TimeSpan.FromSeconds(2));
        JsonElement data = termination.GetProperty("data");
        Assert.Equal("unknown", data.GetProperty("termination").GetString());
        Assert.Equal("b10_abrupt_exit_unobserved", data.GetProperty("outcome").GetString());
        Assert.False(data.GetProperty("successor_permitted").GetBoolean());
        int pid = int.Parse(File.ReadAllText(Path.Combine(fixture.Directory, "child.pid")), CultureInfo.InvariantCulture);
        using Process child = Process.GetProcessById(pid);
        Assert.False(child.HasExited);
        child.Kill(); // The test alone reaps the deliberately sleeping child after checking the parent left it alive.
        Assert.True(child.WaitForExit(10000));
    }

    [Theory]
    [InlineData("empty", "b10_abrupt_exposure_inconclusive")]
    [InlineData("incomplete", "b10_abrupt_control_incomplete")]
    public async Task Given_RealChildBeforeCrossAdmission_When_ControlCannotContinue_Then_NoKillExitsThreeAndDisposes(string mode, string reason)
    {
        using var fixture = new B10AbruptProcessFixture(mode);
        JsonElement termination = await fixture.RunParentAsync();
        JsonElement data = termination.GetProperty("data");
        Assert.Equal("no_kill", data.GetProperty("termination").GetString());
        Assert.Equal(3, data.GetProperty("exit_code_local_observation").GetInt32());
        Assert.Equal(reason, data.GetProperty("outcome").GetString());
        JsonElement[] records = fixture.Records();
        Assert.Equal("no_kill", records[^1].GetProperty("kind").GetString());
        Assert.DoesNotContain(records, record => record.GetProperty("kind").GetString() == "prepared" && record.GetProperty("data").GetProperty("arm").GetString() == "cross_restart");
        Assert.Contains("Dispose", File.ReadAllLines(Path.Combine(fixture.Directory, "producer-calls.txt")));
    }

    [Fact]
    public async Task Given_ReturnedKillInRealChild_When_ParentClassifies_Then_ActualSignalFailureIsRetained()
    {
        using var fixture = new B10AbruptProcessFixture("signal-failed");
        JsonElement termination = await fixture.RunParentAsync();
        JsonElement data = termination.GetProperty("data");
        Assert.Equal(4, data.GetProperty("exit_code_local_observation").GetInt32());
        Assert.Equal("unexpected_exit", data.GetProperty("termination").GetString());
        Assert.Equal("b10_abrupt_signal_failed", data.GetProperty("outcome").GetString());
        Assert.False(data.GetProperty("successor_permitted").GetBoolean());
        Assert.Equal(-1, data.GetProperty("signal_observation").GetProperty("return_value").GetInt32());
        Assert.Equal(1, data.GetProperty("signal_observation").GetProperty("errno").GetInt32());
        JsonElement[] records = fixture.Records();
        Assert.Equal("kill_intent", records[^2].GetProperty("kind").GetString());
        Assert.Equal("signal_failed", records[^1].GetProperty("kind").GetString());
        Assert.Equal(-1, records[^1].GetProperty("data").GetProperty("return_value").GetInt32());
        Assert.Equal(1, records[^1].GetProperty("data").GetProperty("errno").GetInt32());
        Assert.DoesNotContain(records, record => record.GetProperty("kind").GetString() == "no_kill");
    }

    [Fact]
    public async Task Given_ProductionHostAfterCrossAdmission_When_NoKillExits_Then_ProcessExitHandlersDoNotFlushOrDisposeProducer()
    {
        // The child runs B10AbruptHost's production service composition and host lifetime unchanged,
        // with only the producer factory replaced by the recording seam. This includes ProcessExit handlers.
        using var fixture = new B10AbruptProcessFixture("host-cross-failure", realTime: true) { PreserveArtifacts = true };
        int code = await fixture.RunChildAsync(TimeSpan.FromSeconds(160));
        Assert.Equal(3, code);
        JsonElement[] records = fixture.Records();
        JsonElement noKill = records[^1];
        output.WriteLine("journal_tail=" + JsonSerializer.Serialize(records.TakeLast(8)));
        output.WriteLine("producer_calls=" + string.Join(',', File.ReadAllLines(Path.Combine(fixture.Directory, "producer-calls.txt"))));
        output.WriteLine("process_output_tail=" + fixture.ProcessOutput);
        Assert.Equal("no_kill", noKill.GetProperty("kind").GetString());
        Assert.True(noKill.GetProperty("data").GetProperty("cross_restart_admitted").GetBoolean());
        string[] calls = File.ReadAllLines(Path.Combine(fixture.Directory, "producer-calls.txt"));
        int admission = Array.IndexOf(calls, "cross_produce");
        Assert.True(admission >= 0);
        Assert.DoesNotContain("Flush", calls.Skip(admission));
        Assert.DoesNotContain("Dispose", calls.Skip(admission));
        fixture.PreserveArtifacts = false;
    }
}

internal sealed class B10AbruptProcessFixture : IDisposable
{
    internal string Directory { get; } = Path.Combine(Path.GetTempPath(), "b10-process-" + Guid.NewGuid().ToString("N"));
    internal string StorageRoot { get; }
    internal string ProcessOutput { get; private set; } = "";
    internal bool PreserveArtifacts { get; set; }
    internal string JournalPath => Path.Combine(Directory, "abrupt", "journal.jsonl");
    internal Dictionary<string, string> EnvironmentValues { get; }
    internal B10AbruptProcessFixture(string mode, string bootstrapServers = "", bool realTime = false, string? storageRoot = null)
    {
        StorageRoot = storageRoot ?? Path.Combine(Directory, "storage");
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllBytes(Path.Combine(Directory, "test-control.json"), JsonSerializer.SerializeToUtf8Bytes(new B10AbruptProcessHarness.Control(mode, StorageRoot, bootstrapServers, realTime)));
        string binding = Path.Combine(Directory, "start-line.json");
        var identity = new { scenario_id = "B10", configuration_id = "C1", run_id = "test" };
        File.WriteAllBytes(binding, JsonSerializer.SerializeToUtf8Bytes(new
        {
            start_line = "synthetic-start-line", completion = "synthetic-completion",
            broker_inventory = JsonSerializer.Serialize(new { schema_version = 1, identity, brokers = new[] { new { name = "broker" } }, source = "kafka_metadata", source_commit = new string('a', 40) })
        }));
        EnvironmentValues = new(StringComparer.Ordinal)
        {
            ["B10Abrupt__Enabled"] = "true", ["B10Abrupt__Purpose"] = "diagnostic_trial", ["B10Abrupt__Role"] = "parent",
            ["B10Abrupt__RunId"] = "test", ["B10Abrupt__ScenarioId"] = "B10", ["B10Abrupt__ConfigurationId"] = "C1", ["B10Abrupt__Phase"] = "C1",
            ["B10Abrupt__Repeat"] = "1", ["B10Abrupt__SourceCommit"] = new('a', 40), ["B10Abrupt__ImageReference"] = "example/image@sha256:" + new string('b', 64),
            ["B10Abrupt__JobName"] = "test-c1-r1-loadgen-restart", ["B10Abrupt__PodName"] = "first", ["B10Abrupt__PodUid"] = "uid",
            ["B10Abrupt__NamespaceName"] = "parchwell", ["B10Abrupt__BlobEndpoint"] = "https://example.blob.core.windows.net",
            ["B10Abrupt__ScenarioSourceRoot"] = AppContext.BaseDirectory, ["B10Abrupt__OutputRoot"] = Directory,
            ["B10Abrupt__StartLineBindingPath"] = binding
        };
    }
    internal B10AbruptOptions Options => B10AbruptOptions.Read(EnvironmentValues);
    internal ChildCommand Command => new(Apphost(), ["--b10-abrupt-child"], new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DOTNET_STARTUP_HOOKS"] = typeof(B10AbruptProcessHarness).Assembly.Location,
        ["DOTNET_ROOT"] = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "..", "..", "..")),
        ["Telemetry__ConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://127.0.0.1:1/;LiveEndpoint=https://127.0.0.1:1/",
        ["AZURE_TENANT_ID"] = "synthetic-tenant", ["AZURE_CLIENT_ID"] = "synthetic-client", ["AZURE_FEDERATED_TOKEN_FILE"] = Path.Combine(Directory, "unused-token")
    });
    internal async Task<JsonElement> RunParentAsync(TimeSpan? bound = null)
    {
        var clock = TimeProvider.System;
        var publisher = new B10AbruptPublisher(new B10AbruptProcessHarness.FileStorage(StorageRoot), clock);
        var observation = new B10AbruptCgroup.Observation("test-boot", "/test", "test-inode", 0);
        var parent = new B10AbruptParent(Options, clock, publisher, Command, bound ?? TimeSpan.FromSeconds(45), () => observation, _ => observation);
        return await parent.RunAsync(TestContext.Current.CancellationToken);
    }
    internal Task<int> RunChildAsync(TimeSpan bound) => RunRoleAsync("child", bound);
    internal async Task<int> RunRoleAsync(string role, TimeSpan bound)
    {
        var start = new ProcessStartInfo(Apphost()) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("--b10-abrupt-" + role);
        foreach (var pair in EnvironmentValues) start.Environment[pair.Key] = pair.Value;
        start.Environment["B10Abrupt__Role"] = role;
        foreach (var pair in Command.ExtraEnvironment) start.Environment[pair.Key] = pair.Value;
        using Process child = Process.Start(start) ?? throw new InvalidOperationException("synthetic_child_start_failed");
        Task<string> stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        try
        {
            await child.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(bound, TestContext.Current.CancellationToken);
            string errors = await stderr;
            string standard = await stdout;
            string combined = standard + errors;
            ProcessOutput = combined[^Math.Min(combined.Length, 16384)..];
            if (child.ExitCode is not (0 or 3 or 4 or 137)) throw new InvalidOperationException(standard + errors);
            return child.ExitCode;
        }
        finally
        {
            if (!child.HasExited) { child.Kill(entireProcessTree: true); Assert.True(child.WaitForExit(10000)); }
        }
    }
    internal JsonElement[] Records() => File.ReadAllLines(JournalPath).Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
    internal static string Apphost()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Parchwell.LoadGen");
        if (!File.Exists(path))
        {
            string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Parchwell.LoadGen", "bin", configuration, "net10.0", "Parchwell.LoadGen"));
            File.Copy(source, path);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        return path;
    }
    public void Dispose()
    {
        if (PreserveArtifacts) Console.WriteLine("retained_process_fixture=" + Directory);
        else System.IO.Directory.Delete(Directory, recursive: true);
    }
}
