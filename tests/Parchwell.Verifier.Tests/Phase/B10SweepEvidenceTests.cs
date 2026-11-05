using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Phase;

#pragma warning disable CA1707
public sealed class B10SweepEvidenceTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
    private static readonly InputLimits Limits = new(16, 65536, 65536, 65536, 10000, 10000, 256);
    private static B10SweepLine Line() => new(1, "B10", "C0", "synthetic-c5", "C0", new string('a', 40), "document-events",
        [new(0, 10)], Epoch.AddSeconds(2), Epoch.AddSeconds(1), Epoch.AddSeconds(1), 1000);
    private static VerifierHostOptions Options() => new() { ScenarioId = "B10", ConfigurationId = "C0", RunId = "synthetic-c5", Phase = "C0",
        SourceCommit = new string('a', 40), MaxPartitions = 1, B10SettleIntervalMilliseconds = 1000 };
    private static ArtifactReference Ref(string path) => new("verifier", path, 1, new string('a', 64));
    private static B10Finalization Finalization() => new(1, "B10", "C0", "synthetic-c5", "C0", new string('a', 40),
        Ref(PhasePaths.SweepLine("synthetic-c5", "C0")), true, B10SweepEvidence.ResidualRisk,
        Enumerable.Range(1, 5).Select(i => new B10RepeatFinalization(i, Ref(PhasePaths.Result("synthetic-c5", "C0", i)),
            Ref(PhasePaths.VerifyCompletion("synthetic-c5", "C0", i)), true, VerifierResultStatus.CompleteValid, new(1, 0, 0, 0, 0, 0), [])).ToArray());

    [Theory]
    [InlineData("C0")] [InlineData("C1")]
    public void Given_ValidSweep_When_RoundTripped_Then_PreservesAllEvidence(string phase)
    {
        B10SweepLine line = Line() with { Phase = phase, ConfigurationId = phase };
        B10SweepLine parsed = B10SweepEvidenceCodec.ReadSweepLine(B10SweepEvidenceCodec.WriteSweepLine(line), Limits, 1);
        Assert.Equal(line with { PartitionHighs = parsed.PartitionHighs }, parsed);
        Assert.Equal(line.PartitionHighs, parsed.PartitionHighs);
        B10Finalization value = Finalization();
        byte[] bytes = B10SweepEvidenceCodec.WriteFinalization(value);
        Assert.Equal(bytes, B10SweepEvidenceCodec.WriteFinalization(B10SweepEvidenceCodec.ReadFinalization(bytes, Limits)));
    }

    [Fact]
    public void Given_RoundTripUtcZSuffix_When_Read_Then_AcceptsUtcLifecycleTimes()
    {
        string json = Encoding.UTF8.GetString(B10SweepEvidenceCodec.WriteSweepLine(Line())).Replace("+00:00", "Z", StringComparison.Ordinal);
        B10SweepLine value = B10SweepEvidenceCodec.ReadSweepLine(Encoding.UTF8.GetBytes(json));
        Assert.Equal(Epoch.AddSeconds(2), value.CapturedAt);
        Assert.Equal(Epoch.AddSeconds(1), value.LastVerifierCompletedUtc);
        Assert.Equal(Epoch.AddSeconds(1), value.LastLoadgenTerminatedUtc);
    }

    [Theory]
    [InlineData("schema_version")] [InlineData("scenario_id")] [InlineData("configuration_id")] [InlineData("run_id")]
    [InlineData("phase")] [InlineData("source_commit")] [InlineData("topic")] [InlineData("partition_highs")]
    [InlineData("captured_at")] [InlineData("last_verifier_completed_utc")] [InlineData("last_loadgen_terminated_utc")] [InlineData("settle_interval_ms")]
    public void Given_RequiredSweepMember_When_MissingOrDuplicated_Then_Rejects(string name)
    {
        JsonObject root = JsonNode.Parse(B10SweepEvidenceCodec.WriteSweepLine(Line()))!.AsObject();
        string duplicate = root.ToJsonString().Insert(1, $"\"{name}\":{root[name]!.ToJsonString()},");
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadSweepLine(Encoding.UTF8.GetBytes(duplicate)));
        root.Remove(name); Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadSweepLine(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Theory]
    [InlineData("schema_version", "2")] [InlineData("schema_version", "1.5")]
    [InlineData("scenario_id", "\"A01\"")] [InlineData("configuration_id", "\"C1\"")]
    [InlineData("run_id", "\"BAD\"")] [InlineData("phase", "\"probe\"")]
    [InlineData("source_commit", "\"bad\"")] [InlineData("topic", "\"other\"")]
    [InlineData("captured_at", "\"2026-11-05T00:00:02+01:00\"")]
    [InlineData("settle_interval_ms", "0")] [InlineData("settle_interval_ms", "-1")]
    [InlineData("settle_interval_ms", "2147483648")] [InlineData("settle_interval_ms", "1000.1")]
    [InlineData("unknown", "1")]
    public void Given_InvalidSweepField_When_Read_Then_Rejects(string name, string json)
    {
        JsonObject root = JsonNode.Parse(B10SweepEvidenceCodec.WriteSweepLine(Line()))!.AsObject(); root[name] = JsonNode.Parse(json);
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadSweepLine(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Theory]
    [InlineData("[]")] [InlineData("[{\"partition\":0,\"high\":1},{\"partition\":0,\"high\":2}]")]
    [InlineData("[{\"partition\":-1,\"high\":1}]")] [InlineData("[{\"partition\":0,\"high\":-1}]")]
    [InlineData("[{\"partition\":0}]")] [InlineData("[{\"partition\":0,\"partition\":1,\"high\":1}]")]
    [InlineData("[{\"partition\":0,\"high\":1,\"extra\":0}]")]
    [InlineData("[{\"partition\":0.1,\"high\":1}]")] [InlineData("[{\"partition\":0,\"high\":9223372036854775808}]")]
    public void Given_InvalidPartitionVector_When_Read_Then_Rejects(string vector)
    {
        string json = Encoding.UTF8.GetString(B10SweepEvidenceCodec.WriteSweepLine(Line()));
        json = json.Replace("[{\"partition\":0,\"high\":10}]", vector, StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadSweepLine(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(1, true)]
    public void Given_SettleBoundary_When_CaptureVaries_Then_UsesInclusiveElapsedTime(int delta, bool accepted)
    {
        B10SweepLine line = Line() with { CapturedAt = Epoch.AddMilliseconds(2000 + delta) };
        Assert.Equal(accepted, B10SweepEvidenceCodec.MatchesSweepLine(line, Options(), Epoch.AddSeconds(3), [Epoch.AddSeconds(1)]));
    }

    [Theory]
    [InlineData(999, false)] [InlineData(1000, true)] [InlineData(1001, false)]
    public void Given_ExternallyApprovedSettle_When_NoteIntervalVaries_Then_RequiresExactMatch(long interval, bool accepted)
    {
        B10SweepLine line = Line() with { CapturedAt = Epoch.AddSeconds(3), SettleIntervalMilliseconds = interval };
        Assert.Equal(accepted, B10SweepEvidenceCodec.MatchesSweepLine(line, Options(), Epoch.AddSeconds(3), [Epoch.AddSeconds(1)]));
    }

    [Theory]
    [InlineData(-1, true)] [InlineData(0, true)] [InlineData(1, false)]
    public void Given_VerifierCompletionBoundary_When_ObservedCompletionVaries_Then_NoteMustCoverEveryReceipt(int delta, bool accepted) =>
        Assert.Equal(accepted, B10SweepEvidenceCodec.MatchesSweepLine(Line(), Options(), Epoch.AddSeconds(3), [Epoch.AddMilliseconds(1000 + delta)]));

    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(1, true)]
    public void Given_WorkerStartBoundary_When_StartVaries_Then_CaptureMustPrecedeIt(int delta, bool accepted) =>
        Assert.Equal(accepted, B10SweepEvidenceCodec.MatchesSweepLine(Line(), Options(), Epoch.AddMilliseconds(2000 + delta), []));

    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(1, true)]
    public void Given_InputOutputByteBoundary_When_LimitsVary_Then_EnforcesExactSize(int delta, bool accepted)
    {
        byte[] bytes = B10SweepEvidenceCodec.WriteSweepLine(Line());
        if (accepted)
        { Assert.NotNull(B10SweepEvidenceCodec.ReadSweepLine(bytes, Limits with { MaxArtifactBytes = bytes.Length + delta }));
            Assert.NotEmpty(B10SweepEvidenceCodec.WriteSweepLine(Line(), new(bytes.Length + delta, 100, 256))); }
        else
        { Assert.Throws<InputLimitExceededException>(() => B10SweepEvidenceCodec.ReadSweepLine(bytes, Limits with { MaxArtifactBytes = bytes.Length + delta }));
            Assert.Throws<InputLimitExceededException>(() => B10SweepEvidenceCodec.WriteSweepLine(Line(), new(bytes.Length + delta, 100, 256))); }
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, true)] [InlineData(2, true)]
    public void Given_PartitionLimit_When_LimitVaries_Then_EnforcesBound(int limit, bool accepted)
    {
        byte[] bytes = B10SweepEvidenceCodec.WriteSweepLine(Line());
        if (accepted) Assert.NotNull(B10SweepEvidenceCodec.ReadSweepLine(bytes, Limits, limit));
        else Assert.Throws<InputLimitExceededException>(() => B10SweepEvidenceCodec.ReadSweepLine(bytes, Limits, limit));
    }

    [Theory]
    [InlineData(2, false)] [InlineData(3, true)] [InlineData(4, true)]
    public void Given_DepthLimit_When_LimitVaries_Then_EnforcesBound(int depth, bool accepted)
    {
        byte[] bytes = B10SweepEvidenceCodec.WriteSweepLine(Line());
        if (accepted) Assert.NotNull(B10SweepEvidenceCodec.ReadSweepLine(bytes, Limits with { MaxDepth = depth }));
        else Assert.Throws<InputLimitExceededException>(() => B10SweepEvidenceCodec.ReadSweepLine(bytes, Limits with { MaxDepth = depth }));
    }

    [Theory]
    [InlineData("status", "\"incomplete\"")] [InlineData("classification_final", "false")]
    [InlineData("counts", "null")] [InlineData("result", "null")] [InlineData("verify_receipt", "null")]
    [InlineData("repeat", "2")] [InlineData("reasons", "[\"z\",\"a\"]")]
    [InlineData("reasons", "[\"a\",\"a\"]")] [InlineData("unknown", "0")]
    public void Given_InconsistentFinalRepeat_When_Read_Then_Rejects(string name, string json)
    {
        JsonObject root = JsonNode.Parse(B10SweepEvidenceCodec.WriteFinalization(Finalization()))!.AsObject();
        root["repeats"]![0]![name] = JsonNode.Parse(json);
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Theory]
    [InlineData("scan_complete", "false")] [InlineData("sweep_line", "null")]
    [InlineData("residual_risk", "\"wrong\"")] [InlineData("repeats", "[]")]
    [InlineData("scenario_id", "\"A01\"")] [InlineData("unknown", "0")]
    public void Given_InconsistentFinalRoot_When_Read_Then_Rejects(string name, string json)
    {
        JsonObject root = JsonNode.Parse(B10SweepEvidenceCodec.WriteFinalization(Finalization()))!.AsObject(); root[name] = JsonNode.Parse(json);
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Theory]
    [InlineData("schema_version")] [InlineData("scenario_id")] [InlineData("configuration_id")]
    [InlineData("run_id")] [InlineData("phase")] [InlineData("source_commit")] [InlineData("sweep_line")]
    [InlineData("scan_complete")] [InlineData("residual_risk")] [InlineData("repeats")]
    public void Given_RequiredFinalizationMember_When_MissingOrDuplicated_Then_Rejects(string name)
    {
        JsonObject root = JsonNode.Parse(B10SweepEvidenceCodec.WriteFinalization(Finalization()))!.AsObject();
        string duplicate = root.ToJsonString().Insert(1, $"\"{name}\":{root[name]!.ToJsonString()},");
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(Encoding.UTF8.GetBytes(duplicate)));
        root.Remove(name);
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Theory]
    [InlineData("repeat")] [InlineData("result")] [InlineData("verify_receipt")] [InlineData("classification_final")]
    [InlineData("status")] [InlineData("counts")] [InlineData("reasons")]
    public void Given_RequiredFinalRepeatMember_When_MissingOrDuplicated_Then_Rejects(string name)
    {
        JsonObject root = JsonNode.Parse(B10SweepEvidenceCodec.WriteFinalization(Finalization()))!.AsObject();
        JsonObject repeat = root["repeats"]![0]!.AsObject();
        string before = repeat.ToJsonString(); string duplicate = before.Insert(1, $"\"{name}\":{repeat[name]!.ToJsonString()},");
        string json = root.ToJsonString().Replace(before, duplicate, StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(Encoding.UTF8.GetBytes(json)));
        repeat.Remove(name);
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Theory]
    [InlineData("sweep_line", "container", "\"ledger\"")] [InlineData("sweep_line", "name", "\"elsewhere\"")]
    [InlineData("result", "name", "\"synthetic-c5/C0/r2/result.json\"")]
    [InlineData("verify_receipt", "name", "\"synthetic-c5/C0/r2/verify-completion.json\"")]
    [InlineData("result", "sha256", "\"bad\"")] [InlineData("result", "length", "-1")]
    [InlineData("result", "length", "1.5")] [InlineData("result", "unknown", "0")]
    public void Given_ReferenceSubstitution_When_Read_Then_Rejects(string target, string field, string json)
    {
        JsonObject root = JsonNode.Parse(B10SweepEvidenceCodec.WriteFinalization(Finalization()))!.AsObject();
        JsonNode reference = target == "sweep_line" ? root[target]! : root["repeats"]![0]![target]!;
        reference[field] = JsonNode.Parse(json);
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Theory]
    [InlineData(99, true)] [InlineData(100, true)] [InlineData(101, false)]
    public void Given_ReasonCountBoundary_When_Written_Then_EnforcesBound(int count, bool accepted)
    {
        B10Finalization value = Finalization(); B10RepeatFinalization[] repeats = value.Repeats.ToArray();
        repeats[0] = repeats[0] with { Reasons = Enumerable.Range(0, count).Select(i => $"reason-{i:D3}").ToArray() };
        value = value with { Repeats = repeats };
        if (accepted) Assert.NotEmpty(B10SweepEvidenceCodec.WriteFinalization(value));
        else Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.WriteFinalization(value));
    }

    [Theory]
    [InlineData(255, true)] [InlineData(256, true)] [InlineData(257, false)]
    public void Given_ReasonLengthBoundary_When_Written_Then_EnforcesBound(int length, bool accepted)
    {
        B10Finalization value = Finalization(); B10RepeatFinalization[] repeats = value.Repeats.ToArray();
        repeats[0] = repeats[0] with { Reasons = [new string('r', length)] }; value = value with { Repeats = repeats };
        if (accepted) Assert.NotEmpty(B10SweepEvidenceCodec.WriteFinalization(value));
        else Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.WriteFinalization(value));
    }

    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(1, true)]
    public void Given_CountBoundary_When_Written_Then_RejectsNegativeCounts(long count, bool accepted)
    {
        B10Finalization value = Finalization(); B10RepeatFinalization[] repeats = value.Repeats.ToArray();
        repeats[0] = repeats[0] with { Counts = new(count, 0, 0, 0, 0, 0) }; value = value with { Repeats = repeats };
        if (accepted) Assert.NotEmpty(B10SweepEvidenceCodec.WriteFinalization(value));
        else Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.WriteFinalization(value));
    }

    [Theory]
    [InlineData(65535, true)] [InlineData(65536, true)] [InlineData(65537, false)]
    public void Given_HardInputByteBoundary_When_Read_Then_EnforcesCodecCeiling(int length, bool accepted)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(B10SweepEvidenceCodec.WriteSweepLine(Line())).PadRight(length));
        InputLimits generous = Limits with { MaxArtifactBytes = 100000, MaxTotalInputBytes = 100000 };
        if (accepted) Assert.NotNull(B10SweepEvidenceCodec.ReadSweepLine(bytes, generous));
        else Assert.Throws<InputLimitExceededException>(() => B10SweepEvidenceCodec.ReadSweepLine(bytes, generous));
    }

    [Theory]
    [InlineData(-1, true)] [InlineData(0, true)] [InlineData(1, false)]
    public void Given_LifecycleCaptureBoundary_When_VerifierTimeVaries_Then_RejectsFutureLifecycle(int delta, bool accepted)
    {
        B10SweepLine value = Line() with { LastVerifierCompletedUtc = Epoch.AddMilliseconds(2000 + delta) };
        Assert.Equal(accepted, B10SweepEvidenceCodec.MatchesSweepLine(value, Options(), Epoch.AddSeconds(3), []));
    }

    [Theory]
    [InlineData("scenario")] [InlineData("configuration")] [InlineData("run")]
    [InlineData("phase")] [InlineData("source")]
    public void Given_OptionsIdentityMismatch_When_MatchingSweep_Then_Rejects(string field)
    {
        VerifierHostOptions options = Options();
        switch (field) { case "scenario": options.ScenarioId = "A01"; break; case "configuration": options.ConfigurationId = "C1"; break;
            case "run": options.RunId = "another-run"; break; case "phase": options.Phase = "C1"; break; default: options.SourceCommit = new string('b', 40); break; }
        Assert.False(B10SweepEvidenceCodec.MatchesSweepLine(Line(), options, Epoch.AddSeconds(3), []));
    }

    [Fact]
    public void Given_FailedScan_When_RoundTripped_Then_AllSlotsRemainInvalidWithoutCounts()
    {
        B10Finalization failed = Finalization() with { ScanComplete = false, SweepLine = null,
            Repeats = Finalization().Repeats.Select(r => r with { Status = VerifierResultStatus.Invalid, ClassificationFinal = false, Counts = null }).ToArray() };
        B10Finalization actual = B10SweepEvidenceCodec.ReadFinalization(B10SweepEvidenceCodec.WriteFinalization(failed));
        Assert.False(actual.ScanComplete); Assert.Null(actual.SweepLine);
        Assert.All(actual.Repeats, r => { Assert.Equal(VerifierResultStatus.Invalid, r.Status); Assert.False(r.ClassificationFinal); Assert.Null(r.Counts); });
    }
}
#pragma warning restore CA1707
