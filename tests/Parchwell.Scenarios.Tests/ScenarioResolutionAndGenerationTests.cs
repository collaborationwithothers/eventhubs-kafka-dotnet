using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.Scenarios.Tests;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class ScenarioResolutionAndGenerationTests
{
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

    [Fact]
    public async Task Given_OverrideForAnotherRole_When_Resolving_Then_RejectsRoleScope()
    {
        var yaml = ScenarioWithRoleOverride(KafkaClientRole.EnrichmentProducer, "request.timeout.ms: 60001");
        var loaded = await Load(yaml);
        using var provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer, KafkaClientRole.EnrichmentProducer);

        var exception = Assert.Throws<ScenarioValidationException>(() =>
            new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded));

        Assert.Contains(exception.Diagnostics, item => item.Code == "wrong-role-setting");
    }

    [Fact]
    public async Task Given_ReplacementKeyWithDifferentMappingOrder_When_Generating_Then_ProducesSameValues()
    {
        var firstYaml = ScenarioWithLoadGenOverrides("request.timeout.ms: 60001\n      socket.timeout.ms: 60001", "changed");
        var secondYaml = ScenarioWithLoadGenOverrides("socket.timeout.ms: 60001\n      request.timeout.ms: 60001", "changed");
        using var provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer);
        var generator = new HelmValuesGenerator();
        var resolver = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>());

        var first = generator.Generate(resolver.Resolve(await Load(firstYaml)), "changed");
        var second = generator.Generate(resolver.Resolve(await Load(secondYaml)), "changed");

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Given_SelectedBaselineConfiguration_When_Generating_Then_ReturnsCompleteInvariantYamlWithoutOrchestratorFields()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            using var provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer);
            var resolved = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(await Load(ScenarioForRole(KafkaClientRole.LoadGenProducer)));
            var generator = new HelmValuesGenerator();

            var actual = generator.Generate(resolved, "baseline");

            Assert.Equal(ExpectedLoadGenBaselineYaml, actual);
            Assert.Equal(actual, generator.Generate(resolved, "baseline"));
            Assert.EndsWith("\n", actual, StringComparison.Ordinal);
            Assert.DoesNotContain("expected.yaml", actual, StringComparison.Ordinal);
            Assert.DoesNotContain("fault:", actual, StringComparison.Ordinal);
            Assert.DoesNotContain("measures:", actual, StringComparison.Ordinal);
            Assert.DoesNotContain("gate:", actual, StringComparison.Ordinal);
            Assert.DoesNotContain("repeats:", actual, StringComparison.Ordinal);
            Assert.DoesNotContain("throttle", actual, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public async Task Given_UnknownConfigurationId_When_Generating_Then_ThrowsArgumentException()
    {
        using var provider = ScenarioTestData.Provider(KafkaClientRole.LoadGenProducer);
        var resolved = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(await Load(ScenarioForRole(KafkaClientRole.LoadGenProducer)));

        Assert.Throws<ArgumentException>(() => new HelmValuesGenerator().Generate(resolved, "absent"));
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

    private const string ExpectedLoadGenBaselineYaml = """
        "contract_version": 1
        "scenario_id": "A01"
        "configuration_id": "baseline"
        "profile":
          "documents_per_burst": 2
          "id": "synthetic"
          "payloads":
            "classified":
            - "size_bytes": 80
              "weight": 1
            "indexed":
            - "size_bytes": 40
              "weight": 1
            "ocr-finished":
            - "size_bytes": 96
              "weight": 1
            "ocr-started":
            - "size_bytes": 64
              "weight": 1
            "uploaded":
            - "size_bytes": 128
              "weight": 1
            - "size_bytes": 512
              "weight": 2
          "schema_version": 1
          "seed": -17
          "stages":
          - "duration_seconds": 3
            "id": "warmup"
            "offered_attempts_per_second": 2
          - "duration_seconds": 4
            "id": "measure"
            "offered_attempts_per_second": 5
          "tenants":
          - "id": "tenant-a"
            "weight": 3
          - "id": "tenant-b"
            "weight": 1
        "kafka":
          "LoadGenProducer":
            "acks": "-1"
            "allow.auto.create.topics": "true"
            "batch.size": "1000000"
            "bootstrap.servers": "synthetic-namespace.servicebus.windows.net:9093"
            "broker.address.ttl": "1000"
            "client.id": "synthetic-client"
            "compression.type": "none"
            "connections.max.idle.ms": "0"
            "dotnet.producer.delivery.report.fields": "all"
            "dotnet.producer.enable.background.poll": "true"
            "dotnet.producer.enable.delivery.reports": "true"
            "enable.idempotence": "false"
            "enable.sasl.oauthbearer.unsecure.jwt": "false"
            "enable.ssl.certificate.verification": "true"
            "linger.ms": "5"
            "max.in.flight": "1000000"
            "message.max.bytes": "1000000"
            "message.send.max.retries": "2147483647"
            "message.timeout.ms": "300000"
            "metadata.max.age.ms": "180000"
            "partitioner": "consistent_random"
            "queue.buffering.max.kbytes": "1048576"
            "queue.buffering.max.messages": "100000"
            "request.timeout.ms": "60000"
            "sasl.mechanism": "OAUTHBEARER"
            "sasl.oauthbearer.metadata.authentication.type": "none"
            "sasl.oauthbearer.method": "default"
            "security.protocol": "sasl_ssl"
            "socket.keepalive.enable": "true"
            "socket.max.fails": "1"
            "socket.timeout.ms": "60000"
            "ssl.endpoint.identification.algorithm": "https"
            "statistics.interval.ms": "15000"
        """ + "\n";
}
