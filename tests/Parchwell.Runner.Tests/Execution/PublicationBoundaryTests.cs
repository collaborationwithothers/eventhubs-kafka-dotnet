using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Repository-required Given_When_Then names.

/// <summary>Checks publication input budgets before a deliberate downstream schema failure, without hashing large payloads.</summary>
public sealed class PublicationBoundaryTests
{
    private static readonly WorkerIdentity Identity = new("A01", "C0", "synthetic-boundary-r1", "synthetic-pod", "synthetic-pod", "synthetic-pod");

    [Theory]
    [InlineData(268_435_455, true)]
    [InlineData(268_435_456, true)]
    [InlineData(268_435_457, false)]
    public void Given_PublicationArtifactAtByteBoundary_When_Validated_Then_OnlyOversizeFailsBudget(int length, bool fits)
    {
        var objects = Objects();
        objects["ledger.jsonl"] = GC.AllocateUninitializedArray<byte>(length);
        Assert.Equal(length, objects["ledger.jsonl"].Length);
        AssertBudgetDecision(objects, fits);
    }

    [Theory]
    [InlineData(1_073_741_823L, true)]
    [InlineData(1_073_741_824L, true)]
    [InlineData(1_073_741_825L, false)]
    public void Given_PublicationAtAggregateByteBoundary_When_Validated_Then_OnlyOversizeFailsBudget(long length, bool fits)
    {
        var objects = Objects();
        // Four logical artifacts share storage; only lengths are read before the schema sentinel.
        byte[] shared = GC.AllocateUninitializedArray<byte>(268_434_432);
        foreach (string name in new[] { "ledger.jsonl", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json" }) objects[name] = shared;
        objects["manifest.json"] = new byte[checked((int)(length - 4L * shared.Length - objects["index.json"].Length))];
        Assert.Equal(length, objects.Values.Sum(bytes => (long)bytes.Length));
        Assert.All(objects.Values, bytes => Assert.InRange(bytes.Length, 0, 268_435_456));
        AssertBudgetDecision(objects, fits);
    }

    private static Dictionary<string, byte[]> Objects() => new(StringComparer.Ordinal)
    {
        ["ledger.jsonl"] = [], ["manifest.json"] = [], ["statistics.jsonl"] = [],
        ["request-lifecycle.jsonl"] = [], ["capabilities.json"] = [], ["index.json"] = "{}"u8.ToArray()
    };

    private static void AssertBudgetDecision(Dictionary<string, byte[]> objects, bool fits)
    {
        var publication = new ProducerPublication(objects);
        if (fits)
        {
            // Missing schema_version proves the real budget guard accepted the supplied lengths.
            Assert.Throws<KeyNotFoundException>(() => PublicationAcceptance.Validate(publication, Identity));
        }
        else Assert.Equal("publication_limit", Assert.Throws<InvalidDataException>(() => PublicationAcceptance.Validate(publication, Identity)).Message);
    }
}
#pragma warning restore CA1707
