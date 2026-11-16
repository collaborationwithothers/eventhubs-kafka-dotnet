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
public sealed class ConsumerRecordNameTests
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

    [Theory]
    [InlineData("00000.jsonl")]
    [InlineData("0000000.jsonl")]
    [InlineData("000001.jsonx")]
    [InlineData("00000x.jsonl")]
    [InlineData("00000/.jsonl")]
    [InlineData("00000:.jsonl")]
    [InlineData("")]
    public async Task Given_NoncanonicalRecordGrammar_U4_When_Loaded_Then_MarksInvalidAndSkipsUnsupportedName(string fileName)
    {
        var wire = new ConsumerWireFixture();
        string canonical = ConsumerWireFixture.Name("subject", "record/000000.jsonl");
        string malformed = ConsumerWireFixture.Name("subject", "record/" + fileName);
        wire.Blobs[malformed] = wire.Blobs[canonical];
        wire.Blobs.Remove(canonical);

        ConsumerArtifacts result = await wire.Load();

        Assert.False(result.Unavailable);
        Assert.True(result.Members.Single(m => m.Registration!.Role == "subject").RecordInvalid);
        Assert.DoesNotContain(malformed, wire.Reads);
    }

    [Theory]
    [InlineData("000002.jsonl")]
    [InlineData("000001.jsonl")]
    [InlineData("000008.jsonl")]
    [InlineData("000009.jsonl")]
    public async Task Given_CanonicalIndex_U4_When_Loaded_Then_ReadsBothContiguousAndGappedNames(string fileName)
    {
        var wire = new ConsumerWireFixture();
        string name = ConsumerWireFixture.Name("subject", "record/" + fileName);
        wire.Blobs[name] = wire.Blobs[ConsumerWireFixture.Name("subject", "record/000000.jsonl")];
        ConsumerArtifacts result = await wire.Load();
        Assert.False(result.Unavailable);
        Assert.True(result.Members.Single(m => m.Registration!.Role == "subject").RecordInvalid);
        Assert.Contains(name, wire.Reads);
    }

    [Theory]
    [InlineData("bad..pod")]
    [InlineData("bad\\pod")]
    public async Task Given_MarkerRejectedByActualStorageGuard_U20_When_Loaded_Then_ReturnsUnavailableWithArtifactDiagnostic(string pod)
    {
        var wire = new ConsumerWireFixture();
        string name = ConsumerWireFixture.Name(pod, "registration.json");
        wire.Blobs.Add(name, []);
        BlobProducerArtifactStore realGuard = GuardedConsumerArtifactStore.CreateGuard();
        await Assert.ThrowsAsync<ArgumentException>(async () => await realGuard.ReadBoundedAsync(name, wire.Limits.MaxArtifactBytes, TestContext.Current.CancellationToken));
        var guarded = new GuardedConsumerArtifactStore(wire);
        ConsumerArtifacts result = await new ConsumerArtifactReader(guarded, wire.Limits, wire.Timeout, wire.Clock)
            .ReadAsync(ConsumerWireFixture.Prefix, new InputBudget(wire.Limits), TestContext.Current.CancellationToken);
        Assert.True(result.Unavailable);
        Assert.Equal(1, guarded.GuardedReadAttempts);
        ConsumerDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("consumer_record_unavailable", diagnostic.Reason);
        Assert.Equal(name, diagnostic.Artifact);
    }
}
#pragma warning restore CA1707

internal sealed class GuardedConsumerArtifactStore(IProducerArtifactStore inner) : IProducerArtifactStore
{
    private readonly BlobProducerArtifactStore guard = CreateGuard();
    internal int GuardedReadAttempts { get; private set; }
    internal static BlobProducerArtifactStore CreateGuard() => new BlobProducerArtifactStore(new BlobContainerClient(new Uri("https://synthetic.invalid/consumer-record")), 100_000_000);
    public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) => inner.ListAsync(prefix, cancellationToken);
    public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => ReadBoundedAsync(name, 100_000_000, cancellationToken);
    public Task<byte[]?> ReadBoundedAsync(string name, long maximumBytes, CancellationToken cancellationToken)
    {
        if (name.Contains("..", StringComparison.Ordinal) || name.Contains('\\') || name.StartsWith('/'))
        {
            GuardedReadAttempts++;
            return guard.ReadBoundedAsync(name, maximumBytes, cancellationToken);
        }
        return inner.ReadBoundedAsync(name, maximumBytes, cancellationToken);
    }
}
