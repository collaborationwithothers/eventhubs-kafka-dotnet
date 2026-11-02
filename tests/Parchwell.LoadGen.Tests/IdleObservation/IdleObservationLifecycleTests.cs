using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.IdleObservation;
using Parchwell.LoadGen.Tests.SingleAction;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.IdleObservation;

#pragma warning disable CA1707
public sealed class IdleObservationLifecycleTests
{
    [Fact]
    public async Task Given_FiveFreshClients_When_HostSucceeds_Then_EachProducesTwiceAroundTheIdlePeriodAndCompletionIsLast()
    {
        using var fixture = new Fixture();
        int exit = await IdleObservationHost.RunAsync(hostFactory: () => fixture.Host);
        Assert.Equal(0, exit);
        Assert.Equal(5, fixture.NativeClients.Count);
        Assert.All(fixture.NativeClients, native => { Assert.Equal(2, native.ProduceCalls); Assert.Equal(1, native.DisposeCalls); });
        Assert.Equal("registration", fixture.Events[0]);
        Assert.Equal(["capture", "stop", "dispose", "completion"], fixture.Events.TakeLast(4));
        using JsonDocument capture = JsonDocument.Parse(fixture.Publication.Capture!);
        JsonElement root = capture.RootElement;
        Assert.Equal("idle-observation", root.GetProperty("mode").GetString());
        Assert.Equal("batch", root.GetProperty("run_kind").GetString());
        Assert.Equal("document-events", root.GetProperty("topic").GetString());
        Assert.Equal(0, root.GetProperty("partition").GetInt32());
        Assert.Equal(1000, root.GetProperty("payload_bytes").GetInt32());
        Assert.Equal(420, root.GetProperty("idle_seconds").GetInt32());
        Assert.Equal(15000, root.GetProperty("statistics_interval_ms").GetInt32());
        Assert.Equal(80, root.GetProperty("max_statistics_reports").GetInt32());
        JsonElement[] repeats = root.GetProperty("repeats").EnumerateArray().ToArray();
        Assert.Equal([1, 2, 3, 4, 5], repeats.Select(repeat => repeat.GetProperty("repeat").GetInt32()));
        Assert.Equal(["pod-1-r1", "pod-1-r2", "pod-1-r3", "pod-1-r4", "pod-1-r5"], repeats.Select(repeat => repeat.GetProperty("client_id").GetString()));
        foreach (JsonElement repeat in repeats)
        {
            int number = repeat.GetProperty("repeat").GetInt32();
            JsonElement first = repeat.GetProperty("first");
            JsonElement second = repeat.GetProperty("second");
            Assert.Equal($"A02:C0:run-1:r{number}:p1", first.GetProperty("key").GetString());
            Assert.Equal($"A02:C0:run-1:r{number}:p2", second.GetProperty("key").GetString());
            Assert.Equal("success", first.GetProperty("result").GetString());
            Assert.Equal("success", second.GetProperty("result").GetString());
            Assert.True(repeat.GetProperty("idle_ended_elapsed_ms").GetInt64() - repeat.GetProperty("idle_started_elapsed_ms").GetInt64() >= 420000);
            Assert.True(second.GetProperty("started_elapsed_ms").GetInt64() >= repeat.GetProperty("idle_ended_elapsed_ms").GetInt64());
            Assert.Equal("15000", repeat.GetProperty("settings").GetProperty("statistics.interval.ms").GetString());
        }
        Assert.All(fixture.NativeClients.SelectMany(native => native.Produced), produced =>
        {
            Assert.Equal(new TopicPartition("document-events", new Partition(0)), produced.Target);
            Assert.Equal(1000, produced.Value.Length);
            Assert.All(produced.Value, value => Assert.Equal((byte)'s', value));
        });
        Assert.NotNull(fixture.Publication.Completion);
    }

    [Theory]
    [InlineData("registration")]
    [InlineData("capture")]
    [InlineData("stop")]
    [InlineData("dispose")]
    [InlineData("client_cleanup")]
    [InlineData("completion")]
    public async Task Given_PublicationOrCleanupFailure_When_HostEnds_Then_ExitsOneWithoutSuccessfulCompletion(string failure)
    {
        using var fixture = new Fixture(failure);
        int exit = await IdleObservationHost.RunAsync(hostFactory: () => fixture.Host);
        Assert.Equal(1, exit);
        Assert.Null(fixture.Publication.Completion);
        if (failure == "completion") Assert.Contains("completion", fixture.Events);
        else Assert.DoesNotContain("completion", fixture.Events);
    }

