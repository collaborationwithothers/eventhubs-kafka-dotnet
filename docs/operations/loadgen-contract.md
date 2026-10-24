# Load generator evidence contract

The load generator executes a finite synthetic offered schedule and preserves independent attempt evidence. Its inventory is registered before each application invocation, while the ledger records terminal observations separately. This contract implements the [agreed design][design] and [implementation handoff][handoff]; it does not establish A01 execution readiness.

## Worker lifecycle

The load generator runs one finite scenario repeat under a unique Kubernetes Job and pod identity. It resolves its settings from the packaged scenario, registers the invocation before constructing Kafka, publishes failure diagnostics when needed, and writes a completion receipt only after execution, publication and cleanup succeed. It does not classify verifier outcomes or change scenario expectations.

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

The Generic Host binds every writable property in [LoadGenHostOptions](../../src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs) explicitly. The required inputs include phase, repeat number, Job name, pod UID and Application Insights connection string, in addition to the existing scenario, worker, storage, resource and finite-budget inputs. `PARCHWELL_SOURCE_COMMIT` must contain exactly 40 lowercase hexadecimal characters. `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` and `AZURE_FEDERATED_TOKEN_FILE` must be nonblank. Worker options, source identity and the existing workload-identity requirements are checked before telemetry registration or credential construction. The connection string is configured for telemetry and is never logged or attached to a signal.

The accepted phase/configuration pairs are `probe/C0`, `C0/C0` and `C3/C3`; repeat numbers are 1 through 5. Run IDs are lowercase DNS labels of at most 29 characters. The Job name is exactly `<run-id>-<lowercase-phase>-r<repeat>-loadgen`. Pod, actual instance, expected instance and producer client IDs remain equal to the downward-API pod name. The separately recorded pod UID is nonblank and contains no whitespace or control characters.

The filesystem adapter reads only logical scenario and profile paths beneath its configured root and rejects links and path traversal. The existing loader and resolver select the configuration; `expected.yaml` is never read. Effective producer settings and their baseline/override metadata are retained in `capabilities.json`. Resolving a verifier role does not construct a verifier.

The worker validates the producer authentication baseline and publisher availability, then creates a bounded registration object before `RunSessionFactory` or any native producer factory can run. Registration failure, conflict, timeout or cancellation exits nonzero without constructing a producer or writing completion. The worker seals its independent attempt inventory, drains the producer and evidence stores, reconciles persisted rows, and saves the manifest. It publishes normal artifacts only for a successful result and only after session cleanup succeeds. Any run, finalisation, cleanup or publication failure remains sticky. A hard watchdog termination can leave partial objects but cannot establish successful completion.

An asynchronous delivery failure or uncertain persistence remains a sticky oracle guard observation. A fully reconciled ledger can still be complete bookkeeping. The worker does not calculate verifier counts or declare oracle eligibility. Required capture failure, missing rows, incomplete admissions, cleanup failure or publication failure prevents a successful final exit. The worker retains identity-bearing failure reporting. This uses the pinned [Flush contract][native-producer] and [callback path][native-callback]. Only the verified [failed-enqueue path][native-enqueue] is used to label `Local_QueueFull` as a synchronous pre-enqueue rejection; application faults and the separate [serializer exception paths][native-serialization] remain failures.

The publication adapter uses workload identity and does not substitute test credentials or local test client settings. Local component fixtures inject Confluent.Kafka's existing native interface and exercise the same executor, callbacks and ledger. They establish harness behavior against the [pinned local broker][local-broker], with no Event Hubs result. Deployment, registration and last-action receipt policy follows [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates).

## Statistics and request capability evidence

