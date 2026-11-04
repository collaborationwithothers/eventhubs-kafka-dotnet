using Confluent.Kafka;
using Parchwell.LoadGen.Run.Contracts;

namespace Parchwell.LoadGen.Run.Execution;

/// <summary>Which delivery reports the load generator answers with an application retry, and how many retries one scheduled event may receive.</summary>
/// <remarks>ADR-021 permits application retries only in B10 repeats, and only after a send that timed out. Every other scenario uses <see cref="None"/>.</remarks>
public sealed record ApplicationRetryPolicy(int MaxRetriesPerEvent)
{
    private const int MessageTimedOut = (int)ErrorCode.Local_MsgTimedOut;
    private const int NotPersisted = (int)PersistenceStatus.NotPersisted;
    private const int PossiblyPersisted = (int)PersistenceStatus.PossiblyPersisted;

    /// <summary>No application retry.</summary>
    public static ApplicationRetryPolicy None { get; } = new(0);

    /// <summary>Returns one retry per scheduled event for scenario B10, and no retry for any other scenario.</summary>
    public static ApplicationRetryPolicy ForScenario(string scenarioId) =>
        string.Equals(scenarioId, "B10", StringComparison.Ordinal) ? new ApplicationRetryPolicy(1) : None;

    /// <summary>True only for the callback row ADR-021 permits in a B10 repeat: not delivered, error -192, and persistence status NotPersisted or PossiblyPersisted.</summary>
    public static bool IsPermittedTimeout(LedgerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return string.Equals(row.CompletionSource, "callback", StringComparison.Ordinal) && !row.Delivered &&
            row.ErrorCode == MessageTimedOut && row.PersistenceStatus is NotPersisted or PossiblyPersisted;
    }
}
