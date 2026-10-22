#pragma warning disable CA1707 // Repository-required Given_When_Then names.
using System.Globalization;
using System.Text.Json;
using Parchwell.Runner.Tests.Deployment;
using Xunit;

namespace Parchwell.Runner.Tests.Contracts;

public sealed class WorkflowBudgetContractTests
{
    [Theory]
    [InlineData("gate", "gate_job_seconds", 1)]
    [InlineData("phase", "phase_seconds", 3)]
    [InlineData("export", "export_seconds", 3)]
    [InlineData("destroy", "destroy_seconds", 3)]
    public void Given_ScenarioWorkflowRunnerWrappers_When_ComparedWithDeclaredBudgets_Then_EveryWrapperUsesItsEnclosingBudget(
        string command, string budget, int expectedCount)
    {
        using JsonDocument limits = JsonDocument.Parse(File.ReadAllText(Path.Combine(DeploymentFixture.RepositoryRoot, "docs/operations/a01-runtime-limits.json")));
        int seconds = limits.RootElement.GetProperty("execution").GetProperty(budget).GetInt32();
        string[] wrappers = File.ReadAllLines(Path.Combine(DeploymentFixture.RepositoryRoot, "docs/operations/workflows/scenario.yml.txt"))
            .Where(line => line.Contains("Parchwell.Runner.dll " + command + " --", StringComparison.Ordinal)).ToArray();

        Assert.Equal(expectedCount, wrappers.Length);
        foreach (string wrapper in wrappers)
            Assert.StartsWith("timeout " + seconds.ToString(CultureInfo.InvariantCulture) + " dotnet ", wrapper.TrimStart(), StringComparison.Ordinal);
        if (command == "gate")
        {
            Assert.Equal(90, seconds);
            Assert.Equal(60, limits.RootElement.GetProperty("execution").GetProperty("gate_seconds").GetInt32());
            Assert.Equal(TimeSpan.FromSeconds(60), TimeSpan.Parse(limits.RootElement.GetProperty("verifier_gate").GetProperty("ProcessLifetime").GetString()!, CultureInfo.InvariantCulture));
        }
    }
}
#pragma warning restore CA1707
