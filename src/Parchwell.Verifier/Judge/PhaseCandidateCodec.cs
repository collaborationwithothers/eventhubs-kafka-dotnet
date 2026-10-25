using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.Judge;

/// <summary>Writes and validates the closed, bounded public phase-candidate projection.</summary>
internal static class PhaseCandidateCodec
{
    internal const long MaximumBytes = 16_777_216;
    internal static byte[] Write(PhaseCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        byte[] bytes=DeploymentJson.Write(MaximumBytes, w =>
        {
            w.WriteStartObject(); w.WriteNumber("schema_version",candidate.SchemaVersion); w.WriteBoolean("synthetic",candidate.Synthetic);
            w.WriteString("scenario_id",candidate.ScenarioId); w.WriteString("configuration_id",candidate.ConfigurationId); w.WriteString("run_id",candidate.RunId); w.WriteString("phase",candidate.Phase); w.WriteString("source_commit",candidate.SourceCommit); w.WriteString("status",candidate.Status);
            if(candidate.Gate is null)w.WriteNull("gate");else w.WriteString("gate",candidate.Gate);
            WriteStringMap(w,"versions",candidate.Versions); WriteStringMap(w,"image_digests",candidate.ImageDigests);
            WriteObjectMap(w,"timings",candidate.Timings); WriteObjectMap(w,"diagnostics",candidate.Diagnostics);
            w.WritePropertyName("repeats");w.WriteStartArray();foreach(var r in candidate.Repeats)WriteRepeat(w,r);w.WriteEndArray();
            if(candidate.Counts is not null)WriteCounts(w,candidate.Counts);
            w.WritePropertyName("reasons");w.WriteStartArray();foreach(string reason in candidate.Reasons)w.WriteStringValue(reason);w.WriteEndArray();w.WriteEndObject();
        });
        using JsonDocument validated=Read(bytes);
        return bytes;
    }

