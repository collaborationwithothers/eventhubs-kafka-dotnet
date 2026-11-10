using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class KafkaStatisticsLateCaptureTests
{
    private static readonly string[] ExpectedBroker = ["broker/1"];

    // A callback that starts after completion closed capture was never going to be written, so it
    // must not fail a run whose accepted reports all drained.
    [Theory]
    [InlineData("capture-only")]
    [InlineData("inventory")]
    public async Task Given_CallbackAfterCaptureClosed_When_CompletionSeals_Then_CompletionStaysComplete(string mode)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        bool inventory = mode == "inventory";
        var clock = new PausingClock();
        var sink = new HoldingStatisticsSink(_ => true);
        using var metrics = new KafkaStatisticsMetrics();
        await using KafkaStatisticsSession session = LateCaptureTestSupport.NewSession(clock, sink, metrics);

        clock.Advance(TimeSpan.FromSeconds(1));
        Task<KafkaStatisticsCompletion> completionTask = inventory
            ? session.CompleteAsync(ExpectedBroker, token)
            : session.CompleteAsync(token);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        KafkaStatisticsCompletion completion;
        try
        {
            session.Capture(LateCaptureTestSupport.Report(inventory ? 9_999_999 : 10_000_001));
            await sink.EnteredAsync(1, token);
            session.Capture(LateCaptureTestSupport.Report(inventory ? 10_000_001 : 10_000_002));
            sink.Release(1);
            await sink.EnteredAsync(2, token);
            await SessionProbe.WaitUntilCaptureClosedAsync(session, token);
            session.Capture(LateCaptureTestSupport.Report(10_000_003));
            sink.Release(2);
            completion = await completionTask.WaitAsync(LateCaptureTestSupport.Watchdog, token);
        }
        finally
        {
            sink.ReleaseAll();
        }

        Assert.Equal(string.Empty, string.Join(",", completion.Diagnostics.Select(item => item.Code)));
        Assert.True(completion.IsComplete);
        Assert.Empty(completion.MissingBrokers);
        Assert.Equal(inventory ? new long[] { 9_999_999, 10_000_001 } : [10_000_001, 10_000_002], sink.WrittenTimestamps);
    }

    // A callback admitted before capture closed and refused at the gate after it is a real gap, so
    // the fix must keep reporting it.
    [Fact]
    public async Task Given_CallbackAdmittedBeforeCaptureClosed_When_ItReachesGateAfterClose_Then_LossIsReported()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var clock = new PausingClock();
        var sink = new HoldingStatisticsSink(_ => true);
        using var metrics = new KafkaStatisticsMetrics();
        await using KafkaStatisticsSession session = LateCaptureTestSupport.NewSession(clock, sink, metrics);

        clock.Advance(TimeSpan.FromSeconds(1));
        Task<KafkaStatisticsCompletion> completionTask = session.CompleteAsync(token);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        KafkaStatisticsCompletion completion;
        Task? pausedCapture = null;
        try
        {
            session.Capture(LateCaptureTestSupport.Report(10_000_001));
            await sink.EnteredAsync(1, token);
            session.Capture(LateCaptureTestSupport.Report(10_000_002));
            clock.PauseNextTimestamp();
            pausedCapture = Task.Run(() => session.Capture(LateCaptureTestSupport.Report(10_000_003)), token);
            await clock.PausedAsync(token);
            sink.Release(1);
            await sink.EnteredAsync(2, token);
            await SessionProbe.WaitUntilCaptureClosedAsync(session, token);
            SessionProbe.WaitUntilGateFree(session);
            clock.Resume();
            await pausedCapture.WaitAsync(LateCaptureTestSupport.Watchdog, token);
            sink.Release(2);
            completion = await completionTask.WaitAsync(LateCaptureTestSupport.Watchdog, token);
        }
        finally
        {
            clock.Resume();
            sink.ReleaseAll();
            if (pausedCapture is not null)
            {
                await pausedCapture.WaitAsync(LateCaptureTestSupport.Watchdog, token);
            }
        }

        Assert.Equal("capture_loss,late_capture", string.Join(",", completion.Diagnostics.Select(item => item.Code)));
        Assert.False(completion.IsComplete);
        Assert.Equal(new long[] { 10_000_001, 10_000_002 }, sink.WrittenTimestamps);
    }

    // Disposal is not the close that makes a later callback ignorable (R6): a call after disposal
    // without completion keeps recording loss, so a mutant that closes admission in DisposeAsync fails.
    [Fact]
    public async Task Given_SessionDisposedWithoutCompletion_When_CaptureFollows_Then_LateCaptureAndLossAreReported()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var clock = new PausingClock();
        var sink = new HoldingStatisticsSink(_ => false);
        using var metrics = new KafkaStatisticsMetrics();
        await using KafkaStatisticsSession session = LateCaptureTestSupport.NewSession(clock, sink, metrics);

        await session.DisposeAsync();
        session.Capture(StatisticsTestSupport.Report("test-client", 10_000_001, 1_000_001));
        KafkaStatisticsCompletion completion = await session.CompleteAsync(token);

        Assert.Equal(
            "abandoned_without_completion,capture_loss,disposed_before_completion,late_capture",
            string.Join(",", completion.Diagnostics.Select(item => item.Code)));
        Assert.False(completion.IsComplete);
        Assert.Empty(completion.MissingBrokers);
        Assert.Empty(sink.WrittenTimestamps);
    }
}
