using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.StartLine;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

/// <summary>Five real repeat handlers and a real Kafka window, with synthetic Blob storage.</summary>
internal sealed class B10FinalSweepHarness : IDisposable
{
    internal const string Run = "b10-1-1";
    internal const string ManagedVersion = "fixture-managed";
    internal const string NativeVersion = "fixture-native";
    internal const string OtherNativeVersion = "fixture-native-other";
    internal const string DotnetVersion = "fixture-dotnet";
    private readonly B10AbruptVerifyKafkaFixture kafka;
    private readonly B10AbruptVerifyHarness host;
    internal string Phase { get; }
    internal Dictionary<string, byte[]> Verifier => host.Verifier;
    internal Dictionary<string, byte[]> Ledger => host.Ledger;
    internal Dictionary<string, string> Configuration => host.Configuration;
    internal JsonObject Result => JsonNode.Parse(Verifier[$"{Run}/{Phase}/b10-final-sweep.json"])!.AsObject();
    internal JsonObject Data => Result["data"]!.AsObject();
    internal int Reads => host.Reads;
    internal Action<string>? BeforeRead { set => host.BeforeRead = value; }
    internal B10AbruptVerifyEvidence[] Evidence { get; } = new B10AbruptVerifyEvidence[5];
    internal static string First(int repeat) => $"{Run}-first-r{repeat}";
    internal static string Second(int repeat) => $"{Run}-second-r{repeat}";
    internal static string Timeout(int repeat) => $"{Run}-timeout-r{repeat}";
    internal static string Prefix(int repeat) => $"{Run}/C1/r{repeat}/b10-abrupt/";
    internal static string A3(int repeat) => Prefix(repeat) + "nonfinal-result.json";
    internal static string Key(int repeat, string arm) => $"B10/{Run}/C1/{repeat}/{arm}";

    private B10FinalSweepHarness(B10AbruptVerifyKafkaFixture kafka, string phase = "C1")
    {
        this.kafka = kafka; Phase = phase;
        host = new(kafka.Bootstrap, new B10AbruptVerifyEvidence().Files, Run);
        host.Ledger.Clear(); host.Verifier.Clear();
        Configuration["Verifier:Mode"] = "b10-final-sweep";
        Configuration["Verifier:Phase"] = phase; Configuration["Verifier:ConfigurationId"] = phase;
        if (phase == "C0")
        { Configuration["Verifier:NamespaceName"] = "synthetic-namespace"; Configuration["Verifier:VerifierClientId"] = "synthetic-verifier"; }
        Configuration["Verifier:JobName"] = Run + "-" + phase.ToLowerInvariant() + "-b10-final-sweep";
        Configuration["Verifier:PhasePrefix"] = Run + "/" + phase + "/";
        Configuration["Verifier:B10SettleIntervalMilliseconds"] = "60000";
        Configuration["Verifier:ProcessLifetime"] = "00:02:00";
        Configuration["Verifier:MaxInputEntries"] = "4194304";
        foreach (string name in new[] { "Repeat", "RepeatPrefix", "ExpectedInstanceId", "StartLineInputName", "ArtifactPrefix" }) Configuration.Remove("Verifier:" + name);
        ValidateModeConfiguration(Configuration);
        var verifyConfiguration = new Dictionary<string, string>(Configuration, StringComparer.Ordinal);
        ConfigureC0(verifyConfiguration, 1); ValidateModeConfiguration(verifyConfiguration);
    }

