using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707
public sealed class PhaseWireConsumerTests
{
    public static TheoryData<string, string, string> ConsumerCases
    {
        get
        {
            var cases = new TheoryData<string, string, string>();
            foreach (string scenario in new[] { "C01", "C03", "C10" })
                foreach (string phase in new[] { "C0", "C1" })
                    foreach (string kind in Kinds)
                        cases.Add(scenario, phase, kind);
            return cases;
        }
    }

    public static TheoryData<string, string, string, string> RejectedCases
    {
        get
        {
            var cases = new TheoryData<string, string, string, string>();
            foreach ((string scenario, string phase, string configuration) in new[]
                { ("C03", "C0", "C1"), ("C03", "C2", "C2"), ("C02", "C0", "C0") })
                foreach (string kind in new[] { "registration", "completion", "verify" })
                    cases.Add(scenario, phase, configuration, kind);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ConsumerCases))]
    public void Given_ConsumerPair_U21_When_WrittenAndRead_Then_EveryFieldRoundTrips(string scenario, string phase, string kind)
    {
        if (kind == "registration")
        {
            ProducerRegistration value = Registration(scenario, phase, phase);
            Assert.Equal(value, PhaseWireCodec.ReadRegistration(PhaseWireCodec.WriteRegistration(value)));
        }
        else if (kind == "completion")
        {
            ProducerCompletionReceipt value = Completion(scenario, phase, phase);
            byte[] bytes = PhaseWireCodec.WriteProducerCompletion(value);
            Assert.Equal(value, PhaseWireCodec.ReadProducerCompletion(bytes));
            Assert.Equal(value, PhaseWireCodec.ReadProducerCompletion(bytes, phase, 1, Registration(scenario, phase, phase)));
        }
        else
        {
            WorkerReceipt value = Receipt(scenario, phase, phase, kind);
            AssertReceipt(value, PhaseWireCodec.ReadReceipt(PhaseWireCodec.WriteReceipt(value)));
        }
    }

    [Theory]
    [MemberData(nameof(RejectedCases))]
    public void Given_RejectedPair_U21_When_WrittenOrRead_Then_PreservesBaseExceptionTypes(string scenario, string phase, string configuration, string kind)
    {
        byte[] bytes = SubstituteCoordinates(WriteBase(kind), scenario, phase, configuration);
        if (kind == "registration")
        {
            Assert.Throws<FormatException>(() => PhaseWireCodec.WriteRegistration(Registration(scenario, phase, configuration)));
            Exception? error = Record.Exception(() => PhaseWireCodec.ReadRegistration(bytes));
            Assert.IsType(phase == "C2" ? typeof(PhaseWireIdentityException) : typeof(FormatException), error);
        }
        else if (kind == "completion")
        {
            ProducerCompletionReceipt value = Completion(scenario, phase, configuration);
            Assert.Equal(bytes, PhaseWireCodec.WriteProducerCompletion(value));
            Assert.Throws<PhaseWireIdentityException>(() => PhaseWireCodec.ReadProducerCompletion(bytes));
        }
        else
        {
            WorkerReceipt value = Receipt(scenario, phase, configuration, kind);
            Exception? writeError = Record.Exception(() => PhaseWireCodec.WriteReceipt(value));
            Assert.IsType(phase == "C2" ? typeof(ArgumentException) : typeof(FormatException), writeError);
            Exception? readError = Record.Exception(() => PhaseWireCodec.ReadReceipt(bytes));
            Assert.IsType(phase == "C2" ? typeof(PhaseWireIdentityException) : typeof(FormatException), readError);
        }
    }

    [Theory]
    [InlineData("C03", "C0")]
    [InlineData("C01", "C1")]
    [InlineData("C10", "C0")]
    public void Given_ConsumerJudgeReceipt_W1_When_WrittenAndRead_Then_RoundTripsEqual(string scenario, string phase)
    {
        WorkerReceipt value = Receipt(scenario, phase, phase, "judge");
        AssertReceipt(value, PhaseWireCodec.ReadReceipt(PhaseWireCodec.WriteReceipt(value)));
    }

    [Theory]
    [InlineData("b10-sweep-line")]
    [InlineData("b10-sweep")]
    [InlineData("b10-final-sweep")]
    public void Given_B10ModeReceiptNamingAConsumerScenario_W2_When_Written_Then_Rejects(string mode)
    {
        string artifact = mode switch
        {
            "b10-sweep-line" => PhasePaths.SweepLine(Run, "C0"),
            "b10-sweep" => PhasePaths.B10Finalization(Run, "C0"),
            _ => PhasePaths.B10FinalSweep(Run, "C0")
        };
        string job = mode switch
        {
            "b10-sweep-line" => PhaseIdentity.B10SweepLineJobName(Run, "C0"),
            "b10-sweep" => PhaseIdentity.B10SweepJobName(Run, "C0"),
            _ => PhaseIdentity.B10FinalSweepJobName(Run, "C0")
        };
        var value = new WorkerReceipt(1, mode, "C03", "C0", Run, "C0", null, job, "worker-pod", null, new string('a', 40), 0, true, Started, Completed,
            [new ArtifactReference("verifier", artifact, 123, new string('b', 64))], null, null);
        Assert.Throws<FormatException>(() => PhaseWireCodec.WriteReceipt(value));
    }

    [Fact]
    public void Given_B04JudgeReceipt_W3_When_Written_Then_BytesEqualTheBaseOutput()
    {
        // Captured on main at 911c5a1eaaac55107271755f2aa9697ea7a8acf9, before the judge accepted consumer pairs.
        const string baseOutput = """{"schema_version":1,"mode":"judge","scenario_id":"B04","configuration_id":"C0","run_id":"synthetic-c506","phase":"C0","repeat":null,"job_name":"synthetic-c506-c0-judge","pod_name":"worker-pod","identity":null,"source_commit":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","handler_exit_code":0,"cleanup_completed":true,"started_utc":"2026-11-15T00:00:00.0000000\u002B00:00","completed_utc":"2026-11-15T00:00:01.0000000\u002B00:00","artifacts":[{"container":"verifier","name":"synthetic-c506/C0/candidate.json","length":123,"sha256":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"}],"publication_acceptance":null,"repeat_checks":null}""";
        Assert.Equal(baseOutput, Encoding.UTF8.GetString(PhaseWireCodec.WriteReceipt(Receipt("B04", "C0", "C0", "judge"))));
    }

    [Theory]
    [InlineData("registration")]
    [InlineData("completion")]
    [InlineData("start-line")]
    [InlineData("verify")]
    public void Given_B04_U21_When_WrittenAndRead_Then_MatchesRecordedBaseBytesAndEveryField(string kind)
    {
        Assert.Equal(Encoding.UTF8.GetBytes(PhaseWireBaseFixtures.Get(kind)), WriteBase(kind));
        byte[] bytes = WriteBase(kind);
        if (kind == "registration") Assert.Equal(Registration("B04", "C0", "C0"), PhaseWireCodec.ReadRegistration(bytes));
        else if (kind == "completion") Assert.Equal(Completion("B04", "C0", "C0"), PhaseWireCodec.ReadProducerCompletion(bytes));
        else AssertReceipt(Receipt("B04", "C0", "C0", kind), PhaseWireCodec.ReadReceipt(bytes));
    }

    private static readonly string[] Kinds = ["registration", "completion", "start-line", "verify"];
    private const string Run = "synthetic-c506";
    private const string Started = "2026-11-15T00:00:00.0000000+00:00";
    private const string Completed = "2026-11-15T00:00:01.0000000+00:00";

    internal static byte[] WriteBase(string kind) => kind switch
    {
        "registration" => PhaseWireCodec.WriteRegistration(Registration("B04", "C0", "C0")),
        "completion" => PhaseWireCodec.WriteProducerCompletion(Completion("B04", "C0", "C0")),
        _ => PhaseWireCodec.WriteReceipt(Receipt("B04", "C0", "C0", kind))
    };

    private static ProducerRegistration Registration(string scenario, string phase, string configuration)
    {
        string job = PhaseIdentity.JobName(Run, phase == "C2" ? "C0" : phase, 1);
        var identity = new RunIdentity(scenario, configuration, Run, job, job, job);
        return new(1, identity, phase, 1, job, "producer-pod", "producer-uid", new string('a', 40));
    }

    private static ProducerCompletionReceipt Completion(string scenario, string phase, string configuration)
    {
        ProducerRegistration value = Registration(scenario, phase, configuration);
        return new(1, value.Identity, phase, 1, value.JobName, value.PodName, value.PodUid, value.SourceCommit, "loadgen", 0, true);
    }

    private static WorkerReceipt Receipt(string scenario, string phase, string configuration, string mode)
    {
        ProducerRegistration producer = Registration(scenario, phase, configuration);
        string pathPhase = phase == "C2" ? "C0" : phase;
        string job = mode switch
        {
            "start-line" => PhaseIdentity.StartLineJobName(Run, pathPhase, 1),
            "verify" => PhaseIdentity.VerifyJobName(Run, pathPhase, 1),
            _ => PhaseIdentity.JudgeJobName(Run, pathPhase)
        };
        ArtifactReference Reference(string name) => new("verifier", name, 123, new string('b', 64));
        ArtifactReference[] artifacts = mode switch
        {
            "start-line" => [Reference(PhasePaths.StartLine(Run, pathPhase, 1)), Reference(PhasePaths.BrokerInventory(Run, pathPhase, 1))],
            "verify" => [Reference(PhasePaths.Result(Run, pathPhase, 1))],
            _ => [Reference(PhasePaths.Candidate(Run, pathPhase))]
        };
        return new(1, mode, scenario, configuration, Run, phase, mode == "judge" ? null : 1, job, "worker-pod",
            mode == "judge" ? null : producer.Identity, producer.SourceCommit, 0, true, Started, Completed, artifacts, null,
            mode == "verify" ? new("pass", "pass", "pass") : null);
    }

    private static byte[] SubstituteCoordinates(byte[] bytes, string scenario, string phase, string configuration)
    {
        JsonObject root = JsonNode.Parse(bytes)!.AsObject();
        root["phase"] = phase;
        if (root.ContainsKey("scenario_id"))
        {
            root["scenario_id"] = scenario;
            root["configuration_id"] = configuration;
        }
        if (root["identity"] is JsonObject identity)
        {
            identity["scenario_id"] = scenario;
            identity["configuration_id"] = configuration;
        }
        return Encoding.UTF8.GetBytes(root.ToJsonString(new() { WriteIndented = false }));
    }

    private static void AssertReceipt(WorkerReceipt expected, WorkerReceipt actual)
    {
        Assert.Equal(expected with { Artifacts = actual.Artifacts }, actual);
        Assert.Equal(expected.Artifacts, actual.Artifacts);
    }
}
#pragma warning restore CA1707
