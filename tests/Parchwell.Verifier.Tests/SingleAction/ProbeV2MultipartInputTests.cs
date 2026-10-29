using System.Security.Cryptography;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2MultipartInputTests
{
    [Theory]
    [InlineData(0)][InlineData(1)]
    [InlineData(524287)][InlineData(524288)][InlineData(524289)]
    [InlineData(2097151)][InlineData(2097152)]
    public async Task Given_FourPartsWithinBounds_When_Read_Then_OriginalBytesAndHashArePreserved(int length)
    {
        using var fixture = new Fixture();
        byte[] bytes = Enumerable.Range(0, length).Select(index => (byte)(index % 251)).ToArray();
        await fixture.WriteAsync(bytes);
        byte[]? read = await new ProbeV2Inputs().ReadAsync(fixture.Path, 2097152, TestContext.Current.CancellationToken);
        Assert.Equal(bytes, read);
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(read!));
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(1)]
    public async Task Given_CombinedLengthAroundMaximum_When_Read_Then_ExactMaximumIsInclusive(int offset)
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync(new byte[1048576 + offset]);
        var inputs = new ProbeV2Inputs();
        if (offset > 0) await Assert.ThrowsAsync<InputLimitExceededException>(() => inputs.ReadAsync(fixture.Path, 1048576, TestContext.Current.CancellationToken));
        else Assert.Equal(1048576 + offset, (await inputs.ReadAsync(fixture.Path, 1048576, TestContext.Current.CancellationToken))!.Length);
    }

    [Theory]
    [InlineData(524287)][InlineData(524288)][InlineData(524289)]
    public async Task Given_PartLengthAroundMaximum_When_Read_Then_ExactPartMaximumIsInclusive(int length)
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync([]);
        await File.WriteAllBytesAsync(fixture.Path + ".part0", new byte[length], TestContext.Current.CancellationToken);
        var inputs = new ProbeV2Inputs();
        if (length > 524288) await Assert.ThrowsAsync<InputLimitExceededException>(() => inputs.ReadAsync(fixture.Path, 2097152, TestContext.Current.CancellationToken));
        else Assert.Equal(length, (await inputs.ReadAsync(fixture.Path, 2097152, TestContext.Current.CancellationToken))!.Length);
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public async Task Given_OneMissingPart_When_Read_Then_NoPartialEvidenceIsReturned(int missing)
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync([1, 2, 3]);
        File.Delete(fixture.Path + ".part" + missing);
        Assert.Null(await new ProbeV2Inputs().ReadAsync(fixture.Path, 2097152, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(65537)]
    public async Task Given_PrimaryCaptureAndValidParts_When_Read_Then_PrimaryTakesPrecedenceIncludingEmptyAndOversize(int length)
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync([1, 2, 3]);
        byte[] primary = new byte[length];
        await File.WriteAllBytesAsync(fixture.Path, primary, TestContext.Current.CancellationToken);
        var inputs = new ProbeV2Inputs();
        if (length > 65536) await Assert.ThrowsAsync<InputLimitExceededException>(() => inputs.ReadAsync(fixture.Path, 65536, TestContext.Current.CancellationToken));
        else Assert.Equal(primary, await inputs.ReadAsync(fixture.Path, 65536, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_CanceledMultipartRead_When_Read_Then_CancellationPropagates()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync([1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProbeV2Inputs().ReadAsync(fixture.Path, 2097152, cancellation.Token));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "probe-v2-parts-" + Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(directory, "capture.json");
        internal Fixture() => Directory.CreateDirectory(directory);
        internal async Task WriteAsync(byte[] bytes)
        {
            for (int part = 0; part < 4; part++)
            {
                int start = Math.Min(part * 524288, bytes.Length);
                int count = Math.Min(524288, bytes.Length - start);
                await File.WriteAllBytesAsync(Path + ".part" + part, bytes.AsMemory(start, count).ToArray(), TestContext.Current.CancellationToken);
            }
        }
        public void Dispose() => Directory.Delete(directory, true);
    }
}
