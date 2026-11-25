using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Protects scenario-contract.md, "Consumer processing declarations", for C10 and C01: the committed files, checks 1 to 8,
// the two override entries and the approved expected.yaml bytes.
[Trait("Category", "Unit")]
public sealed class KillConsumerProcessingDefinitionTests
{
    private const string C10ProfileSlug = "c03-consumer";
    private const string C01ProfileSlug = "c01-consumer";

    [Fact]
    public async Task Given_CommittedC10Files_When_Loading_Then_ExposesTheKillCountAndProfileDelay()
    {
        var loaded = await Load("C10");
        var processing = loaded.Definition.ConsumerProcessing!;
        Assert.Equal(360000, processing.WindowMs);
        Assert.Equal(30000, processing.CompletionDeadlineMs);
        Assert.Equal(110, processing.KillCounts["C0"]);
        Assert.Equal(110, processing.KillCounts["C1"]);
        Assert.Equal(2, processing.KillCounts.Count);
        Assert.Empty(processing.Configurations);
        Assert.Equal(200, loaded.Profile.Consumer!.ProcessingDelayMs);
        Assert.Equal("c03-consumer", loaded.Profile.Id);
    }

    [Fact]
    public async Task Given_CommittedC01Files_When_Loading_Then_ExposesTheKillCountAndProfileDelay()
    {
        var loaded = await Load("C01");
        var processing = loaded.Definition.ConsumerProcessing!;
        Assert.Equal(360000, processing.WindowMs);
        Assert.Equal(30000, processing.CompletionDeadlineMs);
        Assert.Equal(1, processing.KillCounts["C0"]);
        Assert.Equal(1, processing.KillCounts["C1"]);
        Assert.Equal(2, processing.KillCounts.Count);
        Assert.Empty(processing.Configurations);
        Assert.Equal(200, loaded.Profile.Consumer!.ProcessingDelayMs);
        Assert.Equal("c01-consumer", loaded.Profile.Id);
    }

