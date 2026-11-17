using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Abrupt;
using Parchwell.Verifier.VerifyRun.B10Final;
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
    [InlineData(false)] [InlineData(true)]
    public void Given_NonfinalArmOrMemberBreakdown_When_Validated_Then_RejectsMalformedPresence(bool member)
    {
        IReadOnlyList<AbruptArm> arms = B10FinalClassification.EvaluateArms(Input(), [Record(0, "first", "original")], true, 2)!;
        JsonArray array = JsonSerializer.SerializeToNode(arms, B10FinalProjectionCodec.JsonOptions)!.AsArray();
        JsonObject target = (member ? array[0]!["members"]![0]! : array[0]!).AsObject();
        target.Remove("duplicate_breakdown");
        using (JsonDocument historical = JsonDocument.Parse(array.ToJsonString())) B10FinalRepeatEvidence.ValidateArms(historical.RootElement);
        foreach (string literal in new[] { "null", "{}", "{\"duplicate_same_attempt\":0}",
            "{\"duplicate_same_attempt\":-1,\"duplicate_different_attempt\":0}",
            "{\"duplicate_same_attempt\":9223372036854775807,\"duplicate_different_attempt\":1}",
            "{\"duplicate_same_attempt\":0,\"duplicate_different_attempt\":0,\"extra\":0}" })
        {
            target["duplicate_breakdown"] = JsonNode.Parse(literal);
            using JsonDocument malformed = JsonDocument.Parse(array.ToJsonString());
            Assert.Throws<FormatException>(() => B10FinalRepeatEvidence.ValidateArms(malformed.RootElement));
        }
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

    private static B10FinalInput Input()
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
        return new B10FinalInput(1) { First = first, KillTooLate = true, Options = new() { RunId = "run" } };
    }

    private static ConsumedRecord Record(long offset, string? instance, string? attempt) =>
        new("topic", 0, offset, "key", instance, attempt, 0, [], "hash");
}
