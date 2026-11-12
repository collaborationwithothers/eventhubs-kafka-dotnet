using System.Text;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

/// <summary>Re-evaluates captured guard evidence without substituting a producer declaration.</summary>
internal static class AbruptGuard
{
    internal static bool Evaluate(AbruptFirstEvidence? first, AbruptSuccessorEvidence? successor,
        VerifierHostOptions options, IReadOnlyList<string> expectedBrokerNames,
        ResolvedRoleConfiguration expectedConfiguration, InputLimits limits, InputBudget budget)
    {
        try { return EvaluateCore(first, successor, options, expectedBrokerNames, expectedConfiguration, limits, budget); }
        catch (Exception exception) when (exception is InputLimitExceededException || AbruptReconciliation.Malformed(exception))
        { return false; }
    }

    private static bool EvaluateCore(AbruptFirstEvidence? first, AbruptSuccessorEvidence? successor,
        VerifierHostOptions options, IReadOnlyList<string> expectedBrokerNames,
        ResolvedRoleConfiguration expectedConfiguration, InputLimits limits, InputBudget budget)
    {
        bool valid = true;
        if (first is not null)
            foreach (string name in AbruptEvidenceCodec.ReportFiles)
            {
                if (!first.Files.TryGetValue(name, out byte[]? bytes)) { valid = false; continue; }
                ParseResult<NativeClientReport> parsed = NativeStatisticsReader.Read(bytes, name, limits,
                    budget, options.MaxNativeReportBytes!.Value, options.MaxBrokerEntries!.Value);
                valid &= parsed.IsSuccess && parsed.Value!.Brokers.Presence == FieldPresence.Value &&
                    parsed.Value.Brokers.Value!.Values.All(broker =>
                        expectedBrokerNames.Contains(broker.DictionaryKey, StringComparer.Ordinal) &&
                        broker.TransmissionErrors.Presence == FieldPresence.Value && broker.TransmissionErrors.Value == 0 &&
                        broker.ReceiveErrors.Presence == FieldPresence.Value && broker.ReceiveErrors.Value == 0);
            }
        if (successor is not null)
            valid &= Successor(successor, options, expectedBrokerNames, expectedConfiguration, limits, budget);
        return valid;
    }

    private static bool Successor(AbruptSuccessorEvidence successor, VerifierHostOptions options,
        IReadOnlyList<string> expectedBrokerNames, ResolvedRoleConfiguration expectedConfiguration,
        InputLimits limits, InputBudget budget)
    {
        ParseResult<IReadOnlyList<StatisticsEnvelope>> statistics = CapturedEvidenceReader.ReadStatistics(
            successor.Files["statistics.jsonl"], "statistics.jsonl", limits, budget);
        ParseResult<IReadOnlyList<LifecycleEvent>> lifecycle = CapturedEvidenceReader.ReadLifecycle(
            successor.Files["request-lifecycle.jsonl"], "request-lifecycle.jsonl", limits, budget);
        ParseResult<CapturedCapabilities> capabilities = CapturedEvidenceReader.ReadCapabilities(
            successor.Files["capabilities.json"], "capabilities.json", limits, budget, captureOnly: true);
        var reports = new List<ReportAssociation>();
        foreach (StatisticsEnvelope envelope in statistics.Value ?? [])
            reports.Add(new(envelope, NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(envelope.RawJson),
                "statistics.jsonl", limits, budget, options.MaxNativeReportBytes!.Value, options.MaxBrokerEntries!.Value)));
        var identity = new RunIdentity("B10", "C1", options.RunId!, successor.Pod, successor.Pod, successor.Pod);
        var unavailableIndex = ParseResult<ArtifactIndex>.Failure([]);
        var unavailableManifest = ParseResult<SealedRunManifest>.Failure([]);
        var request = new RunJudgementRequest(identity, unavailableIndex,
            ParseResult<IReadOnlyList<LedgerRow>>.Success([]), unavailableManifest, statistics, lifecycle,
            capabilities, reports, expectedBrokerNames, expectedConfiguration, [], [],
            new OutputLimits(options.MaxOutputBytes!.Value, options.MaxDiagnosticsPerCheck!.Value, options.MaxDiagnosticBytes!.Value));
        CheckResult[] checks = LateWriteChecks.Evaluate(request).Where(check => check.Assessment.CheckNumber is 15 or 16)
            .Concat(StatisticsChecks.Evaluate(request)).ToArray();
        return checks.All(check => check.Assessment.Evaluation == AssessmentEvaluation.Evaluated &&
                check.Assessment.Disposition == AssessmentDisposition.Pass) &&
            capabilities.Value is { GuardObservationsComplete: true } captured &&
            captured.GuardObservations.All(marker => marker is "broker_lifecycle_inventory_unproven" or "request_lifecycle_capture_unsupported") &&
            AbruptGuardConfiguration.Matches(captured, expectedConfiguration);
    }
}
