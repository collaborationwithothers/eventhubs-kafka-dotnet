Revision: 1

# c18-workflow-verifier: run C18 in the scenario workflow, and judge it

This plan covers an ordered set of two pull requests, entries 4 and 5 of the approved C18 split. Pull request 4, `c18-workflow`, admits C18 in the scenario workflow. In each repeat it deletes the subject's pod 60 s after the start line, records the pod's end from a watch, and archives the evidence. Pull request 5, `c18-verifier`, makes the verifier read the new records and judge them under ADR-022 "Amendment 1". It emits the fields that the approved C18 expected outcome pins.

C18 can run only after both merge. Every workflow change, policy value and verdict-relevant check below is a proposal; each is listed under "Open questions for Hari" with a recommendation.

Base commit: `main` at `7639d40826b2222c7dcc39dc14ab8145a9cf784c`. Every repository citation is at that commit.

## 1. Goal and authority

Authority, in creation order:
- the #613 issue body;
- #613 decision 7236669224, which adopts the four #597 decisions;
- #597 decision 7237271679, adopted by #613 decision 7237273680, with Hari's thumbs-up;
- #613 decisions 7237177975, 7237226936, 7238011332, [7239828212](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7239828212) and [7240022152](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7240022152), each confirmed by Hari's thumbs-up where it carries the confirmation line;
- the approvals 7237680139 (c18-stop-smoke), 7238130366 (c18-worker-stop) and [7241864958](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7241864958) (c18-scenario, revision 5 at `7ae55dc7`);
- docs/blueprint.md ADR-020 and ADR-022 with "Amendment 1" (lines 800 to 840).

Split: decision 7237226936 point 1 approves the order "(4) workflow admission and the pod delete; (5) verifier". This plan covers both as one ordered set under docs/agents/factory.md "Item lifecycle" step 3 "Split by scope". Each pull request has its own files, contracts, requirements and tests, and merges green on its own.

Inputs this plan must meet, from the approved plans and the smoke:
- The c18-scenario plan (`items/613/plan-c18-scenario.md` at `7ae55dc7`) section 5.2 pins these evidence names:
  - `reassignment.microseconds`;
  - `shutdown_margin.receipt_microseconds` and `shutdown_margin.process_end_microseconds`;
  - `counts.processed_again` and `counts.committed_never_processed`;
  - the reason `consumer_stop_receipt_missing`.

  It leaves the consumer-error count and the partition-event counts for this plan to name. Its section 12 requires that the pod's end time is read with a watch started before the delete.
