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

    private const string ExportStep = """
        - name: Export available private evidence unless the run was cancelled
          if: always() && !cancelled() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success'
          shell: bash
          run: |
            timeout 300 dotnet src/Parchwell.Runner/bin/Release/net10.0/Parchwell.Runner.dll export --input "$A01_OUTPUT/runtime.json" --output "$A01_OUTPUT/phase" > "$A01_OUTPUT/export.log" 2>&1
        """;

    private const string DestroyStep = """
        - name: Always attempt generation-checked destroy
          if: always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success'
          shell: bash
          run: |
            timeout 900 dotnet src/Parchwell.Runner/bin/Release/net10.0/Parchwell.Runner.dll destroy --input "$A01_OUTPUT/runtime.json" --output "$A01_OUTPUT/phase" > "$A01_OUTPUT/destroy.log" 2>&1
        """;

    private const string SweepStep = """
        - name: Export aged generation and attempt guarded destroy
          shell: bash
          run: |
            timeout 1500 dotnet src/Parchwell.Runner/bin/Release/net10.0/Parchwell.Runner.dll sweep --input "$A01_OUTPUT/runtime.json" --output "$A01_OUTPUT/recovery" > "$A01_OUTPUT/sweep.log" 2>&1
        """;

    [Fact]
    public void Given_AuthenticatedWorkflowJobs_When_SessionSetupIsRequired_Then_InstallationAndFallbacksPreserveFailClosedBudgets()
    {
        (string File, string Job, string FirstWorkload)[] jobs =
        [
            ("scenario.yml.txt", "probe", "Publish full-SHA worker images and record registry digests"),
            ("scenario.yml.txt", "full-C0", "Use immutable receipts from the probe publication"),
            ("scenario.yml.txt", "full-C3", "Use immutable receipts from the probe publication"),
            ("sweeper.yml.txt", "sweep", "Export aged generation and attempt guarded destroy")
        ];
        foreach ((string file, string job, string firstWorkload) in jobs)
        {
            string[] lines = ReadJob(file, job);
            string[] steps = ReadSteps(lines);
            string setup = Assert.Single(steps, step => step.StartsWith("- name: Enable renewable serialized Azure CLI session\n", StringComparison.Ordinal));
            Assert.Equal(InstallationStep, setup);
            Assert.Single(lines, line => line == "        id: azure_session");
            Assert.Single(lines, line => line.Contains("PARCHWELL_REAL_AZ", StringComparison.Ordinal));
            Assert.Single(lines, line => line.Contains("PARCHWELL_AZ_SESSION_DIRECTORY", StringComparison.Ordinal));
            int setupIndex = Array.IndexOf(steps, setup);
            Assert.InRange(setupIndex, 1, steps.Length - 2);
            Assert.Equal(LoginStep, steps[setupIndex - 1]);
            Assert.StartsWith("- name: " + firstWorkload + "\n", steps[setupIndex + 1], StringComparison.Ordinal);
            Assert.DoesNotContain("\n  if:", steps[setupIndex + 1], StringComparison.Ordinal);
            Assert.DoesNotContain("\n  continue-on-error:", steps[setupIndex + 1], StringComparison.Ordinal);
            Assert.Contains("          export A01_OUTPUT=\"$RUNNER_TEMP/a01-" + (job == "sweep" ? "sweep" : "$A01_PHASE") + "\"", lines);

            if (job == "sweep")
            {
                Assert.Equal(SweepStep, steps[setupIndex + 1]);
                continue;
            }

            string export = Assert.Single(steps, step => step.StartsWith("- name: Export available private evidence unless the run was cancelled\n", StringComparison.Ordinal));
            string destroy = Assert.Single(steps, step => step.StartsWith("- name: Always attempt generation-checked destroy\n", StringComparison.Ordinal));
            Assert.Equal(ExportStep, export);
            Assert.Equal(DestroyStep, destroy);
            Assert.DoesNotContain("steps.phase", export, StringComparison.Ordinal);
            Assert.DoesNotContain("steps.phase", destroy, StringComparison.Ordinal);
        }

        using JsonDocument limits = JsonDocument.Parse(File.ReadAllText(Path.Combine(DeploymentFixture.RepositoryRoot, "docs/operations/a01-runtime-limits.json")));
        int apiSeconds = limits.RootElement.GetProperty("execution").GetProperty("api_seconds").GetInt32();
        Assert.True(apiSeconds <= 60, "API budget must not exceed the Azure CLI session invocation budget.");
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
