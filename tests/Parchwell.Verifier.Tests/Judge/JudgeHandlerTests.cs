using System.Text.Json;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Judge;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

#pragma warning disable CA1707
public sealed class JudgeHandlerTests
{
    [Fact]
    public async Task GivenNoRepeatEvidence_WhenJudged_ThenWritesOneIncompleteFiveSlotCandidateAndReceipt()
    {
        VerifierHostOptions options=Options();
        var verifier=new VerifierStore();
        var completion=new VerifierCompletionState();
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(options),verifier,new ProducerStore(),TimeProvider.System,completion);
        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1,exit);
        Assert.Equal("synthetic-c5/C0/candidate.json",verifier.CreatedName);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        Assert.Equal("incomplete",candidate.RootElement.GetProperty("status").GetString());
        Assert.Equal(5,candidate.RootElement.GetProperty("repeats").GetArrayLength());
        completion.MarkReturned(options,exit,CancellationToken.None,CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out string container,out string name,out byte[] receiptBytes));
        Assert.Equal("verifier",container);
        Assert.Equal("synthetic-c5/C0/judge-completion.json",name);
        using JsonDocument receipt=JsonDocument.Parse(receiptBytes);
        Assert.Equal("synthetic-c5/C0/candidate.json",receipt.RootElement.GetProperty("artifacts")[0].GetProperty("name").GetString());
    }

    private static VerifierHostOptions Options()=>new()
    {
        Mode="judge",ScenarioId="A01",ConfigurationId="C0",RunId="synthetic-c5",Phase="C0",SourceCommit=new string('a',40),VerifierCommit=new string('a',40),
        JobName="synthetic-c5-c0-judge",PodName="judge-pod",VerifierContainer="verifier",LedgerContainer="ledger",
        CandidateOutputName="synthetic-c5/C0/candidate.json",IoTimeout=TimeSpan.FromSeconds(1),MaxDiagnosticsPerCheck=16,MaxOutputBytes=16_777_216,
        LoadGenImage="loadgen:tag@sha256:"+new string('b',64),VerifierImage="verifier:tag@sha256:"+new string('c',64)
    };

    private sealed class VerifierStore:IVerifierArtifactStore
    {
        public string? CreatedName{get;private set;}
        public byte[]? Created{get;private set;}
        public Task<byte[]?> ReadAsync(string name,CancellationToken cancellationToken)=>Task.FromResult<byte[]?>(null);
        public Task<BlobListing> ListAsync(string prefix,CancellationToken cancellationToken)=>Task.FromResult(new BlobListing([],0,true));
        public Task CreateAsync(string name,byte[] content,CancellationToken cancellationToken){CreatedName=name;Created=content;return Task.CompletedTask;}
    }
    private sealed class ProducerStore:IProducerArtifactStore
    {
        public Task<BlobListing> ListAsync(string prefix,CancellationToken cancellationToken)=>Task.FromResult(new BlobListing([],0,true));
        public Task<byte[]?> ReadAsync(string name,CancellationToken cancellationToken)=>Task.FromResult<byte[]?>(null);
    }
}
#pragma warning restore CA1707
