using System.Text.Json.Nodes;
using Xunit;
using Parchwell.Verifier.Tests.Support;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
[Collection("B10 abrupt verifier")]
[Trait("Tier", "Docker")]
public sealed class B10AbruptVerifyTerminalOutcomeTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_AcceptedThenRejectedOriginalAndAnotherUnreportedOriginal_When_EitherIsConsumed_Then_OnlyUnrejectedOriginalEntersUnreportedCounts(bool unreportedSeen, bool rejectedSeen)
    {
        await kafka.ResetAsync();
        var evidence = new B10AbruptVerifyEvidence();
        evidence.Frames.RemoveAll(frame => Text(frame["kind"]) == "terminal" && Text(frame["data"]!["attempt_id"]) == "c3");
        JsonObject kill = evidence.Frames[^1];
        evidence.Frames.RemoveAt(evidence.Frames.Count - 1);
        evidence.Add("synchronous_rejection", new { attempt_id = "c4", prepared_sequence = 1, error_code = -1, error_reason = "synthetic" });
        evidence.Frames.Add(kill);
        evidence.Change("first/termination.json", root => root["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray("c3"));
        evidence.Change("successor/completion.json", root =>
        {
            root["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray("c3");
            root["data"]!["invocations"]![0]!["predecessor_attempt_id"] = "c3";
            root["data"]!["invocations"]![0]!["sequence"] = 3;
        });
        evidence.Change("successor/manifest.json", root =>
        {
            root["data"]!["inventory"]![0]!["predecessor_attempt_id"] = "c3";
            root["data"]!["inventory"]![0]!["sequence"] = 3;
        });
        Resequence(evidence);
        evidence.Rehash();
        using var run = new B10AbruptVerifyHarness(kafka.Bootstrap, evidence.Files);
        if (unreportedSeen) await run.ProduceAsync(0, 0, attempt: "c3", sequence: 3);
        if (rejectedSeen) await run.ProduceAsync(1, 0, attempt: "c4", sequence: 4);

        await AssertPendingSweepAsync(run);
        JsonNode cross = Arm(run, "cross_restart");
        Assert.Equal(1, Number(cross, "unconfirmed_count"));
        JsonNode member = Assert.Single(cross["members"]!.AsArray())!;
        Assert.Equal("c3", Text(member["original_attempt_id"]));
        Assert.Equal("r4", Text(member["resend_attempt_id"]));
        Assert.Equal(1, Number(cross, "resend_invoked"));
        Assert.Equal(unreportedSeen ? 1 : 0, Number(cross, "originals_seen"));
        Assert.Equal(0, Number(cross, "resends_seen"));
        Assert.Equal(0, Number(cross, "pair_count"));
        Assert.Equal(1, Number(cross, "unreported_attempts"));
        Assert.Equal(unreportedSeen ? 1 : 0, Number(cross, "unreported_seen"));
        Assert.Equal(unreportedSeen ? 0 : 1, Number(cross, "unreported_unseen"));
        AssertNoUnreported(Arm(run, "same_instance"));
    }

    [Theory]
    [InlineData("s4", "before")]
    [InlineData("t4", "before")]
    [InlineData("s4", "after")]
    [InlineData("t4", "after")]
    [InlineData("s4", "missing")]
    [InlineData("t4", "missing")]
    public async Task Given_AcceptedControlAttemptWithoutCallbackTerminal_When_RejectionPrecedesOrFollowsCrossWarmup_Then_ControlCompletionUsesTerminalOutcomeAndJournalOrder(string attempt, string rejection)
    {
        await kafka.ResetAsync();
        var evidence = new B10AbruptVerifyEvidence();
        JsonObject terminal = evidence.Frames.Single(frame => Text(frame["kind"]) == "terminal" && Text(frame["data"]!["attempt_id"]) == attempt);
        evidence.Frames.Remove(terminal);
        if (rejection != "missing")
        {
            evidence.Add("synchronous_rejection", new { attempt_id = attempt, prepared_sequence = 1, error_code = -1, error_reason = "synthetic" });
            JsonObject rejected = evidence.Frames[^1];
            evidence.Frames.RemoveAt(evidence.Frames.Count - 1);
            int index = evidence.Frames.FindIndex(frame =>
                Text(frame["kind"]) == "warmup_prepared" && Text(frame["data"]!["arm"]) == "cross_restart");
            if (rejection == "after") index++;
            evidence.Frames.Insert(index, rejected);
        }
        Resequence(evidence);
        evidence.Rehash();
        using var run = new B10AbruptVerifyHarness(kafka.Bootstrap, evidence.Files);

        if (rejection == "before")
        {
            await AssertPendingSweepAsync(run);
            JsonNode same = Arm(run, "same_instance");
            AssertNoUnreported(same);
            Assert.Equal(1, Number(same, "unconfirmed_count"));
            Assert.Equal(1, Number(same, "resend_invoked"));
            JsonNode member = Assert.Single(same["members"]!.AsArray())!;
            Assert.Equal("s4", Text(member["original_attempt_id"]));
            Assert.Equal("t4", Text(member["resend_attempt_id"]));
            if (attempt == "t4")
            {
                Assert.Null(member["resend_terminal"]);
                Assert.Null(member["resend_lost"]);
                Assert.Null(same["lost_resends"]);
            }
            else Assert.True(member["resend_terminal"]!["delivered"]!.GetValue<bool>());
        }
        else
        {
            Assert.Equal(0, await run.RunAsync());
            Assert.Equal("b10_abrupt_verify_first_evidence_invalid", Text(Assert.Single(run.Data["problems"]!.AsArray())));
            JsonArray reasons = run.Data["eligibility_reasons"]!.AsArray();
            Assert.Equal(rejection == "missing" ? 2 : 1, reasons.Count);
            Assert.Equal("b10_restart_identity_unauthenticated", Text(reasons[0]));
            if (rejection == "missing") Assert.Equal("b10_abrupt_control_incomplete", Text(reasons[1]));
            Assert.Equal("ineligible", Text(run.Data["eligibility"]));
            Assert.Equal("unavailable", Text(run.Data["comparison"]));
            Assert.Null(run.Data["arms"]);
            Assert.NotNull(run.Data["timeout"]);
        }
    }

    private static async Task AssertPendingSweepAsync(B10AbruptVerifyHarness run)
    {
        Assert.Equal(0, await run.RunAsync());
        Assert.Empty(run.Data["problems"]!.AsArray());
        Assert.Empty(run.Data["eligibility_reasons"]!.AsArray());
        Assert.Equal("pending_sweep", Text(run.Data["eligibility"]));
        Assert.Equal("neither", Text(run.Data["comparison"]));
        Assert.True(run.Data["window_complete"]!.GetValue<bool>());
    }

    private static void AssertNoUnreported(JsonNode arm)
    {
        Assert.Equal(0, Number(arm, "unreported_attempts"));
        Assert.Equal(0, Number(arm, "unreported_seen"));
        Assert.Equal(0, Number(arm, "unreported_unseen"));
    }

    private static JsonNode Arm(B10AbruptVerifyHarness run, string arm) => run.Data["arms"]!.AsArray().Single(node => Text(node!["arm"]) == arm)!;
    private static string? Text(JsonNode? node) => node?.GetValue<string>();
    private static long Number(JsonNode node, string field) => node[field]!.GetValue<long>();

    private static void Resequence(B10AbruptVerifyEvidence evidence)
    {
        var prepared = new Dictionary<string, long>(StringComparer.Ordinal);
        for (int index = 0; index < evidence.Frames.Count; index++)
        {
            JsonObject frame = evidence.Frames[index];
            frame["sequence"] = index + 1L;
            string? kind = Text(frame["kind"]);
            JsonObject data = frame["data"]!.AsObject();
            string? id = Text(data["attempt_id"]);
            if (kind == "prepared") prepared[id!] = index + 1L;
            else if (kind is "accepted" or "synchronous_rejection" && id is not null && prepared.TryGetValue(id, out long sequence)) data["prepared_sequence"] = sequence;
        }
    }
}
#pragma warning restore CA1707
