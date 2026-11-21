using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Judgement;

internal static class DuplicateClassifier
{
    internal static DuplicateBreakdown? TryClassify(IReadOnlyList<ConsumedRecord> records)
    {
        if (records.Any(r => r.Key is null || r.InstanceId is null || r.AttemptId is null ||
            !r.Sequence.HasValue || r.Problems.Count != 0)) return null;
        var distinct = records.DistinctBy(r => (r.Topic, r.Partition, r.Offset));
        long same = 0, different = 0;
        foreach (var group in distinct.GroupBy(r => (r.Key, r.Sequence)))
        {
            long copies = group.LongCount();
            long attempts = group.Select(r => (r.InstanceId, r.AttemptId)).Distinct().LongCount();
            same = checked(same + copies - attempts);
            different = checked(different + attempts - 1);
        }
        var result = new DuplicateBreakdown(same, different);
        _ = result.Total;
        return result;
    }
}
