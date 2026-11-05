using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Tests.Support;
using Xunit;
using static Parchwell.Verifier.Tests.Host.B10TwoInstanceVerifyTests;

namespace Parchwell.Verifier.Tests.Host;

// ADR-021 changes the two-producer judgement while publication, guard ownership and finality remain independent.
#pragma warning disable CA1707 // Repository test naming convention.
public sealed class B10RepeatVerifyTests
{
    private const string Broker = "sasl_ssl://broker-a:9093/1";

    [Fact]
    public void Given_RepairedSyntheticRetryPair_When_InspectingRawEvidence_Then_RestartNamesTheActualTimeoutAttempt()
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        JsonObject firstManifest = PayloadJson(fixture, First, "manifest.json");
        JsonObject firstRow = PayloadJson(fixture, First, "ledger.jsonl");
        string attemptId = firstManifest["inventory"]![0]!["attempt_id"]!.GetValue<string>();
        Assert.Equal("golden-attempt-000", attemptId);
        Assert.Equal(attemptId, firstRow["attempt"]!["attempt_id"]!.GetValue<string>());
        Assert.Equal(attemptId, PayloadJson(fixture, Second, "manifest.json")["restart"]!["planned"]![0]!["predecessor_attempt_id"]!.GetValue<string>());
        Assert.False(firstRow["delivered"]!.GetValue<bool>());
        Assert.Equal(-192, firstRow["error_code"]!.GetValue<int>());
        Assert.Equal("Local_MsgTimedOut", firstRow["error_name"]!.GetValue<string>());
        Assert.Equal(1, firstRow["persistence_status"]!.GetValue<int>());
        Assert.Equal("native_callback", firstRow["persistence_provenance"]!.GetValue<string>());
        string[] observations = firstManifest["guard_observations"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
        Assert.Contains("broker_lifecycle_inventory_unproven", observations);
        Assert.Contains("request_lifecycle_capture_unsupported", observations);
        Assert.Contains("async_delivery_failure_observed", observations);
        Assert.Contains("possibly_persisted_observed", observations);
    }

    [Theory]
    [InlineData("A01", 0)]
    [InlineData("A01", 1)]
    public async Task Given_IdenticalPermittedTimeoutOutsideExactB10_When_Verifying_Then_BaselineDeliveryGuardStillFails(string scenario, int persistenceStatus)
    {
        using var fixture = new HostEvidenceBuilder(scenarioId: scenario, scenarioSourceId: "A01");
        JsonObject row = fixture.Json("ledger.jsonl");
        SetTimeout(row, persistenceStatus);
        fixture.Inputs["ledger.jsonl"] = Encoding.UTF8.GetBytes(row.ToJsonString() + "\n");
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertStatus(output.RootElement, "invalid");
        Assert.Contains(12, FailedChecks(output.RootElement));
        Assert.Equal(persistenceStatus == 1, FailedChecks(output.RootElement).Contains(13));
        Assert.False(output.RootElement.TryGetProperty("classification_final", out _));
        Assert.False(output.RootElement.TryGetProperty("b10_intake", out _));
    }

    public static IEnumerable<object[]> NonPermittedPersistenceCases()
    {
        foreach (string instance in new[] { First, Second })
            foreach (string persistence in new[] { "missing", "null", "-1", "2" })
                yield return [instance, persistence];
    }

