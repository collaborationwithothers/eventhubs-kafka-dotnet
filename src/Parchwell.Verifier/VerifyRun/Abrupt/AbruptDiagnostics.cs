using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

internal static class AbruptDiagnostics
{
    internal static readonly string[] ReasonNames =
    [
        "b10_restart_configuration_invalid", "b10_restart_first_evidence_unavailable",
        "b10_restart_identity_unauthenticated", "b10_restart_termination_unverified",
        "b10_restart_native_error_captured", "b10_restart_fatal_captured",
        "b10_restart_timeout_bookkeeping_unavailable", "b10_abrupt_control_incomplete",
        "b10_restart_successor_evidence_unavailable", "b10_restart_resend_count_invalid",
        "b10_abrupt_successor_late", "b10_restart_window_incomplete", "b10_restart_guard_failed"
    ];

    internal static string Comparison(IReadOnlyList<AbruptArm>? arms, IReadOnlySet<int> reasons)
    {
        if (arms is null || reasons.Any(reason => reason is >= 1 and <= 4 or >= 8 and <= 12)) return "unavailable";
        bool same = arms[0].PairCount >= 1, cross = arms[1].PairCount >= 1;
        return (same, cross) switch
        {
            (true, true) => "both",
            (true, false) => "same_instance_only",
            (false, true) => "cross_restart_only",
            _ => "neither"
        };
    }
}

/// <summary>Checks every non-timeout record against authenticated probe or warm-up evidence.</summary>
internal static class AbruptRecordJoin
{
    internal static bool HasUnjoined(AbruptFirstEvidence first, AbruptSuccessorEvidence? successor,
        IReadOnlyList<ConsumedRecord> records, string? timeoutInstance, string? successorDirectory)
    {
        HashSet<string> keys = first.Attempts.Values.Select(attempt => attempt.Key).ToHashSet(StringComparer.Ordinal);
        foreach (ConsumedRecord record in records)
        {
            if (record.InstanceId == timeoutInstance && timeoutInstance is not null) continue;
            if (successor is null && successorDirectory is not null && record.InstanceId == successorDirectory) continue;
            // Malformed arm records belong to check 6, including absent identity
            // headers. Their copies still reach classification unchanged.
            if (record.Key is not null && keys.Contains(record.Key) &&
                (record.Problems.Count != 0 || record.ValueSha256 is null)) continue;
            if (record.Problems.Count != 0 || record.AttemptId is null) return true;
            if (record.InstanceId == first.Pod)
            {
                if (first.Attempts.TryGetValue(record.AttemptId, out AbruptAttempt? attempt) &&
                    Matches(record, attempt.Key, attempt.Sequence, attempt.PayloadSha256)) continue;
                if (first.Warmups.TryGetValue(record.AttemptId, out AbruptWarmup? warmup) &&
                    Matches(record, warmup.Key, -1, warmup.PayloadSha256)) continue;
            }
            else if (successor is not null && record.InstanceId == successor.Pod)
            {
                KeyValuePair<string, string>[] matches = successor.Resends.Where(resend => resend.Value == record.AttemptId).ToArray();
                if (matches.Length == 1)
                {
                    AbruptAttempt original = first.Attempts[matches[0].Key];
                    if (Matches(record, original.Key, original.Sequence, original.PayloadSha256)) continue;
                }
            }
            return true;
        }
        return false;
    }

    private static bool Matches(ConsumedRecord record, string key, long sequence, string hash) =>
        record.Key == key && record.Sequence == sequence && record.ValueSha256 == hash;
}
