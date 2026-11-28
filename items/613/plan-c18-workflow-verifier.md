Revision: 4

Changes since revision 3, applying [decision 7242811142](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7242811142) and nothing else. The decision answers the [revision 3 check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7242432351) and authorizes this one revision and one fresh check beyond the `plan-pr` check cap. It carries no confirmation line because it only adds scope.
- Decision point 1 (check finding 1): the archive keeps a C18 subject's `stop-receipt.json` with the new kind `consumer_stop_receipt` (3.3, R16, 6.4). `docs/operations/archive-smoke.md` "Phase evidence retention" (line 107) joins section 2's scope.
- Decision point 2 (check note N3): a stopped subject's `members[].ending` is `stop_receipt` (5.4, V16, 6.5).
- Decision "Route" (`apply in implementation`, notes N1, N2 and N4): the event that supplies `terminated` is pinned (3.2 item 5, R5's test); the Job page is cited at lines 501 to 503; `pod_watch.py` also takes `CONTAINER`, `WATCH_BYTE_LIMIT` and `utc()`.
- Base unchanged: `main` at `6be908bf`, still its head.

# c18-workflow-verifier: run C18 in the scenario workflow, and judge it

This plan covers an ordered set of two pull requests, entries 4 and 5 of the approved C18 split. Pull request 4, `c18-workflow`, admits C18 in the scenario workflow. In each repeat it deletes the subject's pod 60 s after the start line, records the pod's end from a watch, and archives the evidence. Pull request 5, `c18-verifier`, makes the verifier read the new records and judge them under ADR-022 "Amendment 1". It emits the fields that the approved C18 expected outcome pins.

C18 can run only after both merge. Every workflow change, policy value and verdict-relevant check below was approved in [decision 7242272268](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7242272268) (section 10).

Base commit: `main` at `6be908bf1a46910d0ad80b574073364a1fae3e40`. Every repository citation is at that commit.

## 1. Goal and authority

Authority, in creation order:
- the #613 issue body;
- #613 decision 7236669224, which adopts the four #597 decisions;
- #597 decision 7237271679, adopted by #613 decision 7237273680, with Hari's thumbs-up;
- #613 decisions 7237177975, 7237226936, 7238011332, [7239828212](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7239828212) and [7240022152](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7240022152), each confirmed by Hari's thumbs-up where it carries the confirmation line;
- [decision 7242272268](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7242272268), which answers revision 2 of this plan, confirmed by Hari's thumbs-up (2026-11-28T16:29:21Z, reported by `scripts/decision-log.py` run from `main`);
- [decision 7242811142](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7242811142), which answers revision 3 of this plan; it adds scope only and carries no confirmation line;
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

If either predecessor has not merged, the implementer stops (section 12).

## 2. Pull request 4, `c18-workflow`: scope

Files allowed to change:
1. `.github/workflows/scenario.yml` (protected) and `docs/operations/workflows/scenario.yml.txt`, byte for byte (AGENTS.md hard safety rule 8). C18 is added at the places listed in 3.1, and nothing else changes.
2. `infra/scripts/phase/phase.py`: `SCENARIOS` (lines 22 to 27) and `CONSUMER_SCENARIOS` (lines 28 to 29) gain C18.
3. `infra/scripts/phase/consumer_repeat.py`: the stop step of 3.2.
4. `infra/scripts/phase/pod_watch.py` (new): the `Watch`, `LogStream`, `stop_group` and `terminated_state` code, moved unchanged from `c18_stop_smoke.py` lines 90 to 100 and 140 to 263, with the names that code uses from outside those ranges, `CONTAINER` (line 23), `WATCH_BYTE_LIMIT` (line 38) and `utc()` (line 67), so both modules share it.
5. `infra/scripts/phase/c18_stop_smoke.py`: imports those names from `pod_watch.py` instead of defining them. No behaviour change.
6. `infra/scripts/phase/archive.py`:
   - `CONSUMER_SCENARIOS` (line 312), `manifest_run` (lines 2160 to 2171) and the prefix map gain C18;
   - `collect_ordinary` reads the subject's pod identity from the stop evidence (3.3) when `pods.json` has no item;
7. `infra/scripts/lifecycle/check-offline.sh`: the pinned `scenario` options at lines 80 to 81.
8. Tests under `infra/scripts/phase/tests/` and `infra/scripts/tests/`, per 6.4.
9. `docs/operations/consumer-workflow.md` (the scenario list, the route, the order table, the records and the budget lines) `docs/operations/c18-stop-smoke.md` (one sentence saying the watch code now lives in `pod_watch.py`), and `docs/operations/archive-smoke.md` "Phase evidence retention" (line 107): C18's admission, the kinds `consumer_pod_stop` and `consumer_stop_receipt`, `pods.stop.json`, the streamed log and the gap condition of 3.3.

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

C18 therefore takes the route of C01: job `probe` runs C0, job `full-C0` is skipped, and job `full-C3` runs C1. Its artifacts are `c18-C0-...` and `c18-C1-...`. No trigger changes; the workflow keeps its one trigger, `workflow_dispatch` (`scenario.yml` line 4; AGENTS.md hard safety rule 8).

### 3.2 The stop step in a C18 repeat

`consumer_repeat.py` keeps the order S1 to S8 and the six records per repeat (consumer-workflow.md "Order of one repeat"). For C18 only, it adds the following, with `STOP_SCENARIOS = ("C18",)` and `STOP_AFTER_START_LINE_SECONDS = 60` in the module:
1. Before S4, after readiness is observed, it reads the subject's pod: `kubectl get pods -n parchwell -l job-name=<subject job> -o json --request-timeout=30s`. Exactly one pod, in phase `Running`, is required. It records the pod's name, uid and node. Otherwise the repeat follows the existing readiness-failure path, with the new reason `consumer_stop_pod_unavailable`.
2. It starts the pod watch (`kubectl get pod <pod> -n parchwell --watch --output-watch-events -o json --request-timeout=0`) and the log stream (`kubectl logs -f -n parchwell <pod> -c enrichment-consumer --limit-bytes=1048576 --request-timeout=0`) through `pod_watch.py`, before S4.
3. When S4 returns, it records `start_line_returned_at`, the runner UTC with milliseconds. A thread waits until `start_line_returned_at + 60 s`, records `issued_at`, and runs `kubectl delete pod <pod> -n parchwell --wait=false --request-timeout=30s`, with no `--grace-period`, while S5 runs. S6 does not start until that thread has ended.
4. S6 waits for the subject Job as today. The member record's `outcome` follows the existing rule: `failed` for a Job condition `Failed=True`. A deleted subject's Job reaches that condition: with no pod failure policy, "the Job controller counts a terminating Pod as an immediate failure, even if that Pod later terminates with `phase: "Succeeded"`" (Kubernetes Job page, kubernetes/website `38552b318453cbf9e282f36262a6f163bb7a8017`, `job.md` lines 501 to 503), and the chart sets `backoffLimit: 0`. The smoke saw `Failed` with `BackoffLimitExceeded` in 3 of 3 trials. Then the watch and the log stream are stopped.
5. Process-end confirmation (Amendment 1, "Stop and observation boundary"): the subject's process counts as ended only when all three hold: the delete was issued (`issued_at` not null); `delete_exit_code` is 0; and the watch delivered a `terminated` state for container `enrichment-consumer` of the recorded pod uid, received after `issued_at` (`terminated_received_at` later than `issued_at`). Otherwise S8 is `not_attempted` with the new reason `consumer_stop_unconfirmed`, and the step exits 1. A subject that ends by itself, at its window end or before the delete, is therefore never confirmed (decision 7242272268 point 1). `terminated` and `terminated_received_at` come from the first watch event, in receipt order, whose object has a `terminated` state for container `enrichment-consumer`, as `c18_stop_smoke.py` line 578 does; a later event that repeats that state never replaces it.
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
   Two more files go beside it, in the layout `collect-job` uses (`lifecycle.sh` lines 298 and 333):
   - the log stream as `<artifact directory>/<subject job>/<pod>.containers.enrichment-consumer.log`, the leaf `<pod>.<kind>.<container>.log` that the archive reads for a declared container (`archive.py` lines 1777 to 1780), capped at 1048576 bytes by `--limit-bytes`;
   - the last pod object the watch delivered before its `DELETED` event, as `<artifact directory>/<subject job>/pods.stop.json`, in the form `{"items": [<pod object>]}` that `pods.json` has.

   Both are written whatever the outcome, and are absent only when the watch or stream never started.
7. A failure in steps 1 to 3 never leaves the subject running. The existing `stop_members` and `delete_confirm` path deletes and confirms both Jobs (consumer_repeat.py lines 269 to 306).

For C01, C03 and C10, nothing in this step runs, and every record is byte-identical to `main`.

### 3.3 Archive

- C18 is admitted with prefix `c18` and phases C0 and C1, as C01 is.
- For a C18 subject whose `pods.json` has no item, the archive reads `pods.stop.json` instead, through the same `project_pods` path (archive.py lines 1740 to 1748), and binds it to `pod-stop.json`'s `pod_uid`.
- Gap condition: when the subject's `collection-status.json` has `no_pods` as its only gap, and `pods.stop.json` is valid (it passes the `read_job_pods` checks and its one pod's uid equals `pod-stop.json`'s `pod_uid`), the archive records none of these four gaps for the subject: `job collection failed`, `job collection incomplete`, `pod identities or declarations unavailable` and `missing job pod` (archive.py lines 1694 to 1696, 1736 to 1737, 1747 to 1748, 1782 to 1784 and 1981). The member record's `collect_gap`, which `finish_member` sets because `collect-job` exits 1 after the pod is gone (`consumer_repeat.py` lines 259 to 261; `lifecycle.sh` lines 313 to 314 and 355 to 356), is expected and stays in the record. If either condition fails, the existing gaps are recorded.
- The streamed log is the source file `<subject job>/<pod>.containers.enrichment-consumer.log` and is archived under the object name `<subject job>/<pod>.containers.enrichment-consumer.log`, through the existing `collector.runner` call (archive.py line 1780).
- `consumer_member` is then called for it as for any member (archive.py lines 2028 to 2029). Without this, a deleted subject's records would not be archived, because its pod is gone before S6's collection.
- `pod-stop.json` is archived through `collector.runner` with the new kind `consumer_pod_stop`. Its keys are validated as closed against 3.2 item 6.
- For a C18 subject, `consumer_member` also archives `<root>stop-receipt.json` from container `consumer-record` with the new kind `consumer_stop_receipt` when it exists. Its identity keys are checked as the completion receipt's are (the `document` check, archive.py lines 1233 to 1239, with kind `consumer_stop_receipt`), and a mismatch is the gap `consumer stop receipt contract`. A missing receipt is not a gap by itself; the verifier reports it as `consumer_stop_receipt_missing`. The worker writes the receipt in place of `completion.json` (`BlobConsumerRecordStore.cs` line 56), so the record blobs are read by name as for a member with no completion (archive.py lines 1275 to 1284).
- A C18 subject that ends through the stop path writes its native-monitoring index (the worker completes monitoring in its shutdown). The archive does not need an expected absence for it. If the index is missing, the existing gap is recorded.

### 3.4 Budget

- The serial additions to one repeat are the pod read in 3.2 item 1, at most 30 s, and the stop of the two streams, at most 15 s for the watch and 10 s for the log stream (`c18_stop_smoke.py` lines 140 to 156, 236 to 237 and 258 to 263).
- The delete thread runs during S5, and its 30 s request timeout ends 90 s after S4 at the latest. That is inside S5's own worst case of 1140 s plus 90 s (the #507 plan, section 14 (Q3)), so it adds nothing to the configured worst case.
- The configured worst case of a repeat therefore grows from 5042 s to 5097 s, still under the 5100 s reserve set by Hari (consumer_repeat.py lines 19 to 20). This is a sum of command timeouts, not a measurement.
- The implementer recomputes the sum by the method of the #507 plan, section 14 (Q3). If it exceeds 5100 s, the implementer stops.

## 4. Pull request 4: requirements

- R1. WHEN `scenario.yml` and its copy are compared, THEN they are byte-identical, and C18 appears exactly at the places of 3.1.
- R2. WHEN `phase.py` runs `run-repeats` for C18, THEN it routes to `consumer_repeats().run()` with the enrichment image, as for C01.
- R3. WHEN a C18 repeat reaches readiness, THEN the pod read, watch and log stream happen before S4, and the delete is issued 60 s after S4 returns, while S5 runs.
- R4. WHEN the pod read finds other than one `Running` pod, THEN the start line, load generator and verifier are `not_attempted` with reason `consumer_stop_pod_unavailable`, and both members are stopped and confirmed.
- R5. WHEN, by the end of S6, the delete was not issued, or exited other than 0, or the watch delivered no `terminated` state for the recorded uid received after `issued_at`, THEN S8 is `not_attempted` with reason `consumer_stop_unconfirmed`, and the step exits 1.
- R6. WHEN the delete command exits non-zero, THEN `delete_exit_code` records it, the repeat continues to S6, and S8 is `not_attempted` with reason `consumer_stop_unconfirmed` (R5), even if the watch later delivers a `terminated` state.
- R7. WHEN the repeat ends, THEN `pod-stop.json` exists with exactly the keys of 3.2 item 6.
- R8. WHEN the archive collects a C18 subject with an empty `pods.json` and a `pod-stop.json`, THEN `consumer_member` runs for it with the uid from `pod-stop.json`, and no "missing job pod" gap is recorded.
- R9. WHEN C01, C03 or C10 runs, THEN its records, archive and commands are unchanged.
- R10. WHEN `c18_stop_smoke.py` runs, THEN its behaviour and its tests are unchanged.
- R11. WHEN pull request 4 is merged and pull request 5 is not, THEN `phase.py` passes no new value to any Job. The pass-through is V13 of pull request 5.
- R12. WHEN a member's wait gives `unknown` and the end is also unconfirmed, THEN S8's reason is `consumer_wait_exhausted`, the existing reason, which takes precedence over `consumer_stop_unconfirmed`.
- R13. WHEN S4 ends other than `complete`, THEN no delete is issued, `issued_at` and `delete_exit_code` are null, and S8 is `not_attempted` with `consumer_stop_unconfirmed` (R5), even when the subject later ends by itself and the watch delivers that `terminated` state.
- R14. WHEN a safety stop is set while the delete thread waits or runs, THEN the delete is still issued, because deleting the subject only stops it. Every later record follows the existing safety-stop rule.
- R15. WHEN the watch and the log stream ran, THEN `pods.stop.json` and `<pod>.containers.enrichment-consumer.log` exist beside `pod-stop.json` (3.2 item 6). WHEN the gap condition of 3.3 holds, THEN the archive records none of its four gaps for the subject, and archives the log under `<subject job>/<pod>.containers.enrichment-consumer.log`. WHEN it does not hold, THEN the existing gaps are recorded.
- R16. WHEN the archive collects a C18 subject that wrote `stop-receipt.json`, THEN it archives one object of kind `consumer_stop_receipt` and records no gap for it. WHEN the subject wrote none, THEN no such object is archived and no gap is recorded. WHEN the receipt's identity differs from the phase and Job identity, THEN the gap `consumer stop receipt contract` is recorded.

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

- Registration: a C18 subject must carry `shutdown_mode`, `close` in C0 and `dispose` in C1. A missing key, any other value, or the key on any other member is `consumer_configuration_mismatch` (Amendment 1, "Configuration fidelity"). The witness rule is stricter than Amendment 1, whose bullet names only a subject instance's marker (docs/blueprint.md line 830); see question 4.
- Row `stop_observed`: exactly `schema_version kind instance_id row_sequence event_utc`.
- Row `consumer_error`: exactly `schema_version kind instance_id row_sequence source error_code is_fatal reason event_utc`, where `source` is `consume` or `error_handler` and `reason` is a string of at most 1024 UTF-16 code units (written by `ConsumerRecordCodec.cs` lines 49 to 58).
- `stop-receipt.json`: exactly the keys `ConsumerRecordCodec.cs` lines 39 to 46 write, where `shutdown_outcome` is `ok`, `failed` or `timed_out`. Its `rows`, `record_blobs` and `messages_processed` must match the record, as the completion receipt's do (`ConsumerArtifactReader.cs` line 48), and its `stop_observed_utc` must equal the `stop_observed` row's `event_utc`. A mismatch is `consumer_record_invalid`.
- C01, C03 and C10 records keep today's closed shapes. A new key or kind there stays `consumer_record_invalid` or `consumer_registration_invalid`.

### 5.3 Validity for C18

These follow Amendment 1's "Invalid run" bullets for S equal to 1, and replace today's stop check (`ConsumerJudgement.cs` lines 48 to 50) for C18 only:
- The subject wrote a `kill_intent` row or `completion.json`, or ended without a `stop_observed` row: `consumer_stop_invalid`.
- The subject wrote more than one `stop_observed` row, or a `processed` row after its `stop_observed` row: `consumer_stop_invalid`. These are additions to Amendment 1's list; see question 4.
- The subject has a `stop_observed` row and no `stop-receipt.json`: incomplete, `consumer_stop_receipt_missing`. Invalid reasons take precedence (ADR-022, "Completion"). The block keeps one incomplete reason (`ConsumerJudgement.cs` lines 79 to 80). `consumer_stop_receipt_missing` is set before the offset read, so a later `consumer_commit_evidence_unavailable`, `consumer_completion_deadline` or `consumer_commit_frontier_short` replaces it, as those replace each other today (lines 64 to 73).
- The witness ended without `completion.json`: `consumer_stop_invalid`, as today.
- The subject has a `stop_observed` row and no `processed` row with a lower `row_sequence`: incomplete, `consumer_stop_before_processing` ([decision 7242272268](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7242272268) point 0). The delete can land on an idle subject when the load generator starts slowly, and such a repeat cannot show a repeated message. It is set after the offset read, so it replaces any other incomplete reason of this list; invalid reasons still take precedence. `scenarios/C18/expected.yaml` does not change: it counts only `complete_valid` repeats.

### 5.4 The C18 block

For a C18 block with `status` `complete_valid`, these keys follow `counts`, in this order. Each is absent otherwise.
- `reassignment`, today's shape (`microseconds` and `unavailable_reason`). It is timed from the subject's `stop_observed` row to the earliest witness `assigned` row at or after it that holds every partition the subject held at that row (`Held(subject, stopObserved.RowSequence)`). `different_nodes` and `no_reassignment` keep their meaning (verifier-contract.md line 128).
- `shutdown_margin`: exactly `shutdown_outcome` (the receipt's value, `ok`, `failed` or `timed_out`), `receipt_microseconds` (from the `stop_observed` row to the receipt's `receipt_written_utc`) and `process_end_microseconds` (from the `stop_observed` row to `ConsumerSubjectProcessEndUtc`). Each is an Int64, rounded down, and may be negative only for `process_end_microseconds`, because `finished_at` has one-second precision. `process_end_microseconds` is null when the option's pod uid does not equal the subject's registered `pod_uid`.
- `consumer_errors`: exactly `subject` and `witness`, each the count of that member's `consumer_error` rows.
- `partition_events`: exactly `subject` and `witness`. Each holds exactly `assigned`, `revoked` and `lost`: the counts of that member's `partition_event` rows of each kind after its first `assigned` row.
- `subject_commit_entries_after_stop`: the count of successful entries (topic `document-events`, offset at least 0, no overall and no partition error) in the subject's `commit` rows after its `stop_observed` row.

`counts` keeps its shape. `membership` and `subject_commit_coverage` stay C03-only and C10-only.

`members[].ending` (`ConsumerJudgement.cs` line 84) gains the value `stop_receipt`, for a C18 subject with a stop receipt and no completion receipt ([decision 7242811142](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7242811142) point 2). It is recorded only; no verdict depends on it. The other values, `completion_receipt`, `kill_intent` and `none`, keep their meaning.

### 5.5 The judge

- J3 (`ConsumerPhaseEvidence.cs`):
  - `ReasonNames` (lines 29 to 36) gains `consumer_stop_receipt_missing` and `consumer_stop_before_processing`.
  - `ProjectedNames` (line 38) gains `shutdown_margin`, `consumer_errors`, `partition_events` and `subject_commit_entries_after_stop`, after `reassignment`.
  - A C18 `complete_valid` block must carry all five keys of 5.4. Any other scenario's block must carry none of the four new ones.
- J10 is unchanged. No phase total of the new keys is published, as for `reassignment`.

### 5.6 The new options

- `ConsumerSubjectProcessEndUtc` and `ConsumerSubjectPodUid` are accepted only in mode `consumer-verify` for C18, and are required there. The end time is UTC in RFC 3339 form; the uid is a GUID.
- The validator rejects them in any other mode or scenario, as it does the B10 timestamps (`VerifierHostOptionsValidator.cs` lines 25 to 27).
- `Program.cs` passes them to `ConsumerRepeatExtension` through its constructor (line 200). `VerifyRunHandler.cs` does not change.
- The validator's message at line 47 becomes exactly "Consumer verify requires C01, C03, C10 or C18."

### 5.7 Pull request 5: requirements

- V1. WHEN a C18 record holds `stop_observed` and `consumer_error` rows, a registration with `shutdown_mode`, and a `stop-receipt.json`, each in the shape of 5.2, THEN they parse, and the receipt is read.
- V2. WHEN any of them has an extra or missing key, a wrong kind, or a `source` or `shutdown_outcome` outside its set, THEN the record is `consumer_record_invalid`, or `consumer_registration_invalid` for a registration.
- V3. WHEN a C18 subject's registration lacks `shutdown_mode`, holds the other configuration's mode, or the witness's registration holds the key, THEN the repeat is invalid with `consumer_configuration_mismatch`.
- V4. WHEN the receipt's `rows`, `record_blobs`, `messages_processed` or `stop_observed_utc` disagree with the record, THEN `consumer_record_invalid`.
- V5. WHEN the subject wrote `kill_intent` or `completion.json`, ended without `stop_observed`, wrote two `stop_observed` rows, or wrote a `processed` row after it, THEN `consumer_stop_invalid`.
- V6. WHEN the subject has `stop_observed` and no receipt, and nothing else is wrong, THEN `incomplete` with `consumer_stop_receipt_missing`. WHEN the frontier is also short, THEN the single incomplete reason is `consumer_commit_frontier_short` (5.3).
- V7. WHEN a C18 block is `complete_valid`, THEN it holds the five keys of 5.4, in that order, after `counts`.
- V8. WHEN the subject held partitions at `stop_observed` and the witness was assigned them later on the same node, THEN `reassignment.microseconds` is the time between; on different nodes it is null with `different_nodes`; with no such assignment, null with `no_reassignment`.
- V9. WHEN `ConsumerSubjectPodUid` differs from the subject's registered `pod_uid`, THEN `process_end_microseconds` is null, and the rest of the block is unchanged.
- V10. WHEN J3 reads a C18 block that lacks any key of 5.4, or a block of another scenario that has one of the four new keys, THEN the slot fails J3.
- V11. WHEN the two options are given outside `consumer-verify` for C18, or are missing in it, THEN validation fails at startup.
- V12. WHEN C01, C03 or C10 is judged, THEN the block, reasons and projection equal those on `main`.
- V13. WHEN `phase.py` renders the C18 verify Job, THEN it passes `pod-stop.json`'s `terminated.finished_at` and `pod_uid` as `consumer_subject_process_end_utc` and `consumer_subject_pod_uid`. It renders that Job only after R5 has confirmed the end, so a missing or invalid file or a null `finished_at` never reaches the chart.
- V14. WHEN the chart renders verify for C18 with both values, THEN the two `Verifier__` variables are set; for any other scenario, or without them, the render fails.
- V15. WHEN the C18 subject has a `stop_observed` row and no `processed` row before it, and nothing makes the repeat invalid, THEN `status` is `incomplete` and `reasons` is `["consumer_stop_before_processing"]`, also when the frontier is short. WHEN it has one `processed` row before `stop_observed`, THEN this reason is not given.
- V16. WHEN a C18 subject has a stop receipt and no `completion.json`, THEN its `members[].ending` is `stop_receipt`, whatever the block's `status`. WHEN a C01, C03 or C10 member is judged, THEN its `ending` is as on `main`.

### 5a. Invariants and safety rules (both pull requests)

- No trigger of any workflow changes, and no workflow that reaches Azure gains a pull-request trigger (AGENTS.md hard safety rules 8 and 9).
- The ephemeral lifecycle, one environment at a time, and the destroy at job end are unchanged (rules 2, 5 and 6).
- No .NET code uses the Kubernetes API; only the workflow deletes the pod (ADR-020).
- No identity, role or network setting changes (Code Review Rules).
- C01, C03 and C10 records, verifier output and archive are unchanged (R9, V12).
- No mutation target file changes (5.1).
- No secret or identifier enters a committed file (rule 7). `pods.stop.json` and the streamed log are uploaded only in the `-pods-` artifact, as `collect-job`'s files are today.

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
| R5 | `test_consumer_repeat.py::C18StopTests.test_unconfirmed_end` | the watch stub prints events with no `terminated` state; then a watch stub whose `terminated` event is received at t = 150.000 s, before `issued_at` t = 160.000 s, followed by a `MODIFIED` event received at t = 161.000 s with the same `terminated` state and a `deletionTimestamp` | in each case verify is `not_attempted` with reason `consumer_stop_unconfirmed`; exit 1 |
| R6 | `C18StopTests.test_delete_failure` | the delete stub exits 1; the watch stub then delivers a `terminated` state at t = 170.000 s | `delete_exit_code` 1; S6 runs; verify `not_attempted`, reason `consumer_stop_unconfirmed`; exit 1 |
| R7 | `C18StopTests.test_evidence_keys` | a full stubbed repeat, with the watch event `terminated.finishedAt` `2026-11-28T10:00:02Z`, exit code 1 | `pod-stop.json` keys equal 3.2 item 6 exactly; `terminated.finished_at` is `2026-11-28T10:00:02Z` |
| R8 | `test_archive_consumer.py`, a new case | a C18 subject with `pods.json` `{"items": []}` and a `pod-stop.json` | `consumer_member` is called with that uid; no `missing job pod` gap |
| R9 | the existing consumer repeat and archive tests | C01, C03 and C10 | unchanged, except 6.2 |
| R10 | the existing `test_c18_stop_smoke.py` and `test_c18_stop_smoke_workflow.py` | as on `main` | pass unchanged |
| R11 | `test_phase.py`, existing values tests | C01, C03, C10 and C18 values | no `consumer_subject_*` key in any rendered values |
| R12 | `C18StopTests.test_wait_exhausted_wins` | the subject wait returns 124 and the watch shows no end | verify `not_attempted`, reason `consumer_wait_exhausted` |
| R13 | `C18StopTests.test_start_line_failed` | S4 stub exits 1; the watch stub then delivers a `terminated` state with exit code 0 | no delete call; `issued_at` and `delete_exit_code` null; verify `not_attempted`, reason `consumer_stop_unconfirmed` |
| R14 | `C18StopTests.test_safety_stop_during_delete` | a safety stop is set while the delete thread waits | the delete argv is still run once; S6 onward follow the safety-stop rule |
| R15 | `test_archive_consumer.py`, new case | a C18 subject with member record `collect` 1 and `collect_gap` true, `pods.json` `{"items": []}`, `collection-status.json` whose only gap is `no_pods`, a `pods.stop.json` with one pod whose uid equals `pod-stop.json`'s `pod_uid`, and the source file `<job>/<pod>.containers.enrichment-consumer.log`; then the same with a `pods.stop.json` uid that differs | first: the pod projection comes from `pods.stop.json`, the object `<job>/<pod>.containers.enrichment-consumer.log` is archived, and none of `job collection failed`, `job collection incomplete`, `pod identities or declarations unavailable` or `missing job pod` is recorded; second: the existing gaps are recorded |
| R16 | `test_archive_consumer.py`, new case | a C18 subject with a `stop-receipt.json` whose identity matches; the same subject with no receipt; the same with a receipt whose `pod_uid` differs | first: one `consumer_stop_receipt` object, no gap; second: no `consumer_stop_receipt` object, no gap; third: the gap `consumer stop receipt contract` |

### 6.5 Pull request 5 tests

| Contract | Test | Inputs | Literal expectation |
|---|---|---|---|
| 5.2 | `ConsumerStopRecordTests.Codec` | each new row, the registration with `shutdown_mode`, and the receipt, as the enrichment codec writes them; then each with one extra key, then one missing key | the first parse; each variant gives `consumer_record_invalid`, or `consumer_registration_invalid` for the registration |
| 5.2 | `ConsumerStopRecordTests.Mode_mismatch` | a C0 subject registered with `dispose`; a witness with `shutdown_mode` | `consumer_configuration_mismatch` in each case |
| 5.2 | `ConsumerStopRecordTests.Receipt_disagrees` | receipt `rows` one less than the record; receipt `stop_observed_utc` 1 tick off | `consumer_record_invalid` in each case |
| V2 | `ConsumerStopRecordTests.Codec_values` | a `consumer_error` row with `source` `poll`; a stop receipt with `shutdown_outcome` `cancelled`; a row with `kind` `stop_seen` and the `stop_observed` keys | `consumer_record_invalid` in each case |
| V4 | `ConsumerStopRecordTests.Receipt_counts_disagree` | receipt `record_blobs` one more than the record's blob count; receipt `messages_processed` one less than the record's `processed` rows | `consumer_record_invalid` in each case |
| V6 | `ConsumerStopJudgementTests.Incomplete_precedence` | no receipt, and the frontier short on partition 3 | `reasons` `["consumer_commit_frontier_short"]`, `status` `incomplete` |
| V3 | `ConsumerStopRecordTests.Mode_missing` | a C18 subject registration with no `shutdown_mode` | `consumer_configuration_mismatch` |
| V13 | `test_phase.py`, new case | C18 verify values with `pod-stop.json` `terminated.finished_at` `2026-11-28T10:00:02Z` and `pod_uid` `00000000-0000-0000-0000-000000000018` | `consumer_subject_process_end_utc` `2026-11-28T10:00:02Z` and `consumer_subject_pod_uid` that uid |
| 5.3 | `ConsumerStopJudgementTests.Endings` | subject: with `kill_intent`; with `completion.json`; with no `stop_observed`; with two `stop_observed`; with a `processed` row after `stop_observed`; with `stop_observed` and no receipt; witness with no completion | `consumer_stop_invalid` five times; `incomplete` with `consumer_stop_receipt_missing`; `consumer_stop_invalid` |
| 5.4 | `ConsumerStopJudgementTests.Reassignment` | subject holds partitions 0 to 15 at `stop_observed` 10:00:00.0000000Z and then writes `revoked` [0..15] at 10:00:01.0000000Z; witness `assigned` [0..31] at 10:00:04.5000000Z; same node | `reassignment.microseconds` 4500000; with the members on different nodes: null, `different_nodes` |
| V8 | `ConsumerStopJudgementTests.Reassignment_none` | as `Reassignment`, same node, with no witness `assigned` row after `stop_observed` | `reassignment.microseconds` null, `reassignment.unavailable_reason` `no_reassignment` |
| 5.4 | `ConsumerStopJudgementTests.Margins` | `stop_observed` 10:00:00.2500000Z; receipt written 10:00:01.0000000Z; process end option `2026-11-28T10:00:01Z` with the matching uid; then a different uid | `shutdown_outcome` `ok`, `receipt_microseconds` 750000, `process_end_microseconds` 750000; then `process_end_microseconds` null |
| 5.4 | `ConsumerStopJudgementTests.Counts` | subject: 2 `consumer_error` rows; `assigned`, `revoked` after its first `assigned`; three commit entries after `stop_observed` with topic `document-events`, offset at least 0 and no overall error, one of which has partition error code 1; witness: 0 errors, 1 `assigned` after its first | `consumer_errors` {subject 2, witness 0}; `partition_events.subject` {assigned 1, revoked 1, lost 0} (the second `assigned` counts, the first does not); `partition_events.witness` {assigned 1, revoked 0, lost 0}; `subject_commit_entries_after_stop` 2 |
| V15 | `ConsumerStopJudgementTests.Stop_before_processing` | subject: `assigned` [0..15], then `stop_observed`, then a receipt, with no `processed` row; then the same with the frontier short on partition 3; then the same with one joined `processed` row before `stop_observed` and a full frontier | first two: `status` `incomplete`, `reasons` `["consumer_stop_before_processing"]`; third: `status` `complete_valid`, `reasons` `[]` |
| V16 | `ConsumerStopJudgementTests.Ending` | a C18 subject with a stop receipt; then the same subject with no receipt | first: subject `ending` `stop_receipt`; second: subject `ending` `none`; the witness `ending` `completion_receipt` in both |
| 5.4 | `ConsumerStopJudgementTests.Key_order` | a `complete_valid` C18 block | its keys, in order: the nine fixed keys, then `counts`, `reassignment`, `shutdown_margin`, `consumer_errors`, `partition_events`, `subject_commit_entries_after_stop` |
| 5.5 | `ConsumerPhaseJudgeTests`, new cases | a C18 block missing `shutdown_margin`; a C01 block with `consumer_errors`; a C18 `incomplete` block with reason `consumer_stop_before_processing` | J3 fails the first two slots; the third is accepted as an incomplete slot |
| 5.6 | `HostOptionsTests`, new cases | the two options in `consumer-verify` C18; missing; in `consumer-verify` C01; in `start-line` C18 | valid; then rejected three times |
| chart | `charts/parchwell/tests/test_consumer_chart.py`, new case | C18 verify with both values; C01 verify with them | the two env vars are rendered; the C01 render fails |
| V14 | `charts/parchwell/tests/test_consumer_chart.py`, new case | C18 verify without `consumer_subject_process_end_utc` and `consumer_subject_pod_uid` | the render fails |
| R9 kept | the existing consumer verifier tests | C01, C03 and C10 | pass, except 6.2 |

## 7. Deployment assumptions

Each says how its evidence was taken. Bullets 1 to 3 have no offline test and are accepted by name in [decision 7242272268](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7242272268) point 10. Bullet 4 is accepted. Bullet 5 is accepted in the same decision, point 8. Bullet 6 is accepted by name in point 3.
- The watch delivers the subject container's `terminated` state before the pod is removed. Smoke run 46142138991: fresh watch, idle member, 3 of 3 trials. Under load this is not measured. R5 makes a failure visible as `consumer_stop_unconfirmed`, so the repeat is not counted.
- A deleted subject's Job reaches `Failed=True` and starts no replacement pod. The same smoke, 3 of 3 trials.
- The scenario workflow's identity may delete, watch and stream logs for pods in `parchwell`. The smoke ran in the same environment, `parchwell-scenarios`, and did all three. That the two workflows use one identity is inference from the shared environment name. It is not read from the role assignment, which is outside this repository.
- A hung `Close`, `Dispose` or receipt write is killed by the 30 s grace and leaves the repeat uncounted. Accepted by name in decision 7239828212 point 3 and decision 7240022152 point 1.
- The runner's clock and the cluster's clock are close enough that `process_end_microseconds`, computed from the node's `finishedAt` against the worker's `stop_observed` time, means something. Not measured. The value is recorded only.

- Readiness, the start line and the 60 s together can pass the subject's 360 s window, which is measured from the worker's start. The subject then completes before the delete, and the repeat is lost as `consumer_stop_unconfirmed` (3.2 item 5), never judged wrong. Readiness took about 20 s in the smoke run (4 polls); its cap is 300 s (`consumer_repeat.py` line 21). Typical durations under load are not measured. Accepted by name in decision 7242272268 point 3.

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
- `shutdown_mode` on the witness counted as `consumer_configuration_mismatch` (5.2; question 4);
- `consumer_stop_before_processing` (5.3), decision 7242272268 point 0;
- `consumer_stop_pod_unavailable` and `consumer_stop_unconfirmed` (3.2), which make a repeat that cannot meet Amendment 1's boundary visible instead of judged.

(c) Tests that depend on code, data or behaviour neither on `main` nor in the pull request:
- pull request 4 depends on the c18-scenario pull request for C18's scenario file and admission;
- pull request 5 depends on pull request 4's `pod-stop.json`.

Both gates are in section 1. The live behaviours are in section 7.

## 10. Decided questions

Open questions: None. Hari approved each of the eight questions below, with its recommendation, in [decision 7242272268](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7242272268) points 4 to 11. They are kept as asked, for the record.


1. Approve the workflow change of 3.1 and 3.2: C18 admitted on C01's route, and the stop step inside each C18 repeat. It is a protected-path change to `scenario.yml`. Recommendation: approve. It changes no trigger and no step of any other scenario.
2. The anchor of "60000 ms after the start line". Recommendation: the runner's UTC when the start-line step returns, plus 60 s, with both times recorded in `pod-stop.json`. The start-line note's own time is written by the verifier pod's clock, which the runner cannot schedule against without a clock comparison. The runner time lags the start line by the start-line Job's wait poll (up to 5 s) and its collection (up to 210 s); the lag is not measured and is recorded as the difference of the two times in `pod-stop.json`.
3. New workflow reasons and behaviour: `consumer_stop_pod_unavailable` and `consumer_stop_unconfirmed`, and no verifier run when the end is not confirmed. Also the budget: the reserve stays 5100 s, because the configured worst case becomes 5097 s, 3 s under the reserve (3.4, computed from timeouts). Recommendation: approve.
4. Verdict-relevant checks in pull request 5 beyond Amendment 1's list: more than one `stop_observed` row, or a `processed` row after it, is `consumer_stop_invalid`; and `shutdown_mode` on the witness is `consumer_configuration_mismatch`. Also the single-incomplete-reason precedence of 5.3. Recommendation: approve. The first two break Amendment 1's "consumes no further message"; the witness never stops, so a mode there means a wrong plan.
5. Evidence field names and shapes: `consumer_errors`, `partition_events`, `subject_commit_entries_after_stop` and the `shutdown_margin` rules. That includes accepting the one-second precision of `process_end_microseconds`, its comparison of two clocks, and its null when the pod uid differs. Recommendation: approve. They are recorded only.
6. The qualification renewal basis for pull request 5: "Changes consumer verifier files that are shared inputs only; no mutation target source changes, and the consumer judgement for C01, C03 and C10 is unchanged (plan c18-workflow-verifier, R9 kept), so the measured mutation runtime does not change." Recommendation: approve. The campaign itself is already decided (decision 7239828212 point 4).
7. Accept by name three live assumptions that no offline test covers (section 7 bullets 1 to 3): the watch delivers the end before the pod is removed under load; a deleted subject's Job reaches `Failed=True` with no replacement, now also from a clean exit 0 after pull request 617 (Kubernetes Job page, `job.md` lines 501 to 503); and the scenario workflow's identity may delete, watch and stream pods. Recommendation: accept. A failure of the first is visible as `consumer_stop_unconfirmed` and leaves the repeat uncounted. The second rests on the Job page and three smoke trials. The third rests on the smoke's run in the same `parchwell-scenarios` environment with the same `AZURE_CLIENT_ID` secret.
8. The treatment of a non-`ok` `shutdown_outcome`. The worker writes a receipt with `failed` or `timed_out` and the repeat is then `complete_valid`, so it counts for H1 to H4. Recommendation: count it, and record `shutdown_margin.shutdown_outcome` per repeat, recorded only, for the verdict text. A failed `Close` still ends the consumer, and the hypotheses are judged by the broker-side outcome.

## 11. Deferred work

- The mutation campaign ([decision 7239828212](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7239828212) point 4): one dispatch of `verifier-c5-mutation-companion.yml`, then one of `verifier-c5-mutation.yml`, on `main` after pull request 5 merges, with the failed-job re-runs docs/operations/verifier-mutation.md allows, covering the c18-scenario change and pull request 5 together. On the verdict path; it cannot run before merge. Responsible: the control session. Cap: that one campaign; a failed campaign comes back to Hari. Gate it blocks: the C18 evidence pack (docs/agents/factory.md "Evidence pack"). Completion evidence: the passing runs linked on #613.
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
