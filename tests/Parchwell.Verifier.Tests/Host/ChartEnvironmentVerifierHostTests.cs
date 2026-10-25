using System.Text.Json;
using System.Diagnostics.Tracing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Parchwell.Verifier;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

[CollectionDefinition("VerifierEnvironment",DisableParallelization=true)]
public sealed class VerifierEnvironmentScopeDefinition { }

[Collection("VerifierEnvironment")]
#pragma warning disable CA1707
public sealed class ChartEnvironmentVerifierHostTests
{
    private static readonly int[] Check27Only=[27];
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GivenChartVerifyKeysWithoutCallerProducerIdentity_WhenProductionHostRuns_ThenDiscoversProducerAndPreservesFullFidelity(bool mismatch)
    {
        using var fixture=new HostEvidenceBuilder("pod-c5","producer-client-c5","verifier-pod-c5");
        fixture.ConfigureRepeat(1,"C0");
        if(mismatch)fixture.SetCapturedClientId("other-client-c5");
        using var environment=new ChartEnvironmentScope(ChartVerifyEnvironment());
        using var requestGuard=new OfflineRequestGuard();
        var resourceReset=new OfflineResourceReset();
        var publisher=new FixtureReceiptPublisher(fixture);

        int exit=await Program.RunAsync([],services=>
        {
            services.RemoveAll<IHostedService>();
            foreach(var descriptor in services.Where(item=>item.ServiceType.Name is "IConfigureTracerProviderBuilder" or "IConfigureMeterProviderBuilder" or "IConfigureLoggerProviderBuilder").ToArray())
                services.Remove(descriptor);
            services.AddOpenTelemetry().ConfigureResource(resourceReset.Prepare);
            services.RemoveAll<IVerifierArtifactStore>();services.AddSingleton<IVerifierArtifactStore>(fixture);
            services.RemoveAll<IProducerArtifactStore>();services.AddSingleton<IProducerArtifactStore>(fixture);
            services.RemoveAll<VerifierKafkaSessionFactory>();
            services.AddSingleton(new VerifierKafkaSessionFactory(_=>fixture.Kafka.Session));
            services.RemoveAll<ICompletionReceiptPublisher>();services.AddSingleton<ICompletionReceiptPublisher>(publisher);
        }).WaitAsync(TimeSpan.FromSeconds(5),TestContext.Current.CancellationToken);
        Assert.False(requestGuard.UnexpectedRequest,$"The offline chart-host proof attempted an HTTP request: {requestGuard.LastRequest}");
        Assert.Equal(0,resourceReset.DetectCalls);

        Assert.Equal(mismatch?1:0,exit);
        string resultName=PhasePaths.Result("synthetic-c5","C0",1);
        using JsonDocument result=JsonDocument.Parse(fixture.CreatedObjects[resultName]);
        JsonElement root=result.RootElement;
        Assert.Equal(mismatch?"invalid":"complete_valid",root.GetProperty("status").GetString());
        Assert.Equal(27,root.GetProperty("configuration_fidelity").GetProperty("assessment").GetProperty("check_number").GetInt32());
        JsonElement assessment=root.GetProperty("configuration_fidelity").GetProperty("assessment");
        Assert.Equal(mismatch?"fail":"pass",assessment.GetProperty("disposition").GetString());
        Assert.Equal(mismatch?Check27Only:Array.Empty<int>(),root.GetProperty("failed_checks").EnumerateArray().Select(x=>x.GetInt32()));
        if(mismatch)
        {
            Assert.False(root.TryGetProperty("ok",out _));
            JsonElement difference=Assert.Single(root.GetProperty("configuration_fidelity").GetProperty("differences").EnumerateArray());
            Assert.Equal("client.id",difference.GetProperty("setting").GetString());
            Assert.Equal("producer-client-c5",difference.GetProperty("expected").GetString());
            Assert.Equal("other-client-c5",difference.GetProperty("actual").GetString());
        }
        else
        {
            Assert.Equal(1,root.GetProperty("ok").GetInt64());
            foreach(string name in new[]{"loss","phantom","reported_failure","duplicate","reorder"})Assert.Equal(0,root.GetProperty(name).GetInt64());
        }
        Assert.True(publisher.Receipt is not null);
        WorkerReceipt receipt=PhaseWireCodec.ReadReceipt(publisher.Receipt!);
        Assert.Equal(mismatch?1:0,receipt.HandlerExitCode);
        Assert.NotNull(receipt.PublicationAcceptance);
        Assert.DoesNotContain(Environment.GetEnvironmentVariables().Keys.Cast<string>(),key=>key is "Verifier__InstanceId" or "Verifier__ClientId" or "Verifier__ExpectedBrokerNames");
    }

