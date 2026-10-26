namespace Parchwell.Verifier.VerifyRun.SingleAction;

internal sealed record SingleActionOutcome(string Result,int? ErrorCode,bool? IsFatal,string? Origin);
internal sealed record SingleActionStatisticsReference(int Sequence,long NativeTime,long NativeTs,string RawSha256);
internal sealed record SingleActionRepeatProjection(int Repeat,string Status,string[] Reasons,SingleActionOutcome? OutcomeClass,int? BrokerCount,SingleActionStatisticsReference? StatisticsReference);
internal sealed record SingleActionItemAssessment(string? Verdict,Dictionary<string,string> Hypotheses,string? Reason);
internal sealed record SingleActionCandidate(int SchemaVersion,string Mode,bool Synthetic,string RunId,string ScenarioId,string ConfigurationId,string SourceCommit,string? ManagedVersion,string? NativeVersion,Dictionary<string,string> ImageDigests,string Status,SingleActionRepeatProjection[] Repeats,bool? Deterministic,SingleActionOutcome? OutcomeClass,string Adr012Counts,string[] Diagnostics,bool SecurityFinding,SingleActionItemAssessment? ItemAssessment);
