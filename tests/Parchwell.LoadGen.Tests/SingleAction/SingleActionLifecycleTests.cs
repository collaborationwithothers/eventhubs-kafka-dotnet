using System.Reflection;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.SingleAction;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleAction;

#pragma warning disable CA1707
public sealed class SingleActionLifecycleTests
{
    [Fact]
    public async Task Given_FiveFreshClients_When_HostSucceeds_Then_CapturePrecedesStopDisposeAndLastReceipt()
    {
        using var fixture = new Fixture();
        int exit = await SingleActionHost.RunAsync(hostFactory: () => fixture.Host);
        Assert.Equal(0, exit);
        Assert.Equal(5, fixture.NativeClients.Count);
        Assert.All(fixture.NativeClients, native => { Assert.Equal(1, native.ProduceCalls); Assert.Equal(1, native.DisposeCalls); });
        Assert.Equal(["registration", "client-1", "client-2", "client-3", "client-4", "client-5", "capture", "stop", "dispose", "completion"], fixture.Events);
        using JsonDocument capture = JsonDocument.Parse(fixture.Publication.Capture!);
        JsonElement[] repeats = capture.RootElement.GetProperty("repeats").EnumerateArray().ToArray();
        Assert.Equal([1, 2, 3, 4, 5], repeats.Select(repeat => repeat.GetProperty("repeat").GetInt32()));
        Assert.Equal(["pod-1-r1", "pod-1-r2", "pod-1-r3", "pod-1-r4", "pod-1-r5"], repeats.Select(repeat => repeat.GetProperty("client_id").GetString()));
        Assert.All(repeats, repeat => Assert.Equal("success", repeat.GetProperty("result").GetString()));
        using JsonDocument completion = JsonDocument.Parse(fixture.Publication.Completion!);
        Assert.Equal(5, completion.RootElement.GetProperty("repeats_attempted").GetInt32());
        Assert.Equal(0, completion.RootElement.GetProperty("handler_exit_code").GetInt32());
        Assert.True(completion.RootElement.GetProperty("cleanup_completed").GetBoolean());
    }

    [Theory]
    [InlineData("registration")]
    [InlineData("capture")]
    [InlineData("stop")]
    [InlineData("dispose")]
    [InlineData("client_cleanup")]
    [InlineData("capture_limit")]
    [InlineData("completion")]
    public async Task Given_PublicationOrCleanupFailure_When_HostEnds_Then_NoSuccessfulFinalReceipt(string failure)
    {
        using var fixture = new Fixture(failure);
        int exit = await SingleActionHost.RunAsync(hostFactory: () => fixture.Host);
        Assert.Equal(1, exit);
        Assert.Null(fixture.Publication.Completion);
        Assert.Contains("stop", fixture.Events);
        Assert.Contains("dispose", fixture.Events);
        if (failure == "client_cleanup")
        {
            Assert.Single(fixture.NativeClients);
            using JsonDocument capture = JsonDocument.Parse(fixture.Publication.Capture!);
            Assert.Equal(5, capture.RootElement.GetProperty("repeats").GetArrayLength());
        }
        if (failure == "capture_limit")
        {
            Assert.Equal(5, fixture.NativeClients.Count);
            using JsonDocument capture = JsonDocument.Parse(fixture.Publication.Capture!);
            Assert.Equal(5, capture.RootElement.GetProperty("repeats").GetArrayLength());
            Assert.All(capture.RootElement.GetProperty("repeats").EnumerateArray(), repeat =>
            {
                Assert.True(repeat.GetProperty("terminal").GetBoolean());
                Assert.Equal("success", repeat.GetProperty("result").GetString());
                Assert.Contains(repeat.GetProperty("diagnostics").EnumerateArray(), diagnostic => diagnostic.GetString() == "capture_limit");
            });
        }
    }

    [Fact]
    public async Task Given_BrokerDenial_When_AllCaptureAndCleanupSucceed_Then_StillPublishesCompletion()
    {
        using var fixture = new Fixture("broker");
        int exit = await SingleActionHost.RunAsync(hostFactory: () => fixture.Host);
        Assert.Equal(0, exit);
        Assert.NotNull(fixture.Publication.Completion);
        using JsonDocument capture = JsonDocument.Parse(fixture.Publication.Capture!);
        Assert.All(capture.RootElement.GetProperty("repeats").EnumerateArray(), repeat => Assert.Equal("error", repeat.GetProperty("result").GetString()));
        Assert.Equal(5, fixture.NativeClients.Sum(native => native.ProduceCalls));
    }

