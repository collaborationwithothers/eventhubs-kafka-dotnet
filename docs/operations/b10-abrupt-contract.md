# B10 abrupt diagnostic contract

The B10 abrupt path runs an ordinary timeout producer, a parent with a self-killing producer child, and a permitted fresh successor in sequence. Its per-repeat verifier reports nonfinal evidence. The diagnostic trial retains its existing contracts; [scientific execution](#scientific-execution) extends the path to five repeats without authorizing final publication.

[ADR-021](../blueprint.md#adr-021-b10-accepts-timed-out-sends-application-retries-and-one-producer-restart-in-a-second-job-and-classifies-late-appends-up-to-a-sweep-line) owns the restart protocol and scientific rules. The [approved part 1 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/fc1a4f36452b2d302f1d73987e1ba5eb967b3d65/items/404/plan-b10-execution-loadgen.md), section 3, owns the LoadGen and schema-3 interfaces below. The [approved part 2 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/0c73e5139e1670d5b675a51fc52dbd99c4b1af49/items/404/plan-b10-execution-trial.md), section 3, owns trial orchestration and collection. The [approved warm-leader plan, Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/34aae8485053429a5b183e86a5f2c39dda570d25/items/404/plan-b10-warm-leader.md), section 3, amends the probe settings, warm-up records, leader gate and child deadline. The [approved classification plan, Revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/aedb21c045b1a9aef962bdea955e62475c0a1590/items/404/plan-pr3b-classification.md), sections 3 and 4, owns the diagnostic verifier contracts, with the [implementation corrections](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816937259). This page owns their wire formats and reader details; ADR-021 owns the design rules. These replace the stop/checkpoint/pipe protocol; the [workflow page](b10-workflow.md#diagnostic-trial-inactive-for-science) records dispatch and trial limits.

## Processes and configuration

Part 1 sections 3.1 and 3.2 own these commands, exit codes and settings. Parent and child share the restart pod's producer identity. The parent constructs no Kafka producer and starts only the fixed child apphost.

| Process | Command | Exit codes |
| --- | --- | --- |
| Parent | `/app/Parchwell.LoadGen --b10-abrupt-parent` | `0`: `termination.json` created and read back, whatever its termination class; `1`: failure before that; `124`: the existing 900 s watchdog |
| Child | `<running apphost directory>/Parchwell.LoadGen --b10-abrupt-child` | `137`: required local exit observation for intended self-kill; `3`: no-kill path; `4`: self-kill call returned; `1`: failure before the first journal record |
| Successor | `/app/Parchwell.LoadGen --b10-abrupt-successor` | `0`: `completion.json` published; `1`: failure |
| Validation | `--validate-b10-abrupt-configuration` | `0` with `{"b10_abrupt_configuration_valid":true}`; otherwise `1` |

`--b10-abrupt-supervisor` is removed and rejected as an unknown argument. Settings come only from environment variables with prefix `B10Abrupt__`. The closed identity/path keys are `Enabled`, `Purpose`, `Role`, `ScenarioId`, `ConfigurationId`, `Phase`, `Repeat`, `RunId`, `SourceCommit`, `ImageReference`, `JobName`, `PodName`, `PodUid`, `NamespaceName`, `BlobEndpoint`, `ScenarioSourceRoot`, `OutputRoot`, `StartLineBindingPath` and `PredecessorBindingPath`.

For the diagnostic trial, required values are `Enabled=true`, `Purpose=diagnostic_trial`, `ScenarioId=B10`, `ConfigurationId=C1`, `Phase=C1` and `Repeat=1`. Role is `parent`, `child` or `successor`; `supervisor` is rejected with `validation_detail=Role`. Parent and child use `StartLineBindingPath`; only the successor uses `PredecessorBindingPath`. The parent passes its settings to the child with role `child`, adding no other key; the child refuses `PredecessorBindingPath`. Scientific identities follow [scientific execution](#scientific-execution).

These fixed policy values are not measurements. A different value is rejected with `validation_detail=FixedPolicy`:

| Key | Value |
| --- | --- |
| `MaxRecordBytes` | 65536 |
| `QueueCapacity` | 256 |
| `MaxJournalBytes` | 16777216 |
| `MaxJournalEntries` | 4096 |
| `MaxAuxiliaryBytes` | 65536 |
| `MaxJsonDepth` | 32 |
| `ReleaseDelaySeconds` | 120 |
| `ProbeRate` | 100 |
| `CutOrdinal` | 4 |

Removed keys are rejected with `SettingNameOrDuplicate`: `EventsPipeHandle`, `AckPipeHandle`, `IdentityChallenge`, `ArmTimeoutSeconds`, `KillTimeoutSeconds`, `ReapTimeoutSeconds`, `EofTimeoutSeconds` and `MaxFrameBytes`.

ADR-021 protocol rule 1 owns the child and successor producer settings and preserves the C1 timeout Job's settings. The probe values are constants in abrupt code, with no new `B10Abrupt__` key or scenario setting. The child's statistics expected-settings dictionary holds its effective configuration, including its `linger.ms`; the child writes no `capabilities.json`. The successor's `capabilities.json` entry for `message.timeout.ms` has `value` "300000", unchanged baseline metadata and `override` null. Its selected configuration is built the same way for `RunEvidenceReader`. The `max.in.flight` and `enable.idempotence` entries retain their C1 values and scenario `override` provenance. Every other key is unchanged.

## Trial order and Job identities

Part 1 section 3.3 owns the names. Part 2 sections 3.1, 3.2 and 3.4 own order, timeout outcomes and run ID. The trial run ID is `b10d-{GITHUB_RUN_ID}-{GITHUB_RUN_ATTEMPT}`; scientific B10 retains prefix `b10`. Job names, configmaps and Blob prefixes derive from the run ID.

After `{run}-c1-r1-start-line` completes and its metadata is checked, the trial runs:

| Order | Job | Execution |
| --- | --- | --- |
| 1 | `{run}-c1-r1-loadgen` | Ordinary C1 timeout host, mode `loadgen`, no `B10Abrupt__` setting or abrupt binding configmap |
| 2 | `{run}-c1-r1-loadgen-restart` | Abrupt parent and child, mode `loadgen-restart` |
| 3 | `{run}-c1-r1-loadgen-successor` | Abrupt successor, mode `loadgen-successor`, admitted only through `release_successor` |
| 4 | `{run}-c1-r1-b10-abrupt-verify` | Diagnostic verifier, mode `b10-abrupt-verify`, after the successor step whenever the first publication was accepted, whatever the successor outcome; not after a rejected publication, no-kill end or empty kill set |

At most one producer pod runs at a time. Each producer Job is waited for before the next producer Job is applied, and its pod is observed terminated before the next producer Job is rendered. Finished Jobs and their terminated pods are not deleted until cleanup.

The [Kubernetes Job page, Terminal Job conditions](https://kubernetes.io/docs/concepts/workloads/controllers/job/#terminal-job-conditions), read 2026-11-11, states that from v1.31 terminal conditions are added only after all Job pods terminate. [The cluster version validation](../../infra/cluster/variables.tf) requires at least 1.35.0. The trial checks pod termination directly instead of relying on terminal conditions.

After timeout wait outcome `complete` or `failed`, before any later render, the trial reads its pods once: `kubectl get pods -n parchwell -l job-name={run}-c1-r1-loadgen -o json --request-timeout=30s`, bounded to 30 s, saving output to `b10-abrupt-diagnostic/loadgen-pods.json`. It requires exit 0, decoded JSON, exactly one pod, exactly one controller owner of kind `Job` with the expected name, one container and one status named `loadgen`, the phase's loadgen image, and `state.terminated` with integer `exitCode`. Any failed condition or read exception makes the outcome `unknown`.

| Timeout outcome | Trial action |
| --- | --- |
| `complete` or `failed`, pod check passed | Retain the record and continue to the restart Job |
| `unknown` or `apply_failed` | Stop further Jobs, set `safety_stop`, record `b10_abrupt_timeout_job_unknown` at `timeout_job`, return 1 |
| `render_failed` | Stop further Jobs, leave `safety_stop` unchanged, record `b10_abrupt_configuration_invalid` at `timeout_job`, return 1 |

An exception in the timeout-Job step records fallback `b10_abrupt_publication_failed` at boundary `timeout_job` and starts no later Job. A failed timeout Job does not repair or fail the restart chain; ADR-021 protocol rule 12 owns independent per-instance checks. All Jobs share the existing 18000 s orchestration deadline. No log collection delays successor release.

For B10 run IDs matching `^b10d-[0-9]+-[0-9]+$`, the start line expects exactly `[{run}-c1-r1-loadgen, {run}-c1-r1-loadgen-restart, {run}-c1-r1-loadgen-successor]`, including the successor name even when no successor starts. Trial metadata requires that list. Scientific and legacy name rules are defined in [scientific execution](#scientific-execution); no diagnostic-trial object becomes scientific evidence. Authority: [PR3c approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6828172387) and the scientific plan linked below.

## Schema-3 envelope and journal

The formats below describe the diagnostic trial. Scientific execution changes only the purpose, repeat-derived identities and paths, chart admission, and per-repeat verifier contract listed in [scientific execution](#scientific-execution).

Part 1 sections 3.4 and 3.5 own the closed formats. Every abrupt JSON object and journal record carries `schema_version: 3`, `dialect: "b10_abrupt"`, `purpose: "diagnostic_trial"`, `synthetic: true`, `scientific_admissible: false`, `kind`, `identity` and `data`. New readers reject schema 2, duplicate or unknown members, trailing content and depth above 32. UTC strings use the exact 33-character `O` representation ending `+00:00`.

Identity has exactly `run_id`, `scenario_id`, `configuration_id`, `phase`, `repeat`, `source_commit`, `image_reference`, `job_name`, `pod_name`, `pod_uid`, `instance_id` and `predecessor_instance_id`. Instance ID equals pod name. Parent and child share identity; only the successor has a non-null predecessor instance ID.

Only the child writes `<OutputRoot>/abrupt/journal.jsonl` (`/work/loadgen/abrupt/journal.jsonl` in the chart), opened create-new. Each UTF-8 JSON record ends in byte `0x0A`. Besides the envelope, a record has `sequence`, `observed_utc`, `monotonic_ticks` and `clock_frequency`. Sequence starts at 1 and increments under one lock shared by both writing threads. The child calls `FileStream.Flush()` after each record before the next record or action.

| Kind | Writer and `data` |
| --- | --- |
| `child_started` | Producing thread, first record: `pid`, `start_time_ticks`, `boot_id` |
| `warmup_prepared` | Producing thread, before the warm-up `Produce`: `attempt_id`, `arm`, `key`, `payload_length`, `payload_sha256` |
| `warmup_terminal` | Journal writer thread, from the warm-up delivery callback through the existing bounded queue: `attempt_id`, `completion_source` (`callback`), `delivered`, `error_code`, `persistence_status`, `partition`, `offset` |
| `prepared` | Producing thread, before `Produce`: `attempt_id`, `arm`, `role`, `ordinal`, `probe_id`, `key`, `sequence`, `payload_length`, `payload_sha256`, `predecessor_attempt_id` |
| `pre_acceptance_report` | Producing thread, after ordinal 0's `prepared` and before its `accepted`: `arm`, `attempt_id`, `name`, `length`, `sha256`, `report_identity`, `report_sequence`, `report_capture_utc`, `warmup_attempt_id`, `partition` |
| `accepted` | Producing thread, after `Produce` returns: `attempt_id`, `prepared_sequence` |
| `synchronous_rejection` | Producing thread, when `Produce` throws: `attempt_id`, `prepared_sequence`, `error_code`, `error_reason` |
| `terminal` | Journal writer thread: `attempt_id`, `completion_source`, `delivered`, `error_code`, `persistence_status` |
| `native_error` | Journal writer thread: `capture_sequence`, `error_code`, `error_name`, `is_error`, `is_fatal`, `callback_monotonic_ticks` |
| `cut` | Producing thread, control arm after ordinal 4's `accepted`: `arm`, `cut_attempt_id`, `release_utc`, `unconfirmed_attempt_ids` |
| `control_drained` | Producing thread: `flush_returned`, `out_queue_length` |
| `post_drain_report` | Producing thread, after `control_drained`: the original descriptor fields (`arm`, `attempt_id`, `name`, `length`, `sha256`, `report_identity`, `report_sequence`, `report_capture_utc`), with arm `same_instance`, null attempt ID and name `control-post-drain-report.json` |
| `kill_intent` | Producing thread, cross-restart arm after ordinal 4's `accepted`: `arm`, `cut_attempt_id`, `release_utc` |
| `signal_failed` | Producing thread, only when the kill call returns after `kill_intent`: `return_value`, `errno` |
| `no_kill` | Producing thread: `reason`, `cross_restart_admitted` |
| `gap` | Either thread when writing remains possible: `reason`, `first_missing_sequence` (nullable), `capture_status` |

Arms are `same_instance` and `cross_restart`; roles are `original` and `resend`. Original ordinals run from 0 to 4, with sequence equal to ordinal; a resend keeps its original's ordinal. Original predecessor attempt IDs are null. Pre-acceptance report names are `<arm>-pre-acceptance-report.json`. Report identity, sequence, capture time, length and hash use part 1 section 3.5's descriptor contract: report identity has `scenario_id`, `run_id`, `role` and `client_id`, with role `LoadGenProducer`; report length is 1 to 1048576 bytes and report sequence is positive. Native-error `capture_sequence` starts at 1 without gaps.

Warm-up wire records use topic `document-events`, the arm key and the arm's probe payload bytes. Their headers, in order, are `instance-id` equal to the instance ID, `attempt-id` equal to `{instance_id}-{arm}-warmup`, and `sequence` equal to `-1`. The caller sets no partition. ADR-021 protocol rule 1 owns the partitioner and cohort rules.

`warmup_prepared` is a producing kind; `warmup_terminal` is not. `partition` is a JSON integer in the signed 32-bit range, and `offset` is a JSON integer in the signed 64-bit range ([Confluent.Kafka v2.16.0 Partition.Value](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Partition.cs), [Offset.Value](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Offset.cs)). C# reads them with `TryGetInt32` and `TryGetInt64`; Python checks `-2147483648 <= v <= 2147483647` and `-9223372036854775808 <= v <= 9223372036854775807`. The existing 32-bit `Integer` helper is not used for `offset`. When `delivered` is true, both are at least 0. A synchronous warm-up rejection has no record of its own; `no_kill` with reason `b10_abrupt_warmup_failed` is its trace.

Every journal, including a no-kill journal, enforces these warm-up rules:

- At most one `warmup_prepared` per arm, with `attempt_id` `{instance_id}-{arm}-warmup` and the arm key.
- Cross-restart `warmup_prepared` comes only after the control arm is complete, under the existing control-completeness condition for cross-restart `prepared`.
- At most one `warmup_terminal` per `warmup_prepared`, after it, with a matching attempt ID. It may follow `no_kill`, because a no-kill flush can complete it late.

Only when the arm's ordinal-0 `prepared` exists, exactly one warm-up preparation and terminal must both precede it. The terminal must have `delivered` true and `error_code` 0, with `persistence_status` 0 or 2, as the existing reader permits for error code 0. The arm's `pre_acceptance_report` must bind that warm-up's attempt ID and delivered partition, with `report_capture_utc` strictly later than the terminal's `observed_utc`. A no-kill journal ending before that ordinal-0 preparation gets no gap or failure from these conditional rules, whatever its warm-up records show. Other breaches of these journal rules give capture gap `malformed_frame`; a report's failed warm-up fields or capture time give `b10_abrupt_report_binding_invalid`.

The control set is computed and appended atomically with `cut`, under the journal lock: accepted control originals without a terminal or synchronous rejection before `cut`, in invocation order. A terminal appended after that lock is released cannot remove a member. Readers recompute the set from journal order and require equality with `unconfirmed_attempt_ids`.

Under ADR-021 protocol rule 6, the kill unconfirmed set is the cross-restart arm's accepted originals without a complete terminal record anywhere in the journal at death, in invocation order. The parent and successor recompute it from the same journal reader.

Limits are charged before writing: record JSON at most 65536 bytes excluding its newline, journal at most 16777216 bytes including newlines, at most 4096 records. Callbacks only enqueue into the bounded 256-entry memory queue and perform no file or network I/O. Capture gaps are sticky. Gap reasons are `frame_oversize`, `queue_full`, `journal_bytes_exceeded`, `journal_entries_exceeded`, `writer_failed`, `malformed_frame` and `sequence_gap`. A torn final line or malformed line gives `malformed_frame`; noncontiguous sequence gives `sequence_gap`.

## Probe sequence

Part 1 section 3.6 owns this fixed policy sequence. The workload is synthetic; the rate is policy, not a measurement.

1. Write `child_started`, construct the child producer with ADR-021 protocol rule 1's settings and wait for a parsed statistics report.
2. Complete the control warm-up and leader gate under ADR-021 protocol rules 1 and 5, then invoke control originals 0 to 4. Invocation `i` is no earlier than `i * 10 ms` after ordinal 0's invocation (100 originals per second). After ordinal 4's acceptance, write `cut` atomically with its control set; release is that record's UTC plus 120 s. An empty set takes `no_kill` with `b10_abrupt_exposure_inconclusive`.
3. At control release, resend each member once in invocation order with a new attempt ID and the same instance, key, sequence and payload bytes, even if it has since completed.
4. Flush within the existing 300 s producer-drain budget, write `control_drained`, then bind the first parsed statistics report captured after drain. Continue only with `flush_returned=true`, queue length 0, a terminal for every control original and resend, and that report. Otherwise take `no_kill` with `b10_abrupt_control_incomplete`.
5. Complete the cross-restart warm-up and leader gate under the same rules, then invoke cross-restart originals 0 to 4 at the same pacing. After ordinal 4's acceptance, write `kill_intent` and call `kill(getpid(), 9)` once. Its release is that record's UTC plus 120 s. After this arm's first original is admitted, call no `Flush` or `Dispose`. A kill call that returns writes `signal_failed` with return value and errno, then exits 4.
6. Failure before `kill_intent` writes `no_kill` best effort. Before cross-restart admission, flush within the time left before the child deadline, dispose and exit 3; after admission, use `Environment.Exit(3)` without flush or disposal.

The keys/probe IDs are `B10/{run_id}/C1/1/same_instance` and `B10/{run_id}/C1/1/cross_restart`. Each arm uses the first generated profile event's payload bytes for every attempt. If ordinal 4 is synchronously rejected, no cut is written: the cross-restart arm with no accepted original gives `kill_too_early`; otherwise `b10_abrupt_cut_rejected`. Rejections of ordinals 0 to 3 are recorded and execution continues.

## Leader report selection and child timing

ADR-021 protocol rules 5 and 7 own the leader gate, wait failures and child deadline. `LeaderUp(report, topic, partition)` reads the raw statistics JSON: `topics[topic].partitions[decimal string of partition].leader` must be an integer L at least 0; exactly one value in `brokers` must have `nodeid` equal to L; and that value's `state` must be the string `UP`. Missing fields, no matching broker, multiple matches or any other state return false. The field meanings come from [STATISTICS.md v2.16.0](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md).

Warm-up and report selection happen before the arm's ordinal-0 `prepared`; the selected report is bound after that preparation and before its `accepted`. Among parsed reports captured strictly after `warmup_terminal.observed_utc`, select the first satisfying `LeaderUp`. The statistics wait is `StatisticsCompletionTimeout` 20 s in the abrupt host, or `CleanupTimeout` when unset, limited by the child deadline. The phase reader rechecks `LeaderUp` on the published report bytes. Failed warm-up binding, capture time or `LeaderUp` gives `b10_abrupt_report_binding_invalid`, and the phase reader rejects the first publication through its existing report-binding failure path without releasing the successor.

The deadline in ADR-021 protocol rule 7 uses the child's monotonic clock. The control drain retains `ProducerDrainTimeout` 300 s; no-kill disposal retains `CleanupTimeout` 10 s. The following are policy-path bounds computed by the approved plan's timing script, in seconds after `child_started`, not measured latencies:

| Path | Latest path entry | Latest end |
| --- | --- | --- |
| Successful `kill_intent` and self-kill | 680 | 680 |
| No initial report | 20 | 700 |
| Control warm-up not delivered | 110 | 700 |
| Control report missing or leader not `UP` | 130 | 700 |
| Control drain incomplete | 550 | 700 |
| Post-drain report missing | 570 | 700 |
| Cross-restart warm-up not delivered | 660 | 700 |
| Cross-restart report missing or leader not `UP` | 680 | 700 |
| Failure after cross-restart admission | 680 | 680 |
| Deadline reached before `kill_intent` | 690 | 700 |

At deadline expiry, ADR-021 protocol rule 7's remaining-time flush bound is 0 s. That rule also owns admission, disposal and exit behavior.

The parent's death wait starts at its own start, earlier than `child_started`. Disposal is outside the child deadline and inside its exit margin. Parent work before child start, child start before `child_started`, disposal and process exit together fitting in that margin is live-only assumption LA6, accepted for the next diagnostic trial by [decision 6801095762, point 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801095762). The decision records parent `child_started` at 627 ms and the child's record 557 ms later, totaling 1184 ms by script in trial run 43010596896. The offline interval below covers only registration to the parent's `Process.Start` mark on a runner; it does not prove LA6. Exceeding the parent's wait still gives unknown termination.

## Offline proof

Warm-leader plan section 3.7 owns the offline test mechanism. Its local TCP proxy listens at `127.0.0.1` on an ephemeral port, which the optional `LocalKafkaFixture` advertised-address input supplies to the broker's `PLAINTEXT` listener. Every client in those tests bootstraps through that proxy. Existing fixture callers retain their behavior.

For each accepted connection, the proxy delays upstream connection by 500 ms. It forwards client-to-broker bytes without added delay and forwards each broker-to-client chunk, in order, no earlier than 500 ms after reading it. On client close it still forwards already-read client bytes upstream before closing upstream. It counts accepted connections. These delays are test policy, not Event Hubs measurements.

The offline image test retains its method, class, 900 s bound, container settings and existing assertions. Its consumer read window is 60 s. Its observation line retains existing keys and order, then appends after `consumed_resends`: `warmup_records=<count of warmup_terminal records> kill_originals_consumed=<count> parent_registered_to_child_started_microseconds=<decimal> connect_delay_ms=500 response_delay_ms=500`. The interval uses the parent's `parent_registered` and `child_started` stage ticks. `B10AbruptEndToEndTests` uses the same proxy and delays with unchanged assertions; its parent-role and successor bounds may each be 300 s under [decision 6801095762, point 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801095762).

## Parent, termination and first publication

Part 1 sections 3.7 to 3.10 own this lifecycle. The parent creates its registration before starting the child, captures its own `memory.events.local`, starts the fixed child without redirection, and reads the child's process start identity and boot ID. It waits for death for 720 s minus elapsed parent time, reserving publication time inside the existing 900 s process lifetime. It sends no signal and opens no pipe. After observed death it captures cgroup evidence again, reads the bounded journal, validates it and publishes.

| `termination.json` class | Required observations |
| --- | --- |
| `intended_self_kill` | Death observed; exit code 137; last producing-thread record `kill_intent`; `child_started` matches the parent's process observations; stable cgroup identity and unchanged `oom_kill` |
| `no_kill` | Death observed; durable `no_kill`; no `kill_intent`. Exit code is recorded without gating this class |
| `unexpected_exit` | Death observed with neither prior class. A durable `signal_failed` after `kill_intent` gives outcome `b10_abrupt_signal_failed` and its return value/errno; otherwise `b10_abrupt_unexpected_exit` |
| `unknown` | Death not observed within the bound; outcome `b10_abrupt_exit_unobserved` |

An intended self-kill with an empty kill unconfirmed set has outcome `kill_too_late`, detail `no_unconfirmed_attempt`; otherwise its outcome is null. A no-kill end carries its record's reason. `successor_permitted` is true only for intended self-kill, a nonempty kill unconfirmed set, no capture gap and successful publication. Unknown termination sets `safety_stop`; a known no-kill end does not. Parent exit 0 means publication succeeded, including a published unknown termination; it does not authorize a successor by itself.

Termination data also records `child`, `exit_observed`, `exit_code_local_observation`, `runtime_version`, `wait_bound_milliseconds`, `cgroup_before`, `cgroup_after`, `control_unconfirmed_attempt_ids`, `kill_unconfirmed_attempt_ids`, `capture_gaps`, `native_error_count`, `native_fatal_count`, `native_tail` and `manifest`. `child` holds `pid`, `start_time_ticks`, `boot_id` and `parent_observed_start_time_ticks`. `signal_observation` is null except for a returned kill call. `native_tail` is `unknown_due_to_intended_kill` only for intended self-kill. At wait expiry the parent publishes what exists, exits 0 after the termination receipt, and never signals the child.

Publication uses container `ledger`, prefix `<run_id>/C1/r1/b10-abrupt-diagnostic/<pod_name>/`, create-only with read-back. Order is registration at start, `journal.jsonl`, journal-bound reports in journal order, `abrupt-manifest.json`, then `termination.json` last. Reports are `same_instance-pre-acceptance-report.json`, `control-post-drain-report.json` and `cross_restart-pre-acceptance-report.json`, only when bound. Manifest data is exactly `journal`, `journal_records`, `report_files` and `gaps`. Descriptors are `{name,length,sha256}` with lowercase hexadecimal SHA-256. Missing or mismatched bound report bytes fail publication before the manifest. This publication has no timeout/control seals, cohort files or kill-selection file.

## Successor

Part 1 section 3.11 owns successor admission and publication; ADR-021 protocol rule 8 owns its no-warm-up behavior. The phase checks first-pod termination and accepts its publication before `release_successor` creates the successor binding configmap. Binding has exactly `first_pod`, `termination`, `start_line_binding`, `release_utc`, `runner_utc`, `runner_monotonic_ticks` and `cross_host_timing`. `first_pod` holds `job_name`, `pod_name`, `pod_uid`, `image_reference`, `source_commit` and `finished_utc`; `termination` is the termination-file descriptor. Cross-host timing remains `not_verified`.

The successor downloads `termination.json`, `abrupt-manifest.json` and `journal.jsonl` from the first pod's Blob prefix. It checks the hash/length chain, exact identities and `successor_permitted=true`, recomputes the kill unconfirmed set and requires equality with the termination receipt. It requires predecessor Job `{run}-c1-r1-loadgen-restart` and binding release equal to `kill_intent.data.release_utc`; stale selection or release gives binding detail `selection_stale`.

After registration with the first pod as predecessor, it waits for that release and resends each kill-set member once in invocation order, preserving key, sequence and payload hash with new instance and attempt IDs. Missing readiness at release gives `b10_abrupt_successor_late` and no resend. It drains and publishes `registration.json`, `ledger.jsonl`, `manifest.json`, `statistics.jsonl`, `request-lifecycle.jsonl`, `capabilities.json` and `completion.json` under its diagnostic prefix. Completion binds `first_termination`, `journal`, `kill_unconfirmed_attempt_ids`, registration and artifacts, readiness/release, invocations, cleanup and `cross_host_timing=not_verified`.

## Diagnostic verifier mode

The [approved full-classification plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/48c52669fd40a3bb80ca0378e061f1d6153d6a22/items/404/plan-pr3c-full-classification.md), sections 3 and 4, owns mode `b10-abrupt-verify`. It reconciles the timeout, first restart and successor instances, classifies the timeout cohort and counts each arm's copies in one bounded original window. For the trial, eligibility is diagnostic and pending the sweep; this mode selects no verdict. The scientific extension is defined in [scientific per-repeat C1 verifier](#scientific-per-repeat-c1-verifier).

### Mode, options and publication

`Verifier:Mode` is `b10-abrupt-verify`. `ScenarioId` is `B10`, `ConfigurationId` and `Phase` are `C1`, and `Repeat` is `1`. `VerifierCommit` equals `PARCHWELL_SOURCE_COMMIT`. A different value exits 1 before any read and writes no output object.

The required option names, under `Verifier__` in the environment, are:

- Common: `Mode`, `ScenarioId`, `VerifierCommit`, `MaxOutputBytes`, `MaxDiagnosticsPerCheck`, `MaxDiagnosticBytes`, `CleanupTimeout`, `ProcessLifetime`, `RunId`, `Phase`, `ConfigurationId`, `BlobServiceUri`, `VerifierContainer` and `LedgerContainer`.
- Repeat: `Repeat`, `RepeatPrefix`, `ExpectedInstanceId`, `VerifierClientId`, `NamespaceName`, `TokenTimeout`, `MetadataTimeout`, `WatermarkTimeout`, `PollSlice` and `BlobIoTimeout`.
- Mode: `IoTimeout`, `SafetyStop`, `MaxConsumedRecords`, `MaxConsumedBytes`, `MaxArtifactBytes`, `MaxTotalInputBytes`, `MaxPartitions`, `MaxInputDepth`, `StartLineInputName`, `LoadGenImage`, `VerifierImage`, `ScenarioRoot`, `ArtifactPrefix`, `MaxRowBytes`, `MaxRowsPerArtifact`, `MaxInventoryEntries`, `MaxInputEntries`, `MaxNativeReportBytes` and `MaxBrokerEntries`, plus the scenario-parsing options required by `verify`. `ArtifactPrefix` must equal `<RunId>/C1/r1/producer/`; a different value exits 1 before any read.

`JobName`, `PodName` and `PodUid` use the existing `PARCHWELL_*` environment fallbacks. For the trial, the chart restricts this mode to run IDs matching `^b10d-[0-9]+-[0-9]+$`, with the fixed scenario/configuration/phase/repeat above and no `b10_abrupt_diagnostic` or `b10_abrupt_binding_configmap` setting. It renders one verifier-role Job on `workload`, using the verifier service account, the existing `verify_job_seconds` deadline and no abrupt binding volume.

The mode creates one object in container `verifier` at `<RunId>/C1/r1/b10-abrupt-diagnostic/nonfinal-result.json`. It writes no `WorkerReceipt`, `result.json` or `verify-completion.json`. Exit 0 means that object was created and read back byte-identical, whatever diagnostic problems it records. An existing object, differing read-back or other publication failure exits nonzero.

### Inputs and enforced checks

The producer envelope, journal, termination, first-publication and successor formats above stay unchanged. These numbered checks are the diagnostic reconciliation contract. A bracketed number identifies the problem literal in the next section.

1. Validate the fixed mode options above. Failure exits 1 without an object or store read.
2. Apply the ordinary `verify` start-line checks to `PhasePaths.StartLineCompletion`, `PhasePaths.StartLine` and `PhasePaths.BrokerInventory` for `(RunId, C1, 1)`. Expected Job names follow the trial run-ID rule above. [1]
3. List the ledger prefix `<RunId>/C1/r1/b10-abrupt-diagnostic/`. Exactly one directory must have a valid registration for `{RunId}-c1-r1-loadgen-restart`; zero or multiple such directories give [2]. No remaining directory gives [3]. Exactly one remaining directory must have a valid successor registration for `{RunId}-c1-r1-loadgen-successor`, naming the first pod as predecessor; anything else gives [4]. An incomplete bounded listing gives [2], including `listing_incomplete` at the existing entry/name-byte/page bounds: 1024 entries, 1048576 name bytes and 16 pages.
4. Enforce exact envelope members, schema 3, `b10_abrupt`, `diagnostic_trial`, `synthetic=true` and `scientific_admissible=false`, with no duplicate/unknown member, trailing content or depth above 32. Require the 12 identity members above, with `RunId`, `B10`, `C1`, `C1`, repeat 1, `source_commit=VerifierCommit`, `image_reference=LoadGenImage`, the expected Job name, and pod/instance equal to the directory name. Predecessor is null for the first pod and the first pod's name for the successor. Pod UID is nonempty, agrees across each pod's objects and differs between pods. [2] for the first pod; [4] for the successor.
5. Enforce the existing per-file byte limits: `journal.jsonl` 16777216; `ledger.jsonl` 268435456; `manifest.json` 67108864; `statistics.jsonl` and `request-lifecycle.jsonl` 134217728 each; each report 1048576; every other member 65536. Total bytes per pod are at most 1073741824. Only `request-lifecycle.jsonl` may be empty. [2] or [4]
6. Check the first hash/length chain from `termination.data.manifest` to `abrupt-manifest.json`, from `manifest.journal` to `journal.jsonl`, and from each `manifest.report_files` descriptor to its report bytes. The object set is exactly registration, journal, abrupt manifest, termination and the listed reports, using only the three report names above. [2]
7. Require `termination=intended_self_kill`, `exit_observed=true`, `exit_code_local_observation=137`, `capture_gaps=[]`, manifest `gaps=[]` and `successor_permitted=true`. [2]
8. Require a newline-terminated journal with at most 4096 records, exact kind/data members from the journal table above, contiguous sequence 1..n, `journal_records=n` and no `gap` record. [2]
9. Each `accepted`, `synchronous_rejection` and `terminal` follows its attempt's `prepared`, at most once each. Per arm, originals have ordinals 0 to 4, sequence equal to ordinal, key `B10/{RunId}/C1/1/{arm}` and one shared payload SHA-256. [2]
10. Recompute the control set under the journal rule above. Require it to equal both `cut.unconfirmed_attempt_ids` and `termination.control_unconfirmed_attempt_ids`, and be nonempty. Require exactly one `same_instance` resend per member, in invocation order, preserving key, sequence and payload SHA-256. [2]
11. Recompute the kill set under the journal rule above. Require it to equal `termination.kill_unconfirmed_attempt_ids` and be nonempty. The last producing-thread record is `kill_intent`. [2]
12. Require all seven successor files above. `completion.json`, kind `successor_receipt`, binds registration and the five artifacts; `first_termination` and `journal` bind the first pod's termination and journal bytes. Require `cleanup_completed=true` and the same kill set as check 11. Require one invocation per member in order, with `predecessor_attempt_id` equal to the member, a unique new attempt ID and the member's key, sequence and payload SHA-256. [4]
13. Each successor ledger row ends in a newline and has exactly `attempt_id`, `completion_source`, `delivered`, `error_code` and `persistence_status`. Require exactly one row per invocation attempt and no other row. [4]
14. Read high watermarks once at verify start for every start-line partition. Complete the drain within `SafetyStop` and the consumed-record budget, retaining existing consumed-byte accounting. Only records with `start <= offset < end` for their partition are in scope. [5]
15. An arm-key record with any `MessageIdentityReader` problem or a null value is malformed. [6]


16. Timeout listing. List `<RunId>/C1/r1/producer/`. It holds exactly one directory, `<pod>/`, whose `registration.json` names Job `{RunId}-c1-r1-loadgen`, no predecessor, run `RunId`, `B10`, `C1`, phase `C1`, repeat 1 and source `VerifierCommit`. Any other registration, object outside that directory, or incomplete listing fails. [7] E7. The directory name is the timeout instance ID.
17. Timeout publication. A reader in `VerifyRun/Abrupt/` applies every check `PublicationAcceptance.Evaluate` applies to one producer instance, except that the start line's expected names are compared with the expected-name list above (6826128120 point 4). [7] E7.
18. Timeout judgement. Evaluated only when problems 1 and 5 are absent; otherwise not evaluated, adding no problem, with `timeout` null. The instance passes per-instance checks 1 to 6 and 12 to 27 of `docs/operations/verifier-contract.md`, as `RunJudgement.Evaluate` applies them to a B10 instance, with complete-valid status. For checks 7 to 11 it receives the mode's own window assessments for the shared window (PR3b checks 2 and 14), which pass whenever problems 1 and 5 are absent. Its records are only those that passed check 19. ADR-021 "Retries" also applies: for each inventory attempt in invocation order, when an earlier attempt with the same key and sequence exists, the latest such attempt has a row for which `B10RepeatChecks.IsPermittedTimeout` is true (as `B10RepeatChecks.cs` lines 79 to 85 apply it). [7] E7.
19. Timeout records. Evaluated before check 18, whenever the window was drained and check 16 passed. Every in-window record whose `instance-id` header equals the timeout instance ID joins exactly one timeout ledger row by `attempt-id`, with equal key and `sequence`, and its key is not an arm key. A failing record gives [8] E3 and is not passed to check 18. When check 16 or 17 fails, these records are held out and add no problem.
20. Probe and warm-up records. Evaluated when the window was drained and problem 2 is absent. Every other in-window record without problem 6 joins one of: a first-instance `prepared` attempt (instance, attempt, key, `sequence` and value hash equal to its `payload_sha256`); a first-instance warm-up (attempt `{pod}-{arm}-warmup`, the arm key, `sequence` -1, value hash equal to its `warmup_prepared` `payload_sha256`); or a successor invocation (instance, attempt, key, `sequence` and hash). [8] E3. When problem 3 or 4 applies, records whose instance equals the directory name check 3 found for the successor Job are held out and add no problem. Under problem 2, records that are not the timeout instance's are held out and add no problem.
21. Native capture. `native_error` records have `capture_sequence` 1..n without gaps. Their count equals `termination.native_error_count`, and those with `is_fatal` true equal `native_fatal_count`. `native_tail` is `unknown_due_to_intended_kill`. [2] E3. A record with `is_fatal` true adds E6. A record with `is_error` true and `is_fatal` false adds E5 (6761524200 point 2).
22. Child and cgroup. `termination.child.pid`, `start_time_ticks` and `boot_id` equal `child_started.data`; `parent_observed_start_time_ticks` equals `start_time_ticks`. `cgroup_before` and `cgroup_after` are non-null and equal in `boot_id`, `cgroup_path`, `directory_identity` and `oom_kill`, and their `boot_id` equals the child's. [2] E4.
23. Warm-up order. The journal meets the warm-up rules in the journal contract above for both arms. [2] E3. The journal contract requires the control arm complete before the cross-restart `warmup_prepared`, under the condition `B10AbruptJournal.cs` `ControlCompleteBefore` applies (lines 166 and 256 to 277): `flush_returned` true, `out_queue_length` 0, `post_drain_report` after `control_drained`, and a terminal, earlier in the journal, for every accepted control attempt. A journal that fails any of these therefore fails this check (6826128120 point 6).
24. Report binding. Each arm's `pre_acceptance_report` binds that arm's warm-up attempt ID and its `warmup_terminal.partition`; its `report_capture_utc` is strictly later than that terminal's `observed_utc`; and its report bytes satisfy `LeaderUp(report, "document-events", partition)` in the report contract above. The `post_drain_report` follows `control_drained`. [2] E3.
25. Control terminals. The journal holds a terminal record for every accepted control original and control resend. Failure adds E8 (6826128120 point 6). Flush, queue and post-drain failures are check 23's, not this check's. Because check 23 also requires those terminals, a journal failing this check also fails check 23, so E8 appears with problem 2 and E3, never alone.
26. Successor inventory. `manifest.json` (`cohort_inventory`) has `instance_id` the successor, `cohort` `cross_restart`, `bookkeeping_complete` true and `inventory_count` equal to the invocation count. Its entries equal the invocations in order: `attempt_id`, `predecessor_attempt_id`, `key`, `sequence` and `payload_sha256`, with `invocation_ordinal` 0..n-1. [4] E9.
27. Successor release. Each invocation's `invocation_utc` is not earlier than `release_utc`. `ready_utc` later than `release_utc` adds E11.
28. Guard (6826128120 point 7). Failure adds E13 only.
    - First instance: the three bound reports parse with `NativeStatisticsReader`. Every `brokers` dictionary key they hold, the broker identity in [Statistics and configuration](verifier-contract.md#statistics-and-configuration), is in the start line's broker inventory, and every broker's `txerrs` and `rxerrs` is 0.
    - Successor: its `statistics.jsonl`, `request-lifecycle.jsonl` and `capabilities.json` pass per-instance checks 15 to 26 as `verify` applies them, with ADR-021's B10 permitted conditions (nonzero `req_timeouts` and `txretries` permitted, ADR-021 "Permitted conditions"). Check 14 is replaced by its capabilities part only: `guard_observations_complete` true and `guard_observations` holding only the two markers [Statistics and configuration](verifier-contract.md#statistics-and-configuration) permits. Its manifest part does not apply, because the successor manifest is a `cohort_inventory` (contract line 185). Check 27 is replaced by: the captured resolved configuration equals C1's scenario-resolved configuration with exactly one difference, `message.timeout.ms` with value `"300000"`, unchanged baseline metadata and `override` null (ADR-021 protocol rule 1). `max.in.flight` and `enable.idempotence` keep their C1 values and scenario override provenance.
29. End-line re-read (6827265635 point 2). Evaluated after the drain when problems 1 and 5 are absent and at least one member has a known resend with `resend_terminal.delivered` true and `resend_copies` 0. The handler reads the high watermarks of every start-line partition once more, through the existing `VerifierKafkaSession.ReadHighWatermarks`, whose metadata read is bounded by `MetadataTimeout` and whose watermark reads share `WatermarkTimeout` (00:00:10 each in `charts/parchwell/values.yaml`), so at most 20 s. The re-read is not charged to `SafetyStop`, which bounds only the drain. WHEN any partition's re-read high watermark is greater than its end line, or the read fails or times out, THEN problem [9] is recorded, and every such member's `resend_lost` and its arm's `lost_resends` are null. WHEN every re-read equals the end line, THEN those members keep `resend_lost` true. No reason is added: an unknown loss is neither loss nor eligibility evidence, and the later late-write sweep from the end line covers the advanced range (ADR-021 "Late-write scan").

Resend counts: check 10 or check 12 failing only because a member has other than one resend gives E10 instead of E3 or E9.

Checks 21 to 25 and the first-instance part of check 28 are evaluated only when the first instance's evidence passed checks 3 to 11. Checks 26, 27 and the successor part of check 28 are evaluated only when the successor passed checks 3, 4, 5, 12 and 13. A check that is not evaluated adds no problem or reason.

Under problem 1, checks 17 to 20 and 28 are not evaluated and add nothing; `timeout` is null. Check 16 still runs. Under problem 2, problem 6 and check 20 are not evaluated. Check 19 runs after a drain if check 16 and the publication acceptance passed.


### Output `nonfinal_result`

The output envelope has exactly `schema_version: 3`, `dialect: "b10_abrupt"`, `purpose: "diagnostic_trial"`, `synthetic: true`, `scientific_admissible: false`, `kind: "nonfinal_result"`, `identity` and `data`. Its identity has the 12 members above: `run_id=RunId`, `scenario_id=B10`, `configuration_id=C1`, `phase=C1`, `repeat=1`, `source_commit=VerifierCommit`, `image_reference=VerifierImage`, `job_name=JobName`, `pod_name=PodName`, `pod_uid=PodUid`, `instance_id=PodName` and `predecessor_instance_id=null`.

`data` has exactly these members:

- `first_instance_id` and `successor_instance_id`: a string, or null when not identified and authenticated.
- `start_line` and `end_line`: arrays of `{partition, offset}` sorted by partition, or null.
- `window_complete`: boolean.
- `window_limit`: the literal `"copies_after_end_line_not_observed"`.
- `arms`: null, or exactly two arm objects, `same_instance` then `cross_restart`.
- `problems`: distinct literals from the ordered list below, in that order.
- `problem_details`: array of `{problem, detail}`, at most `MaxDiagnosticsPerCheck` entries; each detail is at most `MaxDiagnosticBytes` UTF-8 bytes. Detail text is the implementer's choice.
- `finality`: `"nonfinal"`.
- `timeout_instance_id`: string, or null when check 16 fails.
- `timeout`: `{ok, loss, phantom, reported_failure, duplicate, reorder}`, classified from timeout ledger rows and records that passed check 19; null under problems 1, 5 or 7. Problem 2 does not null it.
- `comparison`: `both`, `cross_restart_only`, `same_instance_only`, `neither` or `unavailable`.
- `native_errors`: `{count, fatal_count, tail}`, or null under problem 2.
- `eligibility`: `ineligible` if the ordered `eligibility_reasons` list is nonempty, otherwise `pending_sweep`. Never `eligible`.
- `eligibility_reasons`: distinct literals from the ordered list below.

An arm object has exactly these members:

- `arm` and `key`.
- `unconfirmed_count`: the size of check 10's or check 11's set.
- `resend_invoked`: an integer, or null for `cross_restart` without an authenticated successor.
- `originals_seen` and `resends_seen`: the number of members with at least one copy of that role.
- `pair_count`: the sum of member `pair_count` values.
- `witness`.
- `members`: in invocation order.
- `unreported_attempts`: accepted journaled probe attempts, including control resends, with no terminal anywhere in the journal.
- `unreported_seen`: those attempts with at least one in-window record having their instance and attempt headers; `unreported_unseen`: the rest. Their sum equals `unreported_attempts`.
- `lost_resends`: members with `resend_lost` true.

Each member has exactly these members:

- `original_attempt_id`, `sequence` and `payload_sha256`.
- `resend_attempt_id`.
- `resend_terminal`: `{delivered, error_code, persistence_status}` or null. For control resends it comes from the journal terminal; for cross-restart resends it comes from check 13's ledger row.
- `copy_count`, `original_copies`, `resend_copies` and `other_copies`.
- `pair_count`: `original_copies` times `resend_copies`.
- `copies`: at most `MaxDiagnosticsPerCheck` entries sorted by `(partition, offset)`; each entry has exactly `{partition, offset, instance_id, attempt_id, role}`.
- `copies_truncated`: boolean, or null when `copies` is null.
- `resend_lost`: true when a known resend terminal has `delivered` true and `resend_copies` is 0, false when either is otherwise known, and null when either is null or check 29 makes the loss unknown.

A copy is a window record with the member's key, sequence header and value hash equal to its `payload_sha256`. Warm-up sequence -1 never matches. Its role is `original` when the instance/attempt headers equal the first pod and original attempt; `resend` when they equal the authenticated resend's instance/attempt; otherwise `other`, retaining header values or null. The resend instance is the first pod for `same_instance` and the successor for `cross_restart`.

The witness is the smallest `(original_attempt_id, resend_attempt_id, original coordinate, resend coordinate)` over all original/resend copy pairs, as `{original_attempt_id, resend_attempt_id, original: {partition, offset}, resend: {partition, offset}}`, or null. Counts are JSON integers from 0 to 9223372036854775807. Unknown counts and copies are null under the rules below, never zero.

When more than `MaxDiagnosticsPerCheck` copies match a member, retain the first that many by `(partition, offset)` and set `copies_truncated=true`; every count remains complete. The per-member copy details and diagnostic extras belong only to this nonfinal object. The later final sweep projection must drop them and test that exclusion under the approved diagnostic-only contract and [decision 6816117386, point 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816117386).

### Problems and unavailable fields

The closed problem list, in order, is:

1. `b10_abrupt_verify_start_line_invalid`
2. `b10_abrupt_verify_first_evidence_invalid`
3. `b10_abrupt_verify_successor_absent`
4. `b10_abrupt_verify_successor_evidence_invalid`
5. `b10_abrupt_verify_window_incomplete`
6. `b10_abrupt_verify_probe_record_malformed`
7. `b10_abrupt_verify_timeout_evidence_invalid`
8. `b10_abrupt_verify_record_unjoined`
9. `b10_abrupt_verify_end_line_advanced`

A start-line failure records problem 1 and still runs checks 3 to 13. With valid first evidence, arms retain their members, but each member's `copy_count`, role counts, `pair_count`, `copies` and `copies_truncated` are null. The arm's `originals_seen`, `resends_seen`, `pair_count` and `witness` are null. `start_line` and `end_line` are null and `window_complete=false`.

| Problem | Arms and member fields | Window |
| --- | --- | --- |
| 2 | `arms=null`; do not evaluate problems 3 or 4 | Drained for the timeout cohort; `start_line` follows check 2, `end_line` is as read, and `window_complete` follows the drain. Problem 5 records an incomplete drain. Checks 16 to 19 run; `native_errors=null`, `comparison=unavailable` |
| 3 or 4 | Arms present. For `cross_restart`, member `resend_attempt_id`, `resend_terminal`, `resend_copies` and `pair_count` are null; arm `resend_invoked`, `resends_seen`, `pair_count` and `witness` are null. Compute `copy_count`, `original_copies`, `other_copies`, `copies`, `copies_truncated` and `originals_seen`; successor-headed copies count as `other` | Drained |
| 5 | Arms present; all copy fields and counts derived from them are null, including `copies_truncated` | `window_complete=false`; `end_line` as read, or null |
| 6 | Arms present; a malformed record counts when its sequence and value hash parse, with null invalid headers and role `other` | Drained |


`unreported_seen`, `unreported_unseen`, `lost_resends` and `resend_lost` are null when copy fields are null. Cross-restart loss fields are also null under problems 3 and 4. Under problem 9 the candidate lost members and their arm's `lost_resends` are null, preserving other known members' results. Unknown is never zero.

`comparison` is unavailable under any of reasons E1 to E4 or E8 to E12. Otherwise it is `both` if both arms have at least one pair, `cross_restart_only` or `same_instance_only` if only that arm does, and `neither` if neither does. Native capture, timeout bookkeeping and guard reasons alone do not withdraw the comparison. Loss is not an eligibility reason.

### Eligibility reasons

The reason literals are ordered policy diagnostics. No reason represents the missing sweep; `pending_sweep` states that limit.

| # | Literal | Condition here |
|---|---|---|
| 1 | `b10_restart_configuration_invalid` | problem 1 |
| 2 | `b10_restart_first_evidence_unavailable` | checks 3 and 5 for the first pod |
| 3 | `b10_restart_identity_unauthenticated` | check 4 for the first pod; checks 6, 8, 9, 10, 11, 19, 20, 21 (count or tail), 23 and 24; problems 6 and 8 |
| 4 | `b10_restart_termination_unverified` | checks 7 and 22 |
| 5 | `b10_restart_native_error_captured` | check 21, non-fatal error |
| 6 | `b10_restart_fatal_captured` | check 21, fatal |
| 7 | `b10_restart_timeout_bookkeeping_unavailable` | problem 7 |
| 8 | `b10_abrupt_control_incomplete` | check 25 (missing control terminals only; 6826128120 point 6) |
| 9 | `b10_restart_successor_evidence_unavailable` | problems 3 and 4, except resend count |
| 10 | `b10_restart_resend_count_invalid` | resend-count rule above |
| 11 | `b10_abrupt_successor_late` | check 27 |
| 12 | `b10_restart_window_incomplete` | problem 5 |
| 13 | `b10_restart_guard_failed` | check 28 |


The existing `PhaseWireCodec`, `VerifierResultCodec` and `B10SweepEvidenceCodec` reject the produced bytes. Successful diagnostic publication never authorizes finality or scientific counts.

## Scientific execution

The [approved scientific three-Job plan, Revision 7](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/4196a0325fd91534239efbc70ef3dbafd6dfe10e/items/404/plan-b10-scientific-three-job.md), sections 3.1 to 3.8, extends the existing path to scientific run IDs. Pattern S is `^b10-[0-9]+-[0-9]+$`; pattern T is `^b10d-[0-9]+-[0-9]+$`. Scientific prepare generates S IDs and retains the existing 29-character run-ID limit. The [workflow contract](b10-workflow.md#scientific-execution) owns sequencing, lifecycle records and phase gaps; [scientific C0 verify](verifier-contract.md#scientific-c0-verify) owns the one-producer C0 evidence.

For an S run ID `R` and repeat N from 1 to 5, the expected producer names in the options and start line are:

| Configuration | Ordered expected Job names |
| --- | --- |
| C0 | `R-c0-rN-loadgen` |
| C1 | `R-c1-rN-loadgen`, `R-c1-rN-loadgen-restart`, `R-c1-rN-loadgen-successor` |

The C1 list includes the successor even when it does not start. T keeps the trial's three C1 names. Other run IDs keep the legacy two-name list, `loadgen` then `loadgen2`, for each configuration. The frozen sweep and judge readers reject the new scientific start lines and schema-3 per-repeat objects; their legacy contracts do not supply scientific finality.

### Scientific LoadGen identities and publication

`B10Abrupt__Purpose` accepts `diagnostic_trial` or `scientific`. The diagnostic purpose retains its existing validation, including `Repeat=1`, for any run ID. The scientific purpose requires an S run ID and `Repeat` 1 to 5 written without a leading zero. `ScenarioId`, `ConfigurationId` and `Phase` remain `B10`, `C1` and `C1`. Failure uses `ValidationDetail.DiagnosticIdentity` before registration.

For scientific purpose, restart and successor Jobs are `{RunId}-c1-r{Repeat}-loadgen-restart` and `{RunId}-c1-r{Repeat}-loadgen-successor`. Probe keys and IDs are `B10/{RunId}/C1/{Repeat}/same_instance` and `B10/{RunId}/C1/{Repeat}/cross_restart`. Publication uses ledger prefix `{RunId}/C1/r{Repeat}/b10-abrupt/{pod}/`; the trial keeps `{RunId}/C1/r1/b10-abrupt-diagnostic/{pod}/`. Every object and journal record uses the configured repeat in its identity.

The schema-3 envelope uses the configured purpose, with `scientific_admissible=false` for both purposes. Its schema, dialect, synthetic marker and all other members keep the formats above. The successor reader requires purpose and identity repeat equal to its own configured values. Phase and verifier readers require `scientific` for S and `diagnostic_trial` for other run IDs; governed trial IDs match T. A mismatch follows the existing envelope failure: `malformed_frame` in LoadGen, `b10_abrupt_publication_failed` in the phase, and problem 2 or 4 for the verifier's first or successor evidence.

### Scientific chart admission

Runtime key `b10_abrupt_science` defaults to false. When true, schema validation requires scenario `B10`, configuration and phase `C1`, repeat 1 to 5, an S run ID, `b10_abrupt_binding_configmap` and `bounds.b10_abrupt`. `b10_abrupt_diagnostic` must be false or absent. The [diagnostic schema branch's](../../charts/parchwell/values.schema.json) excluded keys remain excluded; science adds no constraint on `run_kind`.

Modes `loadgen-restart` and `loadgen-successor` require exactly one of `b10_abrupt_diagnostic` and `b10_abrupt_science` true. The binding configmap is forbidden when neither is true. The chart renders `B10Abrupt__Purpose=scientific` for science and `diagnostic_trial` otherwise, and renders the configured `B10Abrupt__Repeat`.

Mode `b10-abrupt-verify` admits T with repeat 1 or S with repeat 1 to 5. Its `Verifier__ArtifactPrefix` is `<run>/C1/r<repeat>/producer/`. The configured deadlines remain 1140 seconds for producer Jobs, 750 for verify and 150 for start-line.

### Scientific per-repeat C1 verifier

For S, mode `b10-abrupt-verify` requires `ScenarioId=B10`, `ConfigurationId=C1`, `Phase=C1`, `Repeat` 1 to 5 and `ArtifactPrefix=<RunId>/C1/r<Repeat>/producer/`. Any other value exits 1 before any read. Checks 1 to 29 above apply with repeat N replacing repeat 1, `r1`, `/C1/1/` and `c1-r1`. Check 3 lists `<RunId>/C1/rN/b10-abrupt/`; check 4 requires purpose `scientific`; check 16 lists `<RunId>/C1/rN/producer/` and binds `{RunId}-c1-rN-loadgen`.

30. Earlier-repeat records. Evaluate only when the window was drained and N is at least 2. For each earlier repeat M in the same phase, list `<RunId>/C1/rM/producer/` and `<RunId>/C1/rM/b10-abrupt/` under the existing listing bounds and read each `registration.json` with the existing registration reader. Empty listings are valid. Hold a window record whose `instance-id` equals an instance registered there out of checks 19 and 20 and every cohort count, and count it in `earlier_repeat_records`. A failed or incomplete listing, an unreadable or invalid listed registration, or an instance ID shared between repeat N and an earlier repeat gives problem 10, `b10_abrupt_verify_earlier_repeat_unavailable`, and E3, `b10_restart_identity_unauthenticated`; `earlier_repeat_records` is null. When N is at least 2 and the window was not drained, do not evaluate check 30, add no problem 10 and set `earlier_repeat_records` to null. For N equal to 1, do not evaluate check 30 and set `earlier_repeat_records` to 0.

The output is create-only, with byte-identical read-back, in container `verifier` at `<RunId>/C1/rN/b10-abrupt/nonfinal-result.json`. It has exactly the diagnostic result's schema-3 envelope and identity members, with purpose `scientific` and identity repeat N. `synthetic=true` and `scientific_admissible=false`. Its `data` has exactly the members in [Output `nonfinal_result`](#output-nonfinal_result), plus `earlier_repeat_records`, a nonnegative integer or null under check 30. Problems 1 to 9 keep their order, followed by problem 10; E1 to E13 keep their order. `finality` remains `nonfinal`, and `eligibility` is `ineligible` or `pending_sweep`.

### Scientific C1 completion receipt

For S only, after creating and reading back the result byte-identical, the mode creates `<RunId>/C1/rN/b10-abrupt/verify-completion.json` once in container `verifier` and reads it back byte-identical. This is its last external write. Exit 0 requires both publications; an existing receipt or a failed write or read-back exits nonzero, and the result is never rewritten.

The receipt is an existing schema-1 `WorkerReceipt` with these mode-specific values:

| Member | Required value |
| --- | --- |
| `schema_version`, `mode` | `1`, `b10-abrupt-verify` |
| `scenario_id`, `configuration_id`, `phase` | `B10`, `C1`, `C1` |
| `run_id`, `repeat` | S run ID, N in 1 to 5 |
| `job_name`, `pod_name` | `{RunId}-c1-rN-b10-abrupt-verify`, verifier pod |
| `identity` | null |
| `source_commit` | `VerifierCommit` |
| `handler_exit_code`, `cleanup_completed` | `0`, true |
| `started_utc`, `completed_utc` | Existing UTC receipt timestamps |
| `artifacts` | Exactly one reference: `{container: "verifier", name: "<RunId>/C1/rN/b10-abrupt/nonfinal-result.json", length, sha256}`, binding the result's exact bytes |
| `publication_acceptance`, `repeat_checks`, `b10_intake` | null |

`PhaseWireCodec.ReadReceipt` and `WriteReceipt` apply the same rules and throw `FormatException` for any other value for this mode. The null identity is an exception for this mode only to the ordinary repeat-receipt identity requirement. Other receipt modes retain their existing rules. T retains the diagnostic result without an `earlier_repeat_records` member and writes no completion receipt.

## Failure names and collection

Part 1 section 3.9 owns the retained names and outcomes; warm-leader plan section 3.6 adds the warm-up and leader names, whose conditions ADR-021 protocol rule 7 defines. The warm-up failure also covers a synchronous `Produce` exception or a terminal with `delivered` false or a nonzero `error_code`. Part 2 section 3.3 adds `b10_abrupt_timeout_job_unknown`; classification plan section 3.6 adds `b10_abrupt_verify_failed`:

```text
b10_abrupt_diagnostic_not_scientific
b10_abrupt_configuration_invalid
b10_abrupt_platform_unsupported
b10_abrupt_signal_preflight_failed
b10_abrupt_child_start_failed
b10_abrupt_child_identity_mismatch
b10_abrupt_signal_failed
b10_abrupt_exit_unobserved
b10_abrupt_unexpected_exit
b10_abrupt_capture_gap
b10_abrupt_sample_missing
b10_abrupt_warmup_failed
b10_abrupt_leader_not_up
b10_abrupt_report_binding_invalid
b10_abrupt_guard_failed
b10_abrupt_control_incomplete
b10_abrupt_exposure_inconclusive
b10_abrupt_cut_rejected
kill_too_early
kill_too_late
b10_abrupt_cgroup_unavailable
b10_abrupt_oom_conflict
b10_abrupt_publication_failed
b10_abrupt_publication_ambiguous
b10_abrupt_first_termination_unknown
b10_abrupt_successor_binding_invalid
b10_abrupt_successor_late
b10_abrupt_deadline_exhausted
b10_abrupt_timeout_job_unknown
b10_abrupt_verify_failed
```

Retired names are `b10_abrupt_target_invalid`, `b10_abrupt_stop_unconfirmed`, `b10_abrupt_resume_unconfirmed` and `b10_abrupt_eof_incomplete`. The closed `no_kill.reason` set is `b10_abrupt_exposure_inconclusive`, `b10_abrupt_control_incomplete`, `kill_too_early`, `b10_abrupt_cut_rejected`, `b10_abrupt_sample_missing`, `b10_abrupt_warmup_failed`, `b10_abrupt_leader_not_up`, `b10_abrupt_report_binding_invalid`, `b10_abrupt_guard_failed`, `b10_abrupt_signal_preflight_failed`, `b10_abrupt_deadline_exhausted` and `b10_abrupt_capture_gap`. A returned kill call is not a no-kill reason.

Part 2 section 3.5 and classification plan requirements P4 and P5 own collection. For each applied `loadgen`, `loadgen-restart`, `loadgen-successor` and `b10-abrupt-verify` state record, `collect-b10-abrupt-trial` runs `collect-job` and copies `artifacts/<job>/` to `artifacts/b10-abrupt-diagnostic/<job>/`. For a `b10-abrupt-verify` record with outcome `complete`, it also downloads `<run_id>/C1/r1/b10-abrupt-diagnostic/nonfinal-result.json` from container `verifier`, at most 16777216 bytes, to `b10-abrupt-diagnostic/nonfinal-result.json`; a missing or ambiguous download records `b10_abrupt_verify_failed` at `verify_collection`. The phase makes no content check on that download. The start-line record is not copied. The workflow uploads only `b10-abrupt-diagnostic`. Before diagnostic verification, the phase gives the timeout Job no successor-receipt check, ordinary verify or evidence check beyond its recorded outcome. Collection exit 124 records `b10_abrupt_deadline_exhausted`; other nonzero exits record `b10_abrupt_publication_ambiguous`; copy exceptions record `b10_abrupt_publication_failed`, all at `job_collection`.

After an accepted first publication, the phase runs `b10-abrupt-verify` after the successor step, including successor failure, unknown outcome or exception. Rejected publication, no-kill, an empty kill set or unknown first termination starts no verify Job. A verify outcome of `failed`, `unknown`, `apply_failed` or `render_failed` records `b10_abrupt_verify_failed` at `verify_job`, preserves the prior `safety_stop` value and returns 1. A complete verify Job preserves the return value the trial would otherwise have had. No producer starts after verify.

The ordinary timeout host publishes under `producer/`. Part 2 section 3.7's production-byte export/checker verifies that scientific paths reject its evidence in frozen diagnostic trial state and that the exported documents contain no diagnostic marker keys. The phase test `test_trial_and_science_run_ids_never_collide` (part 2 section 8.1, T-P1) verifies distinct trial and scientific run-ID prefixes and noncollision. This trial produces no scientific candidate. The [verifier contract](verifier-contract.md) cites this diagnostic mode; the scientific extension above remains nonfinal, with the late-write sweep and final judgement left to later work.

## Final sweep projection

The scientific phase sweep turns the five nonfinal repeats into final timeout classifications and independent restart eligibility. It scans each repeat's original window and owned late records up to the phase sweep line. Its output retains aggregate arm evidence and decision inputs, with no diagnostic per-message table.

The [approved final judgement plan, Revision 6](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/07e2f2a3d80d9d0342590919d7a349f38a0d546a/items/404/plan-b10-final-judgement.md), sections 3.3 to 3.5, owns this format and its enforced checks. [ADR-021](../blueprint.md#adr-021-b10-accepts-timed-out-sends-application-retries-and-one-producer-restart-in-a-second-job-and-classifies-late-appends-up-to-a-sweep-line) owns the classification and decision rules. The [workflow contract](b10-workflow.md#scientific-execution) supplies lifecycle evidence and scheduling, and the [verifier contract](verifier-contract.md#scientific-b10-final-judgement) owns the receipt and candidate authentication.

### Scope, inputs and windows

Mode `b10-final-sweep` admits scenario B10, phase C0 or C1 and run IDs matching `^b10-[0-9]+-[0-9]+$` only. It reads `U/P/sweep-line.json`, the five repeats' start-line evidence and registrations, ordinary producer publications, and, for C1, first-pod and available successor evidence. Its input is Blob evidence; it does not read the phase's local `phase-status.json`. The original per-repeat objects and receipts remain immutable.

For C0, each repeat requires the one-producer `result.json` and `verify-completion.json`: status `incomplete`, window checks complete, exit 1, cleanup complete and an exact result-byte binding. The input, registration and start-line list bind `U-c0-rN-loadgen`. A wrong count, including zero registrations, fails that repeat's input check.

For C1, a repeat is one of four cases, in this precedence:

1. No start-line completion receipt: the repeat did not start.
2. A valid scientific `b10-abrupt/nonfinal-result.json` and its receipt exist and bind the same repeat.
3. The repeat started, neither object nor receipt exists, and its authenticated first termination is absent or does not permit a successor. This includes no-kill ends and rejected first publications.
4. Every other started repeat: missing, malformed or unbound input, including no per-repeat object after authenticated `successor_permitted=true`. Restart receives `b10_restart_input_unavailable`.

The timeout cohort is independently authenticated and classified in every started C1 case with a timeout registration. It reapplies per-repeat checks 16 to 19; failure gives `b10_final_timeout_evidence_invalid` and adds `b10_restart_timeout_bookkeeping_unavailable` to restart eligibility. Restart-only input failure never invalidates otherwise valid timeout evidence.

The sweep reauthenticates first-pod and successor evidence with the per-repeat readers and checks 3 to 13 and 21 to 28, as applicable to the termination class. Identities, cut and kill sets, invocations and member attempts must agree with a valid per-repeat object. Disagreement gives restart reason `b10_restart_input_unavailable`. An instance registered in two repeats is a global `b10_final_ownership_invalid` failure; other registration failures remain scoped to the relevant cohort.

Each started repeat's window uses the first available source:

| Source | Original window | Late range |
| --- | --- | --- |
| Valid per-repeat input with nonnull start and end lines | `[start_line,end_line)` | `[end_line,sweep_line)` |
| Otherwise, its own authenticated start-line note | `[start_line,next)` | `[next,sweep_line)` |

`next` is the next started repeat's authenticated start, or the sweep line for the last window. A repeat that did not start owns no range and is skipped. A started repeat without either authenticated source makes the whole scan invalid with `b10_final_start_line_invalid`; an unknown boundary cannot authorize another repeat's finality. Start must be at most end or next, and that value at most the sweep line; each repeat's end must be at most the next repeat's start. Invalid ordering gives global `b10_final_vector_invalid`.

An earlier repeat's record inside a later original window counts only for its own repeat. An unknown or later owner in an original window invalidates that repeat with `b10_final_owner_invalid` and gives C1 restart `b10_restart_late_record_unjoined`. Late ranges retain only their repeat's own instances. Original and late records form one union, with each topic/partition/offset counted once.

Every owned record must join an authorized timeout ledger attempt, prepared probe or warm-up, or successor invocation, with the checked identity, key and sequence. Unjoined timeout/C0 records give `b10_final_late_record_unjoined`; unjoined probe records affect restart only, with `b10_restart_late_record_unjoined`. Reclassification of the original C1 window must agree with every nonnull per-repeat value: timeout disagreement gives `b10_final_window_mismatch`; arm disagreement affects restart only. Warm-up joins but enters no outcome count.

One Kafka session and the existing safety, input, record and byte budgets cover the phase. Any incomplete drain is global `b10_final_drain_incomplete`. Retained consumed data contains only coordinate, instance/attempt identity, key, sequence and value SHA-256, never value bytes. Arm pair counts use original copies times resend copies. A known delivered resend with no union copy is lost; a missing terminal or unavailable scan leaves loss unknown. The per-repeat end-line-advanced problem alone no longer blocks eligibility, because the late range is scanned.

### Closed output

The create-only object is `U/P/b10-final-sweep.json`. The envelope has exactly `schema_version=3`, `dialect=b10_abrupt`, `purpose=scientific`, `synthetic=true`, `scientific_admissible`, `kind=final_sweep_projection`, `identity` and `data`. `scientific_admissible` equals `data.scan_complete`; it does not itself approve publication. Identity has exactly `run_id`, `scenario_id=B10`, `configuration_id`, `phase`, `source_commit`, verifier `image_reference`, `job_name`, `pod_name` and `pod_uid`.

`data` has exactly `sweep_line`, `scan_complete`, `residual_risk` and `repeats`. `sweep_line` is the note's `{container,name,length,sha256}` reference, or null if its check fails. `repeats` has exactly five entries ordered 1 to 5. Every projection and candidate states the exact residual risk: `A late append after the phase sweep line is not detected.`

Each repeat has exactly these fields:

| Field | Shape |
| --- | --- |
| `repeat` | Integer 1 to 5 |
| `source` | Reference to C0 `result.json` or C1 per-repeat nonfinal result; null when absent |
| `producer_versions` | Instance-ID-ordered ordinary manifest versions, each `{instance_id,workload_generator,confluent_kafka,librdkafka,dotnet}`; unavailable versions are null |
| `classification_final` | True exactly for a complete-valid final timeout cohort |
| `status` | `complete_valid`, `invalid` or `incomplete` |
| `timeout` | `{ok,loss,phantom,reported_failure,duplicate,reorder}` only for `complete_valid`; otherwise null |
| `reasons` | Sorted distinct permitted reason literals, at most 100, each at most 256 ASCII bytes |
| `restart` | Null for C0; the closed C1 object below |

C1 `restart` has exactly `eligibility`, `eligibility_reasons`, `termination`, `first_instance_id`, `successor_instance_id`, `timeout_instance_id`, `comparison`, `native_errors` and `arms`. Eligibility is `eligible` or `ineligible`. Termination is `{class,outcome}` from the authenticated receipt, or both null; classes are `intended_self_kill`, `no_kill`, `unexpected_exit` and `unknown`. Instance IDs may be null. Comparison is `both`, `cross_restart_only`, `same_instance_only`, `neither` or `unavailable`. Native errors are null or `{count,fatal_count,tail}`.

`arms` is null or exactly `same_instance` followed by `cross_restart`. Each arm has exactly `arm`, `cut_attempt_id`, `unconfirmed_attempt_ids`, `unconfirmed_count`, `resend_invoked`, `originals_seen`, `resends_seen`, `pair_count`, `unreported_attempts`, `unreported_seen`, `unreported_unseen`, `lost_resends` and `witness`. The cut and unconfirmed sets come from the authenticated journal/termination, not the per-repeat classification. Counts are nonnegative Int64 values or null for unknown. Witness is null or `{original_attempt_id,resend_attempt_id,original,resend}`, with each coordinate `{partition,offset}`.

The projection drops `members`, every member's copy table and counts (`copies`, `copies_truncated`, `copy_count`, `original_copies`, `resend_copies`, `other_copies`, `resend_terminal`), `problems`, `problem_details`, `window_limit`, `start_line`, `end_line`, `window_complete`, `earlier_repeat_records` and `finality`. These diagnostic fields remain in the per-repeat nonfinal contract only.

### Status and eligibility precedence

The earliest matching timeout row applies. Restart has independent reasons and never supplies timeout counts.

| Condition | Timeout status | Added reason |
| --- | --- | --- |
| Global sweep-line, ownership-collision, window-order, started-window or drain failure | All repeats `invalid` | Global reason |
| No start-line receipt | `incomplete` | `b10_final_repeat_not_started` |
| Started C1 repeat with no timeout registration, including timeout render failure | `incomplete` | `b10_final_timeout_absent` |
| C0 input failure | `invalid` | `b10_final_repeat_input_invalid` |
| C1 timeout evidence failure | `invalid` | `b10_final_timeout_evidence_invalid` |
| Foreign original owner | `invalid` | `b10_final_owner_invalid` |
| Unjoined timeout/C0 record | `invalid` | `b10_final_late_record_unjoined` |
| C1 original timeout mismatch | `invalid` | `b10_final_window_mismatch` |
| Otherwise | `complete_valid`, final, six counts | None |

Non-complete-valid rows have false finality and null timeout counts. The permitted global reason literals are `b10_final_sweep_line_invalid`, `b10_final_ownership_invalid`, `b10_final_vector_invalid`, `b10_final_start_line_invalid` and `b10_final_drain_incomplete`. Global failure makes every C1 restart ineligible with `b10_restart_sweep_unavailable`, unavailable comparison and null sweep-derived arm values. Journal-derived fields may remain known.

A C1 repeat is eligible only with a valid per-repeat object that was `pending_sweep`, no original eligibility reason, no problem except optional `b10_abrupt_verify_end_line_advanced`, a complete scan, no restart reauthentication/join/agreement failure and an available final comparison. Its original ordered eligibility reasons remain, followed by applicable new reasons: `b10_restart_sweep_unavailable`, `b10_restart_late_record_unjoined`, authenticated termination outcome and `b10_restart_input_unavailable`. A missing window in the per-repeat object still retains its original `b10_restart_window_incomplete` even when its start note supplies the scan window. An unauthenticated start note additionally makes the scan unavailable; original eligibility reasons are retained.

Without a per-repeat object, authenticated no-kill termination gives its recorded outcome as the reason, null arms and unavailable comparison. It produces no lost-resend finding. Missing first evidence gives `b10_restart_first_evidence_unavailable`; unexpected or unknown termination gives `b10_restart_termination_unverified`; other rejected intended kills give `b10_restart_input_unavailable` and any applicable termination reason.

For intended self-kill with empty kill set, `kill_too_late` remains ineligible but the authenticated same-instance arm is classified. This path requires observed exit 137, no capture/manifest gaps, empty recomputed kill set, `successor_permitted=false` and last producing-thread record `kill_intent`, with applicable checks 3 to 6, 8 to 10 and 21 to 25. Its cross-restart set is empty and every count is zero. Failed termination verification gives `b10_restart_termination_unverified`; other failed checks retain their per-repeat reasons and null arms, except native nonfatal/fatal errors alone retain computed arms. Unjoined control records null sweep-derived control values. A known control lost resend is retained as a finding even though restart is ineligible. Unknown values never create a finding.
