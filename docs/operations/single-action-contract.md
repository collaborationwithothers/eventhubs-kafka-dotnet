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

Each repeat has exactly `repeat`, `client_id`, `action`, `terminal`, `elapsed_ms`, `terminal_unix_ms`, `settings`, `omitted_keys`, `result`, `error`, `matched`, `client_errors`, `statistics` and `diagnostics`. Elapsed milliseconds are a nonnegative monotonic integer duration. Terminal time is nullable Int64 Unix milliseconds. Settings are the complete string dictionary supplied to client construction; omitted keys are the sorted unique baseline/scenario omission union. Each fresh client uses `<pod>-r<repeat>` as its client ID.

`result` is `success`, `error` or null. Error is null or exactly `code` (Int32), `is_fatal` (boolean), `origin` (`construction`, `local` or `broker`) and `reason` (string). Native codes and fatality are measured, never synthesized for code-less exceptions. Successful outcomes carry no error. Callback errors are separately retained as `code`, `is_fatal`, `origin`, `reason` and `observed_elapsed_ms`.

Matched data is null or exactly `topic`, `partition`, `key_base64` and `value_base64`. Assigned-read success requires the exact declared marker bytes. Only F04 C0 produces UTF-8 key `f04-seed:<run>`; every other `produce_one` key is `<ITEM>:<CONFIG>:<run>:r<repeat>`. F04 seed and control values contain exactly 1000 ASCII `s` bytes. EOF and unrelated records do not establish success.

Each statistics row has `sequence`, `observed_elapsed_ms` and `raw_base64`, preserving the complete original UTF-8 report. Sequence starts at 1 in callback order. The judge compares native emission time with terminal time, rather than treating callback receipt time as emission time. Native `time` is epoch seconds and `ts` is monotonic microseconds in the [pinned statistics specification](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md).

## Seed transport

The separate Enrichment seed writes one line beginning `PARCHWELL_F04_SEED_RECEIPT ` after its action, cleanup and host disposal. Its JSON fields are exactly `schema_version: 1`, `mode: "single-action-seed"`, `run_id`, `source_commit`, `job_name`, `pod_name`, `pod_uid`, `service_account: "enrichment-consumer"`, `topic: "document-state"`, `partition: 0`, `key_base64`, `value_sha256`, `delivered`, `error`, `handler_exit_code` and `cleanup_completed`. The hash is lowercase SHA-256 of the declared value. Error follows the capture error schema.

The workflow transports receipt bytes without deciding delivery success. It separately collects a pod envelope with exactly `schema_version: 1`, `job_name`, `pod_name`, `pod_uid`, `service_account`, `owner_kind` and `owner_name`. Exactly one receipt and one owning pod are required. The immutable `<run>-f04-seed-receipt` ConfigMap exposes `receipt.json` and `pod.json` read-only at `/run/seed/`. A smoke marker cannot satisfy a live seed prerequisite. Missing evidence is judged as a gap rather than replaced with successful content.

## Candidate and judge receipt

Candidate keys are exactly `schema_version: 1`, `mode: "single-action"`, `synthetic: true`, `run_id`, `scenario_id`, `configuration_id`, `source_commit`, `managed_version`, `native_version`, `image_digests`, `status`, `repeats`, `deterministic`, `outcome_class`, `adr012_counts`, `diagnostics`, `security_finding` and `item_assessment`. Image digests have exactly `loadgen`, `verifier` and `enrichment`, each a full SHA-256 digest. Raw statistics bodies remain outside the candidate.

Repeat projections have exactly `repeat`, `status`, `reasons`, `outcome_class`, `broker_count` and `statistics_reference`. Missing observations are null. A non-null outcome class has exactly `result`, `error_code`, `is_fatal` and `origin`. Statistics reference is null or exactly `sequence`, `native_time`, `native_ts` and `raw_sha256`, with the hash computed from original decoded bytes. Configuration and item judgement belong to the [verifier contract](verifier-contract.md#single-action-judgement).

The final configuration of each item may carry `item_assessment` with exactly `verdict`, `hypotheses` and `reason`; other configurations carry null. Verdicts use `confirmed`, `contradicted`, `undocumented`, `untestable` or null; hypothesis values use `holds`, `contradicted`, `recorded_only` or `undecided`. These candidates remain proposed assessments for Hari's protected verdict review. The [factory evidence policy](../agents/factory.md#evidence-pack) owns the single-action exception to ledger counts.

Judge completion has exactly `schema_version: 1`, `mode: "single-action-judge"`, `run_id`, `scenario_id`, `configuration_id`, `source_commit`, `job_name`, `pod_name`, `pod_uid`, `candidate_name`, `handler_exit_code: 0` and `cleanup_completed: true`. Candidate name is the derived Blob name. This receipt follows candidate publication and host disposal. Earlier configuration dependencies require matching successful receipts too.

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
| Client errors and statistics | 64 each per repeat |
| Decoded native report | 262144 bytes; 64 broker entries |
| Callback queue | 128 entries |
| Decoded matched value | 1100000 bytes; F04 requires 1000 |
| Nonmatching assigned records | 4096 per repeat |
| Registration enumeration | 16 pages; 1024 entries; 1048576 name bytes |

Exhaustion retains `capture_limit` or an explicit gap and cannot authorize truncated evidence as complete. Base64 bounds raw reports without arbitrary JSON-string expansion. The [LoadGen lifecycle](loadgen-contract.md#single-action-capture) and [chart contract](chart-contract.md#single-action-execution) own process and orchestration budgets.
