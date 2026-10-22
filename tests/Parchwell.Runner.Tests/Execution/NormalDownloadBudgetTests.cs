using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Deployment;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Tests.Deployment;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Repository-required Given_When_Then names.

// Normal downloads and failure collection share the fixed repeat input allowance before transport.
public sealed class NormalDownloadBudgetTests
{
    private const long RepeatLimit = 1073741824;
    private const long ArtifactLimit = 268435456;
    private static readonly string[] BrokerNames = ["sasl_ssl://broker.example.invalid:9093/1"];
    private static readonly string[] PublicationNames = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "index.json"];

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task Given_StartLineInputsAroundRepeatLimit_When_Downloaded_Then_OnlyFittingMetadataReachesTransport(int boundaryBytes, bool accepted)
    {
        using var fixture = new NormalFixture(5);
        await fixture.BindAsync();
        fixture.Process.SetVerifier("broker-inventory.json", 2);
        fixture.Process.SetVerifier("start-line.json", boundaryBytes);

        if (accepted)
        {
            StartLineExecution result = await fixture.Cluster.StartLineAsync(fixture.Context, fixture.Identity, TestContext.Current.CancellationToken);
            Assert.Equal(boundaryBytes, result.Worker.Result!.Length);
            Assert.Equal(2, result.Inventory.Length);
        }
        else
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Cluster.StartLineAsync(fixture.Context, fixture.Identity, TestContext.Current.CancellationToken));

        Assert.Equal(1, fixture.Process.Shows("start-line.json"));
        Assert.Equal(accepted ? 1 : 0, fixture.Process.Downloads("start-line.json"));
        Assert.Equal(1, fixture.Process.Downloads("broker-inventory.json"));
        Assert.Equal(accepted, File.Exists(Path.Combine(fixture.Context.OutputDirectory, "start-line.json")));
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public async Task Given_EarlierVerifierOriginalsAndPublicationAroundRepeatLimit_When_Downloaded_Then_AggregateIsCheckedBeforeTheNextTransfer(int boundaryBytes, bool accepted)
    {
        using var fixture = new NormalFixture(12);
        await fixture.BindAsync();
        await fixture.ObserveProducerAsync();
        fixture.Process.SetVerifier("broker-inventory.json", 2);
        fixture.Process.SetVerifier("start-line.json", boundaryBytes);
        await fixture.Cluster.StartLineAsync(fixture.Context, fixture.Identity, TestContext.Current.CancellationToken);

        if (accepted)
        {
            ProducerPublication publication = await fixture.Cluster.DownloadPublicationAsync(fixture.Context, TestContext.Current.CancellationToken);
            Assert.Equal(PublicationNames.Order(StringComparer.Ordinal), publication.Objects.Keys.Order(StringComparer.Ordinal));
            Assert.All(publication.Objects.Values, bytes => Assert.Single(bytes));
        }
        else
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Cluster.DownloadPublicationAsync(fixture.Context, TestContext.Current.CancellationToken));

        Assert.Equal(1, fixture.Process.Shows("index.json"));
        Assert.Equal(accepted ? 1 : 0, fixture.Process.Downloads("index.json"));
        Assert.Equal(accepted ? 6 : 5, fixture.Process.StorageRequests.Count(request => request.Arguments[2] == "download" && Value(request.Arguments, "--container-name") == "ledger"));
        Assert.Equal(accepted, File.Exists(Path.Combine(fixture.Context.OutputDirectory, "index.json")));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public async Task Given_StartLineAndPublicationLeaveTwoBytes_When_VerifierResultArrives_Then_ResultUsesOnlyRemainingAllowance(int resultBytes, bool accepted)
    {
        using var fixture = new NormalFixture(12);
        await fixture.BindAsync();
        await fixture.ObserveProducerAsync();
        await fixture.Cluster.StartLineAsync(fixture.Context, fixture.Identity, TestContext.Current.CancellationToken);
        await fixture.Cluster.DownloadPublicationAsync(fixture.Context, TestContext.Current.CancellationToken);
        fixture.Process.SetVerifier("result.json", resultBytes);

        if (accepted)
        {
            WorkerExecution result = await fixture.Cluster.VerifyAsync(fixture.Context, fixture.Identity, BrokerNames, TestContext.Current.CancellationToken);
            Assert.Equal(resultBytes, result.Result!.Length);
        }
        else
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Cluster.VerifyAsync(fixture.Context, fixture.Identity, BrokerNames, TestContext.Current.CancellationToken));

        Assert.Equal(1, fixture.Process.Shows("result.json"));
        Assert.Equal(accepted ? 1 : 0, fixture.Process.Downloads("result.json"));
        Assert.Equal(accepted, File.Exists(Path.Combine(fixture.Context.OutputDirectory, "result.json")));
    }

    [Fact]
    public async Task Given_NormalOriginalsAndOverBudgetResult_When_FailureArtifactsAreCollected_Then_CanonicalFilesAreReusedAndResultStillCannotTransfer()
    {
        using var fixture = new NormalFixture(12);
        await fixture.BindAsync();
        await fixture.ObserveProducerAsync();
        await fixture.Cluster.StartLineAsync(fixture.Context, fixture.Identity, TestContext.Current.CancellationToken);
        await fixture.Cluster.DownloadPublicationAsync(fixture.Context, TestContext.Current.CancellationToken);
        fixture.Process.SetVerifier("result.json", 3);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Cluster.VerifyAsync(fixture.Context, fixture.Identity, BrokerNames, TestContext.Current.CancellationToken));

        await fixture.Cluster.PreserveAvailableArtifactsAsync(fixture.Context, TestContext.Current.CancellationToken);

        Assert.Equal(2, fixture.Process.Shows("result.json"));
        Assert.Equal(0, fixture.Process.Downloads("result.json"));
        foreach (string name in PublicationNames.Concat(["start-line.json", "broker-inventory.json"]))
            Assert.Equal(1, fixture.Process.Downloads(name));
        using JsonDocument receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(fixture.Context.OutputDirectory, "failure-artifacts", "collection-receipt.json"), TestContext.Current.CancellationToken));
        JsonElement[] entries = receipt.RootElement.GetProperty("artifacts").EnumerateArray().ToArray();
        Assert.Equal(9, entries.Length);
        foreach (JsonElement entry in entries.Where(entry => entry.GetProperty("name").GetString() != "result.json"))
        {
            Assert.Equal("retained", entry.GetProperty("status").GetString());
            Assert.Equal(entry.GetProperty("name").GetString(), entry.GetProperty("path").GetString());
        }
        Assert.Equal("unavailable", entries.Single(entry => entry.GetProperty("name").GetString() == "result.json").GetProperty("status").GetString());
        Assert.Single(Directory.EnumerateFiles(Path.Combine(fixture.Context.OutputDirectory, "failure-artifacts"), "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Given_RetainedFailureBytes_When_NormalVerifierDownloads_Then_TheirPhysicalBytesConsumeTheSameAllowance()
    {
        using var fixture = new NormalFixture(8);
        await fixture.BindAsync();
        await fixture.Cluster.StartLineAsync(fixture.Context, fixture.Identity, TestContext.Current.CancellationToken);
        string partial = Path.Combine(fixture.Context.OutputDirectory, "failure-artifacts", "ledger", "ledger.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(partial)!);
        await File.WriteAllBytesAsync(partial, new byte[3], TestContext.Current.CancellationToken);
        fixture.Process.SetVerifier("result.json", 2);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Cluster.VerifyAsync(fixture.Context, fixture.Identity, BrokerNames, TestContext.Current.CancellationToken));

        Assert.Equal(1, fixture.Process.Shows("result.json"));
        Assert.Equal(0, fixture.Process.Downloads("result.json"));
        Assert.Equal(3, new FileInfo(partial).Length);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("extraneous")]
    [InlineData("nested")]
    [InlineData("outside_prefix")]
    public async Task Given_InvalidPublicationMemberSet_When_Listed_Then_RejectsBeforeAnyMetadataOrDownload(string invalid)
    {
        using var fixture = new NormalFixture(12);
        await fixture.BindAsync();
        await fixture.ObserveProducerAsync();
        List<string> listing = fixture.Process.Listing;
        switch (invalid)
        {
            case "unknown": listing[^1] = fixture.Process.PublicationPrefix + "unknown.json"; break;
            case "duplicate": listing[^1] = listing[0]; break;
            case "missing": listing.RemoveAt(listing.Count - 1); break;
            case "extraneous": listing.Add(fixture.Process.PublicationPrefix + "extra.json"); break;
            case "nested": listing[^1] = fixture.Process.PublicationPrefix + "nested/index.json"; break;
            case "outside_prefix": listing[^1] = "elsewhere/index.json"; break;
            default: throw new ArgumentOutOfRangeException(nameof(invalid));
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Cluster.DownloadPublicationAsync(fixture.Context, TestContext.Current.CancellationToken));

        Assert.Single(fixture.Process.StorageRequests);
        Assert.Equal("list", fixture.Process.StorageRequests[0].Arguments[2]);
        Assert.Empty(Directory.EnumerateFiles(fixture.Context.OutputDirectory));
    }

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(1, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(0, 0, true)]
    public async Task Given_ActualOneGiBRepeatBoundaryAndRaisedConfiguration_When_VerifierDownloads_Then_FixedCeilingAndZeroLengthMetadataAreRespected(int remainingBytes, int resultBytes, bool accepted)
    {
        using var fixture = new NormalFixture(int.MaxValue, int.MaxValue);
        await fixture.BindAsync();
        long retained = RepeatLimit - remainingBytes;
        for (int index = 0; index < 4; index++)
        {
            long length = Math.Min(retained, ArtifactLimit);
            using var sparse = new FileStream(Path.Combine(fixture.Context.OutputDirectory, PublicationNames[index]), FileMode.CreateNew);
            sparse.SetLength(length);
            retained -= length;
        }
        Assert.Equal(RepeatLimit - remainingBytes, Directory.EnumerateFiles(fixture.Context.OutputDirectory).Sum(path => new FileInfo(path).Length));
        fixture.Process.SetVerifier("result.json", resultBytes);

        if (accepted)
        {
            WorkerExecution result = await fixture.Cluster.VerifyAsync(fixture.Context, fixture.Identity, BrokerNames, TestContext.Current.CancellationToken);
            Assert.Equal(resultBytes, result.Result!.Length);
        }
        else
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Cluster.VerifyAsync(fixture.Context, fixture.Identity, BrokerNames, TestContext.Current.CancellationToken));

        Assert.Equal(1, fixture.Process.Shows("result.json"));
        Assert.Equal(accepted ? 1 : 0, fixture.Process.Downloads("result.json"));
    }

    [Theory]
    [InlineData(268435455, true)]
    [InlineData(268435456, true)]
    [InlineData(268435457, false)]
    public async Task Given_ActualArtifactBoundaryAndRaisedConfiguration_When_StartLineMetadataIsChecked_Then_FixedArtifactCeilingPrecedesTransport(long contentLength, bool accepted)
    {
        using var fixture = new NormalFixture(int.MaxValue, int.MaxValue);
        await fixture.BindAsync();
        fixture.Process.SetVerifier("broker-inventory.json", contentLength, stopAtTransport: true);

        if (accepted)
            await Assert.ThrowsAsync<TransportSentinelException>(() => fixture.Cluster.StartLineAsync(fixture.Context, fixture.Identity, TestContext.Current.CancellationToken));
        else
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Cluster.StartLineAsync(fixture.Context, fixture.Identity, TestContext.Current.CancellationToken));

        Assert.Equal(1, fixture.Process.Shows("broker-inventory.json"));
        Assert.Equal(accepted ? 1 : 0, fixture.Process.Downloads("broker-inventory.json"));
        Assert.Equal(0, fixture.Process.Shows("start-line.json"));
    }

    [Fact]
    public async Task Given_ObservedProducerAndFinalComparisons_When_Releasing_Then_OnePatchContainsPermitAndBothExactFilesBeforeRefreshingThatUid()
    {
        using var fixture = new NormalFixture(checked((int)RepeatLimit));
        await fixture.BindAsync();
        await fixture.ObserveProducerAsync();
        string hash = await fixture.Cluster.GenerateComparisonAsync(fixture.Context, fixture.Identity, TestContext.Current.CancellationToken);
        Assert.Empty(fixture.Process.Patches);
        byte[] permit = JsonSerializer.SerializeToUtf8Bytes(new { release = true, generated_values_sha256 = hash });

        await fixture.Cluster.ReleaseAsync(fixture.Context, permit, TestContext.Current.CancellationToken);

        Assert.Equal(2, fixture.Process.Patches.Count);
        ProcessRequest configuration = fixture.Process.Patches[0], refresh = fixture.Process.Patches[1];
        Assert.Equal(["patch", "configmap", fixture.Context.RunId + "-admission", "-n", "parchwell", "--type=merge", "-p"], configuration.Arguments.Take(7));
        using JsonDocument patch = JsonDocument.Parse(configuration.Arguments[7]);
        Assert.Equal(System.Text.Encoding.UTF8.GetString(permit), patch.RootElement.GetProperty("data").GetProperty("admission.json").GetString());
        JsonElement binary = patch.RootElement.GetProperty("binaryData");
        Assert.Equal(["generated-values.yaml", "verifier-values.yaml"], binary.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        foreach ((string role, string key) in new[] { ("loadgen", "generated-values.yaml"), ("verifier", "verifier-values.yaml") })
        {
            byte[] expected = await File.ReadAllBytesAsync(Path.Combine(fixture.Context.OutputDirectory, "loadgen-deployment", role + "-values.yaml"), TestContext.Current.CancellationToken);
            Assert.Equal(expected, Convert.FromBase64String(binary.GetProperty(key).GetString()!));
        }
        Assert.Equal(["patch", "pod", fixture.Context.RunId + "-pod", "-n", "parchwell", "--type=merge", "-p"], refresh.Arguments.Take(7));
        using JsonDocument annotation = JsonDocument.Parse(refresh.Arguments[7]);
        Assert.Equal("synthetic-pod-uid", annotation.RootElement.GetProperty("metadata").GetProperty("uid").GetString());
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(permit)),
            annotation.RootElement.GetProperty("metadata").GetProperty("annotations").GetProperty("parchwell.io/admission-sha256").GetString());
        Assert.All(fixture.Process.Patches, request => Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout));
    }

    [Theory]
    [InlineData("configmap", 1)]
    [InlineData("pod", 2)]
    public async Task Given_AdmissionMutationFails_When_Releasing_Then_NoFollowingMutationOrRetryIsIssued(string failure, int calls)
    {
        using var fixture = new NormalFixture(checked((int)RepeatLimit));
        await fixture.BindAsync();
        await fixture.ObserveProducerAsync();
        await fixture.Cluster.GenerateComparisonAsync(fixture.Context, fixture.Identity, TestContext.Current.CancellationToken);
        fixture.Process.FailedPatchKind = failure;

        await Assert.ThrowsAsync<IOException>(() => fixture.Cluster.ReleaseAsync(fixture.Context, "{}"u8.ToArray(), TestContext.Current.CancellationToken));

        Assert.Equal(calls, fixture.Process.Patches.Count);
        Assert.Equal(failure, fixture.Process.Patches[^1].Arguments[1]);
    }

    private sealed class NormalFixture : IDisposable
    {
        private readonly DeploymentFixture deployment = new();
        internal NormalFixture(int repeatBytes, int artifactBytes = 268435456)
        {
            Context = new("C0", 1, "synthetic-a01-r1", new string('a', 40), Path.Combine(deployment.Directory, "repeat"));
            Directory.CreateDirectory(Context.OutputDirectory);
            Process = new(Context);
            JsonObject limits = JsonNode.Parse(File.ReadAllText(Path.Combine(DeploymentFixture.RepositoryRoot, "docs/operations/a01-runtime-limits.json")))!.AsObject();
            limits["execution"]!["repeat_input_bytes"] = repeatBytes;
            limits["execution"]!["producer_artifact_bytes"] = artifactBytes;
            Cluster = new(Process, deployment.Generator(), new ChartRenderer(Process), JsonSerializer.SerializeToElement(deployment.Input("C0")),
                "unused-chart", TimeProvider.System, ExecutionBudgets.Read(JsonSerializer.SerializeToElement(limits)), Path.Combine(deployment.Directory, "credentials", "config"));
        }
        internal RepeatContext Context { get; }
        internal WorkerIdentity Identity => new("A01", "C0", Context.RunId, Context.RunId + "-pod", Context.RunId + "-pod", "synthetic-client");
        internal NormalProcess Process { get; }
        internal KubernetesRunCluster Cluster { get; }
        internal Task BindAsync()
        {
            JsonObject outputs = JsonSerializer.SerializeToNode(deployment.Input("C0"))!["ephemeral_outputs"]!.AsObject();
            outputs["aks"] = JsonSerializer.SerializeToNode(new { name = "synthetic", id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/synthetic/providers/Microsoft.ContainerService/managedClusters/synthetic" });
            return Cluster.BindClusterAsync(JsonSerializer.SerializeToElement(outputs), TestContext.Current.CancellationToken);
        }
        internal async Task ObserveProducerAsync()
        {
            PodAdmission admission = await Cluster.EstablishProducerWatchAsync(Context, TestContext.Current.CancellationToken);
            Assert.Equal(Context.RunId + "-pod", (await Cluster.ObserveProducerAsync(Context, admission, TestContext.Current.CancellationToken)).Name);
        }
        public void Dispose() { Cluster.Dispose(); deployment.Dispose(); }
    }

    private sealed class TransportSentinelException : Exception;

    private sealed record Blob(long Length, bool StopAtTransport = false);

    private sealed class NormalProcess : IProcessRunner
    {
        private readonly RepeatContext context;
        private readonly Dictionary<(string Container, string Name), Blob> blobs = [];
        internal NormalProcess(RepeatContext context)
        {
            this.context = context;
            PublicationPrefix = "A01/C0/" + context.RunId + "/" + context.RunId + "-pod/";
            Listing = PublicationNames.Select(name => PublicationPrefix + name).ToList();
            foreach (string name in PublicationNames) blobs[("ledger", PublicationPrefix + name)] = new(1);
            SetVerifier("broker-inventory.json", 2);
            SetVerifier("start-line.json", 2);
            SetVerifier("result.json", 1);
        }
        internal string PublicationPrefix { get; }
        internal List<string> Listing { get; }
        internal List<ProcessRequest> StorageRequests { get; } = [];
        internal List<ProcessRequest> Patches { get; } = [];
        internal string? FailedPatchKind { get; set; }
        internal void SetVerifier(string filename, long length, bool stopAtTransport = false) => blobs[("verifier", context.Phase + "/" + context.RunId + "/" + filename)] = new(length, stopAtTransport);
        internal int Shows(string filename) => Count("show", filename);
        internal int Downloads(string filename) => Count("download", filename);
        private int Count(string operation, string filename) => StorageRequests.Count(request => request.Arguments[2] == operation && Value(request.Arguments, "--name").EndsWith("/" + filename, StringComparison.Ordinal));
        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> args = request.Arguments;
            if (request.FileName == "az" && args[0] == "aks")
            {
                await File.WriteAllTextAsync(Value(args, "--file"), "synthetic kubeconfig", cancellationToken);
                return new(0, "", "");
            }
            if (request.FileName == "kubelogin") return new(0, args[0] == "--version" ? "synthetic version" : "--login azurecli", "");
            if (request.FileName == "helm") return new(0, args[0] == "version" ? "v4.3.1+synthetic" : "synthetic rendered output", "");
            if (request.FileName == "kubectl")
            {
                if (args[0] == "patch")
                {
                    Patches.Add(request);
                    return new(args[1] == FailedPatchKind ? 1 : 0, "", "");
                }
                if (args[1] == "--raw") return new(0, "{\"type\":\"BOOKMARK\",\"object\":{\"metadata\":{\"resourceVersion\":\"2\"}}}", "");
                if (args[0] == "logs") return new(0, "synthetic original stdout", "");
                if (args[0] == "get")
                {
                    bool job = args.Any(value => value.StartsWith("job-name=", StringComparison.Ordinal));
                    return new(0, JsonSerializer.Serialize(new
                    {
                        metadata = new { resourceVersion = "1" },
                        items = new[] { new { metadata = new { name = context.RunId + "-pod", uid = "synthetic-pod-uid" },
                            status = new { containerStatuses = new[] { new { name = job ? "verifier" : "loadgen", restartCount = 0, state = new { terminated = new { exitCode = 0 } } } } } } }
                    }), "");
                }
                return new(0, "", "");
            }
            Assert.Equal("az", request.FileName);
            Assert.Equal("storage", args[0]);
            StorageRequests.Add(request);
            if (args[2] == "list") return new(0, JsonSerializer.Serialize(Listing.Select(name => new { name })), "");
            string name = Value(args, "--name");
            Blob blob = blobs[(Value(args, "--container-name"), name)];
            if (args[2] == "show") return new(0, JsonSerializer.Serialize(new { etag = "\"synthetic-etag\"", contentLength = blob.Length }), "");
            Assert.Equal("download", args[2]);
            Assert.Equal("\"synthetic-etag\"", Value(args, "--if-match"));
            Assert.Equal("false", Value(args, "--overwrite"));
            if (blob.StopAtTransport) throw new TransportSentinelException();
            Assert.InRange(blob.Length, 0, 16);
            await File.WriteAllBytesAsync(Value(args, "--file"), new byte[checked((int)blob.Length)], cancellationToken);
            return new(0, "", "");
        }
    }

    private static string Value(IReadOnlyList<string> args, string name) => args[args.ToList().IndexOf(name) + 1];
}
#pragma warning restore CA1707
