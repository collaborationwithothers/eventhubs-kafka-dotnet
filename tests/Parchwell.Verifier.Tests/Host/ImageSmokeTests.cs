using System.Globalization;
using System.Text.Json;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class ImageSmokeTests
{
    [Fact]
    public async Task Given_PackagedInputs_When_ImageSmokeRuns_Then_LoadsNativeLibraryAndReportsThreeFiles()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(0, await ImageSmoke.RunAsync(output, AppContext.BaseDirectory));
        using JsonDocument json = JsonDocument.Parse(output.ToString());
        Assert.True(json.RootElement.GetProperty("native_loaded").GetBoolean());
        Assert.Equal(3, json.RootElement.GetProperty("scenario_files").GetInt32());
        Assert.Equal(1654, json.RootElement.GetProperty("uid_expected").GetInt32());
        Assert.False(File.Exists(Path.Combine(AppContext.BaseDirectory, "scenarios/A01/expected.yaml")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_MissingOrSymlinkScenario_When_ImageSmokeRuns_Then_FailsBeforeSuccessOutput(bool symlink)
    {
        string root = Path.Combine(Path.GetTempPath(), "verifier-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "scenarios/A01"));
        try
        {
            if (symlink) File.CreateSymbolicLink(Path.Combine(root, "scenarios/A01/scenario.yaml"),
                Path.Combine(AppContext.BaseDirectory, "scenarios/A01/scenario.yaml"));
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            await Assert.ThrowsAsync<InvalidOperationException>(() => ImageSmoke.RunAsync(output, root));
            Assert.Equal("", output.ToString());
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
#pragma warning restore CA1707
