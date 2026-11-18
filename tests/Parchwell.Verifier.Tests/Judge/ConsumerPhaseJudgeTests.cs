using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Judge;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

/// <summary>
/// Judge mode for the consumer pairs C01, C03 and C10 (items/521/plan-consumer-phase-judge.md, section 6).
/// Fixtures are the B04 judge fixture with the scenario relabelled and a <c>consumer</c> block added to each result.
/// </summary>
#pragma warning disable CA1707
public sealed class ConsumerPhaseJudgeTests
{
    private const string Run = "synthetic-c5";
    private static readonly Lazy<Task<Stores>> BaseC0 = new(() => BuildAsync("C03", "C0"));
    private static readonly Lazy<Task<Stores>> BaseC1 = new(() => BuildAsync("C10", "C1"));

        public static TheoryData<string> BrokenBlocks => new()
    {
        "missing", "extra-property", "schema-2", "status-done", "reasons-unordered", "invalid-without-reasons", "unknown-reason",
        "incomplete-with-counts", "complete-without-counts", "negative-once", "extra-rows-below-again", "foreign-group",
        "kill-zero", "kill-over-limit", "kill-without-reassignment", "reassignment-both-null", "reassignment-both-set",
        "reassignment-bad-reason", "reassignment-negative", "outcome-ok", "partitions-descending", "partitions-repeated",
        "timestamp-without-fraction", "not-attempted-with-error-code", "members-object", "reassignment-without-kill",
        "partition-negative", "offset-missing-error-code", "counts-extra-property", "committed-never-negative", "partitions-object",
        "processed-again-above-extra-rows", "complete-with-reasons", "duplicate-reasons", "incomplete-with-reassignment",
        "not-attempted-with-started", "not-attempted-with-ended", "schema-0", "membership-missing", "membership-extra-property",
        "membership-reordered", "membership-negative-rows", "membership-takeover-string", "membership-no-slow-rows", "membership-during-slow-number",
        "membership-without-counts"
    };

