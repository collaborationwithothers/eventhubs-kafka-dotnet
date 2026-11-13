# B10 scientific three-Job path contract-grade plan

Revision 2. Issue 404, pull request short name `B10 scientific three-Job path`. Status: not approved. Implementing tool: open (section 11, Q1). Hari merges, because the change touches `Parchwell.Verifier*/` (`.github/CODEOWNERS`).

This pull request moves the scientific B10 C1 path that `scenario.yml` runs from two producer Jobs per repeat to ADR-021's three sequential C1 Jobs, for all five repeats. Each scientific C1 repeat then publishes one per-repeat nonfinal object that the later final-judgement pull request reads. It authorizes no dispatch, verdict, result or expected outcome, and it leaves the phase sweep and the five-repeat judgement to that later pull request.

## 0. Changes

Since Revision 1 (`009613ab`), after plan check [6846109878](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846109878):
- B1: a first publication is accepted only with `successor_permitted` true (`b10_abrupt.py` line 717 at `675f8372`). Section 3.2 rows 4 and 5 now say so; T9 now tests a rejected publication: no successor and no verify Job.
- B2: section 3.2 now pins the between-repeat rules: which outcomes end the repeat and which end the phase, where the successor publication check runs inside `run-repeats`, and what a failed pod read does.
- B3: the derived purpose is `scientific` for pattern S and `diagnostic_trial` for every other run ID (3.4), so existing tests with run IDs such as `synthetic`, `run-1` and `offline-image` keep their behaviour.
- B4: check 30 treats an earlier repeat with no registration as valid. Problem 10 is recorded only for a failed or incomplete listing, an unreadable or invalid registration, or a repeated instance ID (3.6). T16 split.
- B5: section 7 lists the trial-only assumptions this path needs again, by name, with their sources.
- P2-A: 3.3 now keeps the trial's failure mapping. P2-B: T5 pins the phase literal. P2-C: T6 and T7 gain the two chart cases. P2-D: 2.4 inventory corrected. P2-E: LA-S3 states how its evidence was taken and the sign-in exposure. P2-F: citation corrected.
- Section 9(b) lists the phase gap of Q7 as stricter than the parent.

Revision 1 was the first revision.

## 1. Goal and authority

Goal: after this pull request merges, a scientific B10 C1 phase run by `scenario.yml` runs, for each repeat N from 1 to 5, the start line, the ordinary C1 timeout Job, the first restart Job and, when ADR-021 protocol rule 10 permits, the successor Job and the per-repeat verifier, and publishes each repeat's nonfinal object in the format of section 3.6. That object is the input the final-judgement pull request reads.

Authority, in creation order, under docs/agents/factory.md "Item lifecycle" step 3:
- The issue body and the decisions listed in `items/404/state.md` on `main` at `675f8372`, which covers comment 6830544567. They include ADR-021 on `main`.
- [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) Q17 option B, as the restart redesign plan states it (`items/404/plan-b10-restart-redesign.md` at `b471d02d`, line 411): "Which pull request moves the legacy scenario path (`scenario.yml`, `B10TwoInstanceVerify`, `B10ExpectedProducerJobs`) from two to three C1 producer Jobs", option B "a separate pull request after the one-repeat trial and before the five-repeat governed run".
- [6774480762](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6774480762): the C1 timeout Job runs the ordinary LoadGen host unchanged as `{run}-c1-r1-loadgen`; the restart and successor Job names.
- [6787982173](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787982173) point 1: science keeps run-ID prefix `b10`; the trial uses `b10d`.
- [6816117386](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816117386) point 2: the nonfinal object may carry diagnostic extras; the later final sweep projection drops them.
- [6837706862](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837706862), [6837940546](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837940546): the two trials after PR3c. Evidence readings [6837900667](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837900667) and [6838450660](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6838450660) are evidence, not decisions.
- [6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192), [6844950677](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844950677), [6845385609](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845385609): the verifier sign-in fix, a parallel pull request. This plan changes none of its files' token code (section 2.2).
- [6845612916](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845612916): this plan, item (a), with points 2 to 4. Applied, not restated: point 1 (a) in sections 1 to 3; point 2 in sections 2, 9 and 11 and in section 3.6's external contract with item (b); point 3 in the plan-branch commit; point 4 in this file's status line and section 11.

