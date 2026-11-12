# PR3c full classification contract-grade plan

Revision 1. Issue 404, pull request short name `PR3c full classification`. Status: not approved. Implementing tool: not named (Q1). Hari merges.

This pull request completes the diagnostic verifier mode `b10-abrupt-verify`. It adds full reconciliation of the three trial instances, classification of the C1 timeout cohort, the per-arm counts ADR-021 publishes, and an eligibility literal with ordered reasons. It also makes the diagnostic trial's start line carry the three C1 Job names. The result stays nonfinal and diagnostic: no sweep, no verdict, nothing admissible to science.

## 0. Changes

Revision 1 is the first revision. There is no earlier plan for this pull request. The working name PR3c was first used in [split decision 6750021224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750021224) item 3 for the trial read-back. That work merged inside PR3b ([6815988248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6815988248) point 2). [Decision 6824437250](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6824437250) point 1 reuses the name for this pull request.

## 1. Goal and authority

Goal: after the next diagnostic trial, one nonfinal object answers, for the one C1 repeat, whether each instance's evidence reconciles, what the timeout cohort's six counts are, what each arm's unreported, pair and lost-resend counts are, and whether the repeat would be eligible pending the sweep, with every reason it is not.

Authority, in creation order, under docs/agents/factory.md "Item lifecycle" step 3:
- The issue body and the decisions listed in `items/404/state.md` on `main` at `78cba6e467c7bd0333f131e159f4989b6cfee828`, which covers comment 6816963301. They include ADR-021 on `main` and the PR3b approval [6816963301](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816963301).
- [6761524200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761524200) and [6761542603](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761542603): answers to PR3b Revision 1 (`b4ece136`). Section 11.1 records which still apply here. 6761542603 states that a changed literal, path or option returns to Hari.
- [6816117386](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816117386) point 1: full reconciliation and eligibility follow PR3b in a later pull request of the same mode, before any science. Point 2: the diagnostic object may carry the per-message table and extras; the later final sweep projection drops them.
- [6816733258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816733258) point 1: the two-name start-line gap of [6787849492](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787849492) point 1 extends to this pull request.
- [6823517462](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6823517462): one trial, run 43217277627, now run. It authorizes no further trial.
- [6824437250](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6824437250): this plan. Its points 1 to 5 are applied, not restated: point 1 in sections 2 and 3, point 2 in section 9, point 3 in section 2, point 4 in section 7 and Q7, point 5 in section 12.

Catch-up: 286 comments, 3 pages. New since the state file: 6823517462, 6824110241 (trial evidence) and 6824437250, each read in full. No comment the state file lists was edited or deleted. Issue `updated_at` 2026-11-12T13:06:31Z. Every `## Decision` and `## Approval` comment whose body names PR3b, PR3c, classification, eligibility, reconciliation, the timeout cohort, the three names or this mode was read in full (30 comments), plus the PR3b Revision 1 plan at `b4ece136` and Revision 4 at `aedb21c0`.

What PR3b defers to this pull request, read on `main` at `78cba6e4`: `docs/operations/b10-abrupt-contract.md` line 227 lists native-error and fatal coverage, cgroup and child identity, report leader binding, statistics and capabilities, warm-up ordering beyond check 8, the timeout cohort, registrations of other instances, eligibility and the sweep. Line 67 holds the two-name gap. `items/404/pr3b-receipt.md` line 29 adds the successor inventory semantics. PR3b Revision 3 also removed Revision 2's comparison (plan file at `aedb21c0`, line 26).

## 2. Scope

Decision 6824437250 point 1 sets the scope: full reconciliation, eligibility reasons, timeout-cohort classification and the three C1 start-line names. The sweep and the final sweep projection are not in it. Whether the per-arm unreported, lost-resend and comparison fields are in it is Q2; this plan is written with them in.

The plan freezes files and contracts, not lines (6824437250 point 3). Line numbers below are references, not boundaries.

### 2.1 Files allowed to change

