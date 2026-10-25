using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Telemetry;
using Parchwell.Kafka.Statistics;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Authentication;
using Parchwell.LoadGen;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class ProducerRegistrationTests
{
    [Theory]
    [InlineData("timeout")]
    [InlineData("cancel")]
    [InlineData("conflict")]
    [InlineData("ambiguous")]
    public async Task Given_RegistrationFailure_When_StartingWorker_Then_ConstructsNoProducerAndWritesNoCompletion(string outcome)
    {
        string output = NewDirectory();
        var lifecycle = new RecordingLifecycle(outcome);
        var producer = new ProducerFactoryTracker();
        try
        {
            using IHost host = CreateHost(output, lifecycle, producer, runSeconds: 1);

            await host.StartAsync(TestContext.Current.CancellationToken);
            await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
            Assert.Equal(1, lifecycle.RegistrationAttempts);
            Assert.False(producer.Called);
            Assert.Equal(0, lifecycle.CompletionAttempts);
            Assert.Equal(0, lifecycle.DiagnosticsAttempts);
            Assert.Null(lifecycle.RegistrationResult);
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task Given_RegistrationSuccess_When_ConstructingProducer_Then_RegistrationAlreadyCompleted()
    {
        string output = NewDirectory();
        var lifecycle = new RecordingLifecycle("success");
        var producer = new ProducerFactoryTracker();
        producer.AssertRegistrationComplete = () => Assert.Equal("success", lifecycle.RegistrationResult);
        try
        {
            IHost host = CreateHost(output, lifecycle, producer, runSeconds: 1);
            using var completionOutput = new StringWriter();
            int exitCode = await Program.RunAsync([], completionOutput: completionOutput, hostFactory: () => host);

            Assert.Equal(0, exitCode);
            Assert.Equal("success", lifecycle.RegistrationResult);
            Assert.True(producer.Called);
            Assert.Equal(1, lifecycle.CompletionAttempts);
            Assert.Equal(string.Empty, completionOutput.ToString());
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task Given_NormalPublicationFails_When_WorkerPublishesDiagnostics_Then_RunFailureAndNoCompletionArePreserved()
    {
        string output = NewDirectory();
        var lifecycle = new RecordingLifecycle("success", diagnosticsFails: true);
        var producer = new ProducerFactoryTracker();
        // The listener is process-wide and other worker tests emit diagnostics in parallel, so
        // only this host's run is recorded.
        string runId = "registration-" + Guid.NewGuid().ToString("N")[..16];
        var diagnosticActivities = new List<ActivityStatusCode>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Parchwell.Workers",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == "loadgen.diagnostics" && Equals(activity.GetTagItem("run"), runId))
                    diagnosticActivities.Add(activity.Status);
            }
        };
        ActivitySource.AddActivityListener(activityListener);
        try
        {
            using IHost host = CreateHost(output, lifecycle, producer, runSeconds: 1, publisherFails: true, runId: runId);

            await host.StartAsync(TestContext.Current.CancellationToken);
            await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
            Assert.True(producer.Called);
            Assert.Equal(1, lifecycle.DiagnosticsAttempts);
            Assert.Equal(0, lifecycle.CompletionAttempts);
            Assert.Equal([ActivityStatusCode.Error], diagnosticActivities);
            Assert.True(lifecycle.DiagnosticsResult is { ExitCode: 0 },
                string.Join(';', lifecycle.DiagnosticsResult?.Manifest.Diagnostics ?? []) + " " +
                await File.ReadAllTextAsync(Path.Combine(output, "capabilities.json"), TestContext.Current.CancellationToken));
            Assert.True(File.Exists(Path.Combine(output, "manifest.json")));
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Theory]
    [InlineData("producer-drain")]
    [InlineData("ledger-drain")]
    [InlineData("statistics-completion")]
    [InlineData("evidence-drain")]
    [InlineData("capabilities-save")]
    [InlineData("evidence-readback")]
    [InlineData("ledger-readback")]
    [InlineData("manifest-save")]
    public async Task Given_FinalisationDeadlineExpires_When_WorkerReturnsFailedResult_Then_AttemptsDiagnostics(
        string operation)
    {
        string output = NewDirectory();
        var lifecycle = new RecordingLifecycle("success");
        var producer = new ProducerFactoryTracker { KeepQueueNonempty = operation == "producer-drain" };
        var clock = new WorkerDeadlineTimeProvider(operation);
        var evidenceWriterStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEvidenceWriter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? evidenceWriterTask = null;
        IHost? host = null;
        using var operationListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Parchwell.Workers",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(operationListener);
        try
        {
            host = CreateHost(output, lifecycle, producer, runSeconds: 1,
                timeProvider: clock, holdEvidenceWriter: operation == "evidence-drain"
                    ? async () =>
                    {
                        evidenceWriterStarted.TrySetResult();
                        await releaseEvidenceWriter.Task.ConfigureAwait(false);
                    }
                    : null, evidenceWriterTaskCaptured: task => evidenceWriterTask = task);

            using var outerBound = new CancellationTokenSource(TimeSpan.FromSeconds(15), TimeProvider.System);
            using var testBound = CancellationTokenSource.CreateLinkedTokenSource(
                outerBound.Token, TestContext.Current.CancellationToken);
            await host.StartAsync(testBound.Token);
            Task shutdown = host.WaitForShutdownAsync(testBound.Token);
            if (operation == "evidence-drain")
            {
                await evidenceWriterStarted.Task.WaitAsync(testBound.Token);
                await clock.SelectedDeadlineExpired.WaitAsync(testBound.Token);
                Assert.Equal([operation], clock.ExpiredOperations);
                releaseEvidenceWriter.TrySetResult();
                await evidenceWriterTask!.WaitAsync(testBound.Token);
            }
            await shutdown;
            await host.StopAsync(testBound.Token);

            Assert.Equal([operation], clock.ExpiredOperations);
            Assert.Equal(1, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
            Assert.True(producer.Called);
            Assert.Equal(1, lifecycle.DiagnosticsAttempts);
            Assert.Equal(0, lifecycle.CompletionAttempts);
            Assert.NotNull(lifecycle.DiagnosticsResult);
            Assert.Equal(1, lifecycle.DiagnosticsResult!.ExitCode);
            Assert.False(lifecycle.DiagnosticsResult.Manifest.BookkeepingComplete);
            Assert.Contains(ExpectedDiagnostic(operation), lifecycle.DiagnosticsResult.Manifest.Diagnostics);
            if (operation != "statistics-completion")
            {
                Assert.DoesNotContain("statistics_completion_incomplete", lifecycle.DiagnosticsResult.Manifest.Diagnostics);
                string statisticsPath = Path.Combine(output, "statistics.jsonl");
                Assert.True(File.Exists(statisticsPath));
                Assert.True(new FileInfo(statisticsPath).Length > 0);
            }
        }
        finally
        {
            releaseEvidenceWriter.TrySetResult();
            if (evidenceWriterStarted.Task.IsCompleted && evidenceWriterTask is not null)
                await evidenceWriterTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            host?.Dispose();
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    private static IHost CreateHost(string output, RecordingLifecycle lifecycle, ProducerFactoryTracker producer, int runSeconds,
        bool publisherFails = false, TimeProvider? timeProvider = null, Func<Task>? holdEvidenceWriter = null,
        Action<Task>? evidenceWriterTaskCaptured = null, string? runId = null)
    {
        Dictionary<string, string> values = LoadGenHostProcessTests.CreateInputs(output);
        if (runId is not null)
        {
            values["LoadGen__RunId"] = runId;
            values["LoadGen__JobName"] = runId + "-c0-r1-loadgen";
        }
        values["LoadGen__ProcessLifetimeTimeout"] = "00:00:15";
        values["LoadGen__MaxAttempts"] = "2";
        values["LoadGen__MaxScheduleLateness"] = "00:00:05";
        values["LoadGen__CleanupTimeout"] = "00:00:00.100";
        values["LoadGen__UploadTimeout"] = "00:00:01";
        string[] args = values.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
        return Program.BuildHostCore(args, services =>
        {
            services.RemoveAll<IScenarioSource>();
            services.AddSingleton<IScenarioSource>(new ScenarioMemorySource(
                ("scenarios/A01/scenario.yaml", ScenarioText),
                ("scenarios/profiles/registration.yaml", ProfileText(runSeconds))));
            services.RemoveAll<IRunArtifactPublisher>();
            services.AddSingleton<IRunArtifactPublisher>(new AvailablePublisher(output, publisherFails));
            services.RemoveAll<IProducerRunLifecycle>();
            services.AddSingleton<IProducerRunLifecycle>(lifecycle);
            if (timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(timeProvider);
            }
            services.RemoveAll<KafkaStatisticsBridgeFactory>();
            services.AddSingleton(provider => new KafkaStatisticsBridgeFactory(
                provider.GetRequiredService<IOptions<KafkaStatisticsOptions>>().Value,
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<IKafkaStatisticsEvidenceSink>(),
                provider.GetRequiredService<KafkaStatisticsMetrics>(), nativeClockCompatible: true,
                completionBoundaryObserver: () => producer.EmitPostDrainStatistics?.Invoke()));
            if (holdEvidenceWriter is not null)
            {
                services.RemoveAll<RunSessionFactory>();
                services.AddSingleton(provider => new RunSessionFactory(
                    provider.GetRequiredService<IOptions<LoadGenHostOptions>>(),
                    provider.GetRequiredService<RunEvidenceSinkRouter>(),
                    provider.GetRequiredService<KafkaStatisticsBridgeFactory>(),
                    provider.GetRequiredService<KafkaConfigurationFactory>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<KafkaAuthenticationFactory>(),
                    provider.GetRequiredService<Func<ProducerConfig, KafkaStatisticsSession,
                        Parchwell.LoadGen.Run.Evidence.RunEvidenceSession,
                        Parchwell.Kafka.Authentication.KafkaOAuthBearerSession, IProducer<byte[], byte[]>>>(),
                    holdEvidenceWriter));
            }
            services.RemoveAll<Func<ProducerConfig, Parchwell.Kafka.Statistics.KafkaStatisticsSession,
                Parchwell.LoadGen.Run.Evidence.RunEvidenceSession, Parchwell.Kafka.Authentication.KafkaOAuthBearerSession,
                IProducer<byte[], byte[]>>>();
            services.AddSingleton<Func<ProducerConfig, KafkaStatisticsSession,
                Parchwell.LoadGen.Run.Evidence.RunEvidenceSession, Parchwell.Kafka.Authentication.KafkaOAuthBearerSession,
                IProducer<byte[], byte[]>>>(provider => (configuration, statistics, evidence, authentication) =>
            {
                evidenceWriterTaskCaptured?.Invoke(evidence.WriterTask);
                producer.AssertRegistrationComplete?.Invoke();
                producer.Called = true;
                IProducer<byte[], byte[]> native = DispatchProxy.Create<IProducer<byte[], byte[]>, SuccessfulProducer>();
                SuccessfulProducer syntheticProducer = (SuccessfulProducer)(object)native;
                syntheticProducer.KeepQueueNonempty = producer.KeepQueueNonempty;
                syntheticProducer.Statistics = statistics;
                producer.EmitPostDrainStatistics = syntheticProducer.EmitPostDrainStatistics;
                return native;
            });
        }, (_, configure) => configure(new ParchwellTelemetryOptions()));
    }

    private const string ScenarioText = """
        schema_version: 1
        id: A01
        baseline: Parchwell.Kafka
        roles: [LoadGenProducer]
        profile: profiles/registration.yaml
        configurations: {C0: {}}
        fault:
          live: {kind: none}
          component: {kind: none}
        repeats: 5
        gate: {kind: none}
        measures:
          - {id: ledger.delivery_latency_ms, role: LoadGenProducer}
        """;

    private static string ProfileText(int durationSeconds) => $$"""
        schema_version: 1
        id: registration
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

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "loadgen-registration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ExpectedDiagnostic(string operation) => operation switch
    {
        "producer-drain" => "producer_drain_timeout",
        "ledger-drain" => "ledger_drain_failed:TaskCanceledException",
        "statistics-completion" => "statistics_completion_incomplete",
        "evidence-drain" => "evidence:evidence_writer_completion_timeout",
        "capabilities-save" => "evidence:capabilities_write_timeout",
        "evidence-readback" => "evidence_readback_failed:TaskCanceledException",
        "ledger-readback" => "ledger_readback_failed:TaskCanceledException",
        "manifest-save" => "manifest_save_failed:TaskCanceledException",
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private sealed class ScenarioMemorySource(params (string Path, string Text)[] documents) : IScenarioSource
    {
        private readonly Dictionary<string, string> _documents = documents.ToDictionary(item => item.Path, item => item.Text, StringComparer.Ordinal);
        public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _documents.TryGetValue(logicalPath, out string? text)
                ? ValueTask.FromResult<TextReader>(new StringReader(text))
                : ValueTask.FromException<TextReader>(new FileNotFoundException("registration_fixture_source_missing"));
        }
    }

    private sealed class RecordingLifecycle(string outcome, bool diagnosticsFails = false) : IProducerRunLifecycle
    {
        public int RegistrationAttempts { get; private set; }
        public int DiagnosticsAttempts { get; private set; }
        public int CompletionAttempts { get; private set; }
        public string? RegistrationResult { get; private set; }
        public RunExecutionResult? DiagnosticsResult { get; private set; }

        public async Task RegisterAsync(ProducerRunContext context, ProducerLifecycleOptions options, CancellationToken cancellationToken)
        {
            RegistrationAttempts++;
            context.Validate();
            if (outcome == "timeout")
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if (outcome == "cancel") throw new OperationCanceledException(cancellationToken);
            if (outcome == "conflict") throw new Azure.RequestFailedException(409, "synthetic registration conflict");
            if (outcome == "ambiguous") throw new IOException("synthetic ambiguous registration response");
            RegistrationResult = "success";
        }

        public Task PublishDiagnosticsAsync(ProducerRunContext context, RunExecutionResult? result,
            ProducerLifecycleOptions options, CancellationToken cancellationToken)
        {
            DiagnosticsAttempts++;
            DiagnosticsResult = result;
            if (diagnosticsFails)
                throw new BlobProducerRunLifecycle.DiagnosticPublicationException(["ledger.jsonl:upload_failed"]);
            return Task.CompletedTask;
        }

        public Task WriteCompletionAsync(ProducerRunContext context, ProducerLifecycleOptions options, CancellationToken cancellationToken)
        {
            CompletionAttempts++;
            return Task.CompletedTask;
        }
    }

    private sealed class AvailablePublisher(string output, bool fail = false) : IRunArtifactPublisher
    {
        public bool IsAvailable => true;
        public Task PublishAsync(RunExecutionResult result, RunPublicationOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.True(File.Exists(Path.Combine(output, "ledger.jsonl")));
            Assert.True(File.Exists(Path.Combine(output, "manifest.json")));
            if (fail) throw new IOException("synthetic_normal_publication_failure");
            return Task.CompletedTask;
        }
    }

    private sealed class ProducerFactoryTracker
    {
        public bool Called { get; set; }
        public bool KeepQueueNonempty { get; set; }
        public Action? AssertRegistrationComplete { get; set; }
        public Action? EmitPostDrainStatistics { get; set; }
    }

    public class SuccessfulProducer : DispatchProxy
    {
        public KafkaStatisticsSession? Statistics { get; set; }
        public bool KeepQueueNonempty { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new DeliveryReport<byte[], byte[]>
                {
                    Error = new Error(ErrorCode.NoError), Status = PersistenceStatus.Persisted
                });
                return null;
            }
            if (targetMethod?.Name == "Flush")
            {
                return KeepQueueNonempty ? 1 : 0;
            }
            if (targetMethod?.Name == "Dispose") return null;
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

    private sealed class WorkerDeadlineTimeProvider(string selectedOperation) : TimeProvider
    {
        private readonly Dictionary<string, int> _operationDeadlineCounts = new(StringComparer.Ordinal);
        private readonly List<string> _expired = [];
        private readonly TaskCompletionSource _selectedDeadlineExpired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<string> ExpiredOperations => _expired;
        public Task SelectedDeadlineExpired => _selectedDeadlineExpired.Task;
        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow();
        public override long GetTimestamp() => TimeProvider.System.GetTimestamp();
        public override long TimestampFrequency => TimeProvider.System.TimestampFrequency;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            string? operation = Activity.Current?.GetTagItem("operation") as string;
            int occurrence = 0;
            if (dueTime != Timeout.InfiniteTimeSpan && operation is not null)
            {
                lock (_operationDeadlineCounts)
                {
                    _operationDeadlineCounts.TryGetValue(operation, out occurrence);
                    occurrence++;
                    _operationDeadlineCounts[operation] = occurrence;
                }
            }
            (string target, int targetOccurrence) = selectedOperation switch
            {
                "producer-drain" => ("producer_drain", 1),
                "ledger-drain" => ("ledger_drain", 1),
                "statistics-completion" => ("statistics_completion", 1),
                "evidence-drain" => ("evidence_completion", 1),
                "capabilities-save" => ("evidence_completion", 2),
                "evidence-readback" => ("evidence_readback", 1),
                "ledger-readback" => ("ledger_readback", 1),
                "manifest-save" => ("manifest_save", 1),
                _ => throw new InvalidOperationException("worker_deadline_operation_unknown")
            };
            if (operation == target && occurrence == targetOccurrence)
            {
                lock (_expired) _expired.Add(selectedOperation);
                callback(state);
                _selectedDeadlineExpired.TrySetResult();
                return new WorkerManualTimer();
            }
            if (IsWorkerFinalisationOperation(operation)) return new WorkerManualTimer();
            return TimeProvider.System.CreateTimer(callback, state, dueTime, period);
        }

        private static bool IsWorkerFinalisationOperation(string? operation) => operation is
            "producer_drain" or "ledger_drain" or "statistics_completion" or "producer_dispose" or
            "evidence_completion" or "evidence_readback" or "ledger_readback" or "manifest_save";
    }

    private sealed class WorkerManualTimer : ITimer
    {
        private int _disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period) => Volatile.Read(ref _disposed) == 0;
        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
#pragma warning restore CA1707
