# Load generator evidence contract

The load generator executes a finite synthetic offered schedule and preserves independent attempt evidence. Its inventory is registered before each application invocation, while the ledger records terminal observations separately. This contract implements the [agreed design][design] and [implementation handoff][handoff]; it does not establish A01 execution readiness.

## Delivery stages

The first stage supplies workload generation, an independent inventory, local ledger persistence and strict read-back reconciliation. Worker/client capture and evidence integration follow in the second stage. Blob publication and final process completion follow in the third stage. No stage implements verifier classification or changes scenario expectations.

## Workload version 1

The workload profile is the existing validated `WorkloadProfile` from `Parchwell.Scenarios`. Stage rates count application invocation opportunities. A tenant is selected once per burst; documents emit `uploaded`, `ocr-started`, `ocr-finished`, `classified`, `indexed` consecutively. A finite schedule may end partway through a burst or document. All profile values and generated payloads are synthetic inputs.

The versioned generator uses unchecked UInt64 SplitMix64 arithmetic initialized from the seed's UInt32 bit pattern. Each step adds `0x9e3779b97f4a7c15`, xor-shifts by 30 and multiplies by `0xbf58476d1ce4e5b9`, xor-shifts by 27 and multiplies by `0x94d049bb133111eb`, then xor-shifts by 31. Weighted selection rejects unsigned draws below `unchecked(0-W) % W`, then takes the remainder modulo positive total weight W. Declared weight order is significant. A single-bucket selection still consumes a draw.

Selection and payload streams are separate, so payload size does not affect later tenant or bucket selections. Payload construction returns exactly the selected value-byte count; key, header and protocol sizes are outside that count. The generator streams event descriptions rather than materializing every payload. Required maximum-attempt and payload budgets are checked before the first event.

The payload stream starts at the seed's UInt32 bit pattern XOR `0xd1b54a32d192ed03` XOR the event ordinal multiplied by `0x9e3779b97f4a7c15`, with unchecked UInt64 arithmetic. Emit each SplitMix64 word least-significant byte first and truncate the last word to the requested size. Literal tests cover the resulting byte stream independently of selection.

Document ordinals are global and zero-based, document IDs are `d` followed by invariant decimal, sequences are the lifecycle index, and the key is `tenantId-documentId`. Instance and application attempt identities are outside the seed. Planned event time is computed from the absolute stage start and slot ordinal, without accumulating per-slot rounding. These are planned times, not achieved throughput or delivery times.

## Identity and final rows

`RunIdentity` contains scenario, configuration, run, actual instance, expected instance and client IDs. Missing identity or an actual instance different from the runner-recorded expected instance is rejected. Detecting an additional worker, even one that sends no messages, remains a runner integration obligation.

`AttemptEntry` contains instance/attempt IDs, invocation ordinal, tenant/document IDs, key, sequence, event type, planned microseconds and actual invocation microseconds. Register it independently before the actual application call. An unexecuted schedule opportunity is not a registered attempt. Inventory capacity and duplicate IDs are rejected before registration succeeds.

`LedgerRow` contains schema version, run identity, the complete attempt entry, completion source, delivered flag, numeric error code and nullable symbolic name, nullable persistence status and provenance, completion microseconds, and nullable callback latency or synchronous call duration. The snake_case JSON names follow these DTO fields. All elapsed offsets are run-relative monotonic microseconds; the manifest carries the original timestamp frequency and start timestamp.

Completion source is `callback` or `synchronous_rejection`. Callback latency is application elapsed milliseconds immediately before invocation to handler entry; it is not wire/request latency. Synchronous rejection has a separate call duration and null callback latency. Its persistence status is null with `pre_enqueue` provenance; it must not turn a managed default into a native observation. Callback status provenance is `native_callback` when present or `missing` when absent. Native/client behavior is implemented and sourced in the worker stage, not inferred by this pure contract.

## Independent manifest and read-back

`RunManifest` contains schema and generator versions, run identity, planned count, independently computed actual count and the sealed inventory, timestamp frequency/start, worker status, bookkeeping completeness, first unexecuted ordinal when applicable, diagnostics, runtime versions, evidence file names and guard observations. The manifest is saved directly, independently of the ledger queue and writer. It is never reconstructed by counting persisted rows.

Before acceptance, read the persisted ledger back and compare its IDs, count, full attempt identity, keys and sequences with the sealed inventory. Require exactly one final row per actual attempt. Missing failed rows are invalid bookkeeping even if the corresponding message was never independently observed. Duplicate rows, mismatched identity and inconsistent manifest counts are rejected. This is the harness bookkeeping rule in [ADR-012][oracle].

Local evidence uses schema version 1, UTF-8, snake_case property names and invariant JSON numbers. Ledger rows are one complete JSON object per LF-terminated line. Missing, duplicate and unknown harness-envelope fields, wrong types/ranges, unsupported versions and truncated rows are rejected within explicit size/depth/count limits. Native raw JSON later stored inside an envelope retains the statistics bridge's own parser policy.

The writer uses bounded non-waiting admission and a separate background file writer. Queue overflow, serialization/size failure or file failure permanently faults the store; later successful writes cannot heal it. Draining and independent manifest saving are separately callable, so the final manifest can retain late capture/completion diagnostics. All resource and drain budgets are required inputs, without production defaults.

Unit tests cover in-memory generation, codecs, inventory and reconciliation. Filesystem cases are component tests in isolated temporary directories. Local tests establish harness behavior, with no live result, verifier totals or A01 verdict.

[design]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/68#issuecomment-6201728123
[handoff]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/68#issuecomment-6201890702
[oracle]: ../blueprint.md#adr-012-the-oracle-is-a-per-message-ledger-joined-into-four-outcome-classes
