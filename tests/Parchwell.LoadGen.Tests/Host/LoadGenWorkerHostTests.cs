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
            Assert.True(host.Services.GetRequiredService<LoadGenWorker>().ExitCode == 0,
                string.Join(';', publisher.FailureDiagnostics));
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
    [InlineData("LoadGen__Phase", "C3", "OptionsValidationException", "LoadGen host inputs or finite budgets are invalid.", "LoadGenHostOptions")]
    [InlineData("LoadGen__RepeatNumber", "0", "OptionsValidationException", "LoadGen host inputs or finite budgets are invalid.", "LoadGenHostOptions")]
    [InlineData("LoadGen__RepeatNumber", "6", "OptionsValidationException", "LoadGen host inputs or finite budgets are invalid.", "LoadGenHostOptions")]
    [InlineData("LoadGen__JobName", "wrong-job", "OptionsValidationException", "LoadGen host inputs or finite budgets are invalid.", "LoadGenHostOptions")]
    [InlineData("LoadGen__JobName", "HOST-RUN-c0-r1-loadgen", "OptionsValidationException", "LoadGen host inputs or finite budgets are invalid.", "LoadGenHostOptions")]
    [InlineData("LoadGen__RunId", "Uppercase", "OptionsValidationException", "LoadGen host inputs or finite budgets are invalid.", "LoadGenHostOptions")]
    [InlineData("LoadGen__RunId", "this-run-identifier-is-over-29", "OptionsValidationException", "LoadGen host inputs or finite budgets are invalid.", "LoadGenHostOptions")]
    [InlineData("LoadGen__PodUid", "<missing>", "InvalidOperationException", "required_loadgen_setting_missing:PodUid", "")]
    [InlineData("LoadGen__PodUid", "bad uid", "OptionsValidationException", "LoadGen host inputs or finite budgets are invalid.", "LoadGenHostOptions")]
    [InlineData("LoadGen__ApplicationInsightsConnectionString", "", "OptionsValidationException", "LoadGen host inputs or finite budgets are invalid.", "LoadGenHostOptions")]
    [InlineData("LoadGen__ApplicationInsightsConnectionString", "<missing>", "InvalidOperationException", "required_loadgen_setting_missing:ApplicationInsightsConnectionString", "")]
    [InlineData("PARCHWELL_SOURCE_COMMIT", "unknown", "InvalidOperationException", "source_commit_must_be_40_lowercase_hex", "")]
    public void Given_InvalidBootstrapInputs_When_ComposingHost_Then_ValidationPrecedesTelemetryAndWorkerOperations(
        string key, string value, string expectedExceptionType, string expectedMessage, string expectedOptionsType)
    {
        string output = Path.Combine(Path.GetTempPath(), "invalid-registered-worker-" + Guid.NewGuid().ToString("N"));
        Dictionary<string, string> inputs = LoadGenHostProcessTests.CreateInputs(output);
        if (value == "<missing>") inputs.Remove(key);
        else inputs[key] = value;
        string[] args = inputs.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
        var telemetry = new RecordingTelemetryRegistration();
        var lifecycle = new BootstrapLifecycleTracker();
        var producerFactory = new ProducerFactoryTracker();
        var publisher = new BootstrapPublisherTracker();
        int serviceConfigurationCalls = 0;

        Exception failure = Assert.ThrowsAny<Exception>(() => Program.BuildHostCore(args, services =>
        {
            serviceConfigurationCalls++;
            services.AddSingleton<IProducerRunLifecycle>(lifecycle);
            services.AddSingleton<IRunArtifactPublisher>(publisher);
            services.AddSingleton<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession,
                IProducer<byte[], byte[]>>>(_ => (_, _, _, _) =>
            {
                producerFactory.Calls++;
                throw new InvalidOperationException("bootstrap_test_producer_factory_must_not_run");
            });
        }, telemetry.Register));

        Assert.Equal(expectedExceptionType, failure.GetType().Name);
        Assert.Equal(expectedMessage, failure.Message);
        if (failure is OptionsValidationException validation)
        {
            Assert.Equal(expectedOptionsType, validation.OptionsType.Name);
            Assert.Equal([expectedMessage], validation.Failures);
        }
        Assert.Equal(0, serviceConfigurationCalls);
        Assert.Equal(0, telemetry.RegistrationCount);
        Assert.Equal(0, lifecycle.RegisterCalls);
        Assert.Equal(0, lifecycle.DiagnosticsCalls);
        Assert.Equal(0, lifecycle.CompletionCalls);
        Assert.Equal(0, producerFactory.Calls);
        Assert.Equal(0, publisher.Calls);
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData("AZURE_TENANT_ID", "<missing>", "TenantId is required.")]
    [InlineData("AZURE_TENANT_ID", "", "TenantId is required.")]
    [InlineData("AZURE_TENANT_ID", " ", "TenantId is required.")]
    [InlineData("AZURE_CLIENT_ID", "<missing>", "ClientId is required.")]
    [InlineData("AZURE_CLIENT_ID", "", "ClientId is required.")]
    [InlineData("AZURE_CLIENT_ID", " ", "ClientId is required.")]
    [InlineData("AZURE_FEDERATED_TOKEN_FILE", "<missing>", "TokenFilePath is required.")]
    [InlineData("AZURE_FEDERATED_TOKEN_FILE", "", "TokenFilePath is required.")]
    [InlineData("AZURE_FEDERATED_TOKEN_FILE", " ", "TokenFilePath is required.")]
    public void Given_InvalidWorkloadIdentity_When_ComposingHost_Then_ValidationPrecedesTelemetryAndWorkerOperations(
        string key, string value, string expectedFailure)
    {
        string output = Path.Combine(Path.GetTempPath(), "invalid-identity-worker-" + Guid.NewGuid().ToString("N"));
        Dictionary<string, string> inputs = LoadGenHostProcessTests.CreateInputs(output);
        if (value == "<missing>") inputs.Remove(key);
        else inputs[key] = value;
        string[] args = inputs.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
        var telemetry = new RecordingTelemetryRegistration();
        var lifecycle = new BootstrapLifecycleTracker();
        var producerFactory = new ProducerFactoryTracker();
        var publisher = new BootstrapPublisherTracker();
        int serviceConfigurationCalls = 0;

        OptionsValidationException failure = Assert.Throws<OptionsValidationException>(() =>
            Program.BuildHostCore(args, services =>
            {
                serviceConfigurationCalls++;
                services.AddSingleton<IProducerRunLifecycle>(lifecycle);
                services.AddSingleton<IRunArtifactPublisher>(publisher);
                services.AddSingleton<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession,
                    IProducer<byte[], byte[]>>>(_ => (_, _, _, _) =>
                {
                    producerFactory.Calls++;
                    throw new InvalidOperationException("bootstrap_test_producer_factory_must_not_run");
                });
            }, telemetry.Register));

        Assert.Equal(typeof(KafkaWorkloadIdentityOptions), failure.OptionsType);
        Assert.Equal([expectedFailure], failure.Failures);
        Assert.Equal(0, telemetry.RegistrationCount);
        Assert.Equal(0, serviceConfigurationCalls);
        Assert.Equal(0, lifecycle.RegisterCalls);
        Assert.Equal(0, lifecycle.DiagnosticsCalls);
        Assert.Equal(0, lifecycle.CompletionCalls);
        Assert.Equal(0, producerFactory.Calls);
        Assert.Equal(0, publisher.Calls);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task Given_NativeProducerDisposeStalls_When_ProgramRuns_Then_NoCompletionReceiptBeforeOrAfterLateDispose()
    {
        var publisher = new RecordingPublisher();
        var lifecycle = new RecordingLifecycle();
        var producerFactory = new ProducerFactoryTracker { StallOnDispose = true };
        IHost host = CreateHost(publisher, durationSeconds: 1, producerFactory: producerFactory, lifecycle: lifecycle);
        host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value.CleanupTimeout = TimeSpan.FromMilliseconds(100);
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        int exitCode = await Program.RunAsync([], completionOutput: output, hostFactory: () => host);

        SuccessfulProducer producer = (SuccessfulProducer)(object)producerFactory.Producer!;
        Assert.Equal(1, exitCode);
        Assert.Equal(1, producer.DisposeAttempts);
        Assert.Contains("producer_dispose_timeout", lifecycle.DiagnosticCodes);
        Assert.False(lifecycle.CompletionAttempted);
        Assert.False(File.Exists(Path.Combine(publisher.OutputRoot, "completion.json")));
        Assert.DoesNotContain("PARCHWELL_COMPLETION", output.ToString(), StringComparison.Ordinal);

        producer.ReleaseDispose.TrySetResult();
        await producer.DisposeFinished.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, producer.DisposeAttempts);
        Assert.False(lifecycle.CompletionAttempted);
        Assert.False(File.Exists(Path.Combine(publisher.OutputRoot, "completion.json")));
        Assert.DoesNotContain("PARCHWELL_COMPLETION", output.ToString(), StringComparison.Ordinal);
        DeleteOutput(publisher);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("publication")]
    [InlineData("stop")]
    [InlineData("stop-stall")]
    [InlineData("dispose")]
    [InlineData("receipt")]
    [InlineData("receipt-timeout")]
    [InlineData("watchdog-dispose")]
    [InlineData("cancel")]
    [InlineData("start")]
    public async Task Given_ProcessLifecycle_When_Completing_Then_BlobReceiptRequiresPublicationStopDisposalAndWatchdogCleanup(string outcome)
    {
        var publisher = new RecordingPublisher(throwOnPublish: outcome == "publication");
        var lifecycle = new RecordingLifecycle(throwOnCompletion: outcome == "receipt",
            waitForCompletionCancellation: outcome == "receipt-timeout");
        var clock = new TrackingTimeProvider(throwOnWatchdogDispose: outcome == "watchdog-dispose");
        var host = new LifecycleHost(CreateHost(publisher, durationSeconds: 1, lifecycle: lifecycle, timeProvider: clock), outcome);
        if (outcome == "stop-stall")
            host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value.CleanupTimeout = TimeSpan.FromMilliseconds(100);
        using var output = new StringWriter();
        var stopwatch = Stopwatch.StartNew();
        lifecycle.OnCompletion = context =>
        {
            Assert.True(host.Disposed);
            Assert.True(publisher.Called);
            Assert.True(clock.WatchdogDisposed);
            Assert.Equal(publisher.RunId, context.Identity.RunId);
        };
        int exitCode = await Program.RunAsync([], completionOutput: output, hostFactory: () => host);
        stopwatch.Stop();
        if (outcome == "stop-stall") await host.DisposeFinished.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal(outcome is not ("start" or "cancel"), publisher.Called);
        Assert.True(host.Disposed);
        Assert.Equal(1, host.StopAttempts);
        if (outcome == "stop-stall") Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.Equal(outcome == "success" ? 0 : 1, exitCode);
        Assert.Equal(outcome is "success" or "receipt" or "receipt-timeout", lifecycle.CompletionAttempted);
        Assert.Equal(outcome == "receipt-timeout", lifecycle.CompletionDeadlineObserved);
        Assert.Equal(string.Empty, output.ToString());
        DeleteOutput(publisher);
    }

    private sealed class LifecycleHost(IHost inner, string outcome) : IHost
    {
        internal bool Disposed { get; private set; }
        internal int StopAttempts { get; private set; }
        internal TaskCompletionSource DisposeFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IServiceProvider Services => inner.Services;
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (outcome == "start") throw new InvalidOperationException("synthetic_start_failure");
            await inner.StartAsync(cancellationToken);
            if (outcome == "cancel") await inner.StopAsync(cancellationToken);
        }
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopAttempts++;
            if (outcome == "stop-stall") await Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None);
            await inner.StopAsync(cancellationToken);
            if (outcome == "stop") throw new InvalidOperationException("synthetic_stop_failure");
        }
        public void Dispose()
        {
            inner.Dispose();
            Disposed = true;
            DisposeFinished.TrySetResult();
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
        ProducerFactoryTracker syntheticProducerFactory = producerFactory ?? new ProducerFactoryTracker();
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
            }
            // Default and tracking clocks follow TimeProvider.System on Linux, so preserve the
            // native monotonic-clock contract while installing the deterministic report fixture.
            if (timeProvider is null or TrackingTimeProvider)
            {
                services.RemoveAll<KafkaStatisticsBridgeFactory>();
                services.AddSingleton(provider => new KafkaStatisticsBridgeFactory(
                    provider.GetRequiredService<IOptions<KafkaStatisticsOptions>>().Value,
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<IKafkaStatisticsEvidenceSink>(),
                    provider.GetRequiredService<KafkaStatisticsMetrics>(), nativeClockCompatible: true,
                    completionBoundaryObserver: () => syntheticProducerFactory.EmitPostDrainStatistics?.Invoke()));
            }
            services.RemoveAll<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>>>();
            services.AddSingleton<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>>>(
                _ => (configuration, statistics, evidence, authentication) =>
                {
                    syntheticProducerFactory.Called = true;
                    syntheticProducerFactory.Calls++;
                    if (authenticationCallback is not null)
                        authenticationCallback(authentication, evidence);
                    IProducer<byte[], byte[]> producer = DispatchProxy.Create<IProducer<byte[], byte[]>, SuccessfulProducer>();
                    SuccessfulProducer syntheticProducer = (SuccessfulProducer)(object)producer;
                    syntheticProducer.Statistics = statistics;
                    if (syntheticProducerFactory.StallOnDispose) syntheticProducer.StallDispose();
                    syntheticProducerFactory.Producer = producer;
                    syntheticProducerFactory.EmitPostDrainStatistics = syntheticProducer.EmitPostDrainStatistics;
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
        public int Calls { get; set; }
        public bool StallOnDispose { get; init; }
        public IProducer<byte[], byte[]>? Producer { get; set; }
        public Action? EmitPostDrainStatistics { get; set; }
    }

    private sealed class BootstrapLifecycleTracker : IProducerRunLifecycle
    {
        public int RegisterCalls { get; private set; }
        public int DiagnosticsCalls { get; private set; }
        public int CompletionCalls { get; private set; }

        public Task RegisterAsync(ProducerRunContext context, ProducerLifecycleOptions options, CancellationToken cancellationToken)
        {
            RegisterCalls++;
            return Task.CompletedTask;
        }

        public Task PublishDiagnosticsAsync(ProducerRunContext context, RunExecutionResult? result,
            ProducerLifecycleOptions options, CancellationToken cancellationToken)
        {
            DiagnosticsCalls++;
            return Task.CompletedTask;
        }

        public Task WriteCompletionAsync(ProducerRunContext context, ProducerLifecycleOptions options,
            CancellationToken cancellationToken)
        {
            CompletionCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class BootstrapPublisherTracker : IRunArtifactPublisher
    {
        public int Calls { get; private set; }
        public bool IsAvailable => true;

        public Task PublishAsync(RunExecutionResult result, RunPublicationOptions options,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
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

    private sealed class RecordingLifecycle(bool throwOnCompletion = false, bool waitForCompletionCancellation = false) : IProducerRunLifecycle
    {
        public bool Registered { get; private set; }
        public int RegisterCalls { get; private set; }
        public int DiagnosticsCalls { get; private set; }
        public int CompletionCalls { get; private set; }
        public bool CompletionAttempted { get; private set; }
        public bool CompletionDeadlineObserved { get; private set; }
        public ProducerRunContext? CompletionContext { get; private set; }
        public Action<ProducerRunContext>? OnCompletion { get; set; }
        public List<string> DiagnosticCodes { get; } = [];

        public Task RegisterAsync(ProducerRunContext context, ProducerLifecycleOptions options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RegisterCalls++;
            Registered = true;
            return Task.CompletedTask;
        }

        public Task PublishDiagnosticsAsync(ProducerRunContext context, RunExecutionResult? result,
            ProducerLifecycleOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiagnosticsCalls++;
            if (result is not null) DiagnosticCodes.AddRange(result.Manifest.Diagnostics);
            return Task.CompletedTask;
        }

        public async Task WriteCompletionAsync(ProducerRunContext context, ProducerLifecycleOptions options,
            CancellationToken cancellationToken)
        {
            CompletionAttempted = true;
            CompletionCalls++;
            CompletionContext = context;
            OnCompletion?.Invoke(context);
            if (throwOnCompletion) throw new IOException("synthetic_blob_receipt_failure");
            if (waitForCompletionCancellation)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    CompletionDeadlineObserved = true;
                    throw;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class TrackingTimeProvider(bool throwOnWatchdogDispose = false) : TimeProvider
    {
        private readonly List<TrackingTimer> _timers = [];
        public bool WatchdogDisposed => _timers.Any(timer => timer.IsWatchdog && timer.Disposed);
        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow();
        public override long GetTimestamp() => TimeProvider.System.GetTimestamp();
        public override long TimestampFrequency => TimeProvider.System.TimestampFrequency;
        public override TimeZoneInfo LocalTimeZone => TimeProvider.System.LocalTimeZone;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new TrackingTimer(TimeProvider.System.CreateTimer(callback, state, dueTime, period),
                throwOnWatchdogDispose && dueTime == TimeSpan.FromSeconds(15), dueTime == TimeSpan.FromSeconds(15));
            _timers.Add(timer);
            return timer;
        }
    }

    private sealed class TrackingTimer(ITimer inner, bool throwOnDispose, bool isWatchdog) : ITimer
    {
        private int _disposed;
        public bool Disposed => Volatile.Read(ref _disposed) != 0;
        public bool IsWatchdog => isWatchdog;
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
        public List<string> FailureDiagnostics { get; } = [];
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
            FailureDiagnostics.AddRange(result.Manifest.Diagnostics);
            if (throwOnPublish) throw new InvalidOperationException("synthetic_upload_failure");
            return Task.CompletedTask;
        }
    }

    public class SuccessfulProducer : DispatchProxy
    {
        public KafkaStatisticsSession? Statistics { get; set; }
        private TaskCompletionSource? _releaseDispose;
        private int _disposeAttempts;
        public int DisposeAttempts => Volatile.Read(ref _disposeAttempts);
        public TaskCompletionSource DisposeFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDispose => _releaseDispose ??= new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void StallDispose() => _releaseDispose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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
                return 0;
            }
            if (targetMethod?.Name == "Dispose")
            {
                if (_releaseDispose is not null)
                {
                    Interlocked.Increment(ref _disposeAttempts);
                    try { _releaseDispose.Task.GetAwaiter().GetResult(); }
                    finally { DisposeFinished.TrySetResult(); }
                }
                return null;
            }
            throw new NotSupportedException(targetMethod?.Name);
        }

        private static long MicrosecondsNow() => (long)((Int128)Stopwatch.GetTimestamp() * 1_000_000 / Stopwatch.Frequency);

        public void EmitPostDrainStatistics()
        {
            KafkaStatisticsSession statistics = Statistics ?? throw new InvalidOperationException("synthetic_statistics_unavailable");
            // KafkaStatisticsSession counts only a report whose ts is strictly after its rounded-up
            // completion boundary. This runs just after that boundary, and a report in the same
            // microsecond would be rejected, so wait two microsecond ticks past entry.
            long earliest = MicrosecondsNow() + 2;
            long timestamp;
            while ((timestamp = MicrosecondsNow()) < earliest) Thread.SpinWait(1);
            long time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string report = "{\"name\":\"worker-producer\",\"client_id\":\"worker-pod\",\"type\":\"producer\",\"ts\":" +
                timestamp.ToString(CultureInfo.InvariantCulture) + ",\"time\":" + time.ToString(CultureInfo.InvariantCulture) +
                ",\"age\":0,\"brokers\":{\"broker-1\":{\"name\":\"broker-1\",\"nodeid\":1,\"state\":\"UP\",\"stateage\":1," +
                "\"req_timeouts\":0,\"disconnects\":0,\"txretries\":0,\"txerrs\":0,\"rxerrs\":0,\"throttle\":{\"cnt\":1,\"max\":0},\"rtt\":{\"cnt\":0,\"p99\":0}}}}";
            statistics.Capture(report);
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
