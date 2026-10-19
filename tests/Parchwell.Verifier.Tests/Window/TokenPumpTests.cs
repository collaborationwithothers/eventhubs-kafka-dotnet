using Azure.Core;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Window;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class TokenPumpTests
{
    [Fact]
    public void Given_InstalledToken_When_ReadingWatermarks_Then_MetadataIsAllowedAndInitialTimerIsDisarmed()
    {
        using var fixture = new KafkaSessionTestSupport();
        CancellationToken acquisition = default;
        fixture.AcquireToken = token => { acquisition = token; return new AccessToken("synthetic", fixture.Clock.GetUtcNow().AddHours(1)); };
        fixture.Install();
        fixture.Clock.Advance(1001);
        Assert.False(acquisition.IsCancellationRequested);
        Assert.Equal(0, fixture.Session.ReadHighWatermarks(cancellationToken: TestContext.Current.CancellationToken)[0]);
        Assert.Single(fixture.MetadataTimeouts);
        Assert.Single(fixture.QueryTimeouts);
    }

    [Fact]
    public void Given_StickyInitialFailureAndLaterSuccess_When_Installing_Then_Check11PreventsEveryQuery()
    {
        using var fixture = new KafkaSessionTestSupport();
        fixture.Poll = () =>
        {
            fixture.AcquireToken = _ => throw new InvalidOperationException("synthetic acquisition failure");
            fixture.Refresh();
            fixture.AcquireToken = null;
            fixture.Refresh();
        };
        Assert.Throws<VerifierKafkaOperationException>(() => fixture.Session.InstallToken(TestContext.Current.CancellationToken));
        Assert.NotNull(fixture.Authentication.InstalledTokenExpiresOn);
        Assert.Throws<VerifierKafkaOperationException>(() => fixture.Session.ReadHighWatermarks(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(fixture.MetadataTimeouts);
        Assert.Empty(fixture.QueryTimeouts);
    }

    [Theory]
    [InlineData(999, true)]
    [InlineData(1000, false)]
    [InlineData(1001, false)]
    public void Given_TokenAcquisitionNearDeadline_When_CallbackRuns_Then_BudgetCancelsSynchronousAcquisition(int elapsed, bool success)
    {
        using var fixture = new KafkaSessionTestSupport();
        bool cancelled = false;
        fixture.AcquireToken = token =>
        {
            fixture.Clock.Advance(elapsed);
            cancelled = token.IsCancellationRequested;
            token.ThrowIfCancellationRequested();
            return new AccessToken("synthetic", fixture.Clock.GetUtcNow().AddHours(1));
        };
        fixture.Poll = fixture.Refresh;
        if (success) fixture.Session.InstallToken(TestContext.Current.CancellationToken);
        else Assert.Throws<VerifierKafkaOperationException>(() => fixture.Session.InstallToken(TestContext.Current.CancellationToken));
        Assert.Equal(!success, cancelled);
        Assert.Empty(fixture.MetadataTimeouts);
    }

    [Fact]
    public void Given_UninstalledToken_When_PollingUntilDeadline_Then_PollReceivesOnlyRemainingBudget()
    {
        using var fixture = new KafkaSessionTestSupport();
        fixture.Options.PollSlice = TimeSpan.FromMilliseconds(600);
        fixture.Poll = () => fixture.Clock.Advance(fixture.PollTimeouts.Count == 1 ? 600 : 400);
        Assert.Throws<VerifierKafkaOperationException>(() => fixture.Session.InstallToken(TestContext.Current.CancellationToken));
        Assert.Equal([TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(400)], fixture.PollTimeouts);
        Assert.Empty(fixture.MetadataTimeouts);
    }

    [Theory]
    [InlineData("poll")]
    [InlineData("metadata")]
    [InlineData("watermark")]
    public void Given_NativeOperationThrows_When_Executing_Then_FailureIsOwnedByCheck11(string phase)
    {
        using var fixture = new KafkaSessionTestSupport();
        static void Fail() => throw new KafkaException(new Error(ErrorCode.Local_TimedOut));
        if (phase == "poll") fixture.Poll = Fail;
        else
        {
            fixture.Install();
            if (phase == "metadata") fixture.ReadMetadata = () => { Fail(); return null!; };
            else fixture.Query = _ => { Fail(); return null!; };
        }
        Assert.Throws<VerifierKafkaOperationException>(() =>
        {
            if (phase == "poll") fixture.Session.InstallToken(TestContext.Current.CancellationToken);
            else fixture.Session.ReadHighWatermarks(cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [Theory]
    [InlineData("metadata", 999, true)]
    [InlineData("metadata", 1000, false)]
    [InlineData("metadata", 1001, false)]
    [InlineData("watermark", 999, true)]
    [InlineData("watermark", 1000, false)]
    [InlineData("watermark", 1001, false)]
    public void Given_QueryNearItsDeadline_When_Executing_Then_EqualityExpiresTheOperation(string phase, int elapsed, bool success)
    {
        using var fixture = new KafkaSessionTestSupport();
        fixture.Install();
        if (phase == "metadata") fixture.ReadMetadata = () => { fixture.Clock.Advance(elapsed); return KafkaSessionTestSupport.Metadata(0); };
        else fixture.Query = _ => { fixture.Clock.Advance(elapsed); return new WatermarkOffsets(0, 0); };
        if (success) Assert.Equal(0, fixture.Session.ReadHighWatermarks(cancellationToken: TestContext.Current.CancellationToken)[0]);
        else Assert.Throws<VerifierKafkaOperationException>(() => fixture.Session.ReadHighWatermarks(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Given_MultiplePartitions_When_ReadingWatermarks_Then_EachQueryUsesRemainingPhaseBudgetAndOriginIsBeforeFirstQuery()
    {
        using var fixture = new KafkaSessionTestSupport();
        fixture.Install();
        fixture.ReadMetadata = () => { fixture.Clock.Advance(300); return KafkaSessionTestSupport.Metadata(0, 1); };
        int callbacks = 0;
        fixture.Query = partition =>
        {
            Assert.Equal(1, callbacks);
            fixture.Clock.Advance(200);
            return new WatermarkOffsets(0, partition.Partition.Value + 5);
        };
        IReadOnlyDictionary<int, long> highs = fixture.Session.ReadHighWatermarks(() =>
        {
            callbacks++;
            Assert.Empty(fixture.QueryTimeouts);
            Assert.Equal(TimeSpan.FromMilliseconds(300).Ticks, fixture.Clock.GetTimestamp());
        }, TestContext.Current.CancellationToken);
        Assert.Equal(5, highs[0]);
        Assert.Equal(6, highs[1]);
        Assert.Equal([TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(800)], fixture.QueryTimeouts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Given_PartitionCount_When_ReadingMetadata_Then_EmptyFails11AndExcessIsExecutionLimit(int count)
    {
        using var fixture = new KafkaSessionTestSupport();
        fixture.Install();
        fixture.ReadMetadata = () => KafkaSessionTestSupport.Metadata(Enumerable.Range(0, count).ToArray());
        if (count == 0) Assert.Throws<VerifierKafkaOperationException>(() => fixture.Session.ReadHighWatermarks(cancellationToken: TestContext.Current.CancellationToken));
        else if (count > 2) Assert.Throws<InputLimitExceededException>(() => fixture.Session.ReadHighWatermarks(cancellationToken: TestContext.Current.CancellationToken));
        else Assert.Equal(count, fixture.Session.ReadHighWatermarks(cancellationToken: TestContext.Current.CancellationToken).Count);
        Assert.Equal(count is 1 or 2 ? count : 0, fixture.QueryTimeouts.Count);
    }

    [Fact]
    public void Given_AdminCleanupThrows_When_Cleaning_Then_ConsumerStillDisposesBeforeErrorReturns()
    {
        using var fixture = new KafkaSessionTestSupport();
        fixture.AdminDispose = () => throw new InvalidOperationException("synthetic cleanup failure");
        Assert.Throws<AggregateException>(() => fixture.Session.Cleanup(TestContext.Current.CancellationToken));
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Events);
    }

    [Theory]
    [InlineData("missing-topic")]
    [InlineData("topic-error")]
    [InlineData("partition-error")]
    public void Given_MetadataErrors_When_ReadingWatermarks_Then_Check11PreventsQueries(string error)
    {
        using var fixture = new KafkaSessionTestSupport();
        fixture.Install();
        fixture.ReadMetadata = () => new Metadata([], error == "missing-topic" ? [] :
            [new TopicMetadata("document-events", [new PartitionMetadata(0, 0, [], [],
                new Error(error == "partition-error" ? ErrorCode.LeaderNotAvailable : ErrorCode.NoError))],
                new Error(error == "topic-error" ? ErrorCode.UnknownTopicOrPart : ErrorCode.NoError))], 0, "synthetic");
        Assert.Throws<VerifierKafkaOperationException>(() => fixture.Session.ReadHighWatermarks(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(fixture.QueryTimeouts);
    }

    [Fact]
    public void Given_CancelledCleanup_When_Cleaning_Then_AllNativeDisposalFinishesBeforeCancellationReturns()
    {
        using var fixture = new KafkaSessionTestSupport();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => fixture.Session.Cleanup(cancellation.Token));
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Events);
    }
}
#pragma warning restore CA1707