    private static void BreakBlock(string change, JsonObject block)
    {
        JsonObject counts() => block["counts"]!.AsObject();
        JsonObject offsetRead() => block["offset_read"]!.AsObject();
        switch (change)
        {
            case "missing": break;
            case "membership-missing": block.Remove("membership"); break;
            case "membership-extra-property": block["membership"]!["x"] = 0; break;
            case "membership-reordered":
                JsonNode first = block["membership"]!["slow_partition"]!.DeepClone();
                block["membership"]!.AsObject().Remove("slow_partition"); block["membership"]!["slow_partition"] = first; break;
            case "membership-negative-rows": block["membership"]!["subject_lost_rows"] = -1; break;
            case "membership-takeover-string": block["membership"]!["witness_takeover_microseconds"] = "61"; break;
            case "membership-no-slow-rows": block["membership"]!["slow_processed_rows"] = 0; break;
            case "membership-during-slow-number": block["membership"]!["witness_assigned_slow_partition_during_slow"] = 1; break;
            case "membership-without-counts":
                // A valid invalid block that keeps membership, so only the membership rule can fail it.
                block["status"] = "invalid"; block["reasons"] = new JsonArray("consumer_record_invalid"); block.Remove("counts"); break;
            case "extra-property": block["x"] = 0; break;
            case "schema-2": block["schema_version"] = 2; break;
            case "status-done": block["status"] = "done"; break;
            case "reasons-unordered":
                block["status"] = "invalid"; RemoveCounts(block);
                block["reasons"] = new JsonArray("consumer_stop_invalid", "consumer_record_invalid"); break;
            case "invalid-without-reasons": block["status"] = "invalid"; RemoveCounts(block); break;
            case "unknown-reason":
                block["status"] = "invalid"; RemoveCounts(block); block["reasons"] = new JsonArray("consumer_x"); break;
            case "incomplete-with-counts":
                block["status"] = "incomplete"; block["reasons"] = new JsonArray("consumer_completion_deadline"); break;
            case "complete-without-counts": block.Remove("counts"); break;
            case "negative-once": counts()["processed_once"] = -1; break;
            case "extra-rows-below-again": counts()["processed_again"] = 1; counts()["processed_again_extra_rows"] = 0; break;
            case "foreign-group": block["group_id"] = "synthetic-c5-c0-r1-consumers"; break;
            case "kill-zero": block["kill_count"] = 0; block["reassignment"] = Reassignment(5, null); break;
            case "kill-over-limit": block["kill_count"] = 1_000_001; block["reassignment"] = Reassignment(5, null); break;
            case "kill-without-reassignment": block["kill_count"] = 5; break;
            case "reassignment-both-null": block["kill_count"] = 5; block["reassignment"] = Reassignment(null, null); break;
            case "reassignment-both-set": block["kill_count"] = 5; block["reassignment"] = Reassignment(5, "different_nodes"); break;
            case "reassignment-bad-reason": block["kill_count"] = 5; block["reassignment"] = Reassignment(null, "late"); break;
            case "reassignment-negative": block["kill_count"] = 5; block["reassignment"] = Reassignment(-1, null); break;
            case "outcome-ok": offsetRead()["outcome"] = "ok"; break;
            case "partitions-descending": offsetRead()["requested_partitions"] = new JsonArray(1, 0); break;
            case "partitions-repeated": offsetRead()["requested_partitions"] = new JsonArray(0, 0); break;
            case "timestamp-without-fraction": offsetRead()["started_utc"] = "2026-11-16T10:00:00Z"; break;
            case "not-attempted-with-error-code":
                offsetRead()["outcome"] = "not_attempted"; offsetRead()["started_utc"] = null; offsetRead()["ended_utc"] = null;
                offsetRead()["error_code"] = 0; offsetRead()["offsets"] = new JsonArray(); break;
            case "members-object": block["members"] = new JsonObject(); break;
            case "reassignment-without-kill": block["reassignment"] = Reassignment(5, null); break;
            case "partition-negative": offsetRead()["requested_partitions"] = new JsonArray(-1, 0); break;
            case "offset-missing-error-code":
                offsetRead()["offsets"] = new JsonArray(new JsonObject { ["partition"] = 0, ["offset"] = 7 },
                    new JsonObject { ["partition"] = 1, ["offset"] = 9, ["error_code"] = 0 }); break;
            case "counts-extra-property": counts()["x"] = 0; break;
            case "committed-never-negative": counts()["committed_never_processed"] = -1; break;
            case "partitions-object": block["partitions"] = new JsonObject(); break;
            case "processed-again-above-extra-rows": counts()["processed_again"] = 3; break;
            case "complete-with-reasons": block["reasons"] = new JsonArray("consumer_stop_invalid"); break;
            case "duplicate-reasons":
                block["status"] = "invalid"; RemoveCounts(block);
                block["reasons"] = new JsonArray("consumer_record_invalid", "consumer_record_invalid"); break;
            case "incomplete-with-reassignment":
                block["status"] = "incomplete"; RemoveCounts(block); block["reasons"] = new JsonArray("consumer_completion_deadline");
                block["kill_count"] = 5; block["reassignment"] = Reassignment(5, null); break;
            case "not-attempted-with-started":
                offsetRead()["outcome"] = "not_attempted"; offsetRead()["ended_utc"] = null; offsetRead()["error_code"] = null; offsetRead()["offsets"] = new JsonArray(); break;
            case "not-attempted-with-ended":
                offsetRead()["outcome"] = "not_attempted"; offsetRead()["started_utc"] = null; offsetRead()["error_code"] = null; offsetRead()["offsets"] = new JsonArray(); break;
            case "schema-0": block["schema_version"] = 0; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
    }

    [Fact]
    public async Task Given_FiveCompleteConsumerRepeats_When_Judged_Then_PublishesEachProjectionAndPhaseTotals()
    {
        Stores stores = (await BaseC0.Value).Copy();
        (int exit, byte[] bytes) = await JudgeAsync(stores, "C03", "C0");
        using (JsonDocument candidate = JsonDocument.Parse(bytes))
        {
            Assert.Equal(0, exit);
            JsonElement root = candidate.RootElement;
            Assert.Equal("complete_valid", root.GetProperty("status").GetString());
            foreach (JsonElement repeat in root.GetProperty("repeats").EnumerateArray())
                Assert.Equal(DefaultProjection(repeat.GetProperty("repeat").GetInt32()).ToJsonString(),
                    JsonNode.Parse(repeat.GetProperty("consumer").GetRawText())!.ToJsonString());
            Assert.Equal("""{"status":"complete_valid","reasons":[],"counts":{"processed_once":50,"processed_again":5,"processed_again_extra_rows":10,"committed_never_processed":0}}""",
                JsonNode.Parse(root.GetProperty("consumer").GetRawText())!.ToJsonString());
        }
    }


    private static readonly Lazy<Task<Stores>> BaseB04 = new(() => BuildAsync("B04", "C0", addConsumer: false));

    private static JsonNode Parse(byte[] bytes) => JsonNode.Parse(bytes)!;
    private static JsonNode RepeatOf(JsonNode candidate, int repeat) => candidate["repeats"]![repeat - 1]!;
    private static string Text(JsonNode? node) => node is null ? "<absent>" : node.ToJsonString();

    private static Stores Mutated(Stores source, int repeat, Action<JsonObject> block, int exit = 0, string phase = "C0")
    {
        Stores copy = source.Copy();
        Apply(copy, phase, repeat, result => block(result["consumer"]!.AsObject()), exit);
        return copy;
    }

    private static readonly string[] CountFields = ["ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"];
    private static readonly string[] StatusChanges = ["status-done", "reasons-unordered", "invalid-without-reasons", "unknown-reason", "incomplete-with-counts", "duplicate-reasons", "incomplete-with-reassignment", "membership-without-counts"];

    [Theory]
    [MemberData(nameof(BrokenBlocks))]
    public async Task Given_BrokenConsumerBlock_When_Judged_Then_FailsJ3AndPublishesNoProjection(string change)
    {
        Stores stores = (await BaseC0.Value).Copy();
        int exit = StatusChanges.Contains(change, StringComparer.Ordinal) ? 1 : 0;
        Apply(stores, "C0", 2, result =>
        {
            if (change == "missing") { result.Remove("consumer"); return; }
            JsonObject block = DefaultBlock(2);
            BreakBlock(change, block);
            result["consumer"] = block;
        }, exit);
        (int judgeExit, byte[] bytes) = await JudgeAsync(stores, "C03", "C0");
        JsonNode candidate = Parse(bytes);
        JsonNode second = RepeatOf(candidate, 2);
        Assert.Equal(1, judgeExit);
        Assert.False(second["checks"]!["j3_verifier_receipt"]!.GetValue<bool>());
        Assert.Contains("j3_verifier_receipt", second["reasons"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal("incomplete", second["status"]!.GetValue<string>());
        Assert.Null(second["consumer"]);
        Assert.True(second.AsObject().ContainsKey("consumer"));
        Assert.Equal("incomplete", candidate["consumer"]!["status"]!.GetValue<string>());
        Assert.False(candidate["consumer"]!.AsObject().ContainsKey("counts"));
        Assert.Contains("j10_consumer_counts_unavailable", candidate["consumer"]!["reasons"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal("incomplete", candidate["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Given_ConsumerPropertyOnNonConsumerScenario_When_Judged_Then_FailsJ3()
    {
        Stores stores = (await BaseB04.Value).Copy();
        Apply(stores, "C0", 2, result => result["consumer"] = new JsonObject());
        (_, byte[] bytes) = await JudgeAsync(stores, "B04", "C0");
        JsonNode candidate = Parse(bytes);
        Assert.False(candidate["repeats"]![1]!["checks"]!["j3_verifier_receipt"]!.GetValue<bool>());
        Assert.False(candidate.AsObject().ContainsKey("consumer"));
    }

    [Theory]
    [InlineData(1, "incomplete", true)]
    [InlineData(1, "complete_valid", false)]
    [InlineData(0, "incomplete", false)]
    [InlineData(0, "complete_valid", true)]
    public async Task Given_ReceiptExitAndStatuses_When_Judged_Then_J3FollowsTheConsumerExitRule(int exit, string consumerStatus, bool expected)
    {
        Stores stores = Mutated((await BaseC0.Value), 2, block =>
        {
            block["status"] = consumerStatus;
            if (consumerStatus == "incomplete") { block["reasons"] = new JsonArray("consumer_completion_deadline"); RemoveCounts(block); }
        }, exit);
        (_, byte[] bytes) = await JudgeAsync(stores, "C03", "C0");
        Assert.Equal(expected, RepeatOf(Parse(bytes), 2)["checks"]!["j3_verifier_receipt"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Given_ConsumerIncompleteOnAcceptedRepeat_When_Judged_Then_KeepsProducerSideAndPublishesIncompletePhaseConsumer()
    {
        Stores stores = Mutated((await BaseC0.Value), 3, block =>
        {
            block["status"] = "incomplete"; block["reasons"] = new JsonArray("consumer_completion_deadline"); RemoveCounts(block);
        }, 1);
        (int exit, byte[] bytes) = await JudgeAsync(stores, "C03", "C0");
        JsonNode candidate = Parse(bytes), third = RepeatOf(candidate, 3);
        Assert.Equal(1, exit);
        Assert.Equal("complete_valid", third["status"]!.GetValue<string>());
        Assert.NotNull(third["counts"]);
        Assert.Equal("incomplete", third["consumer"]!["status"]!.GetValue<string>());
        Assert.NotNull(candidate["counts"]);
        Assert.Equal("""{"status":"incomplete","reasons":["consumer_completion_deadline","j10_consumer_counts_unavailable"]}""", Text(candidate["consumer"]));
        Assert.Equal("incomplete", candidate["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Given_ConsumerInvalidOnAcceptedRepeat_When_Judged_Then_PhaseConsumerAndCandidateAreInvalid()
    {
        Stores stores = Mutated((await BaseC0.Value), 2, block =>
        {
            block["status"] = "invalid"; block["reasons"] = new JsonArray("consumer_record_invalid"); RemoveCounts(block);
        }, 1);
        (int exit, byte[] bytes) = await JudgeAsync(stores, "C03", "C0");
        JsonNode candidate = Parse(bytes);
        Assert.Equal(1, exit);
        Assert.Equal("invalid", candidate["consumer"]!["status"]!.GetValue<string>());
        Assert.Equal("invalid", candidate["status"]!.GetValue<string>());
    }

    private static async Task<Stores> ProducerIncompleteRepeat4(string[] consumerReasons)
    {
        Stores stores = (await BaseC0.Value).Copy();
        Apply(stores, "C0", 4, result =>
        {
            result["status"] = "incomplete";
            foreach (string count in CountFields) result.Remove(count);
            result["consumer"]!["status"] = "invalid"; result["consumer"]!["reasons"] = new JsonArray(consumerReasons.Select(x => (JsonNode?)x).ToArray());
            RemoveCounts(result["consumer"]!.AsObject());
        }, 1);
        return stores;
    }

    [Fact]
    public async Task Given_ProducerSideOnlyReason_When_Judged_Then_RootFollowsTheProducerSide()
    {
        Stores stores = await ProducerIncompleteRepeat4(["producer_side_not_complete_valid"]);
        (int exit, byte[] bytes) = await JudgeAsync(stores, "C03", "C0");
        JsonNode candidate = Parse(bytes);
        Assert.Equal(1, exit);
        Assert.Equal("incomplete", RepeatOf(candidate, 4)["status"]!.GetValue<string>());
        Assert.Null(candidate["counts"]);
        Assert.Contains("j7_counts_unavailable", candidate["reasons"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal("incomplete", candidate["consumer"]!["status"]!.GetValue<string>());
        Assert.Equal("incomplete", candidate["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Given_ProducerSideReasonBesideAnotherReason_When_Judged_Then_PhaseConsumerIsInvalid()
    {
        Stores stores = await ProducerIncompleteRepeat4(["consumer_record_invalid", "producer_side_not_complete_valid"]);
        (_, byte[] bytes) = await JudgeAsync(stores, "C03", "C0");
        JsonNode candidate = Parse(bytes);
        Assert.Equal("invalid", RepeatOf(candidate, 4)["consumer"]!["status"]!.GetValue<string>());
        Assert.Equal("invalid", candidate["consumer"]!["status"]!.GetValue<string>());
        Assert.Equal("invalid", candidate["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Given_RepeatThatFailsJ2_When_Judged_Then_ItsProjectionIsNullAndThePhaseConsumerIsIncomplete()
    {
        Stores stores = (await BaseC0.Value).Copy();
        Assert.True(stores.Verifier.Remove(PhasePaths.StartLineCompletion(Run, "C0", 5)));
        (_, byte[] bytes) = await JudgeAsync(stores, "C03", "C0");
        JsonNode candidate = Parse(bytes);
        Assert.False(RepeatOf(candidate, 5)["checks"]!["j2_publication_unaccepted"]!.GetValue<bool>());
        Assert.True(RepeatOf(candidate, 5).AsObject().ContainsKey("consumer"));
        Assert.Null(RepeatOf(candidate, 5)["consumer"]);
        Assert.Equal("incomplete", candidate["consumer"]!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Given_CountsThatOverflowWhenSummed_When_Judged_Then_PhaseCountsAreUnavailable()
    {
        Stores stores = (await BaseC0.Value).Copy();
        foreach (int repeat in new[] { 1, 2 })
            stores = Mutated(stores, repeat, block => block["counts"]!["processed_once"] = long.MaxValue);
        (int exit, byte[] bytes) = await JudgeAsync(stores, "C03", "C0");
        JsonNode candidate = Parse(bytes);
        Assert.Equal(1, exit);
        Assert.Equal("""{"status":"incomplete","reasons":["j10_consumer_counts_unavailable"]}""", Text(candidate["consumer"]));
        Assert.Equal("incomplete", candidate["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Given_CountsThatSumToInt64Maximum_When_Judged_Then_PhaseCountsArePublished()
    {
        Stores stores = Mutated((await BaseC0.Value), 1, block => block["counts"]!["processed_once"] = 9223372036854775767);
        (int exit, byte[] bytes) = await JudgeAsync(stores, "C03", "C0");
        JsonNode candidate = Parse(bytes);
        Assert.Equal(0, exit);
        Assert.Equal("""{"status":"complete_valid","reasons":[],"counts":{"processed_once":9223372036854775807,"processed_again":5,"processed_again_extra_rows":10,"committed_never_processed":0}}""", Text(candidate["consumer"]));
        Assert.Equal("complete_valid", candidate["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Given_KilledConsumerRepeats_When_Judged_Then_EachRepeatProjectsItsOwnReassignmentAndThePhaseHasNone()
    {
        Stores stores = (await BaseC1.Value).Copy();
        JsonObject[] reassignments =
        [
            Reassignment(1234567, null), Reassignment(null, "different_nodes"), Reassignment(null, "no_reassignment"),
            Reassignment(null, "no_reassignment"), Reassignment(null, "no_reassignment")
        ];
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            int index = repeat - 1;
            Apply(stores, "C1", repeat, result =>
            {
                result["consumer"]!["kill_count"] = 50;
                result["consumer"]!["reassignment"] = reassignments[index].DeepClone();
            });
        }
        (int exit, byte[] bytes) = await JudgeAsync(stores, "C10", "C1");
        JsonNode candidate = Parse(bytes);
        Assert.Equal(0, exit);
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            Assert.Equal(reassignments[repeat - 1].ToJsonString(), Text(RepeatOf(candidate, repeat)["consumer"]!["reassignment"]));
            Assert.Equal(50, RepeatOf(candidate, repeat)["consumer"]!["kill_count"]!.GetValue<int>());
        }
        Assert.False(candidate["consumer"]!.AsObject().ContainsKey("reassignment"));
    }

    [Theory]
    [InlineData("kill-one")]
    [InlineData("kill-million")]
    [InlineData("zero-processed-once")]
    [InlineData("not-attempted-without-error")]
    public async Task Given_BoundaryConsumerBlock_When_Judged_Then_J3PassesAndTheBlockIsProjected(string change)
    {
        JsonObject expectedBlock = DefaultBlock(2);
        Stores stores = Mutated((await BaseC0.Value), 2, block =>
        {
            switch (change)
            {
                case "kill-one": block["kill_count"] = 1; block["reassignment"] = Reassignment(0, null); break;
                case "kill-million": block["kill_count"] = 1_000_000; block["reassignment"] = Reassignment(0, null); break;
                case "zero-processed-once":
                    block["counts"] = JsonNode.Parse("""{"processed_once":0,"processed_again":1,"processed_again_extra_rows":1,"committed_never_processed":0}"""); break;
                default:
                    block["offset_read"] = JsonNode.Parse("""{"outcome":"not_attempted","requested_partitions":[0,1],"started_utc":null,"ended_utc":null,"error_code":null,"offsets":[]}"""); break;
            }
            foreach (var property in block.ToList()) expectedBlock[property.Key] = property.Value?.DeepClone();
        });
        (_, byte[] bytes) = await JudgeAsync(stores, "C03", "C0");
        JsonNode second = RepeatOf(Parse(bytes), 2);
        Assert.True(second["checks"]!["j3_verifier_receipt"]!.GetValue<bool>());
        var expected = new JsonObject();
        foreach (string name in new[] { "status", "reasons", "group_id", "kill_count", "offset_read", "counts", "membership", "reassignment" })
            if (expectedBlock.TryGetPropertyValue(name, out JsonNode? value)) expected[name] = value?.DeepClone();
        Assert.Equal(expected.ToJsonString(), Text(second["consumer"]));
    }

    [Fact]
    public async Task Given_MembershipOnABlockOfAnotherConsumerScenario_When_Judged_Then_FailsJ3()
    {
        Stores stores = (await BaseC1.Value).Copy();
        Apply(stores, "C1", 2, result => result["consumer"]!["membership"] = JsonNode.Parse(Membership));
        (int exit, byte[] bytes) = await JudgeAsync(stores, "C10", "C1");
        JsonNode second = RepeatOf(Parse(bytes), 2);
        Assert.Equal(1, exit);
        Assert.False(second["checks"]!["j3_verifier_receipt"]!.GetValue<bool>());
        Assert.Null(second["consumer"]);
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(2, 0, false)]
    [InlineData(2, null, false)]
    public async Task Given_BoundaryMembershipValues_When_Judged_Then_J3PassesAndTheBlockIsProjected(int slowRows, int? takeover, bool during)
    {
        Stores stores = Mutated(await BaseC0.Value, 2, block =>
        {
            block["membership"]!["slow_processed_rows"] = slowRows;
            block["membership"]!["witness_takeover_microseconds"] = takeover;
            block["membership"]!["witness_assigned_slow_partition_during_slow"] = during;
        });
        (_, byte[] bytes) = await JudgeAsync(stores, "C03", "C0");
        JsonNode second = RepeatOf(Parse(bytes), 2);
        Assert.True(second["checks"]!["j3_verifier_receipt"]!.GetValue<bool>());
        Assert.Equal(slowRows, second["consumer"]!["membership"]!["slow_processed_rows"]!.GetValue<int>());
        Assert.Equal(takeover, second["consumer"]!["membership"]!["witness_takeover_microseconds"]?.GetValue<int>());
    }

    [Fact]
    public async Task Given_FiveC03Repeats_When_Judged_Then_EachProjectionCarriesMembershipAfterCountsAndTheRootDoesNot()
    {
        (int exit, byte[] bytes) = await JudgeAsync((await BaseC0.Value).Copy(), "C03", "C0");
        JsonNode candidate = Parse(bytes);
        Assert.Equal(0, exit);
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            JsonObject projection = RepeatOf(candidate, repeat)["consumer"]!.AsObject();
            string[] names = projection.Select(property => property.Key).ToArray();
            Assert.Equal(["status", "reasons", "group_id", "kill_count", "offset_read", "counts", "membership"], names);
            Assert.Equal(Membership, Text(projection["membership"]));
        }
        Assert.Equal(["status", "reasons", "counts"], candidate["consumer"]!.AsObject().Select(property => property.Key).ToArray());
        using JsonDocument read = PhaseCandidateCodec.Read(bytes);
    }

    [Fact]
    public async Task Given_C03CandidateProjectionWithoutMembership_When_Read_Then_Rejects()
    {
        JsonObject root = await JudgedRoot();
        root["repeats"]![1]!["consumer"]!.AsObject().Remove("membership");
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Fact]
    public async Task Given_NonConsumerPhase_When_Judged_Then_CandidateCarriesNoConsumerProperty()
    {
        Stores stores = (await BaseB04.Value).Copy();
        (int exit, byte[] bytes) = await JudgeAsync(stores, "B04", "C0");
        JsonNode candidate = Parse(bytes);
        Assert.Equal(0, exit);
        Assert.False(candidate.AsObject().ContainsKey("consumer"));
        Assert.All(candidate["repeats"]!.AsArray(), repeat => Assert.False(repeat!.AsObject().ContainsKey("consumer")));
        using JsonDocument read = PhaseCandidateCodec.Read(bytes);
    }

    [Fact]
    public async Task Given_FiveAcceptedB04Repeats_When_JudgedWithAFixedClock_Then_CandidateBytesEqualTheBaseOutput()
    {
        (_, byte[] bytes) = await JudgeAsync((await BaseB04.Value).Copy(), "B04", "C0", new FixedWallClock());
        Assert.Equal(B04BaseCandidate.Text, Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Given_JudgedConsumerCandidate_When_Read_Then_TheCodecAccepts()
    {
        (_, byte[] bytes) = await JudgeAsync((await BaseC0.Value).Copy(), "C03", "C0");
        using JsonDocument read = PhaseCandidateCodec.Read(bytes);
        Assert.Equal("C03", read.RootElement.GetProperty("scenario_id").GetString());
    }

    private static async Task<JsonObject> JudgedRoot()
    {
        (_, byte[] bytes) = await JudgeAsync((await BaseC0.Value).Copy(), "C03", "C0");
        return Parse(bytes).AsObject();
    }

    [Theory]
    [InlineData("root-removed")]
    [InlineData("repeat-removed")]
    [InlineData("root-counts-with-incomplete")]
    [InlineData("root-extra-property")]
    public async Task Given_ConsumerCandidateWithoutItsShape_When_Read_Then_Rejects(string change)
    {
        JsonObject root = await JudgedRoot();
        switch (change)
        {
            case "root-removed": root.Remove("consumer"); break;
            case "repeat-removed": root["repeats"]![2]!.AsObject().Remove("consumer"); break;
            case "root-counts-with-incomplete": root["consumer"]!["status"] = "incomplete"; break;
            default: root["consumer"]!["x"] = 0; break;
        }
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Theory]
    [InlineData("root-object")]
    [InlineData("repeat-null")]
    public async Task Given_NonConsumerCandidateWithConsumerProperty_When_Read_Then_Rejects(string change)
    {
        JsonObject root = Parse(await Judge(await BaseB04.Value, "B04")).AsObject();
        if (change == "root-object") root["consumer"] = new JsonObject();
        else root["repeats"]![0]!["consumer"] = null;
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    private static async Task<byte[]> Judge(Stores stores, string scenario) => (await JudgeAsync(stores.Copy(), scenario, "C0")).Bytes;

    [Theory]
    [InlineData("C1", "C0")]
    [InlineData("C3", "C3")]
    [InlineData("C0", "C1")]
    public async Task Given_ConsumerCandidateWithAMismatchedPair_When_Read_Then_Rejects(string phase, string configuration)
    {
        JsonObject root = await JudgedRoot();
        root["phase"] = phase; root["configuration_id"] = configuration;
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    public static TheoryData<int> ShapeCases => new() { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

    [Theory]
    [MemberData(nameof(ShapeCases))]
    public async Task Given_ConsumerShapeBreak_When_Read_Then_Rejects(int number)
    {
        JsonObject root = await JudgedRoot();
        JsonNode repeat = root["repeats"]![1]!;
        switch (number)
        {
            case 1: repeat["consumer"]!["x"] = 0; break;
            case 2: repeat["consumer"]!["status"] = "done"; break;
            case 3: repeat["consumer"]!["offset_read"]!["outcome"] = "ok"; break;
            case 4: root["consumer"]!["status"] = "done"; break;
            case 5: root["consumer"] = JsonNode.Parse("""{"status":"incomplete","reasons":["b","a"]}"""); break;
            case 6: root["consumer"] = JsonNode.Parse("""{"status":"incomplete","reasons":["a","a"]}"""); break;
            case 7: root["consumer"] = JsonNode.Parse(ReasonObject(101)); break;
            case 8: root["consumer"]!.AsObject().Remove("counts"); break;
            default: root["consumer"]!["counts"]!["processed_once"] = -1; break;
        }
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Fact]
    public async Task Given_HundredConsumerReasons_When_Read_Then_TheCodecAccepts()
    {
        JsonObject root = await JudgedRoot();
        root["consumer"] = JsonNode.Parse(ReasonObject(100));
        using JsonDocument read = PhaseCandidateCodec.Read(Encoding.UTF8.GetBytes(root.ToJsonString()));
        Assert.Equal(100, read.RootElement.GetProperty("consumer").GetProperty("reasons").GetArrayLength());
    }

    private static string ReasonObject(int count) =>
        """{"status":"incomplete","reasons":[""" + string.Join(",", Enumerable.Range(0, count).Select(i => $"\"r{i:000}\"")) + "]}";

    private static PhaseJudgement Judgement(string status, string[] reasons, int failingRepeat = 0)
    {
        PhaseRepeatResult[] repeats = Enumerable.Range(1, 5).Select(i => new PhaseRepeatResult(i, "complete_valid", [],
            new PhaseRepeatChecks(true, i != failingRepeat, true, true, true, true, false), null, null, null)).ToArray();
        return new PhaseJudgement(status, null, null, repeats, reasons);
    }

    private static ConsumerProjection Projection(string status, string[] reasons, long once = 10, bool counts = true) =>
        new(status, reasons, counts ? new ConsumerCounts(once, 1, 2, 0) : null, default);

    [Fact]
    public void Given_TypedInputs_When_EvaluatedDirectly_Then_ProducesTheRowsOfTheJudgeTests()
    {
        ConsumerProjection ok = Projection("complete_valid", []);
        ConsumerProjection[] five = [ok, ok, ok, ok, ok];

        ConsumerPhaseResult complete = ConsumerPhaseJudge.Evaluate(Judgement("complete_valid", []), five);
        Assert.Equal(("complete_valid", "complete_valid"), (complete.Status, complete.RootStatus));
        Assert.Equal(new ConsumerCounts(50, 5, 10, 0), complete.Counts);

        ConsumerProjection deadline = Projection("incomplete", ["consumer_completion_deadline"], counts: false);
        ConsumerPhaseResult incomplete = ConsumerPhaseJudge.Evaluate(Judgement("complete_valid", []), [ok, ok, deadline, ok, ok]);
        Assert.Equal(("incomplete", "incomplete"), (incomplete.Status, incomplete.RootStatus));
        Assert.Equal("consumer_completion_deadline,j10_consumer_counts_unavailable", string.Join(',', incomplete.Reasons));
        Assert.Null(incomplete.Counts);

        ConsumerProjection invalid = Projection("invalid", ["consumer_record_invalid"], counts: false);
        Assert.Equal(("invalid", "invalid"), Pair(ConsumerPhaseJudge.Evaluate(Judgement("complete_valid", []), [ok, invalid, ok, ok, ok])));

        ConsumerProjection producerOnly = Projection("invalid", ["producer_side_not_complete_valid"], counts: false);
        Assert.Equal(("incomplete", "incomplete"), Pair(ConsumerPhaseJudge.Evaluate(Judgement("incomplete", ["j7_counts_unavailable"]), [ok, ok, ok, producerOnly, ok])));

        ConsumerProjection mixed = Projection("invalid", ["consumer_record_invalid", "producer_side_not_complete_valid"], counts: false);
        Assert.Equal(("invalid", "invalid"), Pair(ConsumerPhaseJudge.Evaluate(Judgement("incomplete", ["j7_counts_unavailable"]), [ok, ok, ok, mixed, ok])));

        ConsumerPhaseResult missing = ConsumerPhaseJudge.Evaluate(Judgement("incomplete", []), [ok, ok, ok, ok, null]);
        Assert.Equal(("incomplete", "incomplete"), (missing.Status, missing.RootStatus));

        ConsumerProjection big = Projection("complete_valid", [], long.MaxValue);
        ConsumerPhaseResult overflow = ConsumerPhaseJudge.Evaluate(Judgement("complete_valid", []), [big, big, ok, ok, ok]);
        Assert.Equal(("incomplete", "j10_consumer_counts_unavailable"), (overflow.Status, string.Join(',', overflow.Reasons)));

        ConsumerPhaseResult failedGate = ConsumerPhaseJudge.Evaluate(Judgement("incomplete", [], failingRepeat: 2), five);
        Assert.Equal("incomplete", failedGate.Status);
    }

    private static (string, string) Pair(ConsumerPhaseResult result) => (result.Status, result.RootStatus);

    internal sealed record Stores(Dictionary<string, byte[]> Verifier, Dictionary<string, byte[]> Ledger)
    {
        internal Stores Copy() => new(new(Verifier, StringComparer.Ordinal), new(Ledger, StringComparer.Ordinal));
    }

    private static JsonObject Reassignment(long? microseconds, string? reason) =>
        new() { ["microseconds"] = microseconds, ["unavailable_reason"] = reason };

    /// <summary>The fixture change that C03's membership rule forces (plan 7.1, option a): a block without <c>counts</c> carries no <c>membership</c> either.</summary>
    private static void RemoveCounts(JsonObject block) { block.Remove("counts"); block.Remove("membership"); }

    private const string Membership = """{"slow_partition":3,"slow_offset":10,"slow_processed_rows":2,"subject_lost_rows":1,"subject_revoked_rows":0,"witness_assigned_rows":2,"witness_assigned_slow_partition_during_slow":true,"witness_takeover_microseconds":61000000,"subject_commit_entries_after_lost":0}""";

    private static JsonObject DefaultBlock(int repeat, string phase = "C0", string scenario = "C03")
    {
        JsonObject block = DefaultBlockWithoutMembership(repeat, phase);
        if (scenario != "C03") return block;
        // Insert after counts, as the verifier writes it.
        var withMembership = new JsonObject();
        foreach (var property in block.ToList())
        {
            block.Remove(property.Key);
            withMembership[property.Key] = property.Value;
            if (property.Key == "counts") withMembership["membership"] = JsonNode.Parse(Membership);
        }
        return withMembership;
    }

    private static JsonObject DefaultBlockWithoutMembership(int repeat, string phase) => JsonNode.Parse($$$"""
        {"schema_version":1,"status":"complete_valid","reasons":[],"group_id":"{{{Run}}}-{{{phase.ToLowerInvariant()}}}-r{{{repeat}}}-consumers","kill_count":null,
         "members":[],"offset_read":{"outcome":"success","requested_partitions":[0,1],"started_utc":"2026-11-16T10:00:00.0000000Z","ended_utc":"2026-11-16T10:00:01.0000000Z","error_code":0,
         "offsets":[{"partition":0,"offset":7,"error_code":0},{"partition":1,"offset":9,"error_code":0}]},
         "partitions":[],"diagnostics":[],
         "counts":{"processed_once":10,"processed_again":1,"processed_again_extra_rows":2,"committed_never_processed":0}}
        """)!.AsObject();

    private static JsonObject DefaultProjection(int repeat, string phase = "C0", string scenario = "C03")
    {
        JsonObject block = DefaultBlock(repeat, phase, scenario);
        var projection = new JsonObject();
        foreach (string name in new[] { "status", "reasons", "group_id", "kill_count", "offset_read", "counts", "membership", "reassignment" })
            if (block.TryGetPropertyValue(name, out JsonNode? value)) projection[name] = value?.DeepClone();
        return projection;
    }

    private static async Task<Stores> BuildAsync(string scenario, string phase, bool addConsumer = true)
    {
        var verifier = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var ledger = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            using var fixture = new HostEvidenceBuilder(scenarioId: scenario, scenarioSourceId: "B04", phase: phase);
            fixture.ConfigureRepeat(repeat, phase);
            fixture.SetThrottleWindow(0);
            var completion = new VerifierCompletionState();
            int exit = await fixture.Verify(completion).ExecuteAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, exit);
            completion.MarkReturned(fixture.Options, exit, CancellationToken.None, CancellationToken.None);
            Assert.True(completion.TryGetBlobReceipt(out _, out string receiptName, out byte[] receiptBytes));
            await fixture.CreateAsync(receiptName, receiptBytes, CancellationToken.None);
            foreach (var item in fixture.VerifierObjects) verifier[item.Key] = item.Value;
            foreach (var item in fixture.LedgerObjects) ledger[item.Key] = item.Value;
        }
        var stores = new Stores(verifier, ledger);
        for (int repeat = 1; repeat <= 5 && addConsumer; repeat++)
            Apply(stores, phase, repeat, result => result["consumer"] = DefaultBlock(repeat, phase, scenario));
        return stores;
    }

    /// <summary>Changes one result, refreshes the receipt's reference to it and optionally sets the receipt's exit code.</summary>
    private static void Apply(Stores stores, string phase, int repeat, Action<JsonObject> change, int? exit = null)
    {
        string resultName = PhasePaths.Result(Run, phase, repeat);
        JsonObject result = JsonNode.Parse(stores.Verifier[resultName])!.AsObject();
        change(result);
        byte[] bytes = Encoding.UTF8.GetBytes(result.ToJsonString());
        stores.Verifier[resultName] = bytes;
        string receiptName = PhasePaths.VerifyCompletion(Run, phase, repeat);
        JsonObject receipt = JsonNode.Parse(stores.Verifier[receiptName])!.AsObject();
        JsonObject reference = receipt["artifacts"]![0]!.AsObject();
        reference["length"] = bytes.LongLength;
        reference["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (exit is int code) receipt["handler_exit_code"] = code;
        stores.Verifier[receiptName] = Encoding.UTF8.GetBytes(receipt.ToJsonString());
    }

    private static async Task<(int Exit, byte[] Bytes)> JudgeAsync(Stores stores, string scenario, string phase, TimeProvider? clock = null)
    {
        var verifier = new MemoryVerifierStore(stores.Verifier);
        var options = new VerifierHostOptions
        {
            Mode = "judge", ScenarioId = scenario, ConfigurationId = phase, RunId = Run, Phase = phase, SourceCommit = new string('a', 40),
            VerifierCommit = new string('a', 40), JobName = $"{Run}-{phase.ToLowerInvariant()}-judge", PodName = "judge-pod",
            VerifierContainer = "verifier", LedgerContainer = "ledger", CandidateOutputName = $"{Run}/{phase}/candidate.json",
            IoTimeout = TimeSpan.FromSeconds(1), MaxDiagnosticsPerCheck = 16, MaxOutputBytes = 16_777_216,
            LoadGenImage = "loadgen:tag@sha256:" + new string('b', 64), VerifierImage = "verifier:tag@sha256:" + new string('c', 64)
        };
        int exit = await new JudgeHandler(Options.Create(options), verifier, new MemoryProducerStore(stores.Ledger), clock ?? TimeProvider.System)
            .ExecuteAsync(TestContext.Current.CancellationToken);
        return (exit, verifier.Created!);
    }

    /// <summary>A clock whose wall time is fixed, so candidate bytes are reproducible. Timers and elapsed time stay real.</summary>
    internal sealed class FixedWallClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class MemoryVerifierStore(Dictionary<string, byte[]> objects) : IVerifierArtifactStore
    {
        public byte[]? Created { get; private set; }
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(objects.TryGetValue(name, out byte[]? value) ? value : null);
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) => Task.FromResult(new BlobListing(
            objects.Keys.Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray(), 1, true));
        public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken) { Created = content; objects[name] = content; return Task.CompletedTask; }
    }

    private sealed class MemoryProducerStore(Dictionary<string, byte[]> objects) : IProducerArtifactStore
    {
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) => Task.FromResult(new BlobListing(
            objects.Keys.Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray(), 1, true));
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(objects.TryGetValue(name, out byte[]? value) ? value : null);
    }
}
