using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Enrichment.ConsumerProcessing;
using Parchwell.Kafka.Configuration;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing.Component;

#pragma warning disable CA1707 // Repository test naming uses Given_When_Then.
public sealed class ConsumerProcessingComponentTests
{
    private const string Topic = "document-events";

    [Fact]
    [Trait("Tier", "Docker")]
    [Trait("PlanTest", "CT1")]
    public async Task Given_TwoAssignedMembers_When_AllPartitionsAreProcessed_Then_EveryOffsetIsRecordedOnceAndBrokerCommitsAgree()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await using var broker = await LocalKafkaFixture.StartAsync(deadline.Token);
        await CreateTopicAsync(broker, 6, deadline.Token);
        var runId = NewRunId();
        var subject = StartMember(broker, runId, "subject", Plan(), deadline.Token);
        var witness = StartMember(broker, runId, "witness", Plan(), deadline.Token);
        await WaitForAssignmentsAsync(subject, witness, 6, deadline.Token);
        var produced = await ProduceAsync(broker, 6, 100, 0, deadline.Token);
        await AssertCompletedAsync(subject, witness, deadline.Token);

        var processed = subject.Store.Rows.Concat(witness.Store.Rows).Where(IsProcessed).ToArray();
        Assert.Equal(produced.OrderBy(value => value.Partition).ThenBy(value => value.Offset),
            processed.Select(Coordinate).OrderBy(value => value.Partition).ThenBy(value => value.Offset));
        Assert.All(processed.GroupBy(Coordinate), group => Assert.Single(group));

