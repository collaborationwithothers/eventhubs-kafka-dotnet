#pragma warning disable CA1707
using System.Globalization;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Parchwell.Enrichment.ConsumerProcessing;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

public sealed class ConsumerRecordCodecTests
{
    internal static readonly string[] RegistrationFields = ["schema_version", "kind", "run_id", "scenario_id", "configuration_id", "phase", "repeat_number", "role", "instance_id", "job_name", "pod_name", "pod_uid", "node_name", "source_commit", "group_id", "effective_settings", "registered_utc"];
    private static readonly string[] CompletionFields = ["schema_version", "kind", "run_id", "scenario_id", "configuration_id", "phase", "repeat_number", "role", "instance_id", "job_name", "pod_uid", "source_commit", "rows", "record_blobs", "messages_processed", "closed_utc"];
    private static readonly string[] CommonFields = ["schema_version", "kind", "instance_id", "row_sequence"];
    private static readonly string[] OffsetFields = ["error_code", "offset", "partition", "topic"];
    private static readonly int[] SortedPartitions = [1, 3];
    private const string TimestampPattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$";

    [Fact]
    public void Given_U23_each_row_kind_When_serialized_Then_exact_format_and_utc_times_are_written()
    {
        DateTimeOffset time = DateTimeOffset.Parse("2026-11-15T03:00:00.1234567+03:00", CultureInfo.InvariantCulture);
        (byte[] Bytes, string Kind, string[] Fields)[] rows =
        [
            (ConsumerRecordCodec.Processed("synthetic-pod", 0, ConsumerTestHarness.Message(12), true, time, time.AddSeconds(1)), "processed", ["topic", "partition", "offset", "producer_instance_id", "attempt_id", "key", "sequence", "slow", "processing_started_utc", "processing_ended_utc"]),
            (ConsumerRecordCodec.Commit("synthetic-pod", 1, ConsumerProcessingWorkerTests.CommitResult(), time), "commit", ["source", "outcome", "error_code", "error_reason", "submitted", "offsets", "event_utc"]),
            (ConsumerRecordCodec.PartitionEvent("synthetic-pod", 2, "assigned", [3, 1, 3], time), "partition_event", ["event", "partitions", "event_utc"]),
            (ConsumerRecordCodec.KillIntent("synthetic-pod", 3, 1, 12, time), "kill_intent", ["partition", "offset", "event_utc"])
        ];
        for (int index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            Assert.Equal((byte)'\n', row.Bytes[^1]);
            Assert.Equal(1, row.Bytes.Count(value => value == (byte)'\n'));
            Assert.True(row.Bytes.Length <= 65536);
            using JsonDocument document = JsonDocument.Parse(row.Bytes);
            JsonElement root = document.RootElement;
            Assert.Equal(CommonFields.Concat(row.Fields).Order(StringComparer.Ordinal), root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
            Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
            Assert.Equal(row.Kind, root.GetProperty("kind").GetString());
            Assert.Equal(index, root.GetProperty("row_sequence").GetInt64());
            Assert.Equal("synthetic-pod", root.GetProperty("instance_id").GetString());
            foreach (JsonProperty property in root.EnumerateObject().Where(property => property.Name.EndsWith("_utc", StringComparison.Ordinal)))
            {
                Assert.Matches(TimestampPattern, property.Value.GetString()!);
                Assert.StartsWith("2026-11-15T00:00:", property.Value.GetString());
            }
            if (row.Kind == "commit")
            {
                Assert.Equal("callback", root.GetProperty("source").GetString());
                Assert.Equal("callback", root.GetProperty("outcome").GetString());
                Assert.Equal(JsonValueKind.Null, root.GetProperty("submitted").ValueKind);
                Assert.Equal(JsonValueKind.Null, root.GetProperty("error_reason").ValueKind);
                foreach (JsonElement entry in root.GetProperty("offsets").EnumerateArray())
                    Assert.Equal(OffsetFields, entry.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
            }
            if (row.Kind == "partition_event")
                Assert.Equal(SortedPartitions, root.GetProperty("partitions").EnumerateArray().Select(value => value.GetInt32()));
        }
    }

    [Fact]
    public async Task Given_U23_normal_worker_When_completed_Then_exact_completion_format_has_no_kill_intent()
    {
        using var harness = new ConsumerTestHarness();
        Assert.Equal(0, await harness.RunAsync());
        JsonElement receipt = harness.Store.Completion!.Value;
        Assert.Equal(CompletionFields.Order(StringComparer.Ordinal), receipt.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Matches(TimestampPattern, receipt.GetProperty("closed_utc").GetString()!);
        Assert.Matches(TimestampPattern, harness.Store.Registration!.Value.GetProperty("registered_utc").GetString()!);
        Assert.DoesNotContain(harness.Store.Rows, row => row.GetProperty("kind").GetString() == "kill_intent");
    }

    [Theory]
    [InlineData("instance-id")]
    [InlineData("attempt-id")]
    [InlineData("sequence")]
    public void Given_U22_duplicate_or_invalid_utf8_header_When_serialized_Then_field_is_null(string name)
    {
        foreach (bool duplicate in new[] { false, true })
        {
            ConsumeResult<byte[], byte[]> message = ConsumerTestHarness.Message(0);
            if (!duplicate) message.Message.Headers.Remove(name);
            message.Message.Headers.Add(name, [0xff]);
            byte[] line = ConsumerRecordCodec.Processed("synthetic-pod", 0, message, false, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
            using JsonDocument document = JsonDocument.Parse(line);
            string property = name == "instance-id" ? "producer_instance_id" : name.Replace('-', '_');
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty(property).ValueKind);
        }
    }

    [Theory]
    [InlineData("PARCHWELL_SOURCE_COMMIT")]
    [InlineData("PARCHWELL_JOB_NAME")]
    [InlineData("PARCHWELL_POD_NAME")]
    [InlineData("PARCHWELL_POD_UID")]
    [InlineData("PARCHWELL_NODE_NAME")]
    public void Given_U1_deployment_identity_missing_or_mismatched_When_validated_Then_identity_fails(string key)
    {
        ConsumerProcessingOptions options = ConsumerTestHarness.ValidOptions();
        Dictionary<string, string?> inputs = DeploymentIdentity(options);
        options.ValidateDeploymentIdentity(new ConfigurationBuilder().AddInMemoryCollection(inputs).Build());
        inputs.Remove(key);
        Assert.Equal("consumer_identity_invalid", Assert.Throws<InvalidOperationException>(() => options.ValidateDeploymentIdentity(new ConfigurationBuilder().AddInMemoryCollection(inputs).Build())).Message);
        inputs[key] = "different";
        Assert.Equal("consumer_identity_invalid", Assert.Throws<InvalidOperationException>(() => options.ValidateDeploymentIdentity(new ConfigurationBuilder().AddInMemoryCollection(inputs).Build())).Message);
    }

    private static Dictionary<string, string?> DeploymentIdentity(ConsumerProcessingOptions options) => new(StringComparer.Ordinal)
    {
        ["PARCHWELL_SOURCE_COMMIT"] = options.SourceCommit,
        ["PARCHWELL_JOB_NAME"] = options.JobName,
        ["PARCHWELL_POD_NAME"] = options.PodName,
        ["PARCHWELL_POD_UID"] = options.PodUid,
        ["PARCHWELL_NODE_NAME"] = options.NodeName
    };
}
#pragma warning restore CA1707
