using Parchwell.Verifier.Common.Contracts;

namespace Parchwell.Verifier.Common.Phase;

/// <summary>Identifies the actual producer pod discovered by the verifier.</summary>
internal sealed record ProducerRegistration(
    int SchemaVersion,
    RunIdentity Identity,
    string Phase,
    int RepeatNumber,
    string JobName,
    string PodName,
    string PodUid,
    string SourceCommit);

/// <summary>The successful LoadGen final-action receipt shape.</summary>
internal sealed record ProducerCompletionReceipt(
    int SchemaVersion,
    RunIdentity Identity,
    string Phase,
    int RepeatNumber,
    string JobName,
    string PodName,
    string PodUid,
    string SourceCommit,
    string Mode,
    int HandlerExitCode,
    bool CleanupCompleted);

/// <summary>A fixed reference to one bounded object in a known container.</summary>
internal sealed record ArtifactReference(string Container, string Name, long Length, string Sha256);

/// <summary>Independent publication acceptance copied into the verifier receipt.</summary>
internal sealed record PublicationAcceptanceSnapshot(
    string ProducerPrefix,
    RunIdentity ProducerIdentity,
    string SourceCommit,
    ArtifactReference StartLine,
    ArtifactReference BrokerInventory,
    ArtifactReference ProducerReceipt,
    IReadOnlyList<ArtifactReference> Objects);

/// <summary>Evidence of one completed verifier worker action.</summary>
internal sealed record WorkerReceipt(
    int SchemaVersion,
    string Mode,
    string ScenarioId,
    string ConfigurationId,
    string RunId,
    string Phase,
    int? Repeat,
    string JobName,
    string PodName,
    RunIdentity? Identity,
    string SourceCommit,
    int HandlerExitCode,
    bool CleanupCompleted,
    string StartedUtc,
    string CompletedUtc,
    IReadOnlyList<ArtifactReference> Artifacts,
    PublicationAcceptanceSnapshot? PublicationAcceptance,
    RepeatChecks? RepeatChecks);

internal sealed record RepeatChecks(string ProducerJobBinding, string RegisteredProducerCount, string BrokerInventoryCoverage);
