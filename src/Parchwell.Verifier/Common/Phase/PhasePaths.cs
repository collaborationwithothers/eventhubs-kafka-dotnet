namespace Parchwell.Verifier.Common.Phase;

/// <summary>Builds fixed C5 blob object names from validated run coordinates.</summary>
internal static class PhasePaths
{
    internal static string StartLine(string run, string phase, int repeat) => PhaseIdentity.RepeatPrefix(run, phase, repeat) + "start-line.json";
    internal static string BrokerInventory(string run, string phase, int repeat) => PhaseIdentity.RepeatPrefix(run, phase, repeat) + "broker-inventory.json";
    internal static string StartLineCompletion(string run, string phase, int repeat) => PhaseIdentity.RepeatPrefix(run, phase, repeat) + "start-line-completion.json";
    internal static string Registration(string run, string phase, int repeat, string instance) => ProducerPrefix(run, phase, repeat, instance) + "registration.json";
    internal static string ProducersPrefix(string run, string phase, int repeat) => PhaseIdentity.RepeatPrefix(run, phase, repeat) + "producer/";
    internal static string ProducerPrefix(string run, string phase, int repeat, string instance) => PhaseIdentity.RepeatPrefix(run, phase, repeat) + "producer/" + ValidateSegment(instance) + "/";
    internal static string Publication(string run, string phase, int repeat, string instance, string name) => ProducerPrefix(run, phase, repeat, instance) + "publication/" + ValidatePublicationName(name);
    internal static string ProducerCompletion(string run, string phase, int repeat, string instance) => ProducerPrefix(run, phase, repeat, instance) + "completion.json";
    internal static string Diagnostics(string run, string phase, int repeat, string instance, string name) => ProducerPrefix(run, phase, repeat, instance) + "diagnostics/" + ValidateDiagnosticName(name);
    internal static string Result(string run, string phase, int repeat) => PhaseIdentity.RepeatPrefix(run, phase, repeat) + "result.json";
    internal static string VerifyCompletion(string run, string phase, int repeat) => PhaseIdentity.RepeatPrefix(run, phase, repeat) + "verify-completion.json";
    internal static string Candidate(string run, string phase) => PhaseIdentity.PhasePrefix(run, phase) + "candidate.json";
    internal static string JudgeCompletion(string run, string phase) => PhaseIdentity.PhasePrefix(run, phase) + "judge-completion.json";

    internal static string SweepLine(string run, string phase) => PhaseIdentity.PhasePrefix(run, phase) + "sweep-line.json";
    internal static string B10Finalization(string run, string phase) => PhaseIdentity.PhasePrefix(run, phase) + "b10-finalization.json";
    internal static string B10SweepLineCompletion(string run, string phase) => PhaseIdentity.PhasePrefix(run, phase) + "b10-sweep-line-completion.json";
    internal static string B10SweepCompletion(string run, string phase) => PhaseIdentity.PhasePrefix(run, phase) + "b10-sweep-completion.json";

    internal static string B10FinalSweep(string run, string phase) => PhaseIdentity.PhasePrefix(run, phase) + "b10-final-sweep.json";
    internal static string B10FinalSweepCompletion(string run, string phase) => PhaseIdentity.PhasePrefix(run, phase) + "b10-final-sweep-completion.json";

    internal static string ConsumedCaptureId(string mode, string job, string podUid, long windowIndex)
    {
        if (mode is not ("verify" or "b10-abrupt-verify" or "b10-sweep" or "b10-final-sweep") || windowIndex < 0)
            throw new ArgumentException("Invalid consumed capture identity.");
        string id = $"{mode}-{ValidateSegment(job)}-{ValidateSegment(podUid)}-w{windowIndex:D4}";
        if (id.Length > 1024) throw new ArgumentException("Consumed capture identity exceeds its bound.");
        return id;
    }

    internal static string ConsumedCapturePrefix(string run, string phase, int? repeat, string captureId)
    {
        if (string.IsNullOrEmpty(captureId) || captureId.Length > 1024 ||
            captureId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new ArgumentException("Invalid consumed capture identifier.", nameof(captureId));
        return (repeat is null ? PhaseIdentity.PhasePrefix(run, phase) : PhaseIdentity.RepeatPrefix(run, phase, repeat.Value))
            + "consumed-metadata/" + captureId + "/";
    }

    private static string ValidateSegment(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128 || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new ArgumentException("Instance identifier must be one bounded ASCII path segment.", nameof(value));
        return value;
    }

    private static string ValidatePublicationName(string value) => value is "ledger.jsonl" or "manifest.json" or "statistics.jsonl" or "request-lifecycle.jsonl" or "capabilities.json" or "index.json"
        ? value : throw new ArgumentException("Publication name is not a fixed C5 object.", nameof(value));

    private static string ValidateDiagnosticName(string value) => value is "ledger.jsonl" or "manifest.json" or "statistics.jsonl" or "request-lifecycle.jsonl" or "capabilities.json"
        ? value : throw new ArgumentException("Diagnostic name is not a fixed C5 object.", nameof(value));
}
