# B10 scientific three-Job path contract-grade plan

Revision 5. Issue 404, pull request short name `B10 scientific three-Job path`. Status: not approved. Implementing tool: Codex ([6846521676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846521676) point 1). Hari merges, because the change touches `Parchwell.Verifier*/` (`.github/CODEOWNERS`).

This pull request moves the scientific B10 path that `scenario.yml` runs to ADR-021's Job shape: one ordinary producer Job per C0 repeat, and three sequential producer Jobs per C1 repeat. Each scientific repeat then publishes the per-repeat evidence that the later final-judgement pull request reads. It authorizes no dispatch, verdict, result or expected outcome, and leaves the phase sweep and the five-repeat judgement to that later pull request.

## 0. Changes

Since Revision 4 (`4ba489ab`), after plan check [6847030526](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6847030526):
- B1: the C0 branch of each of the three codecs accepts one producer or `main`'s legacy pair, so the frozen `B10SweepHandler` and `JudgeHandler` and their existing C0 tests keep working. The one-registration rule of the scientific path stays in `VerifyRunHandler`, `B10IntakeChecks` and `B10PhaseRegistrationReader` (3.8, R19, T32). Decision 6846854445 point 1 asks only that one-producer C0 branches be added.
- P2-B to P2-D folded: literal failure names in T9g and T9h; 3.8 cites `VerifierResultCodec.EligibleShape`; row 1 names the deadline path. P2-A stays apply in implementation.

Since Revision 3 (`86e39b00`), under decision [6846854445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846854445) and plan check [6846805964](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846805964):
- B2 (decision point 1): C0 `verify` keeps `main`'s nonfinal B10 shape; 2.2 adds one-producer C0 branches in `PhaseWireCodec.cs`, `VerifierResultCodec.cs` and `B10ProducerObservationsCodec.cs`, C1 branches unchanged (3.8, R14, T23, T27, T32).
- Q10 (decision point 2): option A, the C0 gap `b10_c0_final_judgement_unavailable`. Section 11 now states `None`.
- B1: 3.2 states that a phase stop wins over a repeat end, and rows 1 and 3 name every trial path that sets `safety_stop` (T9g, T9h).
- P2-B and P2-C folded into T2 and T28. P2-A stays apply in implementation (6846854445).

Since Revision 2 (`0fb9caa4`), under decision [6846521676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846521676) and plan check [6846375050](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846375050):
- Q1 to Q9 are decided (6846521676 points 1 to 9). Their text moves out of section 11 into the sections they govern.
- Q2 option A: C0 moves to one ordinary producer Job in this pull request. New sections 3.1 (C0 names), 3.2 (C0 order), 3.8 (C0 verify), 2.2 and 2.3 (freeze lifted on `B10IntakeChecks.cs`, `B10PhaseRegistrationReader.cs` and `PublicationAcceptance.cs`), 2.4 (post-merge mutation run), R13 to R17 and T22 to T30.
- B1: after an accepted first publication, repeat N's verify Job runs even when the successor step or its check sets `safety_stop`; then no Job of a later repeat runs (3.2). T9e added.
- B2 and B3: every producer render failure and every failed pod read now follows the trial's mapping, Job by Job (3.2 table "Ends"). T9b and T9c follow the trial.
- B4: 2.4 lists the existing scientific tests this plan changes, with literal replacement values, and a legacy-fixture rule for tests of frozen legacy readers.
- P2-B to P2-E folded into 3.5, 3.4, 2.1 and the test map. P2-A (`earlier_repeat_records` null when the window was not drained) stays apply in implementation (6846521676, "For Revision 3").
- Q5: the live assumptions are accepted for planning only and for no run; one governed diagnostic run of the scientific path is required after both pull requests merge (6846521676 point 5). Section 7 records this.
- Q6: the measured trial step timings Hari was given are recorded in section 9.
- New open question Q10: the C0 judge gap, a consequence of Q2 option A.
- Section 1 records the full reading of 6827265635 and 6827960705.
- Decision [6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008) point 9 chooses item (b)'s Q9 option B, which asks this pull request for a create-only `WorkerReceipt` for the scientific per-repeat C1 verifier. 3.6 now pins it; R18 and T31 test it. Its sign-in scope change touches no file of this plan.

Since Revision 1 (`009613ab`), after plan check 6846109878: successor and verify only after an accepted first publication; between-repeat rules; purpose by run-ID pattern; empty earlier repeats valid in check 30; trial-only assumptions listed; that check's P2s folded in.

## 1. Goal and authority

Goal: after this pull request merges, a scientific B10 phase run by `scenario.yml` runs, for each repeat N from 1 to 5: for C0, the start line, one ordinary producer Job and the ordinary `verify`; for C1, the start line, the ordinary C1 timeout Job, the first restart Job and, when ADR-021 protocol rule 10 permits, the successor Job, then the per-repeat verifier. Each repeat publishes the evidence of 3.6 (C1) or 3.8 (C0), which the final-judgement pull request reads.

Authority, in creation order, under docs/agents/factory.md "Item lifecycle" step 3:
- The issue body and the decisions listed in `items/404/state.md` on `main` at `675f8372`, which covers comment 6830544567. They include ADR-021 on `main`.
- [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) Q17 option B, as the restart redesign plan states it (`items/404/plan-b10-restart-redesign.md` at `b471d02d`, lines 411 to 415).
- [6774480762](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6774480762): the C1 timeout Job runs the ordinary LoadGen host unchanged; the restart and successor Job names.
- [6787982173](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787982173): science keeps run-ID prefix `b10`; point 2 fixes the timeout-Job outcome rules the trial uses.
- [6816117386](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816117386) point 2 (diagnostic extras only in the nonfinal object) and point 3 (verify runs whether or not the successor completes).
- [6827265635](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827265635) and [6827960705](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6827960705): LA-1, LA-2, LA-4, LA-5, LA-6, LA-3b and LA-4b, each accepted "for the next B10 diagnostic trial only". Both read in full for this revision. 6827960705 also records the control session's advice, which is not part of the decision, that partition and offset in the successor ledger row should come before scientific runs if the trial gives reason to doubt a `resend_lost` value.
- [6837706862](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837706862) and [6837940546](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837940546): the two trials after PR3c, which used up those acceptances. Readings [6837900667](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837900667) and [6838450660](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6838450660) are evidence.
- [6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192), [6844950677](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844950677), [6845385609](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845385609): the parallel verifier sign-in fix. This plan changes none of its files (2.3).
- [6845612916](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845612916): this plan, item (a). Point 2 in sections 2, 9 and 3.6/3.8's contract with item (b); point 3 in the plan-branch commit; point 4 in this file's status line.
- [6846521676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846521676): answers to Q1 to Q9, and the Revision 3 instructions.
- [6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008): answers to item (b)'s questions. Point 9 (item (b)'s Q9 option B, as item (b)'s plan at `51a34df2` line 371 states it: "ask (a) to add a create-only `WorkerReceipt` for the scientific `b10-abrupt-verify` Job") binds 3.6. Point 2 confirms C0 here. Its sign-in scope widening applies to every verifier mode that opens a Kafka session, in the sign-in fix's own pull request.
- [6846854445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846854445): check B2 option A (C0 nonfinal shape, three codec branches) and Q10 option A.

