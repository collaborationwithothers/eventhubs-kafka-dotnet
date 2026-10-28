using System.Text.Json;

namespace Parchwell.LoadGen.Common.SingleAction;

internal sealed record SingleActionIdentity(string ScenarioId, string ConfigurationId, string RunId, string InstanceId);
internal sealed record SingleActionRegistration(int SchemaVersion, string Mode, SingleActionIdentity Identity,
    string JobName, string PodName, string PodUid, string SourceCommit);
internal sealed record SingleActionCompletion(int SchemaVersion, string Mode, SingleActionIdentity Identity,
    string JobName, string PodName, string PodUid, string SourceCommit, int RepeatsAttempted, int HandlerExitCode, bool CleanupCompleted);
internal sealed record SingleActionError(int Code, bool IsFatal, string Origin, string Reason);
internal sealed record SingleActionClientError(int Code, bool IsFatal, string Origin, string Reason, long ObservedElapsedMs);
internal sealed record SingleActionStatistics(int Sequence, long ObservedElapsedMs, string RawBase64);
internal sealed record SingleActionMatched(string Topic, int Partition, string KeyBase64, string ValueBase64);
internal sealed record SingleActionRepeat(int Repeat, string ClientId, string Action, bool Terminal, long ElapsedMs,
    long? TerminalUnixMs, IReadOnlyDictionary<string, string> Settings, IReadOnlyList<string> OmittedKeys,
    string? Result, SingleActionError? Error, SingleActionMatched? Matched,
    IReadOnlyList<SingleActionClientError> ClientErrors, long ClientErrorsDropped, IReadOnlyList<SingleActionStatistics> Statistics, IReadOnlyList<string> Diagnostics);
internal sealed record SingleActionCaptureFile(int SchemaVersion, string Mode, bool Synthetic, SingleActionIdentity Identity,
    string JobName, string PodName, string PodUid, string SourceCommit, string ManagedVersion, string NativeVersion,
    IReadOnlyList<SingleActionRepeat> Repeats);

internal static class SingleActionWire
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    internal static byte[] Encode<T>(T value, int maximumBytes)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        if (bytes.Length > maximumBytes) throw new InvalidDataException("single_action_artifact_limit");
        return bytes;
    }
    internal static string Prefix(string run, string item, string configuration) => $"{run}/single-action/{item}/{configuration}/";
}
