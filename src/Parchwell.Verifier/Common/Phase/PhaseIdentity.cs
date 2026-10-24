using System.Text.RegularExpressions;

namespace Parchwell.Verifier.Common.Phase;

/// <summary>Validates the fixed C5 repeat identity and derives every repeat-scoped name.</summary>
internal static partial class PhaseIdentity
{
    private static readonly Regex RunPattern = RunIdPattern();

    internal static bool IsRunId(string? value) => value is not null && RunPattern.IsMatch(value);

    internal static string ValidatePhase(string phase) => phase is "probe" or "C0" or "C3"
        ? phase
        : throw new ArgumentException("Phase must be exactly probe, C0, or C3.", nameof(phase));

    internal static string ConfigurationFor(string phase) => ValidatePhase(phase) == "C3" ? "C3" : "C0";

    internal static int ValidateRepeat(int repeat) => repeat is >= 1 and <= 5
        ? repeat
        : throw new ArgumentOutOfRangeException(nameof(repeat), "Repeat must be between 1 and 5.");

    internal static string JobName(string runId, string phase, int repeat)
    {
        ValidateRunId(runId);
        ValidatePhase(phase);
        ValidateRepeat(repeat);
        return $"{runId}-{phase.ToLowerInvariant()}-r{repeat}-loadgen";
    }

    internal static string StartLineJobName(string runId, string phase, int repeat) =>
        $"{runId}-{ValidatePhase(phase).ToLowerInvariant()}-r{ValidateRepeat(repeat)}-start-line";

    internal static string VerifyJobName(string runId, string phase, int repeat) =>
        $"{runId}-{ValidatePhase(phase).ToLowerInvariant()}-r{ValidateRepeat(repeat)}-verify";

    internal static string JudgeJobName(string runId, string phase)
    {
        ValidateRunId(runId);
        return $"{runId}-{ValidatePhase(phase).ToLowerInvariant()}-judge";
    }

    internal static string PhasePrefix(string runId, string phase)
    {
        ValidateRunId(runId);
        return $"{runId}/{ValidatePhase(phase)}/";
    }

    internal static string RepeatPrefix(string runId, string phase, int repeat) =>
        $"{PhasePrefix(runId, phase)}r{ValidateRepeat(repeat)}/";

    internal static void ValidateRunId(string value)
    {
        if (!IsRunId(value)) throw new ArgumentException("Run ID is not a DNS-safe identifier.", nameof(value));
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,27}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex RunIdPattern();
}
