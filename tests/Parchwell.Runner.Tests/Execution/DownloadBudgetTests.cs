using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Deployment;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Tests.Deployment;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Repository-required Given_When_Then names.

public sealed class DownloadBudgetTests
{
    private const long RepeatLimit = 1_073_741_824;
    private const long ArtifactLimit = 268_435_456;

    [Theory]
    [InlineData(1_073_741_823L, 1L, 3, 1)]
    [InlineData(1_073_741_824L, 0L, 3, 3)]
    [InlineData(1_073_741_824L, 1L, 3, 0)]
    [InlineData(1_073_741_825L, 0L, 0, 0)]
    [InlineData(0L, 268_435_457L, 3, 0)]
    public async Task Given_RaisedConfiguredCeilingsAndRetainedOriginals_When_PreservingFailure_Then_OriginalCeilingsApplyBeforeTransfer(
        long retained, long length, int propertyReads, int downloads)
    {
        using var fixture = new DeploymentFixture();
        string[] names = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"];
        for (int index = 0; retained > 0; index++)
        {
            long bytes = Math.Min(retained, ArtifactLimit);
            SparseFile(Path.Combine(fixture.Directory, names[index]), bytes);
            retained -= bytes;
        }
        var process = new BlobProcess(_ => length);
        using var cluster = new KubernetesRunCluster(process, fixture.Generator(), new ChartRenderer(process),
            JsonSerializer.SerializeToElement(new { ephemeral_outputs = new { blob = new { account_url = "https://synthetic.blob.core.windows.net/" } } }),
            "unused-chart", TimeProvider.System, Budgets(int.MaxValue, int.MaxValue));
        var context = new RepeatContext("probe", 1, "synthetic-r1", new string('a', 40), fixture.Directory);

        await cluster.PreserveAvailableArtifactsAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(propertyReads, process.PropertyReads);
        Assert.Equal(downloads, process.Downloads.Count);
        Assert.All(process.Downloads, path => Assert.Equal(length, new FileInfo(path).Length));
        using JsonDocument receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(
            Path.Combine(fixture.Directory, "failure-artifacts", "collection-receipt.json"), TestContext.Current.CancellationToken));
        Assert.False(receipt.RootElement.GetProperty("accepted_counts").GetBoolean());
        Assert.Equal(downloads, receipt.RootElement.GetProperty("artifacts").EnumerateArray()
            .Count(item => item.GetProperty("status").GetString() == "retained"));
    }

    [Fact]
    public async Task Given_OriginalAndPartialDiagnosticCopies_When_PreservingFailure_Then_BothRetainedCopiesConsumeTheBudget()
    {
        using var fixture = new DeploymentFixture();
        SparseFile(Path.Combine(fixture.Directory, "ledger.jsonl"), 1);
        string partial = Path.Combine(fixture.Directory, "failure-artifacts", "ledger", "ledger.jsonl");
        SparseFile(partial, 1);
        var process = new BlobProcess(_ => 1);
        using var cluster = new KubernetesRunCluster(process, fixture.Generator(), new ChartRenderer(process),
            JsonSerializer.SerializeToElement(new { ephemeral_outputs = new { blob = new { account_url = "https://synthetic.blob.core.windows.net/" } } }),
            "unused-chart", TimeProvider.System, Budgets(2, 2));

        await cluster.PreserveAvailableArtifactsAsync(new("probe", 1, "synthetic-r1", new string('a', 40), fixture.Directory), TestContext.Current.CancellationToken);

        Assert.Equal(3, process.PropertyReads);
        Assert.Empty(process.Downloads);
        Assert.Equal(1, new FileInfo(partial).Length);
        Assert.Equal(1, new FileInfo(Path.Combine(fixture.Directory, "ledger.jsonl")).Length);
    }

    [Theory]
    [InlineData(2, 2L, 0L, 0L, 3)]
    [InlineData(2, 2L, 0L, 1L, 2)]
    [InlineData(2, 3L, 0L, 0L, 2)]
    [InlineData(int.MaxValue, 268_435_457L, 268_435_457L, 268_435_457L, 0)]
    public async Task Given_RecoveryOriginalsAtByteBoundaries_When_Collecting_Then_MetadataCeilingsAndZeroLengthEqualityApply(
        int repeatBytes, long startLineBytes, long inventoryBytes, long resultBytes, int downloads)
    {
        using var fixture = new DeploymentFixture();
        var process = new BlobProcess(name => Path.GetFileName(name) switch
        {
            "start-line.json" => startLineBytes,
            "broker-inventory.json" => inventoryBytes,
            "result.json" => resultBytes,
            _ => throw new InvalidOperationException("unexpected_synthetic_blob")
        });
        var terraform = new TerraformEnvironment(process, Path.Combine(fixture.Directory, "ephemeral"), "unused-backend", "unused-tfvars", Path.Combine(fixture.Directory, "plans"));
        var collector = new RecoveryEvidenceCollector(process, terraform, TimeProvider.System, Budgets(repeatBytes, int.MaxValue),
            Path.Combine(fixture.Directory, "credentials", "kubeconfig"));
        PhaseRecord phase = new("applying", "synthetic-reservation", process.Generation.RunId, 0, 0, 0, null, null, null,
            process.Generation.ExpiresAt, [new(1, "synthetic-r1", "running")], null, null, null);

        RecoveryCapture capture = await collector.CollectAsync(process.Generation, "probe", phase,
            Path.Combine(fixture.Directory, "recovery"), TestContext.Current.CancellationToken);

        Assert.Equal(3, process.PropertyReads);
        Assert.Equal(downloads, process.Downloads.Count);
        Assert.True(process.Downloads.Sum(path => new FileInfo(path).Length) <= Math.Min(repeatBytes, RepeatLimit));
        Assert.All(process.Downloads, path => Assert.True(new FileInfo(path).Length <= ArtifactLimit));
        Assert.Contains(capture.Files, file => file.RelativePath == "collection-receipt.json");
    }

    [Theory]
    [InlineData(0L, 9, 1_073_741_823L)]
    [InlineData(1L, 9, 1_073_741_824L)]
    [InlineData(2L, 8, 1_073_741_823L)]
    public async Task Given_RaisedRecoveryCeilings_When_OriginalsReachOneGiBBoundary_Then_OnlyOriginalsWithinTheCeilingTransfer(
        long tailBytes, int downloads, long retainedBytes)
    {
        using var fixture = new DeploymentFixture();
        var process = new BlobProcess(name => Path.GetFileName(name) switch
        {
            "start-line.json" or "broker-inventory.json" or "result.json" => ArtifactLimit,
            "ledger.jsonl" => ArtifactLimit - 1,
            "manifest.json" => tailBytes,
            _ => 0
        });
        var terraform = new TerraformEnvironment(process, Path.Combine(fixture.Directory, "ephemeral"), "unused-backend", "unused-tfvars", Path.Combine(fixture.Directory, "plans"));
        var collector = new RecoveryEvidenceCollector(process, terraform, TimeProvider.System, Budgets(int.MaxValue, int.MaxValue),
            Path.Combine(fixture.Directory, "credentials", "kubeconfig"));
        PhaseRecord phase = new("applying", "synthetic-reservation", process.Generation.RunId, 0, 0, 0, null, null, null,
            process.Generation.ExpiresAt, [new(1, "synthetic-r1", "running", "synthetic-pod")], null, null, null);

        await collector.CollectAsync(process.Generation, "probe", phase,
            Path.Combine(fixture.Directory, "recovery"), TestContext.Current.CancellationToken);

        Assert.Equal(9, process.PropertyReads);
        Assert.Equal(downloads, process.Downloads.Count);
        Assert.Equal(retainedBytes, process.Downloads.Sum(path => new FileInfo(path).Length));
        Assert.Equal(tailBytes <= 1, process.Downloads.Any(path => Path.GetFileName(path) == "manifest.json"));
        using JsonDocument receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(
            Path.Combine(fixture.Directory, "recovery", "collection-receipt.json"), TestContext.Current.CancellationToken));
        Assert.False(receipt.RootElement.GetProperty("accepted_counts").GetBoolean());
        Assert.Equal(tailBytes > 1, receipt.RootElement.GetProperty("reasons").EnumerateArray()
            .Any(reason => reason.GetString() == "recovery_worker_artifact_unavailable"));
    }

    private static ExecutionBudgets Budgets(int repeatBytes, int artifactBytes)
    {
        JsonObject limits = JsonNode.Parse(File.ReadAllText(Path.Combine(DeploymentFixture.RepositoryRoot, "docs/operations/a01-runtime-limits.json")))!.AsObject();
        limits["execution"]!["repeat_input_bytes"] = repeatBytes;
        limits["execution"]!["producer_artifact_bytes"] = artifactBytes;
        return ExecutionBudgets.Read(JsonSerializer.SerializeToElement(limits));
    }

    private static void SparseFile(string path, long length)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        stream.SetLength(length);
    }

    private sealed class BlobProcess(Func<string, long> length) : IProcessRunner
    {
        internal EnvironmentGeneration Generation { get; } = new("synthetic-generation", new(2026, 10, 22, 4, 0, 0, TimeSpan.Zero), "synthetic-lineage", 1);
        internal int PropertyReads { get; private set; }
        internal List<string> Downloads { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> args = request.Arguments;
            if (request.FileName == "terraform")
            {
                string result = args[0] switch
                {
                    "state" => JsonSerializer.Serialize(new
                    {
                        lineage = Generation.Lineage, serial = Generation.Serial,
                        resources = new[] { new { type = "azurerm_resource_group", instances = new[] { new { attributes = new { tags = new Dictionary<string, string>
                        {
                            ["repo"] = "eventhubs-kafka-dotnet", ["layer"] = "ephemeral", ["scenario"] = "A01",
                            ["run-id"] = Generation.RunId,
                            ["expires-at"] = Generation.ExpiresAt.ToString("O", CultureInfo.InvariantCulture)
                        } } } } } }
                    }),
                    "output" => JsonSerializer.Serialize(new { blob = new { account_url = "https://synthetic.blob.core.windows.net/", containers = new { ledger = "ledger", verifier = "verifier" } } }),
                    _ => ""
                };
                return Task.FromResult(new ProcessResult(0, result, ""));
            }
            Assert.Equal("az", request.FileName);
            Assert.Equal("storage", args[0]);
            string name = Value("--name");
            if (args[2] == "show")
            {
                PropertyReads++;
                return Task.FromResult(new ProcessResult(0, JsonSerializer.Serialize(new { etag = "\"synthetic-etag\"", contentLength = length(name) }), ""));
            }
            Assert.Equal("download", args[2]);
            Assert.Equal("\"synthetic-etag\"", Value("--if-match"));
            Assert.Equal("false", Value("--overwrite"));
            string path = Value("--file");
            SparseFile(path, length(name));
            Downloads.Add(path);
            return Task.FromResult(new ProcessResult(0, "", ""));

            string Value(string option) => args[args.ToList().IndexOf(option) + 1];
        }
    }
}

#pragma warning restore CA1707
