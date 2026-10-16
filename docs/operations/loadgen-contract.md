# Load generator evidence contract

The load generator executes a finite synthetic offered schedule and preserves independent attempt evidence. Its inventory is registered before each application invocation, while the ledger records terminal observations separately. This contract implements the [agreed design][design] and [implementation handoff][handoff]; it does not establish A01 execution readiness.

## Delivery stages

The first stage supplies workload generation, an independent inventory, local ledger persistence and strict read-back reconciliation. The second adds the executable worker, native client execution, local evidence capture and explicit exit handling. The third supplies Blob publication required for production completion. No stage implements verifier classification or changes scenario expectations.

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

Inventory ordinals count every invocation contiguously from zero, including a repeated invocation of the same logical event. Schedule progress counts first-seen logical slots, derived from the document ordinal and lifecycle sequence defined above. First-seen slots must form a contiguous prefix; a retry must retain its tenant, document, key, sequence and event type. The actual invocation count may exceed the planned count. The executor records admissions in a separate counter rather than deriving that count from the inventory length. When schedule progress is less than the planned count, first_unexecuted_ordinal equals that progress; otherwise it is null. A partial schedule cannot claim complete worker status. These checks validate retry bookkeeping without implementing retry execution.

Before acceptance, read the persisted ledger back and compare its IDs, count, full attempt identity, keys and sequences with the sealed inventory. Require exactly one final row per actual attempt. Missing failed rows are invalid bookkeeping even if the corresponding message was never independently observed. Duplicate rows, mismatched identity and inconsistent manifest counts are rejected. This is the harness bookkeeping rule in [ADR-012][oracle].

Local evidence uses schema version 1, UTF-8, snake_case property names and invariant JSON numbers. Ledger rows are one complete JSON object per LF-terminated line. Missing, duplicate and unknown harness-envelope fields, wrong types/ranges, unsupported versions and truncated rows are rejected within explicit size/depth/count limits. Native raw JSON later stored inside an envelope retains the statistics bridge's own parser policy.

The writer uses bounded non-waiting admission and a separate background file writer. Queue overflow, serialization/size failure or file failure permanently faults the store; later successful writes cannot heal it. Draining and independent manifest saving are separately callable, so the final manifest can retain late capture/completion diagnostics. All resource and drain budgets are required inputs, without production defaults.

Unit tests cover in-memory generation, codecs, inventory and reconciliation. Filesystem cases are component tests in isolated temporary directories. Local tests establish harness behavior, with no live result, verifier totals or A01 verdict.

## Worker inputs and completion

The Generic Host executable binds the `LoadGen` configuration section, including environment variables named `LoadGen__<Property>`. Every property in [LoadGenHostOptions](../../src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs) is an explicit input. Missing inputs fail startup. These include the selected scenario/configuration, run and worker identities, pod-derived client identity, source/output roots, namespace, Blob destination, independent expected broker names and finite parsing, scheduling, queue, file, cleanup, publication and process budgets. Runtime options cannot introduce Kafka settings or disable a measurement declared by the selected scenario.

The filesystem adapter reads only logical scenario and profile paths beneath its configured root and rejects links and path traversal. The existing loader and resolver select the configuration; `expected.yaml` is never read. Effective producer settings and their baseline/override metadata are retained in `capabilities.json`. Resolving a verifier role does not construct a verifier.

The executor creates its producer after preflight, registers each actual invocation independently, stops admission on cancellation, excessive lateness or harness capture failure, and seals the inventory. It drains the producer and ledger, completes statistics while the producer remains alive, disposes the producer while lifecycle capture remains active, then closes evidence and reconciles persisted rows before independently saving the final manifest. The per-call delivery handler records bounded immutable observations without waiting, serializing or writing files. This uses the pinned [Flush contract][native-producer] and [callback path][native-callback]. Only the verified [failed-enqueue path][native-enqueue] is used to label `Local_QueueFull` as a synchronous pre-enqueue rejection; the worker treats application faults and the separate [serializer exception paths][native-serialization] as failures.

The offered schedule origin is sampled after producer construction, so setup is outside the offered schedule. Payload, message, headers and callback allocation, inventory validation and collection capacity growth precede the per-call monotonic sample. Lateness and cancellation checks precede the final inventory commit and `Produce`; rejection discards the reservation without recording an invocation. The final commit assigns the sampled timestamp to the reserved entry before calling `Produce`, so callbacks and the manifest retain the same timestamp. The sample precedes this final bookkeeping and the managed call boundary; it does not measure entry into native code.

