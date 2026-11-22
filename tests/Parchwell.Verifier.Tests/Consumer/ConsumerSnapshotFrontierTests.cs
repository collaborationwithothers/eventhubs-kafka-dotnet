using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Consumer;
using Parchwell.Verifier.VerifyRun.Consumer.Judgement;
using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Xunit;
using F = Parchwell.Verifier.Tests.Consumer.ConsumerJudgementFixture;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707
// A member's final record must reach the last row the start line read. The boundary cases (T1 to T3, T6 and T7) keep all other
// evidence valid, so the frontier check is the only reason that can appear. The empty-record and precedence cases (T4, T5, T5b
// and T8) pin other reasons on purpose, because they test how the frontier check combines with the other checks.
public sealed class ConsumerSnapshotFrontierTests
{
    private const string Detail = "record ends before start snapshot";
    private static readonly string[] RecordInvalid = ["consumer_record_invalid"];

    [Theory]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void Given_WitnessSecondFrontierAroundLastRow_T1_When_Judged_Then_OnlyAboveLastRowIsInvalid(long second, bool complete)
    {
        ConsumerJudgementResult result = Judge(Snapshot(new("subject", "subject", 0, 0), new("witness", "witness", 0, second)));

        AssertOutcome(result, complete);
        if (!complete) AssertOneDiagnostic(result, "witness", "run1/C1/r1/consumer/witness/", second);
    }

    [Theory]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void Given_WitnessBothFrontiersAroundLastRow_T2_When_Judged_Then_OnlyAboveLastRowIsInvalid(long frontier, bool complete)
    {
        ConsumerJudgementResult result = Judge(Snapshot(new("subject", "subject", 0, 0), new("witness", "witness", frontier, frontier)));

        AssertOutcome(result, complete);
    }

    [Fact]
    public void Given_SubjectSecondFrontierAboveLastRow_T3_When_Judged_Then_SubjectIsReported()
    {
        ConsumerJudgementResult result = Judge(Snapshot(new("subject", "subject", 0, 7), new("witness", "witness", 0, 0)));

        AssertOutcome(result, false);
        AssertOneDiagnostic(result, "subject", "run1/C1/r1/consumer/subject/", 7);
    }

