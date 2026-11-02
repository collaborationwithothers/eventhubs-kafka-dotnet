using Parchwell.LoadGen.Common.SingleAction;

namespace Parchwell.LoadGen.IdleObservation;

internal sealed record IdleObservationProduce(int Attempt, string Key, long StartedElapsedMs, bool Terminal, string? Result,
    SingleActionError? Error, long? TerminalElapsedMs, long? TerminalUnixMs);
internal sealed record IdleObservationRepeat(int Repeat, string ClientId, IReadOnlyDictionary<string, string> Settings,
    IReadOnlyList<string> OmittedKeys, long ClientStartedUnixMs, IdleObservationProduce? First, long? IdleStartedElapsedMs,
    long? IdleEndedElapsedMs, IdleObservationProduce? Second, IReadOnlyList<SingleActionClientError> ClientErrors,
    long ClientErrorsDropped, IReadOnlyList<SingleActionStatistics> Statistics, IReadOnlyList<string> Diagnostics);
internal sealed record IdleObservationCaptureFile(int SchemaVersion, string Mode, bool Synthetic, string RunKind,
    SingleActionIdentity Identity, string JobName, string PodName, string PodUid, string SourceCommit, string ManagedVersion,
    string NativeVersion, string Topic, int Partition, int PayloadBytes, int IdleSeconds, int StatisticsIntervalMs,
    int MaxStatisticsReports, IReadOnlyList<IdleObservationRepeat> Repeats);
