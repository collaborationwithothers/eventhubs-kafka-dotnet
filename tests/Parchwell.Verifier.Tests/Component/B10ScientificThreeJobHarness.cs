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

internal sealed class B10ScientificThreeJobHarness : IDisposable, IVerifierArtifactStore, IProducerArtifactStore
{
    private readonly string bootstrap;
    private readonly string runId;
    private readonly int repeat;
    internal string? FailListingPrefix { get; set; }
    internal string? FailCreateSuffix { get; set; }
    internal string? BadReadbackSuffix { get; set; }
    internal List<string> Writes { get; } = [];
    internal List<string> ListedPrefixes { get; } = [];
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
    internal string ResultName => $"{runId}/C1/r{repeat}/b10-abrupt/nonfinal-result.json";
    internal string ReceiptName => $"{runId}/C1/r{repeat}/b10-abrupt/verify-completion.json";
    internal JsonObject Result => JsonNode.Parse(Verifier[ResultName])!.AsObject();
    internal JsonObject Data => Result["data"]!.AsObject();

    internal B10ScientificThreeJobHarness(string bootstrap, Dictionary<string, byte[]> files, string runId = "b10-1-1", int repeat = 3)
    {
        this.runId = runId; this.repeat = repeat;
        files = ScientificFiles(files, runId, repeat);
        this.bootstrap = bootstrap; authentication.Refresh();
        foreach ((string name, byte[] bytes) in files)
        {
            if (name.StartsWith("timeout/", StringComparison.Ordinal))
                Ledger[("synthetic/C1/r1/producer/synthetic-timeout/" + name[8..]).Replace("synthetic", runId, StringComparison.Ordinal).Replace("/r1/", $"/r{repeat}/", StringComparison.Ordinal).Replace("b10-abrupt-diagnostic/", "b10-abrupt/", StringComparison.Ordinal)] = bytes;
            else
            {
                string directory = name.StartsWith("first/", StringComparison.Ordinal) ? B10AbruptVerifyEvidence.First : B10AbruptVerifyEvidence.Second;
                Ledger[(B10AbruptVerifyEvidence.Prefix + directory + "/" + name[(name.IndexOf('/') + 1)..]).Replace("synthetic", runId, StringComparison.Ordinal).Replace("/r1/", $"/r{repeat}/", StringComparison.Ordinal).Replace("b10-abrupt-diagnostic/", "b10-abrupt/", StringComparison.Ordinal)] = bytes;
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
            ["StartLineInputName"] = PhasePaths.StartLine("synthetic", "C1", repeat), ["ProcessLifetime"] = "00:01:00", ["CleanupTimeout"] = "00:00:10",
            ["IoTimeout"] = "00:00:10", ["BlobIoTimeout"] = "00:00:10", ["TokenTimeout"] = "00:00:10", ["MetadataTimeout"] = "00:00:10",
            ["WatermarkTimeout"] = "00:00:10", ["PollSlice"] = "00:00:00.050", ["SafetyStop"] = "00:00:20",
            ["MaxOutputBytes"] = "16777216", ["MaxDiagnosticsPerCheck"] = "100", ["MaxDiagnosticBytes"] = "4096", ["MaxPartitions"] = "32",
            ["ScenarioRoot"] = B10AbruptVerifyEvidence.RepositoryRoot(), ["ArtifactPrefix"] = "synthetic/C1/r1/producer/",
            ["MaxRowBytes"] = "1048576", ["MaxRowsPerArtifact"] = "10000", ["MaxInventoryEntries"] = "10000",
            ["MaxInputEntries"] = "10000", ["MaxNativeReportBytes"] = "1048576", ["MaxBrokerEntries"] = "100",
            ["ScenarioLoadOptions:MaxDocumentCharacters"] = "131072", ["ScenarioLoadOptions:MaxNestingDepth"] = "32",
            ["ScenarioLoadOptions:MaxScalarCharacters"] = "32768", ["ScenarioLoadOptions:MaxParserEvents"] = "16384",
            ["MaxInputDepth"] = "32", ["MaxArtifactBytes"] = "268435456", ["MaxTotalInputBytes"] = "2147483648", ["MaxConsumedRecords"] = "1000", ["MaxConsumedBytes"] = "1048576"
        }) Configuration.Add("Verifier:" + name, value.Replace("synthetic", runId, StringComparison.Ordinal).Replace("/r1/", $"/r{repeat}/", StringComparison.Ordinal).Replace("-r1-", $"-r{repeat}-", StringComparison.Ordinal));
        Configuration["Verifier:Repeat"] = repeat.ToString(CultureInfo.InvariantCulture);
        Configuration["PARCHWELL_SOURCE_COMMIT"] = B10AbruptVerifyEvidence.Commit;
        Configuration["Telemetry:ConnectionString"] = "InstrumentationKey=synthetic-key";
        Configuration["Telemetry:ServiceName"] = "Parchwell.Verifier";
        Configuration["Telemetry:ServiceInstanceId"] = "synthetic-verifier";
        Configuration["Telemetry:ScenarioId"] = "B10";
        Configuration["Telemetry:RunId"] = runId;
        CaptureStart();
    }

