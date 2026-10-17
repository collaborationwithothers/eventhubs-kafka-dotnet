using Confluent.Kafka;
using Parchwell.Kafka.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Tests.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class AuthenticationConfigurationTests
{
    [Theory]
    [InlineData("synthetic", "https://synthetic.servicebus.windows.net/.default")]
    [InlineData("a-b", "https://a-b.servicebus.windows.net/.default")]
    public void Given_ValidNamespace_When_DerivingScope_Then_UsesValidatedNamespace(string namespaceName, string expected)
    {
        Assert.Equal(expected, KafkaAuthenticationConfiguration.GetScope(new KafkaBaselineOptions { NamespaceName = namespaceName }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid.example")]
    public void Given_InvalidNamespace_When_DerivingScope_Then_Rejects(string? namespaceName)
    {
        Assert.Throws<InvalidOperationException>(() => KafkaAuthenticationConfiguration.GetScope(new KafkaBaselineOptions { NamespaceName = namespaceName! }));
    }

    [Fact]
    public void Given_RealBaselineForEveryRole_When_Validating_Then_AcceptsWithoutCreatingClients()
    {
        var services = new ServiceCollection();
        foreach (KafkaClientRole role in Enum.GetValues<KafkaClientRole>())
            services.AddParchwellKafka(role, ConfigurationMappingTests.Configure);

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<Microsoft.Extensions.Options.IStartupValidator>().Validate();
        KafkaConfigurationFactory factory = provider.GetRequiredService<KafkaConfigurationFactory>();

        foreach (KafkaClientRole role in Enum.GetValues<KafkaClientRole>())
            KafkaAuthenticationConfiguration.Validate(GetBaselineConfiguration(factory, role));
    }

    [Fact]
    public void Given_ApprovedAuthenticationSettings_When_Validating_Then_Accepts()
    {
        KafkaAuthenticationConfiguration.Validate(ValidConfiguration());
    }

    [Theory]
    [InlineData("security.protocol", "plaintext")]
    [InlineData("sasl.mechanism", "PLAIN")]
    [InlineData("sasl.oauthbearer.method", "oidc")]
    [InlineData("sasl.oauthbearer.metadata.authentication.type", "azure_imds")]
    [InlineData("enable.sasl.oauthbearer.unsecure.jwt", "true")]
    [InlineData("enable.ssl.certificate.verification", "false")]
    [InlineData("ssl.endpoint.identification.algorithm", "none")]
    public void Given_ChangedRequiredSetting_When_Validating_Then_RejectsWithoutEchoingValue(string key, string value)
    {
        ProducerConfig configuration = ValidConfiguration();
        configuration.Set(key, value);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => KafkaAuthenticationConfiguration.Validate(configuration));
        Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("security.protocol")]
    [InlineData("sasl.mechanism")]
    [InlineData("sasl.oauthbearer.method")]
    [InlineData("sasl.oauthbearer.metadata.authentication.type")]
    [InlineData("enable.sasl.oauthbearer.unsecure.jwt")]
    [InlineData("enable.ssl.certificate.verification")]
    [InlineData("ssl.endpoint.identification.algorithm")]
    public void Given_MissingRequiredSetting_When_Validating_Then_Rejects(string key)
    {
        ProducerConfig configuration = ValidConfiguration(key);
        Assert.Throws<InvalidOperationException>(() => KafkaAuthenticationConfiguration.Validate(configuration));
    }

    [Theory]
    [InlineData("sasl.username")]
    [InlineData("sasl.password")]
    [InlineData("sasl.jaas.config")]
    [InlineData("sasl.oauthbearer.client.id")]
    [InlineData("sasl.oauthbearer.client.secret")]
    [InlineData("sasl.oauthbearer.token.endpoint.url")]
    [InlineData("sasl.oauthbearer.scope")]
    public void Given_ExcludedSettingPresent_When_Validating_Then_Rejects(string key)
    {
        ProducerConfig configuration = ValidConfiguration();
        configuration.Set(key, "synthetic-private-value");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => KafkaAuthenticationConfiguration.Validate(configuration));
        Assert.DoesNotContain("synthetic-private-value", exception.Message, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData("sasl.username")]
    [InlineData("sasl.password")]
    [InlineData("sasl.jaas.config")]
    [InlineData("sasl.oauthbearer.client.id")]
    [InlineData("sasl.oauthbearer.client.secret")]
    [InlineData("sasl.oauthbearer.token.endpoint.url")]
    [InlineData("sasl.oauthbearer.scope")]
    public void Given_ExcludedSettingPresentAsEmpty_When_Validating_Then_Rejects(string key)
    {
        ProducerConfig configuration = ValidConfiguration();
        configuration.Set(key, string.Empty);
        Assert.Throws<InvalidOperationException>(() => KafkaAuthenticationConfiguration.Validate(configuration));
    }

    private static ClientConfig GetBaselineConfiguration(KafkaConfigurationFactory factory, KafkaClientRole role) => role switch
    {
        KafkaClientRole.LoadGenProducer => factory.CreateLoadGenProducer().Configuration,
        KafkaClientRole.EnrichmentProducer => factory.CreateEnrichmentProducer().Configuration,
        KafkaClientRole.EnrichmentConsumer => factory.CreateEnrichmentConsumer().Configuration,
        KafkaClientRole.VerifierConsumer => factory.CreateVerifierConsumer().Configuration,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static ProducerConfig ValidConfiguration(string? omittedKey = null)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal)
        {
            ["security.protocol"] = "sasl_ssl",
            ["sasl.mechanism"] = "OAUTHBEARER",
            ["sasl.oauthbearer.method"] = "default",
            ["sasl.oauthbearer.metadata.authentication.type"] = "none",
            ["enable.sasl.oauthbearer.unsecure.jwt"] = "false",
            ["enable.ssl.certificate.verification"] = "true",
            ["ssl.endpoint.identification.algorithm"] = "https"
        };
        if (omittedKey is not null) values.Remove(omittedKey);
        return new ProducerConfig(values);
    }
}
