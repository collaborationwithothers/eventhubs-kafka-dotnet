// This file sits under Component/ only so the default mutation-only build drops it (the csproj removes Component/**/*.cs).
// The tests still run in the normal build and in the full-suite mutation build. Why: docs/operations/verifier-mutation.md, section "Issue 602 fast-suite equivalence check".
using Azure.Storage.Blobs;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Consumer;
using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707
public sealed partial class ConsumerRecordNameTests
{
    [Theory]
    [InlineData("000001..jsonl")]
    [InlineData("000001\\.jsonl")]
    public async Task Given_UnsafeRecordName_U4_When_ExtensionReads_Then_PreservesInvalidProjectionAndSingleOffsetRead(string fileName)
    {
        var wire = new ConsumerWireFixture();
        var clock = new ManualKafkaTimeProvider();
        wire.Clock = clock;
        string badName = ConsumerWireFixture.Name("subject", "record/" + fileName);
        wire.Blobs.Add(badName, []);
        wire.SetRows("subject", Enumerable.Range(0, 40000).Select(n => ConsumerWireFixture.Assignment("subject", [0], n)));
        wire.Set("subject", "completion.json", ConsumerWireFixture.Completion("subject", 40000, 2, 0));
        BlobProducerArtifactStore realGuard = GuardedConsumerArtifactStore.CreateGuard();
        await Assert.ThrowsAsync<ArgumentException>(async () => await realGuard.ReadBoundedAsync(badName, wire.Limits.MaxArtifactBytes, TestContext.Current.CancellationToken));
        var guarded = new GuardedConsumerArtifactStore(wire);
        var data = ConsumerJudgementFixture.Empty();
        var context = new RepeatResultContext(VerifierResultStatus.CompleteValid, data.Window, [], [], clock.GetUtcNow().AddMinutes(10), new InputBudget(wire.Limits));
        var options = new VerifierHostOptions { RunId = "synthetic-consumer", ScenarioId = "C03", ConfigurationId = "C0", Phase = "C0", Repeat = 1, BlobIoTimeout = wire.Timeout, MaxArtifactBytes = wire.Limits.MaxArtifactBytes };
        IReadOnlyDictionary<string, string> settings = new Dictionary<string, string>(StringComparer.Ordinal) { ["isolation.level"] = "read_uncommitted", ["max.poll.interval.ms"] = "300000" };
        var plan = new ConsumerVerificationPlan(null, 600000, settings, settings);
        int calls = 0;
        ConsumerOffsetReader offsets = (partitions, _, _) =>
        {
            calls++;
            return Task.FromResult(new ConsumerOffsetRead("success", partitions, clock.GetUtcNow(), clock.GetUtcNow(), 0, [new(0, -1001, 0)]));
        };
        var extension = new ConsumerRepeatExtension(options, plan, new ConsumerArtifactReader(guarded, wire.Limits, wire.Timeout, clock), wire, offsets, clock, "synthetic:9092");
        RepeatResultExtension result = await extension.EvaluateAsync(context, TestContext.Current.CancellationToken);
        Assert.Equal(VerifierResultStatus.Invalid, result.Status);
        Assert.Equal(["consumer_record_invalid"], result.Consumer.GetProperty("reasons").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(1, calls);
        Assert.Equal(0, guarded.GuardedReadAttempts);
        Assert.DoesNotContain(badName, wire.Reads);
        Assert.Contains(ConsumerWireFixture.Name("subject", "record/000000.jsonl"), wire.Reads);
        Assert.False(result.Consumer.TryGetProperty("counts", out _));
    }
}
#pragma warning restore CA1707
