namespace Parchwell.Verifier.Common.Consumer;

internal sealed record ConsumerRegistration(string RunId, string ScenarioId, string ConfigurationId, string Phase,
    int RepeatNumber, string Role, string InstanceId, string JobName, string PodName, string PodUid, string NodeName,
    string SourceCommit, string GroupId, IReadOnlyDictionary<string, string> EffectiveSettings, DateTimeOffset RegisteredUtc);
internal sealed record ConsumerCompletion(string RunId, string ScenarioId, string ConfigurationId, string Phase,
    int RepeatNumber, string Role, string InstanceId, string JobName, string PodUid, string SourceCommit,
    long Rows, int RecordBlobs, long MessagesProcessed, DateTimeOffset ClosedUtc);
internal sealed record ConsumerCommitOffset(string Topic, int Partition, long Offset, int ErrorCode = 0);
internal sealed record ConsumerRow(string Kind, string InstanceId, long RowSequence, DateTimeOffset Time,
    int Partition = 0, long Offset = 0, string? ProducerInstanceId = null, string? AttemptId = null,
    string? Key = null, long? Sequence = null, bool Slow = false, DateTimeOffset? ProcessingStartedUtc = null,
    string? Source = null, string? Outcome = null, int ErrorCode = 0, string? ErrorReason = null,
    IReadOnlyList<ConsumerCommitOffset>? Submitted = null, IReadOnlyList<ConsumerCommitOffset>? Offsets = null,
    string? Event = null, IReadOnlyList<int>? Partitions = null);
internal sealed record ConsumerStartSnapshot(string GroupId, DateTimeOffset FirstReadStartedUtc,
    DateTimeOffset SecondReadEndedUtc, IReadOnlyList<ConsumerSnapshotMember> Members);
internal sealed record ConsumerSnapshotMember(string InstanceId, string Role, long FirstReadLastRowSequence, long SecondReadLastRowSequence);
internal sealed record ConsumerDiagnostic(string Reason, string? Member = null, string? Artifact = null,
    long? RowSequence = null, string? Field = null, string? Detail = null);
internal sealed record ConsumerMember(string Prefix, ConsumerRegistration? Registration, IReadOnlyList<ConsumerRow> Rows,
    ConsumerCompletion? Completion, int RecordBlobs, bool RegistrationInvalid, bool RecordInvalid);
internal sealed record ConsumerArtifacts(IReadOnlyList<ConsumerMember> Members, int RegistrationCount,
    bool Unavailable, IReadOnlyList<ConsumerDiagnostic> Diagnostics);
