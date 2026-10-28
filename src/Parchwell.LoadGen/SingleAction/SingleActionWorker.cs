using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Parchwell.LoadGen.Common.SingleAction;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.SingleAction;

internal sealed class SingleActionWorker(SingleActionHostOptions options, SingleActionSessionFactory sessions,
    ISingleActionPublication publication, TimeProvider clock, ILogger<SingleActionWorker>? logger = null, TextWriter? clientLogOutput = null)
{
    private static readonly Action<ILogger, string, string, string, int, Exception?> RepeatStarted = LoggerMessage.Define<string, string, string, int>(
        LogLevel.Information, new EventId(1, nameof(RepeatStarted)),
        "single_action_repeat_started for scenario {ScenarioId} run {RunId} configuration {ConfigurationId} repeat {Repeat}");
    private static readonly Action<ILogger, string, string, string, int, string, Exception?> RepeatCaptured = LoggerMessage.Define<string, string, string, int, string>(
        LogLevel.Information, new EventId(2, nameof(RepeatCaptured)),
        "single_action_repeat_captured for scenario {ScenarioId} run {RunId} configuration {ConfigurationId} repeat {Repeat} outcome {Outcome}");
    private bool _cleanupSucceeded = true;
    internal async Task ExecuteAsync(ResolvedScenario scenario, CancellationToken cancellationToken)
    {
        SingleActionConfiguration action = scenario.Definition.SingleAction!.Configurations[options.ConfigurationId];
        ResolvedRoleConfiguration resolved = scenario.Configurations[options.ConfigurationId].Roles[action.Role];
        string prefix = SingleActionPublication.Prefix(options);
        await WriteAsync(prefix + $"registrations/{options.PodName}.json", SingleActionPublication.Registration(options),
            options.MaxRegistrationBytes, options.RegistrationUploadSeconds, cancellationToken).ConfigureAwait(false);
        List<SingleActionRepeat> repeats = [];
        var diagnostics = new SingleActionDiagnosticBudget(options.MaxDiagnostics);
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (logger is not null) RepeatStarted(logger, options.ScenarioId, options.RunId, options.ConfigurationId, repeat, null);
            if (_cleanupSucceeded) repeats.Add(await CaptureRepeatAsync(action, resolved, repeat, diagnostics, cancellationToken).ConfigureAwait(false));
            else
            {
                var settings = resolved.Settings.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);
                string clientId = $"{options.PodName}-r{repeat}";
                settings["client.id"] = clientId;
                repeats.Add(new(repeat, clientId, action.Action, false, 0, null, settings,
                    resolved.Omissions.Select(value => value.Key).Concat(resolved.ScenarioOmissions.Select(value => value.Key)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                    null, null, null, [], 0, [], ["not_attempted:client_cleanup_failed"]));
            }
            if (logger is not null)
            {
                SingleActionRepeat captured = repeats[^1];
                string outcome = captured.Diagnostics.Contains("capture_limit", StringComparer.Ordinal) ? "capture_limit" : captured.Result ?? "incomplete";
                RepeatCaptured(logger, options.ScenarioId, options.RunId, options.ConfigurationId, repeat, outcome, null);
            }
        }
        var capture = new SingleActionCaptureFile(1, "single-action", true, SingleActionPublication.Identity(options),
            options.JobName, options.PodName, options.PodUid, options.SourceCommit,
            typeof(ProducerConfig).Assembly.GetName().Version!.ToString(), Library.VersionString, repeats);
        await WriteAsync(prefix + $"instances/{options.PodName}/capture.json", capture,
            options.MaxCaptureBytes, options.CaptureUploadSeconds, cancellationToken).ConfigureAwait(false);
        if (!_cleanupSucceeded) throw new InvalidOperationException("single_action_client_cleanup_failed");
        if (repeats.Any(repeat => repeat.Diagnostics.Contains("capture_limit", StringComparer.Ordinal)))
            throw new InvalidOperationException("single_action_capture_budget_exhausted");
    }

    internal async Task<SingleActionRepeat> CaptureRepeatAsync(SingleActionConfiguration action, ResolvedRoleConfiguration resolved,
        int repeat, SingleActionDiagnosticBudget diagnostics, CancellationToken cancellationToken)
    {
        long started = clock.GetTimestamp();
        var clientLogs = new SingleActionClientLogBudget(repeat);
        var capture = new SingleActionCapture(options, clock, started, diagnostics, action.TerminalRule, clientLogs);
        string clientId = $"{options.PodName}-r{repeat}";
        var settings = resolved.Settings.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);
        settings["client.id"] = clientId;
        string[] omitted = resolved.Omissions.Select(value => value.Key).Concat(resolved.ScenarioOmissions.Select(value => value.Key))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        bool terminal = false;
        long elapsed = 0;
        long? terminalUnix = null;
        string? result = null;
        SingleActionError? error = null;
        SingleActionMatched? matched = null;
        IProducer<byte[], byte[]>? producer = null;
        IConsumer<byte[], byte[]>? consumer = null;
        bool constructing = true;
        try
        {
            if (settings.Count > options.MaxSettingsKeys || settings.Sum(pair => Encoding.UTF8.GetByteCount(pair.Key) + Encoding.UTF8.GetByteCount(pair.Value)) > options.MaxSettingsBytes)
                capture.CaptureLimit();
            else
            {
                if (action.Action is "produce_one" or "init_transactions") producer = sessions.Producer(settings, capture, cancellationToken);
                else consumer = sessions.Consumer(settings, capture, cancellationToken);
                constructing = false;
                if (action.Action == "produce_one")
                {
                    if (!capture.ProducerOutcome.IsCompleted)
                        producer!.Produce(new TopicPartition(action.Topic, new Partition(action.Partition)),
                            new Message<byte[], byte[]> { Key = ProduceKey(options.ScenarioId, options.ConfigurationId, options.RunId, repeat), Value = Payload(action.PayloadBytes) },
                            delivery => capture.Delivery(delivery.Error));
                    while (!capture.ProducerOutcome.IsCompleted && !capture.Limit && capture.ElapsedMs < action.WindowMs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        capture.Drain();
                        if (capture.Limit) break;
                        TimeSpan slice = TimeSpan.FromMilliseconds(Math.Min(options.PollSliceMs, action.WindowMs - capture.ElapsedMs));
                        await Task.WhenAny(capture.ProducerOutcome, Task.Delay(slice, clock, cancellationToken)).ConfigureAwait(false);
                    }
                    capture.Drain();
                    ObserveProducerOutcome();
                    if (!capture.ProducerOutcome.IsCompletedSuccessfully && !capture.Limit) capture.Diagnostic("action_window_elapsed");
                }
                else if (action.Action == "init_transactions")
                {
                    // The sole transaction operation, with the approved literal native timeout.
                    await Task.Run(() => producer!.InitTransactions(TimeSpan.FromMilliseconds(120000)), cancellationToken)
                        .WaitAsync(TimeSpan.FromMilliseconds(Math.Max(0, action.WindowMs - capture.ElapsedMs)), clock, cancellationToken).ConfigureAwait(false);
                    SetOutcome(null, capture.ElapsedMs, clock.GetUtcNow().ToUnixTimeMilliseconds(), false);
                }
                else
                {
                    if (action.Action == "subscribe") consumer!.Subscribe(action.Topic);
                    else consumer!.Assign(new TopicPartitionOffset(action.Topic, new Partition(action.Partition), new Offset(0)));
                    ObserveConsumerCallbacks();
                    int nonmatching = 0;
                    while (!terminal && !capture.Limit && capture.ElapsedMs < action.WindowMs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ConsumeResult<byte[], byte[]>? record = consumer!.Consume(TimeSpan.FromMilliseconds(Math.Min(options.PollSliceMs, action.WindowMs - capture.ElapsedMs)));
                        ObserveConsumerCallbacks();
                        if (!terminal && action.Action == "assign_consume" && record is not null)
                        {
                            if (Matches(record, action.Topic, action.Partition, options.RunId))
                            {
                                SetOutcome(null, capture.ElapsedMs, clock.GetUtcNow().ToUnixTimeMilliseconds(), false);
                                if (terminal) matched = new(record.Topic, record.Partition.Value, Convert.ToBase64String(record.Message.Key), Convert.ToBase64String(record.Message.Value));
                            }
                            else if (++nonmatching >= options.MaxNonmatchingRecords) capture.CaptureLimit();
                        }
                    }
                    ObserveConsumerCallbacks();
                    if (!terminal && !capture.Limit && capture.ElapsedMs >= action.WindowMs && action.TerminalRule == "window_end_no_outcome")
                        capture.TryObserveWindowEnd(action.WindowMs, action.Action == "subscribe", () =>
                        {
                            terminal = true;
                            elapsed = action.WindowMs;
                            terminalUnix = clock.GetUtcNow().ToUnixTimeMilliseconds();
                            result = "no_outcome";
                            error = null;
                            matched = null;
                        });
                }
            }
        }
        catch (KafkaException exception)
        {
            if (action.Action == "produce_one") ObserveProducerOutcome();
            if (!constructing && consumer is not null) ObserveConsumerCallbacks();
            SetOutcome(exception.Error, capture.ElapsedMs, clock.GetUtcNow().ToUnixTimeMilliseconds(), constructing);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            if (action.Action == "produce_one") ObserveProducerOutcome();
            if (!constructing && consumer is not null) ObserveConsumerCallbacks();
            capture.Diagnostic((constructing ? "construction:" : "action:") + exception.GetType().Name);
        }
        finally
        {
            try
            {
                if (terminal && !capture.Limit)
                {
                    long statisticsStarted = clock.GetTimestamp();
                    while (clock.GetElapsedTime(statisticsStarted) < TimeSpan.FromSeconds(options.PostTerminalStatisticsSeconds) && !capture.Limit)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (consumer is not null)
                        {
                            try { _ = consumer.Consume(TimeSpan.FromMilliseconds(options.PollSliceMs)); }
                            catch (ConsumeException exception) { capture.Error(exception.Error); }
                        }
                        else await Task.Delay(TimeSpan.FromMilliseconds(options.PollSliceMs), clock, cancellationToken).ConfigureAwait(false);
                        capture.Drain();
                    }
                }
            }
            finally
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(options.CleanupSeconds), clock);
                try
                {
                    if (producer is not null) await Task.Run(producer.Dispose, CancellationToken.None).WaitAsync(cleanup.Token).ConfigureAwait(false);
                    if (consumer is not null) await Task.Run(consumer.Dispose, CancellationToken.None).WaitAsync(cleanup.Token).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _cleanupSucceeded = false;
                    capture.Diagnostic("client_cleanup_failed:" + exception.GetType().Name);
                }
                finally
                {
                    foreach (string line in clientLogs.Seal()) (clientLogOutput ?? Console.Error).Write(line);
                }
            }
        }
        var snapshot = capture.Snapshot();
        // Keep a bounded terminal observation when a later callback exceeds a capture
        // limit. The diagnostic still makes completeness fail; it must not erase an
        // already measured authorization success that the verifier must retain.
        return new(repeat, clientId, action.Action, terminal, terminal ? elapsed : capture.ElapsedMs, terminalUnix,
            settings, omitted, result, error, matched, snapshot.Errors, snapshot.Dropped, snapshot.Statistics, snapshot.Diagnostics);

        void ObserveProducerOutcome()
        {
            if (!capture.ProducerOutcome.IsCompletedSuccessfully) return;
            var outcome = capture.ProducerOutcome.GetAwaiter().GetResult();
            SetOutcome(outcome.Error.IsError ? outcome.Error : null, outcome.Elapsed, outcome.Unix, false, observedBeforeLimit: true);
        }

        void ObserveConsumerCallbacks()
        {
            capture.Drain();
            var assigned = action.Action == "subscribe" ? capture.AssignmentObservation : null;
            var fatal = capture.FatalObservation;
            if (assigned is { } assignment && (fatal is null || assignment.Sequence < fatal.Value.Sequence))
                SetOutcome(null, assignment.Elapsed, assignment.Unix, false, observedBeforeLimit: true);
            else if (fatal is { } fatalTime && capture.FatalError is Error fatalError)
                SetOutcome(fatalError, fatalTime.Elapsed, fatalTime.Unix, false, observedBeforeLimit: true);
        }

        void SetOutcome(Error? observed, long elapsedMs, long unixMs, bool construction, bool observedBeforeLimit = false)
        {
            if (terminal || elapsedMs > action.WindowMs || capture.Limit && !observedBeforeLimit) return;
            if (observed is not null && Encoding.UTF8.GetByteCount(observed.Reason) > options.MaxErrorBytes) { capture.CaptureLimit(); return; }
            terminal = true;
            elapsed = elapsedMs;
            terminalUnix = unixMs;
            result = observed is null ? "success" : "error";
            error = observed is null ? null : new((int)observed.Code, observed.IsFatal, construction ? "construction" : SingleActionCapture.Origin(observed), observed.Reason);
        }
    }

    internal static byte[] ProduceKey(string item, string configuration, string run, int repeat) =>
        Encoding.UTF8.GetBytes(item == "F04" && configuration == "C0" ? $"f04-seed:{run}" : $"{item}:{configuration}:{run}:r{repeat}");
    internal static byte[] Payload(int size) { var value = new byte[size]; value.AsSpan().Fill((byte)'s'); return value; }
    internal static bool Matches(ConsumeResult<byte[], byte[]> record, string topic, int partition, string run) =>
        !record.IsPartitionEOF && record.Topic == topic && record.Partition.Value == partition && record.Message?.Key is byte[] key &&
        key.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes($"f04-seed:{run}")) && record.Message.Value is byte[] value &&
        value.Length == 1000 && value.All(character => character == (byte)'s');
    private async Task WriteAsync<T>(string path, T value, int limit, int seconds, CancellationToken token)
    {
        byte[] bytes = SingleActionWire.Encode(value, limit);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(seconds), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        await publication.WriteAsync(path, bytes, linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
    }
}
