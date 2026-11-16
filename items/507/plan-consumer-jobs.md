# Consumer Jobs and admission of C03, C10 and C01: contract-grade plan

Revision: 1

This plan adds the two consumer member Jobs to the scenario chart and orders them, per repeat, against the start line, the load generator and the verifier. It admits C03, C10 and C01 to `.github/workflows/scenario.yml` as two-phase scenarios on the existing B04 route, and adds the `consumer-record` container and its two role assignments that ADR-022 requires. Nothing in this pull request can run live: the consumer worker, the verifier's consumer mode and the load generator all still lack their C03, C10 and C01 inputs, which issue #508 adds, so this pull request is checked offline and its live smoke is #508's.

Issue: #507 (split entry 4 of #499). Short name: `consumer-jobs`.
Base commit: `e8da5826deba9b42bbf5224c835ad728768544ef` (`main`, 2026-11-16).
Implementing tool: Codex, in a session Hari starts ([decision 6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673) point 2). The Claude review gate of [decision 6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233) point 2 applies.
Protected paths touched: `.github/workflows/scenario.yml` (AGENTS.md hard safety rule 8), `docs/operations/**`, `infra/ephemeral/**` (identity). Hari merges.

## 1. Goal and authority

Goal: when #508 supplies the C03 scenario, plan sources and load generator admission, one dispatch of `scenario.yml` with `scenario=C03` runs phases C0 and C1, five repeats each, with a subject and a witness consumer member per repeat in the order ADR-022 sets, and produces for each repeat the artifacts that `consumer-verify` reads. C10 and C01 take the same route once #510 supplies their data.

Authority, in creation order:
1. The issue body of #507, which names split entry 4 of [proposal revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894560135) and links decisions 6894534441, 6894564479, 6894587233 and 6894614673 on #499.
2. [Decision 6895300348](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6895300348), adopting #499 round 1 ([6895299617](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617)): group shape, start order, invalid runs.
3. [Decision 6895329189](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6895329189), adopting #499 round 2 ([6895328446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446)): point 4 puts the `consumer-record` container and its roles in this entry; point 5 puts both members on the `workload` node; point 7 fixes verifier order.
4. [Decision 6895537201](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6895537201), adopting the [standing decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291): grilling questions go to "Open questions for Hari" with recommended answers; plan approval on a clean check; point 4 lists what it does not cover.

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
- Phase judgement of the `consumer` block: #521. Until it merges, the `judge` step fails for C01, C03 and C10 (`src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` line 32 does not exempt `judge`).
- Subject self-termination: #509.
- The hosted findings on configured consumer row limits: #529.

## 3. External contracts

Everything in this section is read by a later child, by the merged workers or by Hari. The implementer copies it exactly.

### 3.1 Dispatch and routing

