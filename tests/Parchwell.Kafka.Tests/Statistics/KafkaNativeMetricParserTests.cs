using System.Text.Json.Nodes;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707

[Trait("Category", "Unit")]
public sealed class KafkaNativeMetricParserTests
{
    private const string Identity = "\"name\":\"native\",\"client_id\":\"client\",\"type\":\"producer\",\"ts\":1,\"time\":1,\"age\":1";
    private static readonly string[] EmptyWindowFields = ["cnt", "hdrsize", "outofrange"];

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
        Assert.Equal(EmptyWindowFields, points.Keys.Select(k => k.Split('.')[^1]).Order());
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

    public static TheoryData<string, string[]> FiniteStates => new()
    {
        { "brokers.b.state", ["INIT", "DOWN", "TRY_CONNECT", "CONNECT", "SSL_HANDSHAKE", "AUTH_LEGACY", "UP", "APIVERSION_QUERY", "AUTH_HANDSHAKE", "AUTH_REQ", "REAUTH"] },
        { "topics.t.partitions.-1.fetch_state", ["none", "stopping", "stopped", "offset-query", "offset-wait", "validate-epoch-wait", "active"] },
        { "cgrp.state", ["init", "term", "query-coord", "wait-coord", "wait-broker", "wait-broker-transport", "up"] },
        { "cgrp.join_state", ["init", "wait-join", "wait-metadata", "wait-sync", "wait-assign-call", "wait-unassign-call", "wait-unassign-to-complete", "wait-incr-unassign-to-complete", "steady"] },
        { "eos.idemp_state", ["Init", "Terminate", "FatalError", "RequestPID", "WaitTransport", "WaitPID", "Assigned", "DrainReset", "DrainBump", "WaitTxnAbort"] },
        { "eos.txn_state", ["Init", "WaitPID", "ReadyNotAcked", "Ready", "InTransaction", "BeginCommit", "CommittingTransaction", "CommitNotAcked", "BeginAbort", "AbortingTransaction", "AbortedNotAcked", "AbortableError", "FatalError"] },
        { "brokers.b.source", ["learned", "configured", "internal", "logical"] },
    };

    [Theory]
    [MemberData(nameof(FiniteStates))]
    public void Given_EachFiniteState_When_Projected_Then_TheLiteralVectorIsOneHot(string path, string[] allowed)
    {
        foreach (string input in allowed.Append("arbitrary-value"))
        {
            var root = JsonNode.Parse("{" + Identity + "}")!.AsObject();
            Set(root, path, JsonValue.Create(input)!);
            var result = KafkaNativeMetricParser.Parse(root.ToJsonString());
            Assert.True(result.IsValid);
            var points = result.Points.Where(p => p.State is not null).ToArray();
            Assert.Equal(allowed.Order(), points.Select(p => p.State!).Order());
            foreach (var point in points) Assert.Equal(point.State == input ? 1 : 0, point.Value);
            Assert.Equal(input == "arbitrary-value", result.Diagnostics.Any(d => d.Code == "unknown_state"));
        }
    }

