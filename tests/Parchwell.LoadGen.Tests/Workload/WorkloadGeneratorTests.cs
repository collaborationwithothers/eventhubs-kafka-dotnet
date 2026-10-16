using System.Collections.Immutable;
using Parchwell.LoadGen.Run.Workload;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Workload;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class WorkloadGeneratorTests
{
    private static readonly int[] PartialSequences = [0, 1, 2, 3, 4, 0];
    private static readonly string[] PartialDocumentIds = ["d0", "d0", "d0", "d0", "d0", "d1"];

    [Fact]
    public void Given_SeedZeroAndWeightedProfile_When_Generating_Then_LiteralV1EventsMatch()
    {
        var events = WorkloadGenerator.Generate(Profile(0), maxAttempts: 20, maxPayloadBytes: 200,
            cancellationToken: TestContext.Current.CancellationToken).ToArray();

        var actual = events.Select(item =>
            $"{item.Ordinal}|{item.TenantId}|{item.DocumentId}|{item.Sequence}|{item.EventType}|{item.DueMicroseconds}|{item.PayloadSizeBytes}");
        var expected = new[]
        {
            "0|tenantB|d0|0|uploaded|0|100",
            "1|tenantB|d0|1|ocr-started|100000|11",
            "2|tenantB|d0|2|ocr-finished|200000|12",
            "3|tenantB|d0|3|classified|300000|13",
            "4|tenantB|d0|4|indexed|400000|14",
            "5|tenantB|d1|0|uploaded|500000|100",
            "6|tenantB|d1|1|ocr-started|600000|11",
            "7|tenantB|d1|2|ocr-finished|700000|12",
            "8|tenantB|d1|3|classified|800000|13",
            "9|tenantB|d1|4|indexed|900000|14",
            "10|tenantB|d2|0|uploaded|1000000|200",
            "11|tenantB|d2|1|ocr-started|1100000|11",
            "12|tenantB|d2|2|ocr-finished|1200000|12",
            "13|tenantB|d2|3|classified|1300000|13",
            "14|tenantB|d2|4|indexed|1400000|14",
            "15|tenantA|d3|0|uploaded|1500000|100",
            "16|tenantA|d3|1|ocr-started|1600000|11",
            "17|tenantA|d3|2|ocr-finished|1700000|12",
            "18|tenantA|d3|3|classified|1800000|13",
            "19|tenantA|d3|4|indexed|1900000|14"
        };

        Assert.Equal(expected, actual);
        Assert.Equal("tenantB-d2", events[10].Key);
    }

    [Theory]
    [InlineData(-1, 1, "48EFE7B49D37CC01A58EB9D8")]
    [InlineData(0, 0, "B286E7E7C7280F2D52F26551")]
    [InlineData(42, 5, "F642895D3A")]
    public void Given_SeedAndLogicalOrdinal_When_GeneratingPayload_Then_IndependentBytesRepeat(int seed,
        long ordinal, string expectedHex)
    {
        var plannedEvent = new PlannedEvent(ordinal, "stage", "tenant", "d1", "tenant-d1", 0, "uploaded", 0,
            expectedHex.Length == 10 ? 5 : 12);

        var first = WorkloadGenerator.GeneratePayload(plannedEvent, seed);
        var second = WorkloadGenerator.GeneratePayload(plannedEvent, seed);

        Assert.Equal(expectedHex, Convert.ToHexString(first));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Given_NegativeSeed_When_Generating_Then_SeedUsesItsUnsignedBitPattern()
    {
        var negativeSeedProfile = Profile(-1);

        var first = WorkloadGenerator.Generate(negativeSeedProfile, 20, 200,
            TestContext.Current.CancellationToken).ToArray();
        var repeat = WorkloadGenerator.Generate(negativeSeedProfile, 20, 200,
            TestContext.Current.CancellationToken).ToArray();

        Assert.Equal(first, repeat);
        Assert.Equal(20, first.Length);
        Assert.Equal("tenantA", first[0].TenantId);
    }

    [Fact]
    public void Given_SingleBucketAndPartialFinalDocument_When_Generating_Then_AllEventsAndSuffixAreStable()
    {
        var profile = Profile(0) with
        {
            Tenants = [new TenantWeight("only", 1)],
            Payloads = Profile(0).Payloads.ToImmutableDictionary(pair => pair.Key,
                pair => ImmutableArray.Create(new PayloadBucket(pair.Value[0].SizeBytes, 1))),
            Stages = [new LoadStage("partial", 3, 2)]
        };

        var events = WorkloadGenerator.Generate(profile, 6, 200, TestContext.Current.CancellationToken).ToArray();

        Assert.Equal(6, WorkloadGenerator.GetPlannedAttemptCount(profile));
        Assert.Equal(PartialSequences, events.Select(item => item.Sequence));
        Assert.Equal(PartialDocumentIds, events.Select(item => item.DocumentId));
        Assert.All(events, item => Assert.Equal("only", item.TenantId));
        Assert.Equal("indexed", events[4].EventType);
        Assert.Equal("uploaded", events[5].EventType);
    }

    [Fact]
    public void Given_LimitsBelowTheDeclaredPlan_When_Generating_Then_RejectsBeforeReturningStream()
    {
        var profile = Profile(0);

        Assert.Throws<InvalidOperationException>(() => WorkloadGenerator.Generate(profile, 19, 200,
            TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() => WorkloadGenerator.Generate(profile, 20, 199,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Given_StageProductsOverflowAggregateCount_When_Counting_Then_ThrowsOverflow()
    {
        var profile = Profile(0) with
        {
            Stages =
            [
                new LoadStage("large1", int.MaxValue, int.MaxValue),
                new LoadStage("large2", int.MaxValue, int.MaxValue),
                new LoadStage("large3", int.MaxValue, int.MaxValue)
            ]
        };

        Assert.Throws<OverflowException>(() => WorkloadGenerator.GetPlannedAttemptCount(profile));
    }

    [Fact]
    public void Given_CancelledToken_When_Enumerating_Then_StopsBeforeNextEvent()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var enumerator = WorkloadGenerator.Generate(Profile(0), 20, 200, cancellation.Token).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => enumerator.MoveNext());
    }

    private static WorkloadProfile Profile(int seed)
    {
        var payloads = new Dictionary<string, ImmutableArray<PayloadBucket>>(StringComparer.Ordinal)
        {
            ["uploaded"] = [new PayloadBucket(100, 1), new PayloadBucket(200, 3)],
            ["ocr-started"] = [new PayloadBucket(11, 1)],
            ["ocr-finished"] = [new PayloadBucket(12, 1)],
            ["classified"] = [new PayloadBucket(13, 1)],
            ["indexed"] = [new PayloadBucket(14, 1)]
        }.ToImmutableDictionary(StringComparer.Ordinal);

        return new WorkloadProfile(1, "fixture", seed,
            [new TenantWeight("tenantA", 1), new TenantWeight("tenantB", 3)], 1,
            payloads,
            [new LoadStage("first", 10, 1), new LoadStage("second", 10, 1)],
            new SourceLocation("synthetic-test-fixture"));
    }
}
#pragma warning restore CA1707
