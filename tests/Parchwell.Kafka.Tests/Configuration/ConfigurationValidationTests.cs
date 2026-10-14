using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Configuration;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class ConfigurationValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("https://namespace")]
    [InlineData("namespace:9093")]
    [InlineData("namespace.servicebus.windows.net")]
    [InlineData("user@namespace")]
    [InlineData("name_space")]
    [InlineData("-namespace")]
    [InlineData("namespace-")]
    [InlineData("name space")]
    [InlineData("namespace\n")]
    [InlineData("nämespace")]
    public void Given_InvalidNamespace_When_StartupValidating_Then_RejectsBeforeFactoryUse(string? input)
    {
        using var provider = UnvalidatedProvider(KafkaClientRole.LoadGenProducer, options => options.NamespaceName = input!);
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("two words")]
    [InlineData("line\nbreak")]
    [InlineData("control\0character")]
    public void Given_InvalidClientIdentity_When_StartupValidating_Then_RejectsBeforeFactoryUse(string? input)
    {
        using var provider = UnvalidatedProvider(KafkaClientRole.VerifierConsumer, options => options.ClientId = input!);
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("two words")]
    [InlineData("control\0character")]
    public void Given_InvalidEnrichmentGroup_When_StartupValidatingEnrichmentConsumer_Then_Rejects(string? input)
    {
        using var provider = UnvalidatedProvider(KafkaClientRole.EnrichmentConsumer, options => options.EnrichmentGroupId = input);
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Theory]
    [InlineData(KafkaClientRole.LoadGenProducer)]
    [InlineData(KafkaClientRole.EnrichmentProducer)]
    [InlineData(KafkaClientRole.VerifierConsumer)]
    public void Given_UnusedMissingEnrichmentGroup_When_StartupValidatingOtherRole_Then_Accepts(KafkaClientRole role)
    {
        using var provider = UnvalidatedProvider(role, options => options.EnrichmentGroupId = null);
        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public void Given_NamespaceLabelBoundaries_When_StartupValidating_Then_AcceptsValidDnsLabelsAndRejectsOverlongLabel()
    {
        foreach (string name in new[] { "a", "A1", "a-b", new string('a', 63) })
        {
            using var provider = UnvalidatedProvider(KafkaClientRole.LoadGenProducer, options => options.NamespaceName = name);
            provider.GetRequiredService<IStartupValidator>().Validate();
        }
        using var overlong = UnvalidatedProvider(KafkaClientRole.LoadGenProducer, options => options.NamespaceName = new string('a', 64));
        Assert.Throws<OptionsValidationException>(() => overlong.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void Given_OneInvalidNamedRoleAmongValidRoles_When_StartupValidating_Then_FailsTheRegisteredInvalidRole()
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, ConfigurationMappingTests.Configure);
        services.AddParchwellKafka(KafkaClientRole.EnrichmentConsumer, options =>
        {
            ConfigurationMappingTests.Configure(options);
            options.EnrichmentGroupId = null;
        });
        using var provider = services.BuildServiceProvider();
        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Equal(nameof(KafkaClientRole.EnrichmentConsumer), error.OptionsName);
        Assert.Equal("synthetic-client", provider.GetRequiredService<KafkaConfigurationFactory>().CreateLoadGenProducer().Configuration.ClientId);
    }

    [Fact]
    public void Given_UnregisteredRoles_When_RequestingTheirConfigurations_Then_FailsExplicitly()
    {
        using var provider = ConfigurationMappingTests.Provider(KafkaClientRole.LoadGenProducer);
        var factory = provider.GetRequiredService<KafkaConfigurationFactory>();
        Assert.Throws<InvalidOperationException>(() => factory.CreateEnrichmentProducer());
        Assert.Throws<InvalidOperationException>(() => factory.CreateEnrichmentConsumer());
        Assert.Throws<InvalidOperationException>(() => factory.CreateVerifierConsumer());
        using var consumerProvider = ConfigurationMappingTests.Provider(KafkaClientRole.EnrichmentConsumer);
        Assert.Throws<InvalidOperationException>(() => consumerProvider.GetRequiredService<KafkaConfigurationFactory>().CreateLoadGenProducer());
    }

    [Fact]
    public void Given_ValidatedButLaterMutatedNamedOptions_When_CreatingConfiguration_Then_RejectsInvalidSnapshot()
    {
        using var provider = ConfigurationMappingTests.Provider(KafkaClientRole.LoadGenProducer);
        var monitor = provider.GetRequiredService<IOptionsMonitor<KafkaBaselineOptions>>();
        monitor.Get(nameof(KafkaClientRole.LoadGenProducer)).NamespaceName = "https://invalid";
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<KafkaConfigurationFactory>().CreateLoadGenProducer());
    }

    [Fact]
    public void Given_InvalidRegistrationArguments_When_Registering_Then_RejectsLocally()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => KafkaServiceCollectionExtensions.AddParchwellKafka(null!, KafkaClientRole.LoadGenProducer, ConfigurationMappingTests.Configure));
        Assert.Throws<ArgumentNullException>(() => services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddParchwellKafka((KafkaClientRole)(-1), ConfigurationMappingTests.Configure));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KafkaBaselineOptionsValidator((KafkaClientRole)(-1)));
    }

    [Fact]
    public void Given_UnrelatedOptionsName_When_ValidatingNamedRole_Then_SkipsOnlyUnrelatedNames()
    {
        var validator = new KafkaBaselineOptionsValidator(KafkaClientRole.EnrichmentConsumer);
        Assert.True(validator.Validate("unregistered", new()).Skipped);
        Assert.True(validator.Validate(null, new()).Skipped);
        var result = validator.Validate(nameof(KafkaClientRole.EnrichmentConsumer), new());
        Assert.True(result.Failed);
        Assert.Throws<ArgumentNullException>(() => validator.Validate(nameof(KafkaClientRole.EnrichmentConsumer), null!));
    }

    private static ServiceProvider UnvalidatedProvider(KafkaClientRole role, Action<KafkaBaselineOptions> change)
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(role, options =>
        {
            ConfigurationMappingTests.Configure(options);
            change(options);
        });
        return services.BuildServiceProvider();
    }
}
