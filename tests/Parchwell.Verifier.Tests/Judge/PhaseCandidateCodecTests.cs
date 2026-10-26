using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Judge;
using Parchwell.Verifier.Common.Serialization;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

#pragma warning disable CA1707
public sealed class PhaseCandidateCodecTests
{
    private static readonly string[] BrokerCountFields = ["client_id", "first_post_drain", "lower_bound", "maximum", "role"];
    [Fact]
    public void GivenCandidateWithBrokerAndWorkload_WhenWritten_ThenProjectsOpaqueBrokerAndRecordOnlyWorkload()
    {
        byte[] bytes=PhaseCandidateCodec.Write(Candidate());
        using JsonDocument document=PhaseCandidateCodec.Read(bytes);
        JsonElement repeat=Assert.Single(document.RootElement.GetProperty("repeats").EnumerateArray());
        Assert.Equal("broker-",repeat.GetProperty("probe_observation").GetProperty("broker").GetString()![..7]);
        Assert.DoesNotContain("sasl_ssl://private-host:9093/1",System.Text.Encoding.UTF8.GetString(bytes),StringComparison.Ordinal);
        Assert.True(repeat.GetProperty("workload_observation").GetProperty("synthetic").GetBoolean());
        Assert.Equal(1,repeat.GetProperty("workload_observation").GetProperty("invoked_attempts").GetInt64());
    }

