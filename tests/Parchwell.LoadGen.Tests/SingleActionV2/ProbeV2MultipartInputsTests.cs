using Xunit;

namespace Parchwell.LoadGen.Tests.SingleActionV2;

#pragma warning disable CA1707
public sealed partial class ProbeV2InputsTests
{
    [Theory]
    [InlineData(524287)][InlineData(524288)][InlineData(524289)]
    [InlineData(2097151)][InlineData(2097152)]
    public async Task Given_CompleteFourPartCapture_When_ReadHelperAdmits_Then_OriginalEvidenceIsAccepted(int length)
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync([true, true, true, true, true]);
        byte[] original = await File.ReadAllBytesAsync(fixture.CapturePath, TestContext.Current.CancellationToken);
        byte[] padded = Enumerable.Repeat((byte)32, length).ToArray();
        original.CopyTo(padded, 0);
        await WritePartsAsync(fixture.CapturePath, padded);
        File.Delete(fixture.CapturePath);
        var admission = await fixture.Inputs.AdmitAsync(fixture.Stage, 1, TestContext.Current.CancellationToken);
        Assert.Equal("required", admission.Admission);
        Assert.Equal(21, admission.Requested!.Offset);
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(1)]
    public async Task Given_CombinedCaptureAroundMaximum_When_ReadHelperAdmits_Then_ExactMaximumIsInclusive(int offset)
    {
        using var fixture = new Fixture(1048576);
        await fixture.WriteAsync([true, true, true, true, true]);
        byte[] original = await File.ReadAllBytesAsync(fixture.CapturePath, TestContext.Current.CancellationToken);
        byte[] padded = Enumerable.Repeat((byte)32, 1048576 + offset).ToArray();
        original.CopyTo(padded, 0);
        await WritePartsAsync(fixture.CapturePath, padded);
        File.Delete(fixture.CapturePath);
        var admission = await fixture.Inputs.AdmitAsync(fixture.Stage, 1, TestContext.Current.CancellationToken);
        Assert.Equal(offset <= 0 ? "required" : "not_attempted_dependency", admission.Admission);
    }

    [Fact]
    public async Task Given_ValidPrimaryWithInvalidParts_When_ReadHelperAdmits_Then_PrimaryTakesPrecedence()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync([true, true, true, true, true]);
        await File.WriteAllBytesAsync(fixture.CapturePath + ".part0", new byte[524289], TestContext.Current.CancellationToken);
        Assert.Equal("required", (await fixture.Inputs.AdmitAsync(fixture.Stage, 1, TestContext.Current.CancellationToken)).Admission);
    }

    [Theory]
    [InlineData("missing0")][InlineData("missing1")][InlineData("missing2")][InlineData("missing3")]
    [InlineData("oversize")][InlineData("empty")][InlineData("invalid_primary")]
    public async Task Given_InvalidMultipartCapture_When_ReadHelperAdmits_Then_DependencyIsNotAttempted(string fault)
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync([true, true, true, true, true]);
        byte[] original = await File.ReadAllBytesAsync(fixture.CapturePath, TestContext.Current.CancellationToken);
        await WritePartsAsync(fixture.CapturePath, fault == "empty" ? [] : original);
        File.Delete(fixture.CapturePath);
        if (fault.StartsWith("missing", StringComparison.Ordinal)) File.Delete(fixture.CapturePath + ".part" + fault[^1]);
        if (fault == "oversize") await File.WriteAllBytesAsync(fixture.CapturePath + ".part0", new byte[524289], TestContext.Current.CancellationToken);
        if (fault == "invalid_primary") await File.WriteAllTextAsync(fixture.CapturePath, "{}", TestContext.Current.CancellationToken);
        var admission = await fixture.Inputs.AdmitAsync(fixture.Stage, 1, TestContext.Current.CancellationToken);
        Assert.Equal("not_attempted_dependency", admission.Admission);
        Assert.Null(admission.Requested);
    }

    [Fact]
    public async Task Given_CanceledMultipartRead_When_ReadHelperAdmits_Then_CancellationPropagates()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync([true, true, true, true, true]);
        byte[] original = await File.ReadAllBytesAsync(fixture.CapturePath, TestContext.Current.CancellationToken);
        await WritePartsAsync(fixture.CapturePath, original);
        File.Delete(fixture.CapturePath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Inputs.AdmitAsync(fixture.Stage, 1, cancellation.Token));
    }

    private static async Task WritePartsAsync(string path, byte[] bytes)
    {
        for (int part = 0; part < 4; part++)
        {
            int start = Math.Min(part * 524288, bytes.Length);
            int count = Math.Min(524288, bytes.Length - start);
            await File.WriteAllBytesAsync(path + ".part" + part, bytes.AsMemory(start, count).ToArray(), TestContext.Current.CancellationToken);
        }
    }
}
