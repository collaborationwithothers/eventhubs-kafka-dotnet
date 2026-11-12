using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Window;

#pragma warning disable CA1707
// Trial notes require three producer names while the scientific B10 codec retains its two-name rule.
public sealed class B10TrialStartLineCodecTests
{
    [Theory]
    [InlineData("b10d-1-1", 0, false)]
    [InlineData("b10d-1-1", 2, false)]
    [InlineData("b10d-1-1", 3, true)]
    [InlineData("b10d-1-1", 4, false)]
    [InlineData("b10-1-1", 1, false)]
    [InlineData("b10-1-1", 2, true)]
    [InlineData("b10-1-1", 3, false)]
    public void Given_ExpectedJobCountAtTrialBoundary_When_NoteWritten_Then_RequiresExactCount(
        string runId, int count, bool accepted)
    {
        string[] jobs = Enumerable.Range(0, count).Select(index => $"{runId}-job-{index}").ToArray();
        var identity = new RunIdentity("B10", "C1", runId, "instance", "instance", "client");
        var note = new StartLineNote(1, identity, "document-events", [], "2026-11-12T00:00:00.0000000Z",
            "commit", "test-kafka", "test-native", jobs);
        var limits = new OutputLimits(16_384, 4, 512);

        if (!accepted)
        {
            Assert.Throws<ArgumentException>(() => StartLineNoteCodec.Write(note, limits));
            return;
        }

        using JsonDocument written = JsonDocument.Parse(StartLineNoteCodec.Write(note, limits));
        Assert.Equal(jobs, written.RootElement.GetProperty("expected_job_names")
            .EnumerateArray().Select(value => value.GetString()));
    }
}
#pragma warning restore CA1707
