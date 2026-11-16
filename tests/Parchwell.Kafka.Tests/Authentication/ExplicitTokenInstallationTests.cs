using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707

// The offset reader needs the real native token installer without calling Consume.
public sealed class ExplicitTokenInstallationTests
{
    [Fact]
    public void Given_AttachedConsumer_When_InstallingWithoutPolling_Then_UsesExistingProviderAndSingleClientGuard()
    {
        var provider = new SyntheticProvider();
        using ServiceProvider services = CreateServices(provider);
        var factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        KafkaOAuthBearerSession session = factory.CreateSession(Identity, TestContext.Current.CancellationToken);
        ConsumerConfig config = Configuration(services);
        ConsumerBuilder<Ignore, string> builder = factory.CreateConsumerBuilder<Ignore, string>(config, session);
        using IConsumer<Ignore, string> first = builder.Build();
        using IConsumer<Ignore, string> second = builder.Build();

        session.InstallToken(first);

        Assert.Equal(1, provider.Calls);
        Assert.Equal(provider.ExpiresOn, session.InstalledTokenExpiresOn);
        Assert.Equal(TestContext.Current.CancellationToken, provider.ObservedCancellation);
        Assert.Equal("https://synthetic-namespace.servicebus.windows.net/.default", provider.Scope);
        Assert.Null(session.InitialFailure);
        Assert.Null(session.LatestFailure);
        Assert.Empty(first.Subscription);
        Assert.Empty(first.Assignment);
        Assert.Throws<InvalidOperationException>(() => session.InstallToken(second));
        Assert.Equal(1, provider.Calls);
        session.InstallToken(first);
        Assert.Equal(2, provider.Calls);
        Assert.Equal(provider.ExpiresOn, session.InstalledTokenExpiresOn);
    }

    [Fact]
    public void Given_UnattachedSessionOrNullClient_When_Installing_Then_RejectsBeforeAcquisition()
    {
        var provider = new SyntheticProvider();
        using ServiceProvider services = CreateServices(provider);
        var factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        KafkaOAuthBearerSession session = factory.CreateSession(Identity, TestContext.Current.CancellationToken);
        using IConsumer<Ignore, string> client = new ConsumerBuilder<Ignore, string>(
            new ConsumerConfig { GroupId = "synthetic-unattached", BootstrapServers = "127.0.0.1:1" }).Build();

        Assert.Throws<ArgumentNullException>(() => session.InstallToken(null!));
        Assert.Throws<InvalidOperationException>(() => session.InstallToken(client));
        Assert.Equal(0, provider.Calls);
        Assert.Null(session.InitialFailure);
        Assert.Null(session.InstalledTokenExpiresOn);
    }

    [Theory]
    [InlineData("acquisition")]
    [InlineData("expired")]
    [InlineData("cancelled")]
    public void Given_FailedAcquisition_When_Installing_Then_PreservesSanitizedStickyFailure(string failure)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var provider = new SyntheticProvider { Failure = failure };
        using ServiceProvider services = CreateServices(provider);
        var factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        KafkaOAuthBearerSession session = factory.CreateSession(Identity, cancellation.Token);
        using IConsumer<Ignore, string> client = factory.CreateConsumerBuilder<Ignore, string>(Configuration(services), session).Build();
        if (failure == "cancelled") cancellation.Cancel();

        session.InstallToken(client);

        Assert.Null(session.InstalledTokenExpiresOn);
        KafkaAuthenticationFailure initial = Assert.IsType<KafkaAuthenticationFailure>(session.InitialFailure);
        Assert.Equal("initial", initial.Phase);
        Assert.Equal(failure == "expired" ? "token_validation" : "acquisition", initial.Reason);
        Assert.Equal(initial, session.LatestFailure);
        Assert.DoesNotContain("synthetic-secret", initial.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, provider.Calls);
        if (failure != "cancelled")
        {
            provider.Failure = null;
            session.InstallToken(client);
            Assert.Equal(provider.ExpiresOn, session.InstalledTokenExpiresOn);
            Assert.Same(initial, session.InitialFailure);
        }
    }

    private static readonly KafkaAuthenticationIdentity Identity = new(
        "synthetic-scenario", "synthetic-explicit-install", KafkaClientRole.VerifierConsumer, "synthetic-consumer");

    private static ConsumerConfig Configuration(ServiceProvider services)
    {
        ConsumerConfig config = services.GetRequiredService<KafkaConfigurationFactory>().CreateVerifierConsumer().Configuration;
        config.BootstrapServers = "127.0.0.1:1";
        return config;
    }

    private static ServiceProvider CreateServices(SyntheticProvider provider)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaTokenProvider>(provider);
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

    private sealed class SyntheticProvider : IKafkaTokenProvider
    {
        internal int Calls { get; private set; }
        internal string? Failure { get; set; }
        internal string? Scope { get; private set; }
        internal CancellationToken ObservedCancellation { get; private set; }
        internal DateTimeOffset ExpiresOn { get; } = TimeProvider.System.GetUtcNow().AddHours(1);

        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            Calls++;
            Scope = scope;
            ObservedCancellation = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure == "acquisition") throw new InvalidOperationException("synthetic-secret");
            return new AccessToken("synthetic-token", Failure == "expired" ? DateTimeOffset.UnixEpoch : ExpiresOn);
        }
    }
}