        using var reader = new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
        {
            BootstrapServers = broker.BootstrapServers,
            GroupId = subject.Options.GroupId,
            EnableAutoCommit = false,
            IsolationLevel = IsolationLevel.ReadCommitted
        }).Build();
        var offsets = reader.Committed(Enumerable.Range(0, 6)
            .Select(partition => new TopicPartition(Topic, new Partition(partition))), TimeSpan.FromSeconds(20));
        Assert.Equal(6, offsets.Count);
        var successfulEntries = subject.Store.Rows.Concat(witness.Store.Rows)
            .Where(row => Kind(row) == "commit" && row.GetProperty("error_code").GetInt32() == 0)
            .SelectMany(row => row.GetProperty("offsets").EnumerateArray())
            .Where(entry => entry.GetProperty("error_code").GetInt32() == 0).ToArray();
        Assert.All(offsets, offset =>
        {
            Assert.Equal(100, offset.Offset.Value);
            Assert.Contains(successfulEntries, entry => entry.GetProperty("topic").GetString() == Topic &&
                entry.GetProperty("partition").GetInt32() == offset.Partition.Value &&
                entry.GetProperty("offset").GetInt64() == offset.Offset.Value);
        });
    }

    [Fact]
    [Trait("Tier", "Docker")]
    [Trait("PlanTest", "CT2")]
    public async Task Given_InlineSlowSubject_When_PollIntervalExpires_Then_WitnessReprocessesItsSlowMessageAndSubjectRecordsLoss()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await using var broker = await LocalKafkaFixture.StartAsync(deadline.Token);
        await CreateTopicAsync(broker, 2, deadline.Token);
        var runId = NewRunId();
        var subject = StartMember(broker, runId, "subject", Plan("inline"), deadline.Token);
        var witness = StartMember(broker, runId, "witness", Plan(pollLimit: true), deadline.Token);
        await WaitForAssignmentsAsync(subject, witness, 2, deadline.Token);
        await ProduceAsync(broker, 2, 10, 0, deadline.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(7000), deadline.Token);
        await ProduceAsync(broker, 2, 1, 10, deadline.Token);
        await AssertCompletedAsync(subject, witness, deadline.Token);

        var slow = Assert.Single(subject.Store.Rows, row => IsProcessed(row) && row.GetProperty("slow").GetBoolean());
        var assignment = NewestAssignmentBefore(subject.Store.Rows, slow);
        var partition = Assert.Single(Partitions(assignment));
        Assert.Equal(partition, slow.GetProperty("partition").GetInt32());
        Assert.Equal(10, slow.GetProperty("offset").GetInt64());
        Assert.Contains(subject.Store.Rows, row => IsEvent(row, "lost") && Sequence(row) > Sequence(slow));
        var started = Utc(slow, "processing_started_utc");
        var ended = Utc(slow, "processing_ended_utc");
        Assert.Contains(witness.Store.Rows, row => IsEvent(row, "assigned") && Partitions(row).Contains(partition) &&
            Utc(row, "event_utc") >= started.AddMilliseconds(9500) && Utc(row, "event_utc") < ended);
        Assert.Contains(subject.Store.Rows, row => IsMessage(row, partition, 10));
        Assert.Contains(witness.Store.Rows, row => IsMessage(row, partition, 10));
        Assert.All(subject.Store.Rows.Concat(witness.Store.Rows).Where(row => Kind(row) == "commit"),
            row => Assert.Equal("callback", row.GetProperty("source").GetString()));
    }

    [Fact]
    [Trait("Tier", "Docker")]
    [Trait("PlanTest", "CT3")]
    public async Task Given_PausedSlowSubject_When_ItKeepsPolling_Then_AssignmentIsRetainedUntilCloseAndSlowOffsetIsProcessedOnce()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await using var broker = await LocalKafkaFixture.StartAsync(deadline.Token);
        await CreateTopicAsync(broker, 2, deadline.Token);
        var runId = NewRunId();
        var subject = StartMember(broker, runId, "subject", Plan("pause"), deadline.Token, observeClose: true);
        await WaitUntilAsync(() => OwnsAllPartitions(subject, 2, requireReadiness: false), deadline.Token);
        var witness = StartMember(broker, runId, "witness", Plan(pollLimit: true), deadline.Token);
        await WaitForAssignmentsAsync(subject, witness, 2, deadline.Token);
        await ProduceAsync(broker, 2, 10, 0, deadline.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(7000), deadline.Token);
        await ProduceAsync(broker, 2, 1, 10, deadline.Token);
        await AssertCompletedAsync(subject, witness, deadline.Token);

        var slow = Assert.Single(subject.Store.Rows, row => IsProcessed(row) && row.GetProperty("slow").GetBoolean());
        var assignment = NewestAssignmentBefore(subject.Store.Rows, slow);
        var partition = Assert.Single(Partitions(assignment));
        Assert.Equal(partition, slow.GetProperty("partition").GetInt32());
        Assert.Equal(10, slow.GetProperty("offset").GetInt64());
        Assert.NotNull(subject.Store.FirstCloseRowSequence);
        var firstClose = Assert.Single(subject.Store.Rows, row => Sequence(row) == subject.Store.FirstCloseRowSequence);
        Assert.DoesNotContain(subject.Store.Rows, row => Kind(row) == "partition_event" &&
            row.GetProperty("event").GetString() is "revoked" or "lost" &&
            Sequence(row) > Sequence(assignment) && Sequence(row) < Sequence(firstClose));
        Assert.Single(subject.Store.Rows.Concat(witness.Store.Rows), row => IsMessage(row, partition, 10));
        var started = Utc(slow, "processing_started_utc");
        var closing = Utc(firstClose, "event_utc");
        Assert.DoesNotContain(witness.Store.Rows, row => IsEvent(row, "assigned") && Partitions(row).Contains(partition) &&
            Utc(row, "event_utc") >= started && Utc(row, "event_utc") < closing);
    }

    [Fact]
    [Trait("Tier", "Docker")]
    [Trait("PlanTest", "CT4")]
    public async Task Given_SubjectOwnsBothPartitions_When_WitnessJoins_Then_RevokeIsFollowedByAutomaticCommitOfBothFrontiers()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await using var broker = await LocalKafkaFixture.StartAsync(deadline.Token);
        await CreateTopicAsync(broker, 2, deadline.Token);
        var runId = NewRunId();
        var subject = StartMember(broker, runId, "subject", Plan(), deadline.Token);
        await WaitUntilAsync(() => OwnsAllPartitions(subject, 2), deadline.Token);
        await ProduceAsync(broker, 2, 5, 0, deadline.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(1000), deadline.Token);
        var witness = StartMember(broker, runId, "witness", Plan(), deadline.Token);
        await WaitForAssignmentsAsync(subject, witness, 2, deadline.Token);
        await AssertCompletedAsync(subject, witness, deadline.Token);

        var revoked = subject.Store.Rows.First(row => IsEvent(row, "revoked"));
        Assert.Equal([0, 1], Partitions(revoked));
        Assert.Contains(subject.Store.Rows, row => Kind(row) == "commit" && Sequence(row) > Sequence(revoked) &&
            row.GetProperty("source").GetString() == "callback" &&
            row.GetProperty("error_code").GetInt32() == 0 &&
            Partitions(revoked).All(partition => row.GetProperty("offsets").EnumerateArray().Any(entry =>
                entry.GetProperty("topic").GetString() == Topic && entry.GetProperty("partition").GetInt32() == partition &&
                entry.GetProperty("error_code").GetInt32() == 0 && entry.GetProperty("offset").GetInt64() == 5)));
    }

    private static ConsumerProcessingPlan Plan(string? mode = null, bool pollLimit = false) => new()
    {
        ConfigurationId = "C0",
        ProcessingDelayMs = 0,
        WindowMs = 60000,
        SlowMessage = mode is null ? null : new ConsumerSlowMessage(11, 20000, mode),
        ConsumerSettings = mode is not null || pollLimit
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["session.timeout.ms"] = "6000",
                ["max.poll.interval.ms"] = "10000"
            }
            : new Dictionary<string, string>(StringComparer.Ordinal)
    };

    private static Member StartMember(LocalKafkaFixture broker, string runId, string role,
        ConsumerProcessingPlan plan, CancellationToken cancellationToken, bool observeClose = false)
    {
        var options = new ConsumerProcessingOptions
        {
            RunId = runId, ScenarioId = "C03", Phase = "C0", RepeatNumber = 1, Role = role,
            JobName = $"{runId}-c0-r1-consumer-{role}", PodName = $"{runId}-c0-r1-consumer-{role}-pod",
            PodUid = Guid.NewGuid().ToString("D"), NodeName = "component-node",
            SourceCommit = new string('a', 40), NamespaceName = "component", BlobEndpoint = "https://component.invalid"
        };
        var store = new CapturingRecordStore();
        var readiness = new AssignmentReadiness();
        var errors = new StringWriter(CultureInfo.InvariantCulture);
        var worker = new ConsumerProcessingWorker(Options.Create(options), new FixedPlanSource(plan),
            () => Baseline(options, broker.BootstrapServers), () => store,
            (configuration, handlers) =>
            {
                var consumer = new ConsumerBuilder<byte[], byte[]>(configuration)
                    .SetPartitionsAssignedHandler((client, partitions) =>
                    {
                        readiness.Assign(partitions);
                        handlers.Assigned(client, partitions);
                    })
                    .SetPartitionsRevokedHandler((client, partitions) =>
                    {
                        readiness.Clear();
                        handlers.Revoked(client, partitions.Select(value => value.TopicPartition).ToList());
                    })
                    .SetPartitionsLostHandler((client, partitions) =>
                    {
                        readiness.Clear();
                        handlers.Lost(client, partitions.Select(value => value.TopicPartition).ToList());
                    })
                    .SetStatisticsHandler((_, statistics) => readiness.Observe(statistics))
                    .SetOffsetsCommittedHandler(handlers.OffsetsCommitted).Build();
                if (!observeClose) return consumer;
                var proxy = DispatchProxy.Create<IConsumer<byte[], byte[]>, CloseObservingConsumer>();
                ((CloseObservingConsumer)(object)proxy).Initialize(consumer, store.BeginClose);
                return proxy;
            }, TimeProvider.System, code => throw new InvalidOperationException($"Unexpected watchdog exit {code}"), errors);
        var execution = Task.Run(() => worker.ExecuteAsync(cancellationToken), cancellationToken);
        return new Member(options, store, readiness, execution, errors);
    }

    private static ConsumerConfig Baseline(ConsumerProcessingOptions options, string bootstrapServers)
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.EnrichmentConsumer, settings =>
        {
            settings.NamespaceName = options.NamespaceName;
            settings.ClientId = options.PodName;
            settings.EnrichmentGroupId = options.GroupId;
        });
        using var provider = services.BuildServiceProvider();
        var baseline = provider.GetRequiredService<KafkaConfigurationFactory>().CreateEnrichmentConsumer().Configuration;
        var local = new ConsumerConfig(baseline.Where(setting => !setting.Key.StartsWith("sasl.", StringComparison.Ordinal)
            && !setting.Key.StartsWith("ssl.", StringComparison.Ordinal)
            && setting.Key != "enable.sasl.oauthbearer.unsecure.jwt")
            .ToDictionary(setting => setting.Key, setting => setting.Value, StringComparer.Ordinal));
        local.BootstrapServers = bootstrapServers;
        local.SecurityProtocol = SecurityProtocol.Plaintext;
        return local;
    }

    private static async Task CreateTopicAsync(LocalKafkaFixture broker, int partitions, CancellationToken cancellationToken)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = broker.BootstrapServers }).Build();
        await admin.CreateTopicsAsync([new TopicSpecification { Name = Topic, NumPartitions = partitions, ReplicationFactor = 1 }],
            new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(20), OperationTimeout = TimeSpan.FromSeconds(20) })
            .WaitAsync(cancellationToken);
    }

    private static async Task<List<(int Partition, long Offset)>> ProduceAsync(LocalKafkaFixture broker, int partitions,
        int count, int firstSequence, CancellationToken cancellationToken)
    {
        using var producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig
        {
            BootstrapServers = broker.BootstrapServers, Acks = Acks.All, MessageTimeoutMs = 10000
        }).Build();
        var produced = new List<(int Partition, long Offset)>();
        for (int partition = 0; partition < partitions; partition++)
        {
            for (int index = 0; index < count; index++)
            {
                var sequence = firstSequence + index;
                var message = new Message<byte[], byte[]>
                {
                    Key = Encoding.UTF8.GetBytes($"synthetic-{partition}"), Value = "synthetic"u8.ToArray(),
                    Headers =
                    [
                        new Header("instance-id", "synthetic-producer"u8.ToArray()),
                        new Header("attempt-id", Encoding.UTF8.GetBytes($"synthetic-{partition}-{sequence}")),
                        new Header("sequence", Encoding.UTF8.GetBytes(sequence.ToString(CultureInfo.InvariantCulture)))
                    ]
                };
                var delivery = await producer.ProduceAsync(new TopicPartition(Topic, new Partition(partition)), message, cancellationToken);
                produced.Add((delivery.Partition.Value, delivery.Offset.Value));
            }
        }
        return produced;
    }

    private static async Task WaitForAssignmentsAsync(Member subject, Member witness, int partitions, CancellationToken cancellationToken)
    {
        await WaitUntilAsync(() =>
        {
            ThrowIfExited(subject);
            ThrowIfExited(witness);
            var subjectRow = LatestPartitionEvent(subject.Store.Rows);
            var witnessRow = LatestPartitionEvent(witness.Store.Rows);
            if (subjectRow is not { } first || witnessRow is not { } second || !IsEvent(first, "assigned") || !IsEvent(second, "assigned"))
                return false;
            var left = Partitions(first);
            var right = Partitions(second);
            return left.Length > 0 && right.Length > 0 && !left.Intersect(right).Any() &&
                left.Concat(right).Order().SequenceEqual(Enumerable.Range(0, partitions)) &&
                subject.Readiness.IsReady(left) && witness.Readiness.IsReady(right);
        }, cancellationToken);
    }

    private static bool OwnsAllPartitions(Member member, int partitions, bool requireReadiness = true)
    {
        ThrowIfExited(member);
        return LatestPartitionEvent(member.Store.Rows) is { } row && IsEvent(row, "assigned") &&
            Partitions(row).SequenceEqual(Enumerable.Range(0, partitions)) &&
            (!requireReadiness || member.Readiness.IsReady(Partitions(row)));
    }

    private static void ThrowIfExited(Member member)
    {
        if (member.Execution.IsCompleted)
            throw new InvalidOperationException("Member exited before the start condition: " + member.Errors);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition()) await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
    }

    private static async Task AssertCompletedAsync(Member subject, Member witness, CancellationToken cancellationToken)
    {
        var exits = await Task.WhenAll(subject.Execution, witness.Execution).WaitAsync(cancellationToken);
        Assert.True(exits.All(exit => exit == 0), subject.Errors.ToString() + witness.Errors.ToString());
        Assert.NotNull(subject.Store.Completion);
        Assert.NotNull(witness.Store.Completion);
    }

    private static string NewRunId() => "ct-" + Guid.NewGuid().ToString("N")[..12];
    private static string? Kind(JsonElement row) => row.GetProperty("kind").GetString();
    private static long Sequence(JsonElement row) => row.GetProperty("row_sequence").GetInt64();
    private static bool IsProcessed(JsonElement row) => Kind(row) == "processed";
    private static bool IsEvent(JsonElement row, string name) => Kind(row) == "partition_event" && row.GetProperty("event").GetString() == name;
    private static int[] Partitions(JsonElement row) => row.GetProperty("partitions").EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static (int Partition, long Offset) Coordinate(JsonElement row) => (row.GetProperty("partition").GetInt32(), row.GetProperty("offset").GetInt64());
    private static bool IsMessage(JsonElement row, int partition, long offset) => IsProcessed(row) && Coordinate(row) == (partition, offset);
    private static DateTimeOffset Utc(JsonElement row, string field) => row.GetProperty(field).GetDateTimeOffset();
    private static JsonElement? LatestPartitionEvent(IEnumerable<JsonElement> rows) => rows.Where(row => Kind(row) == "partition_event").Select(row => (JsonElement?)row).LastOrDefault();
    private static JsonElement NewestAssignmentBefore(IEnumerable<JsonElement> rows, JsonElement slow) =>
        rows.Last(row => IsEvent(row, "assigned") && Sequence(row) < Sequence(slow));

    private sealed record Member(ConsumerProcessingOptions Options, CapturingRecordStore Store,
        AssignmentReadiness Readiness, Task<int> Execution, StringWriter Errors);

    // Native statistics report fetch_state and next_offset after offset resolution:
    // https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md#partitions
    // https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_partition.c#L1395-L1444
    // Keep the baseline 15000ms statistics cadence and reject snapshots predating the assignment.
    private sealed class AssignmentReadiness
    {
        private readonly object gate = new();
        private readonly HashSet<int> assigned = [];
        private readonly HashSet<int> ready = [];
        private long assignedSecond;

        internal void Assign(IEnumerable<TopicPartition> partitions)
        {
            lock (gate)
            {
                assigned.Clear();
                assigned.UnionWith(partitions.Select(partition => partition.Partition.Value));
                ready.Clear();
                assignedSecond = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds();
            }
        }

        internal void Clear()
        {
            lock (gate)
            {
                assigned.Clear();
                ready.Clear();
            }
        }

        internal void Observe(string statistics)
        {
            using var document = JsonDocument.Parse(statistics);
            var snapshot = document.RootElement;
            lock (gate)
            {
                if (snapshot.GetProperty("time").GetInt64() <= assignedSecond ||
                    !snapshot.GetProperty("topics").TryGetProperty(Topic, out var topic)) return;
                ready.Clear();
                foreach (var partition in topic.GetProperty("partitions").EnumerateObject())
                {
                    var state = partition.Value;
                    var number = state.GetProperty("partition").GetInt32();
                    if (assigned.Contains(number) && state.GetProperty("fetch_state").GetString() == "active" &&
                        state.GetProperty("next_offset").GetInt64() >= 0)
                        ready.Add(number);
                }
            }
        }

        internal bool IsReady(IEnumerable<int> partitions)
        {
            lock (gate) return assigned.SetEquals(partitions) && ready.SetEquals(assigned);
        }
    }
    private sealed class FixedPlanSource(ConsumerProcessingPlan plan) : IConsumerProcessingPlanSource
    {
        public ConsumerProcessingPlan GetPlan(string scenarioId, string configurationId) => plan;
    }

    private sealed class CapturingRecordStore : IConsumerRecordStore
    {
        private readonly object gate = new();
        private readonly List<byte[]> rows = [];
        private bool closing;
        internal long? FirstCloseRowSequence { get; private set; }
        internal byte[]? Registration { get; private set; }
        internal byte[]? Completion { get; private set; }
        internal JsonElement[] Rows
        {
            get
            {
                lock (gate) return rows.Select(Parse).ToArray();
            }
        }
        internal void BeginClose()
        {
            lock (gate) closing = true;
        }
        public Task RegisterAsync(byte[] json, CancellationToken cancellationToken)
        {
            Registration = json.ToArray();
            return Task.CompletedTask;
        }
        public Task AppendAsync(byte[] jsonLine, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                var raw = jsonLine.ToArray();
                if (closing) FirstCloseRowSequence ??= Sequence(Parse(raw));
                rows.Add(raw);
            }
            return Task.CompletedTask;
        }
        public Task CompleteAsync(byte[] json, CancellationToken cancellationToken)
        {
            Completion = json.ToArray();
            return Task.CompletedTask;
        }
        private static JsonElement Parse(byte[] raw)
        {
            using var document = JsonDocument.Parse(raw);
            return document.RootElement.Clone();
        }
    }

    public class CloseObservingConsumer : DispatchProxy
    {
        private IConsumer<byte[], byte[]>? consumer;
        private Action? closeStarted;
        internal void Initialize(IConsumer<byte[], byte[]> client, Action onClose)
        {
            consumer = client;
            closeStarted = onClose;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IConsumer<byte[], byte[]>.Close)) closeStarted!();
            try { return targetMethod.Invoke(consumer, args); }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
#pragma warning restore CA1707
