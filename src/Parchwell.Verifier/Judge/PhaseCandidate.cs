using Parchwell.Verifier.Common.Results;

namespace Parchwell.Verifier.Judge;

/// <summary>Privacy-bounded result of evaluating one fixed five-repeat phase.</summary>
internal sealed record PhaseCandidate(int SchemaVersion, bool Synthetic, string ScenarioId, string ConfigurationId,
    string RunId, string Phase, string SourceCommit, string Status, string? Gate,
    IReadOnlyDictionary<string,string?> Versions, IReadOnlyDictionary<string,string?> ImageDigests,
    IReadOnlyDictionary<string,object?> Timings, IReadOnlyDictionary<string,object?> Diagnostics,
    IReadOnlyList<PhaseRepeatResult> Repeats, OutcomeCounts? Counts, IReadOnlyList<string> Reasons);
