using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Parchwell.Verifier.Common.Consumer;

internal static class ConsumerArtifactCodec
{
    private static readonly JsonSerializerOptions SnapshotOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    internal static ConsumerRegistration ReadRegistration(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument doc = Document(bytes, 65536); JsonElement x = doc.RootElement;
        Shape(x, "schema_version kind run_id scenario_id configuration_id phase repeat_number role instance_id job_name pod_name pod_uid node_name source_commit group_id effective_settings registered_utc");
        Version(x, "consumer_registration"); string role = Role(x);
        JsonElement settings = x.GetProperty("effective_settings"); Require(settings.ValueKind == JsonValueKind.Object);
        var map = new Dictionary<string,string>(StringComparer.Ordinal); string? previous = null;
        foreach (JsonProperty p in settings.EnumerateObject()) { Require(previous is null || StringComparer.Ordinal.Compare(previous,p.Name)<0); map.Add(p.Name, String(p.Value)); previous = p.Name; }
        return new(S(x,"run_id"),S(x,"scenario_id"),S(x,"configuration_id"),S(x,"phase"),Int(x,"repeat_number"),role,S(x,"instance_id"),S(x,"job_name"),S(x,"pod_name"),S(x,"pod_uid"),S(x,"node_name"),S(x,"source_commit"),S(x,"group_id"),map,Time(x,"registered_utc"));
    }
    internal static ConsumerCompletion ReadCompletion(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument doc = Document(bytes,65536); JsonElement x=doc.RootElement;
        Shape(x,"schema_version kind run_id scenario_id configuration_id phase repeat_number role instance_id job_name pod_uid source_commit rows record_blobs messages_processed closed_utc"); Version(x,"consumer_completion");
        long rows=Long(x,"rows"), processed=Long(x,"messages_processed");int blobs=Int(x,"record_blobs");Require(rows>=0&&processed>=0&&blobs>=0);
        return new(S(x,"run_id"),S(x,"scenario_id"),S(x,"configuration_id"),S(x,"phase"),Int(x,"repeat_number"),Role(x),S(x,"instance_id"),S(x,"job_name"),S(x,"pod_uid"),S(x,"source_commit"),rows,blobs,processed,Time(x,"closed_utc"));
    }
    internal static ConsumerRow ReadRow(ReadOnlyMemory<byte> bytes)
    {
        Require(bytes.Length+1<=65536); using JsonDocument doc=Document(bytes,65535);JsonElement x=doc.RootElement;
        string kind=S(x,"kind"), instance=S(x,"instance_id");long seq=Long(x,"row_sequence");Require(seq>=0);Version(x,kind);
        const string common="schema_version kind instance_id row_sequence ";
        switch(kind)
        {
            case "processed":
                Shape(x,common+"topic partition offset producer_instance_id attempt_id key sequence slow processing_started_utc processing_ended_utc");Require(S(x,"topic")=="document-events");
                return new(kind,instance,seq,Time(x,"processing_ended_utc"),NonnegativeInt(x,"partition"),NonnegativeLong(x,"offset"),NullableString(x,"producer_instance_id"),NullableString(x,"attempt_id"),NullableString(x,"key"),NullableLong(x,"sequence"),Bool(x,"slow"),Time(x,"processing_started_utc"));
            case "partition_event":
                Shape(x,common+"event partitions event_utc");string ev=S(x,"event");Require(ev is "assigned" or "revoked" or "lost");
                JsonElement parts=x.GetProperty("partitions");Require(parts.ValueKind==JsonValueKind.Array);int[] partitions=parts.EnumerateArray().Select(IntValue).ToArray();Require(partitions.All(p=>p>=0)&&partitions.SequenceEqual(partitions.Distinct().Order()));
                return new(kind,instance,seq,Time(x,"event_utc"),Event:ev,Partitions:partitions);
            case "kill_intent":
                Shape(x,common+"partition offset event_utc");return new(kind,instance,seq,Time(x,"event_utc"),NonnegativeInt(x,"partition"),NonnegativeLong(x,"offset"));
            case "commit":
                Shape(x,common+"source outcome error_code error_reason submitted offsets event_utc");string source=S(x,"source"),outcome=S(x,"outcome");int error=Int(x,"error_code");string? reason=NullableString(x,"error_reason");Require(error!=0||reason is null);
                IReadOnlyList<ConsumerCommitOffset>? submitted=x.GetProperty("submitted").ValueKind==JsonValueKind.Null?null:Offsets(x.GetProperty("submitted"),false);IReadOnlyList<ConsumerCommitOffset> offsets=Offsets(x.GetProperty("offsets"),true);
                Require(source is "callback" or "explicit");Require(outcome is "callback" or "returned" or "partition_exception" or "request_exception");
                Require(source=="callback"?outcome=="callback"&&submitted is null:outcome!="callback"&&submitted is not null);
                Require(outcome!="returned"||(error==0&&offsets.All(p=>p.ErrorCode==0)&&submitted!.SequenceEqual(offsets)));
                Require(outcome!="partition_exception"||error==0);Require(outcome!="request_exception"||(error!=0&&offsets.Count==0));
                return new(kind,instance,seq,Time(x,"event_utc"),Source:source,Outcome:outcome,ErrorCode:error,ErrorReason:reason,Submitted:submitted,Offsets:offsets);
            default: throw new FormatException("consumer_row_kind");
        }
    }
    internal static ConsumerStartSnapshot ReadSnapshot(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument doc=Document(bytes,int.MaxValue);JsonElement x=doc.RootElement;Shape(x,"schema_version kind group_id first_read_started_utc second_read_ended_utc members");Version(x,"consumer_start_snapshot");
        JsonElement array=x.GetProperty("members");Require(array.ValueKind==JsonValueKind.Array);var members=new List<ConsumerSnapshotMember>();
        foreach(JsonElement m in array.EnumerateArray()){Shape(m,"instance_id role first_read_last_row_sequence second_read_last_row_sequence");long a=Long(m,"first_read_last_row_sequence"),b=Long(m,"second_read_last_row_sequence");Require(a>=-1&&b>=a);members.Add(new(S(m,"instance_id"),Role(m),a,b));}
        Require(members.Select(m=>m.InstanceId).SequenceEqual(members.Select(m=>m.InstanceId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
        return new(S(x,"group_id"),Time(x,"first_read_started_utc"),Time(x,"second_read_ended_utc"),members);
    }
    internal static byte[] WriteSnapshot(ConsumerStartSnapshot value) => JsonSerializer.SerializeToUtf8Bytes(new {
        schema_version=1,kind="consumer_start_snapshot",group_id=value.GroupId,first_read_started_utc=Stamp(value.FirstReadStartedUtc),second_read_ended_utc=Stamp(value.SecondReadEndedUtc),members=value.Members.OrderBy(m=>m.InstanceId,StringComparer.Ordinal)},SnapshotOptions);
    private static string Stamp(DateTimeOffset time)=>time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",CultureInfo.InvariantCulture);
    private static JsonDocument Document(ReadOnlyMemory<byte> bytes,int maximum)
    {
        Require(bytes.Length<=maximum);
        try { _=new UTF8Encoding(false,true).GetString(bytes.Span);JsonDocument doc=JsonDocument.Parse(bytes);ValidateUnique(doc.RootElement);return doc; }
        catch(Exception e) when(e is JsonException or DecoderFallbackException){throw new FormatException("consumer_json",e);}
    }
    private static void ValidateUnique(JsonElement x){if(x.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(StringComparer.Ordinal);foreach(JsonProperty p in x.EnumerateObject()){Require(names.Add(p.Name));ValidateUnique(p.Value);}}else if(x.ValueKind==JsonValueKind.Array)foreach(JsonElement v in x.EnumerateArray())ValidateUnique(v);}
    private static void Shape(JsonElement x,string fields){Require(x.ValueKind==JsonValueKind.Object);string[] names=fields.Split(' ',StringSplitOptions.RemoveEmptyEntries);Require(x.EnumerateObject().Select(p=>p.Name).Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)));}
    private static IReadOnlyList<ConsumerCommitOffset> Offsets(JsonElement x,bool error){Require(x.ValueKind==JsonValueKind.Array);return x.EnumerateArray().Select(p=>{Shape(p,error?"topic partition offset error_code":"topic partition offset");return new ConsumerCommitOffset(S(p,"topic"),NonnegativeInt(p,"partition"),Long(p,"offset"),error?Int(p,"error_code"):0);}).ToArray();}
    private static void Version(JsonElement x,string kind){Require(Int(x,"schema_version")==1&&S(x,"kind")==kind);}
    private static string Role(JsonElement x){string role=S(x,"role");Require(role is "subject" or "witness");return role;}
    private static string S(JsonElement x,string key)=>String(x.GetProperty(key));
    private static string String(JsonElement x){Require(x.ValueKind==JsonValueKind.String);return x.GetString()!;}
    private static string? NullableString(JsonElement x,string key)=>x.GetProperty(key).ValueKind==JsonValueKind.Null?null:S(x,key);
    private static long? NullableLong(JsonElement x,string key)=>x.GetProperty(key).ValueKind==JsonValueKind.Null?null:Long(x,key);
    private static int Int(JsonElement x,string key)=>IntValue(x.GetProperty(key));
    private static int IntValue(JsonElement x){Require(x.ValueKind==JsonValueKind.Number&&x.TryGetInt32(out _));return x.GetInt32();}
    private static long Long(JsonElement x,string key){JsonElement v=x.GetProperty(key);Require(v.ValueKind==JsonValueKind.Number&&v.TryGetInt64(out _));return v.GetInt64();}
    private static int NonnegativeInt(JsonElement x,string key){int n=Int(x,key);Require(n>=0);return n;}
    private static long NonnegativeLong(JsonElement x,string key){long n=Long(x,key);Require(n>=0);return n;}
    private static bool Bool(JsonElement x,string key){JsonElement v=x.GetProperty(key);Require(v.ValueKind is JsonValueKind.True or JsonValueKind.False);return v.GetBoolean();}
    private static DateTimeOffset Time(JsonElement x,string key){string s=S(x,key);Require(s.Length==28&&DateTimeOffset.TryParseExact(s,"yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out _));return DateTimeOffset.ParseExact(s,"yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal);}
    private static void Require(bool condition){if(!condition)throw new FormatException("consumer_artifact_invalid");}
}
