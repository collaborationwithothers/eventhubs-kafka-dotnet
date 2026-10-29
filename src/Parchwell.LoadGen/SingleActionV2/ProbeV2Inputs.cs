using System.Text.Json;
using System.Text.RegularExpressions;

namespace Parchwell.LoadGen.SingleActionV2;

internal sealed partial class ProbeV2Inputs(ProbeV2Options options, TimeProvider clock)
{
    private static readonly int[] Ordinals = [1, 2, 3, 4, 5];
    private readonly Dictionary<string, ProbeV2CaptureFile?> cache = new(StringComparer.Ordinal);

    internal async Task<(string Admission, ProbeV2Requested? Requested)> AdmitAsync(ProbeV2Stage stage, int repeat, CancellationToken token)
    {
        byte[] key = stage.Key(options, repeat);
        byte[]? value = stage.Value(options, repeat);
        var requested = new ProbeV2Requested(stage.Topic, stage.PartitionMode, stage.Partition, null,
            Convert.ToBase64String(key), value is null ? null : Convert.ToBase64String(value));
        if (stage.SourceConfiguration is null || stage.SourceStage is null) return ("required", requested);
        ProbeV2CaptureFile? source = await ReadAsync(stage.SourceConfiguration, stage.SourceStage, token).ConfigureAwait(false);
        ProbeV2Repeat? slot = Slot(source, repeat);
        if (slot is null || !ExpectedSource(slot, stage.SourceConfiguration, stage.SourceStage, repeat)) return ("not_attempted_dependency", null);
        if (options.ScenarioId == "E01" && options.Stage is "action" or "read")
        {
            ProbeV2CaptureFile? seed = await ReadAsync(options.ConfigurationId, "seed", token).ConfigureAwait(false);
            ProbeV2CaptureFile? seedread = await ReadAsync(options.ConfigurationId, "seedread", token).ConfigureAwait(false);
            if (Slot(seed, repeat) is not { } seedSlot || !ExpectedSource(seedSlot, options.ConfigurationId, "seed", repeat) || !ReadMatches(Slot(seedread, repeat), seedSlot)) return ("not_attempted_dependency", null);
        }
        if (options.ScenarioId == "F06" && options.Stage == "action" && options.ServiceAccount == "enrichment-consumer")
        {
            ProbeV2CaptureFile? control = await ReadAsync(stage.SourceConfiguration, "action", token).ConfigureAwait(false);
            if (!ReadMatches(Slot(control, repeat), slot)) return ("not_attempted_dependency", null);
        }
        if (stage.Action == "produce_one")
            return HasLocation(slot) ? ("required", requested) : ("not_attempted_dependency", null);
        if (HasLocation(slot))
        {
            ProbeV2Delivery delivery = slot.Delivery!;
            if (delivery.Topic != stage.Topic || slot.Requested?.KeyBase64 != requested.KeyBase64)
                return ("not_attempted_dependency", null);
            return ("required", requested with { PartitionMode = "explicit", Partition = delivery.Partition, Offset = delivery.Offset });
        }
        if (options.Stage == "read" && slot.Terminal && slot.Result == "error" && ValidError(slot.Error))
            return ("not_applicable_no_delivery", null);
        return ("not_attempted_dependency", null);
    }
    private bool ExpectedSource(ProbeV2Repeat slot, string configuration, string stage, int repeat)
    {
        ProbeV2Stage source = ProbeV2Stage.For(options.ScenarioId, configuration, stage);
        var sourceOptions = new ProbeV2Options { ScenarioId = options.ScenarioId, ConfigurationId = configuration, RunId = options.RunId, Stage = stage };
        byte[]? value = source.Value(sourceOptions, repeat);
        return slot.Requested is { } request && slot.Action == source.Action && request.Topic == source.Topic &&
            request.KeyBase64 == Convert.ToBase64String(source.Key(sourceOptions, repeat)) &&
            request.ValueBase64 == (value is null ? null : Convert.ToBase64String(value)) && request.PartitionMode == source.PartitionMode &&
            (source.Partition is null || request.Partition == source.Partition) && request.Offset is null;
    }
    internal static bool HasLocation(ProbeV2Repeat? slot) => slot is { Terminal: true, Requested: not null } && slot.Delivery is { Error: null, PersistenceStatus: "Persisted", Partition: >= 0, Offset: >= 0 };
    internal static bool ValidError(ProbeV2Error? error) => error is not null && error.Code != 0 &&
        error.Origin is "construction" or "local" or "broker" && error.ObservedElapsedMs >= 0 && error.Sequence >= 0;
    internal static bool ReadMatches(ProbeV2Repeat? reader, ProbeV2Repeat? producer) => HasLocation(producer) &&
        reader is { Admission: "required", Terminal: true, Result: "success", Error: null } && reader.ReadObservation.Record is { } record &&
        record.Topic == producer!.Delivery!.Topic && record.Partition == producer.Delivery.Partition && record.Offset == producer.Delivery.Offset &&
        record.KeyBase64 == producer.Requested?.KeyBase64 && record.ValueBase64 == producer.Requested?.ValueBase64;
    private static ProbeV2Repeat? Slot(ProbeV2CaptureFile? capture, int repeat) => capture?.Repeats.SingleOrDefault(value => value.Repeat == repeat && value.Admission == "required");