Catch-up at this revision's refresh: 332 comments on 4 pages; added since Revision 3: 6846805964 (this plan's check), 6846818616 (handoff), 6846854445 (decision) each read in full; 6847030526 (this plan's Revision 4 check), whose findings were read, and 6846904630 (the sign-in fix's plan check), heading only. Read in full: every comment after 6830544567, with three exceptions read in part: item (b)'s plan checks 6846147228 and 6846466122, of which only the lines naming item (a) or C0 were read, selected by script; and this plan's check 6846375050, of which the findings and the plan-check answers were read but not its claims table.

Reading limit: the prompt that started the first session asked for every `## Decision` and `## Approval` on the issue to be read in full (170 at Revision 1, 335783 bytes by script). This session read the new ones, those selected by script for this plan's terms (listed in Revision 1, section 1) and, for this revision, 6827265635 and 6827960705, in full; for the rest it relied on `items/404/state.md`. Parent items 347 and 348 were not re-read; the Revision 1 and 2 checks found no new decision on them.

## 2. Scope

The scope is decision 6845612916 point 1 (a), with C0 added by 6846521676 point 2. The phase sweep line, the final sweep projection, the five-repeat judgement and the candidate for both configurations are item (b)'s.

The plan freezes files and contracts, not lines (6845612916 point 2). Line numbers are references, not boundaries.

### 2.1 What exists on `main` at `675f8372`

- `scenario.yml` names no producer mode. B10 C0 runs in job `probe` with `PARCHWELL_PHASE` `C0` (line 77) and B10 C1 in job `full-C3` with `C1` (line 468). Both call `phase.sh run-repeats`, `judge` and `candidate`. Its committed copy is `docs/operations/workflows/scenario.yml.txt`.
- `phase.py` runs `("start-line", "loadgen", "loadgen2", "verify")` per repeat (lines 26, 944-972); `b10_prepare_sweep` requires 10 producer observations and 5 verify receipts (line 923).
- The scientific verifier assumes two producers: `VerifyRunHandler.cs` lines 47-51 and 151; `B10IntakeChecks.cs` lines 16 and 19; `B10PhaseRegistrationReader.cs` line 42; `PublicationAcceptance.cs` lines 74-84; `B10RepeatChecks.Evaluate` takes a first and a second request (lines 55-98); `B10SweepHandler`, `JudgeHandler` and `B10JudgeChecks`. `PhaseIdentity.RestartJobName` returns `...-loadgen2` (line 37).
- The restart path is trial-only. LoadGen `B10AbruptOptions.cs` lines 76-77 require `Purpose` `diagnostic_trial` and `Repeat` `1`; line 86 requires `{RunId}-c1-r1-loadgen-{restart|successor}`. Repeat 1 is also hard-coded in probe keys (`B10AbruptProducer.cs` lines 203, 252, 335; `B10AbruptJournal.cs` lines 177, 190), the Blob prefix (`B10AbruptPublisher.cs` lines 134, 213), identity repeat (`B10AbruptPublisher.cs` lines 130, 145, 259; `B10AbruptWire.cs` line 210), and the envelope `purpose` (`B10AbruptWire.cs` lines 54, 66, 69).
- Chart: `values.schema.json` line 649 ties `loadgen-restart` and `loadgen-successor` to `b10_abrupt_diagnostic` true; line 650 then fixes `run_kind` `smoke`, `phase` `C1`, `repeat` 1, requires `bounds.b10_abrupt`, excludes a list of keys, and in its `else` forbids `b10_abrupt_binding_configmap`. Line 931 restricts `b10-abrupt-verify` to repeat 1 and pattern `^b10d-[0-9]+-[0-9]+$`. `jobs.yaml` writes `B10Abrupt__Purpose` as the literal `diagnostic_trial` (line 117).
- Verifier: `VerifierHostOptionsValidator.cs` lines 226-227 and 258-259 fix `b10-abrupt-verify` to `B10/C1/C1/1`. `B10AbruptVerifyHandler.cs` line 47 lists `<RunId>/C1/r1/b10-abrupt-diagnostic/`; lines 231-247 write repeat 1 and `diagnostic_trial`. `VerifierHostOptions.ExpectedProducerJobNames` (lines 59-70) gives three names only for `b10d`; `StartLineNoteCodec` line 45 writes three for `b10d` and two otherwise.
- `phase.py` `reject_b10_abrupt_science` (lines 279-307) fails every scientific entry when the runner environment or `phase.json` carries a diagnostic marker.

### 2.2 Files allowed to change

1. `infra/scripts/phase/phase.py`, `infra/scripts/phase/b10_abrupt.py`, and tests under `infra/scripts/phase/tests/`. The trial's behaviour and outputs stay as on `main` (I1).
2. `src/Parchwell.LoadGen/Run/Abrupt/**`, for 3.4 only. `tests/Parchwell.LoadGen.Tests/Abrupt/**` and `tests/Parchwell.LoadGen.Tests/Host/RestartJobIdentityTests.cs`.
3. `charts/parchwell/templates/jobs.yaml`, `templates/_helpers.tpl`, `values.schema.json`, `charts/parchwell/tests/**`, for 3.5.
4. Verifier, for 3.1, 3.6 and 3.8: `VerifierHostOptions.cs`, `VerifierHostOptionsValidator.cs`, `Common/Window/StartLineNoteCodec.cs`, `Common/Phase/PhaseWireCodec.cs` (the receipt mode of 3.6 and the one-producer C0 intake branch of 3.8), `Common/Results/VerifierResultCodec.cs` and `Common/Results/B10ProducerObservationsCodec.cs` (the one-producer C0 branch of 3.8 only; 6846854445 point 1), `VerifyRun/Abrupt/**`, `VerifyRun/VerifyRunHandler.cs`, `VerifyRun/Judgement/B10IntakeChecks.cs`, `VerifyRun/Input/B10PhaseRegistrationReader.cs`, `VerifyRun/Publication/PublicationAcceptance.cs`, and new files under `VerifyRun/`. The last three are C5 `mutate` targets whose freeze 6846521676 point 2 lifts.
5. Verifier tests: any file under `tests/Parchwell.Verifier.Tests/` that 2.4 lists, `Support/HostEvidenceBuilder.cs` and `Support/B10SweepFixture.cs` for the legacy-fixture rule, and new files prefixed `B10ScientificThreeJob` or `B10OneJobC0`.
6. `docs/operations/b10-abrupt-contract.md` (owner of 3.4 to 3.6), `docs/operations/verifier-contract.md` (owner of 3.8), `docs/operations/b10-workflow.md`, `docs/operations/loadgen-contract.md` and `docs/operations/chart-contract.md`, only where they state the rules this plan changes.
7. `items/404/state.md` and a new receipt `items/404/b10-scientific-three-job-receipt.md`.

### 2.3 Paths that must not change

