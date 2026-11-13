using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Xunit;

namespace Parchwell.Verifier.Tests.Results;

#pragma warning disable CA1707
public sealed class B10OneJobC0CodecTests
{
    [Theory]
    [InlineData("b10-1-1", "C0", 0, false)]
    [InlineData("b10-1-1", "C0", 1, true)]
    [InlineData("b10-1-1", "C0", 2, true)]
    [InlineData("b10-1-1", "C0", 3, false)]
    [InlineData("run-1", "C0", 1, false)]
    [InlineData("run-1", "C0", 2, true)]
    [InlineData("b10-1-1", "C1", 1, false)]
    [InlineData("b10-1-1", "C1", 2, true)]
    [InlineData("run-1", "C1", 2, true)]
    public void Given_IntakeShape_When_ValidatedAndRoundTripped_Then_OnlyScientificC0AddsSingleton(
        string run, string configuration, int count, bool accepted)
    {
        VerifierResult result = Result(run, configuration, count);
        if (!accepted)
        {
            Assert.Throws<FormatException>(() => PhaseWireCodec.ValidateB10Intake(result.B10Intake!, result.Identity, Commit));
            Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(result, Output));
            Assert.Throws<FormatException>(() => B10ProducerObservationsCodec.Validate(result.B10ProducerObservations!, result.B10Intake!));
            return;
        }
        PhaseWireCodec.ValidateB10Intake(result.B10Intake!, result.Identity, Commit);
        B10ProducerObservationsCodec.Validate(result.B10ProducerObservations!, result.B10Intake!);
        byte[] bytes = VerifierResultCodec.Write(result, Output);
        var read = VerifierResultCodec.Read(bytes, "result.json", Input, 8);
        Assert.True(read.IsSuccess);
        Assert.True(read.Value!.B10WindowChecksComplete);
        Assert.Equal(count, read.Value.B10Intake!.Producers.Count);
        Assert.Equal(count, read.Value.B10ProducerObservations!.Count);
    }

    [Fact]
    public void Given_C0CompleteValidResult_When_ReadOrWritten_Then_RejectsFinalStatus()
    {
        VerifierResult result = Result("b10-1-1", "C0", 1);
        Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(result with
        { Status = VerifierResultStatus.CompleteValid, Counts = new(1, 0, 0, 0, 0, 0) }, Output));
        JsonObject json = JsonNode.Parse(VerifierResultCodec.Write(result, Output))!.AsObject();
        json["status"] = "complete_valid";
        foreach (string name in new[] { "ok", "loss", "phantom", "reported_failure", "duplicate", "reorder" }) json[name] = 0;
        Assert.False(VerifierResultCodec.Read(Encoding.UTF8.GetBytes(json.ToJsonString()), "result.json", Input, 8).IsSuccess);
    }

    [Theory]
    [InlineData("job")]
    [InlineData("predecessor")]
    [InlineData("observation")]
    public void Given_SingletonRoleMismatch_When_ReadOrWritten_Then_Rejects(string mismatch)
    {
        VerifierResult result = Result("b10-1-1", "C0", 1);
        B10ProducerEvidence producer = result.B10Intake!.Producers[0];
        if (mismatch == "job") producer = producer with { JobName = "b10-1-1-c0-r1-loadgen2" };
        if (mismatch == "predecessor") producer = producer with { PredecessorInstanceId = "predecessor" };
        if (mismatch == "observation") result = result with { B10ProducerObservations = [] };
        result = result with { B10Intake = result.B10Intake with { Producers = [producer] } };
        if (mismatch == "observation") Assert.Throws<FormatException>(() => VerifierResultCodec.Write(result, Output));
        else Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(result, Output));
        Assert.Throws<FormatException>(() => B10ProducerObservationsCodec.Validate(result.B10ProducerObservations!, result.B10Intake));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Given_SingletonObservationWrongCount_When_Read_Then_Rejects(int count)
    {
        VerifierResult result = Result("b10-1-1", "C0", 1);
        JsonObject json = JsonNode.Parse(VerifierResultCodec.Write(result, Output))!.AsObject();
        JsonNode observation = json["b10_producer_observations"]![0]!.DeepClone();
        json["b10_producer_observations"] = new JsonArray(Enumerable.Range(0, count).Select(_ => observation.DeepClone()).ToArray());
        Assert.False(VerifierResultCodec.Read(Encoding.UTF8.GetBytes(json.ToJsonString()), "result.json", Input, 8).IsSuccess);
        using JsonDocument document = JsonDocument.Parse(json.ToJsonString());
        Assert.Throws<FormatException>(() => B10ProducerObservationsCodec.Read(document.RootElement.GetProperty("b10_producer_observations"), result.B10Intake!));
    }

    private static readonly string Commit = new('a', 40);
    private static readonly OutputLimits Output = new(65536, 8, 256);
    private static readonly InputLimits Input = new(32, 65536, 131072, 65536, 1000, 1000, 256);
    private static readonly string[] PublicationNames = ["index.json", "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
    private static ArtifactReference Reference(string container, string name) => new(container, name, 1, new string('b', 64));

    internal static VerifierResult Result(string run, string configuration, int count)
    {
        var identity = new RunIdentity("B10", configuration, run, "producer-0", "producer-0", "client-0");
        B10ProducerEvidence[] producers = Enumerable.Range(0, count).Select(index =>
        {
            var producer = identity with { InstanceId = $"producer-{index}", ExpectedInstanceId = $"producer-{index}", ClientId = $"client-{index}" };
            var acceptance = new PublicationAcceptanceSnapshot(PhasePaths.ProducerPrefix(run, configuration, 1, producer.InstanceId), producer, Commit,
                Reference("verifier", PhasePaths.StartLine(run, configuration, 1)),
                Reference("verifier", PhasePaths.BrokerInventory(run, configuration, 1)),
                Reference("ledger", PhasePaths.ProducerCompletion(run, configuration, 1, producer.InstanceId)),
                PublicationNames.Select(name => Reference("ledger", PhasePaths.Publication(run, configuration, 1, producer.InstanceId, name))).ToArray());
            return new B10ProducerEvidence(producer, index == 0 ? PhaseIdentity.JobName(run, configuration, 1) : PhaseIdentity.RestartJobName(run, configuration, 1),
                index == 0 ? null : "producer-0", acceptance);
        }).ToArray();
        return new(1, identity, null, null, Commit, "synthetic-managed", "synthetic-native", VerifierResultStatus.Incomplete, [],
            [new(0, 0)], [new(0, 1)], null, null, null, null, null, null, null, [], [],
            B10Intake: new(configuration, 1, producers), ClassificationFinal: false, B10WindowChecksComplete: true,
            B10ProducerObservations: producers.Select(producer => new B10ProducerObservation(producer.Identity.InstanceId, null, null, null)).ToArray());
    }
}
#pragma warning restore CA1707
