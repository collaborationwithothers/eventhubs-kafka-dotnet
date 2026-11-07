using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Host;
using Xunit;

namespace Parchwell.Verifier.Tests.Results;

#pragma warning disable CA1707
public sealed class B10ProducerObservationsCodecTests
{
    private static readonly InputLimits Inputs = new(32, 16_777_216, 64_000_000, 1_000_000, 100_000, 100_000, 4096);
    private static readonly OutputLimits Outputs = new(16_777_216, 100, 4096);

    [Fact]
    public async Task Given_IndependentSyntheticObservations_When_ResultRoundTrips_Then_RetainsVersionsOrderAndSeparateCounts()
    {
        VerifierResult result = await Result();
        result = result with { B10ProducerObservations = Observations(result.B10Intake!) };
        byte[] bytes = VerifierResultCodec.Write(result, Outputs);
        var parsed = VerifierResultCodec.Read(bytes, "synthetic-result", Inputs, 100);
        Assert.True(parsed.IsSuccess);
        using JsonDocument json = JsonDocument.Parse(bytes);
        AssertPair(json.RootElement.GetProperty("b10_producer_observations"));
        var observations = Assert.IsAssignableFrom<IReadOnlyList<B10ProducerObservation>>(parsed.Value!.B10ProducerObservations);
        Assert.Equal(["pod-first", "pod-second"], observations.Select(x => x.InstanceId));
        Assert.Equal(3, observations[0].BrokerCount!.Maximum);
        Assert.Equal(7, observations[1].BrokerCount!.Maximum);
        Assert.Equal(0, observations[1].BrokerCount!.FirstPostDrain);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("optional")]
    [InlineData("empty")]
    public async Task Given_LegacyOrUnavailableObservationFields_When_RoundTripped_Then_PreservesAbsenceAndEmptyValues(string change)
    {
        VerifierResult result = await Result();
        result = result with { B10ProducerObservations = change == "absent" ? null : result.B10Intake!.Producers.Select(p =>
            new B10ProducerObservation(p.Identity.InstanceId, change == "empty" ? "" : null,
                change == "empty" ? new Dictionary<string, string>(StringComparer.Ordinal) : null, null)).ToArray() };
        byte[] bytes = VerifierResultCodec.Write(result, Outputs);
        Assert.True(VerifierResultCodec.Read(bytes, "synthetic-result", Inputs, 100).IsSuccess);
        using JsonDocument json = JsonDocument.Parse(bytes);
        if (change == "absent") Assert.False(json.RootElement.TryGetProperty("b10_producer_observations", out _));
        else foreach (JsonElement entry in json.RootElement.GetProperty("b10_producer_observations").EnumerateArray())
        {
            Assert.False(entry.TryGetProperty("broker_count", out _));
            Assert.Equal(change == "empty", entry.TryGetProperty("generator_version", out JsonElement version));
            Assert.Equal(change == "empty", entry.TryGetProperty("runtime_versions", out JsonElement runtimes));
            if (change == "empty") { Assert.Equal("", version.GetString()); Assert.Empty(runtimes.EnumerateObject()); }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task Given_ObservationCardinality_When_ReadOrWritten_Then_RequiresExactlyTwo(int count)
    {
        VerifierResult result = await Result();
        B10ProducerObservation[] pair = Observations(result.B10Intake!).ToArray();
        var supplied = Enumerable.Range(0, count).Select(i => pair[i % 2]).ToArray();
        if (count == 2) Assert.NotEmpty(VerifierResultCodec.Write(result with { B10ProducerObservations = supplied }, Outputs));
        else Assert.Throws<FormatException>(() => VerifierResultCodec.Write(result with { B10ProducerObservations = supplied }, Outputs));
        JsonObject root = JsonNode.Parse(VerifierResultCodec.Write(result with { B10ProducerObservations = null }, Outputs))!.AsObject();
        JsonArray array = WirePair();
        root["b10_producer_observations"] = new JsonArray(Enumerable.Range(0, count).Select(i => array[i % 2]!.DeepClone()).ToArray());
        Assert.Equal(count == 2, VerifierResultCodec.Read(Encoding.UTF8.GetBytes(root.ToJsonString()), "synthetic-result", Inputs, 100).IsSuccess);
    }

    [Theory]
    [MemberData(nameof(InvalidWireCases))]
    public async Task Given_MalformedObservationWire_When_ResultRead_Then_RejectsWithStructuralField(string name, string wire)
    {
        Assert.NotEmpty(name);
        VerifierResult result = await Result();
        byte[] bytes = VerifierResultCodec.Write(result with { B10ProducerObservations = null }, Outputs);
        string json = Encoding.UTF8.GetString(bytes);
        json = json[..^1] + ",\"b10_producer_observations\":" + wire + "}";
        var parsed = VerifierResultCodec.Read(Encoding.UTF8.GetBytes(json), "synthetic-result", Inputs, 100);
        Assert.False(parsed.IsSuccess);
        Assert.All(parsed.Problems, problem => Assert.Null(problem.CheckNumber));
        Assert.Contains(parsed.Problems, problem => problem.Field == "b10_producer_observations" || problem.Code == "duplicate_property");
    }

    [Theory]
    [InlineData("reverse")] [InlineData("duplicate")] [InlineData("foreign")]
    [InlineData("generator")] [InlineData("runtime-key")] [InlineData("runtime-value")]
    [InlineData("role")] [InlineData("client")] [InlineData("maximum")] [InlineData("first")] [InlineData("lower-bound")]
    public async Task Given_MalformedTypedObservations_When_ResultWritten_Then_RejectsBeforePublication(string change)
    {
        VerifierResult result = await Result();
        B10ProducerObservation[] pair = Observations(result.B10Intake!).ToArray();
        if (change == "reverse") Array.Reverse(pair);
        else pair[1] = change switch
        {
            "duplicate" => pair[1] with { InstanceId = pair[0].InstanceId },
            "foreign" => pair[1] with { InstanceId = "foreign" },
            "generator" => pair[1] with { GeneratorVersion = new string('x', 257) },
            "runtime-key" => pair[1] with { RuntimeVersions = new Dictionary<string, string> { ["unknown"] = "x" } },
            "runtime-value" => pair[1] with { RuntimeVersions = new Dictionary<string, string> { ["dotnet"] = new('x', 257) } },
            "role" => pair[1] with { BrokerCount = pair[1].BrokerCount! with { Role = new('x', 257) } },
            "client" => pair[1] with { BrokerCount = pair[1].BrokerCount! with { ClientId = null! } },
            "maximum" => pair[1] with { BrokerCount = pair[1].BrokerCount! with { Maximum = -1 } },
            "first" => pair[1] with { BrokerCount = pair[1].BrokerCount! with { FirstPostDrain = -1 } },
            _ => pair[1] with { BrokerCount = pair[1].BrokerCount! with { LowerBound = false } }
        };
        Assert.Throws<FormatException>(() => VerifierResultCodec.Write(result with { B10ProducerObservations = pair }, Outputs));
    }

    [Theory]
    [InlineData(0)] [InlineData(255)] [InlineData(256)] [InlineData(257)]
    public async Task Given_ObservationTextBoundaries_When_RoundTripped_Then_RejectsOnlyAboveCeiling(int length)
    {
        VerifierResult result = await Result();
        var pair = Observations(result.B10Intake!).Select(x => x with
        {
            GeneratorVersion = new('x', length), RuntimeVersions = new Dictionary<string, string> { ["dotnet"] = new('x', length) },
            BrokerCount = x.BrokerCount! with { Role = new('x', length), ClientId = new('x', length) }
        }).ToArray();
        if (length > 256) Assert.Throws<FormatException>(() => VerifierResultCodec.Write(result with { B10ProducerObservations = pair }, Outputs));
        else
        {
            byte[] bytes = VerifierResultCodec.Write(result with { B10ProducerObservations = pair }, Outputs);
            var parsed = VerifierResultCodec.Read(bytes, "synthetic-result", Inputs, 100);
            Assert.True(parsed.IsSuccess);
            Assert.All(parsed.Value!.B10ProducerObservations!, x => Assert.Equal(new string('x', length), x.GeneratorVersion));
        }
    }

    [Theory]
    [InlineData(null)] [InlineData(-1L)] [InlineData(0L)] [InlineData(1L)] [InlineData(long.MaxValue)]
    public async Task Given_OptionalMeasurementBoundaries_When_RoundTripped_Then_NegativeRejectedAndMissingRemainsAbsent(long? value)
    {
        VerifierResult result = await Result();
        var pair = Observations(result.B10Intake!).Select(x => x with { BrokerCount = x.BrokerCount! with { Maximum = value, FirstPostDrain = value } }).ToArray();
        if (value < 0) Assert.Throws<FormatException>(() => VerifierResultCodec.Write(result with { B10ProducerObservations = pair }, Outputs));
        else
        {
            byte[] bytes = VerifierResultCodec.Write(result with { B10ProducerObservations = pair }, Outputs);
            var parsed = VerifierResultCodec.Read(bytes, "synthetic-result", Inputs, 100);
            Assert.True(parsed.IsSuccess);
            Assert.All(parsed.Value!.B10ProducerObservations!, x => { Assert.Equal(value, x.BrokerCount!.Maximum); Assert.Equal(value, x.BrokerCount.FirstPostDrain); });
            using JsonDocument json = JsonDocument.Parse(bytes);
            foreach (JsonElement entry in json.RootElement.GetProperty("b10_producer_observations").EnumerateArray())
                foreach (string field in new[] { "maximum", "first_post_drain" })
                    Assert.Equal(value.HasValue, entry.GetProperty("broker_count").TryGetProperty(field, out _));
        }
    }

    [Fact]
    public async Task Given_ObservationsWithoutB10Intake_When_ReadOrWritten_Then_Rejects()
    {
        VerifierResult source = await Result();
        var pair = Observations(source.B10Intake!);
        VerifierResult ordinary = source with { Identity = source.Identity with { ScenarioId = "A01" }, B10Intake = null,
            B10WindowChecksComplete = null, ClassificationFinal = null, B10ProducerObservations = null };
        Assert.Throws<FormatException>(() => VerifierResultCodec.Write(ordinary with { B10ProducerObservations = pair }, Outputs));
        string json = Encoding.UTF8.GetString(VerifierResultCodec.Write(ordinary, Outputs));
        json = json[..^1] + ",\"b10_producer_observations\":" + WirePair().ToJsonString() + "}";
        Assert.False(VerifierResultCodec.Read(Encoding.UTF8.GetBytes(json), "synthetic-result", Inputs, 100).IsSuccess);
    }

    public static IEnumerable<object[]> InvalidWireCases()
    {
        foreach (string scalar in new[] { "null", "{}", "true", "1", "\"pair\"" }) yield return ["array-" + scalar, scalar];
        foreach (string change in new[] { "reverse", "duplicate", "foreign", "missing-instance", "missing-role", "missing-client", "missing-lower-bound", "false-lower-bound" })
        {
            JsonArray pair = WirePair();
            var entry = pair[1]!.AsObject();
            switch (change)
            {
                case "reverse": pair = new JsonArray(pair[1]!.DeepClone(), pair[0]!.DeepClone()); break;
                case "duplicate": entry["instance_id"] = "pod-first"; break;
                case "foreign": entry["instance_id"] = "foreign"; break;
                case "missing-instance": entry.Remove("instance_id"); break;
                case "missing-role": entry["broker_count"]!.AsObject().Remove("role"); break;
                case "missing-client": entry["broker_count"]!.AsObject().Remove("client_id"); break;
                case "missing-lower-bound": entry["broker_count"]!.AsObject().Remove("lower_bound"); break;
                default: entry["broker_count"]!["lower_bound"] = false; break;
            }
            yield return [change, pair.ToJsonString()];
        }
        foreach (string path in new[] { "entry", "instance_id", "generator_version", "runtime_versions", "runtime_versions.dotnet", "broker_count", "broker_count.role", "broker_count.client_id", "broker_count.lower_bound", "broker_count.maximum", "broker_count.first_post_drain" })
            foreach (string wrong in new[] { "null", "[]", "{}", "false", "1", "\"wrong\"" })
            {
                if (path is "instance_id" or "generator_version" or "runtime_versions.dotnet" or "broker_count.role" or "broker_count.client_id" && wrong == "\"wrong\"") continue;
                if (path == "runtime_versions" && wrong == "{}") continue;
                if (path is "broker_count.maximum" or "broker_count.first_post_drain" && wrong == "1") continue;
                JsonArray pair = WirePair();
                string[] parts = path.Split('.');
                if (path == "entry") pair[1] = JsonNode.Parse(wrong);
                else if (parts.Length == 1) pair[1]![parts[0]] = JsonNode.Parse(wrong);
                else pair[1]![parts[0]]![parts[1]] = JsonNode.Parse(wrong);
                yield return [path + "-" + wrong, pair.ToJsonString()];
            }
        foreach (string path in new[] { "entry", "runtime_versions", "broker_count" })
        {
            JsonArray pair = WirePair();
            JsonObject target = path == "entry" ? pair[1]!.AsObject() : pair[1]![path]!.AsObject();
            target["unknown"] = "x";
            yield return [path + "-unknown", pair.ToJsonString()];
            pair = WirePair(); target = path == "entry" ? pair[1]!.AsObject() : pair[1]![path]!.AsObject();
            string objectJson = target.ToJsonString();
            string key = path == "entry" ? "instance_id" : path == "runtime_versions" ? "dotnet" : "role";
            string duplicate = objectJson[..^1] + ",\"" + key + "\":\"duplicate\"}";
            yield return [path + "-duplicate", pair.ToJsonString().Replace(objectJson, duplicate, StringComparison.Ordinal)];
        }
        foreach (string field in new[] { "maximum", "first_post_drain" })
            foreach (string invalid in new[] { "-1", "9223372036854775808" })
            {
                JsonArray pair = WirePair(); pair[1]!["broker_count"]![field] = JsonNode.Parse(invalid);
                yield return [field + "-" + invalid, pair.ToJsonString()];
            }
        foreach (string field in new[] { "generator_version", "runtime_versions.dotnet", "broker_count.role", "broker_count.client_id" })
        {
            JsonArray pair = WirePair(); string[] parts = field.Split('.');
            if (parts.Length == 1) pair[1]![parts[0]] = new string('x', 257);
            else pair[1]![parts[0]]![parts[1]] = new string('x', 257);
            yield return [field + "-257", pair.ToJsonString()];
        }
    }

    internal static IReadOnlyList<B10ProducerObservation> Observations(B10IntakeEvidence intake) => intake.Producers.Select((p, index) =>
        new B10ProducerObservation(p.Identity.InstanceId, "generator-a", new Dictionary<string, string>(StringComparer.Ordinal)
        { ["confluent_kafka"] = "kafka-a", ["librdkafka"] = "native-a", ["dotnet"] = "dotnet-a" },
            new("LoadGenProducer", p.Identity.ClientId, index == 0 ? 3 : 7, index == 0 ? 1 : 0, true))).ToArray();

    internal static JsonArray WirePair() => JsonNode.Parse("""
        [{"instance_id":"pod-first","generator_version":"generator-a","runtime_versions":{"confluent_kafka":"kafka-a","librdkafka":"native-a","dotnet":"dotnet-a"},"broker_count":{"role":"LoadGenProducer","client_id":"pod-first","maximum":3,"first_post_drain":1,"lower_bound":true}},
         {"instance_id":"pod-second","generator_version":"generator-a","runtime_versions":{"confluent_kafka":"kafka-a","librdkafka":"native-a","dotnet":"dotnet-a"},"broker_count":{"role":"LoadGenProducer","client_id":"pod-second","maximum":7,"first_post_drain":0,"lower_bound":true}}]
        """)!.AsArray();

    internal static void AssertPair(JsonElement pair)
    {
        Assert.Equal(2, pair.GetArrayLength());
        Assert.Equal(["pod-first", "pod-second"], pair.EnumerateArray().Select(x => x.GetProperty("instance_id").GetString()));
        foreach (JsonElement entry in pair.EnumerateArray())
        {
            Assert.Equal("generator-a", entry.GetProperty("generator_version").GetString());
            Assert.Equal("kafka-a", entry.GetProperty("runtime_versions").GetProperty("confluent_kafka").GetString());
            Assert.Equal("native-a", entry.GetProperty("runtime_versions").GetProperty("librdkafka").GetString());
            Assert.Equal("dotnet-a", entry.GetProperty("runtime_versions").GetProperty("dotnet").GetString());
            Assert.True(entry.GetProperty("broker_count").GetProperty("lower_bound").GetBoolean());
        }
        Assert.Equal(3, pair[0].GetProperty("broker_count").GetProperty("maximum").GetInt64());
        Assert.Equal(1, pair[0].GetProperty("broker_count").GetProperty("first_post_drain").GetInt64());
        Assert.Equal(7, pair[1].GetProperty("broker_count").GetProperty("maximum").GetInt64());
        Assert.Equal(0, pair[1].GetProperty("broker_count").GetProperty("first_post_drain").GetInt64());
    }

    private static async Task<VerifierResult> Result()
    {
        using var fixture = B10TwoInstanceVerifyTests.CreateSyntheticB10Intake();
        Assert.Equal(1, await fixture.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        var parsed = VerifierResultCodec.Read(fixture.Output!, "synthetic-result", Inputs, 100);
        Assert.True(parsed.IsSuccess);
        return parsed.Value!;
    }
}
