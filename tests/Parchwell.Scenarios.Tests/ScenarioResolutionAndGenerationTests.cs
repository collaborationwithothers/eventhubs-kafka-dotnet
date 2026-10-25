using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class ScenarioResolutionAndGenerationTests
{
    // Every role's baseline must match the hand-written ApprovedConfigurationContract, and each Resolve
    // call must build fresh objects, so two run plans never share mutable state.
    [Fact]
    public async Task Given_AllFourDeclaredRoles_When_Resolving_Then_EmitsCompleteIndependentBaselineSettingsAndMetadata()
    {
        var yaml = ScenarioTestData.ValidScenario.Replace("[LoadGenProducer, VerifierConsumer]",
            "[LoadGenProducer, EnrichmentProducer, EnrichmentConsumer, VerifierConsumer]", StringComparison.Ordinal);
        var loaded = await Load(yaml);
        using var provider = ScenarioTestData.Provider(Enum.GetValues<KafkaClientRole>());
        var resolver = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>());

        var first = resolver.Resolve(loaded);
        var second = resolver.Resolve(loaded);
        var firstRoles = first.Configurations["baseline"].Roles;
        var secondRoles = second.Configurations["baseline"].Roles;

        Assert.Equal(Enum.GetValues<KafkaClientRole>(), firstRoles.Keys.Order());
        foreach (var role in Enum.GetValues<KafkaClientRole>())
        {
            var expected = ApprovedConfigurationContract.Settings(role);
            var actual = firstRoles[role];
            Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Settings.Keys.Order(StringComparer.Ordinal));
            foreach (var (key, expectedSetting) in expected)
            {
                var setting = actual.Settings[key];
                Assert.Equal(expectedSetting.Value, setting.Value);
                Assert.Null(setting.Override);
                Assert.Equal(role, setting.Baseline.Role);
                Assert.Equal(expectedSetting.Value, setting.Baseline.Value);
                Assert.Equal(KafkaSettingDisposition.Explicit, setting.Baseline.Disposition);
                Assert.NotEmpty(setting.Baseline.Sources);
            }

            Assert.NotSame(actual.Settings["client.id"], secondRoles[role].Settings["client.id"]);
            Assert.NotSame(actual.Settings["client.id"].Baseline, secondRoles[role].Settings["client.id"].Baseline);
            Assert.Equal(actual.Settings["client.id"].Baseline.Value, secondRoles[role].Settings["client.id"].Baseline.Value);
            Assert.Equal(actual.Settings["client.id"].Baseline.InventoryIds, secondRoles[role].Settings["client.id"].Baseline.InventoryIds);
            Assert.Equal(actual.Settings["client.id"].Baseline.Sources, secondRoles[role].Settings["client.id"].Baseline.Sources);
            Assert.Equal(actual.Omissions.Select(item => item.Key).Order(StringComparer.Ordinal),
                secondRoles[role].Omissions.Select(item => item.Key).Order(StringComparer.Ordinal));
        }
    }

    // An override keeps the baseline metadata as first emitted and records the scenario, configuration
    // and YAML position that changed it (scenario-contract.md "Composition and output").
    [Fact]
    public async Task Given_LoadGeneratorTimeoutOverrides_When_Resolving_Then_PreservesInitialMetadataAndRecordsSourceProvenance()
    {
        var yaml = ScenarioWithLoadGenOverrides("request.timeout.ms: 60001\n      socket.timeout.ms: 300000", "changed");
        var loaded = await Load(yaml);
        using var provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer);
        var resolved = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
        var baseline = resolved.Configurations["baseline"].Roles[KafkaClientRole.LoadGenProducer];
        var changed = resolved.Configurations["changed"].Roles[KafkaClientRole.LoadGenProducer];
        var timeout = changed.Settings["request.timeout.ms"];

        Assert.Equal("60000", baseline.Settings["request.timeout.ms"].Value);
        Assert.Equal("60001", timeout.Value);
        Assert.Equal("60000", timeout.Baseline.Value);
        Assert.Equal(KafkaSettingDisposition.Explicit, timeout.Baseline.Disposition);
        Assert.Equal("A01", timeout.Override?.ScenarioId);
        Assert.Equal("changed", timeout.Override?.ConfigurationId);
        Assert.Equal(ScenarioTestData.ScenarioPath, timeout.Override?.Location.Path);
        Assert.Equal(10, timeout.Override?.Location.Line);
        Assert.Equal(27, timeout.Override?.Location.Column);
        Assert.Equal("300000", changed.Settings["socket.timeout.ms"].Value);
    }

    // The bounds are librdkafka's native ranges at the pinned v2.16.0, cited in scenario-contract.md
    // "Composition and output". Both ends of each range must be accepted.
    [Theory]
    [InlineData("request.timeout.ms", "1")]
    [InlineData("request.timeout.ms", "900000")]
    [InlineData("socket.timeout.ms", "10")]
    [InlineData("socket.timeout.ms", "300000")]
    public async Task Given_OverrideAtPinnedNativeBoundary_When_Resolving_Then_AcceptsValue(string key, string value)
    {
        var yaml = ScenarioWithLoadGenOverrides($"{key}: {value}", "changed");
        using var provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer);

        var resolved = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(await Load(yaml));

        Assert.Equal(value, resolved.Configurations["changed"].Roles[KafkaClientRole.LoadGenProducer].Settings[key].Value);
    }

    // Only LoadGenProducer request.timeout.ms and socket.timeout.ms may be overridden for now. One past
    // each bound, a value equal to the baseline, a misspelt key and an unsupported key are rejected.
    [Theory]
    [InlineData("request.timeout.ms", "0")]
    [InlineData("request.timeout.ms", "900001")]
    [InlineData("socket.timeout.ms", "9")]
    [InlineData("socket.timeout.ms", "300001")]
    [InlineData("request.timeout.ms", "60000")]
    [InlineData("request.timeout", "10")]
    [InlineData("transactional.id", "1")]
    public async Task Given_UnsupportedOutOfRangeOrNoOpOverride_When_Resolving_Then_Rejects(string key, string value)
    {
        var yaml = ScenarioWithLoadGenOverrides($"{key}: {value}", "changed");
        var loaded = await Load(yaml);
        using var provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer);

        var exception = Assert.Throws<ScenarioValidationException>(() =>
            new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded));

        Assert.Contains(exception.Diagnostics, item => item.FieldPath.Contains(key, StringComparison.Ordinal));
    }

    // Replacements must be plain decimal Int32 (scenario-contract.md); other forms fail at load,
    // before the resolver sees them.
    [Theory]
    [InlineData("request.timeout.ms: \"60001\"")]
    [InlineData("request.timeout.ms: true")]
    [InlineData("request.timeout.ms: 60001.0")]
    [InlineData("request.timeout.ms: +60001")]
    [InlineData("request.timeout.ms: 2147483648")]
    public async Task Given_QuotedBooleanFractionSignedOrOverflowingReplacement_When_Loading_Then_RejectsNonPlainInt32(string entry)
    {
        var yaml = ScenarioWithLoadGenOverrides(entry, "changed");

        var exception = await Assert.ThrowsAsync<ScenarioValidationException>(async () => await Load(yaml));

        Assert.Contains(exception.Diagnostics, item => item.FieldPath.Contains("request.timeout.ms", StringComparison.Ordinal));
    }

    // Which roles may be overridden is repository policy (KafkaScenarioOverrideSetting.SupportedRoles),
    // separate from the native ranges.
    [Fact]
    public async Task Given_OverrideForAnotherRole_When_Resolving_Then_RejectsRoleScope()
    {
        var yaml = ScenarioWithRoleOverride(KafkaClientRole.EnrichmentProducer, "request.timeout.ms: 60001");
        var loaded = await Load(yaml);
        using var provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.EnrichmentProducer);

        var exception = Assert.Throws<ScenarioValidationException>(() =>
            new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded));

        Assert.Contains(exception.Diagnostics, item => item.Code == "wrong_role_setting");
    }

    private static async Task<LoadedScenario> Load(string? scenario = null) =>
        await ScenarioTestData.Loader(ScenarioTestData.Source(scenario ?? ScenarioTestData.ValidScenario))
            .LoadAsync("A01", TestContext.Current.CancellationToken);

    private static string ScenarioWithLoadGenOverrides(string entries, string configurationId) =>
        ScenarioWithRoleOverride(KafkaClientRole.LoadGenProducer, entries, configurationId);

    private static string ScenarioWithRoleOverride(KafkaClientRole role, string entries, string configurationId = "changed")
    {
        var roleName = role.ToString();
        var scenario = ScenarioForRole(role);
        scenario = scenario.Replace("configurations:\n  baseline: {}", $"configurations:\n  baseline: {{}}\n  {configurationId}:\n    {roleName}:\n      {entries}", StringComparison.Ordinal);
        return scenario;
    }

    private static string ScenarioForRole(KafkaClientRole role)
    {
        var roleName = role.ToString();
        var measure = role is KafkaClientRole.LoadGenProducer or KafkaClientRole.EnrichmentProducer
            ? $"measures:\n  - {{id: producer.request_timeout, role: {roleName}}}"
            : "measures:\n  - {id: verifier.ok, role: VerifierConsumer}";
        return ScenarioTestData.ValidScenario
            .Replace("[LoadGenProducer, VerifierConsumer]", $"[{roleName}]", StringComparison.Ordinal)
            .Replace("measures:\n  - {id: verifier.ok, role: VerifierConsumer}", measure, StringComparison.Ordinal);
    }
}
