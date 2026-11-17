using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Statistics;

namespace Parchwell.Verifier.Tests.Support;

internal static class NativeMonitoringTestSupport
{
    internal static string[] Arguments(IEnumerable<string> source)
    {
        string[] args = source.ToArray();
        string? Value(string key) => args.LastOrDefault(value => value.StartsWith("--" + key + "=", StringComparison.Ordinal))?[(key.Length + 3)..];
        string run = Value("Verifier:RunId") ?? "run";
        string phase = Value("Verifier:Phase") ?? "C0";
        string scenario = Value("Verifier:ScenarioId") ?? "A01";
        string configuration = Value("Verifier:ConfigurationId") ?? "C0";
        string job = Value("Verifier:JobName") ?? Value("PARCHWELL_JOB_NAME") ?? "job";
        string pod = Value("Verifier:PodUid") ?? Value("PARCHWELL_POD_UID") ?? "pod";
        string commit = Value("PARCHWELL_SOURCE_COMMIT") ?? Value("Verifier:SourceCommit") ?? "commit";
        return [.. args, "--NativeMonitoring:BlobEndpoint=" + (Value("Verifier:BlobServiceUri") ?? "https://storage.example/"), "--NativeMonitoring:BlobContainer=native-monitoring",
            "--NativeMonitoring:RunId=" + run, "--NativeMonitoring:Phase=" + phase, "--NativeMonitoring:ScenarioId=" + scenario,
            "--NativeMonitoring:ConfigurationId=" + configuration, "--NativeMonitoring:JobName=" + job,
            "--NativeMonitoring:PodUid=" + pod, "--NativeMonitoring:SourceCommit=" + commit];
    }

    internal static void AddConfiguration(Dictionary<string, string?> values)
    {
        foreach (string argument in Arguments(values.Select(pair => "--" + pair.Key + "=" + pair.Value)).Where(value => value.StartsWith("--NativeMonitoring:", StringComparison.Ordinal)))
        {
            int separator = argument.IndexOf('=');
            values[argument[2..separator]] = argument[(separator + 1)..];
        }
    }

    internal sealed class Store : IKafkaNativeMonitoringStore
    {
        internal Dictionary<string, byte[]> Objects { get; } = [];
        public Task CreateAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Objects.Add(path, content.ToArray());
            return Task.CompletedTask;
        }
    }

    internal static void RegisterStore(IServiceCollection services) => services.AddSingleton<IKafkaNativeMonitoringStore>(new Store());
}
