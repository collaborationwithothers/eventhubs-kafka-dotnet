using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Enrichment.Common.NativeMonitoring;

namespace Parchwell.Enrichment.ConsumerProcessing;

internal sealed class ConsumerProcessingWorker(IOptions<ConsumerProcessingOptions> options,
    IConsumerProcessingPlanSource? planSource, Func<ConsumerConfig> baselineFactory,
    Func<IConsumerRecordStore> storeFactory, ConsumerProcessingConsumerFactory consumerFactory,
    TimeProvider clock, Action<int> exitProcess, ConsumerSignalSender signal, TextWriter? error = null, NativeClientCapture? monitoring = null)
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        ConsumerProcessingOptions? identity = null;
        ConsumerProcessingPlan? plan = null;
        ConsumerConfig? configuration = null;
        IConsumerRecordStore? store = null;
        IConsumer<byte[], byte[]>? consumer = null;
        string? failure = null;
        Exception? cause = null;
        bool appendFailed = false;
        bool watchdogFired = false;
        bool killFailed = false;
        bool recordOpen = true;
        long rows = 0;
        long processed = 0;
        long ordinal = 0;
        ITimer? watchdog = null;
        var stop = new StopObservation();
        CancellationTokenRegistration stopRegistration = default;
        bool stopArmed = false;
        bool stopShutdown = false;
        string shutdownOutcome = "ok";
        DateTimeOffset shutdownStarted = default;
        DateTimeOffset shutdownEnded = default;
        var gate = new object();
        var held = new HashSet<TopicPartition>();
        var buffered = new Queue<(ConsumeResult<byte[], byte[]> Message, long Ordinal)>();
        try
        {
            try { identity = options.Value; identity.Validate(); }
            catch (Exception) { throw new InvalidOperationException("consumer_identity_invalid"); }
            failure = "consumer_plan_invalid";
            plan = planSource?.GetPlan(identity.ScenarioId, identity.Phase)
                ?? throw new InvalidOperationException("consumer_plan_invalid");
            configuration = baselineFactory();
            plan.ApplyAndValidate(identity, configuration);
            if (plan.KillCount is not null)
            {
                failure = "consumer_self_kill_unavailable";
                if (signal(0).ReturnValue != 0) throw new InvalidOperationException(failure);
            }
            failure = "consumer_registration_failed";
            store = storeFactory();
            await store.RegisterAsync(ConsumerRecordCodec.Registration(identity, plan, configuration, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
            failure = "consumer_fatal_error";
            var handlers = new ConsumerProcessingHandlers(
                (_, partitions) => PartitionEvent("assigned", partitions),
                (_, partitions) => PartitionEvent("revoked", partitions),
                (_, partitions) => PartitionEvent("lost", partitions),
                (_, committed) => Append(() => ConsumerRecordCodec.Commit(identity.PodName, rows, committed, clock.GetUtcNow())),
                plan.RecordsConsumerErrors
                    ? (_, error) => Append(() => ConsumerRecordCodec.ConsumerError(identity.PodName, rows, "error_handler", error, clock.GetUtcNow()))
                    : null);
            consumer = consumerFactory(configuration, handlers);
            consumer.Subscribe("document-events");
            // A stop counts only if it arrives after Subscribe has returned. An earlier one takes the existing failure path.
            if (plan.StopConfigured && !cancellationToken.IsCancellationRequested)
            {
                stopArmed = true;
                stopRegistration = cancellationToken.Register(() => stop.Observe(clock.GetUtcNow()));
            }
            long windowStart = clock.GetTimestamp();
            watchdog = clock.CreateTimer(_ =>
            {
                watchdogFired = true;
                exitProcess(124);
            }, null, TimeSpan.FromMilliseconds(plan.WindowMs + 120000), Timeout.InfiniteTimeSpan);
            while (clock.GetElapsedTime(windowStart) < TimeSpan.FromMilliseconds(plan.WindowMs))
            {
                if (stopArmed)
                {
                    if (stop.Observed is not null) break;
                }
                else cancellationToken.ThrowIfCancellationRequested();
                ConsumeResult<byte[], byte[]>? message = Poll();
                ThrowIfAppendFailed();
                if (message is null) continue;
                await ProcessAsync(message, ++ordinal).ConfigureAwait(false);
            }
            if (stopArmed && stop.Observed is { } observed)
            {
                Append(() => ConsumerRecordCodec.StopObserved(identity.PodName, rows, observed));
                ThrowIfAppendFailed();
                stopShutdown = true;
            }
            failure = plan.KillCount is null ? null : "consumer_kill_count_not_reached";
        }
        catch (Exception exception)
        {
            cause = exception;
            failure = appendFailed ? "consumer_record_append_failed" : exception.Message is
                "consumer_identity_invalid" or "consumer_plan_invalid" or "consumer_setting_not_supported"
                    ? exception.Message : failure ?? "consumer_fatal_error";
        }
        finally
        {
            if (consumer is not null && !killFailed && stopShutdown)
            {
                shutdownStarted = clock.GetUtcNow();
                try
                {
                    if (plan!.ShutdownMode == "close")
                    {
                        await Task.Run(consumer.Close, CancellationToken.None)
                            .WaitAsync(TimeSpan.FromSeconds(30), clock, CancellationToken.None).ConfigureAwait(false);
                        ThrowIfAppendFailed();
                        consumer.Dispose();
                    }
                    else consumer.Dispose();
                }
                catch (TimeoutException) { shutdownOutcome = "timed_out"; }
                catch (Exception exception)
                {
                    cause ??= exception;
                    shutdownOutcome = "failed";
                }
                // Close the record before the end time is taken, so a callback that a timed-out Close raises later
                // appends nothing and the receipt counts every appended row.
                lock (gate) recordOpen = false;
                shutdownEnded = clock.GetUtcNow();
                if (appendFailed) failure = "consumer_record_append_failed";
                monitoring?.Complete(shutdownOutcome == "ok");
            }
            else if (consumer is not null && !killFailed)
            {
                try
                {
                    await Task.Run(consumer.Close, CancellationToken.None)
                        .WaitAsync(TimeSpan.FromSeconds(30), clock, CancellationToken.None).ConfigureAwait(false);
                    ThrowIfAppendFailed();
                    consumer.Dispose();
                }
                catch (Exception exception)
                {
                    cause ??= exception;
                    failure = appendFailed ? "consumer_record_append_failed" : failure ?? "consumer_close_failed";
                }
                monitoring?.Complete(failure is null);
                recordOpen = false;
            }
        }
        try
        {
            if (failure is null && !watchdogFired && stopShutdown)
            {
                try
                {
                    // Not a completion: the stop receipt replaces completion.json. The token cannot fail the upload.
                    await store!.WriteStopReceiptAsync(ConsumerRecordCodec.StopReceipt(identity!, plan!, rows, processed,
                        stop.Observed!.Value, shutdownStarted, shutdownEnded, shutdownOutcome, clock.GetUtcNow()), CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    cause ??= exception;
                    failure = "consumer_stop_receipt_failed";
                }
            }
            else if (failure is null && !watchdogFired)
            {
                try
                {
                    await store!.CompleteAsync(ConsumerRecordCodec.Completion(identity!, plan!, rows, processed, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    cause ??= exception;
                    failure = "consumer_completion_failed";
                }
            }
        }
        finally { watchdog?.Dispose(); await stopRegistration.DisposeAsync().ConfigureAwait(false); }
        if (watchdogFired) return 124;
        if (failure is null) return 0;
        await (error ?? Console.Error).WriteLineAsync($"consumer_processing_failed reason={failure} scenario_id={Display(identity?.ScenarioId)} run_id={Display(identity?.RunId)} role={Display(identity?.Role)}{CauseSuffix(cause)}").ConfigureAwait(false);
        if (killFailed) exitProcess(1);
        return 1;

        void ThrowIfAppendFailed()
        {
            if (appendFailed) throw new InvalidOperationException("consumer_record_append_failed");
        }

        // With a stop configured, every append and processing wait ignores the stop token for the whole run, so the
        // token cancelled by a stop cannot fail the work that finishes the message in progress or records the shutdown.
        CancellationToken WorkToken() => plan?.StopConfigured == true ? CancellationToken.None : cancellationToken;

        void Append(Func<byte[]> serialize)
        {
            // The lock lets a stop shutdown close the record while a callback of a timed-out Close may still run.
            lock (gate)
            {
                if (!recordOpen || appendFailed) return;
                try
                {
                    store!.AppendAsync(serialize(), WorkToken()).GetAwaiter().GetResult();
                    rows++;
                }
                catch (Exception)
                {
                    appendFailed = true;
                    throw;
                }
            }
        }

        void PartitionEvent(string name, List<TopicPartition> partitions)
        {
            if (appendFailed || !recordOpen) return;
            if (name == "assigned") { held.Clear(); held.UnionWith(partitions); }
            else held.ExceptWith(partitions);
            int pending = buffered.Count;
            for (int index = 0; index < pending; index++)
            {
                var item = buffered.Dequeue();
                if (held.Contains(item.Message.TopicPartition)) buffered.Enqueue(item);
            }
            Append(() => ConsumerRecordCodec.PartitionEvent(identity!.PodName, rows, name,
                partitions.Select(partition => partition.Partition.Value), clock.GetUtcNow()));
        }

        ConsumeResult<byte[], byte[]>? Poll()
        {
            try { return consumer!.Consume(TimeSpan.FromMilliseconds(100)); }
            catch (ConsumeException exception) when (!appendFailed && (plan!.RecordsConsumerErrors || !exception.Error.IsFatal))
            {
                if (plan!.RecordsConsumerErrors)
                    Append(() => ConsumerRecordCodec.ConsumerError(identity!.PodName, rows, "consume", exception.Error, clock.GetUtcNow()));
                if (exception.Error.IsFatal) throw;
                return null;
            }
        }

        void SelfKill(ConsumeResult<byte[], byte[]> message)
        {
            Append(() => ConsumerRecordCodec.KillIntent(identity!.PodName, rows, message.Partition.Value, message.Offset.Value, clock.GetUtcNow()));
            ThrowIfAppendFailed();
            killFailed = true;
            failure = "consumer_self_kill_failed";
            signal(9);
            throw new InvalidOperationException(failure);
        }

        async Task ProcessAsync(ConsumeResult<byte[], byte[]> message, long messageOrdinal)
        {
            ThrowIfAppendFailed();
            if (plan!.KillCount == messageOrdinal)
            {
                SelfKill(message);
                return;
            }
            bool slow = plan.SlowMessage?.Ordinal == messageOrdinal;
            DateTimeOffset started = clock.GetUtcNow();
            int duration = slow ? plan.SlowMessage!.DurationMs : plan.ProcessingDelayMs;
            TopicPartition[]? paused = null;
            if (slow && plan.SlowMessage!.Mode == "pause")
            {
                paused = consumer!.Assignment.ToArray();
                consumer.Pause(paused);
                long pauseStarted = clock.GetTimestamp();
                while (clock.GetElapsedTime(pauseStarted) < TimeSpan.FromMilliseconds(duration))
                {
                    WorkToken().ThrowIfCancellationRequested();
                    ConsumeResult<byte[], byte[]>? next = Poll();
                    ThrowIfAppendFailed();
                    if (next is not null) buffered.Enqueue((next, ++ordinal));
                }
            }
            else if (duration > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(duration), clock, WorkToken()).ConfigureAwait(false);
            DateTimeOffset ended = clock.GetUtcNow();
            Append(() => ConsumerRecordCodec.Processed(identity!.PodName, rows, message, slow, started, ended));
            ThrowIfAppendFailed();
            processed++;
            if (configuration!.EnableAutoOffsetStore == false)
            {
                try { consumer!.StoreOffset(message); }
                catch (KafkaException exception) when (exception.Error.Code == ErrorCode.Local_State) { }
            }
            if (paused is null) return;
            consumer!.Resume(paused.Where(held.Contains).ToArray());
            while (buffered.TryDequeue(out var item))
                if (held.Contains(item.Message.TopicPartition))
                    await ProcessAsync(item.Message, item.Ordinal).ConfigureAwait(false);
        }
    }

    // The stop request's time, read by the worker loop while the token's callback may write it from another thread.
    private sealed class StopObservation
    {
        private long ticks;
        internal void Observe(DateTimeOffset time) => Interlocked.CompareExchange(ref ticks, time.UtcTicks, 0);
        internal DateTimeOffset? Observed => Volatile.Read(ref ticks) is var value and not 0 ? new DateTimeOffset(value, TimeSpan.Zero) : null;
    }

    private static string CauseSuffix(Exception? cause) => cause is null ? string.Empty : " cause=" + ExceptionSummary.Describe(cause);

    private static string Display(string? value) => string.IsNullOrEmpty(value) ? "unknown" : value;
}
