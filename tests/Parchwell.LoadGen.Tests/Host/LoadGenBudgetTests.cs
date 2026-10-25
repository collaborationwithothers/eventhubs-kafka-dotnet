using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Telemetry;
using Parchwell.LoadGen;
using Parchwell.LoadGen.Run.Input;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class LoadGenBudgetTests
{
    [Fact]
    public void Given_RuntimeLimitsAndBoundWorkerOptions_When_ComputingProcessEnvelope_Then_PreservesApprovedWorkerAndJobBounds()
    {
        string root = FindRepositoryRoot();
        using JsonDocument limits = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "docs/operations/a01-runtime-limits.json")));
        JsonElement loadgen = limits.RootElement.GetProperty("loadgen");
        JsonElement execution = limits.RootElement.GetProperty("execution");
        string output = Path.Combine(Path.GetTempPath(), "loadgen-budget-" + Guid.NewGuid().ToString("N"));
        Dictionary<string, string> values = LoadGenHostProcessTests.CreateInputs(output);
        values["LoadGen__ProducerDrainTimeout"] = loadgen.GetProperty("ProducerDrainTimeout").GetString()!;
        values["LoadGen__LedgerDrainTimeout"] = loadgen.GetProperty("LedgerDrainTimeout").GetString()!;
        values["LoadGen__StatisticsCompletionTimeout"] = loadgen.GetProperty("StatisticsCompletionTimeout").GetString()!;
        values["LoadGen__EvidenceCompletionTimeout"] = loadgen.GetProperty("EvidenceCompletionTimeout").GetString()!;
        values["LoadGen__CleanupTimeout"] = loadgen.GetProperty("CleanupTimeout").GetString()!;
        values["LoadGen__UploadTimeout"] = loadgen.GetProperty("UploadTimeout").GetString()!;
        values["LoadGen__ProcessLifetimeTimeout"] = loadgen.GetProperty("ProcessLifetimeTimeout").GetString()!;
        string[] args = values.Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();

        using IHost host = Program.BuildHostCore(args, _ => { }, (_, configure) => configure(new ParchwellTelemetryOptions()));
        LoadGenHostOptions options = host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value;

        int finalisation =
            300 + // Producer drain
            10 + // Ledger drain
            20 + // Statistics completion
            10 + // Native producer disposal
            20 + // Evidence writer drain
            20 + // Capabilities save
            10 + // Evidence readback
            10 + // Ledger readback
            10 + // Manifest save
            30 + // Normal upload
            5 * 10 + // Five bounded diagnostic snapshots
            30 + // Diagnostic upload
            10 + // Session cleanup
            10 + // Host stop/disposal
            10 + // Watchdog cleanup reserve
            10;  // Completion upload
        int completePath = 10 + 40 + 240 + finalisation; // Registration, setup/resolution, offered schedule, finalisation.

        Assert.Equal(TimeSpan.FromSeconds(300), options.ProducerDrainTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), options.LedgerDrainTimeout);
        Assert.Equal(TimeSpan.FromSeconds(20), options.StatisticsCompletionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(20), options.EvidenceCompletionTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), options.CleanupTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.UploadTimeout);
        Assert.Equal(TimeSpan.FromSeconds(900), options.ProcessLifetimeTimeout);
        Assert.Equal(560, finalisation);
        Assert.Equal(850, completePath);
        Assert.True(completePath <= execution.GetProperty("loadgen_seconds").GetInt32());
        Assert.Equal(900, execution.GetProperty("loadgen_seconds").GetInt32());
        Assert.Equal(1140, execution.GetProperty("loadgen_job_seconds").GetInt32());
        Assert.Equal(1140, execution.GetProperty("loadgen_seconds").GetInt32() +
            execution.GetProperty("termination_grace_seconds").GetInt32() + 210);
        Assert.Equal(1500, execution.GetProperty("repeat_seconds").GetInt32());
        Assert.Equal(9750, execution.GetProperty("phase_seconds").GetInt32());
        Assert.Equal(240, execution.GetProperty("phase_job_minutes").GetInt32());
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs/operations/a01-runtime-limits.json"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository_root_not_found");
    }
}
#pragma warning restore CA1707
