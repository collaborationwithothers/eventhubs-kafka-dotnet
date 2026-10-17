using System.Diagnostics;
using System.Diagnostics.Metrics;
using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Native")]
public sealed class KafkaOAuthBearerNativeClientTests
{
    [Fact]
    public void Given_ProducerBuilderWithSession_When_NativeClientPolls_Then_RealTokenInstallSucceeds()
    {
        var tokenProvider = new SyntheticTokenProvider(() => new AccessToken(
            "header.payload.signature", TimeProvider.System.GetUtcNow().AddHours(1)));
        using ServiceProvider services = CreateServices(tokenProvider);
        KafkaAuthenticationFactory factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        ProducerConfig configuration = services.GetRequiredService<KafkaConfigurationFactory>()
            .CreateLoadGenProducer().Configuration;
        configuration.BootstrapServers = "127.0.0.1:1";
        KafkaOAuthBearerSession session = factory.CreateSession(Identity(KafkaClientRole.LoadGenProducer), TestContext.Current.CancellationToken);

        ProducerBuilder<Null, string> builder = factory.CreateProducerBuilder<Null, string>(configuration, session);
        configuration.Set("sasl.mechanism", "PLAIN");
        TaskCompletionSource<Error> nativeFailure = NewNativeFailureSignal();
        builder.SetErrorHandler((_, error) => CaptureAuthenticationFailure(nativeFailure, error));
        using IProducer<Null, string> producer = builder.Build();

        Assert.True(WaitUntil(() => session.InstalledTokenExpiresOn is not null, TimeSpan.FromSeconds(15)),
            "The native producer did not invoke and complete its attached token refresh callback.");
        Assert.Null(session.InitialFailure);
        Assert.Null(session.LatestFailure);
        Assert.Equal(1, tokenProvider.Calls);
        Assert.False(nativeFailure.Task.IsCompleted);
    }

