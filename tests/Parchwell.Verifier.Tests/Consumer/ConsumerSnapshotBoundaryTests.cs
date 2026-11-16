using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Consumer;
using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707
// Snapshot syntax and assignment validity are separate checks; valid empty-window evidence isolates them.
public sealed class ConsumerSnapshotBoundaryTests
{
    private static readonly string[] AssignmentInvalidReason = ["consumer_start_assignment_invalid"];
    [Theory]
    [InlineData(-2, false, false)]
    [InlineData(-1, true, false)]
    [InlineData(0, true, true)]
    public async Task Given_FirstSequenceAtLowerBoundary_U5_When_ReadAndJudged_Then_SyntaxAndPriorAssignmentAreEnforced(long first, bool codecValid, bool complete)
    {
        JsonObject snapshot = Snapshot();
        snapshot["members"]![1]!["first_read_last_row_sequence"] = first;
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);

        if (codecValid)
        {
            ConsumerStartSnapshot parsed = ConsumerArtifactCodec.ReadSnapshot(bytes);
            Assert.Equal(first, parsed.Members[1].FirstReadLastRowSequence);
            Assert.Equal(0, parsed.Members[1].SecondReadLastRowSequence);
        }
        else Assert.Throws<FormatException>(() => ConsumerArtifactCodec.ReadSnapshot(bytes));

        await AssertExtension(bytes, complete);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task Given_SecondSequenceAroundFirst_U5_When_ReadAndJudged_Then_OnlyReversedSequenceIsInvalid(long second, bool complete)
    {
        JsonObject snapshot = Snapshot();
        snapshot["members"]![1]!["first_read_last_row_sequence"] = 1;
        snapshot["members"]![1]!["second_read_last_row_sequence"] = second;
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);

        if (complete)
        {
            ConsumerStartSnapshot parsed = ConsumerArtifactCodec.ReadSnapshot(bytes);
            Assert.Equal(1, parsed.Members[1].FirstReadLastRowSequence);
            Assert.Equal(second, parsed.Members[1].SecondReadLastRowSequence);
        }
        else Assert.Throws<FormatException>(() => ConsumerArtifactCodec.ReadSnapshot(bytes));

