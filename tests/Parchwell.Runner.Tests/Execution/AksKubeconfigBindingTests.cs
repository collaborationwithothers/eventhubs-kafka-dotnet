using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;
using Xunit;

#pragma warning disable CA1707

namespace Parchwell.Runner.Tests.Execution;

public sealed class AksKubeconfigBindingTests
{
    private static readonly string[] ConversionArguments = ["convert-kubeconfig", "-l", "azurecli"];
    private const string Subscription = "00000000-0000-0000-0000-000000000000";
    private const string ResourceId = "/subscriptions/" + Subscription + "/resourceGroups/rg-synthetic/providers/Microsoft.ContainerService/managedClusters/aks-synthetic";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AppliedCluster_When_ContextIsBound_Then_ExplicitPrivateCredentialsAndNamespacePrecedeWorkers(bool wrapped)
    {
        string directory = NewDirectory();
        string path = Path.Combine(directory, "config");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, "unrelated inherited context", TestContext.Current.CancellationToken);
        var process = new FakeProcess(path);
        try
        {
            IReadOnlyDictionary<string, string> overlay = await AksKubeconfigBinding.BindAsync(process,
                Outputs(ResourceId, "aks-synthetic", wrapped), path, TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

            Assert.Equal(path, overlay["KUBECONFIG"]);
            Assert.Equal(5, process.Requests.Count);
            Assert.Equal("az", process.Requests[0].FileName);
            Assert.Equal(new[] { "aks", "get-credentials", "--subscription", Subscription, "--resource-group", "rg-synthetic",
                "--name", "aks-synthetic", "--file", path, "--overwrite-existing", "--format", "exec", "--only-show-errors" }, process.Requests[0].Arguments);
            Assert.DoesNotContain("--admin", process.Requests[0].Arguments);
            Assert.Equal(ConversionArguments, process.Requests[3].Arguments);
            Assert.Equal("kubectl", process.Requests[4].FileName);
            Assert.Equal(new[] { "apply", "-f", Path.Combine(directory, "namespace.json") }, process.Requests[4].Arguments);
            using JsonDocument ns = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory, "namespace.json"), TestContext.Current.CancellationToken));
            Assert.Equal("Namespace", ns.RootElement.GetProperty("kind").GetString());
            Assert.Equal("parchwell", ns.RootElement.GetProperty("metadata").GetProperty("name").GetString());
            Assert.All(process.Requests, request =>
            {
                Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout);
                Assert.Equal(path, request.Environment!["KUBECONFIG"]);
            });
            Assert.True(File.Exists(Path.Combine(directory, "kubelogin-version.txt")));
            Assert.True(File.Exists(Path.Combine(directory, "kubelogin-convert-help.txt")));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("invalid", "aks-synthetic")]
    [InlineData(ResourceId, "different-cluster")]
    [InlineData("/subscriptions/not-a-guid/resourceGroups/rg-synthetic/providers/Microsoft.ContainerService/managedClusters/aks-synthetic", "aks-synthetic")]
    [InlineData("/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic/providers/Microsoft.Storage/managedClusters/aks-synthetic", "aks-synthetic")]
    [InlineData(ResourceId + "/child", "aks-synthetic")]
    [InlineData("x" + ResourceId, "aks-synthetic")]
    public void Given_MalformedOrMismatchedAppliedIdentity_When_Parsed_Then_ClusterBindingIsRejected(string id, string name)
    {
        Assert.Throws<InvalidDataException>(() => AksKubeconfigBinding.ReadTarget(Outputs(id, name)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Given_ContextPreparationFailure_When_Bound_Then_NoSubsequentCommandRuns(int failureIndex)
    {
        string directory = NewDirectory();
        string path = Path.Combine(directory, "config");
        var process = new FakeProcess(path, failureIndex);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => AksKubeconfigBinding.BindAsync(process,
                Outputs(ResourceId, "aks-synthetic"), path, TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken));
            Assert.Equal(failureIndex + 1, process.Requests.Count);
            Assert.DoesNotContain(process.Requests, request => request.Arguments.Contains("pods"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Given_NoExplicitPrivateFile_When_Bound_Then_NoExternalCommandRuns()
    {
        var process = new FakeProcess("unused");
        await Assert.ThrowsAsync<InvalidDataException>(() => AksKubeconfigBinding.BindAsync(process,
            Outputs(ResourceId, "aks-synthetic"), "relative-kubeconfig", TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken));
        Assert.Empty(process.Requests);
    }

    private static JsonElement Outputs(string id, string name, bool wrapped = false)
    {
        object aks = new { id, name, node_pools = new[] { "system", "workload", "loadgen" } };
        return JsonSerializer.SerializeToElement(new { aks = wrapped ? new { value = aks } : aks });
    }
    private static string NewDirectory() => Path.Combine(Path.GetTempPath(), "parchwell-private-context-" + Guid.NewGuid().ToString("N"));

    private sealed class FakeProcess(string path, int failureIndex = -1) : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];
        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = Requests.Count;
            Requests.Add(request);
            if (index == failureIndex) return new(1, "", "synthetic failure");
            if (index == 0)
            {
                Assert.False(File.Exists(path));
                await File.WriteAllTextAsync(path, "synthetic private kubeconfig", cancellationToken);
            }
            return new(0, index == 1 ? "kubelogin synthetic-version" : index == 2 ? "--login supports azurecli" : "", "");
        }
    }
}
