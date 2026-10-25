using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class VerifyRunHandlerTests
{
    public static IEnumerable<object[]> InvalidIndexCases =>
    [
        ["empty"], ["duplicate"], ["extra"], ["nested"], ["reordered"], ["negative-length"], ["unchecked-hash"], ["wrong-hash"]
    ];

    [Fact]
    public async Task Given_CoherentRawSingleDeliveredAttempt_When_Verifying_Then_PublishesSixExactCountsAfterAllCleanupAndReturnsZero()
    {
        using var fixture = new HostEvidenceBuilder();
        Assert.Equal(0, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        fixture.Events.Add("return");
        Assert.Equal(["input-dispose", "input-dispose", "admin-dispose", "consumer-dispose", "create", "return"], fixture.Events);
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        JsonElement result = output.RootElement;
        Assert.Equal("complete_valid", result.GetProperty("status").GetString());
        Assert.Equal(1, result.GetProperty("ok").GetInt64());
        foreach (string count in new[] { "loss", "phantom", "reported_failure", "duplicate", "reorder" }) Assert.Equal(0, result.GetProperty(count).GetInt64());
        Assert.Equal(1, fixture.Creates);
        Assert.Equal("synthetic-c5/C0/r1/result.json", fixture.OutputName);
        Assert.Equal("synthetic-producer", result.GetProperty("generator_version").GetString());
        Assert.Equal("synthetic-native", result.GetProperty("runtime_versions").GetProperty("Confluent.Kafka").GetString());
        Assert.Equal(typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(), result.GetProperty("verifier_confluent_kafka_version").GetString());
        Assert.Equal(Library.VersionString, result.GetProperty("verifier_librdkafka_version").GetString());
        AssertUsableNoSampleProbe(result);
        AssertRecordedWindows(result, null);
    }

    [Fact]
    public async Task Given_FailedStartLineReceipt_When_Verifying_Then_DoesNotAcceptPublicationOrCounts()
    {
        using var fixture = new HostEvidenceBuilder { StartLineHandlerExitCode = 1 };

        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));

        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        Assert.Equal("incomplete", output.RootElement.GetProperty("status").GetString());
        Assert.False(output.RootElement.TryGetProperty("ok", out _));
    }

    [Fact]
    public async Task Given_ExtraPublicationAndMissingProducerCompletion_When_Verifying_Then_KnownIntegrityFailureWins()
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.PreservePublicationReferences = true;
        fixture.AddLedgerObject("synthetic-c5/C0/r1/producer/pod-c5/publication/extra.bin", []);
        Assert.True(fixture.RemoveLedgerObject("synthetic-c5/C0/r1/producer/pod-c5/completion.json"));

        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));

        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        Assert.Equal("invalid", output.RootElement.GetProperty("status").GetString());
        Assert.False(output.RootElement.TryGetProperty("ok", out _));
        Assert.Contains(output.RootElement.GetProperty("diagnostics").EnumerateArray(), diagnostic =>
            diagnostic.GetProperty("code").GetString() == "publication_unaccepted");
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public async Task Given_EachIndependentLateWriteFailureAndQualifyingThrottle_When_Verifying_Then_PublishesUsableProbeWithoutAuthoritativeCounts(int check)
    {
        using var fixture = new HostEvidenceBuilder();
        SetNonemptyWindows(fixture, 20001);
        if (check is 12 or 13)
        {
            JsonObject ledger = fixture.Json("ledger.jsonl");
            if (check == 12) ledger["delivered"] = false;
            else ledger["persistence_status"] = 1;
            fixture.Inputs["ledger.jsonl"] = Encoding.UTF8.GetBytes(ledger.ToJsonString() + "\n");
        }
        if (check == 14)
        {
            JsonObject capabilities = fixture.Json("capabilities.json");
            capabilities["guard_observations_complete"] = false;
            fixture.Set("capabilities.json", capabilities);
        }
        if (check == 15) ChangeNativeReports(fixture, (native, _) => native["brokers"]!["sasl_ssl://broker-a:9093/1"]!["req_timeouts"] = 1);
        if (check == 16)
        {
            var error = new JsonObject
            {
                ["schema_version"] = 1, ["identity"] = fixture.Json("index.json")["identity"]!.DeepClone(),
                ["session_id"] = "synthetic-session", ["receipt_sequence"] = 1, ["received_at"] = "synthetic",
                ["event_type"] = "native_error", ["error_code"] = -184, ["error_name"] = "synthetic-error",
                ["reason"] = "synthetic error", ["is_error"] = true, ["is_fatal"] = false, ["provenance"] = "synthetic"
            };
            fixture.Inputs["request-lifecycle.jsonl"] = Encoding.UTF8.GetBytes(error.ToJsonString() + "\n");
        }

        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        JsonElement result = output.RootElement;
        AssertFailure(result, "invalid", check);
        Assert.Equal([check], FailedChecks(result));
        AssertUsableProbe(result, 20001, true);
        AssertRecordedWindows(result, 20001, check == 15 ? 1 : 0);
        Assert.Equal(1, fixture.Creates);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(19999, false)]
    [InlineData(20000, false)]
    [InlineData(20001, true)]
    public async Task Given_NonemptyThrottleAtZeroOrThresholdBoundary_When_Verifying_Then_PublishesExactUsabilityAndQualification(long maximum, bool qualifying)
    {
        using var fixture = new HostEvidenceBuilder();
        SetNonemptyWindows(fixture, maximum);
        Assert.Equal(0, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        JsonElement result = output.RootElement;
        Assert.Equal("complete_valid", result.GetProperty("status").GetString());
        Assert.Empty(FailedChecks(result));
        Assert.Equal(1, result.GetProperty("ok").GetInt64());
        foreach (string count in new[] { "loss", "phantom", "reported_failure", "duplicate", "reorder" }) Assert.Equal(0, result.GetProperty(count).GetInt64());
        AssertUsableProbe(result, maximum, qualifying);
        AssertRecordedWindows(result, maximum);
        Assert.Equal(1, fixture.Creates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_MissingOrMalformedLedgerAndCompleteStatistics_When_Verifying_Then_ReportsTheOwningFailure(bool malformed)
    {
        using var fixture = new HostEvidenceBuilder();
        SetNonemptyWindows(fixture, 20001);
        fixture.Inputs["ledger.jsonl"] = malformed ? Encoding.UTF8.GetBytes("{}\n") : null;
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        JsonElement result = output.RootElement;
        AssertFailure(result, "invalid", 1);
        AssertUnusableProbe(result);
        AssertRecordedWindows(result, 20001, includeCodes: false);
        Assert.Equal(1, fixture.Creates);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(23)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(26)]
    public async Task Given_EachStatisticsCompletenessFailure_When_Verifying_Then_PublishesCodesWithoutPartialWindowsOrProbeReferences(int check)
    {
        using var fixture = new HostEvidenceBuilder();
        SetNonemptyWindows(fixture, 20001);
        if (check is 17 or 19 or 24)
        {
            JsonObject capabilities = fixture.Json("capabilities.json");
            if (check == 17) capabilities["is_faulted"] = true;
            if (check == 19) capabilities["statistics_completion"]!["is_complete"] = false;
            if (check == 24) capabilities["statistics_completion"]!["missing_brokers"] = new JsonArray("sasl_ssl://broker-a:9093/1");
            fixture.Set("capabilities.json", capabilities);
        }
        if (check == 18) fixture.Inputs["statistics.jsonl"] = [];
        if (check is >= 20 and <= 23 or 25 or 26) ChangeNativeReports(fixture, (native, index) =>
        {
            if (check == 20 && index == 1) native["ts"] = "malformed-native-time";
            if (check == 21 && index == 1) native["brokers"]!["sasl_ssl://broker-a:9093/1"]!.AsObject().Remove("nodeid");
            if (check == 22 && index == 1) { native["ts"] = 90; native["age"] = 40; }
            if (check == 23 && index == 0) native["brokers"]!["sasl_ssl://broker-a:9093/1"]!["disconnects"] = 1;
            if (check == 25 && index == 1) native["brokers"] = new JsonObject();
            if (check == 26 && index == 0) native["brokers"]!["sasl_ssl://broker-a:9093/1"]!["source"] = "internal";
        });

        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        JsonElement result = output.RootElement;
        AssertFailure(result, "invalid", check);
        AssertUnusableProbe(result);
        JsonElement evidence = result.GetProperty("a01_recorded_evidence");
        Assert.Empty(evidence.GetProperty("delivery_error_codes").EnumerateArray());
        AssertPropertyNames(evidence, "delivery_error_codes");
        Assert.Equal(1, fixture.Creates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_MissingOrMalformedIndex_When_Verifying_Then_ExecutionFailsWithoutAnyOutputOrNumberedAttribution(bool malformed)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Inputs["index.json"] = malformed ? Encoding.UTF8.GetBytes("{}") : null;
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Contains("index", error.Message, StringComparison.Ordinal);
        Assert.Equal(ExpectedReads(fixture), fixture.Reads);
        Assert.Empty(fixture.Events);
        Assert.Equal(0, fixture.Creates);
        Assert.Null(fixture.Output);
    }

    [Fact]
    public async Task Given_MissingPublicationIndexAndFailedEvidencePayloads_When_Verifying_Then_PublishesDiagnosticResultWithoutAcceptedCounts()
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Inputs["index.json"] = null;
        foreach (string name in new[] { "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json" })
            fixture.DiagnosticInputs[name] = fixture.Inputs[name]!;

        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));

        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        JsonElement result = output.RootElement;
        Assert.Equal("incomplete", result.GetProperty("status").GetString());
        Assert.False(result.TryGetProperty("ok", out _));
        Assert.Empty(FailedChecks(result));
        Assert.Contains(fixture.Reads, name => name.EndsWith("/diagnostics/ledger.jsonl", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Reads, name => name.EndsWith("/diagnostics/index.json", StringComparison.Ordinal));
        Assert.Equal(1, fixture.Creates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_MissingOrMalformedManifest_When_Verifying_Then_CheckTwoInvalidPublishesWithoutProducerProvenanceOrCounts(bool malformed)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Inputs["manifest.json"] = malformed ? Encoding.UTF8.GetBytes("{}") : null;
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertFailure(output.RootElement, "invalid", 2);
        Assert.False(output.RootElement.TryGetProperty("generator_version", out _));
        Assert.False(output.RootElement.TryGetProperty("runtime_versions", out _));
        Assert.Equal(1, fixture.Creates);
    }

    [Fact]
    public async Task Given_WrongConsumedInstance_When_Verifying_Then_InvalidResultPreservesProducerProvenanceAndOmitsEveryCount()
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.ConsumeRecord = () =>
        {
            fixture.Kafka.Position = _ => new Offset(1);
            ConsumeResult<byte[], byte[]> record = HostEvidenceBuilder.Record();
            record.Message.Headers.Remove("instance-id");
            record.Message.Headers.Add("instance-id", Encoding.UTF8.GetBytes("foreign-instance"));
            return record;
        };
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertFailure(output.RootElement, "invalid", 3);
        Assert.Equal("synthetic-producer", output.RootElement.GetProperty("generator_version").GetString());
        Assert.Equal("synthetic-native", output.RootElement.GetProperty("runtime_versions").GetProperty("Confluent.Kafka").GetString());
        Assert.Equal(1, fixture.Creates);
    }

    [Theory]
    [InlineData("token")]
    [InlineData("metadata")]
    [InlineData("watermark")]
    [InlineData("empty-metadata")]
    public async Task Given_QueryExpiryOrEmptyMetadata_When_Verifying_Then_PublishesIncompleteElevenWithoutInventedBoundariesOrDurations(string phase)
    {
        using var fixture = new HostEvidenceBuilder();
        ExpireQuery(fixture, phase, 1000);
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertFailure(output.RootElement, "incomplete", 11);
        Assert.DoesNotContain(10, FailedChecks(output.RootElement));
        Assert.False(output.RootElement.TryGetProperty("end_line", out _));
        AssertNoDurations(output.RootElement);
        Assert.Equal(1, fixture.Creates);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(1000)]
    [InlineData(1001)]
    public async Task Given_QueryFailureCompetingWithSafetyAndIndependentInvalidInput_When_Verifying_Then_InvalidOutranksIncompleteAndElevenOwnsQueryFailure(int elapsed)
    {
        using var fixture = new HostEvidenceBuilder();
        JsonObject manifest = fixture.Json("manifest.json");
        manifest["identity"]!["instance_id"] = "foreign-instance";
        fixture.Set("manifest.json", manifest);
        fixture.Kafka.Query = _ => { fixture.Kafka.Clock.Advance(elapsed); throw new KafkaException(new Error(ErrorCode.Local_TimedOut)); };
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertFailure(output.RootElement, "invalid", 3);
        Assert.Contains(11, FailedChecks(output.RootElement));
        Assert.DoesNotContain(10, FailedChecks(output.RootElement));
        AssertNoDurations(output.RootElement);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_SafetyStopWithOptionalIndependentInvalidInput_When_Verifying_Then_StopsWithoutAuthoritativeCountsAndInvalidTakesPrecedence(bool invalid)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.ConsumeRecord = null;
        fixture.Kafka.Poll = () =>
        {
            if (fixture.Kafka.Authentication.InstalledTokenExpiresOn is null) fixture.Kafka.Refresh();
            else fixture.Kafka.Clock.Advance(1000);
        };
        if (invalid)
        {
            JsonObject manifest = fixture.Json("manifest.json");
            manifest["identity"]!["instance_id"] = "foreign-instance";
            fixture.Set("manifest.json", manifest);
        }
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertFailure(output.RootElement, invalid ? "invalid" : "incomplete", 10);
        if (invalid) Assert.Contains(3, FailedChecks(output.RootElement));
        Assert.Equal(1000, output.RootElement.GetProperty("drain_elapsed_at_stop").GetInt64());
        Assert.True(output.RootElement.GetProperty("drain_elapsed_at_stop_lower_bound").GetBoolean());
        Assert.False(output.RootElement.TryGetProperty("drain_time", out _));
        Assert.Equal(1, fixture.Creates);
    }

    [Theory]
    [MemberData(nameof(InvalidIndexCases))]
    public async Task Given_InvalidPublicationDescriptor_When_Verifying_Then_RejectsThePublicationWithoutHashesOrCounts(string mutation)
    {
        using var fixture = new HostEvidenceBuilder();
        JsonObject index = fixture.Json("index.json");
        JsonArray artifacts = index["artifacts"]!.AsArray();
        JsonObject first = artifacts[0]!.DeepClone().AsObject();
        switch (mutation)
        {
            case "empty": index["artifacts"] = new JsonArray(); break;
            case "duplicate": artifacts.Add(first.DeepClone()); break;
            case "extra": artifacts.Add(new JsonObject { ["name"] = "extra.bin", ["length"] = 0, ["sha256"] = new string('0', 64) }); break;
            case "nested": first["name"] = "nested/ledger.jsonl"; artifacts[0] = first; break;
            case "reordered":
                var reordered = new JsonArray();
                foreach (JsonNode? item in artifacts.Reverse()) reordered.Add(item!.DeepClone());
                index["artifacts"] = reordered;
                break;
            case "negative-length": first["length"] = -1; artifacts[0] = first; break;
            case "unchecked-hash": first["sha256"] = ""; artifacts[0] = first; break;
            case "wrong-hash": first["sha256"] = new string('0', 64); artifacts[0] = first; break;
        }
        fixture.Set("index.json", index);
        fixture.PreservePublicationReferences = true;
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        Assert.Equal("invalid", output.RootElement.GetProperty("status").GetString());
        Assert.False(output.RootElement.TryGetProperty("ok", out _));
        Assert.Empty(output.RootElement.GetProperty("judged_file_hashes").EnumerateArray());
    }

    [Fact]
    public async Task Given_CompletePublication_When_Verifying_Then_HashesTheFiveActualPayloadsInFixedOrder()
    {
        using var fixture = new HostEvidenceBuilder();
        Assert.Equal(0, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        string[] names = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
        string[] hashes = output.RootElement.GetProperty("judged_file_hashes").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString() + ":" + item.GetProperty("sha256").GetString()).ToArray();
        Assert.Equal(names.Select(name => name + ":" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(fixture.Inputs[name]!))), hashes);
    }

    [Fact]
    public async Task Given_SameLengthPayloadChangeWithoutRebuildingReferences_When_Verifying_Then_RejectsIt()
    {
        using var fixture = new HostEvidenceBuilder();
        byte[] changed = fixture.Inputs["statistics.jsonl"]!.ToArray();
        changed[0] = changed[0] == (byte)'{' ? (byte)'[' : (byte)'{';
        fixture.Inputs["statistics.jsonl"] = changed;
        fixture.PreservePublicationReferences = true;
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        Assert.Equal("invalid", output.RootElement.GetProperty("status").GetString());
        Assert.Empty(output.RootElement.GetProperty("judged_file_hashes").EnumerateArray());
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task Given_TotalInputLimitAtRawByteBoundary_When_Verifying_Then_ChargesEachArtifactOnceAndDoesNotRechargeNestedRawJson(int adjustment, bool succeeds)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Options.MaxTotalInputBytes = fixture.ChargedInputBytes + adjustment;
        if (succeeds) Assert.Equal(0, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        else await Assert.ThrowsAsync<InputLimitExceededException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(succeeds ? 1 : 0, fixture.Creates);
    }

    [Theory]
    [InlineData("blob", 99, true)]
    [InlineData("blob", 100, false)]
    [InlineData("blob", 101, false)]
    [InlineData("local", 99, true)]
    [InlineData("local", 100, false)]
    [InlineData("local", 101, false)]
    public async Task Given_InputOperationNearDeadline_When_Verifying_Then_ExpiryIsExecutionFailureWithZeroOutputCalls(string phase, int elapsed, bool succeeds)
    {
        using var fixture = new HostEvidenceBuilder();
        if (phase == "blob") fixture.BeforeRead = (name, _) => { if (name.EndsWith("/publication/index.json", StringComparison.Ordinal)) fixture.Kafka.Clock.Advance(elapsed); };
        else
        {
            int calls = 0;
            fixture.BeforeScenarioRead = _ => { if (calls++ == 0) fixture.Kafka.Clock.Advance(elapsed); };
        }
        if (succeeds) Assert.Equal(0, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(succeeds ? 1 : 0, fixture.Creates);
    }

    [Theory]
    [InlineData(99, true)]
    [InlineData(100, false)]
    [InlineData(101, false)]
    public async Task Given_CleanupNearDeadline_When_Verifying_Then_BothNativeDisposalsFinishBeforeAnyPublication(int elapsed, bool succeeds)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Kafka.AdminDispose = () => { fixture.Events.Add("admin-dispose"); fixture.Kafka.Clock.Advance(elapsed); };
        if (succeeds) Assert.Equal(0, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["input-dispose", "input-dispose", "admin-dispose", "consumer-dispose"], fixture.Events.Take(4));
        Assert.Equal(succeeds ? 1 : 0, fixture.Creates);
    }

    [Theory]
    [InlineData("input")]
    [InlineData("admin")]
    [InlineData("consumer")]
    public async Task Given_AnyInputOrNativeCleanupThrows_When_Verifying_Then_NoResultPublicationOccurs(string phase)
    {
        using var fixture = new HostEvidenceBuilder();
        if (phase == "input") fixture.InputDispose = () => throw new IOException("synthetic input cleanup");
        if (phase == "admin") fixture.Kafka.AdminDispose = () => throw new IOException("synthetic admin cleanup");
        if (phase == "consumer") fixture.Kafka.ConsumerDispose = () => throw new IOException("synthetic consumer cleanup");
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, fixture.Creates);
        Assert.Null(fixture.Output);
        if (phase != "input") Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Kafka.Events);
    }

    [Theory]
    [InlineData("records")]
    [InlineData("bytes")]
    [InlineData("output")]
    public async Task Given_ConsumedOrOutputLimitExceeded_When_Verifying_Then_ExecutionFailureCleansWithoutPublication(string limit)
    {
        using var fixture = new HostEvidenceBuilder();
        if (limit == "bytes") fixture.Options.MaxConsumedBytes = 1;
        else if (limit == "output") fixture.Options.MaxOutputBytes = 1;
        else
        {
            fixture.Options.MaxConsumedRecords = 1;
            fixture.Kafka.Query = _ => new WatermarkOffsets(0, 2);
            long position = 0;
            fixture.Kafka.Position = _ => new Offset(position);
            fixture.ConsumeRecord = () =>
            {
                ConsumeResult<byte[], byte[]> record = HostEvidenceBuilder.Record();
                record.Offset = position++;
                return record;
            };
        }
        await Assert.ThrowsAsync<InputLimitExceededException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, fixture.Creates);
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Kafka.Events);
    }

    [Fact]
    public async Task Given_MalformedPersistedRawJson_When_Verifying_Then_JudgesNativeEvidenceRatherThanAssumingTypedEvidencePassed()
    {
        using var fixture = new HostEvidenceBuilder();
        string[] rows = Encoding.UTF8.GetString(fixture.Inputs["statistics.jsonl"]!).TrimEnd('\n').Split('\n');
        JsonObject envelope = JsonNode.Parse(rows[1])!.AsObject();
        envelope["raw_json"] = "{}";
        rows[1] = envelope.ToJsonString();
        fixture.Inputs["statistics.jsonl"] = Encoding.UTF8.GetBytes(string.Join('\n', rows) + "\n");
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertFailure(output.RootElement, "invalid", 20);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_UploadFailureAfterPossibleCommit_When_Verifying_Then_OneAttemptLeavesObjectAndNeverReturnsSuccess(bool expires)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.AfterCreate = _ =>
        {
            if (expires) fixture.Kafka.Clock.Advance(100);
            else throw new IOException("synthetic uncertain upload");
        };
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, fixture.Creates);
        Assert.NotNull(fixture.Output);
        Assert.Equal("create", fixture.Events[^1]);
    }

    [Fact]
    public async Task Given_NativeCleanupStagesIndividuallyWithinBudget_When_CombinedCleanupExpires_Then_NoPublicationOccurs()
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Kafka.AdminDispose = () => fixture.Kafka.Clock.Advance(50);
        fixture.Kafka.ConsumerDispose = () => fixture.Kafka.Clock.Advance(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Kafka.Events);
        Assert.Equal(0, fixture.Creates);
    }

    [Fact]
    public async Task Given_HostCancellationDuringSuccessfulCleanup_When_Verifying_Then_CleanupFinishesBeforeCancellationPreventsPublication()
    {
        using var fixture = new HostEvidenceBuilder();
        using var work = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        fixture.Kafka.AdminDispose = work.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Verify().ExecuteAsync(work.Token));
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Kafka.Events);
        Assert.Equal(0, fixture.Creates);
    }

    [Fact]
    public async Task Given_SafetyExpiryDuringDrain_When_Verifying_Then_CancelsKafkaAcquisitionWithoutCancellingHostWork()
    {
        using var fixture = new HostEvidenceBuilder();
        CancellationToken acquisition = default;
        fixture.SessionCreated = token => acquisition = token;
        fixture.ConsumeRecord = null;
        fixture.Kafka.Poll = () =>
        {
            if (fixture.Kafka.Authentication.InstalledTokenExpiresOn is null) fixture.Kafka.Refresh();
            else
            {
                fixture.Kafka.Clock.Advance(1000);
                Assert.True(acquisition.IsCancellationRequested);
                Assert.False(TestContext.Current.CancellationToken.IsCancellationRequested);
            }
        };
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        AssertFailure(output.RootElement, "incomplete", 10);
    }

    [Theory]
    [InlineData("blob", 99, true)]
    [InlineData("blob", 100, false)]
    [InlineData("blob", 101, false)]
    [InlineData("note", 99, true)]
    [InlineData("note", 100, false)]
    [InlineData("note", 101, false)]
    [InlineData("local", 99, true)]
    [InlineData("local", 100, false)]
    [InlineData("local", 101, false)]
    [InlineData("cleanup", 99, true)]
    [InlineData("cleanup", 100, false)]
    [InlineData("cleanup", 101, false)]
    [InlineData("upload", 99, true)]
    [InlineData("upload", 100, false)]
    [InlineData("upload", 101, false)]
    public async Task Given_DelayedTimerCallbacks_When_MonotonicOperationBudgetExpires_Then_StillRejectsExpiredExecutionOrPublication(string phase, int elapsed, bool succeeds)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.DelayHandlerTimerCallbacks();
        if (phase is "blob" or "note") fixture.BeforeRead = (name, _) =>
        {
            if (name.EndsWith(phase == "blob" ? "/publication/index.json" : "/start-line.json", StringComparison.Ordinal)) fixture.Kafka.Clock.Advance(elapsed);
        };
        if (phase == "local")
        {
            int calls = 0;
            fixture.BeforeScenarioRead = _ => { if (calls++ == 0) fixture.Kafka.Clock.Advance(elapsed); };
        }
        if (phase == "cleanup") fixture.Kafka.AdminDispose = () => fixture.Kafka.Clock.Advance(elapsed);
        if (phase == "upload") fixture.AfterCreate = _ => fixture.Kafka.Clock.Advance(elapsed);
        if (succeeds) Assert.Equal(0, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        else await Assert.ThrowsAsync<TimeoutException>(() => fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(succeeds || phase == "upload" ? 1 : 0, fixture.Creates);
        if (phase == "upload") Assert.NotNull(fixture.Output);
    }

    [Theory]
    [InlineData(999, false)]
    [InlineData(1000, false)]
    [InlineData(1001, false)]
    [InlineData(999, true)]
    [InlineData(1000, true)]
    [InlineData(1001, true)]
    public async Task Given_WatermarkBudgetLongerThanSafety_When_QueryReturnsNearSafetyDeadline_Then_NativeTimeoutAndCheckElevenHonorSafety(int elapsed, bool delayedTimers)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Kafka.Options.WatermarkTimeout = TimeSpan.FromMilliseconds(2000);
        if (delayedTimers) fixture.DelayHandlerTimerCallbacks();
        fixture.Kafka.Query = _ =>
        {
            fixture.Kafka.Clock.Advance(elapsed);
            return new WatermarkOffsets(0, 1);
        };
        Assert.Equal(elapsed < 1000 ? 0 : 1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal([TimeSpan.FromMilliseconds(1000)], fixture.Kafka.QueryTimeouts);
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        if (elapsed < 1000)
        {
            Assert.Equal("complete_valid", output.RootElement.GetProperty("status").GetString());
            Assert.Equal(1, output.RootElement.GetProperty("ok").GetInt64());
        }
        else
        {
            AssertFailure(output.RootElement, "incomplete", 11);
            Assert.DoesNotContain(10, FailedChecks(output.RootElement));
            Assert.False(output.RootElement.TryGetProperty("end_line", out _));
            AssertNoDurations(output.RootElement);
        }
        Assert.Equal(1, fixture.Creates);
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Kafka.Events);
        Assert.Equal("create", fixture.Events[^1]);
    }

    [Fact]
    public async Task Given_HostCancellationDuringEndQuery_When_Verifying_Then_CleansAndPropagatesCancellationWithoutPublication()
    {
        using var fixture = new HostEvidenceBuilder();
        using var host = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        fixture.Kafka.Query = _ => { host.Cancel(); return new WatermarkOffsets(0, 1); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Verify().ExecuteAsync(host.Token));
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Kafka.Events);
        Assert.Equal(0, fixture.Creates);
        Assert.Null(fixture.Output);
    }

    private static void SetNonemptyWindows(HostEvidenceBuilder fixture, long maximum) => ChangeNativeReports(fixture, (native, _) =>
    {
        JsonNode broker = native["brokers"]!["sasl_ssl://broker-a:9093/1"]!;
        broker["throttle"]!["cnt"] = 1;
        broker["throttle"]!["max"] = maximum;
        broker["rtt"]!["cnt"] = 1;
        broker["rtt"]!["p99"] = 99327;
    });

    private static void ChangeNativeReports(HostEvidenceBuilder fixture, Action<JsonObject, int> change)
    {
        string[] rows = Encoding.UTF8.GetString(fixture.Inputs["statistics.jsonl"]!).TrimEnd('\n').Split('\n');
        for (int index = 0; index < rows.Length; index++)
        {
            JsonObject envelope = JsonNode.Parse(rows[index])!.AsObject();
            JsonObject native = JsonNode.Parse(envelope["raw_json"]!.GetValue<string>())!.AsObject();
            change(native, index);
            envelope["raw_json"] = native.ToJsonString();
            rows[index] = envelope.ToJsonString();
        }
        fixture.Inputs["statistics.jsonl"] = Encoding.UTF8.GetBytes(string.Join('\n', rows) + "\n");
    }

    private static void AssertUnusableProbe(JsonElement result)
    {
        JsonElement probe = result.GetProperty("probe_observation");
        Assert.False(probe.GetProperty("usable").GetBoolean());
        Assert.False(probe.GetProperty("qualifying").GetBoolean());
        AssertPropertyNames(probe, "usable", "qualifying");
    }

    private static void AssertUsableNoSampleProbe(JsonElement result)
    {
        JsonElement probe = result.GetProperty("probe_observation");
        Assert.True(probe.GetProperty("usable").GetBoolean());
        Assert.False(probe.GetProperty("qualifying").GetBoolean());
        AssertPropertyNames(probe, "usable", "qualifying");
    }

    private static void AssertUsableProbe(JsonElement result, long maximum, bool qualifying)
    {
        JsonElement probe = result.GetProperty("probe_observation");
        Assert.True(probe.GetProperty("usable").GetBoolean());
        Assert.Equal(qualifying, probe.GetProperty("qualifying").GetBoolean());
        Assert.Equal("sasl_ssl://broker-a:9093/1", probe.GetProperty("broker").GetString());
        Assert.Equal(0, probe.GetProperty("report_index").GetInt64());
        Assert.Equal(1, probe.GetProperty("window_count").GetInt64());
        Assert.Equal(maximum, probe.GetProperty("window_maximum").GetInt64());
        AssertPropertyNames(probe, "usable", "qualifying", "broker", "report_index", "window_count", "window_maximum");
    }

    private static void AssertRecordedWindows(JsonElement result, long? maximum, long firstTimeoutIncrement = 0, bool includeCodes = true)
    {
        JsonElement evidence = result.GetProperty("a01_recorded_evidence");
        if (includeCodes) Assert.Empty(evidence.GetProperty("delivery_error_codes").EnumerateArray());
        AssertPropertyNames(evidence, includeCodes ? ["delivery_error_codes", "windows"] : ["windows"]);
        JsonElement[] windows = evidence.GetProperty("windows").EnumerateArray().ToArray();
        Assert.Equal(3, windows.Length);
        for (int index = 0; index < windows.Length; index++)
        {
            JsonElement window = windows[index];
            Assert.Equal("sasl_ssl://broker-a:9093/1", window.GetProperty("broker").GetString());
            Assert.Equal(index, window.GetProperty("report_index").GetInt64());
            Assert.Equal(90 + index * 10, window.GetProperty("native_timestamp").GetInt64());
            Assert.Equal(index == 0 ? firstTimeoutIncrement : 0, window.GetProperty("request_timeouts_increment").GetInt64());
            Assert.Equal(0, window.GetProperty("disconnects_increment").GetInt64());
            if (maximum is long value)
            {
                Assert.Equal(value, window.GetProperty("throttle_maximum_ms").GetInt64());
                Assert.Equal(99327, window.GetProperty("round_trip_p99_us").GetInt64());
            }
            AssertPropertyNames(window, maximum is null
                ? ["broker", "report_index", "native_timestamp", "request_timeouts_increment", "disconnects_increment"]
                : ["broker", "report_index", "native_timestamp", "request_timeouts_increment", "disconnects_increment", "throttle_maximum_ms", "round_trip_p99_us"]);
        }
    }

    private static void AssertPropertyNames(JsonElement value, params string[] names) =>
        Assert.Equal(names.Order(StringComparer.Ordinal), value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));

    private static void ExpireQuery(HostEvidenceBuilder fixture, string phase, int elapsed)
    {
        if (phase == "token") fixture.Kafka.Poll = () => fixture.Kafka.Clock.Advance(elapsed);
        if (phase == "metadata") fixture.Kafka.ReadMetadata = () => { fixture.Kafka.Clock.Advance(elapsed); return KafkaSessionTestSupport.Metadata(0); };
        if (phase == "watermark") fixture.Kafka.Query = _ => { fixture.Kafka.Clock.Advance(elapsed); return new WatermarkOffsets(0, 1); };
        if (phase == "empty-metadata") fixture.Kafka.ReadMetadata = () => KafkaSessionTestSupport.Metadata();
    }
    private static void AssertFailure(JsonElement result, string status, int failed)
    {
        Assert.Equal(status, result.GetProperty("status").GetString());
        Assert.Contains(failed, FailedChecks(result));
        foreach (string count in new[] { "ok", "loss", "phantom", "reported_failure", "duplicate", "reorder" }) Assert.False(result.TryGetProperty(count, out _));
    }
    private static int[] FailedChecks(JsonElement result) => result.GetProperty("failed_checks").EnumerateArray().Select(item => item.GetInt32()).ToArray();
    private static string[] ExpectedReads(HostEvidenceBuilder fixture)
    {
        string run = fixture.Options.RunId!, phase = fixture.Options.Phase!;
        int repeat = fixture.Options.Repeat!.Value;
        string instance = fixture.Options.InstanceId!;
        return
        [
            PhasePaths.Registration(run, phase, repeat, instance),
            PhasePaths.StartLineCompletion(run, phase, repeat),
            PhasePaths.StartLine(run, phase, repeat),
            PhasePaths.BrokerInventory(run, phase, repeat),
            PhasePaths.ProducerCompletion(run, phase, repeat, instance),
            PhasePaths.Publication(run, phase, repeat, instance, "index.json"),
            PhasePaths.Publication(run, phase, repeat, instance, "ledger.jsonl"),
            PhasePaths.Publication(run, phase, repeat, instance, "manifest.json"),
            PhasePaths.Publication(run, phase, repeat, instance, "statistics.jsonl"),
            PhasePaths.Publication(run, phase, repeat, instance, "request-lifecycle.jsonl"),
            PhasePaths.Publication(run, phase, repeat, instance, "capabilities.json")
        ];
    }
    private static void AssertNoDurations(JsonElement result)
    {
        Assert.False(result.TryGetProperty("drain_time", out _));
        Assert.False(result.TryGetProperty("drain_elapsed_at_stop", out _));
    }
}
#pragma warning restore CA1707
