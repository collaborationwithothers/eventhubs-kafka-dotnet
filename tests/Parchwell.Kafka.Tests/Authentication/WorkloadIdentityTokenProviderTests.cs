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

    [Fact]
    public void Given_ObservedCalls_When_GettingCachedTokenTwice_Then_RecordsSdkBoundariesWithoutChangingResponse()
    {
        var expected = new AccessToken("synthetic-token", DateTimeOffset.UnixEpoch.AddHours(1));
        var credential = new RecordingCredential(expected);
        var provider = new WorkloadIdentityTokenProvider(credential);
        var events = new List<string>();

        AccessToken first = ((IObservedKafkaTokenProvider)provider).GetToken("scope", CancellationToken.None, events.Add);
        AccessToken second = ((IObservedKafkaTokenProvider)provider).GetToken("scope", CancellationToken.None, events.Add);

        Assert.Equal(expected, first);
        Assert.Equal(expected, second);
        Assert.Equal(["sdk_get_token_invoked", "sdk_get_token_returned", "sdk_get_token_invoked", "sdk_get_token_returned"], events);
        Assert.Equal(2, credential.Calls);
    }

    [Fact]
    public void Given_ObservedAcquisitionThrows_When_GettingToken_Then_RecordsInvocationOnlyAndPropagatesSameFailure()
    {
        var failure = new InvalidOperationException("synthetic credential failure");
        var credential = new RecordingCredential(failure);
        var provider = new WorkloadIdentityTokenProvider(credential);
        var events = new List<string>();

        InvalidOperationException actual = Assert.Throws<InvalidOperationException>(() =>
            ((IObservedKafkaTokenProvider)provider).GetToken("scope", CancellationToken.None, events.Add));

        Assert.Same(failure, actual);
        Assert.Equal(["sdk_get_token_invoked"], events);
    }

    [Fact]
    public void GivenObserverThrows_When_GettingToken_Then_SdkCallAndResponseAreUnchanged()
    {
        var expected = new AccessToken("synthetic-token", DateTimeOffset.UnixEpoch.AddHours(1));
        var credential = new RecordingCredential(expected);
        var provider = new WorkloadIdentityTokenProvider(credential);

        AccessToken actual = ((IObservedKafkaTokenProvider)provider).GetToken("scope", CancellationToken.None,
            _ => throw new InvalidOperationException("observer failure"));

        Assert.Equal(expected, actual);
        Assert.Equal(1, credential.Calls);
    }

    private sealed class RecordingCredential : TokenCredential
    {
        private readonly AccessToken token;
        private readonly Exception? failure;
        private int calls;
        internal RecordingCredential(AccessToken token) => this.token = token;
        internal RecordingCredential(Exception failure) => this.failure = failure;
        internal int Calls => Volatile.Read(ref calls);
        internal TokenRequestContext? Context { get; private set; }
        internal CancellationToken CancellationToken { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            Context = requestContext;
            CancellationToken = cancellationToken;
            if (failure is not null) throw failure;
            return token;
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromException<AccessToken>(new InvalidOperationException("Async acquisition was not expected."));
    }
}
