using System.Globalization;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;

namespace Parchwell.Enrichment.ConsumerProcessing;

internal static class ConsumerRecordCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static byte[] Registration(ConsumerProcessingOptions options, ConsumerProcessingPlan plan,
        ConsumerConfig configuration, DateTimeOffset registered) => Json(new
        {
            schema_version = 1, kind = "consumer_registration", run_id = options.RunId,
            scenario_id = options.ScenarioId, configuration_id = plan.ConfigurationId, phase = options.Phase,
            repeat_number = options.RepeatNumber, role = options.Role, instance_id = options.PodName,
            job_name = options.JobName, pod_name = options.PodName, pod_uid = options.PodUid, node_name = options.NodeName,
            source_commit = options.SourceCommit, group_id = options.GroupId,
            effective_settings = new SortedDictionary<string, string>(configuration.ToDictionary(
                pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), StringComparer.Ordinal),
            registered_utc = Time(registered)
        });

    public static byte[] Completion(ConsumerProcessingOptions options, ConsumerProcessingPlan plan,
        long rows, long messagesProcessed, DateTimeOffset closed) => Json(new
        {
            schema_version = 1, kind = "consumer_completion", run_id = options.RunId,
            scenario_id = options.ScenarioId, configuration_id = plan.ConfigurationId, phase = options.Phase,
            repeat_number = options.RepeatNumber, role = options.Role, instance_id = options.PodName,
            job_name = options.JobName, pod_uid = options.PodUid, source_commit = options.SourceCommit,
            rows, record_blobs = (rows + 39999) / 40000, messages_processed = messagesProcessed, closed_utc = Time(closed)
        });

    public static byte[] Processed(string instanceId, long rowSequence, ConsumeResult<byte[], byte[]> message,
        bool slow, DateTimeOffset started, DateTimeOffset ended)
    {
        string? sequence = Header(message.Message.Headers, "sequence");
        long? number = long.TryParse(sequence, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value) ? value : null;
        return Line(new
        {
            schema_version = 1, kind = "processed", instance_id = instanceId, row_sequence = rowSequence,
            topic = message.Topic, partition = message.Partition.Value, offset = message.Offset.Value,
            producer_instance_id = Header(message.Message.Headers, "instance-id"),
            attempt_id = Header(message.Message.Headers, "attempt-id"), key = Decode(message.Message.Key),
            sequence = number, slow, processing_started_utc = Time(started), processing_ended_utc = Time(ended)
        });
    }

    public static byte[] PartitionEvent(string instanceId, long rowSequence, string eventName,
        IEnumerable<int> partitions, DateTimeOffset time) => Line(new
        {
            schema_version = 1, kind = "partition_event", instance_id = instanceId, row_sequence = rowSequence,
            @event = eventName, partitions = partitions.Distinct().Order().ToArray(), event_utc = Time(time)
        });

    public static byte[] Commit(string instanceId, long rowSequence, CommittedOffsets committed, DateTimeOffset time) => Line(new
    {
        schema_version = 1, kind = "commit", instance_id = instanceId, row_sequence = rowSequence,
        source = "callback", outcome = "callback", error_code = (int)committed.Error.Code,
        error_reason = committed.Error.Code == ErrorCode.NoError ? null : committed.Error.Reason,
        submitted = (object?)null,
        offsets = committed.Offsets.OrderBy(entry => entry.Topic, StringComparer.Ordinal).ThenBy(entry => entry.Partition.Value)
            .Select(entry => new { topic = entry.Topic, partition = entry.Partition.Value, offset = entry.Offset.Value, error_code = (int)entry.Error.Code }).ToArray(),
        event_utc = Time(time)
    });

    public static byte[] KillIntent(string instanceId, long rowSequence, int partition, long offset, DateTimeOffset time) => Line(new
    {
        schema_version = 1, kind = "kill_intent", instance_id = instanceId, row_sequence = rowSequence,
        partition, offset, event_utc = Time(time)
    });

    private static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    private static byte[] Json<T>(T value)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value);
        if (json.Length > 65536) throw new InvalidOperationException("consumer_record_append_failed");
        return json;
    }

    private static byte[] Line<T>(T value)
    {
        byte[] json = Json(value);
        if (json.Length >= 65536) throw new InvalidOperationException("consumer_record_append_failed");
        byte[] line = new byte[json.Length + 1];
        json.CopyTo(line, 0);
        line[^1] = (byte)'\n';
        return line;
    }

    private static string? Header(Headers? headers, string name)
    {
        if (headers is null) return null;
        IHeader[] matching = headers.Where(header => StringComparer.Ordinal.Equals(header.Key, name)).ToArray();
        return matching.Length == 1 ? Decode(matching[0].GetValueBytes()) : null;
    }

    private static string? Decode(byte[]? bytes)
    {
        if (bytes is null) return null;
        try { return StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException) { return null; }
    }
}
