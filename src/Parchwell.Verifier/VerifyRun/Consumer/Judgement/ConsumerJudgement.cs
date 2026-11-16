using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;

namespace Parchwell.Verifier.VerifyRun.Consumer.Judgement;

internal sealed record ConsumerJudgementRequest(VerifierResultStatus RootStatus, string RunId,string ScenarioId,string ConfigurationId,string Phase,int Repeat,
    string GroupId,ConsumerVerificationPlan Plan,string BootstrapServers,ConsumerArtifacts Artifacts,ConsumerStartSnapshot? Snapshot,
    RunWindow Window,IReadOnlyList<LedgerRow> LedgerRows,IReadOnlyList<ConsumedRecord> ConsumedRecords,DateTimeOffset DeadlineInstant);
internal sealed record ConsumerJudgementResult(VerifierResultStatus Status, JsonElement Consumer, bool ShouldRead);

internal static class ConsumerJudgement
{
    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower};
    internal static ConsumerJudgementResult Evaluate(ConsumerJudgementRequest request,ConsumerOffsetRead read,bool final=true)
    {
        var invalid=new SortedSet<string>(StringComparer.Ordinal);string? incomplete=null;var diagnostics=request.Artifacts.Diagnostics.ToList();
        ConsumerMember[] members=request.Artifacts.Members.Where(m=>m.Registration is not null).ToArray();
        bool skip=false;
        if(request.RootStatus!=VerifierResultStatus.CompleteValid){invalid.Add("producer_side_not_complete_valid");skip=true;}
        else if(request.Artifacts.Unavailable){incomplete="consumer_record_unavailable";skip=true;}
        else
        {
            bool registrationShape=request.Artifacts.RegistrationCount==2&&request.Artifacts.Members.Count==2&&members.Length==2&&members.All(m=>!m.RegistrationInvalid)&&members.Count(m=>m.Registration!.Role=="subject")==1&&members.Count(m=>m.Registration!.Role=="witness")==1;
            bool registrationIdentity=members.All(m=>{ConsumerRegistration r=m.Registration!;return r.RunId==request.RunId&&r.ScenarioId==request.ScenarioId&&r.ConfigurationId==request.ConfigurationId&&r.Phase==request.Phase&&r.RepeatNumber==request.Repeat&&r.GroupId==request.GroupId&&r.PodName==r.InstanceId;});
            if(!registrationShape||!registrationIdentity)invalid.Add("consumer_registration_invalid");
            if(!registrationShape||!registrationIdentity)skip=true;
            if(!skip)
            {
                var ledgerByIdentity=new Dictionary<(string?,string?),LedgerRow>();
                foreach(LedgerRow row in request.LedgerRows)ledgerByIdentity.TryAdd((row.Identity.InstanceId,row.Attempt.AttemptId),row);
                var consumedIdentities=request.ConsumedRecords.Select(c=>((int?)c.Partition,(long?)c.Offset,c.InstanceId,c.AttemptId)).ToHashSet();
                foreach(ConsumerMember member in members)
                {
                    if(member.RecordInvalid)invalid.Add("consumer_record_invalid");
                    CheckSettings(member);
                    foreach(ConsumerRow row in member.Rows.Where(r=>r.Kind=="processed"))
                    {
                        PartitionWindow? window=request.Window.Partitions.FirstOrDefault(p=>p.Partition==row.Partition);
                        if(window is null||row.Offset<window.Start||row.Offset>=window.End){Add("consumer_processed_outside_window",member,row);continue;}
                        LedgerRow? ledger=row.ProducerInstanceId is null||row.AttemptId is null?null:ledgerByIdentity.GetValueOrDefault((row.ProducerInstanceId,row.AttemptId));
                        if(row.Key is null||row.Sequence is null||ledger is null||ledger.Attempt.Key!=row.Key||ledger.Attempt.Sequence!=row.Sequence||!consumedIdentities.Contains((row.Partition,row.Offset,row.ProducerInstanceId,row.AttemptId)))Add("consumer_processed_unjoined",member,row);
                    }
                    bool subject=member.Registration!.Role=="subject";int kills=member.Rows.Count(r=>r.Kind=="kill_intent");
                    bool stopped=subject&&request.Plan.KillCount is int n?member.Rows.Count>0&&member.Rows[^1].Kind=="kill_intent"&&kills==1&&member.Completion is null&&member.Rows.Count(r=>r.Kind=="processed")==n-1:kills==0&&member.Completion is not null;
                    if(!stopped)Add("consumer_stop_invalid",member);
                }
                if(!ValidStart(request,members))invalid.Add("consumer_start_assignment_invalid");
                if(final)
                {
                    if(read.Outcome is "kafka_exception" or "partition_exception" or "invalid_response")incomplete="consumer_commit_evidence_unavailable";
                    else if(read.Outcome=="not_attempted"||read.EndedUtc>request.DeadlineInstant)incomplete="consumer_completion_deadline";
                    else if(read.Outcome=="success")
                    {
                        foreach(PartitionWindow partition in request.Window.Partitions)
                        {
                            long[] reported=members.SelectMany(m=>m.Rows).Where(r=>r.Kind=="commit"&&r.ErrorCode==0).SelectMany(r=>r.Offsets??[]).Where(o=>o.Topic==RunWindow.Topic&&o.Partition==partition.Partition&&o.Offset>=0&&o.ErrorCode==0).Select(o=>o.Offset).Distinct().ToArray();
                            long observed=read.Offsets.Single(o=>o.Partition==partition.Partition).Offset;
                            if(observed>=0?!reported.Contains(observed):observed!=-1001||reported.Length!=0)invalid.Add("consumer_commit_record_disagreement");
                            else if(partition.Start<partition.End&&(observed==-1001||observed<partition.End))incomplete="consumer_commit_frontier_short";
                        }
                    }
                }
            }
        }
        VerifierResultStatus status=invalid.Count>0?VerifierResultStatus.Invalid:incomplete is not null?VerifierResultStatus.Incomplete:VerifierResultStatus.CompleteValid;
        string[] reasons=invalid.Count>0?invalid.ToArray():incomplete is null?[]:[incomplete];
        var block=new Dictionary<string,object?>
        {
            ["schema_version"]=1,["status"]=Status(status),["reasons"]=reasons,["group_id"]=request.GroupId,["kill_count"]=request.Plan.KillCount,
            ["members"]=members.OrderBy(m=>m.Registration!.Role,StringComparer.Ordinal).Select(m=>new {m.Registration!.Role,m.Registration.InstanceId,m.Registration.NodeName,Rows=m.Rows.Count,ProcessedRows=m.Rows.Count(r=>r.Kind=="processed"),Ending=m.Completion is not null?"completion_receipt":m.Rows.Count>0&&m.Rows[^1].Kind=="kill_intent"?"kill_intent":"none"}).ToArray(),
            ["offset_read"]=new {read.Outcome,read.RequestedPartitions,StartedUtc=Stamp(read.StartedUtc),EndedUtc=Stamp(read.EndedUtc),read.ErrorCode,read.Offsets},
            ["partitions"]=request.Window.Partitions.OrderBy(p=>p.Partition).Select(p=>new {p.Partition,p.Start,p.End,Observed=read.Outcome=="success"?(long?)read.Offsets.FirstOrDefault(o=>o.Partition==p.Partition)?.Offset:null}).ToArray(),
            ["diagnostics"]=diagnostics.Take(64).Select(d=>d with { Detail=Bound(d.Detail,d.Field) }).ToArray()
        };
        if(status==VerifierResultStatus.CompleteValid&&final)
        {
            long once=0,again=0,extra=0,never=0;
            var deliveredIdentities=request.LedgerRows.Where(l=>l.Delivered).Select(l=>((string?)l.Identity.InstanceId,(string?)l.Attempt.AttemptId)).ToHashSet();
            var processedCounts=new Dictionary<(int?,long?),int>();
            foreach(ConsumerRow row in members.SelectMany(m=>m.Rows).Where(r=>r.Kind=="processed"))
                processedCounts[(row.Partition,row.Offset)]=checked(processedCounts.GetValueOrDefault((row.Partition,row.Offset))+1);
            foreach(ConsumedRecord consumed in request.ConsumedRecords.DistinctBy(c=>(c.Partition,c.Offset)))
            {
                if(!request.Window.Partitions.Any(p=>p.Partition==consumed.Partition&&consumed.Offset>=p.Start&&consumed.Offset<p.End)||!deliveredIdentities.Contains((consumed.InstanceId,consumed.AttemptId)))continue;
                int count=processedCounts.GetValueOrDefault(((int?)consumed.Partition,(long?)consumed.Offset));
                if(count==0)never++;else if(count==1)once++;else{again++;extra+=count-1;}
            }
            block["counts"]=new {ProcessedOnce=once,ProcessedAgain=again,ProcessedAgainExtraRows=extra,CommittedNeverProcessed=never};
            if(request.Plan.KillCount is not null)block["reassignment"]=Reassignment(members);
        }
        return new(status,JsonSerializer.SerializeToElement(block,JsonOptions),!skip);
        void Add(string reason,ConsumerMember member,ConsumerRow? row=null){invalid.Add(reason);diagnostics.Add(new(reason,member.Registration!.InstanceId,member.Prefix,row?.RowSequence));}
        void CheckSettings(ConsumerMember member)
        {
            ConsumerRegistration r=member.Registration!;IReadOnlyDictionary<string,string> expected=r.Role=="subject"?request.Plan.SubjectSettings:request.Plan.WitnessSettings;
            var all=new Dictionary<string,string>(expected,StringComparer.Ordinal){["group.id"]=request.GroupId,["client.id"]=r.InstanceId,["bootstrap.servers"]=request.BootstrapServers};
            foreach(string key in all.Keys.Union(r.EffectiveSettings.Keys,StringComparer.Ordinal).Order(StringComparer.Ordinal))
                if(!all.TryGetValue(key,out string? e)||!r.EffectiveSettings.TryGetValue(key,out string? actual)||e!=actual)
                {invalid.Add("consumer_configuration_mismatch");diagnostics.Add(new("consumer_configuration_mismatch",r.InstanceId,member.Prefix,Field:key,Detail:"effective setting differs"));}
        }
    }
    internal static IReadOnlySet<int> Held(ConsumerMember member,long sequence)
    {
        var held=new HashSet<int>();foreach(ConsumerRow row in member.Rows.Where(r=>r.Kind=="partition_event"&&r.RowSequence<=sequence).OrderBy(r=>r.RowSequence))
            if(row.Event=="assigned")held=new HashSet<int>(row.Partitions??[]);else held.ExceptWith(row.Partitions??[]);return held;
    }
    private static bool ValidStart(ConsumerJudgementRequest request,IReadOnlyList<ConsumerMember> members)
    {
        ConsumerStartSnapshot? snapshot=request.Snapshot;if(snapshot is null||snapshot.GroupId!=request.GroupId||snapshot.Members.Count!=2||!snapshot.Members.Select(m=>m.InstanceId).Order(StringComparer.Ordinal).SequenceEqual(members.Select(m=>m.Registration!.InstanceId).Order(StringComparer.Ordinal)))return false;
        foreach(ConsumerMember member in members)
        {
            ConsumerSnapshotMember s=snapshot.Members.Single(m=>m.InstanceId==member.Registration!.InstanceId);
            if(s.Role!=member.Registration!.Role||!member.Rows.Any(r=>r.Kind=="partition_event"&&r.Event=="assigned"&&r.RowSequence<=s.FirstReadLastRowSequence)||member.Rows.Any(r=>r.Kind=="partition_event"&&r.RowSequence>s.FirstReadLastRowSequence&&r.RowSequence<=s.SecondReadLastRowSequence))return false;
        }
        return request.Window.Partitions.All(p=>members.Count(m=>Held(m,snapshot.Members.Single(s=>s.InstanceId==m.Registration!.InstanceId).FirstReadLastRowSequence).Contains(p.Partition))==1);
    }
    private static object Reassignment(IReadOnlyList<ConsumerMember> members)
    {
        ConsumerMember subject=members.Single(m=>m.Registration!.Role=="subject"),witness=members.Single(m=>m.Registration!.Role=="witness");
        if(subject.Registration!.NodeName!=witness.Registration!.NodeName)return new {Microseconds=(long?)null,UnavailableReason=(string?)"different_nodes"};
        ConsumerRow last=subject.Rows[^1];IReadOnlySet<int> held=Held(subject,last.RowSequence);
        ConsumerRow? assigned=witness.Rows.Where(r=>r.Kind=="partition_event"&&r.Event=="assigned"&&r.Time>=last.Time&&held.All(p=>(r.Partitions??[]).Contains(p))).MinBy(r=>r.Time);
        return new {Microseconds=assigned is null?(long?)null:(assigned.Time-last.Time).Ticks/10,UnavailableReason=assigned is null?"no_reassignment":(string?)null};
    }
    private static string Status(VerifierResultStatus status)=>status switch{VerifierResultStatus.CompleteValid=>"complete_valid",VerifierResultStatus.Invalid=>"invalid",_=>"incomplete"};
    private static string? Stamp(DateTimeOffset? time)=>time?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",System.Globalization.CultureInfo.InvariantCulture);
    private static string? Bound(string? text,string? field)
    {
        if(field is not null&&(field.Contains("sasl",StringComparison.OrdinalIgnoreCase)||field.Contains("token",StringComparison.OrdinalIgnoreCase)))return "redacted";
        if(text is null)return null;while(Encoding.UTF8.GetByteCount(text)>256)text=text[..^1];return text;
    }
}
