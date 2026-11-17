using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Statistics;

namespace Parchwell.LoadGen.Common.Monitoring;

internal sealed class NativeMonitoringHost(KafkaNativeMonitoringCoordinator coordinator, string source)
{
    private readonly ConcurrentDictionary<KafkaNativeMonitoringLifetime, byte> _clients = [];
    private readonly ConditionalWeakTable<KafkaStatisticsSession, KafkaNativeMonitoringLifetime> _bridgeClients = new();

    internal KafkaNativeMonitoringLifetime? Begin(KafkaStatisticsIdentity identity)
    {
        try
        {
            KafkaNativeMonitoringLifetime lifetime = coordinator.CreateLifetime(identity);
            _clients.TryAdd(lifetime, 0);
            return lifetime;
        }
        catch (Exception) { coordinator.MarkIncomplete("host_begin_failed"); return null; }
    }

    internal void AttachBridge(KafkaStatisticsSession session, KafkaNativeMonitoringLifetime lifetime)
    {
        try { _bridgeClients.Add(session, lifetime); }
        catch (Exception) { lifetime.MarkIncomplete("bridge_attachment_failed"); }
    }

    internal void CaptureBridge(KafkaStatisticsSession session, string raw)
    {
        if (!session.TryCapture(raw) && _bridgeClients.TryGetValue(session, out KafkaNativeMonitoringLifetime? lifetime))
        {
            try { lifetime.MarkIncomplete("bridge_capture_rejected"); }
            catch (Exception) { Console.Error.WriteLine("native_monitoring_admission_failed"); }
        }
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

    internal Task<bool> WaitForDrainAsync(Task? drain, CancellationToken cancellationToken) => coordinator.WaitForDrainAsync(drain, cancellationToken);

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
