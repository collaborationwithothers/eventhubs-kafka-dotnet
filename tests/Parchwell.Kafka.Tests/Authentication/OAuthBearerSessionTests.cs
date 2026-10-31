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

    [Fact]
    public void Given_RefreshPathCap_When_RefreshingCachedTokenTwice_Then_AdvertisesFreshAbsoluteExpiryEachTime()
    {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000);
        AccessToken cached = new("synthetic-cached-token", DateTimeOffset.FromUnixTimeMilliseconds(1700003600000));
        var provider = new ObservedStubTokenProvider(cached, cached);
        using var metrics = new KafkaAuthenticationMetrics();
        var clock = new MutableTimeProvider(now);
        var session = new KafkaOAuthBearerSession(Identity, "scope", "principal", provider, clock, metrics,
            TestContext.Current.CancellationToken, captureLifecycle: true, testExpiryCapMilliseconds: 5000);
        var expiries = new List<long>();

        session.Refresh((_, expiry, _) => expiries.Add(expiry), _ => Assert.Fail("unexpected failure"));
        clock.Now = now.AddMilliseconds(6000);
        session.Refresh((_, expiry, _) => expiries.Add(expiry), _ => Assert.Fail("unexpected failure"));

        Assert.Equal([1700000005000, 1700000011000], expiries);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000011000), session.InstalledTokenExpiresOn);
        var events = session.GetLifecycleSnapshot().Events;
        Assert.Equal(2, events.Count(value => value.Stage == "sdk_get_token_invoked"));
        Assert.Equal(2, events.Count(value => value.Stage == "sdk_get_token_returned"));
        Assert.Equal(2, events.Count(value => value.Stage == "installation_succeeded" &&
            value.AcquiredExpiresOn == cached.ExpiresOn && value.EffectiveExpiresOn is not null &&
            value.CapComputedAt is not null && value.TestExpiryCapMilliseconds == 5000));
    }

    [Fact]
    public void GivenCappedSessionWithoutObservedProvider_WhenRefreshing_ThenProgressDoesNotInferSdkStages()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var session = new KafkaOAuthBearerSession(Identity, "scope", "principal",
            new StubTokenProvider(new AccessToken("synthetic", Now.AddHours(1))), new FixedTimeProvider(Now), metrics,
            TestContext.Current.CancellationToken, captureLifecycle: true, testExpiryCapMilliseconds: 5000);

        session.Refresh((_, _, _) => { }, _ => Assert.Fail("unexpected failure"));

        Assert.Equal(new KafkaHandlerRefreshProgress(0, 0, null), session.HandlerRefreshProgress);
    }

    [Theory]
    [InlineData(1, 1700000000001)]
    [InlineData(3000, 1700000003000)]
    [InlineData(5000, 1700000005000)]
    public void GivenActualExpiryAtOrBeforeCap_When_Refreshing_Then_NeverExtendsSdkExpiry(int actualMilliseconds, long expected)
    {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000);
        var provider = new ObservedStubTokenProvider(new AccessToken("synthetic", DateTimeOffset.FromUnixTimeMilliseconds(expected)));
        using var metrics = new KafkaAuthenticationMetrics();
        var session = new KafkaOAuthBearerSession(Identity, "scope", "principal", provider,
            new MutableTimeProvider(now), metrics, TestContext.Current.CancellationToken,
            captureLifecycle: true, testExpiryCapMilliseconds: 5000);
        long installed = 0;

        session.Refresh((_, expiry, _) => installed = expiry, _ => Assert.Fail("unexpected failure"));

        Assert.Equal(expected, installed);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(expected), session.InstalledTokenExpiresOn);
    }

    [Fact]
    public void GivenActualExpiryExactlyNowInCappedMode_When_Refreshing_Then_ExistingValidationStillRejectsIt()
    {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000);
        using var metrics = new KafkaAuthenticationMetrics();
        var session = new KafkaOAuthBearerSession(Identity, "scope", "principal",
            new ObservedStubTokenProvider(new AccessToken("synthetic", now)), new MutableTimeProvider(now), metrics,
            TestContext.Current.CancellationToken, captureLifecycle: true, testExpiryCapMilliseconds: 5000);
        string? failure = null;

        session.Refresh((_, _, _) => Assert.Fail("expired token must not install"), value => failure = value);

        Assert.Equal("token_validation", failure);
        Assert.Null(session.InstalledTokenExpiresOn);
        Assert.Equal(new KafkaHandlerRefreshProgress(0, 0, null), session.HandlerRefreshProgress);
        KafkaAuthenticationLifecycleEvent failed = Assert.Single(session.GetLifecycleSnapshot().Events,
            value => value.Stage == "failed");
        Assert.Null(failed.EffectiveExpiresOn);
        Assert.Null(failed.CapComputedAt);
    }

    [Fact]
    public void GivenCappedInstallThenReplacementInstallThrows_When_Refreshing_ThenPreviousAdvertisedExpiryRemainsInstalled()
    {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000);
        using var metrics = new KafkaAuthenticationMetrics();
        var session = new KafkaOAuthBearerSession(Identity, "scope", "principal",
            new ObservedStubTokenProvider(
                new AccessToken("first", DateTimeOffset.FromUnixTimeMilliseconds(1700003600000)),
                new AccessToken("replacement", DateTimeOffset.FromUnixTimeMilliseconds(1700007200000))),
            new MutableTimeProvider(now), metrics, TestContext.Current.CancellationToken,
            captureLifecycle: true, testExpiryCapMilliseconds: 5000);
        session.Refresh((_, _, _) => { }, _ => Assert.Fail("first install must succeed"));
        DateTimeOffset firstInstalled = DateTimeOffset.FromUnixTimeMilliseconds(1700000005000);
        Assert.Equal(firstInstalled, session.InstalledTokenExpiresOn);

        session.Refresh((_, _, _) => throw new InvalidOperationException("synthetic native install failure"), _ => { });

        Assert.Equal(firstInstalled, session.InstalledTokenExpiresOn);
        KafkaAuthenticationLifecycleEvent failed = Assert.Single(session.GetLifecycleSnapshot().Events,
            value => value.Stage == "failed");
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700007200000), failed.AcquiredExpiresOn);
        Assert.Equal(firstInstalled, failed.InstalledExpiresOn);
        Assert.Equal(firstInstalled, failed.EffectiveExpiresOn);
        Assert.Equal(5000, failed.TestExpiryCapMilliseconds);
        Assert.Equal(new KafkaHandlerRefreshProgress(1, 1, null), session.HandlerRefreshProgress);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4999)]
    [InlineData(5001)]
    public void GivenUnsupportedRefreshPathCap_When_CreatingSession_Then_Rejects(int cap)
    {
        using var metrics = new KafkaAuthenticationMetrics();
        Assert.Throws<ArgumentOutOfRangeException>(() => new KafkaOAuthBearerSession(Identity, "scope", "principal",
            new ObservedStubTokenProvider(new AccessToken("synthetic", Now.AddHours(1))), new FixedTimeProvider(Now),
            metrics, TestContext.Current.CancellationToken, captureLifecycle: true, testExpiryCapMilliseconds: cap));
    }

    [Fact]
    public async Task GivenObservedSdkAcquisitionBlocks_WhenReadingRefreshProgress_ThenReadDoesNotWaitForSessionGate()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new ObservedStubTokenProvider((_, _, _) =>
        {
            entered.Set();
            release.Wait(TestContext.Current.CancellationToken);
            return new AccessToken("synthetic", Now.AddHours(1));
        });
        var session = new KafkaOAuthBearerSession(Identity, "scope", "principal", provider, new FixedTimeProvider(Now),
            metrics, TestContext.Current.CancellationToken, captureLifecycle: true, testExpiryCapMilliseconds: 5000);
        Task refresh = Task.Run(() => session.Refresh((_, _, _) => { }, _ => { }), TestContext.Current.CancellationToken);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Task<KafkaHandlerRefreshProgress> read = Task.Run(() => session.HandlerRefreshProgress, TestContext.Current.CancellationToken);
        try
        {
            Assert.Same(read, await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)));
            Assert.Equal(new KafkaHandlerRefreshProgress(0, 0, null), await read);
        }
        finally
        {
            release.Set();
            await refresh;
        }
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

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class ObservedStubTokenProvider(params object[] results) : IKafkaTokenProvider, IObservedKafkaTokenProvider
    {
        private readonly Queue<Func<string, CancellationToken, AccessToken>> queue = new(results.Select<object, Func<string, CancellationToken, AccessToken>>(result => result switch
        {
            AccessToken token => (_, _) => token,
            Func<string, CancellationToken, Action<string>, AccessToken> observed => (scope, cancellation) => observed(scope, cancellation, _ => { }),
            _ => throw new ArgumentException("Unsupported synthetic observed token result.", nameof(results))
        }));

        internal ObservedStubTokenProvider(Func<string, CancellationToken, Action<string>, AccessToken> result)
            : this((object)result) { }

        public AccessToken GetToken(string scope, CancellationToken cancellationToken) => queue.Dequeue()(scope, cancellationToken);

        public AccessToken GetToken(string scope, CancellationToken cancellationToken, Action<string> observe)
        {
            observe("sdk_get_token_invoked");
            AccessToken token = queue.Dequeue()(scope, cancellationToken);
            observe("sdk_get_token_returned");
            return token;
        }
    }
}
