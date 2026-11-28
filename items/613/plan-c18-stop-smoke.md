Revision: 3

Changes since revision 2, applying Hari's answers in #613 [decision 7237226936](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237226936) and [decision 7237177975](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237177975):
- Section 11 states `None`; its six questions and their answers are kept as a record in section 11a.
- Point 5: check findings B1, B2, B3 and B4 of the [revision 2 plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237013920), each with that check's literal text (section 2 item 10, sections 3.3 and 3.4, R15, R16, A7 and the new test rows).
- Point 3: the policy values, including `timeout-minutes: 360` (B5), are recorded as set by Hari.
- Point 1: section 12 records the approved split. Its parallel route for pull request 2 is recorded as awaiting the thumbs-up confirmation that factory.md "Item lifecycle" step 3 now requires for a decision that narrows a gate (#270 decision 7236098244, merged in `9c98aa1b` before decision 7237226936 was posted).
- The two P2 findings that the revision 2 check marked `apply in implementation`, P2-a (`upload_smoke` condition and time limit) and P2-b (the `phase.py` `main` dispatch shape), are written into sections 3.1, 3.2 and 6 with their exact text.

Changes in revision 2 since revision 1, applying every finding of the [revision 1 plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7236882963): findings 1 to 8 with the check's literal text, P2-a to P2-d, and the run-ID prefix `c18s` that P2-c recommends. Finding 9 is the open questions of section 11, which stay open for Hari.

# c18-stop-smoke: a live smoke of pod deletion for C18

This plan adds one manual workflow that deletes a running consumer member's pod and records what Kubernetes and the worker do. It answers the two runtime questions that ADR-022 "Amendment 1" says a smoke settles before the C18 plan is approved: whether SIGTERM reaches the worker under the chart's shared process namespace, and whether the workflow can read a deleted pod's container end time. It changes no worker, chart, scenario, verifier or expected outcome, and it publishes no result.

Base commit: `main` at `9c98aa1be09ff76b6fc19fed71bc8cc2f3706259` (the merge of #611). Revisions 1 and 2 were written against `764b18c04b84061087c4578f4da3256eeed001e1` (the merge of #612, entry 1 of the #597 split). Between the two, only governance files changed (`AGENTS.md`, `CLAUDE.md`, `docs/agents/factory.md`, three skills, `scripts/decision-log.py` with its test, and two factory-overview diagram files); no file this plan changes or cites outside those changed. Every repository citation below holds at both commits.

## 1. Goal and authority

Goal. Produce live evidence, from today's enrichment image and chart, for three facts the C18 design depends on:
- Q1. Whether the TERM signal from a pod deletion reaches the `enrichment-consumer` worker process, which is not PID 1 because the pod sets `shareProcessNamespace: true` (`charts/parchwell/templates/_consumer.tpl` line 46).
- Q2. Whether the workflow can read the deleted pod's container end time (`state.terminated.finishedAt`) before the pod object is removed, and by which method: a watch started before the delete, a poll after it, or both.
- Q3. Whether the Job controller starts a replacement pod after the delete. The chart sets `backoffLimit: 0` and `podReplacementPolicy: Failed` (`_consumer.tpl` lines 32 to 33). Inference from the Job page (S3, lines 494 to 503 and 1263 to 1271): with no pod failure policy, the controller counts a terminating pod as a failure at once, and `Failed` delays any replacement until the pod is terminal, so with a backoff limit of 0 no replacement starts. ADR-022 "Amendment 1" makes a second subject registration an invalid repeat ("other than one witness and S subject instances registered"), so this inference must be observed, not assumed.

Authority, in creation order:
- The issue body of #613 (split entry 2 of #597; predecessor #612 merged at the base commit).
- #613 [decision 7236669224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7236669224), which links and adopts the four #597 decisions: [7235176688](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235176688) (which adopts #499 [7234963031](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-7234963031)), [7235586661](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235586661), [7235680921](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235680921) and [7236013792](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7236013792).
- docs/blueprint.md ADR-022 "Amendment 1: C18, C06 and C04" (lines 800 to 845), ADR-017, ADR-019 and ADR-020, and the C18 row of section 12 "Folded into existing items" (line 1322).
- docs/agents/factory.md "Item lifecycle" step 1: "A plan that changes live infrastructure is not approvable until a live smoke run of that design exists. A smoke-only pull request may precede plan approval for this purpose."

What the authorities settle for this pull request: the SIGTERM source is the workflow deleting the subject's pod (decision 7235680921 point 3); the smoke comes before the C18 plan's approval (issue body; ADR-022 "Amendment 1" consequences); the split entry's first planning session proposes a split into pull requests, smoke run included (issue body, quoting proposal revision 2). What they leave open is under section 11.

## 2. Scope

This plan covers one pull request, named `c18-stop-smoke`. It is the first pull request of the C18 split this plan proposes (section 12, "Deferred work"). The rest of C18 is planned in a later revision of this plan, after the smoke has run, because factory.md "Item lifecycle" step 1 does not let a plan that changes live infrastructure be approved before its smoke.

Files allowed to change:
1. `.github/workflows/c18-stop-smoke.yml` (new; protected).
2. `docs/operations/workflows/c18-stop-smoke.yml.txt` (new; byte-identical copy, AGENTS.md hard safety rule 8; `/docs/operations/` is excluded from the required reviewers and has no owner in `.github/CODEOWNERS` line 6).
3. `docs/operations/c18-stop-smoke.md` (new; the operations page; not protected, as item 2).
4. `infra/scripts/phase/c18_stop_smoke.py` (new; the orchestration).
5. `infra/scripts/phase/phase.py` (three commands added to `main`, and nothing else).
6. `infra/scripts/phase/tests/test_c18_stop_smoke.py` (new).
7. `infra/scripts/tests/test_c18_stop_smoke_workflow.py` (new).
8. `infra/scripts/lifecycle/dispatch-state.py` (the new workflow added to `WORKFLOWS`).
9. `infra/scripts/tests/test_dispatch_state.py`: the `WORKFLOWS` assertion at line 189 becomes the six-tuple of R4; the `evaluate` helper at lines 85 to 87 gains `runs_path("c18-stop-smoke.yml"): page("workflow_runs", [])`; each `assertEqual(<n>, len(http.calls))` whose lookup requests `c18-stop-smoke.yml` becomes `<n + 1>`, and no other assertion changes. The implementation notes list each changed line with its old and new value.
10. `infra/scripts/lifecycle/check-offline.sh`: `'c18-stop-smoke.yml'` appended to the `required` tuple of `check_queue` (line 54); the `cmp` line and the actionlint lists (lines 160 to 167).
11. `infra/scripts/phase/tests/test_storage_readiness_workflow.py` (the expected map at lines 33 to 41 gains `('c18-stop-smoke.yml', 'smoke'): None`, and nothing else).

Paths that must not change: `src/`, `charts/`, `scenarios/`, `tests/`, every `expected.yaml`, `results/`, `docs/blueprint.md`, `AGENTS.md`, `CLAUDE.md`, every other file under `.github/workflows/` and `docs/operations/workflows/`, `infra/ephemeral/`, `infra/persistent/`, `infra/cluster/`, `infra/scripts/phase/consumer_repeat.py`, `infra/scripts/phase/b10_abrupt.py`, `infra/scripts/lifecycle/lifecycle.sh`, `infra/scripts/lifecycle/install-tools.sh`.

The module may import and call existing functions of `phase.py` and `consumer_repeat.py` (for example the storage write check, `job_name`, rendering and `delete_confirm`) without changing them.

## 3. External contracts

### 3.1 Workflow

File `.github/workflows/c18-stop-smoke.yml`. Its first line is `# Installed at .github/workflows/c18-stop-smoke.yml; committed copy at docs/operations/workflows/c18-stop-smoke.yml.txt; the two are byte-identical (AGENTS.md hard safety rule 8).`

- `name: C18 stop smoke`.
- Trigger: `workflow_dispatch` only, with one input `confirm_stop_smoke`: `description: Confirm the C18 pod-deletion smoke, which applies and destroys the ephemeral environment`, `type: boolean`, `required: true`, `default: false`.
- `permissions: {contents: read, id-token: write}`; `concurrency: {group: parchwell-ephemeral, queue: max, cancel-in-progress: false}`.
- Jobs `smoke` then `stop`, in that order, with the shape of `.github/workflows/b10-abrupt-trial.yml`: `runs-on: ${{ vars.PARCHWELL_RUNNER_LABEL }}`, `timeout-minutes: 360`; `smoke` in environment `parchwell-scenarios`, `stop` in `parchwell-sweeper` with `needs: [smoke]` and `if: always()`.
- `smoke` env adds, beyond the Azure, ARM and `PARCHWELL_CLUSTER_RESOURCE_ID` variables of `b10-abrupt-trial.yml` lines 24 to 31: `PARCHWELL_SCENARIO: C03`, `PARCHWELL_PHASE: C0`, `PARCHWELL_CONFIGURATION: C0`, `PARCHWELL_RUN_KIND: smoke`, `PARCHWELL_PURPOSE: c18_stop_smoke`, `PARCHWELL_CONFIRM_STOP_SMOKE: ${{ inputs.confirm_stop_smoke && 'true' || 'false' }}`.
- Run ID. `prepare-c18-stop-smoke` calls `prepare_shared` with prefix `c18s` (`phase.py` lines 460 to 481), so the run ID is `c18s-<GITHUB_RUN_ID>-<GITHUB_RUN_ATTEMPT>`, distinct from every scientific run ID, as the B10 trial's `b10d` is (`b10_abrupt.py` line 269).
- `smoke` steps, with these ids in this order: `guard` (as `b10-abrupt-trial.yml` lines 41 to 50, testing `PARCHWELL_CONFIRM_STOP_SMOKE` = `true`), `checkout`, Terraform setup, `tools`, .NET SDK setup, `prepare` (`phase.sh prepare-c18-stop-smoke`), `login`, `azure_session`, `images`, `cluster_init`, `start`, `cluster_plan`, `cluster_apply`, `ephemeral_init`, `ephemeral_plan`, `ephemeral_apply`, `dns`, `credentials`, `namespace`, `smoke_run` (`phase.sh run-c18-stop-smoke`), `collect` (`phase.sh collect-c18-stop-smoke`, `if: always() && steps.prepare.outcome == 'success'`), `upload_smoke`, `delete_workload`, `destroy`, `report`. Every step that `b10-abrupt-trial.yml` has under the same id keeps its text, with `trial`, `collect` and `upload_diagnostic` replaced as named here. `report` lists `WORKLOAD_STEPS: images cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace smoke_run collect upload_smoke delete_workload destroy`.
- `upload_smoke` uploads artifact `c18-stop-smoke-${{ github.run_id }}-${{ github.run_attempt }}` from `${{ env.PARCHWELL_ARTIFACT_DIRECTORY }}/c18-stop-smoke`, `retention-days: 90`, `if-no-files-found: error`, with the pinned `actions/upload-artifact` SHA that `b10-abrupt-trial.yml` uses, `if: always() && steps.prepare.outcome == 'success'` and `timeout-minutes: 5` (as `b10-abrupt-trial.yml` lines 169 and 171).
- `stop` is the `stop` job of `b10-abrupt-trial.yml` with `PARCHWELL_SCENARIO: C03` and `PARCHWELL_PHASE: C0`.
- No `judge`, `archive`, `verify` or load generator step. No `pull_request` or `pull_request_target` trigger (AGENTS.md hard safety rule 9).

### 3.2 Commands

`infra/scripts/phase/phase.py` `main` gains exactly `prepare-c18-stop-smoke`, `run-c18-stop-smoke` and `collect-c18-stop-smoke`, each taking no argument, each calling `infra/scripts/phase/c18_stop_smoke.py`. Code shape: inside the existing `try` of `main`, before the table lookup, a branch for exactly those three commands with no further argument loads `infra/scripts/phase/c18_stop_smoke.py` with `importlib.util.spec_from_file_location`, as `Phase.b10_abrupt` does at lines 285 to 288, and returns the result of calling the module's `prepare`, `run` or `collect` on an object built from `Phase()` and `Failure`. No `Phase` method is added.

### 3.3 Trial

One dispatch runs N trials in sequence in one ephemeral environment, N = 3 (section 11a, question 3; set by Hari). Before trial 1, the run performs the existing consumer storage write check (`docs/operations/consumer-workflow.md` "Storage write check"), unchanged. If it ends `not_ready`, no trial starts and every trial is recorded with `outcome: not_attempted`, `reason: consumer_storage_access_not_ready`.

Trial n (n = 1, 2, 3) runs one member: the subject of scenario C03, configuration C0, repeat n, rendered by the existing chart in mode `consumer-subject`. Its Job name is `job_name(run, "C0", "consumer-subject", n)`, that is `<run>-c0-r<n>-consumer-subject` (`phase.py` line 230). No witness, start line, load generator or verifier runs. The topic has no producer in this workflow, so the member consumes no message (inference: the ephemeral namespace is created by this job's apply).

Steps of trial n, with literal bounds:
1. Apply the rendered Job with `kubectl apply -f <file> --request-timeout=30s`.
2. Readiness: poll the member's record under `<run>/C0/r<n>/consumer/` every 5 s for at most 300 s after the apply. Ready when exactly one member prefix holds `registration.json` and its last `partition_event` row has `event: assigned` and `partitions` equal to the set 0 to 31.
3. Read the pod: `kubectl get pods -n parchwell -l job-name=<job> -o json --request-timeout=30s`; exactly one pod, phase `Running`. Record its name, node name and `metadata.uid`.
4. Start the log stream: `kubectl logs -f -n parchwell <pod> -c enrichment-consumer --limit-bytes=1048576 --request-timeout=0`, writing to a private file. Start the watch: `kubectl get pod <pod> -n parchwell --watch --output-watch-events -o json --request-timeout=0`, writing each event with the runner's UTC receive time.
5. Wait D = 30 s after readiness (section 11a, question 3; set by Hari).
6. Delete: record `issued_at` (runner UTC, milliseconds), then run `kubectl delete pod <pod> -n parchwell --wait=false --request-timeout=30s`. No `--grace-period` flag, so the pod's own `terminationGracePeriodSeconds`, 30 (`charts/parchwell/values.yaml` line 105), applies.
7. Poll: `["kubectl", "get", "pod", <pod>, "-n", "parchwell", "--ignore-not-found", "-o", "json", "--request-timeout=5s"]`. NotFound is exit 0 with empty standard output. Any non-zero exit is a failed poll: it is counted in `poll.polls`, and polling goes on until O. The poll is repeated until it returns NotFound or until O = 120 s after `issued_at` (section 11a, question 3; set by Hari); the next poll starts 1 s after the previous poll returned. In the same loop, every 5 s, list pods with `-l job-name=<job>`.
8. At NotFound or at O, stop the watch and the log stream, list pods once more with `-l job-name=<job>` and add the names to `pods_seen`. Read the Job: `kubectl get job <job> -n parchwell -o json --request-timeout=30s`.
9. Delete and confirm the Job with `ConsumerRepeats.delete_confirm` semantics (`consumer_repeat.py` lines 284 to 306). A failed delete or a remaining pod sets the safety stop, and every later trial is `not_attempted` with reason `safety_stop`.

### 3.4 Evidence file

The artifact holds one file, `c18-stop-smoke.json`, strict JSON, UTF-8, keys sorted. It never holds blob contents, tokens, exception messages, log lines other than the matched prefix below, or any identifier that AGENTS.md hard safety rule 7 bars. Shape, with literal key names:

```json
{
  "dialect": "c18_stop_smoke",
  "schema_version": 1,
  "run_id": "<run>",
  "source_commit": "<40 lowercase hex>",
  "scenario": "C03",
  "configuration": "C0",
  "grace_seconds": 30,
  "bounds": {"trials": 3, "readiness_cap_seconds": 300, "readiness_interval_seconds": 5, "delay_after_ready_seconds": 30, "observation_cap_seconds": 120, "poll_interval_seconds": 1},
  "consumer_storage_access": {"outcome": "ready", "attempts": 1, "elapsed_seconds": 0, "last_failure": null, "probe_delete_failed": false},
  "trials": [
    {
      "trial": 1,
      "outcome": "observed",
      "reason": null,
      "job": "<run>-c0-r1-consumer-subject",
      "pod": "<name>",
      "pod_uid": "<uid>",
      "node": "<node>",
      "readiness": {"outcome": "ready", "polls": 4, "elapsed_seconds": 20, "last_condition": "ready"},
      "delete": {"issued_at": "2026-11-28T10:00:00.123Z", "exit_code": 0, "deletion_timestamp": "2026-11-28T10:00:30Z", "deletion_grace_period_seconds": 30},
      "watch": {"started": true, "events": 5, "terminated": {"exit_code": 1, "signal": null, "reason": "Error", "started_at": "...", "finished_at": "..."}, "terminated_event_type": "MODIFIED", "terminated_received_at": "...", "deleted_received_at": "..."},
      "poll": {"polls": 9, "longest_seconds": 0.4, "terminated": null, "terminated_read_at": null, "not_found_at": "..."},
      "log": {"stream_started": true, "failure_line_reason": "consumer_fatal_error"},
      "pods_seen": ["<name>"],
      "replacement_pod": false,
      "job_conditions": [{"type": "Failed", "status": "True", "reason": "BackoffLimitExceeded"}],
      "job_status": {"active": 0, "failed": 1, "terminating": 0},
      "sigterm": "reached_worker",
      "end_time": "read",
      "end_time_source": "watch"
    }
  ]
}
```

The values in the example are illustrative, not expected. Field rules:
- `outcome` is `observed` or `not_attempted`. `reason` is null for `observed`, otherwise one of `run_not_started`, `consumer_storage_access_not_ready`, `safety_stop`, `render_failed`, `apply_failed`, `readiness_not_observed`, `pod_not_running`, `delete_failed`.
- `terminated` is the `state.terminated` object of the container named `enrichment-consumer`, with these five keys renamed to snake case; any absent field is null.
- `failure_line_reason` is the captured group of the first log line matching `^consumer_processing_failed reason=([a-z_]{1,64}) `, or null. That prefix is the worker's failure line (`ConsumerProcessingWorker.cs` line 117; `Program.cs` line 208).
- Every trial object holds every key of the example. A `not_attempted` trial sets `trial`, `job`, `outcome` and `reason`; `readiness` holds what was observed, or null when no readiness poll ran; every other key is null, except `pods_seen` and `job_conditions`, which are `[]`.
- When `collect-c18-stop-smoke` finds no saved trial state, it writes three `not_attempted` trials with reason `run_not_started` and `consumer_storage_access` null.
- When saved state holds a final record for fewer than three trials, `collect-c18-stop-smoke` writes each trial without one as `not_attempted` with reason `run_not_started`, in the shape the `not_attempted` rule above sets.
- `delete.deletion_timestamp` and `delete.deletion_grace_period_seconds` are `metadata.deletionTimestamp` and `metadata.deletionGracePeriodSeconds` of the first pod object received after `issued_at`, from the watch or the poll, that has `deletionTimestamp` set; null when none was received. Later values are not used.
- `job_status` holds `status.active`, `status.failed` and `status.terminating` of the Job read in step 8; an absent field is null.
- `poll.longest_seconds` is the longest duration of a single poll command, in seconds.
- Times are RFC 3339 UTC strings as Kubernetes returns them, or as the runner records them with milliseconds.

### 3.5 Classification

Each `observed` trial gets three recorded-only labels. They decide no validity and no verdict. Let `T` be the watch's `terminated` object if present, else the poll's. Let `deadline` be `delete.deletion_timestamp`, which the Pod termination page describes as "the time beyond which the Pod is considered 'dead'" (S2, line 74).
- `sigterm`:
  - `reached_worker` when `T` is present, `T.exit_code` is not 137, `T.finished_at` is not earlier than `delete.issued_at` truncated to whole seconds, and `T.finished_at` is earlier than `deadline`;
  - `reached_but_killed_at_grace` when `T.exit_code` is 137 and `failure_line_reason` is not null;
  - `not_reached` when `T.exit_code` is 137 and `failure_line_reason` is null;
  - `undetermined` otherwise, including when `T` is absent or `deadline` is null.
- `end_time`: `read` when `T` is present with a non-null `finished_at`; otherwise `not_read`. `end_time_source` is `watch`, `poll`, `both` or `none`, by which method read it.
- `replacement_pod`: true when any pod name other than the deleted pod appears in `pods_seen`.

Basis for 137: SIGKILL is signal 9 and a container killed by a signal reports 128 plus the signal number (inference from common container-runtime practice; not verified in a Kubernetes source read for this plan; the `signal` field in `terminated`, S5, is recorded beside it so the C18 plan can check it).

### 3.6 Exit status

`run-c18-stop-smoke` exits 1 when any trial is `not_attempted` or has `sigterm` equal to `undetermined`, and 0 otherwise. A trial with `end_time` `not_read` has no `T`, so its `sigterm` is `undetermined` and the run exits 1; that makes a negative answer to Q2 visible in the job status. `not_reached`, `reached_but_killed_at_grace` and a replacement pod are findings, not failures, and do not change the exit status. Every later step still runs under its `if: always()` condition. `collect-c18-stop-smoke` writes the evidence file from saved state even after a failure of the run command, and exits 1 only when it cannot write it.

### 3.7 Not scientific

The evidence is a diagnostic. No result is written under `results/`, no `expected.yaml` exists for it, and no archive, judge or candidate is produced. The operations page says so in its first paragraph.

## 4. Requirements

R1. WHEN the workflow is dispatched with `confirm_stop_smoke` false, or on a ref other than `refs/heads/main`, THEN step `guard` fails before any Azure login.

R2. WHEN the workflow file is read, THEN its only trigger is `workflow_dispatch` with the single input of 3.1, and its job, environment, concurrency, permission and step-id lists equal 3.1 exactly.

R3. WHEN `.github/workflows/c18-stop-smoke.yml` and `docs/operations/workflows/c18-stop-smoke.yml.txt` are compared, THEN they are byte-identical; `check-offline.sh` runs that `cmp` and runs actionlint on the workflow with the `queue` ignore that the other dispatch workflows use.

R4. WHEN `dispatch-state.py` loads, THEN `WORKFLOWS` equals `("scenario.yml", "cluster-smoke.yml", "b10-abrupt-trial.yml", "archive-smoke.yml", "configuration-isolation-smoke.yml", "c18-stop-smoke.yml")`, so the sweeper treats a running smoke as an active dispatch (AGENTS.md hard safety rule 6).

R5. WHEN the storage write check ends `not_ready`, THEN no member Job is applied and all three trials are `not_attempted`, with reason `safety_stop` when the check set the safety stop and `consumer_storage_access_not_ready` otherwise, and the run exits 1.

R6. WHEN trial n starts, THEN exactly one Job named `<run>-c0-r<n>-consumer-subject` is rendered from the unchanged chart and applied, and no other Job is applied in that trial.

R7. WHEN readiness is not met within 300 s of the apply, THEN the trial is `not_attempted` with reason `readiness_not_observed`, no delete is issued, and the Job is deleted and confirmed.

R8. WHEN readiness is met, THEN the watch and log stream start before the delete, and the delete is issued 30 s after readiness, with `--wait=false` and no `--grace-period`.

R9. WHEN the delete command returns non-zero, THEN the trial is `not_attempted` with reason `delete_failed` and its exit code is recorded; the Job is deleted and confirmed.

R10. WHEN the pod returns NotFound or 120 s pass after `issued_at`, THEN the watch and log stream stop, the Job is read, and the trial's labels are computed exactly as 3.5 sets.

R11. WHEN any trial's Job delete or confirmation fails, THEN the safety stop is set and every later trial is `not_attempted` with reason `safety_stop`.

R12. WHEN the run ends by any path after `prepare` succeeded, THEN `collect-c18-stop-smoke` writes `c18-stop-smoke.json` with the shape of 3.4, and `delete_workload` and `destroy` run (`if: always()` conditions as in `b10-abrupt-trial.yml`).

R13. WHEN the evidence file is written, THEN it contains no key outside 3.4 and no log text beyond `failure_line_reason`.

R14. WHEN the run command finishes, THEN its exit status follows 3.6.

R15. WHEN the watch command fails to start, exits before NotFound, or prints output that does not parse as a sequence of JSON objects each with keys `type` and `object`, THEN the trial continues with the poll until NotFound or O, `watch.started` records whether the command started, `watch.terminated` holds only what an event that parsed gave, or null, and the labels follow 3.5.

R16. WHEN the log stream fails to start or ends early, THEN `log.stream_started` records whether it started, `failure_line_reason` comes from the lines received, or is null, and the trial continues.

## 5. Invariants and safety rules

- The worker, chart and image are unchanged; the smoke observes today's behaviour.
- One ephemeral environment, applied and destroyed by this job; the `stop` job stops AKS (AGENTS.md hard safety rules 2, 5, 6).
- No Azure credentials in any agent session; the workflow reaches Azure only through its OIDC identity (rule 3).
- No secret, tenant ID or subscription ID in any file (rule 7).
- No .NET code uses the Kubernetes API; the workflow alone deletes the pod (ADR-020).
- Dispatch only against `main`, only by Hari's decision (rule 8; issue body).

## 6. Test map

All tests are offline, with stub commands, in the existing discovery (`infra/scripts/tests/test_phase_bridge.py` exposes the phase suite; `dotnet-verification.yml` line 209 runs it).

| Requirement | Test | Inputs | Literal expectation |
|---|---|---|---|
| R1 | `test_c18_stop_smoke_workflow.GuardTests.test_guard_refuses_without_confirmation_or_main` | run the `guard` script with `GITHUB_REF=refs/heads/main`, `PARCHWELL_CONFIRM_STOP_SMOKE=false`; then `refs/heads/x` with `true`; then main with `true` and synthetic UUID `00000000-0000-0000-0000-000000000001` | exit non-zero, non-zero, 0 |
| R2 | `test_c18_stop_smoke_workflow.ShapeTests.test_trigger_jobs_and_steps` | parsed workflow | `on` equals the input of 3.1; jobs `['smoke', 'stop']`; step ids equal the 3.1 list; env values equal 3.1; `upload_smoke`'s `if` equals `always() && steps.prepare.outcome == 'success'` and its `timeout-minutes` equals `5` |
| R3 | `test_c18_stop_smoke_workflow.ShapeTests.test_committed_copy_is_identical` | both files | equal bytes |
| R3 | `test_tools_offline.ToolsOfflineTests.test_committed_scenario_items_map_passes_check_workflows` (existing, unchanged) | the committed workflows | no exception |
| R4 | `test_dispatch_state` existing assertion at line 189, updated | `dispatch.WORKFLOWS` | the six-tuple of R4 |
| R5 | `test_c18_stop_smoke.StorageTests.test_not_ready_skips_all_trials` | stub storage check returning `not_ready` | three trials, each `not_attempted` with reason `consumer_storage_access_not_ready` and the null keys of 3.4; with the check's Job delete failing, reason `safety_stop`; no `kubectl apply` of a member Job; exit 1 |
| R6 | `test_c18_stop_smoke.TrialTests.test_one_subject_job_per_trial` | stub commands, run id `smoke1` | apply calls name `smoke1-c0-r1-consumer-subject`, `smoke1-c0-r2-consumer-subject`, `smoke1-c0-r3-consumer-subject`, once each |
| R7 | `test_c18_stop_smoke.TrialTests.test_readiness_timeout` | fake clock; listing never shows `registration.json` | trial `not_attempted`, reason `readiness_not_observed`; no `kubectl delete pod` call; one Job delete |
| R7 | `test_c18_stop_smoke.TrialTests.test_readiness_requires_all_32` | assigned partitions 0 to 30 | not ready; with 0 to 31, ready |
| R8 | `test_c18_stop_smoke.TrialTests.test_delete_after_delay_with_streams_started` | fake clock, ready at t=20 s | watch and log commands started before the delete; delete at t=50 s; delete argv equals `["kubectl", "delete", "pod", <pod>, "-n", "parchwell", "--wait=false", "--request-timeout=30s"]` |
| R9 | `test_c18_stop_smoke.TrialTests.test_delete_failure` | delete stub returns 1 | `not_attempted`, reason `delete_failed`, `delete.exit_code` 1 |
| R10, 3.5 | `test_c18_stop_smoke.ClassifyTests` (one case per row) | `T` and `deadline` below | as listed |
| R11 | `test_c18_stop_smoke.TrialTests.test_safety_stop` | trial 1 confirm returns one pod | trials 2 and 3 `not_attempted`, reason `safety_stop` |
| R12 | `test_c18_stop_smoke.CollectTests.test_collect_without_run_state` | no saved trial state | three trials `not_attempted`, reason `run_not_started`; `consumer_storage_access` null; keys equal 3.4 |
| R12, R13 | `test_c18_stop_smoke.CollectTests.test_collect_after_failure_and_keys` | state after a failed run, with final state saved for trial 1 only; log file with the line `consumer_processing_failed reason=consumer_fatal_error scenario_id=C03 run_id=smoke1 role=subject cause=System.OperationCanceledException` and a second line `secret-text` | file written; top-level and trial keys equal 3.4 exactly; `failure_line_reason` is `consumer_fatal_error`; `secret-text` absent from the file; trials 2 and 3 `not_attempted`, reason `run_not_started` |
| R14 | `test_c18_stop_smoke.ExitTests` | label sets below | as listed |
| 3.2 | `test_c18_stop_smoke.CommandTests.test_main_dispatch` | `phase.main(["run-c18-stop-smoke"])` with the module's `run` stubbed to return 1; then `phase.main(["run-c18-stop-smoke", "x"])` | 1; then 2 |
| R15 | `test_c18_stop_smoke.TrialTests.test_malformed_watch_falls_back_to_poll` | watch stub prints `not-json` and exits 0 1 s after it starts. On its third call the poll stub returns a pod whose `enrichment-consumer` container has `state.terminated` with `exitCode` 1 and `finishedAt` `2026-11-28T10:00:02Z`, and `metadata.deletionTimestamp` `2026-11-28T10:00:30Z`; then NotFound. `issued_at` `2026-11-28T10:00:00.123Z` | `outcome` `observed`, `watch.started` true, `watch.terminated` null, `end_time_source` `poll`, `sigterm` `reached_worker` |
| R15, R16 | `test_c18_stop_smoke.TrialTests.test_watch_and_log_not_started` | watch and log stubs exit 1 at start; poll as above | `watch.started` false, `log.stream_started` false, `failure_line_reason` null, `end_time_source` `poll` |
| A5 | `test_c18_stop_smoke.TrialTests.test_poll_longest_seconds` | three polls taking 0.2, 0.7 and 0.4 s on the fake clock | `poll.polls` 3, `poll.longest_seconds` 0.7 |
| B3 | `test_c18_stop_smoke.TrialTests.test_poll_error_is_not_not_found` | poll stub exits 1 twice, then returns the terminated pod, then exits 0 with empty output | `poll.polls` 4, `poll.not_found_at` set from the fourth poll, `poll.terminated` set |

`ClassifyTests` cases (deadline `2026-11-28T10:00:30Z`, `issued_at` `2026-11-28T10:00:00.123Z` unless a case says otherwise):
1. exit 1, finished `2026-11-28T10:00:02Z` -> `sigterm` `reached_worker`.
2. exit 143, finished `2026-11-28T10:00:01Z` -> `reached_worker`.
3. exit 1, finished `2026-11-28T10:00:30Z` -> `undetermined` (boundary: not earlier than the deadline).
4. exit 137, failure line present -> `reached_but_killed_at_grace`.
5. exit 137, no failure line -> `not_reached`.
6. no `T` -> `undetermined`; `end_time` `not_read`; `end_time_source` `none`.
7. `T` from watch only -> `end_time_source` `watch`; from poll only -> `poll`; from both -> `both`.
8. `pods_seen` `["p1"]` with deleted pod `p1` -> `replacement_pod` false; `["p1", "p2"]` -> true; a pod `p2` that appears only in the listing after NotFound -> true.
9. objects received in order with `deletionTimestamp` `2026-11-28T10:00:30Z` (grace 30) then `2026-11-28T10:00:05Z` (grace 0); exit 1, finished `2026-11-28T10:00:02Z` -> `deadline` `2026-11-28T10:00:30Z`, `sigterm` `reached_worker`.
10. exit 1, finished `2026-11-28T09:59:59Z`, `issued_at` `2026-11-28T10:00:00.123Z` -> `undetermined`.
11. exit 1, finished `2026-11-28T10:00:00Z`, same `issued_at` -> `reached_worker` (boundary).

`ExitTests` cases: three trials `reached_worker`/`read` -> 0; one `not_reached` -> 0; one `reached_but_killed_at_grace` -> 0; one replacement pod -> 0; one `not_read` (so `undetermined`) -> 1; one `undetermined` with `T` present and exit 1 at the deadline -> 1; one `not_attempted` -> 1.

Existing tests. When an existing test's fixture is rejected by a new check, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The pull request's implementation notes list each change with the check that forces it, and the cold review and Codex review check each listed change. The known cases are the two files of section 2 items 9 and 11, changed exactly as those items state.

## 7. Deployment assumptions

Each assumption is tested by the smoke itself or by an offline test; none is accepted as a risk by this plan.

| Assumption | How its evidence was taken | Covered by |
|---|---|---|
| A1. Today's subject member registers and holds all 32 partitions in a fresh namespace with no producer. | C03 runs 44562323424, 45074268881 and 45124695288 ran full repeats, and each repeat polls readiness before the start line and load (`consumer_repeat.py` lines 201 to 208): fresh connection, idle. Their readiness records were not re-read in this session. | The smoke's readiness record; R7 makes a failure visible. |
| A2. The workflow principal may delete a pod, watch a pod and stream logs in namespace `parchwell`. Today it deletes Jobs (`consumer_repeat.py` lines 288 to 289) and reads logs (`lifecycle.sh` line 337); direct pod deletion and watch are not used today. The role assignment is not in this repository. | Not taken. | The smoke: `delete_failed` and `watch.started` false are recorded; R9. |
| A3. A watch through the runner's Azure CLI session and kubelogin stays open for at least 150 s. | Not taken. | The 1 s poll runs beside it; `end_time_source` shows which worked. |
| A5. A `kubectl get pod` poll through kubelogin `azurecli` and the serialized `az` wrapper returns in well under 1 s. | Not taken. | The watch; `poll.longest_seconds`, the longest single poll, recorded beside `poll.polls`. |
| A6. kubectl v1.36.5 prints each watch event as one JSON object with keys `type` and `object`. | Not taken. | The 1 s poll; `watch.events` and `watch.terminated` recorded. |
| A7. kubectl v1.36.5 `get pod <name> --ignore-not-found` exits 0 with empty output for a deleted pod. The kubectl get page says only that the flag 'suppresses NotFound error for specific objects that do not exist' (kubernetes/website `38552b31`, `kubectl_get/_index.md` line 140) | Not taken | `poll.not_found_at` and the watch's `deleted_received_at` are recorded side by side. |
| A4. The existing worker, on cancellation, prints a line with prefix `consumer_processing_failed reason=` before it exits (`ConsumerProcessingWorker.cs` lines 62 to 117). | Source read, not live. | The smoke's `failure_line_reason`; supporting evidence only, not needed for `reached_worker`. |

## 8. Acceptance commands

Run on the branch before the pull request, each with its evidence in the pull request description:
1. `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` -> all pass, including the new suites.
2. `bash infra/scripts/lifecycle/check-offline.sh` -> exit 0, with the new `cmp` and actionlint lines in its output.
3. `cmp .github/workflows/c18-stop-smoke.yml docs/operations/workflows/c18-stop-smoke.yml.txt` -> no output, exit 0.
4. `git diff --name-only origin/main...HEAD` -> only the files of section 2.
5. An ASCII scan of the changed files -> no character outside ASCII.

After Hari merges the pull request, the smoke runs once, by the control session, under #613 decision 7237226936 point 2 and section 12 "Smoke dispatch".

## 9. Sources

Tools: Microsoft Learn MCP search for Azure; `curl` of raw files from kubernetes/website at a pinned commit, and of kubernetes.io pages; repository files read on `main` at the base commit. All read 2026-11-28.
- S1. AKS core concepts, "Container runtime": "For Linux node pools, containerd is used on Kubernetes version 1.19 and higher" (https://learn.microsoft.com/azure/aks/core-aks-concepts#node-configuration).
- S2. Termination of Pods, kubernetes/website `38552b318453cbf9e282f36262a6f163bb7a8017`, `content/en/docs/concepts/workloads/pods/pod-termination.md`: TERM "to the main process in each container" (line 24); KILL to remaining processes once the grace expires (lines 29 to 30); the Pod "is updated with the time beyond which the Pod is considered 'dead' along with the grace period" (lines 73 to 74); TERM "to process 1 inside each container" (line 94); after the grace the kubelet sets a terminal phase and the API server deletes the object (lines 129 to 137).
- S3. Jobs, same commit, `content/en/docs/concepts/workloads/controllers/job.md`: a terminating pod counts as an immediate failure without a pod failure policy (lines 494 to 503); `backoffLimit` counting (lines 520 to 526); the `batch.kubernetes.io/job-tracking` finalizer, removed after the pod is accounted for (lines 1216 to 1224); `podReplacementPolicy: Failed` creates replacements only when the terminating pod is terminal (lines 1263 to 1271).
- S4. Share Process Namespace, same commit, `content/en/docs/tasks/configure-pod-container/share-process-namespace.md` lines 115 to 118: "The container process no longer has PID 1."
- S5. Kubernetes API reference, Pod v1, `ContainerStateTerminated`: `exitCode`, `finishedAt` ("Time at which the container last terminated"), `reason`, `signal`, `startedAt` (https://kubernetes.io/docs/reference/kubernetes-api/workload-resources/pod-v1/); ObjectMeta `deletionTimestamp` and `deletionGracePeriodSeconds` (https://kubernetes.io/docs/reference/kubernetes-api/definitions/object-meta-v1-meta/). Not pinned to a commit.
- S6. kubectl get reference, same commit, `content/en/docs/reference/kubectl/generated/kubectl_get/_index.md` lines 172 to 175: `--output-watch-events` "Output watch event objects when --watch or --watch-only is used." The installed kubectl is v1.36.5 (`install-tools.sh` line 28).
- docs/blueprint.md S54 and S55, as cited by ADR-022 "Amendment 1".

## 10. Plan-check answers

(a) Effort that scales with a number unknown at planning time: none. The trial count, readiness cap, delay, observation cap and poll interval are fixed (3, 300 s, 30 s, 120 s, 1 s), set by Hari (section 11a, question 3). The storage check keeps its existing 1800 s cap.

(b) Stricter than the parent: the parent's split entry names no smoke design. This plan adds Q3, the replacement pod, which the parent does not name; the reason is ADR-022 "Amendment 1"'s registration-count invalid-run condition. It also proposes three trials rather than one, and exit status 1 for `undetermined` (3.6), both set by Hari (section 11a, question 3).

(c) Tests that depend on code, data or behaviour neither on `main` nor in this pull request: none offline. The live behaviours are A1 to A6 in section 7; A5 (poll timing) and A6 (the watch-event shape that offline stubs encode) are settled by no source read, and the smoke records evidence for each.

## 11. Open questions for Hari

None.

## 11a. Questions answered by Hari

All six were answered by #613 [decision 7237226936](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237226936), point by point, except question 5, answered by [decision 7237177975](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237177975), which adopts #597 decision 7237175612. Answers: (1) the split is approved in the order of section 12, with pull request 2 planned and built in parallel with the smoke, that parallel route pending confirmation (section 12); (2) one dispatch after merge, by the control session, under section 12 "Smoke dispatch"; (3) the policy values as proposed, including `timeout-minutes: 360` for both jobs; (4) the borrowed C03 C0 identity with prefix `c18s` is accepted; (5) the label on #597 covers this child's plan; (6) a `## Decision` comment posted by an agent session from either login that quotes Hari's words carries his decision. The questions as put, for the record:

1. Approve the proposed C18 split in section 12, with this smoke as its first pull request. The split itself is scope (#499 decision 7234963031 point 3).
2. Dispatch (answered in point 2). Merging adds a workflow; running it applies and destroys one ephemeral environment and runs AKS for one job. Decide whether to dispatch it once after merge. Estimate, dated 2026-11-28: about 40 to 60 minutes of environment time, on the basis that the consumer phase jobs took 58.3 to 65.3 minutes with five repeats of a full member pair and load (proposal revision 2, "Environment time"), and this smoke runs three single-member trials of at most about 8 minutes each (300 + 30 + 120 s plus deletes) inside the same apply and destroy. Not measured.
3. Policy values (set by Hari in point 3): N = 3 trials, D = 30 s after readiness, O = 120 s observation after the delete (the 30 s grace plus 90 s), poll every 1 s. They are proposals, not measurements. The readiness cap of 300 s and interval of 5 s reuse the consumer workflow's values (`consumer_repeat.py` lines 21 to 22), set by Hari for the full repeats, here applied to a new step.
4. Reusing C03 configuration C0 as the smoke member's identity. The worker accepts only C03, C10 and C01 (`ConsumerProcessingOptions.cs` line 27) and the chart only those (`_consumer.tpl` line 2), so a smoke on today's image must borrow one. C03 C0 has no kill count (`scenarios/C03/scenario.yaml`). The record prefix and group are this run's own, so no C03 result is touched; the evidence is labelled `dialect: c18_stop_smoke` and the run ID has prefix `c18s`. The member still sends telemetry tagged `scenario_id` C03 to the persistent Application Insights (`_consumer.tpl` lines 93 to 102); a query must filter on the run ID.
5. Approval route for this child. #613 decision 7236669224 leaves open whether the `ready-for-agent` label on #597 counts for this child's plan.
6. Comment account. Decisions [7236013792](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7236013792) and [7236669224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7236669224) were posted by the login `haripraghash-bot`, not `haripraghash`. `scripts/decision-log.py` prints only `haripraghash` comments (factory.md "Decision log"), so it omitted both. This planning session's own comments on #613 also post as `haripraghash-bot`, so that login is the agent sessions' account: those two decisions are agent-written records of words Hari gave in a control session, not comments he posted. This plan treats them as his decisions because each quotes his words; confirm that, or post or adopt them from your own account. The job time limit `timeout-minutes: 360` for `smoke` and `stop` copies `b10-abrupt-trial.yml` lines 22 and 203 (check finding B5).

## 12. Deferred work

Pull requests 3, 4 and 5 of the C18 split are deferred to plans written after the smoke's evidence exists. Deferral is required, not chosen: factory.md "Item lifecycle" step 1 bars approving a plan that changes live infrastructure before its smoke, and [decision 7237226936](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237226936) point 1 keeps that order. Each is on the verdict path. Responsible session: a `plan-pr` session on #613 after the smoke artifact is posted. Gate it blocks: the approval of those plans, and so entries 3 to 7 of the #597 split.

Pull request 2 (worker stop path). [decision 7237226936](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237226936) point 1 lets it be planned, approved and implemented in parallel with the smoke, in its own plan file `items/613/plan-c18-worker-stop.md`. That part of point 1 narrows the smoke-first gate of factory.md "Item lifecycle" step 1, so under step 3 on `main` at `9c98aa1be09ff76b6fc19fed71bc8cc2f3706259` (#270 decision 7236098244) no session acts on it until `scripts/decision-log.py` reports Hari's thumbs-up on a comment that records it with the line `Confirmation: thumbs-up required`. Decision 7237226936 carries no such line, and when this revision was written (2026-11-28) it had no reaction. Until that confirmation exists, pull request 2 stays deferred with pull requests 3 to 5. This plan's scope is pull request 1 either way.

Smoke dispatch (execution after merge). On the C18 verdict path, because the C18 plan revision needs its evidence; it cannot run before merge, because only a merged workflow on `main` may be dispatched (AGENTS.md hard safety rule 8). Responsible: the #613 control session, under Hari's dispatch decision ({D} point 2). Prerequisites: this pull request merged; no other ephemeral environment exists (AGENTS.md hard safety rule 5). Cap: one dispatch. Exhaustion action: if the dispatch ends without three `observed` trials, or `run-c18-stop-smoke` exits 1, the control session posts the run link and `c18-stop-smoke.json` on #613 and hands off to Hari; a second dispatch is Hari's decision. Completion evidence: the run link and `c18-stop-smoke.json` posted on #613. Gate it blocks: the C18 plan revision.

Order approved by [decision 7237226936](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237226936) point 1; each is planned in full in its own plan, with its files, contracts and tests:
1. `c18-stop-smoke` (this plan).
2. Worker stop path: stop-observed row, stop receipt, shutdown mode `close` or `dispose`, consumer-error rows, and the shutdown mode in the registration marker (ADR-022 "Amendment 1" "Stop", "Consumer errors", "Configuration fidelity"); the worker and chart admit C18. Not protected, except any `*.csproj`.
3. Scenario C18 (`scenarios/C18/scenario.yaml` with stop time, shutdown mode and two configurations), with its `expected.yaml` approved with the plan (protected), and the load generator admission.
4. Workflow: admission of C18 and the pod delete at the stop time, with the pod evidence the smoke shows can be read (protected; committed copy).
5. Verifier: the Amendment 1 invalid-run conditions, `consumer_stop_receipt_missing`, takeover timing from the stop-observed row, the two shutdown-margin times, and mutation acceptance on `main` (protected).

Completion evidence for the deferral: the smoke artifact on #613, and the plan comments for pull requests 3 to 5.

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, or the scope in section 2. That includes any need to change the worker, the chart or `consumer_repeat.py` to make the smoke run. It also stops if a test cannot express a requirement with the literal values of section 6.

## 14. Size estimate

Estimate, dated 2026-11-28, from the sizes of `b10-abrupt-trial.yml` (273 lines) and its tests: about 1,400 to 1,900 changed lines in 11 files: workflow and copy about 560, module about 400, tests about 600, operations page about 80, the rest under 30. Not measured; reported, not enforced.
