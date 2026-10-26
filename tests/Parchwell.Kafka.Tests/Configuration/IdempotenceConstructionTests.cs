using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Configuration;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Native")]
public sealed class IdempotenceConstructionTests
{
    [Fact]
    public void Given_IdempotenceWithMaxInFlightFive_When_Building_Then_NativeProducerIsCreated()
    {
        using var services = CreateServices();
        var factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        var configuration = services.GetRequiredService<KafkaConfigurationFactory>().CreateLoadGenProducer().Configuration;
        configuration.BootstrapServers = "127.0.0.1:1";
        configuration.Set("enable.idempotence", "true");
        configuration.Set("max.in.flight", "5");
        var session = factory.CreateSession(Identity(), TestContext.Current.CancellationToken);
        var builder = factory.CreateProducerBuilder<Null, string>(configuration, session);

        using var producer = builder.Build();

        Assert.NotNull(producer);
    }

    [Fact]
    public void Given_IdempotenceWithBaselineMaxInFlight_When_Building_Then_NativeProducerIsRejected()
    {
        using var services = CreateServices();
        var factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        var configuration = services.GetRequiredService<KafkaConfigurationFactory>().CreateLoadGenProducer().Configuration;
        configuration.BootstrapServers = "127.0.0.1:1";
        configuration.Set("enable.idempotence", "true");
        var session = factory.CreateSession(Identity(), TestContext.Current.CancellationToken);
        var builder = factory.CreateProducerBuilder<Null, string>(configuration, session);

        Exception? error = Record.Exception(() => builder.Build().Dispose());

        Assert.NotNull(error);
        Assert.Contains("`max.in.flight` must be set <= 5 when `enable.idempotence` is true", error.ToString(), StringComparison.Ordinal);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKafkaTokenProvider>(new SyntheticTokenProvider());
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-producer";
        });
        services.AddParchwellKafkaAuthentication(options =>
        {
            options.TenantId = "synthetic-tenant";
            options.ClientId = "synthetic-workload-client";
            options.TokenFilePath = "synthetic-token-file";
        });
        return services.BuildServiceProvider();
    }

    private static KafkaAuthenticationIdentity Identity() =>
        new("synthetic-scenario", "synthetic-native-client-run", KafkaClientRole.LoadGenProducer, "synthetic-producer");

    private sealed class SyntheticTokenProvider : IKafkaTokenProvider
    {
        public AccessToken GetToken(string scope, CancellationToken cancellationToken) =>
            new("header.payload.signature", TimeProvider.System.GetUtcNow().AddHours(1));
    }
}
