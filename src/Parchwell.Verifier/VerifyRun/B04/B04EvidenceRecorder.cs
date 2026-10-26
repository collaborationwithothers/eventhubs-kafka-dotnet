using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.A01;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;

namespace Parchwell.Verifier.VerifyRun.B04;

internal sealed record B04EvidenceRecording(B04RecordedEvidence? Evidence, IReadOnlyList<ResultDiagnostic> Diagnostics, WorkloadObservation? WorkloadObservation);

/// <summary>Records the approved B04 measurements without adding run-validity checks.</summary>
internal static class B04EvidenceRecorder
{
    internal const int MaxFatalErrors = 256;

    internal static B04EvidenceRecording Record(RunJudgementRequest request)
    {
        if (request.Identity.ScenarioId != "B04") return new(null, [], null);
        int[]? codes = request.Ledger.IsSuccess && request.Ledger.Value is not null
            ? request.Ledger.Value.Select(row => row.ErrorCode).Where(code => code != 0).Distinct().Order().ToArray()
            : null;
        if (codes is { Length: > 256 }) codes = null;
        B04FatalError[]? fatal = null;
        if (request.Lifecycle.IsSuccess && request.Lifecycle.Value is not null)
        {
            B04FatalError[] distinct = request.Lifecycle.Value.OfType<NativeError>().Where(error => error.IsFatal)
                .Select(error => new B04FatalError(error.ErrorCode, error.ErrorName)).Distinct()
                .OrderBy(error => error.Code).ThenBy(error => error.Name, StringComparer.Ordinal).ToArray();
            fatal = distinct.Length <= MaxFatalErrors && distinct.All(error => error.Name.Length <= 256) ? distinct : null;
        }
        WorkloadRecording workload = WorkloadRecorder.Record(request.Manifest, request.Ledger);
        return new(new(codes, fatal), workload.Diagnostics, workload.Observation);
    }
}
