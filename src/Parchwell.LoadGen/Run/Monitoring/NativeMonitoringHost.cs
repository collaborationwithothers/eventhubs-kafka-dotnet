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
    private readonly ConcurrentDictionary<object, KafkaNativeMonitoringLifetime> _clients = new();

    internal KafkaNativeMonitoringLifetime? Begin(object capture, KafkaStatisticsIdentity identity)
    {
        try
        {
            KafkaNativeMonitoringLifetime lifetime = coordinator.CreateLifetime(identity);
            if (!_clients.TryAdd(capture, lifetime)) { lifetime.MarkIncomplete("duplicate_capture"); return null; }
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

    internal Task DrainAsync(CancellationToken cancellationToken) => coordinator.DrainAsync(cancellationToken);

    internal async Task CompleteAsync(bool captureComplete, CancellationToken cancellationToken)
    {
        foreach (KafkaNativeMonitoringLifetime client in _clients.Values)
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