    [Fact]
    public async Task Given_ConsumerBuilderWithSession_When_ConsumerPolls_Then_RefreshFailureIsReportedWithoutStickyFailure()
    {
        DateTimeOffset? expiry = null;
        var tokenProvider = new SyntheticTokenProvider(
            () =>
            {
                expiry = TimeProvider.System.GetUtcNow().AddSeconds(15);
                return new AccessToken("header.payload.signature", expiry.Value);
            },
            () => throw new InvalidOperationException("synthetic consumer credential secret"));
        using ServiceProvider services = CreateServices(tokenProvider);
        KafkaAuthenticationFactory factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        ConsumerConfig configuration = services.GetRequiredService<KafkaConfigurationFactory>()
            .CreateVerifierConsumer().Configuration;
        configuration.BootstrapServers = "127.0.0.1:1";
        KafkaOAuthBearerSession session = factory.CreateSession(Identity(KafkaClientRole.VerifierConsumer), TestContext.Current.CancellationToken);

        ConsumerBuilder<Ignore, string> builder = factory.CreateConsumerBuilder<Ignore, string>(configuration, session);
        configuration.Set("sasl.mechanism", "PLAIN");
        TaskCompletionSource<Error> nativeFailure = NewNativeFailureSignal();
        builder.SetErrorHandler((_, error) => CaptureAuthenticationFailure(nativeFailure, error));
        using IConsumer<Ignore, string> consumer = builder.Build();

        Assert.True(WaitUntil(() =>
        {
            consumer.Consume(TimeSpan.FromMilliseconds(50));
            return session.LatestFailure?.Phase == "refresh" && nativeFailure.Task.IsCompleted;
        }, TimeSpan.FromSeconds(25)), "The native consumer did not report its scheduled refresh failure after token installation.");
        Assert.NotNull(expiry);
        Assert.True(TimeProvider.System.GetUtcNow() < expiry.Value);
        Assert.Equal(expiry, session.InstalledTokenExpiresOn);
        Assert.Null(session.InitialFailure);
        Assert.Equal("acquisition", session.LatestFailure?.Reason);
        Assert.Equal(2, tokenProvider.Calls);
        Error error = await WaitForNativeFailure(nativeFailure, TestContext.Current.CancellationToken);
        Assert.Equal(ErrorCode.Local_Authentication, error.Code);
        Assert.Contains("acquisition", error.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic consumer credential secret", error.Reason, StringComparison.Ordinal);
        Assert.True(TimeProvider.System.GetUtcNow() < expiry.Value);
    }

    [Fact]
    public async Task Given_ProducerReceivesNativeRejectedToken_When_RefreshRuns_Then_InstallationFailureIsSanitized()
    {
        // The pinned native validator rejects SOH (not CR/LF), exercising real installation failure:
        // https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/src/rdkafka_sasl_oauthbearer.c#L296-L326
        var tokenProvider = new SyntheticTokenProvider(() => new AccessToken(
            "invalid\u0001token", TimeProvider.System.GetUtcNow().AddHours(1)));
        using ServiceProvider services = CreateServices(tokenProvider);
        KafkaAuthenticationFactory factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        ProducerConfig configuration = services.GetRequiredService<KafkaConfigurationFactory>()
            .CreateLoadGenProducer().Configuration;
        configuration.BootstrapServers = "127.0.0.1:1";
        KafkaOAuthBearerSession session = factory.CreateSession(Identity(KafkaClientRole.LoadGenProducer), TestContext.Current.CancellationToken);

        TaskCompletionSource<Error> nativeFailure = NewNativeFailureSignal();
        ProducerBuilder<Null, string> builder = factory.CreateProducerBuilder<Null, string>(configuration, session)
            .SetErrorHandler((_, error) => CaptureAuthenticationFailure(nativeFailure, error));
        using IProducer<Null, string> producer = builder.Build();

        Assert.True(WaitUntil(() => session.LatestFailure is not null, TimeSpan.FromSeconds(15)),
            "The native producer did not report its rejected token installation.");
        Assert.Equal("initial", session.InitialFailure?.Phase);
        Assert.Equal("installation", session.InitialFailure?.Reason);
        Assert.Equal(session.InitialFailure, session.LatestFailure);
        Error error = await WaitForNativeFailure(nativeFailure, TestContext.Current.CancellationToken);
        Assert.Equal(ErrorCode.Local_Authentication, error.Code);
        Assert.Contains("installation", error.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid", error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ConsumerInitialAcquisitionFails_When_ConsumerPolls_Then_NativeFailureIsSanitized()
    {
        var tokenProvider = new SyntheticTokenProvider(
            () => throw new InvalidOperationException("synthetic consumer credential secret"));
        using ServiceProvider services = CreateServices(tokenProvider);
        KafkaAuthenticationFactory factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        ConsumerConfig configuration = services.GetRequiredService<KafkaConfigurationFactory>()
            .CreateVerifierConsumer().Configuration;
        configuration.BootstrapServers = "127.0.0.1:1";
        KafkaOAuthBearerSession session = factory.CreateSession(
            Identity(KafkaClientRole.VerifierConsumer), TestContext.Current.CancellationToken);

        TaskCompletionSource<Error> nativeFailure = NewNativeFailureSignal();
        ConsumerBuilder<Ignore, string> builder = factory.CreateConsumerBuilder<Ignore, string>(configuration, session)
            .SetErrorHandler((_, error) => CaptureAuthenticationFailure(nativeFailure, error));
        using IConsumer<Ignore, string> consumer = builder.Build();

        Assert.True(WaitUntil(() =>
        {
            consumer.Consume(TimeSpan.FromMilliseconds(50));
            return session.InitialFailure is not null && nativeFailure.Task.IsCompleted;
        }, TimeSpan.FromSeconds(15)), "The native consumer did not report its attached initial refresh failure.");
        Assert.Equal("initial", session.InitialFailure?.Phase);
        Assert.Equal("acquisition", session.InitialFailure?.Reason);
        Error error = await WaitForNativeFailure(nativeFailure, TestContext.Current.CancellationToken);
        Assert.Equal(ErrorCode.Local_Authentication, error.Code);
        Assert.Contains("acquisition", error.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic consumer credential secret", error.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("acquisition")]
    [InlineData("installation")]
    public async Task Given_InstalledNativeTokenThenRefreshFails_When_ProducerPolls_Then_StickyFailureRemainsUnchanged(string reason)
    {
        DateTimeOffset? expiry = null;
        var tokenProvider = new SyntheticTokenProvider(
            () =>
            {
                expiry = TimeProvider.System.GetUtcNow().AddSeconds(15);
                return new AccessToken("header.payload.signature", expiry.Value);
            },
            () => reason == "installation"
                ? new AccessToken("invalid\u0001token", TimeProvider.System.GetUtcNow().AddHours(1))
                : throw new InvalidOperationException("synthetic credential secret"));
        using ServiceProvider services = CreateServices(tokenProvider);
        KafkaAuthenticationFactory factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        ProducerConfig configuration = services.GetRequiredService<KafkaConfigurationFactory>()
            .CreateLoadGenProducer().Configuration;
        configuration.BootstrapServers = "127.0.0.1:1";
        KafkaOAuthBearerSession session = factory.CreateSession(Identity(KafkaClientRole.LoadGenProducer), TestContext.Current.CancellationToken);

        TaskCompletionSource<Error> nativeFailure = NewNativeFailureSignal();
        ProducerBuilder<Null, string> builder = factory.CreateProducerBuilder<Null, string>(configuration, session)
            .SetErrorHandler((_, error) => CaptureAuthenticationFailure(nativeFailure, error));
        using IProducer<Null, string> producer = builder.Build();

        Assert.True(WaitUntil(() => session.LatestFailure?.Phase == "refresh" && nativeFailure.Task.IsCompleted,
            TimeSpan.FromSeconds(25)),
            "The native producer did not report its scheduled refresh failure after token installation.");
        Assert.Null(session.InitialFailure);
        Assert.NotNull(expiry);
        Assert.True(TimeProvider.System.GetUtcNow() < expiry.Value);
        Assert.Equal(expiry, session.InstalledTokenExpiresOn);
        Assert.Equal("refresh", session.LatestFailure?.Phase);
        Assert.Equal(reason, session.LatestFailure?.Reason);
        Error error = await WaitForNativeFailure(nativeFailure, TestContext.Current.CancellationToken);
        Assert.Equal(ErrorCode.Local_Authentication, error.Code);
        Assert.Contains(reason, error.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic credential secret", error.Reason, StringComparison.Ordinal);
        Assert.True(TimeProvider.System.GetUtcNow() < expiry.Value);
    }

    [Fact]
    public void Given_InvalidFinalConfiguration_When_BuilderIsCreated_Then_RejectsSnapshotAndLeavesCallerConfigUnchanged()
    {
        var tokenProvider = new SyntheticTokenProvider(() => new AccessToken(
            "header.payload.signature", TimeProvider.System.GetUtcNow().AddHours(1)));
        using ServiceProvider services = CreateServices(tokenProvider);
        KafkaAuthenticationFactory factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        ProducerConfig configuration = services.GetRequiredService<KafkaConfigurationFactory>()
            .CreateLoadGenProducer().Configuration;
        configuration.Set("sasl.username", "synthetic-excluded-value");
        KafkaOAuthBearerSession session = factory.CreateSession(Identity(KafkaClientRole.LoadGenProducer), TestContext.Current.CancellationToken);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => factory.CreateProducerBuilder<Null, string>(configuration, session));

        Assert.DoesNotContain("synthetic-excluded-value", error.Message, StringComparison.Ordinal);
        Assert.Equal("synthetic-excluded-value", configuration.Get("sasl.username"));
        Assert.Equal(0, tokenProvider.Calls);
        ProducerBuilder<Null, string> validBuilder = factory.CreateProducerBuilder<Null, string>(
            services.GetRequiredService<KafkaConfigurationFactory>().CreateLoadGenProducer().Configuration, session);
        Assert.NotNull(validBuilder);
        Assert.Equal(0, tokenProvider.Calls);
    }

    [Fact]
    public async Task Given_OneProducerBuilderBuiltForTwoClients_When_CallbacksRun_Then_SecondClientCannotUseSession()
    {
        const string runId = "session-reuse-native-metric-test";
        long failureMeasurements = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (instrument.Meter.Name == "Parchwell.Kafka.Authentication" &&
                instrument.Name == "parchwell.kafka.oauthbearer.refresh_handler.errors")
                currentListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            if (tags.ToArray().Any(tag => tag.Key == "run" && Equals(tag.Value, runId)))
                Interlocked.Add(ref failureMeasurements, value);
        });
        listener.Start();
        var provider = new SyntheticTokenProvider(() => new AccessToken(
            "header.payload.signature", TimeProvider.System.GetUtcNow().AddHours(1)));
        using ServiceProvider services = CreateServices(provider);
        KafkaAuthenticationFactory factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        ProducerConfig configuration = services.GetRequiredService<KafkaConfigurationFactory>()
            .CreateLoadGenProducer().Configuration;
        configuration.BootstrapServers = "127.0.0.1:1";
        KafkaOAuthBearerSession session = factory.CreateSession(new KafkaAuthenticationIdentity(
            "synthetic-scenario", runId, KafkaClientRole.LoadGenProducer, "synthetic-producer"), TestContext.Current.CancellationToken);
        TaskCompletionSource<Error> nativeFailure = NewNativeFailureSignal();
        ProducerBuilder<Null, string> builder = factory.CreateProducerBuilder<Null, string>(configuration, session)
            .SetErrorHandler((_, error) =>
            {
                if (error.Code == ErrorCode.Local_Application &&
                    error.Reason.Contains("OAuth bearer session callback belongs to a different native client.", StringComparison.Ordinal))
                    nativeFailure.TrySetResult(error);
            });

        using IProducer<Null, string> first = builder.Build();
        Assert.True(WaitUntil(() => session.InstalledTokenExpiresOn is not null, TimeSpan.FromSeconds(15)));
        DateTimeOffset firstExpiry = Assert.IsType<DateTimeOffset>(session.InstalledTokenExpiresOn);
        using IProducer<Null, string> second = builder.Build();

        Assert.True(WaitUntil(() => nativeFailure.Task.IsCompleted, TimeSpan.FromSeconds(15)),
            "The pinned Confluent client did not surface its caught callback exception through the error handler.");
        Assert.Equal(1, provider.Calls);
        Assert.Equal(firstExpiry, session.InstalledTokenExpiresOn);
        Assert.Null(session.InitialFailure);
        Assert.Null(session.LatestFailure);
        Assert.Equal(0, Volatile.Read(ref failureMeasurements));
        Error observed = await nativeFailure.Task;
        Assert.Equal(ErrorCode.Local_Application, observed.Code);
        Assert.Contains("OAuth bearer session callback belongs to a different native client.", observed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_OneConsumerBuilderBuiltForTwoClients_When_SecondPolls_Then_ConsumeSurfacesCallbackRejection()
    {
        var provider = new SyntheticTokenProvider(() => new AccessToken(
            "header.payload.signature", TimeProvider.System.GetUtcNow().AddHours(1)));
        using ServiceProvider services = CreateServices(provider);
        KafkaAuthenticationFactory factory = services.GetRequiredService<KafkaAuthenticationFactory>();
        ConsumerConfig configuration = services.GetRequiredService<KafkaConfigurationFactory>()
            .CreateVerifierConsumer().Configuration;
        configuration.BootstrapServers = "127.0.0.1:1";
        KafkaOAuthBearerSession session = factory.CreateSession(Identity(KafkaClientRole.VerifierConsumer), TestContext.Current.CancellationToken);
        ConsumerBuilder<Ignore, string> builder = factory.CreateConsumerBuilder<Ignore, string>(configuration, session);

        using IConsumer<Ignore, string> first = builder.Build();
        Assert.True(WaitUntil(() =>
        {
            try
            {
                first.Consume(TimeSpan.FromMilliseconds(20));
            }
            catch (KafkaException)
            {
                // Broker connection failure is unrelated; keep polling for the OAuth callback.
            }
            return session.InstalledTokenExpiresOn is not null;
        }, TimeSpan.FromSeconds(15)));
        DateTimeOffset firstExpiry = Assert.IsType<DateTimeOffset>(session.InstalledTokenExpiresOn);
        using IConsumer<Ignore, string> second = builder.Build();

        InvalidOperationException? rejection = null;
        Assert.True(WaitUntil(() =>
        {
            try
            {
                second.Consume(TimeSpan.FromMilliseconds(20));
            }
            catch (InvalidOperationException exception)
            {
                rejection = exception;
            }
            catch (KafkaException)
            {
                // Broker connection failure is unrelated; continue until the pinned callback error is surfaced.
            }
            return rejection is not null;
        }, TimeSpan.FromSeconds(15)), "Pinned Consumer.Consume did not surface the caught callback exception.");

        Assert.Equal("OAuth bearer session callback belongs to a different native client.", rejection!.Message);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(firstExpiry, session.InstalledTokenExpiresOn);
        Assert.Null(session.InitialFailure);
        Assert.Null(session.LatestFailure);
    }

    private static ServiceProvider CreateServices(SyntheticTokenProvider tokenProvider)
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
        "synthetic-scenario", "synthetic-native-client-run", role,
        role == KafkaClientRole.LoadGenProducer ? "synthetic-producer" : "synthetic-consumer");

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (condition()) return true;
            Thread.Sleep(20);
        }
        return condition();
    }

    private static TaskCompletionSource<Error> NewNativeFailureSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void CaptureAuthenticationFailure(TaskCompletionSource<Error> signal, Error error)
    {
        if (error.Code == ErrorCode.Local_Authentication)
            signal.TrySetResult(error);
    }

    private static async Task<Error> WaitForNativeFailure(
        TaskCompletionSource<Error> signal,
        CancellationToken cancellationToken)
    {
        Task completed = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(15), cancellationToken));
        Assert.Same(signal.Task, completed);
        return await signal.Task;
    }

    private sealed class SyntheticTokenProvider(params Func<AccessToken>[] results) : IKafkaTokenProvider
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            int call = Interlocked.Increment(ref _calls);
            return results[Math.Min(call - 1, results.Length - 1)]();
        }
    }
}
