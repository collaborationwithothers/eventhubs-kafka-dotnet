using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
public sealed class B10AbruptFailedBuildCleanupTests
{
    [Fact]
    public void Given_BlockedCaptureClose_When_BuildFails_Then_OriginalFailureIsReportedWithoutWaiting()
    {
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int closes = 0;
        ValueTask Close() { closes++; return new ValueTask(blocked.Task); }
        var error = new StringWriter();
        var failure = new InvalidOperationException("native_build_failure");

        B10AbruptHost.CloseFailedBuildCapture(Close, Close);
        int exit = B10AbruptHost.ReportFailure(failure, error);

        Assert.Equal(2, closes);
        Assert.False(blocked.Task.IsCompleted);
        Assert.Equal(1, exit);
        Assert.Contains("System.InvalidOperationException", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("124", error.ToString(), StringComparison.Ordinal);
        blocked.SetResult();
    }
}