- The C18 smoke (#613 comment 7239231113, artifact `c18-stop-smoke-46142138991-1`, read by this session):
  - The watch delivered `state.terminated` in 3 of 3 trials, 0.5 to 1.3 s after the delete.
  - The pod object was gone about 2.3 s after each delete.
  - The 1 s poll saw the end in 1 of 3 trials.
  - The Job ended `Failed` with `BackoffLimitExceeded`, and no replacement pod started.
  - `kubectl get pod` polls took 0.63 to 0.74 s.
- Decision 7239828212 points 2 to 4 and decision 7240022152 point 1:
  - stop 60000 ms after the start line;
  - the 30 s grace kept, no longer grace and no shorter `Close` bound;
  - a hung shutdown yields `consumer_stop_receipt_missing`, which counts as incomplete;
  - one mutation campaign after pull request 5 merges.

Order of implementation:
- pull request 4 starts after the c18-scenario pull request (entry 3) has merged on `main`;
- pull request 5 starts after pull request 4 has merged.

If either predecessor has not merged, the implementer stops (section 15).

## 2. Pull request 4, `c18-workflow`: scope

Files allowed to change:
1. `.github/workflows/scenario.yml` (protected) and `docs/operations/workflows/scenario.yml.txt`, byte for byte (AGENTS.md hard safety rule 8). C18 is added at the places listed in 3.1, and nothing else changes.
2. `infra/scripts/phase/phase.py`: `SCENARIOS` (lines 22 to 27) and `CONSUMER_SCENARIOS` (lines 28 to 29) gain C18.
3. `infra/scripts/phase/consumer_repeat.py`: the stop step of 3.2.
4. `infra/scripts/phase/pod_watch.py` (new): the `Watch`, `LogStream`, `stop_group` and `terminated_state` code, moved unchanged from `c18_stop_smoke.py` lines 90 to 100 and 140 to 263, so both modules share it.
5. `infra/scripts/phase/c18_stop_smoke.py`: imports those names from `pod_watch.py` instead of defining them. No behaviour change.
6. `infra/scripts/phase/archive.py`:
   - `CONSUMER_SCENARIOS` (line 312), `manifest_run` (lines 2160 to 2171) and the prefix map gain C18;
   - `collect_ordinary` reads the subject's pod identity from the stop evidence (3.3) when `pods.json` has no item;
   - `project_phase_status` projects the stop record (3.3).
7. `infra/scripts/lifecycle/check-offline.sh`: the pinned `scenario` options at lines 80 to 81.
8. Tests under `infra/scripts/phase/tests/` and `infra/scripts/tests/`, per 6.4.
9. `docs/operations/consumer-workflow.md` (the scenario list, the route, the order table, the records and the budget lines) and `docs/operations/c18-stop-smoke.md` (one sentence saying the watch code now lives in `pod_watch.py`).

Paths that must not change in pull request 4: `src/`, `charts/`, `scenarios/`, `tests/Parchwell.*`, `docs/blueprint.md`, every other workflow file, and `infra/ephemeral/`, `infra/persistent/` and `infra/cluster/`.

## 3. Pull request 4: external contracts

### 3.1 Workflow admission

`scenario.yml` gains `C18` exactly where it names C01 today:
- the `scenario` input options, after `C01`;
- the probe job's `PARCHWELL_PHASE` expression (line 81);
- the probe uploads (lines 223, 233, 250), as `inputs.scenario == 'C18' && 'c18-C0'`;
- the `full-C0` skip condition (line 291), as `inputs.scenario != 'C18'`;
- the `full-C3` job's `PARCHWELL_PHASE` (line 534);
- the `allowed` step's case `B04|B10|C03|C10|C01|C18)` (line 568);
- the full-C3 uploads (lines 711, 721, 738), as `inputs.scenario == 'C18' && 'c18-C1'`.

C18 therefore takes the route of C01: job `probe` runs C0, job `full-C0` is skipped, and job `full-C3` runs C1. Its artifacts are `c18-C0-...` and `c18-C1-...`. No trigger changes; the workflow keeps `workflow_dispatch` and `schedule` only (AGENTS.md hard safety rule 8).

### 3.2 The stop step in a C18 repeat

`consumer_repeat.py` keeps the order S1 to S8 and the six records per repeat (consumer-workflow.md "Order of one repeat"). For C18 only, it adds the following, with `STOP_SCENARIOS = ("C18",)` and `STOP_AFTER_START_LINE_SECONDS = 60` in the module:
1. Before S4, after readiness is observed, it reads the subject's pod: `kubectl get pods -n parchwell -l job-name=<subject job> -o json --request-timeout=30s`. Exactly one pod, in phase `Running`, is required. It records the pod's name, uid and node. Otherwise the repeat follows the existing readiness-failure path, with the new reason `consumer_stop_pod_unavailable`.
2. It starts the pod watch (`kubectl get pod <pod> -n parchwell --watch --output-watch-events -o json --request-timeout=0`) and the log stream (`kubectl logs -f -n parchwell <pod> -c enrichment-consumer --limit-bytes=1048576 --request-timeout=0`) through `pod_watch.py`, before S4.
3. When S4 returns, it records `start_line_returned_at`, the runner UTC with milliseconds. A thread waits until `start_line_returned_at + 60 s`, records `issued_at`, and runs `kubectl delete pod <pod> -n parchwell --wait=false --request-timeout=30s`, with no `--grace-period`, while S5 runs. S6 does not start until that thread has ended.
4. S6 waits for the subject Job as today. The member record's `outcome` follows the existing rule: `failed` for a Job condition `Failed=True`, which the smoke showed is what a deleted subject's Job reaches. Then the watch and the log stream are stopped.
5. Process-end confirmation (Amendment 1, "Stop and observation boundary"): the subject's process counts as ended when the watch delivered a `terminated` state for container `enrichment-consumer` of the recorded pod uid. If not, S8 is `not_attempted` with the new reason `consumer_stop_unconfirmed`, and the step exits 1.
6. The stop evidence is written to `<artifact directory>/<subject job>/pod-stop.json`, strict JSON, keys sorted, with exactly these keys:
   - `schema_version` 1 and `dialect` `c18_pod_stop`;
   - `pod`, `pod_uid` and `node`;
   - `start_line_returned_at`, `scheduled_at` and `issued_at`;
   - `delete_exit_code`;
   - `deletion_timestamp` and `deletion_grace_period_seconds`, the first values received after `issued_at`;
   - `watch_started`, `watch_events` and `watch_malformed`;
   - `terminated`: an object with `exit_code`, `signal`, `reason`, `started_at` and `finished_at`, or null;
   - `terminated_received_at` and `deleted_received_at`;
   - `log_stream_started` and `failure_line_reason`.

   Times are RFC 3339 UTC. `finished_at` keeps Kubernetes' one-second precision.
7. A failure in steps 1 to 3 never leaves the subject running. The existing `stop_members` and `delete_confirm` path deletes and confirms both Jobs (consumer_repeat.py lines 269 to 306).

For C01, C03 and C10, nothing in this step runs, and every record is byte-identical to `main`.

### 3.3 Archive

- C18 is admitted with prefix `c18` and phases C0 and C1, as C01 is.
- For a C18 subject whose `pods.json` has no item, `collect_ordinary` takes `pod` and `pod_uid` from `pod-stop.json`. Otherwise it adds no "missing job pod" gap. `consumer_member` is then called for it as for any member (archive.py lines 2028 to 2029). Without this, a deleted subject's records would not be archived, because its pod is gone before S6's collection.
- `pod-stop.json` is archived through `collector.runner` with the new kind `consumer_pod_stop`. Its keys are validated as closed against 3.2 item 6.
- A C18 subject that ends through the stop path writes its native-monitoring index (the worker completes monitoring in its shutdown). The archive does not need an expected absence for it. If the index is missing, the existing gap is recorded.

### 3.4 Budget

- The serial additions to one repeat are the pod read in 3.2 item 1, at most 30 s, and the stop of the two streams.
- The delete thread runs during S5, and its 30 s request timeout ends 90 s after S4 at the latest. That is inside S5's own worst case of 1140 s plus 90 s (consumer-workflow.md "Bounds"), so it adds nothing to the configured worst case.
- The configured worst case of a repeat therefore grows from 5042 s to 5072 s, still under the 5100 s reserve set by Hari (consumer_repeat.py lines 19 to 20). This is a sum of command timeouts, not a measurement.
- The implementer recomputes the sum by the method of the #507 plan, section 14 (Q3). If it exceeds 5100 s, the implementer stops.

## 4. Pull request 4: requirements

- R1. WHEN `scenario.yml` and its copy are compared, THEN they are byte-identical, and C18 appears exactly at the places of 3.1.
- R2. WHEN `phase.py` runs `run-repeats` for C18, THEN it routes to `consumer_repeats().run()` with the enrichment image, as for C01.
- R3. WHEN a C18 repeat reaches readiness, THEN the pod read, watch and log stream happen before S4, and the delete is issued 60 s after S4 returns, while S5 runs.
- R4. WHEN the pod read finds other than one `Running` pod, THEN the start line, load generator and verifier are `not_attempted` with reason `consumer_stop_pod_unavailable`, and both members are stopped and confirmed.
- R5. WHEN the watch delivers no `terminated` state for the recorded uid by the end of S6, THEN S8 is `not_attempted` with reason `consumer_stop_unconfirmed`, and the step exits 1.
- R6. WHEN the delete command exits non-zero, THEN `delete_exit_code` records it, and the repeat continues to S6. R5 then decides S8.
- R7. WHEN the repeat ends, THEN `pod-stop.json` exists with exactly the keys of 3.2 item 6.
- R8. WHEN the archive collects a C18 subject with an empty `pods.json` and a `pod-stop.json`, THEN `consumer_member` runs for it with the uid from `pod-stop.json`, and no "missing job pod" gap is recorded.
- R9. WHEN C01, C03 or C10 runs, THEN its records, archive and commands are unchanged.
- R10. WHEN `c18_stop_smoke.py` runs, THEN its behaviour and its tests are unchanged.

## 5. Pull request 5, `c18-verifier`: scope and contracts

### 5.1 Files allowed to change

- `src/Parchwell.Verifier/Common/Consumer/`:
  - `ConsumerArtifactCodec.cs`, `ConsumerArtifactReader.cs` and `ConsumerArtifacts.cs`;
  - `ConsumerVerificationPlan.cs`, including `IsConsumerScenario`;
  - `ScenarioConsumerVerificationPlanSource.cs`.
- `src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs` and `src/Parchwell.Verifier/VerifyRun/Consumer/ConsumerRepeatExtension.cs`.
- `src/Parchwell.Verifier/Judge/ConsumerPhaseEvidence.cs`.
- `src/Parchwell.Verifier/Common/Phase/PhaseWireCodec.cs` line 350.
- `src/Parchwell.Verifier/VerifierHostOptions.cs`, `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` and `src/Parchwell.Verifier/Program.cs`, for the two new options only.
- `src/Parchwell.Verifier/Parchwell.Verifier.csproj` (protected): one `Content` item for `scenarios/C18/scenario.yaml`.
- `charts/parchwell/templates/jobs.yaml` and `charts/parchwell/values.schema.json`: two runtime values, rendered as `Verifier__ConsumerSubjectProcessEndUtc` and `Verifier__ConsumerSubjectPodUid` for mode `verify` of C18 only, in the form of `b10_last_verifier_completed_utc` (`jobs.yaml` lines 206 to 207; schema line 126 and lines 664 to 674).
- `infra/scripts/phase/phase.py` `values`: passes `pod-stop.json`'s `terminated.finished_at` and `pod_uid` to the C18 verify Job.
- Tests under `tests/Parchwell.Verifier.Tests/`, `charts/parchwell/tests/` and `infra/scripts/phase/tests/`, per 6.5.
- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`: only the shared-input assessments that `c5_runtime.py renew` writes, with the basis text in question 6.
- `docs/operations/verifier-contract.md` lines 128 and 136 (the consumer block and projection), and `docs/operations/chart-contract.md` (the two values).

Must not change: every mutation target file (`tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` `mutate`), including `VerifyRun/VerifyRunHandler.cs`, `Judge/PhaseCandidateCodec.cs`, `Common/Results/VerifierResultCodec.cs` and `Judge/PhaseJudge.cs`. A target change cannot be renewed (verifier-mutation.md line 89), so needing one makes the implementer stop. Also unchanged: `src/Parchwell.Enrichment/`, `scenarios/`, `.github/` and `docs/blueprint.md`.

### 5.2 Records the verifier reads

- Registration: `shutdown_mode` is accepted only for a C18 subject, and must be `close` in C0 and `dispose` in C1. Any other value, or the key on any other member, is `consumer_configuration_mismatch` (Amendment 1, "Configuration fidelity").
- Row `stop_observed`: exactly `schema_version kind instance_id row_sequence event_utc`.
- Row `consumer_error`: exactly `schema_version kind instance_id row_sequence source error_code is_fatal reason event_utc`, where `source` is `consume` or `error_handler` and `reason` is a string of at most 1024 UTF-16 code units (written by `ConsumerRecordCodec.cs` lines 49 to 58).
- `stop-receipt.json`: exactly the keys `ConsumerRecordCodec.cs` lines 39 to 46 write, where `shutdown_outcome` is `ok`, `failed` or `timed_out`. Its `rows`, `record_blobs` and `messages_processed` must match the record, as the completion receipt's do (`ConsumerArtifactReader.cs` line 48), and its `stop_observed_utc` must equal the `stop_observed` row's `event_utc`. A mismatch is `consumer_record_invalid`.
- C01, C03 and C10 records keep today's closed shapes. A new key or kind there stays `consumer_record_invalid` or `consumer_registration_invalid`.

### 5.3 Validity for C18

These follow Amendment 1's "Invalid run" bullets for S equal to 1, and replace today's stop check (`ConsumerJudgement.cs` lines 48 to 50) for C18 only:
- The subject wrote a `kill_intent` row or `completion.json`, or ended without a `stop_observed` row: `consumer_stop_invalid`.
- The subject wrote more than one `stop_observed` row, or a `processed` row after its `stop_observed` row: `consumer_stop_invalid`. These are additions to Amendment 1's list; see question 4.
- The subject has a `stop_observed` row and no `stop-receipt.json`: incomplete, `consumer_stop_receipt_missing`. Invalid reasons take precedence (ADR-022, "Completion"). Among incomplete reasons, `consumer_stop_receipt_missing` is reported beside the others, which keep their existing order.
- The witness ended without `completion.json`: `consumer_stop_invalid`, as today.

### 5.4 The C18 block

For a C18 block with `status` `complete_valid`, these keys follow `counts`, in this order. Each is absent otherwise.
- `reassignment`, today's shape (`microseconds` and `unavailable_reason`). It is timed from the subject's `stop_observed` row to the earliest witness `assigned` row at or after it that holds every partition the subject held at that row (`Held(subject, stopObserved.RowSequence)`). `different_nodes` and `no_reassignment` keep their meaning (verifier-contract.md line 128).
- `shutdown_margin`: exactly `receipt_microseconds` (from the `stop_observed` row to the receipt's `receipt_written_utc`) and `process_end_microseconds` (from the `stop_observed` row to `ConsumerSubjectProcessEndUtc`). Each is an Int64, rounded down, and may be negative only for `process_end_microseconds`, because `finished_at` has one-second precision. `process_end_microseconds` is null when the option's pod uid does not equal the subject's registered `pod_uid`.
- `consumer_errors`: exactly `subject` and `witness`, each the count of that member's `consumer_error` rows.
- `partition_events`: exactly `subject` and `witness`. Each holds exactly `assigned`, `revoked` and `lost`: the counts of that member's `partition_event` rows of each kind after its first `assigned` row.
- `subject_commit_entries_after_stop`: the count of successful entries (topic `document-events`, offset at least 0, no overall and no partition error) in the subject's `commit` rows after its `stop_observed` row.

`counts` keeps its shape. `membership` and `subject_commit_coverage` stay C03-only and C10-only.

### 5.5 The judge

- J3 (`ConsumerPhaseEvidence.cs`):
  - `ReasonNames` (lines 29 to 36) gains `consumer_stop_receipt_missing`.
  - `ProjectedNames` (line 38) gains `shutdown_margin`, `consumer_errors`, `partition_events` and `subject_commit_entries_after_stop`, after `reassignment`.
  - A C18 `complete_valid` block must carry all five keys of 5.4. Any other scenario's block must carry none of the four new ones.
- J10 is unchanged. No phase total of the new keys is published, as for `reassignment`.

### 5.6 The new options

- `ConsumerSubjectProcessEndUtc` and `ConsumerSubjectPodUid` are accepted only in mode `consumer-verify` for C18, and are required there. The end time is UTC in RFC 3339 form; the uid is a GUID.
- The validator rejects them in any other mode or scenario, as it does the B10 timestamps (`VerifierHostOptionsValidator.cs` lines 25 to 27).
- `Program.cs` passes them to `ConsumerRepeatExtension` through its constructor (line 200). `VerifyRunHandler.cs` does not change.

## 6. Test maps

### 6.1 Standing fixture rule

When an existing test's fixture is rejected by a new check, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The pull request's implementation notes list each change with the check that forces it, and the cold review and Codex review check each one.

### 6.2 Assertions these plans supersede

Each assertion here pins a list that this plan extends. It changes exactly by adding C18, or the new keys and reasons, and the change is listed in the implementation notes.

Pull request 4:
- `infra/scripts/phase/tests/test_workflow.py` lines 121, 138, 140, 147, 152, 154, 340, 348, 361, 374, 378, 380, 399 and 654;
- `test_phase.py` lines 1590, 1613 to 1614 and 1632;
- `test_archive_config.py` lines 345 to 347;
- `test_archive_consumer.py` line 128;
- `check-offline.sh` lines 80 to 81.

Pull request 5:
- `tests/Parchwell.Verifier.Tests/Consumer/PhaseWireConsumerTests.cs` lines 17 and 89 to 91;
- `Host/ConsumerJudgeOptionsTests.cs` line 19;
- `Consumer/HostOptionsTests.cs` line 220;
- `Support/HostEvidenceBuilder.cs` line 70;
- `Consumer/ConsumerJudgementTests.cs` lines 422, 526 and 613 to 616;
- `Judge/ConsumerPhaseJudgeTests.cs` lines 462, 508 and 724.

### 6.3 Cap

At most 30 changed assertions per pull request under 6.1 and 6.2 together. The lists in 6.2 alone are about 24 assertion lines for pull request 4 and 16 for pull request 5, counted from the lines cited. Exhaustion action: push -wip and hand off to the next session (AGENTS.md "Before opening a pull request" rule 11).

### 6.4 Pull request 4 tests

| Requirement | Test | Inputs | Literal expectation |
|---|---|---|---|
| R1 | `test_workflow.py`, the existing route and admission tests extended | the committed workflow | the C18 entries of 3.1, and byte equality with the copy |
| R2 | `test_phase.py`, a new case | `run-repeats` for C18 C0 with stub commands | `consumer_repeats().run()` is called, and the images built are `loadgen`, `verifier` and `enrichment` |
| R3 | `test_consumer_repeat.py::C18StopTests.test_order_and_delay` | fake clock; S4 returns at t = 100.000 s | the pod read, watch and log commands precede S4; delete argv `["kubectl", "delete", "pod", <pod>, "-n", "parchwell", "--wait=false", "--request-timeout=30s"]` at t = 160.000 s, while S5's stub is still running; S6 after the delete thread |
| R4 | `C18StopTests.test_pod_unavailable` | the pod read returns 0 items; then 2 items; then 1 `Pending` item | in each case the start line, load generator and verifier are `not_attempted` with reason `consumer_stop_pod_unavailable`, and both Jobs are deleted and confirmed |
| R5 | `C18StopTests.test_unconfirmed_end` | the watch stub prints events with no `terminated` state | verify is `not_attempted` with reason `consumer_stop_unconfirmed`; exit 1 |
| R6 | `C18StopTests.test_delete_failure` | the delete stub exits 1 | `delete_exit_code` 1; S6 runs; then R5 decides S8 |
| R7 | `C18StopTests.test_evidence_keys` | a full stubbed repeat, with the watch event `terminated.finishedAt` `2026-11-28T10:00:02Z`, exit code 1 | `pod-stop.json` keys equal 3.2 item 6 exactly; `terminated.finished_at` is `2026-11-28T10:00:02Z` |
| R8 | `test_archive_consumer.py`, a new case | a C18 subject with `pods.json` `{"items": []}` and a `pod-stop.json` | `consumer_member` is called with that uid; no `missing job pod` gap |
| R9 | the existing consumer repeat and archive tests | C01, C03 and C10 | unchanged, except 6.2 |
| R10 | the existing `test_c18_stop_smoke.py` and `test_c18_stop_smoke_workflow.py` | as on `main` | pass unchanged |

### 6.5 Pull request 5 tests

| Contract | Test | Inputs | Literal expectation |
|---|---|---|---|
| 5.2 | `ConsumerStopRecordTests.Codec` | each new row, the registration with `shutdown_mode`, and the receipt, as the enrichment codec writes them; then each with one extra key, then one missing key | the first parse; each variant gives `consumer_record_invalid`, or `consumer_registration_invalid` for the registration |
| 5.2 | `ConsumerStopRecordTests.Mode_mismatch` | a C0 subject registered with `dispose`; a witness with `shutdown_mode` | `consumer_configuration_mismatch` in each case |
| 5.2 | `ConsumerStopRecordTests.Receipt_disagrees` | receipt `rows` one less than the record; receipt `stop_observed_utc` 1 tick off | `consumer_record_invalid` in each case |
| 5.3 | `ConsumerStopJudgementTests.Endings` | subject: with `kill_intent`; with `completion.json`; with no `stop_observed`; with two `stop_observed`; with a `processed` row after `stop_observed`; with `stop_observed` and no receipt; witness with no completion | `consumer_stop_invalid` five times; `incomplete` with `consumer_stop_receipt_missing`; `consumer_stop_invalid` |
| 5.4 | `ConsumerStopJudgementTests.Reassignment` | subject holds partitions 0 to 15 at `stop_observed` 10:00:00.0000000Z and then writes `revoked` [0..15] at 10:00:01.0000000Z; witness `assigned` [0..31] at 10:00:04.5000000Z; same node | `reassignment.microseconds` 4500000; with the members on different nodes: null, `different_nodes` |
| 5.4 | `ConsumerStopJudgementTests.Margins` | `stop_observed` 10:00:00.2500000Z; receipt written 10:00:01.0000000Z; process end option `2026-11-28T10:00:01Z` with the matching uid; then a different uid | `receipt_microseconds` 750000, `process_end_microseconds` 750000; then `process_end_microseconds` null |
| 5.4 | `ConsumerStopJudgementTests.Counts` | subject: 2 `consumer_error` rows; `assigned`, `revoked` after its first `assigned`; 3 successful commit entries after `stop_observed`, one with partition error 1; witness: 0 errors, 1 `assigned` after its first | `consumer_errors` {subject 2, witness 0}; `partition_events.subject` {assigned 1, revoked 1, lost 0} (the second `assigned` counts, the first does not); `subject_commit_entries_after_stop` 2 |
| 5.4 | `ConsumerStopJudgementTests.Key_order` | a `complete_valid` C18 block | its keys, in order: the nine fixed keys, then `counts`, `reassignment`, `shutdown_margin`, `consumer_errors`, `partition_events`, `subject_commit_entries_after_stop` |
| 5.5 | `ConsumerPhaseJudgeTests`, new cases | a C18 block missing `shutdown_margin`; a C01 block with `consumer_errors` | J3 fails each slot |
| 5.6 | `HostOptionsTests`, new cases | the two options in `consumer-verify` C18; missing; in `consumer-verify` C01; in `start-line` C18 | valid; then rejected three times |
| chart | `charts/parchwell/tests/test_consumer_chart.py`, new case | C18 verify with both values; C01 verify with them | the two env vars are rendered; the C01 render fails |
| R9 kept | the existing consumer verifier tests | C01, C03 and C10 | pass, except 6.2 |

## 7. Deployment assumptions

Each says how its evidence was taken.
- The watch delivers the subject container's `terminated` state before the pod is removed. Smoke run 46142138991: fresh watch, idle member, 3 of 3 trials. Under load this is not measured. R5 makes a failure visible as `consumer_stop_unconfirmed`, so the repeat is not counted.
- A deleted subject's Job reaches `Failed=True` and starts no replacement pod. The same smoke, 3 of 3 trials.
- The scenario workflow's identity may delete, watch and stream logs for pods in `parchwell`. The smoke ran in the same environment, `parchwell-scenarios`, and did all three. That the two workflows use one identity is inference from the shared environment name. It is not read from the role assignment, which is outside this repository.
- A hung `Close`, `Dispose` or receipt write is killed by the 30 s grace and leaves the repeat uncounted. Accepted by name in decision 7239828212 point 3 and decision 7240022152 point 1.
- The runner's clock and the cluster's clock are close enough that `process_end_microseconds`, computed from the node's `finishedAt` against the worker's `stop_observed` time, means something. Not measured. The value is recorded only, and question 5 asks Hari to accept that.

## 8. Acceptance commands

Pull request 4:
1. `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` -> all pass.
2. `bash infra/scripts/lifecycle/check-offline.sh` -> exit 0.
3. `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt` -> no output.
4. `git diff --name-only origin/main...HEAD` -> only section 2 files.
5. An ASCII scan of the changed files.

Pull request 5:
1. `dotnet build -warnaserror` -> 0 warnings, 0 errors.
2. `dotnet test` over the solution -> all pass.
3. `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base <merge base> --head "$(git rev-parse HEAD)" --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` -> accepted, after the renewal.
4. `bash infra/scripts/lifecycle/check-offline.sh` -> exit 0.
5. `git diff --name-only origin/main...HEAD` -> only 5.1 files.
6. An ASCII scan of the changed files.

## 9. Plan-check answers

(a) Effort that scales with an unknown count:
- the changed existing assertions, capped at 30 per pull request with exhaustion action "push -wip and hand off" (6.3);
- the mutation campaign, already capped by decision 7239828212 point 4.

(b) Stricter than the parent:
- two invalid conditions beyond Amendment 1's list, more than one `stop_observed` row and a `processed` row after it (5.3; question 4);
- the end-time binding to the registered pod uid (5.4);
- `consumer_stop_pod_unavailable` and `consumer_stop_unconfirmed` (3.2), which make a repeat that cannot meet Amendment 1's boundary visible instead of judged.

(c) Tests that depend on code, data or behaviour neither on `main` nor in the pull request:
- pull request 4 depends on the c18-scenario pull request for C18's scenario file and admission;
- pull request 5 depends on pull request 4's `pod-stop.json`.

Both gates are in section 1. The live behaviours are in section 7.

## 10. Open questions for Hari

1. Approve the workflow change of 3.1 and 3.2: C18 admitted on C01's route, and the stop step inside each C18 repeat. It is a protected-path change to `scenario.yml`. Recommendation: approve. It changes no trigger and no step of any other scenario.
2. The anchor of "60000 ms after the start line". Recommendation: the runner's UTC when the start-line step returns, plus 60 s, with both times recorded in `pod-stop.json`. The start-line note's own time is written by the verifier pod's clock, which the runner cannot schedule against without a clock comparison.
3. New workflow reasons and behaviour: `consumer_stop_pod_unavailable` and `consumer_stop_unconfirmed`, and no verifier run when the end is not confirmed. Also the budget: the reserve stays 5100 s, because the configured worst case becomes 5072 s (3.4, computed from timeouts). Recommendation: approve.
4. Verdict-relevant checks in pull request 5 beyond Amendment 1's list: more than one `stop_observed` row, or a `processed` row after it, is `consumer_stop_invalid`. Recommendation: approve. Each breaks Amendment 1's "consumes no further message".
5. Evidence field names and shapes: `consumer_errors`, `partition_events`, `subject_commit_entries_after_stop` and the `shutdown_margin` rules. That includes accepting the one-second precision of `process_end_microseconds`, its comparison of two clocks, and its null when the pod uid differs. Recommendation: approve. They are recorded only.
6. The qualification renewal basis for pull request 5: "Changes consumer verifier files that are shared inputs only; no mutation target source changes, and the consumer judgement for C01, C03 and C10 is unchanged (plan c18-workflow-verifier, R9 kept), so the measured mutation runtime does not change." Recommendation: approve. The campaign itself is already decided (decision 7239828212 point 4).

## 11. Deferred work

- The mutation campaign, already decided as execution after pull request 5 merges (decision 7239828212 point 4).
- The C18 run: two configurations, five repeats each, dispatched on `main` after both pull requests merge. On the verdict path. Dispatching it is Hari's decision (standing decision point 3). Responsible: the control session. Cap: one dispatch, under the c18-scenario plan's expected outcome; a failed or incomplete run goes back to Hari. Gate it blocks: the C18 verdict pull request (entry 5 of the #597 split). Completion evidence: the run and its artifacts linked on #613.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" in any of these cases:
- it needs to change a contract, a requirement or the scope of its pull request;
- it needs to touch a mutation target;
- the recomputed budget exceeds 5100 s;
- a predecessor in section 1 has not merged.

## 13. Size estimate

Estimate, dated 2026-11-28, from the change points the readers mapped:
- pull request 4: about 500 to 800 changed lines in 12 to 16 files, half of them tests;
- pull request 5: about 700 to 1100 changed lines in 18 to 25 files, most of them tests.

Not measured; reported, not enforced.

## 14. Sources

Repository files at the base commit, read by this session and by two reader subagents whose claims this session spot-checked on 2026-11-28. Those were `phase.py` lines 22 to 29, `archive.py` lines 312 and 2160 to 2171, `ConsumerJudgement.cs` lines 169 to 176, `ConsumerPhaseEvidence.cs` lines 29 to 38, `Program.cs` line 200, the mutation target list, and consumer-workflow.md line 40. The smoke artifact was read through the GitHub artifacts API. The Kubernetes facts are the c18-stop-smoke plan's sources S2, S3 and S5.

## 15. Stop rule for both pull requests

Section 12 applies to each pull request separately.
