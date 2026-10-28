using System.Collections.Immutable;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Configuration;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Protects the scenario override policy: exact keys, value policies, approved roles, inventory IDs and pinned sources.
[Trait("Category", "Unit")]
public sealed class ScenarioOverrideMetadataTests
{
    [Fact]
    public void Given_OverridePolicy_When_ReadingSettings_Then_ExposesEachSupportedEntry()
    {
        var settings = KafkaScenarioOverridePolicy.Settings;

        Assert.Equal(14, settings.Length);
        Assert.Collection(settings,
            setting => { AssertPolicyEntry(setting, "request.timeout.ms", 1, 900_000, "A01"); Assert.Equal(KafkaScenarioOverrideValueKind.Integer, setting.ValueKind); },
            setting => { AssertPolicyEntry(setting, "socket.timeout.ms", 10, 300_000, "A01"); Assert.Equal(KafkaScenarioOverrideValueKind.Integer, setting.ValueKind); },
            setting => { AssertPolicyEntry(setting, "enable.idempotence", 0, 1, "B04", "B05"); Assert.Equal(KafkaScenarioOverrideValueKind.Boolean, setting.ValueKind); },
            setting => { AssertPolicyEntry(setting, "max.in.flight", 1, 1_000_000, "B04", "B05"); Assert.Equal(KafkaScenarioOverrideValueKind.Integer, setting.ValueKind); },
            setting => AssertNewEntry(setting, "message.max.bytes", KafkaScenarioOverrideValueKind.Integer, KafkaClientRole.LoadGenProducer, "B01", 1000, 1000000000),
            setting => AssertNewEntry(setting, "message.timeout.ms", KafkaScenarioOverrideValueKind.Integer, KafkaClientRole.LoadGenProducer, "B05", 0, int.MaxValue),
            setting => AssertNewEntry(setting, "compression.type", KafkaScenarioOverrideValueKind.String, KafkaClientRole.LoadGenProducer, "B03", 0, 0, ["none", "gzip", "snappy", "lz4", "zstd"]),
            setting => AssertNewEntry(setting, "transactional.id", KafkaScenarioOverrideValueKind.String, KafkaClientRole.LoadGenProducer, "B05", 0, 0, [], true, true),
            setting => AssertNewEntry(setting, "group.protocol", KafkaScenarioOverrideValueKind.String, KafkaClientRole.EnrichmentConsumer, "C08", 0, 0, ["classic", "consumer"]),
            setting => AssertNewEntry(setting, "group.id", KafkaScenarioOverrideValueKind.String, KafkaClientRole.EnrichmentConsumer, "C12", 0, 0, [], true),
            setting => AssertNewEntry(setting, "session.timeout.ms", KafkaScenarioOverrideValueKind.Omission, KafkaClientRole.EnrichmentConsumer, "C08", 0, 0),
            setting => AssertNewEntry(setting, "heartbeat.interval.ms", KafkaScenarioOverrideValueKind.Omission, KafkaClientRole.EnrichmentConsumer, "C08", 0, 0),
            setting => AssertNewEntry(setting, "partition.assignment.strategy", KafkaScenarioOverrideValueKind.Omission, KafkaClientRole.EnrichmentConsumer, "C08", 0, 0),
            setting =>
            {
                Assert.Equal("debug", setting.Key);
                Assert.Equal(KafkaScenarioOverrideValueKind.String, setting.ValueKind);
                Assert.Equal(0, setting.Minimum);
                Assert.Equal(0, setting.Maximum);
                Assert.Equal([KafkaClientRole.EnrichmentConsumer, KafkaClientRole.VerifierConsumer], setting.SupportedRoles);
                Assert.Equal(["C08", "F04"], setting.InventoryIds);
                Assert.Equal(["cgrp", "topic,fetch,metadata"], setting.AllowedValues);
                Assert.False(setting.AllowAnyNonemptyValue);
                Assert.True(setting.AllowActivation);
                var source = Assert.Single(setting.Sources);
                Assert.Equal("https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md", source.Url.AbsoluteUri);
                Assert.Equal("v2.16.0", source.VersionOrDate);
            });
    }

