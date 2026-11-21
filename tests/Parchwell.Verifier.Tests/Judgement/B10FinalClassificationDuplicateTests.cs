using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Abrupt;
using Parchwell.Verifier.VerifyRun.B10Final;
using Parchwell.Verifier.VerifyRun.Configuration;
using Parchwell.Verifier.VerifyRun.Input;
using Xunit;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707
public sealed class B10FinalClassificationDuplicateTests
{
    [Fact]
    public void Given_KillTooLateFullControlPopulation_When_Classified_Then_EmptyCrossIsKnownAndControlUsesAllCopies()
    {
        B10FinalInput input = Input();
        ConsumedRecord[] records = [Record(0, "first", "original"), Record(1, "first", "original"),
            Record(2, "first", "original"), Record(3, "first", "resend"), Record(4, "first", "resend")];
        IReadOnlyList<AbruptArm> arms = B10FinalClassification.EvaluateArms(input, records, true, 2)!;
        Assert.Equal(6, arms[0].PairCount);
        Assert.Equal(new DuplicateBreakdown(3, 1), arms[0].DuplicateBreakdown);
        Assert.True(arms[0].Members[0].CopiesTruncated);
        Assert.Equal(2, arms[0].Members[0].Copies!.Count);
        Assert.Equal(new DuplicateBreakdown(0, 0), arms[1].DuplicateBreakdown);
    }

    [Fact]
    public void Given_IncompleteOrOpaqueControl_When_Classified_Then_PreservesAbsence()
    {
        B10FinalInput input = Input();
        IReadOnlyList<AbruptArm> incomplete = B10FinalClassification.EvaluateArms(input, [], false, 2)!;
        Assert.Null(incomplete[0].DuplicateBreakdown);
        Assert.Null(incomplete[1].DuplicateBreakdown);
        IReadOnlyList<AbruptArm> opaque = B10FinalClassification.EvaluateArms(input, [Record(0, "unknown", "unknown")], true, 2)!;
        Assert.Null(opaque[0].DuplicateBreakdown);
        Assert.Equal(new DuplicateBreakdown(0, 0), opaque[1].DuplicateBreakdown);
        Assert.Equal(1, opaque[0].Members[0].OtherCopies);
    }

    [Fact]
    public void Given_SameOffsetAcrossPartitionsAndCoordinateReread_When_Classified_Then_CountsDistinctCoordinatesOnce()
    {
        ConsumedRecord original = Record(0, "first", "original");
        ConsumedRecord otherPartition = original with { Partition = 1 };
        IReadOnlyList<AbruptArm> arms = B10FinalClassification.EvaluateArms(Input(), [original, otherPartition, original], true, 2)!;
        Assert.Equal(new DuplicateBreakdown(1, 0), arms[0].DuplicateBreakdown);
    }

    [Fact]
    public void Given_UnreportedOriginalWithoutTerminal_When_Classified_Then_ObservationDoesNotInventTerminalStatus()
    {
        B10FinalInput input = Input();
        IReadOnlyList<AbruptArm> arms = B10FinalClassification.EvaluateArms(input, [Record(0, "first", "original")], true, 2)!;
        Assert.Equal(new DuplicateBreakdown(0, 0), arms[0].DuplicateBreakdown);
        Assert.Equal(1, arms[0].UnreportedSeen);
        Assert.Equal(0, arms[0].UnreportedUnseen);
        Assert.True(arms[0].Members[0].ResendLost);
    }

    [Fact]
    public void Given_UnassignableIdentity_When_Classified_Then_ControlObservationIsAbsent()
    {
        B10FinalInput input = Input();
        ConsumedRecord[] invalid = [Record(0, null, null), new("topic", 0, 1, null, "first", "original", 0, [], "hash"),
            new("topic", 0, 2, "key", "first", "original", null, [], "hash")];
        foreach (ConsumedRecord record in invalid)
        {
            IReadOnlyList<AbruptArm> arms = B10FinalClassification.EvaluateArms(input, [record], true, 2)!;
            Assert.Null(arms[0].DuplicateBreakdown);
            Assert.Equal(new DuplicateBreakdown(0, 0), arms[1].DuplicateBreakdown);
        }
    }

