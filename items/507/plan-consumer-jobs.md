# Consumer Jobs and admission of C03, C10 and C01: contract-grade plan

Revision: 3

This plan adds the two consumer member Jobs to the scenario chart and orders them, per repeat, against the start line, the load generator and the verifier. It admits C03, C10 and C01 to `.github/workflows/scenario.yml` as two-phase scenarios on the existing B04 route, and adds the `consumer-record` container and its two role assignments that ADR-022 requires. Nothing in this pull request can run live: the consumer worker, the verifier's consumer mode and the load generator all still lack their C03, C10 and C01 inputs, which issue #508 adds, so this pull request is checked offline and its live smoke is #508's.

Issue: #507 (split entry 4 of #499). Short name: `consumer-jobs`.
Base commit: `911c5a1eaaac55107271755f2aa9697ea7a8acf9` (`main`, 2026-11-16, after #528, the phase evidence archive). Revision 2 was written against `46a2c3d11c4e36b9a4c7806a18e9b72ad4e70595`. #528 changed `scenario.yml`, `jobs.yaml`, `values.yaml`, `values.schema.json`, `identity.tf`, `phase.py`, `test_workflow.py`, `test_phase.py` and `main.tftest.hcl`; this revision re-reads each at the places it changes them and updates every citation (section 18).
Implementing tool: Codex, in a session Hari starts ([decision 6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673) point 2). The Claude review gate of [decision 6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233) point 2 applies.
Protected paths touched: `.github/workflows/scenario.yml` (AGENTS.md hard safety rule 8), `docs/operations/**`, `infra/ephemeral/**` (identity). Hari merges.

## 1. Goal and authority

Goal: when #508 supplies the C03 scenario, plan sources and load generator admission, one dispatch of `scenario.yml` with `scenario=C03` runs phases C0 and C1, five repeats each, with a subject and a witness consumer member per repeat in the order ADR-022 sets, and produces for each repeat the artifacts that `consumer-verify` reads. C10 and C01 take the same route once #510 supplies their data.

Authority, in creation order:
1. The issue body of #507, which names split entry 4 of [proposal revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894560135) and links decisions 6894534441, 6894564479, 6894587233 and 6894614673 on #499.
2. [Decision 6895300348](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6895300348), adopting #499 round 1 ([6895299617](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617)): group shape, start order, invalid runs.
3. [Decision 6895329189](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6895329189), adopting #499 round 2 ([6895328446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446)): point 4 puts the `consumer-record` container and its roles in this entry; point 5 puts both members on the `workload` node; point 7 fixes verifier order.
4. [Decision 6895537201](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6895537201), adopting the [standing decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291): grilling questions go to "Open questions for Hari" with recommended answers; plan approval on a clean check; point 4 lists what it does not cover.
5. [decision 6915070211](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6915070211), adopting the recommended answers to Q1, Q2, Q5 and Q6 of [handoff 6915055673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6915055673) under the standing decision.
6. [decision 6925863575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6925863575), Hari's answers to Q3, Q4 and DA1 to DA5: the bounds as recommended except a consumer phase budget of 7200 s; #508's smoke run counts as this design's live smoke; DA1 to DA5 accepted by name.

Design authority: docs/blueprint.md ADR-020 (lines 623 to 661) and ADR-022 (lines 735 to 797). Contracts set for this pull request by the merged predecessors: the approved worker plan, `items/505/plan-consumer-worker.md` at `c617dfba872ff10b89cf248dfd044af7ec135a74` (section 3.2 keys; deployment assumptions D1 and D2), and the approved verifier plan, `items/506/plan-consumer-verifier.md` at `a4c5ea1bec8630359a31d9b096416b2204f85dc7` (section 10(c), line 333; DA2). Where those plans and the merged code differ, this plan follows the merged code on the base commit and cites it.

Not adopted on this issue, read as context only: #499 round 3 ([6895343227](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895343227)) and [6899939624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6899939624). Nothing in this plan depends on them except where Q5 asks for adoption.

## 2. Scope

Files allowed to change:
- `.github/workflows/scenario.yml` and its byte-identical copy `docs/operations/workflows/scenario.yml.txt`.
- `infra/scripts/phase/phase.py`; a new `infra/scripts/phase/consumer_repeat.py`.
- `infra/scripts/phase/tests/test_phase.py`, `infra/scripts/phase/tests/test_workflow.py`; a new `infra/scripts/phase/tests/test_consumer_repeat.py`.
- `charts/parchwell/templates/jobs.yaml`, `charts/parchwell/templates/_helpers.tpl`, `charts/parchwell/templates/serviceaccounts.yaml`; a new `charts/parchwell/templates/_consumer.tpl`.
- `charts/parchwell/values.yaml`, `charts/parchwell/values.schema.json`.
- `charts/parchwell/tests/test_chart.py`, `charts/parchwell/tests/check.sh`; a new `charts/parchwell/tests/test_consumer_chart.py`.
- `infra/ephemeral/storage.tf`, `infra/ephemeral/identity.tf`, `infra/ephemeral/outputs.tf`; `tests/terraform/ephemeral/main.tftest.hcl` (reached also through the `infra/ephemeral/tests` symlink).
- Fixture files under `infra/scripts/tests/fixtures/` and `charts/parchwell/tests/fixture.yaml`, only under the fixture rule in section 6.
- `docs/operations/chart-contract.md`; a new `docs/operations/consumer-workflow.md`.

Must not change: every `src/**` and `tests/Parchwell.*/**` file; any `*.csproj`, `Directory.*.props` or lock file; `scenarios/**`, `results/**`; `docs/blueprint.md`; `infra/persistent/**`, `infra/cluster/**`; every workflow other than `scenario.yml`; the `on:` block of `scenario.yml` other than the `scenario` input's `options` list; `AGENTS.md`, `CLAUDE.md`, `README.md`; `items/**` other than this plan.

Out of scope, owned elsewhere:
- Production plan sources for the worker and verifier, C03 scenario and profile data, and load generator admission of consumer pairs: #508, extended by #510 for C10 and C01 (verifier plan section 10a items 1 and 6; worker plan section 10a item 1).
- Phase judgement of the `consumer` block: #521. Until it merges, the `judge` step fails for C01, C03 and C10 (`src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` line 45 does not exempt `judge`).
- Subject self-termination: #509.
- The hosted findings on configured consumer row limits: #529.

## 3. External contracts

Everything in this section is read by a later child, by the merged workers or by Hari. The implementer copies it exactly.

### 3.1 Dispatch and routing

1. The `scenario` input's `options` list becomes, in this order: `A01`, `B04`, `B10`, `C03`, `C10`, `C01`, `probe-batch`, `probe-batch-2`, `idle-observation`, `ledger-batch`. Its default stays `A01`. No other input changes. Triggers stay `workflow_dispatch` only.
2. C03, C10 and C01 take B04's route ([b04-workflow.md](../../docs/operations/b04-workflow.md) paragraph 3): job `probe` runs phase C0, job `full-C0` is skipped, job `full-C3` runs phase C1. The C0 candidate does not gate C1.
3. `PARCHWELL_PHASE` in job `probe` becomes `${{ (inputs.scenario == 'B04' || inputs.scenario == 'B10' || inputs.scenario == 'C03' || inputs.scenario == 'C10' || inputs.scenario == 'C01') && 'C0' || 'probe' }}`; in `full-C3`, the same condition with `'C1' || 'C3'`.
4. The `full-C0` job's `if` adds `&& inputs.scenario != 'C03' && inputs.scenario != 'C10' && inputs.scenario != 'C01'` immediately after `inputs.scenario != 'B10'`, inside the same parentheses.
5. Artifact names. In jobs `probe` and `full-C3`, the `name:` expressions of the three upload steps, candidate, pods and archive manifest (`scenario.yml` lines 219, 229 and 246; 705, 715 and 732), gain the branches `inputs.scenario == 'C03' && 'c03-C0' || inputs.scenario == 'C10' && 'c10-C0' || inputs.scenario == 'C01' && 'c01-C0'` (job `probe`) and the same with `C1` (job `full-C3`), placed after the B10 branch and before the A01 fallback. Resulting names: `c03-C0-candidate-<run_id>-<run_attempt>`, `c03-C0-pods-<run_id>-<run_attempt>`, `c03-C0-archive-<run_id>-<run_attempt>`, and likewise for C1, C10 and C01. The `full-C0` job's literal `a01-C0` names are unchanged.
6. Images. Job `probe` gains the output `enrichment_digest: ${{ steps.images.outputs.enrichment_digest }}`. In `full-C3`, the `allowed` step's `case` gains the arm `C03|C10|C01) bash infra/scripts/phase/phase.sh allow-images ;;` and its `env` gains `ENRICHMENT_DIGEST: ${{ needs.probe.outputs.enrichment_digest }}`; the `images` step's `env` gains the same line. The `use-images` command keeps its two positional digests.
7. `phase.py` constants: `SCENARIOS` gains `"C03": {"C0": "C0", "C1": "C1"}`, `"C10": {"C0": "C0", "C1": "C1"}` and `"C01": {"C0": "C0", "C1": "C1"}`; a new `CONSUMER_SCENARIOS = ("C01", "C03", "C10")`. Run IDs use the existing prefix rule (`phase.py` line 472): `c03-<run_id>-<run_attempt>`, `c10-...`, `c01-...`.
8. For a scenario in `CONSUMER_SCENARIOS`, `images()` builds and reads back three workers in this order: `loadgen`, `verifier`, `enrichment`. `publish-images` writes `enrichment_digest=<digest>` to `GITHUB_OUTPUT` as it does the other two. `use-images` reads the third digest from env `ENRICHMENT_DIGEST`; `allow-images` writes `workload=true` only when all three of `LOADGEN_DIGEST`, `VERIFIER_DIGEST` and `ENRICHMENT_DIGEST` match `DIGEST`. For every other scenario both commands keep their current behaviour and ignore `ENRICHMENT_DIGEST`.

### 3.2 Per-repeat sequence

For a scenario in `CONSUMER_SCENARIOS`, `run-repeats` runs repeats 1 to 5 in order. Each repeat runs these steps in this order. R is `<run_id>/<phase>/r<n>/` with the phase in upper case.

| Step | Record `mode` | Action |
|---|---|---|
| S1 | `consumer-subject` | render and apply the subject Job; do not wait |
| S2 | `consumer-witness` | render and apply the witness Job; do not wait |
| S3 | `consumer-readiness` | poll the members' records under section 3.3 |
| S4 | `start-line` | existing start-line Job, render, apply, wait, collect |
| S5 | `loadgen` | existing load generator Job, render, apply, wait, collect |
| S6 | `consumer-subject` | wait for the subject Job to be terminal, then collect |
| S7 | `consumer-witness` | wait for the witness Job to be terminal, then collect |
| S8 | `verify` | the verifier Job in mode `consumer-verify`, render, apply, wait, collect |

Rules:
1. Every repeat appends exactly six records to `phase-status.json` `modes`, in this order: `consumer-subject`, `consumer-witness`, `consumer-readiness`, `start-line`, `loadgen`, `verify`. S1 and S2 append the two member records with the existing fields (`phase.py` line 706) and `wait` and `collect` null. S6 and S7 update those two records; they append nothing.
2. S1 runs first. If S1's render fails or its apply exits non-zero, the subject record keeps `outcome` `render_failed` or `apply_failed`, S2 is not run and the witness record is appended with `outcome: "not_attempted"` and `reason: "consumer_apply_failed"`. If S1 succeeds and S2's render or apply fails, the witness record keeps `render_failed` or `apply_failed`. In either case, the `consumer-readiness`, `start-line`, `loadgen` and `verify` records are appended with `outcome: "not_attempted"` and `reason: "consumer_apply_failed"`, S6 and S7 are not run, and the session deletes both member Jobs by name and confirms no pod remains, using the existing delete-and-confirm commands (`phase.py` lines 767 to 785). Any failure of those commands sets `safety_stop`.
3. When S3 ends `not_observed`, the start line is not recorded for that repeat, because the verifier plan sets "start-line after both members hold an assignment" (`items/506/plan-consumer-verifier.md` line 333; Q2). The `start-line`, `loadgen` and `verify` records are appended with `outcome: "not_attempted"` and `reason: "consumer_assignment_not_observed"`, and the session deletes both member Jobs and confirms as in rule 2. When S3 ends `ready`, S4 and S5 run.
4. S6 waits for the subject Job and S7 for the witness Job with the existing `wait-job` command. The deadline argument is anchored to that member's apply: `max(1, ceil(A + consumer_job_seconds - now))`, where A is the monotonic time at which that member's apply returned; the command timeout is that argument plus 90. Exit 0 (Complete) and exit 1 (Failed) are terminal: `outcome` becomes `complete` or `failed`. Any other exit means the Job is not known to be terminal: `outcome` becomes `unknown`. `collect-job` then runs for that member in every case, as the existing `run_mode` does (`phase.py` lines 758 to 765). After collection, a member with `outcome: "unknown"` goes through the existing delete-and-confirm path.
5. When either S6 or S7 ends with `outcome: "unknown"`, the `verify` record is appended with `outcome: "not_attempted"` and `reason: "consumer_wait_exhausted"`, and `consumer-verify` is not invoked (verifier plan line 333; [#506 decision 6899696796](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6899696796) point 3; Q5). The member records keep their `wait` exit codes as the wait diagnostics.
6. For consumer scenarios only, once `safety_stop` is set, every later record of every repeat is appended with `outcome: "not_attempted"` and `reason: "safety_stop"`, and its step runs no command. A01, B04 and B10 keep their existing behaviour (`phase.py` lines 731 to 735).
6a. Phase budget. `run-repeats` for a consumer scenario records its start time T. Repeat n starts only when `T + CONSUMER_PHASE_SECONDS - now` is at least `CONSUMER_REPEAT_RESERVE_SECONDS`, with `CONSUMER_PHASE_SECONDS = 7200` and `CONSUMER_REPEAT_RESERVE_SECONDS = 5100` (policy values set by Hari, [decision 6925863575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6925863575) point 1). Otherwise every record of repeats n to 5 is appended with `outcome: "not_attempted"` and `reason: "orchestration_deadline"`. Exhaustion action: record the gap and continue; the `judge`, `candidate`, upload and destroy steps still run. 5100 is the configured worst case of one repeat, 5042 s, rounded up; section 14 Q3 gives the sum.
6b. Bound that follows ([decision 6925863575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6925863575) point 1). No repeat starts after 2100 s into `run-repeats` (7200 - 5100). Repeat 5 starts only when repeats 1 to 4 together took at most 2100 s, an average of at most 525 s each; otherwise repeat 5, and any later-skipped repeat, is recorded `not_attempted` with `reason: "orchestration_deadline"`. `run-repeats` ends within 7200 s of T. #508 and #510 must choose consumer windows under which five repeats fit this bound; if they cannot, they bring the conflict to Hari rather than raise the budget. For comparison only: `infra/scripts/phase/ledger_batch.py` line 17 bases its five-repeat configuration deadline on "B04's measured 1726 s and 1729 s". If those are five-repeat phase durations, a B04 repeat took about 345 s without readiness or member steps (inference; the comment does not say what the durations cover).
7. `run-repeats` exits 1 when any record has an outcome in `BAD_OUTCOMES`, as today (`phase.py` line 805). A member record with `outcome: "failed"` is not in `BAD_OUTCOMES`; a killed subject ends Failed by design.
8. The existing `judge`, `candidate`, upload, `delete-workload` and destroy steps run unchanged after `run-repeats`.

### 3.3 Readiness poll (S3)

1. Constants in `consumer_repeat.py`: `READINESS_CAP_SECONDS = 300` (policy, Q2), `READINESS_INTERVAL_SECONDS = 5`, `PARTITIONS = 32` (`infra/ephemeral/eventhubs.tf` line 15, `document-events` `partition_count`), `RECORD_BLOB_MAXIMUM = 2621440000` (65536 times 40000, the worker's row cap times rows per blob, `src/Parchwell.Verifier/Common/Consumer/ConsumerRecordLimits.cs` lines 6 to 8).
2. Each poll lists container `consumer-record` with prefix `R + "consumer/"` using `az storage blob list --account-name <account> --container-name consumer-record --prefix <prefix> --auth-mode login --num-results 1000 --only-show-errors -o json`, then downloads each blob whose name matches `^<prefix>[^/]+/record/[0-9]{6}\.jsonl$` with the existing `download_blob` helper (`phase.py` lines 1458 to 1485), `container="consumer-record"`, `maximum=RECORD_BLOB_MAXIMUM`, `minimum=0`.
3. The poll is ready when all of these hold: exactly two distinct member segments appear under the prefix, each with a `registration.json`; in each member's record, read in blob order and line order, the last row whose `kind` is `partition_event` has `event` equal to `assigned`; the two `partitions` lists of those rows are disjoint; their union is exactly the integers 0 to 31.
4. A record line that is not a JSON object, or a list or download failure, makes that poll not ready. It does not end the poll early.
5. The record for S3 has `mode: "consumer-readiness"`, `repeat`, `outcome` (`ready` or `not_observed`), `polls` (count), `elapsed_seconds` (integer, rounded down) and `last_condition`, one of `ready`, `list_failed`, `download_failed`, `row_invalid`, `member_count`, `not_assigned`, `overlap`, `coverage`, checked in that order. It never carries blob contents. `not_observed` is not in `BAD_OUTCOMES`.
6. The poll stops at the first ready poll or when `READINESS_CAP_SECONDS` has passed since S2's apply returned. Every command of S3 runs under `command_deadline` set to that instant (`phase.py` lines 354 to 361), so S3 ends within 300 s.

### 3.4 Chart: consumer member Jobs

1. New runtime modes `consumer-subject` and `consumer-witness`. They are accepted only when `scenario_id` is `C01`, `C03` or `C10`, `configuration_id` is `C0` or `C1`, and `runtime.phase` equals `configuration_id`. Every other combination fails the schema.
2. Job name: `<run_id>-<lower phase>-r<repeat>-consumer-subject` or `...-consumer-witness`, matching the worker's rule (`src/Parchwell.Enrichment/ConsumerProcessing/ConsumerProcessingOptions.cs` lines 26 and 29). With a 29-character run ID the longest name is 52 characters.
3. Job spec: `completions: 1`, `parallelism: 1`, `backoffLimit: 0`, `podReplacementPolicy: Failed`, `activeDeadlineSeconds` equal to `bounds.execution.consumer_job_seconds`, which is `1800` (Q3). Job and pod `metadata.labels` include `parchwell.labels` (`_helpers.tpl` lines 1 to 4), as `jobs.yaml` lines 36 and 49 do, so `delete-workload` selects the member Jobs (`phase.py` lines 1703 to 1710). Pod: `shareProcessNamespace: true` (rule 3a), `restartPolicy: Never`, `terminationGracePeriodSeconds: 30`, `serviceAccountName` from `runtime.service_accounts["enrichment-consumer"].name`, `automountServiceAccountToken: false`, label `azure.workload.identity/use: "true"`, `parchwell.io/role: "enrichment-consumer"`, `nodeSelector: {workload: "workload"}`, no tolerations, the same pod and container security settings as the existing repeat Jobs (`jobs.yaml` lines 66 to 86).
3a. Why the shared process namespace. The subject ends itself with libc `kill` on its own process ID and signal 9 (`src/Parchwell.Enrichment/ConsumerProcessing/ConsumerSelfSignal.cs` at the base commit). Without a shared process namespace the worker is the container's first process, PID 1. Linux ignores SIGKILL that a namespace's PID 1 sends to itself: measured in this planning session with `unshare --pid --fork --mount-proc` on Linux 6.18, a Python process that was PID 1 survived `os.kill(os.getpid(), SIGKILL)` and exited 0, and the same code as PID 2 was killed with exit 137. The worker's preflight `kill(pid, 0)` succeeds either way (`ConsumerProcessingWorker.cs` lines 38 to 42), so the failure would appear only at the kill, as `consumer_self_kill_failed`. With `shareProcessNamespace: true`, "the first process in each container will not be assigned PID 1" (Kubernetes PodSpec reference); the pod sandbox holds PID 1. Both members get the setting, so their pod specs differ only in role values.
4. Container: name `enrichment-consumer`, image `runtime.enrichment_image`, `imagePullPolicy: IfNotPresent`, no `command`, `args: ["--consumer-processing"]` (`src/Parchwell.Enrichment/Program.cs` lines 36 and 37). Resources: requests `cpu: 500m`, `memory: 1Gi`; limits `cpu: "1500m"`, `memory: 4Gi` (Q3). Volumes `/work` (emptyDir, `sizeLimit: 8Gi`) and `/tmp` (emptyDir), as the existing Jobs.
5. Environment, exactly these names and no others:

| Name | Value |
|---|---|
| `PARCHWELL_SOURCE_COMMIT` | `runtime.source_commit` |
| `PARCHWELL_JOB_NAME` | the Job name |
| `PARCHWELL_POD_NAME` | fieldRef `metadata.name` |
| `PARCHWELL_POD_UID` | fieldRef `metadata.uid` |
| `PARCHWELL_NODE_NAME` | fieldRef `spec.nodeName` |
| `ConsumerProcessing__RunId` | `runtime.run_id` |
| `ConsumerProcessing__ScenarioId` | `scenario_id` |
| `ConsumerProcessing__Phase` | `runtime.phase` |
| `ConsumerProcessing__RepeatNumber` | `runtime.repeat`, base-10 string |
| `ConsumerProcessing__Role` | `subject` or `witness` |
| `ConsumerProcessing__JobName` | the Job name |
| `ConsumerProcessing__PodName` | fieldRef `metadata.name` |
| `ConsumerProcessing__PodUid` | fieldRef `metadata.uid` |
| `ConsumerProcessing__NodeName` | fieldRef `spec.nodeName` |
| `ConsumerProcessing__SourceCommit` | `runtime.source_commit` |
| `ConsumerProcessing__NamespaceName` | `runtime.eventhubs.namespace_name` |
| `ConsumerProcessing__BlobEndpoint` | `runtime.blob.account_url` |

   Source: `ConsumerProcessingOptions.cs` lines 9 to 20 and 41 to 45; the section binds with `ErrorOnUnknownConfiguration = true` (`Program.cs` lines 186 to 188), so an extra `ConsumerProcessing__*` name fails the member. The `AZURE_*` names come from the workload identity webhook, not the chart ([chart-contract.md](../../docs/operations/chart-contract.md), "Worker settings checks").
6. `ConsumerProcessing__NamespaceName` and the verifier's `Verifier__NamespaceName` are the same `runtime.eventhubs.namespace_name` input in every render of one repeat (verifier plan line 333; `src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs` line 47).
7. Runtime inputs: for `scenario_id` in `C01`, `C03`, `C10`, every mode requires `runtime.enrichment_image` (existing pattern `^[^/ ]+/parchwell/enrichment@sha256:[a-f0-9]{64}$`, `values.schema.json` lines 593 to 596) and `runtime.service_accounts` with exactly `loadgen`, `verifier` and `enrichment-consumer`. The chart then renders those three service accounts. For every other scenario, inputs and rendered service accounts are unchanged.
8. `phase.py` `values()` supplies, for a consumer scenario, `enrichment_image` from `phase["images"]["enrichment"]["reference"]` and the three service accounts, for every mode including `judge`.

### 3.5 Chart: verifier modes for consumer scenarios

1. Mode `start-line` with `scenario_id` in `C01`, `C03`, `C10` renders the existing start-line environment plus `Verifier__ConsumerRecordContainer` = `consumer-record` (`VerifierHostOptionsValidator.cs` lines 81 and 82).
2. Mode `verify` with `scenario_id` in `C01`, `C03`, `C10` renders the existing verify environment, which since #528 includes the `Verifier__ConsumedMetadata__*` names (`jobs.yaml` lines 152 to 157), with two differences: `Verifier__Mode` = `consumer-verify` (`VerifierHostOptions.cs` line 167), and `Verifier__ConsumerRecordContainer` = `consumer-record`. The Job name stays `<run_id>-<lower phase>-r<repeat>-verify`, as the verifier requires (`VerifierHostOptionsValidator.cs` lines 417 to 422).
3. Modes `loadgen` and `judge` render unchanged for these scenarios. Modes `loadgen2`, `loadgen-restart`, `loadgen-successor`, `b10-abrupt-verify`, `b10-sweep-line`, `b10-sweep`, `b10-final-sweep` and every single-action, idle and probe-v2 mode fail the schema for these scenarios.
4. The `execution` section of `bounds` gains `consumer_job_seconds: 1800`; every other bound is unchanged.

### 3.6 Terraform

1. `infra/ephemeral/storage.tf` `containers` gains `consumer_record = { name = "consumer-record", public_access = "None", role_assignments = {} }`, placed after `single_action`.
2. `infra/ephemeral/identity.tf` `baseline_workload_roles` gains exactly two entries:
   - `enrichment_consumer_record_contributor = { workload = "enrichment-consumer", role_definition_name = "Storage Blob Data Contributor", scope = module.storage.containers.consumer_record.id }`
   - `verifier_consumer_record_reader = { workload = "verifier", role_definition_name = "Storage Blob Data Reader", scope = module.storage.containers.consumer_record.id }`
   Source: ADR-022 "Durability" (docs/blueprint.md line 754).
3. `infra/ephemeral/outputs.tf` output `blob.containers` gains `consumer_record = module.storage.containers.consumer_record.name`.
4. `azurerm_role_assignment.workflow_evidence_reader` (`identity.tf` lines 31 to 38, added by #528) changes its `for_each` to `toset(["ledger", "verifier", "single_action", "consumer_record"])`, giving the workflow principal Storage Blob Data Reader on `consumer-record` only, for the readiness poll of section 3.3 (Q7). No other identity gains a role, and no grant is account-scoped.

### 3.7 Contracts this plan sets for #508 and #510

These are bounds the scenario children must fit. They are not dependencies of this pull request's tests.
1. The consumer window: `WindowMs` must not exceed 1620000 (1800 s Job deadline, minus the worker's 120 s watchdog margin after the window, `ConsumerProcessingWorker.cs` lines 53 to 59, minus 60 s for startup), and must cover readiness (up to 300 s), the start-line Job and the load generator Job.
2. `CompletionDeadlineMs` must be below 40000. With the chart's verify bounds, `ProcessLifetime` 720 s must exceed Cleanup 10 + Io 10 + Token 10 + Metadata 10 + Watermark 10 + BlobIo 30 + SafetyStop 600 s plus the deadline (`src/Parchwell.Verifier/Common/Consumer/ConsumerVerificationPlan.cs` lines 3 to 26).
3. A child that needs larger bounds changes `values.yaml` and its schema constants in its own plan.

## 4. Requirements

- R1. WHEN `scenario` is C03, C10 or C01, THEN job `probe` runs with `PARCHWELL_PHASE=C0`, `full-C0` is skipped, and `full-C3` runs with `PARCHWELL_PHASE=C1`.
- R2. WHEN `scenario` is A01, B04 or B10, THEN every job's `if`, `PARCHWELL_PHASE`, step list and artifact name evaluates as on the base commit.
- R3. WHEN `validate-scenario` runs with `PARCHWELL_SCENARIO` C03, C10 or C01 and no authentication flag, THEN it exits 0; WHEN it runs with `C02` or `c03`, THEN it exits 2 with `unsupported scenario`.
- R4. WHEN `prepare C0` or `prepare C1` runs for a consumer scenario, THEN `phase.json` has `scenario` equal to that ID, `phase` and `configuration` equal to the argument, and `run_id` `<lower ID>-<GITHUB_RUN_ID>-<GITHUB_RUN_ATTEMPT>`; WHEN `prepare C3` or `prepare probe` runs for one, THEN it exits 2 with `unsupported phase`.
- R5. WHEN `publish-images` runs for a consumer scenario, THEN it publishes `loadgen`, `verifier` and `enrichment` in that order and writes three digest lines to `GITHUB_OUTPUT`; for A01, B04 and B10 it publishes two, as today.
- R6. WHEN `allow-images` runs for a consumer scenario with any of the three digests missing or malformed, THEN it writes `workload=false`; with all three valid, `workload=true`. For A01, B04 and B10 an absent `ENRICHMENT_DIGEST` does not change the result.
- R7. WHEN `use-images` runs for a consumer scenario, THEN the three read-back digests must equal the two arguments and `ENRICHMENT_DIGEST`; any mismatch exits 1 with `image digest mismatch: <worker>`.
- R8. WHEN `run-repeats` runs for a consumer scenario and every command succeeds, THEN `phase-status.json` `modes` holds, for each repeat 1 to 5 in order, six records with modes `consumer-subject`, `consumer-witness`, `consumer-readiness`, `start-line`, `loadgen`, `verify`, in that order, 30 in all, and the command order is S1 to S8 of section 3.2.
- R9. WHEN the subject or witness Job's apply fails, THEN rule 2 of section 3.2 applies to that repeat, and the next repeat starts normally unless `safety_stop` is set.
- R10. WHEN the readiness poll sees no ready poll within 300 s, THEN its record has `outcome: "not_observed"` with the last condition, S4, S5, S6, S7 and S8 run no command, the three later records carry `reason: "consumer_assignment_not_observed"`, and both member Jobs are deleted and confirmed.
- R10a. WHEN a consumer phase's remaining budget, `7200 - elapsed`, is below 5100 s at the start of a repeat, THEN that repeat and every later one append six records with `reason: "orchestration_deadline"` and run no command.
- R11. WHEN a member Job ends Failed (exit 1 of `wait-job`), THEN its record has `outcome: "failed"` and S8 runs.
- R12. WHEN a member Job's wait exits other than 0 or 1, THEN that record has `outcome: "unknown"`, `collect-job` runs for it, then the delete-and-confirm path runs for it, and S8 is recorded `not_attempted` with `reason: "consumer_wait_exhausted"`.
- R12a. WHEN S6 starts 2000 s after the subject's apply returned, THEN its `wait-job` argument is `1` and its command timeout 91; WHEN it starts 1000 s after, THEN the argument is `800` and the timeout 890.
- R13. WHEN the chart renders `consumer-subject` or `consumer-witness`, THEN it renders exactly one Job with the spec of section 3.4 and exactly the 17 environment names of its table, and three service accounts.
- R14. WHEN the chart renders `start-line` or `verify` for a consumer scenario, THEN it renders section 3.5 rules 1 and 2; WHEN it renders them for A01, B04 or B10, THEN the environment contains neither `Verifier__ConsumerRecordContainer` nor `Verifier__Mode=consumer-verify`.
- R15. WHEN a consumer mode is rendered with a scenario other than C01, C03 or C10, a configuration other than C0 or C1, a phase not equal to the configuration, a missing `enrichment_image`, or `service_accounts` without `enrichment-consumer`, THEN the render fails and produces no Job.
- R16. WHEN a mode listed in section 3.5 rule 3 as failing is rendered for C01, C03 or C10, THEN the render fails.
- R17. WHEN Terraform plans the ephemeral root with default variables, THEN the storage module has five containers (`ledger`, `verifier`, `claim-check`, `single-action`, `consumer-record`), `azurerm_role_assignment.workload` has 14 instances, the two new assignments have the principals, roles and scopes of section 3.6, and `azurerm_role_assignment.workflow_evidence_reader` has exactly the keys `ledger`, `verifier`, `single_action` and `consumer_record`, each Storage Blob Data Reader on its own container.
- R18. WHEN `probe_namespace_data_roles` is true, THEN the role set is the default 14 plus the existing two namespace roles.

## 5. Invariants and safety rules

- Triggers of `scenario.yml` stay `workflow_dispatch` only (hard safety rules 8 and 9). The installed workflow and `docs/operations/workflows/scenario.yml.txt` are byte-identical.
- At most one ephemeral environment: the shared `parchwell-ephemeral` concurrency group is unchanged.
- Each job still destroys its ephemeral layer under the existing `if: always()` conditions; this pull request adds no step after `destroy` and removes none.
- No production .NET code changes; the workflow alone creates, waits for and deletes Jobs (ADR-020).
- Identity: only the two workload role assignments of section 3.6 and the workflow principal's Reader on `consumer-record` are added. No existing assignment changes scope or role. The verifier and the workflow gain read only; the enrichment-consumer identity gains Contributor on one new container.
- The readiness poll reads only the `consumer-record` container, only under the repeat's prefix, and writes nothing to Blob.
- No member Job ever has two pods: `backoffLimit: 0`, `podReplacementPolicy: Failed`, `restartPolicy: Never`.
- Every existing positive chart case and every existing phase and workflow test keeps its assertions.

## 6. Test map

Standing fixture rule. When an existing test's fixture is rejected by a new check in this plan, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. Basis: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2. Contract updates are separate from this rule. The existing assertions whose expected value is text this plan changes keep their assertion and take exactly the new text of section 3.1 or 3.6: the `scenario` options list (`infra/scripts/phase/tests/test_workflow.py` lines 121, 138 and 599), `sorted(phase.SCENARIOS)` (line 140), the `full-C0` condition (line 147), `PARCHWELL_PHASE` (lines 152 and 154), artifact names (lines 323 and 325) and the `allowed` case text (line 344). In `tests/terraform/ephemeral/main.tftest.hcl`: the role-set assertion (line 474) gains the two new tuples and its count becomes 14; the container assertion (line 391) gains `consumer_record`; the storage configuration assertion's `containers` map (lines 332 to 382) gains `"consumer_record" = { "name" = "consumer-record", "public_access" = "None", "role_assignments" = {} }` with the key shape of `single_action` (lines 354 and 355); and an override for `module.storage.module.containers["consumer_record"].azapi_resource.this` with `"id" = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-parchwell-eph/providers/Microsoft.Storage/storageAccounts/stparchwelloffline1/blobServices/default/containers/consumer-record"` is added, as lines 213 to 216 do for `single_action`. The workflow-reader assertion (lines 265 to 271) changes its key set to `toset(["ledger", "verifier", "single_action", "consumer_record"])` and keeps its other conditions and error message. The PR implementation notes list each as a contract update.

| Test | File | Inputs | Expected |
|---|---|---|---|
| T1 | test_workflow.py | parsed `scenario.yml` | options list exactly section 3.1 rule 1; `on` keys exactly `workflow_dispatch`; installed file bytes equal the committed copy |
| T2 | test_workflow.py | the `probe`, `full-C0`, `full-C3` expressions evaluated for each of A01, B04, B10, C03, C10, C01 | C03, C10, C01: phases C0 and C1, `full-C0` false; A01, B04, B10: values as on the base commit |
| T3 | test_workflow.py | the candidate, pods and archive-manifest upload `name` expressions for C03 in `probe` and `full-C3` | prefixes `c03-C0` and `c03-C1` in all three; C10 and C01 likewise; for A01, B04 and B10 each expression evaluates as on the base commit |
| T4 | test_workflow.py | `full-C3` `allowed` and `images` steps | `case` arm `C03|C10|C01)` runs `allow-images`; both steps carry `ENRICHMENT_DIGEST: ${{ needs.probe.outputs.enrichment_digest }}`; `probe.outputs.enrichment_digest` exists |
| T5 | test_phase.py | `validate-scenario` with C03, C10, C01, C02, c03 | 0, 0, 0, 2, 2 |
| T6 | test_phase.py | `prepare C0`, `prepare C1`, `prepare C3` for C03 | run ID `c03-<id>-<attempt>`; phase C0, C1; exit 2 `unsupported phase` |
| T7 | test_phase.py | `allow-images` for C03 with: three valid digests; enrichment empty; enrichment `sha256:` plus 63 hex; B04 with enrichment empty | `workload=true`, `workload=false`, `workload=false`, `workload=true` |
| T8 | test_consumer_repeat.py | fake runner where every command exits 0 and the third poll is ready | per repeat, command order S1 to S8; `modes` holds 6 records per repeat (two member records, readiness, start-line, loadgen, verify), 30 in all; readiness `polls: 3`, `outcome: ready` |
| T9 | test_consumer_repeat.py | (i) subject apply exits 1 in repeat 2; (ii) subject apply exits 0 and witness apply exits 1 in repeat 2 | (i) subject record `apply_failed`; witness record `not_attempted`, `reason: consumer_apply_failed`, and no witness render or apply command; (ii) subject record keeps `apply: 0`, `wait: null`, `outcome: not_attempted`; witness `apply_failed`. In both: readiness, start-line, loadgen and verify records `not_attempted` with `reason: consumer_apply_failed`; delete and confirm run for both member Job names; six records for repeat 2; repeat 3 runs S1 to S8 |
| T10 | test_consumer_repeat.py | readiness never ready; fake clock advancing 5 s per poll | at 300 s `outcome: not_observed`, `polls: 61`; start-line, loadgen and verify records `not_attempted`, `reason: consumer_assignment_not_observed`, with no start-line, loadgen or verify command; S6 and S7 run no wait; delete and confirm run for both member Jobs; with the clock at 295 s a poll still runs |
| T11 | test_consumer_repeat.py | subject `wait-job` exits 1, witness 0 | subject `outcome: failed`, witness `complete`; verify runs |
| T12 | test_consumer_repeat.py | witness `wait-job` exits 124 | witness `outcome: unknown`; `collect-job` runs for the witness before its delete and confirm; verify `not_attempted`, `reason: consumer_wait_exhausted`; `run-repeats` exits 1 |
| T13 | test_consumer_repeat.py | readiness rows: two members, assigned {0..15} and {16..31} | ready |
| T14 | test_consumer_repeat.py | the T13 rows with, one change each: one member; three members; assigned {0..16} and {16..31}; {0..15} and {17..31}; one member's last `partition_event` is `revoked` after an earlier `assigned`; one record line `[]` appended | not ready, with `last_condition` `member_count`, `member_count`, `overlap`, `coverage`, `not_assigned`, `row_invalid` |
| T15 | test_consumer_repeat.py | list command exits 1 | that poll not ready with `list_failed`; polling continues |
| T16 | test_consumer_repeat.py | `safety_stop` set before repeat 4 | repeats 4 and 5 append six records each with `outcome: not_attempted`, `reason: safety_stop`, and run no command |
| T16a | test_consumer_repeat.py | fake clock, phase start T=0: (i) repeats 1 to 4 take 526 s each; (ii) they take 525 s each; (iii) repeat 1 takes 2101 s | (i) at 2104 s, 5096 s left, below 5100: repeat 5 appends six records with `reason: orchestration_deadline` and runs no command; (ii) at 2100 s exactly 5100 s left: repeat 5 runs; (iii) repeats 2 to 5 append six records each with `reason: orchestration_deadline` |
| T16b | test_consumer_repeat.py | S6 started 2000 s and 1000 s after the subject apply returned, `consumer_job_seconds` 1800 | `wait-job` argument `1`, timeout 91; argument `800`, timeout 890 |
| T17 | test_consumer_chart.py | `consumer-subject` and `consumer-witness` for C03 C0, C10 C1, C01 C0, repeat 1 and 5, 29-character run ID | one Job; name per section 3.4 rule 2 (longest 52 characters); spec values of rule 3, including `shareProcessNamespace: true` and the labels `app.kubernetes.io/name: parchwell` and `app.kubernetes.io/instance: <run_id>` on Job and pod; env names exactly the 17 of rule 5, values as tabled; `args: ["--consumer-processing"]`; no `command`; three service accounts; kubeconform strict passes |
| T18 | test_consumer_chart.py | `start-line` and `verify` for C03 C0 | rule 1 and 2 of section 3.5; verify Job name ends `-r1-verify` |
| T19 | test_consumer_chart.py | negatives: consumer mode with A01, B04; configuration C2; phase C0 with configuration C1; no `enrichment_image`; `service_accounts` without `enrichment-consumer`; `loadgen2` and `b10-sweep` for C03 | each render fails and produces no Job |
| T20 | test_chart.py (existing) | existing positive and negative matrix | unchanged results; for A01, B04, B10 start-line and verify, no `Verifier__ConsumerRecordContainer` name |
| T21 | test_consumer_chart.py | worker option check: env names of T17 against the properties of `ConsumerProcessingOptions.cs` | every property has its `ConsumerProcessing__` name; no rendered name lacks a property |
| T22 | main.tftest.hcl | default plan | R17, including the workflow-reader key set with `consumer_record` |
| T23 | main.tftest.hcl | `probe_namespace_data_roles = true` | R18 |

`check.sh` runs `test_consumer_chart.py` after the existing chart tests.

## 7. Deployment assumptions

Hari accepted DA1 to DA5 by name, against revision 2's text, in [decision 6925863575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6925863575) point 3. None has a test in this pull request; #508's smoke run, which counts as this design's live smoke ([decision 6925863575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6925863575) point 2), is the first live evidence.

- DA1. The workflow principal can list and read blobs in `consumer-record` with `--auth-mode login`. With section 3.6 rule 4 it holds a container-scoped Storage Blob Data Reader grant there, the same pattern #528 added for `ledger`, `verifier` and `single-action`. Evidence: none live for these Reader grants yet; a container-scoped Contributor grant to the same principal worked in the [archive smoke run](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43744609382) (`docs/blueprint.md` line 652 at the base), on a fresh connection, idle. Revision 2's evidence, that no Terraform granted the principal a data role, is no longer true at the base. If it fails, every readiness poll ends `not_observed` and no repeat runs, which #508's smoke run shows at once.
- DA2. Two member pods (requests 500m CPU, 1 GiB each) and the start-line verifier pod (1 CPU, 2 GiB) schedule together on the one `Standard_D4ds_v5` `workload` node (`infra/cluster/aks.tf` line 44). Evidence: none taken; allocatable capacity after system pods was not read.
- DA3. The workload identity webhook injects `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` and `AZURE_FEDERATED_TOKEN_FILE` into the member pods. Evidence: the F04 seed Job runs the same image and identity live (single-action batch runs), on a fresh pod, idle.
- DA4. The worker plan's D1 and the verifier plan's DA2: the role assignments of section 3.6 give the enrichment-consumer identity append and create rights and the verifier read and list rights on `consumer-record` with `WorkloadIdentityCredential`. Evidence: the analogous `ledger` grants work on fresh pods, under load (B06 and B07 runs); not shown for this container.
- DA5. With `shareProcessNamespace: true` on AKS, the subject is not PID 1, so its SIGKILL to itself ends it with exit 137, and Kubernetes then marks its Job Failed because one pod failure exceeds `backoffLimit: 0` (Kubernetes Job documentation, "Terminal Job conditions"). Evidence: the PID 1 behaviour was measured in this planning session on a fresh process, idle, on Linux 6.18 with `unshare`, not on AKS's node kernel or container runtime; the pod-sandbox PID 1 is from the Kubernetes PodSpec reference. Not shown live.

## 8. Acceptance commands

Each command runs on the branch head and its output goes in the PR implementation notes.
1. `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`: all pass, including T5 to T16.
2. `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`: all pass.
3. `bash charts/parchwell/tests/check.sh` with the pinned tools: all pass, including T17 to T21; report the positive and negative case counts.
4. `terraform -chdir=infra/ephemeral test`: all runs pass, including T22 and T23.
5. `terraform fmt -check -recursive infra tests/terraform`: exit 0.
6. `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt`: exit 0.
7. `bash infra/scripts/lifecycle/check-offline.sh`: passes.
8. The repository's dotnet verification jobs pass on the pull request unchanged, since no .NET file changes.

## 9. Documentation

- New `docs/operations/consumer-workflow.md` owns the consumer route: it states section 3.1 rules 2 and 5, the step table and rules of section 3.2, the readiness poll of section 3.3, the bounds of section 3.7, that the `judge` step fails for these scenarios until #521 merges, and that nothing runs live until #508. It opens with two or three plain sentences and states that it records workflow behaviour, not a result.
- `docs/operations/chart-contract.md` gains a section "Consumer member Jobs" that lists the two modes, the 17 environment names and the `consumer_job_seconds` deadline, and links consumer-workflow.md for order. Its "Inputs" paragraph names C01, C03 and C10 as accepted scenario IDs for the modes of section 3.5.

## 10. Sources

- Kubernetes Job documentation, "Delayed creation of replacement pods", "Job termination and cleanup" and "Terminal Job conditions", https://kubernetes.io/docs/concepts/workloads/controllers/job/, read 2026-11-16 through the Context7 documentation tool: `podReplacementPolicy: Failed` waits until a pod is fully terminal before a replacement; `activeDeadlineSeconds` terminates running pods and marks the Job Failed with reason `DeadlineExceeded`; a Job fails when pod failures exceed `backoffLimit`.
- Kubernetes PodSpec reference, `shareProcessNamespace`, https://kubernetes.io/docs/reference/kubernetes-api/core/pod-v1/, and "Share Process Namespace between Containers in a Pod", https://kubernetes.io/docs/tasks/configure-pod-container/share-process-namespace/, read 2026-11-16 through the Context7 documentation tool: with the setting, the first process in each container is not PID 1, and PID 1 is the pod sandbox.
- Measurement in this planning session, 2026-11-16, Linux 6.18: `unshare --pid --fork --mount-proc python3 -c "import os,signal; os.kill(os.getpid(), signal.SIGKILL); print('alive')"` printed `alive` and exited 0 (PID 1); run as PID 2 under `bash -c`, the same code was killed, exit 137.
- Kubernetes downward API, https://kubernetes.io/docs/concepts/workloads/pods/downward-api/, "Information available via fieldRef", read 2026-11-16 through the same tool: `spec.nodeName` is available as an environment variable.
- docs/blueprint.md ADR-020 and ADR-022 at the base commit.
- The merged worker and verifier code at the base commit, cited inline.
- Plan rule 1 (external behaviour): this plan relies on no Kafka, librdkafka, Confluent.Kafka or Event Hubs behaviour beyond what ADR-022 and the merged code already state. The partition count 32 is this repository's configuration, not a service default.

## 11. Plan-check questions

(a) Effort scaling with a number unknown at planning time: none in the implementation. Test counts are fixed by section 6. At run time the readiness poll is capped at 300 s and 61 polls per repeat, and a phase at 7200 s with record-the-gap exhaustion (section 3.2 rules 6a and 6b); neither runs in this pull request.
(b) Stricter than the parent: the readiness poll, which gates each repeat's start line as the verifier plan's line 333 requires (Q2); the phase budget and per-repeat reserve, the member deadline and the resource values (Q3); `shareProcessNamespace` and `podReplacementPolicy: Failed` on the members (Q6); and the section 3.7 bounds for #508 and #510, which no parent decision sets.
(c) Checks depending on code, data or behaviour neither on `main` nor in this pull request: none of this pull request's tests do. Live behaviour depends on #508 (plan sources, scenario data, load generator admission) and #521 (judge); those are listed in section 12, not tested here. DA1 to DA5 are listed in section 7.

## 12. Deferred work

1. Live smoke of this design. Evidence: no consumer scenario can run before #508 (worker `Program.cs` lines 153 to 155 exit `consumer_plan_invalid`; load generator rejects consumer pairs at `src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs` lines 94 to 97). On the verdict path: yes. Why deferral is permitted: the approved split puts the smoke run in entry 5 (#508), and [decision 6925863575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6925863575) point 2 counts it as this design's live smoke under docs/agents/factory.md "Item lifecycle" step 1. Responsible: #508. Stage: after #508 and #521 merge. Completion evidence: one C03 smoke dispatch whose repeat records show S1 to S8 and a `consumer-verify` result. Gate: #508's merge and the C03 batch run.
2. Phase judgement for consumer scenarios: #521, gating #508's smoke run.
3. Plan sources, scenario data and load generator admission: #508 for C03, #510 for C10 and C01.
4. Evidence archive support for C01, C03 and C10. Evidence: `infra/scripts/phase/archive.py` line 1817 at the base allows only A01, B04, B10 and the batch scenarios, so the archive step fails for a consumer dispatch. The step is `continue-on-error`, and the job's result comes from the `science_report` step, which excludes the archive steps (`infra/scripts/phase/tests/test_workflow.py` lines 367 to 370), so the job does not fail; the `continue-on-error` `report` step's summary shows the archive failure. ADR-020 at the base (`docs/blueprint.md` line 651) says an archive gap holds publication. On the verdict path: yes, for #511 and the C10 and C01 verdicts. Not on the path of #508's smoke run, which needs no archive. The consumer record is also not in the archive inventory. Why deferral is permitted: `archive.py` and its inventory design are outside this pull request and outside every entry of the approved split; see Q8. Responsible: decided by Hari under Q8. Completion evidence: a merged archive change whose consumer dispatch archives a complete inventory. Gate: the first consumer verdict pull request.

## 13. Size estimate

Estimate, 2026-11-16, by analogy with the B04 admission and the single-action chart additions, not measured: about 1300 changed lines in about 20 files (workflow and copy 40; phase.py and consumer_repeat.py 350; Python tests 450; chart templates and schema 250; chart tests 200; Terraform and its test 30; documentation 80). Size is reported, not enforced.

## 14. Decided questions

Revision 2 put these to Hari with recommended answers. They are kept as the record of each choice. Q1, Q2, Q5 and Q6 were adopted as recommended by [decision 6915070211](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6915070211). Q3 and Q4 were answered by [decision 6925863575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6925863575): Q4 as recommended, Q3 as recommended except a consumer phase budget of 7200 s. Where a question's text below says 14400 s, the decision replaces it with 7200 s, as section 3.2 rules 6a and 6b state.

- Q1. Route. Recommended: B04's two-job route, one dispatch option per item (section 3.1). Alternative: one `consumer-batch` option running all three items in one job, as `ledger-batch` does. That needs a new batch module and puts up to 30 repeats in one job, which cannot fit the 360-minute job timeout at the configured worst case in Q3 (estimate). Effect on the verdict route: none beyond the route.
- Q2. Readiness. Recommended: the workflow polls the member records for at most 300 s (policy value, not measured). A repeat that is not ready by then runs no start line, load generator or verifier and is recorded `consumer_assignment_not_observed` (section 3.2 rule 3). This keeps the order the approved verifier plan sets for this pull request, "start-line after both members hold an assignment" (`items/506/plan-consumer-verifier.md` line 333). Its cost: if DA1 fails, no repeat runs, which #508's smoke run shows at once. Alternatives: (a) proceed to the start line whatever the outcome and let the verifier judge `consumer_start_assignment_invalid`; that departs from line 333 and was revision 1's recommendation; (b) a fixed wait after both pods run, with no Blob read and no DA1, which also departs from line 333 because nothing observes the assignment.
- Q3. Member Job and phase bounds. Decided, all policy values, not measured: member `activeDeadlineSeconds` 1800; member requests 500m CPU and 1 GiB, limits 1500m CPU and 4 GiB; readiness cap 300 s; consumer phase budget 7200 s (Hari; revision 2 recommended 14400 s) with a 5100 s per-repeat reserve (section 3.2 rules 6a and 6b).
  Basis for the reserve, computed from the configured command timeouts at the base commit (estimate, 2026-11-16): two member renders and applies, 2 x (120 + 60) = 360 s (`phase.py` lines 690 and 755); readiness 300 s; start-line 120 + 60 + (150 + 90) + 210 = 630 s and loadgen 120 + 60 + (1140 + 90) + 210 = 1620 s (`phase.py` lines 755 to 761, `values.yaml` lines 100 to 105); the two member waits start at least 2550 s after their applies, past the 1800 s deadline, so each is 91 s plus a 210 s collect, 602 s; a delete and confirm for each member if both end `unknown`, 2 x (120 + 30) = 300 s; verify 120 + 60 + (750 + 90) + 210 = 1230 s. Sum 5042 s. Revision 1's 3500 s left out the member steps (plan check finding 2).
  Basis for revision 2's recommended phase budget, kept as record: the job timeout is 21600 s; 14400 s for `run-repeats` would have left 7200 s for image publication, cluster and ephemeral apply, the DNS wait, judge, candidate, uploads and destroy. Those steps are not measured here; `infra/scripts/phase/ledger_batch.py` line 17 cites "B04's measured 1726 s and 1729 s" for a configuration's repeats, not its infrastructure. Typical repeats should be far shorter than 5042 s, because the member windows run during the start line and load generator (inference).
  Requests leave room for the start-line pod on the one 4-vCPU node (DA2). CPU limits bound processing speed, which C03 measures against a simulated delay; the limit is above the request so the simulated delay, not throttling, should dominate (inference).
- Q4. Live smoke before approval. docs/agents/factory.md "Item lifecycle" step 1 says a plan that changes live infrastructure is not approvable until a live smoke run of that design exists. This plan adds a container and two role assignments, and nothing in it can run live before #508. Recommended: treat #508's smoke run as the live smoke of this design, as the approved split already places it, and accept DA1 to DA5 by name. Alternative: a smoke-only pull request first that adds only section 3.6 and dispatches an existing scenario to prove the apply; it proves the apply, not the consumer path. This needs Hari: the rule says the plan is not approvable until the run exists, and point 4 of the standing decision excludes Azure dispatch.
- Q5. Wait exhaustion. Recommended: adopt on this issue the contract of [#506 decision 6899696796](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6899696796) point 3, as the verifier plan sets it for this pull request: when the wait for either member runs out, `consumer-verify` is not invoked and the repeat is recorded `consumer_wait_exhausted` with the wait exit codes (section 3.2 rule 5). The decision is on #506, not adopted here.
- Q6. Shared process namespace. Recommended: `shareProcessNamespace: true` on both member pods, so the subject's SIGKILL to itself works (section 3.4 rule 3a, DA5). The only other process in the namespace is the pod sandbox; no container gains `SYS_PTRACE`. Alternatives: a launcher process in the image, which changes `src/**` and is outside this pull request; or no change, under which C10 and C01 cannot run (plan check finding 1).

## 14a. Open questions for Hari

Both arise from #528, which merged after revision 2. Both concern identity or scope beyond the approved split, so they are Hari's under point 4 of the standing decision.

- Q7. Workflow read of `consumer-record`. #528 gives the workflow principal container-scoped Reader on exactly three containers, and its Terraform test asserts that set. The readiness poll Hari adopted (Q2, DA1) reads `consumer-record`. Recommended: add `consumer_record` to that set in this pull request (section 3.6 rule 4), the same scope and role #528 uses. Alternative: no grant, relying on an inherited role that the base no longer evidences; then DA1 is likely false and no repeat runs.
- Q8. Archive support for consumer scenarios (section 12 item 4). Recommended: a new split entry, after this one and before the first consumer verdict (#511), that admits C01, C03 and C10 to the archive and adds the consumer record to its inventory. Alternative: widen this pull request to `archive.py`, which joins the archive's own contract to this one and needs the archive design's owner. Until either merges, every consumer dispatch records an archive failure in its `report` summary, with no effect on the job result or scientific outputs, and no consumer verdict can be published.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, or the scope in section 2, including any change to a `src/**` file to make a render or option check pass.

## 16. Changes since revision 1

Each applies the [revision 1 plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6914958306).
- Finding 1: section 3.4 rules 3 and 3a add `shareProcessNamespace: true`, with the PID 1 measurement; DA5 rewritten; new Q6; T17 checks the field.
- Finding 2: Q3's bound recomputed from command timeouts (5042 s per repeat); new section 3.2 rule 6a, a phase budget with a per-repeat reserve; R10a, T16a.
- Finding 3: section 3.2 rules 1 and 2 rewritten so every repeat has exactly six records, and S2 does not run when S1 fails; T9 has literal values for both cases.
- Finding 4: Q2's recommendation now keeps the verifier plan's order: a repeat not ready in 300 s runs no start line (rule 3, R10, T10). Q4 now states that it needs Hari.
- Finding 5: DA5 corrected; the PID 1 assumption is now DA5's subject.
- P2 findings: member labels (rule 3, T17); the four Terraform test updates (section 6); collect before delete on `unknown` (rule 4, R12, T12); rule 6 limited to consumer scenarios; run ID citation corrected to line 460.
- Also: base commit moved to `46a2c3d11c4e36b9a4c7806a18e9b72ad4e70595`; S6 and S7 wait from each member's apply time (rule 4, R12a, T16b); S3's commands run under the readiness deadline (section 3.3 rule 6).

## 17. Changes since revision 2

Each applies [decision 6915070211](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6915070211) or [decision 6925863575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6925863575).
- Authority: section 1 lists both decisions.
- Q1, Q2, Q5 and Q6: adopted as recommended; the plan text already followed each, so no contract changes.
- Q3: `CONSUMER_PHASE_SECONDS` is 7200, not 14400 (section 3.2 rule 6a). New rule 6b states the bound that follows: no repeat starts after 2100 s, and repeat 5 runs only if repeats 1 to 4 average at most 525 s. R10a and T16a take the new values. The other bounds are unchanged.
- Q4: section 12 item 1 records that #508's smoke run counts as this design's live smoke.
- DA1 to DA5: section 7 records their acceptance.
- Section 14 becomes the record of decided questions; new section 14a, "Open questions for Hari", states None.

## 18. Changes for the new base

Applying #528, merged after revision 2:
- Base commit moved to `911c5a1eaaac55107271755f2aa9697ea7a8acf9`; every changed line citation updated (`phase.py`, `test_workflow.py`, `main.tftest.hcl`, `values.yaml`, `values.schema.json`, `VerifierHostOptionsValidator.cs`).
- Section 3.1 rule 5 and T3: the new archive-manifest upload step's name gains the consumer branches.
- Section 3.5 rule 2: the verify environment now includes `Verifier__ConsumedMetadata__*`.
- Section 3.6 rule 4, section 5, R17, section 6: the workflow principal's Reader set gains `consumer_record` (Q7).
- DA1: evidence rewritten for the explicit grant.
- Section 12 item 4 and Q8: archive support for consumer scenarios.
