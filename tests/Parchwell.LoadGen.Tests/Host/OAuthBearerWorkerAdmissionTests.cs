using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.Run.Execution;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class OAuthBearerWorkerAdmissionTests
{
    [Fact]
    public void Given_ValidAuthenticationWiring_When_AdmissionRuns_Then_DoesNotAcquireOrReadTokenFile()
    {
        var provider = new TrackingProvider();
        using ServiceProvider services = CreateServices(provider);
        ProducerConfig configuration = services.GetRequiredService<KafkaConfigurationFactory>()
            .CreateLoadGenProducer().Configuration;
        var admission = new OAuthBearerWorkerAdmission(services.GetRequiredService<KafkaAuthenticationFactory>());

        admission.EnsureProducerAuthenticationAvailable(configuration);

        Assert.Equal(0, provider.Calls);
        Assert.False(File.Exists(provider.TokenFilePath));
    }

    [Fact]
    public void Given_ExcludedAuthenticationSetting_When_AdmissionRuns_Then_RejectsBeforeCredentialUse()
    {
        var provider = new TrackingProvider();
        using ServiceProvider services = CreateServices(provider);
        ProducerConfig configuration = services.GetRequiredService<KafkaConfigurationFactory>()
            .CreateLoadGenProducer().Configuration;
        configuration.Set("sasl.username", "synthetic-secret");
        var admission = new OAuthBearerWorkerAdmission(services.GetRequiredService<KafkaAuthenticationFactory>());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => admission.EnsureProducerAuthenticationAvailable(configuration));

        Assert.DoesNotContain("synthetic-secret", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, provider.Calls);
        Assert.False(File.Exists(provider.TokenFilePath));
    }

    private static ServiceProvider CreateServices(TrackingProvider provider)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaTokenProvider>(provider);
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-kafka-client";
        });
        services.AddParchwellKafkaAuthentication(options =>
        {
            options.TenantId = "synthetic-tenant";
            options.ClientId = "synthetic-credential-app";
            options.TokenFilePath = provider.TokenFilePath;
        });
        return services.BuildServiceProvider();
    }

    private sealed class TrackingProvider : IKafkaTokenProvider
    {
        private int _calls;
        public string TokenFilePath { get; } = Path.Combine(Path.GetTempPath(), "missing-token-" + Guid.NewGuid().ToString("N"));
        public int Calls => Volatile.Read(ref _calls);

        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            throw new InvalidOperationException("provider_must_not_be_called_during_admission");
        }
    }
}
#pragma warning restore CA1707
