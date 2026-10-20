using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;

using Xunit;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707 // Test names follow the repository Given_When_Then convention.

public sealed class OutcomeClassifierTests
{
    [Fact]
    public void Given_FourAttemptStates_When_Classifying_Then_EachClassIsCountedOnce()
    {
        LedgerRow[] ledger = [
            RecordedEvidence.LedgerRow(RecordedEvidence.Attempt("a1", "k1")),
            RecordedEvidence.LedgerRow(RecordedEvidence.Attempt("a2", "k2")),
            RecordedEvidence.LedgerRow(RecordedEvidence.Attempt("a3", "k3"), false, "synchronous_rejection"),
            RecordedEvidence.LedgerRow(RecordedEvidence.Attempt("a4", "k4"), false, "synchronous_rejection")];
        Assert.Equal(new OutcomeCounts(1, 1, 1, 1, 0, 0), OutcomeClassifier.Classify(ledger,
            [Record("a1", "k1", 1, 0), Record("a3", "k3", 1, 1)]));
    }

    [Fact]
    public void Given_RepeatedConsumedRecord_When_Classifying_Then_AttemptCountAndExcessDuplicatesAreSeparate()
    {
        ConsumedRecord record = Record("a1", "k", 1, 0);
        Assert.Equal(new OutcomeCounts(1, 0, 0, 0, 2, 0), OutcomeClassifier.Classify(
            [RecordedEvidence.LedgerRow()], [record, record, record]));
    }

    [Fact]
    public void Given_RetryWithSameLogicalEvent_When_OnlySecondAttemptSeen_Then_FirstAttemptStaysUnseen()
    {
        Assert.Equal(new OutcomeCounts(1, 1, 0, 0, 0, 0), OutcomeClassifier.Classify(
            [RecordedEvidence.LedgerRow(), RecordedEvidence.LedgerRow(RecordedEvidence.Attempt("a2"))],
            [Record("a2", "k", 1, 0)]));
    }

    [Theory]
    [InlineData(3, 1, 2, 0, 2)]
    [InlineData(1, 2, 1, 1, 1)]
    [InlineData(1, 1, 1, 2, 0)]
    public void Given_OrderedSequences_When_Classifying_Then_DuplicatesAndBelowMaximumReordersAreIndependent(
        long first, long second, long third, long duplicate, long reorder)
    {
        OutcomeCounts counts = OutcomeClassifier.Classify([], [Record("a1", "k", first, 10),
            Record("a2", "k", second, 11), Record("a3", "k", third, 12)]);
        Assert.Equal(duplicate, counts.Duplicate);
        Assert.Equal(reorder, counts.Reorder);
    }

    [Fact]
    public void Given_EqualSequences_When_Classifying_Then_DuplicateIsNotReorder()
    {
        Assert.Equal(new OutcomeCounts(0, 0, 0, 0, 1, 0), OutcomeClassifier.Classify([],
            [Record("a1", "k", 1, 0), Record("a2", "k", 1, 1)]));
    }

    [Fact]
    public void Given_ShuffledInput_When_Classifying_Then_OffsetsDetermineSequenceOrder()
    {
        Assert.Equal(2, OutcomeClassifier.Classify([], [Record("a3", "k", 2, 12),
            Record("a1", "k", 3, 10), Record("a2", "k", 1, 11)]).Reorder);
    }

    [Fact]
    public void Given_SeparatePartitions_When_Classifying_Then_MaximaAreSeparateAndDuplicatesGlobal()
    {
        ConsumedRecord first = Record("a1", "k", 3, 10);
        ConsumedRecord second = Record("a2", "k", 1, 11) with { Partition = 1 };
        ConsumedRecord third = Record("a3", "k", 3, 12) with { Partition = 2 };
        OutcomeCounts counts = OutcomeClassifier.Classify([], [first, second, third]);
        Assert.Equal(0, counts.Reorder);
        Assert.Equal(1, counts.Duplicate);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    public void Given_MaximumZero_When_NextSequenceCompared_Then_OnlyBelowMaximumReorders(long sequence, long reorder)
    {
        Assert.Equal(reorder, OutcomeClassifier.Classify([], [Record("a1", "k", 0, 0),
            Record("a2", "k", sequence, 1)]).Reorder);
    }

    [Fact]
    public void Given_NegativeFirstSequence_When_Classifying_Then_NoInventedZeroMaximum()
    {
        Assert.Equal(0, OutcomeClassifier.Classify([], [Record("a1", "k", -1, 0)]).Reorder);
    }

    [Fact]
    public void Given_EmptyObservations_When_Classifying_Then_OnlyUnobservedClassesAppear()
    {
        Assert.Equal(new OutcomeCounts(0, 1, 0, 1, 0, 0), OutcomeClassifier.Classify(
            [RecordedEvidence.LedgerRow(), RecordedEvidence.LedgerRow(RecordedEvidence.Attempt("a2"), false, "synchronous_rejection")], []));
        Assert.Equal(new OutcomeCounts(0, 0, 0, 0, 0, 0), OutcomeClassifier.Classify([], []));
    }

    [Fact]
    public void Given_CaseDistinctIdentitiesAndKeys_When_Classifying_Then_JoinsAreOrdinal()
    {
        OutcomeCounts counts = OutcomeClassifier.Classify([RecordedEvidence.LedgerRow()],
            [Record("A1", "k", 1, 0), Record("a1", "K", 1, 1) with { InstanceId = "INSTANCE-1" }]);
        Assert.Equal(1, counts.Loss);
        Assert.Equal(0, counts.Duplicate);
        Assert.Equal(0, counts.Ok);
    }

    private static ConsumedRecord Record(string attempt, string key, long sequence, long offset) =>
        new("synthetic-topic", 0, offset, key, "instance-1", attempt, sequence, []);

    [Fact]
    public void Given_NullSequenceBetweenKnownSequences_When_Classifying_Then_NullDoesNotChangeMaximum()
    {
        // Synthetic records isolate the maximum across a missing sequence.
        Assert.Equal(new OutcomeCounts(0, 0, 0, 0, 0, 1), OutcomeClassifier.Classify([], [
            Record("a1", "k", 3, 10),
            Record("a2", "k", 0, 11) with { Sequence = null },
            Record("a3", "k", 2, 12)]));
    }
}
