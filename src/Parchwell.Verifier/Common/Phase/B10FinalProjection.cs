using Parchwell.Verifier.Common.Results;

namespace Parchwell.Verifier.Common.Phase;

/// <summary>The final, bounded classification of five scientific B10 repeats.</summary>
internal sealed record B10FinalProjection(B10FinalIdentity Identity, B10FinalData Data);
internal sealed record B10FinalIdentity(string RunId, string ScenarioId, string ConfigurationId, string Phase,
    string SourceCommit, string ImageReference, string JobName, string PodName, string PodUid);
internal sealed record B10FinalData(ArtifactReference? SweepLine, bool ScanComplete, string ResidualRisk,
    IReadOnlyList<B10FinalRepeat> Repeats);
internal sealed record B10FinalRepeat(int Repeat, ArtifactReference? Source,
    IReadOnlyList<B10FinalProducerVersion> ProducerVersions, bool ClassificationFinal, string Status,
    OutcomeCounts? Timeout, IReadOnlyList<string> Reasons, B10FinalRestart? Restart);
internal sealed record B10FinalProducerVersion(string InstanceId, string? WorkloadGenerator,
    string? ConfluentKafka, string? Librdkafka, string? Dotnet);
internal sealed record B10FinalRestart(string Eligibility, IReadOnlyList<string> EligibilityReasons,
    B10FinalTermination Termination, string? FirstInstanceId, string? SuccessorInstanceId, string? TimeoutInstanceId,
    string Comparison, B10FinalNativeErrors? NativeErrors, IReadOnlyList<B10FinalArm>? Arms);
internal sealed record B10FinalTermination(string? Class, string? Outcome);
internal sealed record B10FinalNativeErrors(long Count, long FatalCount, string Tail);
internal sealed record B10FinalArm(string Arm, string? CutAttemptId, IReadOnlyList<string> UnconfirmedAttemptIds,
    long? UnconfirmedCount, long? ResendInvoked, long? OriginalsSeen, long? ResendsSeen, long? PairCount,
    long? UnreportedAttempts, long? UnreportedSeen, long? UnreportedUnseen, long? LostResends, B10FinalWitness? Witness);
internal sealed record B10FinalWitness(string OriginalAttemptId, string ResendAttemptId,
    B10FinalCoordinate Original, B10FinalCoordinate Resend);
internal sealed record B10FinalCoordinate(int Partition, long Offset);
