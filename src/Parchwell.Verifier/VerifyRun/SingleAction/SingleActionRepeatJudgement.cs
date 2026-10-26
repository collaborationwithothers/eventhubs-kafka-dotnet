using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.SingleAction;

namespace Parchwell.Verifier.VerifyRun.SingleAction;

/// <summary>Owns the closed single-action p4-p7 decisions, independent of Kafka clients.</summary>
internal static class SingleActionRepeatJudgement
{
    internal static SingleActionRepeatProjection Judge(SingleActionRepeat repeat,SingleActionConfiguration declaration,IReadOnlyDictionary<string,string> expected,IEnumerable<string> omissions,string run)
    {
        var reasons=new List<string>();
        bool terminal=repeat.Terminal&&repeat.TerminalUnixMs.HasValue&&repeat.ElapsedMs>=0&&repeat.ElapsedMs<=declaration.WindowMs&&!repeat.Diagnostics.Contains("capture_limit",StringComparer.Ordinal);
        if(!terminal)reasons.Add("p4_no_terminal_outcome");
        string[] omitted=omissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        bool fidelity=repeat.Action==declaration.Action&&repeat.Settings.Count==expected.Count&&expected.All(p=>repeat.Settings.TryGetValue(p.Key,out string? value)&&value==p.Value)&&repeat.OmittedKeys.SequenceEqual(omitted,StringComparer.Ordinal)&&!omitted.Any(repeat.Settings.ContainsKey);
        if(!fidelity)reasons.Add("p5_configuration_fidelity");
        SingleActionOutcome? outcome=null;
        if(repeat.Terminal&&!TryOutcome(repeat,declaration,run,out outcome))reasons.Add("p7_outcome_record");
        var statistics=terminal?SelectStatistics(repeat,declaration):null;
        if(statistics is null)reasons.Add("p6_broker_count");
        string status=reasons.Any(r=>r is "p5_configuration_fidelity" or "p7_outcome_record")?"invalid":reasons.Count==0?"complete_valid":"incomplete";
        return new(repeat.Repeat,status,reasons.ToArray(),outcome,statistics?.Count,statistics?.Reference);
    }

    internal static bool TryOutcome(SingleActionRepeat repeat,SingleActionConfiguration declaration,string run,out SingleActionOutcome? outcome)
    {
        outcome=null;
        if(repeat.Result.ValueKind!=JsonValueKind.String)return false;
        string? result=repeat.Result.GetString();
        if(result=="success")
        {
            if(repeat.Error.ValueKind!=JsonValueKind.Null)return false;
            if(declaration.Action=="assign_consume"&&!Matched(repeat.Matched,declaration,run))return false;
            if(declaration.Action!="assign_consume"&&repeat.Matched.ValueKind!=JsonValueKind.Null)return false;
            outcome=new("success",null,null,null);return true;
        }
        if(result!="error"||repeat.Matched.ValueKind!=JsonValueKind.Null||!Error(repeat.Error,out int code,out bool fatal,out string? origin))return false;
        outcome=new("error",code,fatal,origin);return true;
    }

