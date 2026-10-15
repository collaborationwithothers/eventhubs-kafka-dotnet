using System.Diagnostics;
using System.Text;
using Parchwell.Kafka.Tests.PhantomSpike.Infrastructure;

namespace Parchwell.Kafka.Tests.PhantomSpike.Experiment;

/// <summary>
/// Runs the frozen spike matrix, each trial with a fresh producer, topic, identity and proxy.
/// Healthy controls run first; a failed control or a failed cleanup stops the remaining trials
/// (docs/operations/phantom-spike/README.md, Frozen experiment).
/// </summary>
internal sealed class SpikeRunner(TimeProvider clock)
{
    // Approved design budgets, not measured broker performance.
    private static readonly TimeSpan ExperimentBudget = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan TrialBudget = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DiagnosticBudget = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CleanupBudget = TimeSpan.FromSeconds(30);

    internal async Task<ExperimentEvidence> RunAsync(CancellationToken cancellationToken = default)
    {
        var started = clock.GetUtcNow();
        using var experimentDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        experimentDeadline.CancelAfter(ExperimentBudget);
        var timings = new TimingSettings();
        var trials = new List<TrialEvidence>();
        var failures = new List<string>();
        var commit = string.Empty;
        var dirty = true;
        var cleanupSucceeded = true;
        BrokerEnvironment? environment = null;
        try
        {
            commit = await ReadGitAsync("rev-parse", "HEAD", experimentDeadline.Token).ConfigureAwait(false);
            dirty = !string.IsNullOrEmpty(await ReadGitAsync("status", "--porcelain", experimentDeadline.Token).ConfigureAwait(false));
            environment = await BrokerEnvironment.StartAsync(experimentDeadline.Token).ConfigureAwait(false);
            string? stopReason = null;
            foreach (var configuration in Matrix())
            {
                if (experimentDeadline.IsCancellationRequested)
                    stopReason ??= "The overall experiment deadline or caller cancellation stopped the matrix.";
                if (stopReason is not null)
                {
                    trials.Add(NotExecuted(configuration, timings, environment, stopReason));
                    continue;
                }

                var trial = await RunTrialAsync(environment, configuration, timings, experimentDeadline.Token).ConfigureAwait(false);
                trials.Add(trial);
                if (configuration.Fault == SpikeFault.None && !ControlSucceeded(trial))
                {
                    // Recalibration is optional. Failed controls do not supply a trustworthy successful
                    // latency sample, so changing timings from these failures would invent a measurement.
                    stopReason = "Healthy control matrix was incomplete; no timing revision was applied. The fault matrix was not started.";
                    failures.Add(stopReason);
                }
                if (!trial.CleanupSucceeded)
                {
                    stopReason = "A trial cleanup failed; the remaining matrix was not started.";
                    failures.Add(stopReason);
                }
            }
        }
        catch (Exception exception)
        {
            if (exception is BrokerStartupException startupFailure)
                cleanupSucceeded = startupFailure.CleanupEstablished;
            failures.Add($"Experiment setup or execution failed: {exception}");
            foreach (var configuration in Matrix().Where(configuration => !trials.Any(trial => trial.Configuration == configuration)))
                trials.Add(NotExecuted(configuration, timings, environment, "Experiment setup or execution failed before this trial."));
        }
        finally
        {
            if (environment is not null)
            {
                try
                {
                    await environment.DisposeAsync().AsTask().WaitAsync(CleanupBudget, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    cleanupSucceeded = false;
                    failures.Add($"Environment cleanup failed or exceeded its separate safety budget: {exception}");
                }
            }
        }

        return new ExperimentEvidence(commit, dirty, started, clock.GetUtcNow(), timings, trials, [], failures, cleanupSucceeded,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Kafka"] = BrokerEnvironment.KafkaImage,
                ["Toxiproxy"] = BrokerEnvironment.ProxyImage,
            });
    }

