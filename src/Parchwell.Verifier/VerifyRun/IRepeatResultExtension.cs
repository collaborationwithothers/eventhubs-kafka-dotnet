using System.Text.Json;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;

namespace Parchwell.Verifier.VerifyRun;

internal interface IRepeatResultExtension
{
    int CompletionDeadlineMs { get; }
    Task<RepeatResultExtension> EvaluateAsync(RepeatResultContext context, CancellationToken cancellationToken);
}

internal sealed record RepeatResultContext(VerifierResultStatus RootStatus, RunWindow Window,
    IReadOnlyList<LedgerRow> LedgerRows, IReadOnlyList<ConsumedRecord> ConsumedRecords,
    DateTimeOffset DeadlineInstant, InputBudget InputBudget);
internal sealed record RepeatResultExtension(VerifierResultStatus Status, JsonElement Consumer);