    private static Dictionary<string, byte[]> ScientificFiles(Dictionary<string, byte[]> files, string runId, int repeat)
    {
        var evidence = new B10AbruptVerifyEvidence(); evidence.Files.Clear(); evidence.Frames.Clear();
        foreach ((string name, byte[] bytes) in B10AbruptVerifyEvidence.RemapRun(files, runId))
        {
            string text = Encoding.UTF8.GetString(bytes).Replace("diagnostic_trial", "scientific", StringComparison.Ordinal)
                .Replace("\"repeat\":1", "\"repeat\":" + repeat, StringComparison.Ordinal)
                .Replace("\"repeat_number\":1", "\"repeat_number\":" + repeat, StringComparison.Ordinal)
                .Replace("-r1-", $"-r{repeat}-", StringComparison.Ordinal).Replace("/r1/", $"/r{repeat}/", StringComparison.Ordinal)
                .Replace("/C1/1/", $"/C1/{repeat}/", StringComparison.Ordinal);
            evidence.Files[name] = Encoding.UTF8.GetBytes(text);
        }
        foreach (string line in Encoding.UTF8.GetString(evidence.Files["first/journal.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            evidence.Frames.Add(JsonNode.Parse(line)!.AsObject());
        evidence.RehashTimeout(); evidence.Rehash(); return evidence.Files;
    }

    internal void CaptureStart()
    {
        using var consumer = ComponentRunHarness.Consumer(bootstrap);
        var identity = new RunIdentity("B10", "C1", runId, runId +  $"-c1-r{repeat}-loadgen", runId +  $"-c1-r{repeat}-loadgen", runId +  $"-c1-r{repeat}-loadgen");
        PartitionHigh[] highs = Enumerable.Range(0, 3).Select(partition => new PartitionHigh(partition,
            consumer.QueryWatermarkOffsets(new TopicPartition("document-events", partition), TimeSpan.FromSeconds(10)).High.Value)).ToArray();
        var note = new StartLineNote(1, identity, "document-events", highs, B10AbruptVerifyEvidence.Utc, B10AbruptVerifyEvidence.Commit,
            typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(), Library.VersionString, [runId +  $"-c1-r{repeat}-loadgen", runId +  $"-c1-r{repeat}-loadgen-restart", runId +  $"-c1-r{repeat}-loadgen-successor"]);
        string start = PhasePaths.StartLine(runId, "C1", repeat), inventory = PhasePaths.BrokerInventory(runId, "C1", repeat);
        Verifier[start] = StartLineNoteCodec.Write(note, new OutputLimits(16777216, 100, 4096));
        using var admin = new DependentAdminClientBuilder(consumer.Handle).Build();
        Verifier[inventory] = BrokerInventoryCodec.Write(identity, [new BrokerMetadata(1, "broker-a", 9093)], B10AbruptVerifyEvidence.Commit, 16777216);
        RehashStartReceipt();
    }
    internal void RehashStartReceipt()
    {
        string start = PhasePaths.StartLine(runId, "C1", repeat), inventory = PhasePaths.BrokerInventory(runId, "C1", repeat);
        var identity = new RunIdentity("B10", "C1", runId, runId +  $"-c1-r{repeat}-loadgen", runId +  $"-c1-r{repeat}-loadgen", runId +  $"-c1-r{repeat}-loadgen");
        ArtifactReference Ref(string name) => new("verifier", name, Verifier[name].LongLength, B10AbruptVerifyEvidence.Hash(Verifier[name]));
        Verifier[PhasePaths.StartLineCompletion(runId, "C1", repeat)] = PhaseWireCodec.WriteReceipt(new WorkerReceipt(1, "start-line", "B10", "C1", runId, "C1", repeat,
            runId +  $"-c1-r{repeat}-start-line", runId + "-start", identity, B10AbruptVerifyEvidence.Commit, 0, true, B10AbruptVerifyEvidence.Utc, B10AbruptVerifyEvidence.Utc,
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
        if (BadReadbackSuffix is not null && name.EndsWith(BadReadbackSuffix, StringComparison.Ordinal) && Verifier.ContainsKey(name)) return Task.FromResult<byte[]?>([]);
        if (name == ResultName && Verifier.TryGetValue(name, out byte[]? output))
        { OutputReadbacks++; return Task.FromResult<byte[]?>(MissingReadback ? null : ChangeReadback ? [] : output); }
        return Task.FromResult(Verifier.GetValueOrDefault(name) ?? Ledger.GetValueOrDefault(name));
    }
    public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken)
    {
        Listings++; ListedPrefixes.Add(prefix);
        if (prefix == FailListingPrefix) throw new Azure.RequestFailedException(500, "synthetic_listing_failed");
        string[] names = Ledger.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        return Task.FromResult(IncompleteListing && prefix.Contains("/b10-abrupt-diagnostic/", StringComparison.Ordinal)
            ? new BlobListing(names, 17, false, "listing_incomplete") : new BlobListing(names, 1, true));
    }
    public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); Creates++;
        if (FailCreateSuffix is not null && name.EndsWith(FailCreateSuffix, StringComparison.Ordinal)) throw new InvalidOperationException("synthetic_create_failed");
        Writes.Add(name); Verifier.Add(name, content); return Task.CompletedTask; }
    public void Dispose() { producer?.Dispose(); authentication.Dispose(); }
}