1. Code under `src/Parchwell.Verifier/VerifyRun/Abrupt/`, new or existing. File split and private names are the implementer's choice.
2. `src/Parchwell.Verifier/VerifierHostOptions.cs`, `VerifierHostOptionsValidator.cs` and `Program.cs`: the three-name derivation of 3.1 and the mode options of 3.2.
3. `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs`: the expected-name count rule of 3.1.
4. `src/Parchwell.Verifier/StartLine/StartLineHandler.cs`, only if 3.1 needs it. Its behaviour for every run ID outside the trial pattern stays byte-identical.
5. Tests under `tests/Parchwell.Verifier.Tests/Component/` prefixed `B10AbruptVerify`, and new tests in `tests/Parchwell.Verifier.Tests/Host/` and `tests/Parchwell.Verifier.Tests/Window/` for 3.1, prefixed `B10TrialStartLine`.
6. `infra/scripts/phase/b10_abrupt.py` (`metadata()` only) and `infra/scripts/phase/tests/test_b10_abrupt.py`.
7. `charts/parchwell/templates/jobs.yaml`, `charts/parchwell/tests/test_chart.py` and `charts/parchwell/tests/test_b10_abrupt_chart.py`: the added mode options of 3.2.
8. `docs/operations/b10-abrupt-contract.md` (owner of the formats of 3.3 to 3.6), `docs/operations/verifier-contract.md` lines 5 and 101, and `docs/operations/b10-workflow.md` line 67.
9. `items/404/state.md` and a new receipt `items/404/pr3c-receipt.md`.

### 2.2 Paths that must not change

- Every file under `.github/` and `docs/operations/workflows/`. The trial already runs the verify Job before collect and destroy (`.github/workflows/b10-abrupt-trial.yml` lines 161, 166 and 186).
- `infra/ephemeral/**`, `infra/persistent/**` and `infra/cluster/**`.
- `src/Parchwell.LoadGen/**`, `tests/Parchwell.LoadGen.Tests/**` and `src/Parchwell.Kafka/**`. The abrupt LoadGen reads only `broker_inventory` from its start-line binding (`B10AbruptHost.cs` `ReadExpectedBrokersAsync`), and no LoadGen file reads `expected_job_names` (searched on `main`).
- Every file in `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`'s `mutate` list, including `PublicationAcceptance.cs`, `B10RepeatChecks.cs`, `B10IntakeChecks.cs` and `B10PhaseRegistrationReader.cs`, and that config. Q4 holds the alternative.
- `RunJudgement.cs`, `OutcomeClassifier.cs`, `LateWriteChecks.cs`, `StatisticsChecks.cs`, `NativeStatisticsReader.cs`, `WindowConsumer.cs`, `MessageIdentityReader.cs` and `RunWindow.cs`. They are reused unchanged.
- `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs` and `B10ExpectedProducerJobsTests.cs`, which pass unchanged.
- `phase.py`, and every phase, chart and verifier behaviour for scientific `b10` runs and every other scenario.
- `docs/blueprint.md`, `docs/operations/a01-runtime-limits.json`, `scenarios/**` and `results/**`. `scenarios/B10/expected.yaml` must not be created.
- `AGENTS.md`, `CLAUDE.md`, `docs/agents/**`, any `*.csproj`, `Directory.*.props` and `*.sln*`. No new project, assembly reference or NuGet package.
- Every existing test assertion, except those in 2.3, which change without loosening.

### 2.3 Protected paths, CI and existing tests

- Protected: `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**` match `Parchwell.Verifier*/` in `.github/CODEOWNERS`, so Hari merges. `/docs/operations/` has no owner there.
- Required: `.NET verification`, `Lifecycle verification` (phase suite and `charts/parchwell/tests/check.sh`). `docs-advisory` is advisory.
- Existing tests that change: the PR3b `B10AbruptVerify*` tests that assert the closed `data`, arm and member sets gain the members of 3.4. `test_chart.py` `VERIFIER_REQUIRED['b10-abrupt-verify']` gains the names of 3.2. Each keeps every existing assertion on the members it already names.
- Mutation: no C5 target changes, so no mutation run is part of this pull request.

## 3. External contracts

### 3.1 Three C1 start-line names (trial only)

Pattern T is `^b10d-[0-9]+-[0-9]+$`, the trial run-ID pattern of `values.schema.json` line 931 and contract line 42.

- WHEN scenario is `B10` and `RunId` matches T, the derived expected producer Job names are exactly `[{RunId}-{phase}-r{repeat}-loadgen, {RunId}-{phase}-r{repeat}-loadgen-restart, {RunId}-{phase}-r{repeat}-loadgen-successor]`, lowercase phase. Otherwise they stay as on `main`: `[...-loadgen, ...-loadgen2]` for B10, one name for other scenarios.
- The start-line note's `expected_job_names` carries that list. The codec writes a B10 note only with exactly three names when its run ID matches T, and exactly two otherwise.
- The phase's `metadata()` requires `expected_job_names` equal to `[mode_job('loadgen'), mode_job('loadgen-restart'), mode_job('loadgen-successor')]` for repeat 1. Any other value raises `ValueError('start-line source or jobs')`, as today.
- Mode `b10-abrupt-verify` check 2 expects the three names.
- Scientific readers that compare a start line with `[loadgen, loadgen2]` (`PublicationAcceptance`, `JudgeHandler`, `B10SweepHandler`) stay unchanged, so a trial start line is rejected by each of them.

