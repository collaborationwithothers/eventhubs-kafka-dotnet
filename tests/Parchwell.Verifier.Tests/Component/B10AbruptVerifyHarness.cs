using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.StartLine;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

public sealed class B10AbruptVerifyKafkaFixture : IAsyncLifetime
{
    private readonly LocalKafkaFixture kafka = new();
    internal string Bootstrap => kafka.ObserverBootstrapServers;
    public async ValueTask InitializeAsync() => await kafka.StartAsync(TestContext.Current.CancellationToken);
    public ValueTask DisposeAsync() => kafka.DisposeAsync();

    internal async Task ResetAsync()
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = Bootstrap }).Build();
        try { await admin.DeleteTopicsAsync(["document-events"]); }
        catch (DeleteTopicsException exception) when (exception.Results.All(result => result.Error.Code == ErrorCode.UnknownTopicOrPart)) { }
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                await admin.CreateTopicsAsync([new TopicSpecification { Name = "document-events", NumPartitions = 3, ReplicationFactor = 1 }]);
                await WaitForPartitionsAsync();
                return;
            }
            catch (CreateTopicsException exception) when (attempt < 99 && exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            { await Task.Delay(100, TestContext.Current.CancellationToken); }
        }
        throw new TimeoutException("synthetic_topic_reset_failed");
    }

    private async Task WaitForPartitionsAsync()
    {
        string last = "metadata unavailable";
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                using var consumer = ComponentRunHarness.Consumer(Bootstrap);
                using var admin = new DependentAdminClientBuilder(consumer.Handle).Build();
                Metadata metadata = admin.GetMetadata("document-events", TimeSpan.FromSeconds(1));
                TopicMetadata? topic = metadata.Topics.SingleOrDefault(topic => topic.Topic == "document-events");
                if (topic is not null && !topic.Error.IsError && topic.Partitions.Count == 3 &&
                    topic.Partitions.All(partition => !partition.Error.IsError && metadata.Brokers.Any(broker => broker.BrokerId == partition.Leader)))
                {
                    foreach (int partition in Enumerable.Range(0, 3))
                    {
                        WatermarkOffsets watermarks = consumer.QueryWatermarkOffsets(new TopicPartition("document-events", partition), TimeSpan.FromSeconds(1));
                        if (watermarks.Low.Value != 0 || watermarks.High.Value != 0) throw new InvalidOperationException("synthetic_recreated_topic_not_empty");
                    }
                    return;
                }
                last = topic is null ? "topic absent" : "partition leader unavailable";
            }
            catch (KafkaException exception)
            { last = exception.Error.Code + ":" + exception.Error.Reason; }
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException("synthetic_partition_readiness_failed:" + last);
    }
}