Catch-up: 321 comments on 4 pages at this revision's refresh. Added since Revision 1: 6845836752 (this plan's pointer), 6845914263 (item (b)'s pointer) and 6846109878 (this plan's check), each read in full; and 6846147228 (item (b)'s plan check), whose lines naming item (a) or C0 were read, selected by script, not the whole body. New since the state file, each read in full: 6837706862, 6837900667, 6837940546, 6837975192, 6838148214, 6838298262, 6838309006, 6838450660, 6844950677, 6844977688, 6845326893, 6845335358, 6845385609, 6845555307 and 6845612916. Issue `updated_at` 2026-11-13T06:08:15Z.

Reading limit, stated so the check can weigh it: the prompt that started this session asked for every `## Decision` and `## Approval` comment on the issue to be read in full. There are 170, 335783 bytes (script). This session did not read all of them; its context could not hold them. It read in full the 15 new comments above, the decisions selected by script for terms this plan depends on (`Q17`, `scenario.yml`, `legacy`, `loadgen2`, three Jobs or names, `science`, `scientific_admissible`, five repeats, sweep pull request): 6761783572, 6767506812, 6769057785, 6774480762, 6775474492, 6775679271, 6787849492, 6787982173, 6816117386, 6816733258, 6824437250, 6826128120, 6710336610, 6746177383, 6749995101 and 6750021224; and, for the rest, only the one-line entries of `items/404/state.md`. The item plan, Revision 5 ([6705594392](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705594392)), was searched by script, not read in full.

Parent items 347 and 348 were not re-read in this session.

## 2. Scope

The scope is decision 6845612916 point 1 (a). The final sweep projection, the phase sweep line for C1, the five-repeat judgement, the candidate for C1 and every verdict are item (b)'s ([6845612916](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845612916) point 1 (b)). C0 is open (Q2). This plan assumes Q2 option B (C0 unchanged) and states what changes under option A.

The plan freezes files and contracts, not lines (6845612916 point 2). Line numbers are references, not boundaries.

### 2.1 What exists on `main` at `675f8372`

- `scenario.yml` names no producer mode. B10 C0 runs in job `probe` with `PARCHWELL_PHASE` `C0` (line 77) and B10 C1 in job `full-C3` with `C1` (line 468). Both call `phase.sh run-repeats`, `judge` and `candidate` (lines 199-212, 611-636). Its committed copy is `docs/operations/workflows/scenario.yml.txt`.
- `phase.py` holds the B10 modes `("start-line", "loadgen", "loadgen2", "verify")` (line 26) and runs them per repeat in `run_b10_repeats` (lines 944-972). `b10_prepare_sweep` requires 10 producer observations and 5 `verify-completion.json` receipts (line 923).
- The scientific verifier assumes two producers: `B10IntakeChecks.BindRegistrations` (lines 16 and 19), `B10PhaseRegistrationReader` (line 42), `PublicationAcceptance` (lines 80-84), `B10SweepHandler` (lines 189-200, 237), `B10JudgeChecks` (lines 42-43) and `JudgeHandler` (line 391). `PhaseIdentity.RestartJobName` returns `...-loadgen2` (line 37).
- The restart path is trial-only. LoadGen `B10AbruptOptions.cs` lines 76-77 require `Purpose` `diagnostic_trial` and `Repeat` `1`; line 86 requires `{RunId}-c1-r1-loadgen-{restart|successor}`. Probe keys hard-code repeat `1` (`B10AbruptProducer.cs` lines 203, 252, 335; `B10AbruptJournal.cs` lines 177, 190). The Blob prefix hard-codes `C1/r1/b10-abrupt-diagnostic` (`B10AbruptPublisher.cs` lines 134, 213). The envelope writes and requires `purpose` `diagnostic_trial` and `scientific_admissible` false (`B10AbruptWire.cs` lines 54, 66, 69).
- Chart: `values.schema.json` line 649 makes `loadgen-restart` and `loadgen-successor` require `b10_abrupt_diagnostic` true; line 650 then fixes `run_kind` `smoke`, `phase` `C1` and `repeat` 1. Line 931 restricts `b10-abrupt-verify` to repeat 1 and run ID `^b10d-[0-9]+-[0-9]+$`. `jobs.yaml` writes `B10Abrupt__Purpose` as the literal `diagnostic_trial`.
- Verifier: `VerifierHostOptionsValidator.cs` lines 226-227 and 258-259 fix `b10-abrupt-verify` to `B10/C1/C1/1` and prefix `<RunId>/C1/r1/producer/`. `B10AbruptVerifyHandler.cs` line 47 lists `<RunId>/C1/r1/b10-abrupt-diagnostic/`; lines 231-247 write repeat 1 and `diagnostic_trial`. `VerifierHostOptions.ExpectedProducerJobNames` (lines 59-70) and `StartLineNoteCodec` (line 45) give three names only for `b10d` run IDs.
- `phase.py` `reject_b10_abrupt_science` (lines 279-307) fails every scientific entry when the runner environment or `phase.json` carries a diagnostic marker.

### 2.2 Files allowed to change

1. `infra/scripts/phase/phase.py` and `infra/scripts/phase/b10_abrupt.py`: the scientific C1 orchestration of 3.2 and 3.3. The trial's behaviour stays byte-identical in its outputs (invariant I1). Their tests under `infra/scripts/phase/tests/`.
2. `src/Parchwell.LoadGen/Run/Abrupt/**`: the scientific purpose, repeat, Job name, key and prefix rules of 3.4. No other LoadGen path. `tests/Parchwell.LoadGen.Tests/Abrupt/**` and `tests/Parchwell.LoadGen.Tests/Host/RestartJobIdentityTests.cs`.
3. `charts/parchwell/templates/jobs.yaml`, `templates/_helpers.tpl`, `values.schema.json`, and `charts/parchwell/tests/**`: the scientific values of 3.5.
4. `src/Parchwell.Verifier/VerifierHostOptions.cs`, `VerifierHostOptionsValidator.cs`, `Common/Window/StartLineNoteCodec.cs`, `src/Parchwell.Verifier/VerifyRun/Abrupt/**`: 3.1 and 3.6. Tests under `tests/Parchwell.Verifier.Tests/` (Component files prefixed `B10AbruptVerify`, Host files `B10ExpectedProducerJobsTests.cs` and `B10TrialStartLineOptionsTests.cs`, Window codec tests, and new files prefixed `B10ScientificThreeJob`).
5. `docs/operations/b10-abrupt-contract.md` (owner of the formats of 3.4 to 3.6), `docs/operations/b10-workflow.md`, `docs/operations/verifier-contract.md`, `docs/operations/loadgen-contract.md` and `docs/operations/chart-contract.md`, only where they state the trial-only or two-name rules this plan changes.
6. `items/404/state.md` and a new receipt `items/404/b10-scientific-three-job-receipt.md`.

### 2.3 Paths that must not change

- Every file under `.github/` and `docs/operations/workflows/`. No workflow change is needed: the modes live in `phase.py` (2.1). If one is needed, stop (section 12).
- `infra/ephemeral/**`, `infra/persistent/**`, `infra/cluster/**`.
- `src/Parchwell.Kafka/**`, and every LoadGen file outside `Run/Abrupt/`. The ordinary host already accepts `{run}-c1-rN-loadgen` for N in 1..5 (`LoadGenHostOptions.cs` lines 22, 98-106).
- The verifier sign-in fix's files: `src/Parchwell.Verifier/Program.cs` and `src/Parchwell.Verifier/Common/Kafka/**`.
- Every file in `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`'s `mutate` list (lines 17-35), and that file. They include `PublicationAcceptance.cs`, `B10IntakeChecks.cs`, `B10RepeatChecks.cs`, `B10PhaseRegistrationReader.cs`, `B10SweepHandler.cs`, `B10JudgeChecks.cs`, `B10SweepChecks.cs` and `B10SweepEvidenceCodec.cs`. Also `VerifyRunHandler.cs`, `JudgeHandler.cs` and `PhaseIdentity.cs`.
- `docs/blueprint.md`, `docs/operations/a01-runtime-limits.json`, `scenarios/**`, `results/**`. `scenarios/B10/expected.yaml` must not be created.
- `AGENTS.md`, `CLAUDE.md`, `docs/agents/**`, any `*.csproj`, `Directory.*.props`, `*.sln*`. No new project, assembly reference or NuGet package.
- `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs`, which passes unchanged.

### 2.4 Protected paths, CI, mutation and existing tests

- Protected: `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**` (`Parchwell.Verifier*/`). Hari merges.
- Required checks: `.NET verification`, `Lifecycle verification`. `docs-advisory` is advisory.
- Mutation: no `mutate` target changes (2.3), so no mutation run is part of this pull request (docs/engineering/dotnet.md line 55).
- Standing fixture rule (plan-pr "Plan rules" rule 2, modelled on [6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2). When an existing test's fixture is rejected by a check this plan adds or tightens, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; or (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The receipt lists each change with the check that forces it. The cold review and Codex review check each listed change.
- Inventory of existing tests this plan's checks reject (found by search on `675f8372`; the receipt completes it):
  - Name derivation for C1: `B10ExpectedProducerJobsTests.cs` line 45 asserts `["...-c1-r5-loadgen", "...-c1-r5-loadgen2"]` for valid input. This is a contract change, not a fixture-rule use: its expected value becomes 3.1's three names (T1). The receipt records it as such.
  - Legacy two-instance verify: `B10TwoInstanceVerifyTests.cs` and `B10RepeatVerifyTests.cs` hold only `C0` fixtures (plan check 6846109878, P2-D), so 3.1 changes none of them. Neither file may change.
  - Codec: Window tests that write a two-name B10 C1 note for a non-trial run ID. Rule (b): they expect `ArgumentException`.
  - LoadGen: `B10AbruptOptionsTests.cs` lines 48-67 InlineData `Repeat` "2" and `Purpose` "scientific" with the fixture run ID "synthetic". Rule (b): with run ID "synthetic" they still fail with `DiagnosticIdentity` under 3.4, so expected values are unchanged. Each still runs.
  - Chart: `test_b10_abrupt_chart.py` `invalid_cases` (lines 112-130) `('repeat', 2)` and `invalid_reader_cases` (lines 197-205) `('repeat', 2)` and `('run_id', 'b10-1-1')`. Each keeps its diagnostic fixture and stays rejected under 3.5; expected values unchanged.
  - Phase: `test_b10_abrupt.py` `test_every_scientific_entry_rejects_before_work_and_removes_candidate` (line 328) stays unchanged.

## 3. External contracts

Identities: `R` is the scientific run ID `b10-{GITHUB_RUN_ID}-{GITHUB_RUN_ATTEMPT}` (pattern S `^b10-[0-9]+-[0-9]+$`); `N` is the repeat, 1 to 5, as invariant decimal. Pattern T `^b10d-[0-9]+-[0-9]+$` is the trial's.

### 3.1 Start-line names

- WHEN scenario is `B10` and configuration is `C1`, THEN the expected producer Job names are exactly `[R-c1-rN-loadgen, R-c1-rN-loadgen-restart, R-c1-rN-loadgen-successor]`, for every run ID, trial or not. The successor name is present even when no successor starts (ADR-021 "Consequences").
- WHEN scenario is `B10` and configuration is `C0`, THEN they stay `[R-c0-rN-loadgen, R-c0-rN-loadgen2]` (Q2 option B).
- Other scenarios keep their one name.
- The codec writes a B10 C1 note only with exactly three names and a B10 C0 note only with exactly two. Any other count throws `ArgumentException`.
- Scientific readers that compare B10 start lines with `[loadgen, loadgen2]` (`PublicationAcceptance`, `B10SweepHandler`, `JudgeHandler`) stay unchanged. They therefore reject every C1 note, which retires the two-Job C1 science path.

### 3.2 Scientific C1 orchestration (phase)

For B10 C1 science, `run-repeats` runs, for N = 1..5, in this order:

| Order | Job | Rule |
|---|---|---|
| 1 | `R-c1-rN-start-line` | Ordinary start line. Metadata check of 3.3 before any producer Job |
| 2 | `R-c1-rN-loadgen` | Ordinary C1 timeout host, mode `loadgen`, no `B10Abrupt__` setting |
| 3 | `R-c1-rN-loadgen-restart` | Parent and self-killing child, mode `loadgen-restart` |
| 4 | `R-c1-rN-loadgen-successor` | Only after repeat N's first publication was accepted. Acceptance already requires `successor_permitted` true (`b10_abrupt.py` line 717). Release at `kill_intent` UTC plus 120 s |
| 5 | `R-c1-rN-b10-abrupt-verify` | Whenever repeat N's first publication was accepted, whatever the successor outcome. Not after a rejected publication, a no-kill end, an empty kill set or unknown first termination |

- Each step's outcome handling is the trial's, as `docs/operations/b10-abrupt-contract.md` "Trial order and Job identities" and "Failure names and collection" state it for repeat 1, applied to repeat N. Failure names are the existing closed list; no new producer failure name.
- Between repeats there are exactly two kinds of end:
  - Phase stop. When `safety_stop` is true after any Job of repeat N, no further Job of any repeat is applied. Every later slot is recorded with reason `safety_stop`, as `run_mode` records it. `safety_stop` is set exactly where the trial sets it (unknown or `apply_failed` timeout Job, unknown first termination, unknown or `apply_failed` restart or successor Job) and where a failed pod read cannot be confirmed (next bullet).
  - Repeat end. Every other failure ends repeat N's chain at the point the trial would stop, records the trial's failure name at the trial's boundary, and lets repeat N+1 start with its start line. This includes `render_failed`, which records `b10_abrupt_configuration_invalid` and leaves `safety_stop` unchanged, a rejected first publication, a no-kill end and a failed metadata check.
- Pod reads. After every producer Job of every repeat, before the next producer Job is rendered, the phase reads its pod once as the trial does for that Job. WHEN that read fails, THEN the phase runs the existing `b10_stop_unobserved` deletion and confirmation for that Job (`phase.py` lines 852-877); WHEN confirmation does not show zero pods, THEN `safety_stop` is set. So at most one producer pod runs at a time, across repeats.
- Successor publication check. After repeat N's successor Job, and before its verify Job, `run-repeats` applies the trial's successor checks (`b10_abrupt.py` `collect(files, observation)`, lines 973-1016) to that repeat. A failure records the trial's failure name at the trial's boundary, ends no chain and does not prevent the verify Job. The separate `collect-b10-abrupt-trial` command is not used by science; `scenario.yml`'s existing artifact upload carries the copied Job folders.
- All repeats share the existing 18000 s orchestration deadline (`B10_ORCHESTRATION_SECONDS`, `phase.py` line 28). Q6 states the configured worst case.
- After repeat 5, C1 runs no `b10-sweep-line`, `b10-sweep` or `judge` Job. `judge` and `candidate` for B10 C1 record the gap `b10_c1_final_judgement_unavailable` in `phase-status.json`, write no candidate, keep `full_allowed` false and exit 1 (Q7).
- The scientific path never sets `b10_abrupt_diagnostic`, `purpose` `diagnostic_trial` or `run_kind` `smoke` on its own account in `phase.json`, so `reject_b10_abrupt_science` is unchanged and still passes it.

Lifecycle record, an external contract with item (b). After each producer and verify Job of a C1 repeat, the phase appends to `phase-status.json` `b10_lifecycle.producers` (producers) or `b10_lifecycle.verifiers` (verify Jobs) one object `{job, repeat, mode, pod_name, pod_uid, finished_utc, exit_code}` from one pod read (`kubectl get pods -l job-name=<job>`), or appends `{object: "<job>/pods.json", reason: "producer_termination_unavailable"}` or `"verifier_termination_unavailable"` to `b10_lifecycle.gaps`. `finished_utc` is the container's `state.terminated.finishedAt`, normalized by `b10_utc` to 7 fractional digits and `+00:00`. A skipped Job adds nothing.

### 3.3 Metadata check for scientific C1

Before repeat N's timeout Job, the phase downloads `R/C1/rN/start-line.json`, `broker-inventory.json` and `start-line-completion.json` and applies the trial's `metadata()` checks with repeat N: receipt `repeat` N and `job_name` `R-c1-rN-start-line`; identity Job `R-c1-rN-loadgen`; `expected_job_names` equal to 3.1's list. Each failure records what the trial's `metadata()` records for it: `b10_abrupt_publication_ambiguous` for a missing object, otherwise `b10_abrupt_publication_failed`, at boundary `metadata`. No producer Job of repeat N is applied, and the repeat ends (3.2). It then creates configmap `R-b10-abrupt-first-rN` (and later `R-b10-abrupt-successor-rN`) with the trial's binding format.

### 3.4 LoadGen restart and successor (scientific)

- `B10Abrupt__Purpose` accepts `diagnostic_trial` (unchanged rules: repeat 1) or `scientific`. WHEN `Purpose` is `scientific`, THEN `RunId` must match S, `Repeat` must be `1` to `5` as written without leading zero, and ScenarioId, ConfigurationId and Phase stay `B10`, `C1`, `C1`. Otherwise validation fails with `ValidationDetail.DiagnosticIdentity`.
- Job names: `{RunId}-c1-r{Repeat}-loadgen-restart` and `{RunId}-c1-r{Repeat}-loadgen-successor`. For `diagnostic_trial` this is `r1`, as today.
- Probe keys and probe IDs: `B10/{RunId}/C1/{Repeat}/same_instance` and `B10/{RunId}/C1/{Repeat}/cross_restart` (ADR-021 protocol rule 1, invariant decimal repeat). For the trial this is the existing literal.
- Blob prefix: `{RunId}/C1/r{Repeat}/b10-abrupt/{pod}/` when `Purpose` is `scientific`; `{RunId}/C1/r1/b10-abrupt-diagnostic/{pod}/` unchanged for the trial (Q3).
- Envelope: every object and journal record carries `purpose` equal to the configured `Purpose`. `scientific_admissible` stays `false` for both purposes: no per-repeat object is admissible on its own; only item (b)'s final projection can be (Q3). `schema_version` 3, `dialect` `b10_abrupt`, `synthetic` true and every other format stay as `b10-abrupt-contract.md` states them. Readers (LoadGen successor, phase, verifier) require `purpose` equal to the purpose their own run derives: `scientific` when the run ID matches S, and `diagnostic_trial` for every other run ID, including T and test run IDs such as `synthetic`, `run-1` and `offline-image`. A mismatch fails as a mismatched envelope does today: `malformed_frame` in LoadGen, `ValueError('diagnostic envelope')` recorded as `b10_abrupt_publication_failed` in the phase (`b10_abrupt.py` lines 462-465), problem 2 or 4 in the verifier.
- The successor's predecessor checks use `{RunId}-c1-r{Repeat}-loadgen-restart` and the repeat's keys.

### 3.5 Chart (scientific)

- New runtime key `b10_abrupt_science`, default false. WHEN true, THEN `scenario_id` `B10`, `configuration_id` and `phase` `C1`, `repeat` 1 to 5, `run_id` matching S, `b10_abrupt_binding_configmap` required, and `b10_abrupt_diagnostic` must be false or absent. Modes `loadgen-restart` and `loadgen-successor` require exactly one of `b10_abrupt_diagnostic` and `b10_abrupt_science` true.
- `B10Abrupt__Purpose` renders `scientific` when `b10_abrupt_science` is true and `diagnostic_trial` otherwise. `B10Abrupt__Repeat` renders the repeat.
- Mode `b10-abrupt-verify` accepts either the existing trial values (pattern T, repeat 1) or S with repeat 1 to 5. Its `Verifier__ArtifactPrefix` is `<run>/C1/r<repeat>/producer/`.
- Deadlines are unchanged: `loadgen_job_seconds` 1140, `verify_job_seconds` 750, `start_line_job_seconds` 150 (`values.yaml` lines 96-98).

### 3.6 Per-repeat verifier output (external contract with item (b))

Mode `b10-abrupt-verify` is unchanged for pattern T. For pattern S:

- Options: `ScenarioId` `B10`, `ConfigurationId` and `Phase` `C1`, `Repeat` 1 to 5, `ArtifactPrefix` `<RunId>/C1/r<Repeat>/producer/`. Anything else exits 1 before any read.
- Checks 1 to 29 of `b10-abrupt-contract.md` apply with repeat N in every place they name repeat 1, `r1`, `/C1/1/` or `c1-r1`. Check 3 lists `<RunId>/C1/rN/b10-abrupt/`. Check 4 requires envelope `purpose` `scientific`. Check 16 lists `<RunId>/C1/rN/producer/` and requires Job `{RunId}-c1-rN-loadgen`.
- Earlier-repeat records (ADR-021 "Late-write scan"). New check 30, evaluated when the window was drained and N is at least 2. The handler lists, for every earlier repeat M of the same phase, `<RunId>/C1/rM/producer/` and `<RunId>/C1/rM/b10-abrupt/`, using the existing listing bounds, and reads each `registration.json` found there with the existing registration reader. An empty listing is valid: a repeat whose chain ended before a producer registered contributes no instance. A window record whose `instance-id` equals an instance ID registered there is held out of checks 19 and 20 and of every count, and is counted in `earlier_repeat_records`. WHEN a listing fails or is incomplete at its bounds, or a listed registration cannot be read or is invalid, or an instance ID of repeat N equals an earlier repeat's instance ID, THEN problem [10] `b10_abrupt_verify_earlier_repeat_unavailable` and reason E3 are recorded, and `earlier_repeat_records` is null. For N equal to 1, check 30 is not evaluated and `earlier_repeat_records` is 0.
- Output object: container `verifier`, `<RunId>/C1/rN/b10-abrupt/nonfinal-result.json`, create-only with read-back, as today. Envelope `schema_version` 3, `dialect` `b10_abrupt`, `purpose` `scientific`, `synthetic` true, `scientific_admissible` false, `kind` `nonfinal_result`. Identity `repeat` N, the other 11 members as today.
- `data` members are exactly the members `b10-abrupt-contract.md` "Output `nonfinal_result`" lists, plus `earlier_repeat_records` (integer, or null). `finality` `nonfinal`. `eligibility` `ineligible` or `pending_sweep`, never `eligible`. Problems 1 to 9 keep their order; problem 10 follows them. Reason literals E1 to E13 and their order are unchanged.
- For pattern T the output has no `earlier_repeat_records` member and stays byte-compatible with `main`.
- Item (b) reads this object, `phase-status.json` `b10_lifecycle` (3.2), and the producer evidence under the prefixes of 3.4. Item (b)'s plan states this dependency as an external contract; each plan check checks it against this file's current revision (6845612916 point 2).

### 3.7 Problem and failure literals

- Verifier problem 10: `b10_abrupt_verify_earlier_repeat_unavailable`.
- Phase gap: `b10_c1_final_judgement_unavailable`.
- No other new literal.

## 4. Requirements

- R1. WHEN a B10 C1 start line is written for any run ID, THEN it carries 3.1's three names; WHEN a B10 C0 start line is written, THEN two names as on `main`.
- R2. WHEN `run-repeats` runs B10 C1 science, THEN for each N it applies the Jobs of 3.2 in that order, starting the successor and the verify Job only after an accepted first publication, and applying the successor publication check before the verify Job.
- R3. WHEN repeat N ends under 3.2's repeat-end rule (metadata failure, render failure, rejected first publication, no-kill end), THEN its failure name is recorded at its boundary, no later Job of repeat N is applied, and repeat N+1 starts.
- R4. WHEN `safety_stop` is set during repeat N, including after a failed pod read whose deletion is not confirmed, THEN no later Job of any repeat is applied, and every later slot is recorded with reason `safety_stop`.
- R5. WHEN `judge` or `candidate` runs for B10 C1, THEN `b10_c1_final_judgement_unavailable` is recorded, no candidate file exists, `full_allowed` is false, and the command exits 1.
- R6. WHEN the LoadGen restart or successor host runs with `Purpose` `scientific`, a run ID matching S and `Repeat` N in 1..5, THEN it validates; its Job name, keys and prefix follow 3.4. WHEN `Purpose` is `scientific` and the run ID does not match S, or `Repeat` is outside 1..5, THEN it fails with `DiagnosticIdentity` before registration.
- R7. WHEN a reader receives an envelope whose `purpose` differs from its run's derived purpose, THEN it rejects it as it rejects a malformed envelope today.
- R8. WHEN the verifier runs for pattern S and repeat N with valid evidence, THEN it writes 3.6's object at `<RunId>/C1/rN/b10-abrupt/nonfinal-result.json` with `problems` [], `eligibility` `pending_sweep`, identity `repeat` N and `purpose` `scientific`.
- R9. WHEN repeat N's window holds a record from an earlier repeat's instance, THEN it is held out and counted in `earlier_repeat_records`, and no problem 8 is added for it.
- R10. WHEN an earlier repeat's listing fails or is incomplete, a listed registration is unreadable or invalid, or an instance ID repeats across repeats, THEN problem 10 and E3 are recorded and `earlier_repeat_records` is null. WHEN an earlier repeat's listings are empty, THEN no problem is added.
- R11. WHEN any existing scientific reader (`PublicationAcceptance`, `B10SweepHandler`, `JudgeHandler`, `PhaseWireCodec`, `VerifierResultCodec`, `B10SweepEvidenceCodec`) reads a three-name C1 start line or a 3.6 object, THEN it rejects it.
- R12. WHEN the trial runs (pattern T), THEN its Jobs, names, prefixes, envelope bytes, output path and output members are as on `main`.

## 5. Invariants and safety

- I1. The trial's behaviour, contracts and outputs are unchanged (R12).
- I2. No per-repeat object is `scientific_admissible` true. No verdict, candidate or count is published for C1 by this pull request.
- I3. At most one producer pod runs at a time. Cleanup, collect, upload and destroy keep their existing `always()` steps in `scenario.yml`.
- I4. No workflow dispatch, trial, `results/` or expected-outcome change.
- I5. No Azure credentials in any test. Kafka tests use `LocalKafkaFixture`.

## 6. Test map

| Test | Inputs | Expected |
|---|---|---|
| T1 names | `ExpectedProducerJobNames` with B10, C1, repeat 5, run IDs `b10-1-1`, `b10d-1-1`, `run-1` | each `[x-c1-r5-loadgen, x-c1-r5-loadgen-restart, x-c1-r5-loadgen-successor]`; with C0 and `b10-1-1`, `[b10-1-1-c0-r5-loadgen, b10-1-1-c0-r5-loadgen2]` |
| T2 codec | B10 C1 note with 2 and with 3 names; B10 C0 note with 2 and with 3 | only C1/3 and C0/2 write; the others throw `ArgumentException` |
| T3 LoadGen scientific | `Purpose` `scientific`, run ID `b10-1-1`, `Repeat` "1", "3", "5" | valid; Job `b10-1-1-c1-r3-loadgen-restart` for repeat 3; key `B10/b10-1-1/C1/3/cross_restart`; prefix `b10-1-1/C1/r3/b10-abrupt/` |
| T4 LoadGen rejects | `scientific` with run ID `b10d-1-1`; `synthetic`; `Repeat` "0", "6", "03"; `diagnostic_trial` with `Repeat` "2" | each `DiagnosticIdentity` |
| T5 envelope purpose | scientific journal read by a trial reader; trial journal read by a scientific reader; a `diagnostic_trial` journal with run ID `synthetic` | first two rejected: `malformed_frame` in LoadGen, `b10_abrupt_publication_failed` in the phase, problem 2 in the verifier; the third accepted as on `main` |
| T6 chart | `b10_abrupt_science` true with run ID `b10-1-1`, repeat 1 and 5, modes restart, successor; `b10-abrupt-verify` with `b10-1-1`, repeat 5, `loadgen_job_name` `b10-1-1-c1-r5-loadgen` | renders; `B10Abrupt__Purpose` `scientific`; `B10Abrupt__Repeat` `5`; `Verifier__ArtifactPrefix` `b10-1-1/C1/r5/producer/` |
| T7 chart rejects | science with run ID `b10d-1-1`; repeat 6; both flags true; neither flag for `loadgen-restart`; `b10-abrupt-verify` with `b10-1-1` repeat 6; `b10-abrupt-verify` with `b10d-1-1`, repeat 2, `loadgen_job_name` `b10d-1-1-c1-r2-loadgen` | each a schema failure, not a template failure |
| T8 phase order | fake kubectl/helm, C1 science, every Job complete, `successor_permitted` true | applied Jobs in order: for N=1..5 start-line, loadgen, loadgen-restart, loadgen-successor, b10-abrupt-verify; 25 Jobs; no `b10-sweep-line`, `b10-sweep` or `judge` |
| T9 phase rejected publication | as T8, repeat 2 `termination.json` with `successor_permitted` false | repeat 2: `b10_abrupt_publication_failed` at `first_publication`, no successor, no verify; repeats 3 to 5 apply all five Jobs |
| T9b render failure | as T8, repeat 2 restart render fails | repeat 2: `b10_abrupt_configuration_invalid`, `safety_stop` false; repeat 3 starts with its start line |
| T9c pod read failure | as T8, repeat 2 timeout pod read fails; confirmation shows one pod | `safety_stop` true; no restart Job for repeat 2; repeats 3 to 5 not applied |
| T9d successor check | as T8, repeat 2 successor `completion.json` with `cleanup_completed` false | repeat 2 records `b10_abrupt_publication_failed` at `successor_receipt` and still runs verify |
| T10 phase metadata | repeat 2 start line with `[loadgen, loadgen2]` names | repeat 2: `b10_abrupt_publication_failed` at `metadata`, no producer Job; repeat 3 runs |
| T11 phase safety | repeat 2 timeout Job outcome `unknown` | `safety_stop` true; repeats 3 to 5 not applied, reason `safety_stop` |
| T12 lifecycle record | T8 run | `b10_lifecycle.producers` 15 objects, `verifiers` 5, `gaps` []; each with the 7 members of 3.2 |
| T13 judge gap | `judge` and `candidate` after T8 | gap `b10_c1_final_judgement_unavailable`; no candidate; `full_allowed` false; exit 1 |
| T14 verifier repeat N | component harness, run ID `b10-1-1`, repeat 3, valid evidence | output at `b10-1-1/C1/r3/b10-abrupt/nonfinal-result.json`; `purpose` `scientific`; identity `repeat` 3; `problems` []; `eligibility` `pending_sweep`; `earlier_repeat_records` 0 |
| T15 earlier record | as T14, window holds one record whose `instance-id` is repeat 2's timeout instance | `earlier_repeat_records` 1; `problems` []; timeout counts unchanged from T14 |
| T16 earlier unavailable | as T14, three subcases: repeat 2 producer listing fails through the store seam; a repeat 2 registration with an invalid envelope; repeat 3 timeout instance equal to repeat 1's | problems [10], reasons [E3], `earlier_repeat_records` null |
| T16b earlier empty | as T14, repeat 2 listings both empty | `problems` []; `earlier_repeat_records` 0 |
| T17 repeat 1 | run ID `b10-1-1`, repeat 1 | check 30 not evaluated; `earlier_repeat_records` 0 |
| T18 verifier rejects | pattern S with prefix `b10-1-1/C1/r2/producer/` at repeat 3; repeat 6 | exit 1 before any read |
| T19 old readers | T14 bytes; a three-name C1 start line | `PublicationAcceptance`, `B10SweepHandler`, `JudgeHandler`, `PhaseWireCodec`, `VerifierResultCodec`, `B10SweepEvidenceCodec` reject |
| T20 trial unchanged | the existing trial tests in `test_b10_abrupt.py`, `test_b10_abrupt_chart.py`, `B10AbruptVerify*` and `B10AbruptOptionsTests` | pass with their existing expected values |
| T21 science never diagnostic | scientific `phase.json` after T8 | `reject_b10_abrupt_science` passes; no `b10_abrupt_diagnostic` key |

Each verifier row asserts that every other enforced check still passes in that subcase. The receipt records the rest of the predicate map: below, at and above each boundary (repeat 0, 1, 5, 6; name count 2 and 3).

## 7. Deployment assumptions (live-only)

Plan rule 3 terms: "fresh" means a connection the process opened for this observation; "idle" means no producer was sending. No scientific three-Job run has executed. The evidence is the diagnostic trial of repeat 1 only.

| ID | Assumption | Evidence, and how it was taken | If false |
|---|---|---|---|
| LA-S1 | Five sequential self-kill repeats in one phase each publish their first evidence; repeat N+1's pods start after repeat N's terminate | Runs 43217277627 and 43322351104: one repeat each, fresh pods and connections, idle namespace apart from the trial's own producers. Five repeats never ran | Named failure per repeat; fewer eligible repeats |
| LA-S2 | A record of an earlier repeat's instance that lands in a later window carries that instance's `instance-id` header | No live observation. The header rule is the producers' own (contract "Probe sequence") | Problem 8 instead of an earlier-repeat count |
| LA-S3 | Each repeat's verify Job finishes within 750 s, including check 30's reads of up to four earlier repeats' registrations | Run 43322351104, repeat 1: fresh verifier pod and connections, idle namespace (producers had ended), verify `complete`; check 30 never ran. Run 43318281268 on the same `main` ended its verify with problem 5 after a token timeout (6837900667); five verify Jobs per phase raise that exposure until the parallel sign-in fix merges | `b10_abrupt_verify_failed` for that repeat |
| LA-S4 | The phase finishes five repeats within the 18000 s deadline in practice | Not measured; configured worst case exceeds it (Q6) | Later repeats `orchestration_deadline`; restart held |

Trial-only assumptions this path needs again. Each was accepted by name for one diagnostic trial only and is used up ([6837706862](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837706862), [6837940546](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837940546)). Science repeats them five times per phase:
- LA-1, LA-2, LA-4, LA-5 and LA-6, as stated in `items/404/plan-pr3c-full-classification.md` at `48c52669`, section 7, accepted for one trial in [6827265635](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827265635) point 1. Evidence: runs 43217277627 and 43322351104, one repeat each, fresh pods and connections, idle namespace.
- LA-3b and LA-4b, same file and section, accepted for one trial in [6827960705](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827960705). LA-3b remains inferred, not documented. This session did not read 6827265635 or 6827960705 in full; the state file's lines and the PR3c plan are the sources used.
- LA-V2 to LA-V5 of [6816733258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816733258) point 2. LA-1, LA-2, LA-3b and LA-4 restate them, so they need no separate acceptance if those are accepted.
- The warm-leader live assumptions LA1 to LA5 of [6799006446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6799006446) and LA6 of [6801095762](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801095762) point 1, which ADR-021 protocol rule 15's second amendment records for the next diagnostic trial only.

All of these, and LA-S1 to LA-S4, are open under Q5 until Hari accepts them by name for science.

## 8. Acceptance commands

| # | Command | Evidence |
|---|---|---|
| A1 | `./scripts/verify-dotnet.sh` | `Verification passed.`; reports include T1 to T5, T14 to T20, T16b |
| A2 | `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` and the same for `infra/scripts/phase/tests` | `OK`; includes T8 to T13, T9b to T9d, T21 |
| A3 | `bash infra/scripts/lifecycle/check-offline.sh` | exit 0; includes T6, T7 |
| A4 | `npm run check:docs` | each warning fixed or explained (docs/agents/pull-requests.md rule 8) |
| A5 | `git diff --name-only origin/main...HEAD` | a subset of 2.2, checked by script against 2.3 |

## 9. Caps and plan-check answers

Caps per implementing session, under 6845612916 point 2: 20 verification-failure root causes and 20 review-finding root causes. Exhaustion of either: `push -wip and hand off to the next session` (AGENTS.md "Before opening a pull request" rule 11).

(a) Steps whose effort scales with a number unknown at planning time:
- Implementer root causes: capped above.
- Existing test cases rejected by the new checks (2.4 lists the files, not the case count). No authority caps this count. Under AGENTS.md "Before opening a pull request" rule 11, the implementer measures it once, records the count and an estimate, then pushes `-wip` and hands off to the next session, unless Hari sets a cap first (Q9).
- Runtime work per repeat is bounded by the existing limits (`MaxConsumedRecords`, listings of 1024 entries and 16 pages, journal 4096 records). Check 30 reads at most four earlier repeats.

(b) Stricter than the parent (item plan Revision 5 and ADR-021): check 30 makes a failed or unreadable earlier listing or registration a named problem with E3, where ADR-021 states no rule; an empty earlier repeat is not stricter. Purpose-bound readers (R7) reject cross-purpose evidence. C1 `judge` and `candidate` exit 1 with a named gap until item (b) merges (Q7), where `main` would run the legacy judge.

(c) Dependencies outside `main` and this pull request: the live assumptions of section 7. Item (b) depends on this plan's 3.2 and 3.6, not the other way round.

The live-smoke rule of docs/agents/factory.md "Item lifecycle" step 1 applies to plans that change live infrastructure. This plan changes no Terraform root; whether a diagnostic run of the scientific path must precede science is Q5.

External behaviour (plan-pr "Plan rules" rule 1): the plan relies on one behaviour outside this repository, that from Kubernetes v1.31 the Job controller adds `Complete` or `Failed` only after all Job pods terminate. Verified with Context7 (`/websites/kubernetes_io`), from https://kubernetes.io/docs/concepts/workloads/controllers/job/, "Terminal Job conditions", on 2026-11-13. The phase still reads pod termination directly, as the trial does. No Kafka, librdkafka, Confluent.Kafka or Event Hubs behaviour is newly relied on; ADR-021 owns those already relied on.

## 10. Sources

- ADR-012, ADR-013, ADR-020 and ADR-021 (`docs/blueprint.md` on `main` at `675f8372`), in particular ADR-021 "Restart", "Late-write scan", protocol rules 1, 8, 10 and 14, and "Consequences".
- `docs/operations/b10-abrupt-contract.md`, `docs/operations/b10-workflow.md` on `675f8372`.
- Code cited in 2.1 at `675f8372`.
- `items/404/plan-b10-restart-redesign.md` at `b471d02d`, lines 269 and 411-415; `items/404/plan-pr3c-full-classification.md` at `48c52669`, sections 3.1 and 9(b).

## 11. Open questions for Hari

- Q1. Implementing tool (6845612916 point 2). Options: A Codex; B Claude Code. Recommendation: none; the prior B10 verifier pull requests were Codex, the sign-in fix Claude Code.
- Q2. C0. ADR-021 rule 1 gives C0 one ordinary producer Job; `main` still runs `loadgen` and `loadgen2` for C0. Item (b) Revision 1 reads a one-Job C0, and its plan check 6846147228 (B6) finds that no plan supplies it. Options: A, this pull request also moves C0 to one Job; that changes C5 mutation targets (`B10IntakeChecks.cs`, `B10PhaseRegistrationReader.cs`, `PublicationAcceptance.cs`), lifts 2.3's freeze on them and adds a post-merge mutation run (docs/engineering/dotnet.md line 55), and needs a Revision 3 of this plan; B, C0 stays as on `main`, and a third pull request moves it before item (b) can be implemented; C, item (b) owns it. Recommendation: A, because this pull request already owns the B10 path shape and item (b)'s contract then has one supplier. Counterargument: A roughly doubles this protected pull request and adds a mutation gate. This plan as written implements option B until Hari answers.
- Q3. Scientific literals. Options: A, `purpose` `scientific`, prefix `b10-abrupt/`, `scientific_admissible` false on every per-repeat object (as 3.4 and 3.6 pin); B, keep `diagnostic_trial` and `b10-abrupt-diagnostic` for science too. Recommendation: A, so no reader can mistake trial evidence for science. Counterargument: A changes envelope readers in three languages.
- Q4. Scope of the per-repeat verifier. Options: A, generalizing `b10-abrupt-verify` to repeats 1 to 5 and science is in this pull request (3.6); B, it moves to item (b). Recommendation: A, because without it this path produces no per-repeat object for (b) to read. Counterargument: it makes this pull request large and protected.
- Q5. Accept by name, for science, LA-S1 to LA-S4 and the trial-only assumptions listed in section 7, and decide whether a diagnostic run of the scientific path precedes science. Recommendation: accept for planning only; require one governed diagnostic run after both items merge.
- Q6. Time budget. By script from `values.yaml`, each C1 repeat's configured wait bounds are 150 + 3 x 1140 + 750 plus 90 s per Job wait and the 120 s release, 4890 s; five repeats 24450 s, above the 18000 s orchestration deadline and the 21600 s job timeout (`scenario.yml` line 454, `timeout-minutes: 360`). These are configured bounds, not measurements. Options: A keep both, so exhaustion records later repeats as `orchestration_deadline`; B raise them, which changes `scenario.yml` (protected, Hari merges). Recommendation: A for this pull request. Counterargument: a slow phase can leave fewer than three eligible repeats and hold restart.
- Q7. C1 judge and candidate before item (b) merges: the gap `b10_c1_final_judgement_unavailable` and exit 1 (3.2). Options: A as pinned; B run the legacy judge, which rejects three-name notes anyway. Recommendation: A, a named gap instead of an incidental rejection.
- Q8. Problem 10 and `earlier_repeat_records` (3.6). Options: A as pinned; B leave earlier-repeat records to item (b)'s sweep and record them as problem 8 here. Recommendation: A, because ADR-021 excludes such records from the later repeat, not invalidates it.
- Q9. Cap on existing test cases changed under the standing fixture rule (9(a)). Options: A, 40 cases, exhaustion `push -wip and hand off to the next session`; B, no cap, so rule 11 stops at the first measurement. Recommendation: A; 40 is a policy choice, not a measurement. Counterargument: the inventory is not complete until implementation.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract of section 3, a requirement of section 4, or the scope of section 2, including any C5 target or workflow file, or when an existing test conflicts outside the standing fixture rule. Hitting a section 9 cap pushes `-wip` and hands off to the next session. A design-changing revision of this plan gets one fresh `plan-checker` (docs/agents/factory.md "Item lifecycle" step 1).
