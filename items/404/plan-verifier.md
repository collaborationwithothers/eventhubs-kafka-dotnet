# Verifier (PR3) contract-grade plan

Revision 1. Issue 404, pull request short name `verifier`. Implementing tool: Codex, as `items/404/state.md` records. Status: scope and split revision. It is not implementable until Hari answers question Q1 under "Open questions for Hari".

This plan covers PR3 of the approved restart design: the work that reads the topic back after a B10 abrupt run and classifies the outcome. The scope the design assigns to PR3 is larger than one pull request, and part of it is not verifier code, so the plan proposes a split for Hari instead of choosing one. No trial of the current diagnostic shape can select a restart verdict, because one repeat cannot meet the eligibility threshold of 3 of 5; a trial can at most show one repeat's diagnostic observation.

## 1. Goal and authority

Goal: let a B10 C1 abrupt repeat be read back from `document-events` and classified under ADR-021 "Restart decision rules" and "Restart protocol and policy", so the restart question can be answered by a governed scientific run.

Authority, in creation order, as recognized under docs/agents/factory.md "Item lifecycle" step 3:
- Issue 404 body.
- [6705801800](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705801800), point 3 ("Fatal-error capture is included in this work, so the verifier can tell an intended kill from a crash") and point 5 ("(3) the verifier evidence and fatal capture").
- [6708991894](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6708991894): "PR3 (verifier evidence and fatal capture) [is] not approved by this comment; each needs its own plan, rechecked against merged predecessors. Hari merges every pull request here."
- The approved item plan, [Revision 5](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705594392), section "Three ordered PRs, checks and caps", the PR3 paragraphs quoted in section 2 below.
- docs/blueprint.md ADR-012, ADR-013, ADR-019, ADR-020 and ADR-021 as amended on `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`. ADR-021 rules 9, 12, 13 and 14 and decision rules 1 to 7 are the design PR3 implements.
- The later decisions listed in `items/404/state.md` "Decisions in force", in particular the burst amendment recorded in ADR-021 protocol rule 15.
- [6749565231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749565231) (option 2): plan the successor fix and this pull request in parallel; both plans carry a never-ran inventory; "A further trial needs Hari's new explicit approval and his gate approval. Neither is given by this decision."

Catch-up: `items/404/state.md` covers comment 6746177383. Comments after it, read in full: 6747016131 (completion report for the burst test-gap follow-up, not a decision) and 6749565231 (decision, verified to say what the planning prompt says). Issue body `updated_at` 2026-11-09T19:38:29Z. Snapshot: 140 comments.

Baseline: `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`. The plan branch `claude/404-plan` did not exist on the remote when this revision was written.

## 2. Scope

### 2.1 What the design assigns to PR3

Item plan Revision 5, "Three ordered PRs, checks and caps", verbatim:

> PR3 covers `VerifyRun/Judgement/B10RepeatChecks.cs` (`CheckRetries`, `CheckRestart`), `VerifyRun/Input/RestartManifestReader.cs`, `VerifyRun/Judgement/B10IntakeChecks.cs`, sweep/judge/finalization, abrupt readers and candidate codecs. B10-only oracle changes preserve ordinary `OutcomeClassifier`, legacy behavior and the four-class table. Add every new scientific/admission helper and codec to C5, including candidate-codec predicates, with no threshold reduction or exclusion. Operational loadgen/verifier/B10-workflow contracts move with implementation. PR2 marks inactive contracts "planned; not active for scientific publication"; PR3 records admitted behavior.

and from the PR2 paragraph: "PR3 must explicitly wire scientific admission after its verifier/fatal-capture gates and the #356 adoption are ready."

Together with decision 6705801800 point 3, the assigned scope is these ten parts:

| Part | What | Main paths | Protected |
|---|---|---|---|
| S1 | Fatal capture through death in the first instance, replacing `fatal_capture_status="not_implemented_pr3"` | `src/Parchwell.LoadGen/Run/Abrupt/*`, `docs/operations/b10-abrupt-contract.md` | No |
| S2 | Abrupt readers for the first-instance and successor objects | `src/Parchwell.Verifier/VerifyRun/Input/*` | Yes |
| S3 | Per-repeat classification: `CheckRetries`, `CheckRestart`, intake, cohorts, arms, pairs, loss | `VerifyRun/Judgement/*`, `VerifyRun/VerifyRunHandler.cs`, `VerifyRun/Window/*` | Yes |
| S4 | Nonfinal abrupt result codec | `src/Parchwell.Verifier/Common/Results/*` | Yes |
| S5 | Sweep line and late-write finalization for abrupt identities | `VerifyRun/B10SweepHandler.cs`, `Judgement/B10LateWriteChecks.cs`, `Common/Phase/B10SweepEvidenceCodec.cs` | Yes |
| S6 | Judge: eligibility count, both verdicts, comparison, witnesses, loss findings | `Judge/PhaseJudge.cs`, `Judge/B10JudgeChecks.cs`, `Judge/B10SweepChecks.cs` | Yes |
| S7 | Candidate codec projection | `Judge/PhaseCandidate.cs`, `Judge/PhaseCandidateCodec.cs` | Yes |
| S8 | C5 targets for every new scientific helper and codec | `tests/Parchwell.Verifier.Tests/Mutation/*`, `.github/workflows/verifier-c5-mutation.yml` | Yes |
| S9 | Scientific admission wiring, after S1 to S8 and the #356 adoption | `infra/scripts/phase/phase.py`, `.github/workflows/scenario.yml` and its copy | Workflow yes |
| S10 | Operational contracts | `docs/operations/verifier-contract.md`, `b10-abrupt-contract.md`, `b10-workflow.md`, `loadgen-contract.md` | No |

### 2.2 Findings that make one pull request impossible

Each finding is checked against `main` at `f5cc496f` unless stated.

