using System.Globalization;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;

namespace Parchwell.Verifier.Judge;

internal sealed record B10OriginalEvidence(int Repeat, ArtifactReference? Result, ArtifactReference? VerifyReceipt,
    bool Eligible, DateTimeOffset? CompletedUtc);
internal sealed record B10RepeatFinality(bool SweepFailed, B10RepeatFinalization? Finalization = null);
internal sealed record B10SweepProjection(bool ScanComplete, ArtifactReference? Finalization,
    ArtifactReference? Receipt, ArtifactReference? SweepLine, string ResidualRisk);

/// <summary>Authenticates the phase sweep against the original repeat objects accepted by the judge.</summary>
internal static class B10SweepChecks
{
    internal static bool Accept(VerifierHostOptions options, B10Finalization finalization, WorkerReceipt receipt,
        ArtifactReference actualFinalization, B10SweepLine? note, ArtifactReference? actualNote,
        IReadOnlyList<B10OriginalEvidence> originals)
    {
        if (finalization.SchemaVersion != 1 || receipt.SchemaVersion != 1 || options.ScenarioId != "B10" || finalization.ScenarioId != "B10" ||
            finalization.ConfigurationId != options.ConfigurationId || finalization.RunId != options.RunId ||
            finalization.Phase != options.Phase || finalization.SourceCommit != options.SourceCommit ||
            finalization.ResidualRisk != B10SweepEvidence.ResidualRisk || !finalization.ScanComplete ||
            receipt.Mode != "b10-sweep" || receipt.ScenarioId != "B10" ||
            receipt.ConfigurationId != options.ConfigurationId || receipt.RunId != options.RunId ||
            receipt.Phase != options.Phase || receipt.SourceCommit != options.SourceCommit ||
            receipt.JobName != PhaseIdentity.B10SweepJobName(options.RunId!, options.Phase!) ||
            receipt.Repeat is not null || receipt.Identity is not null || !receipt.CleanupCompleted ||
            receipt.PublicationAcceptance is not null || receipt.RepeatChecks is not null || receipt.B10Intake is not null ||
            receipt.Artifacts.Count != 1 || receipt.Artifacts[0] != actualFinalization ||
            actualFinalization.Container != "verifier" || actualFinalization.Name != PhasePaths.B10Finalization(options.RunId!, options.Phase!) ||
            note is null || actualNote is null || finalization.SweepLine != actualNote ||
            actualNote.Container != "verifier" || actualNote.Name != PhasePaths.SweepLine(options.RunId!, options.Phase!) ||
            !DateTimeOffset.TryParse(receipt.StartedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset started) ||
            !B10SweepEvidenceCodec.MatchesSweepLine(note, options, started,
                originals.Where(x => x.CompletedUtc.HasValue).Select(x => x.CompletedUtc!.Value)) ||
            originals.Count != 5 || finalization.Repeats.Count != 5)
            return false;
        bool allValid = finalization.Repeats.All(x => x.Status == VerifierResultStatus.CompleteValid);
        if (receipt.HandlerExitCode != (allValid ? 0 : 1)) return false;
        for (int i = 0; i < 5; i++)
        {
            B10RepeatFinalization repeat = finalization.Repeats[i];
            B10OriginalEvidence original = originals[i];
            if (repeat.Repeat != i + 1 || original.Repeat != i + 1 ||
                repeat.Result != original.Result || repeat.VerifyReceipt != original.VerifyReceipt ||
                repeat.ClassificationFinal && (!original.Eligible || repeat.Status != VerifierResultStatus.CompleteValid || repeat.Counts is null) ||
                !repeat.ClassificationFinal && (repeat.Status == VerifierResultStatus.CompleteValid || repeat.Counts is not null))
                return false;
        }
        return true;
    }
}
