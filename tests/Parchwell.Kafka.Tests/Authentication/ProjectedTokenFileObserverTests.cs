using System.Text.Json;
using Parchwell.Kafka.Authentication;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class ProjectedTokenFileObserverTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly ProjectedTokenFileMetadata A = new("/synthetic/path-secret-a", 10, 20);
    private static readonly ProjectedTokenFileMetadata B = new("/synthetic/path-secret-b", 11, 22);

    // Matching metadata pairs advance generation only when the stable tuple changes.
    [Fact]
    public void Given_MatchingMetadataPairs_When_Observed_Then_GenerationTracksOnlyStableTupleChanges()
    {
        var reads = new Queue<ProjectedTokenFileMetadata>([A, A, A, A, B, B]);
        var observer = NewObserver(() => reads.Dequeue());

        ProjectedTokenFileObservation first = observer.Observe();
        ProjectedTokenFileObservation second = observer.Observe();
        ProjectedTokenFileObservation third = observer.Observe();

        Assert.Equal("1,1,2", string.Join(',', new[] { first.Generation, second.Generation, third.Generation }));
        Assert.Equal("1,2,3", string.Join(',', new[] { first.Sample, second.Sample, third.Sample }));
        Assert.All(new[] { first, second, third }, result => Assert.Equal("observed", result.Status));
        Assert.Equal("1,2,3", string.Join(',', new[] { first.Timestamp, second.Timestamp, third.Timestamp }));
        Assert.Equal(Now, first.ObservedAt);
        Assert.Equal(1000, first.TimestampFrequency);
    }

    // A path-only tuple change must advance generation while public output remains an allowlist.
    [Fact]
    public void Given_PathOnlyStableTupleChanges_When_Observed_Then_GenerationAdvancesAndOutputStaysSanitized()
    {
        var firstTuple = new ProjectedTokenFileMetadata("/synthetic/v1/token", 10, 20);
        var secondTuple = new ProjectedTokenFileMetadata("/synthetic/v2/token", 10, 20);
        var reads = new Queue<ProjectedTokenFileMetadata>([firstTuple, firstTuple, firstTuple, firstTuple, secondTuple, secondTuple, secondTuple, secondTuple]);
        var observer = NewObserver(() => reads.Dequeue());

        ProjectedTokenFileObservation[] results = Enumerable.Range(0, 4).Select(_ => observer.Observe()).ToArray();

        Assert.Equal(["observed", "observed", "observed", "observed"], results.Select(result => result.Status));
        Assert.Equal([1, 1, 2, 2], results.Select(result => result.Generation));
        Assert.Equal([1, 2, 3, 4], results.Select(result => result.Sample));
        string json = JsonSerializer.Serialize(results[2]);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(
            ["Generation", "ObservedAt", "Sample", "Status", "Timestamp", "TimestampFrequency"],
            document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        foreach (string privateValue in new[] { "/synthetic/v1/token", "/synthetic/v2/token", "synthetic-content", "synthetic-exception" })
            Assert.DoesNotContain(privateValue, json, StringComparison.Ordinal);
    }

    // A mismatched pair consumes one sample without replacing the prior stable tuple.
    [Fact]
    public void Given_PathOnlyMismatchBetweenSnapshots_When_Observed_Then_UnavailablePreservesPriorStableGeneration()
    {
        var firstTuple = new ProjectedTokenFileMetadata("/synthetic/v1/token", 10, 20);
        var secondTuple = new ProjectedTokenFileMetadata("/synthetic/v2/token", 10, 20);
        var reads = new Queue<ProjectedTokenFileMetadata>([firstTuple, firstTuple, firstTuple, secondTuple, secondTuple, secondTuple]);
        var observer = NewObserver(() => reads.Dequeue());

        ProjectedTokenFileObservation stableBefore = observer.Observe();
        ProjectedTokenFileObservation mismatch = observer.Observe();
        ProjectedTokenFileObservation stableAfter = observer.Observe();

        Assert.Equal("observed", stableBefore.Status);
        Assert.Equal(1, stableBefore.Generation);
        Assert.Equal("unavailable", mismatch.Status);
        Assert.Null(mismatch.Generation);
        Assert.Null(mismatch.ObservedAt);
        Assert.Null(mismatch.Timestamp);
        Assert.Equal("observed", stableAfter.Status);
        Assert.Equal(2, stableAfter.Generation);
    }

    // A metadata exception cannot leak its private path or replace the last stable tuple.
    [Fact]
    public void Given_MetadataReadFailureBetweenStableSnapshots_When_Observed_Then_RecoveryUsesLastStableTuple()
    {
        var firstTuple = new ProjectedTokenFileMetadata("/synthetic/v1/token", 10, 20);
        var secondTuple = new ProjectedTokenFileMetadata("/synthetic/v2/token", 10, 20);
        var reads = new Queue<Func<ProjectedTokenFileMetadata>>(
        [
            () => firstTuple, () => firstTuple,
            () => throw new IOException("private-path-sentinel"),
            () => firstTuple, () => firstTuple,
            () => secondTuple, () => secondTuple
        ]);
        var observer = NewObserver(() => reads.Dequeue()());

        ProjectedTokenFileObservation first = observer.Observe();
        ProjectedTokenFileObservation unavailable = observer.Observe();
        ProjectedTokenFileObservation recoveredSame = observer.Observe();
        ProjectedTokenFileObservation recoveredChanged = observer.Observe();

        Assert.Equal(1, first.Generation);
        Assert.Equal("unavailable", unavailable.Status);
        Assert.Null(unavailable.Generation);
        Assert.Equal(1, recoveredSame.Generation);
        Assert.Equal(2, recoveredChanged.Generation);
        Assert.DoesNotContain("private-path-sentinel", JsonSerializer.Serialize(unavailable), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_UnstablePairThenStablePair_When_Observed_Then_UnavailableDoesNotAdvancePriorState()
    {
        var reads = new Queue<ProjectedTokenFileMetadata>([A, B, B, B]);
        var observer = NewObserver(() => reads.Dequeue());

        ProjectedTokenFileObservation unstable = observer.Observe();
        ProjectedTokenFileObservation stable = observer.Observe();

        Assert.Equal("unavailable", unstable.Status);
        Assert.Null(unstable.Generation);
        Assert.Null(unstable.ObservedAt);
        Assert.Equal("observed", stable.Status);
        Assert.Equal(1, stable.Generation);
    }

    [Fact]
    public void Given_MetadataReaderThrows_When_Observed_Then_ExceptionAndPrivateMetadataAreNotReturned()
    {
        var observer = NewObserver(() => throw new IOException("synthetic-exception-path-secret"));

        ProjectedTokenFileObservation result = observer.Observe();

        Assert.Equal("unavailable", result.Status);
        Assert.Null(result.Generation);
        string json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("synthetic-exception-path-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("path-secret", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ClockThrows_When_Observed_Then_UnavailableHasNullClockAndGeneration()
    {
        var reads = new Queue<ProjectedTokenFileMetadata>([A, A]);
        var observer = new ProjectedTokenFileObserver("synthetic-path", new ThrowingTimeProvider(), () => reads.Dequeue());

        ProjectedTokenFileObservation result = observer.Observe();

        Assert.Equal("unavailable", result.Status);
        Assert.Null(result.ObservedAt);
        Assert.Null(result.Timestamp);
        Assert.Null(result.Generation);
        Assert.Equal(1000, result.TimestampFrequency);
    }

    [Fact]
    public void Given_SampleLimitBoundary_When_Observed_Then_NoWorkOccursAfter4096()
    {
        int reads = 0;
        var observer = NewObserver(() =>
        {
            reads++;
            return A;
        });
        Assert.Equal(0, reads);

        ProjectedTokenFileObservation at4095 = default!;
        ProjectedTokenFileObservation at4096 = default!;
        ProjectedTokenFileObservation at4097 = default!;
        ProjectedTokenFileObservation at4098 = default!;
        for (int sample = 1; sample <= 4098; sample++)
        {
            ProjectedTokenFileObservation result = observer.Observe();
            if (sample == 4095) at4095 = result;
            if (sample == 4096) at4096 = result;
            if (sample == 4097) at4097 = result;
            if (sample == 4098) at4098 = result;
        }

        Assert.Equal(4095, at4095.Sample);
        Assert.Equal("observed", at4095.Status);
        Assert.Equal(4096, at4096.Sample);
        Assert.Equal("observed", at4096.Status);
        Assert.Equal(4097, at4097.Sample);
        Assert.Equal("sample_limit", at4097.Status);
        Assert.Equal(4097, at4098.Sample);
        Assert.Equal("sample_limit", at4098.Status);
        Assert.Null(at4097.ObservedAt);
        Assert.Null(at4097.Timestamp);
        Assert.Null(at4097.Generation);
        Assert.Equal(8192, reads);
    }

    private static ProjectedTokenFileObserver NewObserver(Func<ProjectedTokenFileMetadata> read) =>
        new("synthetic-path-secret", new FixedTimeProvider(Now), read);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => 1000;
        public override DateTimeOffset GetUtcNow() => now;
        public override long GetTimestamp() => ++_timestamp;
    }

    private sealed class ThrowingTimeProvider : TimeProvider
    {
        public override long TimestampFrequency => 1000;
        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("synthetic-clock-secret");
        public override long GetTimestamp() => throw new InvalidOperationException("synthetic-clock-secret");
    }
}