Q3 holds the alternative: an explicit option instead of the run-ID pattern.

### 3.2 Mode and options

- `Verifier:Mode` `b10-abrupt-verify`, fixed options, output path, create-only publication and exit rule are unchanged from contract lines 193 to 203.
- Required mode options gain `ScenarioRoot`, `ArtifactPrefix`, `MaxRowBytes`, `MaxRowsPerArtifact`, `MaxInventoryEntries`, `MaxInputEntries`, `MaxNativeReportBytes` and `MaxBrokerEntries`, plus the scenario-parsing names `verify` receives (`VERIFIER_SCENARIO` in `test_chart.py`). The chart renders each from the source it uses for `verify`. `ArtifactPrefix` must equal `<RunId>/C1/r1/producer/`; any other value exits 1 before any read.

### 3.3 Inputs and enforced checks

Inputs fixed on `main`: the abrupt formats of `docs/operations/b10-abrupt-contract.md` lines 69 to 185, and the ordinary producer publication of `docs/operations/verifier-contract.md` as `verify` reads it. A change to either is a stop-rule event.

PR3b checks 1 to 15 (contract lines 209 to 223) stay, except check 2, which expects the names of 3.1. New checks follow. A bracketed number is the problem of 3.5; an R number is the eligibility reason of 3.6.

16. Timeout listing. List `<RunId>/C1/r1/producer/`. It holds exactly one directory, `<pod>/`, whose `registration.json` names Job `{RunId}-c1-r1-loadgen`, no predecessor, run `RunId`, `B10`, `C1`, phase `C1`, repeat 1 and source `VerifierCommit`. Any other registration, object outside that directory, or incomplete listing fails. [7] R7. The directory name is the timeout instance ID.
17. Timeout publication. The publication passes every check `PublicationAcceptance.Evaluate` applies to one producer instance, except that the start line's expected names are compared with 3.1's list. [7] R7.
18. Timeout judgement. The instance passes per-instance checks 1 to 27 of `docs/operations/verifier-contract.md`, as `RunJudgement.Evaluate` applies them to a B10 instance, with complete-valid status. Its window assessments use the timeout instance identity and 3.1's names. ADR-021 "Retries" also applies: for each inventory attempt in invocation order, the latest earlier attempt with the same key and sequence has a row for which `B10RepeatChecks.IsPermittedTimeout` is true. [7] R7.
19. Timeout records. Every in-window record whose `instance-id` header equals the timeout instance ID joins exactly one timeout ledger row by `attempt-id`, with equal key and `sequence`, and its key is not an arm key. [8] R3. When check 16, 17 or 18 fails, these records are held out and add no problem.
20. Probe and warm-up records. Every other in-window record joins one of: a first-instance `prepared` attempt (instance, attempt, key, `sequence` and value hash equal to its `payload_sha256`); a first-instance warm-up (attempt `{pod}-{arm}-warmup`, the arm key, `sequence` -1, value hash equal to its `warmup_prepared` `payload_sha256`); or a successor invocation (instance, attempt, key, `sequence` and hash). [8] R3. When problem 3 or 4 applies, records whose instance equals the directory name check 3 found for the successor Job are held out and add no problem.
21. Native capture. `native_error` records have `capture_sequence` 1..n without gaps. Their count equals `termination.native_error_count`, and those with `is_fatal` true equal `native_fatal_count`. `native_tail` is `unknown_due_to_intended_kill`. [2] R3. A record with `is_fatal` true adds R6. A record with `is_error` true and `is_fatal` false adds R5 (6761524200 point 2).
22. Child and cgroup. `termination.child.pid`, `start_time_ticks` and `boot_id` equal `child_started.data`; `parent_observed_start_time_ticks` equals `start_time_ticks`. `cgroup_before` and `cgroup_after` are non-null and equal in `boot_id`, `cgroup_path`, `directory_identity` and `oom_kill`, and their `boot_id` equals the child's. [2] R4.
23. Warm-up order. The journal meets the warm-up rules of contract lines 102 to 108 for both arms. [2] R3.
24. Report binding. Each arm's `pre_acceptance_report` binds that arm's warm-up attempt ID and its `warmup_terminal.partition`; its `report_capture_utc` is strictly later than that terminal's `observed_utc`; and its report bytes satisfy `LeaderUp(report, "document-events", partition)` of contract line 131. The `post_drain_report` follows `control_drained`. [2] R3.
25. Control completeness. `control_drained` has `flush_returned` true and `out_queue_length` 0. The journal holds a terminal record for every control original and control resend, and a `post_drain_report` bound in the manifest. Failure adds R8 only.
26. Successor inventory. `manifest.json` (`cohort_inventory`) has `instance_id` the successor, `cohort` `cross_restart`, `bookkeeping_complete` true and `inventory_count` equal to the invocation count. Its entries equal the invocations in order: `attempt_id`, `predecessor_attempt_id`, `key`, `sequence` and `payload_sha256`, with `invocation_ordinal` 0..n-1. [4] R9.
27. Successor release. Each invocation's `invocation_utc` is not earlier than `release_utc`. `ready_utc` later than `release_utc` adds R11.
28. Guard (Q5). The three first-instance reports parse with `NativeStatisticsReader`. Every broker they name is in the start line's broker inventory, and every `txerrs` and `rxerrs` is 0. The successor's `statistics.jsonl`, `request-lifecycle.jsonl` and `capabilities.json` pass per-instance checks 14 to 27 as `verify` applies them, with ADR-021's B10 permitted conditions. Failure adds R13 only.