    [Fact]
    public async Task Given_SecondDeliveryDenied_When_HostCaptures_Then_RecordsBrokerErrorAndStillCompletes()
    {
        using var fixture = new Fixture("second_denied");
        int exit = await IdleObservationHost.RunAsync(hostFactory: () => fixture.Host);
        Assert.Equal(0, exit);
        using JsonDocument capture = JsonDocument.Parse(fixture.Publication.Capture!);
        Assert.All(capture.RootElement.GetProperty("repeats").EnumerateArray(), repeat =>
        {
            Assert.Equal("success", repeat.GetProperty("first").GetProperty("result").GetString());
            JsonElement second = repeat.GetProperty("second");
            Assert.Equal("error", second.GetProperty("result").GetString());
            Assert.Equal(29, second.GetProperty("error").GetProperty("code").GetInt32());
            Assert.Equal("broker", second.GetProperty("error").GetProperty("origin").GetString());
        });
        Assert.NotNull(fixture.Publication.Completion);
    }

    [Fact]
    public async Task Given_EightyOneReportsOnFirstProduce_When_HostCaptures_Then_WritesCaptureWithLimitAndNoCompletion()
    {
        using var fixture = new Fixture("statistics_overflow");
        int exit = await IdleObservationHost.RunAsync(hostFactory: () => fixture.Host);
        Assert.Equal(1, exit);
        Assert.Null(fixture.Publication.Completion);
        Assert.NotNull(fixture.Publication.Capture);
        using JsonDocument capture = JsonDocument.Parse(fixture.Publication.Capture!);
        JsonElement[] repeats = capture.RootElement.GetProperty("repeats").EnumerateArray().ToArray();
        Assert.Equal(5, repeats.Length);
        Assert.All(repeats, repeat => Assert.Contains(repeat.GetProperty("diagnostics").EnumerateArray(), diagnostic => diagnostic.GetString() == "capture_limit"));
    }

    [Fact]
    public async Task Given_NoFirstDelivery_When_HostCaptures_Then_RecordsOpenAttemptAndStillIdlesAndProducesAgain()
    {
        using var fixture = new Fixture("no_first");
        int exit = await IdleObservationHost.RunAsync(hostFactory: () => fixture.Host);
        Assert.Equal(0, exit);
        Assert.All(fixture.NativeClients, native => Assert.Equal(2, native.ProduceCalls));
        using JsonDocument capture = JsonDocument.Parse(fixture.Publication.Capture!);
        Assert.All(capture.RootElement.GetProperty("repeats").EnumerateArray(), repeat =>
        {
            JsonElement first = repeat.GetProperty("first");
            Assert.False(first.GetProperty("terminal").GetBoolean());
            Assert.Equal(JsonValueKind.Null, first.GetProperty("result").ValueKind);
            Assert.Contains(repeat.GetProperty("diagnostics").EnumerateArray(), diagnostic => diagnostic.GetString() == "action_window_elapsed:p1");
            Assert.True(repeat.GetProperty("idle_ended_elapsed_ms").GetInt64() - repeat.GetProperty("idle_started_elapsed_ms").GetInt64() >= 420000);
            Assert.Equal("success", repeat.GetProperty("second").GetProperty("result").GetString());
        });
    }

    [Fact]
    public async Task Given_ResolvedStatisticsInterval_When_Checking_Then_AcceptsDeclaredValueAndRejectsMissingOne()
    {
        using var fixture = new Fixture();
        ResolvedScenario scenario = await fixture.ResolveAsync();
        IdleObservationDefinition definition = scenario.Definition.IdleObservation!;
        IdleObservationWorker.RequireStatisticsInterval(scenario.Configurations["C0"].Roles[KafkaClientRole.LoadGenProducer], definition);
        var empty = new ResolvedRoleConfiguration(KafkaClientRole.LoadGenProducer, ImmutableDictionary<string, ResolvedSetting>.Empty, []);
        var error = Assert.Throws<InvalidOperationException>(() => IdleObservationWorker.RequireStatisticsInterval(empty, definition));
        Assert.Equal("idle_observation_statistics_interval_mismatch", error.Message);
    }

