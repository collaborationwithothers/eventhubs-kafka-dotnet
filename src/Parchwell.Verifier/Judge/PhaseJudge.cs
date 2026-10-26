using Parchwell.Verifier.Common.Results;

namespace Parchwell.Verifier.Judge;

/// <summary>Typed, already bounded evidence for one expected phase repeat.</summary>
internal sealed record PhaseRepeatEvidence(
    int Repeat,
    bool SlotPresent,
    bool StartLineAccepted,
    bool PublicationAccepted,
    bool VerifierReceiptValid,
    bool? JobBindingPass,
    bool? RegistrationCountPass,
    bool? InventoryCoveragePass,
    bool KnownInvalid,
    bool TrustedIncomplete,
    OutcomeCounts? Counts,
    ProbeRepeatObservation? ProbeObservation,
    string? Identity = null,
    WorkloadObservation? WorkloadObservation = null,
    BrokerConnectionCount? BrokerCount = null);

internal sealed record PhaseRepeatChecks(bool J1, bool J2, bool J3, bool J4, bool J5, bool J6, bool J8);
internal sealed record PhaseRepeatResult(int Repeat, string Status, IReadOnlyList<string> Reasons, PhaseRepeatChecks Checks,
    OutcomeCounts? Counts, ProbeRepeatObservation? ProbeObservation, string? Identity, WorkloadObservation? WorkloadObservation = null,
    BrokerConnectionCount? BrokerCount = null);
internal sealed record PhaseJudgement(string Status, string? Gate, OutcomeCounts? Counts,
    IReadOnlyList<PhaseRepeatResult> Repeats, IReadOnlyList<string> Reasons);

/// <summary>Aggregates exactly five repeat slots and keeps message and probe decisions independent.</summary>
internal static class PhaseJudge
{
    internal static PhaseJudgement Evaluate(string phase, IReadOnlyList<PhaseRepeatEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (phase is not ("probe" or "C0" or "C3")) throw new ArgumentException("Unsupported phase.", nameof(phase));
        var byRepeat = evidence.GroupBy(x => x.Repeat).ToDictionary(x => x.Key, x => x.ToArray());
        bool extra = evidence.Any(x => x.Repeat is < 1 or > 5);
        var repeats = new List<PhaseRepeatResult>(5);
        var reasons = new List<string>();
        for (int number = 1; number <= 5; number++)
        {
            PhaseRepeatEvidence? e = byRepeat.TryGetValue(number, out var values) && values.Length == 1 ? values[0] : null;
            bool duplicate = byRepeat.TryGetValue(number, out values) && values.Length != 1;
            bool j1 = e?.SlotPresent == true && !duplicate && !extra;
            bool j2 = e is { StartLineAccepted: true, PublicationAccepted: true };
            bool j3 = e?.VerifierReceiptValid == true;
            bool j4 = e?.JobBindingPass == true;
            bool j5 = e?.RegistrationCountPass == true;
            bool j6 = e?.InventoryCoveragePass == true;
            bool usable = j1 && j2 && j3 && j4 && j5 && j6 && e?.ProbeObservation?.Usable == true;
            bool j8 = phase == "probe" && usable;
            var rr = new List<string>();
            if (!j1) rr.Add("j1_repeat_slots");
            if (!j2) rr.Add("j2_publication_unaccepted");
            if (!j3) rr.Add("j3_verifier_receipt");
            if (!j4) rr.Add("j4_producer_job_binding");
            if (!j5) rr.Add("j5_registered_producer_count");
            if (!j6) rr.Add("j6_broker_inventory_coverage");
            bool invalid = e?.KnownInvalid == true || (e is not null && (e.JobBindingPass == false || e.RegistrationCountPass == false || e.InventoryCoveragePass == false));
            string status = invalid ? "invalid" : j1 && j2 && j3 && j4 && j5 && j6 && !e!.TrustedIncomplete ? "complete_valid" : "incomplete";
            if (invalid && e?.KnownInvalid == true) rr.Add("trusted_result_invalid");
            bool complete=status=="complete_valid";
            repeats.Add(new(number,status,rr,new(j1,j2,j3,j4,j5,j6,j8),complete?e?.Counts:null,
                e is { StartLineAccepted:true, PublicationAccepted:true, VerifierReceiptValid:true, JobBindingPass:true,
                    RegistrationCountPass:true, InventoryCoveragePass:true } ? e.ProbeObservation : null,e?.Identity,
                e is { StartLineAccepted:true, PublicationAccepted:true, VerifierReceiptValid:true, JobBindingPass:true,
                    RegistrationCountPass:true, InventoryCoveragePass:true } ? e.WorkloadObservation : null,
                e is { StartLineAccepted:true, PublicationAccepted:true, VerifierReceiptValid:true, JobBindingPass:true,
                    RegistrationCountPass:true, InventoryCoveragePass:true } ? e.BrokerCount : null));
            reasons.AddRange(rr);
        }
        if (extra || evidence.GroupBy(x => x.Repeat).Any(g => g.Count() > 1)) reasons.Add("j1_repeat_slots");
        string statusAll = repeats.Any(x => x.Status == "invalid") ? "invalid"
            : repeats.Any(x => x.Status != "complete_valid") ? "incomplete" : "complete_valid";
        OutcomeCounts? totals = null;
        bool allChecks = repeats.All(r => r.Checks.J1 && r.Checks.J2 && r.Checks.J3 && r.Checks.J4 && r.Checks.J5 && r.Checks.J6);
        if (allChecks && repeats.All(r => r.Status == "complete_valid" && r.Counts is not null))
        {
            try
            {
                var counts=repeats.Select(r=>r.Counts!).ToArray();
                totals=new(checked(counts.Sum(x=>x.Ok)),checked(counts.Sum(x=>x.Loss)),checked(counts.Sum(x=>x.Phantom)),checked(counts.Sum(x=>x.ReportedFailure)),checked(counts.Sum(x=>x.Duplicate)),checked(counts.Sum(x=>x.Reorder)));
            }
            catch(OverflowException) { reasons.Add("j7_counts_unavailable"); statusAll="incomplete"; totals=null; }
        }
        else reasons.Add("j7_counts_unavailable");
        string? gate=null;
        if(phase=="probe")
        {
            bool allUsable=repeats.All(r=>r.Checks.J8);
            if(!allUsable) gate="inconclusive";
            else gate=repeats.Any(r=>r.ProbeObservation?.Qualifying==true)?"pass":"untestable_at_build_scale";
            if(gate=="inconclusive") reasons.Add("j8_probe_inconclusive");
        }
        string[] sorted=reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(100).ToArray();
        return new(statusAll,gate,totals,repeats,sorted);
    }
}