    [Fact]
    public void Given_RequestAllowlist_When_Projected_Then_OnlyTheEightyLiteralKeysAppear()
    {
        string[] requests = ["Produce", "Fetch", "ListOffsets", "Metadata", "LeaderAndIsr", "StopReplica", "UpdateMetadata", "ControlledShutdown", "OffsetCommit", "OffsetFetch",
            "FindCoordinator", "JoinGroup", "Heartbeat", "LeaveGroup", "SyncGroup", "DescribeGroups", "ListGroups", "SaslHandshake", "ApiVersion", "CreateTopics",
            "DeleteTopics", "DeleteRecords", "InitProducerId", "OffsetForLeaderEpoch", "AddPartitionsToTxn", "AddOffsetsToTxn", "EndTxn", "WriteTxnMarkers", "TxnOffsetCommit", "DescribeAcls",
            "CreateAcls", "DeleteAcls", "DescribeConfigs", "AlterConfigs", "AlterReplicaLogDirs", "DescribeLogDirs", "SaslAuthenticate", "CreatePartitions", "CreateDelegationToken", "RenewDelegationToken",
            "ExpireDelegationToken", "DescribeDelegationToken", "DeleteGroups", "ElectLeadersRequest", "IncrementalAlterConfigsRequest", "AlterPartitionReassignmentsRequest", "ListPartitionReassignmentsRequest", "OffsetDeleteRequest", "DescribeClientQuotasRequest", "AlterClientQuotasRequest",
            "DescribeUserScramCredentialsRequest", "AlterUserScramCredentialsRequest", "VoteRequest", "BeginQuorumEpochRequest", "EndQuorumEpochRequest", "DescribeQuorumRequest", "AlterIsrRequest", "UpdateFeaturesRequest", "EnvelopeRequest", "FetchSnapshot",
            "DescribeCluster", "DescribeProducers", "Unknown-62?", "BrokerHeartbeat", "UnregisterBroker", "DescribeTransactions", "ListTransactions", "AllocateProducerIds", "ConsumerGroupHeartbeat", "ConsumerGroupDescribe",
            "Unknown-70?", "GetTelemetrySubscriptions", "PushTelemetry", "Unknown-73?", "Unknown-74?", "Unknown-75?", "ShareGroupHeartbeat", "Unknown-77?", "ShareFetch", "ShareAcknowledge"];
        var root = JsonNode.Parse("{" + Identity + "}")!.AsObject();
        foreach (string request in requests.Append("Unknown-81?")) Set(root, "brokers.b.req." + request, JsonValue.Create(2)!);
        var result = KafkaNativeMetricParser.Parse(root.ToJsonString());
        Assert.True(result.IsValid);
        Assert.Equal(requests.Order(), result.Points.Where(p => p.Request is not null).Select(p => p.Request!).Order());
        Assert.Contains(result.Diagnostics, d => d.Code == "unknown_request");
    }

    [Fact]
    public void Given_AliasOnlyAndSignedSentinels_When_Projected_Then_ExactNativeValuesRemain()
    {
        var result = Parse("\"brokers\":{\"b\":{\"nodeid\":-1,\"txidle\":-1}},\"eos\":{\"producer_id\":-1,\"producer_epoch\":-1,\"txn_may_enq\":false},\"topics\":{\"t\":{\"partitions\":{\"-1\":{\"commited_offset\":42,\"query_offset\":-1001,\"leader_epoch\":-1,\"next_ack_seq\":1}}}}");
        Assert.True(result.IsValid);
        foreach (var (name, value) in new[] { ("broker.nodeid", -1), ("broker.txidle", -1), ("eos.producer_id", -1), ("eos.producer_epoch", -1), ("eos.txn_may_enq", 0), ("topic.partition.committed_offset", 42), ("topic.partition.query_offset", -1001), ("topic.partition.leader_epoch", -1), ("topic.partition.next_ack_seq", 1) })
            Assert.Equal(value, Assert.Single(result.Points, p => p.Definition.Name == "kafka.librdkafka." + name).Value);
    }

    [Fact]
    public void Given_UnknownNestedDepthExceedsLimit_When_Parsed_Then_ProjectionIsRejected()
    {
        string nested = string.Concat(Enumerable.Repeat("{\"x\":", 65)) + "0" + new string('}', 65);
        var result = Parse("\"unknown\":" + nested);
        Assert.False(result.IsValid);
        Assert.Empty(result.Points);
        Assert.Contains(result.Diagnostics, d => d.Code == "invalid_json");
    }

    private static void Set(JsonObject root, string path, JsonNode value)
    {
        string[] segments = path.Split('.');
        JsonObject parent = root;
        foreach (string segment in segments[..^1])
        {
            parent[segment] ??= new JsonObject();
            parent = parent[segment]!.AsObject();
        }
        parent[segments[^1]] = value;
    }

    private static KafkaNativeMetricProjection Parse(string fields) => KafkaNativeMetricParser.Parse("{" + Identity + "," + fields + "}");
}
