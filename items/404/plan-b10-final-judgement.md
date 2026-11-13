# B10 final judgement contract-grade plan

Revision 4. Issue 404, pull request short name `B10 final judgement`. Status: not approved. Implementing tool: Codex ([6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008) point 1). Hari merges.

This pull request turns the per-repeat B10 evidence into final classifications. A new phase sweep scans each repeat's window plus its late range up to the phase sweep line, classifies the C0 and C1 timeout cohorts and the C1 probe arms over that union, and writes one final sweep projection per phase without the nonfinal diagnostic extras. The phase judge then reads the five final repeats and records, for each phase, the inputs and the selection that ADR-021's decision rules give; Hari still approves each verdict in the verdict pull request.

It depends on the scientific three-Job pull request, item (a) of the same decision. Section 3.1 states what this plan reads from (a), reconciled with (a)'s Revision 7. Where (a) leaves a point open, 3.1 says what this plan assumes. Implementation starts only after (a) merges, and any difference is a stop-rule event (section 12).

## 0. Changes

Since Revision 3 (`7961cd4d`), after plan check [6848531440](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6848531440):
- B1: every C1 repeat without a valid per-repeat object now has a late range `[next, sweep_line)` holding only its own instances' records, so no late record of that repeat is lost (F7).
- B2: F4's cases are defined only from Blob inputs the verifier reads: the A3 object and receipt, and the first pod's authenticated `termination.json` (`successor_permitted`). An accepted first publication (`successor_permitted` true) with no valid A3 object is F4's failure, E17. `phase-status.json` is no longer read by the verifier.
- B3: the lost-resend list excludes only `no_kill` repeats; `kill_too_late` repeats publish their control-arm finding. The `kill_too_late` path replaces checks 7 and 11 by an explicit rule that does not require `successor_permitted` (3.4).
- B4: C0 `status` follows the timeout table only; F3's `incomplete` input is the normal case. The "no timeout instance registered" row is C1 only; any C0 count other than one is F3's.
- B5: a TC failure also adds E7 `b10_restart_timeout_bookkeeping_unavailable` to restart, as the PR3c contract maps problem 7 (ADR-021 decision rules 2 and 6).
- B6: the measured mutation evidence stays as it is. Each new C5 target gets exactly one shard with no primary cut. `legacy_layout` and the measured-inventory suite stay on the 17 measured targets, so every value derived from run 42274873194 is unchanged (2.1 item 6). No measured value changes and no re-measurement is needed.
- B7: LA-F2 states how its evidence was taken.
- P2-A to P2-C of the check (apply in implementation) are folded into the text (2.2, 6 TP2, 3.5). TC is numbered F15. Item (a) Revision 7 is approved ([6848300952](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6848300952)), at the commit 3.1 reconciles against.

Since Revision 2 (`51a34df2`), after plan check [6846466122](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846466122) and decisions [6846521676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846521676), [6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008), [6846854445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846854445) and [6847743695](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6847743695):
- Q1 to Q11 are decided by 6846551008; their text moves into the sections they govern. Section 11 states `None`.
- Section 3.1 is reconciled with item (a) Revision 7 (`4196a032`): one-Job C0 with its nonfinal `verify` output (A2), a create-only receipt for the per-repeat C1 object (A3), the run-ID gate `^b10-[0-9]+-[0-9]+$` (A9), and the C0 judge gap (A8).
- B1: every C1 repeat without a per-repeat object gets a literal reason, from the authenticated termination receipt's class and outcome (3.4 "Repeats without a per-repeat object"). `termination.class` now has the receipt's four values, `unexpected_exit` included.
- B2: the timeout cohort is classified for every C1 repeat whose timeout instance published, independently of the restart chain (ADR-021 protocol rules 7 and 12). Restart-only failures change only `restart`. The control arm's lost-resend finding is published after an intended self-kill with an empty kill set, which is not a no-kill end.
- B3: `producer_versions` in the projection, from each ordinary manifest the sweep authenticates, is the candidate's version source (3.4, 3.5).
- B4: `cut_attempt_id` and `unconfirmed_attempt_ids` come from the journal and termination receipt that F6 re-authenticates.
- B5: under Q3 option A (decided), 2.1 item 6 names every changed `test_c5_workflow.py` constant, and 9(a) caps the new shard layout.
- B6: resolved by the widened sign-in fix ([6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008), "Verifier sign-in fix: option A, scope widened"; that plan's Revision 4 at `0b917178`, section 3.4). Its provider covers `start-line`, `verify`, `b10-sweep-line`, `b10-sweep` and `b10-abrupt-verify`; this plan adds `b10-final-sweep` to the same shared Kafka block (3.3). LA-F6 covers all of them.
- B7: Q1 to Q11 answered; no live assumption is accepted (6846551008 point 7, 6846521676 point 5).
- P2-A and P2-B (apply in implementation) folded into T25 and T26. P2-C to P2-K applied where they apply to this revision; CODEOWNERS corrected (2.3).
- `main` moved to `2077964b`. It changes only LoadGen test files and `items/444/`, none of this plan's files.

Since Revision 1 (`523808cf`), after plan check [6846147228](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846147228):
- B1: no new top-level folder. The sweep lives in the `VerifyRun` slice (`VerifyRun/B10Final/`), the projection record and codec in `Common/Phase/`, and the judge's B10 path in the `Judge` slice, so no slice uses another slice's types (2.1).
- B2: Q3 option A now lists the three further files it changes, including the protected `verifier-c5-mutation.yml` (2.1 item 6, Q3).
- B3: the final-path candidate is pinned member by member: root `status`, `reasons`, `versions`, and each repeat's `reasons`, `checks` and `identity`, and the codec's finality rule (3.5).
- B4: every repeat status and null is literal (3.4 "Status and nulls"). T7, T8 and T10 expect literal values; T10's literal moves to `eligibility_reasons`.
- B5: a wrong C0 registration count is per-repeat; only a cross-repeat instance collision is global (F5).
- B6: the C0 one-Job path is conditional on Q2; T14 tests the C0 rule on literal projection entries, and T14b, the handler path, runs only after Q2's chosen pull request supplies one-producer `verify` output (6, 9(c)).
- B7: `b10-final-sweep` gets the sign-in fix's token provider (3.3, 2.1 item 2), and LA-F6 names the residual live assumption.
- B8: Q1 to Q11 remain open for Hari.
- P2-A (apply in implementation) folded into T22. P2-B to P2-H applied: index wording (2.3); R16 keyed on run ID; `b10_abrupt.py` conditionally allowed (2.1 item 7, Q8); connection states labelled inferred (7); live-smoke rule as Q10; `comparison` list includes E16 and `termination.outcome` replaces `no_kill_reason` (3.4); empty C1 verifier set defined (P2). Fixture-change cap proposed as Q11. The version check no longer makes a disagreement `invalid`.

## 1. Goal and authority

Goal: after a scientific B10 run, each phase has one final, authenticated sweep projection and one candidate. The C0 candidate carries the five final timeout classifications and the ADR-021 decision rule 1 selection. The C1 candidate carries the five final restart eligibilities with reasons, the per-arm counts of protocol rule 13, the lost-resend findings and the decision rule 2 to 5 selection, plus the C1 timeout counts as a comparison only.

Authority, in creation order, under docs/agents/factory.md "Item lifecycle" step 3:
- The issue body and the decisions listed in `items/404/state.md` on `main` at `675f837277824d197ce922511b9f55c7d77623b2`, which covers comment 6830544567. They include ADR-021 on `main` and the PR3c approval [6828172387](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6828172387).
- [6816117386](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816117386) point 2: the later final sweep projection drops the nonfinal per-message table and extras, and "that later plan states the removal with a test".
- [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) Q17 option B: a separate pull request moves the legacy `scenario.yml` path to three C1 Jobs. That is item (a), not this plan.
- [6826128120](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6826128120) points 2 and 7: the PR3c plan's Q2 and Q5 each named "the sweep pull request" as the option not taken; both were decided option A, so nothing of Q2 or Q5 is deferred here beyond what section 2 lists.
- [6837706862](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837706862), [6837940546](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837940546), [6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192), [6844950677](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844950677) and [6845385609](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845385609): two diagnostic trials and the verifier sign-in fix. They authorize nothing for this pull request. The sign-in fix changes no file this plan changes (section 2.2).
- [6845612916](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845612916): this plan. It is applied, not restated: point 1 item (b) sets the subject and plan file (sections 1 and 2); point 2 sets the freeze, the caps, the plan rules, the implementing-tool question and the external contract with (a) (sections 2, 3.1, 9 and 11); point 3 sets branch hygiene (section 13); point 4 sets approval by Hari (section 12). Its closing line applies to this plan: "No implementation, trial, dispatch, results or expected-outcome change is authorized by this decision."
- [6846521676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846521676): item (a)'s answers. Point 2: item (a) moves C0 to one ordinary producer Job. Point 5: live assumptions accepted for planning only, and one governed diagnostic run of the scientific path after both pull requests merge, with its own authorization.
- [6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008): this plan's Q1 to Q11, and the widened sign-in fix. Applied, not restated: points 1 to 11 in sections 2.1, 3.3 to 3.6, 7, 9 and 11.
- [6846854445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846854445): C0 `verify` keeps the nonfinal shape (point 1); the C0 judge gap (point 2).
- [6847743695](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6847743695): the scientific rules apply only to run IDs matching `^b10-[0-9]+-[0-9]+$`.
- [6847745560](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6847745560): the sign-in fix's pull request 447 goes forward; context only.
- [6848300952](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6848300952): approval of item (a) Revision 7 at `4196a032`.

Evidence read, not authority: the PR3c plan at `48c52669fd40a3bb80ca0378e061f1d6153d6a22` (sections 2, 3.3 check 29, 3.4, 9 and 11), the evidence readings [6837900667](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837900667) and [6838450660](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6838450660), and the B10 expected-outcomes issue #356 for context only. The expected outcomes are Hari's separate decision; this plan sets and changes none of them.

What the PR3c contract defers to this pull request, read on `main` at `675f8372`: `docs/operations/b10-abrupt-contract.md` "Diagnostic verifier mode" says eligibility is "pending the sweep"; "Output `nonfinal_result`" says "The later final sweep projection must drop them and test that exclusion"; check 29 says "the later late-write sweep from the end line covers the advanced range"; and the last paragraph says "scientific admission and the late-write sweep remain later work". ADR-021 "Consequences" says "the workflow needs a sweep step", and protocol rule 14 names the "final sweep projection" as a closed schema-3 kind.

Catch-up for Revision 3: 342 comments on 4 pages, counted by script at 2026-11-13T08:40Z. New since Revision 2's push and read in full: 6846490767 (this plan's handoff), 6846521676, 6846551008, 6846818616, 6846854445, 6847229061, 6847557265, 6847743695, 6847745560, 6847852929, 6848168285, and the edited pointers 6838148214 and 6845836752. Item (a)'s plan checks 6846805964, 6846904630, 6847030526, 6847206853, 6847936069 and 6848149372 were identified by script; only their `Blocking findings` lines and, for 6848149372, the points raised for Hari were read, because they check another plan. Comment 6847855279 is a connector notice. Catch-up for Revision 1: 317 comments on 4 pages, counted by script at 2026-11-13T06:09Z; issue `updated_at` 2026-11-13T06:08:15Z. New since the state file's comment 6830544567, each read in full: 6837706862, 6837900667, 6837940546, 6837975192, 6838148214, 6838298262, 6838309006, 6838450660, 6844950677, 6844977688, 6845326893, 6845335358, 6845385609, 6845555307 and 6845612916. Every `## Decision` and `## Approval` heading on the issue was listed in creation order by script; the bodies whose subject is the sweep, the PR3c scope or this plan were read in full (6769057785, 6816117386, 6824437250, 6826128120 and the five above).

