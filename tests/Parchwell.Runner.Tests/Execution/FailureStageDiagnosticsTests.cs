using System.Globalization;
using Parchwell.Runner.Run.Execution;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
[Collection("Failure diagnostics environment")]
public sealed class FailureStageDiagnosticsTests
{
    private const string DebugVariable = "PARCHWELL_DEBUG_FAILURES";

    [Theory]
    [InlineData("apply")]
    [InlineData("repeat")]
    [InlineData("export")]
    [InlineData("destroy")]
    [InlineData("sweep")]
    public void Given_StageScope_When_FailureWritten_Then_EmitsOnlyExactStagedSafeLine(string stage) => WithDebug(null, () =>
    {
        using var scope = FailureDiagnostics.UseStage(stage);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);

        FailureDiagnostics.Write(new IOException("synthetic_failure"), writer);

        Assert.Equal("runner_execution_failed " + stage + " IOException synthetic_failure" + Environment.NewLine, writer.ToString());
    });

    [Fact]
    public void Given_StagedExceptionWithCommandOutput_When_DebugToggled_Then_OnlyDebugIncludesExceptionAndTails() => WithDebug("true", () =>
    {
        IOException exception;
        try { throw new IOException("synthetic unsafe message", new InvalidDataException("inner_reason")); }
        catch (IOException thrown) { exception = thrown; }
        Assert.NotNull(exception.StackTrace);
        FailureDiagnostics.WithCommandOutput(exception, "terraform", new(1, "synthetic stdout sentinel", "synthetic stderr sentinel"));
        using var scope = FailureDiagnostics.UseStage("apply");
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        const string safeLine = "runner_execution_failed apply IOException -> InvalidDataException inner_reason";

        FailureDiagnostics.Write(exception, writer);

        Assert.Equal(safeLine + Environment.NewLine
            + "runner_debug_exception apply" + Environment.NewLine + exception + Environment.NewLine
            + "runner_debug_command apply terraform stdout" + Environment.NewLine + "synthetic stdout sentinel" + Environment.NewLine
            + "runner_debug_command apply terraform stderr" + Environment.NewLine + "synthetic stderr sentinel" + Environment.NewLine,
            writer.ToString());

        Environment.SetEnvironmentVariable(DebugVariable, null);
        using var safeWriter = new StringWriter(CultureInfo.InvariantCulture);
        FailureDiagnostics.Write(exception, safeWriter);
        string safeOutput = safeWriter.ToString();
        Assert.Equal(safeLine + Environment.NewLine, safeOutput);
        Assert.DoesNotContain("synthetic unsafe message", safeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic stdout sentinel", safeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic stderr sentinel", safeOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(exception.StackTrace, safeOutput, StringComparison.Ordinal);
    });

    [Fact]
    public void Given_NestedStageScopes_When_Disposed_Then_RestoresOuterAndUnscopedFormats() => WithDebug(null, () =>
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var exception = new IOException("synthetic_failure");
        using (FailureDiagnostics.UseStage("apply"))
        {
            FailureDiagnostics.Write(exception, writer);
            using (FailureDiagnostics.UseStage("export")) FailureDiagnostics.Write(exception, writer);
            FailureDiagnostics.Write(exception, writer);
        }
        FailureDiagnostics.Write(exception, writer);

        Assert.Equal("runner_execution_failed apply IOException synthetic_failure" + Environment.NewLine
            + "runner_execution_failed export IOException synthetic_failure" + Environment.NewLine
            + "runner_execution_failed apply IOException synthetic_failure" + Environment.NewLine
            + "runner_execution_failed IOException synthetic_failure" + Environment.NewLine, writer.ToString());
    });

    [Fact]
    public async Task Given_ConcurrentStageScopes_When_Awaiting_Then_KeepTheirStagesAndLeaveCallerUnchanged()
    {
        string? previousDebug = Environment.GetEnvironmentVariable(DebugVariable);
        TextWriter previousError = Console.Error;
        try
        {
            Environment.SetEnvironmentVariable(DebugVariable, null);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            var repeatReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var sweepReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var scope = FailureDiagnostics.UseStage("apply");
            using var caller = new StringWriter(CultureInfo.InvariantCulture);
            Task<string> repeat = WriteAcrossAwaitAsync("repeat", repeatReady);
            Task<string> sweep = WriteAcrossAwaitAsync("sweep", sweepReady);
            await Task.WhenAll(repeatReady.Task, sweepReady.Task).WaitAsync(deadline.Token);
            FailureDiagnostics.Write(new IOException("synthetic_failure"), caller);
            release.SetResult();
            string[] outputs = await Task.WhenAll(repeat, sweep).WaitAsync(deadline.Token);
            FailureDiagnostics.Write(new IOException("synthetic_failure"), caller);

            Assert.Equal(string.Concat(Enumerable.Repeat("runner_execution_failed repeat IOException synthetic_failure" + Environment.NewLine, 2)), outputs[0]);
            Assert.Equal(string.Concat(Enumerable.Repeat("runner_execution_failed sweep IOException synthetic_failure" + Environment.NewLine, 2)), outputs[1]);
            Assert.Equal(string.Concat(Enumerable.Repeat("runner_execution_failed apply IOException synthetic_failure" + Environment.NewLine, 2)), caller.ToString());

            async Task<string> WriteAcrossAwaitAsync(string stage, TaskCompletionSource ready)
            {
                using var taskScope = FailureDiagnostics.UseStage(stage);
                using var writer = new StringWriter(CultureInfo.InvariantCulture);
                FailureDiagnostics.Write(new IOException("synthetic_failure"), writer);
                ready.SetResult();
                await release.Task.WaitAsync(deadline.Token);
                FailureDiagnostics.Write(new IOException("synthetic_failure"), writer);
                return writer.ToString();
            }
        }
        finally
        {
            Console.SetError(previousError);
            Environment.SetEnvironmentVariable(DebugVariable, previousDebug);
        }
    }

    private static void WithDebug(string? value, Action action)
    {
        string? previousDebug = Environment.GetEnvironmentVariable(DebugVariable);
        TextWriter previousError = Console.Error;
        try
        {
            Environment.SetEnvironmentVariable(DebugVariable, value);
            action();
        }
        finally
        {
            Console.SetError(previousError);
            Environment.SetEnvironmentVariable(DebugVariable, previousDebug);
        }
    }
}
#pragma warning restore CA1707