    [Theory]
    [InlineData(19L, false)]
    [InlineData(20L, true)]
    [InlineData(21L, true)]
    public void Given_MalformedRecordFromAnotherRepeat_When_Classified_Then_OnlyOriginalWindowAffectsObservation(
        long offset, bool hasBreakdown)
    {
        B10FinalInput input = Input();
        input.Started = true;
        input.FirstInstance = "first";
        input.Starts = new() { [0] = 10 };
        input.Ends = new() { [0] = 20 };
        input.Instances.Add("first");
        var later = new B10FinalInput(2) { Started = true, Starts = new() { [0] = 20 }, Ends = new() { [0] = 30 } };
        later.Instances.Add("later");
        var options = new VerifierHostOptions { Phase = "C1", RunId = "run", MaxDiagnosticsPerCheck = 2 };
        var resolver = new RegisteredProducerConfigurationResolver(Options.Create(options));
        ConsumedRecord[] records = [Record(10, "first", "original"), Record(31, "first", "original"),
            Record(offset, "later", "later-attempt") with { Key = null }];

        B10FinalRepeat repeat = B10FinalClassification.Classify(input, [input, later], records, null, options, null, resolver);

        Assert.Equal(hasBreakdown ? new DuplicateBreakdown(1, 0) : null, repeat.Restart!.Arms![0].DuplicateBreakdown);
    }

    [Theory]
    [InlineData("earlier", 19L, true)]
    [InlineData(null, 9L, true)]
    [InlineData(null, 10L, false)]
    [InlineData(null, 19L, false)]
    [InlineData(null, 20L, true)]
    [InlineData("first", 19L, false)]
    public void Given_MalformedRecordNearRepeatWindow_When_AgreeingNonfinalArms_Then_OnlyRelevantRecordsAffectRestart(
        string? owner, long offset, bool agrees)
    {
        B10FinalInput input = Input(number: 2);
        input.Started = true;
        input.InputValid = true;
        input.FirstInstance = "first";
        input.Starts = new() { [0] = 10 };
        input.Ends = new() { [0] = 20 };
        input.Instances.Add("first");
        var earlier = new B10FinalInput(1);
        earlier.Instances.Add("earlier");
        var options = new VerifierHostOptions { Phase = "C1", RunId = "run", MaxDiagnosticsPerCheck = 2 };
        var resolver = new RegisteredProducerConfigurationResolver(Options.Create(options));
        ConsumedRecord[] own = [Record(11, "first", "original"), Record(12, "first", "resend")];
        using JsonDocument stored = JsonDocument.Parse(new JsonObject
        {
            ["arms"] = JsonSerializer.SerializeToNode(B10FinalClassification.EvaluateArms(input, own, true, 2),
                B10FinalProjectionCodec.JsonOptions),
            ["eligibility"] = "pending_sweep",
            ["eligibility_reasons"] = new JsonArray(),
            ["problems"] = new JsonArray()
        }.ToJsonString());
        input.Nonfinal = stored.RootElement;
        ConsumedRecord malformed = Record(offset, owner, "malformed") with { Key = null };

        B10FinalRepeat repeat = B10FinalClassification.Classify(input, [earlier, input], [..own, malformed], null, options, null, resolver);

        B10FinalRestart restart = Assert.IsType<B10FinalRestart>(repeat.Restart);
        Assert.Equal(agrees ? "same_instance_only" : "unavailable", restart.Comparison);
        if (agrees)
            Assert.Empty(restart.EligibilityReasons);
        else
            Assert.Equal("b10_restart_late_record_unjoined", Assert.Single(restart.EligibilityReasons));
        Assert.Equal(agrees ? new DuplicateBreakdown(0, 1) : null, restart.Arms![0].DuplicateBreakdown);
    }

