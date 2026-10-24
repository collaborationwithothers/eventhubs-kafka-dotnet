using System.Globalization;
using System.Runtime.InteropServices;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Tests.Cost;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

[CollectionDefinition("Failure diagnostics environment", DisableParallelization = true)]
public sealed class FailureDiagnosticsEnvironmentDefinition
{
}

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
[Collection("Failure diagnostics environment")]
public sealed class FailureDiagnosticsTests
{
    private const string DebugVariable = "PARCHWELL_DEBUG_FAILURES";
    private static readonly string[] CommandDataKeys =
        ["runner_command_stderr_tail", "runner_command_stdout_tail", "runner_command_tool"];
    private static readonly string[] TerraformInitArguments = ["init", "-input=false", "-backend-config=synthetic-backend.hcl"];
    private static readonly string[] AzureVersionArguments = ["version", "--output", "json"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Given_ConflictingPrivateObject_When_ExportFails_Then_DebugIdentifiesPathAndHashesWithoutChangingRejection(bool debug) =>
        WithDebugAsync(debug ? "true" : null, async () =>
        {
            string file = Path.GetTempFileName();
            try
            {
                await File.WriteAllBytesAsync(file, new byte[] { 1 }, TestContext.Current.CancellationToken);
                var store = new PersistentTestStore();
                const string path = "A01/synthetic-env/worker.bin";
                store.Objects[("evidence", path)] = new([2], "synthetic-etag");
                var exporter = new EvidenceExporter(store, TimeProvider.System);

                var exception = await Assert.ThrowsAsync<InvalidDataException>(() => exporter.ExportAsync("synthetic-env",
                    [new("worker.bin", file)], TestContext.Current.CancellationToken));

                Assert.Equal("private_export_readback_conflict", exception.Message);
                Assert.DoesNotContain("A01/synthetic-env/manifest.json", store.Writes);
                Assert.Equal(new byte[] { 2 }, store.Objects[("evidence", path)].Bytes);
                string output = Write(exception);
                if (debug)
                {
                    string expected = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(new byte[] { 1 }));
                    string actual = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(new byte[] { 2 }));
                    Assert.Contains($"runner_debug_export path={path} write_outcome=Conflict expected_sha256={expected} actual_sha256={actual}", output, StringComparison.Ordinal);
                }
                else
                {
                    Assert.Equal("runner_execution_failed InvalidDataException private_export_readback_conflict" + Environment.NewLine, output);
                    Assert.Empty(exception.Data);
                }
            }
            finally { File.Delete(file); }
        });

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("true")]
    public void Given_LongWorkerLogsAndDependentCommandFailure_When_DiagnosticsAttached_Then_DebugKeepsSeparateBoundedTails(string? debug) => WithDebug(debug, () =>
    {
        var exception = FailureDiagnostics.WithCommandOutput(new IOException("worker_blob_read_failed"), "az", new(1, "", "BlobNotFound"));
        string logs = string.Join('\n', NumberedLines("worker", 51));
        var identity = new WorkerIdentity("A01", "C0", "synthetic-run", "synthetic-pod", "synthetic-pod", "synthetic-client");
        Assert.Same(exception, FailureDiagnostics.WithWorkerOutput(exception, "verify", new(137, logs, "synthetic worker stderr"), identity));
        string output = Write(exception);
        if (debug == "true")
        {
            Assert.DoesNotContain(NumberedLines("worker", 51)[0] + Environment.NewLine, output, StringComparison.Ordinal);
            Assert.Equal(string.Join('\n', NumberedLines("worker", 51)[1..]), exception.Data["runner_worker_stdout_tail"]);
            Assert.Contains("runner_debug_worker verify stderr scenario_id=A01 run_id=synthetic-run" + Environment.NewLine + "synthetic worker stderr", output, StringComparison.Ordinal);
            Assert.Contains("runner_debug_worker verify stdout scenario_id=A01 run_id=synthetic-run", output, StringComparison.Ordinal);
            Assert.Contains("runner_debug_worker verify exit_code=137 timed_out=False scenario_id=A01 run_id=synthetic-run", output, StringComparison.Ordinal);
            Assert.Contains("runner_debug_command az stderr" + Environment.NewLine + "BlobNotFound", output, StringComparison.Ordinal);
            Environment.SetEnvironmentVariable(DebugVariable, "false");
            Assert.Equal("runner_execution_failed IOException worker_blob_read_failed" + Environment.NewLine, Write(exception));
        }
        else Assert.Empty(exception.Data);
    });

    [Fact]
    public void Given_SafeRootReason_When_Written_Then_EmitsExactFailureLine() => WithDebug(null, () =>
        Assert.Equal("runner_execution_failed InvalidDataException command_required" + Environment.NewLine,
            Write(new InvalidDataException("command_required"))));

    [Fact]
    public void Given_SafeInnerReason_When_Written_Then_EmitsExactOrderedChain() => WithDebug(null, () =>
        Assert.Equal("runner_execution_failed IOException outer_reason -> InvalidOperationException inner_reason" + Environment.NewLine,
            Write(new IOException("outer_reason", new InvalidOperationException("inner_reason")))));

    [Theory]
    [InlineData("")]
    [InlineData("UPPER")]
    [InlineData("has space")]
    [InlineData("path/file")]
    [InlineData("secret=value")]
    [InlineData("reason1")]
    [InlineData("reason-code")]
    [InlineData("reason\n")]
    [InlineData("reason\r\n")]
    [InlineData("\u00e9")]
    public void Given_UnsafeOrEmptyMessage_When_Written_Then_OmitsTheWholeMessage(string message) => WithDebug(null, () =>
        Assert.Equal("runner_execution_failed InvalidDataException" + Environment.NewLine,
            Write(new InvalidDataException(message))));

    [Fact]
    public void Given_ColonReason_When_Written_Then_AdmitsCompleteMessage() => WithDebug(null, () =>
        Assert.Equal("runner_execution_failed IOException az:command_failed" + Environment.NewLine,
            Write(new IOException("az:command_failed"))));

    [Theory]
    [InlineData("synthetic unsafe outer", "inner_reason", "IOException -> InvalidOperationException inner_reason")]
    [InlineData("outer_reason", "synthetic unsafe inner", "IOException outer_reason -> InvalidOperationException")]
    public void Given_MixedSafeAndUnsafeReasons_When_Written_Then_KeepsEveryTypeAndOnlyCompleteSafeReasons(
        string outer, string inner, string chain) => WithDebug(null, () =>
        Assert.Equal("runner_execution_failed " + chain + Environment.NewLine,
            Write(new IOException(outer, new InvalidOperationException(inner)))));

    [Fact]
    public void Given_AggregateWithDistinctChildren_When_Written_Then_VisitsBothInSuppliedOrderExactlyOnce() => WithDebug(null, () =>
    {
        var exception = new AggregateException("synthetic aggregate",
            new IOException("first_reason"), new InvalidOperationException("second_reason"));

        Assert.Equal("runner_execution_failed AggregateException -> IOException first_reason -> InvalidOperationException second_reason" + Environment.NewLine,
            Write(exception));
    });

    [Fact]
    public void Given_NestedAggregateAndOrdinaryInnerChain_When_Written_Then_TraversesEveryBranchInOrder() => WithDebug(null, () =>
    {
        var exception = new AggregateException("synthetic aggregate",
            new IOException("outer_reason", new InvalidDataException("inner_reason")),
            new AggregateException("synthetic nested", new InvalidOperationException("last_reason")));

        Assert.Equal("runner_execution_failed AggregateException -> IOException outer_reason -> InvalidDataException inner_reason -> AggregateException -> InvalidOperationException last_reason" + Environment.NewLine,
            Write(exception));
    });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("true ")]
    public void Given_DebugValueOtherThanExactTrue_When_WrittenAndAttached_Then_OnlySafeLineAndNoCommandData(string? value) => WithDebug(value, () =>
    {
        IOException exception;
        try { throw new IOException("synthetic unsafe message", new InvalidDataException("inner_reason")); }
        catch (IOException thrown) { exception = thrown; }
        Assert.NotNull(exception.StackTrace);
        exception.Data["synthetic_unrelated"] = "synthetic unrelated sentinel";

        IOException returned = FailureDiagnostics.WithCommandOutput(exception, "terraform",
            new(1, "synthetic stdout sentinel", "synthetic stderr sentinel"));

        Assert.Same(exception, returned);
        Assert.Single(exception.Data);
        Assert.Equal("synthetic unrelated sentinel", exception.Data["synthetic_unrelated"]);
        Assert.All(CommandDataKeys, key => Assert.False(exception.Data.Contains(key)));
        Assert.Equal("runner_execution_failed IOException -> InvalidDataException inner_reason" + Environment.NewLine,
            Write(exception));
    });

    [Fact]
    public void Given_ExactTrueAndOuterAndInnerCommandData_When_Written_Then_EmitsExceptionOnceAndOnlyOwnedFieldsInOrder() => WithDebug("true", () =>
    {
        var inner = new InvalidOperationException("synthetic unsafe inner message");
        var exception = new IOException("synthetic unsafe outer message", inner);
        FailureDiagnostics.WithCommandOutput(exception, "terraform", new(1, "synthetic outer stdout", "synthetic outer stderr"));
        FailureDiagnostics.WithCommandOutput(inner, "az", new(1, "synthetic inner stdout", "synthetic inner stderr"));
        exception.Data["synthetic_unrelated"] = "synthetic unrelated sentinel";
        inner.Data["synthetic_unrelated"] = "synthetic inner unrelated sentinel";

        string expected = "runner_execution_failed IOException -> InvalidOperationException" + Environment.NewLine
            + exception + Environment.NewLine
            + "runner_debug_command terraform stdout" + Environment.NewLine + "synthetic outer stdout" + Environment.NewLine
            + "runner_debug_command terraform stderr" + Environment.NewLine + "synthetic outer stderr" + Environment.NewLine
            + "runner_debug_command az stdout" + Environment.NewLine + "synthetic inner stdout" + Environment.NewLine
            + "runner_debug_command az stderr" + Environment.NewLine + "synthetic inner stderr" + Environment.NewLine;

        Assert.Equal(expected, Write(exception));

        Environment.SetEnvironmentVariable(DebugVariable, "false");
        Assert.Equal("runner_execution_failed IOException -> InvalidOperationException" + Environment.NewLine, Write(exception));
        Assert.Equal("synthetic outer stdout", exception.Data["runner_command_stdout_tail"]);
        Assert.Equal("synthetic inner stderr", inner.Data["runner_command_stderr_tail"]);
    });

    [Theory]
    [InlineData("terraform")]
    [InlineData("az")]
    [InlineData("kubectl")]
    [InlineData("helm")]
    public void Given_ApprovedToolInDebug_When_Attached_Then_ReturnsSameExceptionAndRecordsOnlyThreeFields(string tool) => WithDebug("true", () =>
    {
        var inner = new InvalidDataException("inner_reason");
        var exception = new IOException("outer_reason", inner);

        IOException returned = FailureDiagnostics.WithCommandOutput(exception, tool, new(1, "synthetic stdout", "synthetic stderr"));

        Assert.Same(exception, returned);
        Assert.Equal("outer_reason", returned.Message);
        Assert.Same(inner, returned.InnerException);
        AssertCommandData(returned, tool, "synthetic stdout", "synthetic stderr");
    });

    [Theory]
    [InlineData("kubelogin")]
    [InlineData("Terraform")]
    [InlineData("/synthetic/bin/terraform")]
    [InlineData("synthetic --argument=value")]
    public void Given_UnapprovedToolInDebug_When_Attached_Then_RecordsNoCommandFields(string tool) => WithDebug("true", () =>
    {
        var exception = new IOException("command_failed");

        Assert.Same(exception, FailureDiagnostics.WithCommandOutput(exception, tool, new(1, "synthetic stdout", "synthetic stderr")));
        Assert.Empty(exception.Data);
    });

    [Theory]
    [InlineData(49, "\n", false)]
    [InlineData(50, "\n", false)]
    [InlineData(51, "\n", false)]
    [InlineData(49, "\r\n", false)]
    [InlineData(50, "\r\n", false)]
    [InlineData(51, "\r\n", false)]
    [InlineData(49, "\n", true)]
    [InlineData(50, "\n", true)]
    [InlineData(51, "\n", true)]
    [InlineData(49, "\r\n", true)]
    [InlineData(50, "\r\n", true)]
    [InlineData(51, "\r\n", true)]
    public void Given_StreamsAroundFiftyLineBoundary_When_Attached_Then_RetainsExactIndependentTailsWithoutPhantomLines(
        int count, string separator, bool terminalNewline) => WithDebug("true", () =>
    {
        string[] stdout = NumberedLines("stdout", count);
        string[] stderr = NumberedLines("stderr", count);
        string suffix = terminalNewline ? separator : string.Empty;
        var exception = new IOException("command_failed");

        FailureDiagnostics.WithCommandOutput(exception, "helm",
            new(1, string.Join(separator, stdout) + suffix, string.Join(separator, stderr) + suffix));

        int firstRetainedIndex = count == 51 ? 1 : 0;
        AssertCommandData(exception, "helm", string.Join('\n', stdout[firstRetainedIndex..]), string.Join('\n', stderr[firstRetainedIndex..]));
    });

    [Theory]
    [InlineData("", "")]
    [InlineData("", "synthetic stderr")]
    [InlineData("synthetic stdout", "")]
    public void Given_EmptyCommandStream_When_WrittenInDebug_Then_OmitsOnlyItsHeading(string stdout, string stderr) => WithDebug("true", () =>
    {
        var exception = new IOException("command_failed");
        FailureDiagnostics.WithCommandOutput(exception, "az", new(1, stdout, stderr));

        AssertCommandData(exception, "az", stdout, stderr);
        string expected = "runner_execution_failed IOException command_failed" + Environment.NewLine + exception + Environment.NewLine;
        if (stdout.Length > 0) expected += "runner_debug_command az stdout" + Environment.NewLine + stdout + Environment.NewLine;
        if (stderr.Length > 0) expected += "runner_debug_command az stderr" + Environment.NewLine + stderr + Environment.NewLine;
        Assert.Equal(expected, Write(exception));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NoRunnerArguments_When_ExecutedAsBoundedChild_Then_RoutesFailureOnlyToStderr(bool debug)
    {
        string assembly = typeof(TerraformEnvironment).Assembly.Location;
        string host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
            ?? Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..",
                OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { [DebugVariable] = debug ? "true" : "false" };
        var process = new BoundedProcessRunner(TimeProvider.System);

        ProcessResult result = await process.RunAsync(new(host, [assembly], TimeSpan.FromSeconds(10), 65536, Environment: environment),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Equal(string.Empty, result.StandardOutput);
        const string firstLine = "runner_execution_failed InvalidDataException command_required";
        if (debug)
        {
            Assert.StartsWith(firstLine + Environment.NewLine, result.StandardError, StringComparison.Ordinal);
            Assert.Contains("System.IO.InvalidDataException: command_required", result.StandardError, StringComparison.Ordinal);
        }
        else Assert.Equal(firstLine + Environment.NewLine, result.StandardError);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task Given_RejectedTerraformInit_When_Initialized_Then_PreservesReasonAndAttachesTailsOnlyInDebug(bool debug, bool timedOut) =>
        WithDebugAsync(debug ? "true" : null, async () =>
        {
            var process = new RejectedProcess(new(timedOut ? 0 : 1,
                string.Join('\n', NumberedLines("terraform stdout", 51)), string.Join('\n', NumberedLines("terraform stderr", 51)), timedOut));
            string root = Path.Combine(Path.GetTempPath(), "synthetic-failure-diagnostics", "ephemeral");
            var terraform = new TerraformEnvironment(process, root, "synthetic-backend.hcl", "synthetic.tfvars", Path.GetTempPath());

            IOException exception = await Assert.ThrowsAsync<IOException>(() => terraform.InitializeAsync(TestContext.Current.CancellationToken));

            Assert.Equal("terraform_command_failed", exception.Message);
            ProcessRequest request = Assert.Single(process.Requests);
            Assert.Equal("terraform", request.FileName);
            Assert.Equal(TerraformInitArguments, request.Arguments);
            Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout);
            Assert.Null(request.Environment);
            AssertBoundaryData(exception, debug, "terraform");
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task Given_RejectedAzureCliVersion_When_Verified_Then_PreservesReasonAndAttachesTailsOnlyInDebug(bool debug, bool timedOut) =>
        WithDebugAsync(debug ? "true" : null, async () =>
        {
            var process = new RejectedProcess(new(timedOut ? 0 : 1,
                string.Join('\n', NumberedLines("az stdout", 51)), string.Join('\n', NumberedLines("az stderr", 51)), timedOut));
            var store = new AzureCliRunStore(process, "https://stsynthetic.blob.core.windows.net", Path.GetTempPath());

            IOException exception = await Assert.ThrowsAsync<IOException>(() => store.VerifyCliAsync(TestContext.Current.CancellationToken));

            Assert.Equal("private_storage_cli_failed", exception.Message);
            ProcessRequest request = Assert.Single(process.Requests);
            Assert.Equal("az", request.FileName);
            Assert.Equal(AzureVersionArguments, request.Arguments);
            Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout);
            Assert.Equal(1_048_576, request.MaxOutputBytes);
            Assert.Null(request.Environment);
            AssertBoundaryData(exception, debug, "az");
        });

    private static void AssertBoundaryData(Exception exception, bool debug, string tool)
    {
        if (debug)
        {
            AssertCommandData(exception, tool,
                string.Join('\n', NumberedLines(tool + " stdout", 51)[1..]),
                string.Join('\n', NumberedLines(tool + " stderr", 51)[1..]));
            Assert.DoesNotContain("synthetic-backend.hcl", Write(exception), StringComparison.Ordinal);
            Assert.DoesNotContain("--output", Write(exception), StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(exception.Data);
            Assert.Equal("runner_execution_failed IOException " + exception.Message + Environment.NewLine, Write(exception));
        }
    }

    private static void AssertCommandData(Exception exception, string tool, string stdout, string stderr)
    {
        Assert.Equal(CommandDataKeys, exception.Data.Keys.Cast<string>().Order(StringComparer.Ordinal));
        Assert.Equal(tool, exception.Data["runner_command_tool"]);
        Assert.Equal(stdout, exception.Data["runner_command_stdout_tail"]);
        Assert.Equal(stderr, exception.Data["runner_command_stderr_tail"]);
    }

    private static string[] NumberedLines(string stream, int count) => Enumerable.Range(1, count)
        .Select(number => "synthetic " + stream + " line " + number.ToString(CultureInfo.InvariantCulture)).ToArray();

    private static string Write(Exception exception)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        FailureDiagnostics.Write(exception, writer);
        return writer.ToString();
    }

    private static void WithDebug(string? value, Action action)
    {
        string? original = Environment.GetEnvironmentVariable(DebugVariable);
        try
        {
            Environment.SetEnvironmentVariable(DebugVariable, value);
            action();
        }
        finally { Environment.SetEnvironmentVariable(DebugVariable, original); }
    }

    private static async Task WithDebugAsync(string? value, Func<Task> action)
    {
        string? original = Environment.GetEnvironmentVariable(DebugVariable);
        try
        {
            Environment.SetEnvironmentVariable(DebugVariable, value);
            await action();
        }
        finally { Environment.SetEnvironmentVariable(DebugVariable, original); }
    }

    private sealed class RejectedProcess(ProcessResult result) : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }
}
#pragma warning restore CA1707
