using Confluent.Kafka;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Window;

internal sealed record WindowDrainResult(IReadOnlyList<ConsumedRecord> Records,
    IReadOnlyList<CheckAssessment> Assessments, TimeSpan? DrainDuration, TimeSpan? StopDuration);

internal static class WindowConsumer
{
    internal static WindowDrainResult Drain(IConsumer<byte[], byte[]> consumer, RunWindow window,
        TimeProvider timeProvider, long safetyOriginTimestamp, TimeSpan safetyStop, TimeSpan pollSlice,
        ConsumedRecordBudget budget, CancellationToken cancellationToken,
        CancellationToken safetyCancellationToken = default)
    {
        if (!window.CanAssign) return new([], [], null, null);
        var records = new List<ConsumedRecord>();
        PartitionWindow[] pending = window.Partitions.Where(p => p.Start != p.End).ToArray();
        Dictionary<int, PartitionWindow> partitions = window.Partitions.ToDictionary(p => p.Partition);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pending.Length != 0)
                consumer.Assign(pending.Select(p => new TopicPartitionOffset(RunWindow.Topic, new Partition(p.Partition), new Offset(p.Start))));
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool complete = true;
                foreach (PartitionWindow partition in pending)
                    if (consumer.Position(new TopicPartition(RunWindow.Topic, new Partition(partition.Partition))).Value < partition.End)
                        complete = false;
                TimeSpan elapsed = timeProvider.GetElapsedTime(safetyOriginTimestamp);
                if (complete && elapsed <= safetyStop)
                    return new(records, [RunWindow.Pass(9), RunWindow.Pass(10)], elapsed, null);
                if (elapsed >= safetyStop)
                    return new(records, [RunWindow.Pass(9), RunWindow.Failure(10, "safety_stop")], null, elapsed);
                TimeSpan remaining = safetyStop - elapsed;
                ConsumeResult<byte[], byte[]>? record = null;
                try
                {
                    record = consumer.Consume(pollSlice < remaining ? pollSlice : remaining);
                }
                catch (Exception exception) when (safetyCancellationToken.IsCancellationRequested &&
                    !cancellationToken.IsCancellationRequested &&
                    (exception is OperationCanceledException || exception is KafkaException native && !IsOffsetError(native)))
                {
                    // Observe positions once more before deciding completion at the deadline.
                }
                if (record is not null && string.Equals(record.Topic, RunWindow.Topic, StringComparison.Ordinal) &&
                    partitions.TryGetValue(record.Partition.Value, out PartitionWindow? boundary) &&
                    record.Offset.Value >= boundary.Start && record.Offset.Value < boundary.End)
                {
                    budget.Account(record);
                    records.Add(MessageIdentityReader.Read(record));
                }
            }
        }
        catch (KafkaException exception) when (IsOffsetError(exception))
        {
            return new(records, [RunWindow.Failure(9, "start_offset_out_of_range")], null, null);
        }
    }

    private static bool IsOffsetError(KafkaException exception) =>
        exception.Error.Code is ErrorCode.Local_AutoOffsetReset or ErrorCode.OffsetOutOfRange;
}