Resend counts: check 10 or check 12 failing only because a member has other than one resend gives R10 instead of R3 or R9.

### 3.4 Output `nonfinal_result`

The envelope, identity and the PR3b `data`, arm and member members stay as in contract lines 229 to 268. Added members:

`data`:
- `timeout_instance_id`: string, or null when check 16 fails.
- `timeout`: `{ok, loss, phantom, reported_failure, duplicate, reorder}`, from `OutcomeClassifier.Classify` over the timeout ledger rows and check 19's records; null when problem 1, 5 or 7 applies.
- `comparison`: `both`, `cross_restart_only`, `same_instance_only`, `neither` or `unavailable`.
- `native_errors`: `{count, fatal_count, tail}`, or null when problem 2 applies.
- `eligibility`: `ineligible` when `eligibility_reasons` is nonempty, otherwise `pending_sweep`. Never `eligible` (6761542603 Q3).
- `eligibility_reasons`: distinct literals of 3.6, in that list's order.

Arm:
- `unreported_attempts`: the arm's accepted journaled probe attempts (originals and, for `same_instance`, resends) with no terminal record anywhere in the journal (ADR-021 protocol rule 3).
- `unreported_seen`: those with at least one in-window copy whose instance and attempt headers equal the attempt's. `unreported_unseen`: the rest. Their sum equals `unreported_attempts` (rule 14).
- `lost_resends`: members with `resend_lost` true.

Member:
- `resend_lost`: true when `resend_terminal.delivered` is true and `resend_copies` is 0; false when either is otherwise known; null when either is null.

`comparison` is `unavailable` when any of R1 to R4 or R8 to R12 is present. Otherwise it is `both` when both arms' `pair_count` is at least 1, `cross_restart_only` or `same_instance_only` when only that arm's is, and `neither` when neither is.

Null rules: unknown is never zero. `unreported_seen`, `unreported_unseen`, `lost_resends` and `resend_lost` are null when the copy fields are null under contract lines 281 to 288. `lost_resends` and `resend_lost` are also null for `cross_restart` under problems 3 and 4.

### 3.5 Problem literals

PR3b's six stay first, in order. Added:
7. `b10_abrupt_verify_timeout_evidence_invalid`
8. `b10_abrupt_verify_record_unjoined`

### 3.6 Eligibility reason literals

In this order. 1 to 12 are the literals of PR3b Revision 1 section 4.4 (`b4ece136`), approved under 6761542603 Q2, with conditions mapped to the redesign as below. Q6 asks whether that mapping stands; 13 is new (Q5).

| # | Literal | Condition here |
|---|---|---|
| 1 | `b10_restart_configuration_invalid` | problem 1 |
| 2 | `b10_restart_first_evidence_unavailable` | checks 3 and 5 for the first pod |
| 3 | `b10_restart_identity_unauthenticated` | checks 4, 6, 8, 9, 10, 11, 19, 20, 21 (count or tail), 23 and 24; problems 6 and 8 |
| 4 | `b10_restart_termination_unverified` | checks 7 and 22 |
| 5 | `b10_restart_native_error_captured` | check 21, non-fatal error |
| 6 | `b10_restart_fatal_captured` | check 21, fatal |
| 7 | `b10_restart_timeout_bookkeeping_unavailable` | problem 7 |
| 8 | `b10_abrupt_control_incomplete` | check 25 |
| 9 | `b10_restart_successor_evidence_unavailable` | problems 3 and 4, except resend count |
| 10 | `b10_restart_resend_count_invalid` | resend count (3.3, last line) |
| 11 | `b10_abrupt_successor_late` | check 27 |
| 12 | `b10_restart_window_incomplete` | problem 5 |
| 13 | `b10_restart_guard_failed` | check 28 |

No reason states the missing sweep; `pending_sweep` says it (6761542603 Q3).

