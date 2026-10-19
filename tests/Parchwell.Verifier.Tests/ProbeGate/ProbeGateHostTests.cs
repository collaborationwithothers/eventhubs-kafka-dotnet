using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Parchwell.Verifier.ProbeGate;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.ProbeGate;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class ProbeGateHostTests
{
    [Theory]
    [InlineData("pass", "pass")]
    [InlineData("untestable", "untestable_at_build_scale")]
    [InlineData("missing", "inconclusive")]
    [InlineData("unreadable", "inconclusive")]
    public async Task Given_LocalRepeatFiles_When_ProductionProcessRuns_Then_WritesMatchingGateOutcomeAndExitsZero(string state, string expectedOutcome)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        if (state == "untestable")
            foreach (string path in fixture.Options.RepeatResultPaths!)
                fixture.Inputs[path] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(fixture.Inputs[path]).Replace("\"qualifying\":true", "\"qualifying\":false", StringComparison.Ordinal));
        if (state == "missing") fixture.Inputs.Remove(fixture.Options.RepeatResultPaths![0]);
        if (state == "unreadable") fixture.Inputs[fixture.Options.RepeatResultPaths![0]] = "not-json"u8.ToArray();
        fixture.WriteLocalInputs();
        (int exitCode, string log) = await RunProductionAsync(fixture.Arguments());
        Assert.True(exitCode == 0, log);
        using JsonDocument output = JsonDocument.Parse(await File.ReadAllBytesAsync(fixture.Options.GateOutputPath!, TestContext.Current.CancellationToken));
        Assert.Equal(expectedOutcome, output.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(5, output.RootElement.GetProperty("repeats").GetArrayLength());
        if (state is "missing" or "unreadable")
        {
            JsonElement repeat = output.RootElement.GetProperty("repeats")[0];
            Assert.Equal(state == "missing" ? "missing_result" : "unreadable_result", repeat.GetProperty("reason").GetString());
            Assert.Equal(JsonValueKind.Null, repeat.GetProperty("identity").ValueKind);
        }
        Assert.Equal(["schema_version", "scenario_id", "outcome", "reasons", "repeats", "verifier_commit"], output.RootElement.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task Given_TrackedLocalGraph_When_HostRuns_Then_OnlyScenarioProfileAndSuppliedRepeatsAreOpenedAndCleanupPrecedesCreate()
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        Assert.Equal(0, await fixture.RunHostAsync());
        Assert.Equal(["scenarios/A01/scenario.yaml", "scenarios/profiles/saturation-ramp.yaml"], fixture.ScenarioOpens);
        Assert.Equal(fixture.Options.RepeatResultPaths, fixture.InputOpens);
        Assert.Equal(1, fixture.Creates);
        int create = fixture.Events.FindIndex(value => value.StartsWith("create:", StringComparison.Ordinal));
        Assert.Equal(5, fixture.Events.Take(create).Count(value => value.StartsWith("dispose:", StringComparison.Ordinal)));
        Assert.DoesNotContain(fixture.ScenarioOpens.Concat(fixture.InputOpens), value => value.Contains("expected.yaml", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("count")]
    [InlineData("actual-count")]
    [InlineData("duplicate")]
    [InlineData("foreign-scenario")]
    [InlineData("foreign-configuration")]
    [InlineData("all")]
    public async Task Given_RootFailure_When_HostRuns_Then_WritesCompleteReasonsAndOpensNoResults(string failure)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        ConfigureRootFailure(fixture, failure);
        fixture.InputStream = _ => throw new InvalidOperationException("Root failures must not open inputs.");
        fixture.Options.MaxDiagnosticBytes = 1;
        fixture.Options.MaxDiagnosticsPerCheck = 1;
        Assert.Equal(0, await fixture.RunHostAsync());
        Assert.Empty(fixture.InputOpens);
        Assert.Equal(1, fixture.Creates);
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        Assert.Equal("inconclusive", output.RootElement.GetProperty("outcome").GetString());
        Assert.Empty(output.RootElement.GetProperty("repeats").EnumerateArray());
        string[] expected = failure switch
        {
            "count" => ["expected_repeat_count"],
            "actual-count" => ["actual_repeat_count"],
            "duplicate" => ["duplicate_expected_run"],
            "all" => ["expected_repeat_count", "actual_repeat_count", "duplicate_expected_run", "foreign_expected_run"],
            _ => ["foreign_expected_run"]
        };
        Assert.Equal(expected, output.RootElement.GetProperty("reasons").EnumerateArray().Select(value => value.GetString()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Given_EmptyOrFourExpectedAndActualEntries_When_HandlerRuns_Then_RootPreflightReadsNothing(int count)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        fixture.Options.ExpectedRepeats = fixture.Options.ExpectedRepeats!.Take(count).ToList();
        fixture.Options.RepeatResultPaths = fixture.Options.RepeatResultPaths!.Take(count).ToList();
        Assert.Equal(0, await fixture.Handler().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Empty(fixture.InputOpens);
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        Assert.Equal(["expected_repeat_count", "actual_repeat_count"], output.RootElement.GetProperty("reasons").EnumerateArray().Select(value => value.GetString()));
        Assert.Empty(output.RootElement.GetProperty("repeats").EnumerateArray());
    }

    [Theory]
    [InlineData("unauthorized")]
    [InlineData("io")]
    [InlineData("malformed")]
    public async Task Given_UnreadableIndividualRepeat_When_HostRuns_Then_PublishesInconclusiveAndReturnsZero(string failure)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        string first = fixture.Options.RepeatResultPaths![0];
        fixture.InputStream = path => path != first ? new MemoryStream(fixture.Inputs[path]) : failure switch
        {
            "unauthorized" => new ProbeGateEvidenceBuilder.FaultStream([], read: () => throw new UnauthorizedAccessException("synthetic")),
            "io" => new ProbeGateEvidenceBuilder.FaultStream([], read: () => throw new IOException("synthetic")),
            _ => new MemoryStream("malformed"u8.ToArray())
        };
        Assert.Equal(0, await fixture.RunHostAsync());
        Assert.Equal(5, fixture.InputOpens.Count);
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        Assert.Equal("inconclusive", output.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("unreadable_result", output.RootElement.GetProperty("repeats")[0].GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("threshold")]
    [InlineData("none")]
    public async Task Given_UnsupportedOrAbsentGate_When_ProductionProcessRuns_Then_ExitsOneWithoutCreatingOutput(string failure)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        string path = Path.Combine(fixture.Root, "scenarios", "A01", "scenario.yaml");
        string yaml = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        if (failure == "threshold") yaml = yaml.Replace("threshold_ms: 20000", "threshold_ms: 20001", StringComparison.Ordinal);
        else
        {
            int start = yaml.IndexOf("gate:\n", StringComparison.Ordinal);
            int end = yaml.IndexOf("\nmeasures:", start, StringComparison.Ordinal);
            yaml = yaml[..start] + "gate:\n  kind: none\n" + yaml[end..];
        }
        await File.WriteAllTextAsync(path, yaml, TestContext.Current.CancellationToken);
        fixture.WriteLocalInputs();
        (int exitCode, string log) = await RunProductionAsync(fixture.Arguments());
        Assert.True(exitCode == 1, log);
        Assert.False(File.Exists(fixture.Options.GateOutputPath));
    }

    [Fact]
    public async Task Given_ExistingOutputSentinel_When_ProductionProcessRuns_Then_ExitsOneAndPreservesExactBytes()
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        fixture.WriteLocalInputs();
        byte[] sentinel = [0, 255, 42, 13, 10];
        await File.WriteAllBytesAsync(fixture.Options.GateOutputPath!, sentinel, TestContext.Current.CancellationToken);
        (int exitCode, string log) = await RunProductionAsync(fixture.Arguments());
        Assert.True(exitCode == 1, log);
        Assert.Equal(sentinel, await File.ReadAllBytesAsync(fixture.Options.GateOutputPath!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_MissingOutputParent_When_ProductionProcessRuns_Then_ExitsOneWithoutCreatingDirectory()
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        fixture.WriteLocalInputs();
        fixture.Options.GateOutputPath = Path.Combine(fixture.Root, "absent", "gate.json");
        (int exitCode, string log) = await RunProductionAsync(fixture.Arguments());
        Assert.True(exitCode == 1, log);
        Assert.False(Directory.Exists(Path.GetDirectoryName(fixture.Options.GateOutputPath)));
    }

    [Theory]
    [InlineData("read-deadline")]
    [InlineData("input-cleanup")]
    [InlineData("cleanup-deadline")]
    [InlineData("write")]
    [InlineData("output-dispose")]
    [InlineData("publication-deadline")]
    public async Task Given_ExecutionOrPublicationFailure_When_HostRuns_Then_ExitsOneWithoutRetryOrDeletion(string failure)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        fixture.InputStream = path => new ProbeGateEvidenceBuilder.FaultStream(fixture.Inputs[path],
            read: failure == "read-deadline" ? () => fixture.Clock.Advance(fixture.Options.IoTimeout!.Value) : null,
            cleanup: failure == "input-cleanup" ? () => throw new IOException("synthetic cleanup") :
                failure == "cleanup-deadline" ? () => fixture.Clock.Advance(fixture.Options.CleanupTimeout!.Value) : null);
        fixture.OutputStream = () => new ProbeGateEvidenceBuilder.FaultStream([], write: failure == "write" ? () => throw new IOException("synthetic write") :
            failure == "publication-deadline" ? () => fixture.Clock.Advance(fixture.Options.IoTimeout!.Value) : null,
            cleanup: failure == "output-dispose" ? () => throw new IOException("synthetic output cleanup") : null);
        Assert.Equal(1, await fixture.RunHostAsync());
        bool created = failure is "write" or "output-dispose" or "publication-deadline";
        Assert.Equal(created ? 1 : 0, fixture.Creates);
        if (!created) Assert.Null(fixture.Output);
        else
        {
            Assert.NotNull(fixture.Output);
            Assert.Single(fixture.Events, value => value.StartsWith("write:", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Given_PartialOutputWriteFailure_When_HostRuns_Then_LeavesPartialBytesWithoutRetry()
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        fixture.OutputStream = () => new PartialWriteStream();
        Assert.Equal(1, await fixture.RunHostAsync());
        Assert.Equal(1, fixture.Creates);
        Assert.Equal("{\"s"u8.ToArray(), fixture.Output);
        Assert.Single(fixture.Events, value => value.StartsWith("write:", StringComparison.Ordinal));
    }

    private sealed class PartialWriteStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            base.Write(buffer.Span[..3]);
            throw new IOException("synthetic partial publication");
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_PerFileByteBoundary_When_HostRuns_Then_AcceptsExactlyTheConfiguredMaximum(int difference)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        long size = fixture.Inputs.Values.Max(bytes => bytes.LongLength);
        fixture.Options.MaxArtifactBytes = size + difference;
        int exitCode = await fixture.RunHostAsync();
        Assert.Equal(difference < 0 ? 1 : 0, exitCode);
        Assert.Equal(difference < 0 ? 0 : 1, fixture.Creates);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_AggregateByteBoundary_When_HostRuns_Then_ChargesEveryActualReadOnce(int difference)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        fixture.Options.MaxTotalInputBytes = fixture.Inputs.Values.Sum(bytes => bytes.LongLength) + difference;
        int exitCode = await fixture.RunHostAsync();
        Assert.Equal(difference < 0 ? 1 : 0, exitCode);
        Assert.Equal(difference < 0 ? 0 : 1, fixture.Creates);
    }

    [Fact]
    public async Task Given_PartialReadBeforeIOException_When_AggregateBudgetIsExceeded_Then_IsExecutionFailure()
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        string first = fixture.Options.RepeatResultPaths![0];
        int calls = 0;
        fixture.InputStream = path => path == first ? new ProbeGateEvidenceBuilder.FaultStream(fixture.Inputs[path], read: () =>
        {
            if (++calls == 2) throw new IOException("synthetic after bytes");
        }) : new MemoryStream(fixture.Inputs[path]);
        fixture.Options.MaxTotalInputBytes = fixture.Inputs[first].LongLength + fixture.Inputs[fixture.Options.RepeatResultPaths[1]].LongLength - 1;
        Assert.Equal(1, await fixture.RunHostAsync());
        Assert.Equal(2, fixture.InputOpens.Count);
        Assert.Equal(0, fixture.Creates);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task Given_InputDepthBoundary_When_HostRuns_Then_DepthExcessIsExecutionFailure(int depth, bool succeeds)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        fixture.Options.MaxInputDepth = depth;
        Assert.Equal(succeeds ? 0 : 1, await fixture.RunHostAsync());
        Assert.Equal(succeeds ? 1 : 0, fixture.Creates);
    }

    [Theory]
    [InlineData("snippet", 0, true)]
    [InlineData("snippet", 1, true)]
    [InlineData("snippet", 2, false)]
    [InlineData("count", 0, true)]
    [InlineData("count", 1, true)]
    [InlineData("count", 2, false)]
    public async Task Given_ActualResultDiagnosticBoundary_When_HostRuns_Then_ExcessIsExecutionFailure(string limit, int amount, bool succeeds)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        fixture.Options.MaxDiagnosticBytes = 1;
        fixture.Options.MaxDiagnosticsPerCheck = 1;
        string diagnostic = "{\"check\":29,\"code\":\"synthetic\",\"artifact\":null,\"row\":null,\"field\":null,\"snippet\":\"" + new string('x', limit == "snippet" ? amount : 0) + "\"}";
        string diagnostics = "[" + string.Join(',', Enumerable.Repeat(diagnostic, limit == "count" ? amount : 1)) + "]";
        fixture.Inputs[fixture.Options.RepeatResultPaths![0]] = ProbeGateEvidenceBuilder.RawResult(ProbeGateEvidenceBuilder.Identity(1), qualifying: true, diagnostics: diagnostics);
        Assert.Equal(succeeds ? 0 : 1, await fixture.RunHostAsync());
        Assert.Equal(succeeds ? 1 : 0, fixture.Creates);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_IndependentlyEncodedGateByteBoundary_When_HostRuns_Then_RetainsAllMetadataOrCreatesNothing(int difference)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        byte[] expected = ExpectedGateBytes(fixture);
        fixture.Options.MaxOutputBytes = expected.LongLength + difference;
        fixture.Options.MaxDiagnosticBytes = 1;
        fixture.Options.MaxDiagnosticsPerCheck = 1;
        Assert.Equal(difference < 0 ? 1 : 0, await fixture.RunHostAsync());
        Assert.Equal(difference < 0 ? 0 : 1, fixture.Creates);
        if (difference < 0) Assert.Null(fixture.Output);
        else Assert.Equal(expected, fixture.Output);
    }

    [Theory]
    [InlineData(999, true)]
    [InlineData(1000, false)]
    [InlineData(1001, false)]
    public async Task Given_InputCleanupDeadlineBoundary_When_HandlerRuns_Then_RequiresCompletedCleanupWithinBudget(int elapsedMilliseconds, bool succeeds)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        fixture.InputStream = path => new ProbeGateEvidenceBuilder.FaultStream(fixture.Inputs[path], cleanup: () => fixture.Clock.Advance(TimeSpan.FromMilliseconds(elapsedMilliseconds)));
        if (succeeds) Assert.Equal(0, await fixture.Handler().ExecuteAsync(TestContext.Current.CancellationToken));
        else await Assert.ThrowsAsync<TimeoutException>(() => fixture.Handler().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(succeeds ? 1 : 0, fixture.Creates);
    }

    [Theory]
    [InlineData(4999, true)]
    [InlineData(5000, false)]
    [InlineData(5001, false)]
    public async Task Given_ReadDeadlineBoundary_When_HandlerRuns_Then_RequiresActualReadsWithinBudget(int elapsedMilliseconds, bool succeeds)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        bool advanced = false;
        fixture.InputStream = path => new ProbeGateEvidenceBuilder.FaultStream(fixture.Inputs[path], read: () =>
        {
            if (advanced) return;
            advanced = true;
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(elapsedMilliseconds));
        });
        if (succeeds) Assert.Equal(0, await fixture.Handler().ExecuteAsync(TestContext.Current.CancellationToken));
        else await Assert.ThrowsAsync<TimeoutException>(() => fixture.Handler().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(succeeds ? 1 : 0, fixture.Creates);
        Assert.Equal(succeeds ? 5 : 1, fixture.InputOpens.Count);
    }

    [Theory]
    [InlineData(4999, true)]
    [InlineData(5000, false)]
    [InlineData(5001, false)]
    public async Task Given_PublicationDeadlineBoundary_When_HandlerRuns_Then_RetainsWrittenOutputWithoutRetry(int elapsedMilliseconds, bool succeeds)
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        fixture.OutputStream = () => new ProbeGateEvidenceBuilder.FaultStream([], write: () => fixture.Clock.Advance(TimeSpan.FromMilliseconds(elapsedMilliseconds)));
        if (succeeds) Assert.Equal(0, await fixture.Handler().ExecuteAsync(TestContext.Current.CancellationToken));
        else await Assert.ThrowsAsync<TimeoutException>(() => fixture.Handler().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, fixture.Creates);
        Assert.Equal(ExpectedGateBytes(fixture), fixture.Output);
        Assert.Single(fixture.Events, value => value.StartsWith("write:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_CancelledOperation_When_HandlerRuns_Then_CreatesNothing()
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Handler().ExecuteAsync(new CancellationToken(canceled: true)));
        Assert.Equal(0, fixture.Creates);
    }

    [Fact]
    public async Task Given_SynchronousBlockedGateRead_When_WatchdogExpires_Then_ProductionHostExits124()
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        ProcessStartInfo start = TestChildStartInfo();
        start.Environment["PARCHWELL_PROBE_GATE_CHILD"] = fixture.Root;
        using var child = Process.Start(start)!;
        Task<string> stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(20));
        try { await child.WaitForExitAsync(limit.Token); }
        finally { if (!child.HasExited) child.Kill(entireProcessTree: true); }
        string log = await stdout + await stderr;
        Assert.True(child.ExitCode == 124, log);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "blocked-gate.json")));
    }

    private static void ConfigureRootFailure(ProbeGateEvidenceBuilder fixture, string failure)
    {
        if (failure == "count") fixture.Options.ExpectedRepeats!.RemoveAt(4);
        if (failure == "actual-count") fixture.Options.RepeatResultPaths!.RemoveAt(4);
        if (failure == "duplicate") fixture.Options.ExpectedRepeats![1] = fixture.Options.ExpectedRepeats[0];
        if (failure == "foreign-scenario") fixture.Options.ExpectedRepeats![0] = fixture.Options.ExpectedRepeats[0] with { ScenarioId = "A02" };
        if (failure == "foreign-configuration") fixture.Options.ExpectedRepeats![0] = fixture.Options.ExpectedRepeats[0] with { ConfigurationId = "C2" };
        if (failure == "all")
        {
            fixture.Options.ExpectedRepeats!.Add(ProbeGateEvidenceBuilder.Expected(ProbeGateEvidenceBuilder.Identity(6) with { ConfigurationId = "C2" }));
            fixture.Options.ExpectedRepeats[1] = fixture.Options.ExpectedRepeats[0];
            fixture.Options.RepeatResultPaths!.RemoveAt(4);
        }
    }

    private static byte[] ExpectedGateBytes(ProbeGateEvidenceBuilder fixture)
    {
        JsonArray repeats = [];
        for (int index = 0; index < 5; index++)
        {
            JsonObject identity = JsonNode.Parse(ProbeGateEvidenceBuilder.IdentityJson(ProbeGateEvidenceBuilder.Identity(index + 1)))!.AsObject();
            repeats.Add(new JsonObject
            {
                ["path"] = fixture.Options.RepeatResultPaths![index], ["identity"] = identity,
                ["usable"] = true, ["qualifying"] = index == 0, ["reason"] = "usable",
                ["observation"] = new JsonObject { ["usable"] = true, ["qualifying"] = index == 0,
                    ["broker"] = "synthetic-long-broker-reference", ["report_index"] = 3, ["window_count"] = 1, ["window_maximum"] = 0 }
            });
        }
        return JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["schema_version"] = 1, ["scenario_id"] = "A01", ["outcome"] = "pass",
            ["reasons"] = new JsonArray(), ["repeats"] = repeats, ["verifier_commit"] = "synthetic-gate-commit" });
    }

    private static async Task<(int ExitCode, string Log)> RunProductionAsync(string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var child = Process.Start(start)!;
        Task<string> stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(40));
        try { await child.WaitForExitAsync(deadline.Token); }
        finally { if (!child.HasExited) child.Kill(entireProcessTree: true); }
        return (child.ExitCode, await stdout + await stderr);
    }

    private static ProcessStartInfo TestChildStartInfo()
    {
        string executable = Environment.ProcessPath!;
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(typeof(ProbeGateWatchdogChildTests).Assembly.Location);
        start.ArgumentList.Add("-class");
        start.ArgumentList.Add(typeof(ProbeGateWatchdogChildTests).FullName!);
        return start;
    }
}

