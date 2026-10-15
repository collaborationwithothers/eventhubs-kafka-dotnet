using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Configuration;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Protects the audit record (ADR-015): every emitted setting or omission must carry a key, a reason,
// frozen inventory IDs and HTTPS sources with a pinned version or date.
[Trait("Category", "Unit")]
public sealed class MetadataValidationTests
{
    private static readonly ImmutableArray<KafkaSettingSource> ValidSources =
        [new(new Uri(ApprovedConfigurationContract.Native), "v2.16.0")];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Given_MissingKey_When_ConstructingMetadata_Then_Rejects(string? input) =>
        Assert.ThrowsAny<ArgumentException>(() => Metadata(key: input!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Given_MissingReason_When_ConstructingMetadata_Then_Rejects(string? input) =>
        Assert.ThrowsAny<ArgumentException>(() => Metadata(reason: input!));

    [Theory]
    [InlineData("role")]
    [InlineData("disposition")]
    public void Given_UnknownAuditEnum_When_ConstructingMetadata_Then_Rejects(string field) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => field == "role"
            ? Metadata(role: (KafkaClientRole)(-1)) : Metadata(disposition: (KafkaSettingDisposition)(-1)));

    // Only an Explicit setting has a value; an omission with a value would record a setting never sent.
    [Theory]
    [InlineData(KafkaSettingDisposition.Explicit, null)]
    [InlineData(KafkaSettingDisposition.Excluded, "60000")]
    [InlineData(KafkaSettingDisposition.Unspecified, "60000")]
    public void Given_ValueDispositionMismatch_When_ConstructingMetadata_Then_Rejects(KafkaSettingDisposition disposition, string? value) =>
        Assert.Throws<ArgumentException>(() => Metadata(value: value, disposition: disposition));

    [Theory]
    [InlineData("default")]
    [InlineData("empty")]
    [InlineData("unknown")]
    [InlineData("null-member")]
    public void Given_InvalidInventory_When_ConstructingMetadata_Then_Rejects(string input)
    {
        ImmutableArray<string> ids = input switch
        {
            "default" => default,
            "empty" => [],
            "unknown" => ["A99"],
            _ => ["A01", null!]
        };
        Assert.Throws<ArgumentException>(() => Metadata(ids: ids));
    }

    [Theory]
    [InlineData("default")]
    [InlineData("empty")]
    [InlineData("null-member")]
    public void Given_InvalidSources_When_ConstructingMetadata_Then_Rejects(string input)
    {
        ImmutableArray<KafkaSettingSource> sources = input switch
        {
            "default" => default,
            "empty" => [],
            _ => [null!]
        };
        Assert.Throws<ArgumentException>(() => Metadata(sources: sources));
    }

    [Fact]
    public void Given_NullInventoryId_When_LookingUp_Then_IsUnknown() => Assert.False(FrozenInventory.Contains(null!));

    [Theory]
    [InlineData(KafkaSettingDisposition.Explicit, "60000")]
    [InlineData(KafkaSettingDisposition.Excluded, null)]
    [InlineData(KafkaSettingDisposition.Unspecified, null)]
    public void Given_ValidDisposition_When_ConstructingMetadata_Then_PreservesValueAndDisposition(KafkaSettingDisposition disposition, string? value)
    {
        var metadata = Metadata(value: value, disposition: disposition);
        Assert.Equal(value, metadata.Value);
        Assert.Equal(disposition, metadata.Disposition);
    }

    [Fact]
    public void Given_NullCitation_When_ConstructingSource_Then_Rejects() =>
        Assert.Throws<ArgumentNullException>(() => new KafkaSettingSource(null!, "v2.16.0"));

    [Theory]
    [InlineData("relative")]
    [InlineData("http://example.com/source")]
    [InlineData("file:///tmp/source")]
    public void Given_NonHttpsCitation_When_ConstructingSource_Then_Rejects(string url) =>
        Assert.Throws<ArgumentException>(() => new KafkaSettingSource(new Uri(url, UriKind.RelativeOrAbsolute), "v2.16.0"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Given_MissingSourceVersion_When_ConstructingSource_Then_Rejects(string? version) =>
        Assert.ThrowsAny<ArgumentException>(() => new KafkaSettingSource(new Uri(ApprovedConfigurationContract.Native), version!));

    [Fact]
    public void Given_ValidCitation_When_ConstructingSource_Then_PreservesUrlAndVersion()
    {
        var source = new KafkaSettingSource(new Uri(ApprovedConfigurationContract.Native), "v2.16.0");
        Assert.Equal(ApprovedConfigurationContract.Native, source.Url.AbsoluteUri);
        Assert.Equal("v2.16.0", source.VersionOrDate);
    }

    [Fact]
    public void Given_NullOptionsMonitor_When_ConstructingFactory_Then_Rejects() =>
        Assert.Throws<ArgumentNullException>(() => new KafkaConfigurationFactory(null!, []));

    [Fact]
    public void Given_NullRegistrations_When_ConstructingFactory_Then_Rejects()
    {
        using var provider = ConfigurationMappingTests.Provider(KafkaClientRole.LoadGenProducer);
        var monitor = provider.GetRequiredService<IOptionsMonitor<KafkaBaselineOptions>>();
        Assert.Throws<ArgumentNullException>(() => new KafkaConfigurationFactory(monitor, null!));
    }

    // Catalogue definitions fail when declared, so a broken entry never reaches a native configuration.
    [Fact]
    public void Given_NullLiteral_When_DefiningExplicitSetting_Then_RejectsBeforeEmission() =>
        Assert.Throws<ArgumentNullException>(() => KafkaSettingDefinition.Explicit("synthetic.key", null!, ["A01"], ValidSources, "Synthetic"));

    [Fact]
    public void Given_NullInputDescriptor_When_DefiningInputSetting_Then_RejectsBeforeEmission() =>
        Assert.Throws<ArgumentNullException>(() => KafkaSettingDefinition.FromInput("synthetic.key", null!, ["A01"], ValidSources, "Synthetic"));

    [Theory]
    [InlineData(KafkaSettingDisposition.Explicit)]
    [InlineData((KafkaSettingDisposition)(-1))]
    public void Given_NonOmissionDisposition_When_DefiningOmission_Then_Rejects(KafkaSettingDisposition disposition) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => KafkaSettingDefinition.Omitted("synthetic.key", disposition, ["A01"], ValidSources, "Synthetic"));

    [Theory]
    [InlineData(KafkaSettingDisposition.Excluded)]
    [InlineData(KafkaSettingDisposition.Unspecified)]
    public void Given_OmittedDefinition_When_ResolvingValue_Then_NamesTheKey(KafkaSettingDisposition disposition)
    {
        var definition = KafkaSettingDefinition.Omitted("synthetic.key", disposition, ["A01"], ValidSources, "Synthetic");
        var error = Assert.Throws<InvalidOperationException>(() => definition.ResolveValue(new()));
        Assert.Contains("synthetic.key", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_MissingRequiredInput_When_ResolvingDefinition_Then_NamesTheKeyAndInput()
    {
        var definition = KafkaSettingDefinition.FromInput("synthetic.key", KafkaRequiredInput.EnrichmentGroupId, ["A01"], ValidSources, "Synthetic");
        var error = Assert.Throws<InvalidOperationException>(() => definition.ResolveValue(new()));
        Assert.Contains("synthetic.key", error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(KafkaBaselineOptions.EnrichmentGroupId), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_UndefinedRole_When_SelectingCatalogue_Then_Rejects() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => BaselineSettingsCatalog.GetEntries((KafkaClientRole)(-1)));

    private static KafkaSettingMetadata Metadata(string key = "socket.timeout.ms", string? value = "60000",
        KafkaClientRole role = KafkaClientRole.LoadGenProducer, KafkaSettingDisposition disposition = KafkaSettingDisposition.Explicit,
        ImmutableArray<string>? ids = null, ImmutableArray<KafkaSettingSource>? sources = null, string reason = "Pinned default") =>
        new(key, value, role, disposition, ids ?? ["A01"], sources ?? ValidSources, reason);
}
