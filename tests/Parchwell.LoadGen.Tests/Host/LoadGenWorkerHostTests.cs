using System.Reflection;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Parchwell.LoadGen;
using Parchwell.Kafka.Statistics;
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

    private static IHost CreateHost(RecordingPublisher publisher, int durationSeconds,
        ProducerFactoryTracker? producerFactory = null)
    {
        string output = Path.Combine(Path.GetTempPath(), "loadgen-host-run-" + Guid.NewGuid().ToString("N"));
        publisher.OutputRoot = output;
        Dictionary<string, string> values = LoadGenHostProcessTests.CreateInputs(output);
        values["LoadGen__ProcessLifetimeTimeout"] = "00:00:15";
        values["LoadGen__StatisticsCompletionTimeout"] = "00:00:00.050";
        values["LoadGen__CleanupTimeout"] = "00:00:01";
        values["LoadGen__ProducerDrainTimeout"] = "00:00:01";
        values["LoadGen__EvidenceCompletionTimeout"] = "00:00:01";
        values["LoadGen__MaxAttempts"] = "2";
        values["LoadGen__MaxScheduleLateness"] = "00:00:05";
        string[] args = values.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();

        return Program.BuildHost(args, services =>
        {
            services.RemoveAll<IScenarioSource>();
            services.AddSingleton<IScenarioSource>(new ScenarioMemorySource(
                ("scenarios/A01/scenario.yaml", Scenario()),
                ("scenarios/profiles/synthetic.yaml", Profile(durationSeconds))));
            services.RemoveAll<IWorkerAdmission>();
            services.AddSingleton<IWorkerAdmission, AllowLocalAdmission>();
            services.RemoveAll<IRunArtifactPublisher>();
            services.AddSingleton<IRunArtifactPublisher>(publisher);
            services.RemoveAll<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, IProducer<byte[], byte[]>>>();
            services.AddSingleton<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, IProducer<byte[], byte[]>>>(
                _ => (_, _, _) =>
                {
                    if (producerFactory is not null) producerFactory.Called = true;
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

    private sealed class AllowLocalAdmission : IWorkerAdmission
    {
        public void EnsureProducerAuthenticationAvailable(ProducerConfig configuration) =>
            Assert.NotNull(configuration);
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

    private sealed class RecordingPublisher(bool throwOnPublish = false, bool available = true) : IRunArtifactPublisher
    {
        public bool IsAvailable => available;
        public bool Called { get; private set; }
        public bool SawLedgerAndManifest { get; private set; }
        public string OutputRoot { get; set; } = string.Empty;

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
}
#pragma warning restore CA1707
