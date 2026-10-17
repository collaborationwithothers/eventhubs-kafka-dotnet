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
    public async Task Given_MismatchedExpectedInstance_When_StartingChildHost_Then_FailsStartupValidation()
    {
        ChildResult result = await RunChildAsync(environment =>
        {
            ConfigureValidInputs(environment, Path.Combine(Path.GetTempPath(), "loadgen-host-output-" + Guid.NewGuid().ToString("N")));
            environment["LoadGen__ExpectedInstanceId"] = "runner-recorded-instance";
        });

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("loadgen_startup_or_host_failure:OptionsValidationException", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("led--ger")]
    [InlineData("-ledger")]
    [InlineData("ledger-")]
    [InlineData("other")]
    public async Task Given_UnsupportedBlobDestination_When_StartingChildHost_Then_FailsStartupValidation(string container)
    {
        ChildResult result = await RunChildAsync(environment =>
        {
            ConfigureValidInputs(environment, Path.Combine(Path.GetTempPath(), "loadgen-host-output-" + Guid.NewGuid().ToString("N")));
            environment["LoadGen__BlobContainer"] = container;
            environment["LoadGen__BlobEndpoint"] = "https://example.invalid/extra-path";
        });

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("loadgen_startup_or_host_failure:OptionsValidationException", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("azure-test")]
    [InlineData("test-AzUrE")]
    [InlineData("testazuretest")]
    public async Task Given_NamespaceViolatingA04Policy_When_StartingChildHost_Then_FailsBeforeCreatingArtifacts(string namespaceName)
    {
        string output = Path.Combine(Path.GetTempPath(), "loadgen-host-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            ChildResult result = await RunChildAsync(environment =>
            {
                ConfigureValidInputs(environment, output);
                environment["LoadGen__NamespaceName"] = namespaceName;
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
    public async Task Given_BlobEndpointWithPath_When_StartingChildHost_Then_FailsStartupValidation()
    {
        ChildResult result = await RunChildAsync(environment =>
        {
            ConfigureValidInputs(environment, Path.Combine(Path.GetTempPath(), "loadgen-host-output-" + Guid.NewGuid().ToString("N")));
            environment["LoadGen__BlobEndpoint"] = "https://example.invalid/extra-path";
        });

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("loadgen_startup_or_host_failure:OptionsValidationException", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_MissingWorkloadTenant_When_StartingChildHost_Then_FailsBeforeProducerAndArtifacts()
    {
        string output = Path.Combine(Path.GetTempPath(), "loadgen-host-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            ChildResult result = await RunChildAsync(environment =>
            {
                ConfigureValidInputs(environment, output);
                environment.Remove("AZURE_TENANT_ID");
            });

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("OptionsValidationException", result.Output, StringComparison.Ordinal);
            Assert.Contains("TenantId is required.", result.Output, StringComparison.Ordinal);
            Assert.Contains("scenario A01 run child-run", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.Hosting.Lifetime", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Application started", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Hosting environment:", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Content root path:", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Application is shutting down", result.Output, StringComparison.Ordinal);
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Theory]
    [InlineData("AZURE_TENANT_ID", "<missing>")]
    [InlineData("AZURE_TENANT_ID", "")]
    [InlineData("AZURE_TENANT_ID", " ")]
    [InlineData("AZURE_CLIENT_ID", "<missing>")]
    [InlineData("AZURE_CLIENT_ID", "")]
    [InlineData("AZURE_CLIENT_ID", "  ")]
    [InlineData("AZURE_FEDERATED_TOKEN_FILE", "<missing>")]
    [InlineData("AZURE_FEDERATED_TOKEN_FILE", "")]
    [InlineData("AZURE_FEDERATED_TOKEN_FILE", "  ")]
    public async Task Given_MissingOrWhitespaceWorkloadIdentity_When_StartingChildHost_Then_FailsBeforeArtifacts(string key, string value)
    {
        string output = Path.Combine(Path.GetTempPath(), "loadgen-host-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            ChildResult result = await RunChildAsync(environment =>
            {
                ConfigureValidInputs(environment, output);
                if (value == "<missing>") environment.Remove(key);
                else environment[key] = value;
            });

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("OptionsValidationException", result.Output, StringComparison.Ordinal);
            Assert.Contains(key switch
            {
                "AZURE_TENANT_ID" => "TenantId is required.",
                "AZURE_CLIENT_ID" => "ClientId is required.",
                _ => "TokenFilePath is required."
            }, result.Output, StringComparison.Ordinal);
            Assert.Contains("scenario A01 run child-run", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.Hosting.Lifetime", result.Output, StringComparison.Ordinal);
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
        foreach (string key in start.Environment.Keys.Where(key => key.StartsWith("LoadGen__", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(key);
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
        Dictionary<string, string> result = values.ToDictionary(pair => "LoadGen__" + pair.Key, pair => pair.Value, StringComparer.Ordinal);
        result["AZURE_TENANT_ID"] = "synthetic-tenant";
        result["AZURE_CLIENT_ID"] = "synthetic-credential-app";
        result["AZURE_FEDERATED_TOKEN_FILE"] = Path.Combine(Path.GetTempPath(), "synthetic-projected-token");
        return result;
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
