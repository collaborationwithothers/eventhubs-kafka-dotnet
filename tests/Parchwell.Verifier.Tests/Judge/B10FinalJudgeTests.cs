using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Tests.Component;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Judge;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

#pragma warning disable CA1707 // Repository test naming convention.
// ADR-021 rules are observed through the real JudgeHandler with independent literal phase inputs.
public sealed class B10FinalJudgeTests
{
    [Theory]
    [InlineData(0, "untestable")]
    [InlineData(1, "confirmed")]
    [InlineData(2, "confirmed")]
    public async Task Given_FiveFinalC0Repeats_When_Judged_Then_UsesDuplicateBoundary(long duplicates, string selection)
    {
        var fixture = new B10FinalJudgeFixture("C0");
        fixture.Repeats[0] = fixture.Repeats[0] with { Timeout = new(1, 0, 0, 0, duplicates, 0) };
        using JsonDocument candidate = await fixture.JudgeAsync();
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        JsonElement rule = candidate.RootElement.GetProperty("b10_final").GetProperty("timeout_rule");
        Assert.Equal(5, rule.GetProperty("complete_valid_final_repeats").GetInt32());
        Assert.Equal(duplicates, rule.GetProperty("duplicate_total").GetInt64());
        Assert.Equal(selection, rule.GetProperty("rule_selection").GetString());
        Assert.Equal(duplicates, candidate.RootElement.GetProperty("counts").GetProperty("duplicate").GetInt64());
        Assert.Equal(JsonValueKind.Null, candidate.RootElement.GetProperty("gate").ValueKind);
    }

    [Theory]
    [InlineData(0)] [InlineData(4)]
    public async Task Given_IncompleteFinalC0Repeats_When_Judged_Then_HoldsTimeoutAndOmitsCounts(int complete)
    {
        var fixture = new B10FinalJudgeFixture("C0");
        for (int index = complete; index < 5; index++) fixture.Repeats[index] = fixture.Repeats[index] with
        { Status = "incomplete", ClassificationFinal = false, Timeout = null, Reasons = ["b10_final_repeat_not_started"] };
        using JsonDocument candidate = await fixture.JudgeAsync();
        Assert.Equal("incomplete", candidate.RootElement.GetProperty("status").GetString());
        Assert.False(candidate.RootElement.TryGetProperty("counts", out _));
        JsonElement rule = candidate.RootElement.GetProperty("b10_final").GetProperty("timeout_rule");
        Assert.Equal(complete, rule.GetProperty("complete_valid_final_repeats").GetInt32());
        Assert.Equal(JsonValueKind.Null, rule.GetProperty("duplicate_total").ValueKind);
        Assert.Equal("held", rule.GetProperty("rule_selection").GetString());
        Assert.Contains("j7_counts_unavailable", Strings(candidate.RootElement.GetProperty("reasons")));
    }

    [Theory]
    [InlineData(0, false, false, "held", "fewer_than_three_eligible")]
    [InlineData(2, true, false, "held", "fewer_than_three_eligible")]
    [InlineData(3, false, false, "untestable", "no_cross_restart_duplicate_observed")]
    [InlineData(4, false, false, "untestable", "no_cross_restart_duplicate_observed")]
    [InlineData(3, false, true, "held", "eligible_loss_without_pair")]
    [InlineData(3, true, true, "confirmed", null)]
    [InlineData(5, true, false, "confirmed", null)]
    public async Task Given_EligibilityPairsAndLoss_When_Judged_Then_SelectsRulesInRequiredOrder(int eligible, bool pair, bool loss,
        string selection, string? reason)
    {
        var fixture = new B10FinalJudgeFixture("C1");
        for (int index = 0; index < 5; index++)
        {
            B10FinalRestart restart = fixture.Repeats[index].Restart!;
            fixture.Repeats[index] = fixture.Repeats[index] with { Restart = restart with
            {
                Eligibility = index < eligible ? "eligible" : "ineligible",
                EligibilityReasons = index < eligible ? [] : ["b10_restart_guard_failed"],
                Comparison = pair ? "cross_restart_only" : "neither",
                Arms = [B10FinalJudgeFixture.Arm("same_instance", false, loss && pair), B10FinalJudgeFixture.Arm("cross_restart", pair, loss && !pair)]
            } };
        }
        using JsonDocument candidate = await fixture.JudgeAsync();
        JsonElement rule = candidate.RootElement.GetProperty("b10_final").GetProperty("restart_rule");
        Assert.Equal(selection, rule.GetProperty("rule_selection").GetString());
        Assert.Equal(reason, rule.GetProperty("selection_reason").GetString());
        Assert.Equal(selection == "confirmed" ? B10FinalJudgeFixture.ExpectedRestartSubject : null, rule.GetProperty("subject").GetString());
        Assert.Equal(eligible, rule.GetProperty("eligible_repeats").GetInt32());
        Assert.Equal(Enumerable.Range(1, eligible), Numbers(rule.GetProperty("eligible_repeat_numbers")));
        Assert.Equal(pair ? Enumerable.Range(1, eligible) : [], Numbers(rule.GetProperty("qualifying_pair_repeats")));
        Assert.Equal(loss ? Enumerable.Range(1, 5) : [], Numbers(rule.GetProperty("lost_resend_finding_repeats")));
        Assert.Equal(loss ? Enumerable.Range(1, eligible) : [], Numbers(rule.GetProperty("eligible_loss_repeats")));
    }

    [Fact]
    public async Task Given_PairsOnlyInIneligibleRepeatsAndEmptyKillLoss_When_Judged_Then_PublishesIndependentFindingWithoutPositive()
    {
        var fixture = new B10FinalJudgeFixture("C1");
        B10FinalRestart noKill = fixture.Repeats[4].Restart! with { Eligibility = "ineligible", EligibilityReasons = ["kill_too_early"],
            Termination = new("no_kill", "kill_too_early"), Comparison = "unavailable", Arms = null };
        B10FinalRestart tooLate = fixture.Repeats[3].Restart! with { Eligibility = "ineligible", EligibilityReasons = ["kill_too_late"],
            Termination = new("intended_self_kill", "kill_too_late"), Comparison = "unavailable",
            Arms = [B10FinalJudgeFixture.Arm("same_instance", false, true), B10FinalJudgeFixture.EmptyArm()] };
        fixture.Repeats[3] = fixture.Repeats[3] with { Restart = tooLate };
        fixture.Repeats[4] = fixture.Repeats[4] with { Restart = noKill };
        using JsonDocument candidate = await fixture.JudgeAsync();
        JsonElement rule = candidate.RootElement.GetProperty("b10_final").GetProperty("restart_rule");
        Assert.Equal("untestable", rule.GetProperty("rule_selection").GetString());
        Assert.Empty(Numbers(rule.GetProperty("qualifying_pair_repeats")));
        Assert.Equal([4], Numbers(rule.GetProperty("lost_resend_finding_repeats")));
        Assert.Empty(Numbers(rule.GetProperty("eligible_loss_repeats")));
    }

