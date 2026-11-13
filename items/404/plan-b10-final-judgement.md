# B10 final judgement contract-grade plan

Revision 2. Issue 404, pull request short name `B10 final judgement`. Status: not approved. Implementing tool: open (section 11, Q1). Hari merges.

This pull request turns the per-repeat B10 evidence into final classifications. A new phase sweep scans each repeat's window plus its late range up to the phase sweep line, classifies the C0 and C1 timeout cohorts and the C1 probe arms over that union, and writes one final sweep projection per phase without the nonfinal diagnostic extras. The phase judge then reads the five final repeats and records, for each phase, the inputs and the selection that ADR-021's decision rules give; Hari still approves each verdict in the verdict pull request.

It depends on the scientific three-Job pull request, item (a) of the same decision. Section 3.1 states what this plan reads from (a), reconciled with (a)'s Revision 2. Where (a) leaves a point open, 3.1 says what this plan assumes. Implementation starts only after (a) merges, and any difference is a stop-rule event (section 12).

## 0. Changes

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

Evidence read, not authority: the PR3c plan at `48c52669fd40a3bb80ca0378e061f1d6153d6a22` (sections 2, 3.3 check 29, 3.4, 9 and 11), the evidence readings [6837900667](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837900667) and [6838450660](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6838450660), and the B10 expected-outcomes issue #356 for context only. The expected outcomes are Hari's separate decision; this plan sets and changes none of them.

What the PR3c contract defers to this pull request, read on `main` at `675f8372`: `docs/operations/b10-abrupt-contract.md` "Diagnostic verifier mode" says eligibility is "pending the sweep"; "Output `nonfinal_result`" says "The later final sweep projection must drop them and test that exclusion"; check 29 says "the later late-write sweep from the end line covers the advanced range"; and the last paragraph says "scientific admission and the late-write sweep remain later work". ADR-021 "Consequences" says "the workflow needs a sweep step", and protocol rule 14 names the "final sweep projection" as a closed schema-3 kind.

Catch-up for this revision: 317 comments on 4 pages, counted by script at 2026-11-13T06:09Z; issue `updated_at` 2026-11-13T06:08:15Z. New since the state file's comment 6830544567, each read in full: 6837706862, 6837900667, 6837940546, 6837975192, 6838148214, 6838298262, 6838309006, 6838450660, 6844950677, 6844977688, 6845326893, 6845335358, 6845385609, 6845555307 and 6845612916. Every `## Decision` and `## Approval` heading on the issue was listed in creation order by script; the bodies whose subject is the sweep, the PR3c scope or this plan were read in full (6769057785, 6816117386, 6824437250, 6826128120 and the five above).

## 2. Scope

The scope is item (b) of 6845612916 point 1: the final sweep projection and the five-repeat judgement under ADR-021's decision rules, from `pending_sweep` evidence to the final restart and timeout classifications, dropping the nonfinal diagnostic extras.

Not in scope: the three C1 Jobs, the per-repeat C1 verifier and the C0 Job shape (item (a)); `scenarios/B10/expected.yaml` (#356); the verdict pull request and its evidence pack (docs/agents/factory.md "Item lifecycle" step 5); any workflow file; any trial or dispatch.

The plan freezes files and contracts, not lines (6845612916 point 2). Line numbers below are references, not boundaries.

### 2.1 Files allowed to change

1. New code, placed so that `tests/Parchwell.ArchitectureTests/CompiledArchitectureTests.cs` lines 52-79 pass unchanged (a slice is the first namespace segment after `Parchwell.Verifier`; only `Common` is shared): the sweep in `src/Parchwell.Verifier/VerifyRun/B10Final/`; the projection record and its codec in new files under `src/Parchwell.Verifier/Common/Phase/`; the judge's B10 final path in new files under `src/Parchwell.Verifier/Judge/`. The judge reads the projection only through the `Common/Phase/` codec and never uses a `VerifyRun` type. File split and private names are the implementer's choice.
2. `src/Parchwell.Verifier/Program.cs`, `VerifierHostOptions.cs` and `VerifierHostOptionsValidator.cs`: mode `b10-final-sweep` (3.3), its registration of the sign-in fix's token provider (3.3), and the B10 branch of `judge` (3.5).
3. `src/Parchwell.Verifier/Judge/JudgeHandler.cs`, `PhaseCandidate.cs` and `PhaseCandidateCodec.cs`: the B10 route to the new `Judge/` files and the `b10_final` candidate member (3.5). `PhaseJudge.cs` and every other C5 target do not change (2.2).
4. `src/Parchwell.Verifier/Common/Phase/PhaseWireCodec.cs` and `PhasePaths.cs`: the `b10-final-sweep` receipt mode and the two object paths of 3.3.
5. `tests/Parchwell.Verifier.Tests/`: new tests prefixed `B10Final`, and existing tests only as 2.3 states.
6. Only if Hari answers Q3 with option A: `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`, `c5_workflow.py` (`TARGETS`, `SHARDS`, `BASE_CONFIG`), `test_c5_workflow.py` (its matrix-row count, today 61 at line 376) and `.github/workflows/verifier-c5-mutation.yml` (matrix rows only; triggers unchanged, AGENTS.md hard safety rule 8). `test_c5_workflow.py` lines 369-375 require the four to agree. That workflow has no committed copy under `docs/operations/workflows/`.
7. `infra/scripts/phase/phase.py` and `infra/scripts/phase/tests/test_phase.py`: `B10_PHASE_MODES`, the post-repeat lifecycle count and the sweep step (3.6). `infra/scripts/phase/b10_abrupt.py` and `tests/test_b10_abrupt.py` only where (a)'s merged code puts the scientific C1 lifecycle records or the C1 judge gap of A6 and A8; trial behaviour stays byte-identical.
8. `charts/parchwell/values.yaml`, `values.schema.json`, `templates/jobs.yaml`, `templates/_helpers.tpl`, `tests/test_chart.py` and `tests/test_b10_abrupt_chart.py`: rendering of `b10-final-sweep` (3.6).
9. `docs/operations/b10-abrupt-contract.md` (owner of the final projection format, new section "Final sweep projection"), `docs/operations/verifier-contract.md` (B10 phase sweep and candidate sections, citing it) and `docs/operations/b10-workflow.md` (post-repeat steps).
10. `items/404/state.md` and a new receipt `items/404/b10-final-judgement-receipt.md`.

### 2.2 Paths that must not change

- Every file under `.github/` and `docs/operations/workflows/`, except `verifier-c5-mutation.yml` under Q3 option A (2.1 item 6). The `judge` and `candidate` workflow steps already call `phase.sh judge` and `phase.sh candidate` (`.github/workflows/scenario.yml` lines 203-212 and 627-636).
- `infra/ephemeral/**`, `infra/persistent/**`, `infra/cluster/**`.
- `src/Parchwell.LoadGen/**`, `tests/Parchwell.LoadGen.Tests/**`, `src/Parchwell.Kafka/**`.
- Every file in the C5 `mutate` list (`stryker-c5-config.json` lines 17-35), including `PhaseJudge.cs`, `B10JudgeChecks.cs`, `B10SweepChecks.cs`, `B10SweepHandler.cs`, `B10LateWriteChecks.cs`, `B10SweepEvidenceCodec.cs`, `B10PhaseRegistrationReader.cs`, `B10RepeatChecks.cs` and `B10IntakeChecks.cs`. The legacy `b10-sweep` mode stays in the code, unchanged, and is no longer scheduled for scientific B10 (3.6).
- `src/Parchwell.Verifier/VerifyRun/Abrupt/**` and `StartLine/**`: reused unchanged. `OutcomeClassifier.cs`, `WindowConsumer.cs`, `MessageIdentityReader.cs` and `RunWindow.cs`: reused unchanged.
- The verifier sign-in fix's changes: the `b10-abrupt-verify` branch of `Program.cs` and its new token-provider file under `VerifyRun/Abrupt/` (that plan's section 2.2 at `14d35b99`). This plan changes `Program.cs` only outside that branch. `src/Parchwell.Verifier/Common/Kafka/**` and `src/Parchwell.Kafka/**` do not change.
- The diagnostic trial's behaviour in `infra/scripts/phase/b10_abrupt.py` (2.1 item 7).
- `tests/Parchwell.ArchitectureTests/**`.
- `docs/blueprint.md`, `docs/operations/a01-runtime-limits.json`, `scenarios/**`, `results/**`. `scenarios/B10/expected.yaml` must not be created.
- `AGENTS.md`, `CLAUDE.md`, `docs/agents/**`, `README.md`, any `*.csproj`, `Directory.*.props` and `*.sln*`. No new project, assembly reference or NuGet package.
- Every existing test assertion, except under the standing fixture rule of 2.3.