- F1. The first instance does not publish the evidence the verifier must reconcile independently. `B10AbruptPublisher.FirstMembers` (`src/Parchwell.LoadGen/Run/Abrupt/B10AbruptPublisher.cs:35`) is exactly `registration.json, journal.jsonl, timeout-seal.json, control-seal.json, kill-selection.json, abrupt-manifest.json, termination.json`. The timeout and control seals bind `manifest.json`, `ledger.jsonl` and `post-drain-report.json` by descriptor (trial 7 `timeout-seal.json`: ledger length 26441293, inventory length 9635312), but none of those files, nor the arm pre-acceptance reports, is published. ADR-012's B10 exception says "the verifier independently downloads and reconciles ADR-021's required journal, manifest, selection and receipt, including genuine terminal records", and ADR-021 rule 12 says each cohort "passes independent checks; none repairs another's missing evidence". Without those files the verifier cannot check the timeout cohort's six counts (decision rule 1) or the control's completeness (decision rule 2). Publishing them is a LoadGen and phase-script change, not verifier code, and the item plan's PR3 file list does not name it. Question Q2.
- F2. Fatal capture through death needs a LoadGen wire change. The first child's error callback goes to `RunEvidenceSession.CaptureError` (`B10AbruptHost.cs:233`), whose files stay in the killed child's local directory and are not published. ADR-021 rule 9 requires capture "before use" with "actual code/name/IsError/IsFatal, ordered sequence/time, instance, start/cutoff and dropped/writer-error status" carried past the kill. That needs a new journal or IPC record kind in the closed wire of `docs/operations/b10-abrupt-contract.md` "Wire and state machine". Decision 6705801800 point 3 assigns it to this work.
- F3. No verifier mode has run in any diagnostic trial, and the trial orchestration has no step that runs one. `b10_abrupt.py` `run()` invokes only `start-line`, `loadgen` and `loadgen2` (lines 559-598). Every trial's `phase-status.json` lists only those modes (section 7). Making a trial read the topic back changes `infra/scripts/phase/b10_abrupt.py` and possibly `.github/workflows/b10-abrupt-trial.yml`, which is protected and has a byte-identical copy at `docs/operations/workflows/b10-abrupt-trial.yml.txt`.
- F4. The verifier does not read record values. `MessageIdentityReader.Read` (`src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs:13-29`) parses the key and the `instance-id`, `attempt-id` and `sequence` headers only. ADR-021 decision rule 3 requires "identical logical key/sequence/payload", so the reader needs a value hash. This changes a shared window reader used by every scenario.
- F5. The sweep, judge and candidate are five-repeat phase machinery. `B10SweepHandler` builds its inputs for repeats 1 to 5 (`B10SweepHandler.cs:37`), and `PhaseCandidateCodec` requires exactly five repeats in the B10 finality check (`PhaseCandidateCodec.cs:186`). A one-repeat diagnostic trial cannot pass through them unchanged, and ADR-021 decision rule 2 needs at least 3 of 5 eligible C1 repeats. So a diagnostic trial can produce a per-repeat nonfinal observation, never a verdict. Question Q3.
- F6. S6 to S8 change at least 8 of the 17 current C5 targets (`PhaseJudge`, `B10JudgeChecks`, `B10SweepChecks`, `B10SweepHandler`, `B10SweepEvidenceCodec`, `RestartManifestReader`, `B10IntakeChecks`, `B10RepeatChecks`, counted from `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`), each needing recomputed cuts under the item plan's cut cap, and new targets change the 61-row workflow matrix in a protected workflow.

Counts in F6 were computed by script from the committed configuration; the matrix row count by `grep -c 'target:' .github/workflows/verifier-c5-mutation.yml` (output `61`).

### 2.3 Proposed split (decision for Hari, question Q1)

The plan does not choose. Option A is the planner's recommendation, with its reason.

Option A, five pull requests in order, each with its own contract-grade plan revision and Hari's merge:
1. PR3a "abrupt evidence completeness" (S1 and the F1 publication; LoadGen, `b10_abrupt.py`, contract docs; not protected except as noted): publish the timeout and control cohort ledgers, inventories and post-drain reports and both arm pre-acceptance reports, and add fatal capture through death to the journal, with `fatal_capture_status` reporting actual coverage. Predecessor: the successor fix.
2. PR3b "per-repeat abrupt classification" (S2 to S4, S10 verifier part; protected): readers, independent reconciliation, value hash, topic read-back, per-cohort and per-arm classification, eligibility reasons for one repeat, nonfinal result codec. Diagnostic-only: scientific readers keep rejecting the dialect. Predecessor: PR3a.
3. PR3c "diagnostic trial read-back" (F3; `b10_abrupt.py`, and the trial workflow only if the step cannot live in the script): run the PR3b verify mode in a diagnostic trial after the successor completes. Predecessor: PR3b. A trial after it needs Hari's approval.
4. PR3d "phase finality and verdicts" (S5 to S8; protected, workflow matrix): sweep, judge, both verdicts, comparison, witnesses, loss findings, candidate projection, C5 targets.
5. PR3e "scientific admission" (S9): after #356 adopts the rules.

Reason for A: it puts every never-ran stage that a diagnostic trial can exercise (PR3a to PR3c) before the five-repeat machinery, so a trial can test them live before PR3d builds on them. Trial 7 failed in a stage no test had run for real; option A limits how much unexercised code accumulates before the next live run.

Option B, three pull requests: PR3a as above; PR3b plus PR3c combined; PR3d plus PR3e combined. Fewer reviews; PR3d plus PR3e would combine the largest protected change with the admission switch.

Option C, as the item plan literally lists: one verifier pull request (S2 to S8, S10), with S1 and F1 left to an unplanned LoadGen pull request. The planner does not recommend it: it exceeds the review-round split rule's practical size (docs/agents/pull-requests.md rule 6 is a governance threshold, so this is a judgement, not a measurement) and leaves F1 unowned.

## 3. Files allowed to change, and paths that must not change

Allowed, for the whole PR3 scope across all slices: the paths in section 2.1, `tests/Parchwell.Verifier.Tests/**`, `tests/Parchwell.LoadGen.Tests/Abrupt/**`, `infra/scripts/phase/tests/test_b10_abrupt.py`, new fixture directory `tests/fixtures/b10-abrupt/trial-42679791413/`, `items/404/state.md` and the slice's receipt under `items/404/`. Each slice's revision narrows this list to its own files.