    [Theory]
    [InlineData("request.timeout.ms", 1, 900_000, "A01")]
    [InlineData("socket.timeout.ms", 10, 300_000, "A01")]
    [InlineData("max.in.flight", 1, 1_000_000, "B04")]
    [InlineData("enable.idempotence", 0, 1, "B04")]
    public void Given_OverrideKey_When_LookingUpPolicy_Then_ExposesPinnedNativeBoundsAndCitation(
        string key, int minimum, int maximum, string inventoryId)
    {
        Assert.True(KafkaScenarioOverridePolicy.TryGetSetting(key, out var setting));
        Assert.NotNull(setting);
        Assert.Equal(key, setting.Key);
        Assert.Equal(minimum, setting.Minimum);
        Assert.Equal(maximum, setting.Maximum);
        var source = Assert.Single(setting.Sources);
        Assert.Equal("https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md", source.Url.AbsoluteUri);
        Assert.Equal("v2.16.0", source.VersionOrDate);
        Assert.Contains(inventoryId, setting.InventoryIds);
    }

    [Theory]
    [InlineData("request.timeout.ms")]
    [InlineData("socket.timeout.ms")]
    [InlineData("enable.idempotence")]
    [InlineData("max.in.flight")]
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
    public void Given_OverridePolicy_When_CheckingSupportedRoles_Then_ExposesExactlyApprovedRoles()
    {
        Assert.True(KafkaScenarioOverridePolicy.SupportsRole(KafkaClientRole.LoadGenProducer));
        Assert.False(KafkaScenarioOverridePolicy.SupportsRole(KafkaClientRole.EnrichmentProducer));
        Assert.True(KafkaScenarioOverridePolicy.SupportsRole(KafkaClientRole.EnrichmentConsumer));
        Assert.True(KafkaScenarioOverridePolicy.SupportsRole(KafkaClientRole.VerifierConsumer));
        Assert.False(KafkaScenarioOverridePolicy.SupportsRole((KafkaClientRole)int.MaxValue));
    }

    [Fact]
    public void Given_DebugOverride_When_CheckingRoles_Then_AllowsExactlyBothConsumers()
    {
        Assert.True(KafkaScenarioOverridePolicy.TryGetSetting("debug", out var setting));
        Assert.True(setting.SupportsRole(KafkaClientRole.EnrichmentConsumer));
        Assert.True(setting.SupportsRole(KafkaClientRole.VerifierConsumer));
        Assert.False(setting.SupportsRole(KafkaClientRole.LoadGenProducer));
        Assert.False(setting.SupportsRole(KafkaClientRole.EnrichmentProducer));
        Assert.False(setting.SupportsRole((KafkaClientRole)int.MaxValue));
    }

    // Keys match exactly, so a misspelt scenario key is refused rather than treated as the approved one.
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

    // Only exact IDs from the frozen inventory (docs/blueprint.md section 12) are in scope.
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

