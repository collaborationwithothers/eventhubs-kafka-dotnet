using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.LoadGen.Run.Restart;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class B10SecondWorkerTests
{
    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public async Task Given_CompleteFirstWorkerWithTwoPermittedTimeouts_When_SecondJobRuns_Then_DiscoversBeforeRegistrationAndResendsOnce(string phase)
    {
        using var fixture = new Fixture(phase);
        Assert.Equal(0, await fixture.RunAsync(second: false));
        RunExecutionResult first = Assert.Single(fixture.Store.Results);
        Assert.Equal(2, fixture.Producers[0].Messages.Count);
        Assert.Equal(2, first.Manifest.AttemptCount);
        LedgerRow[] firstRows = System.Text.Encoding.UTF8.GetString(fixture.Store.Objects[fixture.Store.Registrations[0].PublicationPrefix + "ledger.jsonl"])
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => LedgerValidator.DeserializeRow(System.Text.Encoding.UTF8.GetBytes(line))).ToArray();
        Assert.Equal(2, firstRows.Length);
        Assert.All(firstRows, row =>
        {
            Assert.Equal((int)ErrorCode.Local_MsgTimedOut, row.ErrorCode);
            Assert.Equal((int)PersistenceStatus.PossiblyPersisted, row.PersistenceStatus);
            Assert.False(row.Delivered);
        });
        Assert.Null(first.Manifest.Restart);
        Assert.All(first.Manifest.Inventory, attempt => Assert.Equal("pod-first", attempt.InstanceId));
        Assert.Equal(0, await fixture.RunAsync(second: true));
        RunExecutionResult second = fixture.Store.Results[1];
        ProducerRunContext registration = fixture.Store.Registrations[1];
        Assert.Equal("pod-first", registration.PredecessorInstanceId);
        Assert.Equal("pod-second", registration.Identity.InstanceId);
        Assert.Equal(registration.PodName, registration.Identity.InstanceId);
        Assert.NotEqual(first.Manifest.Identity.InstanceId, second.Manifest.Identity.InstanceId);
        Assert.Equal($"run-1-{phase.ToLowerInvariant()}-r1-loadgen2", registration.JobName);
        Assert.True(fixture.Store.Events.IndexOf("read:index.json") < fixture.Store.Events.IndexOf("register:pod-second"));
        Assert.True(fixture.Store.Events.IndexOf("register:pod-second") < fixture.Store.Events.IndexOf("producer:pod-second"));
        Assert.True(fixture.Store.Events.IndexOf("read:capabilities.json") < fixture.Store.Events.IndexOf("register:pod-second"));
        Assert.Single(fixture.Producers[1].Messages);
        Assert.Equal(1, second.Manifest.AttemptCount);
        Assert.DoesNotContain("application_retry_limit_reached", second.Manifest.Diagnostics);
        RestartManifest restart = Assert.IsType<RestartManifest>(second.Manifest.Restart);
        Assert.Equal("pod-first", restart.PredecessorInstanceId);
        RestartPlannedEntry planned = Assert.Single(restart.Planned);
        AttemptEntry original = first.Manifest.Inventory[0];
        AttemptEntry resend = Assert.Single(second.Manifest.Inventory);
        Assert.Equal(first.Manifest.Inventory[1].AttemptId, planned.PredecessorAttemptId);
        Assert.Equal(original.Key, resend.Key);
        Assert.Equal(original.Sequence, resend.Sequence);
        Assert.Equal(original.PlannedMicroseconds, resend.PlannedMicroseconds);
        Assert.Equal(original.EventType, resend.EventType);
        Assert.NotEqual(original.AttemptId, resend.AttemptId);
        Assert.Equal(resend.AttemptId, System.Text.Encoding.UTF8.GetString(fixture.Producers[1].Messages[0].Headers.Single(header => header.Key == "attempt-id").GetValueBytes()));
        Assert.Equal("pod-second", System.Text.Encoding.UTF8.GetString(fixture.Producers[1].Messages[0].Headers.Single(header => header.Key == "instance-id").GetValueBytes()));
        Assert.NotEqual(first.Manifest.Inventory[1].AttemptId, resend.AttemptId);
        Assert.Equal(fixture.Producers[0].Messages[0].Key, fixture.Producers[1].Messages[0].Key);
        Assert.Equal(fixture.Producers[0].Messages[0].Value, fixture.Producers[1].Messages[0].Value);
        Assert.Equal(fixture.Producers[0].Messages[0].Value, fixture.Producers[0].Messages[1].Value);
        Assert.Equal(2, fixture.Store.Completions.Count);
        Assert.Equal(BlobProducerRunLifecycle.SerializeCompletion(registration),
            BlobProducerRunLifecycle.SerializeCompletion(fixture.Store.Completions[1]));
        Assert.Equal(BlobProducerRunLifecycle.SerializeCompletion(registration),
            fixture.Store.Objects[registration.CompletionPath]);
        Assert.Equal("complete:pod-second", fixture.Store.Events[^1]);
        foreach (ProducerRunContext context in fixture.Store.Registrations)
        {
            Assert.Equal(context.PodName, context.Identity.InstanceId);
            Assert.Equal(context.PodName, context.Identity.ClientId);
            using JsonDocument marker = JsonDocument.Parse(fixture.Store.Objects[context.RegistrationPath]);
            Assert.Equal(context.PredecessorInstanceId, marker.RootElement.TryGetProperty("predecessor_instance_id", out JsonElement predecessor) ? predecessor.GetString() : null);
            using JsonDocument completion = JsonDocument.Parse(fixture.Store.Objects[context.CompletionPath]);
            Assert.Equal(context.JobName, completion.RootElement.GetProperty("job_name").GetString());
            Assert.True(completion.RootElement.GetProperty("cleanup_completed").GetBoolean());
            Assert.Equal(0, completion.RootElement.GetProperty("handler_exit_code").GetInt32());
            Assert.Contains(context.PublicationPrefix + "index.json", fixture.Store.Objects.Keys);
        }
        Assert.All(fixture.Producers, producer => Assert.True(producer.Disposed));
    }

    [Theory]
    [InlineData("C0", "missing")]
    [InlineData("C0", "ambiguous")]
    [InlineData("C0", "failed")]
    [InlineData("C0", "incomplete")]
    [InlineData("C1", "missing")]
    [InlineData("C1", "ambiguous")]
    [InlineData("C1", "failed")]
    [InlineData("C1", "incomplete")]
    public async Task Given_UnusablePredecessor_When_SecondWorkerRuns_Then_FailsBeforeRegistrationOrProducerConstruction(string phase, string change)
    {
        using var fixture = new Fixture(phase);
        Assert.Equal(0, await fixture.RunAsync(second: false));
        ProducerRunContext first = Assert.Single(fixture.Store.Registrations);
        if (change == "missing") fixture.Store.Objects.Clear();
        if (change == "ambiguous") fixture.Store.Objects[first.RepeatPrefix + "producer/pod-other/registration.json"] = [1];
        if (change == "incomplete") fixture.Store.Objects.Remove(first.CompletionPath);
        if (change == "failed")
        {
            JsonNode completion = JsonNode.Parse(fixture.Store.Objects[first.CompletionPath])!;
            completion["handler_exit_code"] = 1;
            fixture.Store.Objects[first.CompletionPath] = JsonSerializer.SerializeToUtf8Bytes(completion);
        }
        Assert.Equal(1, await fixture.RunAsync(second: true));
        Assert.Single(fixture.Store.Registrations);
        Assert.Single(fixture.Store.Completions);
        Assert.Single(fixture.Store.Results);
        Assert.Single(fixture.Producers);
        Assert.DoesNotContain("register:pod-second", fixture.Store.Events);
        Assert.DoesNotContain("producer:pod-second", fixture.Store.Events);
        Assert.DoesNotContain(fixture.Store.Objects.Keys, path => path.Contains("/pod-second/", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_BothWorkerOutputTrees_When_InspectingPackagedInputs_Then_B10ScenarioAndProfileAreCopiedExactly()
    {
        string root = Root();
        foreach (string worker in new[] { "LoadGen", "Verifier" })
        {
            string configuration = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
            string assembly = Path.Combine(root, "src", "Parchwell." + worker, "bin", configuration, "net10.0", "Parchwell." + worker + ".dll");
            Assert.True(File.Exists(assembly));
            foreach (string file in new[] { "B10/scenario.yaml", "profiles/b10-timeout.yaml" })
                Assert.Equal(File.ReadAllBytes(Path.Combine(root, "scenarios", file)), File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(assembly)!, "scenarios", file)));
        }
    }

    // Production index serialization is deliberately exercised without widening its production visibility.
    internal static byte[] SerializePublicationIndex(RunExecutionResult result, ImmutableArray<BlobRunArtifactPublisher.ArtifactSource> sources) =>
        (byte[])typeof(BlobRunArtifactPublisher).GetMethod("SerializeIndex", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [result, sources])!;

    internal static (long AggregateBytes, byte[] Index) PublicationCapacity(RunManifest manifest, long ledgerBytes, long manifestBytes, long evidenceAllowance)
    {
        var sources = ImmutableArray.CreateBuilder<BlobRunArtifactPublisher.ArtifactSource>();
        foreach ((string name, long length) in new[] { ("ledger.jsonl", ledgerBytes), ("manifest.json", manifestBytes),
                     ("statistics.jsonl", evidenceAllowance), ("request-lifecycle.jsonl", evidenceAllowance), ("capabilities.json", evidenceAllowance) })
            sources.Add(new BlobRunArtifactPublisher.ArtifactSource(name, name, null!, length, new string('a', 64)));
        var result = new RunExecutionResult(0, manifest, ImmutableDictionary<string, string>.Empty);
        byte[] index = SerializePublicationIndex(result, sources.ToImmutable());
        return (sources.Sum(source => source.Length) + index.LongLength, index);
    }

    private sealed class Fixture(string phase) : IDisposable
    {
        private readonly string _output = Path.Combine(Path.GetTempPath(), "b10-workers-" + Guid.NewGuid().ToString("N"));
        public MemoryStore Store { get; } = new();
        public List<TimeoutProducer> Producers { get; } = [];
        public async Task<int> RunAsync(bool second)
        {
            string pod = second ? "pod-second" : "pod-first";
            var values = LoadGenHostProcessTests.CreateInputs(Path.Combine(_output, pod));
            foreach (string setting in new[] { "ExpectedInstanceId", "ActualInstanceId", "PodName", "ClientId" }) values["LoadGen__" + setting] = pod;
            values["LoadGen__ScenarioId"] = "B10";
            values["LoadGen__ConfigurationId"] = phase;
            values["LoadGen__Phase"] = phase;
            values["LoadGen__RunId"] = "run-1";
            values["LoadGen__JobName"] = $"run-1-{phase.ToLowerInvariant()}-r1-loadgen{(second ? "2" : "")}";
            values["LoadGen__PodUid"] = "uid-" + pod;
            values["LoadGen__MaxAttempts"] = "2";
            values["LoadGen__MaxScheduleLateness"] = "00:00:05";
            string[] args = values.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
            TimeoutProducer? current = null;
            IHost host = Program.BuildHostCore(args, services =>
            {
                services.RemoveAll<IScenarioSource>();
                services.AddSingleton<IScenarioSource>(new SyntheticSource());
                services.RemoveAll<IKafkaTokenProvider>();
                services.AddSingleton<IKafkaTokenProvider>(new TokenProvider());
                services.RemoveAll<IRunArtifactPublisher>();
                services.AddSingleton<IRunArtifactPublisher>(Store);
                services.RemoveAll<IProducerRunLifecycle>();
                services.AddSingleton<IProducerRunLifecycle>(Store);
                services.RemoveAll<IPredecessorEvidenceSource>();
                services.AddSingleton<IPredecessorEvidenceSource>(Store);
                services.RemoveAll<KafkaStatisticsBridgeFactory>();
                services.AddSingleton(provider => new KafkaStatisticsBridgeFactory(provider.GetRequiredService<IOptions<KafkaStatisticsOptions>>().Value,
                    provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<IKafkaStatisticsEvidenceSink>(), provider.GetRequiredService<KafkaStatisticsMetrics>(),
                    nativeClockCompatible: true, completionBoundaryObserver: () => current!.EmitStatistics(pod)));
                services.RemoveAll<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>>>();
                services.AddSingleton<Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>>>(
                    _ => (configuration, statistics, _, _) =>
                    {
                        Assert.Equal(pod, Store.Registrations[^1].PodName);
                        Assert.Equal(phase == "C1", configuration.EnableIdempotence);
                        Assert.Equal(100, configuration.MessageTimeoutMs);
                        Assert.Equal(5, configuration.MaxInFlight);
                        Store.Events.Add("producer:" + pod);
                        IProducer<byte[], byte[]> producer = DispatchProxy.Create<IProducer<byte[], byte[]>, TimeoutProducer>();
                        current = (TimeoutProducer)(object)producer;
                        current.Statistics = statistics;
                        Producers.Add(current);
                        return producer;
                    });
            }, new RecordingTelemetryRegistration().Register);
            return await Program.RunAsync([], hostFactory: () => host);
        }
        public void Dispose() { if (Directory.Exists(_output)) Directory.Delete(_output, recursive: true); }
    }

    private sealed class SyntheticSource : IScenarioSource
    {
        public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<TextReader>(new StringReader(logicalPath == "scenarios/B10/scenario.yaml"
                ? File.ReadAllText(Path.Combine(Root(), logicalPath)) : SyntheticOneEventProfile));
    }

    // Synthetic one-event fixture, independent of the committed smoke profile tested by B10DeclarationTests.
    private const string SyntheticOneEventProfile = """
        schema_version: 1
        id: b10-timeout
        seed: 1401
        tenants: [{id: tenant01, weight: 1}]
        documents_per_burst: 1
        payloads:
          uploaded: [{size_bytes: 4, weight: 1}]
          ocr-started: [{size_bytes: 4, weight: 1}]
          ocr-finished: [{size_bytes: 4, weight: 1}]
          classified: [{size_bytes: 4, weight: 1}]
          indexed: [{size_bytes: 4, weight: 1}]
        stages: [{id: synthetic, offered_attempts_per_second: 1, duration_seconds: 1}]
        """;

    private sealed class TokenProvider : IKafkaTokenProvider
    {
        public AccessToken GetToken(string scope, CancellationToken cancellationToken) => new("synthetic-token", DateTimeOffset.MaxValue);
    }

    public class TimeoutProducer : DispatchProxy
    {
        public KafkaStatisticsSession? Statistics { get; set; }
        public List<Message<byte[], byte[]>> Messages { get; } = [];
        public bool Disposed { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce" && args is { Length: 3 })
            {
                Messages.Add((Message<byte[], byte[]>)args[1]!);
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new DeliveryReport<byte[], byte[]> { Error = new Error(ErrorCode.Local_MsgTimedOut), Status = PersistenceStatus.PossiblyPersisted });
                return null;
            }
            if (targetMethod?.Name == "Flush") return 0;
            if (targetMethod?.Name == "Dispose") { Disposed = true; return null; }
            throw new NotSupportedException(targetMethod?.Name);
        }
        public void EmitStatistics(string pod)
        {
            long timestamp = (long)((Int128)Stopwatch.GetTimestamp() * 1000000 / Stopwatch.Frequency) + 2;
            while ((long)((Int128)Stopwatch.GetTimestamp() * 1000000 / Stopwatch.Frequency) <= timestamp) Thread.SpinWait(1);
            timestamp = (long)((Int128)Stopwatch.GetTimestamp() * 1000000 / Stopwatch.Frequency);
            string json = "{\"name\":\"synthetic-producer\",\"client_id\":\"" + pod + "\",\"type\":\"producer\",\"ts\":" + timestamp.ToString(CultureInfo.InvariantCulture) +
                ",\"time\":1,\"age\":0,\"brokers\":{\"broker-1\":{\"name\":\"broker-1\",\"nodeid\":1,\"state\":\"UP\",\"stateage\":1,\"req_timeouts\":0,\"disconnects\":0,\"txretries\":0,\"txerrs\":0,\"rxerrs\":0,\"throttle\":{\"cnt\":1,\"max\":0},\"rtt\":{\"cnt\":0,\"p99\":0}}}}";
            Statistics!.Capture(json);
        }
    }

    private sealed class MemoryStore : IRunArtifactPublisher, IProducerRunLifecycle, IPredecessorEvidenceSource
    {
        public bool IsAvailable => true;
        public Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);
        public List<string> Events { get; } = [];
        public List<RunExecutionResult> Results { get; } = [];
        public List<ProducerRunContext> Registrations { get; } = [];
        public List<ProducerRunContext> Completions { get; } = [];
        public Task RegisterAsync(ProducerRunContext context, ProducerLifecycleOptions options, CancellationToken cancellationToken)
        {
            Objects.Add(context.RegistrationPath, BlobProducerRunLifecycle.SerializeRegistration(context));
            Registrations.Add(context);
            Events.Add("register:" + context.PodName);
            return Task.CompletedTask;
        }
        public Task WriteCompletionAsync(ProducerRunContext context, ProducerLifecycleOptions options, CancellationToken cancellationToken)
        {
            Objects.Add(context.CompletionPath, BlobProducerRunLifecycle.SerializeCompletion(context));
            Completions.Add(context);
            Events.Add("complete:" + context.PodName);
            return Task.CompletedTask;
        }
        public Task PublishDiagnosticsAsync(ProducerRunContext context, RunExecutionResult? result, ProducerLifecycleOptions options, CancellationToken cancellationToken) => Task.CompletedTask;
        public async Task PublishAsync(RunExecutionResult result, RunPublicationOptions options, CancellationToken cancellationToken = default)
        {
            ProducerRunContext context = options.Context!;
            ImmutableArray<BlobRunArtifactPublisher.ArtifactSource> sources = await BlobRunArtifactPublisher.OpenAndHashSourcesAsync(result, options, cancellationToken);
            try
            {
                foreach (BlobRunArtifactPublisher.ArtifactSource source in sources)
                    Objects.Add(context.PublicationPrefix + source.Name, await File.ReadAllBytesAsync(source.Path, cancellationToken));
                var payloads = sources.ToDictionary(source => source.Name, source => Objects[context.PublicationPrefix + source.Name], StringComparer.Ordinal);
                RunArtifactIndexReader.ValidatePayloadSemantics(result.Manifest.Identity, result.EvidenceExpectations!, payloads, options.MaxArtifactBytes, cancellationToken);
                Objects.Add(context.PublicationPrefix + "index.json", SerializePublicationIndex(result, sources));
            }
            finally { foreach (BlobRunArtifactPublisher.ArtifactSource source in sources) await source.Stream.DisposeAsync(); }
            Results.Add(result);
            Events.Add("publish:" + context.PodName);
        }
        public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken)
        {
            Events.Add("discover");
            return Task.FromResult<IReadOnlyList<string>>([.. Objects.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal)]);
        }
        public Task<byte[]> ReadAsync(string name, long maximumBytes, CancellationToken cancellationToken)
        {
            Events.Add("read:" + Path.GetFileName(name));
            byte[] bytes = Objects[name];
            Assert.True(bytes.LongLength <= maximumBytes);
            return Task.FromResult(bytes.ToArray());
        }
    }

    private static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Parchwell.slnx"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("repository_root");
        return root;
    }
}
#pragma warning restore CA1707
