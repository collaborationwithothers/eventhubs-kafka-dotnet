using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class KafkaAuthenticationDependencyInjectionTests
{
    [Fact]
    public void Given_ValidIdentityOptionsAndSyntheticProvider_When_HostStarts_Then_FactoryCreatesSession()
    {
        var services = new ServiceCollection();
        var tokenProvider = new SyntheticTokenProvider();
        services.AddSingleton<IKafkaTokenProvider>(tokenProvider);
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-kafka-client";
        });
        services.AddParchwellKafkaAuthentication(options =>
        {
            options.TenantId = "synthetic-tenant";
            options.ClientId = "synthetic-workload-client";
            options.TokenFilePath = "synthetic-token-file";
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStartupValidator>().Validate();
        KafkaAuthenticationFactory factory = provider.GetRequiredService<KafkaAuthenticationFactory>();
        KafkaOAuthBearerSession session = factory.CreateSession(new KafkaAuthenticationIdentity(
            "synthetic-scenario", "synthetic-run", KafkaClientRole.LoadGenProducer, "synthetic-kafka-client"),
            TestContext.Current.CancellationToken);

        Assert.Null(session.InstalledTokenExpiresOn);
        Assert.Null(tokenProvider.Scope);
        session.Refresh((_, _, _) => { }, _ => { });
        Assert.NotNull(session.InstalledTokenExpiresOn);
        Assert.Equal("https://synthetic-namespace.servicebus.windows.net/.default", tokenProvider.Scope);

        KafkaAuthenticationIdentity identity = new("synthetic-scenario", "synthetic-run", KafkaClientRole.LoadGenProducer, "synthetic-kafka-client");
        KafkaOAuthBearerSession refreshPath = factory.CreateRefreshPathTestSession(identity, 5000, TestContext.Current.CancellationToken);
        refreshPath.Refresh((_, _, _) => { }, _ => Assert.Fail("synthetic test token must install"));
        Assert.True(refreshPath.InstalledTokenExpiresOn <= TimeProvider.System.GetUtcNow().AddMilliseconds(5000));
        foreach (int cap in new[] { 0, -1, 4999, 5001 })
            Assert.Throws<ArgumentOutOfRangeException>(() => factory.CreateRefreshPathTestSession(identity, cap));
    }

    [Fact]
    public void Given_ExplicitWorkloadIdentityWithUnreadTokenPath_When_HostStarts_Then_RegistersCredentialWithoutAcquisition()
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-kafka-client";
        });
        services.AddParchwellKafkaAuthentication(options =>
        {
            options.TenantId = "synthetic-tenant";
            options.ClientId = "synthetic-workload-client";
            options.TokenFilePath = "synthetic-nonexistent-token-file";
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.IsType<WorkloadIdentityTokenProvider>(provider.GetRequiredService<IKafkaTokenProvider>());
        var factory = provider.GetRequiredService<KafkaAuthenticationFactory>();
        KafkaOAuthBearerSession session = factory.CreateSession(new KafkaAuthenticationIdentity(
            "synthetic-scenario", "synthetic-run", KafkaClientRole.LoadGenProducer, "synthetic-kafka-client"),
            TestContext.Current.CancellationToken);

        Assert.Null(session.InstalledTokenExpiresOn);
        Assert.Null(session.InitialFailure);
    }

    [Fact]
    public void Given_MissingWorkloadIdentityOption_When_HostStarts_Then_ValidationFails()
    {
        var services = new ServiceCollection();
        services.AddParchwellKafkaAuthentication(_ => { });
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    private sealed class SyntheticTokenProvider : IKafkaTokenProvider
    {
        public string? Scope { get; private set; }

        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            Scope = scope;
            return new AccessToken("synthetic-token", TimeProvider.System.GetUtcNow().AddHours(1));
        }
    }
}
