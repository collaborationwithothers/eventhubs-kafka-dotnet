using System.Text.Json;
using System.Text.Json.Serialization;

namespace Parchwell.LoadGen.SingleActionV2;

internal sealed record ProbeV2Registration(int SchemaVersion, string Mode, bool Synthetic, string RunId,
    string ScenarioId, string ConfigurationId, string Stage, string SourceCommit, string JobName,
    string PodName, string PodUid, string ServiceAccount);
internal sealed record ProbeV2CaptureFile(int SchemaVersion, string Mode, bool Synthetic, string RunId,
    string ScenarioId, string ConfigurationId, string Stage, string SourceCommit, string JobName,
    string PodName, string PodUid, string ServiceAccount, string ManagedVersion, string NativeVersion,
    IReadOnlyList<ProbeV2Repeat> Repeats, int HandlerExitCode, bool CleanupCompleted, bool HostCleanupCompleted);
internal sealed record ProbeV2Error(int Code, bool IsFatal, string Origin, string Reason, long ObservedElapsedMs, long Sequence);
internal sealed record ProbeV2Requested(string Topic, string PartitionMode, int? Partition, long? Offset, string KeyBase64, string? ValueBase64);
internal sealed record ProbeV2Delivery(string Topic, int Partition, long Offset, string PersistenceStatus,
    ProbeV2Error? Error, long ObservedElapsedMs, long Sequence);
internal sealed record ProbeV2Record(string Topic, int Partition, long Offset, string KeyBase64, string? ValueBase64,
    long ObservedElapsedMs, long Sequence);
internal sealed record ProbeV2ReadObservation(int RecordsSeen, int EofCount, ProbeV2Record? Record);
internal sealed record ProbeV2Statistics(long Sequence, long ObservedElapsedMs, string RawBase64);
internal sealed record ProbeV2Repeat(int Repeat, string Admission, string? ClientId, string Action, bool Terminal,
    long ElapsedMs, long? TerminalUnixMs, IReadOnlyDictionary<string, string> Settings, IReadOnlyList<string> OmittedKeys,
    int[]? MetadataPartitions, ProbeV2Requested? Requested, ProbeV2Delivery? Delivery,
    ProbeV2ReadObservation ReadObservation, string? Result, ProbeV2Error? Error,
    IReadOnlyList<ProbeV2Error> ClientErrors, IReadOnlyList<ProbeV2Statistics> Statistics, IReadOnlyList<string> Diagnostics);
internal sealed record ProbeV2Pod(int SchemaVersion, string JobName, string PodName, string PodUid, string ServiceAccount,
    string OwnerKind, string OwnerName, string ImageDigest);

internal static class ProbeV2Wire
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        MaxDepth = 32
    };
    internal static byte[] Encode<T>(T value, int maximum)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        if (bytes.Length > maximum)
        {
            var exception = new InvalidDataException("v2_capture_limit");
            exception.Data["observed_bytes"] = bytes.Length;
            exception.Data["allowed_bytes"] = maximum;
            throw exception;
        }
        return bytes;
    }
    internal static T Read<T>(ReadOnlyMemory<byte> bytes, int maximum)
    {
        if (bytes.Length == 0 || bytes.Length > maximum) throw new InvalidDataException("v2_input_limit");
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        RejectDuplicates(document.RootElement);
        return JsonSerializer.Deserialize<T>(bytes.Span, JsonOptions) ?? throw new JsonException("v2_null_document");
    }
    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw new JsonException("v2_duplicate_key");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement child in element.EnumerateArray()) RejectDuplicates(child);
    }
    internal static ProbeV2Registration Registration(ProbeV2Options options) => new(2, "single-action-v2", true,
        options.RunId, options.ScenarioId, options.ConfigurationId, options.Stage, options.SourceCommit,
        options.JobName, options.PodName, options.PodUid, options.ServiceAccount);
    internal static ProbeV2Repeat Skipped(int repeat, string action, string admission) => new(repeat, admission, null,
        action, false, 0, null, new Dictionary<string, string>(StringComparer.Ordinal), [], null, null, null,
        new(0, 0, null), null, null, [], [], admission == "not_applicable_no_delivery" ? [] : [admission]);
}
