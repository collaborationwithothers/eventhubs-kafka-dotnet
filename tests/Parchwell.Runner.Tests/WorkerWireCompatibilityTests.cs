using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Xunit;
using VerifierIdentity = Parchwell.Verifier.Common.Contracts.RunIdentity;

namespace Parchwell.Runner.Tests;

#pragma warning disable CA1707 // Repository test naming convention.

/// <summary>Exercises actual worker codec bytes against the Runner's evidence admission boundary.</summary>
public sealed class WorkerWireCompatibilityTests
{
    private static readonly VerifierIdentity Identity = new("A01", "C0", "synthetic-wire-run", "synthetic-pod", "synthetic-pod", "synthetic-pod");
    private static readonly WorkerIdentity ExpectedIdentity = new(Identity.ScenarioId, Identity.ConfigurationId,
        Identity.RunId, Identity.InstanceId, Identity.ExpectedInstanceId, Identity.ClientId);
    private const string Commit = "1111111111111111111111111111111111111111";

    [Theory]
    [InlineData("invalid")]
    [InlineData("incomplete")]
    public async Task Given_NormalDiagnosticWorkerOutput_When_Reconciled_Then_PreservesDiagnosticsAndAbsentCounts(string status)
    {
        byte[] bytes = Result(status == "invalid" ? VerifierResultStatus.Invalid : VerifierResultStatus.Incomplete);
        string receipt = Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("verify", Identity, Commit));
        AcceptedWorkerOutcome accepted = WorkerOutcome.Reconcile(new(1, receipt, ""), bytes, ExpectedIdentity, Commit);
        Assert.True(accepted.ExecutionCompleted);
        Assert.False(accepted.CountsAccepted);
        Assert.Null(accepted.Counts);
        Assert.Equal(status, accepted.Status);
        using JsonDocument json = JsonDocument.Parse(bytes);
        foreach (string count in new[] { "ok", "loss", "phantom", "reported_failure", "duplicate", "reorder" })
            Assert.False(json.RootElement.TryGetProperty(count, out _));
        Assert.Single(json.RootElement.GetProperty("diagnostics").EnumerateArray());
    }

    [Theory]
    [InlineData("exit-zero")]
    [InlineData("exit-124")]
    [InlineData("timeout")]
    [InlineData("missing-receipt")]
    [InlineData("duplicate-receipt")]
    [InlineData("foreign-source")]
    [InlineData("foreign-identity")]
    [InlineData("result-exit-disagreement")]
    public async Task Given_ExecutionMismatch_When_RealCodecOutputRetained_Then_RejectsCountsAndCompletion(string failure)
    {
        string receipt = Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("verify", Identity, Commit));
        var process = new ProcessResult(1, receipt, "");
        byte[] bytes = Result(VerifierResultStatus.Invalid);
        if (failure == "exit-zero") process = process with { ExitCode = 0 };
        if (failure == "exit-124") process = process with { ExitCode = 124 };
        if (failure == "timeout") process = process with { TimedOut = true };
        if (failure == "missing-receipt") process = process with { StandardOutput = "" };
        if (failure == "duplicate-receipt") process = process with { StandardOutput = receipt + receipt };
        if (failure == "foreign-source") process = process with { StandardOutput = receipt.Replace(Commit, new string('2', 40), StringComparison.Ordinal) };
        if (failure == "foreign-identity") process = process with { StandardOutput = receipt.Replace("synthetic-wire-run", "other-run", StringComparison.Ordinal) };
        if (failure == "result-exit-disagreement")
        {
            process = new(0, Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("verify", Identity, Commit, 0)), "");
        }
        AcceptedWorkerOutcome accepted = WorkerOutcome.Reconcile(process, bytes, ExpectedIdentity, Commit);
        Assert.False(accepted.ExecutionCompleted);
        Assert.False(accepted.CountsAccepted);
        Assert.Null(accepted.Counts);
        Assert.Equal("execution_failure", accepted.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NullOrZeroCountAddedToInvalidDiagnostic_When_Reconciled_Then_RejectsRatherThanAcceptingTotals(bool useNull)
    {
        var malformed = JsonNode.Parse(Result(VerifierResultStatus.Invalid))!;
        malformed["ok"] = useNull ? null : JsonValue.Create(0);
        string receipt = Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("verify", Identity, Commit));
        AcceptedWorkerOutcome accepted = WorkerOutcome.Reconcile(new(1, receipt, ""),
            JsonSerializer.SerializeToUtf8Bytes(malformed), ExpectedIdentity, Commit);
        Assert.False(accepted.ExecutionCompleted);
        Assert.False(accepted.CountsAccepted);
        Assert.Null(accepted.Counts);
    }

    [Fact]
    public async Task Given_CompleteValidLabelWithoutAuthoritativeCounts_When_Reconciled_Then_DoesNotInventZeros()
    {
        Assert.Throws<ArgumentException>(() => Result(VerifierResultStatus.CompleteValid));
        var malformed = JsonNode.Parse(Result(VerifierResultStatus.Invalid))!;
        malformed["status"] = "complete_valid";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(malformed);
        string receipt = Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("verify", Identity, Commit, 0));
        AcceptedWorkerOutcome accepted = WorkerOutcome.Reconcile(new(0, receipt, ""), bytes, ExpectedIdentity, Commit);
        Assert.False(accepted.ExecutionCompleted);
        Assert.False(accepted.CountsAccepted);
        Assert.Null(accepted.Counts);
    }

    [Theory]
    [InlineData("loadgen")]
    [InlineData("start-line")]
    [InlineData("probe-gate")]
    public async Task Given_RealModeReceipt_When_ExitAndSourceMatch_Then_RecognizesMode(string mode)
    {
        byte[] bytes = await WorkerReceipts.WriteAsync(mode, mode == "probe-gate" ? null : Identity, Commit);
        var process = new ProcessResult(0, Encoding.UTF8.GetString(bytes), "");
        Assert.True(WorkerOutcome.HasReceipt(process, mode, mode == "probe-gate" ? null : ExpectedIdentity, Commit));
        Assert.False(WorkerOutcome.HasReceipt(process with { ExitCode = 124 }, mode, mode == "probe-gate" ? null : ExpectedIdentity, Commit));
    }

    private static byte[] Result(VerifierResultStatus status) => VerifierResultCodec.Write(new(1, Identity,
        "synthetic-writer-only", null, Commit, "synthetic", "synthetic", status, [12], null, null,
        null, null, null, null, null, null, null, [],
        [new(12, "synthetic_diagnostic", "ledger.jsonl", 1, null, null)]), new(65536, 10, 1024));
}
#pragma warning restore CA1707