### 2.3 Protected paths, CI and existing tests

- Protected: `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**` match `Parchwell.Verifier*/` in `.github/CODEOWNERS` line 16, and `/docs/` (line 5) covers the three docs files, so Hari merges.
- Required checks: `.NET verification` and `Lifecycle verification`. `docs-advisory` is advisory.
- Mutation: under Q3 option A, the new files of 2.1 item 1 join the C5 list, and docs/agents/factory.md "Evidence pack" then requires a passing `verifier-c5-mutation.yml` run on `main` after merge before any evidence pack is published. That run is Hari's dispatch, not this pull request's.

Standing fixture rule (`plan-pr` "Plan rules" rule 2, modelled on [6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2). When an existing test's fixture is rejected by a check this plan adds or tightens, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each change with the check that forces it. The cold review and Codex review check each listed change.

Inventory of existing tests whose fixtures the changes would reject, read on `main` at `675f8372` (section 3.6 changes the post-repeat sequence):
- `infra/scripts/phase/tests/test_phase.py` lines 26, 273-274, 383-394, 469-481 and 522-527 name `b10-sweep` in the expected B10 sequence, deadlines or mode loops. They follow rule (a): `b10-sweep` becomes `b10-final-sweep` where the scientific sequence is asserted.
- `test_phase.py` lines 309, 367, 436 and 466 assert `lifecycle_evidence_gap` at record index 20, which is 4 `B10_MODES` times 5 repeats (`phase.py` lines 26 and 944-972). Their record indexes and counts follow (a)'s merged sequence; this plan's check (3.6 P2) keeps the literal `lifecycle_evidence_gap`.
- `charts/parchwell/tests/test_chart.py` lines 42-43, 62, 92-135, 155, 235-251 and 294-303, and `test_b10_abrupt_chart.py` line 125, enumerate verifier modes. They gain `b10-final-sweep` under rule (a); every assertion on `b10-sweep` stays, because the chart still renders it.
- Verifier tests: no existing test fixture changes, because no existing reader changes. `JudgeHandlerTests`, `PhaseJudgeTests`, `B10SweepHandlerTests`, `B10SweepChecksTests` and `PhaseCandidateCodecTests` keep their B10 cases on the legacy path. A legacy B10 candidate without `b10_final` stays valid (R16).

Item (a) changes several of the same test files first. Where (a)'s merged tests already differ from this inventory, the implementer applies the rule to (a)'s merged text and the receipt records it.

## 3. External contracts

### 3.1 Contract with item (a), the scientific three-Job pull request

Read: (a)'s plan `items/404/plan-b10-scientific-three-job.md`, Revision 2, at `0fb9caa` (full SHA in section 13), file SHA-256 `d571ff460e39a93bc4f2bd63ed57f1989ddc5cee4df10b8563395f0614f359b9` (pointer comment 6845836752, edited 2026-11-13T06:42:25Z), sections 0, 2, 3.1 to 3.6 and 11. Its Revision 2 changes to 3.2, 3.4 and 3.6 (successor and verify only after an accepted publication, between-repeat ends, purpose by run-ID pattern, check 30 accepting an empty earlier listing) are consistent with A1 to A8. (a) is not approved. Each item below cites the (a) section it rests on, or says it is assumed. Each plan check checks it against (a)'s current revision (6845612916 point 2). A difference found before approval is an open question; one found during implementation is a stop-rule event.

`U` is the scientific run ID, pattern `^b10-[0-9]+-[0-9]+$` ((a) section 3); `r` the repeat 1 to 5.

- A1. **C1 producer Jobs** ((a) 3.1, 3.2). Each C1 repeat runs `U-c1-r{r}-loadgen`, `U-c1-r{r}-loadgen-restart` and, only when the accepted first publication has `successor_permitted` true, `U-c1-r{r}-loadgen-successor`, in that order, at most one producer pod at a time. Every C1 start line carries exactly those three names, the successor's included.
- A2. **C0** ((a) 2, 3.1, Q2). (a) Revision 2 assumes its Q2 option B: C0 unchanged, two producer Jobs `U-c0-r{r}-loadgen` and `U-c0-r{r}-loadgen2` and the legacy `verify`. ADR-021 protocol rule 1 requires one ordinary C0 producer Job, and decision rule 1 reads C0. This plan's C0 path therefore depends on how Hari answers (a)'s Q2 and this plan's Q2: under one C0 Job, F3 and F5 apply as written; under two C0 Jobs, no ADR-021-compliant C0 evidence exists, every C0 repeat is `invalid` with `b10_final_repeat_input_invalid`, and `timeout_rule` is `held` (R11a).
- A3. **Per-repeat C1 object** ((a) 3.6). After each C1 repeat whose first publication was accepted, `U-c1-r{r}-b10-abrupt-verify` writes `U/C1/r{r}/b10-abrupt/nonfinal-result.json`, create-only, with envelope `schema_version` 3, `dialect` `b10_abrupt`, `purpose` `scientific`, `synthetic` true, `scientific_admissible` false, `kind` `nonfinal_result`, identity `repeat` r, and `data` exactly as `b10-abrupt-contract.md` "Output `nonfinal_result`" defines it plus `earlier_repeat_records` (integer, or null). Problems 1 to 9 keep their order and problem 10 `b10_abrupt_verify_earlier_repeat_unavailable` follows; reasons E1 to E13 are unchanged; `eligibility` is `pending_sweep` or `ineligible`; `finality` is `nonfinal`. The mode writes no `WorkerReceipt` ((a) 3.6; `b10-abrupt-contract.md` "Mode, options and publication"), so this plan binds the object by the length and SHA-256 it reads, not by a receipt (Q9).
- A4. **Repeats with no object.** A no-kill end, an empty kill set, unknown first termination and a rejected first publication start no per-repeat verifier ((a) 3.2 applies the trial's rule, `b10-abrupt-contract.md` "Failure names and collection"). Assumed: the first pod's `termination.json` is still published under A5 whenever the parent published, so this plan reads the `no_kill.reason` literal, or `kill_too_late` for an intended self-kill with an empty kill set, from that authenticated receipt (R9). With no termination receipt, the repeat gets E17.
- A5. **Producer evidence prefixes** ((a) 3.4, 3.6). First pod and successor: `U/C1/r{r}/b10-abrupt/<pod>/`. Timeout instance: `U/C1/r{r}/producer/<pod>/`. Probe keys `B10/U/C1/{r}/same_instance` and `.../cross_restart`. Restart Job names `U-c1-r{r}-loadgen-restart` and `-loadgen-successor`.
- A6. **Lifecycle records** ((a) 3.2). `phase-status.json` `b10_lifecycle` holds `producers` and `verifiers`, each entry `{job, repeat, mode, pod_name, pod_uid, finished_utc, exit_code}`, and `gaps` entries `{object, reason}` with reason `producer_termination_unavailable` or `verifier_termination_unavailable`. A skipped Job adds nothing.
- A7. **Readers reused** ((a) 2.2 item 4, 3.6). `VerifyRun/Abrupt/` accepts repeats 1 to 5 for pattern `U` and applies checks 1 to 30 with repeat r. Check 30 holds earlier-repeat records out of the per-repeat object's counts.
- A8. **C1 judge gap** ((a) 3.2, 3.7). After (a), `judge` and `candidate` for B10 C1 record `b10_c1_final_judgement_unavailable`, write no candidate and exit 1; C1 runs no `b10-sweep-line`. This plan replaces that gap (3.6 P4).

### 3.2 Behaviour relied on (official sources)

- High watermark. Confluent.Kafka v2.16.0 `WatermarkOffsets.High` is "the offset of the latest message in the topic/partition available for consumption + 1" ([WatermarkOffsets.cs v2.16.0](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/WatermarkOffsets.cs), lines 60-64, fetched 2026-11-13; blueprint source S19). Context7 (`/confluentinc/confluent-kafka-dotnet`) returned the API reference for `QueryWatermarkOffsets` ("always queries the cluster") but not the "+ 1" wording, so the pinned source above is the citation. The sweep's half-open ranges `[end_line, sweep_line)` rest on this.
- That a record Event Hubs acknowledged before the sweep line was captured lies below that sweep line is not stated by any source read. It is LA-F3 (section 7), the same gap as the PR3c plan's LA-3b. Microsoft Learn's [Kafka troubleshooting guide](https://learn.microsoft.com/azure/event-hubs/apache-kafka-troubleshooting-guide), "Limits", says the Kafka interface has "for the most part" Kafka's general behaviour; searched with the Microsoft Learn tool on 2026-11-13.
- No other Kafka, librdkafka, Confluent.Kafka or Event Hubs behaviour is new in this plan. The drain, record identity and watermark reads reuse `WindowConsumer`, `MessageIdentityReader` and `VerifierKafkaSession` unchanged.

### 3.3 Mode `b10-final-sweep`

- `Verifier:Mode` `b10-final-sweep`, one Job per scientific B10 phase, `{U}-{p}-b10-final-sweep`, after `b10-sweep-line` and before `judge`. Required options are those `b10-sweep` requires today (`VerifierHostOptionsValidator.cs` lines 35-37, 55-73, 105-117 and 356-378), with `B10SettleIntervalMilliseconds` 60000. Scenario `B10`, phase `C0` or `C1`, run ID matching `^b10-[0-9]+-[0-9]+$`. Any other value exits 1 before any read.
- Outputs, both create-only in container `verifier`: the projection `U/P/b10-final-sweep.json` (3.4) and the receipt `U/P/b10-final-sweep-completion.json`, a `WorkerReceipt` schema 1 with mode `b10-final-sweep`, repeat and identity null, cleanup true, and one artifact binding the projection by length and SHA-256.
- Exit 0 when the projection was created and read back byte-identical, whatever it records. Any publication failure exits nonzero. This differs from `b10-sweep`, whose exit 1 means "not every repeat is final" (`B10SweepHandler.cs` line 131): the judge reads finality from the projection, not from the exit code.
- Kafka token: the mode registers the abrupt-mode Kafka token provider and credential options that the verifier sign-in fix adds under `VerifyRun/Abrupt/` (that plan's R1 to R3 at `14d35b99`), the same way the fix registers it for `b10-abrupt-verify`, so a stalled token request is abandoned after `NetworkTimeout` and retried once inside `TokenTimeout` 00:00:10. The sign-in fix must have merged first (section 12). `VerifierKafkaSession` and `TokenTimeout` are unchanged. LA-F6 is the residual live assumption.
- Budgets: the existing `b10_sweep` chart values, `MaxConsumedRecords` 9300000 and `MaxConsumedBytes` 429496729600 (`charts/parchwell/values.yaml` lines 51-54), and the `verify_job_seconds` 750 deadline (`values.yaml` lines 95-100). These are existing policy values, not measurements (LA-F2).

Enforced checks, numbered F1 to F14. A bracketed literal is a per-repeat reason of 3.4; "global" means `scan_complete` false and every repeat nonfinal.

- F1. Options as above. Failure exits 1 without an object.
- F2. Sweep line. Read `U/P/sweep-line.json` with the existing `B10SweepEvidenceCodec.ValidateLine`, unchanged. Its coordinates equal the options, its settle interval is 60000 and its `captured_at` is not later than this worker's start. For C0, every repeat's `verify-completion.json` completed no later than its `last_verifier_completed_utc`. For C1 no verifier receipt exists (A3), so that comparison is not made; the phase's settle rule (3.6 P2) is the only ordering evidence (Q9). Failure: global, reason `b10_final_sweep_line_invalid`.
- F3. Repeat inputs, C0. For each repeat, `U/C0/r{r}/result.json` and `verify-completion.json` pass the checks `b10-sweep` applies today (`B10SweepHandler.cs` lines 157-239), except that exactly one producer registration `U-c0-r{r}-loadgen` is required instead of two. Any other count, including the two of A2's two-Job case, fails. Failure: the repeat is `invalid` with [`b10_final_repeat_input_invalid`]. The one-producer `verify` output this needs does not exist on `main` (`B10IntakeChecks.cs` line 16, `B10PhaseRegistrationReader.cs` line 42 require two); Q2 decides which pull request supplies it.
- F4. Repeat inputs, C1. For each repeat, either the A3 object exists, or A4 holds. The object is read once, its length and SHA-256 recorded in `source`, and it passes `AbruptEvidenceCodec`'s schema-3 envelope rules with kind `nonfinal_result` and identity `repeat` r. Failure: [`b10_final_repeat_input_invalid`].
- F5. Registrations. C0: one registration per repeat bound to `U-c0-r{r}-loadgen`; a different count is F3's per-repeat failure. C1: the timeout registration under `producer/`, the first-pod and, when present, successor registrations under `b10-abrupt/` (A5), each bound to its A1 Job name; a mismatch is F4's per-repeat failure. Only one condition is global: an instance ID registered in two repeats of the phase, reason `b10_final_ownership_invalid`, because a misassigned instance corrupts every later repeat's exclusion (ADR-021 "Consequences", second bullet).
- F6. Re-authentication (C1). For each C1 repeat with an A3 object, the sweep reads repeat r's first-pod, successor and timeout evidence through the readers of `VerifyRun/Abrupt/` (A7) and requires the identities, unconfirmed sets, invocations and timeout instance it finds to equal the A3 object's `first_instance_id`, `successor_instance_id`, `timeout_instance_id`, arm `unconfirmed_count` and member attempt IDs. Failure: [`b10_final_repeat_input_invalid`].
- F7. Windows. Each repeat's original window is `[start_line, end_line)` from its per-repeat input (C0: `result.json`; C1: the A3 object). Its late range is `[end_line, sweep_line)`. Each vector must be at least the one before it, per partition, as `B10LateWriteChecks.BuildWindows` requires today. Failure: global, reason `b10_final_vector_invalid`. A C1 repeat whose A3 object has null `start_line` or `end_line` has no window and is ineligible with its existing reasons.
- F8. Drain. One Kafka session drains every original window and late range within `SafetyStop` and the record budget, as `b10-sweep` does. Any incomplete drain: global, reason `b10_final_drain_incomplete`.
- F9. Ownership. A record in repeat r's original window whose `instance-id` is neither one of repeat r's instances nor an earlier repeat's makes repeat r `invalid` with [`b10_final_owner_invalid`]. A record carrying an earlier repeat's instance is excluded from repeat r and counted only in its own repeat (ADR-021 "Late-write scan"). In repeat r's late range, only records carrying one of repeat r's instances are repeat r's; others are skipped, as `B10LateWriteChecks.Classify` skips them today.
- F10. Union. Repeat r's classified records are the union of its owned original-window and owned late records, each `(topic, partition, offset)` counted once (ADR-021 decision rule 7).
- F11. Join. Every record in the union joins one authorized attempt: a timeout ledger row (instance, attempt, key and sequence equal, key not an arm key); a first-pod `prepared` attempt or warm-up; or a successor invocation, under the join rules of `b10-abrupt-contract.md` checks 19 and 20. C0 records join the C0 ledger. A record that joins nothing: [`b10_final_late_record_unjoined`], with the statuses and nulls of 3.4 (ADR-021 "Late-write scan": "Unknown/conflicting identities invalidate").
- F12. Agreement. The original-window part of the union, classified alone, must reproduce the per-repeat input's values: C0 `result.json` counts; C1 `timeout` six counts and every member's `original_copies`, `resend_copies` and `other_copies`. A difference: [`b10_final_window_mismatch`], with the statuses and nulls of 3.4.
- F13. Classification. Over the union: the timeout cohort's six counts with `OutcomeClassifier.Classify` over the timeout ledger rows; per member, `original_copies`, `resend_copies`, `other_copies` and `pair_count` (`original_copies` times `resend_copies`); per arm, the counts of 3.4 and the witness of ADR-021 protocol rule 13; warm-up records join and enter no count. A late record from the same attempt adds to its member's copies; it never creates an outcome class for an unreported attempt (ADR-012).
- F14. Resend loss. A member's `resend_lost` is true when its resend terminal has `delivered` true and the union holds no resend copy; false when the terminal is known and either condition fails; null when either is unknown. The A3 object's problem 9 (end line advanced) no longer nulls it, because the late range covers every offset at or after the end line up to the sweep line.

### 3.4 Output `final_sweep_projection` (`U/P/b10-final-sweep.json`)

The envelope has exactly `schema_version` 3, `dialect` `"b10_abrupt"`, `purpose` `"scientific"`, `synthetic` true, `scientific_admissible` (true when `data.scan_complete` is true, otherwise false), `kind` `"final_sweep_projection"`, `identity` and `data`. The `purpose` literal is new (Q4). Identity has exactly `run_id`, `scenario_id` (`B10`), `configuration_id`, `phase`, `source_commit`, `image_reference` (the verifier image), `job_name`, `pod_name` and `pod_uid`.

`data` has exactly:
- `sweep_line`: `{container, name, length, sha256}` of the note, or null when F2 fails.
- `scan_complete`: boolean.
- `residual_risk`: exactly `A late append after the phase sweep line is not detected.` (the constant at `B10SweepEvidence.cs` line 8).
- `repeats`: exactly five objects, repeat 1 to 5.

Each repeat object has exactly:
- `repeat`; `source`: the reference of its per-repeat input object (C0 `result.json`, C1 the A3 object), or null when absent.
- `classification_final`: true only when `scan_complete` is true and no reason of this list applies to the repeat's timeout cohort.
- `status`: the timeout cohort's status, `complete_valid`, `invalid` or `incomplete`. C0: `complete_valid` iff the per-repeat result is `complete_valid` with B10 window checks complete and `classification_final` is true. C1: the same rule over the A3 object's `timeout` and checks 16 to 19.
- `timeout`: `{ok, loss, phantom, reported_failure, duplicate, reorder}`, present only when `status` is `complete_valid`, otherwise null.
- `reasons`: sorted, distinct literals of the list at the end of this section, each at most 256 ASCII bytes, at most 100.
- `restart`: null for C0. For C1, exactly:
  - `eligibility`: `eligible` or `ineligible`.
  - `eligibility_reasons`: distinct literals, ordered as the 13 PR3c literals (`b10-abrupt-contract.md` "Eligibility reasons") followed by E14 to E17 below.
  - `termination`: `{class, outcome}`. `class` is `intended_self_kill`, `no_kill` or `unknown`. `outcome` is the `no_kill.reason` literal when `class` is `no_kill`; `kill_too_late` when `class` is `intended_self_kill` with an empty kill set, which ADR-021 protocol rule 7 says is not a no-kill end; otherwise null.
  - `first_instance_id`, `successor_instance_id`, `timeout_instance_id`: strings or null.
  - `comparison`: `both`, `cross_restart_only`, `same_instance_only`, `neither` or `unavailable`, from the final pair counts under ADR-021 decision rule 5 and the PR3c rule (`unavailable` under E1 to E4, E8 to E12, or E14 to E17).
  - `native_errors`: `{count, fatal_count, tail}` or null.
  - `arms`: null, or exactly `same_instance` then `cross_restart`, each with exactly `arm`, `cut_attempt_id`, `unconfirmed_attempt_ids`, `unconfirmed_count`, `resend_invoked`, `originals_seen`, `resends_seen`, `pair_count`, `unreported_attempts`, `unreported_seen`, `unreported_unseen`, `lost_resends` and `witness`. Counts are JSON integers from 0 to 9223372036854775807, or null when unknown, never zero for unknown.

Dropped, and asserted absent by T12: every member's `members` table and its `copies`, `copies_truncated`, `copy_count`, `original_copies`, `resend_copies`, `other_copies` and `resend_terminal`; `problems`; `problem_details`; `window_limit`; `start_line`, `end_line` and `window_complete`; `earlier_repeat_records`; `finality`. This is the removal 6816117386 point 2 requires. Per-member values stay internal to the sweep, which uses them to compute the arm counts.

Eligibility. A C1 repeat is `eligible` exactly when all hold: its A3 object exists with `eligibility` `pending_sweep` and `eligibility_reasons` []; its `problems` list is empty or exactly `[b10_abrupt_verify_end_line_advanced]` (problem 10 adds E3 under A3, so it is never alone); `scan_complete` is true; F6, F11 and F12 add nothing; and the final `comparison` is not `unavailable`. Otherwise it is `ineligible` with the A3 object's reasons plus any of:

| # | Literal (proposed, Q5) | Condition |
|---|---|---|
| E14 | `b10_restart_sweep_unavailable` | `scan_complete` false |
| E15 | `b10_restart_late_record_unjoined` | F11 or F12 for this repeat |
| E16 | `termination.outcome` itself, for example `kill_too_early` | no-kill end (ADR-021 protocol rule 7: "its reason is the outcome the `no_kill` record names") or an intended self-kill with an empty kill set (`kill_too_late`, protocol rule 6) |
| E17 | `b10_restart_input_unavailable` | F4 or F6 fails, or termination unknown |

Repeat-level reason literals (proposed, Q5), the only literals in a repeat's `reasons`: `b10_final_sweep_line_invalid`, `b10_final_repeat_input_invalid`, `b10_final_ownership_invalid`, `b10_final_vector_invalid`, `b10_final_drain_incomplete`, `b10_final_owner_invalid`, `b10_final_late_record_unjoined`, `b10_final_window_mismatch`, `b10_final_window_unavailable`. E-literals appear only in `restart.eligibility_reasons`.

Status and nulls. "Sweep-derived arm counts" means `originals_seen`, `resends_seen`, `pair_count`, `unreported_seen`, `unreported_unseen`, `lost_resends` and `witness`. "Journal-derived arm fields" means `cut_attempt_id`, `unconfirmed_attempt_ids`, `unconfirmed_count`, `resend_invoked` and `unreported_attempts`.

| Case | `status` | `classification_final` | `timeout` | `reasons` adds | C1 `restart` |
|---|---|---|---|---|---|
| Global failure (F2, F5 collision, F7, F8) | `invalid` for every repeat | false | null | the global reason | `ineligible`, E14; sweep-derived arm counts null; `comparison` `unavailable` |
| F3, F4 or F6 fails | `invalid` | false | null | `b10_final_repeat_input_invalid` | `ineligible`, E17; `arms` null; `comparison` `unavailable` |
| F9 foreign owner | `invalid` | false | null | `b10_final_owner_invalid` | `ineligible`, E15; sweep-derived arm counts null; `comparison` `unavailable` |
| F11 unjoined record | `invalid` | false | null | `b10_final_late_record_unjoined` | `ineligible`, E15; sweep-derived arm counts null; `comparison` `unavailable` |
| F12 mismatch | `invalid` | false | null | `b10_final_window_mismatch` | `ineligible`, E15; sweep-derived arm counts null; `comparison` `unavailable` |
| C1, no A3 object (A4) | `incomplete` | false | null | `b10_final_window_unavailable` | `ineligible`, E16 (or E17 with no termination receipt); `arms` null; `comparison` `unavailable` |
| C1, A3 object with problem 7 or 8 | `invalid` | false | null | none | A3 reasons kept; arms as computed |
| C1, A3 object with problem 1 or 5, without 7 or 8 | `incomplete` | false | null | none | A3 reasons kept; arms as computed |
| None of the above | `complete_valid` | true | six counts | none | per the eligibility rule above |

Where rows overlap, the earliest row applies. Journal-derived arm fields stay as the A3 object gives them wherever `arms` is not null. A repeat with E16 produces no lost-resend finding (ADR-021 protocol rule 7).

### 3.5 Judge for scientific B10

- Mode name `judge`, Job, options, `CandidateOutputName` `U/P/candidate.json`, receipt and exit rule unchanged (`JudgeHandler.cs` lines 404-419). For scenario B10 with a run ID matching `^b10-[0-9]+-[0-9]+$`, the judge reads `U/P/b10-final-sweep.json` and its receipt instead of `b10-finalization.json`, through the new `Judge/` files and the `Common/Phase/` projection codec of 2.1 item 1. `PhaseJudge.cs` is not called for that path.
- Judge checks for that path, numbered G1 to G5: G1 the receipt has mode `b10-final-sweep`, the derived Job, exit 0 and one artifact equal to the projection's actual length and SHA-256; G2 the projection passes 3.4's closed format and its identity equals the options; G3 `sweep_line` equals the actual note reference; G4 each repeat's `source` equals the actual per-repeat input reference; G5 each repeat's registrations match F5. Any failure makes the candidate `invalid` with root reason `b10_final_projection_untrusted` and `b10_final` null.
- Candidate (schema 1, `PhaseCandidateCodec`) on this path, member by member:
  - Root `status`: `invalid` when any G check fails, `scan_complete` is false or any repeat is `invalid`; otherwise `complete_valid` when all five repeats are `complete_valid`; otherwise `incomplete`. The judge exit code stays 0 only for `complete_valid` (`JudgeHandler.cs` line 414).
  - Root `gate`: null.
  - Root `versions`: as `CommonVersion` computes them today (`JudgeHandler.cs` lines 389-395), with the expected observation count `5*2` replaced by the number of producer registrations F5 accepted across the five repeats. A disagreement or an empty value gives null and root reason `version_unavailable`, as today; it does not make the candidate `invalid`.
  - Root `counts`: the checked sum of the five repeats' `timeout`, present only when all five are `complete_valid`; otherwise absent with root reason `j7_counts_unavailable` (as `PhaseJudge.cs` lines 93-104 do).
  - Root `reasons`: the sorted, distinct union of every repeat's `reasons`, `b10_final_projection_untrusted` when a G check fails, and `j7_counts_unavailable`, `version_unavailable` and `timing_unavailable` as today, at most 100.
  - Root `image_digests`, `timings`, `diagnostics`: as today.
  - Repeat `status`, `reasons`, `counts` (from `timeout`) and `classification_final`: copied from the projection.
  - Repeat `checks`: `j1_repeat_slots` true when the repeat's slot has its per-repeat input or A4 record; `j2_publication_unaccepted` and `j3_verifier_receipt` true when F3 (C0) or F4 (C1) passed; `j4_producer_job_binding` and `j5_registered_producer_count` true when F5 passed for the repeat; `j6_broker_inventory_coverage` true when the per-repeat input's start-line check passed (C0 `result.json`; C1 A3 problem 1 absent); `j8_probe_inconclusive` false. Values keep the existing JSON types (`PhaseCandidateCodec.cs` line 112).
  - Repeat `identity`: the registration identity of the repeat's `U-{p}-r{r}-loadgen` instance, as the judge sets it for B10 today, or null when F5 failed.
  - Repeat `probe_observation` and `workload_observation`: null.
  - Root `b10_sweep`: null on this path. `PhaseCandidateCodec` accepts a B10 candidate with `b10_sweep` null only when `b10_final` is present, and then applies to `b10_final.scan_complete` the rule it applies to `b10_sweep.scan_complete` today (lines 18-19, 80-86, 136-143). A legacy B10 candidate keeps today's rule.
  - Root `b10_final`, required on this path:
  - `projection` and `receipt`: references.
  - `scan_complete`, `residual_risk`: copied.
  - `repeats`: JSON-equal to the projection's `data.repeats`.
  - `timeout_rule` (C0; null for C1): `{complete_valid_final_repeats, duplicate_total, rule_selection}`. `duplicate_total` is the checked sum of `timeout.duplicate` over the five repeats, or null unless all five are `complete_valid` and final. `rule_selection` is `confirmed` when all five are and `duplicate_total` is at least 1, `untestable` when all five are and it is 0, and `held` otherwise (ADR-021 decision rule 1).
  - `restart_rule` (C1; null for C0): `{declared_repeats, eligible_repeats, eligible_repeat_numbers, qualifying_pair_repeats, lost_resend_finding_repeats, eligible_loss_repeats, rule_selection, selection_reason, subject}`. `declared_repeats` is 5. `qualifying_pair_repeats` are eligible repeats whose `cross_restart` `pair_count` is at least 1 and `unreported_seen` at least 1 (decision rule 3). `lost_resend_finding_repeats` are repeats, eligible or not, with an arm `lost_resends` of at least 1, excluding E16 repeats. `eligible_loss_repeats` are the eligible ones among them. Selection, in this order: fewer than 3 eligible gives `held` with `selection_reason` `fewer_than_three_eligible`; otherwise a qualifying pair gives `confirmed` with `selection_reason` null and `subject` exactly the full restart subject of ADR-021 "Restart decision rules"; otherwise eligible loss gives `held` with `eligible_loss_without_pair`; otherwise `untestable` with `no_cross_restart_duplicate_observed`. `subject` is null unless `confirmed`.
- The member names say `rule_selection`, not `verdict`: this is the selection ADR-021's rules give, an input to the verdict proposal. Whether the candidate carries it at all is Q6.
- No envelope member of the projection (`dialect`, `purpose`, `scientific_admissible`, `kind`) is copied into the candidate. `phase.py` `reject_b10_abrupt_science` (lines 279-307) rejects any candidate containing `dialect` `b10_abrupt` or `scientific_admissible` false anywhere, so the candidate must hold neither.
- The candidate holds every rule-13 field the evidence pack needs, because ephemeral Blob storage is destroyed with the run (ADR-020) and the workflow uploads only the candidate and the artifacts directory (`scenario.yml` lines 213-232 and 637-656).

### 3.6 Phase and chart

- P1. `B10_PHASE_MODES` becomes `("b10-sweep-line", "b10-final-sweep")`. After the repeats, the phase runs `b10-sweep-line`, then `b10-final-sweep`, then the existing `judge` step. `b10-sweep` is not run for scientific B10. The judge and candidate commands and the workflow are unchanged.
- P2. The lifecycle count in `b10_prepare_sweep` (`phase.py` line 923, today `len(observations) != 10`) becomes: C0, one producer observation per applied C0 producer Job (one per repeat under A2's one-Job case); C1, one `b10_lifecycle.producers` entry per applied producer Job of A1 (two or three per repeat) and one `b10_lifecycle.verifiers` entry per applied per-repeat verifier Job (A6). C0 verifier completions stay `U/C0/r{r}/verify-completion.json` with mode `verify`. Any `b10_lifecycle.gaps` entry, or a count that differs from the applied Jobs in `phase-status.json`, returns `lifecycle_evidence_gap`. The C1 settle input `b10_last_verifier_completed_utc` is the latest `finished_utc` of the C1 verifier entries; when no C1 verifier Job was applied, it is the latest producer `finished_utc`, so the sweep-line codec's ordering rule still holds. The settle rule (`phase.py` line 927, 60 s after the last producer termination or the last verifier completion, if later) is unchanged.
- P3. The chart renders `b10-final-sweep` with the options and deadline of 3.3, a verifier-role Job on `workload`, run IDs matching `^b10-[0-9]+-[0-9]+$` only, scenario B10, phase C0 or C1, and no repeat. `values.schema.json` adds the mode to its enums and rejects it for trial run IDs.
- P4. For B10 C1, `judge` and `candidate` no longer record `b10_c1_final_judgement_unavailable` (A8): C1 runs `b10-sweep-line`, `b10-final-sweep` and `judge` as P1 states, and the candidate step downloads and checks the C1 candidate as it does for C0.

## 4. Requirements

Verifier requirements are observed at the real handler through the mode's entry point, reading the object it writes.

- R1. WHEN all five C1 repeats are eligible pending the sweep and no late record exists, THEN every repeat is `eligible`, its counts equal the A3 object's, and `scan_complete` is true.
- R2. WHEN a cross-restart resend copy lands in repeat r's late range only, THEN repeat r's `resends_seen`, `pair_count` and `comparison` include it, and its member `resend_lost` is false.
- R3. WHEN repeat r's A3 object has problem 9 only and the late range holds the resend copy, THEN repeat r is `eligible` and `resend_lost` is false; WHEN the late range holds no copy and the terminal is delivered, THEN `resend_lost` is true and repeat r is in `lost_resend_finding_repeats`.
- R4. WHEN a record of repeat 2's instance lands in repeat 3's original window, THEN it counts only in repeat 2 and repeat 3 is unaffected.
- R5. WHEN a record carrying no registered instance lands in an original window, THEN that repeat is `invalid` with `b10_final_owner_invalid`.
- R6. WHEN an owned late record joins no attempt, THEN the repeat gets `b10_final_late_record_unjoined` and, for C1, E15, and its affected counts are null.
- R7. WHEN the sweep line is missing, invalid or the drain is incomplete, THEN `scan_complete` is false, every repeat is nonfinal with null counts, every C1 repeat is `ineligible` with E14, and the C1 `rule_selection` is `held` with `fewer_than_three_eligible`.
- R8. WHEN the original-window part disagrees with the per-repeat input, THEN `b10_final_window_mismatch`, null counts and, for C1, E15.
- R9. WHEN a C1 repeat ended on the no-kill path, THEN it is `ineligible` with the `no_kill.reason` literal, its `arms` are null, and it is in no lost-resend list.
- R10. WHEN any field of the dropped list of 3.4 would be written, THEN the build fails its test (T12): the projection holds none of them.
- R11a. WHEN C0 repeats carry two producer registrations (A2, two-Job case), THEN each is `invalid` with `b10_final_repeat_input_invalid` and `timeout_rule.rule_selection` is `held`.
- R11. WHEN all five C0 repeats are final with a duplicate total of at least 1, THEN `timeout_rule.rule_selection` is `confirmed`; with total 0, `untestable`; with fewer than five final, `held` and `duplicate_total` null.
- R12. WHEN at least three C1 repeats are eligible and one has a qualifying pair, THEN `confirmed` with the full subject, AND every lost-resend finding is still listed.
- R13. WHEN at least three are eligible, none has a qualifying pair and an eligible repeat has a lost resend, THEN `held` with `eligible_loss_without_pair`.
- R14. WHEN at least three are eligible, none has a pair and no eligible repeat has a loss, THEN `untestable` with `no_cross_restart_duplicate_observed`, whatever the ineligible repeats' losses.
- R15. WHEN fewer than three are eligible, THEN `held` with `fewer_than_three_eligible`, whatever the pairs.
- R16. WHEN the judge runs for B10 with a run ID that does not match `^b10-[0-9]+-[0-9]+$`, THEN it takes the legacy path and its behaviour and candidate bytes are unchanged.
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
| T6 foreign owner | T1 plus a record with `instance-id` `foreign` inside repeat 4's window | repeat 4 `invalid`, reasons [`b10_final_owner_invalid`], `ineligible` |
| T7 unjoined late | B, T1 plus a repeat-1 first-pod record in its late range with attempt `x` | repeat 1 `status` `invalid`, `classification_final` false, `timeout` null, `reasons` [`b10_final_late_record_unjoined`], `eligibility` `ineligible`, `eligibility_reasons` [`b10_restart_late_record_unjoined`], every sweep-derived arm count null, `comparison` `unavailable`; repeats 2 to 5 as T1 |
| T8 sweep missing | B without `sweep-line.json`; second subcase drain budget below the window | `scan_complete` false; every repeat `invalid`, `classification_final` false, `timeout` null, `reasons` [`b10_final_sweep_line_invalid`] (second subcase [`b10_final_drain_incomplete`]), `eligibility_reasons` [`b10_restart_sweep_unavailable`]; `rule_selection` `held`, `fewer_than_three_eligible`; root `status` `invalid` |
| T9 mismatch | T1 with repeat 3's A3 object rewritten (rehashed) to `timeout.duplicate` one higher | repeat 3 `invalid`, `timeout` null, `reasons` [`b10_final_window_mismatch`], `eligibility_reasons` [`b10_restart_late_record_unjoined`] |
| T10 no-kill | T1 with repeat 5 a no-kill end, `no_kill.reason` `kill_too_early`, no A3 object | repeat 5 `status` `incomplete`, `timeout` null, `reasons` [`b10_final_window_unavailable`], `termination` `{class: no_kill, outcome: kill_too_early}`, `eligibility_reasons` [`kill_too_early`], `arms` null, `comparison` `unavailable`, in no lost list; root `status` `incomplete` |
| T11 thresholds | T1 with 3 eligible (repeats 4, 5 ineligible by E13) and pairs only in ineligible repeats; then 2 eligible | 3 eligible, no pair, no eligible loss: `untestable`, `no_cross_restart_duplicate_observed`; 2 eligible: `held`, `fewer_than_three_eligible` |
| T12 dropped fields | T1 output bytes | none of 3.4's dropped member names occurs at any depth; closed members exactly as 3.4 |
| T13 eligible loss, no pair | 3 eligible, none paired, one eligible lost resend | `held`, `eligible_loss_without_pair` |
| T14 C0 rule | The judge's B10 final path given literal projection repeat entries (C0, five `complete_valid` final repeats with `timeout.duplicate` [0,0,1,0,0]; then all 0; then repeat 3 `invalid`) | `confirmed` with `duplicate_total` 1; `untestable` with 0; `held` with null |
| T14b C0 handler path | B with one-producer C0 `verify` output from real handlers | as T14. Runs only after the pull request Q2 selects supplies that output; until then it is not in this pull request (9(c)) |
| T15 C0 two producers | B, C0 repeats with two registrations each (A2 two-Job case) | every repeat `invalid`, `b10_final_repeat_input_invalid`; `timeout_rule` `held` (R11a) |
| T16 ownership global | B, one instance ID registered in repeats 1 and 2 | `scan_complete` false, `b10_final_ownership_invalid` |
| T17 judge path | T1 projection and receipt | candidate `b10_final.repeats` JSON-equal to the projection's; no `dialect`, `purpose`, `scientific_admissible` member; `check_candidate` in `phase.py` accepts the bytes (run by the phase test TP3) |
| T18 judge untrusted | T1 with the receipt's SHA-256 changed; second subcase projection identity phase changed | candidate `invalid`, `b10_final_projection_untrusted`, `b10_final` null |
| T19 legacy unchanged | existing legacy B10 judge fixtures | candidate bytes equal to `main`'s for the same inputs |
| T20 trial rejected | options with run ID `b10d-1-1` | exit 1, no read |
| T21 vectors | B with repeat 2's start line below repeat 1's end line | `scan_complete` false, `b10_final_vector_invalid` |
| T22 old readers reject (P2-A of 6846147228) | T1's `b10-final-sweep.json` bytes | `VerifierResultCodec.Read` returns `IsSuccess` false; `B10SweepEvidenceCodec.ReadFinalization` throws; `PhaseCandidateCodec.Read` throws `FormatException` (ADR-021 protocol rule 14) |
| T23 kill too late | T1 with repeat 4 an intended self-kill with an empty kill set | repeat 4 `termination` `{class: intended_self_kill, outcome: kill_too_late}`, `eligibility_reasons` [`kill_too_late`], `arms` null |
| T24 token provider | The host built for `b10-final-sweep` | resolves the sign-in fix's abrupt-mode token provider with that fix's credential options, as that fix's own host test asserts for `b10-abrupt-verify` |

Phase tests (`test_phase.py`):
- TP1: the scientific B10 sequence after the repeats is `b10-sweep-line`, `b10-final-sweep`, then `judge`; `b10-sweep` never appears.
- TP2: C1 with repeat 3's successor not started gives 14 producer observations accepted (5 times 3 less 1, computed by script); one missing observation gives `lifecycle_evidence_gap`. C0 with five observations accepted; six or four give `lifecycle_evidence_gap`.
- TP3: `check_candidate` accepts T17's candidate bytes, and rejects the same bytes with `"dialect": "b10_abrupt"` added inside `b10_final`.

Chart test:
- TC1: a rendered `b10-final-sweep` for `b10-1-1`, C1 has the options of 3.3 and deadline 750; the same values with `b10d-1-1` fail the schema.

Each T row asserts every other check still passes in that subcase. The receipt records the predicate map below, at and above each boundary.

## 7. Deployment assumptions (live-only)

"Cold" means the cluster and ephemeral layer were created in that run. "Idle" means no producer was sending. No scientific three-Job run has happened, so no assumption below has live evidence of its own. None is accepted; each is open under Q7.

| ID | Assumption | Evidence, and how it was taken | If false |
|---|---|---|---|
| LA-F1 | Every repeat's records stay readable in `document-events` until the sweep ends, up to the 18000 s orchestration deadline (`phase.py` line 28) | None for a five-repeat three-Job phase. The trial run 43322351104 read one repeat's complete window (6838450660 states `window_complete` true). That its verifier used a fresh connection on an idle namespace is inferred from the trial order (verify runs after the successor; `b10-abrupt-contract.md` "Trial order"), not stated | Drain incomplete, `scan_complete` false, every repeat `invalid` |
| LA-F2 | One `b10-final-sweep` Job drains five original windows and five late ranges within `verify_job_seconds` 750 and 9300000 records | Not measured. The trial's one C1 window held 30945 records (6838450660); five repeats plus late ranges is not measured. Policy values only | Job fails; no projection; candidate `invalid` |
| LA-F3 | A record Event Hubs acknowledged before the sweep line capture lies below the captured high watermark | Same gap as the PR3c plan's LA-3b. One sample (6838450660): both arms' delivered resends were consumed inside the window read at verify start. That the successor used a fresh connection with no warm-up is stated by ADR-021 protocol rule 8 ("The successor sends no warm-up"); that the namespace was idle is inferred from the trial order. No source states the assumption (3.2) | An acknowledged copy is missed; a false `resend_lost` true. The residual-risk sentence covers appends after the line, not this |
| LA-F4 | The settle interval of 60 s after the last producer termination leaves no append of the phase's instances after the sweep line | Not an assumption the plan can test: ADR-021 accepts this residual risk for B10 and every publication states it | Disclosed, not detected |
| LA-F6 | In the deployed verifier pod, `b10-final-sweep`'s token acquisition completes inside `TokenTimeout` 10 s with the sign-in fix's settings (3.3): a healthy token call completes within `NetworkTimeout` 2.0 s, and after one stall the single retry does | The sign-in fix's DA-1 and DA-2, accepted by name in 6845385609 points 2 and 3 for that fix, on Application Insights evidence over 30 days, all pods. Fresh connection at pod start; idle namespace for this mode is inferred, because it runs after every producer | Job fails before the drain; no projection; candidate `invalid` |
| LA-F5 | The verifier identity lists and reads the A5 prefixes for repeats 1 to 5 | `Storage Blob Data Reader` on the ledger container (`infra/ephemeral/identity.tf` line 13); exercised for `r1` only, in the trials (6838450660), fresh pod, idle | `b10_final_repeat_input_invalid`, E17 |

## 8. Acceptance commands

| # | Command | Evidence |
|---|---|---|
| A1 | `./scripts/verify-dotnet.sh` | `Verification passed.`; reports include T0 to T24, and the architecture tests pass unchanged |
| A2 | `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` and the phase suite `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'` | `OK`; includes TP1 to TP3 |
| A3 | `bash infra/scripts/lifecycle/check-offline.sh` | exit 0; includes TC1 |
| A4 | `npm run check:docs` | each warning fixed or explained (docs/agents/pull-requests.md rule 8) |
| A5 | `git diff --name-only origin/main...HEAD` | a subset of 2.1, checked by script against 2.2 |
| A6 | Against (a)'s merged `main`: a script that lists, for each A1 to A8 item, the file and line where (a) implements it | every item found; any difference stops implementation (section 12) |

## 9. Caps and plan-check answers

Caps per implementing session, under 6845612916 point 2: 20 verification-failure root causes and 20 review-finding root causes. Exhaustion of either: `push -wip and hand off to the next session` (AGENTS.md "Before opening a pull request" rule 11).

(a) Steps whose effort scales with a number unknown at planning time:
- Records drained: bounded by `MaxConsumedRecords` 9300000 and `MaxConsumedBytes` (3.3). Exhaustion is F8, a named global failure, not more work.
- Ledger rows and journal records per repeat: the existing per-file limits (`b10-abrupt-contract.md` check 5; `MaxRowsPerArtifact`).
- Reasons per repeat: at most 100 (3.4).
- Implementer root causes: the caps above.
- Fixture changes beyond the 2.3 inventory, when (a)'s merged tests differ: proposed cap 40 test cases, exhaustion `push -wip and hand off to the next session` (Q11). Until Hari sets it, AGENTS.md "Before opening a pull request" rule 11 stops the implementer at the first measurement.

(b) Where this plan is stricter than its parent (item plan Revision 5 and ADR-021):
- F12 re-classifies the original window and requires agreement with the per-repeat object, which ADR-021 does not require.
- F6 re-authenticates each repeat's producer evidence instead of trusting the per-repeat object.
- F5 makes a cross-repeat instance collision a global failure, not a per-repeat one.
- Eligibility also requires F11 and F12 to add nothing for the repeat.

(c) Tests or checks that depend on code, data or behaviour that is neither on `main` nor in this pull request: everything in 3.1 (A1 to A8) depends on (a), checked here only against (a)'s unapproved Revision 2 text, not against code or recorded output; A6 checks them after (a) merges. F3 and T14b depend on one-producer C0 `verify` output that exists nowhere yet (Q2). The token provider of 3.3 and T24 depend on the unmerged sign-in fix. T2 needs the per-repeat handler to record problem 9, which needs a high watermark that moves between its two reads, as the PR3c plan's T24 already arranges. B builds A3 objects with the real `b10-abrupt-verify` handler for repeats other than 1, which depends on A7. Live behaviour is LA-F1 to LA-F5.

## 10. Sources

- ADR-021 "Retries", "Classification", "Late-write scan", decision rules 1 to 7, protocol rules 6, 7, 10, 13 and 14, and "Consequences" (`docs/blueprint.md` lines 659-733 on `main` at `675f8372`). ADR-012 classes and B10 exceptions (lines 367-430). ADR-013 high-watermark window (line 431 onward). ADR-019 publication of checked fields. ADR-020 retention.
- `docs/operations/b10-abrupt-contract.md` "Diagnostic verifier mode", checks 3, 16, 19, 20 and 29, "Output `nonfinal_result`", "Problems and unavailable fields", "Eligibility reasons" and "Failure names and collection".
- `docs/operations/b10-workflow.md` lines 10-19 and 41-51; `docs/operations/verifier-contract.md` B10 phase sweep, judge and candidate sections.
- `src/Parchwell.Verifier/VerifyRun/B10SweepHandler.cs` lines 47-131 and 157-239; `VerifyRun/Judgement/B10LateWriteChecks.cs` lines 16-76; `VerifyRun/Input/B10PhaseRegistrationReader.cs` line 42; `Judge/JudgeHandler.cs` lines 356-419; `Judge/PhaseJudge.cs` lines 65-104; `Judge/PhaseCandidateCodec.cs` lines 18-19; `Common/Phase/B10SweepEvidence.cs` line 8; `Common/Phase/PhaseIdentity.cs` lines 24-38; `Common/Phase/PhaseWireCodec.cs` line 157; `VerifyRun/Abrupt/B10AbruptVerifyHandler.cs` lines 47 and 241-272; `VerifierHostOptions.cs` lines 151-162.
- `infra/scripts/phase/phase.py` lines 26-29, 279-307, 466, 816-850, 905-971 and 1354-1388; `charts/parchwell/values.yaml` lines 49-54 and 95-100; `values.schema.json` lines 930-931; `.github/workflows/scenario.yml` lines 77, 199-232, 449-468 and 622-656; `.github/CODEOWNERS` lines 5 and 16; `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` lines 17-35.
- Confluent.Kafka v2.16.0 `WatermarkOffsets.cs` (3.2); Microsoft Learn Event Hubs Kafka troubleshooting guide (3.2).
- Reader reports from two subagents on `main` at `675f8372`; each claim used here was checked at its cited line in this session.

## 11. Open questions for Hari

- Q1. Implementing tool (6845612916 point 2). Options: A Codex; B Claude Code. Recommendation: A, because Codex wrote the `b10-abrupt-verify` readers this reuses (PR3b, PR3c). Counterargument: Codex also reviews, so A keeps `same-vendor-review` on a protected verifier change.
- Q2. C0 shape (A2). ADR-021 protocol rule 1 requires one ordinary C0 producer Job; today C0 runs `loadgen` and `loadgen2` (`phase.py` line 26), and (a) Revision 2 keeps that under its own Q2 option B; its option A would move C0 to one Job and change C5 targets (`B10IntakeChecks.cs`, `B10PhaseRegistrationReader.cs`, `PublicationAcceptance.cs`). Options: A item (a) moves C0 to one Job (its Q2 option A); B this pull request does it, adding the C0 Job, start-line and verify changes and their tests to section 2.1; C C0 stays two-Job, so this plan marks every C0 repeat invalid and the timeout rule `held` (R11a) until a later pull request. Recommendation: A, because (a) already rewrites the per-repeat sequence and the start-line names. Counterargument: 6845612916 names only the C1 move for (a), and A widens (a).
- Q3. C5 mutation targets. Options: A new code only (2.1 item 1), every listed C5 file unchanged, and the new files added to the `mutate` list, which also changes `c5_workflow.py`, `test_c5_workflow.py` and the matrix rows of the protected `.github/workflows/verifier-c5-mutation.yml` (2.1 item 6), so the post-merge mutation run covers them (planned); B new code only, not added to the list; C change the legacy C5 files in place (`B10SweepHandler`, `PhaseJudge`, `B10JudgeChecks`, `B10PhaseRegistrationReader`). Recommendation: A, because the final judgement is the code that selects published rule inputs and should carry the same mutation bar as the legacy sweep. Counterargument: A leaves a dead legacy `b10-sweep` path in the code and the chart, and a passing mutation run is one more gate before any evidence pack.
- Q4. The projection's `purpose` literal `"scientific"` and `scientific_admissible` true when the scan is complete (3.4). These are new values of members the diagnostic contract fixes at `"diagnostic_trial"` and false. Options: A as planned; B a different literal you name. Recommendation: A.
- Q5. New literals: E14 `b10_restart_sweep_unavailable`, E15 `b10_restart_late_record_unjoined`, E17 `b10_restart_input_unavailable`; E16 reusing the `no_kill.reason` literal; the eight `b10_final_*` repeat reasons; `b10_final_projection_untrusted`; and the selection reasons `fewer_than_three_eligible` and `eligible_loss_without_pair`. `no_cross_restart_duplicate_observed` is ADR-021's own. Options: A accept as listed; B rename or merge. Recommendation: A. Counterargument: E15 covers both F11 and F12, so it does not say which failed; the repeat `reasons` do.
- Q6. Rule selection in the candidate (3.5). Options: A the candidate carries `rule_selection`, `selection_reason` and `subject`, computed by ADR-021 decision rules 1 to 5 exactly as 3.5 states (planned); B the candidate carries only the rule inputs (counts, eligibility, pair and loss lists), and the verdict pull request derives the selection by hand. Recommendation: A, because a script, not a person, should apply a fixed rule (docs/agents/pull-requests.md rule 4), and the verdict still needs your approval. Counterargument: B keeps every verdict word out of verifier code; ADR-021 decision rule 1 also says "The expected outcomes must adopt this cohort projection before publication", which #356 has not done.
- Q7. Accept by name, or reject, LA-F1, LA-F2, LA-F3, LA-F5 and LA-F6 (section 7). LA-F6 extends the sign-in fix's DA-1 and DA-2 to `b10-final-sweep`. Recommendation: accept none now, and accept each for the first scientific run only after (a)'s plan states its own live assumptions, because no scientific three-Job run exists. Counterargument: until accepted, this plan is not approvable under docs/agents/factory.md "Item lifecycle" step 1.
- Q8. Contract with (a) (section 3.1). A1 to A8 rest on (a)'s unapproved Revision 2; A4's termination-receipt point is assumed. If (a) puts the C1 lifecycle records or the C1 judge gap in `b10_abrupt.py`, 2.1 item 7 lets this plan change them there. Options: A approve this plan only after (a)'s plan is approved and a revision of this plan has reconciled 3.1 with the approved text; B approve now and let A6 catch differences at implementation. Recommendation: A, because 3.1 pins paths and formats that (a) owns. Counterargument: A serializes the two approvals, though not the two drafts, which "Parallel, start both now" asked for.
- Q9. No receipt for the per-repeat C1 object (A3). The sweep cannot bind the C1 object to a verifier completion, so F2 makes no C1 verifier-time check and relies on the phase's settle rule. Options: A accept that (planned); B ask (a) to add a create-only `WorkerReceipt` for the scientific `b10-abrupt-verify` Job, which F2 and F4 would then check. Recommendation: B, because every other B10 phase input is receipt-bound, and the sweep line's `last_verifier_completed_utc` then has an authenticated source. Counterargument: B changes (a)'s 3.6 and the abrupt contract's "writes no `WorkerReceipt`".
- Q10. Live smoke (docs/agents/factory.md "Item lifecycle" step 1: "A plan that changes live infrastructure is not approvable until a live smoke run of that design exists"). This plan adds a verifier Job and changes phase orchestration and chart rendering, but no Terraform. Options: A the rule does not apply, because no infrastructure changes; B it applies, and the first scientific B10 run after merge is that smoke, never published; C a smoke run is required before approval. Recommendation: A. Counterargument: item (a) raises the same question as its Q5, and the two answers should agree.
- Q11. Fixture-change cap (9(a)). Options: A 40 test cases beyond the 2.3 inventory, exhaustion `push -wip and hand off to the next session`, matching (a)'s own Q9 proposal; B another number. Recommendation: A. Counterargument: 40 is a policy choice, not a measurement.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change:
- a contract of section 3 or a requirement of section 4;
- the scope of section 2, including any C5 target;
- or when A6 shows any of A1 to A8 differs from (a)'s merged implementation.

Predecessors: item (a) and the verifier sign-in fix must both have merged before implementation starts; the implementer verifies both by script.

Hitting a section 9 cap pushes `-wip` and hands off to the next session. Approval is Hari's `## Approval` naming this revision and commit; standing approval does not apply (6845612916 point 4). A revision gets one fresh `plan-checker` when it is design-changing under docs/agents/factory.md "Item lifecycle" step 1.

## 13. Plan branch hygiene and revision of (a) read

This file is committed alone, on top of the remote tip of `claude/404-plan`, with no rebase or force-push, and its SHA-256 on the remote is verified by script (6845612916 point 3). Revision of (a)'s plan read: for Revision 1 of this plan, (a) Revision 1 at `009613aba2aae789ca6c187140b56f18ea5ef134` (file SHA-256 `dc3b01574dc5ad8b36dcd54f150eb0d9d0de471528665da3cd6151be9e6402e5`); for this Revision 2, (a) Revision 2 at `0fb9caa42abf82b205c7ddb18a2ce3e46d328abc` (file SHA-256 `d571ff460e39a93bc4f2bd63ed57f1989ddc5cee4df10b8563395f0614f359b9`), each verified by script.
