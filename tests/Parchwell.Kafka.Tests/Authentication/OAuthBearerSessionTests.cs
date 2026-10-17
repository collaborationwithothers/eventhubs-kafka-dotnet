using Azure.Core;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class OAuthBearerSessionTests
{
    private static readonly string[] AcquisitionReasons = ["acquisition"];
    private static readonly KafkaAuthenticationIdentity Identity = new(
        "synthetic-scenario", "synthetic-run", KafkaClientRole.LoadGenProducer, "synthetic-kafka-client");
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_ValidToken_When_Refreshing_Then_InstallsUnchangedTokenAndExpiry()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new StubTokenProvider(new AccessToken("opaque-token", Now.AddHours(1)));
        var session = NewSession(provider, metrics, TestContext.Current.CancellationToken);
        string? installedToken = null;
        long installedExpiry = 0;
        string? installedPrincipal = null;

        session.Refresh((token, expiry, principal) =>
        {
            installedToken = token;
            installedExpiry = expiry;
            installedPrincipal = principal;
        }, _ => throw new InvalidOperationException("unexpected failure"));

        Assert.Equal("opaque-token", installedToken);
        Assert.Equal(Now.AddHours(1).ToUnixTimeMilliseconds(), installedExpiry);
        Assert.Equal("workload-application-client", installedPrincipal);
        Assert.Equal(Now.AddHours(1), session.InstalledTokenExpiresOn);
        Assert.Null(session.InitialFailure);
        Assert.Null(session.LatestFailure);
        Assert.False(session.InitialFailureTask.IsCompleted);
        Assert.Equal("https://namespace.servicebus.windows.net/.default", provider.Scope);
    }

    [Fact]
    public async Task Given_InitialAcquisitionFailureThenSuccess_When_Refreshing_Then_InitialFailureStaysLatched()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new StubTokenProvider(new InvalidOperationException("sensitive credential text"),
            new AccessToken("later-token", Now.AddHours(1)));
        var session = NewSession(provider, metrics, TestContext.Current.CancellationToken);
        var reported = new List<string>();

        session.Refresh((_, _, _) => throw new InvalidOperationException("must not install"), reported.Add);
        KafkaAuthenticationFailure initialFailure = Assert.IsType<KafkaAuthenticationFailure>(session.InitialFailure);
        Assert.Same(initialFailure, await session.InitialFailureTask);
        session.Refresh((_, _, _) => { }, reported.Add);

        Assert.Equal("initial", initialFailure.Phase);
        Assert.Equal("acquisition", initialFailure.Reason);
        Assert.Same(initialFailure, session.InitialFailure);
        Assert.Equal(Now.AddHours(1), session.InstalledTokenExpiresOn);
        Assert.Equal(initialFailure, session.LatestFailure);
        Assert.Equal(AcquisitionReasons, reported);
    }

    [Fact]
    public async Task Given_InitialInstallationFailureThenSuccess_When_Refreshing_Then_InitialFailureStaysLatched()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new StubTokenProvider(new AccessToken("first-token", Now.AddHours(1)),
            new AccessToken("later-token", Now.AddHours(2)));
        var session = NewSession(provider, metrics, TestContext.Current.CancellationToken);

        session.Refresh((_, _, _) => throw new InvalidOperationException("native detail"), _ => { });
        session.Refresh((_, _, _) => { }, _ => { });

        Assert.Equal("initial", session.InitialFailure?.Phase);
        Assert.Equal("installation", session.InitialFailure?.Reason);
        Assert.Same(session.InitialFailure, await session.InitialFailureTask);
        Assert.Equal(Now.AddHours(2), session.InstalledTokenExpiresOn);
        Assert.Same(session.InitialFailure, session.LatestFailure);
    }

    [Fact]
    public void Given_ValidCachedToken_When_RefreshingAgain_Then_ExpiryIsNotExtended()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new StubTokenProvider(new AccessToken("cached-token", Now.AddHours(1)),
            new AccessToken("cached-token", Now.AddHours(1)));
        var session = NewSession(provider, metrics, TestContext.Current.CancellationToken);

        session.Refresh((_, _, _) => { }, _ => { });
        session.Refresh((_, _, _) => { }, _ => { });

        Assert.Equal(Now.AddHours(1), session.InstalledTokenExpiresOn);
        Assert.Null(session.LatestFailure);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("expired")]
    [InlineData("exact_now")]
    public async Task Given_InvalidToken_When_Refreshing_Then_ReportsSanitizedValidationFailure(string tokenKind)
    {
        using var metrics = new KafkaAuthenticationMetrics();
        AccessToken token = tokenKind switch
        {
            "empty" => new AccessToken(string.Empty, Now.AddHours(1)),
            "expired" => new AccessToken("opaque-token", Now.AddTicks(-1)),
            _ => new AccessToken("opaque-token", Now)
        };
        var session = NewSession(new StubTokenProvider(token,
            new AccessToken("valid-replacement", Now.AddHours(2))), metrics, TestContext.Current.CancellationToken);
        string? reported = null;

        session.Refresh((_, _, _) => throw new InvalidOperationException("must not install"), value => reported = value);

        Assert.Equal("token_validation", reported);
        Assert.Equal("initial", session.InitialFailure?.Phase);
        Assert.Equal("token_validation", session.InitialFailure?.Reason);
        Assert.Same(session.InitialFailure, await session.InitialFailureTask);
        Assert.Null(session.InstalledTokenExpiresOn);

        KafkaAuthenticationFailure initialFailure = await session.InitialFailureTask;
        session.Refresh((_, _, _) => { }, _ => { });

        Assert.Equal(Now.AddHours(2), session.InstalledTokenExpiresOn);
        Assert.Same(initialFailure, session.InitialFailure);
        Assert.Same(initialFailure, await session.InitialFailureTask);
    }

    [Fact]
    public void Given_InstalledTokenThenFailedReplacement_When_Refreshing_Then_PreservesExpiryAndRefreshPhase()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new StubTokenProvider(new AccessToken("first-token", Now.AddHours(1)),
            new AccessToken("replacement-token", Now.AddHours(2)));
        var session = NewSession(provider, metrics, TestContext.Current.CancellationToken);
        session.Refresh((_, _, _) => { }, _ => { });
        string? reported = null;

        session.Refresh((_, _, _) => throw new InvalidOperationException("native detail"), value => reported = value);

        Assert.Equal("installation", reported);
        Assert.Equal(Now.AddHours(1), session.InstalledTokenExpiresOn);
        Assert.Null(session.InitialFailure);
        Assert.Equal("refresh", session.LatestFailure?.Phase);
        Assert.Equal("installation", session.LatestFailure?.Reason);
        Assert.False(session.InitialFailureTask.IsCompleted);
    }

    [Theory]
    [InlineData("token_validation")]
    [InlineData("installation")]
    public void Given_InstalledTokenThenReplacementFailure_When_RetrySucceeds_Then_PreservesExpiryUntilInstallation(string reason)
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new StubTokenProvider(new AccessToken("synthetic-first", Now.AddHours(1)),
            new AccessToken(reason == "token_validation" ? string.Empty : "synthetic-rejected", Now.AddHours(2)),
            new AccessToken("synthetic-replacement", Now.AddHours(3)));
        var session = NewSession(provider, metrics, TestContext.Current.CancellationToken);
        session.Refresh((_, _, _) => { }, _ => { });

        session.Refresh((_, _, _) => throw new InvalidOperationException("synthetic-install-failure"), _ => { });

        Assert.Equal(Now.AddHours(1), session.InstalledTokenExpiresOn);
        Assert.Null(session.InitialFailure);
        Assert.Equal("refresh", session.LatestFailure?.Phase);
        Assert.Equal(reason, session.LatestFailure?.Reason);
        Assert.False(session.InitialFailureTask.IsCompleted);

        session.Refresh((_, _, _) => { }, _ => { });

        Assert.Equal(Now.AddHours(3), session.InstalledTokenExpiresOn);
        Assert.Null(session.InitialFailure);
    }

    [Fact]
    public void Given_InstalledTokenThenAcquisitionFailure_When_Refreshing_Then_PreservesStateUntilReplacementSucceeds()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new StubTokenProvider(new AccessToken("first-token", Now.AddHours(1)),
            new InvalidOperationException("sensitive credential text"),
            new AccessToken("replacement-token", Now.AddHours(2)));
        var session = NewSession(provider, metrics, TestContext.Current.CancellationToken);
        var reported = new List<string>();
        session.Refresh((_, _, _) => { }, reported.Add);

        session.Refresh((_, _, _) => throw new InvalidOperationException("must not install"), reported.Add);
        KafkaAuthenticationFailure refreshFailure = Assert.IsType<KafkaAuthenticationFailure>(session.LatestFailure);
        Assert.Null(session.InitialFailure);
        Assert.Equal(Now.AddHours(1), session.InstalledTokenExpiresOn);

        session.Refresh((_, _, _) => { }, reported.Add);

        Assert.Same(refreshFailure, session.LatestFailure);
        Assert.Null(session.InitialFailure);
        Assert.Equal(Now.AddHours(2), session.InstalledTokenExpiresOn);
        Assert.False(session.InitialFailureTask.IsCompleted);
        Assert.Equal(AcquisitionReasons, reported);
    }

    [Fact]
    public void Given_CancellationDuringAcquisition_When_Refreshing_Then_RecordsOneSanitizedInitialFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var metrics = new KafkaAuthenticationMetrics();
        CancellationToken observedCancellation = default;
        var provider = new StubTokenProvider((_, token) =>
        {
            observedCancellation = token;
            throw new OperationCanceledException("sensitive cancellation detail", token);
        });
        var session = NewSession(provider, metrics, cancellation.Token);
        var reports = new List<string>();

        session.Refresh((_, _, _) => throw new InvalidOperationException("must not install"), reports.Add);

        Assert.Equal(cancellation.Token, observedCancellation);
        Assert.Equal("initial", session.InitialFailure?.Phase);
        Assert.Equal("acquisition", session.InitialFailure?.Reason);
        Assert.Equal(AcquisitionReasons, reports);
    }

    [Fact]
    public async Task Given_FailureReporterThrows_When_Refreshing_Then_CallbackReturnsWithOneFailureState()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new StubTokenProvider(new InvalidOperationException("secret"));
        var session = NewSession(provider, metrics, TestContext.Current.CancellationToken);
        bool notificationCompletedBeforeReport = false;

        session.Refresh((_, _, _) => { }, _ =>
        {
            notificationCompletedBeforeReport = session.InitialFailureTask.IsCompletedSuccessfully;
            throw new InvalidOperationException("native error");
        });

        Assert.Equal("acquisition", session.InitialFailure?.Reason);
        Assert.Same(session.InitialFailure, session.LatestFailure);
        Assert.Same(session.InitialFailure, await session.InitialFailureTask);
        Assert.True(notificationCompletedBeforeReport);
    }

    private static KafkaOAuthBearerSession NewSession(
        StubTokenProvider provider,
        KafkaAuthenticationMetrics metrics,
        CancellationToken cancellationToken) =>
        new(Identity, "https://namespace.servicebus.windows.net/.default", "workload-application-client",
            provider, new FixedTimeProvider(Now), metrics, cancellationToken);

    private sealed class StubTokenProvider : IKafkaTokenProvider
    {
        private readonly Queue<Func<string, CancellationToken, AccessToken>> _results;

        internal StubTokenProvider(params object[] results)
        {
            _results = new Queue<Func<string, CancellationToken, AccessToken>>(results.Select<object, Func<string, CancellationToken, AccessToken>>(result => result switch
            {
                AccessToken token => (_, _) => token,
                Exception exception => (_, _) => throw exception,
                _ => throw new ArgumentException("Unsupported synthetic token result.", nameof(results))
            }));
        }

        internal StubTokenProvider(Func<string, CancellationToken, AccessToken> result) =>
            _results = new Queue<Func<string, CancellationToken, AccessToken>>([result]);

        internal string? Scope { get; private set; }

        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            Scope = scope;
            return _results.Dequeue()(scope, cancellationToken);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