    [Theory]
    [InlineData("ProcessLifetimeSeconds", 1919)]
    [InlineData("PostTerminalStatisticsSeconds", 19)]
    [InlineData("CleanupSeconds", 9)]
    [InlineData("RegistrationUploadSeconds", 29)]
    [InlineData("CaptureUploadSeconds", 29)]
    [InlineData("HostCleanupSeconds", 9)]
    [InlineData("ReceiptUploadSeconds", 29)]
    [InlineData("PollSliceMs", 99)]
    [InlineData("MaxCaptureBytes", 134217727)]
    [InlineData("MaxRegistrationBytes", 65535)]
    [InlineData("MaxCompletionBytes", 65535)]
    [InlineData("MaxStatisticsReportBytes", 262143)]
    [InlineData("MaxStatisticsReports", 63)]
    [InlineData("MaxClientErrors", 63)]
    [InlineData("MaxErrorBytes", 4095)]
    [InlineData("MaxDiagnostics", 99)]
    [InlineData("MaxDiagnosticBytes", 2047)]
    [InlineData("CallbackQueueCapacity", 127)]
    [InlineData("MaxSettingsKeys", 127)]
    [InlineData("MaxSettingsBytes", 16383)]
    [InlineData("MaxNonmatchingRecords", 4095)]
    [InlineData("MaxInputDepth", 31)]
    public void Given_ChangedFixedBudget_When_Validating_Then_RejectsEveryLiteral(string property, int wrong)
    {
        var options = SingleActionCaptureTests.Options();
        options.Validate();
        typeof(SingleActionHostOptions).GetProperty(property)!.SetValue(options, wrong);
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Theory]
    [InlineData("RunId", "Run")]
    [InlineData("SourceCommit", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("ScenarioId", "A01")]
    [InlineData("ConfigurationId", "C9")]
    [InlineData("JobName", "another-job")]
    [InlineData("PodName", "Invalid")]
    [InlineData("PodUid", "")]
    [InlineData("BlobContainer", "ledger")]
    [InlineData("BlobEndpoint", "http://example.blob.core.windows.net/")]
    [InlineData("NamespaceName", "azure-reserved")]
    public void Given_InvalidCoordinate_When_Validating_Then_Rejects(string property, string wrong)
    {
        var options = SingleActionCaptureTests.Options();
        typeof(SingleActionHostOptions).GetProperty(property)!.SetValue(options, wrong);
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    private sealed class Fixture : IDisposable
    {
        internal List<string> Events { get; } = [];
        internal List<NativeProducer> NativeClients { get; } = [];
        internal RecordingPublication Publication { get; }
        internal TestHost Host { get; }
        internal Fixture(string? failure = null)
        {
            var clock = new SingleActionCaptureTests.AdvancingClock();
            var options = SingleActionCaptureTests.Options();
            options.ScenarioId = "F04";
            options.ConfigurationId = "C1";
            options.JobName = "run-1-f04-c1-single";
            options.BlobContainer = "ledger";
            string root = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(root, "Parchwell.slnx"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("repository_root");
            options.ScenarioRoot = root;
            Publication = new RecordingPublication(Events, failure);
            var services = new ServiceCollection();
            services.AddSingleton<IOptions<SingleActionHostOptions>>(Microsoft.Extensions.Options.Options.Create(options));
            services.AddSingleton(options);
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<ISingleActionPublication>(Publication);
            services.AddSingleton<IHostApplicationLifetime, Lifetime>();
            services.AddParchwellScenarios(value =>
            {
                value.MaxDocumentCharacters = 1048576; value.MaxNestingDepth = 32;
                value.MaxScalarCharacters = 65536; value.MaxParserEvents = 100000;
            });
            services.AddSingleton<IScenarioSource>(new SingleActionScenarioSource(root));
            foreach (KafkaClientRole role in Enum.GetValues<KafkaClientRole>()) services.AddParchwellKafka(role, value =>
            { value.NamespaceName = options.NamespaceName; value.ClientId = options.PodName; value.EnrichmentGroupId = "single-action-run-1-f04-c1"; });
            services.AddSingleton(new SingleActionSessionFactory((_, capture, _) =>
            {
                IProducer<byte[], byte[]> producer = DispatchProxy.Create<IProducer<byte[], byte[]>, NativeProducer>();
                var native = (NativeProducer)(object)producer;
                native.Capture = capture;
                native.ThrowCleanup = failure == "client_cleanup";
                native.BrokerError = failure == "broker";
                native.OverflowAfterDelivery = failure == "capture_limit";
                NativeClients.Add(native);
                native.Ordinal = NativeClients.Count;
                Events.Add("client-" + NativeClients.Count);
                return producer;
            }, (_, _, _) => throw new InvalidOperationException("unexpected_consumer")));
            services.AddSingleton<SingleActionWorker>();
            Host = new(services.BuildServiceProvider(), Events, failure);
        }
        public void Dispose() => Host.DisposeProvider();
    }
    internal sealed class RecordingPublication(List<string> events, string? failure) : ISingleActionPublication
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
    public class NativeProducer : DispatchProxy
    {
        internal SingleActionCapture Capture = null!;
        internal int ProduceCalls, DisposeCalls;
        internal int Ordinal;
        internal bool ThrowCleanup, BrokerError, OverflowAfterDelivery;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Produce")
            {
                ProduceCalls++;
                Assert.Equal(new TopicPartition("document-state", new Partition(0)), args![0]);
                var message = (Message<byte[], byte[]>)args[1]!;
                Assert.Equal($"F04:C1:run-1:r{Ordinal}", System.Text.Encoding.UTF8.GetString(message.Key));
                Assert.Equal(1000, message.Value.Length);
                ((Action<DeliveryReport<byte[], byte[]>>)args[2]!)(new() { Error = new Error(BrokerError ? ErrorCode.TopicAuthorizationFailed : ErrorCode.NoError) });
                if (OverflowAfterDelivery)
                    for (int index = 0; index < 65; index++) Capture.Error(new Error(ErrorCode.Local_Fail));
                return null;
            }
            if (targetMethod?.Name == "Dispose")
            {
                DisposeCalls++;
                if (ThrowCleanup) throw new IOException("synthetic_client_cleanup_failure");
                return null;
            }
            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