    [Fact]
    public async Task Given_ThreeEligibleAndPairsOnlyInIneligibleRepeats_When_Judged_Then_DoesNotPromoteThosePairs()
    {
        var fixture = new B10FinalJudgeFixture("C1");
        for (int index = 3; index < 5; index++) fixture.Repeats[index] = fixture.Repeats[index] with
        { Restart = fixture.Repeats[index].Restart! with { Eligibility = "ineligible", EligibilityReasons = ["b10_restart_guard_failed"],
            Comparison = "cross_restart_only", Arms = [B10FinalJudgeFixture.Arm("same_instance"), B10FinalJudgeFixture.Arm("cross_restart", true)] } };
        using JsonDocument candidate = await fixture.JudgeAsync();
        JsonElement rule = candidate.RootElement.GetProperty("b10_final").GetProperty("restart_rule");
        Assert.Equal("untestable", rule.GetProperty("rule_selection").GetString());
        Assert.Equal("no_cross_restart_duplicate_observed", rule.GetProperty("selection_reason").GetString());
        Assert.Empty(Numbers(rule.GetProperty("qualifying_pair_repeats")));
    }

    [Fact]
    public async Task Given_PhaseSumExactlyInt64Maximum_When_Judged_Then_PreservesKnownTotal()
    {
        var fixture = new B10FinalJudgeFixture("C0");
        for (int index = 0; index < 5; index++) fixture.Repeats[index] = fixture.Repeats[index] with { Timeout = new(index == 0 ? long.MaxValue : 0, 0, 0, 0, 0, 0) };
        using JsonDocument candidate = await fixture.JudgeAsync();
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        Assert.Equal(long.MaxValue, candidate.RootElement.GetProperty("counts").GetProperty("ok").GetInt64());
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("duplicate")]
    public async Task Given_CheckedSumOverflow_When_Judged_Then_HoldsCountsWithoutInventedTotals(string field)
    {
        var fixture = new B10FinalJudgeFixture("C0");
        for (int index = 0; index < 2; index++) fixture.Repeats[index] = fixture.Repeats[index] with
        { Timeout = field == "ok" ? new(long.MaxValue, 0, 0, 0, 0, 0) : new(0, 0, 0, 0, long.MaxValue, 0) };
        using JsonDocument candidate = await fixture.JudgeAsync();
        Assert.Equal("incomplete", candidate.RootElement.GetProperty("status").GetString());
        Assert.False(candidate.RootElement.TryGetProperty("counts", out _));
        Assert.Contains("j7_counts_unavailable", Strings(candidate.RootElement.GetProperty("reasons")));
        JsonElement rule = candidate.RootElement.GetProperty("b10_final").GetProperty("timeout_rule");
        Assert.Equal("held", rule.GetProperty("rule_selection").GetString());
        Assert.Equal(JsonValueKind.Null, rule.GetProperty("duplicate_total").ValueKind);
    }

    [Theory]
    [InlineData("missing-projection")] [InlineData("missing-receipt")]
    [InlineData("extra-registration")] [InlineData("missing-registration")]
    [InlineData("receipt-hash")] [InlineData("receipt-length")] [InlineData("receipt-run")]
    [InlineData("projection-phase")] [InlineData("projection-image")] [InlineData("source")]
    [InlineData("sweep-line")] [InlineData("registration")] [InlineData("registration-listing")]
    [InlineData("abrupt-registration")] [InlineData("cross-repeat-instance")]
    public async Task Given_UntrustedProjectionBinding_When_Judged_Then_RejectsFinalProjection(string change)
    {
        var fixture = new B10FinalJudgeFixture("C1");
        fixture.Publish();
        string projectionName = PhasePaths.B10FinalSweep(B10FinalJudgeFixture.Run, "C1");
        if (change == "missing-projection") fixture.Verifier.Remove(projectionName);
        if (change == "missing-receipt") fixture.Verifier.Remove(PhasePaths.B10FinalSweepCompletion(B10FinalJudgeFixture.Run, "C1"));
        if (change == "extra-registration") fixture.AddOrdinaryRegistration(1, "extra-pod");
        if (change == "missing-registration") fixture.Ledger.Remove(PhasePaths.Registration(B10FinalJudgeFixture.Run, "C1", 1, "timeout-1"));
        if (change.StartsWith("receipt-", StringComparison.Ordinal)) fixture.Mutate(PhasePaths.B10FinalSweepCompletion(B10FinalJudgeFixture.Run, "C1"), node =>
        {
            if (change == "receipt-hash") node["artifacts"]![0]!["sha256"] = new string('f', 64);
            if (change == "receipt-length") node["artifacts"]![0]!["length"] = 1;
            if (change == "receipt-run") node["run_id"] = "b10-2-2";
        });
        if (change.StartsWith("projection-", StringComparison.Ordinal))
        {
            fixture.Mutate(projectionName, node => node["identity"]![change == "projection-phase" ? "phase" : "image_reference"] = change == "projection-phase" ? "C0" : B10FinalJudgeFixture.Loadgen);
            fixture.RebindProjection();
        }
        if (change == "source") fixture.Verifier[fixture.SourceName(1)] = "{}"u8.ToArray();
        if (change == "sweep-line") fixture.Verifier[PhasePaths.SweepLine(B10FinalJudgeFixture.Run, "C1")] = "{}"u8.ToArray();
        if (change == "registration") fixture.MutateLedger(PhasePaths.Registration(B10FinalJudgeFixture.Run, "C1", 1, "timeout-1"), node => node["source_commit"] = new string('b', 40));
        if (change == "abrupt-registration") fixture.MutateLedger($"{B10FinalJudgeFixture.Run}/C1/r1/b10-abrupt/first-1/registration.json", node => node["identity"]!["purpose"] = "diagnostic_trial");
        if (change == "cross-repeat-instance")
        {
            string name = PhasePaths.Registration(B10FinalJudgeFixture.Run, "C1", 2, "timeout-2");
            fixture.MutateLedger(name, node => { node["identity"]!["instance_id"] = "timeout-1"; node["identity"]!["expected_instance_id"] = "timeout-1"; node["pod_name"] = "timeout-1"; });
            fixture.Ledger[PhasePaths.Registration(B10FinalJudgeFixture.Run, "C1", 2, "timeout-1")] = fixture.Ledger[name]; fixture.Ledger.Remove(name);
        }
        if (change == "registration-listing") fixture.IncompletePrefix = PhasePaths.ProducersPrefix(B10FinalJudgeFixture.Run, "C1", 1);
        using JsonDocument candidate = await fixture.JudgeAsync(false);
        Assert.Equal("invalid", candidate.RootElement.GetProperty("status").GetString());
        Assert.Contains("b10_final_projection_untrusted", Strings(candidate.RootElement.GetProperty("reasons")));
        Assert.Equal(JsonValueKind.Null, candidate.RootElement.GetProperty("b10_final").ValueKind);
        Assert.False(candidate.RootElement.TryGetProperty("b10_sweep", out _));
    }

