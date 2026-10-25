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
}
#pragma warning restore CA1707
