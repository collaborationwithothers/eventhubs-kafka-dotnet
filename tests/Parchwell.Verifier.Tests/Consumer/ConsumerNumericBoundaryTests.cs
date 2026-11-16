using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Consumer;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707
public sealed class ConsumerNumericBoundaryTests
{
    public static TheoryData<string, string, int> RowCases
    {
        get
        {
            var cases = new TheoryData<string, string, int>();
            foreach (string kind in new[] { "processed", "kill_intent", "commit", "partition_event" })
                foreach (int value in new[] { -1, 0, 1 }) cases.Add(kind, "row_sequence", value);
            foreach (string kind in new[] { "processed", "kill_intent" })
                foreach (string propertyName in new[] { "partition", "offset" })
                    foreach (int value in new[] { -1, 0, 1 }) cases.Add(kind, propertyName, value);
            foreach (int value in new[] { -1, 0, 1 })
            {
                cases.Add("partition_event", "partitions", value);
                cases.Add("commit", "offsets.partition", value);
                cases.Add("commit", "submitted.partition", value);
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(RowCases))]
    public void Given_RowNumericBoundary_U4_When_CodecReads_Then_NonnegativeGuardIsIndependentOfReconciliation(string kind, string field, int value)
    {
        JsonObject row = kind switch
        {
            "processed" => ConsumerWireFixture.Processed("subject"),
            "partition_event" => ConsumerWireFixture.Assignment("subject", [0]),
            "commit" => ConsumerWireFixture.Commit("subject", 0),
            _ => new JsonObject
            {
                ["schema_version"] = 1, ["kind"] = "kill_intent", ["instance_id"] = "subject", ["row_sequence"] = 0,
                ["partition"] = 0, ["offset"] = 0, ["event_utc"] = ConsumerWireFixture.Stamp
            }
        };
        if (field == "partitions") row[field] = new JsonArray(value);
        else if (field == "offsets.partition") row["offsets"]![0]!["partition"] = value;
        else if (field == "submitted.partition")
        {
            row["source"] = "explicit";
            row["outcome"] = "request_exception";
            row["error_code"] = 1;
            row["error_reason"] = "synthetic";
            row["submitted"] = new JsonArray(ConsumerWireFixture.Offset(0, partition: value));
            row["offsets"] = new JsonArray();
        }
        else row[field] = value;
        byte[] bytes = Encoding.UTF8.GetBytes(row.ToJsonString());
        if (value < 0) Assert.Throws<FormatException>(() => ConsumerArtifactCodec.ReadRow(bytes));
        else
        {
            ConsumerRow parsed = ConsumerArtifactCodec.ReadRow(bytes);
            long actual = field switch
            {
                "row_sequence" => parsed.RowSequence,
                "offset" => parsed.Offset,
                "partition" => parsed.Partition,
                "partitions" => Assert.Single(parsed.Partitions!),
                "offsets.partition" => Assert.Single(parsed.Offsets!).Partition,
                _ => Assert.Single(parsed.Submitted!).Partition
            };
            Assert.Equal(value, actual);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Given_SchemaVersionBoundary_U4_When_CodecReads_Then_OnlyVersionOneIsAccepted(int version, bool valid)
    {
        JsonObject row = ConsumerWireFixture.Assignment("subject", [0]);
        row["schema_version"] = version;
        byte[] bytes = Encoding.UTF8.GetBytes(row.ToJsonString());
        if (valid) Assert.Equal("partition_event", ConsumerArtifactCodec.ReadRow(bytes).Kind);
        else Assert.Throws<FormatException>(() => ConsumerArtifactCodec.ReadRow(bytes));
    }

    [Theory]
    [InlineData("2026-11-15T00:00:00.000000Z", false)]
    [InlineData("2026-11-15T00:00:00.0000000Z", true)]
    [InlineData("2026-11-15T00:00:00.00000000Z", false)]
    [InlineData("2026-02-30T00:00:00.0000000Z", false)]
    public void Given_UtcFormatBoundary_U4_When_CodecReads_Then_ExactWidthAndCalendarDateAreBothRequired(string timestamp, bool valid)
    {
        JsonObject row = ConsumerWireFixture.Assignment("subject", [0]);
        row["event_utc"] = timestamp;
        byte[] bytes = Encoding.UTF8.GetBytes(row.ToJsonString());
        if (valid) Assert.Equal(timestamp, ConsumerArtifactCodec.ReadRow(bytes).Time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture));
        else Assert.Throws<FormatException>(() => ConsumerArtifactCodec.ReadRow(bytes));
    }

    [Theory]
    [InlineData("partition", "\"0\"", false)]
    [InlineData("partition", "2147483646", true)]
    [InlineData("partition", "2147483647", true)]
    [InlineData("partition", "2147483648", false)]
    [InlineData("offset", "\"0\"", false)]
    [InlineData("offset", "9223372036854775806", true)]
    [InlineData("offset", "9223372036854775807", true)]
    [InlineData("offset", "9223372036854775808", false)]
    public void Given_JsonIntegerTypeAndRange_U4_When_CodecReads_Then_ExactWireIntegerTypesAreRequired(string propertyName, string literal, bool valid)
    {
        JsonObject row = ConsumerWireFixture.Processed("subject");
        row[propertyName] = JsonNode.Parse(literal);
        byte[] bytes = Encoding.UTF8.GetBytes(row.ToJsonString());
        if (valid)
        {
            ConsumerRow parsed = ConsumerArtifactCodec.ReadRow(bytes);
            Assert.Equal(long.Parse(literal, System.Globalization.CultureInfo.InvariantCulture), propertyName == "partition" ? parsed.Partition : parsed.Offset);
        }
        else Assert.Throws<FormatException>(() => ConsumerArtifactCodec.ReadRow(bytes));
    }

    [Theory]
    [InlineData("rows", -1)]
    [InlineData("rows", 0)]
    [InlineData("rows", 1)]
    [InlineData("record_blobs", -1)]
    [InlineData("record_blobs", 0)]
    [InlineData("record_blobs", 1)]
    [InlineData("messages_processed", -1)]
    [InlineData("messages_processed", 0)]
    [InlineData("messages_processed", 1)]
    public void Given_CompletionNumericBoundary_U4_When_CodecReads_Then_CountGuardCannotBeMaskedByReconciliation(string field, int value)
    {
        JsonObject completion = ConsumerWireFixture.Completion("subject", 0, 0, 0);
        completion[field] = value;
        byte[] bytes = Encoding.UTF8.GetBytes(completion.ToJsonString());
        if (value < 0) Assert.Throws<FormatException>(() => ConsumerArtifactCodec.ReadCompletion(bytes));
        else
        {
            ConsumerCompletion parsed = ConsumerArtifactCodec.ReadCompletion(bytes);
            long actual = field switch { "rows" => parsed.Rows, "record_blobs" => parsed.RecordBlobs, _ => parsed.MessagesProcessed };
            Assert.Equal(value, actual);
        }
    }
}
#pragma warning restore CA1707
