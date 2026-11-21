using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Protects scenario-contract.md, "Consumer processing declarations": the committed C03 files, checks 1 to 11, the C03 override entry and the approved expected.yaml bytes.
[Trait("Category", "Unit")]
public sealed class ConsumerProcessingDefinitionTests
{
    private const string Slow0 = "slow_message: {ordinal: 100, duration_ms: 90000, mode: inline}";
    private const string Slow1 = "slow_message: {ordinal: 100, duration_ms: 90000, mode: pause}";
    private const string PollOverrideC1 = "  C1:\n    EnrichmentConsumer:\n      max.poll.interval.ms: 60000\n";

    [Fact]
    public async Task Given_CommittedC03Files_When_Loading_Then_ExposesTheFixedDeclarationAndProfileDelay()
    {
        var loaded = await Load("C03", Text("scenarios/C03/scenario.yaml"), Text("scenarios/profiles/c03-consumer.yaml"));
        var processing = loaded.Definition.ConsumerProcessing!;
        Assert.Equal(360000, processing.WindowMs);
        Assert.Equal(30000, processing.CompletionDeadlineMs);
        Assert.Equal(new ConsumerSlowMessageDeclaration(100, 90000, "inline"), processing.Configurations["C0"]);
        Assert.Equal(new ConsumerSlowMessageDeclaration(100, 90000, "pause"), processing.Configurations["C1"]);
        Assert.Equal(2, processing.Configurations.Count);
        Assert.Equal(200, loaded.Profile.Consumer!.ProcessingDelayMs);
        Assert.Equal(["tenant06"], loaded.Profile.Tenants.Select(tenant => tenant.Id));
        Assert.Null(loaded.Definition.SingleAction);
        Assert.Null(loaded.Definition.ProbeV2);
        Assert.Null(loaded.Definition.IdleObservation);
    }