    [Theory]
    [InlineData(0, false, false)] [InlineData(0, false, true)]
    [InlineData(0, true, false)] [InlineData(0, true, true)]
    [InlineData(1, false, false)] [InlineData(1, false, true)]
    [InlineData(1, true, false)] [InlineData(1, true, true)]
    public async Task Given_BoundUnknownPairEvidence_When_Judged_Then_RejectsAvailableComparisonAndEligibility(int armIndex, bool otherPair, bool eligible)
    {
        string[] comparisons = eligible ? ["neither", "same_instance_only", "cross_restart_only", "both", "unavailable"] :
            ["neither", "same_instance_only", "cross_restart_only", "both"];
        foreach (string comparison in comparisons)
        {
            var fixture = new B10FinalJudgeFixture("C1");
            B10FinalArm[] arms = [B10FinalJudgeFixture.Arm("same_instance", armIndex == 1 && otherPair),
                B10FinalJudgeFixture.Arm("cross_restart", armIndex == 0 && otherPair)];
            string knownComparison = otherPair ? armIndex == 0 ? "cross_restart_only" : "same_instance_only" : "neither";
            fixture.Repeats[0] = fixture.Repeats[0] with { Restart = fixture.Repeats[0].Restart! with { Comparison = knownComparison, Arms = arms } };
            fixture.Publish();
            fixture.Mutate(PhasePaths.B10FinalSweep(B10FinalJudgeFixture.Run, "C1"), node =>
            {
                JsonObject restart = node["data"]!["repeats"]![0]!["restart"]!.AsObject();
                restart["arms"]![armIndex]!["pair_count"] = null;
                restart["comparison"] = comparison;
                restart["eligibility"] = eligible ? "eligible" : "ineligible";
                restart["eligibility_reasons"] = eligible ? new JsonArray() : new JsonArray("b10_restart_guard_failed");
            });
            fixture.RebindProjection();
            using JsonDocument candidate = await fixture.JudgeAsync(false);
            Assert.Equal("invalid", candidate.RootElement.GetProperty("status").GetString());
            Assert.Contains("b10_final_projection_untrusted", Strings(candidate.RootElement.GetProperty("reasons")));
            Assert.Equal(JsonValueKind.Null, candidate.RootElement.GetProperty("b10_final").ValueKind);
        }
    }

    [Theory]
    [InlineData(0L, 0L, "neither")]
    [InlineData(1L, 0L, "same_instance_only")] [InlineData(0L, 1L, "cross_restart_only")]
    [InlineData(1L, 1L, "both")]
    [InlineData(2L, 0L, "same_instance_only")] [InlineData(0L, 2L, "cross_restart_only")]
    [InlineData(2L, 2L, "both")]
    public async Task Given_KnownPairCounts_When_Judged_Then_RetainsAvailableComparison(long samePairs, long crossPairs, string comparison)
    {
        var fixture = new B10FinalJudgeFixture("C1");
        fixture.Repeats[0] = fixture.Repeats[0] with { Restart = fixture.Repeats[0].Restart! with { Comparison = comparison,
            Arms = [B10FinalJudgeFixture.Arm("same_instance", samePairs > 0) with { PairCount = samePairs },
                B10FinalJudgeFixture.Arm("cross_restart", crossPairs > 0) with { PairCount = crossPairs }] } };
        using JsonDocument candidate = await fixture.JudgeAsync();
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        JsonElement final = candidate.RootElement.GetProperty("b10_final");
        JsonElement actual = final.GetProperty("repeats")[0].GetProperty("restart");
        Assert.Equal(comparison, actual.GetProperty("comparison").GetString());
        Assert.Equal("eligible", actual.GetProperty("eligibility").GetString());
        Assert.Equal(samePairs, actual.GetProperty("arms")[0].GetProperty("pair_count").GetInt64());
        Assert.Equal(crossPairs, actual.GetProperty("arms")[1].GetProperty("pair_count").GetInt64());
        JsonElement rule = final.GetProperty("restart_rule");
        Assert.Equal(5, rule.GetProperty("eligible_repeats").GetInt32());
        Assert.Equal(crossPairs > 0 ? "confirmed" : "untestable", rule.GetProperty("rule_selection").GetString());
        Assert.Equal(crossPairs > 0 ? [1] : [], Numbers(rule.GetProperty("qualifying_pair_repeats")));
    }

    [Fact]
    public async Task Given_UnavailableIneligibleUnknownPairs_When_Judged_Then_PreservesUnknownEvidenceAndExcludesRepeat()
    {
        var fixture = new B10FinalJudgeFixture("C1");
        fixture.Repeats[0] = fixture.Repeats[0] with { Restart = fixture.Repeats[0].Restart! with
        { Eligibility = "ineligible", EligibilityReasons = ["b10_restart_late_record_unjoined"], Comparison = "unavailable",
            Arms = fixture.Repeats[0].Restart!.Arms!.Select(arm => arm with { OriginalsSeen = null, ResendsSeen = null, PairCount = null,
                UnreportedSeen = null, UnreportedUnseen = null, LostResends = null, Witness = null }).ToArray() } };
        using JsonDocument candidate = await fixture.JudgeAsync();
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        JsonElement final = candidate.RootElement.GetProperty("b10_final");
        JsonElement actual = final.GetProperty("repeats")[0].GetProperty("restart");
        Assert.Equal("unavailable", actual.GetProperty("comparison").GetString());
        Assert.Equal("ineligible", actual.GetProperty("eligibility").GetString());
        Assert.Equal(["b10_restart_late_record_unjoined"], Strings(actual.GetProperty("eligibility_reasons")));
        Assert.All(actual.GetProperty("arms").EnumerateArray(), arm => Assert.Equal(JsonValueKind.Null, arm.GetProperty("pair_count").ValueKind));
        JsonElement rule = final.GetProperty("restart_rule");
        Assert.Equal(4, rule.GetProperty("eligible_repeats").GetInt32());
        Assert.Equal([2, 3, 4, 5], Numbers(rule.GetProperty("eligible_repeat_numbers")));
        Assert.Empty(Numbers(rule.GetProperty("qualifying_pair_repeats")));
        Assert.Equal("untestable", rule.GetProperty("rule_selection").GetString());
    }

    [Theory]
    [InlineData(0, "originals_seen", 0L)] [InlineData(0, "originals_seen", null)]
    [InlineData(0, "resends_seen", 0L)] [InlineData(0, "resends_seen", null)]
    [InlineData(1, "originals_seen", 0L)] [InlineData(1, "originals_seen", null)]
    [InlineData(1, "resends_seen", 0L)] [InlineData(1, "resends_seen", null)]
    public async Task Given_BoundPositivePairsWithoutKnownSeenMember_When_Judged_Then_RejectsFinalProjection(int armIndex, string field, long? count)
    {
        var fixture = new B10FinalJudgeFixture("C1");
        fixture.Repeats[0] = fixture.Repeats[0] with { Restart = fixture.Repeats[0].Restart! with
        { Comparison = "both", Arms = [B10FinalJudgeFixture.Arm("same_instance", true), B10FinalJudgeFixture.Arm("cross_restart", true)] } };
        fixture.Publish();
        fixture.Mutate(PhasePaths.B10FinalSweep(B10FinalJudgeFixture.Run, "C1"), node =>
            node["data"]!["repeats"]![0]!["restart"]!["arms"]![armIndex]![field] = count);
        fixture.RebindProjection();
        using JsonDocument candidate = await fixture.JudgeAsync(false);
        Assert.Equal("invalid", candidate.RootElement.GetProperty("status").GetString());
        Assert.Contains("b10_final_projection_untrusted", Strings(candidate.RootElement.GetProperty("reasons")));
        Assert.Equal(JsonValueKind.Null, candidate.RootElement.GetProperty("b10_final").ValueKind);
    }

