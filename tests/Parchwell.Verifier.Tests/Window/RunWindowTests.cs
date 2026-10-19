using System.Text;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Window;

#pragma warning disable CA1707

public sealed class RunWindowTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schema_version\":2}")]
    public void Given_UnreadableNote_When_Creating_Then_CheckSevenFails(string json)
    {
        var parsed = StartLineNoteCodec.Read(Encoding.UTF8.GetBytes(json), "start-line", RecordedEvidence.SmallLimits, 10);
        AssertFailure(RunWindow.Create(parsed, Identity, new Dictionary<int, long> { [0] = 6 }), 7);
    }

    [Fact]
    public void Given_MissingNote_When_Creating_Then_CheckSevenFails() => AssertFailure(RunWindow.Create(
        ParseResult<StartLineNote>.Failure(new StructuralProblem(7, "missing", "start-line", null, null, null)),
        Identity, new Dictionary<int, long> { [0] = 6 }), 7);

    [Fact]
    public void Given_UnsupportedSchemaWithAllFields_When_Reading_Then_CheckSevenFails()
    {
        byte[] valid = StartLineNoteCodec.Write(Note(), new OutputLimits(4096, 4, 256));
        byte[] unsupported = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(valid).Replace("\"schema_version\":1", "\"schema_version\":2", StringComparison.Ordinal));
        var parsed = StartLineNoteCodec.Read(unsupported, "start-line", RecordedEvidence.SmallLimits, 10);
        Assert.Contains(parsed.Problems, p => p.Code == "unsupported_schema_version");
        AssertFailure(RunWindow.Create(parsed, Identity), 7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Given_ForeignIdentityField_When_Creating_Then_CheckSevenFails(int field)
    {
        RunIdentity identity = field switch
        {
            0 => Identity with { ScenarioId = "foreign" },
            1 => Identity with { ConfigurationId = "foreign" },
            2 => Identity with { RunId = "foreign" },
            3 => Identity with { InstanceId = "foreign" },
            4 => Identity with { ExpectedInstanceId = "foreign" },
            _ => Identity with { ClientId = "foreign" }
        };
        AssertFailure(RunWindow.Create(ParseResult<StartLineNote>.Success(Note() with { Identity = identity }),
            Identity, new Dictionary<int, long> { [0] = 6 }), 7);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("negative")]
    [InlineData("topic")]
    public void Given_InvalidStartBoundaries_When_Creating_Then_CheckSevenFailsBeforeAssignments(string kind)
    {
        StartLineNote note = kind switch
        {
            "empty" => Note() with { PartitionHighs = [] },
            "duplicate" => Note() with { PartitionHighs = [new(0, 5), new(0, 6)] },
            "negative" => Note() with { PartitionHighs = [new(0, -1)] },
            _ => Note() with { Topic = "other-topic" }
        };
        AssertFailure(RunWindow.Create(ParseResult<StartLineNote>.Success(note), Identity,
            new Dictionary<int, long> { [0] = 6 }), 7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Given_NonnegativeStart_When_Creating_Then_CheckSevenPasses(long start)
    {
        RunWindow window = Create([new(0, start)], new Dictionary<int, long> { [0] = 1 });
        Assert.True(window.CanAssign);
        Assert.Contains(window.Assessments, a => a.CheckNumber == 7 && a.Disposition == AssessmentDisposition.Pass);
    }

    [Fact]
    public void Given_LostStartPartition_When_Creating_Then_CheckEightFails() =>
        AssertFailure(Create([new(0, 5), new(1, 8)], new Dictionary<int, long> { [0] = 6 }), 8);

    [Fact]
    public void Given_EmptyEndHighs_When_Creating_Then_AllLostStartPartitionsFailEight() =>
        AssertFailure(Create([new(0, 5)], new Dictionary<int, long>()), 8);

    [Fact]
    public void Given_EndOnlyPartition_When_Creating_Then_NormalizesStartWithoutChangingRecordedStart()
    {
        RunWindow window = Create([new(0, 5)], new Dictionary<int, long> { [0] = 6, [1] = 2 });
        Assert.True(window.CanAssign);
        Assert.Equal([new PartitionWindow(0, 5, 6), new PartitionWindow(1, 0, 2)], window.Partitions);
        Assert.Equal([new PartitionHigh(0, 5)], window.RecordedStartHighs);
        Assert.Contains(window.Assessments, a => a.CheckNumber == 8 && a.Disposition == AssessmentDisposition.Pass);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void Given_EndAroundStart_When_Creating_Then_OnlyRegressionFailsNine(long end, bool canAssign)
    {
        RunWindow window = Create([new(0, 5)], new Dictionary<int, long> { [0] = end });
        Assert.Equal(canAssign, window.CanAssign);
        if (!canAssign) AssertFailure(window, 9);
        else Assert.DoesNotContain(window.Assessments, a => a.CheckNumber == 9);
    }

    [Fact]
    public void Given_UnavailableEnd_When_Creating_Then_OnlyCheckSevenIsEvaluated()
    {
        RunWindow window = RunWindow.Create(ParseResult<StartLineNote>.Success(Note()), Identity);
        Assert.False(window.CanAssign);
        Assert.Equal(7, Assert.Single(window.Assessments).CheckNumber);
        Assert.Empty(window.EndHighs);
        Assert.Empty(window.Partitions);
    }

    [Fact]
    public void Given_ExecutionParseFailure_When_Creating_Then_DoesNotConvertToNumberedFailure() =>
        Assert.Throws<InvalidOperationException>(() => RunWindow.Create(ParseResult<StartLineNote>.ExecutionFailure(
            new StructuralProblem(null, "io", "start-line", null, null, null)), Identity));

    [Fact]
    public void Given_ParserPartitionLimit_When_Reading_Then_RemainsExecutionFailure()
    {
        byte[] bytes = StartLineNoteCodec.Write(Note(), new OutputLimits(4096, 4, 256));
        Assert.Throws<InputLimitExceededException>(() => StartLineNoteCodec.Read(bytes, "start-line", RecordedEvidence.SmallLimits, 0));
    }

    [Fact]
    public void Given_NegativePartitionId_When_Creating_Then_NoUnapprovedRangeCheckIsAdded() =>
        Assert.True(Create([new(-1, 0)], new Dictionary<int, long> { [-1] = 0 }).CanAssign);

    internal static RunIdentity Identity { get; } = new("A01", "C0", "run", "instance", "instance", "client");
    internal static StartLineNote Note() => new(1, Identity, "document-events", [new(0, 5)], "synthetic", "commit", "version", "version");
    internal static RunWindow Create(IReadOnlyList<PartitionHigh> starts, IReadOnlyDictionary<int, long> ends) =>
        RunWindow.Create(ParseResult<StartLineNote>.Success(Note() with { PartitionHighs = starts }), Identity, ends);

    private static void AssertFailure(RunWindow window, int check)
    {
        Assert.False(window.CanAssign);
        Assert.Contains(window.Assessments, a => a.CheckNumber == check && a.Disposition == AssessmentDisposition.Fail &&
            a.Classification == AssessmentClassification.Invalid);
    }
}
