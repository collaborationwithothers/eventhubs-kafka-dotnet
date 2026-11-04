using Parchwell.Verifier.Common.Phase;
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
    private static readonly string[] ExpectedB04EvidenceFields = ["delivery_error_codes", "fatal_errors"];
    private static readonly int[] ExpectedB04DeliveryErrorCodes = [int.MinValue, 0, int.MaxValue];
    private static readonly string[] ExpectedFatalErrorFields = ["code", "name"];
    private static readonly string[] B10PublicationNames = ["index.json", "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];

    // The phase wire format uses exactly the same passive measurement bounds as a repeat result.
    [Fact]
    public void Given_BatchingCandidate_When_RoundTripped_Then_ProjectsEvidenceAndAllowsAbsence()
    {
        PhaseCandidate source = Candidate();
        PhaseCandidate batching = source with
        {
            ScenarioId = "B07", ConfigurationId = "C1", Phase = "C1",
            Repeats = [source.Repeats[0] with { BatchingEvidence = new(4, 4000, 9500, 2372592, 2372592, 11, 11, 8) }]
        };
        using JsonDocument document = PhaseCandidateCodec.Read(PhaseCandidateCodec.Write(batching));
        JsonElement evidence = Assert.Single(document.RootElement.GetProperty("repeats").EnumerateArray()).GetProperty("batching_recorded_evidence");
        Assert.Equal(BatchingRecordedEvidence.FieldNames, evidence.EnumerateObject().Select(x => x.Name));
        Assert.Equal(9500, evidence.GetProperty("latency_p50_us").GetInt64());
        Assert.Equal(11, evidence.GetProperty("produce_requests").GetInt64());
        Assert.Equal(8, evidence.GetProperty("batch_max_messages").GetInt64());
        PhaseCandidate noEvidence = batching with { Repeats = [batching.Repeats[0] with { BatchingEvidence = null }] };
        using JsonDocument absent = PhaseCandidateCodec.Read(PhaseCandidateCodec.Write(noEvidence));
        Assert.False(Assert.Single(absent.RootElement.GetProperty("repeats").EnumerateArray()).TryGetProperty("batching_recorded_evidence", out _));
    }

    [Theory, MemberData(nameof(Results.VerifierResultCodecTests.MalformedBatchingEvidence), MemberType = typeof(Results.VerifierResultCodecTests))]
    public void Given_MalformedBatchingCandidateEvidence_When_Read_Then_Rejects(string json)
    {
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(CandidateWithBatching(json))));
    }

    [Fact]
    public void Given_MalformedBatchingRecord_When_Written_Then_ThrowsFormatException()
    {
        PhaseCandidate candidate = Candidate();
        PhaseCandidate malformed = candidate with { Repeats = [candidate.Repeats[0] with { BatchingEvidence = new(1, 2, 1, 2, 2, null, null, null) }] };
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Write(malformed));
    }

    [Fact]
    public void Given_B04Candidate_When_RoundTripped_Then_ProjectsExactEvidenceAndAllowsAbsence()
    {
        PhaseCandidate a01=Candidate();
        PhaseCandidate b04=a01 with
        {
            ScenarioId="B04",ConfigurationId="C1",Phase="C1",
            Repeats=[a01.Repeats[0] with { B04Evidence=new([-185,7],[new B04FatalError(3,"fatal")]) }]
        };
        byte[] bytes=PhaseCandidateCodec.Write(b04);
        using JsonDocument document=PhaseCandidateCodec.Read(bytes);
        JsonElement repeat=Assert.Single(document.RootElement.GetProperty("repeats").EnumerateArray());
        JsonElement evidence=repeat.GetProperty("b04_recorded_evidence");
        Assert.Equal(ExpectedB04EvidenceFields,evidence.EnumerateObject().Select(x=>x.Name));
        Assert.Equal(new[]{-185,7},evidence.GetProperty("delivery_error_codes").EnumerateArray().Select(x=>x.GetInt32()));
        PhaseCandidate noEvidence=b04 with { Repeats=[b04.Repeats[0] with { B04Evidence=null }] };
        using JsonDocument absent=PhaseCandidateCodec.Read(PhaseCandidateCodec.Write(noEvidence));
        Assert.False(Assert.Single(absent.RootElement.GetProperty("repeats").EnumerateArray()).TryGetProperty("b04_recorded_evidence",out _));
    }

    [Fact]
    public void Given_MalformedB04CandidateEvidence_When_Read_Then_Rejects()
    {
        string json=System.Text.Encoding.UTF8.GetString(PhaseCandidateCodec.Write(Candidate()));
        json=json.Replace("\"workload_observation\":", "\"b04_recorded_evidence\":{\"delivery_error_codes\":[2,1]},\"workload_observation\":", StringComparison.Ordinal);
        Assert.Throws<FormatException>(()=>PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"delivery_error_codes\":[],\"fatal_errors\":[]}")]
    [InlineData("{\"delivery_error_codes\":[-2147483648,0,2147483647],\"fatal_errors\":[{\"code\":-2147483648,\"name\":\"\"},{\"code\":2147483647,\"name\":\"z\"}]}")]
    public void Given_ValidB04CandidateEvidence_When_Read_Then_PreservesClosedValues(string evidence)
    {
        using JsonDocument parsed=PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(CandidateWithB04(evidence)));
        JsonElement actual=Assert.Single(parsed.RootElement.GetProperty("repeats").EnumerateArray()).GetProperty("b04_recorded_evidence");
        if(evidence=="{}")Assert.Empty(actual.EnumerateObject());
        else if(evidence=="{\"delivery_error_codes\":[],\"fatal_errors\":[]}")
        {
            Assert.Empty(actual.GetProperty("delivery_error_codes").EnumerateArray());
            Assert.Empty(actual.GetProperty("fatal_errors").EnumerateArray());
        }
        else
        {
            Assert.Equal(ExpectedB04EvidenceFields,actual.EnumerateObject().Select(x=>x.Name));
            if(actual.TryGetProperty("delivery_error_codes",out JsonElement codes))
                Assert.Equal(ExpectedB04DeliveryErrorCodes,codes.EnumerateArray().Select(x=>x.GetInt32()));
            if(actual.TryGetProperty("fatal_errors",out JsonElement fatal))
            {
                Assert.Equal(ExpectedFatalErrorFields,fatal[0].EnumerateObject().Select(x=>x.Name));
                Assert.Equal(int.MinValue,fatal[0].GetProperty("code").GetInt32());
                Assert.Equal(string.Empty,fatal[0].GetProperty("name").GetString());
            }
        }
    }

    [Theory]
    [InlineData("{\"unknown\":1}")]
    [InlineData("{\"delivery_error_codes\":null}")]
    [InlineData("{\"delivery_error_codes\":[\"1\"]}")]
    [InlineData("{\"delivery_error_codes\":[2147483648]}")]
    [InlineData("{\"delivery_error_codes\":[2,1]}")]
    [InlineData("{\"delivery_error_codes\":[1,1]}")]
    [InlineData("{\"fatal_errors\":null}")]
    [InlineData("{\"fatal_errors\":[{\"code\":1,\"name\":2}]}")]
    [InlineData("{\"fatal_errors\":[{\"code\":\"1\",\"name\":\"n\"}]}")]
    [InlineData("{\"fatal_errors\":[{\"code\":2147483648,\"name\":\"n\"}]}")]
    [InlineData("{\"fatal_errors\":[{\"code\":1,\"name\":\"n\",\"extra\":true}]}")]
    [InlineData("{\"fatal_errors\":[{\"code\":1}]}")]
    [InlineData("{\"fatal_errors\":[{\"code\":1,\"code\":1,\"name\":\"n\"}]}")]
    [InlineData("{\"fatal_errors\":[{\"code\":2,\"name\":\"n\"},{\"code\":1,\"name\":\"n\"}]}")]
    [InlineData("{\"fatal_errors\":[{\"code\":1,\"name\":\"z\"},{\"code\":1,\"name\":\"a\"}]}")]
    [InlineData("{\"fatal_errors\":[{\"code\":1,\"name\":\"a\"},{\"code\":1,\"name\":\"a\"}]}")]
    public void Given_MalformedB04CandidateJson_When_Read_Then_Rejects(string evidence)
    {
        Assert.Throws<FormatException>(()=>PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(CandidateWithB04(evidence))));
    }

    [Theory]
    [InlineData(255)] [InlineData(256)] [InlineData(257)]
    public void Given_B04CandidateArrayBoundaries_When_ReadAndWritten_Then_Enforces256(int count)
    {
        string codes=string.Join(',',Enumerable.Range(0,count));
        string fatal=string.Join(',',Enumerable.Range(0,count).Select(i=>"{\"code\":"+i+",\"name\":\"n\"}"));
        bool valid=count<=256;
        Assert.Equal(valid,TryReadCandidate(CandidateWithB04("{\"delivery_error_codes\":["+codes+"]}")));
        Assert.Equal(valid,TryReadCandidate(CandidateWithB04("{\"fatal_errors\":["+fatal+"]}")));
        PhaseCandidate candidate=CandidateWithB04(new B04RecordedEvidence(
            Enumerable.Range(0,count).ToArray(),Enumerable.Range(0,count).Select(i=>new B04FatalError(i,"n")).ToArray()));
        if(valid)using(PhaseCandidateCodec.Read(PhaseCandidateCodec.Write(candidate))){ }
        else Assert.Throws<FormatException>(()=>PhaseCandidateCodec.Write(candidate));
    }

    [Theory]
    [InlineData(0)] [InlineData(255)] [InlineData(256)] [InlineData(257)]
    public void Given_B04CandidateFatalNameBoundaries_When_ReadAndWritten_Then_Enforces256(int length)
    {
        string name=new('x',length);
        string json=CandidateWithB04("{\"fatal_errors\":[{\"code\":0,\"name\":\""+name+"\"}]}");
        bool valid=length<=256;
        Assert.Equal(valid,TryReadCandidate(json));
        PhaseCandidate candidate=CandidateWithB04(new B04RecordedEvidence(null,[new B04FatalError(0,name)]));
        if(valid)using(PhaseCandidateCodec.Read(PhaseCandidateCodec.Write(candidate))){ }
        else Assert.Throws<FormatException>(()=>PhaseCandidateCodec.Write(candidate));
    }

    [Fact]
    public void Given_UnsortedB04CandidateRecords_When_Written_Then_RejectsMalformedValues()
    {
        PhaseCandidate delivery=CandidateWithB04(new B04RecordedEvidence([2,1],[]));
        PhaseCandidate fatal=CandidateWithB04(new B04RecordedEvidence(null,[new B04FatalError(2,"n"),new B04FatalError(1,"n")]));
        PhaseCandidate equalCodeReverseNames=CandidateWithB04(new B04RecordedEvidence(null,[new B04FatalError(1,"z"),new B04FatalError(1,"a")]));
        PhaseCandidate duplicateFatal=CandidateWithB04(new B04RecordedEvidence(null,[new B04FatalError(1,"a"),new B04FatalError(1,"a")]));
        Assert.Throws<FormatException>(()=>PhaseCandidateCodec.Write(delivery));
        Assert.Throws<FormatException>(()=>PhaseCandidateCodec.Write(fatal));
        Assert.Throws<FormatException>(()=>PhaseCandidateCodec.Write(equalCodeReverseNames));
        Assert.Throws<FormatException>(()=>PhaseCandidateCodec.Write(duplicateFatal));
    }

    [Theory]
    [InlineData("A01","probe","C0",true)]
    [InlineData("A01","C0","C0",true)]
    [InlineData("A01","C3","C3",true)]
    [InlineData("B04","C0","C0",true)]
    [InlineData("B04","C1","C1",true)]
    [InlineData("B04","probe","C0",false)]
    [InlineData("B04","C3","C3",false)]
    [InlineData("A01","C1","C1",false)]
    [InlineData("B04","C1","C0",false)]
    public void Given_ScenarioPhaseConfiguration_When_CandidateRead_Then_EnforcesExecutablePairs(string scenario,string phase,string configuration,bool accepted)
    {
        string json=System.Text.Encoding.UTF8.GetString(PhaseCandidateCodec.Write(Candidate()));
        json=json.Replace("\"scenario_id\":\"A01\"",$"\"scenario_id\":\"{scenario}\"",StringComparison.Ordinal)
            .Replace("\"configuration_id\":\"C0\"",$"\"configuration_id\":\"{configuration}\"",StringComparison.Ordinal)
            .Replace("\"phase\":\"C0\"",$"\"phase\":\"{phase}\"",StringComparison.Ordinal);
        if(accepted)using(PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(json))){ }
        else Assert.Throws<FormatException>(()=>PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(json)));
    }
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

    [Theory]
    [InlineData("C0", 1)]
    [InlineData("C0", 5)]
    [InlineData("C1", 1)]
    [InlineData("C1", 5)]
    public void Given_B10Pair_When_CandidateRoundTrips_Then_PreservesBothRolesAndSnapshots(string phase, int ordinal)
    {
        PhaseCandidate candidate = B10Candidate(phase, ordinal);
        using JsonDocument document = PhaseCandidateCodec.Read(PhaseCandidateCodec.Write(candidate));
        JsonElement repeat = document.RootElement.GetProperty("repeats")[0];
        B10IntakeEvidence actual = PhaseWireCodec.ReadB10Intake(repeat.GetProperty("b10_intake"));
        B10IntakeEvidence expected = candidate.Repeats[0].B10Intake!;
        Assert.Equal(phase, actual.Phase);
        Assert.Equal(ordinal, actual.RepeatNumber);
        Assert.Equal("pod-first", repeat.GetProperty("identity").GetString());
        Assert.Equal(2, actual.Producers.Count);
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(expected.Producers[i].Identity, actual.Producers[i].Identity);
            Assert.Equal(expected.Producers[i].JobName, actual.Producers[i].JobName);
            Assert.Equal(expected.Producers[i].PredecessorInstanceId, actual.Producers[i].PredecessorInstanceId);
            PublicationAcceptanceSnapshot expectedSnapshot = expected.Producers[i].PublicationAcceptance!;
            PublicationAcceptanceSnapshot actualSnapshot = Assert.IsType<PublicationAcceptanceSnapshot>(actual.Producers[i].PublicationAcceptance);
            Assert.Equal(expectedSnapshot.ProducerPrefix, actualSnapshot.ProducerPrefix);
            Assert.Equal(expectedSnapshot.ProducerIdentity, actualSnapshot.ProducerIdentity);
            Assert.Equal(expectedSnapshot.SourceCommit, actualSnapshot.SourceCommit);
            Assert.Equal(expectedSnapshot.StartLine, actualSnapshot.StartLine);
            Assert.Equal(expectedSnapshot.BrokerInventory, actualSnapshot.BrokerInventory);
            Assert.Equal(expectedSnapshot.ProducerReceipt, actualSnapshot.ProducerReceipt);
            Assert.Equal(expectedSnapshot.Objects, actualSnapshot.Objects);
        }
        Assert.False(document.RootElement.TryGetProperty("counts", out _));
        Assert.False(repeat.TryGetProperty("counts", out _));
    }

    [Theory]
    [InlineData("C0")]
    [InlineData("C1")]
    public void Given_B10WithoutTrustedIntake_When_CandidateRoundTrips_Then_AllowsOmission(string phase)
    {
        PhaseCandidate candidate = B10Candidate(phase, 1);
        candidate = candidate with { Repeats = [candidate.Repeats[0] with { B10Intake = null }] };
        using JsonDocument document = PhaseCandidateCodec.Read(PhaseCandidateCodec.Write(candidate));
        Assert.False(document.RootElement.GetProperty("repeats")[0].TryGetProperty("b10_intake", out _));
        Assert.Equal("incomplete", document.RootElement.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("root-status", true)]
    [InlineData("root-status", false)]
    [InlineData("repeat-status", true)]
    [InlineData("repeat-status", false)]
    [InlineData("root-counts", true)]
    [InlineData("root-counts", false)]
    [InlineData("repeat-counts", true)]
    [InlineData("repeat-counts", false)]
    public void Given_B10FinalityOrCounts_When_ReadOrWritten_Then_RejectsEvenWithoutIntake(string change, bool hasIntake)
    {
        PhaseCandidate candidate = B10Candidate("C0", 1);
        if (!hasIntake) candidate = candidate with { Repeats = [candidate.Repeats[0] with { B10Intake = null }] };
        JsonObject root = JsonNode.Parse(PhaseCandidateCodec.Write(candidate))!.AsObject();
        var zero = new OutcomeCounts(0, 0, 0, 0, 0, 0);
        PhaseCandidate invalid = change switch
        {
            "root-status" => candidate with { Status = "complete_valid" },
            "repeat-status" => candidate with { Repeats = [candidate.Repeats[0] with { Status = "complete_valid" }] },
            "root-counts" => candidate with { Counts = zero },
            "repeat-counts" => candidate with { Repeats = [candidate.Repeats[0] with { Counts = zero }] },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        JsonObject target = change.StartsWith("root", StringComparison.Ordinal) ? root : root["repeats"]![0]!.AsObject();
        if (change.EndsWith("status", StringComparison.Ordinal)) target["status"] = "complete_valid";
        else target["counts"] = JsonNode.Parse("{\"ok\":0,\"loss\":0,\"phantom\":0,\"reported_failure\":0,\"duplicate\":0,\"reorder\":0}");
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Write(invalid));
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Theory]
    [InlineData("non-b10")]
    [InlineData("run")]
    [InlineData("configuration")]
    [InlineData("phase")]
    [InlineData("ordinal")]
    [InlineData("identity")]
    [InlineData("identity-null")]
    [InlineData("zero-producers")]
    [InlineData("one-producer")]
    [InlineData("three-producers")]
    [InlineData("swapped-roles")]
    [InlineData("duplicate-instance")]
    [InlineData("predecessor")]
    [InlineData("first-predecessor")]
    [InlineData("snapshot-path")]
    [InlineData("snapshot-source")]
    public void Given_MalformedTypedB10Candidate_When_Written_Then_Rejects(string change)
    {
        PhaseCandidate candidate = B10Candidate("C0", 1);
        PhaseRepeatResult repeat = candidate.Repeats[0];
        B10IntakeEvidence intake = repeat.B10Intake!;
        B10ProducerEvidence first = intake.Producers[0], second = intake.Producers[1];
        PublicationAcceptanceSnapshot snapshot = second.PublicationAcceptance!;
        candidate = change switch
        {
            "non-b10" => candidate with { ScenarioId = "A01" },
            "run" => candidate with { RunId = "foreign" },
            "configuration" => candidate with { ConfigurationId = "C1", Phase = "C1" },
            "phase" => candidate with { Phase = "C1", ConfigurationId = "C1", Repeats = [repeat with
                { B10Intake = B10Candidate("C1", 1).Repeats[0].B10Intake! with { Phase = "C0" } }] },
            "ordinal" => candidate with { Repeats = [repeat with { Repeat = 5 }] },
            "identity" => candidate with { Repeats = [repeat with { Identity = "pod-other" }] },
            "identity-null" => candidate with { Repeats = [repeat with { Identity = null }] },
            "zero-producers" => WithIntake(intake with { Producers = [] }),
            "one-producer" => WithIntake(intake with { Producers = [first] }),
            "three-producers" => WithIntake(intake with { Producers = [first, second, second] }),
            "swapped-roles" => WithIntake(intake with { Producers = [second, first] }),
            "duplicate-instance" => WithIntake(intake with { Producers = [first, second with { Identity = first.Identity }] }),
            "predecessor" => WithIntake(intake with { Producers = [first, second with { PredecessorInstanceId = "pod-other" }] }),
            "first-predecessor" => WithIntake(intake with { Producers = [first with { PredecessorInstanceId = "pod-other" }, second] }),
            "snapshot-path" => WithIntake(intake with { Producers = [first, second with { PublicationAcceptance = snapshot with
                { ProducerReceipt = snapshot.ProducerReceipt with { Name = "foreign.json" } } }] }),
            "snapshot-source" => WithIntake(intake with { Producers = [first, second with { PublicationAcceptance = snapshot with { SourceCommit = new string('b', 40) } }] }),
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Write(candidate));

        PhaseCandidate WithIntake(B10IntakeEvidence value) => candidate with { Repeats = [repeat with { B10Intake = value }] };
    }

    [Theory]
    [InlineData("non-b10")]
    [InlineData("null-intake")]
    [InlineData("run")]
    [InlineData("configuration")]
    [InlineData("phase")]
    [InlineData("ordinal")]
    [InlineData("identity")]
    [InlineData("identity-null")]
    [InlineData("identity-object")]
    [InlineData("zero-producers")]
    [InlineData("one-producer")]
    [InlineData("three-producers")]
    [InlineData("swapped-roles")]
    [InlineData("duplicate-instance")]
    [InlineData("predecessor")]
    [InlineData("first-predecessor")]
    [InlineData("snapshot-path")]
    [InlineData("snapshot-source")]
    [InlineData("unknown-intake-field")]
    [InlineData("unknown-producer-field")]
    [InlineData("unknown-snapshot-field")]
    [InlineData("private-key")]
    public void Given_MalformedB10WireCandidate_When_Read_Then_Rejects(string change)
    {
        JsonObject root = JsonNode.Parse(PhaseCandidateCodec.Write(B10Candidate("C0", 1)))!.AsObject();
        JsonObject repeat = root["repeats"]![0]!.AsObject();
        JsonObject intake = repeat["b10_intake"]!.AsObject();
        JsonArray producers = intake["producers"]!.AsArray();
        JsonObject second = producers[1]!.AsObject();
        JsonObject snapshot = second["publication_acceptance"]!.AsObject();
        switch (change)
        {
            case "non-b10": root["scenario_id"] = "A01"; break;
            case "null-intake": repeat["b10_intake"] = null; break;
            case "run": root["run_id"] = "foreign"; break;
            case "configuration": root["configuration_id"] = "C1"; root["phase"] = "C1"; break;
            case "phase": intake["phase"] = "C1"; break;
            case "ordinal": repeat["repeat"] = 5; break;
            case "identity": repeat["identity"] = "pod-other"; break;
            case "identity-null": repeat["identity"] = null; break;
            case "identity-object": repeat["identity"] = new JsonObject(); break;
            case "zero-producers": producers.Clear(); break;
            case "one-producer": producers.RemoveAt(1); break;
            case "three-producers": producers.Add(second.DeepClone()); break;
            case "swapped-roles": intake["producers"] = new JsonArray(second.DeepClone(), producers[0]!.DeepClone()); break;
            case "duplicate-instance": second["identity"] = producers[0]!["identity"]!.DeepClone(); break;
            case "predecessor": second["predecessor_instance_id"] = "pod-other"; break;
            case "first-predecessor": producers[0]!["predecessor_instance_id"] = "pod-other"; break;
            case "snapshot-path": snapshot["producer_receipt"]!["name"] = "foreign.json"; break;
            case "snapshot-source": snapshot["source_commit"] = new string('b', 40); break;
            case "unknown-intake-field": intake["unknown"] = 1; break;
            case "unknown-producer-field": second["unknown"] = 1; break;
            case "unknown-snapshot-field": snapshot["unknown"] = 1; break;
            case "private-key": snapshot["connection_string"] = "private"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Theory]
    [InlineData("\"b10_intake\":{", "\"b10_intake\":{\"phase\":\"C0\",")]
    [InlineData("\"job_name\":", "\"job_name\":\"duplicate\",\"job_name\":")]
    [InlineData("\"producer_prefix\":", "\"producer_prefix\":\"duplicate\",\"producer_prefix\":")]
    public void Given_DuplicateB10NestedField_When_Read_Then_Rejects(string existing, string replacement)
    {
        string json = System.Text.Encoding.UTF8.GetString(PhaseCandidateCodec.Write(B10Candidate("C0", 1)));
        Assert.Contains(existing, json, StringComparison.Ordinal);
        json = json.Replace(existing, replacement, StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    private static PhaseCandidate B10Candidate(string phase, int ordinal)
    {
        const string run = "synthetic-c5";
        string source = new('a', 40);
        B10ProducerEvidence Producer(string instance, string client, string job, string? predecessor)
        {
            var identity = new Parchwell.Verifier.Common.Contracts.RunIdentity("B10", phase, run, instance, instance, client);
            ArtifactReference Reference(string container, string name) => new(container, name, 1, new string('b', 64));
            var snapshot = new PublicationAcceptanceSnapshot(PhasePaths.ProducerPrefix(run, phase, ordinal, instance), identity, source,
                Reference("verifier", PhasePaths.StartLine(run, phase, ordinal)),
                Reference("verifier", PhasePaths.BrokerInventory(run, phase, ordinal)),
                Reference("ledger", PhasePaths.ProducerCompletion(run, phase, ordinal, instance)),
                B10PublicationNames.Select(name => Reference("ledger", PhasePaths.Publication(run, phase, ordinal, instance, name))).ToArray());
            return new(identity, job, predecessor, snapshot);
        }
        var intake = new B10IntakeEvidence(phase, ordinal,
        [
            Producer("pod-first", "client-first", PhaseIdentity.JobName(run, phase, ordinal), null),
            Producer("pod-second", "client-second", PhaseIdentity.RestartJobName(run, phase, ordinal), "pod-first")
        ]);
        PhaseCandidate candidate = Candidate();
        return candidate with
        {
            ScenarioId = "B10", Phase = phase, ConfigurationId = phase, Status = "incomplete", Counts = null,
            Repeats = [candidate.Repeats[0] with
            {
                Repeat = ordinal, Status = "incomplete", Counts = null, Identity = "pod-first", B10Intake = intake,
                ProbeObservation = null, WorkloadObservation = null, BrokerCount = null
            }]
        };
    }

    private static byte[] WithBrokerCountJson(string countJson)
    {
        JsonObject root = JsonNode.Parse(PhaseCandidateCodec.Write(Candidate()))!.AsObject();
        root["repeats"]![0]!.AsObject().Remove("broker_count");
        string json = root.ToJsonString();
        Assert.Contains("\"repeat\":1,", json, StringComparison.Ordinal);
        return System.Text.Encoding.UTF8.GetBytes(json.Replace("\"repeat\":1,", "\"repeat\":1,\"broker_count\":" + countJson + ",", StringComparison.Ordinal));
    }

    private static string CandidateWithBatching(string evidenceJson)
    {
        string json=System.Text.Encoding.UTF8.GetString(PhaseCandidateCodec.Write(Candidate()));
        return json.Replace("\"workload_observation\":", "\"batching_recorded_evidence\":"+evidenceJson+",\"workload_observation\":", StringComparison.Ordinal);
    }

    private static string CandidateWithB04(string evidenceJson)
    {
        string json=System.Text.Encoding.UTF8.GetString(PhaseCandidateCodec.Write(Candidate()));
        return json.Replace("\"workload_observation\":", "\"b04_recorded_evidence\":"+evidenceJson+",\"workload_observation\":", StringComparison.Ordinal);
    }

    private static PhaseCandidate CandidateWithB04(B04RecordedEvidence evidence)
    {
        PhaseCandidate candidate=Candidate();
        return candidate with { Repeats=[candidate.Repeats[0] with { B04Evidence=evidence }] };
    }

    private static bool TryReadCandidate(string json)
    {
        try { using(PhaseCandidateCodec.Read(System.Text.Encoding.UTF8.GetBytes(json))) { return true; } }
        catch(FormatException) { return false; }
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
