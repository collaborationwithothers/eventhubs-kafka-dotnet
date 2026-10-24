using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Tests.Cost;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
[Collection("Failure diagnostics environment")]
public sealed class TerraformCancellationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "terraform-cancellation-" + Guid.NewGuid().ToString("N"));
    private readonly string? originalDebug = Environment.GetEnvironmentVariable("PARCHWELL_DEBUG_FAILURES");
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 1, 0, 0, TimeSpan.Zero);

    public TerraformCancellationTests() => Directory.CreateDirectory(directory);

    [Theory]
    [InlineData("init", "caller_cancellation", false)]
    [InlineData("plan", "caller_cancellation", false)]
    [InlineData("apply", "caller_cancellation", false)]
    [InlineData("init", "apply_deadline", false)]
    [InlineData("plan", "apply_deadline", false)]
    [InlineData("apply", "apply_deadline", false)]
    [InlineData("init", "command_deadline", false)]
    [InlineData("plan", "command_deadline", false)]
    [InlineData("apply", "command_deadline", false)]
    [InlineData("apply", "caller_cancellation", true)]
    [InlineData("apply", "apply_deadline", true)]
    public async Task Given_PartialTerraformStreams_When_ApplyIsCancelled_Then_PreservesPrivateEvidenceAndOriginalCancellation(
        string command, string reason, bool debug)
    {
        Environment.SetEnvironmentVariable("PARCHWELL_DEBUG_FAILURES", debug ? "true" : null);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var clock = new ControlledClock();
        string marker = Path.Combine(directory, "child.pid");
        var process = new TerraformChild(command, marker, clock);
        var terraform = new TerraformEnvironment(process, Path.Combine(directory, "ephemeral"), "private-backend", "private-vars", directory, clock);
        Task apply = terraform.ApplyAsync("synthetic-probe", Start, Start.AddHours(4), caller.Token);
        try
        {
            await WaitForMarkerAsync(marker);
            if (reason == "caller_cancellation")
            {
                caller.Cancel();
                // Simultaneous observation must retain the caller classification.
                clock.Deadlines[0].Fire();
            }
            else if (reason == "apply_deadline") clock.Deadlines[0].Fire();
            else clock.Deadlines[^1].Fire();

            OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                apply.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Same(process.Failure, exception);
            Assert.Equal(command, exception.Data["runner_terraform_command"]);
            Assert.Equal(reason, exception.Data["runner_terraform_cancellation"]);
            Assert.Equal("saved", exception.Data["runner_terraform_cancellation_evidence"]);
            AssertChildReaped(marker);
            using JsonDocument capture = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(directory, "terraform-apply-cancellation.private.json"), TestContext.Current.CancellationToken));
            JsonElement record = capture.RootElement;
            Assert.Equal("A01", record.GetProperty("scenario_id").GetString());
            Assert.Equal("synthetic-probe", record.GetProperty("run_id").GetString());
            Assert.Equal(command, record.GetProperty("command").GetString());
            Assert.Equal(reason, record.GetProperty("reason").GetString());
            Assert.True(record.GetProperty("capture_available").GetBoolean());
            Assert.Equal(TerraformChild.Output, record.GetProperty("stdout").GetString());
            Assert.Equal(TerraformChild.Error, record.GetProperty("stderr").GetString());
            using var log = new StringWriter(CultureInfo.InvariantCulture);
            FailureDiagnostics.Write(exception, log, "apply");
            string diagnostic = log.ToString();
            Assert.Contains($"terraform_command={command} cancellation={reason} cancellation_evidence=saved", diagnostic, StringComparison.Ordinal);
            Assert.DoesNotContain("private-backend", diagnostic, StringComparison.Ordinal);
            Assert.DoesNotContain("private-vars", diagnostic, StringComparison.Ordinal);
            if (debug)
            {
                Assert.Contains("runner_debug_command apply terraform stdout", diagnostic, StringComparison.Ordinal);
                Assert.Contains("synthetic stdout 60", diagnostic, StringComparison.Ordinal);
                Assert.DoesNotContain("synthetic stdout 1\n", diagnostic, StringComparison.Ordinal);
                Assert.Contains(TerraformChild.Error, diagnostic, StringComparison.Ordinal);
            }
            else
            {
                Assert.Single(diagnostic.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
                Assert.DoesNotContain("synthetic stdout", diagnostic, StringComparison.Ordinal);
                Assert.DoesNotContain("synthetic private stderr", diagnostic, StringComparison.Ordinal);
            }
            Assert.All(process.Requests, request => Assert.Equal("terraform", request.FileName));
            Assert.All(process.Requests, request => Assert.Equal(request.Arguments[0] == "init" ? 60 : 900, request.Timeout.TotalSeconds));
            Assert.Equal(TimeSpan.FromSeconds(900), clock.Deadlines[0].Due);

            var store = new PersistentTestStore();
            var exporter = new EvidenceExporter(store, TimeProvider.System);
            PrivateExportReceipt exported = await exporter.ExportAsync("synthetic-probe",
                [new("terraform-apply-cancellation.private.json", Path.Combine(directory, "terraform-apply-cancellation.private.json"))],
                TestContext.Current.CancellationToken);
            Assert.True(exported.Complete);
            Assert.Contains(store.Objects.Keys, key => key.Item1 == "evidence" && key.Item2.EndsWith("/terraform-apply-cancellation.private.json", StringComparison.Ordinal));
        }
        finally
        {
            caller.Cancel();
            try { await apply; } catch (OperationCanceledException) { }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_CancellationWithoutCaptureOrUnwritableEvidence_When_Recorded_Then_DoesNotReplaceCancellation(bool unwritable)
    {
        Environment.SetEnvironmentVariable("PARCHWELL_DEBUG_FAILURES", null);
        using var caller = new CancellationTokenSource();
        var failure = new OperationCanceledException(caller.Token);
        var process = new CancelledProcess(failure, caller);
        string output = unwritable ? Path.Combine(directory, "blocked") : directory;
        if (unwritable) await File.WriteAllTextAsync(output, "synthetic file blocks directory", TestContext.Current.CancellationToken);
        var terraform = new TerraformEnvironment(process, Path.Combine(directory, "ephemeral"), "private-backend", "private-vars", output);

        OperationCanceledException actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            terraform.ApplyAsync("synthetic-probe", Start, Start.AddHours(4), caller.Token));

        Assert.Same(failure, actual);
        Assert.Equal("caller_cancellation", actual.Data["runner_terraform_cancellation"]);
        Assert.Equal(unwritable ? "unavailable" : "saved", actual.Data["runner_terraform_cancellation_evidence"]);
        if (!unwritable)
        {
            using JsonDocument record = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(directory, "terraform-apply-cancellation.private.json"), TestContext.Current.CancellationToken));
            Assert.False(record.RootElement.GetProperty("capture_available").GetBoolean());
            Assert.Equal(string.Empty, record.RootElement.GetProperty("stdout").GetString());
        }
    }

    private static async Task WaitForMarkerAsync(string marker)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            if (File.Exists(marker) && new FileInfo(marker).Length > 0) return;
            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        }
        Assert.Fail("The finite child fixture must start before cancellation.");
    }

    private static void AssertChildReaped(string marker)
    {
        int pid = int.Parse(File.ReadAllText(marker), CultureInfo.InvariantCulture);
        try
        {
            using Process child = Process.GetProcessById(pid);
            Assert.True(child.HasExited);
        }
        catch (ArgumentException) { }
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PARCHWELL_DEBUG_FAILURES", originalDebug);
        Directory.Delete(directory, recursive: true);
    }

    private sealed class TerraformChild(string interruptedCommand, string marker, TimeProvider clock) : IProcessRunner
    {
        internal static readonly string Output = string.Join('\n', Enumerable.Range(1, 60).Select(i => "synthetic stdout " + i.ToString(CultureInfo.InvariantCulture))) + "\n";
        internal const string Error = "synthetic private stderr é\n";
        internal List<ProcessRequest> Requests { get; } = [];
        internal OperationCanceledException? Failure { get; private set; }

        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Arguments[0] != interruptedCommand) return new(0, "synthetic completed", "");
            try
            {
                return await new BoundedProcessRunner(clock).RunAsync(new("/bin/sh",
                    ["-c", "printf '%s' \"$1\"; printf '%s' \"$2\" >&2; sleep 0.1; printf '%s' \"$$\" > \"$3\"; exec sleep 30", "fixture", Output, Error, marker],
                    request.Timeout, 4096), cancellationToken);
            }
            catch (OperationCanceledException exception) { Failure = exception; throw; }
        }
    }

    private sealed class CancelledProcess(OperationCanceledException failure, CancellationTokenSource caller) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            caller.Cancel();
            return Task.FromException<ProcessResult>(failure);
        }
    }

    private sealed class ControlledClock : TimeProvider
    {
        internal List<ControlledTimer> Deadlines { get; } = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime != TimeSpan.FromSeconds(900) && dueTime != TimeSpan.FromSeconds(60))
                return System.CreateTimer(callback, state, dueTime, period);
            var timer = new ControlledTimer(callback, state, dueTime);
            Deadlines.Add(timer);
            return timer;
        }
    }

    private sealed class ControlledTimer(TimerCallback callback, object? state, TimeSpan due) : ITimer
    {
        internal TimeSpan Due { get; } = due;
        internal void Fire() => callback(state);
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
#pragma warning restore CA1707