- Every file under `.github/` and `docs/operations/workflows/`. If a workflow change is needed, stop (section 12).
- `infra/ephemeral/**`, `infra/persistent/**`, `infra/cluster/**`.
- `src/Parchwell.Kafka/**`, and every LoadGen file outside `Run/Abrupt/`. The ordinary host already accepts `{run}-{phase}-rN-loadgen` for N in 1..5 (`LoadGenHostOptions.cs` lines 22, 98-106).
- `src/Parchwell.Verifier/Program.cs` and `src/Parchwell.Verifier/Common/Kafka/**` (the sign-in fix's files).
- Every other C5 `mutate` target (`stryker-c5-config.json` lines 17-35) and that file, including `B10RepeatChecks.cs`, `B10SweepHandler.cs`, `B10JudgeChecks.cs`, `B10SweepChecks.cs`, `B10LateWriteChecks.cs`, `B10SweepEvidenceCodec.cs`, `PhaseJudge.cs` and `RestartManifestReader.cs`. Also `JudgeHandler.cs` and `PhaseIdentity.cs`.
- `docs/blueprint.md`, `docs/operations/a01-runtime-limits.json`, `scenarios/**`, `results/**`. `scenarios/B10/expected.yaml` must not be created.
- `AGENTS.md`, `CLAUDE.md`, `docs/agents/**`, any `*.csproj`, `Directory.*.props`, `*.sln*`. No new project, assembly reference or NuGet package.
- `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs`, which passes unchanged.

### 2.4 Protected paths, CI, mutation and existing tests

- Protected: `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**`. Hari merges.
- Required checks: `.NET verification`, `Lifecycle verification`. `docs-advisory` is advisory.
- Mutation: three C5 targets change, so acceptance needs `verifier-c5-mutation.yml` dispatched on `main` after merge with `measurement_complete: true` and `break_passed: true` against `thresholds.break: 60` (docs/engineering/dotnet.md line 55; 6846521676 point 2). That run is Hari's; this pull request dispatches nothing. Risk, stated: the C0 two-producer cases of the C5 targets' tests change to rejections, so the measured score can fall.
- Standing fixture rule (plan-pr "Plan rules" rule 2, modelled on [6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2). When an existing test's fixture is rejected by a check this plan adds or tightens, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; or (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The receipt lists each change with the check that forces it. The cold review and Codex review check each listed change. Cap: at most 40 existing test cases changed under this rule; exhaustion `push -wip and hand off to the next session` (6846521676 point 9).
- Legacy-fixture rule, a use of rule (a). A test that exercises a frozen legacy reader (`B10SweepHandler`, `JudgeHandler`, `B10JudgeChecks`, `B10SweepChecks`, `PhaseJudge`, `B10LateWriteChecks`, `B10RepeatChecks`) keeps its two-producer evidence. Its fixture passes the legacy names `[{run}-{phase}-rN-loadgen, {run}-{phase}-rN-loadgen2]` to `HostEvidenceBuilder` or `B10SweepFixture` explicitly, instead of `ExpectedProducerJobNames`, and the codec still writes them (3.1). Every assertion stays. Each such test counts toward the cap.
- Contract changes to existing tests, which are not fixture-rule uses and do not count toward the cap. Each new expected value is literal:
  - `tests/Parchwell.Verifier.Tests/Host/B10ExpectedProducerJobsTests.cs` line 20 expects `["run-1-c0-r1-loadgen"]` and line 45 `["run-1-c1-r5-loadgen", "run-1-c1-r5-loadgen-restart", "run-1-c1-r5-loadgen-successor"]`, with each test's own run ID.
  - `infra/scripts/phase/tests/test_phase.py` `test_b10_runs_twenty_ordered_modes_then_repeat_free_capture_and_sweep` (line 265). C0 subtest: modes per repeat `start-line`, `loadgen`, `verify`, 15 records, no `b10-sweep-line`, `b10-sweep` or `judge` record, `main('run-repeats')` 0, `main('judge')` 1, `b10_lifecycle.producers` 5 and `verifiers` 5. C1 subtest: T8's 25 records in order, `main('run-repeats')` 0, `main('judge')` 1, `producers` 15 and `verifiers` 5.
  - `tests/Parchwell.Verifier.Tests/Window/B10TrialStartLineCodecTests.cs`: the case that writes a `b10-1-1` note with three names now writes (3.1); every other case is unchanged.
- Fixture-rule uses already identified (the receipt completes the inventory):
  - C0 two-instance `verify` host tests: `B10TwoInstanceVerifyTests.cs`, `B10RepeatVerifyTests.cs`, `B10EarlierRepeatExclusionTests.cs` and the B10 cases of `VerifyRunHandlerTests.cs`. Rule (a) where the test's subject survives one producer (move to one registration, keep assertions); rule (b) where the subject is the second instance (expected exit 1 and failure `b10_registration_count_invalid`).
  - `B10IntakeChecksTests.cs`, `B10RegistrationTests.cs` and `PublicationAcceptanceTests.cs`, C0 two-producer cases: rule (b), with the literals of 3.8. Their C1 cases are unchanged (3.8 keeps C1's legacy branch).
  - `B10SweepHandlerTests.cs` (including `InlineData("C1")`, lines 24 and 250-252), `B10JudgeChecksTests.cs`, `PhaseJudgeTests.cs`, `B10SweepChecksTests.cs`, `B10SweepEvidenceTests.cs`, `JudgeHandlerTests.cs`: legacy-fixture rule.
  - LoadGen `B10AbruptOptionsTests.cs` lines 48-67 InlineData `Repeat` "2" and `Purpose` "scientific" with run ID "synthetic": still `DiagnosticIdentity` under 3.4; unchanged.
  - Chart `test_b10_abrupt_chart.py` `invalid_cases` (lines 112-130) and `invalid_reader_cases` (lines 197-205): unchanged expected values; T7 adds the cases that pin the schema rule itself.
  - Phase `test_b10_abrupt.py` `test_every_scientific_entry_rejects_before_work_and_removes_candidate` (line 328): unchanged.

## 3. External contracts

`R` is the scientific run ID `b10-{GITHUB_RUN_ID}-{GITHUB_RUN_ATTEMPT}`, pattern S `^b10-[0-9]+-[0-9]+$`; `N` is the repeat, 1 to 5, in invariant decimal. Pattern T `^b10d-[0-9]+-[0-9]+$` is the trial's.

### 3.1 Start-line names

- B10 C1, any run ID: exactly `[R-c1-rN-loadgen, R-c1-rN-loadgen-restart, R-c1-rN-loadgen-successor]`, including the successor name when no successor starts.
- B10 C0, any run ID: exactly `[R-c0-rN-loadgen]`.
- Other scenarios keep their one name.
- Codec write rule: a B10 note for a pattern-T run ID needs exactly three names (unchanged); any other B10 note needs one, two or three distinct bounded names; any other count throws `ArgumentException`. The derivation decides which list a Job writes; readers check it (two names remain writable only for legacy fixtures, 2.4).
- Frozen legacy readers (`B10SweepHandler`, `JudgeHandler`) still compare with `[loadgen, loadgen2]`, so they reject every note this pull request's start-line Job writes for B10.

### 3.2 Scientific orchestration (phase)

C0, for N = 1..5: `R-c0-rN-start-line`, `R-c0-rN-loadgen` (ordinary), `R-c0-rN-verify` (ordinary `verify`, 3.8). Outcomes, pod observation and `b10_stop_unobserved` follow `run_b10_repeats` on `main` with the `loadgen2` step removed.

C1, for N = 1..5:

| Order | Job | Runs when | Ends (trial mapping, `b10_abrupt.py` at `675f8372`) |
|---|---|---|---|
| 1 | `R-c1-rN-start-line` | always, unless the phase has stopped | `run_mode` sets `safety_stop` after `unknown` or `apply_failed` when it cannot confirm no pod remains (`phase.py` lines 739-756), and at deadline exhaustion after an applied Job that did not complete (lines 758-761): phase stop. Otherwise outcome not `complete`: `b10_abrupt_publication_failed` at `metadata`, repeat end. Then the metadata check of 3.3 |
| 2 | `R-c1-rN-loadgen` | metadata accepted | `render_failed`: `b10_abrupt_configuration_invalid` at `timeout_job`, repeat end, `safety_stop` unchanged (6787982173 point 2). `unknown`, `apply_failed`, or a failed pod read (which makes the outcome `unknown`, lines 392-416): `b10_abrupt_timeout_job_unknown`, `safety_stop` true, phase stop. `complete` or `failed` with the pod check passed: continue |
| 3 | `R-c1-rN-loadgen-restart` | timeout step continued | Outcome `unknown` or `apply_failed` sets `safety_stop` in `job()` (lines 387-388): phase stop, whatever the pod read shows. A first-publication identity mismatch records `b10_abrupt_first_termination_unknown` with `safety_stop` (lines 704-705), and `record_failure` sets `safety_stop` for every `b10_abrupt_first_termination_unknown` (lines 248-249): phase stop. The first-termination pod read always runs (line 936). It fails after a render failure, `apply_failed` or a failed read: `b10_abrupt_first_termination_unknown`, `safety_stop` true, phase stop (lines 431, 458). Outcome not `complete` after a passed read: `b10_abrupt_publication_failed` at `first_termination` (lines 935-938), repeat end. Rejected first publication (including `successor_permitted` not true, no-kill end, empty kill set; line 717): its failure name at `first_publication`, repeat end, no successor, no verify |
| 4 | `R-c1-rN-loadgen-successor` | first publication accepted | Late release: `b10_abrupt_successor_late`. Render failure or outcome not `complete`: `b10_abrupt_publication_ambiguous`, or `b10_abrupt_deadline_exhausted` when wait exits 124, at `successor_job` (lines 950-952); `unknown` or `apply_failed` also sets `safety_stop` (lines 386-388). Then the successor checks of `collect(files, observation)` (lines 973-1016), run here inside `run-repeats` for any applied successor: a failed pod read gives `b10_abrupt_first_termination_unknown` with `safety_stop` true; other failures their trial names at `successor_publication` or `successor_receipt` |
| 5 | `R-c1-rN-b10-abrupt-verify` | first publication accepted, whatever row 4 recorded, including `safety_stop` set in row 4 (6816117386 point 3; contract "Failure names and collection") | Outcome not `complete`: `b10_abrupt_verify_failed` at `verify_job`, `safety_stop` unchanged. Then download of the 3.6 object; failure `b10_abrupt_verify_failed` at `verify_collection` |

- Precedence: a phase stop wins over a repeat end. Whenever a row's event sets `safety_stop`, the phase-stop rule applies, even if the same row also names a repeat-end failure.
- Phase stop: when `safety_stop` is true, no Job of a later repeat is applied, and every later slot is recorded with reason `safety_stop`. Within the current repeat only row 5 may still run, and only under its own condition.
- Repeat end: the remaining rows of repeat N are not applied, and repeat N+1 starts at row 1.
- After each applied Job, `collect-job` copies its folder as the trial does; collection exit codes map as the trial maps them (contract "Failure names and collection") and end nothing.
- At most one producer pod runs at a time across repeats: rows 2 to 4 each confirm pod termination before the next producer Job renders, or stop the phase.
- All repeats of a phase share the existing 18000 s orchestration deadline (`phase.py` line 28), unchanged with the 360-minute job timeout (6846521676 point 6). Exhaustion records later slots as `orchestration_deadline`.
- After repeat 5, neither configuration runs `b10-sweep-line`, `b10-sweep` or `judge`. `judge` and `candidate` record `b10_c1_final_judgement_unavailable` for C1 (6846521676 point 7) and, `b10_c0_final_judgement_unavailable` for C0 (6846854445 point 2); each writes no candidate, keeps `full_allowed` false and exits 1.
- `run-repeats` exits 1 when any record's outcome is in `BAD_OUTCOMES` (as on `main`) or any failure name was recorded; otherwise 0.
- The scientific path never sets `b10_abrupt_diagnostic`, `purpose` `diagnostic_trial` or `run_kind` `smoke` in `phase.json`, so `reject_b10_abrupt_science` is unchanged and passes it.

Lifecycle record, an external contract with item (b). After every producer and verify Job of either configuration, the phase appends to `phase-status.json` `b10_lifecycle.producers` or `b10_lifecycle.verifiers` one object `{job, repeat, mode, pod_name, pod_uid, finished_utc, exit_code}` from that Job's one pod read, or appends `{object: "<job>/pods.json", reason: "producer_termination_unavailable"}` or `"verifier_termination_unavailable"` to `b10_lifecycle.gaps`. `finished_utc` is `state.terminated.finishedAt`, normalized by `b10_utc`. A Job not applied adds nothing.

### 3.3 Metadata check for scientific C1

Before repeat N's timeout Job, the phase downloads `R/C1/rN/start-line.json`, `broker-inventory.json` and `start-line-completion.json` and applies the trial's `metadata()` checks with repeat N: receipt `repeat` N and `job_name` `R-c1-rN-start-line`; identity Job `R-c1-rN-loadgen`; `expected_job_names` equal to 3.1's C1 list. Each failure records what the trial's `metadata()` records for it: `b10_abrupt_publication_ambiguous` for a missing object, otherwise `b10_abrupt_publication_failed`, at boundary `metadata`; repeat end. On success it creates configmap `R-b10-abrupt-first-rN`; before the successor, `R-b10-abrupt-successor-rN`. Binding formats are the trial's.

### 3.4 LoadGen restart and successor (scientific)

- `B10Abrupt__Purpose` is `diagnostic_trial` or `scientific`. `diagnostic_trial` keeps every rule on `main`, including `Repeat` `1`, for any run ID. `scientific` requires `RunId` matching S and `Repeat` `1` to `5` written without a leading zero; ScenarioId, ConfigurationId and Phase stay `B10`, `C1`, `C1`. Otherwise validation fails with `ValidationDetail.DiagnosticIdentity`.
- Job names `{RunId}-c1-r{Repeat}-loadgen-restart` and `{RunId}-c1-r{Repeat}-loadgen-successor`.
- Probe keys and IDs `B10/{RunId}/C1/{Repeat}/same_instance` and `B10/{RunId}/C1/{Repeat}/cross_restart` (ADR-021 protocol rule 1).
- Blob prefix `{RunId}/C1/r{Repeat}/b10-abrupt/{pod}/` for `scientific` (6846521676 point 3); `{RunId}/C1/r1/b10-abrupt-diagnostic/{pod}/` for `diagnostic_trial`, unchanged.
- Identity `repeat` is the configured `Repeat` in every object and record the host writes.
- Envelope: `purpose` is the configured `Purpose`; `scientific_admissible` stays `false` for both purposes; `schema_version` 3, `dialect` `b10_abrupt`, `synthetic` true and every other format as `b10-abrupt-contract.md` states them.
- Readers. The LoadGen successor requires `purpose` equal to its own configured `Purpose`, and identity `repeat` equal to its configured `Repeat`. The phase and the verifier require `purpose` `scientific` for a run ID matching S and `diagnostic_trial` for every other run ID; they never see a `b10-` diagnostic trial, because the trial's run IDs match T. A mismatch fails as a mismatched envelope does today: `malformed_frame` in LoadGen, `ValueError('diagnostic envelope')` recorded as `b10_abrupt_publication_failed` in the phase (lines 460-465), problem 2 or 4 in the verifier.

### 3.5 Chart (scientific)

- New runtime key `b10_abrupt_science`, default false. WHEN true, THEN `scenario_id` `B10`, `configuration_id` and `phase` `C1`, `repeat` 1 to 5, `run_id` matching S, `b10_abrupt_binding_configmap` and `bounds.b10_abrupt` required, `b10_abrupt_diagnostic` false or absent, and the same excluded keys as line 650's diagnostic branch. `run_kind` is not constrained by this key.
- Modes `loadgen-restart` and `loadgen-successor` require exactly one of `b10_abrupt_diagnostic` and `b10_abrupt_science` true. Line 650's `else` forbids `b10_abrupt_binding_configmap` only when neither is true.
- `B10Abrupt__Purpose` renders `scientific` when `b10_abrupt_science` is true, else `diagnostic_trial`. `B10Abrupt__Repeat` renders the repeat.
- `b10-abrupt-verify` accepts the existing trial values (pattern T, repeat 1) or S with repeat 1 to 5. `Verifier__ArtifactPrefix` is `<run>/C1/r<repeat>/producer/`.
- Deadlines unchanged: `loadgen_job_seconds` 1140, `verify_job_seconds` 750, `start_line_job_seconds` 150.

### 3.6 Per-repeat C1 verifier output (external contract with item (b))

Mode `b10-abrupt-verify` is unchanged for pattern T. For pattern S:

- Options: `ScenarioId` `B10`, `ConfigurationId` and `Phase` `C1`, `Repeat` 1 to 5, `ArtifactPrefix` `<RunId>/C1/r<Repeat>/producer/`. Anything else exits 1 before any read.
- Checks 1 to 29 of `b10-abrupt-contract.md` apply with repeat N wherever they name repeat 1, `r1`, `/C1/1/` or `c1-r1`. Check 3 lists `<RunId>/C1/rN/b10-abrupt/`. Check 4 requires `purpose` `scientific`. Check 16 lists `<RunId>/C1/rN/producer/` and requires Job `{RunId}-c1-rN-loadgen`.
- Check 30, earlier-repeat records (ADR-021 "Late-write scan"; 6846521676 point 8), evaluated when the window was drained and N is at least 2. For each earlier repeat M of the phase, the handler lists `<RunId>/C1/rM/producer/` and `<RunId>/C1/rM/b10-abrupt/` within the existing listing bounds and reads each `registration.json` found with the existing registration reader. An empty listing is valid. A window record whose `instance-id` equals an instance registered there is held out of checks 19 and 20 and of every count, and counted in `earlier_repeat_records`. WHEN a listing fails or is incomplete, a listed registration cannot be read or is invalid, or an instance ID of repeat N equals an earlier repeat's, THEN problem [10] `b10_abrupt_verify_earlier_repeat_unavailable` and reason E3 are recorded and `earlier_repeat_records` is null. For N equal to 1 it is not evaluated and `earlier_repeat_records` is 0.
- Output: container `verifier`, `<RunId>/C1/rN/b10-abrupt/nonfinal-result.json`, create-only with read-back. Envelope `schema_version` 3, `dialect` `b10_abrupt`, `purpose` `scientific`, `synthetic` true, `scientific_admissible` false, `kind` `nonfinal_result`; identity `repeat` N.
- `data` members are exactly those of `b10-abrupt-contract.md` "Output `nonfinal_result`", plus `earlier_repeat_records` (integer, or null). `finality` `nonfinal`; `eligibility` `ineligible` or `pending_sweep`. Problems 1 to 9 keep their order; problem 10 follows. Reasons E1 to E13 unchanged.
- Completion receipt (6846551008 point 9). For pattern S only, after the object is created and read back byte-identical, and as the mode's last external write, the mode creates once `<RunId>/C1/rN/b10-abrupt/verify-completion.json` in container `verifier`, read back byte-identical. It is a `WorkerReceipt` in the existing schema-1 format with `mode` `b10-abrupt-verify`, `scenario_id` `B10`, `configuration_id` and `phase` `C1`, `repeat` N, `job_name` `{RunId}-c1-rN-b10-abrupt-verify`, `pod_name` the verifier pod, `identity` null, `source_commit` `VerifierCommit`, `handler_exit_code` 0, `cleanup_completed` true, `started_utc` and `completed_utc`, `artifacts` exactly one reference `{container: "verifier", name: "<RunId>/C1/rN/b10-abrupt/nonfinal-result.json", length, sha256}` of the object's exact bytes, and `publication_acceptance`, `repeat_checks` and `b10_intake` null, as the `b10-sweep` receipt has them (`docs/operations/verifier-contract.md` line 261). Exit 0 requires both writes. An existing receipt or a failed write or read-back exits nonzero; the object is never rewritten.
- For pattern T the output has no `earlier_repeat_records` member and no receipt, and stays as on `main`.

### 3.7 New literals

- Verifier problem 10: `b10_abrupt_verify_earlier_repeat_unavailable`.
- Phase gaps: `b10_c1_final_judgement_unavailable`; `b10_c0_final_judgement_unavailable` (6846854445 point 2).
- No other new literal.

### 3.8 Scientific C0 verify (external contract with item (b))

- Mode `verify` for B10 C0 lists `R/C0/rN/producer/` and requires exactly one `registration.json`, bound to `R-c0-rN-loadgen` with null predecessor. Zero or two or more registrations fail with `b10_registration_count_invalid`, as two-instance failures do on `main`.
- `B10IntakeChecks.BindRegistrations` for C0: exactly one registration and expected list `[R-c0-rN-loadgen]`. Its C1 branch keeps the two-name rule of `main`, which no scientific Job reaches.
- `B10PhaseRegistrationReader` for C0: exactly one registration per earlier repeat, bound to `[JobName]`. C1 unchanged.
- `PublicationAcceptance` for B10 C0: start line `expected_job_names` exactly `[R-c0-rN-loadgen]`, manifest `Restart` absent, failure `start_line_invalid` or `publication_restart_role_mismatch` as on `main`. C1 unchanged.
- The one instance is judged with per-instance checks 1 to 27 of `docs/operations/verifier-contract.md` as `RunJudgement.Evaluate` applies them to a B10 instance, ADR-021 "Permitted conditions" through the unchanged `B10RepeatChecks.IsPermittedTimeout` and `IsPermittedGuardMarker`, and the ADR-021 "Retries" rule as `b10-abrupt-contract.md` check 18 states it. `B10RepeatChecks.Evaluate` is not called for C0 and is unchanged.
- Outputs keep `main`'s nonfinal B10 shape (6846854445 point 1): `R/C0/rN/result.json` with status `incomplete` or `invalid`, never `complete_valid`, `classification_final` false and `b10_window_checks_complete` true when the window checks passed; `R/C0/rN/verify-completion.json` with `handler_exit_code` 1, `publication_acceptance` null and B10 intake present; the handler exits 1. The B10 intake holds exactly one producer, `R-c0-rN-loadgen` with null predecessor.
- Codec branches (6846854445 point 1). `PhaseWireCodec.ValidateB10Intake` (lines 244-256), `VerifierResultCodec` (lines 213-215, 250-251, and `EligibleShape`, lines 1057-1061) and `B10ProducerObservationsCodec.Validate` (line 49) accept, for configuration C0, either exactly one producer (and one observation) bound to `R-c0-rN-loadgen` with null predecessor, or `main`'s legacy pair (`loadgen` then `loadgen2`, with the second naming the first as predecessor) unchanged. For C1 they keep `main`'s two-producer rule unchanged. Any other count or shape fails as it fails on `main`. The codecs are format readers; which count a scientific repeat may have is decided by `VerifyRunHandler`, `B10IntakeChecks` and `B10PhaseRegistrationReader` (one for C0). So the frozen `B10SweepHandler` and `JudgeHandler`, which require two producers, still read their legacy C0 fixtures, and reject the one-producer C0 evidence this path writes (R11). Item (b)'s F3 reads these outputs.

## 4. Requirements

- R1. WHEN a B10 start line is written, THEN it carries 3.1's list for its configuration.
- R2. WHEN `run-repeats` runs B10 C1 science, THEN for each N it applies 3.2's rows in order under their "Runs when" conditions.
- R3. WHEN a row's "Ends" gives repeat end, THEN its failure name is recorded at its boundary, no later row of repeat N runs, and repeat N+1 starts.
- R4. WHEN `safety_stop` is set in repeat N, THEN no Job of a later repeat is applied and every later slot is recorded with reason `safety_stop`; WHEN it is set in row 4 after an accepted first publication, THEN row 5 still runs.
- R5. WHEN `judge` or `candidate` runs for B10 C0 or C1, THEN the configuration's gap of 3.7 is recorded, no candidate exists, `full_allowed` is false and the command exits 1.
- R6. WHEN the LoadGen restart or successor host runs with `Purpose` `scientific`, run ID matching S and `Repeat` N in 1..5, THEN it validates and its names, keys, prefix and identity follow 3.4; otherwise it fails with `DiagnosticIdentity` before registration.
- R7. WHEN a reader receives an envelope whose `purpose` or identity `repeat` differs from what 3.4 requires of it, THEN it rejects it as it rejects a mismatched envelope today.
- R8. WHEN the verifier runs for pattern S and repeat N with valid evidence, THEN it writes 3.6's object with `problems` [], `eligibility` `pending_sweep`, identity `repeat` N and `purpose` `scientific`.
- R9. WHEN repeat N's window holds a record from an earlier repeat's instance, THEN it is held out and counted in `earlier_repeat_records`, with no problem 8 for it.
- R10. WHEN an earlier listing fails or is incomplete, a listed registration is unreadable or invalid, or an instance ID repeats, THEN problem 10 and E3, and `earlier_repeat_records` null; WHEN earlier listings are empty, THEN no problem.
- R11. WHEN a legacy reader (the frozen `B10SweepHandler`, `JudgeHandler` and `B10SweepEvidenceCodec`, or `PhaseWireCodec` and `VerifierResultCodec` outside their 3.6 and 3.8 branches) reads a start line this pull request writes for B10, or a 3.6 object, THEN it rejects it.
- R12. WHEN the trial runs (pattern T), THEN its Jobs, names, prefixes, envelopes, output path and members are as on `main`.
- R13. WHEN `run-repeats` runs B10 C0 science, THEN for each N it applies `start-line`, `loadgen`, `verify` and never `loadgen2`.
- R14. WHEN C0 `verify` finds exactly one registration bound to `R-c0-rN-loadgen`, THEN it judges it under 3.8 and writes the nonfinal `result.json` and `verify-completion.json` of 3.8 with one producer and exits 1; WHEN it finds zero or two, THEN `b10_registration_count_invalid`.
- R19. WHEN the three codecs of 3.8 read or write a B10 C0 intake that is neither one producer nor `main`'s legacy pair, or a B10 C1 intake with other than two, THEN they fail; WHEN `VerifierResultCodec` writes or reads a B10 result with status `complete_valid`, THEN it fails, as on `main`.
- R15. WHEN a C0 instance has a retry whose latest earlier attempt with the same key and sequence lacks a permitted timeout row, THEN the repeat is not complete-valid, as check 18 fails for the C1 timeout instance.
- R16. WHEN `PublicationAcceptance` reads a B10 C0 publication whose start line carries other than `[R-c0-rN-loadgen]`, THEN `start_line_invalid`.
- R17. WHEN `B10PhaseRegistrationReader` reads an earlier C0 repeat with other than one registration, THEN it fails as it fails for a wrong count on `main`.
- R18. WHEN the pattern-S verifier has created and read back its object, THEN it creates the receipt of 3.6 binding the object's exact length and SHA-256; WHEN the receipt already exists or its write fails, THEN it exits nonzero; WHEN the run ID matches T, THEN no receipt is written.

## 5. Invariants and safety

- I1. The trial's behaviour, contracts and outputs are unchanged (R12).
- I2. No per-repeat object is `scientific_admissible` true. No verdict, candidate or count is published by this pull request.
- I3. At most one producer pod runs at a time. Cleanup, collect, upload and destroy keep their existing `always()` steps in `scenario.yml`.
- I4. No workflow dispatch, trial, `results/` or expected-outcome change.
- I5. No Azure credentials in any test. Kafka tests use `LocalKafkaFixture`.

## 6. Test map

| Test | Inputs | Expected |
|---|---|---|
| T1 names | `ExpectedProducerJobNames`, B10, repeat 5, run IDs `b10-1-1`, `b10d-1-1`, `run-1`; C1 and C0 | C1: `[x-c1-r5-loadgen, x-c1-r5-loadgen-restart, x-c1-r5-loadgen-successor]`; C0: `[x-c0-r5-loadgen]` |
| T2 codec | B10 notes: `b10d-1-1` with 1, 2, 3 names; `b10-1-1` with 0, 1, 2, 3, 4 names; `b10-1-1` with `["b10-1-1-c1-r1-loadgen", "b10-1-1-c1-r1-loadgen"]` | `b10d`: only 3 writes; `b10-1-1`: 1, 2 and 3 write, 0 and 4 throw `ArgumentException`; the duplicate pair throws `ArgumentException` |
| T3 LoadGen scientific | `Purpose` `scientific`, run ID `b10-1-1`, `Repeat` "1", "3", "5" | valid; repeat 3: Job `b10-1-1-c1-r3-loadgen-restart`, key `B10/b10-1-1/C1/3/cross_restart`, prefix `b10-1-1/C1/r3/b10-abrupt/`, identity `repeat` 3 |
| T4 LoadGen rejects | `scientific` with run ID `b10d-1-1`, `synthetic`; `Repeat` "0", "6", "03"; `diagnostic_trial` with `Repeat` "2" | each `DiagnosticIdentity` |
| T5 envelope purpose | scientific journal to a trial reader; trial journal to a scientific reader; successor configured `scientific` reading a journal with identity `repeat` 2 at `Repeat` 3; `diagnostic_trial` with run ID `synthetic` and with `b10-1-1` | first three rejected: `malformed_frame` in LoadGen, `b10_abrupt_publication_failed` in the phase, problem 2 in the verifier; the last two accepted by LoadGen as on `main` |
| T6 chart | `b10_abrupt_science` true, run ID `b10-1-1`, repeat 1 and 5, restart and successor; `b10-abrupt-verify` with `b10-1-1`, repeat 5, `loadgen_job_name` `b10-1-1-c1-r5-loadgen` | renders; `B10Abrupt__Purpose` `scientific`; `B10Abrupt__Repeat` `5`; `Verifier__ArtifactPrefix` `b10-1-1/C1/r5/producer/` |
| T7 chart rejects | science with run ID `b10d-1-1`; repeat 6; both flags true; neither flag for `loadgen-restart`; science without `bounds.b10_abrupt`; science with an excluded key; `b10_abrupt_binding_configmap` with neither flag; `b10-abrupt-verify` with `b10-1-1` repeat 6; `b10-abrupt-verify` with `b10d-1-1`, repeat 2, `loadgen_job_name` `b10d-1-1-c1-r2-loadgen` | each a schema failure, not a template failure |
| T8 C1 order | fake kubectl/helm, C1 science, every Job complete | 25 records in order, for N = 1..5: start-line, loadgen, loadgen-restart, loadgen-successor, b10-abrupt-verify; configmaps `R-b10-abrupt-first-rN` and `R-b10-abrupt-successor-rN` created once each; no sweep or judge record; `run-repeats` exit 0 |
| T9 rejected publication | as T8, repeat 2 `termination.json` with `successor_permitted` false | repeat 2: `b10_abrupt_publication_failed` at `first_publication`, no successor, no verify; repeats 3 to 5 apply 5 Jobs each; exit 1 |
| T9b restart render failure | as T8, repeat 2 restart render fails | `b10_abrupt_first_termination_unknown`, `safety_stop` true; repeats 3 to 5 not applied, reason `safety_stop`; exit 1 |
| T9c pod reads | as T8, three subcases: repeat 2 timeout pod read fails; restart pod read fails; successor pod read fails | timeout: `b10_abrupt_timeout_job_unknown`, `safety_stop` true, no restart Job; restart: `b10_abrupt_first_termination_unknown`, `safety_stop` true, no successor or verify; successor: `b10_abrupt_first_termination_unknown`, `safety_stop` true, verify still runs; in each, repeats 3 to 5 not applied |
| T9d successor check | as T8, repeat 2 successor `completion.json` with `cleanup_completed` false | `b10_abrupt_publication_failed` at `successor_receipt`; verify runs; repeat 3 runs |
| T9e successor unknown | as T8, repeat 2 successor wait outcome `unknown` | `safety_stop` true; `b10_abrupt_publication_ambiguous` at `successor_job`; repeat 2 verify applied; repeats 3 to 5 not applied |
| T9g restart unknown | as T8, repeat 2 restart wait outcome `unknown`, pod read passes | `b10_abrupt_publication_failed` at `first_termination`; `safety_stop` true; exit 1; no successor or verify for repeat 2; repeats 3 to 5 not applied, reason `safety_stop` |
| T9h identity mismatch | as T8, repeat 2 first publication with a wrong `pod_uid` in `termination.json` identity | `b10_abrupt_first_termination_unknown` at `admission`, `safety_stop` true; no successor or verify; repeats 3 to 5 not applied; exit 1 |
| T9f timeout render failure | as T8, repeat 2 timeout render fails | `b10_abrupt_configuration_invalid` at `timeout_job`, `safety_stop` false; repeat 3 starts |
| T10 metadata | repeat 2 start line with `[loadgen, loadgen2]` names; separately, repeat 2 `start-line.json` missing | `b10_abrupt_publication_failed` at `metadata`; `b10_abrupt_publication_ambiguous` at `metadata`; no producer Job for repeat 2; repeat 3 runs |
| T12 lifecycle | T8 run; T22 run | C1: `producers` 15, `verifiers` 5, `gaps` []; C0: `producers` 5, `verifiers` 5; each entry with the 7 members of 3.2 |
| T13 judge gap | `judge` and `candidate` after T8 and after T22 | gap `b10_c1_final_judgement_unavailable` or `b10_c0_final_judgement_unavailable`; no candidate; `full_allowed` false; exit 1 |
| T14 verifier repeat N | component harness, `b10-1-1`, repeat 3, valid evidence | object at `b10-1-1/C1/r3/b10-abrupt/nonfinal-result.json`; `purpose` `scientific`; identity `repeat` 3; `problems` []; `eligibility` `pending_sweep`; `earlier_repeat_records` 0 |
| T15 earlier record | as T14, window holds one record with repeat 2's timeout instance ID | `earlier_repeat_records` 1; `problems` []; timeout counts as T14 |
| T16 earlier unavailable | as T14: repeat 2 producer listing fails through the store seam; a repeat 2 registration with an invalid envelope; repeat 3 timeout instance equal to repeat 1's | problems [10], reasons [E3], `earlier_repeat_records` null |
| T16b earlier empty | as T14, repeat 2 listings both empty | `problems` []; `earlier_repeat_records` 0 |
| T17 repeat 1 | `b10-1-1`, repeat 1 | check 30 not evaluated; `earlier_repeat_records` 0 |
| T18 verifier rejects | S with prefix `b10-1-1/C1/r2/producer/` at repeat 3; repeat 6 | exit 1 before any read |
| T19 legacy readers | T14 bytes; a start line written by T1's C1 and C0 derivations | `B10SweepHandler`, `JudgeHandler`, `PhaseWireCodec`, `VerifierResultCodec`, `B10SweepEvidenceCodec` reject |
| T20 trial unchanged | existing trial tests in `test_b10_abrupt.py`, `test_b10_abrupt_chart.py`, `B10AbruptVerify*`, `B10AbruptOptionsTests` | pass with their existing expected values |
| T21 science not diagnostic | scientific `phase.json` after T8 | `reject_b10_abrupt_science` passes; no `b10_abrupt_diagnostic` key |
| T22 C0 order | fake kubectl/helm, C0 science, every Job complete | 15 records: for N = 1..5 start-line, loadgen, verify; no `loadgen2`; exit 0 |
| T23 C0 verify valid | host test, B10 C0 repeat 2, one registration `run-1-c0-r2-loadgen`, start line `[run-1-c0-r2-loadgen]`, valid evidence | handler exit 1; `verify-completion.json` `handler_exit_code` 1, intake with 1 producer; `result.json` status `incomplete`, `classification_final` false, `b10_window_checks_complete` true |
| T24 C0 count | as T23 with 0 and with 2 registrations | exit 1, `b10_registration_count_invalid` |
| T25 C0 start line | as T23 with start line `[run-1-c0-r2-loadgen, run-1-c0-r2-loadgen2]` | `start_line_invalid` |
| T26 C0 restart manifest | as T23 with a manifest carrying `Restart` | `publication_restart_role_mismatch` |
| T27 C0 retry rule | as T23: attempts a1 (k1, seq 0) callback delivered false, -192, persistence 1, then a2 (k1, seq 0) delivered; and the same with a1 delivered false, error -1 | first: T23's eligible shape; second: `result.json` status `invalid` |
| T28 C0 earlier repeat | as T23 at repeat 3, repeat 2 producer prefix with 2 registrations | `InvalidOperationException` with message `b10_registration_count_invalid` (`B10PhaseRegistrationReader.cs` line 42 on `main`) |
| T29 C1 legacy branch | `B10IntakeChecks.BindRegistrations` and `PublicationAcceptance` with C1 two-producer inputs from `main`'s tests | unchanged results |
| T32 C0 codecs | `ValidateB10Intake`, `VerifierResultCodec` (including `EligibleShape`) and `B10ProducerObservationsCodec` with: C0 one producer; C0 legacy pair; C1 one producer; C1 two producers as on `main`; a B10 C0 result with status `complete_valid` | C0 one accepted; C0 legacy pair accepted as on `main`; C1 one rejected; C1 two accepted; `complete_valid` rejected |
| T31 C1 receipt | T14 run; T14 with a pre-existing receipt; trial run as T20 | receipt with the 3.6 members, `artifacts[0]` length and SHA-256 equal to the object's bytes, exit 0; exit nonzero and the object unchanged; no receipt object |
| T30 legacy fixtures | the tests 2.4 puts under the legacy-fixture rule | pass with every existing assertion |

Each verifier row asserts that every other enforced check still passes in that subcase. The receipt records the rest of the predicate map: below, at and above each boundary (repeat 0, 1, 5, 6; name counts 0 to 4; registration counts 0, 1, 2).

## 7. Deployment assumptions (live-only)

"Fresh" means a connection the process opened for this observation; "idle" means no producer was sending. No scientific run of either shape has executed. The evidence is two diagnostic trials of one C1 repeat each.

| ID | Assumption | Evidence, and how it was taken | If false |
|---|---|---|---|
| LA-S1 | Five sequential self-kill repeats in one phase each publish their first evidence; repeat N+1's pods start after repeat N's terminate | Runs 43217277627 and 43322351104: one repeat each, fresh pods and connections, idle namespace apart from the trial's own producers | Named failure per repeat; fewer eligible repeats |
| LA-S2 | A record of an earlier repeat's instance that lands in a later window carries that instance's `instance-id` header | No live observation. The header rule is the producers' own | Problem 8 instead of an earlier-repeat count |
| LA-S3 | Each repeat's verify Job finishes within 750 s, including check 30's reads | Run 43322351104, repeat 1: fresh verifier pod and connections, idle namespace, verify `complete`; check 30 never ran. Run 43318281268 on the same `main` ended its verify with problem 5 after a token timeout (6837900667); five verify Jobs per phase raise that exposure until the sign-in fix merges | `b10_abrupt_verify_failed` for that repeat |
| LA-S4 | Five repeats finish within the 18000 s deadline in practice | Two measured step timings of one C1 repeat each (section 9); five never ran | Later repeats `orchestration_deadline`; restart held |

Trial-only assumptions this path needs again, each accepted for one diagnostic trial only and used up: LA-1, LA-2, LA-4, LA-5 and LA-6 (`items/404/plan-pr3c-full-classification.md` at `48c52669`, section 7; 6827265635 point 1); LA-3b and LA-4b (same section; 6827960705); LA-V2 to LA-V5 (6816733258 point 2), restated by LA-1, LA-2, LA-3b and LA-4; warm-leader LA1 to LA5 (6799006446) and LA6 (6801095762 point 1).

Status of all of these: accepted by Hari for planning only, and not for any run ([6846521676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846521676) point 5). One governed diagnostic run of the scientific path is required after this pull request and the final-judgement pull request merge, and before any scientific run; it needs its own authorization. This plan moves them back to this section under that decision.

## 8. Acceptance commands

| # | Command | Evidence |
|---|---|---|
| A1 | `./scripts/verify-dotnet.sh` | `Verification passed.`; reports include T1 to T5, T14 to T20, T23 to T32 |
| A2 | `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` and the same for `infra/scripts/phase/tests` | `OK`; includes T8 to T13, T9b to T9h, T21, T22 |
| A3 | `bash infra/scripts/lifecycle/check-offline.sh` | exit 0; includes T6, T7 |
| A4 | `npm run check:docs` | each warning fixed or explained (docs/agents/pull-requests.md rule 8) |
| A5 | `git diff --name-only origin/main...HEAD` | a subset of 2.2, checked by script against 2.3 |
| A6 | After merge, by Hari: `verifier-c5-mutation.yml` on `main` | `measurement_complete: true`, `break_passed: true` (2.4) |

## 9. Caps and plan-check answers

Caps per implementing session (6845612916 point 2): 20 verification-failure root causes and 20 review-finding root causes, each exhausting to `push -wip and hand off to the next session` (AGENTS.md "Before opening a pull request" rule 11). Existing test cases changed under the standing fixture rule: at most 40, exhausting to `push -wip and hand off to the next session` (6846521676 point 9).

(a) Steps whose effort scales with a number unknown at planning time: implementer root causes and fixture-rule cases, capped above. Runtime work per repeat is bounded by existing limits (`MaxConsumedRecords`, listings of 1024 entries and 16 pages, journal 4096 records); check 30 reads at most four earlier repeats times two listings.

(b) Stricter than the parent (item plan Revision 5 and ADR-021): check 30 makes a failed or unreadable earlier listing or registration a named problem with E3, where ADR-021 states no rule; purpose-bound readers (R7); `judge` and `candidate` exit 1 with a named gap until item (b) merges (6846521676 point 7, Q10). The between-repeat rules of 3.2 are the trial's, applied per repeat; the one addition is that a repeat end lets the next repeat start, which the one-repeat trial has no occasion to state.

(c) Dependencies outside `main` and this pull request: the live assumptions of section 7. The post-merge mutation run (A6). Item (b) depends on 3.2, 3.6 and 3.8, not the reverse; its check is rerun against this revision (6846521676).

Time evidence for Q6 as decided (6846521676 point 6): measured GitHub step timings of "Run the single diagnostic restart trial", 452 s in run 43322351104 and 448 s in run 43217277627, each on a freshly created environment. Two samples of one C1 repeat, not a bound. The configured wait bounds, by script from `values.yaml`, are 4890 s per C1 repeat and 24450 s for five, against the 18000 s deadline; both limits stay.

The live-smoke rule of docs/agents/factory.md "Item lifecycle" step 1 applies to plans that change live infrastructure; this plan changes no Terraform root. Hari's required governed diagnostic run (section 7) is the live test of this design.

External behaviour (plan-pr "Plan rules" rule 1): from Kubernetes v1.31 the Job controller adds `Complete` or `Failed` only after all Job pods terminate. Verified with Context7 (`/websites/kubernetes_io`), from https://kubernetes.io/docs/concepts/workloads/controllers/job/, "Terminal Job conditions", 2026-11-13; the cluster requires 1.35.0 or later (`infra/cluster/variables.tf`). The phase still reads pod termination directly. No Kafka, librdkafka, Confluent.Kafka or Event Hubs behaviour is newly relied on.

## 10. Sources

- ADR-012, ADR-013, ADR-020 and ADR-021 (`docs/blueprint.md` at `675f8372`), in particular ADR-021 "Retries", "Restart", "Late-write scan", protocol rules 1, 8, 10 and 14, and "Consequences".
- `docs/operations/b10-abrupt-contract.md`, `docs/operations/b10-workflow.md`, `docs/operations/verifier-contract.md` at `675f8372`.
- Code cited in 2.1, 3.2 and 3.8 at `675f8372`; `docs/engineering/dotnet.md` line 55.
- `items/404/plan-b10-restart-redesign.md` at `b471d02d`, lines 269 and 411-415; `items/404/plan-pr3c-full-classification.md` at `48c52669`, sections 3 and 7.
- Item (b)'s plan `items/404/plan-b10-final-judgement.md` at `51a34df2`, section 3.1 (A1 to A8) and F3 to F5, read for the contract.

## 11. Open questions for Hari

None.

Decided, no longer open: Q1 to Q9 (6846521676 points 1 to 9); check B2 and Q10 (6846854445 points 1 and 2).

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract of section 3, a requirement of section 4, or the scope of section 2, including any C5 target outside 2.2 or any workflow file, or when an existing test conflicts outside 2.4's rules. Hitting a section 9 cap pushes `-wip` and hands off to the next session. A design-changing revision of this plan gets one fresh `plan-checker` (docs/agents/factory.md "Item lifecycle" step 1).
