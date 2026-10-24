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
            using IHost host = CreateHost(output, lifecycle, producer, runSeconds: 1);

            await host.StartAsync(TestContext.Current.CancellationToken);
            await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.Equal(0, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
            Assert.Equal("success", lifecycle.RegistrationResult);
            Assert.True(producer.Called);
            Assert.Equal(1, lifecycle.CompletionAttempts);
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
        var lifecycle = new RecordingLifecycle("success");
        var producer = new ProducerFactoryTracker();
        try
        {
            using IHost host = CreateHost(output, lifecycle, producer, runSeconds: 1, publisherFails: true);

            await host.StartAsync(TestContext.Current.CancellationToken);
            await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
            Assert.True(producer.Called);
            Assert.Equal(1, lifecycle.DiagnosticsAttempts);
            Assert.Equal(0, lifecycle.CompletionAttempts);
            Assert.True(lifecycle.DiagnosticsResult is { ExitCode: 0 });
            Assert.True(File.Exists(Path.Combine(output, "manifest.json")));
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Theory]
    [InlineData("producer-drain", 2)]
    [InlineData("ledger-drain", 3)]
    [InlineData("statistics-completion", 4)]
    [InlineData("evidence-drain", 6)]
    [InlineData("capabilities-save", 7)]
    [InlineData("evidence-readback", 8)]
    [InlineData("ledger-readback", 9)]
    [InlineData("manifest-save", 10)]
    public async Task Given_FinalisationDeadlineExpires_When_WorkerReturnsFailedResult_Then_AttemptsDiagnostics(
        string operation, int selectedDeadline)
    {
        string output = NewDirectory();
        var lifecycle = new RecordingLifecycle("success");
        var producer = new ProducerFactoryTracker { KeepQueueNonempty = operation == "producer-drain" };
        var clock = new WorkerDeadlineTimeProvider(selectedDeadline);
        try
        {
            using IHost host = CreateHost(output, lifecycle, producer, runSeconds: 1,
                timeProvider: clock);

            await host.StartAsync(TestContext.Current.CancellationToken);
            await host.WaitForShutdownAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.Contains(selectedDeadline, clock.ExpiredDeadlineNumbers);
            Assert.Equal(1, host.Services.GetRequiredService<LoadGenWorker>().ExitCode);
            Assert.True(producer.Called);
            Assert.Equal(1, lifecycle.DiagnosticsAttempts);
            Assert.Equal(0, lifecycle.CompletionAttempts);
            Assert.NotNull(lifecycle.DiagnosticsResult);
            Assert.Equal(1, lifecycle.DiagnosticsResult!.ExitCode);
            Assert.False(lifecycle.DiagnosticsResult.Manifest.BookkeepingComplete);
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    private static IHost CreateHost(string output, RecordingLifecycle lifecycle, ProducerFactoryTracker producer, int runSeconds,
        bool publisherFails = false, TimeProvider? timeProvider = null)
    {
        Dictionary<string, string> values = LoadGenHostProcessTests.CreateInputs(output);
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
            services.RemoveAll<Func<ProducerConfig, Parchwell.Kafka.Statistics.KafkaStatisticsSession,
                Parchwell.LoadGen.Run.Evidence.RunEvidenceSession, Parchwell.Kafka.Authentication.KafkaOAuthBearerSession,
                IProducer<byte[], byte[]>>>();
            services.AddSingleton<Func<ProducerConfig, KafkaStatisticsSession,
                Parchwell.LoadGen.Run.Evidence.RunEvidenceSession, Parchwell.Kafka.Authentication.KafkaOAuthBearerSession,
                IProducer<byte[], byte[]>>>(_ => (configuration, statistics, evidence, authentication) =>
            {
                producer.AssertRegistrationComplete?.Invoke();
                producer.Called = true;
                IProducer<byte[], byte[]> native = DispatchProxy.Create<IProducer<byte[], byte[]>, SuccessfulProducer>();
                ((SuccessfulProducer)(object)native).KeepQueueNonempty = producer.KeepQueueNonempty;
                ((SuccessfulProducer)(object)native).Statistics = statistics;
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

    private sealed class RecordingLifecycle(string outcome) : IProducerRunLifecycle
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
    }

    public class SuccessfulProducer : DispatchProxy
    {
        public KafkaStatisticsSession? Statistics { get; set; }
        public bool KeepQueueNonempty { get; set; }
        private int _statisticsScheduled;
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
                if (Statistics is not null && Interlocked.Exchange(ref _statisticsScheduled, 1) == 0)
                {
                    KafkaStatisticsSession statistics = Statistics!;
                    _ = Task.Run(async () =>
                    {
                        long creation = (long)(Stopwatch.GetTimestamp() * 1_000_000d / Stopwatch.Frequency);
                        for (int index = 0; index < 2; index++)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(20)).ConfigureAwait(false);
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
                return KeepQueueNonempty ? 1 : 0;
            }
            if (targetMethod?.Name == "Dispose") return null;
            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private sealed class WorkerDeadlineTimeProvider(int selectedDeadline) : TimeProvider
    {
        private int _deadlineCount;
        private readonly List<int> _expired = [];
        public IReadOnlyList<int> ExpiredDeadlineNumbers => _expired;
        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow();
        public override long GetTimestamp() => TimeProvider.System.GetTimestamp();
        public override long TimestampFrequency => TimeProvider.System.TimestampFrequency;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            int number = dueTime == Timeout.InfiniteTimeSpan ? 0 : Interlocked.Increment(ref _deadlineCount);
            // The worker fixture has no native post-drain report, so expire statistics completion
            // on every case to produce the failed RunExecutionResult that triggers diagnostics.
            if (number == selectedDeadline || number == 4)
            {
                lock (_expired) _expired.Add(number);
                callback(state);
            }
            return new WorkerManualTimer();
        }
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
