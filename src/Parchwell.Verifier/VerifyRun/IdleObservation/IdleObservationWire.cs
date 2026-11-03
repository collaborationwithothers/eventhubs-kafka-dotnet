using Parchwell.Verifier.Common.SingleAction;

namespace Parchwell.Verifier.VerifyRun.IdleObservation;

internal sealed record IdleError(int Code, bool IsFatal, string Origin, string Reason);
internal sealed record IdleProduce(int Attempt, string Key, long StartedElapsedMs, bool Terminal, string? Result, IdleError? Error, long? TerminalElapsedMs, long? TerminalUnixMs);
internal sealed record IdleRepeat(int Repeat, string ClientId, Dictionary<string, string> Settings, string[] OmittedKeys, long ClientStartedUnixMs,
    IdleProduce? First, long? IdleStartedElapsedMs, long? IdleEndedElapsedMs, IdleProduce? Second, SingleActionClientError[] ClientErrors,
    long ClientErrorsDropped, SingleActionStatistic[] Statistics, string[] Diagnostics);
internal sealed record IdleCapture(int SchemaVersion, string Mode, bool Synthetic, string RunKind, SingleActionIdentity Identity, string JobName,
    string PodName, string PodUid, string SourceCommit, string ManagedVersion, string NativeVersion, string Topic, int Partition, int PayloadBytes,
    int IdleSeconds, int StatisticsIntervalMs, int MaxStatisticsReports, IdleRepeat[] Repeats);
internal sealed record IdleBroker(long NodeId, string Source, string State, long TxIdleUs, long RxIdleUs, long Connects, long Disconnects,
    long ReqTimeouts, long Metadata);
internal sealed record IdleReport(int Sequence, long ObservedElapsedMs, Dictionary<string, IdleBroker> Brokers);
internal sealed record IdleWindows(long IdleTimedDisconnects, long IdleOtherCauseDisconnects, long IdleConnects, long IdleMetadataRequests,
    long IdleRequestTimeouts, long? FirstCloseAfterIdleMs, long? FirstCloseQuietMs, long ResumeIdleTimedDisconnects, long ResumeOtherCauseDisconnects,
    long ResumeConnects, long ResumeRequestTimeouts, bool ResumeTimeoutReconnect);
internal sealed record IdleOutcome(string Result, int? ErrorCode, bool? IsFatal, string? Origin, long LatencyMs);
internal sealed record IdleClientErrorRow(int Code, bool IsFatal, string Origin, long ObservedElapsedMs);
internal sealed record IdleRepeatResult(int Repeat, string? ClientId, string Status, string[] Reasons, string? ConnectionClass, IdleOutcome? First,
    IdleOutcome? Second, IdleWindows? Windows, int MaximumUpBrokers, IdleReport[] Reports, IdleClientErrorRow[] ClientErrors, long ClientErrorsDropped);
internal sealed record IdleConfigurationResult(string ConfigurationId, string Status, string? Summary, string? CaptureSha256, IdleRepeatResult[] Repeats);
internal sealed record IdleItemAssessment(string? Verdict, string Reason, Dictionary<string, string> Hypotheses);
internal sealed record IdleObservationCandidate(int SchemaVersion, string Mode, bool Synthetic, string RunKind, string RunId, string ScenarioId,
    string SourceCommit, string? ManagedVersion, string? NativeVersion, Dictionary<string, string> ImageDigests,
    IdleConfigurationResult[] Configurations, IdleItemAssessment ItemAssessment, string Adr012Counts, string[] Notes);
internal sealed record IdleCandidateReference(string ScenarioId, string CandidateName, string CandidateSha256, string Status, string Reason);
internal sealed record IdleObservationJudgeReceipt(int SchemaVersion, string Mode, string RunId, string SourceCommit, string JobName,
    string PodName, string PodUid, IdleCandidateReference[] Candidates, int HandlerExitCode, bool CleanupCompleted);

internal static class IdleObservationPaths
{
    internal static string Capture(string run, string item, string configuration) => $"{run}/idle-observation/{item}/{configuration}/";
    internal static string Candidate(string run, string item) => $"{run}/idle-observation/{item}/judge/candidate.json";
    internal static string Receipt(string run) => $"{run}/idle-observation/judge-completion.json";
    internal static string Job(string run) => $"{run}-idle-judge";
}
