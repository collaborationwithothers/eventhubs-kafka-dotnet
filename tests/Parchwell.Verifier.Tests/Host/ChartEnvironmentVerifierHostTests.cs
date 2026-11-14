using System.Text.Json;
using System.Diagnostics.Tracing;
using System.Diagnostics;
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
public sealed class ChartEnvironmentVerifierHostTests(ITestOutputHelper output)
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
        var requestGuard=new OfflineRequestGuard();
        try
        {
        requestGuard.RecordPhase("host_starting");
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
        requestGuard.RecordPhase("host_returned",exit);
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
        finally
        {
            requestGuard.RecordPhase("guard_disposing");
            requestGuard.Dispose();
            requestGuard.RecordPhase("guard_disposed");
            requestGuard.WriteDiagnostics(output.WriteLine);
        }
    }

    [Fact]
    public void GivenSyntheticRequestEvents_WhenRecorded_ThenPreservesPredicateAndAttribution()
    {
        Activity? original=Activity.Current;
        Activity.Current=null;
        try
        {
            using var guard=new OfflineRequestGuard(syntheticOnly:true);
            guard.RecordPhase("host_starting");
            Guid activityId=Guid.Parse("11111111-1111-1111-1111-111111111111");
            Guid relatedActivityId=Guid.Parse("22222222-2222-2222-2222-222222222222");
            guard.RecordRequest("SyntheticRequestStart",1,activityId,relatedActivityId,["host"],["127.0.0.1"],null);
            Assert.Equal("host=127.0.0.1",guard.LastRequest);
            Assert.False(guard.UnexpectedRequest);
            guard.RecordRequest("SyntheticRequestStart",1,activityId,relatedActivityId,["host"],["dc.services.visualstudio.com"],null);
            Assert.Equal("host=dc.services.visualstudio.com",guard.LastRequest);
            Assert.True(guard.UnexpectedRequest);
            guard.RecordRequest("SyntheticRequestStart",1,activityId,relatedActivityId,["host"],["127.0.0.1"],null);
            Assert.Equal("host=127.0.0.1",guard.LastRequest);
            Assert.True(guard.UnexpectedRequest);
            GuardDiagnostic snapshot=guard.Snapshot();
            Assert.Equal("diagnostic_only",snapshot.Purpose);
            Assert.NotEmpty(snapshot.GuardId);
            Assert.Equal(snapshot.GuardId,snapshot.Phases[0].GuardId);
            RequestDiagnostic request=snapshot.Requests[1];
            Assert.Equal(snapshot.GuardId,request.GuardId);
            Assert.Equal("SyntheticRequestStart",request.EventName);
            Assert.Equal(1,request.EventId);
            Assert.Equal(activityId,request.ActivityId);
            Assert.Equal(relatedActivityId,request.RelatedActivityId);
            Assert.Equal("host_starting",request.Phase);
            Assert.Equal("host=dc.services.visualstudio.com",request.Payload);
            Assert.Equal(Environment.CurrentManagedThreadId,request.ManagedThreadId);
            Assert.Equal("unavailable",request.CurrentActivityId);
            Assert.Equal("unavailable",request.CurrentActivityParentId);
            Assert.Equal("unavailable",request.CurrentActivityOperation);
            Assert.Equal("unavailable",request.CurrentActivitySource);
            Assert.NotEmpty(request.CallbackStack);
            Assert.Contains("\"callback_stack\":",JsonSerializer.Serialize(snapshot),StringComparison.Ordinal);
            guard.RecordPhase("guard_disposing");
            guard.Dispose();
            guard.RecordPhase("guard_disposed");
            string? rendered=null;
            guard.WriteDiagnostics(value=>rendered=value);
            Assert.NotNull(rendered);
            Assert.Contains("guard_disposed",rendered,StringComparison.Ordinal);
        }
        finally{Activity.Current=original;}
    }

    [Fact]
    public async Task GivenRealRequestFromAnotherComponent_WhenSyntheticGuardLives_ThenSyntheticGuardIgnoresIt()
    {
        using var observing=new OfflineRequestGuard();
        using var synthetic=new OfflineRequestGuard(syntheticOnly:true);
        var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);
        listener.Start();
        try
        {
            int port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            using var client=new HttpClient{Timeout=TimeSpan.FromMilliseconds(300)};
            // "localhost" is a host other than 127.0.0.1, as a leaked telemetry request would be.
            try{await client.GetAsync(new Uri($"http://localhost:{port}/"),TestContext.Current.CancellationToken);}
            catch(Exception e)when(e is HttpRequestException or TaskCanceledException){}
        }
        finally{listener.Stop();}
        Assert.True(observing.UnexpectedRequest);
        Assert.False(synthetic.UnexpectedRequest);
        Assert.Null(synthetic.LastRequest);
        Assert.Empty(synthetic.Snapshot().Requests);
    }

    [Fact]
    public void GivenCurrentActivity_WhenRecorded_ThenRetainsCorrelationWithoutChangingAcceptance()
    {
        using var guard=new OfflineRequestGuard(syntheticOnly:true);
        using var activity=new Activity("synthetic-operation").SetParentId("synthetic-parent").Start();
        guard.RecordRequest("SyntheticRequestStart",1,Guid.Empty,Guid.Empty,["host"],["127.0.0.1"],activity);
        RequestDiagnostic record=Assert.Single(guard.Snapshot().Requests);
        Assert.Equal(activity.Id,record.CurrentActivityId);
        Assert.Equal("synthetic-parent",record.CurrentActivityParentId);
        Assert.Equal("synthetic-operation",record.CurrentActivityOperation);
        Assert.Equal(activity.Source.Name,record.CurrentActivitySource);
        Assert.False(guard.UnexpectedRequest);
    }

    [Theory]
    [InlineData(2047,2047,false)]
    [InlineData(2048,2048,false)]
    [InlineData(2049,2045,true)]
    public void GivenScalarBoundary_WhenFormatted_ThenMarksTruncationWithinLimit(int length,int retained,bool truncated)
    {
        Assert.Equal(new string('x',retained)+(truncated?"...":""),OfflineRequestGuard.Bound(new string('x',length),2048));
        Assert.Equal("unavailable",OfflineRequestGuard.Bound(null,2048));
    }

    [Theory]
    [InlineData(8191,8191,false)]
    [InlineData(8192,8192,false)]
    [InlineData(8193,8189,true)]
    public void GivenStackBoundary_WhenFormatted_ThenMarksTruncationWithinLimit(int length,int retained,bool truncated)
        =>Assert.Equal(new string('x',retained)+(truncated?"...":""),OfflineRequestGuard.Bound(new string('x',length),8192));

    [Theory]
    [InlineData(15,15,0)]
    [InlineData(16,16,0)]
    [InlineData(17,16,1)]
    public void GivenDiagnosticRecordBoundary_WhenRecorded_ThenKeepsSeparateDropCounts(int attempted,int retained,int dropped)
    {
        using var guard=new OfflineRequestGuard(syntheticOnly:true);
        for(int i=0;i<attempted;i++)
            guard.RecordRequest("SyntheticRequestStart",1,Guid.Empty,Guid.Empty,["host"],["127.0.0.1"],null);
        for(int i=1;i<attempted;i++)guard.RecordPhase("host_starting");
        GuardDiagnostic snapshot=guard.Snapshot();
        Assert.Equal(retained,snapshot.Requests.Length);
        Assert.Equal(retained,snapshot.Phases.Length);
        Assert.Equal(dropped,snapshot.DroppedRequests);
        Assert.Equal(dropped,snapshot.DroppedPhases);
        // Collection bounds never suppress the original sticky guard decision.
        guard.RecordRequest("SyntheticRequestStart",1,Guid.Empty,Guid.Empty,["host"],["dc.services.visualstudio.com"],null);
        Assert.True(guard.UnexpectedRequest);
    }

    [Fact]
    public void GivenLongPayloadAndFailingDiagnosticOutput_WhenRecorded_ThenPreservesRawGuardEvidence()
    {
        using var guard=new OfflineRequestGuard(syntheticOnly:true);
        string payload=new('x',2049);
        guard.RecordRequest("SyntheticRequestStart",1,Guid.Empty,Guid.Empty,["host"],[payload],null);
        Assert.Equal("host="+payload,guard.LastRequest);
        Assert.Equal("host="+new string('x',2040)+"...",Assert.Single(guard.Snapshot().Requests).Payload);
        guard.WriteDiagnostics(_=>throw new InvalidOperationException("synthetic-output-failure"));
        Assert.True(guard.UnexpectedRequest);
        Assert.Equal("host="+payload,guard.LastRequest);
        Assert.Equal(1,guard.Snapshot().DiagnosticErrors);
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
        private const int RecordLimit=16;
        private const int ScalarLimit=2048;
        private const int StackLimit=8192;
        private readonly object diagnosticLock=new();
        private readonly List<RequestDiagnostic> requests=[];
        private readonly List<PhaseDiagnostic> phases=[];
        private readonly string guardId=Guid.NewGuid().ToString("N");
        private readonly long started=TimeProvider.System.GetTimestamp();
        private string phase="guard_created";
        private int droppedRequests;
        private int droppedPhases;
        private int diagnosticErrors;
        private int unexpectedRequest;
        internal string? LastRequest{get;private set;}
        internal bool UnexpectedRequest=>Volatile.Read(ref unexpectedRequest)!=0;

        // The listener sees System.Net.Http events from the whole process. A guard that classifies
        // synthetic events only must not also record real requests that other tests' hosts make
        // on background threads (telemetry exporters), or the synthetic assertions race with them.
        private readonly bool ignoreProcessEvents;

        internal OfflineRequestGuard(bool syntheticOnly=false)
        {
            ignoreProcessEvents=syntheticOnly;
            RecordPhase("guard_created");
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if(eventSource.Name=="System.Net.Http")EnableEvents(eventSource,EventLevel.Informational);
        }
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if(!ignoreProcessEvents&&eventData.EventSource.Name=="System.Net.Http"&&eventData.EventId==1)
                RecordRequest(eventData.EventName,eventData.EventId,eventData.ActivityId,eventData.RelatedActivityId,
                    eventData.PayloadNames,eventData.Payload,Activity.Current);
        }

        internal void RecordRequest(string? eventName,int eventId,Guid activityId,Guid relatedActivityId,
            IReadOnlyList<string>? payloadNames,IReadOnlyList<object?>? payload,Activity? activity)
        {
            string request=string.Join(";",(payloadNames??[]).Zip(payload??[],(name,value)=>$"{name}={value}"));
            LastRequest=request;
            if(!(payload?.Any(value=>value?.ToString()?.Contains("127.0.0.1",StringComparison.Ordinal)==true)??false))
                Interlocked.Exchange(ref unexpectedRequest,1);
            try
            {
                lock(diagnosticLock)
                {
                    if(requests.Count==RecordLimit){droppedRequests++;return;}
                    requests.Add(new RequestDiagnostic(guardId,Bound(eventName,ScalarLimit),eventId,
                        activityId,relatedActivityId,Environment.CurrentManagedThreadId,Elapsed(),phase,
                        Bound(request,ScalarLimit),Bound(activity?.Id,ScalarLimit),Bound(activity?.ParentId,ScalarLimit),
                        Bound(activity?.OperationName,ScalarLimit),Bound(activity?.Source.Name,ScalarLimit),
                        Bound(Environment.StackTrace,StackLimit)));
                }
            }
            catch(Exception){Interlocked.Increment(ref diagnosticErrors);}
        }

        internal void RecordPhase(string name,int? exitCode=null)
        {
            try
            {
                lock(diagnosticLock)
                {
                    phase=Bound(name,ScalarLimit);
                    if(phases.Count==RecordLimit){droppedPhases++;return;}
                    phases.Add(new PhaseDiagnostic(guardId,phase,Environment.ProcessId,
                        TimeProvider.System.GetUtcNow(),TimeProvider.System.GetTimestamp(),Elapsed(),exitCode));
                }
            }
            catch(Exception){Interlocked.Increment(ref diagnosticErrors);}
        }

        internal GuardDiagnostic Snapshot()
        {
            lock(diagnosticLock)
                return new GuardDiagnostic("diagnostic_only",guardId,
                    "Callback stacks and Activity values are correlation evidence and may not identify the issuing provider/host.",
                    [..requests],[..phases],droppedRequests,droppedPhases,Volatile.Read(ref diagnosticErrors));
        }

        internal void WriteDiagnostics(Action<string> write)
        {
            try{write(JsonSerializer.Serialize(Snapshot()));}
            catch(Exception){Interlocked.Increment(ref diagnosticErrors);}
        }

        internal static string Bound(string? value,int cap)
            =>value is null?"unavailable":value.Length<=cap?value:value[..(cap-3)]+"...";
        private double Elapsed()=>TimeProvider.System.GetElapsedTime(started).TotalMilliseconds;
    }

    private sealed record RequestDiagnostic(string GuardId,string EventName,int EventId,Guid ActivityId,
        Guid RelatedActivityId,int ManagedThreadId,double ElapsedMilliseconds,string Phase,string Payload,
        string CurrentActivityId,string CurrentActivityParentId,string CurrentActivityOperation,
        string CurrentActivitySource,[property:System.Text.Json.Serialization.JsonPropertyName("callback_stack")]string CallbackStack);
    private sealed record PhaseDiagnostic(string GuardId,string Phase,int ProcessId,DateTimeOffset ObservedAtUtc,
        long MonotonicTimestamp,double ElapsedMilliseconds,int? ExitCode);
    private sealed record GuardDiagnostic(string Purpose,string GuardId,string AttributionLimit,
        RequestDiagnostic[] Requests,PhaseDiagnostic[] Phases,int DroppedRequests,int DroppedPhases,int DiagnosticErrors);

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
