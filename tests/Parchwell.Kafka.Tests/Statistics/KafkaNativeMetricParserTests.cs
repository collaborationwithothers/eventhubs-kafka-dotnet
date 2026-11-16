using System.Text.Json.Nodes;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707

[Trait("Category", "Unit")]
public sealed class KafkaNativeMetricParserTests
{
    private const string Identity = "\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":1,\"time\":1,\"age\":1";

    [Fact]
    public void Given_AllScalarKinds_When_Projected_Then_LiteralCatalogueTargetsMatch()
    {
        foreach (var row in NativeMetricContract.Rows)
        {
            var root = JsonNode.Parse("{" + Identity + "}")!.AsObject();
            string path = row.Path.Replace("brokers.*", "brokers.broker", StringComparison.Ordinal)
                .Replace("topics.*", "topics.topic", StringComparison.Ordinal)
                .Replace("partitions.*", "partitions.-1", StringComparison.Ordinal)
                .Replace("req.*", "req.Produce", StringComparison.Ordinal);
            string[] segments = path.Split('.');
            JsonObject parent = root;
            foreach (string segment in segments[..^1])
            {
                parent[segment] ??= new JsonObject();
                parent = parent[segment]!.AsObject();
            }
            if (row.Policy == "W+") parent["cnt"] = 1;
            parent[segments[^1]] = row.Policy switch
            {
                "B" => JsonValue.Create(true),
                "S" => JsonValue.Create("not-a-native-state"),
                _ => JsonValue.Create(2L),
            };
            var projection = KafkaNativeMetricParser.Parse(root.ToJsonString());
            Assert.True(projection.IsValid, row.Path);
            var matching = projection.Points.Where(point => point.Definition.Name == row.Name).ToArray();
            Assert.NotEmpty(matching);
            Assert.All(matching, point =>
            {
                Assert.Equal(row.Unit, point.Definition.Unit);
                Assert.Equal(row.Kind, point.Definition.Kind.StartsWith("Counter", StringComparison.Ordinal) ? "Counter`1" : "Gauge`1");
                Assert.Equal(row.Path.StartsWith("brokers.", StringComparison.Ordinal) ? "broker" : null, point.Broker);
                Assert.Equal(row.Path.StartsWith("topics.*", StringComparison.Ordinal) ? "topic" : null, point.Topic);
                Assert.Equal(row.Path.StartsWith("topics.*.partitions.*", StringComparison.Ordinal) ? -1 : null, point.Partition);
                if (row.Policy != "S") Assert.Equal(row.Policy == "B" ? 1 : 2, point.Value);
            });
        }
    }

    [Fact]
    public void Given_EmptyWindow_When_Projected_Then_OnlyAvailableMetadataEmits()
    {
        var empty = Parse("\"brokers\":{\"b\":{\"rtt\":{\"cnt\":0,\"min\":999,\"max\":999,\"sum\":999,\"p99\":999,\"outofrange\":0,\"hdrsize\":128}}}");
        Assert.True(empty.IsValid);
        var points = empty.Points.Where(p => p.Definition.Name.StartsWith("kafka.librdkafka.broker.rtt.", StringComparison.Ordinal)).ToDictionary(p => p.Definition.Name, p => p.Value);
        Assert.Equal(new[] { "cnt", "hdrsize", "outofrange" }, points.Keys.Select(k => k.Split('.')[^1]).Order());
        Assert.Equal(128, points["kafka.librdkafka.broker.rtt.hdrsize"]);
        var filled = Parse("\"brokers\":{\"b\":{\"rtt\":{\"cnt\":2,\"min\":0,\"max\":12,\"avg\":6,\"sum\":12,\"p99\":12}}}");
        Assert.True(filled.IsValid);
        foreach (var (name, value) in new[] { ("min", 0), ("max", 12), ("avg", 6), ("sum", 12), ("p99", 12) })
            Assert.Equal(value, Assert.Single(filled.Points, p => p.Definition.Name == "kafka.librdkafka.broker.rtt." + name).Value);
    }

    [Fact]
    public void Given_AliasesAndStates_When_Projected_Then_ValuesAndDiagnosticsAreBounded()
    {
        var equal = Parse("\"topics\":{\"t\":{\"partitions\":{\"-1\":{\"committed_offset\":42,\"commited_offset\":42}}}},\"brokers\":{\"b\":{\"state\":\"UP\"}}");
        Assert.True(equal.IsValid);
        var offset = Assert.Single(equal.Points, p => p.Definition.Name == "kafka.librdkafka.topic.partition.committed_offset");
        Assert.Equal(42, offset.Value);
        Assert.Equal(-1, offset.Partition);
        var states = equal.Points.Where(p => p.Definition.Name == "kafka.librdkafka.broker.state").ToArray();
        Assert.Equal(11, states.Length);
        Assert.Equal(1, Assert.Single(states, p => p.State == "UP").Value);
        Assert.All(states.Where(p => p.State != "UP"), p => Assert.Equal(0, p.Value));
        var unknown = Parse("\"brokers\":{\"b\":{\"state\":\"NEXT_VERSION\",\"req\":{\"ExplodeMetricCardinality\":1}}}");
        Assert.True(unknown.IsValid);
        Assert.All(unknown.Points.Where(p => p.State is not null), p => Assert.Equal(0, p.Value));
        Assert.Contains(unknown.Diagnostics, d => d.Code == "unknown_state");
        Assert.Contains(unknown.Diagnostics, d => d.Code == "unknown_request");
        Assert.DoesNotContain(unknown.Points, p => p.Request is not null);
        var conflict = Parse("\"topics\":{\"t\":{\"partitions\":{\"-1\":{\"committed_offset\":42,\"commited_offset\":43}}}}");
        Assert.False(conflict.IsValid);
        Assert.Contains(conflict.Diagnostics, d => d.Code == "alias_conflict");
        Assert.Empty(conflict.Points);
    }

    [Theory]
    [InlineData("\"extra\":{\"nested\":{\"x\":1,\"x\":2}}", "duplicate_property")]
    [InlineData("\"topics\":{\"t\":{\"age\":\"bad\"}}", "invalid_numeric")]
    [InlineData("\"tx\":1.5", "invalid_numeric")]
    [InlineData("\"tx\":9223372036854775808", "numeric_overflow")]
    [InlineData("\"topics\":{\"t\":{\"topic\":\"other\"}}", "identity_mismatch")]
    public void Given_MalformedKnownOrDuplicateFields_When_Parsed_Then_ProjectionIsRejected(string fields, string code)
    {
        var result = Parse(fields);
        Assert.False(result.IsValid);
        Assert.Empty(result.Points);
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    private static KafkaNativeMetricProjection Parse(string fields) => KafkaNativeMetricParser.Parse("{" + Identity + "," + fields + "}");
}
