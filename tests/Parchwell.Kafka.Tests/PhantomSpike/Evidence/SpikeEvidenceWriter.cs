using System.Text.Json;

namespace Parchwell.Kafka.Tests.PhantomSpike.Evidence;

internal static class SpikeEvidenceWriter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static async Task<string> WriteAsync(ExperimentEvidence experiment, CancellationToken cancellationToken = default)
    {
        var configured = Environment.GetEnvironmentVariable("PARCHWELL_SPIKE_EVIDENCE_DIRECTORY");
        var root = string.IsNullOrWhiteSpace(configured) ? Path.GetTempPath() : Path.GetFullPath(configured);
        var directory = Path.Combine(root, $"phantom-spike-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        await WriteJsonAsync(Path.Combine(directory, "experiment.json"), experiment, cancellationToken).ConfigureAwait(false);
        await WriteJsonAsync(Path.Combine(directory, "decision.json"), SpikeEvidence.Decide(experiment), cancellationToken).ConfigureAwait(false);
        var records = new
        {
            experiment.SourceCommit, experiment.Dirty, experiment.Started, experiment.Finished, experiment.Timings, experiment.ImagePins,
            experiment.Failures, experiment.CleanupSucceeded,
            Trials = experiment.Trials.Select(ProjectTrial), CalibrationTrials = experiment.CalibrationTrials.Select(ProjectTrial)
        };
        await WriteJsonAsync(Path.Combine(directory, "trial-records.json"), records, cancellationToken).ConfigureAwait(false);
        return directory;
    }

    private static object ProjectTrial(TrialEvidence trial) => new
    {
        trial.TrialId, trial.Configuration, trial.Timings, trial.Target, trial.Executed, trial.Accepted, trial.SynchronousError,
        trial.Callbacks, trial.Scan, trial.AdvertisedBrokers, trial.ExpectedProducerEndpoint,
        trial.RouteVerified, trial.FaultState, trial.FaultVerified, trial.HealthyControlVerified,
        trial.IndependentObserver, trial.Started, trial.FaultActivated, trial.Submitted, trial.Sealed,
        trial.WarmupTarget, trial.WarmupCallbacks, trial.WarmupScan, trial.Failures, trial.CleanupSucceeded,
        Producer = trial.Producer is { } producer ? new { producer.ManagedVersion, producer.NativeVersion, producer.Errors } : null,
        Infrastructure = trial.Infrastructure is { } infrastructure
            ? new { infrastructure.ProxyState, infrastructure.MetricsBeforeTarget, infrastructure.Metrics } : null
    };

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken).ConfigureAwait(false);
    }
}
