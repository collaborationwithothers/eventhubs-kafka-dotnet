using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Xunit;

namespace Parchwell.Verifier.Tests.Phase;

#pragma warning disable CA1707
public sealed class B10IntakeWireTests
{
    private static readonly string[] PublicationNames = ["index.json", "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
    private const string Run = "synthetic-c5";
    private static readonly string Commit = new('a', 40);
    private static readonly OutputLimits Output = new(65_536, 8, 256);
    private static readonly InputLimits Input = new(32, 65_536, 131_072, 65_536, 1000, 1000, 256);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_EachRegisteredJob_When_ContextCompletionRead_Then_BindsAllFields(bool restart)
    {
        ProducerRegistration registration = Registration(restart);
        ProducerCompletionReceipt completion = Completion(registration);
        Assert.Equal(completion, PhaseWireCodec.ReadProducerCompletion(PhaseWireCodec.WriteProducerCompletion(completion), "C0", 1, registration));
        if (restart)
            Assert.Throws<PhaseWireIdentityException>(() => PhaseWireCodec.ReadProducerCompletion(PhaseWireCodec.WriteProducerCompletion(completion)));
    }

    [Theory]
    [InlineData("schema_version", "2")]
    [InlineData("identity.scenario_id", "\"A01\"")]
    [InlineData("identity.configuration_id", "\"C1\"")]
    [InlineData("identity.run_id", "\"foreign\"")]
    [InlineData("identity.instance_id", "\"foreign\"")]
    [InlineData("identity.expected_instance_id", "\"foreign\"")]
    [InlineData("identity.client_id", "\"foreign\"")]
    [InlineData("phase", "\"C1\"")]
    [InlineData("repeat_number", "2")]
    [InlineData("job_name", "\"foreign\"")]
    [InlineData("pod_name", "\"foreign\"")]
    [InlineData("pod_uid", "\"foreign\"")]
    [InlineData("source_commit", "\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"")]
    public void Given_CompletionBindingMismatch_When_ContextRead_Then_IdentityFailure(string path, string replacement)
    {
        foreach (bool restart in new[] { false, true })
        {
            ProducerRegistration registration = Registration(restart);
            JsonObject json = Object(PhaseWireCodec.WriteProducerCompletion(Completion(registration)));
            Set(json, path, replacement);
            Assert.Throws<PhaseWireIdentityException>(() => PhaseWireCodec.ReadProducerCompletion(Bytes(json), "C0", 1, registration));
        }
    }

    [Theory]
    [InlineData("mode", "\"verify\"")]
    [InlineData("handler_exit_code", "1")]
    [InlineData("cleanup_completed", "false")]
    [InlineData("schema_version", "\"1\"")]
    [InlineData("identity", "null")]
    [InlineData("cleanup_completed", "0")]
    public void Given_UnsuccessfulOrMalformedCompletion_When_ContextRead_Then_FormatFailure(string path, string replacement)
    {
        ProducerRegistration registration = Registration(true);
        JsonObject json = Object(PhaseWireCodec.WriteProducerCompletion(Completion(registration)));
        Set(json, path, replacement);
        Assert.Throws<FormatException>(() => PhaseWireCodec.ReadProducerCompletion(Bytes(json), "C0", 1, registration));
    }

    [Theory]
    [InlineData("C1", 1)]
    [InlineData("C0", 2)]
    public void Given_RegistrationOutsideExpectedRepeat_When_ContextRead_Then_IdentityFailure(string phase, int repeat)
    {
        ProducerRegistration registration = Registration(true);
        Assert.Throws<PhaseWireIdentityException>(() => PhaseWireCodec.ReadProducerCompletion(
            PhaseWireCodec.WriteProducerCompletion(Completion(registration)), phase, repeat, registration));
    }

    [Fact]
    public void Given_InvalidSuppliedRegistration_When_ContextRead_Then_RejectsRegistration()
    {
        ProducerRegistration registration = Registration(true);
        Assert.Throws<FormatException>(() => PhaseWireCodec.ReadProducerCompletion(
            PhaseWireCodec.WriteProducerCompletion(Completion(registration)), "C0", 1, registration with { PredecessorInstanceId = null }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_CoherentPair_When_ReceiptAndResultRoundTripped_Then_PreservesIndependentReferences(bool snapshots)
    {
        B10IntakeEvidence intake = Intake(snapshots);
        WorkerReceipt receipt = PhaseWireCodec.ReadReceipt(PhaseWireCodec.WriteReceipt(Receipt(intake)));
        VerifierResult result = ReadResult(VerifierResultCodec.Write(Result(intake), Output)).Value!;
        foreach (B10IntakeEvidence actual in new[] { receipt.B10Intake!, result.B10Intake! })
        {
            Assert.Equal("C0", actual.Phase);
            Assert.Equal(1, actual.RepeatNumber);
            Assert.Equal(2, actual.Producers.Count);
            for (int index = 0; index < 2; index++)
            {
                B10ProducerEvidence expected = intake.Producers[index];
                B10ProducerEvidence producer = actual.Producers[index];
                Assert.Equal(expected.Identity, producer.Identity);
                Assert.Equal(expected.JobName, producer.JobName);
                Assert.Equal(expected.PredecessorInstanceId, producer.PredecessorInstanceId);
                if (snapshots)
                {
                    Assert.Equal(expected.PublicationAcceptance!.ProducerIdentity, producer.PublicationAcceptance!.ProducerIdentity);
                    Assert.Equal(expected.PublicationAcceptance.SourceCommit, producer.PublicationAcceptance.SourceCommit);
                    Assert.Equal(expected.PublicationAcceptance.ProducerPrefix, producer.PublicationAcceptance.ProducerPrefix);
                    Assert.Equal(expected.PublicationAcceptance.StartLine, producer.PublicationAcceptance.StartLine);
                    Assert.Equal(expected.PublicationAcceptance.BrokerInventory, producer.PublicationAcceptance.BrokerInventory);
                    Assert.Equal(expected.PublicationAcceptance.ProducerReceipt, producer.PublicationAcceptance.ProducerReceipt);
                    Assert.Equal(expected.PublicationAcceptance.Objects, producer.PublicationAcceptance.Objects);
                }
                else Assert.Null(producer.PublicationAcceptance);
            }
        }
        Assert.Null(receipt.PublicationAcceptance);
        Assert.Equal(1, receipt.HandlerExitCode);
        Assert.Equal(VerifierResultStatus.Incomplete, result.Status);
        JsonObject wire = Object(VerifierResultCodec.Write(Result(intake) with { Counts = new(1, 2, 3, 4, 5, 6) }, Output));
        foreach (string count in new[] { "ok", "loss", "phantom", "reported_failure", "duplicate", "reorder" })
            Assert.False(wire.ContainsKey(count));
        JsonObject first = wire["b10_intake"]!["producers"]![0]!.AsObject();
        Assert.True(first.ContainsKey("predecessor_instance_id"));
        Assert.True(first.ContainsKey("publication_acceptance"));
        Assert.Null(first["predecessor_instance_id"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Given_ProducerCardinality_When_ReadOrWritten_Then_ExactlyTwoPass(int count)
    {
        B10IntakeEvidence intake = Intake();
        B10ProducerEvidence[] producers = Enumerable.Range(0, count).Select(index => intake.Producers[index % 2]).ToArray();
        B10IntakeEvidence changed = intake with { Producers = producers };
        if (count == 2)
        {
            Assert.NotNull(PhaseWireCodec.ReadReceipt(PhaseWireCodec.WriteReceipt(Receipt(changed))).B10Intake);
            Assert.True(ReadResult(VerifierResultCodec.Write(Result(changed), Output)).IsSuccess);
        }
        else
        {
            Assert.Throws<FormatException>(() => PhaseWireCodec.WriteReceipt(Receipt(changed)));
            Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(Result(changed), Output));
            AssertWireRejected(node =>
            {
                JsonArray array = node["producers"]!.AsArray();
                JsonNode first = array[0]!.DeepClone();
                while (array.Count > count) array.RemoveAt(array.Count - 1);
                while (array.Count < count) array.Add(first.DeepClone());
            });
        }
    }

    [Theory]
    [InlineData("phase", "\"C1\"")]
    [InlineData("phase", "\"foreign\"")]
    [InlineData("repeat", "0")]
    [InlineData("repeat", "6")]
    [InlineData("producers", "null")]
    [InlineData("producers.0.identity.instance_id", "\"foreign\"")]
    [InlineData("producers.1.identity.scenario_id", "\"A01\"")]
    [InlineData("producers.1.identity.configuration_id", "\"C1\"")]
    [InlineData("producers.1.identity.run_id", "\"foreign\"")]
    [InlineData("producers.1.identity.expected_instance_id", "\"foreign\"")]
    [InlineData("producers.1.identity.client_id", "\"\"")]
    [InlineData("producers.0.job_name", "\"foreign\"")]
    [InlineData("producers.1.job_name", "\"foreign\"")]
    [InlineData("producers.0.predecessor_instance_id", "\"pod-second\"")]
    [InlineData("producers.1.predecessor_instance_id", "null")]
    [InlineData("producers.1.predecessor_instance_id", "\"foreign\"")]
    [InlineData("producers.1.predecessor_instance_id", "\"pod-second\"")]
    [InlineData("producers.1.publication_acceptance.producer_identity.instance_id", "\"pod-first\"")]
    [InlineData("producers.1.publication_acceptance.source_commit", "\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"")]
    [InlineData("producers.1.publication_acceptance.producer_prefix", "\"foreign\"")]
    [InlineData("producers.1.publication_acceptance.producer_receipt.name", "\"foreign\"")]
    [InlineData("producers.1.publication_acceptance.producer_receipt.container", "\"verifier\"")]
    [InlineData("producers.1.publication_acceptance.start_line.name", "\"foreign\"")]
    [InlineData("producers.1.publication_acceptance.broker_inventory.name", "\"foreign\"")]
    [InlineData("producers.1.publication_acceptance.objects.0.name", "\"foreign\"")]
    [InlineData("producers.1.publication_acceptance.objects.0.length", "-1")]
    [InlineData("producers.1.publication_acceptance.objects.0.sha256", "\"invalid\"")]
    [InlineData("producers.1.publication_acceptance.status", "\"rejected\"")]
    public void Given_InvalidIntakeField_When_ReceiptOrResultRead_Then_Rejects(string path, string replacement) =>
        AssertWireRejected(node => Set(node, path, replacement));

    [Theory]
    [InlineData("phase")]
    [InlineData("repeat")]
    [InlineData("producers")]
    [InlineData("producers.1.identity")]
    [InlineData("producers.1.job_name")]
    [InlineData("producers.1.predecessor_instance_id")]
    [InlineData("producers.1.publication_acceptance")]
    public void Given_MissingIntakeField_When_ReceiptOrResultRead_Then_Rejects(string path) =>
        AssertWireRejected(node => Remove(node, path));

    [Theory]
    [InlineData("duplicate_identity")]
    [InlineData("reference_order")]
    [InlineData("unknown_intake")]
    [InlineData("unknown_producer")]
    [InlineData("empty_objects")]
    public void Given_InvalidIntakeCollection_When_ReceiptOrResultRead_Then_Rejects(string mutation) =>
        AssertWireRejected(node =>
        {
            JsonArray producers = node["producers"]!.AsArray();
            switch (mutation)
            {
                case "duplicate_identity": producers[1]!["identity"] = producers[0]!["identity"]!.DeepClone(); break;
                case "reference_order":
                    JsonArray objects = producers[1]!["publication_acceptance"]!["objects"]!.AsArray();
                    JsonNode first = objects[0]!.DeepClone();
                    objects[0] = objects[1]!.DeepClone(); objects[1] = first; break;
                case "unknown_intake": node["extra"] = true; break;
                case "unknown_producer": producers[1]!["extra"] = true; break;
                case "empty_objects": producers[1]!["publication_acceptance"]!["objects"] = new JsonArray(); break;
            }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_AbsentOrNullIntake_When_B10Read_Then_Rejects(bool explicitNull)
    {
        JsonObject receipt = Object(PhaseWireCodec.WriteReceipt(Receipt(Intake())));
        JsonObject result = Object(VerifierResultCodec.Write(Result(Intake()), Output));
        foreach (JsonObject json in new[] { receipt, result })
            if (explicitNull) json["b10_intake"] = null; else json.Remove("b10_intake");
        Assert.Throws<FormatException>(() => PhaseWireCodec.ReadReceipt(Bytes(receipt)));
        Assert.False(ReadResult(Bytes(result)).IsSuccess);
        Assert.Throws<FormatException>(() => PhaseWireCodec.WriteReceipt(Receipt(Intake()) with { B10Intake = null }));
        Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(Result(Intake()) with { B10Intake = null }, Output));
    }

    [Fact]
    public void Given_CompleteValidB10_When_ReadOrWritten_Then_RejectsEvenWithCounts()
    {
        VerifierResult result = Result(Intake()) with { Status = VerifierResultStatus.CompleteValid, Counts = new(1, 0, 0, 0, 0, 0) };
        Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(result, Output));
        JsonObject wire = Object(VerifierResultCodec.Write(Result(Intake()), Output));
        wire["status"] = "complete_valid";
        foreach (string count in new[] { "ok", "loss", "phantom", "reported_failure", "duplicate", "reorder" }) wire[count] = 0;
        Assert.False(ReadResult(Bytes(wire)).IsSuccess);
    }

    [Theory]
    [InlineData("handler_exit_code")]
    [InlineData("publication_acceptance")]
    [InlineData("repeat_checks")]
    [InlineData("repeat")]
    public void Given_ForbiddenB10ReceiptClaim_When_ReadOrWritten_Then_Rejects(string field)
    {
        WorkerReceipt receipt = Receipt(Intake());
        WorkerReceipt changed = field switch
        {
            "handler_exit_code" => receipt with { HandlerExitCode = 0 },
            "publication_acceptance" => receipt with { PublicationAcceptance = receipt.B10Intake!.Producers[0].PublicationAcceptance },
            "repeat_checks" => receipt with { RepeatChecks = null },
            _ => receipt with { B10Intake = receipt.B10Intake! with { RepeatNumber = 2 } }
        };
        Assert.Throws<FormatException>(() => PhaseWireCodec.WriteReceipt(changed));
        JsonObject json = Object(PhaseWireCodec.WriteReceipt(receipt));
        if (field == "handler_exit_code") json[field] = 0;
        else if (field == "publication_acceptance") json[field] = json["b10_intake"]!["producers"]![0]![field]!.DeepClone();
        else if (field == "repeat_checks") json[field] = null;
        else json["b10_intake"]![field] = 2;
        Assert.Throws<FormatException>(() => PhaseWireCodec.ReadReceipt(Bytes(json)));
    }

    [Theory]
    [InlineData("A01", "verify")]
    [InlineData("B10", "start-line")]
    [InlineData("B10", "judge")]
    public void Given_ExtensionOutsideB10Verify_When_ReceiptReadOrWritten_Then_Rejects(string scenario, string mode)
    {
        WorkerReceipt value = Receipt(Intake()) with { ScenarioId = scenario, Mode = mode };
        Assert.ThrowsAny<FormatException>(() => PhaseWireCodec.WriteReceipt(value));
        JsonObject json = Object(PhaseWireCodec.WriteReceipt(Receipt(Intake())));
        json["scenario_id"] = scenario; json["mode"] = mode;
        Assert.ThrowsAny<FormatException>(() => PhaseWireCodec.ReadReceipt(Bytes(json)));
    }

    [Fact]
    public void Given_NonB10_When_Written_Then_OmitsExtensionAndRejectsAddedExtension()
    {
        RunIdentity identity = Identity(false) with { ScenarioId = "A01" };
        WorkerReceipt receipt = Receipt(Intake()) with { ScenarioId = "A01", Identity = identity, B10Intake = null };
        VerifierResult result = Result(Intake()) with { Identity = identity, B10Intake = null };
        JsonObject receiptJson = Object(PhaseWireCodec.WriteReceipt(receipt));
        JsonObject resultJson = Object(VerifierResultCodec.Write(result, Output));
        Assert.False(receiptJson.ContainsKey("b10_intake"));
        Assert.False(resultJson.ContainsKey("b10_intake"));
        Assert.True(ReadResult(Bytes(resultJson)).IsSuccess);
        Assert.Null(PhaseWireCodec.ReadReceipt(Bytes(receiptJson)).B10Intake);
        Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(result with { B10Intake = Intake() }, Output));
        resultJson["b10_intake"] = Object(VerifierResultCodec.Write(Result(Intake()), Output))["b10_intake"]!.DeepClone();
        Assert.False(ReadResult(Bytes(resultJson)).IsSuccess);
    }

    [Fact]
    public void Given_DuplicateIntakeProperty_When_Read_Then_ClosedReadersReject()
    {
        byte[] receipt = PhaseWireCodec.WriteReceipt(Receipt(Intake()));
        byte[] result = VerifierResultCodec.Write(Result(Intake()), Output);
        static byte[] Duplicate(byte[] bytes) => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace(
            "\"b10_intake\":{\"phase\":\"C0\"", "\"b10_intake\":{\"phase\":\"C0\",\"phase\":\"C0\"", StringComparison.Ordinal));
        Assert.Throws<FormatException>(() => PhaseWireCodec.ReadReceipt(Duplicate(receipt)));
        Assert.False(ReadResult(Duplicate(result)).IsSuccess);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Given_IntakeRepeatBoundary_When_ResultReadOrWritten_Then_EnforcesOneThroughFive(int repeat, bool accepted)
    {
        B10IntakeEvidence original = Intake(false);
        B10IntakeEvidence intake = original with
        {
            RepeatNumber = repeat,
            Producers = original.Producers.Select((producer, index) => producer with
            { JobName = $"synthetic-c5-c0-r{repeat}-{(index == 0 ? "loadgen" : "loadgen2")}" }).ToArray()
        };
        if (accepted) Assert.True(ReadResult(VerifierResultCodec.Write(Result(intake), Output)).IsSuccess);
        else Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(Result(intake), Output));
    }

    [Theory]
    [InlineData("instance", 0, false)]
    [InlineData("instance", 127, true)]
    [InlineData("instance", 128, true)]
    [InlineData("instance", 129, false)]
    [InlineData("client", 0, false)]
    [InlineData("client", 252, true)]
    [InlineData("client", 253, true)]
    [InlineData("client", 254, false)]
    public void Given_IntakeIdentityBounds_When_ReadOrWritten_Then_EnforcesPhaseLimits(string field, int length, bool accepted)
    {
        B10IntakeEvidence original = Intake(false);
        RunIdentity identity = original.Producers[1].Identity;
        identity = field == "instance" ? identity with { InstanceId = new('x', length), ExpectedInstanceId = new('x', length) }
            : identity with { ClientId = new('x', length) };
        B10IntakeEvidence intake = original with { Producers = [original.Producers[0], original.Producers[1] with { Identity = identity }] };
        if (accepted)
        {
            Assert.True(ReadResult(VerifierResultCodec.Write(Result(intake), Output)).IsSuccess);
            Assert.NotNull(PhaseWireCodec.ReadReceipt(PhaseWireCodec.WriteReceipt(Receipt(intake))).B10Intake);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(Result(intake), Output));
            Assert.Throws<FormatException>(() => PhaseWireCodec.WriteReceipt(Receipt(intake)));
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void Given_AcceptanceObjectCount_When_ReadOrWritten_Then_RequiresSix(int count, bool accepted)
    {
        B10IntakeEvidence original = Intake();
        PublicationAcceptanceSnapshot acceptance = original.Producers[1].PublicationAcceptance!;
        acceptance = acceptance with { Objects = Enumerable.Range(0, count).Select(index => acceptance.Objects[index % 6]).ToArray() };
        B10IntakeEvidence intake = original with { Producers = [original.Producers[0], original.Producers[1] with { PublicationAcceptance = acceptance }] };
        if (accepted) Assert.True(ReadResult(VerifierResultCodec.Write(Result(intake), Output)).IsSuccess);
        else
        {
            Assert.Throws<ArgumentException>(() => VerifierResultCodec.Write(Result(intake), Output));
            Assert.Throws<FormatException>(() => PhaseWireCodec.WriteReceipt(Receipt(intake)));
        }
    }

    private static void AssertWireRejected(Action<JsonObject> mutate)
    {
        JsonObject receipt = Object(PhaseWireCodec.WriteReceipt(Receipt(Intake())));
        JsonObject result = Object(VerifierResultCodec.Write(Result(Intake()), Output));
        mutate(receipt["b10_intake"]!.AsObject()); mutate(result["b10_intake"]!.AsObject());
        Assert.ThrowsAny<FormatException>(() => PhaseWireCodec.ReadReceipt(Bytes(receipt)));
        Assert.False(ReadResult(Bytes(result)).IsSuccess);
    }

    private static JsonNode Parent(JsonNode node, string path, out string leaf)
    {
        string[] parts = path.Split('.'); leaf = parts[^1];
        foreach (string part in parts[..^1]) node = node is JsonArray array ? array[int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)]! : node[part]!;
        return node;
    }
    private static void Set(JsonNode node, string path, string replacement)
    {
        JsonNode parent = Parent(node, path, out string leaf);
        parent[leaf] = JsonNode.Parse(replacement);
    }
    private static void Remove(JsonNode node, string path)
    {
        JsonNode parent = Parent(node, path, out string leaf);
        parent.AsObject().Remove(leaf);
    }
    private static JsonObject Object(byte[] bytes) => JsonNode.Parse(bytes)!.AsObject();
    private static byte[] Bytes(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString());
    private static ParseResult<VerifierResult> ReadResult(byte[] bytes) => VerifierResultCodec.Read(bytes, "result.json", Input, 8);
    private static RunIdentity Identity(bool restart) => new("B10", "C0", Run, restart ? "pod-second" : "pod-first", restart ? "pod-second" : "pod-first", restart ? "pod-second" : "pod-first");
    private static ProducerRegistration Registration(bool restart) => new(1, Identity(restart), "C0", 1,
        restart ? PhaseIdentity.RestartJobName(Run, "C0", 1) : PhaseIdentity.JobName(Run, "C0", 1),
        Identity(restart).InstanceId, restart ? "uid-second" : "uid-first", Commit, restart ? "pod-first" : null);
    private static ProducerCompletionReceipt Completion(ProducerRegistration registration) => new(1, registration.Identity, registration.Phase,
        registration.RepeatNumber, registration.JobName, registration.PodName, registration.PodUid, registration.SourceCommit, "loadgen", 0, true);
    private static ArtifactReference Reference(string container, string name) => new(container, name, 1, new string('b', 64));
    private static PublicationAcceptanceSnapshot Acceptance(RunIdentity identity) => new(
        PhasePaths.ProducerPrefix(Run, "C0", 1, identity.InstanceId), identity, Commit,
        Reference("verifier", PhasePaths.StartLine(Run, "C0", 1)), Reference("verifier", PhasePaths.BrokerInventory(Run, "C0", 1)),
        Reference("ledger", PhasePaths.ProducerCompletion(Run, "C0", 1, identity.InstanceId)),
        PublicationNames
            .Select(name => Reference("ledger", PhasePaths.Publication(Run, "C0", 1, identity.InstanceId, name))).ToArray());
    private static B10IntakeEvidence Intake(bool snapshots = true) => new("C0", 1,
        new[] { Registration(false), Registration(true) }.Select(registration => new B10ProducerEvidence(registration.Identity,
            registration.JobName, registration.PredecessorInstanceId, snapshots ? Acceptance(registration.Identity) : null)).ToArray());
    private static WorkerReceipt Receipt(B10IntakeEvidence intake) => new(1, "verify", "B10", "C0", Run, "C0", 1,
        PhaseIdentity.VerifyJobName(Run, "C0", 1), "verify-pod", Identity(false), Commit, 1, true,
        "2026-10-24T00:00:00.0000000+00:00", "2026-10-24T00:00:01.0000000+00:00",
        [Reference("verifier", PhasePaths.Result(Run, "C0", 1))], null, new("pass", "pass", "pass"), intake);
    private static VerifierResult Result(B10IntakeEvidence intake) => new(1, Identity(false), null, null, Commit, "2.16.0", "2.16.0",
        VerifierResultStatus.Incomplete, [], null, null, null, null, null, null, null, null, null, [], [], B10Intake: intake);
}
#pragma warning restore CA1707
