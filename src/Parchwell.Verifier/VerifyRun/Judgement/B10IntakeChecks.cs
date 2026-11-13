using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.VerifyRun.Judgement;

/// <summary>Binds the two B10 producers and retains their nonfinal intake judgements.</summary>
internal static class B10IntakeChecks
{
    internal static IReadOnlyList<ProducerRegistration> BindRegistrations(
        IReadOnlyList<(ProducerRegistration Registration, string PathInstance)> registrations,
        string scenarioId, string configurationId, string runId, string phase, int repeatNumber,
        string sourceCommit, IReadOnlyList<string> expectedJobNames)
    {
        bool oneProducer = scenarioId == "B10" && configurationId == "C0" && VerifierHostOptions.IsB10ScientificRunId(runId);
        int expectedCount = oneProducer ? 1 : 2;
        if (registrations.Count != expectedCount) throw new InvalidOperationException("b10_registration_count_invalid");
        if (!oneProducer && registrations[0].Registration.Identity.InstanceId == registrations[1].Registration.Identity.InstanceId)
            throw new InvalidOperationException("b10_registration_identity_duplicate");
        if (expectedJobNames.Count != expectedCount ||
            expectedJobNames.Distinct(StringComparer.Ordinal).Count() != expectedCount ||
            oneProducer && expectedJobNames[0] != PhaseIdentity.JobName(runId, phase, repeatNumber) ||
            expectedJobNames.Any(job => registrations.Count(item => item.Registration.JobName == job) != 1))
            throw new InvalidOperationException("b10_registration_job_binding_invalid");

        var ordered = expectedJobNames.Select(job => registrations.Single(item => item.Registration.JobName == job)).ToArray();
        foreach ((ProducerRegistration registration, string pathInstance) in ordered)
        {
            RunIdentity identity = registration.Identity;
            if (identity.InstanceId != pathInstance || identity.ExpectedInstanceId != identity.InstanceId ||
                registration.PodName != identity.InstanceId || identity.ScenarioId != scenarioId ||
                identity.ConfigurationId != configurationId || identity.RunId != runId ||
                registration.Phase != phase || registration.RepeatNumber != repeatNumber ||
                registration.SourceCommit != sourceCommit)
                throw new InvalidOperationException("b10_registration_binding_invalid");
        }
        if (ordered[0].Registration.PredecessorInstanceId is not null ||
            !oneProducer && ordered[1].Registration.PredecessorInstanceId != ordered[0].Registration.Identity.InstanceId)
            throw new InvalidOperationException("b10_registration_predecessor_invalid");
        return ordered.Select(item => item.Registration).ToArray();
    }

    internal static RunJudgementResult Aggregate(RunJudgementResult first, RunJudgementResult second,
        IReadOnlyList<string> orderedInstanceIds, OutputLimits limits)
    {
        int[] failed = first.FailedChecks.Concat(second.FailedChecks).Distinct().Order().ToArray();
        CheckAssessment[] assessments = Enumerable.Range(1, 27)
            .Select(check => AggregateAssessment(check, first, second)).ToArray();
        var retained = new List<ResultDiagnostic> { new(0, "b10_intake_only", null, null, null, null) };
        // The marker and embedded first-producer fidelity each consume an existing slot.
        var countsByCheck = new Dictionary<int, int> { [0] = 1, [27] = 1 };
        Retain(first, orderedInstanceIds[0], includeWindow: true);
        Retain(second, orderedInstanceIds[1], includeWindow: false);
        if (second.ConfigurationFidelity.Assessment is { Disposition: AssessmentDisposition.Fail } fidelity)
            Add(new(27, fidelity.Code ?? "check_27_failed", fidelity.Artifact, fidelity.Row, fidelity.Field, fidelity.Snippet),
                orderedInstanceIds[1]);

        return new(first.Status == VerifierResultStatus.Invalid || second.Status == VerifierResultStatus.Invalid
                ? VerifierResultStatus.Invalid : VerifierResultStatus.Incomplete,
            failed, assessments, null, first.BrokerCount, first.ConfigurationFidelity, retained);

        void Retain(RunJudgementResult judgement, string instanceId, bool includeWindow)
        {
            foreach (ResultDiagnostic diagnostic in judgement.Diagnostics)
            {
                bool window = diagnostic.Check is >= 7 and <= 11;
                if (window && !includeWindow) continue;
                Add(diagnostic, window ? null : instanceId);
            }
        }

        void Add(ResultDiagnostic diagnostic, string? instanceId)
        {
            int count = countsByCheck.GetValueOrDefault(diagnostic.Check);
            if (count >= limits.MaxDiagnosticsPerCheck) return;
            retained.Add(diagnostic with
            {
                Artifact = instanceId is null ? diagnostic.Artifact : diagnostic.Artifact is null
                    ? $"producer/{instanceId}" : $"producer/{instanceId}/{diagnostic.Artifact}",
                Snippet = RunJudgement.TruncateSnippet(diagnostic.Snippet, limits.MaxDiagnosticBytes)
            });
            countsByCheck[diagnostic.Check] = count + 1;
        }
    }

    internal static string BrokerInventoryCoverage(RunJudgementResult first, RunJudgementResult second)
    {
        CheckAssessment? firstCoverage = first.Assessments.FirstOrDefault(a => a.CheckNumber == 24);
        CheckAssessment? secondCoverage = second.Assessments.FirstOrDefault(a => a.CheckNumber == 24);
        if (firstCoverage?.Disposition == AssessmentDisposition.Fail || secondCoverage?.Disposition == AssessmentDisposition.Fail)
            return "fail";
        return firstCoverage is { Evaluation: AssessmentEvaluation.Evaluated, Disposition: AssessmentDisposition.Pass } &&
            secondCoverage is { Evaluation: AssessmentEvaluation.Evaluated, Disposition: AssessmentDisposition.Pass }
                ? "pass" : "unavailable";
    }

    private static CheckAssessment AggregateAssessment(int check, RunJudgementResult first, RunJudgementResult second)
    {
        CheckAssessment[] candidates = [first.Assessments.FirstOrDefault(a => a.CheckNumber == check)
            ?? CheckResult.NotEvaluated(check).Assessment,
            second.Assessments.FirstOrDefault(a => a.CheckNumber == check)
            ?? CheckResult.NotEvaluated(check).Assessment];
        return candidates.FirstOrDefault(a => a.Disposition == AssessmentDisposition.Fail && a.Classification == AssessmentClassification.Invalid)
            ?? candidates.FirstOrDefault(a => a.Disposition == AssessmentDisposition.Fail && a.Classification == AssessmentClassification.Incomplete)
            ?? candidates.FirstOrDefault(a => a.Evaluation == AssessmentEvaluation.NotEvaluated)
            ?? candidates.FirstOrDefault(a => a.Disposition == AssessmentDisposition.Pass)
            ?? CheckResult.NotEvaluated(check).Assessment;
    }
}
