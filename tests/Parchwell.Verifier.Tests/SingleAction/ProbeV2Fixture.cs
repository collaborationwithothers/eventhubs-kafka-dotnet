using System.Globalization;
using System.Text;
using System.Text.Json;
using Parchwell.Verifier.VerifyRun.SingleAction;

namespace Parchwell.Verifier.Tests.SingleAction;

internal static class ProbeV2Fixture
{
    internal const string Source = "1111111111111111111111111111111111111111";
    internal static ProbeV2Options Options(string item = "B08",string configuration = "C0")
    {
        string job = $"run1-{item.ToLowerInvariant()}-{configuration.ToLowerInvariant()}-judge";
        return new()
        {
            RunKind = "smoke",ScenarioId = item,ConfigurationId = configuration,RunId = "run1",SourceCommit = Source,JobName = job,PodName = job + "-abc",PodUid = "judge-uid",
            NamespaceName = "synthetic-ns",ServiceAccount = "verifier",WorkloadIdentityClientId = "44444444-4444-4444-4444-444444444444",ScenarioRoot = "/app",InputRoot = "/run/probe-inputs",Stage = "judge",
            BlobServiceUri = "https://syntheticstore.blob.core.windows.net/",VerifierContainer = "verifier",
            LoadGenImage = "registry.test/parchwell/loadgen@sha256:" + new string('a',64),VerifierImage = "registry.test/parchwell/verifier@sha256:" + new string('b',64),
            Limits = new() { PostTerminalStatisticsSeconds = 20, CleanupSeconds = 10, HostCleanupSeconds = 10, IoSeconds = 10, PollSliceMs = 100, MetadataTimeoutMs = 60000, MaxCaptureBytes = 2097152, MaxRegistrationBytes = 65536, MaxPodBytes = 65536, MaxStatisticsReportBytes = 262144, MaxStatisticsReports = 1, MaxClientErrors = 8, MaxErrorBytes = 1024, MaxDiagnostics = 100, MaxDiagnosticBytes = 2048, CallbackQueueCapacity = 128, MaxSettingsKeys = 128, MaxSettingsBytes = 16384, MaxNonmatchingRecords = 4096, MaxInputDepth = 32, MaxKeyBytes = 4096, MaxValueBytes = 4096, MaxClientLogBytes = 16384 },
            JudgeLimits = new() { ProcessLifetimeSeconds = 90, IoSeconds = 10, CleanupSeconds = 10, MaxCandidateBytes = 16777216, MaxJudgeReceiptBytes = 65536, MaxTotalInputBytes = 268435456, MaxInputFiles = 256, MaxBrokerEntries = 64, MaxRoleScopeBytes = 131072, MaxEffectiveScopeBytes = 32768 },
            ScenarioLoadOptions = new() { MaxDocumentCharacters = 1048576,MaxNestingDepth = 32,MaxScalarCharacters = 65536,MaxParserEvents = 100000 }
        };
    }
    internal static Dictionary<(string Configuration,string Stage),ProbeV2Evidence> Evidence(ProbeV2Options options) =>
        ProbeV2Matrix.Dependencies(options.ScenarioId,options.ConfigurationId).ToDictionary(key => key,key => Evidence(options,key.Configuration,key.Stage));
    internal static ProbeV2Evidence Evidence(ProbeV2Options options,string configuration,string stage)
    {
        var matrix = ProbeV2Matrix.For(options.ScenarioId,configuration,stage);
        string job = ProbeV2Matrix.Job(options.RunId,options.ScenarioId,configuration,stage), pod = job + "-abc";
        var registration = new ProbeV2Registration(2,"single-action-v2",true,options.RunId,options.ScenarioId,configuration,stage,options.SourceCommit,job,pod,job + "-uid",matrix.Account);
        var rows = Enumerable.Range(1,5).Select(ordinal => Row(options,configuration,stage,ordinal,pod)).ToArray();
        var capture = new ProbeV2CaptureFile(2,"single-action-v2",true,options.RunId,options.ScenarioId,configuration,stage,options.SourceCommit,job,pod,registration.PodUid,matrix.Account,"2.16.0","2.16.0",rows,0,true,true);
        return new(configuration,stage,registration,capture,new(2,job,pod,registration.PodUid,matrix.Account,"Job",job,options.ImageDigests()["loadgen"]),new string('a',64),[]);
    }
    internal static ProbeV2Repeat Row(ProbeV2Options options,string configuration,string stage,int ordinal,string? pod = null)
    {
        var matrix = ProbeV2Matrix.For(options.ScenarioId,configuration,stage);
        pod ??= ProbeV2Matrix.Job(options.RunId,options.ScenarioId,configuration,stage) + "-abc";
        string client = pod + "-r" + ordinal.ToString(CultureInfo.InvariantCulture);
        int partition = options.ScenarioId == "B08" ? configuration == "C0" ? 30 : 2 : 0;
        long offset = 100 * ordinal + (options.ScenarioId == "E01" && stage is "action" or "read" ? 10 : 0);
        byte[]? value = matrix.Value(options,ordinal,stage,configuration);
        var requested = new ProbeV2Requested(matrix.Topic,matrix.Action == "assign_consume" ? "explicit" : matrix.PartitionMode,
            matrix.Action == "produce_one" && matrix.PartitionMode == "automatic" ? null : partition,matrix.Action == "assign_consume" ? offset : null,
            Convert.ToBase64String(matrix.Key(options,ordinal,configuration)),value is null ? null : Convert.ToBase64String(value));
        var row = new ProbeV2Repeat(ordinal,"required",client,matrix.Action,true,10,1000,
            new Dictionary<string,string>(StringComparer.Ordinal) { ["client.id"] = client,["synthetic"] = "settings" },[],options.ScenarioId == "B08" ? Enumerable.Range(0,32).ToArray() : null,requested,
            matrix.Action == "produce_one" ? new(matrix.Topic,partition,offset,"Persisted",null,10,1) : null,
            new(matrix.Action == "assign_consume" ? 1 : 0,0,matrix.Action == "assign_consume" ? new(matrix.Topic,partition,offset,requested.KeyBase64,requested.ValueBase64,10,1) : null),
            "success",null,[],[Statistic(client,matrix.Action,1)],[]);
        if (options.ScenarioId == "F06" && configuration is "C2" or "C4")
        {
            if (stage == "read") return new(ordinal,"not_applicable_no_delivery",null,matrix.Action,false,0,null,new Dictionary<string,string>(StringComparer.Ordinal),[],null,null,null,new(0,0,null),null,null,[],[],[]);
            return row with { Result = "error",Error = new(29,false,"broker","synthetic",10,1),Delivery = null };
        }
        if (options.ScenarioId == "F06" && configuration is "C10" or "C11") return row with { Result = "no_outcome",ElapsedMs = 120000,TerminalUnixMs = 120000,ReadObservation = new(0,0,null),Statistics = [Statistic(client,matrix.Action,121,120001)] };
        return row;
    }
    internal static ProbeV2Statistics Statistic(string client,string action,long time,long observed = 11,string brokers = "{}",long ts = 1)
    {
        string kind = action == "produce_one" ? "producer" : "consumer";
        return new(3,observed,Convert.ToBase64String(Encoding.UTF8.GetBytes($$"""{"name":"{{client}}#{{kind}}-1","client_id":"{{client}}","type":"{{kind}}","time":{{time}},"ts":{{ts}},"brokers":{{brokers}}}""")));
    }
    internal static ProbeV2Scope Scope(ProbeV2Options options)
    {
        const string resourceGroup = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/synthetic-rg";
        string ns = resourceGroup + "/providers/Microsoft.EventHub/namespaces/" + options.NamespaceName;
        string storage = resourceGroup + "/providers/Microsoft.Storage/storageAccounts/syntheticstore/blobServices/default/containers/";
        var accounts = new Dictionary<string,ProbeV2Account>(StringComparer.Ordinal)
        {
            ["loadgen"] = new("11111111-1111-1111-1111-111111111111","11111111-1111-1111-1111-111111111111"),
            ["probe"] = new("22222222-2222-2222-2222-222222222222","22222222-2222-2222-2222-222222222222"),
            ["enrichment-consumer"] = new("33333333-3333-3333-3333-333333333333","33333333-3333-3333-3333-333333333333"),
            ["verifier"] = new(options.WorkloadIdentityClientId,options.WorkloadIdentityClientId)
        };
        var topics = new Dictionary<string,string>(StringComparer.Ordinal) { ["document-events"] = ns + "/eventhubs/document-events",["document-state"] = ns + "/eventhubs/document-state",["document-events-dlq"] = ns + "/eventhubs/document-events-dlq" };
        var roles = new Dictionary<string,ProbeV2Assignment>(StringComparer.Ordinal);
        void Add(string key,string account,string role,string scope) => roles.Add(key,new(account,role,scope,accounts[account].PrincipalId));
        Add("probe_events_receiver","probe","Azure Event Hubs Data Receiver",topics["document-events"]);
        Add("probe_single_action_contributor","probe","Storage Blob Data Contributor",storage + "single-action");
        Add("verifier_single_action_reader","verifier","Storage Blob Data Reader",storage + "single-action");
        Add("loadgen_events_sender","loadgen","Azure Event Hubs Data Sender",topics["document-events"]);
        Add("loadgen_ledger_contributor","loadgen","Storage Blob Data Contributor",storage + "ledger");
        Add("enrichment_events_receiver","enrichment-consumer","Azure Event Hubs Data Receiver",topics["document-events"]);
        Add("enrichment_state_sender","enrichment-consumer","Azure Event Hubs Data Sender",topics["document-state"]);
        Add("enrichment_dlq_sender","enrichment-consumer","Azure Event Hubs Data Sender",topics["document-events-dlq"]);
        Add("enrichment_claim_check_contributor","enrichment-consumer","Storage Blob Data Contributor",storage + "claim-check");
        Add("verifier_events_receiver","verifier","Azure Event Hubs Data Receiver",topics["document-events"]);
        Add("verifier_ledger_reader","verifier","Storage Blob Data Reader",storage + "ledger");
        Add("verifier_results_contributor","verifier","Storage Blob Data Contributor",storage + "verifier");
        bool wide = options.ScenarioId != "B08";
        if (wide) { Add("probe_namespace_sender","probe","Azure Event Hubs Data Sender",ns); Add("probe_namespace_receiver","probe","Azure Event Hubs Data Receiver",ns); }
        return new(2,options.RunId,options.SourceCommit,options.NamespaceName,new(ns,topics,accounts,roles,wide),
            new(2,ns,new(StringComparer.Ordinal) { ["loadgen"] = new(accounts["loadgen"].PrincipalId,true),["enrichment-consumer"] = new(accounts["enrichment-consumer"].PrincipalId,true) }),new string('a',64));
    }
    internal static Dictionary<(string Configuration,string Stage),ProbeV2RepeatProjection[]> Check(ProbeV2Options options,Dictionary<(string Configuration,string Stage),ProbeV2Evidence> evidence) =>
        evidence.ToDictionary(x => x.Key,x => x.Value.Capture!.Repeats.Select(row =>
        {
            var projection = SingleActionRepeatJudgement.JudgeV2Slot(options,x.Value,row,row.Settings,[]);
            string[] global = SingleActionConfigurationJudgement.V2EvidenceProblems(options,x.Value);
            return projection with { Reasons = [..projection.Reasons,..global],Status = projection.Status == "invalid" || global.Contains("v2_identity",StringComparer.Ordinal) || global.Contains("v2_wire",StringComparer.Ordinal) ? "invalid" : global.Length != 0 ? "incomplete" : projection.Status };
        }).ToArray());
    internal static ProbeV2Candidate Candidate(string item = "B08",string configuration = "C0")
    {
        var options = Options(item,configuration); var evidence = Evidence(options); var previous = new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal);
        if (ProbeV2Matrix.Configurations(item).Last() == configuration)
            foreach (string prior in ProbeV2Matrix.Configurations(item).TakeWhile(x => x != configuration)) previous.Add(prior,Candidate(item,prior));
        return SingleActionConfigurationJudgement.V2Candidate(options,evidence,Check(options,evidence),Scope(options),previous);
    }
}
