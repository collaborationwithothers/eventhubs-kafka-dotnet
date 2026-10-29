# Single-action evidence contract

A single-action configuration captures five fresh clients in one Job. The verifier judges the captured evidence and publishes a candidate; a successful final receipt is required before that candidate can be accepted. This contract implements the [approved S2 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/213#issuecomment-6425997798) and its [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/213#issuecomment-6426097434).

## Paths and identity

Let `P` be `<run>/single-action/<ITEM>/<CONFIG>/`, preserving item and configuration case. The run is a lowercase DNS label of at most 29 characters. Source commits are 40 lowercase hexadecimal characters. String comparisons are ordinal.

| Container | Name relative to P | Content |
| --- | --- | --- |
| `ledger` for loadgen, `single-action` for probe | `registrations/<pod>.json` | Registration before constructing any Kafka client |
| Same capture container | `instances/<pod>/capture.json` | Five-repeat capture |
| Same capture container | `instances/<pod>/completion.json` | Final capture receipt |
| `verifier` | `candidate.json` | Configuration candidate |
| `verifier` | `judge-completion.json` | Final judge receipt |

The capture Job is `<run>-<item lower>-<configuration lower>-single`; the judge adds `-judge`. The seed Job is `<run>-f04-seed`. The common `identity` object has exactly `scenario_id`, `configuration_id`, `run_id` and `instance_id`, all nonblank; instance ID equals the registered pod name.

Wire objects are closed: missing, unknown or duplicate keys, wrong types, invalid UTF-8 and trailing content are rejected. Nullable fields remain present with JSON null. These rules apply to the envelopes defined here; native statistics remain original report bytes.

## Capture envelopes

Registration has exactly `schema_version: 1`, `mode: "single-action"`, `identity`, `job_name`, `pod_name`, `pod_uid` and `source_commit`. Completion has those fields plus `repeats_attempted: 5`, `handler_exit_code: 0` and `cleanup_completed: true`. Its create-only write follows client cleanup, preceding Blob writes and host disposal. A diagnostic capture never substitutes for a successful completion receipt.

Capture has exactly `schema_version: 1`, `mode: "single-action"`, `synthetic: true`, `identity`, `job_name`, `pod_name`, `pod_uid`, `source_commit`, `managed_version`, `native_version` and `repeats`. Repeat ordinals are exactly 1 through 5, once each.

Each repeat has exactly `repeat`, `client_id`, `action`, `terminal`, `elapsed_ms`, `terminal_unix_ms`, `settings`, `omitted_keys`, `result`, `error`, `matched`, `client_errors`, `client_errors_dropped`, `statistics` and `diagnostics`. Elapsed milliseconds are a nonnegative monotonic integer duration. Terminal time is nullable Int64 Unix milliseconds. Settings are the complete string dictionary supplied to client construction; omitted keys are the sorted unique baseline/scenario omission union. Each fresh client uses `<pod>-r<repeat>` as its client ID.

`result` is `success`, `error`, `no_outcome` or null. Error is null or exactly `code` (Int32), `is_fatal` (boolean), `origin` (`construction`, `local` or `broker`) and `reason` (string). Native codes and fatality are measured, never synthesized for code-less exceptions. Origin is `construction` for an error raised while the client is constructed. Otherwise it is `local` when Confluent.Kafka's `Error.IsLocalError` is true, which means a code below -1, and `broker` for every other code, including -1, which Confluent.Kafka v2.16.0 names `ErrorCode.Unknown`, "Unknown broker error" ([ErrorCode.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ErrorCode.cs) lines 349 to 352; [Error.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Error.cs) lines 203 and 204). Successful outcomes carry no error. Callback errors are separately retained as `code`, `is_fatal`, `origin`, `reason` and `observed_elapsed_ms`.

`client_errors_dropped` is a required Int64 JSON integer. Acceptance follows the [parent plan, Revision 7](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6415356278), Decision rules 2/4 and Enforced checks 4/7; the [verifier](verifier-contract.md#single-action-judgement) applies them. The no-outcome class has explicit null `error_code`, `is_fatal` and `origin`.

Matched data is null or exactly `topic`, `partition`, `key_base64` and `value_base64`. Assigned-read success requires the exact declared marker bytes. Only F04 C0 produces UTF-8 key `f04-seed:<run>`; every other `produce_one` key is `<ITEM>:<CONFIG>:<run>:r<repeat>`. F04 seed and control values contain exactly 1000 ASCII `s` bytes. EOF and unrelated records do not establish success.

Each statistics row has `sequence`, `observed_elapsed_ms` and `raw_base64`, preserving the complete original UTF-8 report. Sequence starts at 1 in callback order. The judge compares native emission time with terminal time, rather than treating callback receipt time as emission time. Native `time` is epoch seconds and `ts` is monotonic microseconds in the [pinned statistics specification](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md).

## Seed transport

The separate Enrichment seed writes one line beginning `PARCHWELL_F04_SEED_RECEIPT ` after its action, cleanup and host disposal. Its JSON fields are exactly `schema_version: 1`, `mode: "single-action-seed"`, `run_id`, `source_commit`, `job_name`, `pod_name`, `pod_uid`, `service_account: "enrichment-consumer"`, `topic: "document-state"`, `partition: 0`, `key_base64`, `value_sha256`, `delivered`, `error`, `handler_exit_code` and `cleanup_completed`. The hash is lowercase SHA-256 of the declared value. Error follows the capture error schema.

The workflow transports receipt bytes without deciding delivery success. It separately collects a pod envelope with exactly `schema_version: 1`, `job_name`, `pod_name`, `pod_uid`, `service_account`, `owner_kind` and `owner_name`. Exactly one receipt and one owning pod are required. The immutable `<run>-f04-seed-receipt` ConfigMap exposes `receipt.json` and `pod.json` read-only at `/run/seed/`. A smoke marker cannot satisfy a live seed prerequisite. Missing evidence is judged as a gap rather than replaced with successful content.

## Candidate and judge receipt

Candidate keys are exactly `schema_version: 1`, `mode: "single-action"`, `synthetic: true`, `run_id`, `scenario_id`, `configuration_id`, `source_commit`, `managed_version`, `native_version`, `image_digests`, `status`, `repeats`, `deterministic`, `outcome_class`, `adr012_counts`, `diagnostics`, `security_finding` and `item_assessment`. Image digests have exactly `loadgen`, `verifier` and `enrichment`, each a full SHA-256 digest. Raw statistics bodies remain outside the candidate.

Repeat projections have exactly `repeat`, `status`, `reasons`, `outcome_class`, `broker_count` and `statistics_reference`. Missing observations are null. A non-null outcome class has exactly `result`, `error_code`, `is_fatal` and `origin`. Statistics reference is null or exactly `sequence`, `native_time`, `native_ts` and `raw_sha256`, with the hash computed from original decoded bytes. Configuration and item judgement belong to the [verifier contract](verifier-contract.md#single-action-judgement).

The final configuration of each item may carry `item_assessment` with exactly `verdict`, `hypotheses` and `reason`; other configurations carry null. Verdicts use `confirmed`, `contradicted`, `undocumented`, `untestable` or null; hypothesis values use `holds`, `contradicted`, `recorded_only` or `undecided`. These candidates remain proposed assessments for Hari's protected verdict review. The [factory evidence policy](../agents/factory.md#evidence-pack) owns the single-action exception to ledger counts.

Successful judge completion has exactly `schema_version: 1`, `mode: "single-action-judge"`, `run_id`, `scenario_id`, `configuration_id`, `source_commit`, `job_name`, `pod_name`, `pod_uid`, `candidate_name`, `handler_exit_code: 0` and `cleanup_completed: true`. Candidate name is the derived Blob name. This receipt follows candidate publication and host disposal. Earlier configuration dependencies require matching successful receipts too. Invalid and incomplete judgement retains an unaccepted diagnostic candidate under the [verifier exit policy](verifier-contract.md#single-action-judgement), without this successful receipt.

## Fixed bounds

The following are approved harness limits, not performance measurements.

| Bound | Limit |
| --- | ---: |
| Capture | 134217728 bytes |
| Registration, capture completion, judge completion | 65536 bytes each |
| Seed receipt, seed pod envelope | 65536 bytes each |
| Candidate | 16777216 bytes |
| Aggregate judge reads | 268435456 bytes |
| Input nesting depth | 32 |
| Settings | 128 keys; 16384 UTF-8 bytes |
| Error reason | 4096 UTF-8 bytes |
| Diagnostics | 100 per configuration; 2048 bytes each |
| Recorded client errors | 64 per repeat |
| Statistics | 64 per repeat |
| Decoded native report | 262144 bytes; 64 broker entries |
| Callback queue | 128 entries |
| Decoded matched value | 1100000 bytes; F04 requires 1000 |
| Nonmatching assigned records | 4096 per repeat |
| Registration enumeration | 16 pages; 1024 entries; 1048576 name bytes |

Exhaustion retains `capture_limit` or an explicit gap and cannot authorize truncated evidence as complete, except for dropped client-error recording under the [parent plan, Revision 7](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6415356278), 'Terminal rules'; the recorded-array bound remains. Base64 bounds raw reports without arbitrary JSON-string expansion. The [LoadGen lifecycle](loadgen-contract.md#single-action-capture) and [chart contract](chart-contract.md#single-action-execution) own process and orchestration budgets.

The parent's 'Client-log bound' and Decision rule 10 govern bounded pod logs and debug-cause availability.

## Schema-two live probes

Schema two is an isolated evidence path for partition selection (B08), null-value transport (E01) and permission scope (F06). It preserves the schema-one contract above. [Smoke admission and acceptance](probe-v2-smoke-contract.md) currently restrict this path to the prerequisite; no scientific item assessment is emitted.

A stage Job is `<run>-<item lower>-<configuration lower>-<stage>`. Actors and helpers use the LoadGen image; judges use the Verifier image and verifier service account. Roles choose Kafka configuration; service accounts choose the actual identity.

| Item/configurations | Stage order | Actor/helper accounts |
| --- | --- | --- |
| B08 C0/C1 | action, read, judge | action loadgen; read probe |
| E01 C0/C1 | seed, seedread, action, read, judge | seed enrichment-consumer; others probe |
| F06 C0-C5 | action, read, judge | even action loadgen; odd action probe; read probe |
| F06 C6-C8 | seed, action, judge | seed C6 loadgen, C7/C8 enrichment-consumer; action probe |
| F06 C9-C11 | action, judge | action enrichment-consumer |

All nonjudge stages have five ordinal slots. Action slots use fresh clients. Helpers construct a fresh client only for an admitted slot. Seed setup uses a separate Job and client under an already authorized Sender identity. For state and DLQ, that identity is enrichment-consumer, which also performs the later narrow Receiver observation; its existing Sender role does not establish Receiver permission.

### Raw envelopes

Exactly one stdout line prefixed `PARCHWELL_PROBE_V2_REGISTRATION ` precedes client construction. Its closed JSON keys are `schema_version:2`, `mode:"single-action-v2"`, `synthetic:true`, `run_id`, `scenario_id`, `configuration_id`, `stage`, `source_commit`, `job_name`, `pod_name`, `pod_uid`, `service_account`.

Exactly one `PARCHWELL_PROBE_V2_CAPTURE ` line follows client cleanup and host stop/disposal. It repeats those keys and adds `managed_version`, `native_version`, `repeats`, `handler_exit_code`, `cleanup_completed`, `host_cleanup_completed`. Acceptance requires zero exit code and both cleanup flags true. Failure retains diagnostics without a successful receipt. Actor mode registers no Blob writer.

Each repeat has exactly `repeat`, `admission`, `client_id`, `action`, `terminal`, `elapsed_ms`, `terminal_unix_ms`, `settings`, `omitted_keys`, `metadata_partitions`, `requested`, `delivery`, `read_observation`, `result`, `error`, `client_errors`, `statistics`, `diagnostics`. The five repeat ordinals are exactly 1 through 5. Required clients use `<pod>-r<repeat>`; settings preserve the actual construction dictionary and omitted-key union. Timing and callback sequences are nonnegative Int64 values. Terminal time is Unix milliseconds; elapsed time and sequence are monotonic capture observations.

| Object | Exact keys |
| --- | --- |
| requested | topic, partition_mode, partition, offset, key_base64, value_base64 |
| delivery | topic, partition, offset, persistence_status, error, observed_elapsed_ms, sequence |
| read_observation | records_seen, eof_count, record |
| record | topic, partition, offset, key_base64, value_base64, observed_elapsed_ms, sequence |
| error | code, is_fatal, origin, reason, observed_elapsed_ms, sequence |
| statistics row | sequence, observed_elapsed_ms, raw_base64 |

Producer requested offsets are null; assigned-reader offsets are the exact nonnegative source delivery offset. Delivery/record/error may be null. A present value_base64 null means a genuine null value; `""` means empty bytes. A null record means no matching record, not a tombstone. Nonmatching records and EOF have separate counts. Error origin is construction, local or broker, by the rule for capture errors above; ordinary exceptions do not acquire invented Kafka codes. Native persistence status is retained verbatim. Metadata partitions is null except for constructed B08 clients, where it contains observed IDs.

### Actions, timing and statistics

B08 uses fixed UTF-8 key `b08-synthetic-0` and marker `B08:<CONFIG>:<run>:r<repeat>`. Its producer calls topic-only Produce, never an explicit destination or custom partitioner. On the same client handle, metadata must report exactly partition IDs 0 through 31. The independent probe reads the delivered location, including when that location differs from a scientific expectation. The native [partition selection](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_msg.c), [CRC32](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdcrc32.c) and [Murmur2](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdmurmur2.c) sources define the algorithms; smoke does not publish their live outcome as a verdict.

E01 uses UTF-8 key `E01:<CONFIG>:<run>:r<repeat>` and independent non-null seed `seed:E01:<CONFIG>:<run>:r<repeat>`. Its seed must be read at the delivered location before the target. Target C0 is an empty byte array; C1 is an actual null. Seed and target share topic/partition/key; target offset must be greater. The pinned [serializer](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Serializers.cs) and [deserializer](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Deserializers.cs) preserve null versus empty. The [service compaction source](https://github.com/MicrosoftDocs/azure-docs/blob/c479cc3fd5f3727448596c3a3b7b339fe2677227/articles/event-hubs/log-compaction.md) defines null tombstones; this transport path establishes no compaction timing or recovery result.

F06 sends key/value `F06:<CONFIG>:<run>:r<repeat>`. C0/C1 use events, C2/C3 state, C4/C5 DLQ. C6-C8 seed/read those respective topics at partition zero; C9-C11 read the same seed locations under the narrow identity. Seed key/value is `f06-seed:<run>:<topic>:r<repeat>`. Service [authorization](https://github.com/MicrosoftDocs/azure-docs/blob/176f290c069cdda3557ea75876a749b1edc58b20/articles/event-hubs/authorize-access-azure-active-directory.md) and [role definitions](https://github.com/MicrosoftDocs/azure-docs/blob/1b4e136a1da731e1ed16444eb6ce5701bcd8c0f4/articles/role-based-access-control/built-in-roles/analytics.md) support distinct send/receive scope, not an exact error code or denial-by-silence guarantee.

The action clock starts before construction: at most 330000 ms for produce and 120000 ms for read, including metadata. The first delivery, synchronous Kafka exception, ConsumeException, exact-location record or fatal callback is terminal in capture sequence order. Nonfatal callbacks are retained without becoming terminal. Only F06 C10/C11 may record `no_outcome`, after the full read window with no target/error/drop and successful cleanup. Its meaning is only `no read and no error within the window`.

B08 consumers poll unassigned with slices at most 100 ms until their attached OAuth session has an installed token, a terminal/limit occurs, or their original deadline expires. Only then may the same-handle metadata call run, with min(60000 ms, remaining window); Assign follows valid metadata. No clock reset, Subscribe or extra client is introduced. This follows the pinned [Consume-driven OAuth callback contract](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ConsumerBuilder.cs) and [dependent handle API](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/DependentAdminClientBuilder.cs).

Poll for statistics for 20 seconds after terminal. Capture keeps the first received report without parsing native contents. The verifier requires native nonnegative Int64 time/ts and checked `time*1000 >= terminal_unix_ms`; epoch-second rounding or a queued earlier report can make evidence incomplete. Broker count uses existing learned/configured/logical and UP lower-bound semantics. Missing/unreadable statistics is null, not zero. The [native statistics specification](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md) supplies units; the freshness predicate is harness policy.

### Dependency transport and admission

Four immutable ConfigMaps named `J-evidence`, `J-evidence-1`, `J-evidence-2` and `J-evidence-3` transport each stage's evidence through `binaryData`. The first contains registration.json, pod.json and capture.json.part0; the others each contain capture.json.part1, capture.json.part2 or capture.json.part3 respectively. Each capture part contains at most 524288 original bytes; unused trailing parts are present and empty. The chart projects all four optional sources read-only at `/run/probe-inputs/J/`. Actors and judges require every part and concatenate them in ordinal order under the whole-capture bound. Original registration/capture bytes are strict UTF-8 JSON between stdout marker and line terminator; splitting may cross a UTF-8 scalar, and decoding occurs only after reassembly. Direct capture.json inputs remain supported and take precedence, without falling back after a failed direct-file read. Hashes bind the reconstructed original bytes.

Pod envelope keys are exactly schema_version=2, job_name, pod_name, pod_uid, service_account, owner_kind, owner_name, image_digest. Workflow requires the one owning pod and captures its actual selected container image digest. Missing parts, failed transport or missing evidence remain gaps. Fixed parts keep each ConfigMap below the existing serialized 1048576-byte transport ceiling; Kubernetes also [limits ConfigMap data to 1 MiB](https://kubernetes.io/docs/concepts/configuration/configmap/).

Dependencies are fixed, never enumerated from caller paths. B08 read uses action. E01 seedread uses seed, action uses seed+seedread, and read uses action+seed+seedread. F06 producer reads use action. C6-C8 action uses its seed. C9-C11 action uses the corresponding C6-C8 seed+action. Judges mount their own and cross-configuration dependencies plus private scope. Final item judges fetch earlier same-item candidates and matching receipts from derived verifier Blob paths; other judges do not fetch them.

A successful source receipt plus error-free Persisted delivery at a valid location requires independent readback, even if delivery followed an earlier terminal error. A complete error without acknowledged location permits `not_applicable_no_delivery`; it does not prove no append. Missing/malformed source, failed completion, absent terminal or success without location gives `not_attempted_dependency`. Cleanup failure stops subsequent construction and gives `not_attempted_cleanup`.

Every read Job still registers and emits its five-slot final capture, even if all slots have no acknowledged location. Non-required slots retain repeat/action/admission; client_id, terminal_unix_ms, metadata_partitions, requested, delivery, result and error are null; terminal=false; elapsed_ms=0; settings={}; omitted_keys/client_errors/statistics=[]; read_observation={records_seen:0,eof_count:0,record:null}. Diagnostics is empty for not_applicable_no_delivery, otherwise contains admission. Such slots construct no client. The verifier recomputes admission and still requires source evidence; no skip can excuse missing successful-send readback.

### V2 candidates and receipts

Top-level candidate keys are the schema-one set, with schema_version=2 and mode=single-action-v2. V2 image_digests has exactly loadgen and verifier, each `sha256:` plus 64 lowercase hex digits from the validated runtime references. Capture pod digests must equal loadgen; transport checks the judge pod against verifier and both against the dispatch image map. Prior candidates require the identical map. The V2 codec is separate; the three-image legacy codec is unchanged.

V2 repeat projections have exactly repeat, status, reasons, outcome_class, broker_count, statistics_reference, semantic_outcome, delivery, read_observation, seed_reference, control_references, clients. Semantic fields are exactly delivered_partition, consumed_partition, key_base64, value_kind, value_match, with inapplicable nulls. Every evidence reference has run_id, scenario_id, configuration_id, stage, source_commit, job_name, pod_uid, raw_sha256. Each clients row has client_id, stage, broker_count, statistics_reference. Statistics references retain sequence/native_time/native_ts/raw_sha256. Only checked fields are public; raw native reports, logs and private role material remain outside complete candidates. ADR-012 counts remain not_applicable with numeric totals absent.

Equality compares ordinary result/error-code/fatality/origin plus item/result semantic masks after per-repeat provenance validation. B08 includes delivered/consumed partition, key and value_match; E01 includes value_kind. F06 successful sends compare delivered/consumed partition, value_kind and value_match; successful reads compare consumed partition, value_kind and value_match. F06 errors/no_outcome add no semantics. Repeat markers, offsets, identities, times, sequences, reasons, statistics and hashes are excluded from equality only; their individual bindings remain checked.

Candidates are create-only at `<run>/single-action-v2/<ITEM>/<CONFIG>/candidate.json` in the verifier container. The final create-only judge-completion.json follows publication and host disposal. Its exact keys are schema_version=2, mode=single-action-v2-judge, run_id, scenario_id, configuration_id, source_commit, job_name, pod_name, pod_uid, candidate_name, candidate_sha256, handler_exit_code=0, cleanup_completed=true. A candidate is eligible only with this matching final receipt, including the last configuration's own receipt.

### Enforced evidence checks

The verifier owns these closed checks. Declaration/options/chart validators enforce their finite inputs; workflow checks transport/ownership only.

1. **V1 Identity:** exact registration/pod/run/item/config/stage/source/account/Job/image binding; five ordinals and required-client IDs. Mismatch is invalid v2_identity.
2. **V2 Completion:** final capture after all cleanup and successful applicable judge receipts. Missing/failed completion is incomplete v2_completion.
3. **V3 Wire/bounds:** strict UTF-8/closed JSON, types, duplicates, nullable presence, base64, offsets and ordinals. Malformed is invalid v2_wire; exceeded bound is incomplete v2_capture_limit.
4. **V4 Configuration:** actual resolved settings/omissions, declared identity/topic/seed/audience and B08 automatic destination. Mismatch is invalid v2_configuration.
5. **V5 Partition input:** observed B08 partition IDs exactly 0-31 on each constructed producer/reader. Missing metadata is incomplete; wrong input is invalid v2_partition_input.
6. **V6 Location:** Persisted delivery and independent exact-location/key/marker readback when required. Missing is incomplete v2_location; contradictory binding/false skip is invalid. Scientific expectation mismatch is not malformed evidence.
7. **V7 Null/seed:** E01 independent seed read, same key/partition and increasing target offset. Missing is incomplete v2_seed; wrong binding is invalid. Null, empty and absent record remain distinct.
8. **V8 Scope/controls:** deployed/effective roles, unchanged narrow identities and same-run independent seeds/controls. Missing is incomplete v2_controls; completed control failures are retained. No verifier or storage grant expansion.
9. **V9 Terminal/errors:** measured native origin/code/fatality, fixed terminal/absence rule and no silence after error/drop. Missing terminal is incomplete v2_terminal; malformed outcome is invalid. Preserve narrow success despite later evidence gaps.
10. **V10 Statistics:** first postterminal native report with the stated time/shape/broker predicate for each constructed client. Missing or unusable measurements remain absent, never zero-filled.
11. **V11 Aggregation:** five complete valid equal semantic action classes; invalid takes precedence, incomplete gets no verdict, unequal does not become a supported scientific result. Smoke positive-control handling follows its separate owning contract.
12. **V12 Publication:** create-only candidate and last-action successful receipt, checked name/source/hash/digest coordinates; raw logs are not authoritative outcomes. Scientific publication still requires hosted mutation evidence and protected verdict review.

[Runtime limits](probe-v2-runtime-limits.json) are the exact V2 finite table. A limit failure cannot authorize truncated evidence or an old callback/drop exception. Logs capped or unavailable have no debug-cause conclusion.

The [statistics repair receipt](evidence/issue-240/statistics-repair-receipt.md) derives the revised report, capture and container-log budgets from pinned native measurements and the five-slot envelope. Capture retains the first rejection as `capture_limit` plus a bounded `capture_rejected:<limit>:observed=<integer>:allowed=<integer>` diagnostic when a size/count can be measured. Invalid statistics UTF-16 records `capture_rejected:StatisticsUtf8`. These diagnostics include no rejected content, credentials or private identity. Exhaustion still fails completion; later smaller reports cannot replace rejected first evidence.
