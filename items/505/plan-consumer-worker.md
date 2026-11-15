# Plan: consumer worker processing mode (#505)

Issue: [#505](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505), child of [#499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499), split entry 2. Pull request name: `consumer-worker`. This is the contract-grade plan for that one pull request under docs/agents/factory.md "Item lifecycle" step 3.

Revision 3. Base: `main` at 09c5bfc384dc516e7b9a8a53d38dee30d40755df, the merge of #504's pull request #518. File paths and line numbers below are on that commit unless a line says otherwise. Between revision 1's base (23a6fb2639a9e0ea1c74dc466d7eba4a8c0a5c9a) and this one, no file under `src/` or `tests/Parchwell.*` changed, so every code line cited below is the same on both.

Changes since revision 2, from its [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6899350281), by finding:
- 2 to 4: the component tests CT1 to CT4 are redesigned. Messages are produced only after the start condition holds, because the baseline `auto.offset.reset` is `latest` (`BaselineSettingsCatalog.cs` line 185) and each repeat has a fresh group. CT2 and CT3 use a 2-partition topic, a committed offset before the slow message, and slow ordinal 11. CT2's `lost` row is expected after the slow row, because the lost handler runs only inside `Consume` or `Close`. CT3 names the subject's partition and counts only rows after the slow message starts.
- 5: plan validation checks that effective `max.poll.interval.ms` is at least effective `session.timeout.ms` before registration; U4 gains the boundary cases.
- 6: `tests/Parchwell.ArchitectureTests/packages.lock.json` joins the file scope.
- 7: deployment assumption D5 (group membership and offset commit on Event Hubs) is added to section 7 and Q3.
- 8 to 10 (`apply in revision`): the exact replacement texts are applied in 3.1, section 8 item 6, and section 6's fixture sentence.
- P2s applied: the `Tier=Docker` trait and test U8b, both `apply in implementation` in the check; the S1c citation now rests on line 509 and is labelled inference; "where one exists" is replaced; test U25 covers exit 124. Acceptance command 6 counts 31 rows.
- Finding 1 (open questions) is not answered here. Section 11 still holds Q1 to Q3.

Changes in revision 2, since revision 1:
- Rebased on the merged ADR-022 (`docs/blueprint.md` lines 735 to 794). Revision 1 was written against the ADR plan's revision 2; the merged text is that plan's revision 3, which changed the commit evidence.
- No explicit commits. Merged "Record" (line 753) says "No extra commit is made to generate evidence" and that the automatic handler does not report explicit `Commit` calls. The Confluent.Kafka source confirms the second point (S1d). Revision 1's window-end and revoke-handler `Commit()` calls are removed. Commits come only from librdkafka's auto-commit, including its commit of removed partitions on revoke and on close (S1c). Revision 1's question Q3 is gone.
- Commit row entries carry `topic` (line 753). `source` keeps both values; this pull request writes only `callback`.
- Stop: the member records the callbacks served during `Close` before its receipt (line 769, "Stop and observation boundary"; S1c, the close loop). Component tests check commit agreement against the broker's committed offsets (line 771).
- The predecessor question (revision 1's Q1) is gone, because #518 merged. Questions are renumbered Q1 to Q3.

This pull request adds a consumer processing mode to `Parchwell.Enrichment`. It runs one member of a two-member consumer group: it simulates processing, stores offsets after processing, pauses and resumes, and records revokes and losses. It writes the ADR-022 consumer record, registration marker and completion receipt to Blob. It pins the record's wire format, which the verifier child (#506) and the chart child (#507) read. Each open question in section 11 carries a recommended answer, under point 2 of [decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291).

Design authority: ADR-022 in `docs/blueprint.md` on `main`, lines 735 to 794 (docs/blueprint.md is the design authority, AGENTS.md).

## 1. Goal and authority

Goal: a worker mode that lets #507 run the subject and witness members of a C03, C10 or C01 repeat, and whose record lets #506 classify every message under ADR-022. Component tests show the K-55 and K-28 behaviours against a local broker.

Authorities, in creation order (factory.md "Item lifecycle" step 3, child issues):

1. #505 body: `Parent items: #499`, `Split entry: 2`, `Predecessor PRs: the pull request of #504 (entry 1)`. Scope: group subscription, the profile's per-message processing delay, store-after-process, pause and resume (K-55), revoke and lost handling (K-28), the ADR-022 processing record behind a seam at our own boundary, registration and completion receipt; component tests against Testcontainers Kafka; the Testcontainers reference is a P1, so Hari merges.
2. #505 [decision 6895300019](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6895300019) adopts #499 [decision 6895299617](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617) (grilling round 1: record row kinds, durability, two members, start order, self-termination, configuration fidelity, invalid run).
3. #505 [decision 6895328783](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6895328783) adopts #499 [decision 6895328446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446) (round 2: classes, completion, commit evidence, storage, C01 timing, witness, verifier order).
4. #505 [decision 6895343691](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6895343691) adopts #499 [decision 6895343227](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895343227) (round 3: SIGKILL, no topic writes, registration before consumer construction).
5. #505 [decision 6895536338](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6895536338) adopts the standing #499 [decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291): the #347 control session coordinates; grilling questions are written with recommended answers, adopted in advance; plan approval on a fresh check with `Blocking findings: none` and section 11 reading `None`; point 4 limits.

Parent decisions read as context (#505 body names them): #499 [6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441) (batch, K-55, K-28, K-57, K-47), [6894564479](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894564479) (heartbeats versus the poll limit), [6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233) (split approved; Claude review gate), [6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673) (Claude plans; Codex implements). Under factory.md step 3 they change this plan only through the adopting decisions above; none of them conflicts with this plan.

Evidence, not authority: [proposal revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894560135), "Proposed split" row 2.

Not adopted on #505: the decisions and approvals on #504. This plan relies on them only through ADR-022's merged text on `main`, which carries them.

Predecessor. `Predecessor PRs:` names the pull request of #504. That is #518, merged on 2026-11-15 as 09c5bfc384dc516e7b9a8a53d38dee30d40755df, and #504 is closed (factory.md step 3, "verify every entry in its `Predecessor PRs:` has merged").

There is no item state file (`items/505/` does not exist on the base) and no parent plan: #499 holds a proposal. Implementing tool: Codex (decision 6894614673 point 2).

## 2. Scope

Files this pull request may add or change:
- `src/Parchwell.Enrichment/ConsumerProcessing/**` (new slice).
- `src/Parchwell.Enrichment/Program.cs`: the new `--consumer-processing` mode only.
- `src/Parchwell.Enrichment/Parchwell.Enrichment.csproj`: add `PackageReference` `Azure.Storage.Blobs` and `Azure.Core` (both centrally pinned in `Directory.Packages.props` lines 14 and 15).
- `src/Parchwell.Enrichment/packages.lock.json`, `src/Parchwell.Enrichment/packages.linux-x64.lock.json`: regenerated by restore only.
- `tests/Parchwell.Enrichment.Tests/ConsumerProcessing/**` (new).
- `tests/Parchwell.Enrichment.Tests/Parchwell.Enrichment.Tests.csproj`: add `PackageReference` `Testcontainers` (line 10 of `Directory.Packages.props`).
- `tests/Parchwell.Enrichment.Tests/packages.lock.json` and `tests/Parchwell.ArchitectureTests/packages.lock.json`: regenerated by restore only. The architecture tests reference the enrichment project (`tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj` line 19), so the new packages reach that lock file too.
- `items/505/state.md` (factory.md "Item state file").

Must not change: `src/Parchwell.Enrichment/SeedF04/**` and the F04 and `--image-smoke` behaviour of `Program.cs`; `src/Parchwell.Kafka/**`, `src/Parchwell.Scenarios/**`, `src/Parchwell.LoadGen/**`, `src/Parchwell.Verifier/**`; `Directory.Packages.props`; `scenarios/`, `results/`, `docs/`, `charts/`, `infra/`, `.github/`, `scripts/`; `AGENTS.md`, `CLAUDE.md`, `README.md`.

Out of scope, owned elsewhere: SIGKILL self-termination and the kill-intent row write (#509); verifier join and checks (#506); chart, Job, environment values and role assignments (#507); scenario files, profile schema and in-process scenario resolution for C03, C10 and C01 (Q1 in section 11); the verifier's group-offset read (#506, ADR-022 "Independent commit evidence"); `document-state` and `document-events-dlq` writes (none, ADR-022 "No topic writes").

Protected and P1 under AGENTS.md "Code Review Rules": three new package references (`Azure.Storage.Blobs`, `Azure.Core` in the enrichment project; `Testcontainers` in its test project). The plan requires them, so the implementer keeps them and lists them in the pull request description; Hari merges (docs/agents/pull-requests.md rule 8, fourth paragraph).

## 3. External contracts

Everything in this section is read by #506, #507 or a later child. The implementer copies it exactly.

### 3.1 Command line and exit codes

- `--consumer-processing`: runs one member. Any other argument list keeps today's behaviour (`Program.cs` lines 21 to 34): `--image-smoke`, `--single-action-seed`, `--validate-seed-configuration`, otherwise exit 2.
- Exit 0 only after the completion receipt (3.5) is written. Exit 1 on every failure in 3.6. Exit 124 when the process watchdog fires (3.7).

### 3.2 Deployment inputs (configuration section `ConsumerProcessing`)

Bound with `ErrorOnUnknownConfiguration = true` and validated on start, as `SeedF04Options` is (`Program.cs` lines 98 to 110). #507 sets them as environment variables `ConsumerProcessing__<Key>`.

| Key | Rule |
|---|---|
| `RunId` | DNS label, length 1 to 29 |
| `ScenarioId` | one of `C03`, `C10`, `C01` |
| `Phase` | `C0` to `C9`, matching `^C[0-9]$` |
| `RepeatNumber` | integer 1 to 5 |
| `Role` | `subject` or `witness` |
| `JobName` | equals `<RunId>-<lower(Phase)>-r<RepeatNumber>-consumer-<Role>` |
| `PodName` | DNS label, length at most 63, starts with `<JobName>-` |
| `PodUid` | GUID in format `D` |
| `NodeName` | non-empty, no whitespace or control characters |
| `SourceCommit` | 40 lowercase hex characters |
| `NamespaceName` | DNS label, length at most 63, not containing `azure` (case-insensitive), as `SeedF04Options.cs` line 26 |
| `BlobEndpoint` | absolute `https` URI |

Each must also equal, by ordinal comparison, the value of its environment variable, and an unset variable is a mismatch: `PARCHWELL_SOURCE_COMMIT`, `PARCHWELL_JOB_NAME`, `PARCHWELL_POD_NAME`, `PARCHWELL_POD_UID`, `PARCHWELL_NODE_NAME`, as `Program.cs` lines 102 to 105 does for the seed. A failure of any rule is `consumer_identity_invalid`.

The instance ID is `PodName` (as the load generator's, `ProducerRunContext.cs` line 58).

Kafka authentication uses `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` and `AZURE_FEDERATED_TOKEN_FILE` as `Program.cs` lines 118 to 120. Blob access uses `WorkloadIdentityCredential` with a `BlobServiceClient` on `BlobEndpoint`, as `src/Parchwell.LoadGen/Program.cs` lines 389 to 390.

### 3.3 Processing plan (the seam `IConsumerProcessingPlanSource`)

The member gets everything a scenario decides through one interface at our own boundary (docs/engineering/dotnet.md "Seams and dependencies": scenario source). It returns a `ConsumerProcessingPlan` with these fields, validated on start; a failure is `consumer_plan_invalid`:

| Field | Rule |
|---|---|
| `ConfigurationId` | `^C[0-9]$` |
| `ProcessingDelayMs` | integer 0 to 60000; applied to every message both members process |
| `WindowMs` | integer 1000 to 3600000 |
| `SlowMessage` | absent, or: `Ordinal` integer 1 to 1000000, `DurationMs` integer 1 to 3600000, `Mode` `inline` or `pause` |
| `ConsumerSettings` | map of key to string value; keys only from the table below |

Rules across fields: `SlowMessage` present only when `Role` is `subject`; `Mode` `pause` only when the effective `enable.auto.offset.store` is `false`; `WindowMs` greater than `SlowMessage.DurationMs` when present; effective `max.poll.interval.ms` at least effective `session.timeout.ms`, both taken after the plan's `ConsumerSettings` are applied to the baseline (librdkafka rejects the opposite, `rdkafka_conf.c` line 4495). All of these are checked before the registration marker is written.

Consumer settings a plan may set, applied over `KafkaConfigurationFactory.CreateEnrichmentConsumer()` (`src/Parchwell.Kafka/Configuration/KafkaConfigurationFactory.cs` line 46). Any other key is `consumer_setting_not_supported`:

| Key | Value rule | Item |
|---|---|---|
| `session.timeout.ms` | integer 1 to 3600000 | C01 |
| `max.poll.interval.ms` | integer 1 to 86400000 | C03 |
| `enable.auto.offset.store` | `true` or `false` | C10 |

Bounds are librdkafka v2.16.0 CONFIGURATION.md lines 131, 137 and 140.

This pull request ships the interface, the plan type and its validation, and a test source. It ships no production source: in `Program.cs` the `--consumer-processing` mode resolves `IConsumerProcessingPlanSource` from DI, and no production registration exists until the child Q2 names adds one. Without a registration, the mode exits 1 with `consumer_plan_invalid` before registration and before any Kafka or Blob client is constructed.

### 3.4 Kafka client

- Group: `group.id` is `<RunId>-<lower(Phase)>-r<RepeatNumber>-consumers`, passed as `KafkaBaselineOptions.EnrichmentGroupId`. `client.id` is `PodName`.
- Topic: subscribe to `document-events` only.
- Effective settings: the baseline from `CreateEnrichmentConsumer()`, then the plan's `ConsumerSettings`, then `group.id`. These are the settings recorded in the registration marker.
- Handlers: `SetPartitionsAssignedHandler`, `SetPartitionsRevokedHandler`, `SetPartitionsLostHandler` and `SetOffsetsCommittedHandler` (Confluent.Kafka v2.16.0 `ConsumerBuilder.cs` lines 472 to 718), all set on the builder from `KafkaAuthenticationFactory.CreateConsumerBuilder` (`KafkaAuthenticationFactory.cs` line 138). No error handler changes behaviour; an error handler, if set, only logs.

### 3.5 Blob layout and record wire format

Container: `consumer-record` (ADR-022 "Durability"). Prefix `P` is `<RunId>/<Phase>/r<RepeatNumber>/consumer/<PodName>/`, with `PodName` escaped by `Uri.EscapeDataString`, as `ProducerRunContext.cs` line 20.

| Blob | Type | Written |
|---|---|---|
| `P` + `registration.json` | block blob, create-only (`IfNoneMatch = *`) | once, before the Kafka consumer is constructed |
| `P` + `record/` + six-digit zero-padded index + `.jsonl`, from `record/000000.jsonl` | append blob, created create-only before its first append | rows, in row-sequence order |
| `P` + `completion.json` | block blob, create-only | once, as the member's last action |

Record rules:
- Each Append Block call carries exactly one row: its UTF-8 JSON followed by one `\n` byte. A row is at most 65536 bytes including the `\n`; a larger row is `consumer_record_append_failed`.
- Each append sets the append-position condition to the blob's expected length (S2, `x-ms-blob-condition-appendpos`). Any failed append, a 412 included, is `consumer_record_append_failed`; there is no application retry.
- Roll-over: after 40000 appends to the current blob, the next row goes to a new blob with the next index. 40000 is below the 50,000-append limit (S2).
- `row_sequence` starts at 0 for the member and increases by 1 per row across blobs.

JSON: property names exactly as below, no other properties, `null` where stated. Times are UTC, written with format `yyyy-MM-ddTHH:mm:ss.fffffffZ` from `TimeProvider.GetUtcNow()`. Integers are JSON numbers.

Common to every row: `schema_version` (1), `kind`, `instance_id` (`PodName`), `row_sequence`.

| `kind` | Further properties |
|---|---|
| `processed` | `topic` (`document-events`), `partition`, `offset`, `producer_instance_id`, `attempt_id`, `key`, `sequence`, `slow` (bool), `processing_started_utc`, `processing_ended_utc` |
| `commit` | `source` (`callback` or `explicit`), `error_code` (int; 0 when no error), `error_reason` (string or `null` when no error), `offsets`: array of `{ "topic", "partition", "offset", "error_code" }` sorted by topic then partition, `event_utc` |
| `partition_event` | `event` (`assigned`, `revoked` or `lost`), `partitions` (array of ints, ascending), `event_utc` |
| `kill_intent` | `partition`, `offset`, `event_utc`. Defined here for #506 and #509; this pull request never writes it |

`processed` fields: `producer_instance_id` and `attempt_id` are the UTF-8 values of headers `instance-id` and `attempt-id` (`src/Parchwell.LoadGen/Run/Execution/RunExecutor.cs` lines 623 to 624); `sequence` is header `sequence` parsed as a base-10 Int64 (line 621); `key` is the message key decoded as UTF-8. Each is `null` when absent, duplicated, not valid UTF-8, or (for `sequence`) not a base-10 Int64. A `null` field does not stop the member; #506 invalidates the run (ADR-022 "Invalid run"). `slow` is `true` only for the message selected by `SlowMessage`.

`commit` fields: a `callback` row is one `OffsetsCommitted` handler call (Confluent.Kafka `CommittedOffsets`). Its `error_code` and `error_reason` are the result's overall `Error`. Its `offsets` hold every `TopicPartitionOffsetError` of the result, each with its own `Error.Code`. `offset` is the `Offset.Value` the client reports, unchanged (for example -1001 for unset). An entry is not filtered, merged or reordered beyond the sort. `explicit` is reserved for an item that already requires explicit commits (ADR-022 "Record"). No item in this batch requires one, so this pull request writes no `explicit` row and makes no `Commit` call.

Registration marker JSON: `schema_version` (1), `kind` (`consumer_registration`), `run_id`, `scenario_id`, `configuration_id`, `phase`, `repeat_number`, `role`, `instance_id`, `job_name`, `pod_name`, `pod_uid`, `node_name`, `source_commit`, `group_id`, `effective_settings` (object of every effective setting of 3.4, key to string value, keys in ordinal order), `registered_utc`. At most 65536 bytes.

Completion receipt JSON: `schema_version` (1), `kind` (`consumer_completion`), `run_id`, `scenario_id`, `configuration_id`, `phase`, `repeat_number`, `role`, `instance_id`, `job_name`, `pod_uid`, `source_commit`, `rows` (the number of rows written), `record_blobs` (the number of record blobs created), `messages_processed` (the number of `processed` rows), `closed_utc` (after `Close` returned). At most 65536 bytes.

### 3.6 Failure names

Written to standard error as one line `consumer_processing_failed reason=<name> scenario_id=<ScenarioId or unknown> run_id=<RunId or unknown> role=<Role or unknown>` before exit 1. Names: `consumer_identity_invalid`, `consumer_plan_invalid`, `consumer_setting_not_supported`, `consumer_registration_failed`, `consumer_record_append_failed`, `consumer_fatal_error`, `consumer_close_failed`, `consumer_completion_failed`.

### 3.7 Processing protocol

The member runs these steps in order:

1. Validate inputs and plan (3.2, 3.3). Write the registration marker. If that write fails, exit 1 with `consumer_registration_failed`; no Kafka client is constructed.
2. Construct the consumer and subscribe. The window clock starts when `Subscribe` returns. Start a watchdog that exits the process with code 124 at `WindowMs + 120000` ms after that instant.
3. Loop until the window ends: call `Consume` with a timeout of 100 ms. For each returned message, count it (the count includes messages buffered under step 5), then process it as in step 4 or 5.
4. Normal and `inline` processing, on the consume thread with no `Consume` call in between: record `processing_started_utc`, wait `ProcessingDelayMs` (or `SlowMessage.DurationMs` for the selected message) through `TimeProvider`, record `processing_ended_utc`, append the `processed` row, then, only when effective `enable.auto.offset.store` is `false`, call `StoreOffset` for that message.
5. `pause` processing of the selected message: pause every currently assigned partition, then call `Consume` with a 100 ms timeout until `SlowMessage.DurationMs` has elapsed since `processing_started_utc`. Messages returned meanwhile are buffered in order and counted. Then append the `processed` row, store its offset, resume every paused partition still assigned, and process the buffered messages in order under step 4 before the next `Consume`.
6. At window end, after the message in progress: call `Close()` within 30000 ms. The handlers that `Close` invokes (revoked, and offsets committed for librdkafka's final commit of the removed partitions; S1c) append their rows as at any other time. Then write the completion receipt and exit 0. The member never calls `Commit`, `Unsubscribe` or `Dispose` before `Close` (ADR-022 "Record": "No extra commit is made to generate evidence").

Handlers, which run on the thread that calls `Consume` or `Close` (Confluent.Kafka v2.16.0 `ConsumerBuilder.cs`, handler remarks):
- Assigned: append a `partition_event` row `assigned`; consume from committed offsets (no offsets returned).
- Revoked: append a `partition_event` row `revoked`. Make no commit. With `enable.auto.commit` `true`, librdkafka itself commits the stored offsets of removed partitions (S1c), and that result reaches the offsets-committed handler.
- Lost: append a `partition_event` row `lost`. Make no commit (K-28; `ConsumerBuilder.cs` `SetPartitionsLostHandler` summary: "The application should not commit offsets in this case"). librdkafka does not commit a lost assignment (S1c).
- In every case, buffered messages of a partition no longer assigned are dropped: no row, no store.
- Offsets committed: append a `callback` commit row.

## 4. Requirements

1. WHEN `--consumer-processing` starts with any 3.2 rule broken THEN it exits 1 with `consumer_identity_invalid`, writes no blob and constructs no Kafka client.
2. WHEN the plan source is not registered or returns a plan that breaks 3.3 THEN it exits 1 with `consumer_plan_invalid` (or `consumer_setting_not_supported` for a key outside the table), writes no blob and constructs no Kafka client.
3. WHEN the registration write fails, including because the blob exists THEN it exits 1 with `consumer_registration_failed` and constructs no Kafka client.
4. WHEN registration succeeds THEN the marker holds the 3.5 fields, and `effective_settings` equals the configuration passed to the consumer builder.
5. WHEN a message is processed with store-after-process THEN its `processed` row is appended before `StoreOffset` is called for it.
6. WHEN an append fails, in the loop or in a handler THEN the member makes no further `StoreOffset` call, appends no further row, writes no receipt, attempts `Close` within 30000 ms if it is not already closing, and exits 1 with `consumer_record_append_failed`.
7. WHEN effective `enable.auto.offset.store` is `true` THEN the member never calls `StoreOffset`.
8. WHEN `StoreOffset` throws `KafkaException` with `ErrorCode.Local_State` (partition not assigned; librdkafka `rdkafka.h` lines 4292 and 4264) THEN the member continues; any other `StoreOffset` exception exits 1 with `consumer_fatal_error`.
9. WHEN partitions are revoked THEN the member appends `revoked` and makes no `Commit` call.
10. WHEN partitions are lost THEN the member appends `lost` and makes no `Commit` call.
10a. WHEN the offsets-committed handler runs THEN the member appends one `callback` row holding every entry of the result. The member never calls `Commit` in any path.
11. WHEN `Consume` throws `ConsumeException` whose error is not fatal (for example `Local_MaxPollExceeded`, librdkafka `rdkafka.h` line 4444) THEN the member continues its loop. WHEN the error is fatal THEN it exits 1 with `consumer_fatal_error` without a receipt.
12. WHEN the selected slow message is processed in `inline` mode THEN no `Consume` call happens during its processing. In `pause` mode THEN `Consume` is called at least every 100 ms plus one `Consume` return during it, and every assigned partition is paused for its duration.
13. WHEN the window ends THEN the member closes, appends the rows of the handlers that `Close` invokes, writes the receipt, and exits 0. The receipt's `rows`, `record_blobs` and `messages_processed` equal the record's, counting the rows appended during `Close`.
14. WHEN `Close` throws or exceeds 30000 ms THEN no receipt is written and the exit is 1 with `consumer_close_failed`. WHEN the receipt write fails THEN the exit is 1 with `consumer_completion_failed`.
15. WHEN the current record blob has 40000 appends THEN the next row creates the next blob, and `row_sequence` continues.
16. WHEN a message's headers or key are absent or malformed THEN the row carries `null` for that field and processing continues.
17. WHEN the arguments are `--image-smoke`, `--single-action-seed` or `--validate-seed-configuration` THEN behaviour and output are unchanged; every existing test in `tests/Parchwell.Enrichment.Tests/SeedF04/` passes unchanged.

## 5. Invariants and safety rules

- The member writes to no Kafka topic (ADR-022 "No topic writes"). No producer is constructed in this mode.
- No secret, key, connection string, tenant ID or subscription ID enters the repository or the record (AGENTS.md hard safety rule 7). `effective_settings` holds client settings only; a test asserts it contains no `sasl.oauthbearer.token` value and no value read from `AZURE_FEDERATED_TOKEN_FILE`.
- No self-kill, no signal to any process, no process start (ADR-020; #509 owns the one exception).
- Confluent.Kafka is not wrapped; tests that need a seam use `IConsumer<byte[], byte[]>` directly (docs/engineering/dotnet.md "Seams and dependencies").
- Time from `TimeProvider` only; no `DateTime.UtcNow` (architecture test).
- No new project and no new project reference; slice isolation from `SeedF04/` holds (architecture tests).
- No Azure access in tests; Blob is replaced through the record-store seam (`IConsumerRecordStore`, our boundary: blob store).

## 6. Test map

Unit tests use a fake `IConsumer<byte[], byte[]>`, a fake `IConsumerRecordStore` and `FakeTimeProvider` or an equivalent test clock. Component tests use a pinned local broker: `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs` in full (lines 1 to 134 at the base, image `confluentinc/cp-kafka:7.5.12@sha256:997883c60e6f1207fb5f779c7a8fdae57312a17f446fd5ea3eb8b73f52d4a1cf` on line 13), copied into the test project with only its namespace changed, with a `document-events` topic of the partition count each row names, and the fake record store. Each component test method carries `[Trait("Tier", "Docker")]`, as `tests/Parchwell.LoadGen.Tests/Component/RunExecutorComponentTests.cs` line 21 does.

Start condition, used by every component test: each member's newest `partition_event` row is `assigned`, the two newest `assigned` rows are disjoint, and together they cover every partition. The test produces no message until it holds (ADR-022 "Start order"). Messages carry the load generator's headers (`instance-id`, `attempt-id`, `sequence`) and are produced to explicit partitions. Component tests run without OAUTHBEARER; the builder path for that is the implementer's choice inside the test project. Names follow `Given_<state>_When_<action>_Then_<outcome>`.

| Req | Test | Input | Literal expected values |
|---|---|---|---|
| 1 | U1 | each 3.2 key in turn set one character past its rule (RunId length 30; RepeatNumber 0 and 6; Role `observer`; JobName with `-subject` swapped for `-witness`; SourceCommit with one uppercase letter) | exit 1; stderr line starts `consumer_processing_failed reason=consumer_identity_invalid`; store calls 0; consumer factory calls 0 |
| 1 | U1b | RunId length 29, RepeatNumber 1 and 5 | validation passes |
| 2 | U2 | no plan source registered | exit 1; `reason=consumer_plan_invalid`; store calls 0; consumer factory calls 0 |
| 2 | U3 | `ConsumerSettings` `{"fetch.wait.max.ms":"100"}` | exit 1; `reason=consumer_setting_not_supported` |
| 2 | U4 | `ProcessingDelayMs` -1, 0, 60000, 60001; `WindowMs` 999, 1000; `SlowMessage` on witness; `Mode` `pause` with `enable.auto.offset.store` `true`; `WindowMs` 5000 with `DurationMs` 5000; `session.timeout.ms` 30000 with `max.poll.interval.ms` 29999, then 30000; `max.poll.interval.ms` 20000 alone (baseline session 30000) | invalid, valid, valid, invalid; invalid, valid; invalid; invalid; invalid; invalid, valid; invalid. Each invalid case: exit 1, `reason=consumer_plan_invalid`, store calls 0, consumer factory calls 0 |
| 3 | U5 | store throws on registration | exit 1; `reason=consumer_registration_failed`; consumer factory calls 0 |
| 4 | U6 | valid inputs, Role `subject`, Phase `C1`, RepeatNumber 2, RunId `run1` | marker path `run1/C1/r2/consumer/<PodName>/registration.json`; `group_id` `run1-c1-r2-consumers`; `effective_settings` equals builder configuration key for key; keys ordinal-sorted; JSON property set equals 3.5 exactly |
| 5 | U7 | 3 messages, store-after-process | call order per message: append `processed` then `StoreOffset(offset+1)`; rows `row_sequence` 1, 2, 3 after an `assigned` row 0 |
| 6 | U8 | append fails on the 2nd `processed` row | `StoreOffset` called once (1st message only); `Commit` calls 0; no receipt; `Close` called once; exit 1; `reason=consumer_record_append_failed` |
| 6 | U8b | 1 `processed` row, then the fake store fails the append of the `revoked` row that the revoked handler makes inside a `Consume` call | no `StoreOffset` call after the failure; no further row appended; no receipt; `Close` called once; exit 1; standard error line starts `consumer_processing_failed reason=consumer_record_append_failed` |
| 7 | U9 | `enable.auto.offset.store` `true`, 3 messages | `StoreOffset` calls 0; 3 `processed` rows |
| 8 | U10 | `StoreOffset` throws `Local_State` on message 1, then `ErrorCode.Local_Fail` on message 2 | after message 1 the loop continues; after message 2 exit 1 `reason=consumer_fatal_error` |
| 9, 10a | U11 | revoke of partitions [3, 1], then an offsets-committed result with entries for partitions 3 and 1 (partition 3 with error `Local_State`) and overall error none | rows: `partition_event` `revoked` `partitions` [1, 3]; then `commit` `source` `callback`, `error_code` 0, `offsets` in order partition 1 (`error_code` 0) and partition 3 (`error_code` -172); `Commit` calls 0 |
| 10 | U12 | lost of [0, 2] then window end | `Commit` calls 0 in the whole run; rows: `partition_event` `lost` `partitions` [0, 2] |
| 11 | U13 | `Consume` throws non-fatal `Local_MaxPollExceeded` once, then returns 1 message | loop continues; 1 `processed` row; exit 0 |
| 11 | U14 | `Consume` throws with `IsFatal` true | exit 1; `reason=consumer_fatal_error`; no receipt |
| 12 | U15 | `inline`, Ordinal 2, DurationMs 10000, delay 0 | `Consume` call count during the 10000 ms is 0; row 2 `slow` true, `processing_ended_utc - processing_started_utc` = 10000 ms on the test clock |
| 12 | U16 | `pause`, Ordinal 1, DurationMs 1000; 2 messages returned while paused | `Pause` once with all assigned partitions before the first in-pause `Consume`; at least 10 `Consume` calls during the 1000 ms; `Resume` once after; then the 2 buffered messages processed in order, rows `slow` true, false, false |
| 12 | U17 | `pause`, a buffered message's partition lost during the pause | that message gets no row and no `StoreOffset` |
| 13 | U18 | window end after 2 messages; the fake `Close` invokes the revoked handler and then the offsets-committed handler once | rows appended during `Close`: `revoked`, then `commit` `callback`; the receipt is written after `Close` returned; receipt `rows` = total rows including those two, `record_blobs` 1, `messages_processed` 2; `Commit` calls 0; exit 0 |
| 14 | U19 | `Close` throws; separately `Close` blocks 30001 ms; separately receipt write fails | exit 1 with `consumer_close_failed`, `consumer_close_failed`, `consumer_completion_failed`; receipt absent in the first two |
| 15 | U20 | 40001 rows through the Blob store against a fake Blob client | blob `record/000000.jsonl` gets appends 1 to 40000, `record/000001.jsonl` gets append 40001; `row_sequence` 40000 is the first row of blob 1; each append carries the expected append position |
| 15 | U21 | one row of 65536 bytes including `\n`; one of 65537 | accepted; `consumer_record_append_failed` |
| 16 | U22 | message with no `attempt-id`; with `sequence` `12x`; with key bytes `0xFF` | rows with `attempt_id` null; `sequence` null; `key` null; exit 0 |
| 3.1 | U25 | the test clock passes `WindowMs + 120000` ms after `Subscribe` while `Close` blocks | the watchdog's exit action is invoked once with code 124; no receipt |
| 3.5 | U23 | one row of each kind written, `kill_intent` built by the serializer only | each row's JSON property set equals 3.5 exactly; times match `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$`; no production path writes `kill_intent` |
| 5 | U24 | `effective_settings` with a token file present | no setting value equals the token file content; no key `sasl.oauthbearer.token` |
| 5, 13 | CT1 | 6 partitions; two members, delay 0, `WindowMs` 60000; after the start condition, 100 messages produced to each partition | union of `processed` rows covers every produced offset exactly once; each member has a receipt. After both members exit, a test-only client calls `Committed` for the group's 6 partitions (as ADR-022 "Independent commit evidence" describes, outside the worker). For every partition, the returned offset is 100, and it equals the `offset` of at least one entry with `error_code` 0, in a `commit` row with `error_code` 0, in either member's record (ADR-022 "Commit agreement") |
| 11, 12, 10 | CT2 (K-55 case 1) | 2 partitions; `session.timeout.ms` 6000, `max.poll.interval.ms` 10000 on both members; subject `inline`, Ordinal 11, DurationMs 20000. After the start condition, 10 messages per partition, then a wait of 7000 ms (above `auto.commit.interval.ms` 5000), then 1 message per partition. P is the partition of the subject's newest `assigned` row before its slow row | the subject's slow row has partition P and offset 10. The subject has a `lost` row with `row_sequence` above the slow row's. The witness has an `assigned` row that includes P, with `event_utc` at least 9500 ms after the slow row's `processing_started_utc` (the poll limit, not the 6000 ms session timeout, removed the subject) and before its `processing_ended_utc`. Partition P offset 10 appears in `processed` rows of both members. The only commit rows are `callback` rows |
| 12 | CT3 (K-55 case 2) | same as CT2, with subject `pause` | the subject's slow row has partition P and offset 10; the subject has no `revoked` or `lost` row with `row_sequence` between its newest `assigned` row before the slow row and the first row appended during its `Close`; partition P offset 10 has exactly one `processed` row across both members; the witness has no `assigned` row that includes P with `event_utc` between the slow row's `processing_started_utc` and the subject's receipt `closed_utc` |
| 9, 10a | CT4 | 2 partitions; the subject starts alone, and after its `assigned` row covers both partitions, 5 messages per partition are produced; 1000 ms later the witness starts | the subject has a `revoked` row, then a `callback` commit row with a higher `row_sequence` whose entries include both revoked partitions, each with `error_code` 0 and `offset` 5 |

Boundary note: CT2 and CT3 use a local broker with `group.min.session.timeout.ms` at its Kafka default of 6000; they test the harness, not Event Hubs (ADR-016; docs/engineering/dotnet.md "Testing"). They are the component evidence decision 6894564479 point 1 calls for; the live observation and verdict rule stay with #508.

Existing tests: `tests/Parchwell.Enrichment.Tests/SeedF04/SeedF04HostTests.cs` and `SeedF04Tests.cs` stay unchanged and pass (requirement 17). This plan adds checks only to the new mode, so no existing fixture is rejected. Standing fixture rule, in case one is: when an existing test's fixture is rejected by a new check, the implementer does exactly one of (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; or (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. It never removes a case, loosens an assertion or changes a check. The pull request's implementation notes list each such change with the check that forces it; the cold review and Codex review check each one. Model: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

## 7. Deployment assumptions

No test in this pull request runs on AKS, Event Hubs or Azure Storage. Each item below is evidence-free until #508's smoke run, and is listed under Q3 in section 11 until Hari accepts the risk by name.

- D1. The enrichment-consumer identity can create and append blobs in `consumer-record` with `WorkloadIdentityCredential`. Evidence: none taken; the role is #507's (ADR-022 "Durability"). The load generator's equivalent path works for `ledger` (B06 and B07 runs), on fresh pods, under load; that is a different identity and container.
- D2. #507 sets every 3.2 key and the `PARCHWELL_*` values, including `PARCHWELL_NODE_NAME` from the pod's node. Evidence: none; #507's plan must cite 3.2.
- D3. Fresh groups per repeat stay under Event Hubs' limit of 1,000 Kafka consumer groups per namespace (S5). Evidence: documentation only, no observation. The batch's count of repeats is set by #508 and #510, so the count is not known here.
- D5. The enrichment-consumer identity can join a Kafka consumer group on Event Hubs and commit its offsets. Evidence: results C08 and C12 exist under `results/` for single-action subscribes in the `EnrichmentConsumer` role. Whether those ran on this identity and committed offsets was not checked for this plan. Not observed on a fresh group under load.
- D4. One Append Block per row is fast enough that a member's processing rate meets the item plans' workloads. Evidence: none; ADR-022 "Consequences" says the effect is not measured.

## 8. Acceptance commands

Run on the pull request branch.

1. Design-authority check: `git fetch origin main && git diff 09c5bfc384dc516e7b9a8a53d38dee30d40755df origin/main -- docs/blueprint.md`. Evidence: empty output, or, if not empty, a statement in the implementation notes that no changed line is in ADR-022 (lines 735 to 794 at the base). If ADR-022 changed, stop (section 12).
2. `./scripts/verify-dotnet.sh --project Parchwell.Enrichment.Tests` while work moves (docs/agents/pull-requests.md "Verification gates" step 1). Evidence: pass counts, cited as a partial run.
3. Cold review under docs/agents/pull-requests.md rule 8 (gate step 2).
4. `./scripts/verify-dotnet.sh --tier sandbox` once on the reviewed source (gate step 3). Evidence: the summary, recorded as a partial run excluding `Tier=CGroupV2`.
5. `git diff --name-only origin/main...HEAD`. Evidence: only section 2's paths.
6. A script that lists every row ID U1, U1b, U2 to U8, U8b, U9 to U25 and CT1 to CT4 with the test in `tests/Parchwell.Enrichment.Tests/ConsumerProcessing/` that covers it and that test's pass result from the step 4 run. Evidence: 31 lines, all passed (a test may cover several rows; the script maps each row ID to at least one test).
7. `npm run check:docs`. Evidence: the summary line.
8. Hosted `.NET verification` on the final head (gate step 4), Codex review, and the Claude review gate of decision 6894587233 point 2.

## 9. Sources

External behaviour (plan-pr "Plan rules" rule 1). Tools: raw GitHub fetches at the pinned tags, and the Microsoft Learn documentation search tool, all on 2026-11-15.

- S1. librdkafka v2.16.0 [CONFIGURATION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md): line 131 `session.timeout.ms` (heartbeats; broker removes a member without them), line 137 `max.poll.interval.ms` ("the consumer is considered failed and the group will rebalance"; "Offset commits may be not possible at this point"), line 140 `enable.auto.offset.store` ("Automatically store offset of last message provided to application").
- S1b. librdkafka v2.16.0 [INTRODUCTION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md) line 2529: "KIP-62 - max.poll.interval and background heartbeats ... Supported". This is the source decision 6894564479 asked for: heartbeats run in the background, separately from the poll limit.
- S1c. librdkafka v2.16.0 [rdkafka.h](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.h): lines 4264 and 4292, storing offsets for non-assigned partitions fails with `RD_KAFKA_RESP_ERR__STATE`; line 4444, `RD_KAFKA_RESP_ERR__MAX_POLL_EXCEEDED` is returned by poll; lines 4642 to 4645, a synchronous commit's callback "will be enqueued for a future call to rd_kafka_poll(), rd_kafka_consumer_poll() or similar" (Confluent.Kafka bypasses this, S1d). [rdkafka_assignment.c](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_assignment.c) lines 503 to 512: with `enable.auto.commit=true`, offsets of removed partitions are committed when the condition on line 509, `!rd_kafka_destroy_flags_no_consumer_close(rk)`, holds. Reading that condition as "skip only when the consumer is destroyed without a close", so that `Close` and a revoke do commit, is inference from the code. The comment on lines 503 to 505 reads ambiguously alone. [rdkafka_cgrp.c](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_cgrp.c) lines 4750 to 4776: that commit is skipped when the assignment is lost ("not committing assigned offsets: assignment lost"), and its result goes to the application's `offset_commit_cb`. [rdkafka.c](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.c) lines 5329 to 5346: `rd_kafka_consumer_close` serves callbacks ("Handle callbacks", `rd_kafka_poll_cb`) until its TERMINATE op. Line 5178 forwards the reply queue to the group queue once the consumer is set to poll, which Confluent.Kafka does (`Consumer.cs` line 779). That the final commit's callback arrives before TERMINATE is inference from this code, not a documented guarantee; CT1 and U18 cover the worker's side of it. [rdkafka_conf.c](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_conf.c) line 4495: `max.poll.interval.ms` must be at least `session.timeout.ms`.
- S1d. Confluent.Kafka v2.16.0 [ConsumerBuilder.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ConsumerBuilder.cs) lines 472 to 718: handlers run on the `Consume`, `Close` or `Dispose` thread; the lost-handler summary says not to commit. [Consumer.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Consumer.cs): line 349, commit callbacks are skipped once the handle is closed; lines 470 to 491, `StoreOffset` stores `offset + 1` and throws `KafkaException` with the partition's error; line 599, `Close` "commits offsets and unsubscribes". [SafeKafkaHandle.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Impl/SafeKafkaHandle.cs) lines 926 to 953: `Commit` uses a temporary queue and a dummy callback "to prevent the global offset_commit_cb to kick in", so explicit commits never reach the offsets-committed handler (ADR-022 "Record", S50).
- S3. `docs/blueprint.md` at the base, ADR-022 lines 735 to 794. The bullets this plan implements: "Group" 750, "Start order" 751, "Registration" 752, "Record" 753, "Durability" 754, "No topic writes" 755, "Stop and observation boundary" 769, "Commit agreement" 771 (for CT1), "Configuration fidelity" 774, "Invalid run" 775.
- S2. Azure Storage REST API [Append Block](https://learn.microsoft.com/rest/api/storageservices/append-block#remarks): "A maximum of 50,000 appends are permitted for each append blob"; `x-ms-blob-condition-appendpos` fails with 412 when the offset differs; "Append Block succeeds only if the blob already exists".
- S5. [Event Hubs quotas](https://learn.microsoft.com/azure/event-hubs/event-hubs-quotas): "Number of Kafka consumer groups per namespace" 1,000 on Premium; Kafka consumer group name up to 256 characters.

Not verified, so not relied on: whether `Pause` discards messages already fetched is not documented in the sources read; 3.7 step 5 buffers any message returned while paused instead of relying on it.

Repository: `src/Parchwell.Enrichment/Program.cs`, `SeedF04/*.cs`, `Parchwell.Enrichment.csproj`; `tests/Parchwell.Enrichment.Tests/Parchwell.Enrichment.Tests.csproj`; `src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs` lines 139 to 186, `KafkaConfigurationFactory.cs`, `KafkaScenarioOverrideSetting.cs` lines 122 to 155; `src/Parchwell.Kafka/Authentication/KafkaAuthenticationFactory.cs` lines 130 to 151; `src/Parchwell.LoadGen/Run/Publication/ProducerRunContext.cs`, `BlobProducerRunLifecycle.cs`; `src/Parchwell.LoadGen/Run/Execution/RunExecutor.cs` lines 616 to 626; `src/Parchwell.Scenarios/Contracts/ScenarioPhases.cs`; `docs/engineering/dotnet.md`; `docs/blueprint.md` ADR-005, ADR-012, ADR-014, ADR-016, ADR-017 and section 3 at the base.

## 10. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Effort that scales with a number unknown at planning time: none in the implementation. Test counts are fixed by section 6. U20 writes 40001 rows to a fake store; that number is fixed.

(b) Where this plan is stricter than, or goes beyond, its authorities:
- Commit on revoke. The blueprint's section 3 lists "commit on partition revoke" as `Parchwell.Kafka` behaviour. This plan makes no explicit commit there, because ADR-022 "Record" allows none made for evidence. librdkafka's auto-commit of removed partitions gives the same effect (S1c). This follows ADR-022 rather than going beyond it, and is listed so the checker can confirm the reading.
- Roll-over at 40000 appends, not "before 50,000". A margin choice; ADR-022 allows it.
- One row per Append Block and the append-position condition. ADR-022 does not pin them; they make a gap or a duplicate append visible.
- `null` for malformed headers rather than stopping the member. ADR-022 puts the consequence on the verifier.
- Slow-message fault (`SlowMessage`) and its two modes. ADR-022 says the subject "carries the configuration's fault" but does not define the C03 fault. Question Q2.
- The three-key settings allowlist. ADR-022 does not list which settings vary; the three serve C01, C03 and C10's questions (blueprint section 12 rows C01, C03, C10).
- `node_name` in the registration marker. ADR-022 "Group" says each run records both members' node names; this pins where.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in this pull request:
- The verifier's group-offset read (#506) is what makes commit rows authoritative. CT1 imitates it with a test-only client; the worker does not depend on it.
- The production plan source and scenario files (Q1), which no test here needs: tests use the test source.
- #506's reading of 3.5 and #507's setting of 3.2 (D2): contracts this plan sets for them, not dependencies of its tests.
- D1, D3 and D4 in section 7.

## 10a. Deferred work

1. Production `IConsumerProcessingPlanSource`. Evidence: no scenario file for C03, C10 or C01 exists at the base (`scenarios/` lists none), `ScenarioPhases.cs` has no entry for them, and the profile schema has no processing delay (`src/Parchwell.Scenarios/Loading/ProfileReader.cs` line 26). On the verdict path: yes; no live run can start without it. Why deferral is permitted: the item data it reads belongs to the scenario children, and this pull request cannot run live before #507. Responsible: the child Q1 names. Stage: before #508's smoke run. Completion evidence: a merged source with tests, and `--consumer-processing` resolving a C03 plan in-process. Gate it blocks: #508's smoke run.
2. Kill-intent writing and SIGKILL (#509). Not deferred work of this plan; it is #509's entry. Listed so a reader does not take its absence for a gap.

## 11. Open questions for Hari

Each question carries a recommended answer, under point 2 of decision 6895535291. The control session records adoption in a `## Decision` on #505. Q1 assigns work to a later child, which may be a split change outside that decision's point 4, so the control session decides whether Q1 needs Hari himself.

- Q1. Where the processing plan comes from. ADR-014 says each worker resolves its configuration in-process. docs/engineering/dotnet.md "Kubernetes workers" says each worker "resolves its selected configuration in-process through Parchwell.Scenarios". No scenario file, profile field or phase entry exists for C03, C10 or C01, and no split row names that schema work. Recommended: #505 ships the seam (3.3) without a production source. #508 (C03 scenario) adds the production source with the scenario-schema and profile changes it needs, and #510 extends it for C10 and C01. Alternative: #505 also adds the Parchwell.Scenarios schema, with fixture scenarios. That needs a project reference from `Parchwell.Enrichment` to `Parchwell.Scenarios`, another P1 under the Code Review Rules.
- Q2. The C03 subject fault. Recommended: one slow message, selected by its ordinal among the messages `Consume` returns to the subject (the same counting as ADR-022's kill point). It is processed either `inline`, with no `Consume` calls (K-55 case 1), or under `pause`, which pauses and keeps calling `Consume` (K-55 case 2). Alternative: a delay distribution from the profile, which the profile schema does not have.
- Q3. Deployment assumptions D1 to D5 (section 7). Recommended: accept each by name. #508's smoke run is the first evidence for D1, D2, D4 and D5, and #508's and #510's plans count repeats against D3.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it would need to change any contract in section 3, a requirement, the file scope, a test's literal expected value, or when acceptance command 1 finds ADR-022 changed since the base.

## 13. Size estimate

Estimate, 2026-11-15, from the size of the comparable load generator lifecycle files (`BlobProducerRunLifecycle.cs` 263 lines, `ProducerRunContext.cs` 65 lines) and the seed slice (145 lines): about 900 added lines of production code in about 10 files, about 1300 added lines of tests in about 6 files, plus 4 changed project and lock files and `items/505/state.md`. About 21 files. Size is reported, not enforced.