### 3.7 Phase and chart

- `metadata()` as in 3.1. No other phase change; no new failure name.
- The chart change is only the added option names of 3.2.

## 4. Requirements

Verifier requirements are observed at the real handler through the mode's entry point, reading the object it writes.

- R1. WHEN `RunId` matches T, THEN the start-line Job writes the three names of 3.1 and mode `b10-abrupt-verify` check 2 expects them. WHEN it does not, THEN both behave as on `main`.
- R2. WHEN a trial start line carries `[loadgen, loadgen2]`, THEN problem 1 and R1 are recorded.
- R3. WHEN every check passes, THEN `problems` and `eligibility_reasons` are `[]`, `eligibility` is `pending_sweep`, `timeout` holds the six counts, and `comparison` follows 3.4.
- R4. WHEN check 16, 17 or 18 fails, THEN problem 7 and R7 are recorded, `timeout` is null, `timeout_instance_id` follows 3.4, and the arms are computed as without the problem.
- R5. WHEN a record fails check 19 or 20, THEN problem 8 and R3 are recorded, and every count is still computed.
- R6. WHEN a check adds a reason without a problem (5, 6, 8, 11, 13), THEN no field becomes null.
- R7. WHEN any of R1 to R4 or R8 to R12 is present, THEN `comparison` is `unavailable`.
- R8. WHEN problem 2 applies, THEN `arms`, `native_errors`, the comparison inputs and problems 3 and 4 behave as PR3b defines, and `comparison` is `unavailable`.
- R9. WHEN a resend is delivered and has no copy, THEN `resend_lost` is true and counts in `lost_resends`. It is never an eligibility reason (ADR-021 decision rules 3 and 4).
- R10. Eligibility is never `eligible`, and finality stays `nonfinal`.
- R11. WHEN an existing reader (`PhaseWireCodec`, `VerifierResultCodec`, `B10SweepEvidenceCodec`) reads the produced bytes, THEN it rejects them.
- R12. WHEN the phase reads a start line without the three names, THEN `run()` records `b10_abrupt_publication_failed` at `metadata` and applies no producer Job.
- R13. Every PR3b requirement R1 to R9 and P1 to P6 still holds.

## 5. Invariants and safety

- The output stays `scientific_admissible` false with `finality` `nonfinal`. Scientific start-line readers reject trial notes (3.1).
- No producer starts after verify. Cleanup, collect, upload and destroy keep their `always()` conditions.
- No workflow dispatch, trial, `results/` or expected-outcome change (6824437250, closing line).
- No Azure credentials in any test. Kafka tests use `LocalKafkaFixture`.

## 6. Test map

Evidence sources:
- E: abrupt bytes from `B10AbruptWireProducerTests` through `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT`, as in PR3b.
- E-T: ordinary timeout bytes from `B10TimeoutEvidenceExportTests` through `PARCHWELL_B10_TIMEOUT_EVIDENCE_OUTPUT` (run `run-1`, pod `pod-first`).
- B: the PR3b builder, extended to write the timeout publication. T0 pins B to E and E-T.

