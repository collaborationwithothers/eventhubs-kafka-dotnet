using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.Common.SingleAction;

internal sealed record SingleActionIdentity(string ScenarioId, string ConfigurationId, string RunId, string InstanceId);
internal sealed record SingleActionRegistration(int SchemaVersion, string Mode, SingleActionIdentity Identity, string JobName, string PodName, string PodUid, string SourceCommit);
internal sealed record SingleActionCompletion(int SchemaVersion, string Mode, SingleActionIdentity Identity, string JobName, string PodName, string PodUid, string SourceCommit, int RepeatsAttempted, int HandlerExitCode, bool CleanupCompleted);
internal sealed record SingleActionCapture(int SchemaVersion, string Mode, bool Synthetic, SingleActionIdentity Identity, string JobName, string PodName, string PodUid, string SourceCommit, string ManagedVersion, string NativeVersion, SingleActionRepeat[] Repeats);
internal sealed record SingleActionRepeat(int Repeat, string ClientId, string Action, bool Terminal, long ElapsedMs, long? TerminalUnixMs, Dictionary<string,string> Settings, string[] OmittedKeys, JsonElement Result, JsonElement Error, JsonElement Matched, SingleActionClientError[] ClientErrors, SingleActionStatistic[] Statistics, string[] Diagnostics);
internal sealed record SingleActionClientError(int Code, bool IsFatal, string Origin, string Reason, long ObservedElapsedMs);
internal sealed record SingleActionStatistic(int Sequence, long ObservedElapsedMs, string RawBase64);
internal sealed record SingleActionJudgeReceipt(int SchemaVersion, string Mode, string RunId, string ScenarioId, string ConfigurationId, string SourceCommit, string JobName, string PodName, string PodUid, string CandidateName, int HandlerExitCode, bool CleanupCompleted);
internal sealed record SingleActionSeedReceipt(int SchemaVersion,string Mode,string RunId,string SourceCommit,string JobName,string PodName,string PodUid,string ServiceAccount,string Topic,int Partition,string KeyBase64,string ValueSha256,bool Delivered,JsonElement Error,int HandlerExitCode,bool CleanupCompleted);
internal sealed record SingleActionSeedPod(int SchemaVersion,string JobName,string PodName,string PodUid,string ServiceAccount,string OwnerKind,string OwnerName);

internal static class SingleActionPaths
{
    internal static string Prefix(string run,string item,string configuration) => $"{run}/single-action/{item}/{configuration}/";
    internal static string CaptureJob(string run,string item,string configuration) => $"{run}-{item.ToLowerInvariant()}-{configuration.ToLowerInvariant()}-single";
    internal static string JudgeJob(string run,string item,string configuration) => $"{run}-{item.ToLowerInvariant()}-{configuration.ToLowerInvariant()}-single-judge";
    internal static string Candidate(string run,string item,string configuration) => Prefix(run,item,configuration)+"candidate.json";
    internal static string JudgeCompletion(string run,string item,string configuration) => Prefix(run,item,configuration)+"judge-completion.json";
}

/// <summary>Strict, passive transport codec. Semantic acceptance belongs to the judgement files.</summary>
internal static class SingleActionWire
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters=true,
        RespectNullableAnnotations=true,
        MaxDepth=32
    };
    internal static T Read<T>(ReadOnlyMemory<byte> bytes,long maximum) where T:class
    {
        var limits=new InputLimits(32,maximum,268435456,int.MaxValue,100000,1000000,2048);
        try
        {
            var parsed=StrictJson.ParseDocument(bytes,"single-action",limits,null);
            if(!parsed.IsSuccess)throw new FormatException("single_action_json");
            using var document=parsed.Value!;
            T result=document.RootElement.Deserialize<T>(JsonOptions)??throw new FormatException("single_action_null");
            ValidateCollections(result);
            return result;
        }
        catch(JsonException exception){throw new FormatException("single_action_schema",exception);}
    }
    private static void ValidateCollections(object value)
    {
        if(value is string or JsonElement||value.GetType().IsPrimitive)return;
        if(value is IDictionary dictionary)
        {
            foreach(DictionaryEntry entry in dictionary)
            {
                if(entry.Value is null)throw new FormatException("single_action_null_collection_value");
                ValidateCollections(entry.Value);
            }
            return;
        }
        if(value is IEnumerable collection)
        {
            foreach(object? item in collection)
            {
                if(item is null)throw new FormatException("single_action_null_collection_item");
                ValidateCollections(item);
            }
            return;
        }
        foreach(var property in value.GetType().GetProperties())
        {
            object? child=property.GetValue(value);
            if(child is not null)ValidateCollections(child);
        }
    }

    internal static byte[] Write<T>(T value,long maximum) => DeploymentJson.Write(maximum,writer=>JsonSerializer.Serialize(writer,value,JsonOptions));
    internal static bool Text(string? value) => !string.IsNullOrWhiteSpace(value);
    internal static int Bytes(string value) => Encoding.UTF8.GetByteCount(value);
}
