using System.Text;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Repository-required Given_When_Then names.

/// <summary>Checks exact wire byte ceilings with otherwise valid synthetic worker documents.</summary>
public sealed class WorkerOutcomeBoundaryTests
{
    private const string Commit = "1111111111111111111111111111111111111111";
    private static readonly WorkerIdentity Identity = new("A01", "C0", "synthetic-boundary-r1", "synthetic-pod", "synthetic-pod", "synthetic-pod");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Theory]
    [InlineData(65_535, true)]
    [InlineData(65_536, true)]
    [InlineData(65_537, false)]
    public void Given_CompletionReceiptAtByteBoundary_When_Checked_Then_OnlyOversizeIsRejected(int length, bool accepted)
    {
        string receipt = Encoding.UTF8.GetString(Pad(Receipt("verify", Identity), length));
        Assert.Equal(length, Encoding.UTF8.GetByteCount(receipt));
        Assert.Equal(accepted, WorkerOutcome.HasReceipt(new(0, WorkerOutcome.ReceiptPrefix + receipt, ""), "verify", Identity, Commit));
    }

    [Theory]
    [InlineData(16_777_215, true)]
    [InlineData(16_777_216, true)]
    [InlineData(16_777_217, false)]
    public void Given_ValidResultAtByteBoundary_When_Reconciled_Then_OnlyOversizeLosesCounts(int length, bool accepted)
    {
        byte[] result = Pad(JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 1, identity = Identity, verifier_commit = Commit, status = "complete_valid",
            ok = 11, loss = 0, phantom = 0, reported_failure = 0, duplicate = 0, reorder = 0
        }, JsonOptions), length);

        AcceptedWorkerOutcome outcome = WorkerOutcome.Reconcile(Process("verify", Identity), result, Identity, Commit);

        Assert.Equal(length, result.Length);
        Assert.Equal(accepted, outcome.ExecutionCompleted);
        Assert.Equal(accepted, outcome.CountsAccepted);
        Assert.Equal(accepted ? "complete_valid" : "result_limit", outcome.Reason);
        if (accepted) Assert.Equal(11, outcome.Counts!["ok"]);
        else Assert.Null(outcome.Counts);
    }

    [Theory]
    [InlineData(16_777_215, true)]
    [InlineData(16_777_216, true)]
    [InlineData(16_777_217, false)]
    public void Given_ValidInventoryAtByteBoundary_When_Read_Then_OnlyOversizeIsRejected(int length, bool accepted)
    {
        byte[] bytes = Pad(Inventory(1), length);
        Assert.Equal(length, bytes.Length);
        if (accepted) Assert.Equal(["sasl_ssl://broker-0.example.invalid:9093/0"], BrokerInventory.Read(bytes, Identity, Commit));
        else Assert.Equal("inventory_limit", Assert.Throws<InvalidDataException>(() => BrokerInventory.Read(bytes, Identity, Commit)).Message);
    }

    [Theory]
    [InlineData(63, true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Given_InventoryAtBrokerCountBoundary_When_Read_Then_OnlyOversizeIsRejected(int count, bool accepted)
    {
        byte[] bytes = Inventory(count);
        if (accepted)
        {
            IReadOnlyList<string> brokers = BrokerInventory.Read(bytes, Identity, Commit);
            Assert.Equal(count, brokers.Count);
            Assert.Equal(Enumerable.Range(0, count).Select(id => $"sasl_ssl://broker-{id}.example.invalid:9093/{id}"), brokers);
        }
        else Assert.Equal("inventory_count", Assert.Throws<InvalidDataException>(() => BrokerInventory.Read(bytes, Identity, Commit)).Message);
    }

    [Theory]
    [InlineData(16_777_215, true)]
    [InlineData(16_777_216, true)]
    [InlineData(16_777_217, false)]
    public void Given_PassingGateAtByteBoundary_When_Inspected_Then_OnlyOversizeCannotAuthorizePhases(int length, bool accepted)
    {
        WorkerIdentity[] identities = Enumerable.Range(1, 5).Select(ordinal => Identity with { RunId = "synthetic-boundary-r" + ordinal }).ToArray();
        byte[] bytes = Pad(JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 1, scenario_id = "A01", verifier_commit = Commit, outcome = "pass",
            repeats = identities.Select(identity => new { identity, usable = true })
        }, JsonOptions), length);
        var gate = new GateExecution(Process("probe-gate", null), bytes);

        Assert.Equal(length, bytes.Length);
        if (accepted)
        {
            ProbeGateDecision decision = ProbeGateRunner.Inspect(gate, Commit, identities);
            Assert.True(decision.AllowsFullPhases);
            Assert.Equal("pass", decision.Outcome);
        }
        else Assert.Equal("gate_repeat_count", Assert.Throws<InvalidDataException>(() => ProbeGateRunner.Inspect(gate, Commit, identities)).Message);
    }

    private static byte[] Inventory(int count) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schema_version = 1, source = "kafka_metadata", source_commit = Commit, identity = Identity,
        brokers = Enumerable.Range(0, count).Select(id => new
        {
            id, host = $"broker-{id}.example.invalid", port = 9093, name = $"sasl_ssl://broker-{id}.example.invalid:9093/{id}"
        })
    }, JsonOptions);

    private static ProcessResult Process(string mode, WorkerIdentity? identity) => new(0,
        WorkerOutcome.ReceiptPrefix + Encoding.UTF8.GetString(Receipt(mode, identity)), "");

    private static byte[] Receipt(string mode, WorkerIdentity? identity) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schema_version = 1, mode, source_commit = Commit, cleanup_completed = true, handler_exit_code = 0,
        identity = identity ?? Identity
    }, JsonOptions);

    private static byte[] Pad(byte[] json, int length)
    {
        Assert.True(json.Length < length);
        byte[] padded = new byte[length];
        json.CopyTo(padded, 0);
        padded.AsSpan(json.Length).Fill((byte)' ');
        return padded;
    }
}
#pragma warning restore CA1707