    [Theory]
    [MemberData(nameof(NonPermittedPersistenceCases))]
    public async Task Given_TimeoutWithUnpermittedPersistence_When_VerifyingB10_Then_DeliveryGuardCannotBeExempted(string instance, string persistence)
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        ChangePayload(fixture, instance, "ledger.jsonl", row =>
        {
            SetTimeout(row, 0);
            if (persistence == "missing") row.Remove("persistence_status");
            else row["persistence_status"] = JsonNode.Parse(persistence);
        });
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertNonfinal(output.RootElement, "invalid");
        if (persistence == "missing") AssertQualifiedFailure(output.RootElement, 1, instance);
        else
        {
            AssertQualifiedFailure(output.RootElement, 12, instance);
            if (persistence == "null") AssertQualifiedFailure(output.RootElement, 13, instance);
        }
        AssertPair(output.RootElement, true, true);
    }

    [Theory]
    [InlineData(First)]
    [InlineData(Second)]
    public async Task Given_PossiblyPersistedSuccessfulCallback_When_VerifyingB10_Then_PersistenceGuardStillFails(string instance)
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        ChangePayload(fixture, instance, "ledger.jsonl", row =>
        {
            row["delivered"] = true;
            row["error_code"] = 0;
            row["error_name"] = null;
            row["persistence_status"] = 1;
        });
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertNonfinal(output.RootElement, "invalid");
        AssertQualifiedFailure(output.RootElement, 13, instance);
        AssertPair(output.RootElement, true, true);
    }

    [Theory]
    [InlineData(First, "req_timeouts", 1)]
    [InlineData(First, "req_timeouts", 2)]
    [InlineData(First, "txretries", 1)]
    [InlineData(First, "txretries", 2)]
    [InlineData(Second, "req_timeouts", 1)]
    [InlineData(Second, "req_timeouts", 2)]
    [InlineData(Second, "txretries", 1)]
    [InlineData(Second, "txretries", 2)]
    public async Task Given_PermittedCounterAndExactMarkerInEitherInstance_When_VerifyingB10_Then_BothSnapshotsAndPairCoveragePassWithoutFinalCounts(string instance, string counter, int value)
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        ChangeReports(fixture, instance, native => native["brokers"]![Broker]![counter] = value);
        ChangePayload(fixture, instance, "manifest.json", manifest => manifest["guard_observations"]!.AsArray()
            .Add(FormattableString.Invariant($"broker={Broker};counter={counter};observed={value}")));
        var completion = new VerifierCompletionState();
        fixture.AfterCreate = writeToken =>
        {
            writeToken.ThrowIfCancellationRequested();
            Assert.True(fixture.Events.IndexOf("consumer-dispose") < fixture.Events.IndexOf("create"));
            Assert.True(fixture.Events.IndexOf("admin-dispose") < fixture.Events.IndexOf("create"));
            Assert.False(completion.TryGetBlobReceipt(out _, out _, out _));
        };
        Assert.Equal(1, await fixture.Verify(completion).ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertNonfinal(output.RootElement, "incomplete");
        Assert.True(output.RootElement.GetProperty("b10_window_checks_complete").GetBoolean());
        Assert.Empty(FailedChecks(output.RootElement));
        AssertPair(output.RootElement, true, true);
        Assert.False(completion.TryGetBlobReceipt(out _, out _, out _));
        completion.MarkReturned(fixture.Options, 1, CancellationToken.None, CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out string container, out string name, out byte[] bytes));
        Assert.Equal("verifier", container);
        Assert.Equal(PhasePaths.VerifyCompletion(fixture.Options.RunId!, "C0", 1), name);
        WorkerReceipt receipt = PhaseWireCodec.ReadReceipt(bytes);
        Assert.Equal(new RepeatChecks("pass", "pass", "pass"), receipt.RepeatChecks);
        Assert.Equal(1, receipt.HandlerExitCode);
        Assert.Null(receipt.PublicationAcceptance);
        Assert.Equal([First, Second], receipt.B10Intake!.Producers.Select(producer => producer.Identity.InstanceId));
        Assert.All(receipt.B10Intake.Producers, producer => Assert.NotNull(producer.PublicationAcceptance));
        Assert.Single(fixture.CreateNames);
        Assert.Equal(fixture.Options.ResultOutputName, fixture.CreateNames[0]);
    }

    [Theory]
    [InlineData(First, "txerrs", -1)]
    [InlineData(First, "txerrs", 1)]
    [InlineData(First, "rxerrs", -1)]
    [InlineData(First, "rxerrs", 1)]
    [InlineData(Second, "txerrs", -1)]
    [InlineData(Second, "txerrs", 1)]
    [InlineData(Second, "rxerrs", -1)]
    [InlineData(Second, "rxerrs", 1)]
    public async Task Given_ForbiddenNonzeroCounterInEitherInstance_When_VerifyingB10_Then_CheckFifteenStillFails(string instance, string counter, int value)
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        ChangeReports(fixture, instance, native => native["brokers"]![Broker]![counter] = value);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertNonfinal(output.RootElement, "invalid");
        AssertQualifiedFailure(output.RootElement, 15, instance);
        AssertPair(output.RootElement, true, true);
    }

    [Theory]
    [InlineData(First, false)]
    [InlineData(First, true)]
    [InlineData(Second, false)]
    [InlineData(Second, true)]
    public async Task Given_NativeErrorCallbackAlongsidePermittedTimeout_When_VerifyingB10_Then_OnlyErrorLevelCallbackFailsCheckSixteen(string instance, bool isError)
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        SetNativeError(fixture, instance, isError);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertNonfinal(output.RootElement, isError ? "invalid" : "incomplete");
        if (isError) AssertQualifiedFailure(output.RootElement, 16, instance);
        else Assert.Empty(FailedChecks(output.RootElement));
        AssertPair(output.RootElement, true, true);
    }

    [Theory]
    [InlineData(First)]
    [InlineData(Second)]
    public async Task Given_IncompleteGuardObservations_When_VerifyingB10_Then_PermittedTimeoutCannotAuthorizeMissingGuardEvidence(string instance)
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        ChangePayload(fixture, instance, "capabilities.json", capabilities => capabilities["guard_observations_complete"] = false);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertNonfinal(output.RootElement, "invalid");
        AssertQualifiedFailure(output.RootElement, 14, instance);
    }

    [Fact]
    public async Task Given_ApplicationRetryAfterSuccessfulPredecessor_When_VerifyingB10_Then_NewRetryAssessmentMakesThePairInvalid()
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        ChangePayload(fixture, First, "ledger.jsonl", row =>
        {
            row["delivered"] = true;
            row["error_code"] = 0;
            row["error_name"] = null;
            row["persistence_status"] = 2;
        });
        ChangePayload(fixture, First, "manifest.json", manifest =>
        {
            JsonArray observations = manifest["guard_observations"]!.AsArray();
            foreach (JsonNode? marker in observations.ToArray())
                if (marker!.GetValue<string>() is "async_delivery_failure_observed" or "possibly_persisted_observed") observations.Remove(marker);
        });
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertNonfinal(output.RootElement, "invalid");
        Assert.Equal([4], FailedChecks(output.RootElement));
        AssertQualifiedCode(output.RootElement, "b10_retry_requires_permitted_timeout", Second, "ledger.jsonl");
        AssertQualifiedCode(output.RootElement, "restart_plan_mismatch", Second, "manifest.json");
        AssertPair(output.RootElement, true, true);
    }

    [Theory]
    [InlineData("predecessor-id", false)]
    [InlineData("planned-time", false)]
    [InlineData("predecessor-id", true)]
    public async Task Given_IndependentlyReadableRestartMismatch_When_VerifyingB10_Then_CheckFourAndIndependentNativeFailureSurvive(string mutation, bool nativeError)
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        ChangePayload(fixture, Second, "manifest.json", manifest =>
        {
            if (mutation == "predecessor-id") manifest["restart"]!["planned"]![0]!["predecessor_attempt_id"] = "synthetic-other-attempt";
            else
            {
                manifest["restart"]!["planned"]![0]!["planned_microseconds"] = 101;
                manifest["inventory"]![0]!["planned_microseconds"] = 101;
            }
        });
        if (mutation == "planned-time") ChangePayload(fixture, Second, "ledger.jsonl", row => row["attempt"]!["planned_microseconds"] = 101);
        if (nativeError) SetNativeError(fixture, First, true);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertNonfinal(output.RootElement, "invalid");
        AssertQualifiedFailure(output.RootElement, 4, Second);
        AssertQualifiedCode(output.RootElement, "restart_plan_mismatch", Second, "manifest.json");
        if (nativeError) AssertQualifiedFailure(output.RootElement, 16, First);
        else Assert.Equal([4], FailedChecks(output.RootElement));
        AssertPair(output.RootElement, true, true);
    }

    [Theory]
    [InlineData(First, "fail")]
    [InlineData(Second, "fail")]
    [InlineData(First, "unavailable")]
    [InlineData(Second, "unavailable")]
    public async Task Given_OnlyOneProducerLacksPassingBrokerCoverage_When_VerifyingB10_Then_OtherProducerCannotSupplyItsReceiptCoverage(string instance, string coverage)
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        ChangePayload(fixture, instance, "capabilities.json", capabilities =>
        {
            if (coverage == "fail") capabilities["statistics_completion"]!["missing_brokers"]!.AsArray().Add(Broker);
            else capabilities.Remove("statistics_completion");
        });
        var completion = new VerifierCompletionState();
        Assert.Equal(1, await fixture.Verify(completion).ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertNonfinal(output.RootElement, "invalid");
        if (coverage == "fail") AssertQualifiedFailure(output.RootElement, 24, instance);
        else Assert.DoesNotContain(24, FailedChecks(output.RootElement));
        AssertPair(output.RootElement, true, true);
        completion.MarkReturned(fixture.Options, 1, CancellationToken.None, CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out _, out _, out byte[] bytes));
        WorkerReceipt receipt = PhaseWireCodec.ReadReceipt(bytes);
        Assert.Equal(new RepeatChecks("pass", "pass", coverage), receipt.RepeatChecks);
        Assert.Equal(1, receipt.HandlerExitCode);
        Assert.Null(receipt.PublicationAcceptance);
    }

    private static void SetTimeout(JsonObject row, int persistenceStatus)
    {
        row["delivered"] = false;
        row["error_code"] = -192;
        row["error_name"] = "Local_MsgTimedOut";
        row["persistence_status"] = persistenceStatus;
    }

    private static void SetNativeError(HostEvidenceBuilder fixture, string instance, bool isError)
    {
        var error = new JsonObject
        {
            ["schema_version"] = 1, ["identity"] = PayloadJson(fixture, instance, "index.json")["identity"]!.DeepClone(),
            ["session_id"] = "synthetic-session", ["receipt_sequence"] = 1, ["received_at"] = "synthetic",
            ["event_type"] = "native_error", ["error_code"] = -184, ["error_name"] = "synthetic-error",
            ["reason"] = "synthetic error", ["is_error"] = isError, ["is_fatal"] = false, ["provenance"] = "synthetic"
        };
        SetPayload(fixture, instance, "request-lifecycle.jsonl", Encoding.UTF8.GetBytes(error.ToJsonString() + "\n"));
        RefreshDescriptors(fixture, instance);
    }

    private static void ChangeReports(HostEvidenceBuilder fixture, string instance, Action<JsonObject> change)
    {
        ChangeRows(fixture, instance, "statistics.jsonl", envelope =>
        {
            JsonObject native = JsonNode.Parse(envelope["raw_json"]!.GetValue<string>())!.AsObject();
            change(native);
            envelope["raw_json"] = native.ToJsonString();
        });
        RefreshDescriptors(fixture, instance);
    }

    private static void AssertNonfinal(JsonElement result, string status)
    {
        AssertStatus(result, status);
        Assert.False(result.GetProperty("classification_final").GetBoolean());
        if (status == "invalid") Assert.False(result.GetProperty("b10_window_checks_complete").GetBoolean());
    }

    private static void AssertQualifiedCode(JsonElement result, string code, string instance, string artifact)
    {
        Assert.Contains(result.GetProperty("diagnostics").EnumerateArray(), diagnostic =>
            diagnostic.GetProperty("check").GetInt32() == 4 && diagnostic.GetProperty("code").GetString() == code &&
            diagnostic.GetProperty("artifact").GetString() == "producer/" + instance + "/" + artifact);
    }
}
#pragma warning restore CA1707