    private async Task<TrialEvidence> RunTrialAsync(BrokerEnvironment environment, TrialConfiguration configuration,
        TimingSettings timings, CancellationToken experimentToken)
    {
        var started = clock.GetUtcNow();
        var trialId = Guid.NewGuid().ToString("N");
        var target = Message($"phantom-{trialId}", Guid.NewGuid().ToString("N"));
        var warmup = Message(target.Topic, target.InstanceId);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(experimentToken);
        deadline.CancelAfter(TrialBudget);
        var failures = new List<string>();
        ProducerProbe? producer = null;
        DirectObserver? observer = null;
        PendingProduce? pending = null;
        PendingProduce? warmupPending = null;
        ObservationScan? scan = null;
        ObservationScan? warmupScan = null;
        InfrastructureDiagnostics? infrastructure = null;
        ProducerDiagnostics? diagnostics = null;
        IReadOnlyList<string> brokers = [];
        string? faultState = null;
        string? metricsBeforeTarget = null;
        DateTimeOffset? activated = null;
        DateTimeOffset? submitted = null;
        var healthy = false;
        var routeVerified = false;
        var faultVerified = false;
        var cleanupSucceeded = true;
        var targetLogStart = 0;
        var sealedAt = started;
        try
        {
            await environment.ResetProxyAsync(trialId, deadline.Token).ConfigureAwait(false);
            await DirectObserver.CreateTopicAsync(environment.ObserverBootstrapServers, target.Topic, deadline.Token).ConfigureAwait(false);
            observer = new DirectObserver(environment.ObserverBootstrapServers, target.Topic, clock);
            producer = new ProducerProbe(environment.ProducerBootstrapServers, configuration, timings, clock);
            brokers = producer.MetadataBrokerEndpoints();
            deadline.Token.ThrowIfCancellationRequested();
            // The producer also connects to brokers it learns from metadata (README, Frozen experiment), so
            // every advertised broker must be the proxy or the fault would not sit on the producer's path.
            if (brokers.Count == 0 || brokers.Any(endpoint => !string.Equals(endpoint, environment.ProducerBootstrapServers, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Producer metadata advertised a route other than the trial proxy.");

            var warmStart = observer.GetHighWatermark(deadline.Token);
            warmupPending = producer.ProduceAttempt(warmup);
            if (!warmupPending.Accepted)
                throw new InvalidOperationException($"Healthy warmup was synchronously rejected: {warmupPending.SynchronousError}");
            var warmDelivery = await warmupPending.Completion.WaitAsync(deadline.Token).ConfigureAwait(false);
            await producer.DrainAsync(deadline.Token).ConfigureAwait(false);
            warmupScan = await observer.ScanAsync(warmStart, warmup, deadline.Token).ConfigureAwait(false);
            healthy = !warmDelivery.IsError && warmupPending.Callbacks.Count == 1 && warmupScan.Complete
                && warmupScan.Failure is null && warmupScan.Records.Count == 1 && Matches(warmupScan.Records[0], warmup);
            if (!healthy)
                throw new InvalidOperationException("The independent direct observer did not confirm the successful healthy warmup.");

            // The asynchronous native statistics stream must provide real evidence, including for
            // fast controls. Waiting for a sample is bounded by the original trial deadline.
            while (producer.SnapshotDiagnostics().Statistics.Count == 0)
                await Task.Delay(TimeSpan.FromMilliseconds(100), clock, deadline.Token).ConfigureAwait(false);
            var warmDiagnostics = producer.SnapshotDiagnostics();
            routeVerified = warmDiagnostics.Logs.Any(line => NativeProduceSend(line, environment.ProducerBootstrapServers));
            if (!routeVerified)
                throw new InvalidOperationException("The successful warmup lacks native ProduceRequest evidence on the advertised proxy endpoint.");

            // Record the observation start after the distinct warmup, while the path is healthy.
            var targetStart = observer.GetHighWatermark(deadline.Token);
            metricsBeforeTarget = (await environment.CaptureDiagnosticsAsync(deadline.Token).ConfigureAwait(false)).Metrics;
            faultState = await environment.ApplyFaultAsync(configuration.Fault, timings, deadline.Token).ConfigureAwait(false);
            activated = clock.GetUtcNow();
            targetLogStart = producer.SnapshotDiagnostics().Logs.Count;
            submitted = clock.GetUtcNow();
            pending = producer.ProduceAttempt(target);
            if (!pending.Accepted)
                throw new InvalidOperationException($"Target Produce was synchronously rejected: {pending.SynchronousError}");
            await pending.Completion.WaitAsync(deadline.Token).ConfigureAwait(false);
            await producer.DrainAsync(deadline.Token).ConfigureAwait(false);
            // This fixed-window diagnostic scan makes no ADR-013 late-write finality claim.
            scan = await observer.ScanAsync(targetStart, target, deadline.Token).ConfigureAwait(false);
            if (!scan.Complete || scan.Failure is not null)
                failures.Add($"The independent direct observation window was incomplete: {scan.Failure}");
        }
        catch (Exception exception)
        {
            failures.Add($"Trial failed or exceeded its original deadline: {exception}");
        }
        finally
        {
            // Seal diagnostics before disposing the producer or changing proxy state. A callback
            // caused by cleanup cannot become evidence of the submitted attempt's outcome.
            sealedAt = clock.GetUtcNow();
            diagnostics = producer?.SnapshotDiagnostics();
            try
            {
                using var diagnosticDeadline = new CancellationTokenSource(DiagnosticBudget);
                infrastructure = await environment.CaptureDiagnosticsAsync(diagnosticDeadline.Token).ConfigureAwait(false);
                infrastructure = infrastructure with { MetricsBeforeTarget = metricsBeforeTarget };
            }
            catch (Exception exception)
            {
                failures.Add($"Infrastructure diagnostics could not be retained: {exception}");
            }
        }

        var callbacks = pending?.Callbacks ?? [];
        var warmupCallbacks = warmupPending?.Callbacks ?? [];
        if (submitted is not null && diagnostics is not null && faultState is not null)
        {
            var targetTraffic = diagnostics.Logs.Skip(targetLogStart).Any(line =>
                line.Contains(environment.ProducerBootstrapServers, StringComparison.OrdinalIgnoreCase)
                && (line.Contains("SEND", StringComparison.Ordinal) || line.Contains("CONNECT", StringComparison.Ordinal)
                    || line.Contains("FAIL", StringComparison.Ordinal)));
            // The proxy GET validates the exact toxic. Native traces retain actual traffic on the
            // verified route, including reconnect failure before a ProduceRequest can be sent.
            faultVerified = routeVerified && targetTraffic;
            if (!faultVerified)
                failures.Add("No native target traffic was recorded on the activated proxy route.");
        }

        var cleanupTask = Task.Run(() =>
        {
            var cleanupFailures = new List<Exception>();
            DisposeClient(observer, cleanupFailures);
            DisposeClient(producer, cleanupFailures);
            if (cleanupFailures.Count != 0)
                throw new AggregateException("Trial client cleanup failed.", cleanupFailures);
        }, CancellationToken.None);
        try
        {
            await cleanupTask.WaitAsync(CleanupBudget, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            cleanupSucceeded = false;
            failures.Add($"Trial cleanup failed or exceeded its separate safety budget: {exception}");
            _ = cleanupTask.ContinueWith(task => _ = task.Exception,
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        return new TrialEvidence(trialId, configuration, timings, target, pending?.Accepted ?? false,
            pending?.SynchronousError, callbacks, scan, brokers, faultState, diagnostics, infrastructure,
            failures, cleanupSucceeded, started, sealedAt, environment.ProducerBootstrapServers,
            routeVerified, faultVerified, healthy, observer is not null, activated, submitted,
            warmup, warmupCallbacks, warmupScan);
    }

    private static void DisposeClient(IDisposable? client, List<Exception> failures)
    {
        try
        {
            client?.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private TrialEvidence NotExecuted(TrialConfiguration configuration, TimingSettings timings,
        BrokerEnvironment? environment, string reason)
    {
        var trialId = Guid.NewGuid().ToString("N");
        var timestamp = clock.GetUtcNow();
        return new TrialEvidence(trialId, configuration, timings,
            Message($"phantom-{trialId}", Guid.NewGuid().ToString("N")), false, null, [], null, [], null,
            null, null, [reason], true, timestamp, timestamp, environment?.ProducerBootstrapServers, Executed: false);
    }

    private static bool ControlSucceeded(TrialEvidence trial) => trial.Failures.Count == 0 && trial.Accepted
        && trial.Callbacks.Count == 1 && !trial.Callbacks[0].IsError && trial.Scan is { Complete: true, Failure: null }
        && trial.Scan.Records.Count == 1 && Matches(trial.Scan.Records[0], trial.Target);

    private static bool Matches(ConsumedObservation record, AttemptMessage target) =>
        record.InstanceId == target.InstanceId && record.AttemptId == target.AttemptId && record.Topic == target.Topic
        && record.Key.AsSpan().SequenceEqual(target.Key) && record.Payload.AsSpan().SequenceEqual(target.Payload);

    private static bool NativeProduceSend(string line, string endpoint) =>
        line.Contains(endpoint, StringComparison.OrdinalIgnoreCase)
        && line.Contains("SEND", StringComparison.Ordinal) && line.Contains("ProduceRequest", StringComparison.Ordinal);

    private static AttemptMessage Message(string topic, string instanceId)
    {
        var attemptId = Guid.NewGuid().ToString("N");
        return new AttemptMessage(topic, instanceId, attemptId, Encoding.UTF8.GetBytes($"synthetic-key-{attemptId}"),
            Encoding.UTF8.GetBytes($"synthetic-payload-{attemptId}"));
    }

    private static IEnumerable<TrialConfiguration> Matrix()
    {
        foreach (var fault in new[] { SpikeFault.None, SpikeFault.Latency, SpikeFault.Timeout, SpikeFault.ResetPeer })
        foreach (var retries in new[] { 0, int.MaxValue })
        foreach (var repeat in Enumerable.Range(1, 5))
            yield return new TrialConfiguration(retries, fault, repeat);
    }

    private static async Task<string> ReadGitAsync(string command, string argument, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            }
        };
        process.StartInfo.ArgumentList.Add(command);
        process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start())
            throw new InvalidOperationException("Git metadata process did not start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Git metadata command failed: {await error.ConfigureAwait(false)}");
        return (await output.ConfigureAwait(false)).Trim();
    }
}