    private static Dictionary<string,string?> ChartVerifyEnvironment()
    {
        string run="synthetic-c5",phase="C0",loadgen=PhaseIdentity.JobName(run,phase,1),verify=PhaseIdentity.VerifyJobName(run,phase,1),commit=new string('a',40);
        var values=new Dictionary<string,string?>(StringComparer.Ordinal)
        {
            ["Verifier__Mode"]="verify",["Verifier__ScenarioId"]="A01",["Verifier__ConfigurationId"]="C0",["Verifier__RunId"]=run,["Verifier__Phase"]=phase,
            ["Verifier__VerifierCommit"]=commit,["Verifier__BlobServiceUri"]="https://storage.example/",["Verifier__VerifierContainer"]="verifier",["Verifier__LedgerContainer"]="ledger",
            ["Verifier__VerifierClientId"]="verifier-pod-c5",["Verifier__NamespaceName"]="synthetic-namespace",["Verifier__Repeat"]="1",["Verifier__ExpectedInstanceId"]=loadgen,
            ["Verifier__RepeatPrefix"]=PhaseIdentity.RepeatPrefix(run,phase,1),["Verifier__ArtifactPrefix"]=PhasePaths.ProducersPrefix(run,phase,1),
            ["Verifier__StartLineOutputName"]=PhasePaths.StartLine(run,phase,1),["Verifier__BrokerInventoryOutputName"]=PhasePaths.BrokerInventory(run,phase,1),
            ["Verifier__StartLineInputName"]=PhasePaths.StartLine(run,phase,1),["Verifier__ResultOutputName"]=PhasePaths.Result(run,phase,1),
            ["Verifier__MaxOutputBytes"]="16777216",["Verifier__MaxDiagnosticsPerCheck"]="100",["Verifier__MaxDiagnosticBytes"]="2048",
            ["Verifier__CleanupTimeout"]="00:00:10",["Verifier__IoTimeout"]="00:00:10",["Verifier__MaxInputDepth"]="32",
            ["Verifier__TokenTimeout"]="00:00:10",["Verifier__MetadataTimeout"]="00:00:10",["Verifier__WatermarkTimeout"]="00:00:10",
            ["Verifier__PollSlice"]="00:00:00.100",["Verifier__BlobIoTimeout"]="00:00:30",["Verifier__MaxPartitions"]="32",["Verifier__ScenarioRoot"]="/app",
            ["Verifier__ProcessLifetime"]="00:12:00",["Verifier__SafetyStop"]="00:10:00",["Verifier__MaxConsumedRecords"]="372000",["Verifier__MaxConsumedBytes"]="17179869184",
            ["Verifier__MaxArtifactBytes"]="268435456",["Verifier__MaxTotalInputBytes"]="1073741824",["Verifier__MaxRowBytes"]="16777216",["Verifier__MaxRowsPerArtifact"]="372000",
            ["Verifier__MaxInventoryEntries"]="186000",["Verifier__MaxInputEntries"]="4194304",["Verifier__MaxNativeReportBytes"]="1048576",["Verifier__MaxBrokerEntries"]="64",
            ["Verifier__ScenarioLoadOptions__MaxDocumentCharacters"]="1048576",["Verifier__ScenarioLoadOptions__MaxNestingDepth"]="32",
            ["Verifier__ScenarioLoadOptions__MaxScalarCharacters"]="65536",["Verifier__ScenarioLoadOptions__MaxParserEvents"]="100000",
            ["PARCHWELL_SOURCE_COMMIT"]=commit,["PARCHWELL_JOB_NAME"]=verify,["PARCHWELL_POD_NAME"]="verifier-pod-c5",["PARCHWELL_POD_UID"]="verifier-pod-uid-c5",
            ["AZURE_TENANT_ID"]="tenant-c5",["AZURE_CLIENT_ID"]="workload-client-c5",["AZURE_FEDERATED_TOKEN_FILE"]="/tmp/federated-token-c5",
            ["Telemetry__ConnectionString"]="InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=http://127.0.0.1:1/",
            ["Telemetry__ServiceName"]="Parchwell.Verifier",["Telemetry__ServiceInstanceId"]="verifier-pod-c5",["Telemetry__ScenarioId"]="A01",["Telemetry__RunId"]=run
        };
        return values;
    }

