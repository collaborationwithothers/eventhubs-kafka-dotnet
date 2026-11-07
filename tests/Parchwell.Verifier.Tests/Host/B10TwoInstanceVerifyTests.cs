using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Publication;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class B10TwoInstanceVerifyTests
{
    public static IEnumerable<object[]> VersionCaptureBoundaries()
    {
        foreach (int producer in new[] { 0, 1 })
            foreach (string field in new[] { "generator_version", "confluent_kafka", "librdkafka", "dotnet" })
                foreach (int length in new[] { 0, 255, 256, 257 }) yield return [producer, field, length];
    }

    [Theory]
    [MemberData(nameof(VersionCaptureBoundaries))]
    public async Task Given_CapturedVersionAtObservationBoundary_When_Verifying_Then_OnlyOversizeValueIsOmittedAndReceiptRemainsAvailable(
        int producer, string field, int length)
    {
        using var fixture = CreateSyntheticB10Intake();
        foreach (string instance in new[] { First, Second })
            ChangePayload(fixture, instance, "manifest.json", manifest =>
            {
                manifest["generator_version"] = "generator-a";
                manifest["runtime_versions"] = new JsonObject
                { ["confluent_kafka"] = "kafka-a", ["librdkafka"] = "native-a", ["dotnet"] = "dotnet-a" };
                if (instance == (producer == 0 ? First : Second))
                    if (field == "generator_version") manifest[field] = new string('v', length);
                    else manifest["runtime_versions"]![field] = new string('v', length);
            });
        var completion = new VerifierCompletionState();
        Assert.Equal(1, await fixture.Verify(completion).ExecuteAsync(TestContext.Current.CancellationToken));
        byte[] resultBytes = fixture.Output!;
        using JsonDocument document = JsonDocument.Parse(resultBytes);
        JsonElement result = document.RootElement;
        AssertStatus(result, "incomplete");
        Assert.Empty(FailedChecks(result));
        Assert.True(result.GetProperty("b10_window_checks_complete").GetBoolean());
        AssertPair(result, true, true);
        JsonElement pair = result.GetProperty("b10_producer_observations");
        Assert.Equal([First, Second], pair.EnumerateArray().Select(entry => entry.GetProperty("instance_id").GetString()));
        for (int index = 0; index < 2; index++)
        {
            JsonElement observation = pair[index];
            foreach (var (versionField, baseline) in new[] { ("generator_version", "generator-a"), ("confluent_kafka", "kafka-a"), ("librdkafka", "native-a"), ("dotnet", "dotnet-a") })
            {
                JsonElement target = versionField == "generator_version" ? observation : observation.GetProperty("runtime_versions");
                bool changed = index == producer && versionField == field;
                if (changed && length > 256) Assert.False(target.TryGetProperty(versionField, out _));
                else Assert.Equal(changed ? new string('v', length) : baseline, target.GetProperty(versionField).GetString());
                if (index == 0)
                {
                    JsonElement legacy = versionField == "generator_version" ? result : result.GetProperty("runtime_versions");
                    Assert.Equal(changed ? new string('v', length) : baseline, legacy.GetProperty(versionField).GetString());
                }
            }
            JsonElement broker = observation.GetProperty("broker_count");
            Assert.Equal(index == 0 ? First : Second, broker.GetProperty("client_id").GetString());
            Assert.Equal(1, broker.GetProperty("maximum").GetInt64());
            Assert.Equal(1, broker.GetProperty("first_post_drain").GetInt64());
            Assert.True(broker.GetProperty("lower_bound").GetBoolean());
        }
        AssertObservationReceipt(fixture, completion, resultBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task Given_AllCapturedVersionsOversizeForOneProducer_When_Verifying_Then_EmptyObservationProvenanceStillPublishesReceipt(int producer)
    {
        using var fixture = CreateSyntheticB10Intake();
        ChangePayload(fixture, producer == 0 ? First : Second, "manifest.json", manifest =>
        {
            manifest["generator_version"] = new string('v', 257);
            manifest["runtime_versions"] = new JsonObject
            { ["confluent_kafka"] = new string('v', 257), ["librdkafka"] = new string('v', 257), ["dotnet"] = new string('v', 257) };
        });
        var completion = new VerifierCompletionState();
        Assert.Equal(1, await fixture.Verify(completion).ExecuteAsync(TestContext.Current.CancellationToken));
        byte[] resultBytes = fixture.Output!;
        using JsonDocument document = JsonDocument.Parse(resultBytes);
        JsonElement result = document.RootElement;
        AssertStatus(result, "incomplete");
        Assert.Empty(FailedChecks(result));
        Assert.True(result.GetProperty("b10_window_checks_complete").GetBoolean());
        AssertPair(result, true, true);
        JsonElement observation = result.GetProperty("b10_producer_observations")[producer];
        Assert.False(observation.TryGetProperty("generator_version", out _));
        Assert.Empty(observation.GetProperty("runtime_versions").EnumerateObject());
        Assert.Equal(1, observation.GetProperty("broker_count").GetProperty("maximum").GetInt64());
        AssertObservationReceipt(fixture, completion, resultBytes);
    }

    private static void AssertObservationReceipt(HostEvidenceBuilder fixture, VerifierCompletionState completion, byte[] resultBytes)
    {
        Assert.True(fixture.Events.IndexOf("consumer-dispose") < fixture.Events.IndexOf("create"));
        completion.MarkReturned(fixture.Options, 1, CancellationToken.None, CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out _, out _, out byte[] receiptBytes));
        WorkerReceipt receipt = PhaseWireCodec.ReadReceipt(receiptBytes);
        Assert.True(receipt.CleanupCompleted);
        Assert.Equal(1, receipt.HandlerExitCode);
        Assert.All(receipt.B10Intake!.Producers, p => Assert.NotNull(p.PublicationAcceptance));
        ArtifactReference reference = Assert.Single(receipt.Artifacts);
        Assert.Equal(resultBytes.LongLength, reference.Length);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(resultBytes)), reference.Sha256);
    }

    [Theory]
    [InlineData("pair")]
    [InlineData("missing-dotnet")]
    [InlineData("unreadable-manifest")]
    public async Task Given_IndependentProducerManifests_When_Verifying_Then_RecordsEachInstancesOwnProvenanceAndBrokerCount(string change)
    {
        using var fixture = CreateSyntheticB10Intake();
        foreach (string instance in new[] { First, Second })
            ChangePayload(fixture, instance, "manifest.json", manifest =>
            {
                string suffix = instance == First ? "first" : "second";
                manifest["generator_version"] = "generator-" + suffix;
                manifest["runtime_versions"] = new JsonObject
                {
                    ["confluent_kafka"] = "kafka-" + suffix, ["librdkafka"] = "native-" + suffix,
                    ["dotnet"] = "dotnet-" + suffix, ["workload_generator"] = "extra-runtime-key"
                };
                if (instance == Second && change == "missing-dotnet") manifest["runtime_versions"]!.AsObject().Remove("dotnet");
                if (instance == Second && change == "unreadable-manifest") manifest.Remove("runtime_versions");
            });
        var completion = new VerifierCompletionState();
        Assert.Equal(1, await fixture.Verify(completion).ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument document = JsonDocument.Parse(fixture.Output!);
        JsonElement result = document.RootElement;
        AssertStatus(result, change == "unreadable-manifest" ? "invalid" : "incomplete");
        Assert.Equal("generator-first", result.GetProperty("generator_version").GetString());
        Assert.Equal("kafka-first", result.GetProperty("runtime_versions").GetProperty("confluent_kafka").GetString());
        JsonElement pair = result.GetProperty("b10_producer_observations");
        Assert.Equal([First, Second], pair.EnumerateArray().Select(x => x.GetProperty("instance_id").GetString()));
        for (int index = 0; index < 2; index++)
        {
            JsonElement observation = pair[index];
            if (index == 1 && change == "unreadable-manifest")
            {
                Assert.False(observation.TryGetProperty("generator_version", out _));
                Assert.False(observation.TryGetProperty("runtime_versions", out _));
                continue;
            }
            string suffix = index == 0 ? "first" : "second";
            Assert.Equal("generator-" + suffix, observation.GetProperty("generator_version").GetString());
            JsonElement runtimes = observation.GetProperty("runtime_versions");
            Assert.Equal("kafka-" + suffix, runtimes.GetProperty("confluent_kafka").GetString());
            Assert.Equal("native-" + suffix, runtimes.GetProperty("librdkafka").GetString());
            Assert.False(runtimes.TryGetProperty("workload_generator", out _));
            if (index == 1 && change == "missing-dotnet") Assert.False(runtimes.TryGetProperty("dotnet", out _));
            else Assert.Equal("dotnet-" + suffix, runtimes.GetProperty("dotnet").GetString());
            JsonElement count = observation.GetProperty("broker_count");
            Assert.Equal(index == 0 ? First : Second, count.GetProperty("client_id").GetString());
            Assert.Equal(1, count.GetProperty("maximum").GetInt64());
            Assert.Equal(1, count.GetProperty("first_post_drain").GetInt64());
            Assert.True(count.GetProperty("lower_bound").GetBoolean());
        }
        completion.MarkReturned(fixture.Options, 1, CancellationToken.None, CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out _, out _, out byte[] receiptBytes));
        using JsonDocument receipt = JsonDocument.Parse(receiptBytes);
        Assert.False(receipt.RootElement.TryGetProperty("b10_producer_observations", out _));
        Assert.Equal(result.GetProperty("b10_intake").GetRawText(), receipt.RootElement.GetProperty("b10_intake").GetRawText());
    }

    private static readonly int[] ForeignInstanceChecks = [3, 6];
    private static readonly int[] MissingInstanceChecks = [6];
    internal const string First = "pod-first";
    internal const string Second = "pod-second";
    private static readonly string[] Counts = ["ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"];
    private static readonly string[] PayloadNames = ["index.json", "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];

    [Theory]
    [InlineData("B10", true)]
    [InlineData("A01", false)]
    public async Task Given_ScenarioStartLine_When_HandlerPublishes_Then_OnlyB10CarriesBothJobsAndBothArtifactsUseFirstJobIdentity(string scenario, bool hasPair)
    {
        using var fixture = new HostEvidenceBuilder(scenarioId: scenario, scenarioSourceId: "A01");
        fixture.Options.Mode = "start-line";
        fixture.Options.JobName = $"{fixture.Options.RunId}-c0-r1-start-line";
        string firstJob = $"{fixture.Options.RunId}-c0-r1-loadgen";
        Assert.Equal(0, await fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal([fixture.Options.StartLineOutputName!, fixture.Options.BrokerInventoryOutputName!], fixture.CreateNames);
        using JsonDocument note = JsonDocument.Parse(fixture.CreatedObjects[fixture.Options.StartLineOutputName!]);
        using JsonDocument inventory = JsonDocument.Parse(fixture.CreatedObjects[fixture.Options.BrokerInventoryOutputName!]);
        Assert.Equal(hasPair, note.RootElement.TryGetProperty("expected_job_names", out JsonElement jobs));
        if (hasPair)
            Assert.Equal([firstJob, firstJob + "2"], jobs.EnumerateArray().Select(job => job.GetString()));
        foreach (JsonElement artifact in new[] { note.RootElement, inventory.RootElement })
        {
            JsonElement identity = artifact.GetProperty("identity");
            Assert.Equal(6, identity.EnumerateObject().Count());
            Assert.Equal(scenario, identity.GetProperty("scenario_id").GetString());
            Assert.Equal("C0", identity.GetProperty("configuration_id").GetString());
            Assert.Equal(fixture.Options.RunId, identity.GetProperty("run_id").GetString());
            Assert.Equal(firstJob, identity.GetProperty("instance_id").GetString());
            Assert.Equal(firstJob, identity.GetProperty("expected_instance_id").GetString());
            Assert.Equal(firstJob, identity.GetProperty("client_id").GetString());
        }
    }

    [Fact]
    public async Task Given_CoherentSyntheticB10Pair_When_Verifying_Then_BothSnapshotsAreAcceptedButResultAndReceiptRemainNonfinal()
    {
        using var fixture = CreateSyntheticB10Intake();
        var completion = new VerifierCompletionState();
        Assert.Equal(1, await fixture.Verify(completion).ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        JsonElement result = output.RootElement;
        AssertStatus(result, "incomplete");
        Assert.Empty(FailedChecks(result));
        Assert.Contains(result.GetProperty("diagnostics").EnumerateArray(), diagnostic => diagnostic.GetProperty("code").GetString() == "b10_intake_only");
        AssertPair(result, true, true);
        completion.MarkReturned(fixture.Options, 1, CancellationToken.None, CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out _, out _, out byte[] receiptBytes));
        WorkerReceipt receipt = PhaseWireCodec.ReadReceipt(receiptBytes);
        Assert.Equal(1, receipt.HandlerExitCode);
        Assert.Null(receipt.PublicationAcceptance);
        Assert.Equal([First, Second], receipt.B10Intake!.Producers.Select(producer => producer.Identity.InstanceId));
        Assert.All(receipt.B10Intake.Producers, producer => Assert.NotNull(producer.PublicationAcceptance));
        Assert.Equal(1, fixture.Reads.Count(name => name == fixture.Options.StartLineInputName));
        Assert.Equal(1, fixture.Reads.Count(name => name == fixture.Options.BrokerInventoryOutputName));
        Assert.Equal(1, fixture.Creates);
        Assert.True(fixture.Events.IndexOf("consumer-dispose") < fixture.Events.IndexOf("create"));
    }

    [Theory]
    [InlineData("descriptor-hash")]
    [InlineData("descriptor-length")]
    [InlineData("completion-identity")]
    [InlineData("completion-job")]
    [InlineData("manifest-identity")]
    [InlineData("restart-role")]
    public async Task Given_OnlySecondPublicationCorrupt_When_Verifying_Then_FirstAcceptanceCannotAuthorizeSecond(string mutation)
    {
        using var fixture = CreateSyntheticB10Intake();
        if (mutation.StartsWith("descriptor-", StringComparison.Ordinal))
            ChangePayload(fixture, Second, "index.json", index => index["artifacts"]![0]![mutation == "descriptor-hash" ? "sha256" : "length"] =
                mutation == "descriptor-hash" ? JsonValue.Create(new string('b', 64)) : JsonValue.Create(1), false);
        else if (mutation.StartsWith("completion-", StringComparison.Ordinal))
            ChangeBlob(fixture, CompletionPath(fixture, Second), receipt =>
            {
                if (mutation == "completion-job") receipt["job_name"] = fixture.Options.ExpectedProducerJobNames[0];
                else receipt["identity"]!["client_id"] = "foreign-client";
            });
        else ChangePayload(fixture, Second, "manifest.json", manifest =>
        {
            if (mutation == "manifest-identity") manifest["identity"]!["client_id"] = "foreign-client";
            else manifest.Remove("restart");
        });
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertStatus(output.RootElement, "invalid");
        AssertPair(output.RootElement, true, false);
        if (mutation.StartsWith("completion-", StringComparison.Ordinal)) Assert.Contains(6, FailedChecks(output.RootElement));
        if (mutation == "manifest-identity") Assert.Contains(3, FailedChecks(output.RootElement));
    }

    [Fact]
    public async Task Given_SecondPayloadMissingWithReadableIndex_When_Verifying_Then_RejectsOnlySecondSnapshotAndOwnsCheckOne()
    {
        using var fixture = CreateSyntheticB10Intake();
        SetPayload(fixture, Second, "ledger.jsonl", null);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertStatus(output.RootElement, "invalid");
        AssertPair(output.RootElement, true, false);
        AssertQualifiedFailure(output.RootElement, 1, Second);
    }

    [Fact]
    public async Task Given_EntireSecondPublicationUnavailable_When_Verifying_Then_ExecutionFailsWithoutInheritingFirstAcceptance()
    {
        using var fixture = CreateSyntheticB10Intake();
        foreach (string name in PayloadNames) SetPayload(fixture, Second, name, null);
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal("publication_index_unavailable_without_diagnostics", error.Message);
        Assert.Equal(0, fixture.Creates);
        Assert.Empty(fixture.Kafka.Events);
    }

    [Theory]
    [InlineData("manifest.json", false)]
    [InlineData("manifest.json", true)]
    [InlineData("ledger.jsonl", false)]
    [InlineData("ledger.jsonl", true)]
    public async Task Given_PredecessorPublicationMissing_When_DiagnosticCopyExistsOrNot_Then_PrerequisiteFailsBeforeKafkaAndOutput(string artifact, bool diagnosticCopy)
    {
        using var fixture = CreateSyntheticB10Intake();
        if (diagnosticCopy) fixture.DiagnosticInputs[artifact] = fixture.Inputs[artifact]!;
        SetPayload(fixture, First, artifact, null);
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal("b10_predecessor_evidence_missing", error.Message);
        Assert.Equal(0, fixture.Creates);
        Assert.Empty(fixture.Kafka.Events);
    }

    [Theory]
    [InlineData("first-restart", First)]
    [InlineData("second-no-restart", Second)]
    [InlineData("second-wrong-predecessor", Second)]
    public async Task Given_ManifestRestartRoleDisagreesWithRegistration_When_AcceptingAndVerifying_Then_ReportsExactRoleRejection(string mutation, string instance)
    {
        using var fixture = CreateSyntheticB10Intake();
        ChangePayload(fixture, instance, "manifest.json", manifest =>
        {
            if (mutation == "first-restart") manifest["restart"] = Restart(manifest, "other-predecessor");
            else if (mutation == "second-no-restart") manifest.Remove("restart");
            else manifest["restart"]!["predecessor_instance_id"] = "other-predecessor";
        });
        PublicationReadOutcome acceptance = Accept(fixture, instance);
        Assert.Equal(["publication_restart_role_mismatch"], acceptance.RejectionCodes);
        Assert.False(acceptance.Incomplete);
        Assert.Null(acceptance.AcceptedSnapshot);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertStatus(output.RootElement, "invalid");
        AssertPair(output.RootElement, instance != First, instance != Second);
    }

    public static IEnumerable<object[]> BookkeepingCases()
    {
        foreach (string instance in new[] { First, Second })
        {
            yield return [instance, "ledger-malformed", 1];
            yield return [instance, "manifest-malformed", 2];
            foreach (string artifact in new[] { "index.json", "manifest.json", "ledger.jsonl", "statistics.jsonl", "capabilities.json", "request-lifecycle.jsonl" })
                yield return [instance, "identity:" + artifact, 3];
            foreach (string mutation in new[] { "actual-count", "duplicate-inventory", "missing-row", "extra-row", "ledger-key", "ledger-sequence" })
                yield return [instance, mutation, 4];
            yield return [instance, "repeated-attempt", 5];
            foreach (string mutation in new[] { "missing-header", "duplicate-header", "unknown-attempt", "record-key", "record-sequence" })
                yield return [instance, mutation, 6];
        }
    }

    [Theory]
    [MemberData(nameof(BookkeepingCases))]
    public async Task Given_OneInstanceBookkeepingFault_When_Verifying_Then_OwningCheckAndProducerPathSurvive(string instance, string mutation, int check)
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        var records = new[] { Record(First, 0), Record(Second, 1) };
        if (mutation == "ledger-malformed") SetPayload(fixture, instance, "ledger.jsonl", Encoding.UTF8.GetBytes("{}\n"));
        else if (mutation == "manifest-malformed") SetPayload(fixture, instance, "manifest.json", Encoding.UTF8.GetBytes("{}"));
        else if (mutation.StartsWith("identity:", StringComparison.Ordinal))
        {
            string artifact = mutation["identity:".Length..];
            if (artifact == "request-lifecycle.jsonl")
                SetPayload(fixture, instance, artifact, Encoding.UTF8.GetBytes(new JsonObject
                {
                    ["schema_version"] = 1, ["identity"] = PayloadJson(fixture, instance, "index.json")["identity"]!.DeepClone(),
                    ["session_id"] = "synthetic-session", ["receipt_sequence"] = 1, ["received_at"] = "synthetic",
                    ["event_type"] = "native_error", ["error_code"] = 0, ["error_name"] = "NoError", ["reason"] = "synthetic",
                    ["is_error"] = false, ["is_fatal"] = false, ["provenance"] = "synthetic"
                }.ToJsonString() + "\n"));
            ChangeRows(fixture, instance, artifact, row => row["identity"]!["run_id"] = "foreign-run");
        }
        else if (mutation is "actual-count" or "duplicate-inventory")
            ChangePayload(fixture, instance, "manifest.json", manifest =>
            {
                if (mutation == "actual-count") manifest["attempt_count"] = 2;
                else
                {
                    manifest["inventory"]!.AsArray().Add(manifest["inventory"]![0]!.DeepClone());
                    manifest["attempt_count"] = 2;
                    // A duplicate inventory in the restarted producer would fail its structural parser first.
                    // Keep the restart planned slots distinct while duplicating only the bare attempt ID.
                    if (instance == Second)
                    {
                        manifest["inventory"]![1]!["sequence"] = 1;
                        manifest["inventory"]![1]!["event_type"] = "ocr-started";
                        manifest["inventory"]![1]!["ordinal"] = 1;
                        manifest["planned_attempt_count"] = 2;
                        manifest["restart"] = Restart(manifest, First);
                    }
                }
            });
        else if (mutation == "missing-row") SetPayload(fixture, instance, "ledger.jsonl", []);
        else if (mutation is "extra-row" or "repeated-attempt")
        {
            byte[] row = Payload(fixture, instance, "ledger.jsonl")!;
            SetPayload(fixture, instance, "ledger.jsonl", [.. row, .. row]);
        }
        else if (mutation is "ledger-key" or "ledger-sequence")
            ChangePayload(fixture, instance, "ledger.jsonl", row => row["attempt"]![mutation == "ledger-key" ? "key" : "sequence"] =
                mutation == "ledger-key" ? JsonValue.Create("foreign-key") : JsonValue.Create(1));
        else
        {
            ConsumeResult<byte[], byte[]> record = records[instance == First ? 0 : 1];
            if (mutation == "missing-header") record.Message.Headers.Remove("attempt-id");
            if (mutation == "duplicate-header") record.Message.Headers.Add("attempt-id", Encoding.UTF8.GetBytes("golden-attempt-000"));
            if (mutation == "unknown-attempt") ReplaceHeader(record, "attempt-id", "unknown-attempt");
            if (mutation == "record-key") record.Message.Key = Encoding.UTF8.GetBytes("foreign-key");
            if (mutation == "record-sequence") ReplaceHeader(record, "sequence", "1");
        }
        RefreshDescriptors(fixture, instance);
        SetRecords(fixture, records);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertStatus(output.RootElement, "invalid");
        AssertQualifiedFailure(output.RootElement, check, instance);
    }

    [Fact]
    public async Task Given_BothInstancesReuseBareAttemptId_When_DrainingCommonWindow_Then_RecordsJoinOnlyTheirOwnLedgersAndCheckFivePasses()
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        int assigned = 0;
        fixture.Kafka.Assign = _ => assigned++;
        // Assignment is still required by the fixture's ReadRecord gate.
        fixture.Kafka.ReadRecord = () => fixture.ConsumeRecord?.Invoke();
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertStatus(output.RootElement, "incomplete");
        Assert.Empty(FailedChecks(output.RootElement));
        AssertPair(output.RootElement, true, true);
        Assert.Equal(1, assigned);
    }

    [Theory]
    [InlineData("foreign-instance", true)]
    [InlineData("pod-earlier-repeat", true)]
    [InlineData(null, false)]
    public async Task Given_ForeignEarlierRepeatOrMissingInstanceRecord_When_Draining_Then_InterimIntakeRetainsItForRejection(string? instance, bool checkThree)
    {
        // Issue #365 owns the later exclusion of known earlier-repeat producer IDs.
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        ConsumeResult<byte[], byte[]> foreign = Record(instance ?? First, 2);
        if (instance is null) foreign.Message.Headers.Remove("instance-id");
        SetRecords(fixture, Record(First, 0), Record(Second, 1), foreign);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertStatus(output.RootElement, "invalid");
        Assert.Equal(checkThree ? ForeignInstanceChecks : MissingInstanceChecks, FailedChecks(output.RootElement));
        Assert.False(output.RootElement.TryGetProperty("phantom", out _));
    }

    [Fact]
    public async Task Given_IncompleteWindowAndSecondInvalidLedger_When_Verifying_Then_InvalidWinsAndCommonFailureIsRetainedOnce()
    {
        using var fixture = CreateSyntheticB10Intake();
        SetPayload(fixture, Second, "ledger.jsonl", Encoding.UTF8.GetBytes("{}\n"));
        RefreshDescriptors(fixture, Second);
        fixture.Kafka.Query = _ => throw new KafkaException(new Error(ErrorCode.Local_TimedOut));
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertStatus(output.RootElement, "invalid");
        Assert.Equal([1, 11], FailedChecks(output.RootElement));
        Assert.Single(output.RootElement.GetProperty("diagnostics").EnumerateArray(), diagnostic => diagnostic.GetProperty("check").GetInt32() == 11);
    }

    [Theory]
    [InlineData(First, 0)]
    [InlineData(First, 1)]
    [InlineData(Second, 0)]
    [InlineData(Second, 1)]
    public async Task Given_PermittedTimeoutRow_When_VerifyingB10_Then_GuardsPassButClassificationRemainsNonfinal(string instance, int persistenceStatus)
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        ChangePayload(fixture, instance, "ledger.jsonl", row =>
        {
            row["delivered"] = false;
            row["error_code"] = -192;
            row["error_name"] = "Local_MsgTimedOut";
            row["persistence_status"] = persistenceStatus;
        });
        if (instance == First && persistenceStatus == 0)
            ChangePayload(fixture, First, "manifest.json", manifest =>
            {
                JsonArray observations = manifest["guard_observations"]!.AsArray();
                JsonNode marker = observations.Single(value =>
                    value!.GetValue<string>() == "possibly_persisted_observed")!;
                observations.Remove(marker);
            });
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertStatus(output.RootElement, "incomplete");
        Assert.Empty(FailedChecks(output.RootElement));
        Assert.False(output.RootElement.GetProperty("classification_final").GetBoolean());
        AssertPair(output.RootElement, true, true);
    }

    [Theory]
    [InlineData(First, -193)]
    [InlineData(First, -191)]
    [InlineData(Second, -193)]
    [InlineData(Second, -191)]
    public async Task Given_NonTimeoutDeliveryError_When_VerifyingB10_Then_DeliveryAndPersistenceGuardsFail(string instance, int errorCode)
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        ChangePayload(fixture, instance, "ledger.jsonl", row =>
        {
            row["delivered"] = false;
            row["error_code"] = errorCode;
            row["persistence_status"] = 1;
        });
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertStatus(output.RootElement, "invalid");
        AssertQualifiedFailure(output.RootElement, 12, instance);
        AssertQualifiedFailure(output.RootElement, 13, instance);
    }

    [Theory]
    [InlineData("bytes", -1, false)]
    [InlineData("bytes", 0, true)]
    [InlineData("bytes", 1, true)]
    [InlineData("entries", -1, false)]
    [InlineData("entries", 0, true)]
    [InlineData("entries", 1, true)]
    [InlineData("diagnostic-bytes", -1, false)]
    [InlineData("diagnostic-bytes", 0, true)]
    [InlineData("diagnostic-bytes", 1, true)]
    [InlineData("diagnostic-entries", -1, false)]
    [InlineData("diagnostic-entries", 0, true)]
    [InlineData("diagnostic-entries", 1, true)]
    public async Task Given_SharedInputBudgetAtMeasuredPairBoundary_When_Verifying_Then_ItIsNeverResetBetweenProducers(string kind, int adjustment, bool succeeds)
    {
        using var fixture = CreateSyntheticB10Intake();
        bool diagnostics = kind.StartsWith("diagnostic-", StringComparison.Ordinal);
        if (kind.EndsWith("entries", StringComparison.Ordinal))
        {
            // Fixed synthetic streams make the shared parser charge exceed each document's
            // separate structural-entry bound, so only the intended aggregate can fail.
            fixture.Options.MaxRowsPerArtifact = 256;
            fixture.Options.MaxArtifactBytes = 524288;
            fixture.Options.MaxTotalInputBytes = 4194304;
            foreach (string instance in new[] { First, Second })
            {
                string template = Encoding.UTF8.GetString(Payload(fixture, instance, "statistics.jsonl")!).Split('\n')[0];
                var stream = new StringBuilder();
                for (int index = 0; index < 256; index++)
                {
                    JsonObject envelope = JsonNode.Parse(template)!.AsObject();
                    JsonObject native = JsonNode.Parse(envelope["raw_json"]!.GetValue<string>())!.AsObject();
                    native["ts"] = 51 + index;
                    native["age"] = 1 + index;
                    envelope["raw_json"] = native.ToJsonString();
                    stream.Append(envelope.ToJsonString()).Append('\n');
                }
                SetPayload(fixture, instance, "statistics.jsonl", Encoding.UTF8.GetBytes(stream.ToString()));
                RefreshDescriptors(fixture, instance);
            }
        }
        if (diagnostics)
        {
            foreach (string instance in new[] { First, Second })
            {
                foreach (string name in PayloadNames.Skip(1))
                {
                    byte[] bytes = Payload(fixture, instance, name)!;
                    if (instance == First) fixture.DiagnosticInputs[name] = bytes;
                    else fixture.AddLedgerObject(Prefix(fixture, instance) + "diagnostics/" + name, bytes);
                }
                ChangePayload(fixture, instance, "index.json", index => index["artifacts"]![0]!["sha256"] = new string('b', 64), false);
            }
        }
        long bytesTotal = fixture.ChargedInputBytes + fixture.DiagnosticInputs.Values.Sum(bytes => (long)bytes.Length);
        long entriesTotal = new[] { First, Second }.Sum(instance => CountEntries(fixture, instance));
        if (diagnostics) entriesTotal += new[] { First, Second }.Sum(instance => CountEntries(fixture, instance) - PayloadJson(fixture, instance, "index.json")["artifacts"]!.AsArray().Count);
        if (kind.EndsWith("bytes", StringComparison.Ordinal)) fixture.Options.MaxTotalInputBytes = bytesTotal + adjustment;
        else fixture.Options.MaxInputEntries = entriesTotal + adjustment;
        foreach (string instance in new[] { First, Second })
        {
            Assert.True(PayloadNames.Sum(name => (long)Payload(fixture, instance, name)!.Length) < bytesTotal + adjustment);
            Assert.True(CountEntries(fixture, instance) < entriesTotal + adjustment);
            if (kind.EndsWith("entries", StringComparison.Ordinal))
                foreach (string name in PayloadNames)
                {
                    string[] documents = name.EndsWith(".jsonl", StringComparison.Ordinal)
                        ? Encoding.UTF8.GetString(Payload(fixture, instance, name)!).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        : [Encoding.UTF8.GetString(Payload(fixture, instance, name)!)];
                    Assert.All(documents, document => Assert.True(StructuralEntries(JsonNode.Parse(document)) < fixture.Options.MaxInputEntries));
                }
        }
        Assert.All(fixture.LedgerObjects.Values, bytes => Assert.True(bytes.LongLength < fixture.Options.MaxArtifactBytes));
        if (succeeds)
        {
            Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
            using JsonDocument output = JsonDocument.Parse(fixture.Output!);
            AssertStatus(output.RootElement, diagnostics ? "invalid" : "incomplete");
            Assert.Equal(1, fixture.Creates);
        }
        else
        {
            await Assert.ThrowsAsync<InputLimitExceededException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, fixture.Creates);
        }
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, -1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public async Task Given_RetainedPairBuffersAtTheirOwnBoundary_When_Verifying_Then_OnlyBelowTotalFailsRetentionBeforeTheParserBudget(bool diagnostics, int adjustment)
    {
        using var fixture = CreateSyntheticB10Intake();
        var retained = new List<(string Path, byte[] Bytes)>();
        long firstProducerBytes = 0;
        long secondPublicationBytes = 0;
        long secondDiagnosticBytes = 0;
        long metadataBytes = fixture.VerifierObjects.Values.Sum(bytes => bytes.LongLength);
        foreach (string instance in new[] { First, Second })
        {
            metadataBytes += fixture.LedgerObjects[RegistrationPath(fixture, instance)].LongLength
                + fixture.LedgerObjects[CompletionPath(fixture, instance)].LongLength;
            if (diagnostics)
                ChangePayload(fixture, instance, "index.json", index => index["artifacts"]![0]!["sha256"] = new string('b', 64), false);
            foreach (string name in PayloadNames)
            {
                byte[] bytes = Payload(fixture, instance, name)!;
                retained.Add((PublicationPath(fixture, instance, name), bytes));
                if (instance == First) firstProducerBytes += bytes.LongLength;
                else secondPublicationBytes += bytes.LongLength;
            }
            if (!diagnostics) continue;
            foreach (string name in PayloadNames.Skip(1))
            {
                byte[] bytes = Payload(fixture, instance, name)!;
                string path = Prefix(fixture, instance) + "diagnostics/" + name;
                if (instance == First) fixture.DiagnosticInputs[name] = bytes;
                else fixture.AddLedgerObject(path, bytes);
                retained.Add((path, bytes));
                if (instance == First) firstProducerBytes += bytes.LongLength;
                else secondDiagnosticBytes += bytes.LongLength;
            }
        }
        long retainedTotal = retained.Sum(item => item.Bytes.LongLength);
        long limit = retainedTotal + adjustment;
        fixture.Options.MaxTotalInputBytes = limit;
        Assert.All(retained, item => Assert.True(item.Bytes.LongLength < fixture.Options.MaxArtifactBytes));
        Assert.True(firstProducerBytes < limit);
        Assert.True(secondPublicationBytes + secondDiagnosticBytes < limit);
        Assert.True(retainedTotal - retained[^1].Bytes.LongLength < limit);

        // Publications are parsed before their diagnostics and before the next producer.
        // The parser must permit that entire prefix to reach the final retained buffer.
        long parsedBeforeLastRead = metadataBytes + firstProducerBytes + (diagnostics ? secondPublicationBytes : 0);
        Assert.True(parsedBeforeLastRead < limit);
        // Success at the retention cap is unreachable: the shared parser also charges
        // registration, completion and start evidence, which retention does not include.
        Assert.True(metadataBytes + retainedTotal > limit);

        InputLimitExceededException error = await Assert.ThrowsAsync<InputLimitExceededException>(() =>
            fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(adjustment < 0
            ? "Combined publication buffers exceed the configured retained-input limit."
            : "The combined verifier inputs exceed the configured byte limit.", error.Message);
        string[] expectedReads = retained.Select(item => item.Path).ToArray();
        Assert.Equal(expectedReads, fixture.Reads.Where(path => expectedReads.Contains(path, StringComparer.Ordinal)));
        Assert.Equal(retained[^1].Path, fixture.Reads[^1]);
        Assert.Equal(0, fixture.Creates);
        Assert.Null(fixture.Output);
        Assert.Empty(fixture.Kafka.Events);
    }

    [Theory]
    [InlineData("records", -1, false)]
    [InlineData("records", 0, true)]
    [InlineData("records", 1, true)]
    [InlineData("bytes", -1, false)]
    [InlineData("bytes", 0, true)]
    [InlineData("bytes", 1, true)]
    public async Task Given_CommonConsumedBudgetAtPairBoundary_When_Verifying_Then_EachProducerFitsButCombinationUsesOneBudget(string kind, int adjustment, bool succeeds)
    {
        using var fixture = CreateSyntheticB10Intake(secondHasAttempt: true);
        ConsumeResult<byte[], byte[]>[] records = [Record(First, 0), Record(Second, 1)];
        long bytes = records.Sum(record => record.Message.Key.LongLength + record.Message.Value.LongLength +
            record.Message.Headers.Sum(header => (long)Encoding.UTF8.GetByteCount(header.Key) + header.GetValueBytes().LongLength));
        if (kind == "bytes") fixture.Options.MaxConsumedBytes = bytes + adjustment;
        else fixture.Options.MaxConsumedRecords = records.Length + adjustment;
        Assert.All(records, record => Assert.True(record.Message.Key.LongLength + record.Message.Value.LongLength +
            record.Message.Headers.Sum(header => (long)Encoding.UTF8.GetByteCount(header.Key) + header.GetValueBytes().LongLength) <= fixture.Options.MaxConsumedBytes));
        Assert.True(fixture.Options.MaxConsumedRecords >= 1);
        SetRecords(fixture, records);
        if (succeeds)
        {
            Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
            using JsonDocument output = JsonDocument.Parse(fixture.Output!);
            AssertStatus(output.RootElement, "incomplete");
            Assert.Empty(FailedChecks(output.RootElement));
        }
        else
        {
            await Assert.ThrowsAsync<InputLimitExceededException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, fixture.Creates);
        }
    }

    [Theory]
    [InlineData("input-cleanup")]
    [InlineData("admin-cleanup")]
    [InlineData("consumer-cleanup")]
    [InlineData("read-cancel")]
    [InlineData("drain-cancel")]
    [InlineData("output-failure")]
    public async Task Given_B10CleanupCancellationOrOutputFailure_When_Verifying_Then_NoEarlyPublicationOrRetryOccurs(string stage)
    {
        using var fixture = CreateSyntheticB10Intake();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (stage == "input-cleanup") fixture.InputDispose = () => throw new IOException("synthetic input cleanup");
        if (stage == "admin-cleanup") fixture.Kafka.AdminDispose = () => throw new IOException("synthetic admin cleanup");
        if (stage == "consumer-cleanup") fixture.Kafka.ConsumerDispose = () => throw new IOException("synthetic consumer cleanup");
        if (stage == "read-cancel") fixture.BeforeRead = (name, _) => { if (name == PublicationPath(fixture, Second, "ledger.jsonl")) cancellation.Cancel(); };
        if (stage == "drain-cancel") fixture.ConsumeRecord = () => { cancellation.Cancel(); return null; };
        if (stage == "output-failure") fixture.AfterCreate = _ => throw new IOException("synthetic uncertain output commit");
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Verify().ExecuteAsync(cancellation.Token));
        Assert.Equal(stage == "output-failure" ? 1 : 0, fixture.Creates);
        if (stage == "output-failure")
        {
            Assert.NotNull(fixture.Output);
            Assert.True(fixture.Events.IndexOf("consumer-dispose") < fixture.Events.IndexOf("create"));
            Assert.Single(fixture.CreateNames);
        }
        else Assert.Null(fixture.Output);
    }

    [Fact]
    public async Task Given_NonB10Singleton_When_Verifying_Then_CompleteValidCountsAndWireRemainUnchanged()
    {
        using var fixture = new HostEvidenceBuilder();
        Assert.Equal(0, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        Assert.Equal("complete_valid", output.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, output.RootElement.GetProperty("ok").GetInt64());
        foreach (string count in Counts.Skip(1)) Assert.Equal(0, output.RootElement.GetProperty(count).GetInt64());
        Assert.False(output.RootElement.TryGetProperty("b10_intake", out _));
    }

    [Fact]
    public async Task Given_NonB10TwoRegistrations_When_Verifying_Then_ExistingSingletonFailureRemains()
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.AddLedgerObject(PhasePaths.Registration(fixture.Options.RunId!, "C0", 1, "other-pod"), []);
        fixture.PreservePublicationReferences = true;
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal("producer_registration_not_singleton", error.Message);
        Assert.Equal(0, fixture.Creates);
    }

    internal static HostEvidenceBuilder CreateSyntheticB10Intake(bool secondHasAttempt = false, int repeat = 1)
    {
        var fixture = new HostEvidenceBuilder(instanceId: First, clientId: First, scenarioId: "B10", scenarioSourceId: "A01");
        fixture.ConfigureRepeat(repeat, "C0");
        fixture.Options.PodName = First;
        fixture.Options.PodUid = First;
        fixture.RebuildPublicationReferences();
        if (secondHasAttempt)
        {
            ChangePayload(fixture, First, "ledger.jsonl", row =>
            {
                row["delivered"] = false;
                row["error_code"] = -192;
                row["error_name"] = "Local_MsgTimedOut";
                row["persistence_status"] = 1;
            });
            ChangePayload(fixture, First, "manifest.json", firstManifest =>
            {
                firstManifest["guard_observations"]!.AsArray().Add("async_delivery_failure_observed");
                firstManifest["guard_observations"]!.AsArray().Add("possibly_persisted_observed");
            });
            fixture.RebuildPublicationReferences();
        }
        using var second = new HostEvidenceBuilder(instanceId: Second, clientId: Second, scenarioId: "B10", scenarioSourceId: "A01");
        second.ConfigureRepeat(repeat, "C0");
        second.Options.PodName = Second;
        second.Options.PodUid = Second;
        JsonObject manifest = second.Json("manifest.json");
        if (!secondHasAttempt)
        {
            manifest["planned_attempt_count"] = 0;
            manifest["attempt_count"] = 0;
            manifest["inventory"] = new JsonArray();
            second.Inputs["ledger.jsonl"] = [];
        }
        IReadOnlyList<string>? predecessorAttemptIds = secondHasAttempt
            ? PayloadJson(fixture, First, "manifest.json")["inventory"]!.AsArray()
                .Select(attempt => attempt!["attempt_id"]!.GetValue<string>()).ToArray()
            : null;
        manifest["restart"] = Restart(manifest, First, predecessorAttemptIds);
        second.Set("manifest.json", manifest);
        second.RebuildPublicationReferences();
        foreach ((string path, byte[] bytes) in second.LedgerObjects) fixture.AddLedgerObject(path, bytes);
        ChangeBlob(fixture, RegistrationPath(fixture, Second), registration =>
        {
            registration["job_name"] = fixture.Options.ExpectedProducerJobNames[1];
            registration["predecessor_instance_id"] = First;
        });
        ChangeBlob(fixture, CompletionPath(fixture, Second), completion => completion["job_name"] = fixture.Options.ExpectedProducerJobNames[1]);
        fixture.PreservePublicationReferences = true;
        SetRecords(fixture, secondHasAttempt ? [Record(First, 0), Record(Second, 1)] : [Record(First, 0)]);
        return fixture;
    }

    private static JsonObject Restart(JsonObject manifest, string predecessor, IReadOnlyList<string>? predecessorAttemptIds = null)
    {
        var planned = new JsonArray();
        foreach (JsonNode? item in manifest["inventory"]!.AsArray())
        {
            JsonObject attempt = item!.AsObject();
            var entry = new JsonObject { ["predecessor_attempt_id"] = predecessorAttemptIds?[planned.Count] ?? "predecessor-" + planned.Count };
            foreach (string field in new[] { "ordinal", "tenant_id", "document_id", "key", "sequence", "event_type", "planned_microseconds" }) entry[field] = attempt[field]!.DeepClone();
            planned.Add(entry);
        }
        return new JsonObject { ["predecessor_instance_id"] = predecessor, ["planned"] = planned };
    }

    private static string Prefix(HostEvidenceBuilder fixture, string instance) => PhasePaths.ProducerPrefix(fixture.Options.RunId!, fixture.Options.Phase!, fixture.Options.Repeat!.Value, instance);
    private static string PublicationPath(HostEvidenceBuilder fixture, string instance, string name) => Prefix(fixture, instance) + "publication/" + name;
    private static string RegistrationPath(HostEvidenceBuilder fixture, string instance) => Prefix(fixture, instance) + "registration.json";
    private static string CompletionPath(HostEvidenceBuilder fixture, string instance) => Prefix(fixture, instance) + "completion.json";
    private static byte[]? Payload(HostEvidenceBuilder fixture, string instance, string name) => instance == First ? fixture.Inputs[name] : fixture.LedgerObjects.GetValueOrDefault(PublicationPath(fixture, instance, name));
    internal static JsonObject PayloadJson(HostEvidenceBuilder fixture, string instance, string name) => JsonNode.Parse(Payload(fixture, instance, name)!)!.AsObject();
    internal static void SetPayload(HostEvidenceBuilder fixture, string instance, string name, byte[]? bytes)
    {
        if (instance == First) fixture.Inputs[name] = bytes;
        string path = PublicationPath(fixture, instance, name);
        if (bytes is null) fixture.RemoveLedgerObject(path);
        else fixture.AddLedgerObject(path, bytes);
    }
    internal static void ChangePayload(HostEvidenceBuilder fixture, string instance, string name, Action<JsonObject> change, bool refresh = true)
    {
        JsonObject value = PayloadJson(fixture, instance, name);
        change(value);
        SetPayload(fixture, instance, name, Encoding.UTF8.GetBytes(value.ToJsonString() + (name.EndsWith(".jsonl", StringComparison.Ordinal) ? "\n" : "")));
        if (refresh && name != "index.json") RefreshDescriptors(fixture, instance);
    }
    internal static void ChangeRows(HostEvidenceBuilder fixture, string instance, string name, Action<JsonObject> change)
    {
        if (!name.EndsWith(".jsonl", StringComparison.Ordinal)) { ChangePayload(fixture, instance, name, change); return; }
        string[] rows = Encoding.UTF8.GetString(Payload(fixture, instance, name)!).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < rows.Length; index++)
        {
            JsonObject row = JsonNode.Parse(rows[index])!.AsObject();
            change(row);
            rows[index] = row.ToJsonString();
        }
        SetPayload(fixture, instance, name, Encoding.UTF8.GetBytes(string.Join('\n', rows) + "\n"));
    }
    private static void ChangeBlob(HostEvidenceBuilder fixture, string path, Action<JsonObject> change)
    {
        JsonObject value = JsonNode.Parse(fixture.LedgerObjects[path])!.AsObject();
        change(value);
        fixture.AddLedgerObject(path, Encoding.UTF8.GetBytes(value.ToJsonString()));
    }
    internal static void RefreshDescriptors(HostEvidenceBuilder fixture, string instance)
    {
        ChangePayload(fixture, instance, "index.json", index =>
        {
            foreach (JsonNode? item in index["artifacts"]!.AsArray())
            {
                JsonObject descriptor = item!.AsObject();
                if (Payload(fixture, instance, descriptor["name"]!.GetValue<string>()) is not { } bytes) continue;
                descriptor["length"] = bytes.LongLength;
                descriptor["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes));
            }
        }, false);
    }
    private static ConsumeResult<byte[], byte[]> Record(string instance, long offset)
    {
        ConsumeResult<byte[], byte[]> record = HostEvidenceBuilder.Record();
        record.Offset = offset;
        ReplaceHeader(record, "instance-id", instance);
        return record;
    }
    private static void ReplaceHeader(ConsumeResult<byte[], byte[]> record, string key, string value)
    {
        record.Message.Headers.Remove(key);
        record.Message.Headers.Add(key, Encoding.UTF8.GetBytes(value));
    }
    private static void SetRecords(HostEvidenceBuilder fixture, params ConsumeResult<byte[], byte[]>[] records)
    {
        int position = 0;
        fixture.Kafka.Query = _ => new WatermarkOffsets(0, records.Length);
        fixture.Kafka.Position = _ => new Offset(position);
        fixture.ConsumeRecord = () => position < records.Length ? records[position++] : null;
    }
    internal static int[] FailedChecks(JsonElement result) => result.GetProperty("failed_checks").EnumerateArray().Select(check => check.GetInt32()).ToArray();
    internal static void AssertStatus(JsonElement result, string status)
    {
        Assert.Equal(status, result.GetProperty("status").GetString());
        foreach (string count in Counts) Assert.False(result.TryGetProperty(count, out _));
    }
    internal static void AssertPair(JsonElement result, bool firstAccepted, bool secondAccepted)
    {
        JsonElement intake = result.GetProperty("b10_intake");
        Assert.Equal("C0", intake.GetProperty("phase").GetString());
        Assert.Equal(1, intake.GetProperty("repeat").GetInt32());
        JsonElement[] producers = intake.GetProperty("producers").EnumerateArray().ToArray();
        Assert.Equal([First, Second], producers.Select(producer => producer.GetProperty("identity").GetProperty("instance_id").GetString()));
        Assert.Equal(JsonValueKind.Null, producers[0].GetProperty("predecessor_instance_id").ValueKind);
        Assert.Equal(First, producers[1].GetProperty("predecessor_instance_id").GetString());
        Assert.Equal(firstAccepted ? JsonValueKind.Object : JsonValueKind.Null, producers[0].GetProperty("publication_acceptance").ValueKind);
        Assert.Equal(secondAccepted ? JsonValueKind.Object : JsonValueKind.Null, producers[1].GetProperty("publication_acceptance").ValueKind);
    }
    internal static void AssertQualifiedFailure(JsonElement result, int check, string instance)
    {
        Assert.Contains(check, FailedChecks(result));
        Assert.Contains(result.GetProperty("diagnostics").EnumerateArray(), diagnostic =>
            diagnostic.GetProperty("check").GetInt32() == check && diagnostic.GetProperty("artifact").GetString()!.StartsWith("producer/" + instance + "/", StringComparison.Ordinal));
    }
    private static long CountEntries(HostEvidenceBuilder fixture, string instance)
    {
        JsonObject manifest = PayloadJson(fixture, instance, "manifest.json");
        long count = PayloadJson(fixture, instance, "index.json")["artifacts"]!.AsArray().Count;
        foreach (string array in new[] { "inventory", "diagnostics", "guard_observations" }) count += manifest[array]!.AsArray().Count;
        foreach (string map in new[] { "runtime_versions", "evidence_files" }) count += manifest[map]!.AsObject().Count;
        if (manifest["restart"] is JsonObject restart) count += restart["planned"]!.AsArray().Count;
        count += Encoding.UTF8.GetString(Payload(fixture, instance, "ledger.jsonl")!).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        foreach (string row in Encoding.UTF8.GetString(Payload(fixture, instance, "statistics.jsonl")!).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            JsonObject envelope = JsonNode.Parse(row)!.AsObject();
            count += 1 + envelope["diagnostics"]!.AsArray().Count + JsonNode.Parse(envelope["raw_json"]!.GetValue<string>())!["brokers"]!.AsObject().Count;
        }
        JsonObject capabilities = PayloadJson(fixture, instance, "capabilities.json");
        count += ArrayEntries(capabilities) + capabilities["resolved_configuration"]!["settings"]!.AsObject().Count;
        return count;
    }
    private static long StructuralEntries(JsonNode? value) => value switch
    {
        JsonArray array => array.Count + array.Sum(StructuralEntries),
        JsonObject map => map.Count + map.Sum(property => StructuralEntries(property.Value)),
        _ => 0
    };
    private static long ArrayEntries(JsonNode? value) => value switch
    {
        JsonArray array => array.Count + array.Sum(ArrayEntries),
        JsonObject map => map.Sum(property => ArrayEntries(property.Value)),
        _ => 0
    };
    private static PublicationReadOutcome Accept(HostEvidenceBuilder fixture, string instance)
    {
        InputLimits limits = new(64, 131072, 1048576, 32768, 100, 8192, 256);
        var budget = new InputBudget(limits);
        var raw = PayloadNames.ToDictionary(name => name, name => Payload(fixture, instance, name), StringComparer.Ordinal);
        ProducerRegistration registration = PhaseWireCodec.ReadRegistration(fixture.LedgerObjects[RegistrationPath(fixture, instance)]);
        ProducerCompletionReceipt completion = PhaseWireCodec.ReadProducerCompletion(fixture.LedgerObjects[CompletionPath(fixture, instance)], "C0", 1, registration);
        ArtifactIndex index = ArtifactIndexReader.Read(raw["index.json"], limits, budget).Value!;
        SealedRunManifest manifest = ManifestReader.Read(raw["manifest.json"], limits, budget, 8192).Value!;
        Assert.NotNull(manifest);
        StartLineNote start = StartLineNoteCodec.Read(fixture.Note!, "start-line", limits, 2).Value!;
        byte[] inventoryBytes = fixture.VerifierObjects[fixture.Options.BrokerInventoryOutputName!];
        BrokerInventory inventory = BrokerInventoryReader.Read(inventoryBytes, "broker-inventory", start.Identity, fixture.Options.SourceCommit!, limits).Value!;
        ArtifactReference Reference(string container, string name, byte[] bytes) => new(container, name, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        return PublicationAcceptance.Evaluate(new BlobListing(PayloadNames.Select(name => PublicationPath(fixture, instance, name)).ToArray(), 1, true),
            Prefix(fixture, instance), raw, registration, completion, index, manifest, start, inventory, fixture.Options.SourceCommit!,
            Reference("verifier", fixture.Options.StartLineInputName!, fixture.Note!), Reference("verifier", fixture.Options.BrokerInventoryOutputName!, inventoryBytes),
            Reference("ledger", CompletionPath(fixture, instance), fixture.LedgerObjects[CompletionPath(fixture, instance)]), 131072, 1048576);
    }
}