Must not change: `scenarios/B10/expected.yaml` (does not exist on `main`; must not be created), anything under `results/`, `docs/blueprint.md`, `docs/agents/**`, `AGENTS.md`, `CLAUDE.md`, `README.md`, any `*.csproj`, `Directory.*.props`, `*.sln*`, `src/Parchwell.Verifier/VerifyRun/Judgement/OutcomeClassifier.cs` (ADR-021 "B10-only oracle changes preserve ordinary `OutcomeClassifier`"), every existing workflow's triggers (AGENTS.md hard safety rule 8), and `infra/persistent/**`. No new project, assembly reference or NuGet package.

## 4. External contracts

### 4.1 Inputs already fixed on `main` (pinned; any change is a stop-rule event)

- Abrupt wire objects: `docs/operations/b10-abrupt-contract.md` "Wire and state machine", envelope `schema_version: 2`, `dialect: "b10_abrupt"`, `purpose: "diagnostic_trial"`, `synthetic: true`, `scientific_admissible: false`, `kind`, `identity` with exact members `run_id,scenario_id,configuration_id,phase,repeat,source_commit,image_reference,job_name,pod_name,pod_uid,instance_id,predecessor_instance_id`.
- Blob locations: container `ledger`, prefix `<run_id>/C1/r1/b10-abrupt-diagnostic/<pod_name>/` (`b10_abrupt.py:384`; `B10AbruptPublisher.cs:161`).
- First-instance members: as F1. Successor members: `registration.json, ledger.jsonl, manifest.json, statistics.jsonl, request-lifecycle.jsonl, capabilities.json, completion.json` (`B10AbruptPublisher.cs:36`; `b10_abrupt.py:18-19`). `request-lifecycle.jsonl` may be empty; every other member must be non-empty (`b10_abrupt.py:392`).
- Successor `ledger.jsonl`: one line per successor probe attempt, the JSON object with exact members `attempt_id,completion_source,delivered,error_code,persistence_status` (`B10AbruptProducer.RecordTerminal`, lines 391-395; `SealProbeCohortAsync`, line 539). Successor `manifest.json`: envelope kind `cohort_inventory`, data members `instance_id,cohort,inventory_count,inventory,bookkeeping_complete` (`B10AbruptWire.cs:37`). Successor `completion.json`: envelope kind `successor_receipt`.
- Kafka record shape for probe records: key UTF-8 `B10/{run_id}/{configuration}/{repeat}/{same_instance|cross_restart}`; value the payload bytes; exactly the headers `instance-id`, `attempt-id`, `sequence`, each UTF-8, `sequence` invariant decimal (`B10AbruptProducer.Message`, lines 655-662). Timeout-cohort records add `tenant-id`, `document-id`, `event-type` (lines 150-152).
- Persistence status numbers on the wire: Confluent.Kafka `PersistenceStatus` `NotPersisted=0`, `PossiblyPersisted=1`, `Persisted=2` (the same mapping `B10RepeatChecks.IsPermittedTimeout` uses at lines 18-21; confirmed against the [PersistenceStatus API page](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.PersistenceStatus.html) cited in ADR-021 sources). Trial 7 terminal frames carry `persistence_status: 2`.

### 4.2 Successor outputs this plan reads, and the predecessor

The verifier reads all seven successor members. Trial 7 published only `registration.json`; the download of `ledger.jsonl` failed with `b10_abrupt_publication_ambiguous` at boundary `successor_publication` (`phase-status.json`, site `b10_abrupt.py` line 394), after the successor raised `b10_abrupt_guard_failed` at `B10AbruptHost.cs` `CreateProducerAsync` line 249 with facts `completion_complete 0, evidence_faulted 0` (successor pod log). The successor fix planned in parallel under decision 6749565231 is a predecessor of every slice that reads successor outputs (PR3b onward). This plan depends on it producing the seven members above with the formats in 4.1 unchanged. If the successor fix changes any of those formats, this plan's revision must follow it (stop rule).

### 4.3 Outputs (pinned per slice after Q1)

Pinned now, because ADR-021 rule 14 and decision rule 4 name them:
- New envelope kinds `nonfinal_result` (per-repeat verify output) and `final_sweep_projection` (phase sweep output) in the `b10_abrupt` schema 2 dialect, closed members, no reuse of the schema-one `VerifierResult` or `B10PhaseFinalization` types.
- Restart verdict literals `confirmed`, `untestable` and `held`; the `untestable` reason literal `no_cross_restart_duplicate_observed`; the native tail label `unknown_due_to_intended_kill`. All from ADR-021 decision rules 3, 4 and 6.
- Per-arm observation names from ADR-021 decision rule 5 and rule 13: arm literals `same_instance` and `cross_restart`; comparison literals `both`, `cross_restart_only`, `same_instance_only`, `neither`, and `unavailable`.

Proposed closed list of ineligibility reason literals (ADR-021 decision rule 2 requires every reason to be published; Hari approves the words with the plan): `b10_restart_configuration_invalid`, `b10_restart_identity_unauthenticated`, `b10_restart_termination_unverified`, `kill_too_early`, `kill_too_late`, `b10_abrupt_exposure_inconclusive`, `b10_restart_resend_count_invalid`, `b10_abrupt_control_incomplete`, `b10_restart_guard_coverage_missing`, `b10_restart_fatal_capture_unavailable`, `b10_restart_fatal_captured`, `b10_restart_timeout_bookkeeping_unavailable`, `b10_restart_successor_evidence_unavailable`, `b10_restart_sweep_not_final`. The four reused names keep their existing meaning in `docs/operations/b10-abrupt-contract.md` "Failure and outcome names".

Deferred to the slice revision after Q1: exact member lists of `nonfinal_result` and `final_sweep_projection`, the candidate projection members, the abrupt verify mode's CLI flag and options, and the phase-status members of a diagnostic read-back. These are deferred because their owner depends on the split; leaving them open here is a known gap, not an oversight.

## 5. Requirements