The second stage adds create-only `statistics.jsonl`, `request-lifecycle.jsonl` and `capabilities.json` files. `MaxReportBytes` limits original native JSON by UTF-8 size. A serialized statistics row has a separate admission ceiling equal to `MaxEvidenceFileBytes`, the total per-file budget; JSON escaping, typed fields, diagnostics and identity can make the envelope larger than its raw JSON. The exact serialized row is checked before queueing, so an envelope above that ceiling is rejected synchronously rather than accepted and failing in the background writer. These are independent limits: the raw JSON limit does not guarantee that every envelope fits the file budget. Statistics envelopes carry the full run identity, the bridge identity/session, original JSON, typed samples and capture diagnostics. Lifecycle records distinguish local receipt time/order from absent native source time/order. Serialization and durable writes run outside native callbacks. Queue, size and writer failures remain sticky.

Strict read-back receives the expected run identity and independently selected resolved producer-role configuration. Production LoadGen uses capture-only expectations; compatibility readers and fixtures may supply an independent broker inventory. The reader selects its exact completion shape from those trusted expectations, never from an artifact field. It requires the persisted effective settings and omissions to match that selection exactly, including baseline metadata and whether each attributed override is present, its selected value, scenario/configuration provenance, and location. The schema-one capability claims must match the canonical pinned-source catalogue, including each claim's source URLs, source facts and inference. For successful capture, observed broker names must equal the set in persisted statistics; faulted capture may retain additional names from reports that failed before persistence. A complete capture-only receipt requires a nonempty parsed broker report, no report diagnostics, no unparsed typed snapshots, no completion diagnostics, an empty `missing_brokers`, a nonnull drain boundary and a persisted first post-drain timestamp strictly greater than that boundary. That first timestamp must occur in the persisted statistics. It also requires strictly increasing native timestamps with a stable timestamp-minus-age creation value, nondecreasing per-broker request-timeout/disconnect/retry/error counters and unique broker names within each typed report. Compatibility receipts additionally require persisted reports covering every independently expected broker. These generation and counter consistency checks apply only to a receipt claiming completion; incomplete or unmarked evidence remains available for archival with its diagnostics. Capture-only receipts always keep `missing_brokers` empty and do not claim independent broker coverage. Incomplete capture preserves the observed nullable boundary and first post-drain timestamp. Compatibility inventory receipts continue validating missing and observed brokers against the supplied inventory. A persisted report may predate drain, so its presence alone does not establish freshness. Required statistics evidence must retain a complete completion marker. Lifecycle receipt sequences must be unique; concurrent capture may enqueue those assigned sequence numbers in a different file order. The schema-one callback rows retain the writer's fixed provenance and session labels (`managed_log_callback` and `managed_error_callback`), and the log row's inference remains null because the writer records none. Native error names must match their codes, and `is_error` must agree with the pinned [Confluent.Kafka 2.16.0 `Error.IsError` definition](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Error.cs#L193-L198). When guard observations claim completeness on a nonfaulted capture, every positive persisted broker/counter pair and callback with `is_error=true` must have exactly one matching retained guard, and every retained guard must match persisted statistics or an error callback. Counter values are matched against the set of persisted positive samples because concurrent reports can enqueue out of capture order. Faulted capture may retain guards from reports that were not persisted, including an earlier value for a pair that later appears in the artifact. First-positive values remain sticky. Native-error and counter guards share one retention bound, and overflow latches incomplete capture.

The selected broker fields retain the bridge's mapping: throttle window maximum in milliseconds, cumulative request-timeout and disconnect counts, and RTT window p99 in microseconds. Empty windows retain an absent value. Positive `req_timeouts`, `txretries`, `txerrs` and `rxerrs` observations remain recorded after later good samples; these are guard inputs, not additional selectable measures. These units and cumulative/window meanings come from [librdkafka 2.16.0 statistics][native-statistics]. Periodic broker names do not supply an independent lifecycle inventory or prove guard completeness.

The C# statistics receipt ([`KafkaStatisticsCompletion`](../../src/Parchwell.Kafka/Statistics/KafkaStatisticsCompletion.cs)) exposes nullable `DrainBoundaryMicroseconds` and `FirstPostDrainTimestampMicroseconds`. The statistics session captures the boundary after producer drain, using the native clock in microseconds, and records the first successfully written qualifying post-drain snapshot. The capture-only completion result proves the captured report is nonempty, attributable and fresh; it does not claim independent broker coverage. See the [statistics bridge](statistics-bridge.md#drain-completion-and-clock-proof) for the source and strict comparison.

