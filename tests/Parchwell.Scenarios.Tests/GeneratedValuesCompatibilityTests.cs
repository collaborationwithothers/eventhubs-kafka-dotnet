using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed partial class GeneratedValuesCompatibilityTests
{
    [Fact]
    public async Task Given_MatchingGeneratedBytes_When_CheckingCompatibility_Then_AcceptsOnlyExactUtf8LfDocument()
    {
        ResolvedScenario resolved = await ResolveAsync();
        var generator = new HelmValuesGenerator();
        string generated = generator.Generate(resolved, "baseline");
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(generated));
            GeneratedValuesCompatibility.Validate(generator, resolved, "baseline", path);
            foreach (byte[] changed in new[]
            {
                Array.Empty<byte>(), Encoding.UTF8.GetBytes(generated + "\n"),
                Encoding.UTF8.GetBytes(generated.Replace("\n", "\r\n", StringComparison.Ordinal)),
                Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(generated)).ToArray(),
                Encoding.UTF8.GetBytes(generated[..^1])
            })
            {
                File.WriteAllBytes(path, changed);
                Assert.Throws<InvalidOperationException>(() => GeneratedValuesCompatibility.Validate(generator, resolved, "baseline", path));
            }
            File.Delete(path);
            Assert.Throws<FileNotFoundException>(() => GeneratedValuesCompatibility.Validate(generator, resolved, "baseline", path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Given_AnyGeneratedProfileOrKafkaLeafChanged_When_CheckingCompatibility_Then_FailsEquality()
    {
        ResolvedScenario resolved = await ResolveAsync();
        var generator = new HelmValuesGenerator();
        string generated = generator.Generate(resolved, "baseline");
        string path = Path.GetTempFileName();
        try
        {
            MatchCollection leaves = EmittedLeaf().Matches(generated);
            Assert.NotEmpty(leaves);
            Assert.Contains(leaves.Cast<Match>(), leaf => leaf.Value.Contains("client.id", StringComparison.Ordinal));
            Assert.Contains(leaves.Cast<Match>(), leaf => leaf.Value.Contains("duration_seconds", StringComparison.Ordinal));
            foreach (Match leaf in leaves)
            {
                Group value = leaf.Groups[1];
                string changed = generated[..value.Index] + "\"changed\"" + generated[(value.Index + value.Length)..];
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes(changed));
                Assert.Throws<InvalidOperationException>(() => GeneratedValuesCompatibility.Validate(generator, resolved, "baseline", path));
            }
        }
        finally { File.Delete(path); }
    }

    private static async Task<ResolvedScenario> ResolveAsync()
    {
        LoadedScenario loaded = await ScenarioTestData.Loader(ScenarioTestData.Source()).LoadAsync("A01", TestContext.Current.CancellationToken);
        using ServiceProvider provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer);
        return new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
    }

    [GeneratedRegex("^\\s*(?:-\\s*)?\\\"[^\\\"]+\\\": ([^\\r\\n]+)$", RegexOptions.Multiline)]
    private static partial Regex EmittedLeaf();
}
#pragma warning restore CA1707
