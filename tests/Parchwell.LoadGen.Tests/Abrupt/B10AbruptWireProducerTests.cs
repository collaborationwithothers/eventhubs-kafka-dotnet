using Azure;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Confluent.Kafka;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Ledger;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
public sealed class B10AbruptWireProducerTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Given_SyntheticProducer_When_SupervisorAndSuccessorRun_Then_ActualBytesBindBothPublications()
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-produced-" + Guid.NewGuid().ToString("N"));
        try { await ProduceAsync(root, TestContext.Current.CancellationToken); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Given_WireOutputDirectory_When_Run_Then_WritesProducedArtifacts()
    {
        string? directory = Environment.GetEnvironmentVariable("PARCHWELL_B10_ABRUPT_WIRE_OUTPUT");
        if (directory is null) return;
        output.WriteLine($"Confluent.Kafka assembly={typeof(IProducer<,>).Assembly.GetName().Version}; librdkafka={Library.VersionString}; runtime={Environment.Version}");
        await ProduceAsync(directory, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_TerminalAfterCheckpointBeforeEof_When_Reconciling_Then_NoReplaySelectionIsPublished()
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-late-terminal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => ProduceAsync(root, TestContext.Current.CancellationToken, true));
            Assert.Equal("kill_too_late:no_unconfirmed_attempt", failure.Message);
            Assert.False(File.Exists(Path.Combine(root, "first", "termination.json")));
            Assert.False(Directory.Exists(Path.Combine(root, "successor")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Given_ProductionBindingFaults_When_ActualHostValidatesSuccessor_Then_ProducerContinuationNeverStarts()
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-invalid-successor-" + Guid.NewGuid().ToString("N"));
        byte[] legacy = [];
        try
        {
            await ProduceAsync(root, TestContext.Current.CancellationToken, captureLegacy: bytes => legacy = bytes);
            Assert.NotEmpty(legacy);
            var files = B10AbruptPublisher.FirstMembers.Where(name => name != "journal.jsonl")
                .ToDictionary(name => name, name => File.ReadAllBytes(Path.Combine(root, "first", name)), StringComparer.Ordinal);
            var termination = B10AbruptWire.ReadEnvelope(files["termination.json"], "termination");
            var first = termination.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions)!;
            var selection = B10AbruptWire.ReadEnvelope(files["kill-selection.json"], "selection");
            DateTimeOffset release = DateTimeOffset.ParseExact(selection.GetProperty("data").GetProperty("checkpoint").GetProperty("data").GetProperty("release_utc").GetString()!, "O", CultureInfo.InvariantCulture);
            byte[] validBinding = Binding(first, files, release, TimeProvider.System);
            string bindingPath = Path.Combine(root, "input.json");
            foreach (string fault in new[] { "missing_file", "missing_receipt", "changed_receipt", "job", "pod_uid", "source", "image", "predecessor", "same_pod", "same_uid", "stale_selection", "unselected_original", "changed_candidates", "terminal_exclusion", "legacy", "dialect", "purpose_removed", "schema_relabel" })
            {
                JsonObject binding = JsonNode.Parse(validBinding)!.AsObject();
                string detail = "receipt_changed";
                Action<Dictionary<string, string>>? configure = null;
                switch (fault)
                {
                    case "missing_file": detail = "receipt_missing"; break;
                    case "missing_receipt": binding["files"]!.AsObject().Remove("termination.json"); detail = "receipt_missing"; break;
                    case "changed_receipt":
                        var changedReceipt = JsonNode.Parse(binding["files"]!["termination.json"]!.GetValue<string>())!;
                        changedReceipt["data"]!["manifest"]!["sha256"] = new string('f', 64);
                        binding["files"]!["termination.json"] = changedReceipt.ToJsonString(); break;
                    case "job": binding["first_pod"]!["job_name"] = "another-job"; detail = "job_mismatch"; break;
                    case "pod_uid": binding["first_pod"]!["pod_uid"] = "another-uid"; detail = "predecessor_mismatch"; break;
                    case "source": configure = values => values["SourceCommit"] = new string('c', 40); detail = "source_mismatch"; break;
                    case "image": configure = values => values["ImageReference"] = "registry.invalid/loadgen@sha256:" + new string('c', 64); detail = "image_mismatch"; break;
                    case "predecessor": binding["first_pod"]!["pod_name"] = "another-predecessor"; detail = "predecessor_mismatch"; break;
                    case "same_pod": configure = values => values["PodName"] = first.PodName; detail = "pod_uid_reused"; break;
                    case "same_uid": configure = values => values["PodUid"] = first.PodUid; detail = "pod_uid_reused"; break;
                    case "stale_selection": binding["release_utc"] = B10AbruptPublisher.Utc(release.AddTicks(1)); detail = "selection_stale"; break;
                    case "unselected_original":
                    case "changed_candidates":
                    case "terminal_exclusion":
                        var changedSelection = JsonNode.Parse(binding["files"]!["kill-selection.json"]!.GetValue<string>())!;
                        var selectionData = changedSelection["data"]!;
                        if (fault == "unselected_original") selectionData["entries"]![0]!["attempt_id"] = "synthetic-first-timeout-0";
                        if (fault == "changed_candidates") selectionData["candidate_attempt_ids"]![0] = "synthetic-first-timeout-0";
                        if (fault == "terminal_exclusion") selectionData["terminal_exclusions"]!.AsArray().Add(new JsonObject
                        {
                            ["attempt_id"] = selectionData["entries"]![0]!["attempt_id"]!.GetValue<string>(),
                            ["terminal_sequence"] = 1
                        });
                        byte[] selectionBytes = Encoding.UTF8.GetBytes(changedSelection.ToJsonString());
                        binding["files"]!["kill-selection.json"] = Encoding.UTF8.GetString(selectionBytes);
                        var changedManifest = JsonNode.Parse(binding["files"]!["abrupt-manifest.json"]!.GetValue<string>())!;
                        changedManifest["data"]!["selection"] = JsonSerializer.SerializeToNode(
                            B10AbruptPublisher.Describe("kill-selection.json", selectionBytes), B10AbruptWire.JsonOptions);
                        byte[] manifestBytes = Encoding.UTF8.GetBytes(changedManifest.ToJsonString());
                        binding["files"]!["abrupt-manifest.json"] = Encoding.UTF8.GetString(manifestBytes);
                        var reboundTermination = JsonNode.Parse(binding["files"]!["termination.json"]!.GetValue<string>())!;
                        reboundTermination["data"]!["manifest"] = JsonSerializer.SerializeToNode(
                            B10AbruptPublisher.Describe("abrupt-manifest.json", manifestBytes), B10AbruptWire.JsonOptions);
                        binding["files"]!["termination.json"] = reboundTermination.ToJsonString();
                        detail = "selection_stale";
                        break;
                    case "legacy": binding["files"]!["termination.json"] = Encoding.UTF8.GetString(legacy); break;
                    default:
                        var changed = JsonNode.Parse(binding["files"]!["termination.json"]!.GetValue<string>())!.AsObject();
                        if (fault == "dialect") changed["dialect"] = "ordinary";
                        if (fault == "purpose_removed") changed.Remove("purpose");
                        if (fault == "schema_relabel") changed["schema_version"] = 1;
                        binding["files"]!["termination.json"] = changed.ToJsonString(); break;
                }
                await File.WriteAllTextAsync(bindingPath, binding.ToJsonString(), TestContext.Current.CancellationToken);
                if (fault == "missing_file") File.Delete(bindingPath);
                B10AbruptOptions options = TestOptions(root, "successor", bindingPath, configure);
                int constructions = 0;
                var error = await Assert.ThrowsAsync<InvalidDataException>(() => B10AbruptHost.RunValidatedSuccessorAsync(options,
                    _ => { constructions++; return Task.FromResult(0); }, TestContext.Current.CancellationToken));
                Assert.Equal("b10_abrupt_successor_binding_invalid:" + detail, error.Message);
                Assert.Equal(0, constructions);
            }
            await File.WriteAllBytesAsync(bindingPath, validBinding, TestContext.Current.CancellationToken);
            int validConstructions = 0;
            Assert.Equal(0, await B10AbruptHost.RunValidatedSuccessorAsync(TestOptions(root, "successor", bindingPath),
                _ => { validConstructions++; return Task.FromResult(0); }, TestContext.Current.CancellationToken));
            Assert.Equal(1, validConstructions);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("stop_errno", -1, 13, null)]
    [InlineData("stop_import", null, null, "EntryPointNotFoundException")]
    public async Task Given_NativeSignalFailure_When_SupervisorAborts_Then_PersistsActualObservation(string fault, int? returnValue, int? errno, string? nativeException)
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-signal-failure-" + Guid.NewGuid().ToString("N"));
        JsonElement? diagnostic = null;
        try
        {
            var failure = await Assert.ThrowsAsync<OwnedProducerProcess.SignalException>(() => ProduceAsync(root, TestContext.Current.CancellationToken,
                processFailure: fault, captureFailure: value => diagnostic = value));
            Assert.Equal("b10_abrupt_signal_failed", failure.Message);
            Assert.NotNull(diagnostic);
            JsonElement data = diagnostic.Value.GetProperty("data");
            Assert.Equal("incomplete", data.GetProperty("mechanical_status").GetString());
            JsonElement observed = data.GetProperty("signal_observation");
            Assert.Equal(19, observed.GetProperty("requested_signal").GetInt32());
            Assert.Equal(returnValue, observed.GetProperty("return_value").Deserialize<int?>());
            Assert.Equal(errno, observed.GetProperty("errno").Deserialize<int?>());
            Assert.Equal(nativeException, observed.GetProperty("native_exception").GetString());
            Assert.False(File.Exists(Path.Combine(root, "first", "termination.json")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("reap", "b10_abrupt_exit_unobserved")]
    [InlineData("eof", "b10_abrupt_eof_incomplete")]
    public async Task Given_ReapOrEofFailure_When_SupervisorRuns_Then_DiagnosticBlocksSuccessor(string fault, string failureName)
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-reap-failure-" + Guid.NewGuid().ToString("N"));
        JsonElement? diagnostic = null;
        try
        {
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => ProduceAsync(root, TestContext.Current.CancellationToken,
                processFailure: fault, captureFailure: value => diagnostic = value));
            Assert.Equal(failureName, failure.Message);
            Assert.NotNull(diagnostic);
            Assert.Equal(failureName, diagnostic.Value.GetProperty("data").GetProperty("failure").GetString());
            Assert.False(File.Exists(Path.Combine(root, "first", "termination.json")));
            Assert.False(Directory.Exists(Path.Combine(root, "successor")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    internal static async Task ProduceAsync(string destination, CancellationToken cancellationToken, bool terminalAtKill = false, Action<byte[]>? captureLegacy = null, string? processFailure = null, Action<JsonElement>? captureFailure = null)
    {
        Directory.CreateDirectory(destination);
        string work = Path.Combine(Path.GetTempPath(), "b10-work-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(work, "abrupt"));
        try
        {
            var clock = new ReleaseClock();
            B10AbruptOptions options = TestOptions(work);
            var firstIdentity = options.Identity();
            var firstStorage = new FileStorage(Path.Combine(destination, "first"));
            var publisher = new B10AbruptPublisher(firstStorage, clock);
            await using var journal = new B10AbruptJournal(Path.Combine(work, "abrupt", "journal.jsonl"), firstIdentity, clock);
            using var process = new SyntheticProcess { Failure = processFailure };
            var supervisor = new B10AbruptSupervisor(options, clock, journal, publisher, () => process,
                token => B10AbruptStatistics.ValidateCapturedAsync(Path.Combine(work, "capture", "statistics.jsonl"),
                    new KafkaStatisticsIdentity("B10", options.RunId, KafkaClientRole.LoadGenProducer, options.PodName),
                    ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>(), token));
            var statisticsIdentity = new KafkaStatisticsIdentity("B10", options.RunId, KafkaClientRole.LoadGenProducer, options.PodName);
            Directory.CreateDirectory(Path.Combine(work, "capture"));
            await using var captured = new RunEvidenceSession(RunIdentity(firstIdentity), new RunEvidenceOptions(Path.Combine(work, "capture"),
                2048, 1048576, 134217728, 100, TimeSpan.FromSeconds(20)), clock);
            var statistics = new B10AbruptStatistics(statisticsIdentity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>(), captured);
            IProducer<byte[], byte[]> producer = DispatchProxy.Create<IProducer<byte[], byte[]>, B10AbruptProducerTests.NativeProducer>();
            var native = (B10AbruptProducerTests.NativeProducer)(object)producer;
            Action<DeliveryReport<byte[], byte[]>>? controlOriginal = null;
            Action<DeliveryReport<byte[], byte[]>>? killOriginal = null;
            process.BeforeKill = () => { if (terminalAtKill) killOriginal?.Invoke(B10AbruptProducerTests.Report()); };
            native.OnProduce = (message, callback) =>
            {
                string key = Encoding.UTF8.GetString(message.Key);
                if (key.EndsWith("same_instance", StringComparison.Ordinal))
                {
                    if (controlOriginal is null) controlOriginal = callback;
                    else { controlOriginal(B10AbruptProducerTests.Report()); callback(B10AbruptProducerTests.Report()); }
                }
                else if (!key.EndsWith("cross_restart", StringComparison.Ordinal)) callback(B10AbruptProducerTests.Report());
                else killOriginal = callback;
            };
            object reportGate = new();
            void Report() { lock (reportGate) { clock.Tick(); statistics.WriteAsync(B10AbruptProducerTests.StatisticsReport(statisticsIdentity, clock), cancellationToken).AsTask().GetAwaiter().GetResult(); } }
            native.OnFlush = Report;
            using var reportsStop = new CancellationTokenSource();
            Task reports = Task.Run(async () =>
            {
                try { while (!reportsStop.IsCancellationRequested) { Report(); await Task.Delay(2, reportsStop.Token); } }
                catch (OperationCanceledException) when (reportsStop.IsCancellationRequested) { }
            }, cancellationToken);
            var execution = Execution(work);
            var runner = new B10AbruptProducer(() => producer, clock, statistics, cohort => Ledger(work, cohort), process.SendAsync,
                process.Terminal, async (arm, token) =>
                {
                    DateTimeOffset release = await B10AbruptHost.ReadReleaseAsync(options, arm, clock, token);
                    clock.AdvanceTo(release);
                    return release;
                }, (cohort, data, token) => SaveSealAsync(work, firstIdentity, cohort, data, token), diagnosticIdentity: firstIdentity);
            using var childStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task supervisorTask = supervisor.RunAsync(cancellationToken);
            Task producerTask = runner.RunFirstAsync(B10AbruptProducerTests.Profile(), RunIdentity(firstIdentity), execution, childStop.Token);
            try { await supervisorTask.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken); }
            finally
            {
                await childStop.CancelAsync();
                await reportsStop.CancelAsync();
                await reports;
                try { await producerTask; } catch (OperationCanceledException) when (childStop.IsCancellationRequested) { }
            }
            try { await producerTask; } catch (OperationCanceledException) when (childStop.IsCancellationRequested) { }
            await reportsStop.CancelAsync();
            await reports;
            Assert.Equal(0, native.Disposals);
            Assert.Equal(0, native.FlushAfterKill);
            Assert.Equal(1, process.Kills);
            Assert.True(process.Stopped && process.Resumed);
            foreach (var checkpoint in journal.Frames.Where(frame => frame.Kind == "checkpoint"))
            {
                var report = checkpoint.Data.GetProperty("pre_acceptance_report");
                byte[] raw = await File.ReadAllBytesAsync(Path.Combine(work, report.GetProperty("name").GetString()!), cancellationToken);
                Assert.Equal(raw.Length, report.GetProperty("length").GetInt64());
                Assert.Equal(B10AbruptPublisher.Hash(raw), report.GetProperty("sha256").GetString());
                Assert.Equal("LoadGenProducer", report.GetProperty("report_identity").GetProperty("role").GetString());
                Assert.True(report.GetProperty("report_sequence").GetInt64() > 0);
                Assert.True(B10AbruptWire.IsUtc(report.GetProperty("report_capture_utc").GetString()!));
            }
            captureLegacy?.Invoke(await File.ReadAllBytesAsync(Path.Combine(work, "timeout", "manifest.json"), cancellationToken));
            var firstFiles = firstStorage.Files;
            var selection = B10AbruptWire.ReadEnvelope(firstFiles["kill-selection.json"], "selection");
            Assert.Single(selection.GetProperty("data").GetProperty("entries").EnumerateArray());
            var termination = B10AbruptWire.ReadEnvelope(firstFiles["termination.json"], "termination");
            Assert.Equal("complete", termination.GetProperty("data").GetProperty("mechanical_status").GetString());
            DateTimeOffset successorRelease = DateTimeOffset.ParseExact(selection.GetProperty("data").GetProperty("checkpoint")
                .GetProperty("data").GetProperty("release_utc").GetString()!, "O", CultureInfo.InvariantCulture);
            string successorWork = Path.Combine(work, "second");
            Directory.CreateDirectory(successorWork);
            string bindingPath = Path.Combine(work, "predecessor.json");
            await File.WriteAllBytesAsync(bindingPath, Binding(firstIdentity, firstFiles, successorRelease, clock), cancellationToken);
            B10AbruptOptions successorOptions = TestOptions(successorWork, "successor", bindingPath);
            var loaded = await B10AbruptPublisher.LoadSelectionAsync(successorOptions, cancellationToken);
            var successorIdentity = successorOptions.Identity(firstIdentity.InstanceId);
            var successorStorage = new FileStorage(Path.Combine(destination, "successor"));
            var successorPublisher = new B10AbruptPublisher(successorStorage, clock);
            await successorPublisher.RegisterDiagnosticAsync(successorIdentity, cancellationToken);
            clock.AdvanceTo(successorRelease);
            var secondStatisticsIdentity = new KafkaStatisticsIdentity("B10", options.RunId, KafkaClientRole.LoadGenProducer, successorOptions.PodName);
            var secondStatistics = new B10AbruptStatistics(secondStatisticsIdentity, ["broker"], new Dictionary<string, string>(), new Dictionary<string, string>());
            IProducer<byte[], byte[]> secondNative = DispatchProxy.Create<IProducer<byte[], byte[]>, B10AbruptProducerTests.NativeProducer>();
            var second = (B10AbruptProducerTests.NativeProducer)(object)secondNative;
            second.OnProduce = (_, callback) => callback(B10AbruptProducerTests.Report());
            object secondReportGate = new();
            second.OnFlush = () => { lock (secondReportGate) secondStatistics.Observe(B10AbruptProducerTests.StatisticsReport(secondStatisticsIdentity, clock)); };
            using var secondStop = new CancellationTokenSource();
            Task secondReports = Task.Run(async () =>
            {
                try { while (!secondStop.IsCancellationRequested) { second.OnFlush(); await Task.Delay(2, secondStop.Token); } }
                catch (OperationCanceledException) when (secondStop.IsCancellationRequested) { }
            }, cancellationToken);
            long next = 0;
            var successor = new B10AbruptProducer(() => secondNative, clock, secondStatistics, cohort => Ledger(successorWork, cohort),
                (_, _, _) => Task.FromResult(++next), _ => true, (_, _) => Task.FromResult(successorRelease),
                (_, _, _) => Task.CompletedTask, diagnosticIdentity: successorIdentity);
            B10AbruptProducer.SuccessorArtifacts result = await successor.RunSuccessorAsync(B10AbruptProducerTests.Profile(), RunIdentity(successorIdentity),
                Execution(successorWork), loaded.Selection.GetProperty("data").GetProperty("entries"), successorRelease, cancellationToken);
            await secondStop.CancelAsync();
            await secondReports;
            var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var pair in result.Paths) payloads.Add(pair.Key, await File.ReadAllBytesAsync(pair.Value, cancellationToken));
            await successorPublisher.PublishSuccessorAsync(successorIdentity, payloads, loaded.Termination, loaded.Selection,
                result.ReadyUtc, result.ReleaseUtc, result.Invocations, true, cancellationToken);
            Assert.Single(second.Calls);
            Assert.Equal(1, second.Disposals);
            var receipt = B10AbruptWire.ReadEnvelope(successorStorage.Files["completion.json"], "successor_receipt").GetProperty("data");
            B10AbruptPublisher.CheckDescriptor(receipt.GetProperty("registration"), "registration.json", successorStorage.Files);
            foreach (var descriptor in receipt.GetProperty("artifacts").EnumerateArray())
                B10AbruptPublisher.CheckDescriptor(descriptor, descriptor.GetProperty("name").GetString()!, successorStorage.Files);
            var exported = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
                .Select(path => B10AbruptPublisher.Describe(Path.GetRelativePath(destination, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllBytes(path))).ToArray();
            Assert.Equal(14, exported.Length);
            await File.WriteAllBytesAsync(Path.Combine(destination, "export.json"), JsonSerializer.SerializeToUtf8Bytes(new { files = exported }, B10AbruptWire.JsonOptions), cancellationToken);
        }
        finally
        {
            string failurePath = Path.Combine(work, "abrupt", "failure.json");
            if (captureFailure is not null && File.Exists(failurePath)) captureFailure(B10AbruptWire.Parse(File.ReadAllBytes(failurePath)));
            Directory.Delete(work, true);
        }
    }

    internal static B10AbruptOptions TestOptions(string root, string role = "supervisor", string? binding = null, Action<Dictionary<string, string>>? configure = null)
    {
        var values = new Dictionary<string, string>
        {
            ["Enabled"] = "true", ["Purpose"] = "diagnostic_trial", ["Role"] = role,
            ["RunId"] = "synthetic", ["ScenarioId"] = "B10", ["ConfigurationId"] = "C1", ["Phase"] = "C1", ["Repeat"] = "1",
            ["SourceCommit"] = new('a', 40), ["ImageReference"] = "registry.invalid/loadgen@sha256:" + new string('b', 64),
            ["JobName"] = "synthetic-c1-r1-loadgen" + (role == "successor" ? "2" : ""),
            ["PodName"] = role == "successor" ? "synthetic-second" : "synthetic-first", ["PodUid"] = role == "successor" ? "uid-second" : "uid-first",
            ["NamespaceName"] = "synthetic", ["BlobEndpoint"] = "https://synthetic.blob.core.windows.net/",
            ["ScenarioSourceRoot"] = "/app", ["OutputRoot"] = root,
            [role == "successor" ? "PredecessorBindingPath" : "StartLineBindingPath"] = binding ?? "/bindings/start-line.json"
        };
        configure?.Invoke(values);
        var options = B10AbruptOptions.Read(values.Select(pair => new KeyValuePair<string, string>(B10AbruptOptions.Prefix + pair.Key, pair.Value)));
        options.Validate();
        return options;
    }
    private static RunIdentity RunIdentity(B10AbruptWire.Identity identity) => new("B10", "C1", identity.RunId, identity.InstanceId, identity.InstanceId, identity.InstanceId);
    private static RunExecutionOptions Execution(string root) => new(8, 256, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), 16384, 1048576, 1048576, root,
        "document-events", ["broker"], false, ApplicationRetry: ApplicationRetryPolicy.ForScenario("B10"));
    private static LocalLedgerStore Ledger(string root, string cohort)
    {
        string directory = Path.Combine(root, cohort); Directory.CreateDirectory(directory);
        return new(new(Path.Combine(directory, "ledger.jsonl"), Path.Combine(directory, "manifest.json"), 16, 16384, 1048576, 1048576, TimeSpan.FromSeconds(2)));
    }
    private static async Task SaveSealAsync(string root, B10AbruptWire.Identity identity, string cohort, JsonElement data, CancellationToken cancellationToken)
    {
        string path = Path.Combine(root, "abrupt-child", cohort + "-seal.json");
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await stream.WriteAsync(B10AbruptWire.SerializeEnvelope("cohort_seal", identity, data), cancellationToken);
        stream.Flush(true);
    }
    private static byte[] Binding(B10AbruptWire.Identity identity, IReadOnlyDictionary<string, byte[]> files, DateTimeOffset release, TimeProvider clock) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            files = files.Where(pair => pair.Key != "journal.jsonl").ToDictionary(pair => pair.Key, pair => Encoding.UTF8.GetString(pair.Value), StringComparer.Ordinal),
            first_pod = new { identity.JobName, identity.PodName, identity.PodUid, identity.ImageReference, identity.SourceCommit, finished_utc = B10AbruptPublisher.Utc(clock.GetUtcNow()) },
            release_utc = B10AbruptPublisher.Utc(release), runner_utc = B10AbruptPublisher.Utc(clock.GetUtcNow()), runner_monotonic_ticks = clock.GetTimestamp(),
            cross_host_timing = "not_verified", start_line_binding = new { start_line = "{}", broker_inventory = "{}", completion = "{}" }
        }, B10AbruptWire.JsonOptions);

    internal sealed class FileStorage(string root) : IB10AbruptStorage
    {
        internal Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public async Task CreateAsync(string name, byte[] bytes, CancellationToken cancellationToken)
        {
            string file = Path.GetFileName(name); Directory.CreateDirectory(root);
            await using var stream = new FileStream(Path.Combine(root, file), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            await stream.WriteAsync(bytes, cancellationToken); stream.Flush(true); Files.Add(file, bytes);
        }
        public Task<byte[]> ReadAsync(string name, int maximumBytes, CancellationToken cancellationToken) =>
            B10AbruptPublisher.ReadFileAsync(Path.Combine(root, Path.GetFileName(name)), maximumBytes, cancellationToken);
    }
    private sealed class ReleaseClock : TimeProvider
    {
        private readonly DateTimeOffset initial = TimeProvider.System.GetUtcNow();
        private readonly long timestamp = TimeProvider.System.GetTimestamp();
        private long offset;
        private bool frozen;
        internal void Tick() { if (!frozen) Interlocked.Increment(ref offset); }
        public override DateTimeOffset GetUtcNow() => initial.AddTicks(Interlocked.Read(ref offset));
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => TimeProvider.System.GetElapsedTime(timestamp).Ticks + Interlocked.Read(ref offset);
        internal void AdvanceTo(DateTimeOffset value) { frozen = true; Interlocked.Exchange(ref offset, (value - initial).Ticks); }
    }
    private sealed class SyntheticProcess : IB10AbruptProcess
    {
        private readonly ByteStream events = new();
        private readonly ByteStream acknowledgements = new();
        private long sent = 1;
        private long ack = 1;
        private readonly object gate = new();
        public OwnedProducerProcess.ProcessIdentity Identity { get; } = new(1234, 123, "synthetic-boot", 42);
        public Stream Events => events;
        public bool EventsReady() => events.Ready;
        public Stream Acknowledgements => acknowledgements;
        public bool HasExited { get; private set; }
        internal string? Failure { get; init; }
        internal int Kills { get; private set; }
        internal Action BeforeKill { get; set; } = () => { };
        internal bool Stopped { get; private set; }
        internal bool Resumed { get; private set; }
        internal async Task<long> SendAsync(string kind, JsonElement data, CancellationToken cancellationToken)
        {
            Write(kind, data);
            if (kind is "pre_acceptance_report" or "terminal") return 0;
            var message = await B10AbruptWire.ReadMessageAsync(acknowledgements, ack++, cancellationToken);
            return message!.Data.GetProperty("acknowledged_sequence").GetInt64();
        }
        private void Write(string kind, JsonElement data)
        {
            lock (gate) B10AbruptWire.WriteMessageAsync(events, new(kind, ++sent, data)).GetAwaiter().GetResult();
        }
        internal bool Terminal(JsonElement data) { Write("terminal", data); return true; }
        public OwnedProducerProcess.SignalObservation Stop()
        {
            if (Failure == "stop_errno") throw new OwnedProducerProcess.SignalException("b10_abrupt_signal_failed", new(19, -1, 13));
            if (Failure == "stop_import") throw new OwnedProducerProcess.SignalException("b10_abrupt_signal_failed", new(19, null, null, nameof(EntryPointNotFoundException)));
            Stopped = true; return new(19, 0, 0);
        }
        public bool ConfirmStopped(TimeSpan timeout, TimeProvider clock) => true;
        public OwnedProducerProcess.SignalObservation Resume() { Resumed = true; return new(18, 0, 0); }
        public OwnedProducerProcess.SignalObservation Kill() { Kills++; BeforeKill(); HasExited = true; if (Failure != "eof") events.Complete(); return new(9, 0, 0); }
        public OwnedProducerProcess.ExitObservation ObserveExit() => new(HasExited && Failure != "reap", Failure == "reap" ? null : 137, Environment.Version.ToString());
        public B10AbruptCgroup.Observation CaptureBefore() => new("synthetic-boot", "/synthetic", "inode-synthetic", 0);
        public B10AbruptCgroup.Observation CaptureAfter(B10AbruptCgroup.Observation before) => before;
        public void Dispose() { events.Dispose(); acknowledgements.Dispose(); }
    }
    private sealed class ByteStream : Stream
    {
        private readonly Channel<byte[]> bytes = Channel.CreateUnbounded<byte[]>();
        private byte[]? current;
        private int offset;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        internal bool Ready => (current is not null && offset < current.Length) || bytes.Reader.TryPeek(out _) || bytes.Reader.Completion.IsCompleted;
        internal void Complete() => bytes.Writer.TryComplete();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (current is null || offset == current.Length)
            {
                if (!await bytes.Reader.WaitToReadAsync(cancellationToken)) return 0;
                current = await bytes.Reader.ReadAsync(cancellationToken); offset = 0;
            }
            int count = Math.Min(buffer.Length, current.Length - offset); current.AsMemory(offset, count).CopyTo(buffer); offset += count; return count;
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => bytes.Writer.WriteAsync(buffer.ToArray(), cancellationToken);
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

public sealed class B10AbruptPublisherTests
{
    [Theory]
    [InlineData(400, "InvalidAuthenticationInfo")]
    [InlineData(400, "InvalidHeaderValue")]
    [InlineData(400, "InvalidInput")]
    [InlineData(400, "InvalidQueryParameterValue")]
    [InlineData(401, "NoAuthenticationInformation")]
    [InlineData(401, "InvalidAuthenticationInfo")]
    [InlineData(403, "AuthenticationFailed")]
    [InlineData(403, "AuthorizationPermissionMismatch")]
    [InlineData(403, "AuthorizationServiceMismatch")]
    [InlineData(403, "AuthorizationFailure")]
    public async Task Given_CreateRefusal_When_Publishing_Then_AmbiguousPreservesCauseWithoutReadbackOrRetry(int status, string code)
    {
        var cause = new RequestFailedException(status, "synthetic refusal", code, null);
        var storage = new Storage { CreateFailure = cause };
        var publisher = new B10AbruptPublisher(storage, TimeProvider.System);
        var failure = await Assert.ThrowsAsync<IOException>(() => publisher.RegisterDiagnosticAsync(
            B10AbruptProcessTests.Options().Identity(), TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_publication_ambiguous", failure.Message);
        Assert.Same(cause, failure.InnerException);
        Assert.Equal(1, storage.Creates);
        Assert.Equal(0, storage.Reads);
        Assert.Null(storage.Bytes);
    }

    [Theory]
    [InlineData(400, "UnknownError")]
    [InlineData(400, "AuthorizationPermissionMismatch")]
    [InlineData(403, "UnknownError")]
    [InlineData(408, "OperationTimedOut")]
    [InlineData(409, "BlobAlreadyExists")]
    [InlineData(412, "ConditionNotMet")]
    [InlineData(429, "ServerBusy")]
    [InlineData(500, "InternalError")]
    public async Task Given_OtherServiceCreateFailure_When_Publishing_Then_AmbiguousPreservesCauseWithoutReadbackOrRetry(int status, string code)
    {
        var cause = new RequestFailedException(status, "synthetic uncertain failure", code, null);
        var storage = new Storage { CreateFailure = cause };
        var publisher = new B10AbruptPublisher(storage, TimeProvider.System);
        var failure = await Assert.ThrowsAsync<IOException>(() => publisher.RegisterDiagnosticAsync(
            B10AbruptProcessTests.Options().Identity(), TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_publication_ambiguous", failure.Message);
        Assert.Same(cause, failure.InnerException);
        Assert.Equal(1, storage.Creates);
        Assert.Equal(0, storage.Reads);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("transport_before_write")]
    [InlineData("transport_after_write")]
    [InlineData("canceled")]
    [InlineData("invalid_data")]
    public async Task Given_UncertainCreateOutcome_When_Publishing_Then_AmbiguousPreservesCauseAndDoesNotRetry(string fault)
    {
        var storage = new Storage();
        Exception cause = fault switch
        {
            "duplicate" => new RequestFailedException(409, "synthetic_duplicate", "BlobAlreadyExists", null),
            "canceled" => new OperationCanceledException("synthetic_cancellation"),
            "invalid_data" => new InvalidDataException("synthetic_failure"),
            _ => new IOException("synthetic_transport_failure")
        };
        storage.CreateFailure = cause;
        storage.WriteBeforeFailure = fault == "transport_after_write";
        var publisher = new B10AbruptPublisher(storage, TimeProvider.System);
        var failure = await Assert.ThrowsAsync<IOException>(() => publisher.RegisterDiagnosticAsync(
            B10AbruptProcessTests.Options().Identity(), TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_publication_ambiguous", failure.Message);
        Assert.Same(cause, failure.InnerException);
        Assert.Equal(1, storage.Creates);
        Assert.Equal(0, storage.Reads);
        Assert.Equal(storage.WriteBeforeFailure, storage.Bytes is not null);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("transport")]
    [InlineData("canceled")]
    [InlineData("invalid_data")]
    public async Task Given_UnverifiableReadBack_When_Publishing_Then_AmbiguousPreservesCause(string fault)
    {
        Exception cause = fault switch
        {
            "missing" => new RequestFailedException(404, "synthetic_missing", "BlobNotFound", null),
            "invalid_data" => new InvalidDataException("synthetic_storage_failure"),
            "canceled" => new OperationCanceledException("synthetic_cancellation"),
            _ => new IOException("synthetic_transport_failure")
        };
        var storage = new Storage { ReadFailure = cause };
        var publisher = new B10AbruptPublisher(storage, TimeProvider.System);
        var failure = await Assert.ThrowsAsync<IOException>(() => publisher.RegisterDiagnosticAsync(
            B10AbruptProcessTests.Options().Identity(), TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_publication_ambiguous", failure.Message);
        Assert.Same(cause, failure.InnerException);
        Assert.Equal(1, storage.Creates);
        Assert.Equal(1, storage.Reads);
        Assert.NotNull(storage.Bytes);
    }

    [Fact]
    public async Task Given_OversizedReadBack_When_Publishing_Then_PreservesPublicationFailure()
    {
        var cause = new InvalidDataException("b10_abrupt_publication_failed");
        var storage = new Storage { ReadFailure = cause };
        var publisher = new B10AbruptPublisher(storage, TimeProvider.System);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => publisher.RegisterDiagnosticAsync(
            B10AbruptProcessTests.Options().Identity(), TestContext.Current.CancellationToken));
        Assert.Same(cause, failure);
        Assert.Equal("b10_abrupt_publication_failed", failure.Message);
        Assert.Equal(1, storage.Creates);
        Assert.Equal(1, storage.Reads);
    }

    [Fact]
    public async Task Given_ChangedReadBack_When_Publishing_Then_FailsWithoutRetry()
    {
        var storage = new Storage { ChangeReadBack = true };
        var publisher = new B10AbruptPublisher(storage, TimeProvider.System);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => publisher.RegisterDiagnosticAsync(
            B10AbruptProcessTests.Options().Identity(), TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_publication_failed", failure.Message);
        Assert.Equal(1, storage.Creates);
        Assert.Equal(1, storage.Reads);
    }

    [Fact]
    public async Task Given_AlreadyPublishedObject_When_PublishingAgain_Then_AmbiguousWithoutAnotherWrite()
    {
        var storage = new Storage();
        var publisher = new B10AbruptPublisher(storage, TimeProvider.System);
        var identity = B10AbruptProcessTests.Options().Identity();
        byte[] original = await publisher.RegisterDiagnosticAsync(identity, TestContext.Current.CancellationToken);
        var failure = await Assert.ThrowsAsync<IOException>(() => publisher.RegisterDiagnosticAsync(identity, TestContext.Current.CancellationToken));
        Assert.Equal("b10_abrupt_publication_ambiguous", failure.Message);
        Assert.Equal(original, storage.Bytes);
        Assert.Equal(1, storage.Creates);
        Assert.Equal(1, storage.Reads);
    }

    private sealed class Storage : IB10AbruptStorage
    {
        internal Exception? CreateFailure { get; set; }
        internal Exception? ReadFailure { get; set; }
        internal bool WriteBeforeFailure { get; set; }
        internal bool ChangeReadBack { get; set; }
        internal byte[]? Bytes { get; private set; }
        internal int Creates { get; private set; }
        internal int Reads { get; private set; }
        public Task CreateAsync(string name, byte[] bytes, CancellationToken cancellationToken)
        {
            Creates++;
            if (CreateFailure is null || WriteBeforeFailure) Bytes = bytes;
            return CreateFailure is null ? Task.CompletedTask : Task.FromException(CreateFailure);
        }
        public Task<byte[]> ReadAsync(string name, int maximumBytes, CancellationToken cancellationToken)
        {
            Reads++;
            if (ReadFailure is not null) return Task.FromException<byte[]>(ReadFailure);
            return Task.FromResult(ChangeReadBack ? [] : Bytes!);
        }
    }
}
