#pragma warning disable CA1707 // Repository-required Given_When_Then names.
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Tests.Deployment;
using Xunit;

namespace Parchwell.Runner.Tests.Contracts;

public sealed class WorkflowAuthenticationContractTests
{
    private const string LoginStep = """
        - name: Existing environment OIDC login
          uses: azure/login@7184910d9eb2b1c5e48f7073824a90609bb9b6d6
          with:
            client-id: ${{ vars.AZURE_CLIENT_ID }}
            tenant-id: ${{ vars.AZURE_TENANT_ID }}
            subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
        """;

    private const string InstallationStep = """
        - name: Enable renewable serialized Azure CLI session
          id: azure_session
          shell: bash
          run: |
            set -euo pipefail
            umask 077
            real_az="$(command -v az)"
            test -x "$real_az"
            session_dir="$(mktemp -d "$RUNNER_TEMP/a01-oidc.XXXXXX")"
            chmod 700 "$session_dir"
            test -x "$GITHUB_WORKSPACE/scripts/azure-cli-session/az"
            printf 'PARCHWELL_REAL_AZ=%s\n' "$real_az" >> "$GITHUB_ENV"
            printf 'PARCHWELL_AZ_SESSION_DIRECTORY=%s\n' "$session_dir" >> "$GITHUB_ENV"
            printf '%s\n' "$GITHUB_WORKSPACE/scripts/azure-cli-session" >> "$GITHUB_PATH"
        """;

    [Fact]
    public void Given_LifecycleSweeper_When_Authenticated_Then_RenewalPrecedesIndependentCleanup()
    {
        string[] lines = ReadJob("sweeper.yml.txt", "sweep");
        string[] steps = ReadSteps(lines);
        string login = Assert.Single(steps, step => step == LoginStep);
        string setup = Assert.Single(steps, step => step == InstallationStep);
        int setupIndex = Array.IndexOf(steps, setup);
        Assert.Equal(Array.IndexOf(steps, login) + 1, setupIndex);
        Assert.Single(lines, line => line == "        id: azure_session");
        Assert.Single(lines, line => line.Contains("PARCHWELL_REAL_AZ", StringComparison.Ordinal));
        Assert.Single(lines, line => line.Contains("PARCHWELL_AZ_SESSION_DIRECTORY", StringComparison.Ordinal));
        Assert.Contains("        id: prepare", lines);
        Assert.Equal("""
            - name: Read dispatch activity and end time
              id: history
              shell: bash
              env:
                GITHUB_TOKEN: ${{ github.token }}
              run: python3 infra/scripts/lifecycle/dispatch-state.py
            """, steps[setupIndex + 1]);
        Assert.Equal("""
            - name: Destroy idle ephemeral infrastructure
              id: destroy
              if: always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success' && steps.history.outputs.idle == 'true'
              shell: bash
              run: bash infra/scripts/lifecycle/lifecycle.sh destroy-ephemeral
            """, steps[setupIndex + 2]);
        Assert.Equal("""
            - name: Stop idle AKS cluster
              id: stop
              if: always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success' && steps.history.outputs.stop_eligible == 'true'
              shell: bash
              run: bash infra/scripts/lifecycle/lifecycle.sh stop-cluster
            """, steps[setupIndex + 3]);
        Assert.DoesNotContain("steps.destroy", steps[setupIndex + 3], StringComparison.Ordinal);
        Assert.Contains("\n  if: always()\n", steps[setupIndex + 4], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_AzureCliSessionOfflineSuite_When_ExecutedWithinBoundedProcess_Then_AllAuthenticationCasesPass()
    {
        var runner = new BoundedProcessRunner(TimeProvider.System);
        ProcessResult result = await runner.RunAsync(new("python3",
            ["-m", "unittest", "discover", "-s", "scripts/tests", "-p", "test_azure_cli_session.py", "-v"],
            TimeSpan.FromSeconds(60), 1048576, DeploymentFixture.RepositoryRoot), TestContext.Current.CancellationToken);
        Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
        Assert.False(result.TimedOut);
    }

    private static string[] ReadJob(string file, string job)
    {
        string[] lines = File.ReadAllLines(Path.Combine(DeploymentFixture.RepositoryRoot, "docs/operations/workflows", file));
        string heading = "  " + job + ":";
        Assert.Single(lines, line => line == heading);
        int start = Array.IndexOf(lines, heading);
        int end = Array.FindIndex(lines, start + 1, line => line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("   ", StringComparison.Ordinal));
        return lines[start..(end < 0 ? lines.Length : end)];
    }

    private static string[] ReadSteps(string[] lines)
    {
        int[] starts = Enumerable.Range(0, lines.Length).Where(index => lines[index].StartsWith("      - name: ", StringComparison.Ordinal)).ToArray();
        return starts.Select((start, index) => string.Join('\n', lines[start..(index + 1 < starts.Length ? starts[index + 1] : lines.Length)]
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("      #", StringComparison.Ordinal))
            .Select(line => line[6..]))).ToArray();
    }
}
#pragma warning restore CA1707