    internal static async Task<B10FinalSweepHarness> CreateAsync(B10AbruptVerifyKafkaFixture kafka,
        int lateRepeat = 0, int lostRepeat = 0, int stopAfter = 5, Action<int, B10AbruptVerifyEvidence>? change = null,
        Func<int, B10AbruptVerifyHarness, long, Task>? beforeVerify = null, int controlLostRepeat = 0, int foreignRepeat = 0, int metadataFailureRepeat = 0, bool emptyWindow = false, int differentVersionRepeat = 0)
    {
        await kafka.ResetAsync();
        var result = new B10FinalSweepHarness(kafka);
        try
        {
            for (int repeat = 1; repeat <= stopAfter; repeat++)
            {
                B10AbruptVerifyEvidence evidence = ScientificEvidence(repeat, emptyWindow, repeat == differentVersionRepeat);
                change?.Invoke(repeat, evidence);
                evidence.Rehash(); result.Evidence[repeat - 1] = evidence;
                using var run = new B10AbruptVerifyHarness(kafka.Bootstrap, evidence.Files, Run);
                RemapHarness(run, repeat);
                run.Verifier.Clear();
                foreach ((string name, byte[] bytes) in result.Verifier) run.Verifier[name] = bytes;
                foreach ((string name, byte[] bytes) in result.Ledger) run.Ledger[name] = bytes;
                result.CaptureStart(run, repeat);
                if (repeat == metadataFailureRepeat)
                {
                    foreach (string name in run.Ledger.Keys.Where(name => name.StartsWith($"{Run}/C1/r{repeat}/", StringComparison.Ordinal)).ToArray()) run.Ledger.Remove(name);
                    foreach ((string name, byte[] bytes) in run.Verifier) result.Verifier[name] = bytes;
                    foreach ((string name, byte[] bytes) in run.Ledger) result.Ledger[name] = bytes;
                    continue;
                }
                if (!emptyWindow)
                {
                    await result.ProduceAsync(run, repeat, First(repeat), "s4", "same_instance");
                    if (repeat != controlLostRepeat) await result.ProduceAsync(run, repeat, First(repeat), "t4", "same_instance");
                    await result.ProduceAsync(run, repeat, First(repeat), "c4", "cross_restart");
                    if (repeat != lateRepeat && repeat != lostRepeat && evidence.Files.ContainsKey("successor/registration.json"))
                        await result.ProduceAsync(run, repeat, Second(repeat), "r4", "cross_restart");
                    await result.ProduceAsync(run, repeat, Timeout(repeat), "timeout0", "timeout", sequence: 0);
                }
                if (repeat == lateRepeat)
                    run.AfterEndQuery = () => result.ProduceAsync(run, repeat, Second(repeat), "r4", "cross_restart").GetAwaiter().GetResult();
                if (beforeVerify is not null) await beforeVerify(repeat, run, result.High());
                Assert.Equal(0, await run.RunAsync());
                JsonObject a3 = JsonNode.Parse(run.Verifier[A3(repeat)])!.AsObject();
                if (change is null && beforeVerify is null)
                {
                    Assert.True(a3["data"]!["eligibility"]!.GetValue<string>() == "pending_sweep", $"repeat {repeat}: {a3["data"]!.ToJsonString()}");
                    Assert.Empty(a3["data"]!["eligibility_reasons"]!.AsArray());
                }
                foreach ((string name, byte[] bytes) in run.Verifier) result.Verifier[name] = bytes;
                foreach ((string name, byte[] bytes) in run.Ledger) result.Ledger[name] = bytes;
                if (repeat == foreignRepeat)
                {
                    await result.ProduceAsync(run, repeat, "foreign", "foreign", "timeout", 0);
                    result.RewriteA3(repeat, root => root["data"]!["end_line"]![0]!["offset"] = result.High());
                }
            }
            result.CaptureSweep();
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    internal static async Task<B10FinalSweepHarness> CreateC0Async(B10AbruptVerifyKafkaFixture kafka, int stopAfter = 5)
    {
        await kafka.ResetAsync(); var result = new B10FinalSweepHarness(kafka, "C0");
        try
        {
            for (int repeat = 1; repeat <= stopAfter; repeat++)
            {
                string instance = $"{Run}-ordinary-r{repeat}";
                using var seed = new HostEvidenceBuilder(instanceId: instance, clientId: instance, scenarioId: "B10", runId: Run);
                seed.ConfigureRepeat(repeat, "C0"); seed.Options.PodName = instance; seed.Options.PodUid = instance;
                JsonObject manifest = seed.Json("manifest.json"); manifest["runtime_versions"] = RuntimeVersions(false); seed.Set("manifest.json", manifest);
                seed.RebuildPublicationReferences();
                using var run = new B10AbruptVerifyHarness(kafka.Bootstrap, new B10AbruptVerifyEvidence().Files, Run);
                run.Ledger.Clear(); run.Verifier.Clear();
                foreach ((string name, byte[] bytes) in result.Ledger) run.Ledger[name] = bytes;
                foreach ((string name, byte[] bytes) in result.Verifier) run.Verifier[name] = bytes;
                foreach ((string name, byte[] bytes) in seed.LedgerObjects) run.Ledger[name] = bytes;
                ConfigureC0(run.Configuration, repeat);
                result.CaptureStart(run, repeat, "C0");
                JsonObject row = seed.Json("ledger.jsonl");
                JsonNode attempt = row["attempt"]!;
                int copies = repeat == 3 ? 2 : 1;
                for (int copy = 0; copy < copies; copy++)
                    await run.ProduceAsync(0, result.High(), instance: instance, attempt: attempt["attempt_id"]!.GetValue<string>(),
                        sequence: attempt["sequence"]!.GetValue<long>(), key: attempt["key"]!.GetValue<string>());
                Assert.Equal(1, await result.RunHostAsync(run));
                JsonObject output = JsonNode.Parse(run.Verifier[PhasePaths.Result(Run, "C0", repeat)])!.AsObject();
                Assert.Equal("incomplete", output["status"]!.GetValue<string>());
                Assert.True(output["b10_window_checks_complete"]!.GetValue<bool>());
                Assert.Empty(output["failed_checks"]!.AsArray());
                foreach ((string name, byte[] bytes) in run.Verifier) result.Verifier[name] = bytes;
                foreach ((string name, byte[] bytes) in run.Ledger) result.Ledger[name] = bytes;
            }
            result.CaptureSweep(); return result;
        }
        catch { result.Dispose(); throw; }
    }

    internal static B10AbruptVerifyEvidence ScientificEvidence(int repeat, bool emptyTimeout = false, bool differentNativeVersion = false)
    {
        var source = new B10AbruptVerifyEvidence();
        source.SetTimeoutRows(emptyTimeout ? [] : [B10AbruptVerifyEvidence.TimeoutRow("timeout0", "timeout", 0)]);
        source.Change("timeout/publication/manifest.json", root => root["runtime_versions"] = RuntimeVersions(differentNativeVersion));
        foreach (string name in source.Files.Keys.ToArray())
        {
            string text = Encoding.UTF8.GetString(source.Files[name]);
            string mapped = text.Replace("synthetic-first", $"synthetic-first-r{repeat}", StringComparison.Ordinal)
                .Replace("synthetic-second", $"synthetic-second-r{repeat}", StringComparison.Ordinal)
                .Replace("synthetic-timeout", $"synthetic-timeout-r{repeat}", StringComparison.Ordinal)
                .Replace("-c1-r1-", $"-c1-r{repeat}-", StringComparison.Ordinal)
                .Replace("/C1/1/", $"/C1/{repeat}/", StringComparison.Ordinal);
            source.Files[name] = Encoding.UTF8.GetBytes(string.Join('\n', mapped.Split('\n').Select(line =>
            {
                if (string.IsNullOrWhiteSpace(line)) return line;
                JsonNode node = JsonNode.Parse(line)!; Rewrite(node); return node.ToJsonString();
            })));
        }
        source.Frames.Clear();
        foreach (string line in Encoding.UTF8.GetString(source.Files["first/journal.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            source.Frames.Add(JsonNode.Parse(line)!.AsObject());
        source.RehashTimeout(); source.Rehash(); return source;

        void Rewrite(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                if (obj.ContainsKey("purpose")) obj["purpose"] = "scientific";
                if (obj.ContainsKey("repeat")) obj["repeat"] = repeat;
                if (obj.ContainsKey("repeat_number")) obj["repeat_number"] = repeat;
                foreach (JsonNode? child in obj.Select(pair => pair.Value).ToArray()) if (child is not null) Rewrite(child);
            }
            else if (node is JsonArray array) foreach (JsonNode? child in array) if (child is not null) Rewrite(child);
        }
    }

    private static JsonObject RuntimeVersions(bool differentNativeVersion) => new()
    {
        ["confluent_kafka"] = ManagedVersion, ["librdkafka"] = differentNativeVersion ? OtherNativeVersion : NativeVersion, ["dotnet"] = DotnetVersion, ["workload_generator"] = "synthetic-producer"
    };

    private static void RemapHarness(B10AbruptVerifyHarness run, int repeat)
    {
        foreach ((string name, string value) in run.Configuration.ToArray())
            run.Configuration[name] = value.Replace("-c1-r1-", $"-c1-r{repeat}-", StringComparison.Ordinal).Replace("/r1/", $"/r{repeat}/", StringComparison.Ordinal);
        run.Configuration["Verifier:Repeat"] = repeat.ToString(CultureInfo.InvariantCulture);
        foreach ((string name, byte[] bytes) in run.Ledger.ToArray())
        {
            run.Ledger.Remove(name);
            run.Ledger[name.Replace("/r1/", $"/r{repeat}/", StringComparison.Ordinal)
                .Replace("/b10-abrupt-diagnostic/", "/b10-abrupt/", StringComparison.Ordinal)
                .Replace($"/{Run}-timeout/", $"/{Timeout(repeat)}/", StringComparison.Ordinal)
                .Replace($"/{Run}-first/", $"/{First(repeat)}/", StringComparison.Ordinal)
                .Replace($"/{Run}-second/", $"/{Second(repeat)}/", StringComparison.Ordinal)] = bytes;
        }
    }

    private void CaptureStart(B10AbruptVerifyHarness run, int repeat, string phase = "C1")
    {
        string job = $"{Run}-{phase.ToLowerInvariant()}-r{repeat}-loadgen";
        var identity = new RunIdentity("B10", phase, Run, job, job, job);
        using var consumer = ComponentRunHarness.Consumer(kafka.Bootstrap);
        PartitionHigh[] highs = Enumerable.Range(0, 3).Select(partition => new PartitionHigh(partition,
            consumer.QueryWatermarkOffsets(new TopicPartition("document-events", partition), TimeSpan.FromSeconds(10)).High.Value)).ToArray();
        var note = new StartLineNote(1, identity, "document-events", highs, B10AbruptVerifyEvidence.Utc, B10AbruptVerifyEvidence.Commit,
            typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(), Library.VersionString,
            phase == "C0" ? [job] : [job, job + "-restart", job + "-successor"]);
        string start = PhasePaths.StartLine(Run, phase, repeat), inventory = PhasePaths.BrokerInventory(Run, phase, repeat);
        run.Verifier[start] = StartLineNoteCodec.Write(note, new OutputLimits(16777216, 100, 4096));
        run.Verifier[inventory] = BrokerInventoryCodec.Write(identity, [new BrokerMetadata(1, "broker-a", 9093)], B10AbruptVerifyEvidence.Commit, 16777216);
        ArtifactReference Ref(string name) => new("verifier", name, run.Verifier[name].LongLength, B10AbruptVerifyEvidence.Hash(run.Verifier[name]));
        run.Verifier[PhasePaths.StartLineCompletion(Run, phase, repeat)] = PhaseWireCodec.WriteReceipt(new WorkerReceipt(1, "start-line", "B10", phase, Run, phase, repeat,
            $"{Run}-{phase.ToLowerInvariant()}-r{repeat}-start-line", Run + "-start", identity, B10AbruptVerifyEvidence.Commit, 0, true, B10AbruptVerifyEvidence.Utc, B10AbruptVerifyEvidence.Utc,
            [Ref(start), Ref(inventory)], null, null));
    }

    private Task ProduceAsync(B10AbruptVerifyHarness run, int repeat, string instance, string attempt, string arm, long sequence = 4) =>
        run.ProduceAsync(0, High(), arm, instance, attempt, sequence, key: arm == "timeout" ? "timeout" : Key(repeat, arm));
    private long High()
    {
        using var consumer = ComponentRunHarness.Consumer(kafka.Bootstrap);
        return consumer.QueryWatermarkOffsets(new TopicPartition("document-events", 0), TimeSpan.FromSeconds(10)).High.Value;
    }
    internal Task AddRecordAsync(int repeat, string instance, string attempt, string arm = "timeout", long sequence = 0) => ProduceAsync(host, repeat, instance, attempt, arm, sequence);
    internal void CaptureSweep(long? high = null)
    {
        DateTimeOffset completed = Verifier.Where(pair => pair.Key.EndsWith("verify-completion.json", StringComparison.Ordinal))
            .Select(pair => DateTimeOffset.Parse(JsonNode.Parse(pair.Value)!["completed_utc"]!.GetValue<string>(), CultureInfo.InvariantCulture))
            .DefaultIfEmpty(DateTimeOffset.Parse(B10AbruptVerifyEvidence.Utc, CultureInfo.InvariantCulture)).Max();
        DateTimeOffset captured = TimeProvider.System.GetUtcNow();
        Verifier[PhasePaths.SweepLine(Run, Phase)] = B10SweepEvidenceCodec.WriteSweepLine(new B10SweepLine(1, "B10", Phase, Run, Phase, B10AbruptVerifyEvidence.Commit,
            "document-events", [new(0, high ?? High()), new(1, 0), new(2, 0)], captured, completed, captured.AddSeconds(-60), 60000));
    }
    internal Task<int> RunAsync() => RunHostAsync(host);
    private async Task<int> RunHostAsync(B10AbruptVerifyHarness target)
    {
        ValidateModeConfiguration(target.Configuration);
        using var authentication = new KafkaSessionTestSupport(); authentication.Refresh();
        int exit = await Program.RunAsync(target.Configuration.Select(pair => "--" + pair.Key + "=" + pair.Value).ToArray(), services =>
        {
            services.RemoveAll<IHostedService>();
            foreach (ServiceDescriptor item in services.Where(item => item.ServiceType.Name is "IConfigureTracerProviderBuilder" or "IConfigureMeterProviderBuilder" or "IConfigureLoggerProviderBuilder").ToArray()) services.Remove(item);
            services.RemoveAll<IVerifierArtifactStore>(); services.AddSingleton<IVerifierArtifactStore>(target);
            services.RemoveAll<IProducerArtifactStore>(); services.AddSingleton<IProducerArtifactStore>(target);
            services.RemoveAll<ICompletionReceiptPublisher>(); services.AddSingleton<ICompletionReceiptPublisher>(new ReceiptPublisher(target));
            services.RemoveAll<VerifierKafkaSessionFactory>();
            services.AddSingleton(provider => new VerifierKafkaSessionFactory(_ =>
            {
                IConsumer<byte[], byte[]> consumer = ComponentRunHarness.Consumer(kafka.Bootstrap);
                return new VerifierKafkaSession(consumer, new DependentAdminClientBuilder(consumer.Handle).Build(), authentication.Authentication,
                    TimeProvider.System, provider.GetRequiredService<IOptions<VerifierHostOptions>>().Value, new CancellationTokenSource());
            }));
        }, TimeProvider.System).WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        if (target.Verifier.TryGetValue(PhasePaths.B10FinalSweep(Run, Phase), out byte[]? projection))
            TestContext.Current.TestOutputHelper?.WriteLine(Encoding.UTF8.GetString(projection));
        return exit;
    }
    private static void ConfigureC0(Dictionary<string, string> configuration, int repeat)
    {
        foreach ((string name, string value) in new Dictionary<string, string>
        {
            ["Mode"] = "verify", ["NamespaceName"] = "synthetic-namespace", ["VerifierClientId"] = "synthetic-verifier", ["ProcessLifetime"] = "00:02:00", ["MaxInputEntries"] = "4194304", ["Phase"] = "C0", ["ConfigurationId"] = "C0", ["Repeat"] = repeat.ToString(CultureInfo.InvariantCulture),
            ["RepeatPrefix"] = $"{Run}/C0/r{repeat}/", ["PhasePrefix"] = $"{Run}/C0/", ["ExpectedInstanceId"] = $"{Run}-c0-r{repeat}-loadgen",
            ["JobName"] = $"{Run}-c0-r{repeat}-verify", ["StartLineInputName"] = PhasePaths.StartLine(Run, "C0", repeat),
            ["ResultOutputName"] = PhasePaths.Result(Run, "C0", repeat), ["ArtifactPrefix"] = PhasePaths.ProducersPrefix(Run, "C0", repeat)
        }) configuration["Verifier:" + name] = value;
    }
    private static void ValidateModeConfiguration(Dictionary<string, string> configuration)
    {
        IConfigurationRoot settings = new ConfigurationBuilder().AddInMemoryCollection(configuration.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))).Build();
        var selected = new VerifierHostOptions(); new VerifierHostOptionsSetup(settings).Configure(selected);
        ValidateOptionsResult validation = new VerifierHostOptionsValidator().Validate(null, selected);
        Assert.True(validation.Succeeded, string.Join("; ", validation.Failures ?? []));
    }
    private sealed class ReceiptPublisher(B10AbruptVerifyHarness target) : ICompletionReceiptPublisher
    {
        public Task PublishAsync(VerifierHostOptions options, string container, string name, byte[] bytes, CancellationToken cancellationToken)
        { Assert.Equal("verifier", container); return target.CreateAsync(name, bytes, cancellationToken); }
    }
    internal JsonObject Candidate => JsonNode.Parse(Verifier[PhasePaths.Candidate(Run, Phase)])!.AsObject();
    internal Task<int> JudgeAsync()
    {
        Configuration["Verifier:Mode"] = "judge"; Configuration["Verifier:JobName"] = PhaseIdentity.JudgeJobName(Run, Phase);
        Configuration["Verifier:CandidateOutputName"] = PhasePaths.Candidate(Run, Phase); return RunAsync();
    }
    internal JsonObject Repeat(int repeat) => Data["repeats"]![repeat - 1]!.AsObject();
    internal void RemoveA3(int repeat) { Verifier.Remove(A3(repeat)); Verifier.Remove(Prefix(repeat) + "verify-completion.json"); }
    internal void RewriteStartHigh(int repeat, long high)
    {
        string name = PhasePaths.StartLine(Run, Phase, repeat);
        JsonObject note = JsonNode.Parse(Verifier[name])!.AsObject(); note["partition_highs"]![0]!["high"] = high;
        Verifier[name] = Encoding.UTF8.GetBytes(note.ToJsonString());
        string receiptName = PhasePaths.StartLineCompletion(Run, Phase, repeat);
        JsonObject receipt = JsonNode.Parse(Verifier[receiptName])!.AsObject();
        receipt["artifacts"]![0]!["length"] = Verifier[name].LongLength;
        receipt["artifacts"]![0]!["sha256"] = B10AbruptVerifyEvidence.Hash(Verifier[name]);
        Verifier[receiptName] = Encoding.UTF8.GetBytes(receipt.ToJsonString());
    }
    internal void AddC0Producer(int repeat, string instance)
    {
        Assert.Equal("C0", Phase);
        using var seed = new HostEvidenceBuilder(instanceId: instance, clientId: instance, scenarioId: "B10", runId: Run);
        seed.ConfigureRepeat(repeat, "C0"); seed.Options.PodName = instance; seed.Options.PodUid = instance;
        JsonObject manifest = seed.Json("manifest.json"); manifest["runtime_versions"] = RuntimeVersions(false); seed.Set("manifest.json", manifest);
        seed.RebuildPublicationReferences();
        foreach ((string name, byte[] bytes) in seed.LedgerObjects) Ledger[name] = bytes;
    }
    internal void ReplaceTimeoutInstance(int repeat, int ownerRepeat)
    {
        B10AbruptVerifyEvidence evidence = Evidence[repeat - 1];
        foreach (string name in evidence.Files.Keys.Where(name => name.StartsWith("timeout/", StringComparison.Ordinal)).ToArray())
            evidence.Files[name] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(evidence.Files[name]).Replace($"synthetic-timeout-r{repeat}", $"synthetic-timeout-r{ownerRepeat}", StringComparison.Ordinal));
        evidence.RehashTimeout();
        Dictionary<string, byte[]> mapped = B10AbruptVerifyEvidence.RemapRun(evidence.Files, Run);
        string oldPrefix = PhasePaths.ProducerPrefix(Run, "C1", repeat, Timeout(repeat));
        foreach (string name in Ledger.Keys.Where(name => name.StartsWith(oldPrefix, StringComparison.Ordinal)).ToArray()) Ledger.Remove(name);
        string prefix = PhasePaths.ProducerPrefix(Run, "C1", repeat, Timeout(ownerRepeat));
        foreach ((string name, byte[] bytes) in mapped.Where(pair => pair.Key.StartsWith("timeout/", StringComparison.Ordinal))) Ledger[prefix + name[8..]] = bytes;
        RewriteA3(repeat, root => root["data"]!["timeout_instance_id"] = Timeout(ownerRepeat));
    }
    internal void RewriteA3(int repeat, Action<JsonObject> change)
    {
        JsonObject root = JsonNode.Parse(Verifier[A3(repeat)])!.AsObject(); change(root); Verifier[A3(repeat)] = Encoding.UTF8.GetBytes(root.ToJsonString());
        string receiptName = Prefix(repeat) + "verify-completion.json";
        JsonObject receipt = JsonNode.Parse(Verifier[receiptName])!.AsObject();
        receipt["artifacts"]![0]!["length"] = Verifier[A3(repeat)].LongLength;
        receipt["artifacts"]![0]!["sha256"] = B10AbruptVerifyEvidence.Hash(Verifier[A3(repeat)]);
        Verifier[receiptName] = Encoding.UTF8.GetBytes(receipt.ToJsonString());
    }
    public void Dispose() => host.Dispose();
}