    private async Task<ProbeV2CaptureFile?> ReadAsync(string configuration, string stage, CancellationToken token)
    {
        string job = ProbeV2Options.Job(options.RunId, options.ScenarioId, configuration, stage);
        if (cache.TryGetValue(job, out ProbeV2CaptureFile? cached)) return cached;
        ProbeV2CaptureFile? capture = null;
        try
        {
            ProbeV2Registration registration = ProbeV2Wire.Read<ProbeV2Registration>(await BytesAsync(job, "registration.json", options.Limits.MaxRegistrationBytes, token).ConfigureAwait(false), options.Limits.MaxRegistrationBytes);
            ProbeV2CaptureFile value = ProbeV2Wire.Read<ProbeV2CaptureFile>(await BytesAsync(job, "capture.json", options.Limits.MaxCaptureBytes, token).ConfigureAwait(false), options.Limits.MaxCaptureBytes);
            ProbeV2Pod pod = ProbeV2Wire.Read<ProbeV2Pod>(await BytesAsync(job, "pod.json", options.Limits.MaxPodBytes, token).ConfigureAwait(false), options.Limits.MaxPodBytes);
            string account = ProbeV2Stage.For(options.ScenarioId, configuration, stage).Account;
            if (registration.SchemaVersion != 2 || registration.Mode != "single-action-v2" || !registration.Synthetic ||
                registration.RunId != options.RunId || registration.ScenarioId != options.ScenarioId || registration.ConfigurationId != configuration ||
                registration.Stage != stage || registration.SourceCommit != options.SourceCommit || registration.JobName != job || registration.ServiceAccount != account ||
                !registration.PodName.StartsWith(job + "-", StringComparison.Ordinal) || !Guid.TryParse(registration.PodUid, out _) ||
                value.SchemaVersion != registration.SchemaVersion || value.Mode != registration.Mode || !value.Synthetic ||
                value.RunId != registration.RunId || value.ScenarioId != registration.ScenarioId || value.ConfigurationId != registration.ConfigurationId ||
                value.Stage != registration.Stage || value.SourceCommit != registration.SourceCommit || value.JobName != registration.JobName ||
                value.PodName != registration.PodName || value.PodUid != registration.PodUid || value.ServiceAccount != registration.ServiceAccount ||
                value.HandlerExitCode != 0 || !value.CleanupCompleted || !value.HostCleanupCompleted || value.Repeats is null || value.Repeats.Count != 5 ||
                value.Repeats.Any(slot => slot is null) || !value.Repeats.Select(slot => slot.Repeat).Order().SequenceEqual(Ordinals) ||
                value.Repeats.Any(slot => slot is null || slot.Settings is null || slot.OmittedKeys is null || slot.ClientErrors is null || slot.Statistics is null || slot.Diagnostics is null || slot.ReadObservation is null || slot.Admission == "required" && slot.ClientId != value.PodName + "-r" + slot.Repeat) ||
                pod.SchemaVersion != 2 || pod.JobName != job || pod.OwnerKind != "Job" || pod.OwnerName != job ||
                pod.ServiceAccount != account || pod.PodName != registration.PodName || pod.PodUid != registration.PodUid || !DigestPattern().IsMatch(pod.ImageDigest))
                throw new InvalidDataException("v2_dependency_binding");
            capture = value;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or InvalidOperationException or ArgumentException or OperationCanceledException)
        {
            if (token.IsCancellationRequested) throw;
        }
        cache.Add(job, capture);
        return capture;
    }
    private async Task<byte[]> BytesAsync(string job, string name, int maximum, CancellationToken token)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Limits.IoSeconds), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
        string directory = Path.Combine(options.InputRoot, job);
        string path = Path.Combine(directory, name);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length <= 0 || stream.Length > maximum) throw new InvalidDataException("v2_input_limit");
        byte[] bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, linked.Token).ConfigureAwait(false);
        if (stream.ReadByte() != -1) throw new InvalidDataException("v2_input_changed");
        return bytes;
    }
    [GeneratedRegex("^sha256:[0-9a-f]{64}$")]
    private static partial Regex DigestPattern();
}
