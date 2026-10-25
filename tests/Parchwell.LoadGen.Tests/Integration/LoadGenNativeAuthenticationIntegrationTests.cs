using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.Kafka.Telemetry;
using Parchwell.LoadGen;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Tests.Host;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Integration;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
[Trait("Category", "Native")]
public sealed class LoadGenNativeAuthenticationIntegrationTests
{
    private static readonly string[] AuthenticationMetricTagNames = ["client_id", "phase", "reason", "role", "run", "scenario"];
    [Fact]
    public async Task Given_ProductionWiringAndInitialCredentialFailure_When_NativeCallbackRuns_Then_EvidenceAndMetricRetainSanitizedRunIdentity()
    {
        using var outputDirectory = new TestOutputDirectory("loadgen-native-auth-");
        string output = outputDirectory.Path;
        string runId = "native-run-" + Guid.NewGuid().ToString("N")[..18];
        Dictionary<string, string> values = LoadGenHostProcessTests.CreateInputs(output);
        values["LoadGen__RunId"] = runId;
        values["LoadGen__JobName"] = $"{runId}-c0-r1-loadgen";
        string[] args = values.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
        var tokenProvider = new FailingTokenProvider();
        var telemetry = new RecordingTelemetryRegistration();
        using IHost host = Program.BuildHostCore(args, services =>
        {
            services.RemoveAll<IKafkaTokenProvider>();
            services.AddSingleton<IKafkaTokenProvider>(tokenProvider);
        }, telemetry.Register);
        AssertTelemetry(telemetry, values["LoadGen__ApplicationInsightsConnectionString"], runId);
        Assert.Same(telemetry.ConfiguredOptions, host.Services.GetRequiredService<IOptions<ParchwellTelemetryOptions>>().Value);

        var measurements = new ConcurrentQueue<(long Value, Dictionary<string, string> Tags)>();
        string? measurementUnit = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "Parchwell.Kafka.Authentication" &&
                instrument.Name == "parchwell.kafka.oauthbearer.refresh_handler.errors")
            {
                measurementUnit = instrument.Unit;
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "parchwell.kafka.oauthbearer.refresh_handler.errors" &&
                tags.ToArray().Any(tag => tag.Key == "run" && StringComparer.Ordinal.Equals(tag.Value?.ToString(), runId)))
                measurements.Enqueue((value, tags.ToArray().ToDictionary(item => item.Key, item => item.Value?.ToString() ?? "", StringComparer.Ordinal)));
        });
        listener.Start();

        IServiceProvider services = host.Services;
        KafkaConfigurationFactory configurationFactory = services.GetRequiredService<KafkaConfigurationFactory>();
        ProducerConfig configuration = configurationFactory.CreateLoadGenProducer().Configuration;
        configuration.BootstrapServers = "127.0.0.1:1";
        var identity = new RunIdentity("A01", "C0", runId, "worker-pod", "worker-pod", "worker-pod");
        LoadedScenario loaded = await new ScenarioLoader(new FileSystemScenarioSource(FindRepositoryRoot()),
            Options.Create(new ScenarioLoadOptions
            {
                MaxDocumentCharacters = 100_000,
                MaxNestingDepth = 32,
                MaxScalarCharacters = 10_000,
                MaxParserEvents = 10_000
            })).LoadAsync("A01", TestContext.Current.CancellationToken);
        ResolvedScenario resolved = new ScenarioResolver(configurationFactory).Resolve(loaded);
        RunEvidenceExpectations expectations = new(resolved.Configurations["C0"].Roles[KafkaClientRole.LoadGenProducer],
            false, ImmutableArray.Create("broker-0"));
        var evidence = new RunEvidenceSession(identity, new RunEvidenceOptions(output, 32, 8192,
            1024 * 1024, 32, TimeSpan.FromSeconds(5)), TimeProvider.System, expectations);
        RunEvidenceSinkRouter router = services.GetRequiredService<RunEvidenceSinkRouter>();
        router.Attach(evidence);
        KafkaAuthenticationFactory authenticationFactory = services.GetRequiredService<KafkaAuthenticationFactory>();
        KafkaOAuthBearerSession authentication = authenticationFactory.CreateSession(new KafkaAuthenticationIdentity(
            "A01", runId, KafkaClientRole.LoadGenProducer, configuration.ClientId!), TestContext.Current.CancellationToken);
        KafkaStatisticsSession statistics = services.GetRequiredService<KafkaStatisticsBridgeFactory>().Create(
            new KafkaStatisticsIdentity("A01", runId, KafkaClientRole.LoadGenProducer, configuration.ClientId!));
        var producerFactory = services.GetRequiredService<Func<ProducerConfig, KafkaStatisticsSession,
            RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>>>();

        try
        {
            using IProducer<byte[], byte[]> producer = producerFactory(configuration, statistics, evidence, authentication);
            Assert.True(await WaitUntilAsync(() => authentication.InitialFailure is not null, TimeSpan.FromSeconds(15)));
            string lifecyclePath = Path.Combine(output, "request-lifecycle.jsonl");
            Assert.True(await WaitUntilAsync(() => File.ReadAllText(lifecyclePath).Contains(
                "\"error_name\":\"Local_Authentication\"", StringComparison.Ordinal), TimeSpan.FromSeconds(15)));
            Assert.Equal("initial", authentication.InitialFailure?.Phase);
            Assert.Equal("acquisition", authentication.InitialFailure?.Reason);
            Assert.Equal(1, tokenProvider.Calls);
            Assert.Single(measurements);
            var measurement = measurements.Single();
            Assert.Equal("count", measurementUnit);
            Assert.Equal(1, measurement.Value);
            Assert.Equal(AuthenticationMetricTagNames, measurement.Tags.Keys.Order(StringComparer.Ordinal));
            Assert.Equal("A01", measurement.Tags["scenario"]);
            Assert.Equal(runId, measurement.Tags["run"]);
            Assert.Equal("LoadGenProducer", measurement.Tags["role"]);
            Assert.Equal(configuration.ClientId, measurement.Tags["client_id"]);
            Assert.Equal("initial", measurement.Tags["phase"]);
            Assert.Equal("acquisition", measurement.Tags["reason"]);
            Assert.DoesNotContain("synthetic-credential-secret", string.Join(" ", measurement.Tags.Values), StringComparison.Ordinal);
            Assert.NotEqual("synthetic-credential-app", measurement.Tags["client_id"]);
        }
        finally
        {
            await statistics.DisposeAsync();
            await evidence.CompleteAsync(null, TestContext.Current.CancellationToken);
        }

        Assert.False(evidence.IsFaulted);
        await evidence.ValidatePersistedAsync(TestContext.Current.CancellationToken);
        string capabilities = await File.ReadAllTextAsync(Path.Combine(output, "capabilities.json"), TestContext.Current.CancellationToken);
        using JsonDocument captured = JsonDocument.Parse(capabilities);
        Assert.Contains("native_error_callback_observed", captured.RootElement.GetProperty("guard_observations").EnumerateArray()
            .Select(item => item.GetString()), StringComparer.Ordinal);
        string[] lifecycleLines = await File.ReadAllLinesAsync(Path.Combine(output, "request-lifecycle.jsonl"), TestContext.Current.CancellationToken);
        string? nativeFailureReason = null;
        foreach (string line in lifecycleLines)
        {
            using JsonDocument row = JsonDocument.Parse(line);
            if (row.RootElement.GetProperty("event_type").GetString() == "native_error" &&
                row.RootElement.GetProperty("error_name").GetString() == "Local_Authentication")
                nativeFailureReason = row.RootElement.GetProperty("reason").GetString();
        }
        Assert.NotNull(nativeFailureReason);
        Assert.Contains("acquisition", nativeFailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-credential-secret", nativeFailureReason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("acquisition")]
    [InlineData("installation")]
    public async Task Given_NativeTokenInstalledThenRefreshFails_When_CallbackRuns_Then_ExpiryAndPersistedErrorAreRetained(string failure)
    {
        using var outputDirectory = new TestOutputDirectory("loadgen-native-refresh-");
        string output = outputDirectory.Path;
        string runId = "native-refresh-run-" + Guid.NewGuid().ToString("N")[..10];
        Dictionary<string, string> values = LoadGenHostProcessTests.CreateInputs(output);
        values["LoadGen__RunId"] = runId;
        values["LoadGen__JobName"] = $"{runId}-c0-r1-loadgen";
        string[] args = values.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
        var tokenProvider = new RefreshFailingTokenProvider(failure);
        var telemetry = new RecordingTelemetryRegistration();
        using IHost host = Program.BuildHostCore(args, services =>
        {
            services.RemoveAll<IKafkaTokenProvider>();
            services.AddSingleton<IKafkaTokenProvider>(tokenProvider);
        }, telemetry.Register);
        AssertTelemetry(telemetry, values["LoadGen__ApplicationInsightsConnectionString"], runId);
        Assert.Same(telemetry.ConfiguredOptions, host.Services.GetRequiredService<IOptions<ParchwellTelemetryOptions>>().Value);
        IServiceProvider services = host.Services;
        KafkaConfigurationFactory configurationFactory = services.GetRequiredService<KafkaConfigurationFactory>();
        ProducerConfig configuration = configurationFactory.CreateLoadGenProducer().Configuration;
        configuration.BootstrapServers = "127.0.0.1:1";
        var identity = new RunIdentity("A01", "C0", runId, "worker-pod", "worker-pod", "worker-pod");
        LoadedScenario loaded = await new ScenarioLoader(new FileSystemScenarioSource(FindRepositoryRoot()),
            Options.Create(new ScenarioLoadOptions
            {
                MaxDocumentCharacters = 100_000,
                MaxNestingDepth = 32,
                MaxScalarCharacters = 10_000,
                MaxParserEvents = 10_000
            })).LoadAsync("A01", TestContext.Current.CancellationToken);
        ResolvedScenario resolved = new ScenarioResolver(configurationFactory).Resolve(loaded);
        RunEvidenceExpectations expectations = new(resolved.Configurations["C0"].Roles[KafkaClientRole.LoadGenProducer],
            false, ImmutableArray.Create("broker-0"));
        var evidence = new RunEvidenceSession(identity, new RunEvidenceOptions(output, 32, 8192,
            1024 * 1024, 32, TimeSpan.FromSeconds(5)), TimeProvider.System, expectations);
        services.GetRequiredService<RunEvidenceSinkRouter>().Attach(evidence);
        KafkaOAuthBearerSession authentication = services.GetRequiredService<KafkaAuthenticationFactory>().CreateSession(
            new KafkaAuthenticationIdentity("A01", runId, KafkaClientRole.LoadGenProducer, configuration.ClientId!),
            TestContext.Current.CancellationToken);
        KafkaStatisticsSession statistics = services.GetRequiredService<KafkaStatisticsBridgeFactory>().Create(
            new KafkaStatisticsIdentity("A01", runId, KafkaClientRole.LoadGenProducer, configuration.ClientId!));
        var producerFactory = services.GetRequiredService<Func<ProducerConfig, KafkaStatisticsSession,
            RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>>>();
        try
        {
            using IProducer<byte[], byte[]> producer = producerFactory(configuration, statistics, evidence, authentication);
            Assert.True(await WaitUntilAsync(() => authentication.InstalledTokenExpiresOn is not null, TimeSpan.FromSeconds(15)));
            DateTimeOffset expiry = Assert.IsType<DateTimeOffset>(authentication.InstalledTokenExpiresOn);
            Assert.True(await WaitUntilAsync(() => authentication.LatestFailure?.Phase == "refresh" &&
                File.ReadAllText(Path.Combine(output, "request-lifecycle.jsonl")).Contains(
                    "\"error_name\":\"Local_Authentication\"", StringComparison.Ordinal), TimeSpan.FromSeconds(35)));
            Assert.Equal(expiry, authentication.InstalledTokenExpiresOn);
            Assert.Null(authentication.InitialFailure);
            Assert.Equal(failure, authentication.LatestFailure?.Reason);
            Assert.Equal(2, tokenProvider.Calls);
        }
        finally
        {
            await statistics.DisposeAsync();
            await evidence.CompleteAsync(null, TestContext.Current.CancellationToken);
        }

        Assert.False(evidence.IsFaulted);
        await evidence.ValidatePersistedAsync(TestContext.Current.CancellationToken);
        string[] rows = await File.ReadAllLinesAsync(Path.Combine(output, "request-lifecycle.jsonl"), TestContext.Current.CancellationToken);
        string? reason = null;
        foreach (string line in rows)
        {
            using JsonDocument row = JsonDocument.Parse(line);
            if (row.RootElement.GetProperty("event_type").GetString() == "native_error" &&
                row.RootElement.GetProperty("error_name").GetString() == "Local_Authentication")
                reason = row.RootElement.GetProperty("reason").GetString();
        }
        Assert.NotNull(reason);
        Assert.Contains(failure, reason, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-credential-secret", reason, StringComparison.Ordinal);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = TimeProvider.System.GetUtcNow() + timeout;
        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            if (condition()) return true;
            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }
        return condition();
    }

    private static void AssertTelemetry(RecordingTelemetryRegistration recorder, string connectionString, string runId)
    {
        Assert.Equal(1, recorder.RegistrationCount);
        ParchwellTelemetryOptions options = Assert.IsType<ParchwellTelemetryOptions>(recorder.ConfiguredOptions);
        Assert.Equal(connectionString, options.ConnectionString);
        Assert.Equal("Parchwell.LoadGen", options.ServiceName);
        Assert.Equal("worker-pod", options.ServiceInstanceId);
        Assert.Equal("A01", options.ScenarioId);
        Assert.Equal(runId, options.RunId);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "scenarios", "A01", "scenario.yaml"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository_root_not_found_for_loadgen_authentication_test");
    }

    private sealed class FailingTokenProvider : IKafkaTokenProvider
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("synthetic-credential-secret");
        }
    }

    private sealed class RefreshFailingTokenProvider(string failure) : IKafkaTokenProvider
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        public AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref _calls) == 1)
                return new AccessToken("synthetic-valid-token", TimeProvider.System.GetUtcNow().AddSeconds(15));
            return failure == "installation"
                ? new AccessToken("invalid\u0001synthetic-token", TimeProvider.System.GetUtcNow().AddHours(1))
                : throw new InvalidOperationException("synthetic-credential-secret");
        }
    }

    private sealed class TestOutputDirectory : IDisposable
    {
        public TestOutputDirectory(string prefix) =>
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
#pragma warning restore CA1707
