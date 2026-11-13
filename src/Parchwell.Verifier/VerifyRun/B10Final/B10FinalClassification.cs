using System.Text.Json;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Abrupt;
using Parchwell.Verifier.VerifyRun.Configuration;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;

namespace Parchwell.Verifier.VerifyRun.B10Final;

/// <summary>Classifies owned original and late records without mixing timeout and restart validity.</summary>
internal static class B10FinalClassification
{
    internal static void SetWindows(IReadOnlyList<B10FinalInput> repeats, IReadOnlyList<PartitionHigh> sweep, int maximumPartitions)
    {
        Dictionary<int, long> captured = sweep.ToDictionary(high => high.Partition, high => high.High);
        B10FinalInput[] started = repeats.Where(repeat => repeat.Starts is not null).ToArray();
        for (int index = 0; index < started.Length; index++)
        {
            B10FinalInput repeat = started[index];
            Dictionary<int, long> next = index + 1 < started.Length ? started[index + 1].Starts! : captured;
            repeat.Ends ??= next;
            RequireVector(repeat.Starts!, repeat.Ends, maximumPartitions);
            RequireVector(repeat.Ends, captured, maximumPartitions);
            RequireVector(repeat.Ends, next, maximumPartitions);
        }
    }

    internal static B10FinalRepeat Classify(B10FinalInput input, IReadOnlyList<B10FinalInput> repeats,
        IReadOnlyList<ConsumedRecord> records, string? globalFailure, VerifierHostOptions options,
        LoadedScenario? scenario, RegisteredProducerConfigurationResolver resolver)
    {
        bool c1 = options.Phase == "C1";
        string? timeoutReason = globalFailure;
        string status = "invalid";
        OutcomeCounts? counts = null;
        HashSet<string> restartReasons = new(input.RestartReasons, StringComparer.Ordinal);
        IReadOnlyList<ConsumedRecord> original = globalFailure is null && input.Starts is not null
            ? records.Where(record => Inside(record, input.Starts, input.Ends!)).ToArray() : [];
        HashSet<string> earlier = repeats.Take(input.Number - 1).SelectMany(repeat => repeat.Instances).ToHashSet(StringComparer.Ordinal);
        bool foreign = original.Any(record => record.InstanceId is null || record.Problems.Any(problem => problem.Field == "instance-id") ||
            !input.Instances.Contains(record.InstanceId) && !earlier.Contains(record.InstanceId));
        IReadOnlyList<ConsumedRecord> union = globalFailure is null && input.Starts is not null
            ? records.Where(record => record.InstanceId is not null && input.Instances.Contains(record.InstanceId) &&
                !record.Problems.Any(problem => problem.Field == "instance-id") &&
                record.Offset >= input.Starts.GetValueOrDefault(record.Partition, long.MaxValue))
                .DistinctBy(record => (record.Topic, record.Partition, record.Offset)).ToArray() : [];
        bool unjoined = false;
        IReadOnlyList<ConsumedRecord> timeoutRecords = [];
        if (timeoutReason is null)
        {
            if (!input.Started) { timeoutReason = "b10_final_repeat_not_started"; status = "incomplete"; }
            else if (c1 && input.TimeoutInstance is null && !input.TimeoutInvalid) { timeoutReason = "b10_final_timeout_absent"; status = "incomplete"; }
            else if (!c1 && !input.InputValid) timeoutReason = "b10_final_repeat_input_invalid";
            else if (c1 && (input.TimeoutInvalid || input.Timeout is null)) timeoutReason = "b10_final_timeout_evidence_invalid";
            else if (foreign) timeoutReason = "b10_final_owner_invalid";
            else
            {
                timeoutRecords = Join(input, union, c1, options.RunId!, out unjoined);
                if (unjoined) timeoutReason = "b10_final_late_record_unjoined";
                else
                {
                    counts = c1 ? TimeoutCounts(input, timeoutRecords, scenario!, resolver, options) : OutcomeClassifier.Classify(input.Rows, timeoutRecords);
                    if (counts is null) timeoutReason = "b10_final_timeout_evidence_invalid";
                    else if (input.Nonfinal is { } data && data.GetProperty("timeout").ValueKind != JsonValueKind.Null &&
                        !AgreeTimeout(data.GetProperty("timeout"), OutcomeClassifier.Classify(input.Rows, timeoutRecords.Where(record => Inside(record, input.Starts!, input.Ends!)).ToArray())))
                        timeoutReason = "b10_final_window_mismatch";
                    else status = "complete_valid";
                }
            }
        }
        if (timeoutReason is not null) counts = null;
        B10FinalRestart? restart = null;
        if (c1)
        {
            if (globalFailure is not null) restartReasons.Add("b10_restart_sweep_unavailable");
            if (!input.Started || input.FirstInstance is null) restartReasons.Add("b10_restart_input_unavailable");
            if (input.TimeoutInvalid || timeoutReason == "b10_final_timeout_evidence_invalid") restartReasons.Add(AbruptDiagnostics.ReasonNames[6]);
            if (foreign) restartReasons.Add("b10_restart_late_record_unjoined");
            if (input.RestartInputInvalid) restartReasons.Add("b10_restart_input_unavailable");
            IReadOnlyList<B10FinalArm>? arms = null;
            IReadOnlyList<AbruptArm>? evaluated = null;
            if (input.First is not null && !input.RestartInputInvalid && input.Termination.Class != "no_kill")
            {
                bool complete = globalFailure is null && !foreign;
                evaluated = EvaluateArms(input, union, complete, options.MaxDiagnosticsPerCheck!.Value);
                if (globalFailure is null && foreign == false && (union.Any(record => record.InstanceId != input.TimeoutInstance && record.Problems.Count != 0) ||
                    AbruptRecordJoin.HasUnjoined(input.First, input.Successor, union,
                    input.TimeoutInstance is null ? new HashSet<string>() : new HashSet<string>([input.TimeoutInstance], StringComparer.Ordinal), input.SuccessorInstance)))
                { restartReasons.Add("b10_restart_late_record_unjoined"); complete = false; }
                if (input.Nonfinal is { } nonfinal && evaluated is not null && globalFailure is null &&
                    !AgreeArms(nonfinal.GetProperty("arms"), EvaluateArms(input, union.Where(record => Inside(record, input.Starts!, input.Ends!)).ToArray(), true, options.MaxDiagnosticsPerCheck.Value)))
                { restartReasons.Add("b10_restart_late_record_unjoined"); complete = false; }
                if (!input.KillTooLate && scenario is not null && input.Start is not null && globalFailure is null)
                {
                    string client = input.Successor?.Pod ?? input.First.Pod;
                    var registration = new ProducerRegistration(1, new("B10", "C1", options.RunId!, client, client, client),
                        "C1", input.Number, client, client, input.Successor is null ? input.First.Uid : client, options.SourceCommit!);
                    ResolvedRoleConfiguration expected = resolver.Resolve(scenario, registration).Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer];
                    var limits = new InputLimits(options.MaxInputDepth!.Value, options.MaxArtifactBytes!.Value, options.MaxTotalInputBytes!.Value,
                        options.MaxRowBytes!.Value, options.MaxRowsPerArtifact!.Value, options.MaxInputEntries!.Value, options.MaxDiagnosticBytes!.Value);
                    if (!AbruptGuard.Evaluate(input.First, input.Successor, input.Options!, input.Start.Inventory.Value!.Brokers.Select(broker => broker.Name).ToArray(),
                        expected, limits, new InputBudget(limits))) restartReasons.Add(AbruptDiagnostics.ReasonNames[12]);
                }
                bool emptyKillBad = input.KillTooLate && restartReasons.Any(reason => reason is not ("kill_too_late" or "b10_restart_native_error_captured" or "b10_restart_fatal_captured" or
                    "b10_restart_timeout_bookkeeping_unavailable" or "b10_restart_sweep_unavailable" or "b10_restart_late_record_unjoined"));
                if (!emptyKillBad) arms = evaluated?.Select(arm => ProjectArm(input.First, arm, complete)).ToArray();
            }
            string comparison = Comparison(arms, restartReasons);
            bool eligible = input.InputValid && input.Nonfinal is { } accepted &&
                accepted.GetProperty("eligibility").GetString() == "pending_sweep" && accepted.GetProperty("eligibility_reasons").GetArrayLength() == 0 &&
                AbruptEvidenceCodec.Strings(accepted.GetProperty("problems")).All(problem => problem == "b10_abrupt_verify_end_line_advanced") &&
                globalFailure is null && restartReasons.Count == 0 && comparison != "unavailable";
            restart = new(eligible ? "eligible" : "ineligible", OrderedReasons(restartReasons), input.Termination,
                input.FirstInstance, input.SuccessorInstance, input.TimeoutInstance, comparison,
                input.First is null ? null : new(input.First.NativeErrors.Count, input.First.NativeErrors.FatalCount, input.First.NativeErrors.Tail), arms);
        }
        return new(input.Number, input.Source, input.Versions.OrderBy(version => version.InstanceId, StringComparer.Ordinal).ToArray(), status == "complete_valid", status,
            counts, timeoutReason is null ? [] : [timeoutReason], restart);
    }

    private static OutcomeCounts? TimeoutCounts(B10FinalInput input, IReadOnlyList<ConsumedRecord> records, LoadedScenario scenario,
        RegisteredProducerConfigurationResolver resolver, VerifierHostOptions options)
    {
        ResolvedRoleConfiguration configuration = resolver.Resolve(scenario, input.Timeout!.Registered.Registration).Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer];
        return TimeoutEvidenceReader.Judge(input.Timeout, records, configuration,
            input.Start!.Inventory.Value!.Brokers.Select(broker => broker.Name).Order(StringComparer.Ordinal).ToArray(),
            new OutputLimits(options.MaxOutputBytes!.Value, options.MaxDiagnosticsPerCheck!.Value, options.MaxDiagnosticBytes!.Value));
    }

    private static IReadOnlyList<ConsumedRecord> Join(B10FinalInput input, IReadOnlyList<ConsumedRecord> records, bool c1, string run, out bool unjoined)
    {
        if (c1)
        {
            TimeoutRecordJoin result = TimeoutEvidenceReader.Join(input.Timeout!, records, run, input.Number);
            unjoined = result.Unjoined;
            return result.Records;
        }
        var rows = input.Rows.ToLookup(row => row.Attempt.AttemptId, StringComparer.Ordinal);
        ConsumedRecord[] owned = records.Where(record => record.InstanceId == input.TimeoutInstance).ToArray();
        unjoined = owned.Any(record => record.Problems.Count != 0 || record.AttemptId is null || record.Key is null || record.Sequence is null ||
            rows[record.AttemptId].Count() != 1 || rows[record.AttemptId].Single().Attempt.Key != record.Key || rows[record.AttemptId].Single().Attempt.Sequence != record.Sequence);
        return owned;
    }

    private static IReadOnlyList<AbruptArm>? EvaluateArms(B10FinalInput input, IReadOnlyList<ConsumedRecord> records, bool complete, int maximumCopies)
    {
        if (input.First is null) return null;
        if (!input.KillTooLate) return AbruptClassification.Evaluate(input.First, input.Successor, records, complete, maximumCopies);
        // The empty arm has known zero counts. The ordinary classifier addresses its first member, so evaluate only the control here.
        AbruptFirstEvidence first = input.First;
        var members = new List<AbruptMember>();
        var witnesses = new List<AbruptWitness>();
        foreach (string id in first.Control)
        {
            AbruptAttempt attempt = first.Attempts[id];
            string resend = first.ControlResends[id];
            AbruptTerminal? terminal = first.Terminals.GetValueOrDefault(resend);
            ConsumedRecord[] copies = records.Where(record => record.Key == attempt.Key && record.Sequence == attempt.Sequence && record.ValueSha256 == attempt.PayloadSha256)
                .OrderBy(record => record.Partition).ThenBy(record => record.Offset).ToArray();
            long originalCopies = copies.LongCount(record => record.InstanceId == first.Pod && record.AttemptId == id);
            long resendCopies = copies.LongCount(record => record.InstanceId == first.Pod && record.AttemptId == resend);
            if (complete && originalCopies > 0 && resendCopies > 0)
            {
                ConsumedRecord original = copies.First(record => record.InstanceId == first.Pod && record.AttemptId == id);
                ConsumedRecord resent = copies.First(record => record.InstanceId == first.Pod && record.AttemptId == resend);
                witnesses.Add(new(id, resend, new(original.Partition, original.Offset), new(resent.Partition, resent.Offset)));
            }
            members.Add(new(id, attempt.Sequence, attempt.PayloadSha256, resend, terminal, complete ? copies.LongLength : null,
                complete ? originalCopies : null, complete ? resendCopies : null, complete ? copies.LongLength - originalCopies - resendCopies : null,
                complete ? checked(originalCopies * resendCopies) : null, complete ? copies.Take(maximumCopies).Select(record => new AbruptCopy(record.Partition, record.Offset, record.InstanceId, record.AttemptId,
                    record.AttemptId == id ? "original" : record.AttemptId == resend ? "resend" : "other")).ToArray() : null,
                complete ? copies.Length > maximumCopies : null, !complete || terminal is null ? null : terminal.Delivered && resendCopies == 0));
        }
        AbruptWitness? witness = witnesses.OrderBy(item => item.OriginalAttemptId, StringComparer.Ordinal).ThenBy(item => item.ResendAttemptId, StringComparer.Ordinal)
            .ThenBy(item => item.Original.Partition).ThenBy(item => item.Original.Offset).ThenBy(item => item.Resend.Partition).ThenBy(item => item.Resend.Offset).FirstOrDefault();
        string[] unreported = first.Attempts.Values.Where(attempt => attempt.Arm == "same_instance" && first.Accepted.Contains(attempt.Id) &&
            !first.Terminals.ContainsKey(attempt.Id) && !first.Rejected.Contains(attempt.Id)).Select(attempt => attempt.Id).ToArray();
        long seen = unreported.LongCount(id => records.Any(record => record.InstanceId == first.Pod && record.AttemptId == id));
        return [new("same_instance", first.Attempts[first.Control[0]].Key, first.Control.Count, first.Control.Count,
            complete ? members.LongCount(member => member.OriginalCopies > 0) : null, complete ? members.LongCount(member => member.ResendCopies > 0) : null,
            complete ? members.Sum(member => member.PairCount!.Value) : null, witness, members, unreported.LongLength, complete ? seen : null,
            complete ? unreported.LongLength - seen : null, complete && members.All(member => member.ResendLost.HasValue) ? members.LongCount(member => member.ResendLost == true) : null),
            new("cross_restart", $"B10/{input.Options!.RunId}/C1/{input.Number}/cross_restart", 0, 0, complete ? 0 : null, complete ? 0 : null, complete ? 0 : null,
                null, [], 0, complete ? 0 : null, complete ? 0 : null, complete ? 0 : null)];
    }

    private static B10FinalArm ProjectArm(AbruptFirstEvidence first, AbruptArm arm, bool complete)
    {
        string kind = arm.Arm == "same_instance" ? "cut" : "kill_intent";
        string? cut = first.Frames.Where(frame => frame.GetProperty("kind").GetString() == kind)
            .Select(frame => frame.GetProperty("data").GetProperty("cut_attempt_id").GetString()).SingleOrDefault();
        return new(arm.Arm, cut, arm.Arm == "same_instance" ? first.Control : first.Kill, arm.UnconfirmedCount, arm.ResendInvoked,
            complete ? arm.OriginalsSeen : null, complete ? arm.ResendsSeen : null, complete ? arm.PairCount : null, arm.UnreportedAttempts,
            complete ? arm.UnreportedSeen : null, complete ? arm.UnreportedUnseen : null, complete ? arm.LostResends : null,
            complete && arm.Witness is { } witness ? new(witness.OriginalAttemptId, witness.ResendAttemptId, new(witness.Original.Partition, witness.Original.Offset), new(witness.Resend.Partition, witness.Resend.Offset)) : null);
    }

    private static bool AgreeTimeout(JsonElement expected, OutcomeCounts actual) =>
        Agree(expected, "ok", actual.Ok) && Agree(expected, "loss", actual.Loss) && Agree(expected, "phantom", actual.Phantom) &&
        Agree(expected, "reported_failure", actual.ReportedFailure) && Agree(expected, "duplicate", actual.Duplicate) && Agree(expected, "reorder", actual.Reorder);
    private static bool Agree(JsonElement expected, string field, long? actual) => !expected.TryGetProperty(field, out JsonElement value) || value.ValueKind == JsonValueKind.Null || value.GetInt64() == actual;
    private static bool AgreeArms(JsonElement expected, IReadOnlyList<AbruptArm>? actual)
    {
        if (expected.ValueKind == JsonValueKind.Null) return true;
        if (actual is null || expected.GetArrayLength() != actual.Count) return false;
        foreach (JsonElement arm in expected.EnumerateArray())
        {
            AbruptArm? found = actual.SingleOrDefault(item => item.Arm == arm.GetProperty("arm").GetString());
            if (found is null) return false;
            foreach (JsonElement member in arm.GetProperty("members").EnumerateArray())
            {
                AbruptMember? match = found.Members.SingleOrDefault(item => item.OriginalAttemptId == member.GetProperty("original_attempt_id").GetString());
                if (match is null || !Agree(member, "original_copies", match.OriginalCopies) || !Agree(member, "resend_copies", match.ResendCopies) || !Agree(member, "other_copies", match.OtherCopies)) return false;
            }
        }
        return true;
    }
    private static string Comparison(IReadOnlyList<B10FinalArm>? arms, IReadOnlySet<string> reasons)
    {
        if (arms is null || reasons.Any(reason => reason is not ("b10_restart_native_error_captured" or "b10_restart_fatal_captured" or "b10_restart_timeout_bookkeeping_unavailable" or "b10_restart_guard_failed"))) return "unavailable";
        return (arms[0].PairCount >= 1, arms[1].PairCount >= 1) switch { (true, true) => "both", (true, false) => "same_instance_only", (false, true) => "cross_restart_only", _ => "neither" };
    }
    private static string[] OrderedReasons(HashSet<string> reasons)
    {
        string[] ordered = AbruptDiagnostics.ReasonNames.Concat(["b10_restart_sweep_unavailable", "b10_restart_late_record_unjoined"])
            .Where(reasons.Contains).ToArray();
        return ordered.Concat(reasons.Except(ordered, StringComparer.Ordinal).Where(reason => reason != "b10_restart_input_unavailable").Order(StringComparer.Ordinal))
            .Concat(reasons.Contains("b10_restart_input_unavailable") ? ["b10_restart_input_unavailable"] : []).ToArray();
    }
    private static bool Inside(ConsumedRecord record, Dictionary<int, long> start, Dictionary<int, long> end) =>
        start.TryGetValue(record.Partition, out long low) && end.TryGetValue(record.Partition, out long high) && record.Offset >= low && record.Offset < high;
    private static void RequireVector(Dictionary<int, long> low, Dictionary<int, long> high, int maximum)
    {
        if (low.Count is 0 || low.Count > maximum || low.Count != high.Count || low.Any(pair => pair.Key < 0 || pair.Value < 0 ||
            !high.TryGetValue(pair.Key, out long end) || pair.Value > end)) throw new InvalidOperationException("b10_final_vector_invalid");
    }
}
