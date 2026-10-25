using System.Text.Json;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.Judge;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

#pragma warning disable CA1707
public sealed class JudgeHandlerTests
{
    [Fact]
    public async Task GivenFiveCompletedRepeats_WhenJudged_ThenPublishesCompleteCandidateAndFinalReceipt()
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var completion=new VerifierCompletionState();
        var options=Options();
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(options),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System,completion);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0,exit);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        Assert.Equal("complete_valid",candidate.RootElement.GetProperty("status").GetString());
        Assert.Equal(5,candidate.RootElement.GetProperty("repeats").GetArrayLength());
        Assert.Equal(5,candidate.RootElement.GetProperty("counts").GetProperty("ok").GetInt64());
        completion.MarkReturned(options,exit,CancellationToken.None,CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out string container,out string name,out byte[] receipt));
        Assert.Equal("verifier",container);Assert.Equal("synthetic-c5/C0/judge-completion.json",name);
        Assert.Equal(0,PhaseWireCodec.ReadReceipt(receipt).HandlerExitCode);
    }

    [Fact]
    public async Task GivenFiveInvalidButUsableProbeRepeats_WhenJudged_ThenGatePassesAndCountsStayAbsent()
    {
        PhaseStores stores=await BuildFiveRepeats("probe",15);
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var options=Options();options.Phase="probe";options.ConfigurationId="C0";options.JobName="synthetic-c5-probe-judge";options.CandidateOutputName="synthetic-c5/probe/candidate.json";
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(options),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1,exit);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        Assert.Equal("invalid",candidate.RootElement.GetProperty("status").GetString());
        Assert.Equal("pass",candidate.RootElement.GetProperty("gate").GetString());
        Assert.False(candidate.RootElement.TryGetProperty("counts",out _));
    }

    [Fact]
    public async Task GivenActualRepeatCountsOverflow_WhenJudged_ThenOmitsPhaseTotals()
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        ReplaceResult(stores,"C0",1,"ok",long.MaxValue);
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1,exit);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        Assert.Equal("incomplete",candidate.RootElement.GetProperty("status").GetString());
        Assert.False(candidate.RootElement.TryGetProperty("counts",out _));
        Assert.Contains(candidate.RootElement.GetProperty("reasons").EnumerateArray(),reason=>reason.GetString()=="j7_counts_unavailable");
    }

    [Theory]
    [InlineData("source")]
    [InlineData("identity")]
    [InlineData("exit-status")]
    public async Task GivenResultDoesNotBindToVerifierReceipt_WhenJudged_ThenDoesNotTrustItsStatus(string mismatch)
    {
        PhaseStores stores = await BuildFiveRepeats("C0", null);
        string resultName = PhasePaths.Result("synthetic-c5", "C0", 1);
        JsonObject result = JsonNode.Parse(stores.Verifier[resultName])!.AsObject();
        string receiptName = PhasePaths.VerifyCompletion("synthetic-c5", "C0", 1);
        JsonObject receipt = JsonNode.Parse(stores.Verifier[receiptName])!.AsObject();
        if (mismatch == "source") result["verifier_commit"] = new string('b', 40);
        if (mismatch == "identity") result["identity"]!["instance_id"] = "foreign-instance";
        if (mismatch == "exit-status")
        {
            result["status"] = "invalid";
            receipt["handler_exit_code"] = 0;
        }
        byte[] resultBytes = Encoding.UTF8.GetBytes(result.ToJsonString());
        stores.Verifier[resultName] = resultBytes;
        UpdateReference(receipt["artifacts"]![0]!.AsObject(), resultName, resultBytes);
        stores.Verifier[receiptName] = Encoding.UTF8.GetBytes(receipt.ToJsonString());
        var verifier = new MemoryVerifierStore(stores.Verifier);
        var handler = new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()), verifier,
            new MemoryProducerStore(stores.Ledger), TimeProvider.System);

        int exit = await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, exit);
        using JsonDocument candidate = JsonDocument.Parse(verifier.Created!);
        JsonElement repeat = candidate.RootElement.GetProperty("repeats")[0];
        Assert.Equal("incomplete", repeat.GetProperty("status").GetString());
        Assert.Contains("j3_verifier_receipt", repeat.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()));
        Assert.DoesNotContain("trusted_result_invalid", repeat.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("slot-job")]
    public async Task GivenKnownStartLineBindingMismatch_WhenJudged_ThenJ4FailsAndRepeatIsInvalid(string mismatch)
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        string name=PhasePaths.StartLineCompletion("synthetic-c5","C0",1);
        JsonObject receipt=JsonNode.Parse(stores.Verifier[name])!.AsObject();
        if(mismatch=="source") receipt["source_commit"]=new string('b',40);
        if(mismatch=="slot-job")
        {
            receipt["repeat"]=2;
            receipt["job_name"]="synthetic-c5-c0-r2-start-line";
            receipt["identity"]!["instance_id"]="synthetic-c5-c0-r2-loadgen";
            receipt["identity"]!["expected_instance_id"]="synthetic-c5-c0-r2-loadgen";
            receipt["identity"]!["client_id"]="synthetic-c5-c0-r2-loadgen";
            receipt["artifacts"]![0]!["name"]=PhasePaths.StartLine("synthetic-c5","C0",2);
            receipt["artifacts"]![1]!["name"]=PhasePaths.BrokerInventory("synthetic-c5","C0",2);
        }
        stores.Verifier[name]=Encoding.UTF8.GetBytes(receipt.ToJsonString());
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,
            new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1,exit);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        JsonElement repeat=candidate.RootElement.GetProperty("repeats")[0];
        Assert.Equal("invalid",repeat.GetProperty("status").GetString());
        Assert.False(repeat.GetProperty("checks").GetProperty("j4_producer_job_binding").GetBoolean());
    }

    [Fact]
    public async Task GivenMalformedInventoryWithMatchingReceipts_WhenJudged_ThenRejectsStartEvidence()
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        const int repeat=5;
        string inventoryName=PhasePaths.BrokerInventory("synthetic-c5","C0",repeat);
        byte[] malformed=Encoding.UTF8.GetBytes("{}");
        stores.Verifier[inventoryName]=malformed;
        string startReceiptName=PhasePaths.StartLineCompletion("synthetic-c5","C0",repeat);
        JsonObject start=JsonNode.Parse(stores.Verifier[startReceiptName])!.AsObject();
        UpdateReference(start["artifacts"]![1]!.AsObject(),inventoryName,malformed);
        stores.Verifier[startReceiptName]=Encoding.UTF8.GetBytes(start.ToJsonString());
        string verifyReceiptName=PhasePaths.VerifyCompletion("synthetic-c5","C0",repeat);
        JsonObject verify=JsonNode.Parse(stores.Verifier[verifyReceiptName])!.AsObject();
        UpdateReference(verify["publication_acceptance"]!["broker_inventory"]!.AsObject(),inventoryName,malformed);
        stores.Verifier[verifyReceiptName]=Encoding.UTF8.GetBytes(verify.ToJsonString());
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1,exit);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        Assert.Equal("incomplete",candidate.RootElement.GetProperty("status").GetString());
        Assert.False(candidate.RootElement.TryGetProperty("counts",out _));
    }

    [Fact]
    public async Task GivenRegistrationWithInvalidUnicode_WhenJudged_ThenClassifiesItAsInvalidRegistration()
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        string name=PhasePaths.Registration("synthetic-c5","C0",5,"pod-c5");
        string json=Encoding.UTF8.GetString(stores.Ledger[name]).Replace("\"pod_name\":\"pod-c5\"", "\"pod_name\":\"\\uD800\"", StringComparison.Ordinal);
        stores.Ledger[name]=Encoding.UTF8.GetBytes(json);
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1,exit);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        Assert.Equal("invalid",candidate.RootElement.GetProperty("status").GetString());
        JsonElement repeat=candidate.RootElement.GetProperty("repeats")[4];
        Assert.Equal("invalid",repeat.GetProperty("status").GetString());
        Assert.False(repeat.GetProperty("checks").GetProperty("j5_registered_producer_count").GetBoolean());
    }

    [Theory]
    [InlineData("second-registration")]
    [InlineData("wrong-job")]
    [InlineData("incomplete-listing")]
    [InlineData("unavailable-producer-listing")]
    [InlineData("missing-registration")]
    [InlineData("malformed-registration-phase")]
    [InlineData("missing-start-first")]
    [InlineData("missing-start-last")]
    [InlineData("missing-loadgen-first")]
    [InlineData("missing-loadgen-last")]
    [InlineData("missing-verify-first")]
    [InlineData("missing-verify-last")]
    [InlineData("inventory-coverage")]
    public async Task GivenCurrentJudgeEvidenceInvalidation_WhenJudged_ThenProbeIsInconclusiveAndTotalsAreAbsent(string mutation)
    {
        PhaseStores stores=await BuildFiveRepeats("probe",null);
        if(mutation=="second-registration")
        {
            string prefix=PhasePaths.ProducersPrefix("synthetic-c5","probe",5);
            RunIdentity identity=new("A01","C0","synthetic-c5","extra-pod","extra-pod","extra-client");
            stores.Ledger[PhasePaths.Registration("synthetic-c5","probe",5,"extra-pod")]=PhaseWireCodec.WriteRegistration(
                new(1,identity,"probe",5,PhaseIdentity.JobName("synthetic-c5","probe",5),"extra-pod","extra-uid",new string('a',40)));
            Assert.True(stores.Ledger.ContainsKey(prefix+"extra-pod/registration.json"));
        }
        if(mutation=="wrong-job")
        {
            string name=PhasePaths.Registration("synthetic-c5","probe",5,"pod-c5");
            using JsonDocument original=JsonDocument.Parse(stores.Ledger[name]);
            JsonObject value=JsonNode.Parse(stores.Ledger[name])!.AsObject();
            value["job_name"]="wrong-job";
            stores.Ledger[name]=Encoding.UTF8.GetBytes(value.ToJsonString());
        }
        if(mutation=="malformed-registration-phase")
        {
            string name=PhasePaths.Registration("synthetic-c5","probe",5,"pod-c5");
            JsonObject value=JsonNode.Parse(stores.Ledger[name])!.AsObject();
            value["phase"]="foreign";
            stores.Ledger[name]=Encoding.UTF8.GetBytes(value.ToJsonString());
        }
        if(mutation=="inventory-coverage")
        {
            string name=PhasePaths.VerifyCompletion("synthetic-c5","probe",5);
            JsonObject value=JsonNode.Parse(stores.Verifier[name])!.AsObject();
            value["repeat_checks"]!["broker_inventory_coverage"]="fail";
            stores.Verifier[name]=Encoding.UTF8.GetBytes(value.ToJsonString());
        }
        if(mutation=="missing-registration")
            stores.Ledger.Remove(PhasePaths.Registration("synthetic-c5","probe",5,"pod-c5"));
        int boundaryRepeat=mutation.EndsWith("first",StringComparison.Ordinal)?1:5;
        if(mutation.StartsWith("missing-start-",StringComparison.Ordinal))
            stores.Verifier.Remove(PhasePaths.StartLineCompletion("synthetic-c5","probe",boundaryRepeat));
        if(mutation.StartsWith("missing-loadgen-",StringComparison.Ordinal))
            stores.Ledger.Remove(PhasePaths.ProducerCompletion("synthetic-c5","probe",boundaryRepeat,"pod-c5"));
        if(mutation.StartsWith("missing-verify-",StringComparison.Ordinal))
            stores.Verifier.Remove(PhasePaths.VerifyCompletion("synthetic-c5","probe",boundaryRepeat));
        var verifier=new MemoryVerifierStore(stores.Verifier){IncompletePrefix=mutation=="incomplete-listing"?"synthetic-c5/probe/":null};
        var producer=new MemoryProducerStore(stores.Ledger){IncompletePrefix=mutation=="unavailable-producer-listing"?PhasePaths.ProducersPrefix("synthetic-c5","probe",5):null};
        var options=Options();options.Phase="probe";options.ConfigurationId="C0";options.JobName="synthetic-c5-probe-judge";options.CandidateOutputName="synthetic-c5/probe/candidate.json";
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(options),verifier,producer,TimeProvider.System);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1,exit);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        Assert.Equal("inconclusive",candidate.RootElement.GetProperty("gate").GetString());
        if(mutation is "unavailable-producer-listing" or "missing-registration" || mutation.StartsWith("missing-",StringComparison.Ordinal))
        {
            Assert.Equal("incomplete",candidate.RootElement.GetProperty("status").GetString());
            int slot=mutation.EndsWith("first",StringComparison.Ordinal)?0:4;
            Assert.Equal("incomplete",candidate.RootElement.GetProperty("repeats")[slot]!.GetProperty("status").GetString());
        }
        Assert.False(candidate.RootElement.TryGetProperty("counts",out _));
    }

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

    private static async Task<PhaseStores> BuildFiveRepeats(string phase,int? invalidCheck)
    {
        var verifierObjects=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        var ledgerObjects=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        for(int repeat=1;repeat<=5;repeat++)
        {
            using var fixture=new HostEvidenceBuilder();
            fixture.ConfigureRepeat(repeat,phase);
            long throttle=phase=="probe"?20_001:0;
            fixture.SetThrottleWindow(throttle);
            if(invalidCheck==15)
            {
                string[] rows=Encoding.UTF8.GetString(fixture.Inputs["statistics.jsonl"]!).TrimEnd('\n').Split('\n');
                for(int i=0;i<rows.Length;i++)
                {
                    JsonObject envelope=JsonNode.Parse(rows[i])!.AsObject();
                    JsonObject native=JsonNode.Parse(envelope["raw_json"]!.GetValue<string>())!.AsObject();
                    native["brokers"]!["sasl_ssl://broker-a:9093/1"]!["req_timeouts"]=1;
                    envelope["raw_json"]=native.ToJsonString();rows[i]=envelope.ToJsonString();
                }
                fixture.Inputs["statistics.jsonl"]=Encoding.UTF8.GetBytes(string.Join('\n',rows)+"\n");
                fixture.RebuildPublicationReferences();
            }
            var completion=new VerifierCompletionState();
            int exit=await fixture.Verify(completion).ExecuteAsync(TestContext.Current.CancellationToken);
            Assert.Equal(invalidCheck is null?0:1,exit);
            completion.MarkReturned(fixture.Options,exit,CancellationToken.None,CancellationToken.None);
            Assert.True(completion.TryGetBlobReceipt(out _,out string receiptName,out byte[] receiptBytes));
            await fixture.CreateAsync(receiptName,receiptBytes,CancellationToken.None);
            foreach(var item in fixture.VerifierObjects)verifierObjects[item.Key]=item.Value;
            foreach(var item in fixture.LedgerObjects)ledgerObjects[item.Key]=item.Value;
        }
        return new(verifierObjects,ledgerObjects);
    }

    private static void ReplaceResult(PhaseStores stores,string phase,int repeat,string count,long value)
    {
        string resultName=PhasePaths.Result("synthetic-c5",phase,repeat);
        JsonObject result=JsonNode.Parse(stores.Verifier[resultName])!.AsObject();result[count]=value;
        byte[] bytes=Encoding.UTF8.GetBytes(result.ToJsonString());stores.Verifier[resultName]=bytes;
        string receiptName=PhasePaths.VerifyCompletion("synthetic-c5",phase,repeat);
        JsonObject receipt=JsonNode.Parse(stores.Verifier[receiptName])!.AsObject();
        receipt["artifacts"]![0]!["length"]=bytes.LongLength;
        receipt["artifacts"]![0]!["sha256"]=Convert.ToHexStringLower(SHA256.HashData(bytes));
        stores.Verifier[receiptName]=Encoding.UTF8.GetBytes(receipt.ToJsonString());
    }

    private static void UpdateReference(JsonObject reference,string name,byte[] bytes)
    {
        reference["name"]=name;
        reference["length"]=bytes.LongLength;
        reference["sha256"]=Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private sealed record PhaseStores(Dictionary<string,byte[]> Verifier,Dictionary<string,byte[]> Ledger);

    private sealed class MemoryVerifierStore(Dictionary<string,byte[]> objects):IVerifierArtifactStore
    {
        public string? CreatedName{get;private set;}public byte[]? Created{get;private set;}public string? IncompletePrefix{get;init;}
        public Task<byte[]?> ReadAsync(string name,CancellationToken cancellationToken)=>Task.FromResult(objects.TryGetValue(name,out var value)?(byte[]?)value:null);
        public Task<BlobListing> ListAsync(string prefix,CancellationToken cancellationToken)
        {
            string[] names=objects.Keys.Where(n=>n.StartsWith(prefix,StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
            return Task.FromResult(new BlobListing(names,1,IncompletePrefix!=prefix,IncompletePrefix==prefix?"listing_incomplete":null));
        }
        public Task CreateAsync(string name,byte[] content,CancellationToken cancellationToken){CreatedName=name;Created=content;objects[name]=content;return Task.CompletedTask;}
    }

    private sealed class MemoryProducerStore(Dictionary<string,byte[]> objects):IProducerArtifactStore
    {
        public string? IncompletePrefix{get;init;}
        public Task<BlobListing> ListAsync(string prefix,CancellationToken cancellationToken)=>Task.FromResult(new BlobListing(
            objects.Keys.Where(n=>n.StartsWith(prefix,StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray(),1,IncompletePrefix!=prefix,IncompletePrefix==prefix?"listing_unavailable":null));
        public Task<byte[]?> ReadAsync(string name,CancellationToken cancellationToken)=>Task.FromResult(objects.TryGetValue(name,out var value)?(byte[]?)value:null);
    }

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
