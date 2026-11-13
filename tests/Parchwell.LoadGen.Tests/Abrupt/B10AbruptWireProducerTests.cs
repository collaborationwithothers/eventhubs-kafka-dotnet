using Azure;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
public sealed class B10AbruptWireProducerTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Given_SyntheticProducer_When_SupervisorAndSuccessorRun_Then_ActualBytesBindBothPublications()
    {
        string root = NewRoot();
        try { await ProduceAsync(root, TestContext.Current.CancellationToken); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Given_ClockAtReportCapture_When_FirstCrossRestartBinds_Then_ExportFixtureKeepsStrictTimestampOrder()
    {
        var identity = TestOptions(NewRoot()).Identity();
        await using var fixture = ExportFixture(identity, "diagnostic_trial");
        fixture.ConfigureControl();
        fixture.Initialize();
        fixture.StartReports();
        await fixture.Runner.RunControlAsync(TestContext.Current.CancellationToken);
        await fixture.Runner.PrepareArmAsync("cross_restart", TestContext.Current.CancellationToken);
        await fixture.StopReportsAsync();
        var terminal = fixture.Journal.Frames.Single(frame => frame.Kind == "warmup_terminal" &&
            frame.Data.GetProperty("attempt_id").GetString() == identity.InstanceId + "-cross_restart-warmup");
        var selected = fixture.Statistics.SelectLeaderReport(DateTimeOffset.ParseExact(terminal.ObservedUtc, "O", CultureInfo.InvariantCulture),
            fixture.Options.Topic, 0, out _);
        Assert.NotNull(selected);
        fixture.Advancing.AdvanceTo(selected.Report.ReceivedAt);
        Assert.Equal(selected.Report.ReceivedAt, fixture.Clock.GetUtcNow());

        await fixture.Runner.InvokeProbe("cross_restart", new byte[256], null, 0, TestContext.Current.CancellationToken);

        var snapshot = B10AbruptJournal.Read(await File.ReadAllBytesAsync(fixture.Journal.Path, TestContext.Current.CancellationToken), identity);
        Assert.Empty(snapshot.Gaps);
        Assert.Empty(snapshot.Failures);
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
        string root = NewRoot();
        try
        {
            await ProduceAsync(root, TestContext.Current.CancellationToken, terminalAtKill: true);
            JsonElement receipt = B10AbruptWire.ReadEnvelope(File.ReadAllBytes(Path.Combine(root, "first", "termination.json")), "termination").GetProperty("data");
            Assert.Equal("kill_too_late", receipt.GetProperty("outcome").GetString());
            Assert.Equal("no_unconfirmed_attempt", receipt.GetProperty("detail").GetString());
            Assert.False(receipt.GetProperty("successor_permitted").GetBoolean());
            Assert.False(Directory.Exists(Path.Combine(root, "successor")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("missing_file", "receipt_missing")]
    [InlineData("changed_receipt", "receipt_changed")]
    [InlineData("not_permitted", "receipt_changed")]
    [InlineData("job", "job_mismatch")]
    [InlineData("pod_uid", "predecessor_mismatch")]
    [InlineData("source", "source_mismatch")]
    [InlineData("image", "image_mismatch")]
    [InlineData("predecessor", "receipt_missing")]
    [InlineData("same_pod", "pod_uid_reused")]
    [InlineData("same_uid", "pod_uid_reused")]
    [InlineData("stale_selection", "selection_stale")]
    [InlineData("changed_set", "selection_stale")]
    [InlineData("changed_journal", "receipt_changed")]
    [InlineData("dialect", "receipt_changed")]
    [InlineData("purpose_removed", "receipt_changed")]
    [InlineData("schema_relabel", "receipt_changed")]
    public async Task Given_ProductionBindingFaults_When_ActualHostValidatesSuccessor_Then_ProducerContinuationNeverStarts(string fault, string detail)
    {
        string root = NewRoot();
        try
        {
            await ProduceAsync(root, TestContext.Current.CancellationToken);
            var files = Directory.GetFiles(Path.Combine(root, "first")).ToDictionary(path => Path.GetFileName(path), File.ReadAllBytes, StringComparer.Ordinal);
            JsonElement termination = B10AbruptWire.ReadEnvelope(files["termination.json"], "termination");
            var identity = termination.GetProperty("identity").Deserialize<B10AbruptWire.Identity>(B10AbruptWire.JsonOptions)!;
            var snapshot = B10AbruptJournal.Read(files["journal.jsonl"]);
            var intent = snapshot.Frames.Single(frame => frame.Kind == "kill_intent");
            DateTimeOffset release = DateTimeOffset.Parse(intent.Data.GetProperty("release_utc").GetString()!, CultureInfo.InvariantCulture);
            JsonObject binding = JsonNode.Parse(Binding(identity, files, release, TimeProvider.System))!.AsObject();
            Action<Dictionary<string, string>>? configure = null;
            switch (fault)
            {
                case "missing_file": files.Remove("termination.json"); break;
                case "changed_receipt": binding["termination"]!["sha256"] = new string('f', 64); break;
                case "job": binding["first_pod"]!["job_name"] = "synthetic-c1-r1-loadgen"; break;
                case "pod_uid": binding["first_pod"]!["pod_uid"] = "another-uid"; break;
                case "source": configure = values => values["SourceCommit"] = new string('c', 40); break;
                case "image": configure = values => values["ImageReference"] = "registry.invalid/loadgen@sha256:" + new string('c', 64); break;
                case "predecessor": files.Clear(); binding["first_pod"]!["pod_name"] = "another-predecessor"; break;
                case "same_pod": configure = values => values["PodName"] = identity.PodName; break;
                case "same_uid": configure = values => values["PodUid"] = identity.PodUid; break;
                case "stale_selection": binding["release_utc"] = B10AbruptPublisher.Utc(release.AddTicks(1)); break;
                case "changed_journal": files["journal.jsonl"][0] ^= 1; break;
                default:
                    JsonObject changed = JsonNode.Parse(files["termination.json"])!.AsObject();
                    switch (fault)
                    {
                        case "not_permitted": changed["data"]!["successor_permitted"] = false; break;
                        case "changed_set": changed["data"]!["kill_unconfirmed_attempt_ids"]!.AsArray().RemoveAt(0); break;
                        case "dialect": changed["dialect"] = "ordinary"; break;
                        case "purpose_removed": changed.Remove("purpose"); break;
                        case "schema_relabel": changed["schema_version"] = 2; break;
                    }
                    files["termination.json"] = Encoding.UTF8.GetBytes(changed.ToJsonString());
                    binding["termination"] = JsonSerializer.SerializeToNode(B10AbruptPublisher.Describe("termination.json", files["termination.json"]), B10AbruptWire.JsonOptions);
                    break;
            }
            string path = Path.Combine(root, "binding.json");
            await File.WriteAllTextAsync(path, binding.ToJsonString(), TestContext.Current.CancellationToken);
            var options = TestOptions(Path.Combine(root, "work"), "successor", path, configure);
            var publisher = new B10AbruptPublisher(new MemoryStorage(files!), TimeProvider.System);
            int continuations = 0;
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => B10AbruptHost.RunValidatedSuccessorAsync(options, publisher,
                _ => { continuations++; return Task.FromResult(0); }, TestContext.Current.CancellationToken));
            Assert.Equal("b10_abrupt_successor_binding_invalid:" + detail, failure.Message);
            Assert.Equal(0, continuations);
            Assert.DoesNotContain("registration.json", Directory.Exists(options.OutputRoot) ? Directory.GetFiles(options.OutputRoot).Select(Path.GetFileName) : []);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    internal sealed record PublicationContext(HarnessClock Clock, SyntheticProcess Process, TextWriter Stderr, IB10AbruptStorage Inner);

    // The export is produced in-process, as `ProduceAsync` does on `main` (lines 238 to 252): a synthetic producer
    // and process seam and the advancing test clock `HarnessClock` (line 464), with no real child process, no real
    // signal and no real 120 s wait. Its bytes describe one intended self-kill with a nonempty kill set and a
    // completed successor. With `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT` set, the whole class completes inside the 60 s
    // bound at `B10AbruptRejectionTests.cs` line 105 and the 120 s bound at `test_b10_abrupt_host.py` line 101,
    // and neither bound changes.
    internal static async Task ProduceAsync(string destination, CancellationToken cancellationToken, bool terminalAtKill = false,
        Action<byte[]>? captureLegacy = null, string? processFailure = null, Action<JsonElement>? captureFailure = null,
        bool nativeErrors = false, Action<string>? atKill = null, Func<PublicationContext, IB10AbruptStorage>? publicationStorage = null,
        TextWriter? supervisorErrors = null, string purpose = "diagnostic_trial", int repeat = 1)
    {
        void Configure(Dictionary<string, string> values)
        {
            values["Purpose"] = purpose;
            values["Repeat"] = repeat.ToString(CultureInfo.InvariantCulture);
            if (purpose == "scientific") values["RunId"] = "b10-1-1";
            values["JobName"] = $"{values["RunId"]}-c1-r{values["Repeat"]}-loadgen-{(values["Role"] == "successor" ? "successor" : "restart")}";
        }
        Directory.CreateDirectory(destination);
        await using var fixture = ExportFixture(TestOptions(destination, configure: Configure).Identity(), purpose);
        var clock = fixture.Advancing;
        var options = TestOptions(fixture.Root, configure: Configure);
        var identity = options.Identity();
        var process = new SyntheticProcess();
        var first = new FileStorage(Path.Combine(destination, "first"));
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        var publisher = new B10AbruptPublisher(publicationStorage?.Invoke(new(clock, process, supervisorErrors ?? errors, first)) ?? first, clock, purpose);
        await publisher.RegisterDiagnosticAsync(identity, cancellationToken);
        fixture.ConfigureControl();
        if (nativeErrors)
        {
            var invoke = fixture.Native.OnProduce;
            bool captured = false;
            fixture.Native.OnProduce = (message, callback) =>
            {
                if (!captured && Encoding.UTF8.GetString(message.Key).EndsWith("cross_restart", StringComparison.Ordinal))
                {
                    captured = true;
                    Assert.True(fixture.Journal.TryCaptureNativeError(-169, "Local_Authentication", true, false, clock.GetTimestamp()));
                    Assert.True(fixture.Journal.TryCaptureNativeError(45, "OutOfOrderSequenceNumber", true, true, clock.GetTimestamp()));
                    fixture.Journal.DrainAvailable().GetAwaiter().GetResult();
                }
                invoke(message, callback);
            };
        }
        fixture.AtKill = () =>
        {
            atKill?.Invoke(fixture.Root);
            process.ObserveSelfKill();
            if (terminalAtKill) foreach (var callback in fixture.CrossCallbacks) callback(B10AbruptProducerTests.Report());
            if (nativeErrors)
            {
                Assert.True(fixture.Journal.TryCaptureNativeError(-169, "Local_Authentication", true, false, clock.GetTimestamp()));
            }
            fixture.Journal.DrainAvailable().GetAwaiter().GetResult();
        };
        fixture.StartReports();
        await Assert.ThrowsAsync<B10AbruptProducerTests.SimulatedDeathException>(() => fixture.Runner.RunFirstAsync(B10AbruptProducerTests.Profile(), fixture.Identity, fixture.Options, cancellationToken));
        await fixture.StopReportsAsync();
        byte[] bytes = await File.ReadAllBytesAsync(fixture.Journal.Path, cancellationToken);
        var snapshot = B10AbruptJournal.Read(bytes, identity, purpose);
        Assert.Empty(snapshot.Gaps);
        if (!nativeErrors) Assert.Empty(snapshot.Failures);
        var started = snapshot.Frames[0].Data;
        process.Identity = new(started.GetProperty("pid").GetInt32(), started.GetProperty("boot_id").GetString()!, started.GetProperty("start_time_ticks").GetInt64());
        var cgroup = new B10AbruptCgroup.Observation(process.Identity.BootId, "/synthetic", "1:2", 0);
        var classified = B10AbruptParent.Classify(snapshot, process.HasExited, process.ExitCode, process.Identity, cgroup, cgroup);
        Assert.Equal("intended_self_kill", classified.Termination);
        Assert.Equal(137, process.ExitCode);
        Assert.Equal(1, process.Kills);
        Assert.Equal(0, fixture.Native.Disposals);
        Assert.Equal(0, fixture.Native.FlushAfterKill);
        var reports = snapshot.Frames.Where(frame => frame.Kind is "pre_acceptance_report" or "post_drain_report").ToArray();
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["journal.jsonl"] = bytes };
        foreach (var report in reports)
        {
            string name = report.Data.GetProperty("name").GetString()!;
            byte[] reportBytes = await File.ReadAllBytesAsync(Path.Combine(fixture.Root, "abrupt", name), cancellationToken);
            Assert.Equal(reportBytes.LongLength, report.Data.GetProperty("length").GetInt64());
            Assert.Equal(B10AbruptPublisher.Hash(reportBytes), report.Data.GetProperty("sha256").GetString());
            files.Add(name, reportBytes);
        }
        files["abrupt-manifest.json"] = B10AbruptWire.SerializeEnvelope("abrupt_manifest", identity, new
        {
            journal = B10AbruptPublisher.Describe("journal.jsonl", bytes), journal_records = snapshot.Frames.Count,
            report_files = reports.Select(frame => B10AbruptPublisher.Describe(frame.Data.GetProperty("name").GetString()!, files[frame.Data.GetProperty("name").GetString()!])).ToArray(),
            gaps = snapshot.Gaps.Concat(snapshot.Failures.Values).ToArray()
        }, purpose);
        files["termination.json"] = B10AbruptWire.SerializeEnvelope("termination", identity, new
        {
            termination = classified.Termination, outcome = classified.Outcome, detail = classified.Detail, signal_observation = classified.SignalObservation,
            successor_permitted = classified.SuccessorPermitted,
            child = new { pid = process.Identity.Pid, start_time_ticks = process.Identity.StartIdentity, boot_id = process.Identity.BootId, parent_observed_start_time_ticks = process.Identity.StartIdentity },
            exit_observed = true, exit_code_local_observation = process.ExitCode, runtime_version = Environment.Version.ToString(), wait_bound_milliseconds = 720000,
            cgroup_before = cgroup, cgroup_after = cgroup, control_unconfirmed_attempt_ids = snapshot.ControlUnconfirmedAttemptIds, kill_unconfirmed_attempt_ids = snapshot.KillUnconfirmedAttemptIds,
            capture_gaps = snapshot.Gaps, native_error_count = snapshot.Frames.Count(frame => frame.Kind == "native_error"),
            native_fatal_count = snapshot.Frames.Count(frame => frame.Kind == "native_error" && frame.Data.GetProperty("is_fatal").GetBoolean()), native_tail = "unknown_due_to_intended_kill",
            manifest = B10AbruptPublisher.Describe("abrupt-manifest.json", files["abrupt-manifest.json"])
        }, purpose);
        await publisher.PublishFirstAsync(identity, files, cancellationToken);
        if (!classified.SuccessorPermitted) return;
        Assert.NotEmpty(snapshot.KillUnconfirmedAttemptIds);
        JsonElement termination = B10AbruptWire.ReadEnvelope(first.Files["termination.json"], "termination", purpose, repeat);
        DateTimeOffset release = DateTimeOffset.Parse(snapshot.Frames.Single(frame => frame.Kind == "kill_intent").Data.GetProperty("release_utc").GetString()!, CultureInfo.InvariantCulture);
        string bindingPath = Path.Combine(fixture.Root, "predecessor.json");
        await File.WriteAllBytesAsync(bindingPath, Binding(identity, first.Files, release, clock), cancellationToken);
        var successorOptions = TestOptions(Path.Combine(fixture.Root, "second"), "successor", bindingPath, Configure);
        var loaded = await publisher.LoadSelectionAsync(successorOptions, cancellationToken);
        var successorIdentity = successorOptions.Identity(identity.InstanceId);
        var second = new FileStorage(Path.Combine(destination, "successor"));
        var successorPublisher = new B10AbruptPublisher(second, clock, purpose);
        await successorPublisher.RegisterDiagnosticAsync(successorIdentity, cancellationToken);
        clock.AdvanceTo(release); clock.ExitManual();
        await using var chain = new B10AbruptSuccessorCompletionTests.Chain(clock, TimeSpan.FromSeconds(20), identity: RunIdentity(successorIdentity), root: successorOptions.OutputRoot, wireIdentity: successorIdentity, purpose: purpose);
        chain.DeliverAtPoll = true; chain.DeliverAtBoundary = true;
        var result = await chain.RunAsync(loaded.Entries, release, Execution(successorOptions.OutputRoot), cancellationToken);
        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var pair in result.Paths) payloads.Add(pair.Key, await File.ReadAllBytesAsync(pair.Value, cancellationToken));
        await successorPublisher.PublishSuccessorAsync(successorIdentity, payloads, loaded.FirstTermination, loaded.Journal, loaded.KillUnconfirmedAttemptIds,
            result.ReadyUtc, result.ReleaseUtc, result.Invocations, true, cancellationToken);
        Assert.Equal(snapshot.KillUnconfirmedAttemptIds.Count, chain.Native.Calls.Count);
        Assert.Equal(1, chain.Native.Disposals);
        foreach (var invocation in result.Invocations.EnumerateArray()) Assert.True(DateTimeOffset.Parse(invocation.GetProperty("invocation_utc").GetString()!, CultureInfo.InvariantCulture) >= release);
        var exported = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(path => B10AbruptPublisher.Describe(Path.GetRelativePath(destination, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllBytes(path))).ToArray();
        Assert.Equal(14, exported.Length);
        await File.WriteAllBytesAsync(Path.Combine(destination, "export.json"), JsonSerializer.SerializeToUtf8Bytes(new { files = exported }, B10AbruptWire.JsonOptions), cancellationToken);
    }

    private static B10AbruptProducerTests.Fixture ExportFixture(B10AbruptWire.Identity identity, string purpose)
    {
        var fixture = new B10AbruptProducerTests.Fixture(diagnosticIdentity: identity, purpose: purpose);
        // A selected report must precede its binding even when the report task does not advance the clock between frames.
        fixture.FlushHook = () =>
        {
            fixture.Advancing.Advance(TimeSpan.FromTicks(1));
            return Task.CompletedTask;
        };
        return fixture;
    }

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "b10-produced-" + Guid.NewGuid().ToString("N"));
    private static RunIdentity RunIdentity(B10AbruptWire.Identity identity) => new("B10", "C1", identity.RunId, identity.InstanceId, identity.InstanceId, identity.InstanceId);
    private static RunExecutionOptions Execution(string root) => new(8, 256, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), 16384, 1048576, 1048576, root,
        "document-events", ["broker"], false, ApplicationRetry: ApplicationRetryPolicy.ForScenario("B10"));
    internal static byte[] Binding(B10AbruptWire.Identity identity, IReadOnlyDictionary<string, byte[]> files, DateTimeOffset release, TimeProvider clock) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        first_pod = new { identity.JobName, identity.PodName, identity.PodUid, identity.ImageReference, identity.SourceCommit, finished_utc = B10AbruptPublisher.Utc(clock.GetUtcNow()) },
        termination = B10AbruptPublisher.Describe("termination.json", files["termination.json"]), release_utc = B10AbruptPublisher.Utc(release), runner_utc = B10AbruptPublisher.Utc(clock.GetUtcNow()), runner_monotonic_ticks = clock.GetTimestamp(),
        cross_host_timing = "not_verified", start_line_binding = new { start_line = "{}", broker_inventory = "{}", completion = "{}" }
    }, B10AbruptWire.JsonOptions);
    internal sealed class SyntheticProcess
    {
        internal B10AbruptProcess.ProcessIdentity Identity { get; set; } = new(1234, "synthetic-boot", 123);
        internal bool HasExited { get; private set; }
        internal int ExitCode { get; private set; }
        internal int Kills { get; private set; }
        internal void ObserveSelfKill() { Kills++; HasExited = true; ExitCode = 137; }
    }
    private sealed class MemoryStorage(Dictionary<string, byte[]> files) : IB10AbruptStorage
    {
        public Task CreateAsync(string name, byte[] bytes, CancellationToken cancellationToken) => throw new InvalidOperationException("registration_must_not_be_written");
        public Task<byte[]> ReadAsync(string name, int maximumBytes, CancellationToken cancellationToken) => files.TryGetValue(Path.GetFileName(name), out byte[]? bytes) ? Task.FromResult(bytes) : Task.FromException<byte[]>(new FileNotFoundException());
    }
    internal static B10AbruptOptions TestOptions(string root, string role = "parent", string? binding = null, Action<Dictionary<string, string>>? configure = null)
    {
        var values = new Dictionary<string, string>
        {
            ["Enabled"] = "true", ["Purpose"] = "diagnostic_trial", ["Role"] = role,
            ["RunId"] = "synthetic", ["ScenarioId"] = "B10", ["ConfigurationId"] = "C1", ["Phase"] = "C1", ["Repeat"] = "1",
            ["SourceCommit"] = new('a', 40), ["ImageReference"] = "registry.invalid/loadgen@sha256:" + new string('b', 64),
            ["JobName"] = "synthetic-c1-r1-loadgen-" + (role == "successor" ? "successor" : "restart"),
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
    /// <summary>
    /// The export flow's clock. After <see cref="EnterManual"/> its timestamp and UTC move only when a test calls
    /// <see cref="Advance"/>, and its timers fire on those calls, so publication timing is exact.
    /// </summary>
    internal sealed class HarnessClock : TimeProvider
    {
        private readonly DateTimeOffset initial = TimeProvider.System.GetUtcNow();
        private readonly long timestamp = TimeProvider.System.GetTimestamp();
        private readonly List<ManualTimer> timers = [];
        private long offset;
        private long manualBase = -1;
        private long manualOffset;
        private bool frozen;
        internal void Tick() { if (!frozen) Interlocked.Increment(ref offset); }
        public override DateTimeOffset GetUtcNow() => initial.AddTicks(Interlocked.Read(ref offset));
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref manualBase) >= 0
            ? Interlocked.Read(ref manualBase) + Interlocked.Read(ref offset) - Interlocked.Read(ref manualOffset)
            : TimeProvider.System.GetElapsedTime(timestamp).Ticks + Interlocked.Read(ref offset);
        internal void AdvanceTo(DateTimeOffset value) { frozen = true; Interlocked.Exchange(ref offset, (value - initial).Ticks); }
        internal void EnterManual()
        {
            frozen = true;
            Interlocked.Exchange(ref manualOffset, Interlocked.Read(ref offset));
            Interlocked.Exchange(ref manualBase, GetTimestamp());
        }
        internal void ExitManual() => Interlocked.Exchange(ref manualBase, -1);
        internal void Advance(TimeSpan elapsed)
        {
            Interlocked.Add(ref offset, elapsed.Ticks);
            ManualTimer[] snapshot; lock (timers) snapshot = timers.ToArray();
            foreach (ManualTimer timer in snapshot) timer.Fire(GetTimestamp());
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (Interlocked.Read(ref manualBase) < 0 || dueTime >= TimeSpan.Zero && dueTime <= TimeSpan.FromMilliseconds(1)) return base.CreateTimer(callback, state, dueTime, period);
            var timer = new ManualTimer(this, callback, state);
            lock (timers) timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }
        private sealed class ManualTimer(HarnessClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long due = long.MaxValue;
            public bool Change(TimeSpan dueTime, TimeSpan period) { Interlocked.Exchange(ref due, dueTime < TimeSpan.Zero ? long.MaxValue : checked(clock.GetTimestamp() + dueTime.Ticks)); return true; }
            internal void Fire(long now) { long target = Interlocked.Read(ref due); if (now >= target && Interlocked.CompareExchange(ref due, long.MaxValue, target) == target) callback(state); }
            public void Dispose() => Interlocked.Exchange(ref due, long.MaxValue);
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
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
