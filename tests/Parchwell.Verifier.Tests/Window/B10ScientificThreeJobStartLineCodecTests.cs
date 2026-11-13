using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Window;

#pragma warning disable CA1707
public sealed class B10ScientificThreeJobStartLineCodecTests
{
    public static IEnumerable<object[]> Counts()
    {
        foreach (string run in new[] { "b10-1-1", "b10d-1-1", "run-1" })
            foreach (string configuration in new[] { "C0", "C1" })
                foreach (int count in Enumerable.Range(0, 5))
                    yield return [run, configuration, count,
                        count == (run == "b10d-1-1" || run == "b10-1-1" && configuration == "C1" ? 3 :
                            run == "b10-1-1" && configuration == "C0" ? 1 : 2)];
    }

    [Theory]
    [MemberData(nameof(Counts))]
    public void Given_JobNameCount_When_Written_Then_RequiresRunAndConfigurationCount(
        string run, string configuration, int count, bool accepted)
    {
        var note = Note(run, configuration, Enumerable.Range(0, count).Select(index => $"job-{index}").ToArray());
        if (accepted) Assert.NotEmpty(StartLineNoteCodec.Write(note, Limits));
        else Assert.Throws<ArgumentException>(() => StartLineNoteCodec.Write(note, Limits));
    }

    [Theory]
    [InlineData("b10-1-1", "C1", 3)]
    [InlineData("b10d-1-1", "C1", 3)]
    [InlineData("run-1", "C0", 2)]
    public void Given_RepeatedJobName_When_Written_Then_RejectsEvenAtExpectedCount(string run, string configuration, int count)
    {
        string[] names = Enumerable.Range(0, count).Select(index => $"{run}-job-{index}").ToArray();
        names[^1] = names[0];
        Assert.Throws<ArgumentException>(() => StartLineNoteCodec.Write(Note(run, configuration, names), Limits));
    }

    private static readonly OutputLimits Limits = new(16384, 8, 256);
    private static StartLineNote Note(string run, string configuration, IReadOnlyList<string> names) => new(1,
        new RunIdentity("B10", configuration, run, "instance", "instance", "client"), "document-events", [],
        "2026-11-13T00:00:00.0000000Z", "commit", "synthetic-managed", "synthetic-native", names);
}
#pragma warning restore CA1707
