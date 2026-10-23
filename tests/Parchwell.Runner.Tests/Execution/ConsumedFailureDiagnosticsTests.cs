using System.Globalization;
using System.Text;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Run.Rehearsal;
using Parchwell.Runner.Tests.Cost;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
[Collection("Failure diagnostics environment")]
public sealed class ConsumedFailureDiagnosticsTests
{
    private const string DebugVariable = "PARCHWELL_DEBUG_FAILURES";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_PartialApplyFailure_When_PhaseExecutes_Then_ReportsFailureAndExportsBeforeCleanup(bool debug)
    {
        PhaseObservation observation = await ExecuteAsync("apply", debug);

        Assert.Equal("failed", observation.Result.Result);
        Assert.Equal(1, Program.PhaseExitCode(observation.Result));
        Assert.Empty(observation.Result.Repeats);
        Assert.True(observation.Result.Exported);
        Assert.True(observation.Result.Destroyed);
        Assert.Equal(1, observation.Applies);
        Assert.Equal(1, observation.Destroys);
        Assert.Equal(0, observation.ClusterCalls);
        AssertCleanupOrder(observation.Trace);
        const string safeLine = "runner_execution_failed apply IOException scripted_apply";
        if (debug)
        {
            Assert.NotNull(observation.ApplyException);
            Assert.StartsWith(safeLine + Environment.NewLine + "runner_debug_exception apply" + Environment.NewLine, observation.Error, StringComparison.Ordinal);
            Assert.Contains(observation.ApplyException.ToString(), observation.Error, StringComparison.Ordinal);
            Assert.Contains("runner_debug_command apply terraform stdout" + Environment.NewLine + "synthetic stdout sentinel" + Environment.NewLine,
                observation.Error, StringComparison.Ordinal);
            Assert.Contains("runner_debug_command apply terraform stderr" + Environment.NewLine + "synthetic stderr sentinel" + Environment.NewLine,
                observation.Error, StringComparison.Ordinal);
        }
        else Assert.Equal(safeLine + Environment.NewLine, observation.Error);
    }

