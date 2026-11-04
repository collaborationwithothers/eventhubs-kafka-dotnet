using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Window;

#pragma warning disable CA1707

// Both producers share one window; the note binds their expected Jobs independently of their pod identities.
public sealed class B10RunWindowTests
{
    [Fact]
    public void Given_B10Note_When_RoundTripping_Then_PreservesOrderedJobsAndFirstJobIdentity()
    {
        byte[] bytes = StartLineNoteCodec.Write(Note(), OutputLimits);
        JsonObject json = JsonNode.Parse(bytes)!.AsObject();
        Assert.Equal(Jobs, json["expected_job_names"]!.AsArray().Select(n => n!.GetValue<string>()));
        ParseResult<StartLineNote> parsed = Read(bytes);
        Assert.True(parsed.IsSuccess);
        Assert.Equal(StartIdentity, parsed.Value!.Identity);
        Assert.Equal(Jobs, parsed.Value.ExpectedJobNames);
        RunWindow window = Create(parsed, new Dictionary<int, long> { [0] = 6 });
        Assert.True(window.CanAssign);
        Assert.Equal(Identities, window.ProducerIdentities);
        Assert.Equal([new PartitionWindow(0, 5, 6)], window.Partitions);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("one")]
    [InlineData("three")]
    [InlineData("duplicate")]
    [InlineData("reversed")]
    [InlineData("foreign")]
    [InlineData("case")]
    public void Given_ReadableWrongExpectedJobs_When_Creating_Then_SevenFailsBeforeEndChecks(string kind)
    {
        JsonObject json = Json();
        json["expected_job_names"] = kind switch
        {
            "empty" => new JsonArray(),
            "one" => new JsonArray(Jobs[0]),
            "three" => new JsonArray(Jobs[0], Jobs[1], "foreign"),
            "duplicate" => new JsonArray(Jobs[0], Jobs[0]),
            "reversed" => new JsonArray(Jobs[1], Jobs[0]),
            "foreign" => new JsonArray(Jobs[0], "foreign"),
            "case" => new JsonArray(Jobs[0].ToUpperInvariant(), Jobs[1]),
            _ => null
        };
        if (kind == "missing") Assert.True(json.Remove("expected_job_names"));
        ParseResult<StartLineNote> parsed = Read(Bytes(json));
        Assert.True(parsed.IsSuccess);
        RunWindow window = Create(parsed, new Dictionary<int, long>());
        Assert.False(window.CanAssign);
        Assert.Empty(window.Partitions);
        Assert.Equal(Identities, window.ProducerIdentities);
        RunWindowTests.AssertSemanticFailure(Assert.Single(window.Assessments), 7, "expected_job_names");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("0")]
    [InlineData("true")]
    [InlineData("\"job\"")]
    [InlineData("[null]")]
    [InlineData("[0]")]
    [InlineData("[true]")]
    [InlineData("[{}]")]
    [InlineData("[[]]")]
    public void Given_MalformedExpectedJobs_When_Reading_Then_StructuralSevenRetainsField(string value)
    {
        JsonObject json = Json();
        json["expected_job_names"] = JsonNode.Parse(value);
        ParseResult<StartLineNote> parsed = Read(Bytes(json));
        Assert.False(parsed.IsSuccess);
        Assert.Equal(new StructuralProblem(7, "wrong_json_kind", "start-line", null,
            "expected_job_names", null), Assert.Single(parsed.Problems));
        RunWindow window = Create(parsed, new Dictionary<int, long> { [0] = 6 });
        Assert.False(window.CanAssign);
        Assert.Equal(Identities, window.ProducerIdentities);
    }

    [Theory]
    [InlineData("schema_version")]
    [InlineData("identity")]
    [InlineData("topic")]
    [InlineData("partition_highs")]
    [InlineData("captured_at")]
    [InlineData("verifier_commit")]
    [InlineData("confluent_kafka_version")]
    [InlineData("librdkafka_version")]
    public void Given_MissingOriginalField_When_ReadingB10_Then_FieldRemainsRequired(string field)
    {
        JsonObject json = Json();
        Assert.True(json.Remove(field));
        ParseResult<StartLineNote> parsed = Read(Bytes(json));
        Assert.False(parsed.IsSuccess);
        Assert.Equal("missing_property", Assert.Single(parsed.Problems).Code);
    }