1. The `scenario` input's `options` list becomes, in this order: `A01`, `B04`, `B10`, `C03`, `C10`, `C01`, `probe-batch`, `probe-batch-2`, `idle-observation`, `ledger-batch`. Its default stays `A01`. No other input changes. Triggers stay `workflow_dispatch` only.
2. C03, C10 and C01 take B04's route ([b04-workflow.md](../../docs/operations/b04-workflow.md) paragraph 3): job `probe` runs phase C0, job `full-C0` is skipped, job `full-C3` runs phase C1. The C0 candidate does not gate C1.
3. `PARCHWELL_PHASE` in job `probe` becomes `${{ (inputs.scenario == 'B04' || inputs.scenario == 'B10' || inputs.scenario == 'C03' || inputs.scenario == 'C10' || inputs.scenario == 'C01') && 'C0' || 'probe' }}`; in `full-C3`, the same condition with `'C1' || 'C3'`.
4. The `full-C0` job's `if` adds `&& inputs.scenario != 'C03' && inputs.scenario != 'C10' && inputs.scenario != 'C01'` immediately after `inputs.scenario != 'B10'`, inside the same parentheses.
5. Artifact names. The `name:` expressions of the four upload steps gain the branches `inputs.scenario == 'C03' && 'c03-C0' || inputs.scenario == 'C10' && 'c10-C0' || inputs.scenario == 'C01' && 'c01-C0'` (job `probe`) and the same with `C1` (job `full-C3`), placed after the B10 branch and before the A01 fallback. Resulting names: `c03-C0-candidate-<run_id>-<run_attempt>`, `c03-C0-pods-<run_id>-<run_attempt>`, and likewise for C1, C10 and C01.
6. Images. Job `probe` gains the output `enrichment_digest: ${{ steps.images.outputs.enrichment_digest }}`. In `full-C3`, the `allowed` step's `case` gains the arm `C03|C10|C01) bash infra/scripts/phase/phase.sh allow-images ;;` and its `env` gains `ENRICHMENT_DIGEST: ${{ needs.probe.outputs.enrichment_digest }}`; the `images` step's `env` gains the same line. The `use-images` command keeps its two positional digests.
7. `phase.py` constants: `SCENARIOS` gains `"C03": {"C0": "C0", "C1": "C1"}`, `"C10": {"C0": "C0", "C1": "C1"}` and `"C01": {"C0": "C0", "C1": "C1"}`; a new `CONSUMER_SCENARIOS = ("C01", "C03", "C10")`. Run IDs use the existing prefix rule (`phase.py` line 466): `c03-<run_id>-<run_attempt>`, `c10-...`, `c01-...`.
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
1. S1 and S2 append a record each to `phase-status.json` `modes` with the existing fields (`phase.py` lines 684 to 686) and `wait`, `collect` null. S6 and S7 update those same two records; no third record is written for a member.
2. If S1 or S2 does not produce `apply == 0`, the repeat skips S3 to S8. Each skipped step appends a record with `outcome: "not_attempted"` and `reason: "consumer_apply_failed"`. The session then deletes both member Jobs and confirms no pod remains, using the existing delete-and-confirm commands (`phase.py` lines 751 to 765); any failure there sets `safety_stop`.
3. S4 and S5 run whatever S3's outcome is. Readiness is orchestration, not judgement: the verifier judges assignment at the start line (`consumer_start_assignment_invalid`).
4. S6 and S7 use the existing `wait-job` command with the member Job's rendered deadline. Exit 0 (Complete) and exit 1 (Failed) are both terminal: the record's `outcome` becomes `complete` or `failed`, then `collect-job` runs. Any other exit leaves the Job not terminal: `outcome` becomes `unknown`, and the existing delete-and-confirm path runs for that Job.
5. When either S6 or S7 ends with `outcome: "unknown"`, S8 appends a record with `outcome: "not_attempted"` and `reason: "consumer_wait_exhausted"`, and `consumer-verify` is not invoked (verifier plan line 333; [#506 decision 6899696796](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6899696796) point 3; see Q5). The member records keep their `wait` exit codes as the wait diagnostics.
6. `safety_stop` keeps its existing meaning: once set, every later step of every repeat appends its record with `reason: "safety_stop"` and does nothing.
7. `run-repeats` exits 1 when any record has an outcome in `BAD_OUTCOMES`, as today (`phase.py` line 785). A member record with `outcome: "failed"` is not in `BAD_OUTCOMES`; a killed subject ends Failed by design.
8. The existing `judge`, `candidate`, upload, `delete-workload` and destroy steps run unchanged after `run-repeats`.

### 3.3 Readiness poll (S3)

1. Constants in `consumer_repeat.py`: `READINESS_CAP_SECONDS = 300` (policy, Q2), `READINESS_INTERVAL_SECONDS = 5`, `PARTITIONS = 32` (`infra/ephemeral/eventhubs.tf` line 15, `document-events` `partition_count`), `RECORD_BLOB_MAXIMUM = 2621440000` (65536 times 40000, the worker's row cap times rows per blob, `src/Parchwell.Verifier/Common/Consumer/ConsumerRecordLimits.cs` lines 6 to 8).
2. Each poll lists container `consumer-record` with prefix `R + "consumer/"` using `az storage blob list --account-name <account> --container-name consumer-record --prefix <prefix> --auth-mode login --num-results 1000 --only-show-errors -o json`, then downloads each blob whose name matches `^<prefix>[^/]+/record/[0-9]{6}\.jsonl$` with the existing `download_blob` helper (`phase.py` lines 1438 to 1465), `container="consumer-record"`, `maximum=RECORD_BLOB_MAXIMUM`, `minimum=0`.
3. The poll is ready when all of these hold: exactly two distinct member segments appear under the prefix, each with a `registration.json`; in each member's record, read in blob order and line order, the last row whose `kind` is `partition_event` has `event` equal to `assigned`; the two `partitions` lists of those rows are disjoint; their union is exactly the integers 0 to 31.
4. A record line that is not a JSON object, or a list or download failure, makes that poll not ready. It does not end the poll early.
5. The record for S3 has `mode: "consumer-readiness"`, `repeat`, `outcome` (`ready` or `not_observed`), `polls` (count), `elapsed_seconds` (integer, rounded down) and `last_condition`, one of `ready`, `list_failed`, `download_failed`, `row_invalid`, `member_count`, `not_assigned`, `overlap`, `coverage`, checked in that order. It never carries blob contents. `not_observed` is not in `BAD_OUTCOMES`.
6. The poll stops at the first ready poll or when `READINESS_CAP_SECONDS` has passed since S2's apply returned.

### 3.4 Chart: consumer member Jobs

1. New runtime modes `consumer-subject` and `consumer-witness`. They are accepted only when `scenario_id` is `C01`, `C03` or `C10`, `configuration_id` is `C0` or `C1`, and `runtime.phase` equals `configuration_id`. Every other combination fails the schema.
2. Job name: `<run_id>-<lower phase>-r<repeat>-consumer-subject` or `...-consumer-witness`, matching the worker's rule (`src/Parchwell.Enrichment/ConsumerProcessing/ConsumerProcessingOptions.cs` lines 26 and 29). With a 29-character run ID the longest name is 52 characters.
3. Job spec: `completions: 1`, `parallelism: 1`, `backoffLimit: 0`, `podReplacementPolicy: Failed`, `activeDeadlineSeconds` equal to `bounds.execution.consumer_job_seconds`, which is `1800` (Q3). Pod: `restartPolicy: Never`, `terminationGracePeriodSeconds: 30`, `serviceAccountName` from `runtime.service_accounts["enrichment-consumer"].name`, `automountServiceAccountToken: false`, label `azure.workload.identity/use: "true"`, `parchwell.io/role: "enrichment-consumer"`, `nodeSelector: {workload: "workload"}`, no tolerations, the same pod and container security settings as the existing repeat Jobs (`jobs.yaml` lines 66 to 86).
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
7. Runtime inputs: for `scenario_id` in `C01`, `C03`, `C10`, every mode requires `runtime.enrichment_image` (existing pattern `^[^/ ]+/parchwell/enrichment@sha256:[a-f0-9]{64}$`, `values.schema.json` lines 591 to 594) and `runtime.service_accounts` with exactly `loadgen`, `verifier` and `enrichment-consumer`. The chart then renders those three service accounts. For every other scenario, inputs and rendered service accounts are unchanged.
8. `phase.py` `values()` supplies, for a consumer scenario, `enrichment_image` from `phase["images"]["enrichment"]["reference"]` and the three service accounts, for every mode including `judge`.

### 3.5 Chart: verifier modes for consumer scenarios

1. Mode `start-line` with `scenario_id` in `C01`, `C03`, `C10` renders the existing start-line environment plus `Verifier__ConsumerRecordContainer` = `consumer-record` (`VerifierHostOptionsValidator.cs` lines 68 and 69).
2. Mode `verify` with `scenario_id` in `C01`, `C03`, `C10` renders the existing verify environment with two differences: `Verifier__Mode` = `consumer-verify` (`VerifierHostOptions.cs` line 167), and `Verifier__ConsumerRecordContainer` = `consumer-record`. The Job name stays `<run_id>-<lower phase>-r<repeat>-verify`, as the verifier requires (`VerifierHostOptionsValidator.cs` lines 404 to 409).
3. Modes `loadgen` and `judge` render unchanged for these scenarios. Modes `loadgen2`, `loadgen-restart`, `loadgen-successor`, `b10-abrupt-verify`, `b10-sweep-line`, `b10-sweep`, `b10-final-sweep` and every single-action, idle and probe-v2 mode fail the schema for these scenarios.
4. The `execution` section of `bounds` gains `consumer_job_seconds: 1800`; every other bound is unchanged.

### 3.6 Terraform

1. `infra/ephemeral/storage.tf` `containers` gains `consumer_record = { name = "consumer-record", public_access = "None", role_assignments = {} }`, placed after `single_action`.
2. `infra/ephemeral/identity.tf` `baseline_workload_roles` gains exactly two entries:
   - `enrichment_consumer_record_contributor = { workload = "enrichment-consumer", role_definition_name = "Storage Blob Data Contributor", scope = module.storage.containers.consumer_record.id }`
   - `verifier_consumer_record_reader = { workload = "verifier", role_definition_name = "Storage Blob Data Reader", scope = module.storage.containers.consumer_record.id }`
   Source: ADR-022 "Durability" (docs/blueprint.md line 754).
3. `infra/ephemeral/outputs.tf` output `blob.containers` gains `consumer_record = module.storage.containers.consumer_record.name`.
4. No role is added for the workflow principal or any other identity.

### 3.7 Contracts this plan sets for #508 and #510

These are bounds the scenario children must fit. They are not dependencies of this pull request's tests.
1. The consumer window: `WindowMs` must not exceed 1620000 (1800 s Job deadline, minus the worker's 120 s watchdog margin after the window, `ConsumerProcessingWorker.cs` lines 49 to 53, minus 60 s for startup), and must cover readiness (up to 300 s), the start-line Job and the load generator Job.
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
- R10. WHEN the readiness poll sees no ready poll within 300 s, THEN its record has `outcome: "not_observed"` with the last condition, and S4 to S8 still run.
- R11. WHEN a member Job ends Failed (exit 1 of `wait-job`), THEN its record has `outcome: "failed"` and S8 runs.
- R12. WHEN a member Job's wait exits other than 0 or 1, THEN that record has `outcome: "unknown"`, the delete-and-confirm path runs for it, and S8 is recorded `not_attempted` with `reason: "consumer_wait_exhausted"`.
- R13. WHEN the chart renders `consumer-subject` or `consumer-witness`, THEN it renders exactly one Job with the spec of section 3.4 and exactly the 17 environment names of its table, and three service accounts.
- R14. WHEN the chart renders `start-line` or `verify` for a consumer scenario, THEN it renders section 3.5 rules 1 and 2; WHEN it renders them for A01, B04 or B10, THEN the environment contains neither `Verifier__ConsumerRecordContainer` nor `Verifier__Mode=consumer-verify`.
- R15. WHEN a consumer mode is rendered with a scenario other than C01, C03 or C10, a configuration other than C0 or C1, a phase not equal to the configuration, a missing `enrichment_image`, or `service_accounts` without `enrichment-consumer`, THEN the render fails and produces no Job.
- R16. WHEN a mode listed in section 3.5 rule 3 as failing is rendered for C01, C03 or C10, THEN the render fails.
- R17. WHEN Terraform plans the ephemeral root with default variables, THEN the storage module has five containers (`ledger`, `verifier`, `claim-check`, `single-action`, `consumer-record`), `azurerm_role_assignment.workload` has 14 instances, and the two new assignments have the principals, roles and scopes of section 3.6.
- R18. WHEN `probe_namespace_data_roles` is true, THEN the role set is the default 14 plus the existing two namespace roles.

## 5. Invariants and safety rules

- Triggers of `scenario.yml` stay `workflow_dispatch` only (hard safety rules 8 and 9). The installed workflow and `docs/operations/workflows/scenario.yml.txt` are byte-identical.
- At most one ephemeral environment: the shared `parchwell-ephemeral` concurrency group is unchanged.
- Each job still destroys its ephemeral layer under the existing `if: always()` conditions; this pull request adds no step after `destroy` and removes none.
- No production .NET code changes; the workflow alone creates, waits for and deletes Jobs (ADR-020).
- Identity: only the two role assignments of section 3.6 are added. No existing assignment changes scope or role. The verifier gains read only; the enrichment-consumer identity gains Contributor on one new container.
- The readiness poll reads only the `consumer-record` container, only under the repeat's prefix, and writes nothing to Blob.
- No member Job ever has two pods: `backoffLimit: 0`, `podReplacementPolicy: Failed`, `restartPolicy: Never`.
- Every existing positive chart case and every existing phase and workflow test keeps its assertions.

## 6. Test map

Standing fixture rule. When an existing test's fixture is rejected by a new check in this plan, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. Basis: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2. Contract updates are separate from this rule. The existing assertions whose expected value is text this plan changes keep their assertion and take exactly the new text of section 3.1 or 3.6: the `scenario` options list (`infra/scripts/phase/tests/test_workflow.py` lines 54, 71 and 470), `sorted(phase.SCENARIOS)` (line 73), the `full-C0` condition (line 80), `PARCHWELL_PHASE` (lines 85 and 87), artifact names (lines 256 and 258) and the `allowed` case text (line 277). The Terraform role-set assertion (`tests/terraform/ephemeral/main.tftest.hcl` line 466) gains the two new tuples and its count becomes 14; the container assertion (line 383) gains `consumer_record`.

| Test | File | Inputs | Expected |
|---|---|---|---|
| T1 | test_workflow.py | parsed `scenario.yml` | options list exactly section 3.1 rule 1; `on` keys exactly `workflow_dispatch`; installed file bytes equal the committed copy |
| T2 | test_workflow.py | the `probe`, `full-C0`, `full-C3` expressions evaluated for each of A01, B04, B10, C03, C10, C01 | C03, C10, C01: phases C0 and C1, `full-C0` false; A01, B04, B10: values as on the base commit |
| T3 | test_workflow.py | upload `name` expressions for C03 in `probe` and `full-C3` | `c03-C0` and `c03-C1`; C10 and C01 likewise |
| T4 | test_workflow.py | `full-C3` `allowed` and `images` steps | `case` arm `C03|C10|C01)` runs `allow-images`; both steps carry `ENRICHMENT_DIGEST: ${{ needs.probe.outputs.enrichment_digest }}`; `probe.outputs.enrichment_digest` exists |
| T5 | test_phase.py | `validate-scenario` with C03, C10, C01, C02, c03 | 0, 0, 0, 2, 2 |
| T6 | test_phase.py | `prepare C0`, `prepare C1`, `prepare C3` for C03 | run ID `c03-<id>-<attempt>`; phase C0, C1; exit 2 `unsupported phase` |
| T7 | test_phase.py | `allow-images` for C03 with: three valid digests; enrichment empty; enrichment `sha256:` plus 63 hex; B04 with enrichment empty | `workload=true`, `workload=false`, `workload=false`, `workload=true` |
| T8 | test_consumer_repeat.py | fake runner where every command exits 0 and the third poll is ready | per repeat, command order S1 to S8; `modes` holds 6 records per repeat (two member records, readiness, start-line, loadgen, verify), 30 in all; readiness `polls: 3`, `outcome: ready` |
| T9 | test_consumer_repeat.py | subject apply exits 1 in repeat 2 | repeat 2 records S3 to S8 `not_attempted`, `reason: consumer_apply_failed`; both member Jobs deleted and confirmed; repeat 3 runs S1 to S8 |
| T10 | test_consumer_repeat.py | readiness never ready; fake clock | after 300 s, `outcome: not_observed`; start-line, loadgen and verify still run; at 299 s still polling |
| T11 | test_consumer_repeat.py | subject `wait-job` exits 1, witness 0 | subject `outcome: failed`, witness `complete`; verify runs |
| T12 | test_consumer_repeat.py | witness `wait-job` exits 124 | witness `outcome: unknown`; delete and confirm run for it; verify `not_attempted`, `reason: consumer_wait_exhausted`; `run-repeats` exits 1 |
| T13 | test_consumer_repeat.py | readiness rows: two members, assigned {0..15} and {16..31} | ready |
| T14 | test_consumer_repeat.py | the T13 rows with, one change each: one member; three members; assigned {0..16} and {16..31}; {0..15} and {17..31}; one member's last `partition_event` is `revoked` after an earlier `assigned`; one record line `[]` appended | not ready, with `last_condition` `member_count`, `member_count`, `overlap`, `coverage`, `not_assigned`, `row_invalid` |
| T15 | test_consumer_repeat.py | list command exits 1 | that poll not ready with `list_failed`; polling continues |
| T16 | test_consumer_repeat.py | `safety_stop` set before repeat 4 | repeats 4 and 5 record every step with `reason: safety_stop` and run no command |
| T17 | test_consumer_chart.py | `consumer-subject` and `consumer-witness` for C03 C0, C10 C1, C01 C0, repeat 1 and 5, 29-character run ID | one Job; name per section 3.4 rule 2 (longest 52 characters); spec values of rule 3; env names exactly the 17 of rule 5, values as tabled; `args: ["--consumer-processing"]`; no `command`; three service accounts; kubeconform strict passes |
| T18 | test_consumer_chart.py | `start-line` and `verify` for C03 C0 | rule 1 and 2 of section 3.5; verify Job name ends `-r1-verify` |
| T19 | test_consumer_chart.py | negatives: consumer mode with A01, B04; configuration C2; phase C0 with configuration C1; no `enrichment_image`; `service_accounts` without `enrichment-consumer`; `loadgen2` and `b10-sweep` for C03 | each render fails and produces no Job |
| T20 | test_chart.py (existing) | existing positive and negative matrix | unchanged results; for A01, B04, B10 start-line and verify, no `Verifier__ConsumerRecordContainer` name |
| T21 | test_consumer_chart.py | worker option check: env names of T17 against the properties of `ConsumerProcessingOptions.cs` | every property has its `ConsumerProcessing__` name; no rendered name lacks a property |
| T22 | main.tftest.hcl | default plan | R17 |
| T23 | main.tftest.hcl | `probe_namespace_data_roles = true` | R18 |

`check.sh` runs `test_consumer_chart.py` after the existing chart tests.

## 7. Deployment assumptions

Each is a blocking finding until a decision or approval by Hari accepts it by name (docs/agents/factory.md "Item lifecycle" step 1). None has a test in this pull request; #508's smoke run is the first live evidence.

- DA1. The workflow principal can list and read blobs in `consumer-record` with `--auth-mode login`. Evidence: the same principal downloads `candidate.json` from the `verifier` container in every live phase (`phase.py` lines 1626 to 1680; for example the B06 and B07 batch runs), although no Terraform in this repository grants it a data role there, so it holds an inherited role from outside this repository (inference). Taken on fresh connections, idle (one read after the judge). Not shown for `consumer-record`. If it fails, every readiness poll ends `not_observed` after 300 s and the run still proceeds (section 3.2 rule 3), so the cost is time, not a wrong count.
- DA2. Two member pods (requests 500m CPU, 1 GiB each) and the start-line verifier pod (1 CPU, 2 GiB) schedule together on the one `Standard_D4ds_v5` `workload` node (`infra/cluster/aks.tf` line 44). Evidence: none taken; allocatable capacity after system pods was not read.
- DA3. The workload identity webhook injects `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` and `AZURE_FEDERATED_TOKEN_FILE` into the member pods. Evidence: the F04 seed Job runs the same image and identity live (single-action batch runs), on a fresh pod, idle.
- DA4. The worker plan's D1 and the verifier plan's DA2: the role assignments of section 3.6 give the enrichment-consumer identity append and create rights and the verifier read and list rights on `consumer-record` with `WorkloadIdentityCredential`. Evidence: the analogous `ledger` grants work on fresh pods, under load (B06 and B07 runs); not shown for this container.
- DA5. Kubernetes reports a SIGKILLed member's Job as Failed, not as a replaced pod: with `backoffLimit: 0` one pod failure exceeds the limit (Kubernetes Job documentation, "Terminal Job conditions"). Evidence: documentation only; the B10 self-kill path uses the same Job settings live.

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
- Kubernetes downward API, https://kubernetes.io/docs/concepts/workloads/pods/downward-api/, "Information available via fieldRef", read 2026-11-16 through the same tool: `spec.nodeName` is available as an environment variable.
- docs/blueprint.md ADR-020 and ADR-022 at the base commit.
- The merged worker and verifier code at the base commit, cited inline.
- Plan rule 1 (external behaviour): this plan relies on no Kafka, librdkafka, Confluent.Kafka or Event Hubs behaviour beyond what ADR-022 and the merged code already state. The partition count 32 is this repository's configuration, not a service default.

## 11. Plan-check questions

(a) Effort scaling with a number unknown at planning time: none in the implementation. Test counts are fixed by section 6. The readiness poll is capped at 300 s and 61 polls per repeat at run time, not in this pull request.
(b) Stricter than the parent: the readiness poll is an addition the parent does not name; it gates nothing (section 3.2 rule 3), so it is not stricter in effect. Resource requests and the 1800 s deadline are new policy values (Q3).
(c) Checks depending on code, data or behaviour neither on `main` nor in this pull request: none of this pull request's tests do. Live behaviour depends on #508 (plan sources, scenario data, load generator admission) and #521 (judge); those are listed in section 12, not tested here. DA1 to DA5 are listed in section 7.

## 12. Deferred work

1. Live smoke of this design. Evidence: no consumer scenario can run before #508 (worker `Program.cs` lines 153 to 155 exit `consumer_plan_invalid`; load generator rejects consumer pairs at `src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs` lines 94 to 97). On the verdict path: yes. Why deferral is permitted: the approved split puts the smoke run in entry 5 (#508), and see Q4. Responsible: #508. Stage: after #508 and #521 merge. Completion evidence: one C03 smoke dispatch whose repeat records show S1 to S8 and a `consumer-verify` result. Gate: #508's merge and the C03 batch run.
2. Phase judgement for consumer scenarios: #521, gating #508's smoke run.
3. Plan sources, scenario data and load generator admission: #508 for C03, #510 for C10 and C01.

## 13. Size estimate

Estimate, 2026-11-16, by analogy with the B04 admission and the single-action chart additions, not measured: about 1300 changed lines in about 20 files (workflow and copy 40; phase.py and consumer_repeat.py 350; Python tests 450; chart templates and schema 250; chart tests 200; Terraform and its test 30; documentation 80). Size is reported, not enforced.

## 14. Open questions for Hari

Each carries a recommended answer under point 2 of the standing decision. The plan as written follows each recommendation.

- Q1. Route. Recommended: B04's two-job route, one dispatch option per item (section 3.1). Alternative: one `consumer-batch` option running all three items in one job, as `ledger-batch` does; that needs a new batch module and makes one long job of up to 30 repeats, beyond the 360-minute job timeout (estimate). Effect on the verdict route: none beyond the route.
- Q2. Readiness. Recommended: the workflow polls the member records for at most 300 s (policy value, not measured), and proceeds to the start line whatever the outcome, so the verifier stays the only judge of assignment (sections 3.2 and 3.3, DA1). Alternatives: (a) a fixed wait after both pods run, with no Blob read and no DA1, at the risk of more `consumer_start_assignment_invalid` repeats; (b) skip a repeat that is not ready, which saves a load generator run but leaves that repeat with no verifier result.
- Q3. Member Job bounds. Recommended: `activeDeadlineSeconds` 1800, requests 500m CPU and 1 GiB, limits 1500m CPU and 4 GiB (policy values, not measured). Basis: a five-repeat phase must fit the 360-minute job timeout; with an 1800 s member deadline the worst case per repeat is about 3500 s and per phase about 290 minutes before infrastructure steps (estimate, 2026-11-16, from the configured deadlines in `values.yaml` lines 95 to 100 plus the existing 90 s wait and 210 s collect allowances). Requests leave room for the start-line pod on the one 4-vCPU node (DA2). CPU limits bound processing speed, which C03 measures against a simulated delay; the limit is above the request so the simulated delay, not throttling, should dominate (inference).
- Q4. Live smoke before approval. docs/agents/factory.md "Item lifecycle" step 1 says a plan that changes live infrastructure is not approvable until a live smoke run of that design exists. This plan adds a container and two role assignments, and nothing in it can run live before #508. Recommended: treat #508's smoke run as the live smoke of this design, as the approved split already places it, and accept DA1 to DA5 by name. Alternative: a smoke-only pull request first that adds only section 3.6 and dispatches an existing scenario to prove the apply; it proves the apply, not the consumer path. This question may be outside the standing decision's adoption, because it concerns a factory rule; the control session decides whether it goes to Hari.
- Q5. Wait exhaustion. Recommended: adopt on this issue the contract of [#506 decision 6899696796](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6899696796) point 3, as the verifier plan sets it for this pull request: when the wait for both members runs out, `consumer-verify` is not invoked and the repeat is recorded `consumer_wait_exhausted` with the wait exit codes (section 3.2 rule 5). The decision is on #506, not adopted here.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, or the scope in section 2, including any change to a `src/**` file to make a render or option check pass.