An asynchronous delivery failure or uncertain persistence remains a sticky oracle guard observation. A fully reconciled ledger can still be complete bookkeeping. The worker does not calculate verifier counts or declare oracle eligibility. Required capture failure, missing rows, incomplete admissions, cleanup failure or publication failure prevents a successful final exit. A process watchdog also exits nonzero if synchronous native cleanup stalls; a hard termination may leave only partial local artifacts, which cannot establish completion.

Production admission currently reports the missing ADR-003 authentication handler. Until the publication stage supplies its adapter, mandatory publication also remains unavailable at preflight. Neither path substitutes test credentials or local test client settings. Local component fixtures inject Confluent.Kafka's existing native interface and exercise the same executor, callbacks and ledger. They establish harness behavior against the [pinned local broker][local-broker], with no Event Hubs result.

## Statistics and request capability evidence

The second stage adds create-only `statistics.jsonl`, `request-lifecycle.jsonl` and `capabilities.json` files. `MaxReportBytes` limits original native JSON by UTF-8 size. A serialized statistics row has a separate admission ceiling equal to `MaxEvidenceFileBytes`, the total per-file budget; JSON escaping, typed fields, diagnostics and identity can make the envelope larger than its raw JSON. The exact serialized row is checked before queueing, so an envelope above that ceiling is rejected synchronously rather than accepted and failing in the background writer. These are independent limits: the raw JSON limit does not guarantee that every envelope fits the file budget. Statistics envelopes carry the full run identity, the bridge identity/session, original JSON, typed samples and capture diagnostics. Lifecycle records distinguish local receipt time/order from absent native source time/order. Serialization and durable writes run outside native callbacks. Queue, size and writer failures remain sticky.

Strict read-back requires the complete producer-role settings and omissions from the configuration catalogue, with matching baseline metadata and attributed overrides. For successful capture, observed broker names must equal the set in persisted statistics; faulted capture may retain additional names from reports that failed before persistence. Lifecycle receipt sequences must be unique; concurrent capture may enqueue those assigned sequence numbers in a different file order. Native error names must match their codes, and `is_error` must agree with the pinned [Confluent.Kafka 2.16.0 `Error.IsError` definition](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Error.cs#L193-L198). Complete statistics require a nonempty statistics artifact. When guard observations claim completeness, every positive persisted broker/counter pair and callback with `is_error=true` must have its retained guard; first-positive values remain sticky. Native-error and counter guards share one retention bound, and overflow latches incomplete capture.

The selected broker fields retain the bridge's mapping: throttle window maximum in milliseconds, cumulative request-timeout and disconnect counts, and RTT window p99 in microseconds. Empty windows retain an absent value. Positive `req_timeouts`, `txretries`, `txerrs` and `rxerrs` observations remain recorded after later good samples; these are guard inputs, not additional selectable measures. These units and cumulative/window meanings come from [librdkafka 2.16.0 statistics][native-statistics]. Periodic broker names do not supply an independent lifecycle inventory or prove guard completeness.

The pure probe evaluator follows [approved A01 revision 3][a01-plan]: all five distinct, correctly attributed C0 repeats must have usable throttle evidence. Any value strictly above the configured threshold passes; five usable repeats without one yield `UntestableAtBuildScale`; missing or unusable evidence yields `Inconclusive`. Oracle invalidity alone does not erase usable throttle observations. The evaluator neither schedules repeats nor reads expectations.

`capabilities.json` separates source facts from inference for request identity, matched response/error/throttle attribution, deadline provenance, retransmission, ordering, completeness and broker lifecycle inventory. The accepted result is negative: the pinned [managed log fields][native-log-fields], [callback threading contract][native-log-handler], [limited timeout diagnostics][native-timeouts] and [retry/deadline paths][native-request] do not establish the complete collector this adapter would need. No native patch, debug override or packet interception is added. A01's request-level measurement remains unsupported, and complete broker lifecycle coverage remains unproven; worker completion cannot turn either into an available capability.

[design]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/68#issuecomment-6201728123
[handoff]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/68#issuecomment-6201890702
[oracle]: ../blueprint.md#adr-012-the-oracle-is-a-per-message-ledger-joined-into-four-outcome-classes
[native-producer]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/IProducer.cs#L211-L244
[native-callback]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Producer.cs#L272-L287
[native-enqueue]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Producer.cs#L323-L355
[native-serialization]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Producer.cs#L1035-L1097
[native-statistics]: https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md
[native-log-fields]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/LogMessage.cs#L45-L71
[native-log-handler]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ProducerBuilder.cs#L261-L278
[native-timeouts]: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L968-L1107
[native-request]: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_request.c#L5030-L5127
[local-broker]: ../../tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs

[a01-plan]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/14#issuecomment-6198834304