        await AssertExtension(bytes, complete, withLaterRows: true);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("out_of_order")]
    [InlineData("empty_bytes")]
    [InlineData("malformed_json")]
    public async Task Given_MalformedSnapshotBytes_U5_When_ReadAndJudged_Then_AssignmentInvalidityIsRetained(string fault)
    {
        JsonObject snapshot = Snapshot();
        if (fault == "duplicate") snapshot["members"]![1]!["instance_id"] = "subject";
        if (fault == "out_of_order")
        {
            snapshot["members"] = new JsonArray(Member("witness", 0, 0), Member("subject", 0, 0));
        }
        byte[] bytes = fault switch
        {
            "empty_bytes" => [],
            "malformed_json" => Encoding.UTF8.GetBytes("{"),
            _ => JsonSerializer.SerializeToUtf8Bytes(snapshot)
        };

        Assert.Throws<FormatException>(() => ConsumerArtifactCodec.ReadSnapshot(bytes));
        await AssertExtension(bytes, false);
    }

    [Fact]
    public async Task Given_EmptySnapshotMembers_U5_When_ReadAndJudged_Then_ValidSyntaxStillLacksRegisteredMembers()
    {
        JsonObject snapshot = Snapshot();
        snapshot["members"] = new JsonArray();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);

        Assert.Empty(ConsumerArtifactCodec.ReadSnapshot(bytes).Members);
        await AssertExtension(bytes, false);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Given_SnapshotMemberCountBoundary_U5_When_ReadAndJudged_Then_OnlyRegisteredPairIsValid(int count)
    {
        JsonObject snapshot = Snapshot();
        JsonObject extra = Member("subject", 0, 0);
        extra["instance_id"] = "a-extra";
        snapshot["members"] = count switch
        {
            1 => new JsonArray(Member("subject", 0, 0)),
            2 => new JsonArray(Member("subject", 0, 0), Member("witness", 0, 0)),
            _ => new JsonArray(extra, Member("subject", 0, 0), Member("witness", 0, 0))
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);

        Assert.Equal(count, ConsumerArtifactCodec.ReadSnapshot(bytes).Members.Count);
        // Exactly matching the two registered IDs also implies count two; the codec permits other sorted sets.
        await AssertExtension(bytes, count == 2);
    }

    private static async Task AssertExtension(byte[] bytes, bool complete, bool withLaterRows = false)
    {
        var clock = new ManualKafkaTimeProvider();
        var store = new ConsumerWireFixture { Clock = clock };
        store.Blobs[ConsumerWireFixture.Prefix + "consumer-start.json"] = bytes;
        if (withLaterRows)
        {
            JsonObject firstCommit = ConsumerWireFixture.Commit("witness", 1);
            firstCommit["offsets"] = new JsonArray();
            JsonObject secondCommit = ConsumerWireFixture.Commit("witness", 2);
            secondCommit["offsets"] = new JsonArray();
            store.SetRows("witness", [ConsumerWireFixture.Assignment("witness", []), firstCommit, secondCommit]);
            store.Set("witness", "completion.json", ConsumerWireFixture.Completion("witness", 3, 1, 0));
        }
        var options = new VerifierHostOptions
        {
            RunId = "synthetic-consumer", ScenarioId = "C03", ConfigurationId = "C0", Phase = "C0", Repeat = 1,
            BlobIoTimeout = store.Timeout, MaxArtifactBytes = store.Limits.MaxArtifactBytes
        };
        var settings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["isolation.level"] = "read_uncommitted", ["max.poll.interval.ms"] = "300000"
        };
        var plan = new ConsumerVerificationPlan(null, 600000, settings, settings);
        var data = ConsumerJudgementFixture.Empty();
        var context = new RepeatResultContext(VerifierResultStatus.CompleteValid, data.Window, [], [],
            clock.GetUtcNow().AddMinutes(10), new InputBudget(store.Limits));
        int offsetCalls = 0;
        var extension = new ConsumerRepeatExtension(options, plan, store.Reader(), store, ReadOffsets, clock, "synthetic:9092");

        RepeatResultExtension result = await extension.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(complete ? VerifierResultStatus.CompleteValid : VerifierResultStatus.Invalid, result.Status);
        Assert.Equal(complete ? [] : AssignmentInvalidReason,
            result.Consumer.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()));
        Assert.Equal(1, offsetCalls);
        Assert.Equal("success", result.Consumer.GetProperty("offset_read").GetProperty("outcome").GetString());
        Assert.Equal(complete, result.Consumer.TryGetProperty("counts", out _));

        Task<ConsumerOffsetRead> ReadOffsets(IReadOnlyList<int> partitions, DateTimeOffset deadline, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            offsetCalls++;
            Assert.Equal([0], partitions);
            Assert.Equal(context.DeadlineInstant, deadline);
            return Task.FromResult(new ConsumerOffsetRead("success", partitions, clock.GetUtcNow(), clock.GetUtcNow(), 0,
                [new(0, -1001, 0)]));
        }
    }

    private static JsonObject Snapshot() => new()
    {
        ["schema_version"] = 1, ["kind"] = "consumer_start_snapshot", ["group_id"] = ConsumerWireFixture.Group,
        ["first_read_started_utc"] = ConsumerWireFixture.Stamp, ["second_read_ended_utc"] = ConsumerWireFixture.Stamp,
        ["members"] = new JsonArray(Member("subject", 0, 0), Member("witness", 0, 0))
    };

    private static JsonObject Member(string role, long first, long second) => new()
    {
        ["instance_id"] = role, ["role"] = role,
        ["first_read_last_row_sequence"] = first, ["second_read_last_row_sequence"] = second
    };
}
#pragma warning restore CA1707
