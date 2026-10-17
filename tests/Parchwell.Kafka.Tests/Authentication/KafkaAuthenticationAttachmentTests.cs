using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class KafkaAuthenticationAttachmentTests
{
    [Theory]
    [InlineData("producer_producer")]
    [InlineData("consumer_consumer")]
    [InlineData("producer_consumer")]
    [InlineData("consumer_producer")]
    public void Given_SessionAttachedOnce_When_AnotherBuilderAttaches_Then_RejectsBeforeAcquisition(string combination)
    {
        var tokenProvider = new CountingTokenProvider();
        using ServiceProvider services = CreateServices(tokenProvider);
        KafkaAuthenticationFactory factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        KafkaConfigurationFactory configurations = services.GetRequiredService<KafkaConfigurationFactory>();
        bool firstIsProducer = combination.StartsWith("producer", StringComparison.Ordinal);
        KafkaClientRole role = firstIsProducer ? KafkaClientRole.LoadGenProducer : KafkaClientRole.VerifierConsumer;
        KafkaOAuthBearerSession session = factory.CreateSession(Identity(role), TestContext.Current.CancellationToken);

        Attach(factory, configurations, session, firstIsProducer);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Attach(factory, configurations, session, combination.EndsWith("producer", StringComparison.Ordinal)));

        Assert.Equal("OAuth bearer session is already attached to a native client.", exception.Message);
        Assert.Equal(0, tokenProvider.Calls);
        Assert.Null(session.InitialFailure);
        Assert.Null(session.InstalledTokenExpiresOn);
    }

    [Fact]
    public async Task Given_CompetingBuilderAttachments_When_ClaimRaces_Then_ExactlyOneSucceeds()
    {
        var tokenProvider = new CountingTokenProvider();
        using ServiceProvider services = CreateServices(tokenProvider);
        KafkaAuthenticationFactory factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        KafkaConfigurationFactory configurations = services.GetRequiredService<KafkaConfigurationFactory>();
        KafkaOAuthBearerSession session = factory.CreateSession(Identity(KafkaClientRole.LoadGenProducer), TestContext.Current.CancellationToken);
        using var barrier = new Barrier(2);

        Task<Exception?> producer = Task.Run(() => TryAttach(barrier, () => Attach(factory, configurations, session, true)));
        Task<Exception?> consumer = Task.Run(() => TryAttach(barrier, () => Attach(factory, configurations, session, false)));
        Exception?[] outcomes = await Task.WhenAll(producer, consumer);

        Assert.Null(Assert.Single(outcomes, outcome => outcome is null));
        Assert.IsType<InvalidOperationException>(Assert.Single(outcomes, outcome => outcome is not null));
        Assert.Equal(0, tokenProvider.Calls);
    }

    [Fact]
    public void Given_ProducerConfiguration_When_PureValidationRuns_Then_DoesNotAcquireOrAttachSession()
    {
        var tokenProvider = new CountingTokenProvider();
        using ServiceProvider services = CreateServices(tokenProvider);
        KafkaAuthenticationFactory factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        ProducerConfig configuration = services.GetRequiredService<KafkaConfigurationFactory>().CreateLoadGenProducer().Configuration;
        KafkaOAuthBearerSession session = factory.CreateSession(Identity(KafkaClientRole.LoadGenProducer), TestContext.Current.CancellationToken);

        factory.ValidateProducerConfiguration(configuration);
        factory.CreateProducerBuilder<Null, string>(configuration, session);

        Assert.Equal(0, tokenProvider.Calls);
    }

    private static Exception? TryAttach(Barrier barrier, Action attach)
    {
        barrier.SignalAndWait(TestContext.Current.CancellationToken);
        try
        {
            attach();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static void Attach(
        KafkaAuthenticationFactory factory,
        KafkaConfigurationFactory configurations,
        KafkaOAuthBearerSession session,
        bool producer)
    {
        if (producer)
            factory.CreateProducerBuilder<Null, string>(configurations.CreateLoadGenProducer().Configuration, session);
        else
            factory.CreateConsumerBuilder<Ignore, string>(configurations.CreateVerifierConsumer().Configuration, session);
    }

    private static ServiceProvider CreateServices(CountingTokenProvider tokenProvider)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaTokenProvider>(tokenProvider);
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-producer";
        });
        services.AddParchwellKafka(KafkaClientRole.VerifierConsumer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-consumer";
        });
        services.AddParchwellKafkaAuthentication(options =>
        {
            options.TenantId = "synthetic-tenant";
            options.ClientId = "synthetic-workload-client";
            options.TokenFilePath = "synthetic-token-file";
        });
        return services.BuildServiceProvider();
    }

    private static KafkaAuthenticationIdentity Identity(KafkaClientRole role) => new(
        "synthetic-scenario", "synthetic-run", role,
        role == KafkaClientRole.LoadGenProducer ? "synthetic-producer" : "synthetic-consumer");

    private sealed class CountingTokenProvider : IKafkaTokenProvider
    {
        private int _calls;
        internal int Calls => Volatile.Read(ref _calls);

        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return new AccessToken("synthetic-token", TimeProvider.System.GetUtcNow().AddHours(1));
        }
    }
}