    [Fact]
    public void Given_DuplicateExpectedJobsProperty_When_Reading_Then_StrictParserRejects()
    {
        string valid = Encoding.UTF8.GetString(StartLineNoteCodec.Write(Note(), OutputLimits));
        ParseResult<StartLineNote> parsed = Read(Encoding.UTF8.GetBytes(valid[..^1] + ",\"expected_job_names\":[]}"));
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Problems, p => p.CheckNumber == 7 && p.Code == "duplicate_property");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Given_B10WriterWithoutExactlyTwoNames_When_Writing_Then_Rejects(int count)
    {
        StartLineNote note = Note() with { ExpectedJobNames = count < 0 ? null : Enumerable.Repeat("job", count).ToArray() };
        Assert.Throws<ArgumentException>(() => StartLineNoteCodec.Write(note, OutputLimits));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(252, true)]
    [InlineData(253, true)]
    [InlineData(254, false)]
    public void Given_JobNameLength_When_Writing_Then_UsesPhaseTextBounds(int length, bool valid)
    {
        StartLineNote note = Note() with { ExpectedJobNames = [new string('a', length), Jobs[1]] };
        if (valid) Assert.True(Read(StartLineNoteCodec.Write(note, OutputLimits)).IsSuccess);
        else Assert.Throws<ArgumentException>(() => StartLineNoteCodec.Write(note, OutputLimits));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("job/name")]
    [InlineData("job\\name")]
    [InlineData("job\nname")]
    [InlineData("job\u007fname")]
    public void Given_JobNameOutsidePhaseTextPredicate_When_Writing_Then_Rejects(string name) =>
        Assert.Throws<ArgumentException>(() => StartLineNoteCodec.Write(Note() with { ExpectedJobNames = [Jobs[0], name] }, OutputLimits));

    [Fact]
    public void Given_NonB10Note_When_Writing_Then_OmitsExtensionAndSingletonRetainsIdentity()
    {
        byte[] bytes = StartLineNoteCodec.Write(RunWindowTests.Note(), OutputLimits);
        Assert.False(JsonNode.Parse(bytes)!.AsObject().ContainsKey("expected_job_names"));
        ParseResult<StartLineNote> parsed = Read(bytes);
        Assert.True(parsed.IsSuccess);
        Assert.Null(parsed.Value!.ExpectedJobNames);
        RunWindow window = RunWindow.Create(parsed, RunWindowTests.Identity, new Dictionary<int, long> { [0] = 6 });
        Assert.True(window.CanAssign);
        Assert.Equal([RunWindowTests.Identity], window.ProducerIdentities);
        Assert.Throws<ArgumentException>(() => StartLineNoteCodec.Write(RunWindowTests.Note() with { ExpectedJobNames = Jobs }, OutputLimits));
    }

