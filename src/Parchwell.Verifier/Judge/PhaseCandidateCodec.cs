using System.Text.Json;
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
        return DeploymentJson.Write(MaximumBytes, w =>
        {
            w.WriteStartObject(); w.WriteNumber("schema_version",candidate.SchemaVersion); w.WriteBoolean("synthetic",candidate.Synthetic);
            w.WriteString("scenario_id",candidate.ScenarioId); w.WriteString("configuration_id",candidate.ConfigurationId); w.WriteString("run_id",candidate.RunId); w.WriteString("phase",candidate.Phase); w.WriteString("source_commit",candidate.SourceCommit); w.WriteString("status",candidate.Status);
            if(candidate.Gate is null)w.WriteNull("gate");else w.WriteString("gate",candidate.Gate);
            WriteStringMap(w,"versions",candidate.Versions); WriteStringMap(w,"image_digests",candidate.ImageDigests);
            WriteObjectMap(w,"timings",candidate.Timings); WriteObjectMap(w,"diagnostics",candidate.Diagnostics);
            w.WritePropertyName("repeats");w.WriteStartArray();foreach(var r in candidate.Repeats)WriteRepeat(w,r);w.WriteEndArray();
            if(candidate.Counts is null)w.WriteNull("counts");else WriteCounts(w,candidate.Counts);
            w.WritePropertyName("reasons");w.WriteStartArray();foreach(string reason in candidate.Reasons)w.WriteStringValue(reason);w.WriteEndArray();w.WriteEndObject();
        });
    }

    internal static JsonDocument Read(ReadOnlyMemory<byte> bytes)
    {
        if(bytes.Length>MaximumBytes)throw new InputLimitExceededException("Candidate exceeds its output byte limit.");
        var d=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=32});
        string[] allowed=["schema_version","synthetic","scenario_id","configuration_id","run_id","phase","source_commit","status","gate","versions","image_digests","timings","diagnostics","repeats","counts","reasons"];
        if(d.RootElement.ValueKind!=JsonValueKind.Object||d.RootElement.EnumerateObject().Any(p=>!allowed.Contains(p.Name,StringComparer.Ordinal))||allowed.Any(n=>!d.RootElement.TryGetProperty(n,out _))){d.Dispose();throw new FormatException("Candidate has missing or unknown fields.");}
        CheckNoPrivateKeys(d.RootElement);return d;
    }
    private static void CheckNoPrivateKeys(JsonElement e)
    {
        if(e.ValueKind==JsonValueKind.Object)foreach(var p in e.EnumerateObject()){if(p.Name is "cost" or "costs" or "reservation" or "reconciliation" or "raw" or "private_evidence")throw new FormatException("Candidate contains a forbidden raw or financial field.");CheckNoPrivateKeys(p.Value);}
        else if(e.ValueKind==JsonValueKind.Array)foreach(var x in e.EnumerateArray())CheckNoPrivateKeys(x);
    }
    private static void WriteStringMap(Utf8JsonWriter w,string name,IReadOnlyDictionary<string,string?> values){w.WritePropertyName(name);w.WriteStartObject();foreach(var p in values.OrderBy(x=>x.Key,StringComparer.Ordinal)){if(p.Value is null)w.WriteNull(p.Key);else w.WriteString(p.Key,p.Value);}w.WriteEndObject();}
    private static void WriteObjectMap(Utf8JsonWriter w,string name,IReadOnlyDictionary<string,object?> values){w.WritePropertyName(name);w.WriteStartObject();foreach(var p in values.OrderBy(x=>x.Key,StringComparer.Ordinal)){w.WritePropertyName(p.Key);JsonSerializer.Serialize(w,p.Value);}w.WriteEndObject();}
    private static void WriteRepeat(Utf8JsonWriter w,PhaseRepeatResult r){w.WriteStartObject();w.WriteNumber("repeat",r.Repeat);w.WriteString("status",r.Status);w.WritePropertyName("reasons");w.WriteStartArray();foreach(var x in r.Reasons)w.WriteStringValue(x);w.WriteEndArray();w.WritePropertyName("checks");w.WriteStartObject();w.WriteBoolean("j1_repeat_slots",r.Checks.J1);w.WriteBoolean("j2_publication_unaccepted",r.Checks.J2);w.WriteBoolean("j3_verifier_receipt",r.Checks.J3);w.WriteBoolean("j4_producer_job_binding",r.Checks.J4);w.WriteBoolean("j5_registered_producer_count",r.Checks.J5);w.WriteBoolean("j6_broker_inventory_coverage",r.Checks.J6);w.WriteBoolean("j8_probe_inconclusive",r.Checks.J8);w.WriteEndObject();if(r.Identity is null)w.WriteNull("identity");else w.WriteString("identity",r.Identity);if(r.ProbeObservation is null)w.WriteNull("probe_observation");else{w.WritePropertyName("probe_observation");w.WriteStartObject();w.WriteBoolean("usable",r.ProbeObservation.Usable);w.WriteBoolean("qualifying",r.ProbeObservation.Qualifying);WriteNullable(w,"broker",r.ProbeObservation.Broker);WriteNullable(w,"report_index",r.ProbeObservation.ReportIndex);WriteNullable(w,"window_count",r.ProbeObservation.WindowCount);WriteNullable(w,"window_maximum",r.ProbeObservation.WindowMaximum);w.WriteEndObject();}if(r.Counts is null)w.WriteNull("counts");else WriteCounts(w,r.Counts);w.WriteEndObject();}
    private static void WriteCounts(Utf8JsonWriter w,OutcomeCounts c){w.WritePropertyName("counts");w.WriteStartObject();w.WriteNumber("ok",c.Ok);w.WriteNumber("loss",c.Loss);w.WriteNumber("phantom",c.Phantom);w.WriteNumber("reported_failure",c.ReportedFailure);w.WriteNumber("duplicate",c.Duplicate);w.WriteNumber("reorder",c.Reorder);w.WriteEndObject();}
    private static void WriteNullable(Utf8JsonWriter w,string name,string? x){if(x is null)w.WriteNull(name);else w.WriteString(name,x);}
    private static void WriteNullable(Utf8JsonWriter w,string name,long? x){if(x is null)w.WriteNull(name);else w.WriteNumber(name,x.Value);}
}