    [Theory]
    [InlineData(0, "originals_seen", 1L)] [InlineData(0, "originals_seen", 2L)]
    [InlineData(0, "resends_seen", 1L)] [InlineData(0, "resends_seen", 2L)]
    [InlineData(1, "originals_seen", 1L)] [InlineData(1, "originals_seen", 2L)]
    [InlineData(1, "resends_seen", 1L)] [InlineData(1, "resends_seen", 2L)]
    public async Task Given_PositivePairsWithSeenMemberBoundary_When_Judged_Then_AcceptsAtAndAboveOne(int armIndex, string field, long count)
    {
        var fixture = new B10FinalJudgeFixture("C1");
        B10FinalArm[] arms = [B10FinalJudgeFixture.Arm("same_instance", true), B10FinalJudgeFixture.Arm("cross_restart", true)];
        arms[armIndex] = arms[armIndex] with { UnconfirmedAttemptIds = ["original", "other-original"], UnconfirmedCount = 2, ResendInvoked = 2,
            OriginalsSeen = field == "originals_seen" ? count : 1, ResendsSeen = field == "resends_seen" ? count : 1 };
        fixture.Repeats[0] = fixture.Repeats[0] with { Restart = fixture.Repeats[0].Restart! with { Comparison = "both", Arms = arms } };
        using JsonDocument candidate = await fixture.JudgeAsync();
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        JsonElement final = candidate.RootElement.GetProperty("b10_final");
        JsonElement actual = final.GetProperty("repeats")[0].GetProperty("restart").GetProperty("arms")[armIndex];
        Assert.Equal(count, actual.GetProperty(field).GetInt64());
        Assert.Equal(1, actual.GetProperty("pair_count").GetInt64());
        Assert.Equal("confirmed", final.GetProperty("restart_rule").GetProperty("rule_selection").GetString());
        Assert.Equal([1], Numbers(final.GetProperty("restart_rule").GetProperty("qualifying_pair_repeats")));
    }

    [Fact]
    public async Task Given_MultipleCopyPairsForOneMember_When_Judged_Then_PreservesPairsAndConfirmsRestart()
    {
        var fixture = new B10FinalJudgeFixture("C1");
        for (int index = 0; index < 5; index++) fixture.Repeats[index] = fixture.Repeats[index] with
        { Restart = fixture.Repeats[index].Restart! with { Comparison = "both", Arms =
            [B10FinalJudgeFixture.Arm("same_instance", true) with { PairCount = 6 }, B10FinalJudgeFixture.Arm("cross_restart", true) with { PairCount = 6 }] } };
        using JsonDocument candidate = await fixture.JudgeAsync();
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        JsonElement final = candidate.RootElement.GetProperty("b10_final");
        Assert.All(final.GetProperty("repeats").EnumerateArray(), repeat =>
            Assert.All(repeat.GetProperty("restart").GetProperty("arms").EnumerateArray(), arm =>
            {
                Assert.Equal(6, arm.GetProperty("pair_count").GetInt64());
                Assert.Equal(1, arm.GetProperty("originals_seen").GetInt64());
                Assert.Equal(1, arm.GetProperty("resends_seen").GetInt64());
            }));
        JsonElement rule = final.GetProperty("restart_rule");
        Assert.Equal("confirmed", rule.GetProperty("rule_selection").GetString());
        Assert.Equal(Enumerable.Range(1, 5), Numbers(rule.GetProperty("qualifying_pair_repeats")));
        Assert.Equal(B10FinalJudgeFixture.ExpectedRestartSubject, rule.GetProperty("subject").GetString());
    }

    [Theory]
    [InlineData("wrong-count")]
    [InlineData("ownership-collision")]
    public async Task Given_AuthenticatedNegativeRegistrationJudgement_When_Judged_Then_RetainsProjectionAndHeldRule(string change)
    {
        var fixture = new B10FinalJudgeFixture("C0"); fixture.Publish();
        if (change == "wrong-count")
        {
            fixture.AddOrdinaryRegistration(2, "extra-pod");
            fixture.Repeats[1] = fixture.Repeats[1] with { Status = "invalid", ClassificationFinal = false, Timeout = null,
                ProducerVersions = [], Reasons = ["b10_final_repeat_input_invalid"] };
            fixture.PublishProjection();
        }
        else
        {
            string name = PhasePaths.Registration(B10FinalJudgeFixture.Run, "C0", 2, "timeout-2");
            fixture.MutateLedger(name, node => { node["identity"]!["instance_id"] = "timeout-1"; node["identity"]!["expected_instance_id"] = "timeout-1"; node["pod_name"] = "timeout-1"; });
            fixture.Ledger[PhasePaths.Registration(B10FinalJudgeFixture.Run, "C0", 2, "timeout-1")] = fixture.Ledger[name]; fixture.Ledger.Remove(name);
            for (int index = 0; index < 5; index++) fixture.Repeats[index] = fixture.Repeats[index] with
            { Status = "invalid", ClassificationFinal = false, Timeout = null, Reasons = ["b10_final_ownership_invalid"] };
            fixture.Repeats[1] = fixture.Repeats[1] with { ProducerVersions = [] };
            fixture.PublishProjection(false);
        }
        using JsonDocument candidate = await fixture.JudgeAsync(false);
        Assert.Equal("invalid", candidate.RootElement.GetProperty("status").GetString());
        JsonElement final = candidate.RootElement.GetProperty("b10_final");
        Assert.Equal(JsonValueKind.Object, final.ValueKind);
        Assert.Equal("held", final.GetProperty("timeout_rule").GetProperty("rule_selection").GetString());
        Assert.Equal(JsonValueKind.Null, final.GetProperty("timeout_rule").GetProperty("duplicate_total").ValueKind);
        Assert.DoesNotContain("b10_final_projection_untrusted", Strings(candidate.RootElement.GetProperty("reasons")));
        JsonElement repeat = candidate.RootElement.GetProperty("repeats")[1];
        Assert.False(repeat.GetProperty("checks").GetProperty("j2_publication_unaccepted").GetBoolean());
        Assert.False(repeat.GetProperty("checks").GetProperty("j4_producer_job_binding").GetBoolean());
        Assert.False(repeat.GetProperty("checks").GetProperty("j5_registered_producer_count").GetBoolean());
        Assert.Equal(JsonValueKind.Null, repeat.GetProperty("identity").ValueKind);
    }

    [Theory]
    [InlineData("true", false)]
    [InlineData("false", true)]
    [InlineData("bad-hash", true)]
    [InlineData("extra-object", true)]
    [InlineData("unknown-member", true)]
    public async Task Given_NoA3AndBoundTermination_When_Judged_Then_F4FlagsFollowAuthenticatedSuccessorPermission(string change, bool accepted)
    {
        var fixture = new B10FinalJudgeFixture("C1"); fixture.Publish(); fixture.AttachFirstPublication(change != "false");
        fixture.Verifier.Remove(fixture.SourceName(1)); fixture.Verifier.Remove(fixture.SourceReceiptName(1));
        fixture.Repeats[0] = fixture.Repeats[0] with { Source = null, Restart = fixture.Repeats[0].Restart! with
        { Eligibility = "ineligible", EligibilityReasons = [accepted && change != "false" ? "b10_restart_first_evidence_unavailable" : "b10_restart_input_unavailable"], Comparison = "unavailable", Arms = null } };
        string prefix = $"{B10FinalJudgeFixture.Run}/C1/r1/b10-abrupt/first-1/";
        if (change == "bad-hash") fixture.Ledger[prefix + "journal.jsonl"] = "changed"u8.ToArray();
        if (change == "extra-object") fixture.Ledger[prefix + "extra.json"] = "{}"u8.ToArray();
        if (change == "unknown-member") fixture.MutateLedger(prefix + "termination.json", node => node["data"]!["extra"] = true);
        fixture.PublishProjection();
        using JsonDocument candidate = await fixture.JudgeAsync(false);
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        JsonElement checks = candidate.RootElement.GetProperty("repeats")[0].GetProperty("checks");
        Assert.Equal(accepted, checks.GetProperty("j2_publication_unaccepted").GetBoolean());
        Assert.Equal(accepted, checks.GetProperty("j3_verifier_receipt").GetBoolean());
    }

