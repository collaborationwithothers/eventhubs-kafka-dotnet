using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Xunit;

namespace Parchwell.Verifier.Tests.Phase;

#pragma warning disable CA1707
public sealed class B10ScientificThreeJobReceiptCodecTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Given_ScientificReceipt_When_RoundTripped_Then_PreservesFixedMembersAndNullIntake(int repeat)
    {
        WorkerReceipt receipt = Receipt(repeat);
        byte[] bytes = PhaseWireCodec.WriteReceipt(receipt);
        WorkerReceipt read = PhaseWireCodec.ReadReceipt(bytes);
        Assert.Equal(bytes, PhaseWireCodec.WriteReceipt(read));
        Assert.Null(read.Identity);
        Assert.Null(read.B10Intake);
        JsonObject root = JsonNode.Parse(bytes)!.AsObject();
        Assert.True(root.ContainsKey("b10_intake"));
        Assert.Null(root["b10_intake"]);
        Assert.Equal(receipt.Artifacts[0], Assert.Single(read.Artifacts));
    }

    [Theory]
    [InlineData("identity", "{}")]
    [InlineData("repeat", "null")]
    [InlineData("repeat", "0")]
    [InlineData("repeat", "6")]
    [InlineData("run_id", "\"b10d-1-1\"")]
    [InlineData("handler_exit_code", "1")]
    [InlineData("cleanup_completed", "false")]
    [InlineData("scenario_id", "\"A01\"")]
    [InlineData("configuration_id", "\"C0\"")]
    [InlineData("phase", "\"C0\"")]
    [InlineData("schema_version", "2")]
    [InlineData("job_name", "\"b10-1-1-c1-r2-b10-abrupt-verify\"")]
    public void Given_ReceiptFieldOutsideContract_When_Read_Then_FormatFailure(string field, string value)
    {
        JsonObject json = JsonNode.Parse(PhaseWireCodec.WriteReceipt(Receipt()))!.AsObject();
        json[field] = JsonNode.Parse(value);
        Assert.ThrowsAny<FormatException>(() => PhaseWireCodec.ReadReceipt(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Given_WrongArtifactCount_When_ReadOrWritten_Then_FormatFailure(int count)
    {
        WorkerReceipt receipt = Receipt();
        ArtifactReference[] artifacts = Enumerable.Repeat(receipt.Artifacts[0], count).ToArray();
        Assert.Throws<FormatException>(() => PhaseWireCodec.WriteReceipt(receipt with { Artifacts = artifacts }));
        JsonObject json = JsonNode.Parse(PhaseWireCodec.WriteReceipt(receipt))!.AsObject();
        JsonNode reference = json["artifacts"]![0]!.DeepClone();
        json["artifacts"] = new JsonArray(Enumerable.Range(0, count).Select(_ => reference.DeepClone()).ToArray());
        Assert.Throws<FormatException>(() => PhaseWireCodec.ReadReceipt(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Fact]
    public void Given_InvalidReceiptBindings_When_Written_Then_FormatFailure()
    {
        WorkerReceipt receipt = Receipt();
        WorkerReceipt[] rejected =
        [
            receipt with { Identity = new RunIdentity("B10", "C1", "b10-1-1", "pod", "pod", "client") },
            receipt with { Repeat = null }, receipt with { Repeat = 0 }, receipt with { Repeat = 6 },
            receipt with { RunId = "b10d-1-1" }, receipt with { HandlerExitCode = 1 },
            receipt with { CleanupCompleted = false }, receipt with { RepeatChecks = new("pass", "pass", "pass") },
            receipt with { Artifacts = [receipt.Artifacts[0] with { Name = "b10-1-1/C1/r2/b10-abrupt/nonfinal-result.json" }] },
            receipt with { Artifacts = [receipt.Artifacts[0] with { Container = "ledger" }] },
            receipt with { Artifacts = [receipt.Artifacts[0] with { Length = -1 }] },
            receipt with { Artifacts = [receipt.Artifacts[0] with { Sha256 = "invalid" }] },
            receipt with { JobName = "b10-1-1-c1-r2-b10-abrupt-verify" }
        ];
        Assert.All(rejected, invalid => Assert.Throws<FormatException>(() => PhaseWireCodec.WriteReceipt(invalid)));
    }

    [Fact]
    public void Given_MissingNullIntake_When_Read_Then_FormatFailure()
    {
        JsonObject json = JsonNode.Parse(PhaseWireCodec.WriteReceipt(Receipt()))!.AsObject();
        json.Remove("b10_intake");
        Assert.Throws<FormatException>(() => PhaseWireCodec.ReadReceipt(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    private static WorkerReceipt Receipt(int repeat = 3) => new(1, "b10-abrupt-verify", "B10", "C1", "b10-1-1", "C1", repeat,
        $"b10-1-1-c1-r{repeat}-b10-abrupt-verify", "verifier-pod", null, new string('a', 40), 0, true,
        "2026-11-13T00:00:00.0000000Z", "2026-11-13T00:00:01.0000000Z",
        [new("verifier", $"b10-1-1/C1/r{repeat}/b10-abrupt/nonfinal-result.json", 123, new string('b', 64))], null, null, null);
}
#pragma warning restore CA1707
