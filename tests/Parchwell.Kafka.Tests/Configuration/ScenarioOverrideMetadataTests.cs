using System.Collections.Immutable;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Configuration;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class ScenarioOverrideMetadataTests
{
    [Fact]
    public void Given_OverridePolicy_When_ReadingSettings_Then_ExposesEachSupportedEntry()
    {
        var settings = KafkaScenarioOverridePolicy.Settings;

        Assert.Equal(2, settings.Length);
        Assert.Collection(settings,
            setting => AssertPolicyEntry(setting, "request.timeout.ms", 1, 900_000),
            setting => AssertPolicyEntry(setting, "socket.timeout.ms", 10, 300_000));
    }

    [Theory]
    [InlineData("request.timeout.ms", 1, 900_000)]
    [InlineData("socket.timeout.ms", 10, 300_000)]
    public void Given_OverrideKey_When_LookingUpPolicy_Then_ExposesPinnedNativeBoundsAndCitation(
        string key, int minimum, int maximum)
    {
        Assert.True(KafkaScenarioOverridePolicy.TryGetSetting(key, out var setting));
        Assert.NotNull(setting);
        Assert.Equal(key, setting.Key);
        Assert.Equal(minimum, setting.Minimum);
        Assert.Equal(maximum, setting.Maximum);
        var source = Assert.Single(setting.Sources);
        Assert.Equal("https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md", source.Url.AbsoluteUri);
        Assert.Equal("v2.16.0", source.VersionOrDate);
        Assert.Contains("A01", setting.InventoryIds);
    }

    [Theory]
    [InlineData("request.timeout.ms")]
    [InlineData("socket.timeout.ms")]
    public void Given_OverrideKey_When_CheckingRolePolicy_Then_AllowsOnlyLoadGenProducer(string key)
    {
        Assert.True(KafkaScenarioOverridePolicy.TryGetSetting(key, out var setting));
        Assert.NotNull(setting);
        Assert.True(setting.SupportsRole(KafkaClientRole.LoadGenProducer));
        Assert.False(setting.SupportsRole(KafkaClientRole.EnrichmentProducer));
        Assert.False(setting.SupportsRole(KafkaClientRole.EnrichmentConsumer));
        Assert.False(setting.SupportsRole(KafkaClientRole.VerifierConsumer));
        Assert.Equal(KafkaClientRole.LoadGenProducer, Assert.Single(setting.SupportedRoles));
    }

    [Fact]
    public void Given_OverridePolicy_When_CheckingSupportedRoles_Then_ExposesOnlyLoadGenProducer()
    {
        Assert.True(KafkaScenarioOverridePolicy.SupportsRole(KafkaClientRole.LoadGenProducer));
        Assert.False(KafkaScenarioOverridePolicy.SupportsRole(KafkaClientRole.EnrichmentProducer));
        Assert.False(KafkaScenarioOverridePolicy.SupportsRole(KafkaClientRole.EnrichmentConsumer));
        Assert.False(KafkaScenarioOverridePolicy.SupportsRole(KafkaClientRole.VerifierConsumer));
        Assert.False(KafkaScenarioOverridePolicy.SupportsRole((KafkaClientRole)int.MaxValue));
    }

    [Theory]
    [InlineData("unknown.key")]
    [InlineData("Request.timeout.ms")]
    [InlineData("request.timeout")]
    public void Given_UnknownOrCaseMismatchedKey_When_LookingUpPolicy_Then_ReturnsUnsupported(string key)
    {
        Assert.False(KafkaScenarioOverridePolicy.TryGetSetting(key, out var setting));
        Assert.Null(setting);
    }

    [Fact]
    public void Given_NullKey_When_LookingUpPolicy_Then_ReturnsUnsupported()
    {
        Assert.False(KafkaScenarioOverridePolicy.TryGetSetting(null, out var setting));
        Assert.Null(setting);
    }

    [Fact]
    public void Given_InventoryLookup_When_IdIsNullUnknownOrWrongCase_Then_ReturnsFalse()
    {
        Assert.False(FrozenInventory.Contains(null));
        Assert.False(FrozenInventory.Contains("Z99"));
        Assert.False(FrozenInventory.Contains("a01"));
    }

    [Fact]
    public void Given_InventoryLookup_When_IdIsKnown_Then_ReturnsTrue() =>
        Assert.True(FrozenInventory.Contains("A01"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_BlankKey_When_CreatingOverrideMetadata_Then_Throws(string? key)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => CreateSetting(key: key!));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void Given_MinimumAboveMaximum_When_CreatingOverrideMetadata_Then_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateSetting(minimum: 2, maximum: 1));

        Assert.Equal("minimum", exception.ParamName);
    }

    [Fact]
    public void Given_NoSupportedRoles_When_CreatingOverrideMetadata_Then_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateSetting(roles: []));

        Assert.Equal("supportedRoles", exception.ParamName);
    }

    [Fact]
    public void Given_UndefinedSupportedRole_When_CreatingOverrideMetadata_Then_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateSetting(roles: [(KafkaClientRole)int.MaxValue]));

        Assert.Equal("supportedRoles", exception.ParamName);
    }

    [Fact]
    public void Given_NoInventoryIds_When_CreatingOverrideMetadata_Then_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateSetting(inventoryIds: []));

        Assert.Equal("inventoryIds", exception.ParamName);
    }

    [Fact]
    public void Given_UnknownInventoryId_When_CreatingOverrideMetadata_Then_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateSetting(inventoryIds: ["Z99"]));

        Assert.Equal("inventoryIds", exception.ParamName);
    }

    [Fact]
    public void Given_NoSources_When_CreatingOverrideMetadata_Then_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateSetting(sources: []));

        Assert.Equal("sources", exception.ParamName);
    }

    [Fact]
    public void Given_NullSource_When_CreatingOverrideMetadata_Then_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateSetting(sources: [null!]));

        Assert.Equal("sources", exception.ParamName);
    }

    private static void AssertPolicyEntry(KafkaScenarioOverrideSetting setting, string key,
        int minimum, int maximum)
    {
        Assert.Equal(key, setting.Key);
        Assert.Equal(minimum, setting.Minimum);
        Assert.Equal(maximum, setting.Maximum);
        Assert.Equal([KafkaClientRole.LoadGenProducer], setting.SupportedRoles);
        Assert.Equal(["A01"], setting.InventoryIds);
        Assert.Single(setting.Sources);
    }

    private static KafkaScenarioOverrideSetting CreateSetting(string key = "request.timeout.ms",
        int minimum = 1, int maximum = 900_000,
        ImmutableArray<KafkaClientRole> roles = default,
        ImmutableArray<string> inventoryIds = default,
        ImmutableArray<KafkaSettingSource> sources = default) =>
        new(key, minimum, maximum,
            roles.IsDefault ? [KafkaClientRole.LoadGenProducer] : roles,
            inventoryIds.IsDefault ? ["A01"] : inventoryIds,
            sources.IsDefault
                ? [new KafkaSettingSource(new Uri("https://example.com/configuration"), "v1")]
                : sources);
}