Tags give the proposed option-A slice. Every requirement keeps unrelated evidence valid in its tests, so another invalid field cannot mask the predicate under test.

Publication and capture (PR3a):
- R1. WHEN the first instance seals the timeout or control cohort, THEN that cohort's `ledger.jsonl`, `manifest.json` and `post-drain-report.json`, and each arm's `abrupt-child/<arm>-pre-acceptance-report.json`, are published under the first-instance prefix with names fixed in the slice revision, bound by the existing seal and checkpoint descriptors (name, length, lowercase SHA-256), and `b10_abrupt.py` requires them in its first-publication acceptance. A missing or mismatched member raises the existing `b10_abrupt_publication_failed` or `b10_abrupt_publication_ambiguous`.
- R2. WHEN the first child's error callback fires at any time from producer construction until death, THEN the record reaches the supervisor journal with the members ADR-021 rule 9 lists, inside the existing 65536-byte frame and 256-frame queue limits; overflow latches the existing sticky gap. WHEN the kill chain completes, THEN `termination.json` reports actual fatal coverage and the native tail `unknown_due_to_intended_kill`, and never `no_fatal_errors`.

Readers (PR3b):
- R3. WHEN the verifier reads the seven first-instance objects, THEN it checks the envelope, identity, descriptor bindings and journal exactly as `b10_abrupt.py` `accept_diagnostic_publication` does (lines 400-546), in .NET, and additionally that the identity equals the start-line run, configuration, repeat, source commit and first Job name. On trial 7's objects (section 6 fixtures) it returns: control checkpoint accepted burst indices `[0,1,2,3,4]`, selected `[3,4]`; kill checkpoint accepted `[0,1,2,3,4]`, selected `[2,3,4]`; kill final selection one entry with sequence `2`; terminal frames for control sequences `0,1,2,3,4` and the control resend; terminal frames for kill sequences `0,1` only; journal entry count `38`; signal order `same_instance 19, same_instance 18, cross_restart 19, cross_restart 9, cross_restart 9`.
- R4. WHEN any first-instance object fails a check in R3, THEN the repeat is not eligible with reason `b10_restart_identity_unauthenticated`, and its authoritative counts are absent, never zero.
- R5. WHEN the verifier reads the seven successor objects, THEN it requires the predecessor to be the first instance, a different pod name and pod UID, the receipt to bind registration and the five payloads, the receipt's `first_termination` and `selection` descriptors to match the first-instance bytes, and each invocation to keep key, sequence and payload hash with a new attempt ID (`b10_abrupt.py` `collect`, lines 606-630). Failure: reason `b10_restart_successor_evidence_unavailable`.
- R6. WHEN the scientific readers (`PhaseWireCodec`, `ManifestReader`, `ArtifactIndexReader`, `B10SweepEvidenceCodec.ReadFinalization`, `PhaseCandidateCodec.Read`) meet abrupt bytes, THEN they still reject them as `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs` asserts today; that test passes unchanged.

Read-back and classification (PR3b):
- R7. WHEN the window consumer reads a record whose key equals one of the repeat's two probe keys, THEN it also records `value_sha256`, the lowercase SHA-256 of the value bytes; a null value is a structural problem `null_value`. Records of other keys are unchanged.
- R8. WHEN a probe record is consumed, THEN it joins exactly one probe attempt on `(instance_id, attempt_id)` from the first-instance journal `prepared` frames or the successor inventory, with key, sequence and `value_sha256` equal to the attempt's `payload_sha256`. A record that matches no attempt, or matches with a different key, sequence or hash, makes the repeat ineligible with reason `b10_restart_identity_unauthenticated`.
- R9. Per arm, WHEN classifying, THEN: an original "without a genuine report" is an accepted burst original with no `terminal` frame in the journal; `unreported_seen` counts such originals consumed at least once, `unreported_unseen` the rest, and their sum equals the number of originals without reports; `resend_seen` is 1 when the arm's single resend is consumed; a pair exists when the arm's selected original and its resend are both consumed at distinct `(topic, partition, offset)` with equal key, sequence and payload hash. Two consumed copies of one attempt are a replay, never a pair. Same-instance pairs never count toward the cross-restart positive.
- R10. WHEN a resend has a genuine terminal report with `delivered: true` and no consumed record through the window (and, once PR3d lands, through the sweep), THEN it is a lost-resend finding for that arm and repeat, published whether the repeat is eligible or not.
- R11. WHEN the timeout cohort's published ledger and inventory are available (R1), THEN they pass the ordinary ADR-012 bookkeeping checks and `CheckRetries` on the first instance only, and the six counts come from the unchanged `OutcomeClassifier` over timeout-cohort records only. Probe records never enter them. WHEN they are not available, THEN the six counts are absent and the reason is `b10_restart_timeout_bookkeeping_unavailable`.
- R12. WHEN `fatal_capture_status` is anything other than the actual-coverage value PR3a defines, including `not_implemented_pr3`, THEN the repeat is ineligible with reason `b10_restart_fatal_capture_unavailable`. On trial 7's objects the repeat is therefore ineligible with at least the reasons `b10_restart_fatal_capture_unavailable`, `b10_restart_timeout_bookkeeping_unavailable` and `b10_restart_successor_evidence_unavailable`.
- R13. WHEN the verify output is written, THEN it is a `nonfinal_result` envelope whose eligibility field is never `eligible` while any reason is present, with every reason listed in a fixed order defined in the slice revision.

Trial wiring (PR3c), finality and verdicts (PR3d), admission (PR3e): requirements are written in their slice revisions after Q1. They implement ADR-021 decision rules 1 to 7 and rule 13 without restating them; each such revision maps every rule to a requirement and a test.

## 6. Invariants and safety