    [Theory]
    [InlineData("purpose")]
    [InlineData("repeat")]
    public async Task Given_BoundSourceWithInvalidNonfinalEnvelope_When_Judged_Then_F4FlagsAreFalseAndTimeoutRemainsIndependent(string field)
    {
        var fixture = new B10FinalJudgeFixture("C1"); fixture.Publish();
        fixture.Mutate(fixture.SourceName(1), node => { if (field == "purpose") node["purpose"] = "diagnostic_trial"; else node["identity"]!["repeat"] = 2; });
        fixture.Repeats[0] = fixture.Repeats[0] with { Source = B10FinalJudgeFixture.Reference(fixture.SourceName(1), fixture.Verifier[fixture.SourceName(1)]),
            Restart = fixture.Repeats[0].Restart! with { Eligibility = "ineligible", EligibilityReasons = ["b10_restart_input_unavailable"], Comparison = "unavailable", Arms = null } };
        fixture.Mutate(fixture.SourceReceiptName(1), node =>
        {
            node["artifacts"]![0]!["length"] = fixture.Repeats[0].Source!.Length;
            node["artifacts"]![0]!["sha256"] = fixture.Repeats[0].Source!.Sha256;
        });
        fixture.PublishProjection();
        using JsonDocument candidate = await fixture.JudgeAsync(false);
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        JsonElement repeat = candidate.RootElement.GetProperty("repeats")[0];
        Assert.False(repeat.GetProperty("checks").GetProperty("j2_publication_unaccepted").GetBoolean());
        Assert.False(repeat.GetProperty("checks").GetProperty("j3_verifier_receipt").GetBoolean());
        Assert.Equal(1, repeat.GetProperty("counts").GetProperty("ok").GetInt64());
    }

