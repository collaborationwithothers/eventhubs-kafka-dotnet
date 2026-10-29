using System.Reflection;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.SingleActionV2;
using Parchwell.Scenarios;
using Parchwell.Testing;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleActionV2;

#pragma warning disable CA1707
public sealed class ProbeV2LifecycleTests
{
    [Fact]
    public async Task Given_FiveMaximumSizedNativeStatistics_When_HostEnds_Then_FullCaptureFollowsCleanupAndIsExportable()
    {
        using var cluster = new ProbeV2MockCluster(64);
        cluster.CreateTopic("document-events", 32);
        string raw = await cluster.CaptureProducerStatisticsAsync("document-events", "synthetic-budget-producer", TestContext.Current.CancellationToken);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(raw) > 32768);
        raw += new string(' ', 262144 - System.Text.Encoding.UTF8.GetByteCount(raw));
        using var fixture = new Fixture(statistics: raw);
        Assert.Equal(0, await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output, logs: fixture.Logs));
        string line = fixture.Output.ToString().Split('\n').Single(value => value.StartsWith("PARCHWELL_PROBE_V2_CAPTURE ", StringComparison.Ordinal));
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(line["PARCHWELL_PROBE_V2_CAPTURE ".Length..]);
        Assert.InRange(bytes.Length, 1048577, fixture.Options.Limits.MaxCaptureBytes);
        ProbeV2CaptureFile capture = ProbeV2Wire.Read<ProbeV2CaptureFile>(bytes, fixture.Options.Limits.MaxCaptureBytes);
        Assert.True(capture.CleanupCompleted && capture.HostCleanupCompleted);
        Assert.Equal(0, capture.HandlerExitCode);
        Assert.All(capture.Repeats, row => Assert.Equal(raw, System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Assert.Single(row.Statistics).RawBase64))));
        Assert.Equal("capture", fixture.Events[^1]);
        string? directory = System.Environment.GetEnvironmentVariable("PARCHWELL_PROBE_V2_LARGE_FIXTURE_DIRECTORY");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "actor.stdout"), fixture.Output.ToString(), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "registration.json"), ProbeV2Wire.Encode(ProbeV2Wire.Registration(fixture.Options), 65536), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "capture.json"), bytes, TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "pod.json"), ProbeV2Wire.Encode(new ProbeV2Pod(2,
                fixture.Options.JobName, fixture.Options.PodName, fixture.Options.PodUid, fixture.Options.ServiceAccount,
                "Job", fixture.Options.JobName, "sha256:" + new string('a', 64)), 65536), TestContext.Current.CancellationToken);
        }
    }
    [Fact]
    public async Task Given_FiveFreshClients_When_HostEnds_Then_CaptureFollowsStopDispose()
    {
        using var fixture = new Fixture();
        int exit = await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output, logs: fixture.Logs);
        Assert.Equal(0, exit);
        Assert.Equal(["registration", "client-1", "client-2", "client-3", "client-4", "client-5", "stop", "dispose", "capture"], fixture.Events);
        Assert.Equal(5, fixture.Clients.Count);
        Assert.All(fixture.Clients, client => { Assert.Equal(1, client.ProduceCalls); Assert.Equal(1, client.DisposeCalls); Assert.IsType<string>(client.Destination); });
        ProbeV2CaptureFile capture = Capture(fixture);
        Assert.True(capture.CleanupCompleted);
        Assert.True(capture.HostCleanupCompleted);
        Assert.Equal(0, capture.HandlerExitCode);
        Assert.Equal([1, 2, 3, 4, 5], capture.Repeats.Select(value => value.Repeat));
        Assert.All(capture.Repeats, value =>
        {
            Assert.Equal("success", value.Result);
            Assert.Equal(31, value.Delivery!.Partition);
            Assert.Equal("Persisted", value.Delivery.PersistenceStatus);
            Assert.Equal(Enumerable.Range(0, 32), value.MetadataPartitions!);
            Assert.Single(value.Statistics);
        });
        string? directory = System.Environment.GetEnvironmentVariable("PARCHWELL_PROBE_V2_FIXTURE_DIRECTORY");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "actor.stdout"), fixture.Output.ToString(), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "registration.json"), ProbeV2Wire.Encode(ProbeV2Wire.Registration(fixture.Options), 65536), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "capture.json"), ProbeV2Wire.Encode(capture, 524288), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "pod.json"), ProbeV2Wire.Encode(new ProbeV2Pod(2,
                fixture.Options.JobName, fixture.Options.PodName, fixture.Options.PodUid, fixture.Options.ServiceAccount,
                "Job", fixture.Options.JobName, "sha256:" + new string('a', 64)), 65536), TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData("registration", 0)]
    [InlineData("stop", 5)]
    [InlineData("dispose", 5)]
    [InlineData("client_cleanup", 1)]
    public async Task Given_RegistrationOrCleanupFailure_When_HostEnds_Then_NoSuccessfulCapture(string failure, int expectedClients)
    {
        using var fixture = new Fixture(failure);
        int exit = await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output, logs: fixture.Logs);
        Assert.Equal(1, exit);
        Assert.Equal(expectedClients, fixture.Clients.Count);
        if (failure == "registration") Assert.DoesNotContain("PARCHWELL_PROBE_V2_CAPTURE ", fixture.Output.ToString(), StringComparison.Ordinal);
        else
        {
            ProbeV2CaptureFile capture = Capture(fixture);
            Assert.Equal(1, capture.HandlerExitCode);
            Assert.False(capture.CleanupCompleted && capture.HostCleanupCompleted);
            if (failure == "client_cleanup") Assert.All(capture.Repeats.Skip(1), value =>
            {
                Assert.Equal("not_attempted_cleanup", value.Admission);
                Assert.Null(value.ClientId);
                Assert.Empty(value.Settings);
            });
        }
    }

    [Fact]
    public async Task Given_BrokerRejection_When_ReceiptCleanupSucceed_Then_CaptureIsSuccessfulEvidenceOfError()
    {
        using var fixture = new Fixture("broker", "F06", "C2");
        Assert.Equal(0, await ProbeV2Host.RunAsync(hostFactory: () => fixture.Host, output: fixture.Output, logs: fixture.Logs));
        Assert.All(Capture(fixture).Repeats, value =>
        {
            Assert.Equal("error", value.Result);
            Assert.Equal((int)ErrorCode.TopicAuthorizationFailed, value.Error!.Code);
            Assert.Null(value.Delivery);
        });
    }

    private static ProbeV2CaptureFile Capture(Fixture fixture)
    {
        string line = fixture.Output.ToString().Split('\n').Single(value => value.StartsWith("PARCHWELL_PROBE_V2_CAPTURE ", StringComparison.Ordinal));
        return ProbeV2Wire.Read<ProbeV2CaptureFile>(System.Text.Encoding.UTF8.GetBytes(line["PARCHWELL_PROBE_V2_CAPTURE ".Length..]), 524288);
    }
    private sealed class Fixture : IDisposable
    {
        internal List<string> Events { get; } = [];
        internal List<NativeProducer> Clients { get; } = [];
        internal ProbeV2Options Options { get; }
        internal RecordingWriter Output { get; }
        internal StringWriter Logs { get; } = new();
        internal TestHost Host { get; }
        internal Fixture(string? failure = null, string item = "B08", string configuration = "C0", string? statistics = null)
        {
            Options = ProbeV2OptionsTests.Options(item, configuration);
            string root = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(root, "Parchwell.slnx"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("repository_root");
            Options.ScenarioRoot = root;
            Output = new(Events, failure);
            var clock = new TestClock();
            var services = new ServiceCollection();
            services.AddSingleton<IOptions<ProbeV2Options>>(Microsoft.Extensions.Options.Options.Create(Options));
            services.AddSingleton(Options);
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IHostApplicationLifetime, Lifetime>();
            services.AddParchwellScenarios(value =>
            {
                value.MaxDocumentCharacters = 1048576; value.MaxNestingDepth = 32;
                value.MaxScalarCharacters = 65536; value.MaxParserEvents = 100000;
            });
            services.AddSingleton<IScenarioSource>(new ProbeV2ScenarioSource(root));
            foreach (KafkaClientRole role in Enum.GetValues<KafkaClientRole>()) services.AddParchwellKafka(role, value =>
            { value.NamespaceName = Options.NamespaceName; value.ClientId = Options.PodName; value.EnrichmentGroupId = "probe-v2-group"; });
            services.AddSingleton(new ProbeV2SessionFactory((_, capture, _) =>
            {
                IProducer<byte[], byte[]> producer = DispatchProxy.Create<IProducer<byte[], byte[]>, NativeProducer>();
                var proxy = (NativeProducer)(object)producer;
                proxy.Capture = capture;
                proxy.Failure = failure;
                proxy.Statistics = statistics;
                proxy.Clock = clock;
                Clients.Add(proxy);
                Events.Add("client-" + Clients.Count);
                return producer;
            }, (_, _, _) => throw new InvalidOperationException("unexpected_consumer"), (_, _, _) => Enumerable.Range(0, 32).ToArray()));
            services.AddSingleton(new ProbeV2Inputs(Options, clock));
            services.AddSingleton<ProbeV2Worker>();
            Host = new(services.BuildServiceProvider(), Events, failure);
        }
        public void Dispose() { Host.DisposeProvider(); Output.Dispose(); Logs.Dispose(); }
    }
    private sealed class RecordingWriter(List<string> events, string? failure) : StringWriter
    {
        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            string value = buffer.ToString();
            string kind = value.StartsWith("PARCHWELL_PROBE_V2_REGISTRATION ", StringComparison.Ordinal) ? "registration" : "capture";
            events.Add(kind);
            if (kind == failure) throw new IOException("synthetic_write_failure");
            return base.WriteLineAsync(buffer, cancellationToken);
        }
    }
    private sealed class TestHost(ServiceProvider services, List<string> events, string? failure) : IHost
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
    public class NativeProducer : DispatchProxy
    {
        internal ProbeV2Capture Capture = null!;
        internal TestClock Clock = null!;
        internal string? Failure;
        internal string? Statistics;
        internal int ProduceCalls, DisposeCalls;
        internal object? Destination;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce")
            {
                ProduceCalls++;
                Destination = args![0];
                var message = (Message<byte[], byte[]>)args[1]!;
                bool broker = Failure == "broker";
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new()
                {
                    Topic = broker ? "document-state" : "document-events", Partition = broker ? Partition.Any : new Partition(31),
                    Offset = broker ? Offset.Unset : new Offset(7), Message = message,
                    Status = broker ? PersistenceStatus.NotPersisted : PersistenceStatus.Persisted,
                    Error = new Error(broker ? ErrorCode.TopicAuthorizationFailed : ErrorCode.NoError)
                });
                Clock.OnDelay = () => Capture.Statistics(Statistics ?? "{\"time\":" + Clock.Milliseconds / 1000 + ",\"ts\":1,\"brokers\":{}}");
                return null;
            }
            if (targetMethod?.Name == "Dispose")
            {
                DisposeCalls++;
                if (Failure == "client_cleanup") throw new IOException("synthetic_cleanup_failure");
                return null;
            }
            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