    private static bool Error(JsonElement value,out int code,out bool fatal,out string? origin)
    {
        code=0;fatal=false;origin=null;
        if(!Closed(value,"code","is_fatal","origin","reason")||!StrictJson.TryGetInt32(value.GetProperty("code"),out code))return false;
        JsonElement fatalValue=value.GetProperty("is_fatal");
        if(fatalValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))return false;
        fatal=fatalValue.GetBoolean();
        JsonElement originValue=value.GetProperty("origin"),reason=value.GetProperty("reason");
        if(originValue.ValueKind!=JsonValueKind.String||reason.ValueKind!=JsonValueKind.String||SingleActionWire.Bytes(reason.GetString()!)>4096)return false;
        origin=originValue.GetString();return origin is "construction" or "local" or "broker";
    }

    private static bool Matched(JsonElement value,SingleActionConfiguration declaration,string run)
    {
        if(!Closed(value,"topic","partition","key_base64","value_base64"))return false;
        try
        {
            if(value.GetProperty("value_base64").GetString()!.Length>1466668||value.GetProperty("key_base64").GetString()!.Length>65536)return false;
            return value.GetProperty("topic").GetString()==declaration.Topic&&value.GetProperty("partition").GetInt32()==declaration.Partition&&
                Convert.FromBase64String(value.GetProperty("key_base64").GetString()!).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes($"f04-seed:{run}"))&&
                Convert.FromBase64String(value.GetProperty("value_base64").GetString()!).AsSpan().SequenceEqual(Encoding.ASCII.GetBytes(new string('s',1000)));
        }
        catch(Exception ex) when(ex is FormatException or InvalidOperationException){return false;}
    }

    private static (int Count,SingleActionStatisticsReference Reference)? SelectStatistics(SingleActionRepeat repeat,SingleActionConfiguration declaration)
    {
        var candidates=new List<(int Count,SingleActionStatisticsReference Reference)>();
        if(repeat.Statistics.Length>64)return null;
        var sequences=new HashSet<int>();
        foreach(var row in repeat.Statistics)
        {
            try
            {
                if(row.Sequence<1||!sequences.Add(row.Sequence)||row.ObservedElapsedMs<0)return null;
                if(row.RawBase64.Length>349528)return null;
                byte[] raw=Convert.FromBase64String(row.RawBase64);
                var limits=new InputLimits(32,262144,268435456,262144,10000,10000,2048);
                var parsed=StrictJson.ParseDocument(raw,"statistics",limits,null);
                if(!parsed.IsSuccess)return null;
                using var document=parsed.Value!;
                JsonElement root=document.RootElement;
                string kind=declaration.Action is "subscribe" or "assign_consume"?"consumer":"producer";
                if(!root.TryGetProperty("name",out var name)||name.ValueKind!=JsonValueKind.String||!name.GetString()!.StartsWith(repeat.ClientId+"#"+kind+"-",StringComparison.Ordinal)||
                    !root.TryGetProperty("client_id",out var client)||client.ValueKind!=JsonValueKind.String||client.GetString()!=repeat.ClientId||
                    !root.TryGetProperty("type",out var type)||type.ValueKind!=JsonValueKind.String||type.GetString()!=kind||
                    !root.TryGetProperty("time",out var timeValue)||!timeValue.TryGetInt64(out long time)||time<0||
                    !root.TryGetProperty("ts",out var tsValue)||!tsValue.TryGetInt64(out long ts)||ts<0||
                    !root.TryGetProperty("brokers",out var brokers)||brokers.ValueKind!=JsonValueKind.Object)return null;
                if(brokers.EnumerateObject().Count()>64)return null;
                int count=0;
                foreach(var broker in brokers.EnumerateObject())
                {
                    if(broker.Value.ValueKind!=JsonValueKind.Object||!broker.Value.TryGetProperty("source",out var source)||source.ValueKind!=JsonValueKind.String||!broker.Value.TryGetProperty("state",out var state)||state.ValueKind!=JsonValueKind.String)return null;
                    if(source.GetString() is "learned" or "configured" or "logical"&&state.GetString()=="UP")count++;
                }
                if(checked(time*1000)<repeat.TerminalUnixMs)continue;
                candidates.Add((count,new(row.Sequence,time,ts,Convert.ToHexStringLower(SHA256.HashData(raw)))));
            }
            catch(Exception ex) when(ex is FormatException or JsonException or InvalidOperationException or OverflowException or InputLimitExceededException){return null;}
        }
        return candidates.OrderBy(c=>c.Reference.NativeTime).ThenBy(c=>c.Reference.NativeTs).ThenBy(c=>c.Reference.Sequence).Cast<(int Count,SingleActionStatisticsReference Reference)?>().FirstOrDefault();
    }

    internal static bool Closed(JsonElement value,params string[] fields) => value.ValueKind==JsonValueKind.Object&&value.EnumerateObject().Select(p=>p.Name).Order(StringComparer.Ordinal).SequenceEqual(fields.Order(StringComparer.Ordinal),StringComparer.Ordinal);
}
