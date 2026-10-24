using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Serialization;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class VerifierCompletionTests
{
    [Theory]
    [InlineData("start-line", 0)]
    [InlineData("verify", 0)]
    [InlineData("verify", 1)]
    [InlineData("probe-gate", 0)]
    [InlineData("probe-gate", 1)]
    public async Task Given_NormalReturn_When_StopAndDisposalFinish_Then_EmitsOneReceiptWithHandlerCode(string mode, int code)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var cleanup = new ObservedCleanup(output);
        int exit = await Program.RunAsync(VerifierHostTests.ArgumentsFor(mode, "unused"), services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(new Handler(Parse(mode), _ => Task.FromResult(code)));
            services.AddSingleton<IHostedService>(_ => cleanup);
        }, completionOutputForTests: output);
        Assert.Equal(code, exit);
        Assert.True(cleanup.Stopped);
        Assert.True(cleanup.Disposed);
        if (mode != "probe-gate")
        {
            Assert.Equal("", output.ToString());
            return;
        }
        string line = Assert.Single(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.StartsWith("PARCHWELL_COMPLETION ", line, StringComparison.Ordinal);
        using JsonDocument json = JsonDocument.Parse(line["PARCHWELL_COMPLETION ".Length..]);
        Assert.Equal(1, json.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal(mode, json.RootElement.GetProperty("mode").GetString());
        Assert.Equal(code, json.RootElement.GetProperty("handler_exit_code").GetInt32());
        Assert.True(json.RootElement.GetProperty("cleanup_completed").GetBoolean());
        Assert.Equal("test-commit", json.RootElement.GetProperty("source_commit").GetString());
        JsonElement identity = json.RootElement.GetProperty("identity");
        Assert.Equal("A01", identity.GetProperty("scenario_id").GetString());
        Assert.Equal(mode == "probe-gate" ? 1 : 6, identity.EnumerateObject().Count());
        if (mode != "probe-gate") Assert.Equal("run-1", identity.GetProperty("run_id").GetString());
    }

    [Theory]
    [InlineData("handler")]
    [InlineData("cancel")]
    [InlineData("stop")]
    [InlineData("dispose")]
    [InlineData("startup")]
    public async Task Given_ExecutionOrCleanupFailure_When_ProcessEnds_Then_NoReceipt(string failure)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        int exit = await Program.RunAsync(VerifierHostTests.ArgumentsFor("probe-gate", "unused"), services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(provider => new Handler(VerifierMode.ProbeGate, _ =>
            {
                if (failure == "handler") throw new IOException("synthetic handler failure");
                if (failure == "cancel") provider.GetRequiredService<IHostApplicationLifetime>().StopApplication();
                return Task.FromResult(1);
            }));
            services.AddSingleton<IHostedService>(_ => new FailingCleanup(failure));
        }, completionOutputForTests: output);
        Assert.Equal(1, exit);
        Assert.Equal("", output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ReceiptWriteOrFlushFailure_When_ProcessEnds_Then_ReturnsOne(bool flush)
    {
        using var writer = new BrokenWriter(flush);
        int exit = await Program.RunAsync(VerifierHostTests.ArgumentsFor("probe-gate", "unused"), services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(new Handler(VerifierMode.ProbeGate, _ => Task.FromResult(0)));
        }, completionOutputForTests: writer);
        Assert.Equal(1, exit);
    }

    [Fact]
    public async Task GivenJudgeCompletes_WhenHostStopsAndDisposes_ThenPublishesBlobReceiptAsFinalAction()
    {
        var events=new List<string>();
        var publisher=new RecordingPublisher(events);
        int exit=await Program.RunAsync(JudgeArguments(),services=>
        {
            services.RemoveAll<IVerifierArtifactStore>();services.AddSingleton<IVerifierArtifactStore>(new EmptyVerifierStore(events));
            services.RemoveAll<IProducerArtifactStore>();services.AddSingleton<IProducerArtifactStore>(new EmptyProducerStore());
            services.RemoveAll<ICompletionReceiptPublisher>();services.AddSingleton<ICompletionReceiptPublisher>(publisher);
            services.AddSingleton<IHostedService>(_=>new OrderedCleanup(events));
        });
        Assert.Equal(1,exit);
        Assert.Equal(["candidate","stop","dispose","receipt"],events);
        Assert.Equal(1,publisher.Calls);
        Assert.Equal("synthetic-c5/C0/judge-completion.json",publisher.Name);
        Assert.Equal(1,PhaseWireCodec.ReadReceipt(publisher.Bytes!).SchemaVersion);
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("dispose")]
    [InlineData("receipt")]
    public async Task GivenJudgeCleanupOrFinalReceiptFailure_WhenProcessEnds_ThenSuppressesOrFailsFinalPublication(string failure)
    {
        var events=new List<string>();
        var publisher=new RecordingPublisher(events,failure=="receipt");
        int exit=await Program.RunAsync(JudgeArguments(),services=>
        {
            services.RemoveAll<IVerifierArtifactStore>();services.AddSingleton<IVerifierArtifactStore>(new EmptyVerifierStore(events));
            services.RemoveAll<IProducerArtifactStore>();services.AddSingleton<IProducerArtifactStore>(new EmptyProducerStore());
            services.RemoveAll<ICompletionReceiptPublisher>();services.AddSingleton<ICompletionReceiptPublisher>(publisher);
            services.AddSingleton<IHostedService>(_=>new OrderedCleanup(events,failure));
        });
        Assert.Equal(1,exit);
        Assert.Equal(failure=="receipt"?1:0,publisher.Calls);
    }

    private static string[] JudgeArguments()=>[
        "--PARCHWELL_SOURCE_COMMIT="+new string('a',40),"--PARCHWELL_POD_NAME=judge-pod",
        "--Verifier:Mode=judge","--Verifier:ScenarioId=A01","--Verifier:VerifierCommit="+new string('a',40),
        "--Verifier:ConfigurationId=C0","--Verifier:RunId=synthetic-c5","--Verifier:Phase=C0","--Verifier:JobName=synthetic-c5-c0-judge",
        "--Verifier:CandidateOutputName=synthetic-c5/C0/candidate.json","--Verifier:ProcessLifetime=00:00:10","--Verifier:CleanupTimeout=00:00:01",
        "--Verifier:IoTimeout=00:00:01","--Verifier:MaxOutputBytes=16777216","--Verifier:MaxDiagnosticsPerCheck=16","--Verifier:MaxDiagnosticBytes=65536",
        "--Verifier:LedgerContainer=ledger","--Verifier:VerifierContainer=verifier","--Verifier:BlobServiceUri=https://storage.example/",
        "--Verifier:TenantId=tenant-id","--Verifier:WorkloadIdentityClientId=workload-client","--Verifier:FederatedTokenFilePath=/tmp/token",
        "--Verifier:LoadGenImage=loadgen:tag@sha256:"+new string('b',64),"--Verifier:VerifierImage=verifier:tag@sha256:"+new string('c',64),
        "--Verifier:MaxArtifactBytes=16777216","--Verifier:MaxTotalInputBytes=83886080",
        "--Verifier:ScenarioLoadOptions:MaxDocumentCharacters=16384","--Verifier:ScenarioLoadOptions:MaxNestingDepth=32",
        "--Verifier:ScenarioLoadOptions:MaxScalarCharacters=2048","--Verifier:ScenarioLoadOptions:MaxParserEvents=2000",
        "--Telemetry:ConnectionString=InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/",
        "--Telemetry:ServiceName=Parchwell.Verifier","--Telemetry:ServiceInstanceId=judge-pod","--Telemetry:ScenarioId=A01","--Telemetry:RunId=synthetic-c5"
    ];

    private sealed class EmptyVerifierStore(List<string> events):IVerifierArtifactStore
    {
        public Task<byte[]?> ReadAsync(string name,CancellationToken token)=>Task.FromResult<byte[]?>(null);
        public Task<BlobListing> ListAsync(string prefix,CancellationToken token)=>Task.FromResult(new BlobListing([],0,true));
        public Task CreateAsync(string name,byte[] content,CancellationToken token){events.Add("candidate");return Task.CompletedTask;}
    }
    private sealed class EmptyProducerStore:IProducerArtifactStore
    {
        public Task<BlobListing> ListAsync(string prefix,CancellationToken token)=>Task.FromResult(new BlobListing([],0,true));
        public Task<byte[]?> ReadAsync(string name,CancellationToken token)=>Task.FromResult<byte[]?>(null);
    }
    private sealed class RecordingPublisher(List<string> events,bool fail=false):ICompletionReceiptPublisher
    {
        public int Calls{get;private set;}public string? Name{get;private set;}public byte[]? Bytes{get;private set;}
        public Task PublishAsync(VerifierHostOptions options,string container,string name,byte[] bytes,CancellationToken token)
        {
            Calls++;Name=name;Bytes=bytes;events.Add("receipt");return fail?Task.FromException(new IOException("synthetic uncertain receipt upload")):Task.CompletedTask;
        }
    }
    private sealed class OrderedCleanup(List<string> events,string? fail=null):IHostedService,IDisposable
    {
        public Task StartAsync(CancellationToken token)=>Task.CompletedTask;
        public Task StopAsync(CancellationToken token){events.Add("stop");return fail=="stop"?Task.FromException(new IOException("synthetic stop failure")):Task.CompletedTask;}
        public void Dispose(){events.Add("dispose");if(fail=="dispose")throw new IOException("synthetic dispose failure");}
    }

    private static VerifierMode Parse(string mode) => mode switch
    {
        "start-line" => VerifierMode.StartLine, "verify" => VerifierMode.Verify, _ => VerifierMode.ProbeGate
    };
    private sealed class Handler(VerifierMode mode, Func<CancellationToken, Task<int>> execute) : IVerifierModeHandler
    {
        public VerifierMode Mode => mode;
        public Task<int> ExecuteAsync(CancellationToken cancellationToken) => execute(cancellationToken);
    }
    private sealed class ObservedCleanup(StringWriter output) : IHostedService, IDisposable
    {
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken)
        {
            Assert.Equal("", output.ToString());
            Stopped = true;
            return Task.CompletedTask;
        }
        public void Dispose()
        {
            Assert.Equal("", output.ToString());
            Disposed = true;
        }
    }
    private sealed class FailingCleanup(string failure) : IHostedService, IDisposable
    {
        public Task StartAsync(CancellationToken cancellationToken) => failure == "startup"
            ? Task.FromException(new IOException("synthetic start failure")) : Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => failure == "stop"
            ? Task.FromException(new IOException("synthetic stop failure")) : Task.CompletedTask;
        public void Dispose() { if (failure == "dispose") throw new IOException("synthetic disposal failure"); }
    }
    private sealed class BrokenWriter(bool flush) : StringWriter(CultureInfo.InvariantCulture)
    {
        public override Task WriteLineAsync(string? value) => flush ? base.WriteLineAsync(value)
            : Task.FromException(new IOException("synthetic write failure"));
        public override Task FlushAsync() => Task.FromException(new IOException("synthetic flush failure"));
    }
}
#pragma warning restore CA1707
