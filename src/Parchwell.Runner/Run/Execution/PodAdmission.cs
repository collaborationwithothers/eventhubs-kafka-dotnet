using System.Text.Json;

namespace Parchwell.Runner.Run.Execution;

public sealed record ProducerPod(string Name, string Uid);
public sealed record PodWatchEvent(string ResourceVersion, string Type, ProducerPod? Pod);

public sealed class PodAdmission(string runId, string initialResourceVersion)
{
    private readonly Dictionary<string, ProducerPod> pods = new(StringComparer.Ordinal);
    private readonly List<PodWatchEvent> events = [];
    private bool watchLost;
    private readonly object gate = new();
    public string RunId { get; } = !string.IsNullOrWhiteSpace(runId) ? runId : throw new ArgumentException("run_id_required", nameof(runId));
    public string InitialResourceVersion { get; } = !string.IsNullOrWhiteSpace(initialResourceVersion) ? initialResourceVersion : throw new ArgumentException("resource_version_required", nameof(initialResourceVersion));
    public IReadOnlyList<PodWatchEvent> Events { get { lock (gate) return events.ToArray(); } }
    public ProducerPod Producer { get { lock (gate) return !watchLost && pods.Count == 1 ? pods.Values.Single() : throw new InvalidDataException("producer_history_not_unique"); } }

    public void Observe(PodWatchEvent observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        lock (gate)
        {
        if (events.Count >= 10000) throw new InvalidDataException("pod_watch_limit");
        events.Add(observation);
        if (observation.Type is "ERROR" or "LOST" || string.IsNullOrWhiteSpace(observation.ResourceVersion)) watchLost = true;
        if (observation.Pod is { } pod)
        {
            if (string.IsNullOrWhiteSpace(pod.Name) || string.IsNullOrWhiteSpace(pod.Uid)) throw new InvalidDataException("pod_identity_missing");
            if (pods.TryGetValue(pod.Uid, out ProducerPod? existing) && existing != pod) throw new InvalidDataException("pod_identity_changed");
            pods[pod.Uid] = pod;
        }
        if (watchLost || pods.Count > 1) throw new InvalidDataException("producer_history_not_unique");
        }
    }

    public byte[] Release(IReadOnlyList<string> brokers, string generatedValuesSha256)
    {
        ProducerPod pod = Producer;
        if (brokers.Count is < 1 or > 64 || generatedValuesSha256.Length != 64) throw new InvalidDataException("admission_inputs");
        return JsonSerializer.SerializeToUtf8Bytes(new { schema_version = 1, run_id = RunId, pod_name = pod.Name,
            pod_uid = pod.Uid, expected_broker_names = brokers, generated_values_sha256 = generatedValuesSha256, release = true });
    }
}
