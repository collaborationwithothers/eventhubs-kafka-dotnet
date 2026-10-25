using System.Text.Json;
using Parchwell.Runner.Run.Rehearsal;
using Xunit;

namespace Parchwell.Runner.Tests;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class RehearsalTests
{
    [Fact]
    public async Task Given_FreshWorkerCodecsAndLegacyReceipts_When_Rehearsing_Then_ThreePhasesAndEveryNegativeTraceAreBounded()
    {
        string root = Path.Combine(Path.GetTempPath(), "runner-rehearsal-" + Guid.NewGuid().ToString("N"));
        try
        {
            string fixtures = Path.Combine(root, "fixtures");
            await WorkerWireFixtureTests.ProduceAsync(fixtures, TestContext.Current.CancellationToken);
            var runner = new RehearsalRunner(TimeProvider.System, new RehearsalScenarioFactory(TimeProvider.System));
            Assert.Equal(0, await runner.ExecuteAsync(fixtures, Path.Combine(root, "output"), TestContext.Current.CancellationToken));
            using JsonDocument summary = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(root, "output", "summary.json"), TestContext.Current.CancellationToken));
            JsonElement result = summary.RootElement;
            Assert.Equal(3, result.GetProperty("phases").GetInt32());
            Assert.Equal(5, result.GetProperty("probe_repeats").GetInt32());
            Assert.Equal(5, result.GetProperty("full_C0_repeats").GetInt32());
            Assert.Equal(5, result.GetProperty("full_C3_repeats").GetInt32());
            Assert.Equal(3, result.GetProperty("applies").GetInt32());
            Assert.Equal(3, result.GetProperty("destroys").GetInt32());
            Assert.Equal(0, result.GetProperty("azure_calls").GetInt32());
            Assert.Equal(0, result.GetProperty("replacement_repeats").GetInt32());
            Assert.Equal("passed", result.GetProperty("result").GetString());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Given_CorruptedWireFile_When_LoadingManifest_Then_SourceBytesAreRejected()
    {
        string root = Path.Combine(Path.GetTempPath(), "runner-corrupt-" + Guid.NewGuid().ToString("N"));
        try
        {
            await WorkerWireFixtureTests.ProduceAsync(root, TestContext.Current.CancellationToken);
            await File.AppendAllTextAsync(Path.Combine(root, "probe", "r1", "result.json"), " ", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => WireFixtures.LoadAsync(root, TestContext.Current.CancellationToken));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
