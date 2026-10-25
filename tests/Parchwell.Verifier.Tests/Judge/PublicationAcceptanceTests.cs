using System.Security.Cryptography;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Publication;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

#pragma warning disable CA1707
public sealed class PublicationAcceptanceTests
{
    private const string Run="synthetic-c5";
    private const string Phase="C0";
    private const int Repeat=1;
    private const string Commit="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly string Instance=PhaseIdentity.JobName(Run,Phase,Repeat);
    private static readonly RunIdentity ProducerIdentity=new("A01","C0",Run,Instance,Instance,"producer-client-c5");
    private static readonly RunIdentity StartIdentity=new("A01","C0",Run,Instance,Instance,Instance);

    [Fact]
    public void Given_ExactSingletonPublication_When_ReferencesMatchBytes_Then_AcceptsAttestation()
    {
        var f=Build();
        PublicationReadOutcome result=Evaluate(f);
        Assert.True(result.Accepted);
        Assert.Equal(6,result.AcceptedSnapshot!.Objects.Count);
        Assert.Equal("index.json",result.AcceptedSnapshot.Objects[0].Name.Split('/').Last());
    }

    [Fact]
    public void Given_SameLengthMutationAfterIndexHash_When_Accepting_Then_RejectsWithoutSnapshot()
    {
        var f=Build();
        f.Payloads["ledger.jsonl"]=[(byte)'x'];
        PublicationReadOutcome result=Evaluate(f);
        Assert.False(result.Accepted);
        Assert.Contains("publication_reference_invalid",result.RejectionCodes);
        Assert.Null(result.AcceptedSnapshot);
    }

    [Fact]
    public void Given_IncompleteListing_When_AllExpectedObjectsWereRead_Then_DoesNotAcceptSingleton()
    {
        var f=Build();
        Fixture incomplete=f with { Listing=new(f.Listing.Names,16,false,"listing_incomplete") };
        PublicationReadOutcome result=Evaluate(incomplete);
        Assert.False(result.Accepted);
        Assert.True(result.Incomplete);
        Assert.Contains("listing_incomplete",result.RejectionCodes);
    }

    [Fact]
    public void Given_KnownExtraPublicationAndMissingCompletion_When_Evaluating_Then_InvalidityTakesPrecedence()
    {
        var f=Build();
        string extra=f.Prefix+"publication/extra.bin";
        Fixture changed=f with
        {
            Listing=new(f.Listing.Names.Append(extra).ToArray(),1,true),
            ProducerReceipt=null!
        };
        PublicationReadOutcome result=PublicationAcceptance.Evaluate(changed.Listing,changed.Prefix,
            changed.Payloads.ToDictionary(p=>p.Key,p=>(byte[]?)p.Value,StringComparer.Ordinal),ProducerRegistration(),
            changed.ProducerReceipt,changed.Index,changed.Manifest,changed.StartLine,changed.Inventory,Commit,
            changed.StartReference,changed.InventoryReference,changed.ProducerReference,1024,1024);
        Assert.False(result.Accepted);
        Assert.False(result.Incomplete);
        Assert.Contains("publication_membership_invalid",result.RejectionCodes);
        Assert.Contains("producer_completion_unavailable",result.RejectionCodes);
        Assert.Null(result.AcceptedSnapshot);
    }

