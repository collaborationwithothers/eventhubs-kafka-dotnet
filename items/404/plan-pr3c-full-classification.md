# PR3c full classification contract-grade plan

Revision 2. Issue 404, pull request short name `PR3c full classification`. Status: not approved. Implementing tool: Codex ([6826128120](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6826128120) point 1). Hari merges.

This pull request completes the diagnostic verifier mode `b10-abrupt-verify`. It adds full reconciliation of the three trial instances, classification of the C1 timeout cohort, the per-arm counts ADR-021 publishes, and an eligibility literal with ordered reasons. It also makes the diagnostic trial's start line carry the three C1 Job names. The result stays nonfinal and diagnostic: no sweep, no verdict, nothing admissible to science.

## 0. Changes

Since Revision 1 (`b82e50c2`), after plan check [6825023203](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6825023203) and decisions [6826128120](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6826128120), [6827180600](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827180600) and [6827265635](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827265635):
- Q1 to Q6 and check B3 are decided by 6826128120. Their text moves from section 11 into the sections they govern.
- B1: check 18 runs only without problems 1 and 5, receives only records that passed check 19, and uses the mode's own window assessments. The retry clause applies only when an earlier attempt exists. T5, T10 and T22 now follow.
- B2: check 28 pins the successor's expected configuration (contract line 38) and replaces check 14's manifest part with its capabilities part. Broker identity is the `brokers` dictionary key.
- B3 (decision point 6): flush, queue and post-drain failures are problem 2 with R3 through check 23; R8 covers only missing control terminals. T16 changes.
- B4 (6827180600, option B): under problem 2 the window is still drained and the timeout cohort classified. The problem-2 row is restated in 3.4.
- B5: reason 3 names check 4 for the first pod.
- LA-3 (6827265635 point 2): new check 29 re-reads the high watermarks when a resend looks lost; problem 9 makes an advanced or unreadable end line visible. Residual LA-3b and the LA-4 extension go to R2-Q1.
- LA-1, LA-2, LA-4, LA-5 and LA-6 are accepted by name for the next diagnostic trial only (6827265635 point 1).
- P2-B and P2-C: 3.1 bullets 4 and 5 corrected. P2-A stays apply in implementation (section 6).

Revision 1 was the first revision. There is no earlier plan for this pull request. The working name PR3c was first used in [split decision 6750021224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750021224) item 3 for the trial read-back. That work merged inside PR3b ([6815988248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6815988248) point 2). [Decision 6824437250](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6824437250) point 1 reuses the name for this pull request.

## 1. Goal and authority

Goal: after the next diagnostic trial, one nonfinal object answers, for the one C1 repeat, whether each instance's evidence reconciles, what the timeout cohort's six counts are, what each arm's unreported, pair and lost-resend counts are, and whether the repeat would be eligible pending the sweep, with every reason it is not.

Authority, in creation order, under docs/agents/factory.md "Item lifecycle" step 3:
- The issue body and the decisions listed in `items/404/state.md` on `main` at `78cba6e467c7bd0333f131e159f4989b6cfee828`, which covers comment 6816963301. They include ADR-021 on `main` and the PR3b approval [6816963301](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816963301).
- [6761524200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761524200) and [6761542603](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761542603): answers to PR3b Revision 1 (`b4ece136`). Section 11.1 records which still apply here. 6761542603 states that a changed literal, path or option returns to Hari.
- [6816117386](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816117386) point 1: full reconciliation and eligibility follow PR3b in a later pull request of the same mode, before any science. Point 2: the diagnostic object may carry the per-message table and extras; the later final sweep projection drops them.
- [6816733258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816733258) point 1: the two-name start-line gap of [6787849492](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787849492) point 1 extends to this pull request.
- [6823517462](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6823517462): one trial, run 43217277627, now run. It authorizes no further trial.
- [6824437250](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6824437250): this plan. Its points 1 to 5 are applied, not restated: point 1 in sections 2 and 3, point 2 in section 9, point 3 in section 2, point 4 in section 7, point 5 in section 12.
- [6825023203](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6825023203): the Revision 1 plan check, evidence only.
- [6826128120](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6826128120): Q1 to Q6 and check B3, each option A.
- [6827180600](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827180600): check B4, option B.
- [6827265635](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827265635): Q7. Point 1 accepts five assumptions; point 2 requires a mechanism for LA-3.