- Verdict integrity (AGENTS.md GOVERNANCE): no slice edits, proposes values for or reads live results into `expected.yaml` or `results/`. A diagnostic read-back is never published as a result (factory.md "Evidence pack": "The diagnostic trial is never published as a result").
- Hard safety rules: no session holds Azure credentials, applies or destroys infrastructure, or dispatches a workflow. Any trial after any slice needs Hari's explicit approval and environment-gate approval (decision 6749565231).
- Missing evidence is absent, never zero (ADR-021 rules 13 and 14). Unknown never becomes a persistence status: a no-report original is `unconfirmed` (decision rule 5).
- Ordinary scenarios: `OutcomeClassifier`, the four-class table and schema-one readers are unchanged (R6). The new value hash (R7) is computed only for probe keys.
- No change to protocol sequence, gate, policy value, verdict rule, timeout, failure-name list of PR2 (beyond the additions Hari approves in section 4.3) or design text. Any need for one is a stop-rule event.

## 7. Never-ran inventory

Evidence is the trial logs and artifacts, not the code. Trials: the seven runs of `b10-abrupt-trial.yml`, all on `main`, all concluded `failure`: 42460966764 (`2d4792ee`), 42475024856 (`b4d6f9e8`), 42512105140 (`25cc301a`), 42536075365 (`ee36f1c6`), 42608381540 (`4163cd1a`), 42670600125 (`fa73aa08`), 42679791413 (`f5cc496f`, trial 7). Artifacts downloaded with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip` into new empty scratch directories; zip SHA-256:

| Run | Artifact | Zip SHA-256 |
|---|---|---|
| 42460966764 | 13609836611 | `bbc4030f30fceef33c156c7b00d8533342c7415887b9d59afab7f52db68a78e7` |
| 42475024856 | 13615377394 | `270597b9f683416aa72ef5230e90bf896973ac7b3fb9c9030bcbbcd1f6a75c83` |
| 42512105140 | 13629179730 | `a5b9c345121eecfc9b836706101ee776cbd3269d57e4150a04a4a632917ef9ad` |
| 42536075365 | 13639442741 | `0564049fa591fdfcdd8d4cfbac36794d43498cac72aeccec12666e800979af69` |
| 42608381540 | 13668809132 | `0c22924e47ed6b455e228f98be5016c6e3c97faab541d9ed2f9853bf1005eb30` |
| 42670600125 | 13699980854 | `b2e2c270f6133c86341eafd1aa5c95a4f0c391a9ecce2b6134707d19a4341bd1` |
| 42679791413 | 13704155185 | `bee17231882ffeb7ddc3134ba10a00ec4590f20e96dad409c050decf1f22cdcf` |

Phase modes recorded in each `phase-status.json`: trials 1 to 6 `start-line complete, loadgen failed`, each with diagnostic `b10_abrupt_publication_failed` at boundary `first_termination`; trial 7 `start-line complete, loadgen complete, loadgen2 failed`. No trial records any `verify`, `b10-sweep-line`, `b10-sweep`, `judge` or `candidate` mode.

Stages the verifier path depends on (upstream):

| Stage | Ran live | Evidence |
|---|---|---|
| U1 Verifier `start-line` mode in a trial | Yes, all 7 | `phase-status.json` mode `start-line` outcome `complete`; trial 7 `start-line.json`, `start-line-completion.json` |
| U2 First-instance publication of the seven objects | Yes, trial 7 only | Supervisor log `b10_abrupt_stage {"role":"supervisor","stage":"publication_complete",...}`; seven objects in the artifact |
| U3 Python first-publication acceptance | Yes, trial 7 only | `predecessor-binding.json` in the artifact; `loadgen2` mode rendered and applied (`render 0, apply 0`) |
| U4 Successor resend | Yes, trial 7 | Successor log `"stage":"probe_terminal","facts":{"arm":2,"is_original":0,"error_code":0,"persistence_status":2,"delivered":1,...}` |
| U5 Successor probe-cohort seal | Yes, trial 7 | Successor log `"stage":"cohort_sealed","facts":{"arm":2,"entry_count":1}` |
| U6 Successor evidence completion | Failed live | Successor log `b10_abrupt_guard_failed ... "member":"CreateProducerAsync","line":249,"facts":{"completion_complete":0,"evidence_faulted":0}` |
| U7 Successor publication of `ledger.jsonl` and later members | No | `phase-status.json` diagnostic `b10_abrupt_publication_ambiguous`, boundary `successor_publication`, site `download` line 394; only successor `registration.json` in the artifact |
| U8 Python successor receipt checks (`collect`) | No | Same; `collect` raised before `self.collect(files, observation)` |
| U9 Publication of cohort ledgers, inventories, post-drain and pre-acceptance reports | No; not implemented | First-child log shows local seals only (`cohort_sealed` arm 0 `entry_count 31240`, arm 1 `entry_count 6`); no such object in any artifact |
| U10 Fatal capture through death | No; not implemented | Trial 7 `termination.json`, `timeout-seal.json`, `control-seal.json`: `"fatal_capture_status": "not_implemented_pr3"` |

Verifier-path stages (this plan changes them):

| Stage | Ran live | Test in this plan that runs it for real | Slice |
|---|---|---|---|
| V1 Abrupt listing and download from the ledger container | No line shows it | None offline: Azure Blob listing cannot run offline. Deployment assumption DA1 | 3b |
| V2 First-instance envelope, identity and binding read (.NET) | No line shows it | T1, T2 on trial 7 bytes | 3b |
| V3 Journal reconciliation (.NET) | No line shows it | T1, T3 on trial 7 journal and produced-byte variants | 3b |
| V4 Selection and termination checks (.NET) | No line shows it | T1, T3 | 3b |
| V5 Successor reader and receipt binding | No line shows it | T4 on successor bytes from the successor-fix real-statistics path (dependency D1) | 3b |
| V6 Timeout-cohort independent bookkeeping and six counts | No line shows it | T6 on published cohort files from the real `LocalLedgerStore` path | 3a, 3b |
| V7 Control-cohort independent bookkeeping | No line shows it | T6 | 3a, 3b |
| V8 Fatal capture write and read | No line shows it | T8 (child to real supervisor journal over the real pipe), T9 | 3a, 3b |
| V9 End-line read and window drain over `document-events` | No line shows it in a trial | T5 against Testcontainers Kafka | 3b |
| V10 Value hash of probe records | No line shows it | T5 | 3b |
| V11 Join of consumed probe records to journal and successor attempts | No line shows it | T5 with trial 7 identities and payload | 3b |
| V12 Per-arm classification, pairs, coordinates, loss | No line shows it | T5, T7 | 3b |
| V13 Eligibility reasons and `nonfinal_result` write | No line shows it | T7, T10 | 3b |
| V14 Verify mode invoked by the trial orchestration | No line shows it | T11 runs `b10_abrupt.py` with the real phase driver and a stub cluster; the live Job is deployment assumption DA2 | 3c |
| V15 Sweep line and late-write scan for abrupt identities | No line shows it | Written in the 3d revision; must run against Testcontainers Kafka | 3d |
| V16 Judge, verdicts, comparison, witnesses | No line shows it | Written in the 3d revision | 3d |
| V17 Candidate projection | No line shows it | Written in the 3d revision | 3d |
| V18 Scientific admission of the abrupt dialect | No line shows it | Written in the 3e revision | 3e |
| V19 C5 mutation of new targets | No line shows it | Main dispatch after merge (factory.md "Evidence pack") | 3d |

Trial 7 artifact files usable as fixtures: the seven first-instance objects, successor `registration.json`, `start-line.json`, `start-line-completion.json`, `broker-inventory.json`. They were scanned by script: none contains the pod's `AZURE_TENANT_ID` or `AZURE_CLIENT_ID` value, or the strings in the banned pattern of acceptance command C1. Not usable: `loadgen-pods.json`, `loadgen2-pods.json`, both `pods.json` and both container logs, which contain the tenant ID and client ID (AGENTS.md hard safety rule 7). `predecessor-binding.json` and `phase-status.json` are runner outputs, not trial-written evidence, and are not fixtures.

## 8. Test map

All new test classes are named with the prefix `B10AbruptVerify` (Verifier) or `B10AbruptFatal`/`B10AbruptPublication` (LoadGen) so the acceptance filters in section 10 select them. Fixture directory: `tests/fixtures/b10-abrupt/trial-42679791413/` with `first/` (seven objects), `successor/registration.json`, `start-line.json`, `start-line-completion.json`, `broker-inventory.json`, byte-identical to the artifact (SHA-256 in C1).

- T1 (R3). Real reader on the trial 7 first-instance fixtures. Expected literals: as in R3. Boundaries: the journal entry count equals the manifest's `journal_entry_count` `38`; 37 and 39 (one frame removed, one duplicated, applied to a copy) fail.
- T2 (R4). One mutation per check on a copy of the fixtures, each with every other byte unchanged: identity member changed (`run_id`, `source_commit`, `image_reference`, `pod_uid`, `instance_id`), descriptor length off by one, descriptor hash changed in one hex digit, unknown member added, member removed, `schema_version` 1, `dialect` changed, `scientific_admissible` true. Each yields reason `b10_restart_identity_unauthenticated` and absent counts.
- T3 (R3, R4). Journal variants on a copy of the trial 7 journal: sequence gap (frame 20 removed and later sequences unchanged), duplicate `accepted` for one attempt, `accepted` before its `prepared`, a `gap` frame appended, kill checkpoint `selected_attempt_ids` empty, kill selection naming sequence 3 instead of 2. Each fails with the reason R4 names; the empty selection yields `kill_too_late`.
- T4 (R5). Successor reader on the seven successor members produced by dependency D1. Variants: predecessor changed, same pod UID as the first, receipt descriptor of `kill-selection.json` changed, invocation key changed, invocation attempt ID equal to the original's. Each yields `b10_restart_successor_evidence_unavailable`.
- T5 (R7 to R10). Component test on the existing `LocalKafkaFixture` (Testcontainers, `confluentinc/cp-kafka:7.5.12`, digest `sha256:997883c60e6f1207fb5f779c7a8fdae57312a17f446fd5ea3eb8b73f52d4a1cf`). A real Confluent.Kafka producer writes probe records with the trial 7 identities: first instance `b10-42679791413-1-c1-r1-loadgen-h9svx`, the kill-arm original attempt IDs from the fixture journal, key `B10/b10-42679791413-1/C1/1/cross_restart`, value the bytes of `kill-selection.json` `payload_base64`, and the three headers in the shape of section 4.1. The successor resend uses instance `b10-42679791413-1-c1-r1-loadgen2-prrgv` and a new attempt ID. Then the real start-line high-watermark read, the real `WindowConsumer.Drain` and the real reader run. Cases and literal expectations, each in a fresh topic: (a) originals for sequences 2, 3, 4 and the resend written once each: kill arm `unreported_seen 3`, `unreported_unseen 0`, `resend_seen 1`, pair present 1; (b) resend only: `unreported_seen 0`, `unreported_unseen 3`, pair 0; (c) sequence 2 original only: pair 0, `resend_seen 0`, lost-resend finding 1 when the resend's receipt reports `delivered: true`; (d) the sequence 2 original written twice and no resend: replay, pair 0; (e) resend value changed in one byte: identity failure reason `b10_restart_identity_unauthenticated`; (f) a record with no `attempt-id` header under the probe key: structural problem, same reason. The control arm is exercised the same way with key `.../same_instance` and selected sequence 3, and a same-instance pair never sets the cross-restart positive.
- T6 (R1, R11). Real `B10AbruptProducer` timeout and control seal path writing through the real `LocalLedgerStore`, then the real publisher, then the real verifier reader and ordinary bookkeeping checks. Expected: counts equal to the produced ledger's own totals; one ledger row removed from a copy makes the six counts absent with `b10_restart_timeout_bookkeeping_unavailable`.
- T7 (R12, R13). The full per-repeat verify on trial 7 fixtures plus T5 case (a) records. Expected: eligibility not `eligible`, reasons containing exactly `b10_restart_fatal_capture_unavailable`, `b10_restart_timeout_bookkeeping_unavailable` and `b10_restart_successor_evidence_unavailable` when run without D1 and R1 outputs, and none of them when run with valid D1, R1 and R2 outputs.
- T8 (R2). Real child process (published apphost, as the existing `B10AbruptHost` process tests use) with a real supervisor and pipe; an error callback raised by a native producer test double before the kill reaches the journal as the PR3a record; a full queue latches the sticky gap. The native library's own fatal path cannot be forced offline without a broker fault; that part is deployment assumption DA3.
- T9 (R2, R12). `termination.json` produced by T8: actual coverage value; the previous literal `not_implemented_pr3` is rejected by the PR3b reader with `b10_restart_fatal_capture_unavailable`.
- T10 (R13). `nonfinal_result` written, read back, byte-identical; unknown member, missing member, fractional count and negative count each rejected.
- T11 (PR3c). Real `b10_abrupt.py` with the real phase driver and the existing stub cluster commands of `infra/scripts/phase/tests/test_b10_abrupt.py`: the verify mode is invoked after `loadgen2` completes and not before; a failed successor skips it and records the failure.

Unchanged and must keep passing: `B10AbruptRejectionTests` (2 tests), all Verifier component tests (13), the full Verifier suite, `test_b10_abrupt.py` (12) and `test_c5_workflow.py` (107).

Mutation-only build: tests that need the LoadGen test assembly follow the existing exclusion when `ParchwellVerifierMutationOnly=true` (`Parchwell.Verifier.Tests.csproj:20-22`), without changing the csproj.

## 9. Deployment assumptions

- DA1. `BlobProducerArtifactStore` lists and reads the `b10-abrupt-diagnostic` prefix in the `ledger` container with the verifier Job's workload identity. Offline tests use the in-memory store fed real bytes. Not covered by any test in this plan; blocking until Hari accepts the risk by name (question Q4).
- DA2. A verifier Job started by `b10_abrupt.py` in the trial namespace reaches `document-events` and the ledger container. Not covered offline; blocking until accepted (Q4).
- DA3. Confluent.Kafka 2.16.0 delivers a fatal error to the error handler with `IsFatal` true on the pinned image. No offline test can force a native fatal error without a broker fault; blocking until accepted (Q4).
- DA4. Successor outputs keep the formats in section 4.1 after the successor fix (dependency D1).

Dependencies: D1, the successor fix pull request planned under decision 6749565231, merged, with a test that produces the seven successor members through a real `KafkaStatisticsSession`; its exported bytes are T4's input.

## 10. Acceptance commands

Each command below was run in this planning session on a clean worktree of `main` at `f5cc496f`, .NET SDK 10.0.402, with a local Docker daemon. Observed output is quoted. Slice revisions add their own commands; these remain required.

- C0 baseline build, from repository root:
  ```
  dotnet restore Parchwell.slnx --locked-mode && dotnet build Parchwell.slnx --configuration Release --no-restore
  ```
  Observed: `Build succeeded.` `0 Warning(s)` `0 Error(s)`.
- C1 fixture integrity, from repository root, after the fixtures are committed (the script is in appendix A; it was run on a scratch copy with the same layout):
  ```
  python3 -I tests/fixtures/b10-abrupt/check_trial_fixtures.py tests/fixtures/b10-abrupt/trial-42679791413
  ```
  Observed: `fixture_check: 11 files pinned, no banned token, 6 descriptors bound`, exit 0.
- C2 full Verifier suite:
  ```
  dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build
  ```
  Observed on `main`: `total: 7389` `failed: 0` `succeeded: 7389` `skipped: 0`, exit 0. After a slice: exit 0, `failed: 0`, `skipped: 0`, total at least 7389 plus the slice's new tests.
- C3 existing abrupt rejection tests:
  ```
  dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"
  ```
  Observed on `main`: `total: 2` `failed: 0`, exit 0. After PR3b: exit 0, total at least 2 plus T1 to T7 and T10 test methods.
- C4 new verifier tests:
  ```
  dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build -- --filter-class "*B10AbruptVerify*"
  ```
  Observed on `main`: `succeeded: 0` and `Test run completed with non-success exit code: 8`, which the testing platform uses for zero tests run. After PR3b: exit 0, `failed: 0`, total equal to the number of test methods the slice adds for T1 to T7 and T10.
- C5 Verifier component tests (real Kafka):
  ```
  dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build -- --filter-class "*Component*"
  ```
  Observed on `main`: `total: 13` `failed: 0` `skipped: 0`, exit 0. After PR3b: exit 0, `skipped: 0`, total 13 plus the T5 methods. A run with no Docker daemon fails; it is not a pass.
- C6 LoadGen produced-byte export (the existing source of real produced bytes):
  ```
  PARCHWELL_B10_ABRUPT_WIRE_OUTPUT=<new empty directory> dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests
  ```
  Observed: `Total: 8, Errors: 0, Failed: 0, Skipped: 0`; the directory holds `export.json` and 14 files, seven under `first/` and seven under `successor/`. This export uses a proxy native producer and a synthetic process, not a real `KafkaStatisticsSession`; it is why T4 depends on D1.
- C7 phase script tests:
  ```
  python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_b10_abrupt.py'
  ```
  Observed: `Ran 12 tests` `OK`.
- C8 C5 workflow tests:
  ```
  python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_workflow.py'
  ```
  Observed: `Ran 107 tests` `OK`.
- C9 documentation: `npm ci --ignore-scripts && npm run check:docs` and `git diff --check`, under docs/agents/pull-requests.md rule 8. Not run in this session for a plan file; each slice runs them.

## 11. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Runtime-sized effort: C5 cut recomputation (cap from the item plan: one computation plus one consolidated recomputation per target; exhaustion `push -wip and hand off to the next session`); per-slice disposition caps of 20 documentation-warning, 20 verification-failure and 20 review-finding root causes (item plan; exhaustion `push -wip and hand off to the next session`); the C5 main dispatch (item plan: one dispatch, no automatic retry, `record the gap and continue` with a fix issue). Survivor triage has no cap in this plan and stops at first measurement under AGENTS.md "Before opening a pull request" rule 11. No other step scales with a runtime count: the fixture set is fixed at 11 files and T5's record count is fixed by its cases.

(b) Stricter than the parent plan: the never-ran inventory and the requirement that each stage's test runs real inputs come from decision 6749565231. The split proposal is stricter in ordering than the item plan, which lists PR3 as one pull request; it is a proposal for Hari, not a decision.

(c) Dependencies neither on `main` nor in the pull request: the successor fix (D1), checked against its source on `main` (`B10AbruptProducer.RunSuccessorAsync`, `B10AbruptHost.CreateProducerAsync` lines 244-250) and recorded live output (trial 7 successor log); DA1 to DA3 have no offline test and are listed under question Q4.

## 12. Sources

- docs/blueprint.md ADR-012, ADR-019, ADR-020, ADR-021 at `f5cc496f`.
- docs/operations/b10-abrupt-contract.md, docs/operations/verifier-contract.md (line 5) at `f5cc496f`.
- docs/agents/factory.md "Item lifecycle", "Authority reads", "Evidence pack" at `f5cc496f`.
- Code at `f5cc496f`: `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptPublisher.cs`, `B10AbruptProducer.cs`, `B10AbruptHost.cs`, `B10AbruptWire.cs`; `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs`, `Window/MessageIdentityReader.cs`, `Judgement/B10RepeatChecks.cs`; `infra/scripts/phase/b10_abrupt.py`, `phase.py`; `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs`; `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`.
- Trial artifacts and runs listed in section 7.
- [Confluent.Kafka PersistenceStatus](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.PersistenceStatus.html), as cited by ADR-021.

## 13. Open questions for Hari

- Q1. Split. The assigned PR3 scope is ten parts across LoadGen, verifier, phase scripts, two protected workflows and the C5 matrix (section 2). Which split: option A (five pull requests, recommended), option B (three), option C (one verifier pull request as the item plan lists, with F1 and fatal capture unowned), or another? Every later revision of this plan follows the answer.
- Q2. Publication gap. The first instance does not publish the timeout and control cohort ledgers, inventories and post-drain reports, or the arm pre-acceptance reports (F1), which ADR-012's B10 exception and ADR-021 rule 12 require the verifier to reconcile independently. Should PR3 publish them (option A's PR3a), or is that a separate decision?
- Q3. What a trial can answer. A one-repeat diagnostic trial cannot meet "at least 3 of the 5 declared C1 repeats must be eligible" (ADR-021 decision rule 2), and its output "is never published as a result". The most it can show is one repeat's nonfinal per-arm observation and eligibility reasons. Is that the intended meaning of "so a trial can answer the restart question" in decision 6749565231, or should the plan target the five-repeat governed run?
- Q4. Deployment assumptions DA1 (blob listing of the abrupt prefix by the verifier identity), DA2 (verifier Job in the trial namespace reaching the topic and ledger container) and DA3 (native fatal-error delivery on the pinned image) have no offline test. Each blocks approval until Hari accepts it by name or names a test.
- Q5. Reason words. Approve or change the proposed ineligibility reason literals in section 4.3; they will appear in published B10 evidence.
- Q6. Value hash. R7 adds value hashing to the shared window reader for probe keys only. Approve this change to a reader every scenario uses, or require a B10-only reader instead.

## 14. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 4, a requirement in section 5 or the scope in section 3, or when the successor fix changes a format this plan reads. It also stops at every cap in section 11 with that cap's exhaustion action.

## Appendix A. Fixture check script

```python
import hashlib, json, pathlib, re, sys
root = pathlib.Path(sys.argv[1])
pinned = {
 'first/abrupt-manifest.json': '3a25132454c41c9cbc70950f9ae5e80f9974ee48b9c03fc73b29aad098a9747a',
 'first/control-seal.json': '31537beac65841212c34c74311aa69e608167f4b98915f0983f2d47de029e8da',
 'first/journal.jsonl': '6fa8d06ecf5170d446e490733338b811aac94c6d1dd9103b28d8c2f69582d77f',
 'first/kill-selection.json': '0f1bff4a0581e2af2775666c06d0f21fb4f59abdc4405845153bbfcda2a90059',
 'first/registration.json': '8407008cd88b92f734b21c03385e54a48786478999b810da34c96990c3721454',
 'first/termination.json': 'eba48bdf255faa4a7db86a6de492de91d6c70740844897749ac57a97a77af9a9',
 'first/timeout-seal.json': '97bd832ef06681b1dcdcbd7a70bd13a9d4390bd803e9481d3a605b48faf36a7f',
 'successor/registration.json': '40656c1d1105dd00d4a2b0cb5d25e89a769b47cf19565acd99e9b1bceb735d1a',
 'start-line.json': '6aebc4abe8b49588435b232ae75ff274147e461b9b0eb35c8a3a85f8776129bf',
 'start-line-completion.json': '5de82e08d74fd766960f357be32f68bf36f1554aad5b5b571bfa52c61b98ed3f',
 'broker-inventory.json': 'dc354c03c619c8783720568fc944b9669e621858d29b000eb03280f6d6f5d235',
}
actual = sorted(p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file())
assert actual == sorted(pinned), ('file set', actual)
banned = re.compile(r'AZURE_|tenant|subscription|AccountKey|SharedAccessSignature|sig=|Bearer |password|secret', re.I)
for name, digest in pinned.items():
    raw = (root / name).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == digest, name
    assert not banned.search(raw.decode('utf-8')), ('banned', name)
def desc(d, name):
    raw = (root / ('first/' + name)).read_bytes()
    assert d['name'] == name and d['length'] == len(raw) and d['sha256'] == hashlib.sha256(raw).hexdigest(), name
m = json.loads((root / 'first/abrupt-manifest.json').read_bytes())['data']
for key, name in [('registration', 'registration.json'), ('journal', 'journal.jsonl'), ('timeout_seal', 'timeout-seal.json'), ('control_seal', 'control-seal.json'), ('selection', 'kill-selection.json')]:
    desc(m[key], name)
desc(json.loads((root / 'first/termination.json').read_bytes())['data']['manifest'], 'abrupt-manifest.json')
print('fixture_check: 11 files pinned, no banned token, 6 descriptors bound')
```

The script file must sit outside the fixture directory it checks, because the check requires the directory to hold exactly the 11 pinned files.
