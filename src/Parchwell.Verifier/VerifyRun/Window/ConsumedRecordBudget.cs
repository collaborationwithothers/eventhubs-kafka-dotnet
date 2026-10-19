using System.Text;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.VerifyRun.Window;

internal sealed class ConsumedRecordBudget
{
    private readonly long _maximumRecords;
    private readonly long _maximumBytes;

    internal ConsumedRecordBudget(long maximumRecords, long maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecords);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        _maximumRecords = maximumRecords;
        _maximumBytes = maximumBytes;
    }

    internal long Records { get; private set; }
    internal long Bytes { get; private set; }

    internal void Account(ConsumeResult<byte[], byte[]> record)
    {
        long bytes = Add(record.Message?.Key?.LongLength ?? 0, record.Message?.Value?.LongLength ?? 0);
        if (record.Message?.Headers is { } headers)
            foreach (IHeader header in headers)
            {
                bytes = Add(bytes, Encoding.UTF8.GetByteCount(header.Key));
                bytes = Add(bytes, header.GetValueBytes()?.LongLength ?? 0);
            }
        long records = Add(Records, 1);
        long totalBytes = Add(Bytes, bytes);
        if (records > _maximumRecords || totalBytes > _maximumBytes)
            throw new InputLimitExceededException("The consumed window exceeds its configured record or byte limit.");
        Records = records;
        Bytes = totalBytes;
    }

    internal static long Add(long current, long amount)
    {
        try { return checked(current + amount); }
        catch (OverflowException)
        {
            throw new InputLimitExceededException("Consumed-window accounting overflowed Int64.");
        }
    }
}
