using System.Security.Cryptography;
using System.Text;
using Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Scenario;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.IdleObservation;
using Xunit;
using static Parchwell.Verifier.Tests.IdleObservation.IdleFixture;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.IdleObservation;

public sealed class IdleObservationJudgeHandlerTests
{
    private static readonly string[] Items = ["A02", "A03", "A04"];

    [Fact]
    public async Task Given_SevenValidCaptures_When_Judged_Then_ThreeCandidatesAreWrittenAtTheContractPaths()
    {
        var fixture = await Fixture.CreateAsync();
        var references = await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Items, references.Select(x => x.ScenarioId));
        Assert.Equal(["complete_valid", "complete_valid", "complete_valid"], references.Select(x => x.Status));
        Assert.Equal(["no_idle_close_observed", "no_idle_close_observed", "not_in_librdkafka_table"], references.Select(x => x.Reason));
        foreach (var reference in references)
        {
            Assert.Equal($"run1/idle-observation/{reference.ScenarioId}/judge/candidate.json", reference.CandidateName);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(fixture.Store.Files[reference.CandidateName])), reference.CandidateSha256);
        }
        var candidates = Items.ToDictionary(item => item, item => SingleActionWire.Read<IdleObservationCandidate>(fixture.Store.Files[IdleObservationPaths.Candidate("run1", item)], 16777216));
        Assert.Equal([2, 3, 2], Items.Select(item => candidates[item].Configurations.Length));
        Assert.All(candidates.Values, candidate => Assert.All(candidate.Configurations, configuration => Assert.Equal("complete_valid", configuration.Status)));
        Assert.All(candidates.Values, candidate => Assert.Equal("kept", candidate.Configurations[0].Summary));
        Assert.Equal(("untestable", "no_idle_close_observed"), (candidates["A02"].ItemAssessment.Verdict, candidates["A02"].ItemAssessment.Reason));
        Assert.Equal(["k37_not_tested"], candidates["A02"].Notes);
        Assert.Empty(candidates["A03"].Notes);
        Assert.Equal(("undocumented", "not_in_librdkafka_table"), (candidates["A04"].ItemAssessment.Verdict, candidates["A04"].ItemAssessment.Reason));
        Assert.Equal("contradicted", candidates["A04"].ItemAssessment.Hypotheses["H_client_close"]);
        Assert.All(candidates.Values, candidate => Assert.Equal("2.16.0", candidate.NativeVersion));
        Assert.All(candidates.Values, candidate => Assert.Equal("not_applicable", candidate.Adr012Counts));
    }

    [Fact]
    public async Task Given_SmokeRun_When_Judged_Then_CandidatesCarryEvidenceButNoVerdict()
    {
        var fixture = await Fixture.CreateAsync(runKind: "smoke");
        await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken);
        var candidate = SingleActionWire.Read<IdleObservationCandidate>(fixture.Store.Files[IdleObservationPaths.Candidate("run1", "A02")], 16777216);
        Assert.Equal((null, "smoke_only"), (candidate.ItemAssessment.Verdict, candidate.ItemAssessment.Reason));
        Assert.Equal(5, candidate.Configurations[0].Repeats.Length);
        Assert.Equal(29, candidate.Configurations[0].Repeats[0].Reports.Length);
    }

    [Fact]
    public async Task Given_MissingCapturePod_When_Judged_Then_ConfigurationIsIncompleteAndItemHasNoVerdict()
    {
        var fixture = await Fixture.CreateAsync(pods: PodKeys.Where(key => key != "A03C2"));
        await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken);
        var candidate = Read(fixture, "A03");
        var missing = candidate.Configurations.Single(x => x.ConfigurationId == "C2");
        Assert.Equal("incomplete", missing.Status);
        Assert.All(missing.Repeats, repeat => Assert.Equal(["idle_capture_missing"], repeat.Reasons));
        Assert.Equal((null, "incomplete_configuration"), (candidate.ItemAssessment.Verdict, candidate.ItemAssessment.Reason));
    }

    [Fact]
    public async Task Given_MissingBlob_When_Judged_Then_CaptureIsMissing()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Ledger.Files.Remove("run1/idle-observation/A02/C1/instances/" + CapturePod("A02C1") + "/completion.json");
        await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.All(Read(fixture, "A02").Configurations[1].Repeats, repeat => Assert.Equal(["idle_capture_missing"], repeat.Reasons));
    }

    [Fact]
    public async Task Given_StorageFailure_When_Judged_Then_CaptureIsMissing()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Ledger.Failing.Add("run1/idle-observation/A02/C1/instances/" + CapturePod("A02C1") + "/capture.json");
        await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken);
        var configuration = Read(fixture, "A02").Configurations[1];
        Assert.Equal("incomplete", configuration.Status);
        Assert.All(configuration.Repeats, repeat => Assert.Equal(["idle_capture_missing"], repeat.Reasons));
    }

    [Fact]
    public async Task Given_OverLimitCapture_When_Judged_Then_CaptureLimitIsReportedAsIncomplete()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Ledger.OverLimit.Add("run1/idle-observation/A02/C0/instances/" + CapturePod("A02C0") + "/capture.json");
        await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken);
        var configuration = Read(fixture, "A02").Configurations[0];
        Assert.Equal("incomplete", configuration.Status);
        Assert.All(configuration.Repeats, repeat => Assert.Equal(["idle_capture_limit"], repeat.Reasons));
    }

    [Fact]
    public async Task Given_MalformedCaptureJson_When_Judged_Then_WireIsInvalid()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Ledger.Files["run1/idle-observation/A02/C0/instances/" + CapturePod("A02C0") + "/capture.json"] = Encoding.UTF8.GetBytes("{\"schema_version\":");
        await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken);
        var configuration = Read(fixture, "A02").Configurations[0];
        Assert.Equal("invalid", configuration.Status);
        Assert.All(configuration.Repeats, repeat => Assert.Equal(["idle_wire"], repeat.Reasons));
    }

    [Fact]
    public async Task Given_SettingsThatContradictResolvedA04C0Omission_When_Judged_Then_RepeatsAreInvalidForSettings()
    {
        var fixture = await Fixture.CreateAsync(alter: (item, configuration, capture) => item == "A04" && configuration == "C0"
            ? capture with { Repeats = [.. capture.Repeats.Select(repeat =>
            {
                repeat.Settings["connections.max.idle.ms"] = "180000";
                return repeat;
            })] }
            : capture);
        await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken);
        var configuration = Read(fixture, "A04").Configurations[0];
        Assert.Equal("invalid", configuration.Status);
        Assert.All(configuration.Repeats, repeat => Assert.Contains("idle_settings", repeat.Reasons));
        Assert.Equal((null, "incomplete_configuration"), (Read(fixture, "A04").ItemAssessment.Verdict, Read(fixture, "A04").ItemAssessment.Reason));
    }

    [Fact]
    public async Task Given_ForeignIdentityInCapture_When_Judged_Then_CaptureIdentityIsInvalid()
    {
        var fixture = await Fixture.CreateAsync(alter: (item, configuration, capture) => item == "A03" && configuration == "C1" ? capture with { PodUid = "other" } : capture);
        await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken);
        var configuration = Read(fixture, "A03").Configurations[1];
        Assert.Equal("invalid", configuration.Status);
        Assert.All(configuration.Repeats, repeat => Assert.Equal(["idle_capture_identity"], repeat.Reasons));
    }

    [Fact]
    public async Task Given_CompleteCaptures_When_HostRuns_Then_ReceiptFollowsDisposalAndBindsEveryCandidateHash()
    {
        var fixture = await Fixture.CreateAsync();
        Assert.Equal(0, await IdleObservationJudgeHost.RunAsync(() => fixture.Host));
        Assert.Equal(["candidate", "candidate", "candidate", "stop", "dispose", "receipt"], fixture.Events);
        var receipt = SingleActionWire.Read<IdleObservationJudgeReceipt>(fixture.Store.Files["run1/idle-observation/judge-completion.json"], 65536);
        Assert.Equal(("idle-observation-judge", "run1-idle-judge", 0, true), (receipt.Mode, receipt.JobName, receipt.HandlerExitCode, receipt.CleanupCompleted));
        Assert.Equal(3, receipt.Candidates.Length);
        foreach (var reference in receipt.Candidates)
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(fixture.Store.Files[reference.CandidateName])), reference.CandidateSha256);
        Assert.Equal(["complete_valid", "complete_valid", "complete_valid"], receipt.Candidates.Select(x => x.Status));
        Assert.Equal(["no_idle_close_observed", "no_idle_close_observed", "not_in_librdkafka_table"], receipt.Candidates.Select(x => x.Reason));
    }

    [Fact]
    public async Task Given_MalformedCaptureForOneConfiguration_When_HostRuns_Then_ReceiptIsWrittenAndExitCodeIsOne()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Ledger.Files["run1/idle-observation/A02/C0/instances/" + CapturePod("A02C0") + "/capture.json"] = Encoding.UTF8.GetBytes("{\"schema_version\":");
        Assert.Equal(1, await IdleObservationJudgeHost.RunAsync(() => fixture.Host));
        Assert.Equal(["candidate", "candidate", "candidate", "stop", "dispose", "receipt"], fixture.Events);
        var receipt = SingleActionWire.Read<IdleObservationJudgeReceipt>(fixture.Store.Files["run1/idle-observation/judge-completion.json"], 65536);
        Assert.Equal((1, true), (receipt.HandlerExitCode, receipt.CleanupCompleted));
        Assert.Equal(Items, receipt.Candidates.Select(x => x.ScenarioId));
        Assert.Equal([("invalid", "incomplete_configuration"), ("complete_valid", "no_idle_close_observed"), ("complete_valid", "not_in_librdkafka_table")],
            receipt.Candidates.Select(x => (x.Status, x.Reason)));
    }

    [Fact]
    public async Task Given_MissingA03C2Pod_When_HostRuns_Then_ReceiptRecordsIncompleteConfigurationAndExitCodeIsOne()
    {
        var fixture = await Fixture.CreateAsync(pods: PodKeys.Where(key => key != "A03C2"));
        Assert.Equal(1, await IdleObservationJudgeHost.RunAsync(() => fixture.Host));
        var receipt = SingleActionWire.Read<IdleObservationJudgeReceipt>(fixture.Store.Files["run1/idle-observation/judge-completion.json"], 65536);
        Assert.Equal((1, true), (receipt.HandlerExitCode, receipt.CleanupCompleted));
        var a03 = receipt.Candidates.Single(x => x.ScenarioId == "A03");
        Assert.Equal(("incomplete", "incomplete_configuration"), (a03.Status, a03.Reason));
    }

    [Fact]
    public async Task Given_MissingA03C2PodInSmokeRun_When_HostRuns_Then_ReceiptRecordsSmokeOnlyAndExitCodeIsOne()
    {
        var fixture = await Fixture.CreateAsync(runKind: "smoke", pods: PodKeys.Where(key => key != "A03C2"));
        Assert.Equal(1, await IdleObservationJudgeHost.RunAsync(() => fixture.Host));
        var receipt = SingleActionWire.Read<IdleObservationJudgeReceipt>(fixture.Store.Files["run1/idle-observation/judge-completion.json"], 65536);
        Assert.Equal(1, receipt.HandlerExitCode);
        var a03 = receipt.Candidates.Single(x => x.ScenarioId == "A03");
        Assert.Equal(("incomplete", "smoke_only"), (a03.Status, a03.Reason));
    }

    [Theory]
    [InlineData("complete_valid", "no_idle_close_observed", 0)]
    [InlineData("complete_valid", "smoke_only", 0)]
    [InlineData("invalid", "smoke_only", 1)]
    [InlineData("incomplete", "smoke_only", 1)]
    [InlineData("complete_valid", "incomplete_configuration", 1)]
    public void Given_ReceiptEntries_When_ExitCodeIsComputed_Then_OnlyValidCompleteEntriesGiveZero(string status, string reason, int expected)
    {
        IdleCandidateReference[] entries =
        [
            new("A02", "run1/idle-observation/A02/judge/candidate.json", new string('a', 64), "complete_valid", "not_in_librdkafka_table"),
            new("A03", "run1/idle-observation/A03/judge/candidate.json", new string('b', 64), status, reason)
        ];
        Assert.Equal(expected, IdleObservationJudgement.ExitCode(entries));
    }

    [Theory]
    [InlineData("invalid", "incomplete", "complete_valid", "invalid")]
    [InlineData("incomplete", "invalid", "complete_valid", "invalid")]
    [InlineData("complete_valid", "incomplete", "invalid", "invalid")]
    [InlineData("incomplete", "complete_valid", "invalid", "invalid")]
    [InlineData("invalid", "complete_valid", "complete_valid", "invalid")]
    [InlineData("complete_valid", "incomplete", "complete_valid", "incomplete")]
    [InlineData("complete_valid", "complete_valid", "complete_valid", "complete_valid")]
    public void Given_ConfigurationStatuses_When_ItemStatusIsComputed_Then_InvalidBeatsIncompleteBeatsCompleteValid(string c0, string c1, string c2, string expected)
    {
        IdleConfigurationResult[] results = [Summary("C0", null, c0), Summary("C1", null, c1), Summary("C2", null, c2)];
        Assert.Equal(expected, IdleObservationJudgement.ItemStatus(results));
    }

    [Fact]
    public void Given_NoConfigurations_When_ItemStatusIsComputed_Then_StatusIsCompleteValid()
    {
        // Vacuous truth, not a judgement. The handler cannot reach this: every item has at least two configurations and each one is added to the results.
        Assert.Equal("complete_valid", IdleObservationJudgement.ItemStatus([]));
    }

    [Theory]
    [InlineData("A03C0", "A03C2")]
    [InlineData("A03C2", "A03C0")]
    public async Task Given_OneInvalidAndOneMissingConfigurationInAnItem_When_HostRuns_Then_ReceiptItemIsInvalidAndExitCodeIsOne(string malformed, string missing)
    {
        var fixture = await Fixture.CreateAsync(pods: PodKeys.Where(key => key != missing));
        fixture.Ledger.Files[$"run1/idle-observation/{malformed[..3]}/{malformed[3..]}/instances/" + CapturePod(malformed) + "/capture.json"] = Encoding.UTF8.GetBytes("{\"schema_version\":");
        Assert.Equal(1, await IdleObservationJudgeHost.RunAsync(() => fixture.Host));
        var receipt = SingleActionWire.Read<IdleObservationJudgeReceipt>(fixture.Store.Files["run1/idle-observation/judge-completion.json"], 65536);
        Assert.Equal(1, receipt.HandlerExitCode);
        Assert.Equal([("complete_valid", "no_idle_close_observed"), ("invalid", "incomplete_configuration"), ("complete_valid", "not_in_librdkafka_table")],
            receipt.Candidates.Select(x => (x.Status, x.Reason)));
        var a03 = Read(fixture, "A03");
        Assert.Equal(("invalid", "incomplete"), (a03.Configurations.Single(x => x.ConfigurationId == malformed[3..]).Status, a03.Configurations.Single(x => x.ConfigurationId == missing[3..]).Status));
    }

    [Fact]
    public async Task Given_HandlerFailure_When_HostRuns_Then_NoReceiptIsWritten()
    {
        var fixture = await Fixture.CreateAsync();
        fixture.Store.FailCandidates = true;
        Assert.Equal(1, await IdleObservationJudgeHost.RunAsync(() => fixture.Host));
        Assert.DoesNotContain("run1/idle-observation/judge-completion.json", fixture.Store.Files.Keys);
    }

    private static IdleObservationCandidate Read(Fixture fixture, string item) =>
        SingleActionWire.Read<IdleObservationCandidate>(fixture.Store.Files[IdleObservationPaths.Candidate("run1", item)], 16777216);

    private sealed class Fixture
    {
        internal Ledger Ledger { get; } = new();
        internal List<string> Events { get; } = [];
        internal Store Store { get; private set; } = null!;
        internal IdleObservationJudgeHandler Handler { get; private set; } = null!;
        internal TestHost Host { get; private set; } = null!;

        internal static async Task<Fixture> CreateAsync(string runKind = "batch", IEnumerable<string>? pods = null,
            Func<string, string, IdleCapture, IdleCapture>? alter = null)
        {
            var fixture = new Fixture();
            fixture.Store = new(fixture.Events);
            var options = JudgeOptions(runKind);
            options.CapturePods = pods is null ? options.CapturePods : CapturePodsText(pods);
            var loader = new ScenarioLoader(new FileSystemScenarioSource(AppContext.BaseDirectory), Microsoft.Extensions.Options.Options.Create(options.ScenarioLoadOptions));
            var resolver = new IdleObservationResolver(options);
            foreach (string key in PodKeys.Where(options.Pods().ContainsKey))
            {
                string item = key[..3], configuration = key[3..], pod = CapturePod(key), uid = "uid-" + key;
                LoadedScenario scenario = await loader.LoadAsync(item, TestContext.Current.CancellationToken);
                var identity = new SingleActionIdentity(item, configuration, "run1", pod);
                string job = $"run1-{item.ToLowerInvariant()}-{configuration.ToLowerInvariant()}-idle";
                var repeats = Enumerable.Range(1, 5).Select(ordinal =>
                {
                    var resolved = resolver.Resolve(scenario, $"{pod}-r{ordinal}", configuration, scenario.Definition.IdleObservation!.Role);
                    return Repeat() with
                    {
                        Repeat = ordinal, ClientId = $"{pod}-r{ordinal}",
                        Settings = resolved.Settings.ToDictionary(x => x.Key, x => x.Value.Value, StringComparer.Ordinal),
                        OmittedKeys = [.. resolved.Omissions.Select(x => x.Key).Concat(resolved.ScenarioOmissions.IsDefault ? [] : resolved.ScenarioOmissions.Select(x => x.Key))
                            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]
                    };
                }).ToArray();
                var capture = new IdleCapture(1, "idle-observation", true, runKind, identity, job, pod, uid, CommitSha, "2.16.0", "2.16.0", "document-events", 0, 1000, 420, 15000, 80, repeats);
                capture = alter?.Invoke(item, configuration, capture) ?? capture;
                string root = $"run1/idle-observation/{item}/{configuration}/";
                fixture.Ledger.Files[root + $"registrations/{pod}.json"] = SingleActionWire.Write(new SingleActionRegistration(1, "idle-observation", identity, job, pod, uid, CommitSha), 65536);
                fixture.Ledger.Files[root + $"instances/{pod}/completion.json"] = SingleActionWire.Write(new SingleActionCompletion(1, "idle-observation", identity, job, pod, uid, CommitSha, 5, 0, true), 65536);
                fixture.Ledger.Files[root + $"instances/{pod}/capture.json"] = SingleActionWire.Write(capture, 167772160);
            }
            fixture.Handler = new IdleObservationJudgeHandler(options, fixture.Ledger, fixture.Store, loader, resolver, TimeProvider.System);
            var services = new ServiceCollection().AddSingleton(options).AddSingleton<TimeProvider>(TimeProvider.System)
                .AddSingleton<IVerifierArtifactStore>(fixture.Store).AddSingleton<IHostApplicationLifetime>(new Lifetime()).AddSingleton(fixture.Handler).BuildServiceProvider();
            fixture.Host = new(services, fixture.Events);
            return fixture;
        }
    }

    private sealed class Ledger : IIdleLedger
    {
        internal Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> OverLimit { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> Failing { get; } = new(StringComparer.Ordinal);

        public Task<byte[]?> ReadAsync(string path, long maximum, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Failing.Contains(path)) throw new RequestFailedException(503, "synthetic");
            if (OverLimit.Contains(path)) throw new InvalidDataException("verifier_input_byte_limit_exceeded");
            if (!Files.TryGetValue(path, out byte[]? bytes)) return Task.FromResult<byte[]?>(null);
            if (bytes.Length > maximum) throw new InvalidDataException("verifier_input_byte_limit_exceeded");
            return Task.FromResult<byte[]?>(bytes);
        }
    }

    private sealed class Store(List<string> events) : IVerifierArtifactStore
    {
        internal Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        internal bool FailCandidates { get; set; }
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => Task.FromResult(Files.GetValueOrDefault(name));
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) => throw new InvalidOperationException("The idle judge must not list blobs");

        public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailCandidates && name.EndsWith("candidate.json", StringComparison.Ordinal)) throw new IOException("synthetic_failure");
            if (!Files.TryAdd(name, content)) throw new IOException("synthetic_duplicate_blob");
            events.Add(name.EndsWith("judge-completion.json", StringComparison.Ordinal) ? "receipt" : "candidate");
            return Task.CompletedTask;
        }
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }

    private sealed class TestHost(ServiceProvider provider, List<string> events) : IHost
    {
        public IServiceProvider Services => provider;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) { events.Add("stop"); return Task.CompletedTask; }
        public void Dispose() { events.Add("dispose"); provider.Dispose(); }
    }
}
