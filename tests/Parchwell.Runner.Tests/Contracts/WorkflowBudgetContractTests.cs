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

    [Fact]
    public void Given_RunnerWorkflowFailures_When_Forwarded_Then_ExactDiagnosticBlocksPreserveCommandsAndCopies()
    {
        (string File, string[] Jobs, (string Command, int Seconds, int Count)[] Wrappers)[] workflows =
        [
            ("scenario.yml", ["probe", "full-C0", "full-C3"],
                [("phase", 9600, 3), ("export", 300, 3), ("destroy", 900, 3), ("gate", 90, 1)]),
            ("sweeper.yml", ["sweep"], [("sweep", 1500, 1)])
        ];
        foreach ((string file, string[] jobs, (string Command, int Seconds, int Count)[] wrappers) in workflows)
        {
            string installed = Path.Combine(DeploymentFixture.RepositoryRoot, ".github/workflows", file);
            string copy = Path.Combine(DeploymentFixture.RepositoryRoot, "docs/operations/workflows", file + ".txt");
            Assert.Equal(File.ReadAllBytes(installed), File.ReadAllBytes(copy));
            string[] lines = File.ReadAllLines(copy);
            Assert.Equal(wrappers.Sum(wrapper => wrapper.Count),
                lines.Count(line => line.Contains("Parchwell.Runner.dll ", StringComparison.Ordinal)));

            foreach ((string command, int seconds, int count) in wrappers)
            {
                int[] starts = Enumerable.Range(0, lines.Length)
                    .Where(index => lines[index].Contains("Parchwell.Runner.dll " + command + " --", StringComparison.Ordinal)).ToArray();
                Assert.Equal(count, starts.Length);
                string outputDirectory = command == "sweep" ? "recovery" : "phase";
                string expected = $$"""
                    timeout {{seconds.ToString(CultureInfo.InvariantCulture)}} dotnet src/Parchwell.Runner/bin/Release/net10.0/Parchwell.Runner.dll {{command}} --input "$A01_OUTPUT/runtime.json" --output "$A01_OUTPUT/{{outputDirectory}}" > "$A01_OUTPUT/{{command}}.log" 2>&1 || {
                      if [ "${PARCHWELL_DEBUG_FAILURES:-}" = true ]; then
                        cat "$A01_OUTPUT/{{command}}.log"
                      else
                        grep '^runner_execution_failed' "$A01_OUTPUT/{{command}}.log" || true
                      fi
                      exit 1
                    }
                    """;
                foreach (int start in starts)
                {
                    Assert.InRange(start, 0, lines.Length - 8);
                    Assert.Equal(expected, string.Join('\n', lines[start..(start + 8)].Select(line => line[10..])));
                }
            }

            foreach (string job in jobs)
            {
                string heading = "  " + job + ":";
                Assert.Single(lines, line => line == heading);
                int start = Array.IndexOf(lines, heading);
                int end = Array.FindIndex(lines, start + 1,
                    line => line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("   ", StringComparison.Ordinal));
                string[] jobLines = lines[start..(end < 0 ? lines.Length : end)];
                string debug = Assert.Single(jobLines, line => line.Contains("PARCHWELL_DEBUG_FAILURES:", StringComparison.Ordinal));
                Assert.Equal("      PARCHWELL_DEBUG_FAILURES: ${{ vars.PARCHWELL_DEBUG_FAILURES }}", debug);
            }
        }
    }
}
#pragma warning restore CA1707