    [Theory]
    [InlineData(-1, new[] { "consumer_start_assignment_invalid" })]
    [InlineData(0, new[] { "consumer_record_invalid", "consumer_start_assignment_invalid" })]
    public void Given_EmptyWitnessRecord_T4_When_Judged_Then_FrontierIsCheckedAgainstMinusOne(long second, string[] reasons)
    {
        ConsumerJudgementRequest request = F.SetMember(F.Create(), "witness", member => F.WithRows(member, []));
        request = request with { Snapshot = request.Snapshot! with { Members = [new("subject", "subject", 0, 0), new("witness", "witness", -1, second)] } };

        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());

        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        Assert.Equal(reasons, Reasons(result));
        Assert.Equal(second == 0, Diagnostics(result).Any(d => d.GetProperty("detail").GetString() == Detail));
    }

    [Fact]
    public void Given_RegistrationInvalid_T5_When_FrontierIsAbove_Then_FrontierCheckDoesNotRun()
    {
        ConsumerJudgementRequest request = F.SetMember(F.Create(), "witness", member => member with { Registration = member.Registration! with { Role = "subject" } });
        request = With(request, new("subject", "subject", 0, 0), new("witness", "subject", 0, 7));

        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());

        Assert.Equal(["consumer_registration_invalid"], Reasons(result));
        Assert.False(result.ShouldRead);
    }

    [Fact]
    public void Given_ProducerSideInvalid_T5b_When_FrontierIsAbove_Then_FrontierCheckDoesNotRun()
    {
        ConsumerJudgementRequest request = With(F.Create() with { RootStatus = VerifierResultStatus.Invalid },
            new("subject", "subject", 0, 0), new("witness", "witness", 0, 7));

        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());

        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        Assert.Equal(["producer_side_not_complete_valid"], Reasons(result));
        Assert.DoesNotContain(Diagnostics(result), d => d.GetProperty("detail").GetString() == Detail);
    }

    [Fact]
    public void Given_PreflightEvaluation_T6_When_FrontierIsAbove_Then_ReasonAppearsAndOffsetReadStillRuns()
    {
        ConsumerJudgementRequest request = Snapshot(new("subject", "subject", 0, 0), new("witness", "witness", 0, 7));

        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.NotAttempted(), false);

        Assert.True(result.ShouldRead);
        Assert.Contains("consumer_record_invalid", Reasons(result));
    }

    [Fact]
    public void Given_SnapshotMemberUnregistered_T8_When_Judged_Then_FrontierCheckSkipsIt()
    {
        ConsumerJudgementRequest request = With(F.Create(), new("subject", "subject", 0, 0), new("other", "witness", 0, 7));

        ConsumerJudgementResult result = ConsumerJudgement.Evaluate(request, F.Read());

        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        Assert.Equal(["consumer_start_assignment_invalid"], Reasons(result));
        Assert.DoesNotContain(Diagnostics(result), d => d.GetProperty("detail").GetString() == Detail);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public async Task Given_WitnessRecordOfThreeRows_T7_When_ReadFromWireAndJudged_Then_OnlyFrontierAboveLastRowIsInvalid(long second, bool complete)
    {
        var clock = new ManualKafkaTimeProvider();
        var store = new ConsumerWireFixture { Clock = clock };
        store.Blobs[ConsumerWireFixture.Prefix + "consumer-start.json"] = JsonSerializer.SerializeToUtf8Bytes(new JsonObject
        {
            ["schema_version"] = 1, ["kind"] = "consumer_start_snapshot", ["group_id"] = ConsumerWireFixture.Group,
            ["first_read_started_utc"] = ConsumerWireFixture.Stamp, ["second_read_ended_utc"] = ConsumerWireFixture.Stamp,
            ["members"] = new JsonArray(WireMember("subject", 0, 0), WireMember("witness", 1, second))
        });
        JsonObject firstCommit = ConsumerWireFixture.Commit("witness", 1);
        firstCommit["offsets"] = new JsonArray();
        JsonObject secondCommit = ConsumerWireFixture.Commit("witness", 2);
        secondCommit["offsets"] = new JsonArray();
        store.SetRows("witness", [ConsumerWireFixture.Assignment("witness", []), firstCommit, secondCommit]);
        store.Set("witness", "completion.json", ConsumerWireFixture.Completion("witness", 3, 1, 0));
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
        var data = F.Empty();
        var context = new RepeatResultContext(VerifierResultStatus.CompleteValid, data.Window, [], [],
            clock.GetUtcNow().AddMinutes(10), new InputBudget(store.Limits));
        var extension = new ConsumerRepeatExtension(options, plan, store.Reader(), store, ReadOffsets, clock, "synthetic:9092");

        RepeatResultExtension result = await extension.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(complete ? VerifierResultStatus.CompleteValid : VerifierResultStatus.Invalid, result.Status);
        Assert.Equal(complete ? [] : RecordInvalid,
            result.Consumer.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()));

        static Task<ConsumerOffsetRead> ReadOffsets(IReadOnlyList<int> partitions, DateTimeOffset deadline, CancellationToken token) =>
            Task.FromResult(new ConsumerOffsetRead("success", partitions, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0, [new(0, -1001, 0)]));
    }

    private static JsonObject WireMember(string role, long first, long second) => new()
    {
        ["instance_id"] = role, ["role"] = role,
        ["first_read_last_row_sequence"] = first, ["second_read_last_row_sequence"] = second
    };

    private static ConsumerJudgementResult Judge(ConsumerJudgementRequest request) => ConsumerJudgement.Evaluate(request, F.Read());

    private static ConsumerJudgementRequest With(ConsumerJudgementRequest request, params ConsumerSnapshotMember[] members) =>
        request with { Snapshot = request.Snapshot! with { Members = members } };

    private static ConsumerJudgementRequest Snapshot(params ConsumerSnapshotMember[] members) => With(F.Create(), members);

    private static void AssertOutcome(ConsumerJudgementResult result, bool complete)
    {
        Assert.Equal(complete ? VerifierResultStatus.CompleteValid : VerifierResultStatus.Invalid, result.Status);
        Assert.Equal(complete ? [] : RecordInvalid, Reasons(result));
        Assert.Equal(complete, result.Consumer.TryGetProperty("counts", out _));
        Assert.False(result.Consumer.TryGetProperty("reassignment", out _));
    }

    private static void AssertOneDiagnostic(ConsumerJudgementResult result, string member, string artifact, long rowSequence)
    {
        JsonElement diagnostic = Assert.Single(Diagnostics(result));
        Assert.Equal("consumer_record_invalid", diagnostic.GetProperty("reason").GetString());
        Assert.Equal(member, diagnostic.GetProperty("member").GetString());
        Assert.Equal(artifact, diagnostic.GetProperty("artifact").GetString());
        Assert.Equal(rowSequence, diagnostic.GetProperty("row_sequence").GetInt64());
        Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty("field").ValueKind);
        Assert.Equal(Detail, diagnostic.GetProperty("detail").GetString());
    }

    private static string?[] Reasons(ConsumerJudgementResult result) =>
        result.Consumer.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()).ToArray();

    private static JsonElement[] Diagnostics(ConsumerJudgementResult result) =>
        result.Consumer.GetProperty("diagnostics").EnumerateArray().ToArray();
}
#pragma warning restore CA1707
