namespace Parchwell.Verifier.Common.Consumer;

internal sealed record ConsumerObservedOffset(int Partition, long Offset, int ErrorCode);
internal sealed record ConsumerOffsetRead(string Outcome, IReadOnlyList<int> RequestedPartitions,
    DateTimeOffset? StartedUtc, DateTimeOffset? EndedUtc, int? ErrorCode, IReadOnlyList<ConsumerObservedOffset> Offsets);
internal delegate Task<ConsumerOffsetRead> ConsumerOffsetReader(IReadOnlyList<int> partitions,
    DateTimeOffset deadline, CancellationToken cancellationToken);
