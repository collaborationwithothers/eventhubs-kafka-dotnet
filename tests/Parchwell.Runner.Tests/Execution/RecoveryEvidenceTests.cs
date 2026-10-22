using System.Globalization;
using System.Text;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Run.Rehearsal;
using Parchwell.Runner.Tests.Cost;
using Parchwell.Verifier.Common.Window;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class RecoveryEvidenceTests
{
    private static readonly string[] RecoveryCommands = ["init", "state"];
    [Theory]
    [InlineData("cleanup-upload")]
    [InlineData("ledger-update")]
    public async Task Given_SuccessfulDestroyAndFailedReceiptPersistence_When_FreshSweepRuns_Then_ClosesRegisteredCleanupWithoutAnotherDestroy(string failure)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Store.FailPersistenceOnce = failure;
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<IOException>(() => fixture.Sweep.ExecuteAsync(fixture.Output, token));
        Assert.Equal(1, fixture.Process.Destroys);
        Assert.Null((await fixture.Ledger.ReadAsync(token)).Phases["probe"].DestroyReceipt);
        int requests = fixture.Process.Requests.Count;

        Assert.True(await fixture.FreshSweep().ExecuteAsync(Path.Combine(fixture.Output, "fresh"), token));
        PhaseRecord recovered = (await fixture.Ledger.ReadAsync(token)).Phases["probe"];
        Assert.Equal("destroyed", recovered.State);
        Assert.True(recovered.DestroyReceipt!.Complete);
        Assert.Equal("already_empty", recovered.DestroyReceipt.Reason);
        Assert.Null(recovered.DestroyReceipt.RecoveryExport);
        Assert.Equal("interrupted", recovered.Repeats[0].State);
        Assert.Null(recovered.PrivateExport);
        Assert.Equal(1, fixture.Process.Destroys);
        Assert.All(fixture.Process.Requests.Skip(requests), request =>
        {
            Assert.Equal("terraform", request.FileName);
            Assert.Contains(request.Arguments[0], RecoveryCommands);
        });
        Assert.False(await fixture.FreshSweep().ExecuteAsync(Path.Combine(fixture.Output, "again"), token));
    }

    [Theory]
    [InlineData("ambiguous")]
    [InlineData("not-applied")]
    [InlineData("not-aged")]
    [InlineData("at-expiry")]
    public async Task Given_EmptyStateWithoutUniqueAgedAppliedOwnership_When_SweepRuns_Then_NeverRecordsCleanup(string condition)
    {
        await using var fixture = await Fixture.CreateAsync();
        var token = TestContext.Current.CancellationToken;
        fixture.Process.Empty = true;
        if (condition is "ambiguous" or "not-applied")
        {
            LedgerDocument document = await fixture.Ledger.ReadAsync(token);
            PhaseRecord probe = document.Phases["probe"];
            if (condition == "ambiguous") document.Phases["C0"] = probe with
            {
                ReservationId = "A01/C0", EnvironmentRunId = "other-generation",
                Repeats = Enumerable.Range(1, 5).Select(ordinal => new RepeatRecord(ordinal, "other-generation-r" + ordinal)).ToArray()
            };
            else document.Phases["probe"] = probe with
            {
                State = "reserved", ApplyStartedUtc = null, ExpiresAt = null,
                Repeats = Enumerable.Range(1, 5).Select(ordinal => new RepeatRecord(ordinal, "rehearsal-probe-r" + ordinal)).ToArray()
            };
            StoredArtifact original = fixture.Store.Objects[("ledger", RunLedger.BlobPath)];
            fixture.Store.Objects[("ledger", RunLedger.BlobPath)] = original with { Bytes = JsonSerializer.SerializeToUtf8Bytes(document, LedgerJson.Options) };
        }
        else fixture.Clock.Now = fixture.Clock.Now.AddHours(condition == "at-expiry" ? -1 : -2);
        int writes = fixture.Store.Objects.Count;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.FreshSweep().ExecuteAsync(fixture.Output, token));
        Assert.Null((await fixture.Ledger.ReadAsync(token)).Phases["probe"].DestroyReceipt);
        Assert.Equal(writes, fixture.Store.Objects.Count);
        Assert.Equal(0, fixture.Process.Destroys);
    }

    [Fact]
    public async Task Given_UnresolvedCleanupAndNewGeneration_When_FreshSweepRuns_Then_DoesNotCloseOrDestroyNewGeneration()
    {
        await using var fixture = await Fixture.CreateAsync();
        var token = TestContext.Current.CancellationToken;
        fixture.Store.FailPersistenceOnce = "ledger-update";
        await Assert.ThrowsAsync<IOException>(() => fixture.Sweep.ExecuteAsync(fixture.Output, token));
        fixture.Process.Empty = false;
        fixture.Process.Changed = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.FreshSweep().ExecuteAsync(fixture.Output, token));
        Assert.Null((await fixture.Ledger.ReadAsync(token)).Phases["probe"].DestroyReceipt);
        Assert.Equal(1, fixture.Process.Destroys);
    }

    [Fact]
    public async Task Given_EmptyStateReplacedBeforeReceipt_When_FreshSweepRechecks_Then_NeverClosesAnotherGeneration()
    {
        await using var fixture = await Fixture.CreateAsync();
        var token = TestContext.Current.CancellationToken;
        fixture.Store.FailPersistenceOnce = "cleanup-upload";
        await Assert.ThrowsAsync<IOException>(() => fixture.Sweep.ExecuteAsync(fixture.Output, token));
        fixture.Process.AfterStateRead = () => { fixture.Process.Empty = false; fixture.Process.Changed = true; };
        int objects = fixture.Store.Objects.Count;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.FreshSweep().ExecuteAsync(fixture.Output, token));
        Assert.Null((await fixture.Ledger.ReadAsync(token)).Phases["probe"].DestroyReceipt);
        Assert.Equal(objects, fixture.Store.Objects.Count);
        Assert.Equal(1, fixture.Process.Destroys);
    }

    [Fact]
    public async Task Given_EmptyFreshRunner_When_SweepingAgedGeneration_Then_RecoversOriginalBytesPrivatelyBeforeGuardedDestroy()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.True(await fixture.Sweep.ExecuteAsync(fixture.Output, TestContext.Current.CancellationToken));
        PhaseRecord phase = (await fixture.Ledger.ReadAsync(TestContext.Current.CancellationToken)).Phases["probe"];
        PrivateExportReceipt recovery = Assert.IsType<PrivateExportReceipt>(phase.DestroyReceipt!.RecoveryExport);
        Assert.False(recovery.Complete);
        Assert.Null(phase.PrivateExport);
        Assert.Equal("interrupted", phase.Repeats[0].State);
        Assert.Equal(1, fixture.Process.Destroys);
        string prefix = recovery.ManifestPath[..^"_private-export-manifest.json".Length];
        Assert.Equal(fixture.Process.Blobs["ledger/A01/C0/rehearsal-probe-r1/rehearsal-probe-r1-pod/ledger.jsonl"],
            fixture.Store.Objects[("evidence", prefix + "rehearsal-probe-r1/blobs/ledger/ledger.jsonl")].Bytes);
        Assert.Equal(Encoding.UTF8.GetBytes(fixture.Process.StateA), fixture.Store.Objects[("evidence", prefix + "terraform/state.private.json")].Bytes);
        Assert.Equal(Encoding.UTF8.GetBytes(fixture.Process.Outputs), fixture.Store.Objects[("evidence", prefix + "terraform/outputs.private.json")].Bytes);
        using JsonDocument receipt = JsonDocument.Parse(fixture.Store.Objects[("evidence", prefix + "collection-receipt.json")].Bytes);
        Assert.False(receipt.RootElement.GetProperty("accepted_counts").GetBoolean());
        Assert.Equal("unavailable", receipt.RootElement.GetProperty("historical_watch_continuity").GetString());
        Assert.Contains(fixture.Store.Objects.Keys, key => key.Path.EndsWith("/pods.json", StringComparison.Ordinal));
        Assert.Contains(fixture.Store.Objects.Keys, key => key.Path.EndsWith("/pod-events.json", StringComparison.Ordinal));
        Assert.True(fixture.Trace.FindIndex(item => item == "destroy") > fixture.Trace.FindLastIndex(item => item == "recovery-readback"));
        Assert.DoesNotContain(fixture.Process.Requests, request => request.FileName == "kubectl" && request.Arguments[0] is "apply" or "create" or "patch");
        Assert.All(fixture.Process.Requests.Where(request => request.FileName == "kubectl" && request.Arguments[0] == "logs"), request =>
        {
            Assert.Contains("-l", request.Arguments);
            Assert.Contains("--all-containers=true", request.Arguments);
            Assert.Contains("--tail=-1", request.Arguments);
            Assert.Contains("--prefix=true", request.Arguments);
            Assert.DoesNotContain(request.Arguments, argument => argument.StartsWith("job/", StringComparison.Ordinal));
        });
        Assert.DoesNotContain(fixture.Store.Objects.Keys, key => key.Path.Contains("kubeconfig", StringComparison.Ordinal));
        Assert.All(fixture.Process.Requests.Where(request => request.Arguments.Contains("--account-name")), request => Assert.Contains("stworkerfixture", request.Arguments));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("oversize")]
    [InlineData("corrupt-readback")]
    [InlineData("cancellation")]
    [InlineData("partial-outputs")]
    [InlineData("pod-log-error")]
    public async Task Given_UnavailableEvidence_When_RecoveryFails_Then_RetainsAvailableBytesAndStillAttemptsCleanup(string failure)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Process.Failure = failure;
        fixture.Store.CorruptOnce = failure == "corrupt-readback";
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (failure == "cancellation") fixture.Process.Cancel = cancellation.Cancel;
        if (failure is "corrupt-readback" or "cancellation")
            await Assert.ThrowsAsync<IOException>(() => fixture.Sweep.ExecuteAsync(fixture.Output, cancellation.Token));
        else Assert.True(await fixture.Sweep.ExecuteAsync(fixture.Output, cancellation.Token));
        Assert.Equal(1, fixture.Process.Destroys);
        Assert.Null((await fixture.Ledger.ReadAsync(TestContext.Current.CancellationToken)).Phases["probe"].PrivateExport);
        if (failure == "oversize")
            Assert.DoesNotContain(fixture.Process.Requests, request => request.Arguments.Contains("download") && request.Arguments.Contains("A01/C0/rehearsal-probe-r1/rehearsal-probe-r1-pod/ledger.jsonl"));
        if (failure == "pod-log-error")
            Assert.Contains(fixture.Store.Objects, entry => entry.Key.Path.EndsWith(".stderr.log", StringComparison.Ordinal) && Encoding.UTF8.GetString(entry.Value.Bytes) == "original unavailable pod log diagnostic");
        if (failure is "missing" or "partial-outputs")
            Assert.Contains(fixture.Store.Objects.Keys, key => key.Path.EndsWith("/state.private.json", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("after-outputs")]
    [InlineData("after-private-export")]
    public async Task Given_AgedAChangesToB_When_RecoveryRechecks_Then_NeverDestroysB(string point)
    {
        await using var fixture = await Fixture.CreateAsync();
        if (point == "after-outputs") fixture.Process.Failure = "changed-generation";
        else fixture.Store.AfterRecoveryManifest = () => fixture.Process.Changed = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.Sweep.ExecuteAsync(fixture.Output, TestContext.Current.CancellationToken));
        Assert.Equal(0, fixture.Process.Destroys);
        if (point == "after-outputs") Assert.DoesNotContain(fixture.Process.Requests, request => request.FileName == "az");
    }

    [Fact]
    public async Task Given_ExistingOriginalManifestAndStaleLocalFiles_When_Recovering_Then_SideSnapshotCannotOverwriteOrIncludeThem()
    {
        await using var fixture = await Fixture.CreateAsync();
        Directory.CreateDirectory(fixture.Output);
        string originalFile = Path.Combine(fixture.Output, "original.dat");
        await File.WriteAllBytesAsync(originalFile, new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        PrivateExportReceipt original = await new EvidenceExporter(fixture.Store, TimeProvider.System).ExportAsync("rehearsal-probe",
            [new("original.dat", originalFile)], TestContext.Current.CancellationToken);
        byte[] originalManifest = fixture.Store.Objects[("evidence", original.ManifestPath)].Bytes.ToArray();
        await fixture.Ledger.MarkExportedAsync("probe", "rehearsal-probe", original, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(fixture.Output, "stale-other-generation.json"), "unrelated", TestContext.Current.CancellationToken);
        Assert.True(await fixture.Sweep.ExecuteAsync(fixture.Output, TestContext.Current.CancellationToken));
        Assert.Equal(originalManifest, fixture.Store.Objects[("evidence", original.ManifestPath)].Bytes);
        Assert.DoesNotContain(fixture.Store.Objects.Keys, key => key.Path.EndsWith("stale-other-generation.json", StringComparison.Ordinal));
        Assert.Equal(original, (await fixture.Ledger.ReadAsync(TestContext.Current.CancellationToken)).Phases["probe"].PrivateExport);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(string root, RecordingProcess process, RecordingStore store, RunLedger ledger, SweepExecutor sweep, List<string> trace)
        { Root = root; Process = process; Store = store; Ledger = ledger; Sweep = sweep; Trace = trace; }
        private string Root { get; }
        internal string Output => Path.Combine(Root, "output");
        internal RecordingProcess Process { get; }
        internal RecordingStore Store { get; }
        internal RunLedger Ledger { get; }
        internal SweepExecutor Sweep { get; }
        internal List<string> Trace { get; }
        internal Func<SweepExecutor> FreshSweep { get; private set; } = null!;
        internal Clock Clock { get; private set; } = null!;
        internal static async Task<Fixture> CreateAsync()
        {
            string root = Path.Combine(Path.GetTempPath(), "recovery-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string fixtures = Path.Combine(root, "fixtures");
            await WorkerWireFixtureTests.ProduceAsync(fixtures, TestContext.Current.CancellationToken);
            var clock = new Clock(); var trace = new List<string>(); var store = new RecordingStore(trace);
            var ledger = new RunLedger(store, clock);
            await ledger.InitializeAsync(PhaseReservationTests.Input(), WireFixtures.BuiltSourceCommit(), true, true, TestContext.Current.CancellationToken);
            await ledger.ReserveAsync("probe", "rehearsal-probe", PhaseReservationTests.Input(), TestContext.Current.CancellationToken);
            await ledger.MarkApplyStartedAsync("probe", "rehearsal-probe", TestContext.Current.CancellationToken);
            await ledger.MarkRepeatStartedAsync("probe", "rehearsal-probe", 1, cancellationToken: TestContext.Current.CancellationToken);
            await ledger.RecordAdmissionAsync("probe", "rehearsal-probe", 1, "rehearsal-probe-r1-pod", TestContext.Current.CancellationToken);
            PhaseRecord phase = (await ledger.ReadAsync(TestContext.Current.CancellationToken)).Phases["probe"];
            clock.Now = clock.Now.AddHours(5);
            var process = new RecordingProcess(new("rehearsal-probe", phase.ExpiresAt!.Value, "synthetic-lineage", 7), trace);
            foreach (string path in Directory.EnumerateFiles(Path.Combine(fixtures, "probe", "r1")))
            {
                string name = Path.GetFileName(path);
                if (name is "broker-inventory.json" or "result.json") process.Blobs["verifier/probe/rehearsal-probe-r1/" + name] = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
                else if (name.EndsWith(".json", StringComparison.Ordinal) || name.EndsWith(".jsonl", StringComparison.Ordinal))
                    process.Blobs["ledger/A01/C0/rehearsal-probe-r1/rehearsal-probe-r1-pod/" + name] = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            }
            process.Blobs["verifier/probe/rehearsal-probe-r1/start-line.json"] = StartLineNoteCodec.Write(new(1,
                new("A01", "C0", "rehearsal-probe-r1", "rehearsal-probe-r1-pod", "rehearsal-probe-r1-pod", "rehearsal-probe-r1-pod"),
                "document-events", [new(0, 4)], "2026-10-22T00:00:00Z", WireFixtures.BuiltSourceCommit(), "2.16.0", "2.16.0"), new(16777216, 100, 2048));
            string repo = FindRepository();
            using JsonDocument limits = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(repo, "docs/operations/a01-runtime-limits.json"), TestContext.Current.CancellationToken));
            ExecutionBudgets budgets = ExecutionBudgets.Read(limits.RootElement);
            var terraform = new TerraformEnvironment(process, Path.Combine(root, "ephemeral"), "private-backend", "private-tfvars", Path.Combine(root, "plans"), clock);
            var collector = new RecoveryEvidenceCollector(process, terraform, clock, budgets, Path.Combine(root, "credentials", "kubeconfig"));
            var sweep = new SweepExecutor(ledger, terraform, new GenerationGuard(terraform, clock), new EvidenceExporter(store, clock), clock, collector, budgets);
            return new(root, process, store, ledger, sweep, trace)
            {
                Clock = clock,
                FreshSweep = () =>
                {
                    var freshTerraform = new TerraformEnvironment(process, Path.Combine(root, "ephemeral"), "private-backend", "private-tfvars", Path.Combine(root, "fresh-plans"), clock);
                    var freshCollector = new RecoveryEvidenceCollector(process, freshTerraform, clock, budgets, Path.Combine(root, "fresh-credentials", "kubeconfig"));
                    return new(new RunLedger(store, clock), freshTerraform, new GenerationGuard(freshTerraform, clock), new EvidenceExporter(store, clock), clock, freshCollector, budgets);
                }
            };
        }
        public ValueTask DisposeAsync() { Directory.Delete(Root, true); return ValueTask.CompletedTask; }
        private static string FindRepository()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Parchwell.slnx"))) directory = directory.Parent;
            return directory!.FullName;
        }
    }

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingStore(List<string> trace) : IArtifactStore
    {
        private readonly PersistentTestStore inner = new();
        internal Dictionary<(string Container, string Path), StoredArtifact> Objects => inner.Objects;
        internal bool CorruptOnce { get; set; }
        internal Action? AfterRecoveryManifest { get; set; }
        internal string? FailPersistenceOnce { get; set; }
        public async Task<StoredArtifact?> ReadAsync(string container, string path, int maxBytes, CancellationToken cancellationToken)
        {
            StoredArtifact? stored = await inner.ReadAsync(container, path, maxBytes, cancellationToken);
            if (container == "evidence" && path.Contains("/recovery/", StringComparison.Ordinal))
            {
                trace.Add("recovery-readback");
                if (CorruptOnce) { CorruptOnce = false; return new([0], "corrupt"); }
            }
            return stored;
        }
        public async Task<ArtifactWriteOutcome> WriteAsync(string container, string path, ReadOnlyMemory<byte> bytes, string? expectedETag, bool createOnly, CancellationToken cancellationToken)
        {
            bool fail = FailPersistenceOnce == "cleanup-upload" && path.Contains("/cleanup/", StringComparison.Ordinal);
            if (FailPersistenceOnce == "ledger-update" && container == "ledger")
            {
                using JsonDocument document = JsonDocument.Parse(bytes);
                fail = document.RootElement.GetProperty("phases").GetProperty("probe").GetProperty("destroy_receipt") is { ValueKind: JsonValueKind.Object } receipt &&
                    receipt.GetProperty("complete").GetBoolean();
            }
            if (fail) { FailPersistenceOnce = null; throw new IOException("synthetic_receipt_persistence_failure"); }
            ArtifactWriteOutcome result = await inner.WriteAsync(container, path, bytes, expectedETag, createOnly, cancellationToken);
            if (path.Contains("/recovery/", StringComparison.Ordinal) && path.EndsWith("/_private-export-manifest.json", StringComparison.Ordinal)) AfterRecoveryManifest?.Invoke();
            return result;
        }
    }

    private sealed class RecordingProcess(EnvironmentGeneration generation, List<string> trace) : IProcessRunner
    {
        internal string? Failure { get; set; }
        internal Action? Cancel { get; set; }
        internal Action? AfterStateRead { get; set; }
        internal bool Changed { get; set; }
        internal int Destroys { get; private set; }
        internal List<ProcessRequest> Requests { get; } = [];
        internal Dictionary<string, byte[]> Blobs { get; } = new(StringComparer.Ordinal);
        internal bool Empty { get; set; }
        internal string StateA => State(generation);
        internal string Outputs { get; } = JsonSerializer.Serialize(new
        {
            aks = new { id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/synthetic/providers/Microsoft.ContainerService/managedClusters/synthetic", name = "synthetic" },
            blob = new { account_url = "https://stworkerfixture.blob.core.windows.net/", containers = new { ledger = "ledger", verifier = "verifier" } }
        });
        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Requests.Add(request);
            IReadOnlyList<string> args = request.Arguments;
            string result = "";
            if (request.FileName == "terraform")
            {
                switch (args[0])
                {
                    case "state": result = Empty ? "" : State(Changed ? generation with { RunId = "generation-b", Serial = 8 } : generation); AfterStateRead?.Invoke(); break;
                    case "output": result = Failure == "partial-outputs" ? "{}" : Outputs; if (Failure == "changed-generation") Changed = true; break;
                    case "show": result = JsonSerializer.Serialize(new { prior_state = new { values = new { root_module = new { resources = new[] { new { type = "azurerm_resource_group", values = new { tags = Tags(generation) } } } } } } }); break;
                    case "apply": trace.Add("destroy"); Destroys++; Empty = true; break;
                }
            }
            else if (request.FileName == "az" && args[0] == "aks") await File.WriteAllTextAsync(Value(args, "--file"), "synthetic kubeconfig", cancellationToken);
            else if (request.FileName == "kubelogin") result = args[0] == "--version" ? "synthetic-version" : "--login azurecli";
            else if (request.FileName == "kubectl") result = args[0] == "logs" ? "original synthetic worker stdout\n" : "{\"metadata\":{\"continue\":\"\"},\"items\":[{\"metadata\":{\"name\":\"rehearsal-probe-r1-pod\",\"uid\":\"original-pod-uid\"},\"status\":{\"phase\":\"Failed\",\"containerStatuses\":[{\"restartCount\":0,\"state\":{\"terminated\":{\"exitCode\":1}}}]}}]}";
            else if (request.FileName == "az" && args[0] == "storage")
            {
                string name = Value(args, "--name"), key = Value(args, "--container-name") + "/" + name;
                if (Failure == "cancellation") { Cancel?.Invoke(); cancellationToken.ThrowIfCancellationRequested(); }
                if (!Blobs.TryGetValue(key, out byte[]? bytes) || Failure == "missing" && name.EndsWith("/result.json", StringComparison.Ordinal)) return new(1, "", "BlobNotFound");
                if (args[2] == "show") result = JsonSerializer.Serialize(new { etag = "\"original-etag\"", contentLength = Failure == "oversize" && name.EndsWith("/ledger.jsonl", StringComparison.Ordinal) ? 268435457L : bytes.Length });
                else
                {
                    Assert.Contains("--if-match", args); Assert.Equal("\"original-etag\"", Value(args, "--if-match"));
                    await File.WriteAllBytesAsync(Value(args, "--file"), bytes, cancellationToken);
                }
            }
            return new(0, result, Failure == "pod-log-error" && request.FileName == "kubectl" && args[0] == "logs" ? "original unavailable pod log diagnostic" : "");
        }
        private static string Value(IReadOnlyList<string> arguments, string name) => arguments[arguments.ToList().IndexOf(name) + 1];
        private static Dictionary<string, string> Tags(EnvironmentGeneration value) => new(StringComparer.Ordinal)
        { ["repo"] = "eventhubs-kafka-dotnet", ["layer"] = "ephemeral", ["scenario"] = "A01", ["run-id"] = value.RunId, ["expires-at"] = value.ExpiresAt.ToString("O", CultureInfo.InvariantCulture) };
        private static string State(EnvironmentGeneration value) => JsonSerializer.Serialize(new
        { lineage = value.Lineage, serial = value.Serial, resources = new[] { new { type = "azurerm_resource_group", instances = new[] { new { attributes = new { tags = Tags(value) } } } } } });
    }
}
