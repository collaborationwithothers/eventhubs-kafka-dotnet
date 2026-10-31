using System.Text.Json;
using Parchwell.Kafka.Authentication;

namespace Parchwell.LoadGen.SingleActionV2;

/// <summary>Contains bounded diagnostics, not evidence of an exchange or broker acceptance.</summary>
internal sealed record ProbeV2AuthenticationMetadata(
    int SchemaVersion, string Mode, bool Synthetic,
    string RunId, string ScenarioId, string ConfigurationId, string Stage,
    string SourceCommit, string JobName, string PodName, string PodUid,
    string ServiceAccount, int Repeat, string ClientId, string Action,
    DateTimeOffset? InitialInstalledExpiresOn,
    KafkaAuthenticationLifecycleSnapshot? Lifecycle,
    IReadOnlyList<ProbeV2ProjectedFileSample> ProjectedFileSamples,
    bool ClientCleanupCompleted, string? CaptureError)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ProbeV2HandlerRefreshMetadata? HandlerRefresh { get; init; }
}

/// <summary>Names one explicit projected-file metadata sample boundary.</summary>
internal sealed record ProbeV2ProjectedFileSample(
    string Boundary, ProjectedTokenFileObservation? Observation);

internal sealed class ProbeV2AuthenticationMetadataCollector
{
    private readonly ProbeV2Options options;
    private readonly ProbeV2Stage stage;
    private readonly int repeat;
    private readonly string clientId;
    private readonly Func<Func<ProjectedTokenFileObservation>> createSampler;
    private readonly List<ProbeV2ProjectedFileSample> samples = new(2);
    private Func<ProjectedTokenFileObservation>? sampler;
    private string? captureError;

    internal ProbeV2AuthenticationMetadataCollector(ProbeV2Options options, ProbeV2Stage stage, int repeat,
        string clientId, Func<Func<ProjectedTokenFileObservation>> createSampler)
    {
        this.options = options;
        this.stage = stage;
        this.repeat = repeat;
        this.clientId = clientId;
        this.createSampler = createSampler;
    }

    internal void BeforeClient()
    {
        ProjectedTokenFileObservation? observation = null;
        try
        {
            sampler = createSampler();
            observation = sampler();
        }
        catch (Exception)
        {
            captureError ??= "projected_observer";
        }
        samples.Add(new("before_client", observation));
    }

    internal ProbeV2AuthenticationMetadata Finish(bool clientConstructed, bool cleanupCompleted,
        Func<KafkaAuthenticationLifecycleSnapshot>? readLifecycle,
        ProbeV2HandlerRefreshMetadata? handlerRefresh = null)
    {
        KafkaAuthenticationLifecycleSnapshot? lifecycle = null;
        DateTimeOffset? initialInstalledExpiresOn = null;
        if (!cleanupCompleted)
        {
            samples.Add(new("after_client_cleanup", null));
            captureError = "client_cleanup_incomplete";
        }
        else
        {
            ProjectedTokenFileObservation? observation = null;
            if (sampler is not null)
            {
                try { observation = sampler(); }
                catch (Exception) { captureError ??= "projected_observer"; }
            }
            samples.Add(new("after_client_cleanup", observation));

            if (!clientConstructed || readLifecycle is null)
            {
                captureError ??= "session_unavailable";
            }
            else
            {
                try
                {
                    lifecycle = readLifecycle();
                    initialInstalledExpiresOn = lifecycle.Events.FirstOrDefault(value => value.Stage == "installation_succeeded")?.InstalledExpiresOn;
                }
                catch (Exception)
                {
                    lifecycle = null;
                    initialInstalledExpiresOn = null;
                    captureError ??= "lifecycle_snapshot";
                }
            }
        }

        return new(options.TestHandlerRefreshPath ? 2 : 1,
            options.TestHandlerRefreshPath ? "single-action-v2-handler-refresh-path" : "single-action-v2-authentication-metadata", true,
            options.RunId, options.ScenarioId, options.ConfigurationId, options.Stage,
            options.SourceCommit, options.JobName, options.PodName, options.PodUid,
            options.ServiceAccount, repeat, clientId, stage.Action,
            initialInstalledExpiresOn, lifecycle, samples.ToArray(), cleanupCompleted, captureError)
        { HandlerRefresh = options.TestHandlerRefreshPath ? handlerRefresh : null };
    }
}

internal static class ProbeV2AuthenticationMetadataWriter
{
    internal const string Marker = "PARCHWELL_PROBE_V2_AUTHENTICATION_METADATA ";
    internal const int MaximumJsonBytes = 262144;

    internal static byte[] Encode(ProbeV2AuthenticationMetadata value)
    {
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, ProbeV2Wire.JsonOptions);
            if (bytes.Length <= MaximumJsonBytes) return bytes;
            return EncodeFallback(value, "serialization_limit");
        }
        catch (Exception)
        {
            return EncodeFallback(value, "serialization");
        }
    }

    internal static async Task TryWriteAsync(TextWriter writer, ProbeV2AuthenticationMetadata value,
        int ioSeconds, TimeProvider clock)
    {
        try
        {
            byte[] bytes = Encode(value);
            if (bytes.Length == 0) return;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(ioSeconds), clock);
            string line = Marker + System.Text.Encoding.UTF8.GetString(bytes);
            await writer.WriteLineAsync(line.AsMemory(), deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
            await writer.FlushAsync(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Diagnostics are best effort and never affect scientific capture or service outcome.
        }
    }

    private static byte[] EncodeFallback(ProbeV2AuthenticationMetadata value, string error)
    {
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value with
            {
                InitialInstalledExpiresOn = null,
                Lifecycle = null,
                HandlerRefresh = null,
                ProjectedFileSamples = Array.Empty<ProbeV2ProjectedFileSample>(),
                CaptureError = error
            }, ProbeV2Wire.JsonOptions);
            return bytes.Length <= MaximumJsonBytes ? bytes : [];
        }
        catch (Exception)
        {
            return [];
        }
    }
}
