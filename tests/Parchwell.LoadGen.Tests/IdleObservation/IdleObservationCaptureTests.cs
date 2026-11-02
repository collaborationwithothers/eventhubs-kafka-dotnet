using Confluent.Kafka;
using Parchwell.LoadGen.IdleObservation;
using Parchwell.LoadGen.Tests.SingleAction;
using Xunit;

namespace Parchwell.LoadGen.Tests.IdleObservation;

#pragma warning disable CA1707
public sealed class IdleObservationCaptureTests
{
    private static IdleObservationCapture Capture(int maxStatisticsReports = 80, IdleObservationOptions? options = null) =>
        new(options ?? IdleObservationOptionsTests.Options(), new SingleActionCaptureTests.AdvancingClock(), 0, maxStatisticsReports, new IdleObservationClientLogBudget(1));

    [Fact]
    public void Given_SixtyFiveErrorCallbacks_When_Snapshotting_Then_KeepsSixtyFourAndCountsOneDropWithoutLimit()
    {
        var capture = Capture();
        for (int index = 0; index < 65; index++) capture.Error(new Error(ErrorCode.Local_Fail, "failed"));
        var snapshot = capture.Snapshot();
        Assert.Equal(64, snapshot.Errors.Length);
        Assert.Equal(1, snapshot.Dropped);
        Assert.DoesNotContain("capture_limit", snapshot.Diagnostics);
    }

    [Fact]
    public void Given_ErrorReasonOverFourKiB_When_Capturing_Then_MarksLimit()
    {
        var capture = Capture();
        capture.Error(new Error(ErrorCode.Local_Fail, new string('x', 4097)));
        var snapshot = capture.Snapshot();
        Assert.Empty(snapshot.Errors);
        Assert.Contains("capture_limit", snapshot.Diagnostics);
    }

    [Fact]
    public void Given_EightyOneStatisticsReports_When_CapIsEighty_Then_KeepsEightyAndMarksLimit()
    {
        var capture = Capture(80);
        for (int index = 0; index < 81; index++)
        {
            capture.Statistics("{}");
            capture.Drain();
        }
        var snapshot = capture.Snapshot();
        Assert.Equal(80, snapshot.Statistics.Length);
        Assert.Equal(Enumerable.Range(1, 80), snapshot.Statistics.Select(report => report.Sequence));
        Assert.Contains("capture_limit", snapshot.Diagnostics);
    }

    [Fact]
    public void Given_ReportOverTheByteLimit_When_Capturing_Then_MarksLimit()
    {
        var capture = Capture();
        capture.Statistics(new string('s', 262145));
        var snapshot = capture.Snapshot();
        Assert.Empty(snapshot.Statistics);
        Assert.Contains("capture_limit", snapshot.Diagnostics);
    }

    [Fact]
    public void Given_ReportWithLoneSurrogate_When_Capturing_Then_MarksLimit()
    {
        var capture = Capture();
        capture.Statistics("{\"a\":\"\ud800\"}");
        var snapshot = capture.Snapshot();
        Assert.Empty(snapshot.Statistics);
        Assert.Contains("capture_limit", snapshot.Diagnostics);
    }

    [Fact]
    public void Given_TwentyDiagnostics_When_Capturing_Then_KeepsNineteenAndMarksLimit()
    {
        var capture = Capture();
        for (int index = 0; index < 20; index++) capture.Diagnostic("diagnostic-" + index);
        var snapshot = capture.Snapshot();
        Assert.Equal(20, snapshot.Diagnostics.Length);
        Assert.Equal(19, snapshot.Diagnostics.Count(value => value.StartsWith("diagnostic-", StringComparison.Ordinal)));
        Assert.Equal("capture_limit", snapshot.Diagnostics[^1]);
    }

    [Fact]
    public void Given_OneHundredTwentyNineUndrainedReports_When_CapIsTwoHundred_Then_QueueOverflowMarksLimit()
    {
        var capture = Capture(200);
        for (int index = 0; index < 129; index++) capture.Statistics("{}");
        var snapshot = capture.Snapshot();
        Assert.Equal(128, snapshot.Statistics.Length);
        Assert.Contains("capture_limit", snapshot.Diagnostics);
    }

    [Fact]
    public async Task Given_SecondDelivery_When_Reported_Then_CompletesOnlyAttemptTwoAndKeepsFirstReport()
    {
        var capture = Capture();
        capture.Delivered(2, new Error(ErrorCode.NoError));
        Assert.True(capture.Delivery(2).IsCompleted);
        Assert.False(capture.Delivery(1).IsCompleted);
        capture.Delivered(1, new Error(ErrorCode.NoError));
        capture.Delivered(1, new Error(ErrorCode.TopicAuthorizationFailed));
        var first = await capture.Delivery(1);
        Assert.False(first.Error.IsError);
    }
}
