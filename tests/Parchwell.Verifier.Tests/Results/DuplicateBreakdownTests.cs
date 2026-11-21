using System.Text.Json;
using Parchwell.Verifier.Common.Results;
using Xunit;

namespace Parchwell.Verifier.Tests.Results;

#pragma warning disable CA1707
public sealed class DuplicateBreakdownTests
{
    public static TheoryData<string> MalformedWireObjects => new()
    {
        "null", "[]", "{}", "{\"duplicate_same_attempt\":0}", "{\"duplicate_different_attempt\":0}",
        "{\"duplicate_same_attempt\":null,\"duplicate_different_attempt\":0}",
        "{\"duplicate_same_attempt\":0,\"duplicate_different_attempt\":null}",
        "{\"duplicate_same_attempt\":-1,\"duplicate_different_attempt\":0}",
        "{\"duplicate_same_attempt\":0,\"duplicate_different_attempt\":-1}",
        "{\"duplicate_same_attempt\":1.5,\"duplicate_different_attempt\":0}",
        "{\"duplicate_same_attempt\":true,\"duplicate_different_attempt\":0}",
        "{\"duplicate_same_attempt\":\"1\",\"duplicate_different_attempt\":0}",
        "{\"duplicate_same_attempt\":[],\"duplicate_different_attempt\":0}",
        "{\"duplicate_same_attempt\":9223372036854775808,\"duplicate_different_attempt\":0}",
        "{\"duplicate_same_attempt\":9223372036854775807,\"duplicate_different_attempt\":1}",
        "{\"duplicate_same_attempt\":0,\"duplicate_same_attempt\":0,\"duplicate_different_attempt\":0}",
        "{\"duplicate_same_attempt\":0,\"duplicate_different_attempt\":0,\"extra\":0}"
    };

    [Theory]
    [MemberData(nameof(MalformedWireObjects))]
    public void Given_MalformedJson_When_Read_Then_Rejected(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(DuplicateBreakdown.TryRead(document.RootElement, out _));
    }

    [Fact]
    public void Given_ValidAndOverflowingValues_When_ValidatedAndSummed_Then_CheckedBoundsApply()
    {
        using JsonDocument document = JsonDocument.Parse("{\"duplicate_same_attempt\":9223372036854775807,\"duplicate_different_attempt\":0}");
        Assert.True(DuplicateBreakdown.TryRead(document.RootElement, out DuplicateBreakdown? maximum));
        Assert.True(maximum!.IsValid(long.MaxValue));
        Assert.False(maximum.IsValid(0));
        Assert.False(new DuplicateBreakdown(-1, 0).IsValid());
        Assert.False(new DuplicateBreakdown(long.MaxValue, 1).IsValid());
        Assert.Equal(new DuplicateBreakdown(3, 4), DuplicateBreakdown.Sum([new(1, 2), new(2, 2)]));
        Assert.Null(DuplicateBreakdown.Sum([new(0, 0), null]));
        Assert.Throws<OverflowException>(() => DuplicateBreakdown.Sum([maximum, new(0, 1)]));
    }
}
