using System.Diagnostics;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class BoundedProcessRunnerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "bounded-child-" + Guid.NewGuid().ToString("N"));
    public BoundedProcessRunnerTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task Given_ArgumentsContainingShellSyntax_When_Executed_Then_ArgumentListPreservesLiteralValues()
    {
        string marker = Path.Combine(_directory, "must-not-exist");
        string[] literalArguments = ["has spaces", "quote\"and'apostrophe", "line-one\nline-two", "$(touch " + marker + ")", "; exit 23", "é"];
        string[] arguments = ["-c", "printf '%s\\000' \"$@\"", "literal-arguments", .. literalArguments];
        var runner = new BoundedProcessRunner(TimeProvider.System);
        ProcessResult result = await runner.RunAsync(new("/bin/sh", arguments, TimeSpan.FromSeconds(5), 4096), TestContext.Current.CancellationToken);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Join('\0', literalArguments) + '\0', result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.False(result.TimedOut);
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task Given_ExplicitWorkingDirectoryEnvironmentAndExit_When_Executed_Then_ReportsExactChildOutcome()
    {
        var runner = new BoundedProcessRunner(TimeProvider.System);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["PARCHWELL_CHILD_FIXTURE_VALUE"] = "synthetic child value" };
        ProcessResult result = await runner.RunAsync(new("/bin/sh",
            ["-c", "printf '%s\\n%s' \"$PARCHWELL_CHILD_FIXTURE_VALUE\" \"$PWD\"; printf 'diagnostic' >&2; exit 7"],
            TimeSpan.FromSeconds(5), 4096, _directory, environment), TestContext.Current.CancellationToken);
        Assert.Equal(7, result.ExitCode);
        Assert.Equal("synthetic child value\n" + _directory, result.StandardOutput);
        Assert.Equal("diagnostic", result.StandardError);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    public async Task Given_FourUtf8BytesAroundOutputLimit_When_Captured_Then_BoundUsesBytesRatherThanCharacters(int limit, bool allowed)
    {
        var runner = new BoundedProcessRunner(TimeProvider.System);
        var request = new ProcessRequest("/bin/sh", ["-c", "printf 'éé'"], TimeSpan.FromSeconds(5), limit);
        if (allowed)
        {
            ProcessResult result = await runner.RunAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal("éé", result.StandardOutput);
            Assert.Equal(0, result.ExitCode);
        }
        else await Assert.ThrowsAsync<InvalidDataException>(() => runner.RunAsync(request, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_StdoutOrStderrBeyondLimit_When_Captured_Then_FailsInsteadOfReturningTruncatedSuccess(bool stderr)
    {
        var runner = new BoundedProcessRunner(TimeProvider.System);
        string command = stderr ? "printf '12345' >&2" : "printf '12345'";
        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() => runner.RunAsync(
            new("/bin/sh", ["-c", command], TimeSpan.FromSeconds(5), 4), TestContext.Current.CancellationToken));
        Assert.Equal("process_output_limit", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_RunningChild_When_DeadlineOrCancellationOccurs_Then_StopsAndReapsChild(bool externalCancellation)
    {
        string pidPath = Path.Combine(_directory, "child.pid");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runner = new BoundedProcessRunner(TimeProvider.System);
        Task<ProcessResult> execution = runner.RunAsync(new("/bin/sh",
            ["-c", "printf '%s' \"$$\" > \"$1\"; exec sleep 30", "child", pidPath],
            externalCancellation ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(500), 4096), cancellation.Token);
        for (int attempt = 0; attempt < 100 && (!File.Exists(pidPath) || new FileInfo(pidPath).Length == 0); attempt++)
            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        Assert.True(File.Exists(pidPath) && new FileInfo(pidPath).Length > 0,
            "The finite child fixture must start before the cancellation assertion.");
        if (externalCancellation) cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        int pid = int.Parse(await File.ReadAllTextAsync(pidPath, TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            using Process child = Process.GetProcessById(pid);
            Assert.True(child.HasExited, "The process must be reaped before RunAsync completes cancellation.");
        }
        catch (ArgumentException)
        {
            // A reaped process no longer has a process table entry.
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(901, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 16777217)]
    public async Task Given_InvalidProcessBudgets_When_Invoked_Then_RejectsBeforeStartingAnyExecutable(int timeoutSeconds, int outputBytes)
    {
        var runner = new BoundedProcessRunner(TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunAsync(
            new("/does-not-exist", [], TimeSpan.FromSeconds(timeoutSeconds), outputBytes), TestContext.Current.CancellationToken));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
#pragma warning restore CA1707
