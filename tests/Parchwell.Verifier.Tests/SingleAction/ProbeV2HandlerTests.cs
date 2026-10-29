using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Scenario;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2HandlerTests
{
    private static readonly string[] SuccessEvents = ["candidate","stop","dispose","receipt"];
    private static readonly string[] FailedCleanupEvents = ["candidate","stop","dispose"], CancelledEvents = ["stop", "dispose"];
    [Fact]
    public async Task Given_CompleteEmittedInputs_When_HostRuns_Then_ReceiptFollowsDisposalAndBindsExactCandidateBytes()
    {
        var fixture = await Fixture.CreateAsync();
        Assert.Equal(0,await ProbeV2Host.RunAsync(() => fixture.Host));
        Assert.Equal(SuccessEvents,fixture.Events);
        byte[] candidateBytes = fixture.Store.Files[fixture.Prefix + "candidate.json"];
        var candidate = ProbeV2Wire.Read<ProbeV2Candidate>(candidateBytes,16777216);
        Assert.True(SingleActionConfigurationJudgement.V2SmokeAccepted(candidate)); Assert.Null(candidate.ItemAssessment);
        byte[] receiptBytes = fixture.Store.Files[fixture.Prefix + "judge-completion.json"];
        var receipt = ProbeV2Wire.Read<ProbeV2JudgeReceipt>(receiptBytes,65536);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(candidateBytes)),receipt.CandidateSha256);
        Assert.True(SingleActionConfigurationJudgement.V2Published(fixture.Options,"C0",candidate,receipt,candidateBytes));
        Assert.False(SingleActionConfigurationJudgement.V2Published(fixture.Options,"C0",candidate,receipt,candidateBytes.Concat(new byte[] { 32 }).ToArray()));
        string? export = Environment.GetEnvironmentVariable("PARCHWELL_V2_CONTRACT_EXPORT");
        if (!string.IsNullOrEmpty(export))
        {
            Directory.CreateDirectory(export);
            await File.WriteAllBytesAsync(Path.Combine(export,"candidate.json"),candidateBytes,TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(export,"judge-completion.json"),receiptBytes,TestContext.Current.CancellationToken);
        }
    }
    [Theory]
    [InlineData("stop")][InlineData("dispose")]
    public async Task Given_HostCleanupFailure_When_Publishing_Then_NoSuccessfulJudgeReceipt(string failure)
    {
        var fixture = await Fixture.CreateAsync(); fixture.Host.Failure = failure;
        Assert.Equal(1,await ProbeV2Host.RunAsync(() => fixture.Host));
        Assert.Equal(FailedCleanupEvents,fixture.Events); Assert.False(fixture.Store.Files.ContainsKey(fixture.Prefix + "judge-completion.json"));
    }
    [Theory]
    [InlineData("missing","incomplete")][InlineData("invalid","invalid")][InlineData("oversized","incomplete")][InlineData("bad_pod","invalid")]
    public async Task Given_UnusableEvidence_When_JudgeRuns_Then_DiagnosticCandidateAndOwnFinalReceiptRemain(string mutation,string status)
    {
        var fixture = await Fixture.CreateAsync(); string capture = "/run/probe-inputs/run1-b08-c0-action/capture.json";
        if (mutation == "missing") fixture.Inputs.Files.Remove(capture);
        if (mutation == "invalid") fixture.Inputs.Files[capture] = "{}"u8.ToArray();
        if (mutation == "oversized") fixture.Inputs.Files[capture] = new byte[fixture.Options.Limits.MaxCaptureBytes + 1];
        if (mutation == "bad_pod")
        {
            string path = "/run/probe-inputs/run1-b08-c0-action/pod.json";
            var pod = ProbeV2Wire.Read<ProbeV2Pod>(fixture.Inputs.Files[path],65536);
            fixture.Inputs.Files[path] = ProbeV2Wire.Write(pod with { ImageDigest = "sha256:" + new string('c',64) },65536);
        }
        Assert.Equal(0,await ProbeV2Host.RunAsync(() => fixture.Host));
        var candidate = ProbeV2Wire.Read<ProbeV2Candidate>(fixture.Store.Files[fixture.Prefix + "candidate.json"],16777216);
        Assert.Equal(status,candidate.Status); Assert.Null(candidate.Deterministic); Assert.Null(candidate.ItemAssessment);
        Assert.False(SingleActionConfigurationJudgement.V2SmokeAccepted(candidate));
        Assert.True(fixture.Store.Files.ContainsKey(fixture.Prefix + "judge-completion.json"));
    }
    [Theory]
    [InlineData("capture",-1)][InlineData("capture",0)][InlineData("capture",1)][InlineData("capture",-2)]
    [InlineData("registration",-1)][InlineData("registration",0)][InlineData("registration",1)][InlineData("registration",-2)]
    [InlineData("pod",-1)][InlineData("pod",0)][InlineData("pod",1)][InlineData("pod",-2)]
    [InlineData("scope",-1)][InlineData("scope",0)][InlineData("scope",1)][InlineData("scope",-2)]
    public async Task Given_MountedInputBytes_When_JudgeRuns_Then_ExactBoundIsInclusiveAndEmptyIsMalformed(string kind,int delta)
    {
        var fixture = await Fixture.CreateAsync();
        string path = kind == "scope" ? "/run/probe-inputs/scope/role-scope.json" : "/run/probe-inputs/run1-b08-c0-action/" + kind + ".json";
        int limit = kind switch { "capture" => fixture.Options.Limits.MaxCaptureBytes,"scope" => 131072,_ => 65536 };
        fixture.Inputs.Files[path] = delta == -2 ? [] : Pad(fixture.Inputs.Files[path],limit + delta);
        Assert.Equal(0,await ProbeV2Host.RunAsync(() => fixture.Host));
        var candidate = ProbeV2Wire.Read<ProbeV2Candidate>(fixture.Store.Files[fixture.Prefix + "candidate.json"],16777216);
        Assert.Equal(delta == -2 ? "invalid" : delta > 0 ? "incomplete" : "complete_valid",candidate.Status);
        string[] reasons = candidate.Diagnostics.Concat(candidate.Repeats.SelectMany(row => row.Reasons)).ToArray();
        Assert.Equal(delta > 0,reasons.Contains("v2_capture_limit",StringComparer.Ordinal));
        Assert.Equal(delta == -2,reasons.Contains("v2_wire",StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("files",-1)][InlineData("files",0)][InlineData("files",1)]
    [InlineData("bytes",-1)][InlineData("bytes",0)][InlineData("bytes",1)]
    public async Task Given_AggregateInputBudget_When_JudgeRuns_Then_TotalAtLimitIsAdmittedAndExceededIsIncomplete(string kind,int delta)
    {
        var fixture = await Fixture.CreateAsync();
        if (kind == "files") fixture.Options.JudgeLimits.MaxInputFiles = fixture.Inputs.Files.Count + delta;
        else fixture.Options.JudgeLimits.MaxTotalInputBytes = fixture.Inputs.Files.Values.Sum(bytes => bytes.Length) + delta;
        Assert.Equal(0,await ProbeV2Host.RunAsync(() => fixture.Host));
        var candidate = ProbeV2Wire.Read<ProbeV2Candidate>(fixture.Store.Files[fixture.Prefix + "candidate.json"],16777216);
        Assert.Equal(delta < 0 ? "incomplete" : "complete_valid",candidate.Status);
        Assert.Equal(delta < 0,candidate.Diagnostics.Contains("v2_capture_limit",StringComparer.Ordinal));
        Assert.DoesNotContain("v2_wire",candidate.Diagnostics);
    }

    [Theory]
    [InlineData("candidate",-1)][InlineData("candidate",0)][InlineData("candidate",1)][InlineData("candidate",-2)]
    [InlineData("receipt",-1)][InlineData("receipt",0)][InlineData("receipt",1)][InlineData("receipt",-2)]
    public async Task Given_PriorBlobBytes_When_FinalJudgeRuns_Then_ExhaustionRetainsItsDiagnostic(string kind,int delta)
    {
        var fixture = await Fixture.CreateAsync("C1");
        string prefix = ProbeV2Matrix.Prefix("run1","B08","C0");
        byte[] bytes = ProbeV2Wire.Write(ProbeV2Fixture.Candidate(),16777216);
        if (kind == "candidate") bytes = delta == -2 ? [] : Pad(bytes,16777216 + delta);
        var receipt = new ProbeV2JudgeReceipt(2,"single-action-v2-judge","run1","B08","C0",ProbeV2Fixture.Source,
            "run1-b08-c0-judge","run1-b08-c0-judge-abc","prior-uid",prefix + "candidate.json",Convert.ToHexStringLower(SHA256.HashData(bytes)),0,true);
        byte[] receiptBytes = ProbeV2Wire.Write(receipt,65536);
        if (kind == "receipt") receiptBytes = delta == -2 ? [] : Pad(receiptBytes,65536 + delta);
        fixture.Store.Files.Add(prefix + "candidate.json",bytes);
        fixture.Store.Files.Add(prefix + "judge-completion.json",receiptBytes);
        Assert.Equal(0,await ProbeV2Host.RunAsync(() => fixture.Host));
        var candidate = ProbeV2Wire.Read<ProbeV2Candidate>(fixture.Store.Files[fixture.Prefix + "candidate.json"],16777216);
        Assert.Equal(delta is 1 or -2 ? "incomplete" : "complete_valid",candidate.Status);
        Assert.Equal(delta > 0,candidate.Diagnostics.Contains("v2_capture_limit",StringComparer.Ordinal));
        if (delta is 1 or -2) Assert.Contains("v2_controls",candidate.Diagnostics);
    }

    private static byte[] Pad(byte[] source,int length)
    {
        byte[] padded = new byte[length]; padded.AsSpan().Fill(32); source.CopyTo(padded,0); return padded;
    }

    [Theory]
    [InlineData("registration")][InlineData("capture")][InlineData("pod")]
    public async Task Given_MissingFileAndWrongAvailableIdentity_When_HostPublishes_Then_CandidateIsInvalid(string missing)
    {
        var fixture = await Fixture.CreateAsync(); string root = "/run/probe-inputs/run1-b08-c0-action/";
        fixture.Inputs.Files.Remove(root + missing + ".json");
        if (missing == "pod")
        {
            var registration = ProbeV2Wire.Read<ProbeV2Registration>(fixture.Inputs.Files[root + "registration.json"],65536);
            fixture.Inputs.Files[root + "registration.json"] = ProbeV2Wire.Write(registration with { SourceCommit = new string('2',40) },65536);
        }
        else
        {
            var pod = ProbeV2Wire.Read<ProbeV2Pod>(fixture.Inputs.Files[root + "pod.json"],65536);
            fixture.Inputs.Files[root + "pod.json"] = ProbeV2Wire.Write(pod with { ImageDigest = "sha256:" + new string('c',64) },65536);
        }
        Assert.Equal(0,await ProbeV2Host.RunAsync(() => fixture.Host));
        var candidate = ProbeV2Wire.Read<ProbeV2Candidate>(fixture.Store.Files[fixture.Prefix + "candidate.json"],16777216);
        Assert.Equal("invalid",candidate.Status); Assert.Null(candidate.Deterministic);
        Assert.All(candidate.Repeats,row => { Assert.Contains("v2_identity",row.Reasons); Assert.Contains("v2_completion",row.Reasons); });
    }

    [Fact]
    public async Task Given_ExistingCandidate_When_JudgeRuns_Then_CreateOnlyWriteCannotOverwriteIt()
    {
        var fixture = await Fixture.CreateAsync(); fixture.Store.Files.Add(fixture.Prefix + "candidate.json","existing"u8.ToArray());
        Assert.Equal(1,await ProbeV2Host.RunAsync(() => fixture.Host));
        Assert.Equal("existing",Encoding.UTF8.GetString(fixture.Store.Files[fixture.Prefix + "candidate.json"]));
        Assert.False(fixture.Store.Files.ContainsKey(fixture.Prefix + "judge-completion.json"));
    }
    [Theory]
    [InlineData("valid",true)][InlineData("hash",false)][InlineData("missing_receipt",false)][InlineData("image",false)][InlineData("forged_aggregate",false)]
    public async Task Given_PriorConfiguration_When_FinalJudgeRuns_Then_OnlyBoundPublishedCandidateIsAControl(string mutation,bool valid)
    {
        var fixture = await Fixture.CreateAsync("C1"); var prior = ProbeV2Fixture.Candidate();
        if (mutation == "image") prior.ImageDigests["loadgen"] = "sha256:" + new string('c',64);
        if (mutation == "forged_aggregate") prior = prior with { Repeats = prior.Repeats.Select(row => row with { Status = "incomplete" }).ToArray() };
        string prefix = ProbeV2Matrix.Prefix("run1","B08","C0"); byte[] bytes = ProbeV2Wire.Write(prior,16777216);
        fixture.Store.Files.Add(prefix + "candidate.json",bytes);
        var receipt = new ProbeV2JudgeReceipt(2,"single-action-v2-judge","run1","B08","C0",ProbeV2Fixture.Source,
            "run1-b08-c0-judge","run1-b08-c0-judge-abc","prior-uid",prefix + "candidate.json",
            mutation == "hash" ? new string('0',64) : Convert.ToHexStringLower(SHA256.HashData(bytes)),0,true);
        if (mutation != "missing_receipt") fixture.Store.Files.Add(prefix + "judge-completion.json",ProbeV2Wire.Write(receipt,65536));
        Assert.Equal(0,await ProbeV2Host.RunAsync(() => fixture.Host));
        var candidate = ProbeV2Wire.Read<ProbeV2Candidate>(fixture.Store.Files[fixture.Prefix + "candidate.json"],16777216);
        Assert.Equal(valid,SingleActionConfigurationJudgement.V2SmokeAccepted(candidate));
        Assert.Equal(valid ? "complete_valid" : "incomplete",candidate.Status);
    }

    [Fact]
    public async Task Given_ApplicationStopping_When_ReadingInputs_Then_CancellationPreventsPublicationAndStillCleansUp()
    {
        var fixture = await Fixture.CreateAsync(); fixture.Inputs.BeforeRead = fixture.Lifetime.StopApplication;
        Assert.Equal(1,await ProbeV2Host.RunAsync(() => fixture.Host));
        Assert.Empty(fixture.Store.Files); Assert.Equal(CancelledEvents,fixture.Events);
    }

    private sealed class Lifetime : IHostApplicationLifetime,IDisposable
    {
        private readonly CancellationTokenSource stopping = new();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => stopping.Cancel();
        public void Dispose() => stopping.Dispose();
    }

    private sealed class Fixture
    {
        internal Lifetime Lifetime { get; } = new();
        internal ProbeV2Options Options { get; private set; } = null!;
        internal Inputs Inputs { get; } = new();
        internal List<string> Events { get; } = [];
        internal Store Store { get; private set; } = null!;
        internal TestHost Host { get; private set; } = null!;
        internal string Prefix => ProbeV2Matrix.Prefix(Options.RunId,Options.ScenarioId,Options.ConfigurationId);
        internal static async Task<Fixture> CreateAsync(string configuration = "C0")
        {
            var fixture = new Fixture { Options = ProbeV2Fixture.Options("B08",configuration) }; fixture.Store = new(fixture.Events);
            var loader = new ScenarioLoader(new FileSystemScenarioSource(AppContext.BaseDirectory),Microsoft.Extensions.Options.Options.Create(fixture.Options.ScenarioLoadOptions));
            var loaded = await loader.LoadAsync("B08",TestContext.Current.CancellationToken); var resolver = new ProbeV2Resolver(fixture.Options);
            foreach (var (_,evidence) in ProbeV2Fixture.Evidence(fixture.Options))
            {
                var capture = evidence.Capture!; var matrix = ProbeV2Matrix.For("B08",evidence.Configuration,evidence.Stage);
                var rows = capture.Repeats.Select(row =>
                {
                    var resolved = resolver.Resolve(loaded,row.ClientId!,evidence.Configuration,matrix.Role);
                    return row with { Settings = resolved.Settings.ToDictionary(x => x.Key,x => x.Value.Value,StringComparer.Ordinal),
                        OmittedKeys = resolved.Omissions.Select(x => x.Key).Concat(resolved.ScenarioOmissions.IsDefault ? [] : resolved.ScenarioOmissions.Select(x => x.Key)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
                }).ToArray();
                string root = Path.Combine(fixture.Options.InputRoot,capture.JobName);
                fixture.Inputs.Files.Add(Path.Combine(root,"registration.json"),ProbeV2Wire.Write(evidence.Registration!,65536));
                fixture.Inputs.Files.Add(Path.Combine(root,"capture.json"),ProbeV2Wire.Write(capture with { Repeats = rows },fixture.Options.Limits.MaxCaptureBytes));
                fixture.Inputs.Files.Add(Path.Combine(root,"pod.json"),ProbeV2Wire.Write(evidence.Pod!,65536));
            }
            fixture.Inputs.Files.Add("/run/probe-inputs/scope/role-scope.json",ProbeV2Wire.Write(ProbeV2Fixture.Scope(fixture.Options),131072));
            var handler = new ProbeV2Handler(fixture.Options,fixture.Inputs,fixture.Store,loader,resolver,TimeProvider.System);
            var services = new ServiceCollection().AddSingleton(fixture.Options).AddSingleton<TimeProvider>(TimeProvider.System)
                .AddSingleton<IVerifierArtifactStore>(fixture.Store).AddSingleton<IHostApplicationLifetime>(fixture.Lifetime).AddSingleton(handler).BuildServiceProvider();
            fixture.Host = new(services,fixture.Events); return fixture;
        }
    }
    private sealed class Inputs : IProbeV2Inputs
    {
        internal Dictionary<string,byte[]> Files { get; } = new(StringComparer.Ordinal);
        internal Action? BeforeRead { get; set; }
        public Task<byte[]?> ReadAsync(string path,int maximum,CancellationToken cancellationToken) { BeforeRead?.Invoke(); cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Files.GetValueOrDefault(path)); }
    }
    private sealed class Store(List<string> events) : IVerifierArtifactStore
    {
        internal Dictionary<string,byte[]> Files { get; } = new(StringComparer.Ordinal);
        public Task<byte[]?> ReadAsync(string name,CancellationToken cancellationToken) => Task.FromResult(Files.GetValueOrDefault(name));
        public Task<BlobListing> ListAsync(string prefix,CancellationToken cancellationToken) => throw new InvalidOperationException("V2 must not list blobs");
        public Task CreateAsync(string name,byte[] content,CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Files.TryAdd(name,content)) throw new IOException("synthetic_duplicate_blob");
            events.Add(name.EndsWith("judge-completion.json",StringComparison.Ordinal) ? "receipt" : "candidate"); return Task.CompletedTask;
        }
    }
    private sealed class TestHost(ServiceProvider provider,List<string> events) : IHost
    {
        internal string? Failure { get; set; }
        public IServiceProvider Services => provider;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) { events.Add("stop"); if (Failure == "stop") throw new IOException("synthetic"); return Task.CompletedTask; }
        public void Dispose() { events.Add("dispose"); provider.Dispose(); if (Failure == "dispose") throw new IOException("synthetic"); }
    }
}
