using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Xunit;

namespace Parchwell.Verifier.Tests.Phase;

#pragma warning disable CA1707
public sealed class B10SweepEvidenceCodecDuplicateTests
{
    private static ArtifactReference Ref(string path) => new("verifier", path, 1, new string('a', 64));
    private static B10Finalization Value(DuplicateBreakdown? observation = null) => new(1, "B10", "C0", "synthetic-c5", "C0", new string('a', 40),
        Ref(PhasePaths.SweepLine("synthetic-c5", "C0")), true, B10SweepEvidence.ResidualRisk,
        Enumerable.Range(1, 5).Select(i => new B10RepeatFinalization(i, Ref(PhasePaths.Result("synthetic-c5", "C0", i)),
            Ref(PhasePaths.VerifyCompletion("synthetic-c5", "C0", i)), true, VerifierResultStatus.CompleteValid,
            new(1, 0, 0, 0, observation?.Total ?? 0, 0), [], observation)).ToArray());

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Given_OptionalObservation_When_RoundTripped_Then_PreservesPresence(bool present)
    {
        B10Finalization value = Value(present ? new(1, 2) : null);
        byte[] bytes = B10SweepEvidenceCodec.WriteFinalization(value);
        B10Finalization read = B10SweepEvidenceCodec.ReadFinalization(bytes);
        Assert.Equal(value.Repeats[0].DuplicateBreakdown, read.Repeats[0].DuplicateBreakdown);
        Assert.Equal(bytes, B10SweepEvidenceCodec.WriteFinalization(read));
    }

    [Fact]
    public void Given_ZeroAndMaximumObservations_When_RoundTripped_Then_PreservesValues()
    {
        foreach (DuplicateBreakdown value in new[] { new DuplicateBreakdown(0, 0), new DuplicateBreakdown(1, 0), new DuplicateBreakdown(long.MaxValue, 0) })
        {
            B10Finalization result = B10SweepEvidenceCodec.ReadFinalization(B10SweepEvidenceCodec.WriteFinalization(Value(value)));
            Assert.Equal(value, result.Repeats[0].DuplicateBreakdown);
        }
    }

