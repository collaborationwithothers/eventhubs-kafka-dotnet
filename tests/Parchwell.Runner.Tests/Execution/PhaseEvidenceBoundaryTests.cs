using System.Globalization;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Run.Rehearsal;
using Parchwell.Runner.Tests.Cost;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class PhaseEvidenceBoundaryTests
{
    // Sparse synthetic additions test retained-byte admission without exporting fabricated worker evidence.
    [Theory]
    [InlineData(8589934591L, true)]
    [InlineData(8589934592L, true)]
    [InlineData(8589934593L, false)]
    public async Task Given_SyntheticPhaseEvidenceByteBoundary_When_FirstRepeatIsRetained_Then_OnlyBoundedTotalReachesRepeatExport(long totalBytes, bool admitted)
    {
        using var fixture = new TemporaryDirectory();
        string wireDirectory = Path.Combine(fixture.Root, "wire");
        await WorkerWireFixtureTests.ProduceAsync(wireDirectory, TestContext.Current.CancellationToken);
        WireFixtures wire = await WireFixtures.LoadAsync(wireDirectory, TestContext.Current.CancellationToken);
        var clock = new FixedClock();
        string baselineDirectory = Path.Combine(fixture.Root, "baseline");
        var baseline = await ExecuteUntilExportAsync(baselineDirectory, wire, clock);
        long baselineBytes = Assert.IsType<long>(baseline.Store.FirstRepeatExportBytes);
        Assert.True(baselineBytes > 0);
        // The small baseline reaches both repeat export and the fallback phase export of the same object path.
        Assert.Equal(2, baseline.Store.RepeatExportAttempts);

        string directory = Path.Combine(fixture.Root, "boundary");
        Directory.CreateDirectory(directory);
        AddSparseFiles(directory, totalBytes - baselineBytes);
        Assert.Equal(totalBytes, RetainedBytes(directory) + baselineBytes);
        var result = await ExecuteUntilExportAsync(directory, wire, clock);

        Assert.Equal("failed", result.Execution.Result);
        Assert.False(result.Execution.Exported);
        Assert.True(result.Execution.Destroyed);
        Assert.Empty(result.Execution.Repeats);
        Assert.Equal(1, result.Environment.Destroys);
        Assert.Equal(admitted ? 1 : 0, result.Store.RepeatExportAttempts);
        if (admitted) Assert.Equal(totalBytes, result.Store.FirstRepeatExportBytes);
        else Assert.Null(result.Store.FirstRepeatExportBytes);
        Assert.Contains("cleanup-receipt", result.Trace);
        Assert.True(result.Trace.IndexOf("cleanup-receipt") > result.Trace.IndexOf("destroy"));
    }

    // Recovery hashes sparse files as streams; it never needs an aggregate-sized byte array.
    [Theory]
    [InlineData(8589934591L, true)]
    [InlineData(8589934592L, true)]
    [InlineData(8589934593L, false)]
    public async Task Given_SyntheticRecoveryEvidenceByteBoundary_When_CollectingInterruptedPhase_Then_OnlyBoundedTotalReturnsCapture(long totalBytes, bool admitted)
    {
        using var fixture = new TemporaryDirectory();
        var clock = new FixedClock();
        var generation = new EnvironmentGeneration("synthetic-phase", clock.GetUtcNow().AddHours(4), "synthetic-lineage", 1);
        var record = new PhaseRecord("interrupted", null, generation.RunId, 0, 0, 0, null, null,
            clock.GetUtcNow(), generation.ExpiresAt, [], null, null, null);
        ExecutionBudgets budgets = await ReadBudgetsAsync();
        string baselineDirectory = Path.Combine(fixture.Root, "baseline");
        RecoveryCapture baseline = await Collector(new RecoveryProcess(generation), "baseline")
            .CollectAsync(generation, "probe", record, baselineDirectory, TestContext.Current.CancellationToken);
        long baselineBytes = baseline.Files.Sum(file => new FileInfo(file.LocalPath).Length);
        Assert.True(baselineBytes > 0);

        string directory = Path.Combine(fixture.Root, "boundary");
        var process = new RecoveryProcess(generation, () => AddSparseFiles(directory, totalBytes - baselineBytes));
        RecoveryEvidenceCollector collector = Collector(process, "boundary");
        if (admitted)
        {
            RecoveryCapture capture = await collector.CollectAsync(generation, "probe", record, directory, TestContext.Current.CancellationToken);
            Assert.Equal(totalBytes, capture.Files.Sum(file => new FileInfo(file.LocalPath).Length));
            Assert.All(capture.Files, file => Assert.InRange(new FileInfo(file.LocalPath).Length, 0, 268435456));
            Assert.Matches("^[a-f0-9]{64}$", capture.CaptureSha256);
            Assert.Contains(capture.Files, file => file.RelativePath == "terraform/state.private.json");
            Assert.Contains(capture.Files, file => file.RelativePath == "terraform/outputs.private.json");
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => collector.CollectAsync(generation, "probe", record, directory, TestContext.Current.CancellationToken));
            Assert.Equal("recovery_evidence_limit", error.Message);
            Assert.Equal(totalBytes, RetainedBytes(directory));
        }
        Assert.Equal(1, process.OutputReads);
        using JsonDocument receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory, "collection-receipt.json"), TestContext.Current.CancellationToken));
        Assert.False(receipt.RootElement.GetProperty("accepted_counts").GetBoolean());
        Assert.False(receipt.RootElement.GetProperty("complete").GetBoolean());

        RecoveryEvidenceCollector Collector(RecoveryProcess transport, string name)
        {
            var terraform = new TerraformEnvironment(transport, Path.Combine(fixture.Root, "ephemeral"),
                "synthetic-backend", "synthetic-tfvars", Path.Combine(fixture.Root, name + "-plans"), clock);
            return new(transport, terraform, clock, budgets, Path.Combine(fixture.Root, name + "-credentials", "kubeconfig"));
        }
    }

    private static async Task<(PhaseExecution Execution, ExportStopStore Store, RehearsalEnvironment Environment, List<string> Trace)> ExecuteUntilExportAsync(
        string directory, WireFixtures wire, TimeProvider clock)
    {
        var trace = new List<string>();
        var store = new ExportStopStore(directory, trace);
        var ledger = new RunLedger(store, clock);
        var environment = new RehearsalEnvironment(trace: trace);
        var executor = new PhaseExecutor(ledger, environment, new RepeatExecutor(new RehearsalCluster(wire), clock),
            new EvidenceExporter(store, clock), new GenerationGuard(environment, clock), clock);
        PhaseExecution execution = await executor.ExecuteAsync(new("probe", "rehearsal-probe", wire.SourceCommit, directory,
            PhaseReservationTests.Input(), true), TestContext.Current.CancellationToken);
        return (execution, store, environment, trace);
    }

    private static void AddSparseFiles(string directory, long bytes)
    {
        Assert.True(bytes > 0);
        string target = Path.Combine(directory, "synthetic-padding");
        Directory.CreateDirectory(target);
        int ordinal = 0;
        while (bytes > 0)
        {
            long length = Math.Min(268435456, bytes);
            using var file = new FileStream(Path.Combine(target, ordinal.ToString(CultureInfo.InvariantCulture) + ".bin"), FileMode.CreateNew, FileAccess.Write);
            file.SetLength(length);
            bytes -= length;
            ordinal++;
        }
    }

    private static long RetainedBytes(string directory) => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);

    private static async Task<ExecutionBudgets> ReadBudgetsAsync()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Parchwell.slnx"))) directory = directory.Parent;
        using JsonDocument limits = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory!.FullName,
            "docs", "operations", "a01-runtime-limits.json"), TestContext.Current.CancellationToken));
        return ExecutionBudgets.Read(limits.RootElement);
    }

    private sealed class ExportStopStore(string directory, List<string> trace) : IArtifactStore
    {
        private readonly PersistentTestStore inner = new();
        public long? FirstRepeatExportBytes { get; private set; }
        public int RepeatExportAttempts { get; private set; }
        public Task<StoredArtifact?> ReadAsync(string container, string path, int maxBytes, CancellationToken cancellationToken) =>
            inner.ReadAsync(container, path, maxBytes, cancellationToken);
        public Task<ArtifactWriteOutcome> WriteAsync(string container, string path, ReadOnlyMemory<byte> bytes,
            string? expectedETag, bool createOnly, CancellationToken cancellationToken)
        {
            if (container == "evidence")
            {
                if (path.Contains("/cleanup/", StringComparison.Ordinal)) trace.Add("cleanup-receipt");
                else
                {
                    if (path.StartsWith("A01/rehearsal-probe/rehearsal-probe-r1/", StringComparison.Ordinal))
                    {
                        RepeatExportAttempts++;
                        FirstRepeatExportBytes ??= RetainedBytes(directory);
                        trace.Add("repeat-export");
                    }
                    throw new IOException("synthetic_export_sentinel");
                }
            }
            return inner.WriteAsync(container, path, bytes, expectedETag, createOnly, cancellationToken);
        }
    }

    private sealed class RecoveryProcess(EnvironmentGeneration generation, Action? addPadding = null) : IProcessRunner
    {
        public int OutputReads { get; private set; }
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("terraform", request.FileName);
            string output = "";
            if (request.Arguments[0] == "state")
                output = JsonSerializer.Serialize(new
                {
                    lineage = generation.Lineage, serial = generation.Serial,
                    resources = new[] { new { type = "azurerm_resource_group", instances = new[] { new { attributes = new { tags = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["repo"] = "eventhubs-kafka-dotnet", ["layer"] = "ephemeral", ["scenario"] = "A01",
                        ["run-id"] = generation.RunId, ["expires-at"] = generation.ExpiresAt.ToString("O", CultureInfo.InvariantCulture)
                    } } } } } }
                });
            if (request.Arguments[0] == "output")
            {
                OutputReads++;
                addPadding?.Invoke();
                // An interruption before the first repeat can leave cluster/storage outputs unavailable.
                output = "{}";
            }
            return Task.FromResult(new ProcessResult(0, output, ""));
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "synthetic-phase-bounds-" + Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true);
    }
}