    internal static JsonDocument Read(ReadOnlyMemory<byte> bytes)
    {
        if(bytes.Length>MaximumBytes)throw new InputLimitExceededException("Candidate exceeds its output byte limit.");
        var d=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=32});
        string[] allowed=["schema_version","synthetic","scenario_id","configuration_id","run_id","phase","source_commit","status","gate","versions","image_digests","timings","diagnostics","repeats","counts","reasons"];
        try
        {
            if(d.RootElement.ValueKind!=JsonValueKind.Object||d.RootElement.EnumerateObject().Any(p=>!allowed.Contains(p.Name,StringComparer.Ordinal))||allowed.Where(n=>n!="counts").Any(n=>!d.RootElement.TryGetProperty(n,out _)))throw new FormatException("Candidate has missing or unknown fields.");
            CheckDuplicates(d.RootElement);
            CheckNoPrivateKeys(d.RootElement);
            ValidateNested(d.RootElement);
            return d;
        }
        catch { d.Dispose(); throw; }
    }
    private static void ValidateNested(JsonElement root)
    {
        foreach(string name in new[]{"schema_version","synthetic","scenario_id","configuration_id","run_id","phase","source_commit","status"})
            if(root.GetProperty(name).ValueKind is JsonValueKind.Null or JsonValueKind.Object or JsonValueKind.Array)throw new FormatException("Candidate scalar has the wrong kind.");
        foreach(string name in new[]{"scenario_id","configuration_id","run_id","phase","source_commit","status"})
            if(root.GetProperty(name).ValueKind!=JsonValueKind.String||root.GetProperty(name).GetString()!.Length>256)throw new FormatException("Candidate text scalar is invalid.");
        if(root.GetProperty("schema_version").ValueKind!=JsonValueKind.Number||root.GetProperty("synthetic").ValueKind!=JsonValueKind.True||
            !root.GetProperty("schema_version").TryGetInt32(out int schema)||schema!=1)throw new FormatException("Candidate header is invalid.");
        if(root.GetProperty("scenario_id").GetString()!="A01"||root.GetProperty("phase").GetString() is not ("probe" or "C0" or "C3")||
            root.GetProperty("configuration_id").GetString()!=PhaseIdentity.ConfigurationFor(root.GetProperty("phase").GetString()!)||
            root.GetProperty("source_commit").GetString()!.Length!=40||root.GetProperty("source_commit").GetString()!.Any(c=>!(c is >= '0' and <= '9' or >= 'a' and <= 'f'))||
            root.GetProperty("status").GetString() is not ("complete_valid" or "invalid" or "incomplete"))throw new FormatException("Candidate identity or status is invalid.");
        if(root.GetProperty("gate").ValueKind is not (JsonValueKind.Null or JsonValueKind.String))throw new FormatException("Candidate gate has the wrong kind.");
        if(root.GetProperty("gate").ValueKind==JsonValueKind.String&&root.GetProperty("gate").GetString() is not ("pass" or "untestable_at_build_scale" or "inconclusive"))throw new FormatException("Candidate gate is invalid.");
        Exact(root.GetProperty("versions"),["verifier","workload_generator","confluent_kafka","librdkafka","dotnet"]);
        Exact(root.GetProperty("image_digests"),["loadgen","verifier"]);
        Exact(root.GetProperty("timings"),["worker_started_utc","judge_completed_utc","duration_seconds"]);
        Exact(root.GetProperty("diagnostics"),["error_codes","max_throttle_ms","request_timeout_increment","disconnect_increment","rtt_p99_ms"]);
        if(root.TryGetProperty("counts",out JsonElement candidateCounts))Exact(candidateCounts,["ok","loss","phantom","reported_failure","duplicate","reorder"]);
        if(root.GetProperty("repeats").ValueKind!=JsonValueKind.Array||root.GetProperty("reasons").ValueKind!=JsonValueKind.Array)throw new FormatException("Candidate repeats and reasons must be arrays.");
        foreach(var item in root.GetProperty("versions").EnumerateObject().Concat(root.GetProperty("image_digests").EnumerateObject()))
            if(item.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.String) || item.Value.ValueKind==JsonValueKind.String && item.Value.GetString()!.Length>256)throw new FormatException("Candidate string map contains an invalid scalar.");
        foreach(string imageName in new[]{"loadgen","verifier"})
        {
            JsonElement digest=root.GetProperty("image_digests").GetProperty(imageName);
            if(digest.ValueKind!=JsonValueKind.String||digest.GetString()!.Length!=71||!digest.GetString()!.StartsWith("sha256:",StringComparison.Ordinal)||digest.GetString()![7..].Any(c=>!(c is >= '0' and <= '9' or >= 'a' and <= 'f')))throw new FormatException("Candidate image digest is invalid.");
        }
        if(root.TryGetProperty("counts",out candidateCounts)&&candidateCounts.EnumerateObject().Any(p=>p.Value.ValueKind!=JsonValueKind.Number||!p.Value.TryGetInt64(out long n)||n<0))throw new FormatException("Candidate counts are invalid.");
        JsonElement timings=root.GetProperty("timings");
        if(timings.GetProperty("worker_started_utc").ValueKind is not (JsonValueKind.Null or JsonValueKind.String)||timings.GetProperty("judge_completed_utc").ValueKind is not (JsonValueKind.Null or JsonValueKind.String)||timings.GetProperty("duration_seconds").ValueKind is not (JsonValueKind.Null or JsonValueKind.Number))throw new FormatException("Candidate timing has the wrong kind.");
        JsonElement diagnostics=root.GetProperty("diagnostics");
        if(diagnostics.GetProperty("error_codes").ValueKind!=JsonValueKind.Array||diagnostics.GetProperty("error_codes").EnumerateArray().Any(x=>x.ValueKind!=JsonValueKind.String||x.GetString()!.Length>256)||diagnostics.EnumerateObject().Where(p=>p.Name!="error_codes").Any(p=>p.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Number)))throw new FormatException("Candidate diagnostics have the wrong kind.");
        if(root.GetProperty("reasons").EnumerateArray().Any(x=>x.ValueKind!=JsonValueKind.String||x.GetString()!.Length>256))throw new FormatException("Candidate reasons are invalid.");
        foreach(var repeat in root.GetProperty("repeats").EnumerateArray())
        {
            string[] repeatFields=["repeat","status","reasons","checks","identity","probe_observation","workload_observation"];
            if(repeat.ValueKind!=JsonValueKind.Object||repeat.EnumerateObject().Any(p=>!repeatFields.Contains(p.Name,StringComparer.Ordinal)&&p.Name!="counts")||repeatFields.Any(n=>!repeat.TryGetProperty(n,out _)))throw new FormatException("Candidate repeat has missing or unknown fields.");
            Exact(repeat.GetProperty("checks"),["j1_repeat_slots","j2_publication_unaccepted","j3_verifier_receipt","j4_producer_job_binding","j5_registered_producer_count","j6_broker_inventory_coverage","j8_probe_inconclusive"]);
            if(repeat.GetProperty("probe_observation").ValueKind!=JsonValueKind.Null)
                Exact(repeat.GetProperty("probe_observation"),["usable","qualifying","broker","report_index","window_count","window_maximum"]);
            if(repeat.GetProperty("workload_observation").ValueKind!=JsonValueKind.Null)
            {
                JsonElement workload=repeat.GetProperty("workload_observation");
                Exact(workload,["synthetic","planned_attempts","invoked_attempts","terminal_observations","fixed_profile_complete","first_unexecuted_ordinal","stages"]);
                JsonElement stages=workload.GetProperty("stages");
                if(stages.ValueKind is not (JsonValueKind.Null or JsonValueKind.Array))throw new FormatException("Candidate workload stages have the wrong kind.");
                if(stages.ValueKind==JsonValueKind.Array)foreach(JsonElement stage in stages.EnumerateArray())
                {
                    Exact(stage,["id","offered_attempts_per_second","duration_seconds","invoked_attempts","delivered_observations","failed_observations"]);
                    if(stage.GetProperty("id").ValueKind!=JsonValueKind.String||stage.GetProperty("id").GetString()!.Length>256||!NonnegativeInt64(stage.GetProperty("offered_attempts_per_second"))||!NonnegativeInt64(stage.GetProperty("duration_seconds")))throw new FormatException("Candidate workload stage has invalid scalar fields.");
                    foreach(string count in new[]{"invoked_attempts","delivered_observations","failed_observations"})if(stage.GetProperty(count).ValueKind is not (JsonValueKind.Null or JsonValueKind.Number)||stage.GetProperty(count).ValueKind==JsonValueKind.Number&&(!stage.GetProperty(count).TryGetInt64(out long n)||n<0))throw new FormatException("Candidate workload count is invalid.");
                }
                if(workload.GetProperty("synthetic").ValueKind!=JsonValueKind.True||workload.GetProperty("fixed_profile_complete").ValueKind is not (JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False))throw new FormatException("Candidate workload flags are invalid.");
                foreach(string count in new[]{"planned_attempts","invoked_attempts","terminal_observations","first_unexecuted_ordinal"})if(workload.GetProperty(count).ValueKind is not (JsonValueKind.Null or JsonValueKind.Number)||workload.GetProperty(count).ValueKind==JsonValueKind.Number&&(!workload.GetProperty(count).TryGetInt64(out long n)||n<0))throw new FormatException("Candidate workload count is invalid.");
            }
            if(repeat.TryGetProperty("counts",out JsonElement repeatCounts))Exact(repeatCounts,["ok","loss","phantom","reported_failure","duplicate","reorder"]);
            if(!repeat.GetProperty("repeat").TryGetInt32(out int ordinal)||ordinal is <1 or >5||repeat.GetProperty("status").ValueKind!=JsonValueKind.String||repeat.GetProperty("reasons").ValueKind!=JsonValueKind.Array||repeat.GetProperty("reasons").EnumerateArray().Any(x=>x.ValueKind!=JsonValueKind.String))throw new FormatException("Candidate repeat has invalid scalar fields.");
            if(repeat.GetProperty("status").GetString() is not ("complete_valid" or "invalid" or "incomplete"))throw new FormatException("Candidate repeat status is invalid.");
            if(repeat.GetProperty("checks").EnumerateObject().Any(p=>p.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))throw new FormatException("Candidate repeat checks must be booleans.");
            if(repeat.GetProperty("identity").ValueKind is not (JsonValueKind.Null or JsonValueKind.String))throw new FormatException("Candidate identity has the wrong kind.");
            if(repeat.TryGetProperty("counts",out repeatCounts)&&repeatCounts.EnumerateObject().Any(p=>p.Value.ValueKind!=JsonValueKind.Number||!p.Value.TryGetInt64(out long n)||n<0))throw new FormatException("Candidate repeat counts are invalid.");
            if(repeat.GetProperty("probe_observation").ValueKind==JsonValueKind.Object)
            {
                JsonElement observation=repeat.GetProperty("probe_observation");
                if(observation.GetProperty("usable").ValueKind is not (JsonValueKind.True or JsonValueKind.False)||observation.GetProperty("qualifying").ValueKind is not (JsonValueKind.True or JsonValueKind.False)||observation.GetProperty("broker").ValueKind is not (JsonValueKind.Null or JsonValueKind.String))throw new FormatException("Candidate probe observation has invalid kinds.");
                foreach(string key in new[]{"report_index","window_count","window_maximum"})if(observation.GetProperty(key).ValueKind is not (JsonValueKind.Null or JsonValueKind.Number)||observation.GetProperty(key).ValueKind==JsonValueKind.Number&&(!observation.GetProperty(key).TryGetInt64(out long n)||n<0))throw new FormatException("Candidate probe observation has invalid numeric values.");
            }
        }
    }
    private static bool NonnegativeInt64(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) && number >= 0;
    private static void Exact(JsonElement e,string[] fields,bool nullable=false)
    {
        if(nullable&&e.ValueKind==JsonValueKind.Null)return;
        if(e.ValueKind!=JsonValueKind.Object)throw new FormatException("Candidate nested value has the wrong kind.");
        var names=e.EnumerateObject().Select(p=>p.Name).ToArray();
        if(names.Distinct(StringComparer.Ordinal).Count()!=names.Length||names.Length!=fields.Length||fields.Any(n=>!names.Contains(n,StringComparer.Ordinal))||names.Any(n=>!fields.Contains(n,StringComparer.Ordinal)))throw new FormatException("Candidate nested object has missing, duplicate or unknown fields.");
    }
    private static void CheckNoPrivateKeys(JsonElement e)
    {
        if(e.ValueKind==JsonValueKind.Object)foreach(var p in e.EnumerateObject()){if(p.Name is "cost" or "costs" or "reservation" or "reconciliation" or "raw" or "private_evidence")throw new FormatException("Candidate contains a forbidden raw or financial field.");CheckNoPrivateKeys(p.Value);}
        else if(e.ValueKind==JsonValueKind.Array)foreach(var x in e.EnumerateArray())CheckNoPrivateKeys(x);
    }
    private static void CheckDuplicates(JsonElement e)
    {
        if(e.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(StringComparer.Ordinal);foreach(var p in e.EnumerateObject()){if(!names.Add(p.Name))throw new FormatException("Candidate contains a duplicate property.");CheckDuplicates(p.Value);}}
        else if(e.ValueKind==JsonValueKind.Array)foreach(var item in e.EnumerateArray())CheckDuplicates(item);
    }
    private static void WriteStringMap(Utf8JsonWriter w,string name,IReadOnlyDictionary<string,string?> values){w.WritePropertyName(name);w.WriteStartObject();foreach(var p in values.OrderBy(x=>x.Key,StringComparer.Ordinal)){if(p.Value is null)w.WriteNull(p.Key);else w.WriteString(p.Key,p.Value);}w.WriteEndObject();}
    private static void WriteObjectMap(Utf8JsonWriter w,string name,IReadOnlyDictionary<string,object?> values){w.WritePropertyName(name);w.WriteStartObject();foreach(var p in values.OrderBy(x=>x.Key,StringComparer.Ordinal)){w.WritePropertyName(p.Key);JsonSerializer.Serialize(w,p.Value);}w.WriteEndObject();}
    private static void WriteRepeat(Utf8JsonWriter w,PhaseRepeatResult r){w.WriteStartObject();w.WriteNumber("repeat",r.Repeat);w.WriteString("status",r.Status);w.WritePropertyName("reasons");w.WriteStartArray();foreach(var x in r.Reasons)w.WriteStringValue(x);w.WriteEndArray();w.WritePropertyName("checks");w.WriteStartObject();w.WriteBoolean("j1_repeat_slots",r.Checks.J1);w.WriteBoolean("j2_publication_unaccepted",r.Checks.J2);w.WriteBoolean("j3_verifier_receipt",r.Checks.J3);w.WriteBoolean("j4_producer_job_binding",r.Checks.J4);w.WriteBoolean("j5_registered_producer_count",r.Checks.J5);w.WriteBoolean("j6_broker_inventory_coverage",r.Checks.J6);w.WriteBoolean("j8_probe_inconclusive",r.Checks.J8);w.WriteEndObject();if(r.Identity is null)w.WriteNull("identity");else w.WriteString("identity",r.Identity);if(r.ProbeObservation is null)w.WriteNull("probe_observation");else{w.WritePropertyName("probe_observation");w.WriteStartObject();w.WriteBoolean("usable",r.ProbeObservation.Usable);w.WriteBoolean("qualifying",r.ProbeObservation.Qualifying);WriteNullable(w,"broker",ProjectBroker(r.ProbeObservation.Broker));WriteNullable(w,"report_index",r.ProbeObservation.ReportIndex);WriteNullable(w,"window_count",r.ProbeObservation.WindowCount);WriteNullable(w,"window_maximum",r.ProbeObservation.WindowMaximum);w.WriteEndObject();}if(r.WorkloadObservation is null)w.WriteNull("workload_observation");else WriteWorkload(w,r.WorkloadObservation);if(r.Counts is not null)WriteCounts(w,r.Counts);w.WriteEndObject();}
    private static void WriteWorkload(Utf8JsonWriter w,WorkloadObservation value){w.WritePropertyName("workload_observation");w.WriteStartObject();w.WriteBoolean("synthetic",value.Synthetic);WriteNullable(w,"planned_attempts",value.PlannedAttempts);WriteNullable(w,"invoked_attempts",value.InvokedAttempts);WriteNullable(w,"terminal_observations",value.TerminalObservations);if(value.FixedProfileComplete is bool complete)w.WriteBoolean("fixed_profile_complete",complete);else w.WriteNull("fixed_profile_complete");WriteNullable(w,"first_unexecuted_ordinal",value.FirstUnexecutedOrdinal);if(value.Stages is null)w.WriteNull("stages");else{w.WritePropertyName("stages");w.WriteStartArray();foreach(var stage in value.Stages){w.WriteStartObject();w.WriteString("id",stage.Id);w.WriteNumber("offered_attempts_per_second",stage.OfferedAttemptsPerSecond);w.WriteNumber("duration_seconds",stage.DurationSeconds);WriteNullable(w,"invoked_attempts",stage.InvokedAttempts);WriteNullable(w,"delivered_observations",stage.DeliveredObservations);WriteNullable(w,"failed_observations",stage.FailedObservations);w.WriteEndObject();}w.WriteEndArray();}w.WriteEndObject();}
    private static string? ProjectBroker(string? broker)=>broker is null?null:"broker-"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(broker)))[..16];
    private static void WriteCounts(Utf8JsonWriter w,OutcomeCounts c){w.WritePropertyName("counts");w.WriteStartObject();w.WriteNumber("ok",c.Ok);w.WriteNumber("loss",c.Loss);w.WriteNumber("phantom",c.Phantom);w.WriteNumber("reported_failure",c.ReportedFailure);w.WriteNumber("duplicate",c.Duplicate);w.WriteNumber("reorder",c.Reorder);w.WriteEndObject();}
    private static void WriteNullable(Utf8JsonWriter w,string name,string? x){if(x is null)w.WriteNull(name);else w.WriteString(name,x);}
    private static void WriteNullable(Utf8JsonWriter w,string name,long? x){if(x is null)w.WriteNull(name);else w.WriteNumber(name,x.Value);}
}
