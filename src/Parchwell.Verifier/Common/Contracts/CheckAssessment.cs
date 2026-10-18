namespace Parchwell.Verifier.Common.Contracts;

/// <summary>Records one numbered check's supplied assessment and bounded evidence reference.</summary>
/// <remarks>This record carries data only; it does not combine assessments into a run status.</remarks>
internal sealed record CheckAssessment(
    int CheckNumber,
    AssessmentEvaluation Evaluation,
    AssessmentDisposition? Disposition,
    AssessmentClassification? Classification,
    string? Artifact,
    long? Row,
    string? Field,
    string? Code,
    string? Snippet);

/// <summary>States whether a numbered check was evaluated.</summary>
internal enum AssessmentEvaluation
{
    NotEvaluated,
    Evaluated
}

/// <summary>States the supplied result of an evaluated check.</summary>
internal enum AssessmentDisposition
{
    Pass,
    Fail
}

/// <summary>Classifies a supplied failure without aggregating it with other checks.</summary>
internal enum AssessmentClassification
{
    Invalid,
    Incomplete
}
