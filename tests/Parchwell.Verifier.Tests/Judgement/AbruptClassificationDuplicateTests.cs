using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Abrupt;
using Parchwell.Verifier.VerifyRun.Input;
using Xunit;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707
public sealed class AbruptClassificationDuplicateTests
{
    [Fact]
    public void Given_FullCopies_When_DiagnosticsAreTruncated_Then_BreakdownUsesAllCopies()
    {
        var (first, successor) = Evidence();
        ConsumedRecord[] records = [Record(0, "first", "original"), Record(1, "first", "original"),
            Record(2, "first", "original"), Record(3, "next", "resend"), Record(4, "next", "resend")];
        AbruptArm arm = AbruptClassification.Evaluate(first, successor, records, true, 2)[1];
        AbruptMember member = Assert.Single(arm.Members);
        Assert.Equal(6, member.PairCount);
        Assert.Equal(new DuplicateBreakdown(3, 1), member.DuplicateBreakdown);
        Assert.Equal(new DuplicateBreakdown(3, 1), arm.DuplicateBreakdown);
        Assert.True(member.CopiesTruncated);
        Assert.Equal(2, member.Copies!.Count);
        Assert.NotNull(arm.Witness);
    }

    [Fact]
    public void Given_UnavailableOrOpaqueCopies_When_Classifying_Then_BreakdownIsAbsent()
    {
        var (first, successor) = Evidence();
        ConsumedRecord[] opaque = [Record(0, "foreign", "unknown")];
        Assert.Null(AbruptClassification.Evaluate(first, successor, opaque, true, 2)[1].DuplicateBreakdown);
        Assert.Null(AbruptClassification.Evaluate(first, successor, [Record(0, null, null)], true, 2)[1].DuplicateBreakdown);
        Assert.Null(AbruptClassification.Evaluate(first, null, [], true, 2)[1].DuplicateBreakdown);
        Assert.Null(AbruptClassification.Evaluate(first, successor, [], false, 2)[1].DuplicateBreakdown);
        Assert.Equal(new DuplicateBreakdown(0, 0), AbruptClassification.Evaluate(first, successor, [], true, 2)[1].DuplicateBreakdown);
    }

    [Fact]
    public void Given_UnassignableNullKey_When_Classifying_Then_OnlyKnownTimeoutIsExcluded()
    {
        var (first, successor) = Evidence();
        ConsumedRecord unknown = new("topic", 0, 0, null, null, null, null, [], "hash");
        IReadOnlyList<AbruptArm> ambiguous = AbruptClassification.Evaluate(first, successor, [unknown], true, 2);
        Assert.All(ambiguous, arm => Assert.Null(arm.DuplicateBreakdown));
        ConsumedRecord timeout = unknown with { InstanceId = "timeout" };
        IReadOnlyList<AbruptArm> excluded = AbruptClassification.Evaluate(first, successor, [timeout], true, 2,
            timeoutInstances: new HashSet<string>(StringComparer.Ordinal) { "timeout" });
        Assert.All(excluded, arm => Assert.Equal(new DuplicateBreakdown(0, 0), arm.DuplicateBreakdown));
    }

    [Fact]
    public void Given_ConflictingHash_When_Classifying_Then_OnlyMatchingCohortIsUnavailable()
    {
        var (first, successor) = Evidence();
        ConsumedRecord conflict = Record(0, "first", "original") with { ValueSha256 = "other" };
        IReadOnlyList<AbruptArm> arms = AbruptClassification.Evaluate(first, successor, [conflict], true, 2);
        Assert.Equal(new DuplicateBreakdown(0, 0), arms[0].DuplicateBreakdown);
        Assert.Null(arms[1].DuplicateBreakdown);
        Assert.Equal(0, Assert.Single(arms[1].Members).CopyCount);
    }

    [Fact]
    public void Given_UnverifiedLoss_When_PopulationIsComplete_Then_BreakdownRemainsAvailable()
    {
        var (first, successor) = Evidence();
        ConsumedRecord[] records = [Record(0, "first", "original"), Record(1, "first", "original")];
        AbruptArm arm = AbruptClassification.Evaluate(first, successor, records, true, 2, false)[1];
        AbruptMember member = Assert.Single(arm.Members);
        Assert.Null(member.ResendLost);
        Assert.Null(arm.LostResends);
        Assert.Equal(new DuplicateBreakdown(1, 0), member.DuplicateBreakdown);
        Assert.Equal(new DuplicateBreakdown(1, 0), arm.DuplicateBreakdown);
    }

    [Fact]
    public void Given_UnreportedOriginalAndReread_When_Classifying_Then_IdentityIsCountedOnce()
    {
        var (first, successor) = Evidence();
        ConsumedRecord record = Record(0, "first", "original");
        AbruptArm arm = AbruptClassification.Evaluate(first, successor, [record, record], true, 2)[1];
        Assert.Equal(new DuplicateBreakdown(0, 0), arm.DuplicateBreakdown);
        Assert.Equal(1, arm.UnreportedSeen);
        Assert.Equal(0, arm.UnreportedUnseen);
    }

    private static (AbruptFirstEvidence, AbruptSuccessorEvidence) Evidence()
    {
        var attempts = new Dictionary<string, AbruptAttempt>(StringComparer.Ordinal)
        {
            ["control"] = new("control", "same_instance", "original", 0, "same", 0, "hash", null, 0),
            ["original"] = new("original", "cross_restart", "original", 0, "cross", 0, "hash", null, 1),
            ["control-resend"] = new("control-resend", "same_instance", "resend", 0, "same", 0, "hash", "control", 2)
        };
        var first = new AbruptFirstEvidence("first", "uid", attempts, ["control"], ["original"],
            new Dictionary<string, string> { ["control"] = "control-resend" },
            new Dictionary<string, AbruptTerminal> { ["control-resend"] = new(true, 0, null) }, [], [])
        { Accepted = new HashSet<string>(StringComparer.Ordinal) { "original", "control", "control-resend" } };
        var successor = new AbruptSuccessorEvidence("next", new Dictionary<string, string> { ["original"] = "resend" },
            new Dictionary<string, AbruptTerminal> { ["resend"] = new(true, 0, null) });
        return (first, successor);
    }

    private static ConsumedRecord Record(long offset, string? instance, string? attempt) =>
        new("topic", 0, offset, "cross", instance, attempt, 0, [], "hash");
}