    [Fact]
    public void GivenUnknownNestedCandidateField_WhenRead_ThenRejectsTheProjection()
    {
        byte[] bytes=PhaseCandidateCodec.Write(Candidate());
        string json=System.Text.Encoding.UTF8.GetString(bytes).Replace("\"versions\":{", "\"versions\":{\"connection_string\":\"private\",", StringComparison.Ordinal);
        Assert.Throws<FormatException>(()=>PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void GivenCandidateWithoutAuthorizedCounts_WhenWritten_ThenOmitsCountProperties()
    {
        PhaseCandidate candidate=Candidate() with
        {
            Counts=null,
            Repeats=[Candidate().Repeats[0] with { Counts=null }]
        };
        byte[] bytes=PhaseCandidateCodec.Write(candidate);
        using JsonDocument document=PhaseCandidateCodec.Read(bytes);
        Assert.False(document.RootElement.TryGetProperty("counts",out _));
        Assert.False(Assert.Single(document.RootElement.GetProperty("repeats").EnumerateArray()).TryGetProperty("counts",out _));
    }

    [Theory]
    [InlineData("offered_attempts_per_second", "-1")]
    [InlineData("offered_attempts_per_second", "1.5")]
    [InlineData("offered_attempts_per_second", "9223372036854775808")]
    [InlineData("duration_seconds", "-1")]
    [InlineData("duration_seconds", "1.5")]
    [InlineData("duration_seconds", "9223372036854775808")]
    public void GivenInvalidWorkloadStageInt64_WhenRead_ThenRejects(string field, string invalid)
    {
        string json = System.Text.Encoding.UTF8.GetString(PhaseCandidateCodec.Write(Candidate()));
        string valid = field == "offered_attempts_per_second" ? "100" : "60";
        json = json.Replace($"\"{field}\":{valid}", $"\"{field}\":{invalid}", StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData(16_777_215, true)]
    [InlineData(16_777_216, true)]
    [InlineData(16_777_217, false)]
    public void GivenCandidateByteBoundary_WhenRead_ThenEnforcesMaximum(int length, bool accepted)
    {
        byte[] compact = PhaseCandidateCodec.Write(Candidate());
        byte[] padded = [.. compact, .. System.Text.Encoding.UTF8.GetBytes(new string(' ', length - compact.Length))];
        if (accepted) using (PhaseCandidateCodec.Read(padded)) { }
        else Assert.Throws<InputLimitExceededException>(() => PhaseCandidateCodec.Read(padded));
    }

    [Fact]
    public void GivenEmptyOrOverDepthCandidate_WhenRead_ThenRejects()
    {
        Assert.ThrowsAny<Exception>(() => PhaseCandidateCodec.Read(Array.Empty<byte>()));
        string json = System.Text.Encoding.UTF8.GetString(PhaseCandidateCodec.Write(Candidate()));
        string deep = json.Replace("\"reasons\":[]", "\"reasons\":[" + new string('[', 32) + "0" + new string(']', 32) + "]", StringComparison.Ordinal);
        Assert.ThrowsAny<Exception>(() => PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(deep)));
    }

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void GivenCandidateScalarLengthBoundary_WhenRead_ThenEnforcesMaximum(int length, bool accepted)
    {
        string json = System.Text.Encoding.UTF8.GetString(PhaseCandidateCodec.Write(Candidate()));
        json = json.Replace("\"verifier\":\"1.0\"", $"\"verifier\":\"{new string('v', length)}\"", StringComparison.Ordinal);
        if (accepted) using (PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(json))) { }
        else Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("timing", 255, true)]
    [InlineData("timing", 256, true)]
    [InlineData("timing", 257, false)]
    [InlineData("repeat_identity", 255, true)]
    [InlineData("repeat_identity", 256, true)]
    [InlineData("repeat_identity", 257, false)]
    [InlineData("repeat_reason", 255, true)]
    [InlineData("repeat_reason", 256, true)]
    [InlineData("repeat_reason", 257, false)]
    [InlineData("probe_broker", 255, true)]
    [InlineData("probe_broker", 256, true)]
    [InlineData("probe_broker", 257, false)]
    public void GivenNestedCandidateScalarLengthBoundary_WhenRead_ThenEnforcesMaximum(string field, int length, bool accepted)
    {
        string scalar = new('v', length);
        string json = System.Text.Encoding.UTF8.GetString(PhaseCandidateCodec.Write(Candidate()));
        json = field switch
        {
            "timing" => json.Replace("\"worker_started_utc\":null", $"\"worker_started_utc\":\"{scalar}\"", StringComparison.Ordinal),
            "repeat_identity" => json.Replace("\"identity\":\"loadgen\"", $"\"identity\":\"{scalar}\"", StringComparison.Ordinal),
            "repeat_reason" => json.Replace("\"reasons\":[]", $"\"reasons\":[\"{scalar}\"]", StringComparison.Ordinal),
            "probe_broker" => System.Text.RegularExpressions.Regex.Replace(json, "\"broker\":\"[^\"]*\"", $"\"broker\":\"{scalar}\"", System.Text.RegularExpressions.RegexOptions.CultureInvariant),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        if (accepted) using (PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(json))) { }
        else Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Given_RecordedBrokerCount_When_Written_Then_PreservesExactPerClientFieldsWithoutPhaseTotal()
    {
        using JsonDocument document = PhaseCandidateCodec.Read(PhaseCandidateCodec.Write(Candidate()));
        JsonElement root = document.RootElement;
        JsonElement count = Assert.Single(root.GetProperty("repeats").EnumerateArray()).GetProperty("broker_count");

        Assert.Equal(BrokerCountFields,
            count.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("LoadGenProducer", count.GetProperty("role").GetString());
        Assert.Equal("client-1", count.GetProperty("client_id").GetString());
        Assert.Equal(3, count.GetProperty("maximum").GetInt64());
        Assert.Equal(1, count.GetProperty("first_post_drain").GetInt64());
        Assert.True(count.GetProperty("lower_bound").GetBoolean());
        Assert.False(root.TryGetProperty("broker_count", out _));
        Assert.False(root.TryGetProperty("broker_count_total", out _));
        Assert.Equal(6, root.GetProperty("counts").EnumerateObject().Count());
    }

    [Fact]
    public void Given_AbsentBrokerCount_When_Written_Then_OmitsObjectAndReadsLegacyCandidate()
    {
        PhaseCandidate candidate = Candidate() with
        {
            Repeats = [Candidate().Repeats[0] with { BrokerCount = null }]
        };

        using JsonDocument document = PhaseCandidateCodec.Read(PhaseCandidateCodec.Write(candidate));

        Assert.False(Assert.Single(document.RootElement.GetProperty("repeats").EnumerateArray()).TryGetProperty("broker_count", out _));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, 1L)]
    [InlineData(3L, null)]
    [InlineData(0L, 0L)]
    public void Given_OptionalBrokerMeasurements_When_Written_Then_PreservesAbsenceAndMeasuredZero(long? maximum, long? postDrain)
    {
        PhaseCandidate candidate = Candidate() with
        {
            Repeats = [Candidate().Repeats[0] with { BrokerCount = new("LoadGenProducer", "client-1", maximum, postDrain, true) }]
        };
        using JsonDocument document = PhaseCandidateCodec.Read(PhaseCandidateCodec.Write(candidate));
        JsonElement count = document.RootElement.GetProperty("repeats")[0].GetProperty("broker_count");

        Assert.Equal(maximum.HasValue, count.TryGetProperty("maximum", out JsonElement maximumValue));
        if (maximum.HasValue) Assert.Equal(maximum.Value, maximumValue.GetInt64());
        Assert.Equal(postDrain.HasValue, count.TryGetProperty("first_post_drain", out JsonElement postDrainValue));
        if (postDrain.HasValue) Assert.Equal(postDrain.Value, postDrainValue.GetInt64());
        Assert.Equal("LoadGenProducer", count.GetProperty("role").GetString());
        Assert.Equal("client-1", count.GetProperty("client_id").GetString());
        Assert.True(count.GetProperty("lower_bound").GetBoolean());
    }

    [Fact]
    public void GivenBrokerCountWithoutLowerBound_WhenWritten_ThenRejects()
    {
        PhaseCandidate candidate = Candidate() with
        {
            Repeats = [Candidate().Repeats[0] with
            {
                BrokerCount = new("LoadGenProducer", "client-1", 3, 1, false)
            }]
        };
        Assert.Throws<ArgumentException>(() => PhaseCandidateCodec.Write(candidate));
    }

    [Theory]
    [InlineData("role")]
    [InlineData("client_id")]
    [InlineData("lower_bound")]
    public void Given_RequiredBrokerCountFieldMissing_When_Read_Then_Rejects(string field)
    {
        JsonObject count = JsonNode.Parse("{\"role\":\"LoadGenProducer\",\"client_id\":\"client-1\",\"maximum\":3,\"first_post_drain\":1,\"lower_bound\":true}")!.AsObject();
        Assert.True(count.Remove(field));

        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(WithBrokerCountJson(count.ToJsonString())));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("1")]
    [InlineData("{\"role\":\"LoadGenProducer\",\"client_id\":\"client-1\",\"lower_bound\":true,\"unexpected\":1}")]
    [InlineData("{\"role\":\"LoadGenProducer\",\"client_id\":\"client-1\",\"lower_bound\":true,\"raw\":{}}")]
    [InlineData("{\"role\":\"LoadGenProducer\",\"role\":\"LoadGenProducer\",\"client_id\":\"client-1\",\"lower_bound\":true}")]
    [InlineData("{\"role\":1,\"client_id\":\"client-1\",\"lower_bound\":true}")]
    [InlineData("{\"role\":\"LoadGenProducer\",\"client_id\":null,\"lower_bound\":true}")]
    [InlineData("{\"role\":\"LoadGenProducer\",\"client_id\":\"client-1\",\"lower_bound\":false}")]
    [InlineData("{\"role\":\"LoadGenProducer\",\"client_id\":\"client-1\",\"lower_bound\":null}")]
    [InlineData("{\"role\":\"LoadGenProducer\",\"client_id\":\"client-1\",\"lower_bound\":\"true\"}")]
    public void Given_MalformedBrokerCountObject_When_Read_Then_Rejects(string json)
    {
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(WithBrokerCountJson(json)));
    }