    [Fact]
    public async Task Given_CommittedC03Files_When_Resolving_Then_OnlyTheSubjectPollLimitDiffersFromBaseline()
    {
        var loaded = await Load("C03", Text("scenarios/C03/scenario.yaml"), Text("scenarios/profiles/c03-consumer.yaml"));
        using var services = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.EnrichmentConsumer, KafkaClientRole.VerifierConsumer);
        var factory = services.GetRequiredService<KafkaConfigurationFactory>();
        var resolved = new ScenarioResolver(factory).Resolve(loaded);
        var baseline = factory.CreateEnrichmentConsumer().Configuration.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var configuration in new[] { "C0", "C1" })
        {
            var role = resolved.Configurations[configuration].Roles[KafkaClientRole.EnrichmentConsumer];
            var poll = role.Settings["max.poll.interval.ms"];
            Assert.Equal("60000", poll.Value);
            Assert.Equal(configuration, poll.Override!.ConfigurationId);
            Assert.Equal("C03", poll.Override.ScenarioId);
            baseline["max.poll.interval.ms"] = "60000";
            Assert.Equal(baseline.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                role.Settings.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Value)).OrderBy(pair => pair.Key, StringComparer.Ordinal));
            Assert.Single(role.Settings, pair => pair.Value.Override is not null);
            foreach (var other in new[] { KafkaClientRole.LoadGenProducer, KafkaClientRole.VerifierConsumer })
                Assert.DoesNotContain(resolved.Configurations[configuration].Roles[other].Settings, pair => pair.Value.Override is not null);
        }
    }

    public static TheoryData<string, string, string, string> RejectedDefinitions() => new()
    {
        { "extra key", "consumer_processing:\n", "consumer_processing:\n  extra: 1\n", "consumer_processing_shape|consumer_processing" },
        { "window 359999", "window_ms: 360000", "window_ms: 359999", "consumer_processing_literal|consumer_processing.window_ms" },
        { "window 360001", "window_ms: 360000", "window_ms: 360001", "consumer_processing_literal|consumer_processing.window_ms" },
        { "window quoted", "window_ms: 360000", "window_ms: \"360000\"", "consumer_processing_literal|consumer_processing.window_ms" },
        { "deadline 29999", "completion_deadline_ms: 30000", "completion_deadline_ms: 29999", "consumer_processing_literal|consumer_processing.completion_deadline_ms" },
        { "deadline 30001", "completion_deadline_ms: 30000", "completion_deadline_ms: 30001", "consumer_processing_literal|consumer_processing.completion_deadline_ms" },
        { "C1 missing", "    C1:\n      " + Slow1 + "\n", "", "consumer_processing_configurations|consumer_processing.configurations" },
        { "mode missing", "duration_ms: 90000, mode: inline}", "duration_ms: 90000}", "consumer_processing_shape|consumer_processing.configurations.C0" },
        { "ordinal 99", Slow0, "slow_message: {ordinal: 99, duration_ms: 90000, mode: inline}", "consumer_processing_literal|consumer_processing.configurations.C0.slow_message.ordinal" },
        { "ordinal 101", Slow0, "slow_message: {ordinal: 101, duration_ms: 90000, mode: inline}", "consumer_processing_literal|consumer_processing.configurations.C0.slow_message.ordinal" },
        { "duration 89999", Slow0, "slow_message: {ordinal: 100, duration_ms: 89999, mode: inline}", "consumer_processing_literal|consumer_processing.configurations.C0.slow_message.duration_ms" },
        { "duration 90001", Slow0, "slow_message: {ordinal: 100, duration_ms: 90001, mode: inline}", "consumer_processing_literal|consumer_processing.configurations.C0.slow_message.duration_ms" },
        { "C0 mode pause", Slow0, "slow_message: {ordinal: 100, duration_ms: 90000, mode: pause}", "consumer_processing_literal|consumer_processing.configurations.C0.slow_message.mode" },
        { "repeats 6", "repeats: 5", "repeats: 6", "consumer_processing_experiment|consumer_processing" },
        { "no verifier role", "  - VerifierConsumer\n", "", "consumer_processing_experiment|consumer_processing" },
        { "C1 poll limit 59999", PollOverrideC1, PollOverrideC1.Replace("60000", "59999", StringComparison.Ordinal), "consumer_processing_experiment|consumer_processing" }
    };

    [Theory]
    [MemberData(nameof(RejectedDefinitions))]
    public async Task Given_ChangedC03Declaration_When_Loading_Then_FailsWithTheDocumentedCodeAndPath(string name, string from, string to, string expected)
    {
        Assert.NotNull(name);
        var yaml = Replace(Text("scenarios/C03/scenario.yaml"), from, to);
        await Reject("C03", yaml, Text("scenarios/profiles/c03-consumer.yaml"), expected);
    }

    [Fact]
    public async Task Given_ConsumerProcessingOnAnotherItem_When_Loading_Then_FailsAsUnexpected()
    {
        var yaml = Text("scenarios/B04/scenario.yaml") + "consumer_processing:\n  window_ms: 360000\n";
        await Reject("B04", yaml, Text("scenarios/profiles/saturation-ramp.yaml"), "consumer_processing_unexpected|consumer_processing");
    }

    [Fact]
    public async Task Given_C03WithoutTheDeclaration_When_Loading_Then_FailsAsRequired()
    {
        var yaml = Text("scenarios/C03/scenario.yaml");
        yaml = yaml[..yaml.IndexOf("consumer_processing:", StringComparison.Ordinal)];
        await Reject("C03", yaml, Text("scenarios/profiles/c03-consumer.yaml"), "consumer_processing_required|consumer_processing");
    }

    [Fact]
    public async Task Given_C03WithIdleObservation_When_Loading_Then_FailsAsCombined()
    {
        var yaml = Text("scenarios/C03/scenario.yaml") + "idle_observation:\n  action: produce\n  role: LoadGenProducer\n  idle_seconds: 420\n  statistics_interval_ms: 15000\n  max_statistics_reports: 80\n";
        await Reject("C03", yaml, Text("scenarios/profiles/c03-consumer.yaml"), "consumer_processing_combined|consumer_processing");
    }

    [Theory]
    [InlineData("processing_delay_ms: 0", true, "")]
    [InlineData("processing_delay_ms: 60000", true, "")]
    [InlineData("processing_delay_ms: -1", false, "profile_consumer_range|consumer.processing_delay_ms")]
    [InlineData("processing_delay_ms: 60001", false, "profile_consumer_range|consumer.processing_delay_ms")]
    [InlineData("processing_delay_ms: \"200\"", false, "profile_consumer_range|consumer.processing_delay_ms")]
    [InlineData("{}", false, "profile_consumer_shape|consumer")]
    [InlineData("processing_delay_ms: 200\n  x: 1", false, "profile_consumer_shape|consumer")]
    public async Task Given_ConsumerProfileSection_When_Loading_Then_AcceptsOrFailsAtTheRangeBoundaries(string section, bool accepted, string expected)
    {
        var profile = Text("scenarios/profiles/c03-consumer.yaml");
        profile = section == "{}" ? Replace(profile, "consumer:\n  processing_delay_ms: 200\n", "consumer: {}\n") :
            Replace(profile, "processing_delay_ms: 200", section);
        if (accepted) Assert.NotNull((await Load("C03", Text("scenarios/C03/scenario.yaml"), profile)).Profile.Consumer);
        else await Reject("C03", Text("scenarios/C03/scenario.yaml"), profile, expected);
    }

    [Fact]
    public async Task Given_C03PointingAtAProfileWithoutConsumer_When_Loading_Then_FailsWithTheProfileSource()
    {
        var yaml = Replace(Text("scenarios/C03/scenario.yaml"), "profiles/c03-consumer.yaml", "profiles/saturation-ramp.yaml");
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () =>
            await Load("C03", yaml, Text("scenarios/profiles/saturation-ramp.yaml"), "saturation-ramp"));
        var diagnostic = Assert.Single(error.Diagnostics, d => d.Code == "consumer_profile_required");
        Assert.Equal("scenarios/profiles/saturation-ramp.yaml", diagnostic.Source);
        Assert.Equal("consumer", diagnostic.FieldPath);
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
              max.poll.interval.ms: 1
        fault:
          live: {kind: none}
          component: {kind: none}
        repeats: 5
        gate: {kind: none}
        measures:
          - {id: client.fatal_error, role: LoadGenProducer}

        """;

    [Theory]
    [InlineData("max.poll.interval.ms: 1", "")]
    [InlineData("max.poll.interval.ms: 86400000", "")]
    [InlineData("max.poll.interval.ms: 0", "setting_out_of_range")]
    [InlineData("max.poll.interval.ms: 86400001", "setting_out_of_range")]
    [InlineData("max.poll.interval.ms: \"60000\"", "int32_required")]
    public async Task Given_NonC03ItemDeclaringEnrichmentConsumer_When_ReplacingThePollLimit_Then_ResolvesWithinTheBounds(string setting, string code)
    {
        var yaml = Replace(DefinitionD, "max.poll.interval.ms: 1", setting);
        Assert.Equal(code, await Diagnose(yaml));
    }

    [Fact]
    public async Task Given_NonC03ItemDeclaringEnrichmentConsumer_When_PlacingThePollLimitUnderAnotherRole_Then_Rejects()
    {
        var underProducer = Replace(DefinitionD, "    EnrichmentConsumer:\n      max.poll.interval.ms: 1", "    LoadGenProducer:\n      max.poll.interval.ms: 1");
        Assert.Equal("unsupported_setting", await Diagnose(underProducer));
        var undeclared = Replace(DefinitionD, "  - EnrichmentConsumer\n", "");
        Assert.Equal("role_not_declared", await Diagnose(undeclared));
    }

    [Fact]
    public void Given_ApprovedExpectedOutcomes_When_ReadingTheCommittedFile_Then_BytesEqualTheApprovedText()
    {
        // With tests X1 and X2 of the #510 plan, the only tests that read an expected.yaml (ADR-014). The hash is of the text in the approved plan, LF endings and a final newline.
        var bytes = File.ReadAllBytes(Path.Combine(Root(), "scenarios", "C03", "expected.yaml"));
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.Equal("71ffd90fa84d837bfe173244341388bca0cd0cb2f86bfc963b4f5aaad860b68f", Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

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

    private static async Task<LoadedScenario> Load(string item, string yaml, string profile, string profileSlug = "c03-consumer")
    {
        var source = new InMemoryScenarioSource(($"scenarios/{item}/scenario.yaml", yaml), ($"scenarios/profiles/{profileSlug}.yaml", profile));
        return await ScenarioTestData.Loader(source).LoadAsync(item, TestContext.Current.CancellationToken);
    }

    private static async Task Reject(string item, string yaml, string profile, string expected)
    {
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(async () =>
            await Load(item, yaml, profile, item == "B04" ? "saturation-ramp" : "c03-consumer"));
        var parts = expected.Split('|');
        Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == parts[0] && diagnostic.FieldPath == parts[1]);
    }
}
