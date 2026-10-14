using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Parchwell.Kafka.Tests.PhantomSpike.Experiment;

// This is a test fixture using the real pinned client, not a production client seam.
// APIs: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/IConsumer.cs
// Admin timeout options: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Admin/CreateTopicsOptions.cs
internal sealed class DirectObserver : IDisposable
{
    private static readonly TimeSpan QuerySlice = TimeSpan.FromMilliseconds(250);
    private readonly IConsumer<byte[], byte[]> consumer;
    private readonly TopicPartition partition;
    private readonly TimeProvider clock;

    public DirectObserver(string bootstrapServers, string topic, TimeProvider clock)
    {
        this.clock = clock;
        partition = new TopicPartition(topic, new Partition(0));
        consumer = new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers, // A01: independent local diagnostic path.
            GroupId = "phantom-spike-direct-observer", // C13: reuse one diagnostic group.
            EnableAutoCommit = false, // C10: explicit bounded assignments, no stored progress.
            EnableAutoOffsetStore = false, // C10: no automatic offset bookkeeping.
            AutoOffsetReset = AutoOffsetReset.Error, // C15: never silently skip an unavailable start.
            IsolationLevel = IsolationLevel.ReadCommitted, // B05: explicit observation isolation.
            EnablePartitionEof = true, // C05/C15: EOF remains diagnostic, not scan completion.
            AllowAutoCreateTopics = false // D01: topics are explicitly created by the fixture.
        }).Build();
    }

    public static async Task CreateTopicAsync(string bootstrapServers, string topic,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = bootstrapServers // D01: use the direct local administration path.
        }).Build();
        await admin.CreateTopicsAsync(
            [new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }],
            new CreateTopicsOptions
            {
                // Planned operational ceiling: the caller's trial token caps the remaining budget.
                RequestTimeout = TimeSpan.FromSeconds(60),
                OperationTimeout = TimeSpan.FromSeconds(60)
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public long GetHighWatermark(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var watermarks = consumer.QueryWatermarkOffsets(partition, QuerySlice);
                cancellationToken.ThrowIfCancellationRequested();
                return watermarks.High.Value;
            }
            catch (KafkaException exception) when (exception.Error.Code is
                ErrorCode.Local_TimedOut or ErrorCode.RequestTimedOut)
            {
                // A query's bounded timeout does not imply that the observation window is complete.
            }
        }
    }

    public Task<ObservationScan> ScanAsync(long start, AttemptMessage target,
        CancellationToken cancellationToken) => Task.Run(() => Scan(start, target, cancellationToken));

    private ObservationScan Scan(long start, AttemptMessage target, CancellationToken cancellationToken)
    {
        var records = new List<ConsumedObservation>();
        long end = -1;
        long position = start;
        try
        {
            if (!string.Equals(target.Topic, partition.Topic, StringComparison.Ordinal))
            {
                return new ObservationScan(start, end, position, false, records,
                    "Observer topic and attempt topic differ.");
            }

            if (start < 0)
            {
                return new ObservationScan(start, end, position, false, records,
                    "The start offset must be explicit and non-negative.");
            }

            end = GetHighWatermark(cancellationToken);
            if (end < start)
            {
                return new ObservationScan(start, end, position, false, records,
                    "The end watermark precedes the start offset.");
            }

            consumer.Assign(new TopicPartitionOffset(partition, new Offset(start)));
            while (position < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var consumed = consumer.Consume(QuerySlice);
                if (consumed is not null && !consumed.IsPartitionEOF)
                {
                    // Preserve every record in the fixed window, including nonmatching identities.
                    if (consumed.Offset.Value >= start && consumed.Offset.Value < end)
                    {
                        var headers = consumed.Message.Headers?.Select(header =>
                            new HeaderObservation(header.Key, header.GetValueBytes()?.ToArray())).ToArray()
                            ?? [];
                        records.Add(new ConsumedObservation(clock.GetUtcNow(), consumed.Topic,
                            consumed.Partition.Value, consumed.Offset.Value,
                            ReadIdentity(headers, "instance-id"), ReadIdentity(headers, "attempt-id"),
                            consumed.Message.Key, consumed.Message.Value, headers));
                    }
                }

                var current = consumer.Position(partition);
                if (current != Offset.Unset)
                {
                    position = current.Value;
                }
                // Empty polls and EOF events never substitute for reaching the captured end.
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new ObservationScan(start, end, position, true, records, null);
        }
        catch (OperationCanceledException exception)
        {
            return new ObservationScan(start, end, position, false, records, exception.Message);
        }
        catch (KafkaException exception)
        {
            return new ObservationScan(start, end, position, false, records,
                $"{exception.Error.Code}: {exception.Error.Reason}");
        }
    }

    private static string? ReadIdentity(IReadOnlyList<HeaderObservation> headers, string key)
    {
        var matching = headers.Where(header => string.Equals(header.Key, key, StringComparison.Ordinal))
            .ToArray();
        return matching.Length == 1 && matching[0].Value is { } value
            ? Encoding.UTF8.GetString(value)
            : null;
    }

    public void Dispose() => consumer.Dispose();
}
