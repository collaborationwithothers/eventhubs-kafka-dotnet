using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun;
using Parchwell.Verifier.VerifyRun.Consumer;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707
public sealed class ConsumerExtensionTests
{
    private static readonly string[] MemberRoles = ["subject", "witness"];
    [Theory]
    [InlineData("invalid")]
    [InlineData("incomplete")]
    public async Task Given_ProducerNotCompleteValid_U2_When_Evaluated_Then_ReadsArtifactsAndSkipsOffsets(string root)
    {
        var fixture = new ExtensionFixture();
        fixture.Context = fixture.Context with { RootStatus = root == "invalid" ? VerifierResultStatus.Invalid : VerifierResultStatus.Incomplete };
        RepeatResultExtension result = await fixture.Run();
        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        AssertReasons(result, "producer_side_not_complete_valid");
        Assert.Equal(0, fixture.OffsetCalls);
        Assert.Contains(ConsumerWireFixture.Name("subject", "record/000000.jsonl"), fixture.Store.Reads);
        Assert.Contains(ConsumerWireFixture.Name("witness", "completion.json"), fixture.Store.Reads);
        AssertNoRead(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Given_RegistrationCount_U3_When_Evaluated_Then_ReadOnlyForTwo(int count)
    {
        var fixture = new ExtensionFixture();
        foreach (string role in MemberRoles.Skip(count)) fixture.Store.Blobs.Remove(ConsumerWireFixture.Name(role, "registration.json"));
        if (count == 3) fixture.Store.Blobs[ConsumerWireFixture.Name("extra", "registration.json")] = Encoding.UTF8.GetBytes(ConsumerWireFixture.Registration("subject").ToJsonString());
        RepeatResultExtension result = await fixture.Run();
        if (count == 2) Assert.Equal(VerifierResultStatus.CompleteValid, result.Status);
        else { Assert.Equal(VerifierResultStatus.Invalid, result.Status); AssertReasons(result, "consumer_registration_invalid"); AssertNoRead(result); }
        Assert.Equal(count == 2 ? 1 : 0, fixture.OffsetCalls);
    }

    [Theory]
    [InlineData("two-subjects")]
    [InlineData("repeat")]
    [InlineData("group")]
    [InlineData("run")]
    [InlineData("observer")]
    [InlineData("pod-prefix")]
    [InlineData("instance-pod")]
    [InlineData("unknown")]
    [InlineData("unordered-settings")]
    public async Task Given_InvalidRegistration_U3_When_Evaluated_Then_SkipsOffsetRead(string mutation)
    {
        var fixture = new ExtensionFixture();
        string member = mutation == "two-subjects" ? "witness" : "subject";
        JsonObject marker = ConsumerWireFixture.Registration(member);
        switch (mutation)
        {
            case "two-subjects": marker["role"] = "subject"; break;
            case "repeat": marker["repeat_number"] = 2; break;
            case "group": marker["group_id"] = ConsumerWireFixture.Group + "x"; break;
            case "run": marker["run_id"] = "other"; break;
            case "observer": marker["role"] = "observer"; break;
            case "pod-prefix": marker["pod_name"] = "other"; marker["instance_id"] = "other"; break;
            case "instance-pod": marker["instance_id"] = "other"; break;
            case "unknown": marker["unknown"] = 1; break;
            case "unordered-settings": marker["effective_settings"] = new JsonObject { ["z"] = "value", ["a"] = "value" }; break;
        }
        fixture.Store.Set(member, "registration.json", marker);
        RepeatResultExtension result = await fixture.Run();
        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        AssertReasons(result, "consumer_registration_invalid");
        Assert.Equal(0, fixture.OffsetCalls);
        AssertNoRead(result);
    }

    [Fact]
    public async Task Given_InvalidRecord_U9_When_Evaluated_Then_StillMakesSingleReadAndKeepsInvalidPrecedence()
    {
        var fixture = new ExtensionFixture();
        string name = ConsumerWireFixture.Name("subject", "record/000000.jsonl");
        fixture.Store.Blobs[name] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(fixture.Store.Blobs[name]).Replace("\"row_sequence\":1", "\"row_sequence\":99", StringComparison.Ordinal));
        RepeatResultExtension result = await fixture.Run();
        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        Assert.Contains("consumer_record_invalid", Reasons(result));
        Assert.DoesNotContain(Reasons(result), reason => reason is "consumer_commit_evidence_unavailable" or "consumer_completion_deadline" or "consumer_commit_frontier_short");
        Assert.Equal(1, fixture.OffsetCalls);
        Assert.False(result.Consumer.TryGetProperty("counts", out _));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task Given_NoTimeLeft_U10_When_Evaluated_Then_SkipsReadAndReportsDeadline(long ticks)
    {
        var fixture = new ExtensionFixture();
        fixture.Context = fixture.Context with { DeadlineInstant = fixture.Clock.GetUtcNow().AddTicks(ticks) };
        RepeatResultExtension result = await fixture.Run();
        Assert.Equal(VerifierResultStatus.Incomplete, result.Status);
        AssertReasons(result, "consumer_completion_deadline");
        Assert.Equal(0, fixture.OffsetCalls);
        AssertNoRead(result);
    }

    [Fact]
    public async Task Given_AllMemberArtifacts_D14_When_Evaluated_Then_OnlyReadsOffsetsAfterRecordsReceiptsAndKillIntent()
    {
        var fixture = new ExtensionFixture();
        fixture.Plan = fixture.Plan with { KillCount = 3 };
        var kill = new JsonObject { ["schema_version"] = 1, ["kind"] = "kill_intent", ["instance_id"] = "subject", ["row_sequence"] = 3, ["partition"] = 0, ["offset"] = 102, ["event_utc"] = ConsumerWireFixture.Stamp };
        fixture.Store.SetRows("subject", [ConsumerWireFixture.Assignment("subject", [0]), ConsumerWireFixture.Processed("subject", 100, 1), ConsumerWireFixture.Processed("subject", 101, 2), kill]);
        fixture.Store.Blobs.Remove(ConsumerWireFixture.Name("subject", "completion.json"));
        fixture.BeforeOffsets = () =>
        {
            Assert.Equal(ConsumerWireFixture.Prefix + "consumer-start.json", fixture.Store.Reads[^1]);
            foreach (string role in new[] { "subject", "witness" })
            {
                Assert.Contains(ConsumerWireFixture.Name(role, "record/000000.jsonl"), fixture.Store.Reads);
                Assert.Contains(ConsumerWireFixture.Name(role, "completion.json"), fixture.Store.Reads);
            }
            Assert.Equal("kill_intent", ConsumerArtifactCodec.ReadRow(Encoding.UTF8.GetBytes(kill.ToJsonString())).Kind);
        };
        RepeatResultExtension result = await fixture.Run();
        Assert.Equal(VerifierResultStatus.CompleteValid, result.Status);
        Assert.Equal(1, fixture.OffsetCalls);
        Assert.Equal(110, result.Consumer.GetProperty("partitions")[0].GetProperty("observed").GetInt64());
    }

    [Theory]
    [InlineData("listing")]
    [InlineData("forbidden")]
    [InlineData("timeout")]
    [InlineData("snapshot-forbidden")]
    public async Task Given_ConsumerReadFailure_U20_When_Evaluated_Then_UnavailableAndNoOffsetRead(string failure)
    {
        var fixture = new ExtensionFixture();
        string name = failure == "snapshot-forbidden" ? ConsumerWireFixture.Prefix + "consumer-start.json" : ConsumerWireFixture.Name("subject", "record/000000.jsonl");
        if (failure == "listing") fixture.Store.ListingComplete = false;
        else fixture.Store.ReadOverride = (artifact, _) =>
        {
            if (artifact == name)
            {
                if (failure == "timeout") { fixture.Clock.Advance(3000); return Task.FromResult(fixture.Store.Blobs.GetValueOrDefault(artifact)); }
                throw new RequestFailedException(403, "synthetic forbidden");
            }
            return Task.FromResult(fixture.Store.Blobs.GetValueOrDefault(artifact));
        };
        RepeatResultExtension result = await fixture.Run();
        Assert.Equal(VerifierResultStatus.Incomplete, result.Status);
        AssertReasons(result, "consumer_record_unavailable");
        JsonElement diagnostic = Assert.Single(result.Consumer.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(failure == "listing" ? ConsumerWireFixture.Prefix + "consumer/" : name, diagnostic.GetProperty("artifact").GetString());
        Assert.Equal(0, fixture.OffsetCalls);
        AssertNoRead(result);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task Given_ArtifactByteBoundary_U20_When_Evaluated_Then_ExactLimitPasses(int excess, bool unavailable)
    {
        var fixture = new ExtensionFixture();
        string name = ConsumerWireFixture.Name("subject", "record/000000.jsonl");
        byte[] original = fixture.Store.Blobs[name];
        const int limit = 9000;
        fixture.Store.Blobs[name] = original[..^1].Concat(Enumerable.Repeat((byte)' ', limit + excess - original.Length)).Append((byte)'\n').ToArray();
        fixture.SetLimits(fixture.Store.Limits with { MaxArtifactBytes = limit });
        RepeatResultExtension result = await fixture.Run();
        Assert.Equal(unavailable ? VerifierResultStatus.Incomplete : VerifierResultStatus.CompleteValid, result.Status);
        if (unavailable) { AssertReasons(result, "consumer_record_unavailable"); AssertNoRead(result); }
        Assert.Equal(unavailable ? 0 : 1, fixture.OffsetCalls);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task Given_TotalInputByteBoundary_U20_When_Evaluated_Then_IncludesPreviouslyCountedProducerBytes(int excess, bool unavailable)
    {
        var fixture = new ExtensionFixture();
        const int producerBytes = 128;
        long total = fixture.Store.Blobs.Values.Sum(bytes => (long)bytes.Length) + producerBytes;
        fixture.SetLimits(fixture.Store.Limits with { MaxTotalInputBytes = total - excess });
        fixture.Context.InputBudget.ConsumeBytes(producerBytes);
        RepeatResultExtension result = await fixture.Run();
        Assert.Equal(unavailable ? VerifierResultStatus.Incomplete : VerifierResultStatus.CompleteValid, result.Status);
        if (unavailable) { AssertReasons(result, "consumer_record_unavailable"); AssertNoRead(result); }
        Assert.Equal(unavailable ? 0 : 1, fixture.OffsetCalls);
    }

    [Fact]
    public async Task Given_ProducerInvalidAndConsumerUnavailable_U20_When_Evaluated_Then_OnlyProducerReasonWins()
    {
        var fixture = new ExtensionFixture();
        fixture.Store.ListingComplete = false;
        fixture.Context = fixture.Context with { RootStatus = VerifierResultStatus.Invalid };
        RepeatResultExtension result = await fixture.Run();
        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        AssertReasons(result, "producer_side_not_complete_valid");
        Assert.Equal(0, fixture.OffsetCalls);
        AssertNoRead(result);
    }

    [Fact]
    public async Task Given_MissingSnapshot_U20_When_Evaluated_Then_AssignmentInvalidRatherThanUnavailable()
    {
        var fixture = new ExtensionFixture();
        fixture.Store.Blobs.Remove(ConsumerWireFixture.Prefix + "consumer-start.json");
        RepeatResultExtension result = await fixture.Run();
        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        AssertReasons(result, "consumer_start_assignment_invalid");
        Assert.Equal(1, fixture.OffsetCalls);
    }

    private static string[] Reasons(RepeatResultExtension result) => result.Consumer.GetProperty("reasons").EnumerateArray().Select(r => r.GetString()!).ToArray();
    private static void AssertReasons(RepeatResultExtension result, params string[] reasons) => Assert.Equal(reasons, Reasons(result));
    private static void AssertNoRead(RepeatResultExtension result)
    {
        JsonElement read = result.Consumer.GetProperty("offset_read");
        Assert.Equal("not_attempted", read.GetProperty("outcome").GetString());
        foreach (string field in new[] { "started_utc", "ended_utc", "error_code" }) Assert.Equal(JsonValueKind.Null, read.GetProperty(field).ValueKind);
        Assert.Empty(read.GetProperty("offsets").EnumerateArray());
        Assert.False(result.Consumer.TryGetProperty("counts", out _));
    }

    private sealed class ExtensionFixture
    {
        internal ConsumerWireFixture Store { get; } = new();
        internal ManualKafkaTimeProvider Clock { get; } = new();
        internal VerifierHostOptions Options { get; } = new()
        {
            RunId = "synthetic-consumer", ScenarioId = "C03", ConfigurationId = "C0", Phase = "C0", Repeat = 1,
            BlobIoTimeout = TimeSpan.FromSeconds(2), MaxArtifactBytes = 100_000_000
        };
        internal ConsumerVerificationPlan Plan { get; set; } = new(null, 600000,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["isolation.level"] = "read_uncommitted", ["max.poll.interval.ms"] = "300000" },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["isolation.level"] = "read_uncommitted", ["max.poll.interval.ms"] = "300000" });
        internal RepeatResultContext Context { get; set; }
        internal int OffsetCalls { get; private set; }
        internal Action? BeforeOffsets { get; set; }
        internal ExtensionFixture()
        {
            Store.Clock = Clock;
            foreach (string role in new[] { "subject", "witness" })
            {
                int first = role == "subject" ? 100 : 105;
                var rows = new List<JsonObject> { ConsumerWireFixture.Assignment(role, role == "subject" ? [0] : []) };
                rows.AddRange(Enumerable.Range(first, 5).Select((offset, index) => ConsumerWireFixture.Processed(role, offset, index + 1)));
                rows.Add(ConsumerWireFixture.Commit(role, 6));
                Store.SetRows(role, rows);
                Store.Set(role, "completion.json", ConsumerWireFixture.Completion(role, 7, 1, 5));
            }
            var data = ConsumerJudgementFixture.Create();
            Context = new(VerifierResultStatus.CompleteValid, data.Window, data.LedgerRows, data.ConsumedRecords,
                Clock.GetUtcNow().AddMinutes(10), new InputBudget(Store.Limits));
        }
        internal void SetLimits(InputLimits limits) { Store.Limits = limits; Options.MaxArtifactBytes = limits.MaxArtifactBytes; Context = Context with { InputBudget = new(limits) }; }
        internal Task<RepeatResultExtension> Run() => new ConsumerRepeatExtension(Options, Plan, Store.Reader(), Store, ReadOffsets, Clock, "synthetic:9092").EvaluateAsync(Context, TestContext.Current.CancellationToken);
        private Task<ConsumerOffsetRead> ReadOffsets(IReadOnlyList<int> partitions, DateTimeOffset deadline, CancellationToken token)
        {
            BeforeOffsets?.Invoke(); OffsetCalls++;
            Assert.Equal(Context.DeadlineInstant, deadline);
            Assert.Equal([0], partitions);
            Assert.False(token.IsCancellationRequested);
            return Task.FromResult(new ConsumerOffsetRead("success", partitions, Clock.GetUtcNow(), Clock.GetUtcNow(), 0, [new(0, 110, 0)]));
        }
    }
}
#pragma warning restore CA1707
