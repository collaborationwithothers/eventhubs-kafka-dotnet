using System.Reflection;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.LoadGen;
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
    [InlineData(null, null, null)]
    [InlineData("generated-alias.yaml", null, "generated-alias.yaml")]
    [InlineData(null, "generated-section.yaml", "generated-section.yaml")]
    [InlineData("generated-alias.yaml", "generated-section.yaml", "generated-alias.yaml")]
    public void Given_OptionalComparisonSettings_When_BindingHostOptions_Then_AliasOverridesSectionAndAbsenceRemainsOptional(
        string? aliasPath, string? sectionPath, string? expectedPath)
    {
        var publisher = new RecordingPublisher();
        using IHost host = CreateHost(publisher, durationSeconds: 1,
            generatedValuesFile: aliasPath, generatedValuesSectionFile: sectionPath);

        Assert.Equal(expectedPath, host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value.GeneratedValuesFile);
        Assert.False(Directory.Exists(publisher.OutputRoot));
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, " ")]
    [InlineData(true, " ")]
    [InlineData(false, "generated\nvalues.yaml")]
    [InlineData(true, "generated\nvalues.yaml")]
    [InlineData(false, "generated\0values.yaml")]
    [InlineData(true, "generated\0values.yaml")]
    public async Task Given_InvalidOptionalComparisonPath_When_StartingHost_Then_ValidationPrecedesProducerPublicationAndArtifacts(
        bool useSection, string path)
    {
        var publisher = new RecordingPublisher();
        var tracker = new ProducerFactoryTracker();
        using IHost host = CreateHost(publisher, durationSeconds: 1, tracker,
            generatedValuesFile: useSection ? null : path, generatedValuesSectionFile: useSection ? path : null);

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        Assert.False(tracker.Called);
        Assert.False(publisher.Called);
        Assert.False(Directory.Exists(publisher.OutputRoot));
    }

    [Theory]
    [InlineData("matching")]
    [InlineData("mismatching")]
    [InlineData("missing")]
    public async Task Given_OptionalComparisonFile_When_WorkerResolvesItsActualInputs_Then_FidelityFailurePrecedesNativeProducerAndArtifacts(string state)
    {
        string path = Path.Combine(Path.GetTempPath(), "worker-values-" + Guid.NewGuid().ToString("N"));
        var publisher = new RecordingPublisher();
        var tracker = new ProducerFactoryTracker();
        using IHost host = CreateHost(publisher, durationSeconds: 1, tracker, generatedValuesFile: path);
        try
        {
            if (state == "matching")
            {
                LoadedScenario loaded = await host.Services.GetRequiredService<ScenarioLoader>().LoadAsync("A01", TestContext.Current.CancellationToken);
                ResolvedScenario resolved = host.Services.GetRequiredService<ScenarioResolver>().Resolve(loaded);
                File.WriteAllText(path, new HelmValuesGenerator().Generate(resolved, "C0"), new System.Text.UTF8Encoding(false));
            }
            else if (state == "mismatching") File.WriteAllText(path, "profile: changed\n");
            await host.StartAsync(TestContext.Current.CancellationToken);
            await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);
            Assert.Equal(state == "matching" ? 0 : 1, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
            Assert.Equal(state == "matching", tracker.Called);
            Assert.Equal(state == "matching", publisher.Called);
            Assert.Equal(state == "matching", Directory.Exists(publisher.OutputRoot));
        }
        finally
        {
            File.Delete(path);
            DeleteOutput(publisher);
        }
    }

    [Theory]
    [InlineData("success")]
    [InlineData("publication")]
    [InlineData("stop")]
    [InlineData("dispose")]
    [InlineData("write")]
    [InlineData("cancel")]
    [InlineData("start")]
    public async Task Given_ProcessLifecycle_When_Completing_Then_ReceiptRequiresPublicationStopDisposalAndWritableOutput(string outcome)
    {
        var publisher = new RecordingPublisher(throwOnPublish: outcome == "publication");
        var host = new LifecycleHost(CreateHost(publisher, durationSeconds: 1), outcome);
        using var output = new CompletionWriter(host, publisher, failWrite: outcome == "write");
        int exitCode = await Program.RunAsync([], completionOutput: output, hostFactory: () => host);
        Assert.Equal(outcome is not ("start" or "cancel"), publisher.Called);
        Assert.True(host.Disposed);
        Assert.Equal(outcome == "success" ? 0 : 1, exitCode);
        if (outcome == "success")
        {
            string line = output.ToString();
            Assert.StartsWith("PARCHWELL_COMPLETION ", line, StringComparison.Ordinal);
            using JsonDocument receipt = JsonDocument.Parse(line["PARCHWELL_COMPLETION ".Length..]);
            Assert.Equal(1, receipt.RootElement.GetProperty("schema_version").GetInt32());
            Assert.Equal("loadgen", receipt.RootElement.GetProperty("mode").GetString());
            Assert.Equal("A01", receipt.RootElement.GetProperty("identity").GetProperty("scenario_id").GetString());
            Assert.Equal(publisher.RunId, receipt.RootElement.GetProperty("identity").GetProperty("run_id").GetString());
            Assert.Equal(0, receipt.RootElement.GetProperty("handler_exit_code").GetInt32());
            Assert.True(receipt.RootElement.GetProperty("cleanup_completed").GetBoolean());
        }
        else Assert.Equal(string.Empty, output.ToString());
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

    private sealed class CompletionWriter(LifecycleHost host, RecordingPublisher publisher, bool failWrite) : StringWriter
    {
        public override Task WriteLineAsync(string? value)
        {
            Assert.True(host.Disposed);
            Assert.True(publisher.Called);
            if (failWrite) throw new IOException("synthetic_receipt_write_failure");
            return base.WriteLineAsync(value);
        }
    }

    private static IHost CreateHost(IRunArtifactPublisher publisher, int durationSeconds,
        ProducerFactoryTracker? producerFactory = null,
        Action<KafkaOAuthBearerSession, RunEvidenceSession>? authenticationCallback = null,
        IKafkaTokenProvider? tokenProvider = null, string? generatedValuesFile = null, string? generatedValuesSectionFile = null)
    {
        string output = Path.Combine(Path.GetTempPath(), "loadgen-host-run-" + Guid.NewGuid().ToString("N"));
        if (publisher is RecordingPublisher recording) recording.OutputRoot = output;
        if (publisher is BlockingPublisher blocking) blocking.OutputRoot = output;
        Dictionary<string, string> values = LoadGenHostProcessTests.CreateInputs(output);
        values["LoadGen__RunId"] = "host-run-" + Guid.NewGuid().ToString("N");
        if (publisher is RecordingPublisher runPublisher) runPublisher.RunId = values["LoadGen__RunId"];
        values["LoadGen__ProcessLifetimeTimeout"] = "00:00:15";
        values["LoadGen__StatisticsCompletionTimeout"] = "00:00:00.050";
        values["LoadGen__CleanupTimeout"] = "00:00:01";
        values["LoadGen__ProducerDrainTimeout"] = "00:00:01";
        values["LoadGen__EvidenceCompletionTimeout"] = "00:00:01";
        values["LoadGen__MaxAttempts"] = "2";
        values["LoadGen__MaxScheduleLateness"] = "00:00:05";
        values["AZURE_TENANT_ID"] = "synthetic-tenant";
        values["AZURE_CLIENT_ID"] = "synthetic-credential-app";
        values["AZURE_FEDERATED_TOKEN_FILE"] = Path.Combine(Path.GetTempPath(), "synthetic-projected-token");
        if (generatedValuesFile is not null) values["PARCHWELL_GENERATED_VALUES_FILE"] = generatedValuesFile;
        if (generatedValuesSectionFile is not null) values["LoadGen__GeneratedValuesFile"] = generatedValuesSectionFile;
        string[] args = values.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();

        return Program.BuildHost(args, services =>
        {
            services.RemoveAll<IScenarioSource>();
            services.AddSingleton<IScenarioSource>(new ScenarioMemorySource(
                ("scenarios/A01/scenario.yaml", Scenario()),
                ("scenarios/profiles/synthetic.yaml", Profile(durationSeconds))));
            services.RemoveAll<IKafkaTokenProvider>();
            services.AddSingleton<IKafkaTokenProvider>(tokenProvider ?? new SyntheticTokenProvider());
            services.RemoveAll<IRunArtifactPublisher>();
            services.AddSingleton<IRunArtifactPublisher>(publisher);
            services.RemoveAll<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>>>();
            services.AddSingleton<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>>>(
                _ => (configuration, statistics, evidence, authentication) =>
                {
                    if (producerFactory is not null) producerFactory.Called = true;
                    if (authenticationCallback is not null)
                        authenticationCallback(authentication, evidence);
                    return DispatchProxy.Create<IProducer<byte[], byte[]>, SuccessfulProducer>();
                });
        });
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
            if (targetMethod?.Name == "Flush") return 0;
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