    [Theory]
    [InlineData("C10", "enable.auto.offset.store", "true")]
    [InlineData("C01", "session.timeout.ms", "45000")]
    public async Task Given_CommittedC10AndC01Files_When_Resolving_Then_OnlyTheSubjectSettingOfC1DiffersFromBaseline(string item, string key, string value)
    {
        var loaded = await Load(item);
        using var services = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.EnrichmentConsumer, KafkaClientRole.VerifierConsumer);
        var factory = services.GetRequiredService<KafkaConfigurationFactory>();
        var resolved = new ScenarioResolver(factory).Resolve(loaded);
        var baseline = factory.CreateEnrichmentConsumer().Configuration.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var configuration in new[] { "C0", "C1" })
        {
            var role = resolved.Configurations[configuration].Roles[KafkaClientRole.EnrichmentConsumer];
            var expected = new Dictionary<string, string>(baseline, StringComparer.Ordinal);
            if (configuration == "C1")
            {
                expected[key] = value;
                var replaced = role.Settings[key];
                Assert.Equal(value, replaced.Value);
                Assert.Equal(configuration, replaced.Override!.ConfigurationId);
                Assert.Equal(item, replaced.Override.ScenarioId);
                Assert.Single(role.Settings, pair => pair.Value.Override is not null);
            }
            else Assert.DoesNotContain(role.Settings, pair => pair.Value.Override is not null);
            Assert.Equal(expected.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                role.Settings.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Value)).OrderBy(pair => pair.Key, StringComparer.Ordinal));
            foreach (var other in new[] { KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer })
                Assert.DoesNotContain(resolved.Configurations[configuration].Roles[other].Settings, pair => pair.Value.Override is not null);
        }
    }

    public static TheoryData<string, string, string, string, string> RejectedDeclarations() => new()
    {
        { "C10", "C10 C0 kill 109", "C0:\n      kill_count: 110", "C0:\n      kill_count: 109", "consumer_processing_literal|consumer_processing.configurations.C0.kill_count" },
        { "C10", "C10 C0 kill 111", "C0:\n      kill_count: 110", "C0:\n      kill_count: 111", "consumer_processing_literal|consumer_processing.configurations.C0.kill_count" },
        { "C10", "C10 C0 kill quoted", "C0:\n      kill_count: 110", "C0:\n      kill_count: \"110\"", "consumer_processing_literal|consumer_processing.configurations.C0.kill_count" },
        { "C01", "C01 C1 kill 0", "C1:\n      kill_count: 1", "C1:\n      kill_count: 0", "consumer_processing_literal|consumer_processing.configurations.C1.kill_count" },
        { "C01", "C01 C1 kill 2", "C1:\n      kill_count: 1", "C1:\n      kill_count: 2", "consumer_processing_literal|consumer_processing.configurations.C1.kill_count" },
        { "C10", "C10 C0 slow message", "C0:\n      kill_count: 110", "C0:\n      slow_message: {ordinal: 100, duration_ms: 90000, mode: inline}", "consumer_processing_shape|consumer_processing.configurations.C0" },
        { "C10", "C10 C0 both forms", "C0:\n      kill_count: 110", "C0:\n      kill_count: 110\n      slow_message: {ordinal: 100, duration_ms: 90000, mode: inline}", "consumer_processing_shape|consumer_processing.configurations.C0" },
        { "C10", "C10 C0 override", "  C0: {}\n", "  C0:\n    EnrichmentConsumer:\n      enable.auto.offset.store: true\n", "consumer_processing_experiment|consumer_processing" },
        { "C10", "C10 C1 no override", "  C1:\n    EnrichmentConsumer:\n      enable.auto.offset.store: true\n", "  C1: {}\n", "consumer_processing_experiment|consumer_processing" },
        { "C01", "C01 C1 session 44999", "session.timeout.ms: 45000", "session.timeout.ms: 44999", "consumer_processing_experiment|consumer_processing" },
        { "C10", "C10 repeats 6", "repeats: 5", "repeats: 6", "consumer_processing_experiment|consumer_processing" }
    };

    [Theory]
    [MemberData(nameof(RejectedDeclarations))]
    public async Task Given_ChangedDeclaration_When_Loading_Then_FailsWithTheDocumentedCodeAndPath(string item, string name, string from, string to, string expected)
    {
        Assert.NotNull(name);
        await Reject(item, Replace(Text($"scenarios/{item}/scenario.yaml"), from, to), expected);
    }

    [Fact]
    public async Task Given_C03Declaring_A_KillCount_When_Loading_Then_FailsAsShape()
    {
        var yaml = Replace(Text("scenarios/C03/scenario.yaml"), "slow_message: {ordinal: 100, duration_ms: 90000, mode: inline}", "kill_count: 110");
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load("C03", yaml, Text("scenarios/profiles/c03-consumer.yaml"), "c03-consumer"));
        Assert.Contains(error.Diagnostics, d => d.Code == "consumer_processing_shape" && d.FieldPath == "consumer_processing.configurations.C0");
    }

    [Fact]
    public async Task Given_C01WithoutTheDeclaration_When_Loading_Then_FailsAsRequired()
    {
        var yaml = Text("scenarios/C01/scenario.yaml");
        yaml = yaml[..yaml.IndexOf("consumer_processing:", StringComparison.Ordinal)];
        await Reject("C01", yaml, "consumer_processing_required|consumer_processing");
    }

    [Fact]
    public async Task Given_C10WithIdleObservation_When_Loading_Then_FailsAsCombined()
    {
        var yaml = Text("scenarios/C10/scenario.yaml") + "idle_observation:\n  action: produce\n  role: LoadGenProducer\n  idle_seconds: 420\n  statistics_interval_ms: 15000\n  max_statistics_reports: 80\n";
        await Reject("C10", yaml, "consumer_processing_combined|consumer_processing");
    }

    [Theory]
    [InlineData("session.timeout.ms: 1", "")]
    [InlineData("session.timeout.ms: 3600000", "")]
    [InlineData("session.timeout.ms: 0", "setting_out_of_range")]
    [InlineData("session.timeout.ms: 3600001", "setting_out_of_range")]
    [InlineData("session.timeout.ms: 30000", "no_op_override")]
    [InlineData("session.timeout.ms: omit", "setting_out_of_range")]
    [InlineData("session.timeout.ms: \"45000\"", "int32_required")]
    [InlineData("enable.auto.offset.store: true", "")]
    [InlineData("enable.auto.offset.store: false", "no_op_override")]
    public async Task Given_NonConsumerScenarioItemDeclaringEnrichmentConsumer_When_ReplacingTheSetting_Then_ResolvesWithinTheBounds(string setting, string code)
    {
        Assert.Equal(code, await Diagnose(Replace(DefinitionD, "session.timeout.ms: 1", setting)));
    }

    [Fact]
    public async Task Given_EnableAutoOffsetStoreUnderTheProducerRole_When_Resolving_Then_Rejects()
    {
        var yaml = Replace(DefinitionD, "    EnrichmentConsumer:\n      session.timeout.ms: 1", "    LoadGenProducer:\n      enable.auto.offset.store: true");
        Assert.Equal("unsupported_setting", await Diagnose(yaml));
    }

    [Theory]
    [InlineData("C10", "21097f98e69c3a8e2f77247c0f5ccd2d0b6bd5ef8b638e8866f2f838f364f34c")]
    [InlineData("C01", "17ced6efa4af8ebb1d8f656fe7dd62fcadc5bcd0629c256840562d8b5c122deb")] // #513 decision 7128051335: the #510 plan text plus eight appended verdict.text_must_state items.
    public void Given_ApprovedExpectedOutcomes_When_ReadingTheCommittedFile_Then_BytesEqualTheApprovedText(string item, string sha256)
    {
        // With the C03 test of ConsumerProcessingDefinitionTests, the only tests that read an expected.yaml (ADR-014).
        // Each hash is of the approved text: the #510 plan (revision 6, section 5.2), plus any verdict decision that a row's trailing comment names; LF endings and a final newline.
        var bytes = File.ReadAllBytes(Path.Combine(Root(), "scenarios", item, "expected.yaml"));
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    private const string DefinitionD = """
        schema_version: 1
        id: C02
        baseline: Parchwell.Kafka
        roles:
          - LoadGenProducer
          - EnrichmentConsumer
        profile: profiles/saturation-ramp.yaml
        configurations:
          C0:
            EnrichmentConsumer:
              session.timeout.ms: 1
        fault:
          live: {kind: none}
          component: {kind: none}
        repeats: 5
        gate: {kind: none}
        measures:
          - {id: client.fatal_error, role: LoadGenProducer}

        """;

    private static async Task<string> Diagnose(string yaml)
    {
        try
        {
            var loaded = await Load("C02", yaml, Text("scenarios/profiles/saturation-ramp.yaml"), "saturation-ramp");
            using var services = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.EnrichmentConsumer);
            new ScenarioResolver(services.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
            return "";
        }
        catch (ScenarioValidationException error)
        {
            return error.Diagnostics[0].Code;
        }
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

    private static Task<LoadedScenario> Load(string item) =>
        Load(item, Text($"scenarios/{item}/scenario.yaml"), Text(item == "C10" ? "scenarios/profiles/c03-consumer.yaml" : "scenarios/profiles/c01-consumer.yaml"),
            item == "C10" ? C10ProfileSlug : C01ProfileSlug);

    private static async Task<LoadedScenario> Load(string item, string yaml, string profile, string profileSlug)
    {
        var source = new InMemoryScenarioSource(($"scenarios/{item}/scenario.yaml", yaml), ($"scenarios/profiles/{profileSlug}.yaml", profile));
        return await ScenarioTestData.Loader(source).LoadAsync(item, TestContext.Current.CancellationToken);
    }

    private static async Task Reject(string item, string yaml, string expected)
    {
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () =>
            await Load(item, yaml, Text(item == "C10" ? "scenarios/profiles/c03-consumer.yaml" : "scenarios/profiles/c01-consumer.yaml"),
                item == "C10" ? C10ProfileSlug : C01ProfileSlug));
        var parts = expected.Split('|');
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == parts[0] && diagnostic.FieldPath == parts[1]);
    }
}