In schema version 1 `capabilities.json`, each non-null capture-only `statistics_completion` object contains `drain_boundary_microseconds` and `first_post_drain_timestamp_microseconds`. The [writer](../../src/Parchwell.LoadGen/Run/Evidence/RunEvidenceSession.cs) preserves nullable observations, while the [reader](../../src/Parchwell.LoadGen/Run/Evidence/RunEvidenceReader.cs) requires both properties and applies capture-only freshness from trusted expectations. Inventory-compatibility output retains its existing completion field set. A null `statistics_completion` remains null.

LoadGen's existing post-drain freshness check remains strict `>` against the rounded boundary, so a report whose native `ts` equals the boundary does not qualify. The approved future verifier check 25 uses `>=` against the recorded boundary, following its "at or after" rule; that verifier check is planned for a later pull request and is not implemented here ([issue #80 Q2 answer][drain-boundary-decision]). The safety of equality for that future check relies on an inference: when the report and boundary use the same host's monotonic clock, `ts` truncated to microseconds and a boundary rounded up to microseconds make equality at or after the true boundary. The clock-source dependency and its limits are documented in the [statistics bridge][statistics-clock].

The pure probe evaluator follows [approved A01 revision 3][a01-plan]: all five distinct, correctly attributed C0 repeats must have usable throttle evidence. Any value strictly above the configured threshold passes; five usable repeats without one yield `UntestableAtBuildScale`; missing or unusable evidence yields `Inconclusive`. Oracle invalidity alone does not erase usable throttle observations. The evaluator neither schedules repeats nor reads expectations.

`capabilities.json` separates source facts from inference for request identity, matched response/error/throttle attribution, deadline provenance, retransmission, ordering, completeness and broker lifecycle inventory. The accepted result is negative: the pinned [managed log fields][native-log-fields], [callback threading contract][native-log-handler], [limited timeout diagnostics][native-timeouts] and [retry/deadline paths][native-request] do not establish the complete collector this adapter would need. No native patch, debug override or packet interception is added. A01's request-level measurement remains unsupported, and complete broker lifecycle coverage remains unproven; worker completion cannot turn either into an available capability.

## Publication and storage paths

The production composition root uses Azure.Storage.Blobs 12.30.1 and Azure.Core 1.60.0, with [WorkloadIdentityCredential][workload-identity] and a real SDK service client. The SDK accepts [TokenCredential construction][blob-token]; the adapter uses create-only upload conditions for registration, payload, index, diagnostics and completion objects. The source endpoint is an HTTPS service root without embedded credentials, query or fragment, and the container is `ledger`. The adapter neither provisions containers nor overwrites or deletes objects.

For run `run-001`, phase `C0`, repeat 1 and pod `pod-001`, the repeat prefix is `run-001/C0/r1/` and the instance prefix is `run-001/C0/r1/producer/pod-001/`. Registration is `registration.json`; accepted payloads use `publication/`; diagnostics use `diagnostics/`; completion is `completion.json`.

Registration JSON contains schema version 1, the existing six-field artifact identity, phase, repeat number, Job name, pod name, pod UID and source commit. Completion adds `mode: "loadgen"`, `handler_exit_code: 0` and `cleanup_completed: true`. Both records are UTF-8, at most 65,536 bytes and create-only. An ambiguous response or conflict is failure; no read-back fallback manufactures success.

The accepted publication contains exactly `ledger.jsonl`, `manifest.json`, `statistics.jsonl`, `request-lifecycle.jsonl`, `capabilities.json` and `index.json`. The manifest holds portable evidence names; local paths remain in the execution result. The publisher retains its successful-result validation, semantic checks, identity, hashes, lengths, per-file and aggregate ceilings, mutation checks and index-last behavior. Failure diagnostics consider only the five known payload paths. Each available regular file is copied once under a fresh cleanup deadline if it fits its byte limit and the aggregate snapshot budget, then uploaded create-only under one fresh bounded upload deadline. Missing, locked, changing or oversized files remain gaps. Diagnostics have no index or completion record and cannot repair a failed result or satisfy normal publication validation.