    [Theory]
    [InlineData("missing", true)] [InlineData("malformed", true)] [InlineData("wrong-run", true)]
    [InlineData("future", true)] [InlineData("last-verifier", true)] [InlineData("valid", false)]
    public async Task Given_ClaimedInvalidSweepLine_When_Judged_Then_RetainsHeldProjectionOnlyForIndependentlyInvalidNote(string change, bool trusted)
    {
        var fixture = new B10FinalJudgeFixture("C0"); fixture.Publish();
        string noteName = PhasePaths.SweepLine(B10FinalJudgeFixture.Run, "C0");
        if (change == "missing") fixture.Verifier.Remove(noteName);
        if (change == "malformed") fixture.Verifier[noteName] = "{}"u8.ToArray();
        if (change == "wrong-run") fixture.Mutate(noteName, node => node["run_id"] = "b10-2-2");
        if (change is "future" or "last-verifier") fixture.Mutate(noteName, node =>
        {
            string field = change == "future" ? "captured_at" : "last_verifier_completed_utc";
            DateTimeOffset current = DateTimeOffset.ParseExact(node[field]!.GetValue<string>(), "O", CultureInfo.InvariantCulture);
            node[field] = current.AddTicks(change == "future" ? 1 : -1).ToString("O", CultureInfo.InvariantCulture);
        });
        for (int index = 0; index < 5; index++) fixture.Repeats[index] = fixture.Repeats[index] with
        { Status = "invalid", ClassificationFinal = false, Timeout = null, Reasons = ["b10_final_sweep_line_invalid"] };
        B10FinalProjection projection = new(new(B10FinalJudgeFixture.Run, "B10", "C0", "C0", B10FinalJudgeFixture.Commit, B10FinalJudgeFixture.Image,
            PhaseIdentity.B10FinalSweepJobName(B10FinalJudgeFixture.Run, "C0"), "sweep-pod", "sweep-uid"),
            new(null, false, B10SweepEvidence.ResidualRisk, fixture.Repeats));
        fixture.Verifier[PhasePaths.B10FinalSweep(B10FinalJudgeFixture.Run, "C0")] = B10FinalProjectionCodec.Write(projection); fixture.RebindProjection();
        using JsonDocument candidate = await fixture.JudgeAsync(false);
        Assert.Equal("invalid", candidate.RootElement.GetProperty("status").GetString());
        JsonElement final = candidate.RootElement.GetProperty("b10_final");
        if (trusted)
        {
            Assert.Equal(JsonValueKind.Object, final.ValueKind);
            Assert.Equal("held", final.GetProperty("timeout_rule").GetProperty("rule_selection").GetString());
            Assert.DoesNotContain("b10_final_projection_untrusted", Strings(candidate.RootElement.GetProperty("reasons")));
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, final.ValueKind);
            Assert.Contains("b10_final_projection_untrusted", Strings(candidate.RootElement.GetProperty("reasons")));
        }
    }

    [Fact]
    public async Task Given_ScopedAbruptRegistrationWithReportedBindingFailure_When_Judged_Then_TimeoutClassificationRemainsTrusted()
    {
        var fixture = new B10FinalJudgeFixture("C1"); fixture.Publish();
        fixture.MutateLedger($"{B10FinalJudgeFixture.Run}/C1/r1/b10-abrupt/first-1/registration.json", node => node["identity"]!["job_name"] = "wrong-job");
        fixture.Repeats[0] = fixture.Repeats[0] with { Restart = fixture.Repeats[0].Restart! with
        { Eligibility = "ineligible", EligibilityReasons = ["b10_restart_input_unavailable"], Comparison = "unavailable", Arms = null, FirstInstanceId = null } };
        fixture.PublishProjection();
        using JsonDocument candidate = await fixture.JudgeAsync(false);
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        JsonElement repeat = candidate.RootElement.GetProperty("repeats")[0];
        Assert.Equal(1, repeat.GetProperty("counts").GetProperty("ok").GetInt64());
        Assert.False(repeat.GetProperty("checks").GetProperty("j4_producer_job_binding").GetBoolean());
        Assert.False(repeat.GetProperty("checks").GetProperty("j5_registered_producer_count").GetBoolean());
        Assert.Equal(JsonValueKind.Object, candidate.RootElement.GetProperty("b10_final").ValueKind);
    }

    [Theory]
    [InlineData("equal", false)] [InlineData("different", true)] [InlineData("empty", true)] [InlineData("missing", true)] [InlineData("none", true)]
    public async Task Given_AuthenticatedProducerVersions_When_Judged_Then_UsesCommonObservedVersions(string change, bool unavailable)
    {
        var fixture = new B10FinalJudgeFixture("C0");
        if (change == "none") for (int index = 0; index < 5; index++) fixture.Repeats[index] = fixture.Repeats[index] with { ProducerVersions = [] };
        else if (change != "equal") fixture.Repeats[1] = fixture.Repeats[1] with { ProducerVersions = [fixture.Repeats[1].ProducerVersions[0] with
        { Librdkafka = change == "different" ? "other" : change == "empty" ? "" : null }] };
        using JsonDocument candidate = await fixture.JudgeAsync();
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        Assert.Equal(unavailable ? null : "2.16.0", candidate.RootElement.GetProperty("versions").GetProperty("librdkafka").GetString());
        Assert.Equal(unavailable, Strings(candidate.RootElement.GetProperty("reasons")).Contains("version_unavailable", StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Given_StartedC1WithoutPerRepeatObject_When_Judged_Then_InputShapeFlagsFollowF4RatherThanEligibility(bool missingReceiptOnly)
    {
        var fixture = new B10FinalJudgeFixture("C1");
        fixture.Publish();
        fixture.Verifier.Remove(fixture.SourceReceiptName(1));
        if (!missingReceiptOnly)
        {
            fixture.Verifier.Remove(fixture.SourceName(1));
            fixture.Repeats[0] = fixture.Repeats[0] with { Source = null, Restart = fixture.Repeats[0].Restart! with
            { Eligibility = "ineligible", EligibilityReasons = ["b10_restart_first_evidence_unavailable"], Comparison = "unavailable", Arms = null } };
            fixture.PublishProjection();
        }
        using JsonDocument candidate = await fixture.JudgeAsync(false);
        JsonElement repeat = candidate.RootElement.GetProperty("repeats")[0];
        Assert.Equal("complete_valid", repeat.GetProperty("status").GetString());
        Assert.True(repeat.GetProperty("checks").GetProperty("j1_repeat_slots").GetBoolean());
        Assert.Equal(!missingReceiptOnly, repeat.GetProperty("checks").GetProperty("j2_publication_unaccepted").GetBoolean());
        Assert.Equal(!missingReceiptOnly, repeat.GetProperty("checks").GetProperty("j3_verifier_receipt").GetBoolean());
        Assert.Equal(1, repeat.GetProperty("counts").GetProperty("ok").GetInt64());
    }

    [Fact]
    public async Task Given_TrustedProjection_When_Judged_Then_CopiesEveryCheckedRepeatAndOmitsEnvelopeAndDiagnosticFields()
    {
        var fixture = new B10FinalJudgeFixture("C1");
        using JsonDocument candidate = await fixture.JudgeAsync();
        using JsonDocument projection = JsonDocument.Parse(fixture.Verifier[PhasePaths.B10FinalSweep(B10FinalJudgeFixture.Run, "C1")]);
        Assert.True(JsonElement.DeepEquals(projection.RootElement.GetProperty("data").GetProperty("repeats"),
            candidate.RootElement.GetProperty("b10_final").GetProperty("repeats")));
        Assert.DoesNotContain("dialect", fixture.CandidateText, StringComparison.Ordinal);
        Assert.DoesNotContain("purpose", fixture.CandidateText, StringComparison.Ordinal);
        Assert.DoesNotContain("scientific_admissible", fixture.CandidateText, StringComparison.Ordinal);
        foreach (JsonElement repeat in candidate.RootElement.GetProperty("repeats").EnumerateArray())
        {
            foreach (string field in new[] { "b10_intake", "b10_producer_observations", "broker_count", "b04_recorded_evidence", "batching_recorded_evidence" }) Assert.False(repeat.TryGetProperty(field, out _));
            Assert.Equal(JsonValueKind.Null, repeat.GetProperty("probe_observation").ValueKind);
            Assert.Equal(JsonValueKind.Null, repeat.GetProperty("workload_observation").ValueKind);
        }
        // TP3 consumes these actual Judge bytes from the external phase acceptance command.
        string? export = Environment.GetEnvironmentVariable("B10_FINAL_T17_EXPORT");
        if (export is not null) await File.WriteAllBytesAsync(export, fixture.Verifier[fixture.Options.CandidateOutputName!], TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("verify")]
    public async Task Given_OptionalMetadataReadUnavailable_When_Judged_Then_RetainsTrustedClassificationAndKnownFlags(string role)
    {
        var fixture = new B10FinalJudgeFixture("C0"); fixture.Publish();
        fixture.FailingVerifierRead = role == "start" ? PhasePaths.StartLineCompletion(B10FinalJudgeFixture.Run, "C0", 1) : fixture.SourceReceiptName(1);
        using JsonDocument candidate = await fixture.JudgeAsync(false);
        Assert.Equal("complete_valid", candidate.RootElement.GetProperty("status").GetString());
        JsonElement checks = candidate.RootElement.GetProperty("repeats")[0].GetProperty("checks");
        if (role == "start") Assert.True(checks.GetProperty("j1_repeat_slots").GetBoolean());
        else Assert.False(checks.GetProperty("j3_verifier_receipt").GetBoolean());
    }

    [Theory]
    [InlineData("rule")] [InlineData("phase-total")] [InlineData("status")] [InlineData("repeat-status")]
    [InlineData("repeat-counts")] [InlineData("unknown-member")] [InlineData("final-reference")] [InlineData("null-final")]
    public async Task Given_CandidateProjectionDisagreement_When_CodecReads_Then_Rejects(string change)
    {
        var fixture = new B10FinalJudgeFixture("C0");
        using JsonDocument candidate = await fixture.JudgeAsync();
        JsonObject node = JsonNode.Parse(fixture.CandidateText)!.AsObject();
        if (change == "rule") node["b10_final"]!["timeout_rule"]!["rule_selection"] = "confirmed";
        if (change == "phase-total") node["counts"]!["ok"] = 6;
        if (change == "status") node["status"] = "incomplete";
        if (change == "repeat-status") node["repeats"]![0]!["status"] = "invalid";
        if (change == "repeat-counts") node["repeats"]![0]!["counts"]!["ok"] = 2;
        if (change == "unknown-member") node["b10_final"]!["dialect"] = "b10_abrupt";
        if (change == "final-reference") node["b10_final"]!["projection"]!["name"] = "wrong";
        if (change == "null-final") node["b10_final"] = null;
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(Encoding.UTF8.GetBytes(node.ToJsonString())));
    }

    [Theory]
    [InlineData("synthetic-c5")] [InlineData("b10d-1-1")]
    public async Task Given_NonScientificRunId_When_Judged_Then_UsesLegacySweepProjection(string run)
    {
        var fixture = new B10FinalJudgeFixture("C0");
        fixture.Options.RunId = run; fixture.Options.CandidateOutputName = PhasePaths.Candidate(run, "C0"); fixture.Options.JobName = PhaseIdentity.JudgeJobName(run, "C0");
        using JsonDocument candidate = await fixture.JudgeAsync(false);
        Assert.True(candidate.RootElement.TryGetProperty("b10_sweep", out _));
        Assert.False(candidate.RootElement.TryGetProperty("b10_final", out _));
    }

    private static string[] Strings(JsonElement value) => value.EnumerateArray().Select(x => x.GetString()!).ToArray();
    private static int[] Numbers(JsonElement value) => value.EnumerateArray().Select(x => x.GetInt32()).ToArray();
}

internal sealed class B10FinalJudgeFixture : IVerifierArtifactStore, IProducerArtifactStore
{
    private static readonly string[] ReportNames = ["same_instance-pre-acceptance-report.json", "control-post-drain-report.json", "cross_restart-pre-acceptance-report.json"];
    internal const string ExpectedRestartSubject = "With idempotence enabled, application recovery that replaces a killed producer and resends a durably identified but unconfirmed logical message can leave both the original and the resend in the log.";
    internal const string Run = "b10-1-1";
    internal static readonly string Commit = new('a', 40);
    internal static readonly string Loadgen = "example/loadgen@sha256:" + new string('1', 64);
    internal static readonly string Image = "example/verifier@sha256:" + new string('2', 64);
    private static readonly string Timestamp = new DateTimeOffset(2026, 11, 13, 0, 0, 0, TimeSpan.Zero).ToString("O", CultureInfo.InvariantCulture);
    internal VerifierHostOptions Options { get; }
    internal Dictionary<string, byte[]> Verifier { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, byte[]> Ledger { get; } = new(StringComparer.Ordinal);
    internal B10FinalRepeat[] Repeats { get; }
    internal string? IncompletePrefix { get; set; }
    internal string? FailingVerifierRead { get; set; }
    internal string CandidateText => Encoding.UTF8.GetString(Verifier[Options.CandidateOutputName!]);
    internal B10FinalJudgeFixture(string phase)
    {
        Options = new() { Mode = "judge", ScenarioId = "B10", ConfigurationId = phase, Phase = phase, RunId = Run, SourceCommit = Commit,
            VerifierCommit = Commit, LoadGenImage = Loadgen, VerifierImage = Image, JobName = PhaseIdentity.JudgeJobName(Run, phase),
            PodName = "judge-pod", PodUid = "judge-uid", IoTimeout = TimeSpan.FromSeconds(5), VerifierContainer = "verifier", CandidateOutputName = PhasePaths.Candidate(Run, phase) };
        Repeats = Enumerable.Range(1, 5).Select(repeat => new B10FinalRepeat(repeat, null,
            [new("timeout-" + repeat, "synthetic-generator", "2.16.0", "2.16.0", "10.0.0")], true, "complete_valid", new(1, 0, 0, 0, 0, 0), [],
            phase == "C0" ? null : new("eligible", [], new("intended_self_kill", null), "first-" + repeat, "successor-" + repeat, "timeout-" + repeat,
                "neither", new(0, 0, "unknown_due_to_intended_kill"), [Arm("same_instance"), Arm("cross_restart")]))).ToArray();
    }
    internal static B10FinalArm Arm(string arm, bool pair = false, bool loss = false) => new(arm, "original", ["original"], 1, 1,
        pair ? 1 : 0, pair ? 1 : 0, pair ? 1 : 0, arm == "same_instance" ? 0 : 1,
        arm == "cross_restart" && pair ? 1 : 0, arm == "cross_restart" && !pair ? 1 : 0, loss ? 1 : 0,
        pair ? new("original", "resend", new(0, 0), new(0, 1)) : null);
    internal static B10FinalArm EmptyArm() => new("cross_restart", "cut", [], 0, 0, 0, 0, 0, 0, 0, 0, 0, null);
    internal string SourceName(int repeat) => Options.Phase == "C0" ? PhasePaths.Result(Run, "C0", repeat) : $"{Run}/C1/r{repeat}/b10-abrupt/nonfinal-result.json";
    internal string SourceReceiptName(int repeat) => Options.Phase == "C0" ? PhasePaths.VerifyCompletion(Run, "C0", repeat) : $"{Run}/C1/r{repeat}/b10-abrupt/verify-completion.json";
    internal B10FinalProjection Projection() => new(new(Run, "B10", Options.Phase!, Options.Phase!, Commit, Image,
        PhaseIdentity.B10FinalSweepJobName(Run, Options.Phase!), "sweep-pod", "sweep-uid"),
        new(Reference(PhasePaths.SweepLine(Run, Options.Phase!), Verifier[PhasePaths.SweepLine(Run, Options.Phase!)]), true, B10SweepEvidence.ResidualRisk, Repeats));
    internal void Publish()
    {
        string phase = Options.Phase!;
        DateTimeOffset lineTime = DateTimeOffset.ParseExact(Timestamp, "O", CultureInfo.InvariantCulture);
        Verifier[PhasePaths.SweepLine(Run, phase)] = B10SweepEvidenceCodec.WriteSweepLine(new(1, "B10", phase, Run, phase, Commit,
            "document-events", [new(0, 1)], lineTime, lineTime, lineTime.AddSeconds(-60), 60000));
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            if (Repeats[repeat - 1].Reasons.Contains("b10_final_repeat_not_started", StringComparer.Ordinal))
            {
                Repeats[repeat - 1] = Repeats[repeat - 1] with { Source = null, ProducerVersions = [] };
                continue;
            }
            string instance = "timeout-" + repeat, job = PhaseIdentity.JobName(Run, phase, repeat);
            var identity = new RunIdentity("B10", phase, Run, instance, instance, job);
            Ledger[PhasePaths.Registration(Run, phase, repeat, instance)] = PhaseWireCodec.WriteRegistration(new(1, identity, phase, repeat, job, instance, "uid-" + repeat, Commit));
            if (phase == "C1")
            {
                foreach ((string pod, string? predecessor) in new[] { ("first-" + repeat, (string?)null), ("successor-" + repeat, "first-" + repeat) })
                {
                    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { schema_version = 3, dialect = "b10_abrupt", purpose = "scientific", synthetic = true, scientific_admissible = false, kind = "registration",
                        identity = new { run_id = Run, scenario_id = "B10", configuration_id = "C1", phase = "C1", repeat, source_commit = Commit, image_reference = Loadgen,
                            job_name = $"{Run}-c1-r{repeat}-loadgen-" + (predecessor is null ? "restart" : "successor"), pod_name = pod, pod_uid = "uid-" + pod,
                            instance_id = pod, predecessor_instance_id = predecessor }, data = new { registered_utc = Timestamp } });
                    Ledger[$"{Run}/C1/r{repeat}/b10-abrupt/{pod}/registration.json"] = bytes;
                }
            }
            Verifier[SourceName(repeat)] = phase == "C0" ? "{\"status\":\"incomplete\",\"b10_window_checks_complete\":true,\"start_line\":[{\"partition\":0,\"offset\":0}],\"failed_checks\":[]}"u8.ToArray() :
                JsonSerializer.SerializeToUtf8Bytes(new { schema_version = 3, dialect = "b10_abrupt", purpose = "scientific", synthetic = true, scientific_admissible = false, kind = "nonfinal_result",
                    identity = new { run_id = Run, scenario_id = "B10", configuration_id = "C1", phase = "C1", repeat, source_commit = Commit, image_reference = Image,
                        job_name = $"{Run}-c1-r{repeat}-b10-abrupt-verify", pod_name = "verify-pod", pod_uid = "verify-uid", instance_id = "verify-pod", predecessor_instance_id = (string?)null },
                    data = new { first_instance_id = "first-" + repeat, successor_instance_id = "successor-" + repeat, timeout_instance_id = instance, timeout = new OutcomeCounts(1, 0, 0, 0, 0, 0),
                        comparison = "neither", native_errors = new B10FinalNativeErrors(0, 0, "unknown_due_to_intended_kill"), eligibility = "pending_sweep", eligibility_reasons = Array.Empty<string>(),
                        start_line = new[] { new B10FinalCoordinate(0, 0) }, end_line = new[] { new B10FinalCoordinate(0, 1) }, window_complete = true,
                        window_limit = "copies_after_end_line_not_observed", arms = (object?)null, problems = Array.Empty<string>(), problem_details = Array.Empty<object>(), finality = "nonfinal", earlier_repeat_records = 0 } },
                    B10FinalProjectionCodec.JsonOptions);
            Repeats[repeat - 1] = Repeats[repeat - 1] with { Source = Reference(SourceName(repeat), Verifier[SourceName(repeat)]) };
            var intake = phase == "C0" ? new B10IntakeEvidence(phase, repeat, [new(identity, job, null, null)]) : null;
            var receipt = new WorkerReceipt(1, phase == "C0" ? "verify" : "b10-abrupt-verify", "B10", phase, Run, phase, repeat,
                phase == "C0" ? PhaseIdentity.VerifyJobName(Run, phase, repeat) : $"{Run}-c1-r{repeat}-b10-abrupt-verify", "verify-pod", phase == "C0" ? identity : null,
                Commit, phase == "C0" ? 1 : 0, true, Timestamp, Timestamp, [Reference(SourceName(repeat), Verifier[SourceName(repeat)])], null,
                phase == "C0" ? new("pass", "pass", "pass") : null, intake);
            Verifier[SourceReceiptName(repeat)] = PhaseWireCodec.WriteReceipt(receipt);
            // The start-line receipt binds fixed paths; this Judge only needs its independently valid timestamp and existence.
            var startIdentity = new RunIdentity("B10", phase, Run, job, job, job);
            var startReceipt = new WorkerReceipt(1, "start-line", "B10", phase, Run, phase, repeat, PhaseIdentity.StartLineJobName(Run, phase, repeat),
                "start-pod", startIdentity, Commit, 0, true, Timestamp, Timestamp,
                [Reference(PhasePaths.StartLine(Run, phase, repeat), []), Reference(PhasePaths.BrokerInventory(Run, phase, repeat), [])], null, null);
            Verifier[PhasePaths.StartLineCompletion(Run, phase, repeat)] = PhaseWireCodec.WriteReceipt(startReceipt);
        }
        PublishProjection();
    }
    internal void PublishProjection(bool scanComplete = true)
    {
        string projectionName = PhasePaths.B10FinalSweep(Run, Options.Phase!);
        B10FinalProjection projection = Projection();
        Verifier[projectionName] = B10FinalProjectionCodec.Write(projection with { Data = projection.Data with { ScanComplete = scanComplete } });
        RebindProjection();
    }
    internal void AddOrdinaryRegistration(int repeat, string instance)
    {
        string phase = Options.Phase!, job = PhaseIdentity.JobName(Run, phase, repeat);
        var identity = new RunIdentity("B10", phase, Run, instance, instance, job);
        Ledger[PhasePaths.Registration(Run, phase, repeat, instance)] = PhaseWireCodec.WriteRegistration(new(1, identity, phase, repeat, job, instance, "uid-" + instance, Commit));
    }
    internal void AttachFirstPublication(bool permitted)
    {
        var evidence = new B10AbruptVerifyEvidence();
        string prefix = $"{Run}/C1/r1/b10-abrupt/first-1/";
        JsonNode identity = JsonNode.Parse(Ledger[prefix + "registration.json"])!["identity"]!.DeepClone();
        JsonObject Scientific(JsonObject root) { root["purpose"] = "scientific"; root["identity"] = identity.DeepClone(); return root; }
        foreach ((string name, byte[] bytes) in evidence.Files.Where(pair => pair.Key.StartsWith("first/", StringComparison.Ordinal)))
        {
            string file = name["first/".Length..];
            if (file == "journal.jsonl")
            {
                string journal = string.Concat(Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => Scientific(JsonNode.Parse(line)!.AsObject()).ToJsonString() + "\n"));
                Ledger[prefix + file] = Encoding.UTF8.GetBytes(journal);
            }
            else
            {
                JsonObject root = JsonNode.Parse(bytes)!.AsObject();
                if (root["dialect"]?.GetValue<string>() == "b10_abrupt") Scientific(root);
                Ledger[prefix + file] = Encoding.UTF8.GetBytes(root.ToJsonString());
            }
        }
        JsonObject Descriptor(string name) => new() { ["name"] = name, ["length"] = Ledger[prefix + name].LongLength,
            ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(Ledger[prefix + name])) };
        MutateLedger(prefix + "abrupt-manifest.json", node =>
        {
            node["data"]!["journal"] = Descriptor("journal.jsonl");
            node["data"]!["report_files"] = new JsonArray(ReportNames.Select(name => (JsonNode)Descriptor(name)).ToArray());
        });
        MutateLedger(prefix + "termination.json", node => { node["data"]!["manifest"] = Descriptor("abrupt-manifest.json"); node["data"]!["successor_permitted"] = permitted; });
    }
    internal void RebindProjection()
    {
        string phase = Options.Phase!, name = PhasePaths.B10FinalSweep(Run, phase);
        var receipt = new WorkerReceipt(1, "b10-final-sweep", "B10", phase, Run, phase, null, PhaseIdentity.B10FinalSweepJobName(Run, phase), "sweep-pod", null,
            Commit, 0, true, Timestamp, Timestamp, [Reference(name, Verifier[name])], null, null);
        Verifier[PhasePaths.B10FinalSweepCompletion(Run, phase)] = PhaseWireCodec.WriteReceipt(receipt);
    }
    internal async Task<JsonDocument> JudgeAsync(bool publish = true)
    {
        if (publish) Publish();
        var handler = new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options), this, this, TimeProvider.System);
        int exit = await handler.ExecuteAsync(TestContext.Current.CancellationToken);
        JsonDocument document = PhaseCandidateCodec.Read(Verifier[Options.CandidateOutputName!]);
        Assert.Equal(document.RootElement.GetProperty("status").GetString() == "complete_valid" ? 0 : 1, exit);
        return document;
    }
    internal void Mutate(string name, Action<JsonObject> mutate) => MutateObject(Verifier, name, mutate);
    internal void MutateLedger(string name, Action<JsonObject> mutate) => MutateObject(Ledger, name, mutate);
    private static void MutateObject(Dictionary<string, byte[]> objects, string name, Action<JsonObject> mutate)
    { JsonObject node = JsonNode.Parse(objects[name])!.AsObject(); mutate(node); objects[name] = Encoding.UTF8.GetBytes(node.ToJsonString()); }
    internal static ArtifactReference Reference(string name, byte[] bytes) => new("verifier", name, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    Task<byte[]?> IVerifierArtifactStore.ReadAsync(string name, CancellationToken cancellationToken) => name == FailingVerifierRead
        ? throw new TimeoutException("Synthetic optional metadata unavailable.") : Task.FromResult(Verifier.GetValueOrDefault(name));
    Task<BlobListing> IVerifierArtifactStore.ListAsync(string prefix, CancellationToken cancellationToken) => Task.FromResult(new BlobListing(Verifier.Keys.Where(x => x.StartsWith(prefix, StringComparison.Ordinal)).ToArray(), 1, true));
    Task IVerifierArtifactStore.CreateAsync(string name, byte[] content, CancellationToken cancellationToken) { Verifier.Add(name, content); return Task.CompletedTask; }
    Task<byte[]?> IProducerArtifactStore.ReadAsync(string name, CancellationToken cancellationToken) => Task.FromResult(Ledger.GetValueOrDefault(name));
    Task<BlobListing> IProducerArtifactStore.ListAsync(string prefix, CancellationToken cancellationToken) => Task.FromResult(new BlobListing(Ledger.Keys.Where(x => x.StartsWith(prefix, StringComparison.Ordinal)).ToArray(), 1, IncompletePrefix != prefix));
}
#pragma warning restore CA1707
