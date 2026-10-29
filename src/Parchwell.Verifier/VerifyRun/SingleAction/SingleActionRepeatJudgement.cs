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
        bool noOutcome=repeat.Result.ValueKind==JsonValueKind.String&&repeat.Result.GetString()=="no_outcome";
        bool terminal=repeat.Terminal&&repeat.TerminalUnixMs.HasValue&&repeat.ElapsedMs>=0&&repeat.ElapsedMs<=declaration.WindowMs&&!repeat.Diagnostics.Contains("capture_limit",StringComparer.Ordinal)&&
            (!noOutcome||(declaration.TerminalRule=="window_end_no_outcome"&&repeat.ElapsedMs==declaration.WindowMs));
        if(!terminal)reasons.Add("p4_no_terminal_outcome");
        string[] omitted=omissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        bool fidelity=repeat.Action==declaration.Action&&repeat.Settings.Count==expected.Count&&expected.All(p=>repeat.Settings.TryGetValue(p.Key,out string? value)&&value==p.Value)&&repeat.OmittedKeys.SequenceEqual(omitted,StringComparer.Ordinal)&&!omitted.Any(repeat.Settings.ContainsKey);
        if(!fidelity)reasons.Add("p5_configuration_fidelity");
        SingleActionOutcome? outcome=null;
        bool outcomeRecord=repeat.Terminal||noOutcome
            ?TryOutcome(repeat,declaration,run,out outcome):DroppedErrorsValid(repeat,declaration);
        if(!outcomeRecord)reasons.Add("p7_outcome_record");
        var statistics=terminal?SelectStatistics(repeat,declaration):null;
        if(statistics is null)reasons.Add("p6_broker_count");
        string status=reasons.Any(r=>r is "p5_configuration_fidelity" or "p7_outcome_record")?"invalid":reasons.Count==0?"complete_valid":"incomplete";
        return new(repeat.Repeat,status,reasons.ToArray(),outcome,statistics?.Count,statistics?.Reference);
    }

    internal static bool TryOutcome(SingleActionRepeat repeat,SingleActionConfiguration declaration,string run,out SingleActionOutcome? outcome)
    {
        outcome=null;
        if(!DroppedErrorsValid(repeat,declaration))return false;
        if(repeat.Result.ValueKind!=JsonValueKind.String)return false;
        string? result=repeat.Result.GetString();
        if(result=="no_outcome")
        {
            if(declaration.TerminalRule!="window_end_no_outcome"||repeat.Error.ValueKind!=JsonValueKind.Null||
                repeat.Matched.ValueKind!=JsonValueKind.Null||repeat.ClientErrors.Length!=0||repeat.ClientErrorsDropped!=0)return false;
            outcome=new("no_outcome",null,null,null);return true;
        }
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

    private static bool DroppedErrorsValid(SingleActionRepeat repeat,SingleActionConfiguration declaration) =>
        repeat.ClientErrorsDropped>=0&&(repeat.ClientErrorsDropped==0||
        declaration.TerminalRule=="first_error_callback_or_delivery"&&repeat.ClientErrors.Length==64);

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
        JsonElement topic=value.GetProperty("topic"),partition=value.GetProperty("partition"),key=value.GetProperty("key_base64"),payload=value.GetProperty("value_base64");
        if(!StrictJson.TryGetString(topic,out string matchedTopic)||!StrictJson.TryGetInt32(partition,out int matchedPartition)||
            !StrictJson.TryGetString(key,out string keyBase64)||!StrictJson.TryGetString(payload,out string valueBase64)||
            valueBase64.Length>1466668||keyBase64.Length>65536)return false;
        try
        {
            return matchedTopic==declaration.Topic&&matchedPartition==declaration.Partition&&
                Convert.FromBase64String(keyBase64).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes($"f04-seed:{run}"))&&
                Convert.FromBase64String(valueBase64).AsSpan().SequenceEqual(Encoding.ASCII.GetBytes(new string('s',1000)));
        }
        catch(FormatException){return false;}
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
                    !root.TryGetProperty("time",out var timeValue)||!StrictJson.TryGetInt64(timeValue,out long time)||time<0||
                    !root.TryGetProperty("ts",out var tsValue)||!StrictJson.TryGetInt64(tsValue,out long ts)||ts<0||
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
    internal static ProbeV2RepeatProjection JudgeV2Slot(ProbeV2Options options, ProbeV2Evidence evidence, ProbeV2Repeat row,
        IReadOnlyDictionary<string,string> expected, IEnumerable<string> omissions)
    {
        var reasons = new List<string>();
        var matrix = ProbeV2Matrix.For(options.ScenarioId, evidence.Configuration, evidence.Stage);
        bool invalid = false;
        void Bad(string reason) { invalid = true; reasons.Add(reason); }
        bool settingsLimited = row.Settings.Count > options.Limits.MaxSettingsKeys ||
            row.Settings.Sum(x => Encoding.UTF8.GetByteCount(x.Key) + Encoding.UTF8.GetByteCount(x.Value)) > options.Limits.MaxSettingsBytes;
        if (row.ReadObservation.RecordsSeen > options.Limits.MaxNonmatchingRecords ||
            row.ClientErrors.Count > options.Limits.MaxClientErrors || row.Statistics.Count > options.Limits.MaxStatisticsReports ||
            row.Diagnostics.Count > options.Limits.MaxDiagnostics || row.Diagnostics.Any(x => Encoding.UTF8.GetByteCount(x) > options.Limits.MaxDiagnosticBytes) ||
            settingsLimited || row.ClientErrors.Any(ErrorLimited) || row.Error is not null && ErrorLimited(row.Error) ||
            row.Delivery?.Error is { } deliveryError && ErrorLimited(deliveryError)) reasons.Add("v2_capture_limit");
        if (row.Repeat is < 1 or > 5 || row.ReadObservation.RecordsSeen < 0 || row.ReadObservation.EofCount < 0 ||
            row.ElapsedMs < 0 || row.TerminalUnixMs < 0 || row.ClientErrors.Any(x => !V2Error(x)) || row.Error is not null && !V2Error(row.Error)) Bad("v2_wire");
        bool ErrorLimited(ProbeV2Error error) => Encoding.UTF8.GetByteCount(error.Reason) > options.Limits.MaxErrorBytes;
        bool PayloadValid(string? value,int maximum,bool nullable = false)
        {
            bool valid = V2Base64(value,maximum,out bool limited,nullable);
            if (!valid) Bad("v2_wire");
            if (limited) reasons.Add("v2_capture_limit");
            return valid;
        }
        foreach (var report in row.Statistics) _ = PayloadValid(report.RawBase64,options.Limits.MaxStatisticsReportBytes);
        if (row.Admission != "required")
        {
            bool allowed = row.Admission is "not_applicable_no_delivery" or "not_attempted_dependency" or "not_attempted_cleanup";
            if (!allowed || row.ClientId is not null || row.Terminal || row.ElapsedMs != 0 || row.TerminalUnixMs is not null ||
                row.MetadataPartitions is not null || row.Requested is not null || row.Delivery is not null || row.Result is not null || row.Error is not null ||
                row.Settings.Count != 0 || row.OmittedKeys.Count != 0 || row.ClientErrors.Count != 0 || row.Statistics.Count != 0 ||
                row.ReadObservation != new ProbeV2ReadObservation(0,0,null) || row.Action != matrix.Action ||
                !row.Diagnostics.SequenceEqual(row.Admission == "not_applicable_no_delivery" ? Array.Empty<string>() : [row.Admission], StringComparer.Ordinal)) Bad("v2_wire");
            if (row.Admission != "not_applicable_no_delivery") reasons.Add(row.Admission == "not_attempted_cleanup" ? "v2_completion" : "v2_location");
            return new(row.Repeat, invalid ? "invalid" : reasons.Count == 0 ? "complete_valid" : "incomplete", reasons.Distinct(StringComparer.Ordinal).ToArray(), null,
                null,null,new(null,null,null,null,null),null,null,null,[],[]);
        }
        if (row.ClientId != evidence.Capture?.PodName + "-r" + row.Repeat.ToString(System.Globalization.CultureInfo.InvariantCulture)) Bad("v2_identity");
        string[] omitted = omissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (row.Action != matrix.Action || !settingsLimited && (expected.Count != row.Settings.Count || expected.Any(x => !row.Settings.TryGetValue(x.Key,out string? value) || value != x.Value)) ||
            !row.OmittedKeys.SequenceEqual(omitted,StringComparer.Ordinal) || omitted.Any(row.Settings.ContainsKey)) Bad("v2_configuration");
        var request = row.Requested;
        bool keyValid = request is not null && PayloadValid(request.KeyBase64,options.Limits.MaxKeyBytes);
        bool valueValid = request is not null && PayloadValid(request.ValueBase64,options.Limits.MaxValueBytes,true);
        bool keyLimited = request is not null && V2Base64(request.KeyBase64,options.Limits.MaxKeyBytes,out bool kl) && kl;
        bool valueLimited = request is not null && V2Base64(request.ValueBase64,options.Limits.MaxValueBytes,out bool vl,true) && vl;
        bool requestValid = request is not null && request.Topic == matrix.Topic && keyValid && valueValid &&
            (keyLimited || request.KeyBase64 == Convert.ToBase64String(matrix.Key(options,row.Repeat,evidence.Configuration)));
        if (requestValid && request is not null)
        {
            bool automatic = options.ScenarioId == "B08" && evidence.Stage == "action";
            requestValid = request.PartitionMode == (automatic ? "automatic" : "explicit") &&
                (automatic ? request.Partition is null : request.Partition >= 0) &&
                (matrix.Action == "produce_one" ? request.Offset is null : request.Offset >= 0);
            if (options.ScenarioId != "B08") requestValid &= request.Partition == 0;
            byte[]? value = matrix.Value(options,row.Repeat,evidence.Stage,evidence.Configuration);
            requestValid &= valueLimited || request.ValueBase64 == (value is null ? null : Convert.ToBase64String(value));
        }
        if (!requestValid) Bad("v2_configuration");
        if (options.ScenarioId == "B08")
        {
            if (row.MetadataPartitions is null) reasons.Add("v2_partition_input");
            else if (!row.MetadataPartitions.SequenceEqual(Enumerable.Range(0,32))) Bad("v2_partition_input");
        }
        else if (row.MetadataPartitions is not null) Bad("v2_wire");
        if (row.Delivery is { } delivery && (delivery.Partition < 0 || delivery.Offset < 0 || delivery.ObservedElapsedMs < 0 || delivery.Sequence < 0 ||
            delivery.Error is not null && !V2Error(delivery.Error) || delivery.Topic != matrix.Topic ||
            delivery.PersistenceStatus is not ("NotPersisted" or "PossiblyPersisted" or "Persisted"))) Bad("v2_wire");
        if (row.ReadObservation.Record is { } record && (record.Partition < 0 || record.Offset < 0 || record.ObservedElapsedMs < 0 || record.Sequence < 0 ||
            !PayloadValid(record.KeyBase64,options.Limits.MaxKeyBytes) || !PayloadValid(record.ValueBase64,options.Limits.MaxValueBytes,true) || row.ReadObservation.RecordsSeen < 1)) Bad("v2_wire");
        bool terminal = row.Terminal && row.TerminalUnixMs.HasValue && row.ElapsedMs <= matrix.WindowMs;
        if (!terminal) reasons.Add("v2_terminal");
        SingleActionOutcome? outcome = null;
        if (row.Result == "success" && row.Error is null)
        {
            outcome = new("success",null,null,null);
            if (matrix.Action == "produce_one" && !V2Delivered(row)) reasons.Add("v2_location");
            if (matrix.Action == "assign_consume" && row.ReadObservation.Record is null) reasons.Add("v2_location");
        }
        else if (row.Result == "error" && row.Error is { } error && V2Error(error)) outcome = new("error",error.Code,error.IsFatal,error.Origin);
        else if (row.Result == "no_outcome" && options.ScenarioId == "F06" && evidence.Stage == "action" && evidence.Configuration is "C10" or "C11" &&
            row.ElapsedMs == 120000 && row.Error is null && row.ClientErrors.Count == 0 && (row.ReadObservation.Record is null || row.ReadObservation.Record.ObservedElapsedMs > matrix.WindowMs) && row.Diagnostics.Count == 0)
            outcome = new("no_outcome",null,null,null);
        else if (row.Result is not null || row.Terminal) Bad("v2_terminal");
        if (row.Terminal && !V2TerminalOrder(row,matrix.WindowMs)) Bad("v2_terminal");
        if (row.Diagnostics.Count != 0) reasons.Add(row.Diagnostics.Any(x => x.StartsWith("client_cleanup_failed",StringComparison.Ordinal)) ? "v2_completion" : "v2_capture_limit");
        bool statisticsLimited = false;
        var statistic = terminal ? V2Statistics(row,out statisticsLimited) : null;
        if (statisticsLimited) reasons.Add("v2_capture_limit");
        if (statistic is null) reasons.Add("v2_statistics");
        var recordValue = row.ReadObservation.Record;
        var semantic = new ProbeV2Semantic(row.Delivery?.Partition,recordValue?.Partition,recordValue?.KeyBase64,
            recordValue is null ? null : recordValue.ValueBase64 is null ? "null" : recordValue.ValueBase64.Length == 0 ? "empty" : "bytes",
            recordValue is null ? null : recordValue.ValueBase64 == request?.ValueBase64);
        return new(row.Repeat,invalid ? "invalid" : reasons.Count == 0 ? "complete_valid" : "incomplete",reasons.Distinct(StringComparer.Ordinal).ToArray(),outcome,
            statistic?.Count,statistic?.Reference,semantic,row.Delivery,row.ReadObservation,null,[],
            row.ClientId is null ? [] : [new(row.ClientId,evidence.Stage,statistic?.Count,statistic?.Reference)]);
    }

    private static bool V2TerminalOrder(ProbeV2Repeat row,int window)
    {
        var observations = new List<(long Sequence,long Elapsed,string Result,ProbeV2Error? Error)>();
        if (row.Delivery is { } delivery && delivery.ObservedElapsedMs <= window)
            observations.Add((delivery.Sequence,delivery.ObservedElapsedMs,delivery.Error is null ? "success" : "error",delivery.Error));
        if (row.ReadObservation.Record is { } record && record.ObservedElapsedMs <= window)
            observations.Add((record.Sequence,record.ObservedElapsedMs,"success",null));
        foreach (var error in row.ClientErrors.Where(x => x.IsFatal && x.ObservedElapsedMs <= window))
            observations.Add((error.Sequence,error.ObservedElapsedMs,"error",error));
        if (row.Error is { } terminalError)
        {
            if (row.ClientErrors.Any(x => x.Sequence == terminalError.Sequence && !x.IsFatal)) return false;
            if (terminalError.ObservedElapsedMs > window) return false;
            observations.Add((terminalError.Sequence,terminalError.ObservedElapsedMs,"error",terminalError));
        }
        if (row.Result == "no_outcome") return observations.Count == 0;
        if (observations.Count == 0) return row.Result == "success" && row.Error is null && row.Delivery is null && row.ReadObservation.Record is null;
        var first = observations.OrderBy(x => x.Sequence).First();
        if (row.ElapsedMs != first.Elapsed || row.Result != first.Result || row.Error != first.Error) return false;
        return row.Statistics.All(x => x.Sequence > first.Sequence);
    }

    internal static bool V2Delivered(ProbeV2Repeat row) => row.Delivery is { Error: null, PersistenceStatus: "Persisted", Partition: >= 0, Offset: >= 0 };
    internal static bool V2Error(ProbeV2Error error) => error.Code != 0 && error.Origin is "construction" or "local" or "broker" &&
        error.ObservedElapsedMs >= 0 && error.Sequence >= 0;
    internal static bool V2Base64(string? text, int maximum, out bool limited, bool nullable = false)
    {
        limited = false;
        if (text is null) return nullable;
        try
        {
            byte[] bytes = Convert.FromBase64String(text);
            if (Convert.ToBase64String(bytes) != text) return false;
            limited = bytes.Length > maximum;
            return true;
        }
        catch (FormatException) { return false; }
    }
    private static (int Count,ProbeV2StatisticsReference Reference)? V2Statistics(ProbeV2Repeat row,out bool limited)
    {
        limited = false;
        if (row.Statistics.Count != 1 || row.TerminalUnixMs is null) return null;
        var report = row.Statistics[0];
        if (!V2Base64(report.RawBase64,32768,out limited) || limited) return null;
        if (report.Sequence < 0 || report.ObservedElapsedMs < row.ElapsedMs || report.ObservedElapsedMs - row.ElapsedMs > 20000) return null;
        try
        {
            byte[] raw = Convert.FromBase64String(report.RawBase64);
            var parsed = StrictJson.ParseDocument(raw,"statistics",new(32,32768,268435456,32768,10000,10000,2048),null);
            if (!parsed.IsSuccess) return null;
            using var document = parsed.Value!;
            var root = document.RootElement;
            string kind = row.Action == "produce_one" ? "producer" : "consumer";
            if (!root.TryGetProperty("name",out var name) || name.ValueKind != JsonValueKind.String || !name.GetString()!.StartsWith(row.ClientId + "#" + kind + "-",StringComparison.Ordinal) ||
                !root.TryGetProperty("client_id",out var client) || client.ValueKind != JsonValueKind.String || client.GetString() != row.ClientId ||
                !root.TryGetProperty("type",out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != kind ||
                !root.TryGetProperty("time",out var timeValue) || !StrictJson.TryGetInt64(timeValue,out long time) || time < 0 ||
                !root.TryGetProperty("ts",out var tsValue) || !StrictJson.TryGetInt64(tsValue,out long ts) || ts < 0 || checked(time * 1000) < row.TerminalUnixMs ||
                !root.TryGetProperty("brokers",out var brokers) || brokers.ValueKind != JsonValueKind.Object) return null;
            if (brokers.EnumerateObject().Count() > 64) { limited = true; return null; }
            int count = 0;
            foreach (var broker in brokers.EnumerateObject())
            {
                if (broker.Value.ValueKind != JsonValueKind.Object || !broker.Value.TryGetProperty("source",out var source) || source.ValueKind != JsonValueKind.String ||
                    !broker.Value.TryGetProperty("state",out var state) || state.ValueKind != JsonValueKind.String) return null;
                if (source.GetString() is "learned" or "configured" or "logical" && state.GetString() == "UP") count++;
            }
            return (count,new(report.Sequence,time,ts,Convert.ToHexStringLower(SHA256.HashData(raw))));
        }
        catch (InputLimitExceededException) { limited = true; return null; }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException or OverflowException) { return null; }
    }
}