    [Theory]
    [InlineData("maximum", "null")]
    [InlineData("maximum", "\"1\"")]
    [InlineData("maximum", "true")]
    [InlineData("maximum", "1.5")]
    [InlineData("maximum", "-9223372036854775809")]
    [InlineData("maximum", "9223372036854775808")]
    [InlineData("first_post_drain", "null")]
    [InlineData("first_post_drain", "\"1\"")]
    [InlineData("first_post_drain", "true")]
    [InlineData("first_post_drain", "1.5")]
    [InlineData("first_post_drain", "-9223372036854775809")]
    [InlineData("first_post_drain", "9223372036854775808")]
    public void Given_InvalidBrokerMeasurement_When_Read_Then_Rejects(string field, string value)
    {
        string count = "{\"role\":\"LoadGenProducer\",\"client_id\":\"client-1\",\"lower_bound\":true,\"" + field + "\":" + value + "}";

        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(WithBrokerCountJson(count)));
    }

    [Theory]
    [InlineData("maximum", "-9223372036854775808", long.MinValue)]
    [InlineData("maximum", "-1", -1L)]
    [InlineData("maximum", "0", 0L)]
    [InlineData("maximum", "1", 1L)]
    [InlineData("maximum", "9223372036854775806", 9223372036854775806L)]
    [InlineData("maximum", "9223372036854775807", long.MaxValue)]
    [InlineData("first_post_drain", "-9223372036854775808", long.MinValue)]
    [InlineData("first_post_drain", "-1", -1L)]
    [InlineData("first_post_drain", "0", 0L)]
    [InlineData("first_post_drain", "1", 1L)]
    [InlineData("first_post_drain", "9223372036854775806", 9223372036854775806L)]
    [InlineData("first_post_drain", "9223372036854775807", long.MaxValue)]
    public void Given_SignedInt64BrokerMeasurement_When_Read_Then_PreservesPassiveRepresentation(string field, string value, long expected)
    {
        string count = "{\"role\":\"LoadGenProducer\",\"client_id\":\"client-1\",\"lower_bound\":true,\"" + field + "\":" + value + "}";

        using JsonDocument document = PhaseCandidateCodec.Read(WithBrokerCountJson(count));

        Assert.Equal(expected, document.RootElement.GetProperty("repeats")[0].GetProperty("broker_count").GetProperty(field).GetInt64());
    }

    [Theory]
    [InlineData("role", 255, true)]
    [InlineData("role", 256, true)]
    [InlineData("role", 257, false)]
    [InlineData("client_id", 255, true)]
    [InlineData("client_id", 256, true)]
    [InlineData("client_id", 257, false)]
    public void Given_BrokerCountStringBoundary_When_Read_Then_EnforcesExistingScalarLimit(string field, int length, bool accepted)
    {
        JsonObject count = JsonNode.Parse("{\"role\":\"LoadGenProducer\",\"client_id\":\"client-1\",\"lower_bound\":true}")!.AsObject();
        count[field] = new string('v', length);
        byte[] bytes = WithBrokerCountJson(count.ToJsonString());

        if (accepted) using (PhaseCandidateCodec.Read(bytes)) { }
        else Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(bytes));
    }

    private static byte[] WithBrokerCountJson(string countJson)
    {
        JsonObject root = JsonNode.Parse(PhaseCandidateCodec.Write(Candidate()))!.AsObject();
        root["repeats"]![0]!.AsObject().Remove("broker_count");
        string json = root.ToJsonString();
        Assert.Contains("\"repeat\":1,", json, StringComparison.Ordinal);
        return System.Text.Encoding.UTF8.GetBytes(json.Replace("\"repeat\":1,", "\"repeat\":1,\"broker_count\":" + countJson + ",", StringComparison.Ordinal));
    }

    private static PhaseCandidate Candidate()
    {
        var checks=new PhaseRepeatChecks(true,true,true,true,true,true,true);
        var workload=new WorkloadObservation(true,1,1,1,true,null,[new WorkloadStageObservation("ramp100",100,60,1,1,0)]);
        var repeat=new PhaseRepeatResult(1,"complete_valid",[],checks,new(1,0,0,0,0,0),new(true,true,"sasl_ssl://private-host:9093/1",0,1,20001),"loadgen",workload,new("LoadGenProducer","client-1",3,1,true));
        return new(1,true,"A01","C0","synthetic-c5","C0",new string('a',40),"complete_valid",null,
            new Dictionary<string,string?>{{"verifier","1.0"},{"workload_generator","generator"},{"confluent_kafka","2.16"},{"librdkafka","2.16"},{"dotnet","10.0"}},
            new Dictionary<string,string?>{{"loadgen","sha256:"+new string('a',64)},{"verifier","sha256:"+new string('b',64)}},
            new Dictionary<string,object?>{{"worker_started_utc",null},{"judge_completed_utc",null},{"duration_seconds",null}},
            new Dictionary<string,object?>{{"error_codes",Array.Empty<string>()},{"max_throttle_ms",20001L},{"request_timeout_increment",0L},{"disconnect_increment",0L},{"rtt_p99_ms",99.3m}},
            [repeat],new(1,0,0,0,0,0),[]);
    }
}
#pragma warning restore CA1707
