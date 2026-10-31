using Confluent.Kafka;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleActionV2;

#pragma warning disable CA1707
public sealed class ProbeV2HandlerRefreshPathTests
{
    [Theory]
    [InlineData(19999, true)]
    [InlineData(20000, false)]
    [InlineData(20001, false)]
    public void GivenAdmissionAtBoundary_WhenCheckingWindow_ThenOnlyStrictlyBeforeLimitIsAdmitted(int elapsed, bool expected)
    {
        var clock = new TestClock();
        var path = new ProbeV2HandlerRefreshPath(clock, 1, "synthetic-client");
        path.StartWindow(clock.GetTimestamp());
        clock.Milliseconds = elapsed;

        Assert.Equal(expected, path.TryAdmit(3, CancellationToken.None));
        if (expected)
        {
            Assert.False(path.TryAdmit(4, CancellationToken.None));
            ProbeV2HandlerRefreshMetadata metadata = path.Finish(true, false);
            Assert.True(metadata.OperationAttempted);
            Assert.Equal(3, metadata.TriggerAttempt);
            Assert.Equal(19999, metadata.OperationAdmittedTimestamp);
        }
    }

    [Fact]
    public void GivenSynchronousServiceCallReturnsLate_WhenCapturingAfterObservation_ThenKeepsRawLateTimeButRejectsNextAdmission()
    {
        var clock = new TestClock();
        var path = new ProbeV2HandlerRefreshPath(clock, 2, "synthetic-client");
        path.StartWindow(clock.GetTimestamp());
        Assert.True(path.TryAdmit(3, CancellationToken.None));
        clock.Milliseconds = 20001;
        path.ObserveAfterDelivery(Report(new Message<byte[], byte[]> { Key = [1], Value = [2] }));
        Assert.False(path.TryAdmit(4, CancellationToken.None));
        path.EndWindow();

        ProbeV2HandlerRefreshMetadata metadata = path.Finish(true, false);
        Assert.Equal(20001, metadata.AfterService!.Timestamp);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(20001), metadata.AfterService.ObservedAt);
        Assert.Equal(3, metadata.TriggerAttempt);
    }

    [Fact]
    public void GivenRepeatedDeliveryCallbacks_WhenObservingRawService_ThenKeepsFirstAndMarksDuplicate()
    {
        var clock = new TestClock();
        var path = new ProbeV2HandlerRefreshPath(clock, 1, "synthetic-client");
        path.ObserveBeforeDelivery(Report(new Message<byte[], byte[]> { Key = [1], Value = [2] }));
        clock.Milliseconds = 1;
        path.ObserveBeforeDelivery(Report(new Message<byte[], byte[]> { Key = [3], Value = [4] }));
        path.ObserveAfterDelivery(Report(new Message<byte[], byte[]> { Key = [5], Value = [6] }));
        path.ObserveAfterDelivery(Report(new Message<byte[], byte[]> { Key = [7], Value = [8] }));

        ProbeV2HandlerRefreshMetadata metadata = path.Finish(true, false);
        Assert.Equal(7, metadata.BeforeService!.Offset);
        Assert.Equal("Persisted", metadata.BeforeService.PersistenceStatus);
        Assert.Equal((int)ErrorCode.NoError, metadata.BeforeService.ErrorCode);
        Assert.True(metadata.DuplicateBeforeObservation);
        Assert.True(metadata.DuplicateAfterObservation);
    }

    [Fact]
    public void GivenReadDataAtWireBoundary_WhenObserving_ThenPreservesNullAndEmptyWithoutExceedingBound()
    {
        var clock = new TestClock();
        var requested = new ProbeV2Requested("document-events", "explicit", 0, 7, "", null);
        var path = new ProbeV2HandlerRefreshPath(clock, 1, "synthetic-client");
        path.ObserveBeforeRead(Read(requested, [], []), requested);
        path.ObserveAfterRead(Read(requested, [], null), requested);
        ProbeV2HandlerRefreshMetadata metadata = path.Finish(true, false);
        Assert.Equal("", metadata.BeforeService!.KeyBase64);
        Assert.Equal("", metadata.BeforeService.ValueBase64);
        Assert.Equal("", metadata.AfterService!.KeyBase64);
        Assert.Null(metadata.AfterService.ValueBase64);

        var tooLarge = new ProbeV2HandlerRefreshPath(clock, 1, "synthetic-client");
        tooLarge.ObserveAfterRead(Read(requested, new byte[4097], []), requested);
        Assert.True(tooLarge.Finish(true, false).CaptureLimitReached);
    }

    [Theory]
    [InlineData(4095, false, 4095)]
    [InlineData(4096, true, 4096)]
    [InlineData(4097, true, 4096)]
    public void GivenNonmatchingReadCount_WhenCapturing_ThenEnforcesDiagnosticLimit(int attempted, bool limited, int retained)
    {
        var clock = new TestClock();
        var path = new ProbeV2HandlerRefreshPath(clock, 1, "synthetic-client");
        for (int index = 0; index < attempted; index++) path.RecordNonmatch();
        ProbeV2HandlerRefreshMetadata metadata = path.Finish(true, false);
        Assert.Equal(retained, metadata.DiagnosticNonmatches);
        Assert.Equal(limited, metadata.CaptureLimitReached);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    public void GivenDiagnosticErrors_WhenCapturing_ThenRetainsAtMostEightAndStopsAtTheCap(int attempted, bool limited)
    {
        var clock = new TestClock();
        var path = new ProbeV2HandlerRefreshPath(clock, 1, "synthetic-client");
        for (int index = 0; index < attempted; index++) path.RecordError(new Error(ErrorCode.Local_Authentication));
        ProbeV2HandlerRefreshMetadata metadata = path.Finish(true, false);
        Assert.Equal(Math.Min(attempted, 8), metadata.DiagnosticErrors.Count);
        Assert.Equal(limited, metadata.CaptureLimitReached);
        Assert.All(metadata.DiagnosticErrors, error => Assert.Equal((int)ErrorCode.Local_Authentication, error.Code));
    }

    [Fact]
    public void GivenCleanupFails_WhenFinishingRawCapture_ThenDoesNotReadCallbackState()
    {
        var clock = new TestClock();
        var path = new ProbeV2HandlerRefreshPath(clock, 1, "synthetic-client");
        path.ObserveBeforeDelivery(Report(new Message<byte[], byte[]> { Key = [1], Value = [2] }));

        ProbeV2HandlerRefreshMetadata metadata = path.Finish(false, false);

        Assert.False(metadata.ClientCleanupCompleted);
        Assert.Null(metadata.BeforeService);
        Assert.Null(metadata.AfterService);
        Assert.Empty(metadata.DiagnosticErrors);
    }

    [Fact]
    public void GivenCallerCancellation_WhenAdmittingDiagnosticOperation_ThenRecordsCancellationAndDoesNotAttempt()
    {
        var clock = new TestClock();
        var path = new ProbeV2HandlerRefreshPath(clock, 1, "synthetic-client");
        path.StartWindow(clock.GetTimestamp());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.False(path.TryAdmit(3, cancellation.Token));
        ProbeV2HandlerRefreshMetadata metadata = path.Finish(true, true);
        Assert.True(metadata.Cancelled);
        Assert.False(metadata.OperationAttempted);
    }

    private static DeliveryReport<byte[], byte[]> Report(Message<byte[], byte[]> message) => new()
    {
        Topic = "document-events", Partition = new Partition(0), Offset = new Offset(7), Message = message,
        Status = PersistenceStatus.Persisted, Error = new Error(ErrorCode.NoError)
    };

    private static ConsumeResult<byte[], byte[]> Read(ProbeV2Requested requested, byte[]? key, byte[]? value) => new()
    {
        Topic = requested.Topic, Partition = new Partition(requested.Partition!.Value), Offset = new Offset(requested.Offset!.Value),
        Message = new Message<byte[], byte[]> { Key = key!, Value = value! }
    };
}
#pragma warning restore CA1707
