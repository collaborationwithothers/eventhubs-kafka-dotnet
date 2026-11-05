using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Window;

namespace Parchwell.Verifier.Common.Phase;

internal static class B10SweepEvidence
{
    internal const string ResidualRisk = "A late append after the phase sweep line is not detected.";
}

/// <summary>Workflow-observed phase bounds and lifecycle times for the B10 sweep.</summary>
internal sealed record B10SweepLine(int SchemaVersion, string ScenarioId, string ConfigurationId, string RunId,
    string Phase, string SourceCommit, string Topic, IReadOnlyList<PartitionHigh> PartitionHighs,
    DateTimeOffset CapturedAt, DateTimeOffset LastVerifierCompletedUtc, DateTimeOffset LastLoadgenTerminatedUtc,
    long SettleIntervalMilliseconds);

/// <summary>Immutable sweep output; authenticating its references belongs to the reader.</summary>
internal sealed record B10Finalization(int SchemaVersion, string ScenarioId, string ConfigurationId, string RunId,
    string Phase, string SourceCommit, ArtifactReference? SweepLine, bool ScanComplete, string ResidualRisk,
    IReadOnlyList<B10RepeatFinalization> Repeats);

internal sealed record B10RepeatFinalization(int Repeat, ArtifactReference? Result, ArtifactReference? VerifyReceipt,
    bool ClassificationFinal, VerifierResultStatus Status, OutcomeCounts? Counts, IReadOnlyList<string> Reasons);
