# B10 restart execution part 1 (LoadGen code) contract-grade plan

Revision 5. Issue [#404](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404). Baseline: `main` at `2ed497d6a8d9ea9fe4b0c00ed4f1ce54f3f41e43` (the merged restart redesign, pull request 427).

This plan changes the load generator so that the first B10 restart instance becomes a parent and a self-killing producer child, as ADR-021 now states, and so that the successor reads the child's journal from Blob storage. It removes the stop/checkpoint/pipe protocol from LoadGen and, under decision 6775474492, brings the chart's abrupt Jobs and the phase script's abrupt evidence reader to schema 3 and the new Job names, so the required .NET verification step keeps passing. Section 3 is the contract that part 2 (trial workflow, three-Job orchestration, timeout Job wiring, operations documents) will cite; section 13 lists what Hari has answered and what is still open.

## 0. Changes since Revision 4

Revision 4 is at `984f727879f722ceffa2afaf7f9430ec999196bc`. Two citation updates only, from the authority refresh after that push; no requirement, contract, test or scope changes:
- [6775679271](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775679271) was edited to the heading `## Decision` (updated 2026-11-10T18:26:30Z, same content); the plan now calls it a decision.
- [Decision 6775792095](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775792095) accepts the per-method rule of section 2.4 in place of Q4's whole-file deletion; section 1 item 11 and section 2.4 cite it.

## 0.0 Changes since Revision 3

Revision 3 is at `c96909355b97bb9db77e911e209ea54b2d2cca6d`. A narrow delta check (relayed by the control session on 2026-11-10) confirmed B1 to B3 and N1 to N7 fixed and raised D1 and E1 to E3. Fixes:

| Finding | Fix |
|---|---|
| D1 (blocking): `charts/parchwell/values.schema.json` gates `runtime.mode` in more places than the abrupt conditional; the new modes failed `helm template`. | Section 2.1 item 4 and section 3.13 now name every mode enum the new modes must join, and section 2.2 exempts exactly those entries. Proof below. |
| D1, same class (rule 5): a fourth enum, the conditional that requires `repeat` and `loadgen_job_name` for `start-line`, `loadgen`, `loadgen2` and `verify` (line 794 on `main`), also lists modes. | Added to the same list, so the new modes keep requiring `loadgen_job_name`. Proof below. |
| E1: hard-coded mode literals in `b10_abrupt.py` would raise `b10_abrupt_configuration_invalid` once `execution_modes('C1')` changes. | Section 3.13 names `run()` line 626, the collection loop at lines 691 and 695, and the role mapping in `values()` line 241; renaming them is stated not to change the orchestration order section 2.2 freezes. |
| E2: section 2.4 still asked Q4; T-H4 named no file; the section 8 preamble said every test is C#. | Section 2.4 now says the narrowing awaits Hari's confirmation. T-H1 to T-H4 name their Python files; the preamble covers both. |
| E3: acceptance command 6 matched a stale `.pdb` under `bin/`. | Command 6 now searches `*.cs` only. |

D1 proof, run by this planning session on 2026-11-10 in a scratch worktree of `main` `2ed497d6` (pinned tools at `/opt/parchwell/tools`, helm v4.3.1), then discarded with `git checkout -- charts`. Input: `diagnostic_cases()[0]` from `charts/parchwell/tests/test_b10_abrupt_chart.py` with `runtime.mode` replaced, written with `test_chart.with_case`, rendered by `helm template parchwell charts/parchwell --values <case>`.
- Before any edit: `loadgen-restart` and `loadgen-successor` exit 1, with "value must be one of" errors from the global enum and from the abrupt conditional.
- After the edits of section 3.13 to the four enums plus the role helper (`_helpers.tpl` line 9 adds the two modes to the `loadgen` role): `loadgen-restart` exit 0, Job `synthetic-c3-c1-r1-loadgen-restart`; `loadgen-successor` exit 0, Job `synthetic-c3-c1-r1-loadgen-successor`.
- Same edits, flag true: `loadgen` and `loadgen2` exit 1, "at '/runtime/mode': value must be one of 'loadgen-restart', 'loadgen-successor'" (T-H2).
- Same edits, flag false and no binding configmap: `loadgen` and `loadgen2` exit 0.
- Same edits, `loadgen-restart` without `loadgen_job_name`: exit 1, "missing property 'loadgen_job_name'".

## 0.1 Changes since Revision 2

Revision 2 is at `46e68bf719f745c727c91cfc9b26031498469d9e`. Hari answered its questions "A, accept both" ([decision 6775679271](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775679271)). Revision 3 records those answers and changes nothing else:
- Q7 decided as option A: `infra/scripts/phase/phase.py` line 660 is in scope (section 2.1 item 5, section 3.13), with new test T-H4 proving the hook fires for both abrupt modes and not for plain `loadgen`, and mutation M21.
- The two clear defaults of Revision 2's section 13.3 become decided defaults D7 and D8 (section 12).
- Section 13 now holds only answered questions; nothing is open.

## 0.2 Changes since Revision 1

Revision 1 is at `76d2744ce14cf9bf8f973d5034b0a11d3c5ce673`. A fresh reproduction check reported fail (relayed by the control session on 2026-11-10). Each finding and its fix:

| Finding | Fix |
|---|---|
| B1: section 2.2 wrongly said no CI step validates the rendered abrupt chart through LoadGen. The required step runs `charts/parchwell/tests/check-loadgen-host.sh`, which runs `b10_abrupt_host_acceptance`. Hari widened part 1 (decision 6775474492). | Scope 2.1 adds the chart's abrupt values, schema, templates and tests, and `infra/scripts/phase/b10_abrupt.py` with its tests. New section 3.13 pins those changes. Section 2.2's false sentence is replaced. Acceptance command 9 runs the step; this session ran it on clean `2ed497d6` and saw it pass. Open question Q7 covers the one `phase.py` line the mode rename needs. |
| B2: a `kill` that returns had nowhere to record return value and errno, and its termination class was unstated. | New record kind `signal_failed` (`return_value`, `errno`), child exit code 4, termination class `unexpected_exit` with outcome `b10_abrupt_signal_failed` (sections 3.1, 3.5, 3.6 step 5, 3.8). `b10_abrupt_signal_failed` leaves the `no_kill` reasons; `b10_abrupt_signal_preflight_failed` joins them. New test T-K2, mutation M17. |
| B3: `B10AbruptSupervisorTests.cs` holds tests of retained behaviour; the count was wrong. | Section 2.4 applies the per-method rule to that file too, names the methods kept or adapted, and corrects the count to 46 test methods (48 matched lines included two `Dispose` helpers). New tests T-B1 to T-B3. Hari accepted the deletions (decision 6774981258, Q4); keeping more tests narrows that acceptance. |
| N1: the control set must be computed atomically with the `cut` append. | Section 3.5 and 3.6 step 2 pin it under the shared journal lock. New test T-C5, mutation M18. |
| N2: `MaxFrameBytes` renamed and new keys are defaults, not "kept unchanged". | Section 3.2 reworded; section 12 lists them as decided defaults. |
| N3: wrong source for the report descriptor members. | Section 3.5 cites `ValidateReportDescriptor` in `B10AbruptWire.cs` (declared at line 306 on `main`, members at line 308). |
| N4: `successor_permitted`'s extra conditions need a basis. | Section 12 default D5, basis ADR-021 rules 4 and 11. |
| N5: source of `ProducerDrainTimeout` in the abrupt host. | Section 3.6 step 4 cites `B10AbruptHost.cs` line 247 (300 s), matching `values.yaml` line 39. |
| N6: acceptance greps and builds. | Command 6 adds the four type names; command 2 builds `src/Parchwell.Verifier` in Release first. |
| N7: section 3.12 out of date. | Rewritten to what part 2 keeps under decision 6775474492. |

Also folded in: Hari's answers to Revision 1's questions (decision 6774981258), including Codex as the implementing tool.

## 1. Goal and authority

Goal: implement ADR-021 protocol rules 1 to 11 for the first restart instance and the successor in `src/Parchwell.LoadGen/**`, with tests in `tests/Parchwell.LoadGen.Tests/**`, and pin every interface part 2 depends on.

Authority, in creation order. Catch-up on 2026-11-10: 230 comments on issue 404, listed by script across three pages; newest 6775474492. No state file covers comments after 6746177383 (`items/404/state.md` on `main`), so every decision and approval comment below was read in full, plus every comment the planning prompt names.

1. Issue body of #404 and the decisions carried in `items/404/state.md`, as recorded in the approved design plan's section 1, items 1 to 3.
2. [Decision 6762516530](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530): the simplified restart (points 1 to 7).
3. [Decision 6768228960](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768228960): the no-kill end, A(ii).
4. Answers to the design plan's open questions: [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) (Q3 to Q11, Q13 to Q19), [6769269811](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769269811) (Q1, `CutOrdinal` 4), [6769312013](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769312013) (Q2, 100 originals per second), [6769416981](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769416981) (Q8b), [6769742939](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769742939) (Q12, deployment assumptions), [6769760685](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769760685) (Q20), [6769808426](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769808426) replaced by [6769843248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769843248) (B2), and [6769877985](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769877985) (conditional approval).
5. [Approval 6770899438](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6770899438) of the design plan `items/404/plan-b10-restart-redesign.md` at `d175ed609717610d39b028253603d5a1c9eba820`. This plan uses its section 7 (what carries over), section 9 (never-ran inventory), section 10 (deployment assumptions) and section 14.1 (answers). The design pull request merged as `2ed497d6`, so ADR-021 on `main` is the governing text; where this plan and ADR-021 differ, ADR-021 wins and the stop rule applies.
6. [Decision 6773282644](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6773282644): execution split into part 1 (this plan) and part 2; each needs its own plan, fresh check, plan check and approval; implementing tools go to Hari.
7. [Decision 6774480762](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6774480762): question D1, option A1. The C1 timeout Job runs the ordinary LoadGen host unchanged as `{run}-c1-r1-loadgen`; part 1 changes no ordinary-host code; part 1 renames the first restart and successor Jobs in the abrupt host, with exact literals pinned here and approved with this plan; part 2 must test that diagnostic-trial evidence, including the timeout Job's ordinary evidence under `producer/`, cannot be read or admitted as an ordinary scientific result. The control session relayed that defaults D2, D3 and D4 of this session's report stand unless Hari objects; section 12 lists them.

8. [Decision 6774981258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6774981258): answers to Revision 1's Q1 to Q6. Job names as recommended; implementing tool Codex; `b10_abrupt_cut_rejected` accepted; deletion of the listed tests accepted by name; the operations contract gap accepted; DA7 accepted by name, DA6 settled by the offline test running the child inside the built image. Its last paragraph returns any changed literal or option to Hari.
9. [Decision 6775474492](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775474492): amends 6773282644. Part 1 also includes what the required step "Verify the load generator accepts the rendered chart environment" needs: the chart's B10 abrupt values and templates (including `bounds.b10_abrupt`), `test_b10_abrupt_chart.py`, `test_b10_abrupt_host.py`, and `infra/scripts/phase/b10_abrupt.py` with its tests, as far as schema 3 and the new Job names require. Part 2 keeps the trial workflow change and its committed copy, the C1 timeout Job wiring, the three-Job phase orchestration, the `docs/operations/` rewrite, and decision 6774480762 point 3's test.

10. [Decision 6775679271](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775679271): Hari's answer "A, accept both" to Revision 2's questions. Q7 option A extends 6775474492 to the one render-hook condition at `phase.py` line 660; `B10_MODES` (line 26) and the ordinary B10 collection loop stay unchanged. Accepted: exit code 4, `unexpected_exit` and `b10_abrupt_signal_failed` for a returned `kill`; `cut.unconfirmed_attempt_ids` computed under the journal lock, stored and checked, a disagreement being a capture gap.

11. [Decision 6775792095](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775792095): the per-method rule of section 2.4 replaces the whole-file deletion of `B10AbruptSupervisorTests.cs` accepted in 6774981258 Q4.

Also read: [issue 428](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/428) (operations documents that still describe the stop protocol; the full rewrite is part 2's).

Not authority: the held offline end-to-end test plan and the paused PR3b plan (section 2.3 only lists shared files), and this session's prototype results (section 10, evidence for literals, not decisions).

## 2. Scope

### 2.1 Files allowed to change

1. `src/Parchwell.LoadGen/Run/Abrupt/**`: change, add and delete files to implement section 4. File split and private names are the implementer's choice.
2. `src/Parchwell.LoadGen/Program.cs`: only the four abrupt flags of section 3.1 (lines 41 to 48 on `main`). The reserved-environment refusal at lines 50 to 54 stays byte-identical.
3. `tests/Parchwell.LoadGen.Tests/Abrupt/**`: new tests of section 8, updated existing tests, and deletion of the tests listed in section 2.4 only.
4. Chart, under decision 6775474492 and section 3.13 only: `charts/parchwell/values.yaml` (`runtime.bounds.b10_abrupt`), `charts/parchwell/values.schema.json` (the `b10_abrupt` bounds constant, and four `mode` enums: the global `runtime.mode` enum at lines 92 to 106, the conditional requiring `repeat` and `loadgen_job_name` at line 794, the abrupt conditional at line 646, and the B10 scenario conditional at line 929, all on `main`), `charts/parchwell/templates/jobs.yaml` (the `$abrupt` branches only), `charts/parchwell/templates/_helpers.tpl` (line 9, the role mapping, gains the two abrupt modes), `charts/parchwell/tests/test_b10_abrupt_chart.py`, `charts/parchwell/tests/test_b10_abrupt_host.py`.
5. Phase, under decision 6775474492 and section 3.13 only: `infra/scripts/phase/b10_abrupt.py` (constants, envelope reader, first-publication acceptance, successor binding, successor collection, abrupt mode names), `infra/scripts/phase/tests/test_b10_abrupt.py`, `infra/scripts/phase/tests/check_b10_abrupt_producers.py`. `infra/scripts/phase/phase.py` only at the render-hook condition, line 660 on `main` (decision 6775679271, Q7 option A); `B10_MODES` (line 26) and the ordinary B10 collection loop (line 950) stay byte-identical.
6. `items/404/state.md`, under docs/agents/factory.md "Item state file", and a new receipt `items/404/execution-loadgen-receipt.md`. The implementing tool is Codex (decision 6774981258), so the branch is `codex/` with the issue number and every commit carries the Codex trailer (AGENTS.md "Implementer and reviewer").

No `*.csproj`, `Directory.*.props`, `packages*.lock.json`, solution file, new project, assembly reference or NuGet package. `src/Parchwell.LoadGen`, `tests/Parchwell.LoadGen.Tests`, `charts/` and `infra/scripts/` have no owner in `.github/CODEOWNERS`, so no protected path is touched; an agent may merge under AGENTS.md GOVERNANCE "Merge rule".

### 2.2 Paths that must not change

- Every ordinary-host file: everything under `src/Parchwell.LoadGen/` outside `Run/Abrupt/` and outside `Program.cs` lines 41 to 48 (decision 6774480762 point 1). This includes `Run/Input/LoadGenHostOptions.cs`, `Run/Restart/**`, `Run/Execution/**`, `Run/Publication/**` and `Run/Evidence/**`.
- `src/Parchwell.Kafka/**`, `src/Parchwell.Verifier*/**`, `src/Parchwell.Scenarios/**`.
- Every file under `charts/` and `infra/` not named in section 2.1, and in the named chart files every line outside the abrupt branches and the four enum entries and helper line named in section 2.1 item 4, so ordinary and legacy `loadgen2` rendering is unchanged (`charts/parchwell/tests/test_chart.py` keeps passing unedited).
- In `infra/scripts/phase/b10_abrupt.py`: the trial orchestration order in `run()` (no third Job; renaming the mode literals of section 3.13 is not a change of order), the start-line metadata checks, including `expected_job_names` (the start line is produced by `src/Parchwell.Verifier`, a protected path and part 2's), and the failure-diagnostics machinery.
- `.github/**`, `docs/**`, `scripts/**`, `scenarios/**` (no `expected.yaml`), `results/**`, `README.md`, `AGENTS.md`, `CLAUDE.md`.
- `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs` (shared with the held offline test plan; this plan uses it unchanged).
- Every test file outside `tests/Parchwell.LoadGen.Tests/Abrupt/`.

Consequence, accepted by this scope and stated for part 2: after part 1 merges, the chart renders the parent and successor Jobs that the new LoadGen accepts, and the phase script reads schema-3 evidence. The required step "Verify the load generator accepts the rendered chart environment" (`.github/workflows/dotnet-verification.yml` line 87) checks both through `b10_abrupt_host_acceptance`. The trial orchestration still runs two Jobs and the start line still expects the old names, so a dispatched trial would fail; no trial can run before part 2 (decisions 6761783572 and 6762516530). `docs/operations/b10-abrupt-contract.md` line 5 already says the page describes the superseded code until the implementation pull requests replace it; its line 174 link to `B10AbruptSupervisor.cs` will dangle until part 2 (accepted, decision 6774981258, Q5).

### 2.3 Files shared with other open plans on issue 404

| Other plan | Shared file | What it may change there | Effect on this plan |
|---|---|---|---|
| Offline end-to-end test (`items/404/plan-offline-e2e-test.md`, Revision 1 at `f75d6ac3`, held) | `tests/Parchwell.LoadGen.Tests/Abrupt/` (new files), `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs` | New test files; one optional parameter on the fixture | This plan changes the fixture not at all. Stop rule 3 if the fixture changes on `main` first. Its retargeting follows this plan's section 3. |
| PR3b classification (`items/404/plan-pr3b-classification.md`, Revision 2 at `37128767`, paused) | `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs` | An environment switch for its schema-2 export | This plan replaces that file's schema-2 producer with schema 3 (section 2.4). PR3b must be revised against section 3 before it resumes. Stop rule 3 if PR3b changes the file on `main` first. |
| PR3b classification | `items/404/state.md` | Its own state update | Stop rule 3. |
| Part 2 (not yet planned) | `infra/scripts/phase/b10_abrupt.py`, `infra/scripts/phase/phase.py`, `charts/parchwell/**` | Three-Job orchestration, timeout Job wiring | Part 2 plans against this pull request's merged text; section 3 is the interface. |

### 2.4 Tests deleted with the code they test

The rule for every file below: delete only a test method whose subject is code that ADR-021 protocol rule 15 removes (stop, resume, checkpoint, pipe framing, supervisor journal, cohort seals, kill selection, burst). Keep or adapt every method whose subject survives (report binding, terminal reconciliation, wire validation, statistics, cgroup, failure-report reduction, publication, successor completion). The receipt lists every deleted method with the removed member it exercised, and every kept or adapted method with its new home. Hari accepted the deletions by name (decision 6774981258, Q4); keeping a method this rule keeps narrows that acceptance, and decision 6775792095 accepts the per-method rule in place of the whole-file deletion.

- `B10AbruptSupervisorTests.cs` (46 test methods, counted by script on `main`; Revision 1's 48 counted two `Dispose` helpers at lines 1492 and 1531). Kept or adapted, at least: `Given_AcceptedWithoutReport_When_LateReportArrives_Then_OnlySampleMissingPersists` (line 26) and `Given_DuplicateReport_When_AcceptedArrives_Then_OnlyBindingInvalidPersists` (line 38), retargeted to the child's `pre_acceptance_report` record; `Given_GenuineTerminal_When_CapturedAroundAcceptance_Then_RecordIsRetained` (line 145), retargeted to the journal reader; `Given_OversizeFailure_When_Reporting_Then_OriginalSurvivesAndDiagnosticReduces` (line 960) and `Given_OversizeCleanupCause_When_Reporting_Then_BuildReducesItAndWrites` (line 984), retargeted to the failure report section 3.9 keeps. `Given_NoDurableAcceptance_When_Finalizing_Then_ReportsKillTooEarly` (line 17) is replaced by T-R1, and the exposure-inconclusive method by T-C3. The file itself may be deleted once its kept methods have moved.
- `B10AbruptProcessTests.cs`: methods that exercise `RequestStop`, `ConfirmStopped`, `Resume`, `RequestKill`, `EventsReady` or the pipe handshake. `/proc` stat parsing tests stay if `B10AbruptProc` stays. `B10AbruptPublishedApphostTests` (line 474) stays, because `test_b10_abrupt_host.py` runs it.
- `B10AbruptJournalTests.cs`, `B10AbruptProducerTests.cs`, `B10AbruptWireProducerTests.cs`: methods whose subject is the supervisor-assigned journal, IPC messages, checkpoints, seals, the timeout cohort in the child, or schema 2.

AGENTS.md "Code Review Rules" flags a deleted test in the same pull request as its code as P1. Hari accepted these deletions by name (decision 6774981258, Q4); reviews answer that P1 by citing it and this section.

## 3. External contracts (the interface part 2, the offline test and PR3b cite)

Every literal here is pinned. "Policy" marks a value fixed by Hari's decisions or by this plan's approval, not a measurement.

### 3.1 Processes, flags and exit codes

| Process | Command | Exit codes |
|---|---|---|
| Parent (first restart pod) | `/app/Parchwell.LoadGen --b10-abrupt-parent` | `0`: `termination.json` published create-only and read back, whatever the termination. `1`: any failure before that. `124`: the existing 900 s watchdog. |
| Child (started only by the parent) | `<directory of the running apphost>/Parchwell.LoadGen --b10-abrupt-child` (the existing `ChildCommand.ForRunningApphost` path check is kept) | `137`: observed for the self-SIGKILL (section 10; this is the value ADR-021 rule 7 and Q15 make the gate). `3`: no-kill path (ADR-021 rule 7). `4`: `kill` returned (section 3.6 step 5). `1`: failure before the journal's first record. |
| Successor | `/app/Parchwell.LoadGen --b10-abrupt-successor` | `0`: `completion.json` published. `1`: any failure. |
| Validation | `--validate-b10-abrupt-configuration` | unchanged: `0` and `{"b10_abrupt_configuration_valid":true}`, else `1`. |

`--b10-abrupt-supervisor` is removed; with it, `Program.RunAsync` returns 1 as for any unknown argument (ordinary path unchanged).

### 3.2 Configuration keys

Prefix `B10Abrupt__`, read from the environment only. Identity and path keys kept with their meaning on `main`: `Enabled` (`true`), `Purpose` (`diagnostic_trial`), `ScenarioId` (`B10`), `ConfigurationId` (`C1`), `Phase` (`C1`), `Repeat` (`1`), `RunId`, `SourceCommit`, `ImageReference`, `JobName`, `PodName`, `PodUid`, `NamespaceName`, `BlobEndpoint`, `ScenarioSourceRoot`, `OutputRoot`, `StartLineBindingPath` (parent and child), `PredecessorBindingPath` (successor only).

- `Role`: exactly `parent`, `child` or `successor`. `supervisor` is rejected (`validation_detail=Role`).
- Removed and rejected (`SettingNameOrDuplicate`): `EventsPipeHandle`, `AckPipeHandle`, `IdentityChallenge`, `ArmTimeoutSeconds`, `KillTimeoutSeconds`, `ReapTimeoutSeconds`, `EofTimeoutSeconds`, `MaxFrameBytes`.
- Fixed policy keys, accepted only at their value (`FixedPolicy`). `MaxRecordBytes` replaces `MaxFrameBytes` with the same value; `ProbeRate` and `CutOrdinal` are new keys for the decided values (decided defaults, section 12): `MaxRecordBytes` 65536, `QueueCapacity` 256, `MaxJournalBytes` 16777216, `MaxJournalEntries` 4096, `MaxAuxiliaryBytes` 65536, `MaxJsonDepth` 32, `ReleaseDelaySeconds` 120, `ProbeRate` 100, `CutOrdinal` 4.
- The parent passes its own settings to the child with `Role` set to `child`; it adds no other key. The child refuses `PredecessorBindingPath`.

### 3.3 Job names (decision 6774480762 point 2; literals decided in 6774981258, Q1)

| Job | Name | Host |
|---|---|---|
| C1 timeout Job | `{RunId}-c1-r1-loadgen` | ordinary host, unchanged |
| First restart Job | `{RunId}-c1-r1-loadgen-restart` | abrupt host, role `parent` and its child |
| Successor Job | `{RunId}-c1-r1-loadgen-successor` | abrupt host, role `successor` |

With `RunId` at its 29-character maximum the names are 51 and 53 characters, and a Job pod name adds 6 (computed by script), within the 63-character label limit. The abrupt host rejects any other `JobName` for its role with `validation_detail=JobName`; it rejects `-loadgen` and `-loadgen2` for every role. The child carries the parent's `JobName`. In the chart these are the Job names of the two abrupt modes of section 3.13.

### 3.4 Envelope and identity (schema version 3)

Every journal record and every JSON object below carries `schema_version: 3`, `dialect: "b10_abrupt"`, `purpose: "diagnostic_trial"`, `synthetic: true`, `scientific_admissible: false`, `kind` and `identity`. Identity members are unchanged from schema 2: `run_id,scenario_id,configuration_id,phase,repeat,source_commit,image_reference,job_name,pod_name,pod_uid,instance_id,predecessor_instance_id`; `instance_id` equals `pod_name`; parent and child share the parent's identity; `predecessor_instance_id` is null except in the successor. A schema-2 object is rejected by every new reader. JSON: no duplicate member, no unknown member, no trailing content, depth at most 32, UTC strings exactly as ADR-021's current `IsUtc` rule (33 characters, `O` format, `+00:00`).

### 3.5 Journal `journal.jsonl`

Written only by the child, at `<OutputRoot>/abrupt/journal.jsonl` (`/work/loadgen/abrupt/journal.jsonl` in the chart), opened `FileMode.CreateNew`. One record per line: UTF-8 JSON, then byte `0x0A`. Record members: the envelope, then `sequence` (from 1, plus 1 per record, assigned under one lock shared by both writing threads), `observed_utc`, `monotonic_ticks`, `clock_frequency`, `data`. After each record the child calls `FileStream.Flush()` before the next record or action (Q4 of the design plan). Limits are charged before writing: a record above 65536 bytes, more than 4096 records or more than 16777216 bytes latches the matching existing gap reason. The reason strings are unchanged: `frame_oversize`, `queue_full`, `journal_bytes_exceeded`, `journal_entries_exceeded`, `writer_failed`, `malformed_frame`, `sequence_gap`.

Record kinds and their `data` members (closed):

| Kind | Writer | `data` members |
|---|---|---|
| `child_started` | producing thread, first record | `pid`, `start_time_ticks` (from `/proc/self/stat` field 22, existing parser), `boot_id` |
| `prepared` | producing thread, before `Produce` | `attempt_id`, `arm` (`same_instance` or `cross_restart`), `role` (`original` or `resend`), `ordinal` (0 to 4; a resend keeps its original's), `probe_id`, `key`, `sequence` (equals `ordinal`), `payload_length`, `payload_sha256`, `predecessor_attempt_id` (null for originals) |
| `pre_acceptance_report` | producing thread, after the arm's ordinal-0 original's `prepared` and before its `accepted` | `arm`, `attempt_id`, `name` (`<arm>-pre-acceptance-report.json`), `length`, `sha256`, `report_identity`, `report_sequence`, `report_capture_utc` (members and bounds as the schema-2 report descriptor, `ValidateReportDescriptor` in `B10AbruptWire.cs`, declared at line 306 on `main` with its member list at line 308, with `arm` and `attempt_id` added and `name` without the `abrupt-child/` directory) |
| `accepted` | producing thread, after `Produce` returns | `attempt_id`, `prepared_sequence` |
| `synchronous_rejection` | producing thread, when `Produce` throws | `attempt_id`, `prepared_sequence`, `error_code`, `error_reason` |
| `terminal` | journal writer thread | `attempt_id`, `completion_source`, `delivered`, `error_code`, `persistence_status` (unchanged members) |
| `native_error` | journal writer thread | unchanged members, `capture_sequence` from 1 without gap |
| `cut` | producing thread, control arm only, right after ordinal 4's `accepted` | `arm` (`same_instance`), `cut_attempt_id`, `release_utc` (this record's `observed_utc` plus 120 s), `unconfirmed_attempt_ids` (the control set, computed while holding the journal lock in the same critical section that appends this record) |
| `control_drained` | producing thread | `flush_returned` (bool), `out_queue_length` |
| `post_drain_report` | producing thread, after `control_drained` | as `pre_acceptance_report`, with `arm` `same_instance`, `attempt_id` null, `name` `control-post-drain-report.json` |
| `kill_intent` | producing thread, cross-restart arm, right after ordinal 4's `accepted` | `arm` (`cross_restart`), `cut_attempt_id`, `release_utc` (this record's `observed_utc` plus 120 s) |
| `signal_failed` | producing thread, only after `kill_intent`, when `kill` returns | `return_value`, `errno` (from `Marshal.GetLastPInvokeError`, as the existing preflight records it) |
| `no_kill` | producing thread | `reason` (section 3.9), `cross_restart_admitted` (bool) |
| `gap` | either thread, when still able to write | `reason`, `first_missing_sequence` (nullable), `capture_status` |

The control set is computed atomically with the `cut` append: under the one journal lock, the child reads the terminal records already appended, computes the set, and appends `cut`; a terminal the writer thread appends after that lock is released is after `cut` in journal order and does not remove a member. Readers recompute the set from journal order and require it equal to `unconfirmed_attempt_ids`; a difference latches `malformed_frame`. Delivery and error callbacks only enqueue to a bounded in-memory queue of 256 entries; they do no file or network I/O. A full queue latches `queue_full`.

Reader rules (one shared reader, used by the parent and the successor): every line must parse and validate; a final byte other than `0x0A` (a torn final record) or any malformed line latches `malformed_frame`; a sequence that is not the previous plus 1 latches `sequence_gap`. A gap is sticky and fails capture (Q9 of the design plan). "Producing-thread record" means a record of a kind the table assigns to the producing thread.

### 3.6 Probe policy and child sequence

1. `child_started`. Construct the producer (B10 C1 settings, unchanged). Wait for a parsed statistics report as the current code does.
2. Control arm `same_instance`: originals ordinal 0 to 4, invocation `i` not before `i * 10 ms` after ordinal 0's invocation (100 per second). After ordinal 4's `accepted`: `cut`, with the set computed atomically as section 3.5 states. The control unconfirmed set is the arm's accepted originals with no `terminal` or `synchronous_rejection` before the `cut` record, in invocation order. Empty: `no_kill` with `b10_abrupt_exposure_inconclusive`.
3. At `release_utc`, resend each member once, in invocation order (new attempt ID, same instance, same key, sequence and payload bytes).
4. `Flush` within the existing `ProducerDrainTimeout` (300 s: `B10AbruptHost.cs` line 247 on `main`, matching `charts/parchwell/values.yaml` line 39), then `control_drained`, then the first parsed statistics report captured after the drain as `post_drain_report`. Complete only when `flush_returned` is true, `out_queue_length` is 0, every control original and resend has a terminal record, and the report exists; otherwise `no_kill` with `b10_abrupt_control_incomplete`.
5. Cross-restart arm `cross_restart`: originals ordinal 0 to 4 at the same pacing. After ordinal 4's `accepted`: `kill_intent`, then libc `kill(getpid(), 9)` once. No `Flush` or `Dispose` after this arm's ordinal 0 is admitted. If `kill` returns, the child writes `signal_failed` with the return value and errno and exits 4 by `Environment.Exit(4)`. This is not the no-kill path, because `kill_intent` was reached (ADR-021 rule 7: "A `kill` call that returns records its return value and errno and fails the chain").
6. No-kill path: any failure before `kill_intent` writes `no_kill` (best effort; a gap may prevent it). Before cross-restart admission: `Flush` within `ProducerDrainTimeout`, dispose, exit 3. After admission: `Environment.Exit(3)`, which runs no `finally` block ([Environment.Exit](https://learn.microsoft.com/en-us/dotnet/api/system.environment.exit?view=net-10.0), Remarks).
7. Keys, probe IDs and payloads: unchanged from `main` (`B10/{run_id}/C1/1/same_instance`, `.../cross_restart`; payload is the first generated profile event, identical bytes for every attempt of an arm). Sequence is the ordinal.
8. If an arm's ordinal-4 original is synchronously rejected, there is no cut: in the cross-restart arm with no accepted original, `no_kill` with `kill_too_early`; otherwise `no_kill` with `b10_abrupt_cut_rejected` (new name, Q3). Rejections of ordinals 0 to 3 are recorded and the arm continues.

### 3.7 Parent

1. Write `registration.json` to Blob (create-only), as on `main`.
2. Read `memory.events.local` (`B10AbruptCgroup.CaptureBefore`, retargeted to the parent's own process).
3. `Process.Start` the child with no redirection and no inherited handle beyond the defaults. Read the child's `/proc/<pid>/stat` start time and `/proc/sys/kernel/random/boot_id`.
4. `WaitForExit(bound)`, where `bound` is 720 s minus the parent's elapsed time since start: the 900 s process lifetime less 180 s, which is 6 post-death uploads times the existing 30 s `UploadTimeout` (Q8b text "the remaining LoadGen process lifetime less the time its publication needs"; arithmetic by script). The parent sends no signal and opens no pipe.
5. After death: read `memory.events.local` again; read the journal (16777216-byte limit); apply the reader rules; compute termination (section 3.8); hash and publish.
6. If the bound expires: termination `unknown`, failure `b10_abrupt_exit_unobserved`, publish what exists, exit 0 after `termination.json`; never signal the child.

### 3.8 Termination classes

`termination.json` `data.termination` is exactly one of:

- `intended_self_kill`: exit observed, `ExitCode` 137, last producing-thread record is `kill_intent`, `child_started` matches the parent's `/proc` reads, cgroup identity stable and `oom_kill` unchanged.
- `no_kill`: exit observed, a durable `no_kill` record, no `kill_intent`. Exit code recorded, not gated (ADR-021 rule 7).
- `unexpected_exit`: exit observed and neither of the above (includes `kill_intent` with an exit code other than 137, and a producing-thread record after `kill_intent`). Failure `b10_abrupt_unexpected_exit`, except that a durable `signal_failed` record after `kill_intent` gives outcome `b10_abrupt_signal_failed` and copies its `return_value` and `errno` into `data.signal_observation`.
- `unknown`: death not observed within the bound.

`data.outcome`: for `intended_self_kill`, `kill_too_late` with `detail` `no_unconfirmed_attempt` when the kill unconfirmed set is empty, else null; for `no_kill`, the record's `reason`; otherwise the failure name. `data.signal_observation` is null except as stated for `signal_failed`.

`data.successor_permitted` is true exactly when termination is `intended_self_kill`, the kill unconfirmed set is nonempty, no capture gap is latched and publication succeeded (the last two conditions are decided default D5, section 12). Part 2's workflow starts the successor only on true, and treats `unknown` as `safety_stop`.

Other `termination.json` `data` members: `child` (`pid`, `start_time_ticks`, `boot_id`, `parent_observed_start_time_ticks`), `exit_observed`, `exit_code_local_observation`, `runtime_version`, `wait_bound_milliseconds`, `cgroup_before`, `cgroup_after`, `control_unconfirmed_attempt_ids`, `kill_unconfirmed_attempt_ids`, `capture_gaps`, `native_error_count`, `native_fatal_count`, `native_tail` (`unknown_due_to_intended_kill` only for `intended_self_kill`, else null), `manifest` (descriptor of `abrupt-manifest.json`).

### 3.9 Failure and outcome names

Retired, never emitted again (Q7 of the design plan): `b10_abrupt_target_invalid`, `b10_abrupt_stop_unconfirmed`, `b10_abrupt_resume_unconfirmed`, `b10_abrupt_eof_incomplete`.

Kept, with their meaning narrowed to the new protocol: `b10_abrupt_diagnostic_not_scientific`, `b10_abrupt_configuration_invalid`, `b10_abrupt_platform_unsupported`, `b10_abrupt_signal_preflight_failed` (the child's own-PID null-signal preflight before producer construction), `b10_abrupt_child_start_failed`, `b10_abrupt_child_identity_mismatch` (`child_started` differs from the parent's `/proc` reads), `b10_abrupt_signal_failed`, `b10_abrupt_exit_unobserved`, `b10_abrupt_unexpected_exit`, `b10_abrupt_capture_gap`, `b10_abrupt_sample_missing`, `b10_abrupt_report_binding_invalid`, `b10_abrupt_guard_failed`, `b10_abrupt_control_incomplete`, `b10_abrupt_exposure_inconclusive`, `kill_too_early`, `kill_too_late` (detail `no_unconfirmed_attempt`), `b10_abrupt_cgroup_unavailable`, `b10_abrupt_oom_conflict`, `b10_abrupt_publication_failed`, `b10_abrupt_publication_ambiguous`, `b10_abrupt_successor_binding_invalid` (details unchanged), `b10_abrupt_successor_late`, `b10_abrupt_deadline_exhausted`. `b10_abrupt_first_termination_unknown` stays part 2's.

New (decided in 6774981258, Q3): `b10_abrupt_cut_rejected`.

`no_kill.reason` is one of: `b10_abrupt_exposure_inconclusive`, `b10_abrupt_control_incomplete`, `kill_too_early`, `b10_abrupt_cut_rejected`, `b10_abrupt_sample_missing`, `b10_abrupt_report_binding_invalid`, `b10_abrupt_guard_failed`, `b10_abrupt_signal_preflight_failed`, `b10_abrupt_deadline_exhausted`, `b10_abrupt_capture_gap`. `b10_abrupt_signal_failed` is never a `no_kill` reason (section 3.6 step 5).

The stderr failure-report format, `ValidationDetail` list (minus `ChildFields` and `ChildHandshake`, which become unreachable and are removed) and the diagnostic object shape on `main` are kept. Supervisor stage names and the `b10_abrupt_burst` lines are removed; child stages become `child_started, producer_constructed, probe_prepared, probe_accepted, cut_written, release_reached, control_drained, kill_intent_written, no_kill_written`; parent stages `parent_registered, child_started, exit_observed, journal_read, publication_complete, failure_observed`.

### 3.10 First restart publication

Container `ledger`, prefix `<run_id>/C1/r1/b10-abrupt-diagnostic/<pod_name>/`, create-only, read back, in this order: `registration.json` (at start), `journal.jsonl`, each report file the journal binds (`same_instance-pre-acceptance-report.json`, `control-post-drain-report.json`, `cross_restart-pre-acceptance-report.json`, in journal order, only those bound), `abrupt-manifest.json`, `termination.json` last. Removed members: `timeout-seal.json`, `control-seal.json`, the six cohort files, `kill-selection.json`.

`abrupt-manifest.json` `data`: `journal` (descriptor), `journal_records`, `report_files` (descriptors in journal order, each equal by length and SHA-256 to its binding record), `gaps`. A report file that is missing or differs from its binding fails publication with `b10_abrupt_publication_failed` before `abrupt-manifest.json`. Descriptors are `{name,length,sha256}`, lowercase hex.

### 3.11 Successor

Binding file at `PredecessorBindingPath`, built by `accept_diagnostic_publication` in `infra/scripts/phase/b10_abrupt.py` (section 3.13) and mounted by the successor's configmap as on `main`, exact members: `first_pod` (`job_name`, `pod_name`, `pod_uid`, `image_reference`, `source_commit`, `finished_utc`), `termination` (`{name:"termination.json",length,sha256}`), `start_line_binding` (unchanged shape), `runner_utc`, `runner_monotonic_ticks`, `cross_host_timing` (`not_verified`).

The successor downloads `termination.json`, `abrupt-manifest.json` and `journal.jsonl` from the first pod's prefix, checks `termination` against the binding descriptor, the manifest against `termination.data.manifest`, and the journal against the manifest; requires `successor_permitted` true; recomputes the kill unconfirmed set with the shared reader and requires it equal to `kill_unconfirmed_attempt_ids` (else detail `selection_stale`); requires `first_pod.job_name` to be `{RunId}-c1-r1-loadgen-restart`. It registers with `predecessor_instance_id` the first pod's instance, waits until `kill_intent.release_utc`, resends each member once in invocation order with new instance and attempt IDs and the original's key, sequence and payload (payload SHA-256 must equal the `prepared` record's), then drains and publishes its existing seven members (`registration.json`, `ledger.jsonl`, `manifest.json`, `statistics.jsonl`, `request-lifecycle.jsonl`, `capabilities.json`, `completion.json`). `completion.json` replaces `selection` with `kill_unconfirmed_attempt_ids` and adds `journal` (descriptor). Not ready at `release_utc`: `b10_abrupt_successor_late`, no resend.

### 3.12 What part 2 must do (named so its plan can cite it)

After decision 6775474492, part 1 delivers the chart's abrupt Jobs and the phase script's schema-3 reader and binding (section 3.13). Part 2 keeps:
1. The C1 timeout Job wiring: an ordinary `loadgen` Job named `{run}-c1-r1-loadgen` in the diagnostic trial, with no `B10Abrupt__` setting.
2. Phase orchestration of the three Jobs in ADR-021 rule 1 order in `infra/scripts/phase/b10_abrupt.py` `run()` and `phase.py`; starting the successor only when `successor_permitted` is true and first-pod termination is observed; mapping `unknown` to `safety_stop`; and the start line's `expected_job_names` for three Jobs (ADR-021 consequences).
3. Decision 6774480762 point 3: a test that diagnostic-trial evidence, including the timeout Job's ordinary evidence under `<run>/C1/r1/producer/`, cannot be read or admitted as an ordinary scientific result.
4. The trial workflow change with its committed copy under AGENTS.md hard safety rule 8 (Hari merges), and the `docs/operations/` rewrite (issue 428).

### 3.13 Chart and phase changes (decision 6775474492)

Chart:
- Abrupt modes are `loadgen-restart` and `loadgen-successor`, allowed only with `b10_abrupt_diagnostic` true. In `values.schema.json` the abrupt conditional's enum (line 646) becomes exactly those two; the global `runtime.mode` enum (lines 92 to 106), the conditional requiring `repeat` and `loadgen_job_name` (line 794) and the B10 scenario conditional (line 929) each gain both, keeping every existing entry. `_helpers.tpl` line 9 adds both to the `loadgen` role. The existing Job-name helper then yields the section 3.3 names. Ordinary modes `loadgen` and `loadgen2`, and their rendering, are unchanged.
- `loadgen-restart`: `args` `["--b10-abrupt-parent"]`, `B10Abrupt__Role` `parent`, `B10Abrupt__StartLineBindingPath` `/bindings/b10-abrupt/start-line.json`. `loadgen-successor`: `args` `["--b10-abrupt-successor"]`, `B10Abrupt__Role` `successor`, `B10Abrupt__PredecessorBindingPath` `/bindings/b10-abrupt/predecessor.json`. Command stays `["/app/Parchwell.LoadGen"]`.
- `runtime.bounds.b10_abrupt` and its schema constant become exactly the nine fixed policy keys of section 3.2 with their values.
- `test_b10_abrupt_chart.py` and `test_b10_abrupt_host.py` assert these literals; the five rejected inputs keep their details (`Role`, `SettingNameOrDuplicate`, `DiagnosticIdentity`, `Binding`, `SettingNameOrDuplicate`), and the host test adds `B10Abrupt__Role=supervisor` rejected with `Role` and `B10Abrupt__JobName={run}-c1-r1-loadgen` rejected with `JobName`. Its final line becomes `b10 abrupt host checks passed: 2 rendered environments, 7 rejected inputs, production-byte bridge`.

Phase (`infra/scripts/phase/b10_abrupt.py`):
- `FIRST_FILES` is `registration.json`, `journal.jsonl`, `abrupt-manifest.json`, `termination.json`, plus the bound report files of section 3.10 read from the manifest; `COHORT_FILES` and `COHORT_LIMITS` are removed; `SUCCESSOR_FILES` is unchanged.
- The envelope reader requires `schema_version` 3 (an integer) and rejects 2.
- `accept_diagnostic_publication` reads the journal with the section 3.5 rules independently of the C# reader, checks the manifest and termination receipt chain, requires `successor_permitted` true, recomputes both unconfirmed sets and requires them equal to the receipt's, and returns the section 3.11 binding.
- `collect` checks the successor's `completion.json` as section 3.11 states.
- `execution_modes('C1')` returns `('loadgen-restart', 'loadgen-successor')`. Every hard-coded abrupt mode literal follows: `run()` line 626 `self.job('loadgen', deadline)` becomes `'loadgen-restart'`; the collection loop's `('loadgen', 'loadgen2')` at line 691 and `'loadgen2'` at line 695 become the two new modes and `'loadgen-successor'`; the role mapping in `values()` line 241 maps `loadgen-restart` to `first`; `release_successor` runs `'loadgen-successor'`. Without these, `values()` (line 239) would raise `b10_abrupt_configuration_invalid` and break the six tests that call `self.trial.run()` (`test_b10_abrupt.py` lines 200, 394, 544, 595, 621 and 696 on `main`, found by script; the check named five). These are renames of the same two Jobs, not a change of the orchestration order frozen by section 2.2; part 2 adds the timeout Job.
- `check_b10_abrupt_producers.py` and `ProducedBytesCases` read the export's `first/` members named by `FIRST_FILES` and the manifest, and the mutation cases become schema-3 cases (T-H3).
- `infra/scripts/phase/phase.py` line 660 changes from `mode in ("loadgen", "loadgen2")` to `mode in ("loadgen-restart", "loadgen-successor")` (decision 6775679271, Q7 option A). No other line of `phase.py` changes.


## 4. Requirements

R1. WHEN the parent runs, THEN it writes `registration.json`, reads the cgroup, starts exactly one child process, waits with `WaitForExit` for at most the section 3.7 bound, and never calls `kill`, `Process.Kill`, or any signal API, and never creates a pipe.

R2. WHEN the child runs, THEN its first journal record is `child_started` and every other record follows section 3.5 and 3.6, with `FileStream.Flush()` after each record.

R3. WHEN a delivery or error callback fires, THEN it only enqueues; the journal writer thread appends the `terminal` or `native_error` record.

R4. WHEN the control arm's ordinal 4 is accepted, THEN `cut` follows immediately; at `release_utc` the child resends exactly the control unconfirmed set once, in invocation order, even if members completed after the cut.

R5. WHEN the control set is empty, THEN the child writes `no_kill` with `b10_abrupt_exposure_inconclusive`, flushes, disposes and exits 3, and no cross-restart original is prepared.

R6. WHEN the control arm is incomplete under section 3.6 step 4, THEN `no_kill` with `b10_abrupt_control_incomplete`, exit 3, no cross-restart original.

R7. WHEN the cross-restart arm's ordinal 4 is accepted, THEN `kill_intent` follows immediately and the child sends SIGKILL to its own PID once; no `Flush` or `Dispose` is called after the arm's ordinal 0 is admitted.

R8. WHEN the child dies, THEN the parent classifies termination exactly as section 3.8, including exit code 137 as the only value that can yield `intended_self_kill`.

R9. WHEN the journal has a torn final record, a malformed line or a sequence gap, THEN the reader latches the section 3.5 gap, `successor_permitted` is false, and publication of diagnostics continues.

R10. WHEN the parent publishes, THEN members and order are exactly section 3.10, each create-only and read back; a missing or mismatched bound report fails with `b10_abrupt_publication_failed` before the manifest.

R11. WHEN the successor starts, THEN it performs every check of section 3.11 before producer construction, and fails with the named `b10_abrupt_successor_binding_invalid` detail on any mismatch.

R12. WHEN the successor resends, THEN it resends every member of the recomputed kill set once, at or after `kill_intent.release_utc`, with the original's key, sequence and payload bytes and new instance and attempt IDs, and never a control or timeout attempt.

R13. WHEN any configuration key, role or Job name differs from section 3.2 and 3.3, THEN the host exits 1 with `b10_abrupt_configuration_invalid` and the named detail, before any producer or child exists.

R14. WHEN LoadGen is built, THEN no type, file or flag of the stop protocol remains: no `OwnedProducerProcess` pipe, STOP, CONT or poll; no `B10AbruptChildChannel`; no `B10AbruptSupervisor`; no checkpoint, seal or `kill-selection.json` writer; no child timeout cohort.

R15. WHEN the ordinary host runs (no arguments, `LoadGen__` settings), THEN its behaviour is byte-for-byte that of `main` (decision 6774480762 point 1).

R16. WHEN `charts/parchwell/tests/check-loadgen-host.sh` runs, THEN it passes and prints `b10 abrupt host checks passed: 2 rendered environments, 7 rejected inputs, production-byte bridge`, with the section 3.13 literals asserted.

R17. WHEN the child's `kill` returns, THEN it appends `signal_failed` with the return value and errno and exits 4, and the parent classifies `unexpected_exit` with outcome `b10_abrupt_signal_failed`.

R18. WHEN the control arm's `cut` is appended while terminals race in, THEN the set in `cut.unconfirmed_attempt_ids` equals the set recomputed from journal order.

## 5. Invariants and safety rules

- AGENTS.md hard safety rules 1 to 11: no Azure credentials, no apply, no dispatch, no workflow change, no secret. The Codex mention is never written.
- Verdict integrity: no change under `results/`, no `expected.yaml`.
- Every number in the code or receipt that is not a policy value or an existing configured limit is a labelled local observation (rule 11).
- The parent never signals the child (cold reviewer property P1, section 8.3); the child signals only its own PID.
- No file or network I/O on the delivery or error callback path (property P2).

## 6. Never-ran inventory (part 1's stages)

From the design plan's section 9, narrowed to part 1. "Prototype" means this session's scratch program (section 10), not repository code.

| Stage | Executed anywhere? | Test this plan requires |
|---|---|---|
| Parent writes registration, starts child, `WaitForExit`, no pipe | Prototype only | T-P1 to T-P3 (real child process) |
| Child journal with two writing threads and `Flush()` per record | Prototype only, with real Confluent.Kafka against local Kafka | T-J1 to T-J4, T-E1 |
| Control cut, release, resend, drain, completeness | Prototype only (release shortened to 2000 ms there) | T-C1 to T-C4, T-E1 |
| Kill intent then self-SIGKILL, exit code 137 | Prototype only | T-K1, T-E1 |
| No-kill path, exit code 3 | Prototype only | T-N1 to T-N3 |
| Parent termination classes and publication | No | T-P4 to T-P8 |
| Successor reads journal from Blob, recomputes the set, resends | No | T-S1 to T-S5 (test storage double), T-E2 (real successor process, real Kafka) |
| Chart renders parent and successor Jobs that LoadGen accepts; phase reads schema-3 bytes | No (old shapes pass on `main`) | T-H1 to T-H3, acceptance command 9 |
| On AKS: all of the above | No | Out of part 1; part 2 and the trial |

## 7. Deployment assumptions

Carried from the design plan's section 10 and decision 6769742939, unchanged: DA1 (flushed `emptyDir` journal intact after the self-SIGKILL) and DA5 (`WaitForExit` observes death with no pipe) are owned by the offline end-to-end test before the trial; DA2, DA3 and DA4 are accepted by name for the one-repeat trial only. Part 1's local tests show DA1 and DA5 on this workspace's ext4 and kernel only, not on AKS.

New in Revision 1, answered by decision 6774981258 (Q6): DA7 accepted by name; DA6 settled by the offline end-to-end test running the child inside the built image.
- DA6. The deployed image's runtime gives exit code 137 to the parent for the child's self-SIGKILL, as observed locally. Basis: the image base is `mcr.microsoft.com/dotnet/aspnet:10.0.13-noble` (`Parchwell.LoadGen.csproj` line 8) and the local runtime is Microsoft.NETCore.App 10.0.13; `ExitCode` documents no SIGKILL encoding, so this is an observation, not a documented fact. The trial's termination receipt records the actual value either way.
- DA7. Part 2's orchestration starts the section 3.3 Jobs and mounts the section 3.11 binding that part 1's phase reader builds. Not testable in part 1.

## 8. Test map

C# tests (sections 8.1 and 8.2) are under `tests/Parchwell.LoadGen.Tests/Abrupt/`, named `Given_When_Then`. Python tests (section 8.2a) are in the file each row names. "Real" means a real OS process started by `Process.Start`, real `kill`, and, where stated, real Confluent.Kafka 2.16.0 against `LocalKafkaFixture` unchanged. The release delay in tests is 120 s unless a test says otherwise; a test that needs a shorter delay uses a test-only seam and says so, and T-E1 uses the real 120 s.

### 8.1 Unit and component tests

| ID | Requirement | Input | Expected (literal) |
|---|---|---|---|
| T-O1 | R13 | `Role` = `supervisor`, `parent`, `child`, `successor`, `Supervisor` | detail `Role`, accept, accept, accept, `Role` |
| T-O2 | R13 | each removed key of 3.2 present | detail `SettingNameOrDuplicate` |
| T-O3 | R13 | each fixed policy key at value, value+1, value-1 | accept, `FixedPolicy`, `FixedPolicy` |
| T-O4 | R13, 3.3 | `JobName` for parent: `r-c1-r1-loadgen-restart`, `r-c1-r1-loadgen`, `r-c1-r1-loadgen2`, `r-c1-r1-loadgen-successor`; for successor the mirror set | accept only the role's own name; others `JobName` |
| T-J1 | R2, 3.5 | records written by two threads, 1000 terminals interleaved with 20 producing-thread records | sequences 1..1020 with no gap; every line ends `0x0A`; `child_started` first |
| T-J2 | R9 | journal with final byte removed; journal with one line `{` ; journal with sequence 1,2,4 | gap `malformed_frame`, `malformed_frame`, `sequence_gap`; `successor_permitted` false |
| T-J3 | 3.5 limits | record of 65536 and 65537 bytes; 4096 and 4097 records; bytes at 16777216 and 16777217 | below/at accepted, above latches `frame_oversize`, `journal_entries_exceeded`, `journal_bytes_exceeded` |
| T-J4 | R3 | 256 and 257 enqueued callbacks with the writer blocked | 256 accepted; 257th latches `queue_full`; callback returns without I/O |
| T-C1 | R4 | synthetic producer: ordinals 0..4 accepted, terminals for 0..3 before `cut` | control set `[attempt of ordinal 4]`; one resend at `release_utc` with `predecessor_attempt_id` = that attempt |
| T-C2 | R4 | terminal for ordinal 4 arrives after `cut` | still resent (set fixed at `cut`) |
| T-C3 | R5 | all five terminals before `cut` | `no_kill` reason `b10_abrupt_exposure_inconclusive`, exit 3, no `cross_restart` `prepared` |
| T-C4 | R6 | `Flush` returns 1 left; and a missing terminal for one resend | `no_kill` reason `b10_abrupt_control_incomplete` both cases |
| T-C5 | R18, N1 | synthetic producer; a terminal for ordinal 4 enqueued concurrently with the `cut` append, 1000 iterations | in every iteration the journal-recomputed set equals `cut.unconfirmed_attempt_ids`; when the terminal precedes `cut` in journal order the set is empty and the child ends `no_kill` with `b10_abrupt_exposure_inconclusive` |
| T-B1 | 3.5, rule 5 | ordinal 0 `accepted` before any `pre_acceptance_report`, then a late report | one failure `b10_abrupt_sample_missing`; no `b10_abrupt_report_binding_invalid` (adapted from line 26 of the supervisor tests) |
| T-B2 | 3.5, rule 5 | two `pre_acceptance_report` records for one arm, then `accepted` | one failure `b10_abrupt_report_binding_invalid`; no `b10_abrupt_sample_missing` (adapted from line 38) |
| T-B3 | 3.5, rule 3 | `terminal` appended before its attempt's `accepted`; and after | the attempt counts as terminal in both cases and is in no unconfirmed set (adapted from line 145) |
| T-R1 | 3.6 step 8 | ordinal 4 rejected in control; in cross with ordinals 0..3 accepted; in cross with all rejected | `b10_abrupt_cut_rejected`, `b10_abrupt_cut_rejected`, `kill_too_early` |
| T-P4 | R8 | parent classifier: exit 137 + last producing record `kill_intent` + matching identity + stable cgroup | `intended_self_kill`, `successor_permitted` true when set nonempty |
| T-P5 | R8 | as T-P4 with exit 134; with exit 0; with a `prepared` after `kill_intent` | `unexpected_exit` each |
| T-P6 | R8 | `no_kill` record, exit 3; `no_kill` record, exit 1 | `no_kill` both; exit code recorded, not gated |
| T-P7 | R8 | intended kill, empty kill set | `outcome` `kill_too_late`, `detail` `no_unconfirmed_attempt`, `successor_permitted` false |
| T-P8 | R10 | storage double: bound report file missing; length differs; all present | `b10_abrupt_publication_failed` before manifest; same; members in 3.10 order with `termination.json` last |
| T-S1 | R11 | binding `termination` SHA-256 changed by one hex digit | `b10_abrupt_successor_binding_invalid:receipt_changed` |
| T-S2 | R11 | `successor_permitted` false | `receipt_changed`; no registration written |
| T-S3 | R11 | journal recomputed set differs from `kill_unconfirmed_attempt_ids` | `selection_stale` |
| T-S4 | R11 | `first_pod.job_name` = `r-c1-r1-loadgen` | `job_mismatch` |
| T-S5 | R12 | kill set of 2 members | 2 resends in invocation order, each payload SHA-256 equal to its `prepared`; no resend before `release_utc` (fake clock 1 ms before: none) |

### 8.2 Real-process tests

| ID | Requirement | Setup | Expected |
|---|---|---|---|
| T-P1 | R1 | real parent code with an injected child command pointing at the test-output `Parchwell.LoadGen` apphost, `--b10-abrupt-child`, synthetic producer seam | one child PID; parent never calls a signal API (asserted by a recording signal seam that must stay empty); `/proc/<child>` gone after `WaitForExit` returns true |
| T-P2 | R8 | real child that self-SIGKILLs right after `kill_intent` | `exit_code_local_observation` 137; `intended_self_kill`; output line records runtime version and kernel release |
| T-P3 | R1 | real child that sleeps past a 2 s test bound | `unknown`, `b10_abrupt_exit_unobserved`, child still alive and then reaped by the test (test cleanup only) |
| T-K1 | R7 | real child; journal read after death | last record `kill_intent`; exit 137; every earlier line complete |
| T-N1 | R5 | real child with control set forced empty | exit 3; last record `no_kill`; `no_kill` termination |
| T-N2 | R6 | real child, cross arm not admitted, control incomplete | exit 3; producer disposed before exit (disposal recorded by the seam) |
| T-N3 | 3.6 step 6 | real child, failure after cross ordinal 0 admitted | exit 3 by `Environment.Exit`; no `Flush` or `Dispose` recorded |
| T-K2 | R17 | real child with the `kill` seam returning -1 and errno 1 | last records `kill_intent`, `signal_failed` with `return_value` -1 and `errno` 1; exit 4; termination `unexpected_exit`, outcome `b10_abrupt_signal_failed`, `successor_permitted` false |
| T-E1 | R2 to R8, DA1/DA5 locally | real parent, real child, real Confluent.Kafka against `LocalKafkaFixture`, B10 C1 producer settings, real 120 s release | parent exit 0; termination `intended_self_kill`; exit 137; control set nonempty; journal parses fully; prints `cut_accepted_to_kill_intent_microseconds` per run as a labelled local observation |
| T-E2 | R11, R12 | after T-E1, a real successor process with a file-backed storage double holding T-E1's published members, real Kafka | successor exit 0; each kill-set member's resend consumed from the topic with equal key, sequence and value bytes and a new attempt ID header |

### 8.2a Chart and phase tests

| ID | Requirement | Input | Expected |
|---|---|---|---|
| T-H1 | R16, 3.13; `charts/parchwell/tests/test_b10_abrupt_chart.py` | render with `mode` `loadgen-restart` and `loadgen-successor`, `b10_abrupt_diagnostic` true | Job names `{run}-c1-r1-loadgen-restart` and `-successor`; args and Role as section 3.13; the nine policy keys exactly; no pipe key |
| T-H2 | R16; `charts/parchwell/tests/test_b10_abrupt_chart.py` | render with `mode` `loadgen` and `loadgen2` and `b10_abrupt_diagnostic` true | schema rejection; with the flag false, output equal to `main`'s for the same values |
| T-H4 | 3.13, decision 6775679271; `infra/scripts/phase/tests/test_b10_abrupt.py` | `Phase.render` in a diagnostic phase (`b10_abrupt_diagnostic` true) with `b10_abrupt().values` patched to record its calls, for modes `loadgen-restart`, `loadgen-successor` and `loadgen` | the hook is called once each for `loadgen-restart` and `loadgen-successor`, with that mode, and not called for `loadgen`; `git diff` of `phase.py` against `main` shows only line 660 |
| T-H3 | 3.13; `infra/scripts/phase/tests/test_b10_abrupt.py` (`ProducedBytesCases`, run through `check_b10_abrupt_producers.py`) | produced schema-3 export; then mutations: `schema_version` 2; `successor_permitted` false; journal last byte removed; manifest journal SHA-256 changed; `kill_unconfirmed_attempt_ids` with one member removed | accepted; then `ValueError` for each mutation |

T-E1 and T-E2 run in the same test class, sequentially, one repeat each per test run; the receipt records their output for one run.

### 8.3 Properties the cold reviewer checks by reading the diff

Tests cannot close these without unbounded mutants:
- P1. No code path reachable from the parent calls `kill`, `Process.Kill`, `Process.Close` on a live child, or any `PosixSignal` API, and no pipe type is constructed anywhere in `Run/Abrupt/`.
- P2. Delivery and error handlers contain no `FileStream`, `File.`, `Stream.Write`, `Flush`, Blob or Kafka call; only the bounded enqueue and timestamp.
- P3. The only `kill` P/Invoke call site passes `Environment.ProcessId` and signal 9, and is reached only after the `kill_intent` append returned.
- P4. No `Flush` or `Dispose` of the producer is reachable after cross-restart ordinal 0 is admitted.
- P5. No file outside `Run/Abrupt/` and `Program.cs` lines 41 to 48 changes under `src/`.

### 8.4 Mutation table

The implementer applies each mutation by hand, runs the named test, records it failing in the receipt, and reverts. 21 mutations, fixed.

| # | Mutation | Must fail |
|---|---|---|
| M1 | Accept exit code 0 as intended | T-P5 |
| M2 | Drop the "last producing record is `kill_intent`" check | T-P5 |
| M3 | Compute the control set at release instead of at `cut` | T-C2 |
| M4 | Skip resend of members completed after `cut` | T-C2 |
| M5 | Treat a torn final record as valid | T-J2 |
| M6 | Accept a sequence gap | T-J2 |
| M7 | `queue_full` at 256 instead of 257 | T-J4 |
| M8 | Set `successor_permitted` when the kill set is empty | T-P7 |
| M9 | Publish `termination.json` before the manifest | T-P8 |
| M10 | Skip the report-binding comparison | T-P8 |
| M11 | Skip the binding `termination` hash check | T-S1 |
| M12 | Skip the recomputed-set comparison | T-S3 |
| M13 | Resend one tick before `release_utc` | T-S5 |
| M14 | Accept `-loadgen` as the parent Job name | T-O4 |
| M15 | Accept `Role=supervisor` | T-O1 |
| M16 | Call `Flush` after cross ordinal 0 on the no-kill path | T-N3 |
| M17 | Exit 3 after `kill` returns, or write `no_kill` | T-K2 |
| M18 | Compute the control set before taking the journal lock for `cut` | T-C5 |
| M19 | Phase reader accepts `schema_version` 2 | T-H3 |
| M20 | Chart renders `--b10-abrupt-supervisor` for `loadgen-restart` | T-H1 and acceptance command 9 |
| M21 | Render hook condition keeps `"loadgen"` among its modes | T-H4 |

## 9. Acceptance commands

Each command was run in this planning session where stated, with its observed output; the rest are run by the implementer, who records output in the receipt.

1. `dotnet build tests/Parchwell.LoadGen.Tests -c Release` with warnings as errors. Planning session on `main` `2ed497d6`: `0 Error(s)`.
2. `dotnet build src/Parchwell.Verifier -c Release`, then `dotnet test tests/Parchwell.LoadGen.Tests -c Release --no-build` with Docker running: every test passes; T-E1 output includes `exit_code_local_observation=137`. Planning session ran the existing `RunExecutorComponentTests` against `LocalKafkaFixture` on `main`: `total: 6, failed: 0, succeeded: 6` (the pinned image pulled from `mirror.gcr.io` by digest after Docker Hub returned 429).
3. `./scripts/verify-dotnet.sh`: passes.
4. `git diff --name-only origin/main...HEAD` prints only paths allowed by section 2.1 (script check in the receipt).
5. `git diff origin/main...HEAD -- src/Parchwell.LoadGen ':!src/Parchwell.LoadGen/Run/Abrupt' ':!src/Parchwell.LoadGen/Program.cs'` prints nothing; and the diff of `Program.cs` touches only lines 41 to 48 (R15, P5).
6. `grep -rnE 'AnonymousPipe|SIGSTOP|SIGCONT|RequestStop|Resume\(|EventsReady|kill-selection|checkpoint|B10AbruptChildChannel|B10AbruptSupervisor|OwnedProducerProcess|ProbeBurstSize' --include='*.cs' src/Parchwell.LoadGen` prints nothing (R14); `--include` keeps build output under `bin/` and `obj/` out of the search.
7. The mutation table: 21 rows, each with the failing test name and its output line.
8. `LC_ALL=C grep -n -P '[^\x00-\x7F]'` over every changed file prints nothing.
9. The required step, as CI runs it: `PARCHWELL_TOOLS_DIRECTORY=<pinned tools> DOTNET_ROOT=<dotnet> python3 infra/scripts/lifecycle/command.py 900 -- bash charts/parchwell/tests/check-loadgen-host.sh`; the last line is R16's. This planning session ran it on clean `main` `2ed497d6` with the pinned tools at `/opt/parchwell/tools` and `DOTNET_ROOT=/opt/parchwell/dotnet`; its last line was `b10 abrupt host checks passed: 2 rendered environments, 5 rejected inputs, production-byte bridge`.
10. `bash charts/parchwell/tests/check.sh` and `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`: pass; `test_chart.py` unedited (section 2.2).

## 10. Prototype evidence (this session, scratch only)

A scratch .NET 10 program outside the repository (Confluent.Kafka 2.16.0, `enable.idempotence` true, `message.timeout.ms` 100, `max.in.flight` 5, `acks` all, journal with one lock shared by the producing thread and a writer thread, `Flush()` per record, callbacks enqueue to a bounded channel of 256) ran on 2026-11-10 against the pinned `confluentinc/cp-kafka:7.5.12` image, started by `docker run` with the fixture's environment rather than through `LocalKafkaFixture`. Kernel 6.18.44, ext4, Microsoft.NETCore.App 10.0.13. Local observations, not measurements of Event Hubs:

- Self-SIGKILL by libc `kill(getpid(), 9)` without Kafka: parent `ExitCode` 137, 3 of 3 runs, journal complete, last record `kill_intent`.
- `no_kill` then return 3: `ExitCode` 3, 1 of 1 run.
- Full sequence, release shortened to 2000 ms: 10 of 10 runs exited 137 with 48 complete records, no malformed line, last record `kill_intent`, kill set `[ordinal 4]`, control set at cut `[ordinal 4]` in the run inspected; time from the cut original's `accepted` record to the `kill_intent` record 765 to 1223 microseconds (minimum and maximum of 10, computed by script). This is to the `kill_intent` record, not to the signal; the offline test owns the measurement to the SIGKILL (decision 6769742939).
- No-kill after a complete control arm: `ExitCode` 3, last record `no_kill`, 10 control terminals.

## 11. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Runtime-sized steps: deleted test methods (bounded by the 2.4 files on `main`, listed by script in the receipt); mutation runs (fixed at 21); documentation warnings (none expected, no doc change); verification failures and review findings. Caps: 20 verification-failure root causes and 20 review-finding root causes, exhaustion `push -wip and hand off to the next session`.

(b) Stricter than the parent plan: the stop rule also covers changes to the shared files of section 2.3; the ordinary host is frozen by acceptance command 5, which decision 6774480762 states but does not test.

(c) Dependencies outside `main` and this pull request: part 2's orchestration (DA7, accepted); the AKS runtime exit code (DA6, settled by the offline test); DA1 to DA5 as decided. The required CI step now depends on part 1's own chart and phase changes, which acceptance command 9 covers.

## 12. Decided defaults (relayed as standing unless Hari objects)

- D2, old code: delete the stop/pipe/supervisor machinery and its tests (section 2.4); adapt wire, journal, statistics, cgroup, publisher, diagnostics and successor.
- D3, child launch: same apphost and path check, `--b10-abrupt-child`, settings by environment only, stderr inherited and never parsed; parent flag `--b10-abrupt-parent`, role `parent`; child identity by its `child_started` record compared with the parent's `/proc` reads.
- D4, successor input: workflow-mounted binding of section 3.11; journal, manifest and termination receipt downloaded from Blob and hash-chained; release at `kill_intent.release_utc`.
- D5, `successor_permitted` also requires no latched capture gap and successful publication. Basis: ADR-021 rule 4 ("capture invalid and authoritative fields absent" on any gap) and rule 11 ("Block successor if death or first-pod termination/publication is unverified").
- D7, a returned self-kill (decision 6775679271): the child appends `signal_failed` and exits 4; termination `unexpected_exit`, outcome `b10_abrupt_signal_failed` (sections 3.1, 3.6 step 5, 3.8).
- D8, the control set at the cut (decision 6775679271): computed under the journal lock in the same critical section as the `cut` append, stored as `cut.unconfirmed_attempt_ids`, and checked by readers; a disagreement latches `malformed_frame` (section 3.5).
- D6, configuration keys: `MaxFrameBytes` becomes `MaxRecordBytes` (same value, ADR-021 rule 4 now says "record"); `ProbeRate` 100 and `CutOrdinal` 4 become fixed policy keys for the values Hari decided (6769312013, 6769269811); the four stop and arm budgets are removed with the protocol.

## 13. Open questions for Hari

### 13.1 Answered (decision 6774981258)

| Question | Answer |
|---|---|
| Q1 Job names | `{run}-c1-r1-loadgen-restart`, `{run}-c1-r1-loadgen-successor` |
| Q2 Implementing tool | Codex |
| Q3 `b10_abrupt_cut_rejected` | accepted |
| Q4 Deleted tests | accepted by name; section 2.4 now keeps more, which narrows it |
| Q5 Operations contract gap until part 2 | accepted |
| Q6 DA6 and DA7 | DA7 accepted by name; DA6 settled by the offline test inside the built image |

### 13.2 Answered (decision 6775679271)

| Question | Answer |
|---|---|
| Q7 `phase.py` render hook | option A: line 660 changes to the two abrupt modes; `B10_MODES` and the ordinary collection loop unchanged |
| Exit code 4 and classification for a returned `kill` | accepted (D7) |
| Stored and checked `cut.unconfirmed_attempt_ids` | accepted (D8) |

### 13.3 Open

None.

## 14. Sources

Read 2026-11-10: ADR-021 in `docs/blueprint.md` at `2ed497d6`; [Environment.Exit](https://learn.microsoft.com/en-us/dotnet/api/system.environment.exit?view=net-10.0) (Remarks: no `finally` block runs); and the sources ADR-021 already cites for `Process.Start`, `WaitForExit`, `ExitCode`, `DllImport`, POSIX `kill`, `write` and `sigaction`, Kubernetes `emptyDir`, and Azure Storage consistency, which this plan relies on through ADR-021 and does not restate. Code read on `main`: `Program.cs`, `Run/Abrupt/B10AbruptHost.cs`, `B10AbruptOptions.cs`, `OwnedProducerProcess.cs`, `B10AbruptJournal.cs`, `B10AbruptPublisher.cs` (successor binding), `Run/Input/LoadGenHostOptions.cs` lines 85 to 125, `charts/parchwell/templates/jobs.yaml` lines 79 to 130, `charts/parchwell/values.yaml` lines 39 to 45, `Parchwell.LoadGen.csproj` line 8.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when:
1. it needs to change a section 3 contract, a requirement or the scope;
2. a section 13 answer differs from this plan's recommendation in a way not already written here;
3. a file in section 2.3 changes on `main` after `2ed497d6`, or any file this plan changes changes on `main` before implementation starts;
4. a test cannot pass without changing a path in section 2.2, or `phase.py` beyond the one line of section 3.13;
5. the self-SIGKILL exit code observed by T-P2 is not 137 (section 3.1 would then be wrong);
6. a change to `phase.py` beyond the line-660 condition seems needed.