    [Theory]
    [InlineData("null")] [InlineData("[]")] [InlineData("{}")] [InlineData("{\"duplicate_same_attempt\":0}")] [InlineData("{\"duplicate_different_attempt\":0}")]
    [InlineData("{\"duplicate_same_attempt\":null,\"duplicate_different_attempt\":0}")]
    [InlineData("{\"duplicate_same_attempt\":0,\"duplicate_different_attempt\":null}")]
    [InlineData("{\"duplicate_same_attempt\":0,\"duplicate_different_attempt\":-1}")]
    [InlineData("{\"duplicate_same_attempt\":-1,\"duplicate_different_attempt\":1}")]
    [InlineData("{\"duplicate_same_attempt\":0.5,\"duplicate_different_attempt\":0}")]
    [InlineData("{\"duplicate_same_attempt\":\"0\",\"duplicate_different_attempt\":0}")]
    [InlineData("{\"duplicate_same_attempt\":true,\"duplicate_different_attempt\":0}")]
    [InlineData("{\"duplicate_same_attempt\":9223372036854775808,\"duplicate_different_attempt\":0}")]
    [InlineData("{\"duplicate_same_attempt\":9223372036854775807,\"duplicate_different_attempt\":1}")]
    [InlineData("{\"duplicate_same_attempt\":0,\"duplicate_different_attempt\":0,\"extra\":0}")]
    [InlineData("{\"duplicate_same_attempt\":0,\"duplicate_same_attempt\":0,\"duplicate_different_attempt\":0}")]
    [InlineData("{\"duplicate_same_attempt\":1,\"duplicate_different_attempt\":0}")]
    public void Given_MalformedObservation_When_Read_Then_Rejects(string observation)
    {
        string json = Encoding.UTF8.GetString(B10SweepEvidenceCodec.WriteFinalization(Value()));
        json = json.Replace("\"classification_final\":true", "\"duplicate_breakdown\":" + observation + ",\"classification_final\":true", StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData(1, false)] [InlineData(2, true)] [InlineData(3, false)]
    public void Given_ComponentSum_When_ComparedWithDuplicateCount_Then_RequiresEquality(long same, bool accepted)
    {
        B10Finalization value = Value(new(2, 0));
        B10RepeatFinalization[] repeats = value.Repeats.ToArray();
        repeats[0] = repeats[0] with { DuplicateBreakdown = new(same, 0) };
        if (accepted) Assert.NotEmpty(B10SweepEvidenceCodec.WriteFinalization(value with { Repeats = repeats }));
        else Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.WriteFinalization(value with { Repeats = repeats }));
        JsonObject root = JsonNode.Parse(B10SweepEvidenceCodec.WriteFinalization(value))!.AsObject();
        root["repeats"]![0]!["duplicate_breakdown"]!["duplicate_same_attempt"] = same;
        byte[] bytes = Encoding.UTF8.GetBytes(root.ToJsonString());
        if (accepted) Assert.NotNull(B10SweepEvidenceCodec.ReadFinalization(bytes));
        else Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(bytes));
    }

    [Theory]
    [InlineData(-1, 0)] [InlineData(0, -1)] [InlineData(1, 0)] [InlineData(0, 1)]
    [InlineData(long.MaxValue, 1)]
    public void Given_InvalidModelComponents_When_Written_Then_Rejects(long same, long different)
    {
        B10Finalization value = Value();
        B10RepeatFinalization[] repeats = value.Repeats.ToArray();
        repeats[0] = repeats[0] with { DuplicateBreakdown = new(same, different) };
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.WriteFinalization(value with { Repeats = repeats }));
    }

    [Theory]
    [InlineData("invalid")] [InlineData("incomplete")] [InlineData("no-counts")] [InlineData("schema")] [InlineData("unknown")]
    public void Given_ObservationWithInvalidEnvelope_When_Read_Then_Rejects(string change)
    {
        JsonObject root = JsonNode.Parse(B10SweepEvidenceCodec.WriteFinalization(Value(new(0, 0))))!.AsObject();
        JsonNode repeat = root["repeats"]![0]!;
        switch (change)
        {
            case "invalid": repeat["status"] = "invalid"; repeat["classification_final"] = false; repeat["counts"] = null; break;
            case "incomplete": repeat["status"] = "incomplete"; repeat["classification_final"] = false; repeat["counts"] = null; break;
            case "no-counts": repeat["counts"] = null; break;
            case "schema": root["schema_version"] = 2; break;
            case "unknown": repeat["extra"] = 0; break;
        }
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Fact]
    public void Given_InvalidModelObservation_When_Written_Then_Rejects()
    {
        B10Finalization value = Value();
        B10RepeatFinalization[] repeats = value.Repeats.ToArray();
        repeats[0] = repeats[0] with { DuplicateBreakdown = new(1, 0) };
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.WriteFinalization(value with { Repeats = repeats }));
        repeats[0] = repeats[0] with { DuplicateBreakdown = new(long.MaxValue, 1) };
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.WriteFinalization(value with { Repeats = repeats }));
    }

    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(1, true)]
    public void Given_ObservationEntries_When_LimitVaries_Then_EnforcesLimit(int delta, bool accepted)
    {
        byte[] bytes = B10SweepEvidenceCodec.WriteFinalization(Value(new(1, 2)));
        using JsonDocument document = JsonDocument.Parse(bytes);
        long entries = Entries(document.RootElement);
        var limits = new InputLimits(16, 65536, 65536, 65536, 10000, entries + delta, 256);
        if (accepted) Assert.NotNull(B10SweepEvidenceCodec.ReadFinalization(bytes, limits));
        else Assert.Throws<InputLimitExceededException>(() => B10SweepEvidenceCodec.ReadFinalization(bytes, limits));
    }

    private static long Entries(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().Sum(p => 1 + Entries(p.Value)),
        JsonValueKind.Array => value.EnumerateArray().Sum(item => 1 + Entries(item)),
        _ => 0
    };

    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(1, true)]
    public void Given_ObservationBytes_When_LimitVaries_Then_EnforcesLimit(int delta, bool accepted)
    {
        B10Finalization value = Value(new(1, 2));
        byte[] bytes = B10SweepEvidenceCodec.WriteFinalization(value);
        var limits = new InputLimits(16, bytes.Length + delta, 65536, 65536, 10000, 10000, 256);
        if (accepted) Assert.NotNull(B10SweepEvidenceCodec.ReadFinalization(bytes, limits));
        else Assert.Throws<InputLimitExceededException>(() => B10SweepEvidenceCodec.ReadFinalization(bytes, limits));
        if (accepted) Assert.NotEmpty(B10SweepEvidenceCodec.WriteFinalization(value, new(bytes.Length + delta, 100, 256)));
        else Assert.Throws<InputLimitExceededException>(() => B10SweepEvidenceCodec.WriteFinalization(value, new(bytes.Length + delta, 100, 256)));
    }
}
#pragma warning restore CA1707