| Test | Inputs | Expected |
|---|---|---|
| T0 parity | E, E-T and B | Per kind and per ordinary file, member-name sets equal |
| T1 real bytes | E with E-T, remapped by B to one run, empty window | `problems` []; `timeout` `ok` 0, `loss` the exported delivered-row count, `reported_failure` the non-delivered count, computed by the test from E-T's ledger; cross_restart `unreported_attempts` equals the kill-set size, `unreported_seen` 0 |
| T2 timeout counts | B. Timeout ledger: t1 (k1, seq 0) delivered; t2 (k2, seq 0) callback, delivered false, -192, persistence 1; t3 (k2, seq 0) delivered. Records: t1 at (3,0); t2 at (4,0); t3 at (4,1) | `timeout` `{ok 2, loss 0, phantom 1, reported_failure 0, duplicate 1, reorder 0}` |
| T3 retry rule | B. As T2 with t2 delivered true | problems [7], R7, `timeout` null |
| T4 timeout listing | B, one subcase each: no registration; a second registration `{run}-c1-r1-loadgen2`; registration job `{run}-c1-r1-loadgen-restart` | problems [7], R7, `timeout_instance_id` null |
| T5 timeout join | B, one subcase each: a timeout-instance record with key `B10/{run}/C1/1/cross_restart`; a timeout-instance record whose attempt is not in the ledger | problems [8], reasons [R3] |
| T6 foreign record | B, a record with `instance-id` `foreign` and attempt `x` | problems [8], reasons [R3]; arm counts unchanged |
| T7 warm-up join | B, both warm-up records in the window | `problems` []; no copy, pair or unreported count changes |
| T8 unreported | B, kill set [c4]; window holds original c4 at (0,10) | cross_restart `unreported_attempts` 1, `unreported_seen` 1, `unreported_unseen` 0; same_instance 0, 0, 0 |
| T9 lost resend | B, successor resend r4 delivered true, no copy | member `resend_lost` true; arm `lost_resends` 1; `eligibility_reasons` [] |
| T10 lost unknown | B, as T9 with `MaxConsumedRecords` below the window | `resend_lost` null, `lost_resends` null, problems [5], reasons [R12] |
| T11 comparison | B, four subcases: pairs in both arms; cross only; same only; none | `both`, `cross_restart_only`, `same_instance_only`, `neither` |
| T12 comparison unavailable | B, no successor directory | `comparison` `unavailable`, reasons [R9] |
| T13 native | B, one subcase each, chain rehashed: one non-fatal error with counts 1 and 0; one fatal with counts 1 and 1; one error with count 0 in termination; `native_tail` null | reasons [R5] with `comparison` not `unavailable`; [R6]; problems [2] with [R3]; problems [2] with [R3] |
| T14 child and cgroup | B, one subcase each: pid differs; `oom_kill` 0 then 1; `cgroup_after` null | problems [2], reasons [R4] |
| T15 warm-up and report | B, one subcase each: report partition differs from warm-up partition; report capture time equal to the terminal's; leader -1; post-drain report before `control_drained` | problems [2], reasons [R3] |
| T16 control | B, `control_drained.flush_returned` false, rehashed | problems [], reasons [R8], `comparison` `unavailable` |
| T17 successor | B, one subcase each: inventory entry key differs; `inventory_count` 2 for one invocation; `ready_utc` one second after `release_utc`; two invocations for one member | [4] with [R9]; [4] with [R9]; [] with [R11]; [4] with [R10] |
| T18 guard | B, one subcase each: a pre-acceptance report with `txerrs` 1; a broker absent from the inventory; a successor report with `rxerrs` 1 | problems [], reasons [R13] |
| T19 output | T2 output | Closed members of 3.4; the three existing readers reject the bytes |
| T20 start-line names | Options B10, C1, repeat 1, with `RunId` `b10d-1-1`, `b10d-01-1`, `b10d-1`, `xb10d-1-1`, `b10-1-1` | three names for the first two; `[...-loadgen, ...-loadgen2]` for the rest |
| T21 codec | B10 note, `b10d-1-1`, with 2 and with 3 names; `b10-1-1` with 3 | only the three-name `b10d` note writes; the others throw `ArgumentException` |
| T22 stale start line | B, trial start line with `[loadgen, loadgen2]` | problems [1], reasons [R1] |

Phase tests (`test_b10_abrupt.py`):
- TP1 (R1): `metadata()` with a start line carrying the three names returns the first configmap.
- TP2 (R12): `metadata()` with `[{run}-c1-r1-loadgen, {run}-c1-r1-loadgen2]` raises `ValueError('start-line source or jobs')`; through `run()`, the gap is `['b10_abrupt_publication_failed']` at `metadata`, and no `loadgen` mode is recorded.

Chart test:
- TC1: a rendered `b10-abrupt-verify` has the added env names of 3.2, all known properties, and `Verifier__ArtifactPrefix` `<run>/C1/r1/producer/`.

Each T row asserts every other enforced check still passes in that subcase, as PR3b's T8 does. The receipt records the rest of the predicate map: below, at and above each boundary.

## 7. Deployment assumptions (live-only)

"Cold" means the cluster and ephemeral layer were created in that run. "Idle" means no producer was sending when the observation was taken. Evidence from run 43217277627 is one sample, read from artifact 13945847489 (`nonfinal-result.json`, `phase-status.json`) and from 6824110241.

| ID | Assumption | Evidence | If false |
|---|---|---|---|
| LA-V1 | The verifier identity lists and reads the abrupt ledger prefix | Accepted by name, with no trial limit, in 6761524200 point 4. Run 43217277627, cold, idle: verify `complete`, both instances identified | Named failure, not a wrong count |
| LA-1 | The verifier pod consumes `document-events` over the private endpoint | Run 43217277627, cold, idle: `window_complete` true on 32 partitions; the end lines sum to 30877 records (script) | Problem 5, R12 |
| LA-2 | The records remain readable when verify runs | Same run; verify precedes destroy (workflow lines 161 and 186); `document-events` lives in `infra/ephemeral/eventhubs.tf` line 15 and is destroyed with that layer | Problem 5 |
| LA-3 | A resend reported delivered before the successor ends is below the high watermark read at verify start | Same run: one resend per arm, each consumed. Successor connection cold (no warm-up), namespace idle | A false `resend_lost` true. This now affects a published count |
| LA-4 | Verify finishes within 750 s with the added timeout reads and joins | PR3b mode `complete` in the same run, so under its 750 s deadline. The added work is not measured | Job fails, `b10_abrupt_verify_failed`, cleanup runs |
| LA-5 | The verifier identity lists and reads `<run>/C1/r1/producer/` | `Storage Blob Data Reader` on the ledger container (`infra/ephemeral/identity.tf` line 13); the same container as LA-V1. Inferred, not exercised for this prefix in a trial | Problem 7, R7 |
| LA-6 | The trial's live timeout Job evidence passes checks 16 to 18 | None live: in run 43217277627 the timeout Job was `complete` but its evidence was never read | Problem 7, R7; restart ineligible |

