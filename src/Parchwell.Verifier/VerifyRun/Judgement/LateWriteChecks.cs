using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Judgement;

/// <summary>Rejects witnessed delivery, persistence and native-client uncertainty across the complete capture.</summary>
internal static class LateWriteChecks
{
    private static readonly HashSet<string> StandingMarkers = new(StringComparer.Ordinal)
    {
        "broker_lifecycle_inventory_unproven",
        "request_lifecycle_capture_unsupported"
    };

    internal static IReadOnlyList<CheckResult> Evaluate(RunJudgementRequest request)
    {
        var delivery = new List<ResultDiagnostic>();
        var persistence = new List<ResultDiagnostic>();
        if (request.Ledger.Value is { } ledger)
        {
            for (int i = 0; i < ledger.Count; i++)
            {
                LedgerRow row = ledger[i];
                if (!StringComparer.Ordinal.Equals(row.CompletionSource, "callback")) continue;
                if (!row.Delivered) delivery.Add(Failure(12, "ledger.jsonl", i + 1L, "delivered"));
                // PersistenceStatus 1 is PossiblyPersisted in the pinned v2.16.0 enum (blueprint S29).
                if (row.PersistenceStatus.Presence != FieldPresence.Value || row.PersistenceStatus.Value == 1)
                    persistence.Add(Failure(13, "ledger.jsonl", i + 1L, "persistence_status"));
            }
        }

        var markers = new List<ResultDiagnostic>();
        if (request.Capabilities.Value is { GuardObservationsComplete: false })
            markers.Add(Failure(14, "capabilities.json", null, "guard_observations_complete"));
        if (request.Manifest.Value is { } manifest)
            for (int i = 0; i < manifest.GuardObservations.Count; i++)
                if (!StandingMarkers.Contains(manifest.GuardObservations[i]))
                    markers.Add(Failure(14, "manifest.json", null, $"guard_observations[{i}]"));

        var counters = new List<ResultDiagnostic>();
        bool countersAvailable = request.Statistics.Value is not null && request.Reports.Count == request.Statistics.Value.Count;
        for (int i = 0; i < request.Reports.Count; i++)
        {
            NativeClientReport? native = request.Reports[i].Native.Value;
            if (native is null || native.Brokers.Presence != FieldPresence.Value || native.Brokers.Value is null)
            {
                countersAvailable = false;
                continue;
            }
            foreach (NativeBrokerReport broker in native.Brokers.Value.Values)
            {
                CheckCounter(broker.RequestTimeouts, "req_timeouts");
                CheckCounter(broker.TransmissionRetries, "txretries");
                CheckCounter(broker.TransmissionErrors, "txerrs");
                CheckCounter(broker.ReceiveErrors, "rxerrs");

                void CheckCounter(ParsedField<long> counter, string field)
                {
                    if (counter.Presence != FieldPresence.Value) countersAvailable = false;
                    else if (counter.Value != 0)
                        counters.Add(Failure(15, "statistics.jsonl", i + 1L, $"brokers.{broker.DictionaryKey}.{field}"));
                }
            }
        }

        var errors = new List<ResultDiagnostic>();
        if (request.Lifecycle.Value is { } lifecycle)
            for (int i = 0; i < lifecycle.Count; i++)
                if (lifecycle[i] is NativeError { IsError: true })
                    errors.Add(Failure(16, "request-lifecycle.jsonl", i + 1L, "is_error"));

        return
        [
            CheckResult.Create(12, delivery, request.Ledger.Value is not null),
            CheckResult.Create(13, persistence, request.Ledger.Value is not null),
            CheckResult.Create(14, markers, request.Manifest.Value is not null && request.Capabilities.Value is not null),
            CheckResult.Create(15, counters, countersAvailable),
            CheckResult.Create(16, errors, request.Lifecycle.Value is not null)
        ];
    }

    private static ResultDiagnostic Failure(int check, string artifact, long? row, string field) =>
        new(check, $"check_{check}_failed", artifact, row, field, null);
}
