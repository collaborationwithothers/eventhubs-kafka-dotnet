using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Judgement;

/// <summary>Classifies reconciled attempts and in-window records after validity has been established.</summary>
internal static class OutcomeClassifier
{
    internal static OutcomeCounts Classify(IReadOnlyList<LedgerRow> ledger, IReadOnlyList<ConsumedRecord> records)
    {
        // Value-tuple string equality is ordinal, matching the recorded identity contract.
        var seen = records.Select(r => (r.InstanceId, r.AttemptId)).ToHashSet();
        long ok = 0, loss = 0, phantom = 0, reportedFailure = 0;
        foreach (LedgerRow row in ledger)
        {
            bool observed = seen.Contains((row.Attempt.InstanceId, row.Attempt.AttemptId));
            if (row.Delivered)
            {
                if (observed) ok++;
                else loss++;
            }
            else
            {
                if (observed) phantom++;
                else reportedFailure++;
            }
        }
        long duplicates = records.GroupBy(r => (r.Key, r.Sequence)).Sum(g => g.LongCount() - 1);
        long reorder = 0;
        foreach (var partitionKey in records.GroupBy(r => (r.Topic, r.Partition, r.Key)))
        {
            long? maximum = null;
            foreach (ConsumedRecord record in partitionKey.OrderBy(r => r.Offset))
            {
                if (record.Sequence is not long sequence) continue;
                if (maximum.HasValue && sequence < maximum.Value) reorder++;
                if (!maximum.HasValue || sequence > maximum.Value) maximum = sequence;
            }
        }
        return new(ok, loss, phantom, reportedFailure, duplicates, reorder);
    }
}