    [Theory]
    [InlineData("bookkeeping")]
    [InlineData("worker")]
    [InlineData("publication")]
    public void GivenFalseCompletionFlag_When_Accepting_Then_RejectsPublication(string flag)
    {
        var f=Build();
        ArtifactIndex changed=flag switch
        {
            "bookkeeping"=>f.Index with{BookkeepingComplete=false},
            "worker"=>f.Index with{WorkerComplete=false},
            _=>f.Index with{PublicationComplete=false}
        };
        PublicationReadOutcome result=Evaluate(f with{Index=changed});
        Assert.False(result.Accepted);
        Assert.Contains("publication_completion_invalid",result.RejectionCodes);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("job")]
    [InlineData("pod")]
    [InlineData("commit")]
    public void GivenProducerCompletionReceiptBindingMismatch_When_Accepting_Then_RejectsPublication(string binding)
    {
        var f=Build();
        ProducerCompletionReceipt changed=binding switch
        {
            "identity"=>f.ProducerReceipt with{Identity=ProducerIdentity with{ClientId="foreign-client"}},
            "job"=>f.ProducerReceipt with{JobName="foreign-job"},
            "pod"=>f.ProducerReceipt with{PodName="foreign-pod"},
            _=>f.ProducerReceipt with{SourceCommit=new string('b',40)}
        };
        PublicationReadOutcome result=Evaluate(f with{ProducerReceipt=changed});
        Assert.False(result.Accepted);
        Assert.Contains("producer_completion_invalid",result.RejectionCodes);
    }

    private static Fixture Build()
    {
        string prefix=PhasePaths.ProducerPrefix(Run,Phase,Repeat,Instance);
        var payloads=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(string name in PublicationAcceptance.PublicationNames)payloads[name]=[(byte)name.Length];
        var descriptors=PublicationAcceptance.DescriptorNames.Select(name=>new ArtifactDescriptor(name,payloads[name].LongLength,Hash(payloads[name]))).ToArray();
        var index=new ArtifactIndex(1,ProducerIdentity,"complete",true,true,true,descriptors);
        var manifest=new SealedRunManifest(1,ProducerIdentity,"generator",0,0,[],1,0,"complete",true,ParsedField<long>.Missing(),[],new Dictionary<string,string>(),new Dictionary<string,string>(),[]);
        var producerReceipt=new ProducerCompletionReceipt(1,ProducerIdentity,Phase,Repeat,PhaseIdentity.JobName(Run,Phase,Repeat),"pod-c5","pod-uid-c5",Commit,"loadgen",0,true);
        var expectedStart=new RunIdentity("A01","C0",Run,Instance,Instance,Instance);
        var start=new StartLineNote(1,expectedStart,"document-events",[],"2026-01-01T00:00:00.0000000+00:00",Commit,"managed","native");
        var inventory=new BrokerInventory(1,expectedStart,[],"metadata",Commit);
        var names=PublicationAcceptance.PublicationNames.Select(name=>prefix+"publication/"+name).ToArray();
        ArtifactReference Ref(string container,string name,byte[] bytes)=>new(container,name,bytes.LongLength,Hash(bytes));
        return new(new(names,1,true),prefix,payloads,index,manifest,start,inventory,producerReceipt,
            Ref("verifier",PhasePaths.StartLine(Run,Phase,Repeat),[1]),
            Ref("verifier",PhasePaths.BrokerInventory(Run,Phase,Repeat),[2]),
            Ref("ledger",PhasePaths.ProducerCompletion(Run,Phase,Repeat,Instance),[3]));
    }

    private static PublicationReadOutcome Evaluate(Fixture f)=>PublicationAcceptance.Evaluate(f.Listing,f.Prefix,
        f.Payloads.ToDictionary(p=>p.Key,p=>(byte[]?)p.Value,StringComparer.Ordinal),ProducerRegistration(),f.ProducerReceipt,f.Index,f.Manifest,
        f.StartLine,f.Inventory,Commit,f.StartReference,f.InventoryReference,f.ProducerReference,1024,1024);

    private static ProducerRegistration ProducerRegistration()=>new(1,ProducerIdentity,Phase,Repeat,PhaseIdentity.JobName(Run,Phase,Repeat),"pod-c5","pod-uid-c5",Commit);
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed record Fixture(BlobListing Listing,string Prefix,Dictionary<string,byte[]> Payloads,ArtifactIndex Index,SealedRunManifest Manifest,
        StartLineNote StartLine,BrokerInventory Inventory,ProducerCompletionReceipt ProducerReceipt,ArtifactReference StartReference,
        ArtifactReference InventoryReference,ArtifactReference? ProducerReference);
}