    [Theory]
    [MemberData(nameof(Results.DuplicateBreakdownTests.MalformedWireObjects), MemberType = typeof(Results.DuplicateBreakdownTests))]
    public void Given_NonfinalArmOrMemberBreakdown_When_Validated_Then_RejectsMalformedPresence(string literal)
    {
        IReadOnlyList<AbruptArm> arms = B10FinalClassification.EvaluateArms(Input(), [Record(0, "first", "original")], true, 2)!;
        foreach (bool member in new[] { false, true })
        {
            JsonArray array = JsonSerializer.SerializeToNode(arms, B10FinalProjectionCodec.JsonOptions)!.AsArray();
            JsonObject target = (member ? array[0]!["members"]![0]! : array[0]!).AsObject();
            target["duplicate_breakdown"] = "fixture";
            string payload = array.ToJsonString().Replace("\"duplicate_breakdown\":\"fixture\"", "\"duplicate_breakdown\":" + literal, StringComparison.Ordinal);
            using JsonDocument malformed = JsonDocument.Parse(payload);
            Assert.Throws<FormatException>(() => B10FinalRepeatEvidence.ValidateArms(malformed.RootElement));
        }
    }

    [Theory]
    [InlineData(0L)] [InlineData(1L)] [InlineData(long.MaxValue)]
    public void Given_HistoricalOrValidArmAndMemberBreakdown_When_Validated_Then_Accepts(long count)
    {
        IReadOnlyList<AbruptArm> arms = B10FinalClassification.EvaluateArms(Input(), [Record(0, "first", "original")], true, 2)!;
        JsonArray array = JsonSerializer.SerializeToNode(arms, B10FinalProjectionCodec.JsonOptions)!.AsArray();
        JsonObject arm = array[0]!.AsObject(), member = arm["members"]![0]!.AsObject();
        arm.Remove("duplicate_breakdown");
        member.Remove("duplicate_breakdown");
        using (JsonDocument historical = JsonDocument.Parse(array.ToJsonString())) B10FinalRepeatEvidence.ValidateArms(historical.RootElement);
        member["duplicate_breakdown"] = new JsonObject { ["duplicate_same_attempt"] = count, ["duplicate_different_attempt"] = 0 };
        arm["duplicate_breakdown"] = member["duplicate_breakdown"]!.DeepClone();
        using JsonDocument present = JsonDocument.Parse(array.ToJsonString());
        B10FinalRepeatEvidence.ValidateArms(present.RootElement);
    }

    [Fact]
    public void Given_KnownWarmupOrTimeoutWithMissingKey_When_Classified_Then_ControlObservationRemainsIndependent()
    {
        B10FinalInput input = Input();
        input.First = input.First! with
        {
            Warmups = new Dictionary<string, AbruptWarmup>(StringComparer.Ordinal)
            {
                ["warmup"] = new("warmup", "same_instance", "warmup-key", "warmup-hash")
            }
        };
        input.TimeoutInstance = "timeout";
        ConsumedRecord original = Record(0, "first", "original");
        ConsumedRecord[] excluded = [new("topic", 0, 1, null, "first", "warmup", -1, [], "warmup-hash"),
            new("topic", 0, 2, null, "timeout", null, null, [], "hash")];
        foreach (ConsumedRecord record in excluded)
        {
            IReadOnlyList<AbruptArm> arms = B10FinalClassification.EvaluateArms(input, [original, record], true, 2)!;
            Assert.Equal(new DuplicateBreakdown(0, 0), arms[0].DuplicateBreakdown);
            Assert.Equal(new DuplicateBreakdown(0, 0), arms[1].DuplicateBreakdown);
        }
    }

    private static B10FinalInput Input(int number = 1)
    {
        var attempts = new Dictionary<string, AbruptAttempt>(StringComparer.Ordinal)
        {
            ["original"] = new("original", "same_instance", "original", 0, "key", 0, "hash", null, 0),
            ["resend"] = new("resend", "same_instance", "resend", 0, "key", 0, "hash", "original", 1)
        };
        var first = new AbruptFirstEvidence("first", "uid", attempts, ["original"], [],
            new Dictionary<string, string> { ["original"] = "resend" },
            new Dictionary<string, AbruptTerminal> { ["resend"] = new(true, 0, null) }, [], [])
        { Accepted = new HashSet<string>(StringComparer.Ordinal) { "original", "resend" } };
        return new B10FinalInput(number) { First = first, KillTooLate = true, Options = new() { RunId = "run" } };
    }

    private static ConsumedRecord Record(long offset, string? instance, string? attempt) =>
        new("topic", 0, offset, "key", instance, attempt, 0, [], "hash");
}
