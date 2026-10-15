using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Configuration;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Protects startup validation: bad caller inputs must stop the host at start, before any client
// configuration is built (docs/operations/kafka-baseline.md, Registration and construction).
[Trait("Category", "Unit")]
public sealed class ConfigurationValidationTests
{
    // The input is a bare namespace name that the library turns into the endpoint itself, so URLs, ports,
    // suffixes and anything outside a single ASCII DNS label are rejected.
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

    // Only the enrichment consumer needs a caller-supplied group; the verifier's group is fixed (ADR-013).
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

    // RFC 1035 section 2.3.4 limits a DNS label to 63 octets; this pins both sides of that limit.
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

    // Each role validates under its own options name, so the error names the broken role and a valid
    // role in the same process still works.
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

    // A process registers only the roles it uses; asking for another must fail, not return a
    // configuration built from empty inputs.
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

    // Startup validation runs once, so the factory validates its own snapshot again; options changed
    // after startup cannot emit an invalid endpoint.
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

    // A role's validator must skip options registered for other names, or one role's rules would
    // reject another role's inputs.
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

    // Required inputs are read from the catalogue entries that emit them, so adding an input-backed
    // setting adds its startup check without a separate list to maintain (kafka-baseline.md).
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
