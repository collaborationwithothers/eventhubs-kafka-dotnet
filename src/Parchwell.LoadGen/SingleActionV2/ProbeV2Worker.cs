using System.Text;
using Confluent.Kafka;
using Parchwell.Kafka.Authentication;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.SingleActionV2;

internal sealed class ProbeV2Worker(ProbeV2Options options, ProbeV2SessionFactory sessions, ProbeV2Inputs inputs, TimeProvider clock)
{
    private readonly ProbeV2DiagnosticBudget diagnosticBudget = new(options.Limits.MaxDiagnostics);
    internal bool CleanupSucceeded { get; private set; } = true;
    internal List<ProbeV2Repeat> Repeats { get; } = [];

    internal async Task ExecuteAsync(ResolvedScenario scenario, TextWriter output, TextWriter logs, CancellationToken token)
    {
        ProbeV2Stage stage = ProbeV2Stage.For(options.ScenarioId, options.ConfigurationId, options.Stage);
        ResolvedRoleConfiguration resolved = scenario.Configurations[options.ConfigurationId].Roles[stage.Role];
        await WriteAsync(output, "PARCHWELL_PROBE_V2_REGISTRATION ", ProbeV2Wire.Registration(options), options.Limits.MaxRegistrationBytes, token).ConfigureAwait(false);
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            token.ThrowIfCancellationRequested();
            if (!CleanupSucceeded)
            { Repeats.Add(ProbeV2Wire.Skipped(repeat, stage.Action, "not_attempted_cleanup")); continue; }
            (string admission, ProbeV2Requested? requested) = await inputs.AdmitAsync(stage, repeat, token).ConfigureAwait(false);
            if (admission != "required")
            { Repeats.Add(ProbeV2Wire.Skipped(repeat, stage.Action, admission)); continue; }
            Repeats.Add(await CaptureAsync(stage, resolved, repeat, requested!, logs, token).ConfigureAwait(false));
        }
    }
    internal async Task<ProbeV2Repeat> CaptureAsync(ProbeV2Stage stage, ResolvedRoleConfiguration resolved, int repeat,
        ProbeV2Requested requested, TextWriter logs, CancellationToken token)
    {
        long started = clock.GetTimestamp();
        using var actionDeadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(stage.WindowMs), clock);
        using var actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, actionDeadline.Token);
        ProbeV2HandlerRefreshPath? refreshPath = options.TestHandlerRefreshPath
            ? sessions.CreateHandlerRefreshPath(clock, repeat, $"{options.PodName}-r{repeat}")
            : null;
        CancellationTokenSource? refreshSessionCancellation = refreshPath is null
            ? null : CancellationTokenSource.CreateLinkedTokenSource(token);
        ITimer? refreshCancellationTimer = refreshSessionCancellation is null ? null : clock.CreateTimer(
            static state =>
            {
                try { ((CancellationTokenSource)state!).Cancel(); }
                catch (ObjectDisposedException) { }
            }, refreshSessionCancellation,
            TimeSpan.FromMilliseconds(stage.WindowMs + 20000), Timeout.InfiniteTimeSpan);
        CancellationToken sessionToken = refreshSessionCancellation?.Token ?? actionCancellation.Token;
        var capture = new ProbeV2Capture(options.Limits, clock, started, stage.WindowMs, diagnosticBudget,
            $"scenario_id={options.ScenarioId} run_id={options.RunId} configuration_id={options.ConfigurationId} stage={options.Stage} repeat={repeat} ",
            options.CaptureAuthenticationMetadata);
        string clientId = $"{options.PodName}-r{repeat}";
        ProbeV2AuthenticationMetadataCollector? metadataCollector = null;
        if (options.CaptureAuthenticationMetadata)
        {
            metadataCollector = new(options, stage, repeat, clientId, sessions.CreateProjectedFileSampler);
            metadataCollector.BeforeClient();
        }
        var settings = resolved.Settings.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);
        settings["client.id"] = clientId;
        string[] omitted = resolved.Omissions.Select(value => value.Key).Concat(resolved.ScenarioOmissions.Select(value => value.Key))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        int[]? metadata = null;
        IProducer<byte[], byte[]>? producer = null;
        IConsumer<byte[], byte[]>? consumer = null;
        KafkaOAuthBearerSession? authentication = null;
        bool constructing = true;
        bool clientConstructed = false;
        bool clientCleanupCompleted = true;
        bool diagnosticConsumerOperationStarted = false;
        try
        {
            int settingsBytes = settings.Sum(pair => Encoding.UTF8.GetByteCount(pair.Key) + Encoding.UTF8.GetByteCount(pair.Value));
            if (settings.Count > options.Limits.MaxSettingsKeys)
                capture.CaptureLimit(nameof(options.Limits.MaxSettingsKeys), settings.Count, options.Limits.MaxSettingsKeys);
            else if (settingsBytes > options.Limits.MaxSettingsBytes)
                capture.CaptureLimit(nameof(options.Limits.MaxSettingsBytes), settingsBytes, options.Limits.MaxSettingsBytes);
            else
            {
                if (stage.Action == "produce_one")
                {
                    producer = sessions.Producer(settings, capture, sessionToken);
                    authentication = capture.AuthenticationSession;
                }
                else (consumer, authentication) = sessions.Consumer(settings, capture, sessionToken);
                clientConstructed = producer is not null || consumer is not null;
                constructing = false;
                if (options.ScenarioId == "B08")
                {
                    if (consumer is not null)
                        Bootstrap(consumer, () => authentication!.InstalledTokenExpiresOn is not null, capture, requested,
                            options.Limits.PollSliceMs, stage.WindowMs, token);
                    if (Continue() && (producer is not null || authentication!.InstalledTokenExpiresOn is not null))
                    {
                        metadata = sessions.Metadata((IClient?)producer ?? consumer!, stage.Topic,
                            TimeSpan.FromMilliseconds(Math.Min(options.Limits.MetadataTimeoutMs, Remaining())));
                        if (!metadata.SequenceEqual(Enumerable.Range(0, 32))) capture.Diagnostic("metadata_partition_input");
                    }
                }
                bool metadataReady = options.ScenarioId != "B08" || metadata?.SequenceEqual(Enumerable.Range(0, 32)) == true;
                if (Continue() && metadataReady)
                {
                    if (producer is not null)
                    {
                        var message = new Message<byte[], byte[]>
                        {
                            Key = Convert.FromBase64String(requested.KeyBase64),
                            Value = requested.ValueBase64 is null ? null! : Convert.FromBase64String(requested.ValueBase64)
                        };
                        Action<DeliveryReport<byte[], byte[]>> delivery = refreshPath is null
                            ? capture.Delivery
                            : report => { capture.Delivery(report); refreshPath.ObserveBeforeDelivery(report); };
                        if (stage.PartitionMode == "automatic") producer.Produce(stage.Topic, message, delivery);
                        else producer.Produce(new TopicPartition(stage.Topic, new Partition(requested.Partition!.Value)), message, delivery);
                        while (Continue())
                            await Task.Delay(Slice(), clock, token).ConfigureAwait(false);
                    }
                    else
                    {
                        consumer!.Assign(new TopicPartitionOffset(requested.Topic, new Partition(requested.Partition!.Value), new Offset(requested.Offset!.Value)));
                        while (Continue())
                        {
                            token.ThrowIfCancellationRequested();
                            ConsumeResult<byte[], byte[]>? result = consumer.Consume(Slice());
                            capture.Read(result, requested);
                            refreshPath?.ObserveBeforeRead(result, requested);
                        }
                        if (options.ScenarioId == "F06" && options.ConfigurationId is "C10" or "C11") capture.NoOutcome();
                    }
                }
                if (!capture.Terminal && !capture.Limit) capture.Diagnostic("action_window_elapsed");
            }
        }
        catch (KafkaException exception) { capture.Exception(exception.Error, constructing); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { capture.Diagnostic("action_cancelled"); }
        catch (Exception exception) { capture.Diagnostic((constructing ? "construction:" : "action:") + exception.GetType().Name); }
        finally
        {
            try
            {
                if (capture.Terminal && !capture.Limit)
                {
                    long statisticsStarted = clock.GetTimestamp();
                    refreshPath?.StartWindow(statisticsStarted);
                    ShortenRefreshCancellationDeadline();
                    while (clock.GetElapsedTime(statisticsStarted) < TimeSpan.FromSeconds(options.Limits.PostTerminalStatisticsSeconds) && !capture.Limit)
                    {
                        token.ThrowIfCancellationRequested();
                        if (refreshPath is not null && !refreshPath.CanContinue(token)) break;
                        if (refreshPath is not null && refreshPath.TryAdmit(
                                authentication?.HandlerRefreshProgress.ThirdSuccessfulAttempt, token))
                        {
                            RunDiagnosticOperation();
                            if (diagnosticConsumerOperationStarted || !refreshPath.CanContinue(token)) break;
                        }
                        TimeSpan remaining = TimeSpan.FromSeconds(options.Limits.PostTerminalStatisticsSeconds) - clock.GetElapsedTime(statisticsStarted);
                        TimeSpan slice = TimeSpan.FromMilliseconds(Math.Min(options.Limits.PollSliceMs, remaining.TotalMilliseconds));
                        if (consumer is not null)
                        {
                            try
                            {
                                ConsumeResult<byte[], byte[]>? result = consumer.Consume(slice);
                                if (!diagnosticConsumerOperationStarted) capture.Read(result, requested);
                            }
                            catch (ConsumeException exception)
                            {
                                if (!diagnosticConsumerOperationStarted) capture.Error(exception.Error);
                            }
                        }
                        else await Task.Delay(slice, clock, token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception) { capture.Diagnostic("statistics:" + exception.GetType().Name); }
            finally
            {
                refreshPath?.EndWindow();
                try
                {
                    refreshCancellationTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    refreshSessionCancellation?.Cancel();
                }
                catch (Exception) { }
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(options.Limits.CleanupSeconds), clock);
                try
                {
                    if (producer is not null) await Task.Run(producer.Dispose, CancellationToken.None).WaitAsync(cleanup.Token).ConfigureAwait(false);
                    if (consumer is not null) await Task.Run(consumer.Dispose, CancellationToken.None).WaitAsync(cleanup.Token).ConfigureAwait(false);
                }
                catch (Exception exception)
                { CleanupSucceeded = false; clientCleanupCompleted = false; capture.Diagnostic("client_cleanup_failed:" + exception.GetType().Name); }
                refreshCancellationTimer?.Dispose();
                refreshSessionCancellation?.Dispose();
                foreach (string line in capture.Logs()) await logs.WriteLineAsync(line.AsMemory(), CancellationToken.None).ConfigureAwait(false);
                if (metadataCollector is not null)
                {
                    try
                    {
                        KafkaOAuthBearerSession? attached = clientConstructed && clientCleanupCompleted ? capture.AuthenticationSession : null;
                        ProbeV2AuthenticationMetadata value = metadataCollector.Finish(clientConstructed, clientCleanupCompleted,
                            attached is null ? null : attached.GetLifecycleSnapshot,
                            refreshPath?.Finish(clientCleanupCompleted, token.IsCancellationRequested));
                        await ProbeV2AuthenticationMetadataWriter.TryWriteAsync(logs, value, options.Limits.IoSeconds, clock).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // Metadata capture is isolated from the service result and cleanup state.
                    }
                }
            }
        }
        return capture.Snapshot(repeat, clientId, stage, settings, omitted, metadata, requested);

        long Remaining() => Math.Max(0, stage.WindowMs - capture.ElapsedMs);
        bool Continue() => !capture.Terminal && !capture.Limit && Remaining() > 0;
        TimeSpan Slice() => TimeSpan.FromMilliseconds(Math.Min(options.Limits.PollSliceMs, Remaining()));

        void ShortenRefreshCancellationDeadline()
        {
            if (refreshCancellationTimer is null) return;
            try
            {
                TimeSpan priorRemaining = TimeSpan.FromMilliseconds(stage.WindowMs + 20000) - clock.GetElapsedTime(started);
                TimeSpan dueTime = priorRemaining < TimeSpan.FromMilliseconds(20000)
                    ? (priorRemaining > TimeSpan.Zero ? priorRemaining : TimeSpan.Zero)
                    : TimeSpan.FromMilliseconds(20000);
                refreshCancellationTimer.Change(dueTime, Timeout.InfiniteTimeSpan);
            }
            catch (Exception) { refreshPath?.RecordClockUnavailable(); }
        }

        void RunDiagnosticOperation()
        {
            if (refreshPath is null) return;
            try
            {
                if (producer is not null)
                {
                    byte[] key = Encoding.UTF8.GetBytes($"handler-refresh:{options.RunId}:{options.ConfigurationId}:{repeat}");
                    byte[] value = Encoding.UTF8.GetBytes("synthetic-handler-refresh-path");
                    var message = new Message<byte[], byte[]> { Key = key, Value = value };
                    Action<DeliveryReport<byte[], byte[]>> callback = refreshPath.ObserveAfterDelivery;
                    if (stage.PartitionMode == "automatic") producer.Produce(stage.Topic, message, callback);
                    else producer.Produce(new TopicPartition(stage.Topic, new Partition(requested.Partition!.Value)), message, callback);
                    return;
                }

                diagnosticConsumerOperationStarted = true;
                consumer!.Seek(new TopicPartitionOffset(requested.Topic,
                    new Partition(requested.Partition!.Value), new Offset(requested.Offset!.Value)));
                refreshPath.SeekReturned();
                while (refreshPath.CanContinue(token))
                {
                    TimeSpan diagnosticSlice = refreshPath.Slice(options.Limits.PollSliceMs, token);
                    if (diagnosticSlice <= TimeSpan.Zero) break;
                    try
                    {
                        ConsumeResult<byte[], byte[]>? result = consumer.Consume(diagnosticSlice);
                        if (refreshPath.ObserveDiagnosticRead(result, requested)) break;
                    }
                    catch (ConsumeException exception)
                    {
                        refreshPath.RecordError(exception.Error);
                        if (!refreshPath.CanContinue(token)) break;
                    }
                }
            }
            catch (Exception exception) { refreshPath.RecordOperationException(exception); }
        }
    }
    internal static bool Bootstrap(IConsumer<byte[], byte[]> consumer, Func<bool> tokenInstalled,
        ProbeV2Capture capture, ProbeV2Requested requested, int pollSliceMs, int windowMs, CancellationToken token)
    {
        while (!tokenInstalled() && !capture.Terminal && !capture.Limit && capture.ElapsedMs < windowMs)
        {
            token.ThrowIfCancellationRequested();
            capture.Read(consumer.Consume(TimeSpan.FromMilliseconds(Math.Min(pollSliceMs, windowMs - capture.ElapsedMs))), requested, unassigned: true);
        }
        return tokenInstalled() && !capture.Terminal && !capture.Limit && capture.ElapsedMs < windowMs;
    }
    internal async Task WriteAsync<T>(TextWriter output, string marker, T value, int maximum, CancellationToken token)
    {
        byte[] bytes = ProbeV2Wire.Encode(value, maximum);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.Limits.IoSeconds), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        await output.WriteLineAsync((marker + Encoding.UTF8.GetString(bytes)).AsMemory(), linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
        await output.FlushAsync(linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
    }
}
