using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.StartLine;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class BrokerInventoryTests
{
    [Fact]
    public async Task Given_IndependentMetadata_When_StartLineCompletes_Then_PublishesSortedInventoryAfterCleanup()
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Options.BrokerInventoryOutputName = "notes/brokers.json";
        fixture.Kafka.ReadMetadata = () => Metadata(new(9, "2001:db8::1", 9093), new(2, "broker.example.invalid", 9093));
        Assert.Equal(0, await fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["admin-dispose", "consumer-dispose", "create", "create"], fixture.Events);
        Assert.Equal(2, fixture.Creates);
        Assert.Single(fixture.Kafka.MetadataTimeouts);
        Assert.Equal("notes/brokers.json", fixture.OutputName);
        using JsonDocument json = JsonDocument.Parse(fixture.Output!);
        Assert.Equal(1, json.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal("kafka_metadata", json.RootElement.GetProperty("source").GetString());
        Assert.Equal(fixture.Options.SourceCommit, json.RootElement.GetProperty("source_commit").GetString());
        Assert.Equal(6, json.RootElement.GetProperty("identity").EnumerateObject().Count());
        Assert.Equal(fixture.Options.RunId, json.RootElement.GetProperty("identity").GetProperty("run_id").GetString());
        JsonElement brokers = json.RootElement.GetProperty("brokers");
        Assert.Equal([2, 9], brokers.EnumerateArray().Select(b => b.GetProperty("id").GetInt32()));
        Assert.Equal(["sasl_ssl://broker.example.invalid:9093/2", "sasl_ssl://[2001:db8::1]:9093/9"],
            brokers.EnumerateArray().Select(b => b.GetProperty("name").GetString()));
        Assert.Equal("2001:db8::1", brokers[1].GetProperty("host").GetString());
        Assert.Equal(9093, brokers[1].GetProperty("port").GetInt32());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(63, true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public async Task Given_BrokerCountBoundary_When_InventoryRequested_Then_EnforcesOneThrough64(int count, bool succeeds)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Options.BrokerInventoryOutputName = "inventory.json";
        fixture.Kafka.ReadMetadata = () => Metadata(Enumerable.Range(0, count)
            .Select(id => new BrokerMetadata(id, "broker.example.invalid", 9093)).ToArray());
        if (succeeds)
        {
            Assert.Equal(0, await fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
            using JsonDocument json = JsonDocument.Parse(fixture.Output!);
            Assert.Equal(count, json.RootElement.GetProperty("brokers").GetArrayLength());
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, fixture.Creates);
        }
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Events.Take(2));
    }

    [Theory]
    [InlineData(-1, "broker.example.invalid", 9093)]
    [InlineData(1, "", 9093)]
    [InlineData(1, "broker/name", 9093)]
    [InlineData(1, "broker name", 9093)]
    [InlineData(1, "broker.example.invalid", 0)]
    [InlineData(1, "broker.example.invalid", 65536)]
    public async Task Given_MalformedMetadata_When_InventoryRequested_Then_CleansBeforeZeroPublications(int id, string host, int port)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Options.BrokerInventoryOutputName = "inventory.json";
        fixture.Kafka.ReadMetadata = () => Metadata(new BrokerMetadata(id, host, port));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Events);
        Assert.Equal(0, fixture.Creates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_DuplicateIdOrName_When_InventoryRequested_Then_Rejects(bool sameName)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Options.BrokerInventoryOutputName = "inventory.json";
        fixture.Kafka.ReadMetadata = () => Metadata(new(1, "broker.example.invalid", 9093),
            new(1, sameName ? "broker.example.invalid" : "other.example.invalid", 9093));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, fixture.Creates);
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("cleanup")]
    [InlineData("publication")]
    public async Task Given_InventoryPathFailure_When_StartLineRuns_Then_StopsWithoutRetry(string failure)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Options.BrokerInventoryOutputName = "inventory.json";
        fixture.Kafka.ReadMetadata = () => Metadata(new BrokerMetadata(1, "broker.example.invalid", 9093));
        if (failure == "metadata") fixture.Kafka.ReadMetadata = () => throw new KafkaException(new Error(ErrorCode.Local_Transport));
        if (failure == "cleanup") fixture.Kafka.AdminDispose = () => throw new IOException("synthetic cleanup failure");
        if (failure == "publication") fixture.AfterCreate = _ =>
        {
            if (fixture.Creates == 2) throw new IOException("synthetic uncertain inventory create");
        };
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(failure == "publication" ? 2 : 0, fixture.Creates);
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Kafka.Events);
    }

    [Fact]
    public void Given_InventoryOverOutputLimit_When_Encoded_Then_Rejects()
    {
        Assert.Throws<InputLimitExceededException>(() => BrokerInventoryCodec.Write(
            new RunIdentity("A01", "C0", "run", "pod", "pod", "pod"),
            [new BrokerMetadata(0, "broker.example.invalid", 9093)], "synthetic", 1));
    }

    [Theory]
    [InlineData(0,false)]
    [InlineData(1,true)]
    [InlineData(63,true)]
    [InlineData(64,true)]
    [InlineData(65,false)]
    public void Given_ReadInventoryBrokerCountBoundary_When_Parsed_Then_EnforcesOneThrough64(int count,bool succeeds)
    {
        RunIdentity identity=new("A01","C0","synthetic-c5","synthetic-c5-c0-r1-loadgen","synthetic-c5-c0-r1-loadgen","synthetic-c5-c0-r1-loadgen");
        BrokerMetadata[] brokers=Enumerable.Range(0,Math.Max(1,Math.Min(count,64))).Select(i=>new BrokerMetadata(i,$"broker-{i}.example.invalid",9093)).ToArray();
        byte[] bytes=BrokerInventoryCodec.Write(identity,brokers,new string('a',40),1_048_576);
        if(count is 0 or 65)
        {
            JsonObject json=JsonNode.Parse(bytes)!.AsObject();
            if(count==0)json["brokers"]!.AsArray().Clear();
            else json["brokers"]!.AsArray().Add(json["brokers"]![0]!.DeepClone());
            bytes=Encoding.UTF8.GetBytes(json.ToJsonString());
        }

        ParseResult<BrokerInventory> result=BrokerInventoryReader.Read(bytes,"inventory.json",identity,new string('a',40),new(32,1_048_576,1_048_576,1_048_576,100_000,1_000_000,65_536));

        Assert.Equal(succeeds,result.IsSuccess);
        if(succeeds)Assert.Equal(count,result.Value!.Brokers.Count);
    }

    [Theory]
    [InlineData(252, true)]
    [InlineData(253, true)]
    [InlineData(254, false)]
    public void Given_HostLengthBoundary_When_Encoded_Then_EnforcesMaximum(int length, bool succeeds)
    {
        string host = string.Join('.', new string('a', 63), new string('b', 63), new string('c', 63), new string('d', length - 192));
        Assert.Equal(length, host.Length);
        var identity = new RunIdentity("A01", "C0", "run", "pod", "pod", "pod");
        BrokerMetadata[] brokers = [new(0, host, 9093)];
        if (succeeds)
        {
            using JsonDocument json = JsonDocument.Parse(BrokerInventoryCodec.Write(identity, brokers, "synthetic", 1048576));
            Assert.Equal(host, json.RootElement.GetProperty("brokers")[0].GetProperty("host").GetString());
        }
        else Assert.Throws<InvalidOperationException>(() => BrokerInventoryCodec.Write(identity, brokers, "synthetic", 1048576));
    }

    [Theory]
    [InlineData(252, true)]
    [InlineData(253, true)]
    [InlineData(254, false)]
    public void Given_HostLengthBoundary_When_IndependentReaderReads_Then_EnforcesMaximum(int length, bool succeeds)
    {
        string host = string.Join('.', new string('a', 63), new string('b', 63), new string('c', 63), new string('d', length - 192));
        RunIdentity identity = new("A01", "C0", "synthetic-c5", "synthetic-c5-c0-r1-loadgen", "synthetic-c5-c0-r1-loadgen", "synthetic-c5-c0-r1-loadgen");
        string json = $"{{\"schema_version\":1,\"identity\":{{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"run_id\":\"synthetic-c5\",\"instance_id\":\"synthetic-c5-c0-r1-loadgen\",\"expected_instance_id\":\"synthetic-c5-c0-r1-loadgen\",\"client_id\":\"synthetic-c5-c0-r1-loadgen\"}},\"brokers\":[{{\"id\":0,\"host\":\"{host}\",\"port\":9093,\"name\":\"sasl_ssl://{host}:9093/0\"}}],\"source\":\"kafka_metadata\",\"source_commit\":\"{new string('a', 40)}\"}}";
        ParseResult<BrokerInventory> result = BrokerInventoryReader.Read(Encoding.UTF8.GetBytes(json), "inventory.json", identity, new string('a', 40), new(32, 1_048_576, 1_048_576, 1_048_576, 100_000, 1_000_000, 65_536));
        Assert.Equal(succeeds, result.IsSuccess);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(65535, true)]
    [InlineData(65536, false)]
    public void Given_PortBoundary_When_IndependentReaderReads_Then_EnforcesRange(int port, bool succeeds)
    {
        RunIdentity identity = new("A01", "C0", "synthetic-c5", "synthetic-c5-c0-r1-loadgen", "synthetic-c5-c0-r1-loadgen", "synthetic-c5-c0-r1-loadgen");
        string json = $"{{\"schema_version\":1,\"identity\":{{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"run_id\":\"synthetic-c5\",\"instance_id\":\"synthetic-c5-c0-r1-loadgen\",\"expected_instance_id\":\"synthetic-c5-c0-r1-loadgen\",\"client_id\":\"synthetic-c5-c0-r1-loadgen\"}},\"brokers\":[{{\"id\":0,\"host\":\"broker.example.invalid\",\"port\":{port},\"name\":\"sasl_ssl://broker.example.invalid:{port}/0\"}}],\"source\":\"kafka_metadata\",\"source_commit\":\"{new string('a', 40)}\"}}";
        ParseResult<BrokerInventory> result = BrokerInventoryReader.Read(Encoding.UTF8.GetBytes(json), "inventory.json", identity, new string('a', 40), new(32, 1_048_576, 1_048_576, 1_048_576, 100_000, 1_000_000, 65_536));
        Assert.Equal(succeeds, result.IsSuccess);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(65534, true)]
    [InlineData(65535, true)]
    [InlineData(65536, false)]
    public void Given_PortBoundary_When_Encoded_Then_EnforcesRange(int port, bool succeeds)
    {
        var identity = new RunIdentity("A01", "C0", "run", "pod", "pod", "pod");
        BrokerMetadata[] brokers = [new(0, "broker.example.invalid", port)];
        if (succeeds)
        {
            using JsonDocument json = JsonDocument.Parse(BrokerInventoryCodec.Write(identity, brokers, "synthetic", 1048576));
            Assert.Equal(port, json.RootElement.GetProperty("brokers")[0].GetProperty("port").GetInt32());
        }
        else Assert.Throws<InvalidOperationException>(() => BrokerInventoryCodec.Write(identity, brokers, "synthetic", 1048576));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Given_BrokerIdentifierBoundary_When_Encoded_Then_EnforcesNonnegative(int id, bool succeeds)
    {
        var identity = new RunIdentity("A01", "C0", "run", "pod", "pod", "pod");
        BrokerMetadata[] brokers = [new(id, "broker.example.invalid", 9093)];
        if (succeeds)
        {
            using JsonDocument json = JsonDocument.Parse(BrokerInventoryCodec.Write(identity, brokers, "synthetic", 1048576));
            Assert.Equal(id, json.RootElement.GetProperty("brokers")[0].GetProperty("id").GetInt32());
        }
        else Assert.Throws<InvalidOperationException>(() => BrokerInventoryCodec.Write(identity, brokers, "synthetic", 1048576));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Given_ExactOutputByteBoundary_When_Encoded_Then_EnforcesLimit(int delta, bool succeeds)
    {
        var identity = new RunIdentity("A01", "C0", "run", "pod", "pod", "pod");
        BrokerMetadata[] brokers = [new(0, "broker.example.invalid", 9093)];
        byte[] expected = BrokerInventoryCodec.Write(identity, brokers, "synthetic", 1048576);
        long limit = expected.Length + delta;
        if (succeeds) Assert.Equal(expected, BrokerInventoryCodec.Write(identity, brokers, "synthetic", limit));
        else Assert.Throws<InputLimitExceededException>(() => BrokerInventoryCodec.Write(identity, brokers, "synthetic", limit));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("start-line.json")]
    public void Given_InvalidOptionalInventoryName_When_Validated_Then_Rejects(string name)
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("start-line");
        options.StartLineOutputName = "start-line.json";
        options.BrokerInventoryOutputName = name;
        Assert.False(new VerifierHostOptionsValidator().Validate(null, options).Succeeded);
    }

    private static Metadata Metadata(params BrokerMetadata[] brokers)
    {
        Metadata source = KafkaSessionTestSupport.Metadata(0);
        return new(brokers.ToList(), source.Topics, 0, "independent-admin");
    }
}
#pragma warning restore CA1707