    [Fact]
    public void Given_NonB10IdentityWithExtension_When_Reading_Then_RejectsForeignScenarioField()
    {
        JsonObject json = Json();
        json["identity"]!["scenario_id"] = "A01";
        ParseResult<StartLineNote> parsed = Read(Bytes(json));
        Assert.False(parsed.IsSuccess);
        Assert.Equal(new StructuralProblem(7, "unknown_property", "start-line", null,
            "expected_job_names", null), Assert.Single(parsed.Problems));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("empty-problems")]
    [InlineData("identity")]
    [InlineData("no-ends")]
    [InlineData("lost-partition")]
    [InlineData("regression")]
    [InlineData("valid")]
    public void Given_EachReturnPath_When_Creating_Then_CopiesOrderedProducerIdentities(string path)
    {
        RunIdentity[] identities = Identities.ToArray();
        ParseResult<StartLineNote> parsed = path switch
        {
            "malformed" => Read("{}"u8.ToArray()),
            "empty-problems" => ParseResult<StartLineNote>.Failure(Array.Empty<StructuralProblem>()),
            "identity" => ParseResult<StartLineNote>.Success(Note() with { Identity = StartIdentity with { RunId = "other" } }),
            _ => ParseResult<StartLineNote>.Success(Note())
        };
        IReadOnlyDictionary<int, long>? ends = path switch
        {
            "no-ends" => null,
            "lost-partition" => new Dictionary<int, long>(),
            "regression" => new Dictionary<int, long> { [0] = 4 },
            _ => new Dictionary<int, long> { [0] = 6 }
        };
        RunWindow window = RunWindow.Create(parsed, identities, StartIdentity, Jobs, ends);
        Assert.Equal(Identities, window.ProducerIdentities);
        Assert.NotSame(identities, window.ProducerIdentities);
        identities[0] = identities[0] with { InstanceId = "changed" };
        Assert.Equal(Identities, window.ProducerIdentities);
        Assert.Equal(path == "valid", window.CanAssign);
        if (path == "lost-partition") RunWindowTests.AssertSemanticFailure(Assert.Single(window.Assessments, a => a.CheckNumber == 8), 8, "partition_highs");
        if (path == "regression") RunWindowTests.AssertSemanticFailure(Assert.Single(window.Assessments, a => a.CheckNumber == 9), 9, "partition_highs");
    }

    [Fact]
    public void Given_IdentityAndJobDisagreement_When_Creating_Then_RetainsBothFailuresBeforeWindowChecks()
    {
        StartLineNote note = Note() with { Identity = StartIdentity with { RunId = "other" }, ExpectedJobNames = [] };
        RunWindow window = Create(ParseResult<StartLineNote>.Success(note), new Dictionary<int, long>());
        Assert.False(window.CanAssign);
        Assert.Equal(["identity", "expected_job_names"], window.Assessments.Select(a => a.Field));
        Assert.All(window.Assessments, a => Assert.Equal(7, a.CheckNumber));
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void Given_EndAroundStart_When_CreatingB10_Then_KeepsExistingHalfOpenBoundaries(long end, bool canAssign)
    {
        RunWindow window = Create(ParseResult<StartLineNote>.Success(Note()), new Dictionary<int, long> { [0] = end, [1] = 0 });
        Assert.Equal(canAssign, window.CanAssign);
        Assert.Equal([new PartitionWindow(0, 5, end), new PartitionWindow(1, 0, 0)], window.Partitions);
        Assert.Equal([new PartitionHigh(0, 5)], window.RecordedStartHighs);
        Assert.Equal(Identities, window.ProducerIdentities);
    }

    private static readonly OutputLimits OutputLimits = new(4096, 4, 256);
    private static readonly string[] Jobs = [PhaseIdentity.JobName("synthetic-c5", "C0", 1), PhaseIdentity.RestartJobName("synthetic-c5", "C0", 1)];
    private static readonly RunIdentity StartIdentity = new("B10", "C0", "synthetic-c5", Jobs[0], Jobs[0], Jobs[0]);
    private static readonly RunIdentity[] Identities =
    [
        new("B10", "C0", "synthetic-c5", "pod-first", "pod-first", "pod-first"),
        new("B10", "C0", "synthetic-c5", "pod-second", "pod-second", "pod-second")
    ];

    private static StartLineNote Note() => new(1, StartIdentity, "document-events", [new(0, 5)],
        "synthetic", "commit", "version", "version", Jobs);
    private static JsonObject Json() => JsonNode.Parse(StartLineNoteCodec.Write(Note(), OutputLimits))!.AsObject();
    private static byte[] Bytes(JsonObject json) => Encoding.UTF8.GetBytes(json.ToJsonString());
    private static ParseResult<StartLineNote> Read(byte[] bytes) => StartLineNoteCodec.Read(bytes, "start-line", RecordedEvidence.SmallLimits, 10);
    private static RunWindow Create(ParseResult<StartLineNote> parsed, IReadOnlyDictionary<int, long> ends) =>
        RunWindow.Create(parsed, Identities, StartIdentity, Jobs, ends);
}
