using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Judge;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

#pragma warning disable CA1707 // Repository test naming convention.
// Closed final projections must preserve unknowns and reject the old nonfinal diagnostic shape.
public sealed class B10FinalProjectionCodecTests
{
    private static readonly InputLimits Limits = new(32, 16_777_216, 83_886_080, 1_048_576, 100_000, 1_000_000, 65_536);

    [Fact]
    public void Given_ProducedFinalProjection_When_OldReadersRead_Then_AllRejectTheNewKind()
    {
        var fixture = Fixture(); byte[] bytes = B10FinalProjectionCodec.Write(fixture.Projection());
        Assert.False(VerifierResultCodec.Read(bytes, "final", Limits, 16).IsSuccess);
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(bytes, Limits));
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(bytes));
    }

    [Fact]
    public void Given_FinalProjection_When_Written_Then_NoMemberTablesOrNonfinalExtrasRemain()
    {
        var fixture = Fixture(); byte[] bytes = B10FinalProjectionCodec.Write(fixture.Projection());
        using JsonDocument document = JsonDocument.Parse(bytes);
        var forbidden = new HashSet<string>(["members", "copies", "copies_truncated", "copy_count", "original_copies", "resend_copies", "other_copies",
            "resend_terminal", "problems", "problem_details", "window_limit", "start_line", "end_line", "window_complete", "earlier_repeat_records", "finality"], StringComparer.Ordinal);
        Assert.DoesNotContain(Names(document.RootElement), forbidden.Contains);
        Assert.Equal("final_sweep_projection", document.RootElement.GetProperty("kind").GetString());
        Assert.True(document.RootElement.GetProperty("scientific_admissible").GetBoolean());
    }

    [Theory]
    [InlineData("members")] [InlineData("copies")] [InlineData("copies_truncated")] [InlineData("copy_count")]
    [InlineData("original_copies")] [InlineData("resend_copies")] [InlineData("other_copies")] [InlineData("resend_terminal")]
    [InlineData("problems")] [InlineData("problem_details")] [InlineData("window_limit")] [InlineData("start_line")]
    [InlineData("end_line")] [InlineData("window_complete")] [InlineData("earlier_repeat_records")] [InlineData("finality")]
    public void Given_NonfinalExtra_When_ProjectionRead_Then_RejectsUnknownMember(string name)
    {
        JsonObject root = Node(); root["data"]!["repeats"]![0]!["restart"]!["arms"]![0]![name] = null;
        Assert.Throws<FormatException>(() => Read(root));
    }

    [Theory]
    [InlineData(-1L, false)] [InlineData(0L, true)] [InlineData(1L, true)] [InlineData(long.MaxValue, true)]
    public void Given_Int64CountBoundary_When_Read_Then_RequiresNonnegativeInteger(long count, bool valid)
    {
        JsonObject root = Node(); root["data"]!["repeats"]![0]!["timeout"]!["ok"] = count;
        if (valid) Assert.Equal(count, Read(root).Data.Repeats[0].Timeout!.Ok);
        else Assert.Throws<FormatException>(() => Read(root));
    }

    [Theory]
    [InlineData("9223372036854775808")] [InlineData("0.5")] [InlineData("null")] [InlineData("true")] [InlineData("\"1\"")]
    public void Given_NonInt64OrUnknownKnownTimeout_When_Read_Then_Rejects(string literal)
    {
        JsonObject root = Node(); root["data"]!["repeats"]![0]!["timeout"]!["ok"] = JsonNode.Parse(literal);
        Assert.Throws<FormatException>(() => Read(root));
    }

    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(6)]
    public void Given_WrongRepeatSlotCount_When_Read_Then_Rejects(int count)
    {
        JsonObject root = Node(); var repeats = root["data"]!["repeats"]!.AsArray();
        JsonNode template = repeats[0]!.DeepClone();
        while (repeats.Count > count) repeats.RemoveAt(repeats.Count - 1);
        while (repeats.Count < count) repeats.Add(template.DeepClone());
        Assert.Throws<FormatException>(() => Read(root));
    }

    [Theory]
    [InlineData(255, true)] [InlineData(256, true)] [InlineData(257, false)]
    public void Given_AsciiTextBoundary_When_Read_Then_AppliesScalarByteLimit(int length, bool valid)
    {
        JsonObject root = Node(); root["identity"]!["pod_uid"] = new string('x', length);
        if (valid) Assert.Equal(length, Read(root).Identity.PodUid.Length);
        else Assert.Throws<FormatException>(() => Read(root));
    }

    [Theory]
    [InlineData("unknown-root")] [InlineData("unknown-data")] [InlineData("unknown-repeat")] [InlineData("unknown-identity")]
    [InlineData("non-ascii")] [InlineData("missing")] [InlineData("schema")] [InlineData("dialect")]
    [InlineData("purpose")] [InlineData("synthetic")] [InlineData("admission")]
    [InlineData("repeat-order")] [InlineData("reason-order")] [InlineData("unknown-reason")]
    [InlineData("finality")] [InlineData("unknown-status")] [InlineData("missing-timeout")]
    [InlineData("source")] [InlineData("sweep-reference")] [InlineData("versions-order")]
    [InlineData("arm-order")] [InlineData("arm-count")] [InlineData("unknown-arm")]
    [InlineData("unconfirmed")] [InlineData("resend-invoked")] [InlineData("originals-seen")]
    [InlineData("resends-seen")] [InlineData("unreported-sum")] [InlineData("loss-bound")]
    [InlineData("native-count")] [InlineData("termination-class")] [InlineData("termination-outcome")]
    [InlineData("eligibility-reason")] [InlineData("eligibility-order")] [InlineData("eligibility-duplicate")]
    [InlineData("eligible-reason")] [InlineData("comparison")]
    public void Given_ClosedFormatViolation_When_Read_Then_Rejects(string change)
    {
        JsonObject root = Node(); JsonObject repeat = root["data"]!["repeats"]![0]!.AsObject();
        JsonObject restart = repeat["restart"]!.AsObject(); JsonObject arm = restart["arms"]![0]!.AsObject();
        if (change is "reason-order" or "unknown-reason") { repeat["status"] = "invalid"; repeat["classification_final"] = false; repeat["timeout"] = null; }
        switch (change)
        {
            case "unknown-root": root["extra"] = true; break;
            case "unknown-data": root["data"]!["extra"] = true; break;
            case "unknown-repeat": repeat["extra"] = true; break;
            case "unknown-identity": root["identity"]!["extra"] = true; break;
            case "non-ascii": root["identity"]!["pod_uid"] = "é"; break;
            case "missing": root.Remove("purpose"); break;
            case "schema": root["schema_version"] = 2; break;
            case "dialect": root["dialect"] = "legacy"; break;
            case "purpose": root["purpose"] = "diagnostic_trial"; break;
            case "synthetic": root["synthetic"] = false; break;
            case "admission": root["scientific_admissible"] = false; break;
            case "repeat-order": repeat["repeat"] = 2; break;
            case "reason-order": repeat["reasons"] = new JsonArray("b10_final_window_mismatch", "b10_final_drain_incomplete"); break;
            case "unknown-reason": repeat["reasons"] = new JsonArray("unknown"); break;
            case "finality": repeat["classification_final"] = false; break;
            case "unknown-status": repeat["status"] = "complete"; break;
            case "missing-timeout": repeat["timeout"] = null; break;
            case "source": repeat["source"]!["name"] = "wrong"; break;
            case "sweep-reference": root["data"]!["sweep_line"]!["sha256"] = new string('A', 64); break;
            case "versions-order": repeat["producer_versions"]!.AsArray().Add(repeat["producer_versions"]![0]!.DeepClone()); break;
            case "arm-order": arm["arm"] = "cross_restart"; break;
            case "arm-count": restart["arms"]!.AsArray().RemoveAt(1); break;
            case "unknown-arm": arm["arm"] = "other"; break;
            case "unconfirmed": arm["unconfirmed_count"] = 2; break;
            case "resend-invoked": arm["resend_invoked"] = 2; break;
            case "originals-seen": arm["originals_seen"] = 2; break;
            case "resends-seen": arm["resends_seen"] = 2; break;
            case "unreported-sum": arm["unreported_unseen"] = 2; break;
            case "loss-bound": arm["lost_resends"] = 2; break;
            case "native-count": restart["native_errors"]!["fatal_count"] = 1; break;
            case "termination-class": restart["termination"]!["class"] = "crash"; break;
            case "termination-outcome": restart["termination"]!["outcome"] = "kill_too_early"; break;
            case "eligibility-reason": restart["eligibility_reasons"] = new JsonArray("unknown"); restart["eligibility"] = "ineligible"; break;
            case "eligibility-order": restart["eligibility_reasons"] = new JsonArray("b10_restart_input_unavailable", "b10_restart_sweep_unavailable"); restart["eligibility"] = "ineligible"; break;
            case "eligibility-duplicate": restart["eligibility_reasons"] = new JsonArray("b10_restart_guard_failed", "b10_restart_guard_failed"); restart["eligibility"] = "ineligible"; break;
            case "eligible-reason": restart["eligibility_reasons"] = new JsonArray("b10_restart_guard_failed"); break;
            case "comparison": restart["comparison"] = "other"; break;
        }
        Assert.Throws<FormatException>(() => Read(root));
    }

    [Theory]
    [InlineData("same-coordinate")] [InlineData("nonmember")] [InlineData("same-attempt")]
    [InlineData("negative-partition")] [InlineData("negative-offset")] [InlineData("zero-pairs")]
    public void Given_InvalidWitness_When_Read_Then_Rejects(string change)
    {
        var fixture = Fixture(); fixture.Repeats[0] = fixture.Repeats[0] with { Restart = fixture.Repeats[0].Restart! with
        { Comparison = "same_instance_only", Arms = [B10FinalJudgeFixture.Arm("same_instance", true), B10FinalJudgeFixture.Arm("cross_restart")] } };
        JsonObject root = JsonNode.Parse(B10FinalProjectionCodec.Write(fixture.Projection()))!.AsObject();
        JsonObject arm = root["data"]!["repeats"]![0]!["restart"]!["arms"]![0]!.AsObject();
        JsonObject witness = arm["witness"]!.AsObject();
        if (change == "same-coordinate") witness["resend"] = witness["original"]!.DeepClone();
        if (change == "nonmember") witness["original_attempt_id"] = "other";
        if (change == "same-attempt") witness["resend_attempt_id"] = "original";
        if (change == "negative-partition") witness["original"]!["partition"] = -1;
        if (change == "negative-offset") witness["original"]!["offset"] = -1;
        if (change == "zero-pairs") { arm["pair_count"] = 0; root["data"]!["repeats"]![0]!["restart"]!["comparison"] = "neither"; }
        Assert.Throws<FormatException>(() => Read(root));
    }

    [Theory]
    [InlineData(0, false, false)] [InlineData(0, false, true)]
    [InlineData(0, true, false)] [InlineData(0, true, true)]
    [InlineData(1, false, false)] [InlineData(1, false, true)]
    [InlineData(1, true, false)] [InlineData(1, true, true)]
    public void Given_UnknownPairEvidence_When_Read_Then_RejectsAvailableComparisonAndEligibility(int armIndex, bool otherPair, bool eligible)
    {
        var fixture = Fixture();
        B10FinalArm[] arms = [B10FinalJudgeFixture.Arm("same_instance", armIndex == 1 && otherPair),
            B10FinalJudgeFixture.Arm("cross_restart", armIndex == 0 && otherPair)];
        string knownComparison = otherPair ? armIndex == 0 ? "cross_restart_only" : "same_instance_only" : "neither";
        fixture.Repeats[0] = fixture.Repeats[0] with { Restart = fixture.Repeats[0].Restart! with { Comparison = knownComparison, Arms = arms } };
        JsonObject baseline = JsonNode.Parse(B10FinalProjectionCodec.Write(fixture.Projection()))!.AsObject();
        string[] comparisons = eligible ? ["neither", "same_instance_only", "cross_restart_only", "both", "unavailable"] :
            ["neither", "same_instance_only", "cross_restart_only", "both"];
        foreach (string comparison in comparisons)
        {
            JsonObject root = baseline.DeepClone().AsObject();
            JsonObject restart = root["data"]!["repeats"]![0]!["restart"]!.AsObject();
            restart["arms"]![armIndex]!["pair_count"] = null;
            restart["comparison"] = comparison;
            restart["eligibility"] = eligible ? "eligible" : "ineligible";
            restart["eligibility_reasons"] = eligible ? new JsonArray() : new JsonArray("b10_restart_guard_failed");
            Assert.Throws<FormatException>(() => Read(root));
        }
    }

    [Theory]
    [InlineData(0L, 0L, "neither")]
    [InlineData(1L, 0L, "same_instance_only")] [InlineData(0L, 1L, "cross_restart_only")]
    [InlineData(1L, 1L, "both")]
    [InlineData(2L, 0L, "same_instance_only")] [InlineData(0L, 2L, "cross_restart_only")]
    [InlineData(2L, 2L, "both")]
    public void Given_KnownPairCounts_When_Read_Then_RetainsAvailableComparison(long samePairs, long crossPairs, string comparison)
    {
        var fixture = Fixture();
        fixture.Repeats[0] = fixture.Repeats[0] with { Restart = fixture.Repeats[0].Restart! with { Comparison = comparison,
            Arms = [B10FinalJudgeFixture.Arm("same_instance", samePairs > 0) with { PairCount = samePairs },
                B10FinalJudgeFixture.Arm("cross_restart", crossPairs > 0) with { PairCount = crossPairs }] } };
        B10FinalRestart actual = Read(JsonNode.Parse(B10FinalProjectionCodec.Write(fixture.Projection()))!.AsObject()).Data.Repeats[0].Restart!;
        Assert.Equal(comparison, actual.Comparison);
        Assert.Equal("eligible", actual.Eligibility);
        Assert.Equal(samePairs, actual.Arms![0].PairCount);
        Assert.Equal(crossPairs, actual.Arms[1].PairCount);
    }

    [Fact]
    public void Given_UnavailableIneligibleUnknownPairs_When_Read_Then_PreservesUnknownEvidence()
    {
        JsonObject root = LocalUnjoined();
        B10FinalRestart actual = Read(root).Data.Repeats[0].Restart!;
        Assert.Equal("unavailable", actual.Comparison);
        Assert.Equal("ineligible", actual.Eligibility);
        Assert.Equal(["b10_restart_late_record_unjoined"], actual.EligibilityReasons);
        Assert.All(actual.Arms!, arm => Assert.Null(arm.PairCount));
    }

    [Theory]
    [InlineData(0, "originals_seen", 0L)] [InlineData(0, "originals_seen", null)]
    [InlineData(0, "resends_seen", 0L)] [InlineData(0, "resends_seen", null)]
    [InlineData(1, "originals_seen", 0L)] [InlineData(1, "originals_seen", null)]
    [InlineData(1, "resends_seen", 0L)] [InlineData(1, "resends_seen", null)]
    public void Given_PositivePairsWithoutKnownSeenMember_When_Read_Then_Rejects(int armIndex, string field, long? count)
    {
        var fixture = Fixture();
        fixture.Repeats[0] = fixture.Repeats[0] with { Restart = fixture.Repeats[0].Restart! with
        { Comparison = "both", Arms = [B10FinalJudgeFixture.Arm("same_instance", true), B10FinalJudgeFixture.Arm("cross_restart", true)] } };
        JsonObject root = JsonNode.Parse(B10FinalProjectionCodec.Write(fixture.Projection()))!.AsObject();
        root["data"]!["repeats"]![0]!["restart"]!["arms"]![armIndex]![field] = count;
        Assert.Throws<FormatException>(() => Read(root));
    }

    [Theory]
    [InlineData(0, "originals_seen", 1L)] [InlineData(0, "originals_seen", 2L)]
    [InlineData(0, "resends_seen", 1L)] [InlineData(0, "resends_seen", 2L)]
    [InlineData(1, "originals_seen", 1L)] [InlineData(1, "originals_seen", 2L)]
    [InlineData(1, "resends_seen", 1L)] [InlineData(1, "resends_seen", 2L)]
    public void Given_PositivePairsWithSeenMemberBoundary_When_Read_Then_AcceptsAtAndAboveOne(int armIndex, string field, long count)
    {
        var fixture = Fixture();
        fixture.Repeats[0] = fixture.Repeats[0] with { Restart = fixture.Repeats[0].Restart! with
        { Comparison = "both", Arms = [B10FinalJudgeFixture.Arm("same_instance", true), B10FinalJudgeFixture.Arm("cross_restart", true)] } };
        JsonObject root = JsonNode.Parse(B10FinalProjectionCodec.Write(fixture.Projection()))!.AsObject();
        JsonObject arm = root["data"]!["repeats"]![0]!["restart"]!["arms"]![armIndex]!.AsObject();
        arm["unconfirmed_attempt_ids"] = new JsonArray("original", "other-original");
        arm["unconfirmed_count"] = 2; arm["resend_invoked"] = 2;
        arm[field] = count;
        B10FinalArm actual = Read(root).Data.Repeats[0].Restart!.Arms![armIndex];
        Assert.Equal(count, field == "originals_seen" ? actual.OriginalsSeen : actual.ResendsSeen);
        Assert.Equal(1, actual.PairCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void Given_MultipleCopyPairsForOneMember_When_Read_Then_PairCountMayExceedSeenMemberCounts(int armIndex)
    {
        var fixture = Fixture();
        B10FinalArm[] arms = [B10FinalJudgeFixture.Arm("same_instance", true), B10FinalJudgeFixture.Arm("cross_restart", true)];
        // One member with two original copies and three resend copies contributes six pairs (F13).
        arms[armIndex] = arms[armIndex] with { PairCount = 6 };
        fixture.Repeats[0] = fixture.Repeats[0] with { Restart = fixture.Repeats[0].Restart! with { Comparison = "both", Arms = arms } };
        B10FinalArm actual = Read(JsonNode.Parse(B10FinalProjectionCodec.Write(fixture.Projection()))!.AsObject()).Data.Repeats[0].Restart!.Arms![armIndex];
        Assert.Equal(6, actual.PairCount);
        Assert.Equal(1, actual.OriginalsSeen);
        Assert.Equal(1, actual.ResendsSeen);
    }

    [Fact]
    public void Given_GlobalFailureAndUnknownSweepCounts_When_Read_Then_RetainsNullsAndFalseAdmission()
    {
        var fixture = Fixture(); B10FinalProjection projection = fixture.Projection();
        B10FinalRepeat[] repeats = projection.Data.Repeats.Select(repeat => repeat with
        { Status = "invalid", ClassificationFinal = false, Timeout = null, Reasons = ["b10_final_drain_incomplete"],
            Restart = repeat.Restart! with { Eligibility = "ineligible", EligibilityReasons = ["b10_restart_sweep_unavailable"], Comparison = "unavailable",
                Arms = repeat.Restart.Arms!.Select(arm => arm with { OriginalsSeen = null, ResendsSeen = null, PairCount = null,
                    UnreportedSeen = null, UnreportedUnseen = null, LostResends = null, Witness = null }).ToArray() } }).ToArray();
        byte[] bytes = B10FinalProjectionCodec.Write(projection with { Data = projection.Data with { ScanComplete = false, Repeats = repeats } });
        B10FinalProjection read = B10FinalProjectionCodec.Read(bytes);
        Assert.All(read.Data.Repeats, repeat => { Assert.Null(repeat.Timeout); Assert.False(repeat.ClassificationFinal); Assert.All(repeat.Restart!.Arms!, arm => Assert.Null(arm.LostResends)); });
        JsonObject node = JsonNode.Parse(bytes)!.AsObject(); node["data"]!["repeats"]![0]!["restart"]!["arms"]![0]!["lost_resends"] = 0;
        Assert.Throws<FormatException>(() => Read(node));
        JsonObject missingReason = JsonNode.Parse(bytes)!.AsObject(); missingReason["data"]!["repeats"]![0]!["restart"]!["eligibility_reasons"] = new JsonArray("b10_restart_identity_unauthenticated");
        Assert.Throws<FormatException>(() => Read(missingReason));
    }

    [Theory]
    [InlineData(0, true)] [InlineData(99, false)] [InlineData(100, false)] [InlineData(101, false)]
    public void Given_ReasonArrayBoundary_When_Read_Then_ClosedLiteralsAndDistinctnessBoundTheCollection(int count, bool valid)
    {
        // The closed repeat vocabulary has twelve entries, so 99/100 distinct approved reasons cannot be constructed.
        JsonObject root = Node(); var reasons = new JsonArray();
        for (int index = 0; index < count; index++) reasons.Add("b10_final_drain_incomplete");
        root["data"]!["repeats"]![0]!["reasons"] = reasons;
        if (!valid) { root["data"]!["repeats"]![0]!["status"] = "invalid"; root["data"]!["repeats"]![0]!["classification_final"] = false; root["data"]!["repeats"]![0]!["timeout"] = null; }
        if (valid) Assert.Empty(Read(root).Data.Repeats[0].Reasons);
        else Assert.Throws<FormatException>(() => Read(root));
    }

    [Theory]
    [InlineData("2026-11-13T00:00:00.0000000+00:00", true)]
    [InlineData("2026-11-13T00:00:00.0000000+01:00", false)]
    [InlineData("2026-11-13T00:00:00+00:00", false)]
    [InlineData("", false)]
    public void Given_RegistrationTimestamp_When_Parsed_Then_RequiresExactUtcRoundTrip(string timestamp, bool valid)
    {
        if (valid) Assert.Equal(TimeSpan.Zero, B10FinalRegistrationReader.ParseUtc(timestamp).Offset);
        else Assert.Throws<FormatException>(() => B10FinalRegistrationReader.ParseUtc(timestamp));
    }

    [Fact]
    public void Given_ProjectionByteBudget_When_Read_Then_AcceptsAtBoundaryAndRejectsAboveBeforeParsing()
    {
        byte[] actual = B10FinalProjectionCodec.Write(Fixture().Projection());
        foreach (long size in new[] { B10FinalProjectionCodec.MaximumBytes - 1, B10FinalProjectionCodec.MaximumBytes })
        {
            byte[] padded = new byte[size]; Array.Fill(padded, (byte)' '); actual.CopyTo(padded, 0);
            Assert.Equal("B10", B10FinalProjectionCodec.Read(padded).Identity.ScenarioId);
        }
        Assert.Throws<InputLimitExceededException>(() => B10FinalProjectionCodec.Read(new byte[B10FinalProjectionCodec.MaximumBytes + 1]));
    }

    [Theory]
    [InlineData("final-reason")]
    [InlineData("comparison-counts")]
    [InlineData("comparison-reason")]
    [InlineData("unexpected-sweep-reason")]
    public void Given_ExplicitFinalProjectionInvariantViolation_When_Read_Then_Rejects(string change)
    {
        JsonObject root = Node(); JsonObject repeat = root["data"]!["repeats"]![0]!.AsObject(); JsonObject restart = repeat["restart"]!.AsObject();
        if (change == "final-reason") repeat["reasons"] = new JsonArray("b10_final_owner_invalid");
        if (change == "comparison-counts") restart["comparison"] = "both";
        if (change == "comparison-reason") { restart["eligibility"] = "ineligible"; restart["eligibility_reasons"] = new JsonArray("b10_restart_identity_unauthenticated"); }
        if (change == "unexpected-sweep-reason") { restart["eligibility"] = "ineligible"; restart["eligibility_reasons"] = new JsonArray("b10_restart_sweep_unavailable"); restart["comparison"] = "unavailable"; }
        Assert.Throws<FormatException>(() => Read(root));
    }

    [Theory]
    [InlineData("originals_seen")] [InlineData("resends_seen")] [InlineData("pair_count")]
    [InlineData("unreported_seen")] [InlineData("unreported_unseen")] [InlineData("lost_resends")]
    public void Given_LocalUnjoinedReason_When_Read_Then_RejectsInventedKnownSweepCount(string field)
    {
        JsonObject root = LocalUnjoined();
        root["data"]!["repeats"]![0]!["restart"]!["arms"]![0]![field] = 0;
        Assert.Throws<FormatException>(() => Read(root));
    }

    [Fact]
    public void Given_EmptyKillSetAndUnjoinedControl_When_Read_Then_OnlyEmptyCrossCountsMayRemainKnownZero()
    {
        JsonObject root = LocalUnjoined(); JsonObject restart = root["data"]!["repeats"]![0]!["restart"]!.AsObject();
        restart["termination"]!["outcome"] = "kill_too_late";
        restart["eligibility_reasons"] = new JsonArray("b10_restart_late_record_unjoined", "kill_too_late");
        restart["arms"]![1] = JsonSerializer.SerializeToNode(B10FinalJudgeFixture.EmptyArm(), B10FinalProjectionCodec.JsonOptions);
        Assert.Equal(0, Read(root).Data.Repeats[0].Restart!.Arms![1].LostResends);
        restart["arms"]![0]!["lost_resends"] = 0;
        Assert.Throws<FormatException>(() => Read(root));
        restart["arms"]![0]!["lost_resends"] = null;
        restart["arms"]![1]!["unreported_seen"] = 1; restart["arms"]![1]!["unreported_unseen"] = null;
        Assert.Throws<FormatException>(() => Read(root));
        foreach (string name in SweepFields) restart["arms"]![1]![name] = null;
        Assert.Null(Read(root).Data.Repeats[0].Restart!.Arms![1].LostResends);
    }

    private static readonly string[] SweepFields = ["originals_seen", "resends_seen", "pair_count", "unreported_seen", "unreported_unseen", "lost_resends", "witness"];
    private static JsonObject LocalUnjoined()
    {
        JsonObject root = Node(); JsonObject restart = root["data"]!["repeats"]![0]!["restart"]!.AsObject();
        restart["eligibility"] = "ineligible"; restart["eligibility_reasons"] = new JsonArray("b10_restart_late_record_unjoined"); restart["comparison"] = "unavailable";
        foreach (JsonNode? arm in restart["arms"]!.AsArray()) foreach (string field in SweepFields) arm![field] = null;
        return root;
    }

    [Fact]
    public void Given_DuplicateNestedProperty_When_Read_Then_Rejects()
    {
        string json = Node().ToJsonString().Replace("\"ok\":1", "\"ok\":1,\"ok\":1", StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => B10FinalProjectionCodec.Read(Encoding.UTF8.GetBytes(json)));
    }

    private static B10FinalJudgeFixture Fixture() { var fixture = new B10FinalJudgeFixture("C1"); fixture.Publish(); return fixture; }
    private static JsonObject Node() => JsonNode.Parse(B10FinalProjectionCodec.Write(Fixture().Projection()))!.AsObject();
    private static B10FinalProjection Read(JsonObject node) => B10FinalProjectionCodec.Read(Encoding.UTF8.GetBytes(node.ToJsonString()));
    private static IEnumerable<string> Names(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object) foreach (JsonProperty property in value.EnumerateObject()) { yield return property.Name; foreach (string name in Names(property.Value)) yield return name; }
        if (value.ValueKind == JsonValueKind.Array) foreach (JsonElement item in value.EnumerateArray()) foreach (string name in Names(item)) yield return name;
    }
}
#pragma warning restore CA1707
