using System.Reflection;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parchwell.LoadGen;
using Parchwell.Kafka.Telemetry;
using Parchwell.Kafka.Statistics;
using Parchwell.Kafka.Authentication;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class LoadGenWorkerHostTests
{
    private static readonly string[] AuthenticationMetricTagNames = ["client_id", "phase", "reason", "role", "run", "scenario"];

    [Theory]
    [InlineData("acquisition")]
    [InlineData("installation")]
    public async Task Given_IncompleteRunWithSpecificDiagnostics_When_WorkerRejectsIt_Then_LogsReasonsAndIdentityWithoutPublishing(string failure)
    {
        var publisher = new RecordingPublisher();
        var logger = new RecordingWorkerLogger();
        Action<KafkaOAuthBearerSession, RunEvidenceSession> fail = failure == "acquisition"
            ? (session, evidence) => session.Refresh((_, _, _) => { }, error =>
                evidence.CaptureError(new Error(ErrorCode.Local_Authentication, "OAuth bearer " + error)))
            : (session, evidence) => session.Refresh((_, _, _) => throw new InvalidOperationException("synthetic-install-secret"), error =>
                evidence.CaptureError(new Error(ErrorCode.Local_Authentication, "OAuth bearer " + error)));
        using IHost host = CreateHost(publisher, durationSeconds: 1, authenticationCallback: fail,
            tokenProvider: failure == "acquisition" ? new ThrowingTokenProvider() : new SyntheticTokenProvider(), workerLogger: logger);
        try
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
            await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
            Assert.False(publisher.Called);
            RecordedWorkerLog entry = Assert.Single(logger.Entries, item => item.EventId == 3);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Equal("A01", entry.State["ScenarioId"]);
            Assert.Equal(publisher.RunId, entry.State["RunId"]);
            using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(publisher.OutputRoot, "manifest.json"), TestContext.Current.CancellationToken));
            Assert.Equal(manifest.RootElement.GetProperty("attempt_count").GetInt64(), entry.State["AttemptCount"]);
            Assert.Equal(manifest.RootElement.GetProperty("planned_attempt_count").GetInt64(), entry.State["PlannedAttemptCount"]);
            string diagnostics = Assert.IsType<string>(entry.State["Diagnostics"]);
            Assert.Contains("oauthbearer_initial_failure:" + failure, diagnostics, StringComparison.Ordinal);
            Assert.Equal(string.Join(';', manifest.RootElement.GetProperty("diagnostics").EnumerateArray().Select(item => item.GetString())), diagnostics);
            Assert.DoesNotContain("synthetic-credential-secret", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("synthetic-install-secret", entry.Message, StringComparison.Ordinal);
            RecordedWorkerLog rejection = Assert.Single(logger.Entries, item => item.EventId == 2);
            Assert.Equal("worker_run_incomplete", rejection.Exception?.Message);
        }
        finally { DeleteOutput(publisher); }
    }

    [Fact]
    public async Task Given_CompletedRun_When_WorkerPublishes_Then_NoIncompleteDiagnosticIsLogged()
    {
        var publisher = new RecordingPublisher();
        var logger = new RecordingWorkerLogger();
        using IHost host = CreateHost(publisher, durationSeconds: 1, workerLogger: logger);
        try
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
            await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
            Assert.True(publisher.Called);
            Assert.DoesNotContain(logger.Entries, item => item.EventId == 3);
            using JsonDocument capabilities = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(publisher.OutputRoot, "capabilities.json"), TestContext.Current.CancellationToken));
            JsonElement completion = capabilities.RootElement.GetProperty("statistics_completion");
            Assert.True(completion.GetProperty("is_complete").GetBoolean());
            long boundary = completion.GetProperty("drain_boundary_microseconds").GetInt64();
            long firstPostDrain = completion.GetProperty("first_post_drain_timestamp_microseconds").GetInt64();
            Assert.True(firstPostDrain > boundary);
            string[] statisticsRows = await File.ReadAllLinesAsync(
                Path.Combine(publisher.OutputRoot, "statistics.jsonl"), TestContext.Current.CancellationToken);
            Assert.NotEmpty(statisticsRows);
            long[] persistedTimestamps = statisticsRows.Select(row =>
            {
                using JsonDocument statisticsRow = JsonDocument.Parse(row);
                return statisticsRow.RootElement.GetProperty("typed_snapshot")
                    .GetProperty("timestamp_microseconds").GetInt64();
            }).ToArray();
            Assert.Contains(firstPostDrain, persistedTimestamps);
        }
        finally { DeleteOutput(publisher); }
    }

    [Fact]
    public async Task Given_AdmittedLocalWorker_When_RunAndPublicationComplete_Then_HostReturnsSuccessAfterPublisherSeesArtifacts()
    {
        var publisher = new RecordingPublisher();
        using IHost host = CreateHost(publisher, durationSeconds: 1);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        LoadGenWorker worker = host.Services.GetRequiredService<LoadGenWorker>();
        Assert.Equal(0, worker.ExitCode);
        Assert.True(publisher.Called);
        Assert.True(publisher.SawLedgerAndManifest);
        DeleteOutput(publisher);
    }

    [Fact]
    public async Task Given_CompletedLocalWorker_When_PublicationFails_Then_HostReturnsFailure()
    {
        var publisher = new RecordingPublisher(throwOnPublish: true);
        using IHost host = CreateHost(publisher, durationSeconds: 1);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
        Assert.True(publisher.Called);
        Assert.True(publisher.SawLedgerAndManifest);
        DeleteOutput(publisher);
    }

    [Fact]
    public async Task Given_PublisherUnavailable_When_AdmittingWorker_Then_FailsBeforeNativeProducerConstruction()
    {
        var publisher = new RecordingPublisher(available: false);
        var producerFactory = new ProducerFactoryTracker();
        using IHost host = CreateHost(publisher, durationSeconds: 1, producerFactory);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
        Assert.False(producerFactory.Called);
        Assert.False(publisher.Called);
        DeleteOutput(publisher);
    }

    [Fact]
    public async Task Given_ActiveScheduledWorker_When_HostIsCancelled_Then_ReturnsFailureWithoutPublishing()
    {
        var publisher = new RecordingPublisher();
        using IHost host = CreateHost(publisher, durationSeconds: 2);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
        Assert.False(publisher.Called);
        DeleteOutput(publisher);
    }

    [Fact]
    public async Task Given_HostIsCancelledDuringPublication_When_PublisherReturns_Then_WorkerDoesNotReportSuccess()
    {
        var publisher = new BlockingPublisher();
        using IHost host = CreateHost(publisher, durationSeconds: 1);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await publisher.PublishStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Task stop = host.StopAsync(TestContext.Current.CancellationToken);
        await publisher.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        publisher.AllowReturn.TrySetResult();
        await stop;

        Assert.Equal(1, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
        Assert.True(publisher.Called);
        DeleteOutput(publisher);
    }

    [Theory]
    [InlineData("acquisition")]
    [InlineData("installation")]
    public async Task Given_InitialAuthenticationFailureAtHostBoundary_When_RunCompletes_Then_SavesIncompleteStateAndSkipsPublisher(string failure)
    {
        var publisher = new RecordingPublisher();
        Action<KafkaOAuthBearerSession, RunEvidenceSession> fail = failure == "acquisition"
            ? (session, evidence) => session.Refresh((_, _, _) => { }, error =>
                evidence.CaptureError(new Error(ErrorCode.Local_Authentication, "OAuth bearer " + error)))
            : (session, evidence) => session.Refresh((_, _, _) => throw new InvalidOperationException("synthetic-install-secret"), error =>
                evidence.CaptureError(new Error(ErrorCode.Local_Authentication, "OAuth bearer " + error)));
        using IHost host = CreateHost(publisher, durationSeconds: 1, authenticationCallback: fail,
            tokenProvider: failure == "acquisition" ? new ThrowingTokenProvider() : new SyntheticTokenProvider());
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
        Assert.False(publisher.Called);
        Assert.True(File.Exists(Path.Combine(publisher.OutputRoot, "manifest.json")));
        using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(publisher.OutputRoot, "manifest.json"), TestContext.Current.CancellationToken));
        Assert.Equal("failed", manifest.RootElement.GetProperty("worker_status").GetString());
        Assert.False(manifest.RootElement.GetProperty("bookkeeping_complete").GetBoolean());
        Assert.Contains("oauthbearer_initial_failure:" + failure, manifest.RootElement.GetProperty("diagnostics")
            .EnumerateArray().Select(item => item.GetString()), StringComparer.Ordinal);
        Assert.Contains("native_error_callback_observed", manifest.RootElement.GetProperty("guard_observations")
            .EnumerateArray().Select(item => item.GetString()), StringComparer.Ordinal);
        DeleteOutput(publisher);
    }

    [Theory]
    [InlineData("acquisition")]
    [InlineData("installation")]
    public async Task Given_RefreshAuthenticationFailureWithInstalledToken_When_RunCompletes_Then_PublishesAndRetainsNativeGuard(string failure)
    {
        var publisher = new RecordingPublisher();
        var tracker = new AuthenticationTracker();
        var tokenProvider = new SyntheticTokenProvider();
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
                tags.ToArray().Any(tag => tag.Key == "run" && StringComparer.Ordinal.Equals(tag.Value?.ToString(), publisher.RunId)))
                measurements.Enqueue((value, tags.ToArray().ToDictionary(item => item.Key, item => item.Value?.ToString() ?? "", StringComparer.Ordinal)));
        });
        listener.Start();
        int installs = 0;
        Action<KafkaOAuthBearerSession, RunEvidenceSession> refresh = (session, evidence) =>
        {
            void Install(string token, long expiry, string principal)
            {
                _ = token;
                _ = expiry;
                _ = principal;
                if (Interlocked.Increment(ref installs) is 2 or 3 && failure == "installation")
                    throw new InvalidOperationException("synthetic-install-secret");
            }

            void Report(string reason) => evidence.CaptureError(
                new Error(ErrorCode.Local_Authentication, "OAuth bearer " + reason));
            session.Refresh(Install, Report);
            tracker.InstalledExpiry = session.InstalledTokenExpiresOn;
            session.Refresh(Install, Report);
            session.Refresh(Install, Report);
            tracker.Session = session;
        };
        using IHost host = CreateHost(publisher, durationSeconds: 1, authenticationCallback: refresh,
            tokenProvider: failure == "acquisition" ? new FailOnSecondTokenProvider() : tokenProvider);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
        Assert.True(publisher.Called);
        Assert.True(publisher.SawLedgerAndManifest);
        Assert.NotNull(tracker.InstalledExpiry);
        Assert.Equal(tracker.InstalledExpiry, tracker.Session?.InstalledTokenExpiresOn);
        Assert.Null(tracker.Session?.InitialFailure);
        Assert.Equal("refresh", tracker.Session?.LatestFailure?.Phase);
        Assert.Equal(failure, tracker.Session?.LatestFailure?.Reason);
        if (failure == "installation") Assert.True(tokenProvider.CancellationObserved);
        Assert.Equal(2, measurements.Count);
        Assert.All(measurements, measurement =>
        {
            Assert.Equal(1, measurement.Value);
            Assert.Equal("count", measurementUnit);
            Assert.Equal(AuthenticationMetricTagNames, measurement.Tags.Keys.Order(StringComparer.Ordinal));
            Assert.Equal("A01", measurement.Tags["scenario"]);
            Assert.Equal(publisher.RunId, measurement.Tags["run"]);
            Assert.Equal("LoadGenProducer", measurement.Tags["role"]);
            Assert.Equal("worker-pod", measurement.Tags["client_id"]);
            Assert.Equal("refresh", measurement.Tags["phase"]);
            Assert.Equal(failure, measurement.Tags["reason"]);
        });
        using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(publisher.OutputRoot, "manifest.json"), TestContext.Current.CancellationToken));
        Assert.True(manifest.RootElement.GetProperty("bookkeeping_complete").GetBoolean());
        Assert.Contains("native_error_callback_observed", manifest.RootElement.GetProperty("guard_observations")
            .EnumerateArray().Select(item => item.GetString()), StringComparer.Ordinal);
        DeleteOutput(publisher);
    }

    [Theory]
    [InlineData("probe", "C0", 1)]
    [InlineData("probe", "C0", 5)]
    [InlineData("C0", "C0", 1)]
    [InlineData("C0", "C0", 5)]
    [InlineData("C3", "C3", 1)]
    [InlineData("C3", "C3", 5)]
    public void Given_RequiredRegisteredWorkerInputs_When_BindingHostOptions_Then_PreservesExactIdentityAndPrefixes(
        string phase, string configurationId, int repeatNumber)
    {
        var publisher = new RecordingPublisher();
        var telemetry = new RecordingTelemetryRegistration();
        using IHost host = CreateHost(publisher, durationSeconds: 1, phase: phase,
            configurationId: configurationId, repeatNumber: repeatNumber, telemetryRegistration: telemetry);
        LoadGenHostOptions options = host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value;
        Assert.Equal(1, telemetry.RegistrationCount);
        ParchwellTelemetryOptions configuredTelemetry = Assert.IsType<ParchwellTelemetryOptions>(telemetry.ConfiguredOptions);
        Assert.Same(configuredTelemetry, host.Services.GetRequiredService<IOptions<ParchwellTelemetryOptions>>().Value);
        Assert.Equal(options.ApplicationInsightsConnectionString, configuredTelemetry.ConnectionString);
        Assert.Equal("Parchwell.LoadGen", configuredTelemetry.ServiceName);
        Assert.Equal("worker-pod", configuredTelemetry.ServiceInstanceId);
        Assert.Equal("A01", configuredTelemetry.ScenarioId);
        Assert.Equal(options.RunId, configuredTelemetry.RunId);
        var context = new ProducerRunContext(new Parchwell.LoadGen.Run.Contracts.RunIdentity(options.ScenarioId,
            options.ConfigurationId, options.RunId, options.ActualInstanceId, options.ExpectedInstanceId, options.ClientId),
            options.Phase, options.RepeatNumber, options.JobName, options.PodName, options.PodUid,
            new string('a', 40));

        Assert.Equal(phase, options.Phase);
        Assert.Equal(configurationId, options.ConfigurationId);
        Assert.Equal(repeatNumber, options.RepeatNumber);
        Assert.Equal($"{options.RunId}-{phase.ToLowerInvariant()}-r{repeatNumber}-loadgen", options.JobName);
        Assert.Equal("worker-pod", options.ActualInstanceId);
        Assert.Equal("worker-pod", options.ExpectedInstanceId);
        Assert.Equal("worker-pod", options.PodName);
        Assert.Equal("worker-pod", options.ClientId);
        Assert.Equal("synthetic-pod-uid", options.PodUid);
        Assert.Equal("InstrumentationKey=synthetic;IngestionEndpoint=https://example.invalid",
            options.ApplicationInsightsConnectionString);
        Assert.Equal($"{options.RunId}/{phase}/r{repeatNumber}/", context.RepeatPrefix);
        Assert.Equal($"{options.RunId}/{phase}/r{repeatNumber}/producer/worker-pod/", context.InstancePrefix);
        Assert.Equal(context.InstancePrefix + "registration.json", context.RegistrationPath);
        Assert.Equal(context.InstancePrefix + "publication/", context.PublicationPrefix);
        Assert.Equal(context.InstancePrefix + "diagnostics/", context.DiagnosticsPrefix);
        Assert.Equal(context.InstancePrefix + "completion.json", context.CompletionPath);
        Assert.False(Directory.Exists(publisher.OutputRoot));
    }

    [Theory]
    [InlineData("LoadGen__Phase", "C3")]
    [InlineData("LoadGen__RepeatNumber", "6")]
    [InlineData("LoadGen__JobName", "wrong-job")]
    [InlineData("LoadGen__RunId", "Uppercase")]
    [InlineData("LoadGen__PodUid", "bad uid")]
    [InlineData("LoadGen__ApplicationInsightsConnectionString", "")]
    [InlineData("LoadGen__ApplicationInsightsConnectionString", "<missing>")]
    [InlineData("PARCHWELL_SOURCE_COMMIT", "unknown")]
    public void Given_InvalidRegisteredWorkerInputs_When_StartingHost_Then_FailsBeforeRegistrationProducerAndArtifacts(
        string key, string value)
    {
        string output = Path.Combine(Path.GetTempPath(), "invalid-registered-worker-" + Guid.NewGuid().ToString("N"));
        Dictionary<string, string> inputs = LoadGenHostProcessTests.CreateInputs(output);
        if (value == "<missing>") inputs.Remove(key);
        else inputs[key] = value;
        string[] args = inputs.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
        var telemetry = new RecordingTelemetryRegistration();
        int serviceConfigurationCalls = 0;

        Assert.ThrowsAny<Exception>(() => Program.BuildHostCore(args, _ => serviceConfigurationCalls++,
            telemetry.Register));

        Assert.Equal(0, telemetry.RegistrationCount);
        Assert.Equal(0, serviceConfigurationCalls);
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("publication")]
    [InlineData("stop")]
    [InlineData("dispose")]
    [InlineData("receipt")]
    [InlineData("receipt-timeout")]
    [InlineData("watchdog-dispose")]
    [InlineData("cancel")]
    [InlineData("start")]
    public async Task Given_ProcessLifecycle_When_Completing_Then_BlobReceiptRequiresPublicationStopDisposalAndWatchdogCleanup(string outcome)
    {
        var publisher = new RecordingPublisher(throwOnPublish: outcome == "publication");
        var lifecycle = new RecordingLifecycle(throwOnCompletion: outcome is "receipt" or "receipt-timeout");
        var clock = new TrackingTimeProvider(throwOnWatchdogDispose: outcome == "watchdog-dispose");
        var host = new LifecycleHost(CreateHost(publisher, durationSeconds: 1, lifecycle: lifecycle, timeProvider: clock), outcome);
        using var output = new StringWriter();
        lifecycle.OnCompletion = context =>
        {
            Assert.True(host.Disposed);
            Assert.True(publisher.Called);
            Assert.True(clock.AllTimersDisposed);
            Assert.Equal(publisher.RunId, context.Identity.RunId);
        };
        int exitCode = await Program.RunAsync([], completionOutput: output, hostFactory: () => host);
        Assert.Equal(outcome is not ("start" or "cancel"), publisher.Called);
        Assert.True(host.Disposed);
        Assert.Equal(outcome == "success" ? 0 : 1, exitCode);
        Assert.Equal(outcome is "success" or "receipt" or "receipt-timeout", lifecycle.CompletionAttempted);
        Assert.Equal(string.Empty, output.ToString());
        DeleteOutput(publisher);
    }

    private sealed class LifecycleHost(IHost inner, string outcome) : IHost
    {
        internal bool Disposed { get; private set; }
        public IServiceProvider Services => inner.Services;
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (outcome == "start") throw new InvalidOperationException("synthetic_start_failure");
            await inner.StartAsync(cancellationToken);
            if (outcome == "cancel") await inner.StopAsync(cancellationToken);
        }
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await inner.StopAsync(cancellationToken);
            if (outcome == "stop") throw new InvalidOperationException("synthetic_stop_failure");
        }
        public void Dispose()
        {
            inner.Dispose();
            Disposed = true;
            if (outcome == "dispose") throw new InvalidOperationException("synthetic_dispose_failure");
        }
    }

    private static IHost CreateHost(IRunArtifactPublisher publisher, int durationSeconds,
        ProducerFactoryTracker? producerFactory = null,
        Action<KafkaOAuthBearerSession, RunEvidenceSession>? authenticationCallback = null,
        IKafkaTokenProvider? tokenProvider = null, string phase = "C0", string configurationId = "C0",
        int repeatNumber = 1, IProducerRunLifecycle? lifecycle = null,
        TimeProvider? timeProvider = null,
        RecordingTelemetryRegistration? telemetryRegistration = null,
        ILogger<LoadGenWorker>? workerLogger = null)
    {
        string output = Path.Combine(Path.GetTempPath(), "loadgen-host-run-" + Guid.NewGuid().ToString("N"));
        if (publisher is RecordingPublisher recording) recording.OutputRoot = output;
        if (publisher is BlockingPublisher blocking) blocking.OutputRoot = output;
        Dictionary<string, string> values = LoadGenHostProcessTests.CreateInputs(output);
        values["LoadGen__RunId"] = "host-run-" + Guid.NewGuid().ToString("N")[..20];
        values["LoadGen__ConfigurationId"] = configurationId;
        values["LoadGen__Phase"] = phase;
        values["LoadGen__RepeatNumber"] = repeatNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        values["LoadGen__JobName"] = $"{values["LoadGen__RunId"]}-{phase.ToLowerInvariant()}-r{repeatNumber}-loadgen";
        if (publisher is RecordingPublisher runPublisher) runPublisher.RunId = values["LoadGen__RunId"];
        values["LoadGen__ProcessLifetimeTimeout"] = "00:00:15";
        values["LoadGen__StatisticsCompletionTimeout"] = "00:00:01";
        values["LoadGen__CleanupTimeout"] = "00:00:01";
        values["LoadGen__ProducerDrainTimeout"] = "00:00:01";
        values["LoadGen__EvidenceCompletionTimeout"] = "00:00:01";
        values["LoadGen__MaxAttempts"] = "2";
        values["LoadGen__MaxScheduleLateness"] = "00:00:05";
        values["AZURE_TENANT_ID"] = "synthetic-tenant";
        values["AZURE_CLIENT_ID"] = "synthetic-credential-app";
        values["AZURE_FEDERATED_TOKEN_FILE"] = Path.Combine(Path.GetTempPath(), "synthetic-projected-token");
        string[] args = values.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
        telemetryRegistration ??= new RecordingTelemetryRegistration();

        return Program.BuildHostCore(args, services =>
        {
            if (workerLogger is not null) services.AddSingleton(workerLogger);
            services.RemoveAll<IScenarioSource>();
            services.AddSingleton<IScenarioSource>(new ScenarioMemorySource(
                ("scenarios/A01/scenario.yaml", Scenario()),
                ("scenarios/profiles/synthetic.yaml", Profile(durationSeconds))));
            services.RemoveAll<IKafkaTokenProvider>();
            services.AddSingleton<IKafkaTokenProvider>(tokenProvider ?? new SyntheticTokenProvider());
            services.RemoveAll<IRunArtifactPublisher>();
            services.AddSingleton<IRunArtifactPublisher>(publisher);
            services.RemoveAll<IProducerRunLifecycle>();
            services.AddSingleton(lifecycle ?? new RecordingLifecycle());
            if (timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(timeProvider);
                if (timeProvider is TrackingTimeProvider)
                {
                    // This wrapper delegates both timestamp APIs to TimeProvider.System; keep the
                    // production monotonic-clock capability enabled for this lifecycle fixture.
                    services.RemoveAll<KafkaStatisticsBridgeFactory>();
                    services.AddSingleton(provider => new KafkaStatisticsBridgeFactory(
                        provider.GetRequiredService<IOptions<KafkaStatisticsOptions>>().Value,
                        provider.GetRequiredService<TimeProvider>(),
                        provider.GetRequiredService<IKafkaStatisticsEvidenceSink>(),
                        provider.GetRequiredService<KafkaStatisticsMetrics>(), nativeClockCompatible: true));
                }
            }
            services.RemoveAll<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>>>();
            services.AddSingleton<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>>>(
                _ => (configuration, statistics, evidence, authentication) =>
                {
                    if (producerFactory is not null) producerFactory.Called = true;
                    if (authenticationCallback is not null)
                        authenticationCallback(authentication, evidence);
                    IProducer<byte[], byte[]> producer = DispatchProxy.Create<IProducer<byte[], byte[]>, SuccessfulProducer>();
                    ((SuccessfulProducer)(object)producer).Statistics = statistics;
                    return producer;
                });
        }, telemetryRegistration.Register);
    }

    private static string Scenario() => """
        schema_version: 1
        id: A01
        baseline: Parchwell.Kafka
        roles: [LoadGenProducer]
        profile: profiles/synthetic.yaml
        configurations: {C0: {}}
        fault:
          live: {kind: none}
          component: {kind: none}
        repeats: 5
        gate: {kind: none}
        measures:
          - {id: ledger.delivery_latency_ms, role: LoadGenProducer}
        """;

    private static string Profile(int durationSeconds) => $$"""
        schema_version: 1
        id: synthetic
        seed: -17
        tenants: [{id: tenant-a, weight: 1}]
        documents_per_burst: 1
        payloads:
          uploaded: [{size_bytes: 4, weight: 1}]
          ocr-started: [{size_bytes: 4, weight: 1}]
          ocr-finished: [{size_bytes: 4, weight: 1}]
          classified: [{size_bytes: 4, weight: 1}]
          indexed: [{size_bytes: 4, weight: 1}]
        stages: [{id: synthetic, offered_attempts_per_second: 1, duration_seconds: {{durationSeconds}}}]
        """;

    private static void DeleteOutput(RecordingPublisher publisher)
    {
        if (Directory.Exists(publisher.OutputRoot)) Directory.Delete(publisher.OutputRoot, recursive: true);
    }

    private sealed class ScenarioMemorySource(params (string Path, string Text)[] documents) : IScenarioSource
    {
        private readonly Dictionary<string, string> _documents = documents.ToDictionary(
            item => item.Path, item => item.Text, StringComparer.Ordinal);

        public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_documents.TryGetValue(logicalPath, out string? contents))
                throw new FileNotFoundException("synthetic_scenario_input_missing");
            return ValueTask.FromResult<TextReader>(new StringReader(contents));
        }
    }

    private sealed class ProducerFactoryTracker
    {
        public bool Called { get; set; }
    }

    private sealed record RecordedWorkerLog(int EventId, LogLevel Level, Dictionary<string, object?> State, string Message, Exception? Exception);

    private sealed class RecordingWorkerLogger : ILogger<LoadGenWorker>
    {
        public List<RecordedWorkerLog> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var fields = ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            Entries.Add(new(eventId.Id, logLevel, fields, formatter(state, exception), exception));
        }
    }

    private sealed class SyntheticTokenProvider : IKafkaTokenProvider
    {
        private int _cancellationObserved;
        public bool CancellationObserved => Volatile.Read(ref _cancellationObserved) != 0;

        public Azure.Core.AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => Interlocked.Exchange(ref _cancellationObserved, 1));
            cancellationToken.ThrowIfCancellationRequested();
            return new Azure.Core.AccessToken("synthetic-token", DateTimeOffset.MaxValue);
        }
    }

    private sealed class ThrowingTokenProvider : IKafkaTokenProvider
    {
        public Azure.Core.AccessToken GetToken(string scope, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("synthetic-credential-secret");
    }

    private sealed class FailOnSecondTokenProvider : IKafkaTokenProvider
    {
        private int _calls;
        public Azure.Core.AccessToken GetToken(string scope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref _calls) > 1)
                throw new InvalidOperationException("synthetic-refresh-secret");
            return new Azure.Core.AccessToken("synthetic-token", DateTimeOffset.MaxValue);
        }
    }

    private sealed class AuthenticationTracker
    {
        public KafkaOAuthBearerSession? Session { get; set; }
        public DateTimeOffset? InstalledExpiry { get; set; }
    }

    private sealed class RecordingLifecycle(bool throwOnCompletion = false) : IProducerRunLifecycle
    {
        public bool Registered { get; private set; }
        public bool CompletionAttempted { get; private set; }
        public ProducerRunContext? CompletionContext { get; private set; }
        public Action<ProducerRunContext>? OnCompletion { get; set; }

        public Task RegisterAsync(ProducerRunContext context, ProducerLifecycleOptions options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Registered = true;
            return Task.CompletedTask;
        }

        public Task PublishDiagnosticsAsync(ProducerRunContext context, RunExecutionResult? result,
            ProducerLifecycleOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task WriteCompletionAsync(ProducerRunContext context, ProducerLifecycleOptions options,
            CancellationToken cancellationToken)
        {
            CompletionAttempted = true;
            CompletionContext = context;
            OnCompletion?.Invoke(context);
            if (throwOnCompletion) throw new IOException("synthetic_blob_receipt_failure");
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingTimeProvider(bool throwOnWatchdogDispose = false) : TimeProvider
    {
        private readonly List<TrackingTimer> _timers = [];
        public bool AllTimersDisposed => _timers.Count > 0 && _timers.All(timer => timer.Disposed);
        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow();
        public override long GetTimestamp() => TimeProvider.System.GetTimestamp();
        public override long TimestampFrequency => TimeProvider.System.TimestampFrequency;
        public override TimeZoneInfo LocalTimeZone => TimeProvider.System.LocalTimeZone;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new TrackingTimer(TimeProvider.System.CreateTimer(callback, state, dueTime, period),
                throwOnWatchdogDispose && dueTime == TimeSpan.FromSeconds(15));
            _timers.Add(timer);
            return timer;
        }
    }

    private sealed class TrackingTimer(ITimer inner, bool throwOnDispose) : ITimer
    {
        private int _disposed;
        public bool Disposed => Volatile.Read(ref _disposed) != 0;
        public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(dueTime, period);
        public void Dispose()
        {
            Interlocked.Exchange(ref _disposed, 1);
            inner.Dispose();
            if (throwOnDispose) throw new InvalidOperationException("synthetic_watchdog_dispose_failure");
        }
        public async ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _disposed, 1);
            await inner.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class RecordingPublisher(bool throwOnPublish = false, bool available = true) : IRunArtifactPublisher
    {
        public bool IsAvailable => available;
        public bool Called { get; private set; }
        public bool SawLedgerAndManifest { get; private set; }
        public string OutputRoot { get; set; } = string.Empty;
        public string RunId { get; set; } = string.Empty;

        public Task PublishAsync(Parchwell.LoadGen.Run.Execution.RunExecutionResult result, RunPublicationOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Called = true;
            string ledgerPath = result.ArtifactPaths["ledger"];
            string manifestPath = result.ArtifactPaths["manifest"];
            int ledgerRows = File.Exists(ledgerPath) ? File.ReadAllLines(ledgerPath).Length : -1;
            SawLedgerAndManifest = File.Exists(ledgerPath) && File.Exists(manifestPath) &&
                result.Manifest.BookkeepingComplete && ledgerRows == result.Manifest.AttemptCount;
            if (throwOnPublish) throw new InvalidOperationException("synthetic_upload_failure");
            return Task.CompletedTask;
        }
    }

    public class SuccessfulProducer : DispatchProxy
    {
        public KafkaStatisticsSession? Statistics { get; set; }
        private int _statisticsScheduled;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                var callback = (Action<DeliveryReport<byte[], byte[]>>)args[2]!;
                callback(new DeliveryReport<byte[], byte[]>
                {
                    Error = new Error(ErrorCode.NoError),
                    Status = PersistenceStatus.Persisted
                });
                return null;
            }
            if (targetMethod?.Name == "Flush")
            {
                if (Statistics is not null && Interlocked.Exchange(ref _statisticsScheduled, 1) == 0)
                {
                    KafkaStatisticsSession statistics = Statistics!;
                    _ = Task.Run(async () =>
                    {
                        long creation = (long)(Stopwatch.GetTimestamp() * 1_000_000d / Stopwatch.Frequency);
                        for (int index = 0; index < 2; index++)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(index == 0 ? 250 : 20)).ConfigureAwait(false);
                            long timestamp = (long)(Stopwatch.GetTimestamp() * 1_000_000d / Stopwatch.Frequency);
                            long age = timestamp - creation;
                            long time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            string report = "{\"name\":\"worker-producer\",\"client_id\":\"worker-pod\",\"type\":\"producer\",\"ts\":" +
                                timestamp.ToString(CultureInfo.InvariantCulture) + ",\"time\":" + time.ToString(CultureInfo.InvariantCulture) +
                                ",\"age\":" + age.ToString(CultureInfo.InvariantCulture) + ",\"brokers\":{\"broker-1\":{\"name\":\"broker-1\",\"nodeid\":1,\"state\":\"UP\",\"stateage\":1," +
                                "\"req_timeouts\":0,\"disconnects\":0,\"txretries\":0,\"txerrs\":0,\"rxerrs\":0,\"throttle\":{\"cnt\":1,\"max\":0},\"rtt\":{\"cnt\":0,\"p99\":0}}}}";
                            statistics.Capture(report);
                        }
                    });
                }
                return 0;
            }
            if (targetMethod?.Name == "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private sealed class BlockingPublisher : IRunArtifactPublisher
    {
        public bool IsAvailable => true;
        public bool Called { get; private set; }
        public string OutputRoot { get; set; } = string.Empty;
        public TaskCompletionSource PublishStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowReturn { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task PublishAsync(Parchwell.LoadGen.Run.Execution.RunExecutionResult result, RunPublicationOptions options,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            PublishStarted.TrySetResult();
            using CancellationTokenRegistration registration = cancellationToken.Register(() => CancellationObserved.TrySetResult());
            await AllowReturn.Task.ConfigureAwait(false);
        }
    }

    private static void DeleteOutput(BlockingPublisher publisher)
    {
        if (Directory.Exists(publisher.OutputRoot)) Directory.Delete(publisher.OutputRoot, recursive: true);
    }
}
#pragma warning restore CA1707
