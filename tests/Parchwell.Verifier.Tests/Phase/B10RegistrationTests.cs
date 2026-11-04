using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Xunit;

namespace Parchwell.Verifier.Tests.Phase;

#pragma warning disable CA1707
// Registration admits the second B10 Job without implying that its publication is accepted.
public sealed class B10RegistrationTests
{
    private const string ProducerJson = """
        {"schema_version":1,"identity":{"scenario_id":"B10","configuration_id":"C0","run_id":"synthetic-b10","instance_id":"second-pod","expected_instance_id":"second-pod","client_id":"second-pod"},"phase":"C0","repeat_number":1,"job_name":"synthetic-b10-c0-r1-loadgen2","pod_name":"second-pod","pod_uid":"second-uid","source_commit":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","predecessor_instance_id":"first-pod"}
        """;

    [Fact]
    public void Given_ProducerShapedRestartRegistration_When_Read_Then_PreservesPredecessor()
    {
        ProducerRegistration actual = PhaseWireCodec.ReadRegistration(Encoding.UTF8.GetBytes(ProducerJson));
        Assert.Equal(Registration(), actual);
        Assert.Equal("first-pod", actual.PredecessorInstanceId);
    }

    [Theory]
    [InlineData("C0", 1, false)]
    [InlineData("C0", 1, true)]
    [InlineData("C0", 5, false)]
    [InlineData("C0", 5, true)]
    [InlineData("C1", 1, false)]
    [InlineData("C1", 1, true)]
    [InlineData("C1", 5, false)]
    [InlineData("C1", 5, true)]
    public void Given_B10FirstOrSecondRole_When_RoundTripped_Then_PreservesRegistration(string phase, int repeat, bool restart)
    {
        ProducerRegistration value = Registration() with
        {
            Identity = Registration().Identity with { ConfigurationId = phase },
            Phase = phase,
            RepeatNumber = repeat,
            JobName = restart ? PhaseIdentity.RestartJobName("synthetic-b10", phase, repeat) : PhaseIdentity.JobName("synthetic-b10", phase, repeat),
            PredecessorInstanceId = restart ? "first-pod" : null
        };
        byte[] bytes = PhaseWireCodec.WriteRegistration(value);
        Assert.Equal(value, PhaseWireCodec.ReadRegistration(bytes));
        Assert.Equal(restart, JsonNode.Parse(bytes)!.AsObject().ContainsKey("predecessor_instance_id"));
    }

    [Fact]
    public void Given_RestartJobCoordinates_When_Derived_Then_UsesExactJobNames()
    {
        Assert.Equal("synthetic-b10-c0-r1-loadgen2", PhaseIdentity.RestartJobName("synthetic-b10", "C0", 1));
        Assert.Equal("synthetic-b10-c1-r5-loadgen2", PhaseIdentity.RestartJobName("synthetic-b10", "C1", 5));
    }

    [Theory]
    [InlineData("missing-predecessor")]
    [InlineData("first-job")]
    [InlineData("A01")]
    [InlineData("B04")]
    [InlineData("B06")]
    [InlineData("B07")]
    [InlineData("wrong-phase")]
    [InlineData("wrong-repeat")]
    [InlineData("wrong-suffix")]
    public void Given_InvalidRegistrationRole_When_ReadOrWritten_Then_Rejects(string defect)
    {
        ProducerRegistration value = defect switch
        {
            "missing-predecessor" => Registration() with { PredecessorInstanceId = null },
            "first-job" => Registration() with { JobName = "synthetic-b10-c0-r1-loadgen" },
            "wrong-phase" => Registration() with { JobName = "synthetic-b10-c1-r1-loadgen2" },
            "wrong-repeat" => Registration() with { JobName = "synthetic-b10-c0-r2-loadgen2" },
            "wrong-suffix" => Registration() with { JobName = "synthetic-b10-c0-r1-loadgen3" },
            _ => Registration() with { Identity = Registration().Identity with { ScenarioId = defect } }
        };
        AssertRejectedByReaderAndWriter(value);
    }

