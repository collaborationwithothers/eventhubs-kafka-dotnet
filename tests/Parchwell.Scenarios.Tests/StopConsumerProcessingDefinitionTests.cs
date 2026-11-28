using System.Security.Cryptography;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Protects scenario-contract.md, "Consumer processing declarations", for C18: the committed file, its stop entries,
// checks 1, 6 and 7, and the approved expected.yaml bytes.
[Trait("Category", "Unit")]
public sealed class StopConsumerProcessingDefinitionTests
{
    private const string Profile = "scenarios/profiles/c01-consumer.yaml";
    private const string ProfileSlug = "c01-consumer";

    [Fact]
    public async Task Loads_C18()
    {
        var loaded = await Load("C18", Text("scenarios/C18/scenario.yaml"));
        var processing = loaded.Definition.ConsumerProcessing!;
        Assert.Equal(360000, processing.WindowMs);
        Assert.Equal(30000, processing.CompletionDeadlineMs);
        Assert.Equal(2, processing.Stops.Count);
        Assert.Equal(new ConsumerStopDeclaration(60000, "close"), processing.Stops["C0"]);
        Assert.Equal(new ConsumerStopDeclaration(60000, "dispose"), processing.Stops["C1"]);
        Assert.Empty(processing.KillCounts);
        Assert.Empty(processing.Configurations);
    }

    [Theory]
    [InlineData("C03")]
    [InlineData("C10")]
    [InlineData("C01")]
    public async Task Given_OtherConsumerItems_When_Loading_Then_StopsIsEmpty(string item)
    {
        var profile = item == "C01" ? "c01-consumer" : "c03-consumer";
        var loaded = await Load(item, Text($"scenarios/{item}/scenario.yaml"), Text($"scenarios/profiles/{profile}.yaml"), profile);
        Assert.Empty(loaded.Definition.ConsumerProcessing!.Stops);
    }

    public static TheoryData<string, string, string, string> RejectedDeclarations() => new()
    {
        { "after 59999", "C0:\n      stop: {after_start_line_ms: 60000, shutdown_mode: close}", "C0:\n      stop: {after_start_line_ms: 59999, shutdown_mode: close}", "consumer_processing_literal|consumer_processing.configurations.C0.stop.after_start_line_ms" },
        { "C0 mode drain", "shutdown_mode: close}", "shutdown_mode: drain}", "consumer_processing_literal|consumer_processing.configurations.C0.stop.shutdown_mode" },
        { "C0 mode dispose", "shutdown_mode: close}", "shutdown_mode: dispose}", "consumer_processing_literal|consumer_processing.configurations.C0.stop.shutdown_mode" },
        { "C1 mode close", "shutdown_mode: dispose}", "shutdown_mode: close}", "consumer_processing_literal|consumer_processing.configurations.C1.stop.shutdown_mode" },
        { "kill count beside stop", "C0:\n      stop: {after_start_line_ms: 60000, shutdown_mode: close}", "C0:\n      kill_count: 1\n      stop: {after_start_line_ms: 60000, shutdown_mode: close}", "consumer_processing_shape|consumer_processing.configurations.C0" },
        { "stop missing", "C0:\n      stop: {after_start_line_ms: 60000, shutdown_mode: close}", "C0: {}", "consumer_processing_shape|consumer_processing.configurations.C0" },
        { "extra stop field", "after_start_line_ms: 60000, shutdown_mode: close}", "after_start_line_ms: 60000, shutdown_mode: close, grace: 1}", "consumer_processing_shape|consumer_processing.configurations.C0" },
        { "C1 override", "  C1: {}\n", "  C1:\n    EnrichmentConsumer:\n      session.timeout.ms: 45000\n", "consumer_processing_experiment|consumer_processing" },
        { "repeats 6", "repeats: 5", "repeats: 6", "consumer_processing_experiment|consumer_processing" }
    };

    [Theory]
    [MemberData(nameof(RejectedDeclarations))]
    public async Task Rejects(string name, string from, string to, string expected)
    {
        Assert.NotNull(name);
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load("C18", Replace(Text("scenarios/C18/scenario.yaml"), from, to)));
        var parts = expected.Split('|');
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == parts[0] && diagnostic.FieldPath == parts[1]);
    }

    [Fact]
    public async Task Given_SwappedModes_When_Loading_Then_BothModePathsFail()
    {
        var yaml = Text("scenarios/C18/scenario.yaml").Replace("shutdown_mode: close}", "shutdown_mode: X}", StringComparison.Ordinal)
            .Replace("shutdown_mode: dispose}", "shutdown_mode: close}", StringComparison.Ordinal).Replace("shutdown_mode: X}", "shutdown_mode: dispose}", StringComparison.Ordinal);
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load("C18", yaml));
        Assert.Contains(error.Diagnostics, d => d.Code == "consumer_processing_literal" && d.FieldPath == "consumer_processing.configurations.C0.stop.shutdown_mode");
        Assert.Contains(error.Diagnostics, d => d.Code == "consumer_processing_literal" && d.FieldPath == "consumer_processing.configurations.C1.stop.shutdown_mode");
    }

    [Fact]
    public async Task Admission()
    {
        var yaml = Replace(Text("scenarios/C18/scenario.yaml"), "id: C18", "id: C19");
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load("C19", yaml));
        Assert.Contains(error.Diagnostics, d => d.Code == "consumer_processing_unexpected");
    }

    [Fact]
    public void File_is_pinned()
    {
        // The hash is of the section 5.2 block of the approved c18-scenario plan (revision 5), LF endings and a final newline.
        var bytes = File.ReadAllBytes(Path.Combine(Root(), "scenarios", "C18", "expected.yaml"));
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.Equal("dd5d9774e505b64fe2250ec0f7f42ee209ba6b639f7168d9572877047596acde", Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    private static string Replace(string text, string from, string to)
    {
        Assert.Equal(1, text.Split(from, StringSplitOptions.None).Length - 1);
        return text.Replace(from, to, StringComparison.Ordinal);
    }

    private static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Parchwell.slnx"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("repository_root");
        return root;
    }

    private static string Text(string path) => File.ReadAllText(Path.Combine(Root(), path));

    private static Task<LoadedScenario> Load(string item, string yaml) => Load(item, yaml, Text(Profile), ProfileSlug);

    private static async Task<LoadedScenario> Load(string item, string yaml, string profile, string profileSlug)
    {
        var source = new InMemoryScenarioSource(($"scenarios/{item}/scenario.yaml", yaml), ($"scenarios/profiles/{profileSlug}.yaml", profile));
        return await ScenarioTestData.Loader(source).LoadAsync(item, TestContext.Current.CancellationToken);
    }
}
