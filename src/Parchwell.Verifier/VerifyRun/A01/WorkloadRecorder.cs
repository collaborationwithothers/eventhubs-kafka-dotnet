using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.A01;

internal sealed record WorkloadRecording(WorkloadObservation? Observation,IReadOnlyList<ResultDiagnostic> Diagnostics);

/// <summary>Records source-backed synthetic workload facts without turning them into judgement inputs.</summary>
internal static class WorkloadRecorder
{
    private static readonly (string Id,long Rate,long Start,long End)[] Profile=[
        ("ramp100",100,0,60_000_000),("ramp300",300,60_000_000,120_000_000),
        ("ramp900",900,120_000_000,180_000_000),("ramp1800",1800,180_000_000,240_000_000)];

    internal static WorkloadRecording Record(ParseResult<SealedRunManifest> manifest,ParseResult<IReadOnlyList<LedgerRow>> ledger)
    {
        SealedRunManifest? m=manifest.Value;
        IReadOnlyList<LedgerRow>? rows=ledger.Value;
        if(m is null&&rows is null)return new(null,[]);
        var diagnostics=new List<ResultDiagnostic>();
        long? terminal=rows is null?null:CheckedCount(rows.Count,diagnostics,"ledger.jsonl");
        long? planned=m?.PlannedAttemptCount;
        long? invoked=m is null?null:CheckedCount(m.Inventory.Count,diagnostics,"manifest.json");
        bool? fixedComplete=m is null?null:m.WorkerStatus=="complete"&&m.BookkeepingComplete&&m.AttemptCount==m.PlannedAttemptCount;
        long? first=m?.FirstUnexecutedOrdinal.Presence==Parchwell.Verifier.Common.Serialization.FieldPresence.Value?m.FirstUnexecutedOrdinal.Value:null;
        IReadOnlyList<WorkloadStageObservation>? stages=m is null?null:BuildStages(m,rows,diagnostics);
        if(planned<0||invoked<0||terminal<0||first<0)diagnostics.Add(new(29,"workload_summary_overflow",m is null?"ledger.jsonl":"manifest.json",null,null,null));
        var observation=new WorkloadObservation(true,Nonnegative(planned),Nonnegative(invoked),Nonnegative(terminal),fixedComplete,Nonnegative(first),stages);
        return new(observation,diagnostics.Distinct().ToArray());
    }

    private static List<WorkloadStageObservation> BuildStages(SealedRunManifest manifest,IReadOnlyList<LedgerRow>? rows,List<ResultDiagnostic> diagnostics)
    {
        var result=new List<WorkloadStageObservation>(4);
        foreach(var stage in Profile)
        {
            long inv=manifest.Inventory.LongCount(a=>a.PlannedMicroseconds>=stage.Start&&a.PlannedMicroseconds<stage.End);
            long? delivered=rows is null?null:rows.LongCount(r=>r.Delivered&&r.Attempt.PlannedMicroseconds>=stage.Start&&r.Attempt.PlannedMicroseconds<stage.End);
            long? failed=rows is null?null:rows.LongCount(r=>!r.Delivered&&r.Attempt.PlannedMicroseconds>=stage.Start&&r.Attempt.PlannedMicroseconds<stage.End);
            if(inv<0||delivered<0||failed<0){diagnostics.Add(new(29,"workload_summary_overflow","manifest.json",null,null,null));inv=0;delivered=null;failed=null;}
            result.Add(new(stage.Id,stage.Rate,60,inv,delivered,failed));
        }
        return result;
    }

    private static long? CheckedCount(int count,List<ResultDiagnostic> diagnostics,string artifact)
    {
        try{return checked((long)count);}catch(OverflowException){diagnostics.Add(new(29,"workload_summary_overflow",artifact,null,null,null));return null;}
    }
    private static long? Nonnegative(long? value)=>value is >=0?value:null;
}
