using System.Text.Json.Serialization;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

internal sealed record AbruptCoordinate(int Partition, long Offset);
internal sealed record AbruptCopy(int Partition, long Offset, string? InstanceId, string? AttemptId, string Role);
internal sealed record AbruptWitness(string OriginalAttemptId, string ResendAttemptId,
    AbruptCoordinate Original, AbruptCoordinate Resend);
internal sealed record AbruptMember(string OriginalAttemptId, long Sequence, string PayloadSha256,
    string? ResendAttemptId, AbruptTerminal? ResendTerminal, long? CopyCount, long? OriginalCopies,
    long? ResendCopies, long? OtherCopies, long? PairCount, IReadOnlyList<AbruptCopy>? Copies, bool? CopiesTruncated, bool? ResendLost,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DuplicateBreakdown? DuplicateBreakdown = null);
internal sealed record AbruptArm(string Arm, string Key, long UnconfirmedCount, long? ResendInvoked,
    long? OriginalsSeen, long? ResendsSeen, long? PairCount, AbruptWitness? Witness, IReadOnlyList<AbruptMember> Members,
    long UnreportedAttempts, long? UnreportedSeen, long? UnreportedUnseen, long? LostResends,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DuplicateBreakdown? DuplicateBreakdown = null);

internal static class AbruptClassification
{
    internal static IReadOnlyList<AbruptArm> Evaluate(AbruptFirstEvidence first, AbruptSuccessorEvidence? successor,
        IReadOnlyList<ConsumedRecord> records, bool complete, int maximumCopies, bool lossVerified = true,
        IReadOnlySet<string>? timeoutInstances = null)
    {
        return new[] { Arm("same_instance", first.Control), Arm("cross_restart", first.Kill) };

        AbruptArm Arm(string arm, IReadOnlyList<string> originals)
        {
            bool authenticated = arm == "same_instance" || successor is not null;
            var members = new List<AbruptMember>();
            var witnesses = new List<AbruptWitness>();
            long originalsSeen = 0, resendsSeen = 0, pairs = 0;
            foreach (string id in originals)
            {
                AbruptAttempt original = first.Attempts[id];
                string? resend = arm == "same_instance" ? first.ControlResends[id] : successor?.Resends[id];
                IReadOnlyDictionary<string, AbruptTerminal>? terminals = arm == "same_instance" ? first.Terminals : successor?.Terminals;
                AbruptTerminal? terminal = resend is not null && terminals?.TryGetValue(resend, out AbruptTerminal? known) == true ? known : null;
                ConsumedRecord[] matched = complete ? records.Where(record => record.Key == original.Key &&
                    record.Sequence == original.Sequence && record.ValueSha256 == original.PayloadSha256).ToArray() : [];
                AbruptCopy[] copies = complete ? matched.OrderBy(record => record.Partition).ThenBy(record => record.Offset)
                    .Select(record => new AbruptCopy(record.Partition, record.Offset, record.InstanceId, record.AttemptId,
                        record.InstanceId == first.Pod && record.AttemptId == id ? "original" :
                        resend is not null && record.InstanceId == (arm == "same_instance" ? first.Pod : successor!.Pod) &&
                        record.AttemptId == resend ? "resend" : "other")).ToArray() : [];
                long originalCopies = copies.LongCount(copy => copy.Role == "original"), resendCopies = copies.LongCount(copy => copy.Role == "resend");
                long pairCount = checked(originalCopies * resendCopies);
                if (originalCopies > 0) originalsSeen++;
                if (resendCopies > 0) resendsSeen++;
                pairs = checked(pairs + pairCount);
                if (pairCount > 0)
                {
                    AbruptCopy firstOriginal = copies.First(copy => copy.Role == "original"), firstResend = copies.First(copy => copy.Role == "resend");
                    witnesses.Add(new(id, resend!, new(firstOriginal.Partition, firstOriginal.Offset), new(firstResend.Partition, firstResend.Offset)));
                }
                bool? resendLost = !complete || !authenticated || terminal is null ? null :
                    terminal.Delivered && resendCopies == 0 ? lossVerified ? true : null : false;
                ConsumedRecord[] cohort = matched.Where(record => !IsTimeout(record)).ToArray();
                bool opaque = cohort.Any(record => !Authenticated(record));
                bool ambiguous = records.Any(record => !IsTimeout(record) && record.Key is null &&
                    !(record.InstanceId == first.Pod && record.AttemptId is not null &&
                      (first.Attempts.ContainsKey(record.AttemptId) || first.Warmups.ContainsKey(record.AttemptId))) &&
                    !(successor is not null && record.InstanceId == successor.Pod && record.AttemptId is not null &&
                      successor.Resends.Values.Contains(record.AttemptId, StringComparer.Ordinal)));
                bool malformed = records.Any(record => !IsTimeout(record) &&
                    (record.Key == original.Key && (record.Sequence == original.Sequence || record.Sequence is null) ||
                     record.InstanceId == first.Pod && record.AttemptId == id ||
                     resend is not null && record.InstanceId == (arm == "same_instance" ? first.Pod : successor?.Pod) && record.AttemptId == resend) &&
                    (record.Problems.Count != 0 || record.Key != original.Key || record.Sequence != original.Sequence ||
                     record.ValueSha256 != original.PayloadSha256));
                DuplicateBreakdown? breakdown = complete && authenticated && !opaque && !ambiguous && !malformed
                    ? DuplicateClassifier.TryClassify(cohort) : null;
                members.Add(new(id, original.Sequence, original.PayloadSha256, resend, terminal,
                    complete ? copies.LongLength : null, complete ? originalCopies : null,
                    complete && authenticated ? resendCopies : null, complete ? copies.LongCount(copy => copy.Role == "other") : null,
                    complete && authenticated ? pairCount : null, complete ? copies.Take(maximumCopies).ToArray() : null,
                    complete ? copies.Length > maximumCopies : null,
                    resendLost, breakdown));

                bool IsTimeout(ConsumedRecord record) => record.InstanceId is not null &&
                    timeoutInstances?.Contains(record.InstanceId) == true;

                bool Authenticated(ConsumedRecord record) =>
                    record.InstanceId == first.Pod && record.AttemptId is not null &&
                        first.Accepted.Contains(record.AttemptId) && !first.Rejected.Contains(record.AttemptId) &&
                        first.Attempts.TryGetValue(record.AttemptId, out AbruptAttempt? attempt) &&
                        attempt.Key == record.Key && attempt.Sequence == record.Sequence && attempt.PayloadSha256 == record.ValueSha256 ||
                    successor is not null && record.InstanceId == successor.Pod && record.AttemptId is not null &&
                        successor.Resends.TryGetValue(id, out string? successorAttempt) && successorAttempt == record.AttemptId;
            }
            AbruptWitness? witness = witnesses.OrderBy(item => item.OriginalAttemptId, StringComparer.Ordinal)
                .ThenBy(item => item.ResendAttemptId, StringComparer.Ordinal).ThenBy(item => item.Original.Partition)
                .ThenBy(item => item.Original.Offset).ThenBy(item => item.Resend.Partition).ThenBy(item => item.Resend.Offset).FirstOrDefault();
            string[] unreported = first.Attempts.Values.Where(attempt => attempt.Arm == arm &&
                first.Accepted.Contains(attempt.Id) && !first.Terminals.ContainsKey(attempt.Id) && !first.Rejected.Contains(attempt.Id))
                .Select(attempt => attempt.Id).ToArray();
            long unreportedSeen = unreported.LongCount(id => records.Any(record => record.InstanceId == first.Pod && record.AttemptId == id));
            return new(arm, first.Attempts[originals[0]].Key, CountOriginals(originals),
                authenticated ? CountOriginals(originals) : null, complete ? originalsSeen : null,
                complete && authenticated ? resendsSeen : null, complete && authenticated ? pairs : null,
                complete && authenticated ? witness : null, members, unreported.LongLength,
                complete ? unreportedSeen : null, complete ? unreported.LongLength - unreportedSeen : null,
                complete && authenticated && members.All(member => member.ResendLost.HasValue)
                    ? members.LongCount(member => member.ResendLost == true) : null,
                complete && authenticated ? DuplicateBreakdown.Sum(members.Select(member => member.DuplicateBreakdown)) : null);
        }
    }

    private static long CountOriginals(IReadOnlyList<string> originals) => originals.Count;
}
