using System.Globalization;
using System.Text;
using Confluent.Kafka;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.Common.SingleAction;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.IdleObservation;

internal sealed class IdleObservationWorker(IdleObservationOptions options, IdleObservationSessionFactory sessions,
    IIdleObservationPublication publication, TimeProvider clock, TextWriter? clientLogOutput = null)
{
    internal const string Topic = "document-events";
    internal const int Partition = 0;
    internal const int PayloadBytes = 1000;
    private readonly object _logGate = new();
    private int _cleanupFailures;

    internal static void RequireStatisticsInterval(ResolvedRoleConfiguration resolved, IdleObservationDefinition definition)
    {
        if (!resolved.Settings.TryGetValue("statistics.interval.ms", out var setting) ||
            !StringComparer.Ordinal.Equals(setting.Value, definition.StatisticsIntervalMs.ToString(CultureInfo.InvariantCulture)))
            throw new InvalidOperationException("idle_observation_statistics_interval_mismatch");
    }

    internal async Task ExecuteAsync(ResolvedScenario scenario, CancellationToken cancellationToken)
    {
        IdleObservationDefinition definition = scenario.Definition.IdleObservation!;
        ResolvedRoleConfiguration resolved = scenario.Configurations[options.ConfigurationId].Roles[KafkaClientRole.LoadGenProducer];
        RequireStatisticsInterval(resolved, definition);
        string prefix = IdleObservationPublication.Prefix(options);
        await WriteAsync(prefix + $"registrations/{options.PodName}.json", IdleObservationPublication.Registration(options),
            options.MaxRegistrationBytes, options.RegistrationUploadSeconds, cancellationToken).ConfigureAwait(false);
        IdleObservationRepeat[] repeats = await Task.WhenAll(Enumerable.Range(1, 5)
            .Select(repeat => CaptureClientAsync(definition, resolved, repeat, cancellationToken))).ConfigureAwait(false);
        var capture = new IdleObservationCaptureFile(1, "idle-observation", true, options.RunKind, IdleObservationPublication.Identity(options),
            options.JobName, options.PodName, options.PodUid, options.SourceCommit,
            typeof(ProducerConfig).Assembly.GetName().Version!.ToString(), Library.VersionString, Topic, Partition, PayloadBytes,
            definition.IdleSeconds, definition.StatisticsIntervalMs, definition.MaxStatisticsReports, repeats);
        await WriteAsync(prefix + $"instances/{options.PodName}/capture.json", capture,
            options.MaxCaptureBytes, options.CaptureUploadSeconds, cancellationToken).ConfigureAwait(false);
        if (_cleanupFailures != 0) throw new InvalidOperationException("idle_observation_client_cleanup_failed");
        if (repeats.Any(repeat => repeat.Diagnostics.Contains("capture_limit", StringComparer.Ordinal)))
            throw new InvalidOperationException("idle_observation_capture_budget_exhausted");
    }

    internal async Task<IdleObservationRepeat> CaptureClientAsync(IdleObservationDefinition definition, ResolvedRoleConfiguration resolved,
        int repeat, CancellationToken token)
    {
        long started = clock.GetTimestamp();
        long startedUnix = clock.GetUtcNow().ToUnixTimeMilliseconds();
        var logs = new IdleObservationClientLogBudget(repeat);
        var capture = new IdleObservationCapture(options, clock, started, definition.MaxStatisticsReports, logs);
        string clientId = $"{options.PodName}-r{repeat}";
        var settings = resolved.Settings.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);
        settings["client.id"] = clientId;
        string[] omitted = resolved.Omissions.Select(value => value.Key).Concat(resolved.ScenarioOmissions.Select(value => value.Key))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        IdleObservationProduce? first = null;
        IdleObservationProduce? second = null;
        long? idleStarted = null;
        long? idleEnded = null;
        IProducer<byte[], byte[]>? producer = null;
        try
        {
            if (settings.Count > options.MaxSettingsKeys || settings.Sum(pair => Encoding.UTF8.GetByteCount(pair.Key) + Encoding.UTF8.GetByteCount(pair.Value)) > options.MaxSettingsBytes)
                capture.CaptureLimit();
            else
            {
                bool constructed = true;
                try { producer = sessions.Producer(settings, capture, token); }
                catch (KafkaException exception)
                {
                    constructed = false;
                    first = new(1, Key(options.ScenarioId, options.ConfigurationId, options.RunId, repeat, 1), 0, true, "error",
                        new((int)exception.Error.Code, exception.Error.IsFatal, "construction", exception.Error.Reason),
                        capture.ElapsedMs, clock.GetUtcNow().ToUnixTimeMilliseconds());
                }
                if (constructed)
                {
                    first = await ProduceAsync(producer!, capture, repeat, 1, token).ConfigureAwait(false);
                    idleStarted = capture.ElapsedMs;
                    await WaitAsync(capture, definition.IdleSeconds, token).ConfigureAwait(false);
                    idleEnded = capture.ElapsedMs;
                    second = await ProduceAsync(producer!, capture, repeat, 2, token).ConfigureAwait(false);
                    await WaitAsync(capture, options.PostTerminalStatisticsSeconds, token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            capture.Diagnostic("client:" + exception.GetType().Name);
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(options.CleanupSeconds), clock);
            try
            {
                if (producer is not null) await Task.Run(producer.Dispose, CancellationToken.None).WaitAsync(cleanup.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref _cleanupFailures);
                capture.Diagnostic("client_cleanup_failed:" + exception.GetType().Name);
            }
            finally
            {
                lock (_logGate)
                    foreach (string line in logs.Seal()) (clientLogOutput ?? Console.Error).Write(line);
            }
        }
        var snapshot = capture.Snapshot();
        return new(repeat, clientId, settings, omitted, startedUnix, first, idleStarted, idleEnded, second,
            snapshot.Errors, snapshot.Dropped, snapshot.Statistics, snapshot.Diagnostics);
    }

    private async Task WaitAsync(IdleObservationCapture capture, int seconds, CancellationToken token)
    {
        long start = clock.GetTimestamp();
        while (clock.GetElapsedTime(start) < TimeSpan.FromSeconds(seconds))
        {
            token.ThrowIfCancellationRequested();
            capture.Drain();
            await Task.Delay(TimeSpan.FromMilliseconds(options.PollSliceMs), clock, token).ConfigureAwait(false);
        }
    }

    private async Task<IdleObservationProduce> ProduceAsync(IProducer<byte[], byte[]> producer, IdleObservationCapture capture,
        int repeat, int attempt, CancellationToken token)
    {
        string key = Key(options.ScenarioId, options.ConfigurationId, options.RunId, repeat, attempt);
        long startedElapsed = capture.ElapsedMs;
        Task<(Error Error, long Elapsed, long Unix)> outcome = capture.Delivery(attempt);
        try
        {
            producer.Produce(new TopicPartition(Topic, new Partition(Partition)),
                new Message<byte[], byte[]> { Key = Encoding.UTF8.GetBytes(key), Value = Payload() },
                report => capture.Delivered(attempt, report.Error));
        }
        catch (KafkaException exception)
        {
            return new(attempt, key, startedElapsed, true, "error",
                new((int)exception.Error.Code, exception.Error.IsFatal, "local", exception.Error.Reason),
                capture.ElapsedMs, clock.GetUtcNow().ToUnixTimeMilliseconds());
        }
        long windowStart = clock.GetTimestamp();
        while (!outcome.IsCompleted && clock.GetElapsedTime(windowStart) < TimeSpan.FromMilliseconds(options.ActionWindowMs))
        {
            token.ThrowIfCancellationRequested();
            capture.Drain();
            await Task.WhenAny(outcome, Task.Delay(TimeSpan.FromMilliseconds(options.PollSliceMs), clock, token)).ConfigureAwait(false);
        }
        capture.Drain();
        if (!outcome.IsCompleted)
        {
            capture.Diagnostic($"action_window_elapsed:p{attempt}");
            return new(attempt, key, startedElapsed, false, null, null, null, null);
        }
        (Error error, long elapsed, long unix) = await outcome.ConfigureAwait(false);
        return new(attempt, key, startedElapsed, true, error.IsError ? "error" : "success",
            error.IsError ? new((int)error.Code, error.IsFatal, IdleObservationCapture.Origin(error), error.Reason) : null, elapsed, unix);
    }

    internal static string Key(string item, string configuration, string run, int repeat, int attempt) => $"{item}:{configuration}:{run}:r{repeat}:p{attempt}";
    private static byte[] Payload() { var value = new byte[PayloadBytes]; value.AsSpan().Fill((byte)'s'); return value; }

    private async Task WriteAsync<T>(string path, T value, int limit, int seconds, CancellationToken token)
    {
        byte[] bytes = SingleActionWire.Encode(value, limit);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(seconds), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        await publication.WriteAsync(path, bytes, linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
    }
}