    private sealed class ChartEnvironmentScope:IDisposable
    {
        private readonly Dictionary<string,string?> original=new(StringComparer.Ordinal);
        public ChartEnvironmentScope(IReadOnlyDictionary<string,string?> values)
        {
            foreach(string name in Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(name=>name.StartsWith("Verifier__",StringComparison.Ordinal)||name.StartsWith("Telemetry__",StringComparison.Ordinal)||name.StartsWith("PARCHWELL_",StringComparison.Ordinal)||name.StartsWith("AZURE_",StringComparison.Ordinal)))
            {original[name]=Environment.GetEnvironmentVariable(name);Environment.SetEnvironmentVariable(name,null);}
            foreach(var pair in values){if(!original.ContainsKey(pair.Key))original[pair.Key]=Environment.GetEnvironmentVariable(pair.Key);Environment.SetEnvironmentVariable(pair.Key,pair.Value);}
        }
        public void Dispose(){foreach(var pair in original)Environment.SetEnvironmentVariable(pair.Key,pair.Value);}
    }

    private sealed class FixtureReceiptPublisher(HostEvidenceBuilder fixture):ICompletionReceiptPublisher
    {
        public byte[]? Receipt{get;private set;}
        public Task PublishAsync(VerifierHostOptions options,string container,string name,byte[] bytes,CancellationToken token)
        {Receipt=bytes;return fixture.CreateAsync(name,bytes,token);}
    }

    private sealed class OfflineRequestGuard:EventListener
    {
        private int unexpectedRequest;
        internal string? LastRequest{get;private set;}
        internal bool UnexpectedRequest=>Volatile.Read(ref unexpectedRequest)!=0;
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if(eventSource.Name=="System.Net.Http")EnableEvents(eventSource,EventLevel.Informational);
        }
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if(eventData.EventSource.Name=="System.Net.Http"&&eventData.EventId==1)
            {
                LastRequest=string.Join(";",(eventData.PayloadNames??[]).Zip(eventData.Payload??[],(name,value)=>$"{name}={value}"));
                if(!(eventData.Payload?.Any(value=>value?.ToString()?.Contains("127.0.0.1",StringComparison.Ordinal)==true)??false))
                    Interlocked.Exchange(ref unexpectedRequest,1);
            }
        }
    }

    private sealed class OfflineResourceReset:IResourceDetector
    {
        private int detectCalls;
        internal int DetectCalls=>Volatile.Read(ref detectCalls);
        internal void Prepare(ResourceBuilder resource)
        {
            resource.AddDetector(this);
            resource.Clear();
            resource.AddAttributes([new KeyValuePair<string,object>("parchwell.test.resource_reset",true)]);
        }
        public Resource Detect()
        {
            Interlocked.Increment(ref detectCalls);
            return Resource.Empty;
        }
    }
}
#pragma warning restore CA1707
