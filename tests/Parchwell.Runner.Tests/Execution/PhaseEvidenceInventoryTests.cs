using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Deployment;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Run.Rehearsal;
using Parchwell.Runner.Tests.Cost;
using Parchwell.Runner.Tests.Deployment;
using Parchwell.Verifier.Common.Window;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Repository-required Given_When_Then names.

public sealed class PhaseEvidenceInventoryTests
{
    [Fact]
    public async Task Given_FiveRepeatsWithActualDeploymentAndLegacyWorkerFixtures_When_ExportingPrivateProvenance_Then_ManifestFitsUnchangedEntryLimit()
    {
        using var deployment = new DeploymentFixture();
        string fixtureDirectory = Path.Combine(deployment.Directory, "wire");
        await WorkerWireFixtureTests.ProduceAsync(fixtureDirectory, TestContext.Current.CancellationToken);
        WireFixtures fixtures = await WireFixtures.LoadAsync(fixtureDirectory, TestContext.Current.CancellationToken);
        string output = Path.Combine(deployment.Directory, "phase");
        var inputs = deployment.Input("probe");
        string provenance = Path.Combine(deployment.Directory, "provenance");
        foreach (string name in new[] { "image-receipts.json", "image-publication.log", "tools/azure-cli.json", "tools/kubectl.json", "tools/kubelogin-version.txt", "tools/kubelogin-convert-help.txt", "tools/aks-get-credentials-help.txt", "tools/terraform.json", "tools/blob-upload-help.txt", "tools/blob-download-help.txt", "tools/helm.json" })
        {
            string path = Path.Combine(provenance, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "synthetic original private receipt\n", TestContext.Current.CancellationToken);
        }
        inputs["cost_file"] = Path.Combine(deployment.Directory, "cost.json");
        inputs["private_provenance_directory"] = provenance;
        await File.WriteAllBytesAsync((string)inputs["cost_file"]!, JsonSerializer.SerializeToUtf8Bytes(PhaseReservationTests.Input()), TestContext.Current.CancellationToken);
        JsonElement runtime = JsonSerializer.SerializeToElement(inputs);
        Assert.True((await PrivateProvenanceCollector.CollectAsync(runtime, JsonSerializer.SerializeToUtf8Bytes(inputs), output, TestContext.Current.CancellationToken)).Complete);
        await new TerraformEnvironment(new OutputCaptureTransport(), Path.Combine(deployment.Directory, "infra", "ephemeral"),
            "synthetic.tfbackend", "synthetic.tfvars", Path.Combine(output, "terraform"))
            .ReadOutputsAsync(TestContext.Current.CancellationToken);
        var store = new PersistentTestStore();
        var ledger = new RunLedger(store, TimeProvider.System);
        var environment = new RehearsalEnvironment();
        var cluster = new GeneratingCluster(deployment, fixtures);
        var executor = new PhaseExecutor(ledger, environment, new RepeatExecutor(cluster, TimeProvider.System),
            new EvidenceExporter(store, TimeProvider.System), new GenerationGuard(environment, TimeProvider.System), TimeProvider.System);

        PhaseExecution result = await executor.ExecuteAsync(new("probe", "rehearsal-probe", fixtures.SourceCommit, output,
            PhaseReservationTests.Input(), true), TestContext.Current.CancellationToken);

        Assert.Equal("completed", result.Result);
        Assert.Equal(5, result.Repeats.Count);
        Assert.True(result.Exported);
        Assert.True(result.Destroyed);
        Assert.All(result.Repeats, repeat => Assert.False(repeat.Outcome.CountsAccepted));
        var receipt = (await ledger.ReadAsync(TestContext.Current.CancellationToken)).Phases["probe"].PrivateExport!;
        Assert.InRange(receipt.VerifiedFileCount, 1, EvidenceExporter.MaxEntries);
        byte[] manifestBytes = store.Objects[("evidence", receipt.ManifestPath)].Bytes;
        using JsonDocument manifest = JsonDocument.Parse(manifestBytes);
        string[] paths = manifest.RootElement.GetProperty("files").EnumerateArray().Select(file => file.GetProperty("path").GetString()!).ToArray();
        Assert.Equal(receipt.VerifiedFileCount, paths.Length);
        Assert.Equal(14, paths.Count(path => path.StartsWith("private-provenance/", StringComparison.Ordinal)));
        Assert.Contains("terraform/outputs.private.json", paths);
        Assert.Contains("terraform/state.private.json", paths);
        for (int ordinal = 1; ordinal <= 5; ordinal++)
        {
            string prefix = "rehearsal-probe-r" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/";
            foreach (string mode in new[] { "loadgen", "start-line", "verify" })
            {
                Assert.Equal(6, paths.Count(path => path.StartsWith(prefix + mode + "-deployment/", StringComparison.Ordinal)));
                Assert.Contains(prefix + mode + "-render/rendered.yaml", paths);
            }
            foreach (string name in new[] { "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "index.json", "start-line.json", "broker-inventory.json", "result.json" })
                Assert.Contains(prefix + name, paths);
        }
    }

    private sealed class GeneratingCluster(DeploymentFixture deployment, WireFixtures fixtures) : IRunCluster
    {
        private readonly RehearsalCluster inner = new(fixtures);
        public Task PreflightAsync(JsonElement outputs, CancellationToken cancellationToken) => inner.PreflightAsync(outputs, cancellationToken);
        public Task<PodAdmission> EstablishProducerWatchAsync(RepeatContext context, CancellationToken cancellationToken) => inner.EstablishProducerWatchAsync(context, cancellationToken);
        public async Task CreateProducerAsync(RepeatContext context, CancellationToken cancellationToken)
        { await GenerateAsync(context, "loadgen", true, cancellationToken); await inner.CreateProducerAsync(context, cancellationToken); }
        public Task<ProducerPod> ObserveProducerAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken) => inner.ObserveProducerAsync(context, admission, cancellationToken);
        public async Task<string> GenerateComparisonAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken)
        { await GenerateAsync(context, "loadgen", false, cancellationToken); return await inner.GenerateComparisonAsync(context, identity, cancellationToken); }
        public async Task<StartLineExecution> StartLineAsync(RepeatContext context, WorkerIdentity identity, CancellationToken cancellationToken)
        {
            await GenerateAsync(context, "start-line", true, cancellationToken);
            StartLineExecution start = await inner.StartLineAsync(context, identity, cancellationToken);
            byte[] boundary = StartLineNoteCodec.Write(new(1,
                new(identity.ScenarioId, identity.ConfigurationId, identity.RunId, identity.InstanceId, identity.ExpectedInstanceId, identity.ClientId),
                "document-events", [new(0, 4)], "2026-10-22T00:00:00Z", context.SourceCommit, "2.16.0", "2.16.0"), new(16777216, 100, 2048));
            return start with { Worker = start.Worker with { Result = boundary } };
        }
        public Task ReleaseAsync(RepeatContext context, byte[] permit, CancellationToken cancellationToken) => inner.ReleaseAsync(context, permit, cancellationToken);
        public Task<WorkerExecution> WaitProducerAsync(RepeatContext context, CancellationToken cancellationToken) => inner.WaitProducerAsync(context, cancellationToken);
        public Task<ProducerPublication> DownloadPublicationAsync(RepeatContext context, CancellationToken cancellationToken) => inner.DownloadPublicationAsync(context, cancellationToken);
        public async Task<WorkerExecution> VerifyAsync(RepeatContext context, WorkerIdentity identity, IReadOnlyList<string> brokers, CancellationToken cancellationToken)
        { await GenerateAsync(context, "verify", true, cancellationToken); return await inner.VerifyAsync(context, identity, brokers, cancellationToken); }
        public Task PreserveAvailableArtifactsAsync(RepeatContext context, CancellationToken cancellationToken) => inner.PreserveAvailableArtifactsAsync(context, cancellationToken);
        public Task CompleteProducerWatchAsync(RepeatContext context, PodAdmission admission, CancellationToken cancellationToken) => inner.CompleteProducerWatchAsync(context, admission, cancellationToken);
        private async Task GenerateAsync(RepeatContext context, string mode, bool render, CancellationToken token)
        {
            Dictionary<string, object?> input = deployment.Input(context.Phase, mode);
            input["source_commit"] = context.SourceCommit;
            input["run_id"] = context.RunId;
            input["producer_pod_name"] = context.RunId + "-pod";
            input["images"] = new { loadgen = new { source_commit = context.SourceCommit, digest = "sha256:" + new string('a', 64) }, verifier = new { source_commit = context.SourceCommit, digest = "sha256:" + new string('b', 64) } };
            DeploymentFiles files = await deployment.Generator().GenerateAsync(JsonSerializer.SerializeToElement(input), Path.Combine(context.OutputDirectory, mode + "-deployment"), token);
            if (render) await new ChartRenderer(new RenderTransport()).RenderAsync(files, "synthetic-chart", Path.Combine(context.OutputDirectory, mode + "-render"), token);
        }
    }

    private sealed class OutputCaptureTransport : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ProcessResult(0, request.Arguments[0] == "output" ? "{}" : "{\"format_version\":\"1.0\",\"values\":{}}", ""));
        }
    }

    private sealed class RenderTransport : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ProcessResult(0, request.Arguments[0] == "version" ? "v4.3.1+synthetic" : "synthetic rendered transport output", ""));
        }
    }
}
#pragma warning restore CA1707