Not an assumption: no writer outside the trial appends. Only the `loadgen` workload holds `Azure Event Hubs Data Sender` on `document-events` (`identity.tf` line 6), unless `probe_namespace_data_roles` is true (lines 16 to 19). A foreign record gives problem 8.

LA-1 to LA-6 are listed under Q7 until Hari accepts each by name (6824437250 point 4). The acceptance of 6816733258 point 2 covered the last trial only and is not carried.

Never-ran inventory (stages not yet executed live, and the test that runs each for real): the timeout prefix listing and judgement (T1 on E-T bytes); the full-window record join (T5 to T7 on a real local Kafka window); the native, child, cgroup, warm-up and report checks (T1 and the replay A6 on real trial bytes); the guard re-evaluation (A6); the three-name start line (T20, T21, TP1, TP2).

## 8. Acceptance commands

| # | Command | Evidence |
|---|---|---|
| A1 | `./scripts/verify-dotnet.sh` | `Verification passed.`; reports include T0 to T22 |
| A2 | `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | `OK`; includes TP1 and TP2 |
| A3 | `bash infra/scripts/lifecycle/check-offline.sh` | exit 0; includes TC1 |
| A4 | `npm run check:docs` | each warning fixed or explained (docs/agents/pull-requests.md rule 8) |
| A5 | `git diff --name-only origin/main...HEAD` | a subset of 2.1, checked by script against 2.2 |
| A6 | Replay: download artifact 13945847489 outside the repository and feed its first-pod and successor files to the evidence readers through the component harness's store double, with no Kafka window. The scratch harness is not committed | The receipt records each check 4 to 13 and 21 to 28 result on these live bytes. Expected: none fails. A failure stops implementation (section 12) |

## 9. Caps and plan-check answers

Caps per implementing session, under 6824437250 point 2: 20 verification-failure root causes and 20 review-finding root causes. Exhaustion of either: `push -wip and hand off to the next session` (AGENTS.md "Before opening a pull request" rule 11).

(a) Steps whose effort scales with a number unknown at planning time:
- Window records: bounded by `MaxConsumedRecords` 372000 and `MaxConsumedBytes` (`a01-runtime-limits.json` lines 67 and 68). Run 43217277627 had 30877 (section 7).
- Timeout ledger rows: bounded by `MaxRowsPerArtifact` 372000 and `MaxRowBytes` (`charts/parchwell/values.yaml` lines 79 and 80).
- Copies per member: `MaxDiagnosticsPerCheck` 100. Journal: 4096 records. Listings: 1024 entries, 16 pages.
- Implementer root causes: the caps above.

(b) Where this plan is stricter than its parent (item plan Revision 5 and ADR-021):
- Three names only for run IDs matching T, so scientific B10 behaviour is unchanged until the separate legacy pull request of [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) Q17.
- A6 checks the readers against live bytes before the next trial.

(c) Tests that depend on code, data or behaviour outside `main` and this pull request: A6 depends on artifact 13945847489, recorded output of `main` at `78cba6e4`. E and E-T come from `main`'s LoadGen tests. Live behaviour is LA-1 to LA-6.

## 10. Sources

- ADR-021 "Permitted conditions", "Retries", "Classification", decision rules 2 to 6 and protocol rules 3, 5, 6, 9, 13 and 14 (`docs/blueprint.md`, ADR-021, on `main` at `78cba6e4`).
- `docs/operations/b10-abrupt-contract.md` lines 42, 67, 69 to 114, 131, 162 to 185 and 187 to 290; `docs/operations/verifier-contract.md` lines 5, 101, 360 to 374.
- `src/Parchwell.Verifier/VerifierHostOptions.cs` `ExpectedProducerJobNames`; `Common/Window/StartLineNoteCodec.cs` write rule; `Common/Phase/PhaseIdentity.cs`; `VerifyRun/Publication/PublicationAcceptance.cs` `Evaluate`; `VerifyRun/Judgement/RunJudgement.cs`, `OutcomeClassifier.cs`, `B10RepeatChecks.cs` `IsPermittedTimeout`, `LateWriteChecks.cs`; `VerifyRun/Abrupt/*`.
- `infra/scripts/phase/b10_abrupt.py` `metadata()` and `run()`; `charts/parchwell/templates/jobs.yaml` verifier env; `charts/parchwell/tests/test_chart.py` `VERIFIER_REQUIRED`.
- `infra/ephemeral/eventhubs.tf` line 15; `infra/ephemeral/identity.tf` lines 6, 12, 13 and 16 to 19; `.github/workflows/b10-abrupt-trial.yml` lines 161 to 186.
- `tests/Parchwell.LoadGen.Tests/Host/B10TimeoutEvidenceExportTests.cs`; `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`.
- Run 43217277627, artifact 13945847489, read 2026-11-12; trial reading 6824110241; retrospective [6798029189](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/419#issuecomment-6798029189) section 4 items 1 and 4.

## 11. Open questions for Hari

- Q1. Implementing tool. No comment names one for this pull request. PR3b used Codex (6816963301). Options: A Codex; B Claude Code. Recommendation: A, because Codex wrote the code this extends. Counterargument: Codex also reviews, so A keeps `same-vendor-review` on a protected verifier change.
- Q2. Scope of the per-arm fields. 6824437250 point 1 names reconciliation, eligibility reasons, timeout classification and the three names. It does not name the unreported counts, lost resends or comparison, which PR3b's Revision 3 removed. Options: A include them (planned); B leave them to the sweep pull request. Recommendation: A, because ADR-021 decision rule 5 makes a missing comparison an eligibility condition, and rule 13 lists these counts. Counterargument: B keeps this pull request smaller.
- Q3. How the start line knows it is a trial. Options: A the run-ID pattern T (planned; no chart or phase render change); B a new verifier option set by the phase for the trial's start-line Job, with chart and schema changes. Recommendation: A, because T already separates trial from science in the chart schema and contract line 42. Counterargument: A is an implicit switch inside a shared property; B makes the choice visible in the rendered Job.
- Q4. Timeout publication acceptance without editing a C5 target. `PublicationAcceptance.Evaluate` rejects a B10 start line unless it carries `[loadgen, loadgen2]`. Options: A a reader in `VerifyRun/Abrupt/` that applies the same checks with 3.1's names (planned); B change `PublicationAcceptance` for T, which needs a main-only C5 mutation run after merge (6705801800 point 5). Recommendation: A. Counterargument: A duplicates acceptance logic that can drift from the scientific path.
- Q5. Guard re-evaluation. 6761524200 point 3 chose binding-only checks for PR3b "in this slice" and requires re-evaluation before scientific admission. Options: A re-evaluate here with new reason 13 `b10_restart_guard_failed` (planned; check 28); B keep binding-only and defer to the sweep pull request. Recommendation: A, so the reasons this mode publishes are the ones science will use. Counterargument: B avoids a new literal before the five-repeat judge exists.
- Q6. Reason literals 1 to 12. 6761542603 Q2 approved them under PR3b Revision 1's conditions, which named seals, signals and selections the redesign removed. Options: A keep the literals with the conditions of 3.6 (planned); B rename or merge. Recommendation: A. Counterargument: reason 3 now covers eleven checks and two problems (3.6, counted by script), so it says little about which one failed; `problem_details` carries that.
- Q7. Accept by name, for the next diagnostic trial only, LA-1 to LA-6 of section 7.

### 11.1 Earlier answers (6761542603 rule)

- Still apply, unchanged: Q3 (eligibility literal), Q5 (Drain overload), Q8 (non-fatal native error, 6761524200 point 2), Q10 (mode, path, exit, envelope), Q12 (pair product, smallest witness) and Q4 (now LA-V1).
- Apply with changed conditions: Q2 (reason literals); Q6 asks.
- Void: Q6 of Revision 1 (the selected original no longer exists; ADR-021 rule 14 now fixes the unreported counts), Q9 and Q11 (subjects gone).
- Q7 (guard): 6761524200 point 3 was for PR3b; Q5 asks.
- Q13 / 6762055013: answered by 6816117386 point 2; the added fields of 3.4 are diagnostic extras of this nonfinal object.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change:
- a contract of section 3 or a requirement of section 4;
- the scope of section 2, including any C5 target;
- a pinned input of 3.3;
- or when A6 shows a check failing on live bytes.

Hitting a section 9 cap pushes `-wip` and hands off to the next session. A revision of this plan gets one fresh `plan-checker` when it is design-changing under docs/agents/factory.md "Item lifecycle" step 1, and no other pre-check (6824437250 point 5).
