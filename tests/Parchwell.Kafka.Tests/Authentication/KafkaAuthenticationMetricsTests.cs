using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Azure.Core;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

/// <summary>Protects the exported OAuth bearer refresh-handler error metric contract.</summary>
[Trait("Category", "Unit")]
public sealed class KafkaAuthenticationMetricsTests
{
    private const string MeterName = "Parchwell.Kafka.Authentication";
    private const string Scenario = "authentication-metric-scenario";
    private const string ClientId = "synthetic-kafka-client";
    private const string Secret = "synthetic-secret-material";

    [Theory]
    [InlineData("initial", "acquisition")]
    [InlineData("initial", "acquisition_cancellation")]
    [InlineData("initial", "token_validation")]
    [InlineData("initial", "installation")]
    [InlineData("refresh", "acquisition")]
    [InlineData("refresh", "acquisition_cancellation")]
    [InlineData("refresh", "token_validation")]
    [InlineData("refresh", "installation")]
    public void Given_HandlerFailure_When_InvokedRepeatedly_Then_EmitsOneSanitizedMetricPerFailure(
        string phase,
        string reason)
    {
        string runId = $"authentication-metric-{phase}-{reason}";
        using var listener = new AuthenticationMetricsListener(runId);
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new MutableTokenProvider();
        using var cancellationSource = new CancellationTokenSource();
        var session = new KafkaOAuthBearerSession(
            new KafkaAuthenticationIdentity(Scenario, runId, KafkaClientRole.LoadGenProducer, ClientId),
            "https://synthetic.servicebus.windows.net/.default",
            "synthetic-workload-application",
            provider,
            new FixedTimeProvider(),
            metrics,
            cancellationSource.Token);
        bool failInstallation = false;
        var reportedFailures = new List<string>();

        if (phase == "refresh")
        {
            session.Refresh((_, _, _) => { }, reportedFailures.Add);
            Assert.NotNull(session.InstalledTokenExpiresOn);
            Assert.Empty(listener.Measurements);
        }

        ConfigureFailure(provider, reason);
        if (reason == "acquisition_cancellation")
        {
            cancellationSource.Cancel();
        }
        failInstallation = reason == "installation";
        Action<string, long, string> install = (_, _, _) =>
        {
            if (failInstallation)
            {
                throw new InvalidOperationException(Secret);
            }
        };

        session.Refresh(install, reportedFailures.Add);
        session.Refresh(install, reportedFailures.Add);

        Assert.Equal(2, listener.Measurements.Length);
        Assert.All(listener.Measurements, measurement =>
        {
            Assert.Equal("parchwell.kafka.oauthbearer.refresh_handler.errors", measurement.Name);
            Assert.Equal("count", measurement.Unit);
            Assert.Equal(1, measurement.Value);
            Assert.Equal(6, measurement.Tags.Count);
            Assert.Equal(Scenario, measurement.Tags["scenario"]);
            Assert.Equal(runId, measurement.Tags["run"]);
            Assert.Equal("LoadGenProducer", measurement.Tags["role"]);
            Assert.Equal(ClientId, measurement.Tags["client_id"]);
            Assert.Equal(phase, measurement.Tags["phase"]);
            Assert.Equal(reason == "acquisition_cancellation" ? "acquisition" : reason, measurement.Tags["reason"]);
            Assert.DoesNotContain(Secret, string.Join(" ", measurement.Tags.Values));
            Assert.DoesNotContain("token", measurement.Tags.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("credential", measurement.Tags.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("path", measurement.Tags.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("exception", measurement.Tags.Keys, StringComparer.OrdinalIgnoreCase);
        });
        PublishedInstrument instrument = Assert.Single(listener.PublishedInstruments
            .Where(item => item.Name == "parchwell.kafka.oauthbearer.refresh_handler.errors")
            .Distinct());
        Assert.Equal(MeterName, instrument.MeterName);
        Assert.Equal("parchwell.kafka.oauthbearer.refresh_handler.errors", instrument.Name);
        Assert.Equal("count", instrument.Unit);
        Assert.Equal("Counter`1", instrument.Kind);
    }

    [Fact]
    public void Given_SuccessfulHandler_When_Refreshing_Then_DoesNotIncrementErrorCounter()
    {
        const string runId = "authentication-metric-success";
        using var listener = new AuthenticationMetricsListener(runId);
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new MutableTokenProvider();
        var session = new KafkaOAuthBearerSession(
            new KafkaAuthenticationIdentity(Scenario, runId, KafkaClientRole.EnrichmentConsumer, ClientId),
            "https://synthetic.servicebus.windows.net/.default",
            "synthetic-workload-application",
            provider,
            new FixedTimeProvider(),
            metrics,
            CancellationToken.None);

        session.Refresh((_, _, _) => { }, _ => throw new InvalidOperationException("Unexpected failure report."));
        session.Refresh((_, _, _) => { }, _ => throw new InvalidOperationException("Unexpected failure report."));

        Assert.Empty(listener.Measurements);
        Assert.Null(session.InitialFailure);
        Assert.Null(session.LatestFailure);
    }

    [Fact]
    public void Given_FailureReporterThrows_When_HandlerFails_Then_RecordsOnlyOneFailureAndReturns()
    {
        const string runId = "authentication-metric-reporter-throws";
        using var listener = new AuthenticationMetricsListener(runId);
        using var metrics = new KafkaAuthenticationMetrics();
        var provider = new MutableTokenProvider
        {
            GetTokenHandler = (_, _) => throw new InvalidOperationException(Secret)
        };
        var session = new KafkaOAuthBearerSession(
            new KafkaAuthenticationIdentity(Scenario, runId, KafkaClientRole.LoadGenProducer, ClientId),
            "https://synthetic.servicebus.windows.net/.default",
            "synthetic-workload-application",
            provider,
            new FixedTimeProvider(),
            metrics,
            CancellationToken.None);
        int reporterCalls = 0;

        session.Refresh((_, _, _) => throw new InvalidOperationException("Unexpected installation."), _ =>
        {
            reporterCalls++;
            throw new InvalidOperationException(Secret);
        });

        Measurement measurement = Assert.Single(listener.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("initial", measurement.Tags["phase"]);
        Assert.Equal("acquisition", measurement.Tags["reason"]);
        Assert.Equal(1, reporterCalls);
        Assert.Equal("acquisition", session.InitialFailure?.Reason);
        Assert.Equal("acquisition", session.LatestFailure?.Reason);
    }

    private static void ConfigureFailure(MutableTokenProvider provider, string reason)
    {
        provider.GetTokenHandler = reason switch
        {
            "acquisition" => (_, _) => throw new InvalidOperationException(Secret),
            "acquisition_cancellation" => (_, cancellationToken) =>
            {
                Assert.True(cancellationToken.IsCancellationRequested);
                throw new OperationCanceledException(cancellationToken);
            },
            "token_validation" => (_, _) => new AccessToken(string.Empty, FixedTimeProvider.Now.AddMinutes(5)),
            "installation" => (_, _) => new AccessToken("synthetic-token", FixedTimeProvider.Now.AddMinutes(5)),
            _ => throw new ArgumentOutOfRangeException(nameof(reason))
        };
    }

    private sealed record PublishedInstrument(string MeterName, string Name, string Unit, string Kind);

    private sealed record Measurement(string Name, long Value, string Unit, IReadOnlyDictionary<string, object?> Tags);

    private sealed class AuthenticationMetricsListener : IDisposable
    {
        private readonly string _runId;
        private readonly MeterListener _listener = new();
        private readonly ConcurrentBag<PublishedInstrument> _published = [];
        private readonly ConcurrentBag<Measurement> _measurements = [];

        internal AuthenticationMetricsListener(string runId)
        {
            _runId = runId;
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MeterName)
                {
                    _published.Add(new PublishedInstrument(
                        instrument.Meter.Name,
                        instrument.Name,
                        instrument.Unit ?? string.Empty,
                        instrument.GetType().Name));
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                var copiedTags = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value);
                if (copiedTags.TryGetValue("run", out object? run) && Equals(run, _runId))
                {
                    _measurements.Add(new Measurement(
                        instrument.Name,
                        value,
                        instrument.Unit ?? string.Empty,
                        copiedTags));
                }
            });
            _listener.Start();
        }

        internal IReadOnlyCollection<PublishedInstrument> PublishedInstruments => _published.ToArray();

        internal Measurement[] Measurements => _measurements.ToArray();

        public void Dispose() => _listener.Dispose();
    }

    private sealed class MutableTokenProvider : IKafkaTokenProvider
    {
        internal Func<string, CancellationToken, AccessToken> GetTokenHandler { get; set; } =
            (_, _) => new AccessToken("synthetic-token", FixedTimeProvider.Now.AddMinutes(5));

        public AccessToken GetToken(string scope, CancellationToken cancellationToken) =>
            GetTokenHandler(scope, cancellationToken);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        internal static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
