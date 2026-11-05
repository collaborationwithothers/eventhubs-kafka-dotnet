using System.Text.Json;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.Judge;
using Xunit;

namespace Parchwell.Verifier.Tests.Judge;

#pragma warning disable CA1707
public sealed class JudgeHandlerTests
{
    // Production-writer metadata must survive verification and phase aggregation without normalization.
    [Theory]
    [InlineData("2.16.0")]
    [InlineData("2.16.0+worker-build")]
    public async Task Given_WorkerRuntimeVersions_When_Judged_Then_PreservesExactProducerVersion(string kafkaVersion)
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null,(fixture,_)=>SetWorkerRuntimeVersions(fixture,kafkaVersion));
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,
            new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0,exit);
        for(int repeat=1;repeat<=5;repeat++)
        {
            using JsonDocument result=JsonDocument.Parse(stores.Verifier[PhasePaths.Result("synthetic-c5","C0",repeat)]);
            JsonElement runtimeVersions=result.RootElement.GetProperty("runtime_versions");
            Assert.Equal(kafkaVersion,runtimeVersions.GetProperty("confluent_kafka").GetString());
            Assert.Equal("2.16.0",runtimeVersions.GetProperty("librdkafka").GetString());
            Assert.Equal("10.0.13",runtimeVersions.GetProperty("dotnet").GetString());
            Assert.False(runtimeVersions.TryGetProperty("Confluent.Kafka",out _));
        }
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        JsonElement root=candidate.RootElement;
        Assert.Equal("complete_valid",root.GetProperty("status").GetString());
        Assert.Equal(5,root.GetProperty("repeats").GetArrayLength());
        Assert.Equal(5,root.GetProperty("counts").GetProperty("ok").GetInt64());
        JsonElement versions=root.GetProperty("versions");
        Assert.Equal(kafkaVersion,versions.GetProperty("confluent_kafka").GetString());
        Assert.Equal("2.16.0",versions.GetProperty("librdkafka").GetString());
        Assert.Equal("10.0.13",versions.GetProperty("dotnet").GetString());
        Assert.Equal("synthetic-producer",versions.GetProperty("workload_generator").GetString());
        Assert.DoesNotContain("version_unavailable",root.GetProperty("reasons").EnumerateArray().Select(reason=>reason.GetString()));
    }

    // Missing or inconsistent metadata remains diagnostic while the message outcomes remain valid.
    [Theory]
    [InlineData("missing",1)]
    [InlineData("missing",5)]
    [InlineData("disagreeing",1)]
    [InlineData("disagreeing",5)]
    public async Task Given_MissingOrDisagreeingWorkerKafkaVersion_When_Judged_Then_PreservesUnavailableMetadata(string condition,int selectedRepeat)
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null,(fixture,repeat)=>SetWorkerRuntimeVersions(fixture,
            repeat==selectedRepeat?(condition=="missing"?null:"2.15.0"):"2.16.0"));
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,
            new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0,exit);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        JsonElement root=candidate.RootElement;
        Assert.Equal("complete_valid",root.GetProperty("status").GetString());
        Assert.Equal(5,root.GetProperty("counts").GetProperty("ok").GetInt64());
        JsonElement versions=root.GetProperty("versions");
        Assert.Equal(JsonValueKind.Null,versions.GetProperty("confluent_kafka").ValueKind);
        Assert.Equal("2.16.0",versions.GetProperty("librdkafka").GetString());
        Assert.Equal("10.0.13",versions.GetProperty("dotnet").GetString());
        Assert.Equal("synthetic-producer",versions.GetProperty("workload_generator").GetString());
        Assert.Contains("version_unavailable",root.GetProperty("reasons").EnumerateArray().Select(reason=>reason.GetString()));
    }

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
        for(int repeat=1;repeat<=5;repeat++)
        {
            using JsonDocument result=JsonDocument.Parse(stores.Verifier[PhasePaths.Result("synthetic-c5","C0",repeat)]);
            AssertBrokerCountEqual(result.RootElement.GetProperty("broker_count"),candidate.RootElement.GetProperty("repeats")[repeat-1].GetProperty("broker_count"));
        }
        completion.MarkReturned(options,exit,CancellationToken.None,CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out string container,out string name,out byte[] receipt));
        Assert.Equal("verifier",container);Assert.Equal("synthetic-c5/C0/judge-completion.json",name);
        Assert.Equal(0,PhaseWireCodec.ReadReceipt(receipt).HandlerExitCode);
    }

    [Fact]
    public async Task Given_B04OptionsWithA01RepeatEvidence_When_Judged_Then_RejectsCrossScenarioEvidence()
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var options=Options();options.ScenarioId="B04";
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(options),verifier,
            new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1,exit);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        JsonElement root=candidate.RootElement;
        Assert.Equal("B04",root.GetProperty("scenario_id").GetString());
        Assert.Equal("invalid",root.GetProperty("status").GetString());
        Assert.False(root.TryGetProperty("counts",out _));
        foreach(JsonElement repeat in root.GetProperty("repeats").EnumerateArray())
        {
            Assert.False(repeat.GetProperty("checks").GetProperty("j4_producer_job_binding").GetBoolean());
            Assert.False(repeat.TryGetProperty("counts",out _));
            Assert.False(repeat.TryGetProperty("broker_count",out _));
            Assert.False(repeat.TryGetProperty("b04_recorded_evidence",out _));
        }
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
        foreach(JsonElement repeat in candidate.RootElement.GetProperty("repeats").EnumerateArray())
        {
            JsonElement count=repeat.GetProperty("broker_count");
            Assert.Equal("LoadGenProducer",count.GetProperty("role").GetString());
            Assert.Equal(1,count.GetProperty("maximum").GetInt64());
            Assert.Equal(1,count.GetProperty("first_post_drain").GetInt64());
            Assert.True(count.GetProperty("lower_bound").GetBoolean());
        }
    }

    [Fact]
    public async Task GivenBoundDistinctBrokerMeasurements_WhenJudged_ThenRetainsEachMeasurementInItsOwnRepeat()
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        for(int repeat=1;repeat<=5;repeat++)
        {
            string resultName=PhasePaths.Result("synthetic-c5","C0",repeat);
            JsonObject result=JsonNode.Parse(stores.Verifier[resultName])!.AsObject();
            result["broker_count"]!["maximum"]=(long)(repeat+1);
            result["broker_count"]!["first_post_drain"]=(long)repeat;
            byte[] bytes=Encoding.UTF8.GetBytes(result.ToJsonString());
            stores.Verifier[resultName]=bytes;
            string receiptName=PhasePaths.VerifyCompletion("synthetic-c5","C0",repeat);
            JsonObject receipt=JsonNode.Parse(stores.Verifier[receiptName])!.AsObject();
            UpdateReference(receipt["artifacts"]![0]!.AsObject(),resultName,bytes);
            stores.Verifier[receiptName]=Encoding.UTF8.GetBytes(receipt.ToJsonString());
        }
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        Assert.Equal(0,await handler.ExecuteAsync(TestContext.Current.CancellationToken));

        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        for(int repeat=1;repeat<=5;repeat++)
        {
            JsonElement count=candidate.RootElement.GetProperty("repeats")[repeat-1].GetProperty("broker_count");
            Assert.Equal("LoadGenProducer",count.GetProperty("role").GetString());
            Assert.Equal("pod-c5",count.GetProperty("client_id").GetString());
            Assert.Equal(repeat+1,count.GetProperty("maximum").GetInt64());
            Assert.Equal(repeat,count.GetProperty("first_post_drain").GetInt64());
            Assert.True(count.GetProperty("lower_bound").GetBoolean());
        }
    }

    [Fact]
    public async Task GivenFiveAcceptedB04EvidenceResults_WhenJudged_ThenProjectsEachIntoItsOwnRepeat()
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        for(int repeat=1;repeat<=5;repeat++)
        {
            string resultName=PhasePaths.Result("synthetic-c5","C0",repeat);
            JsonObject result=JsonNode.Parse(stores.Verifier[resultName])!.AsObject();
            result["b04_recorded_evidence"]=new JsonObject
            {
                ["delivery_error_codes"]=new JsonArray(-100-repeat,repeat),
                ["fatal_errors"]=new JsonArray(new JsonObject{["code"]=repeat,["name"]="fatal-"+repeat})
            };
            byte[] bytes=Encoding.UTF8.GetBytes(result.ToJsonString());stores.Verifier[resultName]=bytes;
            string receiptName=PhasePaths.VerifyCompletion("synthetic-c5","C0",repeat);
            JsonObject receipt=JsonNode.Parse(stores.Verifier[receiptName])!.AsObject();
            UpdateReference(receipt["artifacts"]![0]!.AsObject(),resultName,bytes);
            stores.Verifier[receiptName]=Encoding.UTF8.GetBytes(receipt.ToJsonString());
        }
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System);
        Assert.Equal(0,await handler.ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        JsonElement[] projected=candidate.RootElement.GetProperty("repeats").EnumerateArray().ToArray();
        for(int i=1;i<=5;i++)
        {
            JsonElement evidence=projected[i-1].GetProperty("b04_recorded_evidence");
            Assert.Equal(new[]{-100-i,i},evidence.GetProperty("delivery_error_codes").EnumerateArray().Select(x=>x.GetInt32()));
            Assert.Equal("fatal-"+i,evidence.GetProperty("fatal_errors")[0].GetProperty("name").GetString());
        }
    }

    [Fact]
    public async Task GivenFiveAcceptedBatchingEvidenceResults_WhenJudged_ThenProjectsEachIntoItsOwnRepeat()
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        for(int repeat=1;repeat<=5;repeat++)
        {
            string resultName=PhasePaths.Result("synthetic-c5","C0",repeat);
            JsonObject result=JsonNode.Parse(stores.Verifier[resultName])!.AsObject();
            result["batching_recorded_evidence"]=new JsonObject{["delivered_latency_count"]=1,["latency_min_us"]=repeat,["latency_p50_us"]=repeat,["latency_p95_us"]=repeat,["latency_max_us"]=repeat,["produce_requests"]=100+repeat,["batch_count"]=200+repeat,["batch_max_messages"]=repeat};
            byte[] bytes=Encoding.UTF8.GetBytes(result.ToJsonString());stores.Verifier[resultName]=bytes;
            string receiptName=PhasePaths.VerifyCompletion("synthetic-c5","C0",repeat);
            JsonObject receipt=JsonNode.Parse(stores.Verifier[receiptName])!.AsObject();
            UpdateReference(receipt["artifacts"]![0]!.AsObject(),resultName,bytes);
            stores.Verifier[receiptName]=Encoding.UTF8.GetBytes(receipt.ToJsonString());
        }
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System);
        Assert.Equal(0,await handler.ExecuteAsync(TestContext.Current.CancellationToken));
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        JsonElement[] projected=candidate.RootElement.GetProperty("repeats").EnumerateArray().ToArray();
        for(int i=1;i<=5;i++)
        {
            JsonElement evidence=projected[i-1].GetProperty("batching_recorded_evidence");
            Assert.Equal(i,evidence.GetProperty("latency_p50_us").GetInt64());
            Assert.Equal(100+i,evidence.GetProperty("produce_requests").GetInt64());
            Assert.Equal(200+i,evidence.GetProperty("batch_count").GetInt64());
        }
    }

    [Theory]
    [InlineData("omitted")]
    [InlineData("measurements-omitted")]
    public async Task GivenBoundResultWithOptionalBrokerFieldsOmitted_WhenJudged_ThenPreservesAbsence(string omission)
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        string resultName=PhasePaths.Result("synthetic-c5","C0",1);
        JsonObject result=JsonNode.Parse(stores.Verifier[resultName])!.AsObject();
        if(omission=="omitted") result.Remove("broker_count");
        else
        {
            result["broker_count"]!["maximum"]?.Parent?.AsObject().Remove("maximum");
            result["broker_count"]!["first_post_drain"]?.Parent?.AsObject().Remove("first_post_drain");
        }
        byte[] bytes=Encoding.UTF8.GetBytes(result.ToJsonString());
        stores.Verifier[resultName]=bytes;
        string receiptName=PhasePaths.VerifyCompletion("synthetic-c5","C0",1);
        JsonObject receipt=JsonNode.Parse(stores.Verifier[receiptName])!.AsObject();
        UpdateReference(receipt["artifacts"]![0]!.AsObject(),resultName,bytes);
        stores.Verifier[receiptName]=Encoding.UTF8.GetBytes(receipt.ToJsonString());
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        Assert.Equal(0,await handler.ExecuteAsync(TestContext.Current.CancellationToken));

        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        JsonElement repeat=candidate.RootElement.GetProperty("repeats")[0];
        if(omission=="omitted") Assert.False(repeat.TryGetProperty("broker_count",out _));
        else
        {
            JsonElement count=repeat.GetProperty("broker_count");
            Assert.False(count.TryGetProperty("maximum",out _));
            Assert.False(count.TryGetProperty("first_post_drain",out _));
            Assert.Equal("LoadGenProducer",count.GetProperty("role").GetString());
            Assert.True(count.GetProperty("lower_bound").GetBoolean());
        }
        Assert.Equal("complete_valid",repeat.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("registration","phase","foreign")]
    [InlineData("registration","repeat","0")]
    [InlineData("registration","repeat","6")]
    [InlineData("producer-completion","phase","foreign")]
    [InlineData("producer-completion","repeat","0")]
    [InlineData("producer-completion","repeat","6")]
    [InlineData("start-line","phase","foreign")]
    [InlineData("start-line","repeat","0")]
    [InlineData("start-line","repeat","6")]
    [InlineData("verify","phase","foreign")]
    [InlineData("verify","repeat","0")]
    [InlineData("verify","repeat","6")]
    public async Task GivenUnsupportedWireCoordinate_WhenJudged_ThenRecordsFailureAndStillWritesCandidate(string reader,string field,string value)
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        const int repeat=1;
        bool producer=reader is "registration" or "producer-completion";
        string name=reader switch
        {
            "registration"=>PhasePaths.Registration("synthetic-c5","C0",repeat,"pod-c5"),
            "producer-completion"=>PhasePaths.ProducerCompletion("synthetic-c5","C0",repeat,"pod-c5"),
            "start-line"=>PhasePaths.StartLineCompletion("synthetic-c5","C0",repeat),
            _=>PhasePaths.VerifyCompletion("synthetic-c5","C0",repeat)
        };
        Dictionary<string,byte[]> objects=producer?stores.Ledger:stores.Verifier;
        JsonObject malformed=JsonNode.Parse(objects[name])!.AsObject();
        if(field=="phase") malformed[field]=value;
        else malformed[reader is "registration" or "producer-completion"?"repeat_number":"repeat"]=int.Parse(value,System.Globalization.CultureInfo.InvariantCulture);
        objects[name]=Encoding.UTF8.GetBytes(malformed.ToJsonString());
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1,exit);
        Assert.NotNull(verifier.Created);
        Assert.Equal("synthetic-c5/C0/candidate.json",verifier.CreatedName);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        JsonElement judged=candidate.RootElement.GetProperty("repeats")[repeat-1];
        string? status=judged.GetProperty("status").GetString();
        Assert.True(status is "invalid" or "incomplete");
        string failedCheck=reader switch
        {
            "registration"=>"j5_registered_producer_count",
            "producer-completion"=>"j2_publication_unaccepted",
            "start-line"=>"j4_producer_job_binding",
            _=>"j3_verifier_receipt"
        };
        Assert.False(judged.GetProperty("checks").GetProperty(failedCheck).GetBoolean());
        Assert.Contains(failedCheck,judged.GetProperty("reasons").EnumerateArray().Select(reason=>reason.GetString()));
    }

    [Fact]
    public async Task GivenRegistrationAtMalformedProducerPath_WhenJudged_ThenRecordsJ5FailureAndStillWritesCandidate()
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        string validName=PhasePaths.Registration("synthetic-c5","C0",1,"pod-c5");
        string malformedName=PhasePaths.ProducersPrefix("synthetic-c5","C0",1)+"registration.json";
        stores.Ledger[malformedName]=stores.Ledger[validName];
        stores.Ledger.Remove(validName);
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1,exit);
        Assert.NotNull(verifier.Created);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        JsonElement judged=candidate.RootElement.GetProperty("repeats")[0];
        Assert.False(judged.GetProperty("checks").GetProperty("j5_registered_producer_count").GetBoolean());
        Assert.Contains("j5_registered_producer_count",judged.GetProperty("reasons").EnumerateArray().Select(reason=>reason.GetString()));
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
    [InlineData("receipt-source", "producer_job_binding")]
    [InlineData("receipt-source", "registered_producer_count")]
    [InlineData("receipt-source", "broker_inventory_coverage")]
    [InlineData("result-source", "producer_job_binding")]
    [InlineData("result-source", "registered_producer_count")]
    [InlineData("result-source", "broker_inventory_coverage")]
    [InlineData("receipt-client", "producer_job_binding")]
    [InlineData("receipt-client", "registered_producer_count")]
    [InlineData("receipt-client", "broker_inventory_coverage")]
    public async Task GivenUnboundVerifyReceiptWithFailedProjection_WhenJudged_ThenUsesOnlyIndependentChecks(string mismatch, string failedProjection)
    {
        PhaseStores stores = await BuildFiveRepeats("C0", null);
        string resultName = PhasePaths.Result("synthetic-c5", "C0", 1);
        string receiptName = PhasePaths.VerifyCompletion("synthetic-c5", "C0", 1);
        JsonObject receipt = JsonNode.Parse(stores.Verifier[receiptName])!.AsObject();
        receipt["repeat_checks"]![failedProjection] = "fail";
        if (mismatch == "receipt-source")
        {
            receipt["source_commit"] = new string('b', 40);
            receipt["publication_acceptance"]!["source_commit"] = new string('b', 40);
        }
        else if (mismatch == "result-source")
        {
            JsonObject result = JsonNode.Parse(stores.Verifier[resultName])!.AsObject();
            result["verifier_commit"] = new string('b', 40);
            byte[] bytes = Encoding.UTF8.GetBytes(result.ToJsonString());
            stores.Verifier[resultName] = bytes;
            UpdateReference(receipt["artifacts"]![0]!.AsObject(), resultName, bytes);
        }
        else
        {
            receipt["publication_acceptance"] = null;
            receipt["identity"]!["client_id"] = "foreign-client";
        }
        stores.Verifier[receiptName] = Encoding.UTF8.GetBytes(receipt.ToJsonString());
        var verifier = new MemoryVerifierStore(stores.Verifier);
        var handler = new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()), verifier,
            new MemoryProducerStore(stores.Ledger), TimeProvider.System);

        Assert.Equal(1, await handler.ExecuteAsync(TestContext.Current.CancellationToken));

        using JsonDocument candidate = JsonDocument.Parse(verifier.Created!);
        JsonElement repeat = candidate.RootElement.GetProperty("repeats")[0];
        Assert.Equal("incomplete", repeat.GetProperty("status").GetString());
        JsonElement checks = repeat.GetProperty("checks");
        Assert.False(checks.GetProperty("j2_publication_unaccepted").GetBoolean());
        Assert.False(checks.GetProperty("j3_verifier_receipt").GetBoolean());
        Assert.True(checks.GetProperty("j4_producer_job_binding").GetBoolean());
        Assert.True(checks.GetProperty("j5_registered_producer_count").GetBoolean());
        Assert.False(checks.GetProperty("j6_broker_inventory_coverage").GetBoolean());
        Assert.DoesNotContain("trusted_result_invalid", repeat.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()));
        Assert.False(repeat.TryGetProperty("counts", out _));
        Assert.False(candidate.RootElement.TryGetProperty("counts", out _));
        Assert.False(repeat.TryGetProperty("broker_count",out _));
    }

    [Fact]
    public async Task GivenDiagnosticReceiptWithForeignClientIdentity_WhenJudged_ThenDoesNotTrustInvalidResult()
    {
        PhaseStores stores = await BuildFiveRepeats("C0", 15);
        string receiptName = PhasePaths.VerifyCompletion("synthetic-c5", "C0", 1);
        JsonObject receipt = JsonNode.Parse(stores.Verifier[receiptName])!.AsObject();
        receipt["publication_acceptance"] = null;
        receipt["identity"]!["client_id"] = "foreign-client";
        stores.Verifier[receiptName] = Encoding.UTF8.GetBytes(receipt.ToJsonString());
        var verifier = new MemoryVerifierStore(stores.Verifier);
        var handler = new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()), verifier,
            new MemoryProducerStore(stores.Ledger), TimeProvider.System);

        Assert.Equal(1, await handler.ExecuteAsync(TestContext.Current.CancellationToken));

        using JsonDocument candidate = JsonDocument.Parse(verifier.Created!);
        JsonElement repeat = candidate.RootElement.GetProperty("repeats")[0];
        Assert.Equal("incomplete", repeat.GetProperty("status").GetString());
        Assert.False(repeat.GetProperty("checks").GetProperty("j3_verifier_receipt").GetBoolean());
        Assert.DoesNotContain("trusted_result_invalid", repeat.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()));
        Assert.False(repeat.TryGetProperty("counts", out _));
    }

    [Theory]
    [InlineData("start-line-source", "j4_producer_job_binding")]
    [InlineData("duplicate-registration", "j5_registered_producer_count")]
    public async Task GivenUnboundVerifyReceiptAndIndependentViolation_WhenJudged_ThenKeepsIndependentFailure(string violation, string failedCheck)
    {
        PhaseStores stores = await BuildFiveRepeats("C0", null);
        string receiptName = PhasePaths.VerifyCompletion("synthetic-c5", "C0", 1);
        JsonObject receipt = JsonNode.Parse(stores.Verifier[receiptName])!.AsObject();
        receipt["source_commit"] = new string('b', 40);
        receipt["publication_acceptance"]!["source_commit"] = new string('b', 40);
        receipt["repeat_checks"]!["producer_job_binding"] = "pass";
        receipt["repeat_checks"]!["registered_producer_count"] = "pass";
        receipt["repeat_checks"]!["broker_inventory_coverage"] = "pass";
        stores.Verifier[receiptName] = Encoding.UTF8.GetBytes(receipt.ToJsonString());
        if (violation == "start-line-source")
        {
            string startName = PhasePaths.StartLineCompletion("synthetic-c5", "C0", 1);
            JsonObject start = JsonNode.Parse(stores.Verifier[startName])!.AsObject();
            start["source_commit"] = new string('c', 40);
            stores.Verifier[startName] = Encoding.UTF8.GetBytes(start.ToJsonString());
        }
        else
        {
            stores.Ledger[PhasePaths.Registration("synthetic-c5", "C0", 1, "pod-c5-extra")] =
                stores.Ledger[PhasePaths.Registration("synthetic-c5", "C0", 1, "pod-c5")];
        }
        var verifier = new MemoryVerifierStore(stores.Verifier);
        var handler = new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()), verifier,
            new MemoryProducerStore(stores.Ledger), TimeProvider.System);

        Assert.Equal(1, await handler.ExecuteAsync(TestContext.Current.CancellationToken));

        using JsonDocument candidate = JsonDocument.Parse(verifier.Created!);
        JsonElement repeat = candidate.RootElement.GetProperty("repeats")[0];
        Assert.Equal("invalid", repeat.GetProperty("status").GetString());
        Assert.False(repeat.GetProperty("checks").GetProperty("j3_verifier_receipt").GetBoolean());
        Assert.False(repeat.GetProperty("checks").GetProperty(failedCheck).GetBoolean());
        string?[] reasons = repeat.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()).ToArray();
        Assert.Contains(failedCheck, reasons);
        Assert.DoesNotContain("trusted_result_invalid", reasons);
        Assert.False(repeat.TryGetProperty("counts", out _));
        Assert.False(repeat.TryGetProperty("broker_count",out _));
    }

    [Fact]
    public async Task GivenMissingPublicationAcceptanceWithReadableResult_WhenJudged_ThenOmitsBrokerCount()
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        string resultName=PhasePaths.Result("synthetic-c5","C0",1);
        Assert.True(stores.Verifier.ContainsKey(resultName));
        string receiptName=PhasePaths.VerifyCompletion("synthetic-c5","C0",1);
        JsonObject receipt=JsonNode.Parse(stores.Verifier[receiptName])!.AsObject();
        receipt["publication_acceptance"]=null;
        stores.Verifier[receiptName]=Encoding.UTF8.GetBytes(receipt.ToJsonString());
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        Assert.Equal(1,await handler.ExecuteAsync(TestContext.Current.CancellationToken));

        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        JsonElement repeat=candidate.RootElement.GetProperty("repeats")[0];
        Assert.False(repeat.GetProperty("checks").GetProperty("j2_publication_unaccepted").GetBoolean());
        Assert.False(repeat.TryGetProperty("broker_count",out _));
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
        Assert.False(repeat.TryGetProperty("broker_count",out _));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("slot-job")]
    [InlineData("receipt-job")]
    [InlineData("configuration")]
    [InlineData("unsupported-phase")]
    [InlineData("repeat-zero")]
    [InlineData("repeat-six")]
    public async Task GivenKnownStartLineBindingMismatch_WhenJudged_ThenJ4FailsAndRepeatIsInvalid(string mismatch)
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        string name=PhasePaths.StartLineCompletion("synthetic-c5","C0",1);
        JsonObject receipt=JsonNode.Parse(stores.Verifier[name])!.AsObject();
        if(mismatch=="source") receipt["source_commit"]=new string('b',40);
        if(mismatch=="receipt-job") receipt["job_name"]="wrong-start-line";
        if(mismatch=="configuration") receipt["configuration_id"]="C3";
        if(mismatch=="unsupported-phase") receipt["phase"]="foreign";
        if(mismatch=="repeat-zero") receipt["repeat"]=0;
        if(mismatch=="repeat-six") receipt["repeat"]=6;
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
        Assert.False(repeat.TryGetProperty("broker_count",out _));
    }

    [Fact]
    public async Task GivenMalformedVerifyReceiptCoordinate_WhenJudged_ThenPreservesSlotAsIncomplete()
    {
        PhaseStores stores=await BuildFiveRepeats("C0",null);
        string name=PhasePaths.VerifyCompletion("synthetic-c5","C0",1);
        JsonObject receipt=JsonNode.Parse(stores.Verifier[name])!.AsObject();
        receipt["phase"]="foreign";
        stores.Verifier[name]=Encoding.UTF8.GetBytes(receipt.ToJsonString());
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var handler=new JudgeHandler(Microsoft.Extensions.Options.Options.Create(Options()),verifier,
            new MemoryProducerStore(stores.Ledger),TimeProvider.System);

        int exit=await handler.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1,exit);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        JsonElement repeat=candidate.RootElement.GetProperty("repeats")[0];
        Assert.Equal("incomplete",repeat.GetProperty("status").GetString());
        Assert.Contains("j3_verifier_receipt",repeat.GetProperty("reasons").EnumerateArray().Select(reason=>reason.GetString()));
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
        Assert.False(candidate.RootElement.GetProperty("repeats")[repeat-1]!.TryGetProperty("broker_count",out _));
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
    [InlineData("malformed-producer-completion-phase")]
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
        if(mutation=="malformed-producer-completion-phase")
        {
            string name=PhasePaths.ProducerCompletion("synthetic-c5","probe",1,"pod-c5");
            JsonObject value=JsonNode.Parse(stores.Ledger[name])!.AsObject();
            value["phase"]="foreign";
            stores.Ledger[name]=Encoding.UTF8.GetBytes(value.ToJsonString());
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
        if(mutation is "unavailable-producer-listing" or "missing-registration" or "malformed-producer-completion-phase" || mutation.StartsWith("missing-",StringComparison.Ordinal))
        {
            Assert.Equal("incomplete",candidate.RootElement.GetProperty("status").GetString());
            int slot=mutation.EndsWith("first",StringComparison.Ordinal)||mutation=="malformed-producer-completion-phase"?0:4;
            Assert.Equal("incomplete",candidate.RootElement.GetProperty("repeats")[slot]!.GetProperty("status").GetString());
        }
        if(mutation=="inventory-coverage")
            Assert.False(candidate.RootElement.GetProperty("repeats")[4]!.TryGetProperty("broker_count",out _));
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
        JsonElement versions=candidate.RootElement.GetProperty("versions");
        Assert.Equal(JsonValueKind.Null,versions.GetProperty("confluent_kafka").ValueKind);
        Assert.Equal(JsonValueKind.Null,versions.GetProperty("librdkafka").ValueKind);
        Assert.Equal(JsonValueKind.Null,versions.GetProperty("dotnet").ValueKind);
        Assert.Equal(JsonValueKind.Null,versions.GetProperty("workload_generator").ValueKind);
        Assert.Contains("version_unavailable",candidate.RootElement.GetProperty("reasons").EnumerateArray().Select(reason=>reason.GetString()));
        Assert.Equal("incomplete",candidate.RootElement.GetProperty("status").GetString());
        Assert.Equal(5,candidate.RootElement.GetProperty("repeats").GetArrayLength());
        Assert.All(candidate.RootElement.GetProperty("repeats").EnumerateArray(),repeat=>Assert.False(repeat.TryGetProperty("broker_count",out _)));
        completion.MarkReturned(options,exit,CancellationToken.None,CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out string container,out string name,out byte[] receiptBytes));
        Assert.Equal("verifier",container);
        Assert.Equal("synthetic-c5/C0/judge-completion.json",name);
        using JsonDocument receipt=JsonDocument.Parse(receiptBytes);
        Assert.Equal("synthetic-c5/C0/candidate.json",receipt.RootElement.GetProperty("artifacts")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Given_FiveB10Intakes_When_Judged_Then_RetainsOrderedPairWithoutFinalCountsOrFirstOnlyObservations()
    {
        PhaseStores stores=await BuildFiveB10Repeats();
        var verifier=new MemoryVerifierStore(stores.Verifier);
        var producer=new MemoryProducerStore(stores.Ledger){ReverseListing=true};
        var completion=new VerifierCompletionState();
        VerifierHostOptions options=Options();options.ScenarioId="B10";options.B10SettleIntervalMilliseconds=1000;
        int exit=await new JudgeHandler(Microsoft.Extensions.Options.Options.Create(options),verifier,producer,TimeProvider.System,completion)
            .ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1,exit);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        Assert.Equal("invalid",candidate.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null,candidate.RootElement.GetProperty("gate").ValueKind);
        Assert.False(candidate.RootElement.TryGetProperty("counts",out _));
        Assert.Equal(5,candidate.RootElement.GetProperty("repeats").GetArrayLength());
        foreach(JsonElement repeat in candidate.RootElement.GetProperty("repeats").EnumerateArray())
        {
            Assert.Equal("invalid",repeat.GetProperty("status").GetString());
            Assert.Contains(repeat.GetProperty("reasons").EnumerateArray(),reason=>reason.GetString()=="b10_sweep_failed");
            int number=repeat.GetProperty("repeat").GetInt32();
            string first=number==1?"pod-first":$"pod-first-r{number}";
            string second=number==1?"pod-second":$"pod-second-r{number}";
            foreach(string check in new[]{"j1_repeat_slots","j2_publication_unaccepted","j3_verifier_receipt","j4_producer_job_binding","j5_registered_producer_count","j6_broker_inventory_coverage"})Assert.True(repeat.GetProperty("checks").GetProperty(check).GetBoolean());
            foreach(string field in new[]{"counts","broker_count","b04_recorded_evidence","batching_recorded_evidence"})
                Assert.False(repeat.TryGetProperty(field,out _));
            Assert.Equal(JsonValueKind.Null,repeat.GetProperty("probe_observation").ValueKind);
            Assert.Equal(JsonValueKind.Null,repeat.GetProperty("workload_observation").ValueKind);
            using JsonDocument source=JsonDocument.Parse(stores.Verifier[PhasePaths.Result("synthetic-c5","C0",repeat.GetProperty("repeat").GetInt32())]);
            Assert.True(JsonElement.DeepEquals(source.RootElement.GetProperty("b10_intake"),repeat.GetProperty("b10_intake")));
            JsonElement producers=repeat.GetProperty("b10_intake").GetProperty("producers");
            Assert.Equal(2,producers.GetArrayLength());
            Assert.Equal(first,producers[0].GetProperty("identity").GetProperty("instance_id").GetString());
            Assert.Equal(second,producers[1].GetProperty("identity").GetProperty("instance_id").GetString());
            Assert.Equal(JsonValueKind.Null,producers[0].GetProperty("predecessor_instance_id").ValueKind);
            Assert.Equal(first,producers[1].GetProperty("predecessor_instance_id").GetString());
            Assert.All(producers.EnumerateArray(),entry=>Assert.Equal(JsonValueKind.Object,entry.GetProperty("publication_acceptance").ValueKind));
        }
        completion.MarkReturned(options,exit,CancellationToken.None,CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out _,out _,out byte[] receipt));
        Assert.Equal(1,PhaseWireCodec.ReadReceipt(receipt).HandlerExitCode);
        Assert.All(Enumerable.Range(1,5),repeat=>Assert.Equal(2,producer.Reads.Count(name=>
            name.StartsWith(PhasePaths.ProducersPrefix("synthetic-c5","C0",repeat),StringComparison.Ordinal)&&name.EndsWith("/registration.json",StringComparison.Ordinal))));
    }

    [Theory]
    [InlineData("empty","invalid",false,false)]
    [InlineData("first","invalid",false,false)]
    [InlineData("restart","invalid",false,false)]
    [InlineData("missing-bytes","invalid",false,false)]
    [InlineData("excess","invalid",true,false)]
    [InlineData("incomplete","invalid",true,false)]
    [InlineData("excess-incomplete","invalid",true,false)]
    [InlineData("malformed-path","invalid",false,false)]
    [InlineData("malformed-json","invalid",false,false)]
    [InlineData("duplicate-instance","invalid",false,false)]
    [InlineData("predecessor","invalid",false,true)]
    [InlineData("source","invalid",false,true)]
    [InlineData("source-no-receipt","invalid",false,true)]
    [InlineData("source-read-failure","invalid",false,false)]
    [InlineData("excess-read-failure","invalid",false,false)]
    public async Task Given_B10RegistrationEvidence_When_Judged_Then_KnownFailuresSurviveUnavailableEvidence(
        string change,string status,bool j4,bool j5)
    {
        PhaseStores stores=await BuildFiveB10Repeats();
        string first=PhasePaths.Registration("synthetic-c5","C0",1,"pod-first");
        string second=PhasePaths.Registration("synthetic-c5","C0",1,"pod-second");
        string extra=PhasePaths.Registration("synthetic-c5","C0",1,"z-third");
        if(change is "empty" or "restart")stores.Ledger.Remove(first);
        if(change is "empty" or "first")stores.Ledger.Remove(second);
        if(change.StartsWith("excess",StringComparison.Ordinal))stores.Ledger[extra]=[];
        if(change=="malformed-path") { stores.Ledger[first+"/registration.json"]=stores.Ledger[first];stores.Ledger.Remove(first); }
        if(change=="malformed-json")stores.Ledger[first]=Encoding.UTF8.GetBytes("{");
        if(change=="duplicate-instance")ChangeJson(stores.Ledger,second,node=>node["identity"]=JsonNode.Parse(stores.Ledger[first])!["identity"]!.DeepClone());
        if(change=="predecessor")ChangeJson(stores.Ledger,second,node=>node["predecessor_instance_id"]="foreign");
        if(change.StartsWith("source",StringComparison.Ordinal))ChangeJson(stores.Ledger,first,node=>node["source_commit"]=new string('b',40));
        if(change=="source-no-receipt")stores.Verifier.Remove(PhasePaths.VerifyCompletion("synthetic-c5","C0",1));
        var producer=new MemoryProducerStore(stores.Ledger)
        {
            IncompletePrefix=change.Contains("incomplete",StringComparison.Ordinal)?PhasePaths.ProducersPrefix("synthetic-c5","C0",1):null,
            FailingRead=change.EndsWith("read-failure",StringComparison.Ordinal)?second:null,
            MissingRead=change=="missing-bytes"?second:null
        };
        using JsonDocument candidate=await JudgeB10(stores,producer);
        JsonElement repeat=candidate.RootElement.GetProperty("repeats")[0];
        Assert.Contains(repeat.GetProperty("reasons").EnumerateArray(),reason=>reason.GetString()=="b10_sweep_failed");
        Assert.Equal(status,candidate.RootElement.GetProperty("status").GetString());
        Assert.Equal(status,repeat.GetProperty("status").GetString());
        Assert.Equal(j4,repeat.GetProperty("checks").GetProperty("j4_producer_job_binding").GetBoolean());
        Assert.Equal(j5,repeat.GetProperty("checks").GetProperty("j5_registered_producer_count").GetBoolean());
        Assert.False(repeat.TryGetProperty("b10_intake",out _));
        Assert.False(repeat.TryGetProperty("counts",out _));
        Assert.False(candidate.RootElement.TryGetProperty("counts",out _));
        Assert.DoesNotContain(extra,producer.Reads);
        if(change=="missing-bytes")
        {
            Assert.Contains(second,stores.Ledger.Keys);
            Assert.Contains(first,producer.Reads);
            Assert.Contains(second,producer.Reads);
        }
        Assert.True(producer.Reads.Count(name=>name.StartsWith(PhasePaths.ProducersPrefix("synthetic-c5","C0",1),StringComparison.Ordinal)&&name.EndsWith("/registration.json",StringComparison.Ordinal))<=2);
    }

    [Theory]
    [InlineData(0,"null")]
    [InlineData(1,"null")]
    [InlineData(0,"missing-completion")]
    [InlineData(1,"missing-completion")]
    [InlineData(0,"client")]
    [InlineData(1,"client")]
    [InlineData(0,"pod")]
    [InlineData(1,"pod")]
    [InlineData(0,"pod-uid")]
    [InlineData(1,"pod-uid")]
    [InlineData(0,"source")]
    [InlineData(1,"source")]
    [InlineData(0,"completion-hash")]
    [InlineData(1,"completion-hash")]
    [InlineData(0,"completion-length")]
    [InlineData(1,"completion-length")]
    [InlineData(0,"start-hash")]
    [InlineData(1,"start-hash")]
    [InlineData(0,"inventory-hash")]
    [InlineData(1,"inventory-hash")]
    public async Task Given_OneB10PublicationUnavailable_When_Judged_Then_OtherPublicationCannotReplaceIt(int ordinal,string change)
    {
        PhaseStores stores=await BuildFiveB10Repeats();
        string completion=PhasePaths.ProducerCompletion("synthetic-c5","C0",1,ordinal==0?"pod-first":"pod-second");
        if(change=="missing-completion")stores.Ledger.Remove(completion);
        else if(change is "client" or "pod" or "pod-uid" or "source")ChangeJson(stores.Ledger,completion,node=>
        {
            if(change=="client")node["identity"]!["client_id"]="foreign";
            else node[change switch {"pod"=>"pod_name","pod-uid"=>"pod_uid",_=>"source_commit"}]=change=="source"?new string('b',40):"foreign";
        });
        else ChangeB10Intake(stores,intake=>
        {
            JsonNode entry=intake["producers"]![ordinal]!;
            if(change=="null")entry["publication_acceptance"]=null;
            else
            {
                JsonNode reference=entry["publication_acceptance"]![change.StartsWith("completion",StringComparison.Ordinal)?"producer_receipt":change=="start-hash"?"start_line":"broker_inventory"]!;
                if(change=="completion-length")reference["length"]=reference["length"]!.GetValue<long>()+1;
                else reference["sha256"]=new string('b',64);
            }
        });
        using JsonDocument candidate=await JudgeB10(stores);
        JsonElement repeat=candidate.RootElement.GetProperty("repeats")[0];
        Assert.Contains(repeat.GetProperty("reasons").EnumerateArray(),reason=>reason.GetString()=="b10_sweep_failed");
        Assert.Equal("invalid",repeat.GetProperty("status").GetString());
        Assert.False(repeat.GetProperty("checks").GetProperty("j2_publication_unaccepted").GetBoolean());
        Assert.True(repeat.GetProperty("checks").GetProperty("j3_verifier_receipt").GetBoolean());
        Assert.True(repeat.GetProperty("checks").GetProperty("j4_producer_job_binding").GetBoolean());
        Assert.True(repeat.GetProperty("checks").GetProperty("j5_registered_producer_count").GetBoolean());
        Assert.False(repeat.TryGetProperty("b10_intake",out _));
        Assert.False(repeat.TryGetProperty("counts",out _));
        Assert.False(candidate.RootElement.TryGetProperty("counts",out _));
    }

    [Theory]
    [InlineData("client")]
    [InlineData("identity")]
    [InlineData("object-hash")]
    [InlineData("null")]
    [InlineData("phase")]
    [InlineData("repeat")]
    [InlineData("job")]
    [InlineData("predecessor")]
    public async Task Given_B10ReceiptResultDisagreement_When_Judged_Then_UntrustedInvalidResultDoesNotSetInvalidity(string change)
    {
        PhaseStores stores=await BuildFiveB10Repeats();
        ChangeJson(stores.Verifier,PhasePaths.Result("synthetic-c5","C0",1),result=>
        {
            result["status"]="invalid";
            JsonNode intake=result["b10_intake"]!;
            JsonNode second=intake["producers"]![1]!;
            if(change=="client")
            {
                second["identity"]!["client_id"]="foreign";
                second["publication_acceptance"]!["producer_identity"]!["client_id"]="foreign";
            }
            if(change=="identity")
            {
                second["identity"]!["instance_id"]="foreign";
                second["identity"]!["expected_instance_id"]="foreign";
                JsonNode acceptance=second["publication_acceptance"]!;
                acceptance["producer_identity"]=second["identity"]!.DeepClone();
                acceptance["producer_prefix"]=PhasePaths.ProducerPrefix("synthetic-c5","C0",1,"foreign");
                acceptance["producer_receipt"]!["name"]=PhasePaths.ProducerCompletion("synthetic-c5","C0",1,"foreign");
                foreach(JsonNode? reference in acceptance["objects"]!.AsArray())reference!["name"]=reference["name"]!.GetValue<string>().Replace("pod-second","foreign",StringComparison.Ordinal);
            }
            if(change=="object-hash")second["publication_acceptance"]!["objects"]![0]!["sha256"]=new string('b',64);
            if(change=="null")second["publication_acceptance"]=null;
            if(change=="phase")intake["phase"]="C1";
            if(change=="repeat")intake["repeat"]=2;
            if(change=="job")second["job_name"]="foreign";
            if(change=="predecessor")second["predecessor_instance_id"]="foreign";
        });
        RefreshB10ResultReference(stores);
        using JsonDocument candidate=await JudgeB10(stores);
        JsonElement repeat=candidate.RootElement.GetProperty("repeats")[0];
        Assert.Contains(repeat.GetProperty("reasons").EnumerateArray(),reason=>reason.GetString()=="b10_sweep_failed");
        Assert.Equal("invalid",repeat.GetProperty("status").GetString());
        Assert.False(repeat.GetProperty("checks").GetProperty("j3_verifier_receipt").GetBoolean());
        Assert.DoesNotContain(repeat.GetProperty("reasons").EnumerateArray(),reason=>reason.GetString()=="trusted_result_invalid");
        Assert.False(repeat.TryGetProperty("b10_intake",out _));
        Assert.False(repeat.TryGetProperty("counts",out _));
        Assert.False(candidate.RootElement.TryGetProperty("counts",out _));
    }

    [Theory]
    [InlineData("valid",true)]
    [InlineData("missing",false)]
    [InlineData("one",false)]
    [InlineData("reverse",false)]
    [InlineData("duplicate",false)]
    [InlineData("extra",false)]
    [InlineData("foreign",false)]
    [InlineData("source",false)]
    [InlineData("slot",false)]
    public async Task Given_B10StartJobList_When_Judged_Then_RequiresDerivedOrderedPair(string change,bool accepted)
    {
        PhaseStores stores=await BuildFiveB10Repeats();
        string noteName=PhasePaths.StartLine("synthetic-c5","C0",1);
        ChangeJson(stores.Verifier,noteName,note=>
        {
            JsonArray names=note["expected_job_names"]!.AsArray();
            string first=names[0]!.GetValue<string>(),second=names[1]!.GetValue<string>();
            if(change=="missing")note.Remove("expected_job_names");
            if(change=="one")note["expected_job_names"]=new JsonArray(first);
            if(change=="reverse")note["expected_job_names"]=new JsonArray(second,first);
            if(change=="duplicate")note["expected_job_names"]=new JsonArray(first,first);
            if(change=="extra")note["expected_job_names"]=new JsonArray(first,second,"foreign");
            if(change=="foreign")note["expected_job_names"]=new JsonArray(first,"foreign");
        });
        ChangeJson(stores.Verifier,PhasePaths.StartLineCompletion("synthetic-c5","C0",1),receipt=>
        {
            UpdateReference(receipt["artifacts"]![0]!.AsObject(),noteName,stores.Verifier[noteName]);
            if(change=="source")receipt["source_commit"]=new string('b',40);
            if(change=="slot")receipt["repeat"]=2;
        });
        ChangeB10Intake(stores,intake=>
        {
            foreach(JsonNode? producer in intake["producers"]!.AsArray())
                UpdateReference(producer!["publication_acceptance"]!["start_line"]!.AsObject(),noteName,stores.Verifier[noteName]);
        });
        using JsonDocument candidate=await JudgeB10(stores);
        JsonElement repeat=candidate.RootElement.GetProperty("repeats")[0];
        Assert.Contains(repeat.GetProperty("reasons").EnumerateArray(),reason=>reason.GetString()=="b10_sweep_failed");
        Assert.Equal(accepted,repeat.GetProperty("checks").GetProperty("j2_publication_unaccepted").GetBoolean());
        Assert.Equal("invalid",repeat.GetProperty("status").GetString());
        if(change is "source" or "slot")Assert.False(repeat.GetProperty("checks").GetProperty("j4_producer_job_binding").GetBoolean());
        Assert.Equal(accepted,repeat.TryGetProperty("b10_intake",out _));
        Assert.False(repeat.TryGetProperty("counts",out _));
    }

    [Theory]
    [InlineData("valid","complete_valid",true)]
    [InlineData("partial","invalid",true)]
    [InlineData("failed","invalid",false)]
    [InlineData("missing-receipt","invalid",false)]
    [InlineData("malformed","invalid",false)]
    [InlineData("result-changed","invalid",false)]
    [InlineData("receipt-changed","invalid",false)]
    [InlineData("legacy","invalid",false)]
    [InlineData("settle","invalid",false)]
    [InlineData("note-changed","invalid",false)]
    public async Task Given_B10Sweep_When_Judged_Then_AuthenticatesOriginalBytesAndOnlyPromotesEligibleRepeats(string change,string status,bool scanComplete)
    {
        PhaseStores stores=await BuildFiveB10Repeats();
        if(change=="legacy")
        {
            ChangeJson(stores.Verifier,PhasePaths.Result("synthetic-c5","C0",1),result=>result.Remove("b10_window_checks_complete"));
            RefreshB10ResultReference(stores);
        }
        AttachSweep(stores,change);
        if(change=="missing-receipt")stores.Verifier.Remove(PhasePaths.B10SweepCompletion("synthetic-c5","C0"));
        if(change=="malformed")stores.Verifier[PhasePaths.B10Finalization("synthetic-c5","C0")]=Encoding.UTF8.GetBytes("{");
        if(change=="result-changed")
        {
            ChangeJson(stores.Verifier,PhasePaths.Result("synthetic-c5","C0",1),result=>result["generator_version"]="changed");
            RefreshB10ResultReference(stores);
        }
        if(change=="receipt-changed")ChangeJson(stores.Verifier,PhasePaths.VerifyCompletion("synthetic-c5","C0",1),receipt=>receipt["pod_name"]="different-verifier");
        if(change=="note-changed")ChangeJson(stores.Verifier,PhasePaths.SweepLine("synthetic-c5","C0"),note=>note["partition_highs"]![0]!["high"]=10);
        var verifier=new MemoryVerifierStore(stores.Verifier);
        VerifierHostOptions options=Options();options.ScenarioId="B10";options.B10SettleIntervalMilliseconds=change=="settle"?1001:1000;
        int exit=await new JudgeHandler(Microsoft.Extensions.Options.Options.Create(options),verifier,new MemoryProducerStore(stores.Ledger),TimeProvider.System)
            .ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.Equal(status=="complete_valid"?0:1,exit);
        using JsonDocument candidate=JsonDocument.Parse(verifier.Created!);
        Assert.Equal(status,candidate.RootElement.GetProperty("status").GetString());
        Assert.Equal(scanComplete,candidate.RootElement.GetProperty("b10_sweep").GetProperty("scan_complete").GetBoolean());
        Assert.Equal(B10SweepEvidence.ResidualRisk,candidate.RootElement.GetProperty("b10_sweep").GetProperty("residual_risk").GetString());
        if(change=="valid")
        {
            Assert.Equal(15,candidate.RootElement.GetProperty("counts").GetProperty("ok").GetInt64());
            Assert.Equal(10,candidate.RootElement.GetProperty("counts").GetProperty("duplicate").GetInt64());
        }
        else Assert.False(candidate.RootElement.TryGetProperty("counts",out _));
        foreach(JsonElement repeat in candidate.RootElement.GetProperty("repeats").EnumerateArray())
        {
            bool final=scanComplete && (change!="partial" || repeat.GetProperty("repeat").GetInt32()!=2);
            Assert.Equal(final,repeat.GetProperty("classification_final").GetBoolean());
            Assert.Equal(final,repeat.TryGetProperty("counts",out _));
            Assert.Equal(!scanComplete,repeat.GetProperty("reasons").EnumerateArray().Any(reason=>reason.GetString()=="b10_sweep_failed"));
            Assert.DoesNotContain(repeat.GetProperty("reasons").EnumerateArray(),reason=>reason.GetString()=="trusted_result_invalid");
            Assert.False(repeat.TryGetProperty("broker_count",out _));
        }
    }

    private static void AttachSweep(PhaseStores stores,string change)
    {
        const string run="synthetic-c5";
        ArtifactReference Reference(string name)=>new("verifier",name,stores.Verifier[name].LongLength,Convert.ToHexStringLower(SHA256.HashData(stores.Verifier[name])));
        DateTimeOffset completed=Enumerable.Range(1,5).Select(i=>PhaseWireCodec.ReadReceipt(stores.Verifier[PhasePaths.VerifyCompletion(run,"C0",i)]))
            .Select(receipt=>DateTimeOffset.Parse(receipt.CompletedUtc,System.Globalization.CultureInfo.InvariantCulture)).Max();
        DateTimeOffset captured=completed.AddSeconds(2);
        string noteName=PhasePaths.SweepLine(run,"C0");
        stores.Verifier[noteName]=B10SweepEvidenceCodec.WriteSweepLine(new(1,"B10","C0",run,"C0",new('a',40),"document-events",[new(0,1)],captured,completed,captured.AddSeconds(-1),1000));
        B10RepeatFinalization[] repeats=Enumerable.Range(1,5).Select(i=>new B10RepeatFinalization(i,Reference(PhasePaths.Result(run,"C0",i)),Reference(PhasePaths.VerifyCompletion(run,"C0",i)),true,VerifierResultStatus.CompleteValid,new(3,0,2,0,2,2),[])).ToArray();
        if(change=="partial")repeats[1]=repeats[1] with{ClassificationFinal=false,Status=VerifierResultStatus.Invalid,Counts=null,Reasons=["record_join_failed"]};
        if(change=="failed")repeats=repeats.Select(repeat=>repeat with{ClassificationFinal=false,Status=VerifierResultStatus.Invalid,Counts=null,Reasons=["scan_failed"]}).ToArray();
        string finalizationName=PhasePaths.B10Finalization(run,"C0");
        stores.Verifier[finalizationName]=B10SweepEvidenceCodec.WriteFinalization(new(1,"B10","C0",run,"C0",new('a',40),Reference(noteName),change!="failed",B10SweepEvidence.ResidualRisk,repeats));
        stores.Verifier[PhasePaths.B10SweepCompletion(run,"C0")]=PhaseWireCodec.WriteReceipt(new(1,"b10-sweep","B10","C0",run,"C0",null,PhaseIdentity.B10SweepJobName(run,"C0"),"sweep-pod",null,new('a',40),change is "partial" or "failed"?1:0,true,captured.ToString("O"),captured.AddSeconds(1).ToString("O"),[Reference(finalizationName)],null,null));
    }

    private static void ChangeJson(Dictionary<string,byte[]> objects,string name,Action<JsonObject> change)
    {
        JsonObject node=JsonNode.Parse(objects[name])!.AsObject();change(node);
        objects[name]=Encoding.UTF8.GetBytes(node.ToJsonString());
    }

    private static void ChangeB10Intake(PhaseStores stores,Action<JsonNode> change)
    {
        foreach(string name in new[]{PhasePaths.VerifyCompletion("synthetic-c5","C0",1),PhasePaths.Result("synthetic-c5","C0",1)})
            ChangeJson(stores.Verifier,name,node=>
            {
                change(node["b10_intake"]!);
                if(node.ContainsKey("b10_window_checks_complete") && node["b10_intake"]!["producers"]!.AsArray().Any(producer=>producer!["publication_acceptance"] is null))
                    node["b10_window_checks_complete"]=false;
            });
        RefreshB10ResultReference(stores);
    }

    private static void RefreshB10ResultReference(PhaseStores stores)
    {
        string name=PhasePaths.Result("synthetic-c5","C0",1);
        ChangeJson(stores.Verifier,PhasePaths.VerifyCompletion("synthetic-c5","C0",1),receipt=>
            UpdateReference(receipt["artifacts"]![0]!.AsObject(),name,stores.Verifier[name]));
    }

    private static async Task<JsonDocument> JudgeB10(PhaseStores stores,MemoryProducerStore? producer=null)
    {
        var verifier=new MemoryVerifierStore(stores.Verifier);
        VerifierHostOptions options=Options();options.ScenarioId="B10";options.B10SettleIntervalMilliseconds=1000;
        int exit=await new JudgeHandler(Microsoft.Extensions.Options.Options.Create(options),verifier,
            producer??new MemoryProducerStore(stores.Ledger),TimeProvider.System).ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1,exit);
        return JsonDocument.Parse(verifier.Created!);
    }

    private static async Task<PhaseStores> BuildFiveB10Repeats()
    {
        var verifierObjects=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        var ledgerObjects=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        for(int repeat=1;repeat<=5;repeat++)
        {
            using var fixture=B10SweepFixture.CreateRepeat(repeat);
            foreach(var item in ledgerObjects.Where(item=>item.Key.EndsWith("/registration.json",StringComparison.Ordinal)))
                fixture.AddLedgerObject(item.Key,item.Value);
            var completion=new VerifierCompletionState();
            int exit=await fixture.Verify(completion).ExecuteAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1,exit);
            completion.MarkReturned(fixture.Options,exit,CancellationToken.None,CancellationToken.None);
            Assert.True(completion.TryGetBlobReceipt(out _,out string name,out byte[] bytes));
            await fixture.CreateAsync(name,bytes,CancellationToken.None);
            foreach(var item in fixture.VerifierObjects)verifierObjects[item.Key]=item.Value;
            foreach(var item in fixture.LedgerObjects)ledgerObjects[item.Key]=item.Value;
        }
        return new(verifierObjects,ledgerObjects);
    }

    private static VerifierHostOptions Options()=>new()
    {
        Mode="judge",ScenarioId="A01",ConfigurationId="C0",RunId="synthetic-c5",Phase="C0",SourceCommit=new string('a',40),VerifierCommit=new string('a',40),
        JobName="synthetic-c5-c0-judge",PodName="judge-pod",VerifierContainer="verifier",LedgerContainer="ledger",
        CandidateOutputName="synthetic-c5/C0/candidate.json",IoTimeout=TimeSpan.FromSeconds(1),MaxDiagnosticsPerCheck=16,MaxOutputBytes=16_777_216,
        LoadGenImage="loadgen:tag@sha256:"+new string('b',64),VerifierImage="verifier:tag@sha256:"+new string('c',64)
    };

    private static async Task<PhaseStores> BuildFiveRepeats(
        string phase,int? invalidCheck,Action<HostEvidenceBuilder,int>? configure=null)
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
            configure?.Invoke(fixture,repeat);
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

    private static void SetWorkerRuntimeVersions(HostEvidenceBuilder fixture,
        string? kafkaVersion="2.16.0")
    {
        JsonObject captured=JsonNode.Parse(File.ReadAllBytes(
            RecordedEvidence.FixturePath("captured-run/manifest.json")))!.AsObject();
        JsonObject manifest=fixture.Json("manifest.json");
        JsonObject versions=captured["runtime_versions"]!.DeepClone().AsObject();
        if(kafkaVersion is null)versions.Remove("confluent_kafka");
        else versions["confluent_kafka"]=kafkaVersion;
        manifest["runtime_versions"]=versions;
        fixture.Set("manifest.json",manifest);
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

    private static void AssertBrokerCountEqual(JsonElement expected,JsonElement actual)
    {
        Assert.Equal(expected.GetProperty("role").GetString(),actual.GetProperty("role").GetString());
        Assert.Equal(expected.GetProperty("client_id").GetString(),actual.GetProperty("client_id").GetString());
        Assert.Equal(expected.GetProperty("maximum").GetInt64(),actual.GetProperty("maximum").GetInt64());
        Assert.Equal(expected.GetProperty("first_post_drain").GetInt64(),actual.GetProperty("first_post_drain").GetInt64());
        Assert.Equal(expected.GetProperty("lower_bound").GetBoolean(),actual.GetProperty("lower_bound").GetBoolean());
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
        public bool ReverseListing{get;init;}
        public string? FailingRead{get;init;}
        public string? MissingRead{get;init;}
        public List<string> Reads{get;}=[];
        public Task<BlobListing> ListAsync(string prefix,CancellationToken cancellationToken)
        {
            IEnumerable<string> names=objects.Keys.Where(n=>n.StartsWith(prefix,StringComparison.Ordinal)).Order(StringComparer.Ordinal);
            if(ReverseListing)names=names.Reverse();
            return Task.FromResult(new BlobListing(names.ToArray(),1,IncompletePrefix!=prefix,IncompletePrefix==prefix?"listing_unavailable":null));
        }
        public Task<byte[]?> ReadAsync(string name,CancellationToken cancellationToken)
        {
            Reads.Add(name);
            if(name==FailingRead)throw new TimeoutException("Synthetic unavailable registration.");
            if(name==MissingRead)return Task.FromResult<byte[]?>(null);
            return Task.FromResult(objects.TryGetValue(name,out var value)?(byte[]?)value:null);
        }
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
