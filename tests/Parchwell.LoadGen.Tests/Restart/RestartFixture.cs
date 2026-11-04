using System.Collections.Immutable;
using System.Globalization;
using Confluent.Kafka;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Tests.Restart;

// Synthetic B10 evidence for restart tests. Slot s = document * 5 + sequence is planned at s * 1000 microseconds,
// which is the due time a 1000-per-second stage gives event ordinal s.
internal static class RestartFixture
{
    internal static readonly string[] EventTypes = ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];
    internal static readonly RunIdentity First = new("B10", "baseline", "run-1", "pod-a", "pod-a", "pod-a");
    internal static readonly RunIdentity Second = new("B10", "baseline", "run-1", "pod-b", "pod-b", "pod-b");

    internal static AttemptEntry Attempt(RunIdentity identity, long ordinal, int document, int sequence)
    {
        string documentId = "d" + document.ToString(CultureInfo.InvariantCulture);
        long planned = (document * 5L + sequence) * 1000;
        return new AttemptEntry(identity.InstanceId, identity.InstanceId + "-" + ordinal.ToString(CultureInfo.InvariantCulture),
            ordinal, "tenant", documentId, "tenant-" + documentId, sequence, EventTypes[sequence], planned, planned + 10);
    }

    internal static LedgerRow Row(RunIdentity identity, AttemptEntry attempt, ErrorCode code, PersistenceStatus status) => new(
        1, identity, attempt, "callback", code == ErrorCode.NoError, (int)code, code.ToString(), (int)status,
        "native_callback", attempt.InvokedMicroseconds + 5, 1.0, null);

    internal static RunManifest Manifest(RunIdentity identity, ImmutableArray<AttemptEntry> inventory, long plannedCount,
        RestartManifest? restart = null) => new(1, identity, "1", plannedCount, inventory.Length, inventory, 1_000_000, 0,
        "complete", true, null, [], ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, string>.Empty, [],
        restart);

    // Six slots, d0 sequences 0 to 4 and d1 sequence 0. Attempts 3 and 5 retry slots 1 and 2.
    internal static (RunManifest Manifest, ImmutableArray<LedgerRow> Rows) Predecessor()
    {
        AttemptEntry[] inventory =
        [
            Attempt(First, 0, 0, 0), Attempt(First, 1, 0, 1), Attempt(First, 2, 0, 2), Attempt(First, 3, 0, 1),
            Attempt(First, 4, 0, 3), Attempt(First, 5, 0, 2), Attempt(First, 6, 0, 4), Attempt(First, 7, 1, 0)
        ];
        const ErrorCode Ok = ErrorCode.NoError, Timeout = ErrorCode.Local_MsgTimedOut;
        const PersistenceStatus Yes = PersistenceStatus.Persisted, No = PersistenceStatus.NotPersisted,
            Maybe = PersistenceStatus.PossiblyPersisted;
        (ErrorCode Code, PersistenceStatus Status)[] outcomes =
            [(Ok, Yes), (Timeout, Maybe), (Timeout, No), (Ok, Yes), (Timeout, No), (Timeout, Maybe),
             (ErrorCode.Local_Transport, No), (Timeout, Yes)];
        ImmutableArray<LedgerRow> rows = [.. inventory.Select((entry, index) => Row(First, entry, outcomes[index].Code, outcomes[index].Status))];
        return (Manifest(First, [.. inventory], 6), rows);
    }

    // The resend list Predecessor() selects: slot 2 through retry attempt pod-a-5, then slot 3 through pod-a-4.
    internal static RestartManifest Plan() => new("pod-a",
        [new RestartPlannedEntry(0, "pod-a-5", "tenant", "d0", "tenant-d0", 2, "ocr-finished", 2000),
         new RestartPlannedEntry(1, "pod-a-4", "tenant", "d0", "tenant-d0", 3, "classified", 3000)]);

    internal static AttemptEntry Resend(long ordinal, int sequence) => Attempt(Second, ordinal, 0, sequence);

    internal static RunManifest Restarted() => Manifest(Second, [Resend(0, 2), Resend(1, 3)], 2, Plan());

    internal static WorkloadProfile Profile(int attemptsPerSecond)
    {
        var payloads = EventTypes.ToImmutableDictionary(name => name,
            _ => ImmutableArray.Create(new PayloadBucket(4, 1)), StringComparer.Ordinal);
        return new WorkloadProfile(1, "restart-fixture", 7, [new TenantWeight("tenant", 1)], 1,
            payloads, [new LoadStage("one", attemptsPerSecond, 1)], new SourceLocation("synthetic-test-fixture"));
    }
}