## 2. Scope

The scope is item (b) of 6845612916 point 1: the final sweep projection and the five-repeat judgement under ADR-021's decision rules, from `pending_sweep` evidence to the final restart and timeout classifications, dropping the nonfinal diagnostic extras.

Not in scope: the three C1 Jobs, the per-repeat C1 verifier and the C0 Job shape (item (a)); `scenarios/B10/expected.yaml` (#356); the verdict pull request and its evidence pack (docs/agents/factory.md "Item lifecycle" step 5); any workflow file; any trial or dispatch.

The plan freezes files and contracts, not lines (6845612916 point 2). Line numbers below are references, not boundaries.

### 2.1 Files allowed to change

1. New code, placed so that `tests/Parchwell.ArchitectureTests/CompiledArchitectureTests.cs` lines 52-79 pass unchanged (a slice is the first namespace segment after `Parchwell.Verifier`; only `Common` is shared): the sweep in `src/Parchwell.Verifier/VerifyRun/B10Final/`; the projection record and its codec in new files under `src/Parchwell.Verifier/Common/Phase/`; the judge's B10 final path in new files under `src/Parchwell.Verifier/Judge/`. The judge reads the projection only through the `Common/Phase/` codec and never uses a `VerifyRun` type. File split and private names are the implementer's choice.
2. `src/Parchwell.Verifier/Program.cs`, `VerifierHostOptions.cs` and `VerifierHostOptionsValidator.cs`: mode `b10-final-sweep` (3.3), including its entry in the mode list of the shared Kafka block that the sign-in fix uses (3.3), and the B10 branch of `judge` (3.5).
3. `src/Parchwell.Verifier/Judge/JudgeHandler.cs`, `PhaseCandidate.cs` and `PhaseCandidateCodec.cs`: the B10 route to the new `Judge/` files and the `b10_final` candidate member (3.5). `PhaseJudge.cs` and every other C5 target do not change (2.2).
4. `src/Parchwell.Verifier/Common/Phase/PhaseWireCodec.cs`, `PhasePaths.cs` and `PhaseIdentity.cs`: the `b10-final-sweep` receipt mode, the two object paths and the derived Job name `{U}-{p}-b10-final-sweep` of 3.3 (P2-H of 6846466122). Item (a) freezes `PhaseIdentity.cs` for its own pull request only.
5. `tests/Parchwell.Verifier.Tests/`: new tests prefixed `B10Final`, and existing tests only as 2.3 states.
6. Mutation coverage (6846551008 point 3), with every value measured in run 42274873194 kept:
   - `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`: the new production files of item 1 appended to `mutate`, after the existing 17.
   - `c5_workflow.py`: each new file is one new `TARGETS` entry with exactly one part, so it adds one `PARTS` and `SHARDS` entry named `b10-final-` plus a lowercase suffix, and no `PRIMARY_CUTS` entry; `BASE_CONFIG` equals the new config. No existing entry changes.
   - `.github/workflows/verifier-c5-mutation.yml`: one matrix row per new shard, appended; existing rows and triggers unchanged (AGENTS.md hard safety rule 8). That workflow has no committed copy under `docs/operations/workflows/`.
   - `test_c5_workflow.py`, layout assertions that follow the files: `EXPECTED_JOB_NAMES` (line 57 onward) gains the new shard names; the shard counts at lines 376 and 2193 become 61 plus the number of new targets; the target count at line 2195 becomes 17 plus that number. `EXPECTED_PRIMARY_CUTS` (line 47 onward) is unchanged, because a one-part target has no cut.
   - `test_c5_workflow.py`, measured evidence unchanged: `legacy_layout` (lines 122-130) changes only its iterable, from `workflow.TARGETS.items()` to the first 17 entries of `workflow.TARGETS` (the measured targets, in their existing order), keeping its 17 counts and `strict=True`. Its five ordinary callers (lines 444, 550, 711, 1317 and 1637) and the measured-inventory suite (line 2476) then patch the same 17-target layout as on `main`. Every value at lines 2456-2633 stays, including 28 shards, 5000 and 5329, 6636, the 61-row table at line 2630, `Fraction(445371390747, 80000000)`, 5568 and the zero-complement list. The measured suite therefore proves the 17 measured targets only; the new targets have no measured corpus, and their first measurement is the post-merge run (2.3).
   - `test_c5_workflow.py` lines 369-375 and 2190-2200 require the layout files to agree. Every assertion not named above stays.
   - Item (a) changes no C5 list entry (its 2.3), so these edits do not conflict with (a).
7. `infra/scripts/phase/phase.py` and `infra/scripts/phase/tests/test_phase.py`: `B10_PHASE_MODES`, the post-repeat lifecycle count and the sweep step (3.6). `infra/scripts/phase/b10_abrupt.py` and `tests/test_b10_abrupt.py` only where (a)'s merged code puts the scientific C1 lifecycle records or the C1 judge gap of A6 and A8; trial behaviour stays byte-identical.
8. `charts/parchwell/values.yaml`, `values.schema.json`, `templates/jobs.yaml`, `templates/_helpers.tpl`, `tests/test_chart.py` and `tests/test_b10_abrupt_chart.py`: rendering of `b10-final-sweep` (3.6).
9. `docs/operations/b10-abrupt-contract.md` (owner of the final projection format, new section "Final sweep projection"), `docs/operations/verifier-contract.md` (B10 phase sweep and candidate sections, citing it) and `docs/operations/b10-workflow.md` (post-repeat steps).
10. `items/404/state.md` and a new receipt `items/404/b10-final-judgement-receipt.md`.

### 2.2 Paths that must not change

- Every file under `.github/` and `docs/operations/workflows/`, except the matrix rows of `verifier-c5-mutation.yml` (2.1 item 6). The `judge` and `candidate` workflow steps already call `phase.sh judge` and `phase.sh candidate` (`.github/workflows/scenario.yml` lines 203-212 and 627-636).
- `infra/ephemeral/**`, `infra/persistent/**`, `infra/cluster/**`.
- `src/Parchwell.LoadGen/**`, `tests/Parchwell.LoadGen.Tests/**`, `src/Parchwell.Kafka/**`.
- Every file in the C5 `mutate` list (`stryker-c5-config.json` lines 17-35), including `PhaseJudge.cs`, `B10JudgeChecks.cs`, `B10SweepChecks.cs`, `B10SweepHandler.cs`, `B10LateWriteChecks.cs`, `B10SweepEvidenceCodec.cs`, `B10PhaseRegistrationReader.cs`, `B10RepeatChecks.cs` and `B10IntakeChecks.cs`. The legacy `b10-sweep` mode stays in the code, unchanged, and is no longer scheduled for scientific B10 (3.6).
- `src/Parchwell.Verifier/VerifyRun/Abrupt/**` and `StartLine/**`: reused unchanged. `OutcomeClassifier.cs`, `WindowConsumer.cs`, `MessageIdentityReader.cs` and `RunWindow.cs`: reused unchanged.
- The verifier sign-in fix's changes (its plan Revision 4 at `0b9171787de9d2b285dd1ef087fa8033cea10240`, section 2.2): its one registration line in the shared Kafka block of `Program.cs` and its new file under `Common/Kafka/`. This plan changes that block only by adding `b10-final-sweep` to its mode condition. `src/Parchwell.Kafka/**` does not change.
- The diagnostic trial's behaviour in `infra/scripts/phase/b10_abrupt.py` (2.1 item 7).
- `tests/Parchwell.ArchitectureTests/**`.
- `docs/blueprint.md`, `docs/operations/a01-runtime-limits.json`, `scenarios/**`, `results/**`. `scenarios/B10/expected.yaml` must not be created.
- `AGENTS.md`, `CLAUDE.md`, `docs/agents/**`, `README.md`, any `*.csproj`, `Directory.*.props` and `*.sln*`. No new project, assembly reference or NuGet package.
- Every existing test assertion, except under the standing fixture rule of 2.3.

### 2.3 Protected paths, CI and existing tests

- Protected: `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**` match `Parchwell.Verifier*/` in `.github/CODEOWNERS` line 16, and `.github/workflows/verifier-c5-mutation.yml` matches `/.*/` (line 7), so Hari merges. `/docs/operations/` is listed with no owner (line 6), so the three docs files are not owned through CODEOWNERS.
- Required checks: `.NET verification` and `Lifecycle verification`. `docs-advisory` is advisory.
- Mutation: the new files of 2.1 item 1 join the C5 list (6846551008 point 3), so docs/agents/factory.md "Evidence pack" requires a passing `verifier-c5-mutation.yml` run on `main` after merge before any evidence pack is published. That run is a dispatch this pull request does not make.

Standing fixture rule (`plan-pr` "Plan rules" rule 2, modelled on [6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2). When an existing test's fixture is rejected by a check this plan adds or tightens, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each change with the check that forces it. The cold review and Codex review check each listed change.

Inventory of existing tests whose fixtures the changes would reject, read on `main` at `675f8372` (section 3.6 changes the post-repeat sequence):
- `infra/scripts/phase/tests/test_phase.py` lines 26, 273-274, 383-394, 469-481 and 522-527 name `b10-sweep` in the expected B10 sequence, deadlines or mode loops. They follow rule (a): `b10-sweep` becomes `b10-final-sweep` where the scientific sequence is asserted.
- `test_phase.py` lines 309, 367, 436 and 466 assert `lifecycle_evidence_gap` at record index 20, which is 4 `B10_MODES` times 5 repeats (`phase.py` lines 26 and 944-972). (a) leaves these legacy assertions on the unreachable legacy path (its 3.2); the scientific path's lifecycle check is new (3.6 P2) and keeps the literal `lifecycle_evidence_gap`.
- `charts/parchwell/tests/test_chart.py` lines 42-43, 62, 92-135, 155, 235-251 and 294-303, and `test_b10_abrupt_chart.py` line 125, enumerate verifier modes. They gain `b10-final-sweep` under rule (a); every assertion on `b10-sweep` stays, because the chart still renders it.
- Verifier tests: no existing test fixture changes, because no existing reader changes. `JudgeHandlerTests`, `PhaseJudgeTests`, `B10SweepHandlerTests`, `B10SweepChecksTests` and `PhaseCandidateCodecTests` keep their B10 cases on the legacy path. A legacy B10 candidate without `b10_final` stays valid (R16).

Item (a) changes several of the same test files first. Where (a)'s merged tests already differ from this inventory, the implementer applies the rule to (a)'s merged text and the receipt records it. Cap: at most 40 existing test cases changed under this rule, counting each test method or `InlineData` row whose expected values change; exhaustion `push -wip and hand off to the next session` (6846551008 point 11). The `test_c5_workflow.py` constants of 2.1 item 6 are decided by 6846551008 point 3 and do not count toward the cap.

## 3. External contracts

### 3.1 Contract with item (a), the scientific three-Job pull request

Read: (a)'s plan `items/404/plan-b10-scientific-three-job.md`, Revision 7, at `4196a0325fd91534239efbc70ef3dbafd6dfe10e`, file SHA-256 `11efb0f8ac45a92434e3ba5ad868829d0b0f0b76b347305546bb7e9e7ef57e44`, recomputed by script (pointer 6845836752, edited 2026-11-13T08:27:26Z; check 6848149372 states `Blocking findings: none`). Sections read in full: 2, 3.1 to 3.8, 5 and 11. (a) is approved at exactly this commit and hash ([6848300952](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6848300952)), so this section is reconciled with the approved text (6846551008 point 8). Each item cites the (a) section it rests on, or says it is assumed. A difference found during implementation is a stop-rule event.

`U` is the scientific run ID; pattern S is `^b10-[0-9]+-[0-9]+$` ((a) 3); `r` is the repeat 1 to 5.

- A1. **C1 producer Jobs** ((a) 3.1, 3.2). Each C1 repeat runs `U-c1-r{r}-start-line`, `U-c1-r{r}-loadgen`, `U-c1-r{r}-loadgen-restart` and, only after the first publication was accepted, `U-c1-r{r}-loadgen-successor`. Every C1 start line carries exactly those three producer names, the successor's included. A repeat end lets the next repeat start; a phase stop applies no Job of a later repeat ((a) 3.2), so a phase can have fewer than five started repeats.
- A2. **C0** ((a) 3.1, 3.2, 3.8; 6846521676 point 2). Each C0 repeat runs `U-c0-r{r}-start-line`, one ordinary producer Job `U-c0-r{r}-loadgen`, and `U-c0-r{r}-verify`. The start line carries exactly `[U-c0-r{r}-loadgen]`. `verify` writes `U/C0/r{r}/result.json` with status `incomplete` or `invalid`, never `complete_valid`, `classification_final` false, `b10_window_checks_complete` true when its window checks passed, and a B10 intake of exactly one producer, `U-c0-r{r}-loadgen` with null predecessor; and `U/C0/r{r}/verify-completion.json` with `handler_exit_code` 1, `publication_acceptance` null and the intake present. `PhaseWireCodec`, `VerifierResultCodec` and `B10ProducerObservationsCodec` read the one-producer form for C0 ((a) 3.8).
- A3. **Per-repeat C1 object and receipt** ((a) 3.6). After each C1 repeat whose first publication was accepted, `U-c1-r{r}-b10-abrupt-verify` writes `U/C1/r{r}/b10-abrupt/nonfinal-result.json` (envelope `schema_version` 3, `dialect` `b10_abrupt`, `purpose` `scientific`, `synthetic` true, `scientific_admissible` false, `kind` `nonfinal_result`, identity `repeat` r; `data` as `b10-abrupt-contract.md` "Output `nonfinal_result`" defines it plus `earlier_repeat_records`; problems 1 to 10; reasons E1 to E13; `eligibility` `pending_sweep` or `ineligible`; `finality` `nonfinal`). Then, as its last external write, it creates `U/C1/r{r}/b10-abrupt/verify-completion.json`, a schema-1 `WorkerReceipt` with mode `b10-abrupt-verify`, `repeat` r, `job_name` `U-c1-r{r}-b10-abrupt-verify`, `identity` null, `handler_exit_code` 0, `cleanup_completed` true, `started_utc`, `completed_utc`, and exactly one artifact binding the object's bytes. `PhaseWireCodec` reads that receipt under (a)'s rules.
- A4. **Repeats with no object** ((a) 3.2 rows 3 to 5). No per-repeat verifier runs after a rejected first publication, which includes a no-kill end, an empty kill set and `successor_permitted` not true, or after unknown first termination, and none runs when the chain ended earlier (rows 1 and 2) or the phase stopped. When the parent published, the first pod's `termination.json`, `abrupt-manifest.json` and `journal.jsonl` are under A5 with their hash chain. Assumed, not stated by (a): those objects exist whenever the parent reached its publication step, because the parent writes `termination.json` last (`b10-abrupt-contract.md` "Parent, termination and first publication").
- A5. **Prefixes and names** ((a) 3.4, 3.6). First pod and successor: `U/C1/r{r}/b10-abrupt/<pod>/`. Timeout instance: `U/C1/r{r}/producer/<pod>/`. C0 instance: `U/C0/r{r}/producer/<pod>/`. Probe keys `B10/U/C1/{r}/same_instance` and `.../cross_restart`.
- A6. **Lifecycle records** ((a) 3.2). `phase-status.json` `b10_lifecycle` holds `producers` and `verifiers`, each entry `{job, repeat, mode, pod_name, pod_uid, finished_utc, exit_code}`, and `gaps` entries `{object, reason}` with reason `producer_termination_unavailable` or `verifier_termination_unavailable`, for both configurations. A Job not applied adds nothing.
- A7. **Readers reused** ((a) 2.2 item 4, 3.6). `VerifyRun/Abrupt/` accepts repeats 1 to 5 for pattern S and applies checks 1 to 30 with repeat r. Check 30 holds earlier-repeat records out of the per-repeat object's counts.
- A8. **Judge gaps** ((a) 3.2, 3.7). After (a), neither configuration runs `b10-sweep-line`, `b10-sweep` or `judge`; `judge` and `candidate` record `b10_c1_final_judgement_unavailable` (C1) and `b10_c0_final_judgement_unavailable` (C0), write no candidate and exit 1. The legacy `loadgen2` step, `b10_prepare_sweep` and the legacy sweep calls stay in `phase.py`, unreachable for science. This plan replaces both gaps (3.6 P4).
- A9. **Run-ID gate** ((a) 3.1, 3.8; 6847743695). The one-producer C0 rule, the three-name C1 rule and the three-name start line apply only to run IDs matching S. Every other B10 run ID keeps `main`'s two-producer behaviour, read by the frozen legacy `B10SweepHandler` and `JudgeHandler`. This plan's final path uses the same gate (3.3, 3.5).

### 3.2 Behaviour relied on (official sources)

- High watermark. Confluent.Kafka v2.16.0 `WatermarkOffsets.High` is "the offset of the latest message in the topic/partition available for consumption + 1" ([WatermarkOffsets.cs v2.16.0](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/WatermarkOffsets.cs), lines 60-64, fetched 2026-11-13; blueprint source S19). Context7 (`/confluentinc/confluent-kafka-dotnet`) returned the API reference for `QueryWatermarkOffsets` ("always queries the cluster") but not the "+ 1" wording, so the pinned source above is the citation. The sweep's half-open ranges `[end_line, sweep_line)` rest on this.
- That a record Event Hubs acknowledged before the sweep line was captured lies below that sweep line is not stated by any source read. It is LA-F3 (section 7), the same gap as the PR3c plan's LA-3b. Microsoft Learn's [Kafka troubleshooting guide](https://learn.microsoft.com/azure/event-hubs/apache-kafka-troubleshooting-guide), "Limits", says the Kafka interface has "for the most part" Kafka's general behaviour; searched with the Microsoft Learn tool on 2026-11-13.
- No other Kafka, librdkafka, Confluent.Kafka or Event Hubs behaviour is new in this plan. The drain, record identity and watermark reads reuse `WindowConsumer`, `MessageIdentityReader` and `VerifierKafkaSession` unchanged.

### 3.3 Mode `b10-final-sweep`

- `Verifier:Mode` `b10-final-sweep`, one Job per scientific B10 phase, `{U}-{p}-b10-final-sweep`, after `b10-sweep-line` and before `judge`. Required options are those `b10-sweep` requires today (`VerifierHostOptionsValidator.cs` lines 35-37, 55-73, 105-117 and 356-378), with `B10SettleIntervalMilliseconds` 60000. Scenario `B10`, phase `C0` or `C1`, run ID matching S (A9). Any other value exits 1 before any read.
- Outputs, both create-only in container `verifier`: the projection `U/P/b10-final-sweep.json` (3.4) and the receipt `U/P/b10-final-sweep-completion.json`, a `WorkerReceipt` schema 1 with mode `b10-final-sweep`, repeat and identity null, cleanup true, and one artifact binding the projection by length and SHA-256.
- Exit 0 when the projection was created and read back byte-identical, whatever it records. Any publication failure exits nonzero. This differs from `b10-sweep`, whose exit 1 means "not every repeat is final" (`B10SweepHandler.cs` line 131): the judge reads finality from the projection, not from the exit code.
- Kafka token: `b10-final-sweep` is added to the mode list of the shared Kafka block of `Program.cs`, where the sign-in fix registers its provider once (that plan's Revision 4 at `0b9171787de9d2b285dd1ef087fa8033cea10240`, section 3.4: "A mode added later to that block's mode list resolves the same provider with the section 3.2 values without a registration of its own"). The provider is in `src/Parchwell.Verifier/Common/Kafka/`, so the `VerifyRun` slice may use it. `NetworkTimeout` 2.0 s, `MaxRetries` 1, `Fixed`, `Delay` 0.2 s, `TokenTimeout` 00:00:10, all unchanged. The sign-in fix (pull request 447) must have merged first (section 12). LA-F6 is the residual live assumption.
- Budgets: the existing `b10_sweep` chart values, `MaxConsumedRecords` 9300000 and `MaxConsumedBytes` 429496729600 (`charts/parchwell/values.yaml` lines 51-54), and the `verify_job_seconds` 750 deadline (`values.yaml` lines 95-100). These are existing policy values, not measurements (LA-F2).

Enforced checks, numbered F1 to F15 (F15 is the timeout-cohort check, written TC below). A bracketed literal is a per-repeat reason of 3.4; "global" means `scan_complete` false and every repeat nonfinal.

- F1. Options as above. Failure exits 1 without an object.
- F2. Sweep line. Read `U/P/sweep-line.json` with the existing public `B10SweepEvidenceCodec.ReadSweepLine` and `MatchesSweepLine` (lines 33 and 47), unchanged. Its coordinates equal the options, its settle interval is 60000 and its `captured_at` is not later than this worker's start. Every per-repeat verifier receipt that exists (C0 `verify-completion.json`; C1 A3 receipt) has `completed_utc` no later than the note's `last_verifier_completed_utc`. Failure: global, reason `b10_final_sweep_line_invalid`.
- F3. Repeat inputs, C0 (A2). For each repeat, `U/C0/r{r}/result.json` and `verify-completion.json` exist and the receipt passes `PhaseWireCodec`, with mode `verify`, `handler_exit_code` 1, `cleanup_completed` true, one artifact equal to the result's length and SHA-256, the result's identity and intake equal to the receipt's, and an intake of exactly one producer `U-c0-r{r}-loadgen`. The result has status `incomplete` (not `invalid`) and `b10_window_checks_complete` true. The start-line receipt, note and inventory pass the checks `B10SweepHandler.cs` lines 183-200 apply, with `expected_job_names` `[U-c0-r{r}-loadgen]`. The producer's publication snapshot and ledger pass the checks of lines 203-235 for its one producer. Failure: the repeat is `invalid` with [`b10_final_repeat_input_invalid`]; with the result `invalid`, also that reason.
- F4. Repeat inputs, C1. Each case is decided only from objects the sweep reads in Blob storage; the phase's `phase-status.json` is not an input. For each repeat, exactly one of:
  - (iii) no start-line receipt `U/C1/r{r}/start-line-completion.json` exists: the repeat did not start (phase stop or deadline, A1);
  - (i) the A3 object and receipt both exist; the receipt passes `PhaseWireCodec` under A3's rules and binds the object's bytes; the object passes `AbruptEvidenceCodec`'s schema-3 envelope rules with kind `nonfinal_result`, `purpose` `scientific` and identity `repeat` r;
  - (ii) the repeat started, neither the A3 object nor its receipt exists, and the first pod's authenticated `termination.json` (F6, checks 3 to 6) is absent or has `successor_permitted` not true. These are exactly the repeats after which (a) runs no per-repeat verifier (A4);
  - (iv) every other started repeat: an A3 object without a valid receipt, a receipt without a valid object, or no A3 object and receipt although `termination.json` authenticates with `successor_permitted` true. This is F4's failure: restart [E17].
  The timeout cohort of (i), (ii) and (iv) follows F15, so restart-only evidence never invalidates it. A repeat in (iii) is `incomplete` with [`b10_final_repeat_not_started`], `timeout` null and `restart` `ineligible` with [E17].
- F15 (TC). Timeout cohort, C1, independent of the restart chain (ADR-021 protocol rule 12). For each repeat in (i), (ii) or (iv) whose timeout instance registered: the sweep reads the timeout instance's evidence through `VerifyRun/Abrupt/`'s timeout readers (`TimeoutEvidenceReader`, `TimeoutPublicationAcceptance`) and applies `b10-abrupt-contract.md` checks 16 to 19 to it, with the repeat's window of F7. Failure: the repeat is `invalid` with [`b10_final_timeout_evidence_invalid`], and restart adds E7 `b10_restart_timeout_bookkeeping_unavailable` (the PR3c contract's reason for the same evidence failure, problem 7; ADR-021 decision rules 2 and 6). In (i), the A3 object's `timeout_instance_id` must equal the instance found; a difference is F6's.
- F5. Registrations. C0: one registration per repeat bound to `U-c0-r{r}-loadgen`; a different count is F3's per-repeat failure. C1: the timeout registration under `producer/` (a mismatch is TC's failure), and the first-pod and, when present, successor registrations under `b10-abrupt/` (A5), each bound to its A1 Job name (a mismatch is restart [E17] only). Only one condition is global: an instance ID registered in two repeats of the phase, reason `b10_final_ownership_invalid`, because a misassigned instance corrupts every later repeat's exclusion (ADR-021 "Consequences", second bullet).
- F6. Re-authentication (C1 restart chain). For each C1 repeat whose first pod registered, the sweep reads the first pod's registration, `termination.json`, `abrupt-manifest.json`, `journal.jsonl` and reports, and, when present, the successor's evidence, through the readers of `VerifyRun/Abrupt/` (A7), applying `b10-abrupt-contract.md` checks 3 to 13 and 21 to 28 as the per-repeat mode does. In (i), the identities, `cut` record, control and kill unconfirmed sets, invocations and member attempt IDs it finds must equal the A3 object's. In (ii) and (iv), only the checks that the termination class permits are applied (3.4 "Repeats without a per-repeat object"). A failure, or a difference from the A3 object, is restart [E17]; it changes neither `status` nor `timeout`.
- F7. Windows. C0 and C1 (i): the original window is `[start_line, end_line)` from the per-repeat input (C0 `result.json`; C1 the A3 object), and the late range is `[end_line, sweep_line)`. C1 (ii) and (iv): no end line was read, so the original window is `[start_line, next)` and the late range is `[next, sweep_line)`, where `next` is the next started repeat's start line, or the sweep line for the last started repeat (then the late range is empty). In every late range only records carrying one of the repeat's own instances are the repeat's (F9), so a record of repeat r's instance appended after `next` is counted in repeat r (ADR-021 "Late-write scan", decision rule 7). Per partition, within a repeat: start line at most end line (or `next`) at most sweep line, as `B10LateWriteChecks.BuildWindows` requires (lines 16-30); and across repeats: each started repeat's end line (or `next`) at most the next started repeat's start line. `BuildWindows` checks only the first; the sweep adds the second. Failure: global, reason `b10_final_vector_invalid`.
- F8. Drain. One Kafka session drains every original window and late range within `SafetyStop` and the record budget, as `b10-sweep` does. Any incomplete drain: global, reason `b10_final_drain_incomplete`.
- F9. Ownership. A record in repeat r's original window whose `instance-id` is neither one of repeat r's instances nor an earlier repeat's makes repeat r `invalid` with [`b10_final_owner_invalid`] and, for C1, restart [E15] (ADR-021 "Late-write scan": a record of any other instance "still invalidates the repeat"). A record carrying an earlier repeat's instance is excluded from repeat r and counted only in its own repeat (ADR-021 "Late-write scan"). In repeat r's late range, only records carrying one of repeat r's instances are repeat r's; others are skipped, as `B10LateWriteChecks.Classify` skips them today.
- F10. Union. Repeat r's classified records are the union of its owned original-window and owned late records, each `(topic, partition, offset)` counted once (ADR-021 decision rule 7).
- F11. Join. Every record in the union joins one authorized attempt: a timeout ledger row (instance, attempt, key and sequence equal, key not an arm key); a first-pod `prepared` attempt or warm-up; or a successor invocation, under the join rules of `b10-abrupt-contract.md` checks 19 and 20. C0 records join the C0 ledger. A record carrying the timeout or C0 instance that joins nothing: `invalid` with [`b10_final_late_record_unjoined`]. A record carrying the first-pod or successor instance that joins nothing: restart [E15] only. ADR-021 "Late-write scan": "Unknown/conflicting identities invalidate"; protocol rule 12 keeps the instances' evidence independent.
- F12. Agreement, C1 (i) only. The original-window part of the union, classified alone, must reproduce the A3 object's values, comparing only fields the A3 object gives as non-null (P2-F of 6846466122). A difference in the six `timeout` counts: `invalid` with [`b10_final_window_mismatch`]. A difference in any member's `original_copies`, `resend_copies` or `other_copies`: restart [E15] only. C0 has no such check, because its nonfinal `result.json` is the input of the classification, not a classification to agree with.
- F13. Classification. Over the union: the timeout cohort's six counts with `OutcomeClassifier.Classify` over the timeout ledger rows; per member, `original_copies`, `resend_copies`, `other_copies` and `pair_count` (`original_copies` times `resend_copies`); per arm, the counts of 3.4 and the witness of ADR-021 protocol rule 13; warm-up records join and enter no count. A late record from the same attempt adds to its member's copies; it never creates an outcome class for an unreported attempt (ADR-012).
- F14. Resend loss. A member's `resend_lost` is true when its resend terminal has `delivered` true and the union holds no resend copy; false when the terminal is known and either condition fails; null when either is unknown. The A3 object's problem 9 (end line advanced) no longer nulls it, because the late range covers every offset at or after the end line up to the sweep line.

### 3.4 Output `final_sweep_projection` (`U/P/b10-final-sweep.json`)

The envelope has exactly `schema_version` 3, `dialect` `"b10_abrupt"`, `purpose` `"scientific"`, `synthetic` true, `scientific_admissible` (true when `data.scan_complete` is true, otherwise false), `kind` `"final_sweep_projection"`, `identity` and `data`. The `purpose` literal `scientific` is decided (6846551008 point 4). Identity has exactly `run_id`, `scenario_id` (`B10`), `configuration_id`, `phase`, `source_commit`, `image_reference` (the verifier image), `job_name`, `pod_name` and `pod_uid`.

`data` has exactly:
- `sweep_line`: `{container, name, length, sha256}` of the note, or null when F2 fails.
- `scan_complete`: boolean.
- `residual_risk`: exactly `A late append after the phase sweep line is not detected.` (the constant at `B10SweepEvidence.cs` line 8).
- `repeats`: exactly five objects, repeat 1 to 5.

Each repeat object has exactly:
- `repeat`; `source`: the reference of its per-repeat input object (C0 `result.json`, C1 the A3 object), or null when absent.
- `producer_versions`: an array, in instance-ID order, of `{instance_id, workload_generator, confluent_kafka, librdkafka, dotnet}`, one per ordinary producer manifest the sweep authenticated for the repeat (C0: its producer; C1: the timeout instance), read from the manifest's `generator_version` and `runtime_versions` as `ManifestReader` reads them (`VerifyRun/Input/ManifestReader.cs` lines 54 and 64). A missing value is null. The first pod and the successor are not version sources: neither writes an ordinary manifest (`b10-abrupt-contract.md` "Parent, termination and first publication", "Successor").
- `classification_final`: true only when `scan_complete` is true and no reason of this list applies to the repeat's timeout cohort.
- `status`: the timeout cohort's status, `complete_valid`, `invalid` or `incomplete`, exactly as the timeout table below gives it. A C0 per-repeat result is never `complete_valid` (A2); F3 requires it `incomplete` with window checks complete, and the sweep's own classification then makes the repeat `complete_valid` when no row before the last applies.
- `timeout`: `{ok, loss, phantom, reported_failure, duplicate, reorder}`, present only when `status` is `complete_valid`, otherwise null.
- `reasons`: sorted, distinct literals of the list at the end of this section, each at most 256 ASCII bytes, at most 100.
- `restart`: null for C0. For C1, exactly:
  - `eligibility`: `eligible` or `ineligible`.
  - `eligibility_reasons`: distinct literals, ordered as the 13 PR3c literals (`b10-abrupt-contract.md` "Eligibility reasons") followed by E14 to E17 below.
  - `termination`: `{class, outcome}`, copied from the authenticated `termination.json` members `termination` and `outcome` (`b10-abrupt-contract.md` "Parent, termination and first publication"), or `{class: null, outcome: null}` when no termination receipt authenticated. `class` is `intended_self_kill`, `no_kill`, `unexpected_exit` or `unknown`. `outcome` is the receipt's: the `no_kill.reason` literal for `no_kill`; `kill_too_late` for an intended self-kill with an empty kill set; `b10_abrupt_signal_failed` or `b10_abrupt_unexpected_exit` for `unexpected_exit`; `b10_abrupt_exit_unobserved` for `unknown`; otherwise null.
  - `first_instance_id`, `successor_instance_id`, `timeout_instance_id`: strings or null.
  - `comparison`: `both`, `cross_restart_only`, `same_instance_only`, `neither` or `unavailable`, from the final pair counts under ADR-021 decision rule 5 and the PR3c rule (`unavailable` under E1 to E4, E8 to E12, or E14 to E17).
  - `native_errors`: `{count, fatal_count, tail}` or null.
  - `arms`: null, or exactly `same_instance` then `cross_restart`, each with exactly `arm`, `cut_attempt_id` (the control arm's `cut.cut_attempt_id`, or the cross-restart arm's `kill_intent.cut_attempt_id`, from the journal F6 authenticated), `unconfirmed_attempt_ids` (the control arm's `cut.unconfirmed_attempt_ids`; the cross-restart arm's `termination.kill_unconfirmed_attempt_ids`), `unconfirmed_count`, `resend_invoked`, `originals_seen`, `resends_seen`, `pair_count`, `unreported_attempts`, `unreported_seen`, `unreported_unseen`, `lost_resends` and `witness`. Counts are JSON integers from 0 to 9223372036854775807, or null when unknown, never zero for unknown.

Dropped, and asserted absent by T12: every member's `members` table and its `copies`, `copies_truncated`, `copy_count`, `original_copies`, `resend_copies`, `other_copies` and `resend_terminal`; `problems`; `problem_details`; `window_limit`; `start_line`, `end_line` and `window_complete`; `earlier_repeat_records`; `finality`. This is the removal 6816117386 point 2 requires. Per-member values stay internal to the sweep, which uses them to compute the arm counts.

Eligibility. A C1 repeat is `eligible` exactly when all hold: it is in F4 (i), its A3 object exists with `eligibility` `pending_sweep` and `eligibility_reasons` []; its `problems` list is empty or exactly `[b10_abrupt_verify_end_line_advanced]` (problem 10 adds E3 under A3, so it is never alone); `scan_complete` is true; F6, F11 and F12 add nothing; and the final `comparison` is not `unavailable`. Otherwise it is `ineligible` with the A3 object's reasons plus any of:

| # | Literal (6846551008 point 5) | Condition |
|---|---|---|
| E14 | `b10_restart_sweep_unavailable` | `scan_complete` false |
| E15 | `b10_restart_late_record_unjoined` | F11 or F12 for this repeat |
| E16 | `termination.outcome` itself, for example `kill_too_early` | `class` `no_kill` (ADR-021 protocol rule 7: "its reason is the outcome the `no_kill` record names"), or `intended_self_kill` with outcome `kill_too_late` (protocol rule 6) |
| E17 | `b10_restart_input_unavailable` | F4 or F6 fails, or the repeat did not start, or its first publication was rejected for any reason E16 and E4 do not cover |

Repeat-level reason literals (6846551008 point 5), the only literals in a repeat's `reasons`: `b10_final_sweep_line_invalid`, `b10_final_repeat_input_invalid`, `b10_final_ownership_invalid`, `b10_final_vector_invalid`, `b10_final_drain_incomplete`, `b10_final_owner_invalid`, `b10_final_late_record_unjoined`, `b10_final_window_mismatch`, `b10_final_timeout_evidence_invalid`, `b10_final_timeout_absent` and `b10_final_repeat_not_started`. The last four replace Revision 2's `b10_final_window_unavailable` and follow from 6846551008's adoption of this plan's literals with B2 of check 6846466122 fixed (Q5 there). E-literals appear only in `restart.eligibility_reasons`.

Status, timeout and restart are set separately (ADR-021 protocol rule 12). `status`, `classification_final` and `timeout` describe the timeout cohort (C0: its one instance; C1: the timeout instance). `restart` describes the restart chain. A restart-only failure never changes `status` or `timeout`.

"Sweep-derived arm counts" means `originals_seen`, `resends_seen`, `pair_count`, `unreported_seen`, `unreported_unseen`, `lost_resends` and `witness`. "Journal-derived arm fields" means `cut_attempt_id`, `unconfirmed_attempt_ids`, `unconfirmed_count`, `resend_invoked` and `unreported_attempts`.

Timeout cohort, the earliest matching row applies:

| Case | `status` | `classification_final` | `timeout` | `reasons` adds |
|---|---|---|---|---|
| Global failure (F2, F5 collision, F7, F8) | `invalid` for every repeat | false | null | the global reason |
| Repeat did not start (F4 (iii)) | `incomplete` | false | null | `b10_final_repeat_not_started` |
| C1 only: started, no timeout instance registered (for example a metadata failure or a timeout render failure, A1) | `incomplete` | false | null | `b10_final_timeout_absent` |
| C0 F3 fails, including any registration count other than one | `invalid` | false | null | `b10_final_repeat_input_invalid` |
| C1 F15 fails | `invalid` | false | null | `b10_final_timeout_evidence_invalid` |
| F9 foreign owner | `invalid` | false | null | `b10_final_owner_invalid` |
| F11 unjoined timeout or C0 record | `invalid` | false | null | `b10_final_late_record_unjoined` |
| F12 timeout mismatch (C1 (i)) | `invalid` | false | null | `b10_final_window_mismatch` |
| None of the above | `complete_valid` | true | six counts | none |

C1 restart, every matching row adds its reasons; `arms` and `comparison` follow the first matching row that sets them:

| Case | `eligibility_reasons` adds | `arms` | `comparison` |
|---|---|---|---|
| Global failure | E14 | sweep-derived counts null | `unavailable` |
| Did not start (iii), or no first-pod registration | E17 | null | `unavailable` |
| F15 fails | E7 | unchanged by this row | unchanged by this row |
| (iv) | E17 | null | `unavailable` |
| F9 foreign owner | E15 | sweep-derived counts null | `unavailable` |
| (i) with an F4 or F6 failure | E17 | null | `unavailable` |
| (i), F11 unjoined probe record or F12 arm mismatch | E15 | sweep-derived counts null | `unavailable` |
| (i), otherwise | the A3 object's reasons | computed (F13, F14) | per the PR3c rule above |
| (ii), see "Repeats without a per-repeat object" | as that table | as that table | `unavailable` |

Repeats without a per-repeat object (F4 (ii)). The sweep reads the first pod's termination receipt and its hash chain (F6, checks 3 to 6). Then:

| Authenticated `termination.class` and `outcome` | `eligibility_reasons` | `arms` | Lost-resend finding |
|---|---|---|---|
| No receipt, or the chain fails | E2 `b10_restart_first_evidence_unavailable` | null | none (unknown) |
| `no_kill`, any outcome | E16, the `no_kill.reason` literal | null | none (ADR-021 protocol rule 7) |
| `intended_self_kill`, outcome `kill_too_late` | E16 `kill_too_late` | `same_instance` computed over F7's windows from the authenticated journal, applying checks 3 to 6, 8 to 10 and 21 to 25, with checks 7 and 11 replaced by K below; `cross_restart` with `unconfirmed_count` 0, `unconfirmed_attempt_ids` [], and every count 0, since an empty set has no members | the control arm's `lost_resends`, published (decision rule 3; protocol rule 7: "An intended self-kill, including one with an empty kill set, is not a no-kill end") |
| `unexpected_exit` or `unknown` | E4 `b10_restart_termination_unverified` | null | none (unknown) |
| `intended_self_kill` with a nonempty kill set, publication rejected for another reason (for example a capture gap) | E17 `b10_restart_input_unavailable`, plus E4 when check 7 fails | null | none (unknown) |

K, the `kill_too_late` replacement for checks 7 and 11: `termination` `intended_self_kill`, `outcome` `kill_too_late`, `exit_observed` true, `exit_code_local_observation` 137, `capture_gaps` [], manifest `gaps` [], `kill_unconfirmed_attempt_ids` [] and `successor_permitted` false (the contract makes it false for an empty kill set, `b10-abrupt-contract.md` "Parent, termination and first publication"); the last producing-thread record is `kill_intent`, and the journal-recomputed kill set is empty. A failure of K gives E4 instead of computed arms, with `arms` null.

A no-kill repeat's timeout cohort is still classified and published under decision rule 1 (protocol rule 7: "the C1 timeout instance keeps its own checks and its comparison is published").

### 3.5 Judge for scientific B10

- Mode name `judge`, Job, options, `CandidateOutputName` `U/P/candidate.json`, receipt and exit rule unchanged (`JudgeHandler.cs` lines 404-419). For scenario B10 with a run ID matching S (A9), the judge reads `U/P/b10-final-sweep.json` and its receipt instead of `b10-finalization.json`, through the new `Judge/` files and the `Common/Phase/` projection codec of 2.1 item 1. `PhaseJudge.cs` is not called for that path.
- Judge checks for that path, numbered G1 to G5: G1 the receipt has mode `b10-final-sweep`, the derived Job, exit 0 and one artifact equal to the projection's actual length and SHA-256; G2 the projection passes 3.4's closed format and its identity equals the options; G3 `sweep_line` equals the actual note reference; G4 each repeat's `source` equals the actual per-repeat input reference; G5 each repeat's registrations match F5. The judge reads the schema-3 `b10-abrupt/` registrations through a new reader in `Common/Phase/` (2.1 item 1), not through `VerifyRun/Abrupt/`, which the `Judge` slice cannot use (P2-D of 6846466122). Any failure makes the candidate `invalid` with root reason `b10_final_projection_untrusted` and `b10_final` null.
- Candidate (schema 1, `PhaseCandidateCodec`) on this path, member by member:
  - Root `status`: `invalid` when any G check fails, `scan_complete` is false or any repeat is `invalid`; otherwise `complete_valid` when all five repeats are `complete_valid`; otherwise `incomplete`. The judge exit code stays 0 only for `complete_valid` (`JudgeHandler.cs` line 414).
  - Root `gate`: null.
  - Root `versions`: `CommonVersion`'s rule (`JudgeHandler.cs` lines 389-395) applied to the projection's `producer_versions` entries of every repeat, with the expected observation count `5*2` replaced by the number of those entries, which must be at least 1. `workload_generator` comes from `workload_generator`, the others from their own members. A disagreement, an empty value or no entry gives null and root reason `version_unavailable`, as today; it does not make the candidate `invalid`. That the first pod and successor ran the same versions as the timeout instance is inferred from their equal checked `image_reference` (F6, check 4), not observed.
  - Root `counts`: the checked sum of the five repeats' `timeout`, present only when all five are `complete_valid`; otherwise absent with root reason `j7_counts_unavailable` (as `PhaseJudge.cs` lines 93-104 do). On `OverflowException` the root `status` is `incomplete`, `counts` is absent and `j7_counts_unavailable` is added, as `PhaseJudge.cs` line 102 does (P2-B of 6846466122).
  - Root `reasons`: the sorted, distinct union of every repeat's `reasons`, `b10_final_projection_untrusted` when a G check fails, and `j7_counts_unavailable`, `version_unavailable` and `timing_unavailable` as today, at most 100.
  - Root `image_digests`, `timings`, `diagnostics`: as today.
  - Repeat `status`, `reasons`, `counts` (from `timeout`) and `classification_final`: copied from the projection.
  - Repeat `checks`: `j1_repeat_slots` true when the repeat started (F4 (i) or (ii), or C0 F3's inputs present); `j2_publication_unaccepted` and `j3_verifier_receipt` true when F3 (C0) or F4 (C1) passed; `j4_producer_job_binding` and `j5_registered_producer_count` true when F5 passed for the repeat; `j6_broker_inventory_coverage` true when the per-repeat input's start-line check passed (C0 `result.json`; C1 A3 problem 1 absent); `j8_probe_inconclusive` false. Values keep the existing JSON types (`PhaseCandidateCodec.cs` line 112).
  - Repeat `identity`: the registration identity of the repeat's `U-{p}-r{r}-loadgen` instance, as the judge sets it for B10 today, or null when F5 failed.
  - Repeat `probe_observation` and `workload_observation`: null.
  - Optional members on this path: each repeat omits `broker_count`, `b04_recorded_evidence`, `batching_recorded_evidence`, `b10_intake` and `b10_producer_observations`, and the root omits `b10_sweep` (not written, not null), because the codec applies `Exact` to a present `b10_sweep` and `b10_intake` requires two producers (`PhaseCandidateCodec.cs` line 152; P2-C of 6848531440).
  - Root `b10_sweep`: absent on this path. `PhaseCandidateCodec` accepts a B10 candidate without `b10_sweep` only when `b10_final` is present, and then applies to `b10_final.scan_complete` the rule it applies to `b10_sweep.scan_complete` today (lines 18-19, 80-86, 136-143). A legacy B10 candidate keeps today's rule.
  - Root `b10_final`, required on this path:
  - `projection` and `receipt`: references.
  - `scan_complete`, `residual_risk`: copied.
  - `repeats`: JSON-equal to the projection's `data.repeats`.
  - `timeout_rule` (C0; null for C1): `{complete_valid_final_repeats, duplicate_total, rule_selection}`. `duplicate_total` is the checked sum of `timeout.duplicate` over the five repeats, or null unless all five are `complete_valid` and final, or on overflow. `rule_selection` is `confirmed` when all five are and `duplicate_total` is at least 1, `untestable` when all five are and it is 0, and `held` otherwise (ADR-021 decision rule 1).
  - `restart_rule` (C1; null for C0): `{declared_repeats, eligible_repeats, eligible_repeat_numbers, qualifying_pair_repeats, lost_resend_finding_repeats, eligible_loss_repeats, rule_selection, selection_reason, subject}`. `declared_repeats` is 5. `qualifying_pair_repeats` are eligible repeats whose `cross_restart` `pair_count` is at least 1 and `unreported_seen` at least 1 (decision rule 3). `lost_resend_finding_repeats` are repeats, eligible or not, with an arm `lost_resends` of at least 1, excluding repeats whose `termination.class` is `no_kill` (ADR-021 decision rule 3 and protocol rule 7). A `kill_too_late` repeat is not excluded. `eligible_loss_repeats` are the eligible ones among them. Selection, in this order: fewer than 3 eligible gives `held` with `selection_reason` `fewer_than_three_eligible`; otherwise a qualifying pair gives `confirmed` with `selection_reason` null and `subject` exactly the full restart subject of ADR-021 "Restart decision rules"; otherwise eligible loss gives `held` with `eligible_loss_without_pair`; otherwise `untestable` with `no_cross_restart_duplicate_observed`. `subject` is null unless `confirmed`.
- The member names say `rule_selection`, not `verdict`: this is the selection ADR-021's rules give, an input to the verdict proposal. The candidate carries it (6846551008 point 6).
- No envelope member of the projection (`dialect`, `purpose`, `scientific_admissible`, `kind`) is copied into the candidate. `phase.py` `reject_b10_abrupt_science` (lines 279-307) rejects any candidate containing `dialect` `b10_abrupt` or `scientific_admissible` false anywhere, so the candidate must hold neither.
- The candidate holds every rule-13 field the evidence pack needs, because ephemeral Blob storage is destroyed with the run (ADR-020) and the workflow uploads only the candidate and the artifacts directory (`scenario.yml` lines 213-232 and 637-656).

### 3.6 Phase and chart

- P1. For scientific B10 (A9), after the repeats the phase runs `b10-sweep-line`, then `b10-final-sweep`, then the existing `judge` step. `b10-sweep` is not run for scientific B10. The judge and candidate commands and the workflow are unchanged.
- P2. Lifecycle evidence for the sweep line, scientific B10 only (A9). A new step, not the legacy `b10_prepare_sweep` (A8), reads `b10_lifecycle` (A6). It requires one `producers` entry per producer Job `phase-status.json` records as applied, one `verifiers` entry per applied per-repeat verifier Job (C0 `verify`; C1 `b10-abrupt-verify`), and no `gaps` entry. Any difference returns `lifecycle_evidence_gap` and skips `b10-sweep-line`, as `main` does (`phase.py` lines 923-924, 970). `b10_last_loadgen_terminated_utc` is the latest producer `finished_utc`; `b10_last_verifier_completed_utc` is the latest verifier `finished_utc`, or, when no verifier Job was applied, the latest producer `finished_utc`. The settle rule (`phase.py` line 927: 60 s after the last producer termination, or the last verifier completion if later) is unchanged. With no producer Job applied at all, the step returns `lifecycle_evidence_gap`.
- P3. The chart renders `b10-final-sweep` with the options and deadline of 3.3, a verifier-role Job on `workload`, run IDs matching `^b10-[0-9]+-[0-9]+$` only, scenario B10, phase C0 or C1, and no repeat. `values.schema.json` adds the mode to its enums and rejects it for trial run IDs.
- P4. For scientific B10 (A9), `judge` and `candidate` no longer record `b10_c1_final_judgement_unavailable` or `b10_c0_final_judgement_unavailable` (A8): each configuration runs `b10-sweep-line`, `b10-final-sweep` and `judge` as P1 states, and the candidate step downloads and checks the candidate as it does on `main`. The two gap literals stay defined and unused; no test asserts them for science after this pull request.

## 4. Requirements

Verifier requirements are observed at the real handler through the mode's entry point, reading the object it writes.

- R1. WHEN all five C1 repeats are eligible pending the sweep and no late record exists, THEN every repeat is `eligible`, its counts equal the A3 object's, and `scan_complete` is true.
- R2. WHEN a cross-restart resend copy lands in repeat r's late range only, THEN repeat r's `resends_seen`, `pair_count` and `comparison` include it, and its member `resend_lost` is false.
- R3. WHEN repeat r's A3 object has problem 9 only and the late range holds the resend copy, THEN repeat r is `eligible` and `resend_lost` is false; WHEN the late range holds no copy and the terminal is delivered, THEN `resend_lost` is true and repeat r is in `lost_resend_finding_repeats`.
- R4. WHEN a record of repeat 2's instance lands in repeat 3's original window, THEN it counts only in repeat 2 and repeat 3 is unaffected.
- R5. WHEN a record carrying no registered instance lands in an original window, THEN that repeat is `invalid` with `b10_final_owner_invalid`.
- R6. WHEN an owned timeout or C0 record joins no attempt, THEN the repeat is `invalid` with `b10_final_late_record_unjoined`; WHEN an owned probe record joins no attempt, THEN only `restart` changes, with E15 and null sweep-derived arm counts.
- R7. WHEN the sweep line is missing, invalid or the drain is incomplete, THEN `scan_complete` is false, every repeat is nonfinal with null counts, every C1 repeat is `ineligible` with E14, and the C1 `rule_selection` is `held` with `fewer_than_three_eligible`.
- R8. WHEN a C1 (i) repeat's original-window timeout counts disagree with the A3 object, THEN `invalid` with `b10_final_window_mismatch`; WHEN only arm copies disagree, THEN E15 and `status` unchanged.
- R9. WHEN a C1 repeat ended on the no-kill path, THEN it is `ineligible` with the `no_kill.reason` literal, its `arms` are null, it is in no lost-resend list, AND its timeout cohort is classified and published (`status` per the timeout table).
- R9a. WHEN a C1 repeat is an intended self-kill with an empty kill set, THEN it is `ineligible` with `kill_too_late`, and a control-arm lost resend is in `lost_resend_finding_repeats`.
- R9b. WHEN restart evidence fails F4 or F6, THEN `status` and `timeout` are those the timeout table gives without that failure.
- R9d. WHEN F15 fails, THEN the repeat is `invalid` AND restart is `ineligible` with E7.
- R9e. WHEN a record of a C1 (ii) or (iv) repeat's instance lands after the next started repeat's start line, THEN it counts in that repeat only.
- R9c. WHEN `termination.class` is `unexpected_exit` or `unknown`, THEN E4; WHEN no termination receipt authenticates, THEN E2.
- R10. WHEN any field of the dropped list of 3.4 would be written, THEN the build fails its test (T12): the projection holds none of them.
- R11a. WHEN a scientific C0 repeat carries two producer registrations, THEN it is `invalid` with `b10_final_repeat_input_invalid`.
- R11. WHEN all five C0 repeats are final with a duplicate total of at least 1, THEN `timeout_rule.rule_selection` is `confirmed`; with total 0, `untestable`; with fewer than five final, `held` and `duplicate_total` null.
- R12. WHEN at least three C1 repeats are eligible and one has a qualifying pair, THEN `confirmed` with the full subject, AND every lost-resend finding is still listed.
- R13. WHEN at least three are eligible, none has a qualifying pair and an eligible repeat has a lost resend, THEN `held` with `eligible_loss_without_pair`.
- R14. WHEN at least three are eligible, none has a pair and no eligible repeat has a loss, THEN `untestable` with `no_cross_restart_duplicate_observed`, whatever the ineligible repeats' losses.
- R15. WHEN fewer than three are eligible, THEN `held` with `fewer_than_three_eligible`, whatever the pairs.
- R16. WHEN the judge runs for B10 with a run ID that does not match S, THEN it takes the legacy path and its behaviour and candidate bytes are unchanged.
- R17. WHEN the candidate is written on the final path, THEN it contains no `dialect`, `purpose` or `scientific_admissible` member anywhere, and `phase.py` `check_candidate` accepts it.
- R18. WHEN the projection or its receipt fails G1 to G5, THEN the candidate is `invalid` with `b10_final_projection_untrusted` and `b10_final` is null.
- R19. WHEN `b10-final-sweep` runs with a trial run ID, THEN option validation exits 1 before any read, and the chart schema rejects the render.

## 5. Invariants and safety

- No workflow file, trigger, dispatch, trial, `results/` file or expected outcome changes. No verdict is written; `rule_selection` is an input to the verdict pull request, which Hari approves separately for each B10 verdict (docs/agents/factory.md "Item lifecycle" step 5).
- Unknown is never zero. Missing evidence never becomes zero evidence. No replacement repeat and no favourable subset (ADR-021 decision rule 2).
- Probe records never enter the timeout totals. C1 timeout counts are a comparison only (decision rule 1).
- The diagnostic trial path is unchanged: `b10-abrupt-verify` and `b10_abrupt.py` keep their behaviour, and the trial's `nonfinal_result` stays nonfinal.
- No Azure credentials in any test. Kafka tests use `LocalKafkaFixture`.

## 6. Test map

Evidence sources:
- E: abrupt bytes from `B10AbruptWireProducerTests` and E-T ordinary timeout bytes from `B10TimeoutEvidenceExportTests`, as in the PR3c plan section 6.
- B: a five-repeat builder that writes, per C1 repeat, the producer evidence under A5 and an A3 object produced by the real `b10-abrupt-verify` handler with repeat r (A7); per C0 repeat, one-producer `verify` output; and the sweep line. Records go to a local Kafka window. T0 pins B to E and E-T.

| Test | Inputs | Expected |
|---|---|---|
| T0 parity | E, E-T and B | Per kind, member-name sets equal |
| T1 clean C1 | B, five C1 repeats as in run 43322351104's shape: each arm one member, one original and one resend copy in the original window, no late record | each repeat `eligible`, `comparison` `both`, cross_restart `pair_count` 1, `lost_resends` 0; `restart_rule` `eligible_repeats` 5, `qualifying_pair_repeats` [1,2,3,4,5], `rule_selection` `confirmed`, `subject` the exact ADR-021 string |
| T2 late resend | T1, repeat 2's cross_restart resend copy written at offset `end_line` of its partition instead of inside the window; A3 object for repeat 2 shows `resend_copies` 0, problem 9 | repeat 2 `eligible`, `resends_seen` 1, `pair_count` 1, `lost_resends` 0 |
| T3 lost resend | T1, repeat 2's resend never written, terminal delivered | repeat 2 `lost_resends` 1, `lost_resend_finding_repeats` [2], `eligible_loss_repeats` [2]; with T1's other pairs `rule_selection` `confirmed` and the finding still listed |
| T4 boundary at sweep line | T2 with the copy at offset equal to `sweep_line`'s high for that partition (outside), and a second subcase at `sweep_line - 1` (inside) | outside: `resend_lost` true; inside: false |
| T5 earlier owner | T1 plus a repeat-2 timeout-instance record inside repeat 3's original window | repeat 2's timeout counts include it; repeat 3 unchanged |
| T6 foreign owner | T1 plus a record with `instance-id` `foreign` inside repeat 4's window | repeat 4 `invalid`, `timeout` null, `reasons` [`b10_final_owner_invalid`], `eligibility_reasons` [`b10_restart_late_record_unjoined`], sweep-derived arm counts null, `comparison` `unavailable` |
| T7 unjoined late | B, T1 plus a repeat-1 first-pod record in its late range with attempt `x`; second subcase a repeat-1 timeout-instance record with attempt `x` | first: repeat 1 `status` `complete_valid`, `timeout` as T1, `eligibility_reasons` [`b10_restart_late_record_unjoined`], sweep-derived arm counts null, `comparison` `unavailable`. second: `status` `invalid`, `timeout` null, `reasons` [`b10_final_late_record_unjoined`], restart as T1. Repeats 2 to 5 as T1 |
| T8 sweep missing | B without `sweep-line.json`; second subcase drain budget below the window | `scan_complete` false; every repeat `invalid`, `classification_final` false, `timeout` null, `reasons` [`b10_final_sweep_line_invalid`] (second subcase [`b10_final_drain_incomplete`]), `eligibility_reasons` [`b10_restart_sweep_unavailable`]; `rule_selection` `held`, `fewer_than_three_eligible`; root `status` `invalid` |
| T9 mismatch | T1 with repeat 3's A3 object rewritten (rehashed) to `timeout.duplicate` one higher; second subcase a member's `original_copies` one higher | first: repeat 3 `invalid`, `timeout` null, `reasons` [`b10_final_window_mismatch`], restart as T1. second: `status` `complete_valid`, `eligibility_reasons` [`b10_restart_late_record_unjoined`] |
| T10 no-kill | T1 with repeat 5 a no-kill end, `no_kill.reason` `kill_too_early`, no A3 object, no verifier Job applied | repeat 5 `status` `complete_valid`, `timeout` the timeout instance's counts over `[start_line, sweep_line)`, `reasons` [], `termination` `{class: no_kill, outcome: kill_too_early}`, `eligibility_reasons` [`kill_too_early`], `arms` null, `comparison` `unavailable`, in no lost list; `restart_rule` `eligible_repeats` 4 |
| T11 thresholds | T1 with 3 eligible (repeats 4, 5 ineligible by E13) and pairs only in ineligible repeats; then 2 eligible | 3 eligible, no pair, no eligible loss: `untestable`, `no_cross_restart_duplicate_observed`; 2 eligible: `held`, `fewer_than_three_eligible` |
| T12 dropped fields | T1 output bytes | none of 3.4's dropped member names occurs at any depth; closed members exactly as 3.4 |
| T13 eligible loss, no pair | 3 eligible, none paired, one eligible lost resend | `held`, `eligible_loss_without_pair` |
| T14 C0 rule | The judge's B10 final path given literal projection repeat entries (C0, five `complete_valid` final repeats with `timeout.duplicate` [0,0,1,0,0]; then all 0; then repeat 3 `invalid`) | `confirmed` with `duplicate_total` 1; `untestable` with 0; `held` with null |
| T14b C0 handler path | B with five one-producer C0 `verify` outputs from real handlers (A2), duplicates [0,0,1,0,0] | as T14's first subcase |
| T15 C0 two producers | B, scientific C0 repeat 2 with two registrations | repeat 2 `invalid`, `reasons` [`b10_final_repeat_input_invalid`]; `timeout_rule` `held` |
| T16 ownership global | B, one instance ID registered in repeats 1 and 2 | `scan_complete` false, `b10_final_ownership_invalid` |
| T17 judge path | T1 projection and receipt | candidate `b10_final.repeats` JSON-equal to the projection's; no `dialect`, `purpose`, `scientific_admissible` member; `check_candidate` in `phase.py` accepts the bytes (run by the phase test TP3) |
| T18 judge untrusted | T1 with the receipt's SHA-256 changed; second subcase projection identity phase changed | candidate `invalid`, `b10_final_projection_untrusted`, `b10_final` null |
| T19 legacy unchanged | existing legacy B10 judge fixtures | candidate bytes equal to `main`'s for the same inputs |
| T20 trial rejected | options with run ID `b10d-1-1` | exit 1, no read |
| T21 vectors | B with repeat 2's start line below repeat 1's end line | `scan_complete` false, `b10_final_vector_invalid` |
| T22 old readers reject (P2-A of 6846147228) | T1's `b10-final-sweep.json` bytes | `VerifierResultCodec.Read` returns `IsSuccess` false; `B10SweepEvidenceCodec.ReadFinalization` throws; `PhaseCandidateCodec.Read` throws `FormatException` (ADR-021 protocol rule 14) |
| T23 kill too late | T1 with repeat 4 an intended self-kill with an empty kill set (`successor_permitted` false, so case (ii)), no A3 object; its control resend delivered and never written | repeat 4 `termination` `{class: intended_self_kill, outcome: kill_too_late}`, `eligibility_reasons` [`kill_too_late`], `same_instance` `lost_resends` 1, `cross_restart` `unconfirmed_count` 0; `lost_resend_finding_repeats` [4], `eligible_loss_repeats` []; `status` `complete_valid` |
| T24 token provider | The host built for `b10-final-sweep` | resolves the sign-in fix's provider from `Common/Kafka/` with its section 3.2 values, as that fix's T1 asserts for its five modes |
| T25 re-authentication mismatch (P2-A of 6846466122, adapted to rule 12) | T1, repeat 2's A3 object rewritten (rehashed) so `first_instance_id` is `other-pod` | repeat 2 `status` `complete_valid`, `timeout` as T1, `eligibility_reasons` [`b10_restart_input_unavailable`], `arms` null, `comparison` `unavailable`; repeats 1 and 3 to 5 as T1 |
| T26 counts overflow (P2-B of 6846466122) | Literal C0 projection entries, five `complete_valid` final, `timeout.ok` 9223372036854775807 in repeats 1 and 2, 0 elsewhere | root `status` `incomplete`, no `counts`, `j7_counts_unavailable`; `duplicate_total` null; `rule_selection` `held` |
| T27 termination classes | T1 with repeat 3 `unexpected_exit` (outcome `b10_abrupt_unexpected_exit`), then `unknown`; then repeat 3 with no `termination.json`; then an intended self-kill with a nonempty kill set and a capture gap | E4; E4; E2; E17 with E4. `status` `complete_valid` in each |
| T28 repeats not started | B, phase stop after repeat 3: repeats 4 and 5 have no start-line receipt; second subcase repeat 2 ends at a metadata failure | repeats 4, 5 `incomplete`, `reasons` [`b10_final_repeat_not_started`], E17; second: repeat 2 `incomplete`, `reasons` [`b10_final_timeout_absent`]. Root `status` `incomplete` |
| T29 cross-repeat vectors | B with repeat 1's end line above repeat 2's start line on one partition | `scan_complete` false, `b10_final_vector_invalid` |
| T31 accepted, no object | T1 with repeat 2's A3 object and receipt deleted; its `termination.json` has `successor_permitted` true | repeat 2 case (iv): `status` `complete_valid`, `timeout` over `[start_line, next)` plus the late range, `eligibility_reasons` [`b10_restart_input_unavailable`], `arms` null |
| T32 late record of a no-object repeat | T1 with repeat 2 a no-kill end and one repeat-2 timeout-instance record written after repeat 3's start line | the record counts in repeat 2's `timeout` and not in repeat 3's; repeat 3 unchanged |
| T33 timeout failure blocks eligibility | T1 with repeat 3's timeout ledger hash changed | repeat 3 `invalid`, `reasons` [`b10_final_timeout_evidence_invalid`], `eligibility_reasons` include `b10_restart_timeout_bookkeeping_unavailable`, `eligibility` `ineligible` |
| T34 C0 zero registrations | B, scientific C0 repeat 2 with no registration | repeat 2 `invalid`, `reasons` [`b10_final_repeat_input_invalid`] |
| T30 versions | T1 with the five timeout manifests carrying equal versions; second subcase one differing `librdkafka` | candidate `versions` equal to them; second: `librdkafka` null and `version_unavailable` |

Phase tests (`test_phase.py`):
- TP1: the scientific B10 sequence after the repeats is `b10-sweep-line`, `b10-final-sweep`, then `judge`; `b10-sweep` never appears.
- TP2: C1 with repeat 3's successor Job not applied because its render failed after an accepted first publication, so its `b10-abrupt-verify` Job ran: 14 producer entries (5 times 3 less 1, computed by script) and 5 verifier entries accepted; one missing entry, or one `gaps` entry, gives `lifecycle_evidence_gap`. C0: 5 producer and 5 verifier entries accepted; four of either gives `lifecycle_evidence_gap`. C1 with every repeat a no-kill end: `b10_last_verifier_completed_utc` equals the latest producer `finished_utc`.
- TP3: `check_candidate` accepts T17's candidate bytes, and rejects the same bytes with `"dialect": "b10_abrupt"` added inside `b10_final`.

Chart test:
- TC1: a rendered `b10-final-sweep` for `b10-1-1`, C1 has the options of 3.3 and deadline 750; the same values with `b10d-1-1` fail the schema.

Each T row asserts every other check still passes in that subcase. The receipt records the predicate map below, at and above each boundary.

## 7. Deployment assumptions (live-only)

"Cold" means the cluster and ephemeral layer were created in that run. "Idle" means no producer was sending. No scientific three-Job run has happened, so no assumption below has live evidence of its own. None is accepted for any run (6846551008 point 7; 6846521676 point 5). The governed diagnostic run of the scientific path that 6846521676 point 5 requires, after both pull requests merge and with its own authorization, is where they are first exercised (6846551008 point 10).

| ID | Assumption | Evidence, and how it was taken | If false |
|---|---|---|---|
| LA-F1 | Every repeat's records stay readable in `document-events` until the sweep ends, up to the 18000 s orchestration deadline (`phase.py` line 28) | None for a five-repeat three-Job phase. The trial run 43322351104 read one repeat's complete window (6838450660 states `window_complete` true). That its verifier used a fresh connection on an idle namespace is inferred from the trial order (verify runs after the successor; `b10-abrupt-contract.md` "Trial order"), not stated | Drain incomplete, `scan_complete` false, every repeat `invalid` |
| LA-F2 | One `b10-final-sweep` Job drains five original windows and five late ranges within `verify_job_seconds` 750 and 9300000 records | Not measured. For the trial's one C1 window, 6838450660 states "start offsets 0 to end lines summing to 30945 on 32 partitions"; that this was 30945 records is inferred from those offsets. That reading was taken by the trial's verifier pod on a fresh Kafka connection opened at pod start, on an idle namespace after every producer had stopped; both are inferred from the trial order (`b10-abrupt-contract.md` "Trial order and Job identities": verify runs after the successor), not stated. `b10-final-sweep` would read the same way: a fresh connection, after every producer of the phase. Five repeats plus late ranges is not measured. Policy values only | Job fails; no projection; candidate `invalid` |
| LA-F3 | A record Event Hubs acknowledged before the sweep line capture lies below the captured high watermark | Same gap as the PR3c plan's LA-3b. One sample (6838450660): both arms' delivered resends were consumed inside the window read at verify start. That the successor used a fresh connection with no warm-up is stated by ADR-021 protocol rule 8 ("The successor sends no warm-up"); that the namespace was idle is inferred from the trial order. No source states the assumption (3.2) | An acknowledged copy is missed; a false `resend_lost` true. The residual-risk sentence covers appends after the line, not this |
| LA-F4 | The settle interval of 60 s after the last producer termination leaves no append of the phase's instances after the sweep line | Not an assumption the plan can test: ADR-021 accepts this residual risk for B10 and every publication states it | Disclosed, not detected |
| LA-F6 | In the deployed verifier pod, the token acquisition of `b10-final-sweep`, and of the `start-line`, `b10-sweep-line` and C0 `verify` Jobs the phase also needs, completes inside `TokenTimeout` 10 s with the sign-in fix's settings (3.3): a healthy token call completes within `NetworkTimeout` 2.0 s, and after one stall the single retry does | The sign-in fix's DA-1 and DA-2, accepted by name in 6845385609 points 2 and 3 for that fix, on Application Insights evidence over 30 days, all pods. Fresh connection at pod start; idle namespace for this mode is inferred, because it runs after every producer | Job fails before the drain; no projection; candidate `invalid` |
| LA-F5 | The verifier identity lists and reads the A5 prefixes for repeats 1 to 5 | `Storage Blob Data Reader` on the ledger container (`infra/ephemeral/identity.tf` line 13); exercised for `r1` only, in the trials (6838450660). That the pod was fresh and the namespace idle is inferred from the trial order, not stated | `b10_final_repeat_input_invalid`, E17 |

## 8. Acceptance commands

| # | Command | Evidence |
|---|---|---|
| A1 | `./scripts/verify-dotnet.sh` | `Verification passed.`; reports include T0 to T34, and the architecture tests pass unchanged |
| A2 | `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` and the phase suite `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'` | `OK`; includes TP1 to TP3 |
| A3 | `bash infra/scripts/lifecycle/check-offline.sh` | exit 0; includes TC1 |
| A4 | `npm run check:docs` | each warning fixed or explained (docs/agents/pull-requests.md rule 8) |
| A5 | `git diff --name-only origin/main...HEAD` | a subset of 2.1, checked by script against 2.2 |
| A6 | Against (a)'s merged `main`: a script that lists, for each A1 to A9 item, the file and line where (a) implements it | every item found; any difference stops implementation (section 12) |

## 9. Caps and plan-check answers

Caps per implementing session, under 6845612916 point 2: 20 verification-failure root causes and 20 review-finding root causes. Exhaustion of either: `push -wip and hand off to the next session` (AGENTS.md "Before opening a pull request" rule 11).

(a) Steps whose effort scales with a number unknown at planning time:
- Records drained: bounded by `MaxConsumedRecords` 9300000 and `MaxConsumedBytes` (3.3). Exhaustion is F8, a named global failure, not more work.
- Ledger rows and journal records per repeat: the existing per-file limits (`b10-abrupt-contract.md` check 5; `MaxRowsPerArtifact`).
- Reasons per repeat: at most 100 (3.4).
- Implementer root causes: the caps above.
- Fixture changes under the standing fixture rule: at most 40 existing test cases, each test method or `InlineData` row whose expected values change, exhaustion `push -wip and hand off to the next session` (6846551008 point 11).
- New C5 targets (2.1 item 6): one shard each, no cut, so no measurement is needed in this pull request. Cap: at most 8 new production files, so at most 8 new targets and shards; exhaustion `push -wip and hand off to the next session`. The number 8 is a policy choice, not a measurement. Whether a one-shard target fits the workflow's per-shard time limit is first known from the post-merge run; a shard that does not fit is a follow-up for that run's own fix issue (docs/agents/factory.md "Evidence pack", mutation paragraph), not work in this pull request.

(b) Where this plan is stricter than its parent (item plan Revision 5 and ADR-021):
- F12 re-classifies the original window and requires agreement with the per-repeat object, which ADR-021 does not require.
- F6 re-authenticates each repeat's producer evidence instead of trusting the per-repeat object.
- F5 makes a cross-repeat instance collision a global failure, not a per-repeat one.
- Eligibility also requires F11 and F12 to add nothing for the repeat.
- F15 re-applies checks 16 to 19 to the timeout instance even when an A3 object exists.

(c) Tests or checks that depend on code, data or behaviour that is neither on `main` nor in this pull request: everything in 3.1 (A1 to A9) depends on (a), checked here only against (a)'s unapproved Revision 7 text, not against code or recorded output; acceptance command A6 checks them after (a) merges. F3, T14b, T15 and TP2's C0 half depend on (a)'s one-producer C0 `verify` (A2). The token provider of 3.3 and T24 depend on the unmerged sign-in fix (pull request 447), whose contract is its plan's section 3.4 at `0b917178`. T2 needs the per-repeat handler to record problem 9, which needs a high watermark that moves between its two reads, as the PR3c plan's T24 already arranges. B builds A3 objects with the real `b10-abrupt-verify` handler for repeats other than 1, which depends on A7. Live behaviour is LA-F1 to LA-F5.

## 10. Sources

- ADR-021 "Retries", "Classification", "Late-write scan", decision rules 1 to 7, protocol rules 6, 7, 10, 13 and 14, and "Consequences" (`docs/blueprint.md` lines 659-733 on `main` at `675f8372`). ADR-012 classes and B10 exceptions (lines 367-430). ADR-013 high-watermark window (line 431 onward). ADR-019 publication of checked fields. ADR-020 retention.
- `docs/operations/b10-abrupt-contract.md` "Diagnostic verifier mode", checks 3, 16, 19, 20 and 29, "Output `nonfinal_result`", "Problems and unavailable fields", "Eligibility reasons" and "Failure names and collection".
- `docs/operations/b10-workflow.md` lines 10-19 and 41-51; `docs/operations/verifier-contract.md` B10 phase sweep, judge and candidate sections.
- `src/Parchwell.Verifier/VerifyRun/B10SweepHandler.cs` lines 47-131 and 157-239; `VerifyRun/Judgement/B10LateWriteChecks.cs` lines 16-76; `VerifyRun/Input/B10PhaseRegistrationReader.cs` line 42; `Judge/JudgeHandler.cs` lines 356-419; `Judge/PhaseJudge.cs` lines 65-104; `Judge/PhaseCandidateCodec.cs` lines 18-19; `Common/Phase/B10SweepEvidence.cs` line 8; `Common/Phase/PhaseIdentity.cs` lines 24-38; `Common/Phase/PhaseWireCodec.cs` line 157; `VerifyRun/Abrupt/B10AbruptVerifyHandler.cs` lines 47 and 241-272; `VerifierHostOptions.cs` lines 151-162.
- `infra/scripts/phase/phase.py` lines 26-29, 279-307, 466, 816-850, 905-971 and 1354-1388; `charts/parchwell/values.yaml` lines 49-54 and 95-100; `values.schema.json` lines 930-931; `.github/workflows/scenario.yml` lines 77, 199-232, 449-468 and 622-656; `.github/CODEOWNERS` lines 5 and 16; `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` lines 17-35.
- Confluent.Kafka v2.16.0 `WatermarkOffsets.cs` (3.2); Microsoft Learn Event Hubs Kafka troubleshooting guide (3.2).
- Reader reports from two subagents on `main` at `675f8372`; each claim used here was checked at its cited line in this session.

## 11. Open questions for Hari

None.

Decided, no longer open: Q1 to Q11 of Revision 2 ([6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008) points 1 to 11): Codex implements; item (a) moves C0 to one Job (via [6846521676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846521676) point 2); the new code joins the C5 list; projection `purpose` `scientific`; the literals of 3.4 and 3.5; the candidate carries `rule_selection`, `selection_reason` and `subject`; no live assumption accepted; approval only after item (a)'s approval and reconciliation; a create-only receipt for the per-repeat C1 object (item (a) 3.6); the governed diagnostic run covers this pull request; the 40-case cap. Revision 3's new literals `b10_final_timeout_evidence_invalid`, `b10_final_timeout_absent` and `b10_final_repeat_not_started` fix check B1 and B2 under the literal set Hari adopted; the plan check and Hari's approval of this revision cover them.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change:
- a contract of section 3 or a requirement of section 4;
- the scope of section 2, including any C5 target;
- or when A6 shows any of A1 to A9 differs from (a)'s merged implementation.

Predecessors: item (a) and the verifier sign-in fix (pull request 447) must both have merged before implementation starts; the implementer verifies both by script. Approval: Hari's `## Approval` naming this revision's commit, after item (a) is approved (6846551008 point 8).

Hitting a section 9 cap pushes `-wip` and hands off to the next session. Approval is Hari's `## Approval` naming this revision and commit; standing approval does not apply (6845612916 point 4). A revision gets one fresh `plan-checker` when it is design-changing under docs/agents/factory.md "Item lifecycle" step 1.

## 13. Plan branch hygiene and revision of (a) read

This file is committed alone, on top of the remote tip of `claude/404-plan`, with no rebase or force-push, and its SHA-256 on the remote is verified by script (6845612916 point 3). Revision of (a)'s plan read: for Revision 1 of this plan, (a) Revision 1 at `009613aba2aae789ca6c187140b56f18ea5ef134`; for Revision 2, (a) Revision 2 at `0fb9caa42abf82b205c7ddb18a2ce3e46d328abc`; for Revisions 3 and 4, (a) Revision 7 at `4196a0325fd91534239efbc70ef3dbafd6dfe10e`, file SHA-256 `11efb0f8ac45a92434e3ba5ad868829d0b0f0b76b347305546bb7e9e7ef57e44`, approved in 6848300952, each verified by script.
