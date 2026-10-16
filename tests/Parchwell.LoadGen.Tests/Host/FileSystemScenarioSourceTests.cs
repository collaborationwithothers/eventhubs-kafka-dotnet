using Parchwell.LoadGen.Run.Input;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class FileSystemScenarioSourceTests
{
    [Fact]
    public async Task Given_ValidLoaderPath_When_Opening_Then_ReadsOnlyRequestedScenarioFile()
    {
        string root = CreateScenarioRoot();
        try
        {
            var source = new FileSystemScenarioSource(root);
            using TextReader reader = await source.OpenReadAsync("scenarios/A01/scenario.yaml", TestContext.Current.CancellationToken);
            Assert.Equal("id: A01", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside.yaml")]
    [InlineData("scenarios/../outside.yaml")]
    [InlineData("/etc/passwd")]
    [InlineData("scenarios/A01/expected.yaml")]
    public async Task Given_OutsideOrNonLoaderPath_When_Opening_Then_RejectsPath(string path)
    {
        string root = CreateScenarioRoot();
        try
        {
            var source = new FileSystemScenarioSource(root);
            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await source.OpenReadAsync(path, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Given_SymlinkedScenarioDirectory_When_Opening_Then_RejectsAllSymlinks()
    {
        string root = CreateScenarioRoot();
        string outside = Path.Combine(Path.GetTempPath(), "loadgen-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "scenario.yaml"), "id: A01",
            TestContext.Current.CancellationToken);
        try
        {
            Directory.Delete(Path.Combine(root, "scenarios", "A01"), recursive: true);
            Directory.CreateSymbolicLink(Path.Combine(root, "scenarios", "A01"), outside);
            var source = new FileSystemScenarioSource(root);
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await source.OpenReadAsync("scenarios/A01/scenario.yaml", TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    private static string CreateScenarioRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "loadgen-scenario-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "scenarios", "A01"));
        File.WriteAllText(Path.Combine(root, "scenarios", "A01", "scenario.yaml"), "id: A01");
        File.WriteAllText(Path.Combine(root, "scenarios", "A01", "expected.yaml"), "outcome: ignored");
        return root;
    }
}
#pragma warning restore CA1707
