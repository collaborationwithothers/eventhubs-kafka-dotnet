using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

internal sealed record AbruptCoordinate(int Partition, long Offset);
internal sealed record AbruptCopy(int Partition, long Offset, string? InstanceId, string? AttemptId, string Role);
internal sealed record AbruptWitness(string OriginalAttemptId, string ResendAttemptId,
    AbruptCoordinate Original, AbruptCoordinate Resend);
internal sealed record AbruptMember(string OriginalAttemptId, long Sequence, string PayloadSha256,
    string? ResendAttemptId, AbruptTerminal? ResendTerminal, long? CopyCount, long? OriginalCopies,
    long? ResendCopies, long? OtherCopies, long? PairCount, IReadOnlyList<AbruptCopy>? Copies, bool? CopiesTruncated, bool? ResendLost);
internal sealed record AbruptArm(string Arm, string Key, long UnconfirmedCount, long? ResendInvoked,
    long? OriginalsSeen, long? ResendsSeen, long? PairCount, AbruptWitness? Witness, IReadOnlyList<AbruptMember> Members,
    long UnreportedAttempts, long? UnreportedSeen, long? UnreportedUnseen, long? LostResends);

internal static class AbruptClassification
{
    internal static IReadOnlyList<AbruptArm> Evaluate(AbruptFirstEvidence first, AbruptSuccessorEvidence? successor,
        IReadOnlyList<ConsumedRecord> records, bool complete, int maximumCopies, bool lossVerified = true)
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
                AbruptCopy[] copies = complete ? records.Where(record => record.Key == original.Key &&
                    record.Sequence == original.Sequence && record.ValueSha256 == original.PayloadSha256)
                    .OrderBy(record => record.Partition).ThenBy(record => record.Offset)
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
                members.Add(new(id, original.Sequence, original.PayloadSha256, resend, terminal,
                    complete ? copies.LongLength : null, complete ? originalCopies : null,
                    complete && authenticated ? resendCopies : null, complete ? copies.LongCount(copy => copy.Role == "other") : null,
                    complete && authenticated ? pairCount : null, complete ? copies.Take(maximumCopies).ToArray() : null,
                    complete ? copies.Length > maximumCopies : null,
                    !complete || !authenticated || terminal is null ? null :
                    terminal.Delivered && resendCopies == 0 ? lossVerified ? true : null : false));
            }
            AbruptWitness? witness = witnesses.OrderBy(item => item.OriginalAttemptId, StringComparer.Ordinal)
                .ThenBy(item => item.ResendAttemptId, StringComparer.Ordinal).ThenBy(item => item.Original.Partition)
                .ThenBy(item => item.Original.Offset).ThenBy(item => item.Resend.Partition).ThenBy(item => item.Resend.Offset).FirstOrDefault();
            string[] unreported = first.Attempts.Values.Where(attempt => attempt.Arm == arm &&
                first.Accepted.Contains(attempt.Id) && !first.Terminals.ContainsKey(attempt.Id) && !first.Rejected.Contains(attempt.Id))
                .Select(attempt => attempt.Id).ToArray();
            long unreportedSeen = unreported.LongCount(id => records.Any(record => record.InstanceId == first.Pod && record.AttemptId == id));
            return new(arm, first.Attempts[originals[0]].Key, originals.Count,
                authenticated ? originals.Count : null, complete ? originalsSeen : null,
                complete && authenticated ? resendsSeen : null, complete && authenticated ? pairs : null,
                complete && authenticated ? witness : null, members, unreported.LongLength,
                complete ? unreportedSeen : null, complete ? unreported.LongLength - unreportedSeen : null,
                complete && authenticated && members.All(member => member.ResendLost.HasValue)
                    ? members.LongCount(member => member.ResendLost == true) : null);
        }
    }
}