Catch-up: 292 comments, 3 pages, at the Revision 2 refresh. New since the state file: 6823517462, 6824110241 (trial evidence), 6824437250, 6824766149 (this plan's pointer), 6825023203, 6825044842 (the Revision 1 handoff), 6826128120, 6827180600 and 6827265635, each read in full. No comment the state file lists was edited or deleted. Issue `updated_at` 2026-11-12T13:06:31Z. Every `## Decision` and `## Approval` comment whose body names PR3b, PR3c, classification, eligibility, reconciliation, the timeout cohort, the three names or this mode was read in full (30 comments), plus the PR3b Revision 1 plan at `b4ece136` and Revision 4 at `aedb21c0`.

What PR3b defers to this pull request, read on `main` at `78cba6e4`: `docs/operations/b10-abrupt-contract.md` line 227 lists native-error and fatal coverage, cgroup and child identity, report leader binding, statistics and capabilities, warm-up ordering beyond check 8, the timeout cohort, registrations of other instances, eligibility and the sweep. Line 67 holds the two-name gap. `items/404/pr3b-receipt.md` line 29 adds the successor inventory semantics. PR3b Revision 3 also removed Revision 2's comparison (plan file at `aedb21c0`, line 26).

## 2. Scope

Decision 6824437250 point 1 sets the scope: full reconciliation, eligibility reasons, timeout-cohort classification and the three C1 start-line names. The sweep and the final sweep projection are not in it. The per-arm unreported, lost-resend and comparison fields are in it (6826128120 point 2).

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
- Every file in `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`'s `mutate` list, including `PublicationAcceptance.cs`, `B10RepeatChecks.cs`, `B10IntakeChecks.cs` and `B10PhaseRegistrationReader.cs`, and that config (6826128120 point 4).
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
- Existing problem-2 assertions that change under [6827180600](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827180600): `end_line` null and `window_complete` false in `B10AbruptVerifyTests.cs` `AssertFirstUnavailable` (line 364) and in `B10AbruptVerifyCoverageTests.cs` (lines 83, 84 and 163). Each becomes the problem-2 row of 3.4: `end_line` equal to the high watermarks read, and `window_complete` true for a complete drain. `arms` null and the problem-2 literal stay asserted. This is the decided contract change, not a loosening; the receipt lists each changed assertion.
- Mutation: no C5 target changes, so no mutation run is part of this pull request.

## 3. External contracts

### 3.1 Three C1 start-line names (trial only)

Pattern T is `^b10d-[0-9]+-[0-9]+$`, the trial run-ID pattern of `values.schema.json` line 931 and contract line 42.

- WHEN scenario is `B10` and `RunId` matches T, the derived expected producer Job names are exactly `[{RunId}-{phase}-r{repeat}-loadgen, {RunId}-{phase}-r{repeat}-loadgen-restart, {RunId}-{phase}-r{repeat}-loadgen-successor]`, lowercase phase. Otherwise they stay as on `main`: `[...-loadgen, ...-loadgen2]` for B10, one name for other scenarios.
- The start-line note's `expected_job_names` carries that list. The codec writes a B10 note only with exactly three names when its run ID matches T, and exactly two otherwise.
- The phase's `metadata()` requires `expected_job_names` equal to `[mode_job('loadgen'), mode_job('loadgen-restart'), mode_job('loadgen-successor')]` for repeat 1. Any other value raises `ValueError('start-line source or jobs')`, as today.
- Mode `b10-abrupt-verify` check 2 expects the list derived by the first bullet.
- Scientific readers stay unchanged. `PublicationAcceptance` and `JudgeHandler` compare a B10 start line with `[loadgen, loadgen2]`; `B10SweepHandler` compares it with the bound producers' Job names (`B10SweepHandler.cs` lines 198 and 199). Each therefore rejects a three-name trial note against two-producer science.

The run-ID pattern is the trial recognition Hari chose (6826128120 point 3).

### 3.2 Mode and options

- `Verifier:Mode` `b10-abrupt-verify`, fixed options, output path, create-only publication and exit rule are unchanged from contract lines 193 to 203.
- Required mode options gain `ScenarioRoot`, `ArtifactPrefix`, `MaxRowBytes`, `MaxRowsPerArtifact`, `MaxInventoryEntries`, `MaxInputEntries`, `MaxNativeReportBytes` and `MaxBrokerEntries`, plus the scenario-parsing names `verify` receives (`VERIFIER_SCENARIO` in `test_chart.py`). The chart renders each from the source it uses for `verify`. `ArtifactPrefix` must equal `<RunId>/C1/r1/producer/`; any other value exits 1 before any read.

### 3.3 Inputs and enforced checks

Inputs fixed on `main`: the abrupt formats of `docs/operations/b10-abrupt-contract.md` lines 69 to 185, and the ordinary producer publication of `docs/operations/verifier-contract.md` as `verify` reads it. A change to either is a stop-rule event.

PR3b checks 1 to 15 (contract lines 209 to 223) stay, except check 2, which expects the names of 3.1. New checks follow. A bracketed number is the problem of 3.5; an R number is the eligibility reason of 3.6.

16. Timeout listing. List `<RunId>/C1/r1/producer/`. It holds exactly one directory, `<pod>/`, whose `registration.json` names Job `{RunId}-c1-r1-loadgen`, no predecessor, run `RunId`, `B10`, `C1`, phase `C1`, repeat 1 and source `VerifierCommit`. Any other registration, object outside that directory, or incomplete listing fails. [7] R7. The directory name is the timeout instance ID.
17. Timeout publication. A reader in `VerifyRun/Abrupt/` applies every check `PublicationAcceptance.Evaluate` applies to one producer instance, except that the start line's expected names are compared with 3.1's list (6826128120 point 4). [7] R7.
18. Timeout judgement. Evaluated only when problems 1 and 5 are absent; otherwise not evaluated, adding no problem, with `timeout` null. The instance passes per-instance checks 1 to 6 and 12 to 27 of `docs/operations/verifier-contract.md`, as `RunJudgement.Evaluate` applies them to a B10 instance, with complete-valid status. For checks 7 to 11 it receives the mode's own window assessments for the shared window (PR3b checks 2 and 14), which pass whenever problems 1 and 5 are absent. Its records are only those that passed check 19. ADR-021 "Retries" also applies: for each inventory attempt in invocation order, when an earlier attempt with the same key and sequence exists, the latest such attempt has a row for which `B10RepeatChecks.IsPermittedTimeout` is true (as `B10RepeatChecks.cs` lines 79 to 85 apply it). [7] R7.
19. Timeout records. Evaluated before check 18, whenever the window was drained and check 16 passed. Every in-window record whose `instance-id` header equals the timeout instance ID joins exactly one timeout ledger row by `attempt-id`, with equal key and `sequence`, and its key is not an arm key. A failing record gives [8] R3 and is not passed to check 18. When check 16 or 17 fails, these records are held out and add no problem.
20. Probe and warm-up records. Evaluated when the window was drained and problem 2 is absent. Every other in-window record joins one of: a first-instance `prepared` attempt (instance, attempt, key, `sequence` and value hash equal to its `payload_sha256`); a first-instance warm-up (attempt `{pod}-{arm}-warmup`, the arm key, `sequence` -1, value hash equal to its `warmup_prepared` `payload_sha256`); or a successor invocation (instance, attempt, key, `sequence` and hash). [8] R3. When problem 3 or 4 applies, records whose instance equals the directory name check 3 found for the successor Job are held out and add no problem. Under problem 2, records that are not the timeout instance's are held out and add no problem.
21. Native capture. `native_error` records have `capture_sequence` 1..n without gaps. Their count equals `termination.native_error_count`, and those with `is_fatal` true equal `native_fatal_count`. `native_tail` is `unknown_due_to_intended_kill`. [2] R3. A record with `is_fatal` true adds R6. A record with `is_error` true and `is_fatal` false adds R5 (6761524200 point 2).
22. Child and cgroup. `termination.child.pid`, `start_time_ticks` and `boot_id` equal `child_started.data`; `parent_observed_start_time_ticks` equals `start_time_ticks`. `cgroup_before` and `cgroup_after` are non-null and equal in `boot_id`, `cgroup_path`, `directory_identity` and `oom_kill`, and their `boot_id` equals the child's. [2] R4.
23. Warm-up order. The journal meets the warm-up rules of contract lines 102 to 108 for both arms. [2] R3. Line 105 requires the control arm complete before the cross-restart `warmup_prepared`, under the condition `B10AbruptJournal.cs` `ControlCompleteBefore` applies (lines 166 and 256 to 277): `flush_returned` true, `out_queue_length` 0, `post_drain_report` after `control_drained`, and a terminal, earlier in the journal, for every accepted control attempt. A journal that fails any of these therefore fails this check (6826128120 point 6).
24. Report binding. Each arm's `pre_acceptance_report` binds that arm's warm-up attempt ID and its `warmup_terminal.partition`; its `report_capture_utc` is strictly later than that terminal's `observed_utc`; and its report bytes satisfy `LeaderUp(report, "document-events", partition)` of contract line 131. The `post_drain_report` follows `control_drained`. [2] R3.
25. Control terminals. The journal holds a terminal record for every accepted control original and control resend. Failure adds R8 (6826128120 point 6). Flush, queue and post-drain failures are check 23's, not this check's. Because check 23 also requires those terminals, a journal failing this check also fails check 23, so R8 appears with problem 2 and R3, never alone.
26. Successor inventory. `manifest.json` (`cohort_inventory`) has `instance_id` the successor, `cohort` `cross_restart`, `bookkeeping_complete` true and `inventory_count` equal to the invocation count. Its entries equal the invocations in order: `attempt_id`, `predecessor_attempt_id`, `key`, `sequence` and `payload_sha256`, with `invocation_ordinal` 0..n-1. [4] R9.
27. Successor release. Each invocation's `invocation_utc` is not earlier than `release_utc`. `ready_utc` later than `release_utc` adds R11.
28. Guard (6826128120 point 7). Failure adds R13 only.
    - First instance: the three bound reports parse with `NativeStatisticsReader`. Every `brokers` dictionary key they hold, the broker identity of `docs/operations/verifier-contract.md` line 370, is in the start line's broker inventory, and every broker's `txerrs` and `rxerrs` is 0.
    - Successor: its `statistics.jsonl`, `request-lifecycle.jsonl` and `capabilities.json` pass per-instance checks 15 to 26 as `verify` applies them, with ADR-021's B10 permitted conditions (nonzero `req_timeouts` and `txretries` permitted, ADR-021 "Permitted conditions"). Check 14 is replaced by its capabilities part only: `guard_observations_complete` true and `guard_observations` holding only the two markers line 362 permits. Its manifest part does not apply, because the successor manifest is a `cohort_inventory` (contract line 185). Check 27 is replaced by: the captured resolved configuration equals C1's scenario-resolved configuration with exactly one difference, `message.timeout.ms` with value `"300000"`, unchanged baseline metadata and `override` null (contract line 38, from ADR-021 protocol rule 1). `max.in.flight` and `enable.idempotence` keep their C1 values and scenario override provenance.
29. End-line re-read (6827265635 point 2). Evaluated after the drain when problems 1 and 5 are absent and at least one member has a known resend with `resend_terminal.delivered` true and `resend_copies` 0. The handler reads the high watermarks of every start-line partition once more, with the existing `WatermarkTimeout` (00:00:10, `charts/parchwell/values.yaml` line 65). WHEN any partition's re-read high watermark is greater than its end line, or the read fails or times out, THEN problem [9] is recorded, and every such member's `resend_lost` and its arm's `lost_resends` are null. WHEN every re-read equals the end line, THEN those members keep `resend_lost` true. No reason is added: an unknown loss is neither loss nor eligibility evidence, and the later late-write sweep from the end line covers the advanced range (ADR-021 "Late-write scan").

Resend counts: check 10 or check 12 failing only because a member has other than one resend gives R10 instead of R3 or R9.

### 3.4 Output `nonfinal_result`

The envelope, identity and the PR3b `data`, arm and member members stay as in contract lines 229 to 268. Added members:

`data`:
- `timeout_instance_id`: string, or null when check 16 fails.
- `timeout`: `{ok, loss, phantom, reported_failure, duplicate, reorder}`, from `OutcomeClassifier.Classify` over the timeout ledger rows and the records that passed check 19; null when problem 1, 5 or 7 applies. Problem 2 does not null it (6827180600).
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

Null rules: unknown is never zero. `unreported_seen`, `unreported_unseen`, `lost_resends` and `resend_lost` are null when the copy fields are null under contract lines 281 to 288. `lost_resends` and `resend_lost` are also null for `cross_restart` under problems 3 and 4, and for the members check 29 names under problem 9.

Problem-2 row, replacing contract line 285 under 6827180600 (option B): `arms` is null and problems 3 and 4 are not evaluated, as before. The window is drained as on the valid path: `start_line` follows check 2, `end_line` is as read, and `window_complete` follows the drain (problem 5 when it is incomplete). Checks 16 to 19 run, so `timeout` and `timeout_instance_id` are computed. Check 20 holds out every record that is not the timeout instance's. `native_errors` is null and `comparison` is `unavailable`. The drain is the same drain the valid path runs, so it adds no work beyond LA-4's valid-path bound (section 7).

### 3.5 Problem literals

PR3b's six stay first, in order. Added:
7. `b10_abrupt_verify_timeout_evidence_invalid`
8. `b10_abrupt_verify_record_unjoined`
9. `b10_abrupt_verify_end_line_advanced` (check 29: the re-read found a later high watermark, or failed)

### 3.6 Eligibility reason literals

In this order. 1 to 12 are the literals of PR3b Revision 1 section 4.4 (`b4ece136`), approved under 6761542603 Q2, with the conditions below (6826128120 point 5). 13 is new (6826128120 point 7).

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
- R4. WHEN check 16 or 17 fails, or check 18 is evaluated and fails, THEN problem 7 and R7 are recorded, `timeout` is null, `timeout_instance_id` follows 3.4, and the arms are computed as without the problem.
- R5. WHEN a record fails check 19 or 20, THEN problem 8 and R3 are recorded, every count is still computed, and a record failing check 19 does not reach check 18, so problem 7 is not added for it.
- R6. WHEN a check adds a reason without a problem (R5, R6, R11, R13), THEN no field becomes null.
- R7. WHEN any of R1 to R4 or R8 to R12 is present, THEN `comparison` is `unavailable`.
- R8. WHEN problem 2 applies, THEN `arms` is null, problems 3 and 4 are not evaluated, `native_errors` is null and `comparison` is `unavailable`, AND the window is drained and the timeout cohort classified under the problem-2 row of 3.4 (6827180600).
- R9. WHEN a resend is delivered and has no copy, and check 29's re-read equals every end line, THEN `resend_lost` is true and counts in `lost_resends`. It is never an eligibility reason (ADR-021 decision rules 3 and 4).
- R14. WHEN check 29's re-read is greater than any end line, or fails, THEN problem 9 is recorded and the affected `resend_lost` and `lost_resends` are null. WHEN no member qualifies under check 29, THEN no re-read happens.
- R10. Eligibility is never `eligible`, and finality stays `nonfinal`.
- R11. WHEN an existing reader (`PhaseWireCodec`, `VerifierResultCodec`, `B10SweepEvidenceCodec`) reads the produced bytes, THEN it rejects them.
- R12. WHEN the phase reads a start line without the three names, THEN `run()` records `b10_abrupt_publication_failed` at `metadata` and applies no producer Job.
- R13. Every PR3b requirement R1 to R9 and P1 to P6 still holds, except PR3b R4's problem-2 window column ("not drained"), which R8 replaces.

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
| T9 lost resend | B, successor resend r4 delivered true, no copy; the re-read of check 29 equals every end line | member `resend_lost` true; arm `lost_resends` 1; `problems` []; `eligibility_reasons` []; exactly one re-read |
| T10 lost unknown | B, as T9 with `MaxConsumedRecords` below the window | `resend_lost` null, `lost_resends` null, problems [5], reasons [R12] |
| T11 comparison | B, four subcases: pairs in both arms; cross only; same only; none | `both`, `cross_restart_only`, `same_instance_only`, `neither` |
| T12 comparison unavailable | B, no successor directory | `comparison` `unavailable`, reasons [R9] |
| T13 native | B, one subcase each, chain rehashed: one non-fatal error with counts 1 and 0; one fatal with counts 1 and 1; one error with count 0 in termination (input and expected values as P2-A of 6825023203, apply in implementation); `native_tail` null | reasons [R5] with `comparison` not `unavailable`; [R6]; problems [2] with [R3]; problems [2] with [R3] |
| T14 child and cgroup | B, one subcase each: pid differs; `oom_kill` 0 then 1; `cgroup_after` null | problems [2], reasons [R4] |
| T15 warm-up and report | B, one subcase each: report partition differs from warm-up partition; report capture time equal to the terminal's; leader -1; post-drain report before `control_drained` | problems [2], reasons [R3] |
| T16 control | B, rehashed, one subcase each: `control_drained.flush_returned` false; `out_queue_length` 1; one control resend with no terminal | problems [2] with [R3]; [2] with [R3]; [2] with [R3, R8]. `comparison` `unavailable` in each |
| T17 successor | B, one subcase each: inventory entry key differs; `inventory_count` 2 for one invocation; `ready_utc` one second after `release_utc`; two invocations for one member | [4] with [R9]; [4] with [R9]; [] with [R11]; [4] with [R10] |
| T18 guard | B, one subcase each: a pre-acceptance report with `txerrs` 1; a broker absent from the inventory; a successor report with `rxerrs` 1; successor `message.timeout.ms` value `"100"`; successor `guard_observations_complete` false | problems [], reasons [R13]. With E's successor capabilities (value `"300000"`, override null) no R13 |
| T19 output | T2 output | Closed members of 3.4; the three existing readers reject the bytes |
| T20 start-line names | Options B10, C1, repeat 1, with `RunId` `b10d-1-1`, `b10d-01-1`, `b10d-1`, `xb10d-1-1`, `b10-1-1` | three names for the first two; `[...-loadgen, ...-loadgen2]` for the rest |
| T21 codec | B10 note, `b10d-1-1`, with 2 and with 3 names; `b10-1-1` with 3 | only the three-name `b10d` note writes; the others throw `ArgumentException` |
| T22 stale start line | B, trial start line with `[loadgen, loadgen2]` | problems [1], reasons [R1], `timeout` null; check 18 not evaluated |
| T23 problem 2 drains | B, T2's timeout data, first-pod `abrupt-manifest.json` hash wrong | problems [2], reasons [R3]; `arms` null; `timeout` as T2; `end_line` non-null; `window_complete` true; `comparison` `unavailable` |
| T24 end line advanced | B, as T9, one record produced to partition 0 between the drain and the re-read | problems [9]; `resend_lost` null; `lost_resends` null; `eligibility_reasons` [] |
| T25 re-read fails | B, as T9, re-read times out through the Kafka session seam | problems [9]; `resend_lost` null |
| T26 no re-read | B, T2 inputs with every resend consumed | no re-read; `problems` [] |

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
| LA-3 | Not accepted (6827265635 point 2). Replaced by check 29 and residual LA-3b | | |
| LA-3b | A resend that Event Hubs acknowledged before the successor Job ended is reflected in a high-watermark read taken after the drain. Check 29 relies on it only to keep `resend_lost` true when the re-read equals the end line | Run 43217277627, cold successor connection (no warm-up), idle namespace: each arm's acknowledged resend was below the high watermark read at verify start and was consumed. One sample. Event Hubs documentation on watermark visibility was not read for this plan | A false `resend_lost` true with no problem 9. Open: R2-Q1 |
| LA-4b | Verify still finishes within 750 s with check 29's one extra watermark read, bounded by `WatermarkTimeout` 10 s (policy, not measured). The problem-2 drain of 6827180600 is the valid-path drain, so it adds nothing beyond LA-4 | As LA-4 | As LA-4. Open: R2-Q1 |
| LA-4 | Verify finishes within 750 s with the added timeout reads and joins | PR3b mode `complete` in the same run, so under its 750 s deadline. The added work is not measured | Job fails, `b10_abrupt_verify_failed`, cleanup runs |
| LA-5 | The verifier identity lists and reads `<run>/C1/r1/producer/` | `Storage Blob Data Reader` on the ledger container (`infra/ephemeral/identity.tf` line 13); the same container as LA-V1. Inferred, not exercised for this prefix in a trial | Problem 7, R7 |
| LA-6 | The trial's live timeout Job evidence passes checks 16 to 18 | None live: in run 43217277627 the timeout Job was `complete` but its evidence was never read | Problem 7, R7; restart ineligible |

Not an assumption: no writer outside the trial appends. Only the `loadgen` workload holds `Azure Event Hubs Data Sender` on `document-events` (`identity.tf` line 6), unless `probe_namespace_data_roles` is true (lines 16 to 19). A foreign record gives problem 8.

LA-1, LA-2, LA-4, LA-5 and LA-6 are accepted by name, as stated above, for the next B10 diagnostic trial only (6827265635 point 1; 6824437250 point 4). LA-3b and LA-4b are open under R2-Q1. The acceptance of 6816733258 point 2 covered the last trial only and is not carried.

Never-ran inventory (stages not yet executed live, and the test that runs each for real): the timeout prefix listing and judgement (T1 on E-T bytes); the full-window record join (T5 to T7 on a real local Kafka window); the native, child, cgroup, warm-up and report checks (T1 and the replay A6 on real trial bytes); the guard re-evaluation (A6); the three-name start line (T20, T21, TP1, TP2); the end-line re-read (T24 and T25 on a real local Kafka broker).

## 8. Acceptance commands

| # | Command | Evidence |
|---|---|---|
| A1 | `./scripts/verify-dotnet.sh` | `Verification passed.`; reports include T0 to T26 |
| A2 | `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | `OK`; includes TP1 and TP2 |
| A3 | `bash infra/scripts/lifecycle/check-offline.sh` | exit 0; includes TC1 |
| A4 | `npm run check:docs` | each warning fixed or explained (docs/agents/pull-requests.md rule 8) |
| A5 | `git diff --name-only origin/main...HEAD` | a subset of 2.1, checked by script against 2.2 |
| A6 | Replay: download artifact 13945847489 outside the repository and feed its first-pod and successor files to the evidence readers through the component harness's store double, with no Kafka window. The scratch harness is not committed | The receipt records each check 4 to 13 and 21 to 28 result on these live bytes. Expected: none fails, including check 28 (6826128120 point 7). Read by script for this revision: the successor's `message.timeout.ms` is `"300000"` with `override` null, `enable.idempotence` and `max.in.flight` carry C1 overrides, `linger.ms` is the baseline `"5"`, and `guard_observations_complete` is true. A failure stops implementation (section 12) |

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

(c) Tests that depend on code, data or behaviour outside `main` and this pull request: A6 depends on artifact 13945847489, recorded output of `main` at `78cba6e4`. E and E-T come from `main`'s LoadGen tests. Live behaviour is LA-1, LA-2, LA-3b, LA-4, LA-4b, LA-5 and LA-6.

## 10. Sources

- ADR-021 "Permitted conditions", "Retries", "Classification", decision rules 2 to 6 and protocol rules 3, 5, 6, 9, 13 and 14 (`docs/blueprint.md`, ADR-021, on `main` at `78cba6e4`).
- `docs/operations/b10-abrupt-contract.md` lines 42, 67, 69 to 114, 131, 162 to 185 and 187 to 290; `docs/operations/verifier-contract.md` lines 5, 101, 360 to 374.
- `src/Parchwell.Verifier/VerifierHostOptions.cs` `ExpectedProducerJobNames`; `Common/Window/StartLineNoteCodec.cs` write rule; `Common/Phase/PhaseIdentity.cs`; `VerifyRun/Publication/PublicationAcceptance.cs` `Evaluate`; `VerifyRun/Judgement/RunJudgement.cs`, `OutcomeClassifier.cs`, `B10RepeatChecks.cs` `IsPermittedTimeout`, `LateWriteChecks.cs`; `VerifyRun/Abrupt/*`.
- `infra/scripts/phase/b10_abrupt.py` `metadata()` and `run()`; `charts/parchwell/templates/jobs.yaml` verifier env; `charts/parchwell/tests/test_chart.py` `VERIFIER_REQUIRED`.
- `infra/ephemeral/eventhubs.tf` line 15; `infra/ephemeral/identity.tf` lines 6, 12, 13 and 16 to 19; `.github/workflows/b10-abrupt-trial.yml` lines 161 to 186.
- `tests/Parchwell.LoadGen.Tests/Host/B10TimeoutEvidenceExportTests.cs`; `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`.
- Run 43217277627, artifact 13945847489, read 2026-11-12; trial reading 6824110241; retrospective [6798029189](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/419#issuecomment-6798029189) section 4 items 1 and 4.

## 11. Open questions for Hari

- R2-Q1. Accept by name, for the next B10 diagnostic trial only, LA-3b (an acknowledged resend is reflected in a high-watermark read taken after the drain) and LA-4b (the 750 s verify deadline also covers check 29's one re-read, at most 10 s), as section 7 states them. These are the residual assumptions of the check 29 mechanism that 6827265635 point 2 asked for. Options: A accept both; B reject LA-3b and require partition and offset in the successor ledger row, a LoadGen evidence-contract change outside this pull request's scope. Recommendation: A, because a false LA-3b leaves `resend_lost` true only when Event Hubs acknowledged an append that no later watermark read reflects, and the later late-write sweep rescans from the end line. Counterargument: until the sweep exists, a diagnostic reader of this object cannot tell such a case from a real loss.

Decided, and no longer open: Q1 to Q6 and check B3 ([6826128120](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6826128120)); check B4 ([6827180600](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827180600)); Q7 except LA-3 ([6827265635](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827265635) point 1).

### 11.1 Earlier answers (6761542603 rule)

Q numbers here are those of PR3b Revision 1 (`b4ece136`).

- Still apply, unchanged: Q3 (eligibility literal), Q5 (Drain overload), Q8 (non-fatal native error, 6761524200 point 2), Q10 (mode, path, exit, envelope), Q12 (pair product, smallest witness) and Q4 (now LA-V1).
- Apply with changed conditions: Q2 (reason literals), as 6826128120 point 5 decides.
- Void: Q6 of Revision 1 (the selected original no longer exists; ADR-021 rule 14 now fixes the unreported counts), Q9 and Q11 (subjects gone).
- Q7 (guard): 6761524200 point 3 was for PR3b; 6826128120 point 7 decides re-evaluation here.
- Q13 / 6762055013: answered by 6816117386 point 2; the added fields of 3.4 are diagnostic extras of this nonfinal object.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change:
- a contract of section 3 or a requirement of section 4;
- the scope of section 2, including any C5 target;
- a pinned input of 3.3;
- or when A6 shows a check failing on live bytes.

Hitting a section 9 cap pushes `-wip` and hands off to the next session. A revision of this plan gets one fresh `plan-checker` when it is design-changing under docs/agents/factory.md "Item lifecycle" step 1, and no other pre-check (6824437250 point 5).
