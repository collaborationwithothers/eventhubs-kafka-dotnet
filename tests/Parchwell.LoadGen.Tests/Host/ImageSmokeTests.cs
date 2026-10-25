using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class ImageSmokeTests
{
    [Fact]
    public async Task Given_ImageSmokeFlag_When_StartingProcess_Then_LoadsNativeLibraryAndRegularPackagedFilesWithoutHostComposition()
    {
        bool hostComposed = false;
        Assert.Equal(0, await Program.RunAsync(["--image-smoke"], _ => hostComposed = true));
        Assert.False(hostComposed);
    }
}
#pragma warning restore CA1707
