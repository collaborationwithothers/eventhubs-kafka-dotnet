using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2InputFileBoundaryTests
{
    [Theory]
    [InlineData(0)][InlineData(65535)][InlineData(65536)][InlineData(65537)]
    public async Task Given_MountedFileLength_When_Read_Then_ExactLimitIsInclusiveAndEmptyRemainsEmpty(int length)
    {
        string path = Path.GetTempFileName();
        try
        {
            byte[] bytes = Enumerable.Repeat((byte)32,length).ToArray();
            await File.WriteAllBytesAsync(path,bytes,TestContext.Current.CancellationToken);
            var inputs = new ProbeV2Inputs();
            if (length > 65536) await Assert.ThrowsAsync<InputLimitExceededException>(() => inputs.ReadAsync(path,65536,TestContext.Current.CancellationToken));
            else Assert.Equal(bytes,await inputs.ReadAsync(path,65536,TestContext.Current.CancellationToken));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Given_AbsentMountedFile_When_Read_Then_AbsenceIsDistinctFromEmptyBytes()
    {
        string path = Path.Combine(Path.GetTempPath(),"probe-v2-missing-" + Guid.NewGuid().ToString("N"));
        Assert.Null(await new ProbeV2Inputs().ReadAsync(path,65536,TestContext.Current.CancellationToken));
    }
}
