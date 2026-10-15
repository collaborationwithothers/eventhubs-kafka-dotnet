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

    [Theory]
    [InlineData("a")]
    [InlineData("A1")]
    [InlineData("a-b")]
    public void Given_ValidNamespaceLabel_When_StartupValidating_Then_Accepts(string name)
    {
        using var provider = UnvalidatedProvider(KafkaClientRole.LoadGenProducer, options => options.NamespaceName = name);
        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Theory]
    [InlineData(63, true)]
    [InlineData(64, false)]
    public void Given_NamespaceLengthBoundary_When_StartupValidating_Then_EnforcesDnsLabelLimit(int length, bool valid)
    {
        using var provider = UnvalidatedProvider(KafkaClientRole.LoadGenProducer, options => options.NamespaceName = new string('a', length));
        if (valid)
            provider.GetRequiredService<IStartupValidator>().Validate();
        else
            Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
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

    public static IEnumerable<object[]> RequiredInputCases() =>
        Enum.GetValues<KafkaClientRole>().SelectMany(role => BaselineSettingsCatalog.GetEntries(role)
            .Select(entry => entry.RequiredInput).OfType<KafkaRequiredInput>().Distinct()
            .Select(input => new object[] { role, input.Name }));

    [Theory]
    [MemberData(nameof(RequiredInputCases))]
    public void Given_CatalogueRequiredInput_When_Missing_Then_StartupRejectsForThatRole(KafkaClientRole role, string inputName)
    {
        using var provider = UnvalidatedProvider(role, options =>
            typeof(KafkaBaselineOptions).GetProperty(inputName)!.SetValue(options, null));
        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains(error.Failures, failure => failure.StartsWith(inputName, StringComparison.Ordinal));
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