Readers still validate the index, complete member set, hashes, identity, manifest, ledger and strict evidence envelopes under independently supplied byte limits. Runner acceptance and the lifecycle receipt policy are owned by ADR-020 and the [runner contract](a01-runner.md), not by this worker document. Local tests use the real SDK through its [transport seam][sdk-transport], fake test credentials and recorded HTTPS requests; they do not exercise Azure identity exchange, RBAC, private networking or Event Hubs.

## Finite worker limits

These are execution limits, not measured runtime or scientific thresholds. The conservatively summed path includes setup, the offered schedule, failed normal publication and diagnostic fallback. It totals 850 seconds. The process limit is 900 seconds; the LoadGen Job limit is 1,140 seconds, including a 30-second termination grace and a 210-second scheduling/start reserve. C6 owns the replacement repeat, phase and enclosing Job totals. The previous scenario workflow must not be dispatched until C6 replaces its orchestration.

| Operation | Limit |
|---|---:|
| Registration | 10 s |
| Scenario/authentication/local setup allowance | 40 s |
| Offered schedule | 240 s |
| Producer drain | 300 s |
| Ledger drain | 10 s |
| Statistics completion | 20 s |
| Native producer disposal wait | 10 s |
| Evidence writer drain | 20 s |
| Capabilities save | 20 s |
| Evidence and ledger readback | 10 s each |
| Manifest save | 10 s |
| Normal and diagnostic upload | 30 s each |
| Diagnostic snapshot copies | five independent 10 s operations |
| Session cleanup | 10 s aggregate |
| Host stop and disposal | 10 s aggregate |
| Watchdog cleanup reserve | 10 s |
| Completion write | 10 s |
| Process watchdog | 900 s |
| LoadGen Job | 1,140 s |

Each finalisation operation has its own deadline. One expired or cancelled token does not suppress later evidence, readback, manifest or diagnostic attempts. A native disposal timeout waits on its one disposal attempt and proceeds only to bounded failure preservation; it does not start a concurrent disposal or claim Kafka cleanup succeeded.

## Telemetry

LoadGen uses CT's `Parchwell.Workers` ActivitySource and Meter; the worker-specific registration and signal contract is described in [telemetry](telemetry.md) and [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates). Activity names are `loadgen.run`, `loadgen.registration`, `loadgen.finalisation`, `loadgen.publication` and `loadgen.diagnostics`. The counter `parchwell.worker.operations` and histogram `parchwell.worker.operation.duration_ms` carry `scenario`, `run`, `role=LoadGenProducer`, `client_id`, `operation` and `outcome` tags. Durations use the injected `TimeProvider`. Signals contain no payload, connection string, native JSON or scientific counts.

## Validation receipt

Issue #161 C4 implementation, plan Revision 5. Source baseline: `e70b6f4e4c55a4a5755a8117300e267d725c408f`; branch: `codex/161-registered-loadgen`.

