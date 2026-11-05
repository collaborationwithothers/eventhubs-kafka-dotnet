#pragma warning disable CA1707 // Repository-required Given_When_Then names.
using Xunit;

namespace Parchwell.ArchitectureTests.DeploymentContracts;

public sealed class WorkflowBudgetContractTests
{
    private static string RepositoryRoot =>
        Environment.GetEnvironmentVariable("PARCHWELL_REPOSITORY_ROOT")
        ?? throw new InvalidOperationException("Missing repository root. Run ./scripts/verify-dotnet.sh.");

    [Fact]
    public void Given_LifecycleSweeper_When_Replaced_Then_MirrorAndBoundedCleanupContractRemain()
    {
        string installed = Path.Combine(RepositoryRoot, ".github/workflows/sweeper.yml");
        string copy = Path.Combine(RepositoryRoot, "docs/operations/workflows/sweeper.yml.txt");
        Assert.Equal(File.ReadAllBytes(installed), File.ReadAllBytes(copy));
        string[] lines = File.ReadAllLines(installed);
        Assert.Single(lines, line => line.Trim() == "timeout-minutes: 120");
        Assert.Single(lines, line => line.Trim() == "run: bash infra/scripts/lifecycle/lifecycle.sh destroy-ephemeral");
        Assert.Single(lines, line => line.Trim() == "run: bash infra/scripts/lifecycle/lifecycle.sh stop-cluster");
        string workflow = File.ReadAllText(installed);
        foreach (string obsolete in new[] { "Parchwell.Runner.dll", "dotnet ", "PARCHWELL_DEBUG_FAILURES", "A01_OUTPUT", "start-cluster", "apply-cluster", "apply-ephemeral" })
            Assert.DoesNotContain(obsolete, workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_B10SequentialWorkers_When_ComputingConservativeEnvelope_Then_ExplicitOperationalCapDoesNotPromiseWorstCaseCompletion()
    {
        int overhead = 120 + 60 + 90 + 210 + 120 + 30;
        int discovery = 30;
        int conservative = 5 * (150 + 1140 + 1140 + 750 + 4 * overhead + discovery)
            + (150 + overhead) + (750 + overhead) + (90 + overhead) + 60;
        const int orchestrationCap = 18000;
        Assert.Equal(630, overhead);
        Assert.Equal(31590, conservative);
        Assert.True(conservative > orchestrationCap);
        Assert.True(orchestrationCap < 360 * 60);
        string phase = File.ReadAllText(Path.Combine(RepositoryRoot, "infra/scripts/phase/phase.py"));
        Assert.Contains("B10_ORCHESTRATION_SECONDS = 18000", phase, StringComparison.Ordinal);
        string workflowPath = Path.Combine(RepositoryRoot, ".github/workflows/scenario.yml");
        string mirrorPath = Path.Combine(RepositoryRoot, "docs/operations/workflows/scenario.yml.txt");
        Assert.Equal(File.ReadAllBytes(workflowPath), File.ReadAllBytes(mirrorPath));
        string[] workflow = File.ReadAllLines(workflowPath);
        Assert.Equal(5, workflow.Count(line => line.Trim() == "timeout-minutes: 360"));
        Assert.Equal(4, workflow.Count(line => line.Trim() == "run: bash infra/scripts/lifecycle/lifecycle.sh destroy-ephemeral"));
    }
}
#pragma warning restore CA1707
