using System.Text.Json;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Judge;
using Parchwell.Verifier.Common.Serialization;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

#pragma warning disable CA1707
public sealed class PhaseCandidateCodecTests
{
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

    private static PhaseCandidate Candidate()
    {
        var checks=new PhaseRepeatChecks(true,true,true,true,true,true,true);
        var workload=new WorkloadObservation(true,1,1,1,true,null,[new WorkloadStageObservation("ramp100",100,60,1,1,0)]);
        var repeat=new PhaseRepeatResult(1,"complete_valid",[],checks,new(1,0,0,0,0,0),new(true,true,"sasl_ssl://private-host:9093/1",0,1,20001),"loadgen",workload);
        return new(1,true,"A01","C0","synthetic-c5","C0",new string('a',40),"complete_valid",null,
            new Dictionary<string,string?>{{"verifier","1.0"},{"workload_generator","generator"},{"confluent_kafka","2.16"},{"librdkafka","2.16"},{"dotnet","10.0"}},
            new Dictionary<string,string?>{{"loadgen","sha256:"+new string('a',64)},{"verifier","sha256:"+new string('b',64)}},
            new Dictionary<string,object?>{{"worker_started_utc",null},{"judge_completed_utc",null},{"duration_seconds",null}},
            new Dictionary<string,object?>{{"error_codes",Array.Empty<string>()},{"max_throttle_ms",20001L},{"request_timeout_increment",0L},{"disconnect_increment",0L},{"rtt_p99_ms",99.3m}},
            [repeat],new(1,0,0,0,0,0),[]);
    }
}
#pragma warning restore CA1707
