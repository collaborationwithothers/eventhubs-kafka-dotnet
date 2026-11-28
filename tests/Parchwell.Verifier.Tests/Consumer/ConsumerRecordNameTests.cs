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
