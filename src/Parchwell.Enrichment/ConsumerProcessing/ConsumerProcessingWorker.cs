using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Parchwell.Enrichment.ConsumerProcessing;

internal sealed class ConsumerProcessingWorker(IOptions<ConsumerProcessingOptions> options,
    IConsumerProcessingPlanSource? planSource, Func<ConsumerConfig> baselineFactory,
    Func<IConsumerRecordStore> storeFactory, ConsumerProcessingConsumerFactory consumerFactory,
    TimeProvider clock, Action<int> exitProcess, ConsumerSignalSender signal, TextWriter? error = null)
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        ConsumerProcessingOptions? identity = null;
        ConsumerProcessingPlan? plan = null;
        ConsumerConfig? configuration = null;
        IConsumerRecordStore? store = null;
        IConsumer<byte[], byte[]>? consumer = null;
        string? failure = null;
        bool appendFailed = false;
        bool watchdogFired = false;
        bool killFailed = false;
        bool recordOpen = true;
        long rows = 0;
        long processed = 0;
        long ordinal = 0;
        ITimer? watchdog = null;
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
                (_, committed) => Append(() => ConsumerRecordCodec.Commit(identity.PodName, rows, committed, clock.GetUtcNow())));
            consumer = consumerFactory(configuration, handlers);
            consumer.Subscribe("document-events");
            long windowStart = clock.GetTimestamp();
            watchdog = clock.CreateTimer(_ =>
            {
                watchdogFired = true;
                exitProcess(124);
            }, null, TimeSpan.FromMilliseconds(plan.WindowMs + 120000), Timeout.InfiniteTimeSpan);
            while (clock.GetElapsedTime(windowStart) < TimeSpan.FromMilliseconds(plan.WindowMs))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ConsumeResult<byte[], byte[]>? message = Poll();
                ThrowIfAppendFailed();
                if (message is null) continue;
                await ProcessAsync(message, ++ordinal).ConfigureAwait(false);
            }
            failure = plan.KillCount is null ? null : "consumer_kill_count_not_reached";
        }
        catch (Exception exception)
        {
            failure = appendFailed ? "consumer_record_append_failed" : exception.Message is
                "consumer_identity_invalid" or "consumer_plan_invalid" or "consumer_setting_not_supported"
                    ? exception.Message : failure ?? "consumer_fatal_error";
        }
        finally
        {
            if (consumer is not null && !killFailed)
            {
                try
                {
                    await Task.Run(consumer.Close, CancellationToken.None)
                        .WaitAsync(TimeSpan.FromSeconds(30), clock, CancellationToken.None).ConfigureAwait(false);
                    ThrowIfAppendFailed();
                    consumer.Dispose();
                }
                catch (Exception) { failure = appendFailed ? "consumer_record_append_failed" : failure ?? "consumer_close_failed"; }
                recordOpen = false;
            }
        }
        try
        {
            if (failure is null && !watchdogFired)
            {
                try
                {
                    await store!.CompleteAsync(ConsumerRecordCodec.Completion(identity!, plan!, rows, processed, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception) { failure = "consumer_completion_failed"; }
            }
        }
        finally { watchdog?.Dispose(); }
        if (watchdogFired) return 124;
        if (failure is null) return 0;
        await (error ?? Console.Error).WriteLineAsync($"consumer_processing_failed reason={failure} scenario_id={Display(identity?.ScenarioId)} run_id={Display(identity?.RunId)} role={Display(identity?.Role)}").ConfigureAwait(false);
        if (killFailed) exitProcess(1);
        return 1;

        void ThrowIfAppendFailed()
        {
            if (appendFailed) throw new InvalidOperationException("consumer_record_append_failed");
        }

        void Append(Func<byte[]> serialize)
        {
            if (!recordOpen || appendFailed) return;
            try
            {
                store!.AppendAsync(serialize(), cancellationToken).GetAwaiter().GetResult();
                rows++;
            }
            catch (Exception)
            {
                appendFailed = true;
                throw;
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
            catch (ConsumeException exception) when (!exception.Error.IsFatal && !appendFailed) { return null; }
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
                    cancellationToken.ThrowIfCancellationRequested();
                    ConsumeResult<byte[], byte[]>? next = Poll();
                    ThrowIfAppendFailed();
                    if (next is not null) buffered.Enqueue((next, ++ordinal));
                }
            }
            else if (duration > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(duration), clock, cancellationToken).ConfigureAwait(false);
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

    private static string Display(string? value) => string.IsNullOrEmpty(value) ? "unknown" : value;
}
