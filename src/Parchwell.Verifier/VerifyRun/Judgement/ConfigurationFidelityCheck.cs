using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using CapturedSetting = Parchwell.Verifier.VerifyRun.Input.ResolvedSetting;
using SelectedSetting = Parchwell.Scenarios.ResolvedSetting;

namespace Parchwell.Verifier.VerifyRun.Judgement;

/// <summary>Compares effective settings and the approved override attribution subset.</summary>
internal static class ConfigurationFidelityCheck
{
    internal static ConfigurationFidelityResult Evaluate(RunJudgementRequest request)
    {
        if (!request.Capabilities.IsSuccess)
            return new(CheckResult.NotEvaluated(27).Assessment, []);
        var captured = request.Capabilities.Value!.ResolvedConfiguration;
        if (captured.Presence != FieldPresence.Value)
            return new(Failure("resolved_configuration"), []);

        IReadOnlyDictionary<string, CapturedSetting> actual = captured.Value!.Settings;
        var expected = request.ResolvedProducerConfiguration.Settings;
        var differences = new List<SettingDifference>();
        bool failed = false;
        string? firstField = null;
        foreach (string key in actual.Keys.Union(expected.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            bool hasExpected = expected.TryGetValue(key, out SelectedSetting? selected);
            bool hasActual = actual.TryGetValue(key, out CapturedSetting? observed);
            string field = $"resolved_configuration.settings.{key}";
            bool different = !hasExpected || !hasActual;
            if (!different && !string.Equals(selected!.Value, observed!.Value, StringComparison.Ordinal))
            {
                different = true;
                field += ".value";
            }
            if (!different)
            {
                bool selectedOverride = selected!.Override is not null;
                bool observedOverride = observed!.Override.Presence == FieldPresence.Value;
                if (selectedOverride != observedOverride)
                {
                    different = true;
                    field += ".override";
                }
                else if (selectedOverride && observedOverride)
                {
                    if (!string.Equals(selected.Override!.ScenarioId, observed.Override.Value!.ScenarioId, StringComparison.Ordinal))
                    {
                        different = true;
                        field += ".override.scenario_id";
                    }
                    else if (!string.Equals(selected.Override.ConfigurationId, observed.Override.Value.ConfigurationId, StringComparison.Ordinal))
                    {
                        different = true;
                        field += ".override.configuration_id";
                    }
                }
            }
            if (!different) continue;
            failed = true;
            firstField ??= field;
            if (differences.Count < request.Limits.MaxDiagnosticsPerCheck)
                differences.Add(new(key, selected?.Value, observed?.Value));
        }
        return new(failed ? Failure(firstField!) : CheckResult.Pass(27).Assessment, differences);
    }

    private static CheckAssessment Failure(string field) => new(27, AssessmentEvaluation.Evaluated,
        AssessmentDisposition.Fail, AssessmentClassification.Invalid, "capabilities.json", null, field,
        "check_27_failed", null);
}
