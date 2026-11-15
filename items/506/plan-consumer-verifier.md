# Plan: consumer verifier (#506)

Issue: [#506](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506), child of [#499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499), split entry 3. Pull request name: `consumer-verifier`. This is the contract-grade plan for that one pull request under docs/agents/factory.md "Item lifecycle" step 3.

Revision 2. Base: `main` at `09c5bfc384dc516e7b9a8a53d38dee30d40755df`, the merge of the predecessor pull request #518 (ADR-022). File paths and line numbers are on that commit unless a line says otherwise.

This pull request adds to the verifier the ADR-022 consumer judgement for C03, C10 and C01. A new `consumer-verify` mode verifies the producer side exactly as `verify` does, then reads both consumer members' records from Blob, makes one independent read of the group's committed offsets, and writes a consumer outcome: four classes when the repeat is complete and valid, otherwise the ADR-022 invalid or incomplete reasons. It also extends the `start-line` mode with a start-assignment snapshot for these items. Revision 2 answers the [first plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6899498280); section 14 lists the changes. Section 11 holds eight questions, each with a recommended answer, under point 2 of [decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291).

## 1. Goal and authority

Goal: when #507 integrates the consumer Jobs, the verifier can judge a C03, C10 or C01 repeat under ADR-022 with no further verifier change, apart from the production plan source that the scenario children add (section 10a). Unit tests cover every ADR-022 predicate at its boundaries, including cases D1 to D16 of the approved ADR plan, adapted as section 6 states. Component tests prove the offset reader against a local broker.

Design authority: ADR-022, docs/blueprint.md lines 735 to 797 on the base, with ADR-012 (lines 369 to 431), ADR-013 (433 to 468), ADR-016 (514 to 535) and ADR-019 (584 to 621).

Authorities on #506, in creation order (factory.md "Item lifecycle" step 3, child issues):

1. Issue body: `Parent items: #499`, `Split entry: 3`, `Predecessor PRs: the pull request of #504 (entry 1)`. Scope: "The verifier's join of the ADR-022 consumer record with the produce ledger, its outcome classes and invalid-run conditions, with the unit and component tests ADR-016 requires. Protected path (verifier); Hari merges." Planning by Claude; implementation by Codex, in a session Hari starts.
2. [Decision 6895300183](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6895300183) adopts #499 [decision 6895299617](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617) (grilling round 1).
3. [Decision 6895329001](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6895329001) adopts #499 [decision 6895328446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446) (round 2).
4. [Decision 6895343921](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6895343921) adopts #499 [decision 6895343227](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895343227) (round 3).
5. [Decision 6895536845](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6895536845) adopts the standing #499 [decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291): the #347 control session coordinates; grilling questions are written with recommended answers, adopted in advance; plan approval on a fresh check with `Blocking findings: none` and section 11 reading `None`; its point 4 limits.

Where rounds 1 to 3 differ from ADR-022 as merged (for example round 2's "newest successful commit" completion rule), ADR-022 on `main` governs, because docs/blueprint.md is the design authority (AGENTS.md) and Hari approved its text on #504 ([approval 6898353773](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6898353773)). This plan applies no round 1 to 3 text that ADR-022 replaced.

Parent decisions read as context (the #506 body names them): #499 [6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441), [6894564479](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894564479), [6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233) (split approved; the Claude review gate of #347 decision 6633528258 point 2 applies to each Codex pull request) and [6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673) (Claude plans; Codex implements).

Evidence, not authority: [proposal revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894560135), "Proposed split" row 3; the approved [consumer-adr plan, revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/8a0fe6b7e6df9d4889ace18d7483a1ebc66c8487/items/504/plan-consumer-adr.md), section 6.1 (cases D1 to D16) and section 10a item 2 (offset-reader component proof), which its section 10a says #506 "must adopt"; the unchecked [consumer-worker draft, revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/6cef974528c02c1d7bad87e6676d6a18ce848a1b/items/505/plan-consumer-worker.md), section 3.5, whose blob layout section 3.2 below follows where ADR-022 allows.

Predecessor: #518 (the #504 pull request) merged as `09c5bfc384dc516e7b9a8a53d38dee30d40755df` on 2026-11-15; #504 is closed. No item state file exists (`items/506/` is absent on the base). Implementing tool: Codex (decision 6894614673 point 2; [tool comment 6895573282](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895573282) on #499).

## 2. Scope

Files this pull request may add or change:
- `src/Parchwell.Verifier/VerifyRun/Consumer/**` (new folder in the `VerifyRun` slice).
- `src/Parchwell.Verifier/StartLine/**`: the consumer start snapshot (3.6) only.
- `src/Parchwell.Verifier/Program.cs`, `VerifierHostOptions.cs`, `VerifierHostOptionsValidator.cs`, `VerifierWorker.cs`: the new mode, its options and validation only.
- `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs`: the repeat-result extension seam of 3.8 only. With no extension registered, as in `verify`, its behaviour and output are unchanged.
- `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` and `Common/Results/VerifierResult.cs`: the optional `consumer` block (3.8) only.
- `src/Parchwell.Verifier/Common/**` other files: only where a type must be shared between `StartLine` and `VerifyRun` (architecture rule, `tests/Parchwell.ArchitectureTests/CompiledArchitectureTests.cs` lines 54 to 80).
- `tests/Parchwell.Verifier.Tests/Consumer/**` and `tests/Parchwell.Verifier.Tests/Component/Consumer/**` (new).
- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`: by the `renew` command only (acceptance command 5).
- `tests/Parchwell.ArchitectureTests/**`: one rule that the pure judgement namespace uses no Confluent.Kafka type (5, last bullet).
- `items/506/state.md` (factory.md "Item state file").

Must not change: every existing mode's behaviour and output; `src/Parchwell.Verifier/Common/Phase/PhaseIdentity.cs` and `src/Parchwell.Scenarios/Contracts/ScenarioPhases.cs` (other modes keep rejecting C01, C03 and C10); the 24 C5 target files listed in `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`; `stryker-c5-config.json`, `c5_runtime.py`, `c5_workflow.py`; any `*.csproj`, `Directory.*.props` or lock file; `src/Parchwell.Kafka/**`, `src/Parchwell.Scenarios/**`, `src/Parchwell.Enrichment/**`, `src/Parchwell.LoadGen/**`; `scenarios/`, `results/`, `docs/`, `charts/`, `infra/`, `.github/`, `scripts/`; `AGENTS.md`, `CLAUDE.md`, `README.md`.

Out of scope, owned elsewhere: the record writer (#505); the chart, Jobs, environment values, role assignments and the order of steps (#507); scenario files, schema fields and the production plan source (section 10a; Q2); self-termination (#509).

No new project, assembly reference or NuGet package: the verifier already references Confluent.Kafka, Azure.Storage.Blobs and Testcontainers (its test project). No protected-path exception beyond the verifier itself, which Hari merges.

## 3. External contracts

Everything in this section is read or written by #505, #507, a scenario child or the evidence pack. The implementer copies it exactly.

### 3.1 Scope of the consumer modes

- Scenario IDs: `C01`, `C03`, `C10` only. Phase: `C0` or `C1`. Configuration ID: equal to the phase. Repeat: 1 to 5. Expected load generator: one instance, Job name `PhaseIdentity.JobName(RunId, Phase, Repeat)` (`Common/Phase/PhaseIdentity.cs`), as for B04, B06 and B07 (`VerifierHostOptions.cs` lines 60 to 68). These are the phases `PhaseIdentity.IsPhase` accepts (line 12) that `ScenarioPhases` maps to themselves for B04, B06 and B07 (`src/Parchwell.Scenarios/Contracts/ScenarioPhases.cs` lines 14 to 16) (Q8).
- `start-line` accepts these scenarios, phases and configuration IDs in addition to the ones it accepts today, and for them also writes the snapshot of 3.6. For them the validator does not consult `ScenarioPhases`; it applies the rules of this section instead. Every other rule `start-line` applies today is unchanged.
- New mode `Verifier:Mode = consumer-verify` (exact, case-sensitive) accepts only these scenarios.
- `verify` and every other existing mode keep rejecting `C01`, `C03` and `C10` with their existing validation failure.
- Repeat prefix: `PhaseIdentity.RepeatPrefix(RunId, Phase, Repeat)` (`Common/Phase/PhaseIdentity.cs` lines 76 to 77), `<RunId>/<Phase>/r<Repeat>/`. Every other path is the `PhasePaths` path `verify` uses for the same identity.
- Consumer group ID: `<RunId>-<lower(Phase)>-r<Repeat>-consumers`.

### 3.2 Consumer record blobs (read; #505 writes)

Container `consumer-record` (ADR-022 "Durability"). Member prefix `M` = repeat prefix + `consumer/` + `Uri.EscapeDataString(PodName)` + `/`. The instance ID is the pod name.

| Blob | Content |
|---|---|
| `M` + `registration.json` | registration marker |
| `M` + `record/` + six-digit zero-padded index + `.jsonl`, from `record/000000.jsonl` | rows, one JSON object and one `\n` byte per row, in row-sequence order |
| `M` + `completion.json` | completion receipt |

Members are discovered by listing `<repeat prefix>consumer/` and taking each `registration.json`.

Common rules, all enforced on read:
- UTF-8 JSON; property names exactly as below; no other property; no duplicate property. Times: UTC strings matching `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$`. Integers are JSON numbers.
- Each row is at most 65536 bytes including its `\n`. Each record blob ends with `\n`. A blob holds at most 40000 rows. Record blob indices are contiguous from `000000`; every blob before the last holds exactly 40000 rows.
- `row_sequence` is 0 for the member's first row and increases by 1 per row across blobs.

Every row has `schema_version` (1), `kind`, `instance_id` (equals the member's pod name), `row_sequence`, and:

| `kind` | Further properties |
|---|---|
| `processed` | `topic` (`document-events`), `partition` (int, at least 0), `offset` (int, at least 0), `producer_instance_id`, `attempt_id`, `key` (each a string or `null`), `sequence` (Int64 or `null`), `slow` (bool), `processing_started_utc`, `processing_ended_utc` |
| `commit` | `source` (`callback` or `explicit`), `outcome` (`callback` rows: `callback`; `explicit` rows: `returned`, `partition_exception` or `request_exception`), `error_code` (int, 0 when no error), `error_reason` (string, or `null` when `error_code` is 0), `submitted` (`callback` rows: `null`; `explicit` rows: array of `{ "topic", "partition", "offset" }`), `offsets` (array of `{ "topic", "partition", "offset", "error_code" }`), `event_utc` |
| `partition_event` | `event` (`assigned`, `revoked` or `lost`), `partitions` (array of distinct ints, ascending; may be empty), `event_utc` |
| `kill_intent` | `partition`, `offset`, `event_utc` |

Commit-row meaning, from ADR-022 "Record": `returned` is a successful explicit call, and `offsets` holds the entries the call applied to, each with `error_code` 0. `partition_exception` holds the exception's result list, with top-level `error_code` 0. `request_exception` holds an empty `offsets` and a nonzero `error_code`. A `callback` row is one automatic commit callback. A successful entry is one with `offset` at least 0, entry `error_code` 0 and row `error_code` 0. A row whose fields contradict its `outcome` (for example `request_exception` with `error_code` 0 or a nonempty `offsets`) is malformed.

Registration marker: `schema_version` (1), `kind` (`consumer_registration`), `run_id`, `scenario_id`, `configuration_id`, `phase`, `repeat_number`, `role` (`subject` or `witness`), `instance_id`, `job_name`, `pod_name`, `pod_uid`, `node_name`, `source_commit`, `group_id`, `effective_settings` (object, key to string value, keys in ordinal order), `registered_utc`. At most 65536 bytes.

Completion receipt: `schema_version` (1), `kind` (`consumer_completion`), `run_id`, `scenario_id`, `configuration_id`, `phase`, `repeat_number`, `role`, `instance_id`, `job_name`, `pod_uid`, `source_commit`, `rows`, `record_blobs`, `messages_processed`, `closed_utc`. At most 65536 bytes.

This plan owns this format (Q1). #505's next revision cites this section instead of restating it.

### 3.3 Options

The new mode binds the options `verify` binds today, with the same keys and rules, except that the scenario, phase and configuration rules are those of 3.1, plus:

| Key | Rule |
|---|---|
| `Verifier:Mode` | `consumer-verify` |
| `Verifier:ConsumerRecordContainer` | exactly `consumer-record` |
| `Verifier:ScenarioId` | `C01`, `C03` or `C10` |
| `Verifier:Phase` | `C0` or `C1` |
| `Verifier:ConfigurationId` | equal to `Verifier:Phase` |

`start-line` for C01, C03 or C10 also requires `Verifier:ConsumerRecordContainer`, with the same rule. A rule failure is a validation failure on start (exit 1), as for the existing keys (`VerifierHostOptionsValidator.cs` lines 13 to 232).

### 3.4 Consumer verification plan (the seam `IConsumerVerificationPlanSource`)

Everything the scenario decides reaches the verifier through one interface at our own boundary (docs/engineering/dotnet.md "Seams and dependencies": scenario source). For a scenario ID and configuration ID it returns a `ConsumerVerificationPlan`:

| Field | Rule |
|---|---|
| `KillCount` | absent, or integer 1 to 1000000 |
| `CompletionDeadlineMs` | integer 1000 to 3600000 |
| `SubjectSettings` | expected effective settings of the subject: map of key to string |
| `WitnessSettings` | expected effective settings of the witness: map of key to string |

Neither settings map may contain `bootstrap.servers`, `client.id` or `group.id`; those are checked by rule 6 of section 4. Both maps must contain `isolation.level`, with equal values; that value is the group's `isolation.level` for the offset reader (3.5). A plan that breaks a rule, or a source that has no plan for the pair, is a validation failure on start (exit 1, standard error line `consumer_plan_invalid`), before any Blob or Kafka client is constructed. This pull request ships the interface, the plan type, its validation and a test source, and no production registration (Q2).

### 3.5 Offset reader

A consumer built through the verifier's existing authentication path (`VerifierKafkaSessionFactory.cs` lines 13 to 45), with:
- the configuration of `KafkaConfigurationFactory.CreateVerifierConsumer()`, then `group.id` set to the repeat's consumer group ID (3.1), `enable.auto.commit` `false`, and `isolation.level` set to the value both settings maps hold (3.4);
- no `Subscribe`, `Assign`, `Consume`, `Commit` or `StoreOffset` call;
- exactly one call `Committed(partitions, timeout)`, then `Dispose`. `partitions` is every partition of the run window (the start-line note's partitions), topic `document-events`, ascending. `timeout` is the time left until the deadline instant of rule 10 of section 4, and the call is not made if that is zero or less.

Read outcome names: `success`; `kafka_exception` (a `KafkaException` that is not a `TopicPartitionOffsetException`); `partition_exception` (`TopicPartitionOffsetException`); `invalid_response` (a returned list whose length differs from the request, or with a partition missing, repeated or on another topic); `not_attempted` (no time left under rule 10, or rule 2 or rule 3 of section 4 applies).

### 3.6 Consumer start snapshot (written by `start-line`)

For C01, C03 and C10, `start-line` does, in this order: (1) list members and read every record row of each (first read); (2) read the high watermarks, as today; (3) read every record row of each member again (second read); (4) write `consumer-start.json` create-only to the verifier container at the repeat prefix. A failure to read the consumer record fails the step with exit 1 and no start line, as an existing start-line read failure does. The snapshot JSON:

`schema_version` (1), `kind` (`consumer_start_snapshot`), `group_id`, `first_read_started_utc`, `second_read_ended_utc`, `members`: array sorted by `instance_id` of `{ "instance_id", "role", "first_read_last_row_sequence", "second_read_last_row_sequence" }`, where a member with no row has `-1`. Times are on the verifier's clock.

The snapshot records; it does not judge. `consumer-verify` judges it under rule 5 of section 4.

### 3.7 Reason names

Invalid reasons: `producer_side_not_complete_valid`, `consumer_registration_invalid`, `consumer_record_invalid`, `consumer_start_assignment_invalid`, `consumer_processed_outside_window`, `consumer_processed_unjoined`, `consumer_stop_invalid`, `consumer_configuration_mismatch`, `consumer_commit_record_disagreement`.

Incomplete reasons: `consumer_commit_evidence_unavailable`, `consumer_completion_deadline`, `consumer_commit_frontier_short`.

### 3.8 Result: the `consumer` block

`consumer-verify` runs the `verify` handler (`VerifyRun/VerifyRunHandler.cs`) with one repeat-result extension registered. The extension is an interface at our own boundary in the `VerifyRun` slice. The handler calls it once, after it has built the root judgement and before it writes `result.json`, with the root status, the run window, the ledger rows and the verifier-consumed records. The extension returns the `consumer` block and its status. With no extension registered (`verify`), the handler is unchanged. `result.json` keeps the codec of `verify` (`Common/Results/VerifierResultCodec.cs`), and every existing field is computed exactly as without the extension. One optional root field is added, `consumer`, written only when the extension is registered:

| Property | Value |
|---|---|
| `schema_version` | 1 |
| `status` | `complete_valid`, `invalid` or `incomplete` |
| `reasons` | array of 3.7 names, ordinal order, no duplicates; empty only when `status` is `complete_valid` |
| `group_id` | 3.1 |
| `kill_count` | integer, or `null` when the plan has none |
| `members` | array sorted by `role`, of `{ "role", "instance_id", "node_name", "rows", "processed_rows", "ending" }`, `ending` one of `completion_receipt`, `kill_intent`, `none`; empty when no registration could be read |
| `offset_read` | `{ "outcome", "requested_partitions", "started_utc", "ended_utc", "error_code", "offsets" }`; `offsets` array of `{ "partition", "offset", "error_code" }` as returned; `started_utc`, `ended_utc` and `error_code` are `null` when `outcome` is `not_attempted` |
| `partitions` | array by partition of `{ "partition", "start", "end", "observed" }`, `observed` an integer (`-1001` for `Offset.Unset`) or `null` without a successful read |
| `counts` | only when `status` is `complete_valid`: `{ "processed_once", "processed_again", "processed_again_extra_rows", "committed_never_processed" }` |
| `reassignment` | only when `status` is `complete_valid` and `kill_count` is not `null`: `{ "microseconds", "unavailable_reason" }`, one of them `null`; `unavailable_reason` is `different_nodes` or `no_reassignment` |
| `diagnostics` | array of `{ "reason", "member", "artifact", "row_sequence", "field", "detail" }`, at most 64 entries, `detail` at most 256 bytes |

`consumer-verify` exits 0 only when the root `status` and the consumer `status` are both `complete_valid`; otherwise it writes the result and exits 1 (ADR-013 "For an invalid or incomplete run"). It writes the `verify-completion.json` receipt at the path, with the kind (`verify`) and fields that `verify` writes (`VerifyRunHandler.cs` lines 197 to 203), except that the receipt's exit code is the process exit code of this paragraph.

## 4. Requirements

Definitions. Window: `[S[p], E[p])` per partition from the start-line note and the end line. Delivered in-window messages: each verifier-consumed record in the window joined to a ledger row with `Delivered` true, as `verify` joins them (`VerifyRun/Judgement/OutcomeClassifier.cs` lines 9 to 42); each is identified by `(partition, offset)`. R[p] and B[p] as ADR-022 "Commit agreement". Deadline instant: the time `consumer-verify` captured its end line, plus `CompletionDeadlineMs`, on the verifier's `TimeProvider`.

1. WHEN `consumer-verify` runs THEN the `verify` handler performs every step it performs today up to its result, with identical root fields; then the extension runs rules 2 to 14; then the handler writes one `result.json` and the receipt of 3.8.
2. WHEN the root `status` is not `complete_valid` THEN the consumer `status` is `invalid` with reason `producer_side_not_complete_valid`, the consumer artifacts are still read for diagnostics, and the offset read is `not_attempted`.
3. Registration. WHEN the number of registration markers is not 2, or their roles are not one `subject` and one `witness`, or a marker breaks 3.2, or a marker's `run_id`, `scenario_id`, `configuration_id`, `phase`, `repeat_number` or `group_id` differs from the options and 3.1, or its `pod_name` differs from its prefix's pod name or from `instance_id`, THEN reason `consumer_registration_invalid`. If the markers read are not exactly two that each meet 3.2, one `subject` and one `witness`, rules 4 to 11, 13 and 14 are skipped, the offset read is `not_attempted`, and rule 12 sets the status.
4. Record. WHEN a member has no `record/000000.jsonl`, a non-contiguous blob index, a blob before the last with other than 40000 rows, a row that breaks 3.2, a `row_sequence` gap or repeat, or a completion receipt whose `rows`, `record_blobs` or `messages_processed` differ from its record, or whose `run_id`, `scenario_id`, `configuration_id`, `phase`, `repeat_number`, `role`, `instance_id`, `job_name`, `pod_uid` or `source_commit` differ from its registration marker, THEN reason `consumer_record_invalid`.
5. Start assignment. Held set of a member at a row sequence k: apply its `partition_event` rows with `row_sequence` at most k in order (`assigned` sets it to the listed partitions; `revoked` and `lost` remove the listed partitions). WHEN `consumer-start.json` is missing or breaks 3.6, or a member's snapshot `instance_id` set differs from the registered members, or a member has no `assigned` row with `row_sequence` at most its `first_read_last_row_sequence` (ADR-022 "Start order": both members "hold an assignment"; an assignment may be empty), or either member has a `partition_event` row with `row_sequence` greater than its `first_read_last_row_sequence` and at most its `second_read_last_row_sequence`, or some window partition is in neither member's held set at its `first_read_last_row_sequence`, or in both, THEN reason `consumer_start_assignment_invalid`.
6. Configuration fidelity. WHEN a member's `effective_settings`, without `bootstrap.servers`, `client.id` and `group.id`, differ in any key or value from `SubjectSettings` (subject) or `WitnessSettings` (witness), or its `group.id` differs from 3.1, or its `client.id` differs from its `instance_id`, or its `bootstrap.servers` differs from the verifier's own `bootstrap.servers`, THEN reason `consumer_configuration_mismatch`, with one diagnostic per differing key.
7. Processed rows. WHEN a `processed` row's `(partition, offset)` is outside the window THEN reason `consumer_processed_outside_window`. WHEN a `processed` row inside the window has a `null` identity field, or no ledger row with its `producer_instance_id` and `attempt_id`, or a `key` or `sequence` that differs from that ledger row, or its `(partition, offset)` is not a verifier-consumed record with the same instance and attempt IDs, THEN reason `consumer_processed_unjoined`.
8. Stop. With `KillCount` N: WHEN the subject's last row is not a `kill_intent` row, or it has more than one `kill_intent` row, or it has a completion receipt, or its number of `processed` rows differs from N - 1, THEN reason `consumer_stop_invalid`. Without `KillCount`: WHEN the subject has a `kill_intent` row or no completion receipt THEN `consumer_stop_invalid`. In both cases: WHEN the witness has a `kill_intent` row or no completion receipt THEN `consumer_stop_invalid`.
9. Offset read. WHEN rules 3 to 8 have been evaluated, rules 2 and 3 do not skip the read, and the deadline instant has not passed THEN the verifier makes the read of 3.5 exactly once, whatever rules 4 to 8 found, and records it in `offset_read`. WHEN the outcome is `kafka_exception`, `partition_exception` or `invalid_response` THEN reason `consumer_commit_evidence_unavailable`.
10. Deadline. WHEN the read outcome is `success` and its end time is later than the deadline instant THEN reason `consumer_completion_deadline`. An end time equal to the deadline instant meets the deadline. WHEN the deadline instant has passed before the read THEN the read is `not_attempted` and the reason is `consumer_completion_deadline`.
11. Agreement and completion, after a `success` read within the deadline, per partition p of the window: WHEN B[p] is at least 0 and is not in R[p], or B[p] is `Offset.Unset` and R[p] is not empty, or B[p] is negative and not `Offset.Unset`, THEN reason `consumer_commit_record_disagreement`. Otherwise, WHEN S[p] is less than E[p] and (B[p] is `Offset.Unset` or B[p] is less than E[p]) THEN reason `consumer_commit_frontier_short`. A partition with S[p] equal to E[p] needs nothing more.
12. Status. WHEN any invalid reason (3.7) applies THEN `status` is `invalid` and `reasons` lists every invalid reason found, and no incomplete reason. Otherwise WHEN an incomplete reason applies THEN `status` is `incomplete` and `reasons` holds it. At most one incomplete reason can apply: rule 9's reason needs a failed read, rule 10's a late or `not_attempted` read, and rule 11's frontier reason a `success` read within the deadline. Otherwise `status` is `complete_valid`. `counts` and `reassignment` are written only for `complete_valid`.
13. Classes, for `complete_valid` only, over delivered in-window messages with n = the number of `processed` rows across both members with that `(partition, offset)`: n = 1 is `processed_once`; n > 1 is `processed_again`, adding n - 1 to `processed_again_extra_rows`; n = 0 is `committed_never_processed`. (In a complete run every such message has offset below E[p], at most B[p] - 1, so ADR-022's "not reached" class cannot occur; rule 11 reports it as `consumer_commit_frontier_short`.) `processed` rows for messages the ledger records as failed are not classified.
14. Reassignment (C01 timing), for `complete_valid` with `KillCount`: WHEN the two members' `node_name` values differ THEN `unavailable_reason` is `different_nodes`. Otherwise let t_s be the time of the subject's last row (`event_utc`, or `processing_ended_utc` for a `processed` row) and H the subject's held set at its last row; WHEN the witness has an `assigned` row with time at least t_s and partitions that include every partition of H THEN `microseconds` is the floor of (the earliest such row's time minus t_s) in microseconds; otherwise `unavailable_reason` is `no_reassignment`.
15. `start-line` for C01, C03 and C10 writes 3.6; for every other scenario its output is unchanged.
16. Every other mode, and `start-line` for every other scenario, behaves and writes exactly as before; every existing test passes unchanged.

## 5. Invariants and safety rules

- The verifier writes nothing to Kafka: the offset reader commits nothing and joins no group (ADR-022 "Independent commit evidence"). The ordinary verification consumer keeps group `parchwell-verifier` and its settings.
- No new role or identity scope: the verifier reads `consumer-record` with the Storage Blob Data Reader role ADR-022 gives it, and writes only to its own container, create-only (ADR-012).
- Counts are never written for an invalid or incomplete consumer status, and are never zero-filled (ADR-013).
- No change to a verdict, an expected outcome or `results/` (AGENTS.md "Verdict integrity").
- No secret or identifier in the repository (hard safety rule 7). Diagnostics never copy a setting value whose key contains `sasl` or `token`.
- Time from `TimeProvider` only (architecture test, `tests/Parchwell.ArchitectureTests/CompiledArchitectureTests.cs` lines 84 to 96).
- Confluent.Kafka is not wrapped; the offset reader is behind a delegate seam at our boundary, as `VerifierKafkaSessionFactory(Func<...>)` is (line 48). The pure judgement (rules 3 to 14) lives in a namespace with no Confluent.Kafka type, enforced by an architecture test like the Kafka-type rules at `CompiledArchitectureTests.cs` lines 98 to 116.

## 6. Test map

Unit tests: xUnit, no I/O, fake stores and a fake offset reader, `FakeTimeProvider` or the existing test clock. Names follow `Given_<state>_When_<action>_Then_<outcome>`. Base fixture B unless a row says otherwise: one partition, S = 100, E = 110, a `complete_valid` producer side with ten delivered in-window messages at offsets 100 to 109, a valid snapshot, two valid members on node `n1`, the subject processing offsets 100 to 104 and the witness 105 to 109 once each, no `KillCount`, both with receipts, `CompletionDeadlineMs` 600000, settings equal to the plan, and a `success` read returning B = 110 with R = {110}.

ADR plan cases (from the approved ADR plan, section 6.1; D9 maps its error kinds to the read outcomes of 3.5, and D14 restates its case as a call-order test, because `consumer-verify` reads only after every member artifact is loaded):

| Test | Input | Expected |
|---|---|---|
| D1 | subject row 90 reports 115; witness row 2 reports 110; B = 110; then names, sequences, callback times and blob order reversed | `complete_valid` both times; `partitions[0].observed` 110 |
| D2 | R = {109}, B = 109; R = {110}, B = 110; R = {111}, B = 111 | `incomplete` [`consumer_commit_frontier_short`]; `complete_valid`; `complete_valid` |
| D3 | each member reports 110; B = 110 | `complete_valid` |
| D4 | R = {109}, B = 110; add 110 with row `error_code` 0 and entry `error_code` 1; replace with row `error_code` 1 and entry 0; replace with a fully successful 110 | `invalid` [`consumer_commit_record_disagreement`] three times; then `complete_valid` |
| D5 | R = {}, B = -1001 | `incomplete` [`consumer_commit_frontier_short`] |
| D6 | S = E = 0, and separately S = E = 100; no messages; R = {}; B = -1001 | `complete_valid` both |
| D7 | S = E = 100; R = {100}, B = 100; R = {99}, B = 99; R = {101}, B = 101 | `complete_valid` each |
| D8 | S = E = 100; R = {}, B = 0; R = {100}, B = -1001; R = {100}, B = 101 | `invalid` [`consumer_commit_record_disagreement`] each |
| D9 | read outcome `kafka_exception` with `Local_TimedOut`; `kafka_exception` with an authorization error; `invalid_response` missing the partition; `invalid_response` with it twice; `partition_exception`; each also with S = E = 100 | `incomplete` [`consumer_commit_evidence_unavailable`] each; no `counts` |
| D10 | B = -2, R = {} | `invalid` [`consumer_commit_record_disagreement`] |
| D11 | p0 S 100 E 110 R {110} B 110; p1 S = E = 50 R {} B -1001; then p1 B = 0 | `complete_valid`; then `invalid` [`consumer_commit_record_disagreement`] |
| D12 | read ends at deadline instant minus 1 tick, at it, at it plus 1 tick | `complete_valid`; `complete_valid`; `incomplete` [`consumer_completion_deadline`] |
| D13 | a `row_sequence` gap plus read `kafka_exception`; then valid records with R = {109}, B = 109 | `invalid` [`consumer_record_invalid`]; `incomplete` [`consumer_commit_frontier_short`]; no `counts` in either |
| D14 | snapshot of the fake reader's call order: the read is made only after every record, receipt and kill-intent row was loaded; a second read is never made | exactly one `Committed` call, after the last artifact read; a fake that would return B = 110 earlier is never consulted |
| D15 | callback success at 109; `explicit` `returned` row for 110; B = 110; separately an `explicit` `returned` row with empty `submitted` and `offsets` | `complete_valid`, R = {109, 110}; the empty row adds no entry |
| D16 | B = 110; the only successful entry is 109 | `invalid` [`consumer_commit_record_disagreement`]; `committed_never_processed` absent |

Boundary tests for the other predicates (docs/engineering/dotnet.md "Testing": at, below and above each boundary, and every empty input):

| Req | Test | Input | Expected |
|---|---|---|---|
| 1, 16 | U1 | one C03 fixture through the `verify` handler with no extension registered and with the consumer extension registered | every root field byte-equal; only `consumer` added; with no extension, `result.json` and the receipt equal what the handler writes on the base for the same inputs |
| 2 | U2 | producer side `invalid`; separately `incomplete` | consumer `invalid` [`producer_side_not_complete_valid`]; `offset_read.outcome` `not_attempted`; read call count 0 |
| 3 | U3 | 0, 1, 2, 3 registration markers; two subjects; marker `repeat_number` 2 for repeat 1; `group_id` changed by one character | `consumer_registration_invalid` except at exactly 2 valid; with 0, 1 or 3 markers, read call count 0 |
| 4 | U4 | blob 0 holds 39999, 40000, 40001 rows with a blob 1 after it; indices 000000 and 000002; `row_sequence` 0, 1, 3; 0, 1, 1; receipt `rows` one more than the record; a row of 65536 and 65537 bytes; an unknown property; a missing `\n` at blob end; an empty record (no blob) | `consumer_record_invalid` in each case except 40000 and 65536 bytes |
| 5 | U5 | snapshot missing; witness snapshot values 3 and 5 with a `partition_event` row at sequence 3, 4, 5 and 6 in turn; two partitions with one in neither held set; one in both; witness snapshot values `-1` and `-1`; witness whose only `assigned` row has an empty `partitions` list and sequence 0, with snapshot values 0 and 0 | `consumer_start_assignment_invalid` for missing, for 4, for 5, for neither, for both, for `-1`; valid for 3, for 6 and for the empty assignment |
| 6 | U6 | subject `max.poll.interval.ms` differs; witness has one extra key; subject lacks one key; `client.id` differs; `group.id` differs; the plan's `isolation.level` used by the reader | `consumer_configuration_mismatch` with one diagnostic per key; reader configuration has the plan's `isolation.level`, `enable.auto.commit` `false` and the 3.1 group |
| 7 | U7 | processed offsets 99, 100, 109, 110; `attempt_id` `null`; no ledger row; `sequence` off by one; `(partition, offset)` consumed by the verifier with another attempt ID | `outside_window` for 99 and 110; valid for 100 and 109; `unjoined` for the other four |
| 8 | U8 | N = 3: subject with 1, 2 and 3 processed rows then `kill_intent`; `kill_intent` not last; two `kill_intent` rows; `kill_intent` plus receipt. No N: subject with `kill_intent`; subject without receipt. Witness without receipt; witness with `kill_intent` | `consumer_stop_invalid` in each case except 2 processed rows then `kill_intent` |
| 9 | U9 | invalid record plus `success` read | read call count 1; `invalid` with no incomplete reason |
| 10 | U10 | deadline already passed before the read | read call count 0; `offset_read.outcome` `not_attempted`; `incomplete` [`consumer_completion_deadline`] |
| 11 | U11 | R entry from a `request_exception` row at 110; a `partition_exception` row with its 110 entry `error_code` 0 and row `error_code` 0; B = 110 | `invalid` [`consumer_commit_record_disagreement`]; `complete_valid` |
| 12 | U12 | `consumer_record_invalid` and `consumer_stop_invalid` together; R = {109}, B = 109 with the read ending 1 tick after the deadline instant | `reasons` both invalid names in ordinal order; `incomplete` [`consumer_completion_deadline`] only |
| 13 | U13 | offset 103 processed by both members; offset 104 by none; offset 105 three times; a failed ledger row consumed at 108 with a processed row | `processed_once` 6, `processed_again` 2, `processed_again_extra_rows` 3, `committed_never_processed` 1; offset 108 unclassified |
| 13 | U13b | delivered in-window messages: none (S = E) | `counts` all 0 |
| 14 | U14 | N = 3; subject last row at t; witness `assigned` covering H at t - 1 tick, t, t + 1 ms; covering all but one partition of H; nodes `n1` and `n2` | `no_reassignment`; 0; 1000; `no_reassignment`; `different_nodes` |
| 3.4 | U15 | `KillCount` 0, 1, 1000000, 1000001; `CompletionDeadlineMs` 999, 1000, 3600000, 3600001; `SubjectSettings` with `group.id`; `WitnessSettings` without `isolation.level`; `isolation.level` `read_committed` and `read_uncommitted` in the two maps; no plan for the pair; no source registered | invalid, valid, valid, invalid; invalid, valid, valid, invalid; invalid; invalid; invalid; invalid; invalid; each invalid case exits 1 with `consumer_plan_invalid` and constructs no client |
| 3.1, 3.3 | U16 | `consumer-verify` with `ScenarioId` `B10`; Phase `C3`, `C2`, `probe`, `c1`; Phase `C1` with `ConfigurationId` `C0`; `ConsumerRecordContainer` `ledger`; `verify` with `C03`; `consumer-verify` with `C03`, `C0`, `C0` | each a validation failure, exit 1, except the last, which passes |
| 15 | U17 | `start-line` for `C03` with two members; for `B10` | snapshot written with the four sequence values and both times, read order first-read, watermarks, second-read; for `B10` no consumer read and an unchanged note |
| 3.8 | U18 | each status | `consumer` property set exactly as 3.8; `counts` only for `complete_valid`; `reasons` ordinal; diagnostics capped at 64 entries and 256 bytes; no `sasl` or `token` value |
| 3.8 | U19 | exit code for root and consumer status pairs | 0 only for `complete_valid` and `complete_valid` |

Component tests (`[Trait("Tier","Docker")]`, `tests/Parchwell.Verifier.Tests/Component/LocalKafkaFixture.cs`, a 2-partition `document-events` created through the fixture's direct listener). These are the premerge proof required by the approved ADR plan, section 10a item 2; they test the harness, not Event Hubs (ADR-016; docs/engineering/dotnet.md "Testing"):

| Test | Input | Expected |
|---|---|---|
| CT1 | two member clients in group `run1-c1-r1-consumers` subscribe, receive no messages, close; then the offset reader reads both partitions | outcome `success`; both offsets -1001 |
| CT2 | 10 messages produced to partition 0; the two members consume, one commits offset 7 explicitly, both close; then the offset reader reads | outcome `success`; partition 0 offset 7; partition 1 -1001; the group has no member after the read; group `parchwell-verifier` committed offsets unchanged |
| CT3 | the reader built against `ProducerBootstrapServers` (the proxy listener) after `ApplyFaultAsync(SpikeFault.Timeout, ...)`, with a 1 ms timeout | outcome `kafka_exception`; one `Committed` call |

Existing tests: rule 16 requires every existing test to pass unchanged. This plan adds checks only to the new mode and to `start-line` for the three new scenarios, so no existing fixture should be rejected. Standing fixture rule, in case one is: when an existing test's fixture is rejected by a new check, the implementer does exactly one of (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; or (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. It never removes a case, loosens an assertion or changes a check. The pull request's implementation notes list each such change with the check that forces it; the cold review and Codex review check each one. Model: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

## 7. Deployment assumptions

No test in this pull request runs on AKS, Event Hubs or Azure Storage. Each item is listed as Q7 in section 11 until a decision by Hari accepts it by name.

- DA1. `ConsumerGroupOffsetRead`: the verifier identity can read the repeat group's committed offsets from Event Hubs without joining the group. Evidence: none live; source evidence for the client path only (S1). Hari accepted this risk for the ADR on #504 ([approval 6898353773](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6898353773)) with local proof here and Event Hubs proof at #508's smoke stage; that acceptance is not yet adopted on #506. The intended observation is a fresh connection, after both consumers stopped, idle.
- DA2. The verifier identity can list and read `consumer-record` (ADR-022 "Durability"; #507 grants the role). Evidence: none taken; the analogous `ledger` read works on fresh pods, under load (B06 and B07 runs).
- DA3. Two members on one node read the same clock, so rule 14's difference is not distorted by clock offset. Evidence: none; inference from both pods sharing the node's kernel clock.

## 8. Acceptance commands

Run on the pull request branch, in the order docs/agents/pull-requests.md "Verification gates" sets.

1. `./scripts/verify-dotnet.sh --project Parchwell.Verifier.Tests` while work moves (gate step 1). Evidence: pass counts, cited as a partial run.
2. Cold review under docs/agents/pull-requests.md rule 8 (gate step 2), with the implementation notes and any fixture changes.
3. `./scripts/verify-dotnet.sh --tier sandbox` once on the reviewed source (gate step 3). Evidence: the summary, recorded as a partial run excluding `Tier=CGroupV2`.
4. A script that maps every test ID of section 6 (D1 to D16, U1 to U19, U13b, CT1 to CT3) to at least one test in `tests/Parchwell.Verifier.Tests/Consumer/` or `Component/Consumer/` and its result in the step 3 run. Evidence: 39 lines, all passed.
5. Mutation inputs: commit the change, then `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py renew --head <HEAD_SHA> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json --basis "<text>"` (docs/operations/verifier-mutation.md, renewal paragraph), commit the JSON, then `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base <BASE_SHA> --head <HEAD_SHA> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`. The basis states that no C5 target changed, that the added unit tests add runtime to every mutant's full test run (verifier-mutation.md: "each ID still runs once in a fresh process with the full test population"), and that the risk is accepted pending the post-merge acceptance run. Evidence: `qualified` or `provisional` with `renewed_inputs`. If `renew` refuses because a target source changed, stop (section 12).
6. `git diff --name-only origin/main...HEAD`. Evidence: only section 2's paths.
7. `npm run check:docs`. Evidence: the summary line.
8. Hosted `.NET verification` on the final head (gate step 4), Codex review, and the Claude review gate of decision 6894587233 point 2.

## 9. Sources

External behaviour (plan-pr "Plan rules" rule 1). Tool: raw GitHub file fetches at the pinned commits, 2026-11-15.

- S1. Confluent.Kafka v2.16.0, commit `56ef87df3402861f0dc72ef921fcfac702ae1f16` (blueprint S50): [IConsumer.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/IConsumer.cs#L534) lines 534 to 563, `Committed(partitions, timeout)`: "The offset field of each requested partition will be set to the offset of the last consumed message, or Offset.Unset in case there was no previous message", throwing `KafkaException` or `TopicPartitionOffsetException`; [Impl/SafeKafkaHandle.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/Impl/SafeKafkaHandle.cs#L1104) lines 1104 to 1133: one native `committed` call over the given list; a request error throws `KafkaException`; any partition error throws `TopicPartitionOffsetException`; otherwise the list is returned; [Offset.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/Offset.cs#L36) lines 36 and 56: `Offset.Unset` is -1001.
- S2. librdkafka v2.16.0 `rd_kafka_committed` and the OffsetFetch path without a joined member: blueprint S51, as ADR-022 "Independent commit evidence" cites it. Not re-read in this session; this plan relies on it only through ADR-022's text.
- S3. Azure Storage Append Block, 50,000 appends per append blob: blueprint S49. This plan's 40000-row blob rule is a harness margin under that limit, as in the #505 draft.

Repository, on the base: docs/blueprint.md ADR-012, ADR-013, ADR-016, ADR-019, ADR-022; `src/Parchwell.Verifier/Program.cs` lines 39 to 51 and 108 to 293; `VerifierHostOptions.cs` lines 7 to 18 and 157 to 176; `VerifierHostOptionsValidator.cs` lines 13 to 409; `VerifierWorker.cs` lines 67 to 171; `VerifyRun/VerifyRunHandler.cs`; `VerifyRun/Window/WindowConsumer.cs` lines 20 to 70; `VerifyRun/Input/ConsumedRecord.cs`, `CapturedArtifacts.cs`, `LedgerReader.cs`; `VerifyRun/Judgement/OutcomeClassifier.cs`, `ConfigurationFidelityCheck.cs`; `Common/Results/VerifierResultCodec.cs`; `Common/Window/StartLineNoteCodec.cs`; `Common/Phase/PhaseIdentity.cs`, `PhasePaths.cs`; `Common/Kafka/VerifierKafkaSessionFactory.cs`; `StartLine/StartLineHandler.cs`; `src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs` lines 139 to 205; `tests/Parchwell.Verifier.Tests/Component/LocalKafkaFixture.cs`, `ComponentRunHarness.cs`; `tests/Parchwell.ArchitectureTests/`; `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`, `c5_runtime.py`; `docs/operations/verifier-mutation.md`; docs/engineering/dotnet.md "Testing", "Seams and dependencies"; `.github/CODEOWNERS`. Read through two reader subagents and checked by this session at the places the plan cites.

## 10. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Effort that scales with a number unknown at planning time: none. Test counts are fixed by section 6; U4 writes at most 40001 rows to a fake store. Mutation acceptance is one post-merge run of the existing workflow, outside this pull request (section 10a item 2).

(b) Where this plan is stricter than, or adds to, its authorities:
- Rule 7's last clause: a processed row's `(partition, offset)` must be the verifier-consumed record with the same IDs. ADR-022 joins on IDs and classifies by partition and offset; without this, the two keys could disagree and a message would be classed by one and joined by the other.
- Rule 8's N - 1 processed rows: round 1 point 7 ("the subject ends other than at the fixed point"), adopted on #506, and ADR-022 "Self-termination" (the kill happens when `Consume` returns the Nth message, before it is processed).
- Rule 5's snapshot (3.6) and the no-partition-event interval: the evidence for ADR-022's "at the start line, a partition was assigned to no member or to both", without comparing clocks across pods (Q3).
- Rule 6 checks `bootstrap.servers`, `client.id` and `group.id` separately from the plan maps, because they are per member or per run.
- Rule 12 reports one incomplete reason, in a fixed order. ADR-022 names the reasons but not their combination.
- 40000 rows per blob and 65536 bytes per row: the #505 draft's limits, which this plan adopts as the reader contract (Q1).

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in this pull request:
- The writer's format (3.2): #505 is not implemented; this plan sets the format (Q1). Tests use fixture records built to 3.2.
- The production plan source and scenario files (Q2); tests use a test source.
- #507's step order (start-line after both members hold an assignment; `consumer-verify` after the load generator and both consumer Jobs have terminated), its options, and one value: #507 gives the consumer members and the verifier the same namespace input, so their `bootstrap.servers` are equal (`src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs` line 47). These are contracts this plan sets for #507, not dependencies of its tests. If #507 broke the last one, rule 6 would invalidate the run; it could not produce a wrong count.
- DA1 to DA3 in section 7.

## 10a. Deferred work

1. Production `IConsumerVerificationPlanSource`, with scenario schema fields for `KillCount`, `CompletionDeadlineMs` and the consumer overrides `max.poll.interval.ms` and `enable.auto.offset.store`, which `KafkaScenarioOverrideSetting.cs` lines 141 to 147 do not allow today. The same child links `scenarios/<ID>/scenario.yaml` into `src/Parchwell.Verifier/Parchwell.Verifier.csproj` as `Content`, as the existing scenarios are, because `consumer-verify` loads the scenario definition for the producer side as `verify` does (`VerifyRun/VerifyRunHandler.cs` lines 109 to 112). Evidence: no C01, C03 or C10 scenario exists on the base; `ScenarioContracts.cs` has no such fields. On the verdict path: yes. Why deferral is permitted: the data belongs to the scenario children, and no live run can happen before #507. Responsible: the child Q2 names. Stage: before #508's smoke run. Completion evidence: a merged source with tests and `consumer-verify` resolving a C03 plan. Gate: #508's smoke run.
2. Mutation acceptance. This pull request changes C5 shared inputs, so a passing `verifier-c5-mutation.yml` run dispatched on `main` after it merges is required before any consumer evidence pack is published (factory.md "Evidence pack"). On the verdict path: yes. Responsible: the session that dispatches it after the merge, with Hari's approval of the run. Completion evidence: `summary.json` with `measurement_complete: true` and `break_passed: true` naming a `main` commit that contains this merge. Gate: the C03, C10 and C01 evidence packs. Exhaustion: a failed or incomplete run opens a fix issue (factory.md "Evidence pack").
3. Event Hubs proof of DA1 and DA2: #508's smoke stage, as the approved ADR plan section 10a item 2 sets. On exhaustion `record the gap and continue`; consumer counts stay unpublished.
4. Optional, not on the verdict path: adding the consumer judgement files as C5 mutation targets (Q6).

## 11. Open questions for Hari

Each carries a recommended answer under point 2 of decision 6895535291. The control session records adoption in a `## Decision` on #506.

- Q1. Owner of the record wire format. ADR-022 names the row contents but not the bytes; #505's draft (unchecked) and this plan both need them. Recommended: this plan's section 3.2 is the format; #505's next revision cites it and writes to it. It follows the #505 draft's layout, with the commit-row fields ADR-022 now requires (`outcome`, `submitted`, `topic` per entry). Alternative: #505 owns it, and this plan is revised to match its approved revision.
- Q2. Where the verifier gets the kill count, completion deadline and expected consumer settings. No scenario schema holds them. Recommended: the seam of 3.4 with no production source in this pull request; the C03 scenario child (#508) adds the source and schema, and #510 extends it for C10 and C01, as the #505 draft's Q2 recommends for the worker. Alternative: this pull request also changes `Parchwell.Scenarios` and `Parchwell.Kafka`, which are outside entry 3.
- Q3. Evidence for "at the start line every partition is assigned to exactly one member". Recommended: the `start-line` snapshot of 3.6 and rule 5, which compare row sequences, not clocks. Alternative: compare member `event_utc` times with the start-line note's `captured_at`, which needs a named clock assumption across pods and a tolerance nobody has measured.
- Q4. Anchor of the completion deadline. ADR-022 gives the deadline but not its start. Recommended: from `consumer-verify`'s own end-line capture, on the verifier's clock, to the end of the offset read. #507 invokes `consumer-verify` only after both consumer Jobs terminated, and bounds that wait itself. Alternative: from the start-line note's `captured_at`, which crosses pods and clocks.
- Q5. C01 reassignment timing in this pull request. The issue body names the join, classes and invalid-run conditions, not timing; ADR-022 "C01 timing" is verifier work. Recommended: include it (rule 14), so no later pull request reopens the protected verifier for it. Alternative: defer it to #510.
- Q6. Mutation coverage of the new judgement. docs/engineering/dotnet.md's mutation row covers only the C5 target files; the matrix is at its 256-job capacity, and adding targets needs new measurement and a protected workflow change. Recommended: do not add targets in this pull request; renew the qualification (acceptance command 5); the boundary tests of section 6 stand. Alternative: add targets, which needs a separate measured change.
- Q7. Deployment assumptions DA1 to DA3 (section 7). Recommended: accept each by name; for DA1, adopt the #504 approval's acceptance with its staged proof and publication gate.
- Q8. Phases and configuration IDs for C01, C03 and C10. No scenario file exists, and the verifier's phase and configuration tables have no entry for them. Recommended: phases `C0` and `C1`, each with a configuration ID equal to its phase, which `PhaseIdentity` already accepts and which keeps every existing phase path unchanged; a scenario child that needs a third configuration amends this plan's 3.1. Alternative: widen `PhaseIdentity` to `C2` to `C9` now, which touches every mode's validation.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it would need to change any contract in section 3, a requirement, the file scope, a test's literal expected value, or a C5 target source, or when `renew` refuses the change.

## 13. Size estimate

Estimate, 2026-11-15, from comparable verifier slices on the base (`VerifyRun/Abrupt/B10AbruptVerifyHandler.cs` 379 lines, `AbruptEvidenceReader.cs` 288 lines, `Common/Window/StartLineNoteCodec.cs` 279 lines): about 1600 added lines of production code in about 14 files, about 2600 added lines of tests in about 10 files, about 60 changed lines in 4 existing host files, and the qualification JSON and `items/506/state.md`. About 30 files. Revision 2 adds about 40 lines to `VerifyRunHandler.cs` for the extension seam. Size is reported, not enforced.

## 14. Changes since revision 1

Each answers a finding of the [first plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6899498280).

1. Finding 1: phases `C0` and `C1` with configuration equal to phase, and the load generator Job name (3.1, 3.3, Q8); `VerifyRunHandler.cs` in scope for a repeat-result extension, the receipt pinned (2, 3.8, rule 1); U1 and U16 rewritten.
2. Finding 2: rule 9 no longer gives `consumer_commit_evidence_unavailable` for a `not_attempted` read; rule 12 states that at most one incomplete reason can apply; U12's second case replaced by a reachable one.
3. Finding 3: rule 5 requires an `assigned` row by the first read; an assignment may be empty (3.2); U5 extended.
4. Finding 4: rule 3's skip clause, with the check's text.
5. Finding 5: both settings maps must hold an equal `isolation.level` (3.4, 3.5); U15 extended.
6. Finding 6: the shared namespace input is a contract for #507 (10(c)).
7. Findings 8, 9 and 10: applied as marked (10a item 1; 3.1, whose text finding 1 replaced; section 1 and section 6).
8. Non-blocking: CT3 uses `SpikeFault.Timeout`, as marked `apply in implementation`; rule 4 checks the receipt's identity fields.

This revision changes contracts, requirements and the scope, so it needs a new check (factory.md "Item lifecycle" step 1).