| Command | Result |
|---|---|
| `./scripts/verify-dotnet.sh` | Locked restore passed. The first Release build found implementation/analyzer errors; after those fixes, a later permitted run found test compile errors. Subsequent fixes pass a standalone Release build, but the full verifier was not rerun after the two-rerun limit. Full acceptance remains incomplete. |
| `dotnet build Parchwell.slnx --configuration Release --no-restore` | Passed: 0 warnings, 0 errors. A later `dotnet build tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --configuration Release --no-restore` also passed after the final fixture edits. |
| `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --configuration Release --minimum-expected-tests 1 --results-directory /tmp/161-loadgen-full -- --report-xunit-xml --report-xunit-xml-filename results.xml` (with `DOTNET_ROOT=/workspace/.dotnet` and that SDK on `PATH`) | Exit 2 after 1m 06s: 337 total, 335 passed, 2 failed, 0 skipped. `LoadGenWorkerHostTests.Given_ProcessLifecycle_When_Completing_Then_BlobReceiptRequiresPublicationStopDisposalAndWatchdogCleanup(outcome: "success")` failed at line 402 (`Expected: 0; Actual: 1`); its completion assertion at line 396 failed because the observed lifecycle flag was false. `ProducerRegistrationTests.Given_RegistrationSuccess_When_ConstructingProducer_Then_RegistrationAlreadyCompleted` failed at line 73 (`Expected: 1; Actual: 0`). The complete report is `/tmp/161-loadgen-full/results.xml`. Both Docker-backed checks passed; Testcontainers pulled `confluentinc/cp-kafka:7.5.12@sha256:997883c60e6f1207fb5f779c7a8fdae57312a17f446fd5ea3eb8b73f52d4a1cf`, created two containers, and both became ready. The two failures block acceptance; the verifier was not run afterward. |
| `npm ci --ignore-scripts` | Passed. |
| `npm run test:docs` | Passed: 27 tests, 0 failed. |
| `npm run check:docs` | Passed with 38 advisory warnings in four document groups. The `a01-runner.md` group includes a stale link to the removed optional deployment-boundary section; unrelated missing-file warnings remain in issue-114 evidence documents. |
| `git diff --check` | Passed. |

Accepted P1 removals: delete `DeploymentAdmissionTests.cs` (image smoke, deployment permit/release, identity and broker admission cases); delete `GeneratedValuesCompatibilityTests.cs` (generated-document comparison); remove the three obsolete generated-values comparison theories from `LoadGenWorkerHostTests`; remove `Given_AllLoadGenOptions_When_Generated_Then_EveryWritablePropertyHasAnExplicitInput` from `DeploymentGeneratorTests`. The two minimum Runner generator-test edits and existing OAuth admission tests remain.

Unresolved acceptance gaps: investigate and fix the two full-suite assertion failures before rerunning the full LoadGen suite and verifier; disposition the stale cross-document anchor. The repaired statistics host tests and the full Docker-backed checks passed, but acceptance remains incomplete because the full suite had two failures. The verifier was not run after that blocker. No live workflow or Azure credential was used. Orchestration requested GPT-6 Luna at high effort; the tool surface does not expose an independent runtime model identifier.

[design]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/68#issuecomment-6201728123
[handoff]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/68#issuecomment-6201890702
[oracle]: ../blueprint.md#adr-012-the-oracle-is-a-per-message-ledger-joined-into-four-outcome-classes
[native-producer]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/IProducer.cs#L211-L244
[native-callback]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Producer.cs#L272-L287
[native-enqueue]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Producer.cs#L323-L355
[native-serialization]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Producer.cs#L1035-L1097
[native-statistics]: https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md
[drain-boundary-decision]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6236696194
[statistics-clock]: statistics-bridge.md#drain-completion-and-clock-proof
[native-log-fields]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/LogMessage.cs#L45-L71
[native-log-handler]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ProducerBuilder.cs#L261-L278
[native-timeouts]: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L968-L1107
[native-request]: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_request.c#L5030-L5127
[local-broker]: ../../tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs
[blob-token]: https://github.com/Azure/azure-sdk-for-net/blob/Azure.Storage.Blobs_12.30.1/sdk/storage/Azure.Storage.Blobs/src/BlobClient.cs#L161-L181
[blob-create-only]: https://github.com/Azure/azure-sdk-for-net/blob/Azure.Storage.Blobs_12.30.1/sdk/storage/Azure.Storage.Blobs/src/BlobClient.cs#L854-L899
[workload-identity]: https://github.com/Azure/azure-sdk-for-net/blob/Azure.Core_1.60.0/sdk/core/Azure.Core/src/Identity/Credentials/WorkloadIdentityCredential.cs#L14-L42
[sdk-transport]: https://github.com/Azure/azure-sdk-for-net/blob/Azure.Core_1.60.0/sdk/core/Azure.Core/src/Pipeline/HttpClientTransport.cs#L107-L136

[a01-plan]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/14#issuecomment-6198834304
