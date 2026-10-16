using System.Diagnostics;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class LoadGenHostProcessTests
{
    [Fact]
    public async Task Given_MissingRequiredConfiguration_When_StartingChildHost_Then_ReturnsNonzeroPromptly()
    {
        ChildResult result = await RunChildAsync(configure: null);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("loadgen_startup_or_host_failure", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_LedgerBudgetBeyondReaderLimit_When_StartingChildHost_Then_FailsValidationBeforeCreatingArtifacts()
    {
        string output = Path.Combine(Path.GetTempPath(), "loadgen-host-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            ChildResult result = await RunChildAsync(environment =>
            {
                ConfigureValidInputs(environment, output);
                environment["LoadGen__MaxLedgerBytes"] = ((long)int.MaxValue + 1)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
            });

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("loadgen_startup_or_host_failure:OptionsValidationException", result.Output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task Given_ValidInputsWithoutProductionAuthentication_When_StartingChildHost_Then_BlocksBeforeNativeProducerAndExitsNonzero()
    {
        string output = Path.Combine(Path.GetTempPath(), "loadgen-host-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            ChildResult result = await RunChildAsync(environment => ConfigureValidInputs(environment, output));

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("production_oauthbearer_refresh_handler_unavailable", result.Output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    private static async Task<ChildResult> RunChildAsync(Action<IDictionary<string, string?>>? configure)
    {
        string assembly = Path.Combine(AppContext.BaseDirectory, "Parchwell.LoadGen.dll");
        Assert.True(File.Exists(assembly), "The load generator child assembly must be copied beside the test assembly.");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = FindRepositoryRoot()
        };
        start.ArgumentList.Add(assembly);
        configure?.Invoke(start.Environment);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("child_process_failed_to_start");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            throw new TimeoutException("loadgen_child_process_did_not_stop_within_15_seconds");
        }
        return new ChildResult(process.ExitCode,
            await stdout.WaitAsync(TestContext.Current.CancellationToken) + await stderr.WaitAsync(TestContext.Current.CancellationToken));
    }

    internal static Dictionary<string, string> CreateInputs(string output)
    {
        string root = FindRepositoryRoot();
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ScenarioId"] = "A01", ["ConfigurationId"] = "C0", ["RunId"] = "child-run",
            ["ExpectedInstanceId"] = "worker-pod", ["ActualInstanceId"] = "worker-pod",
            ["PodName"] = "worker-pod", ["ClientId"] = "worker-pod", ["NamespaceName"] = "unit-test",
            ["EnrichmentGroupId"] = "worker-group", ["ScenarioSourceRoot"] = root, ["OutputRoot"] = output,
            ["BlobEndpoint"] = "https://example.invalid", ["BlobContainer"] = "ledger",
            ["ExpectedBrokerNamesCsv"] = "broker-0", ["MaxDocumentCharacters"] = "100000",
            ["MaxNestingDepth"] = "32", ["MaxScalarCharacters"] = "10000", ["MaxParserEvents"] = "10000",
            ["MaxAttempts"] = "100", ["MaxPayloadBytes"] = "1024", ["MaxRowBytes"] = "8192",
            ["MaxManifestBytes"] = "65536", ["MaxLedgerBytes"] = "2000000", ["MaxLedgerReadbackBytes"] = "2000000",
            ["LedgerQueueCapacity"] = "128", ["EvidenceQueueCapacity"] = "128", ["MaxReportBytes"] = "8192",
            ["MaxEvidenceFileBytes"] = "2000000", ["MaxDiagnostics"] = "128", ["MaxBrokerSeries"] = "64",
            ["MaxSnapshotCharacters"] = "65536", ["MaxArtifactBytes"] = "2000000",
            ["MaxAggregateArtifactBytes"] = "8000000", ["MaxScheduleLateness"] = "00:00:00",
            ["ProducerDrainTimeout"] = "00:00:01", ["LedgerDrainTimeout"] = "00:00:01",
            ["EvidenceCompletionTimeout"] = "00:00:01", ["StatisticsCompletionTimeout"] = "00:00:01",
            ["UploadTimeout"] = "00:00:01", ["CleanupTimeout"] = "00:00:03",
            ["ProcessLifetimeTimeout"] = "00:00:10", ["RequireStatisticsEvidence"] = "false",
            ["RequireRequestEvidence"] = "false"
        };
        return values.ToDictionary(pair => "LoadGen__" + pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static void ConfigureValidInputs(IDictionary<string, string?> environment, string output)
    {
        foreach ((string key, string value) in CreateInputs(output)) environment[key] = value;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "scenarios", "A01", "scenario.yaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository_root_not_found_for_host_process_test");
    }

    private sealed record ChildResult(int ExitCode, string Output);
}
#pragma warning restore CA1707
