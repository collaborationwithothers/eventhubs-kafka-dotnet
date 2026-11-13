using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Publication;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707
public sealed class B10OneJobC0VerifyTests
{
    private static readonly int[] InvalidRetryChecks = [4, 12, 13, 14];
    private static readonly int[] SuccessfulPriorRetryChecks = [4];

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task Given_OneBoundOrdinaryProducer_When_Verified_Then_PublishesNonfinalIntakeAndUnsuccessfulReceipt(int repeat)
    {
        using HostEvidenceBuilder fixture = Fixture(repeat);
        var completion = new VerifierCompletionState();
        Assert.Equal(1, await fixture.Verify(completion).ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument json = JsonDocument.Parse(fixture.Output!);
        JsonElement result = json.RootElement;
        Assert.Equal("incomplete", result.GetProperty("status").GetString());
        Assert.False(result.GetProperty("classification_final").GetBoolean());
        Assert.True(result.GetProperty("b10_window_checks_complete").GetBoolean());
        Assert.Empty(result.GetProperty("failed_checks").EnumerateArray());
        Assert.Single(result.GetProperty("b10_intake").GetProperty("producers").EnumerateArray());
        Assert.Single(result.GetProperty("b10_producer_observations").EnumerateArray());
        Assert.False(result.TryGetProperty("ok", out _));
        completion.MarkReturned(fixture.Options, 1, CancellationToken.None, CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out _, out string name, out byte[] bytes));
        Assert.Equal($"b10-1-1/C0/r{repeat}/verify-completion.json", name);
        WorkerReceipt receipt = PhaseWireCodec.ReadReceipt(bytes);
        Assert.Equal(1, receipt.HandlerExitCode);
        Assert.Null(receipt.PublicationAcceptance);
        B10ProducerEvidence producer = Assert.Single(receipt.B10Intake!.Producers);
        Assert.Equal($"b10-1-1-c0-r{repeat}-loadgen", producer.JobName);
        Assert.Null(producer.PredecessorInstanceId);
        Assert.NotNull(producer.PublicationAcceptance);
        Assert.Equal(new RepeatChecks("pass", "pass", "pass"), receipt.RepeatChecks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Given_WrongRegistrationCount_When_Verified_Then_RejectsBeforeWindow(int count)
    {
        using HostEvidenceBuilder fixture = Fixture(2);
        string firstPath = PhasePaths.Registration("b10-1-1", "C0", 2, "pod-c5");
        if (count == 0) Assert.True(fixture.RemoveLedgerObject(firstPath));
        else fixture.AddLedgerObject(PhasePaths.Registration("b10-1-1", "C0", 2, "pod-extra"), fixture.LedgerObjects[firstPath]);
        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal("b10_registration_count_invalid", failure.Message);
        Assert.Null(fixture.Output);
    }

    [Fact]
    public async Task Given_LegacyPairStartLine_When_Verified_Then_RejectsStartLineWithOtherChecksPassing()
    {
        using HostEvidenceBuilder fixture = Fixture(2);
        JsonObject note = JsonNode.Parse(fixture.Note!)!.AsObject();
        note["expected_job_names"] = new JsonArray("b10-1-1-c0-r2-loadgen", "b10-1-1-c0-r2-loadgen2");
        fixture.Note = Encoding.UTF8.GetBytes(note.ToJsonString());
        fixture.PreservePublicationReferences = false;
        fixture.RebuildPublicationReferences();
        fixture.PreservePublicationReferences = true;
        AddEarlierRegistrations(fixture, 2);
        Assert.Equal(["start_line_invalid"], Accept(fixture).RejectionCodes);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument json = JsonDocument.Parse(fixture.Output!);
        Assert.Equal("invalid", json.RootElement.GetProperty("status").GetString());
        Assert.Equal([7], FailedChecks(json.RootElement));
        Assert.Contains(json.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "publication_unaccepted");
        Assert.Null(Assert.Single(json.RootElement.GetProperty("b10_intake").GetProperty("producers").EnumerateArray())
            .GetProperty("publication_acceptance").GetString());
    }

    [Fact]
    public async Task Given_RestartManifestOnOrdinaryProducer_When_Verified_Then_PublicationCannotAuthorizeWindow()
    {
        using HostEvidenceBuilder fixture = Fixture(2);
        JsonObject manifest = fixture.Json("manifest.json");
        JsonObject planned = manifest["inventory"]![0]!.DeepClone().AsObject();
        planned.Remove("instance_id");
        planned.Remove("attempt_id");
        planned.Remove("invoked_microseconds");
        planned["predecessor_attempt_id"] = "earlier-attempt";
        manifest["restart"] = new JsonObject { ["predecessor_instance_id"] = "earlier-pod", ["planned"] = new JsonArray(planned) };
        fixture.Set("manifest.json", manifest);
        Rebuild(fixture);
        Assert.Equal(["publication_restart_role_mismatch"], Accept(fixture).RejectionCodes);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument json = JsonDocument.Parse(fixture.Output!);
        Assert.Equal("invalid", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.GetProperty("b10_window_checks_complete").GetBoolean());
        Assert.Empty(FailedChecks(json.RootElement));
        Assert.Equal(JsonValueKind.Null, Assert.Single(json.RootElement.GetProperty("b10_intake").GetProperty("producers").EnumerateArray())
            .GetProperty("publication_acceptance").ValueKind);
    }

    [Theory]
    [InlineData(-192, "incomplete")]
    [InlineData(-1, "invalid")]
    [InlineData(0, "invalid")]
    public async Task Given_ApplicationRetry_When_Verified_Then_RequiresLatestEarlierPermittedTimeout(int error, string status)
    {
        using HostEvidenceBuilder fixture = Fixture(2);
        JsonObject first = fixture.Json("ledger.jsonl");
        JsonObject second = first.DeepClone().AsObject();
        second["attempt"]!["attempt_id"] = "retry-attempt";
        first["delivered"] = error == 0;
        first["error_code"] = error;
        first["error_name"] = error == 0 ? null : error == -192 ? "Local_MsgTimedOut" : "synthetic-error";
        first["persistence_status"] = error == 0 ? 2 : 1;
        fixture.Inputs["ledger.jsonl"] = Encoding.UTF8.GetBytes(first.ToJsonString() + "\n" + second.ToJsonString() + "\n");
        JsonObject manifest = fixture.Json("manifest.json");
        manifest["inventory"]!.AsArray().Add(second["attempt"]!.DeepClone());
        manifest["attempt_count"] = 2;
        manifest["planned_attempt_count"] = 2;
        if (error != 0)
        {
            manifest["guard_observations"]!.AsArray().Add("async_delivery_failure_observed");
            manifest["guard_observations"]!.AsArray().Add("possibly_persisted_observed");
        }
        fixture.Set("manifest.json", manifest);
        var consume = fixture.ConsumeRecord;
        fixture.ConsumeRecord = () =>
        {
            var record = consume!()!;
            record.Message.Headers.Remove("attempt-id");
            record.Message.Headers.Add("attempt-id", Encoding.UTF8.GetBytes("retry-attempt"));
            return record;
        };
        Rebuild(fixture);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument json = JsonDocument.Parse(fixture.Output!);
        Assert.Equal(status, json.RootElement.GetProperty("status").GetString());
        if (error == -192)
        {
            Assert.Empty(FailedChecks(json.RootElement));
            Assert.True(json.RootElement.GetProperty("b10_window_checks_complete").GetBoolean());
        }
        else
        {
            Assert.Contains(4, FailedChecks(json.RootElement));
            Assert.Contains(json.RootElement.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "b10_retry_requires_permitted_timeout");
            Assert.Equal(error == 0 ? SuccessfulPriorRetryChecks : InvalidRetryChecks, FailedChecks(json.RootElement));
        }
    }

    [Fact]
    public async Task Given_EarlierRepeatWithTwoRegistrations_When_Verified_Then_RejectsEarlierCount()
    {
        using HostEvidenceBuilder fixture = Fixture(3);
        fixture.AddLedgerObject(PhasePaths.Registration("b10-1-1", "C0", 2, "second-earlier"), []);
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal("b10_registration_count_invalid", error.Message);
    }

    internal static HostEvidenceBuilder Fixture(int repeat = 1)
    {
        var fixture = new HostEvidenceBuilder(scenarioId: "B10", scenarioSourceId: "A01", runId: "b10-1-1");
        fixture.ConfigureRepeat(repeat, "C0");
        fixture.PreservePublicationReferences = true;
        AddEarlierRegistrations(fixture, repeat);
        return fixture;
    }

    private static void Rebuild(HostEvidenceBuilder fixture)
    {
        fixture.RebuildPublicationReferences();
        AddEarlierRegistrations(fixture, fixture.Options.Repeat!.Value);
    }

    private static void AddEarlierRegistrations(HostEvidenceBuilder fixture, int repeat)
    {
        for (int earlier = 1; earlier < repeat; earlier++)
        {
            string instance = $"earlier-pod-{earlier}";
            var identity = new RunIdentity("B10", "C0", "b10-1-1", instance, instance, "earlier-client");
            var registration = new ProducerRegistration(1, identity, "C0", earlier,
                PhaseIdentity.JobName("b10-1-1", "C0", earlier), instance, instance + "-uid", fixture.Options.SourceCommit!);
            fixture.AddLedgerObject(PhasePaths.Registration("b10-1-1", "C0", earlier, instance), PhaseWireCodec.WriteRegistration(registration));
        }
    }

    private static int[] FailedChecks(JsonElement result) => result.GetProperty("failed_checks").EnumerateArray()
        .Select(check => check.GetInt32()).ToArray();

    private static PublicationReadOutcome Accept(HostEvidenceBuilder fixture)
    {
        VerifierHostOptions options = fixture.Options;
        int repeat = options.Repeat!.Value;
        string prefix = PhasePaths.ProducerPrefix(options.RunId!, "C0", repeat, options.InstanceId!);
        string registrationName = PhasePaths.Registration(options.RunId!, "C0", repeat, options.InstanceId!);
        ProducerRegistration registration = PhaseWireCodec.ReadRegistration(fixture.LedgerObjects[registrationName]);
        string completionName = PhasePaths.ProducerCompletion(options.RunId!, "C0", repeat, options.InstanceId!);
        byte[] completionBytes = fixture.LedgerObjects[completionName];
        ProducerCompletionReceipt completion = PhaseWireCodec.ReadProducerCompletion(completionBytes, "C0", repeat, registration);
        var limits = new InputLimits(64, 131072, 1048576, 32768, 100, 8192, 256);
        var budget = new InputBudget(limits);
        ArtifactIndex index = ArtifactIndexReader.Read(fixture.Inputs["index.json"], limits, budget).Value!;
        var manifest = ManifestReader.Read(fixture.Inputs["manifest.json"], limits, budget, 8192);
        Assert.True(manifest.IsSuccess);
        StartLineNote start = StartLineNoteCodec.Read(fixture.Note!, "start-line", limits, 2).Value!;
        byte[] inventoryBytes = fixture.VerifierObjects[options.BrokerInventoryOutputName!];
        BrokerInventory inventory = BrokerInventoryReader.Read(inventoryBytes, "inventory", start.Identity, options.SourceCommit!, limits).Value!;
        ArtifactReference Reference(string container, string name, byte[] bytes) => new(container, name, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        return PublicationAcceptance.Evaluate(new BlobListing(PublicationAcceptance.PublicationNames.Select(name => prefix + "publication/" + name).ToArray(), 1, true),
            prefix, fixture.Inputs, registration, completion, index, manifest.Value, start, inventory, options.SourceCommit!,
            Reference("verifier", options.StartLineInputName!, fixture.Note!), Reference("verifier", options.BrokerInventoryOutputName!, inventoryBytes),
            Reference("ledger", completionName, completionBytes), 131072, 1048576);
    }
}
#pragma warning restore CA1707