    private sealed class Fixture : IDisposable
    {
        internal List<string> Events { get; } = [];
        internal List<NativeProducer> NativeClients { get; } = [];
        internal RecordingPublication Publication { get; }
        internal TestHost Host { get; }
        private readonly ServiceProvider _services;
        internal StringWriter Logs { get; } = new();
        internal Fixture(string? failure = null)
        {
            var clock = new SingleActionCaptureTests.AdvancingClock();
            IdleObservationOptions options = IdleObservationOptionsTests.Options();
            options.ScenarioRoot = IdleObservationOptionsTests.Root();
            Publication = new RecordingPublication(Events, failure);
            var services = new ServiceCollection();
            services.AddSingleton<IOptions<IdleObservationOptions>>(Microsoft.Extensions.Options.Options.Create(options));
            services.AddSingleton(options);
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IIdleObservationPublication>(Publication);
            services.AddSingleton<IHostApplicationLifetime, Lifetime>();
            services.AddParchwellScenarios(value =>
            {
                value.MaxDocumentCharacters = 1048576; value.MaxNestingDepth = 32;
                value.MaxScalarCharacters = 65536; value.MaxParserEvents = 100000;
            });
            services.AddSingleton<IScenarioSource>(new IdleObservationScenarioSource(options.ScenarioRoot));
            foreach (KafkaClientRole role in Enum.GetValues<KafkaClientRole>()) services.AddParchwellKafka(role, value =>
            { value.NamespaceName = options.NamespaceName; value.ClientId = options.PodName; value.EnrichmentGroupId = "idle-observation-run-1-a02-c0"; });
            services.AddSingleton(new IdleObservationSessionFactory((_, capture, _) =>
            {
                IProducer<byte[], byte[]> producer = DispatchProxy.Create<IProducer<byte[], byte[]>, NativeProducer>();
                var native = (NativeProducer)(object)producer;
                native.Capture = capture;
                native.Failure = failure;
                NativeClients.Add(native);
                native.Ordinal = NativeClients.Count;
                Events.Add("client-" + NativeClients.Count);
                return producer;
            }));
            services.AddSingleton(provider => new IdleObservationWorker(options, provider.GetRequiredService<IdleObservationSessionFactory>(),
                Publication, clock, Logs));
            _services = services.BuildServiceProvider();
            Host = new(_services, Events, failure);
        }
        internal async Task<ResolvedScenario> ResolveAsync()
        {
            LoadedScenario loaded = await _services.GetRequiredService<ScenarioLoader>().LoadAsync("A02", TestContext.Current.CancellationToken);
            return _services.GetRequiredService<ScenarioResolver>().Resolve(loaded);
        }
        public void Dispose() { Host.DisposeProvider(); Logs.Dispose(); }
    }

    internal sealed class RecordingPublication(List<string> events, string? failure) : IIdleObservationPublication
    {
        internal byte[]? Capture;
        internal byte[]? Completion;
        public Task WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string kind = path.Contains("registrations/", StringComparison.Ordinal) ? "registration" : path.EndsWith("capture.json", StringComparison.Ordinal) ? "capture" : "completion";
            events.Add(kind);
            if (failure == kind) throw new IOException("synthetic_write_failure");
            if (kind == "capture") Capture = bytes.ToArray();
            if (kind == "completion") Completion = bytes.ToArray();
            return Task.CompletedTask;
        }
    }

    internal sealed class TestHost(ServiceProvider services, List<string> events, string? failure) : IHost
    {
        public IServiceProvider Services => services;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            events.Add("stop");
            return failure == "stop" ? Task.FromException(new IOException("synthetic_stop_failure")) : Task.CompletedTask;
        }
        public void Dispose()
        {
            events.Add("dispose");
            if (failure == "dispose") throw new IOException("synthetic_dispose_failure");
            services.Dispose();
        }
        internal void DisposeProvider() => services.Dispose();
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }

    internal sealed record Produced(TopicPartition Target, byte[] Key, byte[] Value);

    public class NativeProducer : DispatchProxy
    {
        internal IdleObservationCapture Capture = null!;
        internal string? Failure;
        internal int ProduceCalls, DisposeCalls, Ordinal;
        internal List<Produced> Produced { get; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce")
            {
                int attempt = ++ProduceCalls;
                var message = (Message<byte[], byte[]>)args![1]!;
                Produced.Add(new((TopicPartition)args[0]!, message.Key, message.Value));
                Assert.Equal($"A02:C0:run-1:r{Ordinal}:p{attempt}", Encoding.UTF8.GetString(message.Key));
                if (Failure == "statistics_overflow" && attempt == 1)
                    for (int index = 0; index < 81; index++) { Capture.Statistics("{}"); Capture.Drain(); }
                if (Failure == "no_first" && attempt == 1) return null;
                bool denied = Failure == "second_denied" && attempt == 2;
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new() { Error = new Error(denied ? ErrorCode.TopicAuthorizationFailed : ErrorCode.NoError) });
                return null;
            }
            if (targetMethod?.Name == "Dispose")
            {
                DisposeCalls++;
                if (Failure == "client_cleanup") throw new IOException("synthetic_client_cleanup_failure");
                return null;
            }
            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
