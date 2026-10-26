using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Scenario;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class SingleActionHandlerTests
{
    [Theory]
    [InlineData("valid","complete_valid",null)][InlineData("zero_registration","invalid","p1_registration")][InlineData("two_registrations","invalid","p1_registration")][InlineData("listing_incomplete","invalid","p1_registration")]
    [InlineData("missing_completion","incomplete","p2_completion")][InlineData("false_cleanup","incomplete","p2_completion")][InlineData("exit1","incomplete","p2_completion")][InlineData("exit-1","incomplete","p2_completion")][InlineData("exit2","incomplete","p2_completion")]
    [InlineData("malformed_capture","invalid","p3_capture_file")][InlineData("missing_capture","invalid","p3_capture_file")][InlineData("restored_omission","invalid","p5_configuration_fidelity")]
    public async Task Given_CapturedArtifacts_When_HandlerRuns_Then_IndependentResolutionAndDiagnosticCandidate(string mutation,string status,string? reason)
    {
        var fixture=await HandlerFixture.CreateAsync();
        if(mutation=="zero_registration")fixture.Store.Registrations=[];
        if(mutation=="two_registrations")fixture.Store.Registrations.Add(fixture.Prefix+"registrations/other.json");
        if(mutation=="listing_incomplete")fixture.Store.ListingComplete=false;
        if(mutation=="missing_completion")fixture.Store.Files.Remove(fixture.CompletionName);
        if(mutation is "false_cleanup" or "exit1" or "exit-1" or "exit2")
        {
            var c=SingleActionFixture.Completion();c=mutation switch{"false_cleanup"=>c with{CleanupCompleted=false},"exit1"=>c with{HandlerExitCode=1},"exit-1"=>c with{HandlerExitCode=-1},_=>c with{HandlerExitCode=2}};fixture.Store.Files[fixture.CompletionName]=SingleActionWire.Write(c,65536);
        }
        if(mutation=="malformed_capture")fixture.Store.Files[fixture.CaptureName]="{}"u8.ToArray();
        if(mutation=="missing_capture")fixture.Store.Files.Remove(fixture.CaptureName);
        if(mutation=="restored_omission")
        {
            var capture=SingleActionWire.Read<SingleActionCapture>(fixture.Store.Files[fixture.CaptureName],134217728);capture.Repeats[0].Settings["transactional.id"]="restored";fixture.Store.Files[fixture.CaptureName]=SingleActionWire.Write(capture,134217728);
        }
        Assert.Equal(0,await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken));
        string name=SingleActionPaths.Candidate("run1","B01","C0");var candidate=SingleActionCandidateCodec.Read(fixture.Store.Files[name]);Assert.Equal(status,candidate.Status);Assert.Equal(5,candidate.Repeats.Length);
        if(reason is not null)Assert.Contains(candidate.Repeats,r=>r.Reasons.Contains(reason,StringComparer.Ordinal));
        Assert.Null(candidate.ItemAssessment);
        Assert.False(fixture.Completion.TryGetBlobReceipt(out _,out _,out _));
        fixture.Completion.MarkReturned(fixture.Options,0,CancellationToken.None,CancellationToken.None);
        Assert.True(fixture.Completion.TryGetBlobReceipt(out string container,out string receiptName,out byte[] receiptBytes));Assert.Equal("verifier",container);Assert.Equal(fixture.Prefix+"judge-completion.json",receiptName);
        var receipt=SingleActionWire.Read<SingleActionJudgeReceipt>(receiptBytes,65536);Assert.Equal(name,receipt.CandidateName);Assert.True(receipt.CleanupCompleted);Assert.Equal(0,receipt.HandlerExitCode);
    }
    [Fact]
    public async Task Given_CandidateUploadFailure_When_HandlerRuns_Then_NoFinalReceipt()
    {
        var fixture=await HandlerFixture.CreateAsync();fixture.Store.FailWrite=true;await Assert.ThrowsAsync<IOException>(()=>fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken));Assert.False(fixture.Completion.TryGetBlobReceipt(out _,out _,out _));
    }
    [Fact]
    public async Task Given_CandidatePublishedButCleanupFails_When_FinalReceiptQueried_Then_Suppressed()
    {
        var fixture=await HandlerFixture.CreateAsync();await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken);fixture.Completion.MarkReturned(fixture.Options,0,CancellationToken.None,CancellationToken.None);fixture.Completion.MarkFailed();Assert.False(fixture.Completion.TryGetBlobReceipt(out _,out _,out _));Assert.Contains(fixture.Prefix+"candidate.json",fixture.Store.Files.Keys);
    }

    [Theory]
    [InlineData(9999,"complete_valid")][InlineData(10000,"invalid")][InlineData(10001,"invalid")]
    public async Task Given_ListingIoBudget_When_HandlerRuns_Then_ExactMonotonicBoundary(int milliseconds,string status)
    {
        var clock=new IoClock();var fixture=await HandlerFixture.CreateAsync(clock);
        fixture.Store.OnList=()=>clock.Timestamp+=milliseconds;
        Assert.Equal(0,await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken));
        var result=SingleActionCandidateCodec.Read(fixture.Store.Files[fixture.Prefix+"candidate.json"]);Assert.Equal(status,result.Status);
        if(status=="invalid")Assert.All(result.Repeats,r=>Assert.Contains("p1_registration",r.Reasons));
    }
    [Fact]
    public async Task Given_LiteralFiveSlotAssignedFixture_When_HandlerRuns_Then_CompleteValidAndSecurityObservation()
    {
        var fixture=await HandlerFixture.CreateAsync(item:"F04",configuration:"C2");
        Assert.Equal(0,await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken));
        var candidate=SingleActionCandidateCodec.Read(fixture.Store.Files[fixture.Prefix+"candidate.json"]);
        Assert.Equal("complete_valid",candidate.Status);Assert.All(candidate.Repeats,repeat=>Assert.Empty(repeat.Reasons));Assert.True(candidate.SecurityFinding);
    }
    [Theory]
    [MemberData(nameof(SingleActionRepeatJudgementTests.MalformedMatchedFields),MemberType=typeof(SingleActionRepeatJudgementTests))]
    public async Task Given_RawNullOrWrongKindMatchedField_When_HandlerRuns_Then_P7DiagnosticCandidatePublished(string field,string raw)
    {
        var fixture=await HandlerFixture.CreateAsync(item:"F04",configuration:"C2");
        var capture=System.Text.Json.Nodes.JsonNode.Parse(fixture.Store.Files[fixture.CaptureName])!;
        capture["repeats"]![0]!["matched"]![field]=System.Text.Json.Nodes.JsonNode.Parse(raw);
        fixture.Store.Files[fixture.CaptureName]=System.Text.Encoding.UTF8.GetBytes(capture.ToJsonString());
        Assert.Equal(0,await fixture.Handler.ExecuteAsync(TestContext.Current.CancellationToken));
        var candidate=SingleActionCandidateCodec.Read(fixture.Store.Files[fixture.Prefix+"candidate.json"]);
        Assert.Equal("invalid",candidate.Status);Assert.Equal("invalid",candidate.Repeats[0].Status);Assert.Contains("p7_outcome_record",candidate.Repeats[0].Reasons);
        Assert.DoesNotContain("p3_capture_file",candidate.Repeats[0].Reasons);Assert.Null(candidate.Repeats[0].OutcomeClass);
        Assert.All(candidate.Repeats[1..],repeat=>Assert.Equal("complete_valid",repeat.Status));
        fixture.Completion.MarkReturned(fixture.Options,0,CancellationToken.None,CancellationToken.None);Assert.True(fixture.Completion.TryGetBlobReceipt(out _,out _,out _));
    }

    private sealed class IoClock:TimeProvider
    {
        internal long Timestamp {get;set;}
        public override long TimestampFrequency=>1000;
        public override long GetTimestamp()=>Timestamp;
    }

    private sealed class HandlerFixture
    {
        internal Store Store {get;}=new();
        internal VerifierCompletionState Completion {get;}=new();
        internal VerifierHostOptions Options {get;}=new(){Mode="single-action-judge",RunId="run1",ScenarioId="B01",ConfigurationId="C0",SourceCommit=SingleActionFixture.Source,VerifierCommit=SingleActionFixture.Source,JobName="run1-b01-c0-single-judge",PodName="judge-pod",PodUid="judge-uid",NamespaceName="synthetic-namespace",LoadGenImage="x@sha256:"+new string('1',64),VerifierImage="x@sha256:"+new string('2',64),EnrichmentImage="x@sha256:"+new string('3',64)};
        internal string Prefix=>SingleActionPaths.Prefix(Options.RunId!,Options.ScenarioId!,Options.ConfigurationId!);
        private string CapturePod=>SingleActionPaths.CaptureJob(Options.RunId!,Options.ScenarioId!,Options.ConfigurationId!)+"-abc";
        internal string CaptureName=>Prefix+$"instances/{CapturePod}/capture.json";
        internal string CompletionName=>Prefix+$"instances/{CapturePod}/completion.json";
        internal SingleActionHandler Handler {get;private set;}=null!;
        internal static async Task<HandlerFixture> CreateAsync(TimeProvider? clock=null,string item="B01",string configuration="C0")
        {
            var fixture=new HandlerFixture();string root=FindRoot();var loader=new ScenarioLoader(new FileSystemScenarioSource(root),Microsoft.Extensions.Options.Options.Create(new ScenarioLoadOptions{MaxDocumentCharacters=65536,MaxNestingDepth=32,MaxScalarCharacters=65536,MaxParserEvents=20000}));
            fixture.Options.ScenarioId=item;fixture.Options.ConfigurationId=configuration;fixture.Options.JobName=SingleActionPaths.JudgeJob("run1",item,configuration);
            var loaded=await loader.LoadAsync(item,TestContext.Current.CancellationToken);var declaration=loaded.Definition.SingleAction!.Configurations[configuration];
            var registration=SingleActionFixture.Registration() with{Identity=new(item,configuration,"run1",fixture.CapturePod),JobName=SingleActionPaths.CaptureJob("run1",item,configuration),PodName=fixture.CapturePod};
            var capture=SingleActionFixture.Capture() with{Identity=registration.Identity,JobName=registration.JobName,PodName=registration.PodName};
            foreach(var repeat in capture.Repeats)
            {
                string client=$"{fixture.CapturePod}-r{repeat.Repeat}";
                var services=new ServiceCollection();
                foreach(var role in loaded.Definition.Roles)services.AddParchwellKafka(role,o=>{o.NamespaceName="synthetic-namespace";o.ClientId=client;o.EnrichmentGroupId=$"single-action-run1-{item.ToLowerInvariant()}-{configuration.ToLowerInvariant()}";});
                using var provider=services.BuildServiceProvider();
                var resolved=new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded).Configurations[configuration].Roles[declaration.Role];
                var statistic=SingleActionFixture.Statistic(client,1,1,1);
                if(declaration.Action is "subscribe" or "assign_consume")statistic=statistic with{RawBase64=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(statistic.RawBase64)).Replace("producer","consumer",StringComparison.Ordinal)))};
                capture.Repeats[repeat.Repeat-1]=repeat with{ClientId=client,Action=declaration.Action,Matched=declaration.Action=="assign_consume"?SingleActionFixture.Matched():SingleActionFixture.Json("null"),Statistics=[statistic],Settings=resolved.Settings.ToDictionary(p=>p.Key,p=>p.Value.Value,StringComparer.Ordinal),OmittedKeys=resolved.Omissions.Select(o=>o.Key).Concat(resolved.ScenarioOmissions.Select(o=>o.Key)).Order(StringComparer.Ordinal).ToArray()};
            }
            var completed=SingleActionFixture.Completion() with{Identity=registration.Identity,JobName=registration.JobName,PodName=registration.PodName};
            string registrationName=fixture.Prefix+$"registrations/{fixture.CapturePod}.json";fixture.Store.Registrations=[registrationName];fixture.Store.Files[registrationName]=SingleActionWire.Write(registration,65536);fixture.Store.Files[fixture.CaptureName]=SingleActionWire.Write(capture,134217728);fixture.Store.Files[fixture.CompletionName]=SingleActionWire.Write(completed,65536);
            var seed=new MemorySeed();
            if(item=="F04")
            {
                fixture.Options.SeedReceiptPath="/run/seed/receipt.json";fixture.Options.SeedPodIdentityPath="/run/seed/pod.json";
                seed.Files[fixture.Options.SeedReceiptPath]=SingleActionWire.Write(SingleActionFixture.Seed(),65536);seed.Files[fixture.Options.SeedPodIdentityPath]=SingleActionWire.Write(SingleActionFixture.SeedPod(),65536);
            }
            fixture.Handler=new(Microsoft.Extensions.Options.Options.Create(fixture.Options),fixture.Store,fixture.Store,loader,seed,new SingleActionScenarioResolver(Microsoft.Extensions.Options.Options.Create(fixture.Options)),clock??TimeProvider.System,fixture.Completion);return fixture;
        }
        private static string FindRoot(){DirectoryInfo? directory=new(AppContext.BaseDirectory);while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"Parchwell.slnx")))directory=directory.Parent;return directory?.FullName??throw new DirectoryNotFoundException();}
    }
    private sealed class MemorySeed:ISingleActionSeedSource
    {
        internal Dictionary<string,byte[]> Files {get;}=new(StringComparer.Ordinal);
        public Task<byte[]?> ReadAsync(string path,CancellationToken cancellationToken)=>Task.FromResult(Files.GetValueOrDefault(path));
    }
    private sealed class Store:IProducerArtifactStore,IVerifierArtifactStore
    {
        internal Dictionary<string,byte[]> Files {get;}=new(StringComparer.Ordinal);
        internal List<string> Registrations {get;set;}=[];
        internal bool ListingComplete {get;set;}=true;
        internal bool FailWrite {get;set;}
        internal Action? OnList {get;set;}
        public Task<BlobListing> ListAsync(string prefix,CancellationToken cancellationToken){OnList?.Invoke();return Task.FromResult(new BlobListing(Registrations,1,ListingComplete));}
        public Task<byte[]?> ReadAsync(string name,CancellationToken cancellationToken)=>Task.FromResult(Files.GetValueOrDefault(name));
        public Task CreateAsync(string name,byte[] content,CancellationToken cancellationToken){if(FailWrite)throw new IOException("synthetic_write_failure");Files.Add(name,content);return Task.CompletedTask;}
    }
}
