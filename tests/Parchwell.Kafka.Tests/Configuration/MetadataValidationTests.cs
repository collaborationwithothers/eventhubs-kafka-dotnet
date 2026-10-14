using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Configuration;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class MetadataValidationTests
{
    private static readonly ImmutableArray<KafkaSettingSource> ValidSources =
        [new(new Uri(ApprovedConfigurationContract.Native), "v2.16.0")];

    [Fact]
    public void Given_InvalidAuditFields_When_ConstructingMetadata_Then_RejectsUnauditableEntries()
    {
        foreach (string? input in new string?[] { null, "", " " })
        {
            Assert.ThrowsAny<ArgumentException>(() => Metadata(key: input!));
            Assert.ThrowsAny<ArgumentException>(() => Metadata(reason: input!));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => Metadata(role: (KafkaClientRole)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Metadata(disposition: (KafkaSettingDisposition)(-1)));
        Assert.Throws<ArgumentException>(() => Metadata(value: null));
        Assert.Throws<ArgumentException>(() => Metadata(disposition: KafkaSettingDisposition.Excluded));
        Assert.Throws<ArgumentException>(() => Metadata(disposition: KafkaSettingDisposition.Unspecified));
        Assert.Throws<ArgumentException>(() => Metadata(ids: default(ImmutableArray<string>)));
        Assert.Throws<ArgumentException>(() => Metadata(ids: []));
        Assert.Throws<ArgumentException>(() => Metadata(ids: ["A99"]));
        Assert.Throws<ArgumentException>(() => Metadata(ids: ["A01", null!]));
        Assert.Throws<ArgumentException>(() => Metadata(sources: default(ImmutableArray<KafkaSettingSource>)));
        Assert.Throws<ArgumentException>(() => Metadata(sources: []));
        Assert.Throws<ArgumentException>(() => Metadata(sources: [null!]));
        Assert.False(FrozenInventory.Contains(null!));
    }

    [Fact]
    public void Given_AllValidDispositions_When_ConstructingMetadata_Then_RequiresValuesOnlyForExplicitEmission()
    {
        var explicitMetadata = Metadata();
        Assert.Equal("60000", explicitMetadata.Value);
        foreach (var disposition in new[] { KafkaSettingDisposition.Excluded, KafkaSettingDisposition.Unspecified })
        {
            var omitted = Metadata(value: null, disposition: disposition);
            Assert.Null(omitted.Value);
            Assert.Equal(disposition, omitted.Disposition);
        }
    }

    [Fact]
    public void Given_InvalidCitation_When_ConstructingSource_Then_RequiresAbsoluteHttpsAndPinnedVersionOrDate()
    {
        Assert.Throws<ArgumentNullException>(() => new KafkaSettingSource(null!, "v2.16.0"));
        foreach (string url in new[] { "relative", "http://example.com/source", "file:///tmp/source" })
            Assert.Throws<ArgumentException>(() => new KafkaSettingSource(new Uri(url, UriKind.RelativeOrAbsolute), "v2.16.0"));
        foreach (string? version in new string?[] { null, "", " " })
            Assert.ThrowsAny<ArgumentException>(() => new KafkaSettingSource(new Uri(ApprovedConfigurationContract.Native), version!));
        var source = new KafkaSettingSource(new Uri(ApprovedConfigurationContract.Native), "v2.16.0");
        Assert.Equal(ApprovedConfigurationContract.Native, source.Url.AbsoluteUri);
        Assert.Equal("v2.16.0", source.VersionOrDate);
    }

    [Fact]
    public void Given_InvalidFactoryDependencies_When_Constructing_Then_RejectsMissingOptionsOrUndefinedRoles()
    {
        using var provider = ConfigurationMappingTests.Provider(KafkaClientRole.LoadGenProducer);
        var monitor = provider.GetRequiredService<IOptionsMonitor<KafkaBaselineOptions>>();
        Assert.Throws<ArgumentNullException>(() => new KafkaConfigurationFactory(null!, []));
        Assert.Throws<ArgumentNullException>(() => new KafkaConfigurationFactory(monitor, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KafkaConfigurationFactory(monitor, [new((KafkaClientRole)(-1))]));
    }

    [Fact]
    public void Given_InvalidInternalDefinition_When_ResolvingValue_Then_FailsRatherThanEmittingInventedValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BaselineSettingsCatalog.GetEntries((KafkaClientRole)(-1)));
        var options = new KafkaBaselineOptions();
        var definition = new KafkaSettingDefinition("synthetic.key", null, KafkaSettingValueKind.Literal,
            KafkaSettingDisposition.Explicit, ["A01"], ValidSources, "Synthetic malformed definition");
        Assert.Throws<InvalidOperationException>(() => KafkaConfigurationFactory.ResolveValue(definition, options));
        Assert.Throws<InvalidOperationException>(() => KafkaConfigurationFactory.ResolveValue(definition with { ValueKind = KafkaSettingValueKind.EnrichmentGroupId }, options));
        Assert.Throws<InvalidOperationException>(() => KafkaConfigurationFactory.ResolveValue(definition with { ValueKind = (KafkaSettingValueKind)(-1) }, options));
    }

    private static KafkaSettingMetadata Metadata(string key = "socket.timeout.ms", string? value = "60000",
        KafkaClientRole role = KafkaClientRole.LoadGenProducer, KafkaSettingDisposition disposition = KafkaSettingDisposition.Explicit,
        ImmutableArray<string>? ids = null, ImmutableArray<KafkaSettingSource>? sources = null, string reason = "Pinned default") =>
        new(key, value, role, disposition, ids ?? ["A01"], sources ?? ValidSources, reason);
}
