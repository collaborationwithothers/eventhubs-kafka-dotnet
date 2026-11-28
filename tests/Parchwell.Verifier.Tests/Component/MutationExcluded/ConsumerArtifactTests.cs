// This file sits under Component/ only so the default mutation-only build drops it (the csproj removes Component/**/*.cs).
// The tests still run in the normal build and in the full-suite mutation build. Why: docs/operations/verifier-mutation.md, section "Issue 602 fast-suite equivalence check".
using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Consumer;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707
public sealed partial class ConsumerArtifactTests
{
    [Theory]
    [InlineData(39999, true, true)]
    [InlineData(40000, true, false)]
    [InlineData(40001, true, true)]
    [InlineData(40000, false, false)]
    [InlineData(40001, false, true)]
    public async Task Given_RecordBlobRowBoundaries_U4_When_Loaded_Then_EnforcesRollover(int count, bool following, bool invalid)
    {
        var fixture = new ConsumerWireFixture();
        fixture.SetRows("subject", Enumerable.Range(0, count).Select(n => ConsumerWireFixture.Assignment("subject", [0], n)));
        if (following) fixture.SetRows("subject", [ConsumerWireFixture.Assignment("subject", [0], count)], 1);
        fixture.Set("subject", "completion.json", ConsumerWireFixture.Completion("subject", count + (following ? 1 : 0), following ? 2 : 1, 0));
        ConsumerArtifacts loaded = await fixture.Load();
        Assert.False(loaded.Unavailable);
        Assert.Equal(invalid, Subject(loaded).RecordInvalid);
    }
}
#pragma warning restore CA1707
