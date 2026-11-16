using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Monitoring;

namespace Parchwell.LoadGen.Tests.Host;

internal sealed class NativeMonitoringFixture : IKafkaNativeMonitoringStore
{
    internal Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);

    public Task CreateAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Objects.Add(path, content.ToArray());
        return Task.CompletedTask;
    }

    internal static NativeMonitoringFixture Register(IServiceCollection services, string run, string scenario, string configuration,
        string job, string podUid, string commit, string source)
    {
        var options = new KafkaNativeMonitoringOptions
        {
            BlobEndpoint = "https://monitoring.example.test/", BlobContainer = "native-monitoring", RunId = run,
            Phase = configuration, ScenarioId = scenario, ConfigurationId = configuration, JobName = job,
            PodUid = podUid, SourceCommit = commit
        };
        NativeMonitoringHost.Register(services, options, source);
        var store = new NativeMonitoringFixture();
        services.AddSingleton<IKafkaNativeMonitoringStore>(store);
        return store;
    }
}