internal sealed class B10AbruptVerifyHarness : IDisposable, IVerifierArtifactStore, IProducerArtifactStore
{
    private readonly string bootstrap;
    private readonly string runId;
    private readonly KafkaSessionTestSupport authentication = new();
    private IProducer<byte[], byte[]>? producer;
    private readonly Dictionary<int, long> nextOffsets = [];
    internal Dictionary<string, byte[]> Ledger { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, byte[]> Verifier { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, string> Configuration { get; } = new(StringComparer.Ordinal);
    internal int Reads { get; private set; }
    internal int Listings { get; private set; }
    internal int Creates { get; private set; }
    internal int OutputReadbacks { get; private set; }
    internal bool IncompleteListing { get; set; }
    internal bool ChangeReadback { get; set; }
    internal bool MissingReadback { get; set; }
    internal TimeProvider Clock { get; set; } = TimeProvider.System;
    internal Action<string>? BeforeRead { get; set; }
    internal Action? AfterCleanup { get; set; }
    internal Action? AfterEndQuery { get; set; }
    internal Action? AfterAssign { get; set; }
    internal Action? BeforeReRead { get; set; }
    internal bool FailReRead { get; set; }
    internal int ReReads { get; private set; }
    private int watermarkQueries;
    internal const string Output = "synthetic/C1/r1/b10-abrupt-diagnostic/nonfinal-result.json";
    internal JsonObject Result => JsonNode.Parse(Verifier[Output.Replace("synthetic", runId, StringComparison.Ordinal)])!.AsObject();
    internal JsonObject Data => Result["data"]!.AsObject();

    internal B10AbruptVerifyHarness(string bootstrap, Dictionary<string, byte[]> files, string runId = "synthetic")
    {
        this.runId = runId;
        if (runId != "synthetic") files = B10AbruptVerifyEvidence.RemapRun(files, runId);
        this.bootstrap = bootstrap; authentication.Refresh();
        foreach ((string name, byte[] bytes) in files)
        {
            if (name.StartsWith("timeout/", StringComparison.Ordinal))
                Ledger[("synthetic/C1/r1/producer/synthetic-timeout/" + name[8..]).Replace("synthetic", runId, StringComparison.Ordinal)] = bytes;
            else
            {
                string directory = name.StartsWith("first/", StringComparison.Ordinal) ? B10AbruptVerifyEvidence.First : B10AbruptVerifyEvidence.Second;
                Ledger[(B10AbruptVerifyEvidence.Prefix + directory + "/" + name[(name.IndexOf('/') + 1)..]).Replace("synthetic", runId, StringComparison.Ordinal)] = bytes;
            }
        }
        foreach ((string name, string value) in new Dictionary<string, string>
        {
            ["Mode"] = "b10-abrupt-verify", ["ScenarioId"] = "B10", ["ConfigurationId"] = "C1", ["Phase"] = "C1", ["Repeat"] = "1",
            ["RunId"] = "synthetic", ["VerifierCommit"] = B10AbruptVerifyEvidence.Commit, ["SourceCommit"] = B10AbruptVerifyEvidence.Commit,
            ["RepeatPrefix"] = "synthetic/C1/r1/", ["ExpectedInstanceId"] = "synthetic-c1-r1-loadgen",
            ["JobName"] = "synthetic-c1-r1-b10-abrupt-verify", ["PodName"] = "synthetic-verifier", ["PodUid"] = "synthetic-verifier-uid",
            ["VerifierClientId"] = "synthetic-verifier", ["NamespaceName"] = "synthetic-namespace", ["BlobServiceUri"] = "https://synthetic.blob.core.windows.net/",
            ["VerifierContainer"] = "verifier", ["LedgerContainer"] = "ledger", ["TenantId"] = "synthetic-tenant", ["WorkloadIdentityClientId"] = "synthetic-client",
            ["FederatedTokenFilePath"] = "/tmp/synthetic-token", ["LoadGenImage"] = B10AbruptVerifyEvidence.Image,
            ["VerifierImage"] = "registry.invalid/verifier@sha256:" + new string('c', 64),
            ["StartLineInputName"] = PhasePaths.StartLine("synthetic", "C1", 1), ["ProcessLifetime"] = "00:01:00", ["CleanupTimeout"] = "00:00:10",
            ["IoTimeout"] = "00:00:10", ["BlobIoTimeout"] = "00:00:10", ["TokenTimeout"] = "00:00:10", ["MetadataTimeout"] = "00:00:10",
            ["WatermarkTimeout"] = "00:00:10", ["PollSlice"] = "00:00:00.050", ["SafetyStop"] = "00:00:20",
            ["MaxOutputBytes"] = "16777216", ["MaxDiagnosticsPerCheck"] = "100", ["MaxDiagnosticBytes"] = "4096", ["MaxPartitions"] = "32",
            ["ScenarioRoot"] = B10AbruptVerifyEvidence.RepositoryRoot(), ["ArtifactPrefix"] = "synthetic/C1/r1/producer/",
            ["MaxRowBytes"] = "1048576", ["MaxRowsPerArtifact"] = "10000", ["MaxInventoryEntries"] = "10000",
            ["MaxInputEntries"] = "10000", ["MaxNativeReportBytes"] = "1048576", ["MaxBrokerEntries"] = "100",
            ["ScenarioLoadOptions:MaxDocumentCharacters"] = "131072", ["ScenarioLoadOptions:MaxNestingDepth"] = "32",
            ["ScenarioLoadOptions:MaxScalarCharacters"] = "32768", ["ScenarioLoadOptions:MaxParserEvents"] = "16384",
            ["MaxInputDepth"] = "32", ["MaxArtifactBytes"] = "268435456", ["MaxTotalInputBytes"] = "2147483648", ["MaxConsumedRecords"] = "1000", ["MaxConsumedBytes"] = "1048576"
        }) Configuration.Add("Verifier:" + name, value.Replace("synthetic", runId, StringComparison.Ordinal));
        Configuration["PARCHWELL_SOURCE_COMMIT"] = B10AbruptVerifyEvidence.Commit;
        Configuration["Telemetry:ConnectionString"] = "InstrumentationKey=synthetic-key";
        Configuration["Telemetry:ServiceName"] = "Parchwell.Verifier";
        Configuration["Telemetry:ServiceInstanceId"] = "synthetic-verifier";
        Configuration["Telemetry:ScenarioId"] = "B10";
        Configuration["Telemetry:RunId"] = runId;
        CaptureStart();
    }

    internal void CaptureStart()
    {
        using var consumer = ComponentRunHarness.Consumer(bootstrap);
        var identity = new RunIdentity("B10", "C1", runId, runId + "-c1-r1-loadgen", runId + "-c1-r1-loadgen", runId + "-c1-r1-loadgen");
        PartitionHigh[] highs = Enumerable.Range(0, 3).Select(partition => new PartitionHigh(partition,
            consumer.QueryWatermarkOffsets(new TopicPartition("document-events", partition), TimeSpan.FromSeconds(10)).High.Value)).ToArray();
        var note = new StartLineNote(1, identity, "document-events", highs, B10AbruptVerifyEvidence.Utc, B10AbruptVerifyEvidence.Commit,
            typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(), Library.VersionString, runId == "synthetic" ? [runId + "-c1-r1-loadgen", runId + "-c1-r1-loadgen2"] : [runId + "-c1-r1-loadgen", runId + "-c1-r1-loadgen-restart", runId + "-c1-r1-loadgen-successor"]);
        string start = PhasePaths.StartLine(runId, "C1", 1), inventory = PhasePaths.BrokerInventory(runId, "C1", 1);
        Verifier[start] = StartLineNoteCodec.Write(note, new OutputLimits(16777216, 100, 4096));
        using var admin = new DependentAdminClientBuilder(consumer.Handle).Build();
        Verifier[inventory] = BrokerInventoryCodec.Write(identity, [new BrokerMetadata(1, "broker-a", 9093)], B10AbruptVerifyEvidence.Commit, 16777216);
        RehashStartReceipt();
    }
    internal void RehashStartReceipt()
    {
        string start = PhasePaths.StartLine(runId, "C1", 1), inventory = PhasePaths.BrokerInventory(runId, "C1", 1);
        var identity = new RunIdentity("B10", "C1", runId, runId + "-c1-r1-loadgen", runId + "-c1-r1-loadgen", runId + "-c1-r1-loadgen");
        ArtifactReference Ref(string name) => new("verifier", name, Verifier[name].LongLength, B10AbruptVerifyEvidence.Hash(Verifier[name]));
        Verifier[PhasePaths.StartLineCompletion(runId, "C1", 1)] = PhaseWireCodec.WriteReceipt(new WorkerReceipt(1, "start-line", "B10", "C1", runId, "C1", 1,
            runId + "-c1-r1-start-line", runId + "-start", identity, B10AbruptVerifyEvidence.Commit, 0, true, B10AbruptVerifyEvidence.Utc, B10AbruptVerifyEvidence.Utc,
            [Ref(start), Ref(inventory)], null, null));
    }
    internal Task<int> RunAsync() => Program.RunAsync(Configuration.Select(pair => "--" + pair.Key + "=" + pair.Value).ToArray(), services =>
    {
        services.RemoveAll<IHostedService>();
        foreach (ServiceDescriptor item in services.Where(item => item.ServiceType.Name is "IConfigureTracerProviderBuilder" or "IConfigureMeterProviderBuilder" or "IConfigureLoggerProviderBuilder").ToArray()) services.Remove(item);
        services.RemoveAll<IVerifierArtifactStore>(); services.AddSingleton<IVerifierArtifactStore>(this);
        services.RemoveAll<IProducerArtifactStore>(); services.AddSingleton<IProducerArtifactStore>(this);
        services.RemoveAll<VerifierKafkaSessionFactory>();
        services.AddSingleton(provider => new VerifierKafkaSessionFactory(_ =>
        {
            IConsumer<byte[], byte[]> real = ComponentRunHarness.Consumer(bootstrap);
            var consumer = NativeProxy.Create<IConsumer<byte[], byte[]>>((method, arguments) =>
            {
                if (method.Name == "QueryWatermarkOffsets" && watermarkQueries++ == 3)
                {
                    ReReads++; BeforeReRead?.Invoke();
                    if (FailReRead) throw new KafkaException(new Error(ErrorCode.Local_TimedOut));
                }
                object? result;
                try { result = method.Invoke(real, arguments); }
                catch (TargetInvocationException exception) when (exception.InnerException is not null)
                { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
                if (method.Name == "QueryWatermarkOffsets") { Action? action = AfterEndQuery; AfterEndQuery = null; action?.Invoke(); }
                if (method.Name == "Assign") { Action? action = AfterAssign; AfterAssign = null; action?.Invoke(); }
                if (method.Name == "Dispose") AfterCleanup?.Invoke();
                return result;
            });
            return new VerifierKafkaSession(consumer, new DependentAdminClientBuilder(real.Handle).Build(), authentication.Authentication,
                Clock, provider.GetRequiredService<IOptions<VerifierHostOptions>>().Value, new CancellationTokenSource());
        }));
    }, Clock).WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

    internal async Task ProduceAsync(int partition, long offset, string arm = "cross_restart", string instance = B10AbruptVerifyEvidence.First,
        string attempt = "c4", long sequence = 4, bool duplicateAttempt = false, bool nullValue = false, string? key = null, byte[]? value = null)
    {
        producer ??= new ProducerBuilder<byte[], byte[]>(new ProducerConfig { BootstrapServers = bootstrap, Acks = Acks.All, EnableIdempotence = false }).Build();
        if (!nextOffsets.TryGetValue(partition, out long current))
        {
            using var observer = ComponentRunHarness.Consumer(bootstrap);
            current = observer.QueryWatermarkOffsets(new TopicPartition("document-events", partition), TimeSpan.FromSeconds(10)).High.Value;
        }
        Assert.True(current <= offset, $"requested coordinate {partition}/{offset} already exists at {current}");
        for (long index = current; index <= offset; index++)
        {
            Headers headers = new() { { "instance-id", Encoding.UTF8.GetBytes(instance) }, { "attempt-id", Encoding.UTF8.GetBytes(attempt) }, { "sequence", Encoding.UTF8.GetBytes(sequence.ToString(CultureInfo.InvariantCulture)) } };
            if (duplicateAttempt && index == offset) headers.Add("attempt-id", Encoding.UTF8.GetBytes(attempt));
            var report = await producer.ProduceAsync(new TopicPartition("document-events", partition), new Message<byte[], byte[]>
            {
                Key = Encoding.UTF8.GetBytes(index == offset ? key ?? B10AbruptVerifyEvidence.Key(arm) : "synthetic-padding"),
                Value = nullValue && index == offset ? null! : value ?? B10AbruptVerifyEvidence.Payload, Headers = headers
            }, TestContext.Current.CancellationToken);
            Assert.Equal(index, report.Offset.Value);
            nextOffsets[partition] = index + 1;
        }
    }
    public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        Reads++;
        BeforeRead?.Invoke(name);
        if (name == Output && Verifier.TryGetValue(name, out byte[]? output))
        { OutputReadbacks++; return Task.FromResult<byte[]?>(MissingReadback ? null : ChangeReadback ? [] : output); }
        return Task.FromResult(Verifier.GetValueOrDefault(name) ?? Ledger.GetValueOrDefault(name));
    }
    public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken)
    {
        Listings++; string[] names = Ledger.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        return Task.FromResult(IncompleteListing && prefix.Contains("/b10-abrupt-diagnostic/", StringComparison.Ordinal)
            ? new BlobListing(names, 17, false, "listing_incomplete") : new BlobListing(names, 1, true));
    }
    public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); Creates++; Verifier.Add(name, content); return Task.CompletedTask; }
    public void Dispose() { producer?.Dispose(); authentication.Dispose(); }
}
