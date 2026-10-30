using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class KafkaAuthenticationLifecycleObservationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset E0 = new(2026, 10, 17, 13, 0, 0, TimeSpan.Zero);
    private static readonly KafkaAuthenticationIdentity Identity = new(
        "synthetic-scenario", "synthetic-run", KafkaClientRole.LoadGenProducer, "synthetic-kafka-client");

    [Fact]
    public void Given_DefaultSession_When_Refreshing_Then_CaptureIsDisabledAndAuthenticationIsUnchanged()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new QueueTokenProvider(new AccessToken("synthetic-token", E0));
        var clock = new FixedTimeProvider(Now);
        var session = NewSession(provider, metrics, clock);
        string? installed = null;

        session.Refresh((token, _, _) => installed = token, _ => Assert.Fail("Unexpected failure."));

        Assert.Equal("synthetic-token", installed);
        Assert.Equal(E0, session.InstalledTokenExpiresOn);
        KafkaAuthenticationLifecycleSnapshot snapshot = session.GetLifecycleSnapshot();
        Assert.False(snapshot.Enabled);
        Assert.False(snapshot.Complete);
        Assert.Equal("capture_disabled", snapshot.IncompleteReason);
        Assert.Empty(snapshot.Events);
        Assert.Equal(0, clock.ObservationReads);
    }

    [Fact]
    public void Given_ObservedSession_When_TokensReturnAndInstall_Then_EventsSeparateEachBoundary()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new QueueTokenProvider(
            new AccessToken("synthetic-token-a", E0),
            new AccessToken("synthetic-token-a", E0),
            new AccessToken("synthetic-token-b", new DateTimeOffset(2026, 10, 17, 14, 0, 0, TimeSpan.Zero)));
        var session = NewSession(provider, metrics, new FixedTimeProvider(Now), captureLifecycle: true);

        session.Refresh((_, _, _) => { }, _ => Assert.Fail("Unexpected failure."));
        session.Refresh((_, _, _) => { }, _ => Assert.Fail("Unexpected failure."));
        session.Refresh((_, _, _) => { }, _ => Assert.Fail("Unexpected failure."));

        KafkaAuthenticationLifecycleSnapshot snapshot = session.GetLifecycleSnapshot();
        Assert.True(snapshot.Enabled);
        Assert.True(snapshot.Complete);
        Assert.Null(snapshot.IncompleteReason);
        Assert.Equal(1000, snapshot.TimestampFrequency);
        Assert.Equal(9, snapshot.Events.Count);
        Assert.Equal("1,2,3,4,5,6,7,8,9", string.Join(',', snapshot.Events.Select(item => item.Sequence)));
        Assert.Equal("callback_started,acquisition_returned,installation_succeeded", string.Join(',', snapshot.Events.Take(3).Select(item => item.Stage)));
        Assert.Equal("initial,initial,initial,refresh,refresh,refresh,refresh,refresh,refresh", string.Join(',', snapshot.Events.Select(item => item.Phase)));
        Assert.Null(snapshot.Events[0].AcquiredExpiresOn);
        Assert.Equal(E0, snapshot.Events[1].AcquiredExpiresOn);
        Assert.Equal(E0, snapshot.Events[2].AcquiredExpiresOn);
        Assert.Null(snapshot.Events[0].InstalledExpiresOn);
        Assert.Null(snapshot.Events[1].InstalledExpiresOn);
        Assert.Equal(E0, snapshot.Events[2].InstalledExpiresOn);
        Assert.Equal(E0, snapshot.Events[5].InstalledExpiresOn);
        Assert.Equal(3, snapshot.Events[6].Attempt);
        Assert.Equal("refresh", snapshot.Events[6].Phase);
        Assert.Equal("callback_started", snapshot.Events[6].Stage);
        Assert.Null(snapshot.Events[6].AcquiredExpiresOn);
        Assert.Equal(E0, snapshot.Events[6].InstalledExpiresOn);
        Assert.Equal(3, snapshot.Events[7].Attempt);
        Assert.Equal("acquisition_returned", snapshot.Events[7].Stage);
        Assert.Equal(new DateTimeOffset(2026, 10, 17, 14, 0, 0, TimeSpan.Zero), snapshot.Events[7].AcquiredExpiresOn);
        Assert.Equal(E0, snapshot.Events[7].InstalledExpiresOn);
        Assert.Equal(3, snapshot.Events[8].Attempt);
        Assert.Equal("installation_succeeded", snapshot.Events[8].Stage);
        Assert.Equal(new DateTimeOffset(2026, 10, 17, 14, 0, 0, TimeSpan.Zero), snapshot.Events[8].AcquiredExpiresOn);
        Assert.Equal(new DateTimeOffset(2026, 10, 17, 14, 0, 0, TimeSpan.Zero), snapshot.Events[8].InstalledExpiresOn);
        Assert.IsAssignableFrom<IList<KafkaAuthenticationLifecycleEvent>>(snapshot.Events);
        Assert.Throws<NotSupportedException>(() => ((IList<KafkaAuthenticationLifecycleEvent>)snapshot.Events)[0] = snapshot.Events[1]);
        Assert.Equal("initial,initial,initial,refresh,refresh,refresh,refresh,refresh,refresh", string.Join(',', snapshot.Events.Select(item => item.Phase)));
        string json = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("synthetic-token-a", json, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-token-b", json, StringComparison.Ordinal);
        Assert.DoesNotContain("workload-principal-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-scope-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("exchange", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cache", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Given_InitialAndRefreshFailures_When_Observed_Then_OnlySanitizedReasonsAreRecordedAndExpiryIsPreserved()
    {
        const string isolatedRunId = "lifecycle-observation-metric-isolation";
        KafkaAuthenticationIdentity isolatedIdentity = Identity with { RunId = isolatedRunId };
        var measurements = new ConcurrentQueue<(long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == KafkaAuthenticationMetrics.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            Dictionary<string, object?> copiedTags = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value);
            if (Equals(copiedTags.GetValueOrDefault("run"), isolatedIdentity.RunId))
                measurements.Enqueue((value, copiedTags));
        });
        listener.Start();
        using var metrics = new KafkaAuthenticationMetrics();
        var initialProvider = new QueueTokenProvider(
            new InvalidOperationException("synthetic-exception-secret"),
            new AccessToken(string.Empty, E0),
            new AccessToken("synthetic-rejected-token", E0.AddHours(1)),
            new AccessToken("synthetic-installed-token", E0));
        var initialSession = NewSession(initialProvider, metrics, new FixedTimeProvider(Now), captureLifecycle: true, identity: isolatedIdentity);
        int failureReports = 0;

        initialSession.Refresh((_, _, _) => Assert.Fail("Acquisition failure must not install."), _ => failureReports++);
        initialSession.Refresh((_, _, _) => Assert.Fail("Empty token must not install."), _ => failureReports++);
        initialSession.Refresh((_, _, _) => throw new InvalidOperationException("synthetic-install-exception-secret"), _ => failureReports++);
        Assert.Equal(3, measurements.Count);
        KafkaAuthenticationFailure initialFailure = Assert.IsType<KafkaAuthenticationFailure>(initialSession.InitialFailure);
        initialSession.Refresh((_, _, _) => { }, _ => Assert.Fail("Valid token should install."));
        Assert.Same(initialFailure, initialSession.InitialFailure);
        Assert.Equal(E0, initialSession.InstalledTokenExpiresOn);
        Assert.Equal(3, measurements.Count);

        var refreshProvider = new QueueTokenProvider(
            new AccessToken("synthetic-first-token", E0),
            new InvalidOperationException("synthetic-refresh-exception-secret"),
            new AccessToken(string.Empty, E0.AddHours(1)),
            new AccessToken("synthetic-rejected-token", E0.AddHours(1)),
            new AccessToken("synthetic-replacement-token", E0.AddHours(1)));
        var refreshSession = NewSession(refreshProvider, metrics, new FixedTimeProvider(Now), captureLifecycle: true, identity: isolatedIdentity);
        refreshSession.Refresh((_, _, _) => { }, _ => Assert.Fail("Initial token should install."));
        refreshSession.Refresh((_, _, _) => Assert.Fail("Refresh acquisition failure must not install."), _ => failureReports++);
        refreshSession.Refresh((_, _, _) => Assert.Fail("Empty refresh token must not install."), _ => failureReports++);
        refreshSession.Refresh((_, _, _) => throw new InvalidOperationException("synthetic-refresh-install-secret"), _ => failureReports++);
        Assert.Equal(6, measurements.Count);
        Assert.Equal(E0, refreshSession.InstalledTokenExpiresOn);
        Assert.Null(refreshSession.InitialFailure);
        Assert.Equal("refresh", refreshSession.LatestFailure?.Phase);
        Assert.Equal("installation", refreshSession.LatestFailure?.Reason);
        refreshSession.Refresh((_, _, _) => { }, _ => Assert.Fail("Replacement should install."));

        KafkaAuthenticationLifecycleEvent[] initialFailures = initialSession.GetLifecycleSnapshot().Events
            .Where(item => item.Stage == "failed").ToArray();
        KafkaAuthenticationLifecycleEvent[] refreshFailures = refreshSession.GetLifecycleSnapshot().Events
            .Where(item => item.Stage == "failed").ToArray();
        Assert.Equal("acquisition,token_validation,installation", string.Join(',', initialFailures.Select(item => item.FailureReason)));
        Assert.Equal("acquisition,token_validation,installation", string.Join(',', refreshFailures.Select(item => item.FailureReason)));
        Assert.All(initialFailures, item => Assert.Equal("initial", item.Phase));
        Assert.All(refreshFailures, item =>
        {
            Assert.Equal("refresh", item.Phase);
            Assert.Equal(E0, item.InstalledExpiresOn);
        });
        Assert.Equal(6, failureReports);
        Assert.Equal("initial,initial,initial,refresh,refresh,refresh", string.Join(',', measurements.Select(item => item.Tags["phase"])));
        Assert.Equal("acquisition,token_validation,installation,acquisition,token_validation,installation", string.Join(',', measurements.Select(item => item.Tags["reason"])));
        Assert.All(measurements, item => Assert.Equal(1, item.Value));
        string initialJson = JsonSerializer.Serialize(initialSession.GetLifecycleSnapshot());
        Assert.DoesNotContain("synthetic-exception-secret", initialJson, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-install-exception-secret", initialJson, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-rejected-token", initialJson, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-installed-token", initialJson, StringComparison.Ordinal);
        Assert.DoesNotContain("workload-principal-secret", initialJson, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-scope-secret", initialJson, StringComparison.Ordinal);
        string json = JsonSerializer.Serialize(new[] { initialSession.GetLifecycleSnapshot(), refreshSession.GetLifecycleSnapshot() });
        Assert.DoesNotContain("exception-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("install-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-first-token", json, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-replacement-token", json, StringComparison.Ordinal);
        Assert.DoesNotContain("workload-principal-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-scope-secret", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(127, 381, null)]
    [InlineData(128, 384, null)]
    [InlineData(129, 384, "attempt_limit")]
    public void Given_CallbackCountAtAttemptBoundary_When_Observed_Then_CaptureIsBoundedAndHandlerContinues(
        int callbackCount, int expectedEvents, string? expectedIncompleteReason)
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new QueueTokenProvider(Enumerable.Repeat<object>(new AccessToken("synthetic-token", E0), callbackCount).ToArray());
        var session = NewSession(provider, metrics, new FixedTimeProvider(Now), captureLifecycle: true);
        int installed = 0;

        for (int i = 0; i < callbackCount; i++)
            session.Refresh((_, _, _) => installed++, _ => Assert.Fail("Unexpected failure."));

        KafkaAuthenticationLifecycleSnapshot snapshot = session.GetLifecycleSnapshot();
        Assert.Equal(expectedEvents, snapshot.Events.Count);
        Assert.Equal(expectedIncompleteReason, snapshot.IncompleteReason);
        Assert.Equal(expectedIncompleteReason is null, snapshot.Complete);
        Assert.Equal(callbackCount, installed);
        Assert.Equal(callbackCount, provider.Calls);
        Assert.Equal(Math.Min(callbackCount, 128), snapshot.Events.Select(item => item.Attempt).Distinct().Count());
        Assert.Equal(Math.Min(callbackCount, 128), snapshot.Events[snapshot.Events.Count - 1].Attempt);
    }

    [Fact]
    public void Given_AttemptLimitAndClockFailure_When_Observed_Then_AttemptLimitTakesPrecedence()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new QueueTokenProvider(Enumerable.Repeat<object>(new AccessToken("synthetic-token", E0), 129).ToArray());
        var session = NewSession(provider, metrics, new FirstClockReadThrows(Now), captureLifecycle: true);

        for (int i = 0; i < 129; i++)
            session.Refresh((_, _, _) => { }, _ => Assert.Fail("Unexpected failure."));

        Assert.Equal("attempt_limit", session.GetLifecycleSnapshot().IncompleteReason);
        Assert.Equal(384, session.GetLifecycleSnapshot().Events.Count);
    }

    [Fact]
    public void Given_ObservationClockThrows_When_CoreClockStillWorks_Then_CaptureIsIncompleteWithoutChangingHandler()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var clock = new FirstClockReadThrows(Now);
        var provider = new QueueTokenProvider(new AccessToken("synthetic-token", E0));
        var session = NewSession(provider, metrics, clock, captureLifecycle: true);

        session.Refresh((_, _, _) => { }, _ => Assert.Fail("Core validation should still succeed."));

        KafkaAuthenticationLifecycleSnapshot snapshot = session.GetLifecycleSnapshot();
        Assert.False(snapshot.Complete);
        Assert.Equal("clock_unavailable", snapshot.IncompleteReason);
        Assert.Null(snapshot.Events[0].ObservedAt);
        Assert.Null(snapshot.Events[0].Timestamp);
        Assert.All(snapshot.Events.Skip(1), item =>
        {
            Assert.NotNull(item.ObservedAt);
            Assert.NotNull(item.Timestamp);
        });
        Assert.Equal(E0, session.InstalledTokenExpiresOn);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public void Given_FactoryObservedProducerAndConsumerSessions_When_Attached_Then_CancellationAndOwnershipArePreserved()
    {
        using var services = CreateServices(new QueueTokenProvider(
            new AccessToken("synthetic-factory-token-secret", E0),
            new AccessToken("synthetic-factory-token-secret", E0),
            new InvalidOperationException("synthetic-factory-exception-secret")));
        KafkaAuthenticationFactory factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        KafkaConfigurationFactory configurations = services.GetRequiredService<KafkaConfigurationFactory>();
        using var cancellation = new CancellationTokenSource();
        KafkaOAuthBearerSession producer = factory.CreateObservedSession(Identity, cancellation.Token);
        KafkaOAuthBearerSession consumer = factory.CreateObservedSession(
            Identity with { Role = KafkaClientRole.VerifierConsumer }, cancellation.Token);
        KafkaOAuthBearerSession ordinary = factory.CreateSession(Identity, cancellation.Token);
        KafkaOAuthBearerSession failed = factory.CreateObservedSession(Identity, cancellation.Token);

        factory.CreateProducerBuilder<Null, string>(configurations.CreateLoadGenProducer().Configuration, producer);
        factory.CreateConsumerBuilder<Ignore, string>(configurations.CreateVerifierConsumer().Configuration, consumer);
        Assert.True(producer.GetLifecycleSnapshot().Complete);
        Assert.True(producer.GetLifecycleSnapshot().Enabled);
        Assert.True(consumer.GetLifecycleSnapshot().Enabled);
        producer.Refresh((_, _, _) => { }, _ => Assert.Fail("Unexpected producer failure."));
        consumer.Refresh((_, _, _) => { }, _ => Assert.Fail("Unexpected consumer failure."));
        failed.Refresh((_, _, _) => Assert.Fail("Factory acquisition failure must not install."), _ => { });
        Assert.Equal(cancellation.Token, Assert.IsType<QueueTokenProvider>(services.GetRequiredService<IKafkaTokenProvider>()).LastCancellation);
        Assert.Equal("synthetic-kafka-client", producer.GetLifecycleSnapshot().Identity.ClientId);
        Assert.Equal("capture_disabled", ordinary.GetLifecycleSnapshot().IncompleteReason);
        string producerJson = JsonSerializer.Serialize(producer.GetLifecycleSnapshot());
        string consumerJson = JsonSerializer.Serialize(consumer.GetLifecycleSnapshot());
        string failedJson = JsonSerializer.Serialize(failed.GetLifecycleSnapshot());
        foreach (string json in new[] { producerJson, consumerJson, failedJson })
        {
            Assert.DoesNotContain("synthetic-factory-token-secret", json, StringComparison.Ordinal);
            Assert.DoesNotContain("synthetic-factory-exception-secret", json, StringComparison.Ordinal);
            Assert.DoesNotContain("workload-principal-secret", json, StringComparison.Ordinal);
            Assert.DoesNotContain("https://storage.azure.com/.default", json, StringComparison.Ordinal);
        }
        Assert.Throws<InvalidOperationException>(() =>
            factory.CreateProducerBuilder<Null, string>(configurations.CreateLoadGenProducer().Configuration, producer));
    }

    [Fact]
    public void Given_EnabledSessionWithoutCallback_When_SnapshotTaken_Then_EmptySnapshotDoesNotInferSuccess()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        KafkaAuthenticationLifecycleSnapshot snapshot = NewSession(
            new QueueTokenProvider(), metrics, new FixedTimeProvider(Now), captureLifecycle: true).GetLifecycleSnapshot();

        Assert.True(snapshot.Enabled);
        Assert.True(snapshot.Complete);
        Assert.Null(snapshot.IncompleteReason);
        Assert.Empty(snapshot.Events);
    }

    private static KafkaOAuthBearerSession NewSession(
        IKafkaTokenProvider provider,
        KafkaAuthenticationMetrics metrics,
        TimeProvider clock,
        bool captureLifecycle = false,
        KafkaAuthenticationIdentity? identity = null) =>
        new(identity ?? Identity, "https://synthetic-scope-secret/.default", "workload-principal-secret",
            provider, clock, metrics, TestContext.Current.CancellationToken, captureLifecycle);

    private static ServiceProvider CreateServices(IKafkaTokenProvider tokenProvider)
    {
        var registrations = new ServiceCollection();
        registrations.AddSingleton<IKafkaTokenProvider>(tokenProvider);
        registrations.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-producer";
        });
        registrations.AddParchwellKafka(KafkaClientRole.VerifierConsumer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-consumer";
        });
        registrations.AddParchwellKafkaAuthentication(options =>
        {
            options.TenantId = "synthetic-tenant";
            options.ClientId = "workload-principal-secret";
            options.TokenFilePath = "synthetic-token-file";
            options.TokenScopeOverride = "https://storage.azure.com/.default";
        });
        return registrations.BuildServiceProvider();
    }

    private sealed class QueueTokenProvider(params object[] results) : IKafkaTokenProvider
    {
        private readonly Queue<object> _results = new(results);
        internal int Calls { get; private set; }
        internal CancellationToken LastCancellation { get; private set; }

        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            Calls++;
            LastCancellation = cancellationToken;
            return _results.Dequeue() switch
            {
                AccessToken token => token,
                Exception exception => throw exception,
                _ => throw new InvalidOperationException("Unsupported synthetic result.")
            };
        }
    }

    private class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private long _timestamp;
        internal int ObservationReads { get; private set; }
        public override long TimestampFrequency => 1000;
        public override DateTimeOffset GetUtcNow() => now;
        public override long GetTimestamp()
        {
            ObservationReads++;
            return ++_timestamp;
        }
    }

    private sealed class FirstClockReadThrows(DateTimeOffset now) : FixedTimeProvider(now)
    {
        private bool _thrown;
        public override DateTimeOffset GetUtcNow()
        {
            if (!_thrown)
            {
                _thrown = true;
                throw new InvalidOperationException("synthetic-clock-secret");
            }
            return base.GetUtcNow();
        }
    }
}
