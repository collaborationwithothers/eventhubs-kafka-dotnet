using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.LoadGen.Tests.Component;
using Parchwell.LoadGen.Run.Workload;
using Parchwell.LoadGen.Run.Input;
using Parchwell.Scenarios;
using Microsoft.Extensions.Options;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Tier", "Docker")]
public sealed class B10AbruptEndToEndTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Given_RealKafka_When_ParentChildAndSuccessorRunOnce_Then_ConfirmedJournalMembersAreResentWithIdenticalBytes()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        await using var proxy = new B10AbruptKafkaProxy();
        await using var kafka = await LocalKafkaFixture.StartAsync(cancellation, advertisedAddress: proxy.BootstrapServers);
        proxy.SetUpstream(kafka.BootstrapServers);
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = proxy.BootstrapServers }).Build();
        await admin.CreateTopicsAsync([new TopicSpecification { Name = "document-events", NumPartitions = 1, ReplicationFactor = 1 }]);
        Metadata metadata = admin.GetMetadata(TimeSpan.FromSeconds(10));
        string[] brokerNames = metadata.Brokers.Select(broker => $"{broker.Host}:{broker.Port}/{broker.BrokerId}").ToArray();
        Assert.NotEmpty(brokerNames);
        using var first = new B10AbruptProcessFixture("real-kafka", proxy.BootstrapServers, realTime: true) { PreserveArtifacts = true };
        WriteBrokerBinding(first, brokerNames);
        var loader = new ScenarioLoader(new FileSystemScenarioSource(first.Options.ScenarioSourceRoot), Options.Create(new ScenarioLoadOptions { MaxDocumentCharacters = 1048576, MaxNestingDepth = 32, MaxScalarCharacters = 65536, MaxParserEvents = 100000 }));
        LoadedScenario loaded = await loader.LoadAsync("B10", cancellation);
        byte[] expectedPayload = WorkloadGenerator.GeneratePayload(WorkloadGenerator.Generate(loaded.Profile, 186000, 900000, cancellation).First(), loaded.Profile.Seed);
        // This is one real run with the policy's unchanged 120 s release and the probe producer settings.
        int parentExit = await first.RunRoleAsync("parent", TimeSpan.FromSeconds(300));
        Assert.Equal(0, parentExit);
        string firstPrefix = Path.Combine(first.StorageRoot, "test", "C1", "r1", "b10-abrupt-diagnostic", "first");
        byte[] terminationBytes = await File.ReadAllBytesAsync(Path.Combine(firstPrefix, "termination.json"), cancellation);
        JsonElement termination = B10AbruptWire.ReadEnvelope(terminationBytes, "termination");
        JsonElement data = termination.GetProperty("data");
        output.WriteLine("termination=" + termination.GetRawText());
        output.WriteLine("journal_tail=" + JsonSerializer.Serialize(first.Records().TakeLast(8)));
        output.WriteLine("process_output_tail=" + first.ProcessOutput);
        Assert.Equal("intended_self_kill", data.GetProperty("termination").GetString());
        Assert.Equal(137, data.GetProperty("exit_code_local_observation").GetInt32());
        Assert.True(data.GetProperty("successor_permitted").GetBoolean());
        Assert.NotEmpty(data.GetProperty("control_unconfirmed_attempt_ids").EnumerateArray());
        byte[] journalBytes = await File.ReadAllBytesAsync(Path.Combine(firstPrefix, "journal.jsonl"), cancellation);
        var snapshot = B10AbruptJournal.Read(journalBytes, first.Options.Identity());
        Assert.Empty(snapshot.Gaps);
        Assert.Empty(snapshot.Failures);
        Assert.NotEmpty(snapshot.KillUnconfirmedAttemptIds);
        B10AbruptWire.Frame intent = Assert.Single(snapshot.Frames, record => record.Kind == "kill_intent");
        B10AbruptWire.Frame cut = Assert.Single(snapshot.Frames, record => record.Kind == "cut");
        string cutAttempt = cut.Data.GetProperty("cut_attempt_id").GetString()!;
        B10AbruptWire.Frame accepted = Assert.Single(snapshot.Frames, record => record.Kind == "accepted" && record.Data.GetProperty("attempt_id").GetString() == cutAttempt);
        decimal elapsedMicroseconds = (decimal)(intent.MonotonicTicks - accepted.MonotonicTicks) * 1000000 / intent.ClockFrequency;
        output.WriteLine($"local_observation parent_exit=0 exit_code_local_observation=137 runtime={Environment.Version} kernel={File.ReadAllText("/proc/sys/kernel/osrelease").Trim()} cut_accepted_to_kill_intent_microseconds={elapsedMicroseconds.ToString(CultureInfo.InvariantCulture)}");

        using var successor = new B10AbruptProcessFixture("real-kafka", proxy.BootstrapServers, realTime: true, storageRoot: first.StorageRoot) { PreserveArtifacts = true };
        successor.EnvironmentValues["B10Abrupt__PodName"] = "successor";
        successor.EnvironmentValues["B10Abrupt__PodUid"] = "successor-uid";
        successor.EnvironmentValues["B10Abrupt__JobName"] = "test-c1-r1-loadgen-successor";
        successor.EnvironmentValues.Remove("B10Abrupt__StartLineBindingPath");
        string binding = Path.Combine(successor.Directory, "predecessor.json");
        successor.EnvironmentValues["B10Abrupt__PredecessorBindingPath"] = binding;
        JsonElement startLine = JsonDocument.Parse(await File.ReadAllBytesAsync(first.EnvironmentValues["B10Abrupt__StartLineBindingPath"], cancellation)).RootElement.Clone();
        await File.WriteAllBytesAsync(binding, JsonSerializer.SerializeToUtf8Bytes(new
        {
            first_pod = new { job_name = "test-c1-r1-loadgen-restart", pod_name = "first", pod_uid = "uid", image_reference = first.Options.ImageReference, source_commit = first.Options.SourceCommit, finished_utc = B10AbruptPublisher.Utc(TimeProvider.System.GetUtcNow()) },
            termination = B10AbruptPublisher.Describe("termination.json", terminationBytes), start_line_binding = startLine,
            release_utc = intent.Data.GetProperty("release_utc").GetString(), runner_utc = B10AbruptPublisher.Utc(TimeProvider.System.GetUtcNow()), runner_monotonic_ticks = Stopwatch.GetTimestamp(), cross_host_timing = "not_verified"
        }, B10AbruptWire.JsonOptions), cancellation);
        int successorExit = await successor.RunRoleAsync("successor", TimeSpan.FromSeconds(300));
        Assert.Equal(0, successorExit);
        string completionPath = Path.Combine(first.StorageRoot, "test", "C1", "r1", "b10-abrupt-diagnostic", "successor", "completion.json");
        JsonElement receipt = B10AbruptWire.ReadEnvelope(await File.ReadAllBytesAsync(completionPath, cancellation), "successor_receipt").GetProperty("data");
        JsonElement[] invocations = receipt.GetProperty("invocations").EnumerateArray().ToArray();
        Assert.Equal(snapshot.KillUnconfirmedAttemptIds, invocations.Select(invocation => invocation.GetProperty("predecessor_attempt_id").GetString()!));
        using var consumer = new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
        {
            BootstrapServers = proxy.BootstrapServers, GroupId = "synthetic-b10-" + Guid.NewGuid().ToString("N"), EnableAutoCommit = false, AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();
        consumer.Assign(new TopicPartitionOffset("document-events", new Partition(0), Offset.Beginning));
        var observed = new Dictionary<string, Message<byte[], byte[]>>(StringComparer.Ordinal);
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(20) && invocations.Any(invocation => !observed.ContainsKey(invocation.GetProperty("attempt_id").GetString()!)))
        {
            ConsumeResult<byte[], byte[]>? record = consumer.Consume(TimeSpan.FromMilliseconds(100));
            if (record?.Message.Headers is null) continue;
            string attempt = Encoding.UTF8.GetString(record.Message.Headers.GetLastBytes("attempt-id"));
            observed.TryAdd(attempt, record.Message);
        }
        foreach (JsonElement invocation in invocations)
        {
            string predecessor = invocation.GetProperty("predecessor_attempt_id").GetString()!;
            string attempt = invocation.GetProperty("attempt_id").GetString()!;
            Assert.NotEqual(predecessor, attempt);
            JsonElement original = Assert.Single(snapshot.Frames, frame => frame.Kind == "prepared" && frame.Data.GetProperty("attempt_id").GetString() == predecessor).Data;
            Assert.True(observed.TryGetValue(attempt, out Message<byte[], byte[]>? resend), "successor_attempt_not_consumed:" + attempt);
            Assert.Equal(Encoding.UTF8.GetBytes(original.GetProperty("key").GetString()!), resend.Key);
            Assert.Equal(original.GetProperty("sequence").GetInt64().ToString(CultureInfo.InvariantCulture), Encoding.UTF8.GetString(resend.Headers.GetLastBytes("sequence")));
            Assert.Equal(expectedPayload, resend.Value);
            Assert.Equal(original.GetProperty("payload_sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(resend.Value)));
            Assert.Equal("successor", Encoding.UTF8.GetString(resend.Headers.GetLastBytes("instance-id")));
            if (observed.TryGetValue(predecessor, out Message<byte[], byte[]>? originalMessage)) Assert.Equal(originalMessage.Value, resend.Value);
        }
        output.WriteLine($"local_observation successor_exit=0 consumed_resends={invocations.Length}");
        successor.PreserveArtifacts = false;
        first.PreserveArtifacts = false;
    }

    private static void WriteBrokerBinding(B10AbruptProcessFixture fixture, string[] names)
    {
        string path = fixture.EnvironmentValues["B10Abrupt__StartLineBindingPath"];
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(new
        {
            start_line = "synthetic-start-line", completion = "synthetic-completion",
            broker_inventory = JsonSerializer.Serialize(new { schema_version = 1, identity = new { scenario_id = "B10", configuration_id = "C1", run_id = "test" },
                brokers = names.Select(name => new { name }), source = "kafka_metadata", source_commit = new string('a', 40) })
        }));
    }
}
