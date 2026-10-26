using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class TokenScopeOverrideTests
{
    [Theory]
    [InlineData(null, "https://synthetic-namespace.servicebus.windows.net/.default")]
    [InlineData("https://storage.azure.com/.default", "https://storage.azure.com/.default")]
    public void Given_ApprovedScope_When_RefreshingFactorySession_Then_ForwardsExactScopeToProvider(string? scope, string expected)
    {
        var tokens = new RecordingTokens();
        var registrations = new ServiceCollection();
        registrations.AddSingleton<IKafkaTokenProvider>(tokens);
        registrations.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-client";
        });
        registrations.AddParchwellKafkaAuthentication(options =>
        {
            options.TenantId = "synthetic-tenant";
            options.ClientId = "synthetic-application";
            options.TokenFilePath = "synthetic.token";
            options.TokenScopeOverride = scope;
        });
        using var services = registrations.BuildServiceProvider();
        var factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        var session = factory.CreateSession(new("F04", "synthetic-run", KafkaClientRole.LoadGenProducer, "synthetic-client"), TestContext.Current.CancellationToken);
        session.Refresh((_, _, _) => { }, _ => Assert.Fail("Synthetic token should install."));
        Assert.Equal(expected, tokens.Scope);
        Assert.Equal(TestContext.Current.CancellationToken, tokens.Cancellation);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("https://storage.azure.com/")]
    [InlineData("https://storage.azure.com/.default ")]
    [InlineData("https://STORAGE.azure.com/.default")]
    [InlineData("https://synthetic-namespace.servicebus.windows.net/.default")]
    [InlineData("https://other.example/.default")]
    public void Given_AnyOtherOverride_When_Validating_Then_Rejects(string scope)
    {
        var result = new KafkaWorkloadIdentityOptionsValidator().Validate(Options.DefaultName,
            new() { TenantId = "tenant", ClientId = "application", TokenFilePath = "synthetic.token", TokenScopeOverride = scope });
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, error => error.Contains("TokenScopeOverride", StringComparison.Ordinal));
    }

    private sealed class RecordingTokens : IKafkaTokenProvider
    {
        internal string? Scope { get; private set; }
        internal CancellationToken Cancellation { get; private set; }
        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            Scope = scope;
            Cancellation = cancellationToken;
            return new("synthetic-token", TimeProvider.System.GetUtcNow().AddHours(1));
        }
    }
}