    // The constructor guards below keep every override entry tied to a role, a frozen inventory ID
    // and a cited source (ADR-015; AGENTS.md, Truth and verification).
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
    public void Given_UndefinedValueKind_When_CreatingOverrideMetadata_Then_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateSetting(valueKind: (KafkaScenarioOverrideValueKind)99));
        Assert.Equal("valueKind", exception.ParamName);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    public void Given_BooleanValueKindWithWrongBounds_When_CreatingOverrideMetadata_Then_Throws(int minimum, int maximum)
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateSetting(valueKind: KafkaScenarioOverrideValueKind.Boolean,
            minimum: minimum, maximum: maximum));
        Assert.Equal("minimum", exception.ParamName);
    }

    [Fact]
    public void Given_BooleanValueKindWithPlaceholderBounds_When_CreatingOverrideMetadata_Then_Accepts()
    {
        var setting = CreateSetting(valueKind: KafkaScenarioOverrideValueKind.Boolean, minimum: 0, maximum: 1);
        Assert.Equal(KafkaScenarioOverrideValueKind.Boolean, setting.ValueKind);
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
        int minimum, int maximum, params string[] inventoryIds)
    {
        Assert.Equal(key, setting.Key);
        Assert.Equal(minimum, setting.Minimum);
        Assert.Equal(maximum, setting.Maximum);
        Assert.Equal([KafkaClientRole.LoadGenProducer], setting.SupportedRoles);
        Assert.Equal(inventoryIds, setting.InventoryIds);
        Assert.Single(setting.Sources);
        Assert.Empty(setting.AllowedValues);
        Assert.False(setting.AllowAnyNonemptyValue);
        Assert.False(setting.AllowActivation);
    }

    private static void AssertNewEntry(KafkaScenarioOverrideSetting setting, string key, KafkaScenarioOverrideValueKind kind,
        KafkaClientRole role, string inventory, int minimum, int maximum, string[]? allowed = null, bool any = false, bool activation = false)
    {
        Assert.Equal(key, setting.Key);
        Assert.Equal(kind, setting.ValueKind);
        Assert.Equal([role], setting.SupportedRoles);
        Assert.Equal([inventory], setting.InventoryIds);
        Assert.Equal(minimum, setting.Minimum);
        Assert.Equal(maximum, setting.Maximum);
        Assert.Equal(allowed ?? [], setting.AllowedValues);
        Assert.Equal(any, setting.AllowAnyNonemptyValue);
        Assert.Equal(activation, setting.AllowActivation);
        Assert.NotEmpty(setting.Sources);
        Assert.All(setting.Sources, source => Assert.Equal("v2.16.0", source.VersionOrDate));
    }

    [Theory]
    [InlineData(KafkaScenarioOverrideValueKind.String)]
    [InlineData(KafkaScenarioOverrideValueKind.Omission)]
    public void Given_StringOrOmissionKind_When_UsingNumericConstructor_Then_Rejects(KafkaScenarioOverrideValueKind kind) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateSetting(valueKind: kind));

    [Theory]
    [InlineData(false, "")]
    [InlineData(false, "gzip,gzip")]
    [InlineData(true, "gzip")]
    [InlineData(false, null)]
    public void Given_InvalidStringEnum_When_Constructing_Then_Rejects(bool any, string? csv)
    {
        ImmutableArray<string> values = csv is null ? [] : [.. csv.Split(',')];
        Assert.Throws<ArgumentException>(() => new KafkaScenarioOverrideSetting("compression.type", values, any, false,
            [KafkaClientRole.LoadGenProducer], ["B03"], [new(new Uri("https://example.com/configuration"), "v1")]));
    }

    [Fact]
    public void Given_DefaultEnumWithAllowAny_When_Constructing_Then_NormalizesValuesToEmpty()
    {
        var setting = new KafkaScenarioOverrideSetting("transactional.id", default, true, true,
            [KafkaClientRole.LoadGenProducer], ["B05"], [new(new Uri("https://example.com/configuration"), "v1")]);
        Assert.False(setting.AllowedValues.IsDefault);
        Assert.Empty(setting.AllowedValues);
        Assert.True(setting.AllowAnyNonemptyValue);
        Assert.True(setting.AllowActivation);
        Assert.Equal(KafkaScenarioOverrideValueKind.String, setting.ValueKind);
        Assert.Equal(0, setting.Minimum);
        Assert.Equal(0, setting.Maximum);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("inventory")]
    [InlineData("source")]
    [InlineData("null_source")]
    public void Given_InvalidSharedMetadata_When_ConstructingStringAndOmission_Then_Rejects(string invalid)
    {
        ImmutableArray<KafkaClientRole> roles = invalid == "role" ? [(KafkaClientRole)99] : [KafkaClientRole.EnrichmentConsumer];
        ImmutableArray<string> inventory = invalid == "inventory" ? ["Z99"] : ["C08"];
        ImmutableArray<KafkaSettingSource> sources = invalid == "source" ? [] : invalid == "null_source" ? [null!] : [new(new Uri("https://example.com/configuration"), "v1")];
        Assert.Throws<ArgumentException>(() => new KafkaScenarioOverrideSetting("group.protocol", ["consumer"], false, false, roles, inventory, sources));
        Assert.Throws<ArgumentException>(() => new KafkaScenarioOverrideSetting("session.timeout.ms", roles, inventory, sources));
    }

    private static KafkaScenarioOverrideSetting CreateSetting(string key = "request.timeout.ms",
        int minimum = 1, int maximum = 900_000,
        KafkaScenarioOverrideValueKind valueKind = KafkaScenarioOverrideValueKind.Integer,
        ImmutableArray<KafkaClientRole> roles = default,
        ImmutableArray<string> inventoryIds = default,
        ImmutableArray<KafkaSettingSource> sources = default) =>
        new(key, valueKind, minimum, maximum,
            roles.IsDefault ? [KafkaClientRole.LoadGenProducer] : roles,
            inventoryIds.IsDefault ? ["A01"] : inventoryIds,
            sources.IsDefault
                ? [new KafkaSettingSource(new Uri("https://example.com/configuration"), "v1")]
                : sources);
}