    [Theory]
    [InlineData("second-pod")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("first pod")]
    [InlineData("first/pod")]
    [InlineData("first\\pod")]
    [InlineData("first\u0001pod")]
    [InlineData("first.pod")]
    public void Given_InvalidPredecessor_When_ReadOrWritten_Then_Rejects(string predecessor) =>
        AssertRejectedByReaderAndWriter(Registration() with { PredecessorInstanceId = predecessor });

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(127, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Given_PredecessorLengthBoundary_When_ReadOrWritten_Then_EnforcesSegmentBound(int length, bool accepted)
    {
        ProducerRegistration value = Registration() with { PredecessorInstanceId = new string('p', length) };
        if (accepted) Assert.Equal(value, PhaseWireCodec.ReadRegistration(PhaseWireCodec.WriteRegistration(value)));
        else AssertRejectedByReaderAndWriter(value);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("number")]
    [InlineData("unknown")]
    [InlineData("missing-required")]
    public void Given_MalformedRestartFields_When_Read_Then_RejectsClosedSchema(string defect)
    {
        JsonObject json = JsonNode.Parse(ProducerJson)!.AsObject();
        switch (defect)
        {
            case "null": json["predecessor_instance_id"] = null; break;
            case "number": json["predecessor_instance_id"] = 1; break;
            case "unknown": json["unknown"] = true; break;
            case "missing-required": json.Remove("source_commit"); break;
        }
        Assert.Throws<FormatException>(() => PhaseWireCodec.ReadRegistration(Bytes(json)));
    }

    [Fact]
    public void Given_DuplicatePredecessor_When_Read_Then_Rejects()
    {
        string json = ProducerJson[..^1] + ",\"predecessor_instance_id\":\"other-pod\"}";
        Assert.Throws<FormatException>(() => PhaseWireCodec.ReadRegistration(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Given_OutOfRangeRepeat_When_DerivingRestartJob_Then_Rejects(int repeat) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseIdentity.RestartJobName("synthetic-b10", "C0", repeat));

    [Theory]
    [InlineData(28, true)]
    [InlineData(29, true)]
    [InlineData(30, false)]
    public void Given_RunLengthBoundary_When_DerivingRestartJob_Then_EnforcesRunBound(int length, bool accepted)
    {
        string run = new('r', length);
        if (accepted) Assert.Equal($"{run}-c0-r1-loadgen2", PhaseIdentity.RestartJobName(run, "C0", 1));
        else Assert.Throws<ArgumentException>(() => PhaseIdentity.RestartJobName(run, "C0", 1));
    }

    [Theory]
    [InlineData("UPPER", "C0")]
    [InlineData("synthetic-b10", "foreign")]
    public void Given_InvalidRunOrPhase_When_DerivingRestartJob_Then_Rejects(string run, string phase) =>
        Assert.Throws<ArgumentException>(() => PhaseIdentity.RestartJobName(run, phase, 1));

    [Fact]
    public void Given_SecondB10JobCompletion_When_Read_Then_RemainsOutsidePublicationAcceptance()
    {
        ProducerRegistration value = Registration();
        var completion = new ProducerCompletionReceipt(value.SchemaVersion, value.Identity, value.Phase, value.RepeatNumber,
            value.JobName, value.PodName, value.PodUid, value.SourceCommit, "loadgen", 0, true);
        Assert.Throws<PhaseWireIdentityException>(() => PhaseWireCodec.ReadProducerCompletion(PhaseWireCodec.WriteProducerCompletion(completion)));
    }

    private static ProducerRegistration Registration() => new(1,
        new RunIdentity("B10", "C0", "synthetic-b10", "second-pod", "second-pod", "second-pod"),
        "C0", 1, "synthetic-b10-c0-r1-loadgen2", "second-pod", "second-uid", new string('a', 40), "first-pod");

    private static void AssertRejectedByReaderAndWriter(ProducerRegistration value)
    {
        Assert.Throws<FormatException>(() => PhaseWireCodec.WriteRegistration(value));
        JsonObject json = JsonNode.Parse(ProducerJson)!.AsObject();
        json["job_name"] = value.JobName;
        json["identity"]!["scenario_id"] = value.Identity.ScenarioId;
        if (value.PredecessorInstanceId is null) json.Remove("predecessor_instance_id");
        else json["predecessor_instance_id"] = value.PredecessorInstanceId;
        Assert.Throws<FormatException>(() => PhaseWireCodec.ReadRegistration(Bytes(json)));
    }

    private static byte[] Bytes(JsonObject json) => Encoding.UTF8.GetBytes(json.ToJsonString());
}
#pragma warning restore CA1707
