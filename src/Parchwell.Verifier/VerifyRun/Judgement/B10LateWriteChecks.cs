using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;

namespace Parchwell.Verifier.VerifyRun.Judgement;

internal sealed record B10SweepWindows(RunWindow Original, RunWindow Late);

internal sealed record B10LateWriteJudgement(bool Valid, OutcomeCounts? Counts, IReadOnlyList<string> Reasons);

/// <summary>Validates immutable sweep boundaries and reconciles each repeat's complete observation union.</summary>
internal static class B10LateWriteChecks
{
    internal static B10SweepWindows BuildWindows(IReadOnlyList<WatermarkOffset>? starts,
        IReadOnlyList<WatermarkOffset>? ends, IReadOnlyList<PartitionHigh> acceptedStarts,
        IReadOnlyList<PartitionHigh> sweep, IReadOnlyDictionary<int,long> current, int maxPartitions)
    {
        Dictionary<int,long> start=Vector(starts,maxPartitions);
        Dictionary<int,long> end=Vector(ends,maxPartitions);
        Dictionary<int,long> accepted=Vector(acceptedStarts.Select(p=>new WatermarkOffset(p.Partition,p.High)).ToArray(),maxPartitions);
        Dictionary<int,long> captured=Vector(sweep.Select(p=>new WatermarkOffset(p.Partition,p.High)).ToArray(),maxPartitions);
        if(start.Count!=accepted.Count||start.Any(p=>!accepted.TryGetValue(p.Key,out long high)||p.Value!=high))
            throw new InvalidOperationException("b10_start_vector_mismatch");
        RequireCoverage(start,end);
        RequireCoverage(end,captured);
        RequireCoverage(captured,current);
        return new(Create(start,end),Create(end,captured));
    }

    internal static B10LateWriteJudgement Classify(IReadOnlyList<RunIdentity> producers,
        IReadOnlyList<IReadOnlyList<LedgerRow>> ledgers, IReadOnlyList<ConsumedRecord> original,
        IReadOnlyList<ConsumedRecord> late, IReadOnlySet<string> earlierInstances)
    {
        if(producers.Count!=2||ledgers.Count!=2||producers[0].InstanceId==producers[1].InstanceId)
            return Invalid("b10_ownership_invalid");
        var owned=producers.Select(p=>p.InstanceId).ToHashSet(StringComparer.Ordinal);
        if(earlierInstances.Overlaps(owned))return Invalid("b10_ownership_invalid");
        var rows=new List<LedgerRow>();
        var joins=new Dictionary<(string Instance,string Attempt),LedgerRow>();
        for(int role=0;role<2;role++)
            foreach(LedgerRow row in ledgers[role])
            {
                if(row.Identity!=producers[role]||row.Attempt.InstanceId!=producers[role].InstanceId||
                    !joins.TryAdd((row.Attempt.InstanceId,row.Attempt.AttemptId),row))
                    return Invalid("b10_ledger_identity_invalid");
                rows.Add(row);
            }
        var records=new List<ConsumedRecord>();
        var coordinates=new HashSet<(string Topic,int Partition,long Offset)>();
        foreach(ConsumedRecord record in original)
        {
            if(record.InstanceId is not null&&earlierInstances.Contains(record.InstanceId)&&
                !record.Problems.Any(p=>p.Field=="instance-id"))continue;
            if(record.InstanceId is null||!owned.Contains(record.InstanceId))return Invalid("b10_original_owner_invalid");
            if(!Retain(record))return Invalid("b10_record_reconciliation_invalid");
        }
        foreach(ConsumedRecord record in late)
        {
            // A tail belongs to this repeat only when its instance header is unambiguous.
            if(record.InstanceId is null||!owned.Contains(record.InstanceId)||record.Problems.Any(p=>p.Field=="instance-id"))continue;
            if(!Retain(record))return Invalid("b10_record_reconciliation_invalid");
        }
        return new(true,OutcomeClassifier.Classify(rows,records),[]);

        bool Retain(ConsumedRecord record)
        {
            if(record.Problems.Count!=0||record.InstanceId is null||record.AttemptId is null||record.Key is null||record.Sequence is null||
                !joins.TryGetValue((record.InstanceId,record.AttemptId),out LedgerRow? row)||
                row.Attempt.Key!=record.Key||row.Attempt.Sequence!=record.Sequence||
                !coordinates.Add((record.Topic,record.Partition,record.Offset)))return false;
            records.Add(record);
            return true;
        }
    }

    private static B10LateWriteJudgement Invalid(string reason)=>new(false,null,[reason]);

    private static Dictionary<int,long> Vector(IReadOnlyList<WatermarkOffset>? vector,int maxPartitions)
    {
        if(vector is null||vector.Count==0||vector.Count>maxPartitions)throw new InvalidOperationException("b10_vector_invalid");
        var result=new Dictionary<int,long>();
        foreach(WatermarkOffset high in vector)
            if(high.Partition<0||high.Offset<0||!result.TryAdd(high.Partition,high.Offset))
                throw new InvalidOperationException("b10_vector_invalid");
        return result;
    }

    private static void RequireCoverage(IReadOnlyDictionary<int,long> earlier,IReadOnlyDictionary<int,long> later)
    {
        foreach((int partition,long high) in earlier)
            if(!later.TryGetValue(partition,out long next)||next<high)throw new InvalidOperationException("b10_vector_regressed");
    }

    private static RunWindow Create(IReadOnlyDictionary<int,long> starts,IReadOnlyDictionary<int,long> ends)=>
        new(starts.OrderBy(p=>p.Key).Select(p=>new PartitionHigh(p.Key,p.Value)).ToArray(),ends,
            ends.OrderBy(p=>p.Key).Select(p=>new PartitionWindow(p.Key,starts.GetValueOrDefault(p.Key),p.Value)).ToArray(),[],true);
}
