namespace Parchwell.Verifier.VerifyRun.SingleAction;

internal sealed record ProbeV2Semantic(int? DeliveredPartition, int? ConsumedPartition, string? KeyBase64, string? ValueKind, bool? ValueMatch);
internal sealed record ProbeV2Reference(string RunId, string ScenarioId, string ConfigurationId, string Stage, string SourceCommit, string JobName, string PodUid, string RawSha256);
internal sealed record ProbeV2StatisticsReference(long Sequence, long NativeTime, long NativeTs, string RawSha256);
internal sealed record ProbeV2Client(string ClientId, string Stage, int? BrokerCount, ProbeV2StatisticsReference? StatisticsReference);
internal sealed record ProbeV2RepeatProjection(int Repeat, string Status, string[] Reasons, SingleActionOutcome? OutcomeClass,
    int? BrokerCount, ProbeV2StatisticsReference? StatisticsReference, ProbeV2Semantic SemanticOutcome,
    ProbeV2Delivery? Delivery, ProbeV2ReadObservation? ReadObservation, ProbeV2Reference? SeedReference,
    ProbeV2Reference[] ControlReferences, ProbeV2Client[] Clients);
internal sealed record ProbeV2Candidate(int SchemaVersion, string Mode, bool Synthetic, string RunId, string ScenarioId,
    string ConfigurationId, string SourceCommit, string? ManagedVersion, string? NativeVersion,
    Dictionary<string,string> ImageDigests, string Status, ProbeV2RepeatProjection[] Repeats, bool? Deterministic,
    SingleActionOutcome? OutcomeClass, string Adr012Counts, string[] Diagnostics, bool SecurityFinding, SingleActionItemAssessment? ItemAssessment);
internal sealed record ProbeV2JudgeReceipt(int SchemaVersion, string Mode, string RunId, string ScenarioId,
    string ConfigurationId, string SourceCommit, string JobName, string PodName, string PodUid, string CandidateName,
    string CandidateSha256, int HandlerExitCode, bool CleanupCompleted);
internal sealed record ProbeV2Evidence(string Configuration, string Stage, ProbeV2Registration? Registration,
    ProbeV2CaptureFile? Capture, ProbeV2Pod? Pod, string? CaptureSha256, string[] Problems);
internal sealed record ProbeV2Scope(int SchemaVersion, string RunId, string SourceCommit, string NamespaceName,
    ProbeV2Deployed Deployed, ProbeV2Effective? EffectiveScope, string? EffectiveScopeSha256);
internal sealed record ProbeV2Deployed(string NamespaceId, Dictionary<string,string> Topics,
    Dictionary<string,ProbeV2Account> Accounts, Dictionary<string,ProbeV2Assignment> Assignments, bool ProbeNamespaceDataRoles);
internal sealed record ProbeV2Account(string ClientId, string PrincipalId);
internal sealed record ProbeV2Assignment(string ServiceAccount, string RoleDefinitionName, string Scope, string PrincipalId);
internal sealed record ProbeV2Effective(int SchemaVersion, string SourceCommit, string NamespaceId,
    Dictionary<string,ProbeV2Confirmation> Accounts);
internal sealed record ProbeV2Confirmation(string PrincipalId, bool NoAdditionalDataAccess);
