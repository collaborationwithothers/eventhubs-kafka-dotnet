using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.Judge;

/// <summary>Authenticates a final sweep and selects the ADR-021 rules without proposing a verdict.</summary>
internal static class B10FinalJudge
{
    internal const string RestartSubject = "With idempotence enabled, application recovery that replaces a killed producer and resends a durably identified but unconfirmed logical message can leave both the original and the resend in the log.";
    private static readonly InputLimits Limits = new(32, 16_777_216, 83_886_080, 1_048_576, 100_000, 1_000_000, 65_536);

    internal static async Task<int> ExecuteAsync(VerifierHostOptions options, IVerifierArtifactStore verifier,
        IProducerArtifactStore producer, TimeProvider clock, VerifierCompletionState? completion, CancellationToken cancellationToken)
    {
        DateTimeOffset started = clock.GetUtcNow();
        DateTimeOffset? phaseStarted = null;
        B10FinalProjection? projection = null;
        ArtifactReference? projectionReference = null, receiptReference = null;
        var repeats = new List<PhaseRepeatResult>(5);
        var budget = new InputBudget(Limits);
        bool trusted = false;
        try
        {
            string projectionName = PhasePaths.B10FinalSweep(options.RunId!, options.Phase!);
            string receiptName = PhasePaths.B10FinalSweepCompletion(options.RunId!, options.Phase!);
            byte[]? projectionBytes = await ReadVerifier(projectionName).ConfigureAwait(false);
            byte[]? receiptBytes = await ReadVerifier(receiptName).ConfigureAwait(false);
            Require(projectionBytes is not null && receiptBytes is not null);
            projectionReference = Reference(projectionName, projectionBytes!);
            receiptReference = Reference(receiptName, receiptBytes!);
            projection = B10FinalProjectionCodec.Read(projectionBytes!);
            WorkerReceipt receipt = PhaseWireCodec.ReadReceipt(receiptBytes!);
            B10FinalIdentity identity = projection.Identity;
            Require(receipt.Mode == "b10-final-sweep" && receipt.HandlerExitCode == 0 && receipt.Artifacts.Count == 1 &&
                receipt.Artifacts[0] == projectionReference && receipt.RunId == options.RunId && receipt.Phase == options.Phase &&
                receipt.ConfigurationId == options.ConfigurationId && receipt.ScenarioId == "B10" && receipt.SourceCommit == options.SourceCommit &&
                receipt.PodName == identity.PodName && receipt.JobName == identity.JobName);
            Require(identity.RunId == options.RunId && identity.ConfigurationId == options.ConfigurationId && identity.Phase == options.Phase &&
                identity.SourceCommit == options.SourceCommit && identity.ImageReference == options.VerifierImage &&
                identity.JobName == PhaseIdentity.B10FinalSweepJobName(options.RunId!, options.Phase!));
            byte[]? note = await ReadVerifier(PhasePaths.SweepLine(options.RunId!, options.Phase!)).ConfigureAwait(false);
            Require(projection.Data.SweepLine == (note is null ? null : Reference(PhasePaths.SweepLine(options.RunId!, options.Phase!), note)));
            var allInstances = new HashSet<string>(StringComparer.Ordinal);
            foreach (B10FinalRepeat repeat in projection.Data.Repeats)
            {
                string sourceName = options.Phase == "C0" ? PhasePaths.Result(options.RunId!, options.Phase!, repeat.Repeat) :
                    $"{options.RunId}/C1/r{repeat.Repeat}/b10-abrupt/nonfinal-result.json";
                byte[]? source = await ReadVerifier(sourceName).ConfigureAwait(false);
                Require(repeat.Source == (source is null ? null : Reference(sourceName, source)));
                B10FinalRegistrations registrations = await B10FinalRegistrationReader.ReadAsync(options, repeat.Repeat, budget,
                    (name, token) => Bounded(ct => producer.ListAsync(name, ct), token),
                    (name, maximum, token) => Bounded(ct => producer.ReadBoundedAsync(name, maximum, ct), token), cancellationToken).ConfigureAwait(false);
                foreach (string instance in registrations.InstanceIds) Require(allInstances.Add(instance));
                Require(repeat.ProducerVersions.All(version => version.InstanceId == registrations.Timeout?.Identity.InstanceId));
                if (repeat.Restart is { } restart)
                {
                    Require(restart.TimeoutInstanceId is null || restart.TimeoutInstanceId == registrations.Timeout?.Identity.InstanceId);
                    Require(restart.FirstInstanceId is null || restart.FirstInstanceId == registrations.First?.InstanceId);
                    Require(restart.SuccessorInstanceId is null || restart.SuccessorInstanceId == registrations.Successor?.InstanceId);
                }
                bool present = false, accepted = false, coverage = false;
                byte[]? startReceipt = await ReadVerifier(PhasePaths.StartLineCompletion(options.RunId!, options.Phase!, repeat.Repeat)).ConfigureAwait(false);
                present = startReceipt is not null;
                if (startReceipt is not null)
                {
                    try
                    {
                        WorkerReceipt start = PhaseWireCodec.ReadReceipt(startReceipt);
                        DateTimeOffset startTime = PhaseWireCodec.ParseUtc(start.StartedUtc);
                        if (phaseStarted is null || startTime < phaseStarted) phaseStarted = startTime;
                    }
                    catch (Exception exception) when (Malformed(exception)) { /* Timing is independently optional. */ }
                }
                if (source is not null)
                {
                    using JsonDocument sourceDocument = JsonDocument.Parse(source);
                    JsonElement root = sourceDocument.RootElement;
                    string inputReceiptName = options.Phase == "C0" ? PhasePaths.VerifyCompletion(options.RunId!, options.Phase!, repeat.Repeat) :
                        $"{options.RunId}/C1/r{repeat.Repeat}/b10-abrupt/verify-completion.json";
                    byte[]? inputReceiptBytes = await ReadVerifier(inputReceiptName).ConfigureAwait(false);
                    if (inputReceiptBytes is not null)
                    {
                        try
                        {
                            WorkerReceipt inputReceipt = PhaseWireCodec.ReadReceipt(inputReceiptBytes);
                            accepted = inputReceipt.RunId == options.RunId && inputReceipt.Phase == options.Phase &&
                                inputReceipt.Repeat == repeat.Repeat && inputReceipt.SourceCommit == options.SourceCommit &&
                                inputReceipt.Artifacts.Count == 1 && inputReceipt.Artifacts[0] == repeat.Source;
                            if (options.Phase == "C0")
                            {
                                accepted = accepted && inputReceipt.Mode == "verify" && root.GetProperty("status").GetString() == "incomplete" &&
                                    root.TryGetProperty("b10_window_checks_complete", out JsonElement complete) && complete.ValueKind == JsonValueKind.True;
                                coverage = inputReceipt.RepeatChecks?.BrokerInventoryCoverage == "pass";
                            }
                            else
                            {
                                accepted = accepted && inputReceipt.Mode == "b10-abrupt-verify";
                                coverage = !root.GetProperty("data").GetProperty("problems").EnumerateArray().Any(problem =>
                                    problem.ValueKind == JsonValueKind.String && problem.GetString() == "b10_abrupt_verify_configuration_invalid");
                            }
                        }
                        catch (Exception exception) when (Malformed(exception)) { accepted = false; }
                    }
                }
                repeats.Add(new(repeat.Repeat, repeat.Status, repeat.Reasons,
                    new(present, accepted, accepted, true, true, coverage, false), repeat.Timeout, null,
                    registrations.Timeout?.Identity.InstanceId, ClassificationFinal: repeat.ClassificationFinal));
            }
            trusted = true;
        }
        catch (Exception exception) when (Malformed(exception) || exception is IOException or TimeoutException ||
            exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            trusted = false;
        }
        if (!trusted)
        {
            repeats = Enumerable.Range(1, 5).Select(number => new PhaseRepeatResult(number, "invalid", [],
                new(false, false, false, false, false, false, false), null, null, null, ClassificationFinal: false)).ToList();
        }
        B10FinalCandidate? final = trusted ? BuildFinal(projection!, projectionReference!, receiptReference!) : null;
        string status = !trusted || !projection!.Data.ScanComplete || repeats.Any(x => x.Status == "invalid") ? "invalid" :
            repeats.All(x => x.Status == "complete_valid") ? "complete_valid" : "incomplete";
        var reasons = new HashSet<string>(repeats.SelectMany(x => x.Reasons), StringComparer.Ordinal);
        if (!trusted) reasons.Add("b10_final_projection_untrusted");
        OutcomeCounts? counts = null;
        if (trusted && repeats.All(x => x.Status == "complete_valid"))
        {
            try { counts = SumCounts(repeats.Select(x => x.Counts!)); }
            catch (OverflowException) { status = "incomplete"; }
        }
        if (counts is null) reasons.Add("j7_counts_unavailable");
        B10FinalProducerVersion[] versions = trusted ? projection!.Data.Repeats.SelectMany(x => x.ProducerVersions).ToArray() : [];
        string? CommonVersion(Func<B10FinalProducerVersion, string?> select)
        {
            string? first = versions.Length == 0 ? null : select(versions[0]);
            return !string.IsNullOrEmpty(first) && versions.All(x => StringComparer.Ordinal.Equals(first, select(x))) ? first : null;
        }
        var versionMap = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["verifier"] = typeof(JudgeHandler).Assembly.GetName().Version?.ToString(),
            ["workload_generator"] = CommonVersion(x => x.WorkloadGenerator), ["confluent_kafka"] = CommonVersion(x => x.ConfluentKafka),
            ["librdkafka"] = CommonVersion(x => x.Librdkafka), ["dotnet"] = CommonVersion(x => x.Dotnet)
        };
        if (versionMap.Where(x => x.Key != "verifier").Any(x => x.Value is null)) reasons.Add("version_unavailable");
        if (phaseStarted is null) reasons.Add("timing_unavailable");
        DateTimeOffset completed = clock.GetUtcNow();
        var candidate = new PhaseCandidate(1, true, "B10", options.ConfigurationId!, options.RunId!, options.Phase!, options.SourceCommit!, status, null,
            versionMap, new Dictionary<string, string?>(StringComparer.Ordinal) { ["loadgen"] = Digest(options.LoadGenImage), ["verifier"] = Digest(options.VerifierImage) },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["worker_started_utc"] = phaseStarted?.ToString("O", CultureInfo.InvariantCulture),
                ["judge_completed_utc"] = phaseStarted is null ? null : completed.ToString("O", CultureInfo.InvariantCulture),
                ["duration_seconds"] = phaseStarted is null ? null : (decimal?)(completed - phaseStarted.Value).TotalSeconds },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["error_codes"] = Array.Empty<string>(), ["max_throttle_ms"] = null,
                ["request_timeout_increment"] = null, ["disconnect_increment"] = null, ["rtt_p99_ms"] = null },
            repeats, counts, reasons.Order(StringComparer.Ordinal).Take(100).ToArray(), B10Final: final);
        byte[] candidateBytes = PhaseCandidateCodec.Write(candidate);
        string candidateName = PhasePaths.Candidate(options.RunId!, options.Phase!);
        if (options.CandidateOutputName != candidateName) throw new InvalidOperationException("Candidate path is not the derived phase path.");
        await Bounded(ct => verifier.CreateAsync(candidateName, candidateBytes, ct), cancellationToken).ConfigureAwait(false);
        int exit = status == "complete_valid" ? 0 : 1;
        var completionReceipt = new WorkerReceipt(1, "judge", "B10", options.ConfigurationId!, options.RunId!, options.Phase!, null,
            PhaseIdentity.JudgeJobName(options.RunId!, options.Phase!), options.PodName!, null, options.SourceCommit!, exit, true,
            started.ToString("O", CultureInfo.InvariantCulture), clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
            [Reference(candidateName, candidateBytes)], null, null);
        completion?.SetPendingBlobReceipt(options.VerifierContainer!, PhasePaths.JudgeCompletion(options.RunId!, options.Phase!), PhaseWireCodec.WriteReceipt(completionReceipt));
        return exit;

        async Task<byte[]?> ReadVerifier(string name)
        {
            byte[]? content = await Bounded(ct => verifier.ReadBoundedAsync(name, Limits.MaxArtifactBytes, ct), cancellationToken).ConfigureAwait(false);
            if (content is not null) { Require(content.LongLength <= Limits.MaxArtifactBytes); budget.ConsumeBytes(content.LongLength); }
            return content;
        }
        async Task<T> Bounded<T>(Func<CancellationToken, Task<T>> operation, CancellationToken token)
        {
            using var deadline = new CancellationTokenSource(options.IoTimeout!.Value, clock);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
            return await operation(linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
        }
        async Task Bounded(Func<CancellationToken, Task> operation, CancellationToken token)
        {
            await Bounded(async ct => { await operation(ct).ConfigureAwait(false); return true; }, token).ConfigureAwait(false);
        }
    }

    internal static B10FinalCandidate BuildFinal(B10FinalProjection projection, ArtifactReference reference, ArtifactReference receipt)
    {
        IReadOnlyList<B10FinalRepeat> repeats = projection.Data.Repeats;
        B10FinalTimeoutRule? timeout = null;
        B10FinalRestartRule? restart = null;
        if (projection.Identity.Phase == "C0")
        {
            int valid = repeats.Count(x => x.Status == "complete_valid" && x.ClassificationFinal);
            long? duplicate = null;
            if (valid == 5)
            {
                try { long total = 0; foreach (B10FinalRepeat repeat in repeats) total = checked(total + repeat.Timeout!.Duplicate); duplicate = total; }
                catch (OverflowException) { duplicate = null; }
            }
            timeout = new(valid, duplicate, duplicate is >= 1 ? "confirmed" : duplicate == 0 ? "untestable" : "held");
        }
        else
        {
            int[] eligible = repeats.Where(x => x.Restart?.Eligibility == "eligible").Select(x => x.Repeat).ToArray();
            int[] pairs = repeats.Where(x => eligible.Contains(x.Repeat) && x.Restart!.Arms?.Any(arm =>
                arm.Arm == "cross_restart" && arm.PairCount is >= 1 && arm.UnreportedSeen is >= 1) == true).Select(x => x.Repeat).ToArray();
            int[] losses = repeats.Where(x => x.Restart?.Termination.Class != "no_kill" &&
                x.Restart?.Arms?.Any(arm => arm.LostResends is >= 1) == true).Select(x => x.Repeat).ToArray();
            int[] eligibleLosses = losses.Where(eligible.Contains).ToArray();
            string selection = eligible.Length < 3 ? "held" : pairs.Length > 0 ? "confirmed" : eligibleLosses.Length > 0 ? "held" : "untestable";
            string? reason = eligible.Length < 3 ? "fewer_than_three_eligible" : pairs.Length > 0 ? null : eligibleLosses.Length > 0 ? "eligible_loss_without_pair" : "no_cross_restart_duplicate_observed";
            restart = new(5, eligible.Length, eligible, pairs, losses, eligibleLosses, selection, reason, selection == "confirmed" ? RestartSubject : null);
        }
        return new(reference, receipt, projection.Data.ScanComplete, projection.Data.ResidualRisk, repeats, timeout, restart);
    }

    private static OutcomeCounts SumCounts(IEnumerable<OutcomeCounts> counts)
    {
        long ok = 0, loss = 0, phantom = 0, failed = 0, duplicate = 0, reorder = 0;
        foreach (OutcomeCounts count in counts) checked { ok += count.Ok; loss += count.Loss; phantom += count.Phantom; failed += count.ReportedFailure; duplicate += count.Duplicate; reorder += count.Reorder; }
        return new(ok, loss, phantom, failed, duplicate, reorder);
    }
    private static bool Malformed(Exception exception) => exception is FormatException or JsonException or InvalidOperationException or ArgumentException or OverflowException;
    private static void Require(bool condition) => B10FinalProjectionCodec.Require(condition);
    private static ArtifactReference Reference(string name, byte[] bytes) => new("verifier", name, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    private static string? Digest(string? image) => image is not null && image.LastIndexOf('@') is int at && at >= 0 ? image[(at + 1)..] : null;
}

internal sealed record B10FinalCandidate(ArtifactReference Projection, ArtifactReference Receipt, bool ScanComplete,
    string ResidualRisk, IReadOnlyList<B10FinalRepeat> Repeats, B10FinalTimeoutRule? TimeoutRule, B10FinalRestartRule? RestartRule);
internal sealed record B10FinalTimeoutRule(int CompleteValidFinalRepeats, long? DuplicateTotal, string RuleSelection);
internal sealed record B10FinalRestartRule(int DeclaredRepeats, int EligibleRepeats, IReadOnlyList<int> EligibleRepeatNumbers,
    IReadOnlyList<int> QualifyingPairRepeats, IReadOnlyList<int> LostResendFindingRepeats, IReadOnlyList<int> EligibleLossRepeats,
    string RuleSelection, string? SelectionReason, string? Subject);
