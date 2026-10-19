using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class VerifyRunHandlerTests
{
    private static readonly string[] ExpectedDuplicateHashes = ["ledger.jsonl:a", "manifest.json:b", "ledger.jsonl:c"];

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
        Assert.Equal("results/custom.json", fixture.OutputName);
        Assert.Equal("synthetic-producer", result.GetProperty("generator_version").GetString());
        Assert.Equal("synthetic-native", result.GetProperty("runtime_versions").GetProperty("Confluent.Kafka").GetString());
        Assert.Equal(typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(), result.GetProperty("verifier_confluent_kafka_version").GetString());
        Assert.Equal(Library.VersionString, result.GetProperty("verifier_librdkafka_version").GetString());
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
        Assert.Equal(["index.json"], fixture.Reads);
        Assert.Empty(fixture.Events);
        Assert.Equal(0, fixture.Creates);
        Assert.Null(fixture.Output);
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_OrderedDuplicateOrEmptyIndexDeclarations_When_Verifying_Then_ProjectsOnlyDeclaredPayloadDigestsAndStillReadsAllFixedFiles(bool empty)
    {
        using var fixture = new HostEvidenceBuilder();
        JsonObject index = fixture.Json("index.json");
        index["artifacts"] = empty ? new JsonArray() : new JsonArray(
            Descriptor("ledger.jsonl", "a"), Descriptor("extra.bin", "x"), Descriptor("manifest.json", "b"), Descriptor("ledger.jsonl", "c"));
        fixture.Set("index.json", index);
        Assert.Equal(0, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        string[] hashes = output.RootElement.GetProperty("judged_file_hashes").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString() + ":" + item.GetProperty("sha256").GetString()).ToArray();
        Assert.Equal(empty ? [] : ExpectedDuplicateHashes, hashes);
        Assert.Equal(["index.json", "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "notes/custom.json"], fixture.Reads);
        static JsonObject Descriptor(string name, string digest) => new() { ["name"] = name, ["length"] = -1, ["sha256"] = digest };
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task Given_TotalInputLimitAtRawByteBoundary_When_Verifying_Then_ChargesEachArtifactOnceAndDoesNotRechargeNestedRawJson(int adjustment, bool succeeds)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Options.MaxTotalInputBytes = fixture.Inputs.Values.Sum(value => (long)value!.Length) + fixture.Note!.Length + adjustment;
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
        if (phase == "blob") fixture.BeforeRead = (name, _) => { if (name == "index.json") fixture.Kafka.Clock.Advance(elapsed); };
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
            if (name == (phase == "blob" ? "index.json" : "notes/custom.json")) fixture.Kafka.Clock.Advance(elapsed);
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
    private static void AssertNoDurations(JsonElement result)
    {
        Assert.False(result.TryGetProperty("drain_time", out _));
        Assert.False(result.TryGetProperty("drain_elapsed_at_stop", out _));
    }
}
#pragma warning restore CA1707