    [Theory]
    [InlineData("repeat", false)]
    [InlineData("repeat", true)]
    [InlineData("export", false)]
    [InlineData("export", true)]
    [InlineData("destroy", false)]
    [InlineData("destroy", true)]
    public async Task Given_ConsumedOperationFailure_When_PhaseExecutes_Then_ReportsItsStageAndStillAttemptsCleanup(string fault, bool debug)
    {
        PhaseObservation observation = await ExecuteAsync(fault, debug);
        string reason = fault == "export" ? "scripted_export_failure" : "scripted_" + fault;
        string safeLine = "runner_execution_failed " + fault + " IOException " + reason;

        Assert.Equal("failed", observation.Result.Result);
        Assert.Equal(1, Program.PhaseExitCode(observation.Result));
        Assert.Empty(observation.Result.Repeats);
        Assert.Equal(1, observation.Applies);
        Assert.Equal(1, observation.Destroys);
        AssertCleanupOrder(observation.Trace);
        Assert.Contains(safeLine + Environment.NewLine, observation.Error, StringComparison.Ordinal);
        if (fault == "repeat")
        {
            Assert.True(observation.Result.Exported);
            Assert.True(observation.Result.Destroyed);
        }
        else
        {
            Assert.Equal(0, observation.ClusterCalls);
            Assert.Contains("runner_execution_failed apply IOException scripted_apply" + Environment.NewLine, observation.Error, StringComparison.Ordinal);
            Assert.Equal(fault == "destroy", observation.Result.Exported);
            Assert.False(observation.Result.Destroyed);
        }
        if (debug)
        {
            Assert.Contains("runner_debug_exception " + fault + Environment.NewLine + "System.IO.IOException: " + reason + Environment.NewLine,
                observation.Error, StringComparison.Ordinal);
            string frame = fault switch
            {
                "repeat" => "FailureCluster.EstablishProducerWatchAsync",
                "export" => "RehearsalArtifactStore.WriteAsync",
                _ => "RehearsalEnvironment.Fail"
            };
            Assert.Contains(frame, observation.Error, StringComparison.Ordinal);
        }
        else
        {
            string[] lines = observation.Error.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.All(lines, line => Assert.StartsWith("runner_execution_failed ", line, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Given_MalformedKqlJson_When_ParsedUnderExport_Then_ReportsSafeFailureAndRetainsOriginalFallback()
    {
        string? previousDebug = Environment.GetEnvironmentVariable(DebugVariable);
        TextWriter previousError = Console.Error;
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        try
        {
            Environment.SetEnvironmentVariable(DebugVariable, null);
            Console.SetError(error);
            byte[] original = Encoding.UTF8.GetBytes("{");
            using var scope = FailureDiagnostics.UseStage("export");

            KqlReceipt receipt = KqlCollector.Parse(original, "synthetic query");

            Assert.Equal("incomplete", receipt.Status);
            Assert.Equal("query_json_invalid", receipt.Reason);
            Assert.Equal(0, receipt.Rows);
            Assert.Same(original, receipt.OriginalOutput);
            Assert.Equal(Encoding.UTF8.GetBytes("{"), receipt.OriginalOutput);
            Assert.Equal("synthetic query", receipt.Query);
            Assert.Null(receipt.OriginalError);
            Assert.Equal("runner_execution_failed export JsonReaderException" + Environment.NewLine, error.ToString());
        }
        finally
        {
            Console.SetError(previousError);
            Environment.SetEnvironmentVariable(DebugVariable, previousDebug);
        }
    }

    private static void AssertCleanupOrder(List<string> trace)
    {
        int apply = trace.IndexOf("apply");
        int export = trace.IndexOf("export_started");
        int plan = trace.IndexOf("destroy_plan");
        int destroy = trace.IndexOf("destroy");
        int receipt = trace.IndexOf("cleanup_receipt");
        Assert.True(apply >= 0);
        Assert.True(apply < export);
        Assert.True(export < plan);
        Assert.True(plan < destroy);
        Assert.True(destroy < receipt);
    }

    private static async Task<PhaseObservation> ExecuteAsync(string fault, bool debug)
    {
        string? previousDebug = Environment.GetEnvironmentVariable(DebugVariable);
        TextWriter previousError = Console.Error;
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        string directory = Path.Combine(Path.GetTempPath(), "parchwell-diagnostics-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable(DebugVariable, debug ? "true" : null);
            Console.SetError(error);
            var trace = new List<string>();
            var store = new RehearsalArtifactStore(failure: fault == "export" ? "export" : null, trace: trace);
            var inner = new RehearsalEnvironment(failure: fault == "destroy" ? "destroy" : null, trace: trace);
            var environment = new ApplyFailureEnvironment(inner, fault != "repeat");
            var cluster = new FailureCluster(fault == "repeat");
            var clock = TimeProvider.System;
            var executor = new PhaseExecutor(new RunLedger(store, clock), environment, new RepeatExecutor(cluster, clock),
                new EvidenceExporter(store, clock), new GenerationGuard(environment, clock), clock);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));

            PhaseExecution result = await executor.ExecuteAsync(new("probe", "rehearsal-probe", new string('a', 40),
                directory, PhaseReservationTests.Input(), InitializeLedger: true), deadline.Token);

            return new(result, error.ToString(), trace, inner.Applies, inner.Destroys, cluster.Calls, environment.Failure);
        }
        finally
        {
            Console.SetError(previousError);
            Environment.SetEnvironmentVariable(DebugVariable, previousDebug);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed record PhaseObservation(PhaseExecution Result, string Error, List<string> Trace,
        int Applies, int Destroys, int ClusterCalls, IOException? ApplyException);

    private sealed class ApplyFailureEnvironment(RehearsalEnvironment inner, bool failApply) : ITerraformEnvironment
    {
        public IOException? Failure { get; private set; }

        public Task<EnvironmentGeneration?> ReadGenerationAsync(CancellationToken cancellationToken) => inner.ReadGenerationAsync(cancellationToken);
        public async Task ApplyAsync(string environmentRunId, DateTimeOffset startedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken)
        {
            await inner.ApplyAsync(environmentRunId, startedAt, expiresAt, cancellationToken);
            if (failApply)
            {
                Failure = FailureDiagnostics.WithCommandOutput(new IOException("scripted_apply"), "terraform",
                    new ProcessResult(1, "synthetic stdout sentinel", "synthetic stderr sentinel"));
                throw Failure;
            }
        }
        public Task<JsonElement> ReadOutputsAsync(CancellationToken cancellationToken) => inner.ReadOutputsAsync(cancellationToken);
        public Task PreflightAsync(JsonElement outputs, CancellationToken cancellationToken) => inner.PreflightAsync(outputs, cancellationToken);
        public Task<DestroyPlan> PlanDestroyAsync(EnvironmentGeneration expected, CancellationToken cancellationToken) => inner.PlanDestroyAsync(expected, cancellationToken);
        public Task ApplyDestroyPlanAsync(DestroyPlan plan, CancellationToken cancellationToken) => inner.ApplyDestroyPlanAsync(plan, cancellationToken);
    }

    private sealed class FailureCluster(bool failRepeat) : IRunCluster
    {
        public int Calls { get; private set; }

        public Task PreflightAsync(JsonElement outputs, CancellationToken cancellationToken) => PreserveAsync(cancellationToken);
        public Task<PodAdmission> EstablishProducerWatchAsync(RepeatContext context, CancellationToken cancellationToken)
        {
            CheckAllowed(cancellationToken);
            throw new IOException("scripted_repeat");
        }
        public Task PreserveObservedWorkersAsync(RepeatContext context, CancellationToken cancellationToken) => PreserveAsync(cancellationToken);
        public Task PreserveAvailableArtifactsAsync(RepeatContext context, CancellationToken cancellationToken) => PreserveAsync(cancellationToken);
        public Task CreateProducerAsync(RepeatContext context, CancellationToken cancellationToken) => throw UnexpectedCall();
        public Task<ProducerPod> ObserveProducerAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken) => throw UnexpectedCall();
        public Task<StartLineExecution> StartLineAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken) => throw UnexpectedCall();
        public Task<string> GenerateComparisonAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken) => throw UnexpectedCall();
        public Task ReleaseAsync(RepeatContext context, byte[] permit, CancellationToken cancellationToken) => throw UnexpectedCall();
        public Task<WorkerExecution> WaitProducerAsync(RepeatContext context, CancellationToken cancellationToken) => throw UnexpectedCall();
        public Task<ProducerPublication> DownloadPublicationAsync(RepeatContext context, CancellationToken cancellationToken) => throw UnexpectedCall();
        public Task<WorkerExecution> VerifyAsync(RepeatContext context, WorkerIdentity identity, IReadOnlyList<string> brokers, CancellationToken cancellationToken) => throw UnexpectedCall();
        public Task CompleteProducerWatchAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken) => throw UnexpectedCall();

        private Task PreserveAsync(CancellationToken cancellationToken)
        {
            CheckAllowed(cancellationToken);
            return Task.CompletedTask;
        }
        private void CheckAllowed(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            if (!failRepeat) throw new InvalidOperationException("synthetic_unexpected_cluster_call");
        }
        private InvalidOperationException UnexpectedCall()
        {
            Calls++;
            return new InvalidOperationException("synthetic_unexpected_cluster_call");
        }
    }
}
#pragma warning restore CA1707
