using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Phase;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
[Trait("Tier", "Docker")]
public sealed class B10FinalSweepHandlerTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    private static readonly int[] RepeatTwo = [2];
    private static readonly int[] RepeatFour = [4];
    private static readonly string[] SweepUnavailableReasons = ["b10_restart_sweep_unavailable"];
    private static readonly string[] StartAndSweepReasons = ["b10_restart_configuration_invalid", "b10_restart_sweep_unavailable"];
    private static readonly string[] EmptyKillReasons = ["kill_too_late"];
    private static readonly string[] EmptyKillIdentityReasons = ["b10_restart_identity_unauthenticated", "kill_too_late"];
    [Fact]
    public async Task Given_FiveAuthenticatedScientificRepeats_When_FinalSweepRuns_Then_BothArmsAreEligibleAndFinal_T1()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        Assert.Equal(0, await run.RunAsync());
        Assert.True(run.Data["scan_complete"]!.GetValue<bool>(), run.Data.ToJsonString());
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            JsonObject item = run.Repeat(repeat); AssertComplete(item);
            Assert.Equal("eligible", Text(item["restart"]!["eligibility"]));
            Assert.Equal("both", Text(item["restart"]!["comparison"]));
            Assert.Equal(1, Arm(item, "cross_restart")["pair_count"]!.GetValue<long>());
            Assert.Equal(0, Arm(item, "cross_restart")["lost_resends"]!.GetValue<long>());
            Assert.Equal(0, item["timeout"]!["duplicate"]!.GetValue<long>());
        }
        string output = PhasePaths.B10FinalSweep(B10FinalSweepHarness.Run, "C1");
        Assert.Contains(output, run.Verifier.Keys);
        Assert.Contains(PhasePaths.B10FinalSweepCompletion(B10FinalSweepHarness.Run, "C1"), run.Verifier.Keys);
        Assert.Equal(0, await run.JudgeAsync());
        JsonNode rule = run.Candidate["b10_final"]!["restart_rule"]!;
        Assert.Equal(5, rule["eligible_repeats"]!.GetValue<int>());
        Assert.Equal(Enumerable.Range(1, 5), rule["qualifying_pair_repeats"]!.AsArray().Select(item => item!.GetValue<int>()));
        Assert.Equal("confirmed", Text(rule["rule_selection"])); Assert.Equal(Parchwell.Verifier.Tests.Judge.B10FinalJudgeFixture.ExpectedRestartSubject, Text(rule["subject"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_EndLineAdvancedOrResendAbsent_When_FinalSweepRuns_Then_UnionResolvesTheResendLoss_T2_T3(bool lost)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka, lateRepeat: lost ? 0 : 2, lostRepeat: lost ? 2 : 0);
        Assert.Equal(0, await run.RunAsync()); JsonObject item = run.Repeat(2); AssertComplete(item);
        Assert.Equal("eligible", Text(item["restart"]!["eligibility"]));
        JsonObject arm = Arm(item, "cross_restart");
        Assert.Equal(lost ? 0 : 1, arm["resends_seen"]!.GetValue<long>());
        Assert.Equal(lost ? 0 : 1, arm["pair_count"]!.GetValue<long>());
        Assert.Equal(lost ? 1 : 0, arm["lost_resends"]!.GetValue<long>());
        Assert.Equal(0, await run.JudgeAsync());
        JsonNode rule = run.Candidate["b10_final"]!["restart_rule"]!;
        Assert.Equal("confirmed", Text(rule["rule_selection"]));
        Assert.Equal(lost ? RepeatTwo : [], rule["lost_resend_finding_repeats"]!.AsArray().Select(item => item!.GetValue<int>()));
        Assert.Equal(lost ? RepeatTwo : [], rule["eligible_loss_repeats"]!.AsArray().Select(item => item!.GetValue<int>()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_ResendBelowAtOrAboveSweepHigh_When_FinalSweepRuns_Then_OnlyBelowHighEntersTheUnion_T4(int relativeOffset)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka, lostRepeat: 2);
        long boundary = SweepHigh(run) + 1;
        for (int padding = -1; padding < relativeOffset; padding++)
            await run.AddRecordAsync(1, B10FinalSweepHarness.Timeout(1), "timeout0");
        await run.AddRecordAsync(2, B10FinalSweepHarness.Second(2), "r4", "cross_restart", 4);
        run.CaptureSweep(boundary);
        Assert.Equal(0, await run.RunAsync());
        Assert.Equal(relativeOffset < 0 ? 0 : 1, Arm(run.Repeat(2), "cross_restart")["lost_resends"]!.GetValue<long>());
    }

    [Fact]
    public async Task Given_EarlierTimeoutOwnerInLaterOriginalWindow_When_FinalSweepRuns_Then_OnlyItsOwnRepeatCountsIt_T5()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka, beforeVerify: (repeat, host, high) => repeat == 3
            ? host.ProduceAsync(0, high, instance: B10FinalSweepHarness.Timeout(2), attempt: "timeout0", sequence: 0, key: "timeout")
            : Task.CompletedTask);
        Assert.Equal(0, await run.RunAsync());
        Assert.Equal(1, run.Repeat(2)["timeout"]!["duplicate"]!.GetValue<long>());
        Assert.Equal(0, run.Repeat(3)["timeout"]!["duplicate"]!.GetValue<long>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_UnjoinedOwnedLateRecord_When_FinalSweepRuns_Then_TimeoutAndRestartFailIndependently_T7(bool timeout)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        await run.AddRecordAsync(1, timeout ? B10FinalSweepHarness.Timeout(1) : B10FinalSweepHarness.First(1), "x"); run.CaptureSweep();
        Assert.Equal(0, await run.RunAsync()); JsonObject item = run.Repeat(1);
        if (timeout)
        {
            Assert.Equal("invalid", Text(item["status"])); Assert.Null(item["timeout"]);
            Assert.Equal(["b10_final_late_record_unjoined"], Strings(item["reasons"]));
            Assert.Equal("eligible", Text(item["restart"]!["eligibility"]));
        }
        else
        {
            AssertComplete(item);
            Assert.Equal(["b10_restart_late_record_unjoined"], Strings(item["restart"]!["eligibility_reasons"]));
            AssertUnknownSweepCounts(item);
        }
        for (int repeat = 2; repeat <= 5; repeat++) AssertComplete(run.Repeat(repeat));
    }

    [Theory]
    [InlineData("missing", "b10_final_sweep_line_invalid")]
    [InlineData("future", "b10_final_sweep_line_invalid")]
    [InlineData("malformed", "b10_final_sweep_line_invalid")]
    [InlineData("wrong-run", "b10_final_sweep_line_invalid")]
    [InlineData("budget", "b10_final_drain_incomplete")]
    public async Task Given_UnavailableSweep_When_FinalSweepRuns_Then_EveryRepeatIsNonfinalWithNullTimeout_T8(string defect, string reason)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        if (defect == "budget") run.Configuration["Verifier:MaxConsumedRecords"] = "1";
        else if (defect == "missing") run.Verifier.Remove(PhasePaths.SweepLine(B10FinalSweepHarness.Run, "C1"));
        else if (defect == "malformed") run.Verifier[PhasePaths.SweepLine(B10FinalSweepHarness.Run, "C1")] = [123];
        else Change(run.Verifier, PhasePaths.SweepLine(B10FinalSweepHarness.Run, "C1"), root =>
        { if (defect == "wrong-run") root["run_id"] = "b10-2-1"; else root["captured_at"] = "2099-01-01T00:00:00.0000000+00:00"; });
        Assert.Equal(0, await run.RunAsync()); AssertGlobal(run, reason);
        Assert.Equal(1, await run.JudgeAsync()); Assert.Equal("invalid", Text(run.Candidate["status"]));
        JsonNode rule = run.Candidate["b10_final"]!["restart_rule"]!;
        Assert.Equal("held", Text(rule["rule_selection"])); Assert.Equal("fewer_than_three_eligible", Text(rule["selection_reason"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_OriginalWindowDisagreement_When_FinalSweepRuns_Then_OnlyTheAffectedCohortIsRejected_T9(bool timeout)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.RewriteA3(3, root =>
        {
            if (timeout) root["data"]!["timeout"]!["duplicate"] = 1;
            else root["data"]!["arms"]![0]!["members"]![0]!["original_copies"] = 2;
        });
        Assert.Equal(0, await run.RunAsync()); JsonObject item = run.Repeat(3);
        if (timeout)
        {
            Assert.Equal("invalid", Text(item["status"])); Assert.Null(item["timeout"]);
            Assert.Equal(["b10_final_window_mismatch"], Strings(item["reasons"]));
            Assert.Equal("eligible", Text(item["restart"]!["eligibility"]));
        }
        else { AssertComplete(item); Assert.Equal(["b10_restart_late_record_unjoined"], Strings(item["restart"]!["eligibility_reasons"])); AssertUnknownSweepCounts(item); }
    }

    [Fact]
    public async Task Given_NoKillWithoutPerRepeatVerifier_When_FinalSweepRuns_Then_TimeoutRemainsFinalAndRestartPublishesTheOutcome_T10()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        NoKill(run, 5);
        Assert.Equal(0, await run.RunAsync()); JsonObject item = run.Repeat(5); AssertComplete(item);
        Assert.Equal("no_kill", Text(item["restart"]!["termination"]!["class"]));
        Assert.Equal("kill_too_early", Text(item["restart"]!["termination"]!["outcome"]));
        Assert.Equal(["kill_too_early"], Strings(item["restart"]!["eligibility_reasons"]));
        Assert.Null(item["restart"]!["arms"]); Assert.Equal("unavailable", Text(item["restart"]!["comparison"]));
        Assert.Equal(0, await run.JudgeAsync());
        JsonNode rule = run.Candidate["b10_final"]!["restart_rule"]!;
        Assert.Equal(4, rule["eligible_repeats"]!.GetValue<int>());
        Assert.DoesNotContain(5, rule["lost_resend_finding_repeats"]!.AsArray().Select(value => value!.GetValue<int>()));
    }

    [Fact]
    public async Task Given_CollidingInstanceAcrossRepeats_When_FinalSweepRuns_Then_OwnershipFailureInvalidatesTheWholePhase_T16()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.ReplaceTimeoutInstance(2, 1);
        Assert.Equal(0, await run.RunAsync()); AssertGlobal(run, "b10_final_ownership_invalid");
    }

    [Theory]
    [InlineData(0, "pair_count", 1)]
    [InlineData(0, "pair_count", -1)]
    [InlineData(0, "originals_seen", 1)]
    [InlineData(0, "originals_seen", -1)]
    [InlineData(0, "resends_seen", 1)]
    [InlineData(0, "resends_seen", -1)]
    [InlineData(0, "unreported_seen", 1)]
    [InlineData(0, "unreported_unseen", 1)]
    [InlineData(0, "lost_resends", 1)]
    [InlineData(1, "pair_count", 1)]
    [InlineData(1, "pair_count", -1)]
    [InlineData(1, "originals_seen", 1)]
    [InlineData(1, "originals_seen", -1)]
    [InlineData(1, "resends_seen", 1)]
    [InlineData(1, "resends_seen", -1)]
    [InlineData(1, "unreported_seen", 1)]
    [InlineData(1, "unreported_seen", -1)]
    [InlineData(1, "unreported_unseen", 1)]
    [InlineData(1, "lost_resends", 1)]
    public async Task Given_NonfinalAggregateArmCountDisagrees_When_FinalSweepRuns_Then_OnlyRestartIsRejected_P1_4567914709(int arm, string field, int delta)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.RewriteA3(3, root => root["data"]!["arms"]![arm]![field] = root["data"]!["arms"]![arm]![field]!.GetValue<long>() + delta);
        Assert.Equal(0, await run.RunAsync());
        AssertArmDisagreement(run.Repeat(3));
    }

    [Theory]
    [InlineData(0, "pair_count", 1)]
    [InlineData(0, "pair_count", -1)]
    [InlineData(0, "copy_count", 1)]
    [InlineData(0, "copy_count", -1)]
    [InlineData(0, "original_copies", 1)]
    [InlineData(0, "original_copies", -1)]
    [InlineData(0, "resend_copies", 1)]
    [InlineData(0, "resend_copies", -1)]
    [InlineData(0, "other_copies", 1)]
    [InlineData(1, "pair_count", 1)]
    [InlineData(1, "pair_count", -1)]
    [InlineData(1, "copy_count", 1)]
    [InlineData(1, "copy_count", -1)]
    [InlineData(1, "original_copies", 1)]
    [InlineData(1, "original_copies", -1)]
    [InlineData(1, "resend_copies", 1)]
    [InlineData(1, "resend_copies", -1)]
    [InlineData(1, "other_copies", 1)]
    public async Task Given_NonfinalMemberCountDisagrees_When_FinalSweepRuns_Then_OnlyRestartIsRejected_P1_4567914709(int arm, string field, int delta)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.RewriteA3(3, root =>
        {
            JsonNode member = root["data"]!["arms"]![arm]!["members"]![0]!;
            member[field] = member[field]!.GetValue<long>() + delta;
        });
        Assert.Equal(0, await run.RunAsync());
        AssertArmDisagreement(run.Repeat(3));
    }

    [Theory]
    [InlineData(0, "copies_truncated")]
    [InlineData(0, "resend_lost")]
    [InlineData(1, "copies_truncated")]
    [InlineData(1, "resend_lost")]
    public async Task Given_NonfinalMemberFlagDisagrees_When_FinalSweepRuns_Then_OnlyRestartIsRejected_P1_4567914709(int arm, string field)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.RewriteA3(3, root => root["data"]!["arms"]![arm]!["members"]![0]![field] = true);
        Assert.Equal(0, await run.RunAsync());
        AssertArmDisagreement(run.Repeat(3));
    }

    [Theory]
    [InlineData(0, "witness_original_offset_up")]
    [InlineData(0, "witness_original_offset_down")]
    [InlineData(0, "witness_resend_offset_up")]
    [InlineData(0, "witness_resend_offset_down")]
    [InlineData(0, "witness_original_attempt")]
    [InlineData(0, "witness_resend_attempt")]
    [InlineData(0, "witness_original_partition")]
    [InlineData(0, "witness_resend_partition")]
    [InlineData(0, "copy_offset_up")]
    [InlineData(0, "copy_offset_down")]
    [InlineData(0, "copy_partition")]
    [InlineData(0, "copy_attempt")]
    [InlineData(0, "copy_instance")]
    [InlineData(0, "copy_role")]
    [InlineData(0, "copy_removed")]
    [InlineData(0, "copy_added")]
    [InlineData(1, "witness_original_offset_up")]
    [InlineData(1, "witness_original_offset_down")]
    [InlineData(1, "witness_resend_offset_up")]
    [InlineData(1, "witness_resend_offset_down")]
    [InlineData(1, "witness_original_attempt")]
    [InlineData(1, "witness_resend_attempt")]
    [InlineData(1, "witness_original_partition")]
    [InlineData(1, "witness_resend_partition")]
    [InlineData(1, "copy_offset_up")]
    [InlineData(1, "copy_offset_down")]
    [InlineData(1, "copy_partition")]
    [InlineData(1, "copy_attempt")]
    [InlineData(1, "copy_instance")]
    [InlineData(1, "copy_role")]
    [InlineData(1, "copy_removed")]
    [InlineData(1, "copy_added")]
    public async Task Given_NonfinalWitnessOrCopiesDisagree_When_FinalSweepRuns_Then_OnlyRestartIsRejected_P1_4567914709(int arm, string change)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.RewriteA3(3, root =>
        {
            JsonNode node = root["data"]!["arms"]![arm]!;
            JsonNode member = node["members"]![0]!;
            JsonNode witness = node["witness"]!;
            JsonArray copies = member["copies"]!.AsArray();
            switch (change)
            {
                case "witness_original_offset_up": witness["original"]!["offset"] = witness["original"]!["offset"]!.GetValue<long>() + 1; break;
                case "witness_original_offset_down": witness["original"]!["offset"] = witness["original"]!["offset"]!.GetValue<long>() - 1; break;
                case "witness_resend_offset_up": witness["resend"]!["offset"] = witness["resend"]!["offset"]!.GetValue<long>() + 1; break;
                case "witness_resend_offset_down": witness["resend"]!["offset"] = witness["resend"]!["offset"]!.GetValue<long>() - 1; break;
                case "witness_original_attempt": witness["original_attempt_id"] = "other"; break;
                case "witness_resend_attempt": witness["resend_attempt_id"] = "other"; break;
                case "witness_original_partition": witness["original"]!["partition"] = witness["original"]!["partition"]!.GetValue<int>() + 1; break;
                case "witness_resend_partition": witness["resend"]!["partition"] = witness["resend"]!["partition"]!.GetValue<int>() + 1; break;
                case "copy_offset_up": copies[0]!["offset"] = copies[0]!["offset"]!.GetValue<long>() + 1; break;
                case "copy_offset_down": copies[0]!["offset"] = copies[0]!["offset"]!.GetValue<long>() - 1; break;
                case "copy_partition": copies[0]!["partition"] = copies[0]!["partition"]!.GetValue<int>() + 1; break;
                case "copy_attempt": copies[1]!["attempt_id"] = "other"; break;
                case "copy_instance": copies[1]!["instance_id"] = "other"; break;
                case "copy_role": copies[1]!["role"] = "other"; break;
                case "copy_removed": copies.RemoveAt(1); break;
                case "copy_added": copies.Add(JsonNode.Parse(copies[1]!.ToJsonString())); break;
                default: throw new InvalidOperationException(change);
            }
        });
        Assert.Equal(0, await run.RunAsync());
        AssertArmDisagreement(run.Repeat(3));
    }

    [Theory]
    [InlineData("timeout_ok_string")]
    [InlineData("timeout_ok_negative")]
    [InlineData("timeout_ok_fraction")]
    [InlineData("timeout_loss_bool")]
    [InlineData("timeout_duplicate_string")]
    [InlineData("timeout_not_object")]
    [InlineData("timeout_array")]
    [InlineData("timeout_empty")]
    [InlineData("timeout_missing_member")]
    [InlineData("timeout_extra_member")]
    [InlineData("timeout_null_member")]
    [InlineData("reasons_unknown")]
    [InlineData("reasons_empty_text")]
    [InlineData("reasons_not_array")]
    [InlineData("reasons_unknown_with_wrong_timeout")]
    [InlineData("reasons_projection_only_sweep_unavailable")]
    [InlineData("reasons_projection_only_late_record")]
    [InlineData("reasons_projection_only_input_unavailable")]
    [InlineData("problems_number")]
    [InlineData("problems_item_number")]
    [InlineData("problems_object")]
    public async Task Given_NonfinalTimeoutOrProblemsAreMalformed_When_FinalSweepRuns_Then_SweepPublishesAndOnlyRestartIsRejected_P1_Codex(string change)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.RewriteA3(3, root =>
        {
            JsonNode data = root["data"]!;
            switch (change)
            {
                case "timeout_ok_string": data["timeout"]!["ok"] = "1"; break;
                case "timeout_ok_negative": data["timeout"]!["ok"] = -1; break;
                case "timeout_ok_fraction": data["timeout"]!["ok"] = 1.5; break;
                case "timeout_loss_bool": data["timeout"]!["loss"] = true; break;
                case "timeout_duplicate_string": data["timeout"]!["duplicate"] = "0"; break;
                case "timeout_not_object": data["timeout"] = "none"; break;
                case "timeout_array": data["timeout"] = new JsonArray(1); break;
                case "timeout_empty": data["timeout"] = new JsonObject(); break;
                case "timeout_missing_member": data["timeout"]!.AsObject().Remove("reorder"); break;
                case "timeout_extra_member": data["timeout"]!["unexpected"] = 0; break;
                case "timeout_null_member": data["timeout"]!["phantom"] = null; break;
                case "reasons_unknown": data["eligibility"] = "ineligible"; data["eligibility_reasons"] = new JsonArray("bogus"); break;
                case "reasons_empty_text": data["eligibility"] = "ineligible"; data["eligibility_reasons"] = new JsonArray(""); break;
                case "reasons_unknown_with_wrong_timeout": data["timeout"]!["duplicate"] = 1; data["eligibility"] = "ineligible"; data["eligibility_reasons"] = new JsonArray("bogus"); break;
                case "reasons_projection_only_sweep_unavailable": data["eligibility"] = "ineligible"; data["eligibility_reasons"] = new JsonArray("b10_restart_sweep_unavailable"); break;
                case "reasons_projection_only_late_record": data["eligibility"] = "ineligible"; data["eligibility_reasons"] = new JsonArray("b10_restart_late_record_unjoined"); break;
                case "reasons_projection_only_input_unavailable": data["eligibility"] = "ineligible"; data["eligibility_reasons"] = new JsonArray("b10_restart_input_unavailable"); break;
                case "reasons_not_array": data["eligibility_reasons"] = "b10_restart_window_incomplete"; break;
                case "problems_number": data["problems"] = 5; break;
                case "problems_item_number": data["problems"] = new JsonArray(5); break;
                case "problems_object": data["problems"] = new JsonObject(); break;
                default: throw new InvalidOperationException(change);
            }
        });
        Assert.Equal(0, await run.RunAsync());
        Assert.True(run.Data["scan_complete"]!.GetValue<bool>(), run.Data.ToJsonString());
        for (int repeat = 1; repeat <= 5; repeat++) AssertComplete(run.Repeat(repeat));
        JsonObject item = run.Repeat(3);
        Assert.Equal("ineligible", Text(item["restart"]!["eligibility"]));
        Assert.Equal(["b10_restart_input_unavailable"], Strings(item["restart"]!["eligibility_reasons"]));
        Assert.Null(item["restart"]!["arms"]);
    }

    [Theory]
    [InlineData("object", "ordinary")]
    [InlineData("receipt", "ordinary")]
    [InlineData("object", "no_kill")]
    [InlineData("receipt", "no_kill")]
    public async Task Given_NonfinalReadFails_When_FinalSweepRuns_Then_TimeoutCohortsSurviveAndOnlyRestartIsRejected_P1_Codex_ReadFailure(string which, string termination)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        if (termination == "no_kill") NoKill(run, 3);
        string target = which == "object" ? B10FinalSweepHarness.A3(3) : B10FinalSweepHarness.Prefix(3) + "verify-completion.json";
        var reads = new List<string>();
        run.BeforeRead = name => { reads.Add(name); if (name == target) throw new IOException("synthetic storage failure"); };
        Assert.Equal(0, await run.RunAsync());
        // A failed read of one object is not evidence that the other is absent, so each is still attempted.
        Assert.Contains(B10FinalSweepHarness.A3(3), reads);
        Assert.Contains(B10FinalSweepHarness.Prefix(3) + "verify-completion.json", reads);
        Assert.True(run.Data["scan_complete"]!.GetValue<bool>(), run.Data.ToJsonString());
        for (int repeat = 1; repeat <= 5; repeat++) AssertComplete(run.Repeat(repeat));
        JsonObject item = run.Repeat(3);
        Assert.Equal("ineligible", Text(item["restart"]!["eligibility"]));
        Assert.Equal(["b10_restart_input_unavailable"], Strings(item["restart"]!["eligibility_reasons"]));
        Assert.Null(item["restart"]!["arms"]);
        foreach (int repeat in new[] { 1, 2, 4, 5 }) Assert.Equal("eligible", Text(run.Repeat(repeat)["restart"]!["eligibility"]));
    }

    [Theory]
    [InlineData("below")]
    [InlineData("above")]
    public async Task Given_NonfinalStartDisagreesWithAuthenticatedStartNote_When_FinalSweepRuns_Then_RejectedObjectContributesNothing_P1_4567914700(string side)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        long previousEnd = JsonNode.Parse(run.Verifier[B10FinalSweepHarness.A3(2)])!["data"]!["end_line"]![0]!["offset"]!.GetValue<long>();
        long start = JsonNode.Parse(run.Verifier[B10FinalSweepHarness.A3(3)])!["data"]!["start_line"]![0]!["offset"]!.GetValue<long>();
        Assert.Equal(previousEnd, start);
        run.RewriteA3(3, root =>
        {
            root["data"]!["start_line"]![0]!["offset"] = side == "below" ? start - 1 : start + 1;
            // Everything else the rejected object carries must be ignored, not only its vectors.
            root["data"]!["timeout"]!["duplicate"] = 1;
            root["data"]!["eligibility"] = "ineligible";
            root["data"]!["eligibility_reasons"] = new JsonArray("b10_restart_window_incomplete");
        });
        Assert.Equal(0, await run.RunAsync());
        Assert.True(run.Data["scan_complete"]!.GetValue<bool>(), run.Data.ToJsonString());
        for (int repeat = 1; repeat <= 5; repeat++) AssertComplete(run.Repeat(repeat));
        JsonObject item = run.Repeat(3);
        Assert.Equal("ineligible", Text(item["restart"]!["eligibility"]));
        Assert.Equal(["b10_restart_input_unavailable"], Strings(item["restart"]!["eligibility_reasons"]));
        Assert.Null(item["restart"]!["arms"]);
        foreach (int repeat in new[] { 1, 2, 4, 5 }) Assert.Equal("eligible", Text(run.Repeat(repeat)["restart"]!["eligibility"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_OverlappingRepeatVectors_When_FinalSweepRuns_Then_NoRepeatCanBeFinal_T21_T29(bool changeEnd)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        long next = JsonNode.Parse(run.Verifier[B10FinalSweepHarness.A3(2)])!["data"]!["start_line"]![0]!["offset"]!.GetValue<long>();
        if (changeEnd) run.RewriteA3(1, root => root["data"]!["end_line"]![0]!["offset"] = next + 1);
        else
        {
            run.RewriteA3(2, root => root["data"]!["start_line"]![0]!["offset"] = next - 1);
            run.RewriteStartHigh(2, next - 1);
        }
        Assert.Equal(0, await run.RunAsync()); AssertGlobal(run, "b10_final_vector_invalid");
    }

    [Fact]
    public async Task Given_ReauthenticatedIdentityMismatch_When_FinalSweepRuns_Then_TimeoutSurvivesAndRestartIsUnavailable_T25()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.RewriteA3(2, root => root["data"]!["first_instance_id"] = "other-pod");
        Assert.Equal(0, await run.RunAsync()); JsonObject item = run.Repeat(2); AssertComplete(item);
        Assert.Equal(["b10_restart_input_unavailable"], Strings(item["restart"]!["eligibility_reasons"]));
        Assert.Null(item["restart"]!["arms"]); Assert.Equal("unavailable", Text(item["restart"]!["comparison"]));
        foreach (int repeat in new[] { 1, 3, 4, 5 }) AssertComplete(run.Repeat(repeat));
    }

    [Theory]
    [InlineData("unexpected_exit", "b10_abrupt_unexpected_exit", "b10_restart_termination_unverified")]
    [InlineData("unknown", "b10_abrupt_exit_unobserved", "b10_restart_termination_unverified")]
    [InlineData("missing", null, "b10_restart_first_evidence_unavailable")]
    public async Task Given_UnverifiedTermination_When_FinalSweepRuns_Then_RestartReasonsDoNotInvalidateTimeout_T27(string kind, string? outcome, string reason)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.RemoveA3(3); string path = B10FinalSweepHarness.Prefix(3) + B10FinalSweepHarness.First(3) + "/termination.json";
        if (kind == "missing") run.Ledger.Remove(path);
        else Change(run.Ledger, path, root => { root["data"]!["termination"] = kind; root["data"]!["outcome"] = outcome; root["data"]!["successor_permitted"] = false; });
        Assert.Equal(0, await run.RunAsync()); AssertComplete(run.Repeat(3));
        Assert.Equal([reason], Strings(run.Repeat(3)["restart"]!["eligibility_reasons"]));
    }

    [Fact]
    public async Task Given_NonemptyIntendedKillWithCaptureGap_When_FinalSweepRuns_Then_E4AndE17BlockOnlyRestart_T27()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka); run.RemoveA3(3);
        string path = B10FinalSweepHarness.Prefix(3) + B10FinalSweepHarness.First(3) + "/termination.json";
        Change(run.Ledger, path, root => { root["data"]!["capture_gaps"] = new JsonArray("synthetic_gap"); root["data"]!["successor_permitted"] = false; });
        Assert.Equal(0, await run.RunAsync()); AssertComplete(run.Repeat(3));
        Assert.Equal(["b10_restart_termination_unverified", "b10_restart_input_unavailable"], Strings(run.Repeat(3)["restart"]!["eligibility_reasons"]));
        Assert.Null(run.Repeat(3)["restart"]!["arms"]);
    }

    [Fact]
    public async Task Given_PhaseStopsAfterThirdRepeat_When_FinalSweepRuns_Then_UnstartedSlotsAreExplicitlyIncomplete_T28()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka, stopAfter: 3);
        Assert.Equal(0, await run.RunAsync()); Assert.True(run.Data["scan_complete"]!.GetValue<bool>(), run.Data.ToJsonString());
        for (int repeat = 4; repeat <= 5; repeat++)
        {
            Assert.Equal("incomplete", Text(run.Repeat(repeat)["status"]));
            Assert.Equal(["b10_final_repeat_not_started"], Strings(run.Repeat(repeat)["reasons"]));
            Assert.Null(run.Repeat(repeat)["timeout"]);
            Assert.Equal(["b10_restart_input_unavailable"], Strings(run.Repeat(repeat)["restart"]!["eligibility_reasons"]));
        }
        Assert.Equal(1, await run.JudgeAsync()); Assert.Equal("incomplete", Text(run.Candidate["status"]));
    }

    [Fact]
    public async Task Given_AcceptedTerminationWithoutA3_When_FinalSweepRuns_Then_FallbackWindowClassifiesTimeoutAndRejectsRestart_T31()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka); run.RemoveA3(2);
        Assert.Equal(0, await run.RunAsync()); JsonObject item = run.Repeat(2); AssertComplete(item);
        Assert.Equal(["b10_restart_input_unavailable"], Strings(item["restart"]!["eligibility_reasons"]));
        Assert.Null(item["restart"]!["arms"]);
    }

    [Fact]
    public async Task Given_NoObjectRepeatHasLateTimeoutCopy_When_FinalSweepRuns_Then_ItsFallbackRangeIncludesTheOwnedCopy_T32()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka); NoKill(run, 2);
        await run.AddRecordAsync(2, B10FinalSweepHarness.Timeout(2), "timeout0"); run.CaptureSweep();
        Assert.Equal(0, await run.RunAsync()); AssertComplete(run.Repeat(2));
        Assert.Equal(1, run.Repeat(2)["timeout"]!["duplicate"]!.GetValue<long>());
        Assert.Equal(0, run.Repeat(3)["timeout"]!["duplicate"]!.GetValue<long>());
    }

    [Fact]
    public async Task Given_TimeoutLedgerHashMismatch_When_FinalSweepRuns_Then_InvalidTimeoutAlsoBlocksRestartEligibility_T33()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        string name = PhasePaths.Publication(B10FinalSweepHarness.Run, "C1", 3, B10FinalSweepHarness.Timeout(3), "index.json");
        Change(run.Ledger, name, root => root["artifacts"]![0]!["sha256"] = new string('f', 64));
        Assert.Equal(0, await run.RunAsync()); JsonObject item = run.Repeat(3);
        Assert.Equal("invalid", Text(item["status"])); Assert.Null(item["timeout"]);
        Assert.Equal(["b10_final_timeout_evidence_invalid"], Strings(item["reasons"]));
        Assert.Contains("b10_restart_timeout_bookkeeping_unavailable", Strings(item["restart"]!["eligibility_reasons"]));
        Assert.Equal("ineligible", Text(item["restart"]!["eligibility"]));
    }

    [Fact]
    public async Task Given_NullEndLineWithIncompleteWindowProblem_When_FinalSweepRuns_Then_UsesAuthenticatedFallbackWindow_T35()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.RewriteA3(3, root =>
        {
            root["data"]!["end_line"] = null; root["data"]!["problems"] = new JsonArray("b10_abrupt_verify_window_incomplete");
            root["data"]!["eligibility"] = "ineligible";
            root["data"]!["eligibility_reasons"] = new JsonArray("b10_restart_window_incomplete");
        });
        Assert.Equal(0, await run.RunAsync()); AssertComplete(run.Repeat(3));
        Assert.Equal(["b10_restart_window_incomplete"], Strings(run.Repeat(3)["restart"]!["eligibility_reasons"]));
        Assert.Equal("unavailable", Text(run.Repeat(3)["restart"]!["comparison"]));
    }

    [Fact]
    public async Task Given_NullA3LinesAndUnboundStartNote_When_FinalSweepRuns_Then_W3InvalidatesEveryRepeat_T36()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.RewriteA3(3, root =>
        {
            root["data"]!["start_line"] = null; root["data"]!["end_line"] = null;
            root["data"]!["problems"] = new JsonArray("b10_abrupt_verify_start_line_invalid");
            root["data"]!["eligibility"] = "ineligible";
            root["data"]!["eligibility_reasons"] = new JsonArray("b10_restart_configuration_invalid");
        });
        string path = PhasePaths.StartLine(B10FinalSweepHarness.Run, "C1", 3);
        run.Verifier[path] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(run.Verifier[path]) + " ");
        Assert.Equal(0, await run.RunAsync()); AssertGlobal(run, "b10_final_start_line_invalid");
        for (int repeat = 1; repeat <= 5; repeat++)
            Assert.Equal(repeat == 3 ? StartAndSweepReasons : SweepUnavailableReasons, Strings(run.Repeat(repeat)["restart"]!["eligibility_reasons"]));
        Assert.Equal(1, await run.JudgeAsync()); Assert.Equal("invalid", Text(run.Candidate["status"]));
        Assert.Empty(run.Candidate["b10_final"]!["restart_rule"]!["lost_resend_finding_repeats"]!.AsArray());
    }

    [Theory]
    [InlineData(24, false)]
    [InlineData(25, true)]
    [InlineData(26, true)]
    public async Task Given_RecordBudgetBelowAtAndAboveFullWindow_When_FinalSweepRuns_Then_OnlyCompleteDrainIsFinal(int records, bool complete)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.Configuration["Verifier:MaxConsumedRecords"] = records.ToString(CultureInfo.InvariantCulture);
        Assert.Equal(0, await run.RunAsync()); Assert.Equal(complete, run.Data["scan_complete"]!.GetValue<bool>());
        if (!complete) AssertGlobal(run, "b10_final_drain_incomplete");
    }

    [Fact]
    public async Task Given_ForeignOwnerInsideOriginalWindow_When_FinalSweepRuns_Then_OnlyThatRepeatIsInvalid_T6()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka, foreignRepeat: 4);
        Assert.Equal(0, await run.RunAsync()); JsonObject item = run.Repeat(4);
        Assert.Equal("invalid", Text(item["status"])); Assert.Null(item["timeout"]);
        Assert.Equal(["b10_final_owner_invalid"], Strings(item["reasons"]));
        Assert.Equal(["b10_restart_late_record_unjoined"], Strings(item["restart"]!["eligibility_reasons"]));
        AssertUnknownSweepCounts(item);
        foreach (int repeat in new[] { 1, 2, 3, 5 }) AssertComplete(run.Repeat(repeat));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_EmptyKillSetWithControlLoss_When_FinalSweepRuns_Then_FindingRequiresAllRemainingChecks_T23_T38(bool partitionMismatch)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka, controlLostRepeat: 4, change: (repeat, evidence) =>
        {
            if (repeat != 4) return;
            ConfigureEmptyKill(evidence, partitionMismatch);
        });
        run.RemoveA3(4);
        Assert.Equal(0, await run.RunAsync()); JsonObject item = run.Repeat(4); AssertComplete(item);
        Assert.Equal("intended_self_kill", Text(item["restart"]!["termination"]!["class"]));
        Assert.Equal("kill_too_late", Text(item["restart"]!["termination"]!["outcome"]));
        Assert.Equal(partitionMismatch ? EmptyKillIdentityReasons : EmptyKillReasons, Strings(item["restart"]!["eligibility_reasons"]));
        if (partitionMismatch) Assert.Null(item["restart"]!["arms"]);
        else
        {
            Assert.Equal(1, Arm(item, "same_instance")["lost_resends"]!.GetValue<long>());
            Assert.Equal(0, Arm(item, "cross_restart")["unconfirmed_count"]!.GetValue<long>());
        }
        Assert.Equal(0, await run.JudgeAsync());
        JsonNode rule = run.Candidate["b10_final"]!["restart_rule"]!;
        Assert.Equal(partitionMismatch ? [] : RepeatFour, rule["lost_resend_finding_repeats"]!.AsArray().Select(value => value!.GetValue<int>()));
        Assert.Empty(rule["eligible_loss_repeats"]!.AsArray());
    }

    [Fact]
    public async Task Given_MetadataFailureAfterStartWithoutProducer_When_FinalSweepRuns_Then_TimeoutIsAbsentAndNotInvented_T28()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka, metadataFailureRepeat: 2);
        Assert.Equal(0, await run.RunAsync());
        Assert.Equal("incomplete", Text(run.Repeat(2)["status"])); Assert.Null(run.Repeat(2)["timeout"]);
        Assert.Equal(["b10_final_timeout_absent"], Strings(run.Repeat(2)["reasons"]));
        Assert.Equal(["b10_restart_first_evidence_unavailable", "b10_restart_input_unavailable"], Strings(run.Repeat(2)["restart"]!["eligibility_reasons"]));
    }

    [Fact]
    public async Task Given_FiveRealOneProducerVerifyOutputs_When_FinalC0SweepRuns_Then_AllTimeoutCountsAreFinal_T14b()
    {
        using var run = await B10FinalSweepHarness.CreateC0Async(kafka);
        Assert.Equal(0, await run.RunAsync()); Assert.True(run.Data["scan_complete"]!.GetValue<bool>(), run.Data.ToJsonString());
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            JsonObject item = run.Repeat(repeat);
            Assert.True(Text(item["status"]) == "complete_valid", item.ToJsonString()); Assert.True(item["classification_final"]!.GetValue<bool>());
            Assert.Equal(repeat == 3 ? 1 : 0, item["timeout"]!["duplicate"]!.GetValue<long>());
            Assert.Null(item["restart"]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Given_C0ZeroOrTwoProducerRegistrations_When_FinalSweepRuns_Then_OnlyThatRepeatHasInvalidInput_T15_T34(int registrations)
    {
        using var run = await B10FinalSweepHarness.CreateC0Async(kafka);
        string path = PhasePaths.Registration(B10FinalSweepHarness.Run, "C0", 2, B10FinalSweepHarness.Run + "-ordinary-r2");
        if (registrations == 0) run.Ledger.Remove(path);
        else run.AddC0Producer(2, "second");
        Assert.Equal(0, await run.RunAsync());
        Assert.Equal("invalid", Text(run.Repeat(2)["status"])); Assert.Null(run.Repeat(2)["timeout"]);
        Assert.Equal(["b10_final_repeat_input_invalid"], Strings(run.Repeat(2)["reasons"]));
        foreach (int repeat in new[] { 1, 3, 4, 5 }) Assert.Equal("complete_valid", Text(run.Repeat(repeat)["status"]));
        Assert.Equal(1, await run.JudgeAsync());
        Assert.Equal("held", Text(run.Candidate["b10_final"]!["timeout_rule"]!["rule_selection"]));
    }

    [Fact]
    public async Task Given_C0StopsAfterThirdRepeat_When_FinalSweepRuns_Then_UnstartedSlotsHaveNoInventedCounts_T37()
    {
        using var run = await B10FinalSweepHarness.CreateC0Async(kafka, stopAfter: 3);
        Assert.Equal(0, await run.RunAsync());
        foreach (int repeat in new[] { 4, 5 })
        {
            Assert.Equal("incomplete", Text(run.Repeat(repeat)["status"])); Assert.Null(run.Repeat(repeat)["timeout"]);
            Assert.Equal(["b10_final_repeat_not_started"], Strings(run.Repeat(repeat)["reasons"]));
        }
        Assert.Equal(1, await run.JudgeAsync());
        Assert.Equal("held", Text(run.Candidate["b10_final"]!["timeout_rule"]!["rule_selection"]));
    }

    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public async Task Given_NoRepeatStarted_When_FinalSweepRuns_Then_EmptyPhaseIsScannedButEverySlotRemainsIncomplete(string phase)
    {
        using var run = phase == "C0" ? await B10FinalSweepHarness.CreateC0Async(kafka, stopAfter: 0) : await B10FinalSweepHarness.CreateAsync(kafka, stopAfter: 0);
        Assert.Equal(0, await run.RunAsync()); Assert.True(run.Data["scan_complete"]!.GetValue<bool>(), run.Data.ToJsonString());
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            Assert.Equal("incomplete", Text(run.Repeat(repeat)["status"])); Assert.Null(run.Repeat(repeat)["timeout"]);
            Assert.False(run.Repeat(repeat)["classification_final"]!.GetValue<bool>());
            Assert.Equal(["b10_final_repeat_not_started"], Strings(run.Repeat(repeat)["reasons"]));
        }
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task Given_SweepReceiptTimeBelowAtAndAboveVerifierCompletion_When_FinalSweepRuns_Then_OnlyEarlierTimeIsRejected(int ticks, bool accepted)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        JsonObject receipt = JsonNode.Parse(run.Verifier[B10FinalSweepHarness.Prefix(5) + "verify-completion.json"])!.AsObject();
        DateTimeOffset completed = DateTimeOffset.Parse(receipt["completed_utc"]!.GetValue<string>(), CultureInfo.InvariantCulture);
        Change(run.Verifier, PhasePaths.SweepLine(B10FinalSweepHarness.Run, "C1"), root => root["last_verifier_completed_utc"] = completed.AddTicks(ticks).ToString("O", CultureInfo.InvariantCulture));
        Assert.Equal(0, await run.RunAsync()); Assert.Equal(accepted, run.Data["scan_complete"]!.GetValue<bool>());
        if (!accepted) AssertGlobal(run, "b10_final_sweep_line_invalid");
        else for (int repeat = 1; repeat <= 5; repeat++) AssertComplete(run.Repeat(repeat));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A3ObjectOrReceiptMissing_When_FinalSweepRuns_Then_TimeoutSurvivesAndRestartIsUnavailable(bool objectMissing)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        run.Verifier.Remove(objectMissing ? B10FinalSweepHarness.A3(2) : B10FinalSweepHarness.Prefix(2) + "verify-completion.json");
        Assert.Equal(0, await run.RunAsync()); AssertComplete(run.Repeat(2));
        Assert.Equal(["b10_restart_input_unavailable"], Strings(run.Repeat(2)["restart"]!["eligibility_reasons"]));
        Assert.Null(run.Repeat(2)["restart"]!["arms"]);
    }

    [Fact]
    public async Task Given_EmptyWindowsAndAuthenticatedEmptyTimeoutCohorts_When_FinalSweepRuns_Then_ZeroCountsAreKnownAndFinal()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka, emptyWindow: true);
        Assert.Equal(0, await run.RunAsync()); Assert.True(run.Data["scan_complete"]!.GetValue<bool>(), run.Data.ToJsonString());
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            JsonObject item = run.Repeat(repeat); Assert.Equal("complete_valid", Text(item["status"]));
            Assert.True(item["classification_final"]!.GetValue<bool>());
            Assert.All(item["timeout"]!.AsObject(), count => Assert.Equal(0, count.Value!.GetValue<long>()));
        }
    }

    [Fact]
    public async Task Given_EmptyKillSetAndInvalidTimeoutPublication_When_FinalSweepRuns_Then_E7PreservesControlLossAndFinding_T33()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka, controlLostRepeat: 4, change: (repeat, evidence) =>
        { if (repeat == 4) ConfigureEmptyKill(evidence, false); });
        run.RemoveA3(4);
        string index = PhasePaths.Publication(B10FinalSweepHarness.Run, "C1", 4, B10FinalSweepHarness.Timeout(4), "index.json");
        Change(run.Ledger, index, root => root["artifacts"]![0]!["sha256"] = new string('f', 64));
        Assert.Equal(0, await run.RunAsync()); JsonObject item = run.Repeat(4);
        Assert.Equal("invalid", Text(item["status"])); Assert.Null(item["timeout"]);
        Assert.Equal(["b10_final_timeout_evidence_invalid"], Strings(item["reasons"]));
        Assert.Equal(["b10_restart_timeout_bookkeeping_unavailable", "kill_too_late"], Strings(item["restart"]!["eligibility_reasons"]));
        Assert.Equal(1, Arm(item, "same_instance")["lost_resends"]!.GetValue<long>());
        Assert.Equal(0, Arm(item, "cross_restart")["unconfirmed_count"]!.GetValue<long>());
        Assert.Equal(1, await run.JudgeAsync());
        JsonNode rule = run.Candidate["b10_final"]!["restart_rule"]!;
        Assert.Equal(RepeatFour, rule["lost_resend_finding_repeats"]!.AsArray().Select(value => value!.GetValue<int>()));
        Assert.Empty(rule["eligible_loss_repeats"]!.AsArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Given_EmptyKillControlPairAndDiagnosticCapBelowAtOrAboveTwoCopies_When_FinalSweepRuns_Then_PairAndWitnessStayKnown(int diagnosticCopies)
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka, change: (repeat, evidence) =>
        { if (repeat == 4) ConfigureEmptyKill(evidence, false); });
        run.RemoveA3(4);
        run.Configuration["Verifier:MaxDiagnosticsPerCheck"] = diagnosticCopies.ToString(CultureInfo.InvariantCulture);
        Assert.Equal(0, await run.RunAsync()); AssertComplete(run.Repeat(4));
        JsonObject control = Arm(run.Repeat(4), "same_instance");
        Assert.Equal(1, control["pair_count"]!.GetValue<long>());
        Assert.NotNull(control["witness"]);
        Assert.Equal("s4", Text(control["witness"]!["original_attempt_id"]));
        Assert.Equal("t4", Text(control["witness"]!["resend_attempt_id"]));
        Assert.Equal(0, control["lost_resends"]!.GetValue<long>());
    }

    [Fact]
    public async Task Given_ScopedFirstRegistrationWithWrongJob_When_FinalSweepRuns_Then_TimeoutSurvivesWithoutForeignOwnershipFailure_R9b()
    {
        using var run = await B10FinalSweepHarness.CreateAsync(kafka);
        string name = B10FinalSweepHarness.Prefix(2) + B10FinalSweepHarness.First(2) + "/registration.json";
        Change(run.Ledger, name, root => root["identity"]!["job_name"] = "wrong-job");
        Assert.Equal(0, await run.RunAsync()); AssertComplete(run.Repeat(2));
        Assert.Equal(["b10_restart_input_unavailable"], Strings(run.Repeat(2)["restart"]!["eligibility_reasons"]));
        Assert.Null(run.Repeat(2)["restart"]!["arms"]);
        foreach (int repeat in new[] { 1, 3, 4, 5 }) AssertComplete(run.Repeat(repeat));
    }

    private static void ConfigureEmptyKill(B10AbruptVerifyEvidence evidence, bool partitionMismatch)
    {
        JsonObject terminal = evidence.Frames.Single(frame => Text(frame["kind"]) == "terminal" && Text(frame["data"]!["attempt_id"]) == "s4").DeepClone().AsObject();
        terminal["data"]!["attempt_id"] = "c4";
        evidence.Frames.Insert(evidence.Frames.Count - 1, terminal);
        for (int index = 0; index < evidence.Frames.Count; index++) evidence.Frames[index]["sequence"] = index + 1;
        evidence.Change("first/termination.json", root =>
        { root["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray(); root["data"]!["outcome"] = "kill_too_late"; root["data"]!["successor_permitted"] = false; });
        foreach (string name in evidence.Files.Keys.Where(name => name.StartsWith("successor/", StringComparison.Ordinal)).ToArray()) evidence.Files.Remove(name);
        if (partitionMismatch)
            evidence.Frames.First(frame => Text(frame["kind"]) == "pre_acceptance_report")["data"]!["partition"] = 1;
    }

    private static void NoKill(B10FinalSweepHarness run, int repeat)
    {
        run.RemoveA3(repeat);
        Change(run.Ledger, B10FinalSweepHarness.Prefix(repeat) + B10FinalSweepHarness.First(repeat) + "/termination.json", root =>
        { root["data"]!["termination"] = "no_kill"; root["data"]!["outcome"] = "kill_too_early"; root["data"]!["successor_permitted"] = false; });
    }
    private static long SweepHigh(B10FinalSweepHarness run) => JsonNode.Parse(run.Verifier[PhasePaths.SweepLine(B10FinalSweepHarness.Run, "C1")])!["partition_highs"]![0]!["high"]!.GetValue<long>();
    private static void Change(Dictionary<string, byte[]> store, string name, Action<JsonObject> change)
    { JsonObject root = JsonNode.Parse(store[name])!.AsObject(); change(root); store[name] = Encoding.UTF8.GetBytes(root.ToJsonString()); }
    internal static JsonObject Arm(JsonObject repeat, string arm) => repeat["restart"]!["arms"]!.AsArray().Single(item => Text(item!["arm"]) == arm)!.AsObject();
    private static string? Text(JsonNode? node) => node?.GetValue<string>();
    private static string?[] Strings(JsonNode? node) => node!.AsArray().Select(Text).ToArray();
    private static void AssertComplete(JsonObject item)
    {
        Assert.True(Text(item["status"]) == "complete_valid", item.ToJsonString()); Assert.True(item["classification_final"]!.GetValue<bool>());
        Assert.Equal(1, item["timeout"]!["ok"]!.GetValue<long>()); Assert.Empty(item["reasons"]!.AsArray());
        foreach (string field in new[] { "loss", "phantom", "reported_failure", "reorder" }) Assert.Equal(0, item["timeout"]![field]!.GetValue<long>());
    }
    private static void AssertArmDisagreement(JsonObject item)
    {
        AssertComplete(item);
        Assert.Equal("ineligible", Text(item["restart"]!["eligibility"]));
        Assert.Equal(["b10_restart_late_record_unjoined"], Strings(item["restart"]!["eligibility_reasons"]));
        AssertUnknownSweepCounts(item);
    }
    private static void AssertUnknownSweepCounts(JsonObject item)
    {
        Assert.Equal("unavailable", Text(item["restart"]!["comparison"]));
        foreach (JsonNode? arm in item["restart"]!["arms"]!.AsArray())
            foreach (string field in new[] { "originals_seen", "resends_seen", "pair_count", "unreported_seen", "unreported_unseen", "lost_resends", "witness" }) Assert.Null(arm![field]);
    }
    private static void AssertGlobal(B10FinalSweepHarness run, string reason)
    {
        Assert.False(run.Data["scan_complete"]!.GetValue<bool>(), run.Data.ToJsonString());
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            JsonObject item = run.Repeat(repeat); Assert.Equal("invalid", Text(item["status"]));
            Assert.False(item["classification_final"]!.GetValue<bool>()); Assert.Null(item["timeout"]);
            Assert.Equal([reason], Strings(item["reasons"]));
            Assert.Contains("b10_restart_sweep_unavailable", Strings(item["restart"]!["eligibility_reasons"]));
            if (item["restart"]!["arms"] is JsonArray arms)
                foreach (JsonNode? arm in arms)
                    foreach (string field in new[] { "originals_seen", "resends_seen", "pair_count", "unreported_seen", "unreported_unseen", "lost_resends", "witness" }) Assert.Null(arm![field]);
        }
    }
}
#pragma warning restore CA1707
