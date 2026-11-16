using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Statistics;

namespace Parchwell.LoadGen.Run.Monitoring;

internal sealed class NativeMonitoringHost(KafkaNativeMonitoringCoordinator coordinator, string source)
{
    private readonly ConcurrentDictionary<KafkaNativeMonitoringLifetime, byte> _clients = [];

    internal KafkaNativeMonitoringLifetime? Begin(KafkaStatisticsIdentity identity)
    {
        try
        {
            KafkaNativeMonitoringLifetime lifetime = coordinator.CreateLifetime(identity);
            _clients.TryAdd(lifetime, 0);
            return lifetime;
        }
        catch (Exception) { return null; }
    }

    internal void Capture(KafkaNativeMonitoringLifetime lifetime, string raw, long sequence)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(raw);
            lifetime.CaptureExisting(raw, new(source, sequence, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes))));
        }
        catch (Exception) { lifetime.MarkIncomplete("host_reference_failed"); }
    }

    internal void ObserveExisting(KafkaNativeMonitoringLifetime lifetime, KafkaStatisticsReport report, long sequence)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(report.RawJson);
            lifetime.ObserveExisting(report, new(source, sequence, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes))));
        }
        catch (Exception) { lifetime.MarkIncomplete("host_reference_failed"); }
    }

    internal void RebindSource(string publishedSource)
    {
        foreach (KafkaNativeMonitoringLifetime client in _clients.Keys)
            client.RebindExistingSource(source, publishedSource);
        source = publishedSource;
    }

    internal void MarkIncomplete() => coordinator.MarkIncomplete("host_capture_incomplete");

    internal Task DrainAsync(CancellationToken cancellationToken) => coordinator.DrainAsync(cancellationToken);

    internal async Task CompleteAsync(bool captureComplete, CancellationToken cancellationToken)
    {
        if (!captureComplete) coordinator.MarkIncomplete("host_capture_incomplete");
        foreach (KafkaNativeMonitoringLifetime client in _clients.Keys)
            client.Complete(captureComplete, "unknown");
        await coordinator.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static void Register(IServiceCollection services, KafkaNativeMonitoringOptions options, string source)
    {
        if (!options.IsValid(out string? failure) || options.BlobContainer != "native-monitoring")
            throw new InvalidOperationException(failure ?? "native_monitoring_container_invalid");
        services.AddParchwellNativeMonitoring();
        services.AddSingleton<IKafkaNativeMonitoringStore>(_ => new NativeMonitoringBlobStore(
            new BlobServiceClient(new Uri(options.BlobEndpoint), new WorkloadIdentityCredential(), new BlobClientOptions { Retry = { MaxRetries = 0 } })));
        services.AddSingleton(provider => new NativeMonitoringHost(
            provider.GetRequiredService<KafkaNativeMonitoringFactory>().Create(options.CreateIdentity()), source));
    }
}
