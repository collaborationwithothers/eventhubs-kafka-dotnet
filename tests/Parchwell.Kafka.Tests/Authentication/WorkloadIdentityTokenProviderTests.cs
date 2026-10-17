using Azure.Core;
using Parchwell.Kafka.Authentication;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class WorkloadIdentityTokenProviderTests
{
    [Fact]
    public void Given_ScopeAndCancellation_When_GettingToken_Then_DelegatesSynchronouslyAndReturnsSdkToken()
    {
        var expiry = DateTimeOffset.UnixEpoch.AddHours(1);
        var expected = new AccessToken("synthetic-token", expiry);
        var credential = new RecordingCredential(expected);
        var provider = new WorkloadIdentityTokenProvider(credential);
        using var source = new CancellationTokenSource();

        AccessToken actual = provider.GetToken("https://synthetic.servicebus.windows.net/.default", source.Token);

        Assert.Equal(expected.Token, actual.Token);
        Assert.Equal(expiry, actual.ExpiresOn);
        Assert.Equal("https://synthetic.servicebus.windows.net/.default", Assert.Single(credential.Context!.Value.Scopes));
        Assert.Equal(source.Token, credential.CancellationToken);
    }

    private sealed class RecordingCredential(AccessToken token) : TokenCredential
    {
        internal TokenRequestContext? Context { get; private set; }
        internal CancellationToken CancellationToken { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Context = requestContext;
            CancellationToken = cancellationToken;
            return token;
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromException<AccessToken>(new InvalidOperationException("Async acquisition was not expected."));
    }
}