/// <summary>Runs the actual gate handler against a deliberately blocked local stream in isolation.</summary>
public sealed class ProbeGateWatchdogChildTests
{
    [Fact]
    public async Task Given_ChildMarker_When_RunningGate_Then_UsesProductionHandlerAndWatchdog()
    {
        string? root = Environment.GetEnvironmentVariable("PARCHWELL_PROBE_GATE_CHILD");
        if (root is null) return;
        using var fixture = new ProbeGateEvidenceBuilder();
        fixture.Options.GateOutputPath = Path.Combine(root, "blocked-gate.json");
        fixture.Options.ProcessLifetime = TimeSpan.FromSeconds(3);
        fixture.Options.IoTimeout = TimeSpan.FromSeconds(1);
        fixture.Options.CleanupTimeout = TimeSpan.FromMilliseconds(100);
        int exit = await Program.RunAsync(fixture.Arguments(), services =>
        {
            services.RemoveAll<IProbeGateFileStore>();
            services.AddSingleton<IProbeGateFileStore>(new BlockedFiles());
        });
        Environment.Exit(exit);
    }

    private sealed class BlockedFiles : IProbeGateFileStore
    {
        public ValueTask<Stream> OpenReadAsync(string path, CancellationToken cancellationToken) => ValueTask.FromResult<Stream>(new BlockedStream());
        public ValueTask<Stream> CreateNewAsync(string path, CancellationToken cancellationToken) => throw new InvalidOperationException("Must not publish blocked input.");
    }
    private sealed class BlockedStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Thread.Sleep(Timeout.Infinite);
            return ValueTask.FromResult(0);
        }
    }
}
#pragma warning restore CA1707
