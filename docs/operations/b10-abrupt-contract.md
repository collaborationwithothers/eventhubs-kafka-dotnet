# B10 abrupt diagnostic contract

The B10 diagnostic trial runs an ordinary timeout producer, a parent with a self-killing producer child, and a fresh successor in sequence. The child records its probes in a local journal; the parent publishes that journal after observing the child's death. This execution is diagnostic-only, inactive for scientific publication.

[ADR-021](../blueprint.md#adr-021-b10-accepts-timed-out-sends-application-retries-and-one-producer-restart-in-a-second-job-and-classifies-late-appends-up-to-a-sweep-line) owns the restart protocol and scientific rules. The [approved part 1 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/fc1a4f36452b2d302f1d73987e1ba5eb967b3d65/items/404/plan-b10-execution-loadgen.md), section 3, owns the LoadGen and schema-3 interfaces below. The [approved part 2 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/0c73e5139e1670d5b675a51fc52dbd99c4b1af49/items/404/plan-b10-execution-trial.md), section 3, owns trial orchestration and collection. These replace the stop/checkpoint/pipe protocol; the [workflow page](b10-workflow.md#diagnostic-trial-inactive-for-science) records dispatch and trial limits.

## Processes and configuration

Part 1 sections 3.1 and 3.2 own these commands, exit codes and settings. Parent and child share the restart pod's producer identity. The parent constructs no Kafka producer and starts only the fixed child apphost.

| Process | Command | Exit codes |
| --- | --- | --- |
| Parent | `/app/Parchwell.LoadGen --b10-abrupt-parent` | `0`: `termination.json` created and read back, whatever its termination class; `1`: failure before that; `124`: the existing 900 s watchdog |
| Child | `<running apphost directory>/Parchwell.LoadGen --b10-abrupt-child` | `137`: required local exit observation for intended self-kill; `3`: no-kill path; `4`: self-kill call returned; `1`: failure before the first journal record |
| Successor | `/app/Parchwell.LoadGen --b10-abrupt-successor` | `0`: `completion.json` published; `1`: failure |
| Validation | `--validate-b10-abrupt-configuration` | `0` with `{"b10_abrupt_configuration_valid":true}`; otherwise `1` |

`--b10-abrupt-supervisor` is removed and rejected as an unknown argument. Settings come only from environment variables with prefix `B10Abrupt__`. The closed identity/path keys are `Enabled`, `Purpose`, `Role`, `ScenarioId`, `ConfigurationId`, `Phase`, `Repeat`, `RunId`, `SourceCommit`, `ImageReference`, `JobName`, `PodName`, `PodUid`, `NamespaceName`, `BlobEndpoint`, `ScenarioSourceRoot`, `OutputRoot`, `StartLineBindingPath` and `PredecessorBindingPath`.

Required values are `Enabled=true`, `Purpose=diagnostic_trial`, `ScenarioId=B10`, `ConfigurationId=C1`, `Phase=C1` and `Repeat=1`. Role is `parent`, `child` or `successor`; `supervisor` is rejected with `validation_detail=Role`. Parent and child use `StartLineBindingPath`; only the successor uses `PredecessorBindingPath`. The parent passes its settings to the child with role `child`, adding no other key; the child refuses `PredecessorBindingPath`.

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

## Trial order and Job identities

Part 1 section 3.3 owns the names. Part 2 sections 3.1, 3.2 and 3.4 own order, timeout outcomes and run ID. The trial run ID is `b10d-{GITHUB_RUN_ID}-{GITHUB_RUN_ATTEMPT}`; scientific B10 retains prefix `b10`. Job names, configmaps and Blob prefixes derive from the run ID.

After `{run}-c1-r1-start-line` completes and its metadata is checked, the trial runs:

| Order | Job | Execution |
| --- | --- | --- |
| 1 | `{run}-c1-r1-loadgen` | Ordinary C1 timeout host, mode `loadgen`, no `B10Abrupt__` setting or abrupt binding configmap |
| 2 | `{run}-c1-r1-loadgen-restart` | Abrupt parent and child, mode `loadgen-restart` |
| 3 | `{run}-c1-r1-loadgen-successor` | Abrupt successor, mode `loadgen-successor`, admitted only through `release_successor` |

At most one producer pod runs at a time. Each Job is waited for before the next is applied, and its pod is observed terminated before the next Job is rendered. Finished Jobs and their terminated pods are not deleted until cleanup.

The [Kubernetes Job page, Terminal Job conditions](https://kubernetes.io/docs/concepts/workloads/controllers/job/#terminal-job-conditions), read 2026-11-11, states that from v1.31 terminal conditions are added only after all Job pods terminate. [The cluster version validation](../../infra/cluster/variables.tf) requires at least 1.35.0. The trial checks pod termination directly instead of relying on terminal conditions.

After timeout wait outcome `complete` or `failed`, before any later render, the trial reads its pods once: `kubectl get pods -n parchwell -l job-name={run}-c1-r1-loadgen -o json --request-timeout=30s`, bounded to 30 s, saving output to `b10-abrupt-diagnostic/loadgen-pods.json`. It requires exit 0, decoded JSON, exactly one pod, exactly one controller owner of kind `Job` with the expected name, one container and one status named `loadgen`, the phase's loadgen image, and `state.terminated` with integer `exitCode`. Any failed condition or read exception makes the outcome `unknown`.

| Timeout outcome | Trial action |
| --- | --- |
| `complete` or `failed`, pod check passed | Retain the record and continue to the restart Job |
| `unknown` or `apply_failed` | Stop further Jobs, set `safety_stop`, record `b10_abrupt_timeout_job_unknown` at `timeout_job`, return 1 |
| `render_failed` | Stop further Jobs, leave `safety_stop` unchanged, record `b10_abrupt_configuration_invalid` at `timeout_job`, return 1 |

An exception in the timeout-Job step records fallback `b10_abrupt_publication_failed` at boundary `timeout_job` and starts no later Job. A failed timeout Job does not repair or fail the restart chain; ADR-021 protocol rule 12 owns independent per-instance checks. All Jobs share the existing 18000 s orchestration deadline. No log collection delays successor release.

The start line still expects `{run}-c1-r1-loadgen` and `{run}-c1-r1-loadgen2`; the trial never runs the latter Job. [Decision 6787849492, point 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787849492) accepts that gap against ADR-021's consequences until the verifier classification pull request supplies all three C1 names.

## Schema-3 envelope and journal

Part 1 sections 3.4 and 3.5 own the closed formats. Every abrupt JSON object and journal record carries `schema_version: 3`, `dialect: "b10_abrupt"`, `purpose: "diagnostic_trial"`, `synthetic: true`, `scientific_admissible: false`, `kind`, `identity` and `data`. New readers reject schema 2, duplicate or unknown members, trailing content and depth above 32. UTC strings use the exact 33-character `O` representation ending `+00:00`.

Identity has exactly `run_id`, `scenario_id`, `configuration_id`, `phase`, `repeat`, `source_commit`, `image_reference`, `job_name`, `pod_name`, `pod_uid`, `instance_id` and `predecessor_instance_id`. Instance ID equals pod name. Parent and child share identity; only the successor has a non-null predecessor instance ID.

Only the child writes `<OutputRoot>/abrupt/journal.jsonl` (`/work/loadgen/abrupt/journal.jsonl` in the chart), opened create-new. Each UTF-8 JSON record ends in byte `0x0A`. Besides the envelope, a record has `sequence`, `observed_utc`, `monotonic_ticks` and `clock_frequency`. Sequence starts at 1 and increments under one lock shared by both writing threads. The child calls `FileStream.Flush()` after each record before the next record or action.

| Kind | Writer and `data` |
| --- | --- |
| `child_started` | Producing thread, first record: `pid`, `start_time_ticks`, `boot_id` |
| `prepared` | Producing thread, before `Produce`: `attempt_id`, `arm`, `role`, `ordinal`, `probe_id`, `key`, `sequence`, `payload_length`, `payload_sha256`, `predecessor_attempt_id` |
| `pre_acceptance_report` | Producing thread, after ordinal 0's `prepared` and before its `accepted`: `arm`, `attempt_id`, `name`, `length`, `sha256`, `report_identity`, `report_sequence`, `report_capture_utc` |
| `accepted` | Producing thread, after `Produce` returns: `attempt_id`, `prepared_sequence` |
| `synchronous_rejection` | Producing thread, when `Produce` throws: `attempt_id`, `prepared_sequence`, `error_code`, `error_reason` |
| `terminal` | Journal writer thread: `attempt_id`, `completion_source`, `delivered`, `error_code`, `persistence_status` |
| `native_error` | Journal writer thread: `capture_sequence`, `error_code`, `error_name`, `is_error`, `is_fatal`, `callback_monotonic_ticks` |
| `cut` | Producing thread, control arm after ordinal 4's `accepted`: `arm`, `cut_attempt_id`, `release_utc`, `unconfirmed_attempt_ids` |
| `control_drained` | Producing thread: `flush_returned`, `out_queue_length` |
| `post_drain_report` | Producing thread, after `control_drained`: same descriptor fields as `pre_acceptance_report`, with arm `same_instance`, null attempt ID and name `control-post-drain-report.json` |
| `kill_intent` | Producing thread, cross-restart arm after ordinal 4's `accepted`: `arm`, `cut_attempt_id`, `release_utc` |
| `signal_failed` | Producing thread, only when the kill call returns after `kill_intent`: `return_value`, `errno` |
| `no_kill` | Producing thread: `reason`, `cross_restart_admitted` |
| `gap` | Either thread when writing remains possible: `reason`, `first_missing_sequence` (nullable), `capture_status` |

Arms are `same_instance` and `cross_restart`; roles are `original` and `resend`. Original ordinals run from 0 to 4, with sequence equal to ordinal; a resend keeps its original's ordinal. Original predecessor attempt IDs are null. Pre-acceptance report names are `<arm>-pre-acceptance-report.json`. Report identity, sequence, capture time, length and hash use part 1 section 3.5's descriptor contract: report identity has `scenario_id`, `run_id`, `role` and `client_id`, with role `LoadGenProducer`; report length is 1 to 1048576 bytes and report sequence is positive. Native-error `capture_sequence` starts at 1 without gaps.

The control set is computed and appended atomically with `cut`, under the journal lock: accepted control originals without a terminal or synchronous rejection before `cut`, in invocation order. A terminal appended after that lock is released cannot remove a member. Readers recompute the set from journal order and require equality with `unconfirmed_attempt_ids`.

Under ADR-021 protocol rule 6, the kill unconfirmed set is the cross-restart arm's accepted originals without a complete terminal record anywhere in the journal at death, in invocation order. The parent and successor recompute it from the same journal reader.

Limits are charged before writing: record JSON at most 65536 bytes excluding its newline, journal at most 16777216 bytes including newlines, at most 4096 records. Callbacks only enqueue into the bounded 256-entry memory queue and perform no file or network I/O. Capture gaps are sticky. Gap reasons are `frame_oversize`, `queue_full`, `journal_bytes_exceeded`, `journal_entries_exceeded`, `writer_failed`, `malformed_frame` and `sequence_gap`. A torn final line or malformed line gives `malformed_frame`; noncontiguous sequence gives `sequence_gap`.

## Probe sequence

Part 1 section 3.6 owns this fixed policy sequence. The workload is synthetic; the rate is policy, not a measurement.

1. Write `child_started`, construct the B10 C1 producer and wait for a parsed statistics report.
2. Invoke control originals 0 to 4. Invocation `i` is no earlier than `i * 10 ms` after ordinal 0's invocation (100 originals per second). After ordinal 4's acceptance, write `cut` atomically with its control set; release is that record's UTC plus 120 s. An empty set takes `no_kill` with `b10_abrupt_exposure_inconclusive`.
3. At control release, resend each member once in invocation order with a new attempt ID and the same instance, key, sequence and payload bytes, even if it has since completed.
4. Flush within the existing 300 s producer-drain budget, write `control_drained`, then bind the first parsed statistics report captured after drain. Continue only with `flush_returned=true`, queue length 0, a terminal for every control original and resend, and that report. Otherwise take `no_kill` with `b10_abrupt_control_incomplete`.
5. Invoke cross-restart originals 0 to 4 at the same pacing. After ordinal 4's acceptance, write `kill_intent` and call `kill(getpid(), 9)` once. Its release is that record's UTC plus 120 s. After this arm's first original is admitted, call no `Flush` or `Dispose`. A kill call that returns writes `signal_failed` with return value and errno, then exits 4.
6. Failure before `kill_intent` writes `no_kill` best effort. Before cross-restart admission, flush within the existing producer-drain budget, dispose and exit 3; after admission, use `Environment.Exit(3)` without flush or disposal.

The keys/probe IDs are `B10/{run_id}/C1/1/same_instance` and `B10/{run_id}/C1/1/cross_restart`. Each arm uses the first generated profile event's payload bytes for every attempt. If ordinal 4 is synchronously rejected, no cut is written: the cross-restart arm with no accepted original gives `kill_too_early`; otherwise `b10_abrupt_cut_rejected`. Rejections of ordinals 0 to 3 are recorded and execution continues.

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

Part 1 section 3.11 owns successor admission and publication. The phase checks first-pod termination and accepts its publication before `release_successor` creates the successor binding configmap. Binding has exactly `first_pod`, `termination`, `start_line_binding`, `release_utc`, `runner_utc`, `runner_monotonic_ticks` and `cross_host_timing`. `first_pod` holds `job_name`, `pod_name`, `pod_uid`, `image_reference`, `source_commit` and `finished_utc`; `termination` is the termination-file descriptor. Cross-host timing remains `not_verified`.

The successor downloads `termination.json`, `abrupt-manifest.json` and `journal.jsonl` from the first pod's Blob prefix. It checks the hash/length chain, exact identities and `successor_permitted=true`, recomputes the kill unconfirmed set and requires equality with the termination receipt. It requires predecessor Job `{run}-c1-r1-loadgen-restart` and binding release equal to `kill_intent.data.release_utc`; stale selection or release gives binding detail `selection_stale`.

After registration with the first pod as predecessor, it waits for that release and resends each kill-set member once in invocation order, preserving key, sequence and payload hash with new instance and attempt IDs. Missing readiness at release gives `b10_abrupt_successor_late` and no resend. It drains and publishes `registration.json`, `ledger.jsonl`, `manifest.json`, `statistics.jsonl`, `request-lifecycle.jsonl`, `capabilities.json` and `completion.json` under its diagnostic prefix. Completion binds `first_termination`, `journal`, `kill_unconfirmed_attempt_ids`, registration and artifacts, readiness/release, invocations, cleanup and `cross_host_timing=not_verified`.

## Failure names and collection

Part 1 section 3.9 owns the retained names and outcomes; part 2 section 3.3 adds only `b10_abrupt_timeout_job_unknown`:

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
```

Retired names are `b10_abrupt_target_invalid`, `b10_abrupt_stop_unconfirmed`, `b10_abrupt_resume_unconfirmed` and `b10_abrupt_eof_incomplete`. The closed `no_kill.reason` set is `b10_abrupt_exposure_inconclusive`, `b10_abrupt_control_incomplete`, `kill_too_early`, `b10_abrupt_cut_rejected`, `b10_abrupt_sample_missing`, `b10_abrupt_report_binding_invalid`, `b10_abrupt_guard_failed`, `b10_abrupt_signal_preflight_failed`, `b10_abrupt_deadline_exhausted` and `b10_abrupt_capture_gap`. A returned kill call is not a no-kill reason.

Part 2 section 3.5 owns collection. For each applied `loadgen`, `loadgen-restart` and `loadgen-successor` state record, `collect-b10-abrupt-trial` runs `collect-job` and copies `artifacts/<job>/` to `artifacts/b10-abrupt-diagnostic/<job>/`. The start-line record is not copied. The workflow uploads only `b10-abrupt-diagnostic`. The timeout Job receives no successor-receipt check, ordinary verify or evidence check beyond its recorded outcome. Collection exit 124 records `b10_abrupt_deadline_exhausted`; other nonzero exits record `b10_abrupt_publication_ambiguous`; copy exceptions record `b10_abrupt_publication_failed`, all at `job_collection`.

The ordinary timeout host publishes under `producer/`. Part 2 section 3.7's production-byte export/checker verifies that scientific paths reject its evidence in frozen diagnostic trial state and that the exported documents contain no diagnostic marker keys. The phase test `test_trial_and_science_run_ids_never_collide` (part 2 section 8.1, T-P1) verifies distinct trial and scientific run-ID prefixes and noncollision. This trial produces no scientific candidate. The [verifier contract](verifier-contract.md) records the later verifier classification support.
