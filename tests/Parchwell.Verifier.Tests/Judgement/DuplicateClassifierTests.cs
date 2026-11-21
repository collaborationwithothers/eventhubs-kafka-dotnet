using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707
public sealed class DuplicateClassifierTests
{
    private static ConsumedRecord Record(string attempt, long offset, string instance = "i", string key = "k", long sequence = 1, int partition = 0) =>
        new("topic", partition, offset, key, instance, attempt, sequence, []);

    [Fact]
    public void Given_CopyPopulations_When_Classified_Then_LiteralDecompositionIsPreserved()
    {
        Assert.Equal(new DuplicateBreakdown(0, 0), DuplicateClassifier.TryClassify([]));
        Assert.Equal(new DuplicateBreakdown(0, 0), DuplicateClassifier.TryClassify([Record("a", 0)]));
        Assert.Equal(new DuplicateBreakdown(0, 0), DuplicateClassifier.TryClassify([Record("a", 0), Record("a", 0)]));
        Assert.Equal(new DuplicateBreakdown(2, 0), DuplicateClassifier.TryClassify([Record("a", 0), Record("a", 1), Record("a", 2)]));
        Assert.Equal(new DuplicateBreakdown(0, 1), DuplicateClassifier.TryClassify([Record("a", 0), Record("b", 1)]));
        Assert.Equal(new DuplicateBreakdown(3, 1), DuplicateClassifier.TryClassify([Record("a", 0), Record("a", 1), Record("a", 2), Record("b", 3), Record("b", 4)]));
    }

    [Fact]
    public void Given_DistinctGroupsAndCoordinates_When_Classified_Then_IdentityIsOrdinalAndGroupsAdd()
    {
        Assert.Equal(new DuplicateBreakdown(1, 2), DuplicateClassifier.TryClassify([
            Record("a", 0), Record("a", 1), Record("a", 0, instance: "I", partition: 1),
            Record("b", 3, key: "K"), Record("c", 4, key: "K"),
            Record("a", 5, sequence: 2)]));
    }

    [Fact]
    public void Given_EmptyButReconciledStringValues_When_Classified_Then_ExistingIdentityContractIsPreserved()
    {
        Assert.Equal(new DuplicateBreakdown(1, 0), DuplicateClassifier.TryClassify([
            Record("", 0, instance: "", key: ""), Record("", 1, instance: "", key: "")]));
    }

    [Fact]
    public void Given_IncompleteIdentity_When_Classified_Then_ObservationIsUnavailable()
    {
        ConsumedRecord record = Record("a", 0);
        Assert.Null(DuplicateClassifier.TryClassify([record with { Key = null }]));
        Assert.Null(DuplicateClassifier.TryClassify([record with { InstanceId = null }]));
        Assert.Null(DuplicateClassifier.TryClassify([record with { AttemptId = null }]));
        Assert.Null(DuplicateClassifier.TryClassify([record with { Sequence = null }]));
    }
}
